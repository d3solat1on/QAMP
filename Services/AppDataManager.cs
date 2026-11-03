using System.IO;
using System.Diagnostics;

namespace QAMP.Services;

/// <summary>
/// Управляет путями приложения и пользовательскими данными в %LOCALAPPDATA%\QAMP\.
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

    // User-installed themes, fonts, languages, and backgrounds.
    public static string ThemesFolderPath => Path.Combine(_appDataPath, "Themes");
    public static string BGFolderPath => Path.Combine(_appDataPath, "Background");
    public static string FontsFolderPath => Path.Combine(_appDataPath, "Fonts");
    public static string LanguageFolderPath => Path.Combine(_appDataPath, "Localization");
    public static string CovercacheFolderPath => Path.Combine(_appDataPath, "covercache");

    public static void MigrateLegacyUserFiles()
    {
        EnsureAppDataFolderExists();
        Directory.CreateDirectory(ThemesFolderPath);
        Directory.CreateDirectory(FontsFolderPath);
        Directory.CreateDirectory(LanguageFolderPath);
        Directory.CreateDirectory(BGFolderPath);
        Directory.CreateDirectory(CovercacheFolderPath);

        MigrateLegacyFiles(
            Path.Combine(AppContext.BaseDirectory, "Themes"),
            ThemesFolderPath,
            [".xaml"],
            ["DarkTheme.xaml", "LightTheme.xaml"]);
        MigrateLegacyFiles(
            Path.Combine(AppContext.BaseDirectory, "Localization"),
            LanguageFolderPath,
            [".xaml"],
            ["LangENG.xaml", "LangRU.xaml"]);
        MigrateLegacyFiles(
            Path.Combine(AppContext.BaseDirectory, "Fonts"),
            FontsFolderPath,
            [".ttf", ".otf"]);
    }

    private static void MigrateLegacyFiles(
        string sourceDirectory,
        string destinationDirectory,
        string[] extensions,
        string[]? excludedFileNames = null)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourcePath in Directory.EnumerateFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(sourcePath);
                if (!extensions.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase) ||
                    excludedFileNames?.Contains(fileName, StringComparer.OrdinalIgnoreCase) == true)
                {
                    continue;
                }

                string destinationPath = Path.Combine(destinationDirectory, fileName);
                if (File.Exists(destinationPath))
                {
                    continue;
                }

                try
                {
                    File.Copy(sourcePath, destinationPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to migrate user file '{sourcePath}': {ex}");
                    App.LogException(ex, "MigrateUserFileError");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to migrate files from '{sourceDirectory}': {ex}");
            App.LogException(ex, "MigrateUserFilesError");
        }
    }

    // Публичные свойства для путей к конкретным файлам
    public static string DatabasePath => GetFilePath("library.db");
    public static string SettingsPath => GetFilePath("settings.json");
    public static string CrashLogPath => GetFilePath("crash_log.txt");
}
