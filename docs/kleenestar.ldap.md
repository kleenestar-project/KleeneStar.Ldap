![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar LDAP

Signs accounts in against an LDAP directory or Active Directory. The plugin adds one
authentication source per configured directory to the core's catalog
(`AuthenticationSourceCatalog`); the core's password sign-in then checks the password by binding
as the account's directory entry. Nothing about the password is kept.

## Configuration

`Plugins:kleenestar.ldap:Directories` in the host's settings, one entry per directory:

|Setting        |Default                  |Meaning
|---------------|-------------------------|-----------------------------------------------------------
|`Key`          |`ldap`                   |what the accounts store as their sign-in (`AuthenticationSource`); not `local`
|`Name`         |`LDAP`                   |the name shown for the source
|`Server`       |-                        |the directory host
|`Port`         |636 with SSL, 389 without|
|`UseSsl`       |`true`                   |LDAPS - a password must not cross the network in the clear
|`BindDnFormat` |-                        |direct bind: `{0}@corp.example.com` (Active Directory), `uid={0},ou=people,dc=example,dc=com` (OpenLDAP)
|`ServiceDn`, `ServicePassword`, `SearchBase`, `SearchFilter`|filter `(sAMAccountName={0})`|search then bind, instead of `BindDnFormat`
|`Order`        |50                       |the order among the sources

A directory without a server, or without a bind format and without a service account and search
base, is logged and skipped at start.

## Behaviour

- The accounts exist before their first sign-in - created by an administrator or by the LDAP
  import - with the directory's key as `AuthenticationSource` and, where the directory login
  differs from the user name, the login as `ExternalSubject`. The source does not provision.
- An empty password is refused before the directory is asked (many directories treat it as an
  anonymous bind and answer success).
- The login is escaped for distinguished names (RFC 4514) and search filters (RFC 4515); a search
  that finds anything but exactly one entry signs nobody in.
- An unreachable directory signs nobody in and is logged.

## Structure

|What                          |Where
|------------------------------|--------------------------------------------------------------
|Plugin entry, registration    |`KleeneStarPlugin` (`Run` reads the plugin's own settings)
|The source and its settings   |`LdapAuthenticationSource`, `LdapSourceSettings`
|Directory access (test seam)  |`ILdapDirectory`, implemented over System.DirectoryServices.Protocols
|Tests                         |`KleeneStar.Ldap.Test` (a fake directory; no directory server is needed)

The tests check binding, escaping, search then bind, registration from settings and the core's
resolution of the source. A real directory server was not part of the verification.
