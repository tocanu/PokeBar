using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PokeBar.Models;

namespace PokeBar.Services;

public class DexService
{
    private readonly SpriteService _spriteService;
    private readonly List<DexEntry> _entries = new();

    public DexService(SpriteService spriteService)
    {
        _spriteService = spriteService;
        _spriteService.SpriteRootChanged += (_, __) => ReloadEntries();
        ReloadEntries();
    }

    public IReadOnlyList<DexEntry> Entries => _entries;

    public DexEntry? GetEntry(int dexNumber) => _entries.FirstOrDefault(e => e.DexNumber == dexNumber);

    public Pokemon CreateRandomPokemon(Random rng) => _entries.Count == 0
        ? new Pokemon()
        : CreatePokemon(_entries[rng.Next(_entries.Count)], rng);

    public Pokemon CreatePokemon(DexEntry entry, Random rng)
    {
        int level = rng.Next(5, 31);
        int hp = 30 + (entry.DexNumber % 15) + rng.Next(level);
        int attack = 8 + (entry.DexNumber % 12) + rng.Next(0, level / 2 + 4);
        int defense = 6 + ((entry.DexNumber / 3) % 10) + rng.Next(0, level / 3 + 3);
        int speed = 6 + ((entry.DexNumber / 5) % 10) + rng.Next(0, level / 2 + 3);

        hp = Math.Clamp(hp, 20, 180);
        attack = Math.Clamp(attack, 8, 200);
        defense = Math.Clamp(defense, 6, 200);
        speed = Math.Clamp(speed, 6, 200);

        return new Pokemon
        {
            Name = entry.Name,
            DexNumber = entry.DexNumber,
            Level = level,
            MaxHP = hp,
            CurrentHP = hp,
            Attack = attack,
            Defense = defense,
            Speed = speed,
        };
    }

    public void ReloadEntries()
    {
        var list = LoadEntries();
        _entries.Clear();
        _entries.AddRange(list);
    }

    private List<DexEntry> LoadEntries()
    {
        var list = new List<DexEntry>();
        var spriteRoot = _spriteService.SpriteRoot;
        if (!Directory.Exists(spriteRoot))
            return list;

        var trackerNames = LoadTrackerNames(spriteRoot);

        foreach (var dir in Directory.EnumerateDirectories(spriteRoot))
        {
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(folderName))
                continue;
            if (!int.TryParse(folderName.AsSpan(0, Math.Min(4, folderName.Length)), out var dex))
                continue;
            if (dex < 1 || dex > 1025)
                continue;
            if (!HasSpriteAssets(dir))
                continue;

            if (!trackerNames.TryGetValue(folderName, out var name) && !trackerNames.TryGetValue(dex.ToString("D4"), out name))
            {
                name = $"Pokémon #{dex:0000}";
            }
            else
            {
                name = CleanName(name);
            }

            list.Add(new DexEntry(dex, name, folderName));
        }

        list.Sort((a, b) => a.DexNumber.CompareTo(b.DexNumber));
        return list;
    }

    private static string CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Pokémon";
        return value.Replace('_', ' ').Trim();
    }

    private static bool HasSpriteAssets(string dir)
    {
        if (File.Exists(Path.Combine(dir, "Walk-Anim.png")) || File.Exists(Path.Combine(dir, "Idle-Anim.png")))
            return true;
        return Directory.EnumerateFiles(dir, "*.png").Any();
    }

    private static Dictionary<string, string> LoadTrackerNames(string spriteRoot)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var trackerPath = Path.Combine(Directory.GetParent(spriteRoot)?.FullName ?? spriteRoot, "tracker.json");
            if (!File.Exists(trackerPath))
                return dict;

            using var doc = JsonDocument.Parse(File.ReadAllText(trackerPath));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("name", out var nameProp))
                {
                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        dict[prop.Name] = name;
                    }
                }
            }
        }
        catch
        {
            // ignore tracker parsing errors, fallback names will be used
        }
        return dict;
    }
}
