using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using RateworksPrototype;

public static class PrototypeRoomBuilder
{
    const string Root = "Assets/RateworksPrototype/";
    const string Industrial = "Assets/Donthrukat/Lowpolyindustrialpack/Prefabs/";
    static Material floor, wall, steel, teal, orange, yellow;
    static TMP_FontAsset font;
    static Transform room;

    static Material Mat(string name, Color color, Material source = null)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        if (source != null && source.mainTexture != null) m.SetTexture("_BaseMap", source.mainTexture);
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .22f);
        AssetDatabase.CreateAsset(m, Root + "Materials/" + name + ".mat"); return m;
    }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Model(string path, Transform parent, Vector3 pos, float maxWidth, Material material = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new Exception("Missing prefab: " + path);
        var wrapper = new GameObject(Path.GetFileNameWithoutExtension(path)); wrapper.transform.SetParent(parent, false);
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab); obj.transform.SetParent(wrapper.transform, false);
        obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.identity;
        var rs = obj.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = Mathf.Min(maxWidth / Mathf.Max(b.size.x, b.size.z), 2.7f / Mathf.Max(.01f, b.size.y));
        obj.transform.localScale *= s;
        obj.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
        if (material != null) foreach (var r in rs) r.sharedMaterials = r.sharedMaterials.Select(m => material).ToArray();
        foreach (var c in obj.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        var box = wrapper.AddComponent<BoxCollider>(); box.center = Vector3.up * b.size.y * s / 2;
        box.size = new Vector3(b.size.x * s, b.size.y * s, b.size.z * s);
        wrapper.transform.localPosition = pos; return wrapper;
    }
    static void Sign(string name, string text, Vector3 pos, float size, float width, Transform parent = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent == null ? room : parent, false); go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>(); t.font = font; t.text = text; t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center; t.color = new Color(.91f, .95f, .88f);
        t.rectTransform.sizeDelta = new Vector2(width, 1.5f); t.enableAutoSizing = false;
    }
    static void Arrow(Transform parent, Material mat)
    {
        Box("Direction shaft", parent, new Vector3(0, .65f, 0), new Vector3(1.1f, .035f, .12f), mat, false);
        Box("Arrow tip A", parent, new Vector3(.40f, .65f, .16f), new Vector3(.5f, .035f, .12f), mat, false).transform.localRotation = Quaternion.Euler(0, 45, 0);
        Box("Arrow tip B", parent, new Vector3(.40f, .65f, -.16f), new Vector3(.5f, .035f, .12f), mat, false).transform.localRotation = Quaternion.Euler(0, -45, 0);
    }
    static GameObject SavePart(string name, Action<Transform> create)
    {
        var g = new GameObject(name); create(g.transform);
        var prefab = PrefabUtility.SaveAsPrefabAsset(g, Root + "Prefabs/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(g); return prefab;
    }
    [MenuItem("Rateworks/Build prototype room")]
    public static string Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "Scenes/RateworksPrototype.unity") != null)
            throw new Exception("Prototype scene already exists; refusing to overwrite it.");
        foreach (string d in new[]{"Materials", "Prefabs", "Scenes"}) Directory.CreateDirectory(Root + d);
        AssetDatabase.Refresh();
        var original = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(original, Root + "Scenes/OriginalSceneSnapshot.unity", true);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        floor = Mat("Floor - warm concrete", new Color(.38f,.40f,.38f));
        wall = Mat("Walls - warm plaster", new Color(.61f,.65f,.61f));
        steel = Mat("Machine - dark steel", new Color(.13f,.19f,.21f));
        teal = Mat("Input - teal", new Color(.14f,.65f,.59f));
        orange = Mat("Dispatch - orange", new Color(.92f,.36f,.12f));
        yellow = Mat("Safety - gold", new Color(.95f,.71f,.22f));
        room = new GameObject("ONE ROOM - Rateworks test bay").transform;
        Box("Factory floor", room, new Vector3(0,-.15f,1), new Vector3(26,.3f,23), floor);
        Box("North wall", room, new Vector3(0,2.5f,12.5f), new Vector3(26,5,.3f), wall);
        Box("West wall", room, new Vector3(-13,2.5f,1), new Vector3(.3f,5,23), wall);
        Box("East wall", room, new Vector3(13,2.5f,1), new Vector3(.3f,5,23), wall);
        Box("South wall", room, new Vector3(0,2.5f,-10.5f), new Vector3(26,5,.3f), wall);
        Box("Back wall wainscot", room, new Vector3(0,.7f,12.29f), new Vector3(25.6f,1.4f,.08f), steel, false);
        for(int x=-12;x<=12;x+=4) Box("Structural column",room,new Vector3(x,2.4f,12.15f),new Vector3(.22f,4.8f,.28f),steel);
        for(int x=-8;x<=8;x+=4) {
            Box("Ceiling beam",room,new Vector3(x,4.85f,1),new Vector3(.17f,.22f,22),steel,false);
            Box("Hanging light",room,new Vector3(x,4.6f,1),new Vector3(.28f,.12f,3),wall,false);
        }
        var lineMat=Mat("Floor grid - pale",new Color(.65f,.69f,.63f));
        for(int x=0;x<=9;x++) Box("Grid line X",room,new Vector3(-9+x*2,.011f,1),new Vector3(.025f,.015f,10),lineMat,false);
        for(int z=0;z<=5;z++) Box("Grid line Z",room,new Vector3(0,.011f,-4+z*2),new Vector3(18,.015f,.025f),lineMat,false);
        Box("Build bay border left",room,new Vector3(-9.2f,.013f,1),new Vector3(.12f,.02f,10.4f),yellow,false);
        Box("Build bay border right",room,new Vector3(9.2f,.013f,1),new Vector3(.12f,.02f,10.4f),yellow,false);
        Box("Build bay border back",room,new Vector3(0,.013f,6.2f),new Vector3(18.5f,.02f,.12f),yellow,false);
        Box("Build bay border front",room,new Vector3(0,.013f,-4.2f),new Vector3(18.5f,.02f,.12f),yellow,false);
        Sign("Room title","RATEWORKS  /  TEST BAY 01",new Vector3(0,3.7f,12.28f),11,20);
        Sign("Wall instructions","IRON PLATES  >  ASSEMBLY  >  GEARS\nTARGET: 1 GEAR / SECOND",new Vector3(0,2.4f,12.26f),5,15);

        string[] props={"barrels/barrel_blue","boxes/box_one","boxes/box_opened","boxes/box_two","boxes/toolcrate","boxes/waterbox","boxes/woodenbox_I","boxes/woodenbox_x","lockers/electrolocker","lockers/locker1","lockers/locker2","lockers/metal cabinet","other/coil_dark","other/conditioner","other/pallet","pipes/bighightpipe","pipes/metalpipes","pipes/pipe_metal_corner","pipes/vent_corner","shelves/shelf1","shelves/shelf2","tanks/cylindertank_green","tanks/longtank_orange","trashcans/smalltrashcan_metal"};
        var sourceMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Donthrukat/Lowpolyindustrialpack/Materials/URPMaterial/Lowpolyindustrialpackmat_URP.mat");
        for(int i=0;i<props.Length;i++)
        {
            var c=Color.HSVToRGB((.05f+i*.037f)%1,.10f+(i%4)*.035f,.82f+(i%3)*.06f);
            var material=Mat("Prop " +(i+1).ToString("00")+" - "+Path.GetFileName(props[i]),c,sourceMat);
            Vector3 pos=i<10?new Vector3(-11+i*2.4f,0,9.6f):i<17?new Vector3(-11.3f,0,-7+(i-10)*2.3f):new Vector3(11.3f,0,-7+(i-17)*2.3f);
            Model(Industrial+props[i]+".prefab",room,pos,1.6f,material);
        }
        Model("Assets/Tyuris/Low_Poly_Industrial_Pack_Lite/Prefabs/Vehicle/Forklift.prefab",room,new Vector3(6,0,-7.5f),2.3f);
        Model("Assets/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Barrier.prefab",room,new Vector3(-6,0,-7.5f),2.6f);
        Model("Assets/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Cone.prefab",room,new Vector3(-4,0,-6.2f),.7f);

        var game=new GameObject("Prototype controller").AddComponent<FactoryGame>(); game.font=font;
        game.beltPrefab=SavePart("Conveyor",p=>{
            var model=Model(Industrial+"conveyors/conveyorbelt.prefab",p,new Vector3(0,.40f,0),1.65f,steel);
            Box("Belt support",p,new Vector3(0,.20f,0),new Vector3(1.4f,.4f,1.1f),steel); Arrow(p,yellow);
        });
        game.assemblerPrefab=SavePart("Basic Assembler",p=>{
            Model("Assets/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Machine/Machn_2.prefab",p,Vector3.zero,1.65f);
            Box("Base plinth",p,new Vector3(0,.08f,0),new Vector3(1.8f,.16f,1.8f),yellow);
            var rotor=Box("Working spindle",p,new Vector3(0,1.8f,0),new Vector3(.6f,.1f,.16f),orange,false);
            Arrow(p,yellow);
        });
        game.sourcePrefab=SavePart("Plate input",p=>{
            Box("Source body",p,new Vector3(0,.5f,0),new Vector3(1.5f,1,1.5f),teal);
            Box("Feed tray",p,new Vector3(.6f,.68f,0),new Vector3(.7f,.12f,1),steel);
            Sign("Port label","IN\nIRON PLATES",new Vector3(0,1.5f,-.7f),4,2,p);
        });
        game.dispatchPrefab=SavePart("Gear dispatch",p=>{
            Box("Dispatch body",p,new Vector3(0,.45f,0),new Vector3(1.6f,.9f,1.6f),orange);
            Box("Dispatch tray",p,new Vector3(0,.93f,0),new Vector3(1.4f,.05f,1.4f),steel);
            Sign("Port label","OUT\nGEARS",new Vector3(0,1.5f,-.7f),4,2,p);
        });
        game.platePrefab=SavePart("Iron plate",p=>Box("Plate",p,Vector3.zero,new Vector3(.38f,.08f,.30f),Mat("Product - iron",new Color(.65f,.77f,.80f)),false));
        game.gearPrefab=SavePart("Gear product",p=>{
            var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disc.transform.SetParent(p,false);disc.transform.localScale=new Vector3(.34f,.05f,.34f);disc.GetComponent<Renderer>().sharedMaterial=yellow;UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var tooth=Box("Tooth",p,new Vector3(Mathf.Cos(a)*.17f,0,Mathf.Sin(a)*.17f),new Vector3(.12f,.1f,.08f),yellow,false);tooth.transform.localRotation=Quaternion.Euler(0,-i*45,0);}
        });
        game.previewValid=Mat("Preview - available",new Color(.20f,.75f,.50f)); game.previewInvalid=Mat("Preview - occupied",new Color(.85f,.20f,.15f));
        var eng=new GameObject("Engineer");eng.transform.position=new Vector3(0,.1f,-7.5f);
        var cc=eng.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.30f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.25f;
        var controller=eng.AddComponent<EngineerController>();game.engineer=controller;controller.game=game;
        var cam=new GameObject("Main Camera");cam.tag="MainCamera";cam.transform.SetParent(eng.transform,false);cam.transform.localPosition=new Vector3(0,1.65f,0);cam.transform.localRotation=Quaternion.Euler(25,0,0);
        var camera=cam.AddComponent<Camera>();camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.backgroundColor=new Color(.39f,.47f,.48f);camera.clearFlags=CameraClearFlags.SolidColor;cam.AddComponent<AudioListener>();controller.view=camera;game.view=camera;
        var sun=new GameObject("Daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.8f;sun.color=new Color(1,.94f,.83f);sun.transform.rotation=Quaternion.Euler(55,-25,0);sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.68f,.72f);
        foreach(float x in new[]{-7f,0f,7f}){var l=new GameObject("Bay light").AddComponent<Light>();l.type=LightType.Point;l.transform.position=new Vector3(x,4,2);l.range=13;l.intensity=3;l.color=new Color(.86f,.95f,1);}
        // A visible example is authored into the scene, then replaced by the runtime build area.
        var display=new GameObject("Editor preview - hidden during play");display.AddComponent<PrototypePreview>();
        var example=new FactorySimulation();example.FillExample();
        foreach(var p in example.parts){var prefab=p.kind==PartKind.Source?game.sourcePrefab:p.kind==PartKind.Dispatch?game.dispatchPrefab:p.kind==PartKind.Assembler?game.assemblerPrefab:game.beltPrefab;var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);obj.transform.SetParent(display.transform);obj.transform.position=FactoryGame.World(p.x,p.z);}
        EditorSceneManager.SaveScene(scene,Root+"Scenes/RateworksPrototype.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/RateworksPrototype.unity",true)};
        AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,1),Quaternion.Euler(50,0,0),20);
        return "Created one room, 27 different imported props, 24 distinct prop materials plus floor, reusable production prefabs and first-person engineer.";
    }
    public static string CheckSimulation()
    {
        var s=new FactorySimulation();s.FillExample();for(int i=0;i<1800;i++)s.Tick();
        if(!s.Passed||s.Rate<1||s.Cost!=2600)throw new Exception("Example failed: "+s.Rate+" / "+s.Hold+" / "+s.Cost);
        s.Remove(4,1);for(int i=0;i<900;i++)s.Tick();
        if(s.Passed||s.Hold>0||s.Rate>.6f||s.Cost!=1600)throw new Exception("Bottleneck/refund check failed");
        s.FillExample();s.Add(PartKind.Assembler,4,0,0);for(int i=0;i<1800;i++)s.Tick();
        if(!s.Passed||s.Cost<=FactorySimulation.Budget)throw new Exception("Soft budget check failed");
        s.FillExample();s.At(3,1).direction=2;for(int i=0;i<1000;i++)s.Tick();
        if(s.Passed||s.Rate>.6f)throw new Exception("Direction blockage check failed");
        return "PASS: example throughput, sustained target, bottleneck reset, full refund, over-budget success, and rotated-belt blockage.";
    }
}
