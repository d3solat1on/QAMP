using System.Globalization;
using System.Windows;
using System.Windows.Data;
using QAMP.Models;

namespace QAMP.Converters
{
    public class PlaylistNameLocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, что value не null и является Playlist
            if (value is Playlist playlist)
            {
                // Для системных плейлистов - локализуем имя
                if (playlist.IsSystemPlaylist)
                {
                    return playlist.Name switch
                    {
                        "Favorites" => Application.Current.TryFindResource("LngFavoritesPlaylist")?.ToString() ?? "Favorites",
                        _ => playlist.Name ?? "Favorites"
                    };
                }

                // Для обычных плейлистов - возвращаем их имя
                return playlist.Name ?? string.Empty;
            }

            // Если value не Playlist или null
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PlaylistDescriptionLocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Playlist playlist)
            {
                if (playlist.IsSystemPlaylist)
                {
                    return playlist.Name switch
                    {
                        "Favorites" => Application.Current.TryFindResource("LngFavoritesDescription")?.ToString() ?? "Your favorite tracks",
                        _ => playlist.Description ?? string.Empty
                    };
                }
                return playlist.Description ?? string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}