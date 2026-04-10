using System.Windows;
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


        public static async void ShowToast(string message, Window owner) //Когда-нибудь сделаю
        {
            var win = new NotificationWindow
            {
                Owner = owner
            };
            win.MessageText.Text = message;

            // Убираем кнопку ОК, если это авто-уведомление
            // (для этого кнопке в XAML тоже нужно дать x:Name, например x:Name="OkButton")
            // win.OkButton.Visibility = Visibility.Collapsed; 

            win.Show(); // Show вместо ShowDialog, чтобы не блокировать плеер

            await Task.Delay(3000);

            // Плавное исчезновение (опционально)
            for (double i = 1; i > 0; i -= 0.1)
            {
                win.Opacity = i;
                await Task.Delay(50);
            }

            win.Close();
        }
        // private void OkButton_Click(object sender, RoutedEventArgs e)
        // {
        //     DialogResult = true;
        //     Close();
        // }
    }
}