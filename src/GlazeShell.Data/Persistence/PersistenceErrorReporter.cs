namespace GlazeShell.Data.Persistence;

/// <summary>
/// Ошибка, о которой нужно сообщить вызывающей стороне, не прерывая работу приложения.
/// Реализация поставляется composition root и, как правило, пишет запись в лог.
/// </summary>
public delegate void PersistenceErrorReporter(string message, Exception? exception);
