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

    private const double FlipDepth        = 1800; // perspectiva: más bajo = más exagerado
    private const double SingleFlipMs     = 340;
    private const double HalfSpreadFlipMs = 200; // doble página: cada mitad del giro
    private const double MaxShade         = 0.45; // la hoja se oscurece al ponerse de canto

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
                    await Turn(leaf, pivotLeft: true, 0, -90, SingleFlipMs, Ease.InOut, ct);
                }
                else
                {
                    AddPage(oldLeft, Slot.Single);
                    await WaitForPages(shown, ct);
                    var leaf = AddPage(vm.PageLeft, Slot.Single);
                    await Turn(leaf, pivotLeft: true, -90, 0, SingleFlipMs, Ease.InOut, ct);
                }
            }
            else if (direction > 0)
            {
                AddPage(oldLeft, Slot.Left);
                var front = AddPage(oldRight, Slot.Right);
                await WaitForPages(shown, ct);
                await Turn(front, pivotLeft: true, 0, -90, HalfSpreadFlipMs, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                var back = AddPage(vm.PageLeft, Slot.Left);
                await Turn(back, pivotLeft: false, 90, 0, HalfSpreadFlipMs, Ease.Out, ct);
            }
            else
            {
                AddPage(oldRight, Slot.Right);
                var front = AddPage(oldLeft, Slot.Left);
                await WaitForPages(shown, ct);
                await Turn(front, pivotLeft: false, 0, 90, HalfSpreadFlipMs, Ease.In, ct);
                FlipLayer.Children.Remove(front);
                var back = AddPage(vm.PageRight, Slot.Right);
                await Turn(back, pivotLeft: true, -90, 0, HalfSpreadFlipMs, Ease.Out, ct);
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
        var image = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);

        var page = new Panel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { image, new Border { Background = Brushes.Black, Opacity = 0 } },
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

        FlipLayer.Children.Add(page);
        return page;
    }

    /// <summary>Gira la hoja en Y alrededor de su borde izquierdo o derecho (el lomo).</summary>
    private async Task Turn(Panel page, bool pivotLeft, double from, double to,
                            double durationMs, Ease ease, CancellationToken ct)
    {
        var shade = (Border)page.Children[1];
        var rotation = new Rotate3DTransform { AngleY = from, Depth = FlipDepth };
        page.RenderTransformOrigin = new RelativePoint(pivotLeft ? 0 : 1, 0.5, RelativeUnit.Relative);
        page.RenderTransform = rotation;

        var clock = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / durationMs);
            var eased = ease switch
            {
                Ease.In  => t * t,
                Ease.Out => 1 - (1 - t) * (1 - t),
                _        => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            };
            var angle = from + (to - from) * eased;
            rotation.AngleY = angle;
            shade.Opacity = MaxShade * Math.Abs(angle) / 90;
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
