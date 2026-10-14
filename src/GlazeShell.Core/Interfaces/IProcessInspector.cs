namespace GlazeShell.Core.Interfaces;

public interface IProcessInspector
{
    IReadOnlyList<int> FindProcessesByPath(string executablePath, CancellationToken cancellationToken = default);

    IReadOnlyList<int> FindProcessesByDirectory(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает процессы, исполняемый файл которых лежит внутри <paramref name="directory"/>
    /// и чьё имя файла входит в <paramref name="imageNames"/>. Нужен для приложений-лаунчеров,
    /// которые запускают настоящий исполняемый файл из подкаталога каталога установки, тогда
    /// как имя образа позволяет не зацепить чужие процессы из общего каталога.
    /// </summary>
    IReadOnlyList<int> FindProcessesByImageInDirectory(
        string directory,
        IReadOnlyCollection<string> imageNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает процессы, исполняемый файл которых лежит внутри каталога установки
    /// MSIX-пакета с указанным именем семейства.
    /// </summary>
    IReadOnlyList<int> FindProcessesByPackage(string packageFamilyName, CancellationToken cancellationToken = default);
}
