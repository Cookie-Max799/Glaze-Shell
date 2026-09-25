using Microsoft.UI.Xaml;

namespace GlazeShell.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(string title)
    {
        InitializeComponent();
        Title = title;
    }
}
