using System.Windows;
using QAMP.Services;
using QAMP.Windows;

namespace QAMP
{
    public partial class MainWindow
    {

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings(_playService)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
#if DEBUG
            string mem = MemoryProfiler.GetMemoryReport();

            System.Diagnostics.Debug.WriteLine(mem);
#endif
        }
        private void EqualizerButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsAudioWindow = new SettingsAudio()
            {
                Owner = this
            };
            settingsAudioWindow.Show();
        }
    }
}
