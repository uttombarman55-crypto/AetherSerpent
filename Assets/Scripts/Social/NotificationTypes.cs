using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Social
{
    public enum NotificationType
    {
        FriendRequest,
        SquadInvite,
        ClanInvite,
        Whisper,
        System
    }

    public enum PresenceStatus
    {
        Online,
        Away,
        InMatch,
        DoNotDisturb,
        Invisible,
        Offline
    }

    public enum LobbyReaction
    {
        Fire,
        Serpent,
        Crown,
        GG,
        Heart,
        Lightning,
        RIP,
        Shield
    }

    [Serializable]
    public class NotificationItem
    {
        public int id;
        public NotificationType type;
        public string title;
        public string message;
        public string senderId;
        public string senderName;
        public int senderNumericId;
        public string dataJson;
        public bool isRead;
        public long createdAt;

        // Runtime helper
        [NonSerialized] public float displayTimer = 5.0f;
    }

    [Serializable]
    public class RecentPlayer
    {
        public string playerId;
        public string username;
        public int numericId;
        public string squadTag;
        public string clanTag;
        public string matchMode = "Sanctuary";
        public string role = "Co-op Partner";
        public long matchTime;
    }

    [Serializable]
    public class ActiveLobbyReaction
    {
        public string senderName;
        public LobbyReaction reaction;
        public string emoji;
        public float timer = 2.5f;
        public Vector2 screenPos;
    }

    [Serializable]
    public class NotificationsListResponse
    {
        public bool success;
        public int unreadCount;
        public int count;
        public List<NotificationItemDto> notifications;
        public string error;
    }

    [Serializable]
    public class NotificationItemDto
    {
        public int id;
        public string type;
        public string title;
        public string message;
        public string senderId;
        public string senderName;
        public int senderNumericId;
        public string dataJson;
        public bool isRead;
        public long createdAt;
    }
}
