using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fits collision to existing artwork without changing any object transform.</summary>
public static class Assignment2HitboxRepair
{
    const string PrefabFolder = "Assets/RateworksPrototype/Prefabs/";
    const string UndoLabel = "Repair Rateworks hitboxes";

    sealed class Changes
    {
        public int wrappers, meshes, boxes, prefabs;
        public readonly List<string> skipped = new List<string>();
    }

    struct Pose
    {
        public string name;
        public Vector3 position, scale;
        public Quaternion rotation;
    }

    [MenuItem("Rateworks/Repair assignment 2 hitboxes (keep all positions)")]
    public static void ApplyFromMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before repairing saved hitboxes.");

        Scene scene = SceneManager.GetActiveScene();
        GameObject room = scene.GetRootGameObjects().FirstOrDefault(g =>
            g.name == "Room" || g.name == "ONE ROOM - Rateworks test bay");
        if (room == null)
            throw new InvalidOperationException("Open the Rateworks prototype scene containing Room first.");

        Dictionary<string, Pose> sceneBefore = Capture(scene.GetRootGameObjects());
        var changes = new Changes();

        // Update the reusable parts first; editor preview instances inherit these repairs.
        foreach (string name in new[] { "Conveyor", "Basic Assembler", "Plate input", "Gear dispatch" })
        {
            string path = PrefabFolder + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("Missing production prefab: " + path);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Dictionary<string, Pose> before = Capture(new[] { prefab });
                int previous = changes.wrappers + changes.meshes + changes.boxes;
                RepairWrappers(prefab, false, changes);
                FitExistingPrimitiveBoxes(prefab, false, changes);
                Verify(before, Capture(new[] { prefab }), path);
                if (previous != changes.wrappers + changes.meshes + changes.boxes)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    changes.prefabs++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        int sceneChanges = changes.wrappers + changes.meshes;
        try
        {
            RepairWrappers(room, true, changes);
            Verify(sceneBefore, Capture(scene.GetRootGameObjects()), scene.name);
            if (sceneChanges != changes.wrappers + changes.meshes)
                EditorSceneManager.MarkSceneDirty(scene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally { Undo.CollapseUndoOperations(group); }

        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        return $"Hitboxes repaired: {changes.wrappers} aggregate boxes disabled, " +
            $"{changes.meshes} mesh colliders added/fixed, {changes.boxes} primitive boxes fitted, " +
            $"{changes.prefabs} production prefabs updated. " +
            $"Verified {sceneBefore.Count} scene transforms unchanged (position, rotation, scale, hierarchy). " +
            (changes.skipped.Count == 0 ? "No objects skipped." : "Skipped: " + string.Join("; ", changes.skipped));
    }

    static void RepairWrappers(GameObject root, bool useUndo, Changes changes)
    {
        // The room builder's model wrappers have a box but no renderer of their own.
        // Primitive walls, floor, columns, trays and supports therefore stay untouched.
        foreach (BoxCollider oldBox in root.GetComponentsInChildren<BoxCollider>(true))
        {
            if (!oldBox.enabled || oldBox.isTrigger || oldBox.GetComponent<Renderer>() != null)
                continue;
            if (oldBox.GetComponentInParent<CharacterController>() != null ||
                oldBox.GetComponentInParent<Rigidbody>() != null ||
                oldBox.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            {
                changes.skipped.Add(oldBox.name + " (dynamic body)");
                continue;
            }

            MeshFilter[] filters = oldBox.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null && f.sharedMesh.vertexCount >= 3 &&
                    f.GetComponent<MeshRenderer>() != null && f.GetComponent<MeshRenderer>().enabled &&
                    f.GetComponent<TMPro.TMP_Text>() == null).ToArray();
            if (filters.Length == 0) continue;

            // Attach directly to the rendered mesh: no bounds-space conversion, child
            // repositioning, convex hull across holes, or extra visual objects needed.
            foreach (MeshFilter filter in filters)
            {
                MeshCollider collider = filter.GetComponents<MeshCollider>()
                    .FirstOrDefault(c => !c.isTrigger);
                if (collider == null)
                {
                    collider = useUndo ? Undo.AddComponent<MeshCollider>(filter.gameObject) :
                        filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.sharedMaterial = oldBox.sharedMaterial;
                    collider.contactOffset = Mathf.Min(oldBox.contactOffset, .01f);
                    changes.meshes++;
                    RecordOverride(collider, useUndo);
                }
                else if (!collider.enabled || collider.convex || collider.sharedMesh != filter.sharedMesh)
                {
                    if (useUndo) Undo.RecordObject(collider, UndoLabel);
                    collider.enabled = true;
                    collider.convex = false;
                    collider.sharedMesh = filter.sharedMesh;
                    changes.meshes++;
                    RecordOverride(collider, useUndo);
                }
            }

            // Keep the original box recoverable in the Inspector and scene Undo history.
            if (useUndo) Undo.RecordObject(oldBox, UndoLabel);
            oldBox.enabled = false;
            RecordOverride(oldBox, useUndo);
            changes.wrappers++;
        }
    }

    static void FitExistingPrimitiveBoxes(GameObject root, bool useUndo, Changes changes)
    {
        // Source/dispatch bodies and the plinth/support are actual cube meshes. Their
        // box belongs in mesh-local coordinates; scaling comes from existing transforms.
        foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
        {
            MeshFilter filter = box.GetComponent<MeshFilter>();
            if (!box.enabled || box.isTrigger || filter == null || filter.sharedMesh == null ||
                filter.sharedMesh.name != "Cube") continue;
            Bounds bounds = filter.sharedMesh.bounds;
            if (box.center == bounds.center && box.size == bounds.size) continue;
            if (useUndo) Undo.RecordObject(box, UndoLabel);
            box.center = bounds.center;
            box.size = bounds.size;
            RecordOverride(box, useUndo);
            changes.boxes++;
        }
    }

    static void RecordOverride(UnityEngine.Object value, bool sceneObject)
    {
        EditorUtility.SetDirty(value);
        if (sceneObject && PrefabUtility.IsPartOfPrefabInstance(value))
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    static Dictionary<string, Pose> Capture(GameObject[] roots)
    {
        var result = new Dictionary<string, Pose>();
        for (int i = 0; i < roots.Length; i++) CaptureChildren(roots[i].transform, i.ToString(), result);
        return result;
    }

    static void CaptureChildren(Transform t, string key, Dictionary<string, Pose> result)
    {
        result.Add(key, new Pose { name = t.name, position = t.localPosition,
            rotation = t.localRotation, scale = t.localScale });
        for (int i = 0; i < t.childCount; i++) CaptureChildren(t.GetChild(i), key + "/" + i, result);
    }

    static void Verify(Dictionary<string, Pose> before, Dictionary<string, Pose> after, string context)
    {
        if (before.Count != after.Count)
            throw new InvalidOperationException("Hitbox repair changed the hierarchy in " + context);
        foreach (KeyValuePair<string, Pose> pair in before)
        {
            Pose a = pair.Value;
            if (!after.TryGetValue(pair.Key, out Pose b) || a.name != b.name ||
                !a.position.Equals(b.position) || !a.rotation.Equals(b.rotation) || !a.scale.Equals(b.scale))
                throw new InvalidOperationException("Hitbox repair changed a transform: " + context + "/" + a.name);
        }
    }
}
