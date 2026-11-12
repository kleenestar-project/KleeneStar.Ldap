using System;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// The outcome of one synchronization of a connection.
    /// </summary>
    public sealed class LdapSyncResult
    {
        /// <summary>Gets or sets the connection.</summary>
        public Guid ConnectionId { get; set; }

        /// <summary>Gets or sets the key of the connection's source.</summary>
        public string Key { get; set; }

        /// <summary>Gets or sets when the run started (UTC).</summary>
        public DateTime Started { get; set; }

        /// <summary>Gets or sets when the run ended (UTC).</summary>
        public DateTime Finished { get; set; }

        /// <summary>Gets or sets whether the run completed.</summary>
        public bool Succeeded { get; set; }

        /// <summary>Gets or sets why the run failed, in English.</summary>
        public string Error { get; set; }

        /// <summary>Gets or sets how many accounts the directory returned.</summary>
        public int Found { get; set; }

        /// <summary>Gets or sets how many accounts were created.</summary>
        public int Created { get; set; }

        /// <summary>Gets or sets how many accounts were changed.</summary>
        public int Updated { get; set; }

        /// <summary>Gets or sets how many accounts were disabled.</summary>
        public int Disabled { get; set; }

        /// <summary>Gets or sets how many accounts were enabled again.</summary>
        public int Enabled { get; set; }

        /// <summary>Gets or sets how many entries were skipped: no login, a duplicate login, or disabled in the directory and unknown here.</summary>
        public int Skipped { get; set; }

        /// <summary>Gets or sets how many entries name a login an account of another sign-in source already carries.</summary>
        public int Conflicts { get; set; }

        /// <summary>
        /// Returns a failed result.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="started">When the run started.</param>
        /// <param name="error">Why it failed.</param>
        /// <returns>The result.</returns>
        public static LdapSyncResult Fail(LdapSourceSettings settings, DateTime started, string error)
        {
            return new LdapSyncResult
            {
                ConnectionId = settings?.ResolveId() ?? Guid.Empty,
                Key = settings?.Key,
                Started = started,
                Finished = DateTime.UtcNow,
                Succeeded = false,
                Error = error
            };
        }
    }
}
