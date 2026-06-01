namespace QAMP.Windows;

using System.Windows;
using System.Threading.Tasks;
using QAMP.Models;

public partial class ShowInfoPlaylist : Window
{
    private readonly Playlist _playlist = null!;

    public ShowInfoPlaylist()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    public ShowInfoPlaylist(Playlist playlist) : this()
    {
        _playlist = playlist;
        DataContext = playlist; 
        LoadDurationAsync();
    }

    private void LoadDurationAsync()
    {

        TotalDurationValueTextBlock?.Text = "...";

        Task.Run(() =>
        {
            string formattedDuration = _playlist.TotalDurationDisplay;

            Dispatcher.Invoke(() =>
            {
                TotalDurationValueTextBlock?.Text = formattedDuration;
            });
        });
    }

    public void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}