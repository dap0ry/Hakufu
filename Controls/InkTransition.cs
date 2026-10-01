using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Hakufu.Controls;

/// <summary>
/// Transición de tema "de tinta", la misma que el cambio de mundo de InkShadow
/// (shaders/world-reveal.frag): una veta de tinta con grano cruza la ventana en
/// diagonal (hacia la derecha o, en espejo, hacia la izquierda) y deja ver el
/// tema nuevo detrás. Va encima de todo: pinta la foto
/// del tema viejo donde la veta aún no ha pasado y la tinta en la veta; el resto
/// es transparente y se ve la app real, ya con el tema nuevo.
///
/// Uso: <see cref="Capture"/> (foto con el tema viejo) → cambiar el tema →
/// <see cref="Play"/>. Sin Skia o si el shader no compila, funde la foto.
/// </summary>
public sealed class InkTransition : Control
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1200);

    // El shader de InkShadow pasado a SkSL. "SAMPLE" se cambia por la forma de
    // leer la foto que entienda el Skia que traiga Avalonia (ver CreateEffect).
    private const string Sksl = """
        uniform shader old;
        uniform float2 size;
        uniform float progress;
        uniform float reverse;   // 1: la veta va de derecha a izquierda

        // El SkSL de SkiaSharp 2.88 no trae smoothstep.
        float smooth(float a, float b, float x) {
            float t = clamp((x - a) / (b - a), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }
        float hash(float2 p) { return fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float noise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            f = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + float2(1, 0)), f.x),
                       mix(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
        }

        half4 main(float2 p) {
            float2 uv = p / size;
            // En espejo solo el borde; la foto se sigue leyendo en p, al derecho.
            uv.x = mix(uv.x, 1.0 - uv.x, reverse);
            float grain = noise(uv * float2(78.0, 112.0));
            float fiber = noise(uv * float2(175.0, 213.0));
            float cloud = noise(uv * float2(11.0, 14.0));
            // Borde de la veta: diagonal, ondulado y con el deshilachado del papel.
            float edge = uv.x + 0.035 * sin(uv.y * 5.6) - 0.13 * uv.y + 0.070 * (cloud - 0.5)
                       + 0.018 * (noise(uv * float2(62.0, 75.0)) - 0.5) + 0.006 * (fiber - 0.5);
            float front = progress * 1.52 - 0.24;
            // wet: ya ha pasado la veta (se ve el tema nuevo).
            float wet = 1.0 - smooth(front - 0.003, front + 0.001, edge);
            // La tinta densa va justo detrás del borde, encima del tema nuevo.
            float pigment = smooth(front - 0.074 - 0.038 * cloud, front - 0.047 - 0.025 * cloud, edge) * wet;
            // Delante del borde el papel viejo chupa un poco de tinta.
            float wick = (1.0 - wet) * (1.0 - smooth(front + 0.001, front + 0.022 + 0.03 * cloud, edge)) * 0.45;
            float3 ink = mix(float3(0.027, 0.026, 0.031), float3(0.17, 0.145, 0.14), grain * 0.28);

            half4 o = SAMPLE;
            float keep = 1.0 - wet;
            float3 rgb = mix(float3(o.rgb), ink * o.a, wick) * keep + ink * pigment * 0.98;
            return half4(half3(rgb), half(o.a * keep + pigment * 0.98));
        }
        """;

    /// <summary>Por qué no compiló el shader (vacío si compiló). Antes que InkEffect: orden de inicialización.</summary>
    internal static string ShaderErrors { get; private set; } = "";

    private static readonly SKRuntimeEffect? InkEffect = CreateEffect();

    private readonly Stopwatch _clock = new();
    private SKImage?            _image;     // foto del tema viejo, para el shader
    private RenderTargetBitmap? _fallback;  // la misma foto si no hay shader
    private double              _scale = 1;
    private bool                _running;
    private bool                _rightToLeft;

    public InkTransition()
    {
        IsHitTestVisible = false;
        IsVisible        = false;
    }

    /// <summary>true si el shader de tinta compiló (si no, la transición es un fundido).</summary>
    public static bool HasInkShader => InkEffect is not null;

    public bool   IsRunning => _running;
    /// <summary>0 → 1 con la curva ya aplicada.</summary>
    public double Progress  { get; private set; }

    /// <summary>
    /// Saca la foto de <paramref name="target"/> tal y como se ve ahora. Si ya
    /// había una transición a medias, sale en la foto y la nueva sigue desde ahí.
    /// </summary>
    public bool Capture(Visual target)
    {
        var size = target.Bounds.Size;
        if (TopLevel.GetTopLevel(target) is not { } top || size.Width < 1 || size.Height < 1)
            return false;

        var scale = top.RenderScaling;
        var px    = PixelSize.FromSize(size, scale);
        var rtb   = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        rtb.Render(target);

        var image = ToSkImage(rtb, px);
        if (image is not null) { rtb.Dispose(); rtb = null; }

        Finish();
        _scale    = scale;
        _image    = image;
        _fallback = rtb;
        return true;
    }

    /// <param name="rightToLeft">La veta entra por la derecha y sale por la izquierda.</param>
    public void Play(bool rightToLeft)
    {
        if (_image is null && _fallback is null) return;
        _rightToLeft = rightToLeft;
        Progress   = 0;
        _running   = true;
        IsVisible  = true;
        _clock.Restart();
        InvalidateVisual();
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    /// <summary>Lleva la transición a un punto fijo y la para ahí (tests, capturas).</summary>
    public void Seek(double progress)
    {
        _running  = false;
        Progress  = Math.Clamp(progress, 0, 1);
        IsVisible = true;
        InvalidateVisual();
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_running) return;
        var t = Math.Min(1, _clock.Elapsed / Duration);
        Progress = EaseInOutCubic(t);
        if (t >= 1) { Finish(); return; }
        InvalidateVisual();
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void Finish()
    {
        _running  = false;
        IsVisible = false;
        Progress  = 0;
        // El SKImage no se libera a mano: puede quedar un fotograma en cola en el
        // hilo de render que aún lo usa. Lo recoge el GC.
        _image = null;
        _fallback?.Dispose();
        _fallback = null;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (_image is not null)
            context.Custom(new InkDrawOperation(rect, _image, (float)_scale, (float)Progress, _rightToLeft));
        else if (_fallback is not null)
            using (context.PushOpacity(1 - Progress))
                context.DrawImage(_fallback, rect);
    }

    private static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    private static SKRuntimeEffect? CreateEffect()
    {
        // Skia nuevo lee un shader hijo con .eval(); el de SkiaSharp 2.88, con sample().
        foreach (var sample in new[] { "old.eval(p)", "sample(old, p)" })
        {
            var effect = SKRuntimeEffect.Create(Sksl.Replace("SAMPLE", sample), out var errors);
            if (effect is not null) { ShaderErrors = ""; return effect; }
            ShaderErrors += $"[{sample}] {errors}\n";
        }
        Trace.WriteLine($"InkTransition: el shader no compila:\n{ShaderErrors}");
        return null;
    }

    private static SKImage? ToSkImage(RenderTargetBitmap rtb, PixelSize px)
    {
        var colorType = rtb.Format == PixelFormat.Rgba8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        var info      = new SKImageInfo(px.Width, px.Height, colorType, SKAlphaType.Premul);
        var buffer    = Marshal.AllocHGlobal(info.BytesSize);
        try
        {
            rtb.CopyPixels(new PixelRect(px), buffer, info.BytesSize, info.RowBytes);
            return SKImage.FromPixelCopy(info, buffer, info.RowBytes);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"InkTransition: no se pudo copiar la foto: {ex.Message}");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Dibuja la foto y la tinta directamente en el lienzo de Skia. Con GPU
    /// (OpenGL) usa el shader; sin GPU, la misma veta con trazados: el Skia de
    /// SkiaSharp 2.88 aborta el proceso (SIGILL) al pintar cualquier shader SkSL
    /// en CPU, y en CPU es como se dibuja en los tests y sin aceleración.
    /// </summary>
    private sealed class InkDrawOperation(Rect bounds, SKImage image, float scale, float progress,
                                          bool rightToLeft)
        : ICustomDrawOperation
    {
        // Tinta de InkShadow: casi negro.
        private static readonly SKColor Ink = new(7, 7, 8);

        public Rect Bounds => bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature) return;
            using var lease = feature.Lease();

            if (InkEffect is not null && lease.GrContext is { Backend: GRBackend.OpenGL })
                DrawWithShader(lease.SkCanvas);
            else
                DrawWithPaths(lease.SkCanvas);
        }

        private void DrawWithShader(SKCanvas canvas)
        {
            // La foto está en píxeles físicos; el lienzo, en DIPs del control.
            using var photo = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
                                             SKMatrix.CreateScale(1 / scale, 1 / scale));
            var uniforms = new SKRuntimeEffectUniforms(InkEffect!)
            {
                ["size"]     = new[] { (float)bounds.Width, (float)bounds.Height },
                ["progress"] = progress,
                ["reverse"]  = rightToLeft ? 1f : 0f
            };
            var children = new SKRuntimeEffectChildren(InkEffect!) { ["old"] = photo };
            using var shader = InkEffect!.ToShader(false, uniforms, children);
            using var paint  = new SKPaint { Shader = shader };
            canvas.DrawRect(0, 0, (float)bounds.Width, (float)bounds.Height, paint);
        }

        /// <summary>
        /// La veta del shader con trazados: el mismo borde (ondulado, inclinado y
        /// deshilachado), con el ruido solo a lo alto. Delante, la foto vieja con
        /// la tinta que chupa el papel; detrás, la banda densa de tinta. De
        /// derecha a izquierda es lo mismo con el lienzo en espejo.
        /// </summary>
        private void DrawWithPaths(SKCanvas canvas)
        {
            float w = (float)bounds.Width, h = (float)bounds.Height;
            if (progress >= 0.999f || w < 1 || h < 1) return;   // ya está todo destapado

            canvas.Save();
            if (rightToLeft) Mirror(canvas, w);

            var front = progress * 1.52f - 0.24f;
            var rows  = Math.Max(2, (int)(h / 2) + 1);
            var edge   = new SKPoint[rows];  // borde de la veta
            var pigIn  = new SKPoint[rows];  // donde la tinta ya es densa
            var pigEnd = new SKPoint[rows];  // donde se acaba la tinta
            for (var i = 0; i < rows; i++)
            {
                var y     = h * i / (rows - 1);
                var v     = y / h;
                var cloud = Noise(v * 14f);
                // Los términos del shader; los finos, más suaves (en 1D serían dientes de sierra).
                var g     = 0.035f * MathF.Sin(v * 5.6f) - 0.13f * v + 0.070f * (cloud - 0.5f)
                          + 0.014f * (Noise(v * 38f + 31f) - 0.5f) + 0.004f * (Noise(v * 140f + 57f) - 0.5f);
                edge[i]   = new SKPoint(w * (front - g), y);
                pigIn[i]  = new SKPoint(w * (front - 0.047f - 0.025f * cloud - g), y);
                pigEnd[i] = new SKPoint(w * (front - 0.074f - 0.038f * cloud - g), y);
            }

            // Delante del borde: la foto del tema viejo, y el papel que chupa tinta.
            using (var ahead = Band(edge, null, w))
            using (var fringe = Band(edge, Shift(edge, w * 0.010f), w))
            using (var wick = new SKPaint
                   {
                       Color = Ink.WithAlpha(120), IsAntialias = true,
                       MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, w * 0.010f)
                   })
            {
                canvas.Save();
                canvas.ClipPath(ahead, SKClipOperation.Intersect, antialias: true);
                // La foto, al derecho: con el lienzo en espejo, otro espejo la deja igual.
                canvas.Save();
                if (rightToLeft) Mirror(canvas, w);
                canvas.DrawImage(image, new SKRect(0, 0, w, h));
                canvas.Restore();
                canvas.DrawPath(fringe, wick);
                canvas.Restore();
            }

            // Detrás del borde: la banda de tinta, densa junto al borde y que se
            // pierde hacia atrás (el smoothstep del shader, aquí con desenfoque).
            using (var tail = Band(pigEnd, edge, w))
            using (var soft = new SKPaint
                   {
                       Color = Ink.WithAlpha(215), IsAntialias = true,
                       MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, w * 0.006f)
                   })
                canvas.DrawPath(tail, soft);
            // Pisa 1,5 px de la foto: si no, entre los dos bordes suavizados se cuela una línea clara.
            using (var dense = Band(pigIn, Shift(edge, 1.5f), w))
            using (var ink = new SKPaint { Color = Ink.WithAlpha(250), IsAntialias = true })
                canvas.DrawPath(dense, ink);

            canvas.Restore();
        }

        private static void Mirror(SKCanvas canvas, float width)
        {
            canvas.Translate(width, 0);
            canvas.Scale(-1, 1);
        }

        /// <summary>Zona entre dos bordes (o desde un borde hasta la derecha si <paramref name="right"/> es null).</summary>
        private static SKPath Band(SKPoint[] left, SKPoint[]? right, float width)
        {
            var path = new SKPath();
            path.MoveTo(left[0]);
            for (var i = 1; i < left.Length; i++) path.LineTo(left[i]);
            if (right is null)
            {
                path.LineTo(width + 1, left[^1].Y);
                path.LineTo(width + 1, left[0].Y);
            }
            else
            {
                for (var i = right.Length - 1; i >= 0; i--) path.LineTo(right[i]);
            }
            path.Close();
            return path;
        }

        private static SKPoint[] Shift(SKPoint[] pts, float dx) =>
            Array.ConvertAll(pts, p => new SKPoint(p.X + dx, p.Y));

        // El ruido de valor del shader, en una dimensión.
        private static float Hash(float n) =>
            Frac(MathF.Sin(n * 127.1f) * 43758.5453f);

        private static float Noise(float x)
        {
            var i = MathF.Floor(x);
            var f = x - i;
            f = f * f * (3 - 2 * f);
            return Hash(i) + (Hash(i + 1) - Hash(i)) * f;
        }

        private static float Frac(float x) => x - MathF.Floor(x);
    }
}
