# AetherSerpent: Eternal Coil

[![Unity 6](https://img.shields.io/badge/Unity-6000.0.32f1-blue.svg)](https://unity.com/)
[![URP](https://img.shields.io/badge/URP-17.0.3-green.svg)](https://unity.com/)
[![Cloudflare Workers](https://img.shields.io/badge/Cloudflare-Workers%20%2B%20SQLite%20DOs-orange.svg)](https://workers.cloudflare.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-purple.svg)](LICENSE)

A high-end, production-grade 3D mythical serpent game featuring realistic Verlet skeletal body physics, dynamic cinematic orbital framing, metabolic growth mechanics, a deep single-player ecosystem loop with 3 distinct evolution paths, and a serverless real-time multiplayer backend powered by **Cloudflare Workers** and **SQLite-backed Durable Objects** with **WebSocket Hibernation**.

---

## 🌟 Key Highlights

### 🐍 Hyper-Realistic 3D Serpent Dynamics
- **50+ Segment Verlet Physics Spine:** Heavy, organic muscular dynamics with natural momentum, turning radius scaling, and zero stretching artifacts.
- **Metabolic Digestion & Progressive Growth:** Segments unfurl smoothly via metabolic token queues rather than popping into existence.
- **Dynamic Cinemachine 3 Camera:** Smooth third-person orbital camera adapting follow distance from 8m up to 45m+ as the serpent grows.
- **Iridescent Diamond Scales:** Runtime normal and roughness scale maps providing metallic iridescence and subsurface scattering highlights.

### 🎮 Rich Single-Player Gameplay
- **Vitals Loop:** Hunger, Energy, and Health dynamics with interactive HUD feedback.
- **Prey Ecosystem:** 4 distinct prey tiers with unique essence properties.
- **Day-Night Cycle & 4 Reactive Biomes:** Luminous Sanctuary, Volcanic Crags, Abyssal Depths, and Verdant Canopy.
- **3 Evolution Trees:**
  - **Titan:** Colossal destructive power, ground-cracking weight, kinetic charge.
  - **Celestial:** Luminescent wings, aerial gliding, radiant energy burst.
  - **Abyssal:** Multi-headed phantom apparitions, toxic trails, venom spikes.
- **Story Flashbacks & Death Flow:** Atmospheric memory flashback triggers and graceful respawn / evolution flow.

### 🌐 Cloudflare Workers + SQLite Durable Objects Backend
- **Live Edge Deployment:** Hosted at `https://aetherserpent-backend.uttombarman55.workers.dev`.
- **Durable Objects Architecture:**
  - `GameRoomDO`: Real-time 20Hz simulation tick, WebSocket Hibernation, server-authoritative validation.
  - `SocialHubDO`: SQLite-backed global & weekly leaderboards, friends management, co-op parties, and cloud saves.
  - `WorldZoneDO`: Spatial open-world sharding foundation.
- **Enterprise Security:** Cloudflare Turnstile bot verification, Web Crypto HMAC-SHA256 JWT auth with refresh token rotation, IP rate limiting, and anti-cheat speed/teleport validation.

### 🤝 Dual-Player Co-op Serpent Mode
- Two players command a single physical serpent simultaneously:
  - **Player A (Head):** Directional steering, ascending, diving, and propulsion boost.
  - **Player B (Tail):** Authoritative body following, stabilization damping, and tail-whip bursts.

### 📸 Polish & Quality-of-Life Subsystems
- **Cinematic Photo Mode:** 6-DOF free camera with FOV zoom, color grading filters, and high-res screenshot capture (`P` key).
- **Atomic Save System:** File-based atomic write + replace with SHA-256 integrity checksums and cloud-save sync readiness.
- **Settings & Pause Menu:** Master/Music/SFX volume sliders, graphics fidelity presets (Ultra, High, Balanced), and pause workflow (`Escape`).
- **Social & Leaderboards Hub:** In-game ranking tabs (`L` key) calculated via server UTC Monday time boundaries.

---

## 🚀 Quick Start Guide

### 1. Unity Client
1. Open **Unity 6** (`6000.0.32f1`).
2. Add and open `D:\AetherSerpent`.
3. Open `Assets/Scenes/Gameplay.unity` (or `Boot.unity`).
4. Click **Play**!

### 2. Controls Quick Reference
- **W / A / S / D:** Steer serpent / Move Photo Mode camera
- **Space:** Ascend / Tail whip (Player B) / Capture photo (Photo Mode)
- **Left Shift:** Speed Boost (Hold) / Descend
- **P:** Toggle Cinematic Photo Mode
- **L:** Open Global & Weekly Leaderboards & Social Hub
- **M or F1:** Open Co-op Matchmaking & Role Selector
- **Escape:** Pause Menu & Settings

---

## 📚 Complete Documentation

- [Unity Setup & Architecture Guide](docs/UNITY_SETUP.md)
- [Cloudflare Deployment & Backend Guide](docs/CLOUDFLARE_DEPLOYMENT.md)
- [API & WebSocket Protocol Reference](docs/API_REFERENCE.md)
- [Multiplayer & Co-op Testing Guide](docs/MULTIPLAYER_TESTING.md)
- [Evidence-Based Release Audit](docs/FINAL_RELEASE_AUDIT.md)
- [Release Notes (v1.0.0)](docs/RELEASE_NOTES.md)

---

## 📄 License
MIT License. Developed for the AetherSerpent project.
