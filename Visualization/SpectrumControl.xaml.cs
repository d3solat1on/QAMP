using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QAMP.Models;
using ScottPlot;

namespace QAMP.Visualization
{
    public partial class SpectrumControl : UserControl
    {
        private ScottPlot.Plottables.BarPlot? myBars;
        private int _barCount;
        private double[] _smoothedValues;
        private double[] _peakValues;
        private ScottPlot.Plottables.BarPlot? _peakBars;
        private readonly List<ScottPlot.Plottables.Scatter> _lineSegments = [];
        private readonly List<ScottPlot.Plottables.Scatter> _peakLineSegments = [];
        private readonly List<double[]> _lineSegmentValues = [];
        private readonly List<double[]> _peakLineSegmentValues = [];
        private double[] _lineValues = [];
        private double[] _linePeakValues = [];

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

                myBars = null;
                _peakBars = null;
                _lineSegments.Clear();
                _peakLineSegments.Clear();
                _lineSegmentValues.Clear();
                _peakLineSegmentValues.Clear();

                if (SettingsManager.Instance.Config.SpectrumType == SpectrumDisplayType.Line)
                {
                    double[] positions = Enumerable.Range(0, BarCount).Select(i => (double)i).ToArray();
                    _lineValues = barValues;
                    _linePeakValues = peakValues;
                    for (int i = 0; i < BarCount - 1; i++)
                    {
                        double[] segmentPositions = [positions[i], positions[i + 1]];
                        double[] segmentValues = [_lineValues[i], _lineValues[i + 1]];
                        double[] peakSegmentValues = [_linePeakValues[i], _linePeakValues[i + 1]];
                        var segment = SpectrumPlot.Plot.Add.Scatter(segmentPositions, segmentValues);
                        var peakSegment = SpectrumPlot.Plot.Add.Scatter(segmentPositions, peakSegmentValues);
                        segment.MarkerSize = 0;
                        peakSegment.MarkerSize = 0;
                        _lineSegments.Add(segment);
                        _peakLineSegments.Add(peakSegment);
                        _lineSegmentValues.Add(segmentValues);
                        _peakLineSegmentValues.Add(peakSegmentValues);
                    }
                }
                else
                {
                    _peakBars = SpectrumPlot.Plot.Add.Bars(peakValues);
                    myBars = SpectrumPlot.Plot.Add.Bars(barValues);

                    for (int i = 0; i < BarCount; i++)
                    {
                        myBars.Bars[i].Position = i;
                        myBars.Bars[i].ValueBase = 0;
                        myBars.Bars[i].Size = 0.78;

                        _peakBars.Bars[i].Position = i;
                        _peakBars.Bars[i].ValueBase = 0;
                        _peakBars.Bars[i].Size = 0.78;
                    }
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

                System.Diagnostics.Debug.WriteLine($"SetupPlot completed, type: {SettingsManager.Instance.Config.SpectrumType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupPlot Error: {ex.Message}");
            }
        }

        private void ApplyGradient()
        {
            if (myBars == null || _peakBars == null)
            {
                return;
            }

            var config = SettingsManager.Instance.Config;
            if (!config.UseSpectrumGradient)
            {
                return;
            }

            var startColor = ParseHexColor(config.SpectrumGradientStartColor, System.Windows.Media.Colors.DeepSkyBlue);
            var endColor = ParseHexColor(config.SpectrumGradientEndColor, System.Windows.Media.Colors.Violet);
            int barCount = myBars.Bars.Count;

            for (int i = 0; i < barCount; i++)
            {
                double horizontalT = barCount == 1 ? 0 : (double)i / (barCount - 1);
                double heightT = Math.Clamp(myBars.Bars[i].Value, 0.0, 1.0);

                ScottPlot.Color plotColor;

                // switch (config.GradientType)
                // {
                //     case SpectrumGradientType.Horizontal:
                plotColor = ToScottPlotColor(InterpolateColor(startColor, endColor, horizontalT));
                // break;

                // case SpectrumGradientType.FullHeight:
                //     plotColor = ToScottPlotColor(InterpolateColor(startColor, endColor, heightT));
                //     break;

                // case SpectrumGradientType.HeightBased:
                // default:
                //     double topBlend = 1.0 - Math.Abs(0.5 - heightT) * 2.0;
                //     plotColor = ToScottPlotColor(InterpolateColor(startColor, endColor, Math.Clamp(topBlend, 0.0, 1.0)));
                //     break;
                // }

                myBars.Bars[i].FillColor = plotColor;
                myBars.Bars[i].LineStyle.Color = plotColor;
                myBars.Bars[i].LineStyle.Width = 0;
                _peakBars.Bars[i].FillColor = plotColor;
                _peakBars.Bars[i].LineStyle.Color = plotColor;
                _peakBars.Bars[i].LineStyle.Width = 0;
            }
        }

        private static System.Windows.Media.Color ParseHexColor(string? value, System.Windows.Media.Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var normalized = value.Trim();
            if (!normalized.StartsWith("#"))
            {
                normalized = $"#{normalized}";
            }

            try
            {
                return (System.Windows.Media.Color)ColorConverter.ConvertFromString(normalized);
            }
            catch
            {
                return fallback;
            }
        }

        private static System.Windows.Media.Color InterpolateColor(System.Windows.Media.Color start, System.Windows.Media.Color end, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            byte a = (byte)(start.A + (end.A - start.A) * t);
            byte r = (byte)(start.R + (end.R - start.R) * t);
            byte g = (byte)(start.G + (end.G - start.G) * t);
            byte b = (byte)(start.B + (end.B - start.B) * t);

            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }

        private static ScottPlot.Color ToScottPlotColor(System.Windows.Media.Color color)
        {
            int argb = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
            return ScottPlot.Color.FromARGB(argb);
        }

        private void ApplyColors()
        {
            var config = SettingsManager.Instance.Config;

            if (!string.IsNullOrEmpty(config.CustomBackgroundPath))
            {
                SpectrumPlot.Plot.FigureBackground.Color = ScottPlot.Colors.Transparent;
                SpectrumPlot.Plot.DataBackground.Color = ScottPlot.Colors.Transparent;
            }
            else
            {
                if (Application.Current.Resources["TertiaryBackgroundBrush"] is System.Windows.Media.SolidColorBrush bgBrush)
                {
                    var c = bgBrush.Color;

                    int argb = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;

                    SpectrumPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromARGB(argb);
                    SpectrumPlot.Plot.DataBackground.Color = ScottPlot.Color.FromARGB(argb);
                }
                else
                {
                    SpectrumPlot.Plot.FigureBackground.Color = ScottPlot.Colors.Black;
                    SpectrumPlot.Plot.DataBackground.Color = ScottPlot.Colors.Black;
                }
            }

            if (myBars != null && _peakBars != null)
            {
                if (config.UseSpectrumGradient)
                {
                    ApplyGradient();
                }
                else
                {
                    ScottPlot.Color plotColor;

                    if (Application.Current.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accent)
                    {
                        var ac = accent.Color;
                        int accentArgb = (ac.A << 24) | (ac.R << 16) | (ac.G << 8) | ac.B;

                        plotColor = ScottPlot.Color.FromARGB(accentArgb);
                    }
                    else
                    {
                        plotColor = ScottPlot.Colors.LimeGreen;
                    }

                    myBars.Color = plotColor;
                    _peakBars.Color = plotColor;

                    foreach (var bar in myBars.Bars)
                    {
                        bar.FillColor = plotColor;
                        bar.LineStyle.Color = plotColor;
                        bar.LineStyle.Width = 0;
                    }
                    foreach (var bar in _peakBars.Bars)
                    {
                        bar.FillColor = plotColor;
                        bar.LineStyle.Color = plotColor;
                        bar.LineStyle.Width = 0;
                    }
                }
            }

            if (_lineSegments.Count > 0 && _peakLineSegments.Count > 0)
            {
                var startColor = ParseHexColor(config.SpectrumGradientStartColor, System.Windows.Media.Colors.DeepSkyBlue);
                var endColor = ParseHexColor(config.SpectrumGradientEndColor, System.Windows.Media.Colors.Violet);
                for (int i = 0; i < _lineSegments.Count; i++)
                {
                    double t = (double)i / Math.Max(1, _lineSegments.Count - 1);
                    ScottPlot.Color plotColor = config.UseSpectrumGradient
                        ? ToScottPlotColor(InterpolateColor(startColor, endColor, t))
                        : GetAccentPlotColor();
                    _lineSegments[i].Color = plotColor;
                    _peakLineSegments[i].Color = plotColor;
                    _lineSegments[i].LineWidth = 1.5f;
                    _peakLineSegments[i].LineWidth = 1.0f;
                }
            }

            SpectrumPlot.Refresh();
        }

        /// <summary>
        /// Обновляет цвета спектра при смене темы или цвета акцента
        /// </summary>
        public void RefreshColors()
        {
            if (SpectrumPlot == null) return;

            ApplyColors();
            SpectrumPlot.Refresh();
            System.Diagnostics.Debug.WriteLine("SpectrumControl colors refreshed");
        }
        public void UpdateSpectrum(double[] spectrumData, double[] peakData, int incomingCount)
        {
            if (spectrumData == null || peakData == null) return;

            if (!SettingsManager.Instance.Config.IsVisualizerEnabled)
            {
                bool hasActiveValues = false;
                for (int i = 0; i < BarCount; i++)
                {
                    if ((myBars?.Bars[i].Value ?? 0) > 0 || _lineValues[i] > 0)
                    {
                        hasActiveValues = true;
                        break;
                    }
                }

                if (hasActiveValues)
                {
                    ResetPeaks();
                    SpectrumPlot.Refresh();
                }
                return;
            }

            try
            {
                int limit = Math.Min(incomingCount, BarCount);
                for (int i = 0; i < limit; i++)
                {
                    if (myBars != null && _peakBars != null)
                    {
                        myBars.Bars[i].Value = spectrumData[i];
                        _peakBars.Bars[i].Value = peakData[i];
                    }
                    else if (_lineSegments.Count > 0)
                    {
                        _lineValues[i] = spectrumData[i];
                        _linePeakValues[i] = peakData[i];
                        if (i > 0)
                        {
                            _lineSegmentValues[i - 1][1] = _lineValues[i];
                            _peakLineSegmentValues[i - 1][1] = _linePeakValues[i];
                        }
                        if (i < _lineSegments.Count)
                        {
                            _lineSegmentValues[i][0] = _lineValues[i];
                            _peakLineSegmentValues[i][0] = _linePeakValues[i];
                        }
                    }
                }

                SpectrumPlot.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSpectrum Error: {ex.Message}");
            }
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
            if (myBars == null && _lineSegments.Count == 0) return;

            for (int i = 0; i < BarCount; i++)
            {
                if (myBars != null && _peakBars != null)
                {
                    myBars.Bars[i].Value = 0.01;
                    _peakBars.Bars[i].Value = 0.01;
                }
                else if (_lineSegments.Count > 0)
                {
                    _lineValues[i] = 0.01;
                    _linePeakValues[i] = 0.01;
                    if (i > 0)
                    {
                        _lineSegmentValues[i - 1][1] = 0.01;
                        _peakLineSegmentValues[i - 1][1] = 0.01;
                    }
                    if (i < _lineSegments.Count)
                    {
                        _lineSegmentValues[i][0] = 0.01;
                        _peakLineSegmentValues[i][0] = 0.01;
                    }
                }
            }
            SpectrumPlot.Refresh();
        }

        public void RefreshDisplayType()
        {
            SetupPlot();
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

        private static ScottPlot.Color GetAccentPlotColor()
        {
            if (Application.Current.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accent)
            {
                var color = accent.Color;
                return ScottPlot.Color.FromARGB((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
            }

            return ScottPlot.Colors.LimeGreen;
        }
    }
    public enum SpectrumDisplayType
    {
        Bars,
        Line
    }

    public enum SpectrumGradientType
    {
        // HeightBased,
        // FullHeight,
        Horizontal
    }

}
