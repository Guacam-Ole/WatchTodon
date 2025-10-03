using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace WatchTodon;

class Program
{
    static async Task Main(string[] args)
    {
        var provider = Inject();
        var looper = provider.GetRequiredService<Looper>();
        Console.WriteLine("WatchTodon up and running");

        while (true) await looper.StartLoop();
    }


    private static ServiceProvider Inject()
    {
        var services = new ServiceCollection();
        services.AddScoped<DataBase>();
        services.AddScoped<MastodonCommunication>();
        services.AddScoped<WatchDog>();
        services.AddSingleton<Looper>();
        services.AddSingleton<Secrets>(JsonConvert.DeserializeObject<Secrets>(File.ReadAllText("./secrets.json")));


        services.AddLogging(cfg => cfg.SetMinimumLevel(LogLevel.Debug));
        services.AddSerilog(cfg =>
        {
            cfg.MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("job", Assembly.GetEntryAssembly()?.GetName().Name)
                .Enrich.WithProperty("service", Assembly.GetEntryAssembly()?.GetName().Name)
                .Enrich.WithProperty("desktop", Environment.GetEnvironmentVariable("DESKTOP_SESSION"))
                .Enrich.WithProperty("language", Environment.GetEnvironmentVariable("LANGUAGE"))
                .Enrich.WithProperty("lc", Environment.GetEnvironmentVariable("LC_NAME"))
                .Enrich.WithProperty("timezone", Environment.GetEnvironmentVariable("TZ"))
                .Enrich.WithProperty("dotnetVersion", Environment.GetEnvironmentVariable("DOTNET_VERSION"))
                .Enrich.WithProperty("inContainer",
                    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
                .WriteTo.GrafanaLoki(Environment.GetEnvironmentVariable("LOKIURL") ?? "http://thebeast:3100",
                    propertiesAsLabels: ["job"]);
            if (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ==
                "Debug")
            {
                cfg.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                cfg.WriteTo.Console();
            }
        });
        var provider = services.BuildServiceProvider();
        return provider;
    }
}