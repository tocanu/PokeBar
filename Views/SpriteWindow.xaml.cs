using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PokeBar.ViewModels;
using System.Windows.Interop;
using PokeBar.Utils;

namespace PokeBar.Views
{
    public partial class SpriteWindow : Window
    {
        private IntPtr _hwnd;
        private MainViewModel? _vm;
        private bool _isThrowDrag;
        private System.Windows.Point _dragStart;
        public SpriteWindow()
        {
            InitializeComponent();
            Loaded += SpriteWindow_Loaded;
            Unloaded += SpriteWindow_Unloaded;
            Deactivated += SpriteWindow_Deactivated;
        }

        private void SpriteWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            EnsureTopmost();
            if (DataContext is MainViewModel vm)
            {
                _vm = vm;
                vm.RequestReposition += OnRequestReposition;
                vm.BattleClashRequested += OnBattleClashRequested;
            }
        }

        private void SpriteWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.RequestReposition -= OnRequestReposition;
                _vm.BattleClashRequested -= OnBattleClashRequested;
                _vm = null;
            }
        }

        private void OnRequestReposition(object? sender, System.Windows.Point p)
        {
            Left = p.X;
            Top = p.Y;
        }

        private void OnBattleClashRequested(object? sender, EventArgs e)
        {
            PlayClashAnimation();
        }

        private void SpriteWindow_Deactivated(object? sender, EventArgs e)
        {
            EnsureTopmost();
        }

        private void EnsureTopmost()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null)
                return;

            if (_vm.InBattle && _vm.WildVisible)
            {
                _isThrowDrag = true;
                _dragStart = e.GetPosition(this);
                CaptureMouse();
            }
            else
            {
                _vm.OnClicked();
                PlayBounce();
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // reserved for future feedback
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isThrowDrag)
                return;

            _isThrowDrag = false;
            ReleaseMouseCapture();
            if (_vm == null)
                return;
            var end = e.GetPosition(this);
            var drag = end - _dragStart;
            if (_vm.CanThrowPokeball(new System.Windows.Vector(drag.X, drag.Y)))
            {
                AnimatePokeballThrow();
                _ = _vm.TryThrowPokeball();
            }
        }

        private void PlayBounce()
        {
            var anim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.85,
                Duration = TimeSpan.FromMilliseconds(75),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BounceScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);
        }

        private void PlayClashAnimation()
        {
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var playerAnim = new DoubleAnimation
            {
                From = 0,
                To = 18,
                Duration = TimeSpan.FromMilliseconds(160),
                AutoReverse = true,
                EasingFunction = easing
            };
            PlayerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, playerAnim);

            var enemyAnim = new DoubleAnimation
            {
                From = 0,
                To = -18,
                Duration = TimeSpan.FromMilliseconds(160),
                AutoReverse = true,
                EasingFunction = easing
            };
            EnemyTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, enemyAnim);
        }

        private void AnimatePokeballThrow()
        {
            if (!EnemyImage.IsVisible)
                return;

            var start = PlayerImage.TranslatePoint(new System.Windows.Point(PlayerImage.ActualWidth / 2, PlayerImage.ActualHeight / 2), FxCanvas);
            var end = EnemyImage.TranslatePoint(new System.Windows.Point(EnemyImage.ActualWidth / 2, EnemyImage.ActualHeight / 2), FxCanvas);
            double startLeft = start.X - (PokeballImage.Width / 2);
            double startTop = start.Y - (PokeballImage.Height / 2);
            double endLeft = end.X - (PokeballImage.Width / 2);
            double endTop = end.Y - (PokeballImage.Height / 2);

            PokeballImage.BeginAnimation(Canvas.LeftProperty, null);
            PokeballImage.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(PokeballImage, startLeft);
            Canvas.SetTop(PokeballImage, startTop);
            PokeballImage.Visibility = Visibility.Visible;

            var animX = new DoubleAnimation
            {
                From = startLeft,
                To = endLeft,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var animY = new DoubleAnimation
            {
                From = startTop,
                To = endTop,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            animX.Completed += (_, __) =>
            {
                PokeballImage.Visibility = Visibility.Collapsed;
                PokeballImage.BeginAnimation(Canvas.LeftProperty, null);
                PokeballImage.BeginAnimation(Canvas.TopProperty, null);
            };
            PokeballImage.BeginAnimation(Canvas.LeftProperty, animX);
            PokeballImage.BeginAnimation(Canvas.TopProperty, animY);
        }
    }
}
