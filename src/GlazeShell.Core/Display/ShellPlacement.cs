namespace GlazeShell.Core.Display;

/// <summary>
/// К какому краю монитора прижимается панель оболочки (функция I6).
/// </summary>
public enum ShellAnchor
{
    /// <summary>Верхний край монитора.</summary>
    Top = 0,

    /// <summary>Нижний край монитора.</summary>
    Bottom = 1,
}

/// <summary>
/// Как панель оболочки выравнивается по горизонтали (функция I6).
/// </summary>
public enum ShellAlignment
{
    /// <summary>По центру монитора.</summary>
    Center = 0,

    /// <summary>У левого края монитора.</summary>
    Left = 1,

    /// <summary>У правого края монитора.</summary>
    Right = 2,

    /// <summary>
    /// Во всю ширину монитора за вычетом боковых отступов. Ширина панели в этом режиме
    /// задаётся монитором, а не панелью.
    /// </summary>
    Stretch = 3,
}

/// <summary>
/// Положение панели оболочки на мониторе в физических пикселях (функция I6).
/// </summary>
/// <remarks>
/// Панели оболочки позиционируются в пикселях: WinUI работает в логических единицах, но
/// <c>AppWindow.MoveAndResize</c> принимает пиксели, а мониторы со смешанным масштабированием
/// требуют пересчёта по DPI конкретного монитора. Правило пересчёта живёт в
/// <see cref="ShellPlacementResolver"/>, поэтому его можно проверить без Windows.
/// </remarks>
/// <param name="X">Левая граница панели в пикселях от левого края виртуального экрана.</param>
/// <param name="Y">Верхняя граница панели в пикселях от верхнего края виртуального экрана.</param>
/// <param name="Width">Ширина панели в пикселях.</param>
/// <param name="Height">Высота панели в пикселях.</param>
public sealed record ShellPlacement(int X, int Y, int Width, int Height)
{
    /// <summary>Правая граница панели в пикселях.</summary>
    public int Right => X + Width;

    /// <summary>Нижняя граница панели в пикселях.</summary>
    public int Bottom => Y + Height;
}
