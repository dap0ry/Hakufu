using Hakufu.I18n;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public record LegalSection(string Title, string Body);

/// <summary>Términos, condiciones y aviso legal (Ajustes → Acerca de).</summary>
public class LegalViewModel : BaseViewModel, IGoBack
{
    private readonly INavigationService _nav;

    public LegalViewModel(INavigationService nav) => _nav = nav;

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<SettingsViewModel>());

    // ── Términos, condiciones y aviso legal ─────────────────────────────────
    // Aviso: este texto es un modelo genérico redactado con fines
    // informativos y de protección razonable del desarrollador; no sustituye
    // el asesoramiento de un abogado. Se recomienda revisión profesional
    // antes de usarlo como base legal definitiva en caso de disputa real.
    // El texto está en Assets/i18n/legal.<idioma>.json (legal.s1.title, legal.s1.body…).
    public const int SectionCount = 13;

    public IReadOnlyList<LegalSection> LegalSections { get; } =
        Enumerable.Range(1, SectionCount)
                  .Select(i => new LegalSection(L.Get($"legal.s{i}.title"), L.Get($"legal.s{i}.body")))
                  .ToList();
}
