using System.Text.Json;
using GlazeShell.Core.Models;
using GlazeShell.Data.Serialization;

namespace GlazeShell.Data.Themes;

/// <summary>
/// Чтение пользовательских тем из каталога <c>themes</c>.
/// </summary>
/// <remarks>
/// Тема — это данные, написанные пользователем, поэтому каталог рассматривается как
/// недоверенный вход.
/// <list type="bullet">
/// <item>Читаются только <c>*.json</c> верхнего уровня: подкаталоги и симлинки не обходятся.</item>
/// <item>Размер файла проверяется до чтения, разбор ограничен по глубине и не допускает
/// комментариев, лишних запятых и полей типа <c>object</c>.</item>
/// <item>Исполняемые ассеты и пути вне каталога темы отклоняет модель
/// <see cref="Theme"/>: тема не может ни запустить код, ни указать файл за пределами себя.</item>
/// <item>Непригодная тема отбрасывается по отдельности: пользовательский набор тем
/// не восстанавливается и не перезаписывается — это данные пользователя, а не документ программы.</item>
/// </list>
/// </remarks>
public sealed class ThemeStore
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = PersistenceLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private readonly string _themesDirectory;

    public ThemeStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException("The user data directory must be an absolute path.", nameof(rootDirectory));
        }

        _themesDirectory = Path.Combine(Path.GetFullPath(rootDirectory), UserDataFileNames.ThemesDirectory);
    }

    /// <summary>
    /// Читает все темы каталога. Отсутствие каталога — не ошибка:
    /// приложение работает на встроенной теме по умолчанию.
    /// </summary>
    public ThemeLoadResult LoadThemes()
    {
        var diagnostics = new List<string>();
        var themes = new List<Theme>();
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_themesDirectory))
        {
            return new ThemeLoadResult(themes, [$"The theme directory '{_themesDirectory}' does not exist, the built-in theme is used."]);
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(_themesDirectory, "*" + UserDataFileNames.ThemeFileExtension, SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new ThemeLoadResult(themes, [$"The theme directory '{_themesDirectory}' cannot be read: {exception.Message}"]);
        }

        // Порядок файлов на диске не задан, поэтому темы сортируются по имени файла:
        // иначе диагностика и выбор первой темы зависели бы от файловой системы.
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        if (files.Length > PersistenceLimits.MaxThemes)
        {
            diagnostics.Add(
                $"The theme directory contains {files.Length} files, more than the limit of {PersistenceLimits.MaxThemes}; the extra files were not read.");
        }

        foreach (var path in files.Take(PersistenceLimits.MaxThemes))
        {
            var fileName = Path.GetFileName(path);
            if (ReadTheme(path, fileName, themes, knownIds, diagnostics) == false)
            {
                continue;
            }
        }

        return new ThemeLoadResult(themes, diagnostics);
    }

    private static bool ReadTheme(
        string path,
        string fileName,
        List<Theme> themes,
        HashSet<string> knownIds,
        List<string> diagnostics)
    {
        string content;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > PersistenceLimits.MaxThemeBytes)
            {
                diagnostics.Add($"The theme file '{fileName}' is {info.Length} bytes, the limit is {PersistenceLimits.MaxThemeBytes} bytes; the theme was skipped.");
                return false;
            }

            content = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            diagnostics.Add($"The theme file '{fileName}' cannot be read: {exception.Message}");
            return false;
        }

        ThemeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ThemeDocument>(content, GlazeJson.Document);
        }
        catch (JsonException exception)
        {
            diagnostics.Add($"The theme file '{fileName}' is not valid JSON: {exception.Message}");
            return false;
        }

        if (!ThemeDocumentMapper.TryMap(document, fileName, out var theme, out var mappingDiagnostics))
        {
            diagnostics.AddRange(mappingDiagnostics.Count > 0
                ? mappingDiagnostics
                : [$"The theme file '{fileName}' does not match the expected structure."]);
            return false;
        }

        diagnostics.AddRange(mappingDiagnostics);

        if (theme is null)
        {
            return false;
        }

        if (!knownIds.Add(theme.Id))
        {
            // Идентификаторы сравниваются без учёта регистра, чтобы 'Midnight' и 'midnight'
            // не оказались двумя разными темами под одним именем в настройках.
            diagnostics.Add($"The theme '{theme.Id}' from '{fileName}' duplicates the identifier of an already loaded theme; the file was skipped.");
            return false;
        }

        themes.Add(theme);
        return true;
    }
}
