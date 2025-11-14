using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PokeBar.Models;
using PokeBar.Utils;
using PokeBar.ViewModels;
using System.Windows.Threading;

namespace PokeBar.Views
{
    public partial class WildSpriteWindow : Window
    {
        private IntPtr _hwnd;
        private MainViewModel? _vm;

        public WildSpriteWindow()
        {
            InitializeComponent();
            Loaded += WildSpriteWindow_Loaded;
            Unloaded += WildSpriteWindow_Unloaded;
            Deactivated += WildSpriteWindow_Deactivated;
        }

        private void WildSpriteWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            EnsureTopmost();
            if (DataContext is MainViewModel vm)
            {
                _vm = vm;
                vm.RequestWildReposition += OnRequestWildReposition;
            }
        }

        private void WildSpriteWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.RequestWildReposition -= OnRequestWildReposition;
                _vm = null;
            }
        }

        private void OnRequestWildReposition(object? sender, System.Windows.Point p)
        {
            Left = p.X;
            Top = p.Y;
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Clique no inimigo para perseguir
            _vm?.OnWildSpriteClicked();
        }

        private void WildSpriteWindow_Deactivated(object? sender, EventArgs e)
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

        public Rect? GetWildScreenBounds()
        {
            if (!IsLoaded || WildImage.ActualWidth <= 0 || WildImage.ActualHeight <= 0)
                return null;
            try
            {
                var transform = WildImage.TransformToAncestor(this);
                var rect = transform.TransformBounds(new Rect(0, 0, WildImage.ActualWidth, WildImage.ActualHeight));
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
