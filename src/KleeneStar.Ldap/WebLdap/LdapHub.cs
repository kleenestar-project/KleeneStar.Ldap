using KleeneStar.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// The plugin's own entry to what its pages, endpoints and the scheduler share: the
    /// effective settings, the settings file and the record of the synchronizations. Set up by
    /// <see cref="KleeneStarPlugin.Run"/>; tests replace the stores.
    /// </summary>
    public static class LdapHub
    {
        private static LdapConnectionStore _store;
        private static LdapSyncStatusStore _status;

        /// <summary>
        /// Gets or sets the plugin's section of the merged configuration - what the sign-in and
        /// the scheduler use, environment overrides included.
        /// </summary>
        public static IConfiguration Settings { get; set; }

        /// <summary>
        /// Gets or sets the component hub, for the log and the cluster lock.
        /// </summary>
        public static IComponentHub ComponentHub { get; set; }

        /// <summary>
        /// Gets or sets the store of the settings file the settings page edits.
        /// </summary>
        public static LdapConnectionStore Store
        {
            get => _store ??= new LdapConnectionStore
            (
                Path.Combine(CoreHub.HttpServerContext?.SettingsPath ?? "settings", LdapConnectionStore.FileName),
                CoreHub.HttpServerContext?.Configuration
            );
            set => _store = value;
        }

        /// <summary>
        /// Gets or sets the record of the synchronizations.
        /// </summary>
        public static LdapSyncStatusStore Status
        {
            get => _status ??= new LdapSyncStatusStore
            (
                Path.Combine(CoreHub.HttpServerContext?.DataPath ?? "data", "ldap", "synchronization.json")
            );
            set => _status = value;
        }

        /// <summary>
        /// Returns the connection as it is in effect: the entry of the merged configuration
        /// carrying the id (environment overrides included, the service password for one),
        /// falling back to the entry of the settings file.
        /// </summary>
        /// <param name="id">The connection.</param>
        /// <returns>The settings, or <see langword="null"/>.</returns>
        public static LdapSourceSettings GetEffective(Guid id)
        {
            return LdapAuthenticationSource.ReadConfigured(Settings).FirstOrDefault(x => x.ResolveId() == id)
                ?? Store.GetConnection(id);
        }

        /// <summary>
        /// Synchronizes a connection under the cluster lock of that connection and records the
        /// outcome. Answers <see langword="null"/> when another run of the same connection holds
        /// the lock.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <returns>The outcome, or <see langword="null"/> when a run is already going on.</returns>
        public static LdapSyncResult Synchronize(LdapSourceSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var id = settings.ResolveId();
            using var clusterLock = ComponentHub?.ClusterManager?.Lock($"kleenestar.ldap.sync.{id:N}", TimeSpan.FromMinutes(30), TimeSpan.Zero);

            if (ComponentHub?.ClusterManager is not null && clusterLock is null)
            {
                return null;
            }

            var result = LdapUserSynchronizer.Synchronize(settings, ComponentHub);

            try
            {
                Status.Set(result);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ComponentHub?.LogManager?.DefaultLog?.Warning($"The outcome of the LDAP synchronization could not be recorded: {ex.Message}");
            }

            return result;
        }
    }
}
