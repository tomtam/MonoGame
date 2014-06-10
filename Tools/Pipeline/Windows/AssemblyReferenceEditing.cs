using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace MonoGame.Tools.Pipeline
{
    /// <summary>
    /// Modal dialog for a list of strings edited as individual lines in a textbox.
    /// </summary>
    public class StringListEditForm : Form
    {
        private readonly RichTextBox _textbox;
        private readonly Button _okButton;

        public StringListEditForm()
        {
            MinimizeBox = false;
            MaximizeBox = false;

            StartPosition = FormStartPosition.CenterParent;

            _textbox = new RichTextBox()
            {
                Dock = DockStyle.Fill,
            };
            Controls.Add(_textbox);

            _okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
                DialogResult = DialogResult.OK
            };
            Controls.Add(_okButton);
        }

        public string[] Lines
        {
            get { return _textbox.Lines; }
            set
            {
                _textbox.Lines = value;

                var size = TextRenderer.MeasureText(_textbox.Text, _textbox.Font, _textbox.Size, TextFormatFlags.TextBoxControl);
                size.Height += _okButton.Height;
                size.Height += _textbox.Font.Height;
                size.Width += 50;

                if (_textbox.Lines.Length < 3)
                {
                    size.Height += _textbox.Font.Height * (3 - _textbox.Lines.Length);
                }

                if (size.Width < 300)
                    size.Width = 300;

                ClientSize = size;
            }
        }
    }

    /// <summary>
    /// Custom editor for a list of strings edited as individual lines in a textbox.
    /// </summary>    
    public class StringListEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            var svc = provider.GetService(typeof (IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            
            var lines = (List<string>)value;
            if (lines == null)
                lines = new List<string>();

            if (svc != null && lines != null)
            {
                using (var form = new StringListEditForm())
                {
                    form.Lines = lines.ToArray();
                    if (svc.ShowDialog(form) == DialogResult.OK)
                    {
                        lines = new List<string>(form.Lines);
                    }
                }
            }

            return lines;            
        }
    }
}