using GlazeShell.App.Theming;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Превращает строку <c>#RRGGBB</c> из <see cref="Core.Settings.SettingChoice"/> в кисть
/// для цветного образца в списке настроек. Пустое значение даёт прозрачный образец.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Color Transparent = Color.FromArgb(0, 0, 0, 0);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var color = value is string hex ? GlazePalette.ParseHex(hex, Transparent) : Transparent;
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
