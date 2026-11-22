using System.ComponentModel;
using GlazeShell.Core.Glance;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Строка результата глобального поиска Glance (B1): модель ядра плюс оформление —
/// значок приложения или глиф категории, подпись категории и признак наличия пояснения.
/// Значок загружается в фоне после показа строки, поэтому изменение поднимает события
/// <see cref="INotifyPropertyChanged"/> для обновления привязок.
/// </summary>
public sealed class GlanceResultViewModel : INotifyPropertyChanged
{
    /// <summary>Логический размер значка в строке результата: совпадает с разметкой (32 DIP).</summary>
    internal const double IconDips = 32;

    /// <summary>
    /// Запас по разрешению: значок запрашивается вдвое крупнее отображаемого, поэтому
    /// уменьшение в плитку 32 DIP остаётся чистым на масштабированных мониторах.
    /// </summary>
    internal const double IconOversample = 2;

    /// <summary>Верхняя граница запроса значка: защита от неверного масштаба монитора.</summary>
    internal const int MaxIconPixels = 256;

    private ImageSource? _icon;

    public GlanceResultViewModel(GlanceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }

    /// <summary>Исходная модель, по которой выбирается действие открытия.</summary>
    public GlanceResult Result { get; }

    public string Title => Result.Title;

    public string Subtitle => Result.Subtitle;

    public bool HasSubtitle => Subtitle.Length > 0;

    /// <summary>Путь к файлу значка результата; пустая строка, если значка нет.</summary>
    public string IconPath => Result.IconPath;

    /// <summary>
    /// Настоящий значок приложения или окна. Пока значка нет (или настройка его отключила),
    /// плитка показывает глиф категории.
    /// </summary>
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (!ReferenceEquals(_icon, value))
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(HasIcon));
                OnPropertyChanged(nameof(ShowGlyph));
            }
        }
    }

    /// <summary>Показывать ли настоящий значок вместо плитки-глифа.</summary>
    public bool HasIcon => _icon is not null;

    /// <summary>Плитка-глиф показывается, пока настоящего значка нет или настройка выключена.</summary>
    public bool ShowGlyph => _icon is null;

    /// <summary>Глиф Segoe Fluent Icons для категории результата.</summary>
    public string Glyph => Result.Kind switch
    {
        GlanceResultKind.Application => "\uECAA",
        GlanceResultKind.Window => "\uE737",
        GlanceResultKind.Setting => "\uE713",
        GlanceResultKind.Command => "\uE756",
        _ => "\uE9CE",
    };

    /// <summary>Подпись категории для правого края строки.</summary>
    public string KindLabel => Result.Kind switch
    {
        GlanceResultKind.Application => "Приложение",
        GlanceResultKind.Window => "Окно",
        GlanceResultKind.Setting => "Настройки",
        GlanceResultKind.Command => "Команда",
        _ => string.Empty,
    };

    /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged" />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
