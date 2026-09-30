namespace GlazeShell.Data.Serialization;

/// <summary>
/// Имена файлов пользовательских данных. Источник значений — только код:
/// пути никогда не приходят из документов или IPC.
/// </summary>
public static class UserDataFileNames
{
    public const string Configuration = "config.json";
    public const string Layout = "layout.json";
    public const string Settings = "settings.json";

    /// <summary>
    /// Каталог пользовательских тем внутри корневого каталога данных.
    /// Темы наполняет пользователь, поэтому программа только читает их.
    /// </summary>
    public const string ThemesDirectory = "themes";

    public const string ThemeFileExtension = ".json";

    public const string BackupsDirectory = "backups";
    public const string RecoveryDirectory = "recovery";

    /// <summary>
    /// Максимальное количество сохраняемых резервных копий одного документа.
    /// </summary>
    public const int MaxRetainedBackups = 3;

    /// <summary>
    /// Максимальное количество изолированных повреждённых документов одного типа.
    /// </summary>
    public const int MaxRetainedRecoveries = 3;
}
