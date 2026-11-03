using System.Security.Cryptography;
using System.Text;

namespace Orbital.Core.Worlds
{
    /// <summary>
    /// Identity of the shared world file (stage 2c.W). Engine-free and without a JSON library: the server links this file too, so the client
    /// and the server compute the hash with the same code.
    /// The hash is SHA-256 (lowercase hex) of the UTF-8 bytes of the text with a leading byte order mark removed and line ends normalized
    /// to LF. It is a hash of the text, not of the parsed numbers: reformatting the file changes it (the client then only downloads the
    /// world again), while two copies that differ only in CRLF/LF (git autocrlf, a Linux server) give the same hash.
    /// </summary>
    public static class WorldFileHash
    {
        /// <summary>Name of the shared world file in the config folder (Assets/_game/Config, the server's Config folder).</summary>
        public const string FileName = "world.json";

        public static string Compute(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Normalize(text));
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            StringBuilder result = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                result.Append(b.ToString("x2"));
            }

            return result.ToString();
        }

        /// <summary>The text that is hashed: no byte order mark, CRLF and lone CR turned into LF.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text[0] == (char) 0xFEFF) text = text.Substring(1);
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
