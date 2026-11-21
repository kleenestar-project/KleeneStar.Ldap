using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The form of the dialog that adds an LDAP or LDAPS connection.
    /// </summary>
    [Title("kleenestar.ldap:connection.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnections.Add>]
    [Cache]
    public sealed class LdapConnectionAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the inputs.
        /// </summary>
        public LdapConnectionFormItems Inputs { get; } = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Inputs.Tabs);

            this.DataService<global::KleeneStar.Ldap.WWW.Api._1_.LdapConnections.Index>();
        }
    }
}
