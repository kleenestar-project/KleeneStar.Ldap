using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_
{
    /// <summary>
    /// The address of one LDAP connection. It declares the route segment the dialogs below it
    /// bind the connection's id into; it has no content of its own.
    /// </summary>
    [SegmentGuid<LdapConnectionIdParameter>]
    [WebIcon<IconNetworkWired>]
    [Title("kleenestar.ldap:connection.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            LdapAuthorization.Demand(renderContext?.Request);
        }
    }
}
