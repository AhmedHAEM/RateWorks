using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using RateworksPrototype;

/// <summary>Project-owned visual setup; never modifies the imported character pack.</summary>
public static class CreativeEngineerSetup
{
    const string Pack = "Assets/ithappy/Creative_Characters_FREE/";
    const string Folder = "Assets/RateworksPrototype/Animations";
    const string ControllerPath = Folder + "/EngineerLocomotion.controller";
    const string VisualName = "Engineer - Creative Characters visual";
    const string UndoName = "Set up animated Creative Characters engineer";

    [MenuItem("Rateworks/Set up animated Creative Characters engineer")]
    public static void ApplyMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before setting up the engineer.");
        var game = UnityEngine.Object.FindFirstObjectByType<FactoryGame>();
        if (game == null || game.engineer == null || game.presentation == null)
            throw new InvalidOperationException("Open the configured Rateworks scene first.");
        var engineer = game.engineer;
        var existing = engineer.transform.Find(VisualName);
        if (existing != null)
        {
            var installed = engineer.GetComponent<EngineerAnimation>();
            if (installed != null && installed.animator != null && game.presentation.engineerAvatar == existing.gameObject)
                return "Animated Creative Characters engineer already installed; no duplicate created.";
            throw new InvalidOperationException("An incomplete Creative Characters visual exists; inspect it before rerunning setup.");
        }

        var prefab = Required<GameObject>(Pack + "Prefabs/Base_Mesh.prefab");
        var sourceAnimator = prefab.GetComponentInChildren<Animator>(true);
        if (sourceAnimator == null || sourceAnimator.avatar == null || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
            throw new InvalidOperationException("Creative Characters Base_Mesh needs its valid humanoid avatar.");

        // These are the same named mesh slots used by the pack's customization tool.
        var outfit = new Dictionary<string, SkinnedMeshRenderer>
        {
            { "Body", Part("Body/Body_010") },
            { "Faces", Part("Faces/Male_emotion_usual_001") },
            { "Hairstyle", Part("Hairstyle/Hairstyle_Male_001") },
            { "Outerwear", Part("Outwear/Outwear_004") },
            { "Pants", Part("Pants/Pants_010") },
            { "Shoes", Part("Shoes/Shoe_Sneakers_009") }
        };
        foreach (string slot in outfit.Keys)
            if (!prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == slot))
                throw new InvalidOperationException("Missing character mesh slot: " + slot);
        var controller = BuildController();
        var body = engineer.GetComponent<CharacterController>();
        Vector3 originalPosition = engineer.transform.position;
        Quaternion originalRotation = engineer.transform.rotation;
        Vector3 originalScale = engineer.transform.localScale;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, engineer.transform);
            Undo.RegisterCreatedObjectUndo(visual, UndoName);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = VisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // The imported visual must not add a second movement/input/physics authority.
            foreach (var component in visual.GetComponentsInChildren<MonoBehaviour>(true)) Undo.DestroyObjectImmediate(component);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
            foreach (var rigidbody in visual.GetComponentsInChildren<Rigidbody>(true)) Undo.DestroyObjectImmediate(rigidbody);
            foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Undo.DestroyObjectImmediate(camera);
            foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true)) Undo.DestroyObjectImmediate(listener);

            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!outfit.TryGetValue(renderer.name, out var source)) { renderer.enabled = false; continue; }
                renderer.sharedMesh = source.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.localBounds = source.sharedMesh.bounds;
                renderer.enabled = true;
                renderer.updateWhenOffscreen = true;
            }

            // Fit only the newly added model to the existing 1.8 m controller.
            Bounds bounds = VisualBounds(visual.transform);
            float height = body != null ? body.height : 1.8f;
            float feet = body != null ? body.center.y - body.height * .5f : 0;
            float scale = height / Mathf.Max(bounds.size.y, .01f);
            visual.transform.localScale = Vector3.one * scale;
            // CharacterController rests one skin-width above the floor. Lower only
            // the artwork so the soles touch the floor without changing the hitbox.
            float skin = body != null ? body.skinWidth : 0;
            visual.transform.localPosition = new Vector3(-bounds.center.x * scale, feet - bounds.min.y * scale - skin, -bounds.center.z * scale);

            var animator = visual.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            foreach (var extra in visual.GetComponentsInChildren<Animator>(true))
                if (extra != animator) extra.enabled = false;

            var driver = engineer.GetComponent<EngineerAnimation>();
            if (driver == null) driver = Undo.AddComponent<EngineerAnimation>(engineer.gameObject);
            else Undo.RecordObject(driver, UndoName);
            driver.engineer = engineer; driver.animator = animator; driver.referenceSpeed = 4.5f; driver.blendTime = .12f;
            var oldVisual = game.presentation.engineerAvatar;
            if (oldVisual != null)
            {
                Undo.RecordObject(oldVisual, UndoName);
                oldVisual.SetActive(false); // Retain the primitive avatar for recovery/Undo.
            }
            Undo.RecordObject(game.presentation, UndoName);
            game.presentation.engineerAvatar = visual;
            visual.SetActive(false); // Existing presentation reveals it in third person and the winning shot.

            if (engineer.transform.position != originalPosition || engineer.transform.rotation != originalRotation || engineer.transform.localScale != originalScale)
                throw new InvalidOperationException("Engineer root transform unexpectedly changed.");
            EditorUtility.SetDirty(driver); EditorUtility.SetDirty(game.presentation);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            foreach (var clip in controller.animationClips) AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.SaveAssetIfDirty(controller); Undo.CollapseUndoOperations(undoGroup);
            return "Installed Creative Characters humanoid with neutral face, shirt, pants and sneakers; " +
                "looping idle/forward/backward/left/right blend, root motion off. Existing movement, hitbox and camera targets retained. Scene ready to save.";
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    static T Required<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }
    static SkinnedMeshRenderer Part(string name)
    {
        var source = Required<GameObject>(Pack + "Prefabs/" + name + ".prefab").GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (source == null || source.sharedMesh == null) throw new InvalidOperationException("Missing skinned mesh in " + name);
        return source;
    }
    static AnimatorController BuildController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RateworksPrototype", "Animations");
        string[] names = { "Idle_Breathing", "Walk_Forward", "Walk_Backward", "Strafe_Left", "Strafe_Right" };
        var clips = names.Select(LoopingCopy).ToArray();
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Forward", AnimatorControllerParameterType.Float);
        var tree = new BlendTree
        {
            name = "Directional engineer locomotion", blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "Horizontal", blendParameterY = "Forward", useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(clips[0], Vector2.zero);
        tree.AddChild(clips[1], Vector2.up);
        tree.AddChild(clips[2], Vector2.down);
        tree.AddChild(clips[3], Vector2.left);
        tree.AddChild(clips[4], Vector2.right);
        var children = tree.children;
        for (int i = 1; i < children.Length; i++) children[i].timeScale = 1.2f;
        tree.children = children;
        var machine = controller.layers[0].stateMachine;
        var state = machine.AddState("Idle and directional walking"); state.motion = tree;
        machine.defaultState = state;
        EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller);
        return controller;
    }
    static AnimationClip LoopingCopy(string name)
    {
        string destination = Folder + "/Engineer_" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
        if (clip != null) return clip;
        var source = Required<AnimationClip>(Pack + "Animations/Other_Animations/" + name + ".anim");
        clip = UnityEngine.Object.Instantiate(source); clip.name = "Engineer " + name;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true; settings.loopBlend = true;
        settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = WrapMode.Loop;
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        AssetDatabase.CreateAsset(clip, destination);
        return clip;
    }
    static Bounds VisualBounds(Transform root)
    {
        bool hasBounds = false; Bounds result = default;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            Bounds bounds = renderer.sharedMesh.bounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                point = matrix.MultiplyPoint3x4(point);
                if (!hasBounds) { result = new Bounds(point, Vector3.zero); hasBounds = true; }
                else result.Encapsulate(point);
            }
        }
        if (!hasBounds) throw new InvalidOperationException("The character has no visible skinned meshes.");
        return result;
    }
}
