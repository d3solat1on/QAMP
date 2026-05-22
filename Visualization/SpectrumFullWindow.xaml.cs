using System.Windows;
using System.Windows.Input;
using QAMP.Services;
using QAMP.Models;

namespace QAMP.Visualization
{
    public partial class SpectrumFullWindow : Window
    {
        public SpectrumFullWindow()
        {
            InitializeComponent();
            Cursor = Cursors.None;
        }

        private void SpectrumFullWindow_Loaded(object sender, RoutedEventArgs e)
        {
            int normalCount = SettingsManager.Instance.Config.VisualizerBarCount;
            FullSpectrumViewer.SetBarCount(normalCount * 2);
            PlayerService.Instance.AddSpectrumControl(FullSpectrumViewer);
            PlayerService.Instance.TrackChanged += PlayerService_TrackChanged;
            UpdateCurrentTrackInfo(PlayerService.Instance.CurrentTrack);
        }

        private void UpdateCurrentTrackInfo(Track? currentTrack)
        {
            if (currentTrack != null)
            {
                DataContext = currentTrack;
                TrackNameText.Text = currentTrack.Name ?? "Неизвестный трек";
                TrackArtistText.Text = currentTrack.Executor ?? "Неизвестный исполнитель";
            }
            else
            {
                DataContext = null;
                TrackNameText.Text = string.Empty;
                TrackArtistText.Text = string.Empty;
                CoverBorder.Child = null;
            }
        }

        private void PlayerService_TrackChanged(Track? track)
        {
            Dispatcher.Invoke(() => UpdateCurrentTrackInfo(track));
        }

        private void SpectrumFullWindow_Closed(object? sender, EventArgs e)
        {
            PlayerService.Instance.RemoveSpectrumControl(FullSpectrumViewer);
            PlayerService.Instance.TrackChanged -= PlayerService_TrackChanged;
        }

        private void SpectrumFullWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W))
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
