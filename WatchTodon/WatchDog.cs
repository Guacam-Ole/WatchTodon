using System.Diagnostics.Contracts;
using Mastonet;

namespace WatchTodon;

public class WatchDog
{
    private readonly DataBase _dataBase;
    private readonly Secrets _secrets;

    public WatchDog(DataBase dataBase, Secrets secrets)
    {
        _dataBase = dataBase;
        _secrets = secrets;
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

    public void OutPutData()
    {
        Console.WriteLine("New Database Contents:\n");
        var all=_dataBase.GetAllEntries();
        Console.WriteLine("   DidFail? \t Interval \tLastChecked \t\tLastPost \t\tCreated \t\tname \n");
        foreach (var entry in all)
        {
            Console.WriteLine($"   {entry.DidFail} \t{entry.Interval} \t{entry.LastChecked} \t{entry.LastStatus} \t{entry.Created}\t'{entry.AccountToWatchName}'\n");
        }
    }

    private async Task CheckNewEntries(MastodonClient client)
    {
        var entries = _dataBase.GetAllUncheckedEntries();
        if (entries.Any())
        {
            Console.WriteLine($"Checking {entries.Count} new Entries ");
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
                Console.WriteLine($"Checked '{entry.AccountToWatchName}' for the first time");
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
        if (!entries.Any()) return;
        var stateChanged = false;
        Console.WriteLine($"Checking {entries.Count} for changes in the last hour ");
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
                    Console.WriteLine($"'{entry.AccountToWatchName[1..]}' is dead (last update:{entry.LastStatus})");
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
        if (entries.Any())
        {
            var stateChanged = false;
            Console.WriteLine($"re-Checking {entries.Count} failed Entries ");
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
                        Console.WriteLine($"'{entry.AccountToWatchName}' is alive again");
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

    private static async Task SendPrivateMessageTo(MastodonClient client, string recipient, string message)
    {
        try
        {
            await client.PublishStatus($"@{recipient} {message}", Visibility.Direct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // Make sure to create no messageloop
            return;
        }
    }
}