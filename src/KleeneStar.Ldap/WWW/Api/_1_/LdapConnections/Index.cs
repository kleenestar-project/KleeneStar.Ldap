using KleeneStar.Core;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Ldap.WWW.Api._1_.LdapConnections
{
    /// <summary>
    /// The CRUD endpoint of the LDAP connections (<c>/api/1/ldapconnections</c>), over the
    /// plugin's settings file rather than the database.
    /// </summary>
    /// <remarks>
    /// Every verb - reads included, a connection names servers and accounts - is for the
    /// account administrators only (<see cref="LdapAuthorization"/>). A write is validated
    /// against what the sign-in demands of a connection (<see cref="LdapAuthenticationSource.Check"/>),
    /// so the page never stores one the plugin would skip at start. The key of a connection
    /// that accounts already carry cannot be changed: their sign-in would stop finding it.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<LdapConnectionItem>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers a read, for an account administrator.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return LdapAuthorization.MayManage(request) ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Adds a connection, for an account administrator.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return LdapAuthorization.MayManage(request) ? base.Create(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Changes a connection, for an account administrator.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return LdapAuthorization.MayManage(request) ? base.Update(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Removes a connection, for an account administrator.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return LdapAuthorization.MayManage(request) ? base.Delete(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Returns the connections of the settings file that match the query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context (unused: the file is no database).</param>
        /// <param name="request">The request.</param>
        /// <returns>The connections.</returns>
        protected override IEnumerable<LdapConnectionItem> Retrieve(IQuery<LdapConnectionItem> query, IQueryContext context, IRequest request)
        {
            return query.Apply(LdapHub.Store.GetConnections().Select(LdapConnectionItem.From).AsQueryable()).ToList();
        }

        /// <summary>
        /// Answers the defaults of a new connection: LDAPS, the Active Directory search filter
        /// and attributes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The defaults.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            var defaults = LdapConnectionItem.From(new LdapSourceSettings { Key = string.Empty, Name = string.Empty });

            return new RestApiCrudResultRetrieve
            {
                Data = new Dictionary<string, object>
                {
                    [nameof(LdapConnectionItem.Enabled)] = defaults.Enabled,
                    [nameof(LdapConnectionItem.Protocol)] = defaults.Protocol,
                    [nameof(LdapConnectionItem.Timeout)] = defaults.Timeout,
                    [nameof(LdapConnectionItem.SearchFilter)] = defaults.SearchFilter,
                    [nameof(LdapConnectionItem.Order)] = defaults.Order,
                    [nameof(LdapConnectionItem.SyncEnabled)] = defaults.SyncEnabled,
                    [nameof(LdapConnectionItem.SyncInterval)] = defaults.SyncInterval,
                    [nameof(LdapConnectionItem.UserFilter)] = defaults.UserFilter,
                    [nameof(LdapConnectionItem.DisableMissing)] = defaults.DisableMissing,
                    [nameof(LdapConnectionItem.LoginAttribute)] = defaults.LoginAttribute,
                    [nameof(LdapConnectionItem.NameAttribute)] = defaults.NameAttribute,
                    [nameof(LdapConnectionItem.EmailAttribute)] = defaults.EmailAttribute,
                    [nameof(LdapConnectionItem.DepartmentAttribute)] = defaults.DepartmentAttribute,
                    [nameof(LdapConnectionItem.PositionAttribute)] = defaults.PositionAttribute,
                    [nameof(LdapConnectionItem.PhoneAttribute)] = defaults.PhoneAttribute
                }
            };
        }

        /// <summary>
        /// Checks a submitted connection.
        /// </summary>
        /// <param name="existingItem">The connection being edited, or <see langword="null"/> for a new one.</param>
        /// <param name="payload">The submitted form.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result of the check.</returns>
        protected override IRestApiValidationResult Validate(LdapConnectionItem existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = new RestApiValidationResult();
            var stored = existingItem is null ? null : LdapHub.Store.GetConnection(existingItem.Id);
            var settings = Merge(stored, payload, out var invalid);

            foreach (var field in invalid)
            {
                result.Add(I18N.Translate(request, $"kleenestar.ldap:connection.{field.ToLowerInvariant()}.invalid"), field);
            }

            if (string.IsNullOrWhiteSpace(settings.Name))
            {
                result.Add(I18N.Translate(request, "kleenestar.ldap:connection.name.required"), nameof(LdapConnectionItem.Name));
            }

            var key = IdentitySource.Normalize(settings.Key);

            if (key is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.ldap:connection.key.required"), nameof(LdapConnectionItem.Key));
            }
            else
            {
                var id = stored?.ResolveId();
                var twin = LdapHub.Store.GetConnections().Any(x => x.ResolveId() != id && IdentitySource.AreEqual(x.Key, key));
                var other = CoreHub.AuthenticationSourceManager.GetSource(key);

                if (twin || (other is not null and not LdapAuthenticationSource))
                {
                    result.Add(I18N.Translate(request, "kleenestar.ldap:connection.key.taken"), nameof(LdapConnectionItem.Key));
                }

                if (stored is not null && !IdentitySource.AreEqual(stored.Key, key) && CarriedByAccounts(stored.Key))
                {
                    result.Add(I18N.Translate(request, "kleenestar.ldap:connection.key.inuse"), nameof(LdapConnectionItem.Key));
                }
            }

            if (string.IsNullOrWhiteSpace(settings.Server))
            {
                result.Add(I18N.Translate(request, "kleenestar.ldap:connection.server.required"), nameof(LdapConnectionItem.Server));
            }

            if (!result.IsValid)
            {
                return result;
            }

            if (LdapAuthenticationSource.Check(settings) is not null)
            {
                result.Add(I18N.Translate(request, "kleenestar.ldap:connection.signin.incomplete"), nameof(LdapConnectionItem.BindDnFormat));
            }

            if (settings.SyncEnabled && LdapUserSynchronizer.Check(settings) is not null)
            {
                result.Add(I18N.Translate(request, "kleenestar.ldap:connection.sync.incomplete"), nameof(LdapConnectionItem.SearchBase));
            }

            return result;
        }

        /// <summary>
        /// Adds a connection to the settings file.
        /// </summary>
        /// <param name="payload">The submitted form.</param>
        /// <param name="request">The request.</param>
        /// <param name="newItem">The added connection.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData payload, IRequest request, out LdapConnectionItem newItem)
        {
            var settings = Merge(null, payload, out _);

            settings.Id = null;
            settings.Id = Save(() => LdapHub.Store.Add(settings)).ToString();
            newItem = LdapConnectionItem.From(settings);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Changes a connection in the settings file.
        /// </summary>
        /// <param name="existingItem">The connection.</param>
        /// <param name="payload">The submitted form.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultUpdate Update(LdapConnectionItem existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var stored = LdapHub.Store.GetConnection(existingItem.Id)
                ?? throw new InvalidOperationException("The connection no longer exists.");
            var settings = Merge(stored, payload, out _);

            settings.Id = existingItem.Id.ToString();
            Save(() => LdapHub.Store.Update(settings));

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Removes a connection from the settings file, and its synchronization record. The
        /// accounts of the connection stay; they no longer sign in.
        /// </summary>
        /// <param name="existingItem">The connection.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultDelete Delete(LdapConnectionItem existingItem, IRequest request)
        {
            Save(() => LdapHub.Store.Remove(existingItem.Id));

            try
            {
                LdapHub.Status.Remove(existingItem.Id);
            }
            catch (IOException)
            {
                // a stale record names a connection nobody can address any more
            }

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// Applies the submitted form onto a copy of the stored connection, or onto the defaults.
        /// </summary>
        /// <param name="stored">The stored connection, or <see langword="null"/>.</param>
        /// <param name="payload">The submitted form.</param>
        /// <param name="invalid">Receives the fields whose number did not parse.</param>
        /// <returns>The resulting settings.</returns>
        private static LdapSourceSettings Merge(LdapSourceSettings stored, RestApiCrudFormData payload, out List<string> invalid)
        {
            var settings = stored?.Clone() ?? new LdapSourceSettings { Key = null, Name = null };

            invalid = [];
            LdapConnectionItem.Apply(payload, settings, invalid);

            return settings;
        }

        /// <summary>
        /// Returns whether accounts carry a source key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when at least one account does.</returns>
        private static bool CarriedByAccounts(string key)
        {
            var normalized = IdentitySource.Normalize(key);

            return normalized is not null && CoreHub.IdentityManager
                .CountIdentities(new Query<Identity>().Where(x => x.AuthenticationSource == normalized)) > 0;
        }

        /// <summary>
        /// Runs a write of the settings file, turning a file the server cannot write into a
        /// readable failure.
        /// </summary>
        /// <typeparam name="T">The type of the answer.</typeparam>
        /// <param name="write">The write.</param>
        /// <returns>Its answer.</returns>
        private static T Save<T>(Func<T> write)
        {
            try
            {
                return write();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                throw new InvalidOperationException($"The settings file '{LdapHub.Store.Path}' could not be written: {ex.Message}", ex);
            }
        }
    }
}
