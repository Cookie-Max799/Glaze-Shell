using GlazeShell.Core.Models;

namespace GlazeShell.Data.Serialization;

/// <summary>
/// Преобразование документа темы в доменную модель с проверкой данных.
/// </summary>
/// <remarks>
/// Тема — входные данные, поэтому проверяется всё, что модель затем передаст UI:
/// идентификаторы и имена, диапазоны размеров, формат цвета и безопасность источников ассетов.
/// Конструкторы модели повторяют часть проверок, а диагностика собирает все проблемы
/// документа, чтобы пользователь увидел в логе причину, а не первый попавшийся отказ.
/// Документ с любыми нарушениями отклоняется целиком: частичная тема означала бы,
/// что UI применил бы половину недопустимых значений.
/// </remarks>
internal static class ThemeDocumentMapper
{
    public static bool TryMap(ThemeDocument? document, string fileName, out Theme? theme, out IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();

        if (document is null)
        {
            theme = null;
            diagnostics = [$"The theme '{fileName}' is empty."];
            return false;
        }

        if (!PersistenceLimits.IsValidIdentifier(document.Id))
        {
            messages.Add($"The theme '{fileName}' has an invalid identifier.");
        }

        if (!PersistenceLimits.IsValidName(document.Name))
        {
            messages.Add($"The theme '{fileName}' has an invalid name.");
        }

        var metadata = MapMetadata(document.Metadata, fileName, messages);
        var colors = MapColors(document.Colors, fileName, messages);
        var fonts = MapFonts(document.Fonts, fileName, messages);
        var dimensions = MapDimensions(document.Dimensions, fileName, messages);
        var icons = MapIcons(document.Icons, fileName, messages);
        var wallpaper = MapWallpaper(document.Wallpaper, fileName, messages);
        var effects = MapEffects(document.Effects, fileName, messages);

        if (messages.Count > 0 || document.Id is null || document.Name is null ||
            metadata is null || colors is null || fonts is null || dimensions is null || icons is null || effects is null)
        {
            theme = null;
            diagnostics = messages.ToArray();
            return false;
        }

        try
        {
            theme = new Theme(
                document.Id,
                document.Name,
                metadata,
                colors,
                fonts,
                dimensions,
                icons,
                wallpaper,
                effects);
        }
        catch (ArgumentException exception)
        {
            // Модель отклонила значение, которое проверки документа пропустили:
            // сообщение модели дополняет диагностику, а не заменяет её.
            messages.Add($"The theme '{fileName}' is invalid: {exception.Message}");
            theme = null;
            diagnostics = messages.ToArray();
            return false;
        }

        diagnostics = messages.ToArray();
        return true;
    }

    private static ThemeMetadata? MapMetadata(ThemeMetadataDocument? document, string fileName, List<string> messages)
    {
        if (document is null)
        {
            messages.Add($"The theme '{fileName}' has no metadata.");
            return null;
        }

        if (!PersistenceLimits.IsValidName(document.Name))
        {
            messages.Add($"The theme '{fileName}' has invalid metadata name.");
        }

        if (!IsValidVersion(document.Version))
        {
            messages.Add($"The theme '{fileName}' has an invalid version.");
        }

        if (document.Author is not null && !PersistenceLimits.IsValidName(document.Author))
        {
            messages.Add($"The theme '{fileName}' has an invalid author.");
        }

        if (document.Description is not null && !PersistenceLimits.IsValidName(document.Description))
        {
            messages.Add($"The theme '{fileName}' has an invalid description.");
        }

        return new ThemeMetadata(
            document.Name ?? string.Empty,
            document.Version ?? string.Empty,
            document.Author,
            document.Description);
    }

    /// <summary>
    /// Версия темы — свободная строка, но ограниченной длины и без управляющих символов:
    /// она попадает в UI и в лог, поэтому мусорные символы недопустимы.
    /// </summary>
    private static bool IsValidVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 32 &&
        !PersistenceLimits.ContainsControlCharacters(value);

    private static ThemeColors? MapColors(List<ThemeColorDocument>? documents, string fileName, List<string> messages)
    {
        var items = documents ?? [];
        if (items.Count > PersistenceLimits.MaxColorsPerTheme)
        {
            messages.Add($"The theme '{fileName}' contains more than {PersistenceLimits.MaxColorsPerTheme} colors.");
            return null;
        }

        var colors = new List<ThemeColor>(items.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null)
            {
                messages.Add($"The theme '{fileName}', color #{index} is null.");
                return null;
            }

            if (!PersistenceLimits.IsValidIdentifier(item.Name))
            {
                messages.Add($"The theme '{fileName}', color #{index} has an invalid name.");
                return null;
            }

            if (!names.Add(item.Name!))
            {
                messages.Add($"The theme '{fileName}' contains the duplicated color '{item.Name}'.");
                return null;
            }

            if (!IsValidColorValue(item.Value))
            {
                messages.Add($"The theme '{fileName}', color '{item.Name}' must be in the #RGB, #RRGGBB or #AARRGGBB format.");
                return null;
            }

            colors.Add(new ThemeColor(item.Name!, item.Value!));
        }

        return new ThemeColors(colors);
    }

    /// <summary>
    /// Цвет задаётся как <c>#RGB</c>, <c>#RRGGBB</c> или <c>#AARRGGBB</c>.
    /// Имена вроде <c>red</c> и выражения вроде <c>rgb(...)</c> отклоняются:
    /// UI передаёт значение движку рендеринга напрямую, и разбор выражений
    /// означал бы выполнение кода, заданного темой.
    /// </summary>
    private static bool IsValidColorValue(string? value)
    {
        if (value is null || value.Length is not (4 or 7 or 9) || value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static ThemeFonts? MapFonts(List<ThemeFontDocument>? documents, string fileName, List<string> messages)
    {
        var items = documents ?? [];
        if (items.Count > PersistenceLimits.MaxFontsPerTheme)
        {
            messages.Add($"The theme '{fileName}' contains more than {PersistenceLimits.MaxFontsPerTheme} fonts.");
            return null;
        }

        var fonts = new List<ThemeFont>(items.Count);
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null)
            {
                messages.Add($"The theme '{fileName}', font #{index} is null.");
                return null;
            }

            if (!PersistenceLimits.IsValidName(item.Family))
            {
                messages.Add($"The theme '{fileName}', font #{index} has an invalid family.");
                return null;
            }

            if (!families.Add(item.Family!))
            {
                messages.Add($"The theme '{fileName}' contains the duplicated font family '{item.Family}'.");
                return null;
            }

            if (item.Size is null || !double.IsFinite(item.Size.Value) || item.Size.Value <= 0)
            {
                messages.Add($"The theme '{fileName}', font '{item.Family}' has an invalid size.");
                return null;
            }

            if (item.Weight is not null && !PersistenceLimits.IsValidName(item.Weight))
            {
                messages.Add($"The theme '{fileName}', font '{item.Family}' has an invalid weight.");
                return null;
            }

            if (item.Style is not null && !PersistenceLimits.IsValidName(item.Style))
            {
                messages.Add($"The theme '{fileName}', font '{item.Family}' has an invalid style.");
                return null;
            }

            fonts.Add(new ThemeFont(item.Family!, item.Size.Value, item.Weight, item.Style));
        }

        return new ThemeFonts(fonts);
    }

    private static ThemeDimensions? MapDimensions(ThemeDimensionsDocument? document, string fileName, List<string> messages)
    {
        if (document is null)
        {
            // Отсутствующий раздел допустим: применяются значения по умолчанию модели.
            return new ThemeDimensions();
        }

        if (document.CornerRadius is not null && (!double.IsFinite(document.CornerRadius.Value) || document.CornerRadius.Value < 0))
        {
            messages.Add($"The theme '{fileName}' has a negative corner radius.");
            return null;
        }

        if (document.Spacing is not null && (!double.IsFinite(document.Spacing.Value) || document.Spacing.Value < 0))
        {
            messages.Add($"The theme '{fileName}' has a negative spacing.");
            return null;
        }

        if (document.IconSize is not null && (!double.IsFinite(document.IconSize.Value) || document.IconSize.Value <= 0))
        {
            messages.Add($"The theme '{fileName}' has a non-positive icon size.");
            return null;
        }

        if (document.TitleBarHeight is not null && document.TitleBarHeight.Value <= 0)
        {
            messages.Add($"The theme '{fileName}' has a non-positive title bar height.");
            return null;
        }

        return new ThemeDimensions(
            document.CornerRadius ?? 8,
            document.Spacing ?? 8,
            document.IconSize ?? 24,
            document.TitleBarHeight ?? 32);
    }

    private static ThemeIcons? MapIcons(List<ThemeIconDocument>? documents, string fileName, List<string> messages)
    {
        var items = documents ?? [];
        if (items.Count > PersistenceLimits.MaxIconsPerTheme)
        {
            messages.Add($"The theme '{fileName}' contains more than {PersistenceLimits.MaxIconsPerTheme} icons.");
            return null;
        }

        var icons = new List<ThemeIcon>(items.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null)
            {
                messages.Add($"The theme '{fileName}', icon #{index} is null.");
                return null;
            }

            if (!PersistenceLimits.IsValidIdentifier(item.Name))
            {
                messages.Add($"The theme '{fileName}', icon #{index} has an invalid name.");
                return null;
            }

            if (!names.Add(item.Name!))
            {
                messages.Add($"The theme '{fileName}' contains the duplicated icon '{item.Name}'.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(item.Source))
            {
                messages.Add($"The theme '{fileName}', icon '{item.Name}' has no source.");
                return null;
            }

            try
            {
                icons.Add(new ThemeIcon(item.Name!, item.Source!));
            }
            catch (ArgumentException exception)
            {
                // Модель отклоняет исполняемые ассеты и пути вне каталога темы.
                messages.Add($"The theme '{fileName}', icon '{item.Name}' is not allowed: {exception.Message}");
                return null;
            }
        }

        return new ThemeIcons(icons);
    }

    private static ThemeWallpaper? MapWallpaper(ThemeWallpaperDocument? document, string fileName, List<string> messages)
    {
        if (document is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(document.Source))
        {
            messages.Add($"The theme '{fileName}' has a wallpaper without a source.");
            return null;
        }

        var fit = WallpaperFit.Fill;
        if (document.Fit is not null && !Enum.TryParse<WallpaperFit>(document.Fit, ignoreCase: true, out fit))
        {
            messages.Add($"The theme '{fileName}' has an unknown wallpaper fit '{document.Fit}'.");
            return null;
        }

        try
        {
            return new ThemeWallpaper(document.Source!, fit);
        }
        catch (ArgumentException exception)
        {
            messages.Add($"The theme '{fileName}' has a wallpaper that is not allowed: {exception.Message}");
            return null;
        }
    }

    private static ThemeEffects? MapEffects(ThemeEffectsDocument? document, string fileName, List<string> messages)
    {
        if (document is null)
        {
            return new ThemeEffects();
        }

        if (document.AnimationDurationMilliseconds is not null && document.AnimationDurationMilliseconds.Value < 0)
        {
            messages.Add($"The theme '{fileName}' has a negative animation duration.");
            return null;
        }

        if (document.Opacity is not null &&
            (!double.IsFinite(document.Opacity.Value) || document.Opacity.Value is < 0 or > 1))
        {
            messages.Add($"The theme '{fileName}' has an opacity outside the 0..1 range.");
            return null;
        }

        return new ThemeEffects(
            document.AnimationsEnabled ?? true,
            document.AnimationDurationMilliseconds ?? 150,
            document.Opacity ?? 1.0);
    }
}
