using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Preview;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние карточки Quick Preview (B2): чтение предпросмотра в фоне, выбор способа
/// показа (картинка, текст, сводка папки или только глиф с метаданными) и подписи.
/// </summary>
/// <remarks>
/// Предпросмотр никогда не выполняет содержимое файла: текст только читается, картинка
/// декодируется WinUI, остальное показывается метаданными с кнопкой «Открыть».
/// </remarks>
public sealed class PreviewViewModel : INotifyPropertyChanged
{
    /// <summary>Ограничение декодирования: файл крупнее этого размера не рисуется в карточке.</summary>
    private const long MaxImageBytes = 512L * 1024 * 1024;

    private readonly IFilePreviewReader _reader;
    private readonly string _path;

    private string _title = string.Empty;
    private string _metaLine = string.Empty;
    private string _content = string.Empty;
    private string _statusText = string.Empty;
    private string _glyph = "\uE8A5";
    private ImageSource? _image;
    private bool _isBusy = true;
    private bool _showImage;
    private bool _showText;
    private bool _showGlyph = true;
    private bool _isTruncated;

    public PreviewViewModel(IFilePreviewReader reader, string path)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Полный путь файла — для «Открыть» и «Копировать путь».</summary>
    public string TargetPath => _path;

    /// <summary>Имя файла — в заголовке карточки.</summary>
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    /// <summary>Строка метаданных: вид, размер и дата изменения.</summary>
    public string MetaLine
    {
        get => _metaLine;
        private set => Set(ref _metaLine, value);
    }

    /// <summary>Текст файла или сводка папки.</summary>
    public string Content
    {
        get => _content;
        private set => Set(ref _content, value);
    }

    /// <summary>Нижняя строка: полный путь и примечания (обрезка текста, причина отказа).</summary>
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>Крупный глиф, когда показывать нечего (аудио, видео, архив, отказ чтения).</summary>
    public string Glyph
    {
        get => _glyph;
        private set => Set(ref _glyph, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public ImageSource? Image
    {
        get => _image;
        private set => Set(ref _image, value);
    }

    public bool ShowImage
    {
        get => _showImage;
        private set => Set(ref _showImage, value);
    }

    public bool ShowText
    {
        get => _showText;
        private set => Set(ref _showText, value);
    }

    public bool ShowGlyph
    {
        get => _showGlyph;
        private set => Set(ref _showGlyph, value);
    }

    /// <summary>Текст показан не весь: внизу появляется примечание об обрезке.</summary>
    public bool IsTruncated
    {
        get => _isTruncated;
        private set => Set(ref _isTruncated, value);
    }

    /// <summary>Читает предпросмотр и решает, как его показывать. Вызывается один раз при открытии.</summary>
    public async Task LoadAsync()
    {
        var preview = await Task.Run(() => _reader.Read(_path)).ConfigureAwait(true);

        IsBusy = false;

        if (preview is null)
        {
            StatusText = $"{_path} · предпросмотр недоступен";
            return;
        }

        Title = preview.Title;
        MetaLine = FormatMeta(preview);
        StatusText = preview.FullPath;

        switch (preview.Kind)
        {
            case FilePreviewKind.Folder:
                ShowFolder(preview);
                break;

            case FilePreviewKind.Text:
                ShowTextPreview(preview);
                break;

            case FilePreviewKind.Image:
                await ShowImagePreviewAsync(preview).ConfigureAwait(true);
                break;

            default:
                Glyph = GlyphFor(preview.Kind);
                break;
        }
    }

    /// <summary>
    /// Декодер отказал на картинке (битый файл): карточка деградирует до глифа с
    /// метаданными, а не показывает сломанное изображение.
    /// </summary>
    public void ReportImageFailure()
    {
        ShowImage = false;
        ShowGlyph = true;
        Image = null;
        StatusText = $"{_path} · не удалось открыть изображение";
    }

    private void ShowFolder(FilePreview preview)
    {
        if (preview.ItemCount is not { } count)
        {
            Glyph = GlyphFor(FilePreviewKind.Folder);
            return;
        }

        Content = preview.IsTruncated
            ? string.Create(CultureInfo.CurrentCulture, $"Элементов: {count:N0} и более")
            : string.Create(CultureInfo.CurrentCulture, $"Элементов: {count:N0}");

        ShowGlyph = false;
        ShowText = true;
    }

    private void ShowTextPreview(FilePreview preview)
    {
        if (preview.Content is not { } text || text.Length == 0)
        {
            // Двоичный файл под текстовым расширением: показываем метаданные вместо мусора.
            Glyph = GlyphFor(preview.Kind);
            StatusText = $"{preview.FullPath} · содержимое не является текстом";
            return;
        }

        Content = text;
        IsTruncated = preview.IsTruncated;
        ShowGlyph = false;
        ShowText = true;

        if (preview.IsTruncated)
        {
            StatusText = $"{preview.FullPath} · показана первая часть файла";
        }
    }

    private async Task ShowImagePreviewAsync(FilePreview preview)
    {
        if (preview.SizeBytes is { } size && size > MaxImageBytes)
        {
            Glyph = GlyphFor(preview.Kind);
            StatusText = $"{preview.FullPath} · файл слишком большой для предпросмотра";
            return;
        }

        try
        {
            // WinUI декодирует файл самостоятельно и не удерживает его открытым.
            Image = new BitmapImage(new Uri(preview.FullPath));
            ShowGlyph = false;
            ShowImage = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            ReportImageFailure();
        }
    }

    private static string FormatMeta(FilePreview preview)
    {
        var parts = new List<string>(4) { KindLabel(preview) };

        if (preview.SizeBytes is { } size)
        {
            parts.Add(ExplorerItemViewModel.FormatSize(size));
        }

        if (preview.Modified is { } modified)
        {
            parts.Add(modified.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));
        }

        return string.Join(" · ", parts);
    }

    private static string KindLabel(FilePreview preview) => preview.Kind switch
    {
        FilePreviewKind.Folder => "Папка",
        FilePreviewKind.Image => "Изображение",
        FilePreviewKind.Text => "Текст",
        FilePreviewKind.Audio => "Аудио",
        FilePreviewKind.Video => "Видео",
        FilePreviewKind.Archive => "Архив",
        _ => "Файл",
    };

    private static string GlyphFor(FilePreviewKind kind) => kind switch
    {
        FilePreviewKind.Folder => "\uE8B7",
        FilePreviewKind.Image => "\uE91B",
        FilePreviewKind.Text => "\uE8A5",
        FilePreviewKind.Audio => "\uE8D6",
        FilePreviewKind.Video => "\uE8B2",
        FilePreviewKind.Archive => "\uE8F4",
        _ => "\uE7C3",
    };

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
