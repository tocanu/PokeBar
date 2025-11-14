using System;
using System.Linq;
using System.Windows.Threading;
using PokeBar.Models;

namespace PokeBar.Services;

public class BattleService
{
    private readonly GameState _state;
    private readonly DexService _dexService;
    private readonly Random _rng;
    private DispatcherTimer? _spawnTimer;
    private DispatcherTimer? _resolveTimer;
    private DispatcherTimer? _manualTimer;
    private Pokemon? _activeWild;
    private bool _awaitingManualCapture;
    private int _pendingManualMoney;
    private bool _battleTriggered;

#if DEBUG
    private const bool LOGGING_ENABLED = true;
#else
    private const bool LOGGING_ENABLED = false;
#endif

    public BattleService(GameState state, DexService dexService, Random? rng = null)
    {
        _state = state;
        _dexService = dexService;
        _rng = rng ?? new Random();
    }

    public event EventHandler<string>? Notify;
    public event EventHandler<Pokemon>? BattleStarted;
    public event EventHandler<(Pokemon wild, bool won, bool caught, int money)>? BattleFinished;
    public event EventHandler<Pokemon>? ManualCaptureStarted;

    public void Start()
    {
        _spawnTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RandomDelay())
        };
        _spawnTimer.Tick += (_, __) => OnSpawn();
        _spawnTimer.Start();
    }

    private double RandomDelay() => _rng.Next(Constants.MIN_SPAWN_SECONDS, Constants.MAX_SPAWN_SECONDS + 1) * 1000;

    public void ForceSpawn()
    {
        OnSpawn();
    }

    private void OnSpawn()
    {
        if (_activeWild != null)
            return;

        var wild = _dexService.CreateRandomPokemon(_rng);
        _activeWild = wild;
        _battleTriggered = false;
        var msg = $"Um {wild.Name} selvagem apareceu!";
        Notify?.Invoke(this, msg);
        BattleStarted?.Invoke(this, wild.Copy());
        
        // Resetar o intervalo do spawn timer para o próximo spawn
        if (_spawnTimer != null)
        {
            _spawnTimer.Interval = TimeSpan.FromMilliseconds(RandomDelay());
        }
    }

    public void TriggerBattleResolution()
    {
        if (_battleTriggered || _activeWild == null)
            return;
            
        _battleTriggered = true;
        ScheduleResolve();
    }

    private void ScheduleResolve()
    {
        _resolveTimer?.Stop();
        _resolveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.BATTLE_RESOLUTION_DELAY_MS)
        };
        _resolveTimer.Tick += (_, __) =>
        {
            _resolveTimer.Stop();
            ResolvePendingBattle();
        };
        _resolveTimer.Start();
    }

    private void ResolvePendingBattle()
    {
        var wild = _activeWild;
        if (wild == null)
            return;

        var player = (_state.Active ?? _state.Party.FirstOrDefault())?.Copy() ?? new Pokemon();
        player.HealFull();
        var result = ResolveBattle(player, wild.Copy());
        if (result.won && !result.caught)
        {
            BeginManualCapture(wild, result.money);
        }
        else
        {
            CompleteBattle(wild, result.won, result.caught, result.money);
        }
    }

    private (bool won, bool caught, int money) ResolveBattle(Pokemon player, Pokemon wild)
    {
        LogToFile($"[ResolveBattle] Iniciando batalha:");
        LogToFile($"  Player: {player.Name} Lv.{player.Level} HP:{player.CurrentHP}/{player.MaxHP} Atk:{player.Attack} Def:{player.Defense} Spd:{player.Speed}");
        LogToFile($"  Wild: {wild.Name} Lv.{wild.Level} HP:{wild.CurrentHP}/{wild.MaxHP} Atk:{wild.Attack} Def:{wild.Defense} Spd:{wild.Speed}");
        LogToFile($"  God Mode: {_state.GodMode}");
        
        // God Mode: vitória instantânea
        if (_state.GodMode)
        {
            LogToFile("[ResolveBattle] God Mode ativo - vitória automática!");
            wild.CurrentHP = 0;
            return (won: true, caught: false, money: 50);
        }
        
        bool playerFirst = player.Speed >= wild.Speed;
        int turns = 0;

        while (player.CurrentHP > 0 && wild.CurrentHP > 0 && turns < Constants.MAX_BATTLE_TURNS)
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

        LogToFile($"[ResolveBattle] Fim da batalha após {turns} turnos:");
        LogToFile($"  Player HP: {player.CurrentHP}/{player.MaxHP}");
        LogToFile($"  Wild HP: {wild.CurrentHP}/{wild.MaxHP}");
        LogToFile($"  Resultado: won={won}");

        if (won)
        {
            money = _rng.Next(12, 40);
            caught = false;
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
        double variance = Constants.DAMAGE_VARIANCE_MIN + _rng.NextDouble() * (Constants.DAMAGE_VARIANCE_MAX - Constants.DAMAGE_VARIANCE_MIN);
        int dmg = Math.Max(1, (int)(baseDamage * variance));
        defender.CurrentHP = Math.Max(0, defender.CurrentHP - dmg);
    }

    public bool TryManualCapture()
    {
        var wild = _activeWild;
        if (wild == null)
            return false;

        if (_awaitingManualCapture)
        {
            double chance = 0.60;
            var player = _state.Active;
            if (player != null)
            {
                chance += Math.Clamp((player.Level - wild.Level) * 0.03, -0.25, 0.25);
            }
            chance = Math.Clamp(chance, 0.25, 0.95);
            bool success = _rng.NextDouble() < chance;
            if (!success)
            {
                Notify?.Invoke(this, $"{wild.Name} escapou da Pokebola!");
            }
            CompleteBattle(wild, won: true, caught: success, money: _pendingManualMoney);
            return success;
        }

        return false;
    }

    private void CompleteBattle(Pokemon wild, bool won, bool caught, int money)
    {
        _awaitingManualCapture = false;
        _pendingManualMoney = 0;
        _manualTimer?.Stop();
        _manualTimer = null;
        _activeWild = null;
        _resolveTimer?.Stop();
        _resolveTimer = null;
        if (caught)
        {
            wild.HealFull();
        }
        BattleFinished?.Invoke(this, (wild, won, caught, money));
    }

    private void BeginManualCapture(Pokemon wild, int money)
    {
        _awaitingManualCapture = true;
        _pendingManualMoney = money;
        ManualCaptureStarted?.Invoke(this, wild.Copy());
        Notify?.Invoke(this, $"{wild.Name} ficou atordoado! Lance uma Pokebola!");
        _manualTimer?.Stop();
        _manualTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(12000)
        };
        _manualTimer.Tick += (_, __) =>
        {
            _manualTimer.Stop();
            ManualTimerElapsed();
        };
        _manualTimer.Start();
    }

    private void ManualTimerElapsed()
    {
        var wild = _activeWild;
        if (wild == null)
            return;
        CompleteBattle(wild, won: true, caught: false, money: _pendingManualMoney);
    }

    private void LogToFile(string message)
    {
#if DEBUG
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "pokebar_debug.txt"
            );
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            System.IO.File.AppendAllText(logPath, $"[{timestamp}] {message}\n");
        }
        catch { /* Ignorar erros de log */ }
#endif
    }
}



