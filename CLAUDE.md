# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6000.0.44f1 AR navigation app for Android. Implements a **hybrid indoor/outdoor navigation system** using ARFoundation 6.0 + ARCore 6.0. The main scene is `Assets/Scenes/Hybrid Navigation.unity`.

## Build & Test Commands

Run from the repo root. `Unity.exe` must be on the system PATH.

```powershell
# EditMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -quit

# PlayMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -quit
```

Open interactively via Unity Hub with editor version `6000.0.44f1`.

Test files live in `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`. Test method naming: `Method_WhenCondition_ExpectedResult`.

## Architecture

### Hybrid Mode System

The app has three operational states — **Outdoor**, **Indoor**, and **Off** — orchestrated by `Assets/Code/HybridModeController.cs`.

- **Outdoor mode:** GPS + compass drives XR Origin positioning. ARFoundation plane detection is inactive. Navigation ribbon overlays the ground.
- **Indoor mode:** Visual localization (SLAM) drives tracking. NavMesh pathfinding routes between waypoints.
- **Transition:** Fade overlay hides the switch. Auto-switching uses configurable thresholds (GPS accuracy < 30m for 3s → switch outdoor; localization success for 2s → switch indoor).

**Critical design decision — XR rig detachment:** The outdoor XR rig is parented _outside_ `OutdoorEnvironment` at runtime so that disabling `IndoorEnvironment` / `OutdoorEnvironment` GameObjects doesn't destroy the shared camera session. `HybridModeController.detachOutdoorXrRigFromEnvironment` controls this.

**Audio enforcement:** `HybridModeController` enforces a single active `AudioListener` on `LateUpdate` — it mutes/unmutes `AudioSource` lists per mode. Do not add extra `AudioListener` components.

**Inspector tools:** `Assets/Editor/HybridModeControllerEditor.cs` adds Force Indoor / Force Outdoor / Force Off buttons visible in Play mode. `Assets/Editor/HybridModeBatchCheck.cs` runs a scripted transition test from the Unity menu.

### GPS Pipeline (`Assets/Code/GPSMarker.cs`)

Three-phase processing on each `Input.location` update:

1. **Quality filter** — rejects fixes below `maxAcceptableAccuracy` (default 30 m) or with stale timestamps.
2. **EMA smoothing + jump rejection** — exponential moving average; discards fixes > 50 m from last accepted position (`LastFixRejectedAsJump`).
3. **Dead reckoning** — projects position forward using compass heading + estimated velocity when GPS is lost > 3 s.

Coordinate conversion path: Lat/Lon/Alt → ECEF → ENU (East-North-Up, relative to a fixed reference origin). Both `GPSMarker` and `Assets/Code/Navigation/MapOrigin.cs` implement this conversion — prefer `MapOrigin` for static destination pins and `GPSMarker` for the live user position.

Navigation systems gate on `GPSMarker.HasRecentGoodFix` and `GPSMarker.LastFixRejectedAsJump`. Do not render paths when either condition fails.

### Navigation Rendering

Two parallel path-rendering paths exist:

| Script | Purpose |
|---|---|
| `Assets/Code/SetNavigation.cs` | Original NavMesh ribbon mesh (used in `ManScene`) |
| `Assets/Code/Navigation/ARPathFinder.cs` | AR-aware path finder used in hybrid scene |

`ARPathFinder` samples the NavMesh from the AR camera position (real-time body tracking), falls back to straight-line geometry if NavMesh sampling fails, and delegates mesh construction to `Assets/Code/Navigation/NavMeshPathRibbon.cs`.

`NavMeshPathRibbon` builds either a single-strip or three-strip ribbon (white borders + colored center) with optional arrow-head geometry and chevron texture tiling.

### Outdoor Navigation Stack (`Assets/Code/Navigation/`)

`SimpleGPSTracker.cs` streams live GPS, applies north-alignment via compass, snaps the position to the nearest walkable NavMesh point (8 m search radius), and stores a calibration offset in `PlayerPrefs` to correct systematic GPS bias.

`HybridOutdoorNavigationRoot.cs` keeps `ARPathFinder` running in both modes but toggles HUD canvases only when the mode is Outdoor.

### UI Architecture (`Assets/UI/Scripts/`)

Uses **Unity UI Toolkit** (`.uxml` / `.uss`). Controllers live in `Assets/UI/Scripts/Controller/`, service logic in `Assets/UI/Scripts/Service/`. One controller class per screen; screen routing is handled separately from controller logic.

### Key Scenes

| Scene | Focus |
|---|---|
| `Hybrid Navigation.unity` | Primary hybrid indoor/outdoor scene |
| `HybridGPSMap.unity` | Outdoor-only GPS map testing |
| `ManScene.unity` | Indoor AR / visual localization testing |

## Coding Conventions

- C#, 4-space indentation, UTF-8.
- Types / methods / properties: `PascalCase`. Local variables / private fields: `camelCase`.
- One `MonoBehaviour` per file; filename must match class name.

## Commit Format

`<area>: <action>` — e.g., `gps-marker: fix jump rejection threshold`, `hybrid-mode: add fade transition`.

Keep commits atomic: code changes together with related scene/prefab/meta file updates. PRs require summary, changed scenes/prefabs, and test evidence (logs or screenshots).

## What Not To Edit

Do not commit or modify `Library/`, `Temp/`, or `Logs/` — these are generated by Unity and are gitignored.
