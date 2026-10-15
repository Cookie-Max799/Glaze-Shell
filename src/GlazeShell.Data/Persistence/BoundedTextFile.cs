using System.Buffers;
using System.Text;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Чтение текстового файла с ограничением размера.
/// </summary>
/// <remarks>
/// Проверка размера и чтение выполняются через один открытый дескриптор, поэтому подмена
/// файла между проверкой и чтением не обходит лимит: дескриптор продолжает указывать
/// на прежний файл. Число прочитанных байт дополнительно ограничено лимитом, поэтому
/// файл, выросший после проверки, не исчерпает память.
/// </remarks>
internal static class BoundedTextFile
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Читает файл не более чем в <paramref name="maxBytes"/> байт.
    /// Возвращает <c>false</c>, если размер превышает лимит; в этом случае
    /// <paramref name="byteLength"/> содержит фактический размер файла.
    /// Исключения файловой системы пробрасываются вызывающему коду.
    /// </summary>
    public static bool TryRead(string path, long maxBytes, out string text, out long byteLength)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.SequentialScan);

        byteLength = stream.Length;
        if (byteLength > maxBytes || byteLength > int.MaxValue)
        {
            text = string.Empty;
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent((int)byteLength);
        try
        {
            var length = (int)byteLength;
            var offset = 0;

            while (offset < length)
            {
                var read = stream.Read(buffer, offset, length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            using var content = new MemoryStream(buffer, 0, offset, writable: false);
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
