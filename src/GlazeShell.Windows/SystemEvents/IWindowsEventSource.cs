namespace GlazeShell.Windows.SystemEvents;

/// <summary>
/// Источник системных событий Windows с явным жизненным циклом.
/// </summary>
/// <remarks>
/// Контракт описывает только запуск и остановку источника, а не запросы состояния:
/// поэтому интерфейс живёт в слое Windows Integration, а не в Core. Core не знает
/// о WinEvent hooks и message windows, но и не должен ими владеть.
/// </remarks>
public interface IWindowsEventSource : IDisposable
{
    /// <summary>
    /// Запускает источник. Повторный вызов на уже запущенном источнике возвращает
    /// <c>true</c> и ничего не меняет.
    /// </summary>
    /// <returns><c>false</c>, если источник не удалось запустить в текущей среде.</returns>
    bool Start();
}
