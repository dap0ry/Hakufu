namespace Hakufu.MVVM.ViewModel;

/// <summary>Una colección en la lista de "qué exportar" de Copia de seguridad.</summary>
public class BackupCollectionOption(Guid id, string name, int volumes) : BaseViewModel
{
    public Guid   Id     { get; } = id;
    public string Name   { get; } = name;
    public string Detail { get; } = BackupViewModel.Volumes(volumes);

    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}
