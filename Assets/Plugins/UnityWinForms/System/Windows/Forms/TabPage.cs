using UnityWinForms.System.Drawing;

namespace UnityWinForms.System.Windows.Forms
{

    public class TabPage : Panel
    {
        public TabPage()
        {
            BackColor = Color.White;
        }
        public TabPage(string text) : this()
        {
            Text = text;
        }

        public int ImageIndex { get; set; }
        public string ImageKey { get; set; }
    }
}
