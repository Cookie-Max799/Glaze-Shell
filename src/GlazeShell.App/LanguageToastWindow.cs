using System.Diagnostics;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using MonitorInfo = GlazeShell.Core.Models.MonitorInfo;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace GlazeShell.App;

/// <summary>
/// Плашка смены раскладки: маленькое окно в правом верхнем углу, которое раскрывается
/// из точки-«капли» до полного размера.
/// </summary>
/// <remarks>
/// Плашка живёт под верхней панелью (на 36 DIP ниже кромки экрана), поэтому не перекрывает
/// её элементы, и использует тот же Acrylic-фон с той же прозрачностью, что и панель.
/// Окно не активируется: смена языка не должна забирать фокус у активного приложения.
/// Анимация — рост прямоугольника окна от квадрата 22×22 DIP с привязкой к правому
/// верхнему углу; содержимое прижато вправо, поэтому плашка раскрывается справа налево,
/// а не «заливается» слева направо.
/// </remarks>
internal sealed class LanguageToastWindow : Window
{
    private const int WidthDips = 214;
    private const int HeightDips = 66;

    /// <summary>Отступ от правого края рабочей области.</summary>
    private const int MarginDips = 12;

    /// <summary>Отступ сверху: верхняя панель занимает 30 DIP, плашка идёт сразу под ней.</summary>
    private const int TopDips = 36;

    /// <summary>Размер «капли», из которой раскрывается плашка.</summary>
    private const int DropletDips = 22;

    private const double GrowMilliseconds = 280;
    private const double HoldMilliseconds = 1150;
    private const double FadeMilliseconds = 180;

    private static readonly TimeSpan GrowTime = TimeSpan.FromMilliseconds(GrowMilliseconds);
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(HoldMilliseconds);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(FadeMilliseconds);
    private static readonly TimeSpan TotalTime = GrowTime + HoldTime + FadeTime;

    private readonly Border _root;
    private readonly TextBlock _code;
    private readonly TextBlock _name;
    private readonly ThemeApplicator _themes;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _watch = new();
    private readonly nint _hwnd;

    private RectInt32 _droplet;
    private RectInt32 _target;
    private PanelBackdrop? _backdrop;
    private bool _closed;

    public LanguageToastWindow(ThemeApplicator themes)
    {
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));

        _code = new TextBlock
        {
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _name = new TextBlock
        {
            FontSize = 11.5,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.Children.Add(_code);
        text.Children.Add(_name);

        _root = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = text,
        };

        Content = _root;
        Title = "Glaze Shell — Раскладка";

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

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;

        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _backdrop?.Dispose();
            _backdrop = null;
        };
    }

    public void ShowLanguage(string code, string name, MonitorInfo monitor)
    {
        if (_closed)
        {
            return;
        }

        _themes.ApplyFromSettings(_root);
        _root.Background = (Brush)Application.Current.Resources["GlazeTopBarGlassBrush"];
        _root.BorderBrush = (Brush)Application.Current.Resources["GlazeBorderBrush"];
        _code.Foreground = (Brush)Application.Current.Resources["GlazeTextPrimaryBrush"];
        _name.Foreground = (Brush)Application.Current.Resources["GlazeTextSecondaryBrush"];
        _code.Text = code;
        _name.Text = name;

        ApplyBackdrop();

        var scale = monitor.ScaleFactor;
        var area = monitor.WorkingArea ?? monitor.Bounds;
        var right = area.X + area.Width - (int)Math.Round(MarginDips * scale);
        var top = area.Y + (int)Math.Round(TopDips * scale);
        var width = (int)Math.Round(WidthDips * scale);
        var height = (int)Math.Round(HeightDips * scale);
        var droplet = (int)Math.Round(DropletDips * scale);

        _target = new RectInt32(right - width, top, width, height);
        _droplet = new RectInt32(right - droplet, top, droplet, droplet);
        AppWindow.MoveAndResize(_target);

        _watch.Restart();
        _timer.Stop();
        RenderFrame(_themes.ReduceMotion ? TotalTime : TimeSpan.Zero);
        OverlayWindow.ShowWithoutActivation(_hwnd);
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        var elapsed = _watch.Elapsed;

        if (elapsed >= TotalTime)
        {
            _timer.Stop();
            AppWindow.Hide();
            return;
        }

        RenderFrame(elapsed);
    }

    private void RenderFrame(TimeSpan elapsed)
    {
        var progress = _themes.ReduceMotion ? 1 : GrowProgress(elapsed);
        var rect = Interpolate(_droplet, _target, progress);
        var scale = OverlayWindow.GetScaleFactor(_hwnd);

        // Содержимое проявляется на первой трети роста: в размере «капли» текст всё равно
        // не читается, а резкое появление букв в маленьком окне выглядело бы рывком.
        _root.Opacity = _themes.ReduceMotion
            ? 1
            : Math.Clamp(progress / 0.35, 0, 1);

        OverlayWindow.ApplyRoundedRegions(
            _hwnd,
            [new PixelRect(_target.Width - rect.Width, 0, rect.Width, rect.Height)],
            (int)Math.Round(16 * scale));
    }

    /// <summary>
    /// Прогресс появления: рост из «капли» с лёгким перелётом (ease-out back), удержание
    /// и плавное угасание в конце.
    /// </summary>
    private static double GrowProgress(TimeSpan elapsed)
    {
        if (elapsed < GrowTime)
        {
            var t = elapsed.TotalMilliseconds / GrowMilliseconds;
            const double Back = 1.35;
            const double BackCube = Back + 1;
            var eased = 1 + (BackCube * Math.Pow(t - 1, 3)) + (Back * Math.Pow(t - 1, 2));
            return Math.Clamp(eased, 0, 1.06);
        }

        var afterGrow = elapsed - GrowTime;

        if (afterGrow < HoldTime)
        {
            return 1;
        }

        var fade = Math.Clamp((afterGrow - HoldTime).TotalMilliseconds / FadeMilliseconds, 0, 1);
        return 1 - fade;
    }

    private static RectInt32 Interpolate(RectInt32 from, RectInt32 to, double progress)
    {
        // Обе границы привязаны к правому верхнему углу: окно растёт влево и вниз,
        // поэтому «капля» остаётся в углу экрана.
        var width = Lerp(from.Width, to.Width, progress);
        var height = Lerp(from.Height, to.Height, progress);
        return new RectInt32(from.X + from.Width - width, from.Y, width, height);
    }

    private static int Lerp(int from, int to, double progress) =>
        Math.Max(1, (int)Math.Round(from + ((to - from) * progress)));

    /// <summary>Тот же Acrylic-фон, что у верхней панели: плашка следует её прозрачности.</summary>
    private void ApplyBackdrop()
    {
        if (_closed)
        {
            return;
        }

        var previous = _backdrop;
        _backdrop = _themes.CreatePanelBackdrop(PanelSurface.TopBar) as PanelBackdrop;
        SystemBackdrop = _backdrop;
        previous?.Dispose();
    }
}
