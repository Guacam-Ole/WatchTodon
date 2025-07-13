using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WatchTodon;

using Mastonet;
using Mastonet.Entities;

public class MastodonCommunication
{
    private readonly Secrets _secrets;
    private readonly DataBase _dataBase;
    private readonly MastodonClient _client;
    private Language _language;


    public MastodonCommunication(Secrets secrets, DataBase dataBase)
    {
        _secrets = secrets;
        _dataBase = dataBase;
        _client = Login();
    }


    private MastodonClient Login()
    {
        return new MastodonClient(_secrets.Instance, _secrets.AccessToken) ??
               throw new Exception("Failed to connect to Mastodon");
    }

    public async Task CollectCommands(string? sinceId)
    {
        var notifications = await _client.GetNotifications(new ArrayOptions() { Limit = 200, SinceId = sinceId });
        var mentions = notifications.Where(q => q.Type == "mention").ToList();
        if (!mentions.Any()) return;
        foreach (var notification in mentions)
        {
            await InterpretNotification(notification);
        }

        //await _client.ClearNotifications();
    }

    private async Task InterpretNotification(Notification notification)
    {
        try
        {
            if (notification?.Status?.Content == null)
                return; // not a valid reply. Don't make a fuzz about it, just ignore
            _language = new Language(notification.Status.Language ?? "en");

            // Quick and dirty html to plain text:
            var plainText = Regex.Replace(notification.Status.Content, "<[^>]+?>", "")
                .Replace("@watchTodon", "", StringComparison.InvariantCultureIgnoreCase).Trim();
            switch (plainText.Split(' ')[0].ToUpper())
            {
                case "ADD":
                    await ParseAdd(notification, plainText);
                    break;
                case "REMOVE":
                    await ParseRemove(notification, plainText);
                    break;
                case "INFO":
                    await ParseInfo(notification);
                    break;
                case "CLEAR":
                    await ParseClear(notification);
                    break;
                    ;
                default:
                    await ReplyWithHelp(notification.Status);
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            await _client.DismissNotification(notification!.Id);
        }
    }

    private async Task ParseAdd(Notification notification, string text)
    {
        ArgumentNullException.ThrowIfNull(notification.Status);
        // Validate input:
        var contentArray = text.Trim().Split(' ');
        if (contentArray.Length < 3)
        {
            await ReplyWithHelp(notification.Status);
            return;
        }

        var account = contentArray[1];
        if (account.StartsWith('@'))
        {
            var fullAccount = notification.Status.Mentions.First(q => q.UserName == account[1..]);
            account = $"@{fullAccount.AccountName}";    
        }
      
        
        var interval = contentArray[2];

        if (!int.TryParse(interval, out var hours))
        {
            await ReplyWithHelp(notification.Status);
            return;
        }

        // Check Account:
        var matchId = await GetAccountIdByName(account);
        if (matchId == null)
        {
            await ReplyWithMessage(notification.Status,
                Language.Convert(_language.GetCaptions().ErrAccountNotFound, account));
            return;
        }

        AddUpdateAccount(notification.Status.Account.Id, notification.Status.Account.AccountName, matchId, account, hours);
        var newCount = _dataBase.GetAllEntriesFor(notification.Status.Account.Id).Count;
        await ReplyWithMessage(notification.Status,
            Language.Convert(_language.GetCaptions().InfoAccountAdded, account, hours, newCount));
        Console.WriteLine($"added or updated Account {account} with {hours}. (now {newCount} for {notification.Status.Account.AccountName}");
    }

    private async Task<string?> GetAccountIdByName(string account)
    {
        if (!account.StartsWith('@')) account = "@" + account;
        var accountSearch = await _client.Search(account, true);
        var match = accountSearch.Accounts.FirstOrDefault();
        return match?.Id;
    }

    private async Task ParseClear(Notification notification)
    {
        RemoveAll(notification.Status.Account.Id);
        await ReplyWithMessage(notification.Status, "Done");
    }
    
    private async Task ParseRemove(Notification notification, string text)
    {
        ArgumentNullException.ThrowIfNull(notification.Status);
        var contentArray = text.Trim().Split(' ');
        if (contentArray.Length < 2)
        {
            await ReplyWithHelp(notification.Status);
            return;
        }

        var account = contentArray[1];
        var matchId = await GetAccountIdByName(contentArray[1]);
        if (matchId == null)
        {
            await ReplyWithMessage(notification.Status,
                Language.Convert(_language.GetCaptions().ErrAccountNotFound, account));
            return;
        }

        if (!RemoveAccount(notification.Status.Account.Id, matchId))
        {
            await ReplyWithMessage(notification.Status, Language.Convert(_language.GetCaptions().ErrNotRemoved, account));
        }
        else
        {
            await ReplyWithMessage(notification.Status,
                Language.Convert(_language.GetCaptions().InfoAccountRemoved, account));
        }
        Console.WriteLine($"removed Account {account} for {notification.Status.Account.AccountName}");
    }

    private async Task ParseInfo(Notification notification)
    {
        _dataBase.GetStats(out var total, out var broken);
        var msg = Language.Convert(_language.GetCaptions().InfoYourStats, total, broken);
        var yourWatchDogs = _dataBase.GetAllEntriesFor(notification.Status!.Account.Id);
        foreach (var watchDog in yourWatchDogs)
        {
            msg += Language.Convert(_language.GetCaptions().InfoWatchdogSingleLine, watchDog.AccountToWatchName[1..],
                watchDog.Interval.TotalHours);
        }

        if (msg.Length > 480) msg = msg[..480] + "...";

        await ReplyWithMessage(notification.Status, msg);
    }


    private bool RemoveAccount(string senderId, string watchAccountId)
    {
        var existingEntry = _dataBase.GetEntryFor(senderId, watchAccountId);
        if (existingEntry == null) return false;
        _dataBase.RemoveEntry(existingEntry.Id);
        return true;
    }

    private bool RemoveAll(string senderId)
    {
        var allEntries = _dataBase.GetAllEntriesFor(senderId);
        foreach (var entry in allEntries)
        {
            _dataBase.RemoveEntry(entry.Id);
        }

        return true;
    }

    private void AddUpdateAccount(string senderId, string senderName, string watchAccountId, string watchAccountName, int interval)
    {
        if (!watchAccountName.StartsWith('@')) watchAccountName = "@" + watchAccountName; 
        var existingEntry = _dataBase.GetEntryFor(senderId, watchAccountId);
        if (existingEntry != null)
        {
            existingEntry.DidFail = false;
            existingEntry.LastChecked = null;
            existingEntry.LastStatus = null;
            existingEntry.Interval = TimeSpan.FromHours(interval);

            _dataBase.UpsertEntry(existingEntry);
        }
        else
        {
            _dataBase.UpsertEntry(new WatchDogEntry
            {
                AccountToWatchId = watchAccountId,
                AccountToWatchName = watchAccountName,
                RequestedById = senderId,
                RequestedByName = senderName,
                Interval = TimeSpan.FromHours(interval),
                Created = DateTime.Now,
                Language = _language.GetCaptions().Language
            });
        }
    }

    private async Task ReplyWithHelp(Status? replyTo)
    {
        if (replyTo == null) return;
        var message = GetHelpMessage(replyTo, out var imageFiles, out var markdownFiles);
        await ReplyWithMessage(replyTo, message, imageFiles, markdownFiles);
    }


    private string GetHelpMessage(Status status, out string[] images, out string[] markdowns)
    {
        var aboutFileImage = $"help.{status.Language}.about.png";
        var helpFileImage = $"help.{status.Language}.commands.png";
        
        var aboutMarkdownFile = $"help.{status.Language}.about.md";
        var helpMarkdownFile = $"help.{status.Language}.commands.md";
        if (!File.Exists(helpFileImage)) { 
            helpFileImage = "help.en.about.png";
            aboutFileImage = "help.en.commands.png";
            aboutMarkdownFile = "help.en.about.md";
            helpMarkdownFile = "help.en.commands.md";
        }

        images = [aboutFileImage, helpFileImage];
        markdowns = [aboutMarkdownFile, helpMarkdownFile];

        return _language.GetCaptions().ErrSorry;
    }

    private async Task ReplyWithMessage(Status? replyTo, string message, string[]? imageFileNames = null,
        string[]? altFilenames = null)
    {
        if (replyTo == null) return;
        var mediaIds = new List<string>();
        if (imageFileNames != null && altFilenames != null)
        {
            for (var index = 0; index < imageFileNames.Length; index++)
            {
                var altFilename = altFilenames[index];
                var imageFileName = imageFileNames[index];
                var alt = await File.ReadAllTextAsync(altFilename);
                if (alt.Length > 1400) alt = alt[..1400];
                await using var fs = File.Open(imageFileName, FileMode.Open);
                var attachment = await _client.UploadMedia(fs, imageFileName, alt);
                mediaIds.Add(attachment.Id);
            }
        }

        message = $"@{replyTo.Account.AccountName} " + message;

        await _client.PublishStatus(message, Visibility.Direct, replyTo.Id, mediaIds);
    }
}