using GlazeShell.Windows.SystemAccessibility;

namespace GlazeShell.Windows.Tests.SystemAccessibility;

[TestClass]
public sealed class SystemAccessibilityProviderTests
{
    /// <summary>
    /// Чтение системных настроек доступности должно быть безопасным: агент сборки может
    /// работать без системной темы высокой контрастности и без анимаций, но вызов не должен
    /// бросать исключение — иначе оболочка не запустится.
    /// </summary>
    [TestMethod]
    public void ReadingSystemStateIsSafe()
    {
        _ = SystemAccessibilityProvider.GetInfo();
    }

    /// <summary>
    /// Системные флаги читаются стабильно: два подряд идущих чтения дают один и тот же
    /// ответ, иначе оформление мигало бы при каждой перерисовке.
    /// </summary>
    [TestMethod]
    public void SystemFlagsAreStableBetweenReads()
    {
        var highContrast = SystemAccessibilityProvider.IsHighContrastEnabled();
        var animations = SystemAccessibilityProvider.AreAnimationsEnabled();

        Assert.AreEqual(highContrast, SystemAccessibilityProvider.IsHighContrastEnabled());
        Assert.AreEqual(animations, SystemAccessibilityProvider.AreAnimationsEnabled());
    }

    /// <summary>
    /// Системная палитра используется только в контрастном режиме, поэтому цвета должны быть
    /// непрозрачными: иначе текст поверх системного фона окажется нечитаемым.
    /// </summary>
    [TestMethod]
    public void SystemPaletteIsOpaque()
    {
        var palette = SystemAccessibilityProvider.GetInfo().Palette;

        Assert.AreEqual(255, palette.Background.A);
        Assert.AreEqual(255, palette.Foreground.A);
        Assert.AreEqual(255, palette.Highlight.A);
        Assert.AreEqual(255, palette.Surface.A);
        Assert.AreEqual(255, palette.Muted.A);
    }
}
