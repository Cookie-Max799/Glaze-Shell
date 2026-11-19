using System.Text.Json;
using GlazeShell.Core.Theming;
using GlazeShell.Windows.Win32;
using Microsoft.Win32;

namespace GlazeShell.Windows.Theming;

/// <summary>
/// Исходное оформление Windows, запомненное перед первым применением темы Glaze.
/// Хранится в JSON рядом с настройками, поэтому восстанавливается и после падения
/// оболочки, и в следующем запуске.
/// </summary>
public sealed record SystemAppearanceSnapshot
{
    public int? AccentColor { get; init; }

    public int? AccentColorInactive { get; init; }

    public int? ColorPrevalence { get; init; }

    public int? StartColorMenu { get; init; }

    public int? AccentColorMenu { get; init; }

    /// <summary>Палитра акцента (32 байта) в Base64: JSON не хранит байтовые массивы.</summary>
    public string? AccentPalette { get; init; }

    public bool? AppsUseLightTheme { get; init; }

    public bool? SystemUsesLightTheme { get; init; }

    public bool? EnableTransparency { get; init; }
}

/// <summary>
/// Переносит оформление Glaze Shell на Windows: акцент, светлый или тёмный режим
/// приложений и прозрачность. Системные приложения (Параметры, Проводник, UWP) и
/// рамки окон подхватывают цвета оболочки, поэтому «вся система» выглядит одинаково.
/// </summary>
/// <remarks>
/// <para>
/// Все изменения — это значения текущего пользователя в реестре плюс одно
/// широковещательное <c>WM_SETTINGCHANGE</c>. Прав администратора не требуется,
/// хуков и опроса нет: запись происходит только при смене темы.
/// </para>
/// <para>
/// Перед первым изменением снимается снимок исходных значений. Он записывается на диск
/// до правки реестра: если приложение упадёт между записью и снимком, пользователь
/// остался бы с чужим акцентом и без возможности вернуть прежний.
/// </para>
/// <para>
/// Темы высокой контрастности не трогаются: системная палитра доступности важнее
/// оформления, и подмена цветов сделала бы интерфейс нечитаемым.
/// </para>
/// </remarks>
public sealed class SystemAppearance
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";
    private const string ExplorerAccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    private const string ImmersiveColorSet = "ImmersiveColorSet";

    private readonly string _snapshotPath;
    private readonly object _sync = new();

    private SystemAppearanceSnapshot? _snapshot;
    private bool _applied;

    public SystemAppearance(string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            throw new ArgumentException("A snapshot path is required.", nameof(snapshotPath));
        }

        _snapshotPath = snapshotPath;
    }

    /// <summary>Тема Glaze уже перенесена на Windows.</summary>
    public bool IsApplied
    {
        get
        {
            lock (_sync)
            {
                return _applied;
            }
        }
    }

    /// <summary>
    /// Красит Windows под тему Glaze. Возвращает true, если значения были записаны.
    /// </summary>
    /// <param name="accent">Акцентный цвет оболочки.</param>
    /// <param name="light">Светлая тема Glaze.</param>
    /// <param name="applyColorMode">
    /// Менять ли светлый/тёмный режим приложений. Для системной темы режим не навязывается:
    /// иначе выбор «системная» переставал бы что-либо значить.
    /// </param>
    /// <param name="transparency">Прозрачность Windows (эффекты прозрачности).</param>
    public bool Apply(GlazeColor accent, bool light, bool applyColorMode, bool transparency)
    {
        lock (_sync)
        {
            try
            {
                EnsureSnapshot();
                return Write(accent, light, applyColorMode, transparency);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Возвращает исходное оформление Windows. Безопасно вызывать повторно и когда
    /// снимка нет: тогда делать нечего.
    /// </summary>
    public void Restore()
    {
        lock (_sync)
        {
            try
            {
                var snapshot = _snapshot ?? LoadSnapshot();

                if (snapshot is null)
                {
                    _applied = false;
                    return;
                }

                RestoreValues(snapshot);
                BroadcastChange();
                _snapshot = null;
                _applied = false;
                DeleteSnapshot();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                // Восстановление не должно ронять оболочку: при следующем запуске
                // снимок ещё на диске, и попытка повторится.
            }
        }
    }

    /// <summary>
    /// Снимок исходных значений. Читается с диска один раз: повторные применения темы
    /// не должны запоминать уже наши же цвета.
    /// </summary>
    private void EnsureSnapshot()
    {
        if (_snapshot is not null)
        {
            return;
        }

        var existing = LoadSnapshot();

        if (existing is not null)
        {
            _snapshot = existing;
            return;
        }

        _snapshot = ReadSnapshot();
        SaveSnapshot(_snapshot);
    }

    private bool Write(GlazeColor accent, bool light, bool applyColorMode, bool transparency)
    {
        var accentAbgr = ToAbgr(accent);
        var changed = false;

        using (var dwm = Registry.CurrentUser.CreateSubKey(DwmKey, writable: true))
        {
            if (dwm is not null)
            {
                changed |= SetValue(dwm, "AccentColor", accentAbgr, RegistryValueKind.DWord);
                changed |= SetValue(dwm, "AccentColorInactive", ToAbgr(GlazePalette.Mix(accent, GlazeColor.Black, 0.35)), RegistryValueKind.DWord);
                changed |= SetValue(dwm, "ColorPrevalence", 1, RegistryValueKind.DWord);
            }
        }

        using (var explorer = Registry.CurrentUser.CreateSubKey(ExplorerAccentKey, writable: true))
        {
            if (explorer is not null)
            {
                changed |= SetValue(explorer, "StartColorMenu", accentAbgr, RegistryValueKind.DWord);
                changed |= SetValue(explorer, "AccentColorMenu", accentAbgr, RegistryValueKind.DWord);
                changed |= SetValue(explorer, "AccentPalette", BuildAccentPalette(accent), RegistryValueKind.Binary);
            }
        }

        if (applyColorMode)
        {
            using var personalize = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true);

            if (personalize is not null)
            {
                var mode = light ? 1 : 0;
                changed |= SetValue(personalize, "AppsUseLightTheme", mode, RegistryValueKind.DWord);
                changed |= SetValue(personalize, "SystemUsesLightTheme", mode, RegistryValueKind.DWord);
                changed |= SetValue(personalize, "EnableTransparency", transparency ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        if (changed || !_applied)
        {
            BroadcastChange();
        }

        _applied = true;
        return changed;
    }

    private static void RestoreValues(SystemAppearanceSnapshot snapshot)
    {
        using (var dwm = Registry.CurrentUser.CreateSubKey(DwmKey, writable: true))
        {
            RestoreValue(dwm, "AccentColor", snapshot.AccentColor);
            RestoreValue(dwm, "AccentColorInactive", snapshot.AccentColorInactive);
            RestoreValue(dwm, "ColorPrevalence", snapshot.ColorPrevalence);
        }

        using (var explorer = Registry.CurrentUser.CreateSubKey(ExplorerAccentKey, writable: true))
        {
            RestoreValue(explorer, "StartColorMenu", snapshot.StartColorMenu);
            RestoreValue(explorer, "AccentColorMenu", snapshot.AccentColorMenu);
            RestoreValue(explorer, "AccentPalette", DecodePalette(snapshot.AccentPalette));
        }

        using (var personalize = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true))
        {
            RestoreValue(personalize, "AppsUseLightTheme", snapshot.AppsUseLightTheme is { } apps ? (apps ? 1 : 0) : (object?)null);
            RestoreValue(personalize, "SystemUsesLightTheme", snapshot.SystemUsesLightTheme is { } system ? (system ? 1 : 0) : (object?)null);
            RestoreValue(personalize, "EnableTransparency", snapshot.EnableTransparency is { } transparency ? (transparency ? 1 : 0) : (object?)null);
        }
    }

    private static SystemAppearanceSnapshot ReadSnapshot()
    {
        return new SystemAppearanceSnapshot
        {
            AccentColor = ReadValue(DwmKey, "AccentColor"),
            AccentColorInactive = ReadValue(DwmKey, "AccentColorInactive"),
            ColorPrevalence = ReadValue(DwmKey, "ColorPrevalence"),
            StartColorMenu = ReadValue(ExplorerAccentKey, "StartColorMenu"),
            AccentColorMenu = ReadValue(ExplorerAccentKey, "AccentColorMenu"),
            AccentPalette = ReadPalette(),
            AppsUseLightTheme = ReadBool(PersonalizeKey, "AppsUseLightTheme"),
            SystemUsesLightTheme = ReadBool(PersonalizeKey, "SystemUsesLightTheme"),
            EnableTransparency = ReadBool(PersonalizeKey, "EnableTransparency"),
        };
    }

    private static int? ReadValue(string key, string name)
    {
        try
        {
            return Registry.GetValue($@"HKEY_CURRENT_USER\{key}", name, null) is int value ? value : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static bool? ReadBool(string key, string name) =>
        ReadValue(key, name) is { } value ? value != 0 : null;

    private static string? ReadPalette()
    {
        try
        {
            return Registry.GetValue($@"HKEY_CURRENT_USER\{ExplorerAccentKey}", "AccentPalette", null) is byte[] palette
                ? Convert.ToBase64String(palette)
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static byte[]? DecodePalette(string? palette)
    {
        if (string.IsNullOrWhiteSpace(palette))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(palette);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool SetValue(RegistryKey key, string name, object value, RegistryValueKind kind)
    {
        if (Equals(key.GetValue(name), value))
        {
            return false;
        }

        key.SetValue(name, value, kind);
        return true;
    }

    private static void RestoreValue(RegistryKey? key, string name, object? value)
    {
        if (key is null || value is null)
        {
            return;
        }

        key.SetValue(name, value);
    }

    /// <summary>
    /// Палитра акцента Windows: восемь оттенков от светлого к тёмному, по четыре байта
    /// на цвет в порядке BGRA. Из неё «Параметры» и меню «Пуск» рисуют градиенты.
    /// </summary>
    private static byte[] BuildAccentPalette(GlazeColor accent)
    {
        var shades = new[]
        {
            GlazePalette.Mix(accent, GlazeColor.White, 0.65),
            GlazePalette.Mix(accent, GlazeColor.White, 0.45),
            GlazePalette.Mix(accent, GlazeColor.White, 0.25),
            accent,
            GlazePalette.Mix(accent, GlazeColor.Black, 0.17),
            GlazePalette.Mix(accent, GlazeColor.Black, 0.34),
            GlazePalette.Mix(accent, GlazeColor.Black, 0.51),
            GlazePalette.Mix(accent, GlazeColor.Black, 0.68),
        };

        var palette = new byte[shades.Length * 4];

        for (var index = 0; index < shades.Length; index++)
        {
            var offset = index * 4;
            palette[offset] = shades[index].B;
            palette[offset + 1] = shades[index].G;
            palette[offset + 2] = shades[index].R;
            palette[offset + 3] = 0xFF;
        }

        return palette;
    }

    /// <summary>Цвет Windows хранится как <c>0xAABBGGRR</c>.</summary>
    private static int ToAbgr(GlazeColor color) =>
        (0xFF << 24) | (color.B << 16) | (color.G << 8) | color.R;

    private static void BroadcastChange()
    {
        _ = User32.SendMessageTimeoutW(
            User32.HwndBroadcast,
            User32.WmSettingChange,
            0,
            ImmersiveColorSet,
            User32.SmtoAbortIfHung,
            200,
            out _);
    }

    private SystemAppearanceSnapshot? LoadSnapshot()
    {
        try
        {
            if (!File.Exists(_snapshotPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SystemAppearanceSnapshot>(File.ReadAllText(_snapshotPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void SaveSnapshot(SystemAppearanceSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_snapshotPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_snapshotPath, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Снимок не сохранён: восстановление будет недоступно, но оформление применится.
        }
    }

    private void DeleteSnapshot()
    {
        try
        {
            if (File.Exists(_snapshotPath))
            {
                File.Delete(_snapshotPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Файл останется: при следующем запуске снимок прочитается и восстановление повторится.
        }
    }
}
