namespace GlazeShell.Core.Settings;

/// <summary>
/// Вид значения настройки — определяет, какой контрол рисует страница настроек.
/// Набор намеренно маленький: MVP покрывает переключатель, выбор из списка и текст.
/// </summary>
public enum SettingKind
{
    /// <summary>Логический переключатель (вкл/выкл).</summary>
    Toggle,

    /// <summary>Выбор одного значения из фиксированного списка.</summary>
    Choice,

    /// <summary>Строка текста.</summary>
    Text,

    /// <summary>Число в диапазоне: ползунок с подписью текущего значения.</summary>
    Range,
}

/// <summary>
/// Один вариант для настройки вида <see cref="SettingKind.Choice"/>.
/// </summary>
/// <param name="Value">Значение варианта (идентификатор или ключ).</param>
/// <param name="Title">Отображаемое имя.</param>
/// <param name="ColorHex">
/// Цвет образца (<c>#RRGGBB</c>) для цветовых вариантов; <c>null</c> — вариант без образца.
/// </param>
public sealed record SettingChoice(string Value, string Title, string? ColorHex = null)
{
    /// <summary>Показывать ли цветной образец рядом с названием варианта.</summary>
    public bool HasColor => !string.IsNullOrWhiteSpace(ColorHex);
}
