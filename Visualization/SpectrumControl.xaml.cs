using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QAMP.Models;

namespace QAMP.Visualization
{
    public partial class SpectrumControl : UserControl
    {
        private ScottPlot.Plottables.BarPlot? myBars;
        private ScottPlot.Plottables.BarPlot? _peakBars;
        private ScottPlot.Plottables.BarPlot? _topPeakBars;
        private ScottPlot.Plottables.BarPlot? _bottomPeakBars;
        private double _fadeMultiplier = 0.0;
        private readonly DispatcherTimer _fadeTimer;
        private long _lastFadeTimestamp;
        private int _barCount;
        private double[] _smoothedValues;
        private double[] _peakValues;
        private double[] _latestSpectrumValues;
        private double[] _latestPeakValues;
        private int _latestSampleCount;
        private bool _hasSpectrumData;
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
            _latestSpectrumValues = new double[_barCount];
            _latestPeakValues = new double[_barCount];
            for (int i = 0; i < _barCount; i++)
            {
                _smoothedValues[i] = 0.02;
                _peakValues[i] = 0.02;
            }
            _fadeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _fadeTimer.Tick += FadeTimer_Tick;
            _peakBars = SpectrumPlot.Plot.Add.Bars(_peakValues);
            System.Diagnostics.Debug.WriteLine($"SpectrumControl initialized, peakValues[0] = {_peakValues[0]}");

            Loaded += SpectrumControl_Loaded;
            Unloaded += SpectrumControl_Unloaded;
        }

        private void SpectrumControl_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < BarCount; i++)
            {
                _peakValues[i] = 0.02;
                _smoothedValues[i] = 0.02;
            }

            SetupPlot();
            _lastFadeTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _fadeTimer.Start();
        }

        private void SpectrumControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _fadeTimer.Stop();
        }

        private void FadeTimer_Tick(object? sender, EventArgs e)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double elapsedSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(_lastFadeTimestamp, now).TotalSeconds;
            _lastFadeTimestamp = now;

            bool isPlaying = SettingsManager.Instance.Config.IsVisualizerEnabled &&
                             Services.PlayerService.Instance.IsPlaying;
            double target = isPlaying ? 1.0 : 0.0;
            double responseTime = isPlaying ? 0.06 : 0.16;
            double amount = 1.0 - Math.Exp(-elapsedSeconds / responseTime);
            double previousFade = _fadeMultiplier;
            _fadeMultiplier += (target - _fadeMultiplier) * amount;

            if (Math.Abs(target - _fadeMultiplier) < 0.005)
                _fadeMultiplier = target;

            if (_hasSpectrumData && Math.Abs(previousFade - _fadeMultiplier) > 0.0001)
                ApplySpectrumValues();
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
                _topPeakBars = null;
                _bottomPeakBars = null;
                _lineValues = [];
                _linePeakValues = [];
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
                else if (SettingsManager.Instance.Config.SpectrumType == SpectrumDisplayType.CenteredBars)
                {
                    _topPeakBars = SpectrumPlot.Plot.Add.Bars(peakValues);
                    _bottomPeakBars = SpectrumPlot.Plot.Add.Bars(peakValues);
                    myBars = SpectrumPlot.Plot.Add.Bars(barValues);

                    for (int i = 0; i < BarCount; i++)
                    {
                        myBars.Bars[i].Position = i;
                        myBars.Bars[i].Size = 0.78;

                        _topPeakBars.Bars[i].Position = i;
                        _topPeakBars.Bars[i].Size = 0.78;

                        _bottomPeakBars.Bars[i].Position = i;
                        _bottomPeakBars.Bars[i].Size = 0.78;
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

                SpectrumPlot.Plot.Axes.SetLimitsY(
                    SettingsManager.Instance.Config.SpectrumType == SpectrumDisplayType.CenteredBars ? -1.0 : 0.0,
                    1.0);
                SpectrumPlot.Plot.Axes.SetLimitsX(0, BarCount);
                SpectrumPlot.Plot.Axes.Top.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Right.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Left.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Bottom.FrameLineStyle.Width = 0;


                SpectrumPlot.IsEnabled = false;
                ApplyColors();
                if (_hasSpectrumData)
                    ApplySpectrumValues();
                SpectrumPlot.Refresh();

                System.Diagnostics.Debug.WriteLine($"SetupPlot completed, type: {SettingsManager.Instance.Config.SpectrumType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupPlot Error: {ex.Message}");
            }
        }

        private static void ApplyBarPlotColors(
            ScottPlot.Plottables.BarPlot? bars,
            bool useGradient,
            System.Windows.Media.Color startColor,
            System.Windows.Media.Color endColor,
            ScottPlot.Color solidColor)
        {
            if (bars == null)
                return;

            int barCount = bars.Bars.Count;

            for (int i = 0; i < barCount; i++)
            {
                double t = barCount == 1 ? 0 : (double)i / (barCount - 1);
                ScottPlot.Color plotColor = useGradient
                    ? ToScottPlotColor(InterpolateColor(startColor, endColor, t))
                    : solidColor;

                bars.Bars[i].FillColor = plotColor;
                bars.Bars[i].LineStyle.Color = plotColor;
                bars.Bars[i].LineStyle.Width = 0;
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

            ScottPlot.Color accentColor = GetAccentPlotColor();
            bool useGradient = config.UseSpectrumGradient;
            var startColor = ParseHexColor(config.SpectrumGradientStartColor, System.Windows.Media.Colors.DeepSkyBlue);
            var endColor = ParseHexColor(config.SpectrumGradientEndColor, System.Windows.Media.Colors.Violet);

            ApplyBarPlotColors(myBars, useGradient, startColor, endColor, accentColor);
            ApplyBarPlotColors(_peakBars, useGradient, startColor, endColor, accentColor);
            ApplyBarPlotColors(_topPeakBars, useGradient, startColor, endColor, accentColor);
            ApplyBarPlotColors(_bottomPeakBars, useGradient, startColor, endColor, accentColor);

            if (_lineSegments.Count > 0 && _peakLineSegments.Count > 0)
            {
                for (int i = 0; i < _lineSegments.Count; i++)
                {
                    double t = (double)i / Math.Max(1, _lineSegments.Count - 1);
                    ScottPlot.Color plotColor = useGradient
                        ? ToScottPlotColor(InterpolateColor(startColor, endColor, t))
                        : accentColor;
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
            if (!IsVisible || Visibility != Visibility.Visible) return;
            if (spectrumData == null || peakData == null) return;

            try
            {
                _latestSampleCount = Math.Min(Math.Min(incomingCount, BarCount), Math.Min(spectrumData.Length, peakData.Length));
                Array.Copy(spectrumData, _latestSpectrumValues, _latestSampleCount);
                Array.Copy(peakData, _latestPeakValues, _latestSampleCount);
                _hasSpectrumData = true;
                ApplySpectrumValues();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSpectrum Error: {ex.Message}");
            }
        }

        private void ApplySpectrumValues()
        {
            try
            {
                for (int i = 0; i < _latestSampleCount; i++)
                {
                    double value = _latestSpectrumValues[i] * _fadeMultiplier;
                    double peak = _latestPeakValues[i] * _fadeMultiplier;

                    if (SettingsManager.Instance.Config.SpectrumType == SpectrumDisplayType.CenteredBars)
                    {
                        if (myBars == null || _topPeakBars == null || _bottomPeakBars == null)
                            continue;

                        myBars.Bars[i].Value = value;
                        myBars.Bars[i].ValueBase = -value;
                        _topPeakBars.Bars[i].Value = peak + 0.03 * _fadeMultiplier;
                        _topPeakBars.Bars[i].ValueBase = peak;
                        _bottomPeakBars.Bars[i].Value = -peak;
                        _bottomPeakBars.Bars[i].ValueBase = -(peak + 0.03 * _fadeMultiplier);
                    }
                    else if (_lineSegments.Count > 0)
                    {
                        _lineValues[i] = value;
                        _linePeakValues[i] = peak;
                        if (i > 0)
                        {
                            _lineSegmentValues[i - 1][1] = value;
                            _peakLineSegmentValues[i - 1][1] = peak;
                        }
                        if (i < _lineSegments.Count)
                        {
                            _lineSegmentValues[i][0] = value;
                            _peakLineSegmentValues[i][0] = peak;
                        }
                    }
                    else if (myBars != null && _peakBars != null)
                    {
                        myBars.Bars[i].Value = value;
                        _peakBars.Bars[i].Value = peak;
                    }
                }

                SpectrumPlot.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplySpectrumValues Error: {ex.Message}");
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
                else if (_topPeakBars != null && _bottomPeakBars != null)
                {
                    myBars?.Bars[i].Value = 0.01;
                    myBars?.Bars[i].ValueBase = -0.01;
                    _topPeakBars?.Bars[i].Value = 0.02;
                    _topPeakBars?.Bars[i].ValueBase = 0.01;
                    _bottomPeakBars?.Bars[i].Value = -0.01;
                    _bottomPeakBars?.Bars[i].ValueBase = -0.02;
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
            Array.Resize(ref _latestSpectrumValues, _barCount);
            Array.Resize(ref _latestPeakValues, _barCount);
            _latestSampleCount = Math.Min(_latestSampleCount, _barCount);

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
        Line,
        CenteredBars
    }

    public enum SpectrumGradientType
    {
        Horizontal
    }

}
