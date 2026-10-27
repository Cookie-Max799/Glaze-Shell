using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Win32;

/// <summary>
/// Форматированное значение счётчика производительности PDH.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PdhFmtCounterValue
{
    public uint Status;
    public double Value;
}

/// <summary>
/// Элемент массива значений счётчика с именем экземпляра (например, движка GPU).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PdhFmtCounterValueItem
{
    public nint Name;
    public PdhFmtCounterValue Value;
}

/// <summary>
/// Минимальная обёртка над <c>pdh.dll</c>: открытие запроса, добавление счётчика и чтение
/// значений по маске экземпляров. Нужна индикатору загрузки GPU в верхней панели (A6).
/// </summary>
internal static class Pdh
{
    /// <summary>Значение возвращается как <c>double</c>.</summary>
    internal const uint FmtDouble = 0x00000200;

    /// <summary>Буфер меньше требуемого: размер и число элементов приходят в ответе.</summary>
    internal const uint MoreData = 0x800007D2;

    internal const uint CStatusValidData = 0x00000000;
    internal const uint CStatusNewData = 0x00000001;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhAddEnglishCounter(nint query, string fullCounterPath, nint userData, out nint counter);

    [DllImport("pdh.dll", ExactSpelling = true)]
    internal static extern uint PdhCollectQueryData(nint query);

    [DllImport("pdh.dll", ExactSpelling = true)]
    internal static extern uint PdhGetFormattedCounterArray(
        nint counter,
        uint format,
        ref uint bufferSize,
        out uint itemCount,
        nint buffer);

    [DllImport("pdh.dll", ExactSpelling = true)]
    internal static extern uint PdhCloseQuery(nint query);
}
