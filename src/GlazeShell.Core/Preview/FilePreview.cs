using System.IO;

namespace GlazeShell.Core.Preview;

/// <summary>
/// Содержимое предпросмотра файла (B2): вид, метаданные и, для текста, сам текст.
/// Модель не содержит UI-типов, поэтому читается и тестируется без WinUI.
/// </summary>
public sealed record FilePreview(
    string FullPath,
    string Title,
    FilePreviewKind Kind,
    long? SizeBytes,
    DateTimeOffset? Modified,
    string? Content,
    bool IsTruncated,
    int? ItemCount)
{
    /// <summary>
    /// Определяет вид файла по расширению (и по признаку каталога) без обращения к диску.
    /// Неизвестное расширение даёт <see cref="FilePreviewKind.Other"/>: карточка покажет
    /// метаданные и кнопку открытия вместо пустой ошибки.
    /// </summary>
    public static FilePreviewKind Classify(string? name, bool isDirectory)
    {
        if (isDirectory)
        {
            return FilePreviewKind.Folder;
        }

        var extension = Path.GetExtension(name ?? string.Empty).TrimStart('.').ToLowerInvariant();

        if (extension.Length == 0)
        {
            return FilePreviewKind.Other;
        }

        if (ImageExtensions.Contains(extension))
        {
            return FilePreviewKind.Image;
        }

        if (TextExtensions.Contains(extension))
        {
            return FilePreviewKind.Text;
        }

        if (AudioExtensions.Contains(extension))
        {
            return FilePreviewKind.Audio;
        }

        if (VideoExtensions.Contains(extension))
        {
            return FilePreviewKind.Video;
        }

        if (ArchiveExtensions.Contains(extension))
        {
            return FilePreviewKind.Archive;
        }

        return FilePreviewKind.Other;
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "bmp", "ico", "tif", "tiff", "webp", "svg", "heic", "jfif",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "log", "md", "markdown", "json", "xml", "yml", "yaml", "ini", "cfg", "conf", "csv",
        "cs", "c", "cpp", "h", "hpp", "py", "js", "jsx", "ts", "tsx", "html", "htm", "css", "scss",
        "ps1", "bat", "cmd", "sh", "sql", "xaml", "csproj", "sln", "props", "targets", "gitignore",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "m4a", "ogg", "wma", "aac", "opus",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "wmv", "webm", "m4v", "mpg", "mpeg",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "zip", "rar", "7z", "tar", "gz", "bz2", "cab",
    };
}
