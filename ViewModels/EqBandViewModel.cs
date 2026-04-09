using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QAMP.ViewModels
{
    public class EqBandViewModel : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string? Frequency { get; set; }

        private float _gain;
        public float Gain
        {
            get => _gain;
            set
            {
                if (_gain != value)
                {
                    _gain = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}