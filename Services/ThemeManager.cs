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

            string themePath = $"Themes/{themeName}Theme.xaml";

            // Попытка подгрузить новую тему, прежде чем убрать старую, чтобы не было провалов в ресурсах
            try
            {
                var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
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
                var accentBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SettingsManager.Instance.Config.AccentColor));
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
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                var accentBrush = new System.Windows.Media.SolidColorBrush(color);

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
    }
}