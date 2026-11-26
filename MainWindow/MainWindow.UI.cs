using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            _ = Task.Run(() => DatabaseService.SaveLastTrackPath(path));
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
                UpdateRepeatUI();
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
                UpdateNextTrackUI(track);
                if (_isLyricsMode) UpdateLyricsView();

#if DEBUG
                Title = $"{track.Executor} — {track.Name} | QAMP [DEBUG]";
#else
                Title = $"{track.Executor} — {track.Name} | QAMP";
#endif

                if (_mediaManager != null)
                {
                    _mediaManager.UpdateTrackInfo(track.Name, track.Executor, track.Album, track.CoverImage);
                    _mediaManager.UpdatePlaybackStatus(PlayerService.Instance.IsPlaying);
                }

                string totalTime = Player.Duration > 0 ? FormatTime(Player.Duration) : "Loading...";
                if (Player.Duration <= 0) CheckDurationAsync();
                TotalTimeText.Text = totalTime;

                UpdateTrackCover();

                System.Diagnostics.Debug.WriteLine($"OnTrackChanged successful: {track.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnTrackChanged UI: {ex.Message}");
            }
        });
    }
    private void CurrentTrackImageBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTrackCover();
    }
    private void UpdateTrackCover()
    {
        var track = PlayerService.Instance.CurrentTrack;

        if (track?.CoverImage != null && track.CoverImage.Length > 0 && track.Path != null)
        {
            double controlSize = CurrentTrackImage1.ActualWidth > 0 ? CurrentTrackImage1.ActualWidth : CurrentTrackImage1.Width;
            if (controlSize <= 0) controlSize = 300; // fallback

            var dpiInfo = VisualTreeHelper.GetDpi(CurrentTrackImage1);
            int targetPixelSize = Math.Max(700, (int)(controlSize * dpiInfo.PixelsPerDip));

            var cover = CoverImageCacheService.GetImage(track.Path, track.CoverImage, targetPixelSize);

            if (cover != null)
            {
                CurrentTrackImage.Source = cover;
                CurrentTrackImage.Visibility = Visibility.Visible;
                DefaultCoverPath.Visibility = Visibility.Collapsed;
                return;
            }
        }

        SetDefaultCover();
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
        System.Diagnostics.Debug.WriteLine($"UpdateNowPlayingInfo: {track?.Name ?? "null"}");
        // System.Diagnostics.Debug.WriteLine($"Cover art size for a new track: {MemoryProfiler.GetSizeCoverTrack(track)} bytes");
        // System.Diagnostics.Debug.WriteLine($"Size of cover art for the old track in Library: {MemoryProfiler.GetSizeCoverTrack(Library.CurrentTrack)} bytes");

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

    private void OnRepeatModeChanged(RepeatMode repeatMode)
    {
        void UpdateRepeatState()
        {
            UpdateRepeatUI();
            UpdateNextTrackUI();
        }

        if (Dispatcher.CheckAccess())
        {
            UpdateRepeatState();
        }
        else
        {
            Dispatcher.Invoke(UpdateRepeatState);
        }
    }

    private void UpdateRepeatUI()
    {
        if (RepeatIcon == null) return;

        string geometryResource = _playService.RepeatMode switch
        {
            RepeatMode.RepeatAll => "repeat_onGeometry",
            RepeatMode.RepeatOne => "repeat_one_onGeometry",
            _ => "repeatGeometry"
        };

        RepeatIcon.Data = (Geometry)Application.Current.Resources[geometryResource];
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
                        t.Year = updatedTrack.Year;

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
    private async Task ReplaceBackgroundImageFromPath(string path)
    {
        var config = _settingsManager.Config;
        if (config == null) return;

        try
        {
            string imageKey = $"bg_{path}_{File.GetLastWriteTime(path).Ticks}";
            byte[]? imageBytes = File.ReadAllBytes(path);
            CoverImageCacheService.GetImage(imageKey, imageBytes, 1920);

            config.CustomBackgroundPath = path;
            config.UseCustomBackground = true;
            SettingsManager.Instance.Save();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error replacing background: {ex.Message}");
            App.LogException(ex, "ReplaceBackgroundImageFromPath");
        }
    }
    private void ToggleDebugConsole()
    {
        if (_settingsManager.Config.UseConsole)
        {
            if (DebugConsoleContainer.Visibility == Visibility.Visible)
            {
                DebugConsoleContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                DebugConsoleContainer.Visibility = Visibility.Visible;
            }
        }
    }
    private void ToggleLeftZona_Click(object sender, RoutedEventArgs e)
    {
        ColumnDefinition? playlistColumn = SettingsManager.Instance.Config.IsNowPlayingOnLeft ? RightColumn : LeftColumn;
        if (LeftPanelIsOpen)
        {
            playlistColumn.Width = new GridLength(75);
            LeftPanelIsOpen = false;
            LeftZonaContentGrid.Margin = new Thickness(0);
            LeftZonaHeaderGrid.Margin = new Thickness(0, 0, 0, 0);
            if (LeftZonaContentGrid.RowDefinitions.Count > 0)
            {
                LeftZonaContentGrid.RowDefinitions[0].Height = new GridLength(75);
            }
            LibraryTextBlock.Visibility = Visibility.Collapsed;
            SortByButton.Visibility = Visibility.Collapsed;
            CreatePlaylistButton.Visibility = Visibility.Collapsed;
            RemovePlaylistButton.Visibility = Visibility.Collapsed;
            AddMusicButton.Visibility = Visibility.Collapsed;
            CreateFolderButton.Visibility = Visibility.Collapsed;
            PlaylistsListBox.UpdateLayout();
            RecenterLyricsAfterPanelChange();
            if (playlistColumn == RightColumn)
            {
                ToggleLeftZonaIcon.Data = (Geometry)Application.Current.Resources["ArrowRightGeometry"];
            }
            else
            {
                ToggleLeftZonaIcon.Data = (Geometry)Application.Current.Resources["ArrowLeftGeometry"];

            }
        }
        else
        {
            playlistColumn.Width = new GridLength(250);
            LeftPanelIsOpen = true;
            LeftZonaContentGrid.Margin = new Thickness(10);
            LeftZonaHeaderGrid.Margin = new Thickness(10, 0, 0, 15);
            if (LeftZonaContentGrid.RowDefinitions.Count > 0)
            {
                LeftZonaContentGrid.RowDefinitions[0].Height = GridLength.Auto;
            }
            LibraryTextBlock.Visibility = Visibility.Visible;
            SortByButton.Visibility = Visibility.Visible;
            CreatePlaylistButton.Visibility = Visibility.Visible;
            RemovePlaylistButton.Visibility = Visibility.Visible;
            AddMusicButton.Visibility = Visibility.Visible;
            CreateFolderButton.Visibility = Visibility.Visible;
            PlaylistsListBox.UpdateLayout();
            RecenterLyricsAfterPanelChange();
            ToggleLeftZonaIcon.Data = (Geometry)Application.Current.Resources["ArrowLeftGeometry"];
        }
    }

    public void ApplyPanelLayout()
    {
        var config = SettingsManager.Instance.Config;
        bool nowPlayingOnLeft = config.IsNowPlayingOnLeft;
        bool nowPlayingIsVisible = config.IsNowPlayingIsVisible;

        RightPanelBorder.Visibility = nowPlayingIsVisible ? Visibility.Visible : Visibility.Collapsed;

        Grid.SetColumn(LeftZona, nowPlayingOnLeft ? 2 : 0);
        Grid.SetColumn(RightPanelBorder, nowPlayingOnLeft ? 0 : 2);
        LeftZona.BorderThickness = nowPlayingOnLeft
            ? new Thickness(1, 0, 0, 0)
            : new Thickness(0, 0, 1, 0);
        RightPanelBorder.BorderThickness = nowPlayingOnLeft
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);

        LeftColumn.Width = nowPlayingOnLeft
            ? new GridLength(nowPlayingIsVisible ? 300 : 0)
            : new GridLength(LeftPanelIsOpen ? 250 : 75);
        LeftColumn.MinWidth = nowPlayingOnLeft && !nowPlayingIsVisible ? 0 : 45;
        LeftColumn.MaxWidth = nowPlayingOnLeft ? 300 : 250;
        RightColumn.Width = nowPlayingOnLeft
            ? new GridLength(LeftPanelIsOpen ? 250 : 75)
            : new GridLength(nowPlayingIsVisible ? 300 : 0);

        DockPanel.SetDock(BottomControlPanel, config.IsControlPanelOnTop ? Dock.Top : Dock.Bottom);
        RecenterLyricsAfterPanelChange();
    }

    public void ApplyTrackColumnVisibility()
    {
        var config = SettingsManager.Instance.Config;
        bool showExtendedColumns = !config.IsCompactMode;

        CoverColumn.Visibility = (config.ShowTrackCoverColumn ?? showExtendedColumns)
            ? Visibility.Visible
            : Visibility.Collapsed;
        TitleColumn.Visibility = (config.ShowTrackTitleColumn ?? true) ? Visibility.Visible : Visibility.Collapsed;
        ArtistColumn.Visibility = (config.ShowTrackArtistColumn ?? true) ? Visibility.Visible : Visibility.Collapsed;
        AlbumColumn.Visibility = (config.ShowTrackAlbumColumn ?? true) ? Visibility.Visible : Visibility.Collapsed;
        DurationColumn.Visibility = (config.ShowTrackDurationColumn ?? true) ? Visibility.Visible : Visibility.Collapsed;
        BitrateColumn.Visibility = (config.ShowTrackBitrateColumn ?? showExtendedColumns)
            ? Visibility.Visible
            : Visibility.Collapsed;
        GenreColumn.Visibility = (config.ShowTrackGenreColumn ?? showExtendedColumns)
            ? Visibility.Visible
            : Visibility.Collapsed;
        DateColumn.Visibility = (config.ShowTrackDateColumn ?? showExtendedColumns)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RecenterLyricsAfterPanelChange()
    {
        if (!_isLyricsMode)
        {
            return;
        }

        LyricsOverlay.UpdateLayout();
        LyricsListBox.UpdateLayout();

        if (_lastHighlightedLine != null)
        {
            Dispatcher.BeginInvoke(
                new Action(() => LyricsListBox.ScrollIntoView(_lastHighlightedLine)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}