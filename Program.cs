using System.Diagnostics;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GenshinDialogueHelper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Screen coordinates must use physical pixels when Windows display scaling is enabled.
        try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { /* Already set. */ }
        ApplicationConfiguration.Initialize();
        Application.Run(new HelperWindow());
    }
}

internal sealed class HelperWindow : Form
{
    private const int ToggleHotkeyId = 1;
    private const int PauseHotkeyId = 2;
    private const int HotkeyMessage = 0x0312;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 90 };
    private readonly Label status = new();
    private readonly Label detection = new();
    private readonly Button toggleButton = new();
    private readonly NumericUpDown intervalInput = new();
    private readonly TextBox keyInput = new();
    private bool enabled;
    private readonly AutomaticDialogueGate inputGate = new();
    private TextDialogueDetector? textDetector;
    private bool scanning;
    private int generation;
    private bool hotkeysReady;
    private long lastLog;
    private long lastActionLog;
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "helper.log");

    public HelperWindow()
    {
        Text = "Genshin Dialogue Helper";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        ClientSize = new Size(300, 215);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        TopMost = true;
        BackColor = Color.FromArgb(27, 30, 39);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 16, area.Top + 50);

        status.SetBounds(16, 12, 270, 25);
        status.Font = new Font(Font, FontStyle.Bold);
        detection.SetBounds(16, 40, 270, 40);
        detection.ForeColor = Color.FromArgb(180, 194, 210);

        toggleButton.SetBounds(16, 85, 268, 34);
        toggleButton.Click += (_, _) => Toggle();

        var speedLabel = new Label { Text = "Interval, ms", ForeColor = ForeColor };
        speedLabel.SetBounds(16, 133, 110, 23);
        intervalInput.SetBounds(125, 130, 65, 27);
        intervalInput.Minimum = 150;
        intervalInput.Maximum = 1000;
        intervalInput.Increment = 25;
        intervalInput.Value = 250;

        var keyLabel = new Label { Text = "Key",ForeColor = ForeColor };
        keyLabel.SetBounds(16, 166, 90, 23);
        keyInput.SetBounds(125, 163, 65, 27);
        keyInput.MaxLength = 1;
        keyInput.Text = "F";
        keyInput.CharacterCasing = CharacterCasing.Upper;

        var hint = new Label { Text = "F8 — on/off    F9 — pause", ForeColor = Color.FromArgb(180, 194, 210) };
        hint.SetBounds(16, 193, 270, 18);

        Controls.AddRange([status, detection, toggleButton, speedLabel, intervalInput, keyLabel, keyInput, hint]);
        SetState("Waiting");
        Log("Started: automatic OCR mode; F8 toggles, F9 pauses.");
        timer.Tick += Tick;
        timer.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        bool a = Native.RegisterHotKey(Handle, ToggleHotkeyId, 0x4000, 0x77); // F8
        bool b = Native.RegisterHotKey(Handle, PauseHotkeyId, 0x4000, 0x78);  // F9
        hotkeysReady = a && b;
        if (!hotkeysReady)
            detection.Text = "F8/F9 are in use by another application.";
        Log($"Hotkeys: F8={a}, F9={b}.");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.UnregisterHotKey(Handle, ToggleHotkeyId);
        Native.UnregisterHotKey(Handle, PauseHotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == HotkeyMessage)
        {
            if (m.WParam.ToInt32() == ToggleHotkeyId) Toggle();
            if (m.WParam.ToInt32() == PauseHotkeyId) Pause();
        }
        base.WndProc(ref m);
    }

    private void Toggle()
    {
        enabled = !enabled;
        generation++;
        inputGate.Reset();
        SetState(enabled ? "On" : "Paused");
        Log($"Automatic mode: {enabled}.");
        if (enabled) BeginInvoke(() =>
        {
            if (!enabled) return;
            ReturnFocusToGame();
            inputGate.Arm(Environment.TickCount64);
        });
    }

    private void Pause()
    {
        generation++;
        inputGate.Reset();
        enabled = false;
        SetState("Paused");
        SystemSounds.Beep.Play();
        Log("F9: paused.");
    }

    private void SetState(string value)
    {
        status.Text = $"Auto dialogue: {value}";
        status.ForeColor = enabled ? Color.FromArgb(99, 220, 151) : Color.FromArgb(255, 200, 105);
        toggleButton.Text = enabled ? "Pause" : "Start";
    }

    private async void Tick(object? sender, EventArgs e)
    {
        if (scanning) return;
        if (!enabled) { detection.Text = "F8 — enable automatic mode"; return; }
        if (!TryGetGameClient(out Rectangle client) || IsNavigationPressed())
        {
            inputGate.Reset();
            detection.Text = "Waiting for dialogue";
            return;
        }

        scanning = true;
        int scanGeneration = generation;
        IntPtr gameWindow = Native.GetForegroundWindow();
        long started = Environment.TickCount64;
        try
        {
            textDetector ??= new TextDialogueDetector();
            using var frame = new Bitmap(client.Width, client.Height);
            using (var graphics = Graphics.FromImage(frame))
                graphics.CopyFromScreen(client.Location, Point.Empty, client.Size, CopyPixelOperation.SourceCopy);
            var result = await textDetector.DetectAsync(frame);
            if (IsDisposed || Disposing || scanGeneration != generation) return;
            bool fresh = Environment.TickCount64 - started <= 1000;
            if (!enabled || !fresh || Native.GetForegroundWindow() != gameWindow ||
                !TryGetGameClient(out Rectangle current) || current != client || IsNavigationPressed())
            {
                inputGate.Reset();
                return;
            }
            detection.Text = result.Marker is null ? "Waiting for dialogue" : $"Dialogue: {result.Marker}";
            PeriodicLog($"OCR: {result.Marker ?? "none"}; top={result.Controls}; bottom={result.Subtitles}; choices={result.Choices}");
            bool click = result.Marker == ContinueDiamondDetector.Marker;
            int interval = click ? Math.Max(1000, (int)intervalInput.Value) : (int)intervalInput.Value;
            if (!inputGate.Update(result.Marker, Environment.TickCount64, interval))
            {
                if (inputGate.NeedsDialogueContext)
                    detection.Text = click
                        ? "Diamond visible; toggle F8 off/on to start"
                        : "Answer outside dialogue; key press blocked";
                return;
            }
            if (click)
            {
                SendContinueClick(gameWindow, client);
                return;
            }
            char key = keyInput.Text.Length == 1 ? keyInput.Text[0] : 'F';
            if (key is < 'A' or > 'Z') { detection.Text = "Key must be a letter A–Z"; return; }
            // Final cancellation check, after OCR has completed and immediately before input.
            if (!enabled || Native.GetForegroundWindow() != gameWindow || IsNavigationPressed()) return;
            SendAdvance(key);
        }
        catch (Exception ex)
        {
            inputGate.Reset();
            detection.Text = "Recognition error; no key presses sent";
            PeriodicLog($"OCR error: {ex.Message}");
        }
        finally { scanning = false; }
    }

    private static bool IsNavigationPressed() => new[] { 0x57, 0x41, 0x53, 0x44, 0x20, 0x1B, 0x4D }
        .Any(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0);

    private void SendAdvance(char key)
    {
        uint scan = Native.MapVirtualKeyW(key, 0);
        if (scan == 0) return;
        var inputs = new[]
        {
            new Native.Input { Type = 1, Keyboard = new Native.KeyboardInput { Scan = (ushort)scan, Flags = 0x0008 } },
            new Native.Input { Type = 1, Keyboard = new Native.KeyboardInput { Scan = (ushort)scan, Flags = 0x000A } }
        };
        uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
        if (sent != inputs.Length)
        {
            detection.Text = "Windows rejected the key press";
            PeriodicLog($"SendInput sent {sent} of {inputs.Length}; error {Marshal.GetLastWin32Error()}.");
            return;
        }
        if (Environment.TickCount64 - lastActionLog >= 5000)
        {
            lastActionLog = Environment.TickCount64;
            Log($"Command: key {key}; Windows accepted {sent} events.");
        }
    }

    private void SendContinueClick(IntPtr gameWindow, Rectangle client)
    {
        // Click only the game surface, never whichever window happens to be
        // under the user's cursor. Refuse if an overlay covers the target.
        var target = new Native.Point(client.Left + client.Width / 2, client.Top + client.Height / 2);
        if (!enabled || IsNavigationPressed() || Native.GetForegroundWindow() != gameWindow ||
            Native.WindowFromPoint(target) != gameWindow) return;
        int left = Native.GetSystemMetrics(76), top = Native.GetSystemMetrics(77);
        int width = Native.GetSystemMetrics(78), height = Native.GetSystemMetrics(79);
        if (width <= 1 || height <= 1) return;
        int x = (int)Math.Round((target.X - left) * 65535.0 / (width - 1));
        int y = (int)Math.Round((target.Y - top) * 65535.0 / (height - 1));
        var inputs = new[]
        {
            new Native.Input { Type = 0, Mouse = new Native.MouseInput { X = x, Y = y, Flags = 0xC001 } },
            new Native.Input { Type = 0, Mouse = new Native.MouseInput { Flags = 0x0002 } },
            new Native.Input { Type = 0, Mouse = new Native.MouseInput { Flags = 0x0004 } }
        };
        uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
        if (sent != inputs.Length)
        {
            // Release defensively if Windows accepted the button-down but not up.
            Native.SendInput(1, new[] { inputs[2] }, Marshal.SizeOf<Native.Input>());
            detection.Text = "Windows rejected the click";
            Log($"Click: accepted {sent} of {inputs.Length} events.");
            return;
        }
        detection.Text = "Insert: click sent";
        Log($"Command: click on insert ({target.X},{target.Y}); Windows accepted {sent} events.");
    }

    private void ReturnFocusToGame()
    {
        foreach (string name in new[] { "GenshinImpact", "YuanShen" })
        foreach (Process process in Process.GetProcessesByName(name))
        using (process)
        {
            IntPtr window = process.MainWindowHandle;
            if (window != IntPtr.Zero && !Native.IsIconic(window) && Native.SetForegroundWindow(window)) return;
        }
    }

    private void PeriodicLog(string message)
    {
        long now = Environment.TickCount64;
        if (now - lastLog < 5000) return;
        lastLog = now;
        Log(message);
    }

    private static void Log(string message)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}"); }
        catch { /* Logging must never interrupt the UI. */ }
    }

    private static bool TryGetGameClient(out Rectangle client)
    {
        client = Rectangle.Empty;
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (!process.ProcessName.Equals("GenshinImpact", StringComparison.OrdinalIgnoreCase) &&
                !process.ProcessName.Equals("YuanShen", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch { return false; }

        if (!Native.GetClientRect(hwnd, out Native.Rect r)) return false;
        var topLeft = new Native.Point(0, 0);
        if (!Native.ClientToScreen(hwnd, ref topLeft)) return false;
        client = new Rectangle(topLeft.X, topLeft.Y, r.Right - r.Left, r.Bottom - r.Top);
        return client.Width >= 640 && client.Height >= 360;
    }


}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyboardInput Keyboard;
        [FieldOffset(8)] public MouseInput Mouse;
    }

    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKeyW(uint code, uint mapType);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
}
