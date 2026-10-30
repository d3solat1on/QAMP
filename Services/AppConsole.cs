using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using QAMP.Models;
using QAMP.ViewModels;

namespace QAMP.Services;

public class AppConsole
{
    public static event Action<string>? LogAdded;
    public static event Action? OnClearRequested;

    /// <summary>
    /// Логирование сообщений в консоль приложения
    /// </summary>
    /// <param name="message">Сообщение для логирования</param>
    public static void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string formattedMessage = $"[{timestamp}] {message}";


        LogAdded?.Invoke(formattedMessage);
    }

    public static async Task InputCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string[] tokens = input.Trim().Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
        string command = tokens[0].ToLowerInvariant();
        string args = tokens.Length > 1 ? tokens[1].Trim() : string.Empty;
        string value = args.Trim().Trim('"');

        var player = PlayerService.Instance;

        switch (command)
        {
            case "help":
                Log("""
        Commands: 
         help
         status
         play [track]
         pause
         stop
         next
         prev
         seek <time|+/-seconds>
         volume <0-100>
         mute
         unmute
         toggleunmute
         shuffle <on|off>
         repeat <off|all|one>
         queue
         search <text>
         playlists
         playlist <name>
         devices
         device <id>
         readconfig
         changefontsize
         changefontsizedatagrid
         createplaylist <title>
         trackinfo
         exit
         quit
         clear
         clearcache
         changeformatlibrary
         changeaccentcolor <#HEX>
         lyricsmode
         changeround <0-10>
         background
         getfonts
         setfont
         settheme
         getthemes
         addbg
         setbg <path to img, with extension>
         addfont
        """);
                break;


            case "volume":
                if (int.TryParse(args, out int volumePercent) && volumePercent >= 0 && volumePercent <= 100)
                {
                    player.Volume = volumePercent / 100.0;
                    Log($"Volume: {volumePercent}%");
                }
                else
                {
                    Log("Usage: volume <0-100> (example: volume 50)");
                }
                break;

            case "play":
                if (string.IsNullOrWhiteSpace(value))
                {
                    player.Resume();
                    Log("Playback started");
                }
                else
                {
                    var match = (await FindTracksAsync(value)).FirstOrDefault();
                    if (match.Track == null)
                    {
                        Log($"No track found matching: {value}");
                        break;
                    }

                    MusicLibrary.Instance.PlayTrackFromPlaylist(match.Track, match.Playlist);
                    Log($"Playing: {match.Track.Name} - {match.Track.Executor}");
                }
                break;

            case "pause":
                await player.PauseAsync();
                Log("Playback paused");
                break;

            case "stop":
                player.Stop();
                Log("Playback stopped");
                break;

            case "next":
                player.PlayNextTrack();
                Log("Skipped to next track");
                break;

            case "prev":
            case "previous":
                player.PlayPreviousTrack();
                Log("Skipped to previous track");
                break;

            case "status":
                if (player.CurrentTrack == null)
                {
                    Log("No track is loaded.");
                    break;
                }

                Log($"Track: {player.CurrentTrack.Name} - {player.CurrentTrack.Executor}");
                Log($"Playback: {(player.IsPlaying ? "playing" : "paused")} | Position: {FormatTime(player.Position)} / {FormatTime(player.Duration)}");
                Log($"Volume: {player.Volume * 100:0}% | Shuffle: {(player.IsShuffleEnabled ? "on" : "off")} | Repeat: {FormatRepeat(player.RepeatMode)}");
                break;

            case "seek":
                if (TryParseSeek(value, out double seekSeconds))
                {
                    if (value.StartsWith('+') || value.StartsWith('-'))
                    {
                        player.SeekRelative(seekSeconds);
                    }
                    else
                    {
                        player.Seek(seekSeconds);
                    }
                    Log($"Position: {FormatTime(player.Position)}");
                }
                else
                {
                    Log("Usage: seek <seconds|mm:ss|+/-seconds> (example: seek 1:25 or seek +10)");
                }
                break;

            case "shuffle":
                if (TryParseToggle(value, out bool shuffleEnabled))
                {
                    player.IsShuffleEnabled = shuffleEnabled;
                    if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RebuildPlaybackQueue();
                    }
                    Log($"Shuffle: {(shuffleEnabled ? "on" : "off")}");
                }
                else
                {
                    Log("Usage: shuffle <on|off>");
                }
                break;

            case "repeat":
                switch (value.ToLowerInvariant())
                {
                    case "off":
                    case "none":
                        player.RepeatMode = RepeatMode.NoRepeat;
                        break;
                    case "all":
                        player.RepeatMode = RepeatMode.RepeatAll;
                        break;
                    case "one":
                        player.RepeatMode = RepeatMode.RepeatOne;
                        break;
                    default:
                        Log("Usage: repeat <off|all|one>");
                        break;
                }
                if (value.Equals("off", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("none", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("all", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("one", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Repeat: {FormatRepeat(player.RepeatMode)}");
                }
                break;

            case "queue":
                LogQueue(player);
                break;

            case "search":
                if (string.IsNullOrWhiteSpace(value))
                {
                    Log("Usage: search <text>");
                    break;
                }

                var matches = await FindTracksAsync(value);
                if (matches.Count == 0)
                {
                    Log($"No tracks found matching: {value}");
                    break;
                }

                Log($"Found {matches.Count} track(s):");
                foreach (var match in matches.Take(20))
                {
                    Log($"{match.Track.Name} - {match.Track.Executor} [{match.Playlist.Name}]");
                }
                if (matches.Count > 20) Log($"...and {matches.Count - 20} more.");
                break;

            case "playlists":
                await EnsurePlaylistsLoadedAsync();
                if (MusicLibrary.Instance.Playlists.Count == 0)
                {
                    Log("No playlists found.");
                    break;
                }
                foreach (var playlist in MusicLibrary.Instance.Playlists)
                {
                    Log($"[{playlist.Id}] {playlist.Name}");
                }
                break;

            case "playlist":
                if (string.IsNullOrWhiteSpace(value))
                {
                    Log("Usage: playlist <name>");
                    break;
                }

                await EnsurePlaylistsLoadedAsync();
                var matchingPlaylist = MusicLibrary.Instance.Playlists.FirstOrDefault(
                    playlist => string.Equals(playlist.Name, value, StringComparison.OrdinalIgnoreCase))
                    ?? MusicLibrary.Instance.Playlists.FirstOrDefault(
                        playlist => playlist.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
                if (matchingPlaylist == null)
                {
                    Log($"Playlist not found: {value}");
                    break;
                }

                await MusicLibrary.Instance.LoadPlaylistTracksAsync(matchingPlaylist);
                if (matchingPlaylist.Tracks.Count == 0)
                {
                    Log($"Playlist is empty: {matchingPlaylist.Name}");
                    break;
                }

                MusicLibrary.Instance.PlayTrackFromPlaylist(matchingPlaylist.Tracks[0], matchingPlaylist);
                Log($"Playing playlist: {matchingPlaylist.Name} ({matchingPlaylist.Tracks.Count} tracks)");
                break;

            case "clear":
                OnClearRequested?.Invoke();
                break;

            case "devices":
            case "getoutputdevices":
                var devices = PlayerService.GetOutputDevices();
                if (devices.Count == 0)
                {
                    Log("No audio devices found.");
                    break;
                }

                Log("Available audio devices:");
                foreach (var device in devices)
                {
                    Log($"[{device.Id}] {device.Name}");
                }
                break;

            case "device":
            case "setoutputdevice":
                if (!int.TryParse(value, out int deviceId))
                {
                    Log("Usage: device <id> (use 'devices' to list available devices)");
                    break;
                }

                var allDevices = PlayerService.GetOutputDevices();
                var selectedDevice = allDevices.FirstOrDefault(device => device.Id == deviceId);
                if (selectedDevice == null)
                {
                    Log($"Audio device with ID {deviceId} does not exist. Use 'devices' to list available devices.");
                    break;
                }

                player.SetOutputDevice(deviceId);
                Log($"Audio output successfully changed to: {selectedDevice.Name}");
                break;
            case "readconfig":
                _ = ReadConfigAsync();
                break;
            case "changefontsize":
                if (double.TryParse(args, out double fontSize))
                {
                    ChangeFontSize(fontSize);
                }
                break;
            case "changefontsizedatagrid":
                if (double.TryParse(args, out double fontsizedatagrid))
                {
                    ChangeFontSizeDataGrid(fontsizedatagrid);
                }
                break;
            case "createplaylist":
                CreatePlaylist(args);
                break;
            case "trackinfo":
                var track = player.CurrentTrack;
                Trackinfo(track);
                break;
            case "exit":
            case "quit":
                Log("Shutting down application...");
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                break;
            case "mute":
            case "unmute":
            case "togglemute":
                player.ToggleMute();
                Log($"Volume: {player.Volume * 100:0}%");
                break;
            case "clearcache":
                CoverImageCacheService.ClearDiskCache();
                Log("Disk cover cache cleared successfully.");
                break;
            case "changeformatlibrary":
                ChangeFormatLibrary();
                break;
            case "changeaccentcolor":
                ChangeAccentColor(args);
                break;
            case "lyricsmode":
                if (Application.Current?.MainWindow is MainWindow mainWindowLyrics)
                {
                    mainWindowLyrics.UpdateFromConsole();
                }
                break;
            case "changeround":
                ChangeCurrentRound(args);
                break;
            case "bg":
            case "background":
                CheckUseCustomBG();
                break;
            case "setbg":
                SetBG(args);
                break;
            case "getfonts":
                GetFonts();
                break;
            case "setfont":
                SetFont(args);
                break;
            case "getthemes":
                GetThemes();
                break;
            case "settheme":
                SetTheme(args);
                break;
            case "addfont":
                AddFont(args);
                break;
            case "addtheme":
                AddTheme(args);
                break;
            case "addbg":
                AddImgForBG(args);
                break;
#if DEBUG
            case "test":
                FreeRam();
                break;
#endif
            default:
                Log($"Unknown command: {tokens[0]}. Type 'help' for available commands.");
                break;
        }
    }

    private static void SetBG(string filePath)
    {
        var config = SettingsManager.Instance.Config;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
        {
            Debug.WriteLine("Error: Only image files (.jpg, .jpeg, .png, .webp) are allowed for background.");
            Log("Error: Only image files (.jpg, .jpeg, .png, .webp) are allowed for background.");
            return;
        }
        config.CustomBackgroundPath = filePath;
    }
    private static void AddImgForBG(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine("Error: File path is invalid or file does not exist.");
            return;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
        {
            Debug.WriteLine("Error: Only image files (.jpg, .jpeg, .png, .webp) are allowed for background.");
            Log("Error: Only image files (.jpg, .jpeg, .png, .webp) are allowed for background.");
            return;
        }

        try
        {
            Directory.CreateDirectory(AppDataManager.BGFolderPath);

            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(AppDataManager.BGFolderPath, fileName);

            File.Copy(filePath, destPath, overwrite: true);
            Debug.WriteLine($"Background image added successfully: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error AddImgForBG: {ex.Message}");
        }
    }

    private static void AddTheme(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine("Error: File path is invalid or file does not exist.");
            Log("Error: File path is invalid or file does not exist.");
            return;
        }

        if (!string.Equals(Path.GetExtension(filePath), ".xaml", StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine("Error: Only .xaml files are allowed for themes.");
            Log("Error: Only .xaml files are allowed for themes.");
            return;
        }

        if (!ThemeManager.IsThemeValid(filePath, out string errorMessage))
        {
            Debug.WriteLine($"Error: Theme is invalid. {errorMessage}");
            return;
        }

        try
        {
            Directory.CreateDirectory(AppDataManager.ThemesFolderPath);

            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(AppDataManager.ThemesFolderPath, fileName);

            File.Copy(filePath, destPath, overwrite: true);
            Debug.WriteLine($"Theme added successfully: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error AddTheme: {ex.Message}");
        }
    }

    private static void AddFont(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine("Error: File path is invalid or file does not exist.");
            return;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".ttf" && ext != ".otf")
        {
            Debug.WriteLine("Error: Only font files (.ttf, .otf) are allowed.");
            return;
        }

        try
        {
            var fontService = FontService.Instance;
            var imported = fontService.ImportFont(filePath);
            if (imported != null)
            {
                Debug.WriteLine($"Font added successfully: {imported.DisplayName}");
            }
            else
            {
                Debug.WriteLine("Error: Failed to import font file.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error AddFont: {ex.Message}");
        }
    }

    private static void SetTheme(string titleTheme)
    {
        bool success = ThemeManager.ApplyTheme(titleTheme);

        if (!success)
        {
            Log("Usage: settheme LightTheme.xaml");
        }
    }
    private static void GetThemes()
    {
        string[] files = Directory.GetFiles(AppDataManager.ThemesFolderPath);
        foreach (string title in files)
        {
            Log(title);
        }
    }
    private static void SetFont(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            Log("Error: Font name cannot be empty.");
            return;
        }

        var fontService = FontService.Instance;
        var selectedFont = fontService.FindFont(fontName.Trim());

        if (selectedFont is null)
        {
            Log($"Error: Font '{fontName}' not found. Use 'getfonts' to see available fonts.");
            return;
        }

        var config = SettingsManager.Instance.Config;
        config.SelectedFontName = selectedFont.DisplayName;

        FontService.ApplyFontToApplication(selectedFont.Family);

        SettingsManager.Instance.Save();
        Log($"Font successfully changed to: {selectedFont.DisplayName}");
    }
    private static void GetFonts()
    {
        var fontService = FontService.Instance;
        foreach (var fontOption in fontService.AvailableFonts)
        {
            Log($"{fontOption}");
        }
    }
    private static void CheckUseCustomBG()
    {
        var config = SettingsManager.Instance.Config;
        if (config.UseCustomBackground == true)
        {
            config.UseCustomBackground = false;
        }
        else
        {
            config.UseCustomBackground = true;
        }
    }
    private static void ChangeCurrentRound(string args)
    {
        var config = SettingsManager.Instance.Config;
        if (string.IsNullOrWhiteSpace(args)) return;
        if (int.TryParse(args, out int parsedRound))
        {
            if (parsedRound >= 0 && parsedRound <= 10)
            {
                config.CurrentRound = parsedRound;
                SettingsManager.Instance.Save();
                Application.Current.Resources["AppCornerRadius"] = new CornerRadius(parsedRound);
            }
            else
            {
                Log("Usage: changeround <0-10>");
            }
        }

    }
    private static void ChangeAccentColor(string color)
    {
        var config = SettingsManager.Instance.Config;
        Color parsedColor;

        try
        {
            parsedColor = (Color)ColorConverter.ConvertFromString(color);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            Log("Usage: #color (HEX)");
        }
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(parsedColor.GetType());

        config.AccentColor = color;

        string? result = converter.ConvertToString(parsedColor);
        if (result != null)
        {
            ThemeManager.UpdateAccentColor(result);
        }
        if (Application.Current.MainWindow is MainWindow mainWin)
        {
            mainWin.UpdateIcons();
        }
        PlayerService.Instance.RefreshSpectrumControls();
    }
    private static void ChangeFormatLibrary()
    {
        var config = SettingsManager.Instance.Config;
        if (config.IsCompactMode == true)
        {
            config.IsCompactMode = false;
        }
        else
        {
            config.IsCompactMode = true;
        }
    }
#if DEBUG
    private static void FreeRam()
    {
        Log($"Memory usage before cleaning: {MemoryProfiler.GetMemoryUsage()}");
        MemoryOptimizer.FreeRam();
        Log($"Memory usage after cleaning: {MemoryProfiler.GetMemoryUsage()}");
    }
#endif
    private static void Trackinfo(Track? track)
    {
        if (track == null)
        {
            Log("No track is currently loaded.");
            return;
        }

        Log($"  Track Information for {track.Name}");
        Log($"  Title: {track.Name}");
        Log($"  Artist: {track.Executor}");
        Log($"  Album: {track.Album}");
        Log($"  Genre: {track.Genre ?? "N/A"}");
        Log($"  Year: {track.Year}");
        Log($"  Composer: {track.Composer ?? "N/A"}");
        Log($"  Duration: {track.Duration}s");
        Log($"  Bitrate: {track.Bitrate} kbps");
        Log($"  Sample Rate: {track.SampleRate} Hz");
        Log($"  Channels: {track.Channels}");
        Log($"  Path: {track.Path}");
    }

    private static void CreatePlaylist(string title)
    {
        long newPlaylist = DatabaseService.CreatePlaylist(title);
        var newPlaylistId = DatabaseService.GetPlaylistById((int)newPlaylist);

        if (newPlaylistId != null)
        {
            Windows.Statistics.RefreshRightPanel(newPlaylistId);
        }
    }

    private static void ChangeFontSizeDataGrid(double size)
    {
        var config = SettingsManager.Instance.Config;
        config.FontSizeDataGrid = size;
        SettingsManager.Instance.Save();
        Application.Current.Resources["FontSizeDataGrid"] = size;
        Log($"New FontSizeDataGrid = {size}");
    }
    private static void ChangeFontSize(double size)
    {
        var config = SettingsManager.Instance.Config;
        Application.Current.Resources["FontSize"] = size;
        config.FontSize = size;
        SettingsManager.Instance.Save();
        Log($"New FontSize = {size}");
    }

    private static async Task ReadConfigAsync()
    {
        string path = AppDataManager.SettingsPath;


        if (!System.IO.File.Exists(path))
        {
            Log("Path is empty.");
            return;
        }

        using System.IO.FileStream openStream = System.IO.File.OpenRead(path);
        AppSettings? config = await JsonSerializer.DeserializeAsync<AppSettings>(openStream);

        if (config == null)
        {
            Log("Config is null.");
            return;
        }


        PropertyInfo[] properties = typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            string name = property.Name;

            object? value = property.GetValue(config);

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                var items = new System.Collections.Generic.List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(item?.ToString() ?? "null");
                }
                Log($"{name}: [{string.Join(", ", items)}]");
            }
            else
            {
                Log($"{name}: {value ?? "null"}");
            }
        }
    }

    private static async Task EnsurePlaylistsLoadedAsync()
    {
        if (MusicLibrary.Instance.Playlists.Count == 0)
        {
            await MusicLibrary.Instance.RefreshPlaylistsAsync();
        }
    }

    private static async Task<List<(Playlist Playlist, Track Track)>> FindTracksAsync(string query)
    {
        await EnsurePlaylistsLoadedAsync();

        var matches = new List<(Playlist Playlist, Track Track)>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var playlist in MusicLibrary.Instance.Playlists)
        {
            await MusicLibrary.Instance.LoadPlaylistTracksAsync(playlist);
            foreach (var track in playlist.Tracks)
            {
                if (seenPaths.Add(track.Path)
                    && (track.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || track.Executor.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || track.Album.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    matches.Add((playlist, track));
                }
            }
        }

        return matches;
    }

    private static void LogQueue(PlayerService player)
    {
        var queue = player.IsShuffleEnabled ? player.ShuffledQueue : player._actualPlayingQueue;
        if (queue.Count == 0)
        {
            Log("The playback queue is empty.");
            return;
        }

        int currentIndex = player.CurrentTrack == null
            ? -1
            : queue.FindIndex(track => track.Path == player.CurrentTrack.Path);
        Log($"Queue ({queue.Count} tracks):");
        for (int index = 0; index < queue.Count; index++)
        {
            string marker = index == currentIndex ? ">" : " ";
            Log($"{marker} {index + 1}. {queue[index].Name} - {queue[index].Executor}");
        }
    }

    private static bool TryParseSeek(string value, out double seconds)
    {
        seconds = 0;
        bool relative = value.StartsWith('+') || value.StartsWith('-');
        string numericValue = relative ? value[1..] : value;
        if (numericValue.Contains(':'))
        {
            string[] parts = numericValue.Split(':');
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out double minutes)
                || !double.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double remainingSeconds)
                || remainingSeconds >= 60)
            {
                return false;
            }

            seconds = (minutes * 60) + remainingSeconds;
            if (value.StartsWith('-')) seconds = -seconds;
            return true;
        }

        if (!double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out seconds))
        {
            return false;
        }

        return !relative || seconds != 0 || value is "+0" or "-0";
    }

    private static bool TryParseToggle(string value, out bool enabled)
    {
        switch (value.ToLowerInvariant())
        {
            case "on":
            case "true":
                enabled = true;
                return true;
            case "off":
            case "false":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    private static string FormatRepeat(RepeatMode repeatMode) => repeatMode switch
    {
        RepeatMode.RepeatAll => "all",
        RepeatMode.RepeatOne => "one",
        _ => "off"
    };
}
