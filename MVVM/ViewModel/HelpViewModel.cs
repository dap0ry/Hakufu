using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Una entrada del índice de la guía. Id = nombre del control de la sección en la vista.</summary>
public class HelpSection(string id, string number, string title) : BaseViewModel
{
    public string Id     { get; } = id;
    public string Number { get; } = number;
    public string Title  { get; } = title;

    private bool _isActive;
    /// <summary>La sección que se está viendo (se resalta en el índice).</summary>
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
}

/// <summary>Un atajo del lector tal y como está ahora (teclas ya en texto: "→", "Ctrl + W"…).</summary>
public record HelpShortcut(string Label, IReadOnlyList<string> Keys);

public record HelpQuestion(string Question, string Answer);

/// <summary>
/// Ayuda: guía "Cómo usar Hakufu". El texto está en la vista; aquí el índice,
/// los atajos actuales del lector y las preguntas frecuentes.
/// </summary>
public class HelpViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public HelpViewModel(INavigationService nav, ReaderSettings reader)
    {
        _nav = nav;
        Sections[0].IsActive = true;
        Shortcuts = ShortcutService.All
            .Select(a => new HelpShortcut(a.Label,
                ShortcutService.GetGestures(reader, a.Id).Select(ShortcutService.Display).ToList()))
            .ToList();
    }

    public IReadOnlyList<HelpSection> Sections { get; } =
    [
        new("SecStart",    "01", "Empezar"),
        new("SecLibrary",  "02", "Biblioteca y colecciones"),
        new("SecReader",   "03", "Leer"),
        new("SecHome",     "04", "Inicio"),
        new("SecProfile",  "05", "Tu perfil"),
        new("SecSettings", "06", "Ajustes"),
        new("SecBackup",   "07", "Copia de seguridad"),
        new("SecFaq",      "08", "Preguntas frecuentes"),
    ];

    /// <summary>Marca en el índice la sección que se está viendo.</summary>
    public void SetActiveSection(string id)
    {
        foreach (var s in Sections) s.IsActive = s.Id == id;
    }

    public IReadOnlyList<HelpShortcut> Shortcuts { get; }

    public IReadOnlyList<HelpQuestion> Questions { get; } =
    [
        new("¿Hakufu copia mis mangas?",
            "No. Lee los archivos directamente de la carpeta de la biblioteca. Si mueves o borras un archivo " +
            "ahí, Hakufu lo verá la próxima vez que actualices la biblioteca."),
        new("¿Necesita internet?",
            "No. Hakufu funciona entero sin conexión: no hay cuentas, ni servidores, ni nada que se envíe a ningún sitio."),
        new("¿Dónde se guardan mis datos?",
            "En tu ordenador, en la carpeta de datos de Hakufu: el progreso de lectura, los favoritos, el orden, " +
            "las portadas y tu perfil. Puedes abrirla desde Copia de seguridad → Abrir carpeta."),
        new("¿Qué formatos lee?",
            "PDF, CBZ y CBR. Las páginas de un CBZ o CBR se ordenan por el nombre de cada imagen."),
        new("He añadido tomos y no salen",
            "Comprueba que están dentro de una subcarpeta de la biblioteca (cada subcarpeta es una colección) " +
            "y pulsa «Actualizar» en Biblioteca."),
        new("¿Puedo cambiar las teclas del lector?",
            "Sí, en Ajustes → Atajos de teclado. Cada acción admite hasta dos teclas, también combinaciones como Ctrl + W."),
        new("¿Cómo me llevo todo a otro ordenador?",
            "Exporta una copia de seguridad y llévatela junto con tu carpeta de mangas (la copia no incluye los " +
            "archivos). En el otro equipo, elige la carpeta de la biblioteca en Ajustes e importa la copia. " +
            "Funciona entre Windows, macOS y Linux."),
        new("¿Dónde están los términos y el aviso legal?",
            "En Ajustes → Acerca de → «Términos y aviso legal»."),
    ];

    public RelayCommand GoBackCommand     => new(() => _nav.NavigateTo<HomeViewModel>());
    public RelayCommand OpenLibraryCommand  => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand OpenSettingsCommand => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand OpenBackupCommand   => new(() => _nav.NavigateTo<BackupViewModel>());
}
