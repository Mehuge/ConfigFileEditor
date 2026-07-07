using System.Windows.Forms;

namespace ConfigFileEditor
{
    public class SingleInputDialog : Form
    {
        public string InputValue { get; private set; } = string.Empty;

        private readonly Label _promptLabel;
        private readonly TextBox _inputTextBox;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public SingleInputDialog(string title, string prompt)
        {
            _promptLabel = new Label();
            _inputTextBox = new TextBox();
            _okButton = new Button();
            _cancelButton = new Button();

            SuspendLayout();

            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Text = title;
            ClientSize = new System.Drawing.Size(385, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            _promptLabel.Text = prompt;
            _promptLabel.Left = 20;
            _promptLabel.Top = 20;
            _promptLabel.Width = 300;
            _promptLabel.AutoSize = true;

            _inputTextBox.Left = 20;
            _inputTextBox.Top = 45;
            _inputTextBox.Width = 345;

            _okButton.Text = "OK";
            _okButton.Left = 175;
            _okButton.Top = 108;
            _okButton.Width = 95;
            _okButton.Height = 25;
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Click += (s, e) => { InputValue = _inputTextBox.Text; Close(); };

            _cancelButton.Text = "Cancel";
            _cancelButton.Left = 275;
            _cancelButton.Top = 108;
            _cancelButton.Width = 95;
            _cancelButton.Height = 25;
            _cancelButton.DialogResult = DialogResult.Cancel;
            _cancelButton.Click += (s, e) => Close();

            Controls.Add(_promptLabel);
            Controls.Add(_inputTextBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
