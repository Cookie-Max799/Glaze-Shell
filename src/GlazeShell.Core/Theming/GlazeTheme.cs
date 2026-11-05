namespace GlazeShell.Core.Theming;

/// <summary>
/// Цветовые токены конкретной темы Glaze Shell: поверхности, текст и акцент.
/// </summary>
/// <param name="IsLight">Светлая ли тема — от этого зависит направление коррекции контраста.</param>
/// <param name="Background">Фон окон.</param>
/// <param name="Surface">Поверхность панелей и карточек.</param>
/// <param name="Elevated">«Приподнятая» поверхность.</param>
/// <param name="Border">Рамка поверхностей.</param>
/// <param name="Glass">Полупрозрачная заливка стекла.</param>
/// <param name="TextPrimary">Основной текст.</param>
/// <param name="TextSecondary">Второстепенный текст.</param>
/// <param name="TextMuted">Приглушённый текст и плейсхолдеры.</param>
/// <param name="DefaultAccent">Акцент для заливок, градиента и рамок.</param>
/// <param name="AccentForeground">Акцент для текста и иконок, поднятый до читаемого контраста.</param>
public sealed record GlazeTheme(
    bool IsLight,
    GlazeColor Background,
    GlazeColor Surface,
    GlazeColor Elevated,
    GlazeColor Border,
    GlazeColor Glass,
    GlazeColor TextPrimary,
    GlazeColor TextSecondary,
    GlazeColor TextMuted,
    GlazeColor DefaultAccent,
    GlazeColor AccentForeground);
