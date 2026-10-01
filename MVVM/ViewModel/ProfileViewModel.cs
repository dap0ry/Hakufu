using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Perfil en forma de tarjeta para compartir: foto, nombre, horas de lectura,
/// gráfica semanal (estilo Strava), manga favorito y los 3 últimos abiertos.
/// La vista la puede exportar a PNG ("Guardar imagen").
/// </summary>
public class ProfileViewModel : BaseViewModel
{
    public const int Weeks = 12;
    private const double MaxBarHeight = 120;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

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
    public ObservableCollection<MangaCardViewModel> RecentMangas { get; } = [];
    public bool HasRecentMangas => RecentMangas.Count > 0;

    private void Load()
    {
        var p = _profile.GetProfile();
        HasName     = !string.IsNullOrWhiteSpace(p.Name);
        DisplayName = HasName ? p.Name : "Tu nombre";
        Initial     = HasName ? p.Name.Trim()[..1].ToUpper(Es) : "?";
        Avatar      = BitmapHelper.TryLoad(p.AvatarPath);
        MemberSinceText = p.MemberSince is { } since
            ? $"En Hakufu desde {since.ToString("MMMM 'de' yyyy", Es)}"
            : "";

        var time = _profile.GetTotalReadingTime();
        (ReadingTimeValue, ReadingTimeUnit) = time.TotalHours >= 1
            ? (time.TotalHours < 10 ? time.TotalHours.ToString("0.#", Es) : time.TotalHours.ToString("N0", Es), "h")
            : (((int)time.TotalMinutes).ToString(Es), "min");
        PagesText    = _profile.GetTotalPagesRead().ToString("N0", Es);
        FinishedText = _profile.GetFinishedCount().ToString("N0", Es);

        var streak = _profile.GetStreakDays();
        HasStreak  = streak > 0;
        StreakText = streak == 1 ? "1 día seguido leyendo" : $"{streak} días seguidos leyendo";

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
                $"Semana del {start.ToString("d 'de' MMMM", Es)}: {FormatDuration(hours)} · {pages} págs."));
        }
        HasReadingLog  = weeks.Any(w => w.Hours > 0);
        FirstWeekLabel = weeks[0].WeekStart.ToString("d MMM", Es).TrimEnd('.');
        var (_, thisHours, thisPages) = weeks[^1];
        ThisWeekText = $"Esta semana: {FormatDuration(thisHours)} · {thisPages} págs.";

        var fav = _profile.GetFavoriteManga();
        FavoriteManga = fav is null ? null : new MangaCardViewModel(fav, _library.GetProgress(fav.Id));
        if (FavoriteManga is not null) _ = FavoriteManga.LoadCoverAsync(_cover);

        RecentMangas.Clear();
        foreach (var m in _profile.GetRecentMangas(3))
        {
            var card = new MangaCardViewModel(m, _library.GetProgress(m.Id));
            RecentMangas.Add(card);
            _ = card.LoadCoverAsync(_cover);
        }

        OnPropertyChanged(string.Empty); // todo cambió
    }

    private static string FormatDuration(double hours)
    {
        var t = TimeSpan.FromHours(hours);
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} h {t.Minutes:D2} min";
        return $"{(int)t.TotalMinutes} min";
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

    /// <summary>Ruta donde guardar la tarjeta en PNG (la imagen la genera la vista).</summary>
    public Task<string?> PickImagePathAsync(string orientation)
        => _files.SaveFileAsync("Guardar perfil como imagen",
                                $"hakufu-{(HasName ? DisplayName : "perfil")}-{orientation}.png", FileFilter.Png);
}

/// <summary>Una barra de la gráfica semanal.</summary>
public record WeekBarViewModel(double Height, bool HasReading, bool IsCurrent, string Tooltip);
