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
            string themeName = config.ColorScheme ?? "Dark";

            if (!themeName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                themeName = $"{themeName}Theme.xaml";
            }

            string themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", themeName);

            if (!File.Exists(themePath))
            {
                themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "DarkTheme.xaml");
                if (!File.Exists(themePath))
                {
                    System.Diagnostics.Debug.WriteLine("ThemeManager: Тема не найдена!");
                    return;
                }
            }

            try
            {
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };

                var app = Application.Current;

                var oldThemes = app.Resources.MergedDictionaries
                    .Where(d => d.Source != null &&
                               (d.Source.ToString().Contains("Theme") ||
                                d.Source.ToString().Contains("Themes/")))
                    .ToList();

                foreach (var old in oldThemes)
                {
                    app.Resources.MergedDictionaries.Remove(old);
                }

                app.Resources.MergedDictionaries.Insert(0, themeDict);

                UpdateAccentColor(config.AccentColor);

                System.Diagnostics.Debug.WriteLine($"ThemeManager: Загружена тема {themeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Ошибка загрузки темы: {ex.Message}");
            }
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

            System.Diagnostics.Debug.WriteLine($"ThemeManager: Тема сохранена в конфиг: {themeName}");

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
                app.Resources["AccentBrush"] = accentBrush;
            }
            catch { }
        }
    }
    public class ThemeHelper
    {
        public static Color GetDominantColor(BitmapSource bitmapSource)
        {
            var colorThief = new ColorThiefDotNet.ColorThief();
            // ColorThief работает с Bitmap, поэтому конвертируем
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