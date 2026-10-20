using System.ComponentModel;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

public sealed class WindowListItemViewModel : INotifyPropertyChanged
{
    private static readonly SolidColorBrush ActiveFallback = new(ColorHelper.FromArgb(0xFF, 0x3F, 0xCF, 0x9B));
    private static readonly SolidColorBrush InactiveFallback = new(ColorHelper.FromArgb(0xFF, 0xA5, 0xA2, 0xC8));

    private readonly IWindowManager _windows;
    private readonly Func<string?, string?>? _monitorName;
    private WindowInfo _window;

    public WindowListItemViewModel(
        WindowInfo window,
        IWindowManager windows,
        Func<string?, string?>? monitorName = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _monitorName = monitorName;

        FocusCommand = new AsyncCommand(FocusAsync);
        MinimizeCommand = new AsyncCommand(MinimizeAsync);
        MaximizeCommand = new AsyncCommand(MaximizeAsync);
        RestoreCommand = new AsyncCommand(RestoreAsync);
        CloseCommand = new AsyncCommand(CloseAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WindowInfo Window => _window;

    public string Id => _window.Id;

    public string Title => _window.Title;

    public int ProcessId => _window.ProcessId;

    public string? ProcessName => _window.ProcessName;

    public string? ExecutablePath => _window.ExecutablePath;

    /// <summary>
    /// Дружелюбное имя монитора, на котором открыто окно. Пусто, если монитор не определён.
    /// </summary>
    public string? MonitorLabel => string.IsNullOrEmpty(_window.MonitorId) ? null : _monitorName?.Invoke(_window.MonitorId);

    public bool HasMonitor => !string.IsNullOrEmpty(MonitorLabel);

    public bool IsActive => _window.IsForeground;

    public string ActiveGlyph => IsActive ? "●" : "○";

    public string StateText => (_window.State, IsActive) switch
    {
        (WindowState.Minimized, _) => "Свернуто",
        (WindowState.Maximized, _) => "Развернуто",
        (_, true) => "Активно",
        _ => "Открыто",
    };

    public SolidColorBrush ActiveBrush => IsActive
        ? Resource("GlazeStatusSuccessBrush", ActiveFallback)
        : Resource("GlazeTextMutedBrush", InactiveFallback);

    public AsyncCommand FocusCommand { get; }

    public AsyncCommand MinimizeCommand { get; }

    public AsyncCommand MaximizeCommand { get; }

    public AsyncCommand RestoreCommand { get; }

    public AsyncCommand CloseCommand { get; }

    public void Update(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;

        OnPropertyChanged(nameof(Window));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ProcessId));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(MonitorLabel));
        OnPropertyChanged(nameof(HasMonitor));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActiveGlyph));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ActiveBrush));
    }

    private Task FocusAsync()
    {
        _ = _windows.FocusWindow(Id);
        return Task.CompletedTask;
    }

    private Task MinimizeAsync()
    {
        _ = _windows.MinimizeWindow(Id);
        return Task.CompletedTask;
    }

    private Task MaximizeAsync()
    {
        _ = _windows.MaximizeWindow(Id);
        return Task.CompletedTask;
    }

    private Task RestoreAsync()
    {
        _ = _windows.RestoreWindow(Id);
        return Task.CompletedTask;
    }

    private Task CloseAsync()
    {
        _ = _windows.CloseWindow(Id);
        return Task.CompletedTask;
    }

    private static SolidColorBrush Resource(string key, SolidColorBrush fallback) =>
        Microsoft.UI.Xaml.Application.Current is { } app
            && app.Resources.TryGetValue(key, out var value)
            && value is SolidColorBrush brush
            ? brush
            : fallback;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}