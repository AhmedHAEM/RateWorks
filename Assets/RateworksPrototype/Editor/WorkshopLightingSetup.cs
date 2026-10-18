using System;
using System.Linq;
using RateworksPrototype;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Fits controllable light sources to existing fixtures without moving room artwork.</summary>
public static class WorkshopLightingSetup
{
    const string UndoName = "Configure workshop lighting and wall switch";
    const string SwitchName = "Ceiling light switch [E]";
    const string LampName = "Ceiling fixture light";
    const string MaterialFolder = "Assets/RateworksPrototype/Materials/";

    [MenuItem("Rateworks/Configure workshop light switch")]
    public static void ApplyMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode first.");
        var game = UnityEngine.Object.FindFirstObjectByType<FactoryGame>();
        if (game == null || game.presentation == null || game.presentation.flashlight == null || game.presentation.engineerAvatar == null)
            throw new InvalidOperationException("Open the configured Rateworks scene first.");
        var scene = game.gameObject.scene;
        var fixtures = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Renderer>(true))
            .Where(r => r.name == "Hanging light").OrderBy(r => r.transform.position.x).ToArray();
        if (fixtures.Length != 5) throw new InvalidOperationException("Expected the five existing Hanging light models; inspect before applying.");
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null || !pipeline.useRenderingLayers)
            throw new InvalidOperationException("The active URP asset must support rendering layers for selective flashlight shadows.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
        var presentation = game.presentation;
        Undo.RecordObject(presentation, UndoName); Undo.RecordObject(game, UndoName);
        var lampMaterial = Material("Workshop - luminous fixture", new Color(.84f, .92f, 1f), true);
        var lamps = new Light[fixtures.Length];
        for (int i = 0; i < fixtures.Length; i++)
        {
            var fixture = fixtures[i];
            Undo.RecordObject(fixture, UndoName);
            fixture.sharedMaterial = lampMaterial;
            var child = fixture.transform.Find(LampName);
            if (child == null)
            {
                var obj = new GameObject(LampName);
                Undo.RegisterCreatedObjectUndo(obj, UndoName);
                // Keep the light's world scale independent of the long, thin fixture mesh.
                obj.transform.position = new Vector3(fixture.bounds.center.x, fixture.bounds.min.y - .035f, fixture.bounds.center.z);
                obj.transform.rotation = Quaternion.Euler(90, 0, 0);
                obj.transform.SetParent(fixture.transform, true);
                child = obj.transform;
            }
            var light = child.GetComponent<Light>();
            if (light == null) light = Undo.AddComponent<Light>(child.gameObject);
            Undo.RecordObject(light, UndoName);
            light.type = LightType.Spot; light.spotAngle = 140; light.innerSpotAngle = 105;
            light.range = 19; light.intensity = 32; light.color = new Color(.84f, .92f, 1f);
            light.shadows = LightShadows.Soft; light.shadowBias = .035f; light.shadowNormalBias = .2f;
            light.shadowNearPlane = .1f; light.enabled = true; light.lightmapBakeType = LightmapBakeType.Realtime;
            lamps[i] = light;
        }
        // Keep old light objects recoverable, but remove their unmotivated floating fill.
        foreach (var old in presentation.bayLights ?? Array.Empty<Light>())
            if (old != null && !lamps.Contains(old)) { Undo.RecordObject(old, UndoName); old.enabled = false; }
        presentation.bayLights = lamps;
        presentation.bayLightLenses = fixtures;
        presentation.buildLightIntensity = 32; presentation.runLightIntensity = 40;

        // World = bit 0, engineer artwork = bit 1. Preserve player shadows from room lights,
        // but exclude the animated face/hat from this one spotlight's shadow casters.
        foreach (var renderer in presentation.engineerAvatar.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, UndoName); renderer.renderingLayerMask = 2u;
        }
        foreach (var light in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Light>(true)))
        {
            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalLightData>(light.gameObject);
            Undo.RecordObject(data, UndoName); Undo.RecordObject(light, UndoName);
            data.renderingLayers = 3u;
            data.customShadowLayers = light == presentation.flashlight;
            data.shadowRenderingLayers = light == presentation.flashlight ? 1u : 3u;
            if (light.type == LightType.Directional) { light.intensity = .8f; light.shadowStrength = .75f; }
        }
        // Physical light origin just ahead of the face, still aimed by the engineer's look target.
        var torch = presentation.flashlight;
        Undo.RecordObject(torch.transform, UndoName);
        torch.transform.localPosition = new Vector3(.22f, -.12f, .38f);
        torch.transform.localRotation = Quaternion.identity;
        torch.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        // Readable machine fronts and corners, while switched ceiling lighting stays visibly distinct.
        RenderSettings.ambientLight = new Color(.55f, .58f, .62f);
        RenderSettings.reflectionIntensity = .25f;

        var existing = scene.GetRootGameObjects().FirstOrDefault(o => o.name == SwitchName);
        RoomLightSwitch wallSwitch;
        if (existing == null)
        {
            // Clear rear-wall space just right of spawn; existing tanks/props remain untouched.
            var root = new GameObject(SwitchName);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.position = new Vector3(3.25f, 1.45f, -10.29f);
            wallSwitch = Undo.AddComponent<RoomLightSwitch>(root);
            var housing = Material("Workshop - switch housing", new Color(.12f, .16f, .18f), false);
            var face = Material("Workshop - switch face", new Color(.8f, .73f, .49f), false);
            var handle = Material("Workshop - switch rocker", new Color(.10f, .13f, .14f), false);
            var indicator = Material("Workshop - switch indicator", new Color(.15f, 1f, .45f), true);
            Box("Switch enclosure", root.transform, Vector3.zero, new Vector3(.48f, .65f, .12f), housing);
            Box("Faceplate", root.transform, new Vector3(0, 0, .074f), new Vector3(.41f, .58f, .035f), face);
            wallSwitch.lever = Box("On off rocker", root.transform, new Vector3(0, -.055f, .115f), new Vector3(.20f, .28f, .075f), handle).transform;
            wallSwitch.indicator = Box("Power indicator", root.transform, new Vector3(0, .2f, .102f), new Vector3(.12f, .045f, .018f), indicator).GetComponent<Renderer>();
            var collider = Undo.AddComponent<BoxCollider>(root);
            collider.center = new Vector3(0, 0, .03f); collider.size = new Vector3(.48f, .65f, .2f);
            var point = new GameObject("Interaction point"); Undo.RegisterCreatedObjectUndo(point, UndoName);
            point.transform.SetParent(root.transform, false); point.transform.localPosition = new Vector3(0, 0, .19f);
            wallSwitch.interactionPoint = point.transform;
            var labelObject = new GameObject("Ceiling lights label"); Undo.RegisterCreatedObjectUndo(labelObject, UndoName);
            labelObject.transform.SetParent(root.transform, false); labelObject.transform.localPosition = new Vector3(0, .65f, .08f);
            labelObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = labelObject.AddComponent<TextMeshPro>(); text.font = game.font;
            text.text = "CEILING LIGHTS\n<size=65%>[E]  ON / OFF</size>";
            text.fontSize = 1.25f; text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.88f, .94f, .86f); text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(1.5f, .45f);
        }
        else wallSwitch = existing.GetComponent<RoomLightSwitch>();
        if (wallSwitch == null) throw new InvalidOperationException("The existing switch root has no RoomLightSwitch component.");
        Undo.RecordObject(wallSwitch, UndoName);
        wallSwitch.game = game; wallSwitch.presentation = presentation; wallSwitch.interactionDistance = 2.7f;
        game.roomLightSwitch = wallSwitch;
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(wallSwitch);
        EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
        return "Five existing ceiling fixtures now emit light; physical E wall switch added. Flashlight shadows exclude only engineer artwork, while room lights retain character shadows. Existing room models and hitboxes unchanged.";
    }

    static Material Material(string name, Color color, bool emissive)
    {
        string path = MaterialFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .3f);
        if (emissive)
        {
            material.SetColor("_EmissionColor", color * 2.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
        Undo.RegisterCreatedObjectUndo(obj, UndoName);
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
    }
}
