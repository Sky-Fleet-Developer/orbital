using System;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    /// <summary>
    /// The own craft online (stage 2c.5), engine-free: the kernel step, the thrust recorder and the attitude policy feeding the session's
    /// uplink, the state history, and the reconciliation of server states (ignore, soft, hard; fuel always from the server). The Unity side
    /// (2c.6) owns the input, the clock and the view; a headless pilot in the tests drives it the same way.
    /// </summary>
    public sealed class OwnCraftLink
    {
        private readonly NetSession _session;
        private RecorderState _rec;
        private AttitudePolicyState _policy;

        public FlightState State;
        public FlightParams Params;

        /// <summary>
        /// Stage 3.2: the world's bodies. With a table the craft changes its body by the sphere of influence rule after each step (gravity and
        /// ground from the table), the history keeps the body of each entry and a server state around another body is compared in the
        /// client's frame. Null = one body (<see cref="Params"/> as the caller set them), as before stage 3.
        /// </summary>
        public BodyFrameTable Frames
        {
            get => _frames;
            set
            {
                _frames = value;
                History.Frames = value;
            }
        }
        private BodyFrameTable _frames;

        /// <summary>Index of the body the craft is around in <see cref="Frames"/> (-1 without a table).</summary>
        public int Parent { get; private set; } = -1;
        /// <summary>Id of that body (null without a table).</summary>
        public string ParentId => _frames != null && Parent >= 0 ? _frames.IdOf(Parent) : null;
        /// <summary>Sphere of influence changes made by the rule on the client (not by corrections).</summary>
        public int ParentChanges { get; private set; }
        public double Time { get; private set; } = double.NaN;
        /// <summary>Cumulative RCS propellant burned by the client (the uplink's baseline counter).</summary>
        public double RcsUsedTotal { get; private set; }
        public readonly StateHistory History = new StateHistory();
        public ViewSmoothing View;
        public RecorderConfig RecorderCfg = RecorderConfig.Online();
        public ReconcileConfig ReconcileCfg = ReconcileConfig.Default();
        public AttitudePolicyConfig PolicyCfg = AttitudePolicyConfig.Default();
        public double HardStep = 0.02;

        public int Soft { get; private set; }
        public int Hard { get; private set; }
        public int Ignored { get; private set; }
        public int Stale { get; private set; }
        public int SegmentsSent { get; private set; }
        public int AttitudesSent { get; private set; }
        public ReconcileResult LastReconcile { get; private set; }
        public string LastHardReason { get; private set; }

        public OwnCraftLink(NetSession session)
        {
            _session = session;
        }

        public bool HasCraft => !double.IsNaN(Time);

        /// <summary>Take a server state as is (spawn, hello, hard correction): history, recorder and send queue start again.</summary>
        public void TakeServerState(CraftStateDto s, double nowTime, string reason)
        {
            if (s == null) return;
            double throttle = HasCraft ? State.Throttle : 0.0;
            State = Reconciler.ApplyHard(s, ref Params, _frames, out int parent, Math.Max(nowTime, s.Time), HardStep, throttle);
            Parent = parent;
            Time = Math.Max(nowTime, s.Time);
            History.Clear();
            History.Push(Time, State, Parent);
            ThrustSegmentRecorder.Clear(ref _rec);
            // the recorder needs the sample before the first step: a burn from this very moment starts here
            ThrustSegmentRecorder.Update(ref _rec, SampleOf(State, Time), AttitudeSample.Of(State, Time, RcsUsedTotal), RecorderCfg);
            _session.Uplink.Clear();
            _session.ResetRcsBaseline(RcsUsedTotal);
            View.Offset = Vec3.Zero;
            LastHardReason = reason;
        }

        /// <summary>One kernel step from <see cref="Time"/>; <paramref name="realNow"/> paces the attitude reports.</summary>
        public StepReport Step(in ControlInput input, double dt, double realNow)
        {
            FlightKernel.Step(ref State, input, Params, Time, dt, out StepReport rep);
            Time += dt;
            int parent = Parent;
            bool newBody = SoiRule.Step(_frames, ref parent, ref State, ref Params, Time);
            Parent = parent;
            if (newBody) ParentChanges++;
            RcsUsedTotal += rep.RcsFuelUsed;
            History.Push(Time, State, Parent);
            View.Decay(dt, ReconcileCfg.ViewTau);

            RecorderSample sample = SampleOf(State, Time);
            AttitudeSample att = AttitudeSample.Of(State, Time, RcsUsedTotal);
            ThrustSegmentRecorder.Update(ref _rec, sample, att, RecorderCfg);
            // a segment does not cross a sphere of influence border on the client: the burn goes on in a new one from this very sample
            if (newBody) ThrustSegmentRecorder.Flush(ref _rec);
            while (ThrustSegmentRecorder.TryDequeue(ref _rec, Time, RecorderCfg, out ThrustSegment seg))
            {
                _session.Uplink.EnqueueSegment(seg);
                SegmentsSent++;
            }
            AttitudePolicyInput pi = new AttitudePolicyInput
            {
                RealTime = realNow, Flying = State.Mode == FlightMode.Flying, Burning = _rec.Active,
                PilotRotating = input.Pitch != 0 || input.Yaw != 0 || input.Roll != 0, SasOn = State.SasOn, SasPhase = State.SasPhase,
                AngularRate = Vec3.Length(State.Omega), RcsUsedTotal = RcsUsedTotal,
            };
            if (AttitudePolicy.Update(ref _policy, pi, PolicyCfg))
            {
                _session.Uplink.EnqueueAttitude(att);
                AttitudesSent++;
            }
            return rep;
        }

        /// <summary>
        /// The recorder sample after a step. Resting = on the ground with thrust not above weight: the step that lifts the craft off ends
        /// still on the ground but burned with full thrust, and it must open the segment (else its propellant is never reported).
        /// </summary>
        private RecorderSample SampleOf(in FlightState s, double time)
        {
            bool resting = s.Mode == FlightMode.Crashed;
            if (s.Mode == FlightMode.Landed)
            {
                double r2 = Vec3.Dot(s.Position, s.Position);
                double weight = r2 > 0 ? FlightKernel.TotalMass(Params.Craft, s) * Params.Nu / r2 : 0.0;
                resting = s.Throttle * Params.Craft.Engine.MaxThrust <= weight;
            }
            return new RecorderSample
            {
                Time = time, Throttle = s.Throttle, ThrustDirection = DQuat.Rotate(s.Orientation, Vec3.UnitY), Sas = s.SasOn, Resting = resting,
            };
        }

        /// <summary>The earliest recorded thrust the server has not accepted: open segment, queued or in flight (+infinity when none).</summary>
        public double PendingThrustStart =>
            Math.Min(_session.Uplink.EarliestPendingThrust, _rec.Active ? _rec.Open.StartTime : double.PositiveInfinity);

        /// <summary>
        /// RCS propellant the server's state does not include yet: burned since the last attitude record it accepted (the uplink baseline).
        /// </summary>
        public double UnreportedRcs => Math.Max(0.0, RcsUsedTotal - _session.Uplink.RcsBaseline);

        /// <summary>Close the open segment now (before a disconnect or at the end of a test) so it is sent.</summary>
        public void Flush()
        {
            ThrustSegmentRecorder.Flush(ref _rec);
            while (ThrustSegmentRecorder.TryDequeue(ref _rec, double.MaxValue, RecorderCfg, out ThrustSegment seg))
            {
                _session.Uplink.EnqueueSegment(seg);
                SegmentsSent++;
            }
        }

        /// <summary>A server state of the own craft: compare with the history and correct.</summary>
        public ReconcileAction OnServerState(CraftStateDto s, string parentId)
        {
            if (!HasCraft || s == null) return ReconcileAction.Ignore;
            ReconcileResult r = Reconciler.Compare(s, History, parentId, PendingThrustStart, ReconcileCfg, _frames);
            LastReconcile = r;
            switch (r.Action)
            {
                case ReconcileAction.Stale:
                    Stale++;
                    return r.Action;
                case ReconcileAction.Hard:
                    Hard++;
                    TakeServerState(s, Time, r.Reason);
                    return r.Action;
                case ReconcileAction.Soft:
                    Soft++;
                    Reconciler.ApplySoft(ref State, Time, History, r, ref View);
                    break;
                default:
                    Ignored++;
                    break;
            }
            double mainBefore = State.MainFuel, rcsBefore = State.RcsFuel;
            Reconciler.ApplyFuel(ref State, s, History, ReconcileCfg.MaxAhead);
            // RCS goes to the server in chunks (segments, attitude reports): its value lacks everything since the baseline, not since its time
            State.RcsFuel = Math.Max(0.0, s.RcsFuel - UnreportedRcs);
            History.ShiftFuel(s.Time, State.MainFuel - mainBefore, State.RcsFuel - rcsBefore);
            return r.Action;
        }
    }
}
