namespace GlazeShell.Core.Theming;

/// <summary>
/// Цвет палитры Glaze Shell. Хранится отдельно от WinUI, чтобы расчёт темы и проверку
/// контраста можно было покрыть обычными тестами ядра.
/// </summary>
/// <param name="R">Красный канал.</param>
/// <param name="G">Зелёный канал.</param>
/// <param name="B">Синий канал.</param>
/// <param name="A">Прозрачность.</param>
public readonly record struct GlazeColor(byte R, byte G, byte B, byte A)
{
    /// <summary>Непрозрачный цвет.</summary>
    public GlazeColor(byte red, byte green, byte blue)
        : this(red, green, blue, 0xFF)
    {
    }

    public static GlazeColor White { get; } = new(0xFF, 0xFF, 0xFF);

    public static GlazeColor Black { get; } = new(0x00, 0x00, 0x00);

    /// <summary>
    /// Относительная яркость по WCAG 2.1. Прозрачность не учитывается: токены темы
    /// задают непрозрачные поверхности, а стекло считается по своему цвету.
    /// </summary>
    public double RelativeLuminance
    {
        get
        {
            var red = Linearize(R);
            var green = Linearize(G);
            var blue = Linearize(B);

            return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        }
    }

    public GlazeColor WithAlpha(byte alpha) => this with { A = alpha };

    /// <summary>Контраст по WCAG: от 1 (цвета совпали) до 21 (чёрный и белый).</summary>
    public double ContrastRatio(GlazeColor other)
    {
        var first = RelativeLuminance;
        var second = other.RelativeLuminance;
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Линейное смешение цветов: <paramref name="amount"/> 0 — <paramref name="from"/>,
    /// 1 — <paramref name="to"/>. Прозрачность берётся у исходного цвета.
    /// </summary>
    public static GlazeColor Mix(GlazeColor from, GlazeColor to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);

        return new GlazeColor(
            (byte)Math.Round(from.R + ((to.R - from.R) * t)),
            (byte)Math.Round(from.G + ((to.G - from.G) * t)),
            (byte)Math.Round(from.B + ((to.B - from.B) * t)),
            from.A);
    }

    /// <summary>Осветляет цвет к белому.</summary>
    public static GlazeColor Lighten(GlazeColor color, double amount) => Mix(color, White, amount);

    /// <summary>
    /// Разбирает цвет вида <c>#RRGGBB</c> или <c>#AARRGGBB</c>. Некорректная строка
    /// возвращает <paramref name="fallback"/>.
    /// </summary>
    public static GlazeColor ParseHex(string? hex, GlazeColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var value = hex.Trim().TrimStart('#');

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8
            || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var packed))
        {
            return fallback;
        }

        return new GlazeColor(
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF),
            (byte)((packed >> 24) & 0xFF));
    }

    /// <summary>
    /// Поднимает контраст цвета к набору фонов до <paramref name="minimum"/>, смещая его
    /// к белому (<paramref name="towardWhite"/>) или к чёрному. Направление задаёт тема:
    /// в светлой текст затемняется, в тёмной — светлеет.
    /// </summary>
    public GlazeColor EnsureContrastAgainst(
        IReadOnlyList<GlazeColor> backgrounds,
        double minimum,
        bool towardWhite)
    {
        if (backgrounds.Count == 0)
        {
            return this;
        }

        var target = towardWhite ? White : Black;

        return BlendUntil(
            this,
            target,
            candidate => MinimumContrast(candidate, backgrounds) >= minimum);
    }

    /// <summary>
    /// Удерживает яркость цвета в диапазоне: слишком тёмный цвет осветляется к белому,
    /// слишком светлый — затемняется к чёрному. Нужно, чтобы тонированные основным цветом
    /// поверхности оставались пригодными для текста выбранной темы.
    /// </summary>
    public GlazeColor ClampLuminance(double minimum, double maximum)
    {
        var luminance = RelativeLuminance;

        if (luminance < minimum)
        {
            return BlendUntil(this, White, candidate => candidate.RelativeLuminance >= minimum);
        }

        if (luminance > maximum)
        {
            return BlendUntil(this, Black, candidate => candidate.RelativeLuminance <= maximum);
        }

        return this;
    }

    /// <summary>
    /// Цвет текста на сплошной акцентной заливке: чёрный или белый — тот, что даёт больший
    /// контраст. Для любого цвета один из них проходит AA (не хуже 4.58:1).
    /// </summary>
    public static GlazeColor OnAccent(GlazeColor accent) =>
        accent.ContrastRatio(White) >= accent.ContrastRatio(Black) ? White : Black;

    private static GlazeColor BlendUntil(GlazeColor source, GlazeColor target, Func<GlazeColor, bool> predicate)
    {
        if (predicate(source))
        {
            return source;
        }

        if (!predicate(target))
        {
            return target;
        }

        var low = 0.0;
        var high = 1.0;

        for (var i = 0; i < 24; i++)
        {
            var middle = (low + high) / 2;

            if (predicate(Mix(source, target, middle)))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return Mix(source, target, high);
    }

    private static double MinimumContrast(GlazeColor foreground, IReadOnlyList<GlazeColor> backgrounds)
    {
        var minimum = double.MaxValue;

        foreach (var background in backgrounds)
        {
            minimum = Math.Min(minimum, foreground.ContrastRatio(background));
        }

        return minimum;
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;

        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
