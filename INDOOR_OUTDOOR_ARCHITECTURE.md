# Kiến trúc Indoor/Outdoor Navigation — TestARMultiSet

## Tổng quan

Ứng dụng AR navigation kết hợp 2 chế độ:
- **Outdoor**: dẫn đường ngoài trời bằng GPS (đến tòa nhà)
- **Indoor**: dẫn đường trong tòa nhà bằng VPS Multiset (đến phòng)

User luôn bắt đầu từ Outdoor → đi đến tòa → chuyển sang Indoor → quét localize → chọn phòng → đi đến.

---

## Cấu trúc Scene (HybridGPSMap.unity)

```
HybridGPSMap (scene root)
│
├── MainScreen                    ← UI Toolkit (login, lịch sử, cài đặt)
│   └── [NavigationManager]       ← điều hướng trang UI Toolkit
│
├── OutdoorEnvironment            ← GPS navigation stack
│   ├── BKMAP                     ← model bản đồ trường (destinations: A3, B8, B9, B10)
│   ├── NavigationManager         ← [ARPathFinder, LineRenderer] vẽ đường outdoor
│   └── OutdoorNavigationUI       ← UGUI Canvas
│       ├── GPS Accuracy Circle
│       ├── Minimap Canvas
│       └── Mobile Navigation HUD ← status panel + dropdown chọn đích
│
├── IndoorEnvironment             ← VPS indoor stack (Multiset SDK)
│   └── UI Home Screen            ← ARPageObject (bật khi vào AR)
│       ├── XR Origin / ARCamera
│       ├── Canvas                ← UGUI indoor
│       │   ├── Header            ← nút "Xóa chỉ đường" + "Lịch sử/Cài đặt"
│       │   ├── NavigationUI      ← nút Destinations + progress slider
│       │   ├── InputChatBot      ← input chat trợ lý
│       │   ├── TroLyChan         ← avatar trợ lý
│       │   ├── CaptureButton     ← nút chụp/quét
│       │   └── ToastPanel
│       ├── Map Space             ← chứa map data + POI
│       │   ├── MapB9             ← building root B9 (inactive mặc định)
│       │   │   ├── material_0/1  ← mesh quét VPS (tag EditorOnly)
│       │   │   ├── POIs-B9       ← 8 POI phòng
│       │   │   └── ForBake       ← geometry cho NavMesh
│       │   └── MapB10            ← building root B10 (inactive mặc định)
│       │       ├── MAP_19VPMO5MUY2E  ← sub-map 1 (tag EditorOnly)
│       │       ├── MAP_0QG8RRPF65OT  ← sub-map 2 (tag EditorOnly)
│       │       └── POIs-B10     ← 7 POI phòng
│       ├── MapLocalizationManager   ← SDK: quét camera → cloud → localize
│       ├── NavigationController     ← SDK: NavMesh agent + path
│       └── MapMeshDownloader        ← SDK: download mesh (chỉ Editor)
│
├── HybridRuntime                 ← [HybridModeController]
├── ARPageController              ← quay về MainScreen khi thoát AR
├── EventSystem
├── Indoor Bootstrap              ← [MultisetIndoorBootstrap] fix NRE
└── SharedARRig (hoặc detached)   ← AR Session + XR Origin dùng chung
```

---

## Luồng hoạt động (User Flow)

### 1. Khởi động
```
App start → HybridModeController.Awake() → DeactivateARMode()
         → Outdoor + Indoor đều inactive
         → MainScreen hiện (UI Toolkit: onboarding/login)
```

### 2. Vào AR
```
User bấm nút "AR chỉ đường" trên MainScreen
→ NavigationManager.EnterARPage()
→ ARPageObject (UI Home Screen).SetActive(true)
→ HybridModeController.ApplyInitialMode() → Outdoor mode
→ OutdoorEnvironment active, IndoorEnvironment active (nhưng MapB9/B10 inactive)
→ NavigationManager.OnAREntered fires
→ Outdoor HUD hiện (Mobile Navigation HUD)
```

### 3. Outdoor navigation
```
User chọn đích (dropdown: B9, B10, A3...)
→ MobileNavigationHUD.SelectTarget(index)
→ ARPathFinder.SetTarget(targetTransform)
→ GPS + NavMesh tính path → LineRenderer vẽ đường
→ User đi theo đường → khoảng cách giảm dần
```

### 4. Chuyển sang Indoor
```
User bấm nút "Indoor" trên runtime mode switcher
→ HybridModeController.ForceIndoor()
→ IndoorEnvironment bật, OutdoorEnvironment tắt (tùy config)
→ User quét camera xung quanh tòa nhà
→ MapLocalizationManager.LocalizeFrame() → gửi frame lên cloud
→ Cloud so khớp → trả pose → SDK dịch Map Space transform
→ Localization success → mesh tím hiện (align check)
→ POI hiện ở vị trí thực
```

### 5. Indoor navigation
```
User bấm nút Destinations → BuildingDestinationListController.Toggle()
→ Hiện list tòa (B9, B10)
→ User bấm tòa → hiện list POI (B10-101, B10-102...)
→ User bấm POI → NavigationController.SetPOIForNavigation(poi)
→ NavMeshAgent tính path → ShowPath vẽ đường
→ PathEstimationUtils tính khoảng cách còn lại
→ User đi theo → đến nơi → ArrivedAtDestination()
```

### 6. Quay về Outdoor
```
User bấm "Outdoor" trên mode switcher
→ HybridModeController.ForceOutdoor()
→ OutdoorEnvironment bật, IndoorEnvironment tắt
→ GPS navigation tiếp tục
```

### 7. Thoát AR
```
User bấm "Lịch sử/Cài đặt" hoặc nút back
→ ARPageController.SwitchObject()
→ MainScreen.SetActive(true)
→ HybridModeController.DeactivateARMode()
→ ARPageObject.SetActive(false)
→ NavigationManager.OnARExited fires
```

---

## Các component chính

### HybridModeController
- Quản lý state: `Outdoor` / `Indoor` / `Transition`
- Bật/tắt environment roots
- Quản lý camera tag, audio, canvas
- Tạo runtime mode switcher (Indoor/Outdoor/Off buttons)
- Tạo transition overlay (fade)
- `autoSwitchEnabled = false` → chỉ switch khi user bấm

### IndoorMapSwitcher
- Switch giữa B9 ↔ B10
- Đổi `MapLocalizationManager.mapOrMapsetCode` qua reflection
- Bật building root đúng, tắt building root khác
- Gọi `LocalizeFrame()` nếu cần

### BuildingSceneBindings (MonoBehaviour trên UI Home Screen)
- Map `BuildingId` → `buildingRoot` (GameObject) + `poiContainer` (Transform)
- Dùng bởi `IndoorMapSwitcher` để biết bật/tắt cái nào

### BuildingRegistry (ScriptableObject asset)
- Metadata: mã VPS, tên hiển thị, tọa độ GPS entrance
- Không chứa scene reference (tránh Type Mismatch)

### MultisetIndoorBootstrap
- Fix NRE `NavigationController.Update() line 81`
- Reflection patch `ARCamera` + `ARCameraCollider` trên NavigationController
- Poll mỗi 0.2s cho đến khi Camera.main + SphereCollider sẵn sàng

### NavigationControllerSetup
- Thêm SphereCollider vào Camera.main (precondition SDK)
- Warp NavMeshAgent về NavMesh gần nhất

### BuildingDestinationListController
- UI list chọn tòa + POI
- `AutoSyncBuildingsIfNeeded()` — tự sync từ BuildingSceneBindings nếu list rỗng

---

## Giao diện (UI)

### MainScreen (UI Toolkit)
- Hệ thống: `NavigationManager` + `IPageController` + `PageFactory`
- Các trang: Onboarding, Login, Register, History, Chatbox, Profile, Settings...
- Shared NavBar (History + Settings) — chỉ hiện ở 2 trang này

### Outdoor HUD (UGUI — runtime tạo bởi MobileNavigationHUD)
- Status panel (trên): điểm đến, khoảng cách, GPS accuracy
- Target dropdown (dưới): chọn đích outdoor
- Minimap (góc phải)
- GPS Accuracy Circle (3D ring)

### Indoor HUD (UGUI — static trong scene)
- Header: "Xóa chỉ đường" + "Lịch sử/Cài đặt"
- NavigationUI: nút Destinations (search POI) + progress slider
- InputChatBot + TroLyChan: trợ lý chat
- CaptureButton: trigger quét VPS

### Runtime Mode Switcher (tạo bởi HybridModeController)
- 3 nút: Indoor / Outdoor / Off
- Anchor bottom-center
- Highlight nút đang active

---

## NavMesh

### Outdoor
- Bake offline trên model bản đồ trường (BKMAP)
- `ARPathFinder` dùng NavMesh để tính path GPS → đích

### Indoor
- Bake offline trên mesh quét VPS (trong Editor)
- Mỗi tòa có `NavMeshSurface` riêng (MapB9, MapB10)
- `CollectObjects = Children` → chỉ bake mesh con
- Mesh quét tag `EditorOnly` → strip khỏi build (APK nhỏ)
- NavMesh data lưu riêng (asset file) → vẫn có trên device
- Sau localize, Map Space transform dịch → NavMesh data theo

### Vấn đề đã biết
- NavMesh bị chia đảo (2 sub-map lệch nhau) → một số POI "Unreachable"
- Trên device: nếu NavMesh data không theo transform → POI hiện "-"
- Giải pháp: bake offline đúng cách + verify NavMesh liền mạch

---

## Multiset SDK

### MapLocalizationManager
- Field: `mapOrMapsetCode` (string), `localizationType` (enum: Map/MapSet)
- Method: `LocalizeFrame()` — chụp camera frame → gửi cloud → trả pose
- Events: `LocalizationSuccess`, `LocalizationFailure`
- `firstLocalizationUntilSuccess = true` → retry cho đến khi thành công

### NavigationController
- Singleton: `NavigationController.instance`
- Field: `agent` (NavMeshAgent), `currentDestination` (POI)
- Method: `SetPOIForNavigation(poi)`, `StopNavigation()`, `ArrivedAtDestination()`
- Cần: SphereCollider trên Camera.main (detect arrival)

### PathEstimationUtils
- Singleton: `PathEstimationUtils.instance`
- `EstimateDistanceToPosition(poi)` → float (mét), -1 (lỗi), -2 (unreachable)
- Dùng `NavMesh.CalculatePath` internal

### AgentPosition
- Mỗi frame warp NavMeshAgent về XZ camera position
- Y giữ nguyên (hoặc reset nếu lệch quá 1.5m/3.5m)

### MapMeshDownloader
- Chỉ chạy trong Editor (`Application.isPlaying → return`)
- Download GLB từ cloud → import vào Assets → tag EditorOnly
- Trên device: KHÔNG download runtime

### MapMeshHandler
- `meshVisualizationOption`: EnableVisualization / DisableVisualization
- Khi Enable: SDK render mesh quét sau localize (bạt tím)
- Khi Disable: không render (user chỉ thấy camera thật)

---

## Quy ước tag

| Tag | Ý nghĩa | Trên device |
|-----|----------|-------------|
| `EditorOnly` | Mesh quét VPS (nặng 50-100MB/map) | Strip khỏi build |
| `Untagged` | POI, building root, UI, logic | Giữ trong build |

---

## Enum BuildingId

```csharp
public enum BuildingId
{
    None = 0,
    B9   = 9,
    B10  = 10,
}
```

- B9: `MAP_9LME2PB7Y3EN` (single Map)
- B10: `MSET_AWDJFJNAVVFM` (MapSet gồm 2 sub-map)

---

## File quan trọng

| File | Vai trò |
|------|---------|
| `Assets/Code/Indoor/IndoorMapSwitcher.cs` | Switch B9 ↔ B10, đổi mã VPS |
| `Assets/Code/Indoor/BuildingSceneBindings.cs` | Map BuildingId → scene GO |
| `Assets/Code/Indoor/BuildingRegistry.cs` | ScriptableObject metadata |
| `Assets/Code/Indoor/BuildingId.cs` | Enum tòa nhà |
| `Assets/Code/Indoor/IndoorEntryConfirmController.cs` | Logic xác nhận vào tòa |
| `Assets/Code/MultisetIndoorBootstrap.cs` | Fix NRE camera collider |
| `Assets/Code/NavigationControllerSetup.cs` | Precondition cho SDK |
| `Assets/Code/HybridModeController.cs` | State machine Outdoor/Indoor |
| `Assets/Code/Navigation/MobileNavigationHUD.cs` | Outdoor HUD runtime |
| `Assets/Code/Navigation/ARPathFinder.cs` | Vẽ đường outdoor (NavMesh/GPS) |
| `Assets/Code/DestinationUI/BuildingDestinationListController.cs` | UI list POI indoor |
| `Assets/Code/DestinationUI/DestinationRowUI.cs` | Row hiển thị POI + khoảng cách |
| `Assets/UI/Scripts/Manager/NavigationManager.cs` | Điều hướng UI Toolkit pages |
| `Assets/Editor/BuildingRegistrySetup.cs` | Tool setup bindings |
| `Assets/Editor/IndoorBakeNavMeshes.cs` | Tool bake NavMesh |
| `Assets/Editor/IndoorBootstrapInstaller.cs` | Tool install components |
| `Assets/Editor/IndoorSetupDiagnostic.cs` | Tool chẩn đoán scene |
| `Assets/Editor/IndoorMeshTagFixer.cs` | Tool quản lý tag EditorOnly |

---

## Vấn đề tồn đọng

1. **POI hiển thị "-" trên device** — NavMesh data có thể không theo Map Space transform sau localize. Cần verify bằng adb logcat.
2. **NavMesh islands** — mesh quét 2 sub-map lệch nhau → bake ra 2 đảo → một số POI unreachable. Fix: tăng Step Height hoặc thêm NavMeshLink.
3. **Localize lần 2 fail** — SDK cache state sau lần 1. Cần reset internal state khi switch building (chưa tìm được method chính xác).
4. **Giao diện Indoor/Outdoor chưa thống nhất** — mỗi mode có UI riêng, style khác nhau. Cần refactor thành shared HUD.
