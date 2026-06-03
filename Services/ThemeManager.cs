using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QAMP.Models;

namespace QAMP.Services
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            var app = Application.Current;
            var resources = app.Resources.MergedDictionaries;

            Uri themeUri;
            if (themeName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", themeName);
                themeUri = new Uri(fullPath, UriKind.Absolute);
            }
            else
            {
                themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);
            }

            // Попытка подгрузить новую тему, прежде чем убрать старую, чтобы не было провалов в ресурсах
            try
            {
                var newTheme = new ResourceDictionary { Source = themeUri };
                resources.Add(newTheme);
            }
            catch (Exception ex)
            {
                // если тема не найдена, оставляем текущую
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Не удалось загрузить тему {themeName}: {ex.Message}");
                return;
            }

            // Удалить старую тему (если есть)
            var currentTheme = resources.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme") && !d.Source.OriginalString.EndsWith($"{themeName}Theme.xaml", StringComparison.OrdinalIgnoreCase));
            if (currentTheme != null)
            {
                resources.Remove(currentTheme);
            }

            // Обновить AccentBrush
            try
            {
                var accentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SettingsManager.Instance.Config.AccentColor));
                app.Resources["AccentBrush"] = accentBrush;
            }
            catch
            {
                // некорректный цвет акцента не критично
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
                app.Resources["AccentBrush"] = accentBrush;
            }
            catch
            {
            }
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