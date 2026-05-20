# Bugfix Requirements Document — Indoor B9 Build Fix

## Introduction

Trên build APK thật (Android, Unity 6000.0.44f1 + Multiset SDK), khi user vào AR ở scene `HybridGPSMap.unity`, đi từ Outdoor sang Indoor (bấm nút "Indoor" trên runtime mode switcher do `HybridModeController` tạo), rồi quét tòa **B9** (`MAP_9LME2PB7Y3EN`, kind = `Map`), xuất hiện 3 triệu chứng song song không có trong Editor Play Mode:

1. **POI sai tòa** — sau khi localize thành công, các row POI hiển thị khoảng cách (mét) là POI tòa **B10**, không phải các POI thuộc `MapB9/POIs-B9` mà user đã đặt sẵn.
2. **Mesh tím che camera** — sau localize, lớp mesh quét VPS (visualization của `MapMeshHandler`) phủ lên feed camera thật, làm mất khả năng quan sát môi trường thực.
3. **Camera vs XR Origin lệch** — cảm giác `Camera.main` (cái SDK đang dùng để localize / tính `AgentPosition`) không trùng với indoor ARCamera đang render lên màn hình; outdoor XROrigin đã được `HybridModeController.detachOutdoorXrRigFromEnvironment` reparent ra scene root, indoor có XROrigin riêng — không có đảm bảo runtime rằng đúng camera được tag `MainCamera` khi Indoor mode active.

Bản fix này **chỉ scope cho B9** (single Map). B10 (MapSet) sẽ xử lý ở phase sau. Mục tiêu: trên build APK, sau khi user bấm "Indoor" và quét B9, ứng dụng phải hiển thị đúng POI B9, không phủ mesh tím, và `Camera.main` mà Multiset SDK quan sát phải là camera đang render indoor thật.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN user đang ở Outdoor mode trên build APK AND user bấm nút "Indoor" trên runtime mode switcher (gọi `HybridModeController.ForceIndoor()`) AND `IndoorMapSwitcher.SwitchTo(BuildingId.B9)` chưa từng được gọi trong session THEN `MapLocalizationManager.mapOrMapsetCode` giữ nguyên giá trị scene-default (không bảo đảm là `MAP_9LME2PB7Y3EN` của B9), khiến cloud có thể trả pose theo map tòa khác.

1.2 WHEN user đang ở Indoor mode targeting B9 AND `MapB9.activeSelf == true` AND `MapB10.activeSelf == true` THEN `BuildingDestinationListController` / `PathEstimationUtils.EstimateDistanceToPosition(poi)` chạy trên cả `POIs-B9` lẫn `POIs-B10` cùng lúc, làm POI B10 bị tính khoảng cách và hiển thị xen kẽ với POI B9.

1.3 WHEN user đang ở Indoor mode targeting B9 trên build APK (không phải Editor) AND localize thành công THEN `MapMeshHandler.meshVisualizationOption == EnableVisualization`, khiến SDK render mesh quét tím che lên camera feed thực.

1.4 WHEN user đang ở Indoor mode targeting B9 trên build APK AND `HybridModeController.detachOutdoorXrRigFromEnvironment == true` AND `disableIndoorXROriginDuplicates == true` THEN `Camera.main` mà `MultisetIndoorBootstrap` reflection patch vào `NavigationController.ARCamera` không có đảm bảo runtime là camera con của indoor ARCamera đang active — có khả năng trỏ về outdoor camera đã detach ra scene root nếu thứ tự apply tag chưa đúng.

### Expected Behavior (Correct)

2.1 WHEN user bấm nút "Indoor" trên runtime mode switcher với target là B9 (single Map) THEN the system SHALL gọi `IndoorMapSwitcher.SwitchTo(BuildingId.B9)` (hoặc đường dẫn tương đương) TRƯỚC khi scan localize, đảm bảo `MapLocalizationManager.mapOrMapsetCode == "MAP_9LME2PB7Y3EN"` AND `MapLocalizationManager.localizationType == Map`.

2.2 WHEN user vào Indoor mode targeting B9 THEN the system SHALL chỉ giữ `MapB9.activeSelf == true` AND set tất cả building roots khác (bao gồm `MapB10`) thành `activeSelf == false`, bất kể trạng thái scene-default.

2.3 WHEN user đang ở Indoor mode targeting B9 trên build APK (`!Application.isEditor`) AND localize thành công THEN the system SHALL set `MapMeshHandler.meshVisualizationOption = DisableVisualization` để không render mesh tím lên camera feed thực.

2.4 WHEN user vào Indoor mode targeting B9 THEN the system SHALL bảo đảm `Camera.main` là camera đang được render bởi indoor ARCamera (con của indoor XROrigin chưa bị disable), AND `NavigationController.ARCamera` reference cùng camera đó AND camera này là camera duy nhất đang có tag `MainCamera` ở thời điểm sau khi `ForceIndoor` hoàn tất.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN user ở Outdoor mode trên build APK THEN the system SHALL CONTINUE TO chạy GPS navigation đúng như hiện tại (ARPathFinder vẽ đường, MobileNavigationHUD hiển thị khoảng cách, GPS accuracy circle hoạt động) — fix chỉ chạy khi mode chuyển sang Indoor.

3.2 WHEN user chạy scene `HybridGPSMap` trong Unity Editor Play Mode THEN the system SHALL CONTINUE TO hiển thị mesh tím (visualization) sau localize để verify alignment giống hành vi hiện tại; fix chỉ disable mesh trên build APK thật.

3.3 WHEN user ở Indoor mode targeting B9 AND đã localize thành công THEN the system SHALL CONTINUE TO hiển thị danh sách POI B9 (8 POI thuộc `POIs-B9`) AND tính khoảng cách qua `PathEstimationUtils.EstimateDistanceToPosition` AND vẽ đường qua `NavigationController.SetPOIForNavigation` đúng như hành vi đã hoạt động trong Editor Play Mode.

3.4 WHEN user thoát AR (bấm "Lịch sử/Cài đặt" hoặc back) AND quay lại MainScreen THEN the system SHALL CONTINUE TO chạy `HybridModeController.DeactivateARMode()` đúng (tắt indoor + outdoor environment, reset transition overlay) như hành vi hiện tại.

3.5 WHEN code chạy với `IndoorMapSwitcher` đã được gọi `SwitchTo(B9)` thành công THEN the system SHALL CONTINUE TO update `MapLocalizationManager.mapOrMapsetCode` và `localizationType` qua reflection AND tắt building roots khác đúng như hành vi hiện tại — fix chỉ bổ sung trigger tự động, không thay đổi semantics của `SwitchTo`.

3.6 WHEN code reflection set field trên `MapLocalizationManager` hoặc `MapMeshHandler` THEN the system SHALL CONTINUE TO **không hard-link** Multiset SDK DLL (giữ pattern `MonoBehaviour + GetField` đang dùng), tránh phá vỡ build pipeline IL2CPP nếu SDK đổi version.
