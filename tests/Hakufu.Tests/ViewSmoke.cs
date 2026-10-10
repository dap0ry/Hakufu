using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.Tests;

/// <summary>
/// Monta la app de verdad (MainWindow + CompositionRoot) sin pantalla, para
/// comprobar que cada vista carga: estilos, recursos y bindings resuelven y
/// no hay excepciones al hacer layout. Uso dentro de un [AvaloniaFact]:
///
///   using var app = ViewSmoke.Start();          // biblioteca de ejemplo
///   app.Root.Navigation.NavigateTo&lt;LibraryViewModel&gt;();
///   app.AssertShows&lt;Hakufu.MVVM.View.LibraryView&gt;();
/// </summary>
public sealed class ViewSmoke : IDisposable
{
    private readonly TempDataDir _tmp;
    private readonly MobileMode? _mobile;

    public CompositionRoot Root   { get; }
    /// <summary>La ventana de escritorio (solo con Start, no con StartMobile).</summary>
    public MainWindow      Window => (MainWindow)Host;
    /// <summary>Lo que hay en pantalla: MainWindow, o en StartMobile una ventana sin marco con MainView dentro (como iOS).</summary>
    public Window          Host   { get; }
    public MainView        View   { get; }
    /// <summary>Colección de ejemplo (subcarpeta de la biblioteca) con dos tomos CBZ (uno leído a medias).</summary>
    public Collection      SampleCollection { get; }
    public Manga           SampleManga      { get; }

    private ViewSmoke(TempDataDir tmp, CompositionRoot root, Collection col, Manga manga,
                      Size? mobile = null, MobileMode? mobileMode = null)
    {
        _tmp = tmp;
        _mobile = mobileMode;
        Root = root;
        SampleCollection = col;
        SampleManga = manga;
        root.ApplySavedAppearance();
        if (mobile is { } size)
        {
            View = new MainView { DataContext = root.CreateMainViewModel() };
            Host = new Window { Content = View, Width = size.Width, Height = size.Height,
                                SystemDecorations = SystemDecorations.None };
        }
        else
        {
            var window = new MainWindow { DataContext = root.CreateMainViewModel(), Width = 1280, Height = 780 };
            View = window.MainView;
            Host = window;
        }
        Host.Show();
        Pump();
    }

    /// <summary>
    /// Como en iPhone/iPad: MainView sola ocupando una pantalla de width×height, con
    /// AppPlatform.IsMobile y la biblioteca de ejemplo como carpeta fija.
    /// </summary>
    public static ViewSmoke StartMobile(double width, double height, bool darkTheme = false)
        => Start(darkTheme, withLibrary: true, mobile: new Size(width, height));

    /// <param name="withLibrary">false: sin carpeta de biblioteca elegida (instalación nueva).</param>
    public static ViewSmoke Start(bool darkTheme = false, bool withLibrary = true, Size? mobile = null)
    {
        var tmp  = new TempDataDir();
        var repo = new JsonDataRepository();
        repo.LoadAsync().GetAwaiter().GetResult();
        repo.Current.ActiveTheme = darkTheme ? "Dark" : "Light";
        if (!withLibrary)
        {
            var empty = mobile is null ? null : new MobileMode(Directory.CreateDirectory(Path.Combine(tmp.Root, "Documents")).FullName);
            return new ViewSmoke(tmp, new CompositionRoot(repo, new FakeUpdateService()), new Collection(), new Manga(), mobile, empty);
        }

        // Carpeta de la biblioteca con una subcarpeta (colección) y dos tomos.
        const string colName = "Ataque a los Titanes";
        var root = Path.Combine(tmp.Root, "Mis mangas");
        var dir  = Path.Combine(root, colName);
        var t2   = "Tomo 2 con un título bastante largo para ver el recorte";
        var m1 = new Manga { Title = "Tomo 1", TotalPages = 3, RelativePath = $"{colName}/Tomo 1.cbz",
                             FilePath = Fixtures.MakeCbz(dir, "Tomo 1.cbz", "1.png", "2.png", "3.png") };
        var m2 = new Manga { Title = t2, TotalPages = 2, RelativePath = $"{colName}/{t2}.cbz",
                             FilePath = Fixtures.MakeCbz(dir, $"{t2}.cbz", "1.png", "2.png"), IsFavorite = true };
        var col = new Collection { Name = colName, RelativePath = colName, Description = "Descripción de prueba",
                                   MangaIds = [m1.Id, m2.Id] };
        repo.Current.LibraryRoot = root;
        repo.Current.Mangas.AddRange([m1, m2]);
        repo.Current.Collections.Add(col);
        repo.Current.Progress.Add(new ReadingProgress { MangaId = m1.Id, CurrentPage = 2 });
        repo.Current.History.Add(new ReadingHistoryEntry { MangaId = m1.Id });

        var mobileMode = mobile is null ? null : new MobileMode(root);
        return new ViewSmoke(tmp, new CompositionRoot(repo, new FakeUpdateService()), col, m1, mobile, mobileMode);
    }

    /// <summary>Procesa la cola del hilo de UI (carga async de portadas, layout…).</summary>
    public void Pump()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Comprueba que la ventana está mostrando una vista TView (no el "Vista pendiente").</summary>
    public TView AssertShows<TView>() where TView : Control
    {
        Pump();
        var view = Host.GetVisualDescendants().OfType<TView>().FirstOrDefault();
        Assert.True(view is not null,
            $"La ventana no muestra {typeof(TView).Name}. ¿Falta la vista o falla el ViewLocator?");
        Assert.True(view!.Bounds.Width > 0, $"{typeof(TView).Name} no tiene tamaño tras el layout.");
        return view;
    }

    /// <summary>Abre un modal a través del DialogService real y comprueba que se ve TView.</summary>
    public TView AssertModal<TView>(BaseViewModel modalVm) where TView : Control
    {
        Root.Dialog.ShowModal(modalVm);
        return AssertShows<TView>();
    }

    public void Dispose()
    {
        Host.Close();
        _mobile?.Dispose();
        _tmp.Dispose();
    }
}
