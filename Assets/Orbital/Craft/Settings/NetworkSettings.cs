using Orbital.Core.Net;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>Where the craft lives.</summary>
    public enum CraftNetMode
    {
        /// <summary>Local flight test: the craft lives on the client only.</summary>
        Offline,
        /// <summary>The craft lives on the server; Enter asks it for a spawn, the time follows the server.</summary>
        Online,
    }

    /// <summary>
    /// How this client connects and who it is (Assets/_game/Settings/NetworkSettings.asset). The environment variables ORBITAL_NET_MODE and
    /// ORBITAL_SERVER_URL win over this asset; tokens stay in persistentDataPath/orbital-client.json, the admin secret never goes here.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital/Network Settings", fileName = "NetworkSettings")]
    public class NetworkSettings : ScriptableObject
    {
        public CraftNetMode mode = CraftNetMode.Offline;
        [Tooltip("Server address for Online.")]
        public string serverUrl = ClientSettings.DefaultUrl;
        [Tooltip("The account's nickname; orbital-client.json wins when it has one.")]
        public string callsign = "Pilot";
    }
}
