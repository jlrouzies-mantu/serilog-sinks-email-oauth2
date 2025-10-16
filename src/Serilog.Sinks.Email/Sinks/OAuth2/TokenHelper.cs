using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.OAuth2
{
    internal static class TokenHelper
    {
        public static string GetAccessToken(string tokenUrl, string scope, string clientId, string clientSecret)
        {
            using (var httpClient = new HttpClient())
            {
                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("scope", scope),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var response = httpClient.PostAsync(tokenUrl, requestBody).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                JObject tokenResult = JObject.Parse(responseBody);

                if (tokenResult == null || tokenResult["access_token"] == null)
                {
                    throw new Exception("Failed to obtain access token from OAuth2 provider.");
                }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string accessToken = tokenResult["access_token"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                return accessToken;
            }
        }

        public static string GetAccessTokenWithWindowsMachineCertificate(string tokenUrl, string scope, string clientId, string certificateThumbprint)
        {
            // Find the certificate in the Windows certificate store
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

                if (certificates.Count == 0)
                {
                    throw new InvalidOperationException($"Certificate with thumbprint {certificateThumbprint} not found in LocalMachine store.");
                }

                var certificate = certificates[0];

                // Create JWT header and payload
                var now = DateTimeOffset.UtcNow;
                var exp = now.AddMinutes(10);

                var header = new
                {
                    alg = "RS256",
                    typ = "JWT",
                    x5t = Convert.ToBase64String(certificate.GetCertHash())
                };

                var payload = new
                {
                    sub = clientId,
                    jti = Guid.NewGuid().ToString(),
                    aud = tokenUrl,
                    iat = ToUnixTimeSeconds(now),
                    nbf = ToUnixTimeSeconds(now),
                    exp = ToUnixTimeSeconds(exp),
                    iss = clientId
                };

                // Encode header and payload
                var headerJson = JsonConvert.SerializeObject(header);
                var payloadJson = JsonConvert.SerializeObject(payload);

                var headerBase64 = Base64UrlEncode(headerJson);
                var payloadBase64 = Base64UrlEncode(payloadJson);

                // Create signature
                var dataToSign = $"{headerBase64}.{payloadBase64}";
                byte[] dataToSignBytes = System.Text.Encoding.UTF8.GetBytes(dataToSign);

                using (var rsa = certificate.GetRSAPrivateKey())
                {
                    var signature = rsa!.SignData(dataToSignBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    var signatureBase64 = Base64UrlEncode(signature);

                    var clientAssertion = $"{headerBase64}.{payloadBase64}.{signatureBase64}";

                    // Request the token
                    using (var httpClient = new HttpClient())
                    {
                        var requestBody = new FormUrlEncodedContent(new[]
                        {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("scope", scope),
                    new KeyValuePair<string, string>("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                    new KeyValuePair<string, string>("client_assertion", clientAssertion),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                        var response = httpClient.PostAsync(tokenUrl, requestBody).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();

                        var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        JObject tokenResult = JObject.Parse(responseBody);

                        if (tokenResult == null || tokenResult["access_token"] == null)
                        {
                            throw new Exception("Failed to obtain access token from OAuth2 provider.");
                        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        string accessToken = tokenResult["access_token"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                        return accessToken;
                    }
                }
            }
        }

        // Helper methods
        private static long ToUnixTimeSeconds(DateTimeOffset date)
        {
            return date.ToUnixTimeSeconds();
        }

        private static string Base64UrlEncode(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return output;
        }
    }
}
