using GlazeShell.Core.Files;

namespace GlazeShell.Core.Interfaces;

/// <summary>
/// Чтение файловой системы для Explorer (E1/E2): содержимое каталога, логические диски и
/// системные папки сайдбара. Реализация живёт в <c>GlazeShell.Windows</c>, поэтому UI не
/// вызывает Win32 напрямую и не знает про <see cref="Environment.SpecialFolder"/>.
/// </summary>
/// <remarks>
/// Все методы best-effort: недоступный или исчезнувший элемент не выпускает исключение
/// наружу, а пропускается. Ошибка чтения одного каталога не должна ломать навигацию.
/// </remarks>
public interface IFileSystemBrowser
{
    /// <summary>Каталог существует и доступен для чтения.</summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Элементы каталога или пустой список, если каталог не существует или чтение запрещено.
    /// Скрытые и системные элементы пропускаются, как в проводнике Windows.
    /// </summary>
    IReadOnlyList<FileSystemEntry> GetEntries(string path);

    /// <summary>Логические диски машины, включая неготовые приводы.</summary>
    IReadOnlyList<DriveEntry> GetDrives();

    /// <summary>Системные папки для секции «Избранное» сайдбара.</summary>
    IReadOnlyList<SystemFolderLocation> GetSystemFolders();
}
