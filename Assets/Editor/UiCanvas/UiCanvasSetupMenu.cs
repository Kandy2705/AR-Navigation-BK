#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>One-click regeneration of the Canvas UI assets; every step is idempotent.</summary>
    public static class UiCanvasSetupMenu
    {
        [MenuItem("Tools/UI Canvas/Run Phase 4-7 Setup")]
        public static void RunPhase4To7()
        {
            if (IsPlaying("Phase 4-7 setup"))
            {
                return;
            }

            // Icons come first: every new screen loads them. The earlier screens are not rebuilt (that renumbers every
            // fileID in their prefabs); run Phase 2/3 Setup if one of them has to change.
            UiCanvasIconGenerator.Generate();
            UiCanvasHomeBuilder.Build();
            UiCanvasSettingsBuilder.Build();
            UiCanvasShellSceneBuilder.Build();
            UiCanvasPreviewRenderer.RenderAll();
        }

        [MenuItem("Tools/UI Canvas/Run Phase 3 Setup")]
        public static void RunPhase3()
        {
            if (IsPlaying("Phase 3 setup"))
            {
                return;
            }

            // Sprites (chevron, avatar tile) come first: the screens load them. The auth prefabs are rebuilt too,
            // because the shared builder helpers changed.
            UiCanvasSpriteGenerator.Generate();
            UiCanvasFontGenerator.Generate();
            UiCanvasAuthBuilder.Build();
            UiCanvasRegisterBuilder.Build();
            UiCanvasShellSceneBuilder.Build();
            UiCanvasPreviewRenderer.RenderAll();
        }

        [MenuItem("Tools/UI Canvas/Run Phase 2 Setup")]
        public static void RunPhase2()
        {
            if (IsPlaying("Phase 2 setup"))
            {
                return;
            }

            // New shared sprites (outlines, back arrow) and the Light font weight come first: the screens load them.
            UiCanvasSpriteGenerator.Generate();
            UiCanvasFontGenerator.Generate();
            UiCanvasAuthBuilder.Build();
            UiCanvasShellSceneBuilder.Build();
            UiCanvasPreviewRenderer.RenderAll();
        }

        [MenuItem("Tools/UI Canvas/Run Phase 1 Setup")]
        public static void RunPhase1()
        {
            if (IsPlaying("Phase 1 setup"))
            {
                return;
            }

            UiCanvasOnboardingBuilder.Build();
            UiCanvasShellSceneBuilder.Build();
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

        private static bool IsPlaying(string what)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            Debug.LogError("[UiCanvas] Stop Play mode before running " + what + ".");
            return true;
        }
    }
}
#endif
