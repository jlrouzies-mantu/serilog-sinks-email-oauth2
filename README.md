# Serilog.Sinks.Email&nbsp;[![Build status](https://github.com/serilog/serilog-sinks-email/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/serilog/serilog-sinks-email/actions)&nbsp;[![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.Email.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.Email/)

Sends log events by SMTP email.

> ℹ️ Version 3.x of this package changes the name and structure of many configuration parameters from their 2.x names; see below for detailed information.

> ✅ Now includes optional OAuth2 (Modern Authentication) support for SMTP using Azure AD / Office 365 or any OAuth2-compliant provider. See the new OAuth2 section below.

**Package Id:** [Serilog.Sinks.EmailOauth2](http://nuget.org/packages/serilog.sinks.emailoauth2)

```csharp
await using var log = new LoggerConfiguration()
    .WriteTo.Email(
        from: "app@example.com",
        to: "support@example.com",
        host: "smtp.example.com")
    .CreateLogger();
```

## Basic (single-message) options

Supported options are:

| Parameter                                      | Description                                                                                                                                                                                                                  |
|------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `from`                                         | The email address emails will be sent from.                                                                                                                                                                                  |
| `to`                                           | The email address emails will be sent to. Multiple addresses can be separated with commas or semicolons.                                                                                                                     |
| `host`                                         | The SMTP server to use.                                                                                                                                                                                                      |
| `port`                                         | The port used for the SMTP connection. The default is 25.                                                                                                                                                                    |
| `connectionSecurity`                           | Choose the security applied to the SMTP connection. This enumeration type is supplied by MailKit. The default is `Auto`.                                                                                                     |
| `credentials`                                  | The network credentials to use to authenticate with the mail server (traditional username/password SMTP AUTH).                                                                                                              |
| `subject`                                      | A message template describing the email subject. The default is `"Log Messages"`.                                                                                                                                            |
| `body`                                         | A message template describing the format of the email body. The default is `"{Timestamp} [{Level}] {Message}{NewLine}{Exception}"`.                                                                                          |
| `formatProvider`                               | Supplies culture-specific formatting information. The default is to use the current culture.                                                                                                                                 |
| `SmtpAuthenticationMode`                       | Specifies which authentication mode to use: e.g. `None`, `Basic`, `OAuth2`. Determines whether classic network credentials or OAuth2 token acquisition flows are used.                                                       |
| `ApplicationId`                                | OAuth2 Client/Application ID (e.g. Azure AD App Registration Client ID). Required for OAuth2 when using client secret or certificate flows.                                                                                  |
| `SecretId`                                     | OAuth2 Client Secret value (NOT the secret’s GUID id). Provide this for client secret flows. Leave null when using certificate-based client assertion.                                                                       |
| `OAuthTokenUrl`                                | The OAuth2 token endpoint URL (e.g. `https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token` for Azure AD).                                                                                                         |
| `OAuthScope`                                   | The space-separated scope(s) requested for the token (e.g. `https://outlook.office365.com/.default` for Office 365 SMTP).                                                                                                   |
| `SecretWindowsStoreCertificateThumbprint`      | Thumbprint of an X.509 certificate in the Windows Certificate Store (CurrentUser / My) used to create a client assertion instead of a client secret.                                                                         |
| `OAuthTokenUsername`                           | The user principal (SMTP mailbox) associated with the OAuth2 token (often the `from` address). Some SMTP servers require the SMTP AUTH identity even with OAuth2.                                                           |

An overload accepting `EmailSinkOptions` can be used to specify advanced options such as batched and/or HTML body templates, along with the OAuth2-related properties above.

## Sending batch email

To send batch email, supply `WriteTo.Email` with a batch size:

```csharp
await using var log = new LoggerConfiguration()
    .WriteTo.Email(
        options: new()
        {
            From = "app@example.com",
            To = "support@example.com",
            Host = "smtp.example.com",
        },
        batchingOptions: new()
        {
            BatchSizeLimit = 10,
            BufferingTimeLimit = TimeSpan.FromSeconds(30),
        })
    .CreateLogger();
```

Batch formatting can be customized using `options.Body`.

## Sending HTML email

To send HTML email, specify a custom `IBatchTextFormatter` in `options.Body` and set `options.IsBodyHtml` to `true`:

```csharp
await using var log = new LoggerConfiguration()
    .WriteTo.Email(
        options: new()
        {
            From = "app@example.com",
            To = "support@example.com",
            Host = "smtp.example.com",
            Body = new MyHtmlBodyFormatter(),
            IsBodyHtml = true,
        },
        batchingOptions: new()
        {
            BatchSizeLimit = 10,
            BufferingTimeLimit = TimeSpan.FromSeconds(30),
        })
    .CreateLogger();
```

A simplistic HTML formatter is shown below:

```csharp
class MyHtmlBodyFormatter : IBatchTextFormatter
{
    public void FormatBatch(IEnumerable<LogEvent> logEvents, TextWriter output)
    {
        output.Write("<table>");
        foreach (var logEvent in logEvents)
        {
            output.Write("<tr>");
            Format(logEvent, output);
            output.Write("</tr>");
        }

        output.Write("</table>");
    }

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter();
        logEvent.RenderMessage(buffer);
        output.Write(WebUtility.HtmlEncode(buffer.ToString()));
    }
}
```

## OAuth2 / Modern Authentication (Optional)

Some SMTP providers (notably Exchange Online / Office 365) are deprecating or disabling basic username/password authentication. The sink supports obtaining an OAuth2 access token and using it for SMTP AUTH (XOAUTH2) when `SmtpAuthenticationMode` is set to `OAuth2`.

### Choosing an authentication mode

```csharp
public enum SmtpAuthenticationMode
{
    None,      // No AUTH attempted
    Basic,     // Username/password via NetworkCredential
    OAuth2     // Acquire token and authenticate using XOAUTH2
}
```

Set `SmtpAuthenticationMode` appropriately in `EmailSinkOptions`. If `Basic` is selected, use `credentials`. If `OAuth2` is selected, configure the OAuth2 properties.

### Example: OAuth2 with Azure AD (Client Secret flow)

```csharp
await using var log = new LoggerConfiguration()
    .WriteTo.Email(
        options: new()
        {
            From = "app@contoso.com",
            To = "support@contoso.com",
            Host = "smtp.office365.com",
            Port = 587,
            ConnectionSecurity = SecureSocketOptions.StartTls,
            SmtpAuthenticationMode = SmtpAuthenticationMode.OAuth2,
            ApplicationId = "<CLIENT_ID>",
            SecretId = "<CLIENT_SECRET_VALUE>",
            OAuthTokenUrl = "https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token",
            OAuthScope = "https://outlook.office365.com/.default",
            OAuthTokenUsername = "app@contoso.com"
        })
    .CreateLogger();
```

Notes:

- `OAuthScope` for Exchange Online SMTP is typically `https://outlook.office365.com/.default` (the `.default` scope requests all application permissions granted).
- Ensure your Azure AD App Registration has the appropriate Application permission (e.g. `SMTP.Send`) and admin consent granted.

### Example: OAuth2 with Azure AD (Certificate / Client Assertion flow)

Instead of a client secret, supply the thumbprint of a certificate installed in the Windows Current User store.

```csharp
await using var log = new LoggerConfiguration()
    .WriteTo.Email(
        options: new()
        {
            From = "app@contoso.com",
            To = "support@contoso.com",
            Host = "smtp.office365.com",
            Port = 587,
            ConnectionSecurity = SecureSocketOptions.StartTls,
            SmtpAuthenticationMode = SmtpAuthenticationMode.OAuth2,
            ApplicationId = "<CLIENT_ID>",
            SecretWindowsStoreCertificateThumbprint = "<CERT_THUMBPRINT>",
            OAuthTokenUrl = "https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token",
            OAuthScope = "https://outlook.office365.com/.default",
            OAuthTokenUsername = "app@contoso.com"
        })
    .CreateLogger();
```

The sink will construct a signed JWT (client assertion) using the certificate to request the access token.

### Example: Configuration via appsettings.json

If you configure Serilog via JSON:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Email" ],
    "WriteTo": [
      {
        "Name": "Email",
        "Args": {
          "options": {
            "From": "app@contoso.com",
            "To": "support@contoso.com",
            "Host": "smtp.office365.com",
            "Port": 587,
            "ConnectionSecurity": "StartTls",
            "SmtpAuthenticationMode": "OAuth2",
            "ApplicationId": "<CLIENT_ID>",
            "SecretId": "<CLIENT_SECRET_VALUE>",
            "OAuthTokenUrl": "https://login.microsoftonline.com/<TENANT_ID>/oauth2/v2.0/token",
            "OAuthScope": "https://outlook.office365.com/.default",
            "OAuthTokenUsername": "app@contoso.com"
          }
        }
      }
    ]
  }
}
```

(Adjust property names if your configuration binder uses slightly different casing conventions.)

### Troubleshooting OAuth2

- Invalid scope: Confirm the `OAuthScope` matches provider requirements.
- 400 / invalid_client: Check `ApplicationId`, secret value (not secret ID), or certificate thumbprint.
- 535 / Authentication failed: Ensure the SMTP mailbox (`OAuthTokenUsername` / `From`) is licensed and permitted to send.
- Time skew: Certificates used for assertions must have valid system time; ensure the host clock is synchronized.
- Permission errors: For Azure AD, verify application permissions (not delegated) include `SMTP.Send` and admin consent is granted.

### Security considerations

- Prefer certificate-based auth over client secrets in production.
- Never commit secrets or thumbprints with corresponding private key material to source control.
- Rotate secrets/certificates regularly.

## Migration notes for OAuth2 adoption

If upgrading from a previous version that used `credentials`:

1. Register an app in your identity provider (Azure AD example).
2. Grant SMTP send permissions.
3. Set `SmtpAuthenticationMode = OAuth2`.
4. Provide either `SecretId` or `SecretWindowsStoreCertificateThumbprint`.
5. Replace username/password with `OAuthTokenUsername` (if required by provider).

If `SmtpAuthenticationMode` remains `Basic`, existing behavior is unchanged.

## Contributing

Contributions are welcome! Please open an issue or PR for enhancements or fixes, including additional OAuth2 providers or flows.

---
