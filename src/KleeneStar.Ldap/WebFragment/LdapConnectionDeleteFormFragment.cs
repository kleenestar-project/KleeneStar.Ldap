using KleeneStar.Ldap.WebParameter;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The form of the dialog that deletes an LDAP connection. Its accounts stay, and stop
    /// signing in.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_.Delete>]
    [Cache]
    public sealed class LdapConnectionDeleteFormFragment : FragmentControlDataFormDelete
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Content.Text = _ => "kleenestar.ldap:connection.delete.description";

            this.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Index>();
            ItemId = renderContext => renderContext.Request.GetParameter<LdapConnectionIdParameter>()?.Value;
        }
    }
}
