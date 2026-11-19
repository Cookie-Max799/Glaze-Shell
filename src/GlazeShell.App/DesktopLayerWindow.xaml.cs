using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Безопасный слой рабочего стола Glaze: неактивирующее окно уровня рабочего стола
/// (<c>HWND_BOTTOM</c>), которое показывает значки приложений активной вкладки
/// <see cref="IDesktopManager"/>. Не заменяет Explorer, не перекрывает обычные окна и
/// показывается без перехвата фокуса.
/// </summary>
public sealed partial class DesktopLayerWindow : Window
{
    private readonly ShellMonitorTarget _monitorTarget;
    private nint _hwnd;
    private bool _closed;

    public DesktopLayerWindow(
        IDesktopManager desktopManager,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IEventManager events,
        IMonitorManager monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        _monitorTarget = new ShellMonitorTarget(monitors);

        ViewModel = new DesktopLayerViewModel(
            desktopManager,
            applications,
            launcher,
            events,
            DispatcherQueue);

        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        Title = "Glaze Shell — Desktop Layer";

        // Слой показывается без активации, поэтому Activated не приходит и x:Bind
        // не обновлялись бы: значки оставались бы пустыми.
        Bindings?.Update();

        ConfigureDesktopLevelWindow();

        Closed += (_, _) =>
        {
            _closed = true;
            ViewModel.Dispose();
        };

        _ = ViewModel.InitializeAsync();
    }

    public DesktopLayerViewModel ViewModel { get; }

    /// <summary>
    /// Настраивает окно: без рамки и панели задач, с уровня рабочего стола. В отличие от
    /// панелей, слой не topmost: обычные окна всегда остаются над ним.
    /// </summary>
    private void ConfigureDesktopLevelWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = false;
        }

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Tool window + уровень рабочего стола: слой не появляется в Alt+Tab и остаётся
        // под всеми обычными окнами. Кнопки панели задач у слоя нет.
        OverlayWindow.MakeDesktopLevelToolWindow(_hwnd);

        UpdatePlacement();
        OverlayWindow.ShowWithoutActivation(_hwnd);
    }

    /// <summary>
    /// Ставит окно на весь основной монитор (в физических пикселях, как dock): слой
    /// повторяет границы монитора, чтобы значки лежали на видимой части экрана.
    /// </summary>
    private void UpdatePlacement()
    {
        if (_closed)
        {
            return;
        }

        var monitor = _monitorTarget.Target;

        if (monitor is null)
        {
            return;
        }

        var bounds = monitor.Bounds;
        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        // MoveAndResize поднимает окно в Z-порядке: уровень рабочего стола возвращается
        // после каждого перемещения.
        OverlayWindow.SendToDesktopLevel(_hwnd);

        ViewModel.LoadIcons();
    }

    private void OnIconDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DesktopIconViewModel icon })
        {
            _ = ViewModel.ActivateAsync(icon);
        }
    }

    private void OnIconRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DesktopIconViewModel icon })
        {
            return;
        }

        if (sender is not FrameworkElement tile)
        {
            return;
        }

        var flyout = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Открыть" };
        open.Click += (_, _) => _ = ViewModel.ActivateAsync(icon);
        flyout.Items.Add(open);

        if (icon.Path is { Length: > 0 } path)
        {
            var location = new MenuFlyoutItem { Text = "Открыть расположение файла" };
            location.Click += (_, _) => ShellTargets.ShowInExplorer(path);
            flyout.Items.Add(location);

            var copyPath = new MenuFlyoutItem { Text = "Копировать путь" };
            copyPath.Click += (_, _) => CopyToClipboard(path);
            flyout.Items.Add(copyPath);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var properties = new MenuFlyoutItem { Text = "Свойства" };
            properties.Click += (_, _) => ShellTargets.ShowProperties(path);
            flyout.Items.Add(properties);
        }

        flyout.Closed += (_, _) => OverlayWindow.SetNonActivating(_hwnd, true);
        OverlayWindow.SetNonActivating(_hwnd, false);

        flyout.ShowAt(tile, new FlyoutShowOptions { Position = e.GetPosition(tile) });
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var pack = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            pack.SetText(text);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pack);
        }
        catch (Exception)
        {
            // Буфер обмена может быть занят другим процессом: повторная попытка — при
            // следующем выборе пункта меню.
        }
    }
}
