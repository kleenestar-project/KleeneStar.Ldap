![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar.Ldap - Sign-in with Your Directory

**KleeneStar.Ldap** connects the **KleeneStar** platform to an LDAP directory or to Microsoft Active Directory. People sign in with the account and the password they already use at work; the password is checked by the directory and never stored by **KleeneStar**.

Built on the **WebExpress-Framework** and **KleeneStar.Core**, it is a plugin of its own: an installation without a directory carries neither its code nor its protocol dependency.

## What It Powers

**KleeneStar.Ldap** adds one authentication source per configured directory to the core's open catalog of sign-in sources. The core's regular sign-in form then checks the password of every account that belongs to such a source by binding to the directory as that account's entry.

- **LDAP and Active Directory** - OpenLDAP, 389 Directory Server, Active Directory and any other LDAPv3 directory.
- **Two ways to find an entry** - a direct bind with a name pattern, or a search by a service account followed by a bind as the entry found.
- **Several directories at once** - each with its own key, name and settings.
- **Encrypted by default** - LDAPS on port 636 unless a directory is explicitly configured otherwise.
- **No password storage** - nothing about the password is kept, cached or logged.

## How It Works

1. When the host starts, the plugin reads its settings and registers one source per directory with the core (`AuthenticationSourceCatalog`). A directory whose settings are incomplete is logged and skipped; the others are registered.
2. An account belongs to a directory when its sign-in source (`AuthenticationSource`) is the directory's key. Accounts are created beforehand - by an administrator or by the LDAP import of the core - because the plugin does not create accounts on first sign-in.
3. At sign-in, the core finds the account by user name or e-mail address and asks its source to check the password. The plugin binds as the account's entry; the directory's answer is the answer.

The directory login is the account's external subject (`ExternalSubject`) where one is set, and its user name otherwise - so a directory login like `a.smith` can stand behind a KleeneStar user name like `alice`.

## Configuration

The directories are configured in the host's settings under the plugin's own id, `Plugins:kleenestar.ldap:Directories`, one entry per directory - for example in `settings/webexpress.settings.json` or in a file of its own in the `settings` directory:

```json
{
  "Plugins": {
    "kleenestar.ldap": {
      "Directories": [
        {
          "Key": "ad",
          "Name": "Corporate directory",
          "Server": "dc01.corp.example.com",
          "UseSsl": true,
          "BindDnFormat": "{0}@corp.example.com"
        },
        {
          "Key": "openldap",
          "Name": "Research directory",
          "Server": "ldap.research.example.com",
          "ServiceDn": "cn=kleenestar,ou=services,dc=research,dc=example,dc=com",
          "ServicePassword": "<set through the environment>",
          "SearchBase": "ou=people,dc=research,dc=example,dc=com",
          "SearchFilter": "(uid={0})"
        }
      ]
    }
  }
}
```

| Setting           | Default                       | Meaning
|-------------------|-------------------------------|-----------------------------------------------------------------
| `Key`             | `ldap`                        | What the accounts store as their sign-in source. Must not be `local`.
| `Name`            | `LDAP`                        | The name shown for the source in the account administration.
| `Server`          | -                             | The directory host. Required.
| `Port`            | `636` with SSL, `389` without | The directory port.
| `UseSsl`          | `true`                        | Connect with LDAPS. A password must not cross the network in the clear.
| `BindDnFormat`    | -                             | **Direct bind:** the entry an account binds as, `{0}` standing for the login.
| `ServiceDn`       | -                             | **Search then bind:** the service account that searches.
| `ServicePassword` | -                             | The service account's password.
| `SearchBase`      | -                             | Where the search starts.
| `SearchFilter`    | `(sAMAccountName={0})`        | The filter, `{0}` standing for the login.
| `Order`           | `50`                          | The order among the sign-in sources.

A directory needs either `BindDnFormat`, or `ServiceDn` together with `SearchBase`.

**Typical bind patterns**

| Directory                          | `BindDnFormat`
|------------------------------------|--------------------------------------------------
| Active Directory (UPN)             | `{0}@corp.example.com`
| OpenLDAP / 389 Directory Server    | `uid={0},ou=people,dc=example,dc=com`

Where the people of a directory live in several branches, use search then bind instead of a pattern.

**Secrets** stay out of the repository. Every value can be overridden through the environment, for example:

```
WEBEXPRESS_Plugins__kleenestar.ldap__Directories__1__ServicePassword=...
```

## Preparing the Accounts

Create the accounts that sign in through a directory with the directory's key as their sign-in source - in the account administration, through `/api/1/identities`, or with the LDAP import of the core. Where the directory login differs from the user name, store the directory login as the account's external subject.

An account of a directory has no password in **KleeneStar**: it is not set, changed or reset here, because the directory owns it.

## Security

- **Empty passwords are refused** before the directory is asked. Many directories treat a bind without a password as an anonymous bind and answer success.
- **The login is escaped** for distinguished names (RFC 4514) and search filters (RFC 4515), so a crafted name can neither name another entry nor widen a search.
- **Exactly one entry** - a search that finds no entry or more than one signs nobody in.
- **Failures say nothing** - an unreachable directory signs nobody in; the cause is logged for the administrator, never shown to the person signing in.

## Requirements

- A **KleeneStar** host with **KleeneStar.Core**.
- Network access from the host to the directory on the configured port.
- For LDAPS, a server certificate the host trusts.
- On Linux, the native OpenLDAP client library (`libldap`), which `System.DirectoryServices.Protocols` uses there. Windows needs nothing further.

## For Developers

The repository contains the plugin and its tests:

| Path                                              | Content
|---------------------------------------------------|--------------------------------------------------------------
| `src/KleeneStar.Ldap/KleeneStarPlugin.cs`         | Plugin entry; registers the configured directories on start
| `src/KleeneStar.Ldap/LdapAuthenticationSource.cs` | The source, its settings and the directory access
| `src/KleeneStar.Ldap.Test`                        | Tests against a fake directory - no directory server needed
| `docs/kleenestar.ldap.md`                         | Concept and behaviour in detail

`ILdapDirectory` is the seam between the source and the directory: the shipped implementation speaks LDAP through `System.DirectoryServices.Protocols`, the tests replace it with a fake that records what it was asked.

**Building**

```
dotnet build KleeneStar.Ldap/src/KleeneStar.Ldap/KleeneStar.Ldap.csproj
```

The host references the project, so the plugin is loaded with it; nothing has to be deployed separately in a development checkout.

**Testing**

```
dotnet build KleeneStar.Ldap/src/KleeneStar.Ldap.Test
KleeneStar.Ldap/src/KleeneStar.Ldap.Test/bin/Debug/net10.0/KleeneStar.Ldap.Test.exe
```

The tests cover direct bind, search then bind, escaping, empty passwords, the registration from settings and the core's resolution of the source. A real directory server is not part of the automated tests.

## Legal & Licensing

**KleeneStar.Ldap** is released under the `MIT License`, a permissive open-source license that allows reuse, modification, and distribution with minimal restrictions. You're free to use **KleeneStar** in personal, academic, or commercial projects, just include the original copyright notice.

The system is designed to be GDPR-compliant:
- No tracking
- No monetization
- No hidden dependencies
- No storage of directory passwords
- Full transparency and infrastructure control

**KleeneStar** respects your data and your autonomy. It's built for clarity, not surveillance.

## Contributing

We welcome contributions in many areas:
- Directory support and interoperability (C#)
- Tests against further directory products
- Documentation and onboarding flows

Feel free to fork the repository, open issues, or submit pull requests. For larger contributions, please reach out via kleenestar.project@gmail.com.

# AI transparency notice

Parts of this software, its documentation, and its assets were created with the assistance of AI-based tools, including large language models. AI-assisted contributions are reviewed by the project maintainer before they are included.

---

Become part of the **KleeneStar** community and contribute to a modular, open-source future.
