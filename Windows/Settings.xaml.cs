using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using QAMP.Models;
using QAMP.Services;
using Microsoft.Win32;
using QAMP.Dialogs;

namespace QAMP.Windows
{
    public partial class Settings : Window
    {
        private bool isInitializing;
        private string? originalColorScheme;
        private string? originalAccentColor;
        private bool originalVisualizerEnabled;
        private int originalBarCount;
        private bool originalCloseToTray;
        private bool originalUseAdaptiveGradients;
        private bool originalUseSpectrumGradient;
        private Visualization.SpectrumDisplayType originalSpectrumType;
        private Visualization.SpectrumGradientType originalSpectrumGradientType;
        private bool originalIsAutoLaunchEnabled;
        private readonly PlayerService _player;
        private DispatcherTimer? _memoryTimer;
        private bool _themeChanged = false;

        public Settings(PlayerService player)
        {
            InitializeComponent();
            InitializeCustomThemes();
            _player = player;
            StartMemoryTicking();
        }

        private static string ThemesFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
        private static string BGFolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Background");


        private void InitializeCustomThemes()
        {
            var appSettings = new AppSettings();
            Debug.WriteLine("[QAMP Theme Debug] --- Инициализация пользовательских тем ---");
            DarkThemeRadio.Checked -= StandardThemeRadio_Checked;
            LightThemeRadio.Checked -= StandardThemeRadio_Checked;
            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;

            try
            {
                Debug.WriteLine($"[QAMP Theme Debug] Целевой путь к папке тем: {ThemesFolderPath}");

                if (!Directory.Exists(ThemesFolderPath))
                {
                    Debug.WriteLine("[QAMP Theme Debug] Папка Themes не найдена. Создаю директорию...");
                    Directory.CreateDirectory(ThemesFolderPath);
                }
                else
                {
                    Debug.WriteLine("[QAMP Theme Debug] Папка Themes успешно обнаружена.");
                }

                RefreshThemesList();
                DarkThemeRadio.IsChecked = false;
                LightThemeRadio.IsChecked = false;
                CustomThemesComboBox.SelectedIndex = -1;

                if (appSettings.ColorScheme != null)
                {
                    string currentTheme = appSettings.ColorScheme;
                    Debug.WriteLine($"[QAMP Theme Debug] Текущая тема из settings.json: {currentTheme}");

                    if (currentTheme == "Dark")
                    {
                        DarkThemeRadio.IsChecked = true;
                    }
                    else if (currentTheme == "Light")
                    {
                        LightThemeRadio.IsChecked = true;
                    }
                    else
                    {
                        if (CustomThemesComboBox.Items.Contains(currentTheme))
                        {
                            CustomThemesComboBox.SelectedItem = currentTheme;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[QAMP Theme Debug] КРИТИЧЕСКАЯ ОШИБКА при инициализации: {ex.Message}");
            }
            DarkThemeRadio.Checked += StandardThemeRadio_Checked;
            LightThemeRadio.Checked += StandardThemeRadio_Checked;
            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;
            Debug.WriteLine("[QAMP Theme Debug] ---------------------------------------------");
        }

        private void RefreshThemesList()
        {
            Debug.WriteLine("[QAMP Theme Debug] --- Обновление списка тем в ComboBox ---");

            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;
            CustomThemesComboBox.Items.Clear();

            if (Directory.Exists(ThemesFolderPath))
            {
                var xamlFiles = Directory.GetFiles(ThemesFolderPath, "*.xaml")
                                         .Select(Path.GetFileName)
                                         .ToList();

                foreach (var file in xamlFiles)
                {
                    CustomThemesComboBox.Items.Add(file);
                }
            }

            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;
            CustomThemesComboBox.UpdateLayout();
        }

        private static bool IsThemeValid(string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 1024 * 1024)
                {
                    errorMessage = (string)Application.Current.FindResource("LngThemeFileMAX");
                    return false;
                }

                ResourceDictionary? customDict;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    customDict = System.Windows.Markup.XamlReader.Load(fs) as ResourceDictionary;
                }

                if (customDict == null)
                {
                    errorMessage = (string)Application.Current.FindResource("LngErrorResourceDictionary");
                    return false;
                }

                var defaultThemeUri = new Uri(";component/Themes/DarkTheme.xaml", UriKind.RelativeOrAbsolute);
                var baseThemeDict = new ResourceDictionary { Source = defaultThemeUri };

                foreach (var key in baseThemeDict.Keys)
                {
                    if (!customDict.Contains(key))
                    {
                        string message = (string)Application.Current.FindResource("LngThemeMissingResource");
                        errorMessage = $"{message} '{key}'";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngXAMLReadingError");
                errorMessage = $"{message} {ex.Message}";
                return false;
            }
        }

        private async void AddThemeButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "XAML |*.xaml"

            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(selectedFilePath);
                string destFilePath = Path.Combine(ThemesFolderPath, fileName);

                if (!IsThemeValid(selectedFilePath, out string error))
                {
                    string message = (string)Application.Current.FindResource("LngFailedImportTheme");
                    NotificationWindow.Show($"{message}\n{error}", this);
                    return;
                }

                try
                {
                    File.Copy(selectedFilePath, destFilePath, overwrite: true);
                    RefreshThemesList();
                    CustomThemesComboBox.SelectedItem = fileName;

                    string message = (string)Application.Current.FindResource("LngThemeAdded");
                    await SettingsInfoToast.ShowAsync(message);
                }
                catch (Exception ex)
                {
                    string message = (string)Application.Current.FindResource("LngFailedToCopyFile");
                    NotificationWindow.Show($"{message} {ex.Message}", this);
                }
            }
        }

        private void StandardThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (CustomThemesComboBox == null) return;

            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;
            CustomThemesComboBox.SelectedIndex = -1;
            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;

            string themeName = (sender == DarkThemeRadio) ? "Dark" : "Light";

            ThemeManager.SetTheme(themeName);

            _themeChanged = true;
        }

        private void CustomThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (CustomThemesComboBox == null) return;

            if (CustomThemesComboBox.SelectedItem is string selectedThemeFile)
            {
                ThemeManager.SetTheme(selectedThemeFile);
                _themeChanged = true;
            }
        }

        private void CustomThemesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            if (CustomThemesComboBox.SelectedItem is string selectedThemeFile)
            {
                DarkThemeRadio.Checked -= StandardThemeRadio_Checked;
                LightThemeRadio.Checked -= StandardThemeRadio_Checked;

                DarkThemeRadio.IsChecked = false;
                LightThemeRadio.IsChecked = false;

                DarkThemeRadio.Checked += StandardThemeRadio_Checked;
                LightThemeRadio.Checked += StandardThemeRadio_Checked;

                ThemeManager.SetTheme(selectedThemeFile);
                _themeChanged = true;
            }
        }

        private async void ShowRestartNotificationIfNeeded()
        {
            if (_themeChanged)
            {
                string message = (string)Application.Current.FindResource("LngThemeRestartRequired") ??
                                 "Для применения темы необходимо перезапустить приложение.";
                NotificationWindow.Show(message, this);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            isInitializing = true;

            var config = SettingsManager.Instance.Config;
            originalColorScheme = config.ColorScheme;
            originalAccentColor = config.AccentColor;
            originalVisualizerEnabled = config.IsVisualizerEnabled;
            originalBarCount = config.VisualizerBarCount;
            originalCloseToTray = config.CloseToTray;
            originalUseAdaptiveGradients = config.UseAdaptiveGradients;
            originalUseSpectrumGradient = config.UseSpectrumGradient;
            originalSpectrumType = config.SpectrumType;
            originalSpectrumGradientType = config.GradientType;
            originalIsAutoLaunchEnabled = config.IsAutoLaunchEnabled;

            VisualizerEnabled.IsChecked = config.IsVisualizerEnabled;
            VisualizerDisabled.IsChecked = !config.IsVisualizerEnabled;
            SpectrumGradientStartColorTextBox.Text = config.SpectrumGradientStartColor;
            SpectrumGradientEndColorTextBox.Text = config.SpectrumGradientEndColor;

            switch (config.ColorScheme)
            {
                case "Dark":
                    DarkThemeRadio.IsChecked = true;
                    break;
                case "Light":
                    LightThemeRadio.IsChecked = true;
                    break;
                default:
                    CustomThemeRadio.IsChecked = true;
                    break;
            }

            AccentColorTextBox.Text = config.AccentColor;
            UpdateColorPreview();

            CurrentRoundTextBox.Text = config.CurrentRound.ToString();

            CloseToTrayRadio.IsChecked = config.CloseToTray;
            CloseAppRadio.IsChecked = !config.CloseToTray;

            AdaptiveGradientsRadio.IsChecked = config.UseAdaptiveGradients;
            StaticGradientsRadio.IsChecked = !config.UseAdaptiveGradients;

            AutoLaunchEnabled.IsChecked = config.IsAutoLaunchEnabled;
            AutoLaunchDisabled.IsChecked = !config.IsAutoLaunchEnabled;

            UseCustomBGradio.IsChecked = config.UseCustomBackground;
            NotUseCustomBGradio.IsChecked = !config.UseCustomBackground;

            UseCoverCache.IsChecked = config.EnableCoverCache;
            NotUseCoverCache.IsChecked = !config.EnableCoverCache;

            if (config.Language == "en")
            {
                LanguageEnRadio.IsChecked = true;
            }
            else
            {
                LanguageRuRadio.IsChecked = true;
            }

            if (config.IsCompactMode)
                CompactModeRadio.IsChecked = true;
            else
                DefaultModeRadio.IsChecked = true;

            LoadCurrentHotkeys();

            CheckAutoLaunch(null, null);

            //    ??

            GradientEnabled.IsChecked = config.UseSpectrumGradient;
            GradientDisabled.IsChecked = !config.UseSpectrumGradient;
            SpectrumBarsRadio.IsChecked = config.SpectrumType == Visualization.SpectrumDisplayType.Bars;
            SpectrumLineRadio.IsChecked = config.SpectrumType == Visualization.SpectrumDisplayType.Line;

            // GradientHeightBasedRadio.IsChecked = config.GradientType == Visualization.SpectrumGradientType.HeightBased;
            // GradientFullHeightRadio.IsChecked = config.GradientType == Visualization.SpectrumGradientType.FullHeight;
            // GradientHorizontalRadio.IsChecked = config.GradientType == Visualization.SpectrumGradientType.Horizontal;

            isInitializing = false;
        }

        private void AdaptiveGradients_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseAdaptiveGradients = AdaptiveGradientsRadio.IsChecked ?? false;
            SettingsManager.Instance.Save();
        }

        // <???>
        private void GradientToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseSpectrumGradient = GradientEnabled.IsChecked ?? false;
            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private void SpectrumType_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.SpectrumType = SpectrumLineRadio.IsChecked == true
                ? Visualization.SpectrumDisplayType.Line
                : Visualization.SpectrumDisplayType.Bars;
            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private void SpectrumGradientTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.SpectrumGradientStartColor = SpectrumGradientStartColorTextBox.Text.Trim();
            config.SpectrumGradientEndColor = SpectrumGradientEndColorTextBox.Text.Trim();

            bool isValidStart = IsHexColorValid(config.SpectrumGradientStartColor);
            bool isValidEnd = IsHexColorValid(config.SpectrumGradientEndColor);

            if (!isValidStart || !isValidEnd)
            {
                return;
            }

            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        // private void GradientType_Checked(object sender, RoutedEventArgs e)
        // {
        //     if (isInitializing) return;

        //     var config = SettingsManager.Instance.Config;

        //     // if (GradientHeightBasedRadio.IsChecked == true)
        //     // {
        //     //     config.GradientType = Visualization.SpectrumGradientType.HeightBased;
        //     // }
        //     // else if (GradientFullHeightRadio.IsChecked == true)
        //     // {
        //     //     config.GradientType = Visualization.SpectrumGradientType.FullHeight;
        //     // }
        //     // else if (GradientHorizontalRadio.IsChecked == true)
        //     // {
        //     config.GradientType = Visualization.SpectrumGradientType.Horizontal;
        //     // }

        //     SettingsManager.Instance.Save();
        //     PlayerService.Instance.RefreshSpectrumControls();
        // }
        private static bool IsHexColorValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (!normalized.StartsWith("#"))
            {
                normalized = $"#{normalized}";
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(normalized);
                return color.A >= 0;
            }
            catch
            {
                return false;
            }
        }

        private void LanguageRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio) return;

            string lang = radio == LanguageEnRadio ? "en" : "ru";
            var config = SettingsManager.Instance.Config;
            if (config.Language == lang) return;

            config.Language = lang;
            SettingsManager.Instance.Save();

            try
            {
                LanguageManager.ApplyLanguage(lang);
            }
            catch { }
        }

        private void Format_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is RadioButton radio && radio.IsChecked == true)
            {
                bool isCompact = radio == CompactModeRadio;
                var config = SettingsManager.Instance.Config;

                if (config.IsCompactMode != isCompact)
                {
                    config.IsCompactMode = isCompact;
                    Debug.WriteLine($"Режим отображения изменен: Компактный = {isCompact}");
                }
            }
        }

        private void CheckUseCustomBG(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is RadioButton radio && radio.IsChecked == true)
            {
                bool useCustomBG = radio == UseCustomBGradio;
                var config = SettingsManager.Instance.Config;
                if (config.UseCustomBackground != useCustomBG)
                {
                    config.UseCustomBackground = useCustomBG;
                }
            }
        }

        private void CheckUseCoverCache(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is RadioButton radio && radio.IsChecked == true)
            {
                bool useCoverCache = radio == UseCoverCache;
                var config = SettingsManager.Instance.Config;
                if (config.EnableCoverCache != useCoverCache)
                {
                    config.EnableCoverCache = useCoverCache;
                }
            }
        }

        private void CloseAction_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio) return;

            bool closeToTray = radio.Name == "CloseToTrayRadio";
            SettingsManager.Instance.Config.CloseToTray = closeToTray;
        }

        private void AccentColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var config = SettingsManager.Instance.Config;
            config.AccentColor = AccentColorTextBox.Text;
            ThemeManager.UpdateAccentColor(config.AccentColor);
            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateIcons();
            }
            PlayerService.Instance.RefreshSpectrumControls();
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(AccentColorTextBox.Text);
                string messageColor = (string)Application.Current.FindResource("LngPreviewColor");
                ColorPreview.Text = $"{messageColor}({color.R}, {color.G}, {color.B})";
                ColorPreview.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                string message = Application.Current.FindResource("LngInvalidColor") as string ?? "Invalid color format";
                ColorPreview.Text = message;
                ColorPreview.Foreground = Brushes.Red;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.Save();

            ShowRestartNotificationIfNeeded();

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshAdaptiveGradients();

                if (!SettingsManager.Instance.Config.IsVisualizerEnabled)
                {
                    mainWindow.SpectrumViewer?.ClearSpectrum();
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var config = SettingsManager.Instance.Config;

            if (originalColorScheme != null)
                config.ColorScheme = originalColorScheme;
            if (originalAccentColor != null)
                config.AccentColor = originalAccentColor;

            config.IsVisualizerEnabled = originalVisualizerEnabled;
            config.VisualizerBarCount = originalBarCount;
            config.CloseToTray = originalCloseToTray;
            config.UseAdaptiveGradients = originalUseAdaptiveGradients;
            config.UseSpectrumGradient = originalUseSpectrumGradient;
            config.SpectrumType = originalSpectrumType;
            config.GradientType = originalSpectrumGradientType;
            config.IsAutoLaunchEnabled = originalIsAutoLaunchEnabled;

            CloseToTrayRadio.IsChecked = originalCloseToTray;
            CloseAppRadio.IsChecked = !originalCloseToTray;

            AdaptiveGradientsRadio.IsChecked = originalUseAdaptiveGradients;
            StaticGradientsRadio.IsChecked = !originalUseAdaptiveGradients;
            GradientEnabled.IsChecked = originalUseSpectrumGradient;
            GradientDisabled.IsChecked = !originalUseSpectrumGradient;
            SpectrumBarsRadio.IsChecked = originalSpectrumType == Visualization.SpectrumDisplayType.Bars;
            SpectrumLineRadio.IsChecked = originalSpectrumType == Visualization.SpectrumDisplayType.Line;
            // GradientHeightBasedRadio.IsChecked = originalSpectrumGradientType == Visualization.SpectrumGradientType.HeightBased;
            // GradientFullHeightRadio.IsChecked = originalSpectrumGradientType == Visualization.SpectrumGradientType.FullHeight;
            // GradientHorizontalRadio.IsChecked = originalSpectrumGradientType == Visualization.SpectrumGradientType.Horizontal;

            AutoLaunchEnabled.IsChecked = originalIsAutoLaunchEnabled;
            AutoLaunchDisabled.IsChecked = !originalIsAutoLaunchEnabled;

            if (originalAccentColor != null)
                ThemeManager.UpdateAccentColor(originalAccentColor);

            PlayerService.Instance.RefreshSpectrumControls();

            DialogResult = false;
            _memoryTimer?.Stop();
            Close();
            MemoryOptimizer.RunAsync(Dispatcher);
        }

        private void VisualizerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.IsVisualizerEnabled = VisualizerEnabled.IsChecked ?? false;
            var spectrumControls = new Visualization.SpectrumControl();
            spectrumControls.ClearSpectrum();
            SettingsManager.Instance.Save();
        }

        private void HelpWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow() { Owner = this };
            helpWindow.ShowHelpWindow();
        }

        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            var statisticsWindow = new Statistics() { Owner = this };
            statisticsWindow.Show();
        }

        private void OpenDatabaseLocation_Click(object sender, RoutedEventArgs e)
        {
            string path = AppDataManager.AppDataPath;
            Process.Start("explorer.exe", path);
        }

        private void OpenAppLocation_Click(object sender, RoutedEventArgs e)
        {
            string path = AppContext.BaseDirectory;
            Process.Start("explorer.exe", path);
        }

        private void StartMemoryTicking()
        {
            _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _memoryTimer.Tick += (s, e) => UsingRam();
            _memoryTimer.Start();
        }

        private void UsingRam()
        {
            if (Keyboard.IsKeyDown(Key.I) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var memoryUsage = Process.GetCurrentProcess().WorkingSet64;
                usingRAM.Text = $"Используемая память: {memoryUsage / (1024 * 1024):F2} MB";
                usingRAM.Visibility = Visibility.Visible;
            }
            else
            {
                usingRAM.Visibility = Visibility.Collapsed;
            }
        }


        private async void ReplaceBG_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(BGFolderPath))
            {
                Directory.CreateDirectory(BGFolderPath);
            }

            OpenFileDialog openFileDialog = new();

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(selectedFilePath);
                string destFilePath = Path.Combine(BGFolderPath, fileName);

                try
                {
                    File.Copy(selectedFilePath, destFilePath, overwrite: true);

                    var config = SettingsManager.Instance.Config;
                    config.CustomBackgroundPath = destFilePath;

                    string message = (string)Application.Current.FindResource("LngSuccessfullyBG");
                    await SettingsInfoToast.ShowAsync(message);
                    SettingsManager.Instance.Save();
                    config.UseCustomBackground = true;
                }
                catch (Exception ex)
                {
                    string errorMessage = (string)Application.Current.FindResource("LngError");
                    NotificationWindow.Show($"{errorMessage} {ex.Message}", this);
                    Debug.WriteLine($"[DEBUG] {ex.Message}");
                }
            }
        }

        private void CurrentRound_TextChanged(object sender, RoutedEventArgs e)
        {
            if (isInitializing || CurrentRoundTextBox == null) return;

            var config = SettingsManager.Instance.Config;
            string currentRoundText = CurrentRoundTextBox.Text;

            if (string.IsNullOrWhiteSpace(currentRoundText)) return;

            if (int.TryParse(currentRoundText, out int parsedRound))
            {
                if (parsedRound >= 0 && parsedRound <= 10)
                {
                    config.CurrentRound = parsedRound;
                    SettingsManager.Instance.Save();
                    Application.Current.Resources["AppCornerRadius"] = new CornerRadius(parsedRound);
                }
                else
                {
                    ShowRoundError();
                }
            }
            else
            {
                ShowRoundError();
            }
        }

        private void ShowRoundError()
        {
            string errorMessage = Application.Current.FindResource("LngErrorMessageRound") as string
                                  ?? "Введите целое число от 0 до 10";
            NotificationWindow.Show(errorMessage, this);
        }

        private void LoadCurrentHotkeys()
        {
            var config = SettingsManager.Instance.Config;
            if (config?.Hotkeys == null) return;

            UpdateHotkeyTextBox(TbKeyPlayPause, HotkeyAction.TogglePlayPause);
            UpdateHotkeyTextBox(TbKeyNextTrack, HotkeyAction.NextTrack);
            UpdateHotkeyTextBox(TbKeyPrevTrack, HotkeyAction.PreviousTrack);
            UpdateHotkeyTextBox(TbLyricsMode, HotkeyAction.ViewLyrics);
            UpdateHotkeyTextBox(TbFavoriteRemoveAdd, HotkeyAction.ToggleFavorite);
            UpdateHotkeyTextBox(TbTrackInfo, HotkeyAction.ShowTrackInfo);
            UpdateHotkeyTextBox(TbRepeatTrack, HotkeyAction.ToggleRepeat);
            UpdateHotkeyTextBox(TbShuffleTrack, HotkeyAction.ToggleShuffle);
            UpdateHotkeyTextBox(Tb5secForward, HotkeyAction.SeekForward);
            UpdateHotkeyTextBox(Tb5secback, HotkeyAction.SeekBackward);
            UpdateHotkeyTextBox(TbFullSpectr, HotkeyAction.OpenFullScreenSpectrum);
        }

        private static void UpdateHotkeyTextBox(TextBox textBox, HotkeyAction action)
        {
            var config = SettingsManager.Instance.Config;
            var hotkey = config.Hotkeys.FirstOrDefault(h => h.Action == action);

            if (hotkey != null)
            {
                string modifiersText = hotkey.Modifiers != ModifierKeys.None ? $"{hotkey.Modifiers} + " : "";
                textBox.Text = $"{modifiersText}{hotkey.Key}";
            }
            else
            {
                textBox.Text = "Не назначено";
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not TextBox currentTextBox) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            Key pressedKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            HotkeyAction targetAction;
            if (currentTextBox == TbKeyPlayPause) targetAction = HotkeyAction.TogglePlayPause;
            else if (currentTextBox == TbKeyNextTrack) targetAction = HotkeyAction.NextTrack;
            else if (currentTextBox == TbKeyPrevTrack) targetAction = HotkeyAction.PreviousTrack;
            else if (currentTextBox == TbLyricsMode) targetAction = HotkeyAction.ViewLyrics;
            else if (currentTextBox == TbFavoriteRemoveAdd) targetAction = HotkeyAction.ToggleFavorite;
            else if (currentTextBox == TbTrackInfo) targetAction = HotkeyAction.ShowTrackInfo;
            else if (currentTextBox == TbRepeatTrack) targetAction = HotkeyAction.ToggleRepeat;
            else if (currentTextBox == TbShuffleTrack) targetAction = HotkeyAction.ToggleShuffle;
            else if (currentTextBox == Tb5secForward) targetAction = HotkeyAction.SeekForward;
            else if (currentTextBox == Tb5secback) targetAction = HotkeyAction.SeekBackward;
            else if (currentTextBox == TbFullSpectr) targetAction = HotkeyAction.OpenFullScreenSpectrum;
            else return;

            var config = SettingsManager.Instance.Config;

            // Проверка на дубликаты
            var duplicate = config.Hotkeys.FirstOrDefault(h => h.Key == pressedKey && h.Modifiers == modifiers && h.Action != targetAction);
            if (duplicate != null)
            {
                string errorMsg = $"Это сочетание уже используется для действия: {duplicate.Action}";
                NotificationWindow.Show(errorMsg, this);
                e.Handled = true;
                return;
            }

            var hotkeyToUpdate = config.Hotkeys.FirstOrDefault(h => h.Action == targetAction);
            if (hotkeyToUpdate != null)
            {
                hotkeyToUpdate.Key = pressedKey;
                hotkeyToUpdate.Modifiers = modifiers;
            }
            else
            {
                config.Hotkeys.Add(new HotkeyItem(targetAction, pressedKey, modifiers));
            }

            SettingsManager.Instance.Save();
            UpdateHotkeyTextBox(currentTextBox, targetAction);

            e.Handled = true;
        }

        private void CheckAutoLaunch(object? sender, RoutedEventArgs? e)
        {
            if (isInitializing) return;

            bool isEnabled = AutoLaunchEnabled.IsChecked == true;
            var config = SettingsManager.Instance.Config;
            config.IsAutoLaunchEnabled = isEnabled;
            SettingsManager.Instance.Save();

            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string appName = "QAMP";
            string appPath = AppContext.BaseDirectory;

            if (isEnabled)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
                    key?.SetValue(appName, $"\"{appPath}QAMP.exe\"");
                }
                catch
                {
                    string message = (string)Application.Current.FindResource("LngFailedAutorun");
                    NotificationWindow.Show(message, this);
                    AutoLaunchEnabled.IsChecked = false;
                    config.IsAutoLaunchEnabled = false;
                    SettingsManager.Instance.Save();
                }
            }
            else
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
                    key?.DeleteValue(appName, false);
                }
                catch
                {
                    string message = (string)Application.Current.FindResource("LngFailedDisableAutorun");
                    NotificationWindow.Show(message, this);
                    AutoLaunchEnabled.IsChecked = true;
                    config.IsAutoLaunchEnabled = true;
                    SettingsManager.Instance.Save();
                }
            }
        }
    }
}