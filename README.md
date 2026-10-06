
# Coba Platinum Leaderboard Forge - Unity

 A high-performance, secure, multi-tenant leaderboard solution for Unity games.

 Leaderboard Forge allows game developers to provision, view, and manage leaderboards dynamically from within the Unity Editor while connecting to a secure server proxy framework.

 Leaderboard Forge supports:

 - Hardware-isolated device scores
- Username-only tracking
- Custom authorization integrations
- Server-side input sanitization
- Evasion-resistant local profanity filtering
- Protected developer dashboards

 ## Architecture Blueprint

```
[ Unity Game Traffic ] ──► [ Express API Server ] ──► [ Cloud Database ]
                                                              ▲
[ Web Admin Dashboard ] ──────────────────────────────────────┘
                  (Direct Link via Database SDK + RLS)
```

 The split architecture separates public game operations from management workflows:

 - **Unity Game Client:** Connects to the Express API proxy using a public key. The private secret key is never embedded as clear text in game builds.
- **Unity Editor Window:** Uses a secure universal developer token (`sk_dev_live_`) to provision and clear tables through protected management endpoints.
- **Express API Server:** Handles cryptographic signature validation, profanity blocking, data sanitization, and request rate limiting.
- **PostgreSQL Backend:** Uses row-level security (RLS) to enforce data boundaries so developers only interact with their own tables.

 # Features

 ## 1\. Unified ScriptableObject Configurations

 Game configuration maps onto standalone `.asset` container modules. This decouples gameplay logic from scene layouts and keeps project structures organized.

 ## 2\. XOR Cryptographic Secret Scrambling

 Sensitive secret keys are processed through an obfuscation layer before serialization. Raw data is stored as scrambled bytes instead of plaintext strings, reducing casual build-decompilation exposure.

 ## 3\. Native UI Toolkit Management Console

 Built entirely on Unity's modern UI Toolkit layout system using fluid Flexbox containers. It queries cloud states asynchronously to keep the interface responsive.

 ## 4\. Single Source of Truth Caching

 Downloaded developer structures update a central static memory cache. The cache broadcasts changes to all active dropdown components simultaneously, eliminating redundant network traffic.

 ## 5\. Server-Side Identity Validation

 User tracking rules are enforced at the database boundary. The game client sends standardized parameters while the Express server authoritatively evaluates identity, signatures, and row updates.

 ## 6\. Evasion-Resistant Soft Censorship

 Integrates an obscenity string-processing library that detects zero-width characters, punctuation shielding, and common leetspeak bypasses. Processing occurs server-side to maintain low response latency.

 # Installation

 ## Method A: Git URL Installation

 1. Open your Unity project and select **Window \> Package Manager**.
2. Click the **+** icon in the top-left corner.
3. Select **Add package from git URL...**
4. Enter this repository URL (https://github.com/Coba-Platinum/leaderboard-forge-unity.git).
5. Click **Add**.

 ## Method B: Manual Project Injection

 1. Clone the repository locally.
2. Drag and drop the master root folder directly into your project's **Assets** hierarchy.

 # Editor Workspace Usage

 ## 1\. Establishing Server Connections

 Open the management control panel by selecting:

 **Tools \> Leaderboard Forge**

 Enter your server's root API base URL and private account token (`sk_dev_live_...`).

 Click **Test Connection** to verify the configuration.

 The tool stores your inputs securely in local installation registries using `EditorPrefs`, allowing keys to persist across project restarts without storing secrets inside project source files.

 ## 2\. Provisioning New Boards

 Inside the **Provision New Leaderboard** interface:

 1. Enter a name, such as `Level 1 Time Attack`.
2. Configure the sorting rules.
3. Enable **Ascending** when lower values should rank higher, such as completion times.
4. Select the desired **User ID Tracking Mode**.
5. Select the desired **Profanity Filter**
6. Click **Provision Leaderboard**.

 The server will:

 - Generate the required keys
- Insert the leaderboard into the cloud database
- Return the leaderboard identifiers
- Automatically select the new leaderboard in the workspace cache

 ## 3\. Scrambling Data into Configuration Files

 1. Right-click inside the project asset tree.
2. Select **Create \> Leaderboard Forge \> Config Asset**.
3. Name the container, for example `Level1_Config.asset`.
4. In the main Forge panel, select the target leaderboard from the **Leaderboard** dropdown.
5. Drag the newly created `Level1_Config` file into the **Target Config Asset** field.
6. Click **Bake into Config Asset**.

 The credentials are then stored in scrambled form inside the asset container.

 ## 4\. Running Live Diagnostics

 Select an active leaderboard from the dropdown viewer and click **Refresh Live Entries**.

 The tool:

 - Clears visual placeholder entries
- Calls the server read routes asynchronously
- Formats numerical values with readable separators
- Displays the top 50 score records

 To clear leaderboard data before a release build:

 1. Click **Purge Leaderboard Entries**.
2. Confirm the safety prompt.

 # Code Reference Guide

 ## Central Data Models — `LeaderboardData.cs`

```
namespace CobaPlatinum.LeaderboardForge
{
    [System.Serializable]
    public class LeaderboardData
    {
        public string id;
        public string name;
        public string public_key;
        public string secret_key;
        public string created_at;
    }
}
```
## Central Data Models — `LeaderboardEntry.cs`
```
namespace CobaPatinum.LeaderboardForge
{
    [System.Serializable]
    public class LeaderboardEntry
    {
        public string Username;
        public int Score;
        public ulong Date;
        public string Metadata;
        public int Rank;
        [SerializeField] internal string UniqueID;

        /// <summary>
        /// Returns the rank of the entry with its suffix.
        /// </summary>
        /// <returns>Rank + suffix (e.g. 1st, 2nd, 3rd, 4th, 5th, etc.).</returns>
        public string RankSuffix()
        {
            var rank = Rank;
            var lastDigit = rank % 10;
            var lastTwoDigits = rank % 100;

            var suffix = lastDigit == 1 && lastTwoDigits != 11 ? "st" :
                lastDigit == 2 && lastTwoDigits != 12 ? "nd" :
                lastDigit == 3 && lastTwoDigits != 13 ? "rd" : "th";

            return $"{rank}{suffix}";
        }

    }
}

```
 # Game Integration Examples

 ## Instantiating UI Score Panels

```
using UnityEngine;
using CobaPlatinum.LeaderboardForge;

public class HighscoreDisplayPanel : MonoBehaviour
{
    public LeaderboardConfig targetConfig;
    public Transform rowParentContainer;
    public GameObject rowItemPrefab;

    private void OnEnable()
    {
        foreach (Transform child in rowParentContainer)
        {
            Destroy(child.gameObject);
        }

        LeaderboardForge.GetLeaderboard(
            targetConfig,
            10,
            (entries) =>
            {
                foreach (var player in entries)
                {
                    GameObject row =
                        Instantiate(
                            rowItemPrefab,
                            rowParentContainer
                        );

                    var rowScript =
                        row.GetComponent<LeaderboardRowUI>();

                    if (rowScript != null)
                    {
                        rowScript.SetRowData(
                            player.Rank, // Or player.RankSuffix() for formatted
                            player.Username,
                            player.Score
                        );
                    }
                }
            }
        );
    }
}
```

 ## Uploading Scores from Event Callbacks

```
using UnityEngine;
using UnityEngine.UI;
using CobaPlatinum.LeaderboardForge;

public class EndGameScreenController : MonoBehaviour
{
    public LeaderboardConfig targetConfig;
    public InputField playerInputNameBox;
    public Button submitButton;

    public void OnClickSubmit()
    {
        string chosenName =
            playerInputNameBox.text.Trim();

        if (string.IsNullOrEmpty(chosenName))
            return;

        submitButton.interactable = false;

        long finalScore =
            ScoreManager.GetSavedScore();

        string customTelemetry =
            "{\"skin_equipped\":\"platinum_armor\"}";

        LeaderboardForge.UploadNewEntry(
            targetConfig,
            chosenName,
            finalScore,
            customMetadata,
            (success) =>
            {
                submitButton.interactable = true;

                if (success)
                {
                    playerInputNameBox.text = "";
                    Debug.Log(
                        "Score posted successfully to the leaderboard."
                    );
                }
            }
        );
    }
}
```
