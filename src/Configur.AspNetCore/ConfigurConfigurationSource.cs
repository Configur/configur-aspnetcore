using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http;

namespace Configur.AspNetCore
{
    public class ConfigurConfigurationSource
        : IConfigurationSource
    {
        private readonly string _appId;
        private readonly string _appSecret;
        private readonly string _appPassword;
        private readonly ConfigurOptions _configurOptions;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public ConfigurConfigurationSource
        (
            string appId,
            string appSecret,
            string appPassword,
            ConfigurOptions configurOptions,
            HttpClient httpClient
        )
        {
            _appId = appId;
            _appSecret = appSecret;
            _appPassword = appPassword;
            _configurOptions = configurOptions;
            _httpClient = httpClient;
        }

        public IConfigurationProvider Build
        (
            IConfigurationBuilder builder
        )
        {
            return new ConfigurConfigurationProvider
            (
                _appId,
                _appSecret,
                _appPassword,
                _configurOptions,
                _httpClient
            );
        }
    }
}
