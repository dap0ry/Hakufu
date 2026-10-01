using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Perfil tipo red social: registro de lectura, gráfica semanal, datos y tarjeta exportable.</summary>
public class ProfileTests
{
    private static async Task<(TempDataDir tmp, JsonDataRepository repo, ProfileService profile)> NewProfile()
    {
        var tmp = new TempDataDir();
        var repo = new JsonDataRepository();
        await repo.LoadAsync();
        return (tmp, repo, new ProfileService(repo));
    }

    [Fact]
    public async Task Reading_is_logged_per_day_and_grouped_by_week()
    {
        var (tmp, repo, profile) = await NewProfile();
        using var _ = tmp;

        profile.LogReading(TimeSpan.FromMinutes(30), 20);
        profile.LogReading(TimeSpan.FromMinutes(15), 5);
        var today = Assert.Single(repo.Current.ReadingLog);
        Assert.Equal(45 * 60, today.Seconds);
        Assert.Equal(25, today.Pages);

        // Jueves 1/10/2026: su semana empieza el lunes 28/9.
        var thu = new DateOnly(2026, 10, 1);
        repo.Current.ReadingLog.Clear();
        repo.Current.ReadingLog.Add(new ReadingDay { Date = new DateOnly(2026, 9, 28), Seconds = 3600, Pages = 10 });
        repo.Current.ReadingLog.Add(new ReadingDay { Date = new DateOnly(2026, 9, 27), Seconds = 1800, Pages = 4 }); // domingo: semana anterior
        var weeks = profile.GetWeeklyReading(2, thu);
        Assert.Equal(new DateOnly(2026, 9, 21), weeks[0].WeekStart);
        Assert.Equal(0.5, weeks[0].Hours, 3);
        Assert.Equal(new DateOnly(2026, 9, 28), weeks[1].WeekStart);
        Assert.Equal(1.0, weeks[1].Hours, 3);
        Assert.Equal(10, weeks[1].Pages);
    }

    [Fact]
    public async Task Streak_counts_consecutive_days_up_to_today_or_yesterday()
    {
        var (tmp, repo, profile) = await NewProfile();
        using var _ = tmp;
        var today = new DateOnly(2026, 10, 1);
        foreach (var back in new[] { 1, 2, 3, 5 })
            repo.Current.ReadingLog.Add(new ReadingDay { Date = today.AddDays(-back), Seconds = 600 });

        Assert.Equal(3, profile.GetStreakDays(today));       // hoy aún no: cuenta desde ayer
        repo.Current.ReadingLog.Add(new ReadingDay { Date = today, Pages = 3 });
        Assert.Equal(4, profile.GetStreakDays(today));
    }

    [Fact]
    public async Task Updating_the_profile_copies_the_photo_and_replaces_the_old_one()
    {
        var (tmp, repo, profile) = await NewProfile();
        using var _ = tmp;
        var photo1 = Path.Combine(tmp.Root, "yo.png");
        var photo2 = Path.Combine(tmp.Root, "yo2.png");
        await File.WriteAllBytesAsync(photo1, Fixtures.TinyPng);
        await File.WriteAllBytesAsync(photo2, Fixtures.TinyPng);
        var manga = new Manga { Title = "Kurogane 1" };
        repo.Current.Mangas.Add(manga);

        await profile.UpdateProfileAsync("  Dani  ", manga.Id, photo1, removeAvatar: false);
        var first = repo.Current.Profile.AvatarPath;
        Assert.Equal("Dani", repo.Current.Profile.Name);
        Assert.StartsWith(AppPaths.ProfileDir, first);
        Assert.True(File.Exists(first));
        Assert.Same(manga, profile.GetFavoriteManga());

        await Task.Delay(1100); // el nombre de la copia lleva la hora
        await profile.UpdateProfileAsync("Dani", manga.Id, photo2, removeAvatar: false);
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(repo.Current.Profile.AvatarPath));

        await profile.UpdateProfileAsync("Dani", null, null, removeAvatar: true);
        Assert.Equal("", repo.Current.Profile.AvatarPath);
        Assert.True(File.Exists(photo1)); // el original no se toca

        // Una copia de seguridad ajena podría apuntar la foto a cualquier archivo:
        // cambiar o quitar la foto nunca debe borrar nada fuera de la carpeta de perfil.
        var outsider = Path.Combine(tmp.Root, "importante.txt");
        await File.WriteAllTextAsync(outsider, "no me borres");
        repo.Current.Profile.AvatarPath = outsider;
        await profile.UpdateProfileAsync("Dani", null, null, removeAvatar: true);
        Assert.True(File.Exists(outsider));
    }

    [AvaloniaFact]
    public void Turning_pages_in_the_reader_logs_reading()
    {
        using var app = ViewSmoke.Start();
        app.Root.Navigation.NavigateTo<ReaderViewModel>(new ReaderNavigationParam(app.SampleManga, 0));
        var vm = Assert.IsType<ReaderViewModel>(app.Root.Navigation.CurrentViewModel);
        app.Pump();

        vm.NextPageCommand.Execute(null);
        vm.NextPageCommand.Execute(null);
        vm.PrevPageCommand.Execute(null); // volver atrás no suma páginas
        app.Root.Navigation.NavigateTo<HomeViewModel>(); // salir apunta el tiempo
        app.Pump();

        var day = Assert.Single(app.Root.Repo.Current.ReadingLog);
        Assert.Equal(2, day.Pages);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), day.Date);
    }

    [AvaloniaFact]
    public void Edit_profile_modal_saves_and_the_card_updates()
    {
        using var app = ViewSmoke.Start(darkTheme: true);
        app.Root.Navigation.NavigateTo<ProfileViewModel>();
        app.AssertShows<ProfileView>();
        var profileVm = Assert.IsType<ProfileViewModel>(app.Root.Navigation.CurrentViewModel);

        profileVm.EditProfileCommand.Execute(null);
        app.AssertShows<EditProfileView>();
        var edit = Assert.IsType<EditProfileViewModel>(
            app.Window.GetVisualDescendants().OfType<EditProfileView>().Single().DataContext);
        edit.Name = "Dani";
        edit.Favorite = edit.FavoriteOptions.Single(o => o.Id == app.SampleManga.Id);
        edit.SaveCommand.Execute(null);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (profileVm.DisplayName != "Dani" && sw.ElapsedMilliseconds < 3000) app.Pump();
        Assert.Equal("Dani", profileVm.DisplayName);
        Assert.Equal("D", profileVm.Initial);
        Assert.Equal(app.SampleManga.Id, profileVm.FavoriteManga?.Model.Id);
    }

    [AvaloniaFact]
    public void The_profile_card_exports_horizontal_and_vertical()
    {
        using var app = ViewSmoke.Start(darkTheme: false);
        app.Root.Navigation.NavigateTo<ProfileViewModel>();
        var view = app.AssertShows<ProfileView>();

        var horizontal = view.GetVisualDescendants().OfType<LayoutTransformControl>().Single(c => c.Name == "ExportHorizontal");
        var vertical   = view.GetVisualDescendants().OfType<LayoutTransformControl>().Single(c => c.Name == "ExportVertical");
        // En la app se ve una horizontal (dentro del Viewbox) y además está la copia de exportar.
        Assert.Equal(2, view.GetVisualDescendants().OfType<ProfileCardHorizontal>().Count());

        using var h = ProfileView.RenderCard(horizontal);
        using var v = ProfileView.RenderCard(vertical);
        Assert.True(h.PixelSize.Width > h.PixelSize.Height, "La horizontal tiene que ser apaisada.");
        Assert.True(v.PixelSize.Height > v.PixelSize.Width, "La vertical tiene que ser alargada.");
        foreach (var bmp in new[] { h, v })
        {
            using var ms = new MemoryStream();
            bmp.Save(ms);
            Assert.True(ms.Length > 1000);
        }
    }
}
