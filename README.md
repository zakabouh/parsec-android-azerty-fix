# Fix Parsec Android AZERTY keyboard mapping on Windows

[Lire en français](README.fr.md)

An open-source Windows workaround for **Parsec Android keyboard layout problems** when an **AZERTY keyboard is recognized as QWERTY** on the remote Windows host.

It fixes swapped `A/Q`, `Z/W` and `M` keys, number-row and punctuation mapping, and provides fallbacks for special characters, Backspace and Delete. The helper automatically distinguishes the near-instant key taps produced by Parsec's Android soft keyboard from the normal key-hold timings of a physical keyboard. There is no device-learning step or keyboard shortcut, and normal Parsec connections from another computer stay unchanged.

## Does this match your problem?

This project targets symptoms commonly described as:

- “Parsec Android keyboard is stuck in QWERTY”;
- “my AZERTY keyboard is recognized as QWERTY on the Windows host”;
- “A and Q / Z and W are swapped in Parsec”;
- “Parsec uses the wrong keyboard layout”;
- “number keys type symbols” or “numbers do not work on the Windows login screen”;
- “special characters do not work in Parsec Android”;
- “Backspace does not work unless I hold it down”;
- “Delete, Enter or special keys are not sent from Android”;
- “Samsung DeX / Galaxy Tab physical keyboard is mapped incorrectly”;
- “Parsec Android Bluetooth keyboard or USB keyboard has mixed-up keys”.

The helper runs on the **Windows host after a Parsec connection has been established**. It does not modify the Android app itself.

## Frequently searched questions

### Why does Parsec Android type QWERTY on my AZERTY PC?

Parsec can send Android/US key positions while the Windows host interprets them through its French layout. This makes an AZERTY keyboard behave like QWERTY and swaps keys such as `A/Q` and `Z/W`. The helper translates those remote events before Windows applications receive them.

### Why do the number keys type symbols in Parsec?

The US and French number rows use Shift differently. When US scan codes are interpreted as French AZERTY input, `1`, `2`, `3` and other numbers can become `&`, `é`, `"` or other characters. The helper sends the intended number or punctuation character directly.

### Why does Backspace only work when held?

Some Android keyboards send Backspace inconsistently through Parsec. The helper preserves normal Backspace events and also offers `` `b `` as a fallback. It cannot recover a tap if the Android client sends no event at all.

## Limitations

- It cannot fix typing inside Parsec's own Android login screen.
- It cannot operate on the Windows secure sign-in/PIN screen before the user session starts.
- It cannot reconstruct a key for which Android and Parsec transmit absolutely no event; the compose shortcuts are the fallback for those characters.
- Enter, mouse/right-click and controller problems are outside this keyboard-layout fix.
- Double typing caused by some SwiftKey configurations is a separate Android keyboard issue.
- If Android and another computer control the host simultaneously, the helper stays in standard mode. Parsec does not expose which client produced each individual keyboard event.
- A macro or another remote on-screen keyboard that emits sub-25 ms key taps can resemble the Android soft keyboard. The tray menu can restart detection or force Android mode for the current session.
- A physical Bluetooth or USB keyboard attached to Android has human key-hold timings. If it suffers from the same layout problem, select **Force Android for this session** from the tray menu.

## What it fixes

- Converts the Android/US QWERTY scan-code positions to French AZERTY for `A/Q`, `Z/W` and `M`.
- Converts the US number row and punctuation to the intended characters instead of letting the French host layout reinterpret them.
- Provides a compose-key fallback for Unicode symbols that Parsec Android does not transmit as usable Windows key events.
- Provides optional compose shortcuts for Backspace and Delete.
- Detects Android soft-keyboard input automatically at the start of every single-client Parsec session.
- Requires two physical-length key presses (or overlapping keys) before classifying a session as a standard computer, while one characteristic Android tap or synthetic-Shift pattern is enough to enable the fix.
- Leaves the physical Windows keyboard and local mouse unchanged.

Parsec officially describes its Android app as experimental and says mouse and keyboard input may work incorrectly in some cases. See [Install Parsec App on Android](https://support.parsec.app/hc/en-us/articles/32381582866452-Install-Parsec-App-on-Android).

## Install

1. Download [`ParsecAzertyFix-Setup.exe`](https://github.com/zakabouh/parsec-android-azerty-fix/releases/latest/download/ParsecAzertyFix-Setup.exe).
2. Run the installer.
3. Connect normally from Android or another computer. Detection is automatic.

No administrator rights are required. The application is installed for the current Windows user at:

```text
%LOCALAPPDATA%\ParsecAzertyFix\ParsecAzertyFix.exe
```

It is registered under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, so it starts automatically at every Windows sign-in.

> The executable is not code-signed. Windows SmartScreen may therefore display an “Unknown publisher” warning. The complete source code and reproducible build script are available in this repository.

## Usage

At the start of each session with one connected client:

- a soft-keyboard tap released within 25 ms, or Android's characteristic synthetic-Shift sequence, enables the correction;
- physical-length presses or overlapping keys classify the client as a standard computer and leave every key unchanged;
- disconnecting resets the decision, so the next session is detected independently.

The helper briefly delays the first one or two printable key-down events by at most 35 ms while deciding. Shortcuts using Ctrl, Alt or the Windows key are never delayed for detection. No keyboard shortcut or layout-dependent key combination is required. The tray menu can restart detection, force Android mode for the current session, disable the helper globally or exit the program.

The classifier is based on input behavior rather than network ports. Parsec's documented default client port is pseudorandom, so a port number cannot reliably identify Android.

### Why automatic detection works

Parsec's Android input layer is built on the open-source [libmatoya Android backend](https://github.com/snowcone-ltd/libmatoya/tree/master/src/unix/linux/android). Its source distinguishes soft-keyboard events with `event.getDeviceId() <= 0`, contains an explicit note that several translations are hard-coded for an EN-US keyboard, and synthesizes Shift events for affected soft-keyboard symbols. Those events reach the Windows host in a tight burst, while a physical key remains down for normal human timing.

The helper observes only event type and timing. It does not record which keys or characters were typed. A key released within 25 ms, or the synthetic-Shift burst, identifies the Android soft keyboard. A key still held after 35 ms is immediately forwarded unchanged; two such presses classify the session as standard.

### Compose shortcuts for missing characters

Some Android keyboard symbols never reach the Windows host as usable key events. Type a backtick `` ` `` followed by the listed key:

| Sequence | Output | Sequence | Output |
|---|---:|---|---:|
| `` `e `` | `€` | `` `l `` | `£` |
| `` `y `` | `¥` | `` `c `` | `¢` |
| `` `o `` | `©` | `` `r `` | `®` |
| `` `t `` | `™` | `` `? `` | `¿` |
| `` `! `` | `¡` | `` `/ `` | `÷` |
| `` `\| `` | `¦` | `` `- `` | `¬` |
| `` `x `` | `×` | `` `s `` | `§` |
| `` `p `` | `¶` | `` `d `` | `°` |
| `` `b `` | Backspace | `` `u `` | Delete |
| <kbd>`</kbd> <kbd>`</kbd> | Literal backtick | | |

An incomplete compose sequence expires after five seconds and outputs a normal backtick.

## Uninstall

Open **Windows Settings > Apps > Installed apps**, find **Parsec Android AZERTY Fix**, and select **Uninstall**.

## Build from source

On Windows 10 or Windows 11 with .NET Framework 4.x:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

The `artifacts\` directory will contain:

- `ParsecAzertyFix.exe` — portable helper;
- `ParsecAzertyFix-Setup.exe` — standalone per-user installer with the helper embedded;
- `SHA256SUMS.txt` — SHA-256 checksums.

## Privacy and security

- Uses the Windows low-level keyboard hook (`WH_KEYBOARD_LL`) and local `SendInput` calls.
- Reads `%APPDATA%\Parsec\log.txt` only to detect when Parsec clients connect and disconnect.
- Stores no keystrokes, typed text, client addresses or device identifiers.
- Makes no network connections and contains no telemetry or data collection.
- Does not require administrator privileges.

This is an unofficial community workaround and is not affiliated with or endorsed by Parsec.

## License

[MIT](LICENSE)
