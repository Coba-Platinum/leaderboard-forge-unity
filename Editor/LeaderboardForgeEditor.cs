using Codice.CM.Common.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace CobaPlatinum.LeaderboardForge
{
    public class LeaderboardForgeEditor : EditorWindow
    {
        // EditorPrefs
        private const string UrlPrefsKey = "CobaPlatinum_LeaderboardForge_ApiUrl";
        private const string KeyPrefsKey = "CobaPlatinum_LeaderboardForge_ApiKey";

        private string pendingSelectionPublicKey = string.Empty;

        private TextField apiUrlInput;
        private TextField apiKeyInput;
        private Button testConnectionButton;
        private bool isTestingConnection = false;
        private DropdownField bakeConfigLeaderboardSelectDropdown;
        private DropdownField diagnosticLeaderboardSelectDropdown;
        private TextField publicKeyInput;
        private TextField secretKeyInput;
        private ScrollView entriesScrollView;
        private Button refreshEntriesButton;
        private bool isRefreshingEntries = false;
        private ObjectField configAssetField;
        private Button bakeConfigButton;
        public TextField newLeaderboardNameInput;
        public Toggle ascendingOrderToggle;
        public DropdownField userIdModeDropdown;
        public DropdownField profanityFilterDropdown;
        private Button provisionButton;
        private bool isProvisioning = false;
        private Button purgeLeaderboardEntriesButton;
        private bool isPurgingEntries = false;

        private Label infoLabel;

        [MenuItem("Tools/Leaderboard Forge")]
        public static void OpenEditorWindow()
        {
            LeaderboardForgeEditor window = GetWindow<LeaderboardForgeEditor>();
            window.titleContent = new GUIContent("Coba Platinum Leaderboard Forge");
            window.minSize = new Vector2(800, 1118);
        }

        private void OnEnable()
        {
            FetchDeveloperLeaderboards();
        }

        private void OnDisable()
        {
            LeaderboardDataCache.OnCacheUpdated -= UpdateDropdownChoices;

            if (testConnectionButton != null)
            {
                testConnectionButton.clicked -= RunConnectionTest;
            }

            if (provisionButton != null)
            {
                provisionButton.clicked -= ExecuteLeaderboardProvisioning;
            }

            if (refreshEntriesButton != null)
            {
                refreshEntriesButton.clicked -= RefreshLiveDiagnosticEntries;
            }

            if (bakeConfigButton != null)
            {
                bakeConfigButton.clicked -= OnBakeConfigButtonClicked;
            }

            if (purgeLeaderboardEntriesButton != null)
            {
                purgeLeaderboardEntriesButton.clicked -= ExecuteLeaderboardPurge;
            }

            bakeConfigLeaderboardSelectDropdown.UnregisterValueChangedCallback(OnBakeDropdownSelectionChanged);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.cobaplatinum.leaderboardforge/Resources/UI Documents/LeaderboardForgeEditorWindow.uxml");
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);

            string currentPackageVersion = GetPackageVersion();

            // Map Inputs
            apiUrlInput = rootVisualElement.Q<TextField>("APIURLInput");
            apiKeyInput = rootVisualElement.Q<TextField>("APIKeyInput");
            bakeConfigLeaderboardSelectDropdown = rootVisualElement.Q<DropdownField>("BakeConfigLeaderboardSelectDropdown");
            diagnosticLeaderboardSelectDropdown = rootVisualElement.Q<DropdownField>("DiagnosticLeaderboardSelectDropdown");
            testConnectionButton = rootVisualElement.Q<Button>("TestConnectionButton");
            publicKeyInput = rootVisualElement.Q<TextField>("PublicKeyInput");
            secretKeyInput = rootVisualElement.Q<TextField>("SecretKeyInput");
            refreshEntriesButton = rootVisualElement.Q<Button>("RefreshEntriesButton");
            entriesScrollView = rootVisualElement.Q<ScrollView>("EntriesScrollView");
            configAssetField = rootVisualElement.Q<ObjectField>("ConfigAssetField");
            bakeConfigButton = rootVisualElement.Q<Button>("BakeConfigButton");
            newLeaderboardNameInput = rootVisualElement.Q<TextField>("NewLeaderboardNameInput");
            ascendingOrderToggle = rootVisualElement.Q<Toggle>("AscendingOrderToggle");
            userIdModeDropdown = rootVisualElement.Q<DropdownField>("UserIDModeDropdown");
            profanityFilterDropdown = rootVisualElement.Q<DropdownField>("ProfanityFilterDropdown");
            provisionButton = rootVisualElement.Q<Button>("ProvisionLeaderboardButton");
            purgeLeaderboardEntriesButton = rootVisualElement.Q<Button>("PurgeLeaderboardEntriesButton");
            infoLabel = rootVisualElement.Q<Label>("InfoLabel");
            infoLabel.text = $"Version {currentPackageVersion} - Created by Coba Platinum";

            if (apiUrlInput != null)
            {
                apiUrlInput.value = EditorPrefs.GetString(UrlPrefsKey, LeaderboardForge.DEFAULT_API_URL);

                apiUrlInput.RegisterValueChangedCallback(evt => {
                    string cleanedUrl = evt.newValue.Trim();
                    EditorPrefs.SetString(UrlPrefsKey, cleanedUrl);

                    if (string.IsNullOrEmpty(cleanedUrl))
                    {
                        PurgeLeaderboardCache();
                    }
                });
            }

            if (apiKeyInput != null)
            {
                apiKeyInput.value = EditorPrefs.GetString(KeyPrefsKey, "");

                apiKeyInput.RegisterValueChangedCallback(evt => {
                    string cleanedKey = evt.newValue.Trim();
                    EditorPrefs.SetString(KeyPrefsKey, cleanedKey);

                    if (string.IsNullOrEmpty(cleanedKey))
                    {
                        PurgeLeaderboardCache();
                    }
                });
            }

            if (testConnectionButton != null)
            {
                testConnectionButton.clicked += RunConnectionTest;
            }

            if (provisionButton != null)
            {
                provisionButton.clicked += ExecuteLeaderboardProvisioning;
            }

            if (refreshEntriesButton != null)
            {
                refreshEntriesButton.clicked += RefreshLiveDiagnosticEntries;
            }

            if (bakeConfigButton != null)
            {
                bakeConfigButton.clicked += OnBakeConfigButtonClicked;
            }

            if (purgeLeaderboardEntriesButton != null)
            {
                purgeLeaderboardEntriesButton.clicked += ExecuteLeaderboardPurge;
            }

            LeaderboardDataCache.OnCacheUpdated += UpdateDropdownChoices;
            bakeConfigLeaderboardSelectDropdown.RegisterValueChangedCallback(OnBakeDropdownSelectionChanged);
            
            UpdateDropdownChoices();
            LoadEditorPrefs();
        }

        private string GetPackageVersion()
        {
            string targetPath = "Packages/com.cobaplatinum.leaderboardforge/package.json";

            if (File.Exists(targetPath))
            {
                try
                {
                    string rawJsonText = File.ReadAllText(targetPath);
                    PackageJsonData packageData = JsonUtility.FromJson<PackageJsonData>(rawJsonText);
                    return packageData.version;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Leaderboard Forge] Could not read package format data: {ex.Message}");
                }
            }

            return "1.0.0-beta"; // Placeholder string fallback
        }

        private void UpdateDropdownChoices()
        {
            if (bakeConfigLeaderboardSelectDropdown != null)
            {
                List<string> dropdownChoices = new List<string>(LeaderboardDataCache.GetDropdownDisplayStrings());

                dropdownChoices.Insert(0, "Add Manually");

                bakeConfigLeaderboardSelectDropdown.choices = dropdownChoices;

                // Check if a leaderboard was just provisioned, if so automatically select it
                if (!string.IsNullOrEmpty(pendingSelectionPublicKey))
                {
                    int targetIndex = -1;

                    for (int i = 0; i < LeaderboardDataCache.CachedLeaderboards.Count; i++)
                    {
                        if (LeaderboardDataCache.CachedLeaderboards[i].public_key == pendingSelectionPublicKey)
                        {
                            targetIndex = i + 1;
                            break;
                        }
                    }

                    if (targetIndex != -1)
                    {
                        bakeConfigLeaderboardSelectDropdown.index = targetIndex;

                        pendingSelectionPublicKey = string.Empty;
                        return;
                    }
                }

                if (bakeConfigLeaderboardSelectDropdown.choices.Count > 0 && bakeConfigLeaderboardSelectDropdown.index < 0)
                {
                    bakeConfigLeaderboardSelectDropdown.index = 0;
                }
            }

            if (diagnosticLeaderboardSelectDropdown != null)
            {
                diagnosticLeaderboardSelectDropdown.choices = LeaderboardDataCache.GetDropdownDisplayStrings();

                if (diagnosticLeaderboardSelectDropdown.choices.Count > 0 && diagnosticLeaderboardSelectDropdown.index < 0)
                {
                    diagnosticLeaderboardSelectDropdown.index = 0;
                }
            }
        }

        private void PurgeLeaderboardCache()
        {
            CobaPlatinum.LeaderboardForge.LeaderboardDataCache.UpdateCache(null);

            if (entriesScrollView != null)
            {
                entriesScrollView.Clear();
            }

            Debug.Log("[Leaderboard Forge] Active connection variables cleared. Leaderboard cache reset completed.");
        }

        private void LoadEditorPrefs()
        {
            if (apiUrlInput != null)
            {
                apiUrlInput.value = EditorPrefs.GetString(UrlPrefsKey, LeaderboardForge.DEFAULT_API_URL);

                apiUrlInput.RegisterValueChangedCallback(evt => {
                    EditorPrefs.SetString(UrlPrefsKey, evt.newValue.Trim());
                });
            }

            if (apiKeyInput != null)
            {
                apiKeyInput.value = EditorPrefs.GetString(KeyPrefsKey, "");

                apiKeyInput.RegisterValueChangedCallback(evt => {
                    EditorPrefs.SetString(KeyPrefsKey, evt.newValue.Trim());
                });
            }
        }

        private void RunConnectionTest()
        {
            if (isTestingConnection) return;

            // If the input is empty or blank, fall back to your default domain URL.
            string baseUrl = string.IsNullOrEmpty(apiUrlInput.value)
                ? LeaderboardForge.DEFAULT_API_URL
                : apiUrlInput.value.Trim();

            // Remove any trailing slashes to prevent malformed URLs (e.g., https://domain.com)
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            }

            string devApiKey = apiKeyInput.value;

            if (string.IsNullOrEmpty(devApiKey))
            {
                EditorUtility.DisplayDialog("Validation Error", "Please provide a valid Developer API Key before testing connection.", "OK");
                return;
            }

            isTestingConnection = true;
            testConnectionButton.text = "Testing Connection...";
            testConnectionButton.SetEnabled(false);

            // Assemble endpoint destination structure
            string fullUrl = $"{baseUrl}/api/v1/management/test-connection";
            UnityWebRequest request = UnityWebRequest.Get(fullUrl);

            // Inject custom X-API-KEY passport string header
            request.SetRequestHeader("x-api-key", devApiKey);

            var operation = request.SendWebRequest();
            operation.completed += (op) => {
                isTestingConnection = false;
                testConnectionButton.text = "Test Connection";
                testConnectionButton.SetEnabled(true);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        ConnectionResponse response = JsonUtility.FromJson<ConnectionResponse>(request.downloadHandler.text);
                        var scopesList = new System.Collections.Generic.List<string>(response.authorizedScopes);

                        // Check if the user has leaderboard permissions
                        if (!scopesList.Contains("leaderboards"))
                        {
                            // Throw a stark warning popup with instructions on how to remedy it
                            EditorUtility.DisplayDialog("Limited Access Account",
                                "Successfully authenticated connection, but your account does not have access to the Leaderboard Service!\n\n" +
                                "Please verify your current active tier or enable the 'leaderboards' scope in your Coba Platinum Developer Dashboard.",
                                "I Understand");
                        }
                        else
                        {
                            // Connection fully operational with required permissions
                            string scopesDisplay = string.Join(", ", response.authorizedScopes);
                            EditorUtility.DisplayDialog("Connection Verified",
                                $"{response.message}\n\nUnlocked Features: [{scopesDisplay}]", "Hooray!");

                            // Fetch latest leaderboards
                            FetchDeveloperLeaderboards();
                        }
                    }
                    catch
                    {
                        EditorUtility.DisplayDialog("Warning", "Server ping was successful, but returned an unreadable response format.", "OK");
                    }
                }
                else
                {
                    // Capture explicitly passed 401 Unauthorized or general 404 network errors
                    string errorDetail = request.downloadHandler != null ? request.downloadHandler.text : "Network unreachable.";
                    EditorUtility.DisplayDialog("Connection Failed!",
                        $"Could not authenticate server.\n\nStatus: {request.error}\nDetail: {errorDetail}", "Fix Setup");
                }

                request.Dispose();
            };
        }

        private void FetchDeveloperLeaderboards()
        {
            // Pull the developer credentials safely cached inside the local machine's registry
            string baseUrl = EditorPrefs.GetString("CobaPlatinum_LeaderboardForge_ApiUrl", LeaderboardForge.DEFAULT_API_URL);
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);

            string devApiKey = EditorPrefs.GetString("CobaPlatinum_LeaderboardForge_ApiKey", "");

            // If no token exists yet, stop to prevent malformed requests
            if (string.IsNullOrEmpty(devApiKey)) return;

            // Assemble the target endpoint payload route
            string fullUrl = $"{baseUrl}/api/v1/management/leaderboards";
            UnityWebRequest request = UnityWebRequest.Get(fullUrl);

            // Pass API Key via standard authentication headers
            request.SetRequestHeader("x-api-key", devApiKey);

            // Dispatch the request asynchronously to keep the main editor UI fluid and non-blocking
            var operation = request.SendWebRequest();
            operation.completed += (op) =>
            {
                // Handle a successful database lookup sequence
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string rawJson = request.downloadHandler.text;

                        // Parse the raw JSON array string directly into your isolated Runtime models list
                        List<LeaderboardData> parsedBoardsList = JsonArrayParser.FromJson<LeaderboardData>(rawJson);

                        // Push the fresh payload to the global data cache
                        LeaderboardDataCache.UpdateCache(parsedBoardsList);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Leaderboard Forge] Parsing exception encountered during board synchronization: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Leaderboard Forge] Failed to automatically sync dropdown lists: {request.error}");
                }

                request.Dispose();
            };
        }

        private void OnBakeDropdownSelectionChanged(ChangeEvent<string> evt)
        {
            int selectedIndex = bakeConfigLeaderboardSelectDropdown.index;

            // Index 0 is your locally injected override option
            if (selectedIndex == 0)
            {
                publicKeyInput.SetEnabled(true);
                secretKeyInput.SetEnabled(true);
                publicKeyInput.value = "";
                secretKeyInput.value = "";
            }
            // Anything higher maps to your true cache array offset by exactly 1 position space
            else if (selectedIndex > 0)
            {
                int actualCacheIndex = selectedIndex - 1;
                var chosenBoard = LeaderboardDataCache.CachedLeaderboards[actualCacheIndex];

                publicKeyInput.value = chosenBoard.public_key;
                secretKeyInput.value = chosenBoard.secret_key;

                publicKeyInput.SetEnabled(false);
                secretKeyInput.SetEnabled(false);
            }
        }

        private void ExecuteLeaderboardProvisioning()
        {
            if (isProvisioning) return;

            string baseUrl = string.IsNullOrEmpty(apiUrlInput.value) ? LeaderboardForge.DEFAULT_API_URL : apiUrlInput.value.Trim();
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);

            string devApiKey = apiKeyInput.value.Trim();
            string boardName = newLeaderboardNameInput.value.Trim();
            bool isAscending = ascendingOrderToggle.value;
            string profanityFilterMode = profanityFilterDropdown.value;
            string userIdMode = "UniqueID";

            if (userIdModeDropdown.value == "Username Only")
            {
                userIdMode = "UsernameOnly";
            }
            else if (userIdModeDropdown.value == "Unique ID")
            {
                userIdMode = "UniqueID";
            }

            if (string.IsNullOrEmpty(devApiKey))
            {
                EditorUtility.DisplayDialog("Validation Error", "Please input your Developer API Key before creating a leaderboard.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(boardName))
            {
                EditorUtility.DisplayDialog("Validation Error", "Please provide a descriptive name for your new leaderboard tracking target.", "OK");
                return;
            }

            isProvisioning = true;
            provisionButton.text = "Provisioning Backend...";
            provisionButton.SetEnabled(false);

            // 2. Build out raw json format body block payload targeting your multi-tenant Express route
            // format expected -> { "developerApiKey": "sk_dev_live_xx", "name": "Level 1", "isAscending": false, "userIdMode": "UniqueID", "profanityFilterMode": "Censor" }
            string rawJsonBody = $"{{\"developerApiKey\":\"{devApiKey}\",\"name\":\"{boardName}\",\"isAscending\":{isAscending.ToString().ToLower()},\"userIdMode\":\"{userIdMode}\",\"profanityFilterMode\":\"{profanityFilterMode}\"}}";

            string fullUrl = $"{baseUrl}/api/v1/management/create";
            UnityWebRequest request = new UnityWebRequest(fullUrl, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(rawJsonBody);

            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            operation.completed += (op) =>
            {
                isProvisioning = false;
                provisionButton.text = "Provision Leaderboard";
                provisionButton.SetEnabled(true);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = request.downloadHandler.text;
                        ProvisionResponse response = JsonUtility.FromJson<ProvisionResponse>(jsonResponse);

                        newLeaderboardNameInput.value = "";

                        pendingSelectionPublicKey = response.public_key;

                        EditorUtility.DisplayDialog("Leaderboard Provisioned",
                            $"Successfully created board: {response.name}\n\nKeys were automatically populated inside the config tool workspace.\n\nDon't forget to bake your keys into a Leaderboard Config asset!", "Hooray!");

                        FetchDeveloperLeaderboards();
                    }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog("UI Template Error", "Database created your board, but structural UI parsing failed: " + ex.Message, "OK");
                    }
                }
                else
                {
                    string errorDetail = request.downloadHandler != null ? request.downloadHandler.text : "Network target unreachable.";
                    EditorUtility.DisplayDialog("Provisioning Failed",
                        $"Server rejected creation command request.\n\nStatus: {request.error}\nDetail: {errorDetail}", "Adjust Profile Settings");
                }

                request.Dispose();
            };
        }

        private void RefreshLiveDiagnosticEntries()
        {
            if (isRefreshingEntries || diagnosticLeaderboardSelectDropdown == null || entriesScrollView == null) return;

            int selectedDropdownIndex = diagnosticLeaderboardSelectDropdown.index;

            if (selectedDropdownIndex < 0 || LeaderboardDataCache.CachedLeaderboards.Count == 0)
            {
                EditorUtility.DisplayDialog("Diagnostic Error", "Please select a valid authenticated leaderboard from the dropdown to run live diagnostics.", "OK");
                return;
            }

            int targetCacheIndex = selectedDropdownIndex;

            if (targetCacheIndex >= LeaderboardDataCache.CachedLeaderboards.Count) return;

            string targetPublicKey = LeaderboardDataCache.CachedLeaderboards[targetCacheIndex].public_key;

            // Lock UI loops state changes to prevent button spamming anomalies
            isRefreshingEntries = true;
            refreshEntriesButton.text = "Loading Entries...";
            refreshEntriesButton.SetEnabled(false);

            // Pull server configurations safely cached out of the registry settings
            string baseUrl = EditorPrefs.GetString("CobaPlatinum_LeaderboardForge_ApiUrl", LeaderboardForge.DEFAULT_API_URL);
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);

            string fullUrl = $"{baseUrl}/api/v1/leaderboard/{targetPublicKey}?limit=50";
            UnityWebRequest request = UnityWebRequest.Get(fullUrl);

            var operation = request.SendWebRequest();
            operation.completed += (op) =>
            {
                isRefreshingEntries = false;
                refreshEntriesButton.text = "Refresh Live Entries";
                refreshEntriesButton.SetEnabled(true);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // Clear dummy/old display rows entirely before drawing fresh matrices
                        entriesScrollView.Clear();

                        string responseText = request.downloadHandler.text;
                        List<LiveLeaderboardEntry> scoresList = JsonArrayParser.FromJson<LiveLeaderboardEntry>(responseText);

                        if (scoresList.Count == 0)
                        {
                            // If no players have uploaded highscores yet, show a clean structural notification row template
                            VisualElement emptyRow = new VisualElement();
                            emptyRow.style.paddingLeft = 6;
                            emptyRow.style.paddingTop = 6;
                            emptyRow.style.height = 40;
                            Label emptyLabel = new Label("No player entries recorded on this leaderboard yet.") { style = { color = Color.gray, unityFontStyleAndWeight = FontStyle.Italic } };
                            emptyRow.Add(emptyLabel);
                            entriesScrollView.Add(emptyRow);
                        }
                        else
                        {
                            // Iterate over your verified server data lists and dynamically build responsive node rows
                            foreach (var entry in scoresList)
                            {
                                VisualElement row = new VisualElement();
                                row.style.flexDirection = FlexDirection.Row;
                                row.style.paddingLeft = 6;
                                row.style.paddingRight = 6;
                                row.style.paddingTop = 4;
                                row.style.paddingBottom = 4;
                                row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                                row.style.borderBottomWidth = 1;
                                row.style.height = 40;

                                Label rank = new Label($"#{entry.Rank}") { style = { width = 45, color = new Color(0.6431373f, 0.6431373f, 0.6431373f), unityTextAlign = TextAnchor.MiddleLeft, unityFontStyleAndWeight = FontStyle.Bold } };
                                Label name = new Label(entry.Username) { style = { flexGrow = 1, color = Color.white , unityTextAlign = TextAnchor.MiddleLeft, unityFontStyleAndWeight = FontStyle.Bold } };
                                Label score = new Label(entry.Score.ToString("N0")) { style = { width = 80, color = new Color(0.6431373f, 0.6431373f, 0.6431373f), unityTextAlign = TextAnchor.MiddleRight, unityFontStyleAndWeight = FontStyle.Bold } };

                                row.Add(rank);
                                row.Add(name);
                                row.Add(score);

                                // Inject the newly generated visual data element line node straight into the scrolling panel tree
                                entriesScrollView.Add(row);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog("UI Parsing Error", "Successfully retrieved scores packet, but layout parser crashed: " + ex.Message, "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Fetch Failed", $"Could not load leaderboard tracking rows.\n\nStatus: {request.error}", "OK");
                }

                request.Dispose();
            };
        }

        private void OnBakeConfigButtonClicked()
        {
            LeaderboardConfigDefinition targetAsset = configAssetField.value as LeaderboardConfigDefinition;

            if (targetAsset == null)
            {
                EditorUtility.DisplayDialog("Baking Error", "Please drag and drop a valid 'LeaderboardConfigDefinition' ScriptableObject asset into the target config slot.", "OK");
                return;
            }

            string baseUrl = string.IsNullOrEmpty(apiUrlInput.value)
                ? LeaderboardForge.DEFAULT_API_URL
                : apiUrlInput.value.Trim();
            string pKey = publicKeyInput.value.Trim();
            string sKey = secretKeyInput.value.Trim();

            if (string.IsNullOrEmpty(pKey) || string.IsNullOrEmpty(sKey))
            {
                EditorUtility.DisplayDialog("Validation Error", "Cannot bake configuration. Keys cannot be blank.", "OK");
                return;
            }

            targetAsset.apiBaseUrl = baseUrl;
            targetAsset.leaderboardPublicKey = pKey;

            targetAsset.SetSecretKey(sKey);

            EditorUtility.SetDirty(targetAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Leaderboard Config Baked Successfully",
                $"Your config file asset '{targetAsset.name}' has been updated.\n\nThe private secret key is completely obfuscated as binary bytes inside the asset framework code.", "Hooray!");
        }

        private void ExecuteLeaderboardPurge()
        {
            if (isPurgingEntries || diagnosticLeaderboardSelectDropdown == null) return;

            int selectedIndex = diagnosticLeaderboardSelectDropdown.index;

            // Ensure a valid leaderboard has been queried out of the global cache
            if (selectedIndex < 0 || LeaderboardDataCache.CachedLeaderboards.Count == 0)
            {
                EditorUtility.DisplayDialog("Purge Error", "Please select a valid authenticated leaderboard from the dropdown before attempting an erasure.", "OK");
                return;
            }

            int targetCacheIndex = selectedIndex;
            if (targetCacheIndex >= LeaderboardDataCache.CachedLeaderboards.Count) return;

            var targetBoard = LeaderboardDataCache.CachedLeaderboards[targetCacheIndex];

            // ----------------------------------------------------------------
            // DANGER ZONE SAFEGUARD: Explicit verification modal popup box
            // ----------------------------------------------------------------
            bool confirmWipe = EditorUtility.DisplayDialog(
                "Purge All Leaderboard Entries?",
                $"Are you absolutely sure you want to delete EVERY single score on the leaderboard '{targetBoard.name}'?\n\n" +
                "This action is permanent and cannot be undone.",
                "Yes, Purge Everything",
                "Cancel"
            );

            if (!confirmWipe) return; // Terminate execution line instantly if they hit cancel

            // Lock interactive controls to eliminate double-trigger network anomalies
            isPurgingEntries = true;
            purgeLeaderboardEntriesButton.text = "Purging Database Rows...";
            purgeLeaderboardEntriesButton.SetEnabled(false);

            // Retrieve saved developer preferences cache
            string baseUrl = EditorPrefs.GetString("CobaPlatinum_LeaderboardForge_ApiUrl", LeaderboardForge.DEFAULT_API_URL);
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            string devApiKey = EditorPrefs.GetString("CobaPlatinum_LeaderboardForge_ApiKey", "");

            // Construct JSON payload
            // expected format -> { "developerApiKey": "sk_dev_live_xx", "targetLeaderboardPublicKey": "uuid_xxx" }
            string rawJsonBody = $"{{\"developerApiKey\":\"{devApiKey}\",\"targetLeaderboardPublicKey\":\"{targetBoard.public_key}\"}}";

            string fullUrl = $"{baseUrl}/api/v1/management/leaderboard/clear";
            UnityWebRequest request = new UnityWebRequest(fullUrl, "DELETE");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(rawJsonBody);

            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            operation.completed += (op) =>
            {
                isPurgingEntries = false;
                purgeLeaderboardEntriesButton.text = "Purge Leaderboard Entries";
                purgeLeaderboardEntriesButton.SetEnabled(true);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    EditorUtility.DisplayDialog("Purge Successful", $"The data for '{targetBoard.name}' was wiped completely.", "Done");

                    // lear the active scrolling view container layout tree automatically
                    if (entriesScrollView != null)
                    {
                        entriesScrollView.Clear();

                        // Inject a clean visual notification state row confirmation
                        VisualElement feedbackRow = new VisualElement();
                        feedbackRow.style.paddingLeft = 6;
                        feedbackRow.style.paddingTop = 6;
                        Label feedbackLabel = new Label("Leaderboard purged. No player records active.") { style = { color = Color.gray, unityFontStyleAndWeight = FontStyle.Italic } };
                        feedbackRow.Add(feedbackLabel);
                        entriesScrollView.Add(feedbackRow);
                    }
                }
                else
                {
                    string errorDetail = request.downloadHandler != null ? request.downloadHandler.text : "Network target unreachable.";
                    EditorUtility.DisplayDialog("Purge Failed", $"Server rejected purge request.\n\nStatus: {request.error}\nDetail: {errorDetail}", "OK");
                }

                request.Dispose();
            };
        }

        private static class JsonArrayParser
        {
            public static List<T> FromJson<T>(string json)
            {
                string wrapJson = "{\"items\":" + json + "}";
                Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapJson);
                return new List<T>(wrapper.items);
            }

            [Serializable]
            private class Wrapper<T> { public T[] items; }
        }

        [Serializable]
        private class ConnectionResponse
        {
            public bool success;
            public string message;
            public string[] authorizedScopes;
        }

        [Serializable]
        public class LiveLeaderboardEntry
        {
            public int Rank;
            public string Username;
            public long Score;
            public string Metadata;
            public string UniqueID;
            public string Date;
        }

        [Serializable]
        private class ProvisionResponse
        {
            public string name;
            public string public_key;
            public string secret_key;
        }
    }
}