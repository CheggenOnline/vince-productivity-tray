using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace ChristianSpellHelper;

internal sealed record SelectionIdentity(
    IntPtr ForegroundWindow,
    IntPtr FocusWindow,
    string FocusClassName,
    string? AutomationRuntimeId,
    string? AutomationSelectedText,
    bool IsPassword)
{
    public bool IsClassicEditControl =>
        FocusClassName.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
        FocusClassName.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);

    public bool MatchesTarget(SelectionIdentity current) =>
        current.ForegroundWindow == ForegroundWindow &&
        (FocusWindow == IntPtr.Zero || current.FocusWindow == FocusWindow) &&
        (AutomationRuntimeId is null || current.AutomationRuntimeId == AutomationRuntimeId) &&
        !current.IsPassword;

    public bool MatchesCurrentTarget() => MatchesTarget(Capture());

    public static SelectionIdentity Capture()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return new SelectionIdentity(IntPtr.Zero, IntPtr.Zero, string.Empty, null, null, false);
        }

        var threadId = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        _ = GetGUIThreadInfo(threadId, ref info);

        string? runtimeId = null;
        string? selectedText = null;
        var focusClassName = GetWindowClassName(info.FocusWindow);
        var isPassword = IsWin32PasswordEdit(info.FocusWindow, focusClassName);
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is not null)
            {
                isPassword |= element.Current.IsPassword;
                runtimeId = string.Join('.', element.GetRuntimeId());
                if (!isPassword && element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject))
                {
                    var ranges = ((TextPattern)patternObject).GetSelection();
                    if (ranges.Length == 1)
                    {
                        selectedText = ranges[0].GetText(20_001);
                    }
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            // The clipboard confirmation still provides a safe fallback.
        }
        catch (InvalidOperationException)
        {
            // Some application accessibility providers fail while focus is changing.
        }

        return new SelectionIdentity(foreground, info.FocusWindow, focusClassName, runtimeId, selectedText, isPassword);
    }

    public bool MatchesCurrentSelection(string clipboardText)
    {
        var current = Capture();
        if (!MatchesTarget(current))
        {
            return false;
        }

        if (current.AutomationSelectedText is not null &&
            !NormalizeLineEndings(current.AutomationSelectedText).Equals(
                NormalizeLineEndings(clipboardText),
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsWin32PasswordEdit(IntPtr window, string className)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        if (!className.Contains("Edit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const int styleIndex = -16;
        const long passwordStyle = 0x20;
        var style = GetWindowLongPtr(window, styleIndex).ToInt64();
        return (style & passwordStyle) != 0;
    }

    private static string GetWindowClassName(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return string.Empty;
        }

        var className = new StringBuilder(128);
        _ = GetClassName(window, className, className.Capacity);
        return className.ToString();
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public int Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public System.Drawing.Rectangle CaretRectangle;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
