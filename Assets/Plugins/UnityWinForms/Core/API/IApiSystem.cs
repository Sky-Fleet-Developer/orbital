using System.Globalization;

namespace System.Drawing
{

    public interface IApiSystem
    {
        CultureInfo CurrentCulture { get; }
        Point MousePosition { get; }
    }
}
