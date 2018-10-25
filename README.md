# Configur.AspNetCore

[![Build status](https://ci.appveyor.com/api/projects/status/1brl42ulw1l08ax9?svg=true)](https://ci.appveyor.com/project/andrewgunn/configur-aspnetcore/branch/master)
[![NuGet](https://buildstats.info/nuget/configur.aspnetcore)](https://www.nuget.org/packages/Configur.AspNetCore/)

Configuration provider for [ASP.NET Core 2.x](https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-2.1) that retrieves Valuables from a Vault within [Configur](https://www.configur.it).

## Overview

> Securely manage your configurations for your applications with a distributed architecture

Once you've created a Vault and added some Valuables, create an App. Each app has a unique connection string, including a password that only you will know, which is displayed after creation. Make sure to keep this safe as you'll need it to complete the integration.

## Usage

1. Install the `Configur.AspNetCore` [NuGet](https://www.nuget.org/packages/Configur.AspNetCore/) package
2. Add the App's connection string to `appsetings.json` (or via Application Settings in Azure with the key `ConfigurConnectionString`)

```
{
    "ConfigurConnectionString": "..."
}
```

3. Add the configuration provider to `ConfigureAppConfiguration` in `Program.cs`

```
public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
        ...
        .ConfigureAppConfiguration((context, config) =>
        {
            var hostingEnvironment = context.HostingEnvironment;
    
            var builtConfig = config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{hostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
    
            config.AddConfigur(builtConfig, builtConfig["ConfigurConnectionString"]);
        })
        ...
```

4. Add the services to `ConfigureServices` in `Startup.cs`

```
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddConfigur();
    ...
}
```

5. Add middleware to `Configure` in `Startup.cs`

```
public void Configure(IApplicationBuilder app)
{
    ...
    app.UseConfigur();
    ...
}
```

## Logs

If you're experiencing any problems, please check the logs at the root directory.
