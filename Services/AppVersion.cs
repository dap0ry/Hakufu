using System.Reflection;

namespace Hakufu.Services;

/// <summary>Versión de la app y comparación de etiquetas de release ("v0.11.0", "v0.11.0-beta.1").</summary>
public static class AppVersion
{
    /// <summary>La &lt;Version&gt; del csproj, con su "-beta.N" si lo tiene (sin el "+commit" que añade el SDK).</summary>
    public static string Current { get; } = ReadCurrent();

    /// <summary>¿Es <paramref name="candidate"/> más nueva que <paramref name="current"/>? Nunca lanza.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        if (!TryParse(candidate, out var c, out var cPre) || !TryParse(current, out var v, out var vPre))
            return false;
        var cmp = c.CompareTo(v);
        if (cmp != 0) return cmp > 0;
        // Misma base: la estable gana a la beta; entre betas, la de número mayor.
        if (cPre is null) return vPre is not null;
        if (vPre is null) return false;
        return string.CompareOrdinal(cPre, vPre) > 0;
    }

    private static bool TryParse(string s, out Version version, out string? pre)
    {
        s = s.Trim().TrimStart('v', 'V');
        var dash = s.IndexOf('-');
        pre = dash >= 0 ? s[(dash + 1)..] : null;
        return Version.TryParse(dash >= 0 ? s[..dash] : s, out version!);
    }

    private static string ReadCurrent()
    {
        var asm = typeof(AppVersion).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info)) return info.Split('+')[0];
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
