using System.Drawing;

namespace System.Windows.Forms
{

    public class PaintEventArgs
    {
        public Rectangle ClipRectangle { get; set; }
        public Graphics Graphics { get; set;  }
    }
}
