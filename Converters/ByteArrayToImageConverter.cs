using System.Globalization;
using System.Windows.Data;
using QAMP.Services;
using QAMP.Models;

namespace QAMP.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Track track)
            {
                if (track.CoverImage != null && track.CoverImage.Length > 0)
                {
                    return CoverImageCacheService.GetImage(track.Path, track.CoverImage, 200);
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
                return null;
            }

            if (value is byte[] bytes && bytes.Length > 0)
            {
                string fallbackKey = $"raw_bytes_{bytes.Length}_{bytes.GetHashCode()}";
                return CoverImageCacheService.GetImage(fallbackKey, bytes, 200);
            }
            return null;
        }
        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}