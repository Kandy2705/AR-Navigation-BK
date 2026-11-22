#if UNITY_EDITOR
namespace Game.Foundation.UI.EditorTools
{
    /// <summary>Command-line entry point: <c>Unity -batchmode -executeMethod Game.Foundation.UI.EditorTools.UiCanvasBatch.BuildAll</c>. Builds the phase 4-7 prefabs and the shell scene; skips the preview render, which needs a GPU.</summary>
    public static class UiCanvasBatch
    {
        public static void BuildAll()
        {
            // Only what the home and settings screens add. The earlier screens are not rebuilt: a rebuild renumbers every
            // fileID in their prefabs and rewrites the font atlases, which is noise in version control.
            UiCanvasIconGenerator.Generate();
            UiCanvasHomeBuilder.Build();
            UiCanvasSettingsBuilder.Build();
            UiCanvasShellSceneBuilder.Build();
        }

        /// <summary>
        /// <c>-executeMethod Game.Foundation.UI.EditorTools.UiCanvasBatch.BuildNavigationHud</c>: the B9 guidance overlay,
        /// its installation in the navigation scene and the shell's scene loader. Previews render when a GPU is available
        /// (batch mode without -nographics).
        /// </summary>
        public static void BuildNavigationHud()
        {
            UiCanvasIconGenerator.Generate();
            UnityEditor.AssetDatabase.Refresh();
            UiCanvasNavigationHudBuilder.Build();
            UiCanvasNavigationSceneInstaller.Install();
            UiCanvasShellSceneBuilder.Build();
            if (UnityEngine.SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                UiCanvasPreviewRenderer.RenderNavigationHud();
            }
        }
    }
}
#endif
