using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Modal "Editar perfil": foto, nombre y manga favorito. Nada se guarda hasta pulsar Guardar.</summary>
public class EditProfileViewModel : BaseViewModel
{
    private readonly ProfileService     _profile;
    private readonly IDialogService     _dialog;
    private readonly IFilePickerService _files;
    private readonly Action             _onSaved;

    private string  _name;
    private Bitmap? _avatar;
    private string? _newAvatarSource;
    private bool    _removeAvatar;
    private FavoriteOption? _favorite;

    public EditProfileViewModel(ProfileService profile, LibraryService library, IDialogService dialog,
                                IFilePickerService files, Action onSaved)
    {
        _profile = profile;
        _dialog  = dialog;
        _files   = files;
        _onSaved = onSaved;

        var p = profile.GetProfile();
        _name   = p.Name;
        _avatar = BitmapHelper.TryLoad(p.AvatarPath);

        FavoriteOptions = [new FavoriteOption(null, "Ninguno"),
                           .. library.GetAllMangas().OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
                                     .Select(m => new FavoriteOption(m.Id, m.Title))];
        _favorite = FavoriteOptions.FirstOrDefault(o => o.Id == p.FavoriteMangaId) ?? FavoriteOptions[0];
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public Bitmap? Avatar
    {
        get => _avatar;
        private set { SetProperty(ref _avatar, value); OnPropertyChanged(nameof(HasAvatar)); }
    }
    public bool HasAvatar => Avatar is not null;

    public IReadOnlyList<FavoriteOption> FavoriteOptions { get; }
    public FavoriteOption? Favorite
    {
        get => _favorite;
        set => SetProperty(ref _favorite, value);
    }

    public RelayCommand ChooseAvatarCommand => new(async () =>
    {
        var picked = await _files.PickFilesAsync("Elige tu foto de perfil", FileFilter.Images, multiSelect: false);
        if (picked.Length == 0) return;
        var bmp = BitmapHelper.TryLoad(picked[0]);
        if (bmp is null) return;
        _newAvatarSource = picked[0];
        _removeAvatar    = false;
        Avatar = bmp;
    });

    public RelayCommand RemoveAvatarCommand => new(() =>
    {
        _newAvatarSource = null;
        _removeAvatar    = true;
        Avatar = null;
    });

    public RelayCommand SaveCommand => new(async () =>
    {
        await _profile.UpdateProfileAsync(Name, Favorite?.Id, _newAvatarSource, _removeAvatar);
        _dialog.CloseModal();
        _onSaved();
    });

    public RelayCommand CancelCommand => new(() => _dialog.CloseModal());
}

public record FavoriteOption(Guid? Id, string Title)
{
    public override string ToString() => Title;
}
