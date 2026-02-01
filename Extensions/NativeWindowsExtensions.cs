using System.Runtime.InteropServices;
using Veldrid.Sdl2;

namespace UniversalUmap.Rendering.Extensions;

public static class NativeWindowsExtensions
{
    public static int GetDisplayRefreshRate()
    {
        Sdl2Native.SDL_Init(SDLInitFlags.Video);
        unsafe
        {
            SDL_DisplayMode displayMode = new SDL_DisplayMode();
            if (Sdl2Native.SDL_GetCurrentDisplayMode(0, &displayMode) == 0)
                return displayMode.refresh_rate;
        }
        return 60;
    }
    
    public static uint EnableHighPrecisionTimer(uint uMilliseconds)
    {
        if (OperatingSystem.IsWindows())
        {
            return TimeBeginPeriod(uMilliseconds);
        }
        return 0; // Not needed on Linux
    }
    
    public static uint DisableHighPrecisionTimer(uint uMilliseconds)
    {
        if (OperatingSystem.IsWindows())
        {
            return TimeEndPeriod(uMilliseconds);
        }
        return 0; // Not needed on Linux
    }

    public static void MakeBorderless(IntPtr hWnd)
    {
        if (OperatingSystem.IsWindows())
        {
            MakeBorderlessWindows(hWnd);
        }
        // On Linux with Avalonia's NativeControlHost, borderless is handled by the host
        // or via SDL2 window flags when creating the window
    }

    // Windows-specific P/Invoke declarations and implementation
#if WINDOWS || NET5_0_OR_GREATER
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);
    
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    
    private static void MakeBorderlessWindows(IntPtr hWnd)
    {
        IntPtr style = GetWindowLong(hWnd, GWL_STYLE);
        IntPtr newStyle = new IntPtr(style.ToInt32() & ~WS_CAPTION & ~WS_THICKFRAME);
        SetWindowLong(hWnd, GWL_STYLE, newStyle);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
    }
#else
    private static uint TimeBeginPeriod(uint uMilliseconds) => 0;
    private static uint TimeEndPeriod(uint uMilliseconds) => 0;
    private static void MakeBorderlessWindows(IntPtr hWnd) { }
#endif
}