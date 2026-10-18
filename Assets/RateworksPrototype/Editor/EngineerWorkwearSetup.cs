using System;
using System.Collections.Generic;
using System.Linq;
using RateworksPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Appearance only: keeps the imported rig, animation and sole player collider intact.</summary>
public static class EngineerWorkwearSetup
{
    const string Folder = "Assets/RateworksPrototype/Materials";
    const string HatName = "Worker hard hat";
    const string UndoName = "Dress engineer in factory workwear";

    [MenuItem("Rateworks/Apply engineer workwear")]
    public static void ApplyMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before changing the engineer outfit.");
        var game = UnityEngine.Object.FindFirstObjectByType<FactoryGame>();
        if (game == null || game.presentation == null || game.presentation.engineerAvatar == null)
            throw new InvalidOperationException("Set up the animated engineer first.");
        var visual = game.presentation.engineerAvatar.transform;
        var animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman)
            throw new InvalidOperationException("The engineer must have a humanoid Animator.");
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var shirt = renderers.Single(r => r.name == "Outerwear");
        var trousers = renderers.Single(r => r.name == "Pants");
        var shoes = renderers.Single(r => r.name == "Shoes");
        var hair = renderers.Single(r => r.name == "Hairstyle");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var orange = Material("Engineer - high visibility orange", new Color(.96f, .30f, .045f), .12f);
        var navy = Material("Engineer - navy work trousers", new Color(.065f, .11f, .16f), .12f);
        var leather = Material("Engineer - brown work shoes", new Color(.30f, .18f, .095f), .2f, shoes.sharedMaterial);
        var yellow = Material("Engineer - safety yellow", new Color(1f, .69f, .055f), .32f);
        var trim = Material("Engineer - helmet trim", new Color(.055f, .065f, .07f), .15f);
        var highlight = Material("Engineer - helmet ridge", new Color(1f, .79f, .15f), .32f);
        Assign(shirt, orange); Assign(trousers, navy); Assign(shoes, leather);
        Undo.RecordObject(hair, UndoName);
        hair.enabled = false; // Avoid hair poking through the fitted helmet.

        // Accessory follows the actual animated head bone, never the player/camera root.
        // Idempotent: repeated application does not add another hat or alter its fit.
        if (head.Find(HatName) == null)
        {
            var hat = new GameObject(HatName);
            Undo.RegisterCreatedObjectUndo(hat, UndoName);
            hat.layer = visual.gameObject.layer;
            hat.transform.SetParent(head, false);
            hat.transform.position = visual.TransformPoint(new Vector3(0, 1.755f, -.012f));
            hat.transform.rotation = visual.rotation;
            hat.transform.localScale = Vector3.one;
            var mesh = HelmetMesh();
            hat.AddComponent<MeshFilter>().sharedMesh = mesh;
            hat.AddComponent<MeshRenderer>().sharedMaterials = new[] { yellow, trim, highlight };
        }
        EditorUtility.SetDirty(hair);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        return "Engineer dressed in an orange work shirt, navy cargo trousers, brown work shoes and a bone-attached yellow hard hat. Movement, animations, colliders and room transforms unchanged.";
    }

    static void Assign(SkinnedMeshRenderer renderer, Material material)
    {
        Undo.RecordObject(renderer, UndoName);
        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMesh.subMeshCount).ToArray();
        EditorUtility.SetDirty(renderer);
    }

    static Material Material(string name, Color color, float smoothness, Material source = null)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            // Shoes retain the pack's sole/lace detail; clothing uses solid workwear colours.
            material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(material, UndoName);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    static Mesh HelmetMesh()
    {
        const string path = "Assets/RateworksPrototype/Prefabs/Engineer hard hat mesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var vertices = new List<Vector3>();
        var shell = new List<int>(); var trim = new List<int>(); var ridge = new List<int>();
        const int segments = 24, rings = 6;
        const float rx = .15f, rz = .163f, height = .165f;
        // Rounded protective shell, open underneath so it does not cross the head.
        for (int r = 0; r <= rings; r++)
        {
            float theta = (Mathf.PI * .5f) * r / rings;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices.Add(new Vector3(rx * Mathf.Sin(theta) * Mathf.Cos(angle), height * Mathf.Cos(theta), rz * Mathf.Sin(theta) * Mathf.Sin(angle)));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int i = 0; i < segments; i++)
            {
                int a = r * (segments + 1) + i, b = a + segments + 1;
                Quad(shell, a, a + 1, b + 1, b);
            }
        // Thin all-around brim, extended forward (+Z), with a dark lower safety band.
        int brim = vertices.Count;
        for (int row = 0; row < 4; row++)
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                bool outer = row == 1 || row == 2;
                float x = (outer ? .181f : rx) * Mathf.Cos(angle);
                float z = (outer ? .195f : rz) * Mathf.Sin(angle);
                if (outer && z > 0) z *= 1.16f;
                vertices.Add(new Vector3(x, row < 2 ? .002f : -.018f, z));
            }
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < segments; i++)
            {
                int a = brim + row * (segments + 1) + i, b = a + segments + 1;
                Quad(row == 2 ? trim : shell, a, a + 1, b + 1, b);
            }
        // Raised centre rib is part of the same mesh, not a separate runtime object.
        int rib = vertices.Count;
        for (int step = 0; step <= 12; step++)
        {
            float angle = Mathf.Lerp(-1.12f, 1.12f, step / 12f);
            float y = height * Mathf.Cos(angle) + .008f;
            float z = rz * Mathf.Sin(angle);
            vertices.Add(new Vector3(-.014f, y, z));
            vertices.Add(new Vector3(.014f, y, z));
        }
        for (int i = 0; i < 12; i++) Quad(ridge, rib + 2 * i, rib + 2 * i + 2, rib + 2 * i + 3, rib + 2 * i + 1);
        var mesh = new Mesh { name = "Engineer safety hard hat", subMeshCount = 3 };
        mesh.SetVertices(vertices); mesh.SetTriangles(shell, 0); mesh.SetTriangles(trim, 1); mesh.SetTriangles(ridge, 2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }
}
