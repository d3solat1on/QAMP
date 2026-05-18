using System;
using System.Windows;
using System.Windows.Input;
using QAMP.Services;
using QAMP.Models;

namespace QAMP.Visualization
{
    public partial class SpectrumFullWindow : Window
    {
        public SpectrumFullWindow()
        {
            InitializeComponent();
        }

        private void SpectrumFullWindow_Loaded(object sender, RoutedEventArgs e)
        {
            int normalCount = SettingsManager.Instance.Config.VisualizerBarCount;
            FullSpectrumViewer.SetBarCount(normalCount * 2);
            PlayerService.Instance.AddSpectrumControl(FullSpectrumViewer);
        }

        private void SpectrumFullWindow_Closed(object? sender, EventArgs e)
        {
            PlayerService.Instance.RemoveSpectrumControl(FullSpectrumViewer);
        }

        private void SpectrumFullWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W))
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
