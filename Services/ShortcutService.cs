using Avalonia.Input;
using Hakufu.I18n;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

/// <summary>
/// Una acción del lector que se puede lanzar con el teclado. Id es lo que se guarda en data.json;
/// el nombre visible sale de LabelKey en el idioma actual.
/// </summary>
public sealed record ShortcutAction(string Id, string LabelKey, params string[] Defaults)
{
    public string Label => L.Get(LabelKey);
}

/// <summary>
/// Atajos del lector: los de fábrica y los que el usuario cambia en Ajustes
/// (ReaderSettings.Shortcuts guarda solo las acciones modificadas). Las teclas
/// se guardan como KeyGesture en texto ("Right", "Ctrl+W"…).
/// </summary>
public static class ShortcutService
{
    public const int MaxKeysPerAction = 2;

    public static IReadOnlyList<ShortcutAction> All { get; } =
    [
        new("next",        "shortcuts.action.next",         "Right", "Space"),
        new("prev",        "shortcuts.action.prev",         "Left"),
        new("toggleZen",   "shortcuts.action.toggle_zen",   "F", "F11"),
        new("exitZen",     "shortcuts.action.exit_zen",     "Escape"),
        new("singlePage",  "shortcuts.action.single_page",  "D1"),
        new("twoPages",    "shortcuts.action.two_pages",    "D2"),
        new("closeReader", "shortcuts.action.close_reader", "Ctrl+W"),
    ];

    public static IReadOnlyList<KeyGesture> GetGestures(ReaderSettings settings, string actionId)
    {
        var keys = settings.Shortcuts.TryGetValue(actionId, out var custom)
            ? custom
            : All.First(a => a.Id == actionId).Defaults.ToList();
        return keys.Select(TryParse).OfType<KeyGesture>().ToList();
    }

    /// <summary>Asigna las teclas de una acción (se guarda aunque coincida con las de fábrica).</summary>
    public static void SetGestures(ReaderSettings settings, string actionId, IEnumerable<KeyGesture> gestures)
        => settings.Shortcuts[actionId] = gestures.Take(MaxKeysPerAction).Select(g => g.ToString()).ToList();

    public static void ResetAll(ReaderSettings settings) => settings.Shortcuts.Clear();

    /// <summary>La acción que dispara esta tecla, o null.</summary>
    public static string? Match(ReaderSettings settings, KeyEventArgs e)
        => All.FirstOrDefault(a => GetGestures(settings, a.Id).Any(g => g.Matches(e)))?.Id;

    public static KeyGesture? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return KeyGesture.Parse(text); } catch { return null; }
    }

    /// <summary>Las teclas que solo modifican (Ctrl, Mayús…) no valen como atajo por sí solas.</summary>
    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;

    /// <summary>Cómo se enseña una tecla en pantalla ("→", "Espacio", "Ctrl + W"…), en el idioma actual.</summary>
    public static string Display(KeyGesture g)
    {
        var parts = new List<string>();
        if (g.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Meta))    parts.Add(OperatingSystem.IsMacOS() ? "⌘" : "Win");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Alt))     parts.Add(OperatingSystem.IsMacOS() ? "⌥" : "Alt");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Shift))   parts.Add(L.Get("shortcuts.key.shift"));
        parts.Add(g.Key switch
        {
            Key.Right    => "→",
            Key.Left     => "←",
            Key.Up       => "↑",
            Key.Down     => "↓",
            Key.Space    => L.Get("shortcuts.key.space"),
            Key.Escape   => "Esc",
            Key.Enter    => L.Get("shortcuts.key.enter"),
            Key.Back     => L.Get("shortcuts.key.backspace"),
            Key.Tab      => "Tab",
            Key.PageUp   => L.Get("shortcuts.key.page_up"),
            Key.PageDown => L.Get("shortcuts.key.page_down"),
            Key.Home     => L.Get("shortcuts.key.home"),
            Key.End      => L.Get("shortcuts.key.end"),
            Key.Delete   => L.Get("shortcuts.key.delete"),
            >= Key.D0 and <= Key.D9 => ((int)(g.Key - Key.D0)).ToString(L.Culture),
            >= Key.NumPad0 and <= Key.NumPad9 => L.Format("shortcuts.key.numpad", (int)(g.Key - Key.NumPad0)),
            Key.OemComma  => ",",
            Key.OemPeriod => ".",
            Key.OemMinus  => "-",
            Key.OemPlus   => "+",
            _ => g.Key.ToString(),
        });
        return string.Join(" + ", parts);
    }
}
