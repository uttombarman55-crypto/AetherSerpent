using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Account
{
    [Serializable]
    public class AccountProfile
    {
        public string playerId = "";
        public int numericId = 0;
        public string username = "";
        public bool isGuest = false;
        public int avatarId = 1;
        public int frameId = 1;
        public int bannerId = 1;
        public string bio = "Drifting through the celestial void.";
        public string squadName = "";
        public int level = 1;
        public int exp = 0;
        public int matchesPlayed = 0;
        public int totalKills = 0;
        public int highestScore = 0;
        public int highestLength = 50;
        public int favoriteEvolution = 0; // 0=Titan, 1=Celestial, 2=Abyssal
        public int usernameChangesLeft = 3;
        public long createdAt = 0;

        public string GetEvolutionName()
        {
            switch (favoriteEvolution)
            {
                case 1: return "Celestial Drake";
                case 2: return "Abyssal Wyrm";
                default: return "Titan Colossus";
            }
        }
    }

    [Serializable]
    public class PrivacySettingsData
    {
        public int allowInvites = 0; // 0=Everyone, 1=Friends Only, 2=Nobody
        public int allowMessages = 0; // 0=Everyone, 1=Friends Only, 2=Nobody
        public bool showOnlineStatus = true;
    }

    [Serializable]
    public class BlockedUserData
    {
        public string blockedId = "";
        public string username = "";
        public int numericId = 0;
        public long blockedAt = 0;
    }

    [Serializable]
    public class AccountAuthResponse
    {
        public bool success;
        public string accessToken;
        public string refreshToken;
        public string playerId;
        public string username;
        public int numericId;
        public bool isGuest;
        public string recoveryCode;
        public int expiresIn;
        public AccountProfile profile;
        public string error;
    }

    [Serializable]
    public class ProfileGetResponse
    {
        public bool success;
        public AccountProfile profile;
        public string error;
    }

    [Serializable]
    public class ProfileSearchResponse
    {
        public bool success;
        public int count;
        public List<AccountProfile> results = new List<AccountProfile>();
        public string error;
    }

    [Serializable]
    public class PrivacyGetResponse
    {
        public bool success;
        public PrivacySettingsData privacy;
        public string error;
    }

    [Serializable]
    public class BlocklistResponse
    {
        public bool success;
        public int count;
        public List<BlockedUserData> blockedUsers = new List<BlockedUserData>();
        public string error;
    }

    [Serializable]
    public class SimpleAccountResponse
    {
        public bool success;
        public string message;
        public string error;
        public string username;
        public int usernameChangesLeft;
        public string recoveryCode;
        public AccountProfile profile;
    }
}
