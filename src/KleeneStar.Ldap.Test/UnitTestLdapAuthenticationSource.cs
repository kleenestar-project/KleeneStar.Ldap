using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using KleeneStar.Ldap.Test.Fixture;
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
            Assert.NotNull(CoreHub.AuthenticationSourceManager.Resolve(new Identity { AuthenticationSource = "ldap-complete" }));
            Assert.Null(CoreHub.AuthenticationSourceManager.Resolve(new Identity { AuthenticationSource = "ldap-incomplete" }));
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

            CoreHub.AuthenticationSourceManager.Register(new LdapAuthenticationSource(new LdapSourceSettings { Key = "ldap-signin", BindDnFormat = "uid={0},dc=example,dc=com" }, directory));

            var carol = new Identity { UserName = "carol", AuthenticationSource = "ldap-signin" };
            var source = CoreHub.AuthenticationSourceManager.Resolve(carol);

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

            Assert.IsType<LdapAuthenticationSource>(CoreHub.AuthenticationSourceManager.Resolve(new Identity { AuthenticationSource = "ldap-plugin" }));
        }

        /// <summary>
        /// A reload of the settings brings the catalog in step: a connection that was disabled
        /// or removed stops signing anybody in, a new one starts.
        /// </summary>
        [Fact]
        public void RegisterConfigured_FollowsTheSettings()
        {
            var data = new Dictionary<string, string>
            {
                ["Directories:0:Key"] = "ldap-follow-a",
                ["Directories:0:Server"] = "ldap.example.com",
                ["Directories:0:BindDnFormat"] = "{0}@example.com",
                ["Directories:1:Key"] = "ldap-follow-b",
                ["Directories:1:Server"] = "ldap.example.com",
                ["Directories:1:BindDnFormat"] = "{0}@example.com"
            };

            LdapAuthenticationSource.RegisterConfigured(new ConfigurationBuilder().AddInMemoryCollection(data).Build(), null);

            Assert.NotNull(CoreHub.AuthenticationSourceManager.GetSource("ldap-follow-a"));
            Assert.NotNull(CoreHub.AuthenticationSourceManager.GetSource("ldap-follow-b"));

            data["Directories:0:Enabled"] = "false";
            data.Remove("Directories:1:Key");
            data.Remove("Directories:1:Server");
            data.Remove("Directories:1:BindDnFormat");
            data["Directories:1:Key"] = "ldap-follow-c";
            data["Directories:1:Server"] = "ldap.example.com";
            data["Directories:1:BindDnFormat"] = "{0}@example.com";

            LdapAuthenticationSource.RegisterConfigured(new ConfigurationBuilder().AddInMemoryCollection(data).Build(), null);

            Assert.Null(CoreHub.AuthenticationSourceManager.GetSource("ldap-follow-a"));
            Assert.Null(CoreHub.AuthenticationSourceManager.GetSource("ldap-follow-b"));
            Assert.NotNull(CoreHub.AuthenticationSourceManager.GetSource("ldap-follow-c"));
        }

        /// <summary>
        /// A service password kept outside the file is bound to the connection's key, not to its
        /// position: when an earlier connection is deleted and the list moves up, the password
        /// stays with its connection and reaches no other server.
        /// </summary>
        [Fact]
        public void Secret_FollowsTheKeyNotThePosition()
        {
            IReadOnlyList<LdapSourceSettings> Read(bool withFirst)
            {
                var data = new Dictionary<string, string>
                {
                    ["Secrets:corp:ServicePassword"] = "corp-secret",
                    ["Secrets:lab:ServicePassword"] = "lab-secret"
                };
                var entries = withFirst ? new[] { "corp", "lab" } : new[] { "lab" };

                for (var index = 0; index < entries.Length; index++)
                {
                    data[$"Directories:{index}:Key"] = entries[index];
                    data[$"Directories:{index}:Server"] = entries[index] + ".example.com";
                    data[$"Directories:{index}:ServicePassword"] = "from-the-file";
                }

                return LdapAuthenticationSource.ReadConfigured(new ConfigurationBuilder().AddInMemoryCollection(data).Build());
            }

            var before = Read(withFirst: true);
            Assert.Equal("corp-secret", before.Single(x => x.Key == "corp").ServicePassword);
            Assert.Equal("lab-secret", before.Single(x => x.Key == "lab").ServicePassword);

            // the first connection is deleted: the second moves to position 0 and keeps its own secret
            var after = Assert.Single(Read(withFirst: false));
            Assert.Equal("lab", after.Key);
            Assert.Equal("lab-secret", after.ServicePassword);
            Assert.Equal("lab.example.com", after.Server);
        }

        /// <summary>
        /// A key another kind of source holds is left to it.
        /// </summary>
        [Fact]
        public void RegisterConfigured_LeavesAForeignKeyAlone()
        {
            var foreign = new ForeignSource("ldap-foreign");
            CoreHub.AuthenticationSourceManager.Register(foreign);

            LdapAuthenticationSource.RegisterConfigured(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Directories:0:Key"] = "ldap-foreign",
                ["Directories:0:Server"] = "ldap.example.com",
                ["Directories:0:BindDnFormat"] = "{0}@example.com"
            }).Build(), null);

            Assert.Same(foreign, CoreHub.AuthenticationSourceManager.GetSource("ldap-foreign"));

            CoreHub.AuthenticationSourceManager.Unregister("ldap-foreign");
        }

        /// <summary>
        /// The transport follows the protocol, and so does the default port.
        /// </summary>
        [Fact]
        public void Protocol_SetsTransportAndPort()
        {
            var settings = new LdapSourceSettings();

            Assert.Equal(LdapProtocol.Ldaps, settings.Protocol);
            Assert.Equal(636, settings.EffectivePort);

            settings.SetProtocol(LdapProtocol.StartTls);
            Assert.True(settings.StartTls && !settings.UseSsl);
            Assert.Equal(389, settings.EffectivePort);

            settings.SetProtocol(LdapProtocol.Ldap);
            Assert.False(settings.StartTls || settings.UseSsl);

            settings.Port = 10389;
            Assert.Equal(10389, settings.EffectivePort);
        }

        /// <summary>
        /// A source of another kind under a key.
        /// </summary>
        private sealed class ForeignSource(string key) : IAuthenticationSource
        {
            public string Key => key;

            public string Name => key;

            public int Order => 10;

            public bool IsExternal => true;

            public bool AcceptsPassword => false;

            public bool Provisions => false;

            public bool VerifyPassword(Identity account, string password) => false;
        }
    }
}
