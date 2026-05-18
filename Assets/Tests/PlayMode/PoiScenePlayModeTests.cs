using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TestAR.Tests.PlayMode
{
    /// <summary>
    /// 7 Play-Mode POI / scene tests – Table 7.4 (TC_POI_PM01 … TC_POI_PM07).
    /// </summary>
    [Category("TestAR")]
    public sealed class PoiScenePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return HybridPlayModeSupport.LoadHybridScene();
        }

        // TC_POI_PM01 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM01_Scene_HasAtLeastOnePoi()
        {
            yield return null;
            // Xác minh scene đã được tải đúng
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            // Tìm POI theo tên kiểu – tránh dùng assembly reflection
            var pois = HybridPlayModeSupport.FindByTypeName("POI");
            Assert.Greater(pois.Length, 0, "Scene phải chứa ít nhất 1 component POI.");
        }

        // TC_POI_PM02 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM02_Scene_HasGpsMarker()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            var markers = HybridPlayModeSupport.FindByTypeName("GPSMarker");
            Assert.Greater(markers.Length, 0, "Scene phải chứa ít nhất 1 GPSMarker.");
        }

        // TC_POI_PM03 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM03_Scene_HasNavigationTarget()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            var targets = HybridPlayModeSupport.FindByTypeName("NavigationTarget");
            Assert.Greater(targets.Length, 0,
                "Scene phải chứa ít nhất 1 NavigationTarget.");
        }

        // TC_POI_PM04 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM04_Scene_HasPoiCollider()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            var colliders = HybridPlayModeSupport.FindByTypeName("POICollider");
            Assert.Greater(colliders.Length, 0,
                "Scene phải chứa ít nhất 1 POICollider.");
        }

        // TC_POI_PM05 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM05_MainScreen_HasUiDocumentAndRootContainer()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            // UIDocument tìm trực tiếp, không cần reflection
            var uiDoc = HybridPlayModeSupport.FindMainUiDocument();
            Assert.IsNotNull(uiDoc, "Scene phải có UIDocument trên MainScreen.");

            var rootContainer = uiDoc.rootVisualElement?.Q<VisualElement>("RootContainer");
            Assert.IsNotNull(rootContainer,
                "UIDocument phải có phần tử RootContainer (từ UI Main.uxml).");
        }

        // TC_POI_PM06 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM06_MainSettings_ShowsArButton()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            var uiDoc = HybridPlayModeSupport.FindMainUiDocument();
            Assert.IsNotNull(uiDoc, "UIDocument phải có mặt trong scene.");

            // Điều hướng sang MainSettings qua reflection
            bool navigated = HybridPlayModeSupport.Navigate("MainSettings");
            Assert.IsTrue(navigated,
                $"Navigate('MainSettings') phải thành công " +
                $"(assembly: {HybridPlayModeSupport.GameplayAssemblyName}).");

            yield return new WaitForSecondsRealtime(0.3f);

            var btnAr = uiDoc.rootVisualElement?.Q<VisualElement>("btn-ar");
            Assert.IsNotNull(btnAr,
                "Sau khi điều hướng sang MainSettings, btn-ar phải hiển thị.");
        }

        // TC_POI_PM07 ─────────────────────────────────────────────────────────
        [UnityTest]
        public IEnumerator TC_POI_PM07_NavigationManager_ArPageObjectAssigned()
        {
            yield return null;
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "SetUp phải tải scene Hybrid Navigation.");

            var arPage = HybridPlayModeSupport.GetArPageObject();
            Assert.IsNotNull(arPage,
                $"NavigationManager.ARPageObject phải được gán trong scene " +
                $"(assembly: {HybridPlayModeSupport.GameplayAssemblyName}).");
        }
    }
}
