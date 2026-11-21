![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar LDAP

Signs accounts in against an LDAP directory or Active Directory, and synchronizes the accounts
with it. The plugin adds one authentication source per configured connection to the core's
catalog (`AuthenticationSourceManager`); the core's password sign-in then checks the password by
binding as the account's directory entry. Nothing about the password is kept.

## Platform independence

The directory is spoken to through **Novell.Directory.Ldap.NETStandard** (MIT), a fully managed
LDAP v3 client. It replaced System.DirectoryServices.Protocols, which on Linux and macOS needs the
native OpenLDAP library (`libldap`) - absent from the ASP.NET container image. The plugin now runs
unchanged on Windows, Linux, macOS and in the container. Three transports:

|Protocol           |Port|What happens
|-------------------|----|-------------------------------------------------------------
|LDAPS              |636 |TLS from the first byte (`UseSsl`)
|LDAP with StartTLS |389 |a plain connection upgraded by the StartTLS operation (`StartTls`)
|LDAP               |389 |unencrypted - the password crosses the network in the clear

The server certificate is validated by the platform's trust store; `AllowUntrustedCertificate`
skips that for a test directory with a self-signed certificate. Every operation opens its own
connection; referrals are not chased (an Active Directory search at the domain root answers
continuation references to its application partitions, which are skipped).

## Settings page

*Settings → User management → LDAP connections* (`WWW/Settings/LdapConnections`, routed by
WebExpress under the plugin's prefix: `/kleenestar/kleenestar.ldap/settings/ldapconnections`).
A table of the connections (name, key, protocol, server, sign-in mode, schedule, last
synchronization, state) with *Add connection*, and per row *Edit*, *Test connection*,
*Synchronize now* and *Delete*. The add/edit dialog has three tabs: connection, directory and
sign-in, synchronization.

**Only account administrators** (members of the `Admin` group, `AccountAuthorization`) see and use
it - a connection decides who signs in and which accounts exist. The pages refuse others with
WebExpress's `ForbiddenException` (access denied, or the sign-in prompt), the endpoints with 403
(`WebLdap/LdapAuthorization`).

**Storage: the plugin's own settings file** `settings/kleenestar.ldap.settings.json`, section
`Plugins:kleenestar.ldap:Directories` (`WebLdap/LdapConnectionStore`). The page lists what that
file holds and writes changes back into it; other sections of the file are kept, comments are
not (it is rewritten as plain json). A save replaces the file in one move and reloads the
configuration, and the plugin follows every reload of the configuration - the page's and an
operator's edit alike - by re-registering its sources
(`LdapAuthenticationSource.RegisterConfigured`: new and changed connections registered, removed
or disabled ones unregistered). No restart.

The **service password** is stored in the file like the other settings and never leaves the
server again: the edit dialog shows an empty field, and an empty field keeps the stored password
(`LdapConnectionItem` is the form's own type for that reason - the CRUD base serializes every
property it is handed).

**The table refreshes over the change channel.** `LdapConnectionItem` implements `IDomain`: the
table declares its endpoint's item type as its domain only for a domain, and the CRUD endpoint's
`DataChangedNotifier` announces creates, updates and deletes only of one. *Synchronize now*
announces an update too, and so does every reload of the configuration - a hand edit of the file
refreshes an open table as well. To keep it out of the file, set
`WEBEXPRESS_Plugins__kleenestar.ldap__Secrets__{key}__ServicePassword` - the section
`Plugins:kleenestar.ldap:Secrets:{key}:ServicePassword`, bound to the connection's **key**, which
cannot change while accounts carry it; it wins over the password in the file
(`LdapAuthenticationSource.ReadConfigured`). Never override by position
(`…__Directories__{index}__…`): deleting an earlier connection moves the list up, and the secret
would reach the next connection's server.

**Validation** (`/api/1/ldapconnections`): name, key (not `local`, unique, not held by another kind
of source such as an OpenID Connect provider), server, numbers in range, and what the sign-in needs
(`LdapAuthenticationSource.Check`) - and, when the schedule is on, what the synchronization needs.
The key cannot be changed while accounts carry it: their sign-in would stop finding the source.
Deleting a connection keeps its accounts; they no longer sign in.

## Configuration

One entry per connection; the page writes every field, a hand-written entry may leave out what it
does not need (an entry without `Id` is addressed by an id derived from its key).

|Setting        |Default                  |Meaning
|---------------|-------------------------|-----------------------------------------------------------
|`Id`           |derived from the key     |what the settings page addresses the entry by
|`Key`          |`ldap`                   |what the accounts store as their sign-in (`AuthenticationSource`); not `local`
|`Name`         |`LDAP`                   |the name shown for the source
|`Enabled`      |`true`                   |an inactive connection signs nobody in and is not synchronized
|`Server`       |-                        |the directory host
|`Port`         |636 with SSL, 389 without|
|`UseSsl`       |`true`                   |LDAPS
|`StartTls`     |`false`                  |upgrade a plain connection with StartTLS
|`AllowUntrustedCertificate`|`false`      |accept a certificate that does not validate (test directories only)
|`Timeout`      |10                       |seconds for a connection and a sign-in check
|`BindDnFormat` |-                        |direct bind: `{0}@corp.example.com` (Active Directory), `uid={0},ou=people,dc=example,dc=com` (OpenLDAP)
|`ServiceDn`, `ServicePassword`, `SearchBase`, `SearchFilter`|filter `(sAMAccountName={0})`|search then bind, instead of `BindDnFormat`; the service account and search base also serve the synchronization
|`Order`        |50                       |the order among the sources
|`SyncEnabled`  |`false`                  |synchronize on a schedule
|`SyncInterval` |60                       |minutes between two runs (at least 5)
|`UserFilter`   |`(&(objectCategory=person)(objectClass=user))`|which entries are accounts (OpenLDAP: `(objectClass=inetOrgPerson)`)
|`LoginAttribute`|`sAMAccountName`        |the login (OpenLDAP: `uid`)
|`NameAttribute`, `EmailAttribute`, `DepartmentAttribute`, `PositionAttribute`, `PhoneAttribute`|`displayName`, `mail`, `department`, `title`, `telephoneNumber`|copied onto the account; empty leaves that property alone
|`DisableMissing`|`true`                  |disable accounts the directory no longer returns

A connection without a server, or without a bind format and without a service account and search
base, is logged and skipped at start.

## Sign-in

- The accounts exist before their first sign-in - created by an administrator or by the
  synchronization - with the connection's key as `AuthenticationSource` and the directory login as
  `ExternalSubject`. The source does not provision.
- An empty password is refused before the directory is asked (many directories treat it as an
  anonymous bind and answer success).
- The login is escaped for distinguished names (RFC 4514) and search filters (RFC 4515); a search
  that finds anything but exactly one entry signs nobody in.
- An unreachable directory signs nobody in and is logged.

## Synchronization

`WebLdap/LdapUserSynchronizer`, run by *Synchronize now* (`/api/1/ldapconnections/synchronize`) or
by the scheduler (`WebLdap/LdapSyncScheduler`, a look once a minute for enabled connections whose
last run is older than their interval). It reads the users below the search base with the user
filter, as the service account (anonymously without one), page by page (paged results, 500 per
page; a directory without paging answers in one go).

- **Matching**: an account of the connection's source by its `ExternalSubject` (the login); the
  first time, an account an administrator prepared for the source without a subject is claimed by
  its user name. An account of **another** source carrying the login as its user name is never
  taken over - the entry counts as a conflict.
- **New users** get an account: user name = the login (qualified with the key if taken), name,
  e-mail, department, position, phone from the mapped attributes, state active. A user that
  Active Directory marks disabled (`userAccountControl` bit 2) and that has no account yet is
  skipped.
- **Existing accounts** get changed attributes copied; a value the directory leaves empty is kept.
- **State**: the sync moves an account between *Active* and *Disabled* only - disabled in the
  directory or missing from it (with `DisableMissing`) disables, back in the directory enables
  again. *Locked* is the administrators' state and stays. A directory answering **no** user
  disables nobody (a wrong filter or base, far more likely than an empty company), and a run
  whose entries **all lack the login attribute** (a misspelt attribute, one the service account
  may not read) fails before it writes anything - it would match no account and disable every
  one of them. Nothing is deleted.
- The accounts are written through the identity manager (audited); per-account toasts are
  silenced. The subject is written through `ICredentialManager.LinkExternalSubject` (core), the
  credential path for external subjects.
- The outcome of the last run per connection is kept in `{DataPath}/ldap/synchronization.json`
  (state, not configuration, so not in the settings file); the scheduler decides by it and every
  run holds the cluster lock `kleenestar.ldap.sync.{id}`, so replicas sharing the data directory
  and cluster state run a connection once per interval between them.

## Structure

|What                          |Where
|------------------------------|--------------------------------------------------------------
|Plugin entry, registration, settings watch, scheduler start|`KleeneStarPlugin`
|The source and its settings   |`LdapAuthenticationSource`, `LdapSourceSettings`
|Directory access (test seam)  |`ILdapDirectory`, `LdapDirectory` (Novell.Directory.Ldap)
|Settings file, sync record, shared entry|`WebLdap/LdapConnectionStore`, `LdapSyncStatusStore`, `LdapHub`
|Synchronization               |`WebLdap/LdapUserSynchronizer`, `LdapSyncScheduler`, `LdapSyncResult`
|Pages                         |`WWW/Settings/LdapConnections/{Index,Add}`, `WWW/Settings/LdapConnection/_ldapconnectionid_/{Edit,Delete,Test,Synchronize}`
|Endpoints                     |`WWW/Api/_1_/LdapConnections/{Index,Table,Protocol,Test,Synchronize}`
|Fragments                     |`WebFragment/LdapConnection*`
|Tests                         |`KleeneStar.Ldap.Test` (a fake directory and an in-memory database; no directory server is needed)


