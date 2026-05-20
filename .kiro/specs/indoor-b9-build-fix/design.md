# Indoor B9 Build Fix — Bugfix Design

## Overview

Bản fix nhắm 4 hành vi mong muốn (2.1–2.4) với scope **chỉ B9** trên build APK. Nguyên tắc:

- **Không động Outdoor**: zero edit `ARPathFinder`, `MobileNavigationHUD`, `GPSMarker`, `OutdoorEnvironment`; mọi code mới guard `HybridModeController.CurrentMode == Indoor`.
- **Editor giữ mesh tím** để verify align (`Application.isEditor == true` ⇒ no-op trên 2.3).
- **Giữ pattern reflection** với Multiset SDK (`MapLocalizationManager`, `MapMeshHandler`) — không add `using Multiset.*`, không reference DLL.
- **Tối thiểu sửa `HybridModeController`**: chỉ thêm 1 public read-only API `GetActiveARCamera()` để observer ngoài lấy camera đúng thread-safe; không đổi flow `ApplyMode`.

## Glossary

- **C (Bug Condition)**: input trigger bug — Indoor mode active mà thiếu 1 trong 4 đảm bảo (mã VPS, single building active, mesh disabled trên APK, single MainCamera tag).
- **P (Property)**: hành vi đúng mỗi sub-condition (xem mục Correctness Properties).
- **Preservation**: mọi flow Outdoor + Editor mesh visualization + semantics `IndoorMapSwitcher.SwitchTo` không đổi.
- **HybridModeController**: `Assets/Code/HybridModeController.cs`. Public API hiện có: `ForceIndoor()`, `ForceOutdoor()`, `CurrentMode`, `OnLocalizationSuccess()`, `OnLocalizationFailure()`. **Không có** event `OnModeChanged`.
- **IndoorMapSwitcher**: `Assets/Code/Indoor/IndoorMapSwitcher.cs`. `SwitchTo(BuildingId)` đã làm 3 việc: tắt building roots khác, set `mapOrMapsetCode` + `localizationType` qua reflection, `ForceIndoor()` (nhưng chỉ nếu `forceIndoorModeOnSwitch == true`).
- **IndoorAutoEnterB9 (NEW)**: component mới — observer poll `CurrentMode`, chịu trách nhiệm cho cả 2.1, 2.3, 2.4.
- **MultisetIndoorBootstrap**: poll loop reflection-patch `NavigationController.ARCamera` qua `Camera.main`. Fix sẽ cho component này lấy camera từ `HybridModeController.GetActiveARCamera()` thay vì `Camera.main` thuần.

---

## 1. Architecture Overview

### Flow trước fix (defective)

```
User bấm "Indoor" trên runtime mode switcher (do HybridModeController tạo)
  └─> HybridModeController.ForceIndoor()
        └─> RequestModeWithPermissions(Indoor) → ApplyMode(Indoor)
              ├─ SetEnvironmentActive: indoorEnvironment.SetActive(true)
              │                         outdoorEnvironment.SetActive(false)
              │                         (MapB9 + MapB10 cùng activeSelf=true do scene-default)
              ├─ ApplyMainCameraTag: tag indoor ARCamera = MainCamera
              │  (nhưng không re-verify sau LocalizationSuccess)
              └─ SetModePresentation
                                                    [BUG: SwitchTo(B9) chưa ai gọi]
                                                    [BUG: MapB10 vẫn active]
SDK chạy localize:
  MapLocalizationManager.mapOrMapsetCode == "<scene default, có thể B10>"
    → cloud trả pose theo map sai
    → POI list (B9 + B10 trộn) hiển thị sai khoảng cách
  OnLocalizationSuccess fire
    → MapMeshHandler.meshVisualizationOption == EnableVisualization
    → mesh tím phủ camera feed (đúng trong Editor, sai trên APK)

MultisetIndoorBootstrap (poll):
  Camera.main → có thể outdated (race với ApplyMainCameraTag)
    → patch NavigationController.ARCamera trỏ camera sai
```

### Flow sau fix

```
User bấm "Indoor"
  └─> HybridModeController.ForceIndoor()
        └─> ApplyMode(Indoor) [KHÔNG ĐỔI]

[NEW] IndoorAutoEnterB9 (observer polling CurrentMode):
  Outdoor → Indoor edge detected
    ├─ (2.1) gọi IndoorMapSwitcher.SwitchTo(B9)
    │        → MapB9 active, MapB10 + others off
    │        → mapOrMapsetCode = "MAP_9LME2PB7Y3EN", localizationType = Map
    │        (đảm bảo 2.1 + 2.2 trước khi cloud localize)
    └─ subscribe OnLocalizationSuccess (UnityEvent inspector)
       hoặc poll localizationGood flag

[NEW] IndoorAutoEnterB9 sau LocalizationSuccess:
  ├─ (2.3) IF !Application.isEditor:
  │         reflection set MapMeshHandler.meshVisualizationOption = DisableVisualization
  └─ (2.4) verify single MainCamera tag = indoor ARCamera
            → nếu mismatch, gọi HybridModeController.GetActiveARCamera() (NEW API)
            → re-tag

MultisetIndoorBootstrap (đã sửa):
  Camera.main thay bằng HybridModeController.GetActiveARCamera() (fallback Camera.main)
    → patch NavigationController.ARCamera đảm bảo trùng indoor ARCamera
```

Toàn bộ logic mới sống trong `IndoorAutoEnterB9` + 1 public API `GetActiveARCamera()` trên `HybridModeController`. Không có method nào trong `HybridModeController.ApplyMode` được sửa.

---

## 2. Solution per Expected Behavior

### 2.1 Auto gọi `IndoorMapSwitcher.SwitchTo(B9)` khi vào Indoor

**Hook chọn:** component mới `IndoorAutoEnterB9` poll `HybridModeController.CurrentMode` ở `Update`, detect rising edge `Outdoor → Indoor` (hoặc `Transition → Indoor`).

Lý do **không** dùng `event`:
- `HybridModeController` hiện không expose event; thêm event là edit thừa và cross scope (user yêu cầu tối thiểu hóa edit `HybridModeController`).
- `OnModeChanged` event sẽ fire **trong** `ApplyMode` — nếu observer gọi `SwitchTo(B9)` ở callback, `SwitchTo` sẽ gọi `ForceIndoor()` lần nữa (re-entrancy) — phải thêm guard. Polling từ `Update` đảm bảo `ApplyMode` đã hoàn tất 1 frame trước.

Lý do **không** dùng decorator pattern (override `ForceIndoor`):
- `ForceIndoor()` không phải virtual.
- Sửa `ForceIndoor()` body chèn `SwitchTo(B9)` đụng tới `HybridModeController` (vi phạm yêu cầu tối thiểu hóa).

**Implementation outline:**

```csharp
// IndoorAutoEnterB9.Update (pseudocode)
void Update()
{
    if (hybridModeController == null) return;
    var mode = hybridModeController.CurrentMode;

    if (mode == HybridMode.Indoor && _lastMode != HybridMode.Indoor)
    {
        // Rising edge: Outdoor/Transition → Indoor
        TriggerSwitchToB9();        // 2.1 + 2.2
        _localizeSuccessHandled = false;
    }

    // Sau localize success: 2.3 (mesh) + 2.4 (camera tag)
    if (mode == HybridMode.Indoor &&
        !_localizeSuccessHandled &&
        IsLocalizationGood())       // poll qua reflection field localizationGood
    {
        DisableMeshVizOnBuild();    // 2.3
        ReinforceMainCameraTag();   // 2.4
        _localizeSuccessHandled = true;
    }

    _lastMode = mode;
}
```

**Guard mode:** mọi nhánh fix bọc `if (CurrentMode != Indoor) return;` ngay đầu helper. Outdoor không bao giờ chạm code mới.

### 2.2 Force only `MapB9` active

`IndoorMapSwitcher.SwitchTo(B9)` đã có vòng lặp tắt mọi `b.buildingRoot` không phải target. Fix 2.2 = **đảm bảo `SwitchTo(B9)` được gọi qua flow 2.1**, không sửa `IndoorMapSwitcher`.

Edge case: `SwitchTo` cần `BuildingSceneBindings` đã reference `MapB10` (không chỉ `MapB9`). Bind list này thuộc scene asset, không thuộc scope code fix — design assumption: scene đã có entries cho cả B9 + B10 (đã verified trong `BuildingRegistry.asset`).

Validation: P1 + P2 (Correctness Properties) cover.

### 2.3 Disable `MapMeshHandler.meshVisualizationOption` trên build sau LocalizationSuccess

**Verify enum (đã grep, không đoán):**
- `INDOOR_OUTDOOR_ARCHITECTURE.md` line 248: `meshVisualizationOption: EnableVisualization / DisableVisualization`.
- `Assets/Editor/IndoorSetupDiagnostic.cs` line 120 đọc field qua `GetField("meshVisualizationOption")` và `ToString()` — confirm field name + enum-typed.

**Reflection write pattern:**

```csharp
private void DisableMeshVizOnBuild()
{
    if (Application.isEditor) return;          // 3.2 preservation
    if (mapMeshHandler == null)
    {
        mapMeshHandler = FindFirstObjectByType<MonoBehaviour>(...);  // discovery by type name
        if (mapMeshHandler == null) return;
    }

    var t = mapMeshHandler.GetType();
    var vizField = t.GetField("meshVisualizationOption");
    if (vizField == null || !vizField.FieldType.IsEnum) return;

    object disable;
    try { disable = System.Enum.Parse(vizField.FieldType, "DisableVisualization"); }
    catch
    {
        // Fallback: log enum names, không crash. Giữ pattern reflection an toàn nếu SDK đổi tên.
        Debug.LogWarning("[IndoorAutoEnterB9] DisableVisualization not found on " +
                         vizField.FieldType.Name + "; values: " +
                         string.Join(",", System.Enum.GetNames(vizField.FieldType)));
        return;
    }

    vizField.SetValue(mapMeshHandler, disable);
}
```

`MapMeshHandler` instance được SDK tạo runtime (sau `OnLocalizationSuccess`). Component mới poll cho đến khi tìm thấy. Không assign trước được trong inspector.

### 2.4 Force unique `MainCamera` tag = indoor ARCamera

**Phân tích thứ tự call trong `ApplyMode(Indoor)`** (xem `HybridModeController.cs:378-410`):

1. `SetEnvironmentActive(Indoor)` — `indoorEnvironment.SetActive(true)`, outdoor off, indoor ARSession enable.
2. `ApplyMainCameraTag(Indoor)` — chọn `indoorMainCamera` (hoặc tìm `ARCamera` trong indoor hierarchy), clear MainCamera tag trên outdoor + detached XR rig + indoor, rồi set `preferred.tag = "MainCamera"`.
3. `RebindOutdoorNavigationCameras(Indoor)` — early-return vì mode != Outdoor.
4. `SetModePresentation(Indoor)`.

Vấn đề: `ApplyMainCameraTag` chạy đúng **frame `ApplyMode`**, nhưng:
- Một số camera con (vd outdoor camera tham chiếu trong asset bundle, hoặc minimap camera quên skip) có thể **giữ tag `MainCamera`** sau khi indoor environment kích hoạt. `ClearMainCameraTag` chỉ duyệt `indoorEnvironment`, `outdoorEnvironment`, `_detachedOutdoorXrRigRoot`, `alwaysActiveRoots` — bỏ qua camera ở scene root khác.
- `Camera.main` cache của Unity đôi khi không refresh ngay frame ấy → `MultisetIndoorBootstrap.PatchNavigationController` trỏ về camera sai nếu poll trùng frame.

**Fix:** sau `LocalizationSuccess` (đã ổn định 1+ frame, indoor environment đã render), `IndoorAutoEnterB9.ReinforceMainCameraTag` thực thi:

```csharp
private void ReinforceMainCameraTag()
{
    var indoorCam = hybridModeController.GetActiveARCamera();   // NEW API
    if (indoorCam == null) return;

    // Quét toàn scene, clear MainCamera trừ indoorCam
    foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
    {
        if (cam == null) continue;
        if (cam == indoorCam) continue;
        if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";
    }
    if (!indoorCam.CompareTag("MainCamera")) indoorCam.tag = "MainCamera";
}
```

**Public API mới trên `HybridModeController` (1 method, read-only):**

```csharp
public Camera GetActiveARCamera()
{
    return currentMode == HybridMode.Indoor ? indoorMainCamera : outdoorMainCamera;
}
```

Đây là **chỉ thay đổi** trên `HybridModeController` — read-only accessor cho field `[SerializeField] private Camera indoorMainCamera/outdoorMainCamera` đã tồn tại. Không sửa `ApplyMode`, không thêm event, không thêm field.

`MultisetIndoorBootstrap` cũng update để ưu tiên `GetActiveARCamera()`:

```csharp
// MultisetIndoorBootstrap.EnsureCameraCollider / PatchNavigationController
var cam = hybridModeController != null
            ? hybridModeController.GetActiveARCamera() ?? Camera.main
            : Camera.main;
```

Edge case: nếu `hybridModeController == null` (scene chưa load đầy đủ), fallback về `Camera.main` — preserve hành vi hiện tại.

---

## 3. Correctness Properties (PBT)

Property 1: Bug Condition — SwitchTo(B9) ép single building active

_For any_ frame `f` sau khi `IndoorAutoEnterB9` detect rising edge `→ Indoor` (xảy ra trong window `[f₀, f₀+1]`), hệ thống SHALL có `MapB9.activeSelf == true` AND `MapB10.activeSelf == false` (và mọi `BuildingSceneBindings` entry khác `b.buildingRoot.activeSelf == false`).

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition — Localization code = B9

_For any_ frame `f` sau khi `IndoorAutoEnterB9` detect rising edge `→ Indoor` (window `[f₀, f₀+1]`), hệ thống SHALL có `MapLocalizationManager.mapOrMapsetCode == "MAP_9LME2PB7Y3EN"` AND `MapLocalizationManager.localizationType == Map` (enum value name = `"Map"`).

**Validates: Requirements 2.1**

Property 3: Bug Condition — Mesh disabled on APK

_For any_ frame `f` sau khi `LocalizationSuccess` đã fire AND `Application.isEditor == false` AND `MapMeshHandler` instance tồn tại trong scene, hệ thống SHALL có `MapMeshHandler.meshVisualizationOption == DisableVisualization` (enum value name = `"DisableVisualization"`).

**Validates: Requirements 2.3**

Property 4: Bug Condition — Single MainCamera = indoor ARCamera

_For any_ frame `f` sau khi `LocalizationSuccess` đã fire AND `CurrentMode == Indoor`, hệ thống SHALL thoả: `count({ c ∈ Camera.allCameras : c.tag == "MainCamera" ∧ c.gameObject.activeInHierarchy }) == 1` AND camera đó là descendant của `indoorEnvironment` (hoặc `== indoorMainCamera` field reference).

**Validates: Requirements 2.4**

Property 5: Preservation — Outdoor untouched

_For any_ input/scene mà `HybridModeController.CurrentMode == Outdoor`, state của `OutdoorEnvironment` subtree (tập `{ activeSelf, transform.position/rotation, ARPathFinder.line, MobileNavigationHUD.text, GPSMarker.transform, Camera.main }`) SHALL bằng nhau giữa pre-fix code và post-fix code (bitwise/observable equality on tracked fields).

**Validates: Requirements 3.1, 3.4, 3.5, 3.6**

---

## 4. File Changes Plan

### 4.1 File mới

**`Assets/Code/Indoor/IndoorAutoEnterB9.cs`** (tách biệt, scope B9 only)

- `[SerializeField] HybridModeController hybridModeController;`
- `[SerializeField] IndoorMapSwitcher indoorMapSwitcher;`
- `[SerializeField] BuildingId defaultBuilding = BuildingId.B9;` (cho phép đổi target sau)
- `[SerializeField] MonoBehaviour mapMeshHandler;` (optional; nếu null sẽ runtime discovery)
- `[SerializeField] bool verboseLog = true;`
- State: `_lastMode`, `_localizeSuccessHandled`, `_switchToCalled`.
- Methods: `Update`, `TriggerSwitchToB9`, `IsLocalizationGood` (reflection đọc `localizationGood` private field hoặc dùng public surrogate), `DisableMeshVizOnBuild`, `ReinforceMainCameraTag`, `DiscoverMapMeshHandler` (poll FindObjects với type name match `"MapMeshHandler"`).
- Guard `if (hybridModeController.CurrentMode != HybridMode.Indoor) return;` đầu mỗi helper public.

Không tạo `.meta` thủ công — Unity Editor sẽ generate khi reload domain.

### 4.2 File sửa (tối thiểu)

**`Assets/Code/HybridModeController.cs`** — thêm **1** public method, **0** field mới, **0** edit logic:

```csharp
/// <summary>
/// Read-only accessor cho camera đang được present theo CurrentMode.
/// Giúp observer ngoài (vd IndoorAutoEnterB9, MultisetIndoorBootstrap) tránh
/// race với Camera.main cache khi ApplyMainCameraTag vừa chạy.
/// </summary>
public Camera GetActiveARCamera()
{
    return currentMode == HybridMode.Indoor ? indoorMainCamera : outdoorMainCamera;
}
```

Vị trí: ngay sau `public HybridMode CurrentMode => currentMode;` (line ~145).

**`Assets/Code/MultisetIndoorBootstrap.cs`** — thêm `[SerializeField] HybridModeController hybridModeController;` và đổi 2 chỗ đọc `Camera.main`:

- `EnsureCameraCollider` line ~89: `var cam = ResolveCamera();`
- `PatchNavigationController` line ~113: `var cam = ResolveCamera();`

```csharp
private Camera ResolveCamera()
{
    if (hybridModeController != null)
    {
        var c = hybridModeController.GetActiveARCamera();
        if (c != null) return c;
    }
    return Camera.main;
}
```

**Không edit nào** trên: `IndoorMapSwitcher`, `BuildingSceneBindings`, `BuildingRegistry`, `ARPathFinder`, `MobileNavigationHUD`, `GPSMarker`, `OutdoorEnvironment` bất kỳ thành phần nào.

### 4.3 Scene wiring (manual hoặc Editor script sau)

Trong scene `HybridGPSMap.unity`:
- Thêm GameObject `Indoor Auto Enter B9` (sibling của `Indoor Bootstrap`), gắn `IndoorAutoEnterB9` component, drag-drop `HybridModeController` + `IndoorMapSwitcher` vào inspector.
- Trên `Indoor Bootstrap`, drag `HybridModeController` vào field mới của `MultisetIndoorBootstrap`.

Auto-wiring có thể thêm vào `HybridGPSMapPipeline` editor script ở Tasks phase nếu cần.

---

## 5. Test Plan Summary

### EditMode tests — `Assets/Tests/EditMode/IndoorB9BuildFixTests.cs`

| Test | Property | Approach |
|------|----------|----------|
| `AutoEnter_OnIndoorRisingEdge_CallsSwitchToB9` | P1, P2 | Mock `HybridModeController.CurrentMode` (test double inheriting), mock `IndoorMapSwitcher` ghi nhận arg. Tick `Update` 1 lần trước rising edge, 1 lần sau → assert `SwitchTo(B9)` invoked đúng 1 lần. |
| `SwitchToB9_ForcesSingleBuildingActive` | P1 | Setup test scene với 2 fake `buildingRoot` (cả 2 `activeSelf=true`) trong `BuildingSceneBindings`. Gọi `SwitchTo(B9)` → assert `MapB9.activeSelf=true`, `MapB10.activeSelf=false`. |
| `SwitchToB9_SetsLocalizationCodeViaReflection` | P2 | Tạo stub `MapLocalizationManager` (MonoBehaviour với 2 public field `mapOrMapsetCode`, `localizationType : SomeEnum`). Gọi `SwitchTo(B9)` → assert field values. |
| `DisableMeshViz_OnEditor_NoOp` | P3 (preservation 3.2) | `Application.isEditor == true` (mặc định trong EditMode test runner). Stub `MapMeshHandler` với `meshVisualizationOption=EnableVisualization`. Tick `IndoorAutoEnterB9.DisableMeshVizOnBuild` → assert giá trị **không đổi**. |
| `DisableMeshViz_OnBuildSimulated_SetsDisable` | P3 | Inject test wrapper bypass `Application.isEditor` (qua `internal static Func<bool> isEditorOverride`). Stub `MapMeshHandler` → assert field = `DisableVisualization` enum value. |
| `ReinforceMainCameraTag_ClearsExtraMainCameras` | P4 | Tạo 3 GameObject với `Camera` component, all tag `MainCamera`, gán `indoorMainCamera` field tới 1 cụ thể. Gọi `ReinforceMainCameraTag` → assert đúng 1 camera giữ tag `MainCamera`. |
| `OutdoorMode_NoIndoorCodePath_RunsZeroTimes` | P5 | Set `CurrentMode=Outdoor` → tick `Update` N lần → assert `IndoorMapSwitcher.SwitchTo` không invoked, `mapMeshHandler.meshVisualizationOption` không đổi, không có camera nào bị retag. |

### PlayMode tests — `Assets/Tests/PlayMode/IndoorB9IntegrationTests.cs`

| Test | Property | Approach |
|------|----------|----------|
| `OutdoorToIndoor_LocalizationSuccess_DisablesMeshOnBuild` | P3 | Load scene `HybridGPSMap`, force `Application.isEditor` simulator off (skip nếu Unity không cho), call `ForceIndoor()`, raise `OnLocalizationSuccess()` qua public method, yield 2 frames → assert `MapMeshHandler.meshVisualizationOption == DisableVisualization`. (Test có thể `[Explicit]` chỉ chạy trên build runner.) |
| `OutdoorToIndoor_PostLocalize_SingleMainCamera` | P4 | Load scene, `ForceIndoor()`, fire `OnLocalizationSuccess`, yield 2 frames → enumerate `Camera.allCameras` filter `tag == MainCamera && activeInHierarchy` → assert count == 1 AND camera là descendant `indoorEnvironment`. |
| `Outdoor_FullSession_NoIndoorTouches` | P5 | Load scene, `ForceOutdoor()`, simulate 5s GPS update → snapshot `OutdoorEnvironment` state (transform tree, ARPathFinder line points, HUD text) → enable `IndoorAutoEnterB9` component → tick thêm 5s → assert snapshot equality. |

### Property mapping summary

- P1 ⇔ EditMode: `AutoEnter_OnIndoorRisingEdge_CallsSwitchToB9`, `SwitchToB9_ForcesSingleBuildingActive`
- P2 ⇔ EditMode: `SwitchToB9_SetsLocalizationCodeViaReflection`
- P3 ⇔ EditMode: `DisableMeshViz_OnEditor_NoOp`, `DisableMeshViz_OnBuildSimulated_SetsDisable`; PlayMode: `OutdoorToIndoor_LocalizationSuccess_DisablesMeshOnBuild`
- P4 ⇔ EditMode: `ReinforceMainCameraTag_ClearsExtraMainCameras`; PlayMode: `OutdoorToIndoor_PostLocalize_SingleMainCamera`
- P5 ⇔ EditMode: `OutdoorMode_NoIndoorCodePath_RunsZeroTimes`; PlayMode: `Outdoor_FullSession_NoIndoorTouches`

### Exploratory checks (chạy trên unfixed code trước khi fix)

1. `OutdoorToIndoor_NoSwitchTo_LeavesScenesDefault` — verify P1/P2 fail trước fix.
2. `OutdoorToIndoor_OnAPK_MeshVizRemainsEnable` — verify P3 fail trên APK build (cần device, có thể skip trong CI).
3. `OutdoorToIndoor_DuplicateMainCameraTag` — log `Camera.allCameras` sau `ForceIndoor` để confirm có > 1 hoặc indoor camera không có tag.

Phase Tasks sẽ schedule các test này theo thứ tự: exploratory (fail expected) → fix → P1–P4 pass → P5 regression check.

---

**Stop. Awaiting design review.**
