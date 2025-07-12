using System.Globalization;

namespace WatchTodon;

public class WatchDogEntry
{
    public int Id { get; set; }
    public required string AccountToWatchId { get; set; }
    public required string AccountToWatchName { get; set; }
    public required string RequestedById { get; set; }
    public required string RequestedByName { get; set; }
    public DateTime Created { get; set; }
    public required TimeSpan Interval { get; set; }
    public DateTime? LastChecked { get; set; }
    public DateTime? LastStatus { get; set; }

    public string LastStatusStr
    {
        get
        {
            if (LastStatus == null) return string.Empty;
            return LastStatus.Value.ToString(new CultureInfo("de-DE"));
        }
    }
    public bool DidFail { get; set; }
    public string Language { get; set; }
    
}