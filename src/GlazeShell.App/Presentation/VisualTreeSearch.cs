using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Поиск модели по визуальному дереву. Нужен обработчикам правой кнопки: событие
/// приходит от самого глубокого элемента шаблона (значок, подпись), а данные элемента
/// лежат в <see cref="FrameworkElement.DataContext"/> одного из предков.
/// </summary>
internal static class VisualTreeSearch
{
    internal static T? FindDataContext<T>(DependencyObject? source)
        where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T match })
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
