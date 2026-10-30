using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Social
{
    public enum ChatChannel
    {
        All = 0,
        Global = 1,
        Squad = 2,
        Clan = 3,
        Match = 4,
        Whisper = 5,
        System = 6
    }

    [Serializable]
    public class ChatMessageItem
    {
        public int id;
        public string channel = "global"; // "global", "squad", "clan", "match", "whisper", "system"
        public string senderId = "";
        public string senderName = "Drifter";
        public int senderNumericId = 0;
        public string clanTag = "";
        public string squadTag = "";
        public string recipientId = "";
        public string recipientName = "";
        public string targetGroupId = "";
        public string message = "";
        public long timestamp = 0;
        public bool isMention = false;

        public ChatChannel GetChannelEnum()
        {
            switch (channel?.ToLowerInvariant())
            {
                case "global": return ChatChannel.Global;
                case "squad": return ChatChannel.Squad;
                case "clan": return ChatChannel.Clan;
                case "match": return ChatChannel.Match;
                case "whisper": return ChatChannel.Whisper;
                case "system": return ChatChannel.System;
                default: return ChatChannel.Global;
            }
        }

        public string GetChannelColorHex()
        {
            switch (GetChannelEnum())
            {
                case ChatChannel.Global: return "#ffb300";   // Golden Amber
                case ChatChannel.Squad: return "#00e5ff";    // Cyan Neon
                case ChatChannel.Clan: return "#e040fb";     // Royal Violet
                case ChatChannel.Match: return "#00e676";    // Emerald Green
                case ChatChannel.Whisper: return "#ff4081";  // Rose Magenta
                case ChatChannel.System: return "#ffff00";   // Radiant Yellow
                default: return "#ffffff";
            }
        }

        public string GetChannelPrefix()
        {
            switch (GetChannelEnum())
            {
                case ChatChannel.Global: return "[GLOBAL]";
                case ChatChannel.Squad: return "[SQUAD]";
                case ChatChannel.Clan: return "[CLAN]";
                case ChatChannel.Match: return "[MATCH]";
                case ChatChannel.Whisper: return "[WHISPER]";
                case ChatChannel.System: return "[SYSTEM]";
                default: return "[CHAT]";
            }
        }

        public string GetFormattedTimestamp()
        {
            if (timestamp <= 0) return DateTime.Now.ToString("HH:mm");
            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime().DateTime;
            return dt.ToString("HH:mm");
        }

        public string GetSenderFullDisplay()
        {
            string tags = "";
            if (!string.IsNullOrEmpty(clanTag)) tags += $"[{clanTag}] ";
            if (!string.IsNullOrEmpty(squadTag)) tags += $"[{squadTag}] ";
            return $"{tags}{senderName}";
        }

        public string GetFormattedLine(string currentUsername = null)
        {
            string color = GetChannelColorHex();
            string prefix = GetChannelPrefix();
            string time = GetFormattedTimestamp();
            string sender = GetSenderFullDisplay();

            string body = message;
            if (isMention)
            {
                body = $"<color=#ffff00><b>{body}</b></color>";
            }

            if (GetChannelEnum() == ChatChannel.Whisper)
            {
                if (!string.IsNullOrEmpty(recipientName) && senderName == currentUsername)
                {
                    return $"<color={color}>[{time}] {prefix} To {recipientName}: {body}</color>";
                }
                return $"<color={color}>[{time}] {prefix} From {sender}: {body}</color>";
            }

            return $"<color={color}>[{time}] {prefix} {sender}: {body}</color>";
        }
    }

    [Serializable]
    public class SendChatPayload
    {
        public string channel = "global";
        public string message = "";
        public string recipientId = "";
        public int recipientNumericId = 0;
        public string recipientName = "";
        public string squadId = "";
        public string clanId = "";
        public string roomId = "";
    }

    [Serializable]
    public class ChatHistoryApiResponse
    {
        public bool success;
        public string channel;
        public int count;
        public List<ChatMessageItem> messages = new List<ChatMessageItem>();
        public string error;
    }

    [Serializable]
    public class ChatSendApiResponse
    {
        public bool success;
        public ChatMessageItem message;
        public string error;
    }

    [Serializable]
    public class ChatWsPacket
    {
        public string type;
        public ChatMessageItem payload;
        public string error;
        public long timestamp;
    }
}

