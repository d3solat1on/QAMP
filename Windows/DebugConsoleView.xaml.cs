using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Services;

namespace QAMP.Windows;

public partial class DebugConsoleView : UserControl
{
    public DebugConsoleView()
    {
        InitializeComponent();

        AppConsole.LogAdded += OnLogAdded;
        AppConsole.OnClearRequested += ClearConsole;
        Unloaded += (s, e) =>
                    {
                        AppConsole.OnClearRequested -= ClearConsole;
                        AppConsole.LogAdded -= OnLogAdded;
                        MemoryOptimizer.RunAsync(this.Dispatcher); //?
                    };
    }

    private void OnLogAdded(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ConsoleOutputBox.AppendText(message + "\n");
            ConsoleOutputBox.ScrollToEnd();
        });
    }
    private async void ConsoleInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            string command = ConsoleInputBox.Text;

            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            ConsoleInputBox.Clear();
            try
            {
                await AppConsole.InputCommand(command);
            }
            catch (Exception ex)
            {
                AppConsole.Log($"Command failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Command failed: {ex.Message}");
                App.LogException(ex, "ConsoleInput");
            }
        }
    }
    public void ClearConsole()
    {
        ConsoleOutputBox.Clear();
    }
}