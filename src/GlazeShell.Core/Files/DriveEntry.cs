namespace GlazeShell.Core.Files;

/// <summary>Тип логического диска для сайдбара Explorer (E2).</summary>
public enum DriveKind
{
    Fixed = 0,
    Removable = 1,
    Network = 2,
    Optical = 3,
    Ram = 4,
    Other = 5
}

/// <summary>
/// Логический диск (E2): корневой путь, метка тома и заполненность. Объём и свободное место
/// читаются отдельно от перечисления, поэтому у неготового диска (пустой DVD-привод,
/// отключённая сетевая папка) они равны <c>null</c> — диск при этом остаётся в списке.
/// </summary>
public sealed record DriveEntry(
    string RootPath,
    string? Label,
    DriveKind Kind,
    long? TotalBytes,
    long? FreeBytes)
{
    /// <summary>
    /// Съёмный или оптический носитель: показывается отдельной секцией сайдбара, потому что
    /// такой диск подключается и отключается на ходу.
    /// </summary>
    public bool IsRemovable => Kind is DriveKind.Removable or DriveKind.Optical;

    /// <summary>Доля занятого места от 0 до 1 или <c>null</c>, если объём неизвестен.</summary>
    public double? UsedFraction =>
        TotalBytes is > 0 && FreeBytes is not null
            ? 1d - ((double)FreeBytes.Value / TotalBytes.Value)
            : null;
}
