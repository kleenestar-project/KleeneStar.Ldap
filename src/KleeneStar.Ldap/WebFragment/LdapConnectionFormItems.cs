using KleeneStar.Core;
using KleeneStar.Ldap.WebRestApi;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The inputs of the add and edit dialogs of an LDAP connection, in three tabs: the
    /// connection itself, how the directory is read and the sign-in finds an account, and the
    /// synchronization of the accounts.
    /// </summary>
    /// <remarks>
    /// The names are the properties of <see cref="LdapConnectionItem"/>, which the CRUD endpoint
    /// answers and reads. The service password is a password input that is never filled: the
    /// endpoint answers it empty, and an empty answer keeps the stored one.
    /// </remarks>
    public sealed class LdapConnectionFormItems
    {
        /// <summary>Gets the tab strip holding every input.</summary>
        public ControlFormItemGroupTab Tabs { get; } = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public LdapConnectionFormItems()
        {
            var connection = new ControlFormItemGroupTabView { Title = _ => "kleenestar.ldap:connection.tab.connection" };
            var directory = new ControlFormItemGroupTabView { Title = _ => "kleenestar.ldap:connection.tab.directory" };
            var synchronization = new ControlFormItemGroupTabView { Title = _ => "kleenestar.ldap:connection.tab.synchronization" };

            connection
                .Add(Text(nameof(LdapConnectionItem.Name), required: true))
                .Add(Text(nameof(LdapConnectionItem.Key), required: true))
                .Add(Check(nameof(LdapConnectionItem.Enabled)))
                .Add(new ControlDataFormItemInputSelection
                {
                    Name = _ => nameof(LdapConnectionItem.Protocol),
                    Label = _ => "kleenestar.ldap:connection.protocol.label",
                    Help = _ => "kleenestar.ldap:connection.protocol.help",
                    StickySelection = _ => true,
                    ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Protocol>().ToString())
                })
                .Add(Text(nameof(LdapConnectionItem.Server), required: true))
                .Add(Text(nameof(LdapConnectionItem.Port), pattern: "[0-9]*"))
                .Add(Text(nameof(LdapConnectionItem.Timeout), pattern: "[0-9]*"))
                .Add(Check(nameof(LdapConnectionItem.AllowUntrustedCertificate)))
                .Add(Text(nameof(LdapConnectionItem.Order), pattern: "-?[0-9]*"));

            directory
                .Add(Text(nameof(LdapConnectionItem.ServiceDn)))
                .Add(new ControlFormItemInputPassword
                {
                    Name = _ => nameof(LdapConnectionItem.ServicePassword),
                    Label = _ => "kleenestar.ldap:connection.servicepassword.label",
                    Placeholder = _ => "kleenestar.ldap:connection.servicepassword.placeholder",
                    Help = _ => "kleenestar.ldap:connection.servicepassword.help"
                })
                .Add(Text(nameof(LdapConnectionItem.SearchBase)))
                .Add(Text(nameof(LdapConnectionItem.BindDnFormat)))
                .Add(Text(nameof(LdapConnectionItem.SearchFilter)));

            synchronization
                .Add(Check(nameof(LdapConnectionItem.SyncEnabled)))
                .Add(Text(nameof(LdapConnectionItem.SyncInterval), pattern: "[0-9]*"))
                .Add(Text(nameof(LdapConnectionItem.UserFilter)))
                .Add(Check(nameof(LdapConnectionItem.DisableMissing)))
                .Add(Text(nameof(LdapConnectionItem.LoginAttribute)))
                .Add(Text(nameof(LdapConnectionItem.NameAttribute)))
                .Add(Text(nameof(LdapConnectionItem.EmailAttribute)))
                .Add(Text(nameof(LdapConnectionItem.DepartmentAttribute)))
                .Add(Text(nameof(LdapConnectionItem.PositionAttribute)))
                .Add(Text(nameof(LdapConnectionItem.PhoneAttribute)));

            Tabs.AddView(connection, directory, synchronization);
        }

        /// <summary>
        /// Builds a text input whose texts follow the convention
        /// <c>kleenestar.ldap:connection.{name}.label|placeholder|help</c>.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <param name="required">Whether an answer is required.</param>
        /// <param name="pattern">The pattern an answer must match, or <see langword="null"/>.</param>
        /// <returns>The input.</returns>
        private static ControlFormItemInputText Text(string name, bool required = false, string pattern = null)
        {
            var key = name.ToLowerInvariant();

            return new ControlFormItemInputText
            {
                Name = _ => name,
                Label = _ => $"kleenestar.ldap:connection.{key}.label",
                Placeholder = _ => $"kleenestar.ldap:connection.{key}.placeholder",
                Help = _ => $"kleenestar.ldap:connection.{key}.help",
                Required = _ => required,
                Pattern = pattern is null ? null : _ => pattern
            };
        }

        /// <summary>
        /// Builds a switch.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The switch.</returns>
        private static ControlFormItemInputCheck Check(string name)
        {
            var key = name.ToLowerInvariant();

            return new ControlFormItemInputCheck
            {
                Name = _ => name,
                Label = _ => $"kleenestar.ldap:connection.{key}.label",
                Help = _ => $"kleenestar.ldap:connection.{key}.help",
                Layout = _ => TypeLayoutCheck.Switch
            };
        }
    }
}
