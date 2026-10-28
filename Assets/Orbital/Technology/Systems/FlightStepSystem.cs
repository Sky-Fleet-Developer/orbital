using Leopotam.Ecs;
using Orbital.Core.Flight;
using Orbital.Technology.Components;

namespace Orbital.Technology.Systems
{
    /// <summary>
    /// Advances every craft by one step with the flight kernel (double precision, no allocations). Each craft only touches its own components,
    /// so the order of the filter does not matter. Uses <see cref="FlightKernel.Step"/> directly: the structure-of-arrays batch
    /// (<see cref="FlightBatch"/>) is for the day many crafts are stepped by a job.
    /// </summary>
    public class FlightStepSystem : IEcsRunSystem
    {
        private FlightClock _clock;
        private EcsFilter<FlightStateComponent, FlightParamsComponent, ControlInputComponent, StepReportComponent> _filter;

        public void Run()
        {
            double time = _clock.Time;
            double dt = _clock.Dt;
            foreach (int i in _filter)
            {
                ref FlightStateComponent state = ref _filter.Get1(i);
                ref FlightParamsComponent flightParams = ref _filter.Get2(i);
                ref ControlInputComponent control = ref _filter.Get3(i);
                ref StepReportComponent report = ref _filter.Get4(i);
                FlightKernel.Step(ref state.State, control.Input, flightParams.Params, time, dt, out StepReport step);
                report.Report = step;
                report.TotalMainFuelUsed += step.MainFuelUsed;
                report.TotalRcsFuelUsed += step.RcsFuelUsed;
                if (!double.IsNaN(step.TouchdownTime)) report.LastImpactSpeed = step.ImpactSpeed;
            }
        }
    }
}
