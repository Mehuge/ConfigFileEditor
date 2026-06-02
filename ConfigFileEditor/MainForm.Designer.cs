namespace ConfigFileEditor
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newFileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            copyToolStripMenuItem = new ToolStripMenuItem();
            pasteToolStripMenuItem = new ToolStripMenuItem();
            selectAllToolStripMenuItem = new ToolStripMenuItem();
            treeViewConfigOptions = new TreeView();
            valueLabel = new Label();
            value = new TextBox();
            buttonAddSetting = new Button();
            buttonRemoveSetting = new Button();
            openINIFileDialog = new OpenFileDialog();
            saveINIFileDialog = new SaveFileDialog();
            sectionLabel = new Label();
            keyLabel = new Label();
            commentCheckBox = new CheckBox();
            sectionName = new TextBox();
            keyName = new TextBox();
            statusBarStrip = new StatusStrip();
            toolStripStatusBarLabel = new ToolStripStatusLabel();
            textFilter = new TextBox();
            buttonClearFilter = new Button();
            buttonAddSection = new Button();
            menuStrip1.SuspendLayout();
            statusBarStrip.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 3, 0, 3);
            menuStrip1.Size = new Size(914, 30);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newFileToolStripMenuItem, openToolStripMenuItem, saveToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(46, 24);
            fileToolStripMenuItem.Text = "File";
            // 
            // newFileToolStripMenuItem
            // 
            newFileToolStripMenuItem.Name = "newFileToolStripMenuItem";
            newFileToolStripMenuItem.Size = new Size(149, 26);
            newFileToolStripMenuItem.Text = "New File";
            newFileToolStripMenuItem.Click += newFileToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Size = new Size(149, 26);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(149, 26);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(149, 26);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { copyToolStripMenuItem, pasteToolStripMenuItem, selectAllToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(49, 24);
            editToolStripMenuItem.Text = "Edit";
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new Size(154, 26);
            copyToolStripMenuItem.Text = "Copy";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            // 
            // pasteToolStripMenuItem
            // 
            pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            pasteToolStripMenuItem.Size = new Size(154, 26);
            pasteToolStripMenuItem.Text = "Paste";
            pasteToolStripMenuItem.Click += pasteToolStripMenuItem_Click;
            // 
            // selectAllToolStripMenuItem
            // 
            selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            selectAllToolStripMenuItem.Size = new Size(154, 26);
            selectAllToolStripMenuItem.Text = "Select All";
            selectAllToolStripMenuItem.Click += selectAllToolStripMenuItem_Click;
            // 
            // treeViewConfigOptions
            // 
            treeViewConfigOptions.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            treeViewConfigOptions.Location = new Point(14, 70);
            treeViewConfigOptions.Margin = new Padding(3, 4, 3, 4);
            treeViewConfigOptions.Name = "treeViewConfigOptions";
            treeViewConfigOptions.Size = new Size(285, 504);
            treeViewConfigOptions.TabIndex = 1;
            treeViewConfigOptions.AfterSelect += treeView1_AfterSelect;
            // 
            // valueLabel
            // 
            valueLabel.AutoSize = true;
            valueLabel.Location = new Point(318, 122);
            valueLabel.Name = "valueLabel";
            valueLabel.Size = new Size(48, 20);
            valueLabel.TabIndex = 2;
            valueLabel.Text = "Value:";
            // 
            // value
            // 
            value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            value.Location = new Point(382, 119);
            value.Margin = new Padding(3, 4, 3, 4);
            value.Name = "value";
            value.Size = new Size(520, 27);
            value.TabIndex = 3;
            value.TextChanged += value_TextChanged;
            // 
            // buttonAddSetting
            // 
            buttonAddSetting.Location = new Point(318, 200);
            buttonAddSetting.Margin = new Padding(3, 4, 3, 4);
            buttonAddSetting.Name = "buttonAddSetting";
            buttonAddSetting.Size = new Size(137, 31);
            buttonAddSetting.TabIndex = 4;
            buttonAddSetting.Text = "Add Setting";
            buttonAddSetting.UseVisualStyleBackColor = true;
            buttonAddSetting.Click += buttonAddSetting_Click;
            // 
            // buttonRemoveSetting
            // 
            buttonRemoveSetting.Location = new Point(462, 200);
            buttonRemoveSetting.Margin = new Padding(3, 4, 3, 4);
            buttonRemoveSetting.Name = "buttonRemoveSetting";
            buttonRemoveSetting.Size = new Size(137, 31);
            buttonRemoveSetting.TabIndex = 5;
            buttonRemoveSetting.Text = "Remove Setting";
            buttonRemoveSetting.UseVisualStyleBackColor = true;
            buttonRemoveSetting.Click += buttonRemove_Click;
            // 
            // openINIFileDialog
            // 
            openINIFileDialog.Filter = "INI files (*.conf)|*.conf|All files (*.*)|*.*";
            // 
            // saveINIFileDialog
            // 
            saveINIFileDialog.Filter = "INI files (*.conf)|*.conf|All files (*.*)|*.*";
            // 
            // sectionLabel
            // 
            sectionLabel.AutoSize = true;
            sectionLabel.Location = new Point(318, 46);
            sectionLabel.Name = "sectionLabel";
            sectionLabel.Size = new Size(61, 20);
            sectionLabel.TabIndex = 6;
            sectionLabel.Text = "Section:";
            // 
            // keyLabel
            // 
            keyLabel.AutoSize = true;
            keyLabel.Location = new Point(318, 84);
            keyLabel.Name = "keyLabel";
            keyLabel.Size = new Size(36, 20);
            keyLabel.TabIndex = 7;
            keyLabel.Text = "Key:";
            // 
            // commentCheckBox
            // 
            commentCheckBox.AutoSize = true;
            commentCheckBox.Location = new Point(382, 154);
            commentCheckBox.Margin = new Padding(3, 4, 3, 4);
            commentCheckBox.Name = "commentCheckBox";
            commentCheckBox.Size = new Size(113, 24);
            commentCheckBox.TabIndex = 8;
            commentCheckBox.Text = "Commented";
            commentCheckBox.UseVisualStyleBackColor = true;
            commentCheckBox.CheckedChanged += commentCheckBox_CheckedChanged;
            // 
            // sectionName
            // 
            sectionName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sectionName.BackColor = SystemColors.Control;
            sectionName.BorderStyle = BorderStyle.None;
            sectionName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            sectionName.Location = new Point(382, 46);
            sectionName.Name = "sectionName";
            sectionName.Size = new Size(520, 20);
            sectionName.TabIndex = 9;
            // 
            // keyName
            // 
            keyName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            keyName.BackColor = SystemColors.Control;
            keyName.BorderStyle = BorderStyle.None;
            keyName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            keyName.Location = new Point(382, 84);
            keyName.Name = "keyName";
            keyName.Size = new Size(520, 20);
            keyName.TabIndex = 10;
            // 
            // statusBarStrip
            // 
            statusBarStrip.ImageScalingSize = new Size(20, 20);
            statusBarStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusBarLabel });
            statusBarStrip.Location = new Point(0, 576);
            statusBarStrip.Name = "statusBarStrip";
            statusBarStrip.Size = new Size(914, 24);
            statusBarStrip.TabIndex = 11;
            statusBarStrip.Text = "statusStrip1";
            // 
            // toolStripStatusBarLabel
            // 
            toolStripStatusBarLabel.Name = "toolStripStatusBarLabel";
            toolStripStatusBarLabel.Size = new Size(0, 18);
            // 
            // textFilter
            // 
            textFilter.Location = new Point(14, 38);
            textFilter.Name = "textFilter";
            textFilter.PlaceholderText = "Search...";
            textFilter.Size = new Size(285, 27);
            textFilter.TabIndex = 12;
            textFilter.TextChanged += textFilter_TextChanged;
            // 
            // buttonClearFilter
            // 
            buttonClearFilter.BackColor = Color.White;
            buttonClearFilter.FlatAppearance.BorderSize = 0;
            buttonClearFilter.FlatStyle = FlatStyle.Flat;
            buttonClearFilter.Font = new Font("Segoe UI", 7.8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            buttonClearFilter.Location = new Point(280, 39);
            buttonClearFilter.Name = "buttonClearFilter";
            buttonClearFilter.Size = new Size(19, 24);
            buttonClearFilter.TabIndex = 13;
            buttonClearFilter.Text = "X";
            buttonClearFilter.UseVisualStyleBackColor = false;
            buttonClearFilter.Click += buttonClearFilter_Click;
            // 
            // buttonAddSection
            // 
            buttonAddSection.Location = new Point(765, 41);
            buttonAddSection.Margin = new Padding(3, 4, 3, 4);
            buttonAddSection.Name = "buttonAddSection";
            buttonAddSection.Size = new Size(137, 31);
            buttonAddSection.TabIndex = 14;
            buttonAddSection.Text = "Add Section";
            buttonAddSection.UseVisualStyleBackColor = true;
            buttonAddSection.Click += buttonAddSection_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 600);
            Controls.Add(buttonAddSection);
            Controls.Add(buttonClearFilter);
            Controls.Add(textFilter);
            Controls.Add(statusBarStrip);
            Controls.Add(keyName);
            Controls.Add(sectionName);
            Controls.Add(commentCheckBox);
            Controls.Add(keyLabel);
            Controls.Add(sectionLabel);
            Controls.Add(buttonRemoveSetting);
            Controls.Add(buttonAddSetting);
            Controls.Add(value);
            Controls.Add(valueLabel);
            Controls.Add(treeViewConfigOptions);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 4, 3, 4);
            Name = "MainForm";
            Text = "Config File Editor";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            statusBarStrip.ResumeLayout(false);
            statusBarStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
        private System.Windows.Forms.TreeView treeViewConfigOptions;
        private System.Windows.Forms.Label valueLabel;
        private System.Windows.Forms.TextBox value;
        private System.Windows.Forms.Button buttonAddSetting;
        private System.Windows.Forms.Button buttonRemoveSetting;
        private System.Windows.Forms.OpenFileDialog openINIFileDialog;
        private System.Windows.Forms.SaveFileDialog saveINIFileDialog;
        private System.Windows.Forms.Label sectionLabel;
        private System.Windows.Forms.Label keyLabel;
        private System.Windows.Forms.CheckBox commentCheckBox;
        private TextBox sectionName;
        private TextBox keyName;
        private StatusStrip statusBarStrip;
        private ToolStripStatusLabel toolStripStatusBarLabel;
        private TextBox textFilter;
        private Button buttonClearFilter;
        private Button buttonAddSection;
        private ToolStripMenuItem newFileToolStripMenuItem;
    }
}