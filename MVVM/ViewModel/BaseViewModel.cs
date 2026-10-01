using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hakufu.MVVM.ViewModel;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        // WPF reevaluaba CanExecute solo con CommandManager; aquí se hace
        // cada vez que cambia el estado de un ViewModel.
        CommandRequery.RequestInvalidate();
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
