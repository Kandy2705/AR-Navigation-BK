using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TestAR.Tests.PlayMode
{
    /// <summary>
    /// 1 Play-Mode UI-flow test – Table 7.2 (TC_UI_PM01).
    /// </summary>
    [Category("TestAR")]
    public sealed class UIFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator TC_UI_PM01_Welcome_Login_MainSettings_UiFlow()
        {
            // 1. Tải scene Hybrid Navigation và chờ khởi tạo xong
            yield return HybridPlayModeSupport.LoadHybridScene();

            // Xác minh scene đúng tên
            Assert.AreEqual(HybridPlayModeSupport.SceneName,
                SceneManager.GetActiveScene().name,
                "Scene Hybrid Navigation phải được nạp thành công.");

            // 2. Tìm UIDocument (không cần reflection)
            var uiDoc = HybridPlayModeSupport.FindMainUiDocument();
            Assert.IsNotNull(uiDoc, "Scene phải có ít nhất 1 UIDocument.");

            var root = uiDoc.rootVisualElement;
            Assert.IsNotNull(root, "rootVisualElement phải không null.");

            // 3. RootContainer phải tồn tại (từ UI Main.uxml)
            var rootContainer = root.Q<VisualElement>("RootContainer");
            Assert.IsNotNull(rootContainer,
                "RootContainer phải hiển thị sau khi scene nạp (UI Main.uxml).");

            // 4. NavigationManager tự điều hướng đến WelcomePage – LoginButton phải có
            var loginButton = root.Q<Button>("LoginButton");
            Assert.IsNotNull(loginButton,
                "WelcomePage phải chứa LoginButton sau khi scene tự điều hướng.");

            // 5. Điều hướng sang Login
            bool navLogin = HybridPlayModeSupport.Navigate("Login");
            Assert.IsTrue(navLogin,
                $"Navigate('Login') phải thành công " +
                $"(assembly: {HybridPlayModeSupport.GameplayAssemblyName}).");
            yield return new WaitForSecondsRealtime(0.3f);

            var loginSubmitBtn = root.Q<VisualElement>("LoginSubmitButton");
            Assert.IsNotNull(loginSubmitBtn, "Màn Login phải có LoginSubmitButton.");

            // 6. Điều hướng sang MainSettings
            bool navMain = HybridPlayModeSupport.Navigate("MainSettings");
            Assert.IsTrue(navMain,
                $"Navigate('MainSettings') phải thành công " +
                $"(assembly: {HybridPlayModeSupport.GameplayAssemblyName}).");
            yield return new WaitForSecondsRealtime(0.3f);

            var btnAr      = root.Q<VisualElement>("btn-ar");
            var btnProfile = root.Q<VisualElement>("BtnProfile");
            Assert.IsNotNull(btnAr,      "MainSettings phải có btn-ar.");
            Assert.IsNotNull(btnProfile, "MainSettings phải có BtnProfile.");
        }
    }
}
