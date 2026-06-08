#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// EditMode tests cho NavMeshPathRibbon.SmoothCornersChaikin — bo tròn góc gấp của path.
    /// Đảm bảo: giữ điểm đầu/cuối, không phình ra ngoài (an toàn obstacle). (TEST_PLAN.md — Tầng 1)
    /// </summary>
    [Category("TestAR")]
    public sealed class NavMeshPathRibbonTests
    {
        // Path hình chữ L: góc gấp 90° tại (10,0,0)
        private static Vector3[] LShape() => new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, 10f),
        };

        // TC_CHAIKIN_01 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_WhenIterationsZero_ReturnsInputUnchanged()
        {
            var pts = LShape();
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 0);
            Assert.AreSame(pts, r, "iterations=0 → trả về input nguyên vẹn");
        }

        // TC_CHAIKIN_02 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_WhenTwoPoints_ReturnsInputUnchanged()
        {
            var pts = new[] { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 5f) };
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 2);
            Assert.AreSame(pts, r, "n<3 (không có góc) → không bo");
        }

        // TC_CHAIKIN_03 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_WhenNull_ReturnsNull()
        {
            var r = NavMeshPathRibbon.SmoothCornersChaikin(null, 2);
            Assert.IsNull(r, "null input → null (không crash)");
        }

        // TC_CHAIKIN_04 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_PreservesEndpoints()
        {
            var pts = LShape();
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 2);
            Assert.AreEqual(pts[0], r[0], "Giữ nguyên điểm đầu (vị trí user)");
            Assert.AreEqual(pts[pts.Length - 1], r[r.Length - 1], "Giữ nguyên điểm cuối (target)");
        }

        // TC_CHAIKIN_05 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_IncreasesPointCount()
        {
            var pts = LShape();
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 1);
            Assert.Greater(r.Length, pts.Length, "Chaikin chèn điểm để bo → count tăng");
        }

        // TC_CHAIKIN_06 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_StaysWithinBoundingBox()
        {
            var pts = LShape(); // box x[0,10], z[0,10]
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 3);
            foreach (var p in r)
            {
                Assert.GreaterOrEqual(p.x, -0.001f, "Không phình ra ngoài box (x-)");
                Assert.LessOrEqual(p.x, 10.001f, "Không phình ra ngoài box (x+)");
                Assert.GreaterOrEqual(p.z, -0.001f, "Không phình ra ngoài box (z-)");
                Assert.LessOrEqual(p.z, 10.001f, "Không phình ra ngoài box (z+)");
            }
        }

        // TC_CHAIKIN_07 ────────────────────────────────────────────────────────
        [Test]
        public void SmoothChaikin_CutsSharpCorner()
        {
            var pts = LShape(); // góc gấp tại (10,0,0)
            var r = NavMeshPathRibbon.SmoothCornersChaikin(pts, 2);

            bool hasExactCorner = false;
            foreach (var p in r)
                if (Vector3.Distance(p, new Vector3(10f, 0f, 0f)) < 0.05f) hasExactCorner = true;

            Assert.IsFalse(hasExactCorner, "Góc gấp (10,0,0) phải bị cắt/bo, không còn điểm trùng đúng góc");
        }
    }
}
#endif
