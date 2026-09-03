using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Suma.Desktop.ViewModels;
using Windows.System;

namespace Suma.Desktop.Pages.Lock;

public sealed partial class LockPage : Page
{
    private readonly Action unlocked;

    public LockPage(LockViewModel viewModel, Action unlocked)
    {
        ViewModel = viewModel;
        this.unlocked = unlocked;
        InitializeComponent();
        DataContext = ViewModel;

        Loaded += (_, _) =>
        {
            PinBox.Focus(FocusState.Programmatic);
            UpdatePinBoxes();
            UpdateResponsiveLayout(ActualWidth);
        };

        SizeChanged += (_, e) => UpdateResponsiveLayout(e.NewSize.Width);
    }

    public LockViewModel ViewModel { get; }

    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0) availableWidth = ActualWidth;
        if (availableWidth <= 0) return;

        if (availableWidth < 860)
        {
            MascotColDef.Width = new GridLength(0);
            MascotImage.Visibility = Visibility.Collapsed;
        }
        else
        {
            MascotColDef.Width = GridLength.Auto;
            MascotImage.Visibility = Visibility.Visible;
        }
    }

    private async void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.UnlockAsync(PinBox.Password))
        {
            // Switch main mascot to approved Sumo with thumbs up
            MascotImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/Mascot/approved.png"));

            // Switch card to celebration and success state
            PinEntryPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;

            // Wait a brief moment so user enjoys the success confirmation
            await Task.Delay(1100);

            unlocked();
        }
    }

    private void OnPinKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            OnUnlock(sender, new RoutedEventArgs());
        }
    }

    private void OnPinPasswordChanged(object sender, RoutedEventArgs e) => UpdatePinBoxes();

    private void OnPinGotFocus(object sender, RoutedEventArgs e) => UpdatePinBoxes();

    private void OnPinLostFocus(object sender, RoutedEventArgs e) => UpdatePinBoxes();

    private void OnPinBoxesTapped(object sender, TappedRoutedEventArgs e)
    {
        PinBox.Focus(FocusState.Programmatic);
    }

    private void OnClearPinClick(object sender, RoutedEventArgs e)
    {
        PinBox.Password = string.Empty;
        UpdatePinBoxes();
        PinBox.Focus(FocusState.Programmatic);
    }

    private void UpdatePinBoxes()
    {
        var length = PinBox.Password.Length;
        Dot0.Visibility = length >= 1 ? Visibility.Visible : Visibility.Collapsed;
        Dot1.Visibility = length >= 2 ? Visibility.Visible : Visibility.Collapsed;
        Dot2.Visibility = length >= 3 ? Visibility.Visible : Visibility.Collapsed;
        Dot3.Visibility = length >= 4 ? Visibility.Visible : Visibility.Collapsed;
        Dot4.Visibility = length >= 5 ? Visibility.Visible : Visibility.Collapsed;
        Dot5.Visibility = length >= 6 ? Visibility.Visible : Visibility.Collapsed;

        var isFocused = PinBox.FocusState != FocusState.Unfocused;
        var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x6F, 0x80, 0x6D));
        var defaultBorder = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE4, 0xE2, 0xDC));
        var filledBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xF2, 0xF6, 0xF0));
        var emptyBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFA, 0xFA, 0xFA));

        Border[] boxes = [Box0, Box1, Box2, Box3, Box4, Box5];
        for (int i = 0; i < boxes.Length; i++)
        {
            boxes[i].BorderBrush = (isFocused && (i == length || (i == 5 && length >= 6))) ? activeBrush : defaultBorder;
            boxes[i].Background = (i < length) ? filledBackground : emptyBackground;
        }
    }
}
