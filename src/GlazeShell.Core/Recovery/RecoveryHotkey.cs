namespace GlazeShell.Core.Recovery;

/// <summary>
/// Комбинация клавиш восстановления интерфейса Windows (функция I2).
/// </summary>
/// <remarks>
/// <para>
/// Комбинация должна быть редкой: она вызывается только когда оболочка уже сломалась,
/// поэтому обычные сочетания вроде <c>Ctrl K</c> для неё не подходят — они заняты
/// приложениями и конфликтуют с привычками пользователя.
/// </para>
/// <para>
/// Поддерживаются только функциональные клавиши <c>F1</c>—<c>F12</c> и не менее трёх
/// модификаторов. Этого достаточно для набора редких комбинаций и достаточно мало,
/// чтобы список оставался конечным и проверяемым.
/// </para>
/// </remarks>
public sealed record RecoveryHotkey
{
    /// <summary>Виртуальный код <c>F1</c>.</summary>
    public const uint VirtualKeyF1 = 0x70;

    /// <summary>Виртуальный код <c>F12</c>.</summary>
    public const uint VirtualKeyF12 = 0x7B;

    /// <summary>Минимальное число модификаторов редкой комбинации.</summary>
    public const int MinimumModifierCount = 3;

    private static readonly (string Name, uint VirtualKey)[] FunctionKeys =
    [
        ("F1", VirtualKeyF1),
        ("F2", VirtualKeyF1 + 1),
        ("F3", VirtualKeyF1 + 2),
        ("F4", VirtualKeyF1 + 3),
        ("F5", VirtualKeyF1 + 4),
        ("F6", VirtualKeyF1 + 5),
        ("F7", VirtualKeyF1 + 6),
        ("F8", VirtualKeyF1 + 7),
        ("F9", VirtualKeyF1 + 8),
        ("F10", VirtualKeyF1 + 9),
        ("F11", VirtualKeyF1 + 10),
        ("F12", VirtualKeyF1 + 11),
    ];

    /// <param name="modifiers">Набор модификаторов.</param>
    /// <param name="virtualKey">Виртуальный код клавиши.</param>
    /// <exception cref="ArgumentException">Модификаторы не заданы или их меньше трёх.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Клавиша не поддерживается.</exception>
    public RecoveryHotkey(HotkeyModifiers modifiers, uint virtualKey)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            throw new ArgumentException("The recovery hotkey needs at least one modifier.", nameof(modifiers));
        }

        if (CountModifiers(modifiers) < MinimumModifierCount)
        {
            throw new ArgumentException(
                $"The recovery hotkey needs at least {MinimumModifierCount} modifiers to stay out of the way of other applications.",
                nameof(modifiers));
        }

        if (!IsSupportedVirtualKey(virtualKey))
        {
            throw new ArgumentOutOfRangeException(
                nameof(virtualKey),
                virtualKey,
                "Only the function keys F1 to F12 are supported as a recovery hotkey.");
        }

        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    /// <summary>Набор модификаторов.</summary>
    public HotkeyModifiers Modifiers { get; }

    /// <summary>Виртуальный код клавиши.</summary>
    public uint VirtualKey { get; }

    /// <summary>Комбинация по умолчанию: <c>Ctrl+Alt+Shift+F12</c>.</summary>
    public static RecoveryHotkey Default { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKeyF12);

    /// <summary>
    /// Готовые комбинации для настройки. Все они редкие, но различимые между собой.
    /// </summary>
    public static IReadOnlyList<RecoveryHotkey> Presets { get; } =
    [
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKeyF12),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKeyF1 + 10),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKeyF1 + 9),
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, VirtualKeyF1 + 8),
    ];

    /// <summary>
    /// Текстовое представление комбинации, например <c>Ctrl+Alt+Shift+F12</c>.
    /// Используется как идентификатор настройки и как подпись в интерфейсе.
    /// </summary>
    public string Id => BuildId();

    /// <summary>
    /// Разбирает комбинацию из текста. Разбор регистронезависимый и допускает любой порядок
    /// модификаторов. Возвращает <see langword="false"/>, если текст не задаёт редкую
    /// комбинацию из поддерживаемых клавиш.
    /// </summary>
    public static bool TryParse(string? value, out RecoveryHotkey? hotkey)
    {
        hotkey = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        uint virtualKey = 0;
        var keyFound = false;

        foreach (var part in parts)
        {
            if (TryParseModifier(part, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (keyFound || !TryParseVirtualKey(part, out virtualKey))
            {
                return false;
            }

            keyFound = true;
        }

        if (!keyFound || CountModifiers(modifiers) < MinimumModifierCount)
        {
            return false;
        }

        hotkey = new RecoveryHotkey(modifiers, virtualKey);
        return true;
    }

    /// <summary>
    /// Возвращает комбинацию по идентификатору либо значение по умолчанию, если идентификатор
    /// неизвестен или повреждён. Настройка не должна ломать запуск из-за одной строки.
    /// </summary>
    public static RecoveryHotkey ParseOrDefault(string? value) =>
        TryParse(value, out var hotkey) && hotkey is not null ? hotkey : Default;

    /// <summary>
    /// Число установленных модификаторов.
    /// </summary>
    public static int CountModifiers(HotkeyModifiers modifiers)
    {
        var count = 0;

        foreach (HotkeyModifiers modifier in new[] { HotkeyModifiers.Alt, HotkeyModifiers.Control, HotkeyModifiers.Shift, HotkeyModifiers.Windows })
        {
            if (modifiers.HasFlag(modifier))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Поддерживается ли виртуальный код клавиши.
    /// </summary>
    public static bool IsSupportedVirtualKey(uint virtualKey) =>
        virtualKey >= VirtualKeyF1 && virtualKey <= VirtualKeyF12;

    private static bool TryParseModifier(string part, out HotkeyModifiers modifier)
    {
        switch (part.ToUpperInvariant())
        {
            case "ALT":
                modifier = HotkeyModifiers.Alt;
                return true;
            case "CTRL":
            case "CONTROL":
                modifier = HotkeyModifiers.Control;
                return true;
            case "SHIFT":
                modifier = HotkeyModifiers.Shift;
                return true;
            case "WIN":
            case "WINDOWS":
                modifier = HotkeyModifiers.Windows;
                return true;
            default:
                modifier = HotkeyModifiers.None;
                return false;
        }
    }

    private static bool TryParseVirtualKey(string part, out uint virtualKey)
    {
        foreach (var (name, code) in FunctionKeys)
        {
            if (string.Equals(name, part, StringComparison.OrdinalIgnoreCase))
            {
                virtualKey = code;
                return true;
            }
        }

        virtualKey = 0;
        return false;
    }

    private string BuildId()
    {
        var parts = new List<string>(4);

        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        foreach (var (name, code) in FunctionKeys)
        {
            if (code == VirtualKey)
            {
                parts.Add(name);
                break;
            }
        }

        return string.Join('+', parts);
    }
}
