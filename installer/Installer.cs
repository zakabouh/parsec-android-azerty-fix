using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Installer
{
    private const string ProductName = "Parsec Android AZERTY Fix";
    private const string Version = "1.4.1";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ParsecAzertyFix";
    private const string RunValueName = "ParsecAzertyFix";
    private const string StartupShortcutName = "Parsec Android AZERTY Fix.lnk";
    private const string PayloadResource = "ParsecAzertyFix.Payload.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        bool silent = HasArgument(args, "/silent") || HasArgument(args, "--silent");
        bool uninstall = HasArgument(args, "/uninstall") || HasArgument(args, "--uninstall");
        bool finalUninstall = HasArgument(args, "/uninstall-final");

        try
        {
            if (uninstall || finalUninstall)
            {
                if (uninstall && IsInstalledUninstaller())
                {
                    RelaunchUninstallerFromTemp(silent);
                    return 0;
                }
                Uninstall(silent);
            }
            else
            {
                Install(silent);
            }
            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show(ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void Install(bool silent)
    {
        string installDirectory = GetInstallDirectory();
        string executable = Path.Combine(installDirectory, "ParsecAzertyFix.exe");
        string uninstaller = Path.Combine(installDirectory, "Uninstall.exe");
        string temporaryPayload = executable + ".new";

        Directory.CreateDirectory(installDirectory);
        StopInstalledApplication(executable);
        DeleteIfPresent(Path.Combine(installDirectory, "android-clients.txt"));

        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
        {
            if (source == null)
                throw new InvalidOperationException("Le correcteur integre est introuvable dans l'installateur.");
            using (var destination = new FileStream(temporaryPayload, FileMode.Create, FileAccess.Write, FileShare.None))
                source.CopyTo(destination);
        }

        if (File.Exists(executable)) File.Delete(executable);
        File.Move(temporaryPayload, executable);

        string currentInstaller = Assembly.GetExecutingAssembly().Location;
        if (!PathsEqual(currentInstaller, uninstaller))
            File.Copy(currentInstaller, uninstaller, true);

        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            run.SetValue(RunValueName, Quote(executable), RegistryValueKind.String);
        CreateStartupShortcut(executable);

        using (RegistryKey entry = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
        {
            entry.SetValue("DisplayName", ProductName);
            entry.SetValue("DisplayVersion", Version);
            entry.SetValue("Publisher", "zakabouh");
            entry.SetValue("URLInfoAbout", "https://github.com/zakabouh/parsec-android-azerty-fix");
            entry.SetValue("InstallLocation", installDirectory);
            entry.SetValue("DisplayIcon", Quote(executable));
            entry.SetValue("UninstallString", Quote(uninstaller) + " /uninstall");
            entry.SetValue("QuietUninstallString", Quote(uninstaller) + " /uninstall /silent");
            entry.SetValue("NoModify", 1, RegistryValueKind.DWord);
            entry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            long estimatedSize = (new FileInfo(executable).Length + new FileInfo(uninstaller).Length + 1023) / 1024;
            entry.SetValue("EstimatedSize", (int)Math.Min(estimatedSize, Int32.MaxValue), RegistryValueKind.DWord);
        }

        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });

        if (!silent)
            MessageBox.Show(
                "Installation terminee.\r\n\r\nLe correcteur est actif et demarrera automatiquement avec votre session Windows.",
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
    }

    private static void Uninstall(bool silent)
    {
        string installDirectory = GetInstallDirectory();
        string executable = Path.Combine(installDirectory, "ParsecAzertyFix.exe");
        string uninstaller = Path.Combine(installDirectory, "Uninstall.exe");

        StopInstalledApplication(executable);

        using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
        {
            if (run != null)
            {
                object value = run.GetValue(RunValueName);
                if (value == null || value.ToString().IndexOf(executable, StringComparison.OrdinalIgnoreCase) >= 0)
                    run.DeleteValue(RunValueName, false);
            }
        }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);

        DeleteIfPresent(GetStartupShortcutPath());
        DeleteIfPresent(executable);
        DeleteIfPresent(Path.Combine(installDirectory, "status.log"));
        DeleteIfPresent(Path.Combine(installDirectory, "android-clients.txt"));
        DeleteIfPresent(Path.Combine(installDirectory, "ParsecAzertyFix.exe.new"));
        if (!PathsEqual(Assembly.GetExecutingAssembly().Location, uninstaller))
            DeleteIfPresent(uninstaller);

        try
        {
            if (Directory.Exists(installDirectory) && Directory.GetFileSystemEntries(installDirectory).Length == 0)
                Directory.Delete(installDirectory);
        }
        catch { }

        if (!silent)
            MessageBox.Show("Le correcteur a ete desinstalle.", ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void RelaunchUninstallerFromTemp(bool silent)
    {
        string temporaryCopy = Path.Combine(
            Path.GetTempPath(),
            "ParsecAzertyFix-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Assembly.GetExecutingAssembly().Location, temporaryCopy, true);
        string arguments = "/uninstall-final" + (silent ? " /silent" : "");
        Process.Start(new ProcessStartInfo(temporaryCopy, arguments) { UseShellExecute = true });
    }

    private static void StopInstalledApplication(string expectedPath)
    {
        foreach (Process process in Process.GetProcessesByName("ParsecAzertyFix"))
        {
            try
            {
                string path = process.MainModule.FileName;
                if (PathsEqual(path, expectedPath))
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private static bool IsInstalledUninstaller()
    {
        return PathsEqual(
            Assembly.GetExecutingAssembly().Location,
            Path.Combine(GetInstallDirectory(), "Uninstall.exe"));
    }

    private static string GetInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ParsecAzertyFix");
    }

    private static string GetStartupShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupShortcutName);
    }

    private static void CreateStartupShortcut(string executable)
    {
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows Script Host est indisponible.");

            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { GetStartupShortcutPath() });

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember(
                "TargetPath", BindingFlags.SetProperty, null, shortcut,
                new object[] { executable });
            shortcutType.InvokeMember(
                "WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                new object[] { Path.GetDirectoryName(executable) });
            shortcutType.InvokeMember(
                "Description", BindingFlags.SetProperty, null, shortcut,
                new object[] { ProductName });
            shortcutType.InvokeMember(
                "IconLocation", BindingFlags.SetProperty, null, shortcut,
                new object[] { executable + ",0" });
            shortcutType.InvokeMember(
                "Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static bool HasArgument(string[] args, string expected)
    {
        foreach (string arg in args)
            if (String.Equals(arg, expected, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool PathsEqual(string left, string right)
    {
        return String.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value) { return "\"" + value + "\""; }
    private static void DeleteIfPresent(string path) { if (File.Exists(path)) File.Delete(path); }
}
