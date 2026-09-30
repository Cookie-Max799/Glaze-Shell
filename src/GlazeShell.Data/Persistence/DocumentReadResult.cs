using System.Text;

namespace GlazeShell.Data.Persistence;

/// <summary>
/// Причина, по которой документ не был возвращён вызывающей стороне.
/// </summary>
internal enum DocumentReadStatus
{
    Loaded = 0,
    Missing = 1,
    TooLarge = 2,
    Unreadable = 3
}

internal readonly record struct DocumentReadResult(DocumentReadStatus Status, string? Content, string? Failure)
{
    public static DocumentReadResult Loaded(string content) => new(DocumentReadStatus.Loaded, content, null);

    public static DocumentReadResult Missing() => new(DocumentReadStatus.Missing, null, null);

    public static DocumentReadResult TooLarge(string failure) => new(DocumentReadStatus.TooLarge, null, failure);

    public static DocumentReadResult Unreadable(string failure) => new(DocumentReadStatus.Unreadable, null, failure);
}
