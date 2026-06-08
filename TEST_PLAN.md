# Kế hoạch Kiểm thử — Hệ thống AR Navigation (Outdoor)

Mục tiêu: chứng minh hệ chỉ đường AR hoạt động đúng + đo được độ chính xác thực tế, làm bằng chứng cho tính ứng dụng.

Hệ thống test gồm **3 tầng**: Unit (tự động) → PlayMode (mock) → Field (thiết bị thật).

---

## Tầng 1 — Unit Test (EditMode, tự động, không cần device)

Vị trí: `Assets/Tests/Editor/EditMode/`. Naming: `Method_WhenCondition_ExpectedResult`.

| Test file | Hàm test | Assert |
|---|---|---|
| `MapOriginTests` | `GetUnityPositionFromGPS` | Điểm 0m → (0,0,0); ~111m Bắc → z≈100; ~109m Đông → x≈100; round-trip lat/lon ổn định |
| `NavMeshPathRibbonTests` | `SmoothCornersChaikin` | Giữ nguyên điểm đầu/cuối; iterations=0 → không đổi; count tăng; mọi điểm nằm trong bounding box polyline gốc |
| `CompassMathTests` *(cần tách static)* | Circular mean | `[359,1]`→~0 (KHÔNG 180); `[10,20,30]`→20; rỗng → fallback |
| `GpsJumpTests` *(cần tách static)* | Jump reject | dist>50m → reject=true; 10m → false |
| `PoiGpsEditModeTests` *(mở rộng)* | POI lat/lon → world | POI tại MapOrigin → (0,0,0); 2 POI cách 70m thực → Unity ~70m |

> *(cần tách static)*: logic đang nằm trong coroutine/private của `SimpleGPSTracker`. Refactor "extract method" sang `CompassMath`/`GpsMath` static để test được (behavior production không đổi).

**Chạy:** `Unity.exe -batchmode -runTests -testPlatform EditMode -testResults Logs/edit.xml -quit`

---

## Tầng 2 — PlayMode Test (mock GPS, có scene)

Vị trí: `Assets/Tests/PlayMode/`. Mở rộng `PoiScenePlayModeTests.cs`.

| Test | Kịch bản mock | Assert |
|---|---|---|
| GPS smoothing | Chuỗi fix ổn định | XR Origin hội tụ về target, không vọt |
| Jump reject runtime | 1 fix nhảy 100m | XR Origin không nhảy theo |
| VIO-from-start | First fix | `_activeSmoothSpeed` = postCalibrationSmoothSpeed |
| Manual snap | Gọi `CalibrateAtSurveyedPoint(snap)` | `HasCalibratedAtAnchor` = true; camera ≈ surveyed point |
| Occlusion | POI sau collider | TargetAnchor renderer.enabled = false |

---

## Tầng 3 — Field Test (thiết bị thật) ⭐ bằng chứng tính ứng dụng

### Chuẩn bị
- 1 thiết bị cố định, ghi rõ model + ngày + thời tiết
- App **GPS Status & Toolbox** làm ground truth
- Thước dây; quay video màn hình mỗi test
- Lặp **3 lần/điểm**, test **2 thời điểm/ngày** (hình học vệ tinh đổi)

### Bộ test case

| # | Test case | Cách đo | Đạt nếu |
|---|---|---|---|
| 1 | Static — đứng tại POI (khu thoáng) | App báo khoảng cách tới POI đó | ≤ 3m |
| 2 | Static gần tòa cao (urban canyon) | Như trên | ≤ 8m (chấp nhận GPS xấu) |
| 3 | Walk-to-POI | Lúc app báo "đã đến", đo khoảng cách thật tới cửa | ≤ 3m |
| 4 | Drift — đi A5→B9 (~200m) | So vị trí cuối app vs thực | ≤ 5m |
| 5 | Chuyển động mượt (VIO) | Đứng im 30s, quan sát path/POI | Không giật/trôi |
| 6 | Heading | Đứng nhìn Bắc, quan sát trục path | lệch ≤ 10° |
| 7 | Occlusion | POI sau tòa nhà | POI ẩn đúng |
| 8 | Cold start | Bấm giờ mở app → GPS ready | ≤ 30s |
| 9 | Mất GPS — vào sảnh rồi ra | Hồi phục tracking | < 10s |
| 10 | Manual snap ở chỗ thoáng | Đứng tại POI thoáng, bấm snap | POI về đúng ~1-2m |
| 11 | Minimap path | Path có hiện trên minimap không | Hiện rõ |

### Mẫu test log (điền tay/ảnh)

```
Ngày: ____  Thiết bị: ____  Thời tiết: ____  Build: ____
| Test# | Vị trí | App báo | Thực tế (đo) | Lệch | Lần1/2/3 | Pass? | Ghi chú |
|-------|--------|---------|--------------|------|----------|-------|---------|
| 1     | A5     |         |              |      |          |       |         |
| 3     | →B9    | "đã đến"|              |      |          |       |         |
```

---

## Metrics theo dõi qua các build (so build mới vs cũ)

| Metric | Mục tiêu |
|---|---|
| POI static offset TB (khu thoáng) | ≤ 3m |
| Navigation arrival error TB | ≤ 3m |
| Drift rate | ≤ 2.5m / 100m |
| GPS lock time (cold) | ≤ 30s |
| Crash / black-screen | 0 |

---

## Tiêu chí nghiệm thu (Definition of Done)

- [ ] Tầng 1: tất cả EditMode test pass
- [ ] Tầng 2: PlayMode test pass
- [ ] Tầng 3: ≥ 8/11 field test case đạt; test #1,3,5,8 (cốt lõi) BẮT BUỘC đạt
- [ ] Có video demo navigation thành công + lặp lại được
- [ ] Tài liệu hóa giới hạn đã biết (GPS multipath gần tòa cao 5-8m)

---

## Giới hạn đã biết (ghi rõ để minh bạch — tăng độ tin cậy)

- GPS phone gần tòa cao bị multipath → lệch 5-8m dù báo ±4m (giới hạn vật lý, không phải bug)
- Auto-snap GPS-proximity tắt vì không tin được gần nhà; dùng VIO + manual snap chỗ thoáng
- Vị trí tuyệt đối phụ thuộc GPS first-fix; chuyển động (relative) chính xác nhờ VIO
- Muốn sub-meter gần nhà cần ARCore Geospatial API (Street View) — ngoài phạm vi hiện tại
