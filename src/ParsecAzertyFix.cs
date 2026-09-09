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
    private const int ClassificationDelayMilliseconds = 35;
    private const int FastTapMaximumMilliseconds = 25;
    private const int ShiftBurstMaximumMilliseconds = 30;
    private const int StandardEvidenceRequired = 2;
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
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _retryDetectionItem;
    private readonly ToolStripMenuItem _forceAndroidItem;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly System.Windows.Forms.Timer _connectionTimer;
    private readonly System.Windows.Forms.Timer _classificationTimer;
    private readonly LowLevelKeyboardProc _callback;
    private IntPtr _hook;
    private bool _enabled = true;
    private bool _parsecConnected;
    private SessionInputMode _sessionMode = SessionInputMode.Standard;
    private string _connectionSignature = String.Empty;
    private ParsecConnectionSnapshot _connectionSnapshot = new ParsecConnectionSnapshot();
    private PendingKey _pendingKey;
    private uint _timedOutScan;
    private uint _timedOutVirtualKey;
    private int _slowTapEvidence;
    private int _shiftBurstCount;
    private long _shiftBurstStartedAt;
    private bool _androidShiftSignatureArmed;
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
        _parsecLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Parsec",
            "log.txt");

        _statusItem = new ToolStripMenuItem("État : démarrage…");
        _statusItem.Enabled = false;
        _retryDetectionItem = new ToolStripMenuItem("Relancer la détection automatique");
        _retryDetectionItem.Click += delegate { RetryAutomaticDetection(); };
        _forceAndroidItem = new ToolStripMenuItem("Forcer Android pour cette session");
        _forceAndroidItem.Click += delegate { ForceAndroidForCurrentSession(); };

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
        menu.Items.Add(_retryDetectionItem);
        menu.Items.Add(_forceAndroidItem);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _tray = new NotifyIcon();
        _tray.Icon = SystemIcons.Application;
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;

        _classificationTimer = new System.Windows.Forms.Timer();
        _classificationTimer.Interval = ClassificationDelayMilliseconds;
        _classificationTimer.Tick += delegate { ClassificationTimerElapsed(); };

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
        if (_classificationTimer != null)
            _classificationTimer.Stop();
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
            ResetTransientInputState();
            _sessionMode = snapshot.ClientPorts.Count == 1
                ? SessionInputMode.Unknown
                : SessionInputMode.Standard;
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

    private string DescribeConnection(ParsecConnectionSnapshot snapshot)
    {
        if (snapshot.ClientPorts.Count == 0)
            return "Session Parsec terminée — retour automatique au mode standard";
        if (snapshot.ClientPorts.Count > 1)
            return "Plusieurs clients Parsec — mode standard par sécurité";
        return "Nouvelle session Parsec — détection automatique en attente";
    }

    private void RetryAutomaticDetection()
    {
        if (!_parsecConnected || _connectionSnapshot.ClientPorts.Count != 1)
        {
            MessageBox.Show(
                "Connectez un seul appareil à ce PC, puis réessayez.",
                "Parsec AZERTY Fix",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ResetTransientInputState();
        _sessionMode = SessionInputMode.Unknown;
        WriteStatus("Détection automatique relancée pour la session actuelle");
        UpdateTray();
    }

    private void ForceAndroidForCurrentSession()
    {
        if (!_parsecConnected || _connectionSnapshot.ClientPorts.Count != 1)
            return;
        ResetTransientInputState();
        SetSessionMode(SessionInputMode.Android, "Mode Android forcé pour la session actuelle");
    }

    private void UpdateTray()
    {
        string state;
        if (!_enabled)
            state = "désactivé";
        else if (!_parsecConnected)
            state = "en attente de Parsec";
        else if (_sessionMode == SessionInputMode.Android)
            state = "Android détecté — correction active";
        else if (_sessionMode == SessionInputMode.Unknown)
            state = "détection automatique…";
        else
            state = "connexion standard — aucune correction";

        _statusItem.Text = "État : " + state;
        bool oneClient = _parsecConnected && _connectionSnapshot.ClientPorts.Count == 1;
        _retryDetectionItem.Enabled = oneClient;
        _forceAndroidItem.Enabled = oneClient && _sessionMode != SessionInputMode.Android;
        _tray.Text = "Parsec AZERTY Fix — " + state;
    }

    private void ResetTransientInputState()
    {
        _classificationTimer.Stop();
        _pendingKey = null;
        _timedOutScan = 0;
        _timedOutVirtualKey = 0;
        _slowTapEvidence = 0;
        _shiftBurstCount = 0;
        _shiftBurstStartedAt = 0;
        _androidShiftSignatureArmed = false;
        _remoteShift = false;
        _remoteControl = false;
        _remoteAlt = false;
        _composeArmed = false;
        _composeConsumedScan = 0;
    }

    private void SetSessionMode(SessionInputMode mode, string status)
    {
        _classificationTimer.Stop();
        _pendingKey = null;
        _timedOutScan = 0;
        _timedOutVirtualKey = 0;
        _sessionMode = mode;
        WriteStatus(status);
        UpdateTray();
    }

    private bool HandleUnknownInput(
        KBDLLHOOKSTRUCT data,
        bool down,
        bool up,
        bool sourceShift,
        bool noShortcutModifier)
    {
        if (down && noShortcutModifier && _androidShiftSignatureArmed &&
            IsDetectionCandidate(data))
        {
            SetSessionMode(
                SessionInputMode.Android,
                "Clavier tactile Android détecté automatiquement (signature Shift)");
            return ProcessAndroidEvent(
                data, true, false, sourceShift, noShortcutModifier);
        }

        if (_pendingKey != null)
        {
            if (up && _pendingKey.Matches(data))
            {
                PendingKey pending = _pendingKey;
                _pendingKey = null;
                _classificationTimer.Stop();

                double elapsedMilliseconds = pending.ElapsedMilliseconds;
                if (elapsedMilliseconds <= FastTapMaximumMilliseconds)
                {
                    SetSessionMode(
                        SessionInputMode.Android,
                        "Clavier tactile Android détecté automatiquement (tap " +
                        Math.Round(elapsedMilliseconds) + " ms)");
                    ReplayAndroidTap(pending, data);
                    return true;
                }

                SendOriginalKeyboard(pending.Data, false);
                RegisterSlowTap();
                return false;
            }

            if (down && noShortcutModifier && IsDetectionCandidate(data))
            {
                PendingKey pending = _pendingKey;
                _pendingKey = null;
                _classificationTimer.Stop();
                SendOriginalKeyboard(pending.Data, false);
                SetSessionMode(
                    SessionInputMode.Standard,
                    "Clavier standard détecté automatiquement (touches simultanées)");
                return false;
            }

            return false;
        }

        if (_timedOutScan != 0)
        {
            if (up && data.scanCode == _timedOutScan && data.vkCode == _timedOutVirtualKey)
            {
                _timedOutScan = 0;
                _timedOutVirtualKey = 0;
                RegisterSlowTap();
                return false;
            }

            if (down && noShortcutModifier && IsDetectionCandidate(data))
            {
                SetSessionMode(
                    SessionInputMode.Standard,
                    "Clavier standard détecté automatiquement (touches simultanées)");
                return false;
            }
        }

        if (down && noShortcutModifier && IsDetectionCandidate(data))
        {
            _pendingKey = new PendingKey(data, sourceShift, noShortcutModifier);
            _classificationTimer.Stop();
            _classificationTimer.Start();
            return true;
        }

        return false;
    }

    private void ClassificationTimerElapsed()
    {
        _classificationTimer.Stop();
        if (_sessionMode != SessionInputMode.Unknown || _pendingKey == null)
            return;

        PendingKey pending = _pendingKey;
        _pendingKey = null;
        _timedOutScan = pending.Data.scanCode;
        _timedOutVirtualKey = pending.Data.vkCode;
        SendOriginalKeyboard(pending.Data, false);
    }

    private void RegisterSlowTap()
    {
        _slowTapEvidence++;
        if (_slowTapEvidence >= StandardEvidenceRequired)
        {
            SetSessionMode(
                SessionInputMode.Standard,
                "Clavier standard détecté automatiquement (pressions physiques)");
        }
    }

    private void ReplayAndroidTap(PendingKey pending, KBDLLHOOKSTRUCT upData)
    {
        bool temporaryShift = pending.SourceShift && IsLetterScan(pending.Data.scanCode) &&
                              !_remoteShift && !ModifierDown(VK_SHIFT);
        if (temporaryShift)
            SendScan(0x2A, false);

        if (!ProcessAndroidEvent(
                pending.Data,
                true,
                false,
                pending.SourceShift,
                pending.NoShortcutModifier))
        {
            SendOriginalKeyboard(pending.Data, false);
        }

        if (!ProcessAndroidEvent(
                upData,
                false,
                true,
                pending.SourceShift,
                pending.NoShortcutModifier))
        {
            SendOriginalKeyboard(upData, true);
        }

        if (temporaryShift)
            SendScan(0x2A, true);
    }

    private static bool IsDetectionCandidate(KBDLLHOOKSTRUCT data)
    {
        if (data.vkCode == 0xE7 || data.scanCode == 0)
            return false;
        if (IsLetterScan(data.scanCode))
            return true;

        char ignored;
        return TryGetUsPrintable(data.vkCode, data.scanCode, false, out ignored);
    }

    private static bool IsLetterScan(uint scan)
    {
        return (scan >= 0x10 && scan <= 0x19) ||
               (scan >= 0x1E && scan <= 0x26) ||
               (scan >= 0x2C && scan <= 0x32);
    }

    private static void SendOriginalKeyboard(KBDLLHOOKSTRUCT data, bool keyUp)
    {
        SendScan(data.scanCode, keyUp, (data.flags & 0x00000001) != 0);
    }

    private void ObserveUnknownShiftDown()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _shiftBurstStartedAt == 0
            ? Double.MaxValue
            : (now - _shiftBurstStartedAt) * 1000.0 / Stopwatch.Frequency;

        if (elapsed <= ShiftBurstMaximumMilliseconds)
        {
            _shiftBurstCount++;
        }
        else
        {
            _shiftBurstCount = 1;
            _shiftBurstStartedAt = now;
        }

        if (_shiftBurstCount >= 2)
            _androidShiftSignatureArmed = true;
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
            if (down && _sessionMode == SessionInputMode.Unknown)
                ObserveUnknownShiftDown();
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

        bool sourceShift = _remoteShift || ModifierDown(VK_SHIFT);
        bool noShortcutModifier = !_remoteControl && !_remoteAlt &&
                                  !ModifierDown(VK_CONTROL) && !ModifierDown(VK_MENU) &&
                                  !ModifierDown(VK_LWIN) && !ModifierDown(VK_RWIN);

        if (_sessionMode == SessionInputMode.Standard)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        if (_sessionMode == SessionInputMode.Unknown)
        {
            if (HandleUnknownInput(data, down, up, sourceShift, noShortcutModifier))
                return new IntPtr(1);
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (ProcessAndroidEvent(data, down, up, sourceShift, noShortcutModifier))
            return new IntPtr(1);
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool ProcessAndroidEvent(
        KBDLLHOOKSTRUCT data,
        bool down,
        bool up,
        bool sourceShift,
        bool noShortcutModifier)
    {
        if (up && _composeConsumedScan == data.scanCode)
        {
            _composeConsumedScan = 0;
            return true;
        }

        char sourceCharacter;
        bool hasSourceCharacter = TryGetUsSourceCharacter(
            data.vkCode, data.scanCode, sourceShift, out sourceCharacter);

        if (down && noShortcutModifier && hasSourceCharacter)
        {
            if (_composeArmed)
            {
                _composeArmed = false;
                if (sourceCharacter == '`')
                {
                    SendUnicode('`');
                    _composeConsumedScan = data.scanCode;
                    return true;
                }

                if (TrySendCompose(sourceCharacter))
                {
                    _composeConsumedScan = data.scanCode;
                    return true;
                }

                SendUnicode('`');
            }
            else if (sourceCharacter == '`')
            {
                _composeArmed = true;
                _composeArmedAt = DateTime.Now;
                _composeConsumedScan = data.scanCode;
                return true;
            }
        }

        uint mappedScan = MapAzertyLetter(data.scanCode);
        if (mappedScan != data.scanCode)
        {
            SendScan(mappedScan, up);
            return true;
        }

        if (noShortcutModifier)
        {
            char printable;
            if (TryGetUsPrintable(data.vkCode, data.scanCode, sourceShift, out printable))
            {
                if (down)
                    SendUnicode(printable);
                return true;
            }
        }

        return false;
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

    private enum SessionInputMode
    {
        Unknown,
        Android,
        Standard
    }

    private sealed class PendingKey
    {
        public readonly KBDLLHOOKSTRUCT Data;
        public readonly bool SourceShift;
        public readonly bool NoShortcutModifier;
        private readonly long _startedAt;

        public PendingKey(
            KBDLLHOOKSTRUCT data,
            bool sourceShift,
            bool noShortcutModifier)
        {
            Data = data;
            SourceShift = sourceShift;
            NoShortcutModifier = noShortcutModifier;
            _startedAt = Stopwatch.GetTimestamp();
        }

        public bool Matches(KBDLLHOOKSTRUCT data)
        {
            return data.scanCode == Data.scanCode && data.vkCode == Data.vkCode;
        }

        public double ElapsedMilliseconds
        {
            get
            {
                long elapsed = Stopwatch.GetTimestamp() - _startedAt;
                return elapsed * 1000.0 / Stopwatch.Frequency;
            }
        }
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
