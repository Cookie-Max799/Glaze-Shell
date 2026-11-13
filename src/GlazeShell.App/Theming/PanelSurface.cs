namespace GlazeShell.App.Theming;

/// <summary>
/// Панель, для которой собирается Acrylic-фон. У каждой панели собственный ползунок
/// прозрачности, поэтому backdrop выбирает плотность по поверхности, а не по «док/не док».
/// </summary>
public enum PanelSurface
{
    /// <summary>Верхняя панель: полоса у верхней кромки экрана.</summary>
    TopBar,

    /// <summary>Dock: капсула у нижней кромки экрана.</summary>
    Dock,

    /// <summary>Меню «Пуск»: большая панель слева по центру экрана.</summary>
    StartMenu,
}
