using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.MVVM.View;

/// <summary>
/// Solo comportamiento de vista: el índice lleva a cada sección y, al hacer
/// scroll, se resalta la sección que se está viendo.
/// </summary>
public partial class HelpView : UserControl
{
    // Margen que se deja por encima de la sección al saltar a ella.
    private const double TopGap = 32;

    public HelpView()
    {
        InitializeComponent();
        ContentScroll.ScrollChanged += (_, _) => UpdateActiveSection();
    }

    private void IndexItem_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is HelpSection section) ScrollToSection(section.Id);
    }

    /// <summary>Lleva la sección arriba del todo (si queda contenido para ello).</summary>
    public void ScrollToSection(string id)
    {
        if (this.FindControl<Control>(id) is not { } section) return;
        // Un rectángulo del alto visible que empieza en la sección: BringIntoView
        // la deja arriba en vez de pegada al borde de abajo.
        var height = Math.Max(1, ContentScroll.Viewport.Height);
        section.BringIntoView(new Rect(0, -TopGap, section.Bounds.Width, height));
    }

    private void UpdateActiveSection()
    {
        if (DataContext is not HelpViewModel vm) return;
        var offset = ContentScroll.Offset.Y;
        var atEnd  = offset >= ContentScroll.Extent.Height - ContentScroll.Viewport.Height - 1;
        string? current = null;
        foreach (var s in vm.Sections)
        {
            if (this.FindControl<Control>(s.Id) is not { } c) continue;
            var top = c.TranslatePoint(default, ContentPanel)?.Y ?? double.MaxValue;
            if (top <= offset + TopGap + 40 || atEnd) current = s.Id;
        }
        if (current is not null) vm.SetActiveSection(current);
    }
}
