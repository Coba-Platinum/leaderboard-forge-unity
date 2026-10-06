using System.Text;
using UnityEngine;


namespace CobaPlatinum.LeaderboardForge
{
    [CreateAssetMenu(fileName = "New Leaderboard Config", menuName = "Leaderboard Forge/Config Asset")]
    public class LeaderboardConfigDefinition : ScriptableObject
    {
        [Header("🌐 API Connection")]
        [Tooltip("The public base URL for the leaderboard API.")]
        public string apiBaseUrl = LeaderboardForge.DEFAULT_API_URL;

        [Header("🔑 Leaderboard Keys")]
        [Tooltip("The public leaderboard key. Safe to expose inside built game assemblies.")]
        public string leaderboardPublicKey;

        [HideInInspector][SerializeField] private byte[] obfuscatedSecretKey;
        [HideInInspector][SerializeField] private int obfuscationSalt;

        [Header("⚙️ Leaderboard Settings")]
        [Tooltip("The settings for how this leaderboard is handled.\n\nSet this to 'Unhandled' if you plan to handle use auth yourself!")]
        public UserIDGenerationMode userIDGenerationMode;

        /// <summary>
        /// Scrambles and bakes a plain text secret key using an automated random XOR byte mask block.
        /// </summary>
        public void SetSecretKey(string rawSecretKey)
        {
            if (string.IsNullOrEmpty(rawSecretKey))
            {
                obfuscatedSecretKey = null;
                obfuscationSalt = 0;
                return;
            }

            // Generate a random dynamic integer shift block mask
            obfuscationSalt = UnityEngine.Random.Range(10, 254);
            byte[] rawBytes = Encoding.UTF8.GetBytes(rawSecretKey.Trim());

            obfuscatedSecretKey = new byte[rawBytes.Length];
            for (int i = 0; i < rawBytes.Length; i++)
            {
                // Mask data natively using XOR logic
                obfuscatedSecretKey[i] = (byte)(rawBytes[i] ^ obfuscationSalt);
            }
        }

        /// <summary>
        /// Reconstructs the clear text secret string dynamically in RAM when signing a packet.
        /// </summary>
        public string GetSecretKey()
        {
            if (obfuscatedSecretKey == null || obfuscatedSecretKey.Length == 0) return string.Empty;

            byte[] decryptedBytes = new byte[obfuscatedSecretKey.Length];
            for (int i = 0; i < obfuscatedSecretKey.Length; i++)
            {
                decryptedBytes[i] = (byte)(obfuscatedSecretKey[i] ^ obfuscationSalt);
            }

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        /// <summary>
        /// Validation checker confirming if this asset layout contains valid compiled signatures.
        /// </summary>
        public bool HasValidConfig()
        {
            return !string.IsNullOrEmpty(apiBaseUrl) &&
                   !string.IsNullOrEmpty(leaderboardPublicKey) &&
                   obfuscatedSecretKey != null &&
                   obfuscatedSecretKey.Length > 0;
        }
    }

    [System.Serializable]
    public enum UserIDGenerationMode
    {
        UnityDeviceID,
        ServerGeneratedUniqueID,
        Unhandled
    }
}
