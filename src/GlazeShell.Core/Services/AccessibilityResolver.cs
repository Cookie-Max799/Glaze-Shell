using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

/// <summary>
/// Итоговое состояние доступности (функция I5): система и настройки пользователя складываются,
/// поэтому поведение одинаково при «уменьшить движение» в Windows и при включённой высокой
/// контрастности.
/// </summary>
/// <param name="ReduceMotion">Нужно ли убрать или сократить анимации.</param>
/// <param name="HighContrast">Нужно ли заменить стекло и приглушённые цвета на контрастные.</param>
public readonly record struct AccessibilityState(bool ReduceMotion, bool HighContrast)
{
    /// <summary>Состояние по умолчанию: движения разрешены, контраст обычный.</summary>
    public static AccessibilityState Default => new(false, false);
}

/// <summary>
/// Правила доступности (I5): складывает системные возможности Windows и настройки пользователя.
/// Правила живут в ядре без WinUI, поэтому проверяются обычными юнит-тестами, а интерфейс
/// только применяет готовый результат.
/// </summary>
public static class AccessibilityResolver
{
    /// <summary>
    /// Собирает итоговое состояние доступности.
    /// </summary>
    /// <param name="settings">Настройки пользователя.</param>
    /// <param name="systemHighContrast">Включена ли в Windows высокая контрастность.</param>
    /// <param name="systemAnimationsEnabled">
    /// Разрешены ли в Windows анимации (системная настройка «Показывать анимации в Windows»).
    /// </param>
    public static AccessibilityState Resolve(
        UserSettings settings,
        bool systemHighContrast,
        bool systemAnimationsEnabled)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Системные ограничения сильнее пользовательских: выключенные в Windows анимации и
        // включённая контрастность остаются в силе, даже если пользователь не просил их.
        var reduceMotion = settings.ReduceMotion || !systemAnimationsEnabled;
        var highContrast = settings.HighContrast || systemHighContrast;

        return new AccessibilityState(reduceMotion, highContrast);
    }
}
