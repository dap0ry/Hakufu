using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.MVVM.View;

public partial class ReaderView : UserControl
{
    private TopLevel? _topLevel;
    private ReaderViewModel? _vm;

    // Animación de pasar la hoja (ver FlipLayer en el .axaml).
    private CancellationTokenSource? _flipCts;
    private TaskCompletionSource? _pagesShown;

    private const double FlipDepth        = 3200; // perspectiva: más bajo = más exagerado
    private const double SingleFlipMs     = 420;
    private const double HalfSpreadFlipMs = 240;  // doble página: cada mitad del giro
    private const double LeafShade        = 0.30; // la hoja se oscurece hacia el borde libre
    private const byte   CastShadowAlpha  = 0x5C; // sombra que la hoja deja junto al lomo

    private enum Slot { Single, Left, Right }
    private enum Ease { In, Out, InOut }

    public ReaderView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // El teclado se escucha en la ventana (en túnel) mientras el lector
        // está visible: funciona aunque el foco se haya perdido al pasar a
        // pantalla completa (modo zen) o tras pulsar un botón de la barra.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, TopLevel_KeyDown, RoutingStrategies.Tunnel);

        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, TopLevel_KeyDown);
        _topLevel = null;
        CancelFlip();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PageTurning -= Vm_PageTurning;
            _vm.PagesLoaded -= Vm_PagesLoaded;
        }
        _vm = DataContext as ReaderViewModel;
        if (_vm is not null)
        {
            _vm.PageTurning += Vm_PageTurning;
            _vm.PagesLoaded += Vm_PagesLoaded;
        }
    }

    private void TopLevel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not ReaderViewModel vm) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        ICommand? command = e.Key switch
        {
            Key.Right or Key.Space => vm.NextPageCommand,
            Key.Left               => vm.PrevPageCommand,
            Key.Escape             => vm.ExitZenModeCommand,
            Key.F or Key.F11       => vm.ToggleZenModeCommand,
            Key.D1 or Key.D2       => vm.ToggleTwoPageCommand,
            _                      => null,
        };
        if (command is null) return;

        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    private void PageArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus(); // mantener el foco en el lector tras hacer clic en la página
    }

    // ── Pasar la hoja ────────────────────────────────────────────────────
    //
    // Como un libro abierto con el lomo a la izquierda (página simple) o en
    // el centro (doble página). Se pinta encima una copia de la página vieja
    // que tapa el cambio; cuando la nueva ya está cargada debajo, la copia
    // gira alrededor del lomo hasta ponerse de canto y desaparece. En doble
    // página, la segunda mitad del giro la hace el reverso de la hoja (la
    // página nueva del otro lado). Hacia atrás es lo mismo al revés.

    private void Vm_PagesLoaded(object? sender, int page) => _pagesShown?.TrySetResult();

    private async void Vm_PageTurning(object? sender, int direction)
    {
        if (_vm is null) return;
        CancelFlip();

        var cts = new CancellationTokenSource();
        _flipCts = cts;
        var ct = cts.Token;
        _pagesShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shown = _pagesShown.Task;

        var vm = _vm;
        var oldLeft  = vm.PageLeft;
        var oldRight = vm.PageRight;

        try
        {
            if (!vm.IsTwoPageMode)
            {
                if (direction > 0)
                {
                    var leaf = AddPage(oldLeft, Slot.Single);
                    await WaitForPages(shown, ct);
                    var shadow = AddShadowUnder(leaf, vm.PageLeft, Slot.Single, spineOnLeft: true);
                    await Turn(leaf, shadow, pivotLeft: true, 0, -90, SingleFlipMs, Ease.InOut, ct);
                }
                else
                {
                    AddPage(oldLeft, Slot.Single);
                    await WaitForPages(shown, ct);
                    var shadow = AddShadow(oldLeft, Slot.Single, spineOnLeft: true);
                    var leaf = AddPage(vm.PageLeft, Slot.Single);
                    await Turn(leaf, shadow, pivotLeft: true, -90, 0, SingleFlipMs, Ease.InOut, ct);
                }
            }
            else if (direction > 0)
            {
                AddPage(oldLeft, Slot.Left);
                var front = AddPage(oldRight, Slot.Right);
                await WaitForPages(shown, ct);
                var under = AddShadowUnder(front, vm.PageRight, Slot.Right, spineOnLeft: true);
                await Turn(front, under, pivotLeft: true, 0, -90, HalfSpreadFlipMs, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                FlipLayer.Children.Remove(under);
                var landing = AddShadow(oldLeft, Slot.Left, spineOnLeft: false);
                var back = AddPage(vm.PageLeft, Slot.Left);
                await Turn(back, landing, pivotLeft: false, 90, 0, HalfSpreadFlipMs, Ease.Out, ct);
            }
            else
            {
                AddPage(oldRight, Slot.Right);
                var front = AddPage(oldLeft, Slot.Left);
                await WaitForPages(shown, ct);
                var under = AddShadowUnder(front, vm.PageLeft, Slot.Left, spineOnLeft: false);
                await Turn(front, under, pivotLeft: false, 0, 90, HalfSpreadFlipMs, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                FlipLayer.Children.Remove(under);
                var landing = AddShadow(oldRight, Slot.Right, spineOnLeft: true);
                var back = AddPage(vm.PageRight, Slot.Right);
                await Turn(back, landing, pivotLeft: true, -90, 0, HalfSpreadFlipMs, Ease.Out, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return; // otra vuelta de página ya limpió la capa
        }

        if (_flipCts == cts) CancelFlip();
    }

    private void CancelFlip()
    {
        _flipCts?.Cancel();
        _flipCts = null;
        FlipLayer.Children.Clear();
    }

    private static async Task WaitForPages(Task shown, CancellationToken ct)
    {
        // Las páginas vecinas suelen estar precargadas; si una tarda, no
        // dejamos la hoja congelada más de un momento.
        await Task.WhenAny(shown, Task.Delay(800, ct));
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>Copia de una página colocada exactamente donde está la real.</summary>
    private Panel AddPage(Bitmap? bitmap, Slot slot)
    {
        var page = MakePage(bitmap, slot);
        FlipLayer.Children.Add(page);
        return page;
    }

    /// <summary>
    /// Sombra que proyecta la hoja sobre la página de debajo: un degradado que
    /// nace en el lomo. Ocupa lo mismo que esa página (la imagen va invisible,
    /// solo para tomar su tamaño).
    /// </summary>
    private Panel AddShadow(Bitmap? under, Slot slot, bool spineOnLeft)
    {
        var shadow = MakeShadow(under, slot, spineOnLeft);
        FlipLayer.Children.Add(shadow);
        return shadow;
    }

    private Panel AddShadowUnder(Panel leaf, Bitmap? under, Slot slot, bool spineOnLeft)
    {
        var shadow = MakeShadow(under, slot, spineOnLeft);
        FlipLayer.Children.Insert(FlipLayer.Children.IndexOf(leaf), shadow);
        return shadow;
    }

    private static Panel MakeShadow(Bitmap? under, Slot slot, bool spineOnLeft)
    {
        var shadow = MakePage(under, slot);
        shadow.Children[0].Opacity = 0;
        shadow.Children[1].Opacity = 0;
        ((Border)shadow.Children[1]).Background = SpineGradient(spineOnLeft, CastShadowAlpha, 0, 0.45);
        return shadow;
    }

    private static Panel MakePage(Bitmap? bitmap, Slot slot)
    {
        var image = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);

        var page = new Panel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { image, new Border { Opacity = 0 } },
        };

        switch (slot)
        {
            case Slot.Single:
                Grid.SetColumnSpan(page, 3);
                page.HorizontalAlignment = HorizontalAlignment.Center;
                page.Margin = new Thickness(8);
                break;
            case Slot.Left:
                Grid.SetColumn(page, 0);
                page.HorizontalAlignment = HorizontalAlignment.Right;
                page.Margin = new Thickness(8, 8, 0, 8);
                break;
            case Slot.Right:
                Grid.SetColumn(page, 2);
                page.HorizontalAlignment = HorizontalAlignment.Left;
                page.Margin = new Thickness(0, 8, 8, 8);
                break;
        }
        return page;
    }

    /// <summary>Degradado horizontal de negro: alphaAtSpine en el lomo → alphaFar en <paramref name="reach"/>.</summary>
    private static LinearGradientBrush SpineGradient(bool spineOnLeft, byte alphaAtSpine, byte alphaFar, double reach) => new()
    {
        StartPoint = new RelativePoint(spineOnLeft ? 0 : 1, 0, RelativeUnit.Relative),
        EndPoint   = new RelativePoint(spineOnLeft ? 1 : 0, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(alphaAtSpine, 0, 0, 0), 0),
            new GradientStop(Color.FromArgb(alphaFar, 0, 0, 0), reach),
        },
    };

    /// <summary>
    /// Gira la hoja en Y alrededor de su borde izquierdo o derecho (el lomo).
    /// A la vez oscurece la hoja hacia su borde libre y hace crecer la sombra
    /// que proyecta (<paramref name="castShadow"/>), las dos según lo levantada
    /// que esté (seno del ángulo).
    /// </summary>
    private async Task Turn(Panel page, Panel castShadow, bool pivotLeft, double from, double to,
                            double durationMs, Ease ease, CancellationToken ct)
    {
        var shade = (Border)page.Children[1];
        shade.Background = SpineGradient(spineOnLeft: pivotLeft, 0, 255, 1);
        var cast = castShadow.Children[1];

        var rotation = new Rotate3DTransform { AngleY = from, Depth = FlipDepth };
        page.RenderTransformOrigin = new RelativePoint(pivotLeft ? 0 : 1, 0.5, RelativeUnit.Relative);
        page.RenderTransform = rotation;

        var clock = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / durationMs);
            // Senos: el final de Ease.In y el principio de Ease.Out van a la
            // misma velocidad, así las dos mitades de la doble página empalman.
            var eased = ease switch
            {
                Ease.In  => 1 - Math.Cos(t * Math.PI / 2),
                Ease.Out => Math.Sin(t * Math.PI / 2),
                _        => (1 - Math.Cos(t * Math.PI)) / 2,
            };
            var angle = from + (to - from) * eased;
            rotation.AngleY = angle;

            var lift = Math.Sin(Math.Abs(angle) * Math.PI / 180);
            shade.Opacity = LeafShade * lift;
            cast.Opacity  = lift;

            if (t >= 1) return;
            await NextFrame();
        }
    }

    private Task NextFrame()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return Task.Delay(16);
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        top.RequestAnimationFrame(_ => frame.TrySetResult());
        return frame.Task;
    }
}
