using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Suma.Domain.Accounts;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Suma.Desktop.Common;

public enum ModalSize
{
    Compact, // 360px
    Medium,  // 520px
    Large    // 720px
}

public static class SumaDialog
{
    public static double GetWidth(ModalSize size) => size switch
    {
        ModalSize.Compact => 360,
        ModalSize.Medium => 520,
        ModalSize.Large => 720,
        _ => 520
    };

    private static T? GetResource<T>(string key) where T : class
    {
        if (XamlApp.Current.Resources.TryGetValue(key, out var val) && val is T typed)
        {
            return typed;
        }
        return null;
    }

    public static ContentDialog Create(
        XamlRoot xamlRoot,
        string title,
        UIElement body,
        string primaryText = "Save",
        string closeText = "Cancel",
        ModalSize size = ModalSize.Medium,
        string? subtitle = null,
        bool isDestructive = false)
    {
        var targetWidth = GetWidth(size);
        var contentWidth = targetWidth - 48;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Light,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = isDestructive ? ContentDialogButton.Close : ContentDialogButton.Primary
        };
        PrepareBody(body);

        dialog.CornerRadius = new CornerRadius(16);
        dialog.Resources["ContentDialogButtonMinWidth"] = 130.0;

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            dialog.Background = surface;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            dialog.BorderBrush = border;
            dialog.BorderThickness = new Thickness(1);
        }

        if (isDestructive && GetResource<Style>("SumaModalDestructiveButtonStyle") is { } destructiveStyle)
        {
            dialog.PrimaryButtonStyle = destructiveStyle;
        }
        else if (GetResource<Style>("SumaModalPrimaryButtonStyle") is { } primaryStyle)
        {
            dialog.PrimaryButtonStyle = primaryStyle;
        }

        if (GetResource<Style>("SumaModalSecondaryButtonStyle") is { } closeStyle)
        {
            dialog.CloseButtonStyle = closeStyle;
        }

        var rootGrid = new Grid
        {
            Width = contentWidth,
            MaxWidth = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var headerGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 16)
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 4 };
        var titleBlock = new TextBlock { Text = title };
        if (GetResource<Style>("SumaModalTitleTextStyle") is { } titleStyle)
        {
            titleBlock.Style = titleStyle;
        }
        titleStack.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subtitleBlock = new TextBlock { Text = subtitle };
            if (GetResource<Style>("SumaModalSubtitleTextStyle") is { } subStyle)
            {
                subtitleBlock.Style = subStyle;
            }
            titleStack.Children.Add(subtitleBlock);
        }

        headerGrid.Children.Add(titleStack);

        var closeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }
        };
        if (GetResource<Style>("SumaModalCloseButtonStyle") is { } closeBtnStyle)
        {
            closeButton.Style = closeBtnStyle;
        }
        closeButton.Click += (_, _) => dialog.Hide();
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);

        rootGrid.Children.Add(headerGrid);

        // Body container
        var bodyContainer = new ScrollViewer
        {
            Content = body,
            MaxHeight = 520,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(bodyContainer, 1);
        rootGrid.Children.Add(bodyContainer);

        dialog.Content = rootGrid;
        return dialog;
    }

    public static ContentDialog CreateDestructive(
        XamlRoot xamlRoot,
        string title,
        string message,
        string consequenceText,
        string destructiveButtonText = "Archive",
        string cancelButtonText = "Cancel",
        ModalSize size = ModalSize.Medium)
    {
        var targetWidth = GetWidth(size);
        var contentWidth = targetWidth - 48;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Light,
            PrimaryButtonText = destructiveButtonText,
            CloseButtonText = cancelButtonText,
            DefaultButton = ContentDialogButton.Close
        };

        dialog.CornerRadius = new CornerRadius(16);

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            dialog.Background = surface;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            dialog.BorderBrush = border;
            dialog.BorderThickness = new Thickness(1);
        }

        if (GetResource<Style>("SumaModalDestructiveButtonStyle") is { } destructiveStyle)
        {
            dialog.PrimaryButtonStyle = destructiveStyle;
        }

        if (GetResource<Style>("SumaModalSecondaryButtonStyle") is { } closeStyle)
        {
            dialog.CloseButtonStyle = closeStyle;
        }

        var rootGrid = new Grid
        {
            Width = contentWidth,
            MaxWidth = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header with warning icon
        var headerGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 16)
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconCircle = new Border
        {
            Margin = new Thickness(0, 0, 12, 0)
        };
        if (GetResource<Style>("SumaModalWarningIconCircleStyle") is { } iconCircleStyle)
        {
            iconCircle.Style = iconCircleStyle;
        }
        var warningIcon = new FontIcon { Glyph = "\uE7BA", FontSize = 16 };
        if (GetResource<Brush>("SumaWarningBrush") is { } warnBrush)
        {
            warningIcon.Foreground = warnBrush;
        }
        iconCircle.Child = warningIcon;
        headerGrid.Children.Add(iconCircle);

        var titleBlock = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (GetResource<Style>("SumaModalTitleTextStyle") is { } titleStyle)
        {
            titleBlock.Style = titleStyle;
        }
        Grid.SetColumn(titleBlock, 1);
        headerGrid.Children.Add(titleBlock);

        var closeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }
        };
        if (GetResource<Style>("SumaModalCloseButtonStyle") is { } closeBtnStyle)
        {
            closeButton.Style = closeBtnStyle;
        }
        closeButton.Click += (_, _) => dialog.Hide();
        Grid.SetColumn(closeButton, 2);
        headerGrid.Children.Add(closeButton);

        rootGrid.Children.Add(headerGrid);

        // Body with message and consequence box
        var bodyStack = new StackPanel { Spacing = 16 };

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };
        if (GetResource<Style>("SumaModalSubtitleTextStyle") is { } subStyle)
        {
            messageBlock.Style = subStyle;
        }
        bodyStack.Children.Add(messageBlock);

        if (!string.IsNullOrEmpty(consequenceText))
        {
            var consequenceBox = new Border();
            if (GetResource<Style>("SumaModalConsequenceBoxStyle") is { } boxStyle)
            {
                consequenceBox.Style = boxStyle;
            }

            var consequenceGrid = new Grid { ColumnSpacing = 10 };
            consequenceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            consequenceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var infoIcon = new FontIcon { Glyph = "\uE946", FontSize = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            if (GetResource<Brush>("SumaWarningBrush") is { } wb2)
            {
                infoIcon.Foreground = wb2;
            }
            consequenceGrid.Children.Add(infoIcon);

            var consequenceTextBlock = new TextBlock
            {
                Text = consequenceText,
                TextWrapping = TextWrapping.Wrap
            };
            if (GetResource<Style>("SumaModalHelperTextStyle") is { } helpStyle)
            {
                consequenceTextBlock.Style = helpStyle;
            }
            Grid.SetColumn(consequenceTextBlock, 1);
            consequenceGrid.Children.Add(consequenceTextBlock);

            consequenceBox.Child = consequenceGrid;
            bodyStack.Children.Add(consequenceBox);
        }

        Grid.SetRow(bodyStack, 1);
        rootGrid.Children.Add(bodyStack);

        dialog.Content = rootGrid;
        return dialog;
    }

    public static ContentDialog CreateSuccess(
        XamlRoot xamlRoot,
        string title,
        string message,
        string actionButtonText = "Done")
    {
        var targetWidth = GetWidth(ModalSize.Compact);
        var contentWidth = targetWidth - 48;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Light,
            CloseButtonText = actionButtonText,
            DefaultButton = ContentDialogButton.Close
        };

        dialog.CornerRadius = new CornerRadius(16);

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            dialog.Background = surface;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            dialog.BorderBrush = border;
            dialog.BorderThickness = new Thickness(1);
        }

        if (GetResource<Style>("SumaModalPrimaryButtonStyle") is { } primaryStyle)
        {
            var fullWidthStyle = new Style(typeof(Button)) { BasedOn = primaryStyle };
            fullWidthStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, contentWidth));
            fullWidthStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            dialog.CloseButtonStyle = fullWidthStyle;
        }

        var rootGrid = new Grid
        {
            Width = contentWidth,
            MaxWidth = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header with close button
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var closeButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }
        };
        if (GetResource<Style>("SumaModalCloseButtonStyle") is { } closeBtnStyle)
        {
            closeButton.Style = closeBtnStyle;
        }
        closeButton.Click += (_, _) => dialog.Hide();
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);

        rootGrid.Children.Add(headerGrid);

        // Centered body
        var bodyStack = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 16)
        };

        var successIconCircle = new Border();
        if (GetResource<Style>("SumaModalSuccessIconCircleStyle") is { } iconCircleStyle)
        {
            successIconCircle.Style = iconCircleStyle;
        }
        var checkIcon = new FontIcon { Glyph = "\uE73E", FontSize = 22 };
        if (GetResource<Brush>("SumaSuccessBrush") is { } successBrush)
        {
            checkIcon.Foreground = successBrush;
        }
        successIconCircle.Child = checkIcon;
        bodyStack.Children.Add(successIconCircle);

        var titleBlock = new TextBlock
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        if (GetResource<Style>("SumaModalTitleTextStyle") is { } titleStyle)
        {
            titleBlock.Style = titleStyle;
        }
        bodyStack.Children.Add(titleBlock);

        var messageBlock = new TextBlock
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        if (GetResource<Style>("SumaModalSubtitleTextStyle") is { } subStyle)
        {
            messageBlock.Style = subStyle;
        }
        bodyStack.Children.Add(messageBlock);

        Grid.SetRow(bodyStack, 1);
        rootGrid.Children.Add(bodyStack);

        dialog.Content = rootGrid;
        return dialog;
    }

    public static TextBox CreateTextBox(
        string placeholder,
        string text = "",
        bool acceptsReturn = false,
        InputScopeNameValue? inputScope = null,
        int? maxLength = null,
        CharacterCasing casing = CharacterCasing.Normal)
    {
        var box = new TextBox
        {
            PlaceholderText = placeholder,
            Text = text,
            AcceptsReturn = acceptsReturn,
            TextWrapping = acceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = acceptsReturn ? 96 : 48,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            FontSize = 14,
            RequestedTheme = ElementTheme.Light,
            CharacterCasing = casing
        };

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            box.Background = surface;
        }

        if (GetResource<Brush>("SumaTextPrimaryBrush") is { } primary)
        {
            box.Foreground = primary;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            box.BorderBrush = border;
        }

        if (inputScope.HasValue)
        {
            box.InputScope = new InputScope { Names = { new InputScopeName(inputScope.Value) } };
        }

        if (maxLength.HasValue)
        {
            box.MaxLength = maxLength.Value;
        }

        return box;
    }

    public static ComboBox CreateComboBox(object items, string? displayMemberPath = null)
    {
        var combo = new ComboBox
        {
            ItemsSource = items,
            MinHeight = 48,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 0, 14, 0),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RequestedTheme = ElementTheme.Light
        };

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            combo.Background = surface;
        }

        if (GetResource<Brush>("SumaTextPrimaryBrush") is { } primary)
        {
            combo.Foreground = primary;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            combo.BorderBrush = border;
        }

        if (!string.IsNullOrEmpty(displayMemberPath))
        {
            combo.DisplayMemberPath = displayMemberPath;
        }

        return combo;
    }

    public static DatePicker CreateDatePicker(DateTimeOffset? date = null)
    {
        var picker = new DatePicker
        {
            Date = date ?? DateTimeOffset.Now,
            MinHeight = 48,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RequestedTheme = ElementTheme.Light
        };

        if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
        {
            picker.Background = surface;
        }

        if (GetResource<Brush>("SumaTextPrimaryBrush") is { } primary)
        {
            picker.Foreground = primary;
        }

        if (GetResource<Brush>("SumaBorderBrush") is { } border)
        {
            picker.BorderBrush = border;
        }

        return picker;
    }

    public static ToggleSwitch CreateToggleSwitch(bool isOn)
    {
        return new ToggleSwitch
        {
            IsOn = isOn,
            OffContent = null,
            OnContent = null,
            HorizontalAlignment = HorizontalAlignment.Right,
            RequestedTheme = ElementTheme.Light
        };
    }

    public static StackPanel CreateField(string label, UIElement control, string? helperText = null)
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };

        if (GetResource<Brush>("SumaTextPrimaryBrush") is { } primary)
        {
            labelBlock.Foreground = primary;
        }
        else
        {
            labelBlock.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 28, 30));
        }

        panel.Children.Add(labelBlock);
        panel.Children.Add(control);

        if (!string.IsNullOrEmpty(helperText))
        {
            var helperBlock = new TextBlock
            {
                Text = helperText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };

            if (GetResource<Brush>("SumaTextSecondaryBrush") is { } secondary)
            {
                helperBlock.Foreground = secondary;
            }
            else
            {
                helperBlock.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 110, 115));
            }

            panel.Children.Add(helperBlock);
        }

        return panel;
    }

    public static UIElement CreateAccountTypeSelector(AccountType currentType, Action<AccountType> onSelectionChanged)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var types = new[]
        {
            AccountType.Cash,
            AccountType.Bank,
            AccountType.EWallet,
            AccountType.Savings,
            AccountType.Other
        };

        var buttons = new List<(AccountType Type, ToggleButton Button, FontIcon Icon, TextBlock Text)>();

        foreach (var type in types)
        {
            var label = type switch
            {
                AccountType.Cash => "Cash",
                AccountType.Bank => "Bank",
                AccountType.EWallet => "E-Wallet",
                AccountType.Savings => "Savings",
                _ => "Other"
            };

            var iconGlyph = type switch
            {
                AccountType.Cash => "\uE8C7",
                AccountType.Bank => "\uE80F",
                AccountType.EWallet => "\uE8C7",
                AccountType.Savings => "\uE9D9",
                _ => "\uE71D"
            };

            var btnContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            var icon = new FontIcon { Glyph = iconGlyph, FontSize = 12 };
            var text = new TextBlock { Text = label, FontSize = 13 };
            btnContent.Children.Add(icon);
            btnContent.Children.Add(text);

            var isSelected = type == currentType;
            var btn = new ToggleButton
            {
                Content = btnContent,
                IsChecked = isSelected,
                Height = 38,
                MinHeight = 38,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 0, 14, 0),
                RequestedTheme = ElementTheme.Light
            };

            UpdateSegmentedButtonVisual(btn, icon, text, isSelected);

            var capturedType = type;
            btn.Click += (_, _) =>
            {
                foreach (var item in buttons)
                {
                    var selected = item.Type == capturedType;
                    item.Button.IsChecked = selected;
                    UpdateSegmentedButtonVisual(item.Button, item.Icon, item.Text, selected);
                }
                onSelectionChanged(capturedType);
            };

            buttons.Add((type, btn, icon, text));
            panel.Children.Add(btn);
        }

        return panel;
    }

    private static void UpdateSegmentedButtonVisual(ToggleButton button, FontIcon icon, TextBlock text, bool isSelected)
    {
        button.RequestedTheme = ElementTheme.Light;
        if (isSelected)
        {
            if (GetResource<Brush>("SumaAccentMutedBrush") is { } mutedAccent)
            {
                button.Background = mutedAccent;
            }
            else
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 231, 237, 228));
            }

            if (GetResource<Brush>("SumaAccentBrush") is { } accent)
            {
                button.BorderBrush = accent;
            }
            else
            {
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 111, 128, 109));
            }

            button.BorderThickness = new Thickness(1.5);

            var darkSage = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 62, 44));
            icon.Foreground = darkSage;
            text.Foreground = darkSage;
            text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        else
        {
            if (GetResource<Brush>("SumaSurfaceBrush") is { } surface)
            {
                button.Background = surface;
            }
            else
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            }

            if (GetResource<Brush>("SumaBorderBrush") is { } border)
            {
                button.BorderBrush = border;
            }
            else
            {
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 228, 226, 220));
            }

            button.BorderThickness = new Thickness(1);

            if (GetResource<Brush>("SumaTextPrimaryBrush") is { } primary)
            {
                text.Foreground = primary;
            }
            else
            {
                text.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 28, 30));
            }

            if (GetResource<Brush>("SumaTextSecondaryBrush") is { } secondary)
            {
                icon.Foreground = secondary;
            }
            else
            {
                icon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 110, 115));
            }

            text.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    public static TextBlock CreateErrorText()
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            RequestedTheme = ElementTheme.Light
        };
        if (GetResource<Style>("SumaModalErrorTextStyle") is { } errStyle)
        {
            textBlock.Style = errStyle;
        }
        return textBlock;
    }

    public static void SetError(TextBlock textBlock, string message)
    {
        textBlock.Text = message;
        textBlock.Visibility = Visibility.Visible;
    }

    private static void PrepareBody(UIElement element)
    {
        switch (element)
        {
            case TextBox textBox when GetResource<Style>("SumaModalTextBoxStyle") is { } style: textBox.Style = style; break;
            case ComboBox comboBox when GetResource<Style>("SumaModalComboBoxStyle") is { } style: comboBox.Style = style; break;
            case DatePicker datePicker when GetResource<Style>("SumaModalDatePickerStyle") is { } style: datePicker.Style = style; break;
            case NumberBox numberBox when GetResource<Style>("SumaModalNumberBoxStyle") is { } style: numberBox.Style = style; break;
            case PasswordBox passwordBox when GetResource<Style>("SumaModalPasswordBoxStyle") is { } style: passwordBox.Style = style; break;
        }
        if (element is Panel panel) foreach (var panelChild in panel.Children) PrepareBody(panelChild);
        else if (element is Border { Child: UIElement borderChild }) PrepareBody(borderChild);
        else if (element is ContentControl { Content: UIElement contentChild }) PrepareBody(contentChild);
    }
}
