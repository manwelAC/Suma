using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Suma.Desktop.ViewModels;
using Windows.System;

namespace Suma.Desktop.Pages.Lock;

public sealed partial class LockPage : Page
{
    private readonly Action unlocked;
    public LockPage(LockViewModel viewModel, Action unlocked) { ViewModel = viewModel; this.unlocked = unlocked; InitializeComponent(); DataContext = ViewModel; Loaded += (_, _) => PinBox.Focus(FocusState.Programmatic); }
    public LockViewModel ViewModel { get; }
    private async void OnUnlock(object sender, RoutedEventArgs e) { if (await ViewModel.UnlockAsync(PinBox.Password)) unlocked(); }
    private void OnPinKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Enter) { e.Handled = true; OnUnlock(sender, new RoutedEventArgs()); } }
}
