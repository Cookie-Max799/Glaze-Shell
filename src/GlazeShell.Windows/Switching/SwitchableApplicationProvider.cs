using System.Diagnostics;
using System.Globalization;
using GlazeShell.Core.Switching;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Switching;

/// <summary>
/// Снимок приложений для переключателя Alt+Tab: перечисляет окна, группирует их по
/// приложению и раскладывает по порядку недавней работы.
/// </summary>
/// <remarks>
/// <para>
/// Одна запись — одно приложение, а не одно окно: окна группируются по процессу, как
/// Cmd+Tab на macOS. Окна UWP живут в общем процессе <c>ApplicationFrameHost</c>, поэтому
/// для рамки <c>ApplicationFrameWindow</c> приложение определяется по дочернему
/// <c>Windows.UI.Core.CoreWindow</c>: иначе «Параметры», «Калькулятор» и «Фотографии»
/// слились бы в одну иконку.
/// </para>
/// <para>
/// Перечисление идёт по Z-порядку окон: система отдаёт самое недавно активное окно первым,
/// поэтому при пустом MRU порядок снимка уже осмысленный. Затем MRU переставляет записи.
/// Служебные окна (панель задач, рабочий стол, оверлеи) отбрасываются по классу, стилю
/// <c>WS_EX_TOOLWINDOW</c>/<c>WS_EX_NOACTIVATE</c>, видимости и прозрачности DWM.
/// </para>
/// </remarks>
public sealed class SwitchableApplicationProvider
{
    private const int MaxWindowText = 512;
    private const int MaxClassName = 256;

    private const string ApplicationFrameClass = "ApplicationFrameWindow";
    private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Shell_InputSwitchTopLevelWindow",
        "Windows.UI.Core.CoreWindow",
        "ApplicationManager_DesktopShellWindow",
        "ForegroundStaging",
        "MultitaskingViewFrame",
        "TaskListThumbnailWnd",
        "XamlExplorerHostIslandWindow",
    };

    private readonly Dictionary<int, ProcessInfo> _processes = [];
    private readonly Lock _processLock = new();

    /// <summary>
    /// Собирает приложения для показа переключателя. Приложения без окон в снимок не
    /// попадают: активировать их без окна средствами Win32 нельзя, а иконка без действия
    /// только запутывала бы.
    /// </summary>
    /// <param name="order">Порядок недавней работы, применяемый к снимку.</param>
    public IReadOnlyList<SwitchableApplication> Snapshot(RecentApplicationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var candidates = CollectWindows();
        var processes = candidates.Select(candidate => candidate.ProcessId).ToHashSet();
        PruneProcessCache(processes);

        var groupOrder = new List<string>(candidates.Count);
        var windowsByGroup = new Dictionary<string, List<SwitchableWindow>>(StringComparer.Ordinal);
        var processByGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        var titleByGroup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var groupId = candidate.GroupId;

            if (!windowsByGroup.TryGetValue(groupId, out var windows))
            {
                windows = [];
                windowsByGroup[groupId] = windows;
                processByGroup[groupId] = candidate.ProcessId;
                titleByGroup[groupId] = candidate.Title;
                groupOrder.Add(groupId);
            }

            windows.Add(new SwitchableWindow(candidate.Handle, candidate.Title, candidate.IsMinimized));
        }

        var applications = new List<SwitchableApplication>(groupOrder.Count);

        foreach (var groupId in groupOrder)
        {
            var processId = processByGroup[groupId];
            var process = ResolveProcess(processId);
            var name = process.Name;

            // Рамка UWP без дочернего CoreWindow отдаёт имя хоста, а не приложения:
            // в этом случае понятнее заголовок окна.
            if (groupId.StartsWith("uwp:", StringComparison.Ordinal) &&
                (name.Length == 0 || name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)))
            {
                name = titleByGroup[groupId];
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = titleByGroup[groupId];
            }

            applications.Add(new SwitchableApplication(
                groupId,
                string.IsNullOrWhiteSpace(name) ? "Приложение" : name,
                processId,
                process.ExecutablePath,
                windowsByGroup[groupId]));
        }

        return order.Order(applications, application => application.Id);
    }

    /// <summary>
    /// Идентификатор приложения для окна. Нужен обновлению MRU по событию фокуса: событие
    /// приносит только дескриптор окна.
    /// </summary>
    public static bool TryGetGroupId(nint hwnd, out string groupId)
    {
        groupId = string.Empty;

        if (hwnd == 0 || !User32.IsWindow(hwnd))
        {
            return false;
        }

        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);
        groupId = ResolveGroupId(hwnd, unchecked((int)processId), out _);
        return groupId.Length > 0;
    }

    /// <summary>
    /// Окна одного приложения по его окну, в порядке Z-порядка. По этому списку Alt+`
    /// переключает окна текущего приложения.
    /// </summary>
    public static IReadOnlyList<SwitchableWindow> GetGroupWindows(nint window)
    {
        if (!TryGetGroupId(window, out var groupId))
        {
            return [];
        }

        var windows = new List<SwitchableWindow>();

        foreach (var candidate in CollectWindows())
        {
            if (string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            {
                windows.Add(new SwitchableWindow(candidate.Handle, candidate.Title, candidate.IsMinimized));
            }
        }

        return windows;
    }

    private static List<Candidate> CollectWindows()
    {
        var candidates = new List<Candidate>(32);

        _ = User32.EnumWindows((hwnd, parameter) =>
        {
            if (!IsSwitchable(hwnd))
            {
                return true;
            }

            var title = GetTitle(hwnd);
            _ = User32.GetWindowThreadProcessId(hwnd, out var processId);
            var pid = unchecked((int)processId);
            var groupId = ResolveGroupId(hwnd, pid, out var applicationProcessId);

            candidates.Add(new Candidate(
                hwnd,
                applicationProcessId,
                title,
                User32.IsIconic(hwnd),
                groupId));

            return true;
        }, 0);

        return candidates;
    }

    private static bool IsSwitchable(nint hwnd)
    {
        if (!User32.IsWindowVisible(hwnd) || IsCloaked(hwnd))
        {
            return false;
        }

        var extendedStyle = (nint)User32.GetWindowLongPtr(hwnd, User32.GwlExStyle);

        if ((extendedStyle & (nint)(User32.WsExToolWindow | User32.WsExNoActivate)) != 0)
        {
            return false;
        }

        var className = GetClassName(hwnd);

        if (IgnoredClasses.Contains(className))
        {
            return false;
        }

        if (!User32.IsIconic(hwnd))
        {
            if (!User32.GetWindowRect(hwnd, out var bounds) ||
                bounds.Right - bounds.Left <= 0 ||
                bounds.Bottom - bounds.Top <= 0)
            {
                return false;
            }
        }

        if (GetTitle(hwnd).Length == 0)
        {
            return false;
        }

        // Окна с владельцем — диалоги и всплывающие панели: они принадлежат тому же
        // приложению, и отдельная иконка для них не нужна.
        var owner = User32.GetWindow(hwnd, User32.GwOwner);

        return owner == 0 || (extendedStyle & (nint)User32.WsExAppWindow) != 0;
    }

    /// <summary>
    /// Идентификатор приложения: процесс для обычных окон и дочерний процесс CoreWindow
    /// для UWP-рамок. Возвращает также процесс, из которого берутся имя и иконка.
    /// </summary>
    private static string ResolveGroupId(nint hwnd, int processId, out int applicationProcessId)
    {
        applicationProcessId = processId;

        if (!string.Equals(GetClassName(hwnd), ApplicationFrameClass, StringComparison.Ordinal))
        {
            return processId > 0
                ? string.Create(CultureInfo.InvariantCulture, $"pid:{processId}")
                : string.Empty;
        }

        var coreWindow = User32.FindWindowEx(hwnd, 0, CoreWindowClass, null);

        if (coreWindow != 0)
        {
            _ = User32.GetWindowThreadProcessId(coreWindow, out var coreProcessId);

            if (coreProcessId != 0)
            {
                applicationProcessId = unchecked((int)coreProcessId);
                return string.Create(CultureInfo.InvariantCulture, $"uwp:{coreProcessId}");
            }
        }

        // Дочернее окно ещё не создано: приложение всё равно должно остаться отдельной
        // иконкой, поэтому рамка становится собственной группой.
        return string.Create(CultureInfo.InvariantCulture, $"uwp:{hwnd:X}");
    }

    private ProcessInfo ResolveProcess(int processId)
    {
        lock (_processLock)
        {
            if (_processes.TryGetValue(processId, out var cached))
            {
                return cached;
            }
        }

        var name = ApplicationNameResolver.GetFriendlyName(processId);
        var path = TryGetExecutablePath(processId);
        var info = new ProcessInfo(string.IsNullOrWhiteSpace(name) ? string.Empty : name, path);

        lock (_processLock)
        {
            _processes[processId] = info;
        }

        return info;
    }

    private void PruneProcessCache(HashSet<int> alive)
    {
        lock (_processLock)
        {
            if (_processes.Count == 0)
            {
                return;
            }

            foreach (var processId in _processes.Keys.Where(id => !alive.Contains(id)).ToArray())
            {
                _processes.Remove(processId);
            }
        }
    }

    private static string? TryGetExecutablePath(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsCloaked(nint hwnd) =>
        Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DwmwaCloaked, out var value, sizeof(int)) >= 0 && value != 0;

    private static string GetTitle(nint hwnd)
    {
        var buffer = new char[MaxWindowText];
        var length = User32.GetWindowText(hwnd, buffer, buffer.Length);

        return length <= 0
            ? string.Empty
            : new string(buffer, 0, Math.Min(length, buffer.Length)).Trim();
    }

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[MaxClassName];
        var length = User32.GetClassName(hwnd, buffer, buffer.Length);

        return length <= 0
            ? string.Empty
            : new string(buffer, 0, Math.Min(length, buffer.Length)).Trim();
    }

    private readonly record struct Candidate(
        nint Handle,
        int ProcessId,
        string Title,
        bool IsMinimized,
        string GroupId);

    private readonly record struct ProcessInfo(string Name, string? ExecutablePath);
}
