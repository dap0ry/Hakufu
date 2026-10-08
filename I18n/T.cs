using Avalonia;
using Avalonia.Markup.Xaml;

namespace Hakufu.I18n;

/// <summary>
/// Texto traducido en XAML: Text="{i18n:T settings.title}" (xmlns:i18n="using:Hakufu.I18n").
/// Se actualiza solo al cambiar de idioma.
/// </summary>
public sealed class T : MarkupExtension
{
    public T() { }
    public T(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
        => Localizer.Instance.Observe(Key).ToBinding();
}
