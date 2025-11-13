using System;
using System.Linq;
using PokeBar.Models;

namespace PokeBar.Services;

public class BattleService
{
    private readonly GameState _state;
    private readonly Random _rng = new();
    private System.Timers.Timer? _spawnTimer;
    private System.Timers.Timer? _resolveTimer;
    private Pokemon? _activeWild;

    public BattleService(GameState state)
    {
        _state = state;
    }

    public event EventHandler<string>? Notify;
    public event EventHandler<Pokemon>? BattleStarted;
    public event EventHandler<(Pokemon wild, bool won, bool caught, int money)>? BattleFinished;

    public void Start()
    {
        _spawnTimer = new System.Timers.Timer(RandomDelay());
        _spawnTimer.Elapsed += (_, __) => OnSpawn();
        _spawnTimer.AutoReset = true;
        _spawnTimer.Start();
    }

    private double RandomDelay() => _rng.Next(30, 91) * 1000; // 30-90s

    private void OnSpawn()
    {
        if (_activeWild != null)
            return;

        var wild = CreateCharizard();
        _activeWild = wild;
        var msg = $"Um {wild.Name} selvagem apareceu!";
        Notify?.Invoke(this, msg);
        BattleStarted?.Invoke(this, wild.Clone());
        ScheduleResolve();
    }

    private void ScheduleResolve()
    {
        _resolveTimer?.Stop();
        _resolveTimer?.Dispose();
        _resolveTimer = new System.Timers.Timer(5500);
        _resolveTimer.AutoReset = false;
        _resolveTimer.Elapsed += (_, __) => ResolvePendingBattle();
        _resolveTimer.Start();
    }

    private void ResolvePendingBattle()
    {
        var wild = _activeWild;
        if (wild == null)
            return;

        var player = (_state.Active ?? _state.Party.FirstOrDefault())?.Clone() ?? new Pokemon();
        player.HealFull();
        var result = ResolveBattle(player, wild.Clone());
        CompleteBattle(wild, result.won, result.caught, result.money);
    }

    private (bool won, bool caught, int money) ResolveBattle(Pokemon player, Pokemon wild)
    {
        bool playerFirst = player.Speed >= wild.Speed;
        int turns = 0;

        while (player.CurrentHP > 0 && wild.CurrentHP > 0 && turns < 200)
        {
            if (playerFirst)
            {
                Attack(player, wild);
                if (wild.CurrentHP <= 0) break;
                Attack(wild, player);
            }
            else
            {
                Attack(wild, player);
                if (player.CurrentHP <= 0) break;
                Attack(player, wild);
            }
            turns++;
            playerFirst = !playerFirst;
        }

        bool won = player.CurrentHP > 0 && wild.CurrentHP <= 0;
        bool caught = false;
        int money = 0;

        if (won)
        {
            money = _rng.Next(12, 40);
            caught = _rng.NextDouble() < 0.25;
        }
        else if (player.CurrentHP > 0 && wild.CurrentHP < wild.MaxHP / 3)
        {
            caught = _rng.NextDouble() < 0.4;
        }

        return (won, caught, money);
    }

    private void Attack(Pokemon attacker, Pokemon defender)
    {
        int baseDamage = Math.Max(1, attacker.Attack - Math.Max(1, defender.Defense / 2));
        double variance = 0.85 + _rng.NextDouble() * 0.3; // 0.85 - 1.15
        int dmg = Math.Max(1, (int)(baseDamage * variance));
        defender.CurrentHP = Math.Max(0, defender.CurrentHP - dmg);
    }

    public bool TryManualCapture()
    {
        var wild = _activeWild;
        if (wild == null)
            return false;

        double chance = 0.45;
        var player = _state.Active;
        if (player != null)
        {
            chance += Math.Clamp((player.Level - wild.Level) * 0.02, -0.2, 0.2);
        }
        chance = Math.Clamp(chance, 0.15, 0.85);
        bool success = _rng.NextDouble() < chance;
        if (success)
        {
            CompleteBattle(wild, won: true, caught: true, money: _rng.Next(6, 14));
        }
        else
        {
            Notify?.Invoke(this, $"{wild.Name} escapou da Pokébola!");
        }
        return success;
    }

    private void CompleteBattle(Pokemon wild, bool won, bool caught, int money)
    {
        _activeWild = null;
        _resolveTimer?.Stop();
        _resolveTimer?.Dispose();
        _resolveTimer = null;
        if (caught)
        {
            wild.HealFull();
        }
        BattleFinished?.Invoke(this, (wild, won, caught, money));
    }

    private Pokemon CreateCharizard()
    {
        int level = _rng.Next(20, 41);
        int hp = 120 + _rng.Next(-10, 21);
        int attack = 95 + _rng.Next(-5, 15);
        int defense = 80 + _rng.Next(-10, 10);
        int speed = 100 + _rng.Next(-10, 10);

        hp = Math.Max(1, hp);
        attack = Math.Max(1, attack);
        defense = Math.Max(1, defense);
        speed = Math.Max(1, speed);

        return new Pokemon
        {
            Name = "Charizard",
            DexNumber = 6,
            Level = level,
            MaxHP = hp,
            CurrentHP = hp,
            Attack = attack,
            Defense = defense,
            Speed = speed,
        };
    }
}
