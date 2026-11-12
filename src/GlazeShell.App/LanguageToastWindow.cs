using System.Diagnostics;
using GlazeShell.App.Theming;
using GlazeShell.Core.Display;
using MonitorInfo = GlazeShell.Core.Models.MonitorInfo;
using GlazeShell.Windows.WindowManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace GlazeShell.App;

internal sealed class LanguageToastWindow : Window
{
    private readonly Border _root;
    private readonly TextBlock _code;
    private readonly TextBlock _name;
    private readonly ThemeApplicator _themes;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _watch = new();
    private readonly nint _hwnd;
    private RectInt32 _bounds;
    private bool _closed;

    public LanguageToastWindow(ThemeApplicator themes)
    {
        _themes = themes;
        _code = new TextBlock { FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _name = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(_code);
        text.Children.Add(_name);
        _root = new Border
        {
            Padding = new Thickness(22, 12, 22, 12),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = text
        };
        Content = _root;
        Title = "Glaze Shell — Раскладка";
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
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
        };
    }

    public void ShowLanguage(string code, string name, MonitorInfo monitor)
    {
        if (_closed)
        {
            return;
        }

        _themes.ApplyFromSettings(_root);
        _root.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GlazeGlassBrush"];
        _root.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GlazeBorderBrush"];
        _code.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GlazeTextPrimaryBrush"];
        _name.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GlazeTextSecondaryBrush"];
        _code.Text = code;
        _name.Text = name;
        var scale = monitor.ScaleFactor;
        var width = (int)Math.Round(230 * scale);
        var height = (int)Math.Round(88 * scale);
        var area = monitor.WorkingArea ?? monitor.Bounds;
        _bounds = new RectInt32(area.X + (area.Width - width) / 2, area.Y + area.Height - height - (int)(110 * scale), width, height);
        _watch.Restart();
        RenderFrame(0);
        OverlayWindow.ShowWithoutActivation(_hwnd);
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        var elapsed = _watch.Elapsed.TotalMilliseconds;
        if (elapsed >= 1400)
        {
            _timer.Stop();
            AppWindow.Hide();
            return;
        }

        RenderFrame(elapsed);
    }

    private void RenderFrame(double elapsed)
    {
        var progress = _themes.ReduceMotion ? 1 : elapsed < 220
            ? 1 - Math.Pow(1 - elapsed / 220, 3)
            : elapsed > 1180 ? 1 - Math.Pow((elapsed - 1180) / 220, 2) : 1;
        _root.Opacity = progress;
        var rect = _bounds;
        rect.Y += (int)Math.Round((1 - progress) * 18 * OverlayWindow.GetScaleFactor(_hwnd));
        AppWindow.MoveAndResize(rect);
        OverlayWindow.ApplyRoundedRegions(_hwnd,
            new[] { new PixelRect(0, 0, rect.Width, rect.Height) },
            (int)(16 * OverlayWindow.GetScaleFactor(_hwnd)));
    }
}
