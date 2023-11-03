using UnityWinForms.System.Drawing;

namespace UnityWinForms.System.Windows.Forms
{

    public class PaintEventArgs
    {
        public Rectangle ClipRectangle { get; set; }
        public Graphics Graphics { get; set;  }
    }
}
