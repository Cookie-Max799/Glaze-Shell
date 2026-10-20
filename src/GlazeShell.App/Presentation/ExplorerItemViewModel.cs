using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Files;
using GlazeShell.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

/// <summary>Вариант сортировки списка Explorer (E1) для выпадающего списка панели.</summary>
public sealed record ExplorerSortOption(FileSystemSortColumn Column, string Label);

/// <summary>Вариант вида списка Explorer (E1) для выпадающего списка панели.</summary>
public sealed record ExplorerViewOption(ExplorerViewMode Mode, string Label);

/// <summary>
/// Строка сайдбара Explorer (E2): системная папка, избранная папка или логический диск.
/// Диск отличается тем, что показывает заполненность, а избранное — тем, что его можно убрать.
/// </summary>
public sealed class ExplorerSidebarItemViewModel
{
    public ExplorerSidebarItemViewModel(
        string name,
        string path,
        string glyph,
        bool isFavorite = false,
        double? usedFraction = null,
        string? toolTip = null)
    {
        Name = name;
        Path = path;
        Glyph = glyph;
        IsFavorite = isFavorite;
        UsedFraction = usedFraction;
        ToolTip = toolTip ?? path;
    }

    public string Name { get; }

    public string Path { get; }

    /// <summary>Глиф Segoe Fluent Icons: папка, дом или диск.</summary>
    public string Glyph { get; }

    /// <summary>Избранное пользователя: такую строку можно убрать из сайдбара.</summary>
    public bool IsFavorite { get; }

    public double? UsedFraction { get; }

    /// <summary>Показывать ли полосу заполненности: только у диска с известным объёмом.</summary>
    public bool ShowUsage => UsedFraction is not null;

    /// <summary>Заполненность диска в процентах (0–100) для полосы прогресса.</summary>
    public double UsagePercent => (UsedFraction ?? 0) * 100;

    public string ToolTip { get; }
}

/// <summary>
/// Элемент списка Explorer (E1): элемент каталога вместе со значком и подписями колонок.
/// Настоящая иконка оболочки грузится асинхронно; до этого показывается глиф папки или
/// документа, поэтому список не «мигает» пустотой.
/// </summary>
public sealed class ExplorerItemViewModel : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public ExplorerItemViewModel(FileSystemEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FileSystemEntry Entry { get; }

    public string Name => Entry.Name;

    public string FullPath => Entry.FullPath;

    public bool IsFolder => Entry.IsFolder;

    /// <summary>Запасной значок до загрузки настоящей иконки оболочки.</summary>
    public string Glyph => IsFolder ? "\uE8B7" : "\uE8A5";

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (!Set(ref _icon, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(ShowGlyph));
        }
    }

    public bool HasIcon => _icon is not null;

    /// <summary>Запасной значок виден, пока настоящая иконка не загружена.</summary>
    public bool ShowGlyph => _icon is null;

    /// <summary>Колонка «Тип»: «Папка», расширение файла или «Файл».</summary>
    public string KindLabel =>
        IsFolder ? "Папка" : Entry.Extension.Length == 0 ? "Файл" : Entry.Extension.TrimStart('.').ToUpperInvariant();

    /// <summary>Колонка «Размер»: у папки размер не считается.</summary>
    public string SizeLabel => Entry.Size is { } size ? FormatSize(size) : "—";

    /// <summary>Колонка «Изменён»: местное время последней записи.</summary>
    public string ModifiedLabel => Entry.Modified is { } modified
        ? modified.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)
        : "—";

    public string ToolTip => FullPath;

    /// <summary>Человекочитаемый размер: байты, килобайты, мегабайты, гигабайты, терабайты.</summary>
    internal static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} Б";
        }

        string[] units = ["КБ", "МБ", "ГБ", "ТБ"];
        double value = bytes;
        var unit = -1;

        do
        {
            value /= 1024;
            unit++;
        }
        while (value >= 1024 && unit < units.Length - 1);

        return $"{value:0.#} {units[unit]}";
    }

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
