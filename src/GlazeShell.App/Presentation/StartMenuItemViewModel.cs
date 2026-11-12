using System.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GlazeShell.App.Presentation;

/// <summary>Плитка приложения в меню «Пуск».</summary>
public sealed class StartMenuItemViewModel : INotifyPropertyChanged
{
    private WriteableBitmap? _icon;

    public StartMenuItemViewModel(string applicationId, string name, string? executablePath = null)
    {
        ApplicationId = applicationId;
        Name = name;
        ExecutablePath = executablePath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ApplicationId { get; }

    public string Name { get; }

    /// <summary>Путь к исполняемому файлу; по нему загружается значок.</summary>
    public string? ExecutablePath { get; }

    /// <summary>Значок; приходит из фона позже и обновляет плитку по уведомлению.</summary>
    public WriteableBitmap? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public override string ToString() => Name;
}
