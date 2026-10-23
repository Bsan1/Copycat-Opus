using Opus.World;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Pistol-type hose nozzle: a model generated from code in Blender (tools/blender/models/hose_nozzle.py).
    /// As in the video: a round green head with a black knurled dial, a short barrel, a flow knob at the back facing the camera,
    /// a grip with finger grooves in front, a yellow rear trigger squeezed by the palm behind it; the coupling and hose go down and back.
    /// The origin is the centre of the nozzle, +Z is the direction the water comes out.
    /// </summary>
    public sealed class HoseModel
    {
        public const string File = "hose_nozzle";
        /// <summary>Joint rotation when the trigger is fully squeezed (degrees, X axis): its lower end nears the grip without touching it.</summary>
        public const float TriggerSqueeze = -6f;

        /// <summary>
        /// Measuring points (in the root frame, same as hose_nozzle.py): the back face of the head and the centre of the flow knob,
        /// their radii (outer edge), the top and bottom of the grip.
        /// </summary>
        public static readonly Vector3 HeadCenter = new Vector3(0f, 0f, -0.034f);
        public static readonly Vector3 KnobCenter = new Vector3(0f, 0f, -0.192f);
        public const float HeadRadius = 0.0525f;
        public const float KnobRadius = 0.03f;
        public static readonly Vector3 GripTop = new Vector3(0f, -0.035f, -0.142f);
        public static readonly Vector3 GripBottom = new Vector3(0f, -0.205f, -0.168f);

        public Transform Root { get; private set; }
        /// <summary>Rear trigger (origin at the joint): squeezed by the palm while watering.</summary>
        public Transform Trigger { get; private set; }

        public static HoseModel Build(Transform parent)
        {
            var model = new HoseModel();
            Transform root = new GameObject("Gun").transform;
            root.SetParent(parent, false);
            model.Root = root;
            ModelLibrary.Part(File, "Body", root);
            model.Trigger = ModelLibrary.Part(File, "Trigger", root, keepPosition: true);
            return model;
        }
    }
}
