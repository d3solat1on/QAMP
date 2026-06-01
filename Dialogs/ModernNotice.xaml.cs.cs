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
        System.Diagnostics.Debug.WriteLine($"[TOAST] Метод ShowAsync вызван с сообщением: \"{message}\"");

        // Проверяем, примонтирован ли элемент к окну
        Window parentWindow = Window.GetWindow(this);
        if (parentWindow == null)
        {
            System.Diagnostics.Debug.WriteLine("[TOAST] КРИТИЧЕСКАЯ ОШИБКА: UserControl не находится внутри какого-либо Окна (Parent Window равен null)!");
            return;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[TOAST] Родительское окно найдено: {parentWindow.Title}. Видимость окна: {parentWindow.Visibility}");
        }

        // Выводим размеры до анимации
        System.Diagnostics.Debug.WriteLine($"[TOAST] Размеры до показа: ActualWidth = {this.ActualWidth}, ActualHeight = {this.ActualHeight}, Visibility = {this.Visibility}");

        ToastMessageText.Text = message;
        this.Visibility = Visibility.Visible;

        // Выводим размеры ПОСЛЕ изменения видимости
        // Принудительно заставим WPF пересчитать разметку для теста
        this.UpdateLayout();
        System.Diagnostics.Debug.WriteLine($"[TOAST] Размеры после UpdateLayout: ActualWidth = {this.ActualWidth}, ActualHeight = {this.ActualHeight}");

        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fadeIn = new DoubleAnimation(0, 1, duration);
        var slideIn = new DoubleAnimation(30, 0, duration) { EasingFunction = ease };

        // Подписываемся на завершение анимации появления для проверки Opacity
        fadeIn.Completed += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[TOAST] Анимация появления завершена. Текущий Opacity элемента = {this.Opacity}");
        };

        this.BeginAnimation(OpacityProperty, fadeIn);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        await Task.Delay(2000);

        var fadeOut = new DoubleAnimation(1, 0, duration);
        var slideOut = new DoubleAnimation(0, -15, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        this.BeginAnimation(OpacityProperty, fadeOut);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

        await Task.Delay(250);
        this.Visibility = Visibility.Collapsed;
        System.Diagnostics.Debug.WriteLine("[TOAST] Цикл показа уведомления успешно завершен.");
    }
    public void StartLoading(string baseMessage)
    {
        if (_isAnimating) return; // Если уже ищет, игнорируем повторный вызов

        _isAnimating = true;
        ToastMessageText.Text = baseMessage;
        this.Visibility = Visibility.Visible;

        // Плавно показываем плашку
        var fadeIn = new DoubleAnimation(0, 1, _animationDuration);
        var slideIn = new DoubleAnimation(30, 0, _animationDuration) { EasingFunction = _easeOut };
        this.BeginAnimation(OpacityProperty, fadeIn);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

        // Запускаем асинхронный цикл анимации точек (без блокировки UI)
        Task.Run(async () =>
        {
            int dotCount = 0;
            while (_isAnimating)
            {
                dotCount = (dotCount + 1) % 4;
                string currentText = baseMessage + new string('.', dotCount);

                // Так как мы в другом потоке (Task.Run), меняем текст через Dispatcher
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_isAnimating) // Дополнительная проверка, чтобы не затерся текст успеха
                        ToastMessageText.Text = currentText;
                });

                await Task.Delay(500);
            }
        });
    }
    public async Task StopLoadingAsync()
    {
        _isAnimating = false;

        // Плавно скрываем плашку
        var fadeOut = new DoubleAnimation(1, 0, _animationDuration);
        var slideOut = new DoubleAnimation(0, -15, _animationDuration) { EasingFunction = _easeIn };

        this.BeginAnimation(OpacityProperty, fadeOut);
        ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

        await Task.Delay(250);
        this.Visibility = Visibility.Collapsed;
    }
}