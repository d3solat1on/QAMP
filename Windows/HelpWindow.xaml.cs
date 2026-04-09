using QAMP;
using System;
using System.Diagnostics;
using System.Windows;
namespace QAMP.Windows;

partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        this.MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove(); 
        };
    }
    public void ShowHelpWindow()
    {
        ShowDialog();
    }
    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/d3solat1on/QAMP") { UseShellExecute = true });
    }
    public void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}