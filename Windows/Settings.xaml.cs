using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Text.Json;
using QAMP.Models;
using QAMP.Services;
using Microsoft.Win32;
using QAMP.Dialogs;

namespace QAMP.Windows
{
    public partial class Settings : Window
    {
        private bool isInitializing = true;
        private AppSettings? originalSettingsSnapshot;
        public Settings()
        {
            InitializeComponent();
            SettingsSectionList.SelectionChanged += SettingsSectionList_SelectionChanged;
            SettingsSectionList.SelectedIndex = 0;
            InitializeCustomThemes();
            InitializeCustomLanguages();
        }

        private void SettingsSectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = SettingsSectionList.SelectedIndex;
            InterfaceSettingsSection.Visibility = selectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            GraphicsSettingsSection.Visibility = selectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            SystemSettingsSection.Visibility = selectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            HotKeysSettingsSection.Visibility = selectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            SettingsContentScrollViewer.ScrollToTop();
        }

        private void InitializeCustomThemes()
        {
            var appSettings = SettingsManager.Instance.Config;
            Debug.WriteLine("[QAMP Theme Debug] Initializing custom themes...");
            DarkThemeRadio.Checked -= StandardThemeRadio_Checked;
            LightThemeRadio.Checked -= StandardThemeRadio_Checked;
            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;

            try
            {
                Debug.WriteLine($"[QAMP Theme Debug] Themes folder path: {AppDataManager.ThemesFolderPath}");

                if (!Directory.Exists(AppDataManager.ThemesFolderPath))
                {
                    Debug.WriteLine("[QAMP Theme Debug] Themes folder not found. Creating directory...");
                    Directory.CreateDirectory(AppDataManager.ThemesFolderPath);
                }
                else
                {
                    Debug.WriteLine("[QAMP Theme Debug] Themes folder successfully found.");
                }

                RefreshThemesList();
                DarkThemeRadio.IsChecked = false;
                LightThemeRadio.IsChecked = false;
                CustomThemesComboBox.SelectedIndex = -1;

                if (appSettings.ColorScheme != null)
                {
                    string currentTheme = appSettings.ColorScheme;
                    Debug.WriteLine($"[QAMP Theme Debug] Current theme from settings.json: {currentTheme}");

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
                Debug.WriteLine($"[QAMP Theme Debug] CRITICAL ERROR during initialization: {ex.Message}");
            }
            DarkThemeRadio.Checked += StandardThemeRadio_Checked;
            LightThemeRadio.Checked += StandardThemeRadio_Checked;
            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;
        }
        private void InitFontSettingsUI()
        {
            FontRadioPanel.Children.Clear();

            var fontService = FontService.Instance;
            var currentSelectedFont = fontService.FindFont(SettingsManager.Instance.Config.SelectedFontName)
                ?? fontService.DefaultFont;

            foreach (var fontOption in fontService.AvailableFonts)
            {
                AddFontRadioButton(fontOption, isChecked: fontOption == currentSelectedFont);
            }
        }
        private void AddFontRadioButton(FontService.FontOption fontOption, bool isChecked)
        {
            var radioButton = new RadioButton
            {
                Content = fontOption.DisplayName,
                GroupName = "AvailableFonts",
                Tag = fontOption,
                IsChecked = isChecked,
                Foreground = (Brush)FindResource("ForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 5)
            };

            FontRadioPanel.Children.Add(radioButton);

            radioButton.Checked += (s, e) =>
            {
                if (radioButton.IsChecked == true && radioButton.Tag is FontService.FontOption selectedFont)
                {
                    var config = SettingsManager.Instance.Config;
                    config.SelectedFontName = selectedFont.DisplayName;
                    FontService.ApplyFontToApplication(selectedFont.Family);
                    SettingsManager.Instance.Save();
                }
            };
        }
        private void ReplaceFont_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Filter = (string)Application.Current.FindResource("LngFontFiles") ?? "Файлы шрифтов (*.ttf;*.otf)|*.ttf;*.otf",
                Title = (string)Application.Current.FindResource("LngSelectedFont") ?? "Выберите файл шрифта"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                var importedFont = FontService.Instance.ImportFont(filePath);

                if (importedFont is not null)
                {
                    InitFontSettingsUI();
                    var importedRadio = FontRadioPanel.Children
                        .OfType<RadioButton>()
                        .FirstOrDefault(radio =>
                            radio.Tag is FontService.FontOption option && option == importedFont);
                    importedRadio?.IsChecked = true;

                    string message = (string)Application.Current.FindResource("LngFontWasAdded") ?? "Шрифт успешно добавлен";
                    _ = SettingsInfoToast.ShowAsync(message);
                }
            }
        }

        private void RefreshThemesList()
        {
            Debug.WriteLine("[QAMP Theme Debug] Updating themes list in ComboBox");

            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;
            CustomThemesComboBox.Items.Clear();

            if (Directory.Exists(AppDataManager.ThemesFolderPath))
            {
                var xamlFiles = Directory.GetFiles(AppDataManager.ThemesFolderPath, "*.xaml")
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
                string destFilePath = Path.Combine(AppDataManager.ThemesFolderPath, fileName);

                if (!ThemeManager.IsThemeValid(selectedFilePath, out string error))
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
                    Debug.WriteLine($"{message} {ex.Message}");
                }
            }
        }

        private void InitializeCustomLanguages()
        {
            RefreshLanguagesList();
            var config = SettingsManager.Instance.Config;
            if (string.IsNullOrEmpty(config.Language)) return;

            string languageFileName = $"{config.Language}.xaml";

            var match = CustomLanguagesComboBox.Items.Cast<string>()
                .FirstOrDefault(item => string.Equals(item, languageFileName,
                    StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                CustomLanguagesComboBox.SelectedItem = match;
                if (CustomLanguageRadio != null)
                    CustomLanguageRadio.IsChecked = true;
            }
            else
            {
                if (config.Language == "ru") LanguageRuRadio.IsChecked = true;
                else if (config.Language == "en") LanguageEnRadio.IsChecked = true;
            }
        }
        private void RefreshLanguagesList()
        {
            CustomLanguagesComboBox.SelectionChanged -= CustomLanguagesComboBox_SelectionChanged;
            CustomLanguagesComboBox.Items.Clear();

            if (!Directory.Exists(AppDataManager.LanguageFolderPath))
            {
                Directory.CreateDirectory(AppDataManager.LanguageFolderPath);
            }

            var xamlFiles = Directory.GetFiles(AppDataManager.LanguageFolderPath, "*.xaml")
                                     .Select(Path.GetFileName)
                                     .ToList();

            foreach (var file in xamlFiles)
                CustomLanguagesComboBox.Items.Add(file);

            CustomLanguagesComboBox.SelectionChanged += CustomLanguagesComboBox_SelectionChanged;
            CustomLanguagesComboBox.UpdateLayout();
        }

        private void CustomLanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            if (CustomLanguagesComboBox.SelectedItem is string selectedLanguageFile)
            {
                var config = SettingsManager.Instance.Config;
                config.Language = Path.GetFileNameWithoutExtension(selectedLanguageFile);
                SettingsManager.Instance.Save();

                try
                {
                    LanguageManager.ApplyLanguage(config.Language);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error occurred while applying language: {ex.Message}");
                }
            }
        }

        private async void AddLanguageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "XAML |*.xaml"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(selectedFilePath);
                string destFilePath = Path.Combine(AppDataManager.LanguageFolderPath, fileName);

                if (!LanguageManager.IsLanguageAvailable(selectedFilePath, out string errorMessage))
                {
                    string message = (string)Application.Current.FindResource("LngFailedImportLanguage");
                    NotificationWindow.Show($"{message}\n{errorMessage}", this);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(AppDataManager.LanguageFolderPath);
                    File.Copy(selectedFilePath, destFilePath, overwrite: true);
                    RefreshLanguagesList();

                    // ✅ Правильный комбобокс
                    if (CustomLanguagesComboBox.Items.Contains(fileName))
                    {
                        CustomLanguagesComboBox.SelectedItem = fileName;
                    }

                    // ✅ Явно применяем язык (не полагаемся на SelectionChanged)
                    string newLang = Path.GetFileNameWithoutExtension(fileName);
                    var config = SettingsManager.Instance.Config;
                    config.Language = newLang;
                    SettingsManager.Instance.Save();

                    if (CustomLanguageRadio != null)
                        CustomLanguageRadio.IsChecked = true;

                    LanguageManager.ApplyLanguage(newLang);

                    string message = (string)Application.Current.FindResource("LngLanguageAdded");
                    await SettingsInfoToast.ShowAsync(message);
                }
                catch (Exception ex)
                {
                    string message = (string)Application.Current.FindResource("LngFailedToCopyFile");
                    NotificationWindow.Show($"{message} {ex.Message}", this);
                    Debug.WriteLine($"{message} {ex}");
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

            ApplyTheme(themeName);

        }

        private void CustomThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (CustomThemesComboBox == null) return;

            if (CustomThemesComboBox.SelectedItem is string selectedThemeFile)
            {
                ApplyTheme(selectedThemeFile);
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

                ApplyTheme(selectedThemeFile);
            }
        }

        private void ApplyTheme(string themeName)
        {
            if (ThemeManager.ApplyTheme(themeName))
            {
                return;
            }

            string message = Application.Current.TryFindResource("LngError") as string
                             ?? "Could not apply the selected theme.";
            NotificationWindow.Show($"{message} {themeName}", this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            isInitializing = true;

            var config = SettingsManager.Instance.Config;
            originalSettingsSnapshot = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(config))
                ?? throw new InvalidOperationException("Failed to snapshot application settings.");
            VisualizerEnabled.IsChecked = config.IsVisualizerEnabled;
            VisualizerDisabled.IsChecked = !config.IsVisualizerEnabled;
            SpectrumColumnsSlider.Value = Math.Clamp(
                config.VisualizerBarCount,
                Visualization.SpectrumControl.MinimumConfiguredBarCount,
                Visualization.SpectrumControl.MaximumConfiguredBarCount);
            SpectrumColumnsValueText.Text = ((int)SpectrumColumnsSlider.Value).ToString();
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
            TextBrushTextBox.Text = config.TextBrush;
            UpdateColorPreviewTextBrush();

            CurrentRoundTextBox.Text = config.CurrentRound.ToString();

            CloseToTrayRadio.IsChecked = config.CloseToTray;
            CloseAppRadio.IsChecked = !config.CloseToTray;

            AdaptiveGradientsRadio.IsChecked = config.UseAdaptiveGradients;
            StaticGradientsRadio.IsChecked = !config.UseAdaptiveGradients;

            AutoLaunchEnabled.IsChecked = config.IsAutoLaunchEnabled;
            AutoLaunchDisabled.IsChecked = !config.IsAutoLaunchEnabled;

            UseCustomBGradio.IsChecked = config.UseCustomBackground;
            NotUseCustomBGradio.IsChecked = !config.UseCustomBackground;
            UseCustomBGUpDownradio.IsChecked = config.UseCustomBackgroundUpDown;
            NotUseCustomBGUpDownradio.IsChecked = !config.UseCustomBackgroundUpDown;

            UseCoverCache.IsChecked = config.EnableCoverCache;
            NotUseCoverCache.IsChecked = !config.EnableCoverCache;

            InitializeCustomLanguages();

            if (config.IsCompactMode)
                CompactModeRadio.IsChecked = true;
            else
                DefaultModeRadio.IsChecked = true;

            LoadCurrentHotkeys();

            //    ??

            GradientEnabled.IsChecked = config.UseSpectrumGradient;
            GradientDisabled.IsChecked = !config.UseSpectrumGradient;
            SpectrumBarsRadio.IsChecked = config.SpectrumType == Visualization.SpectrumDisplayType.Bars;
            SpectrumLineRadio.IsChecked = config.SpectrumType == Visualization.SpectrumDisplayType.Line;
            SpectrumCenteredBarsRadio.IsChecked = config.SpectrumType == Visualization.SpectrumDisplayType.CenteredBars;
            EnabledConsole.IsChecked = config.UseConsole;
            DisabledConsole.IsChecked = !config.UseConsole;
            EnabledDiscordRpc.IsChecked = config.EnableDiscordRpc;
            DisabledDiscordRpc.IsChecked = !config.EnableDiscordRpc;
            ControlPanelTopRadio.IsChecked = config.IsControlPanelOnTop;
            ControlPanelBottomRadio.IsChecked = !config.IsControlPanelOnTop;
            NowPlayingLeftRadio.IsChecked = config.IsNowPlayingOnLeft;
            NowPlayingRightRadio.IsChecked = !config.IsNowPlayingOnLeft;
            NowPlayingPanelVisible.IsChecked = config.IsNowPlayingIsVisible;
            NowPlayingPanelHidden.IsChecked = !config.IsNowPlayingIsVisible;
            InitializeTrackColumnVisibilityControls(config);

            InitFontSettingsUI();


            isInitializing = false;
        }
        private void NowPlayingPanelVisbile_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;
            var config = SettingsManager.Instance.Config;
            config.IsNowPlayingIsVisible = NowPlayingPanelVisible.IsChecked == true;
            config.IsNowPlayingIsVisible = NowPlayingPanelHidden.IsChecked == false;
            SettingsManager.Instance.Save();
            (Owner as MainWindow)?.ApplyPanelLayout();
        }
        private void PanelLayout_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            var config = SettingsManager.Instance.Config;
            config.IsControlPanelOnTop = ControlPanelTopRadio.IsChecked == true;
            config.IsNowPlayingOnLeft = NowPlayingLeftRadio.IsChecked == true;
            SettingsManager.Instance.Save();
            (Owner as MainWindow)?.ApplyPanelLayout();
        }

        private void TrackColumnVisibility_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            var config = SettingsManager.Instance.Config;
            bool isVisible = sender is CheckBox checkBox && checkBox.IsChecked == true;
            if (sender == ShowTrackCoverColumnCheckBox)
                config.ShowTrackCoverColumn = isVisible;
            else if (sender == ShowTrackTitleColumnCheckBox)
                config.ShowTrackTitleColumn = isVisible;
            else if (sender == ShowTrackArtistColumnCheckBox)
                config.ShowTrackArtistColumn = isVisible;
            else if (sender == ShowTrackAlbumColumnCheckBox)
                config.ShowTrackAlbumColumn = isVisible;
            else if (sender == ShowTrackDurationColumnCheckBox)
                config.ShowTrackDurationColumn = isVisible;
            else if (sender == ShowTrackBitrateColumnCheckBox)
                config.ShowTrackBitrateColumn = isVisible;
            else if (sender == ShowTrackGenreColumnCheckBox)
                config.ShowTrackGenreColumn = isVisible;
            else if (sender == ShowTrackDateColumnCheckBox)
                config.ShowTrackDateColumn = isVisible;

            SettingsManager.Instance.Save();
            (Owner as MainWindow)?.ApplyTrackColumnVisibility();
        }

        private void InitializeTrackColumnVisibilityControls(AppSettings config)
        {
            bool wasInitializing = isInitializing;
            isInitializing = true;
            ShowTrackCoverColumnCheckBox.IsChecked = config.ShowTrackCoverColumn ?? !config.IsCompactMode;
            ShowTrackTitleColumnCheckBox.IsChecked = config.ShowTrackTitleColumn ?? true;
            ShowTrackArtistColumnCheckBox.IsChecked = config.ShowTrackArtistColumn ?? true;
            ShowTrackAlbumColumnCheckBox.IsChecked = config.ShowTrackAlbumColumn ?? true;
            ShowTrackDurationColumnCheckBox.IsChecked = config.ShowTrackDurationColumn ?? true;
            ShowTrackBitrateColumnCheckBox.IsChecked = config.ShowTrackBitrateColumn ?? !config.IsCompactMode;
            ShowTrackGenreColumnCheckBox.IsChecked = config.ShowTrackGenreColumn ?? !config.IsCompactMode;
            ShowTrackDateColumnCheckBox.IsChecked = config.ShowTrackDateColumn ?? !config.IsCompactMode;
            isInitializing = wasInitializing;
        }

        private void LanguageRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio) return;

            string lang;
            if (radio == LanguageEnRadio)
            {
                lang = "en";
            }
            else if (radio == LanguageRuRadio)
            {
                lang = "ru";
            }
            else if (radio == CustomLanguageRadio)
            {
                if (CustomLanguagesComboBox.SelectedItem is string selectedFile)
                {
                    lang = Path.GetFileNameWithoutExtension(selectedFile);
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            var config = SettingsManager.Instance.Config;
            if (config.Language == lang) return;

            config.Language = lang;
            SettingsManager.Instance.Save();

            try
            {
                LanguageManager.ApplyLanguage(lang);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Language] Apply error: {ex.Message}");
            }
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
                    Debug.WriteLine($"Mode changed: Compact = {isCompact}");
                    InitializeTrackColumnVisibilityControls(config);
                    (Owner as MainWindow)?.ApplyTrackColumnVisibility();
                }
            }
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
        private void TextBrushTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var config = SettingsManager.Instance.Config;
            config.TextBrush = TextBrushTextBox.Text;
            ThemeManager.UpdateTextBrush(config.TextBrush);
            UpdateColorPreviewTextBrush();
        }
        private void UpdateColorPreviewTextBrush()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(TextBrushTextBox.Text);
                string messageColor = (string)Application.Current.FindResource("LngPreviewColor");
                ColorPreviewTextBrush.Text = $"{messageColor}({color.R}, {color.G}, {color.B})";
                ColorPreviewTextBrush.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                string message = Application.Current.FindResource("LngInvalidColor") as string ?? "Invalid color format";
                ColorPreviewTextBrush.Text = message;
                ColorPreviewTextBrush.Foreground = Brushes.Red;
            }
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
            if (originalSettingsSnapshot is null)
            {
                DialogResult = false;
                Close();
                return;
            }

            bool autoLaunchChanged = SettingsManager.Instance.Config.IsAutoLaunchEnabled !=
                                     originalSettingsSnapshot.IsAutoLaunchEnabled;
            SettingsManager.Instance.Config = originalSettingsSnapshot;
            var config = originalSettingsSnapshot;

            ApplyTheme(config.ColorScheme);
            ThemeManager.UpdateAccentColor(config.AccentColor);
            ThemeManager.UpdateTextBrush(config.TextBrush);
            Application.Current.Resources["AppCornerRadius"] = new CornerRadius(config.CurrentRound);

            var font = FontService.Instance.FindFont(config.SelectedFontName) ?? FontService.Instance.DefaultFont;
            FontService.ApplyFontToApplication(font.Family);

            LanguageManager.ApplyLanguage(config.Language);

            SetAutoLaunchRadioState(config.IsAutoLaunchEnabled);

            if (autoLaunchChanged)
                ApplyAutoLaunchSetting(config.IsAutoLaunchEnabled);

            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
            PlayerService.Instance.RefreshDiscordPresence();

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateIcons();
                mainWindow.RefreshAdaptiveGradients();
                mainWindow.ApplyPanelLayout();
                mainWindow.ApplyPanelLayout();
                mainWindow.ApplyTrackColumnVisibility();
                if (!config.IsVisualizerEnabled)
                    mainWindow.SpectrumViewer?.ClearSpectrum();
            }

            DialogResult = false;
            Close();
            MemoryOptimizer.RunAsync(Dispatcher);
        }

        private void HelpWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow() { Owner = this };
            helpWindow.ShowHelpWindow();
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

        private async void ReplaceBG_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(AppDataManager.BGFolderPath))
            {
                Directory.CreateDirectory(AppDataManager.BGFolderPath); //v pizdu nahuy 
            }

            OpenFileDialog openFileDialog = new();

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(selectedFilePath);
                string destFilePath = Path.Combine(AppDataManager.BGFolderPath, fileName);

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
        private async void ReplaceBGUpDown_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(AppDataManager.BGFolderPath))
            {
                Directory.CreateDirectory(AppDataManager.BGFolderPath);
            }

            OpenFileDialog openFileDialog = new();

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(selectedFilePath);
                string destFilePath = Path.Combine(AppDataManager.BGFolderPath, fileName);

                try
                {
                    File.Copy(selectedFilePath, destFilePath, overwrite: true);

                    var config = SettingsManager.Instance.Config;
                    config.CustomBackgroundUpDownPath = destFilePath;

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

    }
}