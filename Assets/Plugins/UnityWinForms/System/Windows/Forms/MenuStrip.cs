using System.Drawing;
namespace System.Windows.Forms
{
    
    public class MenuStrip : ToolStrip
    {
        public MenuStrip()
        {
            ShowItemToolTips = false;
        }

        protected override Padding DefaultPadding
        {
            get { return new Padding(6, 2, 0, 2); }
        }
        protected override Size DefaultSize
        {
            get { return new Size(200, 24); }
        }
    }
}
