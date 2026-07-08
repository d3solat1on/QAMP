using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using QAMP.Services;

namespace QAMP.Models;

public class AppSettings : INotifyPropertyChanged
{
    public AppSettings()
    {
        if (Hotkeys == null || Hotkeys.Count == 0) InitializeDefaultHotkeys();
    }
    public bool CloseToTray { get; set; } = true;
    public bool IsVisualizerEnabled { get; set; } = true;
    public int VisualizerBarCount { get; set; } = 64; 
    public string ColorScheme { get; set; } = "Dark"; 
    public string AccentColor { get; set; } = "#1db954";
    public int CurrentRound { get; set; } = 0;
    public double[] EqualizerGains { get; set; } = new double[10]; 
    public double[] CurrentEqualizerValues { get; set; } = new double[10];
    public string EqualizerPreset { get; set; } = "Пользовательский";
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
    public int OutputDeviceId { get; set; } = -1;
    public string OutputDeviceName { get; set; } = string.Empty;
    private bool _isCompactMode = true;
    public bool UseAdaptiveGradients { get; set; } = true;
    public bool EnableCoverCache { get; set; } = true;
    public bool IsAutoLaunchEnabled { get; set; } = false; 
    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            _isCompactMode = value;
            OnPropertyChanged(nameof(IsCompactMode));
        }
    }

    public string? CustomBackgroundPath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(CustomBackgroundPath));
        }
    }
    private bool _useCustomBackground;
    public bool UseCustomBackground
    {
        get => _useCustomBackground;
        set
        {
            if (_useCustomBackground != value)
            {
                _useCustomBackground = value;
                OnPropertyChanged(nameof(UseCustomBackground));
                OnPropertyChanged(nameof(CustomBackgroundPath));
            }
        }
    }
    public PlaylistSortOrder CurrentPlaylistSort { get; set; } = PlaylistSortOrder.Manual;
    public string Language { get; set; } = "eng";
    public List<HotkeyItem> Hotkeys { get; set; } = [];
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
            new HotkeyItem(HotkeyAction.ToggleFavorite, Key.F, ModifierKeys.Control)
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public class SettingsManager
{
    private static SettingsManager? _instance;
    public static SettingsManager Instance => _instance ??= new SettingsManager();

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
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(Config, options);
        File.WriteAllText(_path, json);
    }
}