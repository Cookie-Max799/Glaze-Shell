using System.Runtime.InteropServices;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>Доступная Wi-Fi-сеть: имя (SSID) и уровень сигнала в процентах.</summary>
public readonly record struct WiFiNetworkInfo(string Name, int SignalPercent, bool IsConnected);

/// <summary>Состояние Wi-Fi: подключение и список видимых сетей.</summary>
public readonly record struct WiFiState(bool HasAdapter, bool IsConnected, string? ConnectedName, IReadOnlyList<WiFiNetworkInfo> Networks);

/// <summary>
/// Читает состояние Wi-Fi и список доступных сетей. Список берётся из Windows.Devices.WiFi
/// с явным запросом доступа: без разрешения Windows возвращает только состояние текущего
/// подключения, а панель показывает его и предлагает системные настройки.
/// </summary>
public static class WiFiStatusProvider
{
    /// <summary>Состояние Wi-Fi и доступные сети. Ошибки Windows не пробрасываются наружу.</summary>
    public static async Task<WiFiState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var (connected, connectedName) = GetCurrentConnection();

        try
        {
            if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed)
            {
                return new WiFiState(false, connected, connectedName, []);
            }

            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            var adapter = adapters.Count > 0 ? adapters[0] : null;

            if (adapter is null)
            {
                return new WiFiState(false, connected, connectedName, []);
            }

            await adapter.ScanAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var networks = adapter.NetworkReport.AvailableNetworks
                .GroupBy(network => network.Ssid)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => group.OrderByDescending(network => network.SignalBars).First())
                .Select(network => new WiFiNetworkInfo(
                    network.Ssid,
                    Math.Clamp((int)network.SignalBars * 25, 0, 100),
                    string.Equals(network.Ssid, connectedName, StringComparison.Ordinal)))
                .OrderByDescending(network => network.IsConnected)
                .ThenByDescending(network => network.SignalPercent)
                .ThenBy(network => network.Name, StringComparer.CurrentCulture)
                .ToList();

            return new WiFiState(true, connected, connectedName, networks);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new WiFiState(false, connected, connectedName, []);
        }
    }

    /// <summary>Текущее Wi-Fi-подключение: есть ли оно и имя сети.</summary>
    public static (bool Connected, string? Name) GetCurrentConnection()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();

            if (profile is null || !profile.IsWlanConnectionProfile)
            {
                return (false, null);
            }

            var connected = profile.GetNetworkConnectivityLevel() is not NetworkConnectivityLevel.None;
            return (connected, profile.ProfileName);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (false, null);
        }
    }
}
