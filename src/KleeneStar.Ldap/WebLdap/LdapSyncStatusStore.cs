using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// Remembers the last synchronization of every connection, in
    /// <c>{DataPath}/ldap/synchronization.json</c>.
    /// </summary>
    /// <remarks>
    /// This is state, not configuration, so it stays out of the versioned settings file. It is
    /// a file in the data directory rather than memory because the scheduler decides by it when
    /// a connection is due: kept in memory, every restart - and every replica - would start a
    /// run of its own. Replicas sharing the data directory share the record.
    /// </remarks>
    /// <param name="path">The full path of the file.</param>
    public sealed class LdapSyncStatusStore(string path)
    {
        private static readonly object _sync = new();

        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

        /// <summary>
        /// Returns the last synchronization of a connection.
        /// </summary>
        /// <param name="connectionId">The connection.</param>
        /// <returns>The result, or <see langword="null"/> when it was never synchronized.</returns>
        public LdapSyncResult Get(Guid connectionId)
        {
            lock (_sync)
            {
                return Read().FirstOrDefault(x => x.ConnectionId == connectionId);
            }
        }

        /// <summary>
        /// Records a synchronization, replacing the previous one of its connection.
        /// </summary>
        /// <param name="result">The result.</param>
        public void Set(LdapSyncResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            lock (_sync)
            {
                var list = Read().Where(x => x.ConnectionId != result.ConnectionId).ToList();

                list.Add(result);

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));

                var temporary = Path + ".tmp";

                File.WriteAllText(temporary, JsonSerializer.Serialize(list, _options));
                File.Move(temporary, Path, overwrite: true);
            }
        }

        /// <summary>
        /// Forgets a connection.
        /// </summary>
        /// <param name="connectionId">The connection.</param>
        public void Remove(Guid connectionId)
        {
            lock (_sync)
            {
                var list = Read();

                if (list.RemoveAll(x => x.ConnectionId == connectionId) > 0)
                {
                    File.WriteAllText(Path, JsonSerializer.Serialize(list, _options));
                }
            }
        }

        /// <summary>
        /// Reads the file; an unreadable one counts as empty, because losing the record only
        /// means the next run comes a little early.
        /// </summary>
        /// <returns>The results.</returns>
        private List<LdapSyncResult> Read()
        {
            try
            {
                return File.Exists(Path)
                    ? JsonSerializer.Deserialize<List<LdapSyncResult>>(File.ReadAllText(Path), _options) ?? []
                    : [];
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return [];
            }
        }
    }
}
