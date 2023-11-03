using System.Windows.Forms;
using Button = UnityWinForms.System.Windows.Forms.Button;
using DialogResult = UnityWinForms.System.Windows.Forms.DialogResult;
using FontDialog = UnityWinForms.System.Windows.Forms.FontDialog;
using Label = UnityWinForms.System.Windows.Forms.Label;

namespace UnityWinForms.Examples.Panels
{
    
    public class PanelFontDialog : BaseExamplePanel
    {
        public override void Initialize()
        {
            var label = this.Create<Label>("Selected font:");
            var button = this.Create<Button>("Show");
            button.Click += (sender, args) =>
            {
                var fd = new FontDialog();
                fd.Font = label.Font;
                fd.FormClosed += (o, eventArgs) =>
                {
                    if (fd.DialogResult == DialogResult.OK)
                    {
                        label.Font = fd.Font;
                        label.Text = "Selected font: " + fd.Font;
                    }
                };
                fd.ShowDialog();
            };
        }
    }
}