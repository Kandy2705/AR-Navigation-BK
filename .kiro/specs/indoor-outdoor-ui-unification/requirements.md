# Requirements Document

> Đồng bộ UI Indoor / Outdoor (HybridGPSMap)

## Introduction

Tài liệu này mô tả yêu cầu nghiệp vụ và tiêu chí chấp nhận cho việc thống nhất trải nghiệm giao diện (HUD) giữa hai chế độ AR navigation trong scene `HybridGPSMap`:

- **Outdoor mode**: dẫn đường ngoài trời dựa trên GPS, hiển thị POI tòa nhà trên minimap, sử dụng `HybridModeController` + `GpsAR` module.
- **Indoor mode**: dẫn đường trong tòa nhà dựa trên Multiset VPS (Visual Positioning System), mỗi tòa là một mapset riêng (B9 = `MAP_9LME2PB7Y3EN`, B10 = `MSET_AWDJFJNAVVFM`).

Hai chế độ hiện đang dùng UGUI riêng biệt, layout và visual không đồng nhất. Mục tiêu của feature là đưa cả hai HUD về một ngôn ngữ thiết kế chung, đồng thời chuẩn hóa luồng người dùng giữa Outdoor → Indoor → Outdoor sao cho mọi chuyển đổi đều do người dùng chủ động khởi xướng (manual switch), và indoor navigation chỉ được kích hoạt sau khi VPS đã định vị thành công.

Phạm vi tài liệu này chỉ bao phủ HUD trong scene `HybridGPSMap`. MainScreen của ứng dụng (UI Toolkit) nằm ngoài phạm vi điều chỉnh, nhưng quyết định công nghệ HUD phải được xem xét trong tương quan với MainScreen để đạt sự nhất quán toàn ứng dụng.

## Glossary

- **AR_Navigation_App**: Ứng dụng Unity `TestARMultiSet`, target Unity 6000.0.44f1.
- **Hybrid_Mode**: Chế độ hỗn hợp Outdoor / Indoor / Transition do `HybridModeController` quản lý. Tại mọi thời điểm chỉ một giá trị được giữ trong `CurrentMode`.
- **Outdoor_Mode**: Trạng thái Hybrid_Mode trong đó `outdoorEnvironment` được kích hoạt và GPS được dùng để định vị.
- **Indoor_Mode**: Trạng thái Hybrid_Mode trong đó `indoorEnvironment` được kích hoạt và Multiset VPS được dùng để định vị.
- **Transition_Mode**: Trạng thái trung gian khi đang fade overlay hoặc đang chờ permission.
- **Hybrid_HUD**: Lớp giao diện chung trong scene `HybridGPSMap`, gồm Header, Status_Panel, Action_Bar, Destination_List và Mode_Switcher.
- **Outdoor_HUD**: Tập con của Hybrid_HUD hiển thị khi đang ở Outdoor_Mode (Minimap, GPS_Accuracy_Indicator, Mobile Navigation HUD).
- **Indoor_HUD**: Tập con của Hybrid_HUD hiển thị khi đang ở Indoor_Mode (Localization_Panel, danh sách phòng, distance/heading).
- **Mode_Switcher**: Thành phần UI khởi xướng chuyển Outdoor ↔ Indoor; bọc lệnh `HybridModeController.ForceIndoor()` / `HybridModeController.ForceOutdoor()`.
- **Localization_Panel**: Thành phần UI trong Indoor_HUD hiển thị trạng thái VPS (NotScanned / Scanning / Localized / Failed) và nút "Quét Localization".
- **Localization_Status**: Một trong bốn giá trị {NotScanned, Scanning, Localized, Failed}.
- **Localized**: Trạng thái sau khi `MapLocalizationManager.LocalizeFrame()` trả thành công ít nhất một lần và chưa bị reset.
- **Localization_Scan**: Hành động người dùng bấm nút "Quét Localization" để gọi `MapLocalizationManager.LocalizeFrame()`.
- **VPS**: Visual Positioning System; ở đây cụ thể là Multiset VPS — định vị bằng cách so khớp camera frame với mapset đã quét trước.
- **Multiset**: SDK định vị thị giác (`com.multiset.sdk`) cung cấp `MapLocalizationManager`, `NavigationController`.
- **Mapset**: Một mapset Multiset — tập hợp các map đã merge, định danh bằng `MSET_xxx`. Một single map dùng tiền tố `MAP_xxx`.
- **Building**: Một tòa nhà có một mapset riêng. Hiện có B9 và B10. Định danh bằng `BuildingId` enum.
- **B9**: Tòa B9, mapset code `MAP_9LME2PB7Y3EN`, kind = Map.
- **B10**: Tòa B10, mapset code `MSET_AWDJFJNAVVFM`, kind = MapSet.
- **POI**: Point of Interest. Trong Outdoor_Mode đại diện tòa nhà; trong Indoor_Mode đại diện phòng/khu vực bên trong tòa. POI được gắn cứng vào model bản đồ (không lưu DB).
- **Destination_List**: Thành phần UI hiển thị danh sách POI có thể chọn làm điểm đích.
- **Active_Building**: Tòa nhà đang được load qua `IndoorMapSwitcher.CurrentBuilding`. Bằng `BuildingId.None` khi chưa chọn.
- **GPS_Accuracy**: Độ chính xác GPS hiện tại tính bằng mét, lấy từ `Input.location.lastData.horizontalAccuracy`.
- **Status_Panel**: Khu vực HUD hiển thị chỉ số định vị (GPS_Accuracy hoặc VPS quality), khoảng cách tới đích, hướng đi.

## Requirements

### Yêu cầu 1 — Khung HUD đồng bộ giữa Outdoor và Indoor

**User Story:** Là một người dùng AR, tôi muốn nhìn thấy cùng một bố cục Header / Status_Panel / Action_Bar khi chuyển giữa Outdoor và Indoor, để giảm tải nhận thức và biết chính xác phải nhìn vào đâu trong từng tình huống.

#### Acceptance Criteria

1. THE Hybrid_HUD SHALL trình bày Header, Status_Panel và Action_Bar tại cùng vị trí pixel-anchor (top, bottom-center) trong cả Outdoor_Mode và Indoor_Mode.
2. WHEN Hybrid_Mode thay đổi giữa Outdoor_Mode và Indoor_Mode, THE Hybrid_HUD SHALL giữ nguyên kích thước, màu nền và typography của Header, Status_Panel và Action_Bar.
3. THE Hybrid_HUD SHALL sử dụng một công nghệ render UI duy nhất cho toàn bộ HUD trong scene `HybridGPSMap` (UGUI hoặc UI Toolkit, nhưng không trộn lẫn trong cùng một panel).
4. WHERE phần MainScreen của AR_Navigation_App đang dùng UI Toolkit, THE Hybrid_HUD SHALL ghi nhận lựa chọn công nghệ và lý do trong tài liệu thiết kế trước khi triển khai.
5. THE Hybrid_HUD SHALL phơi ra ít nhất ba slot có thể tái sử dụng (HeaderSlot, StatusSlot, ActionSlot) để Outdoor_HUD và Indoor_HUD bơm nội dung riêng vào.

### Yêu cầu 2 — Mode_Switcher do người dùng khởi xướng

**User Story:** Là một người dùng AR, tôi muốn tự bấm nút để chuyển từ Outdoor sang Indoor (và ngược lại), để kiểm soát thời điểm chuyển chế độ và tránh ứng dụng tự đổi mode khi tôi chưa sẵn sàng.

#### Acceptance Criteria

1. WHEN Hybrid_Mode bằng Outdoor_Mode, THE Mode_Switcher SHALL hiển thị nút "Vào tòa" trong Action_Bar.
2. WHEN Hybrid_Mode bằng Indoor_Mode, THE Mode_Switcher SHALL hiển thị nút "Rời tòa" trong Action_Bar.
3. WHEN người dùng bấm nút "Vào tòa", THE Mode_Switcher SHALL gọi `HybridModeController.ForceIndoor()` thông qua `IndoorMapSwitcher.EnterIndoor(BuildingId)` với BuildingId của Active_Building được chọn.
4. WHEN người dùng bấm nút "Rời tòa", THE Mode_Switcher SHALL gọi `IndoorMapSwitcher.ExitToOutdoor()`.
5. THE Mode_Switcher SHALL chuyển Hybrid_Mode chỉ khi nhận được sự kiện bấm nút từ người dùng (user-initiated).
6. IF không có Active_Building được chọn, THEN THE Mode_Switcher SHALL vô hiệu hóa nút "Vào tòa" và hiển thị gợi ý "Chọn tòa cần đến" trong Action_Bar.
7. IF người dùng bấm "Rời tòa" trong khi Localization_Status bằng Scanning, THEN THE Mode_Switcher SHALL hủy lệnh quét đang chạy trước khi gọi `ExitToOutdoor()`.
8. THE Mode_Switcher SHALL không phụ thuộc vào timer tự động nào để chuyển Hybrid_Mode (auto-switch tắt vĩnh viễn ở mọi luồng người dùng cuối).

### Yêu cầu 3 — Localization_Panel và luồng quét VPS

**User Story:** Là một người dùng đã đi vào tòa nhà, tôi muốn thấy rõ một nút "Quét Localization" và trạng thái quét hiện tại, để biết khi nào tôi đã được định vị và có thể bắt đầu chọn phòng.

#### Acceptance Criteria

1. WHEN Hybrid_Mode chuyển sang Indoor_Mode, THE Localization_Panel SHALL khởi tạo Localization_Status bằng `NotScanned`.
2. WHILE Localization_Status bằng `NotScanned`, THE Localization_Panel SHALL hiển thị nút "Quét Localization" cùng hướng dẫn "Hướng camera vào khu vực có dấu hiệu nhận diện và bấm quét".
3. WHEN người dùng bấm "Quét Localization", THE Localization_Panel SHALL chuyển Localization_Status sang `Scanning` và gọi `MapLocalizationManager.LocalizeFrame()` đúng một lần.
4. WHILE Localization_Status bằng `Scanning`, THE Localization_Panel SHALL hiển thị progress indicator và vô hiệu hóa nút "Quét Localization".
5. WHEN `MapLocalizationManager` báo thành công, THE Localization_Panel SHALL chuyển Localization_Status sang `Localized` và phát sự kiện `OnLocalized(BuildingId)`.
6. IF `MapLocalizationManager` báo thất bại, THEN THE Localization_Panel SHALL chuyển Localization_Status sang `Failed` và hiển thị thông điệp "Chưa định vị được — thử lại với góc khác".
7. WHEN Localization_Status bằng `Failed`, THE Localization_Panel SHALL kích hoạt lại nút "Quét Localization" để cho phép quét lại.
8. WHEN Hybrid_Mode rời khỏi Indoor_Mode (qua `ExitToOutdoor()` hoặc `Clear()`), THE Localization_Panel SHALL reset Localization_Status về `NotScanned`.
9. IF Localization_Status duy trì giá trị `Scanning` quá 15 giây, THEN THE Localization_Panel SHALL chuyển Localization_Status sang `Failed` và hiển thị thông điệp timeout.

### Yêu cầu 4 — Cổng vào navigation indoor sau khi Localized

**User Story:** Là một người dùng vừa quét VPS xong, tôi muốn ứng dụng chỉ cho tôi chọn phòng đích sau khi tôi đã được định vị, để tránh dẫn đường sai khi VPS chưa biết tôi đang ở đâu.

#### Acceptance Criteria

1. WHILE Localization_Status khác `Localized`, THE Indoor_HUD SHALL vô hiệu hóa Destination_List và hiển thị placeholder "Bấm Quét Localization để bắt đầu chọn phòng".
2. WHEN Localization_Status chuyển sang `Localized`, THE Indoor_HUD SHALL kích hoạt Destination_List với danh sách POI thuộc Active_Building.
3. WHEN người dùng chọn một POI từ Destination_List trong Indoor_Mode, THE Indoor_HUD SHALL gọi Multiset `NavigationController.StartNavigation(targetPoi)`.
4. IF Localization_Status chuyển từ `Localized` về `Failed` trong khi navigation đang chạy, THEN THE Indoor_HUD SHALL dừng navigation và hiển thị banner "Mất định vị — bấm Quét Localization để tiếp tục".
5. THE Indoor_HUD SHALL không khởi tạo navigation indoor mà không đi qua trạng thái Localization_Status = `Localized` ít nhất một lần trong phiên Indoor_Mode hiện tại.

### Yêu cầu 5 — Destination_List thống nhất giữa Outdoor và Indoor

**User Story:** Là một người dùng, tôi muốn cùng một cách thao tác để chọn điểm đến — dù là chọn tòa nhà ngoài trời hay chọn phòng bên trong tòa — để không phải học hai luồng khác nhau.

#### Acceptance Criteria

1. THE Destination_List SHALL dùng cùng một mẫu hàng (icon, tiêu đề, phụ đề khoảng cách) cho cả danh sách tòa nhà (Outdoor_Mode) và danh sách phòng (Indoor_Mode).
2. WHEN Hybrid_Mode bằng Outdoor_Mode, THE Destination_List SHALL nạp các tòa nhà từ `BuildingRegistry.Buildings`.
3. WHEN Hybrid_Mode bằng Indoor_Mode AND Localization_Status bằng `Localized`, THE Destination_List SHALL nạp các POI con của `BuildingSceneBindings.Find(Active_Building).poiContainer`.
4. WHEN người dùng chọn một mục trong Destination_List ở Outdoor_Mode, THE Destination_List SHALL ghi nhận mục đó là Active_Building và hiển thị tuyến GPS dẫn đến `entranceLatitude` / `entranceLongitude` của tòa.
5. WHEN người dùng chọn một mục trong Destination_List ở Indoor_Mode, THE Destination_List SHALL ghi nhận mục đó là target POI và phơi sự kiện `OnDestinationSelected(BuildingId, PoiId)`.
6. THE Destination_List SHALL sắp xếp các mục theo khoảng cách tăng dần từ vị trí hiện tại của người dùng.
7. IF Destination_List rỗng trong Outdoor_Mode, THEN THE Destination_List SHALL hiển thị thông điệp "Chưa có tòa nhà nào trong khu vực".
8. IF Destination_List rỗng trong Indoor_Mode, THEN THE Destination_List SHALL hiển thị thông điệp "Tòa này chưa có POI được cấu hình".

### Yêu cầu 6 — Status_Panel hiển thị chỉ số định vị

**User Story:** Là một người dùng, tôi muốn nhìn thấy ngay chất lượng định vị (GPS hoặc VPS), khoảng cách còn lại và hướng đi, để quyết định có tin tưởng tuyến dẫn đường hay không.

#### Acceptance Criteria

1. WHEN Hybrid_Mode bằng Outdoor_Mode, THE Status_Panel SHALL hiển thị GPS_Accuracy bằng số mét lấy từ `Input.location.lastData.horizontalAccuracy`.
2. WHEN Hybrid_Mode bằng Indoor_Mode AND Localization_Status bằng `Localized`, THE Status_Panel SHALL hiển thị nhãn "Đã định vị" cùng tên Active_Building.
3. WHEN Hybrid_Mode bằng Indoor_Mode AND Localization_Status khác `Localized`, THE Status_Panel SHALL hiển thị nhãn Localization_Status hiện tại bằng tiếng Việt ("Chưa quét" / "Đang quét" / "Định vị thất bại").
4. WHILE người dùng có Active_Building hoặc target POI, THE Status_Panel SHALL hiển thị khoảng cách tới đích đo bằng mét, làm tròn đến đơn vị mét.
5. WHILE người dùng có Active_Building hoặc target POI, THE Status_Panel SHALL hiển thị mũi tên hướng đi tương đối so với hướng đầu của người dùng.
6. IF GPS_Accuracy lớn hơn 30 mét trong Outdoor_Mode, THEN THE Status_Panel SHALL đổi màu chỉ số GPS_Accuracy sang màu cảnh báo và hiển thị nhãn "GPS yếu".
7. IF `Input.location` không hoạt động trong Outdoor_Mode, THEN THE Status_Panel SHALL hiển thị nhãn "Không có GPS" và ẩn chỉ số khoảng cách.

### Yêu cầu 7 — Empty và error states

**User Story:** Là một người dùng, tôi muốn được dẫn dắt rõ ràng khi gặp tình huống bất thường (chưa chọn tòa, mất GPS, mất VPS), để tôi biết phải làm gì tiếp theo thay vì nhìn vào HUD trống.

#### Acceptance Criteria

1. WHILE Hybrid_Mode bằng Outdoor_Mode AND Active_Building bằng `BuildingId.None`, THE Outdoor_HUD SHALL hiển thị empty state với thông điệp "Chọn tòa nhà từ danh sách để bắt đầu" và ẩn nút "Vào tòa".
2. WHILE Hybrid_Mode bằng Indoor_Mode AND Localization_Status bằng `NotScanned`, THE Indoor_HUD SHALL hiển thị empty state Localization_Panel chiếm khu vực Destination_List.
3. IF GPS_Accuracy lớn hơn 30 mét liên tục trong 5 giây trong Outdoor_Mode, THEN THE Outdoor_HUD SHALL hiển thị banner "GPS yếu — đứng yên 5 giây để cải thiện độ chính xác".
4. IF Multiset báo lỗi mạng khi gọi `LocalizeFrame()`, THEN THE Localization_Panel SHALL hiển thị thông điệp "Mất kết nối tới Multiset — kiểm tra mạng".
5. IF Localization_Status chuyển từ `Localized` sang `Failed` trong khi đang ở Indoor_Mode, THEN THE Indoor_HUD SHALL hiển thị banner cảnh báo và giữ Hybrid_Mode bằng Indoor_Mode (không tự chuyển ra Outdoor_Mode).
6. WHEN người dùng bấm "Rời tòa" trong banner cảnh báo mất VPS, THE Mode_Switcher SHALL gọi `IndoorMapSwitcher.ExitToOutdoor()`.
7. IF Active_Building đã chọn nhưng `BuildingSceneBindings.TryGet(Active_Building, ...)` trả về false, THEN THE Indoor_HUD SHALL hiển thị thông điệp lỗi "Tòa này chưa được cấu hình trong scene" và quay về Outdoor_Mode sau khi người dùng xác nhận.

### Yêu cầu 8 — Khả năng mở rộng số lượng tòa nhà

**User Story:** Là một developer, tôi muốn thêm tòa nhà mới mà không phải sửa code HUD, để chi phí mở rộng giữ ở mức thấp khi đội triển khai thêm B11, B12 trong tương lai.

#### Acceptance Criteria

1. THE Hybrid_HUD SHALL nạp danh sách tòa nhà cho Destination_List trực tiếp từ `BuildingRegistry.Buildings` mà không hard-code BuildingId nào.
2. THE Hybrid_HUD SHALL nạp danh sách POI indoor cho Destination_List trực tiếp từ `BuildingSceneBindings.Find(Active_Building).poiContainer` mà không hard-code BuildingId nào.
3. WHERE người dùng thêm một entry mới vào `BuildingRegistry`, THE Hybrid_HUD SHALL hiển thị tòa mới trong Destination_List sau khi scene `HybridGPSMap` được mở lại, mà không cần thay đổi code Hybrid_HUD.
4. THE Hybrid_HUD SHALL chỉ tham chiếu các BuildingId qua `BuildingId` enum và chuỗi hiển thị qua `BuildingRegistry.Entry.displayName`.

### Yêu cầu 9 — Outdoor là điểm vào duy nhất của phiên AR

**User Story:** Là một người dùng AR, tôi muốn mỗi phiên AR luôn bắt đầu ở Outdoor mode để có thời gian định vị GPS trước khi tiến vào tòa, vì VPS chỉ chạy được khi đã ở bên trong tòa nhà.

#### Acceptance Criteria

1. WHEN người dùng vào AR scene từ MainScreen (qua `NavigationManager.EnterARPage()`), THE AR_Navigation_App SHALL khởi tạo Hybrid_Mode bằng Outdoor_Mode.
2. WHEN người dùng quay lại AR scene sau khi đã thoát ra MainScreen, THE AR_Navigation_App SHALL reset Hybrid_Mode về Outdoor_Mode kể cả khi phiên trước kết thúc ở Indoor_Mode.
3. THE AR_Navigation_App SHALL không cho phép chuyển sang Indoor_Mode trừ khi Hybrid_Mode đã ổn định ở Outdoor_Mode ít nhất một frame trong phiên hiện tại.
4. WHILE Hybrid_Mode bằng Outdoor_Mode trong giai đoạn khởi đầu phiên, THE Outdoor_HUD SHALL hiển thị Status_Panel và Destination_List ngay cả khi GPS chưa sẵn sàng (chỉ vô hiệu hóa nút "Vào tòa" cho đến khi user chọn Active_Building).

### Yêu cầu 10 — Đặc tính bất biến của Hybrid_Mode

**User Story:** Là một QA engineer, tôi muốn các bất biến cốt lõi của Hybrid_Mode có thể kiểm thử tự động, để tránh hồi quy khi mã nguồn HUD và `HybridModeController` được sửa song song.

#### Acceptance Criteria

1. THE AR_Navigation_App SHALL giữ tại mọi thời điểm đúng một giá trị `HybridModeController.CurrentMode` thuộc tập {Outdoor, Indoor, Transition}.
2. WHILE `HybridModeController.CurrentMode` bằng Outdoor, THE AR_Navigation_App SHALL giữ `outdoorEnvironment.activeSelf` bằng true và `indoorEnvironment.activeSelf` bằng false (trừ khi cờ `keepIndoorActiveWhileOutdoor` được bật trong cấu hình Editor).
3. WHILE `HybridModeController.CurrentMode` bằng Indoor, THE AR_Navigation_App SHALL giữ `indoorEnvironment.activeSelf` bằng true và `outdoorEnvironment.activeSelf` bằng false (trừ khi cờ `keepOutdoorActiveWhileIndoor` được bật trong cấu hình Editor).
4. THE Mode_Switcher SHALL không gọi `ForceIndoor()` hoặc `ForceOutdoor()` từ bất kỳ callback nào ngoại trừ handler của sự kiện bấm nút người dùng.
5. WHEN một chuỗi `EnterIndoor(B)` rồi `ExitToOutdoor()` được thực thi, THE AR_Navigation_App SHALL kết thúc với `HybridModeController.CurrentMode` bằng Outdoor và `IndoorMapSwitcher.CurrentBuilding` bằng `BuildingId.None`.
6. WHEN `HybridModeController.ForceIndoor()` được gọi nhiều lần liên tiếp trong khi đang ở Indoor_Mode, THE AR_Navigation_App SHALL giữ nguyên trạng thái quan sát được (idempotent).
7. WHEN `HybridModeController.ForceOutdoor()` được gọi nhiều lần liên tiếp trong khi đang ở Outdoor_Mode, THE AR_Navigation_App SHALL giữ nguyên trạng thái quan sát được (idempotent).

## Đặc tính đúng đắn cho kiểm thử dựa trên thuộc tính

Các bất biến dưới đây là property-based testing targets ánh xạ trực tiếp từ Yêu cầu 2, 4, 9 và 10. Mỗi đặc tính được phát biểu dưới dạng property over traces để có thể sinh dữ liệu ngẫu nhiên (chuỗi sự kiện UI / lệnh `HybridModeController`) và kiểm tra tự động.

- **P1 — Mutex Mode (invariant):** Với mọi trace lệnh hợp lệ áp lên `HybridModeController`, tại mọi thời điểm sau khi `ApplyMode` chạy xong, không tồn tại trạng thái mà cả `outdoorEnvironment.activeSelf` và `indoorEnvironment.activeSelf` đều bằng true (khi hai cờ `keepIndoorActiveWhileOutdoor` và `keepOutdoorActiveWhileIndoor` đều bằng false).
- **P2 — Indoor Navigation Gate (invariant):** Với mọi trace sự kiện UI áp lên Indoor_HUD, mọi lần `NavigationController.StartNavigation` được gọi đều đứng sau ít nhất một sự kiện `OnLocalized` trong cùng phiên Indoor_Mode hiện tại.
- **P3 — User-Initiated Mode Switch (invariant):** Với mọi trace sự kiện kết hợp (timer tick + GPS tick + VPS tick + user tap) khi `autoSwitchEnabled` bằng false, mọi transition `CurrentMode` từ Outdoor sang Indoor (hoặc ngược lại) đều có nguyên nhân là user tap; không có transition nào chỉ do GPS / VPS / timer gây ra.
- **P4 — Mode Round-trip:** Với mọi `BuildingId` B hợp lệ, chuỗi `[EnterIndoor(B), ExitToOutdoor()]` áp lên trạng thái khởi tạo Outdoor sẽ kết thúc với `CurrentMode` bằng Outdoor và `IndoorMapSwitcher.CurrentBuilding` bằng `BuildingId.None`, bất kể nội dung của `B`.
- **P5 — Idempotence của ForceIndoor / ForceOutdoor:** `ForceIndoor()` áp lên trạng thái Indoor cho ra trạng thái quan sát được giống hệt; tương tự cho `ForceOutdoor()` áp lên trạng thái Outdoor. Phát biểu hình thức: `apply(ForceIndoor, apply(ForceIndoor, s)) ≡ apply(ForceIndoor, s)` khi `s.CurrentMode == Indoor`.
- **P6 — Building Selection Consistency:** Với mọi `BuildingId` B hiển thị trên Destination_List rồi được người dùng chọn và xác nhận, sau khi `IndoorMapSwitcher.EnterIndoor(B)` chạy thành công thì `IndoorMapSwitcher.CurrentBuilding == B` (no aliasing giữa giá trị hiển thị và giá trị áp dụng).
- **P7 — Liveness của Localization timeout (error condition):** Với mọi trace mà `MapLocalizationManager` không phát sự kiện success trong 15 giây sau lệnh quét, `Localization_Status` chuyển sang `Failed` trong khoảng 15 giây ± dung sai 1 giây; `Localization_Status` không kẹt mãi mãi ở `Scanning`.
- **P8 — Confluence của empty / error state:** Với mọi cặp sự kiện (mất GPS, người dùng chưa chọn tòa) áp lên Outdoor_HUD, kết quả hiển thị HUD không phụ thuộc vào thứ tự áp dụng các sự kiện đó (hai trace hoán vị cho ra cùng một trạng thái HUD quan sát được).

## Lưu ý phạm vi và giả định

- Quyết định công nghệ HUD (UGUI vs UI Toolkit) được hoãn sang phase Design; tài liệu requirements chỉ ràng buộc *tính đồng nhất* của lựa chọn cuối cùng (Yêu cầu 1 mục 3 và 4).
- Auto-switch giữa Outdoor và Indoor trong code `HybridModeController` (`autoSwitchEnabled`, `indoorLostToOutdoorDelay`, `indoorSuccessRequiredTime`) phải được giữ tắt cho luồng người dùng cuối; cờ này chỉ tồn tại để debug trong Editor.
- Multiset SDK được gọi qua reflection trong `IndoorMapSwitcher` để tránh hard-link DLL; mọi yêu cầu nhắc tên `MapLocalizationManager` hay `NavigationController` đều ngầm hiểu sử dụng cách gọi reflection hiện hành.
- POI được gắn cứng trong scene model, không lưu trong DB; do đó Destination_List indoor không cần luồng đồng bộ remote.
- Phạm vi tài liệu này không bao gồm flow đăng nhập, lịch sử dẫn đường, hay chỉnh sửa POI; các tính năng đó nằm ngoài scene `HybridGPSMap`.
