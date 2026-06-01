using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace QAMP.Dialogs
{
    public partial class NotificationWindow : Window
    {
        private bool _isAnimating = false;
        public enum NotificationMode
        {
            Info,
            Confirm
        }
        public NotificationWindow()
        {
            InitializeComponent();
        }
        public static bool? Show(string message, Window owner, NotificationMode mode = NotificationMode.Info)
        {
            var win = new NotificationWindow
            {
                Owner = owner
            };
            win.MessageText.Text = message; // Убедись, что x:Name="MessageText" в XAML есть
            if (mode == NotificationMode.Confirm)
            {
                win.ConfirmButtons.Visibility = Visibility.Visible;
                win.SingleOkButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                win.ConfirmButtons.Visibility = Visibility.Collapsed;
                win.SingleOkButton.Visibility = Visibility.Visible;
            }
            return win.ShowDialog();
        }
        public async Task StartDotAnimation(string baseMessage)
        {
            _isAnimating = true;
            int dotCount = 0;
            while (_isAnimating)
            {
                dotCount = (dotCount + 1) % 4;
                // Генерируем строку вида: "Поиск в LRCLIB", "Поиск в LRCLIB.", "Поиск в LRCLIB.." и т.д.
                MessageText.Text = baseMessage + new string('.', dotCount);
                await Task.Delay(500); // Скорость мигания
            }
        }

        public void StopDotAnimation()
        {
            _isAnimating = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape && e.Key != Key.Enter)
                return;

            e.Handled = true;
            try
            {
                DialogResult = e.Key == Key.Enter;
            }
            catch (InvalidOperationException)
            {
                // Если окно не было открыто как диалог, просто игнорируем
            }
            Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                // Если окно не было открыто как диалог, просто игнорируем
            }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
                // Если окно не было открыто как диалог, просто игнорируем
            }
            Close();
        }


        public static async Task ShowToast(string message, Window owner)
        {
            if (owner == null || !owner.IsLoaded) return;

            // Создаем окно-"стекло" строго поверх Owner
            var win = new NotificationWindow
            {
                Owner = owner,
                // Полностью копируем геометрию главного окна
                Left = owner.Left,
                Top = owner.Top,
                Width = owner.Width,
                Height = owner.Height,
                Opacity = 0
            };

            // Заполняем текст
            win.MessageText.Text = message;

            // Скрываем все дефолтные кнопки
            win.SingleOkButton.Visibility = Visibility.Collapsed;
            win.ConfirmButtons.Visibility = Visibility.Collapsed;

            // На случай, если главное окно изменит размер или подвинется, пока висит тост
            owner.LocationChanged += (s, e) => { win.Left = owner.Left; win.Top = owner.Top; };
            owner.SizeChanged += (s, e) => { win.Width = owner.Width; win.Height = owner.Height; };

            win.Show();

            // --- НАСТРОЙКА АНИМАЦИИ ВНУТРИ ОКНА ---
            var duration = TimeSpan.FromMilliseconds(250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 1. Плавное появление самого окна
            var fadeIn = new DoubleAnimation(0, 1, duration);

            // 2. Выплывание карточки снизу вверх (смещаем по оси Y с 30px до 0)
            var slideIn = new DoubleAnimation(30, 0, duration) { EasingFunction = ease };

            win.BeginAnimation(OpacityProperty, fadeIn);
            win.ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            // Ждем 2 секунды (в Релизе этот await держит ссылку, спасая от GC!)
            await Task.Delay(2000);

            // --- АНИМАЦИЯ ИСЧЕЗНОВЕНИЯ ---
            var fadeOut = new DoubleAnimation(1, 0, duration);
            var slideOut = new DoubleAnimation(0, -15, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

            win.BeginAnimation(OpacityProperty, fadeOut);
            win.ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

            // Даем анимации завершиться и закрываем окно
            await Task.Delay(250);
            win.Close();
        }
        // private void OkButton_Click(object sender, RoutedEventArgs e)
        // {
        //     DialogResult = true;
        //     Close();
        // }
    }
}