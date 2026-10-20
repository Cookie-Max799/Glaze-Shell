using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace GlazeShell.App;

/// <summary>
/// Окно Explorer (E1/E2): сайдбар с избранным и дисками, сетка иконок или список с колонками,
/// навигация по истории.
/// </summary>
/// <remarks>
/// Это обычное окно приложения, а не оверлей: ему нужны фокус, запись в панели задач и
/// системное меню, поэтому оно не собирается из регионов, как dock и верхняя панель.
/// </remarks>
public sealed partial class ExplorerWindow : Window
{
    private const int DefaultWidth = 1120;
    private const int DefaultHeight = 720;

    private readonly ThemeApplicator _themes;
    private readonly IDisposable _settingsChanged;

    public ExplorerWindow(
        IFileSystemBrowser browser,
        ISettingsManager settings,
        IEventManager events,
        ThemeApplicator themes,
        string? startPath)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(events);

        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        ViewModel = new ExplorerViewModel(browser, settings, DispatcherQueue);

        InitializeComponent();

        Title = "Проводник Glaze Shell";

        _themes.ApplyFromSettings(Root);
        _settingsChanged = events.Subscribe<SettingsChanged>(_ => _themes.ApplyFromSettings(Root));

        AppWindow.Resize(new SizeInt32(DefaultWidth, DefaultHeight));

        Activated += OnActivated;
        Closed += OnClosed;

        _ = StartAsync(startPath);
    }

    public ExplorerViewModel ViewModel { get; }

    private async Task StartAsync(string? path)
    {
        try
        {
            await ViewModel.InitializeAsync(path).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Explorer: initialization failed. {exception}");
            ViewModel.ReportInitializationFailure();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Подключённый диск должен появиться в сайдбаре без перезапуска окна.
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            ViewModel.RefreshSidebar();
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;
        _settingsChanged.Dispose();
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _ = ViewModel.GoBackAsync();

    private void OnForwardClick(object sender, RoutedEventArgs e) => _ = ViewModel.GoForwardAsync();

    private void OnUpClick(object sender, RoutedEventArgs e) => _ = ViewModel.GoUpAsync();

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = ViewModel.RefreshAsync();

    private void OnSortDirectionClick(object sender, RoutedEventArgs e) => ViewModel.ToggleSortDirection();

    private void OnAddFavoriteClick(object sender, RoutedEventArgs e) =>
        ViewModel.AddFavorite(ViewModel.CurrentPath);

    private void OnRemoveFavoriteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ExplorerSidebarItemViewModel item)
        {
            ViewModel.RemoveFavorite(item.Path);
        }
    }

    private void OnSidebarItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ExplorerSidebarItemViewModel item)
        {
            _ = ViewModel.NavigateAsync(item.Path);
        }
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ExplorerItemViewModel item)
        {
            _ = ViewModel.OpenItemAsync(item);
        }
    }

    private void OnItemsKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter ||
            sender is not ListViewBase list ||
            list.SelectedItem is not ExplorerItemViewModel item)
        {
            return;
        }

        e.Handled = true;

        _ = ViewModel.OpenItemAsync(item);
    }
}
