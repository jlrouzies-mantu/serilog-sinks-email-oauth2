using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.Email
{
    /// <summary>
    /// Specifies the SMTP authentication mode used when sending emails.
    /// </summary>
    public enum SmtpAuthenticationMode
    {
        /// <summary>
        /// No authentication
        /// </summary>
        None,

        /// <summary>
        /// Use traditional username/password (LOGIN/PLAIN) authentication.
        /// </summary>
        Basic,

        /// <summary>
        /// Use OAuth2 (e.g. XOAUTH2) token-based authentication.
        /// </summary>
        OAuth2
    }
}
