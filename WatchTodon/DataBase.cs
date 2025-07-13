using LiteDB;

namespace WatchTodon;

public class DataBase
{
    private readonly LiteDatabase _database = new(Path);
    private const string Path = "/db/order.db";

    public WatchDogEntry? GetEntryFor(string requestedBy, string accountToWatch)
    {
        var firstMatch = _database.GetCollection<WatchDogEntry>()
            .FindOne(q => q.AccountToWatchId == accountToWatch && q.RequestedById == requestedBy);
        return firstMatch;
    }

    public bool UpsertEntry(WatchDogEntry entry)
    {
        return _database.GetCollection<WatchDogEntry>().Upsert(entry);
    }

    public List<WatchDogEntry> GetAllEntriesFor(string requestedBy)
    {
        return _database.GetCollection<WatchDogEntry>().Find(q => q.RequestedById == requestedBy).ToList();
    }

    public void GetStats(out long total, out long broken)
    {
        total = _database.GetCollection<WatchDogEntry>().LongCount();
        broken = _database.GetCollection<WatchDogEntry>().LongCount(q => q.DidFail);
    }

    public void RemoveEntry(int id)
    {
        _database.GetCollection<WatchDogEntry>().Delete(id);
    }

    public List<WatchDogEntry> GetAllUncheckedEntries()
    {
        return _database.GetCollection<WatchDogEntry>().Find(q => q.LastChecked == null).ToList();
    }

    public List<WatchDogEntry> GetAllFailedEntries()
    {
        return _database.GetCollection<WatchDogEntry>().Find(q => q.DidFail).ToList();
    }

    public List<WatchDogEntry> GetAllEntriesOlderThan(TimeSpan interval)
    {
        return _database.GetCollection<WatchDogEntry>().Find(q => q.LastChecked < DateTime.Now.AddHours(-interval.TotalHours)).ToList();
    }

    public List<WatchDogEntry> GetAllEntries()
    {
        return _database.GetCollection<WatchDogEntry>().FindAll().ToList();
    }
}
