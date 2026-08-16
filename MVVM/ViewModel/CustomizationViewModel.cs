using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

// Página dedicada a la personalización local (imágenes 100% en este
// dispositivo, nunca se suben a ningún sitio) — antes vivía como una
// sección más dentro de Ajustes, ahora tiene su propia pantalla con
// todos los huecos en una cuadrícula de 3 columnas.
public class CustomizationViewModel : BaseViewModel
{
    private readonly IDataRepository    _repo;
    private readonly INavigationService _nav;

    public CustomizationViewModel(IDataRepository repo, INavigationService nav,
                                   ICustomizationService customization,
                                   IFilePickerService filePicker,
                                   IWallpaperService wallpaper)
    {
        _repo = repo;
        _nav  = nav;

        CustomizationSlots = BuildCustomizationSlots(customization, filePicker, wallpaper);
    }

    public List<ImageSlotViewModel> CustomizationSlots { get; }

    private List<ImageSlotViewModel> BuildCustomizationSlots(
        ICustomizationService customization, IFilePickerService filePicker, IWallpaperService wallpaper)
    {
        var c = _repo.Current.Customization;

        ImageSlotViewModel IconSlot(string key, string label) => new(
            $"nav.{key}.icon", label, c.NavIcons.GetValueOrDefault(key),
            customization, filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.NavIcons.Remove(key); return; }
                var img = c.NavIcons.TryGetValue(key, out var cur) ? cur : new CustomizationImage();
                img.Path = path;
                c.NavIcons[key] = img;
            },
            onOpacityChanged: op =>
            {
                if (c.NavIcons.TryGetValue(key, out var img)) img.Opacity = op;
            },
            defaultOpacity: 1.0);

        ImageSlotViewModel BackgroundSlot(string key, string label) => new(
            $"nav.{key}.background", label, c.NavBackgrounds.GetValueOrDefault(key),
            customization, filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.NavBackgrounds.Remove(key); return; }
                var img = c.NavBackgrounds.TryGetValue(key, out var cur) ? cur : new CustomizationImage();
                img.Path = path;
                c.NavBackgrounds[key] = img;
            },
            onOpacityChanged: op =>
            {
                if (c.NavBackgrounds.TryGetValue(key, out var img)) img.Opacity = op;
            },
            defaultOpacity: 0.3);

        var panelSlot = new ImageSlotViewModel("panel.left", "Fondo del panel izquierdo", c.LeftPanelBackground,
            customization, filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.LeftPanelBackground = null; return; }
                c.LeftPanelBackground ??= new CustomizationImage();
                c.LeftPanelBackground.Path = path;
            },
            onOpacityChanged: op =>
            {
                if (c.LeftPanelBackground is not null) c.LeftPanelBackground.Opacity = op;
            },
            defaultOpacity: 0.3);

        // El wallpaper general, a diferencia de los demás huecos, también
        // tiene que aplicarse en el momento (WallpaperService sustituye el
        // recurso AppBackground del que cuelga casi toda la UI) — si no,
        // habría que salir de esta pantalla y volver a entrar para verlo.
        var wallpaperSlot = new ImageSlotViewModel("wallpaper.general", "Wallpaper general (fondo de toda la app)",
            c.GeneralWallpaper, customization, filePicker, _repo,
            onPathChanged: path =>
            {
                if (path is null) { c.GeneralWallpaper = null; }
                else
                {
                    c.GeneralWallpaper ??= new CustomizationImage();
                    c.GeneralWallpaper.Path = path;
                }
                wallpaper.Apply(c.GeneralWallpaper?.Path, c.GeneralWallpaper?.Opacity ?? 0.3);
            },
            onOpacityChanged: op =>
            {
                if (c.GeneralWallpaper is not null) c.GeneralWallpaper.Opacity = op;
                wallpaper.Apply(c.GeneralWallpaper?.Path, op);
            },
            defaultOpacity: 0.3);

        return
        [
            wallpaperSlot,
            panelSlot,

            IconSlot("library", "Icono — Biblioteca"),  BackgroundSlot("library", "Fondo — Biblioteca"),
            IconSlot("profile", "Icono — Perfil"),      BackgroundSlot("profile", "Fondo — Perfil"),
            IconSlot("friends", "Icono — Amigos"),      BackgroundSlot("friends", "Fondo — Amigos"),
            IconSlot("settings", "Icono — Ajustes"),    BackgroundSlot("settings", "Fondo — Ajustes"),
            IconSlot("help", "Icono — Ayuda"),          BackgroundSlot("help", "Fondo — Ayuda"),
            IconSlot("account", "Icono — Cuenta"),      BackgroundSlot("account", "Fondo — Cuenta"),
        ];
    }

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<SettingsViewModel>());
}
