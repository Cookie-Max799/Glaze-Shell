using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Audio;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Состояние popover «Центр управления» (функция D1): уровень и режим «без звука» устройства
/// вывода по умолчанию (Core Audio), режим оформления Glaze Shell (H1) и быстрые ссылки на
/// системные настройки.
/// </summary>
/// <remarks>
/// Тумблеры радио (Wi-Fi, Bluetooth, авиарежим) требуют разрешения на доступ к радио, которое
/// Win32-приложению без упаковки Windows не выдаёт, поэтому в базовом слайсе вместо них —
/// ссылки на страницы настроек; яркость (WMI/DDC-CI) идёт следующим шагом.
/// </remarks>
public sealed class QuickSettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsManager _settings;
    private readonly SystemVolumeProvider _volume;

    private bool _isOpen;
    private bool _isVolumeAvailable;
    private double _volumePercent;
    private bool _isMuted;
    private ThemeMode _themeMode;

    public QuickSettingsViewModel(ISettingsManager settings, SystemVolumeProvider volume)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _themeMode = settings.GetSettings().ThemeMode;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Открыт ли popover. При открытии громкость перечитывается сразу.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (!Set(ref _isOpen, value) || !value)
            {
                return;
            }

            RefreshVolume();
        }
    }

    /// <summary>Есть ли устройство вывода: иначе ползунок и кнопка «без звука» отключаются.</summary>
    public bool IsVolumeAvailable
    {
        get => _isVolumeAvailable;
        private set
        {
            if (Set(ref _isVolumeAvailable, value))
            {
                OnPropertyChanged(nameof(HasNoVolume));
            }
        }
    }

    /// <summary>Устройство вывода не найдено: показывается пояснение вместо ползунка.</summary>
    public bool HasNoVolume => !_isVolumeAvailable;

    /// <summary>Уровень громкости в процентах (0–100). Изменение применяется сразу.</summary>
    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (!Set(ref _volumePercent, Math.Clamp(value, 0, 100)))
            {
                return;
            }

            _ = _volume.TrySetVolume((int)Math.Round(_volumePercent));
            OnPropertyChanged(nameof(VolumeLabel));
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (Set(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(VolumeGlyph));
                OnPropertyChanged(nameof(VolumeLabel));
            }
        }
    }

    /// <summary>Глиф кнопки «без звука»: динамик или перечёркнутый динамик.</summary>
    public string VolumeGlyph => _isMuted ? "\uE74F" : "\uE767";

    /// <summary>Подпись уровня: проценты или «Выключен».</summary>
    public string VolumeLabel => _isMuted
        ? "Выключен"
        : string.Create(CultureInfo.CurrentCulture, $"{(int)Math.Round(_volumePercent)}%");

    public IReadOnlyList<ThemeModeOption> ThemeModeOptions { get; } = ThemeOptions.CreateThemeModes();

    /// <summary>
    /// Режим оформления Glaze Shell. Сохраняется в настройках, поэтому смена из popover
    /// сразу применяется и к главному окну.
    /// </summary>
    public ThemeModeOption SelectedThemeMode
    {
        get => ThemeModeOptions.FirstOrDefault(option => option.Mode == _themeMode) ?? ThemeModeOptions[0];
        set
        {
            if (value is null || value.Mode == _themeMode)
            {
                return;
            }

            _themeMode = value.Mode;
            SaveThemeMode(value.Mode);
            OnPropertyChanged();
        }
    }

    /// <summary>Переключает режим «без звука».</summary>
    public void ToggleMute()
    {
        if (_volume.TrySetMute(!_isMuted))
        {
            IsMuted = !_isMuted;
        }
    }

    /// <summary>
    /// Перечитывает громкость устройства: вызывается раз в секунду, пока popover открыт,
    /// поэтому изменение извне (клавиши мультимедиа, системный микшер) видно в интерфейсе.
    /// </summary>
    public void RefreshVolume()
    {
        if (!_isOpen)
        {
            return;
        }

        if (!_volume.TryGetVolume(out var percent, out var muted))
        {
            IsVolumeAvailable = false;
            return;
        }

        IsVolumeAvailable = true;

        // Пока идёт перетаскивание, ползунок уже показывает это значение: перезаписывать
        // его тем же числом не нужно, иначе курсор «дёргался» бы.
        if (Math.Abs(percent - _volumePercent) > 0.5)
        {
            _volumePercent = percent;
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(VolumeLabel));
        }

        IsMuted = muted;
    }

    public void Dispose() => _volume.Dispose();

    private void SaveThemeMode(ThemeMode mode)
    {
        var settings = _settings.GetSettings();

        _settings.SetSettings(new UserSettings(
            settings.StartWithWindows,
            settings.MinimizeToTray,
            settings.ShowDesktopOnStartup,
            settings.RestoreLastLayout,
            settings.Language,
            settings.ActiveThemeId,
            settings.MonitorNames,
            mode,
            settings.AccentId,
            settings.BaseColorId,
            settings.TopBarEnabled,
            settings.DockEnabled,
            settings.DockPinnedApps,
            settings.DockReplacesTaskbar,
            settings.ExplorerFavorites,
            settings.ExplorerViewMode,
            settings.ReduceMotion,
            settings.HighContrast,
            settings.RecoveryHotkeyEnabled,
            settings.RecoveryHotkeyId,
            settings.SafeModeEnabled));
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
