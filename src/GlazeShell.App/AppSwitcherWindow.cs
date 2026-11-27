using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Models;
using GlazeShell.Core.Switching;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.Text;

namespace GlazeShell.App;

/// <summary>
/// Панель переключателя приложений Alt+Tab: горизонтальный ряд одинаковых значков,
/// скользящая подсветка выбора и общая подпись выделенного приложения.
/// </summary>
/// <remarks>
/// <para>
/// Окно не активируется: выбор ведёт клавиатурный хук, а мышь и клавиши панель читает
/// опросом, как верхняя панель и её плашки. Поэтому фокус остаётся у приложения, из
/// которого пользователь переключается, и ничего не мигает при показе.
/// </para>
/// <para>
/// Показ, скрытие, скольжение подсветки и удаление значка анимируются вручную по кадрам
/// таймера: так анимацию можно прервать в любой момент и начать из текущего положения,
/// а Reduce Motion сводится к короткому затуханию без масштаба.
/// </para>
/// <para>
/// Значки грузятся из путей процессов в фоне и кэшируются по пути. Пока значок не готов,
/// на его месте плашка с первой буквой названия — панель не ждёт извлечения иконок.
/// </para>
/// </remarks>
internal sealed class AppSwitcherWindow : Window
{
    private const double PaddingDips = 14;
    private const double GapDips = 8;
    private const double DefaultCellDips = 64;
    private const double MinCellDips = 44;
    private const double IconRatio = 0.68;
    private const double LabelHeightDips = 24;
    private const double LabelGapDips = 4;
    private const double ListRowDips = 20;
    private const double ListGapDips = 6;
    private const double HighlightInsetDips = 3;
    private const double PanelScaleFrom = 0.96;
    private const double ShowMilliseconds = 135;
    private const double HideMilliseconds = 90;
    private const double ReduceMotionMilliseconds = 100;
    private const double SelectionMilliseconds = 130;
    private const double RemovalMilliseconds = 150;

    private const int MarginDips = 20;
    private const int OffscreenX = -30000;
    private const int OffscreenY = -30000;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly ThemeApplicator _themes;
    private readonly Border _root;
    private readonly StackPanel _panel;
    private readonly Canvas _canvas;
    private readonly Border _highlight;
    private readonly TextBlock _label;
    private readonly StackPanel _windowList;
    private readonly ScaleTransform _panelScale;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly PanelBackdrop? _backdrop;
    private readonly nint _hwnd;

    private readonly List<ItemView> _views = [];
    private readonly List<Removal> _removals = [];
    private readonly Dictionary<string, ImageSource> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _iconRequests = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SwitchableApplication> _applications = [];
    private MonitorInfo? _monitor;
    private RectInt32 _placement;

    private double _scale = 1.0;
    private double _cell = DefaultCellDips;
    private double _contentWidth;
    private double _contentHeight;
    private double _listHeight;
    private int _selectedIndex;

    private bool _closed;
    private bool _prepared;
    private bool _revealed;
    private bool _panelAnimating;
    private bool _panelHiding;
    private double _panelStart;
    private double _panelDuration;
    private double _panelFromOpacity;
    private double _panelToOpacity;
    private double _panelFromScale;
    private double _panelToScale;

    private bool _itemAnimating;
    private double _itemStart;

    private bool _highlightAnimating;
    private double _highlightStart;
    private double _highlightFromX;
    private double _highlightFromY;
    private double _highlightX = double.NaN;
    private double _highlightY = double.NaN;
    private double _highlightTargetX;
    private double _highlightTargetY;

    private int _hoverIndex = -1;
    private bool _leftWasDown;

    public AppSwitcherWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        _panelScale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };

        _highlight = new Border
        {
            CornerRadius = new CornerRadius(10),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        _canvas = new Canvas();
        _canvas.Children.Add(_highlight);

        _label = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Height = LabelHeightDips,
        };

        _windowList = new StackPanel
        {
            Spacing = 2,
            Visibility = Visibility.Collapsed,
        };

        _panel = new StackPanel
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _panel.Children.Add(_canvas);
        _panel.Children.Add(new Grid { Height = LabelGapDips });
        _panel.Children.Add(_label);
        _panel.Children.Add(_windowList);

        _root = new Border
        {
            Padding = new Thickness(PaddingDips),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            RenderTransform = _panelScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Child = _panel,
        };

        Content = _root;
        Title = "Glaze Shell — Переключение приложений";
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindow.MakeTopMostToolWindow(_hwnd);
        OverlayWindow.SetNonActivating(_hwnd, true);
        OverlayWindow.UseRoundedCorners(_hwnd);

        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = _backdrop;

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = FrameInterval;
        _timer.Tick += OnTick;

        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _backdrop?.Dispose();
        };
    }

    /// <summary>
    /// Значок выбран пользователем мышью (наведение или клик). Индекс относится к снимку
    /// текущей сессии.
    /// </summary>
    public event Action<int>? SelectionRequested;

    /// <summary>По значку кликнули: приложение нужно активировать сразу.</summary>
    public event Action<int>? ItemActivated;

    /// <summary>Панель подготовлена вне экрана и ещё не показана пользователю.</summary>
    public bool IsRevealed => _revealed;

    /// <summary>
    /// Строит ряд значков и прогревает окно вне экрана. Вызывается сразу после нажатия
    /// Alt+Tab, поэтому к моменту показа Acrylic уже подключён.
    /// </summary>
    public void Prepare(
        IReadOnlyList<SwitchableApplication> applications,
        int selectedIndex,
        MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(monitor);

        if (_closed)
        {
            return;
        }

        _applications = applications;
        _monitor = monitor;
        _scale = monitor.ScaleFactor <= 0 ? 1.0 : monitor.ScaleFactor;
        _selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, applications.Count - 1));
        _listHeight = 0;
        _windowList.Visibility = Visibility.Collapsed;
        _windowList.Children.Clear();

        ApplyBrushes();
        Rebuild();

        _panelAnimating = false;
        _panelHiding = false;
        _root.Opacity = 0;
        _panelScale.ScaleX = PanelScaleFrom;
        _panelScale.ScaleY = PanelScaleFrom;
        _prepared = true;
        _revealed = false;

        var width = Math.Max(1, _placement.Width);
        var height = Math.Max(1, _placement.Height);
        AppWindow.MoveAndResize(new RectInt32(OffscreenX, OffscreenY, width, height));
        OverlayWindow.ShowWithoutActivation(_hwnd);
        _timer.Start();
    }

    /// <summary>Переносит прогретую панель на место монитора и проявляет её.</summary>
    public void Reveal()
    {
        if (_closed || !_prepared || _revealed)
        {
            return;
        }

        _revealed = true;
        AppWindow.MoveAndResize(_placement);
        OverlayWindow.RaiseTopMost(_hwnd);

        if (_themes.ReduceMotion)
        {
            StartPanelAnimation(1, 1, ReduceMotionMilliseconds, hiding: false);
        }
        else
        {
            StartPanelAnimation(1, 1, ShowMilliseconds, hiding: false);
        }

        _timer.Start();
    }

    /// <summary>Обновляет выделение; подсветка скользит к новому значку.</summary>
    public void SetSelection(int index)
    {
        if (_closed || _views.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Clamp(index, 0, _views.Count - 1);
        var selected = _views[_selectedIndex];

        _label.Text = selected.Application.Name;

        for (var i = 0; i < _views.Count; i++)
        {
            var scale = i == _selectedIndex ? 1.05 : 1.0;
            _views[i].Scale.ScaleX = scale;
            _views[i].Scale.ScaleY = scale;
        }

        StartHighlightAnimation();
    }

    /// <summary>
    /// Показывает или прячет список окон выделенного приложения. Подпись приложения
    /// остаётся общей, строки окон идут под ней.
    /// </summary>
    public void SetWindowList(IReadOnlyList<string>? titles, int selectedWindow)
    {
        if (_closed)
        {
            return;
        }

        _windowList.Children.Clear();

        if (titles is null || titles.Count == 0)
        {
            _windowList.Visibility = Visibility.Collapsed;
            _listHeight = 0;
        }
        else
        {
            for (var i = 0; i < titles.Count; i++)
            {
                var row = new TextBlock
                {
                    Text = titles[i],
                    FontSize = 12,
                    Height = ListRowDips,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = i == selectedWindow
                        ? ResourceBrush("GlazeGlassTextPrimaryBrush")
                        : ResourceBrush("GlazeGlassTextMutedBrush"),
                };

                _windowList.Children.Add(row);
            }

            _windowList.Visibility = Visibility.Visible;
            _windowList.Margin = new Thickness(0, ListGapDips, 0, 0);
            _listHeight = ListGapDips + (titles.Count * ListRowDips) + (Math.Max(0, titles.Count - 1) * 2);
        }

        UpdateSize();
    }

    /// <summary>Приглушает значок приложения, свёрнутого клавишей H.</summary>
    public void SetHidden(string applicationId)
    {
        foreach (var view in _views)
        {
            if (string.Equals(view.Application.Id, applicationId, StringComparison.Ordinal))
            {
                view.Cell.Opacity = 0.45;
            }
        }
    }

    /// <summary>
    /// Убирает значок с анимацией уменьшения; соседи съезжаются после неё. Запись уже
    /// удалена из сессии, поэтому вызывающий сразу обновляет выделение.
    /// </summary>
    public void Remove(string applicationId)
    {
        if (_closed)
        {
            return;
        }

        var index = _views.FindIndex(view => string.Equals(view.Application.Id, applicationId, StringComparison.Ordinal));

        if (index < 0)
        {
            return;
        }

        var view = _views[index];
        _views.RemoveAt(index);

        _removals.Add(new Removal(view, _clock.Elapsed.TotalMilliseconds));
        LayoutItems(animate: true);

        if (_views.Count == 0)
        {
            _highlight.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Скрывает панель сразу или с коротким затуханием.</summary>
    public void Hide(bool animate)
    {
        if (_closed || !_prepared)
        {
            return;
        }

        if (!animate || !_revealed)
        {
            CompleteHide();
            return;
        }

        StartPanelAnimation(0, _panelScale.ScaleX, HideMilliseconds, hiding: true);
    }

    /// <summary>Закрывает окно окончательно (выход из приложения).</summary>
    public void CloseWindow()
    {
        if (!_closed)
        {
            Close();
        }
    }

    private void Rebuild()
    {
        foreach (var view in _views)
        {
            _canvas.Children.Remove(view.Cell);
        }

        foreach (var removal in _removals)
        {
            _canvas.Children.Remove(removal.View.Cell);
        }

        _views.Clear();
        _removals.Clear();
        _label.Text = string.Empty;

        foreach (var application in _applications)
        {
            var view = CreateItem(application);
            _views.Add(view);
            _canvas.Children.Add(view.Cell);
        }

        LayoutItems(animate: false);
        SetSelection(_selectedIndex);
    }

    private ItemView CreateItem(SwitchableApplication application)
    {
        var letter = new TextBlock
        {
            Text = FirstLetter(application.Name),
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResourceBrush("GlazeGlassTextPrimaryBrush"),
        };

        var plate = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = ResourceBrush("GlazeGlassBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = letter,
        };

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        var cell = new Grid { RenderTransform = scale, RenderTransformOrigin = new Point(0.5, 0.5) };
        cell.Children.Add(plate);
        cell.Children.Add(image);

        var view = new ItemView(application, cell, image, plate, scale);
        ApplyCellSize(view);
        RequestIcon(view);
        return view;
    }

    private void LayoutItems(bool animate)
    {
        if (_monitor is null || _views.Count == 0)
        {
            return;
        }

        var gap = GapDips;
        var cell = DefaultCellDips;
        var perRow = _views.Count;

        var available = (_monitor.Bounds.Width / _scale) - (2 * MarginDips);

        // Сначала уменьшаем значки до минимального размера, затем переносим на второй ряд.
        while (true)
        {
            perRow = Math.Max(1, (int)Math.Floor((available + gap) / (cell + gap)));

            if (perRow >= _views.Count || cell <= MinCellDips)
            {
                break;
            }

            cell = Math.Max(MinCellDips, cell - 4);
        }

        perRow = Math.Max(1, perRow);

        var rows = (int)Math.Ceiling(_views.Count / (double)perRow);
        _cell = cell;

        var contentWidth = 0.0;

        for (var row = 0; row < rows; row++)
        {
            var count = Math.Min(perRow, _views.Count - (row * perRow));
            var rowWidth = (count * cell) + ((count - 1) * gap);
            contentWidth = Math.Max(contentWidth, rowWidth);
        }

        _contentWidth = contentWidth;
        _contentHeight = (rows * cell) + ((rows - 1) * gap);

        _canvas.Width = _contentWidth;
        _canvas.Height = _contentHeight;
        _label.Width = _contentWidth;
        _windowList.Width = _contentWidth;
        _panel.Width = _contentWidth;
        _root.Width = _contentWidth + (2 * PaddingDips);

        var highlightSize = Math.Max(1, cell - (2 * HighlightInsetDips));
        _highlight.Width = highlightSize;
        _highlight.Height = highlightSize;

        for (var index = 0; index < _views.Count; index++)
        {
            var view = _views[index];
            var row = index / perRow;
            var column = index % perRow;
            var count = Math.Min(perRow, _views.Count - (row * perRow));
            var rowWidth = (count * cell) + ((count - 1) * gap);
            var rowOffset = (_contentWidth - rowWidth) / 2;

            view.TargetX = rowOffset + (column * (cell + gap));
            view.TargetY = row * (cell + gap);
            view.StartX = view.X;
            view.StartY = view.Y;

            if (!animate || !_revealed)
            {
                view.X = view.TargetX;
                view.Y = view.TargetY;
            }

            ApplyCellSize(view);
            Canvas.SetLeft(view.Cell, view.X);
            Canvas.SetTop(view.Cell, view.Y);
        }

        if (animate && _revealed)
        {
            _itemAnimating = true;
            _itemStart = _clock.Elapsed.TotalMilliseconds;
        }
        else
        {
            _itemAnimating = false;
        }

        UpdateSize();
        StartHighlightAnimation();
    }

    private void ApplyCellSize(ItemView view)
    {
        var iconSize = Math.Max(20, Math.Round(_cell * IconRatio));
        var plateSize = Math.Max(20, Math.Round(_cell * 0.78));
        var radius = Math.Max(8, Math.Round(plateSize * 0.22));

        view.Cell.Width = _cell;
        view.Cell.Height = _cell;
        view.Image.Width = iconSize;
        view.Image.Height = iconSize;
        view.Plate.Width = plateSize;
        view.Plate.Height = plateSize;
        view.Plate.CornerRadius = new CornerRadius(radius);
    }

    private void UpdateSize()
    {
        if (_monitor is null)
        {
            return;
        }

        var height = (2 * PaddingDips) + _contentHeight + LabelGapDips + LabelHeightDips + _listHeight;
        _root.Height = height + 2;

        var area = _monitor.Bounds;
        var margin = (int)Math.Round(MarginDips * _scale);
        var widthPx = Math.Max(1, (int)Math.Round(_root.Width * _scale));
        var heightPx = Math.Max(1, (int)Math.Round((height + 2) * _scale));

        var x = area.X + ((area.Width - widthPx) / 2);
        var y = area.Y + (int)Math.Round((area.Height * 0.40) - (heightPx / 2.0));

        y = Math.Clamp(y, area.Y + margin, Math.Max(area.Y + margin, area.Y + area.Height - heightPx - margin));

        _placement = new RectInt32(x, y, widthPx, heightPx);

        if (_revealed && _prepared)
        {
            AppWindow.MoveAndResize(_placement);
        }
    }

    private void StartHighlightAnimation()
    {
        if (_views.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _views.Count)
        {
            _highlight.Visibility = Visibility.Collapsed;
            return;
        }

        var view = _views[_selectedIndex];
        var inset = Math.Max(0, (_cell - _highlight.Width) / 2);

        _highlight.Visibility = Visibility.Visible;
        _highlightTargetX = view.TargetX + inset;
        _highlightTargetY = view.TargetY + inset;

        if (double.IsNaN(_highlightX) || double.IsNaN(_highlightY))
        {
            _highlightX = _highlightTargetX;
            _highlightY = _highlightTargetY;
        }

        _highlightFromX = _highlightX;
        _highlightFromY = _highlightY;
        _highlightAnimating = true;
        _highlightStart = _clock.Elapsed.TotalMilliseconds;

        if (!_revealed)
        {
            _highlightAnimating = false;
            _highlightX = _highlightTargetX;
            _highlightY = _highlightTargetY;
            ApplyHighlightPosition(_highlightX, _highlightY);
        }
    }

    private void StartPanelAnimation(double opacity, double scale, double milliseconds, bool hiding)
    {
        _panelFromOpacity = _root.Opacity;
        _panelFromScale = _panelScale.ScaleX;
        _panelToOpacity = opacity;
        _panelToScale = scale;
        _panelStart = _clock.Elapsed.TotalMilliseconds;
        _panelDuration = Math.Max(1, milliseconds);
        _panelAnimating = true;
        _panelHiding = hiding;
    }

    private void CompleteHide()
    {
        _panelAnimating = false;
        _panelHiding = false;
        _prepared = false;
        _revealed = false;
        _root.Opacity = 0;
        _timer.Stop();
        AppWindow.Hide();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_closed)
        {
            _timer.Stop();
            return;
        }

        var now = _clock.Elapsed.TotalMilliseconds;

        AnimatePanel(now);
        AnimateItems(now);
        AnimateHighlight(now);
        AnimateRemovals(now);

        if (_revealed && _prepared)
        {
            PollMouse();
        }
    }

    private void AnimatePanel(double now)
    {
        if (!_panelAnimating)
        {
            return;
        }

        var progress = Math.Clamp((now - _panelStart) / _panelDuration, 0, 1);

        var eased = _panelHiding
            ? progress * progress
            : 1 - Math.Pow(1 - progress, 3);

        _root.Opacity = _panelFromOpacity + ((_panelToOpacity - _panelFromOpacity) * eased);

        var scale = _panelFromScale + ((_panelToScale - _panelFromScale) * eased);
        _panelScale.ScaleX = scale;
        _panelScale.ScaleY = scale;

        if (progress < 1)
        {
            return;
        }

        _panelAnimating = false;

        if (_panelHiding)
        {
            CompleteHide();
        }
    }

    private void AnimateItems(double now)
    {
        if (!_itemAnimating)
        {
            return;
        }

        var progress = Math.Clamp((now - _itemStart) / SelectionMilliseconds, 0, 1);
        var eased = progress < 0.5
            ? 2 * progress * progress
            : 1 - (Math.Pow((-2 * progress) + 2, 2) / 2);

        foreach (var view in _views)
        {
            view.X = view.StartX + ((view.TargetX - view.StartX) * eased);
            view.Y = view.StartY + ((view.TargetY - view.StartY) * eased);
            Canvas.SetLeft(view.Cell, view.X);
            Canvas.SetTop(view.Cell, view.Y);
        }

        if (progress >= 1)
        {
            _itemAnimating = false;
        }
    }

    private void AnimateHighlight(double now)
    {
        if (!_highlightAnimating)
        {
            return;
        }

        var progress = Math.Clamp((now - _highlightStart) / SelectionMilliseconds, 0, 1);
        var eased = progress < 0.5
            ? 2 * progress * progress
            : 1 - (Math.Pow((-2 * progress) + 2, 2) / 2);

        _highlightX = _highlightFromX + ((_highlightTargetX - _highlightFromX) * eased);
        _highlightY = _highlightFromY + ((_highlightTargetY - _highlightFromY) * eased);
        ApplyHighlightPosition(_highlightX, _highlightY);

        if (progress >= 1)
        {
            _highlightAnimating = false;
            _highlightX = _highlightTargetX;
            _highlightY = _highlightTargetY;
            ApplyHighlightPosition(_highlightX, _highlightY);
        }
    }

    private void ApplyHighlightPosition(double x, double y)
    {
        Canvas.SetLeft(_highlight, x);
        Canvas.SetTop(_highlight, y);
    }

    private void AnimateRemovals(double now)
    {
        for (var i = _removals.Count - 1; i >= 0; i--)
        {
            var removal = _removals[i];
            var progress = Math.Clamp((now - removal.Start) / RemovalMilliseconds, 0, 1);
            var scale = 1 - (0.5 * progress);

            removal.View.Cell.Opacity = 1 - progress;
            removal.View.Scale.ScaleX = scale;
            removal.View.Scale.ScaleY = scale;

            if (progress < 1)
            {
                continue;
            }

            _canvas.Children.Remove(removal.View.Cell);
            _removals.RemoveAt(i);
        }
    }

    private void PollMouse()
    {
        if (!OverlayWindow.TryGetCursorPosition(_hwnd, out var pixelX, out var pixelY))
        {
            return;
        }

        var scale = OverlayWindow.GetScaleFactor(_hwnd);

        if (scale <= 0)
        {
            scale = 1.0;
        }

        var x = (pixelX / scale) - PaddingDips;
        var y = (pixelY / scale) - PaddingDips;
        var hovered = HitTest(x, y);

        if (hovered != _hoverIndex)
        {
            _hoverIndex = hovered;

            if (hovered >= 0 && hovered != _selectedIndex)
            {
                SelectionRequested?.Invoke(hovered);
            }
        }

        var leftDown = OverlayWindow.IsLeftButtonDown();

        if (_leftWasDown && !leftDown && hovered >= 0)
        {
            ItemActivated?.Invoke(hovered);
        }

        _leftWasDown = leftDown;
    }

    private int HitTest(double x, double y)
    {
        for (var i = 0; i < _views.Count; i++)
        {
            var view = _views[i];

            if (x >= view.TargetX && x < view.TargetX + _cell &&
                y >= view.TargetY && y < view.TargetY + _cell)
            {
                return i;
            }
        }

        return -1;
    }

    private void RequestIcon(ItemView view)
    {
        var path = view.Application.ExecutablePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (_icons.TryGetValue(path, out var cached))
        {
            ApplyIcon(view, cached);
            return;
        }

        if (!_iconRequests.Add(path))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            var pixels = ShellIconProvider.TryGetIcon(path, 128);

            if (pixels is null)
            {
                return;
            }

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                var source = IconBitmap.Create(pixels);

                if (source is null || _closed)
                {
                    return;
                }

                _icons[path] = source;

                foreach (var item in _views)
                {
                    if (string.Equals(item.Application.ExecutablePath, path, StringComparison.OrdinalIgnoreCase) &&
                        item.Image.Source is null)
                    {
                        ApplyIcon(item, source);
                    }
                }
            });
        });
    }

    private static void ApplyIcon(ItemView view, ImageSource source)
    {
        view.Image.Source = source;
        view.Image.Visibility = Visibility.Visible;
        view.Plate.Visibility = Visibility.Collapsed;
    }

    private void ApplyBrushes()
    {
        _root.Background = ResourceBrush("GlazeTopBarGlassBrush");
        _root.BorderBrush = ResourceBrush("GlazeBorderBrush");
        _label.Foreground = ResourceBrush("GlazeGlassTextPrimaryBrush");
        _highlight.Background = ResourceBrush("GlazeSelectionBrush");
        _themes.ApplyFromSettings(_root, _root);
    }

    private static Brush ResourceBrush(string key)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static string FirstLetter(string name)
    {
        foreach (var character in name)
        {
            if (!char.IsWhiteSpace(character))
            {
                return char.ToUpperInvariant(character).ToString();
            }
        }

        return "?";
    }

    private sealed class ItemView
    {
        public ItemView(
            SwitchableApplication application,
            Grid cell,
            Image image,
            Border plate,
            ScaleTransform scale)
        {
            Application = application;
            Cell = cell;
            Image = image;
            Plate = plate;
            Scale = scale;
        }

        public SwitchableApplication Application { get; }

        public Grid Cell { get; }

        public Image Image { get; }

        public Border Plate { get; }

        public ScaleTransform Scale { get; }

        public double X { get; set; }

        public double Y { get; set; }

        public double StartX { get; set; }

        public double StartY { get; set; }

        public double TargetX { get; set; }

        public double TargetY { get; set; }
    }

    private readonly record struct Removal(ItemView View, double Start);
}
