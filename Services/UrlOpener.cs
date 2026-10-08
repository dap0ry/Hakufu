using System.Diagnostics;

namespace Hakufu.Services;

/// <summary>Abre una página https en el navegador del sistema.</summary>
public static class UrlOpener
{
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        if (AppPlatform.OpenUrl is { } open) { open(url); return; } // iOS: Safari
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sin navegador: no pasa nada */ }
    }
}
