using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GlazeShell.Infrastructure.Logging;

public sealed class FileLogger : IGlazeLogger
{
    public const long DefaultMaxFileSizeBytes = 1024 * 1024;
    public const int DefaultMaxFileCount = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();
    private readonly string _logFilePath;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxFileCount;

    public FileLogger(
        string logFilePath,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        int maxFileCount = DefaultMaxFileCount)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException("A log file path is required.", nameof(logFilePath));
        }

        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), maxFileSizeBytes, "The maximum log size must be positive.");
        }

        if (maxFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileCount), maxFileCount, "At least one log file must be retained.");
        }

        _logFilePath = logFilePath;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxFileCount = maxFileCount;

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
            LogRedactor.Redact(message),
            exception is null ? null : LogRedactor.Redact(exception.ToString()));
        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        var lineLength = Encoding.UTF8.GetByteCount(line);

        lock (_sync)
        {
            try
            {
                RollIfNeeded(lineLength);
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

    private void RollIfNeeded(long incomingLength)
    {
        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length + incomingLength <= _maxFileSizeBytes)
        {
            return;
        }

        Roll();
    }

    private void Roll()
    {
        var obsolete = GetArchivePath(_maxFileCount - 1);
        if (File.Exists(obsolete))
        {
            File.Delete(obsolete);
        }

        for (var index = _maxFileCount - 2; index >= 0; index--)
        {
            var source = GetArchivePath(index);
            if (File.Exists(source))
            {
                File.Move(source, GetArchivePath(index + 1), overwrite: true);
            }
        }

        File.Move(_logFilePath, GetArchivePath(0), overwrite: true);
    }

    private string GetArchivePath(int index) =>
        index == 0 ? _logFilePath + ".1" : $"{_logFilePath}.{index}";
}
