using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hakufu.MVVM.ViewModel;
using Microsoft.Web.WebView2.Core;

namespace Hakufu.MVVM.View;

public partial class StoreView : UserControl
{
    // ── Dominios de los sitios de manga → popups navegan dentro de la app ──
    private static readonly HashSet<string> MangaSiteDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "tomosmanga.com", "lexmangas.com", "mangaycomics.com",
    };

    // ── Páginas de entrada, probadas en orden: si una está caída se prueba la siguiente ──
    // Tomosmanga es la mejor de las tres y va primero; si en ese momento está caída
    // (Cloudflare 522, etc.) se cae automáticamente a las otras dos, tanto al abrir la
    // Tienda como al pulsar directamente su marcador de la barra de favoritos.
    private static readonly string[] EntryPages =
    {
        "https://tomosmanga.com/",
        "https://lexmangas.com/",
        "https://mangaycomics.com/",
    };
    private int _entryPageIndex;

    // ── Redes publicitarias y trackers bloqueados por dominio ──
    private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        // Google Ads
        "doubleclick.net", "googlesyndication.com", "pagead2.googlesyndication.com",
        "adservice.google.com", "google-analytics.com", "googletagmanager.com",
        // Redes de display
        "amazon-adsystem.com", "advertising.com", "ads.yahoo.com",
        "adnxs.com", "outbrain.com", "taboola.com", "criteo.com",
        "media.net", "adsterra.com", "exoclick.com", "hilltopads.net",
        "trafficjunky.net", "trafficfactory.biz", "trafficholder.com",
        "adcash.com", "clickadu.com", "plugrush.com", "yllix.com",
        "adxpansion.com", "cpmstar.com", "revcontent.com", "mgid.com",
        "triplelift.com", "sharethrough.com", "liveintent.com",
        // Pop-under / pop-up
        "popads.net", "popcash.net", "propellerads.com", "onclicka.com",
        "popunder.ru", "pop.imonomy.com", "adpop.net",
        // (acortadores de enlace eliminados — los usan los sitios manga para descargas)
        // Push notifications de redes publicitarias
        "pushwoosh.com", "push.express", "notix.io", "izooto.com",
        "pushassist.com", "webpushr.com", "pushcrew.com", "aimtell.com",
        // Minería de criptomonedas
        "coinhive.com", "cryptoloot.com", "coin-hive.com", "jsecoin.com",
        "moneybots.cloud", "webmine.pro", "crypto-loot.com",
        // Trackers y telemetría
        "moatads.com", "connect.facebook.net", "scorecardresearch.com",
        "quantserve.com", "adsymptotic.com", "rubiconproject.com",
        "openx.net", "pubmatic.com", "smartadserver.com", "yieldmanager.com",
        "2mdn.net", "adsafeprotected.com", "adsrvr.org", "adtechus.com",
        "adbrite.com", "adcolony.com", "admob.com", "adroll.com",
        "bidswitch.net", "bluekai.com", "casalemedia.com", "contextweb.com",
        "exelator.com", "eyeota.net", "hotjar.com", "imrworldwide.com",
        "loopme.com", "mediaplex.com", "mopub.com", "omtrdc.net",
        "sovrn.com", "spotxchange.com", "strikead.com", "valueclick.com",
        "verizonmedia.com", "vidoomy.com", "zedo.com", "sizmek.com",
        "addthis.com", "chartbeat.com", "clicktale.com", "comscore.com",
        "demdex.net", "doubleverify.com", "integral-api.com", "krux.com",
        "liveramp.com", "lotame.com", "mouseflow.com", "segment.com",
        "segment.io", "snowplow.io", "tealiumiq.com", "tradedoubler.com",
        "turn.com", "tynt.com", "yottaa.com", "histats.com",
        "statcounter.com", "etracker.com", "hitslink.com",
        // Redes de anuncios para adultos (muy comunes en sitios manga)
        "juicyads.com", "ero-advertising.com", "traffichaus.com",
        "adspyglass.com", "advertstream.com", "coinzilla.io",
    };

    // ── JS inyectado tras cada carga: limpia overlays y banners de anuncios ──
    private const string AntiPopupScript = """
        (function () {
            // Selectores de elementos publicitarios conocidos
            const AD_SELECTORS = [
                '.adsbygoogle', 'ins.adsbygoogle',
                '[class*="ad-banner"]',  '[class*="ads-banner"]',
                '[class*="ad-container"]', '[class*="ads-container"]',
                '[class*="ad-wrapper"]',   '[class*="ad-unit"]',
                '[id*="ad-container"]',    '[id*="adsense"]',
                '[class*="popup-overlay"]',   '[class*="ad-overlay"]',
                '[class*="overlay-ad"]',      '[class*="pop-overlay"]',
                '[class*="interstitial"]',    '[id*="interstitial"]',
                '[class*="popunder"]',        '[id*="popunder"]',
                '[class*="adblock-message"]', '[class*="adblocker-message"]',
                '[class*="cookie-consent"]',  '[id*="cookie-consent"]',
                '[class*="gdpr-banner"]',     '[id*="gdpr"]',
                '[class*="push-subscribe"]',  '[id*="push-subscribe"]',
                '[class*="notification-prompt"]',
            ].join(',');

            const clean = () => {
                try {
                    document.querySelectorAll(AD_SELECTORS).forEach(el => {
                        if (el !== document.body && el !== document.documentElement)
                            el.remove();
                    });
                    // Restaurar scroll si fue bloqueado por overlay
                    document.body.style.overflow = '';
                    document.body.style.position = '';
                    document.documentElement.style.overflow = '';
                } catch (_) {}
            };

            clean();
            // Observar cambios en el DOM para limpiar anuncios inyectados dinámicamente
            new MutationObserver(clean).observe(document.documentElement,
                { childList: true, subtree: true });
        })();
        """;

    private StoreViewModel? _vm;
    private bool _webViewReady;

    public StoreView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.NavigationRequested -= OnNavigationRequested;
        _vm = e.NewValue as StoreViewModel;
        if (_vm != null)
            _vm.NavigationRequested += OnNavigationRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewReady) return;

        try
        {
            await WebBrowser.EnsureCoreWebView2Async();
            _webViewReady = true;

            var core = WebBrowser.CoreWebView2;

            // ── Gestión de ventanas emergentes ──────────────────────────────
            // Política: bloqueamos solo lo que sabemos que es publicidad/tracking
            // (BlockedDomains). Todo lo demás que no sea el propio sitio de manga
            // se trata como descarga/enlace legítimo y se abre en el navegador del
            // sistema — los sitios de manga rotan constantemente los dominios de
            // sus mirrors de descarga, y mantener un allowlist a mano los rompe
            // en cuanto aparece un dominio nuevo.
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (string.IsNullOrEmpty(args.Uri)) return;
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) return;

                // Solo http/https pueden acabar en Process.Start — estas páginas están
                // cargadas de publicidad hostil, y un popup puede intentar abrir
                // cualquier esquema de URI (file:, o el de otra app instalada) para
                // lanzar programas locales. Cualquier otra cosa se descarta aquí,
                // antes incluso de mirar el dominio.
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

                var host = uri.Host.ToLowerInvariant();

                // Publicidad/tracking conocido → bloqueado silenciosamente
                foreach (var domain in BlockedDomains)
                {
                    if (host == domain || host.EndsWith("." + domain))
                        return;
                }

                // Mismo sitio de manga → navegar dentro de la app
                foreach (var domain in MangaSiteDomains)
                {
                    if (host == domain || host.EndsWith("." + domain))
                    {
                        core.Navigate(args.Uri);
                        return;
                    }
                }

                // Cualquier otra cosa (mirrors de descarga, acortadores, etc.)
                // → abrir en el navegador del sistema
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

            // ── Descargas directas (navegación, no popup) ───────────────────
            core.DownloadStarting += (_, args) =>
            {
                var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                downloads = Path.Combine(downloads, "Downloads");
                Directory.CreateDirectory(downloads);

                var name = string.IsNullOrWhiteSpace(args.ResultFilePath)
                    ? Path.GetFileName(new Uri(args.DownloadOperation.Uri).LocalPath)
                    : Path.GetFileName(args.ResultFilePath);
                if (string.IsNullOrWhiteSpace(name)) name = "descarga";

                var dest = Path.Combine(downloads, name);
                var i = 1;
                while (File.Exists(dest))
                    dest = Path.Combine(downloads, $"{Path.GetFileNameWithoutExtension(name)} ({i++}){Path.GetExtension(name)}");

                args.ResultFilePath = dest;
                args.Handled = false; // deja visible la UI nativa de descarga de WebView2
            };

            // ── Bloquear permisos (notificaciones, cámara, localización…) ──
            core.PermissionRequested += (_, args) =>
                args.State = CoreWebView2PermissionState.Deny;

            // ── Descartar diálogos de script (alert/confirm/prompt de spam) ──
            core.ScriptDialogOpening += (_, _) => { };

            // ── Adblock por dominio ─────────────────────────────────────────
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnWebResourceRequested;

            // ── Limpiar overlays tras cada carga ────────────────────────────
            core.NavigationCompleted += async (_, _) =>
                await core.ExecuteScriptAsync(AntiPopupScript);
            core.DOMContentLoaded += async (_, _) =>
                await core.ExecuteScriptAsync(AntiPopupScript);

            // ── Sincronizar barra de direcciones ────────────────────────────
            core.SourceChanged += (_, _) => _vm?.OnNavigated(core.Source ?? "");

            // ── Si una de las páginas de manga conocidas falla, probar la siguiente ──
            // Aplica tanto a la navegación de entrada como a un click directo en un
            // marcador (Tomosmanga/LexMangas/MangaYComics): si la que se pidió está
            // caída, salta sola a la siguiente en vez de quedarse en blanco.
            core.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess) return;
                var idx = Array.IndexOf(EntryPages, core.Source);
                if (idx < 0 || idx >= EntryPages.Length - 1) return; // no es una de las nuestras, o ya no quedan más que probar

                _entryPageIndex = idx + 1;
                core.Navigate(EntryPages[_entryPageIndex]);
            };

            core.Navigate(EntryPages[_entryPageIndex]);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo iniciar el navegador integrado.\n\n" +
                $"Asegúrate de tener Microsoft Edge/WebView2 Runtime instalado.\n\n{ex.Message}",
                "Error de navegador", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;

        var host = uri.Host;
        foreach (var domain in BlockedDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                e.Response = WebBrowser.CoreWebView2.Environment.CreateWebResourceResponse(
                    new MemoryStream(), 200, "OK", "");
                return;
            }
        }
    }

    private void OnNavigationRequested(string url)
    {
        if (!_webViewReady) return;
        Dispatcher.InvokeAsync(() => WebBrowser.CoreWebView2?.Navigate(url));
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewReady && WebBrowser.CoreWebView2.CanGoBack)
            WebBrowser.CoreWebView2.GoBack();
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewReady && WebBrowser.CoreWebView2.CanGoForward)
            WebBrowser.CoreWebView2.GoForward();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewReady)
            WebBrowser.CoreWebView2.Reload();
    }
}
