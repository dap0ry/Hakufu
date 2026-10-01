using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

/// <summary>
/// Crea todos los servicios y la factoría de ViewModels (inyección manual).
/// La usa App al arrancar y también los tests de vistas, para que ambos
/// monten exactamente lo mismo.
/// </summary>
public sealed class CompositionRoot
{
    public IDataRepository       Repo          { get; }
    public ThemeService          Theme         { get; } = new();
    public DialogService         Dialog        { get; } = new();
    public FilePickerService     FilePicker    { get; } = new();
    public CoverService          Cover         { get; } = new();
    public LibraryService        Library       { get; }
    public ProfileService        Profile       { get; }
    public BackupService         Backup        { get; }
    public NavigationService     Navigation    { get; }

    public CompositionRoot(IDataRepository repo)
    {
        Repo    = repo;
        Library = new LibraryService(repo);
        Profile = new ProfileService(repo);
        Backup  = new BackupService(repo);
        Navigation = new NavigationService(Create);
    }

    /// <summary>Aplica el tema guardado. Llamar antes de crear ninguna vista.</summary>
    public void ApplySavedAppearance()
        => Theme.SetTheme(Repo.Current.ActiveTheme == "Dark" ? AppTheme.Dark : AppTheme.Light);

    /// <summary>ViewModel de la ventana principal; navega a Inicio al crearse.</summary>
    public MainWindowViewModel CreateMainViewModel() => new(Navigation, Dialog);

    private BaseViewModel Create(Type type, object? param) => type.Name switch
    {
        nameof(HomeViewModel) => new HomeViewModel(Library, Cover, Navigation, Repo),

        nameof(LibraryViewModel) => new LibraryViewModel(Library, Cover, Dialog, Navigation),

        nameof(CollectionDetailViewModel) when param is Guid id => new CollectionDetailViewModel(
            id, Library, Cover, Dialog, Navigation, FilePicker),

        nameof(ReaderViewModel) when param is ReaderNavigationParam p => new ReaderViewModel(
            p.Manga, p.StartPage, Library, Profile, Navigation, Repo.Current.Reader),

        nameof(ProfileViewModel) => new ProfileViewModel(Profile, Library, Cover, Dialog, Navigation, FilePicker),

        nameof(SettingsViewModel) => new SettingsViewModel(
            Theme, Repo, Navigation, Dialog, Library, FilePicker),

        nameof(HelpViewModel) => new HelpViewModel(Navigation),

        nameof(LegalViewModel) => new LegalViewModel(Navigation),

        nameof(BackupViewModel) => new BackupViewModel(
            Backup, FilePicker, Navigation, Repo, Theme),

        _ => throw new InvalidOperationException($"Unknown ViewModel: {type.Name}")
    };
}
