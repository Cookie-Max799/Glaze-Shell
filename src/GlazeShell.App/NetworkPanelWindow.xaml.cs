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
/// Всплывающая панель Wi-Fi: текущее подключение, видимые сети и переход в системные
/// настройки. Открывается по нажатию на индикатор сети верхней панели.
/// </summary>
public sealed partial class NetworkPanelWindow : Window
{
    private const int WidthDips = 340;
    private const int HeightDips = 380;

    private readonly ThemeApplicator _themes;
    private bool _closed;

    public NetworkPanelWindow(ThemeApplicator themes)
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

        SystemBackdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar);
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

    /// <summary>Показывает панель под индикатором сети.</summary>
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

        var state = await WiFiStatusProvider.GetStateAsync();

        if (state.IsConnected)
        {
            StatusText.Text = $"Подключено: {state.ConnectedName}";
            SubtitleText.Text = state.HasAdapter
                ? "Доступные сети Wi-Fi"
                : "Список сетей недоступен без разрешения Windows";
        }
        else
        {
            StatusText.Text = "Нет подключения";
            SubtitleText.Text = state.HasAdapter
                ? "Доступные сети Wi-Fi"
                : "Список сетей недоступен без разрешения Windows";
        }

        NetworksList.ItemsSource = state.Networks
            .Select(network => new
            {
                network.Name,
                Status = network.IsConnected
                    ? $"Подключено · сигнал {network.SignalPercent} %"
                    : $"Сигнал {network.SignalPercent} %",
            })
            .ToList();

        EmptyText.Visibility = state.Networks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        ShellTargets.Open("ms-settings:network-wifi");
        AppWindow.Hide();
    }
}
