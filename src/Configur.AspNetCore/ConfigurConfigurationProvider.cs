using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using Virgil.Crypto;

namespace Configur.AspNetCore
{
    public class ConfigurConfigurationProvider
        : ConfigurationProvider
    {
        private readonly string _appId;
        private readonly string _appSecret;
        private readonly string _appPassword;
        private readonly ConfigurOptions _configurOptions;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public ConfigurConfigurationProvider
        (
            string appId,
            string appSecret,
            string appPassword,
            ConfigurOptions configurOptions,
            HttpClient httpClient,
            ILogger logger
        )
        {
            _appId = appId;
            _appSecret = appSecret;
            _appPassword = appPassword;
            _configurOptions = configurOptions;
            _httpClient = httpClient;
            _logger = logger;
        }

        public override void Load()
        {
            Task.Run(async () =>
                {
                    await LoadAsync();
                })
                .GetAwaiter()
                .GetResult();
        }

        private async Task LoadAsync()
        {
            try
            {
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                FindAppSettingsProjection projection = null;
                string findAppSettingsResponseContent = null;

                var stopwatch = Stopwatch.StartNew();

                try
                {
                    _logger.Information
                    (
                        "Attempting to load app settings from the API. AppId='{AppId}'",
                        _appId
                    );

                    findAppSettingsResponseContent = await FindAppSettingsAsync();
                    projection = JsonConvert.DeserializeObject<FindAppSettingsProjection>
                    (
                        findAppSettingsResponseContent
                    );

                    _logger.Information
                    (
                        "Successfully loaded app settings from the API in {ElapsedMilliseconds}ms. AppId='{AppId}'",
                        stopwatch.ElapsedMilliseconds,
                        _appId
                    );
                }
                catch (Exception exception)
                {
                    _logger.Error
                    (
                        exception,
                        "Failed to load app settings from the API in {ElapsedMilliseconds}ms. AppId='{AppId}'",
                        stopwatch.ElapsedMilliseconds,
                        _appId
                    );
                }
                finally
                {
                    stopwatch.Stop();
                }

                var fileCachePath = $"configur_appsettings_{_appId}.json";

                if (projection != null)
                {
                    try
                    {
                        _logger.Information
                        (
                            "Attempting to save app settings to the file cache. AppId='{AppId}'",
                            _appId
                        );

                        if (!File.Exists(fileCachePath))
                        {
                            File.Create(fileCachePath);
                        }

                        File.WriteAllText
                        (
                            fileCachePath,
                            findAppSettingsResponseContent,
                            Encoding.UTF8
                        );

                        _logger.Information
                        (
                            "Successfully saved app settings to the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                    catch (Exception exception)
                    {
                        _logger.Error
                        (
                            exception,
                            "Failed to save app settings to the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                }
                else if (File.Exists(fileCachePath))
                {
                    try
                    {
                        _logger.Information
                        (
                            "Attempting to load app settings from the file cache. AppId='{AppId}'",
                            _appId
                        );

                        var fileContents = File.ReadAllText
                        (
                            fileCachePath,
                            Encoding.UTF8
                        );

                        projection = JsonConvert.DeserializeObject<FindAppSettingsProjection>
                        (
                            fileContents
                        );

                        _logger.Information
                        (
                            "Successfully loaded app settings from the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                    catch (Exception exception)
                    {
                        _logger.Error
                        (
                            exception,
                            "Failed to load app settings from the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                }

                if (projection == null)
                {
                    _logger.Warning
                    (
                        "Failed to load the app settings. AppId='{AppId}'",
                        _appId
                    );

                    return;
                }

                var appSettings = DecryptAppSettingsCiphertext
                (
                    projection
                );

                foreach (var appSetting in appSettings)
                {
                    var key = appSetting.Key;

                    Data[key] = appSetting.Value;
                }
            }
            catch (Exception exception)
            {
                // TODO Add logging.
            }
        }

        public async Task<string> FindAppSettingsAsync()
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"https://{_configurOptions.ApiHost}/app-settings/find")
            };

            if (_configurOptions.IsDevelopment)
            {
                request.Headers.Add("X-ClientId", _appId);
            }
            else
            {
                var tokenResponse = await _httpClient.RequestTokenAsync
                (
                    new TokenRequest
                    {
                        Address = $"{_configurOptions.IdentityServerAuthority.TrimEnd('/')}/connect/token",
                        ClientId = _appId,
                        ClientSecret = _appSecret,
                        Parameters =
                        {
                            {
                                "scope",
                                "configur_api"
                            }
                        }
                    }
                );

                request.Headers.Add
                (
                    "Authorization",
                    $"Bearer {tokenResponse.AccessToken}"
                );
            }

            var response = await _httpClient.SendAsync
            (
                request
            );

            return await response.Content.ReadAsStringAsync();
        }

        public IReadOnlyCollection<AppSetting> DecryptAppSettingsCiphertext
        (
            FindAppSettingsProjection projection
        )
        {
            var virgilCrypto = new VirgilCrypto();

            var appKey = Encoding.UTF8.GetString
            (
                virgilCrypto.GenerateHash
                (
                    Encoding.UTF8.GetBytes
                    (
                        _appPassword
                    )
                )
            );
            var privateKey = virgilCrypto.ImportPrivateKey
            (
                Convert.FromBase64String(projection.PrivateKeyCiphertext),
                appKey
            );
            var valuables = Encoding.UTF8.GetString
            (
                virgilCrypto.Decrypt
                (
                    Convert.FromBase64String(projection.Ciphertext),
                    privateKey
                )
            );

            var appSettings = JsonConvert.DeserializeObject<IReadOnlyCollection<AppSetting>>
            (
                valuables
            );

            Data["__configur-signalr-url"] = projection.SignalR.Url;
            Data["__configur-signalr-accesstoken"] = projection.SignalR.AccessToken;

            return appSettings;
        }
    }
}
