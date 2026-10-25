using System.Windows;
using QAMP.Services;
using QAMP.Windows;

namespace QAMP
{
    public partial class MainWindow
    {

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings()
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }
        private void EqualizerButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsAudioWindow = new SettingsAudio()
            {
                Owner = this
            };
            settingsAudioWindow.Show();
        }
        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            var statisticsWindow = new Statistics()
            {
                Owner = this
            };
            statisticsWindow.Show();
        }
    }
}
