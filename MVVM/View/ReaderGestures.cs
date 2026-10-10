using Avalonia;

namespace Hakufu.MVVM.View;

public enum ReaderGesture { None, Prev, Next, ToggleBars }

/// <summary>
/// Lector con el dedo (iPhone/iPad): un toque en el tercio izquierdo o derecho pasa
/// de página (como los botones del lector), en el centro muestra/oculta las barras
/// (modo zen); deslizar hacia la izquierda avanza y hacia la derecha retrocede. Con
/// la página ampliada el dedo la mueve: no pasa nada más.
/// </summary>
public static class ReaderGestures
{
    /// <summary>Lo que se puede mover el dedo y seguir siendo un toque.</summary>
    public const double TapSlop = 12;
    public static readonly TimeSpan TapTime = TimeSpan.FromMilliseconds(400);
    /// <summary>Recorrido horizontal mínimo de un deslizamiento.</summary>
    public const double SwipeDistance = 60;

    public static ReaderGesture Classify(Point start, Point end, TimeSpan duration, Size area, bool zoomed)
    {
        if (zoomed) return ReaderGesture.None;

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        if (Math.Abs(dx) <= TapSlop && Math.Abs(dy) <= TapSlop)
        {
            if (duration > TapTime) return ReaderGesture.None; // dejar el dedo quieto no es un toque
            var third = area.Width / 3;
            return start.X < third     ? ReaderGesture.Prev
                 : start.X > 2 * third ? ReaderGesture.Next
                 : ReaderGesture.ToggleBars;
        }

        // Deslizar: sobre todo en horizontal.
        if (Math.Abs(dx) >= SwipeDistance && Math.Abs(dx) > 1.5 * Math.Abs(dy))
            return dx < 0 ? ReaderGesture.Next : ReaderGesture.Prev;

        return ReaderGesture.None;
    }
}

/// <summary>
/// Ampliar la página con dos dedos (de 1× a 4×) y moverla con uno sin que se salga:
/// con escala s, la página mide s veces el área y se puede desplazar (s−1)/2 del
/// área a cada lado. A 1× vuelve a su sitio.
/// </summary>
public sealed class PageZoom
{
    public const double MaxScale = 4;

    private double _pinchBase = 1;

    public double Scale  { get; private set; } = 1;
    public Vector Offset { get; private set; }
    public bool   IsZoomed => Scale > 1.01;

    /// <summary>Empieza un pellizco: las escalas que lleguen son relativas a la de ahora.</summary>
    public void PinchStarted() => _pinchBase = Scale;

    public void Pinch(double relativeScale)
    {
        Scale = Math.Clamp(_pinchBase * relativeScale, 1, MaxScale);
        if (!IsZoomed) Reset();
    }

    public void PanBy(Vector delta, Size area)
    {
        if (!IsZoomed) return;
        var maxX = (Scale - 1) * area.Width / 2;
        var maxY = (Scale - 1) * area.Height / 2;
        var next = Offset + delta;
        Offset = new Vector(Math.Clamp(next.X, -maxX, maxX), Math.Clamp(next.Y, -maxY, maxY));
    }

    public void Reset()
    {
        Scale = 1;
        _pinchBase = 1;
        Offset = default;
    }
}
