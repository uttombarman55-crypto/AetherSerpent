using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AetherSerpent.Network;

namespace AetherSerpent.Account
{
    /// <summary>
    /// Free Fire-style Enterprise Account & Identity Manager.
    /// Manages:
    /// - Guest accounts bound to device identifier
    /// - Full registration & JWT login
    /// - Guest account upgrade to official credentials
    /// - Permanent 9-digit numeric Player IDs (searchable & immutable)
    /// - Account recovery via emergency recovery codes
    /// - Limited username modifications
    /// - Cosmetic profile customizations (avatars, frames, banners, bio, squad)
    /// - Esport privacy settings (invites, direct messages, visibility)
    /// - Block list & Player reporting system
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class AccountManager : MonoBehaviour
    {
        public static AccountManager Instance { get; private set; }

        private const string PREF_ACCESS_TOKEN = "aether_jwt_access";
        private const string PREF_REFRESH_TOKEN = "aether_jwt_refresh";
        private const string PREF_PLAYER_ID = "aether_player_id";
        private const string PREF_NUMERIC_ID = "aether_numeric_id";
        private const string PREF_USERNAME = "aether_username";
        private const string PREF_IS_GUEST = "aether_is_guest";
        private const string PREF_RECOVERY_CODE = "aether_recovery_code";
        private const string PREF_DEVICE_ID = "aether_device_id";

        [Header("Backend Configuration")]
        [SerializeField] private string backendUrl = "https://aetherserpent-backend.uttombarman55.workers.dev";
        [SerializeField] private string turnstileToken = "test-token";

        [Header("Runtime State")]
        [SerializeField] private bool isLoggedIn = false;
        [SerializeField] private bool isGuest = true;
        [SerializeField] private string playerId = "";
        [SerializeField] private int numericId = 0;
        [SerializeField] private string username = "AetherGuest";
        [SerializeField] private string accessToken = "";
        [SerializeField] private string refreshToken = "";
        [SerializeField] private string recoveryCode = "";
        [SerializeField] private string deviceId = "";

        public bool IsLoggedIn => isLoggedIn;
        public bool IsGuest => isGuest;
        public string PlayerId => playerId;
        public int NumericId => numericId;
        public string Username => username;
        public string AccessToken => accessToken;
        public string RecoveryCode => recoveryCode;
        public string DeviceId => deviceId;
        public AccountProfile Profile { get; private set; } = new AccountProfile();
        public PrivacySettingsData PrivacySettings { get; private set; } = new PrivacySettingsData();
        public List<BlockedUserData> BlockedUsers { get; private set; } = new List<BlockedUserData>();

        // Events
        public event Action<bool, AccountProfile> OnAccountStateChanged;
        public event Action<AccountProfile> OnProfileUpdated;
        public event Action<PrivacySettingsData> OnPrivacyUpdated;
        public event Action<List<BlockedUserData>> OnBlocklistUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitDeviceId();
            LoadCachedCredentials();
        }

        private async void Start()
        {
            await InitializeSessionAsync();
        }

        private void InitDeviceId()
        {
            deviceId = PlayerPrefs.GetString(PREF_DEVICE_ID, "");
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = Guid.NewGuid().ToString("N");
                }
                PlayerPrefs.SetString(PREF_DEVICE_ID, deviceId);
                PlayerPrefs.Save();
            }
        }

        private void LoadCachedCredentials()
        {
            accessToken = PlayerPrefs.GetString(PREF_ACCESS_TOKEN, "");
            refreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
            playerId = PlayerPrefs.GetString(PREF_PLAYER_ID, "");
            numericId = PlayerPrefs.GetInt(PREF_NUMERIC_ID, 0);
            username = PlayerPrefs.GetString(PREF_USERNAME, "Guest");
            isGuest = PlayerPrefs.GetInt(PREF_IS_GUEST, 1) == 1;
            recoveryCode = PlayerPrefs.GetString(PREF_RECOVERY_CODE, "");

            if (!string.IsNullOrEmpty(playerId) && numericId > 0)
            {
                Profile.playerId = playerId;
                Profile.numericId = numericId;
                Profile.username = username;
                Profile.isGuest = isGuest;
            }
        }

        private string GetBaseUrl()
        {
            if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.BackendHttpUrl))
            {
                return NetworkManager.Instance.BackendHttpUrl;
            }
            return backendUrl;
        }

        /// <summary>
        /// Initializes session: verifies cached token or automatically provisions guest account.
        /// </summary>
        public async Task InitializeSessionAsync()
        {
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(playerId))
            {
                AccountProfile verified = await FetchProfileAsync(numericId: numericId, targetPlayerId: playerId);
                if (verified != null)
                {
                    Profile = verified;
                    isLoggedIn = true;
                    username = verified.username;
                    isGuest = verified.isGuest;
                    SyncWithNetworkManager();
                    _ = FetchPrivacySettingsAsync();
                    _ = FetchBlocklistAsync();
                    OnAccountStateChanged?.Invoke(true, Profile);
                    Debug.Log($"[AccountManager] Existing session restored for {username} (ID: {numericId})");
                    return;
                }
            }

            // Fallback to Guest Account bound to Device ID
            Debug.Log("[AccountManager] No valid session found. Authenticating guest account with Device ID...");
            await GuestLoginAsync();
        }

        private void SyncWithNetworkManager()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SetAuthSession(accessToken, refreshToken, playerId, username);
            }
        }

        private void SaveSession(AccountAuthResponse auth)
        {
            accessToken = auth.accessToken ?? "";
            refreshToken = auth.refreshToken ?? "";
            playerId = auth.playerId ?? "";
            numericId = auth.numericId;
            username = auth.username ?? "";
            isGuest = auth.isGuest;
            if (!string.IsNullOrEmpty(auth.recoveryCode))
            {
                recoveryCode = auth.recoveryCode;
                PlayerPrefs.SetString(PREF_RECOVERY_CODE, recoveryCode);
            }

            if (auth.profile != null)
            {
                Profile = auth.profile;
            }
            else
            {
                Profile.playerId = playerId;
                Profile.numericId = numericId;
                Profile.username = username;
                Profile.isGuest = isGuest;
            }

            isLoggedIn = true;

            PlayerPrefs.SetString(PREF_ACCESS_TOKEN, accessToken);
            PlayerPrefs.SetString(PREF_REFRESH_TOKEN, refreshToken);
            PlayerPrefs.SetString(PREF_PLAYER_ID, playerId);
            PlayerPrefs.SetInt(PREF_NUMERIC_ID, numericId);
            PlayerPrefs.SetString(PREF_USERNAME, username);
            PlayerPrefs.SetInt(PREF_IS_GUEST, isGuest ? 1 : 0);
            PlayerPrefs.Save();

            SyncWithNetworkManager();
            OnAccountStateChanged?.Invoke(true, Profile);
        }

        /// <summary>
        /// Registers a brand new permanent account with username & password.
        /// </summary>
        public async Task<(bool success, string error)> RegisterAsync(string registerUsername, string password)
        {
            string url = $"{GetBaseUrl()}/api/account/register";
            string jsonBody = $"{{\"username\":\"{Escape(registerUsername)}\",\"password\":\"{Escape(password)}\",\"deviceId\":\"{deviceId}\",\"turnstileToken\":\"{turnstileToken}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AccountAuthResponse res = JsonUtility.FromJson<AccountAuthResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        SaveSession(res);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Registration failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Logs in with existing registered credentials.
        /// </summary>
        public async Task<(bool success, string error)> LoginAsync(string loginUsername, string password)
        {
            string url = $"{GetBaseUrl()}/api/account/login";
            string jsonBody = $"{{\"username\":\"{Escape(loginUsername)}\",\"password\":\"{Escape(password)}\",\"deviceId\":\"{deviceId}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AccountAuthResponse res = JsonUtility.FromJson<AccountAuthResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        SaveSession(res);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Login failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Logs in or creates a guest account bound to this device ID.
        /// </summary>
        public async Task<(bool success, string error)> GuestLoginAsync()
        {
            string url = $"{GetBaseUrl()}/api/account/guest-login";
            string jsonBody = $"{{\"deviceId\":\"{deviceId}\",\"turnstileToken\":\"{turnstileToken}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AccountAuthResponse res = JsonUtility.FromJson<AccountAuthResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        SaveSession(res);
                        Debug.Log($"[AccountManager] Guest login successful: {username} (ID: {numericId})");
                        return (true, null);
                    }
                    return (false, res?.error ?? "Guest login failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Upgrades current guest account to permanent official account without losing numeric ID or stats.
        /// </summary>
        public async Task<(bool success, string error)> UpgradeGuestAsync(string newUsername, string password)
        {
            if (!isGuest) return (false, "Current account is already a registered account.");

            string url = $"{GetBaseUrl()}/api/account/upgrade-guest";
            string jsonBody = $"{{\"username\":\"{Escape(newUsername)}\",\"password\":\"{Escape(password)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AccountAuthResponse res = JsonUtility.FromJson<AccountAuthResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        SaveSession(res);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Account upgrade failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Recovers account password using username/numeric ID + emergency recovery code.
        /// </summary>
        public async Task<(bool success, string newCode, string error)> RecoverAccountAsync(string numericOrName, string recCode, string newPassword)
        {
            string url = $"{GetBaseUrl()}/api/account/recover";
            string jsonBody = $"{{\"numericIdOrUsername\":\"{Escape(numericOrName)}\",\"recoveryCode\":\"{Escape(recCode.ToUpper())}\",\"newPassword\":\"{Escape(newPassword)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SimpleAccountResponse res = JsonUtility.FromJson<SimpleAccountResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        return (true, res.recoveryCode, null);
                    }
                    return (false, null, res?.error ?? "Recovery failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, null, err);
                }
            }
        }

        /// <summary>
        /// Changes player username (limited number of changes allowed).
        /// </summary>
        public async Task<(bool success, string error)> ChangeUsernameAsync(string newUsername)
        {
            if (Profile.usernameChangesLeft <= 0)
            {
                return (false, "No username modifications remaining.");
            }

            string url = $"{GetBaseUrl()}/api/account/change-username";
            string jsonBody = $"{{\"newUsername\":\"{Escape(newUsername)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SimpleAccountResponse res = JsonUtility.FromJson<SimpleAccountResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        username = res.username;
                        Profile.username = res.username;
                        Profile.usernameChangesLeft = res.usernameChangesLeft;
                        PlayerPrefs.SetString(PREF_USERNAME, username);
                        PlayerPrefs.Save();
                        SyncWithNetworkManager();
                        OnProfileUpdated?.Invoke(Profile);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Username update failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Updates cosmetic customization: Avatar, Frame, Banner, Bio, and Squad Name.
        /// </summary>
        public async Task<(bool success, string error)> UpdateProfileAsync(int avatarId, int frameId, int bannerId, string bio, string squadName)
        {
            string url = $"{GetBaseUrl()}/api/profile/update";
            string jsonBody = $"{{\"avatarId\":{avatarId},\"frameId\":{frameId},\"bannerId\":{bannerId},\"bio\":\"{Escape(bio)}\",\"squadName\":\"{Escape(squadName)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ProfileGetResponse res = JsonUtility.FromJson<ProfileGetResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.profile != null)
                    {
                        Profile = res.profile;
                        OnProfileUpdated?.Invoke(Profile);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Profile update failed.");
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Retrieves a profile by numeric ID, player ID, or username.
        /// </summary>
        public async Task<AccountProfile> FetchProfileAsync(int numericId = 0, string targetPlayerId = null, string targetUsername = null)
        {
            string query = "";
            if (numericId > 0) query = $"numericId={numericId}";
            else if (!string.IsNullOrEmpty(targetUsername)) query = $"username={UnityWebRequest.EscapeURL(targetUsername)}";
            else if (!string.IsNullOrEmpty(targetPlayerId)) query = $"playerId={UnityWebRequest.EscapeURL(targetPlayerId)}";
            else return null;

            string url = $"{GetBaseUrl()}/api/profile/get?{query}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                }

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ProfileGetResponse res = JsonUtility.FromJson<ProfileGetResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        return res.profile;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Searches profiles by partial numeric ID or username query.
        /// </summary>
        public async Task<List<AccountProfile>> SearchProfilesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<AccountProfile>();

            string url = $"{GetBaseUrl()}/api/profile/search?query={UnityWebRequest.EscapeURL(query)}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ProfileSearchResponse res = JsonUtility.FromJson<ProfileSearchResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.results != null)
                    {
                        return res.results;
                    }
                }
            }
            return new List<AccountProfile>();
        }

        /// <summary>
        /// Fetches user privacy settings.
        /// </summary>
        public async Task<PrivacySettingsData> FetchPrivacySettingsAsync()
        {
            if (string.IsNullOrEmpty(playerId)) return PrivacySettings;

            string url = $"{GetBaseUrl()}/api/profile/privacy";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    PrivacyGetResponse res = JsonUtility.FromJson<PrivacyGetResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.privacy != null)
                    {
                        PrivacySettings = res.privacy;
                        OnPrivacyUpdated?.Invoke(PrivacySettings);
                        return PrivacySettings;
                    }
                }
            }
            return PrivacySettings;
        }

        /// <summary>
        /// Updates privacy settings on Cloudflare edge.
        /// </summary>
        public async Task<bool> UpdatePrivacySettingsAsync(int allowInvites, int allowMessages, bool showOnline)
        {
            string url = $"{GetBaseUrl()}/api/profile/privacy";
            string jsonBody = $"{{\"allowInvites\":{allowInvites},\"allowMessages\":{allowMessages},\"showOnlineStatus\":{(showOnline ? "true" : "false")}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    PrivacySettings.allowInvites = allowInvites;
                    PrivacySettings.allowMessages = allowMessages;
                    PrivacySettings.showOnlineStatus = showOnline;
                    OnPrivacyUpdated?.Invoke(PrivacySettings);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Fetches current player's block list.
        /// </summary>
        public async Task<List<BlockedUserData>> FetchBlocklistAsync()
        {
            string url = $"{GetBaseUrl()}/api/profile/blocklist";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    BlocklistResponse res = JsonUtility.FromJson<BlocklistResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.blockedUsers != null)
                    {
                        BlockedUsers = res.blockedUsers;
                        OnBlocklistUpdated?.Invoke(BlockedUsers);
                        return BlockedUsers;
                    }
                }
            }
            return BlockedUsers;
        }

        /// <summary>
        /// Blocks a target player.
        /// </summary>
        public async Task<bool> BlockPlayerAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/profile/block";
            string jsonBody = $"{{\"targetId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await FetchBlocklistAsync();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Unblocks a previously blocked player.
        /// </summary>
        public async Task<bool> UnblockPlayerAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/profile/unblock";
            string jsonBody = $"{{\"targetId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await FetchBlocklistAsync();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Submits an anti-cheat / conduct report against a player.
        /// </summary>
        public async Task<(bool success, string error)> ReportPlayerAsync(string targetPlayerId, string reason, string details)
        {
            string url = $"{GetBaseUrl()}/api/profile/report";
            string jsonBody = $"{{\"targetId\":\"{Escape(targetPlayerId)}\",\"reason\":\"{Escape(reason)}\",\"details\":\"{Escape(details)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    return (true, null);
                }
                else
                {
                    string err = TryParseError(req.downloadHandler.text) ?? req.error;
                    return (false, err);
                }
            }
        }

        /// <summary>
        /// Clears session credentials and resets to a clean guest state.
        /// </summary>
        public void Logout()
        {
            PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(PREF_PLAYER_ID);
            PlayerPrefs.DeleteKey(PREF_NUMERIC_ID);
            PlayerPrefs.DeleteKey(PREF_USERNAME);
            PlayerPrefs.DeleteKey(PREF_IS_GUEST);
            PlayerPrefs.DeleteKey(PREF_RECOVERY_CODE);
            PlayerPrefs.Save();

            accessToken = "";
            refreshToken = "";
            playerId = "";
            numericId = 0;
            username = "Guest";
            isGuest = true;
            recoveryCode = "";
            isLoggedIn = false;
            Profile = new AccountProfile();

            Debug.Log("[AccountManager] User logged out. Re-provisioning guest session...");
            _ = GuestLoginAsync();
        }

        private static string Escape(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private static string TryParseError(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                int errIdx = json.IndexOf("\"error\":");
                if (errIdx >= 0)
                {
                    int startQuote = json.IndexOf('"', errIdx + 8);
                    if (startQuote >= 0)
                    {
                        int endQuote = json.IndexOf('"', startQuote + 1);
                        if (endQuote > startQuote)
                        {
                            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                }
            }
            catch {}
            return null;
        }
    }
}

