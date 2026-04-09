using System.IO;
using System.Windows;
using QAMP.Models;
using QAMP.Services;

namespace QAMP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // DatabaseService.EnsureDatabaseCreated();

            // Применить тему при запуске
            ThemeManager.ApplyTheme(SettingsManager.Instance.Config.ColorScheme);

            DispatcherUnhandledException += (s, ex) => LogException(ex.Exception, "UI Dispatcher");

            TaskScheduler.UnobservedTaskException += (s, ex) => LogException(ex.Exception, "Task Scheduler");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogException(ex.ExceptionObject as Exception, "AppDomain");
        }
        public static void LogException(Exception? ex, string source)
        {
            if (ex == null) return;

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string message = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] [{source}]\n{ex}\n";
            message += "----------------------------------------------------------------\n";

            try
            {
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Игнорируем ошибку логирования - не можем ничего сделать
            }
            
            // НЕ показываем MessageBox в обработчике исключений!
            // Это может вызвать новое исключение и привести к StackOverflow
            // Приложение будет корректно завершено, лог сохранен в файл
        }
        public static void LogInfo(string message)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_info.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
    }
}