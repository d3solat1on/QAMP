using System.Windows;

namespace QAMP.Services
{
    public static class LanguageManager
    {
        public static void ApplyLanguage(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) lang = "ru";

            var app = Application.Current;
            var resources = app.Resources.MergedDictionaries;

            // Load target dictionary
            string fileName = lang.ToLower() switch
            {
                "en" => "Localization/LangENG.xaml",
                _ => "Localization/LangRU.xaml",
            };

            try
            {
                var newDict = new ResourceDictionary { Source = new Uri(fileName, UriKind.Relative) };
                // Add new first to allow overrides
                resources.Add(newDict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LanguageManager: cannot load {fileName}: {ex.Message}");
                return;
            }

            // Remove previous language dictionaries (leave themes and styles)
            var existing = resources.Where(d => d.Source != null && (d.Source.OriginalString.Contains("Localization/LangENG.xaml") || d.Source.OriginalString.Contains("Localization/LangRU.xaml"))).ToList();
            // Remove all but the last added
            for (int i = 0; i < existing.Count - 1; i++)
            {
                resources.Remove(existing[i]);
            }
        }
    }
}
