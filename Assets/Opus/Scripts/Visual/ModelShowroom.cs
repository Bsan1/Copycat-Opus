using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Core;
using Opus.Tools;
using Opus.World;
using UnityEngine;

namespace Opus.Visual
{
    /// <summary>
    /// Model showroom (Assets/Opus/Scenes/ModelShowroom.unity): lines up every model in the game side by side the way the game builds them
    /// (materials, sun, sky, ambient colour, colour grading). It is inspected in the editor's Scene view without pressing Play:
    /// double-clicking a model in the Hierarchy frames it. Blender models in the back row,
    /// hand-built garden props in the front row. What is built is not saved into the scene (its materials are generated from code); it is
    /// rebuilt when the scene opens and when the code compiles.
    /// </summary>
    [ExecuteAlways]
    public sealed class ModelShowroom : MonoBehaviour
    {
        public const string BuiltName = "Showroom (not saved, built when the scene opens)";
        const float Gap = 6f;
        const float ModelRowZ = 0f;
        const float PropRowZ = -20f;

        /// <summary>A built model: its name in the Hierarchy, root object and world bounding box.</summary>
        public readonly struct Item
        {
            public readonly string Name;
            public readonly Transform Root;
            public readonly Bounds Bounds;

            public Item(string name, Transform root, Bounds bounds)
            {
                Name = name;
                Root = root;
                Bounds = bounds;
            }
        }

        readonly List<Item> items = new List<Item>();
        Transform built;

        public IReadOnlyList<Item> Items => items;

        void OnEnable() => Rebuild();

        void OnDisable() => Clear();

        /// <summary>Rebuilds the showroom from scratch (light, colour grading, ground, models).</summary>
        public void Rebuild()
        {
            Clear();
            Atmosphere.SetShaderGlobals();
            built = new GameObject(BuiltName).transform;
            built.SetParent(transform, false);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(built, true);
            Atmosphere.ConfigureSun(sun);
            PostFx.Create(built);

            float modelsEnd = Row(ModelRowZ, new (string, Func<Transform, Transform>)[]
            {
                ("Mower", parent => { MowerModel.Build(parent); return parent; }),
                ("Hose nozzle", parent => { HoseModel.Build(Child(parent, new Vector3(0f, 0.55f, 0f))); return parent; }),
                ("Trimmer", parent => { TrimmerModel.Build(Child(parent, new Vector3(0f, 0.17f, 0f))); return parent; }),
                ("Shed", parent => YardProps.BuildShed(parent, Vector3.zero)),
                ("House", parent => YardProps.BuildHouse(parent, Vector3.zero)),
                ("Tree and swing", parent => YardProps.BuildTree(parent, Vector3.zero)),
            });

            YardDefinition home = HomeYard.Create();
            var props = new List<(string, Func<Transform, Transform>)>
            {
                ("Fence", parent => YardProps.BuildFence(parent, new[] { (new Vector3(-2.4f, 0f, 0f), new Vector3(2.4f, 0f, 0f)) })),
                ("Flowerbed", parent => Prop(parent, new PropPlacement(PropKind.Flowerbed, Vector3.zero, HomeYard.FlowerbedSize))),
                ("Birdbath", parent => Prop(parent, new PropPlacement(PropKind.Birdbath, Vector3.zero))),
                ("Compost", parent => Prop(parent, new PropPlacement(PropKind.Compost, Vector3.zero))),
                ("Rain barrel", parent => Prop(parent, new PropPlacement(PropKind.RainBarrel, Vector3.zero))),
                ("Wheelbarrow", parent => Prop(parent, new PropPlacement(PropKind.Wheelbarrow, Vector3.zero))),
                ("Apple crate", parent => Prop(parent, new PropPlacement(PropKind.AppleCrate, Vector3.zero))),
                ("Bean trellis", parent => Prop(parent, new PropPlacement(PropKind.BeanTrellis, Vector3.zero, new Vector2(8f, 0f)))),
            };
            // The vegetable bed's four kinds (variant % 4); apple trees only vary by the random seed: two are enough
            string[] crops = { "cabbage", "tomato", "lettuce", "carrot" };
            for (int i = 0; i < crops.Length; i++)
            {
                int variant = i;
                props.Add(($"Vegetable bed ({crops[i]})", parent => Prop(parent, new PropPlacement(PropKind.RaisedBed, Vector3.zero, HomeYard.RaisedBedSize, variant: variant))));
            }
            for (int i = 0; i < 2; i++)
            {
                int variant = i;
                props.Add(($"Apple tree {i + 1}", parent => Prop(parent, new PropPlacement(PropKind.FruitTree, Vector3.zero, variant: variant))));
            }
            if (home.Gates.Length > 0)
                props.Add(("Garden gate", parent => { Gate.Create(parent, home.Gates[0], Vector2.zero); return parent; }));
            float propsEnd = Row(PropRowZ, props);

            // Ground: pale grass green (so the models read on a green ground as in the game), clear shadows
            float width = Mathf.Max(modelsEnd, propsEnd) + 2f * Gap;
            PrimitiveKit.Box(built, "Ground", new Vector3(width * 0.5f - Gap, -0.05f, (ModelRowZ + PropRowZ) * 0.5f),
                new Vector3(width, 0.1f, 60f), Palette.Hex("#A9AF8C").As(SurfaceKind.Ground), collider: false);

            foreach (Transform t in built.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }

        void Clear()
        {
            items.Clear();
            if (built == null)
            {
                Transform old = transform.Find(BuiltName);
                if (old != null) built = old;
            }
            if (built != null)
                DestroyImmediate(built.gameObject);
            built = null;
        }

        /// <summary>Lays out a row from left to right: each model by its own bounding box, with gaps between them. Returns the end of the row (x).</summary>
        float Row(float z, IEnumerable<(string name, Func<Transform, Transform> build)> entries)
        {
            float x = 0f;
            foreach ((string name, Func<Transform, Transform> build) in entries)
            {
                var group = new GameObject($"{items.Count + 1:00} {name}").transform;
                group.SetParent(built, false);
                build(group);
                Bounds bounds = BoundsOf(group);
                group.position += new Vector3(x - bounds.min.x, 0f, z - bounds.center.z);
                bounds = BoundsOf(group);
                items.Add(new Item(name, group, bounds));
                x = bounds.max.x + Gap;
            }
            return x;
        }

        static Transform Child(Transform parent, Vector3 localPosition)
        {
            var child = new GameObject("Holder").transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static Transform Prop(Transform parent, PropPlacement placement)
        {
            YardBuilder.BuildProp(parent, placement);
            return parent;
        }

        public static Bounds BoundsOf(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

#if UNITY_EDITOR
        static GUIStyle labelStyle;

        void OnDrawGizmos()
        {
            labelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold };
            foreach (Item item in items)
            {
                if (item.Root == null) continue;
                Vector3 top = item.Bounds.center + Vector3.up * (item.Bounds.extents.y + 0.25f);
                UnityEditor.Handles.Label(top, item.Root.name, labelStyle);
            }
        }
#endif
    }
}
