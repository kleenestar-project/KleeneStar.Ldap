using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// The settings of an LDAP or Active Directory source (<c>Plugins:kleenestar.ldap:Directories</c>,
    /// one entry per directory).
    /// </summary>
    /// <remarks>
    /// Two ways to find the entry a password is checked against. <b>Direct bind</b>: the login
    /// fills <see cref="BindDnFormat"/> (<c>{0}@corp.example.com</c> for Active Directory,
    /// <c>uid={0},ou=people,dc=example,dc=com</c> for OpenLDAP). <b>Search then bind</b>: a
    /// service account (<see cref="ServiceDn"/>) searches <see cref="SearchBase"/> with
    /// <see cref="SearchFilter"/> (<c>(sAMAccountName={0})</c>), and the password is checked
    /// against the entry found.
    /// </remarks>
    public sealed class LdapSourceSettings
    {
        /// <summary>Gets or sets the key the accounts store as their source.</summary>
        public string Key { get; set; } = "ldap";

        /// <summary>Gets or sets the name shown for the source.</summary>
        public string Name { get; set; } = "LDAP";

        /// <summary>Gets or sets the directory host.</summary>
        public string Server { get; set; }

        /// <summary>Gets or sets the port; 636 for LDAPS, 389 otherwise.</summary>
        public int Port { get; set; }

        /// <summary>Gets or sets whether the connection is LDAPS. On by default: a password must not cross the network in the clear.</summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>Gets or sets the distinguished name a login is bound as, <c>{0}</c> standing for the login.</summary>
        public string BindDnFormat { get; set; }

        /// <summary>Gets or sets the service account that searches, for search then bind.</summary>
        public string ServiceDn { get; set; }

        /// <summary>Gets or sets the service account's password.</summary>
        public string ServicePassword { get; set; }

        /// <summary>Gets or sets where the search starts.</summary>
        public string SearchBase { get; set; }

        /// <summary>Gets or sets the search filter, <c>{0}</c> standing for the login.</summary>
        public string SearchFilter { get; set; } = "(sAMAccountName={0})";

        /// <summary>Gets or sets the order in the sign-in.</summary>
        public int Order { get; set; } = 50;
    }

    /// <summary>
    /// Talks to a directory; replaced in tests.
    /// </summary>
    public interface ILdapDirectory
    {
        /// <summary>
        /// Binds as an entry.
        /// </summary>
        /// <param name="distinguishedName">The entry.</param>
        /// <param name="password">The password.</param>
        /// <returns><see langword="true"/> when the directory accepted the credentials.</returns>
        bool Bind(string distinguishedName, string password);

        /// <summary>
        /// Searches as the service account.
        /// </summary>
        /// <param name="searchBase">Where to start.</param>
        /// <param name="filter">The filter, its values escaped.</param>
        /// <returns>The distinguished names found.</returns>
        IReadOnlyList<string> Search(string searchBase, string filter);
    }

    /// <summary>
    /// Signs accounts in against an LDAP directory or Active Directory: the password offered is
    /// checked by binding as the account's directory entry. Nothing about the password is kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The accounts exist before their first sign-in - created by an administrator or by the
    /// directory import - with this source and, where the directory login differs from the user
    /// name, the login as <see cref="Identity.ExternalSubject"/>. The source does not provision.
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

        private readonly LdapSourceSettings _settings;
        private readonly ILdapDirectory _directory;
        private readonly IComponentHub _componentHub;

        /// <inheritdoc/>
        public string Key => IdentitySource.Normalize(_settings.Key);

        /// <inheritdoc/>
        public string Name => _settings.Name;

        /// <inheritdoc/>
        public int Order => _settings.Order;

        /// <inheritdoc/>
        public bool IsExternal => true;

        /// <inheritdoc/>
        public bool AcceptsPassword => true;

        /// <inheritdoc/>
        public bool Provisions => false;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="directory">The directory, or <see langword="null"/> for the configured server.</param>
        /// <param name="componentHub">The component hub, for the log; may be <see langword="null"/>.</param>
        public LdapAuthenticationSource(LdapSourceSettings settings, ILdapDirectory directory = null, IComponentHub componentHub = null)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (IdentitySource.IsLocal(settings.Key))
            {
                throw new ArgumentException($"An LDAP source needs a key other than '{IdentitySource.Local}'.");
            }

            if (directory is null && string.IsNullOrWhiteSpace(settings.Server))
            {
                throw new ArgumentException("An LDAP source needs a server.");
            }

            if (string.IsNullOrWhiteSpace(settings.BindDnFormat) && (string.IsNullOrWhiteSpace(settings.ServiceDn) || string.IsNullOrWhiteSpace(settings.SearchBase)))
            {
                throw new ArgumentException("An LDAP source needs a bind format, or a service account and a search base.");
            }

            _settings = settings;
            _directory = directory ?? new LdapDirectory(settings);
            _componentHub = componentHub;
        }

        /// <inheritdoc/>
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
            catch (Exception ex) when (ex is LdapException or DirectoryOperationException or InvalidOperationException)
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
        /// Registers every configured directory with the catalog.
        /// </summary>
        /// <param name="pluginSettings">The settings of this plugin. May be null.</param>
        /// <param name="componentHub">The component hub, for the log.</param>
        /// <returns>The registered sources.</returns>
        public static IReadOnlyList<LdapAuthenticationSource> RegisterConfigured(IConfiguration pluginSettings, IComponentHub componentHub)
        {
            var registered = new List<LdapAuthenticationSource>();

            foreach (var section in pluginSettings?.GetSection(Section).GetChildren() ?? [])
            {
                var settings = new LdapSourceSettings();
                section.Bind(settings);

                try
                {
                    var source = new LdapAuthenticationSource(settings, componentHub: componentHub);

                    AuthenticationSourceCatalog.Register(source);
                    registered.Add(source);
                }
                catch (ArgumentException ex)
                {
                    componentHub?.LogManager?.DefaultLog?.Warning($"The LDAP source '{settings.Key}' ({section.Path}) was not registered: {ex.Message}");
                }
            }

            return registered;
        }

        /// <summary>
        /// The directory a configured source talks to.
        /// </summary>
        /// <param name="settings">The settings.</param>
        private sealed class LdapDirectory(LdapSourceSettings settings) : ILdapDirectory
        {
            /// <inheritdoc/>
            public bool Bind(string distinguishedName, string password)
            {
                using var connection = Connect();

                try
                {
                    connection.Bind(new NetworkCredential(distinguishedName, password));

                    return true;
                }
                catch (LdapException ex) when (ex.ErrorCode == 49)
                {
                    // invalid credentials
                    return false;
                }
            }

            /// <inheritdoc/>
            public IReadOnlyList<string> Search(string searchBase, string filter)
            {
                using var connection = Connect();

                connection.Bind(new NetworkCredential(settings.ServiceDn, settings.ServicePassword));

                var response = (SearchResponse)connection.SendRequest(new SearchRequest(searchBase, filter, SearchScope.Subtree, "distinguishedName"));

                return [.. response.Entries.Cast<SearchResultEntry>().Take(2).Select(x => x.DistinguishedName)];
            }

            /// <summary>
            /// Opens a connection.
            /// </summary>
            /// <returns>The connection.</returns>
            private LdapConnection Connect()
            {
                var port = settings.Port > 0 ? settings.Port : settings.UseSsl ? 636 : 389;
                var connection = new LdapConnection(new LdapDirectoryIdentifier(settings.Server, port))
                {
                    AuthType = AuthType.Basic,
                    Timeout = TimeSpan.FromSeconds(10)
                };

                connection.SessionOptions.ProtocolVersion = 3;
                connection.SessionOptions.SecureSocketLayer = settings.UseSsl;

                return connection;
            }
        }
    }
}
