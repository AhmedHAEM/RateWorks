using System;
using UnityEditor;
using UnityEngine;

/// <summary>Updates production artwork without changing placement or collision geometry.</summary>
public static class ConveyorVisualPolish
{
    const string Root = "Assets/RateworksPrototype/";

    [MenuItem("Rateworks/Move conveyor arrows to sides and match gear colour")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before updating the production prefabs.");
        string beltPath = Root + "Prefabs/Conveyor.prefab";
        var belt = PrefabUtility.LoadPrefabContents(beltPath);
        try
        {
            var gold = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Safety - gold.mat");
            // Arrows lie on the outer vertical rails; the carrying surface stays clear.
            ArrowPart(belt.transform, "Direction shaft", new Vector3(-.06f, .56f, -.845f), new Vector3(.55f, .045f, .018f), 0, gold);
            ArrowPart(belt.transform, "Arrow tip A", new Vector3(.22f, .63f, -.845f), new Vector3(.24f, .045f, .018f), -45, gold);
            ArrowPart(belt.transform, "Arrow tip B", new Vector3(.22f, .49f, -.845f), new Vector3(.24f, .045f, .018f), 45, gold);
            ArrowPart(belt.transform, "Far side direction shaft", new Vector3(-.06f, .56f, .845f), new Vector3(.55f, .045f, .018f), 0, gold);
            ArrowPart(belt.transform, "Far side arrow tip A", new Vector3(.22f, .63f, .845f), new Vector3(.24f, .045f, .018f), -45, gold);
            ArrowPart(belt.transform, "Far side arrow tip B", new Vector3(.22f, .49f, .845f), new Vector3(.24f, .045f, .018f), 45, gold);
            PrefabUtility.SaveAsPrefabAsset(belt, beltPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(belt); }

        var steel = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Machine - dark steel.mat");
        if (steel == null) throw new InvalidOperationException("Missing conveyor steel material.");
        string materialPath = Root + "Materials/Product - dark steel gear.mat";
        var gearMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (gearMaterial == null)
        {
            gearMaterial = new Material(steel);
            AssetDatabase.CreateAsset(gearMaterial, materialPath);
        }
        gearMaterial.color = steel.color;
        // Same colour, slightly polished so the gear edges remain readable on the belt.
        gearMaterial.SetFloat("_Metallic", .45f);
        gearMaterial.SetFloat("_Smoothness", .48f);
        EditorUtility.SetDirty(gearMaterial);
        string gearPath = Root + "Prefabs/Gear product.prefab";
        var gear = PrefabUtility.LoadPrefabContents(gearPath);
        try
        {
            foreach (var renderer in gear.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = gearMaterial;
            PrefabUtility.SaveAsPrefabAsset(gear, gearPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(gear); }
        AssetDatabase.SaveAssetIfDirty(gearMaterial);
        Debug.Log("Gear colour matches conveyor steel; both belt-side arrows point +X. Placement transforms and colliders unchanged.");
    }

    static void ArrowPart(Transform parent, string name, Vector3 position, Vector3 scale, float angle, Material material)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            child = go.transform; child.SetParent(parent, false);
        }
        child.localPosition = position; child.localScale = scale;
        child.localRotation = Quaternion.Euler(0, 0, angle);
        child.GetComponent<Renderer>().sharedMaterial = material;
    }
}
