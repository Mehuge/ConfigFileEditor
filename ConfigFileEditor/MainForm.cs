using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ConfigFileEditor
{
    public partial class MainForm : Form
    {
        private string? currentFilePath = null;
        private List<IniEntry> iniStructure = new List<IniEntry>();
        private Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>();

        private System.Windows.Forms.Timer? searchDebounceTimer;
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
            if (!this.Text.EndsWith("*")) return DialogResult.None;

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
            iniStructure.Clear();
            sections.Clear();
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

            if (draggedNode.Level == 0 && targetNode.Level == 0)
            {
                // CASE A: Moving a Section. We must move the SectionEntry AND all its settings/comments block.
                MoveSectionInDataStructure(draggedNode, targetNode);
            }
            else if (draggedNode.Level == 1)
            {
                // CASE B: Moving an Option/Comment within or between sections.

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
            RebuildSectionsDictionary();

            // --- STEP 4: Mark as changed
            MarkChanged();
        }

        private void MoveSectionInDataStructure(TreeNode draggedSectionNode, TreeNode targetSectionNode)
        {
            SectionEntry? draggedSection = draggedSectionNode.Tag as SectionEntry;
            SectionEntry? targetSection = targetSectionNode.Tag as SectionEntry;

            if (draggedSection == null || targetSection == null) return;

            // 1. Gather the entire contiguous block of IniEntries belonging to the dragged section
            int startIdx = iniStructure.IndexOf(draggedSection);
            List<IniEntry> blockToMove = new List<IniEntry>();

            // Read forward from the section header until we hit the next section header
            for (int i = startIdx; i < iniStructure.Count; i++)
            {
                if (i > startIdx && iniStructure[i] is SectionEntry)
                    break;
                blockToMove.Add(iniStructure[i]);
            }

            // 2. Remove that block from the data structure
            foreach (var entry in blockToMove)
            {
                iniStructure.Remove(entry);
            }

            // 3. Find the new target index to insert the block
            int targetIdx = iniStructure.IndexOf(targetSection);

            // Insert the whole block back in chunk-by-chunk
            for (int i = 0; i < blockToMove.Count; i++)
            {
                iniStructure.Insert(targetIdx + i, blockToMove[i]);
            }
        }

        private void RebuildSectionsDictionary()
        {
            sections.Clear();
            string currentSectionName = "[Default]";

            foreach (var entry in iniStructure)
            {
                if (entry is SectionEntry sectionEntry)
                {
                    currentSectionName = sectionEntry.SectionName;
                    if (!sections.ContainsKey(currentSectionName))
                    {
                        sections[currentSectionName] = new Dictionary<string, string>();
                    }
                }
                else if (entry is SettingEntry settingEntry)
                {
                    // Ensure section container exists in the dictionary
                    if (!sections.ContainsKey(currentSectionName))
                    {
                        sections[currentSectionName] = new Dictionary<string, string>();
                    }

                    // Only index active settings, or adjust rules if you want commented ones skipped
                    if (!settingEntry.IsCommentedOut)
                    {
                        sections[currentSectionName][settingEntry.Key] = settingEntry.Value;
                    }
                }
            }
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
                        if (sections.ContainsKey(sectionName) && sections[sectionName].ContainsKey(settingName))
                        {
                            sections[sectionName][settingName] = value.Text;
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
                    if (!sections.ContainsKey(sectionName))
                    {
                        sections[sectionName] = new Dictionary<string, string>();
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
                        if (!sections.ContainsKey(sectionName))
                        {
                            sections[sectionName] = new Dictionary<string, string>();
                        }
                        sections[sectionName][settingName] = settingValue;
                        var newSetting = new SettingEntry { Key = settingName, Value = settingValue };

                        // 2. Find the correct insertion index in iniStructure
                        int insertIndex = -1;

                        // Find the section entry
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
                if (sections.ContainsKey(sectionName))
                {
                    sections[sectionName].Remove(settingName);
                }
                iniStructure.Remove(settingEntry);

                // 2. Remove from UI
                treeViewConfigOptions.SelectedNode.Remove();
            }

            MarkChanged();
        }

        private void MarkChanged()
        {
            if (this.Text.EndsWith("*")) return;
            this.Text += "*";
        }

        private void ClearChanged()
        {
            this.Text = this.Text.Replace("*", "");
        }

        private async void LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            iniStructure.Clear();
            treeViewConfigOptions.Nodes.Clear();
            sections.Clear(); // Still clear it at the start

            // Clear detail panel
            sectionName.Text = "";
            keyName.Text = "";
            value.Text = "";
            value.Enabled = false;
            buttonAddSetting.Enabled = true;
            buttonRemoveSetting.Enabled = false;

            TreeNode? currentSectionNode = null;
            string currentSectionName = "[Default]";

            foreach (string line in File.ReadAllLines(path))
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);

                    var sectionEntry = new SectionEntry { SectionName = currentSectionName, RawLine = line };
                    iniStructure.Add(sectionEntry);

                    currentSectionNode = treeViewConfigOptions.Nodes.Add(currentSectionName, currentSectionName);
                    currentSectionNode.Tag = sectionEntry;
                }
                else
                {
                    bool isCommented = trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#");
                    string potentialSettingLine = isCommented ? trimmedLine.Substring(1).Trim() : trimmedLine;

                    if (potentialSettingLine.Contains("="))
                    {
                        string[] parts = potentialSettingLine.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string val = parts[1].Trim(); // renamed 'value' to 'val' to avoid collision with textBox 'value'

                        // If a setting appears before any explicit section, ensure a [Default] node exists
                        if (currentSectionNode == null)
                        {
                            var defaultSection = new SectionEntry { SectionName = "[Default]", RawLine = "" };
                            iniStructure.Add(defaultSection);
                            currentSectionNode = treeViewConfigOptions.Nodes.Add("[Default]", "[Default]");
                            currentSectionNode.Tag = defaultSection;
                        }

                        var settingEntry = new SettingEntry { Key = key, Value = val, IsCommentedOut = isCommented };
                        iniStructure.Add(settingEntry);

                        string nodeText = isCommented ? $"; {key}" : key;
                        TreeNode settingNode = currentSectionNode.Nodes.Add(key, nodeText);
                        settingNode.Tag = settingEntry;
                    }
                    else if (isCommented)
                    {
                        var commentEntry = new CommentEntry { RawLine = line };
                        iniStructure.Add(commentEntry);

                        if (currentSectionNode != null)
                        {
                            TreeNode settingNode = currentSectionNode.Nodes.Add(line);
                            settingNode.Tag = commentEntry;
                        }
                        else
                        {
                            currentSectionNode = treeViewConfigOptions.Nodes.Add(currentSectionName, line);
                            currentSectionNode.Tag = commentEntry;
                        }
                    }
                    else
                    {
                        var blankLineEntry = new BlankLineEntry { RawLine = line };
                        iniStructure.Add(blankLineEntry);
                    }
                }
            }

            // --- THE MAGIC TOUCH ---
            // Now that iniStructure is perfectly built, let the helper generate the dictionary!
            RebuildSectionsDictionary();

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

            List<string> lines = new List<string>();
            foreach (var entry in iniStructure)
            {
                if (entry is SectionEntry se)
                {
                    lines.Add(se.RawLine);
                }
                else if (entry is SettingEntry st)
                {
                    string prefix = st.IsCommentedOut ? "; " : "";
                    lines.Add($"{prefix}{st.Key}={st.Value}");
                }
                else if (entry is CommentEntry ce)
                {
                    lines.Add(ce.RawLine);
                }
                else if (entry is BlankLineEntry be)
                {
                    lines.Add(be.RawLine);
                }
            }

            File.WriteAllLines(currentFilePath, lines);
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
        private readonly string mruPath = Path.Combine(Application.StartupPath, "recent_files.json");
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

            // Optional: Reset the text after 5 seconds so it doesn't look stale
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                toolStripStatusBarLabel.Text = "Ready";
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        private void UpdateTreeView(string searchTerm = "")
        {
            treeViewConfigOptions.Nodes.Clear();

            // Normalize search term for case-insensitive matching
            string query = searchTerm.Trim().ToLower();
            bool isSearching = !string.IsNullOrEmpty(query);

            TreeNode? currentSectionNode = null;

            foreach (var entry in iniStructure)
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

                // We need a valid section node to attach children to. If none exists yet, skip.
                if (currentSectionNode == null) continue;

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
            await ExecuteSearchAsync(query, "");
        }

        private async Task ExecuteSearchAsync(string query, string status)
        {
            // 1. Cancel any previous background search running
            searchCancellationTokenSource?.Cancel();
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
                return new List<IniEntry>(iniStructure);
            }

            SectionEntry? activeSection = null;
            List<IniEntry> temporarySectionItems = new List<IniEntry>();
            bool includeEntireSection = false;

            foreach (var entry in iniStructure)
            {
                token.ThrowIfCancellationRequested();

                if (entry is SectionEntry sectionEntry)
                {
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

            return filteredList;
        }

        private void PopulateTreeWithFilteredData(List<IniEntry> filteredItems, bool isSearching)
        {
            // Crucial Performance Boost: Freeze the TreeView control drawing engine
            treeViewConfigOptions.BeginUpdate();
            treeViewConfigOptions.Nodes.Clear();

            TreeNode? currentSectionNode = null;

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
            if (sections.ContainsKey(newSectionName))
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
            iniStructure.Add(newSectionEntry);

            // 5. Update the fast-lookup dictionary and refresh the visual tree
            RebuildSectionsDictionary();
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

    // New classes for INI file structure
    public abstract class IniEntry
    {
        // No common properties here, each derived class will handle its own representation
    }

    public class SectionEntry : IniEntry
    {
        public string SectionName { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }

    public class SettingEntry : IniEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsCommentedOut { get; set; } = false;
    }

    public class CommentEntry : IniEntry
    {
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }

    public class BlankLineEntry : IniEntry
    {
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }
}