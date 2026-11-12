using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// Brings the accounts of a connection in step with the directory: creates an account for
    /// every directory user that has none, copies name, e-mail, department, position and phone
    /// onto the accounts, and disables the accounts the directory no longer has or has disabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An account belongs to a connection when it carries the connection's key as its sign-in
    /// source. It is matched by its <see cref="Identity.ExternalSubject"/> - the directory login,
    /// which is also what the sign-in binds with - and, the first time, by its user name: an
    /// account an administrator prepared for the source without a subject is claimed. An
    /// account of another source (an internal one, say) that carries the login as its user name
    /// is never taken over; the entry is counted as a conflict and left to an administrator.
    /// </para>
    /// <para>
    /// Nothing is ever deleted, and the sync only ever moves an account between
    /// <see cref="IdentityState.Active"/> and <see cref="IdentityState.Disabled"/>: a
    /// <see cref="IdentityState.Locked"/> account was locked by an administrator and stays so.
    /// A directory that answers no user at all disables nothing - that is a wrong filter or
    /// search base far more often than an empty company - and a run whose entries all lack the
    /// login attribute fails before it writes anything, because it could match no account and
    /// would otherwise disable all of them.
    /// </para>
    /// <para>
    /// The accounts are written through the identity manager, so the audit log records every
    /// change; the per-account toasts are silenced (<see cref="CoreHub.BeginSilentNotifications"/>).
    /// </para>
    /// </remarks>
    public static partial class LdapUserSynchronizer
    {
        /// <summary>
        /// The flag of Active Directory's <c>userAccountControl</c> that marks a disabled account.
        /// </summary>
        private const int AccountDisable = 0x2;

        /// <summary>
        /// Gets or sets how a directory is opened for a connection; replaced in tests.
        /// </summary>
        public static Func<LdapSourceSettings, ILdapDirectory> DirectoryFactory { get; set; } = settings => new LdapDirectory(settings);

        /// <summary>
        /// Returns what keeps a connection from being synchronized, or <see langword="null"/>.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <returns>The problem in English, or <see langword="null"/>.</returns>
        public static string Check(LdapSourceSettings settings)
        {
            if (settings is null)
            {
                return "The connection does not exist.";
            }

            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                return "The connection names no server.";
            }

            if (string.IsNullOrWhiteSpace(settings.SearchBase))
            {
                return "The synchronization needs a search base.";
            }

            if (string.IsNullOrWhiteSpace(settings.UserFilter))
            {
                return "The synchronization needs a user filter.";
            }

            if (string.IsNullOrWhiteSpace(settings.LoginAttribute))
            {
                return "The synchronization needs the attribute that carries the login.";
            }

            if (string.IsNullOrWhiteSpace(IdentitySource.Normalize(settings.Key)))
            {
                return "The connection needs a key other than 'local'.";
            }

            return null;
        }

        /// <summary>
        /// Synchronizes the accounts of a connection.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="componentHub">The component hub, for the log; may be null.</param>
        /// <returns>The outcome; a failure is reported, not thrown.</returns>
        public static LdapSyncResult Synchronize(LdapSourceSettings settings, IComponentHub componentHub = null)
        {
            var started = DateTime.UtcNow;
            var problem = Check(settings);

            if (problem is not null)
            {
                return LdapSyncResult.Fail(settings, started, problem);
            }

            // the subjects are recorded for a registered source only, the way the sign-in reads them
            if (CoreHub.AuthenticationSourceManager.GetSource(settings.Key) is not LdapAuthenticationSource)
            {
                return LdapSyncResult.Fail(settings, started, "The connection is not active: it is disabled, incomplete, or its key is used by another sign-in source.");
            }

            IReadOnlyList<LdapDirectoryEntry> entries;

            try
            {
                entries = DirectoryFactory(settings).SearchEntries(settings.SearchBase, settings.UserFilter, AttributesOf(settings));
            }
            catch (LdapDirectoryException ex)
            {
                componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP connection '{settings.Key}' could not be synchronized: {ex.Message}");

                return LdapSyncResult.Fail(settings, started, ex.Message);
            }

            // entries that all lack the login attribute - a misspelt attribute, a directory that
            // withholds it from the service account - match no account; carried on, every account
            // of the source would count as gone and be disabled
            if (entries.Count > 0 && !entries.Any(x => x.Get(settings.LoginAttribute) is not null))
            {
                var unusable = $"The directory answered {entries.Count} entries, but none carries the login attribute '{settings.LoginAttribute}'. Nothing was changed.";

                componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP connection '{settings.Key}' could not be synchronized: {unusable}");

                return LdapSyncResult.Fail(settings, started, unusable);
            }

            var result = new LdapSyncResult
            {
                ConnectionId = settings.ResolveId(),
                Key = settings.Key,
                Started = started,
                Found = entries.Count
            };

            try
            {
                Apply(settings, entries, result);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                componentHub?.LogManager?.DefaultLog?.Exception(ex);

                result.Succeeded = false;
                result.Error = ex.Message;
                result.Finished = DateTime.UtcNow;

                return result;
            }

            result.Succeeded = true;
            result.Finished = DateTime.UtcNow;

            componentHub?.LogManager?.DefaultLog?.Info($"The LDAP connection '{settings.Key}' was synchronized: {result.Found} found, {result.Created} created, {result.Updated} updated, {result.Disabled} disabled, {result.Enabled} enabled, {result.Conflicts} conflicts, {result.Skipped} skipped.");

            return result;
        }

        /// <summary>
        /// Writes the directory's users onto the accounts.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="entries">The users the directory answered.</param>
        /// <param name="result">The outcome being collected.</param>
        private static void Apply(LdapSourceSettings settings, IReadOnlyList<LdapDirectoryEntry> entries, LdapSyncResult result)
        {
            var key = IdentitySource.Normalize(settings.Key);
            var accounts = CoreHub.IdentityManager.GetIdentities(new Query<Identity>()).ToList();
            var bySubject = accounts
                .Where(x => x.AuthenticationSource == key && !string.IsNullOrWhiteSpace(x.ExternalSubject))
                .GroupBy(x => x.ExternalSubject.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            var userNames = new HashSet<string>(accounts.Where(x => !string.IsNullOrWhiteSpace(x.UserName)).Select(x => x.UserName.Trim()), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var silent = CoreHub.BeginSilentNotifications();

            foreach (var entry in entries)
            {
                var login = entry.Get(settings.LoginAttribute);

                if (login is null || !seen.Add(login))
                {
                    result.Skipped++;

                    continue;
                }

                var disabledThere = IsDisabled(entry);

                if (!bySubject.TryGetValue(login, out var account))
                {
                    var byName = accounts.Where(x => string.Equals(x.UserName?.Trim(), login, StringComparison.OrdinalIgnoreCase)).ToList();
                    var prepared = byName.FirstOrDefault(x => x.AuthenticationSource == key && string.IsNullOrWhiteSpace(x.ExternalSubject));

                    if (prepared is not null)
                    {
                        // an account an administrator created for the source before its first sign-in
                        if (!CoreHub.CredentialManager.LinkExternalSubject(prepared.Id, login))
                        {
                            result.Conflicts++;

                            continue;
                        }

                        prepared.ExternalSubject = login;
                        account = prepared;
                    }
                    else if (byName.Count > 0)
                    {
                        // an account of another sign-in source carries the login: never taken over
                        result.Conflicts++;

                        continue;
                    }
                }

                if (account is null)
                {
                    if (disabledThere)
                    {
                        result.Skipped++;

                        continue;
                    }

                    Create(settings, key, login, entry, userNames);
                    result.Created++;

                    continue;
                }

                Update(settings, account.Id, entry, disabledThere, result);
            }

            if (!settings.DisableMissing)
            {
                return;
            }

            if (seen.Count == 0)
            {
                // no usable login at all: a wrong filter, search base or login attribute, far
                // more likely than a directory without users
                return;
            }

            foreach (var missing in bySubject.Values.Where(x => !seen.Contains(x.ExternalSubject.Trim()) && x.State == IdentityState.Active))
            {
                var stored = CoreHub.IdentityManager.GetIdentity(missing.Id);

                if (stored is null || stored.State != IdentityState.Active)
                {
                    continue;
                }

                stored.State = IdentityState.Disabled;
                CoreHub.IdentityManager.Update(stored);
                result.Disabled++;
            }
        }

        /// <summary>
        /// Creates the account of a directory user.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="key">The normalized source key.</param>
        /// <param name="login">The login.</param>
        /// <param name="entry">The directory entry.</param>
        /// <param name="userNames">The user names taken so far; the new one is added.</param>
        private static void Create(LdapSourceSettings settings, string key, string login, LdapDirectoryEntry entry, HashSet<string> userNames)
        {
            var id = Guid.NewGuid();
            var userName = UniqueUserName(login, key, userNames);
            var name = entry.Get(settings.NameAttribute) ?? login;

            var account = new Identity(id)
            {
                Name = Clip(name, 128),
                UserName = userName,
                Email = entry.Get(settings.EmailAttribute) ?? string.Empty,
                Department = entry.Get(settings.DepartmentAttribute),
                Position = entry.Get(settings.PositionAttribute),
                Phone = entry.Get(settings.PhoneAttribute),
                State = IdentityState.Active,
                Avatar = GenerateIcon(id),
                AuthenticationSource = key
            };

            CoreHub.IdentityManager.Add(account);

            // the subject is a credential column, written on its own like every credential
            CoreHub.CredentialManager.LinkExternalSubject(id, login);
        }

        /// <summary>
        /// Copies a directory user onto its account.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="accountId">The account.</param>
        /// <param name="entry">The directory entry.</param>
        /// <param name="disabledThere">Whether the directory disabled the user.</param>
        /// <param name="result">The outcome being collected.</param>
        private static void Update(LdapSourceSettings settings, Guid accountId, LdapDirectoryEntry entry, bool disabledThere, LdapSyncResult result)
        {
            var stored = CoreHub.IdentityManager.GetIdentity(accountId);

            if (stored is null)
            {
                return;
            }

            var changed = false;

            changed |= Assign(entry.Get(settings.NameAttribute), stored.Name, x => stored.Name = Clip(x, 128));
            changed |= Assign(entry.Get(settings.EmailAttribute), stored.Email, x => stored.Email = x);
            changed |= Assign(entry.Get(settings.DepartmentAttribute), stored.Department, x => stored.Department = x);
            changed |= Assign(entry.Get(settings.PositionAttribute), stored.Position, x => stored.Position = x);
            changed |= Assign(entry.Get(settings.PhoneAttribute), stored.Phone, x => stored.Phone = x);

            if (disabledThere && stored.State == IdentityState.Active)
            {
                stored.State = IdentityState.Disabled;
                result.Disabled++;
            }
            else if (!disabledThere && stored.State == IdentityState.Disabled)
            {
                // disabled by an earlier run (or by hand, the same state); a lock stays
                stored.State = IdentityState.Active;
                result.Enabled++;
            }
            else if (!changed)
            {
                return;
            }
            else
            {
                result.Updated++;
            }

            CoreHub.IdentityManager.Update(stored);
        }

        /// <summary>
        /// Assigns a value the directory knows and the account does not carry yet. A value the
        /// directory leaves empty keeps what the account has.
        /// </summary>
        /// <param name="value">The directory's value.</param>
        /// <param name="current">The account's value.</param>
        /// <param name="assign">Writes the value.</param>
        /// <returns><see langword="true"/> when the value was written.</returns>
        private static bool Assign(string value, string current, Action<string> assign)
        {
            if (value is null || string.Equals(value, current?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            assign(value);

            return true;
        }

        /// <summary>
        /// Returns whether Active Directory marks the user disabled.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns><see langword="true"/> for a disabled user.</returns>
        private static bool IsDisabled(LdapDirectoryEntry entry)
        {
            return int.TryParse(entry.Get("userAccountControl"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var control)
                && (control & AccountDisable) != 0;
        }

        /// <summary>
        /// Returns the attributes the synchronization reads.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <returns>The attribute names.</returns>
        private static IReadOnlyCollection<string> AttributesOf(LdapSourceSettings settings)
        {
            return new[]
            {
                settings.LoginAttribute,
                settings.NameAttribute,
                settings.EmailAttribute,
                settings.DepartmentAttribute,
                settings.PositionAttribute,
                settings.PhoneAttribute,
                "userAccountControl"
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        /// <summary>
        /// Derives a user name no other account carries: the login itself where it is free,
        /// else the login qualified with the source key, else numbered.
        /// </summary>
        /// <param name="login">The login.</param>
        /// <param name="key">The source key.</param>
        /// <param name="taken">The user names taken; the result is added.</param>
        /// <returns>The user name.</returns>
        private static string UniqueUserName(string login, string key, HashSet<string> taken)
        {
            var baseName = Clip(UserNameInvalid().Replace(login.Trim(), "."), 100);
            var candidate = baseName;

            if (taken.Contains(candidate))
            {
                candidate = $"{baseName}.{key}";
            }

            for (var number = 2; taken.Contains(candidate); number++)
            {
                candidate = $"{baseName}.{key}{number}";
            }

            taken.Add(candidate);

            return candidate;
        }

        /// <summary>
        /// Generates the placeholder picture of a new account; an account without one is shown
        /// under the generic silhouette, so a failure must not stop the run.
        /// </summary>
        /// <param name="id">The account.</param>
        /// <returns>The picture, or <see langword="null"/>.</returns>
        private static WebExpress.WebUI.WebIcon.ImageIcon GenerateIcon(Guid id)
        {
            try
            {
                return CoreHub.GenerateIcon(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Shortens a text to a length.
        /// </summary>
        /// <param name="value">The text.</param>
        /// <param name="length">The length.</param>
        /// <returns>The text, at most that long.</returns>
        private static string Clip(string value, int length)
        {
            return value is not null && value.Length > length ? value[..length] : value;
        }

        /// <summary>
        /// Matches what a user name must not contain.
        /// </summary>
        [GeneratedRegex(@"[\s,;:/\\]+")]
        private static partial Regex UserNameInvalid();
    }
}
