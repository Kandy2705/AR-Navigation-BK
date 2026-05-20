using System;
using UnityEngine;

/// <summary>
/// Logic để hỏi user xác nhận đã đến tòa và muốn chuyển sang indoor.
/// Theo yêu cầu UX (manual switch): KHÔNG tự động vào indoor khi GPS gần entrance,
/// chỉ chuyển sau khi user bấm nút "Vào trong" trên modal.
///
/// Cách dùng trong UI Toolkit:
///   var ctrl = new IndoorEntryConfirmController(rootVisualElement, switcher, registry);
///   ctrl.OnUserConfirmed += building => Debug.Log($"User confirmed enter {building}");
///   ctrl.Show(BuildingId.B10);
///
/// Hoặc cho luồng GPS auto-suggest: gọi <see cref="MaybeShowForGpsProximity"/> mỗi vài giây
/// với tọa độ user hiện tại.
/// </summary>
public class IndoorEntryConfirmController
{
    public event Action<BuildingId> OnUserConfirmed;
    public event Action<BuildingId> OnUserDismissed;

    private readonly BuildingRegistry _registry;
    private readonly IndoorMapSwitcher _switcher;
    private BuildingId _lastSuggested = BuildingId.None;

    public IndoorEntryConfirmController(BuildingRegistry registry, IndoorMapSwitcher switcher)
    {
        _registry = registry;
        _switcher = switcher;
    }

    /// <summary>Gọi khi user explicit chọn tòa từ menu (vd dropdown outdoor).</summary>
    public void TriggerConfirm(BuildingId target)
    {
        if (_registry == null) return;
        var entry = _registry.Find(target);
        if (entry == null) return;

        _lastSuggested = target;
        // Modal/UI thực sự sẽ được tích hợp vào UI Toolkit ở Controller cụ thể.
        // Ở đây chỉ phơi event để view layer hook vào.
        Debug.Log($"[IndoorEntryConfirm] Suggest entry: {entry.displayName} ({entry.mapsetCode})");
    }

    /// <summary>
    /// Gọi từ outdoor loop với tọa độ user hiện tại. Nếu user nằm trong vùng entrance của
    /// một tòa nào đó (chưa được suggest), trigger event suggest. UI tự hiển thị modal.
    /// </summary>
    public void MaybeShowForGpsProximity(double userLat, double userLon)
    {
        if (_registry == null) return;

        BuildingId nearest = BuildingId.None;
        float nearestDistance = float.PositiveInfinity;

        foreach (var b in _registry.Buildings)
        {
            if (b == null || b.entranceTriggerRadiusMeters <= 0f) continue;

            float distance = HaversineMeters(userLat, userLon, b.entranceLatitude, b.entranceLongitude);
            if (distance <= b.entranceTriggerRadiusMeters && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = b.id;
            }
        }

        if (nearest != BuildingId.None && nearest != _lastSuggested)
        {
            TriggerConfirm(nearest);
        }
        else if (nearest == BuildingId.None && _lastSuggested != BuildingId.None)
        {
            // User đã ra khỏi vùng entrance — reset để lần sau có thể suggest lại.
            _lastSuggested = BuildingId.None;
        }
    }

    /// <summary>UI gọi khi user bấm "Vào trong tòa".</summary>
    public void Confirm(BuildingId building)
    {
        if (_switcher != null)
        {
            _switcher.SwitchTo(building);
        }
        OnUserConfirmed?.Invoke(building);
    }

    /// <summary>UI gọi khi user bấm "Để sau".</summary>
    public void Dismiss(BuildingId building)
    {
        OnUserDismissed?.Invoke(building);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static float HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000d; // Earth radius (m)
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);

        double a = Math.Sin(dLat * 0.5d) * Math.Sin(dLat * 0.5d) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon * 0.5d) * Math.Sin(dLon * 0.5d);
        double c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return (float)(R * c);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
