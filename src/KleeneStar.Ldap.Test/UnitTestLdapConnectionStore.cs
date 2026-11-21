using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebRestApi;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Nodes;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Ldap.Test
{
    /// <summary>
    /// Tests the settings file the settings page writes: the connections land under
    /// <c>Plugins:kleenestar.ldap:Directories</c> where the sign-in reads them, the rest of the
    /// file is kept, a hand-written entry is addressable, and the service password survives an
    /// edit that leaves it empty.
    /// </summary>
    public sealed class UnitTestLdapConnectionStore : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "kleenestar-ldap-" + Guid.NewGuid().ToString("N"));

        private string File => Path.Combine(_directory, LdapConnectionStore.FileName);

        public UnitTestLdapConnectionStore()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        /// <summary>
        /// What the page stores is what the configuration - and so the sign-in - reads.
        /// </summary>
        [Fact]
        public void Add_WritesTheSectionTheSignInReads()
        {
            var store = new LdapConnectionStore(File);
            var id = store.Add(new LdapSourceSettings { Key = "ad", Name = "Corporate", Server = "dc01.corp.example.com", BindDnFormat = "{0}@corp.example.com" });

            var configuration = new ConfigurationBuilder().AddJsonFile(File).Build();
            var configured = LdapAuthenticationSource.ReadConfigured(configuration.GetSection("Plugins:kleenestar.ldap"));

            var entry = Assert.Single(configured);
            Assert.Equal(id, entry.ResolveId());
            Assert.Equal("ad", entry.Key);
            Assert.Equal("dc01.corp.example.com", entry.Server);
            Assert.True(entry.UseSsl);
        }

        /// <summary>
        /// A save keeps the other sections of the file.
        /// </summary>
        [Fact]
        public void Save_KeepsTheRestOfTheFile()
        {
            System.IO.File.WriteAllText(File, """
                {
                  // an operator's note
                  "Plugins": {
                    "kleenestar.ldap": { "Other": "kept" },
                    "somebody.else": { "Value": 42 }
                  }
                }
                """);

            new LdapConnectionStore(File).Add(new LdapSourceSettings { Key = "ldap", Server = "ldap.example.com", BindDnFormat = "uid={0},dc=example,dc=com" });

            var root = JsonNode.Parse(System.IO.File.ReadAllText(File));

            Assert.Equal("kept", (string)root["Plugins"]["kleenestar.ldap"]["Other"]);
            Assert.Equal(42, (int)root["Plugins"]["somebody.else"]["Value"]);
            Assert.Single(root["Plugins"]["kleenestar.ldap"]["Directories"].AsArray());
        }

        /// <summary>
        /// An entry written by hand without an id is addressable under an id derived from its
        /// key, and keeps it once the page saved the file.
        /// </summary>
        [Fact]
        public void HandWrittenEntry_GetsAStableId()
        {
            System.IO.File.WriteAllText(File, """
                { "plugins": { "KleeneStar.Ldap": { "directories": [ { "key": "corp", "server": "dc", "bindDnFormat": "{0}@corp", "port": "389" } ] } } }
                """);

            var store = new LdapConnectionStore(File);
            var entry = Assert.Single(store.GetConnections());
            var id = entry.ResolveId();

            Assert.Equal(389, entry.Port);
            Assert.Equal(id, Assert.Single(store.GetConnections()).ResolveId());

            entry.Key = "corp-renamed";
            entry.Id = id.ToString();
            Assert.True(store.Update(entry));

            Assert.Equal(id, Assert.Single(store.GetConnections()).ResolveId());
            Assert.Equal("corp-renamed", store.GetConnection(id).Key);
        }

        /// <summary>
        /// The password is never sent to the browser, and an edit that leaves it empty keeps it.
        /// </summary>
        [Fact]
        public void ServicePassword_StaysOnTheServer()
        {
            var store = new LdapConnectionStore(File);
            var id = store.Add(new LdapSourceSettings { Key = "svc", Server = "dc", ServiceDn = "cn=svc", ServicePassword = "secret", SearchBase = "dc=corp" });

            Assert.Equal(string.Empty, LdapConnectionItem.From(store.GetConnection(id)).ServicePassword);

            var edited = store.GetConnection(id);
            var invalid = new List<string>();
            LdapConnectionItem.Apply(new RestApiCrudFormData { ["servicepassword"] = "", ["server"] = "dc02" }, edited, invalid);
            store.Update(edited);

            Assert.Empty(invalid);
            Assert.Equal("secret", store.GetConnection(id).ServicePassword);
            Assert.Equal("dc02", store.GetConnection(id).Server);

            LdapConnectionItem.Apply(new RestApiCrudFormData { ["servicepassword"] = "changed" }, edited, invalid);
            store.Update(edited);

            Assert.Equal("changed", store.GetConnection(id).ServicePassword);
        }

        /// <summary>
        /// The form's values are read as the controls post them.
        /// </summary>
        [Fact]
        public void Apply_ReadsTheFormValues()
        {
            var settings = new LdapSourceSettings();
            var invalid = new List<string>();

            LdapConnectionItem.Apply(new RestApiCrudFormData
            {
                ["protocol"] = LdapConnectionItem.IdOf(LdapProtocol.StartTls).ToString(),
                ["port"] = "",
                ["syncenabled"] = "on",
                ["disablemissing"] = false,
                ["syncinterval"] = 30L,
                ["binddnformat"] = "",
                ["timeout"] = "abc"
            }, settings, invalid);

            Assert.Equal(LdapProtocol.StartTls, settings.Protocol);
            Assert.Equal(0, settings.Port);
            Assert.True(settings.SyncEnabled);
            Assert.False(settings.DisableMissing);
            Assert.Equal(30, settings.SyncInterval);
            Assert.Null(settings.BindDnFormat);
            Assert.Equal(["Timeout"], invalid);
        }

        /// <summary>
        /// A cleared attribute stays cleared: it is stored as the empty name, not left out of
        /// the file, where it would read back as its default.
        /// </summary>
        [Fact]
        public void ClearedAttribute_StaysCleared()
        {
            var store = new LdapConnectionStore(File);
            var id = store.Add(new LdapSourceSettings { Key = "attr", Server = "dc", BindDnFormat = "{0}" });
            var edited = store.GetConnection(id);

            LdapConnectionItem.Apply(new RestApiCrudFormData { ["departmentattribute"] = "", ["phoneattribute"] = " " }, edited, new List<string>());
            store.Update(edited);

            var stored = store.GetConnection(id);
            Assert.Equal(string.Empty, stored.DepartmentAttribute);
            Assert.Equal(string.Empty, stored.PhoneAttribute);
            Assert.Equal("title", stored.PositionAttribute);

            var configured = LdapAuthenticationSource.ReadConfigured(new ConfigurationBuilder().AddJsonFile(File).Build().GetSection("Plugins:kleenestar.ldap"));
            Assert.Equal(string.Empty, Assert.Single(configured).DepartmentAttribute);
        }

        /// <summary>
        /// The table refreshes after a write only when its item type is a domain: the table
        /// declares no domain otherwise, and the CRUD endpoint's change notification is dropped.
        /// </summary>
        [Fact]
        public void TableItem_IsADomainOfTheChangeChannel()
        {
            Assert.True(typeof(WebExpress.WebCore.WebDomain.IDomain).IsAssignableFrom(typeof(LdapConnectionItem)));
        }

        /// <summary>
        /// A removed connection is gone from the file.
        /// </summary>
        [Fact]
        public void Remove_TakesTheConnectionOut()
        {
            var store = new LdapConnectionStore(File);
            var keep = store.Add(new LdapSourceSettings { Key = "a", Server = "dc", BindDnFormat = "{0}" });
            var drop = store.Add(new LdapSourceSettings { Key = "b", Server = "dc", BindDnFormat = "{0}" });

            Assert.True(store.Remove(drop));
            Assert.False(store.Remove(drop));
            Assert.Equal(keep, Assert.Single(store.GetConnections()).ResolveId());
        }
    }
}
