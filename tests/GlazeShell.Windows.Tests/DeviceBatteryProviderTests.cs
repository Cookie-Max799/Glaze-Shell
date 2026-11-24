using GlazeShell.Windows.SystemStatus;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class DeviceBatteryProviderTests
{
    [TestMethod]
    public void ReadingDevicesNeverThrows()
    {
        // Дерево PnP зависит от машины: на одном стенде устройства с зарядом есть,
        // на другом нет. Проверяется устойчивость чтения, а не конкретный список.
        var devices = DeviceBatteryProvider.GetDevices();

        Assert.IsNotNull(devices);
    }

    [TestMethod]
    public void EveryDeviceHasUsableNameAndBattery()
    {
        foreach (var device in DeviceBatteryProvider.GetDevices())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(device.Name), "Устройство без имени.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(device.Id), "Устройство без идентификатора.");
            Assert.IsGreaterThanOrEqualTo(0, device.Battery, $"Недопустимый заряд у «{device.Name}».");
            Assert.IsLessThanOrEqualTo(100, device.Battery, $"Недопустимый заряд у «{device.Name}».");
        }
    }

    [TestMethod]
    public void DevicesAreUniquePerContainer()
    {
        // Профили одного устройства объединяются по контейнеру PnP: наушники с
        // гарнитурой и транспортом не должны появляться в списке несколько раз.
        var ids = DeviceBatteryProvider.GetDevices().Select(device => device.Id).ToArray();

        Assert.HasCount(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
