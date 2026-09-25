using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GlazeShell.Infrastructure.Logging;

public sealed class FileLogger : IGlazeLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();
    private readonly string _logFilePath;

    public FileLogger(string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException("A log file path is required.", nameof(logFilePath));
        }

        _logFilePath = logFilePath;
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Write(GlazeLogLevel level, string component, string message, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("A log component is required.", nameof(component));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A log message is required.", nameof(message));
        }

        var entry = new LogEntry(
            DateTimeOffset.UtcNow,
            level,
            component,
            message,
            exception?.ToString());
        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;

        lock (_sync)
        {
            try
            {
                File.AppendAllText(_logFilePath, line, Encoding.UTF8);
            }
            catch (IOException exceptionInfo)
            {
                Debug.WriteLine($"Glaze Shell log write failed: {exceptionInfo.Message}");
            }
            catch (UnauthorizedAccessException exceptionInfo)
            {
                Debug.WriteLine($"Glaze Shell log write failed: {exceptionInfo.Message}");
            }
        }
    }
}
