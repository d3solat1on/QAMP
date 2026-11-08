using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QAMP.Dialogs;

public partial class ModernNotice : UserControl
{
    private bool _isAnimating = false;
    private readonly TimeSpan _animationDuration = TimeSpan.FromMilliseconds(250);
    private readonly IEasingFunction _easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private readonly IEasingFunction _easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
    public ModernNotice()
    {
        InitializeComponent();
    }
    public async Task ShowAsync(string message)
    {
        Window parentWindow = Window.GetWindow(this);
        if (parentWindow == null)
        {
            return;
        }


        ToastMessageText.Text = message;
        this.Visibility = Visibility.Visible;

        this.UpdateLayout();

        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fadeIn = new DoubleAnimation(0, 1, duration);
        var slideIn = new DoubleAnimation(30, 0, duration) { EasingFunction = ease };


        this.BeginAnimation(OpacityProperty, fadeIn);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        await Task.Delay(2000);

        var fadeOut = new DoubleAnimation(1, 0, duration);
        var slideOut = new DoubleAnimation(0, -15, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        this.BeginAnimation(OpacityProperty, fadeOut);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

        await Task.Delay(250);
        this.Visibility = Visibility.Collapsed;
    }
    public void StartLoading(string baseMessage)
    {
        if (_isAnimating) return;

        _isAnimating = true;
        ToastMessageText.Text = baseMessage;
        this.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, _animationDuration);
        var slideIn = new DoubleAnimation(30, 0, _animationDuration) { EasingFunction = _easeOut };
        this.BeginAnimation(OpacityProperty, fadeIn);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        Task.Run(async () =>
        {
            int dotCount = 0;
            while (_isAnimating)
            {
                dotCount = (dotCount + 1) % 4;
                string currentText = baseMessage + new string('.', dotCount);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_isAnimating) 
                        ToastMessageText.Text = currentText;
                });

                await Task.Delay(500);
            }
        });
    }
    public async Task StopLoadingAsync()
    {
        _isAnimating = false;

        var fadeOut = new DoubleAnimation(1, 0, _animationDuration);
        var slideOut = new DoubleAnimation(0, -15, _animationDuration) { EasingFunction = _easeIn };

        this.BeginAnimation(OpacityProperty, fadeOut);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

        await Task.Delay(250);
        this.Visibility = Visibility.Collapsed;
    }
}