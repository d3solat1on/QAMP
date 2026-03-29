#pragma warning disable SYSLIB1054
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace QAMP.Services;
public static class HotKeyManager
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint VK_MEDIA_PLAY_PAUSE = 0xB3;
    public const uint VK_MEDIA_NEXT = 0xB0;
    public const uint VK_MEDIA_PREV = 0xB1;

    public static void RegisterMediaKeys(Window window)
    {
        var helper = new WindowInteropHelper(window);
        RegisterHotKey(helper.Handle, 9000, 0, VK_MEDIA_PLAY_PAUSE);
        RegisterHotKey(helper.Handle, 9001, 0, VK_MEDIA_NEXT);
        RegisterHotKey(helper.Handle, 9002, 0, VK_MEDIA_PREV);
    }
}