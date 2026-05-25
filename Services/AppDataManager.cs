using System.IO;

namespace QAMP.Services;

/// <summary>
/// Управляет всеми путями для сохранения данных приложения (БД, логи, настройки)
/// Все файлы хранятся в %LOCALAPPDATA%\QAMP\ для корректной работы при установке в Program Files
/// </summary>
public static class AppDataManager
{
    // Путь к папке данных приложения
    private static readonly string _appDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QAMP");

    /// <summary>
    /// Получает путь к папке AppData (%LOCALAPPDATA%\QAMP\)
    /// </summary>
    public static string AppDataPath => _appDataPath;

    /// <summary>
    /// Получает полный путь к файлу в папке AppData
    /// </summary>
    public static string GetFilePath(string fileName)
    {
        EnsureAppDataFolderExists();
        return Path.Combine(_appDataPath, fileName);
    }

    /// <summary>
    /// Гарантирует существование папки AppData
    /// </summary>
    public static void EnsureAppDataFolderExists()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }
    }

    // Публичные свойства для путей к конкретным файлам
    public static string DatabasePath => GetFilePath("library.db");
    public static string SettingsPath => GetFilePath("settings.json");
    public static string CrashLogPath => GetFilePath("crash_log.txt");
    public static string AppInfoLogPath => GetFilePath("app_info.log");
    public static string SmtcLogPath => GetFilePath("QAMP_SMTC.log");

    public static void AppendSmtcLog(string text)
    {
        try
        {
            EnsureAppDataFolderExists();
            File.AppendAllText(SmtcLogPath, text + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }
}
