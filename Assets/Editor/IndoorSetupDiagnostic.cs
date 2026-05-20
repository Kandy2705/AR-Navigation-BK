#if UNITY_EDITOR
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Chẩn đoán indoor setup. Không reference trực tiếp class POI (ambiguous giữa assemblies).
/// Menu: Tools/Indoor/Diagnostic Full Report
/// </summary>
public static class IndoorSetupDiagnostic
{
    [MenuItem("Tools/Indoor/Diagnostic Full Report")]
    public static void Run()
    {
        string report = "=== INDOOR SETUP DIAGNOSTIC ===\n\n";

        // 1. BuildingSceneBindings
        var bindings = Object.FindFirstObjectByType<BuildingSceneBindings>(FindObjectsInactive.Include);
        if (bindings == null)
        {
            report += "X BuildingSceneBindings: NOT FOUND!\n\n";
        }
        else
        {
            report += $"OK BuildingSceneBindings: on '{bindings.gameObject.name}' (instanceId={bindings.gameObject.GetInstanceID()})\n";
            report += $"   Registry: {(bindings.Registry != null ? bindings.Registry.name : "NULL")}\n";
            report += $"   Bindings: {bindings.Bindings.Count}\n";
            if (bindings.Bindings.Count == 0)
            {
                // Thử đọc qua SerializedObject
                var so = new SerializedObject(bindings);
                var prop = so.FindProperty("bindings");
                report += $"   [SerializedObject] bindings.arraySize: {(prop != null ? prop.arraySize.ToString() : "prop not found")}\n";
            }
            foreach (var b in bindings.Bindings)
            {
                if (b == null) { report += "   - NULL\n"; continue; }
                string rootName = b.buildingRoot != null ? b.buildingRoot.name : "NULL";
                string rootActive = b.buildingRoot != null ? $"active={b.buildingRoot.activeSelf}" : "n/a";
                string poiName = b.poiContainer != null ? b.poiContainer.name : "NULL";
                report += $"   - id={b.id}, root='{rootName}' ({rootActive}), poi='{poiName}'\n";
            }
            report += "\n";
        }

        // 2. IndoorMapSwitcher
        var switcher = Object.FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        if (switcher == null)
            report += "X IndoorMapSwitcher: NOT FOUND!\n\n";
        else
            report += $"OK IndoorMapSwitcher: on '{switcher.gameObject.name}', CurrentBuilding={switcher.CurrentBuilding}\n\n";

        // 3. MapLocalizationManager (reflection)
        var allMono = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        MonoBehaviour locMgr = allMono.FirstOrDefault(m => m != null && m.GetType().Name == "MapLocalizationManager" && m.gameObject.scene.IsValid());
        if (locMgr == null)
        {
            report += "X MapLocalizationManager: NOT FOUND!\n\n";
        }
        else
        {
            var t = locMgr.GetType();
            var codeField = t.GetField("mapOrMapsetCode");
            var typeField = t.GetField("localizationType");
            string code = codeField != null ? (string)codeField.GetValue(locMgr) : "?";
            string locType = typeField != null ? typeField.GetValue(locMgr).ToString() : "?";
            report += $"OK MapLocalizationManager: on '{locMgr.gameObject.name}'\n";
            report += $"   mapOrMapsetCode: {code}\n";
            report += $"   localizationType: {locType}\n\n";
        }

        // 4. Map Space
        var mapSpaceT = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(x => x.name == "Map Space" && x.gameObject.scene.IsValid());
        if (mapSpaceT != null)
            report += $"OK Map Space: pos={mapSpaceT.position}, scale={mapSpaceT.lossyScale}\n\n";
        else
            report += "X Map Space: NOT FOUND!\n\n";

        // 5. MapB9 / MapB10
        var allT = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (string mapName in new[] { "MapB9", "MapB10" })
        {
            var mapT = allT.FirstOrDefault(x => x != null && x.name == mapName && x.gameObject.scene.IsValid());
            if (mapT == null) { report += $"X {mapName}: NOT FOUND!\n\n"; continue; }

            report += $"--- {mapName} ---\n";
            report += $"   Pos: {mapT.position}, Scale: {mapT.lossyScale}, Tag: {mapT.gameObject.tag}, Active: {mapT.gameObject.activeSelf}\n";

            var surface = mapT.GetComponent<NavMeshSurface>();
            report += $"   NavMeshSurface: {(surface != null ? "YES" : "NO")}\n";

            foreach (Transform child in mapT)
            {
                if (child == null) continue;
                bool hasMesh = child.GetComponent<MeshRenderer>() != null;
                report += $"   Child: '{child.name}' tag={child.gameObject.tag} mesh={hasMesh} active={child.gameObject.activeSelf}\n";
            }
            report += "\n";
        }

        // 6. RuntimeNavMeshRebaker (đã xóa — không còn dùng)

        // 7. EditorOnly mesh count
        int editorOnly = 0, nonEditorOnly = 0;
        foreach (var x in allT)
        {
            if (x == null || !x.gameObject.scene.IsValid()) continue;
            if (!(x.name.StartsWith("MAP_") || x.name.StartsWith("MSET_") || x.name == "material_0" || x.name == "material_1")) continue;
            if (x.GetComponent<MeshRenderer>() == null) continue;
            if (x.gameObject.CompareTag("EditorOnly")) editorOnly++; else nonEditorOnly++;
        }
        report += $"EditorOnly meshes: {editorOnly}\n";
        report += $"NON-EditorOnly meshes (render on device!): {nonEditorOnly}\n";

        // 8. MapMeshHandler
        MonoBehaviour meshHandler = allMono.FirstOrDefault(m => m != null && m.GetType().Name == "MapMeshHandler" && m.gameObject.scene.IsValid());
        if (meshHandler != null)
        {
            var vizField = meshHandler.GetType().GetField("meshVisualizationOption");
            string viz = vizField != null ? vizField.GetValue(meshHandler).ToString() : "?";
            report += $"MapMeshHandler.meshVisualizationOption: {viz}\n";
        }

        report += "\n=== END ===";
        Debug.Log(report);
    }
}
#endif
