namespace GlazeShell.Core.Interfaces;

public interface IProcessInspector
{
    IReadOnlyList<int> FindProcessesByPath(string executablePath, CancellationToken cancellationToken = default);

    IReadOnlyList<int> FindProcessesByDirectory(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает процессы, исполняемый файл которых лежит внутри каталога установки
    /// MSIX-пакета с указанным именем семейства.
    /// </summary>
    IReadOnlyList<int> FindProcessesByPackage(string packageFamilyName, CancellationToken cancellationToken = default);
}
