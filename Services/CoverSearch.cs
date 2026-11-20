using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QAMP.Services;

public static partial class CoverSearch
{
    private static readonly HttpClient _coverUrlClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly HttpClient _coverImageClient = new();

    static CoverSearch()
    {
        _coverImageClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "QAMP-MusicPlayer (https://github.com/d3solat1on/QAMP)");
    }

    /// <summary>
    /// Searches Deezer first and falls back to iTunes, returning the artwork URL.
    /// </summary>
    public static async Task<string?> GetAlbumCoverUrlAsync(string artist, string album)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return null;

        string? coverUrl = await GetDeezerCoverUrlAsync(artist, album);
        if (!string.IsNullOrEmpty(coverUrl))
            return coverUrl;

        System.Diagnostics.Debug.WriteLine("[DiscordRPC] Deezer didn't find the cover. Falling back to iTunes...");
        return await GetItunesCoverUrlAsync(artist, album);
    }

    /// <summary>
    /// Searches iTunes for an album or track and downloads its artwork bytes.
    /// </summary>
    public static Task<byte[]?> FetchAlbumCoverFromItunesAsync(string artist, string album) =>
        FetchAlbumCoverFromItunesAsync(artist, album, album);

    public static async Task<byte[]?> FetchAlbumCoverFromItunesAsync(string artist, string? album, string title)
    {
        try
        {
            if (!IsInvalidSearchValue(album, "Неизвестный"))
            {
                byte[]? albumResults = await SearchItunesArtworkAsync(
                    $"{artist} {album}", artist, album!, "album", "collectionName");
                if (albumResults != null)
                    return albumResults;
            }

            return await SearchItunesArtworkAsync(
                $"{artist} {title}", artist, title, "song", "trackName");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iTunes API Error: {ex.Message}");
            App.LogException(ex, "FetchAlbumCoverFromItunesAsync");
            return null;
        }
    }

    private static async Task<string?> GetDeezerCoverUrlAsync(string artist, string album)
    {
        try
        {
            string cleanArtist = CleanBracketsRegex().Replace(artist, string.Empty).Trim();
            string cleanAlbum = !string.IsNullOrWhiteSpace(album)
                ? CleanBracketsRegex().Replace(album, string.Empty).Trim()
                : string.Empty;

            string searchQuery = string.IsNullOrEmpty(cleanAlbum)
                ? cleanArtist
                : $"{cleanArtist} {cleanAlbum}";
            string url = $"https://api.deezer.com/search?q={Uri.EscapeDataString(searchQuery)}&limit=1";

            string json = await _coverUrlClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataProp) && dataProp.GetArrayLength() > 0)
            {
                var firstTrack = dataProp[0];
                if (firstTrack.TryGetProperty("album", out var albumProp))
                {
                    if (albumProp.TryGetProperty("cover_xl", out var coverProp) &&
                        coverProp.ValueKind != JsonValueKind.Null)
                        return coverProp.GetString();

                    if (albumProp.TryGetProperty("cover_big", out var coverPropAlt) &&
                        coverPropAlt.ValueKind != JsonValueKind.Null)
                        return coverPropAlt.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordRPC] Deezer API error: {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> GetItunesCoverUrlAsync(string artist, string album)
    {
        try
        {
            string cleanArtist = CleanBracketsRegex().Replace(artist, string.Empty).Replace("$", "S").Trim();
            string cleanAlbum = !string.IsNullOrWhiteSpace(album)
                ? CleanBracketsRegex().Replace(album, string.Empty).Trim()
                : string.Empty;

            string query = Uri.EscapeDataString($"{cleanArtist} {cleanAlbum}");
            string url = $"https://itunes.apple.com/search?term={query}&entity=album&limit=1";
            string json = await _coverUrlClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (root.GetProperty("resultCount").GetInt32() > 0)
            {
                var result = root.GetProperty("results")[0];
                if (result.TryGetProperty("artworkUrl100", out var artworkProp))
                    return artworkProp.GetString()?.Replace("100x100bb.jpg", "600x600bb.jpg");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscordRPC] iTunes API error: {ex.Message}");
        }

        return null;
    }

    private static async Task<byte[]?> SearchItunesArtworkAsync(
        string term,
        string artist,
        string expectedTitle,
        string entity,
        string titleProperty)
    {
        string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&entity={entity}&limit=10";
        using var response = await _coverImageClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            return null;

        string normalizedArtist = NormalizeSearchText(artist);
        string normalizedTitle = NormalizeSearchText(expectedTitle);
        var candidates = results.EnumerateArray()
            .OrderByDescending(result =>
            {
                string resultArtist = result.TryGetProperty("artistName", out var artistProp)
                    ? NormalizeSearchText(artistProp.GetString())
                    : string.Empty;
                string resultTitle = result.TryGetProperty(titleProperty, out var titleProp)
                    ? NormalizeSearchText(titleProp.GetString())
                    : string.Empty;
                return (resultArtist == normalizedArtist ? 2 : 0) +
                       (resultTitle == normalizedTitle ? 1 : 0);
            });

        foreach (var result in candidates)
        {
            if (!result.TryGetProperty("artworkUrl100", out var artworkProp))
                continue;

            string? artworkUrl = artworkProp.GetString();
            if (string.IsNullOrWhiteSpace(artworkUrl))
                continue;

            string highResolutionUrl = artworkUrl.Replace(
                "100x100bb", "600x600bb", StringComparison.OrdinalIgnoreCase);
            using var imageResponse = await _coverImageClient.GetAsync(highResolutionUrl);
            if (!imageResponse.IsSuccessStatusCode)
                continue;

            byte[] imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
            if (imageBytes.Length > 0)
                return imageBytes;
        }

        return null;
    }

    private static bool IsInvalidSearchValue(string? value, string localizedUnknown) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals(localizedUnknown, StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSearchText(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    [GeneratedRegex(@"\s*\(.*?\)")]
    private static partial Regex CleanBracketsRegex();
}