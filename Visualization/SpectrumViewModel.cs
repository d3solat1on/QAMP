using System.Collections.ObjectModel;

namespace QAMP.Visualization
{
    public class SpectrumViewModel
    {
        public ObservableCollection<BarItem> Bars { get; }
        private int _barCount = 64;
        private readonly object _barsLock = new object(); // Для синхронизации многопоточного доступа

        public int BarCount
        {
            get => _barCount;
            set
            {
                if (_barCount == value) return;
                _barCount = value;
                ReinitializeBars();
            }
        }

        public SpectrumViewModel()
        {
            Bars = [];
            ReinitializeBars();
        }

        private void ReinitializeBars()
        {
            lock (_barsLock)
            {
                Bars.Clear();
                for (int i = 0; i < _barCount; i++)
                    Bars.Add(new BarItem { Value = 1 });
            }
        }

        public void Update(float[] fftData)
        {
            int halfLength = fftData.Length; // В SampleAggregator мы уже передаем половину (1024)

            lock (_barsLock)
            {
                for (int i = 0; i < _barCount && i < Bars.Count; i++)
                {
                    // Твоя логика распределения частот из Form1.cs
                    double percent = (double)i / _barCount;
                    double logPercent = Math.Pow(percent, 2);

                    int startIndex = (int)(logPercent * (halfLength - 10));
                    int endIndex = (int)(Math.Pow((double)(i + 1) / _barCount, 2) * (halfLength - 10));
                    endIndex = Math.Max(startIndex + 1, endIndex);

                    float maxInBand = 0;
                    for (int j = startIndex; j < endIndex && j < halfLength; j++)
                    {
                        if (fftData[j] > maxInBand) maxInBand = fftData[j];
                    }
                    double newValue = Math.Pow(maxInBand * 1000, 0.5) * 0.5;
                    newValue *= 75;
                    if (newValue > 100) newValue = 75;
                    if (newValue < 2) newValue = 2;

                    if (newValue > Bars[i].Value)
                        Bars[i].Value = newValue;
                    else
                        Bars[i].Value *= 0.5; // Быстрое падение для отзывчивого спектра
                }
            }
        }
    }
}