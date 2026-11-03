using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Orbital.Core.Net
{
    /// <summary>
    /// Client connection settings (stage 2c.5): a JSON file at a path the caller gives (Unity: persistentDataPath/orbital-client.json,
    /// outside the repository) plus the environment variables ORBITAL_SERVER_URL, ORBITAL_CALLSIGN, ORBITAL_ADMIN_SECRET (they win over the
    /// file). Account tokens are kept per (url, callsign). The admin secret is never stored here: only the path of the file that holds it
    /// (the same kind of file the server reads; contents trimmed). No file and no variable = no admin_auth.
    /// </summary>
    public sealed class ClientSettings
    {
        public const string DefaultUrl = "ws://127.0.0.1:8765/ws";
        public const string DefaultAdminSecretFile = "E:/Projects/orbital-secrets/admin-secret.txt";
        public const string EnvUrl = "ORBITAL_SERVER_URL";
        public const string EnvCallsign = "ORBITAL_CALLSIGN";
        public const string EnvAdminSecret = "ORBITAL_ADMIN_SECRET";

        public string ServerUrl = DefaultUrl;
        public string Callsign;
        public string AdminSecretFile = DefaultAdminSecretFile;
        /// <summary>Key: <see cref="TokenKey"/>.</summary>
        public Dictionary<string, string> Tokens = new Dictionary<string, string>();

        [JsonIgnore] public string Path { get; private set; }

        public static string TokenKey(string url, string callsign) => (url ?? "").Trim().ToLowerInvariant() + "|" + (callsign ?? "").Trim();

        public string TokenFor(string url, string callsign) =>
            Tokens != null && Tokens.TryGetValue(TokenKey(url, callsign), out string t) ? t : null;

        public void SetToken(string url, string callsign, string token)
        {
            if (Tokens == null) Tokens = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(token)) Tokens.Remove(TokenKey(url, callsign));
            else Tokens[TokenKey(url, callsign)] = token;
        }

        /// <summary>
        /// Reads the file (missing or broken = defaults, the broken file is kept aside as .bad) and applies the environment.
        /// <paramref name="env"/> replaces <see cref="Environment.GetEnvironmentVariable(string)"/> in tests.
        /// </summary>
        public static ClientSettings Load(string path, Func<string, string> env = null)
        {
            env = env ?? Environment.GetEnvironmentVariable;
            ClientSettings s = null;
            if (path != null && File.Exists(path))
            {
                try { s = JsonConvert.DeserializeObject<ClientSettings>(File.ReadAllText(path)); }
                catch (Exception)
                {
                    try { File.Copy(path, path + ".bad", true); } catch (Exception) { }
                }
            }
            s = s ?? new ClientSettings();
            s.Path = path;
            if (s.Tokens == null) s.Tokens = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(s.ServerUrl)) s.ServerUrl = DefaultUrl;
            string url = env(EnvUrl), callsign = env(EnvCallsign);
            if (!string.IsNullOrWhiteSpace(url)) s.ServerUrl = url.Trim();
            if (!string.IsNullOrWhiteSpace(callsign)) s.Callsign = callsign.Trim();
            s._env = env;
            return s;
        }

        [JsonIgnore] private Func<string, string> _env = Environment.GetEnvironmentVariable;

        /// <summary>Writes the file (url, callsign, secret file path, tokens; never the secret). No path = nothing.</summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(Path)) return;
            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(Path)) File.Delete(Path);
            File.Move(tmp, Path);
        }

        /// <summary>
        /// The admin secret: ORBITAL_ADMIN_SECRET, else the trimmed contents of <see cref="AdminSecretFile"/>; null when neither exists or it is
        /// empty. The caller passes it to the session and keeps it out of logs and the screen.
        /// </summary>
        public string ReadAdminSecret()
        {
            string v = _env(EnvAdminSecret);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            if (string.IsNullOrWhiteSpace(AdminSecretFile)) return null;
            try
            {
                if (!File.Exists(AdminSecretFile)) return null;
                string s = File.ReadAllText(AdminSecretFile).Trim();
                return s.Length > 0 ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
