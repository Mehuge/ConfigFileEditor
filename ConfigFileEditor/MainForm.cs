using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ConfigFileEditor
{
    public partial class MainForm : Form
    {
        private string? _currentFilePath = null;
        private readonly IniFileHandler _iniFileHandler = new IniFileHandler();
        private readonly MruManager _mruManager = new MruManager(
            Path.Combine(Application.StartupPath, "recent_files.txt"));

        private System.Windows.Forms.Timer? _searchDebounceTimer;
        private System.Windows.Forms.Timer? _statusResetTimer;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private bool _isSearchRunning = false;
        private ContextMenuStrip? _sectionContextMenu;

        public MainForm()
        {
            InitializeComponent();
            _mruManager.Changed += (s, e) => RefreshMruMenu();
            RefreshMruMenu();
            EnableTreeViewDragDrop();
            InitSectionContextMenu();
            NewFile();
            InitDebounceTimer();
            this.FormClosing += Form1_FormClosing;
        }

        private void InitDebounceTimer()
        {
            _searchDebounceTimer = new System.Windows.Forms.Timer();
            _searchDebounceTimer.Interval = 300;
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
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
            _currentFilePath = null;
            
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

        private void InitSectionContextMenu()
        {
            _sectionContextMenu = new ContextMenuStrip();

            var copyAsIniItem = new ToolStripMenuItem("Copy");
            copyAsIniItem.Click += (s, e) => CopySectionToClipboard(treeViewConfigOptions.SelectedNode);
            _sectionContextMenu.Items.Add(copyAsIniItem);

            _sectionContextMenu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem("Delete");
            deleteItem.Click += (s, e) =>
            {
                bool shiftHeld = (Control.ModifierKeys & Keys.Shift) != 0;
                DeleteSectionNode(treeViewConfigOptions.SelectedNode, skipConfirmation: shiftHeld);
            };
            _sectionContextMenu.Items.Add(deleteItem);

            treeViewConfigOptions.NodeMouseClick += TreeView_NodeMouseClick;
            treeViewConfigOptions.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C && treeViewConfigOptions.SelectedNode?.Tag is SectionEntry)
                    CopySectionToClipboard(treeViewConfigOptions.SelectedNode);
                if (e.KeyCode == Keys.Delete && treeViewConfigOptions.SelectedNode?.Tag is SectionEntry)
                    DeleteSectionNode(treeViewConfigOptions.SelectedNode, skipConfirmation: false);
            };
        }

        private void treeView1_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            // Prevent dragging if a search filter is currently applied
            if (!string.IsNullOrEmpty(textFilter.Text) || _isSearchRunning) return;

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

            if (targetNode == null) return;
            if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode draggedNode) return;
            if (draggedNode == targetNode) return;
            if (draggedNode.Tag is not IniEntry draggedEntry) return;
            if (targetNode.Tag is not IniEntry targetEntry) return;

            // --- STEP 1: Update the single source of truth (iniStructure) ---

            if (draggedNode.Level == 0 && targetNode.Level == 0
                && draggedEntry is SectionEntry draggedSection && targetEntry is SectionEntry targetSection)
            {
                // CASE A: Moving a Section. We must move the SectionEntry AND all its settings/comments block.
                _iniFileHandler.MoveSectionInDataStructure(draggedSection, targetSection);
            }

            if (draggedNode.Level == 1)
            {
                // CASE B: Moving a key/value entry.
                var iniStructure = _iniFileHandler.IniStructure;
                iniStructure.Remove(draggedEntry);

                int targetIndex = iniStructure.IndexOf(targetEntry);
                int insertIndex = targetNode.Level == 0 ? targetIndex + 1 : targetIndex;
                iniStructure.Insert(insertIndex, draggedEntry);
            }

            // --- STEP 2: Update the UI to match ---
            if (draggedNode.Level == 0 && targetNode.Level == 0)
            {
                draggedNode.Remove();
                treeViewConfigOptions.Nodes.Insert(targetNode.Index, draggedNode);
            }

            if (draggedNode.Level == 1)
            {
                draggedNode.Remove();

                if (targetNode.Level == 0)
                {
                    targetNode.Nodes.Insert(0, draggedNode);
                    targetNode.Expand();
                }

                if (targetNode.Level == 1)
                {
                    targetNode.Parent.Nodes.Insert(targetNode.Index, draggedNode);
                }
            }

            treeViewConfigOptions.SelectedNode = draggedNode;

            // --- STEP 3: Rebuild the fast-lookup Dictionary ---
            _iniFileHandler.RebuildSectionsDictionary();

            // --- STEP 4: Mark as changed ---
            MarkChanged();
        }

        private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.Node?.Tag is not SectionEntry) return;
            treeViewConfigOptions.SelectedNode = e.Node;
            _sectionContextMenu?.Show(treeViewConfigOptions, e.Location);
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
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                if (saveINIFileDialog.ShowDialog() == DialogResult.OK)
                    _currentFilePath = saveINIFileDialog.FileName;
                else
                    return;
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

        private void DeleteSectionNode(TreeNode? sectionNode, bool skipConfirmation = false)
        {
            if (sectionNode?.Tag is not SectionEntry sectionTag) return;

            if (!skipConfirmation)
            {
                int count = sectionNode.Nodes.Count;
                string detail = count > 0 ? $" and its {count} item(s)" : "";
                string message = $"Delete section [{sectionTag.SectionName}]{detail}?";
                if (MessageBox.Show(message, "Delete Section", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            _iniFileHandler.DeleteSection(sectionTag.SectionName);
            sectionNode.Remove();
            MarkChanged();
        }

        private void CopySectionToClipboard(TreeNode? sectionNode)
        {
            if (sectionNode?.Tag is not SectionEntry sectionTag) return;

            string sectionName = sectionTag.SectionName;
            bool isDefault = string.IsNullOrEmpty(sectionTag.RawLine);

            var sb = new StringBuilder();

            // Write the section header, preserving original formatting for real sections
            sb.AppendLine(!string.IsNullOrEmpty(sectionTag.RawLine)
                ? sectionTag.RawLine
                : $"[{sectionName}]");

            // Collect entries belonging to this section from the data structure
            bool collecting = isDefault; // Default section entries come before any explicit header

            foreach (var entry in _iniFileHandler.IniStructure)
            {
                if (entry is SectionEntry se)
                {
                    if (!isDefault && se.SectionName == sectionName)
                    {
                        collecting = true;
                        continue;
                    }
                    if (collecting) break; // Reached the next section boundary
                    continue;
                }

                if (!collecting) continue;

                if (entry is SettingEntry st)
                    sb.AppendLine($"{(st.IsCommentedOut ? "; " : "")}{st.Key}={st.Value}");
                else if (entry is CommentEntry ce)
                    sb.AppendLine(ce.RawLine);
            }

            string text = sb.ToString().TrimEnd('\r', '\n');
            if (!string.IsNullOrWhiteSpace(text))
                Clipboard.SetText(text);
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
            if (treeViewConfigOptions.SelectedNode?.Parent == null) return;
            if (treeViewConfigOptions.SelectedNode.Tag is not SettingEntry settingEntry) return;
            if (settingEntry.Value == value.Text) return;

            settingEntry.Value = value.Text;
            _iniFileHandler.UpdateSettingValue(
                treeViewConfigOptions.SelectedNode.Parent.Name,
                treeViewConfigOptions.SelectedNode.Name,
                value.Text);
            MarkChanged();
        }

        private void buttonAddSetting_Click(object sender, EventArgs e)
        {
            string sectionName;
            TreeNode? sectionNode;

            if (treeViewConfigOptions.SelectedNode == null)
            {
                sectionName = IniFileHandler.DefaultSectionName;
                sectionNode = EnsureDefaultSectionNode();
            }
            else if (treeViewConfigOptions.SelectedNode.Parent == null)
            {
                sectionNode = treeViewConfigOptions.SelectedNode;
                sectionName = sectionNode.Name;
            }
            else
            {
                sectionNode = treeViewConfigOptions.SelectedNode.Parent;
                sectionName = sectionNode.Name;
            }

            using var form = new InputForm(sectionName);
            if (form.ShowDialog() != DialogResult.OK) return;

            string? settingName = form.SettingName;
            string? settingValue = form.SettingValue;

            if (!string.IsNullOrEmpty(settingName) && settingValue != null)
            {
                SettingEntry newSetting = _iniFileHandler.AddSetting(sectionName, settingName, settingValue);
                TreeNode newSettingNode = sectionNode!.Nodes.Add(settingName, settingName);
                newSettingNode.Tag = newSetting;
                treeViewConfigOptions.SelectedNode = newSettingNode;
                sectionNode.Expand();
            }

            MarkChanged();
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
            if (treeViewConfigOptions.SelectedNode?.Parent != null &&
                treeViewConfigOptions.SelectedNode.Tag is SettingEntry settingEntry)
            {
                _iniFileHandler.RemoveSetting(
                    treeViewConfigOptions.SelectedNode.Parent.Name,
                    treeViewConfigOptions.SelectedNode.Name,
                    settingEntry);
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

            _mruManager.Add(path);
            UpdateStatus($"File Loaded: {path}");
            this.Text = "Editing " + path;
            _currentFilePath = path;

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
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveFileAs();
                return;
            }

            _iniFileHandler.CurrentFilePath = _currentFilePath;
            _iniFileHandler.SaveFile();
            UpdateStatus($"File saved: {_currentFilePath}");
            ClearChanged();
        }

        private void SaveFileAs()
        {
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Configuration Files (*.ini;*.cfg)|*.ini;*.cfg|All Files (*.*)|*.*";
            saveFileDialog.DefaultExt = "ini";
            saveFileDialog.Title = "Save Configuration File As";

            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            _currentFilePath = saveFileDialog.FileName;
            SaveFile();
            _mruManager.Add(_currentFilePath);
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

        private void RefreshMruMenu()
        {
            var fileMenu = menuStrip1.Items.Cast<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "&File" || x.Text == "File");
            if (fileMenu == null) return;

            for (int i = fileMenu.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (fileMenu.DropDownItems[i].Tag?.ToString() == "MRU")
                    fileMenu.DropDownItems.RemoveAt(i);
            }

            if (_mruManager.Files.Count == 0) return;

            fileMenu.DropDownItems.Add(new ToolStripSeparator { Tag = "MRU" });
            foreach (var path in _mruManager.Files)
            {
                var item = new ToolStripMenuItem(path) { Tag = "MRU" };
                item.Click += (s, e) => LoadFile(path);
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
            var existing = treeViewConfigOptions.Nodes[IniFileHandler.DefaultSectionName];
            if (existing != null) return existing;
            var node = treeViewConfigOptions.Nodes.Add(IniFileHandler.DefaultSectionName, IniFileHandler.DefaultSectionName);
            node.Tag = new SectionEntry { SectionName = IniFileHandler.DefaultSectionName };
            return node;
        }

        private void AddEntryToSectionNode(IniEntry entry, TreeNode sectionNode)
        {
            if (entry is SettingEntry settingEntry)
            {
                string nodeText = settingEntry.IsCommentedOut ? $"; {settingEntry.Key}" : settingEntry.Key;
                var node = sectionNode.Nodes.Add(settingEntry.Key, nodeText);
                node.Tag = settingEntry;
            }
            else if (entry is CommentEntry commentEntry)
            {
                var node = sectionNode.Nodes.Add(commentEntry.RawLine);
                node.Tag = commentEntry;
            }
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
                    if (isSearching && !keyMatch && !valueMatch && currentSectionNode.ImageKey != "match")
                        continue;
                    AddEntryToSectionNode(entry, currentSectionNode);
                }
                else if (entry is CommentEntry commentEntry)
                {
                    if (isSearching && !commentEntry.RawLine.ToLower().Contains(query) && currentSectionNode.ImageKey != "match")
                        continue;
                    AddEntryToSectionNode(entry, currentSectionNode);
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
            if (_searchDebounceTimer == null) return;
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            if (_searchDebounceTimer == null || textFilter == null) return;
            _searchDebounceTimer.Stop();

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
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var token = _searchCancellationTokenSource.Token;

            _isSearchRunning = true;
            UpdateStatus((status ?? "") + "Searching...");

            try
            {
                List<IniEntry> filteredResults = await Task.Run(() => PerformBackgroundFilter(query, token), token);
                PopulateTreeWithFilteredData(filteredResults, !string.IsNullOrEmpty(query));
                UpdateStatus((status ?? "") + (string.IsNullOrEmpty(query) ? "Ready." : $"Found results matching '{query}'."));
            }
            catch (OperationCanceledException)
            {
                // Silently swallow if a newer keystroke or action takes over
            }
            finally
            {
                _isSearchRunning = false;
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
                    AddEntryToSectionNode(entry, currentSectionNode);
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
            using var dialog = new SingleInputDialog("Add New Section", "Enter section name:");
            if (dialog.ShowDialog() != DialogResult.OK) return;

            string newSectionName = dialog.InputValue.Trim('[', ']').Trim();
            if (string.IsNullOrWhiteSpace(newSectionName)) return;

            if (_iniFileHandler.HasSection(newSectionName))
            {
                MessageBox.Show($"A section named [{newSectionName}] already exists.",
                    "Duplicate Section", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _iniFileHandler.AddSection(newSectionName);
            UpdateTreeView();
            SelectSectionNode(newSectionName);
            MarkChanged();
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