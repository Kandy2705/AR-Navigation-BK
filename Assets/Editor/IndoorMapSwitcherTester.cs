#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu: Tools/Indoor/Test Switch B9 và Tools/Indoor/Test Switch B10.
/// Tìm IndoorMapSwitcher trong scene rồi gọi SwitchTo(target).
/// Dùng để test nhanh khi đang chỉnh code, không cần play mode.
/// </summary>
public static class IndoorMapSwitcherTester
{
    [MenuItem("Tools/Indoor/Test Switch to B9")]
    public static void SwitchB9() => DoSwitch(BuildingId.B9);

    [MenuItem("Tools/Indoor/Test Switch to B10")]
    public static void SwitchB10() => DoSwitch(BuildingId.B10);

    [MenuItem("Tools/Indoor/Clear Indoor")]
    public static void Clear()
    {
        var s = Object.FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        if (s == null) { Debug.LogError("Không tìm thấy IndoorMapSwitcher trong scene."); return; }
        s.Clear();
    }

    private static void DoSwitch(BuildingId id)
    {
        var s = Object.FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        if (s == null) { Debug.LogError("Không tìm thấy IndoorMapSwitcher trong scene."); return; }
        bool ok = s.SwitchTo(id);
        Debug.Log($"[Tester] SwitchTo({id}) = {ok}");
    }
}
#endif
