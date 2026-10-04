using System.Windows.Interop;
using QAMP.Models;
using QAMP.Services;
using QAMP.Windows;

namespace QAMP;

public partial class MainWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(new HwndSourceHook(HwndHook));
        HotKeyManager.RegisterMediaKeys(this);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case 9000:
                    TogglePlayPause();
                    break;
                case 9001:
                    _playService.PlayNextTrack();
                    break;
                case 9002:
                    _playService.PlayPreviousTrack();
                    break;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }
    private void ExecuteHotkeyAction(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.TogglePlayPause:
                TogglePlayPause();
                break;
            case HotkeyAction.SeekForward:
                _playService.SeekRelative(5);
                break;
            case HotkeyAction.SeekBackward:
                _playService.SeekRelative(-5);
                break;
            case HotkeyAction.VolumeUp:
                _playService.Volume += 0.05;
                break;
            case HotkeyAction.VolumeDown:
                _playService.Volume -= 0.05;
                break;
            case HotkeyAction.NextTrack:
                _playService.PlayNextTrack();
                break;
            case HotkeyAction.PreviousTrack:
                _playService.PlayPreviousTrack();
                break;
            case HotkeyAction.ViewLyrics:
                ViewLyricsButton_Click(null, null);
                break;
            case HotkeyAction.ShowTrackInfo:
                var fullInfo = TagReader.GetFullTrackInfo(Player.CurrentTrack.Path);
                if (fullInfo != null)
                {
                    fullInfo.Id = Player.CurrentTrack.Id;
                    fullInfo.PlayCount = Player.CurrentTrack.PlayCount;
                    var infoWindow = new ShowTrackInfo(fullInfo)
                    {
                        Owner = this
                    };
                    infoWindow.Show();
                }
                break;
            case HotkeyAction.ToggleRepeat:
                RepeatButton_Click(null, null);
                break;
            case HotkeyAction.ToggleShuffle:
                ShuffleButton_Click(null, null);
                break;
            case HotkeyAction.OpenFullScreenSpectrum:
                OpenSpectrumFullScreen();
                break;
            case HotkeyAction.ToggleFocusGrid:
                if (PlaylistsListBox.IsFocused || PlaylistsListBox.IsKeyboardFocusWithin)
                {
                    TracksDataGrid.Focus();
                    if (TracksDataGrid.SelectedItem == null && TracksDataGrid.Items.Count > 0)
                    {
                        TracksDataGrid.SelectedIndex = 0;
                    }
                }
                else
                {
                    PlaylistsListBox.Focus();
                    if (PlaylistsListBox.SelectedItem == null && PlaylistsListBox.Items.Count > 0)
                    {
                        PlaylistsListBox.SelectedIndex = 0;
                    }
                }
                break;
            case HotkeyAction.ToggleFavorite:
                FavoriteButton_Click(null, null);
                break;
        }
    }
}