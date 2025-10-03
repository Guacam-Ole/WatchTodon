using Microsoft.Extensions.Logging;

namespace WatchTodon;

public class Looper
{
    private readonly MastodonCommunication _communication;
    private readonly WatchDog _watchDog;
    private readonly ILogger<Looper> _logger;
    private bool _startUp = true;
    private DateTime? lastCheck;

    public Looper(MastodonCommunication communication, WatchDog watchDog, ILogger<Looper> logger)
    {
        _communication = communication;
        _watchDog = watchDog;
        _logger = logger;
    }
    
    public async Task StartLoop()
    {
        try
        {
            await _communication.CollectCommands(null);

            if (_startUp)
            {
                _watchDog.OutPutData();
            }
            else
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
                if (lastCheck > DateTime.Now.AddMinutes(-15)) return;
            }

            lastCheck = DateTime.Now;
            await _watchDog.Run();
        }
        catch (Exception e)
        {
           _logger.LogError(e, "Error in Loop. Will wait for 15 minutes. Most likely Mastodon is just down");
            Thread.Sleep(TimeSpan.FromMinutes(15)); // sleep on error (mastodon down)
        }
    }
}