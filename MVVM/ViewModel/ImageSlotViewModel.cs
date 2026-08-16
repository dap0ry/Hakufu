using Hakufu.Data;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

// Un "hueco" de personalización elegible desde Ajustes: fondo del panel
// izquierdo, o el icono/fondo de un botón de navegación concreto. onChanged
// es quien sabe en qué campo/diccionario de HomeCustomization escribir —
// este ViewModel no conoce esa forma, solo gestiona elegir/quitar imagen.
public class ImageSlotViewModel : BaseViewModel
{
    private readonly string _slotKey;
    private readonly ICustomizationService _customization;
    private readonly IFilePickerService    _filePicker;
    private readonly IDataRepository       _repo;
    private readonly Action<string?>       _onChanged;

    public string Label { get; }

    private string? _imagePath;
    public string? ImagePath
    {
        get => _imagePath;
        private set
        {
            if (!SetProperty(ref _imagePath, value)) return;
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    public ImageSlotViewModel(string slotKey, string label, string? initialPath,
                              ICustomizationService customization, IFilePickerService filePicker,
                              IDataRepository repo, Action<string?> onChanged)
    {
        _slotKey       = slotKey;
        Label          = label;
        _imagePath     = initialPath;
        _customization = customization;
        _filePicker    = filePicker;
        _repo          = repo;
        _onChanged     = onChanged;
    }

    public RelayCommand PickCommand => new(() =>
    {
        var files = _filePicker.PickFiles(
            "Elegir imagen",
            "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            multiSelect: false);
        if (files.Length == 0) return;

        var saved = _customization.SaveImage(files[0], _slotKey);
        ImagePath = saved;
        _onChanged(saved);
        _ = _repo.SaveAsync();
    });

    public RelayCommand RemoveCommand => new(() =>
    {
        _customization.RemoveImage(_slotKey);
        ImagePath = null;
        _onChanged(null);
        _ = _repo.SaveAsync();
    }, () => HasImage);
}
