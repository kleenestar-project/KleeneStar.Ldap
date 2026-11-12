using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Ldap.WWW.Api._1_.LdapConnections
{
    /// <summary>
    /// Checks a stored connection (<c>POST /api/1/ldapconnections/test?id=</c>): connects with
    /// the configured transport, binds the service account where one is named and reads the
    /// search base where one is named. Nothing is changed.
    /// </summary>
    [Title("kleenestar.ldap:connection.test.title")]
    [Cache]
    public sealed class Test : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Test()
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
        /// Checks the connection.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The outcome, shown in the dialog.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Probe(IRequest request)
        {
            var settings = LdapConnectionAction.Resolve(request, out var refusal);

            if (settings is null)
            {
                return refusal;
            }

            try
            {
                var probe = LdapUserSynchronizer.DirectoryFactory(settings).Probe();
                var lines = new List<string>
                {
                    string.Format(I18N.Translate(request, probe.Secured
                        ? "kleenestar.ldap:connection.test.connected.secured"
                        : "kleenestar.ldap:connection.test.connected.plain"), $"{settings.Server}:{settings.EffectivePort}"),
                    I18N.Translate(request, probe.BoundAsService
                        ? "kleenestar.ldap:connection.test.bound"
                        : "kleenestar.ldap:connection.test.anonymous")
                };

                if (probe.SearchBaseFound is { } found)
                {
                    lines.Add(I18N.Translate(request, found
                        ? "kleenestar.ldap:connection.test.base.found"
                        : "kleenestar.ldap:connection.test.base.missing"));
                }

                return LdapConnectionAction.Succeed(string.Join(" ", lines));
            }
            catch (LdapDirectoryException ex)
            {
                return LdapConnectionAction.Fail(I18N.Translate(request, "kleenestar.ldap:connection.test.failed") + " " + ex.Message);
            }
        }
    }
}
