using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PokeBar.Models;

namespace PokeBar.Services;

public class StateService
{
    private readonly string _dir;
    private readonly string _file;
    private GameState? _state;
    public GameState State => _state ??= new GameState();

    // Classe temporária para migração de saves antigos
    private class OldGameState
    {
        public int Money { get; set; }
        public Dictionary<string, int> Inventory { get; set; } = new();
        public BallType SelectedBall { get; set; }
        public List<Pokemon> Party { get; set; } = new();
        public List<Pokemon> Box { get; set; } = new();
        public int ActiveIndex { get; set; }
        public bool ReverseMonitorWalk { get; set; }
        public int HeightOffsetPixels { get; set; }
        public bool ShowDialogBubbles { get; set; }
        public bool GodMode { get; set; }
        public bool InfinitePokeballs { get; set; }
        public bool SingleMonitorMode { get; set; }
        public bool InteractWithTaskbar { get; set; }
    }

    public StateService()
    {
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _dir = Path.Combine(appdata, ".pokebar");
        _file = Path.Combine(_dir, "save.json");
    }

    public GameState Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                
                // Tentar carregar como novo formato (BallType keys)
                try
                {
                    _state = JsonSerializer.Deserialize<GameState>(json, options);
                }
                catch
                {
                    // Se falhar, tentar migrar do formato antigo (string keys)
                    var oldState = JsonSerializer.Deserialize<OldGameState>(json, options);
                    if (oldState != null)
                    {
                        _state = MigrateFromOldFormat(oldState);
                    }
                }
                
                _state ??= new GameState();
            }
        }
        catch { _state = new GameState(); }

        _state ??= new GameState();
        if (_state.Party.Count == 0)
        {
            _state.Party.Add(new Pokemon());
        }
        return _state;
    }

    private GameState MigrateFromOldFormat(OldGameState oldState)
    {
        var newState = new GameState
        {
            Money = oldState.Money,
            Inventory = new Dictionary<BallType, int>(),
            SelectedBall = oldState.SelectedBall,
            Party = oldState.Party,
            Box = oldState.Box,
            ActiveIndex = oldState.ActiveIndex,
            ReverseMonitorWalk = oldState.ReverseMonitorWalk,
            HeightOffsetPixels = oldState.HeightOffsetPixels,
            ShowDialogBubbles = oldState.ShowDialogBubbles,
            GodMode = oldState.GodMode,
            InfinitePokeballs = oldState.InfinitePokeballs,
            SingleMonitorMode = oldState.SingleMonitorMode,
            InteractWithTaskbar = oldState.InteractWithTaskbar
        };

        // Migrar inventário: converter strings para BallType enum
        foreach (var kvp in oldState.Inventory)
        {
            if (Enum.TryParse<BallType>(kvp.Key, true, out var ballType))
            {
                newState.Inventory[ballType] = kvp.Value;
            }
        }

        return newState;
    }

    public void Save()
    {
        if (_state == null) return;
        Directory.CreateDirectory(_dir);
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}

