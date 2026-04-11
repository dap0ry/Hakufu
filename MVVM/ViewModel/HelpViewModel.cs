using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public record ShortcutEntry(string Keys, string Description);

public class HelpViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public HelpViewModel(INavigationService nav) => _nav = nav;

    public IReadOnlyList<ShortcutEntry> Shortcuts { get; } =
    [
        new("← / →",          "Página anterior / siguiente"),
        new("Espacio",         "Siguiente página"),
        new("F / F11",         "Activar / salir modo zen"),
        new("ESC",             "Salir modo zen"),
        new("1",               "Modo una página"),
        new("2",               "Modo dos páginas"),
        new("Ctrl + W",        "Cerrar lector"),
    ];

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());
}
