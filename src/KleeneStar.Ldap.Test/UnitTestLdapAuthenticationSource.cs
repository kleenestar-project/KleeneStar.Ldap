using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;

namespace KleeneStar.Ldap.Test
{
    /// <summary>
    /// Tests the LDAP and Active Directory sign-in: a password is checked by binding as the
    /// account's entry, found directly or by a search; an empty password never reaches the
    /// directory; a crafted login cannot name another entry or widen a search; an account of
    /// the source signs in with the directory's password and no other.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestLdapAuthenticationSource
    {
        /// <summary>
        /// A directory that knows a set of entries and their passwords, and records what it was asked.
        /// </summary>
        private sealed class FakeDirectory : ILdapDirectory
        {
            /// <summary>Gets the entries and passwords.</summary>
            public Dictionary<string, string> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Gets the search results by filter.</summary>
            public Dictionary<string, List<string>> Results { get; } = new(StringComparer.Ordinal);

            /// <summary>Gets the binds made.</summary>
            public List<string> Binds { get; } = [];

            /// <summary>Gets the filters searched.</summary>
            public List<string> Filters { get; } = [];

            /// <inheritdoc/>
            public bool Bind(string distinguishedName, string password)
            {
                Binds.Add(distinguishedName);

                return Entries.TryGetValue(distinguishedName, out var stored) && stored == password;
            }

            /// <inheritdoc/>
            public IReadOnlyList<string> Search(string searchBase, string filter)
            {
                Filters.Add(filter);

                return Results.TryGetValue(filter, out var found) ? found : [];
            }
        }

        /// <summary>
        /// Direct bind: the login fills the format; the right password signs in, a wrong or empty
        /// one does not, and the empty one never reaches the directory.
        /// </summary>
        [Fact]
        public void DirectBind_ChecksThePassword()
        {
            var directory = new FakeDirectory();
            directory.Entries["alice@corp.example.com"] = "s3cret";

            var source = new LdapAuthenticationSource(new LdapSourceSettings { Key = "ad", BindDnFormat = "{0}@corp.example.com" }, directory);
            var alice = new Identity { UserName = "alice", AuthenticationSource = "ad" };

            Assert.True(source.VerifyPassword(alice, "s3cret"));
            Assert.False(source.VerifyPassword(alice, "wrong"));

            directory.Binds.Clear();
            Assert.False(source.VerifyPassword(alice, string.Empty));
            Assert.Empty(directory.Binds);

            // the directory login wins over the user name where it differs
            directory.Entries["a.smith@corp.example.com"] = "other";
            Assert.True(source.VerifyPassword(new Identity { UserName = "alice", ExternalSubject = "a.smith" }, "other"));
        }

        /// <summary>
        /// A login with the characters of a distinguished name or a filter is escaped: it cannot
        /// name another entry or turn a search into a wildcard.
        /// </summary>
        [Fact]
        public void Logins_AreEscaped()
        {
            Assert.Equal(@"admin\,ou\=admins", LdapAuthenticationSource.EscapeDn("admin,ou=admins"));
            Assert.Equal(@"\#x\ ", LdapAuthenticationSource.EscapeDn("#x "));
            Assert.Equal(@"\2a\29\28uid=\2a", LdapAuthenticationSource.EscapeFilter("*)(uid=*"));

            var directory = new FakeDirectory();
            var source = new LdapAuthenticationSource(new LdapSourceSettings
            {
                Key = "ldap",
                ServiceDn = "cn=svc",
                ServicePassword = "x",
                SearchBase = "dc=example,dc=com",
                SearchFilter = "(uid={0})"
            }, directory);

            Assert.False(source.VerifyPassword(new Identity { UserName = "*" }, "anything"));
            Assert.Equal(@"(uid=\2a)", Assert.Single(directory.Filters));
        }

        /// <summary>
        /// Search then bind: exactly one entry found is bound; none or several sign nobody in.
        /// </summary>
        [Fact]
        public void SearchThenBind_NeedsExactlyOneEntry()
        {
            var directory = new FakeDirectory();
            directory.Results["(sAMAccountName=bob)"] = ["cn=Bob,ou=people,dc=example,dc=com"];
            directory.Results["(sAMAccountName=twin)"] = ["cn=Twin1,dc=example,dc=com", "cn=Twin2,dc=example,dc=com"];
            directory.Entries["cn=Bob,ou=people,dc=example,dc=com"] = "pw";
            directory.Entries["cn=Twin1,dc=example,dc=com"] = "pw";

            var source = new LdapAuthenticationSource(new LdapSourceSettings { Key = "corp", ServiceDn = "cn=svc", ServicePassword = "x", SearchBase = "dc=example,dc=com" }, directory);

            Assert.True(source.VerifyPassword(new Identity { UserName = "bob" }, "pw"));
            Assert.False(source.VerifyPassword(new Identity { UserName = "twin" }, "pw"));
            Assert.False(source.VerifyPassword(new Identity { UserName = "nobody" }, "pw"));
        }

        /// <summary>
        /// A configuration without a server or a way to find entries is refused at registration,
        /// logged and skipped; a complete one is registered.
        /// </summary>
        [Fact]
        public void RegisterConfigured_SkipsIncompleteDirectories()
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Directories:0:Key"] = "ldap-complete",
                ["Directories:0:Server"] = "ldap.example.com",
                ["Directories:0:BindDnFormat"] = "uid={0},ou=people,dc=example,dc=com",
                ["Directories:1:Key"] = "ldap-incomplete",
                ["Directories:1:Server"] = "ldap.example.com",
                ["Directories:2:Key"] = "local",
                ["Directories:2:Server"] = "ldap.example.com",
                ["Directories:2:BindDnFormat"] = "{0}"
            }).Build();

            var registered = LdapAuthenticationSource.RegisterConfigured(settings, null);

            Assert.Equal("ldap-complete", Assert.Single(registered).Key);
            Assert.NotNull(AuthenticationSourceCatalog.Resolve(new Identity { AuthenticationSource = "ldap-complete" }));
            Assert.Null(AuthenticationSourceCatalog.Resolve(new Identity { AuthenticationSource = "ldap-incomplete" }));
        }

        /// <summary>
        /// A registered directory is the source the core resolves for the accounts that name
        /// it, and the password check the sign-in asks is the directory's.
        /// </summary>
        [Fact]
        public void Catalog_ResolvesTheDirectoryForItsAccounts()
        {
            var directory = new FakeDirectory();
            directory.Entries["uid=carol,dc=example,dc=com"] = "directory-pw";

            AuthenticationSourceCatalog.Register(new LdapAuthenticationSource(new LdapSourceSettings { Key = "ldap-signin", BindDnFormat = "uid={0},dc=example,dc=com" }, directory));

            var carol = new Identity { UserName = "carol", AuthenticationSource = "ldap-signin" };
            var source = AuthenticationSourceCatalog.Resolve(carol);

            Assert.IsType<LdapAuthenticationSource>(source);
            Assert.True(source.IsExternal && source.AcceptsPassword && !source.Provisions);
            Assert.True(source.VerifyPassword(carol, "directory-pw"));
            Assert.False(source.VerifyPassword(carol, "wrong"));
        }

        /// <summary>
        /// The plugin registers the directories of its own settings when it runs.
        /// </summary>
        [Fact]
        public void Plugin_RegistersItsDirectories()
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Directories:0:Key"] = "ldap-plugin",
                ["Directories:0:Server"] = "ldap.example.com",
                ["Directories:0:BindDnFormat"] = "{0}@example.com"
            }).Build();

            new KleeneStarPlugin(new StubPluginContext(settings), null).Run();

            Assert.IsType<LdapAuthenticationSource>(AuthenticationSourceCatalog.Resolve(new Identity { AuthenticationSource = "ldap-plugin" }));
        }
    }
}
