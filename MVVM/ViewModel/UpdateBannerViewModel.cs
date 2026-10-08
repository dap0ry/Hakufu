using Avalonia.Threading;
using Hakufu.I18n;
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
    private readonly TimeSpan _downloadStall;

    private UpdateBannerState _state;
    private string _version = "";
    private bool _retry;
    private int _progress;
    // Se guarda cómo escribirlo, no el texto: así sigue al idioma si se cambia después.
    private Func<string> _checkStatus = () => "";

    /// <param name="prepareRestart">Guarda los datos: aplicar la actualización cierra el proceso sin pasar por la salida normal.</param>
    public UpdateBannerViewModel(IUpdateService updates, UpdateSettings settings,
                                 Action prepareRestart, Action<string> openUrl, TimeSpan? startupDelay = null,
                                 TimeSpan? downloadStall = null)
    {
        _updates = updates;
        _settings = settings;
        _prepareRestart = prepareRestart;
        _openUrl = openUrl;
        _startupDelay = startupDelay ?? TimeSpan.FromSeconds(5);
        _downloadStall = downloadStall ?? TimeSpan.FromSeconds(60);
        PrimaryCommand = new AsyncRelayCommand(() => LastOperation = PrimaryAsync(),
            () => _state is not (UpdateBannerState.Hidden or UpdateBannerState.Downloading));
        LaterCommand = new RelayCommand(() => State = UpdateBannerState.Hidden);
        // Vive toda la sesión: al cambiar de idioma, rehacer sus textos.
        Localizer.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(PrimaryText));
            OnPropertyChanged(nameof(CurrentVersion));
            OnPropertyChanged(nameof(CheckStatus));
        };
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
    public string CurrentVersion => L.Format("common.version", _updates.CurrentVersion);
    /// <summary>Resultado de «Buscar actualizaciones» en Ajustes.</summary>
    public string CheckStatus    => _checkStatus();

    public string Message => _state switch
    {
        UpdateBannerState.Downloading => L.Format("updates.downloading", _version),
        UpdateBannerState.Ready       => L.Format("updates.ready", _version),
        _                             => L.Format("updates.available", _version),
    };

    public string PrimaryText => _state switch
    {
        UpdateBannerState.Ready    => L.Get("updates.restart"),
        UpdateBannerState.Portable => L.Get("updates.download"),
        _ when _retry              => L.Get("updates.retry"),
        _                          => L.Get("updates.update"),
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
        // Ya descargando o descargada: no se vuelve a preguntar (no se cambia lo que se va a aplicar).
        if (_state is UpdateBannerState.Downloading or UpdateBannerState.Ready)
        {
            var busy = _version;
            SetCheckStatus(() => L.Format("updates.busy", busy));
            return;
        }
        SetCheckStatus(() => L.Get("updates.checking"));
        var r = await _updates.CheckAsync();
        SetCheckStatus(r.Status switch
        {
            UpdateCheckStatus.UpToDate  => () => L.Get("updates.up_to_date"),
            UpdateCheckStatus.Available => () => L.Format("updates.new_version", r.Version),
            _                           => () => L.Get("updates.check_failed"),
        });
        Apply(r);
    }

    private void SetCheckStatus(Func<string> text)
    {
        _checkStatus = text;
        OnPropertyChanged(nameof(CheckStatus));
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
                try
                {
                    _prepareRestart();
                    _updates.ApplyAndRestart();
                }
                catch
                {
                    // No se pudo aplicar: mejor seguir con la versión actual que cerrar la app.
                    FailBackToAvailable();
                }
                return;

            case UpdateBannerState.Available:
                Progress = 0;
                State = UpdateBannerState.Downloading;
                // Si la descarga se queda colgada (wifi caída, portátil dormido) sin dar
                // error, se corta tras un rato sin progreso y se ofrece reintentar.
                using (var stall = new CancellationTokenSource(_downloadStall))
                {
                    try
                    {
                        await _updates.DownloadAsync(p =>
                        {
                            try { stall.CancelAfter(_downloadStall); } catch (ObjectDisposedException) { }
                            Dispatcher.UIThread.Post(() => Progress = p);
                        }, stall.Token);
                        Progress = 100;
                        State = UpdateBannerState.Ready;
                    }
                    catch
                    {
                        FailBackToAvailable();
                    }
                }
                return;
        }
    }

    private void FailBackToAvailable()
    {
        _retry = true;
        State = UpdateBannerState.Available;
        OnPropertyChanged(nameof(PrimaryText));
    }
}
