using System.Diagnostics;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Открывает системную цель оболочки (<c>ms-settings:</c>, <c>shell:</c>, http) через
/// ShellExecute. Общий помощник верхней панели, Glance и popover «Центр управления».
/// </summary>
internal static class ShellTargets
{
    internal static void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.ShellTargets: opening '{target}' failed. {exception}");
        }
    }

    /// <summary>
    /// Показывает файл или папку в проводнике Windows с выделением. Команда
    /// <c>/select,</c> передаётся одним аргументом: иначе оболочка считает путь
    /// отдельным параметром и открывает только «Документы».
    /// </summary>
    internal static void ShowInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.ShellTargets: selecting '{path}' failed. {exception}");
        }
    }

    /// <summary>
    /// Открывает системное окно «Свойства» для файла или папки. Глагол обрабатывает
    /// сама оболочка, поэтому приложение не читает и не меняет атрибуты само.
    /// </summary>
    internal static void ShowProperties(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "properties",
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.ShellTargets: properties of '{path}' failed. {exception}");
        }
    }

    /// <summary>
    /// Запускает программу от имени администратора: глагол <c>runas</c> показывает
    /// системное подтверждение UAC, поэтому приложение не запрашивает права само и
    /// не повышает собственный процесс.
    /// </summary>
    internal static void RunAsAdministrator(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "runas",
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Отказ пользователя в UAC — обычный исход, а не ошибка приложения.
            Trace.WriteLine($"GlazeShell.ShellTargets: running '{path}' as administrator failed. {exception}");
        }
    }

    /// <summary>
    /// Кладёт текст в системный буфер обмена. Буфер может быть занят другим процессом,
    /// поэтому сбой не пробрасывается: повторная попытка — следующим выбором пункта.
    /// </summary>
    internal static void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.ShellTargets: copying to the clipboard failed. {exception}");
        }
    }
}
