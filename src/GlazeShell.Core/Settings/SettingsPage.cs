namespace GlazeShell.Core.Settings;

/// <summary>
/// Страница настроек: заголовок, глиф сайдбара и раздел, к которому она относится.
/// </summary>
/// <param name="Id">Устойчивый идентификатор страницы.</param>
/// <param name="Title">Отображаемое имя в сайдбаре.</param>
/// <param name="Glyph">Глиф Segoe Fluent Icons для пункта сайдбара.</param>
/// <param name="Section">Группа сайдбара («Основные», «Интерфейс», «Система»).</param>
public sealed record SettingsPage(string Id, string Title, string Glyph, string Section);
