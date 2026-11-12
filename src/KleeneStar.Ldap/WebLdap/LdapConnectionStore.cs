using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// Keeps the LDAP connections in the plugin's own settings file,
    /// <c>settings/kleenestar.ldap.settings.json</c>, under
    /// <c>Plugins:kleenestar.ldap:Directories</c> - the section the sign-in reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The settings page owns this one file: it lists what the file holds and writes changes
    /// back into it. Everything else of the file - other sections an operator put there - is
    /// kept; comments are not, because the file is rewritten as plain json. A save replaces the
    /// file in one move (written beside it first), so the configuration watcher never reads a
    /// half-written file, and the configuration is reloaded right away, which re-registers the
    /// sign-in sources (<see cref="LdapAuthenticationSource.RegisterConfigured"/>).
    /// </para>
    /// <para>
    /// The service password is stored in the file, as the other settings are. It never leaves
    /// the server again: the page shows an empty field, and an empty field on a save keeps the
    /// stored password. An installation that keeps secrets out of files sets
    /// <c>WEBEXPRESS_Plugins__kleenestar.ldap__Secrets__{key}__ServicePassword</c> instead
    /// (<see cref="LdapAuthenticationSource.SecretsSection"/>): bound to the connection's key, it stays
    /// with its connection when another one is deleted and the list moves up. An override by
    /// position (<c>…__Directories__{index}__…</c>) does not, and must not be used.
    /// </para>
    /// </remarks>
    /// <param name="path">The full path of the settings file.</param>
    /// <param name="configuration">The configuration to reload after a save. May be null.</param>
    public sealed class LdapConnectionStore(string path, IConfigurationRoot configuration = null)
    {
        /// <summary>
        /// The name of the plugin's settings file.
        /// </summary>
        public const string FileName = "kleenestar.ldap.settings.json";

        private static readonly object _sync = new();

        private static readonly JsonDocumentOptions _documentOptions = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions _readOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions _writeOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Gets the full path of the settings file.
        /// </summary>
        public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

        /// <summary>
        /// Returns the connections the file holds, in their order. A missing file holds none.
        /// </summary>
        /// <returns>Copies of the connections.</returns>
        public IReadOnlyList<LdapSourceSettings> GetConnections()
        {
            lock (_sync)
            {
                return Read(out _);
            }
        }

        /// <summary>
        /// Returns a connection.
        /// </summary>
        /// <param name="id">Its id.</param>
        /// <returns>A copy of the connection, or <see langword="null"/>.</returns>
        public LdapSourceSettings GetConnection(Guid id)
        {
            return GetConnections().FirstOrDefault(x => x.ResolveId() == id);
        }

        /// <summary>
        /// Adds a connection at the end of the list and gives it an id when it has none.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>The id of the connection.</returns>
        public Guid Add(LdapSourceSettings connection)
        {
            ArgumentNullException.ThrowIfNull(connection);

            lock (_sync)
            {
                var list = Read(out var root).ToList();
                var stored = connection.Clone();

                if (!Guid.TryParse(stored.Id, out var id) || id == Guid.Empty || list.Any(x => x.ResolveId() == id))
                {
                    id = Guid.NewGuid();
                }

                stored.Id = id.ToString();
                list.Add(stored);

                Write(root, list);

                return id;
            }
        }

        /// <summary>
        /// Replaces a connection, keeping its place in the list. An empty service password keeps
        /// the stored one.
        /// </summary>
        /// <param name="connection">The connection, carrying the id of the one to replace.</param>
        /// <returns><see langword="true"/> when the connection was found.</returns>
        public bool Update(LdapSourceSettings connection)
        {
            ArgumentNullException.ThrowIfNull(connection);

            lock (_sync)
            {
                var list = Read(out var root).ToList();
                var id = connection.ResolveId();
                var index = list.FindIndex(x => x.ResolveId() == id);

                if (index < 0)
                {
                    return false;
                }

                var stored = connection.Clone();
                stored.Id = id.ToString();

                if (string.IsNullOrEmpty(stored.ServicePassword))
                {
                    stored.ServicePassword = list[index].ServicePassword;
                }

                list[index] = stored;

                Write(root, list);

                return true;
            }
        }

        /// <summary>
        /// Removes a connection.
        /// </summary>
        /// <param name="id">Its id.</param>
        /// <returns><see langword="true"/> when the connection was found.</returns>
        public bool Remove(Guid id)
        {
            lock (_sync)
            {
                var list = Read(out var root).ToList();

                if (list.RemoveAll(x => x.ResolveId() == id) == 0)
                {
                    return false;
                }

                Write(root, list);

                return true;
            }
        }

        /// <summary>
        /// Reads the file.
        /// </summary>
        /// <param name="root">The whole document, so a save keeps what else it holds.</param>
        /// <returns>The connections.</returns>
        private List<LdapSourceSettings> Read(out JsonObject root)
        {
            root = new JsonObject();

            if (!File.Exists(Path))
            {
                return [];
            }

            var text = File.ReadAllText(Path);

            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            root = JsonNode.Parse(text, documentOptions: _documentOptions) as JsonObject
                ?? throw new InvalidDataException($"The settings file '{Path}' does not hold a json object.");

            var directories = Child(Child(Child(root, "Plugins"), "kleenestar.ldap"), LdapAuthenticationSource.Section) as JsonArray;

            if (directories is null)
            {
                return [];
            }

            var list = new List<LdapSourceSettings>();

            foreach (var node in directories)
            {
                if (node is JsonObject)
                {
                    list.Add(node.Deserialize<LdapSourceSettings>(_readOptions) ?? new LdapSourceSettings());
                }
            }

            return list;
        }

        /// <summary>
        /// Writes the connections into the document and replaces the file with it.
        /// </summary>
        /// <param name="root">The document.</param>
        /// <param name="connections">The connections.</param>
        private void Write(JsonObject root, List<LdapSourceSettings> connections)
        {
            foreach (var connection in connections)
            {
                // a hand-written entry gets its derived id written down, so it keeps it
                // when its key changes later
                connection.Id = connection.ResolveId().ToString();
            }

            var plugin = Ensure(Ensure(root, "Plugins"), "kleenestar.ldap");
            var name = plugin.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, LdapAuthenticationSource.Section, StringComparison.OrdinalIgnoreCase))
                ?? LdapAuthenticationSource.Section;

            plugin[name] = JsonSerializer.SerializeToNode(connections, _writeOptions);

            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // not *.json, so the watcher does not read it half written
            var temporary = Path + ".tmp";

            File.WriteAllText(temporary, root.ToJsonString(_writeOptions));
            File.Move(temporary, Path, overwrite: true);

            try
            {
                configuration?.Reload();
            }
            catch (Exception)
            {
                // the watcher reloads on its own; a reload that fails here keeps the previous
                // values, as it would there
            }
        }

        /// <summary>
        /// Returns the child of an object, its name compared without case as the configuration does.
        /// </summary>
        /// <param name="node">The object. May be null.</param>
        /// <param name="name">The name of the child.</param>
        /// <returns>The child, or <see langword="null"/>.</returns>
        private static JsonNode Child(JsonNode node, string name)
        {
            return node is JsonObject obj
                ? obj.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value
                : null;
        }

        /// <summary>
        /// Returns the child object of an object, creating it when it is missing.
        /// </summary>
        /// <param name="node">The object.</param>
        /// <param name="name">The name of the child.</param>
        /// <returns>The child object.</returns>
        private static JsonObject Ensure(JsonObject node, string name)
        {
            if (Child(node, name) is JsonObject existing)
            {
                return existing;
            }

            var key = node.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)) ?? name;
            var created = new JsonObject();

            node[key] = created;

            return created;
        }
    }
}
