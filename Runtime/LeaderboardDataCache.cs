using System;
using System.Collections.Generic;
using UnityEngine;

namespace CobaPlatinum.LeaderboardForge
{
    public class LeaderboardDataCache
    {
        // Global, centralized data list accessible by any script at any time
        public static List<LeaderboardData> CachedLeaderboards { get; private set; } = new List<LeaderboardData>();

        // An event that fires whenever the data updates, notifying all listening dropdowns to refresh themselves simultaneously
        public static event Action OnCacheUpdated;

        public static void UpdateCache(List<LeaderboardData> newBoardsList)
        {
            CachedLeaderboards = newBoardsList ?? new List<LeaderboardData>();

            OnCacheUpdated?.Invoke();
        }

        public static List<string> GetDropdownDisplayStrings()
        {
            List<string> displayStrings = new List<string>();
            foreach (var board in CachedLeaderboards)
            {
                displayStrings.Add($"{board.name} ({board.public_key.Substring(0, Mathf.Min(6, board.public_key.Length))}...)");
            }
            return displayStrings;
        }
    }

    [Serializable]
    public class LeaderboardData
    {
        public string id;
        public string name;
        public string public_key;
        public string secret_key;
        public string created_at;
    }
}
