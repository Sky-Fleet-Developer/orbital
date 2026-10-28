using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orbital.Core.Flight;

namespace Orbital.Core.Config
{
    /// <summary>Propellant type (fuels.json). The type decides the specific impulse of the main engine.</summary>
    public class FuelDef
    {
        public string Id;
        public string Name;
        public double IspSeconds;
    }

    /// <summary>Craft model (craftspec.json).</summary>
    public class CraftDef
    {
        public string Id;
        public string Name;
        public double DryMass;   // kg
        public double MaxFuel;   // kg of the main propellant
        public double ThrustN;   // N at full throttle
        public string Fuel;      // id of a FuelDef
    }

    /// <summary>Start point (startpoints.json). Kind is "surface" or "space"; latitude and longitude are degrees in the rotating body frame.</summary>
    public class StartPointDef
    {
        public string Id;
        public string Name;
        public string Kind;
        public string BodyId;
        public double Latitude;
        public double Longitude;

        public bool IsSurface => string.Equals(Kind, "surface", StringComparison.OrdinalIgnoreCase);
    }

    public class FuelCatalogFile
    {
        public List<FuelDef> Fuels = new List<FuelDef>();
        public string DefaultFuel;
    }

    public class CraftCatalogFile
    {
        public List<CraftDef> Crafts = new List<CraftDef>();
        public string DefaultCraft;
    }

    public class StartPointCatalogFile
    {
        public List<StartPointDef> StartPoints = new List<StartPointDef>();
        public string DefaultStartPoint;
    }

    /// <summary>
    /// The shared JSON configs of the client and the server (Assets/_game/Config) read together. Engine-free; the Unity side only hands it the text
    /// of the TextAssets.
    /// </summary>
    public class GameConfig
    {
        /// <summary>Flight numbers as the file gives them (missing parts come from <see cref="FlightConfig.Default"/>).</summary>
        public FlightConfig Flight = FlightConfig.Default();
        public FuelCatalogFile Fuels = new FuelCatalogFile();
        public CraftCatalogFile Crafts = new CraftCatalogFile();
        public StartPointCatalogFile StartPoints = new StartPointCatalogFile();

        public FuelDef FindFuel(string id) => Find(Fuels.Fuels, x => x.Id, id, Fuels.DefaultFuel);

        public CraftDef FindCraft(string id) => Find(Crafts.Crafts, x => x.Id, id, Crafts.DefaultCraft);

        public StartPointDef FindStartPoint(string id) => Find(StartPoints.StartPoints, x => x.Id, id, StartPoints.DefaultStartPoint);

        /// <summary>
        /// Flight config of a craft model. As on the server, the dry mass, the tank, the thrust and the specific impulse come from the craft model and
        /// its fuel and override the same fields of the flight config. Without a craft model the flight config is returned as it is.
        /// </summary>
        public FlightConfig ResolveFlight(string craftId = null)
        {
            FlightConfig f = Flight;
            CraftDef craft = FindCraft(craftId);
            if (craft == null) return f;
            f.DryMass = craft.DryMass;
            f.MainFuelCapacity = craft.MaxFuel;
            f.Engine.MaxThrust = craft.ThrustN;
            FuelDef fuel = FindFuel(craft.Fuel);
            if (fuel != null) f.Engine.Isp = fuel.IspSeconds;
            return f;
        }

        private static T Find<T>(List<T> list, Func<T, string> idOf, string id, string defaultId) where T : class
        {
            if (list == null || list.Count == 0) return null;
            string wanted = string.IsNullOrEmpty(id) ? defaultId : id;
            if (!string.IsNullOrEmpty(wanted))
            {
                foreach (T item in list)
                {
                    if (string.Equals(idOf(item), wanted, StringComparison.OrdinalIgnoreCase)) return item;
                }
                if (!string.IsNullOrEmpty(id)) return null; // an explicit unknown id is not replaced by the default
            }
            return list[0];
        }
    }

    /// <summary>Reads the shared configs from JSON text. A missing (null or empty) text leaves the built-in value of that part.</summary>
    public static class GameConfigLoader
    {
        public const string FlightFile = "flightconfig.json";
        public const string FuelsFile = "fuels.json";
        public const string CraftSpecFile = "craftspec.json";
        public const string StartPointsFile = "startpoints.json";

        public static GameConfig FromJson(string flightJson, string fuelsJson, string craftSpecJson, string startPointsJson)
        {
            GameConfig config = new GameConfig();
            if (!string.IsNullOrWhiteSpace(flightJson)) config.Flight = ParseFlight(flightJson);
            if (!string.IsNullOrWhiteSpace(fuelsJson)) config.Fuels = JsonConvert.DeserializeObject<FuelCatalogFile>(fuelsJson) ?? config.Fuels;
            if (!string.IsNullOrWhiteSpace(craftSpecJson)) config.Crafts = JsonConvert.DeserializeObject<CraftCatalogFile>(craftSpecJson) ?? config.Crafts;
            if (!string.IsNullOrWhiteSpace(startPointsJson))
            {
                config.StartPoints = JsonConvert.DeserializeObject<StartPointCatalogFile>(startPointsJson) ?? config.StartPoints;
            }
            return config;
        }

        /// <summary>Reads the four files of a folder; a file that is not there is skipped.</summary>
        public static GameConfig FromDirectory(string directory)
        {
            return FromJson(Read(directory, FlightFile), Read(directory, FuelsFile), Read(directory, CraftSpecFile), Read(directory, StartPointsFile));
        }

        /// <summary>
        /// FlightConfig from JSON: camelCase, nested structs, vectors as {"x","y","z"}; names are matched without regard to case and the file may be
        /// partial (what it does not say comes from <see cref="FlightConfig.Default"/>), like the server reads it.
        /// </summary>
        public static FlightConfig ParseFlight(string json)
        {
            JObject merged = JObject.FromObject(FlightConfig.Default());
            JObject file = JObject.Parse(json);
            Merge(merged, file);
            return merged.ToObject<FlightConfig>();
        }

        private static void Merge(JObject target, JObject source)
        {
            foreach (JProperty property in source.Properties())
            {
                JProperty existing = target.Property(property.Name, StringComparison.OrdinalIgnoreCase);
                if (existing == null)
                {
                    continue; // unknown fields are ignored
                }
                if (existing.Value is JObject existingObject && property.Value is JObject sourceObject)
                {
                    Merge(existingObject, sourceObject);
                }
                else
                {
                    existing.Value = property.Value.DeepClone();
                }
            }
        }

        private static string Read(string directory, string file)
        {
            string path = Path.Combine(directory, file);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
