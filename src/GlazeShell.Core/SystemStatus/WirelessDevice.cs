namespace GlazeShell.Core.SystemStatus;

/// <summary>
/// Тип беспроводного устройства. Определяет значок в верхней панели: наушники,
/// клавиатура, мышь, геймпад, телефон, колонка или «прочее».
/// </summary>
public enum WirelessDeviceKind
{
    /// <summary>Тип определить не удалось: показывается общий значок устройств.</summary>
    Other,

    /// <summary>Наушники или гарнитура.</summary>
    Headphones,

    /// <summary>Клавиатура.</summary>
    Keyboard,

    /// <summary>Мышь или трекпад.</summary>
    Mouse,

    /// <summary>Геймпад или джойстик.</summary>
    Gamepad,

    /// <summary>Телефон.</summary>
    Phone,

    /// <summary>Колонка или звуковая панель.</summary>
    Speaker,
}

/// <summary>
/// Подключённое беспроводное устройство с известным зарядом.
/// </summary>
/// <param name="Id">Устойчивый идентификатор (GUID контейнера устройства PnP).</param>
/// <param name="Name">Понятное имя устройства без служебных суффиксов профилей.</param>
/// <param name="Battery">Заряд в процентах, 0–100.</param>
/// <param name="Kind">Тип устройства для подбора значка.</param>
public sealed record WirelessDeviceInfo(string Id, string Name, int Battery, WirelessDeviceKind Kind);

/// <summary>
/// Определяет тип беспроводного устройства по его имени и служебным профилям.
/// </summary>
/// <remarks>
/// Служебные профили Bluetooth надёжнее имени: у наушников имя часто — просто модель
/// («Ear (2)», «WH-1000XM5»), и только профиль аудио выдаёт, что это именно наушники.
/// Имя помогает там, где профилей нет или они служебные: «Logitech MX Master» — мышь.
/// </remarks>
public static class WirelessDeviceClassifier
{
    /// <summary>
    /// Профили Bluetooth, по которым устройство считается аудио. Идентификаторы
    /// перечислены в Bluetooth Assigned Numbers: A2DP Sink, Handsfree и Headset.
    /// </summary>
    private static readonly string[] AudioProfiles = ["0000110b", "0000111e", "00001108"];

    /// <summary>
    /// Определяет тип устройства.
    /// </summary>
    /// <param name="name">Имя устройства как его показывает Windows.</param>
    /// <param name="instanceIds">
    /// Идентификаторы узлов PnP устройства: по ним видны профили Bluetooth.
    /// </param>
    /// <param name="deviceClasses">
    /// Классы PnP всех узлов устройства («Mouse», «Keyboard», «MEDIA»…). Драйвер ставит
    /// их точнее имени: мышь «Logitech MX Master 3» по имени не отличить от колонки,
    /// а класс <c>Mouse</c> в её контейнере однозначен.
    /// </param>
    public static WirelessDeviceKind Classify(
        string? name,
        IReadOnlyList<string>? instanceIds,
        IReadOnlyList<string>? deviceClasses = null)
    {
        // Класс драйвера — самый надёжный признак: он не зависит от того, как назвал
        // устройство производитель.
        if (HasClass(deviceClasses, "Mouse"))
        {
            return WirelessDeviceKind.Mouse;
        }

        if (HasClass(deviceClasses, "Keyboard"))
        {
            return WirelessDeviceKind.Keyboard;
        }

        var hasAudioProfile = instanceIds is not null &&
            instanceIds.Any(id => AudioProfiles.Any(profile =>
                id.Contains(profile, StringComparison.OrdinalIgnoreCase)));

        var hasAudioClass = HasClass(deviceClasses, "MEDIA") || HasClass(deviceClasses, "AudioEndpoint");

        if (hasAudioProfile || hasAudioClass)
        {
            return Contains(name, "колонк", "speaker", "динамик", "сабвуфер", "soundbar")
                ? WirelessDeviceKind.Speaker
                : WirelessDeviceKind.Headphones;
        }

        if (Contains(
            name,
            "мышь",
            "mouse",
            "mice",
            "трекпад",
            "trackpad",
            "mx master",
            "mx anywhere",
            "magic mouse"))
        {
            return WirelessDeviceKind.Mouse;
        }

        if (Contains(name, "клавиатур", "keyboard", "клавиа"))
        {
            return WirelessDeviceKind.Keyboard;
        }

        if (Contains(name, "геймпад", "gamepad", "controller", "джойст", "joystick", "dualsense", "dualshock"))
        {
            return WirelessDeviceKind.Gamepad;
        }

        if (Contains(name, "телефон", "phone", "смартфон"))
        {
            return WirelessDeviceKind.Phone;
        }

        if (Contains(name, "наушник", "headphone", "headset", "гарнитур", "earbuds"))
        {
            return WirelessDeviceKind.Headphones;
        }

        return WirelessDeviceKind.Other;
    }

    private static bool HasClass(IReadOnlyList<string>? classes, string expected) =>
        classes is not null && classes.Any(value =>
            string.Equals(value, expected, StringComparison.OrdinalIgnoreCase));

    private static bool Contains(string? haystack, params string[] needles) =>
        !string.IsNullOrWhiteSpace(haystack) &&
        needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
