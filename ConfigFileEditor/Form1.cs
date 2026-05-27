using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ConfigFileEditor
{
    public partial class Form1 : Form
    {
        private string? currentFilePath;
        private List<IniEntry> iniStructure = new List<IniEntry>();
        private Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>();

        public Form1()
        {
            InitializeComponent();
            LoadMruList();
            EnableTreeViewDragDrop();

            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!this.Text.EndsWith("*")) return;

            // Prompt to save changes
            DialogResult result = MessageBox.Show(
                "You have unsaved changes. Save them before exiting?", 
                "Unsaved Changes", 
                MessageBoxButtons.YesNoCancel, 
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Cancel)
            {
                // User clicked Cancel -> Stop the application from closing!
                e.Cancel = true; 
                return;
            }

            if (result == DialogResult.Yes)
            {
                // User wants to save. Call your save logic.
                SaveFile();
                ClearChanged();
            }
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
            if (e.Button == MouseButtons.Left && e.Item != null)
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }
        }

        private void treeView1_DragEnter(object ?sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private void treeView1_DragOver(object ?sender, DragEventArgs e)
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

        private void treeView1_DragDrop(object ?sender, DragEventArgs e)
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
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                LoadFile(openFileDialog1.FileName);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveFileDialog1.FileName;
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
            if (!this.Text.EndsWith("*")) this.Text += "*";
        }

        private void ClearChanged()
        {
            this.Text = this.Text.Replace("*", "");
        }

        private void LoadFile(string path)
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
        }

        // Placeholder methods for LoadFile and SaveFile, will be implemented later
        private void LoadFile_Orig_deleteme(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            iniStructure.Clear();
            treeViewConfigOptions.Nodes.Clear();
            sections.Clear();

            // Clear detail panel
            sectionName.Text = "";
            keyName.Text = "";
            value.Text = "";
            value.Enabled = false;
            buttonAddSetting.Enabled = true;
            buttonRemoveSetting.Enabled = false;

            TreeNode? currentSectionNode = null;
            string currentSectionName = "[Default]"; // Start with default section context

            foreach (string line in File.ReadAllLines(path))
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (!sections.ContainsKey(currentSectionName))
                    {
                        sections[currentSectionName] = new Dictionary<string, string>();
                        var sectionEntry = new SectionEntry { SectionName = currentSectionName, RawLine = line };
                        iniStructure.Add(sectionEntry);
                        currentSectionNode = treeViewConfigOptions.Nodes.Add(currentSectionName, currentSectionName);
                        currentSectionNode.Tag = sectionEntry;
                    }
                    else
                    {
                        // Find existing section node if it exists
                        TreeNode[] nodes = treeViewConfigOptions.Nodes.Find(currentSectionName, false);
                        if (nodes.Length > 0)
                        {
                            currentSectionNode = nodes[0];
                        }
                    }
                }
                else
                {
                    bool isCommented = trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#");
                    string potentialSettingLine = isCommented ? trimmedLine.Substring(1).Trim() : trimmedLine;

                    if (potentialSettingLine.Contains("="))
                    {
                        // This is a setting, commented or not
                        string[] parts = potentialSettingLine.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        // Ensure the section exists in the UI and data structures
                        if (!sections.ContainsKey(currentSectionName))
                        {
                            sections[currentSectionName] = new Dictionary<string, string>();
                            if (currentSectionName == "[Default]")
                            {
                                currentSectionNode = treeViewConfigOptions.Nodes.Add(currentSectionName, currentSectionName);
                                currentSectionNode.Tag = new SectionEntry { SectionName = currentSectionName, RawLine = "" };
                            }
                        }

                        sections[currentSectionName][key] = value;

                        var settingEntry = new SettingEntry { Key = key, Value = value, IsCommentedOut = isCommented };
                        iniStructure.Add(settingEntry);

                        if (currentSectionNode != null)
                        {
                            string nodeText = isCommented ? $"; {key}" : key;
                            TreeNode settingNode = currentSectionNode.Nodes.Add(key, nodeText);
                            settingNode.Tag = settingEntry;
                        }
                    }
                    else if (isCommented)
                    {
                        // This is a regular comment
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
                        // This is a blank line
                        var blankLineEntry = new BlankLineEntry { RawLine = line };
                        iniStructure.Add(blankLineEntry);
                    }
                }
            }

            AddToMru(path);
            UpdateStatus($"File Loaded: {path}");
            this.Text = "Editing " + path;
            currentFilePath = path;
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

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
                try {
                    string json = File.ReadAllText(mruPath);
                    // Using a simple split if you don't want to add JSON dependencies, 
                    // but here is the logic for a basic string list:
                    mruFiles = json.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
                } catch { /* Handle file error */ }
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
            toolStripStatusLabel1.Text = $"{DateTime.Now.ToShortTimeString()} - {message}";
            
            // Optional: Reset the text after 5 seconds so it doesn't look stale
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) => {
                toolStripStatusLabel1.Text = "Ready";
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
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