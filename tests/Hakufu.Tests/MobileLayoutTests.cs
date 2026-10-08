using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>
/// Cada pantalla a tamaño de iPhone (390×844), iPad mini en vertical (744×1133) y
/// iPad en horizontal (1180×820), en claro y oscuro: nada que se pueda tocar ni leer
/// se sale por los lados (Review Focus #2). Deja una captura de cada una en
/// bin/…/mobile-shots/ para revisarlas a ojo.
/// </summary>
public class MobileLayoutTests
{
    public static readonly TheoryData<int, int, bool> Sizes = new()
    {
        { 390, 844, false }, { 390, 844, true },
        { 744, 1133, false },
        { 1180, 820, true },
    };

    private static readonly string ShotsDir = Path.Combine(AppContext.BaseDirectory, "mobile-shots");

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public void Every_screen_fits_the_width(int width, int height, bool dark)
    {
        using var app = ViewSmoke.StartMobile(width, height, dark);
        var problems = new List<string>();
        var nav = app.Root.Navigation;

        void Check(string name, Action open)
        {
            open();
            app.Pump();
            Shot(app, $"{width}x{height}-{(dark ? "dark" : "light")}-{name}");
            problems.AddRange(Overflowing(app.Host, width).Select(p => $"{name}: {p}"));
        }

        Check("home",       () => nav.NavigateTo<HomeViewModel>());
        Check("library",    () => nav.NavigateTo<LibraryViewModel>());
        Check("collection", () => nav.NavigateTo<CollectionDetailViewModel>(app.SampleCollection.Id));
        Check("reorder",    () => ((CollectionDetailViewModel)nav.CurrentViewModel!).OpenReorderCommand.Execute(null));
        app.Root.Dialog.CloseModal();
        Check("reader",     () => nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0)));
        Check("profile",    () => nav.NavigateTo<ProfileViewModel>());
        Check("edit-profile", () => ((ProfileViewModel)nav.CurrentViewModel!).EditProfileCommand.Execute(null));
        app.Root.Dialog.CloseModal();
        Check("settings",   () => nav.NavigateTo<SettingsViewModel>());
        Check("backup",     () => nav.NavigateTo<BackupViewModel>());
        Check("help",       () => nav.NavigateTo<HelpViewModel>());
        Check("legal",      () => nav.NavigateTo<LegalViewModel>());

        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
    }

    /// <summary>Botones y textos visibles que acaban fuera de [0, width] (salvo en un scroll horizontal).</summary>
    private static IEnumerable<string> Overflowing(TopLevel host, double width)
    {
        foreach (var c in host.GetVisualDescendants().OfType<Control>())
        {
            if (c is not (Button or TextBlock) || !c.IsEffectivelyVisible || c.Bounds.Width <= 0) continue;
            if (c.FindAncestorOfType<ScrollViewer>() is { HorizontalScrollBarVisibility: not ScrollBarVisibility.Disabled })
                continue;
            // Lo que un padre recorta del todo no se ve (las tarjetas de exportar del perfil
            // viven en un Canvas de 0×0 recortado). Lo demás se mide entero: un texto que
            // desborda y el scroll recorta sigue estando mal.
            if (OnScreen(c, host) is not { } rect || IsClippedAway(c, host)) continue;
            if (rect.X < -1 || rect.Right > width + 1)
            {
                var label = c switch
                {
                    TextBlock t => $"TextBlock \"{Trim(t.Text)}\"",
                    Button b    => $"Button \"{Trim(b.Content?.ToString())}\"",
                    _           => c.GetType().Name,
                };
                yield return $"{label} x={rect.X:0}..{rect.Right:0}";
            }
        }
    }

    /// <summary>Rectángulo en pantalla, ya escalado (Viewbox, LayoutTransform…).</summary>
    private static Rect? OnScreen(Visual v, Visual host)
        => v.TranslatePoint(new Point(0, 0), host) is { } a &&
           v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), host) is { } b
            ? new Rect(a, b).Normalize()
            : null;

    private static bool IsClippedAway(Control c, Visual host)
    {
        if (OnScreen(c, host) is not { } rect) return true;
        for (var v = c.GetVisualParent(); v is not null && v is not MainView; v = v.GetVisualParent())
        {
            // El recorte de los scroll (lo de más abajo) y el de la página no cuentan.
            if (!v.ClipToBounds || v is ScrollContentPresenter || v is Border { Name: "PageHost" }) continue;
            if (OnScreen(v, host) is not { } clip) continue;
            rect = rect.Intersect(clip);
            if (rect.Width <= 0 || rect.Height <= 0) return true;
        }
        return false;
    }

    private static string Trim(string? s) => s is null ? "" : s.Length > 40 ? s[..40] + "…" : s;

    private static void Shot(ViewSmoke app, string name)
    {
        Directory.CreateDirectory(ShotsDir);
        using var frame = app.Host.CaptureRenderedFrame();
        frame?.Save(Path.Combine(ShotsDir, name + ".png"));
    }
}
