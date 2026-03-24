using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace QAMP.Visualization
{
    public class SampleAggregator : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int fftSize;
        private readonly Complex[] fftBuffer;
        private readonly float[] lastFftResults;
        private readonly float[] windowBuffer; // Предсчитанное окно
        private int fftPos;

        // Событие, которое будет передавать рассчитанные данные в UI
        public event EventHandler<float[]> FftCalculated;

        public WaveFormat WaveFormat => source.WaveFormat;

        public SampleAggregator(ISampleProvider source, int fftSize = 4096)
        {
            if (!IsPowerOfTwo(fftSize)) throw new ArgumentException("FFT size must be a power of two");
            this.source = source;
            this.fftSize = fftSize;
            fftBuffer = new Complex[fftSize];
            lastFftResults = new float[fftSize / 2];
            windowBuffer = new float[fftSize];

            // Предсчитываем окно Хеннинга один раз
            for (int i = 0; i < fftSize; i++)
                windowBuffer[i] = (float)(0.5 * (1.0 - Math.Cos(2 * Math.PI * i / fftSize)));
        }

        private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

        public int Read(float[] buffer, int offset, int count)
        {
            // Читаем данные из основного источника (файла)
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
                // Если выключено — просто не считаем FFT, экономя ресурсы
                return;
            }
            // Используем предсчитанное окно для ускорения
            fftBuffer[fftPos].X = value * windowBuffer[fftPos];
            fftBuffer[fftPos].Y = 0;
            fftPos++;

            if (fftPos >= fftSize)
            {
                fftPos = 0;
                FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2), fftBuffer);

                // Заполняем результат (только половина спектра полезна)
                for (int i = 0; i < fftSize / 2; i++)
                {
                    float magnitude = (float)Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y);
                    lastFftResults[i] = magnitude;
                }

                // Вызываем событие для отрисовки
                FftCalculated?.Invoke(this, lastFftResults);
            }
        }
    }
}