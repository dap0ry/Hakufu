using Hakufu.MVVM.ViewModel;

namespace Hakufu.Services;

/// <summary>
/// Abre una pantalla nada más arrancar según HAKUFU_START_SCREEN (library, collection,
/// reader, pdf —el primer tomo en PDF—, settings, profile). Solo lo usa el CI para capturar cada pantalla en el
/// simulador de iOS, donde no hay forma de tocar la app; sin la variable, Inicio.
/// </summary>
public static class StartScreen
{
    public static void Apply(CompositionRoot root)
        => Apply(root, Environment.GetEnvironmentVariable("HAKUFU_START_SCREEN"));

    public static void Apply(CompositionRoot root, string? screen)
    {
        var nav = root.Navigation;
        // La primera colección por nombre y su primer tomo (los de la biblioteca de ejemplo).
        var collection = root.Library.GetCollections().OrderBy(c => c.Name, NaturalComparer.Instance).FirstOrDefault();
        var manga = collection is null ? null : root.Library.GetMangasInCollectionSorted(collection.Id).FirstOrDefault();

        switch (screen)
        {
            case "library":
                nav.NavigateTo<LibraryViewModel>();
                break;
            case "collection" when collection is not null:
                nav.NavigateTo<CollectionDetailViewModel>(collection.Id);
                break;
            case "reader" when manga is not null:
                nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(manga, 0));
                break;
            case "pdf" when root.Library.GetAllMangas()
                                .OrderBy(m => m.RelativePath, NaturalComparer.Instance)
                                .FirstOrDefault(m => m.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) is { } pdf:
                nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(pdf, 0));
                break;
            case "settings":
                nav.NavigateTo<SettingsViewModel>();
                break;
            case "profile":
                nav.NavigateTo<ProfileViewModel>();
                break;
        }
    }
}
