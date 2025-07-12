using Newtonsoft.Json;

namespace WatchTodon;

public class Language
{
    public class Captions
    {
        public string InfoYourStats { get; set; } // "Here are your statistics"
        public string ErrAccountNotFound { get; set; } // "Account not found"
        public string InfoAccountAdded { get; set; } // "Account added"
        public string ErrNotRemoved { get; set; } // Can't remove. No watch foundd
        public string InfoWatchdogSingleLine { get; set; }
        public string ErrSorry { get; set; } // I did not get that
        public string InfoAccountRemoved { get; set; }
        public string Language { get; set; }
        
        public string WatchDogFirstTime { get; set; }
        public string WatchDogGoodNews { get; set; }
        public string WatchDogBadNews { get; set; }
    }

    private readonly Captions _captions = new();

    public static string Convert(string caption, params object?[] parameters)
    {
        var counter = 0;
        while (caption.Contains('['))
        {
            var start = caption.IndexOf('[');
            var end = caption.IndexOf(']');
            caption = caption.Replace(caption.Substring(start, end - start+1), $"{parameters[counter++]}");
        }

        return caption;
    }

    public Language(string lang)
    {
        var langFile = $"./messages.{lang}.json";
        if (!File.Exists(langFile)) langFile = "./messages.en.json";
        _captions = JsonConvert.DeserializeObject<Captions>(File.ReadAllText(langFile));
        _captions.Language = lang;
    }

    public Captions GetCaptions()
    {
        return _captions;
    }
}