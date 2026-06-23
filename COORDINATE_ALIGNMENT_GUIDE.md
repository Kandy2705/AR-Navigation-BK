# Hướng Dẫn Đồng Bộ Hệ Tọa Độ: ManScene ↔ HybridGPSMap

## Tổng Quan Vấn Đề

**Scene cũ (ManScene)** đã được test nhiều lần và hiển thị đúng vị trí thực tế.
**Scene mới (HybridGPSMap)** đang biểu diễn sai vị trí.

**Nguyên nhân có thể:**
- MapOrigin lat/lon khác nhau giữa 2 scene → TargetAnchor tính vị trí sai
- OriginOffset / Marker không align → các object con bị offset
- Có parent nào thay đổi local/world transform
- ENU mapping hoặc coordinate conversion lỗi

---

## Kiến Trúc GPS↔ENU↔Unity

```
GPS (Lat/Lon)
  ↓ (MapOrigin.LatLonAltToECEF)
ECEF (x, y, z - tâm Trái Đất)
  ↓ (MapOrigin.ECEFToENU)
ENU (East, North, Up - tương đối so với MapOrigin)
  ↓ (Vector3(e, 0, n))
Unity World (X, Z với Y=0)
```

**Hằng số chuẩn:**
- a = 6378137.0 (bán kính Trái Đất)
- e² = 6.694380004e-3 (tâm sai)

**Cây scene:**
```
ManScene/
├── Environment/
│   └── MapBK/
│       ├── Marker               ← Mốc chuẩn (0, 0, 0)
│       ├── B10
│       ├── B10-107, B10-106, ...
│       └── SoccerField (nếu có)

HybridGPSMap/
├── OutdoorEnvironment/
│   └── BKMAP/
│       ├── OriginOffset         ← Cần align với Marker
│       ├── SchoolGround
│       ├── A5OriginalOffset
│       ├── B10
│       └── ObstacleContainer
```

---

## STEP 1: Scan & So Sánh (Debug)

### Menu: `Tools > Scene Coordinate Comparison`

Script sẽ:
1. Load ManScene → log vị trí Marker, B10, MapOrigin
2. Load HybridGPSMap → log vị trí OriginOffset, B10, MapOrigin
3. Tính offset cần apply
4. In ra khuyến nghị sửa chữa

**Output:**
- Console output
- File: `Logs/scene-coordinate-comparison.txt`

**Nội dung cần kiểm tra:**
```
>>> COMPARISON ANALYSIS

MapOrigin Comparison:
  ManScene origin:    lat=10.77XX, lon=106.65XX
  HybridGPSMap origin: lat=10.77XX, lon=106.65XX
  Difference: Δlat=XXX, Δlon=XXX

Object Position Comparison:
  Marker:
    ManScene:    pos=(-50.2, 0, 120.5)
    HybridGPSMap: pos=(?, ?, ?)
    Offset: ?, Distance: XXm
```

**Nếu Distance > 0.1m → cần sửa OriginOffset**

---

## STEP 2: Fix Tự Động (Recommended)

### Menu: `Tools > Scene Coordinate Aligner > 1. Align HybridGPSMap with ManScene`

Script sẽ tự động:
1. ✓ So sánh MapOrigin giữa 2 scene
2. ✓ Cập nhật MapOrigin trong HybridGPSMap nếu khác
3. ✓ Dịch chuyển OriginOffset để align với Marker
4. ✓ Save scene

**Khuyến cáo:** Sao lưu scene trước khi chạy.

---

## STEP 3: Re-bake NavMesh

### Menu: `Tools > Scene Coordinate Aligner > 2. Bake NavMesh (HybridGPSMap)`

Vì các object đã dịch chuyển → cần bake lại NavMesh.

Script sẽ:
1. Tìm tất cả NavMeshSurface trong scene
2. Bake từng cái
3. Save scene

**Thời gian:** Tùy độ phức tạp (vài giây - vài phút)

---

## STEP 4: Verify (Debug)

### Menu: `Tools > Scene Coordinate Aligner > 3. Verify Alignment (Debug)`

Kiểm tra status:
```
ManScene:
  Marker: (-50.2, 0, 120.5)
  MapOrigin: lat=10.7736444, lon=106.6593743

HybridGPSMap:
  OriginOffset: (-50.2, 0, 120.5)     ← Phải trùng Marker
  MapOrigin: lat=10.7736444, lon=106.6593743  ← Phải trùng

Alignment Status:
  Position: ✓ ALIGNED
  MapOrigin: ✓ ALIGNED
```

**Nếu ALIGNED → OK. Nếu MISALIGNED → cần điều chỉnh thêm.**

---

## STEP 5: Manual Adjustments (Nếu cần)

Nếu script fix không hoàn toàn, bạn có thể điều chỉnh thủ công:

### 5.1. Mở HybridGPSMap, kiểm tra OriginOffset

1. Open scene: `Assets/Scenes/HybridGPSMap.unity`
2. Tìm: `OutdoorEnvironment/BKMAP/OriginOffset`
3. Kiểm tra **Inspector → Transform → Position**
4. So sánh với Marker trong ManScene

**Nếu khác → điều chỉnh trực tiếp trong Inspector**

### 5.2. Kiểm tra MapOrigin

1. Tìm: MapOrigin component (có thể trong Scene hoặc GlobalProperties)
2. Kiểm tra: `originLat`, `originLon`
3. So sánh với giá trị trong MapOrigin của ManScene

**Nếu khác → chỉnh lại cho giống**

### 5.3. Kiểm tra TargetAnchor

Chạy: `Tools > GPS Navigation Diagnostic`

Nó sẽ log:
```
TargetAnchor count: X
  [B10]  lat=10.7734  lon=106.660375  active=true
  [B9]   lat=10.7734  lon=106.660375  active=true
  ...
```

**Kiểm tra:**
- Lat/lon có đúng không?
- Active status có đúng không?

---

## STEP 6: Xác Minh Trực Quan (Play Mode)

1. Open HybridGPSMap
2. Play
3. Kiểm tra:
   - ✓ B10 nằm đúng vị trí trên bản đồ (so sánh với ManScene)
   - ✓ Sân banh align đúng
   - ✓ Path rendering khớp với NavMesh
   - ✓ POI (Des1, Des2) nằm đúng vị trí

---

## Troubleshooting

### Problem 1: Script không tìm thấy Marker hoặc OriginOffset

**Nguyên nhân:** Tên object khác hoặc nằm ở path khác

**Giải pháp:**
1. Chạy: `Tools > Scene Coordinate Aligner > 4. Print Scene Structure (Debug)`
2. Tìm tên đúng của object
3. Sửa script: thay đổi tên trong `GameObject.Find()`

### Problem 2: MapOrigin khác nhau

**Nguyên nhân:** Scene mới tạo MapOrigin mới với tọa độ khác

**Giải pháp:**
- Lấy MapOrigin từ ManScene hoặc WorldAnchor pool
- Hoặc manually set giá trị trong Inspector

### Problem 3: Vẫn sai vị trí sau khi align

**Nguyên nhân:** Có layer/parent khác thay đổi transform

**Kiểm tra:**
- OriginOffset có parent không? Nếu có, parent có rotateY ≠ 0 không?
- BKMAP (hoặc SchoolGround) có scale ≠ 1 không?
- Có script nào đang move object trong Awake/Start không?

**Giải pháp:**
- Reset parent: position = (0,0,0), scale = (1,1,1), rotation = (0,0,0)
- Kiểm tra script tương ứng

### Problem 4: NavMesh vẫn sai sau bake

**Nguyên nhân:** Object chứa NavMesh không ở vị trí align

**Giải pháp:**
1. Kiểm tra BKMAP (hoặc object khác chứa ForBake geometry)
2. Đảm bảo nó là child của OriginOffset và nằm đúng vị trí
3. Bake lại NavMesh

---

## Debug Log Keywords

Khi chạy, tìm log với từ khóa:

```
[SceneCoordinateComparison]  - Scan & so sánh
[SceneCoordinateAligner]     - Fix & align
[TargetAnchor]               - Vị trí POI được tính
[MapOrigin]                  - GPS conversion debug
[ARPathFinder]               - Path rendering
```

---

## Cheat Sheet: Lệnh nhanh

| Menu | Công dụng |
|------|----------|
| `Tools > Scene Coordinate Comparison` | Scan & so sánh vị trí |
| `Tools > Scene Coordinate Aligner > 1` | **AUTO FIX** (khuyến cáo) |
| `Tools > Scene Coordinate Aligner > 2` | Bake NavMesh |
| `Tools > Scene Coordinate Aligner > 3` | Verify alignment |
| `Tools > Scene Coordinate Aligner > 4` | Print scene structure |
| `Tools > GPS Navigation Diagnostic` | Debug GPS/TargetAnchor |
| `Tools/Nav/Run ManScene Health Check` | Debug ManScene |

---

## Reference: MapOrigin Công Thức

```csharp
// Lat/Lon → ECEF
double N = a / sqrt(1 - e² * sin²(lat))
x = (N + alt) * cos(lat) * cos(lon)
y = (N + alt) * cos(lat) * sin(lon)
z = (N * (1 - e²) + alt) * sin(lat)

// ECEF → ENU
e = -sin(lon) * dx + cos(lon) * dy
n = -sin(lat)*cos(lon)*dx - sin(lat)*sin(lon)*dy + cos(lat)*dz
u = cos(lat)*cos(lon)*dx + cos(lat)*sin(lon)*dy + sin(lat)*dz

// ENU → Unity
Vector3(e, 0, n)
```

---

## Kết Luận

**Workflow:**
1. `Tools > Scene Coordinate Comparison` → debug
2. `Tools > Scene Coordinate Aligner > 1` → **AUTO FIX**
3. `Tools > Scene Coordinate Aligner > 2` → Bake NavMesh
4. `Tools > Scene Coordinate Aligner > 3` → Verify
5. Play mode test → kiểm tra trực quan

Nếu không hoạt động, hãy check output của script 1 và điều chỉnh thủ công.
