using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AetherSerpent.Network;

namespace AetherSerpent.Social
{
    [Serializable]
    public class FriendData
    {
        public string playerId;
        public string username;
        public long addedAt;
        public bool isOnline;
    }

    [Serializable]
    public class FriendsListResponse
    {
        public bool success;
        public List<FriendData> friends = new List<FriendData>();
        public string error;
    }

    [Serializable]
    public class PartyMemberData
    {
        public string playerId;
        public string username;
        public bool isLeader;
        public long joinedAt;
    }

    [Serializable]
    public class PartyData
    {
        public string partyId;
        public string leaderId;
        public string leaderName;
        public List<PartyMemberData> members = new List<PartyMemberData>();
        public long createdAt;
    }

    [Serializable]
    public class PartyResponse
    {
        public bool success;
        public string partyId;
        public PartyData party;
        public string error;
    }

    /// <summary>
    /// Governs Friends List & Party Co-op assembly against Cloudflare SocialHubDO.
    /// </summary>
    [DisallowMultipleComponent]
    public class SocialManager : MonoBehaviour
    {
        public static SocialManager Instance { get; private set; }

        public List<FriendData> Friends { get; private set; } = new List<FriendData>();
        public PartyData CurrentParty { get; private set; }

        public event Action<List<FriendData>> OnFriendsUpdated;
        public event Action<PartyData> OnPartyUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "http://127.0.0.1:8787";
        }

        private string GetToken()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.AccessToken : "";
        }

        /// <summary>
        /// Retrieves the player's friends list.
        /// </summary>
        public async Task<List<FriendData>> RefreshFriendsListAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/friends/list";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    FriendsListResponse res = JsonUtility.FromJson<FriendsListResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        Friends = res.friends ?? new List<FriendData>();
                        OnFriendsUpdated?.Invoke(Friends);
                        return Friends;
                    }
                }
            }

            return Friends;
        }

        /// <summary>
        /// Adds a friend by Player ID / username.
        /// </summary>
        public async Task<bool> AddFriendAsync(string friendId, string friendUsername)
        {
            string url = $"{GetBaseUrl()}/api/social/friends/add";
            string json = $"{{\"friendId\":\"{friendId}\",\"friendUsername\":\"{friendUsername}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshFriendsListAsync();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a new Co-op Party.
        /// </summary>
        public async Task<PartyData> CreatePartyAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/party/create";
            string json = "{}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    PartyResponse res = JsonUtility.FromJson<PartyResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.party != null)
                    {
                        CurrentParty = res.party;
                        OnPartyUpdated?.Invoke(CurrentParty);
                        return CurrentParty;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Joins an existing party via invite code.
        /// </summary>
        public async Task<bool> JoinPartyAsync(string partyId)
        {
            string url = $"{GetBaseUrl()}/api/social/party/join";
            string json = $"{{\"partyId\":\"{partyId.Trim()}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await FetchPartyInfoAsync(partyId);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Fetches live party information and members.
        /// </summary>
        public async Task<PartyData> FetchPartyInfoAsync(string partyId)
        {
            string url = $"{GetBaseUrl()}/api/social/party/info?partyId={partyId}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    PartyResponse res = JsonUtility.FromJson<PartyResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.party != null)
                    {
                        CurrentParty = res.party;
                        OnPartyUpdated?.Invoke(CurrentParty);
                        return CurrentParty;
                    }
                }
            }
            return null;
        }

        public void LeaveParty()
        {
            CurrentParty = null;
            OnPartyUpdated?.Invoke(null);
        }
    }
}

