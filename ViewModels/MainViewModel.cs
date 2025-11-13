using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using PokeBar.Models;
using PokeBar.Services;
using PokeBar.Utils;

namespace PokeBar.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private const double WindowWidth = 128;
    private const double WindowHeight = 56;
    private readonly GameState _state;
    private readonly StateService _stateService;
    private readonly SpriteService _spriteService;
    private readonly TaskbarService _taskbarService;
    private readonly BattleService _battleService;
    private readonly Random _rng = new();

    private IReadOnlyList<ImageSource> _rightFrames = Array.Empty<ImageSource>();
    private IReadOnlyList<ImageSource> _leftFrames = Array.Empty<ImageSource>();
    private IReadOnlyList<ImageSource> _idleRight = Array.Empty<ImageSource>();
    private IReadOnlyList<ImageSource> _idleLeft = Array.Empty<ImageSource>();
    private IReadOnlyList<ImageSource> _sleepRight = Array.Empty<ImageSource>();
    private IReadOnlyList<ImageSource> _sleepLeft = Array.Empty<ImageSource>();
    private int _frameIndex = 0;
    private IReadOnlyList<ImageSource> _wildFrames = Array.Empty<ImageSource>();
    private int _wildFrameIndex = 0;
    private bool _wildVisible;
    private bool _facingRight = true;
    private bool? _battleFacingRestore;
    private System.Timers.Timer? _animTimer;
    private System.Timers.Timer? _walkTimer;
    private System.Timers.Timer? _clashTimer;
    private double _x = 0;
    private int _barIndex = 0;
    private bool _visible = true;
    private PetBehavior _behavior = PetBehavior.Walking;
    private DateTime _behaviorUntil = DateTime.MinValue;
    private DateTime _nextFlipCooldown = DateTime.MinValue;
    private bool _inBattle = false;
    private DateTime _pokeballCooldownUntil = DateTime.MinValue;

    private string _bubbleText = string.Empty;
    private bool _bubbleVisible;
    private System.Timers.Timer? _bubbleTimer;

    private enum PetBehavior
    {
        Walking,
        Idle,
        Sleeping
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<System.Windows.Point>? RequestReposition;
    public event EventHandler? BattleClashRequested;

    public ImageSource? CurrentFrame
    {
        get
        {
            var frames = GetCurrentFrames();
            if (frames.Count == 0) return null;
            return frames[_frameIndex % frames.Count];
        }
    }

    public string BubbleText { get => _bubbleText; private set { _bubbleText = value; OnPropertyChanged(); } }
    public bool BubbleVisible { get => _bubbleVisible; private set { _bubbleVisible = value; OnPropertyChanged(); } }
    public bool WildVisible { get => _wildVisible; private set { _wildVisible = value; OnPropertyChanged(); } }
    public bool InBattle => _inBattle;

    public ImageSource? WildCurrentFrame
    {
        get
        {
            if (_wildFrames.Count == 0) return null;
            return _wildFrames[_wildFrameIndex % _wildFrames.Count];
        }
    }

    public bool CanThrowPokeball(System.Windows.Vector drag)
    {
        if (!_inBattle) return false;
        if (DateTime.UtcNow < _pokeballCooldownUntil) return false;
        if (drag.Length < 30) return false;
        if (drag.X < 10) return false;
        return true;
    }

    public bool TryThrowPokeball()
    {
        if (!_inBattle) return false;
        _pokeballCooldownUntil = DateTime.UtcNow.AddSeconds(1.2);
        bool success = _battleService.TryManualCapture();
        if (success)
        {
            ShowBubble("Capturado!");
        }
        else
        {
            ShowBubble("A Pokébola falhou!");
        }
        return success;
    }

    public bool ReverseMonitorWalk
    {
        get => _state.ReverseMonitorWalk;
        set { _state.ReverseMonitorWalk = value; _stateService.Save(); }
    }

    public int HeightOffsetPixels
    {
        get => _state.HeightOffsetPixels;
        set { _state.HeightOffsetPixels = value; _stateService.Save(); PositionOnTaskbar(); }
    }

    public MainViewModel(GameState state, StateService stateService, SpriteService spriteService, TaskbarService taskbarService, BattleService battleService)
    {
        _state = state;
        _stateService = stateService;
        _spriteService = spriteService;
        _taskbarService = taskbarService;
        _battleService = battleService;

        _taskbarService.TaskbarChanged += (_, info) => PositionOnTaskbar();
        _battleService.Notify += (_, msg) => ShowBalloon(msg);
        _battleService.BattleStarted += (_, wild) => System.Windows.Application.Current?.Dispatcher?.Invoke(() => OnBattleStarted(wild));
        _battleService.BattleFinished += (_, result) => System.Windows.Application.Current?.Dispatcher?.Invoke(() => OnBattleFinished(result.wild, result.won, result.caught, result.money));
    }

    public void Initialize()
    {
        var active = _state.Active ?? _state.Party.First();
        var sprites = _spriteService.LoadAnimations(active.DexNumber);
        _rightFrames = sprites.WalkRight;
        _leftFrames = sprites.WalkLeft;
        _idleRight = sprites.IdleRight;
        _idleLeft = sprites.IdleLeft;
        _sleepRight = sprites.SleepRight;
        _sleepLeft = sprites.SleepLeft;
        _behavior = PetBehavior.Walking;
        _behaviorUntil = DateTime.MinValue;
        _frameIndex = 0;

        _animTimer = new System.Timers.Timer(150);
        _animTimer.Elapsed += (_, __) =>
        {
            var frames = GetCurrentFrames();
            if (frames.Count > 0)
            {
                _frameIndex = (_frameIndex + 1) % frames.Count;
                OnPropertyChanged(nameof(CurrentFrame));
            }
            if (_wildFrames.Count > 0)
            {
                _wildFrameIndex = (_wildFrameIndex + 1) % _wildFrames.Count;
                OnPropertyChanged(nameof(WildCurrentFrame));
            }
        };
        _animTimer.Start();

        _walkTimer = new System.Timers.Timer(16);
        _walkTimer.Elapsed += (_, __) => System.Windows.Application.Current?.Dispatcher?.Invoke(WalkStep);
        _walkTimer.Start();

        PositionOnTaskbar();
        _battleService.Start();
    }

    private void OnBattleStarted(Pokemon wild)
    {
        _inBattle = true;
        _battleFacingRestore = _facingRight;
        _facingRight = true;
        if (wild.DexNumber == 6) // Charizard demo
        {
            var sprites = _spriteService.LoadAnimations(wild.DexNumber);
            _wildFrames = sprites.WalkLeft.Count > 0 ? sprites.WalkLeft : sprites.IdleLeft;
            _wildFrameIndex = 0;
            WildVisible = _wildFrames.Count > 0;
            OnPropertyChanged(nameof(WildCurrentFrame));
            BattleClashRequested?.Invoke(this, EventArgs.Empty);
            StartClashTimer();
        }
        else
        {
            HideWild();
        }
    }

    private IReadOnlyList<ImageSource> GetCurrentFrames()
    {
        IReadOnlyList<ImageSource> frames = _behavior switch
        {
            PetBehavior.Idle => _facingRight ? _idleRight : _idleLeft,
            PetBehavior.Sleeping => _facingRight ? _sleepRight : _sleepLeft,
            _ => _facingRight ? _rightFrames : _leftFrames
        };

        if (frames.Count == 0)
        {
            frames = _facingRight ? _rightFrames : _leftFrames;
        }

        return frames;
    }

    private void WalkStep()
    {
        var bars = _taskbarService.GetAllTaskbars();
        if (bars.Length == 0)
        {
            bars = new[] { _taskbarService.GetTaskbarInfo() };
        }
        _barIndex = Math.Clamp(_barIndex, 0, bars.Length - 1);
        var info = bars[_barIndex];

        if (_taskbarService.IsMonitorFullscreen(info.MonitorHandle))
        {
            int target = -1;
            for (int i = 0; i < bars.Length; i++)
            {
                if (!_taskbarService.IsMonitorFullscreen(bars[i].MonitorHandle))
                {
                    target = i;
                    break;
                }
            }
            if (target >= 0)
            {
                var prev = info;
                _barIndex = target;
                info = bars[_barIndex];
                double ratio = 0.5;
                double denom = prev.Bounds.Width - WindowWidth;
                if (denom > 1)
                {
                    ratio = Math.Clamp((_x - prev.Bounds.Left) / denom, 0, 1);
                }
                double newDenom = info.Bounds.Width - WindowWidth;
                _x = info.Bounds.Left + (newDenom > 0 ? ratio * newDenom : 0);
                if (!_visible) ToggleVisibilityInternal(true);
            }
            else
            {
                if (_visible) ToggleVisibilityInternal(false);
                return;
            }
        }
        else if (!_visible)
        {
            ToggleVisibilityInternal(true);
        }

        double minX = info.Bounds.Left;
        double maxX = info.Bounds.Right - WindowWidth;

        if (_inBattle)
        {
            _x = Math.Clamp(_x, minX, maxX);
            return;
        }

        RefreshBehaviorState();
        if (_behavior == PetBehavior.Walking)
        {
            TryStartRestState();
            MaybeFlipDirection();
        }

        if (_behavior == PetBehavior.Walking)
        {
            double speed = 0.5 + _rng.NextDouble();
            _x += _facingRight ? speed : -speed;
        }

        if (_x <= minX)
        {
            if (_facingRight)
            {
                _x = minX;
            }
            else
            {
                // mover para barra anterior
                int next = ReverseMonitorWalk ? _barIndex + 1 : _barIndex - 1;
                if (next >= 0 && next < bars.Length)
                {
                    _barIndex = next;
                    info = bars[_barIndex];
                    _x = info.Bounds.Right - WindowWidth;
                }
                else
                {
                    _x = minX;
                    _facingRight = true;
                }
            }
        }
        else if (_x >= maxX)
        {
            if (!_facingRight)
            {
                _x = maxX;
            }
            else
            {
                int next = ReverseMonitorWalk ? _barIndex - 1 : _barIndex + 1;
                if (next >= 0 && next < bars.Length)
                {
                    _barIndex = next;
                    info = bars[_barIndex];
                    _x = info.Bounds.Left;
                }
                else
                {
                    _x = maxX;
                    _facingRight = false;
                }
            }
        }

        _x = Math.Clamp(_x, minX, maxX);

        const double pad = 4; // leve ajuste para descer em taskbar superior e evitar jitter
        double y = info.Edge switch
        {
            TaskbarEdge.Top => info.Bounds.Top + pad,
            TaskbarEdge.Bottom => info.Bounds.Bottom - WindowHeight,
            TaskbarEdge.Left => info.Bounds.Top + pad,
            TaskbarEdge.Right => info.Bounds.Top + pad,
            _ => info.Bounds.Bottom - WindowHeight
        };
        y += HeightOffsetPixels;

        RequestReposition?.Invoke(this, new System.Windows.Point(_x, y));
        OnPropertyChanged(nameof(CurrentFrame));
    }

    private void RefreshBehaviorState()
    {
        if (_behavior == PetBehavior.Walking)
            return;

        if (DateTime.UtcNow < _behaviorUntil)
            return;

        if (_behavior == PetBehavior.Idle)
        {
            if (_rng.NextDouble() < 0.6)
            {
                SetBehavior(PetBehavior.Sleeping, 4 + _rng.NextDouble() * 4);
            }
            else
            {
                SetBehavior(PetBehavior.Walking);
            }
            return;
        }

        if (_behavior == PetBehavior.Sleeping)
        {
            SetBehavior(PetBehavior.Walking);
        }
    }

    private void TryStartRestState()
    {
        if (_behavior != PetBehavior.Walking)
            return;

        double roll = _rng.NextDouble();
        if (roll < 0.005)
        {
            SetBehavior(PetBehavior.Idle, 1.5 + _rng.NextDouble() * 2.5);
        }
    }

    private void MaybeFlipDirection()
    {
        if (_behavior != PetBehavior.Walking)
            return;
        if (DateTime.UtcNow < _nextFlipCooldown)
            return;
        if (_rng.NextDouble() < 0.0015)
        {
            _facingRight = !_facingRight;
            _frameIndex = 0;
            _nextFlipCooldown = DateTime.UtcNow.AddSeconds(6 + _rng.NextDouble() * 6);
            OnPropertyChanged(nameof(CurrentFrame));
        }
    }

    private void SetBehavior(PetBehavior behavior, double? durationSeconds = null)
    {
        bool changed = _behavior != behavior;
        _behavior = behavior;
        _behaviorUntil = (behavior == PetBehavior.Walking || durationSeconds is null)
            ? DateTime.MinValue
            : DateTime.UtcNow.AddSeconds(durationSeconds.Value);
        if (changed)
        {
            _frameIndex = 0;
            OnPropertyChanged(nameof(CurrentFrame));
        }
    }

    private void StartClashTimer()
    {
        StopClashTimer();
        if (!WildVisible)
            return;
        _clashTimer = new System.Timers.Timer(1800);
        _clashTimer.Elapsed += ClashTimerElapsed;
        _clashTimer.Start();
    }

    private void ClashTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (WildVisible)
            {
                BattleClashRequested?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void StopClashTimer()
    {
        if (_clashTimer != null)
        {
            _clashTimer.Elapsed -= ClashTimerElapsed;
            _clashTimer.Stop();
            _clashTimer.Dispose();
            _clashTimer = null;
        }
    }

    private void HideWild()
    {
        StopClashTimer();
        _wildFrames = Array.Empty<ImageSource>();
        _wildFrameIndex = 0;
        WildVisible = false;
        OnPropertyChanged(nameof(WildCurrentFrame));
        _inBattle = false;
        if (_battleFacingRestore.HasValue)
        {
            _facingRight = _battleFacingRestore.Value;
            _battleFacingRestore = null;
        }
    }

    public void PositionOnTaskbar()
    {
        var bars = _taskbarService.GetAllTaskbars();
        TaskbarInfo info;
        if (bars.Length > 0)
        {
            _barIndex = ReverseMonitorWalk ? bars.Length - 1 : 0;
            info = bars[_barIndex];
        }
        else
        {
            info = _taskbarService.GetTaskbarInfo();
            _barIndex = 0;
        }
        _x = info.Bounds.Left + (info.Bounds.Width - WindowWidth) / 2;
        const double pad = 4;
        double y = info.Edge switch
        {
            TaskbarEdge.Top => info.Bounds.Top + pad,
            TaskbarEdge.Bottom => info.Bounds.Bottom - WindowHeight,
            TaskbarEdge.Left => info.Bounds.Top + pad,
            TaskbarEdge.Right => info.Bounds.Top + pad,
            _ => info.Bounds.Bottom - WindowHeight
        };
        y += HeightOffsetPixels;
        RequestReposition?.Invoke(this, new System.Windows.Point(_x, y));
    }

    public void OnClicked()
    {
        if (_behavior != PetBehavior.Walking)
        {
            SetBehavior(PetBehavior.Walking);
            _nextFlipCooldown = DateTime.UtcNow.AddSeconds(2);
        }

        var roll = _rng.NextDouble();
        var active = _state.Active;
        string msg;
        if (roll < 0.05 && active != null && active.CurrentHP < active.MaxHP)
        {
            active.CurrentHP += 1;
            msg = $"{active.Name} ganhou +1 HP!";
            _stateService.Save();
        }
        else if (roll < 0.10)
        {
            _state.Money += 1;
            msg = "+$1 encontrado!";
            _stateService.Save();
        }
        else
        {
            var texts = new[] { "Pika?", "Zzz...", "Quero biscoito!", "Hora da aventura!", "Treinar?" };
            msg = texts[_rng.Next(texts.Length)];
        }
        ShowBubble(msg);
    }

    private void ShowBubble(string text)
    {
        BubbleText = text;
        BubbleVisible = true;
        _bubbleTimer ??= new System.Timers.Timer(2000);
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = 2000;
        _bubbleTimer.Elapsed += BubbleTimer_Elapsed;
        _bubbleTimer.Start();
    }

    private void BubbleTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() => BubbleVisible = false);
        if (_bubbleTimer != null)
        {
            _bubbleTimer.Elapsed -= BubbleTimer_Elapsed;
            _bubbleTimer.Stop();
        }
        _bubbleTimer = null;
    }

    public void HealAll()
    {
        foreach (var p in _state.Party)
            p.CurrentHP = p.MaxHP;
        _stateService.Save();
        ShowBalloon("PokéCenter", "Seu time foi curado!");
    }

    public void OpenShop()
    {
        // Simplificado: compra 1 Pokébola por $5
        const string item = "PokeBall";
        if (_state.Money >= 5)
        {
            _state.Money -= 5;
            _state.Inventory[item] = _state.Inventory.GetValueOrDefault(item) + 1;
            _stateService.Save();
            ShowBalloon("PokéMart", "+1 Pokébola comprada");
        }
        else
        {
            ShowBalloon("PokéMart", "Dinheiro insuficiente");
        }
    }

    public void ManagePC()
    {
        ShowBalloon("PC", "Organização simples ainda");
    }

    public void ToggleVisibility()
    {
        ToggleVisibilityInternal(!_visible);
    }

    private void ToggleVisibilityInternal(bool visible)
    {
        _visible = visible;
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
                w.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        });
    }

    public void SaveNow() => _stateService.Save();

    private void ShowBalloon(string message)
    {
        // Routed via TrayService by App — not directly available here, so show as bubble
        ShowBubble(message);
    }

    private void ShowBalloon(string title, string text)
    {
        ShowBubble($"{title}: {text}");
    }

    private void OnBattleFinished(Pokemon wild, bool won, bool caught, int money)
    {
        HideWild();

        bool saved = false;

        if (won)
        {
            _state.Money += money;
            saved = true;
            ShowBalloon($"Vitória vs {wild.Name}! +${money}");
        }

        if (caught)
        {
            wild.HealFull();
            _state.Box.Add(wild);
            saved = true;
            ShowBalloon($"Você capturou {wild.Name}!");
        }

        if (!won && !caught)
        {
            ShowBalloon($"{wild.Name} fugiu...");
        }

        if (saved)
        {
            _stateService.Save();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
