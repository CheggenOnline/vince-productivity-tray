using System.Runtime.InteropServices;

namespace ChristianSpellHelper;

internal static class KeyboardInput
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyX = 0x58;
    private const ushort VirtualKeyC = 0x43;
    private const ushort VirtualKeyV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;

    public static bool WaitForHotkeyRelease(TimeSpan timeout, TimeSpan stableDuration) =>
        InputTimingPolicy.WaitForStableRelease(
            timeout,
            stableDuration,
            () => IsDown(VirtualKeyControl) || IsDown(VirtualKeyShift) || IsDown(VirtualKeyX),
            () => Environment.TickCount64,
            () =>
            {
                Application.DoEvents();
                Thread.Sleep(5);
            });

    public static bool Copy() => SendControlChord(VirtualKeyC);

    public static bool Paste() => SendControlChord(VirtualKeyV);

    public static bool PasteDirect(IntPtr focusWindow)
    {
        const uint pasteMessage = 0x0302;
        const uint abortIfHung = 0x0002;
        return focusWindow != IntPtr.Zero &&
               SendMessageTimeout(focusWindow, pasteMessage, UIntPtr.Zero, IntPtr.Zero, abortIfHung, 500, out _) != IntPtr.Zero;
    }

    private static bool SendControlChord(ushort key)
    {
        var inputs = new[]
        {
            CreateKey(VirtualKeyControl, keyUp: false),
            CreateKey(key, keyUp: false),
            CreateKey(key, keyUp: true),
            CreateKey(VirtualKeyControl, keyUp: true),
        };

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    private static bool IsDown(ushort key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static Input CreateKey(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInputData
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyUp : 0,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInputData Keyboard;

        [FieldOffset(0)]
        public MouseInputData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out UIntPtr result);
}
