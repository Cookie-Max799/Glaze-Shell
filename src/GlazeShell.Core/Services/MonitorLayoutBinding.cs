using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

/// <summary>
/// Результат сверки привязок вкладок к мониторам.
/// </summary>
public sealed record MonitorLayoutReconciliation
{
    public MonitorLayoutReconciliation(DesktopLayout layout, IReadOnlyList<string> clearedTabIds)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        ClearedTabIds = ModelValidation.Copy(clearedTabIds, nameof(clearedTabIds));
    }

    /// <summary>
    /// Layout с привязками только к существующим мониторам.
    /// </summary>
    public DesktopLayout Layout { get; }

    /// <summary>
    /// Идентификаторы вкладок, у которых привязка была снята, потому что монитор недоступен.
    /// </summary>
    public IReadOnlyList<string> ClearedTabIds { get; }

    public bool HasChanges => ClearedTabIds.Count > 0;
}

/// <summary>
/// Привязка desktop layout к мониторам. Остаётся framework-free: на вход принимаются
/// только модели Core, поэтому сервис можно использовать и при загрузке сохранённого layout,
/// и в тестах без Windows Integration.
/// </summary>
public static class MonitorLayoutBinding
{
    /// <summary>
    /// Снимает привязки вкладок, которые ссылаются на отсутствующие мониторы.
    /// Мониторы, которых нет в системе, не удаляют вкладки: вкладка остаётся в рабочем пространстве,
    /// но теряет мониторную привязку и может быть показана на любом дисплее.
    /// </summary>
    /// <remarks>
    /// Если список мониторов пуст или недоступен, layout возвращается без изменений: при отсутствии
    /// достоверных сведений о мониторах привязки не снимаются, чтобы не потерять настройку пользователя.
    /// </remarks>
    public static MonitorLayoutReconciliation Reconcile(DesktopLayout layout, IEnumerable<MonitorInfo>? monitors)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (monitors is null)
        {
            return new MonitorLayoutReconciliation(layout, Array.Empty<string>());
        }

        var knownMonitors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var monitor in monitors)
        {
            if (monitor is not null)
            {
                knownMonitors.Add(monitor.Id);
            }
        }

        if (knownMonitors.Count == 0 ||
            layout.Tabs.All(tab => tab.MonitorId is null || knownMonitors.Contains(tab.MonitorId)))
        {
            return new MonitorLayoutReconciliation(layout, Array.Empty<string>());
        }

        var cleared = new List<string>();
        var tabs = layout.Tabs
            .Select(tab =>
            {
                if (tab.MonitorId is null || knownMonitors.Contains(tab.MonitorId))
                {
                    return tab;
                }

                cleared.Add(tab.Id);
                return tab.WithMonitor(null);
            })
            .ToArray();

        return new MonitorLayoutReconciliation(new DesktopLayout(tabs, layout.ActiveTabId, layout.LayoutVersion), cleared);
    }
}
