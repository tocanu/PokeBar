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
    private const double WindowWidth = 64;
    private const double WindowHeight = 96;
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
    private SpriteAnimationSet? _wildAnimations;
    private IReadOnlyList<ImageSource> _wildFrames = Array.Empty<ImageSource>();
    private int _wildFrameIndex = 0;
    private bool _wildVisible;
    private bool _facingRight = true;
    private bool _wildFacingRight = true;
    private bool? _battleFacingRestore;
    private System.Timers.Timer? _animTimer;
    private System.Timers.Timer? _walkTimer;
    private System.Timers.Timer? _clashTimer;
    private double _x = 0;
    private double _wildX = 0;
    private int _barIndex = 0;
    private int _wildBarIndex = 0;
    private PetBehavior _wildBehavior = PetBehavior.Walking;
    private bool _visible = true;
    private PetBehavior _behavior = PetBehavior.Walking;
    private DateTime _behaviorUntil = DateTime.MinValue;
    private DateTime _nextFlipCooldown = DateTime.MinValue;
    private bool _inBattle = false;
    private bool _manualCaptureActive = false;
    private DateTime _pokeballCooldownUntil = DateTime.MinValue;
    private bool _isChasing = false;
    private bool _enemyStunned = false;
    private Pokemon? _stunnedEnemy = null;

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
    public event EventHandler<System.Windows.Point>? RequestWildReposition;
    public event EventHandler? BattleClashRequested;
    public event EventHandler<bool>? ManualCaptureModeChanged;
    
    // Referência para a janela do inimigo (necessária para a Pokébola)
    public Window? WildWindow { get; set; }

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
    public bool BubbleVisible { get => _bubbleVisible && _state.ShowDialogBubbles; private set { _bubbleVisible = value; OnPropertyChanged(); } }
    public bool ShowDialogBubbles { get => _state.ShowDialogBubbles; set { _state.ShowDialogBubbles = value; OnPropertyChanged(); OnPropertyChanged(nameof(BubbleVisible)); _stateService.Save(); } }
    public bool GodMode { get => _state.GodMode; set { _state.GodMode = value; OnPropertyChanged(); _stateService.Save(); } }
    public bool InfinitePokeballs { get => _state.InfinitePokeballs; set { _state.InfinitePokeballs = value; OnPropertyChanged(); _stateService.Save(); } }
    public bool WildVisible { get => _wildVisible; private set { _wildVisible = value; OnPropertyChanged(); } }
    public bool InBattle => _inBattle;
    public bool ShowPokeball => _inBattle || _manualCaptureActive; // Mostra durante batalha OU captura
    public BallType SelectedBall { get => _state.SelectedBall; set { _state.SelectedBall = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedBallName)); _stateService.Save(); } }
    public string SelectedBallName => BallInfo.GetName(_state.SelectedBall);
    public double PlayerVerticalOffset { get; private set; }
    public double WildVerticalOffset { get; private set; }

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
        if (!_manualCaptureActive) return false;
        if (!WildVisible) return false;
        if (!_enemyStunned) return false; // Só pode jogar se o inimigo estiver atordoado
        if (DateTime.UtcNow < _pokeballCooldownUntil) return false;
        if (drag.Length < 20) return false; // Precisa arrastar pelo menos 20 pixels
        
        // Se modo infinito está ativo, sempre pode jogar
        if (_state.InfinitePokeballs) return true;
        
        // Verificar se tem a Pokébola selecionada
        var ballName = _state.SelectedBall.ToString();
        if (_state.Inventory.GetValueOrDefault(ballName, 0) <= 0)
        {
            ShowBalloon($"Sem {BallInfo.GetName(_state.SelectedBall)}! Vá ao PokéMart!");
            return false;
        }
        
        return true;
    }

    public bool TryThrowPokeball()
    {
        if (!_manualCaptureActive || _stunnedEnemy == null) return false;
        
        // Se modo infinito NÃO está ativo, verificar e consumir Pokébola
        if (!_state.InfinitePokeballs)
        {
            // Verificar se tem a Pokébola selecionada no inventário
            var ballName = _state.SelectedBall.ToString();
            if (_state.Inventory.GetValueOrDefault(ballName, 0) <= 0)
            {
                ShowBubble($"Sem {BallInfo.GetName(_state.SelectedBall)}!");
                return false;
            }
            
            _state.Inventory[ballName]--;
        }
        
        _pokeballCooldownUntil = DateTime.UtcNow.AddSeconds(1.2);
        
        // Chance de captura baseada em HP e tipo de Pokébola
        double hpPercent = (double)_stunnedEnemy.CurrentHP / _stunnedEnemy.MaxHP;
        double baseCaptureChance = 0.3 + (1.0 - hpPercent) * 0.5; // 30% a 80% base
        
        // Aplicar multiplicador da Pokébola
        double ballMultiplier = BallInfo.GetBaseCatchRate(_state.SelectedBall);
        double captureChance = Math.Min(1.0, baseCaptureChance * ballMultiplier);
        
        bool success = _rng.NextDouble() < captureChance;
        
        if (success)
        {
            ShowBubble($"Capturado! ★");
            _stunnedEnemy.HealFull();
            _state.Box.Add(_stunnedEnemy);
            _stateService.Save();
            
            // Esconder inimigo após captura
            HideWild();
            // NÃO sair do modo de captura - manter ativo para próximo Pokémon
            _enemyStunned = false;
            _stunnedEnemy = null;
        }
        else
        {
            ShowBubble("Quase!");
        }
        
        return success;
    }

    public void SpawnRandomEnemy()
    {
        _battleService.ForceSpawn();
    }
    
    public void ActivateManualCaptureMode()
    {
        // Ativar modo de captura manual para testes
        if (!_wildVisible)
        {
            // Spawnar um inimigo primeiro se não houver um
            SpawnRandomEnemy();
            // Aguardar o spawn processar
            System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (_wildVisible)
                    {
                        _manualCaptureActive = true;
                        ManualCaptureModeChanged?.Invoke(this, true);
                        ShowBubble("Modo de Captura Ativado!");
                    }
                });
            });
        }
        else
        {
            _manualCaptureActive = true;
            ManualCaptureModeChanged?.Invoke(this, true);
            ShowBubble("Modo de Captura Ativado!");
        }
    }

    public void OnWildSpriteClicked()
    {
        if (!_wildVisible || _inBattle) return;
        
        // Determinar direção do inimigo
        bool enemyIsRight = _wildX > _x;
        
        // Fazer o Pokémon olhar para o inimigo e pular
        _facingRight = enemyIsRight;
        PlayJumpReaction();
        
        // Mostrar indicador visual
        ShowBubble("!");
        
        // Iniciar movimento em direção ao inimigo
        StartChaseEnemy();
    }

    private void PlayJumpReaction()
    {
        // Animação de pulo será aplicada via evento
        RequestPlayerJump?.Invoke(this, EventArgs.Empty);
    }

    private void StartChaseEnemy()
    {
        // Forçar comportamento de andar
        _behavior = PetBehavior.Walking;
        _behaviorUntil = DateTime.UtcNow.AddSeconds(15);
        
        // Acelerar movimento em direção ao inimigo
        _isChasing = true;
    }

    public event EventHandler? RequestPlayerJump;

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
        _battleService.ManualCaptureStarted += (_, wild) => System.Windows.Application.Current?.Dispatcher?.Invoke(() => OnManualCaptureStarted(wild));
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
        PlayerVerticalOffset = sprites.VerticalOffset;
        OnPropertyChanged(nameof(PlayerVerticalOffset));
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
        _wildAnimations = _spriteService.LoadAnimations(wild.DexNumber);
        var sprites = _wildAnimations;
        WildVerticalOffset = sprites.VerticalOffset;
        OnPropertyChanged(nameof(WildVerticalOffset));
        if (sprites.WalkLeft.Count == 0 && sprites.WalkRight.Count == 0)
        {
            return;
        }
        
        // Se modo de captura manual está ativo, simular que o Pokémon já está atordoado
        if (_manualCaptureActive)
        {
            // Usar frames de sono/atordoado
            var sleepFrames = sprites.SleepLeft.Count > 0 ? sprites.SleepLeft : sprites.IdleLeft;
            _wildFrames = sleepFrames;
            _wildFrameIndex = 0;
            _stunnedEnemy = wild;
            _enemyStunned = true;
            OnPropertyChanged(nameof(WildCurrentFrame));
            WildVisible = true;
            ManualCaptureModeChanged?.Invoke(this, true); // Re-notificar para atualizar Pokébola
            ShowBubble($"{wild.Name} apareceu atordoado!");
            return;
        }
        
        // Começar andando para a esquerda (a direção padrão inicial)
        _wildFacingRight = false;
        _wildFrames = sprites.WalkLeft.Count > 0 ? sprites.WalkLeft : sprites.IdleLeft;
        _wildBehavior = PetBehavior.Walking;
        _wildFrameIndex = 0;
        
        // Spawn inimigo em uma posição aleatória
        var bars = _taskbarService.GetAllTaskbars();
        if (bars.Length == 0)
        {
            bars = new[] { _taskbarService.GetTaskbarInfo() };
        }
        _wildBarIndex = _rng.Next(bars.Length);
        var info = bars[_wildBarIndex];
        _wildX = info.Bounds.Left + _rng.NextDouble() * Math.Max(0, info.Bounds.Width - WindowWidth);
        
        WildVisible = true;
        OnPropertyChanged(nameof(WildCurrentFrame));
        PositionWild();
    }

    private void OnManualCaptureStarted(Pokemon wild)
    {
        _manualCaptureActive = true;
        _inBattle = false;
        OnPropertyChanged(nameof(ShowPokeball)); // Atualiza visibilidade da Pokébola
        var sprites = _wildAnimations ?? _spriteService.LoadAnimations(wild.DexNumber);
        _wildAnimations = sprites;
        var sleepFrames = sprites.SleepLeft.Count > 0 ? sprites.SleepLeft : sprites.IdleLeft;
        if (sleepFrames.Count > 0)
        {
            _wildFrames = sleepFrames;
            _wildFrameIndex = 0;
            OnPropertyChanged(nameof(WildCurrentFrame));
        }
        WildVisible = true;
        ManualCaptureModeChanged?.Invoke(this, true);
        ShowBubble($"{wild.Name} esta atordoado!");
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
        _wildBarIndex = Math.Clamp(_wildBarIndex, 0, bars.Length - 1);
        var info = bars[_barIndex];
        
        // Atualiza posição do inimigo se estiver visível e NÃO atordoado
        if (WildVisible && !_inBattle && !_enemyStunned)
        {
            UpdateWildPosition(bars);
            CheckCollision();
        }

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
            if (!_isChasing) // Não muda direção aleatoriamente durante perseguição
            {
                MaybeFlipDirection();
            }
        }

        if (_behavior == PetBehavior.Walking)
        {
            double speed = _isChasing ? 1.5 : (0.5 + _rng.NextDouble());
            
            // Se está perseguindo, sempre mover em direção ao inimigo
            if (_isChasing && WildVisible)
            {
                bool shouldGoRight = _wildX > _x;
                _facingRight = shouldGoRight;
                _x += shouldGoRight ? speed : -speed;
            }
            else
            {
                _x += _facingRight ? speed : -speed;
            }
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

        double y = CalculateYPosition(info);

        RequestReposition?.Invoke(this, new System.Windows.Point(_x, y));
        OnPropertyChanged(nameof(CurrentFrame));
    }

    private double CalculateYPosition(TaskbarInfo info)
    {
        const double pad = 4;
        double y = info.Edge switch
        {
            TaskbarEdge.Top => info.Bounds.Top + pad,
            TaskbarEdge.Bottom => info.Bounds.Bottom - WindowHeight,
            TaskbarEdge.Left => info.Bounds.Top + pad,
            TaskbarEdge.Right => info.Bounds.Top + pad,
            _ => info.Bounds.Bottom - WindowHeight
        };
        return y + HeightOffsetPixels;
    }

    private void UpdateWildPosition(TaskbarInfo[] bars)
    {
        var info = bars[_wildBarIndex];
        
        if (_wildBehavior == PetBehavior.Walking)
        {
            double speed = 0.5 + _rng.NextDouble();
            _wildX += _wildFacingRight ? speed : -speed;
        }

        double minX = info.Bounds.Left;
        double maxX = info.Bounds.Right - WindowWidth;

        if (_wildX <= minX)
        {
            if (_wildFacingRight)
            {
                _wildX = minX;
            }
            else
            {
                int next = ReverseMonitorWalk ? _wildBarIndex + 1 : _wildBarIndex - 1;
                if (next >= 0 && next < bars.Length)
                {
                    _wildBarIndex = next;
                    info = bars[_wildBarIndex];
                    _wildX = info.Bounds.Right - WindowWidth;
                }
                else
                {
                    _wildX = minX;
                    _wildFacingRight = true;
                    UpdateWildFrames();
                }
            }
        }
        else if (_wildX >= maxX)
        {
            if (!_wildFacingRight)
            {
                _wildX = maxX;
            }
            else
            {
                int next = ReverseMonitorWalk ? _wildBarIndex - 1 : _wildBarIndex + 1;
                if (next >= 0 && next < bars.Length)
                {
                    _wildBarIndex = next;
                    info = bars[_wildBarIndex];
                    _wildX = info.Bounds.Left;
                }
                else
                {
                    _wildX = maxX;
                    _wildFacingRight = false;
                    UpdateWildFrames();
                }
            }
        }

        _wildX = Math.Clamp(_wildX, minX, maxX);
        PositionWild();
    }

    private void UpdateWildFrames()
    {
        if (_wildAnimations == null) return;
        
        _wildFrames = _wildFacingRight 
            ? (_wildAnimations.WalkRight.Count > 0 ? _wildAnimations.WalkRight : _wildAnimations.IdleRight)
            : (_wildAnimations.WalkLeft.Count > 0 ? _wildAnimations.WalkLeft : _wildAnimations.IdleLeft);
        _wildFrameIndex = 0;
        OnPropertyChanged(nameof(WildCurrentFrame));
    }

    private void PositionWild()
    {
        var bars = _taskbarService.GetAllTaskbars();
        if (bars.Length == 0)
        {
            bars = new[] { _taskbarService.GetTaskbarInfo() };
        }
        
        if (_wildBarIndex >= bars.Length) return;
        
        var info = bars[_wildBarIndex];
        
        // Usar exatamente a mesma lógica de posicionamento do jogador
        double y = CalculateYPosition(info);

        RequestWildReposition?.Invoke(this, new System.Windows.Point(_wildX, y));
    }

    public string GetPositionDebugInfo()
    {
        var bars = _taskbarService.GetAllTaskbars();
        if (bars.Length == 0)
        {
            bars = new[] { _taskbarService.GetTaskbarInfo() };
        }

        var playerBar = _barIndex < bars.Length ? bars[_barIndex] : null;
        var wildBar = _wildBarIndex < bars.Length ? bars[_wildBarIndex] : null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Mapa das Barras ===");
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            sb.AppendLine($"Monitor {i}: X={bar.Bounds.Left:F0}-{bar.Bounds.Right:F0} ({bar.Bounds.Width:F0}px), Edge={bar.Edge}");
            
            if (i == _barIndex)
                sb.AppendLine($"  -> Jogador em X={_x:F0} (olhando {(_facingRight ? "direita" : "esquerda")})");
            
            if (i == _wildBarIndex && WildVisible)
                sb.AppendLine($"  -> Inimigo em X={_wildX:F0} (olhando {(_wildFacingRight ? "direita" : "esquerda")})");
        }
        
        if (playerBar != null && wildBar != null && _barIndex == _wildBarIndex)
        {
            double distance = Math.Abs(_x - _wildX);
            sb.AppendLine($"\nDistância entre personagens: {distance:F0}px");
            sb.AppendLine($"Limiar de colisão: {WindowWidth * 0.8:F0}px");
        }
        
        return sb.ToString();
    }

    private void CheckCollision()
    {
        if (_inBattle) return;
        
        // Verifica se est\u00e3o no mesmo monitor
        if (_barIndex != _wildBarIndex) return;
        
        // Verifica dist\u00e2ncia
        double distance = Math.Abs(_x - _wildX);
        if (distance < WindowWidth * 0.8)
        {
            StartBattle();
        }
    }

    private void StartBattle()
    {
        // Verificar HP antes de iniciar batalha
        var active = _state.Active;
        if (active == null || active.CurrentHP <= 0)
        {
            ShowBubble("Seu Pokémon está incapacitado!");
            return;
        }
        
        _inBattle = true;
        _isChasing = false; // Parar perseguição quando batalha começar
        ExitManualCaptureMode();
        
        // Forçar comportamento de walking para evitar dormir durante batalha
        SetBehavior(PetBehavior.Walking);
        _behaviorUntil = DateTime.MaxValue; // Manter andando durante batalha
        
        _battleFacingRestore = _facingRight;
        
        // Fazer os personagens se encararem baseado na posição
        if (_x < _wildX)
        {
            _facingRight = true;  // Jogador olha para direita
            _wildFacingRight = false; // Inimigo olha para esquerda
        }
        else
        {
            _facingRight = false; // Jogador olha para esquerda
            _wildFacingRight = true;  // Inimigo olha para direita
        }
        
        UpdateWildFrames();
        OnPropertyChanged(nameof(CurrentFrame));
        BattleClashRequested?.Invoke(this, EventArgs.Empty);
        StartClashTimer();
        _battleService.TriggerBattleResolution();
    }

    private void RefreshBehaviorState()
    {
        // Não mudar comportamento durante batalha ou perseguição
        if (_inBattle || _isChasing)
            return;
            
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
        // Não descansar durante batalha ou perseguição
        if (_inBattle || _isChasing)
            return;
            
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
        LogToFile("[HideWild] Chamado - escondendo inimigo");
        
        StopClashTimer();
        ExitManualCaptureMode();
        _wildFrames = Array.Empty<ImageSource>();
        _wildFrameIndex = 0;
        WildVisible = false;
        OnPropertyChanged(nameof(WildCurrentFrame));
        _inBattle = false;
        
        // Restaurar comportamento normal
        _behaviorUntil = DateTime.MinValue;
        
        if (_battleFacingRestore.HasValue)
        {
            _facingRight = _battleFacingRestore.Value;
            _battleFacingRestore = null;
        }
        _wildAnimations = null;
    }

    private void LogToFile(string message)
    {
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
    }

    private void ExitManualCaptureMode()
    {
        if (_manualCaptureActive)
        {
            _manualCaptureActive = false;
            OnPropertyChanged(nameof(ShowPokeball)); // Atualiza visibilidade da Pokébola
            ManualCaptureModeChanged?.Invoke(this, false);
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
            var texts = new[] { ":)", "=)", ":D", "^_^", "♥", ":3", "^^", ":P", "o_o", "XD" };
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
        ShowBalloon("PokéCenter", $"Seu time foi curado! ({_state.Party.Count} Pokémon)");
    }

    public void OpenShop()
    {
        var shopWindow = new Views.ShopWindow(_state, () => _stateService.Save());
        shopWindow.ShowDialog();
    }

    public void ManagePC()
    {
        var partyInfo = string.Join("\n", _state.Party.Select((p, i) => 
            $"  {(i == _state.ActiveIndex ? "★" : "  ")} {p.Name} (Lv.{p.Level}) - HP: {p.CurrentHP}/{p.MaxHP}"));
        
        var boxInfo = _state.Box.Count > 0 
            ? string.Join("\n", _state.Box.Select(p => $"  • {p.Name} (Lv.{p.Level})"))
            : "  (vazio)";
        
        var message = $"🎒 Time ({_state.Party.Count}/6):\n{partyInfo}\n\n" +
                     $"💾 PC ({_state.Box.Count}):\n{boxInfo}";
        
        System.Windows.MessageBox.Show(message, "PC - Organizar Pokémon", System.Windows.MessageBoxButton.OK);
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
        LogToFile($"[OnBattleFinished] won={won}, caught={caught}, wild={wild.Name}");
        LogToFile($"[OnBattleFinished] _manualCaptureActive={_manualCaptureActive}, _inBattle={_inBattle}");
        
        if (won && !caught)
        {
            LogToFile("[OnBattleFinished] Vitória sem captura - ativando modo de captura manual");
            
            // IMPORTANTE: Parar batalha e animações PRIMEIRO
            _inBattle = false;
            StopClashTimer();
            
            // Restaurar comportamento do player IMEDIATAMENTE
            _behaviorUntil = DateTime.MinValue;
            _behavior = PetBehavior.Walking; // Forçar estado de andar
            if (_battleFacingRestore.HasValue)
            {
                _facingRight = _battleFacingRestore.Value;
                _battleFacingRestore = null;
            }
            
            // Inimigo fica atordoado para captura - NÃO esconder
            _enemyStunned = true;
            _stunnedEnemy = wild;
            _state.Money += money;
            _stateService.Save();
            
            LogToFile($"[OnBattleFinished] WildVisible={WildVisible}, _wildFrames.Count={_wildFrames.Count}");
            
            // Ativar modo de captura manual
            _manualCaptureActive = true;
            OnPropertyChanged(nameof(ShowPokeball)); // Atualiza visibilidade da Pokébola
            ManualCaptureModeChanged?.Invoke(this, true);
            
            ShowBalloon($"Vitória! Arraste seu Pokémon para lançar!");
            
            LogToFile($"[OnBattleFinished] Final - _inBattle={_inBattle}, _enemyStunned={_enemyStunned}, _manualCaptureActive={_manualCaptureActive}");
            return;
        }

        LogToFile("[OnBattleFinished] Escondendo inimigo - não foi vitória sem captura");
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
        
        _enemyStunned = false;
        _stunnedEnemy = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


