using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebRestApi;
using System.Globalization;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Ldap.WWW.Api._1_.LdapConnections
{
    /// <summary>
    /// Synchronizes the accounts of a connection now (<c>POST /api/1/ldapconnections/synchronize?id=</c>),
    /// whether or not its schedule is switched on, and records the outcome like a scheduled run.
    /// </summary>
    [Title("kleenestar.ldap:connection.synchronize.title")]
    [Cache]
    public sealed class Synchronize : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Synchronize()
        {
        }

        /// <summary>
        /// Answers the load of the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            return LdapConnectionAction.Load(request);
        }

        /// <summary>
        /// Runs the synchronization.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The outcome, shown in the dialog.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Run(IRequest request)
        {
            var settings = LdapConnectionAction.Resolve(request, out var refusal);

            if (settings is null)
            {
                return refusal;
            }

            var result = LdapHub.Synchronize(settings);

            if (result is null)
            {
                return LdapConnectionAction.Fail(I18N.Translate(request, "kleenestar.ldap:connection.synchronize.running"));
            }

            // the table shows the outcome of the last run
            _ = DataChangedNotifier.NotifyAsync(LdapConnectionItem.From(settings), DataChangeOperation.Updated, settings.ResolveId().ToString());

            if (!result.Succeeded)
            {
                return LdapConnectionAction.Fail(I18N.Translate(request, "kleenestar.ldap:connection.synchronize.failed") + " " + result.Error);
            }

            return LdapConnectionAction.Succeed(string.Format
            (
                CultureInfo.InvariantCulture,
                I18N.Translate(request, "kleenestar.ldap:connection.synchronize.done"),
                result.Found, result.Created, result.Updated, result.Disabled, result.Enabled, result.Conflicts, result.Skipped
            ));
        }
    }
}
