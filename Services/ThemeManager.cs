using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QAMP.Models;

namespace QAMP.Services
{
    public static class ThemeManager
    {
        public static void LoadThemeFromConfig()
        {
            var config = SettingsManager.Instance.Config;
            if (!TryApplyThemeResources(config.ColorScheme))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ThemeManager: Failed to apply theme from settings: {config.ColorScheme}");
                return;
            }

            UpdateAccentColor(config.AccentColor);
        }

        public static void SetTheme(string themeName)
        {
            var config = SettingsManager.Instance.Config;

            if (themeName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                config.ColorScheme = themeName;
            }
            else
            {
                config.ColorScheme = themeName;
            }

            SettingsManager.Instance.Save();

            System.Diagnostics.Debug.WriteLine($"ThemeManager: Theme saved in config: {themeName}");
        }

        public static bool ApplyTheme(string themeName)
        {
            if (!TryApplyThemeResources(themeName))
            {
                return false;
            }

            var config = SettingsManager.Instance.Config;
            config.ColorScheme = themeName;
            SettingsManager.Instance.Save();
            UpdateAccentColor(config.AccentColor);
            config.NotifyThemeResourcesChanged();
            System.Diagnostics.Debug.WriteLine($"ThemeManager: Theme applied: {themeName}");
            return true;
        }

        private static bool TryApplyThemeResources(string themeName)
        {
            string fileName = themeName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                ? themeName
                : $"{themeName}Theme.xaml";
            string themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", fileName);

            if (!File.Exists(themePath))
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Theme file not found: {themePath}");
                return false;
            }

            try
            {
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };
                var app = Application.Current;
                var oldThemes = app.Resources.MergedDictionaries
                    .Where(dictionary => dictionary.Source != null &&
                        dictionary.Source.ToString().Contains("Themes/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var oldTheme in oldThemes)
                {
                    app.Resources.MergedDictionaries.Remove(oldTheme);
                }

                app.Resources.MergedDictionaries.Insert(0, themeDict);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error loading theme '{themeName}': {ex}");
                return false;
            }
        }

        public static void UpdateAccentColor(string colorHex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(colorHex) || !colorHex.StartsWith("#")) return;

                var app = Application.Current;
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var accentBrush = new SolidColorBrush(color);
                accentBrush.Freeze();
                app.Resources["AccentColor"] = color;
                app.Resources["AccentBrush"] = accentBrush;
            }
            catch { }
        }
        public static bool IsThemeValid(string filePath, out string errorMessage)
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

                var defaultThemeUri = new Uri(";component/Themes/DarkTheme.xaml", UriKind.RelativeOrAbsolute);
                var baseThemeDict = new ResourceDictionary { Source = defaultThemeUri };

                foreach (var key in baseThemeDict.Keys)
                {
                    if (!customDict.Contains(key))
                    {
                        string message = (string)Application.Current.FindResource("LngThemeMissingResource");
                        errorMessage = $"{message} '{key}'";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                string message = (string)Application.Current.FindResource("LngXAMLReadingError");
                errorMessage = $"{message} {ex.Message}";
                return false;
            }
        }
    }
    public class ThemeHelper
    {
        public static Color GetDominantColor(BitmapSource bitmapSource)
        {
            var colorThief = new ColorThiefDotNet.ColorThief();
            using var memoryStream = new System.IO.MemoryStream();
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);
            using var bitmap = new System.Drawing.Bitmap(memoryStream);
            var quantizeColor = colorThief.GetColor(bitmap);
            return Color.FromRgb(quantizeColor.Color.R, quantizeColor.Color.G, quantizeColor.Color.B);
        }
        public static Color GetAdaptiveSecondaryColor(Color baseColor)
        {
            double luminance = (0.299 * baseColor.R + 0.587 * baseColor.G + 0.114 * baseColor.B) / 255;

            if (luminance > 0.6)
            {
                return Color.FromRgb(
                    (byte)Math.Min(255, baseColor.R * 0.75 + 255 * 0.25),
                    (byte)Math.Min(255, baseColor.G * 0.75 + 255 * 0.25),
                    (byte)Math.Min(255, baseColor.B * 0.75 + 255 * 0.25)
                );
            }
            else
            {
                return Color.FromRgb(
                    (byte)(baseColor.R * 0.12),
                    (byte)(baseColor.G * 0.12),
                    (byte)(baseColor.B * 0.12)
                );
            }
        }
    }
}