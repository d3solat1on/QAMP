using System.Globalization;
using System.Windows.Data;
using QAMP.Services;
using QAMP.Models;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;

namespace QAMP.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? ConverterParameter = parameter as string;
            if (value is Track track)
            {
                if (track.CoverImage != null && track.CoverImage.Length > 0 && ConverterParameter == "DataGrid")
                {
                    return CoverImageCacheService.GetImage(track.Path, track.CoverImage, 200);
                }
                if (track.CoverImage != null && track.CoverImage.Length > 0 && ConverterParameter == "Large")
                {
                    return CoverImageCacheService.GetImage(track.Path, track.CoverImage, 700);
                }
                return null;
            }

            if (value is Playlist playlist)
            {
                if (playlist.CoverImage != null && playlist.CoverImage.Length > 0)
                {
                    string playlistKey = $"playlist_{playlist.Name}";
                    return CoverImageCacheService.GetImage(playlistKey, playlist.CoverImage, 200);
                }
                if (playlist.CoverImage != null && playlist.CoverImage.Length > 0 && ConverterParameter == "Large")
                {
                    string playlistKey = $"playlist_{playlist.Name}";
                    return CoverImageCacheService.GetImage(playlistKey, playlist.CoverImage, 700);
                }
                return null;
            }

            if (value is byte[] bytes && bytes.Length > 0)
            {
                string fallbackKey = $"raw_bytes_{bytes.Length}_{bytes.GetHashCode()}";
                return CoverImageCacheService.GetImage(fallbackKey, bytes, 200);
            }
            if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                string imageKey = $"bg_{path}_{File.GetLastWriteTime(path).Ticks}";

                byte[]? imageBytes = null;
                try
                {
                    imageBytes = File.ReadAllBytes(path);
                }
                catch { System.Diagnostics.Debug.WriteLine($"Failed to read image bytes from path: {path}"); }

                if (imageBytes != null)
                {
                    return CoverImageCacheService.GetImage(imageKey, imageBytes, 1920);
                }
            }
            return null;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}