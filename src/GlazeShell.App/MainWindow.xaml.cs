using GlazeShell.App.Presentation;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace GlazeShell.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1120;
    private const int DefaultHeight = 720;
    private const int MinWidth = 720;
    private const int MinHeight = 480;

    public MainWindow(
        string title,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IEventManager events)
    {
        ViewModel = new MainViewModel(applications, launcher, windows, events, DispatcherQueue);
        InitializeComponent();
        Title = title;

        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), handledEventsToo: true);

        ApplyInitialSizeAndPosition();
        Activate();

        ViewModel.Initialize();
        _ = ViewModel.RefreshAsync();
    }

    public MainViewModel ViewModel { get; }

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        ViewModel.RefreshCommand.Execute(null);

    private void OnLaunchClick(object sender, RoutedEventArgs e) =>
        ViewModel.LaunchCommand.Execute(null);

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        ViewModel.CloseCommand.Execute(null);

    private void OnRestartClick(object sender, RoutedEventArgs e) =>
        ViewModel.RestartCommand.Execute(null);

    private void ApplyInitialSizeAndPosition()
    {
        var appWindow = ResolveAppWindow();
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var width = Math.Clamp(workArea.Width / 2, MinWidth, DefaultWidth);
        var height = Math.Clamp(workArea.Height / 2, MinHeight, DefaultHeight);
        var size = new global::Windows.Graphics.SizeInt32(width, height);

        appWindow.Resize(size);
        appWindow.Move(new global::Windows.Graphics.PointInt32(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2)));
    }

    private AppWindow ResolveAppWindow()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                ViewModel.MoveSelection(1);
                e.Handled = true;
                break;

            case VirtualKey.Up:
                ViewModel.MoveSelection(-1);
                e.Handled = true;
                break;

            case VirtualKey.Enter when ViewModel.Selected is not null:
                ViewModel.LaunchCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.Space when SearchBox.FocusState == FocusState.Unfocused && ViewModel.Selected is not null:
                ViewModel.LaunchCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.F5:
                ViewModel.RefreshCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                SearchBox.Text = string.Empty;
                e.Handled = true;
                break;
        }
    }
}