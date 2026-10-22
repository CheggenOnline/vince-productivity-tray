using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ChristianSpellHelper;

internal interface IRecorderController : IDisposable
{
    string Status { get; }
    bool IsRecording { get; }
    bool IsAvailable { get; }
    event EventHandler? StatusChanged;
    void Toggle();
    void Stop();
}

internal sealed class PowerShellRecorderController : IRecorderController
{
    private const string TeamsMicrophoneKey =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\MSTeams_8wekyb3d8bbwe";
    private readonly string _packagedAssets;
    private readonly string _runtimeAssets;
    private readonly Func<string> _outputFolder;
    private readonly Func<bool> _autoRecordEnabled;
    private readonly System.Windows.Forms.Timer _timer;
    private Process? _process;
    private bool _autoRecording;
    private readonly MeetingAutoRecordState _meetingState = new();
    private int _teamPollTicks;
    private string _status = "Ready";

    public PowerShellRecorderController(string packagedAssets, Func<string> outputFolder, Func<bool> autoRecordEnabled)
    {
        _packagedAssets = packagedAssets;
        _runtimeAssets = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VinceProductivityTray", "RecorderRuntime");
        _outputFolder = outputFolder;
        _autoRecordEnabled = autoRecordEnabled;
        IsAvailable = RequiredPackagedFiles().All(File.Exists);
        if (!IsAvailable) _status = "Recorder assets are missing";
        _timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        _timer.Tick += (_, _) => Poll();
    }

    public string Status => _status;
    public bool IsRecording => _process is { HasExited: false };
    public bool IsAvailable { get; }
    public event EventHandler? StatusChanged;

    public void Toggle()
    {
        if (IsRecording)
        {
            BringRecordingWindowToFront();
            return;
        }

        Start(auto: false);
    }

    public void Stop()
    {
        if (!IsRecording) return;
        var window = FindRecorderWindow(_process!.Id);
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The recorder window is not ready yet. Wait a moment and choose Stop recording again.");
        }

        _status = "Stopping…";
        StatusChanged?.Invoke(this, EventArgs.Empty);
        if (!PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero))
        {
            throw new InvalidOperationException("Windows could not send a graceful stop request.");
        }
    }

    private void Start(bool auto)
    {
        if (!IsAvailable) throw new InvalidOperationException(_status);
        var outputFolder = SettingsValidator.NormalizeFolder(_outputFolder(), "Recording folder");
        PrepareRuntime();
        Directory.CreateDirectory(outputFolder);
        File.WriteAllText(
            Path.Combine(_runtimeAssets, "innstillinger.txt"),
            BuildSettingsText(outputFolder),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-WindowStyle");
        info.ArgumentList.Add("Hidden");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(Path.Combine(_runtimeAssets, "opptak.ps1"));
        if (auto) info.ArgumentList.Add("-Auto");

        _process = Process.Start(info)
            ?? throw new InvalidOperationException("Windows did not start the recorder process.");
        _autoRecording = auto;
        _status = auto ? "Teams auto-recording starting…" : "Recording starting…";
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PrepareRuntime()
    {
        Directory.CreateDirectory(_runtimeAssets);
        Directory.CreateDirectory(Path.Combine(_runtimeAssets, "lib"));
        foreach (var source in RequiredPackagedFiles())
        {
            var relative = Path.GetRelativePath(_packagedAssets, source);
            var target = Path.Combine(_runtimeAssets, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private IEnumerable<string> RequiredPackagedFiles()
    {
        yield return Path.Combine(_packagedAssets, "opptak.ps1");
        yield return Path.Combine(_packagedAssets, "felles.ps1");
        yield return Path.Combine(_packagedAssets, "lib", "NAudio.Core.dll");
        yield return Path.Combine(_packagedAssets, "lib", "NAudio.Wasapi.dll");
    }

    private void Poll()
    {
        if (_process is not null)
        {
            try
            {
                if (_process.HasExited)
                {
                    _process.Dispose();
                    _process = null;
                    _status = "Ready";
                    _autoRecording = false;
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var wanted = _autoRecording ? "Teams auto-recording" : "Recording";
                    if (_status != wanted)
                    {
                        _status = wanted;
                        StatusChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                _process = null;
            }
        }

        if (++_teamPollTicks < 4) return;
        _teamPollTicks = 0;
        var inCall = IsTeamsUsingMicrophone();
        if (_meetingState.Observe(inCall, _autoRecordEnabled(), IsRecording))
        {
            if (IsAvailable)
            {
                try { Start(auto: true); }
                catch (Exception exception)
                {
                    LocalLog.Write("Teams automatic recording could not start", exception);
                    _status = "Teams auto-start failed";
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    internal static bool IsTeamsUsingMicrophone()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TeamsMicrophoneKey, writable: false);
            return key?.GetValue("LastUsedTimeStop") is long value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    internal static string BuildSettingsText(string outputFolder) =>
        string.Join(Environment.NewLine,
        [
            "# Generated by Vince Productivity Tray. Local transcription is parked.",
            $"Utmappe = {outputFolder}",
            "Transkriber = nei",
            "Spraak = no",
            "Traader = auto",
            string.Empty,
        ]);

    private void BringRecordingWindowToFront()
    {
        var window = _process is null ? IntPtr.Zero : FindRecorderWindow(_process.Id);
        if (window != IntPtr.Zero) _ = SetForegroundWindow(window);
    }

    private static IntPtr FindRecorderWindow(int processId)
    {
        var match = IntPtr.Zero;
        _ = EnumWindows((window, parameter) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner != processId || !IsWindowVisible(window)) return true;
            var title = new StringBuilder(128);
            _ = GetWindowText(window, title, title.Capacity);
            if (!title.ToString().Equals("Moteopptak", StringComparison.OrdinalIgnoreCase)) return true;
            match = window;
            return false;
        }, IntPtr.Zero);
        return match;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _process?.Dispose();
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}

internal sealed class MeetingAutoRecordState
{
    private bool _wasInCall;

    public bool Observe(bool isInCall, bool enabled, bool isRecording)
    {
        var risingEdge = isInCall && !_wasInCall;
        _wasInCall = isInCall;
        return risingEdge && enabled && !isRecording;
    }
}

