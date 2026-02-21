using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using System.Drawing;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Reflection;
using Guna.UI2.WinForms.Enums;
using System.IO.Compression;
using System.Diagnostics; 
using System.Threading; 
using System.Runtime.InteropServices; 
using System.Media;

namespace GreenSteam
{
    public partial class MainForm : Form
    {
        private const string SETTINGS_FILE = "settings.json";

        private TextBox steamPathTextBox;
        private TextBox luaFolderTextBox;
        private TextBox appListPathTextBox;
        private Guna2Button steamBrowseButton;
        private Guna2Button luaBrowseButton;
        private Guna2Button appListBrowseButton;
        private Guna2Button validateButton;
        private Guna2Button previewButton;
        private Guna2Button applyButton;
        private Guna2Button installButton; 
        private Guna.UI2.WinForms.Guna2ProgressBar progressBar;
		private Label statusLabel;
        private Label attentionLabel; 
		private TableLayoutPanel mainPanel;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        protected override CreateParams CreateParams
        {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        
		private string GetGameNameFromSteamDB(string appId)
		{
			try {
				using (var client = new HttpClient()) {
					client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
					string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
					string json = client.GetStringAsync(url).Result;
					using (JsonDocument doc = JsonDocument.Parse(json)) {
						var root = doc.RootElement;
						var appData = root.GetProperty(appId);
						if (appData.GetProperty("success").GetBoolean()) {
							return appData.GetProperty("data").GetProperty("name").GetString() ?? "Unknown Game";
						}
					}
				}
			} catch { }
			return "Unknown Game";
		}

        public class Settings {
            public string SteamFolder { get; set; } = "";
            public string AppListFolder { get; set; } = "";
        }

        public MainForm()
        {
            InitializeComponent();
            new Guna2BorderlessForm { ContainerControl = this, BorderRadius = 20, DragForm = false };
            var dragControl = new Guna2DragControl { TargetControl = mainPanel };
            var closeButton = new Guna2ControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(this.Width - 65, 10), FillColor = Color.Red, Size = new Size(35, 15) };
            var minimizeButton = new Guna2ControlBox { ControlBoxType = ControlBoxType.MinimizeBox, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(this.Width - 100, 10), FillColor = Color.Gray, Size = new Size(35, 15) };
            this.Controls.Add(minimizeButton);
            this.Controls.Add(closeButton);
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("GreenSteam.background.png")) {
                if (stream != null) this.BackgroundImage = Image.FromStream(stream);
            }
            this.BackgroundImageLayout = ImageLayout.Stretch;
            minimizeButton.BringToFront();
            closeButton.BringToFront();
            LoadSettings();
        }
        
        private void InitializeComponent()
        {
			this.DoubleBuffered = true;
            this.Size = new Size(700, 380); 
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
			int row = 0;
            mainPanel = new TableLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(40), ColumnCount = 3, RowCount = 7, AutoSize = true, BackColor = Color.Transparent };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 7; i++) mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            mainPanel.Controls.Add(new Label { Text = "Steam Folder:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.White }, 0, row);
            steamPathTextBox = new TextBox { Dock = DockStyle.Top, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            mainPanel.Controls.Add(steamPathTextBox, 1, row);
            steamBrowseButton = new Guna2Button { Text = "Browse", AutoSize = true };
            steamBrowseButton.Click += SteamBrowseButton_Click;
            mainPanel.Controls.Add(steamBrowseButton, 2, row++);
            
            mainPanel.Controls.Add(new Label { Text = "Folder or Zip Containing Lua + Manifest Files:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.White }, 0, row);
            luaFolderTextBox = new TextBox { Dock = DockStyle.Top, Anchor = AnchorStyles.Left | AnchorStyles.Right, AllowDrop = true };
            luaFolderTextBox.DragEnter += LuaFolderTextBox_DragEnter;
            luaFolderTextBox.DragDrop += LuaFolderTextBox_DragDrop;
            mainPanel.Controls.Add(luaFolderTextBox, 1, row);
            luaBrowseButton = new Guna2Button { Text = "Browse", AutoSize = true };
            luaBrowseButton.Click += LuaBrowseButton_Click;
            mainPanel.Controls.Add(luaBrowseButton, 2, row++);

            mainPanel.Controls.Add(new Label { Text = "AppList Folder:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.White }, 0, row);
            appListPathTextBox = new TextBox { Dock = DockStyle.Top, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            mainPanel.Controls.Add(appListPathTextBox, 1, row);
            appListBrowseButton = new Guna2Button { Text = "Browse", AutoSize = true };
            appListBrowseButton.Click += AppListBrowseButton_Click;
            mainPanel.Controls.Add(appListBrowseButton, 2, row++);

			var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 20, 0, 10) };
			validateButton = new Guna2Button { Text = "Validate Paths", FillColor = Color.Teal, AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            validateButton.Click += ValidateButton_Click;
			previewButton = new Guna2Button { Text = "Preview Changes", FillColor = Color.MediumSlateBlue, AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            previewButton.Click += PreviewButton_Click;
			applyButton = new Guna2Button { Text = "Apply Changes", FillColor = Color.ForestGreen, AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            applyButton.Click += ApplyButton_Click;
            installButton = new Guna2Button { Text = "Open Steam Console & Install", FillColor = Color.DarkOrange, AutoSize = true, Margin = new Padding(10, 0, 0, 0) };
            installButton.Click += InstallButton_Click;

			buttonPanel.Controls.AddRange(new Control[] { validateButton, previewButton, applyButton, installButton });
			mainPanel.Controls.Add(buttonPanel, 0, row);
			mainPanel.SetColumnSpan(buttonPanel, 3);
			row++;

            progressBar = new Guna2ProgressBar { Size = new Size(300, 20), FillColor = Color.Gray, ProgressColor = Color.Teal, Visible = false, AutoRoundedCorners = true };
			mainPanel.Controls.Add(progressBar, 0, row);
			mainPanel.SetColumnSpan(progressBar, 3);
			row++;

            statusLabel = new Label { Text = "Ready", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.LimeGreen };
            mainPanel.Controls.Add(statusLabel, 0, row);
            mainPanel.SetColumnSpan(statusLabel, 3);
            row++;

            attentionLabel = new Label { 
                Text = "", 
                AutoSize = true, 
                Anchor = AnchorStyles.Left, 
                ForeColor = Color.Yellow, 
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Visible = false 
            };
            mainPanel.Controls.Add(attentionLabel, 0, row);
            mainPanel.SetColumnSpan(attentionLabel, 3);

            this.Controls.Add(mainPanel);
        }
        
        private void UpdateStatus(string message) { statusLabel.Text = message; }
        private void SteamBrowseButton_Click(object sender, EventArgs e) { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) steamPathTextBox.Text = d.SelectedPath; }
        private void LuaBrowseButton_Click(object sender, EventArgs e) {
            using (var selector = new SourceSelectionDialog()) {
                DialogResult result = selector.ShowDialog();
                if (result == DialogResult.OK) { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) luaFolderTextBox.Text = d.SelectedPath; }
                else if (result == DialogResult.Yes) { using (var d = new OpenFileDialog { Filter = "Zip Files (*.zip)|*.zip" }) if (d.ShowDialog() == DialogResult.OK) luaFolderTextBox.Text = d.FileName; }
            }
        }
        private void LuaFolderTextBox_DragEnter(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }
        private void LuaFolderTextBox_DragDrop(object sender, DragEventArgs e) { string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop); if (paths.Length > 0) luaFolderTextBox.Text = paths[0]; }
        private void AppListBrowseButton_Click(object sender, EventArgs e) { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) appListPathTextBox.Text = d.SelectedPath; }
        private bool ValidateSteamPath(string path) { return File.Exists(Path.Combine(path, "config", "config.vdf")); }
        
        private void ValidateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(steamPathTextBox.Text) || !ValidateSteamPath(steamPathTextBox.Text)) {
                MessageBox.Show("Invalid Steam Folder!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(luaFolderTextBox.Text) || (!Directory.Exists(luaFolderTextBox.Text) && !File.Exists(luaFolderTextBox.Text))) {
                MessageBox.Show("Invalid Lua Source!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(appListPathTextBox.Text) || !Directory.Exists(appListPathTextBox.Text)) {
                MessageBox.Show("Invalid AppList Folder!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show("All paths are valid!", "Validation Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void PreviewButton_Click(object sender, EventArgs e)
        {
            if (!ValidatePaths()) return;
            try {
                var (luaContent, luaFileName) = FindFirstLuaFileContent(luaFolderTextBox.Text); 
                var entries = ExtractAllAddAppIdValues(luaContent); 
                var manifestCount = GetManifestFileCount(luaFolderTextBox.Text);
                var previewText = $"Found {entries.Count} app entries in {luaFileName}:\n\n";
                foreach (var entry in entries.Take(10)) previewText += $"AppID: {entry.AppId}, Key: {entry.Key.Substring(0, 16)}...\n";
                if (entries.Count > 10) previewText += $"\n... and {entries.Count - 10} more entries";
                previewText += $"\n\nManifest files to copy: {manifestCount}";
                MessageBox.Show(previewText, "Preview Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        
        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            if (!ValidatePaths()) return;
            SetButtonsEnabled(false);
            progressBar.Value = 0; progressBar.Maximum = 7; progressBar.Visible = true;
            try { await Task.Run(() => EditConfig()); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); UpdateStatus("Error occurred"); }
            finally { SetButtonsEnabled(true); progressBar.Visible = false; }
        }
        
        private void KillSteamProcesses()
        {
            foreach (var p in Process.GetProcessesByName("steam")) { try { p.Kill(); p.WaitForExit(500); } catch { } }
            Thread.Sleep(1000); 
        }

        private bool WaitForSteamProcessToStart(int timeout)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout) { if (Process.GetProcessesByName("steam").Any()) return true; Thread.Sleep(500); }
            return false;
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            if (!ValidatePaths()) return;
            try {
                attentionLabel.Visible = false; 
                KillSteamProcesses();
                var (_, luaFileName) = FindFirstLuaFileContent(luaFolderTextBox.Text); 
                string appId = Path.GetFileNameWithoutExtension(luaFileName);
                string installCommand = $"app_install {appId}";
                string baseDir = Path.GetDirectoryName(appListPathTextBox.Text);

                Process.Start(new ProcessStartInfo(Path.Combine(baseDir, "DLLInjector.exe")) { WorkingDirectory = baseDir, UseShellExecute = true });
                
                if (!WaitForSteamProcessToStart(15000)) return;
                Process.Start("explorer.exe", "steam://open/console");
                IntPtr steamHandle = IntPtr.Zero;
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 15000 && steamHandle == IntPtr.Zero) {
                    steamHandle = FindWindow(null, "Steam");
                    if (steamHandle == IntPtr.Zero) Thread.Sleep(200);
                }
                if (steamHandle != IntPtr.Zero) {
                    SetForegroundWindow(steamHandle);
                    if (GetWindowRect(steamHandle, out RECT r)) {
                        int centerX = r.Left + (r.Right - r.Left) / 2;
                        int centerY = r.Top + (r.Bottom - r.Top) / 2;
                        Cursor.Position = new Point(centerX, centerY);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0); Thread.Sleep(50); mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    }
                    Thread.Sleep(2000); 
                }
                Clipboard.SetText(installCommand); Thread.Sleep(400); 
                keybd_event(VK_CONTROL, 0, 0, 0); keybd_event(VK_V, 0, 0, 0); Thread.Sleep(50);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0); keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); 
                Thread.Sleep(200); SendKeys.SendWait("~");

                Thread.Sleep(1000); 
                SystemSounds.Exclamation.Play(); 

                attentionLabel.Text = $"ATTENTION: If the game does not start downloading, the automatic paste may have failed.\n" +
                                     $"Please switch to the Steam Console window and manually press CTRL+V then ENTER\n" +
                                     $"The command '{installCommand}' is still in your clipboard.";
                attentionLabel.Visible = true;

            } catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void SetButtonsEnabled(bool e) { validateButton.Enabled = previewButton.Enabled = applyButton.Enabled = installButton.Enabled = e; }
        private bool ValidatePaths() { return Directory.Exists(steamPathTextBox.Text) && (Directory.Exists(luaFolderTextBox.Text) || luaFolderTextBox.Text.EndsWith(".zip")) && Directory.Exists(appListPathTextBox.Text); }
        
		private void EditConfig()
        {
            var steamFolder = steamPathTextBox.Text;
            var luaFolder = luaFolderTextBox.Text;
            var appListFolder = appListPathTextBox.Text;
            var configPath = Path.Combine(steamFolder, "config", "config.vdf");

            BeginInvoke(new Action(() => { UpdateStatus("Copying manifest files..."); progressBar.Value = 1; }));
            CopyManifestFiles(luaFolder, steamFolder);

            BeginInvoke(new Action(() => { UpdateStatus("Parsing Lua file..."); progressBar.Value = 2; }));
            var (luaContent, luaFileName) = FindFirstLuaFileContent(luaFolder); 
            var entries = ExtractAllAddAppIdValues(luaContent); 
            string baseLuaFileName = Path.GetFileNameWithoutExtension(luaFileName); 

            BeginInvoke(new Action(() => { UpdateStatus("Creating backup..."); progressBar.Value = 3; }));
            var backupPath = CreateBackup(configPath);

            BeginInvoke(new Action(() => { UpdateStatus("Reading config file..."); progressBar.Value = 4; }));
            var lines = File.ReadAllLines(configPath).ToList();
            int targetLine = lines.FindIndex(line => line.Contains("\"CurrentCellID\""));
            if (targetLine == -1) targetLine = lines.FindIndex(line => line.Contains("\"RecentDownloadRate\""));
            
            int insertIndex = -1;
            for (int i = targetLine - 1; i >= 0; i--) if (lines[i].Trim() == "}") { insertIndex = i; break; }

            string indent = DetectIndentation(lines, insertIndex - 1);
            string innerIndent = indent + "\t";

            var insertBlocks = new List<string>();
            foreach (var entry in entries) {
                insertBlocks.Add($"{indent}\"{entry.AppId}\"");
                insertBlocks.Add($"{indent}{{");
                insertBlocks.Add($"{innerIndent}\"DecryptionKey\"\t\t\"{entry.Key}\"");
                insertBlocks.Add($"{indent}}}");
            }
            lines.InsertRange(insertIndex, insertBlocks);

            BeginInvoke(new Action(() => { UpdateStatus("Writing modified config..."); progressBar.Value = 5; }));
            File.WriteAllLines(configPath, lines);

            BeginInvoke(new Action(() => { UpdateStatus("Creating AppID files..."); progressBar.Value = 6; }));
            string gameName = GetGameNameFromSteamDB(baseLuaFileName); 
            foreach (var entry in entries) {
                string txtPath = GetNextAvailableTxtPath(appListFolder);
                File.WriteAllText(txtPath, $"{entry.AppId} - {gameName}");
            }
            File.WriteAllText(GetNextAvailableTxtPath(appListFolder), $"{baseLuaFileName} - {gameName}");

            BeginInvoke(new Action(() => { UpdateStatus("Creating ACF file..."); progressBar.Value = 7; }));
            CreateAcfFile(steamFolder, baseLuaFileName, gameName);

            SaveSettings();
            
            BeginInvoke(new Action(() => { 
                UpdateStatus("Ready");
            }));
        }

        private void CreateAcfFile(string steamFolder, string appId, string gameName)
        {
            string steamappsFolder = Path.Combine(steamFolder, "steamapps");
            
            // Create steamapps folder if it doesn't exist
            if (!Directory.Exists(steamappsFolder)) {
                Directory.CreateDirectory(steamappsFolder);
            }

            // Sanitize the game name for use as install directory
            string installDir = SanitizeInstallDir(gameName);
            
            // Create ACF filename
            string acfFileName = $"appmanifest_{appId}.acf";
            string acfPath = Path.Combine(steamappsFolder, acfFileName);

            // Create ACF content with exact formatting
            var acfLines = new List<string>
            {
                "\t\"AppState\"",
                "\t{",
                $"      \"AppID\"  \"{appId}\"",
                "\t  \"Universe\" \"1\"",
                $"\t  \"installdir\" \"{installDir}\"",
                "\t  \"StateFlags\" \"1026\"",
                "\t}"
            };

            // Write the ACF file
            File.WriteAllLines(acfPath, acfLines);
        }

        private string SanitizeInstallDir(string gameName)
        {
            // Remove invalid characters for folder names
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid) {
                gameName = gameName.Replace(c.ToString(), "");
            }
            
            // Replace spaces and special characters with underscores
            gameName = Regex.Replace(gameName, @"[^\w\s-]", "");
            gameName = Regex.Replace(gameName, @"\s+", "_");
            
            // Truncate if too long and ensure it's not empty
            if (gameName.Length > 100) gameName = gameName.Substring(0, 100);
            if (string.IsNullOrWhiteSpace(gameName)) gameName = "game";
            
            return gameName;
        }

        private (string content, string fileName) FindFirstLuaFileContent(string path)
        {
            if (File.Exists(path) && path.EndsWith(".zip")) {
                using (ZipArchive zip = ZipFile.OpenRead(path)) {
                    var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".lua"));
                    using (var reader = new StreamReader(entry.Open())) return (reader.ReadToEnd(), entry.FullName);
                }
            }
            var files = Directory.GetFiles(path, "*.lua");
            return (File.ReadAllText(files[0]), Path.GetFileName(files[0]));
        }

        private List<AppEntry> ExtractAllAddAppIdValues(string content)
        {
            var pattern = @"addappid\s*\(\s*(\d+)\s*,\s*[01]\s*,\s*[""']([a-fA-F0-9]{64})[""']\s*\)";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            return matches.Cast<Match>().Select(m => new AppEntry { AppId = m.Groups[1].Value, Key = m.Groups[2].Value }).ToList();
        }

        private string CreateBackup(string path) { var b = $"{path}.bak_{DateTime.Now:yyyyMMdd_HHmmss}"; File.Copy(path, b); return b; }

        private void CopyManifestFiles(string sourcePath, string steamFolder)
        {
            var depotCacheFolder = Path.Combine(steamFolder, "depotcache");
            Directory.CreateDirectory(depotCacheFolder);
            if (File.Exists(sourcePath) && sourcePath.EndsWith(".zip")) {
                using (ZipArchive zip = ZipFile.OpenRead(sourcePath)) {
                    foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".manifest")))
                        entry.ExtractToFile(Path.Combine(depotCacheFolder, Path.GetFileName(entry.FullName)), true);
                }
            } else {
                foreach (var f in Directory.GetFiles(sourcePath, "*.manifest"))
                    File.Copy(f, Path.Combine(depotCacheFolder, Path.GetFileName(f)), true);
            }
        }
        
        private int GetManifestFileCount(string path) {
            if (File.Exists(path) && path.EndsWith(".zip")) {
                using (ZipArchive zip = ZipFile.OpenRead(path)) return zip.Entries.Count(e => e.FullName.EndsWith(".manifest"));
            }
            return Directory.GetFiles(path, "*.manifest").Length;
        }

        private string DetectIndentation(List<string> lines, int index) {
            if (index < 0) return "\t\t\t\t\t";
            var m = Regex.Match(lines[index], @"^(\s*)");
            return m.Success ? m.Groups[1].Value : "\t\t\t\t\t";
        }

        private string GetNextAvailableTxtPath(string folderPath) {
            int counter = 0; string path;
            do { path = Path.Combine(folderPath, $"{counter}.txt"); counter++; } while (File.Exists(path));
            return path;
        }
        
        private void LoadSettings() {
            if (File.Exists(SETTINGS_FILE)) {
                var json = File.ReadAllText(SETTINGS_FILE);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                steamPathTextBox.Text = settings.SteamFolder; appListPathTextBox.Text = settings.AppListFolder;
            }
        }
        
        private void SaveSettings() {
            var s = new Settings { SteamFolder = steamPathTextBox.Text, AppListFolder = appListPathTextBox.Text };
            File.WriteAllText(SETTINGS_FILE, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        
        public class AppEntry { public string AppId { get; set; } public string Key { get; set; } }
    }

    public class SourceSelectionDialog : Form
    {
        public SourceSelectionDialog()
        {
            this.Text = "Select Source Type"; this.Size = new Size(350, 150); this.StartPosition = FormStartPosition.CenterParent; this.ControlBox = false; this.BackColor = Color.White;
            var prompt = new Label { Text = "Choose the source type for Lua and Manifest files:", Location = new Point(10, 20), AutoSize = true, Font = new Font("Segoe UI", 9.75f) };
            var folder = new Guna2Button { Text = "Folder", Location = new Point(20, 60), Size = new Size(95, 30), FillColor = Color.Teal, ForeColor = Color.White, Font = new Font("Segoe UI", 9.75f, FontStyle.Bold) };
            folder.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            var zip = new Guna2Button { Text = "Zip File", Location = new Point(125, 60), Size = new Size(95, 30), FillColor = Color.MediumSlateBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 9.75f, FontStyle.Bold) };
            zip.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };
            var cancel = new Guna2Button { Text = "Cancel", Location = new Point(230, 60), Size = new Size(95, 30), FillColor = Color.Gray, ForeColor = Color.White, Font = new Font("Segoe UI", 9.75f, FontStyle.Bold) };
            cancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.AddRange(new Control[] { prompt, folder, zip, cancel });
        }
    }
    
    public static class Program { [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); } }
}