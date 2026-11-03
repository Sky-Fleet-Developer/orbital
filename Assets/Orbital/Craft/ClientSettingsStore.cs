using System.IO;
using Orbital.Core.Net;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// Where the client keeps its connection settings (stage 2c.6): <c>persistentDataPath/orbital-client.json</c>, outside the repository.
    /// The file holds the server url, the callsign, the account tokens and the path of the admin secret file, never the secret itself.
    /// </summary>
    public static class ClientSettingsStore
    {
        public const string FileName = "orbital-client.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static ClientSettings Load() => ClientSettings.Load(DefaultPath);
    }
}
