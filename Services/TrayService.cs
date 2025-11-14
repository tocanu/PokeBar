using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using PokeBar.Models;
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
        
        // PokéCenter
        menu.Items.Add("PokéCenter (Curar)", null, (_, __) => _vm.HealAll());
        
        // PokéMart
        menu.Items.Add("PokéMart (Comprar)", null, (_, __) => _vm.OpenShop());
        
        // PC
        menu.Items.Add("PC (Organizar)", null, (_, __) => _vm.ManagePC());
        
        menu.Items.Add(new ToolStripSeparator());
        
        // Configurações
        var invert = new ToolStripMenuItem("Inverter transição multimonitor") { CheckOnClick = true, Checked = _vm.ReverseMonitorWalk };
        invert.CheckedChanged += (_, __) => _vm.ReverseMonitorWalk = invert.Checked;
        menu.Items.Add(invert);
        
        var showBubbles = new ToolStripMenuItem("Mostrar diálogos") { CheckOnClick = true, Checked = _vm.ShowDialogBubbles };
        showBubbles.CheckedChanged += (_, __) => _vm.ShowDialogBubbles = showBubbles.Checked;
        menu.Items.Add(showBubbles);
        
        var godModeItem = new ToolStripMenuItem("⚡ God Mode (Invencível)") { CheckOnClick = true, Checked = _vm.GodMode };
        godModeItem.CheckedChanged += (_, __) => _vm.GodMode = godModeItem.Checked;
        menu.Items.Add(godModeItem);
        
        menu.Items.Add(new ToolStripSeparator());
        
        // Seleção de Pokébola
        var ballMenu = new ToolStripMenuItem("🎯 Selecionar Pokébola");
        
        var pokeball = new ToolStripMenuItem($"⚪ {BallInfo.GetName(BallType.PokeBall)} (1x)") { CheckOnClick = true, Checked = _vm.SelectedBall == BallType.PokeBall };
        pokeball.Click += (_, __) => { _vm.SelectedBall = BallType.PokeBall; UpdateBallSelection(ballMenu); };
        ballMenu.DropDownItems.Add(pokeball);
        
        var greatball = new ToolStripMenuItem($"🔵 {BallInfo.GetName(BallType.GreatBall)} (1.5x)") { CheckOnClick = true, Checked = _vm.SelectedBall == BallType.GreatBall };
        greatball.Click += (_, __) => { _vm.SelectedBall = BallType.GreatBall; UpdateBallSelection(ballMenu); };
        ballMenu.DropDownItems.Add(greatball);
        
        var ultraball = new ToolStripMenuItem($"⚫ {BallInfo.GetName(BallType.UltraBall)} (2x)") { CheckOnClick = true, Checked = _vm.SelectedBall == BallType.UltraBall };
        ultraball.Click += (_, __) => { _vm.SelectedBall = BallType.UltraBall; UpdateBallSelection(ballMenu); };
        ballMenu.DropDownItems.Add(ultraball);
        
        var netball = new ToolStripMenuItem($"🌊 {BallInfo.GetName(BallType.NetBall)} (3x água/inseto)") { CheckOnClick = true, Checked = _vm.SelectedBall == BallType.NetBall };
        netball.Click += (_, __) => { _vm.SelectedBall = BallType.NetBall; UpdateBallSelection(ballMenu); };
        ballMenu.DropDownItems.Add(netball);
        
        var quickball = new ToolStripMenuItem($"⚡ {BallInfo.GetName(BallType.QuickBall)} (5x 1º turno)") { CheckOnClick = true, Checked = _vm.SelectedBall == BallType.QuickBall };
        quickball.Click += (_, __) => { _vm.SelectedBall = BallType.QuickBall; UpdateBallSelection(ballMenu); };
        ballMenu.DropDownItems.Add(quickball);
        
        menu.Items.Add(ballMenu);
        
        menu.Items.Add(new ToolStripSeparator());
        
        // Debug/Utilidades
        var godMode = new ToolStripMenuItem("⚡ God Mode") { CheckOnClick = true, Checked = _vm.GodMode };
        godMode.Click += (_, __) => _vm.GodMode = godMode.Checked;
        menu.Items.Add(godMode);
        
        var infiniteBalls = new ToolStripMenuItem("♾️ Pokébolas Infinitas") { CheckOnClick = true, Checked = _vm.InfinitePokeballs };
        infiniteBalls.Click += (_, __) => _vm.InfinitePokeballs = infiniteBalls.Checked;
        menu.Items.Add(infiniteBalls);
        
        menu.Items.Add(new ToolStripSeparator());
        
        menu.Items.Add("🎯 Ativar Modo Captura Manual", null, (_, __) => _vm.ActivateManualCaptureMode());
        menu.Items.Add("🎮 Spawnar Inimigo Aleatório", null, (_, __) => _vm.SpawnRandomEnemy());
        menu.Items.Add("Ver Mapa de Posições", null, (_, __) => System.Windows.MessageBox.Show(_vm.GetPositionDebugInfo(), "Mapa de Posições", System.Windows.MessageBoxButton.OK));
        menu.Items.Add("Mostrar/Ocultar", null, (_, __) => _vm.ToggleVisibility());
        menu.Items.Add("Salvar Agora", null, (_, __) => _vm.SaveNow());
        
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, __) => System.Windows.Application.Current.Shutdown());

        _icon.ContextMenuStrip = menu;
    }

    private void UpdateBallSelection(ToolStripMenuItem ballMenu)
    {
        foreach (ToolStripMenuItem item in ballMenu.DropDownItems)
        {
            item.Checked = false;
        }
        
        // Marcar apenas a selecionada
        foreach (ToolStripMenuItem item in ballMenu.DropDownItems)
        {
            if (item.Text?.Contains(BallInfo.GetName(_vm.SelectedBall)) == true)
            {
                item.Checked = true;
                break;
            }
        }
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
