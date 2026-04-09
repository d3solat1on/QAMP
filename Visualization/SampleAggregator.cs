using System.Windows;
using NAudio.Dsp;
using NAudio.Wave;
using QAMP.Models;

namespace QAMP.Visualization
{
    public class SampleAggregator : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int fftSize = 512;
        private readonly Complex[] fftBuffer;
        private readonly float[] lastFftResults;
        private readonly float[] windowBuffer;
        private int fftPos;
        private readonly Lock _lockObject = new();

        public event EventHandler<float[]>? FftCalculated;

        public WaveFormat WaveFormat => source.WaveFormat;

        public SampleAggregator(ISampleProvider source, int unused = 4096)
        {
            this.source = source;
            fftBuffer = new Complex[fftSize];
            lastFftResults = new float[fftSize / 2];
            windowBuffer = new float[fftSize];

            for (int i = 0; i < fftSize; i++)
                windowBuffer[i] = (float)(0.5 * (1.0 - Math.Cos(2 * Math.PI * i / fftSize)));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            for (int n = 0; n < samplesRead; n++)
            {
                Add(buffer[offset + n]);
            }

            return samplesRead;
        }

        private void Add(float value)
        {
            if (!SettingsManager.Instance.Config.IsVisualizerEnabled)
            {
                return;
            }

            lock (_lockObject)
            {
                fftBuffer[fftPos].X = value * windowBuffer[fftPos];
                fftBuffer[fftPos].Y = 0;
                fftPos++;

                if (fftPos >= fftSize)
                {
                    fftPos = 0;
                    ProcessFFT(fftBuffer);
                }
            }
        }

        private void ProcessFFT(Complex[] fftData)
        {
            try
            {
                FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2), fftData);

                for (int i = 0; i < fftSize / 2; i++)
                {
                    float magnitude = (float)Math.Sqrt(fftData[i].X * fftData[i].X + fftData[i].Y * fftData[i].Y);
                    magnitude *= 225f;
                    lastFftResults[i] = magnitude;
                }

                float[] result = new float[fftSize / 2];
                Array.Copy(lastFftResults, result, fftSize / 2);

                // Используем Background вместо Dispatcher для улучшения производительности
                Task.Run(() =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(
                        new Action(() => { FftCalculated?.Invoke(this, result); }),
                        System.Windows.Threading.DispatcherPriority.Render); 
                });
            }
            catch { /* ignore */ }
        }
    }
}