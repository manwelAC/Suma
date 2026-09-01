using Microsoft.UI.Xaml.Controls;

namespace Suma.Desktop.Navigation;

public interface INavigationPageFactory
{
    Page GetPage(NavigationRoute route);
}
