using GlazeShell.Windows.SystemStatus;

namespace GlazeShell.Windows.Tests.SystemStatus;

[TestClass]
public sealed class SystemStatusProvidersTests
{
    /// <summary>
    /// Чтение состояния Bluetooth не должно падать на машине без адаптера: провайдер
    /// обязан вернуть согласованный результат (без адаптера он выключен и без устройств).
    /// </summary>
    [TestMethod]
    public async Task BluetoothStateIsSafeWithoutAdapter()
    {
        var state = await BluetoothStatusProvider.GetStateAsync();

        Assert.IsGreaterThanOrEqualTo(0, state.ConnectedCount);
        Assert.IsFalse(state is { HasAdapter: false, IsEnabled: true });
    }

    /// <summary>
    /// Список Wi-Fi-сетей может быть пустым без разрешения Windows, но сам вызов
    /// обязан завершиться без исключения и вернуть не-null коллекцию.
    /// </summary>
    [TestMethod]
    public async Task WiFiStateReturnsCollectionWithoutException()
    {
        var state = await WiFiStatusProvider.GetStateAsync();

        Assert.IsNotNull(state.Networks);
        Assert.IsFalse(state is { IsConnected: true, ConnectedName: null });
    }
}
