namespace Orbital.Model
{
    public abstract class System<TRuntimeVariables, TSettings> : Component
    {
        protected TRuntimeVariables MyVariables;
        protected TSettings MySettings;
        public TRuntimeVariables GetVariables() => MyVariables;
        public TSettings GetSettings() => MySettings;
        protected System(TRuntimeVariables variables, TSettings settings, Body myBody) : base(myBody)
        {
            MyVariables = variables;
            MySettings = settings;
        }
    }
}