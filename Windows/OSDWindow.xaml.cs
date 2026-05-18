using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace QAMP.Windows;

public partial class OSDWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public OSDWindow()
    {
        InitializeComponent();
        Left = 5;
        Top = 5;

        ShowActivated = false;

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (s, e) => HideOSD();
    }

    public void ShowOSD(string executor, string name) //lol, оказывается Executor и Artist не одно и то же xD
    {
        ExecutorText.Text = executor;
        NameText.Text = name;

        Opacity = 0;
        Show();

        DoubleAnimation fadeIn = new(1, TimeSpan.FromMilliseconds(300));
        BeginAnimation(OpacityProperty, fadeIn);

        _timer.Stop();
        _timer.Start();
    }

    private void HideOSD()
    {
        DoubleAnimation fadeOut = new(0, TimeSpan.FromMilliseconds(500));
        fadeOut.Completed += (s, e) => Hide();
        BeginAnimation(OpacityProperty, fadeOut);
        _timer.Stop();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        var val = GetWindowLong(helper.Handle, GWL_EXSTYLE);

        _ = SetWindowLong(helper.Handle, GWL_EXSTYLE, val | 0x08000000);
    }
}