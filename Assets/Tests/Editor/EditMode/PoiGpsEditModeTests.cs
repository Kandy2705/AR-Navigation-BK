#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using TestAR.GpsAR;
using UnityEditor;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// 8 Edit-Mode POI / GPS tests – Table 7.3 (TC_POI_ED01 … TC_POI_ED08).
    /// </summary>
    [Category("TestAR")]
    public sealed class PoiGpsEditModeTests
    {
        // Tìm type trong assembly của GPSMarker (Assembly-CSharp), tránh nhầm MultiSet SDK
        private static Type ResolveType(string typeName)
        {
            var asm = typeof(GPSMarker).Assembly;
            var t = asm.GetType(typeName)
                    ?? asm.GetExportedTypes().FirstOrDefault(x => x.Name == typeName);
            Assert.IsNotNull(t, $"Không tìm thấy '{typeName}' trong {asm.GetName().Name}.");
            return t;
        }

        // TC_POI_ED01 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED01_PoiType_HasThirteenKinds()
        {
            var poiType = ResolveType("POIType");
            Assert.AreEqual(13, Enum.GetNames(poiType).Length,
                "POIType phải có đúng 13 loại (khớp với bảng mã nguồn).");
        }

        // TC_POI_ED02 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED02_GpsMarker_LatLonAlt_ToEcef_IsFinite()
        {
            var go = new GameObject("gps-ed02");
            try
            {
                var gps = go.AddComponent<GPSMarker>();
                var ecef = gps.LatLonAltToECEF(10.7741875, 106.6606904, 0.0);
                Assert.IsTrue(double.IsFinite(ecef.x), "ECEF.x phải hữu hạn");
                Assert.IsTrue(double.IsFinite(ecef.y), "ECEF.y phải hữu hạn");
                Assert.IsTrue(double.IsFinite(ecef.z), "ECEF.z phải hữu hạn");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // TC_POI_ED03 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED03_GpsMarker_Ecef_ToEnu_EastNonZero()
        {
            var go = new GameObject("gps-ed03");
            try
            {
                var gps = go.AddComponent<GPSMarker>();
                double refLat = 10.7736444, refLon = 106.6593743;
                var refECEF = gps.LatLonAltToECEF(refLat, refLon, 0);
                // Vị trí lệch kinh độ nhỏ về phía đông
                var ptECEF  = gps.LatLonAltToECEF(refLat, refLon + 0.001, 0);
                var enu = gps.ECEFToENU(ptECEF, refECEF, refLat, refLon);
                Assert.Greater(Math.Abs(enu.e), 1e-3,
                    "Thành phần Đông (ENU.e) phải khác 0 khi lệch kinh độ");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // TC_POI_ED04 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED04_AnchoredPoi_DefaultState_NotAnchored()
        {
            var go = new GameObject("anchored-ed04");
            try
            {
                var apoi = go.AddComponent<AnchoredPOI>();
                // AnchoredPOI.anchored bắt đầu = false → IsAnchored = false
                Assert.IsFalse(apoi.IsAnchored,
                    "AnchoredPOI mới tạo chưa được neo (IsAnchored phải là false).");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // TC_POI_ED05 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED05_NavigationTarget_Coordinates_Writable()
        {
            var go = new GameObject("navtarget-ed05");
            try
            {
                var nt = go.AddComponent<NavigationTarget>();
                nt.targetLat = 10.7741875;
                nt.targetLon = 106.6606904;
                nt.targetAlt = 5.0;

                Assert.AreEqual(10.7741875,  nt.targetLat, 1e-6, "targetLat");
                Assert.AreEqual(106.6606904, nt.targetLon, 1e-6, "targetLon");
                Assert.AreEqual(5.0,         nt.targetAlt, 1e-6, "targetAlt");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // TC_POI_ED06 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED06_MultiSet_NavigationSampleScene_Exists()
        {
            const string path = "Assets/Samples/MultiSet-SDK/1.9.2/Sample Scenes/Navigation/Navigation.unity";
            var scene = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
            Assert.IsNotNull(scene,
                $"Scene Navigation của MultiSet SDK không tìm thấy tại: {path}");
        }

        // TC_POI_ED07 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED07_HybridNavigationScene_Exists()
        {
            const string path = "Assets/Scenes/Hybrid Navigation.unity";
            var scene = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
            Assert.IsNotNull(scene,
                $"Scene Hybrid Navigation không tìm thấy tại: {path}");
        }

        // TC_POI_ED08 ─────────────────────────────────────────────────────────
        [Test]
        public void TC_POI_ED08_PoiSign_CanBeAddedToGameObject()
        {
            // Dùng reflection để tránh CS0433: POISign trùng tên với MultiSet-SDK.Core
            var poiSignType = ResolveType("POISign");
            var go = new GameObject("poisign-ed08");
            try
            {
                var comp = go.AddComponent(poiSignType);
                Assert.IsNotNull(comp, "AddComponent(POISign) phải trả về instance hợp lệ.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
