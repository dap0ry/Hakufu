using System.IO;
using System.Windows.Forms;

namespace Hakufu.Services;

public class TrayIconService : ITrayIconService
{
    private NotifyIcon? _icon;

    public void Initialize(Action onRestore, Action onExit)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "HakufuLogo.ico");
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Hakufu", null, (_, _) => onRestore());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => onExit());

        _icon = new NotifyIcon
        {
            Icon    = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application,
            Text    = "Hakufu",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => onRestore();
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
        _icon = null;
    }
}
