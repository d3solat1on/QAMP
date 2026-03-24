using System.Windows;
using System.Windows.Media.Animation;

namespace QAMP.Windows;

public partial class OSDWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public OSDWindow()
    {
        InitializeComponent();
        Left = 5;
        Top = 5;

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
}