using System.Runtime.InteropServices;

namespace ChristianSpellHelper;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly Dictionary<int, Action> _callbacks = [];
    private readonly Dictionary<int, HotkeyDefinition> _definitions = [];
    private readonly IHotkeyNativeApi _native;
    private int _nextId = 0x5600;

    public HotkeyWindow() : this(new WindowsHotkeyNativeApi())
    {
    }

    internal HotkeyWindow(IHotkeyNativeApi native)
    {
        _native = native;
        CreateHandle(new CreateParams
        {
            Caption = "VinceProductivityTrayMessageWindow",
            Parent = new IntPtr(-3), // HWND_MESSAGE
        });
    }

    public bool TryRegister(HotkeyDefinition hotkey, Action callback, out int registrationId)
    {
        registrationId = ++_nextId;
        if (!_native.Register(Handle, registrationId, hotkey.Modifiers | ModNoRepeat, hotkey.VirtualKey))
        {
            registrationId = 0;
            return false;
        }

        _callbacks[registrationId] = callback;
        _definitions[registrationId] = hotkey;
        return true;
    }

    public bool TryReplaceAll(IReadOnlyList<HotkeyBinding> bindings, out IReadOnlyList<string> conflicts)
    {
        var previous = _callbacks.Keys
            .Select(id => new HotkeyBinding(_definitions[id], _callbacks[id], "Previous hotkey"))
            .ToArray();
        UnregisterAll();

        var failures = new List<string>();
        foreach (var binding in bindings)
        {
            if (!TryRegister(binding.Definition, binding.Callback, out _))
            {
                failures.Add($"{binding.Label}: already in use.");
                break;
            }
        }

        if (failures.Count == 0)
        {
            conflicts = [];
            return true;
        }

        UnregisterAll();
        foreach (var binding in previous)
        {
            if (!TryRegister(binding.Definition, binding.Callback, out _))
            {
                failures.Add("A previous hotkey could not be restored; restart the tray app.");
            }
        }

        conflicts = failures;
        return false;
    }

    public HotkeyProbeResult Probe(string text)
    {
        HotkeyDefinition hotkey;
        try { hotkey = HotkeyDefinition.Parse(text); }
        catch (FormatException exception) { return new(HotkeyProbeStatus.Invalid, exception.Message); }
        if (_definitions.Values.Contains(hotkey))
            return new(HotkeyProbeStatus.AlreadyUsedByThisApp, "Already used by this app.");

        var id = ++_nextId;
        if (!_native.Register(Handle, id, hotkey.Modifiers | ModNoRepeat, hotkey.VirtualKey))
            return new(HotkeyProbeStatus.Occupied, "Occupied by Windows or another program.");
        try { return new(HotkeyProbeStatus.Available, "Available to RegisterHotKey."); }
        finally { _ = _native.Unregister(Handle, id); }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey &&
            _callbacks.TryGetValue(message.WParam.ToInt32(), out var callback))
        {
            callback();
            return;
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
        GC.SuppressFinalize(this);
    }

    private void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys.ToArray())
        {
            _ = _native.Unregister(Handle, id);
        }

        _callbacks.Clear();
        _definitions.Clear();
    }

    private sealed class WindowsHotkeyNativeApi : IHotkeyNativeApi
    {
        public bool Register(IntPtr window, int id, uint modifiers, uint virtualKey) =>
            RegisterHotKey(window, id, modifiers, virtualKey);

        public bool Unregister(IntPtr window, int id) => UnregisterHotKey(window, id);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}

internal readonly record struct HotkeyBinding(HotkeyDefinition Definition, Action Callback, string Label);

internal interface IHotkeyNativeApi
{
    bool Register(IntPtr window, int id, uint modifiers, uint virtualKey);
    bool Unregister(IntPtr window, int id);
}

internal enum HotkeyProbeStatus { Available, Occupied, AlreadyUsedByThisApp, Invalid }

internal readonly record struct HotkeyProbeResult(HotkeyProbeStatus Status, string Message);
