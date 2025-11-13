using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using PokeBar.ViewModels;

namespace PokeBar.Services;

public class TrayService : IDisposable
{
    private readonly MainViewModel _vm;
    private NotifyIcon? _icon;

    public TrayService(MainViewModel vm)
    {
        _vm = vm;
    }

    public void Initialize()
    {
        _icon = new NotifyIcon
        {
            Text = "PokéBar",
            Visible = true,
            Icon = SystemIcons.Application
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("PokéCenter (Curar)", null, (_, __) => _vm.HealAll());
        menu.Items.Add("PokéMart (Comprar)", null, (_, __) => _vm.OpenShop());
        menu.Items.Add("PC (Organizar)", null, (_, __) => _vm.ManagePC());
        var invert = new ToolStripMenuItem("Inverter transição multimonitor") { CheckOnClick = true, Checked = _vm.ReverseMonitorWalk };
        invert.CheckedChanged += (_, __) => _vm.ReverseMonitorWalk = invert.Checked;
        menu.Items.Add(invert);
        // Ajuste fino de altura
        var heightMenu = new ToolStripMenuItem("Ajuste Fino de Altura");
        heightMenu.DropDownItems.Add("-1 px", null, (_, __) => _vm.HeightOffsetPixels -= 1);
        heightMenu.DropDownItems.Add("+1 px", null, (_, __) => _vm.HeightOffsetPixels += 1);
        heightMenu.DropDownItems.Add("Zerar (0 px)", null, (_, __) => _vm.HeightOffsetPixels = 0);
        menu.Items.Add(heightMenu);
        menu.Items.Add("Mostrar/Ocultar", null, (_, __) => _vm.ToggleVisibility());
        menu.Items.Add("Salvar Agora", null, (_, __) => _vm.SaveNow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, __) => System.Windows.Application.Current.Shutdown());

        _icon.ContextMenuStrip = menu;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        _icon?.ShowBalloonTip(timeout, title, text, icon);
    }

    public void Dispose()
    {
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
