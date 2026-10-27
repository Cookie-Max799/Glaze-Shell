using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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

    private static ulong _lastIdleTime;
    private static ulong _lastKernelTime;
    private static ulong _lastUserTime;
    private static bool _hasCpuBaseline;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME idleTime,
        out FILETIME kernelTime,
        out FILETIME userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    private static ulong ToULong(FILETIME time) => ((ulong)time.High << 32) | time.Low;

    /// <summary>
    /// Загрузка процессора в процентах за интервал между двумя вызовами. Первый вызов только
    /// запоминает базовые значения счётчиков ядра и возвращает 0: разницу считать не с чем.
    /// </summary>
    public static double GetCpuUsage()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return _lastCpuUsage;
            }

            var idleTime = ToULong(idle);
            var kernelTime = ToULong(kernel);
            var userTime = ToULong(user);

            if (!_hasCpuBaseline)
            {
                StoreCpuSample(idleTime, kernelTime, userTime);
                return 0;
            }

            var idleDiff = idleTime - _lastIdleTime;
            var kernelDiff = kernelTime - _lastKernelTime;
            var userDiff = userTime - _lastUserTime;

            var total = kernelDiff + userDiff;
            StoreCpuSample(idleTime, kernelTime, userTime);

            if (total == 0)
            {
                return _lastCpuUsage;
            }

            var usage = (1.0 - ((double)idleDiff / total)) * 100.0;
            _lastCpuUsage = Math.Clamp(usage, 0, 100);
            return _lastCpuUsage;
        }
        catch
        {
            return 0;
        }
    }

    private static void StoreCpuSample(ulong idleTime, ulong kernelTime, ulong userTime)
    {
        _lastIdleTime = idleTime;
        _lastKernelTime = kernelTime;
        _lastUserTime = userTime;
        _hasCpuBaseline = true;
    }
}
