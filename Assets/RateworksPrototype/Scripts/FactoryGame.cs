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
        public FactorySimulation Simulation { get; private set; }
        public bool Playing { get; private set; }
        public bool ResultsVisible { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
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
        TMP_Text pauseLabel;
        FactoryPart selected, hovered;
        int tool = -1, direction;
        float accumulator;
        bool showedResult;
        string notice = "Start with an assembler and conveyors. Arrows point toward dispatch.";
        public static Vector3 World(int x, int z) => new Vector3(-8 + x * 2, 0, -3 + z * 2);

        void Start()
        {
            Simulation = new FactorySimulation(); Simulation.Transferred += OnTransfer;
            BestCost = PlayerPrefs.GetInt(BestKey, 0);
            factoryRoot = new GameObject("Placed factory parts").transform;
            tokenRoot = new GameObject("Moving products").transform;
            RebuildVisuals(); BuildUI();
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube); ghost.name = "Placement footprint";
            Destroy(ghost.GetComponent<Collider>()); ghost.transform.localScale = new Vector3(1.85f, 0.035f, 1.85f); ghost.SetActive(false);
            engineer.ResetPosition(); UpdateHUD();
        }
        public void Begin()
        {
            Playing = true; briefing.SetActive(false); engineer.SetCursor(false);
        }
        public void ChooseTool(int value) { tool = value; selected = null; engineer.SetCursor(false); }
        public void CancelTool() { tool = -1; }
        public void TogglePause() { Paused = !Paused; }
        public void ToggleSpeed() { Speed = Speed == 1 ? 2 : 1; }
        public void KeepOptimizing()
        {
            ResultsVisible = false; results.SetActive(false); engineer.SetCursor(false);
        }
        public void Restart()
        {
            Simulation.Transferred -= OnTransfer; Simulation = new FactorySimulation(); Simulation.Transferred += OnTransfer;
            Paused = false; Speed = 1; accumulator = 0; showedResult = false; ResultsVisible = false;
            selected = null; tool = -1; results.SetActive(false); ClearTokens(); RebuildVisuals(); engineer.ResetPosition(); engineer.SetCursor(false);
            notice = "Fresh layout. Two assemblers can meet the contract.";
        }
        public void LoadExample()
        {
            Simulation.FillExample(); selected = null; ClearTokens(); RebuildVisuals(); showedResult = false;
            ResultsVisible = false; results.SetActive(false); Paused = false; accumulator = 0; tool = -1;
            notice = "Example loaded: 2 assemblers, 12 belts, $2,600. Try rotating a belt to see a blockage.";
            engineer.SetCursor(false);
        }
        public bool Place(PartKind kind, int x, int z, int facing)
        {
            if (kind != PartKind.Belt && kind != PartKind.Assembler) return false;
            var p = Simulation.Add(kind, x, z, facing);
            if (p == null) return false;
            AddVisual(p); LayoutChanged(); return true;
        }
        void LayoutChanged()
        {
            Simulation.ResetFlow(); accumulator = 0; ClearTokens(); showedResult = false;
            notice = "Layout changed. Flow and verification restarted; full cost refund on removal.";
        }
        void Update()
        {
            if (Simulation == null) return;
            if (Playing && !ResultsVisible)
            {
                var k = Keyboard.current; var m = Mouse.current;
                if (k != null)
                {
                    if (k.bKey.wasPressedThisFrame) ChooseTool(0);
                    if (k.mKey.wasPressedThisFrame) ChooseTool(1);
                    if (k.spaceKey.wasPressedThisFrame) TogglePause();
                    if (k.digit1Key.wasPressedThisFrame) Speed = 1;
                    if (k.digit2Key.wasPressedThisFrame) Speed = 2;
                    if (k.hKey.wasPressedThisFrame) notice = "HINT: build two straight rows from IN to OUT. Each row needs one assembler and six belts, all pointing right (+X).";
                    if (k.rKey.wasPressedThisFrame)
                    {
                        if (tool >= 0) direction = (direction + 1) % 4;
                        else if (selected != null && selected.Cost > 0)
                        { selected.direction = (selected.direction + 1) % 4; visuals[selected].transform.rotation = Quaternion.Euler(0, selected.direction * 90, 0); LayoutChanged(); }
                    }
                    if ((k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) && selected != null && selected.Cost > 0)
                    {
                        Simulation.Remove(selected.x, selected.z); Destroy(visuals[selected]); visuals.Remove(selected); selected = null; LayoutChanged();
                    }
                }
                Aim();
                bool overUI = engineer.CursorMode && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (m != null && !overUI)
                {
                    if (m.rightButton.wasPressedThisFrame) CancelTool();
                    if (m.leftButton.wasPressedThisFrame && hasCell)
                    {
                        if (tool >= 0)
                        {
                            if (hovered != null) notice = "That cell is occupied. Right click to inspect or remove it.";
                            else if (Vector2.Distance(new Vector2(engineer.transform.position.x, engineer.transform.position.z), new Vector2(World(aimX, aimZ).x, World(aimX, aimZ).z)) < 0.9f)
                                notice = "Step off this cell before placing a machine.";
                            else Place(tool == 0 ? PartKind.Belt : PartKind.Assembler, aimX, aimZ, direction);
                        }
                        else selected = hovered;
                    }
                }
                if (!Paused)
                {
                    accumulator += Mathf.Min(Time.deltaTime, 0.2f) * Speed;
                    while (accumulator >= FactorySimulation.Step) { Simulation.Tick(); accumulator -= FactorySimulation.Step; }
                    AnimateProducts(Time.deltaTime * Speed);
                }
                if (Simulation.Passed && !showedResult) ShowResults();
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
            if (Physics.Raycast(ray, out var hit, 22f))
            {
                var marker = hit.collider.GetComponentInParent<FactoryPartMarker>();
                if (marker != null) { point = World(marker.x, marker.z); hitCell = true; }
                else if (hit.collider.gameObject.name == "Factory floor") { point = hit.point; hitCell = true; }
            }
            aimX = Mathf.RoundToInt((point.x + 8) / 2); aimZ = Mathf.RoundToInt((point.z + 3) / 2);
            hasCell = hitCell && FactorySimulation.Inside(aimX, aimZ);
            hovered = hasCell ? Simulation.At(aimX, aimZ) : null;
            ghost.SetActive(hasCell && tool >= 0);
            if (hasCell && tool >= 0)
            {
                ghost.transform.position = World(aimX, aimZ) + Vector3.up * 0.035f;
                ghost.GetComponent<Renderer>().sharedMaterial = hovered == null ? previewValid : previewInvalid;
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
            foreach (var obj in visuals.Values) if (obj != null) Destroy(obj);
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
            results.SetActive(true); engineer.SetCursor(true);
        }
        void UpdateHUD()
        {
            if (rateText == null) return;
            gameplayHUD.SetActive(Playing && !ResultsVisible);
            buildPanel.SetActive(engineer.CursorMode);
            rateText.text = $"<b>{Simulation.Rate:0.00} / 1.00 gear/s</b>\nHold {Simulation.Hold:0.0} / 20 s  |  {(Paused ? "PAUSED" : Speed + "x")}";
            costText.text = $"<b>${Simulation.Cost:N0}</b> / ${FactorySimulation.Budget:N0} budget" + (Simulation.Cost <= FactorySimulation.Budget ? "" : "\n<color=#F1BB70>Over budget — passing still allowed</color>");
            progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Simulation.Hold / FactorySimulation.HoldSeconds), 1);
            string facing = new[] { "RIGHT (+X)", "BACK (-Z)", "LEFT (-X)", "FORWARD (+Z)" }[direction];
            toolText.text = tool < 0 ? "INSPECT  /  Click a part, R to rotate, Delete to refund" : (tool == 0 ? "BELT • $50" : "ASSEMBLER • $1,000") + "  /  " + facing + "  /  R to rotate";
            helpText.text = engineer.CursorMode ? notice : "WASD  Move   |   B  Belt   M  Assembler   R  Rotate   |   Tab  Build menu   H  Hint";
            if (!engineer.CursorMode && tool >= 0) helpText.text = toolText.text + "   |   Click to place   Right click to cancel   Tab for menu";
            var messageRect = (RectTransform)helpText.transform.parent;
            messageRect.anchorMin = new Vector2(.015f, engineer.CursorMode ? .155f : .02f);
            messageRect.anchorMax = new Vector2(.985f, engineer.CursorMode ? .21f : .075f);
            if (!engineer.CursorMode && Keyboard.current != null && Keyboard.current.hKey.isPressed) helpText.text = notice;
            var p = selected;
            inspectorPanel.SetActive(p != null && tool < 0);
            inspectionText.text = p == null ? "ENGINEER CONTROLS\nWASD  Walk    Mouse  Look\nTab  Cursor / mouse look\nB  Belt    M  Assembler\nR  Rotate    Right click  Inspect\nSpace  Pause    1 / 2  Speed\nH  Hint    Delete  Remove" :
                p.kind.ToString().ToUpperInvariant() + "  /  " + p.status + "\n" + (p.kind == PartKind.Assembler ? $"2 plates → 1 gear / 2 seconds\nMaximum: 0.50 gear/s\nInput buffer: {p.plates} / 4 plates\nCycle: {p.timer:0.0} / 2 s" : p.kind == PartKind.Belt ? "Carries plates and gears\nOne item at a time\nRotate the arrow toward the next part" : p.kind == PartKind.Source ? "Free iron plates\n4 plates / second maximum" : "Accepts gears only\nCounts deliveries over the last 6 s") + (p.Cost > 0 ? $"\nCost / refund: ${p.Cost:N0}" : "\nFixed port");
            crosshair.SetActive(Playing && !ResultsVisible && !engineer.CursorMode);
            pauseLabel.text = Paused ? "Resume [Space]" : "Pause [Space]";
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
            var t = Label("Caption", r, caption, 17, Vector2.zero, Vector2.one); t.alignment = TextAlignmentOptions.Center; return t;
        }
        void BuildUI()
        {
            var canvas = new GameObject("Rateworks HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0f;
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
            Button(bottom, "Belt [B]  $50", new Vector2(0, 0), new Vector2(.17f, .59f), () => ChooseTool(0));
            Button(bottom, "Assembler [M]  $1,000", new Vector2(.17f, 0), new Vector2(.39f, .59f), () => ChooseTool(1));
            pauseLabel = Button(bottom, "Pause [Space]", new Vector2(.39f, 0), new Vector2(.55f, .59f), TogglePause);
            Button(bottom, "1x / 2x", new Vector2(.55f, 0), new Vector2(.65f, .59f), ToggleSpeed);
            Button(bottom, "Load example", new Vector2(.65f, 0), new Vector2(.84f, .59f), LoadExample);
            Button(bottom, "Restart", new Vector2(.84f, 0), Vector2.one * new Vector2(1, .59f), Restart);
            var help = Panel("Message", root.transform, new Vector2(.015f, .155f), new Vector2(.985f, .21f));
            helpText = Label("Hint", help, "", 17, Vector2.zero, Vector2.one);
            crosshair = Label("Crosshair", root.transform, "+", 24, new Vector2(.48f, .47f), new Vector2(.52f, .53f)).gameObject;
            crosshair.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            var intro = Panel("Briefing", canvas.transform, new Vector2(.25f, .20f), new Vector2(.75f, .80f)); briefing = intro.gameObject;
            Label("Instructions", intro, "<size=36><b>RATEWORKS</b></size>\nTHE FIRST SHIFT\n\nDeliver <b>1 gear/s for 20 seconds.</b>\n\nConnect teal inputs to two assemblers, then to orange outputs. Each assembler makes one gear every two seconds.\n\nAim for <b>$2,800</b>. Going over budget is allowed.\n\n<b>WASD</b> walk  •  <b>Mouse</b> look\n<b>B</b> belt  •  <b>M</b> assembler  •  <b>R</b> rotate\n<b>Tab</b> opens the build menu and example layout.", 21, new Vector2(.035f, .20f), new Vector2(.965f, .96f));
            Button(intro, "START SHIFT", new Vector2(.32f, .035f), new Vector2(.68f, .15f), Begin);
            var result = Panel("Results", canvas.transform, new Vector2(.24f, .19f), new Vector2(.76f, .81f)); results = result.gameObject;
            resultText = Label("Result", result, "", 24, new Vector2(.03f, .25f), new Vector2(.97f, .94f)); resultText.alignment = TextAlignmentOptions.Center;
            Button(result, "Keep optimizing", new Vector2(.08f, .06f), new Vector2(.55f, .20f), KeepOptimizing);
            Button(result, "Restart", new Vector2(.57f, .06f), new Vector2(.92f, .20f), Restart); results.SetActive(false);
        }
    }
}
