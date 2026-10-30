# Multiplayer & Two-Player Co-op Serpent Testing Guide

This guide describes how to run two independent clients and test dual-player Co-op control over a single synchronized 3D serpent.

---

## 1. Co-op Architecture Model

In AetherSerpent Co-op Mode, two players operate the same physical serpent:
- **Player A (Head):** Governs directional steering (`WASD`), ascension and diving, and main boost propulsion.
- **Player B (Tail):** Follows authoritative server trajectory, provides body stabilization damping, and triggers the powerful Tail Whip burst to stun nearby prey or shatter crystalline obstacles.

---

## 2. Launching Two Independent Clients

You can test either:
1. **Unity Editor + Standalone Build**:
   - Build a standalone Windows executable (`File -> Build Settings -> Build`).
   - Run the executable as Client 1, and press Play in Unity Editor as Client 2.
2. **Two Separate Terminal / Script Test Clients**:
   - Run the included WebSocket test script to act as Player B while playing in the Unity Editor as Player A.

---

## 3. Step-by-Step Co-op Room Session

1. **Client 1 (Player A - Head)**:
   - Start the game and enter `Gameplay.unity`.
   - Press **`M`** (or click **Open Co-op Matchmaking**).
   - Enter Room Code: `COOP_ALPHA`.
   - Click **`Join Co-op as HEAD (Player A)`**.
   - The status changes to `ONLINE` with Role: `CO-OP HEAD (PLAYER A)`.
   - You can steer the serpent freely in the Sanctuary.
2. **Client 2 (Player B - Tail)**:
   - In the second client, press **`M`**.
   - Enter the same Room Code: `COOP_ALPHA`.
   - Click **`Join Co-op as TAIL (Player B)`**.
   - The status changes to `ONLINE` with Role: `CO-OP TAIL (PLAYER B)`.
   - Notice that Player B's serpent head automatically mirrors Player A's position and orientation via server-authoritative dead reckoning.
   - Press `Space` / `Sprint` on Client 2 to fire tail-whip bursts!

---

## 4. Latency & Disconnect Recovery Verification

1. **Disconnection Test**:
   - In Client 1, press `M` and click **`Disconnect`**.
   - Client 1 falls back seamlessly to local solo movement.
   - Client 2 detects the disconnect and logs status smoothly.
2. **Reconnection Test**:
   - In Client 1, click `Join Co-op as HEAD (Player A)` again.
   - The GameRoom Durable Object reattaches Client 1 and resumes synchronous 20Hz state broadcast.
3. **Anti-Cheat Validation**:
   - Any client attempting to jump beyond 28 units/sec is automatically clamped by the server tick loop in `GameRoomDO.ts`.

