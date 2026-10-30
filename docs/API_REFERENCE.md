# Cloudflare Backend API & WebSocket Protocol Reference

**Production Base URL:** `https://aetherserpent-backend.uttombarman55.workers.dev`  
**WebSocket Base URL:** `wss://aetherserpent-backend.uttombarman55.workers.dev`  

---

## 1. REST Endpoints

### 1.1 Health & Diagnostics
- **`GET /api/health`**
  - Response:
    ```json
    {
      "status": "online",
      "service": "AetherSerpent_EternalCoil Backend",
      "environment": "production",
      "version": "1.0.0",
      "timestamp": 1793372704173
    }
    ```

### 1.2 System Version & Maintenance
- **`GET /api/system/version-check?clientVersion=1.0.0`**
  - Response:
    ```json
    {
      "clientVersion": "1.0.0",
      "minSupportedVersion": "1.0.0",
      "latestVersion": "1.0.0",
      "isUpdateRequired": false,
      "isMaintenanceActive": false,
      "maintenanceMessage": "All celestial servers operating normally."
    }
    ```

- **`GET /api/system/maintenance`**
  - Response:
    ```json
    {
      "maintenance": false,
      "message": "All servers nominal. Co-op and Leaderboards active."
    }
    ```

### 1.3 Authentication
- **`POST /api/auth/login`**
  - Headers: `Content-Type: application/json`, `X-Turnstile-Token: <token>`
  - Body:
    ```json
    {
      "username": "CelestialDrifter",
      "turnstileToken": "test-token"
    }
    ```
  - Response:
    ```json
    {
      "success": true,
      "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
      "playerId": "usr_556f4792",
      "username": "CelestialDrifter",
      "expiresIn": 900
    }
    ```

- **`POST /api/auth/refresh`**
  - Body: `{ "refreshToken": "<valid_refresh_token>" }`
  - Response: Rotated access token + new refresh token.

- **`POST /api/auth/logout`**
  - Body: `{}`
  - Response: `{ "success": true, "message": "Session terminated" }`

### 1.4 Matchmaking
- **`POST /api/matchmaking/find-or-create`**
  - Headers: `Authorization: Bearer <accessToken>`
  - Body:
    ```json
    {
      "preferredRoomId": "COOP_ALPHA",
      "requestedRole": "coop_head" // "solo" | "coop_head" | "coop_tail"
    }
    ```
  - Response:
    ```json
    {
      "success": true,
      "roomId": "COOP_ALPHA",
      "assignedRole": "coop_head",
      "wsUrl": "/api/room/COOP_ALPHA/ws?playerId=usr_556f4792&username=CelestialDrifter&role=coop_head"
    }
    ```

### 1.5 Leaderboards
- **`POST /api/leaderboard/submit`**
  - Headers: `Authorization: Bearer <accessToken>`
  - Body:
    ```json
    {
      "score": 28400,
      "length": 110,
      "evolutionPath": 1
    }
    ```
  - Response: `{ "success": true, "submittedAt": 1793372795186 }`

- **`GET /api/leaderboard/global?limit=50&offset=0`**
  - Response:
    ```json
    {
      "success": true,
      "count": 1,
      "entries": [
        {
          "playerId": "usr_0073c88d",
          "username": "TitanSlayer99",
          "score": 28400,
          "length": 110,
          "evolutionPath": 1,
          "submittedAt": 1793372795186
        }
      ]
    }
    ```

- **`GET /api/leaderboard/weekly?limit=50&offset=0`**
  - Response: Returns top entries for current UTC week number and year.

### 1.6 Friends & Parties
- **`GET /api/social/friends/list`**
  - Headers: `Authorization: Bearer <accessToken>`
- **`POST /api/social/friends/add`**
  - Body: `{ "friendId": "usr_12345678", "friendUsername": "FriendName" }`
- **`POST /api/social/friends/remove`**
  - Body: `{ "friendId": "usr_12345678" }`
- **`POST /api/social/party/create`**
  - Body: `{}`
- **`POST /api/social/party/join`**
  - Body: `{ "partyId": "PTY_AB12CD" }`
- **`GET /api/social/party/info?partyId=PTY_AB12CD`**

### 1.7 Cloud Saves
- **`POST /api/save/cloud`**
  - Headers: `Authorization: Bearer <accessToken>`
  - Body: `{ "playerId": "usr_...", "saveVersion": 1, "saveDataJson": "...", "checksum": "..." }`
- **`GET /api/save/cloud?playerId=usr_...`**

---

## 2. WebSocket Protocol (GameRoomDO)

**Endpoint:** `wss://aetherserpent-backend.uttombarman55.workers.dev/api/room/{roomId}/ws?playerId={id}&username={name}&role={role}`

### Client -> Server Messages
1. **Heartbeat:**
   ```json
   { "type": "PING", "timestamp": 1793372700000 }
   ```
2. **Input Update (20Hz):**
   ```json
   {
     "type": "INPUT_UPDATE",
     "payload": {
       "role": "coop_head",
       "moveInput": { "x": 0.5, "y": 1.0 },
       "verticalInput": 0.0,
       "isSprinting": true,
       "isAbilityTriggered": false,
       "tailWhipTriggered": false,
       "tailStabilizeAxis": 0.0,
       "clientTimestamp": 1793372700050
     },
     "timestamp": 1793372700050
   }
   ```
3. **Prey Consumption Claim:**
   ```json
   {
     "type": "EAT_PREY_REQUEST",
     "payload": {
       "preyId": "orb_14",
       "preyType": 2,
       "claimedPosition": { "x": 12.5, "y": 3.5, "z": -8.2 },
       "clientTimestamp": 1793372700100
     },
     "timestamp": 1793372700100
   }
   ```

### Server -> Client Messages
1. **Heartbeat Response:**
   ```json
   { "type": "PONG", "timestamp": 1793372700000 }
   ```
2. **Room Joined Initialization:**
   ```json
   {
     "type": "ROOM_JOINED",
     "payload": {
       "playerId": "usr_556f4792",
       "assignedRole": "coop_head",
       "state": { ... }
     }
   }
   ```
3. **Authoritative Tick Sync (20Hz Broadcast):**
   ```json
   {
     "type": "TICK_SYNC",
     "payload": {
       "headPosition": { "x": 0.0, "y": 3.5, "z": 0.0 },
       "headRotation": { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 },
       "speed": 10.0,
       "isBoosting": false,
       "evolutionPath": 0,
       "segmentCount": 50,
       "health": 100,
       "hunger": 100,
       "energy": 100,
       "score": 0,
       "lastUpdatedTimestamp": 1793372700050
     }
   }
   ```
4. **Prey Consumed Confirmation:**
   ```json
   {
     "type": "PREY_CONSUMED",
     "payload": {
       "preyId": "orb_14",
       "newSegmentCount": 52,
       "newScore": 150
     }
   }
   ```
