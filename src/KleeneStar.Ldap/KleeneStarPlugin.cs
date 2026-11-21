using KleeneStar.Core;
using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebRestApi;
using Microsoft.Extensions.Primitives;
using System;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// Plugin entry point of the LDAP and Active Directory sign-in and account synchronization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The directory protocol is a dependency only an installation with a directory needs, so it
    /// lives here and not in the core: the core knows authentication sources only through its
    /// open catalog (<c>AuthenticationSourceManager</c>), and this plugin adds one source per
    /// configured connection when it runs - and again whenever the settings change.
    /// </para>
    /// <para>
    /// The connections are read from the plugin's own settings,
    /// <c>Plugins:kleenestar.ldap:Directories</c>, and edited on the settings page
    /// <c>/settings/ldapconnections</c> (user management), which writes
    /// <c>settings/kleenestar.ldap.settings.json</c>.
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
        private IDisposable _settingsWatch;
        private LdapSyncScheduler _scheduler;

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
        /// Registers the configured connections with the core's catalog of authentication
        /// sources, follows changes of the settings, and starts the scheduled synchronization.
        /// </summary>
        public void Run()
        {
            var settings = _pluginContext?.Settings;

            LdapHub.Settings = settings;
            LdapHub.ComponentHub = _componentHub;

            LdapAuthenticationSource.RegisterConfigured(settings, _componentHub);

            if (settings is not null)
            {
                // the settings page and an operator editing the file both end in a reload
                _settingsWatch = ChangeToken.OnChange(settings.GetReloadToken, () =>
                {
                    LdapAuthenticationSource.RegisterConfigured(settings, _componentHub);

                    // an edit of the file by hand changes the table as the page's own writes do
                    _ = DataChangedNotifier.NotifyAsync<LdapConnectionItem>(DataChangeOperation.Updated);
                });
            }

            // only a running host synchronizes; a test that runs the plugin has no host
            if (_componentHub is not null)
            {
                _scheduler = new LdapSyncScheduler();
            }
        }

        /// <summary>
        /// Releases the plugin.
        /// </summary>
        public void Dispose()
        {
            _settingsWatch?.Dispose();
            _scheduler?.Dispose();
        }
    }
}
