using Avalonia;
using Avalonia.Controls;
using Hakufu.MVVM.View;

namespace Hakufu.Tests;

/// <summary>Lector táctil: qué hace cada toque o deslizamiento (Review Focus #4).</summary>
public class ReaderGesturesTests
{
    private static readonly Size Area = new(390, 700);
    private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(120);

    private static ReaderGesture Tap(double x, bool zoomed = false) =>
        ReaderGestures.Classify(new Point(x, 350), new Point(x + 3, 352), Quick, Area, zoomed);

    [Fact]
    public void Tapping_the_sides_turns_pages_and_the_middle_toggles_the_bars()
    {
        Assert.Equal(ReaderGesture.Prev,       Tap(40));
        Assert.Equal(ReaderGesture.Next,       Tap(350));
        Assert.Equal(ReaderGesture.ToggleBars, Tap(195));
    }

    [Fact]
    public void Swiping_left_goes_forward_and_right_goes_back()
    {
        Assert.Equal(ReaderGesture.Next,
            ReaderGestures.Classify(new Point(300, 350), new Point(150, 370), Quick, Area, zoomed: false));
        Assert.Equal(ReaderGesture.Prev,
            ReaderGestures.Classify(new Point(100, 350), new Point(260, 330), Quick, Area, zoomed: false));
    }

    [Fact]
    public void Vertical_drags_and_slow_presses_do_nothing()
    {
        Assert.Equal(ReaderGesture.None,
            ReaderGestures.Classify(new Point(200, 100), new Point(230, 500), Quick, Area, zoomed: false));
        Assert.Equal(ReaderGesture.None,
            ReaderGestures.Classify(new Point(40, 350), new Point(42, 351), TimeSpan.FromSeconds(1), Area, zoomed: false));
    }

    [Fact]
    public void Nothing_turns_the_page_while_zoomed_in()
    {
        Assert.Equal(ReaderGesture.None, Tap(40, zoomed: true));
        Assert.Equal(ReaderGesture.None,
            ReaderGestures.Classify(new Point(300, 350), new Point(100, 350), Quick, Area, zoomed: true));
    }
}

public class PageZoomTests
{
    private static readonly Size Area = new(400, 600);

    [Fact]
    public void Pinch_scales_between_one_and_four()
    {
        var zoom = new PageZoom();
        zoom.PinchStarted();
        zoom.Pinch(2.5);
        Assert.Equal(2.5, zoom.Scale);
        Assert.True(zoom.IsZoomed);

        zoom.Pinch(9);
        Assert.Equal(PageZoom.MaxScale, zoom.Scale);

        zoom.PinchStarted();
        zoom.Pinch(0.1);
        Assert.Equal(1, zoom.Scale);
        Assert.False(zoom.IsZoomed);
        Assert.Equal(default, zoom.Offset); // a 1× la página vuelve a su sitio
    }

    [Fact]
    public void Panning_stops_at_the_edges_of_the_zoomed_page()
    {
        var zoom = new PageZoom();
        zoom.PinchStarted();
        zoom.Pinch(2); // la página mide el doble: se puede mover medio área en cada sentido

        zoom.PanBy(new Vector(1000, -1000), Area);
        Assert.Equal(new Vector(200, -300), zoom.Offset);

        zoom.PanBy(new Vector(-50, 20), Area);
        Assert.Equal(new Vector(150, -280), zoom.Offset);
    }

    [Fact]
    public void Panning_does_nothing_at_normal_size_and_reset_goes_back()
    {
        var zoom = new PageZoom();
        zoom.PanBy(new Vector(80, 0), Area);
        Assert.Equal(default, zoom.Offset);

        zoom.PinchStarted();
        zoom.Pinch(3);
        zoom.PanBy(new Vector(80, 0), Area);
        zoom.Reset();
        Assert.Equal(1, zoom.Scale);
        Assert.Equal(default, zoom.Offset);
    }
}

/// <summary>El lector de verdad recibiendo toques (headless no tiene ayudas de toque: se lanzan los eventos).</summary>
public class ReaderTouchTests
{
    private static void Touch(Avalonia.Controls.Control area, Avalonia.Input.Pointer pointer, Point at, bool down)
    {
        var root = (Avalonia.Visual)Avalonia.Controls.TopLevel.GetTopLevel(area)!;
        var props = new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.None,
            down ? Avalonia.Input.PointerUpdateKind.LeftButtonPressed : Avalonia.Input.PointerUpdateKind.LeftButtonReleased);
        var onRoot = area.TranslatePoint(at, root)!.Value;
        area.RaiseEvent(down
            ? new Avalonia.Input.PointerPressedEventArgs(area, pointer, root, onRoot, 0, props, Avalonia.Input.KeyModifiers.None)
            : new Avalonia.Input.PointerReleasedEventArgs(area, pointer, root, onRoot, 0, props, Avalonia.Input.KeyModifiers.None,
                                                           Avalonia.Input.MouseButton.Left));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Tapping_the_right_side_turns_the_page_but_a_mouse_click_does_not()
    {
        using var app = ViewSmoke.StartMobile(390, 844);
        app.Root.Navigation.NavigateTo<Hakufu.MVVM.ViewModel.ReaderViewModel>(
            new Hakufu.MVVM.ViewModel.ReaderNavigationParam(app.SampleManga, 0));
        var view = app.AssertShows<ReaderView>();
        var vm = (Hakufu.MVVM.ViewModel.ReaderViewModel)view.DataContext!;
        var area = view.FindControl<Avalonia.Controls.Grid>("PageArea")!;
        var right = new Point(area.Bounds.Width - 30, area.Bounds.Height / 2);

        var mouse = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);
        Touch(area, mouse, right, down: true);
        Touch(area, mouse, right, down: false);
        app.Pump();
        Assert.Equal(0, vm.CurrentPage);

        var finger = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Touch, true);
        Touch(area, finger, right, down: true);
        Touch(area, finger, right, down: false);
        app.Pump();
        Assert.Equal(1, vm.CurrentPage);

        var middle = new Point(area.Bounds.Width / 2, area.Bounds.Height / 2);
        Touch(area, finger, middle, down: true);
        Touch(area, finger, middle, down: false);
        app.Pump();
        Assert.True(vm.IsZenMode);
    }
}
