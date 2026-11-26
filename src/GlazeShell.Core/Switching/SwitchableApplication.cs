namespace GlazeShell.Core.Switching;

/// <summary>
/// Окно приложения в переключателе. Дескриптор хранится числом, чтобы модель оставалась
/// независимой от Win32 и проверялась тестами без окон.
/// </summary>
/// <param name="Handle">Дескриптор окна.</param>
/// <param name="Title">Заголовок окна.</param>
/// <param name="IsMinimized">Окно свёрнуто.</param>
public sealed record SwitchableWindow(long Handle, string Title, bool IsMinimized);

/// <summary>
/// Приложение в переключателе Alt+Tab: одна запись на процесс, его окна сгруппированы.
/// </summary>
/// <remarks>
/// Группировка по процессу, а не по окну, повторяет поведение Cmd+Tab: переключатель
/// показывает приложения, а не каждое окно отдельно. Окружение Win32 приносит список
/// окон сверху вниз по Z-порядку, поэтому первый элемент <see cref="Windows"/> — самое
/// недавно активное окно приложения.
/// </remarks>
/// <param name="Id">Устойчивый идентификатор приложения (идентификатор процесса).</param>
/// <param name="Name">Понятное имя приложения.</param>
/// <param name="ProcessId">Идентификатор процесса.</param>
/// <param name="ExecutablePath">Путь к исполняемому файлу, если удалось прочитать.</param>
/// <param name="Windows">Окна приложения, самое недавно активное — первым.</param>
public sealed record SwitchableApplication(
    string Id,
    string Name,
    int ProcessId,
    string? ExecutablePath,
    IReadOnlyList<SwitchableWindow> Windows)
{
    /// <summary>Есть ли у приложения окна.</summary>
    public bool HasWindows => Windows.Count > 0;

    /// <summary>Самое недавно активное окно приложения.</summary>
    public SwitchableWindow? MostRecentWindow => Windows.Count == 0 ? null : Windows[0];
}
