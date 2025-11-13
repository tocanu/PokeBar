using System;
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
                _state = JsonSerializer.Deserialize<GameState>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new GameState();
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

    public void Save()
    {
        if (_state == null) return;
        Directory.CreateDirectory(_dir);
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}

