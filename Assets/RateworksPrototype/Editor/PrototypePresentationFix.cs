using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class PrototypePresentationFix
{
    public static string Apply()
    {
        const string folder = "Assets/RateworksPrototype/Prefabs/";
        var belt = PrefabUtility.LoadPrefabContents(folder + "Conveyor.prefab");
        // Rotate the mesh and its collider, not the transport arrow or simulation root.
        belt.transform.Find("conveyorbelt").localRotation = Quaternion.Euler(0, 90, 0);
        foreach (var name in new[] { "Direction shaft", "Arrow tip A", "Arrow tip B" })
        {
            var t = belt.transform.Find(name);
            t.localPosition = new Vector3(t.localPosition.x, .80f, t.localPosition.z);
        }
        PrefabUtility.SaveAsPrefabAsset(belt, folder + "Conveyor.prefab");
        PrefabUtility.UnloadPrefabContents(belt);
        foreach (var name in new[] { "Plate input", "Gear dispatch" })
        {
            string path = folder + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var label = root.transform.Find("Port label").GetComponent<TextMeshPro>();
            label.text = name == "Plate input" ? "IN  /  IRON PLATES" : "OUT  /  GEARS";
            label.fontSize = 1.35f;
            label.rectTransform.sizeDelta = new Vector2(1.5f, .40f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.transform.localPosition = new Vector3(0, 1.22f, -.83f);
            label.alignment = TextAlignmentOptions.Center;
            var existing = root.transform.Find("Port sign backing");
            if (existing == null)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Port sign backing";
                board.transform.SetParent(root.transform, false);
                board.transform.localPosition = new Vector3(0, 1.22f, -.78f);
                board.transform.localScale = new Vector3(1.65f, .48f, .07f);
                board.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/RateworksPrototype/Materials/Machine - dark steel.mat");
                Object.DestroyImmediate(board.GetComponent<Collider>());
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Saved clockwise belt mesh rotation and compact backed port labels.";
    }
}
