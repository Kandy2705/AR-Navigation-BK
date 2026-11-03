#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>One-click regeneration of the Canvas UI foundation assets; every step is idempotent.</summary>
    public static class UiCanvasSetupMenu
    {
        [MenuItem("Tools/UI Canvas/Run Phase 1 Setup")]
        public static void RunPhase1()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before running Phase 1 setup.");
                return;
            }

            UiCanvasOnboardingBuilder.Build();
            UiCanvasPreviewRenderer.RenderAll();
        }

        [MenuItem("Tools/UI Canvas/Run Phase 0 Setup")]
        public static void RunPhase0()
        {
            UiCanvasSpriteGenerator.Generate();
            UiCanvasFontGenerator.Generate();
            UiCanvasPrefabBuilder.Build();
            AssetDatabase.Refresh();
        }
    }
}
#endif
