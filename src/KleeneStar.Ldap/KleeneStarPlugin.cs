using KleeneStar.Core;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// Plugin entry point of the LDAP and Active Directory sign-in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The directory protocol is a dependency only an installation with a directory needs, so it
    /// lives here and not in the core: the core knows authentication sources only through its
    /// open catalog (<c>AuthenticationSourceCatalog</c>), and this plugin adds one source per
    /// configured directory when it runs.
    /// </para>
    /// <para>
    /// The directories are read from the plugin's own settings,
    /// <c>Plugins:kleenestar.ldap:Directories</c> (see <c>settings/readme.md</c> of the host).
    /// </para>
    /// </remarks>
    [Name("kleenestar.ldap:plugin.name")]
    [Description("kleenestar.ldap:plugin.description")]
    [Application<KleeneStarApplication>()]
    [Dependency("webexpress.webapp")]
    [Dependency("kleenestar.core")]
    public sealed class KleeneStarPlugin : IPlugin
    {
        private readonly IPluginContext _pluginContext;
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="pluginContext">The context of this plugin, carrying its settings.</param>
        /// <param name="componentHub">The component hub, for the log.</param>
        public KleeneStarPlugin(IPluginContext pluginContext, IComponentHub componentHub)
        {
            _pluginContext = pluginContext;
            _componentHub = componentHub;
        }

        /// <summary>
        /// Registers the configured directories with the core's catalog of authentication sources.
        /// </summary>
        public void Run()
        {
            LdapAuthenticationSource.RegisterConfigured(_pluginContext?.Settings, _componentHub);
        }

        /// <summary>
        /// Releases the plugin.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
