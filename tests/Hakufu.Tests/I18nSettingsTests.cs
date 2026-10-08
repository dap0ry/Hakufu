using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Hakufu.I18n;
using Hakufu.MVVM.View;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>Ajustes y atajos de teclado en inglés.</summary>
public class I18nSettingsTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Settings_and_shortcuts_show_in_english()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<SettingsViewModel>();
            var view = app.AssertShows<SettingsView>();
            var vm = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);

            Assert.Equal($"Version {AppVersion.Current}", vm.VersionText);
            Assert.Contains(vm.Shortcuts, r => r.Label == "Close the reader");
            var next = vm.Shortcuts.Single(r => r.Action.Id == "next");
            Assert.Equal("Space", next.Slots[1].Text);
            next.Slots[0].ClickCommand.Execute(null);
            Assert.Equal("Press a key…", next.Slots[0].Text);
            Assert.Equal("Shift + Page Down", ShortcutService.Display(new KeyGesture(Key.PageDown, KeyModifiers.Shift)));

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Dark theme", texts);
            Assert.Contains("Keyboard shortcuts", texts);
            Assert.Contains("Disk space used", texts);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void Shortcut_conflict_notice_is_in_english()
    {
        try
        {
            Localizer.Instance.SetLanguage("en");
            using var app = ViewSmoke.Start();
            app.Root.Navigation.NavigateTo<SettingsViewModel>();
            var vm = Assert.IsType<SettingsViewModel>(app.Root.Navigation.CurrentViewModel);

            // Space (de "Página siguiente") pasa a "Página anterior".
            var prev = vm.Shortcuts.Single(r => r.Action.Id == "prev");
            prev.Slots[1].ClickCommand.Execute(null);
            Assert.True(vm.AssignCapturedKey(new KeyGesture(Key.Space)));
            Assert.Equal("Space no longer does “Next page”.", vm.ShortcutNotice);
        }
        finally { Localizer.Instance.SetLanguage("es"); }
    }
}
