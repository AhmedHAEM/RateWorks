using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using RateworksPrototype;

public static class Assignment2Setup
{
    [MenuItem("Rateworks/Configure assignment 2 cameras and lighting")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        var game = UnityEngine.Object.FindFirstObjectByType<FactoryGame>();
        if (game == null) throw new InvalidOperationException("Open RateworksPrototype first.");
        var presentation = game.GetComponent<FactoryPresentation>();
        if (presentation != null) { Debug.Log("Assignment 2 presentation is already configured."); return; }
        presentation = Undo.AddComponent<FactoryPresentation>(game.gameObject);
        Undo.RecordObject(game, "Connect assignment 2 presentation");
        Undo.RecordObject(game.engineer, "Connect camera tracking target");
        game.presentation = presentation; presentation.game = game;
        var rig = NewObject("Assignment 2 - Cinemachine and lighting", null);
        var eye = NewObject("Engineer camera target", game.engineer.transform);
        eye.localPosition = game.view.transform.localPosition;
        eye.localRotation = game.view.transform.localRotation;
        game.engineer.lookTarget = eye;
        game.engineer.gameObject.tag = "Player";
        // Disabled in edit mode: adding a Brain must not move the existing scene camera.
        var brain = Undo.AddComponent<CinemachineBrain>(game.view.gameObject);
        brain.enabled = false;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .65f);
        presentation.brain = brain;
        var fp = CameraRig("CM - First person", rig, eye, 20, 65);
        fp.gameObject.AddComponent<CinemachineHardLockToTarget>();
        fp.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
        presentation.firstPerson = fp;
        var tp = CameraRig("CM - Third person", rig, eye, 10, 65);
        var follow = tp.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        follow.Damping = new Vector3(.1f, .15f, .1f);
        follow.ShoulderOffset = new Vector3(.5f, 0, 0);
        follow.VerticalArmLength = .2f; follow.CameraDistance = 2.6f; follow.CameraSide = 1;
        follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings {
            Enabled = true, CollisionFilter = ~0, IgnoreTag = "Player", CameraRadius = .18f,
            DampingIntoCollision = 0, DampingFromCollision = .3f };
        presentation.thirdPerson = tp;
        var shot = CameraRig("CM - Contract complete", rig, null, 0, 65);
        shot.transform.position = new Vector3(0, 3.5f, -8.4f);
        shot.transform.rotation = Quaternion.LookRotation(new Vector3(0, .6f, 1) - shot.transform.position);
        presentation.completion = shot;
        var torch = NewObject("Engineer flashlight [F]", eye).gameObject.AddComponent<Light>();
        torch.type = LightType.Spot; torch.range = 16; torch.spotAngle = 55; torch.innerSpotAngle = 28;
        torch.color = new Color(1, .96f, .82f); torch.intensity = 18; torch.shadows = LightShadows.Soft;
        torch.enabled = false; presentation.flashlight = torch;
        presentation.bayLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(l => l.name.StartsWith("Bay light", StringComparison.OrdinalIgnoreCase)).ToArray();
        var signal = NewObject("Contract status beacon", rig);
        signal.position = new Vector3(0, 3.5f, 5.5f);
        var lamp = signal.gameObject.AddComponent<Light>();
        lamp.type = LightType.Point; lamp.range = 7; lamp.intensity = .8f; lamp.color = Color.cyan;
        presentation.statusLight = lamp;
        var lens = Primitive("Status lens", PrimitiveType.Sphere, signal, Vector3.zero, Vector3.one * .25f, new Color(.1f,.8f,1));
        presentation.statusLens = lens.GetComponent<Renderer>();
        presentation.statusLens.sharedMaterial.EnableKeyword("_EMISSION");
        var avatar = NewObject("Engineer - third person visual only", game.engineer.transform);
        Primitive("Work overalls", PrimitiveType.Capsule, avatar, new Vector3(0,.8f,0), new Vector3(.45f,.58f,.4f), new Color(.12f,.42f,.43f));
        Primitive("Hard hat", PrimitiveType.Sphere, avatar, new Vector3(0,1.5f,0), new Vector3(.4f,.3f,.4f), new Color(1,.65f,.08f));
        presentation.engineerAvatar = avatar.gameObject;
        avatar.gameObject.SetActive(false);
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.engineer); EditorUtility.SetDirty(presentation);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Debug.Log("Configured 3 Cinemachine views, flashlight, work lights and contract beacon. No existing item transforms changed.");
    }
    static Transform NewObject(string name, Transform parent)
    {
        var obj = new GameObject(name); Undo.RegisterCreatedObjectUndo(obj, "Assignment 2 setup");
        if (parent != null) obj.transform.SetParent(parent, false);
        return obj.transform;
    }
    static CinemachineCamera CameraRig(string name, Transform parent, Transform target, int priority, float fov)
    {
        var camera = NewObject(name, parent).gameObject.AddComponent<CinemachineCamera>();
        camera.Follow = target; camera.Priority = priority; camera.Lens.FieldOfView = fov;
        camera.Lens.NearClipPlane = .05f; camera.Lens.FarClipPlane = 150;
        return camera;
    }
    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name;
        Undo.RegisterCreatedObjectUndo(obj, "Assignment 2 setup");
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
        string path = "Assets/RateworksPrototype/Materials/Assignment2_" + name.Replace(" ", "_") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            AssetDatabase.CreateAsset(material, path);
        }
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }
}
