using System.Windows;
using System.Windows.Controls;
using QAMP.Models;
using ScottPlot;

namespace QAMP.Visualization
{
    public partial class SpectrumControl : UserControl
    {
        private ScottPlot.Plottables.BarPlot? myBars;
        private int _barCount;
        // private readonly SpectrumSettings _settings;
        // Буфер для сглаживания
        private double[] _smoothedValues;
        private double[] _peakValues;
        private ScottPlot.Plottables.BarPlot _peakBars;

        public int BarCount => _barCount;

        public SpectrumControl()
        {
            InitializeComponent();
            _barCount = SettingsManager.Instance.Config.VisualizerBarCount;
            _smoothedValues = new double[_barCount];
            _peakValues = new double[_barCount];
            for (int i = 0; i < _barCount; i++)
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

                SpectrumPlot.Plot.Axes.SetLimitsY(0.0, 1.0);
                SpectrumPlot.Plot.Axes.SetLimitsX(0, BarCount);
                SpectrumPlot.Plot.Axes.Top.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Right.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Left.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Bottom.FrameLineStyle.Width = 0;


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
                Color plotColor;

                if (Application.Current.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accent)
                {
                    plotColor = new Color(accent.Color.R, accent.Color.G, accent.Color.B);
                }
                else
                {
                    plotColor = Colors.LimeGreen;
                }

                myBars.Color = plotColor;
                _peakBars.Color = plotColor;

                foreach (var bar in myBars.Bars)
                {
                    bar.LineStyle.Width = 0;
                }
                foreach (var bar in _peakBars.Bars)
                {
                    bar.LineStyle.Width = 0;
                }
            }
        }

        /// <summary>
        /// Обновляет цвета спектра при смене темы или цвета акцента
        /// </summary>
        public void RefreshColors()
        {
            if (SpectrumPlot == null || myBars == null || _peakBars == null) return;

            ApplyColors();
            SpectrumPlot.Refresh();
            System.Diagnostics.Debug.WriteLine("SpectrumControl colors refreshed");
        }
        public void UpdateSpectrum(double[] spectrumData, double[] peakData, int incomingCount)
        {
            if (myBars == null || _peakBars == null || spectrumData == null || peakData == null) return;

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
                    for (int i = 0; i < incomingCount && i < myBars.Bars.Count; i++)
                    {
                        myBars.Bars[i].Value = spectrumData[i];
                        _peakBars.Bars[i].Value = peakData[i];
                    }
                    SpectrumPlot.Refresh();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateSpectrum Error: {ex.Message}");
                }
            });
        }
        public void ResetPeaks()
        {
            for (int i = 0; i < BarCount; i++)
            {
                _peakValues[i] = 0.01;
                _smoothedValues[i] = 0.01;
            }
        }

        public void ClearSpectrum()
        {
            if (myBars == null || _peakBars == null) return;

            for (int i = 0; i < BarCount && i < myBars.Bars.Count; i++)
            {
                myBars.Bars[i].Value = 0.01;
                _peakBars.Bars[i].Value = 0.01;
            }
            SpectrumPlot.Refresh();
        }

        public void SetBarCount(int count)
        {
            if (count <= 0 || count == _barCount)
                return;

            _barCount = count;
            _smoothedValues = new double[_barCount];
            _peakValues = new double[_barCount];

            for (int i = 0; i < _barCount; i++)
            {
                _smoothedValues[i] = 0.02;
                _peakValues[i] = 0.02;
            }

            SetupPlot();
        }
    }
}