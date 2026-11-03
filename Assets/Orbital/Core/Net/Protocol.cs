using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Orbital.Core.Net
{
    // Client side of the WebSocket protocol of orbital-server (stage 2c.4). Every message is one JSON object {"type": ..., "id": ..., ...}.
    // The server writes camelCase, enums as camelCase strings and nulls; the client writes camelCase and leaves nulls out.
    // The server classes these mirror: GameServer.cs (HelloResult, SpawnResult, AttitudeReport/Result), Maneuvers.cs (ManeuverRequest/Result),
    // Craft.cs (CraftSnapshot, CraftState), Attitude.cs (SasRecord), Net.cs (message envelopes and pushes).

    /// <summary>Message type names.</summary>
    public static class MessageTypes
    {
        // client -> server
        public const string Hello = "hello";
        public const string Ping = "ping";
        public const string GetWorld = "get_world";
        public const string GetStartPoints = "get_start_points";
        public const string GetWrecks = "get_wrecks";
        public const string GetState = "get_state";
        public const string Spawn = "spawn";
        public const string Maneuver = "maneuver";
        public const string Attitude = "attitude";
        public const string AdminAuth = "admin_auth";
        public const string AdminSetTimeScale = "admin_set_time_scale";
        public const string AdminStatus = "admin_status";
        // server -> client: replies
        public const string HelloOk = "hello_ok";
        public const string HelloError = "hello_error";
        public const string Pong = "pong";
        public const string World = "world";
        public const string States = "states";
        public const string Wrecks = "wrecks";
        public const string SpawnResult = "spawn_result";
        public const string ManeuverResult = "maneuver_result";
        public const string AttitudeResult = "attitude_result";
        public const string AdminAuthResult = "admin_auth_result";
        public const string AdminResult = "admin_result";
        public const string Error = "error";
        // server -> client: pushes (no id)
        public const string CraftUpdate = "craft_update";
        public const string TimeScale = "time_scale";
        public const string ControlTaken = "control_taken";
        public const string CraftDestroyed = "craft_destroyed";
        public const string WreckCreated = "wreck_created";
        public const string ProximityUpdate = "proximity_update";
    }

    /// <summary>Rejection codes the client acts on.</summary>
    public static class Reasons
    {
        /// <summary>The seq was accepted already with the same content: an acknowledgement, although it comes with accepted = false.</summary>
        public const string Duplicate = "duplicate";
        public const string StaleSeq = "stale_seq";
        public const string SeqConflict = "seq_conflict";
        public const string ControlLost = "control_lost";
        public const string WrongCraft = "wrong_craft";
        public const string NoCraft = "no_craft";
    }

    public enum CraftModeDto { Flying, Landed, Crashed }

    /// <summary>
    /// Frame of a craft state (stage 2c.8, server CraftFrame). Surface: a point of the rotating ground at Latitude/Longitude (landed,
    /// crashed), carried to another time as that ground point; Inertial: position and velocity are carried by the flight kernel.
    /// </summary>
    public enum CraftFrameDto { Inertial, Surface }

    // ------------------------------------------------------------------ shared parts

    /// <summary>Stability assist state (server SasRecord). Null in a message = off.</summary>
    public class SasDto
    {
        public bool On;
        /// <summary>0 hold, 1 manual, 2 settle (<c>Orbital.Core.Flight.SasPhase</c>).</summary>
        public int Phase;
        /// <summary>[w,x,y,z], body to parent-inertial axes.</summary>
        public double[] Target;
        /// <summary>[x,y,z], N*m, body frame.</summary>
        public double[] Integral;
    }

    public class TimeScaleDto
    {
        public double Scale;
        /// <summary>Game time of the last change.</summary>
        public double Since;
    }

    /// <summary>hello_ok.clock: how the server process started (stage 2c.1b).</summary>
    public class BootInfoDto
    {
        public string BootId;
        public double ResumedAt;
        public double? DowntimeReal;
        public string Policy;
        public string Source;
    }

    public class CraftSpecDto
    {
        public double DryMass;
        public double MaxFuel;
        public double ThrustN;
        public double IspSeconds;
        /// <summary>The craft's own copy of the flight configuration (server FlightConfig JSON); null in old saves.</summary>
        public JObject Flight;
    }

    /// <summary>The stored record of a craft; the client reads only what it needs (the rest is ignored).</summary>
    public class CraftSnapshotDto
    {
        public string Id;
        public string Callsign;
        public string OwnerAccountId;
        public CraftSpecDto Spec;
        public double Fuel;
        public double? RcsFuel;
        public CraftModeDto Mode;
        public string ParentId;
        public double SnapshotTime;
        public int Version;
    }

    public class OrbitElementsDto
    {
        public double Inclination;
        public double Eccentricity;
        public double SemiMajorAxis;
        public double LongitudeAscendingNode;
        public double ArgumentOfPeriapsis;
        public double MeanAnomalyAtEpoch;
        public double Epoch;
    }

    /// <summary>State of a craft at <see cref="Time"/> (server CraftState): positions in the parent-inertial frame, radians for latitude/longitude.</summary>
    public class CraftStateDto
    {
        public string CraftId;
        public string Callsign;
        public double Time;
        public CraftModeDto Mode;
        /// <summary>Null from a server before 2c.8: then the mode decides (<see cref="StateMapper.IsSurface"/>).</summary>
        public CraftFrameDto? Frame;
        public string ParentId;
        public double[] Position;
        public double[] Velocity;
        public double[] GlobalPosition;
        public double Mass;
        public double Fuel;
        public double RcsFuel;
        /// <summary>[w,x,y,z].</summary>
        public double[] Orientation;
        public double[] AngularVelocity;
        public double[] GyroH;
        public double[] Inertia;
        public SasDto Sas;
        public bool Burning;
        public double? Latitude;
        public double? Longitude;
        public double Altitude;
        public double[] SurfaceVelocity;
        public double[] VelocityRelativeToSurface;
        public int Version;
        public OrbitElementsDto Orbit;
        public double? CrashedAt;
        public double? DestroyAt;
        /// <summary>The thrust tail (stage 2c.8): only in proximity_update, for a craft whose burn goes on past its last accepted segment.</summary>
        public ThrustTailDto Thrust;
        /// <summary>Client side only: the state came in a push of the near group (proximity_update), the detailed channel.</summary>
        [JsonIgnore] public bool Detailed;
    }

    /// <summary>
    /// Server ThrustTail (stage 2c.8): the last accepted segment of the craft ended at <see cref="Time"/> with the engine on at
    /// <see cref="Throttle"/> along <see cref="Direction"/>; position, velocity and fuel are its state then.
    /// </summary>
    public class ThrustTailDto
    {
        public double Time;
        public double Throttle;
        public double[] Direction;
        public double[] Position;
        public double[] Velocity;
        public double Fuel;
    }

    /// <summary>Degrees.</summary>
    public class SpawnPointDto
    {
        public string BodyId;
        public double Latitude;
        public double Longitude;
    }

    public class WreckDto
    {
        public string Id;
        public string CraftId;
        public string Callsign;
        public string AccountId;
        public double Time;
        public string Reason;
        public string ParentId;
        public double Latitude;
        public double Longitude;
        public double ImpactSpeed;
    }

    // ------------------------------------------------------------------ requests (client -> server)

    public class HelloRequest
    {
        public string Nickname;
        public string Token;
    }

    public class GetStateRequest
    {
        /// <summary>Null = every craft.</summary>
        public List<string> CraftIds;
    }

    public class SpawnRequestDto
    {
        public string StartPointId;
        /// <summary>A free point (admin only).</summary>
        public SpawnPointDto Point;
    }

    /// <summary>A thrust segment (server ManeuverRequest). Vectors in the parent-inertial frame, quaternions [w,x,y,z].</summary>
    public class ManeuverRequestDto
    {
        public double[] Direction;
        public double Throttle;
        public double? StartTime;
        public double Duration;
        public double[] Orientation;
        public double[] Omega;
        public double[] GyroH;
        public double[] EndOrientation;
        public double[] EndOmega;
        public double[] EndGyroH;
        /// <summary>Throttle right after the end (stage 2c.8): above 0 the burn goes on in the next segment; a hint for the others' ghosts.</summary>
        public double? EndThrottle;
        /// <summary>RCS propellant (kg) since the last attitude record the server accepted, up to the end of the segment.</summary>
        public double RcsFuelUsed;
        public SasDto Sas;
        public SasDto EndSas;
        public long? Seq;
        public string CraftId;
    }

    /// <summary>Attitude while coasting (server AttitudeReport).</summary>
    public class AttitudeReportDto
    {
        public double? Time;
        public double[] Orientation;
        public double[] Omega;
        public double[] GyroH;
        public double RcsFuelUsed;
        public SasDto Sas;
        public long? Seq;
        public string CraftId;
    }

    public class AdminAuthRequest
    {
        /// <summary>Never logged: <see cref="ProtocolCodec.ForLog"/> masks it.</summary>
        public string Secret;
    }

    public class AdminSetTimeScaleRequest
    {
        public double Scale;
    }

    // ------------------------------------------------------------------ replies and pushes (server -> client)

    public class HelloOkMessage
    {
        public string Nickname;
        public string CraftId;
        public string Token;
        public bool Created;
        public double ServerTime;
        public TimeScaleDto TimeScale;
        public BootInfoDto Clock;
        /// <summary>Null when the account has no craft (spawn needed).</summary>
        public CraftSnapshotDto Snapshot;
        public CraftStateDto State;
        public long? LastSeq;
        public double? CommittedUntil;
        public bool Control;
        public string WorldHash;
        public double WorldEpochZero;
    }

    public class HelloErrorMessage
    {
        public string Reason;
    }

    public class PongMessage
    {
        public double Time;
        public double Scale;
        public string BootId;
    }

    /// <summary>get_world reply; the client loads <see cref="WorldJson"/> with the Core world loader and checks the hash.</summary>
    public class WorldMessage
    {
        public double EpochZero;
        public string WorldHash;
        public string WorldJson;
        public List<WreckDto> Wrecks;
    }

    /// <summary>get_wrecks reply (stage 2c.7).</summary>
    public class WrecksMessage
    {
        public List<WreckDto> Wrecks;
    }

    public class StatesMessage
    {
        public double Time;
        public List<CraftStateDto> States;
    }

    public class SpawnResultDto
    {
        public bool Ok;
        public string Reason;
        public string CraftId;
        public string StartPointId;
        public SpawnPointDto Point;
        public CraftStateDto State;
        public CraftSnapshotDto Snapshot;
        public int ArchiveAvailable;
        public int ArchiveInTransit;
        public long? LastSeq;
        public double? CommittedUntil;
        public string AbandonedCraftId;
    }

    public class ManeuverResultDto
    {
        public bool Accepted;
        public string Reason;
        public string Message;
        public double StartTime;
        public double EndTime;
        public double FuelUsed;
        public double FuelRemaining;
        public double RcsFuelUsed;
        public double RcsFuelRemaining;
        public double DeltaV;
        public double MaxDeltaV;
        public string Outcome;
        public double? DestroyAt;
        public CraftStateDto StateAtEnd;
        /// <summary>On a rejection: the authoritative state now.</summary>
        public CraftStateDto State;
        public int Version;
        public long? Seq;
        public long? LastSeq;
        public double? CommittedUntil;
    }

    public class AttitudeResultDto
    {
        public bool Accepted;
        public string Reason;
        public string Message;
        public double RcsFuelUsed;
        public double RcsFuelRemaining;
        public CraftStateDto State;
        public int Version;
        public long? Seq;
        public long? LastSeq;
        public double? CommittedUntil;
    }

    /// <summary>Envelope of spawn_result, maneuver_result and attitude_result: the result is nested.</summary>
    public class ResultMessage<T>
    {
        public T Result;
    }

    public class CraftUpdateMessage
    {
        public double Time;
        public string Cause;
        public string CraftId;
        public long? Seq;
        public CraftStateDto State;
    }

    public class TimeScaleMessage
    {
        public double Scale;
        public double Time;
        public double Previous;
    }

    public class ControlTakenMessage
    {
        public double Time;
        public string CraftId;
        public string Message;
    }

    public class CraftDestroyedMessage
    {
        public string CraftId;
        public string Reason;
        public double Time;
        public string ParentId;
        public double Latitude;
        public double Longitude;
        public double ImpactSpeed;
        public WreckDto Wreck;
        public bool Abandoned;
    }

    public class WreckCreatedMessage
    {
        public WreckDto Wreck;
    }

    public class AdminAuthResultMessage
    {
        public bool Ok;
        public string Reason;
        public double? RetryAfterSeconds;
    }

    public class AdminResultMessage
    {
        public string Command;
        public bool Ok;
        public string Reason;
        public string Message;
        public double Scale;
        public double Previous;
        public double Time;
        public bool Changed;
    }

    public class ErrorMessage
    {
        public string Reason;
        public string Message;
    }

    // ------------------------------------------------------------------ codec

    /// <summary>A decoded message: its type, the request id it answers (null for a push) and the whole object.</summary>
    public sealed class Envelope
    {
        public string Type;
        public long? Id;
        public JObject Body;

        public T As<T>() => Body.ToObject<T>(ProtocolCodec.Serializer);
    }

    /// <summary>JSON of the protocol: camelCase, nulls left out, InvariantCulture, enums as camelCase strings, no date parsing.</summary>
    public static class ProtocolCodec
    {
        public const string Masked = "***";

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) },
        };

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        /// <summary>{"type": type, "id": id, ...fields of body}. A null body sends only the type and id.</summary>
        public static string Encode(string type, long? id, object body = null)
        {
            JObject o = new JObject { ["type"] = type };
            if (id != null) o["id"] = id.Value;
            if (body != null)
            {
                JObject fields = JObject.FromObject(body, Serializer);
                foreach (JProperty p in fields.Properties())
                    if (p.Name != "type" && p.Name != "id") o[p.Name] = p.Value;
            }
            return o.ToString(Formatting.None);
        }

        /// <summary>Parses one message. Throws <see cref="JsonException"/> on text that is not a JSON object.</summary>
        public static Envelope Decode(string text)
        {
            JObject o;
            using (JsonTextReader reader = new JsonTextReader(new StringReader(text)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                reader.Culture = CultureInfo.InvariantCulture;
                JToken token = JToken.ReadFrom(reader);
                o = token as JObject;
                if (o == null) throw new JsonReaderException("a message must be a JSON object");
            }
            Envelope e = new Envelope { Body = o, Type = (string) o["type"] };
            JToken id = o["id"];
            if (id != null && id.Type == JTokenType.Integer) e.Id = id.Value<long>();
            return e;
        }

        /// <summary>The message text for a log: every "secret" field (any depth, any case) becomes "***". Not JSON: replaced by a placeholder.</summary>
        public static string ForLog(string text)
        {
            if (text == null) return null;
            JToken token;
            try
            {
                using (JsonTextReader reader = new JsonTextReader(new StringReader(text)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    token = JToken.ReadFrom(reader);
                }
            }
            catch (JsonException)
            {
                return text.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ? "<unparsed message hidden: it mentions a secret>" : text;
            }
            Mask(token);
            return token.ToString(Formatting.None);
        }

        private static void Mask(JToken token)
        {
            if (token is JObject o)
            {
                foreach (JProperty p in o.Properties())
                {
                    if (string.Equals(p.Name, "secret", StringComparison.OrdinalIgnoreCase)) p.Value = Masked;
                    else Mask(p.Value);
                }
            }
            else if (token is JArray a)
            {
                foreach (JToken item in a) Mask(item);
            }
        }
    }
}
