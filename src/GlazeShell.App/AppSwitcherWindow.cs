using GlazeShell.App.Theming;
using GlazeShell.Core.Models;
using GlazeShell.Core.Switching;
using GlazeShell.Windows.Switching;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;

namespace GlazeShell.App;

internal sealed class AppSwitcherWindow : Window
{
    private const double Gap = 12;
    private const double TitleHeight = 30;
    private const double DesktopStripHeight = 88;
    private const int MaxColumns = 4;

    private readonly Canvas _cards = new();
    private readonly StackPanel _desktops = new() { Orientation = Orientation.Horizontal, Spacing = Gap };
    private readonly TextBlock _status = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12 };
    private readonly List<DesktopThumbnail> _thumbnails = [];
    private readonly List<(Border Border, Rect Bounds)> _tiles = [];
    private readonly DispatcherQueueTimer _pointerTimer;
    private readonly nint _hwnd;
    private IReadOnlyList<SwitchableWindow> _windows = [];
    private double _width;
    private double _height;
    private double _scale = 1;
    private int _perPage = 1;
    private int _page = -1;
    private bool _leftDown;
    private int _pressed = -1;

    public AppSwitcherWindow(ThemeApplicator themes)
    {
        var root = new Grid { Background = Brush("GlazeTopBarGlassBrush") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DesktopStripHeight) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        root.Children.Add(new ScrollViewer
        {
            Content = _desktops,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(Gap, Gap, Gap, 0),
        });
        Grid.SetRow(_cards, 1);
        root.Children.Add(_cards);
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        themes.ApplyFromSettings(root, root);
        SystemBackdrop = themes.CreatePanelBackdrop(PanelSurface.TopBar);
        Content = root;
        Title = "Glaze Shell — Обзор окон";
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.SetNonActivating(_hwnd, true);
        OverlayWindow.UseRoundedCorners(_hwnd);
        _pointerTimer = DispatcherQueue.CreateTimer();
        _pointerTimer.Interval = TimeSpan.FromMilliseconds(16);
        _pointerTimer.Tick += OnPointerTick;
        Closed += (_, _) =>
        {
            _pointerTimer.Stop();
            _pointerTimer.Tick -= OnPointerTick;
            ClearThumbnails();
            (SystemBackdrop as IDisposable)?.Dispose();
        };
    }

    public event Action<int>? ItemActivated;

    public int Columns { get; private set; } = 1;

    public void ShowOverview(IReadOnlyList<SwitchableWindow> windows, int selectedIndex, MonitorInfo monitor)
    {
        var bounds = monitor.WorkingArea ?? monitor.Bounds;
        _scale = monitor.ScaleFactor;
        var margin = Math.Min(Pixels(20), Math.Min(bounds.Width, bounds.Height) / 20);
        var width = Math.Max(1, bounds.Width - 2 * margin);
        var dockSpace = Math.Min(Pixels(80), bounds.Height / 4);
        var height = Math.Max(1, bounds.Height - margin - Math.Max(margin, dockSpace));
        AppWindow.MoveAndResize(new RectInt32(bounds.X + margin, bounds.Y + margin, width, height));
        _width = width / _scale;
        _height = height / _scale;
        PopulateDesktops();
        Refresh(windows, selectedIndex);
        OverlayWindow.ShowWithoutActivation(_hwnd);
        OverlayWindow.RaiseTopMost(_hwnd);
        _leftDown = OverlayWindow.IsLeftButtonDown();
        _pointerTimer.Start();
    }

    public void Refresh(IReadOnlyList<SwitchableWindow> windows, int selectedIndex)
    {
        _windows = windows;
        _page = -1;
        Columns = Math.Min(Math.Max(1, windows.Count), Math.Clamp((int)(_width / 245), 1, MaxColumns));
        var rows = Math.Clamp((int)((_height - DesktopStripHeight - 26) / 150), 1, 3);
        _perPage = Columns * rows;
        SetSelection(selectedIndex);
    }

    public void SetSelection(int selectedIndex)
    {
        var page = Math.Max(0, selectedIndex) / _perPage;
        if (_page != page)
        {
            _page = page;
            BuildPage();
        }

        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Border.BorderBrush = Brush(i + page * _perPage == selectedIndex
                ? "GlazeAccentBrush"
                : "GlazeBorderBrush");
        }
    }

    public void HideOverview()
    {
        _pointerTimer.Stop();
        AppWindow.Hide();
        ClearThumbnails();
        _cards.Children.Clear();
        _tiles.Clear();
        _windows = [];
    }

    private void PopulateDesktops()
    {
        _desktops.Children.Clear();
        foreach (var desktop in VirtualDesktopStrip.Read())
        {
            _desktops.Children.Add(new Border
            {
                Width = 156,
                Height = 54,
                Padding = new Thickness(10, 0, 10, 0),
                Background = Brush(desktop.IsCurrent ? "GlazeSelectionBrush" : "GlazeGlassBrush"),
                BorderBrush = Brush(desktop.IsCurrent ? "GlazeAccentBrush" : "GlazeBorderBrush"),
                BorderThickness = new Thickness(desktop.IsCurrent ? 2 : 1),
                CornerRadius = new CornerRadius(9),
                Child = new TextBlock
                {
                    Text = desktop.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            });
        }
    }

    private void BuildPage()
    {
        ClearThumbnails();
        _cards.Children.Clear();
        _tiles.Clear();
        _pressed = -1;
        var first = _page * _perPage;
        var count = Math.Min(_perPage, _windows.Count - first);
        _status.Text = count == 0 ? "Нет открытых окон" :
            _windows.Count > _perPage ? $"Страница {_page + 1} из {(_windows.Count + _perPage - 1) / _perPage}" : "";
        var rows = Math.Max(1, (count + Columns - 1) / Columns);
        var width = Math.Min(520, (_width - (Columns + 1) * Gap) / Columns);
        var availableHeight = _height - DesktopStripHeight - 26;
        var height = Math.Max(TitleHeight + 16, Math.Min(width * 0.70, (availableHeight - (rows + 1) * Gap) / rows));
        var top = Math.Max(Gap, (availableHeight - rows * height - (rows - 1) * Gap) / 2);

        for (var i = 0; i < count; i++)
        {
            var row = i / Columns;
            var columnsInRow = Math.Min(Columns, count - row * Columns);
            var rowWidth = columnsInRow * width + (columnsInRow - 1) * Gap;
            var x = (_width - rowWidth) / 2 + i % Columns * (width + Gap);
            var y = top + row * (height + Gap);
            AddCard(first + i, x, y, width, height);
        }
    }

    private void AddCard(int index, double x, double y, double width, double height)
    {
        var window = _windows[index];
        var label = new TextBlock
        {
            Text = window.IsMinimized ? $"{window.Title} · свёрнуто" : window.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = TitleHeight - 4,
            Foreground = Brush("GlazeGlassTextPrimaryBrush"),
        };
        var content = new Grid();
        content.Children.Add(label);
        var card = new Border
        {
            Width = width,
            Height = height,
            Background = Brush("GlazeGlassBrush"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Child = content,
        };
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        _tiles.Add((card, new Rect(x, DesktopStripHeight + y, width, height)));
        _cards.Children.Add(card);

        var thumbnail = DesktopThumbnail.TryCreate(
            _hwnd, (nint)window.Handle,
            Pixels(x + 6), Pixels(DesktopStripHeight + y + 6),
            Pixels(width - 12), Pixels(height - TitleHeight - 8));
        if (thumbnail is not null)
        {
            _thumbnails.Add(thumbnail);
        }
        else
        {
            content.Children.Insert(0, new FontIcon
            {
                Glyph = "\uE737",
                FontSize = 30,
                Foreground = Brush("GlazeGlassTextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, TitleHeight),
            });
        }
    }

    private void OnPointerTick(DispatcherQueueTimer sender, object args)
    {
        var down = OverlayWindow.IsLeftButtonDown();
        if (down == _leftDown)
        {
            return;
        }

        _leftDown = down;
        var hit = -1;
        if (OverlayWindow.TryGetCursorPosition(_hwnd, out var x, out var y))
        {
            var point = new Point(x / _scale, y / _scale);
            hit = _tiles.FindIndex(tile => tile.Bounds.Contains(point));
        }

        if (down)
        {
            _pressed = hit;
        }
        else
        {
            var clicked = hit >= 0 && hit == _pressed;
            _pressed = -1;
            if (clicked)
            {
                ItemActivated?.Invoke(_page * _perPage + hit);
            }
        }
    }

    private int Pixels(double value) => (int)Math.Round(value * _scale);

    private void ClearThumbnails()
    {
        foreach (var thumbnail in _thumbnails)
        {
            thumbnail.Dispose();
        }

        _thumbnails.Clear();
    }

    private static Brush Brush(string key) => (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
}
