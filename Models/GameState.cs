using System.Collections.Generic;

namespace PokeBar.Models;

public class GameState
{
    public int Money { get; set; } = 0;
    public Dictionary<string, int> Inventory { get; set; } = new();
    public List<Pokemon> Party { get; set; } = new();
    public List<Pokemon> Box { get; set; } = new();
    public int ActiveIndex { get; set; } = 0;
    public bool ReverseMonitorWalk { get; set; } = false;
    public int HeightOffsetPixels { get; set; } = 0;

    public Pokemon? Active => (ActiveIndex >= 0 && ActiveIndex < Party.Count) ? Party[ActiveIndex] : null;
}
