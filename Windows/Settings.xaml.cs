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
        private bool originalIsAutoLaunchEnabled;
        private readonly PlayerService _player;
        private DispatcherTimer? _memoryTimer;
        public Settings(PlayerService player)
        {
            InitializeComponent();
            InitializeCustomThemes();
            _player = player;
            StartMemoryTicking();
        }

        private static string ThemesFolderPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");

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
                // Проверяем, что записано в настройках
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
                        // Если это кастомный файл темы, выбираем его в списке
                        if (CustomThemesComboBox.Items.Contains(currentTheme))
                        {
                            CustomThemesComboBox.SelectedItem = currentTheme;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[QAMP Theme Debug] Предупреждение: SettingsManager или CurrentTheme равны null.");
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

            // 1. Отключаем событие
            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;
            Debug.WriteLine("[QAMP Theme Debug] Событие SelectionChanged временно отключено.");

            // 2. Очищаем элементы
            CustomThemesComboBox.Items.Clear();
            Debug.WriteLine("[QAMP Theme Debug] Элементы ComboBox очищены.");

            if (Directory.Exists(ThemesFolderPath))
            {
                // 3. Ищем файлы
                var xamlFiles = Directory.GetFiles(ThemesFolderPath, "*.xaml")
                                         .Select(System.IO.Path.GetFileName)
                                         .ToList();

                Debug.WriteLine($"[QAMP Theme Debug] Найдено файлов .xaml в папке: {xamlFiles.Count}");

                foreach (var file in xamlFiles)
                {
                    CustomThemesComboBox.Items.Add(file);
                    Debug.WriteLine($"[QAMP Theme Debug] Добавлен в ComboBox: {file}");
                }
            }
            else
            {
                Debug.WriteLine("[QAMP Theme Debug] Ошибка: Папка Themes не существует на момент вызова RefreshThemesList.");
            }

            // 4. Возвращаем событие
            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;
            Debug.WriteLine("[QAMP Theme Debug] Событие SelectionChanged снова подключено.");

            // На всякий случай выведем итоговое количество элементов в самом контроле
            Debug.WriteLine($"[QAMP Theme Debug] Итоговое количество Items в ComboBox: {CustomThemesComboBox.Items.Count}");

            // Принудительно заставляем UI перерисоваться
            CustomThemesComboBox.UpdateLayout();
        }

        private static bool IsThemeValid(string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                // 1. Быстрая проверка размера файла
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 1024 * 1024)
                {
                    errorMessage = "Файл темы слишком большой (макс. 1 МБ).";
                    return false;
                }

                // 2. Пытаемся распарсить кастомный XAML
                ResourceDictionary? customDict;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    customDict = System.Windows.Markup.XamlReader.Load(fs) as ResourceDictionary;
                }

                if (customDict == null)
                {
                    errorMessage = "Файл не является корректным словарем ресурсов (ResourceDictionary).";
                    return false;
                }

                // 3. Загружаем нашу стандартную Темную тему как эталон для сверки
                var defaultThemeUri = new Uri(";component/Themes/DarkTheme.xaml", UriKind.RelativeOrAbsolute);
                var baseThemeDict = new ResourceDictionary { Source = defaultThemeUri };

                // 4. Проверяем, что в новой теме есть все ключи из базовой
                foreach (var key in baseThemeDict.Keys)
                {
                    if (!customDict.Contains(key))
                    {
                        errorMessage = $"В теме отсутствует обязательный ресурс: '{key}'";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка чтения XAML: {ex.Message}";
                return false;
            }
        }

        private async void AddThemeButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                string fileName = System.IO.Path.GetFileName(selectedFilePath);
                string destFilePath = System.IO.Path.Combine(ThemesFolderPath, fileName);

                // Валидация файла
                if (!IsThemeValid(selectedFilePath, out string error))
                {
                    NotificationWindow.Show($"Не удалось импортировать тему.\n{error}", this);
                    return;
                }

                try
                {
                    // Копируем в папку приложения
                    File.Copy(selectedFilePath, destFilePath, overwrite: true);

                    // Обновляем список в ComboBox
                    RefreshThemesList();

                    // Автоматически выбираем добавленную тему
                    CustomThemesComboBox.SelectedItem = fileName;

                    await SettingsInfoToast.ShowAsync("Тема успешно добавлена!");
                }
                catch (Exception ex)
                {
                    NotificationWindow.Show($"Не удалось скопировать файл: {ex.Message}", this);
                }
            }
        }

        private void StandardThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (CustomThemesComboBox == null) return;

            // Сбрасываем выбор в кастомных темах
            CustomThemesComboBox.SelectionChanged -= CustomThemesComboBox_SelectionChanged;
            CustomThemesComboBox.SelectedIndex = -1;
            CustomThemesComboBox.SelectionChanged += CustomThemesComboBox_SelectionChanged;

            string themeName = (sender == DarkThemeRadio) ? "Dark" : "Light";
            SettingsManager.Instance.Config.ColorScheme = themeName;
            ThemeManager.ApplyTheme(themeName);
        }

        private void CustomThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (CustomThemesComboBox == null) return;

            if (CustomThemesComboBox.SelectedItem is string selectedThemeFile)
            {
                SettingsManager.Instance.Config.ColorScheme = selectedThemeFile;
                ThemeManager.ApplyTheme(selectedThemeFile);
            }
        }

        // Выбор кастомной темы из ComboBox
        private void CustomThemesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomThemesComboBox.SelectedItem is string selectedThemeFile)
            {
                // Снимаем флажки со стандартных радио-кнопок
                DarkThemeRadio.Checked -= StandardThemeRadio_Checked;
                LightThemeRadio.Checked -= StandardThemeRadio_Checked;

                DarkThemeRadio.IsChecked = false;
                LightThemeRadio.IsChecked = false;

                DarkThemeRadio.Checked += StandardThemeRadio_Checked;
                LightThemeRadio.Checked += StandardThemeRadio_Checked;

                SettingsManager.Instance.Config.ColorScheme = selectedThemeFile;
                ThemeManager.ApplyTheme(selectedThemeFile);
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
            originalIsAutoLaunchEnabled = config.IsAutoLaunchEnabled;

            // Установить параметры спектрограммы
            VisualizerEnabled.IsChecked = config.IsVisualizerEnabled;
            VisualizerDisabled.IsChecked = !config.IsVisualizerEnabled;
            // SetBarCountComboValue(config.VisualizerBarCount);

            // Установить выбранную тему
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
            // Установить акцентный цвет
            AccentColorTextBox.Text = config.AccentColor;
            UpdateColorPreview();

            // Загружаем выбранное действие при закрытии
            CloseToTrayRadio.IsChecked = config.CloseToTray;
            CloseAppRadio.IsChecked = !config.CloseToTray;

            // Загружаем состояние адаптивных градиентов
            AdaptiveGradientsRadio.IsChecked = config.UseAdaptiveGradients;
            StaticGradientsRadio.IsChecked = !config.UseAdaptiveGradients;

            // Загружаем состояние автозапуска
            AutoLaunchEnabled.IsChecked = config.IsAutoLaunchEnabled;
            AutoLaunchDisabled.IsChecked = !config.IsAutoLaunchEnabled;

            if (config.IsCompactMode)
                CompactModeRadio.IsChecked = true;
            else
                DefaultModeRadio.IsChecked = true;
            CheckAutoLaunch(null, null);

            isInitializing = false;
        }

        private void AdaptiveGradients_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseAdaptiveGradients = AdaptiveGradientsRadio.IsChecked ?? false;
            SettingsManager.Instance.Save();
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            if (sender is not RadioButton radio) return;

            string theme = radio.Content.ToString() switch
            {
                "Темная" => "Dark",
                "Светлая" => "Light",
                "Пользовательская" => "Custom",
                _ => "Dark"
            };

            if (SettingsManager.Instance.Config.ColorScheme == theme)
                return;

            SettingsManager.Instance.Config.ColorScheme = theme;

            // Не применяем несуществующую тему
            if (theme == "Custom")
            {
                // Просто оставляем текущую тему и применяем оттенок
                ThemeManager.UpdateAccentColor(SettingsManager.Instance.Config.AccentColor);
                // Обновляем цвета спектра
                PlayerService.Instance.RefreshSpectrumControls();
                return;
            }

            ThemeManager.ApplyTheme(theme);
            // Обновляем цвета спектра после смены темы
            PlayerService.Instance.RefreshSpectrumControls();
        }
        private void Format_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing || sender is not RadioButton radio) return;

            bool isCompact = radio.Name == "CompactModeRadio";

            var config = SettingsManager.Instance.Config;
            config.IsCompactMode = isCompact;

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
            // Обновляем цвета спектра при смене цвета акцента
            PlayerService.Instance.RefreshSpectrumControls();
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(AccentColorTextBox.Text);
                ColorPreview.Text = $"Предварительный просмотр: RGB({color.R}, {color.G}, {color.B})";
                ColorPreview.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                ColorPreview.Text = "Неверный формат цвета";
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
            var config = SettingsManager.Instance.Config;

            // Восстановить оригинальные настройки для темы
            if (originalColorScheme != null)
                config.ColorScheme = originalColorScheme;
            if (originalAccentColor != null)
                config.AccentColor = originalAccentColor;


            // Восстановить оригинальные значения спектрограммы
            config.IsVisualizerEnabled = originalVisualizerEnabled;
            config.VisualizerBarCount = originalBarCount;

            // Восстановить оригинальное значение "Сворачивать в трей"
            config.CloseToTray = originalCloseToTray;

            // Восстановить оригинальное значение адаптивных градиентов
            config.UseAdaptiveGradients = originalUseAdaptiveGradients;

            // Восстановить оригинальное значение автозапуска
            config.IsAutoLaunchEnabled = originalIsAutoLaunchEnabled;

            // Обновляем RadioButton при восстановлении
            if (originalCloseToTray)
            {
                CloseToTrayRadio.IsChecked = true;
            }
            else
            {
                CloseAppRadio.IsChecked = true;
            }

            // Обновляем RadioButton адаптивных градиентов при восстановлении
            if (originalUseAdaptiveGradients)
            {
                AdaptiveGradientsRadio.IsChecked = true;
            }
            else
            {
                StaticGradientsRadio.IsChecked = true;
            }

            // Обновляем RadioButton автозапуска при восстановлении
            if (originalIsAutoLaunchEnabled)
            {
                AutoLaunchEnabled.IsChecked = true;
            }
            else
            {
                AutoLaunchDisabled.IsChecked = true;
            }

            if (originalColorScheme != null)
                ThemeManager.ApplyTheme(originalColorScheme);
            if (originalAccentColor != null)
                ThemeManager.UpdateAccentColor(originalAccentColor);

            // Обновляем цвета спектра при отмене настроек
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
            var helpWindow = new HelpWindow()
            {
                Owner = this
            };

            helpWindow.ShowHelpWindow();
        }
        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            var statisticsWindow = new Statistics()
            {
                Owner = this
            };
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
            _memoryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
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
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true);
                    key?.SetValue(appName, $"\"{appPath}QAMP.exe\"");
                }
                catch
                {
                    Dialogs.NotificationWindow.Show("Не удалось установить автозапуск. Пожалуйста, запустите приложение от имени администратора.", this);
                    AutoLaunchEnabled.IsChecked = false;
                    config.IsAutoLaunchEnabled = false;
                    SettingsManager.Instance.Save();
                }
            }
            else
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true);
                    key?.DeleteValue(appName, false);
                }
                catch
                {
                    Dialogs.NotificationWindow.Show("Не удалось отключить автозапуск. Пожалуйста, запустите приложение от имени администратора.", this);
                    AutoLaunchEnabled.IsChecked = true;
                    config.IsAutoLaunchEnabled = true;
                    SettingsManager.Instance.Save();
                }
            }
        }
    }
}