#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// EditMode tests cho MapOrigin — chuyển đổi GPS (lat/lon) → vị trí Unity (ENU).
    /// Đây là nền tảng định vị: sai ở đây thì mọi POI sai. (TEST_PLAN.md — Tầng 1)
    /// </summary>
    [Category("TestAR")]
    public sealed class MapOriginTests
    {
        private const double OriginLat = 10.0;
        private const double OriginLon = 106.0;

        private MapOrigin NewOrigin()
        {
            var go = new GameObject("maporigin-test");
            var mo = go.AddComponent<MapOrigin>();
            mo.originLat = OriginLat;
            mo.originLon = OriginLon;
            mo.originAlt = 0.0;
            return mo;
        }

        // TC_MAPORIGIN_01 ──────────────────────────────────────────────────────
        [Test]
        public void GetUnityPositionFromGPS_WhenSameAsOrigin_ReturnsNearZero()
        {
            var mo = NewOrigin();
            try
            {
                var p = mo.GetUnityPositionFromGPS(OriginLat, OriginLon);
                Assert.Less(p.magnitude, 0.5f, "Điểm trùng gốc phải ≈ (0,0,0)");
            }
            finally { Object.DestroyImmediate(mo.gameObject); }
        }

        // TC_MAPORIGIN_02 ──────────────────────────────────────────────────────
        [Test]
        public void GetUnityPositionFromGPS_WhenNorthOfOrigin_ReturnsPositiveZ()
        {
            var mo = NewOrigin();
            try
            {
                // +0.001° vĩ độ ≈ 111m Bắc
                var p = mo.GetUnityPositionFromGPS(OriginLat + 0.001, OriginLon);
                Assert.Greater(p.z, 100f, "Bắc → Z dương lớn (~111m)");
                Assert.Less(p.z, 120f);
                Assert.Less(Mathf.Abs(p.x), 2f, "Không lệch kinh độ → X≈0");
            }
            finally { Object.DestroyImmediate(mo.gameObject); }
        }

        // TC_MAPORIGIN_03 ──────────────────────────────────────────────────────
        [Test]
        public void GetUnityPositionFromGPS_WhenEastOfOrigin_ReturnsPositiveX()
        {
            var mo = NewOrigin();
            try
            {
                // +0.001° kinh độ ở vĩ độ 10° ≈ 109.6m Đông
                var p = mo.GetUnityPositionFromGPS(OriginLat, OriginLon + 0.001);
                Assert.Greater(p.x, 100f, "Đông → X dương lớn (~109m)");
                Assert.Less(p.x, 120f);
                Assert.Less(Mathf.Abs(p.z), 2f, "Không lệch vĩ độ → Z≈0");
            }
            finally { Object.DestroyImmediate(mo.gameObject); }
        }

        // TC_MAPORIGIN_04 ──────────────────────────────────────────────────────
        [Test]
        public void GetUnityPositionFromGPS_WhenSouthWest_ReturnsNegativeXZ()
        {
            var mo = NewOrigin();
            try
            {
                var p = mo.GetUnityPositionFromGPS(OriginLat - 0.001, OriginLon - 0.001);
                Assert.Less(p.x, 0f, "Tây → X âm");
                Assert.Less(p.z, 0f, "Nam → Z âm");
            }
            finally { Object.DestroyImmediate(mo.gameObject); }
        }

        // TC_MAPORIGIN_05 ──────────────────────────────────────────────────────
        [Test]
        public void GetUnityPositionFromGPS_AltitudeIgnored_YAlwaysZero()
        {
            var mo = NewOrigin();
            try
            {
                var p = mo.GetUnityPositionFromGPS(OriginLat + 0.001, OriginLon + 0.001, 50.0);
                Assert.AreEqual(0f, p.y, 1e-4f, "Y luôn 0 (bỏ qua altitude GPS)");
            }
            finally { Object.DestroyImmediate(mo.gameObject); }
        }
    }
}
#endif
