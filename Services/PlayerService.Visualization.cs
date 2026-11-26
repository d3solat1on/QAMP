using Un4seen.Bass;
using QAMP.Models;
using QAMP.Visualization;
using Un4seen.BassWasapi;
using System.Numerics;

namespace QAMP.Services;

public partial class PlayerService
{
    private sealed class SpectrumBuffers
    {
        public double[] Spectrum { get; }
        public double[] Peaks { get; }
        public long Frame { get; set; }

        public SpectrumBuffers(int count)
        {
            Spectrum = new double[count];
            Peaks = new double[count];
        }
    }

    private readonly Dictionary<int, SpectrumBuffers> _resampledSpectrumBuffers = [];
    private long _spectrumFrame;

    public void AddSpectrumControl(SpectrumControl control)
    {
        if (control == null) return;
        if (!SpectrumControls.Contains(control))
        {
            SpectrumControls.Add(control);
        }
    }

    public void RemoveSpectrumControl(SpectrumControl control)
    {
        if (control == null) return;
        SpectrumControls.Remove(control);
    }

    public void RefreshSpectrumControls()
    {
        int configuredBarCount = Math.Clamp(
            SettingsManager.Instance.Config.VisualizerBarCount,
            SpectrumControl.MinimumConfiguredBarCount,
            SpectrumControl.MaximumConfiguredBarCount);

        foreach (var control in SpectrumControls)
        {
            int multiplier = control.IsFullScreenSpectrum ? 2 : 1;
            int targetBarCount = configuredBarCount * multiplier;
            if (control.BarCount == targetBarCount)
                control.RefreshDisplayType();
            else
                control.SetBarCount(targetBarCount);
            control.RefreshColors();
        }
    }

    private void UpdateSpectrumTimerInterval()
    {
        int fps = SettingsManager.Instance.Config.SpectrumFps;
        if (fps <= 0) fps = 60;

        _spectrumTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
    }

    private void SpectrumTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentStream == 0 || !IsPlaying || !SettingsManager.Instance.Config.IsVisualizerEnabled)
            return;

        int maxBarsNeeded = 0;
        foreach (var control in SpectrumControls)
        {
            if (control.IsVisible && control.Visibility == System.Windows.Visibility.Visible)
            {
                if (control.BarCount > maxBarsNeeded)
                    maxBarsNeeded = control.BarCount;
            }
        }

        if (maxBarsNeeded <= 0)
            return;

        maxBarsNeeded = Math.Min(maxBarsNeeded, _nativeSpectrumBuffer.Length);

        int bytesRead = _usesWasapiCurrentStream
            ? BassWasapi.BASS_WASAPI_GetData(_fftBuffer, (int)BASSData.BASS_DATA_FFT1024)
            : Bass.BASS_ChannelGetData(_currentStream, _fftBuffer, (int)BASSData.BASS_DATA_FFT1024);

        if (bytesRead <= 0)
            return;

        if (!Native.QampCoreNative.CalculateSpectrumFromFFT(_fftBuffer, _nativeSpectrumBuffer, _nativePeakBuffer, maxBarsNeeded))
            return;

        ConvertFloatToDoubleSimd(_nativeSpectrumBuffer.AsSpan(0, maxBarsNeeded), _scottPlotBuffer.AsSpan(0, maxBarsNeeded));
        ConvertFloatToDoubleSimd(_nativePeakBuffer.AsSpan(0, maxBarsNeeded), _scottPlotPeakBuffer.AsSpan(0, maxBarsNeeded));

        long currentFrame = ++_spectrumFrame;
        foreach (var control in SpectrumControls)
        {
            if (control.IsVisible && control.Visibility == System.Windows.Visibility.Visible)
            {
                int count = Math.Min(control.BarCount, maxBarsNeeded);
                if (count == maxBarsNeeded)
                {
                    control.UpdateSpectrum(_scottPlotBuffer, _scottPlotPeakBuffer, count);
                }
                else
                {
                    if (!_resampledSpectrumBuffers.TryGetValue(count, out var buffers))
                    {
                        buffers = new SpectrumBuffers(count);
                        _resampledSpectrumBuffers.Add(count, buffers);
                    }

                    if (buffers.Frame != currentFrame)
                    {
                        ResampleSpectrum(
                            _scottPlotBuffer.AsSpan(0, maxBarsNeeded),
                            _scottPlotPeakBuffer.AsSpan(0, maxBarsNeeded),
                            buffers.Spectrum,
                            buffers.Peaks);
                        buffers.Frame = currentFrame;
                    }

                    control.UpdateSpectrum(buffers.Spectrum, buffers.Peaks, count);
                }
            }
        }
    }

    private static void ResampleSpectrum(
        ReadOnlySpan<double> spectrum,
        ReadOnlySpan<double> peaks,
        Span<double> resampledSpectrum,
        Span<double> resampledPeaks)
    {
        int sourceCount = Math.Min(spectrum.Length, peaks.Length);
        int destinationCount = Math.Min(resampledSpectrum.Length, resampledPeaks.Length);

        for (int i = 0; i < destinationCount; i++)
        {
            int start = (int)((long)i * sourceCount / destinationCount);
            int end = (int)((long)(i + 1) * sourceCount / destinationCount);
            double maxSpectrum = 0;
            double maxPeak = 0;

            for (int j = start; j < end; j++)
            {
                maxSpectrum = Math.Max(maxSpectrum, spectrum[j]);
                maxPeak = Math.Max(maxPeak, peaks[j]);
            }

            resampledSpectrum[i] = maxSpectrum;
            resampledPeaks[i] = maxPeak;
        }
    }

    private static void ConvertFloatToDoubleSimd(ReadOnlySpan<float> source, Span<double> destination)
    {
        int i = 0;
        int floatVecSize = Vector<float>.Count;

        if (Vector.IsHardwareAccelerated && source.Length >= floatVecSize)
        {
            int limit = source.Length - floatVecSize;
            for (; i <= limit; i += floatVecSize)
            {
                var floatVec = new Vector<float>(source.Slice(i, floatVecSize));

                Vector.Widen(floatVec, out Vector<double> lowDoubles, out Vector<double> highDoubles);

                lowDoubles.CopyTo(destination.Slice(i, Vector<double>.Count));
                highDoubles.CopyTo(destination.Slice(i + Vector<double>.Count, Vector<double>.Count));
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }
    public static void ConvertAndScaleSimd(ReadOnlySpan<float> source, Span<double> destination, double fade)
    {
        int i = 0;
        int floatVecSize = Vector<float>.Count;   
        int doubleVecSize = Vector<double>.Count; 

        if (Vector.IsHardwareAccelerated && source.Length >= floatVecSize)
        {
            Vector<double> vFade = new(fade);
            int limit = source.Length - floatVecSize;

            for (; i <= limit; i += floatVecSize)
            {
                var vFloat = new Vector<float>(source.Slice(i, floatVecSize));

                Vector.Widen(vFloat, out Vector<double> vLow, out Vector<double> vHigh);

                vLow *= vFade;
                vHigh *= vFade;

                vLow.CopyTo(destination.Slice(i, doubleVecSize));
                vHigh.CopyTo(destination.Slice(i + doubleVecSize, doubleVecSize));
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = source[i] * fade;
        }
    }
}
