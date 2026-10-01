using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.DisplayManagement;

/// <summary>
/// Определяет, какие оконные сообщения означают изменение конфигурации дисплеев.
/// </summary>
/// <remarks>
/// Выделено в отдельный тип без состояния: решение принимается на основе номера сообщения
/// и значения <c>wParam</c> и не зависит ни от окна, ни от потока. Поэтому проверяется
/// обычным модульным тестом, а не рассылкой сообщений по всей системе — иначе результат
/// зависел бы от сообщений, отправленных параллельными тестами.
/// </remarks>
internal static class DisplaySignal
{
    /// <summary>
    /// Возвращает <c>true</c>, если сообщение означает изменение конфигурации дисплеев.
    /// </summary>
    /// <param name="message">Номер сообщения.</param>
    /// <param name="wParam">
    /// Первый параметр сообщения. Для <c>WM_DEVICECHANGE</c> содержит тип изменения
    /// устройства, для <c>WM_SETTINGCHANGE</c> — код изменяемой системной настройки.
    /// </param>
    /// <remarks>
    /// Отбор намеренно узкий: <c>WM_DEVICECHANGE</c> публикуется только для
    /// <c>DBT_DEVNODES_CHANGED</c> (появление или исчезновение узла устройства), а
    /// <c>WM_SETTINGCHANGE</c> — только для <c>SPI_SETWORKAREA</c> и
    /// <c>SPI_SETLOGICALDPIOVERRIDE</c>. Остальные сообщения этой группы приходят
    /// постоянно (смена клавиатуры, раскладки, темы) и не меняют ни состав, ни геометрию
    /// мониторов, поэтому перечисление мониторов на них было бы лишней работой.
    /// </remarks>
    internal static bool IsDisplaySignal(uint message, nint wParam)
    {
        switch (message)
        {
            case User32.WmDisplayChange:
            case User32.WmDpiChanged:
                return true;

            case User32.WmDeviceChange:
                return unchecked((uint)wParam) == User32.DevNodesChanged;

            case User32.WmSettingChange:
                var action = unchecked((uint)wParam);
                return action is User32.SpiSetWorkArea or User32.SpiSetLogicalDpiOverride;

            default:
                return false;
        }
    }
}
