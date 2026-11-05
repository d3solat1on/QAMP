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

#if DEBUG
        private const string AppUserModelId = "QAMPCompany.QAMP.MusicPlayer.Debug";
        private const string StartMenuShortcutName = "QAMP Debug";
#else
        private const string AppUserModelId = "QAMPCompany.QAMP.MusicPlayer";
        private const string StartMenuShortcutName = "QAMP";
#endif

        private static readonly int WM_SHOWME = RegisterWindowMessage("QAMP_UNIQUE_SHOW_ME_MSG");
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
#else
            const string appName = "Global\\QAMP_MusicPlayer_Unique_Mutex";
#endif            

            _appMutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                SignalToExistingInstance();
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            AppDataManager.MigrateLegacyUserFiles();
            InitializeTray();
            InitializeBass();
            InitializeInterface();
            InitializeUnhandledExceptions();

            _ = Task.Run(() => ShortcutHelpers.EnsureStartMenuShortcut(
                StartMenuShortcutName,
                Path.Combine(AppContext.BaseDirectory, "QAMP.exe"),
                AppUserModelId));
        }

        private static void SignalToExistingInstance()
        {
            PostMessage(
                HWND_BROADCAST,
                WM_SHOWME,
                IntPtr.Zero,
                IntPtr.Zero
            );
        }

        public static void RegisterWindowForSingleInstance(Window window)
        {
            var wih = new System.Windows.Interop.WindowInteropHelper(window);
            var hWnd = wih.EnsureHandle();

            System.Windows.Interop.HwndSource.FromHwnd(hWnd)?.AddHook((hwnd, msg, wParam, lParam, ref handled) =>
            {
                if (msg == WM_SHOWME && msg != 0)
                {
                    if (window.Visibility != Visibility.Visible)
                    {
                        window.Show();
                    }

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }

                    window.Activate();
                    handled = true;
                }
                return IntPtr.Zero;
            });
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
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
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

        private void OpenQAMP_Click(object? sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Maximized;
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
            TrayIcon?.Dispose();
            _appMutex?.Dispose();
            WasapiEngine.Instance.Stop();
            Bass.BASS_Free();
            base.OnExit(e);
        }

        private const int HWND_BROADCAST = 0xffff;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial int RegisterWindowMessage(string lpString);

        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetCurrentProcessExplicitAppUserModelID(string appId);

        private class BassConfigDto
        {
            public string? BassEmail { get; set; }
            public string? BassKey { get; set; }
        }
    }
}