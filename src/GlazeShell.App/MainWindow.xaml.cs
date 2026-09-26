using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace GlazeShell.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1120;
    private const int DefaultHeight = 720;
    private const int MinWidth = 720;
    private const int MinHeight = 480;

    public MainWindow(string title)
    {
        InitializeComponent();
        Title = title;

        ApplyInitialSizeAndPosition();
    }

    private void ApplyInitialSizeAndPosition()
    {
        var appWindow = ResolveAppWindow();
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var width = Math.Clamp(workArea.Width / 2, MinWidth, DefaultWidth);
        var height = Math.Clamp(workArea.Height / 2, MinHeight, DefaultHeight);
        var size = new Windows.Graphics.SizeInt32(width, height);

        appWindow.Resize(size);
        appWindow.Move(new Windows.Graphics.PointInt32(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2)));
    }

    private AppWindow ResolveAppWindow()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }
}
