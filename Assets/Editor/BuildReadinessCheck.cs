using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Text;

/// <summary>
/// Kiểm tra toàn bộ điều kiện cần thiết để navigation path hoạt động đúng trên thiết bị.
/// Menu: Tools → Fix AR → Build Readiness Check
/// </summary>
public static class BuildReadinessCheck
{
    private struct Check
    {
        public bool pass;
        public string label;
        public string detail;
        public bool critical;
    }

    [MenuItem("Tools/Fix AR/Build Readiness Check")]
    public static void Execute()
    {
        var checks = new System.Collections.Generic.List<Check>();

        // ── 1. AR Session: chỉ 1 cái active ─────────────────────────────────
        {
            int activeCount = 0;
            foreach (var s in Object.FindObjectsByType<UnityEngine.XR.ARFoundation.ARSession>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (s.gameObject.activeInHierarchy) activeCount++;

            checks.Add(new Check {
                pass = activeCount == 1,
                label = $"AR Session count (active) = {activeCount}",
                detail = activeCount == 1 ? "Chỉ SharedARRig > AR Session chạy." :
                         activeCount == 0 ? "Không có AR Session nào active!" :
                         $"{activeCount} AR Sessions active → camera đen/tracking lỗi. Deactivate cái trong OutdoorEnvironment.",
                critical = true
            });
        }

        // ── 2. SimpleGPSTracker: xrOrigin và arCamera (private → dùng SerializedObject) ──
        {
            SimpleGPSTracker gps = Object.FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
            if (gps != null)
            {
                SerializedObject so = new SerializedObject(gps);
                Object xrRef  = so.FindProperty("xrOrigin")?.objectReferenceValue;
                Object camRef = so.FindProperty("arCamera")?.objectReferenceValue;
                bool hasXr  = xrRef  != null;
                bool hasCam = camRef != null;
                checks.Add(new Check {
                    pass = hasXr,
                    label = "SimpleGPSTracker.xrOrigin assigned",
                    detail = hasXr ? $"→ {xrRef.name}" : "NULL → GPS sẽ không di chuyển camera theo vị trí thực. Chạy Auto Wire.",
                    critical = true
                });
                checks.Add(new Check {
                    pass = hasCam,
                    label = "SimpleGPSTracker.arCamera assigned",
                    detail = hasCam ? $"→ {camRef.name}" : "NULL → Compass/North alignment thất bại. Chạy Auto Wire.",
                    critical = true
                });
            }
            else
            {
                checks.Add(new Check { pass = false, label = "SimpleGPSTracker not found", detail = "Không tìm thấy SimpleGPSTracker.", critical = true });
            }
        }

        // ── 3. ARPathFinder: camera resolve ──────────────────────────────────
        {
            ARPathFinder pf = Object.FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
            bool found = pf != null;
            checks.Add(new Check {
                pass = found,
                label = "ARPathFinder tồn tại trong scene",
                detail = found ? $"→ trên GO '{pf.gameObject.name}'" : "Không tìm thấy ARPathFinder!",
                critical = true
            });
            if (found)
            {
                // prioritizePathVisibility
                SerializedObject so = new SerializedObject(pf);
                bool prioritize = so.FindProperty("prioritizePathVisibility")?.boolValue ?? false;
                bool fallback   = so.FindProperty("showStraightLineFallbackWhenNavMeshFails")?.boolValue ?? false;
                checks.Add(new Check {
                    pass = prioritize,
                    label = "ARPathFinder.prioritizePathVisibility = true",
                    detail = prioritize ? "GPS gate bypassed ✅" : "⚠️ GPS gate CÓ THỂ block path. Bật lên trong Inspector.",
                    critical = false
                });
                checks.Add(new Check {
                    pass = fallback,
                    label = "ARPathFinder.showStraightLineFallbackWhenNavMeshFails = true",
                    detail = fallback ? "Nếu NavMesh lỗi → straight line" : "⚠️ Nếu NavMesh lỗi → path KHÔNG hiện.",
                    critical = false
                });
            }
        }

        // ── 4. TargetAnchor: ít nhất 1 cái ──────────────────────────────────
        {
            TargetAnchor[] anchors = Object.FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            checks.Add(new Check {
                pass = anchors.Length > 0,
                label = $"TargetAnchor count = {anchors.Length}",
                detail = anchors.Length > 0 ? string.Join(", ", System.Array.ConvertAll(anchors, a => a.name)) :
                         "Không có TargetAnchor → MobileNavigationHUD không có điểm đến!",
                critical = true
            });
        }

        // ── 5. MapOrigin tồn tại ─────────────────────────────────────────────
        {
            MapOrigin mo = Object.FindFirstObjectByType<MapOrigin>(FindObjectsInactive.Include);
            checks.Add(new Check {
                pass = mo != null,
                label = "MapOrigin tồn tại",
                detail = mo != null ? $"→ {mo.gameObject.name}" : "Không có MapOrigin → GPS coordinates không convert được.",
                critical = true
            });
        }

        // ── 6. NavMesh baked ─────────────────────────────────────────────────
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            bool hasMesh = tri.vertices.Length > 0;
            checks.Add(new Check {
                pass = hasMesh,
                label = $"NavMesh baked ({tri.vertices.Length} vertices)",
                detail = hasMesh ? "NavMesh có thể dùng cho path routing." :
                         "NavMesh chưa bake → path sẽ là straight line. Vào Window → AI → Navigation → Bake.",
                critical = false
            });
        }

        // ── 7. HybridModeController: activateInitialModeOnStart ──────────────
        {
            HybridModeController hmc = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
            if (hmc != null)
            {
                SerializedObject so = new SerializedObject(hmc);
                bool autoStart = so.FindProperty("activateInitialModeOnStart")?.boolValue ?? false;
                bool hasNavMgr = Object.FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null;
                bool ok = !autoStart || hasNavMgr; // nếu autoStart=true thì phải có NavigationManager để intercept
                checks.Add(new Check {
                    pass = ok,
                    label = "HybridModeController không auto-activate trước onboarding",
                    detail = ok ? (autoStart ? "activateInitialModeOnStart=true nhưng NavigationManager sẽ intercept ✅" : "activateInitialModeOnStart=false ✅") :
                             "activateInitialModeOnStart=true và không có NavigationManager → AR bật ngay khi load!",
                    critical = true
                });
            }
        }

        // ── 8. NavigationControllerSetup ─────────────────────────────────────
        {
            NavigationController sdkCtrl = Object.FindFirstObjectByType<NavigationController>(FindObjectsInactive.Include);
            bool hasSetup = sdkCtrl != null && sdkCtrl.GetComponent<NavigationControllerSetup>() != null;
            checks.Add(new Check {
                pass = sdkCtrl == null || hasSetup,
                label = "NavigationControllerSetup added",
                detail = sdkCtrl == null ? "NavigationController (SDK) không tìm thấy" :
                         hasSetup ? "NavigationControllerSetup có mặt ✅" :
                         "Thiếu NavigationControllerSetup → SphereCollider error. Chạy Auto Wire.",
                critical = false
            });
        }

        // ── Render report ─────────────────────────────────────────────────────
        var sb = new StringBuilder();
        int passCount = 0, failCritical = 0, failWarn = 0;

        foreach (var c in checks)
        {
            string icon = c.pass ? "✅" : (c.critical ? "🔴" : "🟡");
            sb.AppendLine($"{icon} {c.label}");
            if (!c.pass || c.detail.Length > 0)
                sb.AppendLine($"      {c.detail}");
            sb.AppendLine();
            if (c.pass) passCount++;
            else if (c.critical) failCritical++;
            else failWarn++;
        }

        string title = failCritical > 0 ? "❌ Build Readiness — CÓ LỖI NGHIÊM TRỌNG" :
                       failWarn > 0    ? "⚠️  Build Readiness — Có cảnh báo" :
                                         "✅ Build Readiness — Sẵn sàng build!";

        string summary = $"Pass: {passCount}/{checks.Count}   " +
                         $"Critical fails: {failCritical}   Warnings: {failWarn}\n\n";

        Debug.Log($"[BuildReadinessCheck]\n{summary}{sb}");
        EditorUtility.DisplayDialog(title, summary + sb.ToString(), "OK");
    }

    [MenuItem("Tools/Fix AR/Build Readiness Check", true)]
    public static bool Validate() =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid();
}
