using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// Signs accounts in against an LDAP directory or Active Directory: the password offered is
    /// checked by binding as the account's directory entry. Nothing about the password is kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The accounts exist before their first sign-in - created by an administrator or by the
    /// synchronization (<see cref="WebLdap.LdapUserSynchronizer"/>) - with this source and,
    /// where the directory login differs from the user name, the login as
    /// <see cref="Identity.ExternalSubject"/>. The source does not provision.
    /// </para>
    /// <para>
    /// An empty password is refused before the directory is asked: many directories answer a
    /// bind without one as an anonymous bind and call it success. The login is escaped for the
    /// distinguished name (RFC 4514) and the filter (RFC 4515), so a crafted name cannot widen a
    /// search or name another entry. A search that finds anything but exactly one entry signs
    /// nobody in.
    /// </para>
    /// </remarks>
    public sealed class LdapAuthenticationSource : IAuthenticationSource
    {
        /// <summary>
        /// The section of the plugin's settings the sources are read from.
        /// </summary>
        public const string Section = "Directories";

        /// <summary>
        /// The section of the plugin's settings that holds the secrets of the connections, keyed
        /// by the connection's key: <c>Secrets:{key}:ServicePassword</c>.
        /// </summary>
        /// <remarks>
        /// A secret kept outside the settings file - in the environment, as
        /// <c>WEBEXPRESS_Plugins__kleenestar.ldap__Secrets__{key}__ServicePassword</c> - is bound to
        /// the connection by its key, which cannot change while accounts carry it. Bound by its
        /// position in <see cref="Section"/> instead, it moved to the next connection whenever an
        /// earlier one was deleted, and a service password reached the wrong server.
        /// </remarks>
        public const string SecretsSection = "Secrets";

        private static readonly object _registrationSync = new();
        private static readonly HashSet<string> _registered = new(StringComparer.Ordinal);

        private readonly LdapSourceSettings _settings;
        private readonly ILdapDirectory _directory;
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Gets the key the accounts of this source carry: the connection's key, normalized.
        /// </summary>
        public string Key => IdentitySource.Normalize(_settings.Key);

        /// <summary>
        /// Gets the name the source is shown under: the connection's name.
        /// </summary>
        public string Name => _settings.Name;

        /// <summary>
        /// Gets the position of the source among the sign-in sources. Lower comes first.
        /// </summary>
        public int Order => _settings.Order;

        /// <summary>
        /// Gets whether the credentials are owned outside this installation: always, the directory
        /// owns them, so no password is set, changed or reset here.
        /// </summary>
        public bool IsExternal => true;

        /// <summary>
        /// Gets whether the sign-in form's password is checked by this source: always, by binding
        /// as the account's directory entry.
        /// </summary>
        public bool AcceptsPassword => true;

        /// <summary>
        /// Gets whether a sign-in that names no stored account creates one: never - the accounts
        /// come from an administrator or the synchronization.
        /// </summary>
        public bool Provisions => false;

        /// <summary>
        /// Gets the settings the source was built from.
        /// </summary>
        public LdapSourceSettings Settings => _settings;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="directory">The directory, or <see langword="null"/> for the configured server.</param>
        /// <param name="componentHub">The component hub, for the log; may be <see langword="null"/>.</param>
        public LdapAuthenticationSource(LdapSourceSettings settings, ILdapDirectory directory = null, IComponentHub componentHub = null)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var problem = Check(settings, directory is not null);

            if (problem is not null)
            {
                throw new ArgumentException(problem);
            }

            _settings = settings;
            _directory = directory ?? new LdapDirectory(settings);
            _componentHub = componentHub;
        }

        /// <summary>
        /// Returns what keeps a set of settings from making a source, or <see langword="null"/>
        /// when they make one. The settings page asks the same question before it stores a
        /// connection.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="hasDirectory">Whether a directory is supplied, so no server is needed.</param>
        /// <returns>The problem in English, or <see langword="null"/>.</returns>
        public static string Check(LdapSourceSettings settings, bool hasDirectory = false)
        {
            if (settings is null)
            {
                return "An LDAP source needs settings.";
            }

            if (string.IsNullOrWhiteSpace(settings.Key) || IdentitySource.IsLocal(settings.Key))
            {
                return $"An LDAP source needs a key other than '{IdentitySource.Local}'.";
            }

            if (!hasDirectory && string.IsNullOrWhiteSpace(settings.Server))
            {
                return "An LDAP source needs a server.";
            }

            if (string.IsNullOrWhiteSpace(settings.BindDnFormat) && (string.IsNullOrWhiteSpace(settings.ServiceDn) || string.IsNullOrWhiteSpace(settings.SearchBase)))
            {
                return "An LDAP source needs a bind format, or a service account and a search base.";
            }

            if (!string.IsNullOrWhiteSpace(settings.BindDnFormat) && !settings.BindDnFormat.Contains("{0}", StringComparison.Ordinal))
            {
                return "The bind format of an LDAP source must contain {0} for the login.";
            }

            if (string.IsNullOrWhiteSpace(settings.BindDnFormat) && (string.IsNullOrWhiteSpace(settings.SearchFilter) || !settings.SearchFilter.Contains("{0}", StringComparison.Ordinal)))
            {
                return "The search filter of an LDAP source must contain {0} for the login.";
            }

            return null;
        }

        /// <summary>
        /// Checks a password by binding as the account's directory entry - found by the direct bind
        /// format, or searched by the service account. The login is the account's external subject,
        /// else its user name; an empty password is refused before the directory is asked.
        /// </summary>
        /// <param name="account">The stored account the sign-in named.</param>
        /// <param name="password">The offered password.</param>
        /// <returns><see langword="true"/> when the directory accepted the password.</returns>
        public bool VerifyPassword(Identity account, string password)
        {
            var login = string.IsNullOrWhiteSpace(account?.ExternalSubject) ? account?.UserName : account.ExternalSubject;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(_settings.BindDnFormat))
                {
                    return _directory.Bind(string.Format(_settings.BindDnFormat, EscapeDn(login)), password);
                }

                var found = _directory.Search(_settings.SearchBase, string.Format(_settings.SearchFilter, EscapeFilter(login)));

                return found.Count == 1 && _directory.Bind(found[0], password);
            }
            catch (Exception ex) when (ex is LdapDirectoryException or InvalidOperationException or FormatException)
            {
                // a directory that cannot be reached signs nobody in, and says nothing more
                _componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP source '{Key}' could not check a password: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Escapes a value for a distinguished name (RFC 4514).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The escaped value.</returns>
        public static string EscapeDn(string value)
        {
            var builder = new StringBuilder();

            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                var special = c is ',' or '+' or '"' or '\\' or '<' or '>' or ';' or '='
                    || (index == 0 && (c == '#' || c == ' '))
                    || (index == value.Length - 1 && c == ' ');

                if (c == '\0')
                {
                    builder.Append("\\00");
                }
                else
                {
                    builder.Append(special ? "\\" + c : c);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Escapes a value for a search filter (RFC 4515).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The escaped value.</returns>
        public static string EscapeFilter(string value)
        {
            var builder = new StringBuilder();

            foreach (var c in value)
            {
                builder.Append(c switch
                {
                    '\\' => "\\5c",
                    '*' => "\\2a",
                    '(' => "\\28",
                    ')' => "\\29",
                    '\0' => "\\00",
                    _ => c.ToString()
                });
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reads the configured connections from the plugin's settings, in their order, each with
        /// the service password of <see cref="SecretsSection"/> under its key where one is set
        /// there - it wins over the password in the entry.
        /// </summary>
        /// <param name="pluginSettings">The settings of this plugin. May be null.</param>
        /// <returns>The connections, enabled or not.</returns>
        public static IReadOnlyList<LdapSourceSettings> ReadConfigured(IConfiguration pluginSettings)
        {
            var list = new List<LdapSourceSettings>();

            foreach (var section in pluginSettings?.GetSection(Section).GetChildren() ?? [])
            {
                var settings = new LdapSourceSettings();
                section.Bind(settings);

                var key = settings.Key?.Trim();
                var secret = string.IsNullOrEmpty(key)
                    ? null
                    : pluginSettings.GetSection(SecretsSection).GetSection(key)["ServicePassword"];

                if (!string.IsNullOrEmpty(secret))
                {
                    settings.ServicePassword = secret;
                }

                list.Add(settings);
            }

            return list;
        }

        /// <summary>
        /// Brings the catalog in step with the configured connections: registers every enabled,
        /// complete connection and takes out the sources an earlier call registered that are no
        /// longer configured or were disabled. Runs at start and after every change of the
        /// settings, so the settings page takes effect without a restart.
        /// </summary>
        /// <remarks>
        /// A key another kind of source holds (an OpenID Connect provider, say) is left to it:
        /// a directory configured with that key would otherwise take over the sign-in of its
        /// accounts.
        /// </remarks>
        /// <param name="pluginSettings">The settings of this plugin. May be null.</param>
        /// <param name="componentHub">The component hub, for the log.</param>
        /// <returns>The registered sources.</returns>
        public static IReadOnlyList<LdapAuthenticationSource> RegisterConfigured(IConfiguration pluginSettings, IComponentHub componentHub)
        {
            var registered = new List<LdapAuthenticationSource>();

            lock (_registrationSync)
            {
                foreach (var settings in ReadConfigured(pluginSettings).Where(x => x.Enabled))
                {
                    try
                    {
                        var source = new LdapAuthenticationSource(settings, componentHub: componentHub);
                        var present = CoreHub.AuthenticationSourceManager.GetSource(source.Key);

                        if (present is not null and not LdapAuthenticationSource)
                        {
                            componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP source '{source.Key}' was not registered: another sign-in source uses the key.");

                            continue;
                        }

                        if (registered.Any(x => x.Key == source.Key))
                        {
                            componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP source '{source.Key}' is configured twice; the first entry is used.");

                            continue;
                        }

                        CoreHub.AuthenticationSourceManager.Register(source);
                        registered.Add(source);
                    }
                    catch (ArgumentException ex)
                    {
                        componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP source '{settings.Key}' was not registered: {ex.Message}");
                    }
                }

                // a connection that was removed or disabled must stop signing anybody in
                foreach (var stale in _registered.Where(x => registered.All(y => y.Key != x)).ToList())
                {
                    if (CoreHub.AuthenticationSourceManager.GetSource(stale) is LdapAuthenticationSource)
                    {
                        CoreHub.AuthenticationSourceManager.Unregister(stale);
                    }

                    _registered.Remove(stale);
                }

                foreach (var source in registered)
                {
                    _registered.Add(source.Key);
                }
            }

            return registered;
        }
    }
}
