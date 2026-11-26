using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using QAMP.Services;

namespace QAMP.Models;

public class AppSettings : INotifyPropertyChanged
{
    private const int EqualizerBandCount = 10;
    private bool _isCompactMode = true;
    private string? _customBackgroundPath;
    private string? _customBackgroundUpDownPath;
    private bool _useCustomBackgroundUpDown;
    private bool _useCustomBackground;
    public AppSettings()
    {
        if (Hotkeys == null || Hotkeys.Count == 0)
            InitializeDefaultHotkeys();
    }

    public bool EnableDiscordRpc { get; set; } = false;
    public bool CloseToTray { get; set; } = true;
    public int SpectrumFps { get; set; } = 60;
    public bool IsVisualizerEnabled { get; set; } = true;
    public int VisualizerBarCount { get; set; } = 64;
    public bool UseSpectrumGradient { get; set; } = false;
    public string SpectrumGradientStartColor { get; set; } = "#0f172a";
    public string SpectrumGradientEndColor { get; set; } = "#4338ca";
    public Visualization.SpectrumDisplayType SpectrumType { get; set; } = Visualization.SpectrumDisplayType.Bars;
    public Visualization.SpectrumGradientType GradientType { get; set; } = Visualization.SpectrumGradientType.Horizontal;
    public string ColorScheme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#1db954";
    public string TextBrush { get; set; } = "#FFFFFF";
    public int CurrentRound { get; set; } = 0;
    public double[] EqualizerGains { get; set; } = new double[EqualizerBandCount];
    public double[] CustomEqualizerGains { get; set; } = new double[EqualizerBandCount];
    public string EqualizerPreset { get; set; } = "Default";
    public bool ReverbEnabled { get; set; } = false;
    public double ReverbLevel { get; set; } = 50.0;
    public bool EchoEnabled { get; set; } = false;
    public double EchoDelay { get; set; } = 300.0;
    public bool VocalEnhancementEnabled { get; set; } = false;
    public bool LoudnessEnabled { get; set; } = false;
    public bool CompressorEnabled { get; set; } = false;
    public double CompressorThreshold { get; set; } = 0.5;
    public double Balance { get; set; } = 0.0;
    public double Tempo { get; set; } = 1.0;
    public double Pitch { get; set; } = 1.0;
    public bool UseConsole { get; set; } = false;
    public int OutputDeviceId { get; set; } = -1;
    public string OutputDeviceName { get; set; } = string.Empty;
    public bool CrossfadeEnabled { get; set; } = true;
    public double CrossfadeDuration { get; set; } = 3.0;
    public bool UseWASAPI { get; set; } = false;
    public string SelectedFontName { get; set; } = "GOST type A [GOSTA]";
    public double FontSize { get; set; } = 16.0;
    public double FontSizeDataGrid { get; set; } = 18.0;
    public bool UseAdaptiveGradients { get; set; } = true;
    public bool EnableCoverCache { get; set; } = true;
    public bool IsAutoLaunchEnabled { get; set; } = false;
    public bool IsControlPanelOnTop { get; set; } = false;
    public bool IsNowPlayingOnLeft { get; set; } = false;
    public bool IsNowPlayingIsVisible { get; set; } = true;
    public bool? ShowTrackCoverColumn { get; set; }
    public bool? ShowTrackTitleColumn { get; set; }
    public bool? ShowTrackArtistColumn { get; set; }
    public bool? ShowTrackAlbumColumn { get; set; }
    public bool? ShowTrackDurationColumn { get; set; }
    public bool? ShowTrackBitrateColumn { get; set; }
    public bool? ShowTrackGenreColumn { get; set; }
    public bool? ShowTrackDateColumn { get; set; }
    public PlaylistSortOrder CurrentPlaylistSort { get; set; } = PlaylistSortOrder.Manual;
    public string Language { get; set; } = "eng";
    public List<HotkeyItem> Hotkeys { get; set; } = [];

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set => SetField(ref _isCompactMode, value);
    }

    public string? CustomBackgroundPath
    {
        get => _customBackgroundPath;
        set => SetField(ref _customBackgroundPath, value);
    }

    public string? CustomBackgroundUpDownPath
    {
        get => _customBackgroundUpDownPath;
        set => SetField(ref _customBackgroundUpDownPath, value);
    }

    public bool UseCustomBackgroundUpDown
    {
        get => _useCustomBackgroundUpDown;
        set
        {
            if (SetField(ref _useCustomBackgroundUpDown, value))
                OnPropertyChanged(nameof(CustomBackgroundUpDownPath));
        }
    }

    public bool UseCustomBackground
    {
        get => _useCustomBackground;
        set
        {
            if (SetField(ref _useCustomBackground, value))
                OnPropertyChanged(nameof(CustomBackgroundPath));
        }
    }

    public void NotifyThemeResourcesChanged() =>
        OnPropertyChanged(nameof(CustomBackgroundPath));

    // public void NormalizeAfterLoad()
    // {
    //     EqualizerGains = NormalizeBandArray(EqualizerGains);
    //     CustomEqualizerGains = NormalizeBandArray(CustomEqualizerGains);

    //     if (Hotkeys.Count == 0)
    //         InitializeDefaultHotkeys();
    // }

    // private static double[] NormalizeBandArray(double[]? source)
    // {
    //     var result = new double[EqualizerBandCount];
    //     if (source is null || source.Length == 0)
    //         return result;

    //     Array.Copy(source, result, Math.Min(source.Length, EqualizerBandCount));
    //     return result;
    // }

    public void InitializeDefaultHotkeys()
    {
        Hotkeys =
        [
            new HotkeyItem(HotkeyAction.TogglePlayPause, Key.Space, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.SeekForward, Key.Right, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.SeekBackward, Key.Left, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.VolumeUp, Key.Up, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.VolumeDown, Key.Down, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.NextTrack, Key.N, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.PreviousTrack, Key.B, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ViewLyrics, Key.L, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ShowTrackInfo, Key.I, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ToggleRepeat, Key.R, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ToggleShuffle, Key.S, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.OpenFullScreenSpectrum, Key.W, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ToggleFocusGrid, Key.Tab, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.ToggleFavorite, Key.F, ModifierKeys.Control),
            new HotkeyItem(HotkeyAction.OpenConsole, Key.F12, ModifierKeys.Control)
        ];
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName!);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public class SettingsManager
{
    private static SettingsManager? _instance;
    public static SettingsManager Instance => _instance ??= new SettingsManager();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path = AppDataManager.SettingsPath;
    private AppSettings _config = new();

    public AppSettings Config
    {
        get => _config;
        set
        {
            _config = value;
            OnPropertyChanged(nameof(Config));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public SettingsManager()
    {
        AppDataManager.EnsureAppDataFolderExists();

        if (File.Exists(_path))
        {
            string json = File.ReadAllText(_path);
            Config = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        else
        {
            Config = new AppSettings();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(Config, SerializerOptions);
        File.WriteAllText(_path, json);
    }
}