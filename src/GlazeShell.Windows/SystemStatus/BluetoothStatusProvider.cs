using System.Runtime.InteropServices;
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
/// Вызовы WinRT асинхронные и могут завершиться <see cref="COMException"/> (например,
/// когда служба Bluetooth выключена). Ошибки не пробрасываются наружу: панель статуса
/// в таком случае просто показывает «Bluetooth недоступен».
/// </remarks>
public static class BluetoothStatusProvider
{
    /// <summary>Идентификатор протокола Bluetooth в свойствах устройства.</summary>
    private const string BluetoothProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";

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
            var devices = await GetPairedDevicesAsync();
            return new BluetoothState(true, enabled, devices.Count(d => d.IsConnected));
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new BluetoothState(false, false, 0);
        }
    }

    /// <summary>Есть ли подключённые Bluetooth-устройства.</summary>
    public static async Task<bool> HasConnectedDevicesAsync()
    {
        var devices = await GetPairedDevicesAsync();
        return devices.Any(d => d.IsConnected);
    }

    /// <summary>Сопряжённые Bluetooth-устройства с признаком подключения.</summary>
    public static async Task<IReadOnlyList<BluetoothDeviceInfo>> GetPairedDevicesAsync()
    {
        var selector =
            $"(System.Devices.Aep.ProtocolId:=\"{BluetoothProtocolId}\") AND " +
            "(System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True)";

        try
        {
            var devices = await DeviceInformation.FindAllAsync(selector, ["System.Devices.Aep.IsConnected"]);
            var result = new List<BluetoothDeviceInfo>(devices.Count);

            foreach (var device in devices)
            {
                var name = string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth-устройство" : device.Name;
                var connected = device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value)
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
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }
}
