using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The view of the connection page; the table and its pager are its items.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnections.Index>]
    [Cache]
    public sealed class LdapConnectionViewFragment : FragmentControlView
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Layout = _ => TypeLayoutView.ToggleGroup;
        }
    }
}
