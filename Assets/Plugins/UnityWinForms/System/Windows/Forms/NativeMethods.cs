﻿namespace UnityWinForms.System.Windows.Forms
{
    internal static class NativeMethods
    {
        public const int BDR_RAISEDINNER = 0x0004,
                         BDR_RAISEDOUTER = 0x0001,
                         BDR_SUNKENINNER = 0x0008,
                         BDR_SUNKENOUTER = 0x0002,
                         BF_LEFT = 0x0001,
                         BF_TOP = 0x0002,
                         BF_RIGHT = 0x0004,
                         BF_BOTTOM = 0x0008,
                         BF_ADJUST = 0x2000,
                         BF_FLAT = 0x4000,
                         BF_MIDDLE = 0x0800,
                         EDGE_BUMP = 0x0001 | 0x0008,
                         EDGE_ETCHED = 0x0002 | 0x0004,
                         EDGE_RAISED = 0x0001 | 0x0004,
                         EDGE_SUNKEN = 0x0002 | 0x0008;

        public const int WHEEL_DELTA = 120,
                         WM_PARENTNOTIFY = 0x0210,
                         WM_ENTERMENULOOP = 0x0211,
                         WM_EXITMENULOOP = 0x0212,
                         WM_NEXTMENU = 0x0213,
                         WM_SIZING = 0x0214,
                         WM_CAPTURECHANGED = 0x0215,
                         WM_MOVING = 0x0216,
                         WM_POWERBROADCAST = 0x0218,
                         WM_DEVICECHANGE = 0x0219,
                         WM_IME_SETCONTEXT = 0x0281,
                         WM_IME_NOTIFY = 0x0282,
                         WM_IME_CONTROL = 0x0283,
                         WM_IME_COMPOSITIONFULL = 0x0284,
                         WM_IME_SELECT = 0x0285,
                         WM_IME_CHAR = 0x0286,
                         WM_IME_KEYDOWN = 0x0290,
                         WM_IME_KEYUP = 0x0291,
                         WM_MDICREATE = 0x0220,
                         WM_MDIDESTROY = 0x0221,
                         WM_MDIACTIVATE = 0x0222,
                         WM_MDIRESTORE = 0x0223,
                         WM_MDINEXT = 0x0224,
                         WM_MDIMAXIMIZE = 0x0225,
                         WM_MDITILE = 0x0226,
                         WM_MDICASCADE = 0x0227,
                         WM_MDIICONARRANGE = 0x0228,
                         WM_MDIGETACTIVE = 0x0229,
                         WM_MDISETMENU = 0x0230,
                         WM_ENTERSIZEMOVE = 0x0231,
                         WM_EXITSIZEMOVE = 0x0232,
                         WM_DROPFILES = 0x0233,
                         WM_MDIREFRESHMENU = 0x0234,
                         WM_MOUSEHOVER = 0x02A1,
                         WM_MOUSELEAVE = 0x02A3,
                         WM_CUT = 0x0300,
                         WM_COPY = 0x0301,
                         WM_PASTE = 0x0302,
                         WM_CLEAR = 0x0303,
                         WM_UNDO = 0x0304,
                         WM_RENDERFORMAT = 0x0305,
                         WM_RENDERALLFORMATS = 0x0306,
                         WM_DESTROYCLIPBOARD = 0x0307,
                         WM_DRAWCLIPBOARD = 0x0308,
                         WM_PAINTCLIPBOARD = 0x0309,
                         WM_VSCROLLCLIPBOARD = 0x030A,
                         WM_SIZECLIPBOARD = 0x030B,
                         WM_ASKCBFORMATNAME = 0x030C,
                         WM_CHANGECBCHAIN = 0x030D,
                         WM_HSCROLLCLIPBOARD = 0x030E,
                         WM_QUERYNEWPALETTE = 0x030F,
                         WM_PALETTEISCHANGING = 0x0310,
                         WM_PALETTECHANGED = 0x0311,
                         WM_HOTKEY = 0x0312,
                         WM_PRINT = 0x0317,
                         WM_PRINTCLIENT = 0x0318,
                         WM_THEMECHANGED = 0x031A,
                         WM_HANDHELDFIRST = 0x0358,
                         WM_HANDHELDLAST = 0x035F,
                         WM_AFXFIRST = 0x0360,
                         WM_AFXLAST = 0x037F,
                         WM_PENWINFIRST = 0x0380,
                         WM_PENWINLAST = 0x038F,
                         WM_APP = 0x8000,
                         WM_USER = 0x0400,
                         WM_REFLECT = WM_USER + 0x1C00,
                         WS_OVERLAPPED = 0x00000000,
                         WS_POPUP = unchecked((int)0x80000000),
                         WS_CHILD = 0x40000000,
                         WS_MINIMIZE = 0x20000000,
                         WS_VISIBLE = 0x10000000,
                         WS_DISABLED = 0x08000000, // preventing auto-focus atm.
                         WS_CLIPSIBLINGS = 0x04000000,
                         WS_CLIPCHILDREN = 0x02000000,
                         WS_MAXIMIZE = 0x01000000,
                         WS_CAPTION = 0x00C00000,
                         WS_BORDER = 0x00800000,
                         WS_DLGFRAME = 0x00400000,
                         WS_VSCROLL = 0x00200000,
                         WS_HSCROLL = 0x00100000,
                         WS_SYSMENU = 0x00080000,
                         WS_THICKFRAME = 0x00040000,
                         WS_TABSTOP = 0x00010000,
                         WS_MINIMIZEBOX = 0x00020000,
                         WS_MAXIMIZEBOX = 0x00010000,
                         WS_EX_DLGMODALFRAME = 0x00000001,
                         WS_EX_MDICHILD = 0x00000040,
                         WS_EX_TOOLWINDOW = 0x00000080,
                         WS_EX_CLIENTEDGE = 0x00000200,
                         WS_EX_CONTEXTHELP = 0x00000400,
                         WS_EX_RIGHT = 0x00001000,
                         WS_EX_LEFT = 0x00000000,
                         WS_EX_RTLREADING = 0x00002000,
                         WS_EX_LEFTSCROLLBAR = 0x00004000,
                         WS_EX_CONTROLPARENT = 0x00010000,
                         WS_EX_STATICEDGE = 0x00020000,
                         WS_EX_APPWINDOW = 0x00040000,
                         WS_EX_LAYERED = 0x00080000,
                         WS_EX_TOPMOST = 0x00000008,
                         WS_EX_LAYOUTRTL = 0x00400000,
                         WS_EX_NOINHERITLAYOUT = 0x00100000,
                         WPF_SETMINPOSITION = 0x0001,
                         WM_CHOOSEFONT_GETLOGFONT = 0x0400 + 1;

        public const int SB_HORZ = 0,
                         SB_VERT = 1;
    }
}
