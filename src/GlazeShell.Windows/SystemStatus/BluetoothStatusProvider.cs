using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>Bluetooth-устройство: имя и признак активного подключения.</summary>
public readonly record struct BluetoothDeviceInfo(string Name, bool IsConnected);

/// <summary>Состояние Bluetooth-адаптера.</summary>
public readonly record struct BluetoothState(bool HasAdapter, bool IsEnabled, int ConnectedCount);

/// <summary>
/// Читает состояние Bluetooth и список устройств через Windows.Devices WinRT API.
/// </summary>
/// <remarks>
/// <para>
/// Подключение определяется несколькими независимыми способами, потому что один и тот же
/// запрос на разных машинах ведёт себя по-разному: классические устройства (наушники,
/// колонки, мыши) Windows показывает через AEP-контейнер, LE-устройства — через
/// <c>BluetoothLEDevice</c>, а свойство <c>System.Devices.Aep.IsConnected</c> у части
/// устройств остаётся <c>false</c>, пока подключён дочерний профиль (например, аудио).
/// Поэтому сначала спрашиваются готовые селекторы «только подключённые», а запрос по
/// протоколу Bluetooth остаётся запасным вариантом.
/// </para>
/// <para>
/// Вызовы WinRT асинхронные и могут завершиться <see cref="COMException"/> или
/// <see cref="UnauthorizedAccessException"/> (например, когда служба Bluetooth выключена
/// или устройство не отдаёт список). Ошибки не пробрасываются наружу: панель статуса
/// в таком случае просто показывает «Bluetooth недоступен».
/// </para>
/// </remarks>
public static class BluetoothStatusProvider
{
    /// <summary>Идентификатор протокола Bluetooth в свойствах устройства.</summary>
    private const string BluetoothProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";

    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";

    /// <summary>Состояние адаптера: есть ли он и включён ли.</summary>
    public static async Task<BluetoothState> GetStateAsync()
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var bluetooth = radios.Where(r => r.Kind == RadioKind.Bluetooth).ToList();

            if (bluetooth.Count == 0)
            {
                return new BluetoothState(false, false, 0);
            }

            var enabled = bluetooth.Any(r => r.State == RadioState.On);

            if (!enabled)
            {
                return new BluetoothState(true, false, 0);
            }

            var devices = await GetPairedDevicesAsync();
            var connected = devices.Count(device => device.IsConnected);
            return new BluetoothState(true, true, connected);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return new BluetoothState(false, false, 0);
        }
    }

    /// <summary>Есть ли подключённые Bluetooth-устройства.</summary>
    public static async Task<bool> HasConnectedDevicesAsync()
    {
        try
        {
            if (await AnyConnectedAsync(
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)))
            {
                return true;
            }

            if (await AnyConnectedAsync(
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)))
            {
                return true;
            }

            // Запасной путь: AEP-контейнеры с явным признаком подключения. Он ловит
            // устройства, которые не видны селекторам BluetoothDevice (например,
            // некоторые док-станции и составные профили).
            var selector =
                $"(System.Devices.Aep.ProtocolId:=\"{BluetoothProtocolId}\") AND " +
                $"(System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True)";

            return await AnyConnectedAsync(selector);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return false;
        }
    }

    /// <summary>Сопряжённые Bluetooth-устройства с признаком подключения.</summary>
    public static async Task<IReadOnlyList<BluetoothDeviceInfo>> GetPairedDevicesAsync()
    {
        var paired = await QueryDevicesAsync(
            $"(System.Devices.Aep.ProtocolId:=\"{BluetoothProtocolId}\") AND " +
            "(System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True)",
            [ConnectedProperty]);

        if (paired.Count > 0)
        {
            return paired;
        }

        // Список сопряжённых пуст (или запрос недоступен) — показываем хотя бы то,
        // что сейчас подключено: для индикатора и панели это полезнее пустоты.
        return await QueryConnectedDevicesAsync();
    }

    private static async Task<bool> AnyConnectedAsync(string selector)
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(selector, [ConnectedProperty]);
            return devices.Count > 0;
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return false;
        }
    }

    private static async Task<IReadOnlyList<BluetoothDeviceInfo>> QueryDevicesAsync(
        string selector,
        IReadOnlyList<string> properties)
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(selector, properties);
            var result = new List<BluetoothDeviceInfo>(devices.Count);

            foreach (var device in devices)
            {
                var name = string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth-устройство" : device.Name;
                var connected = device.Properties.TryGetValue(ConnectedProperty, out var value)
                    && value is true;

                if (!result.Any(existing => string.Equals(existing.Name, name, StringComparison.Ordinal)))
                {
                    result.Add(new BluetoothDeviceInfo(name, connected));
                }
            }

            return result
                .OrderByDescending(device => device.IsConnected)
                .ThenBy(device => device.Name, StringComparer.CurrentCulture)
                .ToList();
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<BluetoothDeviceInfo>> QueryConnectedDevicesAsync()
    {
        var names = new List<BluetoothDeviceInfo>();

        Collect(await QueryDevicesAsync(
            BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
            [ConnectedProperty]));

        Collect(await QueryDevicesAsync(
            BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
            [ConnectedProperty]));

        return names;

        void Collect(IReadOnlyList<BluetoothDeviceInfo> devices)
        {
            foreach (var device in devices)
            {
                if (!names.Any(existing => string.Equals(existing.Name, device.Name, StringComparison.Ordinal)))
                {
                    names.Add(device with { IsConnected = true });
                }
            }
        }
    }

    /// <summary>
    /// Ожидаемые отказы WinRT: служба выключена, доступ запрещён, API недоступен без
    /// пакетной идентичности. Всё остальное считается ошибкой программирования.
    /// </summary>
    private static bool IsExpected(Exception exception) =>
        exception is COMException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException;
}
