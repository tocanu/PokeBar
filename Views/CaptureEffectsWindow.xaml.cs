using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinPoint = System.Windows.Point;
using WinColor = System.Windows.Media.Color;

namespace PokeBar.Views
{
    public partial class CaptureEffectsWindow : Window
    {
        private readonly DispatcherTimer _particleTimer;
        private readonly List<Particle> _particles = new();
        private readonly Random _random;
        private WinPoint _targetPoint; // Para onde as partículas devem ir (centro da Pokébola)
        private bool _isActive = false;

        private class Particle
        {
            public required Ellipse Element { get; set; }
            public WinPoint Position { get; set; }
            public Vector Velocity { get; set; }
            public double Life { get; set; } // 0 a 1
            public double TargetPhase { get; set; } // 0 = dispersão, 1 = sugando
        }

        public CaptureEffectsWindow(Random? random = null)
        {
            InitializeComponent();
            _random = random ?? new Random();
            
            _particleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 60 FPS
            };
            _particleTimer.Tick += UpdateParticles;
        }

        /// <summary>
        /// Inicia o efeito de brilho no ponto de impacto
        /// </summary>
        public void ShowImpactFlash(WinPoint screenPosition)
        {
            var flash = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Colors.White, 0),
                        new GradientStop(WinColor.FromArgb(0, 255, 255, 255), 1)
                    }
                }
            };

            Canvas.SetLeft(flash, screenPosition.X - 5);
            Canvas.SetTop(flash, screenPosition.Y - 5);
            EffectsCanvas.Children.Add(flash);

            // Animação de expansão e fade
            var scaleAnim = new DoubleAnimation
            {
                From = 1,
                To = 8,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            opacityAnim.Completed += (s, e) => EffectsCanvas.Children.Remove(flash);

            var scaleTransform = new ScaleTransform(1, 1, 5, 5);
            flash.RenderTransform = scaleTransform;

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            flash.BeginAnimation(OpacityProperty, opacityAnim);
        }

        /// <summary>
        /// Inicia a dissolução do Pokémon em partículas
        /// </summary>
        public void StartDissolveEffect(Rect pokemonBounds, WinPoint pokeballCenter)
        {
            _targetPoint = pokeballCenter;
            _isActive = true;
            
            // Criar partículas cobrindo toda a área do Pokémon
            int particleCount = 150;
            for (int i = 0; i < particleCount; i++)
            {
                var particle = CreateParticle(pokemonBounds);
                _particles.Add(particle);
                EffectsCanvas.Children.Add(particle.Element);
            }

            _particleTimer.Start();
        }

        private Particle CreateParticle(Rect bounds)
        {
            var ellipse = new Ellipse
            {
                Width = _random.Next(2, 5),
                Height = _random.Next(2, 5),
                Fill = new SolidColorBrush(WinColor.FromArgb(
                    255,
                    (byte)_random.Next(200, 255),
                    (byte)_random.Next(200, 255),
                    (byte)_random.Next(150, 255)
                ))
            };

            var startX = bounds.Left + _random.NextDouble() * bounds.Width;
            var startY = bounds.Top + _random.NextDouble() * bounds.Height;

            Canvas.SetLeft(ellipse, startX);
            Canvas.SetTop(ellipse, startY);

            // Velocidade inicial aleatória (dispersão)
            var angle = _random.NextDouble() * Math.PI * 2;
            var speed = _random.Next(50, 150);

            return new Particle
            {
                Element = ellipse,
                Position = new WinPoint(startX, startY),
                Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                Life = 1.0,
                TargetPhase = 0
            };
        }

        private void UpdateParticles(object? sender, EventArgs e)
        {
            if (!_isActive) return;

            double deltaTime = 0.016; // 60 FPS
            var toRemove = new List<Particle>();

            foreach (var particle in _particles)
            {
                // Fase 1 (0 a 0.3): Dispersão
                // Fase 2 (0.3 a 1.0): Sucção para a Pokébola
                particle.TargetPhase += deltaTime * 1.5; // Velocidade da transição

                if (particle.TargetPhase < 0.3)
                {
                    // Dispersão
                    particle.Position += particle.Velocity * deltaTime;
                    particle.Velocity *= 0.95; // Desacelerar
                }
                else
                {
                    // Sucção para a Pokébola
                    var toTarget = _targetPoint - particle.Position;
                    var distance = toTarget.Length;
                    
                    if (distance < 5)
                    {
                        // Chegou ao destino
                        toRemove.Add(particle);
                        continue;
                    }

                    // Acelerar em direção ao alvo
                    toTarget.Normalize();
                    var pullStrength = 500 * (particle.TargetPhase - 0.3);
                    particle.Velocity = toTarget * pullStrength;
                    particle.Position += particle.Velocity * deltaTime;
                }

                // Atualizar posição visual
                Canvas.SetLeft(particle.Element, particle.Position.X);
                Canvas.SetTop(particle.Element, particle.Position.Y);

                // Fade out ao se aproximar
                var distToTarget = (_targetPoint - particle.Position).Length;
                particle.Element.Opacity = Math.Min(1, distToTarget / 50);
            }

            // Remover partículas que chegaram
            foreach (var particle in toRemove)
            {
                _particles.Remove(particle);
                EffectsCanvas.Children.Remove(particle.Element);
            }

            // Terminar quando todas as partículas sumirem
            if (_particles.Count == 0)
            {
                _particleTimer.Stop();
                _isActive = false;
            }
        }

        public void Stop()
        {
            _particleTimer.Stop();
            _isActive = false;
            _particles.Clear();
            EffectsCanvas.Children.Clear();
        }
    }
}
