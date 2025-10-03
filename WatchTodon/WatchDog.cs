using System.Globalization;
using Mastonet;
using Microsoft.Extensions.Logging;

namespace WatchTodon;

public class WatchDog
{
    private readonly DataBase _dataBase;
    private readonly Secrets _secrets;
    private readonly ILogger<WatchDog> _logger;

    public WatchDog(DataBase dataBase, Secrets secrets, ILogger<WatchDog> logger)
    {
        _dataBase = dataBase;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task Run()
    {
        var client = Login();

        await CheckFailedEntries(client);
        await CheckWorkingEntries(client);
        await CheckNewEntries(client);
    }

    private MastodonClient Login()
    {
        return new MastodonClient(_secrets.Instance, _secrets.AccessToken) ??
               throw new Exception("Failed to connect to Mastodon");
    }

    private string D2S(DateTime? dateTime)
    {
        return dateTime == null ? "never" : dateTime.Value.ToString(CultureInfo.InvariantCulture);
    }

    public void OutPutData()
    {
        _logger.LogDebug("New Database Contents:");
        var all = _dataBase.GetAllEntries();
        _logger.LogDebug("    Interval    LastChecked LastPost    Created     name ");
        foreach (var entry in all)
        {
            _logger.LogDebug(
                "   {FailIcon} {TotalHours} {LastChecked} {LastStatus} {Created} '{AccountToWatchName}'",
                entry.DidFail ? "🛑" : "🥑️", entry.Interval.TotalHours, D2S(entry.LastChecked), D2S(entry.LastStatus),
                entry.Created, entry.AccountToWatchName);
        }
    }

    private async Task CheckNewEntries(MastodonClient client)
    {
        var entries = _dataBase.GetAllUncheckedEntries();
        if (entries.Count != 0)
        {
            _logger.LogDebug("Checking '{Count}' new Entries ", entries.Count);
            foreach (var entry in entries)
            {
                entry.LastChecked = DateTime.Now;
                var newestStatuses =
                    await client.GetAccountStatuses(entry.AccountToWatchId, new ArrayOptions { Limit = 1 });
                if (newestStatuses.Any())
                {
                    entry.LastStatus = newestStatuses.First().CreatedAt.ToLocalTime();
                }

                _dataBase.UpsertEntry(entry);
                var language = new Language(entry.Language);
                _logger.LogDebug("Checked '{AccountToWatchName}' for the first time", entry.AccountToWatchName);
                await SendPrivateMessageTo(client, entry.RequestedByName,
                    Language.Convert(language.GetCaptions().WatchDogFirstTime, entry.AccountToWatchName[1..],
                        entry.LastStatusStr));
            }

            OutPutData();
        }
    }

    private async Task CheckWorkingEntries(MastodonClient client)
    {
        var entries = _dataBase.GetAllEntriesOlderThan(TimeSpan.FromHours(1)).Where(q => !q.DidFail).ToList();
        if (entries.Count == 0) return;
        var stateChanged = false;
        _logger.LogDebug("Checking {Count} for changes in the last hour ", entries.Count);
        foreach (var entry in entries)
        {
            entry.LastChecked = DateTime.Now;
            var newestStatuses =
                await client.GetAccountStatuses(entry.AccountToWatchId, new ArrayOptions { Limit = 1 });
            if (newestStatuses.Any())
            {
                entry.LastStatus = newestStatuses.First().CreatedAt.ToLocalTime();
                entry.DidFail = entry.LastStatus < DateTime.Now.Add(-entry.Interval);
                if (entry.DidFail)
                {
                    stateChanged = true;
                    _logger.LogDebug("'{AccountToWatchName}' is dead (last update:{LastStatus})", entry.AccountToWatchName[1..], entry.LastStatus);
                    var language = new Language(entry.Language);
                    await SendPrivateMessageTo(client, entry.RequestedByName,
                        Language.Convert(language.GetCaptions().WatchDogBadNews, entry.AccountToWatchName[1..],
                            entry.LastStatusStr));
                }
            }

            _dataBase.UpsertEntry(entry);
        }

        if (stateChanged) OutPutData();
    }

    private async Task CheckFailedEntries(MastodonClient client)
    {
        var entries = _dataBase.GetAllFailedEntries();
        if (entries.Count != 0)
        {
            var stateChanged = false;
            _logger.LogInformation("re-Checking {Count} failed Entries ", entries.Count);
            foreach (var entry in entries)
            {
                entry.LastChecked = DateTime.Now;
                var newestStatuses =
                    await client.GetAccountStatuses(entry.AccountToWatchId, new ArrayOptions { Limit = 1 });
                if (newestStatuses.Any())
                {
                    entry.LastStatus = newestStatuses.First().CreatedAt.ToLocalTime();
                    if ((entry.LastStatus > DateTime.Now.Add(-entry.Interval)))
                    {
                        _logger.LogInformation("'{Name}' is alive again", entry.AccountToWatchName);
                        stateChanged = true;
                        entry.DidFail = false;
                        var language = new Language(entry.Language);
                        await SendPrivateMessageTo(client, entry.RequestedByName,
                            Language.Convert(language.GetCaptions().WatchDogGoodNews, entry.AccountToWatchName[1..],
                                entry.LastStatusStr));
                    }
                }

                _dataBase.UpsertEntry(entry);
            }

            if (stateChanged) OutPutData();
        }
    }

    private async Task SendPrivateMessageTo(MastodonClient client, string recipient, string message)
    {
        try
        {
            await client.PublishStatus($"@{recipient} {message}", Visibility.Direct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed sending message to '{Recipient}'", recipient);
        }
    }
}