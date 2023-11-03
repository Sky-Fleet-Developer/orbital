using UnityWinForms.Core.API;

namespace UnityWinForms.Unity.API
{
    public class UnityInput : IApiInput
    {
        public bool CursorVisible
        {
            get { return UnityEngine.Cursor.visible; }
            set { UnityEngine.Cursor.visible = value; }
        }
    }
}