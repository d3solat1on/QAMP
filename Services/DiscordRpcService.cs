using System.Diagnostics;
using DiscordRPC;
using QAMP.Models;

namespace QAMP.Services
{
    public partial class DiscordRpcService
    {
        private static DiscordRpcClient? _client;
        private static readonly Lock _coverCacheLock = new();
        private static readonly Dictionary<string, string> _coverCache = [with(StringComparer.OrdinalIgnoreCase)];
        private static readonly Dictionary<string, Task<string?>> _coverLookups = [with(StringComparer.OrdinalIgnoreCase)];
        private static long _presenceUpdateVersion;

        public static void Initialize(string clientId)
        {
            if (_client != null)
                return;

            _client = new DiscordRpcClient(clientId);

            _client.Initialize();
        }

        public static void UpdatePresence(string title, string artist, string album, bool isPlaying, TimeSpan duration, TimeSpan position)
        {
            if (SettingsManager.Instance.Config.EnableDiscordRpc == false)
            {
                ClearPresence();
                return;
            }
            if (_client == null || !_client.IsInitialized)
                return;

            long updateVersion = Interlocked.Increment(ref _presenceUpdateVersion);
            string? cachedCoverUrl = GetCachedAlbumCoverUrl(artist, album);
            try
            {
                SetPresence(title, album, isPlaying, duration, position, cachedCoverUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error SetPresence: {ex.Message}");
                App.LogException(ex, "SetPresence");
            }
            _ = UpdatePresenceArtworkAsync(
                title, artist, album, isPlaying, duration, position, cachedCoverUrl, updateVersion);
        }

        private static async Task UpdatePresenceArtworkAsync(
            string title,
            string artist,
            string album,
            bool isPlaying,
            TimeSpan duration,
            TimeSpan position,
            string? currentCoverUrl,
            long updateVersion)
        {
            try
            {
                string? coverUrl = await GetCachedAlbumCoverUrlAsync(artist, album);
                if (updateVersion != Interlocked.Read(ref _presenceUpdateVersion) ||
                    !SettingsManager.Instance.Config.EnableDiscordRpc ||
                    _client == null || !_client.IsInitialized ||
                    string.Equals(coverUrl, currentCoverUrl, StringComparison.Ordinal))
                {
                    return;
                }

                SetPresence(title, album, isPlaying, duration, position, coverUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error UpdatePresenceArtworkAsync: {ex.Message}");
                App.LogException(ex, "UpdatePresenceArtworkAsync");
            }
        }

        private static void SetPresence(
            string title,
            string album,
            bool isPlaying,
            TimeSpan duration,
            TimeSpan position,
            string? coverUrl)
        {
            if (_client?.IsInitialized != true)
                return;

            position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, Math.Max(0, duration.Ticks)));
            Timestamps? timestamps = null;
            string state = "Paused";
            if (isPlaying)
            {
                if (duration > TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    timestamps = new Timestamps
                    {
                        Start = now - position,
                        End = now - position + duration
                    };
                }
                state = "Playing";
            }
            else if (duration > TimeSpan.Zero)
            {
                state = $"Paused • {FormatTime(position)} / {FormatTime(duration)}";
            }

            _client.SetPresence(new RichPresence
            {
                Type = ActivityType.Listening,
                Details = string.IsNullOrEmpty(title) ? "Unknown Track" : title,
                State = state,
                Timestamps = timestamps,
                Assets = new Assets
                {
                    LargeImageKey = !string.IsNullOrEmpty(coverUrl) ? coverUrl : "qamp_logo",
                    LargeImageText = album,
                }
            });
        }

        private static string? GetCachedAlbumCoverUrl(string artist, string album)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(album))
                return null;

            string key = $"{artist.Trim()}\0{album.Trim()}";
            lock (_coverCacheLock)
            {
                return _coverCache.TryGetValue(key, out string? cachedUrl) ? cachedUrl : null;
            }
        }

        private static string FormatTime(TimeSpan time) =>
            time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

        private static async Task<string?> GetCachedAlbumCoverUrlAsync(string artist, string album)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(album))
                return null;

            string key = $"{artist.Trim()}\0{album.Trim()}";
            Task<string?> lookup;
            lock (_coverCacheLock)
            {
                if (_coverCache.TryGetValue(key, out string? cachedUrl))
                    return cachedUrl;

                if (_coverLookups.TryGetValue(key, out Task<string?>? pendingLookup))
                {
                    lookup = pendingLookup;
                }
                else
                {
                    lookup = CoverSearch.GetAlbumCoverUrlAsync(artist, album);
                    _coverLookups.Add(key, lookup);
                }
            }

            string? coverUrl = await lookup;
            lock (_coverCacheLock)
            {
                if (_coverLookups.TryGetValue(key, out Task<string?>? pendingLookup) &&
                    ReferenceEquals(pendingLookup, lookup))
                {
                    _coverLookups.Remove(key);
                }
                if (!string.IsNullOrEmpty(coverUrl))
                    _coverCache[key] = coverUrl;
            }

            return coverUrl;
        }

        public static void ClearPresence()
        {
            Interlocked.Increment(ref _presenceUpdateVersion);
            try
            {
                if (_client?.IsInitialized == true)
                    _client.ClearPresence();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ClearPresence: {ex.Message}");
                App.LogException(ex, "ClearPresence");
            }
        }

        public static void Shutdown()
        {
            ClearPresence();
            _client?.Dispose();
            _client = null;
        }
    }
}