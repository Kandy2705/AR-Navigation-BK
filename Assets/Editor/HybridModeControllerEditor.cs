using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HybridModeController))]
public class HybridModeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HybridModeController controller = (HybridModeController)target;

        EditorGUILayout.LabelField("Mode Test Controls", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use mode test controls.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Indoor"))
                {
                    controller.ForceIndoor();
                    EditorUtility.SetDirty(controller);
                }

                if (GUILayout.Button("Force Outdoor"))
                {
                    controller.ForceOutdoor();
                    EditorUtility.SetDirty(controller);
                }
            }

            if (GUILayout.Button("Apply Initial Mode"))
            {
                controller.ApplyInitialMode();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("Deactivate AR"))
            {
                controller.DeactivateARMode();
                EditorUtility.SetDirty(controller);
            }
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
