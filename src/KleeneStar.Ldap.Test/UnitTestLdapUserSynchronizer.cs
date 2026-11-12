using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Ldap.Test.Fixture;
using KleeneStar.Ldap.WebLdap;
using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Ldap.Test
{
    /// <summary>
    /// Tests the synchronization of the accounts with a directory: new users get an account of
    /// the connection's source, a prepared account is claimed, attributes are copied, accounts
    /// the directory dropped or disabled are disabled, an account of another source is never
    /// taken over, and a directory answering nobody disables nobody.
    /// </summary>
    [Collection("NonParallelTests")]
    public sealed class UnitTestLdapUserSynchronizer : IDisposable
    {
        private const string Key = "ldap-sync";

        private readonly FakeDirectory _directory = new();
        private readonly LdapSourceSettings _settings = new()
        {
            Key = Key,
            Name = "Corporate",
            Server = "dc01.corp.example.com",
            ServiceDn = "CN=svc",
            ServicePassword = "x",
            SearchBase = "DC=corp,DC=example,DC=com"
        };

        public UnitTestLdapUserSynchronizer()
        {
            LdapHubFixture.Initialize("ldap-sync-" + Guid.NewGuid().ToString("N"));

            CoreHub.AuthenticationSourceManager.Register(new LdapAuthenticationSource(_settings, _directory));
            LdapUserSynchronizer.DirectoryFactory = _ => _directory;
        }

        public void Dispose()
        {
            LdapUserSynchronizer.DirectoryFactory = settings => new LdapDirectory(settings);
            CoreHub.AuthenticationSourceManager.Unregister(Key);
        }

        /// <summary>
        /// The directory's users become accounts of the source, matched by their login from then on.
        /// </summary>
        [Fact]
        public void NewUsers_GetAnAccountOfTheSource()
        {
            _directory
                .User("alice", "Alice Archer", "alice@corp.example.com", 512, "Finance")
                .User("bob", "Bob Baker");

            var result = LdapUserSynchronizer.Synchronize(_settings);

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(2, result.Found);
            Assert.Equal(2, result.Created);

            var alice = Account("alice");
            Assert.Equal(Key, alice.AuthenticationSource);
            Assert.Equal("alice", alice.ExternalSubject);
            Assert.Equal("Alice Archer", alice.Name);
            Assert.Equal("alice@corp.example.com", alice.Email);
            Assert.Equal("Finance", alice.Department);
            Assert.Equal(IdentityState.Active, alice.State);

            // a second run changes nothing
            var again = LdapUserSynchronizer.Synchronize(_settings);

            Assert.Equal(0, again.Created + again.Updated + again.Disabled);
        }

        /// <summary>
        /// A changed attribute is copied, a value the directory leaves empty is kept.
        /// </summary>
        [Fact]
        public void ChangedAttributes_AreCopied()
        {
            _directory.User("carol", "Carol", "carol@corp.example.com", department: "Sales");
            LdapUserSynchronizer.Synchronize(_settings);

            _directory.Users.Clear();
            _directory.User("carol", "Carol Carter");

            var result = LdapUserSynchronizer.Synchronize(_settings);
            var carol = Account("carol");

            Assert.Equal(1, result.Updated);
            Assert.Equal("Carol Carter", carol.Name);
            Assert.Equal("carol@corp.example.com", carol.Email);
            Assert.Equal("Sales", carol.Department);
        }

        /// <summary>
        /// An account an administrator prepared for the source is claimed by its user name.
        /// </summary>
        [Fact]
        public void PreparedAccount_IsClaimed()
        {
            var prepared = Seed("dave", Key);
            _directory.User("dave", "Dave Doe", "dave@corp.example.com");

            var result = LdapUserSynchronizer.Synchronize(_settings);
            var dave = CoreHub.IdentityManager.GetIdentity(prepared);

            Assert.Equal(0, result.Created);
            Assert.Equal("dave", dave.ExternalSubject);
            Assert.Equal("dave@corp.example.com", dave.Email);
        }

        /// <summary>
        /// An internal account carrying the login is never taken over.
        /// </summary>
        [Fact]
        public void AccountOfAnotherSource_IsAConflict()
        {
            var local = Seed("erin", null);
            _directory.User("erin", "Erin Evil", "erin@evil.example.com");

            var result = LdapUserSynchronizer.Synchronize(_settings);
            var erin = CoreHub.IdentityManager.GetIdentity(local);

            Assert.Equal(1, result.Conflicts);
            Assert.Equal(0, result.Created);
            Assert.Null(erin.AuthenticationSource);
            Assert.Null(erin.ExternalSubject);
            Assert.NotEqual("erin@evil.example.com", erin.Email);
        }

        /// <summary>
        /// Accounts the directory dropped or disabled are disabled, and enabled again when the
        /// directory has them back; a disabled user the installation never knew gets no account.
        /// </summary>
        [Fact]
        public void MissingAndDisabledUsers_AreDisabled()
        {
            _directory.User("frank").User("grace").User("heidi");
            LdapUserSynchronizer.Synchronize(_settings);

            _directory.Users.Clear();
            _directory.User("frank").User("grace", accountControl: 514).User("ivan", accountControl: 514);

            var result = LdapUserSynchronizer.Synchronize(_settings);

            Assert.Equal(2, result.Disabled);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(IdentityState.Active, Account("frank").State);
            Assert.Equal(IdentityState.Disabled, Account("grace").State);
            Assert.Equal(IdentityState.Disabled, Account("heidi").State);
            Assert.Null(Account("ivan"));

            _directory.Users.Clear();
            _directory.User("frank").User("grace").User("heidi");

            var back = LdapUserSynchronizer.Synchronize(_settings);

            Assert.Equal(2, back.Enabled);
            Assert.Equal(IdentityState.Active, Account("heidi").State);
        }

        /// <summary>
        /// A lock set by an administrator is not lifted by the synchronization.
        /// </summary>
        [Fact]
        public void LockedAccount_StaysLocked()
        {
            _directory.User("judy");
            LdapUserSynchronizer.Synchronize(_settings);

            var judy = Account("judy");
            judy.State = IdentityState.Locked;
            CoreHub.IdentityManager.Update(judy);

            LdapUserSynchronizer.Synchronize(_settings);

            Assert.Equal(IdentityState.Locked, Account("judy").State);
        }

        /// <summary>
        /// A directory that answers nobody - a wrong filter, most likely - disables nobody.
        /// </summary>
        [Fact]
        public void EmptyAnswer_DisablesNobody()
        {
            _directory.User("ken");
            LdapUserSynchronizer.Synchronize(_settings);

            _directory.Users.Clear();

            var result = LdapUserSynchronizer.Synchronize(_settings);

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.Disabled);
            Assert.Equal(IdentityState.Active, Account("ken").State);
        }

        /// <summary>
        /// Entries that all lack the login attribute - a misspelt attribute, say - fail the run
        /// before anything is written, instead of letting every account count as gone.
        /// </summary>
        [Fact]
        public void EntriesWithoutLogin_FailTheRunAndDisableNobody()
        {
            _directory.User("lena").User("mike");
            LdapUserSynchronizer.Synchronize(_settings);

            _directory.Users.Clear();
            _directory.Users.Add(new LdapDirectoryEntry("CN=lena,DC=corp", new Dictionary<string, string> { ["displayName"] = "Lena" }));
            _directory.Users.Add(new LdapDirectoryEntry("CN=mike,DC=corp", new Dictionary<string, string> { ["displayName"] = "Mike" }));

            var result = LdapUserSynchronizer.Synchronize(_settings);

            Assert.False(result.Succeeded);
            Assert.Contains("sAMAccountName", result.Error);
            Assert.Equal(0, result.Disabled);
            Assert.Equal(IdentityState.Active, Account("lena").State);
            Assert.Equal(IdentityState.Active, Account("mike").State);
        }

        /// <summary>
        /// A directory that cannot be reached fails the run and changes nothing.
        /// </summary>
        [Fact]
        public void UnreachableDirectory_FailsTheRun()
        {
            _directory.Failure = new LdapDirectoryException("The directory could not be reached.");

            var result = LdapUserSynchronizer.Synchronize(_settings);

            Assert.False(result.Succeeded);
            Assert.Equal("The directory could not be reached.", result.Error);
        }

        /// <summary>
        /// A connection without a search base cannot be synchronized.
        /// </summary>
        [Fact]
        public void IncompleteConnection_IsRefused()
        {
            var settings = _settings.Clone();
            settings.SearchBase = null;

            Assert.NotNull(LdapUserSynchronizer.Check(settings));
            Assert.False(LdapUserSynchronizer.Synchronize(settings).Succeeded);
        }

        /// <summary>
        /// Returns the account with a user name.
        /// </summary>
        private static Identity Account(string userName)
        {
            return CoreHub.IdentityManager.GetIdentities(new Query<Identity>())
                .FirstOrDefault(x => string.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Stores an account.
        /// </summary>
        private static Guid Seed(string userName, string source)
        {
            var id = Guid.NewGuid();

            CoreHub.IdentityManager.Add(new Identity(id)
            {
                Name = userName,
                UserName = userName,
                Email = userName + "@example.com",
                State = IdentityState.Active,
                AuthenticationSource = source
            });

            return id;
        }
    }
}
