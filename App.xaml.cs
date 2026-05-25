using System.IO;
using System.Text;
using System.Windows;
using QAMP.Models;
using QAMP.Services;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Un4seen.Bass;
using H.NotifyIcon;

namespace QAMP
{
    public partial class App : Application
    {
        public static TaskbarIcon? TrayIcon { get; private set; }
        private static Mutex? _mutex = null;

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        protected override void OnStartup(StartupEventArgs e)
        {
            _ = SetCurrentProcessExplicitAppUserModelID("QAMPCompany.QAMP.MusicPlayer.1.7");

            try
            {
                // Ensure Start Menu shortcut exists with the same AppUserModelID — this is required
                // so Windows can show app name and icon in the volume flyout / media overlay.
                ShortcutHelpers.EnsureStartMenuShortcut("QAMP", Path.Combine(AppContext.BaseDirectory, "QAMP.exe"), "QAMPCompany.QAMP.MusicPlayer.1.7");
            }
            catch (Exception ex)
            {
                LogException(ex, "EnsureStartMenuShortcut");
            }
            const string appName = "QAMP_MusicPlayer_Unique_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var runningProcess = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName)
                    .FirstOrDefault(p => p.Id != currentProcess.Id);

                if (runningProcess != null)
                {
                    IntPtr hWnd = runningProcess.MainWindowHandle;

                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }
                    else
                    {
                        hWnd = FindWindow(null, "QAMP");
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }
                    }
                }
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            ///<summary>
            /// Инициализация трея и BASS.NET
            /// </summary>
            try
            {
                TrayIcon = new TaskbarIcon
                {
                    ToolTipText = "QAMP",
                    Visibility = Visibility.Visible,
                    IconSource = new System.Windows.Media.ImageSourceConverter()
                        .ConvertFromString("pack://application:,,,/icon/QAMP_icon.ico") as System.Windows.Media.ImageSource
                };
                TrayIcon.TrayMouseDoubleClick += (s, args) =>
                {
                    var mainWindow = Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                };
                if (FindResource("TrayContextMenu") is System.Windows.Controls.ContextMenu trayMenu)
                {
                    TrayIcon.ContextMenu = trayMenu;
                }

                TrayIcon.ForceCreate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации трея: {ex.Message}");
            }
            try
            {
                //Here we register BASS.NET with the email and registration key. You should replace these with your own if you have a license.
                BassNet.Registration("example@mail.com", "key-example-1234567890");
                if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, nint.Zero))
                {
                    var errorCode = Bass.BASS_ErrorGetCode();
                    LogException(new Exception($"BASS_Init failed with error code: {errorCode}"), "BASS Initialization");
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "App Startup");
            }

            ThemeManager.ApplyTheme(SettingsManager.Instance.Config.ColorScheme);

            // Применяем автозапуск из конфига
            ApplyAutoLaunchFromConfig();

            DispatcherUnhandledException += (s, ex) => LogException(ex.Exception, "UI Dispatcher");

            TaskScheduler.UnobservedTaskException += (s, ex) => LogException(ex.Exception, "Task Scheduler");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogException(ex.ExceptionObject as Exception, "AppDomain");
        }

        // Обработчики событий трея
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
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.TogglePlayPause();
            }
        }

        private void Next_Click(object? sender, RoutedEventArgs e)
        {
            PlayerService.Instance.PlayNextTrack();
        }

        private void Previous_Click(object? sender, RoutedEventArgs e)
        {
            PlayerService.Instance.PlayPreviousTrack();
        }

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
            Current.Shutdown();
            TrayIcon?.Dispose();
        }

        private static void ApplyAutoLaunchFromConfig()
        {
            var config = SettingsManager.Instance.Config;
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string appName = "QAMP";
            string appPath = AppContext.BaseDirectory;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
                if (config.IsAutoLaunchEnabled)
                {
                    key?.SetValue(appName, $"\"{appPath}QAMP.exe\"");
                }
                else
                {
                    key?.DeleteValue(appName, false);
                }
            }
            catch
            {
                // Игнорируем ошибки при запуске
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
            }
        }
        public static void LogInfo(string message)
        {
            string logPath = AppDataManager.AppInfoLogPath;
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
    }
}

// Дополнительные определения и хелперы для создания ярлыка с AppUserModelID
namespace QAMP
{
    internal static class ShortcutHelpers
    {
        // PROPERTYKEY for AppUserModelID
        private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY
        {
            fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            pid = 5
        };

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue(ref PROPERTYKEY key, out PropVariant pv);
            void SetValue(ref PROPERTYKEY key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public short vt;
            public short wReserved1;
            public short wReserved2;
            public short wReserved3;
            public IntPtr p;
            public int p2;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        private static PropVariant CreatePropVariant(string value)
        {
            var pv = new PropVariant();
            pv.vt = 31; // VT_LPWSTR
            pv.p = Marshal.StringToCoTaskMemUni(value);
            return pv;
        }

        public static void EnsureStartMenuShortcut(string shortcutName, string targetPath, string appUserModelId)
        {
            try
            {
                var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var programs = Path.Combine(startMenu, "Microsoft", "Windows", "Start Menu", "Programs");
                var linkPath = Path.Combine(programs, shortcutName + ".lnk");

                if (File.Exists(linkPath))
                {
                    // Shortcut exists — nothing to do
                    return;
                }

                // Create ShellLink object
                var shellType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
                var shellLink = (IShellLinkW)Activator.CreateInstance(shellType)!;
                shellLink.SetPath(targetPath);
                shellLink.SetDescription(shortcutName);
                shellLink.SetIconLocation(targetPath, 0);

                var propStore = (IPropertyStore)shellLink;
                var pv = CreatePropVariant(appUserModelId);
                // copy readonly static PROPERTYKEY into local variable to pass by ref
                PROPERTYKEY key = PKEY_AppUserModel_ID;
                propStore.SetValue(ref key, ref pv);
                propStore.Commit();
                PropVariantClear(ref pv);

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(linkPath, true);
            }
            catch (Exception ex)
            {
                // Если что-то пошло не так — логируем и продолжаем
                try { File.AppendAllText(AppDataManager.GetFilePath("shortcut_errors.log"), $"[{DateTime.Now}] EnsureStartMenuShortcut error: {ex}\n"); } catch { }
            }
        }
    }
}