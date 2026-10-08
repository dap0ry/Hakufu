using System.Globalization;
using System.Text.Json;

namespace Hakufu.I18n;

/// <summary>
/// Textos de la app en español o inglés. Cada área tiene sus archivos
/// Assets/i18n/&lt;area&gt;.es.json y .en.json (clave → texto; se copian a &lt;app&gt;/i18n/ al compilar). Las vistas los usan con
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
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("es-ES");

    public event Action? LanguageChanged;

    /// <summary>Suscripciones vivas a textos (para los tests de memoria).</summary>
    internal int ObserverCount => _observers.Count;

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
        // GetCultureInfo: la cultura estándar (sin los retoques regionales del usuario), como antes.
        Culture = CultureInfo.GetCultureInfo(lang == "es" ? "es-ES" : "en-US");
        foreach (var o in _observers.ToArray()) o.Push();
        _observers.RemoveAll(o => !o.IsAlive);
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
        var dir = Path.Combine(AppContext.BaseDirectory, "i18n");
        string[] files;
        try { files = Directory.Exists(dir) ? Directory.GetFiles(dir, $"*.{lang}.json") : []; }
        catch { files = []; }
        foreach (var file in files)
        {
            try
            {
                using var s = File.OpenRead(file);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(s) ?? [];
                foreach (var (k, v) in map) if (v is not null) all[k] = v;
            }
            catch { /* un archivo roto o ilegible no tumba la app: esas claves caen al español */ }
        }
        if (all.Count > 0) _byLanguage[lang] = all;
        return all;
    }

    private sealed class KeyObservable(Localizer owner, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            owner._observers.RemoveAll(o => !o.IsAlive);
            var o = new KeyObserver(owner, key, observer);
            owner._observers.Add(o);
            o.Push();
            return o;
        }
    }

    // Referencia débil al observador: Avalonia no deshace los bindings de un control al
    // quitarlo de la pantalla, y una referencia fuerte desde este singleton mantendría
    // viva la vista entera para siempre. El control sí sujeta su binding mientras vive.
    private sealed class KeyObserver(Localizer owner, string key, IObserver<string> target) : IDisposable
    {
        private readonly WeakReference<IObserver<string>> _target = new(target);

        public bool IsAlive => _target.TryGetTarget(out _);

        public void Push()
        {
            if (_target.TryGetTarget(out var t)) t.OnNext(owner.Get(key));
        }

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
