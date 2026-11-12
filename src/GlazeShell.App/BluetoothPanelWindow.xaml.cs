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
/// в системные настройки. Открывается по нажатию на индикатор верхней панели.
/// </summary>
public sealed partial class BluetoothPanelWindow : Window
{
    private const int WidthDips = 340;
    private const int HeightDips = 360;

    private readonly ThemeApplicator _themes;
    private bool _closed;

    public BluetoothPanelWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        InitializeComponent();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        SystemBackdrop = _themes.CreatePanelBackdrop(isDock: false);
        _themes.ApplyFromSettings(Root);

        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppWindow.Hide();
            }
        };

        Closed += (_, _) => _closed = true;
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
        AppWindow.Show();
        Activate();

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
        AppWindow.Hide();
    }
}
