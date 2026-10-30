using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Voice
{
    /// <summary>
    /// Supported Voice Channels in AetherSerpent.
    /// </summary>
    public enum VoiceChannel
    {
        Off,
        Squad,
        Match
    }

    /// <summary>
    /// Microphone capture mode.
    /// </summary>
    public enum VoiceInputMode
    {
        OpenMic,
        PushToTalk
    }

    /// <summary>
    /// WebRTC signaling message types for peer-to-peer / mesh negotiation.
    /// </summary>
    public enum WebRtcSignalType
    {
        Offer,
        Answer,
        Candidate,
        Join,
        Leave,
        State
    }

    /// <summary>
    /// Represents a remote peer in the active voice session.
    /// </summary>
    [Serializable]
    public class VoicePeer
    {
        public string playerId;
        public string username;
        public int numericId;
        public string squadTag;
        public string clanTag;
        public VoiceChannel channel = VoiceChannel.Squad;

        public bool isSpeaking = false;
        public bool isMuted = false;
        public bool isDeafened = false;
        public float volumeLevel = 0f; // 0.0 to 1.0 (realtime RMS meter)

        // Local settings for this peer
        public float userVolumeScale = 1.0f; // 0.0 to 2.0 (individual volume control)
        public bool isLocallyMuted = false;

        // Runtime audio playback components
        [NonSerialized] public AudioSource audioSource;
        [NonSerialized] public GameObject sourceObj;
        [NonSerialized] public Queue<float> sampleQueue = new Queue<float>();
        [NonSerialized] public float lastPacketTime = 0f;
        [NonSerialized] public Vector3 worldPosition = Vector3.zero;
    }

    /// <summary>
    /// Persisted settings for local player voice chat.
    /// </summary>
    [Serializable]
    public class VoiceSettings
    {
        public string selectedMicrophone = "";
        public VoiceInputMode inputMode = VoiceInputMode.OpenMic;
        public KeyCode pttKey = KeyCode.V;
        public float vadThreshold = 0.015f; // RMS speech detection threshold
        public float micGain = 1.0f; // Input amplification (0.5 to 2.0)
        public float masterVoiceVolume = 1.0f; // Master incoming volume (0.0 to 2.0)
        public bool noiseSuppressionEnabled = true;
        public bool echoCancellationEnabled = true;
        public bool spatialProximityEnabled = true;
        public bool isMicMuted = false;
        public bool isDeafened = false;
    }

    /// <summary>
    /// Wire payload for audio packets relayed through edge.
    /// </summary>
    [Serializable]
    public class VoiceAudioPacketPayload
    {
        public string type = "VOICE_FRAME";
        public string senderId;
        public string senderName;
        public string channel;
        public string squadId;
        public string roomId;
        public int sequence;
        public string audioBase64;
        public float posX;
        public float posY;
        public float posZ;
        public long timestamp;
    }

    /// <summary>
    /// Wire payload for WebRTC SDP / ICE signals relayed through edge.
    /// </summary>
    [Serializable]
    public class WebRtcSignalPayload
    {
        public string type = "VOICE_SIGNAL";
        public string senderId;
        public string senderName;
        public int senderNumericId;
        public string targetPeerId;
        public string signalType;
        public string sdp;
        public string candidate;
        public long timestamp;
    }

    /// <summary>
    /// Wire payload for voice user state updates.
    /// </summary>
    [Serializable]
    public class VoiceStateUpdatePayload
    {
        public string type = "VOICE_STATE_UPDATE";
        public bool isSpeaking;
        public bool isMuted;
        public bool isDeafened;
        public float volumeLevel;
    }

    /// <summary>
    /// Wire payload for channel switching.
    /// </summary>
    [Serializable]
    public class VoiceChannelSetPayload
    {
        public string type = "VOICE_CHANNEL_SET";
        public string channel;
        public string squadId;
        public string roomId;
    }

    /// <summary>
    /// Wire payload for reporting voice abuse.
    /// </summary>
    [Serializable]
    public class VoiceReportPayload
    {
        public string type = "VOICE_REPORT";
        public string targetId;
        public string reason;
        public string details;
    }
}

