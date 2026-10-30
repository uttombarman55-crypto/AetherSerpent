# AetherSerpent_EternalCoil — Release Notes (v1.0.0)

**Release Date:** 2026-10-30  
**Engine:** Unity 6 (`6000.0.32f1`) + URP (`17.0.3`)  
**Backend:** Cloudflare Workers (`4.156.0`) with SQLite-backed Durable Objects  

---

## What's New in v1.0.0

### 1. AAA 3D Serpent Dynamics & Growth
- **50+ Vertebrae Verlet Dynamic Spine:** Realistic skeletal/hybrid physics following the head with natural organic lag and weight.
- **Metabolic Unfurl Growth Queue:** Smooth growth animation avoiding jarring popping artifacts.
- **Dynamic Cinemachine 3 Camera:** Smooth third-person orbital camera adapting distance between 8m and 45m+ as the serpent grows.
- **Iridescent Diamond Scales:** Runtime normal and roughness maps for hyper-realistic lighting.

### 2. Core Single-Player Experience
- **Vitals Loop:** Hunger, Health, Energy, and digestion dynamics.
- **Prey Ecosystem:** 4 distinct prey tiers with unique essence properties.
- **Celestial Arena & Reactive Biomes:** 4 interactive biomes with destructible pillars and terrain deformation.
- **3 Evolution Trees:** Titan (colossal & destructive), Celestial (winged & luminous), and Abyssal (toxic & dark).
- **Day-Night Cycle & Story Flashbacks:** Atmospheric lighting shifts and unlocked memory cutscenes.

### 3. Production Cloudflare Backend & SQLite Durable Objects
- **Live Deployed Worker:** Running at `https://aetherserpent-backend.uttombarman55.workers.dev`.
- **Durable Objects:**
  - `GameRoomDO`: 20Hz real-time simulation tick, WebSocket Hibernation, anti-cheat validation.
  - `SocialHubDO`: Global & weekly leaderboards, friends management, co-op parties, and cloud saves.
  - `WorldZoneDO`: Spatial open-world sharding foundation.
- **Security & Anti-Cheat:** Server-authoritative movement clamp, Turnstile bot protection, HMAC-SHA256 JWT auth with refresh token rotation.

### 4. Co-op Serpent Mode
- Two players command a single serpent simultaneously:
  - Player A controls head navigation, diving, and speed boosting.
  - Player B controls tail stabilization, body whip bursts, and trailing defense.

### 5. Extended Social, Save & Quality of Life Systems
- **Global & Weekly Leaderboards:** In-game rankings viewer (`L` key) with server-calculated ISO week boundaries.
- **Friends & Party System:** In-game friend lists and party invite codes.
- **Cinematic Photo Mode:** 6-DOF free camera with visual filters and high-res screenshot capture (`P` key).
- **Atomic Save System:** File-based atomic replacement (`.tmp` -> primary save) with SHA-256 integrity checksums and cloud-save sync readiness.
- **Settings & Pause Menu:** Audio sliders, graphics quality presets, and clean UI navigation.

