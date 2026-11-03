using System;
using System.Collections.Generic;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    public struct UplinkConfig
    {
        /// <summary>Real seconds without a reply before the link asks for a resynchronisation (5).</summary>
        public double ReplyTimeout;

        public static UplinkConfig Default() => new UplinkConfig { ReplyTimeout = 5.0 };
    }

    /// <summary>One message to send: its type (maneuver or attitude), seq, and the request object.</summary>
    public struct OutgoingMessage
    {
        public string Type;
        public long Seq;
        public object Body;
    }

    public enum UplinkOutcome
    {
        /// <summary>The reply does not match the message in flight (late, foreign): nothing changed.</summary>
        Ignored,
        Accepted,
        /// <summary>The server refused: the queue is cleared and <see cref="Uplink.NeedsResync"/> is set.</summary>
        Rejected,
        /// <summary>Another session of the account has the control: the queue is cleared, nothing is sent until a new hello.</summary>
        ControlLost,
    }

    /// <summary>
    /// Sends the own craft's thrust segments and attitude reports in strict order, one at a time (stop-and-wait, stage 2c.4b). Seq is shared
    /// by both kinds, starts after the server's lastSeq (1 after a spawn). The RCS propellant of a message is the cumulative counter at its
    /// end minus the counter at the last attitude record the server accepted (the baseline), which moves only on acceptance. A request is
    /// built once, at its first send, and resent unchanged, so that a resend after a lost reply is recognised as a duplicate (an
    /// acknowledgement) and not as a conflict.
    /// </summary>
    public sealed class Uplink
    {
        private struct Item
        {
            public bool IsSegment;
            public ThrustSegment Segment;
            public AttitudeSample Attitude;
            public long Seq;
            public object Body;          // built at the first send
            public double EndRcsTotal;   // the counter at the end of the message
            public double StartTime;     // thrust start (segments) or report time
        }

        private readonly List<Item> _queue = new List<Item>();
        private readonly UplinkConfig _cfg;
        private bool _inFlight;
        private double _sentAt;

        public Uplink(UplinkConfig cfg)
        {
            _cfg = cfg;
        }

        public string CraftId { get; private set; }
        public long LastAcceptedSeq { get; private set; }
        public long NextSeq { get; private set; } = 1;
        /// <summary>Cumulative RCS counter at the last attitude record the server accepted.</summary>
        public double RcsBaseline { get; private set; }
        public double? CommittedUntil { get; private set; }
        public bool NeedsResync { get; private set; }
        public bool ControlLost { get; private set; }
        public string LastRejection { get; private set; }
        public string LastRejectionMessage { get; private set; }
        public int Pending => _queue.Count;
        public bool InFlight => _inFlight;

        /// <summary>
        /// After hello_ok, spawn_result or a resynchronisation: the craft and the server's lastSeq. A message the server already has (seq
        /// &lt;= lastSeq, its reply was lost) is dropped as accepted and moves the RCS baseline; the rest is resent in order, the head with
        /// its body unchanged. A different craft drops the whole queue. <paramref name="rcsBaseline"/> sets the baseline: the client's RCS
        /// counter now when it has just taken the server state (new craft, hard correction); null keeps it (a reconnect with the queue).
        /// </summary>
        public void Reset(string craftId, long lastSeq, double? committedUntil, double? rcsBaseline)
        {
            if (craftId != CraftId) _queue.Clear();
            CraftId = craftId;
            LastAcceptedSeq = lastSeq;
            CommittedUntil = committedUntil;
            // stop-and-wait: only the head can carry a seq (it was sent, its reply is missing)
            if (_queue.Count > 0 && _queue[0].Seq != 0 && _queue[0].Seq <= lastSeq)
            {
                RcsBaseline = _queue[0].EndRcsTotal;
                _queue.RemoveAt(0);
            }
            if (rcsBaseline.HasValue) RcsBaseline = rcsBaseline.Value;
            NextSeq = lastSeq + 1;
            if (_queue.Count > 0 && _queue[0].Seq != 0)
            {
                Item head = _queue[0];
                if (head.Seq == NextSeq && !rcsBaseline.HasValue) NextSeq++; // resent as it was: a duplicate if it did arrive after all
                else
                {
                    head.Seq = 0; // the numbering or the baseline changed: built again at the next send
                    head.Body = null;
                    _queue[0] = head;
                }
            }
            _inFlight = false;
            NeedsResync = false;
            ControlLost = false;
        }

        /// <summary>Forgets everything queued (after a hard correction the recorded past is no longer the craft's).</summary>
        public void Clear()
        {
            _queue.Clear();
            _inFlight = false;
        }

        public void EnqueueSegment(in ThrustSegment seg)
        {
            _queue.Add(new Item { IsSegment = true, Segment = seg, EndRcsTotal = seg.End.RcsUsedTotal, StartTime = seg.StartTime });
        }

        /// <summary>The queued thrust segments in order (for checks and tests; nothing is removed).</summary>
        public void CopySegments(List<ThrustSegment> into)
        {
            foreach (Item it in _queue)
                if (it.IsSegment) into.Add(it.Segment);
        }

        public void EnqueueAttitude(in AttitudeSample a)
        {
            _queue.Add(new Item { IsSegment = false, Attitude = a, EndRcsTotal = a.RcsUsedTotal, StartTime = a.Time });
        }

        /// <summary>The earliest thrust start the server has not accepted yet (queued or in flight); +infinity when none.</summary>
        public double EarliestPendingThrust
        {
            get
            {
                for (int i = 0; i < _queue.Count; i++) if (_queue[i].IsSegment) return _queue[i].StartTime;
                return double.PositiveInfinity;
            }
        }

        /// <summary>The next message to send, if nothing is in flight and the link is usable. The caller sends it at once.</summary>
        public bool TryTakeToSend(double realNow, out OutgoingMessage msg)
        {
            msg = default;
            if (_inFlight || NeedsResync || ControlLost || CraftId == null || _queue.Count == 0) return false;
            Item it = _queue[0];
            if (it.Body == null)
            {
                if (it.Seq == 0) it.Seq = NextSeq++;
                double rcs = it.EndRcsTotal - RcsBaseline;
                it.Body = it.IsSegment
                    ? (object) StateMapper.ToManeuver(it.Segment, it.Seq, CraftId, rcs)
                    : StateMapper.ToAttitudeReport(it.Attitude, it.Seq, CraftId, rcs);
                _queue[0] = it;
            }
            msg = new OutgoingMessage { Type = it.IsSegment ? MessageTypes.Maneuver : MessageTypes.Attitude, Seq = it.Seq, Body = it.Body };
            _inFlight = true;
            _sentAt = realNow;
            return true;
        }

        /// <summary>No reply within the timeout: ask for a resynchronisation (the message stays queued and is resent after it).</summary>
        public void Tick(double realNow)
        {
            if (_inFlight && realNow - _sentAt > _cfg.ReplyTimeout)
            {
                _inFlight = false;
                NeedsResync = true;
                LastRejection = "timeout";
                LastRejectionMessage = "no reply within " + _cfg.ReplyTimeout + " s";
            }
        }

        public UplinkOutcome OnManeuverResult(ManeuverResultDto r) =>
            r == null ? UplinkOutcome.Ignored : OnResult(true, r.Accepted, r.Reason, r.Message, r.Seq, r.LastSeq, r.CommittedUntil);

        public UplinkOutcome OnAttitudeResult(AttitudeResultDto r) =>
            r == null ? UplinkOutcome.Ignored : OnResult(false, r.Accepted, r.Reason, r.Message, r.Seq, r.LastSeq, r.CommittedUntil);

        private UplinkOutcome OnResult(bool segment, bool accepted, string reason, string message, long? seq, long? lastSeq, double? committed)
        {
            if (_queue.Count == 0 || !_inFlight) return UplinkOutcome.Ignored;
            Item head = _queue[0];
            if (head.IsSegment != segment) return UplinkOutcome.Ignored;
            if (reason == Reasons.ControlLost)
            {
                _queue.Clear();
                _inFlight = false;
                ControlLost = true;
                LastRejection = reason;
                LastRejectionMessage = message;
                return UplinkOutcome.ControlLost;
            }
            if (seq != null && seq.Value != head.Seq) return UplinkOutcome.Ignored;
            _inFlight = false;
            if (lastSeq.HasValue && lastSeq.Value > LastAcceptedSeq) LastAcceptedSeq = lastSeq.Value;
            if (committed.HasValue) CommittedUntil = committed;
            if (accepted || reason == Reasons.Duplicate)
            {
                // duplicate: the server accepted this very message before (the reply was lost); its propellant is counted already
                LastAcceptedSeq = Math.Max(LastAcceptedSeq, head.Seq);
                RcsBaseline = head.EndRcsTotal;
                _queue.RemoveAt(0);
                return UplinkOutcome.Accepted;
            }
            _queue.Clear();
            NeedsResync = true;
            LastRejection = reason;
            LastRejectionMessage = message;
            return UplinkOutcome.Rejected;
        }
    }

    public struct AttitudePolicyConfig
    {
        /// <summary>Real seconds between two reports at least (1).</summary>
        public double MinInterval;
        /// <summary>Real seconds between periodic reports while the craft turns or burns RCS (10).</summary>
        public double Period;
        /// <summary>rad/s: below this the craft counts as not turning (1e-3).</summary>
        public double RateThreshold;

        public static AttitudePolicyConfig Default() => new AttitudePolicyConfig { MinInterval = 1.0, Period = 10.0, RateThreshold = 1e-3 };
    }

    /// <summary>What the policy looks at after a step.</summary>
    public struct AttitudePolicyInput
    {
        public double RealTime;
        public bool Flying;
        /// <summary>A thrust segment is open (the recorder is active): the attitude goes with the segment.</summary>
        public bool Burning;
        /// <summary>The pilot holds an attitude key (pitch, yaw or roll).</summary>
        public bool PilotRotating;
        public int SasOn;
        public int SasPhase;
        public double AngularRate;
        public double RcsUsedTotal;
    }

    public struct AttitudePolicyState
    {
        public double LastReportReal;
        public bool HasReported;
        public bool PrevPilot;
        public int PrevSasOn;
        public int PrevSasPhase;
        public bool Pending;          // a trigger fired inside the minimum interval
        public double RcsAtLastReport;
    }

    /// <summary>
    /// When to send an attitude report while coasting (stage 2c.4b): in flight and outside a burn only; when the pilot releases the attitude
    /// keys, when the SAS phase changes, and every period while the craft turns or the RCS burned propellant; never more often than the
    /// minimum interval (a trigger inside it waits). The caller enqueues the report and the policy remembers it.
    /// </summary>
    public static class AttitudePolicy
    {
        public static bool Update(ref AttitudePolicyState st, in AttitudePolicyInput input, in AttitudePolicyConfig cfg)
        {
            bool released = st.PrevPilot && !input.PilotRotating;
            bool sasChanged = input.SasOn != st.PrevSasOn || (input.SasOn != 0 && input.SasPhase != st.PrevSasPhase);
            st.PrevPilot = input.PilotRotating;
            st.PrevSasOn = input.SasOn;
            st.PrevSasPhase = input.SasPhase;
            if (!input.Flying || input.Burning)
            {
                st.Pending = false; // a burn segment carries the attitude; the ground has none
                return false;
            }
            bool active = input.AngularRate > cfg.RateThreshold || input.RcsUsedTotal > st.RcsAtLastReport;
            bool periodic = active && (!st.HasReported || input.RealTime - st.LastReportReal >= cfg.Period);
            if (released || sasChanged || periodic) st.Pending = true;
            if (!st.Pending) return false;
            if (st.HasReported && input.RealTime - st.LastReportReal < cfg.MinInterval) return false;
            st.Pending = false;
            st.HasReported = true;
            st.LastReportReal = input.RealTime;
            st.RcsAtLastReport = input.RcsUsedTotal;
            return true;
        }
    }
}
