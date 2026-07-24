using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QAMP.Services;

namespace QAMP;

public partial class MainWindow
{
    private bool _isSliderDragging = false;

    private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider) return;

        Point point = e.GetPosition(slider);
        double relativePosition = point.X / slider.ActualWidth;
        double newValue = slider.Minimum + (relativePosition * (slider.Maximum - slider.Minimum));
        slider.Value = newValue;
    }
    private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isSliderDragging = true;
    }

    private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isSliderDragging = false;
        if (Player.CurrentTrack != null)
        {
            var newPosition = ProgressSlider.Value / 100 * Player.Duration;
            Player.Seek(newPosition);
        }
    }
    private void OnPositionChanged(double position)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action<double>(OnPositionChanged), position);
            return;
        }

        if (!_isSliderDragging)
        {
            if (Player.Duration > 0)
            {
                double sliderValue = position / Player.Duration * 100;
                if (!double.IsNaN(sliderValue) && !double.IsInfinity(sliderValue))
                {
                    ProgressSlider.Value = sliderValue;
                }
            }

            double currentWholeSecond = Math.Floor(position);
            if (currentWholeSecond != _lastFormattedSeconds)
            {
                _lastFormattedSeconds = currentWholeSecond;
                CurrentTimeText.Text = FormatTime(position);
            }
        }
        if (_isLyricsMode)
        {
            UpdateLyricsHighlight(TimeSpan.FromSeconds(position));
        }
    }
    private void OnDurationChanged()
    {
        Dispatcher.Invoke(() => { TotalTimeText.Text = FormatTime(Player.Duration); });
    }

    private void OnVolumeChanged(double volume)
    {
        Dispatcher.Invoke(() =>
        {
            VolumeSlider.Value = volume * 100;
            VolumePercentage.Text = $"{volume * 100:F0}%";
        });
    }
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            if (Player != null)
            {
                double volume = VolumeSlider.Value / 100.0;
                Player.Volume = volume;

                string volumeStr = volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _ = DatabaseService.SaveSetting("Volume", volumeStr);

                if (VolumeSlider.Value == 0)
                {
                    VolumeImage.Data = (Geometry)Application.Current.Resources["volume_offGeometry"];
                }
                else
                {
                    VolumeImage.Data = (Geometry)Application.Current.Resources["volumeGeometry"];
                }

                VolumePercentage?.Text = $"{VolumeSlider.Value:F0}%";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in VolumeSlider_ValueChanged: {ex.Message}");
        }
    }

    private void VolumeButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VolumeSlider.Value > 0)
        {
            _lastVolume = VolumeSlider.Value;
            VolumeSlider.Value = 0;
        }
        else
        {
            VolumeSlider.Value = _lastVolume;
        }
    }
    private async void CheckDurationAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(100);
            if (Player.Duration > 0)
            {
                Dispatcher.Invoke(() => { TotalTimeText.Text = FormatTime(Player.Duration); });
                break;
            }
        }
    }

}