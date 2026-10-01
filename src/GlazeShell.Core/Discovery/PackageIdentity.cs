namespace GlazeShell.Core.Discovery;

/// <summary>
/// Разбирает идентификаторы MSIX-пакетов. Каталоги установки пакетов лежат в
/// <c>C:\Program Files\WindowsApps\&lt;PackageFullName&gt;</c>, а имя самого каталога
/// имеет вид <c>Name_Version_Architecture_PublisherId</c>.
/// </summary>
/// <remarks>
/// Реестровый путь установки для bundle-пакетов указывает на <c>neutral</c>-вариант,
/// тогда как на диске установлен вариант конкретной архитектуры. Поэтому пакет
/// определяется по разбору имени каталога из пути процесса, а не по каталогу из реестра:
/// имя пакета и publisher id в обоих вариантах совпадают.
/// </remarks>
public static class PackageIdentity
{
    private const string WindowsAppsSegment = "\\WindowsApps\\";

    /// <summary>
    /// Извлекает имя семейства пакета (<c>Name_PublisherId</c>) из полного имени
    /// <c>Name_Version_Architecture_PublisherId</c>.
    /// </summary>
    public static bool TryGetPackageFamilyName(string packageFullName, out string packageFamilyName)
    {
        packageFamilyName = string.Empty;

        if (string.IsNullOrWhiteSpace(packageFullName))
        {
            return false;
        }

        var value = packageFullName.Trim();
        var first = value.IndexOf('_');
        var last = value.LastIndexOf('_');

        if (first <= 0 || last <= first)
        {
            return false;
        }

        var publisherId = value[(last + 1)..];

        if (publisherId.Length == 0)
        {
            return false;
        }

        packageFamilyName = string.Concat(value[..first], "_", publisherId);
        return true;
    }

    /// <summary>
    /// Разбирает имя семейства пакета из пути процесса внутри
    /// <c>C:\Program Files\WindowsApps</c>. Для любых других путей возвращает <c>false</c>.
    /// </summary>
    public static bool TryGetPackageFamilyNameFromPath(string? processPath, out string packageFamilyName)
    {
        packageFamilyName = string.Empty;

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var marker = processPath.IndexOf(WindowsAppsSegment, StringComparison.OrdinalIgnoreCase);

        if (marker < 0)
        {
            return false;
        }

        var start = marker + WindowsAppsSegment.Length;
        var end = processPath.IndexOf('\\', start);
        var directory = end < 0 ? processPath[start..] : processPath[start..end];

        return TryGetPackageFamilyName(directory, out packageFamilyName);
    }

    /// <summary>
    /// Проверяет, принадлежит ли путь процесса пакету с указанным именем семейства.
    /// </summary>
    public static bool IsPathOfPackage(string? processPath, string packageFamilyName)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyName))
        {
            return false;
        }

        return TryGetPackageFamilyNameFromPath(processPath, out var candidate)
            && string.Equals(candidate, packageFamilyName.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
