using System.Text;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Файловый слой каталога пользовательских данных.
/// </summary>
/// <remarks>
/// Свойства слоя:
/// запись атомарна (временный файл рядом с целью и переименование), поэтому оборванная запись
/// не оставляет частичный JSON; чтение ограничено по размеру до разбора; повреждённый документ
/// изолируется перемещением, а не удаляется; перед миграцией создаётся резервная копия.
/// </remarks>
internal sealed class UserDataDirectory
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public UserDataDirectory(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("The user data directory is required.", nameof(rootDirectory));
        }

        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException("The user data directory must be an absolute path.", nameof(rootDirectory));
        }

        RootDirectory = TrimTrailingSeparators(Path.GetFullPath(rootDirectory));
    }

    public string RootDirectory { get; }

    /// <summary>
    /// Абсолютный путь документа. Проверяется, что результат остаётся внутри корневого каталога.
    /// </summary>
    public string GetDocumentPath(string fileName)
    {
        ValidateFileName(fileName);

        var path = Path.GetFullPath(Path.Combine(RootDirectory, fileName));
        if (!path.StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new PersistenceException($"The document path escapes the user data directory: {fileName}.");
        }

        return path;
    }

    public DocumentReadResult ReadDocument(string fileName, long maxBytes)
    {
        var path = GetDocumentPath(fileName);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return DocumentReadResult.Missing();
            }

            if (info.Length > maxBytes)
            {
                return DocumentReadResult.TooLarge(
                    $"The document '{fileName}' is {info.Length} bytes, the limit is {maxBytes} bytes.");
            }

            return DocumentReadResult.Loaded(File.ReadAllText(path, Utf8WithoutBom));
        }
        catch (FileNotFoundException) when (!File.Exists(path))
        {
            return DocumentReadResult.Missing();
        }
        catch (DirectoryNotFoundException)
        {
            return DocumentReadResult.Missing();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return DocumentReadResult.Unreadable($"The document '{fileName}' cannot be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Пишет документ атомарно. Возвращает фактический размер записанного содержимого.
    /// </summary>
    public void WriteDocument(string fileName, string content, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(content);

        var byteCount = Encoding.UTF8.GetByteCount(content);
        if (byteCount > maxBytes)
        {
            throw new PersistenceLimitExceededException(
                $"The document '{fileName}' is {byteCount} bytes, the limit is {maxBytes} bytes.");
        }

        var path = GetDocumentPath(fileName);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new PersistenceException($"The path of '{fileName}' has no directory.");
        }

        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var payload = Utf8WithoutBom.GetBytes(content);

            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            TryDelete(temporaryPath);
            throw new PersistenceException($"The document '{fileName}' cannot be written: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Проверяет, что документ существует. Используется, чтобы отличить «нечего копировать»
    /// от «копирование не удалось»: во втором случае документ нельзя перезаписывать.
    /// </summary>
    public bool Exists(string fileName) => File.Exists(GetDocumentPath(fileName));

    /// <summary>
    /// Создаёт резервную копию документа. Возвращает путь к копии либо <c>null</c>,
    /// если копировать нечего или копирование не удалось.
    /// </summary>
    public string? CreateBackup(string fileName)
    {
        var source = GetDocumentPath(fileName);
        if (!File.Exists(source))
        {
            return null;
        }

        var directory = Path.Combine(RootDirectory, UserDataFileNames.BackupsDirectory);

        try
        {
            Directory.CreateDirectory(directory);
            var backupPath = Path.Combine(directory, $"{fileName}.{Timestamp()}.bak");
            File.Copy(source, backupPath, overwrite: true);
            Prune(directory, $"{fileName}.", UserDataFileNames.MaxRetainedBackups);
            return backupPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// Изолирует повреждённый документ: перемещает его в каталог recovery.
    /// Возвращает путь к изолированному файлу либо <c>null</c>, если перемещение не удалось.
    /// </summary>
    public string? QuarantineDocument(string fileName)
    {
        var source = GetDocumentPath(fileName);
        if (!File.Exists(source))
        {
            return null;
        }

        var directory = Path.Combine(RootDirectory, UserDataFileNames.RecoveryDirectory);

        try
        {
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, $"{fileName}.{Timestamp()}.corrupt.json");
            File.Move(source, target, overwrite: true);
            Prune(directory, $"{fileName}.", UserDataFileNames.MaxRetainedRecoveries);
            return target;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("The document file name is required.", nameof(fileName));
        }

        if (fileName.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new PersistenceException($"The document file name is not valid: {fileName}.");
        }
    }

    private static string Timestamp() => DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

    private static void Prune(string directory, string prefix, int maxRetained)
    {
        foreach (var obsolete in new DirectoryInfo(directory)
                     .GetFiles(prefix + "*")
                     .OrderByDescending(static file => file.Name)
                     .Skip(maxRetained))
        {
            TryDelete(obsolete.FullName);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
        }
    }

    private static string TrimTrailingSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) is { Length: > 0 } trimmed
            ? trimmed
            : Path.GetPathRoot(path)!;
}
