using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using PokeBar.ViewModels;
using SWPoint = System.Windows.Point;
using SWMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PokeBar.Views;

public partial class CaptureOverlayWindow : Window
{
    private const double HoverRadius = 140;
    private const double MinSpinDegrees = 120;

    private MainViewModel? _vm;
    private SpriteWindow? _spriteWindow;
    private WildSpriteWindow? _wildWindow;
    private Rect _virtualBounds;
    private SWPoint? _targetCenter;
    private bool _captureActive;
    private bool _ballVisible;
    private bool _dragging;
    private bool _throwInProgress;
    private SWPoint _dragStart;
    private double _spinAccum;
    private double _lastAngle;
    private bool _hasLastAngle;

    public CaptureOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void Attach(MainViewModel vm, SpriteWindow sprite, WildSpriteWindow wildWindow)
    {
        Detach();
        _vm = vm;
        _spriteWindow = sprite;
        _wildWindow = wildWindow;
        vm.RequestReposition += OnRequestReposition;
        UpdateVirtualBounds();
        UpdateTargetCenter();
    }

    public void Detach()
    {
        if (_vm != null)
        {
            _vm.RequestReposition -= OnRequestReposition;
            _vm = null;
        }
        _spriteWindow = null;
        _wildWindow = null;
    }

    public void EnableCaptureMode()
    {
        _captureActive = true;
        UpdateTargetCenter();
        IsHitTestVisible = true;
        Opacity = 1;
        Visibility = Visibility.Visible;
    }

    public void DisableCaptureMode()
    {
        _captureActive = false;
        CancelDrag();
        HideBall();
        _throwInProgress = false;
        StopThrowAnimation();
        IsHitTestVisible = false;
        Opacity = 0;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateVirtualBounds();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Detach();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateVirtualBounds);
    }

    private void OnRequestReposition(object? sender, SWPoint e)
    {
        UpdateTargetCenter();
    }

    private void UpdateVirtualBounds()
    {
        _virtualBounds = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = _virtualBounds.Left;
        Top = _virtualBounds.Top;
        Width = _virtualBounds.Width;
        Height = _virtualBounds.Height;
        RootCanvas.Width = Width;
        RootCanvas.Height = Height;
        UpdateTargetCenter();
    }

    private void UpdateTargetCenter()
    {
        if (_wildWindow == null)
        {
            _targetCenter = null;
            HideBall();
            return;
        }

        var bounds = _wildWindow.GetWildScreenBounds();
        if (bounds == null)
        {
            _targetCenter = null;
            HideBall();
            return;
        }

        var center = new SWPoint(bounds.Value.Left + bounds.Value.Width / 2, bounds.Value.Top + bounds.Value.Height / 2);
        _targetCenter = ScreenToOverlay(center);
    }

    private void Canvas_MouseMove(object sender, SWMouseEventArgs e)
    {
        var position = e.GetPosition(RootCanvas);

        if (!_captureActive || _throwInProgress)
        {
            if (!_dragging)
            {
                HideBall();
            }
            return;
        }

        if (_dragging)
        {
            UpdateDrag(position);
            return;
        }

        if (_targetCenter.HasValue && Distance(position, _targetCenter.Value) <= HoverRadius)
        {
            ShowBall(position);
        }
        else
        {
            HideBall();
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_captureActive || !_ballVisible || _throwInProgress)
            return;

        _dragging = true;
        _dragStart = e.GetPosition(RootCanvas);
        _spinAccum = 0;
        _hasLastAngle = false;
        CaptureMouse();
        PowerLine.Visibility = Visibility.Visible;
        UpdateDrag(_dragStart);
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        ReleaseMouseCapture();
        PowerLine.Visibility = Visibility.Collapsed;

        if (!_targetCenter.HasValue)
        {
            HideBall();
            return;
        }

        if (_spinAccum < MinSpinDegrees)
        {
            HideBall();
            return;
        }

        var releasePos = e.GetPosition(RootCanvas);
        StartThrow(releasePos, _targetCenter.Value, Math.Min(1.0, _spinAccum / 540.0));
    }

    private void UpdateDrag(SWPoint pointer)
    {
        Canvas.SetLeft(PokeballOverlay, pointer.X - PokeballOverlay.Width / 2);
        Canvas.SetTop(PokeballOverlay, pointer.Y - PokeballOverlay.Height / 2);

        if (_targetCenter.HasValue)
        {
            var target = _targetCenter.Value;
            PowerLine.X1 = pointer.X;
            PowerLine.Y1 = pointer.Y;
            PowerLine.X2 = target.X;
            PowerLine.Y2 = target.Y;

            double angle = Math.Atan2(pointer.Y - target.Y, pointer.X - target.X);
            PokeballRotation.Angle = angle * 180 / Math.PI + 180;

            if (_hasLastAngle)
            {
                double delta = angle - _lastAngle;
                delta = Math.IEEERemainder(delta, Math.PI * 2);
                _spinAccum += Math.Abs(delta * 180 / Math.PI);
            }

            _lastAngle = angle;
            _hasLastAngle = true;
        }
        else
        {
            PowerLine.Visibility = Visibility.Collapsed;
        }
    }

    private void StartThrow(SWPoint start, SWPoint target, double power)
    {
        _throwInProgress = true;
        _ballVisible = true;
        double startLeft = start.X - PokeballOverlay.Width / 2;
        double startTop = start.Y - PokeballOverlay.Height / 2;
        double endLeft = target.X - PokeballOverlay.Width / 2;
        double endTop = target.Y - PokeballOverlay.Height / 2;

        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        double duration = Math.Clamp(320 - power * 120, 160, 320);

        var animX = new DoubleAnimation(startLeft, endLeft, TimeSpan.FromMilliseconds(duration)) { EasingFunction = easing };
        var animY = new DoubleAnimation(startTop, endTop, TimeSpan.FromMilliseconds(duration)) { EasingFunction = easing };
        animY.Completed += (_, __) => OnThrowCompleted();

        PokeballOverlay.BeginAnimation(Canvas.LeftProperty, animX);
        PokeballOverlay.BeginAnimation(Canvas.TopProperty, animY);
    }

    private void OnThrowCompleted()
    {
        _throwInProgress = false;
        StopThrowAnimation();
        HideBall();
        _ = _vm?.TryThrowPokeball();
    }

    private void StopThrowAnimation()
    {
        PokeballOverlay.BeginAnimation(Canvas.LeftProperty, null);
        PokeballOverlay.BeginAnimation(Canvas.TopProperty, null);
    }

    private void ShowBall(SWPoint position)
    {
        _ballVisible = true;
        Canvas.SetLeft(PokeballOverlay, position.X - PokeballOverlay.Width / 2);
        Canvas.SetTop(PokeballOverlay, position.Y - PokeballOverlay.Height / 2);
        if (PokeballOverlay.Visibility != Visibility.Visible)
        {
            PokeballOverlay.Visibility = Visibility.Visible;
        }
    }

    private void HideBall()
    {
        if (_dragging || _throwInProgress)
            return;
        _ballVisible = false;
        PokeballOverlay.Visibility = Visibility.Collapsed;
        PowerLine.Visibility = Visibility.Collapsed;
        _hasLastAngle = false;
        _spinAccum = 0;
    }

    private void CancelDrag()
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
        PowerLine.Visibility = Visibility.Collapsed;
        _hasLastAngle = false;
        _spinAccum = 0;
    }

    private SWPoint ScreenToOverlay(SWPoint screenPoint) => new(screenPoint.X - _virtualBounds.Left, screenPoint.Y - _virtualBounds.Top);

    private static double Distance(SWPoint a, SWPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
