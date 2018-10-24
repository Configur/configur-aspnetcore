using System;
using System.Threading;
using System.Threading.Tasks;
using Gunnsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Configur.AspNetCore
{
    public class ValuablesRefresherScheduledTask
        : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private Timer _timer;
        private readonly IBackgroundTaskQueue _queue;

        public ValuablesRefresherScheduledTask
        (
            IConfiguration configuration,
            IBackgroundTaskQueue queue
        )
        {
            _configuration = configuration;
            _queue = queue;
        }

        public async Task StartAsync
        (
            CancellationToken cancellationToken
        )
        {
            var appId = _configuration[ConfigurKeys.AppId];

            try
            {
                var reloadInterval = TimeSpan.Parse
                (
                    _configuration[ConfigurKeys.RefreshInterval]
                );

                SerilogLogger.Instance.Information
                (
                    "Attemtping to start valuables refresher. AppId='{AppId}' ReloadInterval='{ReloadInterval}'",
                    appId,
                    reloadInterval
                );

                _timer = new Timer
                (
                    Refresh,
                    null,
                    reloadInterval,
                    reloadInterval
                );

                SerilogLogger.Instance.Information
                (
                    "Started valuables refresher. AppId='{AppId}'",
                    appId
                );
            }
            catch (Exception exception)
            {
                SerilogLogger.Instance.Error
                (
                    exception,
                    "Failed to start valubles refresher. AppId='{AppId}'",
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
            var appId = _configuration[ConfigurKeys.AppId];

            try
            {
                SerilogLogger.Instance.Information
                (
                    "Reloading configuration via refresher. AppId='{AppId}'",
                    appId
                );

                ((IConfigurationRoot)_configuration).Reload();

                _queue.DequeueAsync(default(CancellationToken))
                    .GetAwaiter()
                    .GetResult();

                _queue.QueueBackgroundWorkItem(BackgroundTasks.SignalR);
            }
            catch (Exception exception)
            {
                SerilogLogger.Instance.Error
                (
                    exception,
                    "Failed to reload configuration via refresher. AppId='{AppId}'",
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

            var appId = _configuration[ConfigurKeys.AppId];

            SerilogLogger.Instance.Information
            (
                "Stopped valuables refresher. AppId='{AppId}'",
                appId
            );

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
