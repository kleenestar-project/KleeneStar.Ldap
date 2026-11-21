using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// The transport of an LDAP connection.
    /// </summary>
    public enum LdapProtocol
    {
        /// <summary>Plain LDAP (port 389). The password crosses the network in the clear.</summary>
        Ldap,

        /// <summary>LDAP upgraded to TLS by the StartTLS extended operation (port 389).</summary>
        StartTls,

        /// <summary>LDAP over TLS from the first byte (port 636).</summary>
        Ldaps
    }

    /// <summary>
    /// The settings of an LDAP or Active Directory connection (<c>Plugins:kleenestar.ldap:Directories</c>,
    /// one entry per directory). The same entry serves the sign-in and the synchronization of the
    /// accounts.
    /// </summary>
    /// <remarks>
    /// Two ways to find the entry a password is checked against. <b>Direct bind</b>: the login
    /// fills <see cref="BindDnFormat"/> (<c>{0}@corp.example.com</c> for Active Directory,
    /// <c>uid={0},ou=people,dc=example,dc=com</c> for OpenLDAP). <b>Search then bind</b>: a
    /// service account (<see cref="ServiceDn"/>) searches <see cref="SearchBase"/> with
    /// <see cref="SearchFilter"/> (<c>(sAMAccountName={0})</c>), and the password is checked
    /// against the entry found. The synchronization always reads as the service account (or
    /// anonymously when none is named) below <see cref="SearchBase"/>.
    /// </remarks>
    public sealed class LdapSourceSettings
    {
        /// <summary>
        /// Gets or sets the id the settings page addresses the connection by. A hand-written
        /// entry may leave it out; <see cref="ResolveId"/> derives one from the key then.
        /// </summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the key the accounts store as their source.</summary>
        public string Key { get; set; } = "ldap";

        /// <summary>Gets or sets the name shown for the source.</summary>
        public string Name { get; set; } = "LDAP";

        /// <summary>Gets or sets whether the connection is in use - for the sign-in and the synchronization alike.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the directory host.</summary>
        public string Server { get; set; }

        /// <summary>Gets or sets the port; 0 picks 636 for LDAPS and 389 otherwise.</summary>
        public int Port { get; set; }

        /// <summary>Gets or sets whether the connection is LDAPS. On by default: a password must not cross the network in the clear.</summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>Gets or sets whether a plain connection is upgraded with StartTLS. Ignored for LDAPS.</summary>
        public bool StartTls { get; set; }

        /// <summary>
        /// Gets or sets whether a server certificate that does not validate is accepted anyway.
        /// Meant for a test directory with a self-signed certificate only: it gives up the
        /// assurance of talking to the right server.
        /// </summary>
        public bool AllowUntrustedCertificate { get; set; }

        /// <summary>Gets or sets the timeout of a connection and of a sign-in check, in seconds.</summary>
        public int Timeout { get; set; } = 10;

        /// <summary>Gets or sets the distinguished name a login is bound as, <c>{0}</c> standing for the login.</summary>
        public string BindDnFormat { get; set; }

        /// <summary>Gets or sets the service account that searches, for search then bind and for the synchronization.</summary>
        public string ServiceDn { get; set; }

        /// <summary>Gets or sets the service account's password.</summary>
        public string ServicePassword { get; set; }

        /// <summary>Gets or sets where the search starts.</summary>
        public string SearchBase { get; set; }

        /// <summary>Gets or sets the search filter, <c>{0}</c> standing for the login.</summary>
        public string SearchFilter { get; set; } = "(sAMAccountName={0})";

        /// <summary>Gets or sets the order in the sign-in.</summary>
        public int Order { get; set; } = 50;

        /// <summary>Gets or sets whether the accounts are synchronized with the directory on a schedule.</summary>
        public bool SyncEnabled { get; set; }

        /// <summary>Gets or sets the minutes between two scheduled synchronizations.</summary>
        public int SyncInterval { get; set; } = 60;

        /// <summary>Gets or sets the filter that selects the directory entries that are accounts.</summary>
        public string UserFilter { get; set; } = "(&(objectCategory=person)(objectClass=user))";

        /// <summary>Gets or sets the attribute that carries the login (<c>sAMAccountName</c>, <c>uid</c>).</summary>
        public string LoginAttribute { get; set; } = "sAMAccountName";

        /// <summary>Gets or sets the attribute that carries the display name.</summary>
        public string NameAttribute { get; set; } = "displayName";

        /// <summary>Gets or sets the attribute that carries the e-mail address.</summary>
        public string EmailAttribute { get; set; } = "mail";

        /// <summary>Gets or sets the attribute that carries the department.</summary>
        public string DepartmentAttribute { get; set; } = "department";

        /// <summary>Gets or sets the attribute that carries the position.</summary>
        public string PositionAttribute { get; set; } = "title";

        /// <summary>Gets or sets the attribute that carries the phone number.</summary>
        public string PhoneAttribute { get; set; } = "telephoneNumber";

        /// <summary>Gets or sets whether accounts the directory no longer returns are disabled.</summary>
        public bool DisableMissing { get; set; } = true;

        /// <summary>
        /// Gets the transport the settings describe.
        /// </summary>
        [JsonIgnore]
        public LdapProtocol Protocol => UseSsl ? LdapProtocol.Ldaps : StartTls ? LdapProtocol.StartTls : LdapProtocol.Ldap;

        /// <summary>
        /// Gets the port that is connected to.
        /// </summary>
        [JsonIgnore]
        public int EffectivePort => Port > 0 ? Port : UseSsl ? 636 : 389;

        /// <summary>
        /// Sets the transport.
        /// </summary>
        /// <param name="protocol">The transport.</param>
        public void SetProtocol(LdapProtocol protocol)
        {
            UseSsl = protocol == LdapProtocol.Ldaps;
            StartTls = protocol == LdapProtocol.StartTls;
        }

        /// <summary>
        /// Returns the id of the connection: the stored one, or - for an entry written by hand
        /// without one - an id derived from the key, so it stays the same from one request to
        /// the next.
        /// </summary>
        /// <returns>The id.</returns>
        public Guid ResolveId()
        {
            if (Guid.TryParse(Id, out var id) && id != Guid.Empty)
            {
                return id;
            }

            var hash = MD5.HashData(Encoding.UTF8.GetBytes("kleenestar.ldap:" + (Key ?? string.Empty).Trim().ToLowerInvariant()));

            return new Guid(hash);
        }

        /// <summary>
        /// Returns a copy of the settings.
        /// </summary>
        /// <returns>The copy.</returns>
        public LdapSourceSettings Clone()
        {
            return (LdapSourceSettings)MemberwiseClone();
        }
    }
}
