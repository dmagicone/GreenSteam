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
using System.IO.Compression; // ADDED: Required for ZipFile and ZipArchive


namespace GreenSteam
{
    public partial class MainForm : Form
    {
        private const string SETTINGS_FILE = "settings.json";
        
        // UI Controls
        private TextBox steamPathTextBox;
        private TextBox luaFolderTextBox;
        private TextBox appListPathTextBox;
        private Guna2Button steamBrowseButton;
        private Guna2Button luaBrowseButton;
        private Guna2Button appListBrowseButton;
        private Guna2Button validateButton;
        private Guna2Button previewButton;
        private Guna2Button applyButton;
        private Guna.UI2.WinForms.Guna2ProgressBar progressBar;
        private Label statusLabel;
		private TableLayoutPanel mainPanel;


        // Solution 1: Override CreateParams for additional control styles
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }		

        
		private string GetGameNameFromSteamDB(string appId)
		{
			try
			{
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

					string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
					string json = client.GetStringAsync(url).Result;

					using (JsonDocument doc = JsonDocument.Parse(json))
					{
						var root = doc.RootElement;
						var appData = root.GetProperty(appId);

						if (appData.GetProperty("success").GetBoolean())
						{
							string name = appData.GetProperty("data").GetProperty("name").GetString();
							return name ?? "Unknown Game";
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching game name for AppID {appId}: {ex.Message}");
			}

			return "Unknown Game";
		}


		
        // Settings class for JSON serialization
        public class Settings
        {
            public string SteamFolder { get; set; } = "";
            public string AppListFolder { get; set; } = "";
        }
        
public MainForm()
{
    InitializeComponent(); // Call this FIRST
	
    
    // Rounded, draggable, borderless form
    var borderlessForm = new Guna2BorderlessForm
    {
        ContainerControl = this,
        BorderRadius = 20,
        TransparentWhileDrag = false,
        DockIndicatorTransparencyValue = 0.6,
        DragForm = false,              // turn off background drag
    };

    // ✅ Enable dragging from mainPanel
    var dragControl = new Guna2DragControl();
    dragControl.TargetControl = mainPanel;
    dragControl.UseTransparentDrag = false;

    // Create close and minimize buttons AFTER InitializeComponent
    var closeButton = new Guna2ControlBox
    {
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        Location = new Point(this.Width - 65, 10),
        FillColor = Color.Red,
        IconColor = Color.White,
        Size = new Size(35, 15) // Explicitly set size
    };
	

    var minimizeButton = new Guna2ControlBox
    {
        ControlBoxType = ControlBoxType.MinimizeBox,
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        Location = new Point(this.Width - 100, 10),
        FillColor = Color.Gray,
        IconColor = Color.White,
        Size = new Size(35, 15) // Explicitly set size
    };

    // Add to form AFTER mainPanel
    this.Controls.Add(minimizeButton);
    this.Controls.Add(closeButton);
	// Background image setup
	var assembly = Assembly.GetExecutingAssembly();
	using (Stream stream = assembly.GetManifestResourceStream("GreenSteam.background.png"))
	{
		if (stream != null)
			this.BackgroundImage = Image.FromStream(stream);
	}
	this.BackgroundImageLayout = ImageLayout.Stretch;
    
    // Bring buttons to front to ensure they're visible
    minimizeButton.BringToFront();
    closeButton.BringToFront();

    // Load embedded icon
 
    using (Stream stream = assembly.GetManifestResourceStream("GreenSteam.icon.ico"))
    {
        if (stream != null)
            this.Icon = new Icon(stream);
    }
	
	


	


    LoadSettings();
}
        
        private void InitializeComponent()
        {
			this.DoubleBuffered = true;
			
            this.Text = "GreenSteam";
            this.Size = new Size(700, 300);
            this.MinimumSize = new Size(600, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
			
			
			int row = 0;


            
            // Create main panel
            mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(40),
                ColumnCount = 3,
                RowCount = 6,
                AutoSize = true
				
            };
			
			mainPanel.BackColor = Color.Transparent;
			

			//mainPanel.Controls.Add(progressBar);
			

            
            // Configure column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            
            // Configure row styles
            for (int i = 0; i < 6; i++)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            
            
            // Steam Folder
            var steamLabel = new Label
            {
                Text = "Steam Folder:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            mainPanel.Controls.Add(steamLabel, 0, row);
            
            steamPathTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            mainPanel.Controls.Add(steamPathTextBox, 1, row);
            
            steamBrowseButton = new Guna2Button
            {
                Text = "Browse",
                AutoSize = true
            };
            steamBrowseButton.Click += SteamBrowseButton_Click;
            mainPanel.Controls.Add(steamBrowseButton, 2, row);
            row++;
            
            // Lua Folder
            var luaLabel = new Label
            {
                // UPDATED text to reflect new functionality
                Text = "Folder or Zip Containing Lua + Manifest Files:", 
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            mainPanel.Controls.Add(luaLabel, 0, row);
            
            luaFolderTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AllowDrop = true // NEW: Enable drag and drop
            };
            mainPanel.Controls.Add(luaFolderTextBox, 1, row);
            
            // NEW: Wire up drag-and-drop events
            luaFolderTextBox.DragEnter += LuaFolderTextBox_DragEnter;
            luaFolderTextBox.DragDrop += LuaFolderTextBox_DragDrop;
            
            luaBrowseButton = new Guna2Button
            {
                Text = "Browse",
                AutoSize = true
            };
            luaBrowseButton.Click += LuaBrowseButton_Click;
            mainPanel.Controls.Add(luaBrowseButton, 2, row);
            row++;
            
            // AppList Folder
            var appListLabel = new Label
            {
                Text = "AppList Folder:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            mainPanel.Controls.Add(appListLabel, 0, row);
            
            appListPathTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            mainPanel.Controls.Add(appListPathTextBox, 1, row);
            
            appListBrowseButton = new Guna2Button
            {
                Text = "Browse",
                AutoSize = true
            };
            appListBrowseButton.Click += AppListBrowseButton_Click;
            mainPanel.Controls.Add(appListBrowseButton, 2, row);
            row++;
            
			// Buttons panel
			var buttonPanel = new FlowLayoutPanel
			{
				FlowDirection = FlowDirection.LeftToRight,
				AutoSize = true,
				Margin = new Padding(0, 20, 0, 10)
			};

			validateButton = new Guna2Button
			{
				Text = "Validate Paths",
				AutoSize = true,
				Margin = new Padding(0, 0, 10, 0)
			};
			validateButton.FillColor = Color.Teal;
			validateButton.ForeColor = Color.White;
			validateButton.HoverState.FillColor = Color.DarkCyan;
			validateButton.HoverState.ForeColor = Color.LightYellow;
			validateButton.PressedColor = Color.Navy;
			validateButton.Click += ValidateButton_Click;
			buttonPanel.Controls.Add(validateButton);

			previewButton = new Guna2Button
			{
				Text = "Preview Changes",
				AutoSize = true,
				Margin = new Padding(0, 0, 10, 0)
			};
			previewButton.FillColor = Color.MediumSlateBlue;
			previewButton.ForeColor = Color.White;
			previewButton.HoverState.FillColor = Color.SlateBlue;
			previewButton.HoverState.ForeColor = Color.WhiteSmoke;
			previewButton.PressedColor = Color.Indigo;
			previewButton.Click += PreviewButton_Click;
			buttonPanel.Controls.Add(previewButton);

			applyButton = new Guna2Button
			{
				Text = "Apply Changes",
				AutoSize = true,
				Margin = new Padding(0, 0, 10, 0)
			};
			applyButton.FillColor = Color.ForestGreen;
			applyButton.ForeColor = Color.White;
			applyButton.HoverState.FillColor = Color.DarkGreen;
			applyButton.HoverState.ForeColor = Color.WhiteSmoke;
			applyButton.PressedColor = Color.SeaGreen;
			applyButton.Click += ApplyButton_Click;
			buttonPanel.Controls.Add(applyButton);

			mainPanel.Controls.Add(buttonPanel, 0, row);
			mainPanel.SetColumnSpan(buttonPanel, 3);
			row++;

            // Progress bar
			progressBar = new Guna2ProgressBar
			{
				Text = "",
				Size = new Size(300, 20),
				FillColor = Color.Gray,
				ProgressColor = Color.Teal,
				Style = ProgressBarStyle.Continuous,
				AutoRoundedCorners = true,
				BorderRadius = 10,
				Visible = false   // ✅ hide by default
			};
			mainPanel.Controls.Add(progressBar, 0, row);
			mainPanel.SetColumnSpan(progressBar, 3);
			row++;

           
            // Status label
            statusLabel = new Label
            {
                Text = "Ready",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.DarkGreen
            };
            mainPanel.Controls.Add(statusLabel, 0, row);
            mainPanel.SetColumnSpan(statusLabel, 3);
            
            this.Controls.Add(mainPanel);
        }
        
        private void UpdateStatus(string message)
        {
            statusLabel.Text = message;
        //   statusLabel.Refresh();
        //    Application.DoEvents();
        }
        
        private void SteamBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Steam Folder";
                dialog.SelectedPath = steamPathTextBox.Text;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    steamPathTextBox.Text = dialog.SelectedPath;
                    ValidateSteamPath(dialog.SelectedPath);
                }
            }
        }
        
        // MODIFIED: This method now uses the new custom dialog for selection
        private void LuaBrowseButton_Click(object sender, EventArgs e)
        {
            using (var selector = new SourceSelectionDialog())
            {
                DialogResult result = selector.ShowDialog();

                if (result == DialogResult.OK) // Folder selected (mapped to DialogResult.OK in custom dialog)
                {
                    using (var folderDialog = new FolderBrowserDialog())
                    {
                        folderDialog.Description = "Select Folder with Lua + Manifest Files";
                        folderDialog.SelectedPath = luaFolderTextBox.Text;
                        if (folderDialog.ShowDialog() == DialogResult.OK)
                        {
                            luaFolderTextBox.Text = folderDialog.SelectedPath;
                        }
                    }
                }
                else if (result == DialogResult.Yes) // Zip File selected (mapped to DialogResult.Yes in custom dialog)
                {
                    using (var fileDialog = new OpenFileDialog())
                    {
                        fileDialog.Title = "Select Zip File (.zip) containing Lua + Manifest Files";
                        // Filter specifically for .zip files
                        fileDialog.Filter = "Zip Files (*.zip)|*.zip|All files (*.*)|*.*";
                        if (fileDialog.ShowDialog() == DialogResult.OK)
                        {
                            luaFolderTextBox.Text = fileDialog.FileName;
                        }
                    }
                }
                // If DialogResult.Cancel is returned, do nothing.
            }
        }
        
        // NEW: DragEnter handler for the Lua text box
        private void LuaFolderTextBox_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the data being dragged is a file or folder path
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Get the first path
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths != null && paths.Length > 0)
                {
                    string path = paths[0];
                    // Check if it's a folder or a .zip file
                    if (Directory.Exists(path) || (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
                    {
                        e.Effect = DragDropEffects.Copy; // Show copy cursor
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None; // Show no-drop cursor
        }

        // NEW: DragDrop handler for the Lua text box
        private void LuaFolderTextBox_DragDrop(object sender, DragEventArgs e)
        {
            // Get the dragged paths
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths != null && paths.Length > 0)
            {
                // Set the TextBox text to the path of the first dragged item
                luaFolderTextBox.Text = paths[0];
            }
        }
        
        private void AppListBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select AppList Output Folder";
                dialog.SelectedPath = appListPathTextBox.Text;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    appListPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }
        
        private bool ValidateSteamPath(string path)
        {
            var configPath = Path.Combine(path, "config", "config.vdf");
            if (!File.Exists(configPath))
            {
                MessageBox.Show(
                    $"No config.vdf found in {path}\\config\\\nPlease select a valid Steam installation folder.",
                    "Invalid Steam Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return false;
            }
            return true;
        }
        
        private void ValidateButton_Click(object sender, EventArgs e)
        {
            var errors = new List<string>();
            var luaPath = luaFolderTextBox.Text;
            
            // Validate Steam path
            if (string.IsNullOrWhiteSpace(steamPathTextBox.Text))
                errors.Add("Steam folder not selected");
            else if (!Directory.Exists(steamPathTextBox.Text))
                errors.Add("Steam folder does not exist");
            else if (!ValidateSteamPath(steamPathTextBox.Text))
                errors.Add("Invalid Steam installation folder");
            
            // Validate Lua folder/zip (LOGIC RESTORED for zip support)
            bool isDirectory = Directory.Exists(luaPath);
            bool isZipFile = File.Exists(luaPath) && Path.GetExtension(luaPath).Equals(".zip", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(luaPath))
                errors.Add("Lua folder/zip not selected");
            else if (!isDirectory && !isZipFile)
                errors.Add("Lua folder/zip path does not exist or is not a folder/zip");
            else
            {
                try
                {
                    // Try to find the Lua file within the folder or zip
                    FindFirstLuaFileContent(luaPath); 
                }
                catch (FileNotFoundException)
                {
                    errors.Add($"No .lua file found in selected {(isZipFile ? "zip file" : "folder")}");
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
                {
                    if(isZipFile) errors.Add($"File is not a valid zip archive: {luaPath}");
                }
            }
            
            // Validate AppList folder
            if (string.IsNullOrWhiteSpace(appListPathTextBox.Text))
                errors.Add("AppList folder not selected");
            else if (!Directory.Exists(appListPathTextBox.Text))
                errors.Add("AppList folder does not exist");
            
            if (errors.Any())
            {
                MessageBox.Show(
                    string.Join("\n• ", new[] { "Validation Errors:" }.Concat(errors)),
                    "Validation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            else
            {
                MessageBox.Show(
                    "All paths are valid!",
                    "Validation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
        
        private void PreviewButton_Click(object sender, EventArgs e)
        {
            if (!ValidatePaths())
                return;
            
            try
            {
                // LOGIC RESTORED: Get content and filename from folder/zip
                var (luaContent, luaFileName) = FindFirstLuaFileContent(luaFolderTextBox.Text); 
                var entries = ExtractAllAddAppIdValues(luaContent); // Pass content
                
                var previewText = $"Found {entries.Count} app entries in {luaFileName}:\n\n";
                
                var displayEntries = entries.Take(10).ToList();
                foreach (var entry in displayEntries)
                {
                    previewText += $"AppID: {entry.AppId}, Key: {entry.Key.Substring(0, 16)}...\n";
                }
                
                if (entries.Count > 10)
                    previewText += $"\n... and {entries.Count - 10} more entries";
                
                // Count manifest files (LOGIC RESTORED for zip support)
                var manifestCount = GetManifestFileCount(luaFolderTextBox.Text);
                previewText += $"\n\nManifest files to copy: {manifestCount}";
                
                MessageBox.Show(previewText, "Preview Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error previewing changes:\n{ex.Message}", "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            if (!ValidatePaths())
                return;
            
            // Disable buttons during operation
            SetButtonsEnabled(false);
            progressBar.Visible = true;
            progressBar.Value = 0;
            progressBar.Maximum = 6;
            
            try
            {
                await Task.Run(() => EditConfig());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Error occurred");
            }
            finally
            {
                SetButtonsEnabled(true);
                progressBar.Visible = false;
            }
        }
        
        private void SetButtonsEnabled(bool enabled)
        {
            validateButton.Enabled = enabled;
            previewButton.Enabled = enabled;
            applyButton.Enabled = enabled;
            steamBrowseButton.Enabled = enabled;
            luaBrowseButton.Enabled = enabled;
            appListBrowseButton.Enabled = enabled;
        }
        
        private bool ValidatePaths()
        {
            var errors = new List<string>();
            var luaPath = luaFolderTextBox.Text; // Use local variable for cleanliness

            if (string.IsNullOrWhiteSpace(steamPathTextBox.Text) || !Directory.Exists(steamPathTextBox.Text))
                errors.Add("Invalid Steam folder");
            
            // Check if luaFolderTextBox.Text is a directory OR a zip file (LOGIC RESTORED for zip support)
            bool isDirectory = Directory.Exists(luaPath);
            bool isZipFile = File.Exists(luaPath) && Path.GetExtension(luaPath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
            
            if (string.IsNullOrWhiteSpace(luaPath) || (!isDirectory && !isZipFile))
                errors.Add("Invalid Lua folder/zip path (must be a folder or a .zip file)");
            
            if (string.IsNullOrWhiteSpace(appListPathTextBox.Text) || !Directory.Exists(appListPathTextBox.Text))
                errors.Add("Invalid AppList folder");
            
            if (errors.Any())
            {
                MessageBox.Show(string.Join("\n", errors), "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            return true;
        }
        
		private void EditConfig()
        {
                var steamFolder = steamPathTextBox.Text;
                var luaFolder = luaFolderTextBox.Text;
                var appListFolder = appListPathTextBox.Text;
                var configPath = Path.Combine(steamFolder, "config", "config.vdf");

                // Step 1: Copy manifest files (LOGIC RESTORED for zip support)
                BeginInvoke(new Action(() => {
                        UpdateStatus("Copying manifest files...");
                        progressBar.Value = 1;
                }));
                CopyManifestFiles(luaFolder, steamFolder);

                // Step 2: Parse Lua file (LOGIC RESTORED for zip support)
                BeginInvoke(new Action(() => {
                        UpdateStatus("Parsing Lua file...");
                        progressBar.Value = 2;
                }));
                var (luaContent, luaFileName) = FindFirstLuaFileContent(luaFolder);
                var entries = ExtractAllAddAppIdValues(luaContent); // Pass content
                string baseLuaFileName = Path.GetFileNameWithoutExtension(luaFileName); // Get name for SteamDB call

                // Step 3: Create backup
                BeginInvoke(new Action(() => {
                        UpdateStatus("Creating backup...");
                        progressBar.Value = 3;
                }));
                var backupPath = CreateBackup(configPath);

                // Step 4: Read and modify config
                BeginInvoke(new Action(() => {
                        UpdateStatus("Reading config file...");
                        progressBar.Value = 4;
                }));
                var lines = File.ReadAllLines(configPath).ToList();

                // Find the "CurrentCellID" line, or fallback to "RecentDownloadRate"
                int targetLine = lines.FindIndex(line => line.Contains("\"CurrentCellID\""));
                string searchedFor = "CurrentCellID";
                
                if (targetLine == -1)
                {
                        targetLine = lines.FindIndex(line => line.Contains("\"RecentDownloadRate\""));
                        searchedFor = "RecentDownloadRate";
                }
                
                if (targetLine == -1)
                        throw new InvalidOperationException("Neither \"CurrentCellID\" nor \"RecentDownloadRate\" found in config.");

                // Find the closing brace directly before the target line
                int insertIndex = -1;
                for (int i = targetLine - 1; i >= 0; i--)
                {
                        if (lines[i].Trim() == "}")
                        {
                                insertIndex = i;
                                break;
                        }
                }
                if (insertIndex == -1)
                        throw new InvalidOperationException($"Could not find closing brace before {searchedFor}.");

                // Detect proper indent from last AppID
                string indent = "";
                for (int i = insertIndex - 1; i >= 0; i--)
                {
                        if (Regex.IsMatch(lines[i].Trim(), "^\"\\d+\"$"))
                        {
                                indent = DetectIndentation(lines, i);
                                break;
                        }
                }
                if (string.IsNullOrWhiteSpace(indent))
                        indent = "\t\t\t\t\t"; // fallback: 5 tabs

                string innerIndent = indent + "\t";

                // Build new blocks
                var insertBlocks = new List<string>();
                foreach (var entry in entries)
                {
                        insertBlocks.Add($"{indent}\"{entry.AppId}\"");
                        insertBlocks.Add($"{indent}{{");
                        insertBlocks.Add($"{innerIndent}\"DecryptionKey\"\t\t\"{entry.Key}\"");
                        insertBlocks.Add($"{indent}}}");
                }

                // Insert before the closing brace
                lines.InsertRange(insertIndex, insertBlocks);

                // Step 5: Write config
                BeginInvoke(new Action(() => {
                        UpdateStatus("Writing modified config...");
                        progressBar.Value = 5;
                }));
                File.WriteAllLines(configPath, lines);

                // Step 6: Create AppID files
                BeginInvoke(new Action(() => {
                        UpdateStatus("Creating AppID files...");
                        progressBar.Value = 6;
                }));

                var createdFiles = new List<string>();

                // Get game name from the .lua file name (e.g., 1593500.lua)
                string gameName = GetGameNameFromSteamDB(baseLuaFileName); // Use the correct variable

                foreach (var entry in entries)
                {
                        string txtPath = GetNextAvailableTxtPath(appListFolder);
                        File.WriteAllText(txtPath, $"{entry.AppId} - {gameName}");
                        createdFiles.Add(Path.GetFileName(txtPath));
                }

                // Also create one file that just stores the Lua filename
                string luaNameTxtPath = GetNextAvailableTxtPath(appListFolder);
                File.WriteAllText(luaNameTxtPath, $"{baseLuaFileName} - {gameName}");
                createdFiles.Add(Path.GetFileName(luaNameTxtPath));

                SaveSettings();

                var successMsg = $"Successfully processed {entries.Count} app entries!\n\n" +
                        $"Backup created: {Path.GetFileName(backupPath)}\n" +
                        $"AppID files created: {string.Join(", ", createdFiles.Take(5))}" +
                        (createdFiles.Count > 5 ? $" and {createdFiles.Count - 5} more..." : "");

                BeginInvoke(new Action(() => {
                        MessageBox.Show(successMsg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        UpdateStatus("Operation completed successfully");
                }));
        }

        
        // RESTORED: Finds first .lua file and returns its content and file name, handling zip files
        private (string content, string fileName) FindFirstLuaFileContent(string path)
        {
            // 1. Check if the path is a ZIP file
            if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    // Find the first .lua entry in the zip archive
                    var luaEntry = archive.Entries
                        .FirstOrDefault(e => e.FullName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));

                    if (luaEntry == null)
                        throw new FileNotFoundException($"No .lua file found in the zip archive: {path}");

                    using (var stream = luaEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        // Read content without extraction
                        return (reader.ReadToEnd(), luaEntry.FullName); 
                    }
                }
            }
            // 2. Otherwise, treat as a regular directory
            else if (Directory.Exists(path))
            {
                var luaFiles = Directory.GetFiles(path, "*.lua", SearchOption.TopDirectoryOnly);
                if (luaFiles.Length == 0)
                    throw new FileNotFoundException($"No .lua file found in selected folder: {path}");
                    
                var luaPath = luaFiles[0];
                return (File.ReadAllText(luaPath), Path.GetFileName(luaPath));
            }
            
            throw new DirectoryNotFoundException($"Path is neither a valid directory nor a zip file: {path}");
        }

        // RESTORED/MODIFIED: Now accepts the content as a string
        private List<AppEntry> ExtractAllAddAppIdValues(string content)
        {
            var pattern = @"addappid\s*\(\s*(\d+)\s*,\s*[01]\s*,\s*[""']([a-fA-F0-9]{64})[""']\s*\)";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            
            if (matches.Count == 0)
                throw new InvalidOperationException("No valid addappid(appid, 0 or 1, \"key\") lines found in Lua file.");
            
            return matches.Cast<Match>()
                         .Select(m => new AppEntry { AppId = m.Groups[1].Value, Key = m.Groups[2].Value })
                         .ToList();
        }
        
        private string CreateBackup(string originalPath)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = $"{originalPath}.bak_{timestamp}";
            
            File.Copy(originalPath, backupPath);
            return backupPath;
        }
        
        // RESTORED: Handles both folder and zip file as a source for manifest files
        private void CopyManifestFiles(string sourcePath, string steamFolder)
        {
            var depotCacheFolder = Path.Combine(steamFolder, "depotcache");
            Directory.CreateDirectory(depotCacheFolder);
            
            // 1. Check if path is a ZIP file
            if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(sourcePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Check if the entry is a manifest file
                        if (entry.FullName.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                        {
                            var destFile = Path.Combine(depotCacheFolder, Path.GetFileName(entry.FullName));
                            
                            // ExtractToFile allows copying the stream directly to a file
                            entry.ExtractToFile(destFile, overwrite: true);
                        }
                    }
                }
            }
            // 2. Otherwise, treat as a regular directory
            else if (Directory.Exists(sourcePath))
            {
                var manifestFiles = Directory.GetFiles(sourcePath, "*.manifest");
                foreach (var manifestFile in manifestFiles)
                {
                    var destFile = Path.Combine(depotCacheFolder, Path.GetFileName(manifestFile));
                    File.Copy(manifestFile, destFile, true);
                }
            }
        }
        
        // RESTORED: Helper method to count manifest files for preview logic
        private int GetManifestFileCount(string sourcePath)
        {
            if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(sourcePath))
                {
                    return archive.Entries.Count(e => e.FullName.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase));
                }
            }
            else if (Directory.Exists(sourcePath))
            {
                return Directory.GetFiles(sourcePath, "*.manifest").Length;
            }
            return 0;
        }

        private string DetectIndentation(List<string> lines, int index)
        {
            if (index < 0 || index >= lines.Count)
                return "\t";
            
            var line = lines[index];
            var match = Regex.Match(line, @"^(\s*)");
            if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                var indent = match.Groups[1].Value;
                return indent.Contains("\t") ? "\t" : indent;
            }
            return "\t";
        }
        
        private string GetNextAvailableTxtPath(string folderPath)
        {
            int counter = 0;
            string filePath;
            do
            {
                filePath = Path.Combine(folderPath, $"{counter}.txt");
                counter++;
            } while (File.Exists(filePath));
            
            return filePath;
        }
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    var json = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    
                    steamPathTextBox.Text = settings.SteamFolder ?? "";
                    appListPathTextBox.Text = settings.AppListFolder ?? "";
                    // Don't load lua folder - let user select each time
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                var settings = new Settings
                {
                    SteamFolder = steamPathTextBox.Text,
                    AppListFolder = appListPathTextBox.Text
                    // Don't save lua folder - let user select each time
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SETTINGS_FILE, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        public class AppEntry
        {
            public string AppId { get; set; }
            public string Key { get; set; }
        }
    }

    // NEW CUSTOM DIALOG FORM (RESTORED and FIXED)
    public class SourceSelectionDialog : Form
    {
        public SourceSelectionDialog()
        {
            this.Text = "Select Source Type";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Size = new Size(350, 150);
            
            this.BackColor = Color.White;
            this.ControlBox = false; // Hide default window controls

            var promptLabel = new Label
            {
                Text = "Choose the source type for Lua and Manifest files:",
                Location = new Point(10, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.75f, FontStyle.Regular),
            };

            var folderButton = new Guna2Button
            {
                Text = "Folder",
                Location = new Point(20, 60),
                Size = new Size(95, 30),
                FillColor = Color.Teal,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            };
            // FIX: Add explicit click handler for Folder button
            folderButton.Click += (sender, e) => { this.DialogResult = DialogResult.OK; this.Close(); };


            var zipButton = new Guna2Button
            {
                Text = "Zip File",
                Location = new Point(125, 60),
                Size = new Size(95, 30),
                FillColor = Color.MediumSlateBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            };
            // FIX: Add explicit click handler for Zip File button
            zipButton.Click += (sender, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };

            var cancelButton = new Guna2Button
            {
                Text = "Cancel",
                Location = new Point(230, 60),
                Size = new Size(95, 30),
                FillColor = Color.Gray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            };
            // FIX: Add explicit click handler to ensure Cancel button closes the dialog
            cancelButton.Click += (sender, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };


            this.Controls.Add(promptLabel);
            this.Controls.Add(folderButton);
            this.Controls.Add(zipButton);
            this.Controls.Add(cancelButton);

            this.AcceptButton = folderButton;
            this.CancelButton = cancelButton;
        }
    }
    
    // Program entry point
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}