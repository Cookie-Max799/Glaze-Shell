using GlazeShell.Core.Preview;

namespace GlazeShell.Core.Interfaces;

/// <summary>
/// Читает содержимое предпросмотра файла (B2): текст — с ограничением по размеру,
/// каталог — со сводкой, остальное — с метаданными. Реализация best-effort:
/// недоступный путь возвращает <c>null</c>, а не исключение.
/// </summary>
public interface IFilePreviewReader
{
    /// <summary>Читает предпросмотр; <c>null</c>, если путь пустой или недоступен.</summary>
    FilePreview? Read(string path);
}
