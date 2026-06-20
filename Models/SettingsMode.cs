using System.ComponentModel;
using System.IO;
using System.Text.Json;
using QAMP.Services;

namespace QAMP.Models;

public class AppSettings : INotifyPropertyChanged
{
    public bool CloseToTray { get; set; } = true;
    public bool IsVisualizerEnabled { get; set; } = true;
    public int VisualizerBarCount { get; set; } = 64; // Количество столбцов спектрограммы
    public string ColorScheme { get; set; } = "Dark"; // "Dark", "Light", "Custom"
    public string AccentColor { get; set; } = "#1db954"; // Главный цвет приложения
    public int CurrentRound { get; set; } = 0;
    public double[] EqualizerGains { get; set; } = new double[10]; // Значения эквалайзера
    public double[] CurrentEqualizerValues { get; set; } = new double[10];
    public string EqualizerPreset { get; set; } = "Пользовательский"; // Текущий выбранный режим
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
    public bool IsAutoLaunchEnabled { get; set; } = false; // Автозапуск приложения
    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            _isCompactMode = value;
            OnPropertyChanged(nameof(IsCompactMode));
        }
    }
    private string? _customBackgroundPath;
    public string CustomBackgroundPath
    {
        get => _customBackgroundPath;
        set
        {
            _customBackgroundPath = value;
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
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public class SettingsManager
{
    private static SettingsManager? _instance;
    public static SettingsManager Instance => _instance ??= new SettingsManager();

    private readonly string _path = AppDataManager.SettingsPath;
    public AppSettings Config { get; set; } = new AppSettings();

    public SettingsManager()
    {
        // Гарантируем существование папки AppData
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
        string json = JsonSerializer.Serialize(Config);
        File.WriteAllText(_path, json);
    }
}