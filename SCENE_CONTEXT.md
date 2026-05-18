# HybridGPSMap Scene Context
_Last scanned: 2026-05-18_

## Project
Unity 6000.0.44f1 | ARFoundation 6 | ARCore 6 | Android
Scene: `Assets/Scenes/HybridGPSMap.unity`

---

## Top-level Hierarchy

```
HybridGPSMap (root)
├── MainScreen              [NavigationManager]
├── OutdoorEnvironment      (starts INACTIVE via DeactivateARMode)
├── IndoorEnvironment       (starts INACTIVE via DeactivateARMode)
├── HybridRuntime           [HybridModeController, HybridOutdoorNavigationRoot]
├── ARPageController        [ARPageController]
├── EventSystem
├── SharedARRig             (alwaysActiveRoots → managed by HybridModeController)
│   ├── AR Session          ← THE ONLY valid AR Session
│   └── XR Origin           [SimpleGPSTracker]
│       └── Camera Offset
│           └── Main Camera [Camera]
│               └── UserTrigger
└── DontDestroyOnLoad
```

---

## OutdoorEnvironment (detail)
```
OutdoorEnvironment
├── AR Session (INACTIVE)   ← DEACTIVATED (duplicate — SharedARRig handles AR Session)
├── BKField
├── BKMAP                   (school map with destinations: A3, B8, B9, B10)
├── Directional Light
├── MYPHUMAP                (outdoorOnlyVisualRoots — hidden in Indoor mode)
│   ├── Des1, Des2          ← TargetAnchors for outdoor navigation
├── Minimap Camera          [Camera]
├── NavigationManager       [ARPathFinder, LineRenderer]  ← outdoor path renderer
├── OutdoorNavigationUI
│   ├── GPS Accuracy Circle
│   ├── GPS Startup Overlay
│   ├── Minimap Canvas
│   └── Mobile Navigation HUD  [MobileNavigationHUD]
│       ├── Status Panel
│       └── Target Dropdown Panel
└── UI (INACTIVE)           ← legacy UI, unused
```

---

## IndoorEnvironment (detail)
```
IndoorEnvironment (INACTIVE at start)
├── UI Home Screen          ← ARPageObject for NavigationManager (indoor AR scene)
│   ├── XR Origin           [SimpleGPSTracker is on SharedARRig XR Origin, not here]
│   │   └── Camera Offset
│   │       └── ARCamera    [Camera]
│   │           ├── NavMeshAgent  [NavMeshAgent]  ← for SDK indoor nav
│   │           └── UserTrigger
│   ├── Canvas              (indoor UI: ChatBot, Navigation UI, ToastPanel)
│   ├── Directional Light
│   ├── Map Space           (MultiSet SDK maps: MapB10, POIs-B10, NavigationContent)
│   │   └── NavigationContent (INACTIVE)
│   │       ├── NavMesh     (indoor NavMesh with obstacles)
│   │       └── POIs        (B6-301..304 indoor POIs)
│   ├── MapLocalizationManager  [MapLocalizationManager] mapCode=MAP_9LME2PB7Y3EN
│   ├── MapMeshDownloader
│   ├── MultiSetSDKManager
│   ├── NavigationController    [NavigationController(SDK), LineRenderer]
│   │   ← Add NavigationControllerSetup here!
│   └── SimulationDataManager
├── UI Login/Signup/Welcome/etc. (INACTIVE)
└── UI Management
    └── UI Router
```

---

## Key Component Configurations

### HybridModeController (HybridRuntime)
| Field | Value | Notes |
|---|---|---|
| indoorEnvironment | IndoorEnvironment | ✅ |
| outdoorEnvironment | OutdoorEnvironment | ✅ |
| indoorVisualRoot | UI Home Screen | auto-added to indoorOnlyVisualRoots |
| alwaysActiveRoots | [SharedARRig] | managed lifecycle |
| outdoorXrRigRootOverride | SharedARRig | ✅ assigned |
| detachOutdoorXrRigFromEnvironment | **0 (false)** | no detach, uses alwaysActiveRoots |
| keepOutdoorActiveWhileIndoor | **1 (true)** | outdoor GPS runs in background during indoor |
| keepIndoorActiveWhileOutdoor | 0 (false) | indoor disabled during outdoor ✅ |
| disableIndoorXROriginDuplicates | **0 (false)** | ⚠️ 2 XR Origins in Indoor mode |
| disableIndoorARSessionDuplicates | **0 (false)** | OK (only 1 AR Session now) |
| activateInitialModeOnStart | **1 (true)** | ⚠️ FIXED in code: defers when NavigationManager present |
| autoSwitchEnabled | 0 | manual mode only |
| initialMode | 0 (Outdoor) | |
| createRuntimeModeSwitcher | 1 | Indoor/Outdoor/Off buttons |
| showRuntimeModeSwitcherOnlyInAR | 1 | hidden during MainScreen ✅ |
| maxGpsAccuracyMeters | 15.0 | |

### HybridOutdoorNavigationRoot (HybridRuntime)
| Field | Value | Notes |
|---|---|---|
| outdoorNavigationContentRoot | **null** | ⚠️ script exits early in Awake — does nothing |
| outdoorHudVisualSubtree | **null** | ⚠️ same |
| hybridModeController | null | auto-finds via FindFirstObjectByType |

### NavigationManager (MainScreen)
| Field | Value | Notes |
|---|---|---|
| ARPageObject | UI Home Screen | activates indoor AR scene |
| keepARPageDisabledOnStart | 1 (true) | ✅ |
| hybridModeController | null | uses FindFirstObjectByType |
| firstPage | 15 (Onboarding enum) | |

### ARPageController (ARPageController GO)
| Field | Value | Notes |
|---|---|---|
| nextObject | MainScreen | re-activates MainScreen when leaving AR |
| hybridModeController | null | uses FindFirstObjectByType |

### ARPathFinder (OutdoorEnvironment > NavigationManager)
| Field | Value | Notes |
|---|---|---|
| arCamera | null | ✅ auto-resolved via Camera.main in Start() + EnsureLiveArCamera() |
| xrOrigin | null | OK — arCamera takes precedence |
| navigationGpsTracker | null | GPS gate disabled (null tracker) |
| gateLineUntilNavigationGpsHealthy | 1 | enabled but gated by tracker=null |
| bypassNavigationGpsGateInEditor | 1 | bypassed in editor |
| prioritizePathVisibility | **1 (true)** | ✅ GPS gate bypassed, always tries to draw |
| showStraightLineFallbackWhenNavMeshFails | 1 | ✅ fallback to straight line |
| pathGeometryMode | 1 (NavMeshRoute) | tries NavMesh first |
| navMeshSampleRadius | 3.0m | |
| navMeshSampleRadiusExpanded | 24.0m | extended search |
| minMoveDistanceMeters | 0.5m | recalculate threshold |
| pathUpdateInterval | 0.5s | |

### SimpleGPSTracker (SharedARRig > XR Origin)
| Field | Value | Notes |
|---|---|---|
| xrOrigin | null | ⚠️ needs assignment to track camera position |
| arCamera | null | ⚠️ needs assignment |
| snapGpsPositionsToNavMesh | 1 | snaps GPS to NavMesh |
| navMeshSnapSampleRadiusMeters | 8.0m | |
| jumpRejectThresholdMeters | 50.0m | |
| maxNavigationDistanceFromMapOriginMeters | 250.0m | |
| accuracyThresholdMeters | 20.0m | |

### MobileNavigationHUD (OutdoorEnvironment > OutdoorNavigationUI > Mobile Navigation HUD)
| Field | Value | Notes |
|---|---|---|
| pathFinder | null | auto-resolved via ResolveReferences() |
| gpsTracker | null | auto-resolved via FindFirstObjectByType |
| targets | [null×4] | auto-resolved via FindObjectsByType<TargetAnchor>() |

### NavigationController SDK (IndoorEnvironment > UI Home Screen > NavigationController)
| Field | Value | Notes |
|---|---|---|
| agent | (NavMeshAgent on ARCamera child) | my parser shows null but IS assigned |
| augmentedSpace | Map Space | IS assigned (non-GO object) |
| **→ Add NavigationControllerSetup component here** | | fixes SphereCollider + NavMesh errors |

### MapLocalizationManager (IndoorEnvironment > UI Home Screen > MapLocalizationManager)
| Field | Value | Notes |
|---|---|---|
| arCamera | null | needs assignment for localization |
| mapSpace | Map Space | ✅ |
| mapOrMapsetCode | MAP_9LME2PB7Y3EN | |
| autoLocalize | 0 | manual trigger |

---

## AR Sessions & XR Origins

| Component | Location | Active when |
|---|---|---|
| AR Session | SharedARRig | Always (alwaysActiveRoots) |
| ~~AR Session~~ | ~~OutdoorEnvironment~~ | **DEACTIVATED** ✅ |
| XR Origin (outdoor) | SharedARRig > XR Origin | Always |
| XR Origin (indoor) | IndoorEnvironment > UI Home Screen > XR Origin | Indoor mode only |

⚠️ In Indoor mode: both XR Origins active simultaneously (disableIndoorXROriginDuplicates=false)

---

## NavMesh Situation
- `NavMeshData: 1` object in scene ✅ (NavMesh exists)
- Indoor NavMesh: `IndoorEnvironment > UI Home Screen > Map Space > NavigationContent > NavMesh`
- Outdoor NavMesh: in `OutdoorEnvironment` area (for GPS path snapping)
- NavMeshAgent: child of `IndoorEnvironment > ... > ARCamera`

---

## Code Changes Made (this session)

### Modified Files
| File | Change |
|---|---|
| `Assets/Code/HybridModeController.cs` | `Start()` defers AR activation when NavigationManager present |
| `Assets/UI/Scripts/Manager/NavigationManager.cs` | Added `OnAREntered` / `OnARExited` static events |
| `Assets/Code/Navigation/HybridOutdoorNavigationRoot.cs` | Subscribes to NavigationManager events; hides outdoor nav until AR entered |
| `Assets/UI/Scripts/UIRouter.cs` | Added `OnHomePageShown` event (legacy, unused now) |

### New Files Created
| File | Purpose |
|---|---|
| `Assets/Code/NavigationControllerSetup.cs` | Add to NavigationController GO — fixes SphereCollider + NavMesh Warp errors |
| `Assets/Code/CameraColliderAutoSetup.cs` | Alternative auto-setup (not needed if NavigationControllerSetup used) |
| `Assets/Editor/RemoveDuplicateARSession.cs` | Editor tool: Tools → Fix AR → Remove Duplicate AR Session |

---

## Outstanding Issues / TODO

| Priority | Issue | Fix |
|---|---|---|
| 🔴 | `OutdoorEnvironment > AR Session` active | Deactivate GO in Inspector (user doing this) |
| 🔴 | `NavigationControllerSetup` not added | Add component to NavigationController GO |
| 🟡 | `SimpleGPSTracker.xrOrigin` & `arCamera` null | Assign in Inspector: xrOrigin=XR Origin, arCamera=Main Camera |
| 🟡 | 2 XR Origins in Indoor mode | Enable `disableIndoorXROriginDuplicates` in HybridModeController Inspector |
| 🟡 | `HybridOutdoorNavigationRoot` does nothing | Assign `outdoorNavigationContentRoot` and `outdoorHudVisualSubtree` in Inspector |
| 🟢 | `NavigationManager.hybridModeController` null | Works via FindFirstObjectByType, but assign for performance |
| 🟢 | `ARPageController.hybridModeController` null | Same |

---

## Flow Summary

### Startup
1. `HybridModeController.Awake()` → `DeactivateARMode()` → everything disabled
2. `HybridModeController.Start()` → sees NavigationManager → skips `ApplyMode()` → keeps disabled
3. `NavigationManager.OnEnable()` → fires `OnARExited` → outdoor nav stays hidden
4. User sees MainScreen (onboarding/login)

### Enter AR
1. User taps AR button → `NavigationManager.SwitchObject()`
2. `ARPageObject (UI Home Screen).SetActive(true)` → indoor AR scene activates
3. `ApplyHybridInitialMode()` → `HybridModeController.ApplyInitialMode()` → `ApplyMode(Outdoor)`
4. Outdoor mode: OutdoorEnvironment.SetActive(true), IndoorEnvironment.SetActive(false), SharedARRig.SetActive(true)
5. `NavigationManager.OnAREntered` fires → HybridOutdoorNavigationRoot shows outdoor nav
6. MainScreen.SetActive(false)

### Exit AR
1. `ARPageController.SwitchObject()` → MainScreen.SetActive(true)
2. `NavigationManager.OnEnable()` → `OnARExited` fires → outdoor nav hides
3. `DeactivateHybridARMode()` → everything deactivated
4. `ARPageObject.SetActive(false)`
