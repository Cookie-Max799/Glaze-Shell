namespace GlazeShell.Core.Preview;

/// <summary>
/// Вид содержимого для предпросмотра (B2): определяет, как карточка показывает файл —
/// картинкой, текстом, сводкой папки или только метаданными.
/// </summary>
public enum FilePreviewKind
{
    Other = 0,
    Folder = 1,
    Image = 2,
    Text = 3,
    Audio = 4,
    Video = 5,
    Archive = 6,
}
