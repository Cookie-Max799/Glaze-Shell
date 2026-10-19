using GlazeShell.Windows.Audio;
using GlazeShell.Windows.Interop;

namespace GlazeShell.Windows.Tests.Audio;

[TestClass]
public sealed class SystemVolumeProviderTests
{
    /// <summary>
    /// Проверка COM-интеропов Core Audio: вызов идёт на выделенном STA-потоке, потому что
    /// объекты <c>MMDeviceEnumerator</c> создаются и освобождаются внутри одной квартиры.
    /// Устройства вывода может не быть (сборочный агент без звуковой карты) — тогда проверяется
    /// отсутствие исключения, а при наличии устройства ещё и диапазон 0–100.
    /// </summary>
    [TestMethod]
    public void ReadingVolumeIsSafeAndWithinRange()
    {
        var (read, percent) = ComApartment.Run(() =>
        {
            using var volume = new SystemVolumeProvider();
            var succeeded = volume.TryGetVolume(out var value, out _);
            return (succeeded, value);
        });

        Assert.IsFalse(read && percent is < 0 or > 100);
    }
}
