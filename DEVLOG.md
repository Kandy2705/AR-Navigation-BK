# DEVLOG — AR-Navigation-BK

Nhật ký kỹ thuật để lần sau đọc lại biết đã làm gì, vì sao, file nào.

---

## 2026-07-18 (7) — Dọn UI chồng panel (passenger clean)

### Vấn đề
Nhiều overlay debug + status dài + Shared AR UI + mode switcher status → UI "tùm lum".

### Cách làm
- `CleanPassengerUi` auto trên HybridGPSMap: tắt HybridState/MultisetPose/OnScreen/GPSMapWorld debug, HybridRuntimeDiagnose
- `MobileNavigationHUD`: `showPathBuildDebugLine=false`, `passengerCompactStatus=true` (status 2–3 dòng)
- Ẩn Shared AR UI + status line mode switcher
- `ApplyPassengerCleanMode()` public

Dev cần debug: disable `CleanPassengerUi` hoặc tick lại showPathBuildDebugLine.

---

## 2026-07-18 (6) — Đến nơi → tắt chỉ đường (Google Maps style)

### Hành vi
`ArrivalWatcher.endNavigationOnArrival = true`:
- Hiện banner "Bạn đã đến nơi!"
- `HybridDestinationService.Clear()` + clear mọi `ARPathFinder` → path ribbon tắt
- Multiset StopNavigation best-effort
- Toast HUD: "Da den… Tat chi duong."
- Chọn đích mới vẫn hoạt động bình thường

---

## 2026-07-18 (5) — Banner giữa màn hình khi đến nơi

### Mục tiêu
Khi user đi tới điểm đích → thông báo **giữa màn hình**: "Bạn đã đến nơi!" + tên điểm.

### Cách làm
- `ArrivalBanner`: overlay Canvas (sortingOrder 9000), fade in/out, auto-hide ~4.5s
- `ArrivalWatcher`: đo XZ user → dest (HybridDestinationService / RouteCoordinator / ARPathFinder), bán kính 3m; rời >6m mới cho báo lại
- `MobileNavigationHUD`: rising-edge arrival cũng gọi banner

### File mới
- `Assets/Code/Navigation/HUD/ArrivalBanner.cs`
- `Assets/Code/Navigation/HUD/ArrivalWatcher.cs`

### Verify
Chọn dest gần → mock/WASD vào <3m → banner giữa màn hình.

---

## 2026-07-18 (4) — Dropdown chỉ outdoor: fix catalog indoor Multiset POI

### Nguyên nhân
HybridGPSMap gắn **Multiset SDK `POI`** (assembly MultiSet), không phải `Assets/Code/POI.cs`.
`HybridDestinationService` gọi `GetComponentsInChildren<POI>()` / `FindObjectsByType<POI>()` → type project → **0 indoor**.

### Sửa
- Resolve type Multiset `POI` qua reflection (assembly name chứa MultiSet)
- `CollectMultisetPoisEverywhere` + under `BuildingSceneBindings`
- Fallback: children `POIs-B9` / `POIs-B10` theo tên
- Log catalog: outdoor/indoor count + multiset type

### Verify
Play → Console: `indoor=N` (N>0) → dropdown có `[Trong] B9 · …`.

---

## 2026-07-18 (3) — Outdoor destination Tòa B9 + Tòa B10

### Mục tiêu
Dropdown outdoor không chỉ Des1/Des2 — thêm **chỉ đường ngoài trời tới B9 và B10**.

### Cách làm (không edit scene binary HybridGPSMap)
- Runtime bootstrap `OutdoorBuildingDestinationBootstrap`:
  - Auto chạy AfterSceneLoad trên `HybridGPSMap` / `Hybrid Navigation`
  - Tạo `TargetAnchor` `Outdoor_B9` / `Outdoor_B10` (displayName **Tòa B9** / **Tòa B10**)
  - Capsule visual màu xanh / cam + TextMesh label
- Nguồn GPS (ưu tiên):
  1. `EntranceAnchor` B9/B10 → `MapOrigin.GetGPSFromUnityPosition`
  2. `PoiDatabase` id B9/B10
  3. Fallback survey: B9 `(10.7734, 106.660375)`, B10 `(10.773675, 106.6608861)`
- `BuildingRegistry.asset` entrance lat/lon cập nhật cùng số
- `MobileNavigationHUD.RebuildDestinationList()` sau spawn

### File
| File | Việc |
|------|------|
| `Assets/Code/Navigation/Integration/OutdoorBuildingDestinationBootstrap.cs` | **Mới** |
| `Assets/Code/Navigation/Core/MapOrigin.cs` | `GetGPSFromUnityPosition` |
| `Assets/Code/Navigation/HUD/MobileNavigationHUD.cs` | `RebuildDestinationList` |
| `Assets/Code/Indoor/BuildingRegistry.asset` | entrance B9/B10 GPS |

### Verify
Play HybridGPSMap → dropdown có `[Ngoài] Tòa B9`, `[Ngoài] Tòa B10` (cùng Des1/Des2 nếu còn).
Chọn Tòa B9 → path outdoor tới neo GPS B9.

### Lưu ý
- MapOrigin scene phải cùng hệ tọa độ campus BK (~10.77, 106.66). Nếu MapOrigin còn tọa độ test 10.748… thì marker B9/B10 sẽ lệch — chỉnh MapOrigin / EntranceAnchor.
- Tinh chỉnh lat/lon: sửa `PoiDatabase` hoặc đặt `Entrance_B9` đúng world trên map.

---

## 2026-07-18 (2) — Điểm đến qua UI (outdoor + indoor), bỏ hardcode

### Mục tiêu

Không còn “chỉ đường cứng” (`HybridDestinationByName` / destination Inspector).
Trên **HybridGPSMap** user chọn điểm đến bằng UI:

- Dropdown outdoor HUD: **cả ngoài trời + trong tòa**
- Ô **search** gõ tên (vd `B9`, `P101`, `toilet`)
- List Destinations indoor (nút Multiset) cũng đẩy vào hybrid pipeline

### Cách hoạt động

1. **`HybridDestinationService`** (`Assets/Code/Hybrid/HybridDestinationService.cs`)
   - Catalog runtime:
     - Outdoor: mọi `TargetAnchor`
     - Indoor: mọi `POI` dưới `BuildingSceneBindings` (fallback: mọi POI scene)
   - `Apply(entry)` → `HybridRouteCoordinator.SetDestination` + `HybridLocalizationManager.SetDestinationBuilding`
   - Indoor dest khi còn outdoor: **không ForceIndoor** — path phase 1 ra cửa, phase 2 vào phòng

2. **`MobileNavigationHUD`**
   - `useHybridDestinationCatalog = true` (default)
   - Dropdown label: `[Ngoài] …` / `[Trong] B9 · P101`
   - Tự tạo InputField search nếu thiếu
   - Gõ lọc list; Enter → apply match

3. **`BuildingDestinationListController.StartNavigationTo`**
   - Ưu tiên `HybridDestinationService.ApplyIndoorPoi` (reflection)
   - Fallback legacy Multiset `SetPOIForNavigation` nếu không có hybrid service

4. **`HybridDestinationByName`**
   - `autoApplyOnEnable = false` (default) — không còn auto gán P101 lúc Play

### Thêm điểm đến indoor mới (không sửa code)

1. Trong scene HybridGPSMap → tòa (vd `MapB9` / `POIs-B9`)
2. Tạo GO + component **`POI`** (+ collider/sign nếu cần Multiset)
3. Gán `poiName` / `listTitle` rõ (vd `P102`, `WC tầng 1`)
4. Play → search/dropdown tự thấy entry mới

### Thêm điểm outdoor

1. Tạo GO + **`TargetAnchor`** (lat/lon + displayName)
2. Catalog auto pick

### Verify

1. Play HybridGPSMap → dropdown có cả `[Ngoài]` và `[Trong]`
2. Gõ `P101` → list lọc → chọn → status “Den: …”
3. Dest indoor khi outdoor: path hướng **entrance**, không teleport indoor ngay
4. `HybridDestinationByName` log “autoApplyOnEnable=false”

### File đụng

| File | Việc |
|------|------|
| `Assets/Code/Hybrid/HybridDestinationService.cs` | **Mới** — catalog + Apply |
| `Assets/Code/Navigation/HUD/MobileNavigationHUD.cs` | Catalog dropdown + search |
| `Assets/Code/DestinationUI/BuildingDestinationListController.cs` | Wire hybrid ApplyIndoorPoi |
| `Assets/Code/Hybrid/HybridDestinationByName.cs` | Tắt auto hardcode |

### Scene note

- Component `HybridDestinationService` **tự tạo** trên `Hybrid Hub` nếu thiếu (`EnsureExists`).
- Inspector destination cũ trên `HybridRouteCoordinator` bị **ghi đè** khi user chọn UI.

### Fix compile 2026-07-18

- **CS1503** `HybridDestinationService`: không gọi `NavigationController.SetPOIForNavigation(project POI)` — hai type `POI` (project vs MultiSet-SDK) không convert được. Hybrid chỉ set `HybridRouteCoordinator`.

---

## 2026-07-18 — Fix: điểm/path chỉ đường vào indoor quá chậm

### Bối cảnh (đọc doc trước khi sửa)

Đã đọc:

- `CLAUDE.md`, `AGENTS.md` — hybrid Outdoor/Indoor, Multiset VPS, NavMesh.
- `INDOOR_OUTDOOR_ARCHITECTURE.md` — flow Outdoor → cửa → localize → POI.
- `INDOOR_LOCALIZATION_BUGS.md` — lỗi scene/XROrigin (không phải root cause lần này).
- `COORDINATE_ALIGNMENT_GUIDE.md`, `SCENE_CONTEXT.md`, `TEST_PLAN.md`.
- Pipeline hybrid: `HybridLocalizationManager` → `LocalizationQualityGate` → `HybridRouteCoordinator` → `HybridArPathFinderBridge` / `HybridPathRenderer` / `HybridArrowFollower` → `ARPathFinder`.

Triệu chứng (từ screenshot + mô tả): player tiến vào tòa, **điểm/path chỉ đường indoor cập nhật/di chuyển quá lâu** — path chevron + điểm đích không nhảy sang route indoor ngay.

### Nguyên nhân (code, không phải “phải ra trường”)

1. **`HybridRouteCoordinator` — `TransitionScanning` = `Pause`**  
   Vào cửa → ẩn path đến khi VPS stable (gate + có thể tới `scanTimeoutSeconds` 25s). User thấy “chờ mãi mới chỉ đường trong nhà”.

2. **Đổi phase không force recompute**  
   Vào phase `IndoorToPoi` vẫn có thể chờ `recomputeIntervalSeconds` (0.4s) + không recompute khi POI/Map Space snap.

3. **`HybridArrowFollower` SmoothDamp thuần**  
   `positionSmoothTime = 0.4s` trên quãng nhảy lớn (entrance → POI) → mũi tên/điểm “bò” vài giây.

4. **`HybridPathRenderer` crossfade 0.7s**  
   Đổi source Outdoor→Indoor: fade-out hết mới swap → path mới trễ ~0.7–1.4s.

5. **`HybridArPathFinderBridge` không force `SetTarget` khi target nhảy**  
   Cùng Transform marker, `ARPathFinder` throttle `pathUpdateInterval` 0.5s → ribbon trễ.

6. **`LocalizationQualityGate` warm-up dài**  
   `indoorStableSeconds = 1.5` + `requiredConsecutiveSuccesses = 2` (+ `minStateDwellSeconds = 0.5`) → state `Indoor` trễ sau khi VPS đã có pose.

### Thay đổi đã làm

| File | Thay đổi |
|------|----------|
| `Assets/Code/Hybrid/HybridRouteCoordinator.cs` | Provisional indoor path khi `TransitionScanning`; force recompute khi phase/source đổi; recompute khi target nhảy; clear corners khi Pause |
| `Assets/Code/Hybrid/HybridArrowFollower.cs` | Snap khi phase đổi / nhảy ≥ 1.25m; smooth time ngắn hơn cho micro-move |
| `Assets/Code/Hybrid/HybridPathRenderer.cs` | `crossfadeSeconds` 0.7→0.2; `snapSourceOnChange` swap path ngay |
| `Assets/Code/Hybrid/HybridArPathFinderBridge.cs` | Force `SetTarget` khi phase/source đổi hoặc target nhảy ≥ 0.75m |
| `Assets/Code/Hybrid/LocalizationQualityGate.cs` | Indoor ready nhanh hơn (0.6s, 1 hit); logic hits = Min(profile, gate) |
| `Assets/Code/Hybrid/HybridLocalizationManager.cs` | `minStateDwellSeconds` 0.5→0.25 |
| `Assets/Code/Hybrid/BuildingLocalizationProfile.cs` | Default consecutive successes 2→1 |

### Cách verify (không bắt buộc ra trường cho logic)

1. Editor Play — scene hybrid có `HybridRouteCoordinator` + destination indoor.  
2. Force / mock: ApproachingEntrance → TransitionScanning.  
3. Kỳ vọng: path tới POI indoor **hiện ngay** (provisional), không biến mất chờ VPS.  
4. Khi target/POI nhảy: ribbon + mũi tên **snap**, không bò.  
5. Device tại trường: so time-to-indoor-path trước/sau (cảm quan + stopwatch).

### Flag / rollback

- `HybridRouteCoordinator.provisionalIndoorPathWhileScanning` — tắt nếu provisional path xuyên tường khó chịu khi NavMesh outdoor/indoor chưa nối.  
- `HybridPathRenderer.snapSourceOnChange` — tắt để về crossfade cũ.  
- Gate thresholds: chỉnh lại trong Inspector nếu false IndoorReady (localize nhầm).

### Chưa làm (ngoài scope)

- Sửa accuracy Multiset cloud VPS.  
- Scene wiring XROrigin (xem `INDOOR_LOCALIZATION_BUGS.md`).  
- Unit/PlayMode test mới cho handover path (nên thêm sau).

---
