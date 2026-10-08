using Avalonia.Threading;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public enum UpdateBannerState { Hidden, Available, Downloading, Ready, Portable }

/// <summary>
/// Barra de «versión nueva» de MainWindow y apartado de Ajustes → Acerca de.
/// Al arrancar nunca enseña errores: sin conexión, simplemente no sale.
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

    /// <param name="prepareRestart">Guarda los datos: aplicar la actualización cierra el proceso sin pasar por la salida normal.</param>
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
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(PrimaryText));
        }
    }

    public bool   IsVisible      => _state != UpdateBannerState.Hidden;
    public bool   IsDownloading  => _state == UpdateBannerState.Downloading;
    public int    Progress       { get => _progress; private set => SetProperty(ref _progress, value); }
    public string CurrentVersion => $"Versión {_updates.CurrentVersion}";
    /// <summary>Resultado de «Buscar actualizaciones» en Ajustes.</summary>
    public string CheckStatus    { get => _checkStatus; private set => SetProperty(ref _checkStatus, value); }

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

    /// <summary>Escribe en los ajustes; guardarlos es cosa de quien lo cambie.</summary>
    public bool CheckOnStartup
    {
        get => _settings.CheckOnStartup;
        set
        {
            if (_settings.CheckOnStartup == value) return;
            _settings.CheckOnStartup = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Actualizar / Reintentar / Reiniciar / Descargar, según el estado.</summary>
    public AsyncRelayCommand PrimaryCommand { get; }
    public RelayCommand LaterCommand { get; }

    /// <summary>La última operación lanzada por PrimaryCommand (los tests la esperan).</summary>
    public Task LastOperation { get; private set; } = Task.CompletedTask;

    /// <summary>Comprobación al abrir: respeta el ajuste y espera un poco para no estorbar al arranque.</summary>
    public async Task StartAsync()
    {
        if (!_settings.CheckOnStartup) return;
        if (_startupDelay > TimeSpan.Zero) await Task.Delay(_startupDelay);
        Apply(await _updates.CheckAsync());
    }

    /// <summary>Botón de Ajustes: comprueba siempre y lo cuenta con palabras.</summary>
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
        // Ya descargando o lista: una comprobación nueva no la pisa.
        if (r.Status != UpdateCheckStatus.Available || _state is UpdateBannerState.Downloading or UpdateBannerState.Ready)
            return;
        _version = r.Version ?? "";
        _retry = false;
        State = r.CanSelfUpdate ? UpdateBannerState.Available : UpdateBannerState.Portable;
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(PrimaryText));
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
                    await _updates.DownloadAsync(p => Dispatcher.UIThread.Post(() => Progress = p));
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
