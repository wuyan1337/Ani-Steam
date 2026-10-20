using System.Runtime.InteropServices;
using AniSteam;

internal static class WindowChecks
{
    private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, EnumWindowProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public static HashSet<IntPtr> VisibleWindows()
    {
        var result = new HashSet<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(), (window, _) => {
            if (IsWindowVisible(window)) result.Add(window);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void CheckDispatcher(Action<bool, string> check)
    {
        using var dispatcher = new MessageDispatcher { Text = "AniSteam.Dispatcher.Test." + Guid.NewGuid() };
        IntPtr handle = dispatcher.Handle;
        check(FindWindowEx(new IntPtr(-3), IntPtr.Zero, null, dispatcher.Text) == handle,
            "background dispatcher belongs to HWND_MESSAGE, outside the desktop window tree");
        check(!IsWindowVisible(handle), "background dispatcher cannot appear on screen");
    }
}
