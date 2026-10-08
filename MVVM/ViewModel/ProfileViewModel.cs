using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Perfil en forma de tarjeta para compartir: foto, nombre, horas de lectura,
/// gráfica semanal (estilo Strava), manga favorito y las 3 últimas colecciones leídas.
/// La vista la puede exportar a PNG ("Guardar imagen").
/// </summary>
public class ProfileViewModel : BaseViewModel
{
    public const int Weeks = 12;
    private const double MaxBarHeight = 120;

    private readonly ProfileService     _profile;
    private readonly LibraryService     _library;
    private readonly ICoverService      _cover;
    private readonly IDialogService     _dialog;
    private readonly INavigationService _nav;
    private readonly IFilePickerService _files;

    public ProfileViewModel(
        ProfileService profile, LibraryService library, ICoverService cover,
        IDialogService dialog, INavigationService nav, IFilePickerService files)
    {
        _profile = profile;
        _library = library;
        _cover   = cover;
        _dialog  = dialog;
        _nav     = nav;
        _files   = files;
        Load();
    }

    // ── Cabecera ────────────────────────────────────────────────────────────
    public string  DisplayName     { get; private set; } = "";
    public bool    HasName         { get; private set; }
    public string  Initial         { get; private set; } = "?";
    public Bitmap? Avatar          { get; private set; }
    public bool    HasAvatar       => Avatar is not null;
    public string  MemberSinceText { get; private set; } = "";

    // ── Números ─────────────────────────────────────────────────────────────
    public string ReadingTimeValue { get; private set; } = "0";
    public string ReadingTimeUnit  { get; private set; } = "h";
    public string PagesText        { get; private set; } = "0";
    public string FinishedText     { get; private set; } = "0";
    public string StreakText       { get; private set; } = "";
    public bool   HasStreak        { get; private set; }

    // ── Gráfica ─────────────────────────────────────────────────────────────
    public ObservableCollection<WeekBarViewModel> WeekBars { get; } = [];
    public string ThisWeekText   { get; private set; } = "";
    public string FirstWeekLabel { get; private set; } = "";
    public bool   HasReadingLog  { get; private set; }

    // ── Mangas ──────────────────────────────────────────────────────────────
    public MangaCardViewModel? FavoriteManga { get; private set; }
    public bool HasFavoriteManga => FavoriteManga is not null;
    /// <summary>"Leyendo ahora": las 3 colecciones leídas más recientemente.</summary>
    public ObservableCollection<RecentCollectionCardViewModel> RecentCollections { get; } = [];
    public bool HasRecentCollections => RecentCollections.Count > 0;

    private void Load()
    {
        var p = _profile.GetProfile();
        HasName     = !string.IsNullOrWhiteSpace(p.Name);
        DisplayName = HasName ? p.Name : L.Get("profile.your_name");
        Initial     = HasName ? p.Name.Trim()[..1].ToUpper(L.Culture) : "?";
        Avatar      = BitmapHelper.TryLoad(p.AvatarPath);
        MemberSinceText = p.MemberSince is { } since
            ? L.Format("profile.since", since)
            : "";

        var time = _profile.GetTotalReadingTime();
        var c    = L.Culture;
        (ReadingTimeValue, ReadingTimeUnit) = time.TotalHours >= 1
            ? (time.TotalHours < 10 ? time.TotalHours.ToString("0.#", c) : time.TotalHours.ToString("N0", c), L.Get("profile.unit_hours"))
            : (((int)time.TotalMinutes).ToString(c), L.Get("profile.unit_minutes"));
        PagesText    = _profile.GetTotalPagesRead().ToString("N0", c);
        FinishedText = _profile.GetFinishedCount().ToString("N0", c);

        var streak = _profile.GetStreakDays();
        HasStreak  = streak > 0;
        StreakText = streak == 1 ? L.Get("profile.streak_one") : L.Format("profile.streak_many", streak);

        // Barras: altura relativa a la mejor semana del periodo.
        var weeks = _profile.GetWeeklyReading(Weeks);
        var best  = Math.Max(weeks.Max(w => w.Hours), 0.0001);
        WeekBars.Clear();
        for (var i = 0; i < weeks.Count; i++)
        {
            var (start, hours, pages) = weeks[i];
            WeekBars.Add(new WeekBarViewModel(
                hours > 0 ? Math.Max(4, hours / best * MaxBarHeight) : 3,
                hours > 0,
                i == weeks.Count - 1,
                L.Format("profile.week_tooltip", start, FormatDuration(hours), pages)));
        }
        HasReadingLog  = weeks.Any(w => w.Hours > 0);
        FirstWeekLabel = L.Format("profile.week_label", weeks[0].WeekStart).TrimEnd('.');
        var (_, thisHours, thisPages) = weeks[^1];
        ThisWeekText = L.Format("profile.this_week_summary", FormatDuration(thisHours), thisPages);

        var fav = _profile.GetFavoriteManga();
        FavoriteManga = fav is null ? null : new MangaCardViewModel(fav, _library.GetProgress(fav.Id));
        if (FavoriteManga is not null) _ = FavoriteManga.LoadCoverAsync(_cover);

        RecentCollections.Clear();
        foreach (var r in _profile.GetRecentCollections(3))
        {
            var card = new RecentCollectionCardViewModel(r);
            RecentCollections.Add(card);
            _ = card.LoadCoverAsync(_cover);
        }

        OnPropertyChanged(string.Empty); // todo cambió
    }

    private static string FormatDuration(double hours)
    {
        var t = TimeSpan.FromHours(hours);
        if (t.TotalHours >= 1) return L.Format("profile.duration_hours", (int)t.TotalHours, t.Minutes);
        return L.Format("profile.duration_minutes", (int)t.TotalMinutes);
    }

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    public RelayCommand EditProfileCommand => new(() =>
        _dialog.ShowModal(new EditProfileViewModel(_profile, _library, _dialog, _files, Load)));

    public RelayCommand<MangaCardViewModel> OpenMangaCommand => new(card =>
    {
        if (card is null) return;
        var progress  = _library.GetProgress(card.Model.Id);
        int startPage = Math.Max(0, (progress?.CurrentPage ?? 1) - 1);
        _nav.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(card.Model, startPage));
    });

    public RelayCommand<RecentCollectionCardViewModel> OpenCollectionCommand => new(card =>
    {
        if (card is not null) _nav.NavigateTo<CollectionDetailViewModel>(card.CollectionId);
    });

    /// <summary>Ruta donde guardar la tarjeta en PNG (la imagen la genera la vista).</summary>
    public Task<string?> PickImagePathAsync(string orientation)
        => _files.SaveFileAsync(L.Get("profile.save_dialog_title"),
                                $"hakufu-{(HasName ? DisplayName : L.Get("profile.file_default_name"))}-{orientation}.png",
                                FileFilter.Png);
}

/// <summary>Una colección de "Leyendo ahora": portada del último tomo leído y progreso de toda la colección.</summary>
public class RecentCollectionCardViewModel : BaseViewModel
{
    private readonly Manga _lastVolume;

    public RecentCollectionCardViewModel(RecentCollection r)
    {
        _lastVolume  = r.LastVolume;
        CollectionId = r.Collection.Id;
        Title        = LibraryService.DisplayName(r.Collection);
        ProgressPct  = r.ProgressPct;
    }

    public Guid   CollectionId { get; }
    public string Title        { get; }
    public double ProgressPct  { get; }

    private Bitmap? _cover;
    public Bitmap? Cover { get => _cover; private set => SetProperty(ref _cover, value); }

    public async Task LoadCoverAsync(ICoverService coverService)
        => Cover = await coverService.GetCoverAsync(_lastVolume);
}

/// <summary>Una barra de la gráfica semanal.</summary>
public record WeekBarViewModel(double Height, bool HasReading, bool IsCurrent, string Tooltip);
