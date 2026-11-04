# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6000.3.11f1 AR campus-navigation app (Android, with an iOS runtime guard). The navigation core is **`Assets/ARNavB9V2`**: outdoor GPS guidance that hands over to MultiSet VPS visual localization inside buildings B9 and B10. The single scene in the build is `Assets/ARNavB9V2/Scenes/B9NavigationV2.unity`.

The earlier hybrid indoor/outdoor system (`Assets/Code`, `Hybrid Navigation.unity`, the `HybridModeController` flow, many test scenes) was removed from this branch. It is preserved at the git tag `paper-b9-snapshot` (and branch `paper/merge-indoor-and-outdoor`) if anything has to be recovered.

## Build & Test Commands

Run from the repo root with the editor version in `ProjectSettings/ProjectVersion.txt`. On macOS the binary is `/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity`. Close the Unity editor first: a project can only be open once.

```bash
# EditMode tests
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile -

# PlayMode tests
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -logFile -
```

`-nographics` has no GPU, so tests that create a `RenderTexture` fail there (`ApiTransportTests`). Run those from the Test Runner window.

Tests: `Assets/ARNavB9V2/Tests` (assembly `ARNav.B9V2.EditorTests`: adaptive policy, completion, foundation, handover geometry, outdoor) and `Assets/Tests/Editor/EditMode` (API transport, router, and one `UiCanvas*EditModeTests` class per phase, driven through `PageHarness`). Test method naming: `Method_WhenCondition_ExpectedResult`.

## Architecture

### Navigation core (`Assets/ARNavB9V2`)

Three assemblies, all referencing packages only (never project code): `ARNav.B9V2.Runtime`, `ARNav.B9V2.Editor`, `ARNav.B9V2.EditorTests`. Keep new navigation code inside them.

| Folder | Role |
|---|---|
| `Runtime/Reliability` | `B9ReliableNavigationController` owns the state machine GPS → transition PDR → MultiSet VPS. GPS is frozen at the outer handover volume and VPS takes over after a successful pose. `B9TransitionPdrTracker` is the short-range dead reckoning used between the two. `B9HandoverGateParameters` are the only values adapted at runtime. |
| `Runtime/Vps` | `B9VpsTransitionController` runs the automatic outdoor → B9 handover. The MultiSet SDK is accessed through reflection (internal API, no public interface); this is intentional. `B9MultisetLocalizationUx` shows the official localization loader. |
| `Runtime/Handover` | `B9HandoverVolume`, `B9PortalAnchor`, `B9BuildingTransitionGeometry`: measured doorway pairs between campus space and the MultiSet map. |
| `Runtime/Outdoor` | GPS pose, route (`B9OutdoorRouteController`, `B9RouteRibbonRenderer`), destination anchors, minimap, `B10EntranceGroup`. World X is East and Z is North. |
| `Runtime/Indoor` | `B9IndoorPoseTracker` fuses AR visual-inertial motion with a step estimate and never moves the XR camera. Also route, minimap, step detector. |
| `Runtime/Adaptive` | On-device PPO policy ("HARMONY V6") that tunes the gate parameters once per second. Weights: `Resources/ARNavB9V2Policy/b9_handover_policy.json`. |
| `Runtime/Experiment` | Research logger, exporter and experiment profile for the paper (three-file CSV bundle). |
| `Runtime/Data`, `Data/Buildings` | ScriptableObject definitions: B9/B10 buildings, outdoor map calibration, destination catalog, indoor NavMesh data. |
| `Runtime/Scene` | Scene context components, `B9MapVisibility`, `B9IosRuntimeGuard`. |
| `Runtime/UI`, `UI` | `B9NavigationHud` (UI Toolkit) with `B9NavigationPanelSettings.asset` and its stylesheet `UI Login.uss`. |

MultiSet map data (`MAP_*`, `B10_MAP_*` prefabs) lives in `Assets/MultiSet/MapData`. The builders under `Tools/AR Navigation V2/Maps` create and validate the B9/B10 map prefabs, so prefer re-running them over hand edits.

A duplicate AR Session breaks tracking; `Tools/Fix AR` removes the duplicate one in `OutdoorEnvironment`.

### App shell (`Assets/Game`)

The whole app shell is Unity Canvas (uGUI); the UI Toolkit shell (`Assets/UI`: UXML, USS, controllers) was removed. One prefab per screen under `Assets/Game/Bundles/UI/<Name>UI/Prefabs`, shared prefabs (`AppCanvas`, `BottomNav`, `ConfirmSheet`, `BusyOverlay`, `StatusOverlay`, `PickerSheet`) and generated sprites in `Bundles/UI/Common`, scene `Bundles/Scenes/AppShell.unity`.

- `Scripts/Foundation/UI/NavigationManager.cs` is the router (global class): history stack, `Navigate`, `GoBack`, `NavigateFrom`, `ReturnTo`, `ResetTo`. `PageID.cs` lists the pages. `UiPage` is the base of every screen; `Common/` holds the shared widgets (namespace `Game.Foundation.UI`).
- `Scripts/Features/<Feature>`: `Auth` (login, forgot password, OTP, register), `Onboarding`, `Home` (`ArHudUI`, the overlay for the AR page), `Chat` (history, search, chat), `Settings` (settings, profile, change email/password, support, contact). Backend access is behind interfaces held in static holders (`AuthServices`, `ChatServices`, `AccountServices`) so tests swap them. `Scripts/Foundation/Net/ApiHelper.cs` and `Assets/RestClient` are the HTTP layer.
- The AppShell scene is not in the build yet: the AR page (`ArHudUI`) is a transparent overlay, but nothing loads `B9NavigationV2` underneath it or hides the UI Toolkit `B9NavigationHud`. That integration needs a device run.

### Editor tooling

- `Tools/AR Navigation V2/*` builders create and update the B9 scene step by step (foundation, outdoor route, VPS handover, indoor navigation, MultiSet UX, reliability, maps). `Step 1`, `Step 2` and `Sync MapB9 + POIs From HybridGPSMap` read the removed reference scene `Assets/Scenes/HybridGPSMap.unity`, so they only run after restoring it from `paper-b9-snapshot`.
- `Tools/UI Canvas/*` (`Assets/Editor/UiCanvas`) generates the sprites, icons, fonts and every screen prefab and the AppShell scene; `Run Phase N Setup` runs a stage and renders previews to `Logs/UiPreview`. Do not re-run an earlier phase unless that screen has to change: a rebuild renumbers every fileID in its prefabs. `UiCanvasBatch.BuildAll` is the batch-mode entry (no previews: they need a GPU).
- `Tools/Indoor/*` bakes the B9 NavMesh and fixes map mesh tags; `Tools/Fix AR/*` removes duplicate AR Sessions.
- `Assets/Editor/PixelPerfectOverlay.cs` overlays a design PNG on a uGUI Canvas in the Scene view (key `1`).

## Key External Packages

- `com.multiset.sdk`: MultiSet VPS visual localization.
- `com.unity.xr.arfoundation` / `com.unity.xr.arcore` 6.3.3: ARFoundation / ARCore.
- `com.unity.ai.navigation` 2.0.11: NavMesh runtime and baking.
- `com.unity.inputsystem` 1.19.0, `com.unity.render-pipelines.universal` 17.3.0 (URP), `com.unity.ugui` 2.0.0.
- `com.gamelovers.mcp-unity`: MCP bridge for editor integration.

## Coding Conventions

- C#, 4-space indentation, UTF-8.
- Types / methods / properties: `PascalCase`. Local variables / private fields: `camelCase`.
- One `MonoBehaviour` per file; filename must match class name.

## Commit Format

`<area>: <action>`, e.g. `b9-reliability: fix handover gate smoothing`, `ui-canvas: add login screen prefab`.

Keep commits atomic: code changes together with related scene/prefab/meta file updates. PRs require a summary, changed scenes/prefabs and test evidence (logs or screenshots). For UI changes include before/after screenshots.

## What Not To Edit

Do not commit or modify `Library/`, `Temp/`, or `Logs/`; they are generated by Unity and are gitignored. Always move assets together with their `.meta` file so GUID references survive.
