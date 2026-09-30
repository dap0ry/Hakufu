using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

/// <summary>
/// Hakufu.MVVM.ViewModel.XxxViewModel → Hakufu.MVVM.View.XxxView. Así cada
/// vista nueva funciona sin registrarla en App.axaml.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        var vmName   = data!.GetType().FullName!;
        var viewName = vmName.Replace(".ViewModel.", ".View.").Replace("ViewModel", "View");
        var type     = Type.GetType(viewName);

        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = $"Vista pendiente: {viewName}", Margin = new(24) };
    }

    public bool Match(object? data) => data is BaseViewModel;
}
