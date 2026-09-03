using Microsoft.UI.Xaml.Controls;

namespace Suma.Desktop.Pages.Recovery;

public sealed partial class RecoveryPage : Page
{
    public RecoveryPage(string message) { Message = message; InitializeComponent(); }
    public string Message { get; }
}
