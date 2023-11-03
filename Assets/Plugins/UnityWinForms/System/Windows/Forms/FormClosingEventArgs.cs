﻿using System.ComponentModel;

namespace UnityWinForms.System.Windows.Forms
{

    public enum CloseReason
    {
        None = 0,
        WindowsShutDown = 1,
        MdiFormClosing = 2,
        UserClosing = 3,
        TaskManagerClosing = 4,
        FormOwnerClosing = 5,
        ApplicationExitCall = 6,
    }

    public class FormClosingEventArgs : CancelEventArgs
    {
        public FormClosingEventArgs(CloseReason closeReason, bool cancel)
        {
            CloseReason = closeReason;
        }

        public CloseReason CloseReason { get; private set; }
    }
}
