using System;
using System.Threading;
using System.Threading.Tasks;
using Gunnsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Configur.AspNetCore
{
    public class ConfigurRefresherScheduledTask
        : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurRefresherScheduledTask> _logger;
        private Timer _timer;
        private readonly IBackgroundTaskQueue _queue;

        public ConfigurRefresherScheduledTask
        (
            IConfiguration configuration,
            IBackgroundTaskQueue queue,
            ILogger<ConfigurRefresherScheduledTask> logger
        )
        {
            _configuration = configuration;
            _queue = queue;
            _logger = logger;
        }

        public async Task StartAsync
        (
            CancellationToken cancellationToken
        )
        {
            try
            {
                var reloadInterval = TimeSpan.Parse
                (
                    _configuration[ConfigurKeys.RefreshInterval]
                );

                _timer = new Timer
                (
                    Refresh,
                    null,
                    reloadInterval,
                    reloadInterval
                );
            }
            catch (Exception exception)
            {
                var appId = _configuration[ConfigurKeys.AppId];

                _logger.LogError
                (
                    exception,
                    "Failed to start app setting refresher. AppId='{AppId}'",
                    appId
                );

                await StopAsync(cancellationToken);
            }
        }

        private void Refresh
        (
            object state
        )
        {
            try
            {
                ((IConfigurationRoot)_configuration).Reload();

                _queue.DequeueAsync(default(CancellationToken))
                    .GetAwaiter()
                    .GetResult();

                _queue.QueueBackgroundWorkItem(ConfigurSignalR.QueueWorkItem);
            }
            catch ( Exception exception)
            {
                var appId = _configuration[ConfigurKeys.AppId];

                _logger.LogError
                (
                    exception,
                    "Failed to refresh app settings. AppId='{AppId}'",
                    appId
                );

                StopAsync(default(CancellationToken))
                    .GetAwaiter()
                    .GetResult();
            }
        }

        public Task StopAsync
        (
            CancellationToken cancellationToken
        )
        {
            _timer?.Change(Timeout.Infinite, 0);

            Dispose();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
