# Cloudflare Workers & Durable Objects Deployment Guide

**Backend Name:** `aetherserpent-backend`  
**Runtime:** Cloudflare Workers with SQLite Durable Objects & WebSocket Hibernation API  
**Wrangler Version:** `4.156.0`  
**Live Production URL:** `https://aetherserpent-backend.uttombarman55.workers.dev`  
**Live WebSocket Root:** `wss://aetherserpent-backend.uttombarman55.workers.dev`  

---

## 1. Cloudflare Architecture Overview

The backend uses a distributed serverless and stateful edge architecture:

1. **Edge Router (`src/index.ts`)**:
   - Handles global CORS preflight and sliding-window IP rate limiting (120 req/min).
   - Validates Cloudflare Turnstile bot verification tokens.
   - Issues short-lived Web Crypto HMAC-SHA256 access tokens (15m) and refresh tokens (7d).
   - Routes matchmaking, health status, and system version checks.
2. **GameRoom Durable Object (`src/GameRoomDO.ts`)**:
   - Backed by embedded SQLite (`new_sqlite_classes: ["GameRoomDO"]`).
   - Runs a 20Hz (50ms) authoritative simulation tick loop via Durable Object Alarms.
   - Employs the WebSocket Hibernation API (`ctx.acceptWebSocket`) for zero compute cost while waiting for network I/O.
   - Enforces real-time anti-cheat: speed limit bounds, teleport detection, and prey distance validation.
   - Implements two-player Co-op Serpent merging (Player A Head controls steering/pitch; Player B Tail controls body stabilization and whip bursts).
3. **Social Hub Durable Object (`src/SocialHubDO.ts`)**:
   - Backed by embedded SQLite (`new_sqlite_classes: ["SocialHubDO"]`).
   - Manages global and weekly leaderboards using server-side ISO week calculations (Monday 00:00:00 UTC boundary).
   - Manages friends lists and co-op party creation and joining.
   - Stores versioned player cloud saves with checksum verification.
4. **World Zone Durable Object (`src/WorldZoneDO.ts`)**:
   - Ready for open-world spatial sharding.

---

## 2. Configuration & Bindings (`wrangler.jsonc`)

```jsonc
{
  "name": "aetherserpent-backend",
  "main": "src/index.ts",
  "compatibility_date": "2025-01-01",
  "compatibility_flags": [
    "nodejs_compat"
  ],
  "durable_objects": {
    "bindings": [
      {
        "name": "GAME_ROOM",
        "class_name": "GameRoomDO"
      },
      {
        "name": "WORLD_ZONE",
        "class_name": "WorldZoneDO"
      },
      {
        "name": "SOCIAL_HUB",
        "class_name": "SocialHubDO"
      }
    ]
  },
  "migrations": [
    {
      "tag": "v1",
      "new_sqlite_classes": [
        "GameRoomDO",
        "WorldZoneDO",
        "SocialHubDO"
      ]
    }
  ],
  "vars": {
    "ENVIRONMENT": "production",
    "JWT_SECRET": "aether-serpent-eternal-coil-production-secret-key-32chars",
    "JWT_REFRESH_SECRET": "aether-serpent-refresh-secret-production-key-32chars",
    "TURNSTILE_SECRET_KEY": "1x0000000000000000000000000000000AA",
    "ALLOWED_ORIGINS": "*"
  }
}
```

---

## 3. How to Deploy to Production

To deploy updates to Cloudflare Workers from the command line:

```powershell
cd D:\AetherSerpent\Cloudflare
npm install
npx tsc --noEmit
npx wrangler deploy
```

Upon successful deployment, Wrangler outputs:
```text
Uploaded aetherserpent-backend
Deployed aetherserpent-backend triggers
  https://aetherserpent-backend.uttombarman55.workers.dev
Current Version ID: 0fe221be-a0ba-4482-89ca-a9fb6d18b500
```

---

## 4. Production Security & Secrets Checklist

- **Turnstile Verification:** Uses server-side validation against `https://challenges.cloudflare.com/turnstile/v0/siteverify`.
- **JWT Cryptography:** Employs native Web Crypto API (`crypto.subtle`) with HMAC-SHA256.
- **Client Security:** No private signing keys or database credentials are embedded inside the Unity client.
