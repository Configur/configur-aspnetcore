using System;
using System.Collections.Generic;
using System.Text;

namespace Configur.AspNetCore
{
    public static class ConfigurKeys
    {
        public const string ApiHost = "__configur_api-host";
        public const string AppId = "__configur_app-id";
        public const string IdentityServerAuthority = "__configur_identity-server-authority";
        public const string IsDevelopment = "__configur_is-development";
        public const string IsFileCacheEnabled = "__configur_is-file-cache-enabled";
        public const string SignalRAccessToken = "__configur_signalr-accesstoken";
        public const string SignalRUrl = "__configur_signalr-url";
        public const string RefreshInterval = "__configur_refresh-interval";
    }
}
