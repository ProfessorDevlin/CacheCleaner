using System;
using System.Linq;
using System.Windows.Forms;

namespace CacheCleaner
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // "--quick RedM FiveM" comes from the Quick Clean shortcut: skip the main window
            if (args.Length > 0 && args[0].Equals(Shortcut.QuickArgument, StringComparison.OrdinalIgnoreCase))
            {
                var games = GameCache.All
                    .Where(g => args.Skip(1).Any(a => a.Equals(g.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                Application.Run(new QuickCleanForm(games));
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
