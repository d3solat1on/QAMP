using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QAMP.Models;

namespace QAMP.Visualization
{
    public partial class SpectrumControl : UserControl
    {
        public const int MinimumConfiguredBarCount = 16;
        public const int MaximumConfiguredBarCount = 256;
        public const int MaximumBarCount = MaximumConfiguredBarCount * 2;
        private const int MaximumLineGradientSegments = 32;

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
        private readonly List<int> _lineSegmentStartIndices = [];
        private double[] _lineValues = [];
        private double[] _linePeakValues = [];

        public bool IsFullScreenSpectrum { get; set; }
        public int BarCount => _barCount;

        public SpectrumControl()
        {
            InitializeComponent();
            _barCount = Math.Clamp(
                SettingsManager.Instance.Config.VisualizerBarCount,
                MinimumConfiguredBarCount,
                MaximumConfiguredBarCount);
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
                Interval = TimeSpan.FromMilliseconds(25)
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
                return;

            try
            {
                var config = SettingsManager.Instance.Config;
                int count = BarCount;

                SpectrumPlot.Plot.Clear();

                myBars = null;
                _peakBars = null;
                _topPeakBars = null;
                _bottomPeakBars = null;
                _lineSegments.Clear();
                _peakLineSegments.Clear();
                _lineSegmentValues.Clear();
                _peakLineSegmentValues.Clear();
                _lineSegmentStartIndices.Clear();

                _lineValues = new double[count];
                _linePeakValues = new double[count];
                Array.Fill(_lineValues, 0.01);
                Array.Fill(_linePeakValues, 0.01);

                if (config.SpectrumType == SpectrumDisplayType.Line)
                {
                    int segmentCount = Math.Min(count - 1, MaximumLineGradientSegments);
                    for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                    {
                        int startIndex = segmentIndex * (count - 1) / segmentCount;
                        int endIndex = (segmentIndex + 1) * (count - 1) / segmentCount + 1;
                        int segmentLength = endIndex - startIndex;
                        double[] segmentPositions = new double[segmentLength];
                        double[] segmentValues = new double[segmentLength];
                        double[] peakSegmentValues = new double[segmentLength];

                        for (int i = 0; i < segmentLength; i++)
                        {
                            int dataIndex = startIndex + i;
                            segmentPositions[i] = dataIndex;
                            segmentValues[i] = _lineValues[dataIndex];
                            peakSegmentValues[i] = _linePeakValues[dataIndex];
                        }

                        var segment = SpectrumPlot.Plot.Add.Scatter(segmentPositions, segmentValues);
                        var peakSegment = SpectrumPlot.Plot.Add.Scatter(segmentPositions, peakSegmentValues);
                        segment.MarkerSize = 0;
                        peakSegment.MarkerSize = 0;

                        _lineSegments.Add(segment);
                        _peakLineSegments.Add(peakSegment);
                        _lineSegmentValues.Add(segmentValues);
                        _peakLineSegmentValues.Add(peakSegmentValues);
                        _lineSegmentStartIndices.Add(startIndex);
                    }
                }
                else if (config.SpectrumType == SpectrumDisplayType.CenteredBars)
                {
                    _topPeakBars = SpectrumPlot.Plot.Add.Bars(_linePeakValues);
                    _bottomPeakBars = SpectrumPlot.Plot.Add.Bars(_linePeakValues);
                    myBars = SpectrumPlot.Plot.Add.Bars(_lineValues);

                    for (int i = 0; i < count; i++)
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
                    _peakBars = SpectrumPlot.Plot.Add.Bars(_linePeakValues);
                    myBars = SpectrumPlot.Plot.Add.Bars(_lineValues);

                    for (int i = 0; i < count; i++)
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
                    config.SpectrumType == SpectrumDisplayType.CenteredBars ? -1.0 : 0.0,
                    1.0);
                SpectrumPlot.Plot.Axes.SetLimitsX(0, count);
                SpectrumPlot.Plot.Axes.Top.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Right.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Left.FrameLineStyle.Width = 0;
                SpectrumPlot.Plot.Axes.Bottom.FrameLineStyle.Width = 0;
                SpectrumPlot.UserInputProcessor.IsEnabled = false;
                SpectrumPlot.IsEnabled = false;
                ApplyColors();

                if (_hasSpectrumData)
                    ApplySpectrumValues();
                else
                    SpectrumPlot.Refresh();
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
            if (!normalized.StartsWith('#'))
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
            System.Diagnostics.Debug.WriteLine("SpectrumControl colors refreshed");
        }
        public void UpdateSpectrum(double[] spectrumData, double[] peakData, int incomingCount)
        {
            if (!IsVisible || Visibility != Visibility.Visible) return;
            if (spectrumData == null || peakData == null) return;

            try
            {
                _latestSampleCount = Math.Min(Math.Min(incomingCount, BarCount), Math.Min(spectrumData.Length, peakData.Length));
                if (_latestSampleCount <= 0)
                    return;

                _latestSpectrumValues = spectrumData;
                _latestPeakValues = peakData;
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
                int count = Math.Min(_latestSampleCount, BarCount);
                if (count <= 0)
                    return;

                var displayType = SettingsManager.Instance.Config.SpectrumType;
                double fade = _fadeMultiplier;
                double peakCapOffset = 0.03 * fade;

                if (displayType == SpectrumDisplayType.CenteredBars)
                {
                    if (myBars == null || _topPeakBars == null || _bottomPeakBars == null)
                        return;

                    int maxBars = Math.Min(count, Math.Min(myBars.Bars.Count, Math.Min(_topPeakBars.Bars.Count, _bottomPeakBars.Bars.Count)));
                    for (int i = 0; i < maxBars; i++)
                    {
                        double value = _latestSpectrumValues[i] * fade;
                        double peak = _latestPeakValues[i] * fade;

                        myBars.Bars[i].Value = value;
                        myBars.Bars[i].ValueBase = -value;
                        _topPeakBars.Bars[i].Value = peak + peakCapOffset;
                        _topPeakBars.Bars[i].ValueBase = peak;
                        _bottomPeakBars.Bars[i].Value = -peak;
                        _bottomPeakBars.Bars[i].ValueBase = -(peak + peakCapOffset);
                    }
                }
                else if (displayType == SpectrumDisplayType.Line && _lineSegments.Count > 0)
                {
                    int lineCount = Math.Min(count, Math.Min(_lineValues.Length, _linePeakValues.Length));
                    for (int i = 0; i < lineCount; i++)
                    {
                        double value = _latestSpectrumValues[i] * fade;
                        double peak = _latestPeakValues[i] * fade;

                        _lineValues[i] = value;
                        _linePeakValues[i] = peak;
                    }

                    for (int segmentIndex = 0; segmentIndex < _lineSegmentValues.Count; segmentIndex++)
                    {
                        int startIndex = _lineSegmentStartIndices[segmentIndex];
                        double[] segmentValues = _lineSegmentValues[segmentIndex];
                        double[] peakSegmentValues = _peakLineSegmentValues[segmentIndex];

                        for (int i = 0; i < segmentValues.Length; i++)
                        {
                            int dataIndex = startIndex + i;
                            segmentValues[i] = _lineValues[dataIndex];
                            peakSegmentValues[i] = _linePeakValues[dataIndex];
                        }
                    }
                }
                else if (myBars != null && _peakBars != null)
                {
                    int maxBars = Math.Min(count, Math.Min(myBars.Bars.Count, _peakBars.Bars.Count));
                    for (int i = 0; i < maxBars; i++)
                    {
                        myBars.Bars[i].Value = _latestSpectrumValues[i] * fade;
                        _peakBars.Bars[i].Value = _latestPeakValues[i] * fade;
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

            if (_lineSegments.Count > 0)
            {
                Array.Fill(_lineValues, 0.01);
                Array.Fill(_linePeakValues, 0.01);
                for (int segmentIndex = 0; segmentIndex < _lineSegmentValues.Count; segmentIndex++)
                {
                    Array.Fill(_lineSegmentValues[segmentIndex], 0.01);
                    Array.Fill(_peakLineSegmentValues[segmentIndex], 0.01);
                }

                SpectrumPlot.Refresh();
                return;
            }

            for (int i = 0; i < BarCount; i++)
            {
                if (myBars != null && _peakBars != null)
                {
                    myBars.Bars[i].Value = 0.01;
                    _peakBars.Bars[i].Value = 0.01;
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
            if (count <= 0)
                return;

            count = Math.Min(count, MaximumBarCount);
            if (count == _barCount)
                return;

            _barCount = count;
            _smoothedValues = new double[_barCount];
            _peakValues = new double[_barCount];
            var resizedSpectrumValues = new double[_barCount];
            var resizedPeakValues = new double[_barCount];
            Array.Copy(_latestSpectrumValues, resizedSpectrumValues, Math.Min(_latestSpectrumValues.Length, _barCount));
            Array.Copy(_latestPeakValues, resizedPeakValues, Math.Min(_latestPeakValues.Length, _barCount));
            _latestSpectrumValues = resizedSpectrumValues;
            _latestPeakValues = resizedPeakValues;
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
