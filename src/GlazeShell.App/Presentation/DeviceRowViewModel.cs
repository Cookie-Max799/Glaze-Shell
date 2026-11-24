using GlazeShell.Core.SystemStatus;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Строка подключённого устройства в верхней панели: значок типа, имя и заряд.
/// Используется и в строке-индикаторе, и в раскрывающемся списке всех устройств.
/// </summary>
public sealed class DeviceRowViewModel
{
    public DeviceRowViewModel(WirelessDeviceInfo device)
    {
        Device = device;
    }

    /// <summary>Данные устройства из провайдера.</summary>
    public WirelessDeviceInfo Device { get; }

    /// <summary>Устойчивый идентификатор устройства.</summary>
    public string Id => Device.Id;

    /// <summary>Имя устройства без суффиксов профилей.</summary>
    public string Name => Device.Name;

    /// <summary>Значок типа устройства (наушники, клавиатура, мышь…).</summary>
    public string Glyph => DeviceGlyphs.For(Device.Kind);

    /// <summary>Заряд в процентах.</summary>
    public int Battery => Device.Battery;

    /// <summary>Заряд в виде «90 %» для списка устройств.</summary>
    public string BatteryText => string.Create(
        System.Globalization.CultureInfo.CurrentCulture,
        $"{Device.Battery} %");
}

/// <summary>Значки Segoe Fluent Icons для типов беспроводных устройств.</summary>
public static class DeviceGlyphs
{
    /// <summary>Наушники или гарнитура.</summary>
    public const string Headphones = "\uE7F6";

    /// <summary>Клавиатура.</summary>
    public const string Keyboard = "\uE765";

    /// <summary>Мышь.</summary>
    public const string Mouse = "\uE962";

    /// <summary>Геймпад.</summary>
    public const string Gamepad = "\uE7FC";

    /// <summary>Телефон.</summary>
    public const string Phone = "\uE8EA";

    /// <summary>Колонка.</summary>
    public const string Speaker = "\uE7F5";

    /// <summary>Неопознанное устройство.</summary>
    public const string Other = "\uE703";

    /// <summary>Свернувшаяся строка: несколько устройств сразу.</summary>
    public const string Collapsed = "\uE772";

    /// <summary>Возвращает значок для типа устройства.</summary>
    public static string For(WirelessDeviceKind kind) => kind switch
    {
        WirelessDeviceKind.Headphones => Headphones,
        WirelessDeviceKind.Keyboard => Keyboard,
        WirelessDeviceKind.Mouse => Mouse,
        WirelessDeviceKind.Gamepad => Gamepad,
        WirelessDeviceKind.Phone => Phone,
        WirelessDeviceKind.Speaker => Speaker,
        _ => Other,
    };
}
