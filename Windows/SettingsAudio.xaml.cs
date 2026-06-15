using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP.Windows
{
    public partial class SettingsAudio : Window
    {
        private bool isInitializing;
        private readonly PlayerService _player;
        private List<EqBandViewModel>? _bands;

        public SettingsAudio()
        {
            isInitializing = true;
            InitializeComponent();
            _player = PlayerService.Instance;
            LoadEqualizerData();
        }

        private void LoadEqualizerData()
        {
            isInitializing = true;
            _bands = [];
            float[] freqs = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
            var config = SettingsManager.Instance.Config;

            for (int i = 0; i < freqs.Length; i++)
            {
                string freqLabel = freqs[i] < 1000 ? $"{freqs[i]}" : $"{freqs[i] / 1000}k";
                _bands.Add(new EqBandViewModel
                {
                    Index = i,
                    Frequency = freqLabel,
                    Gain = (float)config.EqualizerGains[i]
                });
            }

            EqItemsControl.ItemsSource = _bands;
            SetPresetComboBoxValue(config.EqualizerPreset);
            ApplySavedGains();
            LoadAudioSettings();
            isInitializing = false;
        }

        private void LoadAudioSettings()
        {
            var config = SettingsManager.Instance.Config;

            ReverbEnabled.IsChecked = config.ReverbEnabled;
            ReverbLevelSlider.Value = config.ReverbLevel;
            ReverbLevelText.Text = $"{config.ReverbLevel:F0}%";

            EchoEnabled.IsChecked = config.EchoEnabled;
            EchoDelaySlider.Value = config.EchoDelay;
            EchoDelayText.Text = $"{config.EchoDelay:F0} ms";

            VocalEnhancementEnabled.IsChecked = config.VocalEnhancementEnabled;
            LoudnessEnabled.IsChecked = config.LoudnessEnabled;

            TempoSlider.Value = config.Tempo;
            TempoText.Text = $"{config.Tempo:F2}x";
            PitchSlider.Value = config.Pitch;
            PitchText.Text = $"{config.Pitch:F2}x";

            CompressorEnabled.IsChecked = config.CompressorEnabled;
            CompressorThresholdSlider.Value = config.CompressorThreshold;
            CompressorThresholdText.Text = $"{config.CompressorThreshold:F2}";

            BalanceSlider.Value = config.Balance;
            BalanceText.Text = $"{config.Balance:F2}";

            OutputDeviceComboBox.ItemsSource = PlayerService.GetOutputDevices();
            if (config.OutputDeviceId >= 0)
            {
                OutputDeviceComboBox.SelectedValue = config.OutputDeviceId;
            }
            else if (OutputDeviceComboBox.Items.Count > 0)
            {
                OutputDeviceComboBox.SelectedIndex = 0;
            }
        }

        private void EqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            if (sender is Slider slider && slider.DataContext is EqBandViewModel band)
            {
                float newValue = (float)e.NewValue;
                _player.EqGains[band.Index] = newValue;

                var config = SettingsManager.Instance.Config;
                config.EqualizerGains[band.Index] = newValue;
                SettingsManager.Instance.Save();

                // Применить эквалайзер к текущему потоку в реальном времени
                _player.ApplyCurrentEqGains();
                DrawEqGraph();
            }
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            if (PresetComboBox.SelectedItem is ComboBoxItem item)
            {
                string presetName = item.Content?.ToString() ?? "Пользовательский";
                switch (presetName)
                {
                    case "Bass Boost":
                        ApplyPreset([8, 10, 7, 3, 0, 0, 2, 4, 6, 5]);
                        break;
                    case "Rock":
                        ApplyPreset([5, 4, 3, 1, -1, -1, 2, 4, 5, 3]);
                        break;
                    case "Classical":
                        ApplyPreset([4, 3, 2, 1, 1, 1, 2, 3, 4, 4]);
                        break;
                    case "Pop":
                        ApplyPreset([-2, -1, 1, 2, 4, 4, 3, 1, -1, -2]);
                        break;
                    case "Lo-Fi":
                        ApplyPreset([4, 3, 2, 2, 3, 4, 3, 1, -3, -5]);
                        break;
                    case "Electronic":
                        ApplyPreset([7, 6, 3, -1, -3, -3, 0, 4, 7, 8]);
                        break;
                    case "Vocal":
                        ApplyPreset([-5, -4, -2, 2, 5, 6, 4, 2, 0, -2]);
                        break;
                    case "Metal":
                        ApplyPreset([6, 5, 2, -2, -3, -1, 3, 5, 4, 2]);
                        break;
                    case "808":
                        ApplyPreset([12, 11, 8, 2, -2, -1, 1, 4, 8, 10]);
                        break;
                    default:
                        break;
                }

                var config = SettingsManager.Instance.Config;
                config.EqualizerPreset = presetName;
                SettingsManager.Instance.Save();
            }
        }

        private void ApplyPreset(float[] gains)
        {
            if (_bands == null) return;

            for (int i = 0; i < _bands.Count && i < gains.Length; i++)
            {
                _bands[i].Gain = gains[i];
            }

            _player.UpdateEqualizerGains(gains);

            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < gains.Length; i++)
            {
                config.EqualizerGains[i] = gains[i];
            }
            config.EqualizerPreset = GetCurrentPresetName();
            SettingsManager.Instance.Save();

            DrawEqGraph();
        }

        private string GetCurrentPresetName()
        {
            if (PresetComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "Пользовательский";
            }
            return "Пользовательский";
        }

        private void AudioProcessingSettingChanged(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.ReverbEnabled = ReverbEnabled.IsChecked ?? false;
            config.EchoEnabled = EchoEnabled.IsChecked ?? false;
            config.VocalEnhancementEnabled = VocalEnhancementEnabled.IsChecked ?? false;
            config.LoudnessEnabled = LoudnessEnabled.IsChecked ?? false;
            config.CompressorEnabled = CompressorEnabled.IsChecked ?? false;
            SettingsManager.Instance.Save();
            _player.ApplyAudioEffects();
        }

        private void ReverbLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.ReverbLevel = e.NewValue;
            ReverbLevelText.Text = $"{e.NewValue:F0}%";
            SettingsManager.Instance.Save();
            _player.ApplyAudioEffects();
        }

        private void EchoDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.EchoDelay = e.NewValue;
            EchoDelayText.Text = $"{e.NewValue:F0} ms";
            SettingsManager.Instance.Save();
            _player.ApplyAudioEffects();
        }

        private void TempoPitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            if (TempoSlider == null || PitchSlider == null || TempoText == null || PitchText == null) return;
            var config = SettingsManager.Instance.Config;
            config.Tempo = TempoSlider.Value;
            config.Pitch = PitchSlider.Value;
            TempoText.Text = $"{config.Tempo:F2}x";
            PitchText.Text = $"{config.Pitch:F2}x";
            SettingsManager.Instance.Save();
            _player.ApplyPlaybackRate(config.Tempo, config.Pitch);
        }

        private void CompressorThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.CompressorThreshold = e.NewValue;
            CompressorThresholdText.Text = $"{e.NewValue:F2}";
            SettingsManager.Instance.Save();
            _player.ApplyAudioEffects();
        }

        private void BalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.Balance = e.NewValue;
            BalanceText.Text = $"{e.NewValue:F2}";
            SettingsManager.Instance.Save();
            _player.SetBalance((float)e.NewValue);
        }

        private void OutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            if (OutputDeviceComboBox.SelectedItem is OutputDeviceInfo device)
            {
                var config = SettingsManager.Instance.Config;
                config.OutputDeviceId = device.Id;
                config.OutputDeviceName = device.Name;
                SettingsManager.Instance.Save();
                _player.SetOutputDevice(device.Id);
            }
        }

        private void ResetEq_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new float[10]);
            PresetComboBox.SelectedIndex = 0;
        }

        private void ApplySavedGains()
        {
            if (_player == null) return;

            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < config.EqualizerGains.Length && i < _player.EqGains.Length; i++)
            {
                _player.EqGains[i] = (float)config.EqualizerGains[i];
            }
            _player.ApplyCurrentEqGains();
        }

        private void SetPresetComboBoxValue(string presetName)
        {
            for (int i = 0; i < PresetComboBox.Items.Count; i++)
            {
                if (PresetComboBox.Items[i] is ComboBoxItem item && item.Content?.ToString() == presetName)
                {
                    PresetComboBox.SelectedIndex = i;
                    return;
                }
            }
            PresetComboBox.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DrawEqGraph();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
            MemoryOptimizer.RunAsync(Dispatcher);
        }

        private void DrawEqGraph()
        {
            if (EqGraphCanvas == null) return;

            EqGraphCanvas.Children.Clear();

            double canvasWidth = EqGraphCanvas.ActualWidth;
            double canvasHeight = EqGraphCanvas.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            const double maxDb = 12;
            double midY = canvasHeight / 2;

            float[] gains = new float[10];
            if (_bands != null)
            {
                for (int i = 0; i < _bands.Count; i++)
                {
                    gains[i] = _bands[i].Gain;
                }
            }

            DrawGridLines(canvasWidth, canvasHeight, midY);

            var zeroLine = new Line
            {
                X1 = 0,
                Y1 = midY,
                X2 = canvasWidth,
                Y2 = midY,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 200, 0)),
                StrokeThickness = 1,
                StrokeDashArray = [3, 3]
            };
            EqGraphCanvas.Children.Add(zeroLine);

            var points = new PointCollection();
            for (int i = 0; i <= 100; i++)
            {
                double t = i / 100.0;
                int bandIndex = (int)(t * (gains.Length - 1));
                double bandFraction = t * (gains.Length - 1) - bandIndex;

                float gainValue;
                if (bandIndex >= gains.Length - 1)
                {
                    gainValue = gains[gains.Length - 1];
                }
                else
                {
                    gainValue = gains[bandIndex] * (1 - (float)bandFraction) + gains[bandIndex + 1] * (float)bandFraction;
                }

                double y = midY - gainValue / maxDb * (midY - 2);
                double x = i / 100.0 * canvasWidth;
                points.Add(new Point(x, y));
            }

            var polyline = new Polyline
            {
                Points = points,
                Stroke = (Brush)TryFindResource("AccentBrush") ?? new SolidColorBrush(Colors.Green),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            EqGraphCanvas.Children.Add(polyline);

            double stepX = canvasWidth / (gains.Length - 1);
            for (int i = 0; i < gains.Length; i++)
            {
                double x = i * stepX;
                double y = midY - gains[i] / maxDb * (midY - 2);

                var circle = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)TryFindResource("AccentBrush") ?? new SolidColorBrush(Colors.Green)
                };
                Canvas.SetLeft(circle, x - 3);
                Canvas.SetTop(circle, y - 3);
                EqGraphCanvas.Children.Add(circle);
            }
        }

        private void DrawGridLines(double canvasWidth, double canvasHeight, double midY)
        {
            for (int i = 0; i < 10; i++)
            {
                double x = i / 9.0 * canvasWidth;
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Stroke = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200)),
                    StrokeThickness = 1
                };
                EqGraphCanvas.Children.Add(line);
            }

            double stepY = canvasHeight / 4.0;
            for (int i = 0; i <= 4; i++)
            {
                double y = i * stepY;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = canvasWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200)),
                    StrokeThickness = 1
                };
                EqGraphCanvas.Children.Add(line);
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}