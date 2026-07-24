using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Models;
using QAMP.ViewModels;

namespace QAMP;

public partial class MainWindow
{
    private Point _queueDragStartPoint;
    private Track? _queueDraggedTrack;
    private void OpenQueue_Click(object sender, RoutedEventArgs e)
    {
        if (QueuePanel.Visibility == Visibility.Collapsed)
        {
            QueuePanel.Visibility = Visibility.Visible;

            if (MusicLibrary.Instance != null && _playService != null)
            {
                MusicLibrary.Instance.PlaybackQueue.Clear();

                var activeSource = _playService.IsShuffleEnabled
                    ? _playService.ShuffledQueue
                    : _playService._actualPlayingQueue;

                foreach (var track in activeSource)
                {
                    MusicLibrary.Instance.PlaybackQueue.Add(track);
                }

                QueueListBox.ItemsSource = MusicLibrary.Instance.PlaybackQueue;
            }
        }
        else
        {
            QueuePanel.Visibility = Visibility.Collapsed;
        }
    }
    private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
    {
        if (QueueListBox.SelectedItem is Track selectedTrack)
        {
            _playService._actualPlayingQueue.Remove(selectedTrack);

            if (_playService.IsShuffleEnabled)
            {
                _playService.ShuffledQueue.Remove(selectedTrack);
            }
            SyncPlaybackQueueWithCurrentState();
        }
    }
    private void PlayFromQueue_Click(object sender, RoutedEventArgs e)
    {
        if (QueueListBox.SelectedItem is Track selectedTrack)
        {
            var playingPlaylist = MusicLibrary.Instance.PlayingPlaylist;

            if (playingPlaylist != null)
            {
                var currentOrder = MusicLibrary.Instance.PlaybackQueue.ToList();

                MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, playingPlaylist, currentOrder, bypassShuffle: true);
            }
            else
            {
                if (MusicLibrary.Instance.CurrentPlaylist != null)
                {
                    var currentPlaylist = MusicLibrary.Instance.CurrentPlaylist;
                    MusicLibrary.Instance.PlayTrackFromPlaylist(selectedTrack, currentPlaylist);
                }
            }
            if (_isLyricsMode) UpdateLyricsView();
        }
    }
    private void QueueListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _queueDragStartPoint = e.GetPosition(null);
        if (ItemsControl.ContainerFromElement(QueueListBox, e.OriginalSource as DependencyObject) is ListBoxItem item)
        {
            _queueDraggedTrack = item.Content as Track;
        }
    }

    private void QueueListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _queueDraggedTrack != null)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _queueDragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(QueueListBox, new DataObject("QueueTrackItem", _queueDraggedTrack), DragDropEffects.Move);
            }
        }
    }

    private void QueueListBox_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("QueueTrackItem"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void QueueListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("QueueTrackItem"))
        {
            if (e.Data.GetData("QueueTrackItem") is not Track droppedTrack) return;

            if (ItemsControl.ContainerFromElement(QueueListBox, e.OriginalSource as DependencyObject) is not ListBoxItem item) return;

            if (item.Content is not Track targetTrack || droppedTrack.Path == targetTrack.Path) return;

            var uiQueue = MusicLibrary.Instance.PlaybackQueue;
            int oldIndex = uiQueue.IndexOf(droppedTrack);
            int newIndex = uiQueue.IndexOf(targetTrack);

            if (oldIndex >= 0 && newIndex >= 0)
            {
                uiQueue.Move(oldIndex, newIndex);

                Player._actualPlayingQueue.RemoveAt(oldIndex);
                Player._actualPlayingQueue.Insert(newIndex, droppedTrack);
            }
            _queueDraggedTrack = null;
            e.Handled = true;
            UpdateNextTrackUI();
        }
    }
}