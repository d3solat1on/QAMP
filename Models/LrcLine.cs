using System.ComponentModel;

namespace QAMP.Models;

public class LrcLine : INotifyPropertyChanged
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = string.Empty;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}