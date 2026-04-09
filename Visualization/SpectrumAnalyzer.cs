using System.Numerics;
using System.Windows;
using FftSharp;

namespace QAMP.Visualization
{
    public class SpectrumAnalyzer
    {
        private readonly SpectrumSettings _settings;
        private readonly double[] fftBuffer;
        private readonly int fftSize = 4096;
        private double currentMaxValue = 0.1;
        private readonly double[] previousValues;

        public event EventHandler<(double[] Data, double MaxY)>? SpectrumUpdated;

        public SpectrumAnalyzer(SpectrumSettings? settings = null)
        {
            _settings = settings ?? new SpectrumSettings();
            fftBuffer = new double[_settings.PointCount];
            previousValues = new double[_settings.PointCount];

            for (int i = 0; i < _settings.PointCount; i++)
            {
                fftBuffer[i] = _settings.MinBarValue;
                previousValues[i] = _settings.MinBarValue;
            }
        }

        public void ProcessSamples(float[] samples, int samplesRead)
        {
            if (samplesRead < 100) return;

            double[] doubleSamples = new double[samplesRead];
            for (int i = 0; i < samplesRead; i++)
                doubleSamples[i] = samples[i];

            double[] spectrum = ComputeSpectrum(doubleSamples);
            UpdateVisualization(spectrum);
        }

        private double[] ComputeSpectrum(double[] samples)
        {
            try
            {
                int fftSize = 4096;
                double[] padded = new double[fftSize];
                Array.Copy(samples, padded, Math.Min(samples.Length, fftSize));

                var window = new FftSharp.Windows.Hanning();
                window.ApplyInPlace(padded);

                Complex[] spectrum = FFT.Forward(padded);
                double[] magnitude = FFT.Magnitude(spectrum);

                return magnitude;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFT Error: {ex.Message}");
                return new double[fftSize / 2];
            }
        }

        private void UpdateVisualization(double[] spectrum)
        {
            if (spectrum.Length < 2) return;

            try
            {
                int halfLength = spectrum.Length / 2;
                double[] newValues = new double[_settings.PointCount];
                double frameMax = 0;

                for (int i = 0; i < _settings.PointCount; i++)
                {
                    // Логарифмическое распределение частот
                    double percent = (double)i / _settings.PointCount;
                    double logPercent = Math.Pow(percent, _settings.FreqPower);
                    double nextLogPercent = Math.Pow((double)(i + 1) / _settings.PointCount, _settings.FreqPower);

                    int startIndex = (int)(logPercent * (halfLength - 10));
                    int endIndex = (int)(nextLogPercent * (halfLength - 10));
                    endIndex = Math.Max(startIndex + 1, endIndex);

                    double maxInBand = 0;
                    for (int j = startIndex; j < endIndex && j < halfLength; j++)
                    {
                        if (spectrum[j] > maxInBand) maxInBand = spectrum[j];
                    }

                    double newValue = maxInBand * _settings.AmplitudeGain;
                    newValue = Math.Pow(newValue, _settings.AmplitudePower);
                    
                    if (newValue > previousValues[i])
                        newValues[i] = previousValues[i] + (newValue - previousValues[i]) * _settings.AttackSpeed;
                    else
                        newValues[i] = previousValues[i] * _settings.ReleaseSpeed;

                    newValues[i] = Math.Min(_settings.MaxBarValue, Math.Max(_settings.MinBarValue, newValues[i]));
                    
                    if (newValues[i] > frameMax) frameMax = newValues[i];
                    previousValues[i] = newValues[i];
                }

                Array.Copy(newValues, fftBuffer, _settings.PointCount);

                if (_settings.AutoNormalize)
                {
                    currentMaxValue = Math.Max(frameMax, currentMaxValue * 0.98);
                    double maxY = Math.Max(0.3, currentMaxValue * 1.1);
                    
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        SpectrumUpdated?.Invoke(this, (fftBuffer, maxY));
                    });
                }
                else
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        SpectrumUpdated?.Invoke(this, (fftBuffer, 1.0));
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
            }
        }

        public void Reset()
        {
            for (int i = 0; i < _settings.PointCount; i++)
            {
                fftBuffer[i] = _settings.MinBarValue;
                previousValues[i] = _settings.MinBarValue;
            }
            currentMaxValue = 0.1;
        }
        
        public void SetPreset(string presetName)
        {
            _settings.ApplyPreset(presetName);
        }
    }
}