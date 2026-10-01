using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Hakufu.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
// Los tests cambian HAKUFU_DATA_DIR (variable de proceso): nada en paralelo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Hakufu.Tests;

/// <summary>
/// Antes de nada, todo el proceso de tests apunta a una carpeta de datos
/// aislada: aunque algo guarde fuera de un TempDataDir (o tarde, en segundo
/// plano), nunca puede tocar la biblioteca real de quien ejecuta los tests.
/// </summary>
internal static class TestSandbox
{
    public static readonly string DataDir =
        Path.Combine(Path.GetTempPath(), $"Hakufu tests sandbox {Guid.NewGuid():N}");

    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", DataDir);
}

public static class TestAppBuilder
{
    // La App real (estilos, temas, converters de App.axaml) sin ventana: en
    // headless no hay IClassicDesktopStyleApplicationLifetime, así que no
    // arranca la composición de servicios.
    // Skia real (no el dibujo "de mentira" de headless) para poder decodificar
    // y guardar imágenes igual que en la app.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Hakufu.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Carpeta temporal con espacios y tildes (Review Focus #5) que hace de carpeta de datos.</summary>
public sealed class TempDataDir : IDisposable
{
    private readonly string? _previous;
    public string Root { get; }

    public TempDataDir()
    {
        Root = Path.Combine(Path.GetTempPath(), $"Hakufu tests ñandú {Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        _previous = Environment.GetEnvironmentVariable("HAKUFU_DATA_DIR");
        Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", Path.Combine(Root, "datos"));
    }

    public string DataDir => Path.Combine(Root, "datos");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HAKUFU_DATA_DIR", _previous);
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}

public static class Fixtures
{
    // PNG de 2x3 píxeles: una página vertical (las de 1x1 no son ni verticales ni apaisadas).
    public static readonly byte[] TallPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAADCAIAAAA2iEnWAAAAFElEQVR4nGNkYDjBwMDAxAAGUAoADmQAzvryGdcAAAAASUVORK5CYII=");

    // PNG de 1x1 píxel rojo.
    public static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    /// <summary>CBZ con las imágenes dadas (nombre de entrada → PNG de 1x1).</summary>
    public static string MakeCbz(string dir, string name, params string[] entries)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var s = zip.CreateEntry(entry).Open();
            s.Write(TinyPng);
        }
        return path;
    }

    /// <summary>PDF mínimo válido de una página en blanco (para comprobar que pdfium carga en cada SO).</summary>
    public static string MakePdf(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>"
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        var path = Path.Combine(dir, name);
        File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        return path;
    }
}
