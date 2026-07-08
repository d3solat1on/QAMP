using System.ComponentModel;
using System.Text.RegularExpressions;

namespace QAMP.Models;

public class LrcLine : INotifyPropertyChanged
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = string.Empty;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public static partial class LyricsCache
{
    private static readonly Dictionary<string, List<LrcLine>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _plainTextCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxCacheSize = 50;

    [GeneratedRegex(@"\[(?<time>\d{2}:\d{2}\.\d{2,3})\]\s*(?<text>.*)")]
    private static partial Regex LrcRegex();

    public static List<LrcLine> GetParsedLyrics(string filePath, string? lyrics)
    {
        if (string.IsNullOrEmpty(filePath))
            return [];

        if (_cache.TryGetValue(filePath, out var cached))
            return cached;

        var parsed = ParseLrc(lyrics);

        if (_cache.Count >= MaxCacheSize)
        {
            var first = _cache.First();
            _cache.Remove(first.Key);
        }
        _cache[filePath] = parsed;

        return parsed;
    }

    public static string GetPlainText(string filePath, string? lyrics)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        if (_plainTextCache.TryGetValue(filePath, out var cached))
            return cached;

        var text = string.IsNullOrEmpty(lyrics) ? "Lyrics are missing" : lyrics;

        if (_plainTextCache.Count >= MaxCacheSize)
        {
            var first = _plainTextCache.First();
            _plainTextCache.Remove(first.Key);
        }
        _plainTextCache[filePath] = text;

        return text;
    }

    public static void Clear()
    {
        _cache.Clear();
        _plainTextCache.Clear();
    }

    private static List<LrcLine> ParseLrc(string? lrcText)
    {
        var lines = new List<LrcLine>();
        if (string.IsNullOrEmpty(lrcText)) return lines;

        var regex = LrcRegex();

        foreach (var line in lrcText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var match = regex.Match(line);
            if (match.Success)
            {
                if (TimeSpan.TryParse("00:" + match.Groups["time"].Value.Replace(".", ","), out TimeSpan time))
                {
                    lines.Add(new LrcLine { Time = time, Text = match.Groups["text"].Value.Trim() });
                }
            }
        }

        return [.. lines.OrderBy(l => l.Time)];
    }
}