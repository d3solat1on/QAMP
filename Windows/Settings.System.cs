using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using QAMP.Dialogs;
using QAMP.Models;
using QAMP.Services;

namespace QAMP.Windows
{
    public partial class Settings
    {
        private void CheckDiscordRpc(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.EnableDiscordRpc = EnabledDiscordRpc.IsChecked ?? false;
            SettingsManager.Instance.Save();
            PlayerService.Instance.RefreshDiscordPresence();
        }

        private void CheckUseConsole(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            var config = SettingsManager.Instance.Config;
            config.UseConsole = EnabledConsole.IsChecked ?? false;
            SettingsManager.Instance.Save();
        }

        private void CheckUseCustomBGUpDown(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio || radio.IsChecked != true) return;

            var config = SettingsManager.Instance.Config;
            config.UseCustomBackgroundUpDown = radio == UseCustomBGUpDownradio;
        }

        private void CheckUseCustomBG(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio || radio.IsChecked != true) return;

            var config = SettingsManager.Instance.Config;
            config.UseCustomBackground = radio == UseCustomBGradio;
        }

        private void CheckUseCoverCache(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio || radio.IsChecked != true) return;

            SettingsManager.Instance.Config.EnableCoverCache = radio == UseCoverCache;
        }

        private void CloseAction_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (sender is not RadioButton radio) return;

            SettingsManager.Instance.Config.CloseToTray = radio.Name == "CloseToTrayRadio";
        }

        private void CheckAutoLaunch(object? sender, RoutedEventArgs? e)
        {
            if (isInitializing) return;

            ApplyAutoLaunchSetting(AutoLaunchEnabled.IsChecked == true);
        }

        private void ApplyAutoLaunchSetting(bool isEnabled)
        {
            if (!OperatingSystem.IsWindows())
                return;

            var config = SettingsManager.Instance.Config;
            config.IsAutoLaunchEnabled = isEnabled;
            SettingsManager.Instance.Save();

            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string appName = "QAMP";
            string appPath = AppContext.BaseDirectory;

            if (isEnabled)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(keyName, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(keyName, writable: true)
                        ?? throw new InvalidOperationException("Could not access the current user's startup registry key.");
                    key.SetValue(appName, $"\"{appPath}QAMP.exe\"");
                }
                catch (Exception ex) when (IsRegistryAccessError(ex))
                {
                    Debug.WriteLine($"Failed to enable auto-launch: {ex}");
                    string message = (string)Application.Current.FindResource("LngFailedAutorun");
                    NotificationWindow.Show(message, this);
                    SetAutoLaunchRadioState(false);
                    config.IsAutoLaunchEnabled = false;
                    SettingsManager.Instance.Save();
                }
            }
            else
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
                    key?.DeleteValue(appName, false);
                }
                catch (Exception ex) when (IsRegistryAccessError(ex))
                {
                    Debug.WriteLine($"Failed to disable auto-launch: {ex}");
                    string message = (string)Application.Current.FindResource("LngFailedDisableAutorun");
                    NotificationWindow.Show(message, this);
                    SetAutoLaunchRadioState(true);
                    config.IsAutoLaunchEnabled = true;
                    SettingsManager.Instance.Save();
                }
            }
        }

        private static bool IsRegistryAccessError(Exception exception) =>
            exception is UnauthorizedAccessException
                or SecurityException
                or IOException
                or InvalidOperationException;

        private void SetAutoLaunchRadioState(bool isEnabled)
        {
            bool wasInitializing = isInitializing;
            isInitializing = true;
            AutoLaunchEnabled.IsChecked = isEnabled;
            AutoLaunchDisabled.IsChecked = !isEnabled;
            isInitializing = wasInitializing;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
