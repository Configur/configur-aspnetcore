using System;
using System.Threading;
using System.Threading.Tasks;
using Gunnsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Configur.AspNetCore
{
    public class ConfigurTimedRefresher : IHostedService, IDisposable
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly IConfiguration _configuration;
        private Timer _timer;

        public ConfigurTimedRefresher
        (
            IConfiguration configuration,
            IBackgroundTaskQueue queue
        )
        {
            _configuration = configuration;
            _queue = queue;
        }

        public Task StartAsync
        (
            CancellationToken cancellationToken
        )
        {
            _timer = new Timer
            (
                Reload,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );

            return Task.CompletedTask;
        }

        private void Reload
        (
            object state
        )
        {
            ((IConfigurationRoot)_configuration).Reload();

            _queue.DequeueAsync(default(CancellationToken))
                .GetAwaiter()
                .GetResult();

            _queue.QueueBackgroundWorkItem(ConfigurSignalR.QueueWorkItem);
        }

        public Task StopAsync
        (
            CancellationToken cancellationToken
        )
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
