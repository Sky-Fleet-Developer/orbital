#if UNITY_STANDALONE_WIN
#define KEYBOARD_LAYOUT_SUPPORTED
#endif
using System;
using System.Globalization;

namespace UnityWinForms.System.Windows.Forms
{

    public sealed class InputLanguage
    {
        private readonly IntPtr handle;

        internal InputLanguage(IntPtr handle)
        {
            this.handle = handle;
        }

        public CultureInfo Culture
        {
            get { return new CultureInfo((int) handle & 0xFFFF); }
        }

        public static InputLanguage CurrentInputLanguage
        {
            get
            {
#if KEYBOARD_LAYOUT_SUPPORTED
                return new InputLanguage(SafeNativeMethods.GetKeyboardLayout(0));
#else
                throw new NotSupportedException("keyboard layout");
#endif
            }
        }
        public IntPtr Handle
        {
            get { return handle; }
        }

        public override int GetHashCode()
        {
            return unchecked((int) (long) handle);
        }
    }
}