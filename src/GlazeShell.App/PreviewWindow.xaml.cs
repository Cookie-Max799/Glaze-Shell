using System.Diagnostics;
using GlazeShell.App.Presentation;
using GlazeShell.App.Theming;
using GlazeShell.Core.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace GlazeShell.App;

/// <summary>
/// Карточка Quick Preview (B2): плавающий предпросмотр выбранного файла по клавише
/// пробел — картинка, текст, сводка папки или метаданные с кнопкой открытия.
/// </summary>
/// <remarks>
/// Обычное окно приложения: ему нужны фокус для Esc/пробела и запись в панели задач.
/// Содержимое файла никогда не выполняется — текст только читается, картинку декодирует
/// WinUI.
/// </remarks>
public sealed partial class PreviewWindow : Window
{
    private const int DefaultWidth = 820;
    private const int DefaultHeight = 620;

    public PreviewWindow(IFilePreviewReader reader, string path, ThemeApplicator themes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(path);

        ViewModel = new PreviewViewModel(reader, path);

        InitializeComponent();

        Title = "Предпросмотр";

        (themes ?? throw new ArgumentNullException(nameof(themes))).ApplyFromSettings(Root);

        AppWindow.Resize(new SizeInt32(DefaultWidth, DefaultHeight));

        Root.KeyDown += OnRootKeyDown;
        Closed += OnClosed;

        _ = ViewModel.LoadAsync();
    }

    public PreviewViewModel ViewModel { get; }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Escape or VirtualKey.Space)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShellTargets.Open(ViewModel.TargetPath);

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        var path = ViewModel.TargetPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(path);
            Clipboard.SetContent(package);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.Preview: copying path failed. {exception}");
        }
    }

    private void OnImageFailed(object sender, ExceptionRoutedEventArgs e) => ViewModel.ReportImageFailure();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Root.KeyDown -= OnRootKeyDown;
        Closed -= OnClosed;
    }
}
