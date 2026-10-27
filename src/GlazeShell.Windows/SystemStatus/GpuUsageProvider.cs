using System.Diagnostics;
using System.Runtime.InteropServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>
/// Загрузка графического процессора по счётчику производительности
/// <c>\GPU Engine(*)\Utilization Percentage</c> (A6).
/// </summary>
/// <remarks>
/// Используется PDH, а не WMI: счётчик читается за доли миллисекунды и не поднимает службу
/// WMI ради одного процента. Значение берётся как максимум по экземплярам (движкам) всех
/// адаптеров — сумма по движкам легко превышает 100 % и не отражает общую загрузку.
/// Провайдер best-effort: если счётчика нет (нет GPU, старый драйвер) или он не отдал
/// ни одного экземпляра, индикатор просто не показывается.
/// </remarks>
public sealed class GpuUsageProvider : IDisposable
{
    private const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private const int InitialBufferSize = 16 * 1024;
    private const int MaxBufferSize = 1024 * 1024;

    private nint _query;
    private nint _counter;
    private nint _buffer;
    private uint _bufferSize;
    private bool _unavailable;
    private bool _disposed;

    public GpuUsageProvider()
    {
        _bufferSize = InitialBufferSize;
        _buffer = Marshal.AllocHGlobal((int)_bufferSize);

        if (Pdh.PdhOpenQuery(null, 0, out var query) != 0)
        {
            MarkUnavailable();
            return;
        }

        if (Pdh.PdhAddEnglishCounter(query, CounterPath, 0, out var counter) != 0)
        {
            _ = Pdh.PdhCloseQuery(query);
            MarkUnavailable();
            return;
        }

        _query = query;
        _counter = counter;

        // Первый сбор задаёт базу: значение процента появится только на следующем.
        _ = Pdh.PdhCollectQueryData(_query);
    }

    /// <summary>Удалось ли открыть счётчик: без него индикатор не имеет смысла.</summary>
    public bool IsAvailable => !_unavailable;

    /// <summary>
    /// Текущая загрузка GPU в процентах. <c>false</c>, если счётчик недоступен или не вернул
    /// ни одного экземпляра движка — тогда графического процессора для индикатора нет.
    /// </summary>
    public bool TryGetUsage(out double usage)
    {
        usage = 0;

        if (_unavailable || _disposed || _query == 0)
        {
            return false;
        }

        try
        {
            if (Pdh.PdhCollectQueryData(_query) != 0)
            {
                MarkUnavailable();
                return false;
            }

            var status = Pdh.PdhGetFormattedCounterArray(
                _counter,
                Pdh.FmtDouble,
                ref _bufferSize,
                out var itemCount,
                _buffer);

            if (status == Pdh.MoreData)
            {
                if (!GrowBuffer())
                {
                    MarkUnavailable();
                    return false;
                }

                status = Pdh.PdhGetFormattedCounterArray(
                    _counter,
                    Pdh.FmtDouble,
                    ref _bufferSize,
                    out itemCount,
                    _buffer);
            }

            if (status != 0 || itemCount == 0)
            {
                return false;
            }

            var peak = 0.0;
            var itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();

            for (var index = 0; index < itemCount; index++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(
                    _buffer + (index * itemSize));

                if (item.Value.Status is not (Pdh.CStatusValidData or Pdh.CStatusNewData))
                {
                    continue;
                }

                if (item.Value.Value > peak)
                {
                    peak = item.Value.Value;
                }
            }

            // Маска экземпляров может вернуть только движки без полезных значений: тогда
            // считать, что GPU нет, нельзя — счётчик доступен, просто кадр пустой.
            usage = Math.Clamp(peak, 0, 100);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.GpuUsage: reading the counter failed. {exception}");
            MarkUnavailable();
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_query != 0)
        {
            _ = Pdh.PdhCloseQuery(_query);
            _query = 0;
            _counter = 0;
        }

        if (_buffer != 0)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = 0;
        }
    }

    private bool GrowBuffer()
    {
        if (_bufferSize >= MaxBufferSize)
        {
            return false;
        }

        var size = Math.Min(_bufferSize * 2, MaxBufferSize);
        var buffer = Marshal.AllocHGlobal((int)size);

        if (buffer == 0)
        {
            return false;
        }

        Marshal.FreeHGlobal(_buffer);
        _buffer = buffer;
        _bufferSize = size;
        return true;
    }

    private void MarkUnavailable()
    {
        _unavailable = true;

        if (_buffer != 0)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = 0;
            _bufferSize = 0;
        }
    }
}
