namespace GlazeShell.Core.Recovery;

/// <summary>
/// Модификаторы системной горячей клавиши восстановления (I2).
/// </summary>
/// <remarks>
/// Значения совпадают с константами модификаторов <c>RegisterHotKey</c>, поэтому слой Windows
/// переводит их напрямую, без таблицы соответствия.
/// </remarks>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>Без модификаторов.</summary>
    None = 0,

    /// <summary>Alt.</summary>
    Alt = 0x0001,

    /// <summary>Ctrl.</summary>
    Control = 0x0002,

    /// <summary>Shift.</summary>
    Shift = 0x0004,

    /// <summary>Win.</summary>
    Windows = 0x0008,
}
