using System.IO;
using System.Windows;
using QAMP.Models;
using QAMP.Services;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace QAMP
{
    public partial class App : Application
    {
        private static Mutex? _mutex = null;

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

            ThemeManager.ApplyTheme(SettingsManager.Instance.Config.ColorScheme);

            // Применяем автозапуск из конфига
            ApplyAutoLaunchFromConfig();

            DispatcherUnhandledException += (s, ex) => LogException(ex.Exception, "UI Dispatcher");

            TaskScheduler.UnobservedTaskException += (s, ex) => LogException(ex.Exception, "Task Scheduler");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogException(ex.ExceptionObject as Exception, "AppDomain");
        }

        private void ApplyAutoLaunchFromConfig()
        {
            var config = SettingsManager.Instance.Config;
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string appName = "QAMP";
            string appPath = AppContext.BaseDirectory;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true);
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