using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QAMP.Models;
using QAMP.Services;

namespace QAMP.Windows
{
    public partial class Settings
    {
        private void AdaptiveGradients_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseAdaptiveGradients = AdaptiveGradientsRadio.IsChecked ?? false;
            SettingsManager.Instance.Save();
        }

        private void GradientToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.UseSpectrumGradient = GradientEnabled.IsChecked ?? false;
            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private void SpectrumType_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            switch (sender)
            {
                case RadioButton radio when radio == SpectrumBarsRadio:
                    config.SpectrumType = Visualization.SpectrumDisplayType.Bars;
                    break;
                case RadioButton radio when radio == SpectrumLineRadio:
                    config.SpectrumType = Visualization.SpectrumDisplayType.Line;
                    break;
                case RadioButton radio when radio == SpectrumCenteredBarsRadio:
                    config.SpectrumType = Visualization.SpectrumDisplayType.CenteredBars;
                    break;
            }

            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private void SpectrumColumnsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpectrumColumnsValueText != null)
                SpectrumColumnsValueText.Text = ((int)e.NewValue).ToString();

            if (isInitializing)
                return;

            int count = (int)e.NewValue;
            var config = SettingsManager.Instance.Config;
            if (config.VisualizerBarCount == count)
                return;

            config.VisualizerBarCount = count;
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private void SpectrumGradientTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.SpectrumGradientStartColor = SpectrumGradientStartColorTextBox.Text.Trim();
            config.SpectrumGradientEndColor = SpectrumGradientEndColorTextBox.Text.Trim();

            bool isValidStart = IsHexColorValid(config.SpectrumGradientStartColor);
            bool isValidEnd = IsHexColorValid(config.SpectrumGradientEndColor);

            if (!isValidStart || !isValidEnd)
                return;

            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshSpectrumControls();
        }

        private static bool IsHexColorValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim();
            if (!normalized.StartsWith('#'))
                normalized = $"#{normalized}";

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(normalized);
                return color.A >= 0;
            }
            catch
            {
                return false;
            }
        }

        private void VisualizerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var config = SettingsManager.Instance.Config;
            config.IsVisualizerEnabled = VisualizerEnabled.IsChecked ?? false;
            PlayerService.Instance.RefreshSpectrumControls();
            SettingsManager.Instance.Save();
        }
    }
}
