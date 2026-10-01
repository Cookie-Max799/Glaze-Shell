using GlazeShell.Windows.DisplayManagement;
using GlazeShell.Windows.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Windows.Tests.DisplayManagement;

/// <summary>
/// Проверка отбора оконных сообщений, приводящих к перечислению мониторов.
/// </summary>
/// <remarks>
/// Тесты не рассылают сообщения по системе: рассылка привела бы к зависимости от
/// сообщений, отправляемых параллельными тестами, и результат был бы недетерминированным.
/// </remarks>
[TestClass]
public sealed class DisplaySignalTests
{
    [TestMethod]
    public void DisplayChangeIsADisplaySignal() =>
        Assert.IsTrue(DisplaySignal.IsDisplaySignal(User32.WmDisplayChange, 0));

    [TestMethod]
    public void DpiChangedIsADisplaySignal() =>
        Assert.IsTrue(DisplaySignal.IsDisplaySignal(User32.WmDpiChanged, 0));

    [TestMethod]
    public void DeviceNodesChangedIsADisplaySignal() =>
        Assert.IsTrue(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, (nint)User32.DevNodesChanged));

    [TestMethod]
    public void WorkAreaChangeIsADisplaySignal() =>
        Assert.IsTrue(DisplaySignal.IsDisplaySignal(User32.WmSettingChange, (nint)User32.SpiSetWorkArea));

    [TestMethod]
    public void LogicalDpiOverrideIsADisplaySignal() =>
        Assert.IsTrue(DisplaySignal.IsDisplaySignal(User32.WmSettingChange, (nint)User32.SpiSetLogicalDpiOverride));

    [TestMethod]
    public void OtherDeviceChangesAreNotDisplaySignals()
    {
        // DBT_REMOUSECHANGE, DBT_KEYDEVRANGE, DBT_DEVICEARRIVAL и DBT_DEVICEREMOVECOMPLETE
        // не меняют конфигурацию дисплеев.
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, 0x0002));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, 0x0007 + 1));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, 0x8000));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, 0x8004));
    }

    [TestMethod]
    public void OtherSettingChangesAreNotDisplaySignals()
    {
        // SPI_SETWORKAREA и SPI_SETLOGICALDPIOVERRIDE — единственные настройки этой группы,
        // которые меняют геометрию или масштаб монитора.
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmSettingChange, 0x0002));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmSettingChange, 0x000B));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmSettingChange, 0x0014));
    }

    [TestMethod]
    public void UnrelatedMessagesAreNotDisplaySignals()
    {
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmClose, 0));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmQuit, 0));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(0, 0));
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(0xFFFF, unchecked((nint)0xFFFFFFFF)));
    }

    [TestMethod]
    public void ZeroDeviceChangeIsNotADisplaySignal() =>
        Assert.IsFalse(DisplaySignal.IsDisplaySignal(User32.WmDeviceChange, 0));
}
