using System;
using System.Linq;
using System.Threading;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// Runs the scheduled synchronizations: once a minute it looks for enabled connections with
    /// the synchronization switched on whose last run is older than their interval, and runs
    /// them one after the other.
    /// </summary>
    /// <remarks>
    /// The decision reads the shared record (<see cref="LdapSyncStatusStore"/>) and every run
    /// holds the cluster lock of its connection, so replicas sharing the data directory and the
    /// cluster state run a connection once per interval between them. A tick that finds the
    /// previous one still running skips.
    /// </remarks>
    public sealed class LdapSyncScheduler : IDisposable
    {
        private readonly Timer _timer;
        private int _running;

        /// <summary>
        /// Initializes a new instance of the class and starts the timer.
        /// </summary>
        /// <param name="interval">How often to look; a minute when null.</param>
        public LdapSyncScheduler(TimeSpan? interval = null)
        {
            var period = interval ?? TimeSpan.FromMinutes(1);

            _timer = new Timer(_ => Tick(DateTime.UtcNow), null, period, period);
        }

        /// <summary>
        /// Runs every connection that is due.
        /// </summary>
        /// <param name="now">The current time (UTC).</param>
        /// <returns>How many connections were synchronized.</returns>
        public int Tick(DateTime now)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                return 0;
            }

            var count = 0;

            try
            {
                foreach (var settings in LdapAuthenticationSource.ReadConfigured(LdapHub.Settings).Where(x => x.Enabled && x.SyncEnabled))
                {
                    if (!IsDue(settings, now))
                    {
                        continue;
                    }

                    if (LdapHub.Synchronize(settings) is not null)
                    {
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                // the timer thread has nobody to report to but the log
                LdapHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }

            return count;
        }

        /// <summary>
        /// Returns whether a connection is due.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <returns><see langword="true"/> when its last run is older than its interval.</returns>
        public static bool IsDue(LdapSourceSettings settings, DateTime now)
        {
            if (settings is null || !settings.SyncEnabled || settings.SyncInterval <= 0)
            {
                return false;
            }

            var last = LdapHub.Status.Get(settings.ResolveId());

            return last is null || now - last.Finished >= TimeSpan.FromMinutes(Math.Max(5, settings.SyncInterval));
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
