using System;
using System.Collections.Generic;

namespace KleeneStar.Ldap
{
    /// <summary>
    /// Talks to a directory; replaced in tests.
    /// </summary>
    /// <remarks>
    /// A directory that cannot be reached, refuses the service account or answers with an error
    /// throws <see cref="LdapDirectoryException"/>; a wrong password of a login is no error but
    /// the answer <see langword="false"/> of <see cref="Bind"/>.
    /// </remarks>
    public interface ILdapDirectory
    {
        /// <summary>
        /// Binds as an entry.
        /// </summary>
        /// <param name="distinguishedName">The entry.</param>
        /// <param name="password">The password.</param>
        /// <returns><see langword="true"/> when the directory accepted the credentials.</returns>
        bool Bind(string distinguishedName, string password);

        /// <summary>
        /// Searches as the service account, for the sign-in. At most two entries are answered -
        /// all the caller needs to tell "exactly one" from "none" and "several".
        /// </summary>
        /// <param name="searchBase">Where to start.</param>
        /// <param name="filter">The filter, its values escaped.</param>
        /// <returns>The distinguished names found.</returns>
        IReadOnlyList<string> Search(string searchBase, string filter);

        /// <summary>
        /// Searches as the service account and answers every entry with the requested attributes,
        /// page by page, for the synchronization.
        /// </summary>
        /// <param name="searchBase">Where to start.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="attributes">The attributes to read.</param>
        /// <returns>The entries found.</returns>
        IReadOnlyList<LdapDirectoryEntry> SearchEntries(string searchBase, string filter, IReadOnlyCollection<string> attributes);

        /// <summary>
        /// Checks the connection: connects (with TLS where configured), binds as the service
        /// account where one is named, and reads the search base where one is named.
        /// </summary>
        /// <returns>What was checked.</returns>
        LdapProbeResult Probe();
    }

    /// <summary>
    /// An entry the directory answered, with the attributes that were asked for.
    /// </summary>
    /// <param name="distinguishedName">The entry.</param>
    /// <param name="attributes">The first value of each attribute, keyed without regard to case.</param>
    public sealed class LdapDirectoryEntry(string distinguishedName, IReadOnlyDictionary<string, string> attributes)
    {
        /// <summary>Gets the distinguished name.</summary>
        public string DistinguishedName { get; } = distinguishedName;

        /// <summary>Gets the attributes.</summary>
        public IReadOnlyDictionary<string, string> Attributes { get; } = attributes ?? new Dictionary<string, string>();

        /// <summary>
        /// Returns the value of an attribute.
        /// </summary>
        /// <param name="name">The attribute. May be blank.</param>
        /// <returns>The trimmed value, or <see langword="null"/>.</returns>
        public string Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Attributes.TryGetValue(name.Trim(), out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
        }
    }

    /// <summary>
    /// What a connection check found.
    /// </summary>
    /// <param name="Secured">Whether the connection was encrypted.</param>
    /// <param name="BoundAsService">Whether the service account was bound.</param>
    /// <param name="SearchBaseFound">Whether the search base was read; <see langword="null"/> when none is configured.</param>
    public sealed record LdapProbeResult(bool Secured, bool BoundAsService, bool? SearchBaseFound);

    /// <summary>
    /// A directory could not answer: unreachable, refused the service account, failed the TLS
    /// handshake or reported an error.
    /// </summary>
    public sealed class LdapDirectoryException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="message">What went wrong, readable for an administrator.</param>
        /// <param name="inner">The cause.</param>
        public LdapDirectoryException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
