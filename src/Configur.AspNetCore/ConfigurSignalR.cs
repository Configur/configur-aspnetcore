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

                var hubConnection = new HubConnectionBuilder()
                    .WithUrl
                    (
                        configuration["__configur-signalr-url"],
                        o =>
                        {
                            o.AccessTokenProvider = () =>
                            {
                                return Task.FromResult
                                (
                                    configuration["__configur-signalr-accesstoken"]
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
                        ((IConfigurationRoot)configuration).Reload();
                    }
                );

                await hubConnection.StartAsync(cancellationToken);

                Process.GetCurrentProcess()
                    .WaitForExit();
            }
        }
    }
}
