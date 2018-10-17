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

        private void AddOption
        (
            string key,
            string value
        )
        {
            if (Data.ContainsKey(key))
            {
                _logger.Debug
                (
                    "App setting already added. AppId='{AppId}' AppSettingKey='{AppSettingKey}'",
                    _appId,
                    value
                );
            }
            else
            {
                Data[key] = value;

                _logger.Debug
                (
                    "Added app setting. AppId='{AppId}' AppSettingKey='{AppSettingKey}'",
                    _appId
                );

            }
        }

        private void AddConfigurOptions()
        {
            AddOption
            (
                ConfigurKeys.ApiHost,
                _configurOptions.ApiHost
            );

            AddOption
            (
                ConfigurKeys.IdentityServerAuthority,
                _configurOptions.IdentityServerAuthority
            );

            AddOption
            (
                ConfigurKeys.IsDevelopment,
                _configurOptions.IsDevelopment.ToString()
            );

            AddOption
            (
                ConfigurKeys.IsFileCacheEnabled,
                _configurOptions.IsFileCacheEnabled.ToString()
            );
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

            if (_configurOptions.IsFileCacheEnabled)
            {
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

            IReadOnlyCollection<AppSetting> appSettings = null;

            try
            {
                appSettings = DecryptAppSettingsCiphertext
                (
                    projection
                );
            }
            catch (Exception exception)
            {
                _logger.Error
                (
                    exception,
                    "Failed to decrypt app settings. AppId='{AppId}'",
                    _appId
                );
            }

            var appSettingCount = 0;

            if (appSettings != null)
            {
                AddConfigurOptions();

                foreach (var appSetting in appSettings)
                {
                    var key = appSetting.Key;

                    AddOption
                    (
                        key,
                        appSetting.Value
                    );

                    appSettingCount++;
                }
            }

            _logger.Information
            (
                "Added " + appSettingCount + " app settings. AppId='{AppId}'",
                _appId
            );
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
                _logger.Debug
                (
                    "Calling IdentityServer to retrieve access token. AppId='{AppId}'",
                    _appId
                );

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

            _logger.Debug
            (
                "Calling API to retrieve app settings. AppId='{AppId}'",
                _appId
            );

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

            _logger.Debug
            (
                "Generating the key hash. AppId='{AppId}'",
                _appId
            );

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

            _logger.Debug
            (
                "Importing the private key. AppId='{AppId}'",
                _appId
            );

            var privateKey = virgilCrypto.ImportPrivateKey
            (
                Convert.FromBase64String(projection.PrivateKeyCiphertext),
                appKey
            );

            _logger.Debug
            (
                "Decrypting the app settings. AppId='{AppId}'",
                _appId
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

            AddOption
            (
                ConfigurKeys.SignalRAccessToken,
                projection.SignalR.AccessToken
            );

            AddOption
            (
                ConfigurKeys.SignalRUrl,
                projection.SignalR.Url
            );

            return appSettings;
        }
    }
}
