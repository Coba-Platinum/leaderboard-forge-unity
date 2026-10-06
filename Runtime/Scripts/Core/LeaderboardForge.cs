using CobaPatinum.LeaderboardForge;
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CobaPlatinum.LeaderboardForge
{
    public static class LeaderboardForge
    {
        public static string DEFAULT_API_URL = "https://leaderboards.cobaplatinum.com";

        private static LeaderboardForgeBehaviour _behaviour;

        // Manual override hook string variable when utilizing UserIDMode.Unhandled logic
        public static string CustomUserIdentifier { get; set; } = string.Empty;
        private const string PrefSystemKey = "CobaPlatinum_LeaderboardForge_ServerUID";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Debug.Log("[Leaderboard Forge] Initializing Leaderboard Forge Engine...");

            GameObject go = new GameObject("[LeaderboardForgeEngine]");
            _behaviour = go.AddComponent<LeaderboardForgeBehaviour>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            Debug.Log("[Leaderboard Forge] Leaderboard Forge Engine initialized!");
        }

        /// <summary>
        /// Retrieves or dynamically fetches the user identifier based on active config rules.
        /// </summary>
        public static void GetResolvedIdentifier(LeaderboardConfigDefinition config, Action<string> onResolved)
        {
            switch (config.userIDGenerationMode)
            {
                case UserIDGenerationMode.UnityDeviceID:
                    onResolved?.Invoke(SystemInfo.deviceUniqueIdentifier);
                    break;

                case UserIDGenerationMode.Unhandled:
                    if (string.IsNullOrEmpty(CustomUserIdentifier))
                    {
                        Debug.LogWarning("[Leaderboard Forge] UserIDMode is set to Unhandled, but CustomUserIdentifier remains empty!");
                    }
                    onResolved?.Invoke(CustomUserIdentifier);
                    break;

                case UserIDGenerationMode.ServerGeneratedUniqueID:
                    // Check if this machine already cached a server-assigned uid locally
                    string savedUid = PlayerPrefs.GetString(PrefSystemKey, "");
                    if (!string.IsNullOrEmpty(savedUid))
                    {
                        onResolved?.Invoke(savedUid);
                        break;
                    }

                    // Otherwise, invoke the network initialization route script routine
                    string url = $"{config.apiBaseUrl}/api/v1/auth/generate-uid";
                    _behaviour.StartCoroutine(FetchServerUidRoutine(url, (newUid) => {
                        PlayerPrefs.SetString(PrefSystemKey, newUid);
                        PlayerPrefs.Save();
                        onResolved?.Invoke(newUid);
                    }));
                    break;
            }
        }

        private static System.Collections.IEnumerator FetchServerUidRoutine(string url, Action<string> callback)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string txt = webRequest.downloadHandler.text;
                        UidResponse response = JsonUtility.FromJson<UidResponse>(txt);

                        callback?.Invoke(response.uniqueId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Leaderboard Forge] Failed to parse server UID layout: {ex.Message}");
                        callback?.Invoke("fallback_local_" + Guid.NewGuid().ToString());
                    }
                }
                else
                {
                    // Fall back cleanly to a local placeholder generation if backend times out or fails
                    callback?.Invoke("fallback_local_" + Guid.NewGuid().ToString());
                }
            }
        }


        /// <summary>
        /// Sets the user's unique identifier to the given string value.
        /// This will only be used if the leaderboard's ID mode is set to 'unhandled'
        /// </summary>
        /// <param name="uniqueID">The user's unique identifier.</param>
        public static void SetUserGuid(string uniqueID)
        {
            CustomUserIdentifier = uniqueID;
            Debug.Log("Set custom UID: " + CustomUserIdentifier);
        }

        /// <summary>
        /// Fetches entries from a leaderboard utilizing a given leaderboard config.
        /// </summary>
        public static void GetLeaderboard(LeaderboardConfigDefinition config, int limit, Action<LeaderboardEntry[]> callback, Action<string> errorCallback = null)
        {
            if (config == null || !config.HasValidConfig())
            {
                Debug.LogError("Cannot fetch entries: Invalid or incomplete ScriptableObject config asset passed.");
                return;
            }

            string url = $"{config.apiBaseUrl}/api/v1/leaderboard/{config.leaderboardPublicKey}?limit={limit}";
            _behaviour.SendGetRequest(url, callback, errorCallback);
        }

        /// <summary>
        /// Fetches entries with explicit ascending order overrides.
        /// </summary>
        public static void GetLeaderboard(LeaderboardConfigDefinition config, int limit, bool isAscending, Action<LeaderboardEntry[]> callback, Action<string> errorCallback = null)
        {
            if (config == null || !config.HasValidConfig()) return;

            string url = $"{config.apiBaseUrl}/api/v1/leaderboard/{config.leaderboardPublicKey}?limit={limit}&isAscending={isAscending.ToString().ToLower()}";
            _behaviour.SendGetRequest(url, callback, errorCallback);
        }

        /// <summary>
        /// Signs and posts player scores using the resolved environment identification rules.
        /// </summary>
        public static void UploadNewEntry(LeaderboardConfigDefinition config, string username, long score, string metadataJson = "", Action<bool> callback = null, Action<string> errorCallback = null)
        {
            if (config == null || !config.HasValidConfig()) return;

            // Resolve the correct user identity string
            GetResolvedIdentifier(config, (resolvedId) =>
            {
                if (string.IsNullOrEmpty(resolvedId))
                {
                    errorCallback?.Invoke("Score submission aborted. Could not resolve user identifier.");
                    return;
                }

                string cleanUsername = username.Trim();

                // Build hash template order matching server requirements
                // format -> resolvedId:score:username
                string stringToHash = $"{resolvedId}:{score}:{cleanUsername}";
                //Debug.Log(stringToHash);
                string generatedSignature = GenerateHMACSHA256(stringToHash, config.GetSecretKey());
                //Debug.Log(generatedSignature);

                PayloadWrapper jsonBody = new PayloadWrapper
                {
                    publicKey = config.leaderboardPublicKey,
                    signature = generatedSignature,
                    uniqueId = resolvedId,
                    username = cleanUsername,
                    score = score,
                    metadata = string.IsNullOrEmpty(metadataJson) ? null : metadataJson
                };

                string jsonPayload = JsonUtility.ToJson(jsonBody);
                string url = $"{config.apiBaseUrl}/api/v1/leaderboard/upload";

                _behaviour.SendPostRequest(url, jsonPayload, callback, errorCallback);
            });
        }

        // Cryptographic Hex Hash Utility
        private static string GenerateHMACSHA256(string message, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            using HMACSHA256 hmac = new(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(messageBytes);
            StringBuilder hex = new(hashBytes.Length * 2);
            foreach (byte b in hashBytes) hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        [Serializable]
        private class UidResponse
        {
            public string uniqueId;
        }

        [Serializable] private class PayloadWrapper 
        { 
            public string publicKey; 
            public string signature; 
            public string uniqueId; 
            public string username; 
            public long score; 
            public string metadata; 
        }

    }
}
