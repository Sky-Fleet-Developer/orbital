using System.Globalization;
using UnityWinForms.System.Drawing;

namespace UnityWinForms.Core.API
{

    public interface IApiSystem
    {
        CultureInfo CurrentCulture { get; }
        Point MousePosition { get; }
    }
}
