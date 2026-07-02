using System.Runtime.InteropServices;

namespace MarkdownBlaze;

/// <summary>
/// Sets a window's icon directly via WM_SETICON / the window class. Photino's SetIconFile only
/// updates the Windows title bar, so we push the .ico onto the HWND once the native window exists.
/// Windows-only: guarded so it is a no-op (and never touches user32) on other platforms.
/// </summary>
internal static class NativeIcon
{
    private const int WM_SETICON = 0x0080;
    private const nint ICON_SMALL = 0, ICON_BIG = 1;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(nint hinst, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern nint SetClassLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    public static void Apply(nint hwnd, string icoPath)
    {
        if (!OperatingSystem.IsWindows() || hwnd == nint.Zero) return;
        try
        {
            // Windows 11's taskbar only adopts ICON_BIG when it's a *large* image — a 16/32px icon
            // updates just the title bar and leaves the taskbar generic. So load the big icon at 256px.
            var big = LoadImage(nint.Zero, icoPath, IMAGE_ICON, 256, 256, LR_LOADFROMFILE);
            var small = LoadImage(nint.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            if (big != nint.Zero)
            {
                SendMessage(hwnd, WM_SETICON, ICON_BIG, big);
                SetClassLongPtr(hwnd, GCLP_HICON, big);   // the shell/taskbar also reads the class icon
            }
            if (small != nint.Zero)
            {
                SendMessage(hwnd, WM_SETICON, ICON_SMALL, small);
                SetClassLongPtr(hwnd, GCLP_HICONSM, small);
            }
        }
        catch { /* best-effort */ }
    }
}
