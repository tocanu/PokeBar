using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using PokeBar.ViewModels;
using PokeBar.Utils;
using WinPoint = System.Windows.Point;
using WinVector = System.Windows.Vector;
using WinMessageBox = System.Windows.MessageBox;

namespace PokeBar.Views
{
    public partial class PokeballWindow : Window
    {
        private IntPtr _hwnd;
        private MainViewModel? _vm;
        
        // Controle de arrasto
        private bool _isDragging = false;
        private WinPoint _dragStartPos;
        private WinPoint _lastMousePos;
        private double _currentRotation = 0;
        private DateTime _lastFrameTime = DateTime.Now;
        private WinVector _lastVelocity = new WinVector(0, 0); // Velocidade do último frame
        
        // Física
        private WinVector _velocity = new WinVector(0, 0);
        private const double GRAVITY = 800; // pixels/s² (simulação de gravidade)
        private const double BOUNCE_DAMPING = 0.6; // Coeficiente de restituição
        private const double AIR_RESISTANCE = 0.98; // Resistência do ar
        private const double MIN_VELOCITY = 10; // Velocidade mínima antes de parar
        
        private DispatcherTimer? _physicsTimer;
        private DateTime _lastUpdate;
        
        // Multi-monitor
        private Rect _virtualScreenBounds;
        
        // Animação de captura
        private enum CaptureState
        {
            Flying,      // Voando em direção ao Pokémon
            Impact,      // Momento do impacto (flash)
            Opening,     // Abrindo e sugando partículas
            Shaking,     // Balançando no chão
            Captured     // Captura confirmada
        }
        private CaptureState _captureState = CaptureState.Flying;
        private int _shakeCount = 0;
        private double _shakeAngle = 0;
        private DateTime _capturePhaseStart;
        private WinPoint _impactPosition; // Posição onde acertou o Pokémon

        public PokeballWindow()
        {
            InitializeComponent();
            Loaded += PokeballWindow_Loaded;
            Unloaded += PokeballWindow_Unloaded;
            Deactivated += PokeballWindow_Deactivated;
            
            // Calcular bounds de todos os monitores
            UpdateVirtualScreenBounds();
        }

        private void UpdateVirtualScreenBounds()
        {
            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                minX = Math.Min(minX, screen.Bounds.Left);
                minY = Math.Min(minY, screen.Bounds.Top);
                maxX = Math.Max(maxX, screen.Bounds.Right);
                maxY = Math.Max(maxY, screen.Bounds.Bottom);
            }
            
            _virtualScreenBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void PokeballWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            
            var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pokeball_debug.txt");
            System.IO.File.AppendAllText(logPath, $"\n=== PokeballWindow_Loaded {DateTime.Now:HH:mm:ss} ===\n");
            System.IO.File.AppendAllText(logPath, $"Handle: {_hwnd}\n");
            
            System.Diagnostics.Debug.WriteLine("=== PokeballWindow_Loaded ===");
            System.Diagnostics.Debug.WriteLine($"Handle: {_hwnd}");
            
            // Remover WS_EX_TRANSPARENT para tornar a janela clicável
            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            System.Diagnostics.Debug.WriteLine($"ExStyle antes: 0x{exStyle:X}");
            System.IO.File.AppendAllText(logPath, $"ExStyle antes: 0x{exStyle:X}\n");
            
            exStyle &= ~(int)NativeMethods.WS_EX_TRANSPARENT;
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
            int newExStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            System.Diagnostics.Debug.WriteLine($"ExStyle depois: 0x{newExStyle:X}");
            System.IO.File.AppendAllText(logPath, $"ExStyle depois: 0x{newExStyle:X}\n");
            
            // Tornar a janela transparente mas clicável
            var hwndSource = HwndSource.FromHwnd(_hwnd);
            if (hwndSource != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            }
            
            EnsureTopmost();
            
            if (DataContext is MainViewModel vm)
            {
                _vm = vm;
                vm.ManualCaptureModeChanged += OnManualCaptureModeChanged;
                System.Diagnostics.Debug.WriteLine($"ViewModel conectado. ShowPokeball: {vm.ShowPokeball}");
                System.IO.File.AppendAllText(logPath, $"ViewModel conectado. ShowPokeball: {vm.ShowPokeball}\n");
            }
            
            // Timer para física (60 FPS)
            _physicsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
            };
            _physicsTimer.Tick += PhysicsUpdate;
            _lastUpdate = DateTime.Now;
            
            System.Diagnostics.Debug.WriteLine($"Visibility: {Visibility}, IsHitTestVisible: {IsHitTestVisible}");
            System.IO.File.AppendAllText(logPath, $"Visibility: {Visibility}, IsHitTestVisible: {IsHitTestVisible}\n");
        }

        private void PokeballWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _physicsTimer?.Stop();
            _physicsTimer = null;
            
            if (_vm != null)
            {
                _vm.ManualCaptureModeChanged -= OnManualCaptureModeChanged;
                _vm = null;
            }
        }

        private void OnManualCaptureModeChanged(object? sender, bool active)
        {
            System.Diagnostics.Debug.WriteLine($"=== OnManualCaptureModeChanged: {active} ===");
            if (active)
            {
                // Posicionar acima do inimigo quando ativar modo de captura
                if (_vm?.WildWindow != null)
                {
                    Left = _vm.WildWindow.Left + (_vm.WildWindow.Width - Width) / 2;
                    Top = _vm.WildWindow.Top - 30; // 30px acima
                    System.Diagnostics.Debug.WriteLine($"Posicionado em: Left={Left}, Top={Top}");
                }
                Visibility = Visibility.Visible;
                _velocity = new WinVector(0, 0);
                System.Diagnostics.Debug.WriteLine("Visibility = Visible");
            }
            else
            {
                Visibility = Visibility.Collapsed;
                _physicsTimer?.Stop();
                System.Diagnostics.Debug.WriteLine("Visibility = Collapsed");
            }
        }

        private void PokeballWindow_Deactivated(object? sender, EventArgs e)
        {
            EnsureTopmost();
        }

        private void EnsureTopmost()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            
            var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pokeball_debug.txt");
            System.IO.File.AppendAllText(logPath, $"\n=== CLIQUE DETECTADO {DateTime.Now:HH:mm:ss} ===\n");
            System.IO.File.AppendAllText(logPath, $"ShowPokeball: {_vm?.ShowPokeball}\n");
            
            // Só permitir drag quando está voando (não durante animação de captura)
            if (_vm?.ShowPokeball == true && _captureState == CaptureState.Flying)
            {
                _isDragging = true;
                _dragStartPos = PointToScreen(e.GetPosition(this));
                _lastMousePos = _dragStartPos;
                Mouse.Capture(this);
                _physicsTimer?.Stop();
                _velocity = new WinVector(0, 0);
                Opacity = 0.8;
                e.Handled = true;
                System.IO.File.AppendAllText(logPath, "DRAG INICIADO!\n");
            }
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var now = DateTime.Now;
                double deltaTime = (now - _lastFrameTime).TotalSeconds;
                if (deltaTime < 0.001) deltaTime = 0.001; // Evitar divisão por zero
                _lastFrameTime = now;
                
                var currentPos = PointToScreen(e.GetPosition(this));
                
                // Calcular velocidade do mouse (pixels por segundo)
                var movement = currentPos - _lastMousePos;
                double mouseSpeed = movement.Length / deltaTime; // px/s
                
                // Armazenar a velocidade atual para usar quando soltar
                _lastVelocity = new WinVector(movement.X / deltaTime, movement.Y / deltaTime);
                
                // Atualizar posição
                Left = currentPos.X - Width / 2;
                Top = currentPos.Y - Height / 2;
                
                // Rotação baseada na velocidade do mouse
                // Quanto mais rápido move, mais gira (1 pixel/s = 1 grau/s)
                _currentRotation += mouseSpeed * deltaTime * 0.5; // 0.5 = sensibilidade
                
                // Aplicar rotação ao controle
                var rotateTransform = (PokeballImage.RenderTransform as System.Windows.Media.TransformGroup)?.Children[0] as System.Windows.Media.RotateTransform;
                if (rotateTransform != null)
                {
                    rotateTransform.Angle = _currentRotation;
                }
                
                _lastMousePos = currentPos;
                
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pokeball_debug.txt");
                System.IO.File.AppendAllText(logPath, $"Velocidade mouse: {mouseSpeed:F0} px/s, Rotação={_currentRotation:F0}°\n");
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            
            if (_isDragging)
            {
                _isDragging = false;
                Mouse.Capture(null);
                Opacity = 1.0;
                
                var currentPos = PointToScreen(e.GetPosition(this));
                var delta = currentPos - _dragStartPos;
                double distance = delta.Length;
                
                if (distance >= 20)
                {
                    // Usar a velocidade do último frame de movimento (direção real do mouse)
                    _velocity = _lastVelocity * 0.8; // Multiplicador para ajustar força
                    
                    // Log para debug
                    var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pokeball_debug.txt");
                    System.IO.File.AppendAllText(logPath, $"\n=== SOLTOU ===\nVelocidade: X={_velocity.X:F0}, Y={_velocity.Y:F0}\n");
                    
                    var canThrow = _vm?.CanThrowPokeball(_velocity) ?? false;
                    if (canThrow)
                    {
                        _lastUpdate = DateTime.Now;
                        _physicsTimer?.Start();
                        
                        // NÃO chamar TryThrowPokeball aqui - só durante a física quando colidir
                    }
                    else
                    {
                        // Falhou - esconder a Pokébola para aparecer uma nova
                        Visibility = Visibility.Collapsed;
                        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => 
                        {
                            Dispatcher.Invoke(() => {
                                if (_vm?.ShowPokeball == true)
                                {
                                    ResetPosition();
                                    Visibility = Visibility.Visible;
                                }
                            });
                        });
                    }
                }
                else
                {
                    ResetPosition();
                }
                
                e.Handled = true;
            }
        }

        private void PhysicsUpdate(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            double deltaTime = (now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;
            
            if (deltaTime > 0.1) deltaTime = 0.1; // Limitar dt para evitar saltos
            
            // Aplicar gravidade
            _velocity.Y += GRAVITY * deltaTime;
            
            // Aplicar "magnetismo" suave em direção ao Pokémon quando está próximo
            if (_vm?.WildWindow != null)
            {
                var ballCenter = new WinPoint(Left + Width / 2, Top + Height / 2);
                var enemyCenter = new WinPoint(
                    _vm.WildWindow.Left + _vm.WildWindow.Width / 2,
                    _vm.WildWindow.Top + _vm.WildWindow.Height / 2
                );
                var toEnemy = enemyCenter - ballCenter;
                double distanceToEnemy = toEnemy.Length;
                
                // Magnetismo aumenta quando está próximo (dentro de 200px)
                if (distanceToEnemy < 200 && distanceToEnemy > 10)
                {
                    double magnetStrength = (200 - distanceToEnemy) / 200; // 0 a 1
                    var magnetForce = toEnemy;
                    magnetForce.Normalize();
                    magnetForce *= magnetStrength * 300; // Força do magnetismo
                    
                    _velocity.X += magnetForce.X * deltaTime;
                    _velocity.Y += magnetForce.Y * deltaTime;
                }
            }
            
            // Aplicar resistência do ar
            _velocity *= AIR_RESISTANCE;
            
            // Atualizar posição
            Left += _velocity.X * deltaTime;
            Top += _velocity.Y * deltaTime;
            
            // Rotação baseada na velocidade da Pokébola
            double speed = _velocity.Length; // Velocidade em px/s
            _currentRotation += speed * deltaTime * 0.8; // Quanto mais rápido, mais gira
            
            var rotateTransform = (PokeballImage.RenderTransform as System.Windows.Media.TransformGroup)?.Children[0] as System.Windows.Media.RotateTransform;
            if (rotateTransform != null)
            {
                rotateTransform.Angle = _currentRotation;
            }
            
            // Verificar colisão com o Pokémon selvagem
            if (_captureState == CaptureState.Flying && _vm?.WildWindow != null)
            {
                var ballCenter = new WinPoint(Left + Width / 2, Top + Height / 2);
                var enemyRect = new Rect(_vm.WildWindow.Left, _vm.WildWindow.Top, _vm.WildWindow.Width, _vm.WildWindow.Height);
                
                if (enemyRect.Contains(ballCenter))
                {
                    // Colidiu! Iniciar sequência de captura
                    StartCaptureSequence(ballCenter, enemyRect);
                    return;
                }
            }
            
            if (Left < _virtualScreenBounds.Left)
            {
                Left = _virtualScreenBounds.Left;
                _velocity.X = -_velocity.X * BOUNCE_DAMPING;
            }
            else if (Left + Width > _virtualScreenBounds.Right)
            {
                Left = _virtualScreenBounds.Right - Width;
                _velocity.X = -_velocity.X * BOUNCE_DAMPING;
            }
            
            if (Top < _virtualScreenBounds.Top)
            {
                Top = _virtualScreenBounds.Top;
                _velocity.Y = -_velocity.Y * BOUNCE_DAMPING;
            }
            else if (Top + Height > _virtualScreenBounds.Bottom)
            {
                Top = _virtualScreenBounds.Bottom - Height;
                _velocity.Y = -_velocity.Y * BOUNCE_DAMPING;
            }
            
            // Parar física se velocidade muito baixa e no chão
            if (_velocity.Length < MIN_VELOCITY && Top + Height >= _virtualScreenBounds.Bottom - 1)
            {
                _physicsTimer?.Stop();
                _velocity = new Vector(0, 0);
                
                // Fade out e reset
                var fadeAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                fadeAnim.Completed += (s, args) =>
                {
                    BeginAnimation(OpacityProperty, null);
                    ResetPosition();
                };
                BeginAnimation(OpacityProperty, fadeAnim);
            }
        }

        private void StartCaptureSequence(WinPoint impactPoint, Rect pokemonBounds)
        {
            _captureState = CaptureState.Impact;
            _capturePhaseStart = DateTime.Now;
            _velocity = new WinVector(0, 0); // Parar movimento
            
            // Desabilitar interação durante a animação
            IsHitTestVisible = false;
            
            // Salvar e manter a Pokébola na posição do impacto
            _impactPosition = impactPoint;
            Left = impactPoint.X - Width / 2;
            Top = impactPoint.Y - Height / 2;
            
            // 1. Flash de impacto
            // (removido - sem janela de efeitos)
            
            // Aguardar 200ms e iniciar abertura
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                StartDissolvePhase(impactPoint, pokemonBounds);
            };
            timer.Start();
        }

        private void StartDissolvePhase(WinPoint ballPosition, Rect pokemonBounds)
        {
            _captureState = CaptureState.Opening;
            _capturePhaseStart = DateTime.Now;
            
            // Esconder o Pokémon selvagem imediatamente
            if (_vm?.WildWindow != null)
            {
                _vm.WildWindow.Visibility = Visibility.Collapsed;
            }
            
            // Animação de abertura da Pokébola (escalar para simular abertura)
            var scaleAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true
            };
            
            var scaleTransform = new System.Windows.Media.ScaleTransform(1, 1, Width / 2, Height / 2);
            RenderTransform = scaleTransform;
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            
            // Aguardar animação terminar e iniciar queda
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                RenderTransform = System.Windows.Media.Transform.Identity;
                // A bola já está na posição de impacto, apenas cai daqui
                StartFallingPhase();
            };
            timer.Start();
        }

        private void StartFallingPhase()
        {
            _captureState = CaptureState.Shaking;
            _capturePhaseStart = DateTime.Now;
            _shakeCount = 0;
            
            // Encontrar em qual monitor a bola está
            var currentScreen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)_impactPosition.X, (int)_impactPosition.Y));
            
            // Usar Bounds (tela toda) para ficar dentro da barra de tarefas
            var screenBounds = currentScreen.Bounds;
            
            // Cair para dentro da barra de tarefas (próximo ao fundo da tela)
            var groundY = screenBounds.Bottom - Height - 5; // 5px acima da borda da tela
            
            var fallAnim = new DoubleAnimation
            {
                From = Top,
                To = groundY,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            fallAnim.Completed += (s, e) => StartShakingPhase();
            BeginAnimation(TopProperty, fallAnim);
        }

        private void StartShakingPhase()
        {
            var shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            var shakePhase = 0.0;
            
            // Salvar posição Y original para manter durante o balanço
            var groundY = Top;
            
            shakeTimer.Tick += (s, e) =>
            {
                shakePhase += 0.3;
                
                // Balançar a Pokébola (rotação alternada)
                _shakeAngle = Math.Sin(shakePhase) * 25; // ±25 graus
                var rotateTransform = (PokeballImage.RenderTransform as System.Windows.Media.TransformGroup)?.Children[0] as System.Windows.Media.RotateTransform;
                if (rotateTransform != null)
                {
                    rotateTransform.Angle = _shakeAngle;
                    // Mudar o ponto de rotação para a base durante o balanço
                    rotateTransform.CenterY = Height; // Rotacionar na base
                }
                
                // Manter a posição Y fixa durante o balanço
                Top = groundY;
                
                // Flash vermelho a cada balanço
                if (Math.Abs(_shakeAngle) > 20)
                {
                    FlashRed();
                    _shakeCount++;
                }
                
                // Após 3 balanços, confirmar captura
                if (_shakeCount >= 3)
                {
                    shakeTimer.Stop();
                    ConfirmCapture();
                }
            };
            
            shakeTimer.Start();
        }

        private void FlashRed()
        {
            var originalOpacity = Opacity;
            
            // Flash rápido
            var flashAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };
            
            BeginAnimation(OpacityProperty, flashAnim);
        }

        private void ConfirmCapture()
        {
            _captureState = CaptureState.Captured;
            
            // Flash final branco forte
            var flash = new System.Windows.Shapes.Ellipse
            {
                Width = 60,
                Height = 60,
                Fill = new System.Windows.Media.RadialGradientBrush
                {
                    GradientStops = new System.Windows.Media.GradientStopCollection
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Colors.White, 0),
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 1)
                    }
                }
            };
            
            // TODO: Adicionar som de "click" de captura confirmada
            
            // Resetar rotação
            var rotateTransform = (PokeballImage.RenderTransform as System.Windows.Media.TransformGroup)?.Children[0] as System.Windows.Media.RotateTransform;
            if (rotateTransform != null)
            {
                var resetRotation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                rotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, resetRotation);
            }
            
            // Aguardar 500ms e finalizar
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                
                // Chamar o método original de captura
                _vm?.TryThrowPokeball();
                
                // Fade out e resetar
                var fadeAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                fadeAnim.Completed += (s2, args) =>
                {
                    BeginAnimation(OpacityProperty, null);
                    // Resetar estado interno
                    _velocity = new WinVector(0, 0);
                    _lastVelocity = new WinVector(0, 0);
                    _currentRotation = 0;
                    _lastFrameTime = DateTime.Now;
                    _captureState = CaptureState.Flying;
                    
                    // Reabilitar interação
                    IsHitTestVisible = true;
                    
                    // Esconder completamente e reposicionar para o novo Pokémon
                    Visibility = Visibility.Collapsed;
                    
                    // Aguardar um pouco e reposicionar acima do novo Pokémon
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_vm?.ShowPokeball == true)
                            {
                                ResetPosition();
                                Opacity = 1.0;
                                Visibility = Visibility.Visible;
                            }
                        });
                    });
                };
                BeginAnimation(OpacityProperty, fadeAnim);
            };
            timer.Start();
        }

        private void ResetPosition()
        {
            if (_vm?.WildWindow != null)
            {
                Left = _vm.WildWindow.Left + (_vm.WildWindow.Width - Width) / 2;
                Top = _vm.WildWindow.Top - 30;
            }
            _velocity = new WinVector(0, 0);
            _lastVelocity = new WinVector(0, 0);
            _currentRotation = 0;
            _lastFrameTime = DateTime.Now;
            
            var rotateTransform = (PokeballImage.RenderTransform as System.Windows.Media.TransformGroup)?.Children[0] as System.Windows.Media.RotateTransform;
            if (rotateTransform != null)
            {
                rotateTransform.Angle = 0;
            }
            
            Opacity = 1.0;
        }
    }
}
