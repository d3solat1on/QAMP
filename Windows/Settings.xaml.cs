using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using QAMP.Models;
using QAMP.Services;
using QAMP.ViewModels;

namespace QAMP.Windows
{
    public partial class Settings : Window
    {
        private bool isInitializing;
        private string? originalColorScheme;
        private string? originalAccentColor;
        private double[]? originalEqGains;
        private string? originalPreset;
        private bool originalVisualizerEnabled;
        private int originalBarCount;
        private bool originalCloseToTray;
        private bool originalUseAdaptiveGradients;
        private readonly PlayerService _player;

        public Settings(PlayerService player)
        {
            InitializeComponent();
            _player = player;
            LoadEqualizerData();
        }

        private void LoadEqualizerData()
        {
            var bands = new List<EqBandViewModel>();
            float[] freqs = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
            var config = SettingsManager.Instance.Config;

            for (int i = 0; i < freqs.Length; i++)
            {
                string freqLabel = freqs[i] < 1000 ? $"{freqs[i]}" : $"{freqs[i] / 1000}k";
                bands.Add(new EqBandViewModel
                {
                    Index = i,
                    Frequency = freqLabel,
                    // Читаем значение из сохраненного конфига, а не из памяти плеера!
                    Gain = (float)config.EqualizerGains[i]
                });
            }
            EqItemsControl.ItemsSource = bands;

            // Восстанавливаем сохраненный режим
            SetPresetComboBoxValue(config.EqualizerPreset);

            // Применяем сохраненные значения к плееру
            ApplySavedGains();
        }

        private void EqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.DataContext is EqBandViewModel band)
            {
                float newValue = (float)e.NewValue;

                // Обновляем эквалайзер в плеере
                if (_player != null)
                {
                    _player.CurrentEqualizer?.SetGain(band.Index, newValue);
                    _player.EqGains[band.Index] = newValue;
                }

                // Сохраняем в конфиг
                var config = SettingsManager.Instance.Config;
                config.EqualizerGains[band.Index] = newValue;

                // Обновляем график АЧХ
                DrawEqGraph();
            }
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

                SettingsManager.Instance.Config.EqualizerPreset = presetName;
                SettingsManager.Instance.Save();
            }
        }

        private void ApplyPreset(float[] gains)
        {
            if (EqItemsControl.ItemsSource is List<EqBandViewModel> bands)
            {
                for (int i = 0; i < bands.Count && i < gains.Length; i++)
                {
                    bands[i].Gain = gains[i]; // Обновляет UI через Binding
                }

                // Применяем к плееру
                if (_player != null)
                {
                    _player.UpdateEqualizerGains(gains);
                }

                // Сохраняем в конфиг
                var config = SettingsManager.Instance.Config;
                for (int i = 0; i < gains.Length; i++)
                {
                    config.EqualizerGains[i] = gains[i];
                }
                config.EqualizerPreset = GetCurrentPresetName();
                SettingsManager.Instance.Save();

                // Обновляем график
                DrawEqGraph();
            }
        }
        private string GetCurrentPresetName()
        {
            if (PresetComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "Пользовательский";
            }
            return "Пользовательский";
        }

        private void ResetEq_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new float[10]); // Все в 0
            PresetComboBox.SelectedIndex = 0; // Возвращаем в "Пользовательский"
        }

        private void ApplySavedGains()
        {
            if (_player?.CurrentEqualizer == null) return;

            var config = SettingsManager.Instance.Config;
            for (int i = 0; i < config.EqualizerGains.Length; i++)
            {
                _player.CurrentEqualizer.SetGain(i, (float)config.EqualizerGains[i]);
                _player.EqGains[i] = (float)config.EqualizerGains[i];
            }
        }

        private void SetPresetComboBoxValue(string presetName)
        {
            for (int i = 0; i < PresetComboBox.Items.Count; i++)
            {
                if (PresetComboBox.Items[i] is ComboBoxItem item && item.Content.ToString() == presetName)
                {
                    PresetComboBox.SelectedIndex = i;
                    return;
                }
            }
            PresetComboBox.SelectedIndex = 0; // По умолчанию на "Пользовательский"
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            isInitializing = true;

            var config = SettingsManager.Instance.Config;
            originalColorScheme = config.ColorScheme;
            originalAccentColor = config.AccentColor;
            originalEqGains = (double[])config.EqualizerGains.Clone();
            originalPreset = config.EqualizerPreset;
            originalVisualizerEnabled = config.IsVisualizerEnabled;
            originalBarCount = config.VisualizerBarCount;
            originalCloseToTray = config.CloseToTray;
            originalUseAdaptiveGradients = config.UseAdaptiveGradients;

            // Установить параметры спектрограммы
            VisualizerEnabled.IsChecked = config.IsVisualizerEnabled;
            VisualizerDisabled.IsChecked = !config.IsVisualizerEnabled;
            // SetBarCountComboValue(config.VisualizerBarCount);

            // Установить выбранную тему
            switch (config.ColorScheme)
            {
                case "Dark":
                    DarkThemeRadio.IsChecked = true;
                    break;
                case "Light":
                    LightThemeRadio.IsChecked = true;
                    break;
                case "Custom":
                    CustomThemeRadio.IsChecked = true;
                    break;
            }

            // Установить акцентный цвет
            AccentColorTextBox.Text = config.AccentColor;
            UpdateColorPreview();

            // Загружаем выбранное действие при закрытии
            CloseToTrayRadio.IsChecked = config.CloseToTray;
            CloseAppRadio.IsChecked = !config.CloseToTray;

            // Загружаем состояние адаптивных градиентов
            AdaptiveGradientsRadio.IsChecked = config.UseAdaptiveGradients;
            StaticGradientsRadio.IsChecked = !config.UseAdaptiveGradients;

            isInitializing = false;

            // Синхронизируем эквалайзер с плеером
            if (_player != null && _player.CurrentEqualizer != null)
            {
                for (int i = 0; i < config.EqualizerGains.Length; i++)
                {
                    _player.CurrentEqualizer.SetGain(i, (float)config.EqualizerGains[i]);
                    _player.EqGains[i] = (float)config.EqualizerGains[i];
                }
            }

            // Рисуем график АЧХ
            Dispatcher.InvokeAsync(() => DrawEqGraph(), System.Windows.Threading.DispatcherPriority.Loaded);
            if (config.IsCompactMode)
                CompactModeRadio.IsChecked = true;
            else
                DefaultModeRadio.IsChecked = true;
        }

        private void AdaptiveGradients_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseAdaptiveGradients = AdaptiveGradientsRadio.IsChecked ?? false;
            SettingsManager.Instance.Save();
        }
        
        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            if (sender is not RadioButton radio) return;

            string theme = radio.Content.ToString() switch
            {
                "Темная" => "Dark",
                "Светлая" => "Light",
                "Пользовательская" => "Custom",
                _ => "Dark"
            };

            if (SettingsManager.Instance.Config.ColorScheme == theme)
                return;

            SettingsManager.Instance.Config.ColorScheme = theme;

            // Не применяем несуществующую тему
            if (theme == "Custom")
            {
                // Просто оставляем текущую тему и применяем оттенок
                ThemeManager.UpdateAccentColor(SettingsManager.Instance.Config.AccentColor);
                return;
            }

            ThemeManager.ApplyTheme(theme);
        }
        private void Format_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing || sender is not RadioButton radio) return;

            bool isCompact = radio.Name == "CompactModeRadio";

            var config = SettingsManager.Instance.Config;
            config.IsCompactMode = isCompact;

        }
        private void CloseAction_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            if (sender is not RadioButton radio) return;

            bool closeToTray = radio.Name == "CloseToTrayRadio";
            SettingsManager.Instance.Config.CloseToTray = closeToTray;
        }

        private void AccentColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var config = SettingsManager.Instance.Config;
            config.AccentColor = AccentColorTextBox.Text;
            ThemeManager.UpdateAccentColor(config.AccentColor);
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(AccentColorTextBox.Text);
                ColorPreview.Text = $"Предварительный просмотр: RGB({color.R}, {color.G}, {color.B})";
                ColorPreview.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                ColorPreview.Text = "Неверный формат цвета";
                ColorPreview.Foreground = Brushes.Red;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.Save();
            
            // Обновляем интерфейс, если опция адаптивных градиентов изменилась
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshAdaptiveGradients();
            }
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var config = SettingsManager.Instance.Config;

            // Восстановить оригинальные настройки для темы
            if (originalColorScheme != null)
                config.ColorScheme = originalColorScheme;
            if (originalAccentColor != null)
                config.AccentColor = originalAccentColor;

            // Восстановить оригинальные значения эквалайзера
            if (originalEqGains != null)
            {
                for (int i = 0; i < originalEqGains.Length; i++)
                {
                    config.EqualizerGains[i] = originalEqGains[i];
                }
            }
            if (originalPreset != null)
                config.EqualizerPreset = originalPreset;

            // Восстановить оригинальные значения спектрограммы
            config.IsVisualizerEnabled = originalVisualizerEnabled;
            config.VisualizerBarCount = originalBarCount;

            // Восстановить оригинальное значение "Сворачивать в трей"
            config.CloseToTray = originalCloseToTray;

            // Восстановить оригинальное значение адаптивных градиентов
            config.UseAdaptiveGradients = originalUseAdaptiveGradients;

            // Обновляем RadioButton при восстановлении
            if (originalCloseToTray)
            {
                CloseToTrayRadio.IsChecked = true;
            }
            else
            {
                CloseAppRadio.IsChecked = true;
            }

            // Обновляем RadioButton адаптивных градиентов при восстановлении
            if (originalUseAdaptiveGradients)
            {
                AdaptiveGradientsRadio.IsChecked = true;
            }
            else
            {
                StaticGradientsRadio.IsChecked = true;
            }

            // if (_player?.SpectrumViewModel != null)
            // {
            //     _player.SpectrumViewModel.BarCount = originalBarCount;
            // }

            if (originalColorScheme != null)
                ThemeManager.ApplyTheme(originalColorScheme);
            if (originalAccentColor != null)
                ThemeManager.UpdateAccentColor(originalAccentColor);

            ApplySavedGains();

            DialogResult = false;
            Close();
        }



        private void DrawEqGraph()
        {
            if (EqGraphCanvas == null) return;

            EqGraphCanvas.Children.Clear();

            var config = SettingsManager.Instance.Config;
            double canvasWidth = EqGraphCanvas.ActualWidth;
            double canvasHeight = EqGraphCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            // Параметры графика
            const double maxDb = 12;
            double midY = canvasHeight / 2; // Центр для 0dB

            // Получаем текущие значения эквалайзера
            float[] gains = new float[10];
            if (EqItemsControl.ItemsSource is List<EqBandViewModel> bands)
            {
                for (int i = 0; i < bands.Count; i++)
                {
                    gains[i] = bands[i].Gain;
                }
            }

            // Рисуем фоновую сетку
            DrawGridLines(canvasWidth, canvasHeight, midY);

            // Рисуем линию 0dB
            var zeroline = new Line
            {
                X1 = 0,
                Y1 = midY,
                X2 = canvasWidth,
                Y2 = midY,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 200, 0)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([3, 3])
            };
            EqGraphCanvas.Children.Add(zeroline);

            // Вычисляем точки для полилинии
            PointCollection points = [];

            // Интерполируем значения между полосами для плавной кривой
            for (int i = 0; i <= 100; i++)
            {
                double t = i / 100.0; // 0 до 1
                int bandIndex = (int)(t * (gains.Length - 1));
                double bandFraction = t * (gains.Length - 1) - bandIndex;

                float gainValue;
                if (bandIndex >= gains.Length - 1)
                {
                    gainValue = gains[gains.Length - 1];
                }
                else
                {
                    // Линейная интерполяция между двумя полосами
                    gainValue = gains[bandIndex] * (1 - (float)bandFraction) +
                               gains[bandIndex + 1] * (float)bandFraction;
                }

                // Преобразуем dB в координаты Y (инвертируем, чтобы положительные dB шли вверх)
                double y = midY - (gainValue / maxDb) * (midY - 2);
                double x = (i / 100.0) * canvasWidth;

                points.Add(new Point(x, y));
            }

            // Рисуем полилинию
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

            // Рисуем точки для каждой полосы
            double stepX = canvasWidth / (gains.Length - 1);
            for (int i = 0; i < gains.Length; i++)
            {
                double x = i * stepX;
                double y = midY - (gains[i] / maxDb) * (midY - 2);

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
            // Вертикальные линии сетки
            for (int i = 0; i < 10; i++)
            {
                double x = (i / 9.0) * canvasWidth;
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

            // Горизонтальные линии сетки
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

        // private void SetBarCountComboValue(int barCount)
        // {
        //     for (int i = 0; i < BarCountCombo.Items.Count; i++)
        //     {
        //         if (BarCountCombo.Items[i] is ComboBoxItem item &&
        //             item.Content?.ToString() == barCount.ToString())
        //         {
        //             BarCountCombo.SelectedIndex = i;
        //             return;
        //         }
        //     }
        //     BarCountCombo.SelectedIndex = 1; // По умолчанию 64
        // }

        private void VisualizerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.IsVisualizerEnabled = VisualizerEnabled.IsChecked ?? false;

            // Очищаем спектрограмму при отключении
            // if (_player?.SpectrumViewModel?.Bars != null)
            // {
            //     _player.SpectrumViewModel.Bars.Clear();
            // }
        }
        private void HelpWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow()
            {
                Owner = this
            };
            
            helpWindow.ShowHelpWindow();
        }
        // private void BarCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        // {
        //     if (isInitializing || BarCountCombo.SelectedItem == null) return;
        //     // if (_player?.SpectrumViewModel == null) return;

        //     if (BarCountCombo.SelectedItem is ComboBoxItem item &&
        //         item.Content != null &&
        //         int.TryParse(item.Content.ToString(), out int barCount))
        //     {
        //         var config = SettingsManager.Instance.Config;
        //         config.VisualizerBarCount = barCount;
        //         // _player.SpectrumViewModel.BarCount = barCount;
        //     }
        // }
    }
}