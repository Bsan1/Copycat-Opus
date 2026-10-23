using System.Collections.Generic;
using Opus.World;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Push mower: parts generated from code in Blender (tools/blender/models/mower.py: Housing, Motor,
    /// Handle; wheel.py: the wheels). The parts sit in the file at their places on the mower: the wheels on the axle pins of the housing,
    /// the handle where the hands hold it, the control cable from the lever to the engine's brake tab. The connection points are
    /// in Assets/Opus/ModelSpecs/mower.json; ModelImportTests compares them with the in-game values.
    /// Sizes in metres; the origin is below the housing centre (ground), +Z is the front of the mower.
    /// </summary>
    public sealed class MowerModel
    {
        public const string File = "mower";
        public const float FrontWheelRadius = 0.095f;
        public const float RearWheelRadius = 0.115f;
        /// <summary>Axle of the right wheels (left: X mirrored); the same place as the axle pins in Blender.</summary>
        public static readonly Vector3 FrontAxle = new Vector3(0.276f, FrontWheelRadius, 0.22f);
        public static readonly Vector3 RearAxle = new Vector3(0.316f, RearWheelRadius, -0.23f);

        readonly List<(Transform axle, float baseX)> wheels = new List<(Transform, float)>();
        Transform deck;
        Transform handle;

        /// <summary>Root rotated for body vibration/tilt (everything except the wheels).</summary>
        public Transform Chassis { get; private set; }
        public readonly List<Transform> FrontAxles = new List<Transform>();
        public readonly List<Transform> RearAxles = new List<Transform>();
        /// <summary>Number of parts (renderers) built.</summary>
        public int PartCount { get; private set; }
        /// <summary>Total triangles drawn.</summary>
        public int TriangleCount { get; private set; }

        /// <summary>
        /// Wide deck upgrade: the housing grows sideways; the handle and wheels move outwards by the same ratio so they
        /// stay attached to the housing's plates (the axle pins and handle bolts stay in place).
        /// </summary>
        public void SetDeckScale(float scale)
        {
            deck.localScale = new Vector3(scale, 1f, 1f);
            handle.localScale = new Vector3(scale, 1f, 1f);
            foreach ((Transform axle, float baseX) in wheels)
                axle.localPosition = new Vector3(baseX * scale, axle.localPosition.y, axle.localPosition.z);
        }

        public static MowerModel Build(Transform parent)
        {
            var model = new MowerModel();
            Transform chassis = new GameObject("Body").transform;
            chassis.SetParent(parent, false);
            model.Chassis = chassis;
            model.deck = ModelLibrary.Part(File, "Housing", chassis, "Deck");
            ModelLibrary.Part(File, "Motor", chassis);
            model.handle = ModelLibrary.Part(File, "Handle", chassis);

            foreach (float side in new[] { -1f, 1f })
            {
                Transform front = Wheel(parent, "FrontWheel", Side(FrontAxle, side), side);
                Transform rear = Wheel(parent, "RearWheel", Side(RearAxle, side), side);
                model.FrontAxles.Add(front);
                model.RearAxles.Add(rear);
                model.wheels.Add((front, front.localPosition.x));
                model.wheels.Add((rear, rear.localPosition.x));
            }

            foreach (MeshFilter filter in parent.GetComponentsInChildren<MeshFilter>())
            {
                model.PartCount++;
                model.TriangleCount += filter.sharedMesh.triangles.Length / 3;
            }
            return model;
        }

        static Vector3 Side(Vector3 right, float side) => new Vector3(right.x * side, right.y, right.z);

        /// <summary>
        /// Axle + the wheel generated in Blender (wheel.py: one-piece treaded tire, pocketed rim, hub cap). The model's outer
        /// face points to +X; on the left wheel it is turned 180°. The radii are the same as Front/RearWheelRadius.
        /// </summary>
        static Transform Wheel(Transform parent, string name, Vector3 center, float side)
        {
            Transform axle = new GameObject(name).transform;
            axle.SetParent(parent, false);
            axle.localPosition = center;
            Transform wheel = ModelLibrary.Part("wheel", name, axle, "WheelModel");
            if (side < 0f)
                wheel.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return axle;
        }
    }
}
