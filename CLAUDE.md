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

Tests: `Assets/ARNavB9V2/Tests` (assembly `ARNav.B9V2.EditorTests`: adaptive policy, completion, foundation, handover geometry, outdoor) and `Assets/Tests/Editor/EditMode` (API transport, page factory, UI Toolkit layout). Test method naming: `Method_WhenCondition_ExpectedResult`.

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

### App shell (`Assets/UI`, `Assets/RestClient`)

Login, register, profile, settings, history and chat screens built with Unity UI Toolkit (`.uxml` / `.uss`), one controller per screen. `NavigationManager` is the router, `PageFactory` creates controllers by `PageID`, and `Service/*` plus `APIModel/APIHelper.cs` wrap the REST API through `Assets/RestClient`. No scene in this branch hosts the shell yet; the AR page will be the B9 navigation scene.

The shell is being migrated to Unity Canvas (uGUI) on the `ui/canvas-migration` branch: one prefab per screen under `Assets/Game/Bundles/UI/<Name>UI`, shared widgets in `Assets/Game/Scripts/Foundation/UI/Common` (namespace `Game.Foundation.UI`). Follow that layout for new UI.

### Editor tooling

- `Tools/AR Navigation V2/*` builders create and update the B9 scene step by step (foundation, outdoor route, VPS handover, indoor navigation, MultiSet UX, reliability, maps). `Step 1`, `Step 2` and `Sync MapB9 + POIs From HybridGPSMap` read the removed reference scene `Assets/Scenes/HybridGPSMap.unity`, so they only run after restoring it from `paper-b9-snapshot`.
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
