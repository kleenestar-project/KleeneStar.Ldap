using KleeneStar.Ldap.WebLdap;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Ldap.WebRestApi
{
    /// <summary>
    /// What the test and synchronize endpoints share: the rest form of their dialog loads with a
    /// GET and submits a POST carrying the connection's id, and the dialog shows the
    /// <c>message</c> of the answer - a success where the form stays open to show it, a failure
    /// as the form's error.
    /// </summary>
    public static class LdapConnectionAction
    {
        /// <summary>
        /// Answers the load of the dialog's form; it has nothing to fill in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public static IResponse Load(IRequest request)
        {
            if (!LdapAuthorization.MayManage(request))
            {
                return new ResponseForbidden();
            }

            return new RestApiCrudResultRetrieve { Data = new Dictionary<string, object>() }.ToResponse();
        }

        /// <summary>
        /// Resolves the connection the request names, as it is in effect.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="refusal">The response when the caller may not act or the connection is unknown.</param>
        /// <returns>The connection, or <see langword="null"/>.</returns>
        public static LdapSourceSettings Resolve(IRequest request, out IResponse refusal)
        {
            refusal = null;

            if (!LdapAuthorization.MayManage(request))
            {
                refusal = new ResponseForbidden();

                return null;
            }

            var value = request?.GetParameter<ParameterId>()?.Value ?? request?.GetParameter("id")?.Value;
            var settings = Guid.TryParse(value, out var id) ? LdapHub.GetEffective(id) : null;

            if (settings is null)
            {
                refusal = new ResponseNotFound();
            }

            return settings;
        }

        /// <summary>
        /// Answers a success the dialog shows.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The response.</returns>
        public static IResponse Succeed(string message)
        {
            return new RestApiCrudResultUpdate { Message = message }.ToResponse();
        }

        /// <summary>
        /// Answers a failure the dialog shows as its error.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The response.</returns>
        public static IResponse Fail(string message)
        {
            return new ResponseBadRequest
            {
                Content = new RestApiValidationResult().Add(message).ToJson()
            }
            .AddHeaderContentType("application/json");
        }
    }
}
