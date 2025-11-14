using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PokeBar.Utils;

namespace PokeBar.Services;

public enum TaskbarEdge { Left, Top, Right, Bottom }

public class TaskbarInfo
{
    public Rect Bounds { get; init; }
    public TaskbarEdge Edge { get; init; }
    public IntPtr MonitorHandle { get; init; }
}

public class TaskbarService
{
    public event EventHandler<TaskbarInfo>? TaskbarChanged;

    private TaskbarInfo[]? _cachedTaskbars;
    private DateTime _cacheTime = DateTime.MinValue;
    private const double CACHE_VALIDITY_SECONDS = 10; // Fallback: revalidar a cada 10s

    public TaskbarService()
    {
        SystemEvents.DisplaySettingsChanged += (_, __) => InvalidateCache();
        
        // Detectar mudanças na taskbar (reposicionamento, auto-hide)
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.Desktop)
            {
                InvalidateCache();
            }
        };
    }

    private void InvalidateCache()
    {
        _cachedTaskbars = null;
        RaiseChanged();
    }

    public TaskbarInfo GetTaskbarInfo()
    {
        var hTaskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        var abd = new NativeMethods.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>() };
        uint result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref abd);
        var taskbarMonitor = NativeMethods.MonitorFromWindow(hTaskbar, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (result == 1)
        {
            var edge = abd.uEdge switch
            {
                NativeMethods.ABE_LEFT => TaskbarEdge.Left,
                NativeMethods.ABE_TOP => TaskbarEdge.Top,
                NativeMethods.ABE_RIGHT => TaskbarEdge.Right,
                _ => TaskbarEdge.Bottom
            };
            return new TaskbarInfo { Bounds = abd.rc.ToRect(), Edge = edge, MonitorHandle = taskbarMonitor };
        }
        // Fallback: assume bottom across primary work area edge
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (NativeMethods.GetMonitorInfo(taskbarMonitor, ref mi))
        {
            var tbHeight = Math.Abs((mi.rcMonitor.Bottom - mi.rcWork.Bottom));
            var r = new Rect(mi.rcMonitor.Left, mi.rcMonitor.Bottom - tbHeight, mi.rcMonitor.Right - mi.rcMonitor.Left, tbHeight);
            return new TaskbarInfo { Bounds = r, Edge = TaskbarEdge.Bottom, MonitorHandle = taskbarMonitor };
        }
        return new TaskbarInfo { Bounds = new Rect(0, SystemParameters.PrimaryScreenHeight - 40, SystemParameters.PrimaryScreenWidth, 40), Edge = TaskbarEdge.Bottom, MonitorHandle = taskbarMonitor };
    }

    public TaskbarInfo[] GetAllTaskbars()
    {
        // Retornar cache se válido
        if (_cachedTaskbars != null && (DateTime.UtcNow - _cacheTime).TotalSeconds < CACHE_VALIDITY_SECONDS)
        {
            return _cachedTaskbars;
        }

        // Recalcular e cachear
        var result = new System.Collections.Generic.List<TaskbarInfo>();
        var targets = new System.Collections.Generic.HashSet<IntPtr>();
        NativeMethods.EnumWindows((h, _) =>
        {
            var sb = new System.Text.StringBuilder(256);
            NativeMethods.GetClassName(h, sb, sb.Capacity);
            var cls = sb.ToString();
            if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
            {
                targets.Add(h);
            }
            return true;
        }, IntPtr.Zero);

        foreach (var h in targets)
        {
            if (!NativeMethods.GetWindowRect(h, out var r)) continue;
            var rect = r.ToRect();
            // Deduz a borda pela proporção da barra no monitor correspondente
            var mon = NativeMethods.MonitorFromWindow(h, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(mon, ref mi)) continue;
            var m = mi.rcMonitor.ToRect();
            TaskbarEdge edge;
            if (Math.Abs(rect.Top - m.Top) < 2 && rect.Height < rect.Width) edge = TaskbarEdge.Top;
            else if (Math.Abs(rect.Bottom - m.Bottom) < 2 && rect.Height < rect.Width) edge = TaskbarEdge.Bottom;
            else if (Math.Abs(rect.Left - m.Left) < 2 && rect.Width < rect.Height) edge = TaskbarEdge.Left;
            else edge = TaskbarEdge.Right;

            result.Add(new TaskbarInfo { Bounds = rect, Edge = edge, MonitorHandle = mon });
        }

        // Ordena por X (esquerda->direita) para caminhada sequencial
        result.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        
        // Atualizar cache
        _cachedTaskbars = result.ToArray();
        _cacheTime = DateTime.UtcNow;
        
        return _cachedTaskbars;
    }

    public bool IsFullscreenActive()
    {
        var h = NativeMethods.GetForegroundWindow();
        if (h == IntPtr.Zero) return false;
        if (!NativeMethods.GetWindowRect(h, out var rect)) return false;
        var mon = NativeMethods.MonitorFromWindow(h, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(mon, ref mi)) return false;
        var fg = rect.ToRect();
        var monBounds = mi.rcMonitor.ToRect();
        // Consider fullscreen if window size covers the monitor bounds almost exactly
        return Math.Abs(fg.Width - monBounds.Width) < 2 && Math.Abs(fg.Height - monBounds.Height) < 2;
    }

    public bool IsMonitorFullscreen(IntPtr monitorHandle)
    {
        if (monitorHandle == IntPtr.Zero)
            return false;
        var h = NativeMethods.GetForegroundWindow();
        if (h == IntPtr.Zero) return false;
        var fgMon = NativeMethods.MonitorFromWindow(h, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (fgMon == IntPtr.Zero || fgMon != monitorHandle)
            return false;
        if (!NativeMethods.GetWindowRect(h, out var rect))
            return false;
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitorHandle, ref mi))
            return false;
        var fg = rect.ToRect();
        var monBounds = mi.rcMonitor.ToRect();
        return Math.Abs(fg.Width - monBounds.Width) < 2 && Math.Abs(fg.Height - monBounds.Height) < 2;
    }

    public void RaiseChanged()
    {
        TaskbarChanged?.Invoke(this, GetTaskbarInfo());
    }
}
