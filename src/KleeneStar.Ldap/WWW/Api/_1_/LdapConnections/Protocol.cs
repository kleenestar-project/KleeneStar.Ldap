using KleeneStar.Ldap.WebRestApi;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Ldap.WWW.Api._1_.LdapConnections
{
    /// <summary>
    /// The transports a connection can use, for the protocol selection of the dialogs
    /// (<c>/api/1/ldapconnections/protocol</c>).
    /// </summary>
    [Title("LDAP protocol")]
    public sealed class Protocol : RestApiSelection<LdapConnectionItem>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Protocol()
        {
        }

        /// <summary>
        /// Returns the transports, the encrypted ones first.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The entries.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<LdapConnectionItem> query, IQueryContext context, IRequest request)
        {
            var search = request?.GetParameter("q")?.Value;

            return new[] { LdapProtocol.Ldaps, LdapProtocol.StartTls, LdapProtocol.Ldap }
                .Select(x => new RestApiSelectionItem
                {
                    Id = LdapConnectionItem.IdOf(x),
                    Text = I18N.Translate(request, $"kleenestar.ldap:connection.protocol.{LdapConnectionItem.TokenOf(x)}")
                })
                .Where(x => string.IsNullOrWhiteSpace(search) || search == "null" || x.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsQueryable();
        }

        /// <summary>
        /// Leaves the query alone: the list is fixed and narrowed above.
        /// </summary>
        /// <param name="search">The search text.</param>
        /// <param name="query">The query.</param>
        /// <param name="request">The request.</param>
        /// <returns>The query.</returns>
        protected override IQuery<LdapConnectionItem> Filter(string search, IQuery<LdapConnectionItem> query, IRequest request)
        {
            return query;
        }
    }
}
