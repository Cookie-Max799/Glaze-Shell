namespace GlazeShell.Core.Configuration;

/// <summary>
/// Правила имени каталога пользовательских данных.
/// Вынесены в Core, чтобы конфигурация, filesystem-пути и слой хранения
/// использовали одну проверку и не расходились.
/// </summary>
public static class UserDataDirectoryName
{
    public const int MaxLength = 64;

    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// Проверяет имя каталога и возвращает его без изменений.
    /// </summary>
    /// <exception cref="ArgumentException">Имя недопустимо.</exception>
    public static string Validate(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            directoryName is "." or ".." ||
            directoryName.Length > MaxLength ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The data directory name is invalid.", nameof(directoryName));
        }

        if (directoryName.EndsWith('.') || directoryName.EndsWith(' '))
        {
            throw new ArgumentException("The data directory name cannot end with a dot or a space.", nameof(directoryName));
        }

        var deviceStem = directoryName.Split('.')[0];
        if (ReservedDeviceNames.Contains(deviceStem, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The data directory name is a reserved device name.", nameof(directoryName));
        }

        return directoryName;
    }

    /// <summary>
    /// Не бросает исключение: используется при загрузке сохранённой конфигурации,
    /// где некорректное значение означает recovery, а не ошибку программы.
    /// </summary>
    public static bool IsValid(string? directoryName)
    {
        try
        {
            return directoryName is not null && Validate(directoryName) is not null;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
