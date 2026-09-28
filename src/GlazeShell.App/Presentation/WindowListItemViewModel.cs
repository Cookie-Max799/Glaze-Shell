using System.ComponentModel;
using System.Runtime.CompilerServices;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

public sealed class WindowListItemViewModel : INotifyPropertyChanged
{
    private readonly IWindowManager _windows;
    private WindowInfo _window;

    public WindowListItemViewModel(WindowInfo window, IWindowManager windows)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));

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
        ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0xDE, 0x80))
        : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x58, 0x66));

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}