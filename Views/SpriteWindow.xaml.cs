using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
                vm.RequestPlayerJump += OnRequestPlayerJump;
            }
        }

        private void SpriteWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.RequestReposition -= OnRequestReposition;
                _vm.BattleClashRequested -= OnBattleClashRequested;
                _vm.RequestPlayerJump -= OnRequestPlayerJump;
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

        private void OnRequestPlayerJump(object? sender, EventArgs e)
        {
            PlayJumpAnimation();
        }

        private void PlayJumpAnimation()
        {
            var jump = new DoubleAnimation
            {
                From = 0,
                To = -12,
                Duration = TimeSpan.FromMilliseconds(150),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            PlayerTranslate.BeginAnimation(TranslateTransform.YProperty, jump);
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
            _vm?.OnClicked();
            PlayBounce();
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
        }

        public Rect? GetPlayerScreenBounds() => GetElementScreenBounds(PlayerImage);

        private Rect? GetElementScreenBounds(FrameworkElement element)
        {
            if (!IsLoaded || element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return null;
            try
            {
                var transform = element.TransformToAncestor(this);
                var rect = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                var topLeft = PointToScreen(new System.Windows.Point(rect.Left, rect.Top));
                return new Rect(topLeft, new System.Windows.Size(rect.Width, rect.Height));
            }
            catch
            {
                return null;
            }
        }
    }
}
