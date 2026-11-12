using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The form of the dialog that checks an LDAP connection.
    /// </summary>
    [Title("kleenestar.ldap:connection.test.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_.Test>]
    [Cache]
    public sealed class LdapConnectionTestFormFragment : LdapConnectionActionFormFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionTestFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, new ControlFormItemButtonSubmit
            {
                Text = _ => "kleenestar.ldap:connection.test.label",
                Icon = _ => new IconPlugCircleCheck(),
                Color = _ => new PropertyColorButton(TypeColorButton.Primary)
            })
        {
            this.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Test>();
        }

        /// <summary>
        /// Gets the key of the explanation above the button: what the check does.
        /// </summary>
        protected override string Explanation => "kleenestar.ldap:connection.test.description";
    }
}
