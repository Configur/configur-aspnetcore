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
using Virgil.Crypto;
using static IdentityModel.OidcConstants;
using TokenRequest = IdentityModel.Client.TokenRequest;

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

        public ConfigurConfigurationProvider
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

        private void AddConfigurOptions()
        {
            AddOption
            (
                ConfigurKeys.ApiHost,
                _configurOptions.ApiHost
            );

            AddOption
            (
                ConfigurKeys.AppId,
                _appId
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

            AddOption
            (
                ConfigurKeys.RefreshInterval,
                _configurOptions.RefreshInterval.ToString()
            );
        }

        private void AddOption
        (
            string key,
            string value
        )
        {
            if (Data.ContainsKey(key))
            {
                SerilogLogger.Instance.Debug
                (
                    "App setting already added. AppId='{AppId}' ValuableKey='{ValuableKey}'",
                    _appId,
                    key
                );
            }
            else
            {
                Data[key] = value;

                SerilogLogger.Instance.Debug
                (
                    "Added app setting. AppId='{AppId}' ValuableKey='{ValuableKey}'",
                    _appId,
                    key
                );

            }
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

            AddConfigurOptions();

            FindValuablesProjection projection = null;
            string findValuablesResponseContent = null;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                SerilogLogger.Instance.Information
                (
                    "Attempting to find valuables from the API. AppId='{AppId}'",
                    _appId
                );

                findValuablesResponseContent = await FindValuablesAsync();
                projection = JsonConvert.DeserializeObject<FindValuablesProjection>
                (
                    findValuablesResponseContent
                );

                SerilogLogger.Instance.Information
                (
                    "Successfully found valuables from the API in {ElapsedMilliseconds}ms. AppId='{AppId}'",
                    stopwatch.ElapsedMilliseconds,
                    _appId
                );
            }
            catch (Exception exception)
            {
                SerilogLogger.Instance.Error
                (
                    exception,
                    "Failed to find valuables from the API in {ElapsedMilliseconds}ms. AppId='{AppId}'",
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
                var fileCachePath = $"configur_find-valuables_{_appId}.json";

                if (projection != null)
                {
                    try
                    {
                        SerilogLogger.Instance.Information
                        (
                            "Attempting to save valuables to the file cache. AppId='{AppId}'",
                            _appId
                        );

                        if (!File.Exists(fileCachePath))
                        {
                            File.Create(fileCachePath)
                                .Dispose();
                        }

                        File.WriteAllText
                        (
                            fileCachePath,
                            findValuablesResponseContent,
                            Encoding.UTF8
                        );

                        SerilogLogger.Instance.Information
                        (
                            "Successfully saved valuables to the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                    catch (Exception exception)
                    {
                        SerilogLogger.Instance.Error
                        (
                            exception,
                            "Failed to save valuables to the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                }
                else if (File.Exists(fileCachePath))
                {
                    try
                    {
                        SerilogLogger.Instance.Information
                        (
                            "Attempting to load valuables from the file cache. AppId='{AppId}'",
                            _appId
                        );

                        var fileContents = File.ReadAllText
                        (
                            fileCachePath,
                            Encoding.UTF8
                        );

                        projection = JsonConvert.DeserializeObject<FindValuablesProjection>
                        (
                            fileContents
                        );

                        SerilogLogger.Instance.Information
                        (
                            "Successfully loaded valuables from the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                    catch (Exception exception)
                    {
                        SerilogLogger.Instance.Error
                        (
                            exception,
                            "Failed to load valuables from the file cache. AppId='{AppId}'",
                            _appId
                        );
                    }
                }
            }

            if (projection == null)
            {
                SerilogLogger.Instance.Warning
                (
                    "Failed to add app settings. AppId='{AppId}'",
                    _appId
                );

                return;
            }

            IReadOnlyCollection<Valuable> valuables = null;

            try
            {
                valuables = DecryptValuablesCiphertext
                (
                    projection
                );
            }
            catch (Exception exception)
            {
                SerilogLogger.Instance.Error
                (
                    exception,
                    "Failed to decrypt valuables. AppId='{AppId}'",
                    _appId
                );
            }

            var valuableCount = 0;

            if (valuables != null)
            {
                foreach (var valuable in valuables)
                {
                    var key = valuable.Key;

                    AddOption
                    (
                        key,
                        valuable.Value
                    );

                    valuableCount++;
                }
            }

            SerilogLogger.Instance.Information
            (
                "Added " + valuableCount + " app setting(s). AppId='{AppId}'",
                _appId
            );
        }

        public async Task<string> FindValuablesAsync()
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"https://{_configurOptions.ApiHost}/valuables/find")
            };

            if (_configurOptions.IsDevelopment)
            {
                request.Headers.Add("X-ClientId", _appId);
            }
            else
            {
                SerilogLogger.Instance.Debug
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
                        GrantType = GrantTypes.ClientCredentials,
                        Parameters =
                        {
                            {
                                "scope",
                                "configur_api"
                            }
                        }
                    }
                );

                // TODO Check tokenResponse.IsError

                request.Headers.Add
                (
                    "Authorization",
                    $"Bearer {tokenResponse.AccessToken}"
                );
            }

            SerilogLogger.Instance.Debug
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

        public IReadOnlyCollection<Valuable> DecryptValuablesCiphertext
        (
            FindValuablesProjection projection
        )
        {
            var virgilCrypto = new VirgilCrypto();

            SerilogLogger.Instance.Debug
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
                    ),
                    HashAlgorithm.SHA512
                )
            );

            SerilogLogger.Instance.Debug
            (
                "Importing the private key. AppId='{AppId}'",
                _appId
            );

            var privateKey = virgilCrypto.ImportPrivateKey
            (
                Convert.FromBase64String(projection.PrivateKeyCiphertext),
                appKey
            );

            SerilogLogger.Instance.Debug
            (
                "Decrypting the app settings. AppId='{AppId}'",
                _appId
            );

            var decrypted = Encoding.UTF8.GetString
            (
                virgilCrypto.Decrypt
                (
                    Convert.FromBase64String(projection.Ciphertext),
                    privateKey
                )
            );

            var valuables = JsonConvert.DeserializeObject<IReadOnlyCollection<Valuable>>
            (
                decrypted
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

            return valuables;
        }
    }
}
