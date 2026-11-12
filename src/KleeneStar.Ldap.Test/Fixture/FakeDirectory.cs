namespace KleeneStar.Ldap.Test.Fixture
{
    /// <summary>
    /// A directory in memory: entries with passwords for the binds, canned search results, and
    /// the users the synchronization reads.
    /// </summary>
    internal sealed class FakeDirectory : ILdapDirectory
    {
        public Dictionary<string, string> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<string>> Results { get; } = new(StringComparer.Ordinal);

        public List<LdapDirectoryEntry> Users { get; } = [];

        public List<string> Binds { get; } = [];

        public List<string> Filters { get; } = [];

        public LdapDirectoryException Failure { get; set; }

        public bool Bind(string distinguishedName, string password)
        {
            Binds.Add(distinguishedName);

            return Entries.TryGetValue(distinguishedName, out var stored) && stored == password;
        }

        public IReadOnlyList<string> Search(string searchBase, string filter)
        {
            Filters.Add(filter);

            return Results.TryGetValue(filter, out var found) ? found : [];
        }

        public IReadOnlyList<LdapDirectoryEntry> SearchEntries(string searchBase, string filter, IReadOnlyCollection<string> attributes)
        {
            Filters.Add(filter);

            return Failure is not null ? throw Failure : Users;
        }

        public LdapProbeResult Probe()
        {
            return Failure is not null ? throw Failure : new LdapProbeResult(true, true, true);
        }

        /// <summary>
        /// Adds a user the synchronization reads.
        /// </summary>
        public FakeDirectory User(string login, string name = null, string mail = null, int? accountControl = null, string department = null)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sAMAccountName"] = login
            };

            if (name is not null) attributes["displayName"] = name;
            if (mail is not null) attributes["mail"] = mail;
            if (department is not null) attributes["department"] = department;
            if (accountControl is not null) attributes["userAccountControl"] = accountControl.Value.ToString();

            Users.Add(new LdapDirectoryEntry($"CN={login},OU=People,DC=corp,DC=example,DC=com", attributes));

            return this;
        }
    }
}
