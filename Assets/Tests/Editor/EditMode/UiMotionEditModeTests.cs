#if UNITY_EDITOR
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// The motion design: page transition poses at chosen moments, easing curves, and the shared sheets and overlays
    /// (wired to the part that moves, instant outside Play mode).
    /// </summary>
    [Category("TestAR")]
    public sealed class UiMotionEditModeTests
    {
        private const float Width = 375f;

        [Test]
        public void Push_SlidesTheNewPageInOpaqueAndParksTheOldOneDimmed()
        {
            PagePose start = UiMotion.Page(PageTransition.Push, true, 0f, Width);
            PagePose end = UiMotion.Page(PageTransition.Push, true, 1f, Width);
            Assert.AreEqual(Width, start.X, 0.01f);
            Assert.AreEqual(0f, end.X, 0.01f);
            Assert.AreEqual(1f, start.Alpha, "the arriving page is never see-through");

            PagePose under = UiMotion.Page(PageTransition.Push, false, 1f, Width);
            Assert.AreEqual(-Width * UiMotion.UnderlayShift, under.X, 0.01f);
            Assert.AreEqual(UiMotion.UnderlayDim, under.Dim, 0.001f);
            Assert.AreEqual(1f, under.Alpha);
        }

        [Test]
        public void Pop_MirrorsPush()
        {
            for (float t = 0f; t <= 1f; t += 0.25f)
            {
                PagePose pushIn = UiMotion.Page(PageTransition.Push, true, 1f - t, Width);
                PagePose popOut = UiMotion.Page(PageTransition.Pop, false, t, Width);
                Assert.AreEqual(0f, popOut.Dim);
                Assert.AreEqual(1f, popOut.Alpha);
                Assert.That(popOut.X, Is.InRange(0f, Width));
                Assert.That(pushIn.X, Is.InRange(0f, Width));
            }

            Assert.AreEqual(-Width * UiMotion.UnderlayShift, UiMotion.Page(PageTransition.Pop, true, 0f, Width).X, 0.01f);
            Assert.AreEqual(UiMotion.UnderlayDim, UiMotion.Page(PageTransition.Pop, true, 0f, Width).Dim, 0.001f);
            Assert.AreEqual(0f, UiMotion.Page(PageTransition.Pop, true, 1f, Width).X, 0.01f);
            Assert.AreEqual(Width, UiMotion.Page(PageTransition.Pop, false, 1f, Width).X, 0.01f);
        }

        [Test]
        public void Fade_KeepsTheOldPageOpaqueUntilTheNewOneIsMostlyIn()
        {
            Assert.AreEqual(0f, UiMotion.Page(PageTransition.Fade, true, 0f, Width).Alpha, 0.001f);
            Assert.AreEqual(1f, UiMotion.Page(PageTransition.Fade, true, 0.3f, Width).Scale, "shared chrome must not ghost");
            Assert.AreEqual(1f, UiMotion.Page(PageTransition.Fade, true, 1f, Width).Alpha, 0.001f);

            Assert.AreEqual(1f, UiMotion.Page(PageTransition.Fade, false, UiMotion.FadeOutDelay, Width).Alpha, 0.001f);
            float arriving = UiMotion.Page(PageTransition.Fade, true, UiMotion.FadeOutDelay, Width).Alpha;
            Assert.Greater(arriving, 0.95f, "by the time the old page starts fading the new one covers almost all of it");
            Assert.AreEqual(0f, UiMotion.Page(PageTransition.Fade, false, 1f, Width).Alpha, 0.001f);
            Assert.AreEqual(0f, UiMotion.Page(PageTransition.Fade, false, 0.5f, Width).X);
        }

        [Test]
        public void Easings_StartAtZeroEndAtOneAndOutBackOvershoots()
        {
            foreach (System.Func<float, float> ease in new System.Func<float, float>[]
                     { UiMotion.OutCubic, UiMotion.OutQuart, UiMotion.InCubic, UiMotion.OutBack })
            {
                Assert.AreEqual(0f, ease(0f), 0.0001f);
                Assert.AreEqual(1f, ease(1f), 0.0001f);
            }

            Assert.Greater(UiMotion.OutBack(0.7f), 1f);
            Assert.Greater(UiMotion.OutQuart(0.3f), UiMotion.OutCubic(0.3f), "quart front-loads more of the slide");
        }

        [TestCase(UiCanvasListKit.ConfirmSheetPrefab, "panel")]
        [TestCase(UiCanvasRegisterBuilder.PickerSheetPrefab, "panel")]
        [TestCase(UiCanvasAuthBuilder.StatusOverlayPrefab, "card")]
        public void SharedOverlays_KnowWhichPartMoves(string prefabPath, string field)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            foreach (MonoBehaviour behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                SerializedProperty property = new SerializedObject(behaviour).FindProperty(field);
                if (property == null)
                {
                    continue;
                }

                var moving = property.objectReferenceValue as RectTransform;
                Assert.IsNotNull(moving, prefabPath + " → " + field);
                Assert.AreNotEqual(prefab.transform, moving, "the scrim stays put; only the sheet or card moves");
                return;
            }

            Assert.Fail(prefabPath + " has no " + field + " field");
        }

        [Test]
        public void Modal_OutsidePlayMode_ShowsAndHidesAtOnceWithThePanelAtRest()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasListKit.ConfirmSheetPrefab);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                var modal = instance.GetComponent<UiModal>();
                var panel = (RectTransform)new SerializedObject(modal).FindProperty("panel").objectReferenceValue;
                Vector2 rest = panel.anchoredPosition;

                modal.Show("Tiêu đề", "Nội dung", "Xác nhận", "Hủy");
                Assert.IsTrue(modal.IsVisible);
                Assert.AreEqual(rest, panel.anchoredPosition);
                Assert.AreEqual(1f, instance.GetComponent<CanvasGroup>().alpha);

                modal.Hide();
                Assert.IsFalse(modal.IsVisible);
                Assert.IsFalse(instance.activeSelf);
                Assert.AreEqual(rest, panel.anchoredPosition, "a later Show starts from the resting place");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PressFeedback_SkipsScrimButtonsAndIsAddedOnce()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var pill = new GameObject("Pill", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                pill.transform.SetParent(root.transform, false);
                var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                scrim.transform.SetParent(root.transform, false);
                scrim.GetComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;

                UiPressFeedback.AttachTo(root.transform);
                UiPressFeedback.AttachTo(root.transform);

                Assert.AreEqual(1, pill.GetComponents<UiPressFeedback>().Length);
                Assert.IsNull(scrim.GetComponent<UiPressFeedback>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
