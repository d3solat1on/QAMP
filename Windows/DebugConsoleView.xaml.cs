using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QAMP.Services;

namespace QAMP.Windows;

public partial class DebugConsoleView : UserControl
{
    private readonly List<string> _allCommands =
    [
        "help", "status", "play", "pause", "stop", "next", "prev",
        "seek", "volume", "shuffle", "repeat", "queue", "search",
        "playlists", "playlist", "devices", "device", "readconfig",
        "changefontsize", "changefontsizedatagrid", "createplaylist",
        "clear", "exit", "quit", "mute", "unmute", "trackinfo", "clearcache",
        "changeformatlibrary","changeaccentcolor","lyricsmode", "changeround",
        "background", "getfonts", "setfont","getthemes", "settheme","setbg",
        "addbg", "addtheme","addfont"
    ];
    public DebugConsoleView()
    {
        InitializeComponent();

        AppConsole.LogAdded += OnLogAdded;
        AppConsole.OnClearRequested += ClearConsole;
        Unloaded += (s, e) =>
                    {
                        AppConsole.OnClearRequested -= ClearConsole;
                        AppConsole.LogAdded -= OnLogAdded;
                        SuggestionsPopup.IsOpen = false;
                        MemoryOptimizer.RunAsync(this.Dispatcher); //?
                    };
        Loaded += (s, e) =>
        {
            ConsoleInputBox.Focus();

            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.StateChanged += (sender, args) =>
                {
                    if (parentWindow.WindowState == WindowState.Minimized)
                    {
                        SuggestionsPopup.IsOpen = false;
                    }
                };

                parentWindow.Deactivated += (sender, args) =>
                {
                    SuggestionsPopup.IsOpen = false;
                };
            }
        };

        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ConsoleInputBox.Focus();
                    Keyboard.Focus(ConsoleInputBox);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
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
    private void ConsoleInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string input = ConsoleInputBox.Text.Trim();

        string commandPart = input.Split(' ')[0].ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(commandPart) || input.Contains(' '))
        {
            SuggestionsPopup.IsOpen = false;
            return;
        }

        var matches = _allCommands
            .Where(c => c.StartsWith(commandPart, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c)
            .ToList();

        if (matches.Count > 0 && !(matches.Count == 1 && matches[0].Equals(commandPart, StringComparison.OrdinalIgnoreCase)))
        {
            SuggestionsListBox.ItemsSource = matches;
            SuggestionsListBox.SelectedIndex = 0;
            SuggestionsPopup.IsOpen = true;
        }
        else
        {
            SuggestionsPopup.IsOpen = false;
        }
    }
    private void SuggestionsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            ApplySelectedSuggestion();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SuggestionsPopup.IsOpen = false;
            ConsoleInputBox.Focus();
            e.Handled = true;
        }
    }
    private void SuggestionsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ApplySelectedSuggestion();
    }
    private void ApplySelectedSuggestion()
    {
        if (SuggestionsListBox.SelectedItem is string selectedCmd)
        {
            ConsoleInputBox.Text = selectedCmd + " ";
            ConsoleInputBox.CaretIndex = ConsoleInputBox.Text.Length;
            SuggestionsPopup.IsOpen = false;
            ConsoleInputBox.Focus();
        }
    }
    private async void ConsoleInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SuggestionsPopup.IsOpen)
        {
            if (e.Key == Key.Down && SuggestionsListBox.Items.Count > 0)
            {
                if (SuggestionsListBox.SelectedIndex < SuggestionsListBox.Items.Count - 1)
                    SuggestionsListBox.SelectedIndex++;
                else
                    SuggestionsListBox.SelectedIndex = 0;

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up && SuggestionsListBox.Items.Count > 0)
            {
                if (SuggestionsListBox.SelectedIndex > 0)
                    SuggestionsListBox.SelectedIndex--;
                else
                    SuggestionsListBox.SelectedIndex = SuggestionsListBox.Items.Count - 1;

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab || e.Key == Key.Enter)
            {
                if (SuggestionsListBox.SelectedItem is string selectedCmd)
                {
                    ConsoleInputBox.Text = selectedCmd + " ";
                    ConsoleInputBox.CaretIndex = ConsoleInputBox.Text.Length;
                    SuggestionsPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }
            }
        }

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
                SuggestionsPopup.IsOpen = false;
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