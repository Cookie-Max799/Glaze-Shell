using GlazeShell.Core.Models;

namespace GlazeShell.Core.Discovery;

/// <summary>
/// Сопоставляет процессы и окна с приложением по пути исполняемого файла. Кроме точного
/// совпадения пути учитывается каталог установки: приложения-лаунчеры (например, Squirrel
/// у Discord) запускают настоящий исполняемый файл из подкаталога каталога установки, и
/// путь из ярлыка (лаунчер) после запуска уже не существует. Чтобы не смешивать разные
/// приложения из одного каталога (например, git-bash и git-cmd в одном каталоге Git),
/// в пределах каталога сопоставление идёт по имени образа, а имя из аргумента
/// <c>--processStart</c> учитывается отдельно.
/// </summary>
public static class ApplicationProcessMatcher
{
    private const string ProcessStartArgument = "--processStart";

    /// <summary>
    /// Проверяет, принадлежит ли процесс приложению. Сначала сопоставляется путь к
    /// исполняемому файлу, затем — имя образа процесса: события процессов могут не нести
    /// полный путь, но всегда содержат имя образа.
    /// </summary>
    public static bool Matches(Application application, string? executablePath, string? processName)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (MatchesExecutable(application, executablePath))
        {
            return true;
        }

        return application.ExecutablePath is { } target
            && !string.IsNullOrWhiteSpace(processName)
            && string.Equals(Path.GetFileNameWithoutExtension(target), processName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, принадлежит ли исполняемый файл приложению: совпадает с путём запуска,
    /// лежит в каталоге установки под именем образа приложения (лаунчер) или, для MSIX,
    /// находится внутри каталога установки пакета.
    /// </summary>
    public static bool MatchesExecutable(Application application, string? executablePath)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        if (application.ExecutablePath is { } target)
        {
            return IsSamePath(target, executablePath) || IsUnderInstallDirectory(application, executablePath);
        }

        return application.PackageFamilyName is { } packageFamilyName
            && PackageIdentity.IsPathOfPackage(executablePath, packageFamilyName);
    }

    /// <summary>Каталог установки приложения (каталог исполняемого файла), если он известен.</summary>
    public static string? GetInstallDirectory(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        return application.ExecutablePath is { Length: > 0 } path ? Path.GetDirectoryName(path) : null;
    }

    /// <summary>
    /// Имена образов, которыми может быть представлено приложение: имя файла запуска и,
    /// если приложение запускается через лаунчер, имя из аргумента <c>--processStart</c>.
    /// </summary>
    public static IReadOnlyCollection<string> GetImageNames(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (application.ExecutablePath is { } executablePath
            && Path.GetFileName(executablePath) is { Length: > 0 } fileName)
        {
            names.Add(fileName);
        }

        if (TryGetProcessStartImage(application.Arguments) is { } started)
        {
            names.Add(started);
        }

        return names;
    }

    private static bool IsUnderInstallDirectory(Application application, string executablePath)
    {
        if (GetInstallDirectory(application) is not { } directory || !IsUnderDirectory(executablePath, directory))
        {
            return false;
        }

        var imageName = Path.GetFileName(executablePath);
        return imageName.Length > 0 && GetImageNames(application).Contains(imageName);
    }

    private static string? TryGetProcessStartImage(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var index = arguments.IndexOf(ProcessStartArgument, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return null;
        }

        var rest = arguments[(index + ProcessStartArgument.Length)..].TrimStart();

        if (rest.Length == 0)
        {
            return null;
        }

        var token = rest[0] == '"' ? ReadQuoted(rest) : ReadToken(rest);
        var image = Path.GetFileName(token.Trim('"'));

        return string.IsNullOrWhiteSpace(image) ? null : image;
    }

    private static string ReadQuoted(string value)
    {
        var end = value.IndexOf('"', 1);
        return end < 0 ? value[1..] : value[1..end];
    }

    private static string ReadToken(string value)
    {
        var end = value.IndexOf(' ');
        return end < 0 ? value : value[..end];
    }

    private static bool IsSamePath(string expected, string actual) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderDirectory(string path, string directory)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return path.Length > prefix.Length
            && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (path[prefix.Length] == Path.DirectorySeparatorChar || path[prefix.Length] == Path.AltDirectorySeparatorChar);
    }
}
