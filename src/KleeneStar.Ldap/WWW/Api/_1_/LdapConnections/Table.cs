using KleeneStar.Core;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebParameter;
using KleeneStar.Ldap.WebRestApi;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Ldap.WWW.Api._1_.LdapConnections
{
    /// <summary>
    /// The table of the LDAP connections (<c>/api/1/ldapconnections/table</c>): name, key,
    /// protocol, server, how the sign-in finds an account, the synchronization schedule and
    /// the outcome of the last run. Rows only for an account administrator.
    /// </summary>
    [Title("kleenestar.ldap:connection.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<LdapConnectionItem>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
        }

        /// <summary>
        /// Gets the query mode: the rows are filtered in memory, not by a database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Exact;

        /// <summary>
        /// Returns the columns.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The columns.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return Column(request, "name", true);
            yield return Column(request, "key", true);
            yield return Column(request, "protocol", true);
            yield return Column(request, "server", true);
            yield return Column(request, "signin", true);
            yield return Column(request, "sync", true);
            yield return Column(request, "lastsync", true);
            yield return Column(request, "state", true);
        }

        /// <summary>
        /// Returns how many connections match.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The number of connections.</returns>
        protected override int RetrieveTotal(IQuery<LdapConnectionItem> query, IQueryContext context, IRequest request)
        {
            return LdapAuthorization.MayManage(request) ? query.Apply(Items().AsQueryable()).Count() : 0;
        }

        /// <summary>
        /// Returns the rows, their cells in the order of the columns.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="columns">The columns, in the order they are shown.</param>
        /// <param name="request">The request.</param>
        /// <returns>The rows.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<LdapConnectionItem> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            if (!LdapAuthorization.MayManage(request))
            {
                return [];
            }

            // the client pairs cells with the columns by position
            var order = columns?.Select(c => c.Id).ToList() ?? [];
            var stored = LdapHub.Store.GetConnections().ToDictionary(x => x.ResolveId());

            return query.Apply(Items().AsQueryable()).ToList().Select(item =>
            {
                var settings = stored[item.Id];
                var last = LdapHub.Status.Get(item.Id);

                return new RestApiTableRow
                {
                    Id = item.Id.ToString(),
                    Cells =
                    [
                        .. order.Select(id => new RestApiTableCell
                        {
                            Content = id switch
                            {
                                "name" => settings.Name,
                                "key" => settings.Key,
                                "protocol" => I18N.Translate(request, $"kleenestar.ldap:connection.protocol.{LdapConnectionItem.TokenOf(settings.Protocol)}"),
                                "server" => $"{settings.Server}:{settings.EffectivePort}",
                                "signin" => I18N.Translate(request, string.IsNullOrWhiteSpace(settings.BindDnFormat)
                                    ? "kleenestar.ldap:connection.signin.search"
                                    : "kleenestar.ldap:connection.signin.bind"),
                                "sync" => settings.SyncEnabled
                                    ? string.Format(CultureInfo.InvariantCulture, I18N.Translate(request, "kleenestar.ldap:connection.sync.every"), settings.SyncInterval)
                                    : I18N.Translate(request, "kleenestar.ldap:connection.sync.manual"),
                                "lastsync" => Describe(last, request),
                                "state" => I18N.Translate(request, settings.Enabled
                                    ? "kleenestar.ldap:connection.state.enabled"
                                    : "kleenestar.ldap:connection.state.disabled"),
                                _ => null
                            }
                        })
                    ],
                    Options = GetOptions(item, request).Select(o => o.ToJson())
                };
            });
        }

        /// <summary>
        /// Describes the last synchronization.
        /// </summary>
        /// <param name="last">The last run, or <see langword="null"/>.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        public static string Describe(LdapSyncResult last, IRequest request)
        {
            if (last is null)
            {
                return I18N.Translate(request, "kleenestar.ldap:connection.lastsync.never");
            }

            var when = last.Finished.ToLocalTime().ToString("g", request?.Culture ?? CultureInfo.CurrentCulture);

            return last.Succeeded
                ? $"{when} · " + string.Format(CultureInfo.InvariantCulture, I18N.Translate(request, "kleenestar.ldap:connection.lastsync.summary"), last.Found, last.Created, last.Updated, last.Disabled)
                : $"{when} · " + I18N.Translate(request, "kleenestar.ldap:connection.lastsync.failed");
        }

        /// <summary>
        /// Returns the connections as items.
        /// </summary>
        /// <returns>The items.</returns>
        private static IEnumerable<LdapConnectionItem> Items()
        {
            return LdapHub.Store.GetConnections().Select(LdapConnectionItem.From);
        }

        /// <summary>
        /// Builds a column.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="id">The id.</param>
        /// <param name="visible">Whether it is shown by default.</param>
        /// <returns>The column.</returns>
        private static RestApiTableColumn Column(IRequest request, string id, bool visible)
        {
            return new RestApiTableColumn
            {
                Id = id,
                Label = I18N.Translate(request, $"kleenestar.ldap:connection.column.{id}"),
                Visible = visible
            };
        }

        /// <summary>
        /// Returns the options of a row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="request">The request.</param>
        /// <returns>The options.</returns>
        private static IEnumerable<RestApiOption> GetOptions(LdapConnectionItem row, IRequest request)
        {
            IUri Bound<TPage>() where TPage : WebExpress.WebCore.WebEndpoint.IEndpoint
            {
                return CoreHub.GetUri<TPage>()?
                    .BindParameters(request)
                    .BindParameters(new LdapConnectionIdParameter(row.Id));
            }

            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                PrimaryAction = new ActionModal("modal-form", Bound<Settings.LdapConnection._ldapconnectionid_.Edit>(), TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.ldap:connection.test.label"),
                Icon = new IconPlugCircleCheck(),
                PrimaryAction = new ActionModal("modal-form", Bound<Settings.LdapConnection._ldapconnectionid_.Test>(), TypeModalSize.Default)
            };

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.ldap:connection.synchronize.label"),
                Icon = new IconArrowsRotate(),
                PrimaryAction = new ActionModal("modal-form", Bound<Settings.LdapConnection._ldapconnectionid_.Synchronize>(), TypeModalSize.Default)
            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                PrimaryAction = new ActionModal("modal-form", Bound<Settings.LdapConnection._ldapconnectionid_.Delete>(), TypeModalSize.Small)
            };
        }
    }
}
