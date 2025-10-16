﻿// Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.Email;

class MailKitEmailTransport(EmailSinkOptions options) : IEmailTransport
{
    public async Task SendMailAsync(EmailMessage emailMessage)
    {
        var fromAddress = MailboxAddress.Parse(emailMessage.From);
        using var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(fromAddress);
        mimeMessage.To.AddRange(emailMessage.To.Select(MailboxAddress.Parse));
        mimeMessage.Subject = emailMessage.Subject;
        mimeMessage.Body = options.IsBodyHtml
            ? new BodyBuilder { HtmlBody = emailMessage.Body }.ToMessageBody()
            : new BodyBuilder { TextBody = emailMessage.Body }.ToMessageBody();

        using var smtpClient = OpenConnectedSmtpClient();
        await smtpClient.SendAsync(mimeMessage);
        await smtpClient.DisconnectAsync(quit: true);
    }

    SmtpClient OpenConnectedSmtpClient()
    {
        var smtpClient = new SmtpClient();

        if (string.IsNullOrWhiteSpace(options.Host)) return smtpClient;

        if (options.ServerCertificateValidationCallback != null)
        {
            smtpClient.ServerCertificateValidationCallback += options.ServerCertificateValidationCallback;
        }

        smtpClient.Connect(options.Host, options.Port, options.ConnectionSecurity);

        if (options.SmtpAuthenticationMode == SmtpAuthenticationMode.OAuth2)
        {
            if (options.OAuthScope == null) throw new InvalidOperationException("OAuthScope must be set when using OAuth2 authentication.");
            if (options.OAuthTokenUrl == null) throw new InvalidOperationException("OAuthTokenUrl must be set when using OAuth2 authentication.");
            if (options.OAuthTokenUsername == null) throw new InvalidOperationException("OAuthTokenUsername must be set when using OAuth2 authentication.");
            if (options.ApplicationId == null) throw new InvalidOperationException("ApplicationId must be set when using OAuth2 authentication.");
            if (options.SecretId == null && options.SecretWindowsStoreCertificateThumbprint == null)
                throw new InvalidOperationException("Either SecretId or SecretWindowsStoreCertificateThumbprint must be set when using OAuth2 authentication.");

            string token = "";

            if (!String.IsNullOrEmpty(options.SecretId))
            {
                token = OAuth2.TokenHelper.GetAccessToken(
                    options.OAuthTokenUrl,
                    options.OAuthScope,
                    options.ApplicationId,
                    options.SecretId!);
            }

            if (!String.IsNullOrEmpty(options.SecretWindowsStoreCertificateThumbprint))
            {
                token = OAuth2.TokenHelper.GetAccessTokenWithWindowsMachineCertificate(
                    options.OAuthTokenUrl,
                    options.OAuthScope,
                    options.ApplicationId,
                    options.SecretWindowsStoreCertificateThumbprint!);
            }

            var oauth2 = new SaslMechanismOAuth2(options.OAuthTokenUsername, token);
            smtpClient.Authenticate(oauth2);
        }
        else if (options.SmtpAuthenticationMode == SmtpAuthenticationMode.Basic || options.SmtpAuthenticationMode == SmtpAuthenticationMode.None)
        {
            if (options.Credentials != null)
            {
                smtpClient.Authenticate(
                    Encoding.UTF8,
                    options.Credentials.GetCredential(
                        options.Host, options.Port, "smtp"));
            }
        }
        return smtpClient;
    }

    public void Dispose()
    {
    }
}
