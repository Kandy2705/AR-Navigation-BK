# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6000.0.44f1 AR navigation app for Android. Implements a **hybrid indoor/outdoor navigation system** using ARFoundation 6.0 + ARCore 6.0 with Multiset VPS for indoor visual localization. The main scene is `Assets/Scenes/Hybrid Navigation.unity`.

## Build & Test Commands

Run from the repo root. `Unity.exe` must be on the system PATH.

```powershell
# EditMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -quit

# PlayMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -quit
```

Open interactively via Unity Hub with editor version `6000.0.44f1`.

Test files live in `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`. Test method naming: `Method_WhenCondition_ExpectedResult`. New feature tests should prioritize navigation logic, API service parsing, and UI routing.

## Architecture

### Hybrid Mode System

The app has three operational states — **Outdoor**, **Indoor**, and **Off** — orchestrated by `Assets/Code/HybridModeController.cs`.

- **Outdoor mode:** GPS + compass drives XR Origin positioning. ARFoundation plane detection is inactive. Navigation ribbon overlays the ground.
- **Indoor mode:** Visual localization (SLAM via Multiset SDK) drives tracking. NavMesh pathfinding routes between waypoints.
- **Transition:** Fade overlay hides the switch. Auto-switching uses configurable thresholds (GPS accuracy < 30 m for 3 s → switch outdoor; localization success for 2 s → switch indoor).

**Critical design decision — XR rig detachment:** The outdoor XR rig is parented _outside_ `OutdoorEnvironment` at runtime so that disabling `IndoorEnvironment` / `OutdoorEnvironment` GameObjects doesn't destroy the shared camera session. `HybridModeController.detachOutdoorXrRigFromEnvironment` controls this.

**Audio enforcement:** `HybridModeController` enforces a single active `AudioListener` on `LateUpdate` — it mutes/unmutes `AudioSource` lists per mode. Do not add extra `AudioListener` components.

**Android permissions:** `HybridModeController` handles the Camera + Location permission flow with a timeout and UI feedback; do not request these permissions elsewhere.

**Inspector tools:** `Assets/Editor/HybridModeControllerEditor.cs` adds Force Indoor / Force Outdoor / Force Off buttons visible in Play mode. `Assets/Editor/HybridModeBatchCheck.cs` runs a scripted transition test from the Unity menu.

### GPS Pipeline

Two GPS tracker implementations exist — prefer the modern one for new work:

| Script | Use |
|---|---|
| `Assets/Code/Navigation/SimpleGPSTracker.cs` | **Current** — north-alignment via compass, NavMesh snapping (8 m radius), calibration offset in `PlayerPrefs` |
| `Assets/Code/GPSMarker.cs` | **Legacy** — also live; referenced by outdoor HUD and navigation gating |
| `Assets/Code/MockGPSMarker.cs` | **Optimized variant** — moves XROrigin instead of the map plane (O(1) vs O(N)); use in scenes with large tile counts |

`GPSMarker` three-phase processing: quality filter (rejects below `maxAcceptableAccuracy` = 30 m or stale timestamps) → EMA smoothing + jump rejection (discards fixes > 50 m from last accepted position, flagged as `LastFixRejectedAsJump`) → dead reckoning (projects forward at 1.4 m/s via compass when GPS lost > 3 s).

Coordinate conversion: Lat/Lon/Alt → ECEF → ENU (East-North-Up, relative to a fixed reference origin). Both `GPSMarker` and `Assets/Code/Navigation/MapOrigin.cs` implement this — prefer `MapOrigin` for static destination pins and `GPSMarker` for the live user position.

Navigation systems gate on `GPSMarker.HasRecentGoodFix` and `GPSMarker.LastFixRejectedAsJump`. Do not render paths when either condition fails.

### Navigation Rendering

Two parallel path-rendering paths exist:

| Script | Purpose |
|---|---|
| `Assets/Code/SetNavigation.cs` | Original NavMesh ribbon mesh (used in `ManScene`) |
| `Assets/Code/Navigation/ARPathFinder.cs` | AR-aware path finder used in hybrid scene |

`ARPathFinder` samples the NavMesh from the AR camera position, falls back to straight-line geometry when sampling fails, and delegates mesh construction to `Assets/Code/Navigation/NavMeshPathRibbon.cs`. It throttles path updates (0.5 s interval, 0.15 m delta) and uses two-pass sampling (3 m → 24 m radii).

`NavMeshPathRibbon` builds either a single-strip or three-strip ribbon (white borders + colored center) with optional arrow-head geometry and chevron texture tiling. Y is clamped to camera foot level (1.6 m eye-to-foot default).

`HybridOutdoorNavigationRoot.cs` keeps `ARPathFinder` running in both modes but toggles HUD canvases only when mode is Outdoor.

### Indoor Subsystem (`Assets/Code/Indoor/`)

Indoor mode uses Multiset SDK for visual localization. Key components:

- **`IndoorMapSwitcher.cs`** — switches between buildings (B9, B10) via reflection on the Multiset SDK (internal API, no public interface). This is intentional.
- **`BuildingRegistry.cs` / `BuildingSceneBindings.cs`** — maps `BuildingId` enum values to scene GameObjects.
- **`IndoorAutoEnterB9.cs`** — detects proximity to B9 and triggers indoor mode automatically.
- **`IndoorEntryConfirmController.cs`** — confirmation dialog before entering indoor mode.
- **`MultisetIndoorBootstrap.cs`** — fixes a `NullReferenceException` in the Multiset SDK at runtime; must remain active.
- **`NavigationControllerSetup.cs`** — adds `SphereCollider` and warps `NavMeshAgent` to satisfy Multiset SDK preconditions.

Do not remove `MultisetIndoorBootstrap` or `NavigationControllerSetup` — they paper over SDK bugs.

### GpsAR Subsystem (`Assets/Code/GpsAR/`)

VPS-integrated GPS anchoring for real-world POI placement:

- **`GpsArBootstrap.cs`** — initializes VPS + GPS anchoring session.
- **`AnchoredPOI.cs`** — a POI pinned to a real-world GPS coordinate via an AR anchor.
- **`POIAnchorService.cs`** — lifecycle manager for GPS-anchored POIs.

### POI System

- **`POI.cs`** — data container (id, name, description, type, collider ref, sign ref).
- **`POICollider.cs`** — trigger that fires when the user arrives at a POI.
- **`POISign.cs`** — 3D billboard label + clickable sign rendered in AR space.
- **`DestinationUI/BuildingDestinationListController.cs`** — hierarchical destination picker (buildings → POIs list) rendered with UI Toolkit.

### UI Architecture (`Assets/UI/Scripts/`)

Uses **Unity UI Toolkit** (`.uxml` / `.uss`). One controller class per screen.

- **`NavigationManager.cs`** — central router; fires `OnAREntered` / `OnARExited` events consumed by outdoor HUD controllers.
- **`PageFactory.cs`** — creates controller instances by screen ID.
- **`ControllerRouting.cs`** — wires controllers to routes at startup.
- Controllers: `Assets/UI/Scripts/Controller/` — one file per screen (login, register, profile, settings, history, chat, email/password change flows, onboarding).
- Services: `Assets/UI/Scripts/Service/` — stateless HTTP wrappers using `APIModel/APIHelper.cs`.
- `UIRouter.cs` is the legacy router — prefer `NavigationManager` for new screens.

### Key Scenes

| Scene | Focus |
|---|---|
| `Hybrid Navigation.unity` | Primary hybrid indoor/outdoor scene |
| `HybridGPSMap.unity` | Outdoor-only GPS map testing |
| `ManScene.unity` | Indoor AR / visual localization testing |

## Editor Tooling (`Assets/Editor/`)

31 editor scripts. Key ones to know:

| Script | Purpose |
|---|---|
| `HybridModeControllerEditor.cs` | Force Indoor/Outdoor/Off buttons in Play mode Inspector |
| `HybridModeBatchCheck.cs` | Scripted mode-transition test via Unity menu |
| `BuildReadinessCheck.cs` | Pre-build validation; run before Android builds |
| `IndoorBakeNavMeshes.cs` | Bakes NavMesh for all indoor floors |
| `NavMeshBatchDiagnose.cs` | Diagnoses NavMesh connectivity issues |
| `HybridArSessionAudit.cs` | Detects duplicate AR Session components (breaks tracking) |
| `SceneHierarchyDumper.cs` | Prints full hierarchy to console for debugging |
| `AutoWireReferences.cs` | Auto-assigns serialized fields by type/name conventions |

## Key External Packages

- **`com.multiset.sdk`** — Multiset VPS (visual localization); internal APIs accessed via reflection in `IndoorMapSwitcher`.
- **`com.unity.xr.arfoundation@6.0.6` + `com.unity.xr.arcore@6.0.6`** — ARFoundation / ARCore.
- **`com.unity.ai.navigation@2.0.6`** — NavMesh runtime baking.
- **`com.unity.render-pipelines.universal@17.0.4`** — URP rendering.
- **`com.gamelovers.mcp-unity`** — MCP-Unity bridge (editor integration).

## Coding Conventions

- C#, 4-space indentation, UTF-8.
- Types / methods / properties: `PascalCase`. Local variables / private fields: `camelCase`.
- One `MonoBehaviour` per file; filename must match class name.

## Commit Format

`<area>: <action>` — e.g., `gps-marker: fix jump rejection threshold`, `hybrid-mode: add fade transition`.

Keep commits atomic: code changes together with related scene/prefab/meta file updates. PRs require summary, changed scenes/prefabs, and test evidence (logs or screenshots). For UI changes include before/after screenshots.

## What Not To Edit

Do not commit or modify `Library/`, `Temp/`, or `Logs/` — these are generated by Unity and are gitignored.
