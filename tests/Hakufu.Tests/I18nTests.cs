using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hakufu.I18n;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Raíz del repo (para leer .axaml y .json desde los tests).</summary>
internal static class Repo
{
    public static string Root { get; } = Find();
    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hakufu.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No encuentro Hakufu.csproj");
    }
}

public class LocalizerTests : IDisposable
{
    public void Dispose() => Localizer.Instance.SetLanguage("es");

    [Theory]
    [InlineData("en", false, "es-ES", "en")]
    [InlineData("",   true,  "en-US", "es")]   // instalación con datos: sigue en español
    [InlineData("",   false, "es-MX", "es")]
    [InlineData("",   false, "fr-FR", "en")]
    public void Initial_language(string saved, bool hasData, string system, string expected)
        => Assert.Equal(expected, Localizer.ResolveInitial(saved, hasData, new CultureInfo(system)));

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Gets_and_formats_in_both_languages()
    {
        Localizer.Instance.SetLanguage("es");
        Assert.Equal("← Inicio", L.Get("common.back_home"));
        Localizer.Instance.SetLanguage("en");
        Assert.Equal("← Home", L.Get("common.back_home"));
        Assert.Equal("en-US", L.Culture.Name);
        Assert.Equal("Version 1.2", L.Format("common.version", "1.2"));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Missing_key_shows_the_key()
        => Assert.Equal("nope.missing", L.Get("nope.missing"));

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Observe_emits_current_and_on_change()
    {
        Localizer.Instance.SetLanguage("es");
        var seen = new List<string>();
        using var _ = Localizer.Instance.Observe("common.back_home").Subscribe(new Obs(seen.Add));
        Localizer.Instance.SetLanguage("en");
        Assert.Equal(["← Inicio", "← Home"], seen);
    }

    private sealed class Obs(Action<string> next) : IObserver<string>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(string value) => next(value);
    }
}

public class I18nFilesTests
{
    private static Dictionary<string, string> Read(string path)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;

    public static IEnumerable<object[]> Areas() =>
        Directory.GetFiles(Path.Combine(Repo.Root, "Assets", "i18n"), "*.es.json")
                 .Select(f => new object[] { Path.GetFileName(f)[..^".es.json".Length] });

    [Theory]
    [MemberData(nameof(Areas))]
    public void Spanish_and_English_have_the_same_keys_and_none_is_empty(string area)
    {
        var dir = Path.Combine(Repo.Root, "Assets", "i18n");
        var es = Read(Path.Combine(dir, $"{area}.es.json"));
        var en = Read(Path.Combine(dir, $"{area}.en.json"));
        Assert.Empty(es.Keys.Except(en.Keys));
        Assert.Empty(en.Keys.Except(es.Keys));
        Assert.DoesNotContain(es.Concat(en), kv => string.IsNullOrWhiteSpace(kv.Value));
        Assert.All(es.Keys, k => Assert.StartsWith(area + ".", k));
    }

    // Una vista entra en la comprobación en cuanto declara xmlns:i18n y no lleva la marca
    // "i18n: pendiente" (la quita quien termina de traducirla).
    public static IEnumerable<string> TranslatedViews() =>
        Directory.GetFiles(Repo.Root, "*.axaml", SearchOption.AllDirectories)
                 .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                             !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                             !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}") &&
                             !f.Contains($"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}") &&
                             File.ReadAllText(f) is var x && x.Contains("xmlns:i18n=") && !x.Contains("i18n: pendiente"))
                 .Select(f => Path.GetRelativePath(Repo.Root, f));

    private static readonly Regex VisibleAttr = new(
        @"\s(Text|Content|ToolTip\.Tip|Watermark|PlaceholderText|BackText|Title|Header)=""([^""]*)""");
    private static readonly HashSet<string> Allowed = ["Hakufu", "HAKUFU", "H", "Aa", "Español", "English"];

    [Fact]
    public void Translated_views_have_no_hand_written_text()
    {
        var offenders = TranslatedViews()
            .SelectMany(view => VisibleAttr.Matches(File.ReadAllText(Path.Combine(Repo.Root, view)))
                .Select(m => m.Groups[2].Value)
                .Where(v => !v.StartsWith('{') && v.Any(char.IsLetter) && !Allowed.Contains(v))
                .Select(v => $"{view}: {v}"))
            .ToList();
        Assert.Empty(offenders);
    }
}

public class LanguageSettingTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Choosing_English_in_settings_switches_saves_and_refreshes_the_update_bar()
    {
        try
        {
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<SettingsViewModel>();
            var vm = (SettingsViewModel)app.Root.Navigation.CurrentViewModel!;
            var changed = new List<string?>();
            app.Root.UpdateBanner.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            vm.EnglishCommand.Execute(null);
            app.Pump();

            Assert.Equal("en", Localizer.Instance.Language);
            Assert.Equal("en", app.Root.Repo.Current.Language);
            Assert.Contains(nameof(UpdateBannerViewModel.Message), changed);
            // La pantalla se recarga (VM nuevo) y sigue en Ajustes.
            var after = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);
            Assert.NotSame(vm, after);
            Assert.True(after.IsEnglish);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }
}
