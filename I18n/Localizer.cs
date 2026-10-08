using System.Globalization;
using Avalonia.Platform;
using System.Text.Json;

namespace Hakufu.I18n;

/// <summary>
/// Textos de la app en español o inglés. Cada área tiene sus archivos
/// Assets/i18n/&lt;area&gt;.es.json y .en.json (clave → texto, AvaloniaResource). Las vistas los usan con
/// {i18n:T clave}; el código, con L.Get / L.Format. Cambiar de idioma avisa a todo
/// lo que esté observando una clave, así que el cambio se ve al momento.
/// </summary>
public sealed class Localizer
{
    public static Localizer Instance { get; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _byLanguage = [];
    private readonly List<KeyObserver> _observers = [];
    private Dictionary<string, string> _current = [];
    private Dictionary<string, string> _fallback = [];

    private Localizer()
    {
        _fallback = Load("es");
        SetLanguage("es");
    }

    public string Language { get; private set; } = "es";
    public CultureInfo Culture { get; private set; } = new("es-ES");

    public event Action? LanguageChanged;

    /// <summary>El texto de la clave en el idioma actual (si falta: español; si tampoco: la clave).</summary>
    public string Get(string key)
        => _current.TryGetValue(key, out var v) ? v
         : _fallback.TryGetValue(key, out var f) ? f
         : key;

    public string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    /// <summary>Emite el texto actual al suscribirse y el nuevo en cada cambio de idioma.</summary>
    public IObservable<string> Observe(string key) => new KeyObservable(this, key);

    /// <summary>"es" o "en" (cualquier otro valor se trata como inglés).</summary>
    public void SetLanguage(string lang)
    {
        lang = lang == "es" ? "es" : "en";
        if (_fallback.Count == 0) _fallback = Load("es");
        _current = Load(lang);
        Language = lang;
        Culture = new CultureInfo(lang == "es" ? "es-ES" : "en-US");
        foreach (var o in _observers.ToArray()) o.Push();
        LanguageChanged?.Invoke();
    }

    /// <summary>Idioma al arrancar: el guardado; si no hay, español para quien ya usaba Hakufu y el del sistema para los nuevos.</summary>
    public static string ResolveInitial(string saved, bool hasExistingData, CultureInfo system)
    {
        if (saved is "es" or "en") return saved;
        if (hasExistingData) return "es";
        return system.TwoLetterISOLanguageName == "es" ? "es" : "en";
    }

    private Dictionary<string, string> Load(string lang)
    {
        if (_byLanguage.TryGetValue(lang, out var cached)) return cached;
        var all = new Dictionary<string, string>();
        IEnumerable<Uri> files;
        try { files = AssetLoader.GetAssets(new Uri("avares://Hakufu/Assets/i18n/"), null); }
        catch { files = []; }
        foreach (var uri in files.Where(u => u.AbsolutePath.EndsWith($".{lang}.json", StringComparison.Ordinal)))
        {
            try
            {
                using var s = AssetLoader.Open(uri);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(s) ?? [];
                foreach (var (k, v) in map) all[k] = v;
            }
            catch (JsonException) { /* un archivo roto no tumba la app: esas claves caen al español */ }
        }
        // Si Avalonia aún no podía leer recursos, no se guarda la carga vacía: se reintenta.
        if (all.Count > 0) _byLanguage[lang] = all;
        return all;
    }

    private sealed class KeyObservable(Localizer owner, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            var o = new KeyObserver(owner, key, observer);
            owner._observers.Add(o);
            o.Push();
            return o;
        }
    }

    private sealed class KeyObserver(Localizer owner, string key, IObserver<string> target) : IDisposable
    {
        public void Push() => target.OnNext(owner.Get(key));
        public void Dispose() => owner._observers.Remove(this);
    }
}

/// <summary>Atajos para el código: L.Get("clave"), L.Format("clave", n), L.Culture.</summary>
public static class L
{
    public static string Get(string key) => Localizer.Instance.Get(key);
    public static string Format(string key, params object?[] args) => Localizer.Instance.Format(key, args);
    public static CultureInfo Culture => Localizer.Instance.Culture;
}
