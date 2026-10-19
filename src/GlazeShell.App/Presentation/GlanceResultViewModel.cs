using GlazeShell.Core.Glance;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Строка результата глобального поиска Glance (B1): модель ядра плюс оформление —
/// глиф категории, подпись категории и признак наличия пояснения.
/// </summary>
public sealed class GlanceResultViewModel
{
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
}
