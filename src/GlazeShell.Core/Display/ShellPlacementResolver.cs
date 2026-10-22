using GlazeShell.Core.Models;

namespace GlazeShell.Core.Display;

/// <summary>
/// Правила размещения панелей оболочки на мониторе (функция I6).
/// </summary>
/// <remarks>
/// <para>
/// Размещение — чистая функция от монитора, якоря и размера панели: ядро не знает о WinUI и
/// не измеряет ничего. Поэтому одинаковое правило применяется к dock и верхней панели, а
/// проверяется обычными юнит-тестами.
/// </para>
/// <para>
/// Размер панели задаётся в логических единицах (DIP), а результат возвращается в физических
/// пикселях: масштаб монитора (DPI/96) может отличаться от масштаба соседнего, поэтому
/// пересчёт обязателен.
/// </para>
/// </remarks>
public static class ShellPlacementResolver
{
    /// <summary>
    /// Вычисляет положение панели на мониторе.
    /// </summary>
    /// <param name="monitor">Монитор, на котором размещается панель.</param>
    /// <param name="anchor">К какому краю монитора прижимается панель.</param>
    /// <param name="alignment">Как панель выравнивается по горизонтали.</param>
    /// <param name="widthDip">Ширина панели в логических единицах. В режиме
    /// <see cref="ShellAlignment.Stretch"/> игнорируется и не проверяется: ширину задаёт
    /// монитор.</param>
    /// <param name="heightDip">Высота панели в логических единицах.</param>
    /// <param name="marginDip">Боковой отступ от края монитора в логических единицах.</param>
    /// <param name="verticalMarginDip">Вертикальный отступ от края монитора в логических
    /// единицах; по умолчанию равен боковому отступу.</param>
    /// <returns>Положение панели в физических пикселях.</returns>
    public static ShellPlacement Resolve(
        MonitorInfo monitor,
        ShellAnchor anchor,
        ShellAlignment alignment,
        double widthDip,
        double heightDip,
        double marginDip = 0,
        double? verticalMarginDip = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        if (alignment != ShellAlignment.Stretch)
        {
            ModelValidation.Positive(widthDip, nameof(widthDip));
        }

        ModelValidation.Positive(heightDip, nameof(heightDip));
        ModelValidation.NonNegative(marginDip, nameof(marginDip));

        if (verticalMarginDip is { } vertical && vertical < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalMarginDip), vertical, "The value cannot be negative.");
        }

        var scale = monitor.ScaleFactor;
        var margin = (int)Math.Round(marginDip * scale, MidpointRounding.AwayFromZero);
        var verticalMargin = (int)Math.Round((verticalMarginDip ?? marginDip) * scale, MidpointRounding.AwayFromZero);

        var bounds = monitor.Bounds;
        var height = ToPixels(heightDip, scale);

        int x;
        int width;

        switch (alignment)
        {
            case ShellAlignment.Left:
                width = ToPixels(widthDip, scale);
                x = bounds.X + margin;
                break;

            case ShellAlignment.Right:
                width = ToPixels(widthDip, scale);
                x = bounds.X + bounds.Width - width - margin;
                break;

            case ShellAlignment.Stretch:
                width = Math.Max(1, bounds.Width - (margin * 2));
                x = bounds.X + margin;
                break;

            default:
                width = ToPixels(widthDip, scale);
                x = bounds.X + ((bounds.Width - width) / 2);
                break;
        }

        var y = anchor switch
        {
            ShellAnchor.Top => bounds.Y + verticalMargin,
            _ => bounds.Y + bounds.Height - height - verticalMargin,
        };

        return new ShellPlacement(x, y, width, height);
    }

    /// <summary>
    /// Выбирает монитор, на котором должны лежать панели оболочки.
    /// </summary>
    /// <remarks>
    /// Основной монитор — первый выбор: панели оболочки следуют за системным понятием
    /// основного монитора, а не за монитором с активным окном. Если основной монитор в
    /// списке отсутствует (например, он отключён прямо сейчас), берётся первый подключённый.
    /// </remarks>
    /// <param name="monitors">Известные мониторы.</param>
    /// <returns>Целевой монитор либо <see langword="null"/>, если мониторов нет.</returns>
    public static MonitorInfo? SelectTarget(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        MonitorInfo? firstConnected = null;

        foreach (var monitor in monitors)
        {
            if (!monitor.IsConnected)
            {
                continue;
            }

            if (monitor.IsPrimary)
            {
                return monitor;
            }

            firstConnected ??= monitor;
        }

        return firstConnected;
    }

    private static int ToPixels(double value, double scale)
    {
        var pixels = (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
        return Math.Max(1, pixels);
    }
}
