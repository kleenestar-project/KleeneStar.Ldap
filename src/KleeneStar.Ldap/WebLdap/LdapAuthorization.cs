using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Ldap.WebLdap
{
    /// <summary>
    /// Who may manage the LDAP connections: the account administrators, like the rest of the
    /// user management (<see cref="AccountAuthorization"/>). A connection decides who signs in
    /// and which accounts exist, so it is administration of the accounts, and it fails closed.
    /// </summary>
    /// <remarks>
    /// A plugin page is not judged by the core's route convention, so the pages ask in
    /// <c>Process</c> and refuse with WebExpress's <c>ForbiddenException</c> (an access-denied
    /// page, or the sign-in prompt for a caller who is not signed in); the endpoints ask the
    /// same question and answer 403.
    /// </remarks>
    public static class LdapAuthorization
    {
        /// <summary>
        /// Returns whether the caller may manage the connections.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for an account administrator.</returns>
        public static bool MayManage(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request);
        }

        /// <summary>
        /// Refuses a page to a caller who may not manage the connections.
        /// </summary>
        /// <param name="request">The request.</param>
        public static void Demand(IRequest request)
        {
            if (!MayManage(request))
            {
                throw PageAuthorization.Refuse();
            }
        }
    }
}
