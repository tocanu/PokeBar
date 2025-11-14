using System.Collections.Generic;

namespace PokeBar.Models;

public class GameState
{
    public int Money { get; set; } = 0;
    public Dictionary<BallType, int> Inventory { get; set; } = new();
    public BallType SelectedBall { get; set; } = BallType.PokeBall; // Pokébola selecionada
    public List<Pokemon> Party { get; set; } = new();
    public List<Pokemon> Box { get; set; } = new();
    public int ActiveIndex { get; set; } = 0;
    public bool ReverseMonitorWalk { get; set; } = false;
    public int HeightOffsetPixels { get; set; } = 0;
    public bool ShowDialogBubbles { get; set; } = true;
    public bool GodMode { get; set; } = false;
    public bool InfinitePokeballs { get; set; } = false;
    public bool SingleMonitorMode { get; set; } = false;
    public bool InteractWithTaskbar { get; set; } = true;
    public string? SpriteRootPath { get; set; } = null; // null = usar padrão

    public Pokemon? Active => (ActiveIndex >= 0 && ActiveIndex < Party.Count) ? Party[ActiveIndex] : null;
}
