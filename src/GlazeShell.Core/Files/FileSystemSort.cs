namespace GlazeShell.Core.Files;

/// <summary>Колонка сортировки списка Explorer (E1).</summary>
public enum FileSystemSortColumn
{
    Name = 0,
    Kind = 1,
    Size = 2,
    Modified = 3
}

/// <summary>
/// Направление сортировки. При «папки сверху» оно меняет порядок только внутри группы:
/// папки не уезжают под файлы, как и в Finder.
/// </summary>
public enum FileSystemSortDirection
{
    Ascending = 0,
    Descending = 1
}

/// <summary>
/// Сортировка элементов каталога (E1). Папки всегда идут первыми (как в Finder и в
/// проводнике Windows), направление применяется внутри каждой группы. Сортировка устойчива:
/// элементы с равным ключом сохраняют порядок, в котором их отдала файловая система.
/// </summary>
public static class FileSystemSorter
{
    public static IReadOnlyList<FileSystemEntry> Sort(
        IReadOnlyList<FileSystemEntry> entries,
        FileSystemSortColumn column,
        FileSystemSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = column switch
        {
            FileSystemSortColumn.Kind => Order(entries, static entry => entry.Extension, StringComparer.OrdinalIgnoreCase, direction),
            FileSystemSortColumn.Size => Order(entries, static entry => entry.Size ?? -1L, Comparer<long>.Default, direction),
            FileSystemSortColumn.Modified => Order(entries, static entry => entry.Modified ?? DateTimeOffset.MinValue, Comparer<DateTimeOffset>.Default, direction),
            _ => Order(entries, static entry => entry.Name, StringComparer.OrdinalIgnoreCase, direction),
        };

        var sorted = ordered.ToList();
        var result = new List<FileSystemEntry>(sorted.Count);

        // Два прохода вместо одного: так папки стоят перед файлами независимо от ключа
        // и направления, а порядок внутри каждой группы остаётся отсортированным.
        foreach (var entry in sorted)
        {
            if (entry.IsFolder)
            {
                result.Add(entry);
            }
        }

        foreach (var entry in sorted)
        {
            if (!entry.IsFolder)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static IEnumerable<FileSystemEntry> Order<TKey>(
        IEnumerable<FileSystemEntry> entries,
        Func<FileSystemEntry, TKey> key,
        IComparer<TKey> comparer,
        FileSystemSortDirection direction) =>
        direction == FileSystemSortDirection.Descending
            ? entries.OrderByDescending(key, comparer)
            : entries.OrderBy(key, comparer);
}
