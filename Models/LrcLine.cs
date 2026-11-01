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

        ReadOnlySpan<char> span = lrcText.AsSpan();

        Span<char> tempTime = stackalloc char[11];
        tempTime[0] = '0';
        tempTime[1] = '0';
        tempTime[2] = ':';

        while (!span.IsEmpty)
        {
            int lineEnd = span.IndexOf('\n');
            ReadOnlySpan<char> line = lineEnd == -1 ? span : span[..lineEnd];
            if (lineEnd == -1)
            {
                span = ReadOnlySpan<char>.Empty;
            }
            else
            {
                span = span[(lineEnd + 1)..];
            }

            line = line.Trim();
            if (line.IsEmpty) continue;

            if (line.Length > 10 && line[0] == '[' && line[9] == ']')
            {
                ReadOnlySpan<char> timeSpanStr = line.Slice(1, 8); // format "01:23.45"
                ReadOnlySpan<char> textPart = line[10..].Trim();

                timeSpanStr.CopyTo(tempTime[3..]);

                for (int i = 0; i < tempTime.Length; i++)
                {
                    if (tempTime[i] == '.') tempTime[i] = ',';
                }

                if (TimeSpan.TryParse(tempTime, out TimeSpan time))
                {
                    lines.Add(new LrcLine
                    {
                        Time = time,
                        Text = textPart.ToString()
                    });
                }
            }
        }

        return [.. lines.OrderBy(l => l.Time)];
    }
}