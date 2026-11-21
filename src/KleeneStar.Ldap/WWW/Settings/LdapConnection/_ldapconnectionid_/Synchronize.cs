using KleeneStar.Ldap.WebLdap;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Ldap.WWW.Settings.LdapConnection._ldapconnectionid_
{
    /// <summary>
    /// The dialog that synchronizes the accounts of an LDAP connection with the directory.
    /// </summary>
    [WebIcon<IconArrowsRotate>]
    [Title("kleenestar.ldap:connection.synchronize.title")]
    [Scope<IScopeGeneral>]
    public sealed class Synchronize : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Synchronize()
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
