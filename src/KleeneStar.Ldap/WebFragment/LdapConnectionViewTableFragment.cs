using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The table of the LDAP connections.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<LdapConnectionViewFragment>]
    [Cache]
    public sealed class LdapConnectionViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the table.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the table is the view, so it takes the height it is handed and its rows scroll
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.ldap:connection.table.header";

            // declares the endpoint and, from its item type, the domain the table serves: the
            // CRUD endpoint's change notifications then refresh it after a create, edit,
            // delete or synchronization
            Table.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Table>();
            Table.Bind = _ => new Binding()
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = LdapConnectionViewPaginationFragment.ContentId });

            Add(Table);
        }
    }
}
