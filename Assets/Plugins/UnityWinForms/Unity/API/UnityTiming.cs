using UnityWinForms.Core.API;

namespace UnityWinForms.Unity.API
{
    public class UnityTiming : IApiTiming
    {
        public float DeltaTime { get { return UnityEngine.Time.deltaTime; } }
    }
}
