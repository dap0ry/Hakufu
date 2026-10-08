# Actualizaciones automáticas — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hakufu avisa de versión nueva y se actualiza con un clic en Windows, macOS y Linux (Velopack + GitHub Releases).

**Architecture:** `UpdateService` (única pieza con red) envuelve el `UpdateManager` de Velopack contra
`GithubSource("https://github.com/dap0ry/Hakufu")`; si la app no está instalada con Velopack (zip
portable o `dotnet run`) consulta `releases/latest` de la API de GitHub y solo avisa.
`UpdateBannerViewModel` lleva el estado de la barra de `MainWindow` y lo comparten Ajustes → Acerca de.
El CI añade `vpk pack` por sistema y sube instaladores + feeds a la misma release.

**Tech Stack:** .NET 10, Avalonia 11.3, Velopack 1.2.161 (NuGet `Velopack` + herramienta `vpk`), xUnit + Avalonia.Headless.

**Spec:** `docs/superpowers/specs/2026-10-08-actualizaciones-design.md`

## Global Constraints

- `packId` = `Hakufu` (el de la 0.9.7). Canales: `win`, `osx-arm64`, `osx-x64`, `linux`.
- `Velopack` NuGet y `vpk` en la **misma versión: 1.2.161**.
- Repo de releases: `https://github.com/dap0ry/Hakufu`; `GithubSource` con `prerelease: false`.
- `UpdateService` es lo ÚNICO con red. Ningún test usa la red (fakes / `HttpMessageHandler` falso).
- macOS: `vpk pack ... --signAppIdentity "-"` (firma ad hoc; sin ella la firma del `.app` queda rota — comprobado 08/10).
- Los `.zip`/`.tar.gz` actuales se siguen publicando con los mismos nombres (la web depende de ellos).
- Texto de UI en español. Colores con `{DynamicResource}`. Vistas con `x:DataType`.
- Commits solo en local (rama `feat/actualizaciones`); nada de push sin que Dani lo diga.
- Ejecutar dotnet con `export DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH:~/.dotnet/tools`.

## Review Focus

1. **Sin internet / GitHub caído / límite de la API (403)** → al arrancar no aparece nada y la app no se cuelga ni lanza; en Ajustes sale «No se pudo comprobar». Test en Task 2 (handler que lanza y que devuelve 403) y Task 3 (Failed → Hidden).
2. **Tag raro o pre-release** (`v0.11.0-beta.1`, `0.11.0`, `vX`) → comparación de versiones no lanza; una beta nunca se ofrece a una estable más nueva. Tests en Task 2.
3. **Reiniciar para actualizar sin perder el tiempo de lectura de la sesión** → se guarda `data.json` antes de `ApplyUpdatesAndRestart` (que mata el proceso sin pasar por `desktop.Exit`). Test en Task 3 (orden prepare → apply).
4. **Descarga cortada** → la barra vuelve a «Disponible» con «Reintentar», sin estado colgado. Test en Task 3.
5. **Pulsar «Buscar actualizaciones» dos veces o mientras descarga** → no lanza dos descargas. Test en Task 3 (el comando no se puede ejecutar en `Downloading`).

---

### Task 1: Velopack en el arranque + ajuste `CheckOnStartup` + excepción de red documentada

**Files:**
- Modify: `Hakufu.csproj` (PackageReference `Velopack` 1.2.161 — ya añadido en el worktree)
- Modify: `Program.cs`
- Create: `MVVM/Model/UpdateSettings.cs`
- Modify: `Data/AppDataStore.cs`
- Modify: `CLAUDE.md`
- Test: `tests/Hakufu.Tests/UpdateTests.cs`

**Interfaces:**
- Produces: `UpdateSettings { bool CheckOnStartup = true }`, `AppDataStore.Updates`.

- [ ] **Step 1: test que falla** — `tests/Hakufu.Tests/UpdateTests.cs`:

```csharp
using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Tests;

public class UpdateSettingsTests
{
    [Fact]
    public async Task CheckOnStartup_is_on_by_default_and_survives_a_save()
    {
        using var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        Assert.True(repo.Current.Updates.CheckOnStartup);

        repo.Current.Updates.CheckOnStartup = false;
        await repo.SaveAsync();

        var again = new JsonDataRepository();
        await again.LoadAsync();
        Assert.False(again.Current.Updates.CheckOnStartup);
    }
}
```

- [ ] **Step 2:** `dotnet test tests/Hakufu.Tests --filter UpdateSettingsTests` → FAIL (no existe `Updates`).
- [ ] **Step 3: implementación**

`MVVM/Model/UpdateSettings.cs`:
```csharp
namespace Hakufu.MVVM.Model;

/// <summary>Ajustes de actualizaciones (Ajustes → Acerca de).</summary>
public class UpdateSettings
{
    /// <summary>Buscar versión nueva al abrir Hakufu.</summary>
    public bool CheckOnStartup { get; set; } = true;
}
```
`Data/AppDataStore.cs`, al final de la clase:
```csharp
    public UpdateSettings            Updates         { get; set; } = new();
```
`Program.cs`:
```csharp
using Avalonia;
using Velopack;

namespace Hakufu;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Lo primero de todo: al instalar/actualizar, Velopack arranca la app con
        // argumentos propios, hace su trabajo y sale aquí mismo.
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    …
```
`CLAUDE.md`: cambiar «**no backend, no accounts, no network access at all** (don't add any: it's a product decision)» por «**no backend, no accounts, no network access** — the only exception is `Services/UpdateService` checking/downloading updates from GitHub Releases (opt-out in Ajustes). Don't add any other network use: it's a product decision.» y añadir `UpdateService` a la lista de Services y una sección «Releases» con `vpk` (ver Task 5).

- [ ] **Step 4:** test → PASS. `dotnet build Hakufu.csproj` sin warnings.
- [ ] **Step 5:** commit `HMR actualizaciones: Velopack en el arranque y ajuste para buscar al abrir`.

---

### Task 2: `UpdateService` + comparación de versiones

**Files:**
- Create: `Services/IUpdateService.cs`, `Services/UpdateService.cs`, `Services/AppVersion.cs`
- Test: `tests/Hakufu.Tests/UpdateTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum UpdateCheckStatus { UpToDate, Available, Failed }
public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version = null, bool CanSelfUpdate = false);
public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
    Task DownloadAsync(Action<int> progress, CancellationToken ct = default); // lanza si falla
    void ApplyAndRestart();                                                   // sale del proceso
}
public static class AppVersion
{
    public static string Current { get; }                         // "0.10.1"
    public static bool IsNewer(string candidate, string current); // tolera "v", "-beta.N"; nunca lanza
}
public sealed class UpdateService : IUpdateService
{
    public const string RepoUrl = "https://github.com/dap0ry/Hakufu";
    public UpdateService(HttpMessageHandler? http = null, string? localSource = null);
}
```

- [ ] **Step 1: tests que fallan** (añadir a `UpdateTests.cs`):

```csharp
public class AppVersionTests
{
    [Theory]
    [InlineData("v0.11.0", "0.10.1", true)]
    [InlineData("0.10.1", "0.10.1", false)]
    [InlineData("v0.10.0", "0.10.1", false)]
    [InlineData("v0.11.0-beta.1", "0.10.1", true)]
    [InlineData("v0.11.0-beta.1", "0.11.0", false)]
    [InlineData("v0.11.0", "0.11.0-beta.2", true)]
    [InlineData("vX", "0.10.1", false)]
    [InlineData("", "0.10.1", false)]
    public void IsNewer(string candidate, string current, bool expected)
        => Assert.Equal(expected, AppVersion.IsNewer(candidate, current));
}

public class UpdateServicePortableTests
{
    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Calls++; return Task.FromResult(respond(r)); }
    }

    private static HttpResponseMessage Json(string tag) =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent($"{{\"tag_name\":\"{tag}\",\"prerelease\":false}}") };

    [Fact]
    public async Task Not_installed_newer_tag_is_available_but_not_self_updatable()
    {
        var http = new FakeHttp(_ => Json("v99.0.0"));
        var r = await new UpdateService(http).CheckAsync();
        Assert.Equal(UpdateCheckStatus.Available, r.Status);
        Assert.Equal("99.0.0", r.Version);
        Assert.False(r.CanSelfUpdate);
    }

    [Fact]
    public async Task Same_tag_is_up_to_date()
    {
        var r = await new UpdateService(new FakeHttp(_ => Json("v" + AppVersion.Current))).CheckAsync();
        Assert.Equal(UpdateCheckStatus.UpToDate, r.Status);
    }

    [Fact]
    public async Task Network_error_or_403_is_failed_and_never_throws()
    {
        var down = new FakeHttp(_ => throw new HttpRequestException("sin red"));
        Assert.Equal(UpdateCheckStatus.Failed, (await new UpdateService(down).CheckAsync()).Status);

        var limited = new FakeHttp(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        Assert.Equal(UpdateCheckStatus.Failed, (await new UpdateService(limited).CheckAsync()).Status);
    }

    [Fact]
    public async Task Sends_a_user_agent_and_asks_for_the_latest_release()
    {
        HttpRequestMessage? seen = null;
        await new UpdateService(new FakeHttp(r => { seen = r; return Json("v0.0.1"); })).CheckAsync();
        Assert.Equal("https://api.github.com/repos/dap0ry/Hakufu/releases/latest", seen!.RequestUri!.ToString());
        Assert.NotEmpty(seen.Headers.UserAgent);
    }
}
```

- [ ] **Step 2:** `dotnet test tests/Hakufu.Tests --filter "AppVersionTests|UpdateServicePortableTests"` → FAIL (no compila).
- [ ] **Step 3: implementación**

`Services/AppVersion.cs`:
```csharp
using System.Reflection;

namespace Hakufu.Services;

/// <summary>Versión de la app y comparación de etiquetas de release ("v0.11.0", "v0.11.0-beta.1").</summary>
public static class AppVersion
{
    public static string Current { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    public static bool IsNewer(string candidate, string current)
    {
        if (!TryParse(candidate, out var c, out var cPre) || !TryParse(current, out var v, out var vPre))
            return false;
        var cmp = c.CompareTo(v);
        if (cmp != 0) return cmp > 0;
        // Misma base: estable > beta; entre betas, la de número mayor.
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
}
```
`Services/IUpdateService.cs`: las firmas de **Interfaces** (enum, record, interfaz) con comentarios de una línea.

`Services/UpdateService.cs`:
```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace Hakufu.Services;

/// <summary>
/// ÚNICA parte de Hakufu que usa la red: comprueba y descarga versiones nuevas de
/// las releases de GitHub. Instalada con Velopack se actualiza sola; si no (zip
/// portable o dotnet run), solo consulta la última release para avisar.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public const string RepoUrl = "https://github.com/dap0ry/Hakufu";
    private const string LatestApi = "https://api.github.com/repos/dap0ry/Hakufu/releases/latest";

    private readonly UpdateManager? _velopack;
    private readonly HttpClient _http;
    private UpdateInfo? _pending;

    public UpdateService(HttpMessageHandler? http = null, string? localSource = null)
    {
        _http = http is null ? new HttpClient() : new HttpClient(http);
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Hakufu", AppVersion.Current));

        // HAKUFU_UPDATE_SOURCE=<carpeta>: probar actualizaciones reales sin publicar nada.
        localSource ??= Environment.GetEnvironmentVariable("HAKUFU_UPDATE_SOURCE");
        IUpdateSource source = string.IsNullOrEmpty(localSource)
            ? new GithubSource(RepoUrl, null, false)
            : new SimpleFileSource(new DirectoryInfo(localSource));
        try
        {
            var mgr = new UpdateManager(source);
            _velopack = mgr.IsInstalled && http is null ? mgr : null;
        }
        catch { _velopack = null; }
    }

    public string CurrentVersion => _velopack?.CurrentVersion?.ToString() ?? AppVersion.Current;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (_velopack is not null)
            {
                _pending = await _velopack.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(10), ct);
                return _pending is null
                    ? new(UpdateCheckStatus.UpToDate)
                    : new(UpdateCheckStatus.Available, _pending.TargetFullRelease.Version.ToString(), CanSelfUpdate: true);
            }

            using var res = await _http.GetAsync(LatestApi, ct);
            if (!res.IsSuccessStatusCode) return new(UpdateCheckStatus.Failed);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            return AppVersion.IsNewer(tag, AppVersion.Current)
                ? new(UpdateCheckStatus.Available, tag.TrimStart('v', 'V'))
                : new(UpdateCheckStatus.UpToDate);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new(UpdateCheckStatus.Failed);
        }
    }

    public async Task DownloadAsync(Action<int> progress, CancellationToken ct = default)
    {
        if (_velopack is null || _pending is null) throw new InvalidOperationException("Nada que descargar.");
        await _velopack.DownloadUpdatesAsync(_pending, progress, ct);
    }

    public void ApplyAndRestart()
    {
        if (_velopack is null || _pending is null) return;
        _velopack.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}
```
(Al pasar un `HttpMessageHandler` de test, `_velopack` queda en null a propósito: los tests siempre van por el camino portable, que es el único con red falseable.)

- [ ] **Step 4:** tests → PASS; suite completa `dotnet test tests/Hakufu.Tests` en verde.
- [ ] **Step 5:** commit `HMR actualizaciones: UpdateService (Velopack o API de GitHub) y comparación de versiones`.

---

### Task 3: `UpdateBannerViewModel`

**Files:**
- Create: `MVVM/ViewModel/UpdateBannerViewModel.cs`, `Services/UrlOpener.cs`
- Test: `tests/Hakufu.Tests/UpdateTests.cs` (+ `FakeUpdateService` en el mismo archivo)

**Interfaces:**
- Consumes: `IUpdateService`, `UpdateCheckResult`, `UpdateSettings`.
- Produces:
```csharp
public enum UpdateBannerState { Hidden, Available, Downloading, Ready, Portable }
public class UpdateBannerViewModel : BaseViewModel
{
    public UpdateBannerViewModel(IUpdateService updates, UpdateSettings settings,
                                 Action prepareRestart, Action<string> openUrl, TimeSpan? startupDelay = null);
    public const string DownloadPage = "https://hakufu.vercel.app/#descargas";
    UpdateBannerState State; bool IsVisible; string Message; int Progress; string PrimaryText;
    string CurrentVersion; string CheckStatus;     // texto para Ajustes
    bool CheckOnStartup { get; set; }              // escribe en UpdateSettings (el guardado lo hace quien lo use)
    Task StartAsync();                             // arranque: respeta CheckOnStartup y el retraso
    Task CheckNowAsync();                          // botón de Ajustes: siempre comprueba y rellena CheckStatus
    AsyncRelayCommand PrimaryCommand;              // Actualizar / Reintentar / Reiniciar / Descargar
    RelayCommand LaterCommand;                     // oculta la barra
}
public static class UrlOpener { public static void Open(string url); }
```

- [ ] **Step 1: tests que fallan**:

```csharp
public sealed class FakeUpdateService : IUpdateService
{
    public UpdateCheckResult Result = new(UpdateCheckStatus.UpToDate);
    public bool FailDownload;
    public int Checks, Downloads, Applies;
    public List<string> Log = [];
    public string CurrentVersion => "0.10.1";
    public Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default) { Checks++; return Task.FromResult(Result); }
    public Task DownloadAsync(Action<int> progress, CancellationToken ct = default)
    {
        Downloads++;
        progress(50);
        if (FailDownload) throw new HttpRequestException("cortado");
        progress(100);
        return Task.CompletedTask;
    }
    public void ApplyAndRestart() { Applies++; Log.Add("apply"); }
}

public class UpdateBannerTests
{
    private static (UpdateBannerViewModel vm, FakeUpdateService svc, UpdateSettings set, List<string> urls)
        Make(UpdateCheckResult result, bool checkOnStartup = true)
    {
        var svc = new FakeUpdateService { Result = result };
        var set = new UpdateSettings { CheckOnStartup = checkOnStartup };
        var urls = new List<string>();
        var vm = new UpdateBannerViewModel(svc, set, () => svc.Log.Add("save"), urls.Add, TimeSpan.Zero);
        return (vm, svc, set, urls);
    }

    [Fact]
    public async Task Up_to_date_or_failed_keeps_the_banner_hidden()
    {
        foreach (var s in new[] { UpdateCheckStatus.UpToDate, UpdateCheckStatus.Failed })
        {
            var (vm, _, _, _) = Make(new(s));
            await vm.StartAsync();
            Assert.Equal(UpdateBannerState.Hidden, vm.State);
            Assert.False(vm.IsVisible);
        }
    }

    [Fact]
    public async Task Startup_check_is_skipped_when_disabled()
    {
        var (vm, svc, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true), checkOnStartup: false);
        await vm.StartAsync();
        Assert.Equal(0, svc.Checks);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public async Task Available_update_downloads_then_restarts_saving_first()
    {
        var (vm, svc, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Contains("0.11.0", vm.Message);
        Assert.Equal("Actualizar", vm.PrimaryText);

        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(UpdateBannerState.Ready, vm.State);
        Assert.Equal(100, vm.Progress);
        Assert.Equal("Reiniciar", vm.PrimaryText);

        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(["save", "apply"], svc.Log);
    }

    [Fact]
    public async Task Failed_download_goes_back_to_available_with_retry()
    {
        var (vm, svc, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        svc.FailDownload = true;
        await vm.StartAsync();
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal(UpdateBannerState.Available, vm.State);
        Assert.Equal("Reintentar", vm.PrimaryText);
    }

    [Fact]
    public async Task Cannot_start_a_second_download_while_downloading()
    {
        var (vm, _, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        vm.SetStateForTest(UpdateBannerState.Downloading);
        Assert.False(vm.PrimaryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Portable_copy_offers_the_download_page()
    {
        var (vm, svc, _, urls) = Make(new(UpdateCheckStatus.Available, "0.11.0", CanSelfUpdate: false));
        await vm.StartAsync();
        Assert.Equal(UpdateBannerState.Portable, vm.State);
        Assert.Equal("Descargar", vm.PrimaryText);
        vm.PrimaryCommand.Execute(null);
        await vm.LastOperation;
        Assert.Equal([UpdateBannerViewModel.DownloadPage], urls);
        Assert.Equal(0, svc.Downloads);
    }

    [Fact]
    public async Task Later_hides_and_check_now_reports_in_words()
    {
        var (vm, svc, _, _) = Make(new(UpdateCheckStatus.Available, "0.11.0", true));
        await vm.StartAsync();
        vm.LaterCommand.Execute(null);
        Assert.False(vm.IsVisible);

        svc.Result = new(UpdateCheckStatus.UpToDate);
        await vm.CheckNowAsync();
        Assert.Equal("Ya tienes la última versión.", vm.CheckStatus);

        svc.Result = new(UpdateCheckStatus.Failed);
        await vm.CheckNowAsync();
        Assert.Equal("No se pudo comprobar. ¿Hay conexión a internet?", vm.CheckStatus);

        svc.Result = new(UpdateCheckStatus.Available, "0.12.0", true);
        await vm.CheckNowAsync();
        Assert.Equal("Hay una versión nueva: 0.12.0.", vm.CheckStatus);
        Assert.True(vm.IsVisible);
    }
}
```

- [ ] **Step 2:** `dotnet test tests/Hakufu.Tests --filter UpdateBannerTests` → FAIL.
- [ ] **Step 3: implementación** — `MVVM/ViewModel/UpdateBannerViewModel.cs`:

```csharp
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public enum UpdateBannerState { Hidden, Available, Downloading, Ready, Portable }

/// <summary>
/// Barra de «versión nueva» de MainWindow (y el apartado de Ajustes → Acerca de).
/// Nunca muestra errores al arrancar: sin conexión, simplemente no sale.
/// </summary>
public class UpdateBannerViewModel : BaseViewModel
{
    public const string DownloadPage = "https://hakufu.vercel.app/#descargas";

    private readonly IUpdateService _updates;
    private readonly UpdateSettings _settings;
    private readonly Action _prepareRestart;
    private readonly Action<string> _openUrl;
    private readonly TimeSpan _startupDelay;

    private UpdateBannerState _state;
    private string _version = "";
    private bool _retry;
    private int _progress;
    private string _checkStatus = "";

    public UpdateBannerViewModel(IUpdateService updates, UpdateSettings settings,
                                 Action prepareRestart, Action<string> openUrl, TimeSpan? startupDelay = null)
    {
        _updates = updates;
        _settings = settings;
        _prepareRestart = prepareRestart;
        _openUrl = openUrl;
        _startupDelay = startupDelay ?? TimeSpan.FromSeconds(5);
        PrimaryCommand = new AsyncRelayCommand(() => LastOperation = PrimaryAsync(),
                                               () => _state is not (UpdateBannerState.Hidden or UpdateBannerState.Downloading));
        LaterCommand = new RelayCommand(() => State = UpdateBannerState.Hidden);
    }

    public UpdateBannerState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(PrimaryText));
            OnPropertyChanged(nameof(IsDownloading));
        }
    }

    public bool   IsVisible     => _state != UpdateBannerState.Hidden;
    public bool   IsDownloading => _state == UpdateBannerState.Downloading;
    public int    Progress      { get => _progress; private set => SetProperty(ref _progress, value); }
    public string CurrentVersion => $"Versión {_updates.CurrentVersion}";
    public string CheckStatus   { get => _checkStatus; private set => SetProperty(ref _checkStatus, value); }

    public string Message => _state switch
    {
        UpdateBannerState.Downloading => $"Descargando Hakufu {_version}…",
        UpdateBannerState.Ready       => $"Hakufu {_version} está lista. Reinicia para terminar.",
        _                             => $"Hakufu {_version} disponible",
    };

    public string PrimaryText => _state switch
    {
        UpdateBannerState.Ready    => "Reiniciar",
        UpdateBannerState.Portable => "Descargar",
        _ when _retry              => "Reintentar",
        _                          => "Actualizar",
    };

    public bool CheckOnStartup
    {
        get => _settings.CheckOnStartup;
        set { if (_settings.CheckOnStartup == value) return; _settings.CheckOnStartup = value; OnPropertyChanged(); }
    }

    public AsyncRelayCommand PrimaryCommand { get; }
    public RelayCommand LaterCommand { get; }

    /// <summary>La última operación lanzada por un comando (para esperarla en los tests).</summary>
    public Task LastOperation { get; private set; } = Task.CompletedTask;

    public async Task StartAsync()
    {
        if (!_settings.CheckOnStartup) return;
        if (_startupDelay > TimeSpan.Zero) await Task.Delay(_startupDelay);
        Apply(await _updates.CheckAsync());
    }

    public async Task CheckNowAsync()
    {
        CheckStatus = "Buscando…";
        var r = await _updates.CheckAsync();
        CheckStatus = r.Status switch
        {
            UpdateCheckStatus.UpToDate  => "Ya tienes la última versión.",
            UpdateCheckStatus.Available => $"Hay una versión nueva: {r.Version}.",
            _                           => "No se pudo comprobar. ¿Hay conexión a internet?",
        };
        Apply(r);
    }

    internal void SetStateForTest(UpdateBannerState s) => State = s;

    private void Apply(UpdateCheckResult r)
    {
        if (r.Status != UpdateCheckStatus.Available || _state is UpdateBannerState.Downloading or UpdateBannerState.Ready)
            return;
        _version = r.Version ?? "";
        _retry = false;
        State = r.CanSelfUpdate ? UpdateBannerState.Available : UpdateBannerState.Portable;
        OnPropertyChanged(nameof(Message));
    }

    private async Task PrimaryAsync()
    {
        switch (_state)
        {
            case UpdateBannerState.Portable:
                _openUrl(DownloadPage);
                return;
            case UpdateBannerState.Ready:
                _prepareRestart();
                _updates.ApplyAndRestart();
                return;
            case UpdateBannerState.Available:
                Progress = 0;
                State = UpdateBannerState.Downloading;
                try
                {
                    await _updates.DownloadAsync(p => Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = p));
                    Progress = 100;
                    State = UpdateBannerState.Ready;
                }
                catch
                {
                    _retry = true;
                    State = UpdateBannerState.Available;
                    OnPropertyChanged(nameof(PrimaryText));
                }
                return;
        }
    }
}
```
Nota: en los tests sin Dispatcher, `Dispatcher.UIThread.Post` encola y el valor final lo fija `Progress = 100` tras la descarga. Para que `Assert.Equal(100, vm.Progress)` no dependa del dispatcher, el `Progress = 100` va después del await (ya está).

`Services/UrlOpener.cs`:
```csharp
using System.Diagnostics;

namespace Hakufu.Services;

/// <summary>Abre una URL https en el navegador del sistema.</summary>
public static class UrlOpener
{
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sin navegador: no pasa nada */ }
    }
}
```

- [ ] **Step 4:** tests → PASS; suite completa en verde.
- [ ] **Step 5:** commit `HMR actualizaciones: barra de versión nueva (estado, descarga, reiniciar, portable)`.

---

### Task 4: Barra en `MainWindow`, Ajustes → Acerca de y cableado

**Files:**
- Modify: `CompositionRoot.cs`, `App.axaml.cs`, `MVVM/ViewModel/MainWindowViewModel.cs`, `MainWindow.axaml`,
  `MVVM/ViewModel/SettingsViewModel.cs`, `MVVM/View/SettingsView.axaml`, `tests/Hakufu.Tests/ViewSmoke.cs`
- Test: `tests/Hakufu.Tests/UpdateTests.cs` (vistas)

**Interfaces:**
- Consumes: `UpdateBannerViewModel`, `IUpdateService`, `UrlOpener.Open`.
- Produces: `CompositionRoot(IDataRepository repo, IUpdateService? updates = null)`, `CompositionRoot.Updates`,
  `CompositionRoot.UpdateBanner`, `CompositionRoot.PrepareRestart` (Action asignable por App),
  `MainWindowViewModel.UpdateBanner`, `SettingsViewModel.Updates` (= el mismo `UpdateBannerViewModel`),
  `SettingsViewModel.CheckUpdatesCommand`.

- [ ] **Step 1: tests que fallan**:

```csharp
public class UpdateViewTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Banner_is_hidden_until_there_is_an_update_and_then_shows_it()
    {
        using var app = ViewSmoke.Start();
        var fake = (FakeUpdateService)app.Root.Updates;
        var bar = app.Window.FindControl<Avalonia.Controls.Border>("UpdateBar")!;
        Assert.False(bar.IsVisible);

        fake.Result = new(UpdateCheckStatus.Available, "0.11.0", true);
        await app.Root.UpdateBanner.CheckNowAsync();
        app.Pump();
        Assert.True(bar.IsVisible);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Settings_shows_the_update_section()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<SettingsViewModel>();
        app.Pump();
        var vm = (SettingsViewModel)app.Root.Navigation.CurrentViewModel!;
        Assert.Same(app.Root.UpdateBanner, vm.Updates);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
    }
}
```
(`using Hakufu.MVVM.ViewModel; using Hakufu.Services;` al principio de `UpdateTests.cs`.)

- [ ] **Step 2:** FAIL (no existen `Updates`, `UpdateBar`…).
- [ ] **Step 3: implementación**
  - `CompositionRoot`: constructor `CompositionRoot(IDataRepository repo, IUpdateService? updates = null)`;
    `Updates = updates ?? new UpdateService();`
    `UpdateBanner = new UpdateBannerViewModel(Updates, repo.Current.Updates, () => PrepareRestart(), UrlOpener.Open);`
    `public Action PrepareRestart { get; set; } = () => { };`
    `CreateMainViewModel() => new(Navigation, Dialog, UpdateBanner);`
    `SettingsViewModel` recibe `UpdateBanner` como sexto parámetro.
  - `MainWindowViewModel`: parámetro `UpdateBannerViewModel? updateBanner = null` → propiedad `UpdateBanner`.
  - `MainWindow.axaml`: dentro de `Root`, tras el contenido de página (fila 1, `ZIndex="50"`), abajo y centrada:
```xml
<Border Name="UpdateBar" Grid.Row="1" ZIndex="50"
        x:DataType="vm:MainWindowViewModel"
        IsVisible="{Binding UpdateBanner.IsVisible, FallbackValue=False}"
        Classes="card" Padding="16,10" Margin="0,0,0,20"
        HorizontalAlignment="Center" VerticalAlignment="Bottom">
    <Grid ColumnDefinitions="Auto,Auto,Auto" >
        <StackPanel Grid.Column="0" VerticalAlignment="Center" Margin="0,0,16,0">
            <TextBlock Text="{Binding UpdateBanner.Message}" FontWeight="SemiBold"/>
            <ProgressBar IsVisible="{Binding UpdateBanner.IsDownloading}"
                         Value="{Binding UpdateBanner.Progress}" Maximum="100"
                         Height="3" MinWidth="200" Margin="0,6,0,0"/>
        </StackPanel>
        <Button Grid.Column="1" Classes="primary" Padding="16,8"
                Content="{Binding UpdateBanner.PrimaryText}"
                Command="{Binding UpdateBanner.PrimaryCommand}"/>
        <Button Grid.Column="2" Classes="ghost" Padding="12,8" Margin="8,0,0,0"
                Content="Más tarde" Command="{Binding UpdateBanner.LaterCommand}"
                IsVisible="{Binding !UpdateBanner.IsDownloading}"/>
    </Grid>
</Border>
```
  - `App.axaml.cs`, tras `mainWindow.DataContext = …`:
    `root.PrepareRestart = SaveOnExit;` y `_ = root.UpdateBanner.StartAsync();`
    En `SaveOnExit`, tras sumar `elapsed`, poner `_sessionStart = DateTime.Now;` (si se llama dos veces no cuenta doble).
  - `SettingsViewModel`: campo y propiedad `public UpdateBannerViewModel Updates { get; }`;
    `public AsyncRelayCommand CheckUpdatesCommand => new(() => Updates.CheckNowAsync());`
    y un `public bool CheckUpdatesOnStartup { get => Updates.CheckOnStartup; set { Updates.CheckOnStartup = value; OnPropertyChanged(); _ = _repo.SaveAsync(); } }`.
  - `SettingsView.axaml`, en la tarjeta ACERCA DE, sustituir el `TextBlock` de `{Binding VersionText}` por
    el mismo texto + debajo (antes del botón legal):
```xml
<StackPanel Orientation="Horizontal" Spacing="12" Margin="0,18,0,0">
    <Button Classes="ghost" Content="Buscar actualizaciones" Padding="16,9"
            Command="{Binding CheckUpdatesCommand}"/>
    <TextBlock Text="{Binding Updates.CheckStatus}" FontSize="12" VerticalAlignment="Center"
               Foreground="{DynamicResource TextMuted}"/>
</StackPanel>
<CheckBox Content="Buscar actualizaciones al abrir" Margin="0,10,0,0"
          IsChecked="{Binding CheckUpdatesOnStartup}"/>
```
  - `ViewSmoke.cs`: las dos `new CompositionRoot(repo)` → `new CompositionRoot(repo, new FakeUpdateService())`.
- [ ] **Step 4:** tests → PASS; suite completa en verde; `dotnet build` sin warnings.
- [ ] **Step 5: ver en la app** — relanzar con `dotnet run` (regla de Dani) y comprobar con
  `HAKUFU_UPDATE_SOURCE` vacío que no sale nada y que Ajustes → Acerca de dice «Ya tienes…» / «No se pudo…»
  (con `dotnet run` no está instalada: va por la API de GitHub; la última es 0.10.1 = actual → «Ya tienes la última versión.»).
  Captura de la barra forzando estado (test headless) para enseñarla.
- [ ] **Step 6:** commit `HMR actualizaciones: barra en la ventana y apartado en Ajustes → Acerca de`.

---

### Task 5: Empaquetado Velopack (scripts + CI)

**Files:**
- Create: `scripts/pack-velopack.sh`, `scripts/pack-velopack.ps1`
- Modify: `.github/workflows/build.yml`, `README.md`, `CLAUDE.md`

**Interfaces:**
- Produces (por canal, en `publish/velopack/`): `Hakufu-win-Setup.exe`, `Hakufu-win-Portable.zip`,
  `releases.win.json`, `RELEASES`, `assets.win.json`, `Hakufu-<v>-full.nupkg`;
  macOS `Hakufu-osx-arm64-Setup.pkg` (+ x64), `releases.osx-arm64.json`…; Linux `Hakufu-linux.AppImage`, `releases.linux.json`.
  (Los nombres exactos se confirman en Step 2 y se copian a la web en Task 6.)

- [ ] **Step 1: scripts**

`scripts/pack-velopack.sh`:
```bash
#!/usr/bin/env bash
# Empaqueta con Velopack lo que dejó scripts/publish.sh (instalador + feed de actualizaciones).
#   ./scripts/pack-velopack.sh osx-arm64 | osx-x64 | linux-x64
# Requiere: dotnet tool install -g vpk --version 1.2.161
set -euo pipefail
cd "$(dirname "$0")/.."
RID="$1"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Hakufu.csproj | head -1)"
OUT="publish/velopack"
mkdir -p "${OUT}"
case "${RID}" in
  osx-*)
    # --signAppIdentity "-": firma ad hoc DESPUÉS de meter UpdateMac; sin ella el .app sale con la firma rota.
    vpk pack -u Hakufu -v "${VERSION}" -p publish/Hakufu.app -e Hakufu \
      --channel "${RID}" --packTitle Hakufu --packAuthors "Daniel Poza Rivera" \
      --signAppIdentity "-" -o "${OUT}"
    ;;
  linux-x64)
    vpk pack -u Hakufu -v "${VERSION}" -p "publish/Hakufu-${RID}" -e Hakufu \
      --channel linux --packTitle Hakufu --packAuthors "Daniel Poza Rivera" \
      -i HakufuLogo.png --categories Graphics -o "${OUT}"
    ;;
  *) echo "RID no soportado: ${RID}" >&2; exit 1 ;;
esac
```
`scripts/pack-velopack.ps1`:
```powershell
# Empaqueta con Velopack lo que dejó scripts/publish.ps1 (Setup.exe + feed de actualizaciones).
# Canal "win": el mismo que la 0.9.7, para que se actualice sola.
param([string]$Rid = "win-x64")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$version = ([xml](Get-Content Hakufu.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
vpk pack -u Hakufu -v $version -p "publish\Hakufu-$Rid" -e Hakufu.exe `
  --channel win --packTitle Hakufu --packAuthors "Daniel Poza Rivera" `
  -i HakufuLogo.ico -o publish\velopack
```

- [ ] **Step 2: probar en el Mac** — `./scripts/publish.sh osx-arm64 && ./scripts/pack-velopack.sh osx-arm64`;
  `codesign --verify --deep --strict` sobre el `.app` del `Portable.zip` → OK; anotar nombres de archivos.
- [ ] **Step 3: CI** — en `build.yml`, job `build`, tras «Publicar»:
```yaml
      - name: Instalar vpk
        run: dotnet tool install -g vpk --version 1.2.161

      - name: Empaquetar con Velopack (Windows)
        if: runner.os == 'Windows'
        shell: pwsh
        run: ./scripts/pack-velopack.ps1 -Rid ${{ matrix.rid }}

      - name: Empaquetar con Velopack (macOS / Linux)
        if: runner.os != 'Windows'
        run: bash ./scripts/pack-velopack.sh ${{ matrix.rid }}
```
  y en `upload-artifact` añadir `publish/velopack/*`. Ojo: los dos Mac comparten nombre de artefacto
  distinto (`Hakufu-${{ matrix.rid }}`) y el `merge-multiple: true` del job `release` junta todo en
  `dist/`: los archivos de Velopack llevan el canal en el nombre, salvo `RELEASES`/`assets` —
  comprobar en Step 2 que ninguno choca entre canales (Windows: `RELEASES`; Mac: `RELEASES-osx-arm64`).
  En el job `release`: si el tag lleva `-` (beta) → `gh release create … --prerelease`.
  Linux en `ubuntu-latest`: `vpk` necesita `squashfs-tools`? → si falla, `sudo apt-get install -y squashfs-tools` antes.
- [ ] **Step 4:** `README.md` (sección de releases) y `CLAUDE.md` (Commands: `./scripts/pack-velopack.sh`).
- [ ] **Step 5:** commit `HMR actualizaciones: instaladores y feeds de Velopack en cada release`.

---

### Task 6: Web — instaladores como descarga principal

**Files:**
- Modify: `web/index.html` (botones, guías, `ASSETS` del script)

- [ ] **Step 1:** `ASSETS` del script:
```js
win:      ['Hakufu-win-Setup.exe', 'Hakufu-win-x64.zip'],
macArm:   ['Hakufu-osx-arm64-Setup.pkg', 'Hakufu-osx-arm64.zip'],
macIntel: ['Hakufu-osx-x64-Setup.pkg', 'Hakufu-osx-x64.zip'],
linux:    ['Hakufu-linux.AppImage', 'Hakufu-linux-x64.tar.gz'],
```
  (el primero que exista en la release gana: mientras la última release sea la 0.10.1, siguen saliendo los zip).
  Añadir claves `winZip`, `macArmZip`, `macIntelZip`, `linuxTar` para los enlaces secundarios «Versión portable (no se actualiza sola)».
- [ ] **Step 2:** textos de cada tarjeta y de las guías paso a paso: Windows → Setup.exe (SmartScreen: Más información → Ejecutar de todas formas);
  Mac → .pkg (Abrir igualmente en Privacidad y seguridad, una sola vez); Linux → AppImage (Propiedades → permitir ejecutar, o `chmod +x`).
  Añadir una línea: «Se actualiza sola: cuando haya versión nueva, Hakufu te avisa.»
- [ ] **Step 3:** captura headless de `#descargas` (desktop) para revisar.
- [ ] **Step 4:** commit `HMR web: los instaladores son la descarga principal; el zip queda como versión portable`.
  **No desplegar** hasta que exista una release con instaladores (la web nueva con la release vieja cae al zip, pero los textos no cuadrarían).

---

### Task 7: Prueba real de actualización en el Mac

- [ ] **Step 1:** subir `<Version>` a `0.11.0-beta.1` en el csproj (solo para la prueba; no commitear), publish + pack a `$SCRATCH/feed`;
  repetir con `0.11.0-beta.2` al mismo `feed` (vpk añade al feed existente).
- [ ] **Step 2:** instalar beta.1 con `sudo installer`? — NO: abrir el `.pkg` con `open` y que Dani lo instale, o extraer el
  `Portable.zip` a `~/Applications/Hakufu-prueba/` (sigue siendo «instalada» para Velopack).
  Lanzar con `HAKUFU_UPDATE_SOURCE=$SCRATCH/feed HAKUFU_DATA_DIR=$SCRATCH/datos` → aparece la barra «0.11.0-beta.2 disponible»
  → Actualizar → Reiniciar → arranca beta.2 (Ajustes → Acerca de dice 0.11.0-beta.2) sin aviso de Gatekeeper.
- [ ] **Step 3:** anotar resultado en el spec («Verificado en Mac arm64 el …») y commit.
- [ ] **Step 4:** Windows/Linux y 0.9.7 → beta: necesita push del branch + tag de pre-release → **preguntar a Dani** antes.

## Self-review

- Spec → tareas: experiencia (T3/T4), Ajustes (T4), portable (T2/T3), arquitectura (T1–T4), canales/0.9.7 (T5, verificación T7.4),
  CI y pre-release (T5), web (T6), errores (T2/T3), pruebas (T2–T4, T7). Fuera de alcance respetado.
- Tipos: `UpdateCheckResult(Status, Version, CanSelfUpdate)`, `UpdateBannerViewModel(IUpdateService, UpdateSettings, Action, Action<string>, TimeSpan?)`
  usados igual en T3/T4. `CompositionRoot.Updates` es `IUpdateService` (el test lo castea a `FakeUpdateService`).
