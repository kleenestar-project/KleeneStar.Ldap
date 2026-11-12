using Novell.Directory.Ldap;
using Novell.Directory.Ldap.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// The directory a configured connection talks to, over the fully managed
    /// Novell.Directory.Ldap client - LDAP, LDAP with StartTLS and LDAPS alike, on every platform
    /// .NET runs on and without a native LDAP library.
    /// </summary>
    /// <remarks>
    /// Every operation opens its own connection and closes it again: a sign-in is rare enough
    /// that a pooled connection would mostly be a stale one, and a connection carrying a user's
    /// bind must never serve the next request. Referrals are not chased - a search below an
    /// Active Directory domain root answers continuation references to its application
    /// partitions, which are skipped rather than failing the search.
    /// </remarks>
    /// <param name="settings">The settings of the connection.</param>
    public sealed class LdapDirectory(LdapSourceSettings settings) : ILdapDirectory
    {
        /// <summary>
        /// The size of a page of the synchronization's search. Active Directory answers at most
        /// 1000 entries per page by default.
        /// </summary>
        public const int PageSize = 500;

        /// <summary>
        /// The longest a synchronization search may take.
        /// </summary>
        private static readonly TimeSpan _searchBudget = TimeSpan.FromMinutes(10);

        private readonly LdapSourceSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        /// <summary>
        /// Binds as an entry over a connection of its own.
        /// </summary>
        /// <param name="distinguishedName">The entry.</param>
        /// <param name="password">The password.</param>
        /// <returns><see langword="true"/> when the directory accepted the credentials; <see langword="false"/>
        /// for invalid credentials.</returns>
        /// <exception cref="LdapDirectoryException">The directory could not be reached or answered an error.</exception>
        public bool Bind(string distinguishedName, string password)
        {
            return Run(async ct =>
            {
                using var connection = await ConnectAsync(ct).ConfigureAwait(false);

                try
                {
                    await connection.BindAsync(distinguishedName, password, ct).ConfigureAwait(false);

                    return connection.Bound;
                }
                catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
                {
                    return false;
                }
            }, OperationBudget);
        }

        /// <summary>
        /// Searches as the service account, for the sign-in, and answers at most two entries -
        /// enough to tell exactly one from none and several. Continuation references are skipped.
        /// </summary>
        /// <param name="searchBase">Where to start.</param>
        /// <param name="filter">The filter, its values escaped.</param>
        /// <returns>The distinguished names found.</returns>
        /// <exception cref="LdapDirectoryException">The directory could not be reached or refused the service account.</exception>
        public IReadOnlyList<string> Search(string searchBase, string filter)
        {
            return Run<IReadOnlyList<string>>(async ct =>
            {
                using var connection = await ConnectAsServiceAsync(ct).ConfigureAwait(false);

                var constraints = new LdapSearchConstraints { ReferralFollowing = false };
                var results = await connection.SearchAsync(searchBase, LdapConnection.ScopeSub, filter, ["1.1"], false, constraints, ct).ConfigureAwait(false);
                var found = new List<string>();

                while (found.Count < 2 && await results.HasMoreAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        found.Add((await results.NextAsync(ct).ConfigureAwait(false)).Dn);
                    }
                    catch (LdapReferralException)
                    {
                        // a continuation reference names no entry
                    }
                }

                return found;
            }, OperationBudget);
        }

        /// <summary>
        /// Searches as the service account and answers every entry with the requested attributes,
        /// page by page (<see cref="PageSize"/>) where the directory supports paged results.
        /// </summary>
        /// <param name="searchBase">Where to start.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="attributes">The attributes to read.</param>
        /// <returns>The entries found.</returns>
        /// <exception cref="LdapDirectoryException">The directory could not be reached or refused the service account.</exception>
        public IReadOnlyList<LdapDirectoryEntry> SearchEntries(string searchBase, string filter, IReadOnlyCollection<string> attributes)
        {
            return Run<IReadOnlyList<LdapDirectoryEntry>>(async ct =>
            {
                using var connection = await ConnectAsServiceAsync(ct).ConfigureAwait(false);

                var names = attributes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
                var entries = new List<LdapDirectoryEntry>();
                var cookie = SimplePagedResultsControl.GetEmptyCookie;
                var paged = true;

                while (true)
                {
                    var constraints = new LdapSearchConstraints { ReferralFollowing = false, BatchSize = 0 };

                    if (paged)
                    {
                        constraints.SetControls(new SimplePagedResultsControl(PageSize, cookie));
                    }

                    ILdapSearchResults results;

                    try
                    {
                        results = await connection.SearchAsync(searchBase, LdapConnection.ScopeSub, filter, names, false, constraints, ct).ConfigureAwait(false);

                        await ReadAsync(results, entries, ct).ConfigureAwait(false);
                    }
                    catch (LdapException ex) when (paged && entries.Count == 0 && ex.ResultCode == LdapException.UnavailableCriticalExtension)
                    {
                        // a directory without paged results answers the whole set at once
                        paged = false;

                        continue;
                    }

                    if (!paged)
                    {
                        return entries;
                    }

                    var control = results.ResponseControls?.OfType<SimplePagedResultsControl>().FirstOrDefault();

                    // a directory that ignored the control answered everything in the first page
                    if (control is null || control.IsEmptyCookie())
                    {
                        return entries;
                    }

                    cookie = control.Cookie;
                }
            }, _searchBudget);
        }

        /// <summary>
        /// Checks the connection: connects with the configured transport, binds the service
        /// account where one is named and reads the search base where one is named.
        /// </summary>
        /// <returns>What was checked.</returns>
        /// <exception cref="LdapDirectoryException">The check failed; the message says where.</exception>
        public LdapProbeResult Probe()
        {
            return Run(async ct =>
            {
                using var connection = await ConnectAsServiceAsync(ct).ConfigureAwait(false);

                var secured = _settings.Protocol != LdapProtocol.Ldap;
                var bound = !string.IsNullOrWhiteSpace(_settings.ServiceDn) && connection.Bound;
                bool? found = null;

                if (!string.IsNullOrWhiteSpace(_settings.SearchBase))
                {
                    try
                    {
                        var constraints = new LdapSearchConstraints { ReferralFollowing = false };
                        var results = await connection.SearchAsync(_settings.SearchBase, LdapConnection.ScopeBase, "(objectClass=*)", ["1.1"], false, constraints, ct).ConfigureAwait(false);

                        found = await results.HasMoreAsync(ct).ConfigureAwait(false);
                    }
                    catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject)
                    {
                        found = false;
                    }
                }

                return new LdapProbeResult(secured, bound, found);
            }, OperationBudget);
        }

        /// <summary>
        /// Gets how long a sign-in check or a connection check may take, connecting included.
        /// </summary>
        private TimeSpan OperationBudget => TimeSpan.FromSeconds(Math.Clamp(_settings.Timeout, 1, 300) * 3);

        /// <summary>
        /// Reads the entries of one page, skipping continuation references.
        /// </summary>
        /// <param name="results">The search results.</param>
        /// <param name="entries">The entries read so far.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task.</returns>
        private static async Task ReadAsync(ILdapSearchResults results, List<LdapDirectoryEntry> entries, CancellationToken ct)
        {
            while (await results.HasMoreAsync(ct).ConfigureAwait(false))
            {
                LdapEntry entry;

                try
                {
                    entry = await results.NextAsync(ct).ConfigureAwait(false);
                }
                catch (LdapReferralException)
                {
                    continue;
                }

                var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var attribute in entry.GetAttributeSet())
                {
                    attributes[attribute.Name] = attribute.StringValue;
                }

                entries.Add(new LdapDirectoryEntry(entry.Dn, attributes));
            }
        }

        /// <summary>
        /// Opens a connection and binds the service account, or stays anonymous when none is
        /// named.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The connection.</returns>
        private async Task<LdapConnection> ConnectAsServiceAsync(CancellationToken ct)
        {
            var connection = await ConnectAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(_settings.ServiceDn))
            {
                return connection;
            }

            try
            {
                await connection.BindAsync(_settings.ServiceDn, _settings.ServicePassword ?? string.Empty, ct).ConfigureAwait(false);

                return connection;
            }
            catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
            {
                connection.Dispose();

                throw new LdapDirectoryException("The directory refused the service account (invalid credentials).", ex);
            }
            catch
            {
                connection.Dispose();

                throw;
            }
        }

        /// <summary>
        /// Opens a connection, encrypted as the settings say.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The connection.</returns>
        private async Task<LdapConnection> ConnectAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_settings.Server))
            {
                throw new LdapDirectoryException("No directory server is configured.");
            }

            var options = new LdapConnectionOptions();

            if (_settings.UseSsl)
            {
                options.UseSsl();
            }

            if (_settings.AllowUntrustedCertificate)
            {
                // a test directory with a self-signed certificate; the settings page warns about it
                options.ConfigureRemoteCertificateValidationCallback((_, _, _, _) => true);
            }

            var connection = new LdapConnection(options)
            {
                ConnectionTimeout = Math.Clamp(_settings.Timeout, 1, 300) * 1000
            };

            try
            {
                await connection.ConnectAsync(_settings.Server.Trim(), _settings.EffectivePort, ct).ConfigureAwait(false);

                if (!_settings.UseSsl && _settings.StartTls)
                {
                    await connection.StartTlsAsync(ct).ConfigureAwait(false);
                }

                return connection;
            }
            catch
            {
                connection.Dispose();

                throw;
            }
        }

        /// <summary>
        /// Runs an operation within a time budget and turns every failure of the directory into
        /// an <see cref="LdapDirectoryException"/> an administrator can read.
        /// </summary>
        /// <typeparam name="T">The type of the answer.</typeparam>
        /// <param name="operation">The operation.</param>
        /// <param name="budget">How long it may take.</param>
        /// <returns>The answer.</returns>
        private T Run<T>(Func<CancellationToken, Task<T>> operation, TimeSpan budget)
        {
            using var cancellation = new CancellationTokenSource(budget);

            try
            {
                // off the caller's context: the sign-in and the pages are synchronous
                return Task.Run(() => operation(cancellation.Token), cancellation.Token).GetAwaiter().GetResult();
            }
            catch (LdapDirectoryException)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                throw new LdapDirectoryException($"The directory {Endpoint} did not answer within {(int)budget.TotalSeconds} seconds.", ex);
            }
            catch (LdapException ex)
            {
                var detail = string.IsNullOrWhiteSpace(ex.LdapErrorMessage) ? ex.Message : $"{ex.Message} ({ex.LdapErrorMessage.Trim()})";

                throw new LdapDirectoryException($"The directory {Endpoint} answered: {detail}", ex);
            }
            catch (AuthenticationException ex)
            {
                throw new LdapDirectoryException($"The TLS handshake with {Endpoint} failed: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or InvalidOperationException or ArgumentException)
            {
                throw new LdapDirectoryException($"The directory {Endpoint} could not be reached: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the address of the directory, for messages.
        /// </summary>
        private string Endpoint => $"{(_settings.UseSsl ? "ldaps" : "ldap")}://{_settings.Server}:{_settings.EffectivePort}";
    }
}
