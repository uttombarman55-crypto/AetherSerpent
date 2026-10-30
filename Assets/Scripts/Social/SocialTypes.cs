using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Social
{
    // ==========================================
    // 1. FRIENDS DATA TYPES
    // ==========================================

    [Serializable]
    public class FriendItem
    {
        public string playerId = "";
        public string username = "Drifter";
        public int numericId = 0;
        public bool isOnline = false;
        public string status = "offline"; // "online", "in_match", "offline"
        public long lastLoginAt = 0;
        public long addedAt = 0;
        public string currentSquadId = "";
        public string squadTag = "";
        public string clanTag = "";

        public string GetDisplayTag()
        {
            string tagStr = "";
            if (!string.IsNullOrEmpty(clanTag)) tagStr += $"[{clanTag}] ";
            if (!string.IsNullOrEmpty(squadTag)) tagStr += $"[{squadTag}] ";
            return $"{tagStr}{username}";
        }

        public string GetStatusBadge()
        {
            switch (status)
            {
                case "online": return "<color=#00ff66>● ONLINE</color>";
                case "in_match": return "<color=#ffcc00>● IN MATCH</color>";
                default: return "<color=#888888>○ OFFLINE</color>";
            }
        }
    }

    [Serializable]
    public class FriendRequestItem
    {
        public int id;
        public string senderId = "";
        public string senderName = "Drifter";
        public int senderNumericId = 0;
        public string receiverId = "";
        public string receiverName = "";
        public string status = "pending";
        public long createdAt = 0;
    }

    // ==========================================
    // 2. SQUAD DATA TYPES (4-5 PLAYERS)
    // ==========================================

    [Serializable]
    public class SquadMemberItem
    {
        public string playerId = "";
        public string username = "Drifter";
        public int numericId = 0;
        public string role = "member"; // "leader" or "member"
        public bool isReady = false;
        public long joinedAt = 0;
    }

    [Serializable]
    public class SquadItem
    {
        public string squadId = "";
        public string name = "Squad";
        public string tag = "AES";
        public string leaderId = "";
        public string leaderName = "";
        public int maxMembers = 5;
        public bool isOpen = true;
        public int squadXp = 0;
        public int squadLevel = 1;
        public string matchRoomId = "";
        public List<SquadMemberItem> members = new List<SquadMemberItem>();
        public long createdAt = 0;

        public bool AreAllMembersReady()
        {
            if (members == null || members.Count == 0) return false;
            foreach (var m in members)
            {
                if (!m.isReady) return false;
            }
            return true;
        }
    }

    [Serializable]
    public class SquadInviteItem
    {
        public int id;
        public string squadId = "";
        public string squadName = "";
        public string squadTag = "";
        public string inviterId = "";
        public string inviterName = "";
        public string status = "pending";
        public long createdAt = 0;
    }

    [Serializable]
    public class SquadChatItem
    {
        public int id;
        public string squadId = "";
        public string senderId = "";
        public string senderName = "";
        public string message = "";
        public long sentAt = 0;
    }

    // ==========================================
    // 3. CLAN / GUILD DATA TYPES (UP TO 50 PLAYERS)
    // ==========================================

    [Serializable]
    public class ClanMemberItem
    {
        public string playerId = "";
        public string username = "Drifter";
        public int numericId = 0;
        public string rank = "member"; // "leader", "officer", "member"
        public int contribution = 0;
        public bool isOnline = false;
        public long joinedAt = 0;
    }

    [Serializable]
    public class ClanItem
    {
        public string clanId = "";
        public string name = "Celestial Guild";
        public string tag = "ETRN";
        public string leaderId = "";
        public string leaderName = "";
        public string description = "";
        public string announcement = "";
        public int badgeId = 1;
        public int memberCount = 1;
        public int maxMembers = 50;
        public int minLevel = 1;
        public bool isOpen = true;
        public int clanXp = 0;
        public int clanLevel = 1;
        public List<ClanMemberItem> members = new List<ClanMemberItem>();
        public int onlineCount = 0;
        public long createdAt = 0;
    }

    [Serializable]
    public class ClanJoinRequestItem
    {
        public int id;
        public string clanId = "";
        public string playerId = "";
        public string username = "";
        public int numericId = 0;
        public int level = 1;
        public string status = "pending";
        public long createdAt = 0;
    }

    [Serializable]
    public class ClanChatItem
    {
        public int id;
        public string clanId = "";
        public string senderId = "";
        public string senderName = "";
        public string senderRank = "member";
        public string message = "";
        public long sentAt = 0;
    }

    // ==========================================
    // 4. API RESPONSE WRAPPERS
    // ==========================================

    [Serializable]
    public class FriendsListFullResponse
    {
        public bool success;
        public int count;
        public List<FriendItem> friends = new List<FriendItem>();
        public string error;
    }

    [Serializable]
    public class FriendRequestsResponse
    {
        public bool success;
        public List<FriendRequestItem> incoming = new List<FriendRequestItem>();
        public string error;
    }

    [Serializable]
    public class SquadResponse
    {
        public bool success;
        public bool hasSquad;
        public SquadItem squad;
        public string error;
    }

    [Serializable]
    public class SquadInvitesResponse
    {
        public bool success;
        public List<SquadInviteItem> invites = new List<SquadInviteItem>();
        public string error;
    }

    [Serializable]
    public class SquadChatResponse
    {
        public bool success;
        public List<SquadChatItem> messages = new List<SquadChatItem>();
        public string error;
    }

    [Serializable]
    public class SquadMatchLaunchResponse
    {
        public bool success;
        public string matchRoomId;
        public string wsUrl;
        public string message;
        public string error;
    }

    [Serializable]
    public class ClanResponse
    {
        public bool success;
        public bool hasClan;
        public string myRank;
        public ClanItem clan;
        public string error;
    }

    [Serializable]
    public class ClanListResponse
    {
        public bool success;
        public int count;
        public List<ClanItem> clans = new List<ClanItem>();
        public string error;
    }

    [Serializable]
    public class ClanRequestsResponse
    {
        public bool success;
        public List<ClanJoinRequestItem> requests = new List<ClanJoinRequestItem>();
        public string error;
    }

    [Serializable]
    public class ClanChatResponse
    {
        public bool success;
        public List<ClanChatItem> messages = new List<ClanChatItem>();
        public string error;
    }

    [Serializable]
    public class SocialActionResponse
    {
        public bool success;
        public string message;
        public string error;
    }
}

