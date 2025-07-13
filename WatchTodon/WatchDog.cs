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
        Console.WriteLine("Watchdog START");
        var client = Login();
        
        // Just once an hour
        await CheckFailedEntries(client);
        await CheckWorkingEntries(client);

        await CheckNewEntries(client);

        Console.WriteLine("Watchdog FINISH");
    }

    private MastodonClient Login()
    {
        return new MastodonClient(_secrets.Instance, _secrets.AccessToken) ??
               throw new Exception("Failed to connect to Mastodon");
    }

    public async Task OutPutData()
    {
        Console.WriteLine("Databease-Contents:\n");
        var all=_dataBase.GetAllEntries();
        Console.WriteLine("   DidFail? \tLastChecked \tLastPost \tname\n");
        foreach (var entry in all)
        {
            Console.WriteLine($"   {entry.DidFail} \t{entry.LastChecked} \t{entry.LastStatus} \t '{entry.AccountToWatchName}'\n");
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
        }
    }

    private async Task CheckWorkingEntries(MastodonClient client)
    {
        Console.WriteLine("Checking woring entries");
        var entries = _dataBase.GetAllEntriesOlderThan(TimeSpan.FromHours(1)).Where(q => !q.DidFail).ToList();
        if (!entries.Any()) return;
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
                    Console.WriteLine($"'{entry.AccountToWatchName[1..]}' is dead (last update:{entry.LastStatus})");
                    var language = new Language(entry.Language);
                    await SendPrivateMessageTo(client, entry.RequestedByName,
                        Language.Convert(language.GetCaptions().WatchDogBadNews, entry.AccountToWatchName[1..],
                            entry.LastStatusStr));
                }
            }

            _dataBase.UpsertEntry(entry);
        }
    }

    private async Task CheckFailedEntries(MastodonClient client)
    {
        Console.WriteLine("Checking failed entries");
        var entries = _dataBase.GetAllFailedEntries();
        if (entries.Any())
        {
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
                        entry.DidFail = false;
                        var language = new Language(entry.Language);
                        await SendPrivateMessageTo(client, entry.RequestedByName,
                            Language.Convert(language.GetCaptions().WatchDogGoodNews, entry.AccountToWatchName[1..],
                                entry.LastStatusStr));
                    }
                }

                _dataBase.UpsertEntry(entry);
            }
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