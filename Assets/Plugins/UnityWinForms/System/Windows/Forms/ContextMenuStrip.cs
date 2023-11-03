using System;
using System.ComponentModel;

namespace UnityWinForms.System.Windows.Forms
{

    public class ContextMenuStrip : ToolStripDropDownMenu
    {
        public ContextMenuStrip()
        {
        }
        public ContextMenuStrip(IContainer container)
        {
            if (container == null)
                throw new ArgumentNullException("container");
            container.Add(this);
        }
    }
}
