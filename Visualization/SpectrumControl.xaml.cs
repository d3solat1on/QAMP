using System.Windows;
using System.Windows.Controls;
using QAMP.Models;
using ScottPlot;

namespace QAMP.Visualization
{
    public partial class SpectrumControl : UserControl
    {
        private ScottPlot.Plottables.BarPlot? myBars;
        private const int BarCount = 48;
        // private readonly SpectrumSettings _settings;
        // Буфер для сглаживания
        private readonly double[] _smoothedValues;
        private readonly double[] _peakValues;
        private ScottPlot.Plottables.BarPlot _peakBars;

        public SpectrumControl()
        {
            InitializeComponent();
            _smoothedValues = new double[BarCount];
            _peakValues = new double[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                _smoothedValues[i] = 0.02;
                _peakValues[i] = 0.02;
            }
            _peakBars = SpectrumPlot.Plot.Add.Bars(_peakValues);
            // Дополнительная проверка
            System.Diagnostics.Debug.WriteLine($"SpectrumControl initialized, peakValues[0] = {_peakValues[0]}");

            Loaded += SpectrumControl_Loaded;
        }

        private void SpectrumControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Еще раз сбрасываем при загрузке
            for (int i = 0; i < BarCount; i++)
            {
                _peakValues[i] = 0.02;
                _smoothedValues[i] = 0.02;
            }

            SetupPlot();
        }

        private void SetupPlot()
        {
            if (!SettingsManager.Instance.Config.IsVisualizerEnabled)
            {
                return;
            }
            try
            {
                SpectrumPlot.Plot.Clear();

                double[] barValues = new double[BarCount];
                double[] peakValues = new double[BarCount];

                for (int i = 0; i < BarCount; i++)
                {
                    barValues[i] = 0.01; // Минимальная высота
                    peakValues[i] = 0.01; // Минимальная высота для пиков
                }

                _peakBars = SpectrumPlot.Plot.Add.Bars(peakValues);
                myBars = SpectrumPlot.Plot.Add.Bars(barValues);

                for (int i = 0; i < BarCount; i++)
                {
                    myBars.Bars[i].Position = i;
                    myBars.Bars[i].ValueBase = 0;

                    _peakBars.Bars[i].Position = i;
                    _peakBars.Bars[i].ValueBase = 0;
                }

                SpectrumPlot.Plot.HideGrid();
                SpectrumPlot.Plot.HideAxesAndGrid();

                SpectrumPlot.Plot.Axes.SetLimits(left: -0.5, right: BarCount - 0.5, bottom: 0, top: 1.0);

                SpectrumPlot.IsEnabled = false;
                ApplyColors();
                SpectrumPlot.Refresh();

                System.Diagnostics.Debug.WriteLine($"SetupPlot completed, bars: {myBars.Bars.Count}, peakBars: {_peakBars.Bars.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupPlot Error: {ex.Message}");
            }
        }
        private void ApplyColors()
        {

            if (Application.Current.Resources["TertiaryBackgroundBrush"] is System.Windows.Media.SolidColorBrush bgBrush)
            {
                var c = bgBrush.Color;
                SpectrumPlot.Plot.FigureBackground.Color = new Color(c.R, c.G, c.B);
                SpectrumPlot.Plot.DataBackground.Color = new Color(c.R, c.G, c.B);
            }
            else
            {
                SpectrumPlot.Plot.FigureBackground.Color = Colors.Black;
                SpectrumPlot.Plot.DataBackground.Color = Colors.Black;
            }

            if (myBars != null)
            {
                if (Application.Current.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accent)
                {
                    myBars.Color = new Color(accent.Color.R, accent.Color.G, accent.Color.B);
                    _peakBars.Color = new Color(accent.Color.R, accent.Color.G, accent.Color.B);
                }
                else
                {
                    myBars.Color = Colors.LimeGreen;
                    _peakBars.Color = Colors.LimeGreen;
                }
            }
        }
        public void UpdateSpectrum(double[] spectrumData)
        {
            if (myBars == null || _peakBars == null || spectrumData == null || spectrumData.Length == 0) return;
            if (!SettingsManager.Instance.Config.IsVisualizerEnabled)
            {
                if (myBars != null && myBars.Bars.Any(b => b.Value > 0))
                {
                    ResetPeaks();
                    SpectrumPlot.Refresh(); 
                }
                return;
            }
            Dispatcher.Invoke(() =>
            {
                try
                {
                    double gain = 15.5; //tweak

                    for (int i = 0; i < BarCount && i < myBars.Bars.Count; i++)
                    {
                        int spectrumIndex = i * spectrumData.Length / BarCount;
                        if (spectrumIndex >= spectrumData.Length) spectrumIndex = spectrumData.Length - 1;

                        double targetValue = spectrumData[spectrumIndex] * gain;
                        targetValue = Math.Min(0.95, Math.Max(0, targetValue));

                        if (targetValue > _smoothedValues[i])
                            _smoothedValues[i] = _smoothedValues[i] + (targetValue - _smoothedValues[i]) * 0.6;
                        else
                            _smoothedValues[i] = _smoothedValues[i] * 0.88;

                        if (_smoothedValues[i] > _peakValues[i])
                        {
                            _peakValues[i] = _smoothedValues[i];
                        }
                        else
                        {
                            _peakValues[i] = _peakValues[i] * 0.98;
                            if (_peakValues[i] < 0.02) _peakValues[i] = 0.02;
                        }

                        myBars.Bars[i].Value = _smoothedValues[i];
                        _peakBars.Bars[i].Value = _peakValues[i];
                    }

                    SpectrumPlot.Refresh();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateSpectrum Error: {ex.Message}");
                }
            });
        }
        // public void SetSpectrumPreset(string presetName)
        // {
        //     _settings.ApplyPreset(presetName);
        //     System.Diagnostics.Debug.WriteLine($"Spectrum preset changed to: {presetName}");
        // }
        public void ResetPeaks()
        {
            for (int i = 0; i < BarCount; i++)
            {
                _peakValues[i] = 0.01;
                _smoothedValues[i] = 0.01;
            }
        }
    }
}