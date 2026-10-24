using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CacheCleaner
{
    /// <summary>Creates the "Quick Clean" desktop shortcut that starts the tool with --quick and the chosen games.</summary>
    internal static class Shortcut
    {
        public const string QuickArgument = "--quick";

        /// <summary>Creates (or replaces) the shortcut and returns its name.</summary>
        public static string CreateQuickClean(IList<string> gameNames)
        {
            string name = "Quick Clean – " + string.Join(" & ", gameNames);
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");
            string exe = Application.ExecutablePath;

            // WScript.Shell is the built-in Windows way to write .lnk files
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object link = null;
            try
            {
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Set(link, "TargetPath", exe);
                Set(link, "Arguments", QuickArgument + " " + string.Join(" ", gameNames));
                Set(link, "WorkingDirectory", Path.GetDirectoryName(exe));
                Set(link, "IconLocation", exe + ",0");
                Set(link, "Description", "Clears the " + string.Join(" and ", gameNames) + " cache");
                link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null)
                    Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
            return name;
        }

        private static void Set(object link, string property, string value) =>
            link.GetType().InvokeMember(property, BindingFlags.SetProperty, null, link, new object[] { value });
    }
}
