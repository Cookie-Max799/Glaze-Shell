using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Простое сообщение пользователю средствами системы (функция I2).
/// </summary>
/// <remarks>
/// Используется в режимах, где окна приложения нет: восстановление интерфейса по ключу
/// командной строки. Обычный диалог WinUI в этом режиме недоступен, поэтому вывод идёт
/// через системное окно сообщений.
/// </remarks>
public static class UserNotifier
{
    /// <summary>
    /// Показывает информационное сообщение. Ошибка вывода не прерывает работу вызывающего кода:
    /// сообщение — это удобство, а не часть восстановления.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="caption">Заголовок окна.</param>
    public static void ShowInformation(string text, string caption)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(caption);

        try
        {
            _ = User32.MessageBoxW(
                0,
                text,
                caption,
                User32.MbOk | User32.MbIconInformation | User32.MbSetForeground);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
