namespace GlazeShell.Windows.Shell;

using GlazeShell.Infrastructure.Logging;
using Microsoft.Win32;

/// <summary>
/// Автоматически включает жест «тап тремя пальцами» для точной сенсорной панели
/// (Precision Touchpad) на Windows 11, чтобы системный жест открывал меню Glaze Shell
/// вместо системного поиска. Конфигурация выполняется один раз: если значение уже
/// записано в реестре, повторная настройка пропускается.
/// </summary>
public sealed class PrecisionTouchpadConfigurator
{
    private const string TouchpadKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";

    private const string ThreeFingerTapValueName = "ThreeFingerTapEnabled";

    private const int Windows11Build = 22000;

    private readonly FileLogger _logger;

    public PrecisionTouchpadConfigurator(FileLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Включает жест «тап тремя пальцами», если система поддерживает точную
    /// сенсорную панель и конфигурация ещё не применена. Возвращает true,
    /// когда конфигурация была записана при этом вызове.
    /// </summary>
    public bool ConfigureIfNeeded()
    {
        try
        {
            if (!IsWindows11())
            {
                _logger.Write(
                    GlazeLogLevel.Information,
                    "TouchpadConfig",
                    "Windows 11 not detected; three-finger tap auto-configuration is skipped.");
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(TouchpadKeyPath, writable: true);
            if (key is null)
            {
                _logger.Write(
                    GlazeLogLevel.Warning,
                    "TouchpadConfig",
                    $"The PrecisionTouchPad key is missing; the device may not be a Precision Touchpad.");
                return false;
            }

            if (key.GetValue(ThreeFingerTapValueName) is int enabled && enabled == 1)
            {
                _logger.Write(
                    GlazeLogLevel.Information,
                    "TouchpadConfig",
                    "Three-finger tap is already configured; nothing to do.");
                return false;
            }

            key.SetValue(ThreeFingerTapValueName, 1, RegistryValueKind.DWord);
            _logger.Write(
                GlazeLogLevel.Information,
                "TouchpadConfig",
                "Three-finger tap is enabled for the Precision Touchpad.");
            return true;
        }
        catch (Exception exception)
        {
            _logger.Write(
                GlazeLogLevel.Error,
                "TouchpadConfig",
                "Failed to configure the three-finger tap gesture.",
                exception);
            return false;
        }
    }

    /// <summary>Windows 11 — сборки начиная с 22000 (21H2).</summary>
    private static bool IsWindows11()
    {
        var build = Environment.OSVersion.Version.Build;
        return Environment.OSVersion.Platform is PlatformID.Win32NT
            && Environment.OSVersion.Version.Major >= 10
            && build >= Windows11Build;
    }
}
