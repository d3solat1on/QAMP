using System.Windows;
using System.Windows.Input;
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
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };

            win.MessageText.Text = message;
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape && e.Key != Key.Enter)
                return;

            e.Handled = true;
            try
            {
                DialogResult = e.Key == Key.Enter;
                DialogResult = e.Key == Key.Escape;
            }
            catch (InvalidOperationException)
            {
                // I
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
                // I
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
                // I
            }
            Close();
        }
    }
}