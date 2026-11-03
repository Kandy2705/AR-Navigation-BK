#if UNITY_EDITOR
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phase 0 of the UI Toolkit → Canvas migration: design tokens, base sprites, TMP fonts,
    /// the AppCanvas prefab and the UiPage base behaviour. The generated assets are committed;
    /// when one is missing run Tools ▸ UI Canvas ▸ Run Phase 0 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasFoundationEditModeTests
    {
        private const string SetupHint = " — run Tools/UI Canvas/Run Phase 0 Setup";

        [Test]
        public void UiTheme_Primary_MatchesLegacyUssButtonColor()
        {
            Assert.AreEqual(UiTheme.Hex("#403285"), UiTheme.Primary);
        }

        [Test]
        public void UiTheme_ReferenceResolution_IsFigmaFrame()
        {
            Assert.AreEqual(new Vector2(375f, 812f), UiTheme.ReferenceResolution);
        }

        [Test]
        public void UiTheme_Hex_WhenInvalid_ReturnsMagentaSoItIsNoticed()
        {
            Assert.AreEqual(Color.magenta, UiTheme.Hex("not-a-color"));
        }

        [Test]
        public void RoundedSprite_WhenImported_HasUniformNineSliceBorder()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiCanvasPaths.RoundedSprite);
            Assert.NotNull(sprite, "ui_rounded_9slice.png" + SetupHint);

            float corner = UiCanvasPaths.RoundedCornerPixels;
            Assert.AreEqual(new Vector4(corner, corner, corner, corner), sprite.border);
        }

        [Test]
        public void CircleSprite_WhenImported_Exists()
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<Sprite>(UiCanvasPaths.CircleSprite), "ui_circle.png" + SetupHint);
        }

        [TestCase("Regular")]
        [TestCase("Medium")]
        [TestCase("SemiBold")]
        [TestCase("Bold")]
        public void PoppinsFont_WhenGenerated_IsDynamicWithVietnameseFallback(string weight)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiCanvasPaths.PoppinsFontAsset(weight));
            Assert.NotNull(font, "Poppins-" + weight + " SDF" + SetupHint);
            Assert.AreEqual(TMPro.AtlasPopulationMode.Dynamic, font.atlasPopulationMode);
            Assert.IsNotEmpty(font.fallbackFontAssetTable, "Poppins lacks most Vietnamese vowels; a fallback is required");
        }

        [Test]
        public void AppCanvasPrefab_WhenBuilt_ScalesFromFigmaFrameMatchingWidth()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.AppCanvasPrefab);
            Assert.NotNull(prefab, "AppCanvas.prefab" + SetupHint);

            var canvas = prefab.GetComponent<Canvas>();
            var scaler = prefab.GetComponent<CanvasScaler>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(UiTheme.ReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(0f, scaler.matchWidthOrHeight);
        }

        [Test]
        public void AppCanvasPrefab_WhenBuilt_HasPagesModalsAndEventSystemGuard()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.AppCanvasPrefab);
            Assert.NotNull(prefab, "AppCanvas.prefab" + SetupHint);

            Assert.NotNull(prefab.transform.Find(UiCanvasPrefabBuilder.PagesName), "Pages container");
            Assert.NotNull(prefab.transform.Find(UiCanvasPrefabBuilder.ModalsName), "Modals container");
            Assert.NotNull(prefab.GetComponent<GraphicRaycaster>(), "GraphicRaycaster");
            Assert.NotNull(prefab.GetComponent<UiEventSystemEnsurer>(), "UiEventSystemEnsurer");
        }

        [Test]
        public void UiPage_ShowThenHide_TogglesActiveWithoutAnimationInEditMode()
        {
            var go = new GameObject("TestPage", typeof(RectTransform), typeof(CanvasGroup), typeof(UiPage));
            go.SetActive(false);
            try
            {
                var page = go.GetComponent<UiPage>();
                int shown = 0;
                int hidden = 0;
                page.Shown += _ => shown++;
                page.Hidden += _ => hidden++;

                page.Show();
                Assert.IsTrue(go.activeSelf, "active after Show");
                Assert.IsTrue(page.IsVisible);
                Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha);

                page.Hide();
                Assert.IsFalse(go.activeSelf, "inactive after Hide");
                Assert.IsFalse(page.IsVisible);
                Assert.AreEqual(1, shown);
                Assert.AreEqual(1, hidden);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UiPage_HideImmediate_WhenAlreadyInactive_DoesNotThrow()
        {
            var go = new GameObject("TestPage", typeof(RectTransform), typeof(CanvasGroup), typeof(UiPage));
            go.SetActive(false);
            try
            {
                Assert.DoesNotThrow(() => go.GetComponent<UiPage>().HideImmediate());
                Assert.IsFalse(go.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
