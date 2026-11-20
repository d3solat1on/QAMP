using System.IO;
using System.Windows;
using QAMP.Models;
using QAMP.Services;
using System.Runtime.InteropServices;
using Un4seen.Bass;
using H.NotifyIcon;
using System.Diagnostics;

namespace QAMP
{
    public partial class App : Application
    {
        private static Mutex? _appMutex;
        private static EventWaitHandle? _singleInstanceWaitHandle;

#if DEBUG
        private const string AppUserModelId = "QAMPCompany.QAMP.MusicPlayer.Debug";
        private const string StartMenuShortcutName = "QAMP Debug";
#else
        private const string AppUserModelId = "QAMPCompany.QAMP.MusicPlayer";
        private const string StartMenuShortcutName = "QAMP";
#endif

        public static TaskbarIcon? TrayIcon { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            int appUserModelIdResult = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            if (appUserModelIdResult < 0)
            {
                var exception = Marshal.GetExceptionForHR(appUserModelIdResult);
                Debug.WriteLine($"Setting AppUserModelID failed: 0x{appUserModelIdResult:X8}");
                LogException(exception, "SetAppUserModelIdError");
            }

#if DEBUG
            const string appName = "Global\\QAMP_MusicPlayer_Unique_Mutex_Debug";
            const string eventName = "Global\\QAMP_MusicPlayer_Unique_Event_Debug";
#else
            const string appName = "Global\\QAMP_MusicPlayer_Unique_Mutex";
            const string eventName = "Global\\QAMP_MusicPlayer_Unique_Event";
#endif            

            _appMutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                try
                {
                    using var h = EventWaitHandle.OpenExisting(eventName);
                    h.Set();
                }
                catch { }

                Current.Shutdown();
                return;
            }
            _singleInstanceWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            _ = Task.Run(ListenForNewInstances);

            base.OnStartup(e);

            AppDataManager.MigrateLegacyUserFiles();
            InitializeTray();
            InitializeBass();
            InitializeInterface();
            InitializeUnhandledExceptions();
            InitializeDiscord();

            _ = Task.Run(() => ShortcutHelpers.EnsureStartMenuShortcut(
                StartMenuShortcutName,
                Path.Combine(AppContext.BaseDirectory, "QAMP.exe"),
                AppUserModelId));
        }

        private static void ListenForNewInstances()
        {
            while (_singleInstanceWaitHandle != null)
            {
                if (_singleInstanceWaitHandle.WaitOne())
                {
                    Current.Dispatcher.Invoke(() =>
                    {
                        if (Current.MainWindow is MainWindow mainWindow)
                        {
                            if (mainWindow.Visibility != Visibility.Visible)
                            {
                                mainWindow.Show();
                            }

                            if (mainWindow.WindowState == WindowState.Minimized)
                            {
                                mainWindow.WindowState = App.SavedState;
                            }

                            mainWindow.Activate();
                        }
                    });
                }
            }
        }

        private static void InitializeTray()
        {
            try
            {
                TrayIcon = new TaskbarIcon
                {
#if DEBUG
                    ToolTipText = "QAMP_DEBUG",
#else
                    ToolTipText = "QAMP",
#endif             
                    Visibility = Visibility.Visible,
#if DEBUG
                    IconSource = new System.Windows.Media.ImageSourceConverter()
                        .ConvertFromString("pack://application:,,,/icon/QAMP_icon_debug.ico") as System.Windows.Media.ImageSource
#else
                    IconSource = new System.Windows.Media.ImageSourceConverter()
                        .ConvertFromString("pack://application:,,,/icon/QAMP_icon.ico") as System.Windows.Media.ImageSource
#endif
                };

                TrayIcon.TrayMouseDoubleClick += (s, args) =>
                {
                    if (Current.MainWindow is MainWindow mainWindow)
                    {
                        if (App.SavedWidth > 0 && App.SavedHeight > 0)
                        {
                            mainWindow.Width = App.SavedWidth;
                            mainWindow.Height = App.SavedHeight;
                        }

                        if (App.SavedLeft >= 0 && App.SavedTop >= 0 &&
                            App.SavedLeft < SystemParameters.VirtualScreenWidth &&
                            App.SavedTop < SystemParameters.VirtualScreenHeight)
                        {
                            mainWindow.Left = App.SavedLeft;
                            mainWindow.Top = App.SavedTop;
                        }

                        mainWindow.WindowState = App.SavedState;
                        mainWindow.Show();
                        mainWindow.Activate();
                    }
                };

                if (Current.FindResource("TrayContextMenu") is System.Windows.Controls.ContextMenu trayMenu)
                {
                    TrayIcon.ContextMenu = trayMenu;
                }

                TrayIcon.ForceCreate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tray init error: {ex.Message}");
                LogException(ex, "TrayInit Error");
            }
        }
        private static void InitializeBass()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "bass_settings.json");
                string email = "example@mail.com";
                string key = "key-example-1234567890";

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var data = System.Text.Json.JsonSerializer.Deserialize<BassConfigDto>(json);
                    if (data != null)
                    {
                        if (!string.IsNullOrEmpty(data.BassEmail)) email = data.BassEmail;
                        if (!string.IsNullOrEmpty(data.BassKey)) key = data.BassKey;
                    }
                }

                BassNet.Registration(email, key);
                Debug.WriteLine($"BASS registered with email: {email}, key: {key}");

                var deviceId = SettingsManager.Instance.Config.OutputDeviceId;
                if (deviceId >= 0) Bass.BASS_SetDevice(deviceId);
                Debug.WriteLine($"BASS output device set to ID: {deviceId}");

                if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, nint.Zero))
                {
                    Debug.WriteLine($"BASS_Init failed: {Bass.BASS_ErrorGetCode()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BASS init Error: {ex.Message}");
                LogException(ex, "BASS init Error");
            }
        }
        private static void InitializeInterface()
        {
            try
            {
                LanguageManager.ApplyLanguage(SettingsManager.Instance.Config.Language);
                ThemeManager.UpdateAccentColor(SettingsManager.Instance.Config.AccentColor);
                ThemeManager.LoadThemeFromConfig();
                var config = SettingsManager.Instance.Config;
                var fontService = FontService.Instance;
                var selectedFont = fontService.FindFont(config.SelectedFontName);
                if (selectedFont is null)
                {
                    selectedFont = fontService.DefaultFont;
                    Debug.WriteLine($"Saved font '{config.SelectedFontName}' was not found. Using the default font.");
                    config.SelectedFontName = selectedFont.DisplayName;
                    SettingsManager.Instance.Save();
                }
                Current.Resources["MainFont"] = selectedFont.Family;
                var fontSize = config.FontSize;
                Current.Resources["FontSize"] = fontSize;
                var fontSizeDataGrid = config.FontSizeDataGrid;
                Current.Resources["FontSizeDataGrid"] = fontSizeDataGrid;
                Debug.WriteLine($"Font size from config: {config.FontSize}");
                Debug.WriteLine($"Font size DataGrid from config: {config.FontSizeDataGrid}");
                Debug.WriteLine($"Font size set to: {fontSize}");
                var savedRound = SettingsManager.Instance.Config.CurrentRound;
                Current.Resources["AppCornerRadius"] = new CornerRadius(savedRound);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Interface init Error: {ex.Message}");
                LogException(ex, "Interface init Error");
            }
        }
        public static void LogException(Exception? ex, string source)
        {
            if (ex == null) return;

            string logPath = AppDataManager.CrashLogPath;
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [{source}]\n{ex}\n";
            message += "----------------------------------------------------------------\n";

            try
            {
                File.AppendAllText(logPath, message);
            }
            catch
            {
                Debug.WriteLine("ььуьуьу баб эбэбэ");
            }
        }
        private static void InitializeUnhandledExceptions()
        {
            static void handler(object s, UnhandledExceptionEventArgs ex)
            {
                if (ex.ExceptionObject is Exception exception)
                {
                    Debug.WriteLine($"[CRASH] {exception.Message}");
                    try
                    {
                        var logPath = AppDataManager.CrashLogPath;
                        var logDir = Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrEmpty(logDir)) Directory.CreateDirectory(logDir);

                        File.AppendAllText(
                            logPath,
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{s ?? "Unknown"}]\n{exception}\n---\n"
                        );
                    }
                    catch { }
                }
            }

            AppDomain.CurrentDomain.UnhandledException += handler;

            Current.DispatcherUnhandledException += (s, ex) =>
            {
                handler(s, new UnhandledExceptionEventArgs(ex.Exception, false));
                ex.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                handler(s ?? "TaskScheduler", new UnhandledExceptionEventArgs(ex.Exception, false));
                ex.SetObserved();
            };
        }
        private static void InitializeDiscord()
        {
            try
            {
                string configPath = Path.Combine(AppContext.BaseDirectory, "discord_settings.json");
                string discordKey = "key-example-1234567890";
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    DiscordConfigDto? data = System.Text.Json.JsonSerializer.Deserialize<DiscordConfigDto>(json);
                    if (data != null)
                    {
                        if (!string.IsNullOrEmpty(data.DiscordKey)) discordKey = data.DiscordKey;
                    }
                }
                DiscordRpcService.Initialize(discordKey);
                System.Diagnostics.Debug.WriteLine($"InitializeDiscord with key {discordKey}");

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error InitializeDiscord: {ex.Message}");
                App.LogException(ex, "Error InitializeDiscord");
            }
        }

        private void OpenQAMP_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                if (App.SavedWidth > 0 && App.SavedHeight > 0)
                {
                    mainWindow.Width = App.SavedWidth;
                    mainWindow.Height = App.SavedHeight;
                }

                if (App.SavedLeft >= 0 && App.SavedTop >= 0 &&
                    App.SavedLeft < SystemParameters.VirtualScreenWidth &&
                    App.SavedTop < SystemParameters.VirtualScreenHeight)
                {
                    mainWindow.Left = App.SavedLeft;
                    mainWindow.Top = App.SavedTop;
                }

                mainWindow.WindowState = App.SavedState;
                mainWindow.Show();
                mainWindow.Activate();
            }
        }

        private void PlayPause_Click(object? sender, RoutedEventArgs e)
        {
            (Current.MainWindow as MainWindow)?.TogglePlayPause();
        }

        private void Next_Click(object? sender, RoutedEventArgs e) => PlayerService.Instance.PlayNextTrack();
        private void Previous_Click(object? sender, RoutedEventArgs e) => PlayerService.Instance.PlayPreviousTrack();
        private void ShowTrackInfo_Click(object? sender, RoutedEventArgs e)
        {
            var player = PlayerService.Instance;
            if (player.CurrentTrack != null)
            {
                if (Current.MainWindow is MainWindow mainWindow)
                {
                    var fullInfo = TagReader.GetFullTrackInfo(player.CurrentTrack.Path);
                    if (fullInfo != null)
                    {
                        fullInfo.Id = player.CurrentTrack.Id;
                        fullInfo.PlayCount = player.CurrentTrack.PlayCount;

                        var infoWindow = new Windows.ShowTrackInfo(fullInfo)
                        {
                            Owner = mainWindow
                        };
                        infoWindow.Show();
                    }
                }
            }
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            (Current.MainWindow as MainWindow)?.SaveCurrentState();
            System.Threading.Thread.Sleep(200);
            Current.Shutdown();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstanceWaitHandle?.Dispose();
            TrayIcon?.Dispose();
            _appMutex?.Dispose();
            WasapiEngine.Instance.Stop();
            Bass.BASS_Free();
            base.OnExit(e);
        }

        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetCurrentProcessExplicitAppUserModelID(string appId);

        private class BassConfigDto
        {
            public string? BassEmail { get; set; }
            public string? BassKey { get; set; }
        }
        private class DiscordConfigDto
        {
            public string? DiscordKey { get; set; }
        }
    }
    public partial class App : Application
    {
        public static double SavedLeft { get; set; }
        public static double SavedTop { get; set; }
        public static double SavedWidth { get; set; }
        public static double SavedHeight { get; set; }
        public static WindowState SavedState { get; set; } = WindowState.Normal;
    }
}