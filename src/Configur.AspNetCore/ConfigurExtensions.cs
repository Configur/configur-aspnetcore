﻿using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Gunnsoft.AspNetCore;
using Gunnsoft.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configur.AspNetCore
{
    public static class ConfigurExtensions
    {
        public static IConfigurationBuilder AddConfigur
        (
            this IConfigurationBuilder extended,
            IConfigurationRoot configuration,
            string connectionString
        )
        {
            return AddConfigur
            (
                extended,
                configuration,
                connectionString,
                options => { }
            );
        }

        public static IConfigurationBuilder AddConfigur
        (
            this IConfigurationBuilder extended,
            IConfigurationRoot configuration,
            string connectionString,
            Action<ConfigurOptions> options
        )
        {
            SerilogLogger.Instance.Warning
            (
                "Adding Configur to IConfigurationBuilder."
            );

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SerilogLogger.Instance.Warning
                (
                    "Failed to add Configur because the connection string is null or empty."
                );

                return extended;
            }

            var chunks = connectionString.Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s, "^[^=]+=[^=]+$"))
                .Select(s =>
                {
                    var chunk = s.Split('=');

                    return new
                    {
                        Key = chunk[0],
                        Value = chunk[1]
                    };
                })
                .ToList();

            var appId = chunks.SingleOrDefault(c => string.Equals(c.Key, "AppId", StringComparison.InvariantCultureIgnoreCase));
            var appSecret = chunks.SingleOrDefault(c => string.Equals(c.Key, "AppSecret", StringComparison.InvariantCultureIgnoreCase));
            var appPassword = chunks.SingleOrDefault(c => string.Equals(c.Key, "AppPassword", StringComparison.InvariantCultureIgnoreCase));

            if (appId == null
                || appSecret == null
                || appPassword == null)
            {
                SerilogLogger.Instance.Warning
                (
                    "Failed to add Configur because the connection string is invalid. AppId='{AppId}' AppSecret='{AppSecret}' AppPassword='{AppPassword}'",
                    appId,
                    appSecret,
                    appPassword
                );

                return extended;
            }

            var configurOptions = new ConfigurOptions();
            options(configurOptions);
            
            var configurationSource = new ConfigurConfigurationSource
            (
                appId.Value,
                appSecret.Value,
                appPassword.Value,
                configurOptions,
                new HttpClient
                (
                    new LoggingHandler
                    (
                        new HttpClientHandler(),
                        new Logger<LoggingHandler>
                        (
                            new SerilogLoggerFactory
                            (
                                SerilogLogger.Instance
                            )
                        )
                    )
                )
                {
                    Timeout = TimeSpan.FromSeconds(5),
                }
            );

            extended.Add(configurationSource);

            return extended;
        }

        public static IServiceCollection AddConfigur
        (
            this IServiceCollection extended
        )
        {
            SerilogLogger.Instance.Warning
            (
                "Adding Configur to IServiceCollection."
            );

            extended.AddHostedService<ValuablesRefresherScheduledTask>();
            extended.AddHostedService<QueuedHostedService>();
            extended.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            return extended;
        }

        public static IApplicationBuilder UseConfigur
        (
            this IApplicationBuilder extended
        )
        {
            SerilogLogger.Instance.Warning
            (
                "Adding Configur to IApplicationBuilder."
            );

            var queue = extended.ApplicationServices.GetService<IBackgroundTaskQueue>();
            queue.QueueBackgroundWorkItem(BackgroundTasks.SignalR);

            return extended;
        }
    }
}
