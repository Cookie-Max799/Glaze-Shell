using System.Diagnostics;
using System.Text;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Preview;

namespace GlazeShell.Windows.FileSystem;

/// <summary>
/// Реализация <see cref="IFilePreviewReader"/> на <c>System.IO</c> (B2): текст читается с
/// ограничением по размеру и с распознаванием кодировки, каталог — со сводкой по числу
/// элементов, остальные файлы — только с метаданными (картинку, аудио и видео показывает
/// сам WinUI).
/// </summary>
/// <remarks>
/// Файл открывается на чтение с общим доступом (<see cref="FileShare.ReadWrite"/> и
/// <see cref="FileShare.Delete"/>), поэтому предпросмотр не блокирует файл, пока открыт
/// другим приложением. Провайдер best-effort: недоступный путь возвращает <c>null</c>.
/// </remarks>
public sealed class NativeFilePreviewReader : IFilePreviewReader
{
    /// <summary>Сколько байт текстового файла читается в предпросмотр.</summary>
    internal const int MaxTextBytes = 256 * 1024;

    /// <summary>Максимальная длина текста в предпросмотре: длиннее окно не показывает.</summary>
    internal const int MaxTextCharacters = 120_000;

    /// <summary>Объём первых байтов для решения «это текст или двоичный файл».</summary>
    private const int ProbeBytes = 4096;

    /// <summary>Ограничение подсчёта элементов папки: огромный каталог не должен висеть.</summary>
    internal const int MaxFolderItems = 10_000;

    public FilePreview? Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var isDirectory = Directory.Exists(path);

            if (!isDirectory && !File.Exists(path))
            {
                return null;
            }

            return isDirectory ? ReadFolder(path) : ReadFile(path);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Preview: reading '{path}' failed. {exception}");
            return null;
        }
    }

    /// <summary>Сводка папки: число элементов с ограничением, чтобы каталог на 100k не висел.</summary>
    private static FilePreview ReadFolder(string path)
    {
        var directory = new DirectoryInfo(path);
        var count = 0;
        var truncated = false;

        foreach (var info in directory.EnumerateFileSystemInfos("*", CreateOptions()))
        {
            if (count >= MaxFolderItems)
            {
                truncated = true;
                break;
            }

            count++;
        }

        return new FilePreview(
            path,
            directory.Name,
            FilePreviewKind.Folder,
            SizeBytes: null,
            new DateTimeOffset(directory.LastWriteTime),
            Content: null,
            truncated,
            count);
    }

    /// <summary>
    /// Сводка считает те же элементы, что показывает проводник: скрытые и системные
    /// пропускаются, недоступные каталоги не прерывают обход.
    /// </summary>
    private static EnumerationOptions CreateOptions() => new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    private static FilePreview ReadFile(string path)
    {
        var info = new FileInfo(path);
        var kind = FilePreview.Classify(path, isDirectory: false);
        var content = kind == FilePreviewKind.Text ? ReadText(info) : null;

        return new FilePreview(
            path,
            info.Name,
            kind,
            info.Length,
            new DateTimeOffset(info.LastWriteTime),
            content?.Text,
            content?.Truncated ?? false,
            ItemCount: null);
    }

    /// <summary>
    /// Читает начало текстового файла: определяет кодировку по BOM, отсекает «двоичный»
    /// файл (байты с нулями) и ограничивает длину. Чтение начинается заново с начала файла.
    /// </summary>
    private static (string Text, bool Truncated)? ReadText(FileInfo info)
    {
        using var stream = new FileStream(
            info.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var toRead = (int)Math.Min(stream.Length, MaxTextBytes);
        var buffer = new byte[toRead];

        var offset = 0;

        while (offset < toRead)
        {
            var read = stream.Read(buffer, offset, toRead - offset);

            if (read <= 0)
            {
                break;
            }

            offset += read;
        }

        if (!LooksLikeText(buffer, offset, out var bomLength))
        {
            return null;
        }

        var encoding = DetectEncoding(buffer, bomLength);
        var text = encoding.GetString(buffer, bomLength, offset - bomLength);
        var truncated = stream.Length > toRead;

        if (text.Length > MaxTextCharacters)
        {
            text = text[..MaxTextCharacters];
            truncated = true;
        }

        return (text, truncated);
    }

    /// <summary>
    /// Текст ли это: BOM UTF-8/UTF-16 однозначно говорит «да», нулевой байт без BOM — «нет»
    /// (двоичный файл с текстовым расширением). Кодировки без BOM (например UTF-16 без
    /// метки) распознаются как двоичные — это ограничение базового слайса.
    /// </summary>
    private static bool LooksLikeText(byte[] buffer, int length, out int bomLength)
    {
        bomLength = 0;

        if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            bomLength = 3;
            return true;
        }

        if (length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            bomLength = 2;
            return true;
        }

        if (length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            bomLength = 2;
            return true;
        }

        var probe = Math.Min(length, ProbeBytes);

        for (var index = 0; index < probe; index++)
        {
            if (buffer[index] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Кодировка по BOM: три байта — UTF-8, FF FE — UTF-16 LE, FE FF — UTF-16 BE.</summary>
    private static Encoding DetectEncoding(byte[] buffer, int bomLength) => bomLength switch
    {
        3 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        2 when buffer[0] == 0xFE => Encoding.BigEndianUnicode,
        2 => Encoding.Unicode,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };
}
