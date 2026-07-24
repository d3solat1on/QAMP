using System.Windows;
using System.Windows.Media;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{

    // main
    private async void OnTrackChanged(Track? track)
    {
        if (track != null)
        {
            string path = track.Path ?? string.Empty;
            _ = Task.Run(() => DatabaseService.SaveSettingSync("LastTrackPath", path));
        }

        System.Windows.Media.ImageSource? preparedCover = null;
        if (track != null && track.CoverImage != null)
        {
            preparedCover = await Task.Run(() =>
            {
                try
                {
                    return _imageConverter.Convert(track.CoverImage, typeof(System.Windows.Media.Imaging.BitmapSource), null, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.ImageSource;
                }
                catch
                {
                    return null;
                }
            });
        }

        Dispatcher.Invoke(() =>
        {
            try
            {
                UpdateNowPlayingInfo(track);

                if (track == null)
                {
                    ResetPlayerUI();
                    return;
                }

                UpdatePlayPauseIconState();
                UpdateShuffleUI();
                UpdateFavoriteIcon(track);
                FavoriteButton1Grid.Visibility = Visibility.Visible;

                CurrentTrackName.Text = track.Name;
                CurrentTrackExecutor.Text = track.Executor;
                CurrentTrackAlbum.Text = track.Album;
                CurrentTrackData.Text = $"{track.Genre} | {track.Duration} | {track.SampleRate} Hz | {track.Bitrate} kbps";
                CurrentTrackExtension.Text = track.DisplayExtension;
                CurrentTrackYear.Text = track.Year > 0 ? track.Year.ToString() : "Unknown year";

                NextTrack.Text = "Next Track";
                NowPlaying.Text = "NOW PLAYING";

                Title = $"{track.Executor} — {track.Name} | QAMP";

                if (_mediaManager != null)
                {
                    _mediaManager.UpdateTrackInfo(track.Name, track.Executor, track.Album);
                    _mediaManager.UpdatePlaybackStatus(PlayerService.Instance.IsPlaying);
                }

                string totalTime = Player.Duration > 0 ? FormatTime(Player.Duration) : "Loading...";
                if (Player.Duration <= 0) CheckDurationAsync();
                TotalTimeText.Text = totalTime;

                if (preparedCover != null)
                {
                    CurrentTrackImage.Source = preparedCover;
                    CurrentTrackImage.Visibility = Visibility.Visible;
                    DefaultCoverPath.Visibility = Visibility.Collapsed;
                    CurrentTrackImage.Stretch = System.Windows.Media.Stretch.UniformToFill;
                    CurrentTrackImage.Margin = new Thickness(0);
                }
                else
                {
                    SetDefaultCover();
                }

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTrackChanged Успешно: {track.Name}");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Error in OnTrackChanged UI: {ex.Message}");
            }
        });
    }

    private void ResetPlayerUI()
    {
        SetDefaultCover();
        FavoriteButton1Grid.Visibility = Visibility.Collapsed;

        CurrentTrackName.Text = string.Empty;
        CurrentTrackExecutor.Text = string.Empty;
        CurrentTrackAlbum.Text = string.Empty;
        CurrentTrackData.Text = string.Empty;
        CurrentTrackExtension.Text = string.Empty;
        CurrentTrackYear.Text = string.Empty;

        Title = "QAMP";
        _mediaManager?.UpdatePlaybackStatus(false);
    }

    private void SetDefaultCover()
    {
        CurrentTrackImage.Source = null;
        CurrentTrackImage.Visibility = Visibility.Collapsed;
        DefaultCoverPath.Visibility = Visibility.Visible;
    }
    private void UpdateNowPlayingInfo(Track? track)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"UpdateNowPlayingInfo: {track?.Name ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"Размер обложки нового трека: {MemoryProfiler.GetSizeCoverTrack(track)} байт");
        System.Diagnostics.Debug.WriteLine($"Размер обложки старого трека из Library: {MemoryProfiler.GetSizeCoverTrack(Library.CurrentTrack)} байт");
#endif

        Library.CurrentTrack?.UnloadCover();
        if (track == null)
        {
            CurrentTrackTitleLeftBottomPanel.Text = string.Empty;
            CurrentTrackArtistLeftBottomPanel.Text = string.Empty;
            UpperPanel.Background = (Brush)Application.Current.Resources["BackgroundBrush"];

            Library.CurrentTrack = null;
            return;
        }

        CurrentTrackTitleLeftBottomPanel.Text = track.Name;
        CurrentTrackArtistLeftBottomPanel.Text = track.Executor;

        Library.CurrentTrack = track;
    }

    public void UpdateIcons()
    {
        FavoriteIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
        ShuffleImage.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
        ShuffleImageContorlPanel.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
    }

    private void UpdateShuffleUI()
    {
        bool isShuffle = _playService.IsShuffleEnabled;

        if (ShuffleImage != null)
        {
            ShuffleImage.Data = (Geometry)Application.Current.Resources[isShuffle ? "shuffle_OnGeometry" : "shuffleGeometry"];
            ShuffleImage.Fill = (Brush)Application.Current.Resources["AccentBrush"];
        }

        if (ShuffleImageContorlPanel != null)
        {
            var playingPlaylist = MusicLibrary.Instance.PlayingPlaylist;

            bool isCurrentPlaylistShuffled = isShuffle &&
                                            PlaylistsListBox.SelectedItem is Playlist displayedPlaylist &&
                                            playingPlaylist != null &&
                                            displayedPlaylist.Id == playingPlaylist.Id;

            ShuffleImageContorlPanel.Data = (Geometry)Application.Current.Resources[isCurrentPlaylistShuffled ? "shuffle_OnGeometry" : "shuffleGeometry"];
            ShuffleImageContorlPanel.Fill = (Brush)Application.Current.Resources["AccentBrush"];
        }
    }

    private void UpdatePlayPauseIconState()
    {
        bool hasTrack = Player.CurrentTrack != null;

        bool hasPlayingPlaylist = Library.PlayingPlaylist != null;

        bool isPlaying = Player.IsPlaying;

        bool shouldShowPlaying = hasTrack && hasPlayingPlaylist && isPlaying;

        UpdatePlayPauseIcon(shouldShowPlaying);
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    private void UpdatePlayPauseIcon(bool isPlaying)
    {
        var playingPlaylist = MusicLibrary.Instance.PlayingPlaylist;

        var globalGeometry = isPlaying
            ? (Geometry)Application.Current.Resources["pauseGeometry"]
            : (Geometry)Application.Current.Resources["playGeometry"];

        bool isCurrentPlaylistPlaying = isPlaying &&
                                        PlaylistsListBox.SelectedItem is Playlist displayedPlaylist &&
                                        playingPlaylist != null &&
                                        displayedPlaylist.Id == playingPlaylist.Id;

        var contextGeometry = isCurrentPlaylistPlaying
            ? (Geometry)Application.Current.Resources["pauseGeometry"]
            : (Geometry)Application.Current.Resources["playGeometry"];

        PlayPauseIcon1.Data = contextGeometry;
        PlayPauseIcon.Data = globalGeometry;

        PlayPauseIcon.InvalidateVisual();
        PlayPauseIcon1.InvalidateVisual();
        _mediaManager?.UpdatePlaybackStatus(isPlaying);
    }
    public void RefreshSingleTrackInUI(Track updatedTrack)
    {
        if (updatedTrack == null || TracksDataGrid == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (TracksDataGrid.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is Track t && t.Path == updatedTrack.Path)
                    {
                        t.Lyrics = updatedTrack.Lyrics;
                        t.Name = updatedTrack.Name;
                        t.Executor = updatedTrack.Executor;
                        t.Genre = updatedTrack.Genre;
                        t.Album = updatedTrack.Album;

                        if (TracksDataGrid.ItemContainerGenerator.ContainerFromItem(t) is FrameworkElement itemProperties)
                        {
                            TracksDataGrid.Items.Refresh();
                        }
                        break;
                    }
                }
            }
        });
    }
}