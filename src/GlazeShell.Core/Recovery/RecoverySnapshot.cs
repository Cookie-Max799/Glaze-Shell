namespace GlazeShell.Core.Recovery;

/// <summary>
/// Состояние восстановления, сохранённое между запусками (функция I2).
/// </summary>
/// <param name="SessionCompleted">
/// Завершилась ли прошлая сессия штатно. Незавершённая сессия означает падение оболочки.
/// </param>
/// <param name="Crashes">Моменты известных падений.</param>
public sealed record RecoverySnapshot(bool SessionCompleted, IReadOnlyList<DateTimeOffset> Crashes)
{
    /// <summary>Пустое состояние: падений не было, сессия завершена штатно.</summary>
    public static RecoverySnapshot Empty { get; } = new(true, []);
}
