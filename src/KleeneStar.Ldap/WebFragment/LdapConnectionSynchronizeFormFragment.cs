using KleeneStar.Ldap.WebLdap;
using System.Collections.Generic;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The form of the dialog that synchronizes the accounts of an LDAP connection now.
    /// </summary>
    [Title("kleenestar.ldap:connection.synchronize.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_.Synchronize>]
    [Cache]
    public sealed class LdapConnectionSynchronizeFormFragment : LdapConnectionActionFormFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionSynchronizeFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, new ControlFormItemButtonSubmit
            {
                Text = _ => "kleenestar.ldap:connection.synchronize.label",
                Icon = _ => new IconArrowsRotate(),
                Color = _ => new PropertyColorButton(TypeColorButton.Primary)
            })
        {
            this.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Synchronize>();
        }

        /// <summary>
        /// Gets the key of the explanation above the button: what the synchronization changes.
        /// </summary>
        protected override string Explanation => "kleenestar.ldap:connection.synchronize.description";

        /// <summary>
        /// Describes the connection and its last synchronization.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="request">The request.</param>
        /// <returns>The lines.</returns>
        protected override IEnumerable<string> Describe(LdapSourceSettings settings, IRequest request)
        {
            foreach (var line in base.Describe(settings, request))
            {
                yield return line;
            }

            var last = LdapHub.Status.Get(settings.ResolveId());

            yield return I18N.Translate(request, "kleenestar.ldap:connection.column.lastsync") + ": "
                + global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Table.Describe(last, request);

            if (last is { Succeeded: false } && !string.IsNullOrWhiteSpace(last.Error))
            {
                yield return last.Error;
            }
        }
    }
}
