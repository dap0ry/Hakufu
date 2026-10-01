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
    private const double LiftScale        = 0.035; // cuánto se acerca la hoja al levantarse
    private const double BendDegrees      = 2.2;   // cuánto se comba

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

        // Teclas configurables en Ajustes → Atajos de teclado.
        var command = vm.CommandForKey(e);
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
        if (!_vm.AnimatePageTurns) return;
        var single = SingleFlipMs * _vm.PageTurnSpeedFactor;
        var half   = HalfSpreadFlipMs * _vm.PageTurnSpeedFactor;

        var cts = new CancellationTokenSource();
        _flipCts = cts;
        var ct = cts.Token;
        _pagesShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shown = _pagesShown.Task;

        var vm = _vm;
        var oldTwo   = vm.ShowsTwoPages;
        var oldLeft  = vm.PageLeft;
        var oldRight = vm.PageRight;

        try
        {
            // Tapar el cambio con una copia exacta de lo que se ve ahora.
            List<Panel> cover = oldTwo
                ? [AddPage(oldLeft, Slot.Left), AddPage(oldRight, Slot.Right)]
                : [AddPage(oldLeft, Slot.Single)];
            await WaitForPages(shown, ct);

            var newTwo  = vm.ShowsTwoPages;
            var newLeft = vm.PageLeft;
            // La hoja solo gira sobre el lomo si lo de antes y lo de después tienen la
            // misma forma; si no (vertical ↔ apaisada, dos ↔ una), un fundido limpio.
            var sameShape = oldTwo == newTwo &&
                            (oldTwo || ReaderViewModel.IsWide(oldLeft) == ReaderViewModel.IsWide(newLeft));

            if (!sameShape)
            {
                await FadeAway(cover, direction, single * 0.75, ct);
            }
            else if (!oldTwo)
            {
                if (direction > 0)
                {
                    var leaf = cover[0];
                    var shadow = AddShadowUnder(leaf, newLeft, Slot.Single, spineOnLeft: true);
                    await Turn(leaf, shadow, pivotLeft: true, 0, -90, single, Ease.InOut, ct);
                }
                else
                {
                    var shadow = AddShadow(oldLeft, Slot.Single, spineOnLeft: true);
                    var leaf = AddPage(newLeft, Slot.Single);
                    await Turn(leaf, shadow, pivotLeft: true, -90, 0, single, Ease.InOut, ct);
                }
            }
            else if (direction > 0)
            {
                var front = cover[1];
                var under = AddShadowUnder(front, vm.PageRight, Slot.Right, spineOnLeft: true);
                await Turn(front, under, pivotLeft: true, 0, -90, half, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                FlipLayer.Children.Remove(under);
                var landing = AddShadow(oldLeft, Slot.Left, spineOnLeft: false);
                var back = AddPage(newLeft, Slot.Left);
                await Turn(back, landing, pivotLeft: false, 90, 0, half, Ease.Out, ct);
            }
            else
            {
                var front = cover[0];
                var under = AddShadowUnder(front, newLeft, Slot.Left, spineOnLeft: false);
                await Turn(front, under, pivotLeft: false, 0, 90, half, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                FlipLayer.Children.Remove(under);
                var landing = AddShadow(oldRight, Slot.Right, spineOnLeft: true);
                var back = AddPage(vm.PageRight, Slot.Right);
                await Turn(back, landing, pivotLeft: true, -90, 0, half, Ease.Out, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return; // otra vuelta de página ya limpió la capa
        }

        if (_flipCts == cts) CancelFlip();
    }

    /// <summary>
    /// Cuando lo de antes y lo de después no tienen la misma forma: lo viejo se
    /// aparta un poco hacia donde se pasa, encoge y se desvanece sobre lo nuevo.
    /// </summary>
    private async Task FadeAway(List<Panel> pages, int direction, double durationMs, CancellationToken ct)
    {
        var moves = pages.Select(p =>
        {
            var scale = new ScaleTransform(1, 1);
            var move = new TranslateTransform();
            p.RenderTransformOrigin = RelativePoint.Center;
            p.RenderTransform = new TransformGroup { Children = { scale, move } };
            return (p, scale, move);
        }).ToList();

        var clock = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / durationMs);
            var e = 1 - Math.Pow(1 - t, 3);
            foreach (var (p, scale, move) in moves)
            {
                p.Opacity = 1 - e;
                scale.ScaleX = scale.ScaleY = 1 - 0.04 * e;
                move.X = -direction * 36 * e;
            }
            if (t >= 1) return;
            await NextFrame();
        }
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
            // image · sombreado hacia el borde libre · brillo que la recorre al girar
            Children = { image, new Border { Opacity = 0 }, new Border { Opacity = 0 } },
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

    /// <summary>Banda de brillo diagonal en la posición <paramref name="at"/> (0 lomo, 1 borde libre).</summary>
    private static LinearGradientBrush Gloss(bool spineOnLeft, double at)
    {
        var p = Math.Clamp(at, 0, 1);
        var a = Math.Max(0, p - 0.18);
        var b = Math.Min(1, p + 0.18);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(spineOnLeft ? 0 : 1, 0.15, RelativeUnit.Relative),
            EndPoint   = new RelativePoint(spineOnLeft ? 1 : 0, 0.85, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 255, 255, 255), a),
                new GradientStop(Color.FromArgb(60, 255, 255, 255), p),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), b),
            },
        };
    }

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
        var gloss = (Border)page.Children[2];
        var cast = castShadow.Children[1];

        // Papel, no tabla: al levantarse la hoja se acerca un poco (escala), se
        // comba (inclinación vertical leve hacia el borde libre) y la recorre un
        // brillo; todo en el plano de la hoja, antes del giro 3D.
        var bend = new SkewTransform();
        var liftScale = new ScaleTransform(1, 1);
        var rotation = new Rotate3DTransform { AngleY = from, Depth = FlipDepth };
        page.RenderTransformOrigin = new RelativePoint(pivotLeft ? 0 : 1, 0.5, RelativeUnit.Relative);
        page.RenderTransform = new TransformGroup { Children = { bend, liftScale, rotation } };

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
            liftScale.ScaleX = liftScale.ScaleY = 1 + LiftScale * lift;
            bend.AngleY = (pivotLeft ? -1 : 1) * BendDegrees * lift;
            // El brillo cruza la hoja del lomo al borde libre según se levanta.
            var band = Math.Abs(angle) / 90;
            gloss.Background = Gloss(pivotLeft, band);
            gloss.Opacity = 0.9 * lift;

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
