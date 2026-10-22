using GlazeShell.Core.Display;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Целевой монитор панелей оболочки (функция I6).
/// </summary>
/// <remarks>
/// <para>
/// Панели оболочки следуют за системным понятием основного монитора, а не за монитором
/// с активным окном: иначе панель уезжала бы при каждом переключении между приложениями.
/// Правило выбора живёт в ядре (<see cref="ShellPlacementResolver.SelectTarget"/>), а этот
/// класс только кэширует результат.
/// </para>
/// <para>
/// Мониторы перечисляются один раз, а не при каждой перерисовке панели: перечисление
/// обращается к Win32, а панели перерисовываются часто. Список приходит извне при
/// изменении конфигурации дисплеев — тогда кэш обновляется без повторного перечисления.
/// </para>
/// </remarks>
public sealed class ShellMonitorTarget
{
    private readonly IMonitorManager _monitors;
    private MonitorInfo? _target;
    private bool _resolved;

    public ShellMonitorTarget(IMonitorManager monitors)
    {
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
    }

    /// <summary>
    /// Монитор, на котором должны лежать панели оболочки, либо <see langword="null"/>,
    /// если мониторы неизвестны.
    /// </summary>
    public MonitorInfo? Target
    {
        get
        {
            if (!_resolved)
            {
                Resolve();
            }

            return _target;
        }
    }

    /// <summary>
    /// Принимает новый список мониторов: конфигурация дисплеев изменилась, поэтому
    /// целевой монитор выбирается заново.
    /// </summary>
    public void Update(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        _resolved = true;
        _target = ShellPlacementResolver.SelectTarget(monitors);
    }

    private void Resolve()
    {
        _resolved = true;

        try
        {
            _target = ShellPlacementResolver.SelectTarget(_monitors.GetMonitors());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Перечисление мониторов может упасть, если устройство отключается в этот
            // момент. Панели в этом случае остаются там, где были, — лучше, чем исчезнуть.
            _target = null;
        }
    }
}
