using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Models;

namespace QAMP;

public partial class MainWindow
{
    private bool _isLyricsMode = false;
    private List<LrcLine> _parsedLyrics = [];
    private string? _currentLyricsFilePath = null;
    private LrcLine? _lastHighlightedLine = null;
    private bool _hasTimeCodes = false;
    private void ViewLyricsButton_Click(object? sender, RoutedEventArgs? e)
    {
        _isLyricsMode = !_isLyricsMode;
        UpdateInterfaceMode();
    }

    private void UpdateInterfaceMode()
    {
        if (_isLyricsMode)
        {
            Player.PositionChanged += OnPositionChangedForLyrics;
            UpdateLyricsView();

            TracksDataGrid.Visibility = Visibility.Collapsed;
            PlaylistsListBox.Visibility = Visibility.Collapsed;
            ControlsPanel.Visibility = Visibility.Collapsed;
            UpperPanel.Visibility = Visibility.Collapsed;
            LibraryTextBlock.Visibility = Visibility.Collapsed;
            SortByButton.Visibility = Visibility.Collapsed;
            CreatePlaylistButton.Visibility = Visibility.Collapsed;
            RemovePlaylistButton.Visibility = Visibility.Collapsed;
            AddMusicButton.Visibility = Visibility.Collapsed;

            LyricsOverlay.Visibility = Visibility.Visible;

            if (_parsedLyrics != null && _parsedLyrics.Count > 0)
            {
                foreach (var line in _parsedLyrics)
                    line.IsActive = false;

                _lastHighlightedLine = null;

                if (_hasTimeCodes)
                {
                    var currentTime = TimeSpan.FromSeconds(Player.Position);
                    int index = BinarySearchLrc(_parsedLyrics, currentTime);
                    if (index >= 0 && index < _parsedLyrics.Count)
                    {
                        var currentLine = _parsedLyrics[index];
                        currentLine.IsActive = true;
                        _lastHighlightedLine = currentLine;
                        LyricsListBox.ScrollIntoView(currentLine);
                    }
                }
                else
                {
                    _parsedLyrics[0].IsActive = true;
                    _lastHighlightedLine = _parsedLyrics[0];
                }
            }
        }
        else
        {
            Player.PositionChanged -= OnPositionChangedForLyrics;

            LyricsCache.Clear();

            if (_parsedLyrics != null)
            {
                foreach (var line in _parsedLyrics)
                    line.IsActive = false;

                _parsedLyrics.Clear();
            }

            _lastHighlightedLine = null;
            LyricsListBox.ItemsSource = null;

            LibraryTextBlock.Visibility = Visibility.Visible;
            SortByButton.Visibility = Visibility.Visible;
            CreatePlaylistButton.Visibility = Visibility.Visible;
            RemovePlaylistButton.Visibility = Visibility.Visible;
            AddMusicButton.Visibility = Visibility.Visible;
            TracksDataGrid.Visibility = Visibility.Visible;
            PlaylistsListBox.Visibility = Visibility.Visible;
            LeftZona.Visibility = Visibility.Visible;
            ControlsPanel.Visibility = Visibility.Visible;
            UpperPanel.Visibility = Visibility.Visible;
            LyricsOverlay.Visibility = Visibility.Collapsed;
        }
    }
    private void OnPositionChangedForLyrics(double position)
    {
        if (_isLyricsMode && _hasTimeCodes)
        {
            UpdateLyricsHighlight(TimeSpan.FromSeconds(position));
        }
    }
    /// <summary>
    /// Создает список LrcLine для текста без таймкодов, разбивая по строкам
    /// </summary>
    private static List<LrcLine> CreatePlainTextLines(string text)
    {
        var lines = new List<LrcLine>();
        if (string.IsNullOrEmpty(text)) return lines;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                lines.Add(new LrcLine { Text = trimmed });
            }
        }
        return lines;
    }
    public void UpdateLyricsView()
    {
        Track? track = Player.CurrentTrack;
        if (track == null || string.IsNullOrEmpty(track.Path))
        {
            return;
        }

        if (_currentLyricsFilePath == track.Path && _parsedLyrics != null && _parsedLyrics.Count > 0)
        {
            if (_isLyricsMode && _hasTimeCodes)
            {
                var currentTime = TimeSpan.FromSeconds(Player.Position);
                UpdateLyricsHighlight(currentTime);
            }
            return;
        }

        _currentLyricsFilePath = track.Path;

        string lyricsFromFile = GetLyricsFromFile(track.Path);
        string finalLyrics = !string.IsNullOrEmpty(lyricsFromFile) ? lyricsFromFile : track.Lyrics;

        _parsedLyrics = LyricsCache.GetParsedLyrics(track.Path, finalLyrics);

        _hasTimeCodes = _parsedLyrics != null && _parsedLyrics.Any(l => l.Time > TimeSpan.Zero);

        if (!_hasTimeCodes && (_parsedLyrics == null || _parsedLyrics.Count == 0))
        {
            string plainText = LyricsCache.GetPlainText(track.Path, finalLyrics);
            _parsedLyrics = CreatePlainTextLines(plainText);
        }

        if (LyricsListBox != null && LyricsListBox.ItemsSource != _parsedLyrics)
        {
            LyricsListBox.ItemsSource = _parsedLyrics;
        }

        track.Lyrics = finalLyrics;
        _lastHighlightedLine = null;

        if (_parsedLyrics != null)
        {
            foreach (var line in _parsedLyrics)
                line.IsActive = false;
        }

        if (_isLyricsMode && _parsedLyrics != null && _parsedLyrics.Count > 0)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    LyricsListBox?.ScrollIntoView(LyricsListBox.Items[0]);
                    LyricsListBox?.SelectedIndex = 0;
                }), System.Windows.Threading.DispatcherPriority.Background);
            if (_hasTimeCodes)
            {
                var currentTime = TimeSpan.FromSeconds(Player.Position);
                int index = BinarySearchLrc(_parsedLyrics, currentTime);
                if (index >= 0 && index < _parsedLyrics.Count)
                {
                    var currentLine = _parsedLyrics[index];
                    currentLine.IsActive = true;
                    _lastHighlightedLine = currentLine;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LyricsListBox?.ScrollIntoView(currentLine);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            else
            {
                _parsedLyrics[0].IsActive = true;
                _lastHighlightedLine = _parsedLyrics[0];
            }
        }
    }
    private void UpdateLyricsHighlight(TimeSpan currentTime)
    {
        if (_parsedLyrics == null || _parsedLyrics.Count == 0 || !_hasTimeCodes) return;

        int index = BinarySearchLrc(_parsedLyrics, currentTime);
        if (index < 0 || index >= _parsedLyrics.Count)
        {
            _lastHighlightedLine?.IsActive = false;
            _lastHighlightedLine = null;
            return;
        }

        var currentLine = _parsedLyrics[index];
        if (_lastHighlightedLine == currentLine) return;

        _lastHighlightedLine?.IsActive = false;

        currentLine.IsActive = true;
        _lastHighlightedLine = currentLine;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (LyricsListBox == null) return;

            if (LyricsListBox.ItemContainerGenerator.ContainerFromItem(currentLine) is ListBoxItem container)
            {
                container.BringIntoView();

                LyricsListBox.ScrollIntoView(currentLine);

                container.UpdateLayout();

                if (!IsLineFullyVisible(currentLine))
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(LyricsListBox);
                    if (scrollViewer != null)
                    {
                        var itemPosition = container.TransformToAncestor(LyricsListBox).Transform(new Point(0, 0));
                        double targetOffset = itemPosition.Y - (scrollViewer.ViewportHeight / 2) + (container.ActualHeight / 2);
                        targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableHeight));
                        scrollViewer.ScrollToVerticalOffset(targetOffset);
                    }
                }
            }
            else
            {
                LyricsListBox.ScrollIntoView(currentLine);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
    private void LyricsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (LyricsOverlay.Visibility != Visibility.Visible)
            return;

        int currentIndex = LyricsListBox.SelectedIndex;
        int itemCount = LyricsListBox.Items.Count;

        switch (e.Key)
        {
            case Key.Up:
                if (currentIndex > 0)
                {
                    LyricsListBox.SelectedIndex = currentIndex - 1;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (currentIndex < itemCount - 1)
                {
                    LyricsListBox.SelectedIndex = currentIndex + 1;
                    LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.PageUp:
                int newIndexUp = Math.Max(0, currentIndex - 5);
                LyricsListBox.SelectedIndex = newIndexUp;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                e.Handled = true;
                break;

            case Key.PageDown:
                int newIndexDown = Math.Min(itemCount - 1, currentIndex + 5);
                LyricsListBox.SelectedIndex = newIndexDown;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                e.Handled = true;
                break;

            case Key.Home:
                LyricsListBox.SelectedIndex = 0;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                e.Handled = true;
                break;

            case Key.End:
                LyricsListBox.SelectedIndex = itemCount - 1;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
                e.Handled = true;
                break;
        }
    }
    private void LyricsListBox_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (LyricsOverlay.Visibility != Visibility.Visible)
            return;

        int currentIndex = LyricsListBox.SelectedIndex;
        int itemCount = LyricsListBox.Items.Count;

        if (e.Delta > 0)
        {
            if (currentIndex > 0)
            {
                LyricsListBox.SelectedIndex = currentIndex - 1;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
            }
        }
        else
        {
            if (currentIndex < itemCount - 1)
            {
                LyricsListBox.SelectedIndex = currentIndex + 1;
                LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
            }
        }

        e.Handled = true;
    }
    private void LrcLine_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && _hasTimeCodes)
        {
            if (item.DataContext is LrcLine clickedLine)
            {
                double targetSeconds = clickedLine.Time.TotalSeconds;

                if (targetSeconds >= 0)
                {
                    Player.Seek(targetSeconds);
                    UpdateLyricsHighlight(clickedLine.Time);
                }
            }
        }
    }
    private static string GetLyricsFromFile(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            return file.Tag.Lyrics ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool IsLineFullyVisible(LrcLine line)
    {
        if (LyricsListBox == null || LyricsListBox.Items.Count == 0) return false;

        if (LyricsListBox.ItemContainerGenerator.ContainerFromItem(line) is not FrameworkElement container) return false;

        var rect = container.TransformToAncestor(LyricsListBox).TransformBounds(
            new Rect(0, 0, container.ActualWidth, container.ActualHeight));
        var listRect = new Rect(0, 0, LyricsListBox.ActualWidth, LyricsListBox.ActualHeight);

        return rect.Y >= listRect.Y && rect.Y + rect.Height <= listRect.Y + listRect.Height;
    }
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private static int BinarySearchLrc(List<LrcLine> lines, TimeSpan time)
    {
        if (lines.Count == 0) return -1;
        if (time < lines[0].Time) return -1;

        int left = 0;
        int right = lines.Count - 1;
        int result = -1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (lines[mid].Time <= time)
            {
                result = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return result;
    }
}