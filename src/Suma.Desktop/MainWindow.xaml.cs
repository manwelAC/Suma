using Microsoft.UI.Xaml;
using Suma.Desktop.Shell;
using Windows.Graphics;

namespace Suma.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow(ShellPage shellPage)
    {
        InitializeComponent();
        ShellHost.Content = shellPage;
        AppWindow.Resize(new SizeInt32(1200, 760));
    }
}
