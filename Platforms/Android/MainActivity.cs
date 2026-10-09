using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace Hakufu.Platforms.Droid;

/// <summary>
/// Avalonia en Android: la App de siempre con MainView como vista única
/// (ISingleViewApplicationLifetime, ver App.StartSingleView). Girar la pantalla no recrea la
/// actividad (ConfigurationChanges): el lector sigue donde estaba.
/// </summary>
[Activity(Name = "com.dapory.hakufu.MainActivity", Label = "Hakufu", Theme = "@style/HakufuTheme",
          Icon = "@mipmap/ic_launcher", MainLauncher = true, Exported = true,
          ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout |
                                 ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Density |
                                 ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Antes que nada: rutas y comportamiento de Android, para que App ya los vea.
        DroidPlatform.Configure(this);
        // Solo CI y pruebas: abrir una pantalla al arrancar (como HAKUFU_START_SCREEN en iOS).
        if (Intent?.GetStringExtra("start_screen") is { Length: > 0 } screen)
            System.Environment.SetEnvironmentVariable("HAKUFU_START_SCREEN", screen);
        base.OnCreate(savedInstanceState);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont().LogToTrace();
}
