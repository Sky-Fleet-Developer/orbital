#if UNITY_STANDALONE_WIN
#define KEYBOARD_LAYOUT_SUPPORTED
#endif
using System;

namespace UnityWinForms.System.Windows.Forms
{
    internal static class SafeNativeMethods
    {
#if KEYBOARD_LAYOUT_SUPPORTED
        
        [global::System.Runtime.InteropServices.DllImport("user32.dll", ExactSpelling = true, CharSet = global::System.Runtime.InteropServices.CharSet.Auto)]
        [global::System.Runtime.Versioning.ResourceExposure(global::System.Runtime.Versioning.ResourceScope.None)]
        public static extern IntPtr GetKeyboardLayout(int dwLayout);
        
#endif
    }
}