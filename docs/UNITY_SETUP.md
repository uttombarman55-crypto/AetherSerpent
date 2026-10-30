# Unity Setup & Client Architecture Guide

**Project Name:** `AetherSerpent_EternalCoil`  
**Engine Version:** Unity 6 (`6000.0.32f1`)  
**Render Pipeline:** Universal Render Pipeline (URP `17.0.3`)  
**Color Space:** Linear (`activeColorSpace: 1`)  

---

## 1. Engine & Environment Prerequisites

1. **Unity 6 Editor:** Install Unity `6000.0.32f1` (or compatible Unity 6 LTS) via Unity Hub.
2. **Modules Required:**
   - Windows Build Support (Mono / IL2CPP)
   - Android Build Support (optional, for mobile touch targets)
   - WebGL Build Support (optional, for browser targets)
3. **Graphics API:** DirectX 12, DirectX 11, or Vulkan.

---

## 2. Project Directory Structure

```text
D:\AetherSerpent\
├── Assets/
│   ├── Art/                      # Textures, normal maps, skyboxes
│   ├── Audio/                    # Soundscapes, impacts, ambient audio
│   ├── Prefabs/                  # Preconfigured GameObjects & arena obstacles
│   ├── Resources/                # Runtime loadable assets
│   ├── Scenes/
│   │   ├── Boot.unity            # Subsystem discovery & initialization
│   │   ├── MainMenu.unity        # Main menu & mode selection
│   │   └── Gameplay.unity        # Celestial Sanctuary arena & 3D Serpent
│   ├── Scripts/
│   │   ├── Camera/               # Cinemachine 3 orbit & Photo Mode 6-DOF
│   │   ├── Core/                 # Game loop, vitals, bootloader, graphics config
│   │   ├── Environment/          # Biomes, day-night cycle, preys, reactive arena
│   │   ├── Input/                # New Input System reader & virtual touch HUD
│   │   ├── Network/              # Native WebSocket client, synchronizer, network HUD
│   │   ├── Save/                 # Atomic disk save & cloud save sync boundary
│   │   ├── Serpent/              # Verlet body dynamics, growth unfurl queue, scales
│   │   ├── Social/               # Leaderboard & friends/party manager
│   │   └── UI/                   # HUD, pause/settings, social tabs, flashback modal
│   └── Settings/                 # URP Render Pipeline assets, Input Actions asset
├── Cloudflare/                   # Cloudflare Workers + SQLite Durable Objects backend
├── Packages/                     # manifest.json & package locks
└── ProjectSettings/              # ProjectVersion.txt, InputSystem, Graphics settings
```

---

## 3. Step-by-Step Editor Startup & Play Mode

1. Open **Unity Hub**.
2. Click **Add** -> **Add project from disk** and choose `D:\AetherSerpent`.
3. Open the project in Unity 6. The Package Manager will automatically resolve:
   - `com.unity.render-pipelines.universal` (`17.0.3`)
   - `com.unity.cinemachine` (`3.1.2`)
   - `com.unity.inputsystem` (`1.11.2`)
   - `com.unity.ugui` (`2.0.0`)
4. In the Project window, expand `Assets/Scenes/`.
5. You can start testing from either:
   - **`Boot.unity`**: Executes hardware capability check, prewarms managers, and transitions to `MainMenu`.
   - **`Gameplay.unity`**: Directly drops you into the Sanctuary Arena with the 50-segment Serpent ready.
6. Press the **Play** button at the top of the Unity Editor.

---

## 4. Input & Control Mapping

| Action | Keyboard / Mouse | Gamepad | Mobile Touch |
| :--- | :--- | :--- | :--- |
| **Pitch & Yaw Steering** | `W` / `S` / `A` / `D` or Mouse Pointer | Left Analog Stick | Virtual Left Joystick |
| **Ascend / Descend** | `Space` (Ascend) / `Left Shift` (Descend) | Right Trigger / Left Trigger | Ascend / Descend Virtual Buttons |
| **Speed Boost (Sprint)** | `Left Shift` (Hold) | Right Bumper | Sprint Virtual Button |
| **Tail Whip Burst** *(Player B)* | `Space` / `F` | `X` Button / Right Trigger | Tail Action Virtual Button |
| **Photo Mode** | `P` | Click Right Stick | Photo Button |
| **Leaderboard & Social Hub** | `L` | View / Select Button | Leaderboard HUD Icon |
| **Co-op Matchmaking Modal** | `M` or `F1` | D-Pad Up | Network HUD Pill |
| **Pause Menu & Settings** | `Escape` | Start Button | Pause HUD Button |
| **Evolution Ability** | `E` | `Y` Button | Ability Virtual Button |

---

## 5. Serpent Physics & Growth Diagnostics

- The serpent body uses fixed-timestep Verlet dynamics (`SerpentPhysicsFollower.cs`).
- Newly consumed prey adds metabolic tokens to `SerpentGrowth.cs`.
- Vertebrae dynamically scale between 50 and 500+ segments without hitching via `SerpentSegmentPool.cs`.
- Camera automatically pulls back smoothly as the serpent grows.

