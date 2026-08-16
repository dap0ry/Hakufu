using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

// Un "hueco" de personalización elegible desde Ajustes: fondo del panel
// izquierdo, el wallpaper general, o el icono/fondo de un botón de
// navegación concreto. onPathChanged/onOpacityChanged son quienes saben en
// qué campo/diccionario de HomeCustomization escribir — este ViewModel no
// conoce esa forma, solo gestiona elegir/quitar imagen y ajustar su opacidad.
public class ImageSlotViewModel : BaseViewModel
{
    private readonly string _slotKey;
    private readonly ICustomizationService _customization;
    private readonly IFilePickerService    _filePicker;
    private readonly IDataRepository       _repo;
    private readonly Action<string?> _onPathChanged;
    private readonly Action<double>  _onOpacityChanged;

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

    private double _opacity;
    public double Opacity
    {
        get => _opacity;
        set
        {
            if (!SetProperty(ref _opacity, value)) return;
            _onOpacityChanged(value);
            _ = _repo.SaveAsync();
        }
    }

    public ImageSlotViewModel(string slotKey, string label, CustomizationImage? initial,
                              ICustomizationService customization, IFilePickerService filePicker,
                              IDataRepository repo, Action<string?> onPathChanged, Action<double> onOpacityChanged,
                              double defaultOpacity = 1.0)
    {
        _slotKey          = slotKey;
        Label             = label;
        _imagePath        = initial?.Path;
        _opacity          = initial?.Opacity ?? defaultOpacity;
        _customization    = customization;
        _filePicker       = filePicker;
        _repo             = repo;
        _onPathChanged    = onPathChanged;
        _onOpacityChanged = onOpacityChanged;
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
        _onPathChanged(saved);
        _ = _repo.SaveAsync();
    });

    public RelayCommand RemoveCommand => new(() =>
    {
        _customization.RemoveImage(_slotKey);
        ImagePath = null;
        _onPathChanged(null);
        _ = _repo.SaveAsync();
    }, () => HasImage);
}
