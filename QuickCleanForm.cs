using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CacheCleaner
{
    /// <summary>
    /// Small window for the "Quick Clean" shortcut: asks once, shows the progress and closes itself.
    /// The main window is skipped completely.
    /// </summary>
    [DesignerCategory("")]
    internal class QuickCleanForm : Form
    {
        private readonly GameCache[] games;
        private readonly Label titleLabel = new Label { AutoSize = true, Font = Theme.TitleFont, Margin = new Padding(0, 0, 0, 10) };
        private readonly ThemedProgressBar progressBar = new ThemedProgressBar { Height = 8, Dock = DockStyle.Fill, Margin = Padding.Empty };
        private readonly Label statusLabel = new Label { AutoSize = true, Tag = "sub", Margin = new Padding(0, 8, 0, 0) };
        private bool busy;

        public QuickCleanForm(GameCache[] games)
        {
            this.games = games;
            Theme.Refresh();
            Text = "Cache Cleaner for RedM & FiveM";
            using (var stream = typeof(QuickCleanForm).Assembly.GetManifestResourceStream("app.ico"))
                Icon = new Icon(stream, new Size(32, 32));
            Font = Theme.UiFont;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 120);

            titleLabel.Text = "Quick Clean – " + string.Join(" & ", games.Select(g => g.Name));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(18, 16, 18, 16) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(titleLabel, 0, 0);
            layout.Controls.Add(progressBar, 0, 1);
            layout.Controls.Add(statusLabel, 0, 2);
            Controls.Add(layout);
            Theme.Apply(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            var installed = games.Where(g => g.IsInstalled).ToList();
            if (installed.Count == 0)
            {
                ThemedDialog.Info(this, "Quick Clean",
                    (games.Length == 0 ? "No game was given." : string.Join(" and ", games.Select(g => g.Name)) + " was not found on this PC."));
                Close();
                return;
            }

            // Files of a running game are locked and cannot be deleted
            var running = installed.Where(g => g.IsRunning()).Select(g => g.Name).ToList();
            if (running.Count > 0)
            {
                ThemedDialog.Info(this, "Close the game first", Cleaner.RunningMessage(running));
                Close();
                return;
            }

            busy = true;
            progressBar.Marquee = true;
            statusLabel.Text = "Checking cache size …";
            var sizes = await Task.Run(() => installed.Select(g => Cleaner.GetSize(g.CacheDirs)).ToArray());
            progressBar.Marquee = false;
            busy = false;

            if (sizes.Sum() == 0)
            {
                ThemedDialog.Info(this, "Quick Clean", "The cache is already empty.");
                Close();
                return;
            }

            if (!ThemedDialog.Confirm(this, "Clear cache",
                    Cleaner.ConfirmMessage(installed.Select((g, i) => (g.Name, sizes[i]))),
                    "Clear cache", "Cancel"))
            {
                Close();
                return;
            }

            busy = true;
            progressBar.Marquee = true;
            statusLabel.Text = "Preparing …";
            var progress = new Progress<CleanProgress>(p =>
            {
                progressBar.Marquee = false;
                progressBar.Value = p.Percent;
                statusLabel.Text = Cleaner.ProgressMessage(p);
            });
            var result = await Task.Run(() => Cleaner.Clear(installed.SelectMany(g => g.CacheDirs).ToList(), progress));
            busy = false;

            progressBar.Marquee = false;
            progressBar.Value = 100;
            statusLabel.Text = Cleaner.ResultMessage(result);

            // Leave the result visible for a moment, then close on its own
            var closeTimer = new Timer { Interval = 4000 };
            closeTimer.Tick += (s, args) => { closeTimer.Dispose(); Close(); };
            closeTimer.Start();
        }

        // Closing while files are being deleted would leave the cache half cleared
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
