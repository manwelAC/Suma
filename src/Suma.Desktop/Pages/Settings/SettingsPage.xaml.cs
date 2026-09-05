using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.ViewModels;
using Suma.Desktop.Common;
using Windows.Storage.Pickers;

namespace Suma.Desktop.Pages.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel) { ViewModel = viewModel; InitializeComponent(); DataContext = ViewModel; Loaded += OnLoaded; SizeChanged += OnSizeChanged; }
    public SettingsViewModel ViewModel { get; }
    private async void OnLoaded(object sender, RoutedEventArgs e) { await ViewModel.InitializeAsync(); Refresh(); UpdateResponsiveLayout(ActualWidth); }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout(e.NewSize.Width);
    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0) availableWidth = ActualWidth;
        if (availableWidth <= 0) return;
        var isWide = availableWidth >= 900;
        if (isWide)
        {
            Grid.SetColumn(LocalDataPanel, 1);
            Grid.SetRow(LocalDataPanel, 0);
            SettingsRightColDef.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            Grid.SetColumn(LocalDataPanel, 0);
            Grid.SetRow(LocalDataPanel, 1);
            SettingsRightColDef.Width = new GridLength(0);
        }
    }
    private async void OnEnablePin(object sender, RoutedEventArgs e) { await ViewModel.EnablePinAsync(NewPinBox.Password, ConfirmPinBox.Password); ClearPins(); Refresh(); }
    private async void OnChangePin(object sender, RoutedEventArgs e) { await ViewModel.ChangePinAsync(CurrentPinBox.Password, ChangedPinBox.Password, ChangedConfirmBox.Password); ClearPins(); Refresh(); }
    private async void OnDisablePin(object sender, RoutedEventArgs e) { await ViewModel.DisablePinAsync(CurrentPinBox.Password); ClearPins(); Refresh(); }
    private async void OnBackup(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunBackupAsync(PickBackupDestinationAsync, DateTime.Now);
        Refresh();
        if (!string.IsNullOrEmpty(ViewModel.SuccessMessage))
        {
            var successDialog = SumaDialog.CreateSuccess(
                XamlRoot,
                "Backup created",
                "Your data was backed up successfully to your local device.",
                "Done");
            _ = await successDialog.ShowAsync();
        }
    }
    private async void OnRestore(object sender, RoutedEventArgs e) { await ViewModel.RunRestoreAsync(PickRestoreSourceAsync, ConfirmRestoreAsync); Refresh(); }
    private async void OnResetData(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunResetDataAsync(ConfirmResetAsync);
        Refresh();
        if (!string.IsNullOrEmpty(ViewModel.SuccessMessage))
        {
            var successDialog = SumaDialog.CreateSuccess(
                XamlRoot,
                "Data Reset Complete",
                "All local accounts, transactions, budgets, and goals have been deleted. You are now starting fresh.",
                "Done");
            _ = await successDialog.ShowAsync();
        }
    }
    private async Task<bool> ConfirmResetAsync()
    {
        var dialog = SumaDialog.CreateDestructive(
            XamlRoot,
            "Erase all financial data?",
            "This will permanently delete all accounts, transactions, budgets, recurring expenses, and savings goals from your device.",
            "This action cannot be undone. Consider creating a backup first if you want to keep any current records.",
            destructiveButtonText: "Erase Everything",
            cancelButtonText: "Cancel");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async Task<string?> PickBackupDestinationAsync(string suggestedName) { var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName) }; picker.FileTypeChoices.Add("Suma backup", [".suma-backup"]); InitializePicker(picker); return (await picker.PickSaveFileAsync())?.Path; }
    private async Task<string?> PickRestoreSourceAsync() { var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".suma-backup"); InitializePicker(picker); return (await picker.PickSingleFileAsync())?.Path; }
    private async Task<bool> ConfirmRestoreAsync()
    {
        var dialog = SumaDialog.CreateDestructive(
            XamlRoot,
            "Restore Suma backup?",
            "Restore replaces your current Suma financial data with the selected backup. Your local PIN setting will not change.",
            "Make sure you have backed up any current data you wish to keep before proceeding.",
            destructiveButtonText: "Prepare Restore",
            cancelButtonText: "Cancel");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private static void InitializePicker(object picker) { var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow ?? throw new InvalidOperationException("The Suma window is unavailable."); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window)); }
    private void ClearPins() { NewPinBox.Password = ConfirmPinBox.Password = CurrentPinBox.Password = ChangedPinBox.Password = ChangedConfirmBox.Password = string.Empty; }
    private void Refresh() { PinStatusText.Text = ViewModel.IsPinEnabled ? "Local PIN is enabled" : "Local PIN is disabled"; EnablePanel.Visibility = ViewModel.IsPinEnabled ? Visibility.Collapsed : Visibility.Visible; EnabledPanel.Visibility = ViewModel.IsPinEnabled ? Visibility.Visible : Visibility.Collapsed; RestartText.Visibility = ViewModel.IsRestartRequired ? Visibility.Visible : Visibility.Collapsed; }
}
