namespace GlazeShell.Core.Files;

/// <summary>Тип элемента каталога, который показывает Explorer (E1).</summary>
public enum FileSystemEntryKind
{
    Folder = 0,
    File = 1
}

/// <summary>
/// Элемент каталога для Explorer (E1): имя, полный путь, тип, размер и время изменения.
/// Размер папки не читается — в Windows он не известен без обхода всего дерева, — поэтому
/// у папки он равен <c>null</c>. Модель нейтральна: ни Win32, ни UI в неё не проникают,
/// поэтому список сортируется и фильтруется без WinUI.
/// </summary>
public sealed record FileSystemEntry(
    string Name,
    string FullPath,
    FileSystemEntryKind Kind,
    long? Size,
    DateTimeOffset? Modified)
{
    /// <summary>Папка ли это: по папке можно перейти глубже.</summary>
    public bool IsFolder => Kind == FileSystemEntryKind.Folder;

    /// <summary>
    /// Расширение файла вместе с точкой в нижнем регистре (например, <c>.txt</c>); у папки —
    /// пустая строка. Нужно для колонки «Тип» и сортировки по нему.
    /// </summary>
    public string Extension =>
        Kind == FileSystemEntryKind.File ? Path.GetExtension(Name).ToLowerInvariant() : string.Empty;
}
