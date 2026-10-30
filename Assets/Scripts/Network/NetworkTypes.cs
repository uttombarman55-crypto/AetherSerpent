using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Network
{
    [Serializable]
    public struct NetworkVector3
    {
        public float x;
        public float y;
        public float z;

        public NetworkVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public NetworkVector3(Vector3 v)
        {
            this.x = v.x;
            this.y = v.y;
            this.z = v.z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public struct NetworkQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public NetworkQuaternion(Quaternion q)
        {
            this.x = q.x;
            this.y = q.y;
            this.z = q.z;
            this.w = q.w;
        }

        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
    }

    public enum NetworkRole
    {
        Solo,
        CoopHead, // Player A
        CoopTail  // Player B
    }

    [Serializable]
    public class NetworkSerpentState
    {
        public NetworkVector3 headPosition;
        public NetworkQuaternion headRotation;
        public float speed;
        public bool isBoosting;
        public int evolutionPath;
        public int segmentCount;
        public List<NetworkVector3> segmentPositions = new List<NetworkVector3>();
        public float health;
        public float hunger;
        public float energy;
        public int score;
        public long lastUpdatedTimestamp;
    }

    [Serializable]
    public class ClientInputPayload
    {
        public string role; // "solo", "coop_head", "coop_tail"
        public NetworkVector2 moveInput;
        public float verticalInput;
        public bool isSprinting;
        public bool isAbilityTriggered;
        public bool tailWhipTriggered;
        public float tailStabilizeAxis;
        public long clientTimestamp;
    }

    [Serializable]
    public struct NetworkVector2
    {
        public float x;
        public float y;

        public NetworkVector2(Vector2 v)
        {
            this.x = v.x;
            this.y = v.y;
        }
    }

    [Serializable]
    public class NetworkMessageEnvelope
    {
        public string type;
        public string payload;
        public long timestamp;
        public string senderId;
    }

    [Serializable]
    public class AuthResponsePayload
    {
        public bool success;
        public string accessToken;
        public string refreshToken;
        public string playerId;
        public string username;
        public int expiresIn;
        public string error;
    }

    [Serializable]
    public class MatchmakingResponsePayload
    {
        public bool success;
        public string roomId;
        public string assignedRole;
        public string wsUrl;
        public string error;
    }

    [Serializable]
    public class RoomJoinedPayload
    {
        public string playerId;
        public string assignedRole;
        public NetworkSerpentState state;
    }

    [Serializable]
    public class PreyConsumedPayload
    {
        public string preyId;
        public int newSegmentCount;
        public int newScore;
    }
}
