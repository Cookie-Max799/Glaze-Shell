namespace GlazeShell.Core.Glance;

/// <summary>
/// Категория результата глобального поиска Glance (B1). Категория определяет, как UI
/// оформляет строку и какое действие выполняется при открытии.
/// </summary>
public enum GlanceResultKind
{
    /// <summary>Приложение из меню «Пуск» или упакованное (запускается лаунчером).</summary>
    Application,

    /// <summary>Открытое окно (переключение на него).</summary>
    Window,

    /// <summary>Страница системных настроек (<c>ms-settings:</c>).</summary>
    Setting,

    /// <summary>Команда Glaze Shell (открыть окно, включить панель и т.п.).</summary>
    Command,
}
