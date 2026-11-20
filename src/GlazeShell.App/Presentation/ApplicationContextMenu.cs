using Microsoft.UI.Xaml.Controls;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Общие пункты контекстного меню приложения, которые есть и в обычном меню Windows:
/// запуск от имени администратора, открытие расположения файла, копирование пути и
/// свойства. Меню dock, «Пуска» и слоя рабочего стола добавляют свои пункты сами,
/// поэтому порядок и разделители остаются за вызывающим окном.
/// </summary>
internal static class ApplicationContextMenu
{
    /// <summary>
    /// Добавляет файловые действия для программы. Если путь неизвестен (например, у
    /// ярлыка без найденного исполняемого файла), меню не меняется.
    /// </summary>
    internal static void AddFileActions(MenuFlyout flyout, string? executablePath)
    {
        ArgumentNullException.ThrowIfNull(flyout);

        if (executablePath is not { Length: > 0 } path)
        {
            return;
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var runAs = new MenuFlyoutItem { Text = "Запуск от имени администратора" };
        runAs.Click += (_, _) => ShellTargets.RunAsAdministrator(path);
        flyout.Items.Add(runAs);

        var location = new MenuFlyoutItem { Text = "Открыть расположение файла" };
        location.Click += (_, _) => ShellTargets.ShowInExplorer(path);
        flyout.Items.Add(location);

        var copy = new MenuFlyoutItem { Text = "Копировать путь" };
        copy.Click += (_, _) => ShellTargets.CopyToClipboard(path);
        flyout.Items.Add(copy);

        var properties = new MenuFlyoutItem { Text = "Свойства" };
        properties.Click += (_, _) => ShellTargets.ShowProperties(path);
        flyout.Items.Add(properties);
    }
}
