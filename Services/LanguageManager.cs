using System.Diagnostics;
using System.IO;
using System.Windows;

namespace QAMP.Services
{
    public static class LanguageManager
    {
        private const string CustomLanguageDictTag = "QAMP.CustomLanguage";

        public static void ApplyLanguage(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) lang = "en";

            Debug.WriteLine($"[Language] ApplyLanguage: '{lang}'");

            var app = Application.Current;
            var resources = app.Resources.MergedDictionaries;

            Uri? newDictUri = ResolveLanguageUri(lang);
            if (newDictUri == null)
            {
                Debug.WriteLine($"[Language] Cannot resolve URI for '{lang}'");
                return;
            }

            Debug.WriteLine($"[Language] Loading: {newDictUri}");

            ResourceDictionary newDict;
            try
            {
                newDict = new ResourceDictionary { Source = newDictUri };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Language] FAILED to load {newDictUri}: {ex}");
                return;
            }

            var toRemove = resources
                .Where(d => d.Contains(CustomLanguageDictTag) ||
                    (d.Source != null && IsLanguageDictionary(d.Source)))
                .ToList();

            foreach (var old in toRemove)
            {
                resources.Remove(old);
            }

            newDict[CustomLanguageDictTag] = true;
            resources.Add(newDict);

            Debug.WriteLine($"[Language] Applied '{lang}', removed {toRemove.Count} old dictionaries");
        }

        /// <summary>
        /// Строит URI для языка: встроенные en/ru — из Localization, кастомные — из папки языков.
        /// </summary>
        private static Uri? ResolveLanguageUri(string lang)
        {
            string lower = lang.ToLowerInvariant();

            switch (lower)
            {
                case "en":
                    return new Uri("Localization/LangENG.xaml", UriKind.Relative);
                case "ru":
                    return new Uri("Localization/LangRU.xaml", UriKind.Relative);
            }

            string? folder = AppDataManager.LanguageFolderPath;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.WriteLine($"[Language] Language folder not found: {folder}");
                return null;
            }

            var candidates = new[]
            {
                $"{lang}.xaml",
                $"Lang{lang.ToUpperInvariant()}.xaml",
                $"Lang{lang}.xaml"
            };

            foreach (var candidate in candidates)
            {
                string fullPath = Path.Combine(folder, candidate);
                if (File.Exists(fullPath))
                {
                    Debug.WriteLine($"[Language] Found custom file: {fullPath}");
                    return new Uri(fullPath, UriKind.Absolute);
                }
            }

            Debug.WriteLine($"[Language] No file found for '{lang}' in {folder}");
            return null;
        }

        /// <summary>
        /// Проверяет, является ли Source языковым словарём (встроенным или кастомным).
        /// </summary>
        private static bool IsLanguageDictionary(Uri source)
        {
            string s = source.OriginalString;

            if (s.Contains("Localization/LangENG.xaml", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Localization/LangRU.xaml", StringComparison.OrdinalIgnoreCase))
                return true;

            string? folder = AppDataManager.LanguageFolderPath;
            if (!string.IsNullOrEmpty(folder) &&
                s.StartsWith(new Uri(folder).AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static bool IsLanguageAvailable(string filePath, out string errorMessage)
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

                var baseDict = new ResourceDictionary
                {
                    Source = new Uri("Localization/LangENG.xaml", UriKind.Relative)
                };

                var missing = baseDict.Keys.Cast<object>()
                    .Where(key => !customDict.Contains(key))
                    .ToList();

                if (missing.Count > 0)
                {
                    string message = (string)Application.Current.FindResource("LngThemeMissingResource");
                    errorMessage = $"{message} {string.Join(", ", missing.Take(5))}" +
                                   (missing.Count > 5 ? $" (+{missing.Count - 5} more)" : "");
                    Debug.WriteLine($"[Language] Missing keys: {string.Join(", ", missing)}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngXAMLReadingError");
                errorMessage = $"{message} {ex.Message}";
                Debug.WriteLine($"[Language] Validation failed: {ex}");
                return false;
            }
        }
    }
}