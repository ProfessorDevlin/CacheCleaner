using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace CacheCleaner
{
    /// <summary>A CFX game (RedM or FiveM) and the cache folders that are safe to clear.</summary>
    internal class GameCache
    {
        // Game files (game-storage) and in-game UI settings (nui-storage) are deliberately not included.
        private static readonly string[] CacheFolders = { "cache", "server-cache", "server-cache-priv" };

        // Must be declared before All: static fields are initialized in this order
        private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static readonly GameCache[] All = { new GameCache("RedM"), new GameCache("FiveM") };

        public string Name { get; }

        /// <summary>Folder that contains RedM.exe / FiveM.exe, e.g. %LocalAppData%\RedM or D:\Games\RedM.</summary>
        public string InstallDir { get; }

        private GameCache(string name)
        {
            Name = name;
            InstallDir = FindInstallDir();
        }

        public string DataDir => Path.Combine(InstallDir, Name + ".app", "data");

        public bool IsInstalled => Directory.Exists(DataDir);

        /// <summary>The install folder for display; the default location is shortened to %LocalAppData%.</summary>
        public string DisplayDir =>
            InstallDir.StartsWith(LocalAppData, StringComparison.OrdinalIgnoreCase)
                ? "%LocalAppData%" + InstallDir.Substring(LocalAppData.Length)
                : InstallDir;

        // FiveM and RedM can be installed anywhere (e.g. on a second drive). Both register their real location
        // in Windows, so these places are checked in order; the first one that contains the data folder wins.
        private string FindInstallDir()
        {
            var candidates = new[] { FromUninstallEntry(), FromProtocolHandler(), Path.Combine(LocalAppData, Name) };
            return candidates.FirstOrDefault(dir => dir != null && Directory.Exists(Path.Combine(dir, Name + ".app", "data")))
                   ?? Path.Combine(LocalAppData, Name);
        }

        // "Installed apps" entry: Uninstall\<key> with DisplayName "FiveM" / "RedM" and its InstallLocation
        private string FromUninstallEntry()
        {
            var places = new[]
            {
                (RegistryHive.CurrentUser, RegistryView.Default),
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
            };
            foreach (var (hive, view) in places)
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null)
                            continue;
                        foreach (string keyName in uninstall.GetSubKeyNames())
                            using (var entry = uninstall.OpenSubKey(keyName))
                            {
                                if (string.Equals(entry?.GetValue("DisplayName") as string, Name, StringComparison.OrdinalIgnoreCase)
                                    && entry.GetValue("InstallLocation") is string location && location.Length > 0)
                                    return location.Trim('"');
                            }
                    }
                }
                catch
                {
                    // no access to this part of the registry
                }
            }
            return null;
        }

        // Link handler for fivem:// and redm://, e.g. "D:\Games\RedM\RedM.exe" "%1".
        // The command is registered under "FiveM.ProtocolHandler" (current versions) or directly under "fivem".
        private string FromProtocolHandler()
        {
            var classNames = new[] { Name + ".ProtocolHandler", Name.ToLowerInvariant() };
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
                foreach (string className in classNames)
                {
                    try
                    {
                        using (var key = root.OpenSubKey($@"Software\Classes\{className}\shell\open\command"))
                        {
                            if (!(key?.GetValue("") is string command) || command.Length == 0)
                                continue;
                            string exe = command.StartsWith("\"")
                                ? command.Substring(1, Math.Max(0, command.IndexOf('"', 1) - 1))
                                : command.Split(' ')[0];
                            return Path.GetDirectoryName(exe);
                        }
                    }
                    catch
                    {
                        // missing or malformed entry
                    }
                }
            return null;
        }

        public IEnumerable<string> CacheDirs =>
            CacheFolders.Select(f => Path.Combine(DataDir, f)).Where(Directory.Exists);

        /// <summary>RedM and FiveM run several processes (e.g. RedM_GameProcess); all start with the game's name.</summary>
        public bool IsRunning()
        {
            bool running = false;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                    if (p.ProcessName.StartsWith(Name, StringComparison.OrdinalIgnoreCase))
                        running = true;
            }
            return running;
        }
    }

    internal struct CleanProgress
    {
        public long Done;
        public long Total;
        public int Percent => Total == 0 ? 100 : (int)(Done * 100 / Total);
    }

    internal class CleanResult
    {
        public long Freed;
        public int Failed;
    }

    internal static class Cleaner
    {
        public static long GetSize(IEnumerable<string> roots) => roots.SelectMany(Files).Sum(SafeLength);

        /// <summary>
        /// Deletes all files inside the given folders and removes the emptied subfolders.
        /// The folders themselves are kept. Files in use are skipped and counted as failed.
        /// </summary>
        public static CleanResult Clear(IEnumerable<string> roots, IProgress<CleanProgress> progress)
        {
            var rootList = roots.ToList();
            var files = rootList.SelectMany(Files).ToList();
            var state = new CleanProgress { Total = files.Sum(SafeLength) };
            var result = new CleanResult();
            var lastReport = Stopwatch.StartNew();
            int lastPercent = -1;

            foreach (var file in files)
            {
                long length = SafeLength(file);
                try
                {
                    if (file.IsReadOnly)
                        file.IsReadOnly = false;
                    file.Delete();
                    result.Freed += length;
                }
                catch
                {
                    result.Failed++;
                }

                state.Done += length;
                if (state.Percent != lastPercent || lastReport.ElapsedMilliseconds > 200)
                {
                    lastPercent = state.Percent;
                    lastReport.Restart();
                    progress.Report(state);
                }
            }

            foreach (var root in rootList)
                DeleteEmptyDirs(new DirectoryInfo(root), isRoot: true);

            progress.Report(state);
            return result;
        }

        // Walks the folder tree without following junctions or symbolic links,
        // so nothing outside the cache folder can ever be reached.
        private static IEnumerable<FileInfo> Files(string root)
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));

            while (pending.Count > 0)
            {
                var dir = pending.Pop();
                FileSystemInfo[] entries;
                try { entries = dir.GetFileSystemInfos(); }
                catch { continue; }

                foreach (var entry in entries)
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (entry is DirectoryInfo sub)
                        pending.Push(sub);
                    else
                        yield return (FileInfo)entry;
                }
            }
        }

        private static void DeleteEmptyDirs(DirectoryInfo dir, bool isRoot)
        {
            DirectoryInfo[] subs;
            try { subs = dir.GetDirectories(); }
            catch { return; }

            foreach (var sub in subs)
                if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                    DeleteEmptyDirs(sub, isRoot: false);

            if (isRoot)
                return;
            try
            {
                if (!dir.EnumerateFileSystemInfos().Any())
                    dir.Delete();
            }
            catch
            {
                // still contains a file that is in use
            }
        }

        private static long SafeLength(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

        // ---------------------------------------------------------------- Texts shared by the main window and quick clean

        public static string RunningMessage(IEnumerable<string> names) =>
            string.Join(" and ", names) + " is still running.\nClose it completely and try again.";

        public static string ConfirmMessage(IEnumerable<(string Name, long Size)> items) =>
            "The cache of the following games will be deleted:\n\n" +
            string.Join("\n", items.Select(i => i.Name + "  –  " + FormatSize(i.Size))) +
            "\n\nServer files are downloaded again the next time you join a server. This cannot be undone.";

        public static string ProgressMessage(CleanProgress p) =>
            $"Deleting …  {FormatSize(p.Done)} of {FormatSize(p.Total)}  ({p.Percent} %)";

        public static string ResultMessage(CleanResult result) =>
            result.Failed == 0
                ? $"Done. {FormatSize(result.Freed)} freed."
                : $"Done. {FormatSize(result.Freed)} freed, {result.Failed} files were in use and skipped.";

        public static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0
                ? bytes + " B"
                : value.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
