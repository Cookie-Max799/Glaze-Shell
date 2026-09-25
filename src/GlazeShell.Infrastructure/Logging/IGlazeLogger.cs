namespace GlazeShell.Infrastructure.Logging;

public interface IGlazeLogger
{
    void Write(GlazeLogLevel level, string component, string message, Exception? exception = null);
}
