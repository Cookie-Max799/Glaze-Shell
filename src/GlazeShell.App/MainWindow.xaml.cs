using System.ComponentModel;
using System.IO;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace GlazeShell.App;

public sealed partial class MainWindow : Window
{
    // Размеры главного окна заданы в DIP: половина рабочей области и границы ниже
    // переводятся в пиксели по масштабу монитора, иначе при 125 % окно открывалось бы
    // уже своего же минимума и список сжимался бы сильнее задуманного.
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 760;
    private const int MinWidth = 960;
    private const int MinHeight = 520;

    /// <summary>Ширина панели мониторов в DIP: совпадает с шириной колонки в разметке.</summary>
    private const int MonitorsWidth = 320;

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
        _themes.ApplyTitleBar(this);
        ViewModel.ThemeSelectionChanged += OnThemeSelectionChanged;

        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), handledEventsToo: true);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyWindowIcon();
        _appWindow = ResolveAppWindow();
        _appWindow.IsShownInSwitchers = true;
        _appWindow.Closing += OnAppWindowClosing;
        ApplyInitialSizeAndPosition(monitors.GetPrimaryMonitor()?.ScaleFactor ?? 1.0);
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

    public event EventHandler? RestoreGlazeRequested;

    public void SetGlazeRestoreAvailable(bool available) => RestoreGlazeButton.IsEnabled = available;

    private AppWindow? _appWindow;
    private bool _allowClose;
    public bool TrayAvailable { get; set; }

    /// <summary>
    /// Скрывает главное окно: панели и оформление продолжают работать, выход — через значок
    /// в трее. Вызывается при закрытии окна крестиком, если трей создан, или по команде
    /// из «Центра управления».
    /// </summary>
    public void HideToTray()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            if (_appWindow is not null)
            {
                _appWindow.IsShownInSwitchers = false;
                _appWindow.Hide();
            }
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(HideToTray);
        }
    }

    /// <summary>
    /// Показывает главное окно из трея: двойной клик по значку или повторный запуск
    /// приложения. Вызов может прийти с фонового потока значка, поэтому переключаемся
    /// на поток интерфейса.
    /// </summary>
    public void ShowFromTray()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(ShowFromTray);
            return;
        }

        if (_appWindow is not null)
        {
            _appWindow.IsShownInSwitchers = true;
            if (_appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            {
                presenter.Restore();
            }
            _appWindow.Show();

            // Show только делает окно видимым: свёрнутое остаётся свёрнутым, а фокус
            // может остаться у чужого окна. Activate восстанавливает окно и переводит
            // его на передний план — без него пользователь слышал звук отказа активации,
            // но так и не видел окно.
            Activate();

            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                GlazeShell.Windows.Lifecycle.SingleInstanceHelper.BringToForeground(hwnd);
            }
            catch
            {
                // Best-effort: если активация не удалась, окно всё равно показано.
            }
        }
    }

    /// <summary>
    /// Разрешает настоящее закрытие окна и приложения: вызывается по команде «Выход»
    /// из значка в трее. После вызова крестик завершит процесс как раньше.
    /// </summary>
    public void CloseForExit()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            _allowClose = true;
            Close();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                _allowClose = true;
                Close();
            });
        }
    }

    private void OnThemeSelectionChanged(object? sender, ThemeSelectionChangedEventArgs e)
    {
        _themes.Apply(Root, e.Mode, e.AccentId);
        _themes.ApplyTitleBar(this);
    }

    private void OnRecoveryClick(object sender, RoutedEventArgs e) => RecoveryRequested?.Invoke(this, EventArgs.Empty);

    private void OnRestoreGlazeClick(object sender, RoutedEventArgs e) => RestoreGlazeRequested?.Invoke(this, EventArgs.Empty);

    private void OnExplorerClick(object sender, RoutedEventArgs e) => ExplorerRequested?.Invoke(this, null);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        ViewModel.RefreshCommand.Execute(null);

    /// <summary>
    /// Сворачивает и разворачивает панель мониторов: колонка получает нулевую ширину,
    /// чтобы список приложений занимал всё окно, а не оставлял пустую полосу.
    /// </summary>
    private void OnMonitorsToggleClick(object sender, RoutedEventArgs e)
    {
        var visible = MonitorsToggle.IsChecked == true;

        MonitorsColumn.Width = new GridLength(visible ? MonitorsWidth : 0);
        MonitorsPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

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

    private void ApplyInitialSizeAndPosition(double scale)
    {
        var appWindow = ResolveAppWindow();
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        if (scale <= 0)
        {
            scale = 1.0;
        }

        var width = (int)Math.Round(Math.Clamp(workArea.Width / 2.0 / scale, MinWidth, DefaultWidth) * scale);
        var height = (int)Math.Round(Math.Clamp(workArea.Height / 2.0 / scale, MinHeight, DefaultHeight) * scale);
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

    /// <summary>
    /// Перехват закрытия главного окна. Если приложение владеет значком в трее, крестик
    /// скрывает окно вместо выхода — панели и оформление продолжают работать. Настоящий
    /// выход — через пункт «Выход» в трее. Если трей не создан, крестик завершает процесс.
    /// </summary>
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !TrayAvailable)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Навигация Up/Down работает всегда (даже когда SearchBox в фокусе, иначе стрелки
        // не могли бы перемещаться по списку). Остальные клавиши игнорируются, если событие
        // уже обработано (например, Enter в диалоге) или если нажат модификатор (Win+Space).
        if (e.Handled && e.Key != VirtualKey.Up && e.Key != VirtualKey.Down)
        {
            return;
        }

        // Проверяем модификаторы: Win+Space (переключение языка), Alt+Space (системное меню),
        // Ctrl+Enter и т.п. НЕ должны запускать приложение из списка.
        var isModifierPressed =
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);

        if (isModifierPressed && e.Key != VirtualKey.Up && e.Key != VirtualKey.Down)
        {
            return;
        }

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

            // Enter запускает выбранное приложение ТОЛЬКО когда фокус внутри SearchBox или ApplicationList,
            // чтобы нажатие Enter на кнопках («Настройки», «Восстановить UI») или в диалогах НЕ запускало
            // случайное приложение из списка (предыдущая логика не проверяла FocusedElement).
            case VirtualKey.Enter when ViewModel.Selected is not null:
            {
                var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;

                if (focused is TextBox || IsDescendantOf(focused, ApplicationList))
                {
                    ViewModel.LaunchCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }

            // Space запускает выбранное приложение ТОЛЬКО когда фокус внутри ApplicationList
            // (НЕ в SearchBox: там Space вводит символ).
            case VirtualKey.Space when ViewModel.Selected is not null:
            {
                var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;

                if (IsDescendantOf(focused, ApplicationList))
                {
                    ViewModel.LaunchCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }

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

    /// <summary>Проверяет, является ли элемент потомком контейнера.</summary>
    private static bool IsDescendantOf(DependencyObject? element, DependencyObject? container)
    {
        while (element is not null)
        {
            if (element == container)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }
}
