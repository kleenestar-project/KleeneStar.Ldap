using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The pager below the table of the LDAP connections.
    /// </summary>
    [Section<SectionViewFooterPrimary>]
    [Scope<LdapConnectionViewFragment>]
    [Cache]
    public sealed class LdapConnectionViewPaginationFragment : FragmentControlViewFooter
    {
        /// <summary>
        /// The id the table binds its paging to.
        /// </summary>
        public static readonly string ContentId = "id_7D2E4B9C1A3F4E6B8C0D5A2F9E1B7C34";

        /// <summary>
        /// Gets the pager.
        /// </summary>
        public ControlPagination Pagination { get; } = new ControlPagination(ContentId);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionViewPaginationFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Pagination);
        }
    }
}
