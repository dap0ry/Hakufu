using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Una entrada del índice de la guía. Id = nombre del control de la sección en la vista.</summary>
public class HelpSection(string id, string number, string title) : BaseViewModel
{
    public string Id     { get; } = id;
    public string Number { get; } = number;
    public string Title  { get; } = title;

    private bool _isActive;
    /// <summary>La sección que se está viendo (se resalta en el índice).</summary>
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
}

/// <summary>Un atajo del lector tal y como está ahora (teclas ya en texto: "→", "Ctrl + W"…).</summary>
public record HelpShortcut(string Label, IReadOnlyList<string> Keys);

public record HelpQuestion(string Question, string Answer);

/// <summary>
/// Ayuda: guía "Cómo usar Hakufu". El texto está en la vista; aquí el índice,
/// los atajos actuales del lector y las preguntas frecuentes.
/// </summary>
public class HelpViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public HelpViewModel(INavigationService nav, ReaderSettings reader)
    {
        _nav = nav;
        Sections[0].IsActive = true;
        Shortcuts = ShortcutService.All
            .Select(a => new HelpShortcut(a.Label,
                ShortcutService.GetGestures(reader, a.Id).Select(ShortcutService.Display).ToList()))
            .ToList();
    }

    public IReadOnlyList<HelpSection> Sections { get; } =
    [
        new("SecStart",    "01", L.Get("help.s1.title")),
        new("SecLibrary",  "02", L.Get("help.s2.title")),
        new("SecReader",   "03", L.Get("help.s3.title")),
        new("SecHome",     "04", L.Get("help.s4.title")),
        new("SecProfile",  "05", L.Get("help.s5.title")),
        new("SecSettings", "06", L.Get("help.s6.title")),
        new("SecBackup",   "07", L.Get("help.s7.title")),
        new("SecFaq",      "08", L.Get("help.s8.title")),
    ];

    /// <summary>Marca en el índice la sección que se está viendo.</summary>
    public void SetActiveSection(string id)
    {
        foreach (var s in Sections) s.IsActive = s.Id == id;
    }

    public IReadOnlyList<HelpShortcut> Shortcuts { get; }

    /// <summary>Preguntas frecuentes: help.faq.q1/a1 … q8/a8 (se leen al crear el VM, que se rehace al cambiar de idioma).</summary>
    public IReadOnlyList<HelpQuestion> Questions { get; } =
        Enumerable.Range(1, 8).Select(i => new HelpQuestion(L.Get($"help.faq.q{i}"), L.Get($"help.faq.a{i}"))).ToList();

    public RelayCommand GoBackCommand     => new(() => _nav.NavigateTo<HomeViewModel>());
    public RelayCommand OpenLibraryCommand  => new(() => _nav.NavigateTo<LibraryViewModel>());
    public RelayCommand OpenSettingsCommand => new(() => _nav.NavigateTo<SettingsViewModel>());
    public RelayCommand OpenBackupCommand   => new(() => _nav.NavigateTo<BackupViewModel>());
}
