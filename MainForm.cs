using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CacheCleaner
{
    // The UI is built in code, not in the Visual Studio designer.
    [DesignerCategory("")]
    internal class MainForm : Form
    {
        private readonly GameCache[] games = GameCache.All;
        private readonly ThemedCheckBox[] gameBoxes;
        private readonly Label[] sizeLabels;
        private readonly Label[] pathLabels;
        private readonly long[] sizes;

        private readonly ThemedProgressBar progressBar = new ThemedProgressBar { Height = 8, Dock = DockStyle.Top, Visible = false };
        private readonly Label statusLabel = new Label { AutoSize = true, Tag = "sub", Margin = new Padding(0, 8, 0, 0) };
        private readonly Button refreshButton = new Button { Text = "Refresh" };
        private readonly Button clearButton = new Button { Text = "Clear cache", Tag = "primary" };
        private readonly Button shortcutButton = new Button { Text = "Create shortcut" };
        private bool busy;

        public MainForm()
        {
            Theme.Refresh();
            Text = "Cache Cleaner for RedM & FiveM";
            using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico"))
                Icon = new Icon(stream, new Size(32, 32));
            Font = Theme.UiFont;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 370);

            gameBoxes = games.Select(g => new ThemedCheckBox { Text = g.Name, Dock = DockStyle.Fill, Height = 28 }).ToArray();
            sizeLabels = games.Select(g => new Label { AutoSize = true, Anchor = AnchorStyles.Right, Tag = "sub" }).ToArray();
            pathLabels = games.Select(g => new Label
            {
                Text = g.IsInstalled ? g.DisplayDir : "",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Tag = "sub",
                Font = new Font(Theme.UiFont.FontFamily, 8.5f),
                Margin = new Padding(25, 0, 0, 0), // aligned with the checkbox text
            }).ToArray();
            sizes = new long[games.Length];

            Controls.Add(BuildContent());
            Theme.Apply(this);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private Control BuildContent()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(18, 14, 18, 16) };

            var intro = new Label
            {
                Text = "Clears the cache, server-cache and server-cache-priv folders.\nGame files and settings are kept.",
                AutoSize = true,
                Tag = "sub",
                Margin = new Padding(0, 0, 0, 12),
            };

            // Two rows per game: checkbox and cache size, then the folder it was found in
            var card = new CardPanel { Text = "Games", Dock = DockStyle.Top, Height = 34 + 50 * games.Length + 12 };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = games.Length * 2 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < games.Length; i++)
            {
                rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                rows.Controls.Add(gameBoxes[i], 0, i * 2);
                rows.Controls.Add(sizeLabels[i], 1, i * 2);
                rows.Controls.Add(pathLabels[i], 0, i * 2 + 1);
                rows.SetColumnSpan(pathLabels[i], 2);
            }
            card.Controls.Add(rows);

            var progressPanel = new Panel { Dock = DockStyle.Top, Height = 44, Margin = new Padding(0, 16, 0, 0) };
            statusLabel.Dock = DockStyle.Top;
            progressPanel.Controls.Add(statusLabel);
            progressPanel.Controls.Add(progressBar);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom, Margin = Padding.Empty };
            foreach (var b in new[] { clearButton, refreshButton, shortcutButton })
            {
                b.AutoSize = true;
                b.Padding = new Padding(10, 3, 10, 3);
                buttons.Controls.Add(b);
            }
            clearButton.Click += (s, e) => ClearSelected();
            refreshButton.Click += (s, e) => RefreshSizes();
            shortcutButton.Click += (s, e) => CreateShortcut();

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(intro, 0, 0);
            layout.Controls.Add(card, 0, 1);
            layout.Controls.Add(progressPanel, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            card.Dock = DockStyle.Fill;
            progressPanel.Dock = DockStyle.Fill;
            return layout;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshSizes();
        }

        // ---------------------------------------------------------------- Sizes

        private async void RefreshSizes()
        {
            SetBusy(true);
            for (int i = 0; i < games.Length; i++)
            {
                var game = games[i];
                if (!game.IsInstalled)
                {
                    sizes[i] = 0;
                    gameBoxes[i].Checked = false;
                    gameBoxes[i].Enabled = false;
                    sizeLabels[i].Text = "Not installed";
                    continue;
                }

                sizeLabels[i].Text = "Calculating …";
                sizes[i] = await Task.Run(() => Cleaner.GetSize(game.CacheDirs));
                sizeLabels[i].Text = Cleaner.FormatSize(sizes[i]);
                gameBoxes[i].Enabled = true;
                gameBoxes[i].Checked = sizes[i] > 0;
            }
            SetBusy(false);

            if (statusLabel.Text.Length == 0)
                statusLabel.Text = games.Any(g => g.IsInstalled)
                    ? "Select the games and click Clear cache."
                    : "Neither RedM nor FiveM was found on this PC.";
        }

        // ---------------------------------------------------------------- Clearing

        private async void ClearSelected()
        {
            var selected = Enumerable.Range(0, games.Length).Where(i => gameBoxes[i].Checked && games[i].IsInstalled).ToList();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Select at least one game.";
                return;
            }

            // Files of a running game are locked and cannot be deleted
            var running = selected.Where(i => games[i].IsRunning()).Select(i => games[i].Name).ToList();
            if (running.Count > 0)
            {
                ThemedDialog.Info(this, "Close the game first", Cleaner.RunningMessage(running));
                return;
            }

            if (!ThemedDialog.Confirm(this, "Clear cache",
                    Cleaner.ConfirmMessage(selected.Select(i => (games[i].Name, sizes[i]))),
                    "Clear cache", "Cancel"))
                return;

            SetBusy(true);
            progressBar.Visible = true;
            progressBar.Marquee = true;
            statusLabel.Text = "Preparing …";

            var progress = new Progress<CleanProgress>(p =>
            {
                progressBar.Marquee = false;
                progressBar.Value = p.Percent;
                statusLabel.Text = Cleaner.ProgressMessage(p);
            });

            var dirs = selected.SelectMany(i => games[i].CacheDirs).ToList();
            var result = await Task.Run(() => Cleaner.Clear(dirs, progress));

            progressBar.Marquee = false;
            progressBar.Value = 100;
            statusLabel.Text = Cleaner.ResultMessage(result);
            SetBusy(false);
            RefreshSizes();
        }

        // ---------------------------------------------------------------- Quick clean shortcut

        private void CreateShortcut()
        {
            var names = Enumerable.Range(0, games.Length)
                .Where(i => gameBoxes[i].Checked && games[i].IsInstalled)
                .Select(i => games[i].Name)
                .ToList();
            if (names.Count == 0)
            {
                statusLabel.Text = "Select the games for the shortcut first.";
                return;
            }

            try
            {
                string name = Shortcut.CreateQuickClean(names);
                statusLabel.Text = $"\"{name}\" was created on the desktop.";
            }
            catch (Exception ex)
            {
                ThemedDialog.Info(this, "Shortcut", "The shortcut could not be created:\n" + ex.Message);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            clearButton.Enabled = !value;
            refreshButton.Enabled = !value;
            shortcutButton.Enabled = !value;
            for (int i = 0; i < games.Length; i++)
                gameBoxes[i].Enabled = !value && games[i].IsInstalled;
            UseWaitCursor = value;
        }

        // Closing while files are being deleted would leave the cache half cleared
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        // ---------------------------------------------------------------- Theme

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // Windows was switched between light and dark → apply immediately
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && IsHandleCreated)
                BeginInvoke(new Action(() =>
                {
                    if (Theme.Refresh())
                        Theme.Apply(this);
                }));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            base.Dispose(disposing);
        }
    }
}
