using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GlazeShell.App.Theming;

/// <summary>
/// Стеклянный фон панели на системном Acrylic с управляемым тонированием. В отличие от
/// готового <see cref="DesktopAcrylicBackdrop"/>, контроллер позволяет привязать
/// <see cref="DesktopAcrylicController.TintColor"/> и
/// <see cref="DesktopAcrylicController.TintOpacity"/> к ползункам прозрачности: при 0 %
/// панель плотная, при 90 % остаётся почти чистый blur рабочего стола.
/// </summary>
/// <remarks>
/// Окно панели имеет региональную маску (<see cref="GlazeShell.Windows.WindowManagement.OverlayWindow"/>),
/// и Acrylic рисуется только внутри неё — поэтому backdrop не закрашивает полэкрана под
/// раскрытым popover.
/// </remarks>
public sealed class PanelBackdrop : SystemBackdrop, IDisposable
{
    private readonly Color _tint;
    private readonly double _tintOpacity;
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;

    /// <param name="tint">Цвет тонирования — цвет стекла действующей темы.</param>
    /// <param name="tintOpacity">
    /// Непрозрачность тонирования (0–1). 1 — панель плотная, 0 — только размытие.
    /// </param>
    public PanelBackdrop(Color tint, double tintOpacity)
    {
        _tint = tint;
        _tintOpacity = Math.Clamp(tintOpacity, 0, 1);
    }

    /// <summary>Поддерживает ли система Acrylic за окном.</summary>
    public static bool IsSupported()
    {
        try
        {
            return DesktopAcrylicController.IsSupported();
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Освобождает системный контроллер, если окно закрылось без отключения цели.</summary>
    public void Dispose()
    {
        if (_controller is not null)
        {
            _controller.RemoveAllSystemBackdropTargets();
            _controller.Dispose();
            _controller = null;
        }

        _configuration = null;
    }

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        try
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            _configuration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
            _configuration.IsInputActive = true;

            _controller = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Base,
                TintColor = _tint,
                TintOpacity = (float)_tintOpacity,

                // Слой свечения чуть слабее тонирования: он даёт «матовость» стекла, но при
                // высокой прозрачности не должен превращать панель в серую пелену.
                LuminosityOpacity = (float)(_tintOpacity * 0.6),
                FallbackColor = _tint
            };

            _controller.AddSystemBackdropTarget(connectedTarget);
            _controller.SetSystemBackdropConfiguration(_configuration);
        }
        catch (Exception exception)
        {
            // Система может отказать в Acrylic (старая сборка Windows, удалённый компонент).
            // Панель тогда остаётся без backdrop: лучше плотный фон, чем падение оболочки.
            System.Diagnostics.Trace.WriteLine($"GlazeShell.PanelBackdrop: acrylic is unavailable. {exception}");
            Dispose();
        }
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot)
    {
        if (_controller is not null && _configuration is not null)
        {
            _controller.SetSystemBackdropConfiguration(_configuration);
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        if (_controller is not null)
        {
            _controller.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller.Dispose();
            _controller = null;
        }

        _configuration = null;
    }
}
