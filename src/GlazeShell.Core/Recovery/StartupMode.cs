namespace GlazeShell.Core.Recovery;

/// <summary>
/// Режим запуска оболочки (функция I2).
/// </summary>
public enum StartupMode
{
    /// <summary>Обычный запуск: хуки и оверлеи включены.</summary>
    Normal,

    /// <summary>
    /// Безопасный режим: хуки и оверлеи отключены, оболочка ничего не меняет в системе.
    /// </summary>
    SafeMode,
}

/// <summary>
/// Решение о режиме запуска с объяснением: по чему выбран режим.
/// </summary>
/// <param name="Mode">Выбранный режим.</param>
/// <param name="Reason">Причина выбора, показывается в журнале и в интерфейсе.</param>
public readonly record struct StartupDecision(StartupMode Mode, string Reason)
{
    /// <summary>Включён ли безопасный режим.</summary>
    public bool IsSafeMode => Mode == StartupMode.SafeMode;
}

/// <summary>
/// Правило выбора режима запуска (I2). Причины проверяются по порядку: команда командной
/// строки сильнее настройки, настройка сильнее автоматики по числу падений.
/// </summary>
public static class StartupModeResolver
{
    /// <summary>
    /// Выбирает режим запуска.
    /// </summary>
    /// <param name="requestedByCommandLine">Безопасный режим запрошен ключом командной строки.</param>
    /// <param name="enabledInSettings">Безопасный режим включён в настройках.</param>
    /// <param name="repeatedCrashes">Приложение падало несколько раз подряд.</param>
    public static StartupDecision Resolve(
        bool requestedByCommandLine,
        bool enabledInSettings,
        bool repeatedCrashes)
    {
        if (requestedByCommandLine)
        {
            return new StartupDecision(
                StartupMode.SafeMode,
                "The safe mode was requested from the command line.");
        }

        if (enabledInSettings)
        {
            return new StartupDecision(
                StartupMode.SafeMode,
                "The safe mode is enabled in the settings.");
        }

        if (repeatedCrashes)
        {
            return new StartupDecision(
                StartupMode.SafeMode,
                "The application crashed several times in a row, so the safe mode was enabled.");
        }

        return new StartupDecision(StartupMode.Normal, "The usual start, no recovery reasons were found.");
    }
}
