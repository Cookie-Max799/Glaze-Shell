using System.Diagnostics;
using System.Net.NetworkInformation;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>
/// Снимок питания и сети для индикаторов верхней панели (A6).
/// </summary>
/// <param name="HasBattery">Обнаружена ли батарея (у настольных ПК её обычно нет).</param>
/// <param name="BatteryPercent">Заряд в процентах (0–100).</param>
/// <param name="IsCharging">Идёт ли зарядка.</param>
/// <param name="IsOnAcPower">Подключено ли внешнее питание.</param>
public readonly record struct PowerStatus(bool HasBattery, int BatteryPercent, bool IsCharging, bool IsOnAcPower);

/// <summary>
/// Читает состояние питания через <c>GetSystemPowerStatus</c> и доступность сети через
/// <see cref="NetworkInterface"/>. Значения обновляются по таймеру верхней панели.
/// </summary>
public static class SystemStatusProvider
{
    private const byte AcLineOnline = 1;
    private const byte BatteryFlagCharging = 0x08;
    private const byte BatteryFlagNoBattery = 0x80;
    private const byte BatteryPercentUnknown = 0xFF;

    private static TimeSpan _lastCpuTime = TimeSpan.Zero;
    private static DateTime _lastCpuCheck = DateTime.UtcNow;
    private static double _lastCpuUsage;

    public static PowerStatus GetPowerStatus()
    {
        if (!Kernel32.GetSystemPowerStatus(out var status))
        {
            return new PowerStatus(false, 100, false, true);
        }

        var hasBattery = (status.BatteryFlag & BatteryFlagNoBattery) == 0;
        var isOnAcPower = status.AcLineStatus == AcLineOnline;
        var isCharging = (status.BatteryFlag & BatteryFlagCharging) != 0;

        var percent = status.BatteryLifePercent == BatteryPercentUnknown
            ? (isOnAcPower ? 100 : 0)
            : status.BatteryLifePercent;

        return new PowerStatus(hasBattery, percent, isCharging, isOnAcPower);
    }

    /// <summary>
    /// Есть ли хотя бы один активный сетевой интерфейс. Это признак подключения, а не
    /// гарантия доступа в интернет.
    /// </summary>
    public static bool IsNetworkAvailable()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var currentCpu = process.TotalProcessorTime;

            if (_lastCpuTime == TimeSpan.Zero)
            {
                _lastCpuTime = currentCpu;
                _lastCpuCheck = now;
                return 0;
            }

            var timeDiff = (now - _lastCpuCheck).TotalMilliseconds;
            if (timeDiff <= 0)
            {
                return _lastCpuUsage;
            }

            var cpuDiff = (currentCpu - _lastCpuTime).TotalMilliseconds;
            var cpuUsage = (cpuDiff / (timeDiff * Environment.ProcessorCount)) * 100.0;
            _lastCpuUsage = Math.Clamp(cpuUsage, 0, 100);
            _lastCpuTime = currentCpu;
            _lastCpuCheck = now;
            return _lastCpuUsage;
        }
        catch
        {
            return 0;
        }
    }
}
