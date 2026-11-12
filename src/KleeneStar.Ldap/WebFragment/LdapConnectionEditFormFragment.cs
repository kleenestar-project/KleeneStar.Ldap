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
    /// The form of the dialog that edits an LDAP connection.
    /// </summary>
    [Title("kleenestar.ldap:connection.edit.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_.Edit>]
    [Cache]
    public sealed class LdapConnectionEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the inputs.
        /// </summary>
        public LdapConnectionFormItems Inputs { get; } = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Inputs.Tabs);

            this.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Index>();
            ItemId = renderContext => renderContext.Request.GetParameter<LdapConnectionIdParameter>()?.Value;
        }
    }
}
