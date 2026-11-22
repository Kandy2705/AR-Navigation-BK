#if UNITY_EDITOR
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>Checks shared by the screen tests: a prefab exists with the right page id and none of its scripts has an empty reference.</summary>
    internal static class PrefabAsserts
    {
        public static UiPage LoadPage(string screenName)
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(page, screenName + ".prefab — run Tools/UI Canvas/Run Phase 4-7 Setup");
            return page;
        }

        // Fields a screen may legitimately leave empty: UiTextField's optional error line, background and password toggle.
        private static readonly System.Collections.Generic.HashSet<string> OptionalTextFieldReferences = new System.Collections.Generic.HashSet<string>
        {
            "errorLabel", "background", "visibilityToggle", "visibilityIcon", "iconHidden", "iconShown",
        };

        // Scene references a prefab cannot hold: NavigationHudUI finds the navigation scene's HUD logic at start.
        private static readonly System.Collections.Generic.HashSet<string> SceneReferences = new System.Collections.Generic.HashSet<string>
        {
            "NavigationHudUI.hud",
        };

        /// <summary>Every serialized object reference (and array element) on every script under <paramref name="root"/> is assigned.</summary>
        public static void AssertAllReferencesWired(GameObject root, string label)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Namespace == null || !behaviour.GetType().Namespace.StartsWith("Game."))
                {
                    continue;
                }

                SerializedProperty property = new SerializedObject(behaviour).GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    enter = false;
                    if (behaviour is UiTextField && OptionalTextFieldReferences.Contains(property.name))
                    {
                        continue;
                    }

                    if (SceneReferences.Contains(behaviour.GetType().Name + "." + property.name))
                    {
                        continue;
                    }

                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                    {
                        Assert.NotNull(property.objectReferenceValue, $"{label}: {behaviour.GetType().Name}.{property.name} is not wired");
                    }
                    else if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                    {
                        for (int i = 0; i < property.arraySize; i++)
                        {
                            SerializedProperty element = property.GetArrayElementAtIndex(i);
                            if (element.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                Assert.NotNull(element.objectReferenceValue, $"{label}: {behaviour.GetType().Name}.{property.name}[{i}] is not wired");
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif
