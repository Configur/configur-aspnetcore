using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Configur.AspNetCore
{
    public static class ConfigurSignalR
    {
        public static async Task QueueWorkItem
        (
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken
        )
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var appId = configuration[ConfigurKeys.AppId];
                var signalRUrl = configuration[ConfigurKeys.SignalRUrl];
                var signalRAccessToken = configuration[ConfigurKeys.SignalRAccessToken];

                try
                {
                    var hubConnection = new HubConnectionBuilder()
                        .WithUrl
                        (
                            signalRUrl,
                            o =>
                            {
                                o.AccessTokenProvider = () =>
                                {
                                    return Task.FromResult
                                    (
                                        signalRAccessToken
                                    );
                                };
                            }
                        )
                        .Build();

                    hubConnection.On
                    (
                        "ValuablesDeposited",
                        (string vaultId) =>
                        {
                            try
                            {
                                SerilogLogger.Instance.Information
                                (
                                    "Reloading configuration via SignalR. AppId='{AppId}'",
                                    appId
                                );

                                ((IConfigurationRoot)configuration).Reload();
                            }
                            catch (Exception exception)
                            {
                                SerilogLogger.Instance.Error
                                (
                                    exception,
                                    "Failed to reload configuration via SignalR. AppId='{AppId}'",
                                    appId
                                );
                            }
                        }
                    );

                    await hubConnection.StartAsync(cancellationToken);

                    Process.GetCurrentProcess()
                        .WaitForExit();
                }
                catch (Exception exception)
                {
                    SerilogLogger.Instance.Error
                    (
                        exception,
                        "Failed to add SignalR. AppId='{AppId}'",
                        appId
                    );
                }
            }
        }
    }
}
