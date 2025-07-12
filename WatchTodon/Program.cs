using System.Security.Authentication.ExtendedProtection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace WatchTodon;

class Program
{
    static async Task Main(string[] args)
    {
        var provider = Inject();

        Console.WriteLine("WatchTodon up and running");
        DateTime lastCheck = DateTime.Now;
        var counter = 0;
        while (true)
        {
            try
            {
                using (var scope = provider.CreateScope())
                {
                    var mastodonCommunication = scope.ServiceProvider.GetRequiredService<MastodonCommunication>();
                    await mastodonCommunication.CollectCommands(null);

                    if (counter != 0)
                    {
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                        if (lastCheck > DateTime.Now.AddMinutes(-15)) continue;
                        counter++;
                    }
                    
                    
                    lastCheck = DateTime.Now;
                    var watchDog = scope.ServiceProvider.GetRequiredService<WatchDog>();
                    await watchDog.Run(++counter);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(TimeSpan.FromMinutes(15)); // sleep on error (mastodon down)
            }
        }
    }


    private static ServiceProvider Inject()
    {
        var services = new ServiceCollection();
        services.AddScoped<DataBase>();
        services.AddScoped<MastodonCommunication>();
        services.AddScoped<WatchDog>();
        services.AddSingleton<Secrets>(JsonConvert.DeserializeObject<Secrets>(File.ReadAllText("./secrets.json")));

        var provider = services.BuildServiceProvider();
        return provider;
    }
}