using System;

namespace Configur.AspNetCore
{
    public class ConfigurOptions
    {
        public string ApiHost { get; set; } = "api.configur.it";
        public string IdentityServerAuthority { get; set; } = "https://id.configur.it";
        public bool IsDevelopment { get; set; } = false;
        public bool IsFileCacheEnabled { get; set; } = true;
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
    }
}
