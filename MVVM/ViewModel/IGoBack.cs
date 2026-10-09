namespace Hakufu.MVVM.ViewModel;

/// <summary>Pantalla con flecha «←»: el botón de atrás de Android hace lo mismo.</summary>
public interface IGoBack
{
    RelayCommand GoBackCommand { get; }
}
