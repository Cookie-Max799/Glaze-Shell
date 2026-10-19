using System.Runtime.InteropServices;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Audio;

/// <summary>
/// Громкость устройства вывода по умолчанию через Core Audio (функция D1): чтение и запись
/// общего уровня и режима «без звука». Устройство берётся лениво и переживает его смену:
/// при первой же ошибке ссылка сбрасывается, поэтому следующий вызов найдёт новое устройство,
/// а не останется с мёртвым указателем.
/// </summary>
/// <remarks>
/// Методы рассчитаны на поток интерфейса (STA), где COM уже инициализирован WinUI. Объекты
/// намеренно не создаются на отдельном временном потоке: вместе с его завершением квартира
/// была бы разрушена, а ссылка на громкость стала бы висячей.
/// </remarks>
public sealed class SystemVolumeProvider : IDisposable
{
    /// <summary>CLSID перечислителя устройств <c>MMDeviceEnumerator</c>.</summary>
    private static readonly Guid EnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static readonly Guid EnumeratorInterfaceId = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    private static readonly Guid EndpointVolumeInterfaceId = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private readonly object _sync = new();
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _endpoint;
    private bool _disposed;

    /// <summary>Общий уровень громкости в процентах (0–100) и режим «без звука».</summary>
    public bool TryGetVolume(out int percent, out bool muted)
    {
        percent = 0;
        muted = false;

        lock (_sync)
        {
            if (!TryGetEndpoint(out var endpoint))
            {
                return false;
            }

            if (endpoint.GetMasterVolumeLevelScalar(out var scalar) < 0 || endpoint.GetMute(out var isMuted) < 0)
            {
                DropEndpoint();
                return false;
            }

            percent = ToPercent(scalar);
            muted = isMuted;
            return true;
        }
    }

    /// <summary>Задаёт общий уровень громкости; значение ограничивается диапазоном 0–100.</summary>
    public bool TrySetVolume(int percent)
    {
        var scalar = Math.Clamp(percent, 0, 100) / 100f;

        lock (_sync)
        {
            if (!TryGetEndpoint(out var endpoint))
            {
                return false;
            }

            if (endpoint.SetMasterVolumeLevelScalar(scalar, 0) < 0)
            {
                DropEndpoint();
                return false;
            }

            return true;
        }
    }

    /// <summary>Включает или выключает звук.</summary>
    public bool TrySetMute(bool muted)
    {
        lock (_sync)
        {
            if (!TryGetEndpoint(out var endpoint))
            {
                return false;
            }

            if (endpoint.SetMute(muted, 0) < 0)
            {
                DropEndpoint();
                return false;
            }

            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            DropEndpoint();

            var enumerator = _enumerator;
            _enumerator = null;
            Release(enumerator);
        }
    }

    private static int ToPercent(float scalar) =>
        (int)Math.Round(Math.Clamp(scalar, 0f, 1f) * 100);

    private bool TryGetEndpoint(out IAudioEndpointVolume endpoint)
    {
        endpoint = null!;

        if (_disposed)
        {
            return false;
        }

        if (_endpoint is not null)
        {
            endpoint = _endpoint;
            return true;
        }

        IMMDevice? device = null;

        try
        {
            _enumerator ??= ShellIdentifiers.CreateInstance<IMMDeviceEnumerator>(
                in EnumeratorClassId,
                in EnumeratorInterfaceId,
                Ole32.ClsctxInProcServer | Ole32.ClsctxLocalServer);

            if (_enumerator.GetDefaultAudioEndpoint(AudioDataFlow.Render, AudioRole.Multimedia, out device) < 0 ||
                device is null)
            {
                return false;
            }

            var interfaceId = EndpointVolumeInterfaceId;

            if (device.Activate(
                ref interfaceId,
                Ole32.ClsctxInProcServer | Ole32.ClsctxLocalServer,
                0,
                out var instance) < 0 || instance is null)
            {
                return false;
            }

            _endpoint = (IAudioEndpointVolume)instance;
            endpoint = _endpoint;
            return true;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or InvalidOperationException)
        {
            // Устройства вывода может не быть вовсе (сервер без звуковой карты) или COM
            // может отказать: это не ошибка приложения, а недоступная возможность.
            DropEndpoint();
            return false;
        }
        finally
        {
            Release(device);
        }
    }

    private void DropEndpoint()
    {
        var endpoint = _endpoint;
        _endpoint = null;
        Release(endpoint);
    }

    private static void Release(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
        {
            return;
        }

        try
        {
            _ = Marshal.ReleaseComObject(instance);
        }
        catch (ArgumentException)
        {
            // Объект уже освобождён: повторное освобождение не требуется.
        }
    }
}
