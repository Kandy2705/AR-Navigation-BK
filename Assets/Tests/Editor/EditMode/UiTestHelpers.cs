#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace TestAR.Tests.Editor
{
    internal static class UiTestHelpers
    {
        public const string DocumentsRoot = "Assets/UI/Documents";

        public static VisualTreeAsset LoadTree(string assetPath)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.NotNull(tree, $"Không load được UXML: {assetPath}");
            return tree;
        }

        public static VisualElement InstantiateRoot(string assetPath)
        {
            var tree = LoadTree(assetPath);
            var root = tree.Instantiate();
            Assert.NotNull(root, $"Instantiate UXML trả null: {assetPath}");
            return root;
        }
    }
}
#endif
