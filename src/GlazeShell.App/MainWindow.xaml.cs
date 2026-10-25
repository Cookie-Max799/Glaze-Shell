using System.ComponentModel;
using System.IO;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace GlazeShell.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 760;
    private const int MinWidth = 960;
    private const int MinHeight = 520;

    private readonly ThemeApplicator _themes;

    public MainWindow(
        string title,
        IApplicationManager applications,
        IApplicationLauncher launcher,
        IWindowManager windows,
        IMonitorManager monitors,
        IDesktopManager desktop,
        ISettingsManager settings,
        IEventManager events,
        ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        ViewModel = new MainViewModel(applications, launcher, windows, monitors, desktop, settings, events, DispatcherQueue);
        InitializeComponent();
        Title = title;

        _themes.ApplyFromSettings(Root);
        ViewModel.ThemeSelectionChanged += OnThemeSelectionChanged;

        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), handledEventsToo: true);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyWindowIcon();
        ApplyInitialSizeAndPosition();
        Activate();

        ViewModel.Initialize();
        _ = ViewModel.RefreshAsync();
    }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Просьба открыть проводник Glaze Shell: папку можно передать аргументом, <c>null</c>
    /// означает папку по умолчанию. Окном Explorer владеет приложение, а не главное окно.
    /// </summary>
    public event EventHandler<string?>? ExplorerRequested;

    /// <summary>
    /// Запрос на открытие окна настроек (I1). Окном владеет приложение, а не главное окно.
    /// </summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Запрос на восстановление интерфейса Windows (I2): панель задач и значки рабочего
    /// стола возвращаются на место. Восстановлением занимается приложение, а не окно.
    /// </summary>
    public event EventHandler? RecoveryRequested;

    private void OnThemeSelectionChanged(object? sender, ThemeSelectionChangedEventArgs e) =>
        _themes.Apply(Root, e.Mode, e.AccentId);

    private void OnRecoveryClick(object sender, RoutedEventArgs e) => RecoveryRequested?.Invoke(this, EventArgs.Empty);

    private void OnExplorerClick(object sender, RoutedEventArgs e) => ExplorerRequested?.Invoke(this, null);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        ViewModel.RefreshCommand.Execute(null);

    private void OnLaunchClick(object sender, RoutedEventArgs e) =>
        ViewModel.LaunchCommand.Execute(null);

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        ViewModel.CloseCommand.Execute(null);

    private void OnRestartClick(object sender, RoutedEventArgs e) =>
        ViewModel.RestartCommand.Execute(null);

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ApplicationListViewModel { Path.Length: > 0 } item })
        {
            return;
        }

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(item.Path);
        Clipboard.SetContent(package);

        ViewModel.NotifyPathCopied(item.Path);
    }

    private async void OnTerminateClick(object sender, RoutedEventArgs e)
    {
        var target = ViewModel.Selected;

        if (target is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Завершить принудительно?",
            Content = $"Приложение «{target.Name}» и его фоновые процессы будут сняты без сохранения данных. Продолжить?",
            PrimaryButtonText = "Завершить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.TerminateSelectedAsync();
        }
    }

    /// <summary>
    /// Переименование дисплея. «Сохранить» задаёт пользовательское имя, «Сбросить» возвращает
    /// имя по умолчанию, «Отмена» ничего не меняет.
    /// </summary>
    private async void OnRenameMonitorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MonitorListItemViewModel monitor })
        {
            return;
        }

        var input = new TextBox
        {
            Text = monitor.DisplayName,
            MaxLength = 64,
            SelectionStart = monitor.DisplayName.Length,
        };

        var dialog = new ContentDialog
        {
            Title = "Имя дисплея",
            Content = input,
            PrimaryButtonText = "Сохранить",
            SecondaryButtonText = "Сбросить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };

        var result = await dialog.ShowAsync();

        switch (result)
        {
            case ContentDialogResult.Primary:
                ViewModel.RenameMonitor(monitor, input.Text);
                break;

            case ContentDialogResult.Secondary:
                ViewModel.RenameMonitor(monitor, null);
                break;
        }
    }

    /// <summary>
    /// WinUI ListView не подносит элемент к видимой области при программной смене
    /// SelectedItem, поэтому прокрутку выполняем сами. Вызов откладывается на диспетчер,
    /// чтобы привязка SelectedItem успела примениться до ScrollIntoView.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Selected))
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.Selected is { } selected)
            {
                ApplicationList.ScrollIntoView(selected);
            }
        });
    }

    private void ApplyInitialSizeAndPosition()
    {
        var appWindow = ResolveAppWindow();
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var width = Math.Clamp(workArea.Width / 2, MinWidth, DefaultWidth);
        var height = Math.Clamp(workArea.Height / 2, MinHeight, DefaultHeight);
        var size = new global::Windows.Graphics.SizeInt32(width, height);

        appWindow.Resize(size);
        appWindow.Move(new global::Windows.Graphics.PointInt32(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2)));
    }

    private AppWindow ResolveAppWindow()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    /// <summary>
    /// Ставит иконку окна и панели задач из вложенного app.ico. Если файл недоступен,
    /// окно продолжает работать со стандартной иконкой, а причина не прерывает запуск.
    /// </summary>
    private void ApplyWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

            if (File.Exists(iconPath))
            {
                ResolveAppWindow().SetIcon(iconPath);
            }
        }
        catch (Exception)
        {
            // Иконка — оформление, а не функция: её сбой не должен мешать работе окна.
        }
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                ViewModel.MoveSelection(1);
                e.Handled = true;
                break;

            case VirtualKey.Up:
                ViewModel.MoveSelection(-1);
                e.Handled = true;
                break;

            case VirtualKey.Enter when ViewModel.Selected is not null:
                ViewModel.LaunchCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.Space when SearchBox.FocusState == FocusState.Unfocused && ViewModel.Selected is not null:
                ViewModel.LaunchCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.F5:
                ViewModel.RefreshCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                SearchBox.Text = string.Empty;
                e.Handled = true;
                break;
        }
    }
}