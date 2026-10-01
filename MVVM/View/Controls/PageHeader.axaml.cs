using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Hakufu.MVVM.View.Controls;

/// <summary>Cabecera común de las pantallas interiores (ver PageHeader.axaml).</summary>
public partial class PageHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Subtitle));
    public static readonly StyledProperty<string> BackTextProperty =
        AvaloniaProperty.Register<PageHeader, string>(nameof(BackText), "← Inicio");
    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<PageHeader, ICommand?>(nameof(BackCommand));
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PageHeader, object?>(nameof(Actions));

    public string?   Title       { get => GetValue(TitleProperty);       set => SetValue(TitleProperty, value); }
    public string?   Subtitle    { get => GetValue(SubtitleProperty);    set => SetValue(SubtitleProperty, value); }
    public string    BackText    { get => GetValue(BackTextProperty);    set => SetValue(BackTextProperty, value); }
    public ICommand? BackCommand { get => GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }
    /// <summary>Botones a la derecha (opcional).</summary>
    public object?   Actions     { get => GetValue(ActionsProperty);     set => SetValue(ActionsProperty, value); }

    public PageHeader() => InitializeComponent();
}
