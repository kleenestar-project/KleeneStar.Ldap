using System;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Ldap.WebParameter
{
    /// <summary>
    /// The id of an LDAP connection in a route (<c>/settings/ldapconnection/{ldapconnectionid}/…</c>).
    /// </summary>
    public sealed class LdapConnectionIdParameter : IParameterStatic
    {
        /// <summary>
        /// Gets the key of the parameter.
        /// </summary>
        public static string Key => "ldapconnectionid";

        /// <summary>
        /// Gets or sets the scope of the parameter.
        /// </summary>
        public ParameterScope Scope { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public LdapConnectionIdParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="value">The id.</param>
        public LdapConnectionIdParameter(Guid value)
        {
            Value = value.ToString();
        }

        /// <summary>
        /// Returns the key of the parameter.
        /// </summary>
        /// <returns>The key.</returns>
        public string GetKey()
        {
            return Key;
        }
    }
}
