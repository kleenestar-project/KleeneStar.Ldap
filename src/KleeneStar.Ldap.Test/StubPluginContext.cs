using Microsoft.Extensions.Configuration;
using System.Reflection;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Ldap.Test
{
    /// <summary>
    /// The context the framework hands the plugin, carrying nothing but its settings.
    /// </summary>
    /// <param name="settings">The plugin's settings.</param>
    internal sealed class StubPluginContext(IConfiguration settings) : IPluginContext
    {
        /// <inheritdoc/>
        public IComponentId PluginId => null;

        /// <inheritdoc/>
        public string PluginName => "kleenestar.ldap";

        /// <inheritdoc/>
        public string Description => null;

        /// <inheritdoc/>
        public string Manufacturer => null;

        /// <inheritdoc/>
        public string Copyright => null;

        /// <inheritdoc/>
        public string Version => null;

        /// <inheritdoc/>
        public string License => null;

        /// <inheritdoc/>
        public IRoute Icon => null;

        /// <inheritdoc/>
        public Assembly Assembly => typeof(KleeneStarPlugin).Assembly;

        /// <inheritdoc/>
        public IConfiguration Settings => settings;
    }
}
