using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ConfigFileEditor
{
    public partial class MainForm : Form
    {
        private string? currentFilePath = null;
        private readonly IniFileHandler _iniFileHandler = new IniFileHandler();

        private System.Windows.Forms.Timer? searchDebounceTimer;
        private System.Windows.Forms.Timer? _statusResetTimer;
        private CancellationTokenSource? searchCancellationTokenSource;
        private bool isSearchRunning = false;

        public MainForm()
        {
            InitializeComponent();
            LoadMruList();
            EnableTreeViewDragDrop();
            NewFile();
            InitDebounceTimer();

            this.FormClosing += Form1_FormClosing;
        }

        private void InitDebounceTimer()
        {
            // Set up the debounce timer
            searchDebounceTimer = new System.Windows.Forms.Timer();
            searchDebounceTimer.Interval = 300; // milliseconds delay
            searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private void NewFile()
        {
            if (!string.IsNullOrEmpty(textFilter?.Text))
            {
                textFilter.Text = "";
            }

            this.Text = "New Configuration File";
            UpdateStatus("New Configuration");
        }

        private DialogResult CheckUnsavedChanges()
        {
            if (!_iniFileHandler.IsDirty) return DialogResult.None;

            // Prompt to save changes
            DialogResult result = MessageBox.Show(
                "You have unsaved changes. Save them before exiting?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                // User wants to save. Call your save logic.
                SaveFile();
                ClearChanged();
            }

            return result;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            DialogResult result = CheckUnsavedChanges();
            if (result == DialogResult.Cancel) e.Cancel = true;
        }
            
        private void newFileToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DialogResult result = CheckUnsavedChanges();
            if (result == DialogResult.Cancel) return;

            // Reset everything to a clean, blank slate
            _iniFileHandler.NewFile();
            treeViewConfigOptions.Nodes.Clear();
            currentFilePath = null;
            
            // Reset detail panels
            sectionName.Text = "";
            keyName.Text = "";
            value.Text = "";
            value.Enabled = false;

            // Keep editing capabilities wide open
            buttonAddSection.Enabled = true;
            buttonAddSetting.Enabled = true;
            buttonRemoveSetting.Enabled = false;

            NewFile();
        }

        private void EnableTreeViewDragDrop()
        {
            // 1. Enable dropping on the TreeView
            treeViewConfigOptions.AllowDrop = true;

            // 2. Wire up the drag-and-drop events
            treeViewConfigOptions.ItemDrag += treeView1_ItemDrag;
            treeViewConfigOptions.DragEnter += treeView1_DragEnter;
            treeViewConfigOptions.DragOver += treeView1_DragOver;
            treeViewConfigOptions.DragDrop += treeView1_DragDrop;
        }

        private void treeView1_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            // Prevent dragging if a search filter is currently applied
            if (!string.IsNullOrEmpty(textFilter.Text) || isSearchRunning) return;

            if (e.Button == MouseButtons.Left && e.Item != null)
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }
        }

        private void treeView1_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private void treeView1_DragOver(object? sender, DragEventArgs e)
        {
            Point targetPoint = treeViewConfigOptions.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeViewConfigOptions.GetNodeAt(targetPoint);

            treeViewConfigOptions.SelectedNode = targetNode;

            if (targetNode == null || e.Data?.GetData(typeof(TreeNode)) is not TreeNode draggedNode)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            // Restrict Sections (Level 0) to only drop on other Sections
            if (draggedNode.Level == 0 && targetNode.Level != 0)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            // Same node
            if (draggedNode == targetNode)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;
        }

        private void treeView1_DragDrop(object? sender, DragEventArgs e)
        {
            Point targetPoint = treeViewConfigOptions.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeViewConfigOptions.GetNodeAt(targetPoint);

            if (targetNode == null || e.Data?.GetData(typeof(TreeNode)) is not TreeNode draggedNode || draggedNode == targetNode) return;

            // Grab the actual data entries bound to these nodes

            if (draggedNode.Tag is not IniEntry draggedEntry || targetNode.Tag is not IniEntry targetEntry) return;

            // --- STEP 1: Update the single source of truth (iniStructure) ---

            if (draggedNode.Level == 0 && targetNode.Level == 0 && 
                draggedEntry is SectionEntry draggedSection && targetEntry is SectionEntry targetSection)
            {
                // CASE A: Moving a Section. We must move the SectionEntry AND all its settings/comments block.
                _iniFileHandler.MoveSectionInDataStructure(draggedSection, targetSection);
            }
            else if (draggedNode.Level == 1)
            {
                var iniStructure = _iniFileHandler.IniStructure;

                // Find where the dragged entry currently lives and pull it out
                iniStructure.Remove(draggedEntry);

                // Figure out where to insert it based on where it was dropped
                int targetIndex = iniStructure.IndexOf(targetEntry);

                if (targetNode.Level == 0)
                {
                    // Dropped directly on a section header node -> Insert it right after the section header
                    iniStructure.Insert(targetIndex + 1, draggedEntry);
                }
                else if (targetNode.Level == 1)
                {
                    // Dropped onto another option -> Insert it right at that option's position
                    iniStructure.Insert(targetIndex, draggedEntry);
                }
            }

            // --- STEP 2: Update the UI to match ---
            if (draggedNode.Level == 0 && targetNode.Level == 0)
            {
                draggedNode.Remove();
                treeViewConfigOptions.Nodes.Insert(targetNode.Index, draggedNode);
            }
            else if (draggedNode.Level == 1)
            {
                draggedNode.Remove();
                if (targetNode.Level == 0)
                {
                    targetNode.Nodes.Insert(0, draggedNode);
                    targetNode.Expand();
                }
                else if (targetNode.Level == 1)
                {
                    targetNode.Parent.Nodes.Insert(targetNode.Index, draggedNode);
                }
            }

            treeViewConfigOptions.SelectedNode = draggedNode;

            // --- STEP 3: Rebuild the fast-lookup Dictionary ---
            _iniFileHandler.RebuildSectionsDictionary();

            // --- STEP 4: Mark as changed
            MarkChanged();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openINIFileDialog.ShowDialog() == DialogResult.OK)
            {
                LoadFile(openINIFileDialog.FileName);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                if (saveINIFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveINIFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }
            SaveFile();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (value.Focused)
            {
                value.Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (value.Focused)
            {
                value.Paste();
            }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (value.Focused)
            {
                value.SelectAll();
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is SettingEntry settingEntry) // A setting is selected
            {
                sectionName.Text = e.Node.Parent.Text;
                keyName.Text = e.Node.Text;
                value.Text = settingEntry.Value;

                commentCheckBox.Visible = true;
                commentCheckBox.Checked = settingEntry.IsCommentedOut;

                value.Enabled = !settingEntry.IsCommentedOut;
                buttonAddSetting.Enabled = true; // Can add a setting to the parent section
                buttonRemoveSetting.Enabled = true;
            }
            else if (e.Node?.Tag is SectionEntry) // A section is selected
            {
                sectionName.Text = e.Node.Text;
                keyName.Text = "";
                value.Text = "";

                commentCheckBox.Visible = false;
                value.Enabled = false;
                buttonAddSetting.Enabled = true; // Can add a setting to this section
                buttonRemoveSetting.Enabled = false;
            }
            else // Nothing valid is selected
            {
                sectionName.Text = "";
                keyName.Text = "";
                value.Text = "";

                commentCheckBox.Visible = false;
                value.Enabled = false;
                buttonAddSetting.Enabled = false; // Cannot add a setting if nothing is selected
                buttonRemoveSetting.Enabled = false;
            }
        }

        private void value_TextChanged(object sender, EventArgs e)
        {
            if (treeViewConfigOptions.SelectedNode?.Parent != null)
            {
                SettingEntry? settingEntry = treeViewConfigOptions.SelectedNode.Tag as SettingEntry;
                if (settingEntry != null && settingEntry.Value != value.Text)
                {
                    settingEntry.Value = value.Text;
                    MarkChanged();

                    // Keep sections dictionary in sync
                    if (treeViewConfigOptions.SelectedNode.Parent != null)
                    {
                        string sectionName = treeViewConfigOptions.SelectedNode.Parent.Name;
                        string settingName = treeViewConfigOptions.SelectedNode.Name;
                        if (_iniFileHandler.Sections.ContainsKey(sectionName) && _iniFileHandler.Sections[sectionName].ContainsKey(settingName))
                        {
                            _iniFileHandler.Sections[sectionName][settingName] = value.Text;
                        }
                    }
                }
            }
        }

        private void buttonAddSetting_Click(object sender, EventArgs e)
        {
            string sectionName;
            TreeNode? sectionNode;

            if (treeViewConfigOptions.SelectedNode == null)
            {
                sectionName = "[Default]";
                TreeNode[] nodes = treeViewConfigOptions.Nodes.Find(sectionName, false);
                if (nodes.Length > 0)
                {
                    sectionNode = nodes[0];
                }
                else
                {
                    sectionNode = treeViewConfigOptions.Nodes.Add(sectionName, sectionName);
                    if (!_iniFileHandler.Sections.ContainsKey(sectionName))
                    {
                        _iniFileHandler.Sections[sectionName] = new Dictionary<string, string>();
                    }
                }
            }
            else if (treeViewConfigOptions.SelectedNode.Parent == null) // A section is selected
            {
                sectionNode = treeViewConfigOptions.SelectedNode;
                sectionName = sectionNode.Name;
            }
            else // A setting is selected
            {
                sectionNode = treeViewConfigOptions.SelectedNode.Parent;
                sectionName = sectionNode.Name;
            }

            using (var form = new InputForm(sectionName))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    string? settingName = form.SettingName;
                    string? settingValue = form.SettingValue;

                    if (!string.IsNullOrEmpty(settingName) && settingValue != null)
                    {
                        // 1. Update data structures
                        if (!_iniFileHandler.Sections.ContainsKey(sectionName))
                        {
                            _iniFileHandler.Sections[sectionName] = new Dictionary<string, string>();
                        }
                        _iniFileHandler.Sections[sectionName][settingName] = settingValue;
                        var newSetting = new SettingEntry { Key = settingName, Value = settingValue };
                        var iniStructure = _iniFileHandler.IniStructure;

                        // 2. Find the correct insertion index in iniStructure
                        int insertIndex = -1;

                        // Find the section entry in the main structure
                        int sectionEntryIndex = -1;
                        for (int i = 0; i < iniStructure.Count; i++)
                        {
                            if (iniStructure[i] is SectionEntry se && se.SectionName == sectionName)
                            {
                                sectionEntryIndex = i;
                                break;
                            }
                        }

                        if (sectionEntryIndex != -1)
                        {
                            // Find the end of the section
                            int endOfSection = sectionEntryIndex + 1;
                            while (endOfSection < iniStructure.Count && !(iniStructure[endOfSection] is SectionEntry))
                            {
                                endOfSection++;
                            }

                            // Work backwards from the end of the section
                            insertIndex = endOfSection;
                            for (int i = endOfSection - 1; i > sectionEntryIndex; i--)
                            {
                                if (iniStructure[i] is SettingEntry)
                                {
                                    insertIndex = i + 1;
                                    break;
                                }
                                if (iniStructure[i] is BlankLineEntry)
                                {
                                    insertIndex = i;
                                }
                            }
                        }
                        else if (sectionName == "[Default]")
                        {
                            // Find the last setting in the default section
                            int lastDefaultSetting = -1;
                            for (int i = 0; i < iniStructure.Count; i++)
                            {
                                if (iniStructure[i] is SectionEntry) break; // Stop at the first section
                                if (iniStructure[i] is SettingEntry)
                                {
                                    lastDefaultSetting = i;
                                }
                            }
                            insertIndex = lastDefaultSetting + 1;
                        }
                        else
                        {
                            // Section not found, add to the end
                            insertIndex = iniStructure.Count;
                        }

                        iniStructure.Insert(insertIndex, newSetting);

                        // Determine if this is the last section
                        bool isLastSection = true;
                        int searchStartIndex = (sectionEntryIndex == -1) ? 0 : sectionEntryIndex + 1;
                        for (int i = searchStartIndex; i < iniStructure.Count; i++)
                        {
                            if (iniStructure[i] is SectionEntry)
                            {
                                isLastSection = false;
                                break;
                            }
                        }

                        // Add a blank line if the next line is a comment, but not for the last section
                        if (!isLastSection && insertIndex < iniStructure.Count && iniStructure[insertIndex] is CommentEntry)
                        {
                            iniStructure.Insert(insertIndex + 1, new BlankLineEntry { RawLine = "" });
                        }

                        // 3. Update UI
                        TreeNode newSettingNode = sectionNode.Nodes.Add(settingName, settingName);
                        newSettingNode.Tag = newSetting;
                        treeViewConfigOptions.SelectedNode = newSettingNode;
                        sectionNode.Expand();
                    }

                    MarkChanged();
                }
            }
        }

        private void commentCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (treeViewConfigOptions.SelectedNode?.Tag is SettingEntry settingEntry)
            {
                // Prevent event from firing when the node is first selected and the checkbox is set
                if (settingEntry.IsCommentedOut == commentCheckBox.Checked) return;

                settingEntry.IsCommentedOut = commentCheckBox.Checked;

                if (settingEntry.IsCommentedOut)
                {
                    keyName.Text = treeViewConfigOptions.SelectedNode.Text = $"; {settingEntry.Key}";
                    value.Enabled = false;
                }
                else
                {
                    keyName.Text = treeViewConfigOptions.SelectedNode.Text = settingEntry.Key;
                    value.Enabled = true;
                }
            }
            MarkChanged();
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            if (treeViewConfigOptions.SelectedNode?.Parent != null && treeViewConfigOptions.SelectedNode.Tag is SettingEntry settingEntry)
            {
                string sectionName = treeViewConfigOptions.SelectedNode.Parent.Name;
                string settingName = treeViewConfigOptions.SelectedNode.Name;

                // 1. Remove from data structures
                if (_iniFileHandler.Sections.ContainsKey(sectionName))
                {
                    _iniFileHandler.Sections[sectionName].Remove(settingName);
                }
                _iniFileHandler.IniStructure.Remove(settingEntry);

                // 2. Remove from UI
                treeViewConfigOptions.SelectedNode.Remove();
            }

            MarkChanged();
        }

        private void MarkChanged()
        {
            _iniFileHandler.MarkDirty();
            if (this.Text.EndsWith("*")) return;
            this.Text += "*";
        }

        private void ClearChanged()
        {
            _iniFileHandler.ClearDirty();
            this.Text = this.Text.Replace("*", "");
        }

        private async void LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Clear detail panel
            sectionName.Text = "";
            keyName.Text = "";
            value.Text = "";
            value.Enabled = false;
            buttonAddSetting.Enabled = true;
            buttonRemoveSetting.Enabled = false;

            _iniFileHandler.LoadFile(path);

            AddToMru(path);
            UpdateStatus($"File Loaded: {path}");
            this.Text = "Editing " + path;
            currentFilePath = path;

            string query = textFilter?.Text.Trim().ToLower() ?? "";
            if (query == "")
            {
                UpdateTreeView();
                return;
            }

            await ExecuteSearchAsync(query, "File Loaded. ");
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveFileAs();
                return;
            }

            _iniFileHandler.CurrentFilePath = currentFilePath;
            _iniFileHandler.SaveFile();
            UpdateStatus($"File saved: {currentFilePath}");
            ClearChanged();
        }

        private void SaveFileAs()
        {
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Configuration Files (*.ini;*.cfg)|*.ini;*.cfg|All Files (*.*)|*.*";
            saveFileDialog.DefaultExt = "ini";
            saveFileDialog.Title = "Save Configuration File As";

            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            // Capture the chosen path
            currentFilePath = saveFileDialog.FileName;

            // Now that currentFilePath is valid, call the core SaveFile() to write the data
            SaveFile();

            // Add to your Most Recently Used list if applicable
            AddToMru(currentFilePath);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveFile();
                return true; // Indicate the key was handled
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // MRU
        private List<string> mruFiles = new List<string>();
        private readonly string mruPath = Path.Combine(Application.StartupPath, "recent_files.txt");
        private const int MaxMruItems = 5;

        private void LoadMruList()
        {
            if (File.Exists(mruPath))
            {
                try
                {
                    string json = File.ReadAllText(mruPath);
                    // Using a simple split if you don't want to add JSON dependencies, 
                    // but here is the logic for a basic string list:
                    mruFiles = json.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                catch { /* Handle file error */ }
            }
            RefreshMruMenu();
        }

        private void SaveMruList()
        {
            File.WriteAllLines(mruPath, mruFiles);
        }

        private void AddToMru(string filePath)
        {
            if (mruFiles.Contains(filePath))
                mruFiles.Remove(filePath);

            mruFiles.Insert(0, filePath);

            if (mruFiles.Count > MaxMruItems)
                mruFiles.RemoveAt(MaxMruItems);

            SaveMruList();
            RefreshMruMenu();
        }

        private void RefreshMruMenu()
        {
            // Find the File ToolStripMenuItem
            var fileMenu = menuStrip1.Items.Cast<ToolStripMenuItem>().FirstOrDefault(x => x.Text == "&File" || x.Text == "File");
            if (fileMenu == null) return;

            // Remove existing MRU items to prevent duplicates (identify them by a Tag or Name)
            for (int i = fileMenu.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (fileMenu.DropDownItems[i].Tag?.ToString() == "MRU")
                    fileMenu.DropDownItems.RemoveAt(i);
            }

            if (mruFiles.Count == 0) return;

            fileMenu.DropDownItems.Add(new ToolStripSeparator { Tag = "MRU" });

            foreach (var path in mruFiles)
            {
                var item = new ToolStripMenuItem(path) { Tag = "MRU" };
                item.Click += (s, e) => LoadFile(path); // Update LoadFile to accept a path!
                fileMenu.DropDownItems.Add(item);
            }
        }

        private void UpdateStatus(string message)
        {
            toolStripStatusBarLabel.Text = $"{DateTime.Now.ToShortTimeString()} - {message}";

            if (_statusResetTimer == null)
            {
                _statusResetTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                _statusResetTimer.Tick += (s, e) =>
                {
                    toolStripStatusBarLabel.Text = "Ready";
                    _statusResetTimer!.Stop();
                };
            }
            _statusResetTimer.Stop();
            _statusResetTimer.Start();
        }

        private TreeNode EnsureDefaultSectionNode()
        {
            var existing = treeViewConfigOptions.Nodes["[Default]"];
            if (existing != null) return existing;
            var node = treeViewConfigOptions.Nodes.Add("[Default]", "[Default]");
            node.Tag = new SectionEntry { SectionName = "[Default]" };
            return node;
        }

        private void UpdateTreeView(string searchTerm = "")
        {
            treeViewConfigOptions.Nodes.Clear();

            // Normalize search term for case-insensitive matching
            string query = searchTerm.Trim().ToLower();
            bool isSearching = !string.IsNullOrEmpty(query);

            TreeNode? currentSectionNode = null;

            // Special Handling for the [Default] section
            // Check if there are any settings before the first explicit section
            var firstSectionIndex = _iniFileHandler.IniStructure.FindIndex(e => e is SectionEntry);
            var defaultSettingsExist = _iniFileHandler.IniStructure
                .Take(firstSectionIndex == -1 ? _iniFileHandler.IniStructure.Count : firstSectionIndex)
                .Any(e => e is SettingEntry || e is CommentEntry);

            if (defaultSettingsExist)
            {
                currentSectionNode = EnsureDefaultSectionNode();
            }

            foreach (var entry in _iniFileHandler.IniStructure)
            {
                if (entry is SectionEntry sectionEntry)
                {
                    // If searching, only add the section header if its name matches, 
                    // OR we can let it dynamically appear if its children match (handled below)
                    bool sectionMatches = sectionEntry.SectionName.ToLower().Contains(query);

                    // We create the node, but we might remove it later if it ends up empty during a search
                    currentSectionNode = treeViewConfigOptions.Nodes.Add(sectionEntry.SectionName, sectionEntry.SectionName);
                    currentSectionNode.Tag = sectionEntry;

                    // Tag along whether the section itself was a match
                    currentSectionNode.ImageKey = sectionMatches ? "match" : "";
                    continue;
                }

                // We need a valid section node to attach children to.
                // This handles settings that fall under the implicit [Default] section.
                if (currentSectionNode == null)
                {
                    continue; // Should not happen with the new default handling, but good for safety.
                }

                if (entry is SettingEntry settingEntry)
                {
                    bool keyMatch = settingEntry.Key.ToLower().Contains(query);
                    bool valueMatch = settingEntry.Value.ToLower().Contains(query);

                    // If searching, skip this setting if neither key nor value matches, 
                    // UNLESS the parent section name itself was a match
                    if (isSearching && !keyMatch && !valueMatch && currentSectionNode.ImageKey != "match")
                        continue;

                    string nodeText = settingEntry.IsCommentedOut ? $"; {settingEntry.Key}" : settingEntry.Key;
                    TreeNode settingNode = currentSectionNode.Nodes.Add(settingEntry.Key, nodeText);
                    settingNode.Tag = settingEntry;
                }
                else if (entry is CommentEntry commentEntry)
                {
                    bool commentMatch = commentEntry.RawLine.ToLower().Contains(query);

                    if (isSearching && !commentMatch && currentSectionNode.ImageKey != "match")
                        continue;

                    TreeNode commentNode = currentSectionNode.Nodes.Add(commentEntry.RawLine);
                    commentNode.Tag = commentEntry;
                }
            }

            // Clean up phase: If searching, remove any section nodes that don't have any matching children
            if (isSearching)
            {
                CleanEmptySectionsAndSelectFirst();
            }
        }

        private void CleanEmptySectionsAndSelectFirst()
        {
            // Loop backwards through top-level nodes so removing elements doesn't break indices
            for (int i = treeViewConfigOptions.Nodes.Count - 1; i >= 0; i--)
            {
                TreeNode sectionNode = treeViewConfigOptions.Nodes[i];

                // If the section header didn't match the query AND it has no matching children, vanish it
                if (sectionNode.ImageKey != "match" && sectionNode.Nodes.Count == 0)
                {
                    sectionNode.Remove();
                }
                else
                {
                    // If it survived, expand it so the user sees the results inside
                    sectionNode.Expand();
                }
            }

            // Automatically select the first available result
            if (treeViewConfigOptions.Nodes.Count == 0) return;

            TreeNode firstNode = treeViewConfigOptions.Nodes[0];

            // If the section has children, select the first actual setting/comment instead of the header
            if (firstNode.Nodes.Count > 0)
            {
                treeViewConfigOptions.SelectedNode = firstNode.Nodes[0];
                return;
            }

            treeViewConfigOptions.SelectedNode = firstNode;
        }

        private void textFilter_TextChanged(object sender, EventArgs e)
        {
            if (searchDebounceTimer == null) return;

            // Stop the timer if it's currently counting down from a previous keystroke
            searchDebounceTimer.Stop();

            // Start a fresh 300ms countdown
            searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            if (searchDebounceTimer == null || textFilter == null) return;
            searchDebounceTimer.Stop();

            string query = textFilter.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                UpdateTreeView();
                return;
            }

            await ExecuteSearchAsync(query, "");
        }

        private async Task ExecuteSearchAsync(string query, string status)
        {
            // 1. Cancel any previous background search running
            searchCancellationTokenSource?.Cancel();
            searchCancellationTokenSource?.Dispose();
            searchCancellationTokenSource = new CancellationTokenSource();
            var token = searchCancellationTokenSource.Token;

            isSearchRunning = true;
            UpdateStatus((status ?? "") + "Searching...");

            try
            {
                // 2. Offload the heavy matching loop to a background thread
                List<IniEntry> filteredResults = await Task.Run(() => PerformBackgroundFilter(query, token), token);

                // 3. Update the UI thread instantly using BeginUpdate/EndUpdate
                PopulateTreeWithFilteredData(filteredResults, !string.IsNullOrEmpty(query));
                UpdateStatus((status ?? "") + (string.IsNullOrEmpty(query) ? "Ready." : $"Found results matching '{query}'."));
            }
            catch (OperationCanceledException)
            {
                // Silently swallow if a newer keystroke or action takes over
            }
            finally
            {
                isSearchRunning = false;
            }
        }

        private List<IniEntry> PerformBackgroundFilter(string query, CancellationToken token)
        {
            List<IniEntry> filteredList = new List<IniEntry>();
            if (string.IsNullOrEmpty(query))
            {
                return new List<IniEntry>(_iniFileHandler.IniStructure);
            }

            SectionEntry? activeSection = null;
            List<IniEntry> temporarySectionItems = new List<IniEntry>();
            bool includeEntireSection = false;

            // Separately collect items that belong to the implicit [Default] section
            // (entries before the first explicit [Section] header)
            bool passedFirstSection = false;
            List<IniEntry> defaultSectionItems = new List<IniEntry>();

            foreach (var entry in _iniFileHandler.IniStructure)
            {
                token.ThrowIfCancellationRequested();

                if (entry is SectionEntry sectionEntry)
                {
                    passedFirstSection = true;

                    // First, flush the previous section if it qualified for the results
                    if (activeSection != null && (includeEntireSection || temporarySectionItems.Count > 0))
                    {
                        filteredList.Add(activeSection);
                        filteredList.AddRange(temporarySectionItems);
                    }

                    // Set up the context for the new section header
                    activeSection = sectionEntry;
                    temporarySectionItems.Clear();
                    
                    // Core Fix: Check if the section name itself matches the search query
                    includeEntireSection = sectionEntry.SectionName.ToLower().Contains(query);
                    continue;
                }

                // Collect default section entries separately before the first explicit section
                if (!passedFirstSection)
                {
                    if (entry is SettingEntry defaultSetting)
                    {
                        if (defaultSetting.Key.ToLower().Contains(query) || defaultSetting.Value.ToLower().Contains(query))
                            defaultSectionItems.Add(defaultSetting);
                    }
                    else if (entry is CommentEntry defaultComment)
                    {
                        if (defaultComment.RawLine.ToLower().Contains(query))
                            defaultSectionItems.Add(defaultComment);
                    }
                    continue;
                }

                // If the section name was a match, pull in absolutely everything inside it
                if (includeEntireSection)
                {
                    temporarySectionItems.Add(entry);
                    continue;
                }

                // Otherwise, fallback to checking individual settings/comments
                if (entry is SettingEntry settingEntry)
                {
                    if (settingEntry.Key.ToLower().Contains(query) || settingEntry.Value.ToLower().Contains(query))
                    {
                        temporarySectionItems.Add(settingEntry);
                    }
                }
                else if (entry is CommentEntry commentEntry)
                {
                    if (commentEntry.RawLine.ToLower().Contains(query))
                    {
                        temporarySectionItems.Add(commentEntry);
                    }
                }
            }

            // Flush the final remaining section block out of the loop safely
            if (activeSection != null && (includeEntireSection || temporarySectionItems.Count > 0))
            {
                filteredList.Add(activeSection);
                filteredList.AddRange(temporarySectionItems);
            }

            // Prepend any matching default-section items at the start of the results.
            // PopulateTreeWithFilteredData will create the [Default] node for them automatically.
            if (defaultSectionItems.Count > 0)
                filteredList.InsertRange(0, defaultSectionItems);

            return filteredList;
        }

        private void PopulateTreeWithFilteredData(List<IniEntry> filteredItems, bool isSearching)
        {
            // Crucial Performance Boost: Freeze the TreeView control drawing engine
            treeViewConfigOptions.BeginUpdate();
            treeViewConfigOptions.Nodes.Clear();

            TreeNode? currentSectionNode = null;

            // Special Handling for the [Default] section
            // Check if there are any settings/comments before the first explicit section in the filtered items
            var firstSectionIndex = filteredItems.FindIndex(e => e is SectionEntry);
            var defaultSettingsExist = filteredItems
                .Take(firstSectionIndex == -1 ? filteredItems.Count : firstSectionIndex)
                .Any(e => e is SettingEntry || e is CommentEntry);

            if (defaultSettingsExist)
            {
                currentSectionNode = EnsureDefaultSectionNode();
            }

            foreach (var entry in filteredItems)
            {
                if (entry is SectionEntry sectionEntry)
                {
                    currentSectionNode = treeViewConfigOptions.Nodes.Add(sectionEntry.SectionName, sectionEntry.SectionName);
                    currentSectionNode.Tag = sectionEntry;
                    
                    if (isSearching) currentSectionNode.Expand(); // Instantly show matching items
                }
                else if (currentSectionNode != null)
                {
                    if (entry is SettingEntry settingEntry)
                    {
                        string nodeText = settingEntry.IsCommentedOut ? $"; {settingEntry.Key}" : settingEntry.Key;
                        TreeNode settingNode = currentSectionNode.Nodes.Add(settingEntry.Key, nodeText);
                        settingNode.Tag = settingEntry;
                    }
                    else if (entry is CommentEntry commentEntry)
                    {
                        TreeNode commentNode = currentSectionNode.Nodes.Add(commentEntry.RawLine);
                        commentNode.Tag = commentEntry;
                    }
                }
            }

            // Automatically highlight the first result
            if (treeViewConfigOptions.Nodes.Count > 0)
            {
                TreeNode firstNode = treeViewConfigOptions.Nodes[0];
                treeViewConfigOptions.SelectedNode = firstNode.Nodes.Count > 0 ? firstNode.Nodes[0] : firstNode;
            }

            // Unfreeze the UI engine and paint the changes all at once to the screen
            treeViewConfigOptions.EndUpdate();
        }

        private void buttonClearFilter_Click(object sender, EventArgs e)
        {
            if (textFilter == null) return;
            textFilter.Text = ""; // Clearing the text will automatically fire TextChanged and reset the tree!
            textFilter.Focus();
        }

        private void buttonAddSection_Click(object? sender, EventArgs e)
        {
            // 1. Prompt the user for the new section name using a dialog
            string newSectionName = PromptForSectionName();

            // Early return if the user cancelled or entered nothing
            if (string.IsNullOrWhiteSpace(newSectionName)) return;

            // Clean up brackets if the user typed them manually (e.g., "[MySection]" -> "MySection")
            newSectionName = newSectionName.Trim('[', ']');

            // 2. Prevent duplicate sections
            if (_iniFileHandler.Sections.ContainsKey(newSectionName))
            {
                MessageBox.Show($"A section named [{newSectionName}] already exists.", "Duplicate Section", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Create the new section entry data model
            var newSectionEntry = new SectionEntry
            {
                SectionName = newSectionName,
                RawLine = $"[{newSectionName}]"
            };

            // 4. Update the single source of truth (Append it to the end of the file structure)
            _iniFileHandler.IniStructure.Add(newSectionEntry);

            // 5. Update the fast-lookup dictionary and refresh the visual tree
            _iniFileHandler.RebuildSectionsDictionary();
            UpdateTreeView();

            // 6. Find and automatically select the newly created section in the UI
            SelectSectionNode(newSectionName);

            // 7. Mark the file as modified
            MarkChanged();
        }

        private string PromptForSectionName()
        {
            // Creates a lightweight, dynamic popup dialog form on the fly
            Form prompt = new Form()
            {
                Width = 350,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Add New Section",
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 300, Text = "Enter section name:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 300 };
            Button confirmation = new Button() { Text = "OK", Left = 115, Width = 100, Top = 80, Height = 30, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 220, Width = 100, Top = 80, Height = 30, DialogResult = DialogResult.Cancel };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            // Return the text if they clicked OK, otherwise return string.Empty
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : string.Empty;
        }

        private void SelectSectionNode(string sectionName)
        {
            // Search the tree view for the exact key/text matching the section name
            foreach (TreeNode node in treeViewConfigOptions.Nodes)
            {
                if (node.Text != sectionName) continue;

                treeViewConfigOptions.SelectedNode = node;
                node.EnsureVisible();
                return;
            }
        }

    }
}