using System.Windows;
using System.Windows.Media;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{
    private async void FavoriteButton_Click(object? sender, RoutedEventArgs? e)
    {
        if (Player.CurrentTrack == null)
        {
            string message = Application.Current.Resources["LngSelectTrackFirst"] as string ?? "Please select a track first.";
            NotificationWindow.Show(message, this);
            return;
        }

        var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
        if (favoritePlaylist == null)
        {

            string message = Application.Current.Resources["LngErrorFavoritesNotFound"] as string ?? "Error: Favorites playlist not found";
            NotificationWindow.Show(message, this);
            return;
        }

        bool isAlreadyFavorite = favoritePlaylist.Tracks.Any(t => t.Path == Player.CurrentTrack.Path);
        if (!isAlreadyFavorite)
        {
            var trackToSave = TagReader.GetFullTrackInfo(Player.CurrentTrack.Path) ?? Player.CurrentTrack;

            trackToSave.AddedDate = DateTime.Now;
            trackToSave.PlayCount = Player.CurrentTrack.PlayCount;

            DatabaseService.SaveTrackToPlaylist(favoritePlaylist.Id, trackToSave);
            favoritePlaylist.Tracks.Add(trackToSave);
            UpdateFavoriteIcon(Player.CurrentTrack, true);
            string successMessage = Application.Current.Resources["LngTrackAddedToFavorites"] as string ?? "Successfully added to favorites";
            await MyToast.ShowAsync(successMessage);
            System.Diagnostics.Debug.WriteLine($"DEBUG: Трек \"{trackToSave.Name}\" добавлен в Избранное. Путь: {trackToSave.Path}");
        }
        else
        {
            string successMessage = Application.Current.Resources["LngTrackRemovedFromFavorites"] as string ?? "Successfully removed from favorites";
            await MyToast.ShowAsync(successMessage);
            System.Diagnostics.Debug.WriteLine($"DEBUG: Трек \"{Player.CurrentTrack.Name}\" удален из Избранного. Путь: {Player.CurrentTrack.Path}");
            DatabaseService.RemoveTrackFromPlaylist(favoritePlaylist.Id, Player.CurrentTrack.Id);
            var trackToRemove = favoritePlaylist.Tracks.FirstOrDefault(t => t.Id == Player.CurrentTrack.Id);
            if (trackToRemove != null)
            {
                favoritePlaylist.Tracks.Remove(trackToRemove);
            }
            UpdateFavoriteIcon(Player.CurrentTrack, false);
        }

        if (Library.CurrentPlaylist?.Id == favoritePlaylist.Id)
        {
            TracksDataGrid.ItemsSource = null;
            TracksDataGrid.ItemsSource = favoritePlaylist.Tracks;
        }
    }
    private void UpdateFavoriteIcon(Track track, bool? forceState = null)
    {
        if (FavoriteIcon == null) return;

        bool isFavorite;
        if (forceState.HasValue)
        {
            isFavorite = forceState.Value;
        }
        else if (track != null)
        {
            var favoritePlaylist = Library.Playlists.FirstOrDefault(p => p.Name == MusicLibrary.FavoritesName);
            isFavorite = favoritePlaylist?.Tracks.Any(t => t.Path == track.Path) ?? false;
        }
        else
        {
            isFavorite = false;
        }

        FavoriteIcon.Data = isFavorite
            ? (Geometry)Application.Current.Resources["favorites_addedGeometry"]
            : (Geometry)Application.Current.Resources["add_favoritesGeometry"];

        FavoriteButton.ToolTip = isFavorite ? Application.Current.Resources["LngRemoveFromFavorites"] : Application.Current.Resources["LngAddToFavorites"];
        FavoriteIcon.Fill = (Brush)Application.Current.Resources["AccentBrush"];
    }
}