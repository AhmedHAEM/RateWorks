using System;
using System.Collections.Generic;
using UnityEngine;

namespace RateworksPrototype
{
    public enum PartKind { Belt, Assembler, Source, Dispatch }
    public enum Product { None, Plate, Gear }

    // Small deterministic simulation: one cell = one part; no item physics.
    [Serializable]
    public class FactoryPart
    {
        public int x, z, direction;
        public PartKind kind;
        public Product output;
        public int plates;
        public float timer, age;
        public bool processing;
        public string status = "Idle";
        public int Cost => kind == PartKind.Belt ? 50 : kind == PartKind.Assembler ? 1000 : 0;
    }

    public sealed class FactorySimulation
    {
        public const int Width = 9, Depth = 5;
        // Six seconds contains three complete 2-second cycles, avoiding a 0.8/1.2
        // oscillation when two assemblers deliver in sync into a five-second window.
        public const float Step = 0.05f, Target = 1f, HoldSeconds = 20f, Window = 6f;
        public const int Budget = 2800;
        public readonly FactoryPart[,] cells = new FactoryPart[Width, Depth];
        public readonly List<FactoryPart> parts = new List<FactoryPart>();
        readonly Queue<float> deliveries = new Queue<float>();
        int ticks;
        public float Time { get; private set; }
        public float Rate { get; private set; }
        public float Hold { get; private set; }
        public bool Passed { get; private set; }
        public int Delivered { get; private set; }
        public int Cost { get { int cost = 0; foreach (var p in parts) cost += p.Cost; return cost; } }
        public static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.down, Vector2Int.left, Vector2Int.up };
        public event Action<FactoryPart, FactoryPart, Product> Transferred;

        public FactorySimulation()
        {
            Add(PartKind.Source, 0, 1, 0);
            Add(PartKind.Source, 0, 3, 0);
            Add(PartKind.Dispatch, 8, 1, 0);
            Add(PartKind.Dispatch, 8, 3, 0);
        }
        public static bool Inside(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Depth;
        public FactoryPart At(int x, int z) => Inside(x, z) ? cells[x, z] : null;
        public FactoryPart Add(PartKind kind, int x, int z, int direction)
        {
            if (!Inside(x, z) || cells[x, z] != null) return null;
            var p = new FactoryPart { kind = kind, x = x, z = z, direction = direction % 4 };
            cells[x, z] = p; parts.Add(p); return p;
        }
        public bool Remove(int x, int z)
        {
            var p = At(x, z);
            if (p == null || p.Cost == 0) return false;
            cells[x, z] = null; parts.Remove(p); ResetFlow(); return true;
        }
        public void ResetFlow()
        {
            ticks = 0; Time = Rate = Hold = 0; Passed = false; Delivered = 0; deliveries.Clear();
            foreach (var p in parts)
            { p.output = Product.None; p.plates = 0; p.timer = p.age = 0; p.processing = false; p.status = "Idle"; }
        }
        public void Tick()
        {
            Time = ++ticks * Step;
            // Only outputs ready at the start of the tick may transfer this tick.
            var ready = new List<FactoryPart>();
            foreach (var p in parts)
            {
                p.age += Step;
                if (p.kind == PartKind.Source && p.output == Product.None)
                {
                    p.timer += Step;
                    if (p.timer >= 0.25f) { p.output = Product.Plate; p.timer = 0; p.age = 0; }
                }
                if (p.output != Product.None && (p.kind != PartKind.Belt || p.age >= 0.333f)) ready.Add(p);
            }
            foreach (var p in ready)
            {
                var d = Directions[p.direction]; var next = At(p.x + d.x, p.z + d.y);
                if (!Accepts(next, p.output)) { p.status = "Blocked"; continue; }
                var item = p.output;
                if (next.kind == PartKind.Dispatch) { deliveries.Enqueue(Time); Delivered++; }
                else if (next.kind == PartKind.Assembler) next.plates++;
                else { next.output = item; next.age = 0; }
                p.output = Product.None; p.status = "Working";
                Transferred?.Invoke(p, next, item);
            }
            foreach (var p in parts)
            {
                if (p.kind == PartKind.Assembler)
                {
                    if (!p.processing && p.plates >= 2 && p.output == Product.None)
                    { p.plates -= 2; p.processing = true; p.timer = 0; }
                    if (p.processing)
                    {
                        p.timer += Step; p.status = "Working";
                        if (p.timer + 0.0001f >= 2f)
                        { p.processing = false; p.output = Product.Gear; p.age = 0; }
                    }
                    else p.status = p.output != Product.None ? "Blocked" : "Starved";
                }
                else if (p.kind == PartKind.Dispatch) p.status = "Receiving gears";
                else if (p.output == Product.None) p.status = p.kind == PartKind.Source ? "Supplying plates" : "Starved";
            }
            while (deliveries.Count > 0 && Time - deliveries.Peek() >= Window - 0.0001f) deliveries.Dequeue();
            Rate = deliveries.Count / Window;
            Hold = Rate + 0.001f >= Target ? Mathf.Min(HoldSeconds, Hold + Step) : 0;
            if (Hold + 0.001f >= HoldSeconds) Passed = true;
        }
        static bool Accepts(FactoryPart p, Product item)
        {
            if (p == null) return false;
            if (p.kind == PartKind.Dispatch) return item == Product.Gear;
            if (p.kind == PartKind.Assembler) return item == Product.Plate && p.plates < 4;
            return p.kind == PartKind.Belt && p.output == Product.None;
        }
        public void FillExample()
        {
            parts.RemoveAll(p => p.Cost > 0);
            for (int x = 0; x < Width; x++) for (int z = 0; z < Depth; z++)
                if (cells[x, z] != null && cells[x, z].Cost > 0) cells[x, z] = null;
            foreach (int z in new[] { 1, 3 })
                for (int x = 1; x < 8; x++) Add(x == 4 ? PartKind.Assembler : PartKind.Belt, x, z, 0);
            ResetFlow();
        }
    }
}
