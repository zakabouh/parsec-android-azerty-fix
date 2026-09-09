using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private static Mutex _singleInstance;

    [STAThread]
    private static void Main()
    {
        bool created;
        _singleInstance = new Mutex(true, "Local\\ParsecAzertyFix", out created);
        if (!created)
            return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new FixContext());
    }
}

internal sealed class FixContext : ApplicationContext
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_LOWER_IL_INJECTED = 0x00000002;
    private const uint LLKHF_INJECTED = 0x00000010;
    private const uint LLKHF_UP = 0x00000080;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const ulong OwnInputMarker = 0x504152534543415AUL; // "PARSECAZ"

    private static readonly Regex ConnectedLine = new Regex(
        @"^\[I [^\]]+\] .+ connected\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DisconnectedLine = new Regex(
        @"^\[I [^\]]+\] .+ disconnected\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NetworkLine = new Regex(
        @"^\[D [^\]]+\] net\s*=\s*[^|]+\|.*\|(?<port>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ParsecRestartLine = new Regex(
        @"^\[D [^\]]+\] log: Parsec release",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _parsecLog;
    private readonly string _statusLog;
    private readonly string _androidClientsFile;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _learnCurrentItem;
    private readonly ToolStripMenuItem _forgetClientsItem;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly System.Windows.Forms.Timer _connectionTimer;
    private readonly LowLevelKeyboardProc _callback;
    private readonly HashSet<int> _androidClientPorts = new HashSet<int>();
    private IntPtr _hook;
    private bool _enabled = true;
    private bool _parsecConnected;
    private bool _sessionAndroidMode;
    private string _connectionSignature = String.Empty;
    private ParsecConnectionSnapshot _connectionSnapshot = new ParsecConnectionSnapshot();
    private bool _remoteShift;
    private bool _remoteControl;
    private bool _remoteAlt;
    private bool _composeArmed;
    private uint _composeConsumedScan;
    private DateTime _composeArmedAt;

    public FixContext()
    {
        string localDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ParsecAzertyFix");
        Directory.CreateDirectory(localDir);
        _statusLog = Path.Combine(localDir, "status.log");
        _androidClientsFile = Path.Combine(localDir, "android-clients.txt");
        LoadAndroidClientPorts();
        _parsecLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Parsec",
            "log.txt");

        _statusItem = new ToolStripMenuItem("État : démarrage…");
        _statusItem.Enabled = false;
        _learnCurrentItem = new ToolStripMenuItem("Reconnaître cette connexion comme Android");
        _learnCurrentItem.Click += delegate { LearnCurrentConnection(); };
        _forgetClientsItem = new ToolStripMenuItem("Oublier les appareils Android enregistrés");
        _forgetClientsItem.Click += delegate { ForgetAndroidClients(); };

        _enabledItem = new ToolStripMenuItem("Correcteur activé (interrupteur général)");
        _enabledItem.Checked = true;
        _enabledItem.CheckOnClick = true;
        _enabledItem.CheckedChanged += delegate
        {
            _enabled = _enabledItem.Checked;
            UpdateTray();
            WriteStatus("Correction " + (_enabled ? "activée" : "désactivée"));
        };

        var exitItem = new ToolStripMenuItem("Quitter");
        exitItem.Click += delegate { ExitThread(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_learnCurrentItem);
        menu.Items.Add(_forgetClientsItem);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _tray = new NotifyIcon();
        _tray.Icon = SystemIcons.Application;
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;

        _callback = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            WriteStatus("Impossible d'installer le hook clavier, erreur " + error);
            MessageBox.Show(
                "Le correcteur Parsec AZERTY n'a pas pu démarrer (erreur " + error + ").",
                "Parsec AZERTY Fix",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _tray.Visible = false;
            ExitThread();
            return;
        }

        _connectionTimer = new System.Windows.Forms.Timer();
        _connectionTimer.Interval = 750;
        _connectionTimer.Tick += delegate { RefreshConnectionState(); };
        _connectionTimer.Start();
        RefreshConnectionState();
        WriteStatus("Correcteur démarré");
    }

    protected override void ExitThreadCore()
    {
        if (_connectionTimer != null)
            _connectionTimer.Stop();
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _tray.Visible = false;
        _tray.Dispose();
        WriteStatus("Correcteur arrêté");
        base.ExitThreadCore();
    }

    private void RefreshConnectionState()
    {
        if (_composeArmed && DateTime.Now - _composeArmedAt > TimeSpan.FromSeconds(5))
        {
            _composeArmed = false;
            SendUnicode('`');
        }

        ParsecConnectionSnapshot snapshot = ReadParsecConnectionState();
        if (!String.Equals(snapshot.Signature, _connectionSignature, StringComparison.Ordinal))
        {
            _connectionSnapshot = snapshot;
            _connectionSignature = snapshot.Signature;
            _parsecConnected = snapshot.ClientPorts.Count > 0;
            _sessionAndroidMode = IsKnownAndroidConnection(snapshot);
            ResetTransientInputState();
            WriteStatus(DescribeConnection(snapshot));
        }
        UpdateTray();
    }

    private ParsecConnectionSnapshot ReadParsecConnectionState()
    {
        try
        {
            if (!File.Exists(_parsecLog))
                return new ParsecConnectionSnapshot();

            var ports = new List<int>();
            int pendingPort = 0;
            using (var stream = new FileStream(_parsecLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (ParsecRestartLine.IsMatch(line))
                    {
                        ports.Clear();
                        pendingPort = 0;
                        continue;
                    }

                    Match network = NetworkLine.Match(line);
                    if (network.Success)
                    {
                        Int32.TryParse(network.Groups["port"].Value, out pendingPort);
                        continue;
                    }

                    if (ConnectedLine.IsMatch(line))
                    {
                        ports.Add(pendingPort);
                        pendingPort = 0;
                    }
                    else if (DisconnectedLine.IsMatch(line))
                    {
                        if (ports.Count > 0)
                            ports.RemoveAt(ports.Count - 1);
                        pendingPort = 0;
                    }
                }
            }
            return new ParsecConnectionSnapshot(ports);
        }
        catch
        {
            return _connectionSnapshot;
        }
    }

    private bool IsKnownAndroidConnection(ParsecConnectionSnapshot snapshot)
    {
        if (snapshot.ClientPorts.Count == 0)
            return false;

        foreach (int port in snapshot.ClientPorts)
            if (port <= 0 || !_androidClientPorts.Contains(port))
                return false;
        return true;
    }

    private string DescribeConnection(ParsecConnectionSnapshot snapshot)
    {
        if (snapshot.ClientPorts.Count == 0)
            return "Session Parsec terminée — retour automatique au mode standard";
        if (_sessionAndroidMode)
            return "Appareil Android reconnu — correction automatique active (port " + snapshot.ClientPorts[0] + ")";
        return "Connexion Parsec standard — aucune correction (port " + snapshot.ClientPorts[0] + ")";
    }

    private void LearnCurrentConnection()
    {
        if (_connectionSnapshot.ClientPorts.Count != 1 || _connectionSnapshot.ClientPorts[0] <= 0)
        {
            MessageBox.Show(
                "Connectez uniquement l'appareil Android à ce PC, puis réessayez.",
                "Parsec AZERTY Fix",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        int port = _connectionSnapshot.ClientPorts[0];
        _androidClientPorts.Add(port);
        SaveAndroidClientPorts();
        _sessionAndroidMode = true;
        ResetTransientInputState();
        WriteStatus("Connexion apprise comme appareil Android (port " + port + ")");
        UpdateTray();
    }

    private void ForgetAndroidClients()
    {
        _androidClientPorts.Clear();
        SaveAndroidClientPorts();
        _sessionAndroidMode = false;
        ResetTransientInputState();
        WriteStatus("Appareils Android enregistrés oubliés");
        UpdateTray();
    }

    private void LoadAndroidClientPorts()
    {
        try
        {
            if (!File.Exists(_androidClientsFile))
                return;
            foreach (string line in File.ReadAllLines(_androidClientsFile))
            {
                int port;
                if (Int32.TryParse(line.Trim(), out port) && port > 0 && port <= 65535)
                    _androidClientPorts.Add(port);
            }
        }
        catch { }
    }

    private void SaveAndroidClientPorts()
    {
        try
        {
            var ports = new List<int>(_androidClientPorts);
            ports.Sort();
            var lines = new List<string>();
            foreach (int port in ports)
                lines.Add(port.ToString());
            File.WriteAllLines(_androidClientsFile, lines.ToArray());
        }
        catch (Exception error)
        {
            WriteStatus("Impossible d'enregistrer les appareils Android : " + error.Message);
        }
    }

    private void UpdateTray()
    {
        string state;
        if (!_enabled)
            state = "désactivé";
        else if (!_parsecConnected)
            state = "en attente de Parsec";
        else if (_sessionAndroidMode)
            state = "Android reconnu — correction active";
        else
            state = "connexion standard — aucune correction";

        _statusItem.Text = "État : " + state;
        bool oneKnownClient = _connectionSnapshot.ClientPorts.Count == 1 &&
                              _connectionSnapshot.ClientPorts[0] > 0;
        _learnCurrentItem.Enabled = oneKnownClient && !_sessionAndroidMode;
        _forgetClientsItem.Enabled = _androidClientPorts.Count > 0;
        _tray.Text = "Parsec AZERTY Fix — " + state;
    }

    private void ResetTransientInputState()
    {
        _remoteShift = false;
        _remoteControl = false;
        _remoteAlt = false;
        _composeArmed = false;
        _composeConsumedScan = 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !_enabled || !_parsecConnected)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        int message = wParam.ToInt32();
        bool down = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
        bool up = message == WM_KEYUP || message == WM_SYSKEYUP;
        if (!down && !up)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
        bool injected = (data.flags & LLKHF_INJECTED) != 0;
        bool lowerIntegrity = (data.flags & LLKHF_LOWER_IL_INJECTED) != 0;
        if (!injected || lowerIntegrity || data.dwExtraInfo.ToUInt64() == OwnInputMarker)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        if (data.vkCode == 0x10 || data.vkCode == 0xA0 || data.vkCode == 0xA1)
        {
            _remoteShift = down;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (data.vkCode == 0x11 || data.vkCode == 0xA2 || data.vkCode == 0xA3)
        {
            _remoteControl = down;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (data.vkCode == 0x12 || data.vkCode == 0xA4 || data.vkCode == 0xA5)
        {
            _remoteAlt = down;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        bool controlDown = _remoteControl || ModifierDown(VK_CONTROL);
        bool altDown = _remoteAlt || ModifierDown(VK_MENU);
        bool noShortcutModifier = !controlDown && !altDown &&
                                  !ModifierDown(VK_LWIN) && !ModifierDown(VK_RWIN);
        bool sourceShift = _remoteShift || ModifierDown(VK_SHIFT);
        char sourceCharacter;
        bool hasSourceCharacter = TryGetUsSourceCharacter(
            data.vkCode, data.scanCode, sourceShift, out sourceCharacter);

        if (!_sessionAndroidMode)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        if (up && _composeConsumedScan == data.scanCode)
        {
            _composeConsumedScan = 0;
            return new IntPtr(1);
        }

        if (down && noShortcutModifier && hasSourceCharacter)
        {
            if (_composeArmed)
            {
                _composeArmed = false;
                if (sourceCharacter == '`')
                {
                    SendUnicode('`');
                    _composeConsumedScan = data.scanCode;
                    return new IntPtr(1);
                }

                if (TrySendCompose(sourceCharacter))
                {
                    _composeConsumedScan = data.scanCode;
                    return new IntPtr(1);
                }

                SendUnicode('`');
            }
            else if (sourceCharacter == '`')
            {
                _composeArmed = true;
                _composeArmedAt = DateTime.Now;
                _composeConsumedScan = data.scanCode;
                return new IntPtr(1);
            }
        }

        uint mappedScan = MapAzertyLetter(data.scanCode);
        if (mappedScan != data.scanCode)
        {
            SendScan(mappedScan, up);
            return new IntPtr(1);
        }

        if (noShortcutModifier)
        {
            char printable;
            if (TryGetUsPrintable(data.vkCode, data.scanCode, sourceShift, out printable))
            {
                if (down)
                    SendUnicode(printable);
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static uint MapAzertyLetter(uint scan)
    {
        switch (scan)
        {
            case 0x1E: return 0x10; // Android A -> French A position
            case 0x10: return 0x1E; // Android Q -> French Q position
            case 0x2C: return 0x11; // Android Z -> French Z position
            case 0x11: return 0x2C; // Android W -> French W position
            case 0x32: return 0x27; // Android M -> French M position
            default: return scan;
        }
    }

    private static bool TryGetUsSourceCharacter(uint virtualKey, uint scan, bool shift, out char value)
    {
        const string top = "qwertyuiop";
        const string home = "asdfghjkl";
        const string bottom = "zxcvbnm";

        if (scan >= 0x10 && scan <= 0x19)
        {
            value = top[(int)(scan - 0x10)];
            if (shift) value = Char.ToUpperInvariant(value);
            return true;
        }
        if (scan >= 0x1E && scan <= 0x26)
        {
            value = home[(int)(scan - 0x1E)];
            if (shift) value = Char.ToUpperInvariant(value);
            return true;
        }
        if (scan >= 0x2C && scan <= 0x32)
        {
            value = bottom[(int)(scan - 0x2C)];
            if (shift) value = Char.ToUpperInvariant(value);
            return true;
        }
        return TryGetUsPrintable(virtualKey, scan, shift, out value);
    }

    private static bool TrySendCompose(char source)
    {
        char normalized = Char.ToLowerInvariant(source);
        char output = '\0';
        switch (normalized)
        {
            case 'e': output = '€'; break;
            case 'l': output = '£'; break;
            case 'y': output = '¥'; break;
            case 'c': output = '¢'; break;
            case 'o': output = '©'; break;
            case 'r': output = '®'; break;
            case 't': output = '™'; break;
            case '?': output = '¿'; break;
            case '!': output = '¡'; break;
            case '/': output = '÷'; break;
            case '|': output = '¦'; break;
            case '-': output = '¬'; break;
            case 'x': output = '×'; break;
            case 's': output = '§'; break;
            case 'p': output = '¶'; break;
            case 'd': output = '°'; break;
            case 'b':
                SendScan(0x0E, false, false);
                SendScan(0x0E, true, false);
                return true;
            case 'u':
                SendScan(0x53, false, true);
                SendScan(0x53, true, true);
                return true;
            default:
                return false;
        }

        SendUnicode(output);
        return true;
    }

    private static bool TryGetUsPrintable(uint virtualKey, uint scan, bool shift, out char value)
    {
        value = '\0';
        if (virtualKey == 0xE7 && scan != 0) // VK_PACKET / Unicode sent by Parsec
        {
            value = (char)scan;
            return !char.IsControl(value);
        }

        string normal = null;
        string shifted = null;
        switch (scan)
        {
            case 0x02: normal = "1"; shifted = "!"; break;
            case 0x03: normal = "2"; shifted = "@"; break;
            case 0x04: normal = "3"; shifted = "#"; break;
            case 0x05: normal = "4"; shifted = "$"; break;
            case 0x06: normal = "5"; shifted = "%"; break;
            case 0x07: normal = "6"; shifted = "^"; break;
            case 0x08: normal = "7"; shifted = "&"; break;
            case 0x09: normal = "8"; shifted = "*"; break;
            case 0x0A: normal = "9"; shifted = "("; break;
            case 0x0B: normal = "0"; shifted = ")"; break;
            case 0x0C: normal = "-"; shifted = "_"; break;
            case 0x0D: normal = "="; shifted = "+"; break;
            case 0x1A: normal = "["; shifted = "{"; break;
            case 0x1B: normal = "]"; shifted = "}"; break;
            case 0x27: normal = ";"; shifted = ":"; break;
            case 0x28: normal = "'"; shifted = "\""; break;
            case 0x29: normal = "`"; shifted = "~"; break;
            case 0x2B: normal = "\\"; shifted = "|"; break;
            case 0x33: normal = ","; shifted = "<"; break;
            case 0x34: normal = "."; shifted = ">"; break;
            case 0x35: normal = "/"; shifted = "?"; break;
        }

        string selected = shift ? shifted : normal;
        if (String.IsNullOrEmpty(selected))
            return false;
        value = selected[0];
        return true;
    }

    private static bool ModifierDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    internal static void SendScan(uint scan, bool keyUp, bool extended)
    {
        var input = new INPUT();
        input.type = 1;
        input.U.ki.wVk = 0;
        input.U.ki.wScan = (ushort)scan;
        input.U.ki.dwFlags = KEYEVENTF_SCANCODE | (extended ? 0x0001U : 0U) |
                             (keyUp ? KEYEVENTF_KEYUP : 0);
        input.U.ki.time = 0;
        input.U.ki.dwExtraInfo = new UIntPtr(OwnInputMarker);
        SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SendScan(uint scan, bool keyUp)
    {
        SendScan(scan, keyUp, false);
    }

    internal static void SendUnicode(char value)
    {
        var down = new INPUT();
        down.type = 1;
        down.U.ki.wVk = 0;
        down.U.ki.wScan = value;
        down.U.ki.dwFlags = 0x0004; // KEYEVENTF_UNICODE
        down.U.ki.dwExtraInfo = new UIntPtr(OwnInputMarker);

        var up = down;
        up.U.ki.dwFlags = 0x0004 | KEYEVENTF_KEYUP;
        SendInput(2, new INPUT[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
    }

    private sealed class ParsecConnectionSnapshot
    {
        public readonly List<int> ClientPorts;
        public readonly string Signature;

        public ParsecConnectionSnapshot()
            : this(new List<int>())
        {
        }

        public ParsecConnectionSnapshot(List<int> ports)
        {
            ClientPorts = new List<int>(ports);
            string[] values = new string[ClientPorts.Count];
            for (int index = 0; index < ClientPorts.Count; index++)
                values[index] = ClientPorts[index].ToString();
            Signature = String.Join(",", values);
        }
    }

    private void WriteStatus(string message)
    {
        try
        {
            File.AppendAllText(
                _statusLog,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);
}
