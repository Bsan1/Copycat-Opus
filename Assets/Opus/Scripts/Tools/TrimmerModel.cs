using Opus.World;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Battery string trimmer: parts generated from code in Blender (tools/blender/models/trimmer.py, textured pipeline). As in the
    /// video: a red body bottom right, a framed black battery behind it, light grey underneath; a black shaft; a thick D handle standing
    /// across the shaft; in front a flat black head puck with a ringed knob on top. Root frame: the origin is the front end of the shaft, +Z
    /// runs along the shaft towards the head; the shaft extends back from the origin (-Z). The head is also aligned to world up.
    /// </summary>
    public sealed class TrimmerModel
    {
        public const string File = "trimmer";

        /// <summary>Shaft length at build time; in game it is stretched to the hands with <see cref="SetLength"/>.</summary>
        public const float ShaftLength = 1.25f;

        /// <summary>
        /// The D handle stands just in front of the body (as in the video's 0:07 frame, where it nearly touches the body's front):
        /// this far ahead of the body's front end along the shaft, whatever the shaft length.
        /// </summary>
        public const float HandleAhead = 0.11f;

        // Feature points (Blender spec anchors, each in its part's frame; ModelImportTests compares them with the spec). The framing fit
        // (u16_trimmer_tuning) projects them on screen and matches the video's 0:07 frame.
        /// <summary>Front end of the body, where the shaft enters (Motor).</summary>
        public static readonly Vector3 BodyFront = new Vector3(0f, 0f, 0f);
        /// <summary>Centre of the battery's back face (Motor); the face is <see cref="BatteryHalfWidth"/> wide on each side.</summary>
        public static readonly Vector3 BatteryBack = new Vector3(0f, 0.016f, -0.432f);
        public const float BatteryHalfWidth = 0.077f;
        /// <summary>Half the height of the battery block's back face (top 0.088 m, bottom -0.056 m in the Motor frame).</summary>
        public const float BatteryHalfHeight = 0.072f;
        /// <summary>Top of the D handle's arch (DHandle).</summary>
        public static readonly Vector3 HandleTop = new Vector3(0f, 0.155f, 0f);
        /// <summary>Middle of the head's puck (Head).</summary>
        public static readonly Vector3 HeadCenter = new Vector3(0f, -0.088f, 0f);

        Transform shaft;
        Transform motor;
        Transform handle;

        public Transform Root { get; private set; }
        public Transform Motor => motor;
        public Transform Handle => handle;
        /// <summary>Head: a sub-part aligned to world up.</summary>
        public Transform Head { get; private set; }
        /// <summary>Spinning line (only visible while running); its origin is the spool's axis.</summary>
        public Transform Spinner { get; private set; }
        /// <summary>Short line tips visible while idle.</summary>
        public Transform IdleLines { get; private set; }

        public static TrimmerModel Build(Transform parent)
        {
            var model = new TrimmerModel();
            Transform root = new GameObject("Body").transform;
            root.SetParent(parent, false);
            model.Root = root;

            // The shaft model is unit length (0 -> -1 m); SetLength stretches it with the Z scale
            model.shaft = ModelLibrary.Part(File, "Shaft", root);
            model.handle = ModelLibrary.Part(File, "DHandle", root);
            model.motor = ModelLibrary.Part(File, "Motor", root, "MotorGroup");
            model.SetLength(ShaftLength);

            Transform head = ModelLibrary.Part(File, "Head", root);
            model.Head = head;
            model.Spinner = ModelLibrary.Part(File, "Line", head, keepPosition: true);
            model.IdleLines = ModelLibrary.Part(File, "LineTips", head, keepPosition: true);
            model.Spinner.gameObject.SetActive(false);
            return model;
        }

        /// <summary>Shaft length from the head to the front end of the motor body: adjusted every frame so the body always stays in the hands.</summary>
        public void SetLength(float length)
        {
            length = Mathf.Max(0.6f, length);
            shaft.localScale = new Vector3(1f, 1f, length);
            motor.localPosition = new Vector3(0f, 0f, -length);
            handle.localPosition = new Vector3(0f, 0f, -length + HandleAhead);
        }
    }
}
