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
            if (value is Playlist playlist)
            {
                if (playlist.IsSystemPlaylist)
                {
                    return playlist.Name switch
                    {
                        "Favorites" => Application.Current.TryFindResource("LngFavoritesPlaylist")?.ToString() ?? "Favorites",
                        "Top50Tracks" => Application.Current.TryFindResource("LngTop50Tracks")?.ToString() ?? "Top 50 tracks",
                        "TracksWithoutListening" => Application.Current.TryFindResource("LngTrackWithoutListening")?.ToString() ?? "Tracks without listening",
                        _ => playlist.Name ?? "Favorites"
                    };
                }

                return playlist.Name ?? string.Empty;
            }

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
                        "Top50Tracks" => Application.Current.TryFindResource("LngDescriptionTop50Tracks")?.ToString() ?? "Top 50 Tracks",
                        "TracksWithoutListening" => Application.Current.TryFindResource("LngTrackWithoutListening")?.ToString() ?? "Tracks that haven't been listened",
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