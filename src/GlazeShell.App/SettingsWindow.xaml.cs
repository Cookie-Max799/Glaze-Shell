using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Окно настроек (I1, базовый слайс): одиночное окно с сайдбаром страниц и панелью настроек.
/// Создаётся из главного окна кнопкой «Настройки» и переиспользуется, чтобы не плодить окна.
/// Настройки применяются вживую через <see cref="ISettingsManager"/>.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const int DefaultWidth = 880;
    private const int DefaultHeight = 680;

    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;

    public SettingsWindow(ISettingsManager settings, IEventManager events, ThemeApplicator themes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(events);

        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        ViewModel = new SettingsViewModel(settings, events);

        InitializeComponent();
        Title = "Настройки Glaze Shell";

        _themes.ApplyFromSettings(Root);
        _settingsChanged = events.Subscribe<SettingsChanged>(_ => _themes.ApplyFromSettings(Root));

        AppWindow.Resize(new SizeInt32(DefaultWidth, DefaultHeight));
        Closed += OnClosed;
    }

    public SettingsViewModel ViewModel { get; }

    private void OnResetPageClick(object sender, RoutedEventArgs e) => ViewModel.ResetSelectedPage();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _settingsChanged.Dispose();
        ViewModel.Dispose();
    }
}
