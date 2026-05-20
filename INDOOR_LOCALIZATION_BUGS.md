# Indoor Localization Bug Report — Scene `HybridGPSMap`

> Phân tích chi tiết các lỗi gây ra **localization sai khi vào Indoor mode** trong scene `Assets/Scenes/HybridGPSMap.unity`. Khi build ra device, Multiset VPS localize không khớp với tòa nhà B9 thực tế.

---

## Triệu chứng

- Khi chuyển sang Indoor mode, Multiset VPS localize sai vị trí so với tòa B9 thực tế.
- XROrigin của Indoor navigation không trùng với NavMeshAgent.
- Console log:
  - `Failed to create agent because it is not close enough to the NavMesh`
  - `No simulation selected or invalid selection for localization!`
  - `NullReferenceException` ở `AgentPosition.Awake()`, `NavigationController.Start()`, `PathEstimationUtils.Start()`

---

## Nguyên nhân — 6 lỗi đan xen

### 🔴 Lỗi 1: Trùng `IndoorEnvironment` GameObject (CRITICAL)

Scene có **2 root GameObject cùng tên `IndoorEnvironment`**:

| Instance ID | activeSelf | Children | Vai trò |
|---|---|---|---|
| **60502** | `false` | 12 (full Multiset stack: IndoorMapSwitcher, MultisetSDKManager, MapLocalizationManager, NavigationController, MapMeshDownloader, SimulationDataManager, Map Space (AugmentedSpace), AR Session, XR Origin, …) | **Proper** — indoor SDK đầy đủ |
| **62396** | `true` | 1 (chỉ có Map Space > MapB10 inactive) | **Duplicate rỗng** |

`HybridModeController.indoorEnvironment` reference theo Unity Object — nhưng có khả năng đang trỏ vào duplicate hoặc một trong hai cái — gây xung đột.

### 🔴 Lỗi 2: Inspector của `HybridModeController` cấu hình sai

GameObject `HybridRuntime > HybridModeController`:

| Field | Value hiện tại | Phải là | Hậu quả nếu sai |
|---|---|---|---|
| `detachOutdoorXrRigFromEnvironment` | `false` | **`true`** | Khi vào Indoor mode, `OutdoorEnvironment.SetActive(false)` làm **XR Origin + AR Camera bên trong tắt theo** → AR Session vỡ → Multiset SDK lấy camera null |
| `disableIndoorXROriginDuplicates` | `false` | **`true`** | 2 XROrigin chạy song song → Multiset pose update đi nhầm rig → map B9 lệch |
| `simpleGpsTracker` | `null` | Reference đến `OutdoorEnvironment > XR Origin` | Fix `freezeXROriginUpdate` không kích hoạt → GPS vẫn ghi đè XROrigin trong Indoor mode |
| `gpsMarker` | `null` | (không dùng nữa — bỏ qua) | Project không xài GPSMarker, để null là OK |
| `alignXROriginToUser` | `null` | (scene không có) | Không bắt buộc |
| `alwaysActiveRoots` | `[]` | (giữ nguyên hoặc thêm XR rig) | XR rig phải được giữ alive khi switch mode |

### 🔴 Lỗi 3: 3 XROrigin cùng tồn tại trong scene

| Đường dẫn | Components | Mode |
|---|---|---|
| `OutdoorEnvironment > XR Origin` | XROrigin, SimpleGPSTracker, EditorUserIconMockRigDriver, NavigationProximityRefinement | Outdoor |
| `IndoorEnvironment(60502) > UI Home Screen > XR Origin` | XROrigin, XROriginEditorFlyController | Indoor |
| (cộng thêm rủi ro từ duplicate IndoorEnvironment) | | |

→ Cần `disableIndoorXROriginDuplicates = true` để chỉ 1 XROrigin active tại mỗi thời điểm.

### 🔴 Lỗi 4: NullReference cascade từ Multiset SDK

```
AgentPosition.Awake()           → NullRef (line 27)
NavigationController.Start()    → NullRef (line 47)
PathEstimationUtils.Start()     → NullRef (line 51)
```

Hệ quả của Lỗi 1+2+3. Multiset components initialize **trước khi** camera/XROrigin được setup đúng. `MultisetIndoorBootstrap` patch sau Awake() nên không kịp.

### 🔴 Lỗi 5: "No simulation selected" (Editor-only)

```
Error: No simulation selected or invalid selection for localization!
Stack: MapLocalizationManager.LocalizeSimulationData()
       ← IndoorMapSwitcher.TriggerLocalizeFrame() (line 179)
       ← IndoorAutoEnterB9.TriggerSwitchToBuilding() (line 115)
```

Chỉ ảnh hưởng **Editor**. Trên device dùng camera thật → bỏ qua. Để test trong Editor, cần chọn simulation scan trong `MapLocalizationManager` inspector.

### 🔴 Lỗi 6: NavMesh không có cho MapB9

```
Warning: Failed to create agent because it is not close enough to the NavMesh
```

Có thể do MapB9 chưa bake NavMesh hoặc NavMesh bake tại vị trí khác với vị trí Multiset đặt Map Space sau khi localize.

---

## Trạng thái sửa

### ✅ Đã sửa tự động qua MCP

- ✅ **Xóa duplicate `IndoorEnvironment`** (instanceId 62396) — done
- ✅ **`HybridModeController.detachOutdoorXrRigFromEnvironment` = `true`** — done
- ✅ **`HybridModeController.disableIndoorXROriginDuplicates` = `true`** — done
- ✅ **Save scene** — done
- ✅ **Code fix** (đã làm trước đó): `freezeXROriginUpdate` flag thêm vào `SimpleGPSTracker.cs` và `HybridModeController.ApplyXROriginFreezeForMode()` (chỉ freeze SimpleGPSTracker, không dùng GPSMarker)

### ⚠️ Phải làm thủ công trong Unity Inspector

MCP server không resolve được component references cho Unity custom type, nên phải wiring tay:

1. Chọn GameObject `HybridRuntime` trong Hierarchy
2. Trong Inspector, kéo các reference sau vào `HybridModeController` component:

| Field | Drag từ | Path |
|---|---|---|
| `Simple Gps Tracker` | `SimpleGPSTracker` component trên `XR Origin` | `OutdoorEnvironment > XR Origin` |
| `Gps Marker` | (không dùng — bỏ qua) | — |
| `Align XR Origin To User` | (scene không có — bỏ qua) | — |

3. Save lại scene (`Ctrl+S` / `Cmd+S`)

### ⚠️ Các việc khác phải làm trong Unity Editor

- **Bake NavMesh cho MapB9**: `Tools > Indoor Bake NavMeshes` (hoặc menu tương ứng từ `Assets/Editor/IndoorBakeNavMeshes.cs`)
- **Verify Multiset VPS code**: Mở `IndoorMapSwitcher` component (path: `IndoorEnvironment > UI Home Screen`) → kiểm tra entry B9 có `mapOrMapsetCode` khớp scan trên Multiset console
- **(Editor test only)** Chọn simulation scan trong `MapLocalizationManager` inspector — chỉ cần khi test trong Editor, không ảnh hưởng device build

---

## Vì sao sửa xong sẽ đúng vị trí B9 thực tế

1. **`detachOutdoorXrRigFromEnvironment = true`** → outdoor XR rig được reparent ra ngoài, sống xuyên suốt mode switch → AR Camera + AR Session liên tục → Multiset có frame video ổn định để localize
2. **`disableIndoorXROriginDuplicates = true`** → chỉ 1 XROrigin nhận pose → Multiset pose áp đúng rig
3. **`freezeXROriginUpdate` (fix đã làm trong code)** + wire reference đúng → GPS bị freeze trong Indoor → không kéo XROrigin về tọa độ GPS lệch
4. **Xóa duplicate IndoorEnvironment** → chỉ 1 stack Multiset chạy → không xung đột
5. **Bake NavMesh đúng MapB9** → NavMeshAgent snap được lên path
