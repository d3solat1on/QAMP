namespace QAMP.Windows;

using System.Windows;
using QAMP.Models;
public partial class ShowInfoPlaylist : Window
{
    public ShowInfoPlaylist()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    public ShowInfoPlaylist(Playlist playlist)
        : this()
    {
        DataContext = playlist;
    }

    public void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    public void SetPlaylistInfo(Playlist playlist)
    {
        DataContext = playlist;
    }
}