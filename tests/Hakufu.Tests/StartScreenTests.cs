using Avalonia.Headless.XUnit;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>HAKUFU_START_SCREEN: el CI abre cada pantalla en el simulador de iOS para capturarla.</summary>
public class StartScreenTests
{
    [AvaloniaTheory]
    [InlineData("library",    typeof(LibraryViewModel))]
    [InlineData("collection", typeof(CollectionDetailViewModel))]
    [InlineData("reader",     typeof(ReaderViewModel))]
    [InlineData("settings",   typeof(SettingsViewModel))]
    [InlineData("profile",    typeof(ProfileViewModel))]
    [InlineData("",           typeof(HomeViewModel))]
    [InlineData("nada",       typeof(HomeViewModel))]
    public void Opens_the_requested_screen(string screen, Type expected)
    {
        using var app = ViewSmoke.StartMobile(390, 844);

        StartScreen.Apply(app.Root, screen);
        app.Pump();

        Assert.IsType(expected, app.Root.Navigation.CurrentViewModel);
    }
}
