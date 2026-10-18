using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace RateworksPrototype
{
    public sealed class FactoryGame : MonoBehaviour
    {
        public GameObject beltPrefab, assemblerPrefab, sourcePrefab, dispatchPrefab, platePrefab, gearPrefab;
        public Material previewValid, previewInvalid;
        public TMP_FontAsset font;
        public EngineerController engineer;
        public Camera view;
        public FactoryPresentation presentation;
        public RoomLightSwitch roomLightSwitch;
        public FactorySimulation Simulation { get; private set; }
        public bool Playing { get; private set; }
        public bool Running { get; private set; }
        public bool Building => Playing && !Running && !ResultsVisible;
        public bool ResultsVisible { get; private set; }
        public bool Paused { get; private set; }
        public float Speed { get; private set; } = 1;
        public int BestCost { get; private set; }
        const string BestKey = "RateworksPrototype.OneRoom.BestCost";
        readonly Dictionary<FactoryPart, GameObject> visuals = new Dictionary<FactoryPart, GameObject>();
        readonly Dictionary<FactoryPart, GameObject> heldItems = new Dictionary<FactoryPart, GameObject>();
        readonly List<MovingItem> movingItems = new List<MovingItem>();
        sealed class MovingItem { public GameObject obj; public Vector3 start, end; public float t; }
        Transform factoryRoot, tokenRoot;
        GameObject ghost, briefing, results, crosshair, gameplayHUD, buildPanel, inspectorPanel;
        TMP_Text rateText, costText, inspectionText, toolText, helpText, resultText;
        UnityEngine.UI.Image progress;
        Renderer footprintRenderer;
        TMP_Text pauseLabel, runLabel, speedLabel;
        readonly List<UnityEngine.UI.Button> buildButtons = new List<UnityEngine.UI.Button>();
        FactoryPart selected, hovered;
        int tool = -1, direction;
        float accumulator;
        bool showedResult;
        float noticeUntil;
        string notice = "Start with an assembler and conveyors. Arrows point toward dispatch.";
        public static Vector3 World(int x, int z) => new Vector3(-8 + x * 2, 0, -3 + z * 2);

        void Start()
        {
            Simulation = new FactorySimulation(); Simulation.Transferred += OnTransfer;
            BestCost = PlayerPrefs.GetInt(BestKey, 0);
            factoryRoot = new GameObject("Placed factory parts").transform;
            tokenRoot = new GameObject("Moving products").transform;
            RebuildVisuals(); BuildUI();
            ghost = new GameObject("Placement footprint and direction");
            GhostBox("Footprint", Vector3.zero, new Vector3(1.85f, .025f, 1.85f), 0);
            GhostBox("Direction shaft", new Vector3(0, .035f, 0), new Vector3(.9f, .025f, .09f), 0);
            GhostBox("Arrow tip A", new Vector3(.32f, .035f, .12f), new Vector3(.4f, .025f, .09f), 45);
            GhostBox("Arrow tip B", new Vector3(.32f, .035f, -.12f), new Vector3(.4f, .025f, .09f), -45);
            ghost.SetActive(false);
            engineer.ResetPosition(); UpdateHUD();
        }
        void GhostBox(string name, Vector3 position, Vector3 scale, float angle)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(ghost.transform, false); part.transform.localPosition = position;
            part.transform.localScale = scale; part.transform.localRotation = Quaternion.Euler(0, angle, 0);
            part.GetComponent<Collider>().enabled = false; Destroy(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            if (name == "Footprint") { footprintRenderer = renderer; renderer.sharedMaterial = previewValid; }
            else
            {
                var arrow = beltPrefab.transform.Find("Direction shaft");
                renderer.sharedMaterial = arrow != null ? arrow.GetComponent<Renderer>().sharedMaterial : previewValid;
            }
        }
        void Notify(string message, float seconds = 4f) { notice = message; noticeUntil = Time.unscaledTime + seconds; }
        public void Begin()
        {
            Playing = true; briefing.SetActive(false); engineer.SetCursor(false);
            if (presentation != null) presentation.Begin();
            Notify("BUILD mode: place your machines, then press Enter to run the contract.", 6);
        }
        public void ChooseTool(int value)
        {
            if (!Building) { Notify("Stop the run with Enter before changing the layout."); return; }
            tool = value; selected = null; engineer.SetCursor(false);
        }
        public void CancelTool() { tool = -1; }
        public void TogglePause()
        {
            if (!Running || ResultsVisible) return;
            Paused = !Paused; Notify(Paused ? "Run paused. Press Space to resume, or Enter to return to Build." : "Run resumed.");
        }
        public void ToggleSpeed() { Speed = Speed < 1 ? 1 : Speed < 2 ? 2 : .5f; }
        public void ToggleRun() { if (!Playing || ResultsVisible) return; if (Running) StopRun(); else StartRun(); }
        public void StartRun()
        {
            if (!Building) return;
            Running = true; Paused = false; tool = -1; accumulator = 0; showedResult = false;
            Simulation.ResetFlow(); ClearTokens();
            if (presentation != null) presentation.OnRunChanged(true);
            Notify("RUN mode: watch the output. Enter stops and returns to Build; Space pauses.", 5);
        }
        public void StopRun()
        {
            Running = false; Paused = false; accumulator = 0;
            if (Simulation != null) Simulation.ResetFlow(); ClearTokens();
            if (presentation != null) presentation.OnRunChanged(false);
            Notify("BUILD mode: products and timers cleared. Layout and factory cost retained.", 5);
        }
        public void KeepOptimizing()
        {
            StopRun(); ResultsVisible = false; results.SetActive(false); engineer.SetCursor(false);
            if (presentation != null) presentation.ReturnToGameplay();
        }
        public void Restart()
        {
            Simulation.Transferred -= OnTransfer; Simulation = new FactorySimulation(); Simulation.Transferred += OnTransfer;
            Running = false; Paused = false; Speed = 1; accumulator = 0; showedResult = false; ResultsVisible = false;
            selected = null; tool = -1; results.SetActive(false); ClearTokens(); RebuildVisuals(); engineer.ResetPosition(); engineer.SetCursor(false);
            if (presentation != null) presentation.ResetPresentation();
            Notify("Fresh layout in Build mode. Two assemblers can meet the contract.");
        }
        public void LoadExample()
        {
            if (!Building) { Notify("Stop the run before loading an example layout."); return; }
            engineer.ResetPosition();
            Simulation.FillExample(); selected = null; ClearTokens(); RebuildVisuals(); showedResult = false;
            ResultsVisible = false; results.SetActive(false); Paused = false; accumulator = 0; tool = -1;
            Notify("Example loaded: 2 assemblers, 12 belts, $2,600. Press Enter to start the run.", 6);
            engineer.SetCursor(false);
        }
        public bool Place(PartKind kind, int x, int z, int facing)
        {
            if (!CanPlace(kind, x, z, facing, out var reason)) { Notify(reason); return false; }
            var p = Simulation.Add(kind, x, z, facing);
            if (p == null) return false;
            AddVisual(p); LayoutChanged(); return true;
        }
        public bool RemoveAt(int x, int z)
        {
            if (!Building) { Notify("Stop the run before removing parts."); return false; }
            var part = Simulation.At(x, z);
            if (part == null || part.Cost == 0) return false;
            Simulation.Remove(x, z);
            if (visuals.TryGetValue(part, out var obj)) { obj.SetActive(false); Destroy(obj); visuals.Remove(part); }
            if (selected == part) selected = null;
            LayoutChanged(); return true;
        }
        public bool RotateAt(int x, int z)
        {
            if (!Building) { Notify("Stop the run before rotating parts."); return false; }
            var part = Simulation.At(x, z);
            if (part == null || part.Cost == 0) return false;
            int facing = (part.direction + 1) % 4;
            var obj = visuals[part];
            if (!SpaceIsClear(part.kind, x, z, facing, obj.transform, out var reason)) { Notify(reason); return false; }
            part.direction = facing; obj.transform.rotation = Quaternion.Euler(0, facing * 90, 0);
            LayoutChanged(); return true;
        }
        public bool CanPlace(PartKind kind, int x, int z, int facing, out string reason)
        {
            reason = "";
            if (!Building) { reason = "Stop the run before changing the layout."; return false; }
            if (kind != PartKind.Belt && kind != PartKind.Assembler) { reason = "Choose a belt or an assembler."; return false; }
            if (!FactorySimulation.Inside(x, z)) { reason = "Build inside the marked factory grid."; return false; }
            if (Simulation.At(x, z) != null) { reason = "That cell is occupied. Right click the part to inspect or remove it."; return false; }
            return SpaceIsClear(kind, x, z, facing, null, out reason);
        }
        bool SpaceIsClear(PartKind kind, int x, int z, int facing, Transform ignore, out string reason)
        {
            reason = "";
            var prefab = kind == PartKind.Belt ? beltPrefab : assemblerPrefab;
            if (prefab == null) { reason = "The selected part has no prefab."; return false; }
            Quaternion rotation = Quaternion.Euler(0, facing * 90, 0);
            Matrix4x4 placement = Matrix4x4.TRS(World(x, z), rotation, prefab.transform.localScale);
            Physics.SyncTransforms();
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Bounds localBounds;
                if (collider is BoxCollider box) localBounds = new Bounds(box.center, box.size);
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null) localBounds = mesh.sharedMesh.bounds;
                else if (collider is SphereCollider sphere) localBounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
                else if (collider is CapsuleCollider capsule)
                {
                    Vector3 size = Vector3.one * capsule.radius * 2; size[capsule.direction] = capsule.height;
                    localBounds = new Bounds(capsule.center, size);
                }
                else continue;
                Matrix4x4 relative = prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                Matrix4x4 matrix = placement * relative;
                Vector3 scale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
                Vector3 half = Vector3.Scale(localBounds.size, scale) * .5f;
                half = Vector3.Max(half - Vector3.one * .015f, Vector3.one * .005f);
                Quaternion orientation = rotation * Quaternion.Inverse(prefab.transform.rotation) * collider.transform.rotation;
                foreach (var hit in Physics.OverlapBox(matrix.MultiplyPoint3x4(localBounds.center), half, orientation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.bounds.max.y <= .05f || (ignore != null && hit.transform.IsChildOf(ignore))) continue;
                    if (hit.transform.IsChildOf(engineer.transform)) { reason = "Step clear of this footprint before building or rotating."; return false; }
                    // Grid occupancy governs factory-to-factory placement. Adjacent cells
                    // intentionally meet at feed trays and extended conveyor ends; those
                    // connector overlaps must not reject an otherwise empty build cell.
                    var neighbor = hit.GetComponentInParent<FactoryPartMarker>();
                    if (neighbor != null && Simulation.At(neighbor.x, neighbor.z) != null &&
                        Mathf.Abs(neighbor.x - x) + Mathf.Abs(neighbor.z - z) == 1) continue;
                    reason = "This footprint overlaps an existing object. Choose a clear cell."; return false;
                }
            }
            return true;
        }
        void LayoutChanged()
        {
            Simulation.ResetFlow(); accumulator = 0; ClearTokens(); showedResult = false;
            Notify("Layout updated. Press Enter to run; removed parts receive a full refund.", 2.5f);
        }
        void Update()
        {
            if (Simulation == null) return;
            if (Playing && !ResultsVisible)
            {
                var k = Keyboard.current; var m = Mouse.current;
                bool cinematic = presentation != null && presentation.IsCinematic;
                if (k != null && !cinematic)
                {
                    if (k.bKey.wasPressedThisFrame) ChooseTool(0);
                    if (k.mKey.wasPressedThisFrame) ChooseTool(1);
                    if (k.spaceKey.wasPressedThisFrame) TogglePause();
                    if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) ToggleRun();
                    if (k.digit0Key.wasPressedThisFrame) Speed = .5f;
                    if (k.digit1Key.wasPressedThisFrame) Speed = 1;
                    if (k.digit2Key.wasPressedThisFrame) Speed = 2;
                    if (k.hKey.wasPressedThisFrame) Notify("HINT: two IN-to-OUT rows; each needs one assembler and six belts pointing right (+X). Enter runs the layout.", 7);
                    if (k.rKey.wasPressedThisFrame)
                    {
                        if (Building && tool >= 0) direction = (direction + 1) % 4;
                        else if (selected != null && selected.Cost > 0) RotateAt(selected.x, selected.z);
                    }
                    if ((k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) && selected != null && selected.Cost > 0)
                        RemoveAt(selected.x, selected.z);
                }
                Aim();
                bool overUI = engineer.CursorMode && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (m != null && !overUI && !cinematic)
                {
                    if (m.rightButton.wasPressedThisFrame) { CancelTool(); selected = hovered; }
                    if (m.leftButton.wasPressedThisFrame && hasCell)
                    {
                        if (tool >= 0)
                            Place(tool == 0 ? PartKind.Belt : PartKind.Assembler, aimX, aimZ, direction);
                        else selected = hovered;
                    }
                }
                if (Running && !Paused)
                {
                    accumulator += Mathf.Min(Time.deltaTime, 0.2f) * Speed;
                    while (accumulator >= FactorySimulation.Step) { Simulation.Tick(); accumulator -= FactorySimulation.Step; }
                    AnimateProducts(Time.deltaTime * Speed);
                }
                if (Running && Simulation.Passed && !showedResult) ShowResults();
            }
            else if (ghost != null) ghost.SetActive(false);
            SyncHeldItems(); UpdateHUD();
        }
        int aimX, aimZ; bool hasCell;
        void Aim()
        {
            var pos = engineer.CursorMode && Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = view.ScreenPointToRay(pos);
            Vector3 point = Vector3.zero; bool hitCell = false;
            var hits = Physics.RaycastAll(ray, 22f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(engineer.transform)) continue;
                var marker = hit.collider.GetComponentInParent<FactoryPartMarker>();
                if (marker != null) { point = World(marker.x, marker.z); hitCell = true; }
                else if (hit.collider.bounds.max.y <= .05f && hit.normal.y > .5f) { point = hit.point; hitCell = true; }
                break;
            }
            aimX = Mathf.RoundToInt((point.x + 8) / 2); aimZ = Mathf.RoundToInt((point.z + 3) / 2);
            hasCell = hitCell && FactorySimulation.Inside(aimX, aimZ);
            hovered = hasCell ? Simulation.At(aimX, aimZ) : null;
            ghost.SetActive(hasCell && tool >= 0 && Building && (presentation == null || !presentation.IsCinematic));
            if (ghost.activeSelf)
            {
                ghost.transform.position = World(aimX, aimZ) + Vector3.up * 0.035f;
                ghost.transform.rotation = Quaternion.Euler(0, direction * 90, 0);
                bool valid = CanPlace(tool == 0 ? PartKind.Belt : PartKind.Assembler, aimX, aimZ, direction, out _);
                footprintRenderer.sharedMaterial = valid ? previewValid : previewInvalid;
            }
        }
        void AddVisual(FactoryPart p)
        {
            GameObject prefab = p.kind == PartKind.Belt ? beltPrefab : p.kind == PartKind.Assembler ? assemblerPrefab : p.kind == PartKind.Source ? sourcePrefab : dispatchPrefab;
            var obj = Instantiate(prefab, World(p.x, p.z), Quaternion.Euler(0, p.direction * 90, 0), factoryRoot);
            obj.name = p.kind + " [" + p.x + "," + p.z + "]";
            var marker = obj.AddComponent<FactoryPartMarker>(); marker.x = p.x; marker.z = p.z;
            visuals[p] = obj;
        }
        void RebuildVisuals()
        {
            foreach (var obj in visuals.Values) if (obj != null) { obj.SetActive(false); Destroy(obj); }
            visuals.Clear(); foreach (var p in Simulation.parts) AddVisual(p);
        }
        void OnTransfer(FactoryPart from, FactoryPart to, Product product)
        {
            var obj = Instantiate(product == Product.Plate ? platePrefab : gearPrefab, tokenRoot);
            movingItems.Add(new MovingItem { obj = obj, start = World(from.x, from.z) + Vector3.up * 0.8f, end = World(to.x, to.z) + Vector3.up * 0.8f });
            obj.transform.position = movingItems[movingItems.Count - 1].start;
        }
        void AnimateProducts(float dt)
        {
            for (int i = movingItems.Count - 1; i >= 0; i--)
            {
                var t = movingItems[i]; t.t += dt / 0.28f;
                t.obj.transform.position = Vector3.Lerp(t.start, t.end, t.t);
                t.obj.transform.Rotate(0, dt * 90, 0);
                if (t.t >= 1) { Destroy(t.obj); movingItems.RemoveAt(i); }
            }
            foreach (var p in Simulation.parts)
                if (p.kind == PartKind.Assembler && p.processing && visuals.TryGetValue(p, out var obj))
                { var rotor = obj.transform.Find("Working spindle"); if (rotor != null) rotor.Rotate(0, dt * 140, 0); }
        }
        void SyncHeldItems()
        {
            foreach (var p in Simulation.parts)
            {
                if (p.output != Product.None && !heldItems.ContainsKey(p))
                    heldItems[p] = Instantiate(p.output == Product.Plate ? platePrefab : gearPrefab, World(p.x, p.z) + Vector3.up * 0.82f, Quaternion.identity, tokenRoot);
                else if (p.output == Product.None && heldItems.TryGetValue(p, out var obj)) { Destroy(obj); heldItems.Remove(p); }
            }
        }
        void ClearTokens()
        {
            foreach (var t in movingItems) if (t.obj != null) Destroy(t.obj);
            foreach (var t in heldItems.Values) if (t != null) Destroy(t);
            movingItems.Clear(); heldItems.Clear();
        }
        void ShowResults()
        {
            showedResult = true; ResultsVisible = true;
            if (BestCost == 0 || Simulation.Cost < BestCost) { BestCost = Simulation.Cost; PlayerPrefs.SetInt(BestKey, BestCost); PlayerPrefs.Save(); }
            resultText.text = "<size=40>CONTRACT COMPLETE</size>\n\n1.00 gear/s sustained for 20 seconds\n" +
                $"Measured rate: {Simulation.Rate:0.00}/s\nFactory cost: ${Simulation.Cost:N0}   /   Budget: ${FactorySimulation.Budget:N0}\n\n" +
                (Simulation.Cost <= FactorySimulation.Budget ? "<color=#8FDEB5>BUDGET TARGET MET</color>" : "<color=#F1BB70>PASS — OVER BUDGET</color>\nA cheaper layout is optional.") +
                $"\nPersonal best: ${BestCost:N0}";
            if (presentation != null) presentation.OnCompleted();
            results.SetActive(presentation == null || !presentation.IsCinematic); engineer.SetCursor(true);
        }
        void UpdateHUD()
        {
            if (rateText == null) return;
            gameplayHUD.SetActive(Playing && !ResultsVisible && (presentation == null || !presentation.IsCinematic));
            results.SetActive(ResultsVisible && (presentation == null || !presentation.IsCinematic));
            buildPanel.SetActive(engineer.CursorMode);
            rateText.text = $"<b>{(Running ? "RUN" : "BUILD")}  |  {Simulation.Rate:0.00} / 1.00 gear/s</b>\nHold {Simulation.Hold:0.0} / 20 s  |  {(Paused ? "PAUSED" : Speed.ToString("0.#") + "x")}";
            costText.text = $"<b>${Simulation.Cost:N0}</b> / ${FactorySimulation.Budget:N0} budget" + (Simulation.Cost <= FactorySimulation.Budget ? "" : "\n<color=#F1BB70>Over budget — passing still allowed</color>");
            progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Simulation.Hold / FactorySimulation.HoldSeconds), 1);
            string facing = new[] { "RIGHT (+X)", "BACK (-Z)", "LEFT (-X)", "FORWARD (+Z)" }[direction];
            toolText.text = Running ? "RUN  /  Inspect any part  |  Enter stops the run and returns to Build" : tool < 0 ? "BUILD  /  Click to inspect  |  R rotate  |  Delete refund  |  Enter run" : (tool == 0 ? "LOGISTICS: BELT $50" : "MACHINES: ASSEMBLER $1,000") + "  /  " + facing + "  /  R rotate";
            helpText.text = "WASD Move  |  Tab Menu  |  Enter Run/Stop  |  V Camera  |  F Flashlight  |  E Wall switch  |  H Hint";
            if (!engineer.CursorMode && tool >= 0) helpText.text = toolText.text + "  |  Click place  |  Right click inspect";
            if (engineer.CursorMode) helpText.text = "B Logistics  |  M Machines  |  Space Pause  |  0/1/2 Speed  |  V Camera  |  F Flashlight";
            if (Time.unscaledTime < noticeUntil) helpText.text = notice;
            if (roomLightSwitch != null && roomLightSwitch.CanInteract)
                helpText.text = roomLightSwitch.Prompt + "  |  F flashlight works independently";
            var messageRect = (RectTransform)helpText.transform.parent;
            messageRect.anchorMin = new Vector2(.015f, engineer.CursorMode ? .155f : .02f);
            messageRect.anchorMax = new Vector2(.985f, engineer.CursorMode ? .21f : .075f);
            var p = selected;
            inspectorPanel.SetActive(p != null && tool < 0);
            inspectionText.text = p == null ? "ENGINEER CONTROLS\nWASD  Walk    Mouse  Look\nTab  Cursor / mouse look\nB  Belt    M  Assembler\nR  Rotate    Right click  Inspect\nSpace  Pause    1 / 2  Speed\nH  Hint    Delete  Remove" :
                p.kind.ToString().ToUpperInvariant() + "  /  " + (Running ? p.status : "Build mode") + "\n" + (p.kind == PartKind.Assembler ? $"2 plates → 1 gear / 2 seconds\nMaximum: 0.50 gear/s\nInput buffer: {p.plates} / 4 plates\nCycle: {p.timer:0.0} / 2 s" : p.kind == PartKind.Belt ? "Carries plates and gears\nOne item at a time\nRotate the arrow toward the next part" : p.kind == PartKind.Source ? "Free iron plates\n4 plates / second maximum" : "Accepts gears only\nCounts deliveries over the last 6 s") + (p.Cost > 0 ? $"\nCost / refund: ${p.Cost:N0}" : "\nFixed port");
            crosshair.SetActive(Playing && !ResultsVisible && !engineer.CursorMode);
            pauseLabel.text = Paused ? "Resume [Space]" : "Pause [Space]";
            pauseLabel.transform.parent.GetComponent<UnityEngine.UI.Button>().interactable = Running && !ResultsVisible;
            runLabel.text = Running ? "Stop [Enter]" : "Run [Enter]";
            speedLabel.text = "Speed " + Speed.ToString("0.#") + "x";
            foreach (var button in buildButtons) button.interactable = Building;
        }

        static readonly Color Ink = new Color(0.055f, 0.085f, 0.10f, 0.96f);
        static readonly Color Paper = new Color(0.91f, 0.95f, 0.92f);
        RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offsetMin; r.offsetMax = offsetMax; return r;
        }
        RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = Rect(name, parent, min, max, Vector2.zero, Vector2.zero); var img = r.gameObject.AddComponent<UnityEngine.UI.Image>(); img.color = Ink; img.raycastTarget = true; return r;
        }
        TMP_Text Label(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var r = Rect(name, parent, min, max, new Vector2(16, 8), new Vector2(-16, -8));
            var text = r.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = Paper;
            text.text = value; text.raycastTarget = false; text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal; return text;
        }
        TMP_Text Button(Transform parent, string caption, Vector2 min, Vector2 max, Action click)
        {
            var r = Rect(caption, parent, min, max, new Vector2(5, 5), new Vector2(-5, -5));
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>(); img.color = new Color(0.16f, 0.28f, 0.30f);
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = img; b.onClick.AddListener(() => click());
            b.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var t = Label("Caption", r, caption, 16, Vector2.zero, Vector2.one); t.alignment = TextAlignmentOptions.Center; return t;
        }
        void BuildUI()
        {
            var canvas = new GameObject("Rateworks HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var root = Rect("Gameplay HUD", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            gameplayHUD = root;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var contract = Panel("Contract", root.transform, new Vector2(.015f, .88f), new Vector2(.28f, .98f));
            rateText = Label("Rate", contract, "", 20, new Vector2(0, .12f), Vector2.one);
            var track = Panel("Verification track", contract, new Vector2(.04f, .07f), new Vector2(.96f, .10f));
            var bar = Rect("Verified time", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            progress = bar.gameObject.AddComponent<UnityEngine.UI.Image>(); progress.color = new Color(.45f, .85f, .68f); progress.raycastTarget = false;
            var cost = Panel("Cost", root.transform, new Vector2(.70f, .88f), new Vector2(.985f, .98f));
            costText = Label("Cost details", cost, "", 18, Vector2.zero, Vector2.one);
            var inspector = Panel("Inspector", root.transform, new Vector2(.73f, .53f), new Vector2(.985f, .85f));
            inspectorPanel = inspector.gameObject;
            inspectionText = Label("Details", inspector, "", 19, Vector2.zero, Vector2.one);
            var bottom = Panel("Build bar", root.transform, new Vector2(.015f, .015f), new Vector2(.985f, .145f));
            buildPanel = bottom.gameObject;
            toolText = Label("Tool", bottom, "", 18, new Vector2(0, .59f), Vector2.one);
            var logistics = Button(bottom, "Logistics [B]\nBelt $50", new Vector2(0, 0), new Vector2(.18f, .59f), () => ChooseTool(0));
            var machines = Button(bottom, "Machines [M]\nAssembler $1,000", new Vector2(.18f, 0), new Vector2(.38f, .59f), () => ChooseTool(1));
            buildButtons.Add(logistics.transform.parent.GetComponent<UnityEngine.UI.Button>());
            buildButtons.Add(machines.transform.parent.GetComponent<UnityEngine.UI.Button>());
            runLabel = Button(bottom, "Run [Enter]", new Vector2(.38f, 0), new Vector2(.51f, .59f), ToggleRun);
            pauseLabel = Button(bottom, "Pause [Space]", new Vector2(.51f, 0), new Vector2(.65f, .59f), TogglePause);
            speedLabel = Button(bottom, "Speed 1x", new Vector2(.65f, 0), new Vector2(.75f, .59f), ToggleSpeed);
            var example = Button(bottom, "Load example", new Vector2(.75f, 0), new Vector2(.89f, .59f), LoadExample);
            buildButtons.Add(example.transform.parent.GetComponent<UnityEngine.UI.Button>());
            Button(bottom, "Restart", new Vector2(.89f, 0), new Vector2(1, .59f), Restart);
            var help = Panel("Message", root.transform, new Vector2(.015f, .155f), new Vector2(.985f, .21f));
            helpText = Label("Hint", help, "", 17, Vector2.zero, Vector2.one);
            crosshair = Label("Crosshair", root.transform, "+", 24, new Vector2(.48f, .47f), new Vector2(.52f, .53f)).gameObject;
            crosshair.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            var intro = Panel("Briefing", canvas.transform, new Vector2(.25f, .17f), new Vector2(.75f, .83f)); briefing = intro.gameObject;
            Label("Instructions", intro, "<size=36><b>RATEWORKS</b></size>\nTHE FIRST SHIFT\n\nDeliver <b>1 gear/s for 20 seconds.</b>\n\nConnect teal inputs to two assemblers, then to orange outputs. Each assembler makes one gear every two seconds.\n\n<b>Build</b> your layout, then <b>Enter</b> starts the run.\nStop with Enter to edit; Space pauses the run.\nAim for <b>$2,800</b>. Going over budget is allowed.\n\n<b>WASD</b> walk  •  <b>Mouse</b> look  •  <b>Tab</b> menu\n<b>B</b> belt  •  <b>M</b> assembler  •  <b>R</b> rotate\n<b>V</b> camera  •  <b>F</b> flashlight\nThe build menu includes a ready-made example.", 20, new Vector2(.035f, .20f), new Vector2(.965f, .96f));
            Button(intro, "START SHIFT", new Vector2(.32f, .035f), new Vector2(.68f, .15f), Begin);
            var result = Panel("Results", canvas.transform, new Vector2(.24f, .19f), new Vector2(.76f, .81f)); results = result.gameObject;
            resultText = Label("Result", result, "", 24, new Vector2(.03f, .25f), new Vector2(.97f, .94f)); resultText.alignment = TextAlignmentOptions.Center;
            Button(result, "Keep optimizing", new Vector2(.08f, .06f), new Vector2(.55f, .20f), KeepOptimizing);
            Button(result, "Restart", new Vector2(.57f, .06f), new Vector2(.92f, .20f), Restart); results.SetActive(false);
        }
    }
}
