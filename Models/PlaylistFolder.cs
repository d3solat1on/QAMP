using System.Collections.ObjectModel;
using System.ComponentModel;

namespace QAMP.Models;

public class PlaylistFolder : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _editingTitle = string.Empty;
    private bool _isExpanded = true;
    private bool _isEditing;
    private bool _isPinned;

    public int Id { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string EditingTitle
    {
        get => _editingTitle;
        set
        {
            if (_editingTitle == value) return;
            _editingTitle = value;
            OnPropertyChanged(nameof(EditingTitle));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged(nameof(IsEditing));
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value) return;
            _isPinned = value;
            OnPropertyChanged(nameof(IsPinned));
        }
    }

    public ObservableCollection<Playlist> PlaylistInFolder { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
