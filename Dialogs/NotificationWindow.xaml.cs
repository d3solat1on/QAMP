using System.Windows;
namespace QAMP.Dialogs
{
    public partial class NotificationWindow : Window
    {
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
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; 
            this.Close();
        }

        
        public static async void ShowToast(string message, Window owner)
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
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }
    }
}