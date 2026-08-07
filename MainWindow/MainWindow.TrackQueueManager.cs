using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{
    private readonly Stack<List<int>> _trackReorderUndoStack = new();
    private Track? _draggedTrack;
    private Point? _dragStartPoint;

    public void RebuildPlaybackQueue()
    {
        Track? currentTrack = Player.CurrentTrack;

        if (_playService.IsShuffleEnabled)
        {
            if (_playService.ShuffledQueue.Count == 0)
            {
                var sourceQueue = _playService._actualPlayingQueue.ToList();

                if (sourceQueue.Count > 0)
                {
                    var shuffledList = sourceQueue.OrderBy(x => Guid.NewGuid()).ToList();

                    if (currentTrack != null)
                    {
                        var existing = shuffledList.FirstOrDefault(t => t.Path == currentTrack.Path);
                        if (existing != null) shuffledList.Remove(existing);
                        shuffledList.Insert(0, currentTrack);
                    }

                    _playService.ShuffledQueue = shuffledList;
                }
            }
        }
        else
        {
            _playService.ShuffledQueue.Clear();
        }
        SyncPlaybackQueueWithCurrentState();
    }
    public void SyncPlaybackQueueWithCurrentState()
    {
        MusicLibrary.Instance.PlaybackQueue.Clear();

        var queueToLoad = _playService.IsShuffleEnabled
            ? _playService.ShuffledQueue
            : _playService._actualPlayingQueue;

        foreach (var track in queueToLoad)
        {
            MusicLibrary.Instance.PlaybackQueue.Add(track);
        }

        UpdateNextTrackUI();
    }

    public void PlayNext(Track nextTrack)
    {
        if (nextTrack == null) return;

        Track? currentTrack = Player.CurrentTrack;

        _playService._actualPlayingQueue.Remove(nextTrack);
        _playService.ShuffledQueue.Remove(nextTrack);

        if (currentTrack != null)
        {
            int currentIndex = _playService._actualPlayingQueue.IndexOf(currentTrack);
            if (currentIndex != -1)
            {
                _playService._actualPlayingQueue.Insert(currentIndex + 1, nextTrack);
            }
            else
            {
                _playService._actualPlayingQueue.Insert(0, nextTrack);
            }
        }

        else
        {
            _playService._actualPlayingQueue.Insert(0, nextTrack);
        }

        if (_playService.IsShuffleEnabled)
        {
            if (currentTrack != null)
            {
                int currentIndex = _playService.ShuffledQueue.IndexOf(currentTrack);
                if (currentIndex != -1)
                {
                    _playService.ShuffledQueue.Insert(currentIndex + 1, nextTrack);
                }
                else
                {
                    _playService.ShuffledQueue.Insert(0, nextTrack);
                }
            }
            else
            {
                _playService.ShuffledQueue.Insert(0, nextTrack);
            }
        }

        MusicLibrary.Instance.PlaybackQueue.Clear();

        var queueToLoad = _playService.IsShuffleEnabled
            ? _playService.ShuffledQueue
            : _playService._actualPlayingQueue;

        foreach (var track in queueToLoad)
        {
            MusicLibrary.Instance.PlaybackQueue.Add(track);
        }
        UpdateNextTrackUI();
    }

    private void PlayNextFromDataGrid_Click(object sender, RoutedEventArgs e)
    {
        if (TracksDataGrid.SelectedItem is Track selectedTrack)
        {
            PlayNext(selectedTrack);
            UpdateNextTrackUI();
        }
    }
    public void UpdateNextTrackUI(Track? currentTrack = null)
    {
        if (Player.RepeatMode == RepeatMode.RepeatOne)
        {
            var current = Player.CurrentTrack;
            if (current != null)
            {
                string text = Application.Current.FindResource("LngRepeatTrack") as string ?? "Repeat current track";
                NextTrackName.Text = text;
            }
        }
        else
        {
            var next = _playService.GetNextTrack();

            if (next != null)
            {
                NextTrackName.Text = $"{next.Executor} - {next.Name}";
            }
            else
            {
                string text = Application.Current.FindResource("LngPlaylistEnded") as string ?? "Playlist has ended";
                NextTrackName.Text = text;
            }
        }
    }
    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (Player.CurrentTrack == null)
        {
            _ = MyToast.ShowAsync(Application.Current.FindResource("LngNoTrackToPlay") as string ?? "No track to play");
            return;
        }
        Player.PlayPreviousTrack();

        if (_isLyricsMode)
        {
            UpdateLyricsView();
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        Player.PlayNextTrack();

        UpdateNextTrackUI();
        if (_isLyricsMode) UpdateLyricsView();
    }



    private void ShuffleButton_Click(object? sender, RoutedEventArgs? e)
    {
        _playService.IsShuffleEnabled = !_playService.IsShuffleEnabled;

        RebuildPlaybackQueue();

        UpdateShuffleUI();
    }

    public void TogglePlayPause()
    {
        if (Player.CurrentTrack == null)
        {
            if (MusicLibrary.Instance.PlaybackQueue.Count > 0)
            {
                var track = MusicLibrary.Instance.PlaybackQueue[0];
                _ = Player.PlayTrack(track);
                UpdateNextTrackUI();
            }
        }
        else if (Player.IsPlaying)
        {
            _ = Player.PauseAsync();
        }
        else
        {
            Player.Resume();
        }
        UpdatePlayPauseIconState();
        UpdateShuffleUI();
        UpdateOSD();
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void PlayPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        if (Library.CurrentPlaylist == null || Library.CurrentPlaylist.Tracks.Count == 0)
        {
            _ = MyToast.ShowAsync(Application.Current.FindResource("LngPlaylistEmpty") as string ?? "Playlist is empty");
            return;
        }

        if (Player.CurrentTrack != null && Library.PlayingPlaylist != null &&
            Library.PlayingPlaylist.Id != Library.CurrentPlaylist.Id)
        {
            StartPlayingPlaylist("from different");
            return;
        }

        if (Player.IsPlaying)
        {
            _ = Player.PauseAsync();
            UpdatePlayPauseIconState();
            UpdateShuffleUI();
            UpdateOSD();
            return;
        }

        if (Player.CurrentTrack != null && Library.CurrentPlaylist.Tracks.Contains(Player.CurrentTrack))
        {
            Player.Resume();
            UpdatePlayPauseIconState();
            UpdateShuffleUI();
            UpdateOSD();
            return;
        }

        StartPlayingPlaylist("");
    }

    private void StartPlayingPlaylist(string logSuffix)
    {
        Library.PlayingPlaylist = Library.CurrentPlaylist;

        var firstTrack = Library.CurrentPlaylist!.Tracks[0];
        string? logMsg;
        if (string.IsNullOrEmpty(logSuffix))
        {
            logMsg = $"PlayPlaylist: {Library.CurrentPlaylist.Name} | Track: {firstTrack.Executor} - {firstTrack.Name}";
        }
        else
        {
            logMsg = $"PlayPlaylist ({logSuffix}): {Library.CurrentPlaylist.Name} | Track: {firstTrack.Executor} - {firstTrack.Name}";
        }

        _ = Player.PlayTrack(firstTrack, true);

        Library.PlaybackQueue.Clear();
        foreach (var track in Library.CurrentPlaylist.Tracks)
        {
            Library.PlaybackQueue.Add(track);
        }
        if (_playService.IsShuffleEnabled)
        {
            var shuffledList = Library.PlaybackQueue.ToList();
            if (firstTrack != null && shuffledList.Contains(firstTrack))
            {
                _ = shuffledList.Remove(firstTrack);
            }
            shuffledList = [.. shuffledList.OrderBy(x => Guid.NewGuid())];
            if (firstTrack != null)
            {
                shuffledList.Insert(0, firstTrack);
            }
            _playService.ShuffledQueue = shuffledList;
        }
        UpdatePlayPauseIconState();
        UpdateShuffleUI();
        UpdateNextTrackUI();
        UpdateOSD();
    }
    private void RepeatButton_Click(object? sender, RoutedEventArgs? e)
    {
        var resources = Application.Current.Resources;

        switch (Player.RepeatMode)
        {
            case RepeatMode.NoRepeat:
                Player.RepeatMode = RepeatMode.RepeatAll;
                RepeatIcon.Data = (Geometry)resources["repeat_onGeometry"];
                break;

            case RepeatMode.RepeatAll:
                Player.RepeatMode = RepeatMode.RepeatOne;
                RepeatIcon.Data = (Geometry)resources["repeat_one_onGeometry"];
                break;

            case RepeatMode.RepeatOne:
                Player.RepeatMode = RepeatMode.NoRepeat;
                RepeatIcon.Data = (Geometry)resources["repeatGeometry"];
                break;
        }
        UpdateNextTrackUI();
    }

    private void PlayTrackMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TracksDataGrid.SelectedItem is Track selectedTrack)
        {
            if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
            {
                var displayOrder = TracksDataGrid.ItemsSource as IEnumerable<Track>;
                MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist, displayOrder);
                UpdateNextTrackUI();
            }
        }
    }
    private void TracksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TracksDataGrid.SelectedItem is Track selectedTrack)
        {
            if (PlaylistsListBox.SelectedItem is Playlist currentPlaylist)
            {
                var displayOrder = TracksDataGrid.ItemsSource as IEnumerable<Track>;
                MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist, displayOrder);
                UpdateNextTrackUI();
            }
        }
    }

    private void TracksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _scrollCleanupTimer?.Stop();
        _scrollCleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _scrollCleanupTimer.Tick += (s, args) =>
        {
            _scrollCleanupTimer?.Stop();
            CoverImageCacheService.ForceGarbageCollection();
        };
        _scrollCleanupTimer.Start();
    }

    private void TracksDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var row = FindVisualParent<DataGridRow>(source);
        if (row?.Item is Track track)
        {
            _draggedTrack = track;
            _dragStartPoint = e.GetPosition(TracksDataGrid);
        }
    }

    private void TracksDataGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedTrack == null || e.LeftButton != MouseButtonState.Pressed || _dragStartPoint is not Point startPoint)
        {
            return;
        }

        var currentPoint = e.GetPosition(TracksDataGrid);
        if (Math.Abs(currentPoint.X - startPoint.X) < 2 && Math.Abs(currentPoint.Y - startPoint.Y) < 2)
        {
            return;
        }

        var data = new DataObject("QampTrackReorder", _draggedTrack);
        DragDrop.DoDragDrop(TracksDataGrid, data, DragDropEffects.Move);
        _draggedTrack = null;
    }

    private void TracksDataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("QampTrackReorder"))
        {
            if (e.GetPosition(TracksDataGrid) is Point point)
            {
                ScrollDataGridDuringDrag(point);
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (ViewModels.MusicLibrary.Instance.CurrentPlaylist != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void TracksDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (paths != null && paths.Length > 0)
            {
                await ProcessDroppedPaths(paths);
            }
            return;
        }

        if (e.Data.GetData("QampTrackReorder") is not Track droppedTrack)
        {
            return;
        }

        var currentPlaylist = Library.CurrentPlaylist;
        if (currentPlaylist == null)
        {
            return;
        }

        var targetTrack = FindTrackFromDependencyObject(e.OriginalSource as DependencyObject);
        targetTrack ??= currentPlaylist.Tracks.LastOrDefault();

        if (targetTrack == null || targetTrack == droppedTrack)
        {
            return;
        }

        _trackReorderUndoStack.Push(currentPlaylist.Tracks.Select(t => t.Id).ToList());

        int oldIndex = currentPlaylist.Tracks.IndexOf(droppedTrack);
        int newIndex = currentPlaylist.Tracks.IndexOf(targetTrack);
        if (oldIndex < 0 || newIndex < 0)
        {
            return;
        }

        currentPlaylist.Tracks.RemoveAt(oldIndex);
        currentPlaylist.Tracks.Insert(newIndex, droppedTrack);

        currentPlaylist.SortType = TrackSortType.CustomOrder;
        DatabaseService.UpdatePlaylistSortType(currentPlaylist.Id, TrackSortType.CustomOrder);
        DatabaseService.SavePlaylistTrackOrder(currentPlaylist.Id, currentPlaylist.Tracks);

        TracksDataGrid.ItemsSource = null;
        TracksDataGrid.ItemsSource = currentPlaylist.Tracks;
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null)
        {
            return null;
        }

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T found)
            {
                return found;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static Track? FindTrackFromDependencyObject(DependencyObject? source)
    {
        if (source == null)
        {
            return null;
        }

        if (source is DataGridRow row && row.Item is Track track)
        {
            return track;
        }

        if (source is DependencyObject current)
        {
            var parent = VisualTreeHelper.GetParent(current);
            while (parent != null)
            {
                if (parent is DataGridRow parentRow && parentRow.Item is Track parentTrack)
                {
                    return parentTrack;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        return null;
    }
    protected void TracksDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Z)
        {
            UndoLastTrackReorder();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            var currentPlaylist = Library.CurrentPlaylist;
            if (currentPlaylist == null) return;

            var tracksToDelete = TracksDataGrid.SelectedItems.Cast<Track>().ToList();
            if (tracksToDelete.Count == 0) return;

            string confirmMessage = Application.Current.FindResource("LngDeleteTracksConfirm") as string
                                    ?? "Вы уверены, что хотите удалить выбранные треки?";

            var result = NotificationWindow.Show(confirmMessage, this, NotificationWindow.NotificationMode.Confirm);
            if (result == true)
            {
                foreach (var track in tracksToDelete)
                {
                    DatabaseService.RemoveTrackFromPlaylist(currentPlaylist.Id, track.Id);
                    currentPlaylist.Tracks.Remove(track);
                }
            }
            e.Handled = true;
        }
    }

    private void UndoLastTrackReorder()
    {
        var currentPlaylist = Library.CurrentPlaylist;
        if (currentPlaylist == null || _trackReorderUndoStack.Count == 0)
        {
            return;
        }

        var previousOrder = _trackReorderUndoStack.Pop();
        var trackMap = currentPlaylist.Tracks.ToDictionary(t => t.Id);
        var restoredTracks = previousOrder
            .Where(trackMap.ContainsKey)
            .Select(id => trackMap[id])
            .ToList();

        if (restoredTracks.Count != currentPlaylist.Tracks.Count)
        {
            return;
        }

        currentPlaylist.Tracks.Clear();
        foreach (var track in restoredTracks)
        {
            currentPlaylist.Tracks.Add(track);
        }

        currentPlaylist.SortType = TrackSortType.CustomOrder;
        DatabaseService.UpdatePlaylistSortType(currentPlaylist.Id, TrackSortType.CustomOrder);
        DatabaseService.SavePlaylistTrackOrder(currentPlaylist.Id, currentPlaylist.Tracks);

        TracksDataGrid.ItemsSource = null;
        TracksDataGrid.ItemsSource = currentPlaylist.Tracks;
    }

    private void ScrollDataGridDuringDrag(Point point)
    {
        if (TracksDataGrid.Items.Count == 0)
        {
            return;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(TracksDataGrid);
        if (scrollViewer == null)
        {
            return;
        }

        const double edgeThreshold = 35;
        const double scrollStep = 12;
        double currentOffset = scrollViewer.VerticalOffset;

        if (point.Y < edgeThreshold && currentOffset > 0)
        {
            scrollViewer.ScrollToVerticalOffset(Math.Max(0, currentOffset - scrollStep));
        }
        else if (point.Y > TracksDataGrid.ActualHeight - edgeThreshold && currentOffset < scrollViewer.ScrollableHeight)
        {
            scrollViewer.ScrollToVerticalOffset(Math.Min(scrollViewer.ScrollableHeight, currentOffset + scrollStep));
        }
    }
}