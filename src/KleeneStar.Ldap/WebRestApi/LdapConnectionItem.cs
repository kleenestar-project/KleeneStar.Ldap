using System;
using System.Collections.Generic;
using System.Globalization;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebDomain;
using WebExpress.WebIndex;

namespace KleeneStar.Ldap.WebRestApi
{
    /// <summary>
    /// An LDAP connection as the settings page's forms and table see it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is its own type rather than <see cref="LdapSourceSettings"/> because the CRUD base
    /// serializes every public property it is handed: the service password would travel to the
    /// browser. Here <see cref="ServicePassword"/> is always empty on the way out, and an empty
    /// password on the way in keeps the stored one.
    /// </para>
    /// <para>
    /// The numbers travel as text, because the forms edit them in text inputs.
    /// </para>
    /// <para>
    /// It is an <see cref="IDomain"/>, because that is what the change channel is keyed by: the
    /// table declares the domain of its endpoint's item type only for a domain, and
    /// <c>DataChangedNotifier</c> announces a create, update or delete only of one. Without it
    /// the CRUD endpoint's notifications went nowhere and the table stayed stale.
    /// </para>
    /// </remarks>
    public sealed class LdapConnectionItem : IIndexItem, IDomain
    {
        /// <summary>
        /// Gets or sets the id of the connection (<see cref="LdapSourceSettings.ResolveId"/>).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the name shown in the sign-in.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the key the accounts store as their source.</summary>
        public string Key { get; set; }

        /// <summary>Gets or sets whether the connection is in use.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the transport, as the id the selection offers it under (<see cref="IdOf"/>).</summary>
        public string Protocol { get; set; }

        /// <summary>Gets or sets the server.</summary>
        public string Server { get; set; }

        /// <summary>Gets or sets the port; empty for the protocol's default.</summary>
        public string Port { get; set; }

        /// <summary>Gets or sets the timeout in seconds.</summary>
        public string Timeout { get; set; }

        /// <summary>Gets or sets whether an untrusted certificate is accepted.</summary>
        public bool AllowUntrustedCertificate { get; set; }

        /// <summary>Gets or sets the direct bind format.</summary>
        public string BindDnFormat { get; set; }

        /// <summary>Gets or sets the service account.</summary>
        public string ServiceDn { get; set; }

        /// <summary>Gets or sets the service password; always empty when read.</summary>
        public string ServicePassword { get; set; }

        /// <summary>Gets or sets the search base.</summary>
        public string SearchBase { get; set; }

        /// <summary>Gets or sets the sign-in search filter.</summary>
        public string SearchFilter { get; set; }

        /// <summary>Gets or sets the order in the sign-in.</summary>
        public string Order { get; set; }

        /// <summary>Gets or sets whether the accounts are synchronized on a schedule.</summary>
        public bool SyncEnabled { get; set; }

        /// <summary>Gets or sets the minutes between two synchronizations.</summary>
        public string SyncInterval { get; set; }

        /// <summary>Gets or sets the filter that selects the users.</summary>
        public string UserFilter { get; set; }

        /// <summary>Gets or sets whether accounts missing in the directory are disabled.</summary>
        public bool DisableMissing { get; set; }

        /// <summary>Gets or sets the login attribute.</summary>
        public string LoginAttribute { get; set; }

        /// <summary>Gets or sets the name attribute.</summary>
        public string NameAttribute { get; set; }

        /// <summary>Gets or sets the e-mail attribute.</summary>
        public string EmailAttribute { get; set; }

        /// <summary>Gets or sets the department attribute.</summary>
        public string DepartmentAttribute { get; set; }

        /// <summary>Gets or sets the position attribute.</summary>
        public string PositionAttribute { get; set; }

        /// <summary>Gets or sets the phone attribute.</summary>
        public string PhoneAttribute { get; set; }

        /// <summary>
        /// Returns the token of a protocol, as the texts name it.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <returns>The token.</returns>
        public static string TokenOf(LdapProtocol protocol)
        {
            return protocol.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Returns the id a protocol is offered under: a selection offers its entries by guid.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <returns>The id.</returns>
        public static Guid IdOf(LdapProtocol protocol)
        {
            return protocol switch
            {
                LdapProtocol.Ldap => new Guid("5a0d1f2e-3b4c-4d5e-8f60-000000000389"),
                LdapProtocol.StartTls => new Guid("5a0d1f2e-3b4c-4d5e-8f60-0000000003f5"),
                _ => new Guid("5a0d1f2e-3b4c-4d5e-8f60-000000000636")
            };
        }

        /// <summary>
        /// Reads a submitted protocol: the id it is offered under, or its name.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <param name="protocol">The protocol.</param>
        /// <returns><see langword="true"/> when the value names a protocol.</returns>
        public static bool TryParseProtocol(string value, out LdapProtocol protocol)
        {
            foreach (var candidate in Enum.GetValues<LdapProtocol>())
            {
                if (Guid.TryParse(value, out var id) && id == IdOf(candidate))
                {
                    protocol = candidate;

                    return true;
                }
            }

            return Enum.TryParse(value, ignoreCase: true, out protocol) && Enum.IsDefined(protocol);
        }

        /// <summary>
        /// Projects stored settings. The service password stays behind.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The item.</returns>
        public static LdapConnectionItem From(LdapSourceSettings settings)
        {
            return new LdapConnectionItem
            {
                Id = settings.ResolveId(),
                Name = settings.Name,
                Key = settings.Key,
                Enabled = settings.Enabled,
                Protocol = IdOf(settings.Protocol).ToString(),
                Server = settings.Server,
                Port = settings.Port > 0 ? settings.Port.ToString(CultureInfo.InvariantCulture) : string.Empty,
                Timeout = settings.Timeout.ToString(CultureInfo.InvariantCulture),
                AllowUntrustedCertificate = settings.AllowUntrustedCertificate,
                BindDnFormat = settings.BindDnFormat,
                ServiceDn = settings.ServiceDn,
                ServicePassword = string.Empty,
                SearchBase = settings.SearchBase,
                SearchFilter = settings.SearchFilter,
                Order = settings.Order.ToString(CultureInfo.InvariantCulture),
                SyncEnabled = settings.SyncEnabled,
                SyncInterval = settings.SyncInterval.ToString(CultureInfo.InvariantCulture),
                UserFilter = settings.UserFilter,
                DisableMissing = settings.DisableMissing,
                LoginAttribute = settings.LoginAttribute,
                NameAttribute = settings.NameAttribute,
                EmailAttribute = settings.EmailAttribute,
                DepartmentAttribute = settings.DepartmentAttribute,
                PositionAttribute = settings.PositionAttribute,
                PhoneAttribute = settings.PhoneAttribute
            };
        }

        /// <summary>
        /// Applies a submitted form onto settings. A field the payload does not carry keeps its
        /// value; a number that does not parse is reported in <paramref name="invalid"/>.
        /// </summary>
        /// <param name="payload">The submitted form.</param>
        /// <param name="target">The settings to change.</param>
        /// <param name="invalid">Receives the names of the fields whose number did not parse.</param>
        public static void Apply(RestApiCrudFormData payload, LdapSourceSettings target, ICollection<string> invalid)
        {
            target.Name = Text(payload, nameof(Name)) ?? target.Name;
            target.Key = Text(payload, nameof(Key)) ?? target.Key;
            target.Enabled = Flag(payload, nameof(Enabled)) ?? target.Enabled;
            target.Server = Text(payload, nameof(Server)) ?? target.Server;
            target.AllowUntrustedCertificate = Flag(payload, nameof(AllowUntrustedCertificate)) ?? target.AllowUntrustedCertificate;
            target.BindDnFormat = Text(payload, nameof(BindDnFormat), blankIsNull: true, present: target.BindDnFormat);
            target.ServiceDn = Text(payload, nameof(ServiceDn), blankIsNull: true, present: target.ServiceDn);
            target.SearchBase = Text(payload, nameof(SearchBase), blankIsNull: true, present: target.SearchBase);
            target.SearchFilter = Text(payload, nameof(SearchFilter)) ?? target.SearchFilter;
            target.SyncEnabled = Flag(payload, nameof(SyncEnabled)) ?? target.SyncEnabled;
            target.UserFilter = Text(payload, nameof(UserFilter)) ?? target.UserFilter;
            target.DisableMissing = Flag(payload, nameof(DisableMissing)) ?? target.DisableMissing;
            target.LoginAttribute = Text(payload, nameof(LoginAttribute)) ?? target.LoginAttribute;
            target.NameAttribute = Attribute(payload, nameof(NameAttribute), target.NameAttribute);
            target.EmailAttribute = Attribute(payload, nameof(EmailAttribute), target.EmailAttribute);
            target.DepartmentAttribute = Attribute(payload, nameof(DepartmentAttribute), target.DepartmentAttribute);
            target.PositionAttribute = Attribute(payload, nameof(PositionAttribute), target.PositionAttribute);
            target.PhoneAttribute = Attribute(payload, nameof(PhoneAttribute), target.PhoneAttribute);

            // empty keeps the stored password: the form never shows it
            var password = Raw(payload, nameof(ServicePassword));

            if (!string.IsNullOrEmpty(password))
            {
                target.ServicePassword = password;
            }

            if (Text(payload, nameof(Protocol)) is { } protocol)
            {
                if (TryParseProtocol(protocol, out var parsed))
                {
                    target.SetProtocol(parsed);
                }
                else
                {
                    invalid.Add(nameof(Protocol));
                }
            }

            target.Port = Number(payload, nameof(Port), target.Port, 0, 65535, invalid, blank: 0);
            target.Timeout = Number(payload, nameof(Timeout), target.Timeout, 1, 300, invalid);
            target.Order = Number(payload, nameof(Order), target.Order, -1000, 1000, invalid);
            target.SyncInterval = Number(payload, nameof(SyncInterval), target.SyncInterval, 5, 525600, invalid);
        }

        /// <summary>
        /// Returns a submitted value as it came, or <see langword="null"/> when the payload does not carry it.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="name">The field.</param>
        /// <returns>The value.</returns>
        private static string Raw(RestApiCrudFormData payload, string name)
        {
            if (payload is null || !payload.TryGetValue(name.ToLowerInvariant(), out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                string text => text,
                bool flag => flag ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Returns a submitted text, trimmed.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="name">The field.</param>
        /// <param name="blankIsNull">Whether a blank answer clears the value (else it is ignored).</param>
        /// <param name="present">The value to keep when the payload does not carry the field.</param>
        /// <returns>The text; <see langword="null"/> for a field not carried (or blank, unless cleared).</returns>
        private static string Text(RestApiCrudFormData payload, string name, bool blankIsNull = false, string present = null)
        {
            var raw = Raw(payload, name);

            if (raw is null)
            {
                return blankIsNull ? present : null;
            }

            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        /// <summary>
        /// Returns a submitted attribute name. A blank answer is stored as the empty name rather
        /// than as nothing: the settings file leaves nothing out, and a left-out attribute would
        /// read back as its default, so a cleared attribute would come back on the next load.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="name">The field.</param>
        /// <param name="present">The value to keep when the payload does not carry the field.</param>
        /// <returns>The attribute name; empty when cleared.</returns>
        private static string Attribute(RestApiCrudFormData payload, string name, string present)
        {
            var raw = Raw(payload, name);

            return raw is null ? present : raw.Trim();
        }

        /// <summary>
        /// Returns a submitted switch. A checkbox posts <c>true</c>/<c>false</c> or the html
        /// default <c>on</c>; all of them are read.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="name">The field.</param>
        /// <returns>The value, or <see langword="null"/> when not carried.</returns>
        private static bool? Flag(RestApiCrudFormData payload, string name)
        {
            var raw = Raw(payload, name);

            if (raw is null)
            {
                return null;
            }

            return raw.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes";
        }

        /// <summary>
        /// Returns a submitted number within bounds.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="name">The field.</param>
        /// <param name="current">The value to keep.</param>
        /// <param name="min">The least value.</param>
        /// <param name="max">The greatest value.</param>
        /// <param name="invalid">Receives the field when its value does not parse or is out of bounds.</param>
        /// <param name="blank">The value of a blank answer; <see langword="null"/> keeps the current one.</param>
        /// <returns>The number.</returns>
        private static int Number(RestApiCrudFormData payload, string name, int current, int min, int max, ICollection<string> invalid, int? blank = null)
        {
            var raw = Raw(payload, name);

            if (raw is null)
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return blank ?? current;
            }

            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
            {
                return value;
            }

            invalid.Add(name);

            return current;
        }
    }
}
