using System.Diagnostics;
using System.Runtime.InteropServices;

public static class User32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
    [DllImport("user32.dll")]
    public static extern IntPtr GetClientRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);
    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(IntPtr hWnd);

    public static Size GetWindowSize(Process p)
    {
        var dpiScale = 1.25f;
        GetClientRect(p.MainWindowHandle, out Rect rect);
        return Size.Ceiling(new Size(rect.right, rect.bottom) * dpiScale);
    }
}