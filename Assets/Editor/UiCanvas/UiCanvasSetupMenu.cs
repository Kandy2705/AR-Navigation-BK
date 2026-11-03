#if UNITY_EDITOR
using UnityEditor;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>One-click regeneration of the Canvas UI foundation assets; every step is idempotent.</summary>
    public static class UiCanvasSetupMenu
    {
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
