using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.ViewModels;
using Windows.Storage.Pickers;

namespace Suma.Desktop.Pages.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel) { ViewModel = viewModel; InitializeComponent(); DataContext = ViewModel; Loaded += OnLoaded; }
    public SettingsViewModel ViewModel { get; }
    private async void OnLoaded(object sender, RoutedEventArgs e) { await ViewModel.InitializeAsync(); Refresh(); }
    private async void OnEnablePin(object sender, RoutedEventArgs e) { await ViewModel.EnablePinAsync(NewPinBox.Password, ConfirmPinBox.Password); ClearPins(); Refresh(); }
    private async void OnChangePin(object sender, RoutedEventArgs e) { await ViewModel.ChangePinAsync(CurrentPinBox.Password, ChangedPinBox.Password, ChangedConfirmBox.Password); ClearPins(); Refresh(); }
    private async void OnDisablePin(object sender, RoutedEventArgs e) { await ViewModel.DisablePinAsync(CurrentPinBox.Password); ClearPins(); Refresh(); }
    private async void OnBackup(object sender, RoutedEventArgs e) { await ViewModel.RunBackupAsync(PickBackupDestinationAsync, DateTime.Now); Refresh(); }
    private async void OnRestore(object sender, RoutedEventArgs e) { await ViewModel.RunRestoreAsync(PickRestoreSourceAsync, ConfirmRestoreAsync); Refresh(); }
    private async Task<string?> PickBackupDestinationAsync(string suggestedName) { var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName) }; picker.FileTypeChoices.Add("Suma backup", [".suma-backup"]); InitializePicker(picker); return (await picker.PickSaveFileAsync())?.Path; }
    private async Task<string?> PickRestoreSourceAsync() { var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".suma-backup"); InitializePicker(picker); return (await picker.PickSingleFileAsync())?.Path; }
    private async Task<bool> ConfirmRestoreAsync() { var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Restore Suma backup?", Content = "Restore replaces your current Suma financial data with the selected backup. Your local PIN setting will not change.", PrimaryButtonText = "Prepare Restore", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close }; return await dialog.ShowAsync() == ContentDialogResult.Primary; }
    private static void InitializePicker(object picker) { var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow ?? throw new InvalidOperationException("The Suma window is unavailable."); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window)); }
    private void ClearPins() { NewPinBox.Password = ConfirmPinBox.Password = CurrentPinBox.Password = ChangedPinBox.Password = ChangedConfirmBox.Password = string.Empty; }
    private void Refresh() { PinStatusText.Text = ViewModel.IsPinEnabled ? "Local PIN is enabled" : "Local PIN is disabled"; EnablePanel.Visibility = ViewModel.IsPinEnabled ? Visibility.Collapsed : Visibility.Visible; EnabledPanel.Visibility = ViewModel.IsPinEnabled ? Visibility.Visible : Visibility.Collapsed; RestartText.Visibility = ViewModel.IsRestartRequired ? Visibility.Visible : Visibility.Collapsed; }
}
