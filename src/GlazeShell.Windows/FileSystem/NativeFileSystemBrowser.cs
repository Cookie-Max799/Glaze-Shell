using GlazeShell.Core.Files;
using GlazeShell.Core.Interfaces;

namespace GlazeShell.Windows.FileSystem;

/// <summary>
/// Реализация <see cref="IFileSystemBrowser"/> на <c>System.IO</c> (E1/E2): содержимое
/// каталога, логические диски и системные папки.
/// </summary>
/// <remarks>
/// Провайдер best-effort: каталог без прав чтения, исчезнувший элемент и неготовый привод
/// не выпускают исключение наружу. Исключения выходят только неустранимые
/// (<see cref="OutOfMemoryException"/> и <see cref="StackOverflowException"/>) — они означают
/// отказ процесса, а не отказ конкретного пути.
/// </remarks>
public sealed class NativeFileSystemBrowser : IFileSystemBrowser
{
    private List<SystemFolderLocation>? _systemFolders;

    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return Directory.Exists(path);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return false;
        }
    }

    public IReadOnlyList<FileSystemEntry> GetEntries(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var entries = new List<FileSystemEntry>();

        try
        {
            var directory = new DirectoryInfo(path);

            foreach (var info in directory.EnumerateFileSystemInfos("*", CreateOptions()))
            {
                var entry = TryReadEntry(info);

                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Каталог мог исчезнуть или стать недоступным между навигацией и чтением:
            // показываем то, что успели прочитать, вместо исключения в UI.
        }

        return entries;
    }

    public IReadOnlyList<DriveEntry> GetDrives()
    {
        var drives = new List<DriveEntry>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                drives.Add(ReadDrive(drive));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Перечисление дисков — системный вызов; его отказ не должен ломать сайдбар.
        }

        return drives;
    }

    public IReadOnlyList<SystemFolderLocation> GetSystemFolders() => _systemFolders ??= BuildSystemFolders();

    /// <summary>
    /// Параметры перечисления: скрытые и системные элементы пропускаются (как в проводнике
    /// Windows), недоступные каталоги не прерывают обход, рекурсии нет — элементы читаются
    /// по одному уровню за навигацию.
    /// </summary>
    private static EnumerationOptions CreateOptions() => new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    private static FileSystemEntry? TryReadEntry(FileSystemInfo info)
    {
        try
        {
            var isFolder = info is DirectoryInfo;

            return new FileSystemEntry(
                info.Name,
                info.FullName,
                isFolder ? FileSystemEntryKind.Folder : FileSystemEntryKind.File,
                isFolder ? null : ((FileInfo)info).Length,
                new DateTimeOffset(info.LastWriteTime));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Элемент исчез между перечислением и чтением свойств: пропускаем его.
            return null;
        }
    }

    private static DriveEntry ReadDrive(DriveInfo drive)
    {
        var kind = MapKind(drive.DriveType);
        var name = drive.Name;

        try
        {
            if (!drive.IsReady)
            {
                // Неготовый привод остаётся в списке без объёма: пользователь видит, что
                // устройство есть, но данных о нём пока нет.
                return new DriveEntry(name, null, kind, null, null);
            }

            var label = drive.VolumeLabel?.Trim();

            return new DriveEntry(
                name,
                string.IsNullOrEmpty(label) ? null : label,
                kind,
                drive.TotalSize,
                drive.AvailableFreeSpace);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new DriveEntry(name, null, kind, null, null);
        }
    }

    private static DriveKind MapKind(DriveType type) => type switch
    {
        DriveType.Fixed => DriveKind.Fixed,
        DriveType.Removable => DriveKind.Removable,
        DriveType.Network => DriveKind.Network,
        DriveType.CDRom => DriveKind.Optical,
        DriveType.Ram => DriveKind.Ram,
        _ => DriveKind.Other,
    };

    /// <summary>
    /// Системные папки сайдбара: только те, что существуют на этой машине (например, папки
    /// мультимедиа могут отсутствовать). «Загрузки» строится от профиля пользователя, потому
    /// что отдельного значения <see cref="Environment.SpecialFolder"/> для неё нет.
    /// </summary>
    private static List<SystemFolderLocation> BuildSystemFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folders = new List<SystemFolderLocation>(7);

        Add(SystemFolderKind.Home, "Домашняя папка", profile);
        Add(SystemFolderKind.Desktop, "Рабочий стол", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add(SystemFolderKind.Documents, "Документы", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(SystemFolderKind.Downloads, "Загрузки", string.IsNullOrEmpty(profile) ? string.Empty : Path.Combine(profile, "Downloads"));
        Add(SystemFolderKind.Pictures, "Изображения", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(SystemFolderKind.Music, "Музыка", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        Add(SystemFolderKind.Videos, "Видео", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

        return folders;

        void Add(SystemFolderKind kind, string name, string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                folders.Add(new SystemFolderLocation(kind, name, path));
            }
        }
    }
}
