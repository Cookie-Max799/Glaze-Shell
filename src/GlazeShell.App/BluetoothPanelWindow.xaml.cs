using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.SystemStatus;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Всплывающая панель Bluetooth: состояние адаптера, сопряжённые устройства и переход
/// в системные настройки. Открывается по нажатию на индикатор верхней панели. Живёт до
/// закрытия: повторный показ создаёт новый экземпляр, потому что повторный <c>Show</c>
/// скрытого окна WinUI ненадёжен.
/// </summary>
public sealed partial class BluetoothPanelWindow : Window
{
    private const int WidthDips = 340;
    private const int HeightDips = 360;

    private readonly ThemeApplicator _themes;
    private bool _closed;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private long _shownAtTick;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _dismissTimer;
    private PanelBackdrop? _backdrop;

    public BluetoothPanelWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        InitializeComponent();
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = _backdrop;
        _themes.ApplyFromSettings(Root);

        // Панель забирает фокус при показе, поэтому любой клик вне её — по рабочему столу,
        // верхней панели или приложению — уводит фокус. Таймер замечает это и закрывает
        // панель: надёжнее, чем событие Deactivated, которое не всегда доставляется.
        _dismissTimer = DispatcherQueue.CreateTimer();
        _dismissTimer.Interval = TimeSpan.FromMilliseconds(120);
        _dismissTimer.Tick += (_, _) => DismissIfDeactivated();

        Activated += (_, args) =>
        {
            var elapsed = Environment.TickCount64 - _shownAtTick;
            if (elapsed > 800 && args.WindowActivationState == WindowActivationState.Deactivated)
            {
                ClosePanel();
            }
        };

        Closed += (_, _) => _closed = true;
    }

    /// <summary>Пересоздаёт Acrylic-фон при смене прозрачности или темы.</summary>
    public void RefreshBackdrop()
    {
        if (_closed)
        {
            return;
        }

        var previous = _backdrop;
        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = _backdrop;
        previous?.Dispose();
        _themes.ApplyFromSettings(Root);
    }

    /// <summary>Закрывает панель: окно разрушается, следующий показ создаёт новое.</summary>
    public void ClosePanel()
    {
        _dismissTimer?.Stop();
        if (!_closed)
        {
            Close();
        }
    }

    /// <summary>Сдвигает панель вместе с верхней полосой при её выезде или автоскрытии.</summary>
    public void ShiftBy(int deltaPx)
    {
        if (_closed || deltaPx == 0 || !AppWindow.IsVisible)
        {
            return;
        }

        var bounds = AppWindow.Position;
        AppWindow.Move(new PointInt32(bounds.X, bounds.Y + deltaPx));
    }

    private void DismissIfDeactivated()
    {
        if (_closed || !AppWindow.IsVisible)
        {
            _dismissTimer?.Stop();
            return;
        }

        // Панели нужно время получить фокус после Show: пока идёт грейс-период, не трогаем.
        if (Environment.TickCount64 - _shownAtTick <= 800)
        {
            return;
        }

        // Фокус у самой панели или в своём процессе — панель остаётся открытой.
        if (!OverlayWindow.TryGetForegroundWindow(out var hwnd, out var processId))
        {
            return;
        }

        var panelHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == panelHwnd || processId == _ownProcessId)
        {
            return;
        }

        // Фокус на рабочем столе или в чужом приложении — панель закрывается.
        ClosePanel();
    }

    /// <summary>Показывает панель под индикатором Bluetooth.</summary>
    public async Task ShowNearIconAsync(int iconX, int iconY, int screenLeft, int screenWidth, double scale)
    {
        if (_closed)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(hwnd);
        OverlayWindow.UseRoundedCorners(hwnd);

        var width = (int)Math.Round(WidthDips * scale);
        var height = (int)Math.Round(HeightDips * scale);
        var margin = (int)Math.Round(8 * scale);
        var x = Math.Clamp(iconX - (width / 2), screenLeft + margin, screenLeft + screenWidth - width - margin);
        var y = iconY + (int)Math.Round(36 * scale);

        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        _shownAtTick = Environment.TickCount64;
        AppWindow.Show();
        Activate();
        OverlayWindow.ActivateForInput(hwnd);
        _dismissTimer?.Start();

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        StatusText.Text = "Проверка состояния…";
        SubtitleText.Text = string.Empty;

        var state = await BluetoothStatusProvider.GetStateAsync();

        if (!state.HasAdapter)
        {
            StatusText.Text = "Bluetooth недоступен";
            SubtitleText.Text = "Адаптер не найден или служба выключена";
            DevicesList.ItemsSource = null;
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        if (!state.IsEnabled)
        {
            StatusText.Text = "Bluetooth выключен";
            SubtitleText.Text = "Включите Bluetooth в параметрах Windows";
            DevicesList.ItemsSource = null;
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        var devices = await BluetoothStatusProvider.GetPairedDevicesAsync();

        StatusText.Text = state.ConnectedCount > 0
            ? $"Bluetooth включён · подключено {state.ConnectedCount}"
            : "Bluetooth включён";
        SubtitleText.Text = devices.Count == 0
            ? "Сопряжённых устройств нет"
            : $"Сопряжённые устройства: {devices.Count}";

        DevicesList.ItemsSource = devices
            .Select(device => new
            {
                device.Name,
                Status = device.IsConnected ? "Подключено" : "Не подключено",
            })
            .ToList();

        EmptyText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        ShellTargets.Open("ms-settings:bluetooth");
        ClosePanel();
    }
}
