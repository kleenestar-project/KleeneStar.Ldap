using KleeneStar.Core;
using KleeneStar.Model;
using KleeneStar.Model.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;
using System.Reflection;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Ldap.Test.Fixture
{
    /// <summary>
    /// Wires <see cref="CoreHub"/> and the model to an isolated in-memory database, the way the
    /// core's own test fixture does - but by reflection over <see cref="CoreHub"/>'s manager
    /// backing fields, so the list never has to be kept in step with the core by hand.
    /// </summary>
    internal static class LdapHubFixture
    {
        /// <summary>
        /// Points the model at an in-memory database and constructs every manager of the core.
        /// </summary>
        /// <param name="database">A unique name of the in-memory database.</param>
        public static void Initialize(string database)
        {
            // ModelHub is internal to the model; its settings are reached by reflection
            var modelHub = typeof(DatabaseSettings).Assembly.GetType("KleeneStar.Model.ModelHub", throwOnError: true);

            modelHub.GetProperty("DatabaseSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, new DatabaseSettings
                {
                    Assembly = typeof(LdapHubFixture).Assembly.GetName().Name,
                    ConnectionString = database
                });

            foreach (var field in typeof(CoreHub).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                var ctor = field.FieldType.IsClass && !field.FieldType.IsAbstract
                    ? field.FieldType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IComponentHub), typeof(IHttpServerContext)], null)
                    : null;

                if (ctor is not null && typeof(IComponentManager).IsAssignableFrom(field.FieldType))
                {
                    field.SetValue(null, ctor.Invoke([null, null]));
                }
            }
        }
    }

    /// <summary>
    /// The provider the model loads by the fixture's assembly name: the first public static
    /// <c>Create(string)</c> of this assembly.
    /// </summary>
    public static class InMemoryDbContextFactory
    {
        private static readonly ConcurrentDictionary<string, DbContextOptions<KleeneStarDbContext>> _options = new();

        /// <summary>
        /// Creates a context over the named in-memory database.
        /// </summary>
        /// <param name="connectionString">The database name.</param>
        /// <returns>The context.</returns>
        public static KleeneStarDbContext Create(string connectionString)
        {
            var key = string.IsNullOrWhiteSpace(connectionString) ? "LdapTestDb" : connectionString;
            var options = _options.GetOrAdd(key, name => new DbContextOptionsBuilder<KleeneStarDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

            return new KleeneStarDbContext(options);
        }
    }
}
