using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using Opus.World;
using UnityEngine;

namespace Opus.Core
{
    public delegate IEnumerator ScenarioScript(ScenarioContext s);

    /// <summary>
    /// Automated scenarios; run with tools/Shots.ps1 -Scenario &lt;name&gt;.
    /// Pitch: a positive value looks down. Positions are the player's foot point.
    /// </summary>
    public static class Scenarios
    {
        static readonly Dictionary<string, ScenarioScript> Scripts = new Dictionary<string, ScenarioScript>
        {
            ["u0_overview"] = U0Overview,
            ["u1_mower"] = U1Mower,
            ["u1_framing"] = U1Framing,
            ["u2_grass"] = U2Grass,
            ["u3_mow"] = U3Mow,
            ["u4_trim"] = U4Trim,
            ["u5_hose"] = U5Hose,
            ["u6_hud"] = U6Hud,
            ["u7_scene"] = U7Scene,
            ["u8_areas"] = U8Areas,
            ["u9_perf"] = U9Perf,
            ["u9_audio"] = U9Audio,
            ["u10_progress"] = U10Progress,
            ["u10_pattern"] = U10Pattern,
            ["u10_tools"] = U10Tools,
            ["u11_props"] = U11Props,
            ["u14_areas"] = U14Areas,
            ["u15_jobs"] = U15Jobs,
            ["u16_models"] = U16Models,
            ["u16_hose_tuning"] = U16HoseFit,
            ["u16_trimmer_tuning"] = U16TrimmerFit,
            ["u16_showroom"] = U16Showcase,
        };

        /// <summary>
        /// In-game showroom of the Blender models: each model on mown grass, in the game's light, from six sides and from above
        /// (no interface), then from the player's eye. The contact sheets sent to the user are made from these.
        /// </summary>
        static IEnumerator U16Showcase(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            LawnState lawn = game.Lawn;
            for (float x = -4f; x <= 4f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, -8.4f), new Vector2(x, -3.8f), 0.15f, Vector2.up, true);
            s.Equip(Tool.HandSlot);
            yield return s.Frames(3);
            Camera ui = s.Player.UiCamera;

            var spot = new Vector3(0f, 0f, -6.1f);
            string[] sides = { "front", "front_right", "right", "back", "left", "front_left" };

            IEnumerator Orbit(string model, Vector3 center, float distance, float elevation, float topDistance, float frontYaw = 0f)
            {
                ui.enabled = false;
                for (int i = 0; i < sides.Length; i++)
                {
                    // 0 = from the front (looks from the model's +Z; for a building facing another way in the garden it turns by frontYaw),
                    // 60 degrees each clockwise
                    float a = (frontYaw + i * 60f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Vector3 eye = center + dir * distance * Mathf.Cos(elevation * Mathf.Deg2Rad) + Vector3.up * distance * Mathf.Sin(elevation * Mathf.Deg2Rad);
                    yield return s.FreeCamera(eye, center, 40f, 6);
                    yield return s.Shot($"v_{model}_{sides[i]}", 2);
                }
                yield return s.FreeCamera(center + new Vector3(0.05f, topDistance, -0.35f * topDistance), center, 40f, 6);
                yield return s.Shot($"v_{model}_top", 2);
                s.EndFreeCamera();
                ui.enabled = true;
            }

            // Mower (front +Z)
            Transform holder = new GameObject("Showroom").transform;
            holder.position = spot;
            MowerModel.Build(holder);
            yield return Orbit("mower", spot + Vector3.up * 0.35f, 1.9f, 28f, 2.3f);
            ui.enabled = false;
            yield return s.FreeCamera(spot + new Vector3(0.45f, 0.75f, -0.75f), spot + new Vector3(0f, 0.22f, -0.05f), 40f, 6);
            yield return s.Shot("v_mower_engine_close", 2);
            yield return s.FreeCamera(spot + new Vector3(0.75f, 0.32f, -0.55f), spot + new Vector3(0.28f, 0.16f, -0.24f), 40f, 6);
            yield return s.Shot("v_mower_handle_base_close", 2);
            s.EndFreeCamera();
            ui.enabled = true;
            Object.Destroy(holder.gameObject);

            // Hose nozzle (mouth +Z; grip and hose downwards)
            holder = new GameObject("Showroom").transform;
            holder.position = spot + Vector3.up * 0.55f;
            HoseModel.Build(holder);
            yield return Orbit("hose", spot + new Vector3(0f, 0.47f, -0.06f), 0.5f, 18f, 0.6f);
            Object.Destroy(holder.gameObject);

            // Trimmer (head +Z; the shaft extends backwards)
            holder = new GameObject("Showroom").transform;
            holder.position = spot + new Vector3(0f, 0.17f, 0.85f);
            TrimmerModel.Build(holder);
            yield return Orbit("trimmer", spot + new Vector3(0f, 0.15f, 0.05f), 2.6f, 25f, 3.0f);
            ui.enabled = false;
            Vector3 motor = holder.TransformPoint(new Vector3(0f, 0f, -TrimmerModel.ShaftLength - 0.2f));
            yield return s.FreeCamera(motor + new Vector3(0.45f, 0.35f, -0.45f), motor, 40f, 6);
            yield return s.Shot("v_trimmer_body_close", 2);
            yield return s.FreeCamera(motor + new Vector3(-0.5f, 0.25f, 0.35f), motor, 40f, 6);
            yield return s.Shot("v_trimmer_body_front_close", 2);
            Vector3 head = holder.position + new Vector3(0f, -0.08f, 0f);
            yield return s.FreeCamera(head + new Vector3(0.35f, 0.4f, -0.5f), head, 40f, 6);
            yield return s.Shot("v_trimmer_head_close", 2);
            s.EndFreeCamera();
            ui.enabled = true;
            Object.Destroy(holder.gameObject);

            // Shed (in its place in the garden; its door faces -Z, towards the garden)
            Vector3 shed = HomeYard.ShedPosition;
            yield return Orbit("shed", shed + new Vector3(0f, 1.5f, 0f), 6.5f, 12f, 7.5f, 180f);
            ui.enabled = false;
            // The angle in the video (0:16): player's eye, 6.5 m, 34 degrees west of the door face (in the video the west wall shows at 0.4
            // the width of the front wall), walking field of view
            yield return s.FreeCamera(shed + new Vector3(-3.63f, 1.6f, -5.39f), shed + new Vector3(0f, 0.7f, 0f), 60f, 6);
            yield return s.Shot("v_shed_video_angle", 2);
            yield return s.FreeCamera(shed + new Vector3(0.5f, 1.25f, -4.0f), shed + new Vector3(0f, 1.0f, -1.3f), 45f, 6);
            yield return s.Shot("v_shed_door", 2);
            yield return s.FreeCamera(shed + new Vector3(-2.9f, 1.7f, -0.9f), shed + new Vector3(-1.3f, 1.55f, 0f), 45f, 6);
            yield return s.Shot("v_shed_window_eave", 2);
            s.EndFreeCamera();
            ui.enabled = true;

            // House (in its place in the garden; the garden facade faces -Z)
            Vector3 house = HomeYard.HouseCenter;
            yield return Orbit("house", house + new Vector3(0f, 3.5f, 0f), 24f, 10f, 30f, 180f);
            ui.enabled = false;
            // The angles in the video: 0:57 (garden facade, from the left), 0:53.5 (the west gable shows too), 0:41.6 (deck)
            yield return s.FreeCamera(house + new Vector3(-1f, 1.6f, -23.5f), house + new Vector3(3f, 3.2f, -5f), 60f, 6);
            yield return s.Shot("v_house_video_angle", 2);
            yield return s.FreeCamera(house + new Vector3(-13f, 1.6f, -18.5f), house + new Vector3(1f, 3.2f, -3f), 60f, 6);
            yield return s.Shot("v_house_side_angle", 2);
            yield return s.FreeCamera(house + new Vector3(-2.5f, 1.5f, -14f), house + new Vector3(2.5f, 0.9f, -7f), 60f, 6);
            yield return s.Shot("v_house_deck", 2);
            yield return s.FreeCamera(house + new Vector3(4.5f, 1.6f, -11f), house + new Vector3(4.5f, 1.8f, -5f), 50f, 6);
            yield return s.Shot("v_house_doors", 2);
            yield return s.FreeCamera(house + new Vector3(-10.5f, 4.5f, -8.5f), house + new Vector3(-8f, 5.4f, -5f), 50f, 6);
            yield return s.Shot("v_house_eave", 2);
            // Close-ups at arm's length (quality control, 21 October 2026): the deck's west corner (boards resting on the rim joist,
            // the lattice behind the corner post), the same corner from low down, the step and lattice, the downspout's shoe
            // (the grass in front of the deck and along the west gable mown, or it hides what is checked)
            for (float x = house.x - 6f; x <= house.x + 9f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, house.z - 10.5f), new Vector2(x, house.z - 7.75f), 0.15f, Vector2.up, true);
            for (float x = house.x - 10.5f; x <= house.x - 8.05f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, house.z - 7.75f), new Vector2(x, house.z - 2f), 0.15f, Vector2.up, true);
            Vector3 corner = house + new Vector3(-4.2f, 0f, -7.8f);
            yield return s.FreeCamera(corner + new Vector3(-1.1f, 0.9f, -1.3f), corner + new Vector3(0.1f, 0.3f, 0.1f), 45f, 6);
            yield return s.Shot("v_house_deck_corner", 2);
            yield return s.FreeCamera(corner + new Vector3(-0.6f, 0.2f, -0.8f), corner + new Vector3(0.2f, 0.3f, 0.2f), 50f, 6);
            yield return s.Shot("v_house_deck_corner_low", 2);
            yield return s.FreeCamera(house + new Vector3(0.6f, 0.7f, -9.6f), house + new Vector3(0.6f, 0.25f, -7.9f), 50f, 6);
            yield return s.Shot("v_house_step_lattice", 2);
            // The steps from the side: the treads rest on notched stringers
            yield return s.FreeCamera(house + new Vector3(2.7f, 0.55f, -8.75f), house + new Vector3(1.1f, 0.2f, -8.1f), 50f, 6);
            yield return s.Shot("v_house_step_side", 2);
            // The porch roof's east edge (rake) and the main roof's west rake from below: the shingle rows' cut ends
            yield return s.FreeCamera(house + new Vector3(9.7f, 2.4f, -8.6f), house + new Vector3(8.1f, 3.25f, -6.6f), 45f, 6);
            yield return s.Shot("v_house_porch_edge", 2);
            // The porch roof's front corner at eye height on a ladder: the rake board's plumb end against the fascia (21 October 2026)
            yield return s.FreeCamera(house + new Vector3(9.0f, 3.3f, -9.3f), house + new Vector3(8.1f, 2.9f, -8.05f), 45f, 6);
            yield return s.Shot("v_house_porch_corner", 2);
            // The main roof's eave corner (rake board, fascia, gutter, first shingle row)
            yield return s.FreeCamera(house + new Vector3(9.4f, 5.4f, -6.6f), house + new Vector3(8.25f, 6.0f, -5.3f), 45f, 6);
            yield return s.Shot("v_house_eave_corner", 2);
            Vector3 spout = house + new Vector3(-8.07f, 0f, -4.85f);
            // (the garden fence runs at z = 15 from this corner and hides the shoe from the garden: it is seen from behind the fence line)
            yield return s.FreeCamera(spout + new Vector3(-1.2f, 0.9f, -0.45f), spout + new Vector3(-0.1f, 0.3f, 0.05f), 50f, 6);
            yield return s.Shot("v_house_downspout", 2);
            s.EndFreeCamera();
            ui.enabled = true;

            // Tree (in its place in the garden). In the video's 1:06 frame the tree's patch is finished: the grass around it is mown and
            // the roots show
            Vector3 tree = HomeYard.TreePosition;
            for (float x = tree.x - 3f; x <= tree.x + 3f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, tree.z - 3f), new Vector2(x, tree.z + 3f), 0.15f, Vector2.up, true);
            yield return Orbit("tree", tree + new Vector3(0.6f, 3.3f, 0.3f), 11.5f, 10f, 11f);
            ui.enabled = false;
            yield return s.FreeCamera(tree + new Vector3(0.6f, 1.0f, -2.4f), tree + new Vector3(0f, 0.8f, 0f), 50f, 6);
            yield return s.Shot("v_tree_trunk", 2);
            yield return s.FreeCamera(tree + new Vector3(3.4f, 4.4f, -3.6f), tree + new Vector3(0.6f, 5.0f, 0.2f), 45f, 6);
            yield return s.Shot("v_tree_crown", 2);
            yield return s.FreeCamera(tree + new Vector3(3.2f, 1.4f, -1.2f), tree + new Vector3(2.1f, 1.6f, 0.9f), 45f, 6);
            yield return s.Shot("v_tree_swing", 2);
            // Swing (textured model): the close angle in the video (1:07), tire, knot, the round turn on the branch. The tire hangs
            // under the side of the branch where the rope leaves it (Builds/QC/tire_swing.json "rope": tireCenter)
            Vector3 tire = tree + new Vector3(2.105f, 0.78f, 0.80f);
            Vector3 hook = tree + new Vector3(2.105f, 3.95f, 0.85f);
            yield return s.FreeCamera(tire + new Vector3(-0.55f, 0.25f, -0.95f), tire + new Vector3(0f, 0.12f, 0f), 60f, 6);
            yield return s.Shot("v_swing_video_angle", 2);
            yield return s.FreeCamera(tire + new Vector3(-0.35f, 0.4f, -1.25f), tire + new Vector3(0f, 0.08f, 0f), 45f, 6);
            yield return s.Shot("v_swing_tire", 2);
            yield return s.FreeCamera(tire + new Vector3(0.25f, 0.55f, -0.5f), tire + new Vector3(0f, 0.32f, 0f), 45f, 6);
            yield return s.Shot("v_swing_wrap", 2);
            yield return s.FreeCamera(hook + new Vector3(-0.5f, -0.45f, -0.9f), hook + new Vector3(0f, -0.1f, 0f), 45f, 6);
            yield return s.Shot("v_swing_branch", 2);
            s.EndFreeCamera();
            ui.enabled = true;

            // From the player's eye (with the interface)
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Shot("v_fps_mower");
            s.Equip(Tool.HoseSlot);
            var feet = new Vector3(5f, 0f, -8.6f);
            Vector2 look = LookAt(feet, new Vector3(5f, 0f, -6f));
            yield return s.Pose(feet, look.x, look.y);
            yield return s.Seconds(0.2f);
            yield return s.Shot("v_fps_hose");
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Seconds(0.8f);
            yield return s.Shot("v_fps_hose_watering", 2);
            s.SetInput(PlayerInputState.None);
            MowInFront(s, TrimmerFrameFeet, TrimmerFrameYaw);
            s.Equip(Tool.TrimmerSlot);
            yield return s.Pose(TrimmerFrameFeet, TrimmerFrameYaw, TrimmerFramePitch);
            yield return s.Shot("v_fps_trimmer");
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Seconds(0.5f);
            yield return s.Shot("v_fps_trimmer_running", 2);
            s.SetInput(PlayerInputState.None);
            s.Equip(Tool.HandSlot);
            Vector3 eyeYard = new Vector3(15.6f, 0.05f, -9.4f);
            Vector2 view = LookAt(eyeYard, tree);
            yield return s.Pose(eyeYard, view.x, view.y - 16f);
            yield return s.Shot("v_fps_tree");
        }

        // The hose frame in the video (0:20.6, docs/reference/24): screen position (x right, y up, 0..1), radius relative to the
        // screen height; the apparent roundness of the head (minor/major axis) and the direction the grip goes down on screen
        static readonly Vector2 RefHoseHead = new Vector2(0.649f, 0.32f);
        static readonly Vector2 RefHoseKnob = new Vector2(0.698f, 0.202f);
        const float RefHoseHeadRadius = 0.07f, RefHoseKnobRadius = 0.043f, RefHoseHeadRoundness = 0.85f;
        static readonly Vector2 RefHoseGripDirection = new Vector2(-0.1f, -1f).normalized;

        struct HoseView
        {
            public Vector2 Head, Knob, Grip;
            public float HeadRadius, KnobRadius, Roundness;
        }

        /// <summary>How the nozzle looks on screen (with the root rotation as it is right now).</summary>
        static HoseView MeasureHoseView(Camera cam, Transform root)
        {
            float aspect = (float)cam.pixelWidth / cam.pixelHeight;
            Vector2 Screen(Vector3 world)
            {
                Vector3 v = cam.WorldToViewportPoint(world);
                return new Vector2(v.x, v.y);
            }
            Vector2 Scaled(Vector2 v) => new Vector2(v.x * aspect, v.y);
            var view = new HoseView();
            Vector3 head = root.TransformPoint(HoseModel.HeadCenter), knob = root.TransformPoint(HoseModel.KnobCenter);
            view.Head = Screen(head);
            view.Knob = Screen(knob);
            float min = float.MaxValue, max = 0f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                Vector3 rim = root.TransformPoint(HoseModel.HeadCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * HoseModel.HeadRadius);
                float d = (Scaled(Screen(rim)) - Scaled(view.Head)).magnitude;
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
            view.HeadRadius = max;
            view.Roundness = max > 1e-5f ? min / max : 0f;
            float kmax = 0f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 rim = root.TransformPoint(HoseModel.KnobCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * HoseModel.KnobRadius);
                kmax = Mathf.Max(kmax, (Scaled(Screen(rim)) - Scaled(view.Knob)).magnitude);
            }
            view.KnobRadius = kmax;
            view.Grip = Scaled(Screen(root.TransformPoint(HoseModel.GripBottom)) - Screen(root.TransformPoint(HoseModel.GripTop))).normalized;
            return view;
        }

        static float HoseCost(HoseView v) =>
            Vector2.Distance(v.Head, RefHoseHead) + Vector2.Distance(v.Knob, RefHoseKnob)
            + 1.5f * (Mathf.Abs(v.HeadRadius - RefHoseHeadRadius) + Mathf.Abs(v.KnobRadius - RefHoseKnobRadius))
            + 0.25f * Mathf.Abs(v.Roundness - RefHoseHeadRoundness)
            + 0.1f * (1f - Vector2.Dot(v.Grip, RefHoseGripDirection));

        static string Describe(HoseView v) =>
            $"head {v.Head.x:F3},{v.Head.y:F3} r {v.HeadRadius:F3} round {v.Roundness:F2} (video {RefHoseHead.x:F3},{RefHoseHead.y:F3} r {RefHoseHeadRadius:F3} round {RefHoseHeadRoundness:F2}); " +
            $"knob {v.Knob.x:F3},{v.Knob.y:F3} r {v.KnobRadius:F3} (video {RefHoseKnob.x:F3},{RefHoseKnob.y:F3} r {RefHoseKnobRadius:F3}); " +
            $"grip direction {v.Grip.x:F2},{v.Grip.y:F2} (video {RefHoseGripDirection.x:F2},{RefHoseGripDirection.y:F2})";

        static void PoseHose(HoseTool hose, Transform eye, Vector3 anchor, Vector3 euler) =>
            hose.transform.SetPositionAndRotation(eye.TransformPoint(anchor), eye.rotation * Quaternion.Euler(euler));

        /// <summary>
        /// Finds the hose nozzle's grip by measuring against the video: position (3) and rotation (3) relative to the camera are tried
        /// thousands of times in the same frame (random starts + coordinate descent); the one whose head and knob screen position, size,
        /// head roundness and grip direction are closest to the video is chosen and written to the log.
        /// </summary>
        // The trimmer in the video's 0:07 frame (docs/reference/26_trimmer_body.png, 848 x 478 px; viewport units, y up), read from a
        // gridded zoom of the frame: the head puck's middle, the body's front end where the shaft enters, the top of the D handle's
        // arch, the middle of the battery's back face (the narrow framed face with the red latch: the trimmer is seen from its left
        // side, most of the dark block on screen is the battery's left side) and that face's width
        static readonly Vector2 RefTrimmerHead = new Vector2(430f / 848f, 1f - 277f / 478f);
        static readonly Vector2 RefTrimmerBodyFront = new Vector2(527f / 848f, 1f - 262f / 478f);
        static readonly Vector2 RefTrimmerHandleTop = new Vector2(492f / 848f, 1f - 215f / 478f);
        static readonly Vector2 RefTrimmerBattery = new Vector2(675f / 848f, 1f - 300f / 478f);
        const float RefTrimmerBatteryWidth = 45f / 478f;           // in screen heights
        const float RefTrimmerBatteryHeight = 88f / 478f;          // the battery block's back face, top to bottom
        /// <summary>Where the trimmer framing is fitted and shot: an open stretch of the back yard, the fitted camera pitch.</summary>
        static readonly Vector3 TrimmerFrameFeet = new Vector3(-2.5f, 0f, -12.9f);
        const float TrimmerFrameYaw = 200f;
        const float TrimmerFramePitch = 45f;

        struct TrimmerView
        {
            public Vector2 Head, BodyFront, HandleTop, Battery, BatteryUp;
            public float BatteryWidth, BatteryHeight;
        }

        /// <summary>The trimmer's feature points on screen (x scaled by the aspect ratio, so distances are in screen heights).</summary>
        static TrimmerView MeasureTrimmerView(Camera cam, TrimmerModel model)
        {
            float aspect = (float)cam.pixelWidth / cam.pixelHeight;
            Vector2 Screen(Vector3 world)
            {
                Vector3 v = cam.WorldToViewportPoint(world);
                return new Vector2(v.x * aspect, v.y);
            }
            Vector3 side = new Vector3(TrimmerModel.BatteryHalfWidth, 0f, 0f);
            return new TrimmerView
            {
                Head = Screen(model.Head.TransformPoint(TrimmerModel.HeadCenter)),
                BodyFront = Screen(model.Motor.TransformPoint(TrimmerModel.BodyFront)),
                HandleTop = Screen(model.Handle.TransformPoint(TrimmerModel.HandleTop)),
                Battery = Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack)),
                BatteryWidth = (Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack + side))
                                - Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack - side))).magnitude,
                BatteryHeight = (Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack + new Vector3(0f, TrimmerModel.BatteryHalfHeight, 0f)))
                                 - Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack - new Vector3(0f, TrimmerModel.BatteryHalfHeight, 0f)))).magnitude,
                // The battery face's own up on screen: upright in the video (the body is held level, not rolled)
                BatteryUp = (Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack + new Vector3(0f, 0.06f, 0f)))
                             - Screen(model.Motor.TransformPoint(TrimmerModel.BatteryBack))).normalized,
            };
        }

        static float TrimmerCost(TrimmerView v, float aspect)
        {
            Vector2 Ref(Vector2 r) => new Vector2(r.x * aspect, r.y);
            return Vector2.Distance(v.Head, Ref(RefTrimmerHead)) + Vector2.Distance(v.BodyFront, Ref(RefTrimmerBodyFront))
                   + Vector2.Distance(v.HandleTop, Ref(RefTrimmerHandleTop)) + Vector2.Distance(v.Battery, Ref(RefTrimmerBattery))
                   + Mathf.Abs(v.BatteryWidth - RefTrimmerBatteryWidth) + Mathf.Abs(v.BatteryHeight - RefTrimmerBatteryHeight)
                   + 0.2f * (1f - Vector2.Dot(v.BatteryUp, Vector2.up));
        }

        /// <summary>In the video's 0:07 frame the trimmer works on short grass: a 3 x 3 m patch in front of the feet is mown so the head shows.</summary>
        static void MowInFront(ScenarioContext s, Vector3 feet, float yaw)
        {
            LawnState lawn = GameBootstrap.Instance.Lawn;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            for (float x = -1.5f; x <= 1.5f; x += 0.2f)
            {
                Vector3 a = feet + right * x + forward * 0.3f, b = feet + right * x + forward * 3.3f;
                lawn.CutSwept(new Vector2(a.x, a.z), new Vector2(b.x, b.z), 0.15f, new Vector2(forward.x, forward.z), true);
            }
        }

        static string Describe(TrimmerView v, float aspect) =>
            $"head ({v.Head.x / aspect:F3}, {v.Head.y:F3}), body front ({v.BodyFront.x / aspect:F3}, {v.BodyFront.y:F3}), " +
            $"D handle top ({v.HandleTop.x / aspect:F3}, {v.HandleTop.y:F3}), battery ({v.Battery.x / aspect:F3}, {v.Battery.y:F3}) " +
            $"{v.BatteryWidth:F3} wide, {v.BatteryHeight:F3} tall";

        /// <summary>
        /// U16 trimmer framing: fits where the hands hold the trimmer (camera space), its roll about the shaft and the camera pitch so the
        /// head, the body's front, the D handle's top and the battery's back face sit where they are in the video's 0:07 frame, with the
        /// battery face as wide. Multi-start coordinate descent; the result is written into TrimmerTool (HandAnchor, HoldRoll).
        /// </summary>
        static IEnumerator U16TrimmerFit(ScenarioContext s)
        {
            PlayerController p = s.Player;
            MowInFront(s, TrimmerFrameFeet, TrimmerFrameYaw);
            s.Equip(Tool.TrimmerSlot);
            yield return s.Pose(TrimmerFrameFeet, TrimmerFrameYaw, TrimmerFramePitch);
            yield return s.Seconds(0.8f);
            var trimmer = (TrimmerTool)p.Tools.FindSlot(Tool.TrimmerSlot);
            Camera cam = p.WorldCamera;
            Transform eye = cam.transform;
            float aspect = (float)cam.pixelWidth / cam.pixelHeight;
            Vector3 eyePosition = eye.position;
            Vector3 feet = p.transform.position;
            float Cost(float[] q)
            {
                TrimmerTool.HandAnchor = new Vector3(q[0], q[1], q[2]);
                TrimmerTool.HoldRoll = Mathf.Clamp(q[4], -15f, 15f);       // held level: a little roll at most
                // The video's camera looks fairly level (the fence fills the upper half): the pitch stays between 20 and 45 degrees
                eye.SetPositionAndRotation(eyePosition, Quaternion.Euler(Mathf.Clamp(q[3], 20f, 45f), TrimmerFrameYaw, 0f));
                trimmer.PoseFrom(eye, feet);
                return TrimmerCost(MeasureTrimmerView(cam, trimmer.Model), aspect);
            }

            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float[] best = null;
            float bestCost = float.MaxValue;
            for (int start = 0; start < 40; start++)
            {
                float[] q = { R(0.1f, 0.5f), R(-0.45f, -0.08f), R(0.35f, 1.1f), R(20f, 45f), R(-15f, 15f) };
                float[] step = { 0.04f, 0.04f, 0.08f, 4f, 6f };
                float c = Cost(q);
                for (int round = 0; round < 80; round++)
                {
                    bool improved = false;
                    for (int k = 0; k < q.Length; k++)
                    {
                        foreach (float sign in new[] { 1f, -1f })
                        {
                            float old = q[k];
                            q[k] = old + sign * step[k];
                            float trial = Cost(q);
                            if (trial < c) { c = trial; improved = true; }
                            else q[k] = old;
                        }
                    }
                    if (!improved)
                    {
                        for (int k = 0; k < step.Length; k++) step[k] *= 0.5f;
                        if (step[3] < 0.1f) break;
                    }
                }
                if (c < bestCost)
                {
                    bestCost = c;
                    best = (float[])q.Clone();
                }
            }

            best[3] = Mathf.Clamp(best[3], 20f, 45f);
            best[4] = Mathf.Clamp(best[4], -15f, 15f);
            Cost(best);
            TrimmerView fitted = MeasureTrimmerView(cam, trimmer.Model);
            string detail = $"best: hands {best[0]:F3}, {best[1]:F3}, {best[2]:F3}; pitch {best[3]:F1}; roll {best[4]:F1}; " +
                            $"{Describe(fitted, aspect)}; deviation {bestCost:F3}";
            Debug.Log("[Opus] Trimmer tuning " + detail);
            // 0.164 on 21 October 2026: the video's body is a little bigger and its camera a little more level than the game allows
            s.Check("trimmer_tuning", bestCost < 0.2f, detail);
            yield return s.Pose(TrimmerFrameFeet, TrimmerFrameYaw, best[3]);
            yield return s.Seconds(0.3f);
            yield return s.Shot("u16_trimmer_tuning");
        }

        static IEnumerator U16HoseFit(ScenarioContext s)
        {
            PlayerController p = s.Player;
            s.Equip(Tool.HoseSlot);
            var hose = (HoseTool)p.Tools.FindSlot(Tool.HoseSlot);
            var feet = new Vector3(5f, 0f, -8.6f);
            Vector2 look = LookAt(feet, new Vector3(5f, 0f, -6f));
            yield return s.Pose(feet, look.x, look.y);
            yield return s.Seconds(0.3f);

            Camera cam = p.WorldCamera;
            Transform eye = cam.transform;
            Transform root = hose.Model.Root;
            float Cost(float[] q)
            {
                PoseHose(hose, eye, new Vector3(q[0], q[1], q[2]), new Vector3(q[3], q[4], q[5]));
                return HoseCost(MeasureHoseView(cam, root));
            }

            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float[] best = null;
            float bestCost = float.MaxValue;
            for (int start = 0; start < 40; start++)
            {
                float[] q = { R(0.1f, 0.35f), R(-0.25f, -0.02f), R(0.45f, 1.0f), R(-30f, 60f), R(-60f, 30f), R(-40f, 40f) };
                float[] step = { 0.04f, 0.04f, 0.08f, 10f, 10f, 10f };
                float c = Cost(q);
                for (int round = 0; round < 60; round++)
                {
                    bool improved = false;
                    for (int k = 0; k < q.Length; k++)
                    {
                        foreach (float sign in new[] { 1f, -1f })
                        {
                            float old = q[k];
                            q[k] = old + sign * step[k];
                            float trial = Cost(q);
                            if (trial < c) { c = trial; improved = true; }
                            else q[k] = old;
                        }
                    }
                    if (!improved)
                    {
                        for (int k = 0; k < step.Length; k++) step[k] *= 0.5f;
                        if (step[3] < 0.2f) break;
                    }
                }
                if (c < bestCost)
                {
                    bestCost = c;
                    best = (float[])q.Clone();
                }
            }

            HoseTool.HandAnchor = new Vector3(best[0], best[1], best[2]);
            HoseTool.HoldEuler = new Vector3(best[3], best[4], best[5]);
            yield return s.Frames(3);
            HoseView view = MeasureHoseView(cam, root);
            string detail = $"best: hand {best[0]:F3}, {best[1]:F3}, {best[2]:F3}; rotation {best[3]:F1}, {best[4]:F1}, {best[5]:F1}; {Describe(view)}; deviation {bestCost:F3}";
            Debug.Log("[Opus] Hose tuning " + detail);
            s.Check("hose_tuning", bestCost < 0.08f, detail);
            yield return s.Shot("u16_hose_tuning");
        }

        /// <summary>
        /// U16 Blender model pipeline: the calibration model lines up with Unity's own axes in game (white balls computed by
        /// Unity at the arrow tips); the wheel generated in Blender up close, on the mower and from the player's eye.
        /// </summary>
        static IEnumerator U16Models(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            LawnState lawn = game.Lawn;
            for (float x = -3.5f; x <= 3.8f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, -7.8f), new Vector2(x, -4.4f), 0.15f, Vector2.up, true);
            var showcase = new GameObject("Showroom").transform;
            s.Equip(Tool.HandSlot);

            // 1) Calibration: are the arrow tips in the FBX at the points Unity computes with +Z/+X/+Y?
            Transform calibration = ModelLibrary.Whole("calibration", showcase);
            calibration.position = new Vector3(1.6f, 0f, -6.4f);
            float worst = 0f;
            foreach ((string part, Vector3 local) in new[]
                     {
                         ("Arrow_Front", new Vector3(0f, 0.5f, 1.1f)), ("Arrow_Right", new Vector3(0.9f, 0.5f, 0f)), ("Arrow_Up", new Vector3(0f, 1.3f, 0f)),
                     })
            {
                Vector3 expected = calibration.TransformPoint(local);
                PrimitiveKit.Sphere(showcase, "Expected_" + part, expected, Vector3.one * 0.07f, Color.white);
                Bounds b = calibration.Find(part).GetComponent<Renderer>().bounds;
                Vector3 tip = local.z > 0.6f ? new Vector3(b.center.x, b.center.y, b.max.z)
                    : local.x > 0.6f ? new Vector3(b.max.x, b.center.y, b.center.z)
                    : new Vector3(b.center.x, b.max.y, b.center.z);
                worst = Mathf.Max(worst, Vector3.Distance(tip, expected));
            }
            worst = Mathf.Max(worst, Vector3.Distance(calibration.Find("Marker").position, calibration.TransformPoint(new Vector3(0.7f, 0.2f, 0.7f))));
            s.Check("calibration_axes", worst < 0.002f, $"arrow tips and marker: largest deviation {worst * 1000f:F2} mm");
            yield return s.FreeCamera(calibration.position + new Vector3(2.4f, 1.7f, 2.7f), calibration.position + new Vector3(0.25f, 0.5f, 0.25f), 42f);
            yield return s.Shot("u16_calibration");

            // 2) Mower showcase (same place and angle as u10) and the wheel close-up
            Transform mowerSpot = new GameObject("MowerShowcase").transform;
            mowerSpot.SetParent(showcase, false);
            mowerSpot.position = new Vector3(-1.8f, 0f, -6.2f);
            mowerSpot.rotation = Quaternion.Euler(0f, 150f, 0f);
            MowerModel mower = MowerModel.Build(mowerSpot);
            float rearRadius = mower.RearAxles[1].GetComponentInChildren<MeshFilter>().sharedMesh.bounds.extents.y;
            float frontRadius = mower.FrontAxles[1].GetComponentInChildren<MeshFilter>().sharedMesh.bounds.extents.y;
            s.Check("wheel_size", Mathf.Abs(rearRadius - MowerModel.RearWheelRadius) < 0.002f && Mathf.Abs(frontRadius - MowerModel.FrontWheelRadius) < 0.002f,
                $"rear r {rearRadius * 1000f:F1} mm (physics {MowerModel.RearWheelRadius * 1000f:F0}), front r {frontRadius * 1000f:F1} mm (physics {MowerModel.FrontWheelRadius * 1000f:F0})");
            Vector3 wheel = mower.RearAxles[1].position;
            yield return s.FreeCamera(wheel + mowerSpot.right * 0.62f - mowerSpot.forward * 0.42f + Vector3.up * 0.3f, wheel + Vector3.up * 0.02f, 34f);
            yield return s.Shot("u16_wheel_close");
            yield return s.FreeCamera(mowerSpot.position + mowerSpot.right * 0.95f + mowerSpot.forward * 0.55f + Vector3.up * 0.55f,
                mowerSpot.position + Vector3.up * 0.16f, 38f);
            yield return s.Shot("u16_mower_front_side");
            Vector3 mowerEye = mowerSpot.position + new Vector3(1.1f, 0.05f, -1.2f);
            Vector2 view = LookAt(mowerEye, mowerSpot.position);
            yield return s.Pose(mowerEye, view.x, view.y - 8f);
            yield return s.Shot("u16_mower_close");
            Object.Destroy(showcase.gameObject);

            // 3) From the player's eye (the reference 01 framing: entering long grass with the mower)
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Shot("u16_mower_fps");

            // 4) Hose nozzle and trimmer: close up in the showcase and from the player's eye (the reference 04/07 and 25/26 framings)
            var tools = new GameObject("ToolShowcase").transform;
            Transform hoseSpot = new GameObject("HoseShowcase").transform;
            hoseSpot.SetParent(tools, false);
            hoseSpot.SetPositionAndRotation(new Vector3(2.4f, 0.7f, -6.4f), Quaternion.Euler(0f, 210f, 0f));
            HoseModel hoseModel = HoseModel.Build(hoseSpot);
            s.Check("hose_trigger_at_joint", hoseModel.Trigger.localPosition.y < -0.02f && hoseModel.Trigger.localPosition.z < HoseModel.HeadCenter.z - 0.03f,
                $"trigger joint {hoseModel.Trigger.localPosition:F3}");
            Transform trimmerSpot = new GameObject("TrimmerShowcase").transform;
            trimmerSpot.SetParent(tools, false);
            trimmerSpot.SetPositionAndRotation(new Vector3(-0.6f, 0.16f, -6.2f), Quaternion.Euler(0f, 70f, 0f));
            TrimmerModel trimmerModel = TrimmerModel.Build(trimmerSpot);
            yield return s.FreeCamera(hoseSpot.position + new Vector3(0.35f, 0.18f, -0.5f), hoseSpot.position + new Vector3(0f, -0.08f, 0f), 34f);
            yield return s.Shot("u16_hose_close");
            Vector3 trimmerMid = trimmerSpot.TransformPoint(new Vector3(0f, 0f, -0.8f));
            yield return s.FreeCamera(trimmerMid + new Vector3(0.9f, 0.9f, -1.3f), trimmerMid, 40f);
            yield return s.Shot("u16_trimmer_close");
            Object.Destroy(tools.gameObject);

            s.Equip(Tool.HoseSlot);
            var feet = new Vector3(5f, 0f, -8.6f);
            Vector2 look = LookAt(feet, new Vector3(5f, 0f, -6f));
            yield return s.Pose(feet, look.x, look.y);
            yield return s.Seconds(0.2f);
            var hoseTool = (HoseTool)s.Player.Tools.FindSlot(Tool.HoseSlot);
            HoseView hoseView = MeasureHoseView(s.Player.WorldCamera, hoseTool.Model.Root);
            float hoseCost = HoseCost(hoseView);
            s.Check("hose_framing_matches_video", hoseCost < 0.08f, $"{Describe(hoseView)}; deviation {hoseCost:F3}");
            yield return s.Shot("u16_hose_fps");
            // Water: a puffy jet and rings spreading on the ground
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Seconds(0.9f);
            WaterSpray spray = hoseTool.Spray;
            s.Check("water_jet", spray.AliveCount > 15 && spray.Reach > 0.25f && spray.Reach < 0.7f && spray.ActiveRipples >= 1,
                $"{spray.AliveCount} puffs, jet length {spray.Reach:F2} m, {spray.ActiveRipples} rings");
            yield return s.Shot("u16_hose_watering", 2);
            s.SetInput(PlayerInputState.None);
            MowInFront(s, TrimmerFrameFeet, TrimmerFrameYaw);
            s.Equip(Tool.TrimmerSlot);
            yield return s.Pose(TrimmerFrameFeet, TrimmerFrameYaw, TrimmerFramePitch);
            yield return s.Seconds(0.3f);
            var trimmerTool = (TrimmerTool)s.Player.Tools.FindSlot(Tool.TrimmerSlot);
            Camera trimmerCamera = s.Player.WorldCamera;
            float trimmerAspect = (float)trimmerCamera.pixelWidth / trimmerCamera.pixelHeight;
            TrimmerView trimmerView = MeasureTrimmerView(trimmerCamera, trimmerTool.Model);
            float trimmerCost = TrimmerCost(trimmerView, trimmerAspect);
            s.Check("trimmer_framing_matches_video", trimmerCost < 0.2f, $"{Describe(trimmerView, trimmerAspect)}; deviation {trimmerCost:F3}");
            yield return s.Shot("u16_trimmer_fps");

            // 5) Garden tree: from the garden, trunk and roots (the video's 1:06 angle), crown up close
            s.Equip(Tool.HandSlot);
            Vector3 tree = HomeYard.TreePosition;
            Vector3 eye = new Vector3(15.6f, 0.05f, -9.4f);
            view = LookAt(eye, tree);
            yield return s.Pose(eye, view.x, view.y - 16f);
            yield return s.Shot("u16_tree");
            yield return s.FreeCamera(tree + new Vector3(0.6f, 1.0f, -2.4f), tree + new Vector3(0f, 0.8f, 0f), 50f);
            yield return s.Shot("u16_tree_trunk");
            yield return s.FreeCamera(tree + new Vector3(3.4f, 4.4f, -3.6f), tree + new Vector3(0.6f, 5.0f, 0.2f), 45f);
            yield return s.Shot("u16_tree_crown");
            s.EndFreeCamera();
        }

        /// <summary>Waits for a garden change: the new scene is built, the transition card is gone, (when coming home) the night is over.</summary>
        static IEnumerator WaitForYard(ScenarioContext s, string yardId, System.Action<YardSession> onNight = null, float timeout = 25f)
        {
            bool nightSeen = false;
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                GameBootstrap game = GameBootstrap.Instance;
                if (game != null && game.Session != null && YardBuilder.Definition.Id == yardId && !Career.Traveling)
                {
                    YardSession session = game.Session;
                    if (session.IsNight && session.Hud.NightAlpha >= 0.999f && !nightSeen)
                    {
                        nightSeen = true;
                        onNight?.Invoke(session);
                    }
                    if (!session.IsNight && session.Hud.NightAlpha <= 0.001f && t > 0.2f)
                        yield break;
                }
                yield return null;
            }
            s.Check($"transition_{yardId}", false, "the garden change timed out");
        }

        /// <summary>
        /// U15: three client jobs from the job board. The city garden is finished and delivered (three stars); the manor is left half done
        /// and later resumed where it was left, delivered without the requested pattern (two stars); the country house is delivered early
        /// (no stars). Every return home brings night, the payment is added to the points, the job records are written to the home save; the
        /// profile (day, points, upgrades) is carried both ways; the build time of every garden change is measured.
        /// </summary>
        static IEnumerator U15Jobs(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            var buildTimes = new List<float>();
            JobDef city = Jobs.Find(ClientYards.CityId), manor = Jobs.Find(ClientYards.ManorId), country = Jobs.Find(ClientYards.CountryId);
            session.Upgrades.Restore(new[] { 1, 0, 0, 0, 0 });
            int points = session.Wallet.Points, day = session.Days.Day;

            // 1) Job board
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(0f, 0.05f, -8f), 0f, 4f);
            game.Menu.OpenJobBoard();
            yield return s.Frames(3);
            s.Check("job_board", game.Menu.IsOpen && game.Menu.CurrentPage == UI.GameMenu.Page.JobBoard && Jobs.All.Length == 3, $"page {game.Menu.CurrentPage}");
            yield return s.Shot("u15_job_board");

            // 2) City garden: accept, travel, work, the automatic delivery card, three stars
            game.Menu.AcceptJob(city);
            yield return WaitForYard(s, ClientYards.CityId);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("city_garden", session.Job == city && session.Days.Day == day && session.Wallet.Points == points && session.Upgrades.Level(UpgradeId.WideDeck) == 1
                                     && session.Hud.AreaTitle.StartsWith("City garden") && session.Hud.DayLine.Contains("Ms. Selin") && session.Tasks.Total == 5,
                $"job {session.Job?.Id}, day {session.Days.Day}, points {session.Wallet.Points}, wide deck {session.Upgrades.Level(UpgradeId.WideDeck)}, " +
                $"card '{session.Hud.AreaTitle}' / '{session.Hud.DayLine}', {session.Tasks.Total} patches");
            yield return s.Shot("u15_city_arrival");
            yield return s.Pose(new Vector3(-6.5f, 0.05f, -5f), 40f, 10f);
            yield return s.Shot("u15_city_overview");
            // Nothing thin and tall (stretched like a line) may be drawn: a visible object in the garden longer than 2.5 m and thinner than 0.4 m
            var stretched = new List<string>();
            var tallParts = new HashSet<string> { "Column", "Downspout", "CornerTrim", "Pole", "Rope", "ArchPost", "Post", "LatticePost", "Leg", "Branch", "SideBranch", "Zenith", "BodySlice", "Baluster", "TireSwing" };
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Bounds b = renderer.bounds;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy && !tallParts.Contains(renderer.name) && b.size.y > 2.5f && b.size.x < 1.5f && b.size.z < 1.5f && b.min.y < 1f)
                    stretched.Add($"{renderer.name}@{b.center:F1}");
            }
            s.Check("no_stretched_objects", stretched.Count == 0, stretched.Count == 0 ? "none" : string.Join(", ", stretched.Take(20)));
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(-1f, 0.05f, -2.5f), 90f, MovementProfile.Mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2f);
            float worked = session.EvaluateJob(true).Completion;
            int earnedInJob = session.Wallet.Points - points;
            s.Check("working_on_job", worked > 0f && earnedInJob == 0, $"completion {worked * 100f:0.0}%, points earned on the job {earnedInJob} (paid on delivery)");
            foreach (PatchProgress patch in session.Tasks.Patches)
            {
                session.CompletePatch(patch.Id);
                yield return null;
            }
            yield return s.Frames(3);
            JobResult cityResult = session.EvaluateJob(true);
            s.Check("delivery_card", game.Menu.IsOpen && game.Menu.CurrentPage == UI.GameMenu.Page.JobResult && cityResult.Stars == 3 && cityResult.Payment == city.MaxPay,
                $"page {game.Menu.CurrentPage}, {cityResult.Stars} stars, payment {cityResult.Payment}, time {Jobs.FormatTime(cityResult.Seconds)}");
            yield return s.Shot("u15_delivery_card");

            // 3) Delivery → home → night (a job takes a day) → payment and record
            string nightText = string.Empty;
            game.Menu.DeliverJob();
            yield return WaitForYard(s, HomeYard.Id, home => nightText = home.Hud.NightText);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("return_home", session.Job == null && session.Days.Day == day + 1 && session.Wallet.Points == points + city.MaxPay && nightText.Contains("Ms. Selin")
                                 && Career.RecordOf(city.Id).bestStars == 3 && !SaveSystem.SlotExists(city.Id) && session.Upgrades.Level(UpgradeId.WideDeck) == 1,
                $"day {session.Days.Day}, points {session.Wallet.Points} (+{session.Wallet.Points - points}), record {Career.RecordOf(city.Id).bestStars} stars, night '{nightText.Split('\n')[0]}…'");
            points = session.Wallet.Points;
            day = session.Days.Day;

            // 4) Manor: do half, leave; then continue where it was left
            game.Menu.AcceptJob(manor);
            yield return WaitForYard(s, ClientYards.ManorId);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("manor_pattern", session.Pattern != null && session.Pattern.Kind == PatternKind.StripesNorthSouth && session.Tasks.Get(session.Pattern.Patch)?.Name == "Front lawn",
                $"pattern {session.Pattern?.Name} · {session.Tasks.Get(session.Pattern?.Patch ?? 0)?.Name}");
            yield return s.Pose(new Vector3(-16f, 0.05f, -12f), 35f, 5f);
            yield return s.Shot("u15_manor_overview");
            foreach (PatchProgress patch in session.Tasks.Patches.OrderByDescending(p => p.AreaSquareMeters))
            {
                if (session.EvaluateJob(true).Completion >= 0.55f)
                    break;
                session.CompletePatch(patch.Id);
                yield return null;
            }
            yield return s.Seconds(0.5f);
            JobResult manorHalf = session.EvaluateJob(true);
            game.Menu.ShowJobResult();
            yield return s.Frames(3);
            s.Check("early_delivery_preview", manorHalf.Stars == 1 && game.Menu.CurrentPage == UI.GameMenu.Page.JobResult,
                $"completion {manorHalf.Completion * 100f:0}%, {manorHalf.Stars} stars, payment {manorHalf.Payment}");
            yield return s.Shot("u15_early_delivery");
            game.Menu.ShowPage(UI.GameMenu.Page.ConfirmLeave);
            yield return s.Frames(2);
            game.Menu.LeaveJob();
            yield return WaitForYard(s, HomeYard.Id);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("job_abandoned", session.Days.Day == day + 1 && session.Wallet.Points == points && SaveSystem.SlotExists(manor.Id) && Career.RecordOf(manor.Id).delivered == 0,
                $"day {session.Days.Day}, points {session.Wallet.Points}, half-done save {(SaveSystem.SlotExists(manor.Id) ? "yes" : "none")}");
            day = session.Days.Day;

            game.Menu.AcceptJob(manor);
            yield return WaitForYard(s, ClientYards.ManorId);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            JobResult resumed = session.EvaluateJob(true);
            s.Check("resumed_where_left", Mathf.Abs(resumed.Completion - manorHalf.Completion) < 0.01f && resumed.Seconds >= manorHalf.Seconds && session.Days.Day == day,
                $"completion {resumed.Completion * 100f:0}% (before {manorHalf.Completion * 100f:0}%), time {Jobs.FormatTime(resumed.Seconds)}");
            foreach (PatchProgress patch in session.Tasks.Patches)
            {
                session.CompletePatch(patch.Id);
                yield return null;
            }
            yield return s.Frames(3);
            JobResult manorResult = session.EvaluateJob(true);
            s.Check("two_stars_without_pattern", manorResult.Stars == 2 && !manorResult.PatternDone && game.Menu.CurrentPage == UI.GameMenu.Page.JobResult,
                $"{manorResult.Stars} stars, pattern {(manorResult.PatternDone ? "done" : "missing")}, payment {manorResult.Payment}");
            game.Menu.DeliverJob();
            yield return WaitForYard(s, HomeYard.Id);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("manor_paid", session.Wallet.Points == points + manor.PayByStars[2] && !SaveSystem.SlotExists(manor.Id) && Career.RecordOf(manor.Id).bestStars == 2,
                $"points {session.Wallet.Points} (+{session.Wallet.Points - points})");
            points = session.Wallet.Points;
            day = session.Days.Day;

            // 5) Country house: pull one weed with the trimmer, deliver right away → no stars, no payment but recorded
            game.Menu.AcceptJob(country);
            yield return WaitForYard(s, ClientYards.CountryId);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            yield return s.Pose(new Vector3(-3f, 0.05f, -8.8f), -30f, 6f);
            yield return s.Shot("u15_country_overview");
            yield return Look(s, "u15_country_shed", new Vector3(-4.4f, 0.05f, -9.3f), new Vector3(-9.5f, 0f, -6.5f), 9f);
            int weedsBefore = game.Weeds.RemainingWeeds;
            s.Equip(Tool.TrimmerSlot);
            Vector2 aim = LookAt(new Vector3(-4f, 0.05f, -6f), new Vector3(-4f, 0f, -5f));
            yield return s.Pose(new Vector3(-4f, 0.05f, -6f), aim.x, aim.y);
            yield return s.Hold(ScenarioContext.Input(use: true), 2.5f);
            s.Check("country_house_weeds", game.Weeds.TotalWeeds >= 14 && game.Weeds.RemainingWeeds < weedsBefore, $"weeds {weedsBefore} → {game.Weeds.RemainingWeeds} / {game.Weeds.TotalWeeds}");
            game.Menu.ShowJobResult();
            yield return s.Frames(2);
            JobResult countryResult = session.EvaluateJob(true);
            game.Menu.DeliverJob();
            yield return WaitForYard(s, HomeYard.Id);
            buildTimes.Add(Career.LastBuildSeconds);
            game = GameBootstrap.Instance;
            session = game.Session;
            s.Check("delivery_without_stars", countryResult.Stars == 0 && session.Wallet.Points == points && Career.RecordOf(country.Id).delivered == 1 && session.Days.Day == day + 1,
                $"{countryResult.Stars} stars (completion {countryResult.Completion * 100f:0}%), points {session.Wallet.Points}");

            // 6) The home save carries the job records; garden changes are fast
            bool readBack = SaveSystem.TryRead(out SaveData homeSave, out string error);
            s.Check("home_save", readBack && homeSave.yardId == HomeYard.Id && homeSave.jobRecords != null && homeSave.jobRecords.Length == 3 && homeSave.points == session.Wallet.Points,
                $"{error} record {homeSave?.jobRecords?.Length} jobs, points {homeSave?.points}");
            float slowest = buildTimes.Max();
            s.Check("build_time", slowest < 1f, $"{buildTimes.Count} changes, longest {slowest * 1000f:0} ms (average {buildTimes.Average() * 1000f:0} ms; target < 1000 ms)");
            yield return s.Pose(new Vector3(0f, 0.05f, -8f), 0f, 4f);
            game.Menu.OpenJobBoard();
            yield return s.Frames(3);
            yield return s.Shot("u15_job_board_saved");
            game.Menu.Close();
        }

        static IEnumerator Look(ScenarioContext s, string shot, Vector3 feet, Vector3 target, float raise)
        {
            Vector2 view = LookAt(feet, target);
            yield return s.Pose(feet, view.x, view.y - raise);
            yield return s.Shot(shot);
        }

        /// <summary>
        /// U14: when the side yard is finished the orchard's gate opens, when the orchard is finished the vegetable garden's gate opens;
        /// you walk through the gates, collect apples, mow between the trees, water the dry path, the whole garden is finished.
        /// The pixel accuracy of occlusion through the new gate, the frame time with the whole garden visible and the gain of the grass mesh LOD
        /// with the image difference are measured.
        /// </summary>
        static IEnumerator U14Areas(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            LawnTasks tasks = session.Tasks;
            GrassField grass = game.Grass;
            Gate toOrchard = YardBuilder.GateTo(3), toGarden = YardBuilder.GateTo(4);
            int definitionErrors = YardBuilder.Definition.Validate().Count;
            s.Check("start", toOrchard != null && toGarden != null && !toOrchard.IsOpen && !toGarden.IsOpen && !tasks.IsAreaUnlocked(3)
                                 && !tasks.IsAreaUnlocked(4) && session.Apples.Count == 0 && definitionErrors == 0 && tasks.AreaIds.Count == 4,
                $"{tasks.AreaIds.Count} areas, {tasks.Total} patches, definition errors {definitionErrors}, apples {session.Apples.Count}");

            IEnumerator FinishArea(int area)
            {
                foreach (PatchProgress patch in tasks.Patches)
                {
                    if (patch.Area == area && !patch.Finished)
                        session.CompletePatch(patch.Id);
                    yield return null;
                }
            }

            IEnumerator Look(string shot, Vector3 feet, Vector3 target, float raise)
            {
                Vector2 view = LookAt(feet, target);
                yield return s.Pose(feet, view.x, view.y - raise);
                yield return s.Shot(shot);
            }

            float ChangedFraction(Color32[] a, Color32[] b, int threshold, out float meanDiff)
            {
                int changed = 0;
                double sum = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    int d = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                    sum += d / 3.0;
                    if (d > threshold) changed++;
                }
                meanDiff = (float)(sum / a.Length);
                return changed / (float)a.Length;
            }

            // 1) When the back and side yards are finished the orchard's gate opens and apples drop
            s.Equip(Tool.HandSlot);
            yield return FinishArea(1);
            yield return FinishArea(2);
            s.Check("orchard_gate_opening", tasks.IsAreaUnlocked(3) && (toOrchard.IsOpening || toOrchard.IsOpen) && !tasks.IsAreaUnlocked(4)
                                             && session.Apples.Count > 0 && session.Hud.BannerText == "Gate opened!",
                $"gate {toOrchard.Progress:F2}, apples {session.Apples.Count}, banner '{session.Hud.BannerText}'");
            yield return s.Pose(new Vector3(24f, 0.05f, -9f), 180f, 3f);
            yield return s.Seconds(Gate.OpenSeconds);
            s.Check("target_orchard", session.Hud.Objective.Contains("Orchard"), session.Hud.Objective);
            yield return s.Shot("u14_orchard_gate");

            // Occlusion through the new gate: looking south from the side yard the orchard's grass is only visible through the opening
            yield return s.Pose(new Vector3(26.5f, 0.05f, -6f), 200f, 2f, settleFrames: 20);
            int occluded = grass.OccludedChunks;
            Time.timeScale = 0f;
            YardVisibility.Enabled = false;
            yield return s.Shot("u14_from_gate_unoccluded", settleFrames: 3);
            Color32[] withoutOcclusion = s.LastShotPixels;
            YardVisibility.Enabled = true;
            yield return s.Shot("u14_from_gate", settleFrames: 3, allowSame: true);
            Time.timeScale = 1f;
            float occlusionChange = ChangedFraction(s.LastShotPixels, withoutOcclusion, 12, out _);
            s.Check("occlusion_orchard_gate", occluded > 0 && occlusionChange < 0.0005f, $"{occluded} chunks occluded, changed pixels {occlusionChange * 100f:F3}%");

            // 2) Through the gate into the orchard
            yield return s.Pose(new Vector3(24f, 0.05f, -13.2f), 180f, 4f);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 1.8f);
            yield return s.Frames(2);
            s.Check("entered_orchard", session.CurrentArea == 3 && p.transform.position.z < -15.5f
                                             && session.Hud.AreaTitle.StartsWith("Orchard") && session.Hud.AreaTitle.EndsWith("Area 3"),
                $"z {p.transform.position.z:F2}, area {session.CurrentArea}, card '{session.Hud.AreaTitle}'");
            yield return Look("u14_orchard_overview", new Vector3(22.5f, 0.05f, -17f), new Vector3(0f, 0f, -22.5f), 2f);
            yield return Look("u14_apple_tree", new Vector3(8.2f, 0.05f, -22.4f), new Vector3(5.5f, 0f, -25.2f), 20f);

            // 3) Collecting apples: go under the tree
            Vector3[] apples = session.Apples.Capture();
            Vector3 apple = apples[0];
            Vector3 tree = session.Apples.Trees.OrderBy(t => (t.Position - apple).sqrMagnitude).First().Position;
            Vector3 away = (apple - tree).normalized;
            Vector2 down = LookAt(apple + away * 1.6f, apple);
            yield return s.Pose(apple + away * 1.6f + Vector3.up * 0.05f, down.x, down.y);
            yield return s.Shot("u14_apples");
            int points = session.Wallet.Points, count = session.Apples.Count;
            yield return s.Pose(apple + away * 0.3f + Vector3.up * 0.05f, down.x, down.y, settleFrames: 3);
            int picked = count - session.Apples.Count;
            s.Check("apple_collected", picked >= 1 && session.Wallet.Points >= points + Rewards.Apple * picked && session.Days.Today.Apples >= 1,
                $"{picked} apples, points {points} → {session.Wallet.Points}, left {session.Apples.Count}");

            // 4) Mowing between the tree rows
            game.Lawn.RegrowAll();
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(-24f, 0.05f, -22.5f), 90f, MovementProfile.Mowing.EquipPitch);
            int cutBefore = game.Lawn.TotalCutCells;
            float startX = p.transform.position.x;
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 4f);
            yield return s.Frames(3);
            s.Check("mowing_between_trees", p.transform.position.x - startX > 4f && game.Lawn.TotalCutCells > cutBefore + 150 && session.ActivePatch?.Area == 3,
                $"{p.transform.position.x - startX:F1} m travelled, {game.Lawn.TotalCutCells - cutBefore} cells mowed, patch {session.ActivePatch?.Name}");
            yield return s.Shot("u14_orchard_mowing");

            // 5) When the orchard is finished, the vegetable garden's gate
            yield return FinishArea(3);
            s.Check("vegetable_gate_opening", tasks.IsAreaUnlocked(4) && (toGarden.IsOpening || toGarden.IsOpen) && session.Hud.BannerText == "Gate opened!",
                $"gate {toGarden.Progress:F2}, banner '{session.Hud.BannerText}'");
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-24f, 0.05f, -16.8f), 0f, 4f);
            yield return s.Seconds(Gate.OpenSeconds);
            yield return s.Shot("u14_vegetable_gate");
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 1.8f);
            yield return s.Frames(2);
            s.Check("entered_vegetable_garden", session.CurrentArea == 4 && p.transform.position.z > -14.5f
                                             && session.Hud.AreaTitle.StartsWith("Vegetable garden") && session.Hud.AreaTitle.EndsWith("Area 4"),
                $"z {p.transform.position.z:F2}, area {session.CurrentArea}, card '{session.Hud.AreaTitle}'");
            yield return Look("u14_vegetable_overview", new Vector3(-23.2f, 0.05f, -12.5f), new Vector3(-24f, 0f, 2f), 4f);
            yield return Look("u14_beds", new Vector3(-22.4f, 0.05f, 0.4f), new Vector3(-24f, 0f, 4.5f), 6f);
            yield return Look("u14_trellis_barrel", new Vector3(-25.2f, 0.05f, 8.2f), new Vector3(-27.4f, 0f, 13f), 8f);
            yield return Look("u14_wheelbarrow", new Vector3(-22.6f, 0.05f, -9.4f), new Vector3(-20.4f, 0f, -11.4f), 16f);

            // 6) Watering the dry path
            PatchProgress dryPath = tasks.Patches.First(patch => patch.Area == 4 && patch.HasWater && patch.Name == "West path");
            float waterBefore = dryPath.WaterFraction;
            s.Equip(Tool.HoseSlot);
            Vector2 aim = LookAt(new Vector3(-28.9f, 0.05f, -1.8f), new Vector3(-28.9f, 0f, 0.6f));
            yield return s.Pose(new Vector3(-28.9f, 0.05f, -1.8f), aim.x, aim.y);
            yield return s.Hold(ScenarioContext.Input(use: true), 3f);
            yield return s.Frames(3);
            s.Check("dry_path_watered", dryPath.WaterFraction > waterBefore + 0.01f, $"{dryPath.Name}: {waterBefore * 100f:0}% → {dryPath.WaterFraction * 100f:0}%");
            yield return s.Shot("u14_watering");

            // 7) The whole garden (the regrowth in the mowing step had reopened the other areas too: those first, the vegetable garden last)
            session.Finds.Restore(session.Finds.CaptureCollected(), null);
            yield return FinishArea(1);
            yield return FinishArea(2);
            yield return FinishArea(3);
            yield return FinishArea(4);
            s.Check("garden_done", tasks.AllFinished && session.Hud.BannerText == "The whole garden is done!", $"{tasks.FinishedCount}/{tasks.Total}, '{session.Hud.BannerText}'");

            // 8) Frame time and the grass LOD: the whole garden from above (no occlusion), along the orchard from the ground
            s.Equip(Tool.HandSlot);
            game.Lawn.RegrowAll();
            IEnumerator CompareLod(string name, Vector3 feet, float yaw, float pitch, float raise, float targetMs)
            {
                p.CameraJitter = Vector3.up * raise;
                yield return s.Pose(feet, yaw, pitch, settleFrames: 20);
                PerfStats on = default, off = default;
                yield return s.MeasurePerf(240, value => on = value);
                long trianglesOn = grass.DrawnTriangles;
                int far = grass.DrawnFarChunks, drawn = grass.DrawnChunks;
                GrassField.MeshLodEnabled = false;
                yield return s.Frames(5);
                yield return s.MeasurePerf(240, value => off = value);
                long trianglesOff = grass.DrawnTriangles;

                Time.timeScale = 0f;
                yield return s.Shot($"u14_{name}_no_lod", settleFrames: 3);
                Color32[] without = s.LastShotPixels;
                GrassField.MeshLodEnabled = true;
                yield return s.Shot($"u14_{name}", settleFrames: 3, allowSame: true);
                Time.timeScale = 1f;
                float changed = ChangedFraction(s.LastShotPixels, without, 60, out float meanDiff);
                p.CameraJitter = Vector3.zero;
                float saved = 1f - trianglesOn / (float)Mathf.Max(1, trianglesOff);
                // Hard limit 60 fps (measurements depending on the power profile vary); the U14 target (< 6 ms, RTX 4060 normal profile) is reported in the detail
                s.Check($"lod_{name}", on.FrameMs < 16.6f && saved > 0.2f && meanDiff < 3f,
                    $"LOD on {on.FrameMs:F2} ms (target < {targetMs:F0} ms: {(on.FrameMs < targetMs ? "held" : "missed")}) ↔ off {off.FrameMs:F2} ms; grass triangles {trianglesOn / 1e6f:F2} M ↔ {trianglesOff / 1e6f:F2} M ({saved * 100f:0}% fewer), " +
                    $"{far}/{drawn} chunks with the far mesh; image difference avg. {meanDiff:F2}/255, clearly changed pixels {changed * 100f:F2}%");
            }
            yield return CompareLod("whole_garden", new Vector3(0f, 0.05f, -27.5f), 0f, 30f, 16f, 6f);
            yield return CompareLod("along_orchard", new Vector3(-28.5f, 0.05f, -22.5f), 90f, 3f, 0f, 6f);
        }

        /// <summary>Detailed models of the garden props: close framings and the frame time once the props are added.</summary>
        static IEnumerator U11Props(ScenarioContext s)
        {
            s.Equip(Tool.HandSlot);

            IEnumerator Look(string shot, Vector3 feet, Vector3 target, float raise)
            {
                Vector2 view = LookAt(feet, target);
                yield return s.Pose(feet, view.x, view.y - raise);
                yield return s.Shot(shot);
            }

            yield return Look("u11_house", new Vector3(0f, 0.05f, 6.5f), new Vector3(0f, 0f, 15.5f), 16f);
            yield return Look("u11_veranda", new Vector3(2.8f, 0.05f, 11.6f), new Vector3(1.2f, 0f, 14f), 18f);
            yield return Look("u11_shed", new Vector3(-11.4f, 0.05f, 5.2f), new Vector3(-13f, 0f, 9f), 20f);
            yield return Look("u11_shed_side", new Vector3(-16.8f, 0.05f, 6.6f), new Vector3(-14.4f, 0f, 9.6f), 16f);
            yield return Look("u11_flowerbed", new Vector3(11.6f, 0.05f, 10.4f), new Vector3(12.2f, 0f, 13.8f), 8f);
            yield return Look("u11_tree", new Vector3(15.6f, 0.05f, -9.4f), new Vector3(12.6f, 0f, -5.2f), 16f);
            yield return Look("u11_fence", new Vector3(-14.2f, 0.05f, -11.4f), new Vector3(-17.6f, 0f, -14.6f), 6f);
            yield return Look("u11_birdbath", new Vector3(22.7f, 0.05f, -5.3f), new Vector3(24f, 0f, -3.5f), 2f);
            yield return Look("u11_compost", new Vector3(26f, 0.05f, 10.1f), new Vector3(27.5f, 0f, 12.5f), 4f);
            yield return Look("u11_gate", new Vector3(15.4f, 0.05f, -1.8f), new Vector3(18f, 0f, 0f), 18f);

            // Overall framing and frame time with all props
            yield return s.Pose(new Vector3(-3f, 0.05f, -13.4f), 14f, 4f);
            PerfStats stats = default;
            yield return s.MeasurePerf(240, value => stats = value);
            s.Check("frame_time_with_objects", stats.FrameMs < 16.6f, stats.ToString());
            yield return s.Shot("u11_overview");
        }

        /// <summary>
        /// U10 progression: earning points, finding lost items in the grass, buying upgrades in the workshop, the end-of-day screen and night
        /// regrowth, a rainy night, a save/load round trip, the collection page.
        /// </summary>
        static IEnumerator U10Progress(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            LawnTasks tasks = session.Tasks;
            LawnState lawn = game.Lawn;
            var mower = (MowerTool)p.Tools.FindSlot(Tool.MowerSlot);
            var hose = (HoseTool)p.Tools.FindSlot(Tool.HoseSlot);

            List<Vector3> hidden = session.Finds.HiddenPositions();
            s.Check("start", session.Days.Day == 1 && session.Wallet.Points == 0 && session.Pattern != null && hidden.Count == LostItems.MaxActive
                    && session.Hud.DayLine.Contains("Day 1"), $"day {session.Days.Day}, points {session.Wallet.Points}, pattern {session.Pattern?.Name}, hidden items {hidden.Count}");

            // 1) Mowing earns points
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(-8f, 0.05f, -12f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 3f);
            int mowedPoints = session.Wallet.Points;
            s.Check("mowing_points", mowedPoints >= 3, $"{mowedPoints} points for 3 s of mowing, today {session.Days.Today.MowedSquareMeters:F1} m²");

            // 2) When the mower blade passes over a hidden item, the item pops out and enters the collection
            // Pick the one far from the fences (the player must be able to stand 1.2 m south of the item)
            Vector3 item = hidden.Find(h => h.z > -12f && h.z < 13f && Mathf.Abs(h.x) < 15f);
            if (item == Vector3.zero) item = hidden[0];
            yield return s.Pose(new Vector3(item.x, 0.05f, item.z - MowerTool.ForwardOffset - 0.03f), 0f, MovementProfile.Mowing.EquipPitch, settleFrames: 3);
            yield return s.Frames(3);
            s.Check("item_found", session.Finds.Collected.Count == 1 && session.Hud.BannerText.StartsWith("Found:")
                    && session.Wallet.Points >= mowedPoints + Rewards.Find, $"'{session.Hud.BannerText}', points {mowedPoints} → {session.Wallet.Points}");
            yield return s.Seconds(0.25f);
            yield return s.Shot("u10_item_found");

            // 3) Workshop: wide deck and sprinkler head
            session.Wallet.Add(3000f);
            game.Menu.Open();
            game.Menu.ShowPage(UI.GameMenu.Page.Workshop);
            yield return s.Shot("u10_workshop");
            bool deck1 = session.Upgrades.TryBuy(UpgradeId.WideDeck, session.Wallet);
            bool deck2 = session.Upgrades.TryBuy(UpgradeId.WideDeck, session.Wallet);
            bool rain = session.Upgrades.TryBuy(UpgradeId.RainHead, session.Wallet);
            game.Menu.ShowPage(UI.GameMenu.Page.Workshop);
            yield return s.Shot("u10_workshop_bought", allowSame: true);
            game.Menu.Close();
            s.Check("upgrades", deck1 && deck2 && rain && Mathf.Approximately(mower.CurrentBladeRadius, MowerTool.BladeRadius * 1.3f) && hose.ModeCount == 3,
                $"blade {mower.CurrentBladeRadius:F3} m, hose modes {hose.ModeCount}, points left {session.Wallet.Points}");
            yield return s.Pose(new Vector3(-3f, 0.05f, -12.5f), 0f, MovementProfile.Mowing.EquipPitch - 6f);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 1.2f);
            yield return s.Shot("u10_wide_deck");
            s.Equip(Tool.HoseSlot);
            hose.SetMode(2);
            Vector2 look = LookAt(new Vector3(3f, 0f, -9f), new Vector3(5f, 0f, -6f));
            yield return s.Pose(new Vector3(3f, 0.05f, -9f), look.x, look.y);
            yield return s.Hold(ScenarioContext.Input(use: true), 1f);
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Shot("u10_sprinkler");
            s.SetInput(PlayerInputState.None);
            s.Check("sprinkler_mode", hose.Mode.Name == SprayMode.Rain.Name && hose.DisplayName == "Hose / Rain", hose.DisplayName);

            // 4) End of day: end the day once all the work is done; the grass grows at night, patches need upkeep
            foreach (PatchProgress patch in tasks.Patches)
            {
                if (patch.Area == 1 && !patch.Finished)
                    session.CompletePatch(patch.Id);
                yield return null;
            }
            int doneBefore = tasks.FinishedIn(1);
            session.BeginEndDay();
            yield return s.Seconds(YardSession.NightFadeIn + 0.6f);
            s.Check("night_screen", session.IsNight && session.Hud.NightAlpha > 0.95f && session.Hud.NightText.Contains("Day 1 is over"), session.Hud.NightText.Replace("\n", " / "));
            yield return s.Shot("u10_night");
            for (float t = 0f; t < 8f && session.IsNight; t += Time.unscaledDeltaTime)
                yield return null;
            yield return s.Frames(2);
            s.Check("second_day", !session.IsNight && session.Days.Day == 2 && Time.timeScale == 1f && tasks.FinishedIn(1) < doneBefore
                    && session.Hud.DayLine.Contains("Day 2") && tasks.IsAreaUnlocked(2),
                $"day {session.Days.Day}, area 1 finished {doneBefore} → {tasks.FinishedIn(1)}, objective '{session.Hud.Objective}'");
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-3f, 0.05f, -13.4f), 14f, 6f);
            yield return s.Shot("u10_second_day");

            // 5) Rainy night: dry spots get watered by themselves, the grass is wet in the morning
            int nights = 0;
            while (nights++ < 16 && !DayCycle.RainsAfter(session.Days.Day))
                session.EndDayNow();
            NightReport rainy = session.EndDayNow();
            DrySpot spot = YardBuilder.DrySpots[0];
            lawn.Grid.TryGetCell(spot.Center, out int sx, out int sz);
            int spotCell = lawn.Grid.Index(sx, sz);
            s.Check("rainy_night", rainy.Rain && lawn.IsWatered(spotCell) && lawn.WetnessAt(spotCell) > 0.4f,
                $"rain on night {rainy.FinishedDay}: {rainy.Rain}, spot watered: {lawn.IsWatered(spotCell)}, wetness {lawn.WetnessAt(spotCell):F2}");
            yield return s.Pose(new Vector3(spot.Center.x - 2.5f, 0.05f, spot.Center.y - 3f), 40f, 18f, settleFrames: 2);
            yield return s.Shot("u10_rainy_morning");

            // 6) Save → mess up → load
            int day = session.Days.Day, points = session.Wallet.Points, collectedCount = session.Finds.Collected.Count;
            session.Save();
            session.EndDayNow();
            session.Wallet.TrySpend(session.Wallet.Points);
            bool loaded = session.Load(out string error);
            s.Check("progress_save", loaded && session.Days.Day == day && session.Wallet.Points == points && session.Upgrades.Level(UpgradeId.WideDeck) == 2
                    && session.Finds.Collected.Count == collectedCount && collectedCount >= 1 && Mathf.Approximately(mower.CurrentBladeRadius, MowerTool.BladeRadius * 1.3f) && hose.ModeCount == 3,
                $"{error} day {session.Days.Day}/{day}, points {session.Wallet.Points}/{points}, collection {session.Finds.Collected.Count}/{collectedCount}");

            // 7) Collection page
            game.Menu.Open();
            game.Menu.ShowPage(UI.GameMenu.Page.Collection);
            yield return s.Shot("u10_collection");
            s.Check("collection_page", game.Menu.StatusText.Contains($"{collectedCount} / 12"), game.Menu.StatusText);
            game.Menu.Close();
        }

        /// <summary>U10 pattern mode: guide tint; mowing the stripes in the requested direction completes the task (stripes and checker).</summary>
        static IEnumerator U10Pattern(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            LawnState lawn = game.Lawn;
            LawnTasks tasks = session.Tasks;

            // This scenario only looks at the pattern: hidden items found while mowing must not override the banner
            session.Finds.Restore(null, null);
            int stripesPatch = lawn.Layout.PatchAt(new Vector2(1f, -3f));
            session.ForcePattern(PatternKind.StripesNorthSouth, stripesPatch);
            PatchProgress info = tasks.Get(stripesPatch);
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(info.Centroid.x, 0.05f, info.Centroid.y - 1f), 0f, 30f);
            yield return s.Seconds(0.6f);
            s.Check("guide", session.Overlay.PatternStrength > 0.9f && session.Hud.PatternLine.Contains("North-south"), $"strength {session.Overlay.PatternStrength:F2}, '{session.Hud.PatternLine}'");
            yield return s.Shot("u10_pattern_guide");

            int before = session.Wallet.Points;
            PatternTask task = session.Pattern;
            MowPattern(lawn, info, task);
            session.EvaluatePattern();
            string banner = session.Hud.BannerText;
            yield return s.Frames(2);
            s.Check("pattern_done", task.Completed && session.Wallet.Points >= before + task.Reward && banner == "Pattern done!",
                $"{task.Score * 100f:0}% (target {task.Goal * 100f:0}%), points {before} → {session.Wallet.Points}, '{banner}'");
            yield return s.Pose(new Vector3(info.Centroid.x - 1f, 0.05f, info.Centroid.y - 5.5f), 5f, 24f);
            yield return s.Shot("u10_pattern_stripes");

            int checkerPatch = lawn.Layout.PatchAt(new Vector2(-7f, -4f));
            session.ForcePattern(PatternKind.Checker, checkerPatch);
            PatchProgress checkerInfo = tasks.Get(checkerPatch);
            MowPattern(lawn, checkerInfo, session.Pattern);
            session.EvaluatePattern();
            s.Check("checker_done", session.Pattern.Completed, $"{session.Pattern.Score * 100f:0}% (target {session.Pattern.Goal * 100f:0}%)");
            yield return s.Pose(new Vector3(checkerInfo.Centroid.x + 1f, 0.05f, checkerInfo.Centroid.y - 6f), -5f, 22f);
            yield return s.Shot("u10_checker");
        }

        /// <summary>Mows the patch in north-south stripes 10 cm apart, every cell in the direction the pattern wants.</summary>
        static void MowPattern(LawnState lawn, PatchProgress info, PatternTask task)
        {
            float reach = info.Radius + 1f;
            for (float x = info.Centroid.x - reach; x <= info.Centroid.x + reach; x += 0.1f)
            {
                // The requested direction can change along a stripe (checker): mow in 0.3 m pieces
                for (float z = info.Centroid.y - reach; z < info.Centroid.y + reach; z += 0.3f)
                {
                    Vector2 want = task.Desired(new Vector2(x, z + 0.15f));
                    Vector2 a = new Vector2(x, z), b = new Vector2(x, z + 0.3f);
                    if (want.y < 0f) (a, b) = (b, a);
                    lawn.CutSwept(a, b, 0.08f, want, true);
                }
            }
        }

        /// <summary>U10 tools and gamepad: close views of the detailed models, merged drawing, gamepad button names and rumble.</summary>
        static IEnumerator U10Tools(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;

            // Showcase: the models in a mown corner, on the ground (mower and trimmer) and hanging (hose)
            LawnState lawn = game.Lawn;
            for (float x = -3.5f; x <= 3.8f; x += 0.2f)
                lawn.CutSwept(new Vector2(x, -7.8f), new Vector2(x, -4.4f), 0.15f, Vector2.up, true);
            var showcase = new GameObject("Showroom").transform;
            Transform mowerSpot = new GameObject("MowerShowcase").transform;
            mowerSpot.SetParent(showcase, false);
            mowerSpot.position = new Vector3(-1.8f, 0f, -6.2f);
            mowerSpot.rotation = Quaternion.Euler(0f, 150f, 0f);
            MowerModel mower = MowerModel.Build(mowerSpot);
            Transform trimmerSpot = new GameObject("TrimmerShowcase").transform;
            trimmerSpot.SetParent(showcase, false);
            trimmerSpot.position = new Vector3(1.4f, 0.16f, -6.0f);
            trimmerSpot.rotation = Quaternion.Euler(0f, 70f, 0f);
            TrimmerModel trimmer = TrimmerModel.Build(trimmerSpot);
            trimmer.Spinner.gameObject.SetActive(false);
            Transform hoseSpot = new GameObject("HoseShowcase").transform;
            hoseSpot.SetParent(showcase, false);
            hoseSpot.position = new Vector3(2.8f, 0.72f, -6.6f);
            hoseSpot.rotation = Quaternion.Euler(0f, 230f, 0f);
            HoseModel.Build(hoseSpot);
            yield return s.Frames(2);

            int renderers = 0;
            foreach (MeshRenderer r in mowerSpot.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled) renderers++;
            s.Check("mower_detail", mower.TriangleCount >= 40000 && renderers <= 8, $"{mower.PartCount} parts, {mower.TriangleCount} triangles, {renderers} renderers once merged");

            s.Equip(Tool.HandSlot);
            Vector3 overview = new Vector3(0.4f, 0.05f, -9.6f);
            Vector2 view = LookAt(overview, new Vector3(0.4f, 0f, -6.2f));
            yield return s.Pose(overview, view.x, view.y - 6f);
            yield return s.Shot("u10_tools_page");
            Vector3 mowerEye = mowerSpot.position + new Vector3(1.1f, 0.05f, -1.2f);
            view = LookAt(mowerEye, mowerSpot.position);
            yield return s.Pose(mowerEye, view.x, view.y - 8f);
            yield return s.Shot("u10_mower_close");
            mower.SetDeckScale(1.3f);
            yield return s.Shot("u10_mower_wide_deck");
            Vector3 toolsEye = new Vector3(2.0f, 0.05f, -7.9f);
            view = LookAt(toolsEye, new Vector3(2.0f, 0f, -6.2f));
            yield return s.Pose(toolsEye, view.x, view.y - 10f);
            yield return s.Shot("u10_trimmer_hose_close");
            Object.Destroy(showcase.gameObject);

            // Gamepad button names
            InputReader.ForceScheme(InputScheme.Gamepad);
            s.Equip(Tool.HoseSlot);
            var hose = (HoseTool)p.Tools.FindSlot(Tool.HoseSlot);
            yield return s.Pose(new Vector3(0f, 0.05f, -12f), 0f, 20f);
            s.Check("gamepad_prompts", hose.Prompt.StartsWith("RT (hold)") && InputGlyphs.ToolbarHint.StartsWith("LB"), hose.Prompt);
            yield return s.Shot("u10_gamepad_prompts");
            InputReader.ForceScheme(InputScheme.KeyboardMouse);
            s.Check("keyboard_prompts", hose.Prompt.StartsWith("Left click"), hose.Prompt);

            // Rumble: grows stronger when the mower is loaded in long grass
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(8f, 0.05f, -12.5f), 0f, MovementProfile.Mowing.EquipPitch);
            // Standing still the blade only mows the circle under it; let the load fade, then drive into long grass
            yield return s.Seconds(1.5f);
            Vector2 idle = game.Rumble.Compute();
            Vector2 strongest = Vector2.zero;
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2f, _ =>
            {
                Vector2 now = game.Rumble.Compute();
                if (now.x > strongest.x) strongest = now;
            });
            s.Check("vibration", strongest.x > idle.x + 0.05f && game.Rumble.Motors == Vector2.zero,
                $"idle {idle.x:F2} → under load {strongest.x:F2} (not sent to the gamepad in scenarios: {game.Rumble.Motors})");
        }

        /// <summary>
        /// U9 sounds: the mix is recorded (mix.wav; speakers muted). Mower engine, labouring under load, trimmer revs,
        /// hose and spray mode, footsteps, birds, feedback sounds (tool, patch/area finished, gate, weed), going quiet on a break
        /// and the master volume setting.
        /// </summary>
        static IEnumerator U9Audio(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            Audio.AudioDirector audio = game.Audio;
            LawnTasks tasks = session.Tasks;

            bool listener = p.WorldCamera.GetComponent<AudioListener>() != null;
            s.Check("listener", listener && audio.Tap != null && audio.Tap.MuteOutput, $"listener {(listener ? "yes" : "none")}, output {(audio.Tap.MuteOutput ? "muted" : "on")}, {AudioSettings.outputSampleRate} Hz");
            audio.Tap.StartRecording();

            // 1) Mower: runs in hand; in long grass, load, the revs drop and the heavy layer comes in
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(-8f, 0.05f, -12f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Seconds(1.2f);
            float idleVolume = audio.EngineVolume, idlePitch = audio.EnginePitch;
            s.Check("motor_running", idleVolume > 0.25f, $"volume {idleVolume:F2}, pitch {idlePitch:F2}");
            float maxLoaded = 0f, minPitch = 9f;
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 3f, _ =>
            {
                maxLoaded = Mathf.Max(maxLoaded, audio.EngineLoadedVolume);
                minPitch = Mathf.Min(minPitch, audio.EnginePitch);
            });
            s.Check("motor_under_load", maxLoaded > 0.12f && minPitch < idlePitch - 0.02f,
                $"load layer at most {maxLoaded:F2}, pitch {idlePitch:F2} → {minPitch:F2}");

            // 2) Trimmer: low at idle, revs up on the trigger
            s.Equip(Tool.TrimmerSlot);
            yield return s.Pose(new Vector3(-4f, 0.05f, -10f), 0f, 30f);
            yield return s.Seconds(0.8f);
            float trimmerIdle = audio.TrimmerPitch, trimmerIdleVolume = audio.TrimmerVolume;
            float spun = 0f;
            yield return s.Hold(ScenarioContext.Input(use: true), 1.5f, _ => spun = Mathf.Max(spun, audio.TrimmerPitch));
            yield return s.Seconds(0.8f);
            s.Check("trimmer_rpm", trimmerIdle < 0.65f && spun > 0.95f && audio.TrimmerPitch < 0.75f && trimmerIdleVolume > 0.05f,
                $"idle {trimmerIdle:F2} (volume {trimmerIdleVolume:F2}) → on trigger {spun:F2} → released {audio.TrimmerPitch:F2}");

            // 3) Hose: a wide hiss, a narrow jet with R
            s.Equip(Tool.HoseSlot);
            Vector2 look = LookAt(new Vector3(3f, 0f, -9f), new Vector3(5f, 0f, -6f));
            yield return s.Pose(new Vector3(3f, 0.05f, -9f), look.x, look.y);
            float wide = 0f;
            yield return s.Hold(ScenarioContext.Input(use: true), 1.2f, _ => wide = Mathf.Max(wide, audio.WaterVolume));
            bool wideMode = !audio.NarrowWaterAudible;
            int clicks = audio.PlayCount(Audio.SoundBank.HoseClick);
            yield return s.Hold(ScenarioContext.Input(alt: true, use: true), 1.2f);
            s.Check("hose_sound", wide > 0.25f && wideMode && audio.NarrowWaterAudible && audio.PlayCount(Audio.SoundBank.HoseClick) == clicks + 1,
                $"wide {wide:F2}, narrow mode audible: {audio.NarrowWaterAudible}, spray click {audio.PlayCount(Audio.SoundBank.HoseClick) - clicks}");
            yield return s.Seconds(0.6f);
            s.Check("hose_goes_quiet", audio.WaterVolume < 0.03f, $"released {audio.WaterVolume:F3}");

            // 4) Footsteps
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-12f, 0.05f, -8f), 0f, 5f);
            int steps = audio.FootstepsPlayed;
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 3f);
            int walked = audio.FootstepsPlayed - steps;
            s.Check("footsteps", walked >= 8 && walked <= 20, $"{walked} steps in 3 s of walking");

            // 5) Feedback: tool switch, patch finished, weed pulling, area finished and gate
            s.Check("tool_sound", audio.PlayCount(Audio.SoundBank.ToolSwitch) >= 3, $"{audio.PlayCount(Audio.SoundBank.ToolSwitch)} tool switch sounds");
            int weedy = 0;
            foreach (PatchProgress patch in tasks.Patches)
            {
                if (patch.Area == 1 && patch.WeedsTotal > 0 && !patch.Finished)
                {
                    weedy = patch.Id;
                    break;
                }
            }
            session.CompletePatch(weedy);
            yield return s.Frames(2);
            s.Check("patch_and_weed_sound", audio.PlayCount(Audio.SoundBank.ChimePatch) == 1 && audio.PlayCount(Audio.SoundBank.WeedPop) >= 1,
                $"patch chime {audio.PlayCount(Audio.SoundBank.ChimePatch)}, weed pull {audio.PlayCount(Audio.SoundBank.WeedPop)}");
            yield return s.Seconds(1.5f);
            foreach (PatchProgress patch in tasks.Patches)
            {
                if (patch.Area == 1 && !patch.Finished)
                    session.CompletePatch(patch.Id);
                yield return null;
            }
            s.Check("area_and_gate_sound", audio.PlayCount(Audio.SoundBank.ChimeArea) == 1 && audio.PlayCount(Audio.SoundBank.GateCreak) == 1,
                $"area sound {audio.PlayCount(Audio.SoundBank.ChimeArea)}, gate {audio.PlayCount(Audio.SoundBank.GateCreak)}, patch chime {audio.PlayCount(Audio.SoundBank.ChimePatch)} (the area's last one does not play)");
            yield return s.Seconds(3f);

            // 6) Ambience: wind and birds
            yield return s.Seconds(6f);
            s.Check("ambience", audio.WindVolume > 0.1f && audio.BirdsSung >= 3, $"wind {audio.WindVolume:F2}, {audio.BirdsSung} bird calls");

            // 7) Break: game sounds stop, menu sounds play; master volume setting
            game.Menu.Open();
            yield return s.Seconds(0.3f);
            bool pausedAudio = AudioListener.pause;
            game.Menu.Close();
            yield return s.Frames(2);
            s.Check("quiet_during_break", pausedAudio && !AudioListener.pause && audio.PlayCount(Audio.SoundBank.MenuOpen) == 1 && audio.PlayCount(Audio.SoundBank.MenuClose) == 1,
                $"paused on break: {pausedAudio}, menu sounds {audio.PlayCount(Audio.SoundBank.MenuOpen)}/{audio.PlayCount(Audio.SoundBank.MenuClose)}");
            GameSettings.SetMasterVolume(0f);
            yield return s.Frames(2);
            float muted = AudioListener.volume;
            GameSettings.SetMasterVolume(1f);
            yield return s.Frames(2);
            s.Check("master_volume", muted == 0f && Mathf.Approximately(AudioListener.volume, 1f), $"0 → {muted:F1}, back {AudioListener.volume:F1}");

            // 8) Mix: no clipping, at an audible level
            float[] mix = audio.Tap.StopRecording();
            audio.Tap.Levels(out float peak, out float rms);
            string path = System.IO.Path.Combine(s.OutputDirectory, "mix.wav");
            Audio.Synth.WriteWav(path, mix, audio.Tap.Channels, audio.Tap.SampleRate);
            float seconds = mix.Length / (float)Mathf.Max(1, audio.Tap.Channels * audio.Tap.SampleRate);
            s.Check("mix", seconds > 15f && peak < 0.99f && rms > 0.01f,
                $"{seconds:F1} s recorded, peak {peak:F2}, RMS {rms:F3} → {path}");
        }

        /// <summary>
        /// U9 performance tour: frame time in heavy framings (with the CPU main thread and draw counts), the gain and accuracy of
        /// the occlusion between areas (time is frozen and two screenshots with occlusion on/off are compared pixel by pixel).
        /// </summary>
        static IEnumerator U9Perf(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            GrassField grass = game.Grass;
            Rect annex = YardBuilder.Areas[1].Bounds;

            IEnumerator Measure(string name, Vector3 feet, float yaw, float pitch, PlayerInputState input = default, int frames = 240)
            {
                yield return s.Pose(feet, yaw, pitch, settleFrames: 20);
                if (input.Move != Vector2.zero || input.UseHeld)
                {
                    s.SetInput(input);
                    yield return s.Seconds(0.6f);
                }
                PerfStats stats = default;
                yield return s.MeasurePerf(frames, value => stats = value);
                s.SetInput(PlayerInputState.None);
                s.Check($"perf_{name}", stats.FrameMs < 16.6f, $"{stats} · {grass.DrawnTufts / 1000f:F0}k tufts, {grass.DrawnChunks} chunks drawn, {grass.OccludedChunks} occluded, {session.Flowers.DrawnCount} flowers");
            }

            // Occlusion on/off in the same framing: times and (in frozen time) the image difference
            IEnumerator Compare(string name, Vector3 feet, float yaw, float pitch)
            {
                yield return s.Pose(feet, yaw, pitch, settleFrames: 20);
                PerfStats on = default, off = default;
                yield return s.MeasurePerf(240, value => on = value);
                int occluded = grass.OccludedChunks, annexOn = grass.CountDrawnIn(annex);
                YardVisibility.Enabled = false;
                yield return s.Frames(5);
                yield return s.MeasurePerf(240, value => off = value);
                int annexOff = grass.CountDrawnIn(annex);

                // Image comparison: when time stops, the wind, clouds and flowers stop too
                Time.timeScale = 0f;
                yield return s.Shot($"u9_{name}_unoccluded", settleFrames: 3);
                Color32[] without = s.LastShotPixels;
                YardVisibility.Enabled = true;
                yield return s.Shot($"u9_{name}", settleFrames: 3, allowSame: true);
                Color32[] with = s.LastShotPixels;
                Time.timeScale = 1f;
                int changed = 0;
                for (int i = 0; i < with.Length; i++)
                {
                    if (Mathf.Abs(with[i].r - without[i].r) + Mathf.Abs(with[i].g - without[i].g) + Mathf.Abs(with[i].b - without[i].b) > 12)
                        changed++;
                }
                float changedFraction = changed / (float)with.Length;
                s.Check($"occlusion_{name}", changedFraction < 0.0005f && occluded > 0,
                    $"{occluded} chunks occluded (drawn in the side yard {annexOn}/{annexOff}), changed pixels {changedFraction * 100f:F3}%; " +
                    $"on {on.FrameMs:F2} ms ↔ off {off.FrameMs:F2} ms");
            }

            s.Equip(Tool.HandSlot);
            yield return Measure("overview", new Vector3(-3f, 0.05f, -13.4f), 14f, 4f);
            // Gate closed: looking east from the back yard the side yard is fully occluded
            yield return Compare("closed_to_east", new Vector3(6f, 0.05f, -4f), 80f, 3f);

            // All flowers open (the heaviest scene) and the gate open
            foreach (int area in session.Tasks.AreaIds)
            {
                foreach (PatchProgress patch in session.Tasks.Patches)
                {
                    if (patch.Area == area && !patch.Finished)
                        session.CompletePatch(patch.Id);
                    yield return null;
                }
            }
            yield return s.Seconds(Gate.OpenSeconds + 3.5f);
            s.Check("flowers_open", session.Flowers.OpenCount == session.Flowers.Count, $"{session.Flowers.OpenCount}/{session.Flowers.Count}");
            yield return Measure("flowers_overview", new Vector3(-3f, 0.05f, -13.4f), 14f, 4f);
            yield return Measure("flowers_u6", new Vector3(0f, 0.05f, -13.6f), 0f, 9f);
            yield return Compare("from_gate", new Vector3(12f, 0.05f, 0.3f), 90f, 3f);
            yield return Compare("from_side_yard", new Vector3(27.5f, 0.05f, -1f), -90f, 3f);

            // While working with the tools
            game.Lawn.RegrowAll();
            s.Equip(Tool.MowerSlot);
            yield return Measure("mower", new Vector3(-8f, 0.05f, -12f), 0f, MovementProfile.Mowing.EquipPitch, ScenarioContext.Input(Vector2.up), 180);
            s.Equip(Tool.HoseSlot);
            Vector2 look = LookAt(new Vector3(3f, 0f, -9f), new Vector3(5f, 0f, -6f));
            yield return Measure("hose", new Vector3(3f, 0.05f, -9f), look.x, look.y, ScenarioContext.Input(use: true), 180);
        }

        /// <summary>
        /// U8: the gate cannot be passed while closed, the gate opens when area 1 is finished, you walk through the gate into the side yard and work there;
        /// a save/mess up/load round trip, the break menu and settings, a new game, finishing the whole garden.
        /// </summary>
        static IEnumerator U8Areas(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            LawnTasks tasks = session.Tasks;
            Gate gate = YardBuilder.GateTo(2);
            UI.GameMenu menu = game.Menu;

            s.Check("start", gate != null && !gate.IsOpen && !tasks.IsAreaUnlocked(2) && session.Hud.AreaTitle.StartsWith("Back yard"),
                $"gate {(gate != null && gate.IsOpen ? "open" : "closed")}, area 2 {(tasks.IsAreaUnlocked(2) ? "open" : "locked")}, card '{session.Hud.AreaTitle}'");
            s.Check("scenario_save_separate", !SaveSystem.Exists && SaveSystem.Directory != Application.persistentDataPath, SaveSystem.Directory);
            PatchLayout layout = game.Lawn.Layout;
            s.Check("grass_at_threshold", layout.PatchAt(new Vector2(17.95f, 0f)) != PatchLayout.NoGrass && layout.PatchAt(new Vector2(18.05f, 0f)) != PatchLayout.NoGrass,
                "the cells at the gate threshold have grass (decorations do not clear the grass)");

            // 1) The gate cannot be passed while closed
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(15.8f, 0.05f, 0f), 90f, 4f);
            yield return s.Shot("u8_gate_closed");
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2f);
            s.Check("gate_blocks", p.transform.position.x < 17.8f && session.CurrentArea == 1, $"x {p.transform.position.x:F2}");

            // 2) The gate opens when area 1 is finished
            foreach (PatchProgress patch in tasks.Patches)
            {
                if (patch.Area == 1 && !patch.Finished)
                    session.CompletePatch(patch.Id);
                yield return null;
            }
            s.Check("area1_done", tasks.IsAreaFinished(1) && tasks.IsAreaUnlocked(2) && (gate.IsOpening || gate.IsOpen),
                $"area 1 {tasks.FinishedIn(1)}/{tasks.TotalIn(1)}, gate progress {gate.Progress:F2}");
            s.Check("banner", session.Hud.BannerText == "Gate opened!", $"'{session.Hud.BannerText}'");
            yield return s.Pose(new Vector3(14.6f, 0.05f, -1.2f), 80f, 6f, settleFrames: 2);
            yield return s.Seconds(0.4f);
            yield return s.Shot("u8_gate_opening");
            yield return s.Seconds(Gate.OpenSeconds);
            s.Check("gate_open", gate.IsOpen, $"progress {gate.Progress:F2}");
            s.Check("target_text", session.Hud.Objective.Contains("The gate is open") && session.Hud.Objective.Contains("Side yard"), session.Hud.Objective);
            yield return s.Shot("u8_gate_open");

            // 3) Through the gate into the side yard
            yield return s.Pose(new Vector3(16.2f, 0.05f, 0f), 90f, 4f);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 1.6f);
            yield return s.Frames(2);
            s.Check("entered_side_yard", session.CurrentArea == 2 && p.transform.position.x > 19f, $"x {p.transform.position.x:F2}, area {session.CurrentArea}");
            s.Check("card_area2", session.Hud.AreaTitle.StartsWith("Side yard") && session.Hud.AreaTitle.EndsWith("Area 2")
                    && session.Hud.Objective.StartsWith("Complete every patch"), $"'{session.Hud.AreaTitle}' / '{session.Hud.Objective}'");
            yield return s.Pose(new Vector3(19.3f, 0.05f, -1.6f), 112f, 7f);
            yield return s.Shot("u8_side_yard");
            yield return s.Pose(new Vector3(23.6f, 0.05f, 6.4f), 28f, 9f);
            yield return s.Shot("u8_compost");
            yield return s.Pose(new Vector3(27.5f, 0.05f, 3f), -95f, 4f);
            yield return s.Shot("u8_gate_side_view");

            // 4) Mowing in the side yard
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(21.5f, 0.05f, -12.5f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2.2f);
            yield return s.Frames(3);
            PatchProgress annex = session.ActivePatch;
            s.Check("mowing_in_side_yard", annex != null && annex.Area == 2 && annex.Discovered && session.Hud.PatchTitle == annex.Name,
                $"{annex?.Name} (area {annex?.Area}) mowed {annex?.MowFraction * 100f:0.0}%");
            yield return s.Shot("u8_side_mowing");

            // 5) Save, mess up the state, load
            s.Equip(Tool.HoseSlot);
            var hose = (HoseTool)p.Tools.FindSlot(Tool.HoseSlot);
            hose.SetMode(1);
            yield return s.Frames(2);
            Vector3 savedFeet = p.transform.position;
            float savedYaw = p.Yaw;
            int savedCut = game.Lawn.TotalCutCells, savedFinished = tasks.FinishedCount, savedWeeds = game.Weeds.RemainingWeeds;
            int savedFlowers = session.Flowers.Count;
            bool saved = session.Save();
            long size = SaveSystem.Exists ? new System.IO.FileInfo(SaveSystem.FilePath).Length : 0;
            s.Check("saved", saved && SaveSystem.Exists && size < 2_000_000, $"{size / 1024f:F0} KB, {savedCut} mowed cells");

            foreach (PatchProgress patch in tasks.Patches)
            {
                if (patch.Area == 2 && patch.Id != annex?.Id)
                {
                    session.CompletePatch(patch.Id);
                    break;
                }
            }
            p.Teleport(new Vector3(0f, 0.05f, -12f), 0f, 10f);
            s.Equip(Tool.MowerSlot);
            hose.SetMode(0);
            yield return s.Frames(3);
            bool changed = tasks.FinishedCount != savedFinished;
            bool loaded = session.Load(out string loadError);
            yield return s.Frames(3);
            bool same = game.Lawn.TotalCutCells == savedCut && tasks.FinishedCount == savedFinished && game.Weeds.RemainingWeeds == savedWeeds
                        && Vector3.Distance(p.transform.position, savedFeet) < 0.05f && Mathf.Abs(Mathf.DeltaAngle(p.Yaw, savedYaw)) < 0.1f
                        && p.Tools.Current == hose && hose.ModeIndex == 1 && gate.IsOpen && tasks.IsAreaUnlocked(2) && annex != null && annex.Discovered
                        && session.Flowers.Count > savedFlowers * 0.9f;
            s.Check("loaded", changed && loaded && same,
                $"{loadError} mowed {game.Lawn.TotalCutCells}/{savedCut}, finished {tasks.FinishedCount}/{savedFinished}, " +
                $"position difference {Vector3.Distance(p.transform.position, savedFeet):F3} m, tool {p.Tools.Current?.DisplayName}, flowers {session.Flowers.Count}");
            yield return s.Shot("u8_loaded");

            // 6) Break menu: the game stops, input does not move the player
            s.Check("event_system", UnityEngine.EventSystems.EventSystem.current != null, "uGUI event system present");
            menu.Open();
            Vector3 before = p.transform.position;
            s.SetInput(ScenarioContext.Input(Vector2.up));
            yield return s.Seconds(0.5f);
            s.SetInput(PlayerInputState.None);
            float moved = Vector3.Distance(before, p.transform.position);
            s.Check("break_paused", menu.IsOpen && Time.timeScale == 0f && moved < 0.001f, $"timeScale {Time.timeScale}, drift {moved * 1000f:F1} mm");
            yield return s.Shot("u8_break");
            menu.ShowPage(UI.GameMenu.Page.Settings);
            yield return s.Shot("u8_settings");
            GameSettings.SetGrassDensity(0);
            bool lowDensity = Mathf.Approximately(game.Grass.Density, GameSettings.GrassDensityValues[0]);
            GameSettings.SetGrassDensity(GameSettings.GrassDensityValues.Length - 1);
            GameSettings.SetLookSensitivity(1.5f);
            bool sensitivity = Mathf.Approximately(InputReader.LookSensitivity, 1.5f);
            GameSettings.SetLookSensitivity(1f);
            s.Check("settings_applied", lowDensity && sensitivity && Mathf.Approximately(game.Grass.Density, 1f), "grass density and sensitivity applied instantly");
            menu.ShowPage(UI.GameMenu.Page.ConfirmNew);
            yield return s.Shot("u8_new_game_confirm");
            menu.ShowPage(UI.GameMenu.Page.Main);
            menu.Close();
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 0.5f);
            s.Check("break_over", !menu.IsOpen && Time.timeScale == 1f && Vector3.Distance(before, p.transform.position) > 0.3f,
                $"closed, walked {Vector3.Distance(before, p.transform.position):F2} m");

            // 7) New game: the save is deleted, the garden starts over
            session.NewGame();
            bool fresh = !SaveSystem.Exists && game.Lawn.TotalCutCells == 0 && tasks.FinishedCount == 0 && !gate.IsOpen && !tasks.IsAreaUnlocked(2)
                         && session.Flowers.Count == 0 && game.Weeds.RemainingWeeds == game.Weeds.TotalWeeds
                         && Vector3.Distance(p.transform.position, YardBuilder.SpawnFeet) < 0.05f && p.Tools.Current?.Slot == Tool.MowerSlot;
            s.Check("new_game", fresh, $"save {(SaveSystem.Exists ? "yes" : "none")}, mowed {game.Lawn.TotalCutCells}, finished {tasks.FinishedCount}, " +
                                        $"gate {(gate.IsOpen ? "open" : "closed")}, weeds {game.Weeds.RemainingWeeds}/{game.Weeds.TotalWeeds}");
            yield return s.Frames(3);
            yield return s.Shot("u8_new_game");

            // 8) The whole garden (every area hides items when it unlocks; ones found while mowing must not override the banner)
            foreach (int area in tasks.AreaIds)
            {
                session.Finds.Restore(session.Finds.CaptureCollected(), null);
                foreach (PatchProgress patch in tasks.Patches)
                {
                    if (patch.Area == area && !patch.Finished)
                        session.CompletePatch(patch.Id);
                    yield return null;
                }
            }
            s.Check("garden_done", tasks.AllFinished && session.Hud.Objective.StartsWith("The whole garden is done") && session.Hud.BannerText == "The whole garden is done!",
                $"{tasks.FinishedCount}/{tasks.Total}, '{session.Hud.Objective}', banner '{session.Hud.BannerText}'");
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(28.6f, 0.05f, -13.6f), -38f, 8f);
            yield return s.Seconds(3f);
            yield return s.Shot("u8_garden_done");
            float ms = 0f;
            yield return s.MeasureFrames(240, value => ms = value);
            s.Check("two_areas_frame_time", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps), {session.Flowers.Count} flowers");
        }

        /// <summary>U7: garden and atmosphere shots in the reference framings; post-process, scenery and frame time checks.</summary>
        static IEnumerator U7Scene(ScenarioContext s)
        {
            PlayerController p = s.Player;
            var cameraData = p.WorldCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            UnityEngine.Rendering.Volume volume = Visual.PostFx.Volume;
            bool bloom = volume != null && volume.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.Bloom b) && b.active && b.intensity.value > 0f;
            s.Check("post_process", cameraData.renderPostProcessing && bloom, "camera post-process on, bloom active");
            s.Check("sky", RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Opus/Sky", RenderSettings.skybox?.shader.name);
            s.Check("painted_trees", Visual.PaintedBackdrop.CardCount >= 50, $"{Visual.PaintedBackdrop.CardCount} cards");

            s.Equip(Tool.HandSlot);
            // Reference frame 66 / 37: a wide view from the garden to the house
            yield return s.Pose(new Vector3(-3f, 0f, -13.4f), 14f, 4f);
            yield return s.Seconds(0.3f);
            yield return s.Shot("u7_overview");
            float ms = 0f;
            yield return s.MeasureFrames(240, value => ms = value);
            s.Check("overview_frame_time", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps)");

            // Reference frame 13: the fence, trees behind it and a cloudy sky
            yield return s.Pose(new Vector3(-9f, 0f, 3f), -95f, -8f);
            yield return s.Shot("u7_fence_scenery");
            yield return s.Pose(new Vector3(0f, 0f, -2f), 200f, -30f);
            yield return s.Shot("u7_sky");
            // Reference frame 42: house and veranda
            yield return s.Pose(new Vector3(3.5f, 0f, 9.5f), -10f, 4f);
            yield return s.Shot("u7_house");
            // Reference frame 50: flowerbed
            yield return s.Pose(new Vector3(11.2f, 0f, 10.6f), 20f, 24f);
            yield return s.Shot("u7_flowerbed");
            // Reference frame 66: tree and tire swing
            yield return s.Pose(new Vector3(9.4f, 0f, -10.4f), 30f, 2f);
            yield return s.Shot("u7_tree");
            yield return s.Pose(new Vector3(-9.5f, 0f, 6.2f), -45f, 8f);
            yield return s.Shot("u7_shed");

            // Reference frame 46: the mower framing (with the background)
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, MovementProfile.Mowing.EquipPitch - 14f);
            yield return s.Shot("u7_mower");
        }

        /// <summary>U6: patch tasks, HUD, toolbar, dashed border, show remaining, finish wave, flowers, area finished.</summary>
        static IEnumerator U6Hud(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            YardSession session = game.Session;
            LawnTasks tasks = session.Tasks;
            PatchLayout layout = game.Lawn.Layout;

            int weedsInPatches = 0;
            foreach (PatchProgress patch in tasks.Patches)
                weedsInPatches += patch.WeedsTotal;
            s.Check("patch_tasks", tasks.TotalIn(1) == 16 && tasks.Total == YardBuilder.PatchSeeds.Length && tasks.FinishedCount == 0,
                $"area 1: {tasks.TotalIn(1)}, total {tasks.Total} patches, {tasks.FinishedCount} finished");
            s.Check("weeds_in_patches", weedsInPatches == game.Weeds.States.Count, $"{weedsInPatches}/{game.Weeds.States.Count} weeds belong to a patch");

            // 1) Start: mower, unknown patch, dashed border
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Seconds(0.5f);
            PatchProgress active = session.ActivePatch;
            s.Check("active_patch", active != null && session.Hud.ShownPatch == active && session.Hud.PatchTitle == "???",
                $"{active?.Name} → '{session.Hud.PatchTitle}' in the strip");
            s.Check("dashed_border", active != null && session.Overlay.EdgePatch == active.Id && session.Overlay.EdgeCellCount() > 100
                    && session.Overlay.EdgeStrength > 0.99f, $"{session.Overlay.EdgeCellCount()} border cells");
            yield return s.Shot("u6_hud");

            // 2) Mowing discovers the patch, its name and percentage show
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2f);
            yield return s.Frames(3);
            PatchProgress now = session.ActivePatch;
            s.Check("patch_discovered", active != null && active.Discovered && now != null && session.Hud.PatchTitle == now.DisplayName,
                $"first patch {active?.Name} mowed {active?.MowFraction * 100f:0.0}%; '{session.Hud.PatchTitle}' in the strip");
            yield return s.Shot("u6_while_mowing");

            // 3) Toolbar
            yield return s.Hold(ScenarioContext.Input(selectSlot: Tool.TrimmerSlot), 0.6f);
            s.Check("toolbar_trimmer", session.Hud.ToolTitle == "Trimmer", session.Hud.ToolTitle);
            yield return s.Hold(ScenarioContext.Input(selectSlot: Tool.HoseSlot), 0.6f);
            s.Check("toolbar_hose", session.Hud.ToolTitle == "Hose / Wide", session.Hud.ToolTitle);
            yield return s.Shot("u6_hose_selected");

            // 4) Show remaining
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-2f, 0f, -13.6f), 12f, 9f);
            yield return s.Hold(ScenarioContext.Input(highlight: true), 0.7f);
            s.Check("highlight", session.Overlay.IsHighlighting, "remaining in cyan");
            yield return s.Shot("u6_highlight");
            yield return s.Seconds(LawnOverlay.HighlightSeconds);
            s.Check("highlight_fades", !session.Overlay.IsHighlighting, "the highlight faded by itself");

            // 5) Finish a patch: wave, flowers, "Done"
            int id = layout.PatchAt(new Vector2(4f, -11f));
            PatchProgress target = tasks.Get(id);
            yield return s.Pose(new Vector3(4f, 0f, -14.4f), 0f, 16f);
            yield return s.Frames(5);
            session.CompletePatch(id);
            s.Check("patch_done", target.Finished && tasks.FinishedCount == 1, $"{target.Name}: {tasks.FinishedCount}/{tasks.Total}");
            yield return s.Frames(2);
            s.Check("stripe_done", session.Hud.PatchTitle == $"{target.Name} – Done", session.Hud.PatchTitle);
            yield return s.Seconds(0.3f);
            yield return s.Shot("u6_finish_wave");
            yield return s.Seconds(1.9f);
            s.Check("flowers_opened", session.Flowers.OpenCount > target.AreaSquareMeters * Flowers.PerSquareMeter * 0.5f,
                $"{session.Flowers.OpenCount} flowers ({target.AreaSquareMeters:F0} m²)");
            s.Check("border_hidden", session.Overlay.EdgeStrength < 0.05f, $"border strength on the finished patch {session.Overlay.EdgeStrength:F2}");
            yield return s.Shot("u6_flowers");

            // 6) The whole area (only the first area; the second in U8)
            foreach (PatchProgress patch in tasks.Patches)
            {
                if (!patch.Finished && patch.Area == 1)
                    session.CompletePatch(patch.Id);
                yield return null;
            }
            s.Check("area_done", tasks.IsAreaFinished(1) && !tasks.AllFinished && session.Hud.Objective.StartsWith("This area is done"), session.Hud.Objective);
            yield return s.Seconds(3f);
            yield return s.Pose(new Vector3(0f, 0f, -13.6f), 0f, 9f);
            yield return s.Shot("u6_area_done");

            // 7) Frame time with all flowers open
            float ms = 0f;
            yield return s.MeasureFrames(240, value => ms = value);
            s.Check("frame_time_with_flowers", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps), {session.Flowers.Count} flowers");
        }

        /// <summary>(yaw, pitch) to look at a ground point from the feet.</summary>
        static Vector2 LookAt(Vector3 feet, Vector3 ground)
        {
            Vector3 d = ground - feet;
            float flat = new Vector2(d.x, d.z).magnitude;
            return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, AimPitch(flat));
        }

        /// <summary>How much of the cells needing water in a circle got watered.</summary>
        static float WateredFractionAround(LawnState lawn, Vector2 center, float radius, out int needed)
        {
            needed = 0;
            int watered = 0;
            if (!lawn.Grid.TryGetCellRange(center - Vector2.one * radius, center + Vector2.one * radius, out Vector2Int lo, out Vector2Int hi))
                return 0f;
            for (int z = lo.y; z <= hi.y; z++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                if ((lawn.Grid.CellCenter(x, z) - center).sqrMagnitude > radius * radius)
                    continue;
                int index = lawn.Grid.Index(x, z);
                if (!lawn.NeedsWater(index)) continue;
                needed++;
                if (lawn.IsWatered(index)) watered++;
            }
            return needed == 0 ? 0f : (float)watered / needed;
        }

        static float DrynessAt(LawnState lawn, Vector2 world)
        {
            return lawn.Grid.TryGetCell(world, out int x, out int z) ? lawn.DrynessAt(lawn.Grid.Index(x, z)) : -1f;
        }

        /// <summary>U5: hose view, flow, watering a dry spot, the wet look, spray modes, fence, frame time.</summary>
        static IEnumerator U5Hose(ScenarioContext s)
        {
            PlayerController p = s.Player;
            LawnState lawn = GameBootstrap.Instance.Lawn;
            s.Equip(Tool.HoseSlot);
            var hose = (HoseTool)p.Tools.FindSlot(Tool.HoseSlot);
            var spot = new Vector2(5f, -6f); // YardBuilder.DrySpots[0]
            var feet = new Vector3(5f, 0f, -8.6f);

            s.Check("unwatered_at_start", lawn.TotalNeedsWater > 2000 && lawn.TotalWatered == 0,
                $"{lawn.TotalNeedsWater * 0.01f:F0} m² dry, watered {lawn.TotalWatered}");

            // 1) View: the hose facing a dry spot, the ring (reference frame 19)
            Vector2 look = LookAt(feet, new Vector3(spot.x, 0f, spot.y));
            yield return s.Pose(feet, look.x, look.y);
            yield return s.Seconds(0.2f);
            yield return s.Shot("u5_hose_view");

            // 2) Start watering: the flow shows, dryness drops, the height does not change
            float before = DrynessAt(lawn, spot);
            yield return s.Hold(ScenarioContext.Input(use: true), 0.7f);
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Shot("u5_watering", 2);
            float after = DrynessAt(lawn, spot);
            s.Check("flow_open", hose.SprayLevel >= 0.99f, $"level {hose.SprayLevel:F2}");
            s.Check("dryness_dropped", before > 0.95f && after < before - 0.3f, $"centre dryness {before:F2} → {after:F2}");
            s.Check("watering_kept_height", IsTallAt(lawn, spot), "the watered grass stayed long");

            // 3) Sweep the spot: aim at the points on a grid one by one
            for (float dz = -0.9f; dz <= 0.91f; dz += 0.45f)
            {
                for (float dx = -0.9f; dx <= 0.91f; dx += 0.45f)
                {
                    if (dx * dx + dz * dz > 1.1f) continue;
                    Vector2 aim = LookAt(feet, new Vector3(spot.x + dx, 0f, spot.y + dz));
                    p.Teleport(feet, aim.x, aim.y);
                    s.SetInput(ScenarioContext.Input(use: true));
                    yield return s.Seconds(0.35f);
                }
            }
            s.SetInput(PlayerInputState.None);
            float spotWatered = WateredFractionAround(lawn, spot, 1f, out int spotCells);
            s.Check("dry_spot_watered", spotWatered >= 0.9f, $"{spotWatered * 100f:0}% of the spot watered ({spotCells} cells)");
            s.Check("counter_increased", lawn.TotalWatered > 0 && lawn.WaterFraction > 0f, $"total watered {lawn.WaterFraction * 100f:0.0}%");
            yield return s.Seconds(0.4f);
            look = LookAt(new Vector3(5f, 0f, -9.6f), new Vector3(spot.x, 0f, spot.y));
            yield return s.Pose(new Vector3(5f, 0f, -9.6f), look.x, look.y - 4f);
            yield return s.Shot("u5_watered");

            // 4) Narrow spray: reaches further
            yield return s.Hold(ScenarioContext.Input(alt: true), 0.1f);
            s.Check("mode_narrow", hose.Mode.Name == SprayMode.Narrow.Name && hose.DisplayName == "Hose / Narrow", hose.DisplayName);
            var farFeet = new Vector3(10f, 0f, -1.4f);
            yield return s.Pose(farFeet, 0f, 2f);
            yield return s.Seconds(0.3f);
            float reach = Vector2.Distance(new Vector2(hose.Target.x, hose.Target.z), new Vector2(farFeet.x, farFeet.z));
            s.Check("narrow_reaches_far", reach > SprayMode.Wide.MaxReach + 2f, $"{reach:F1} m");
            look = LookAt(farFeet, new Vector3(10f, 0f, 4f));
            yield return s.Pose(farFeet, look.x, look.y);
            yield return s.Hold(ScenarioContext.Input(use: true), 1.2f);
            s.SetInput(ScenarioContext.Input(use: true));
            yield return s.Shot("u5_narrow", 2);
            s.SetInput(PlayerInputState.None);
            yield return s.Hold(ScenarioContext.Input(alt: true), 0.1f);
            s.Check("mode_back_to_wide", hose.Mode.Name == SprayMode.Wide.Name, hose.DisplayName);

            // 5) The water does not pass through the fence
            yield return s.Pose(new Vector3(0f, 0f, -13.6f), 180f, 3f);
            yield return s.Seconds(0.3f);
            s.Check("water_did_not_cross_fence", hose.Target.z > -15f, $"target z {hose.Target.z:F2} (fence -15)");

            // 6) Frame time while watering (flow, splashes, the wetness texture)
            look = LookAt(new Vector3(-3f, 0f, -2.5f), new Vector3(-3f, 0f, 0f));
            yield return s.Pose(new Vector3(-3f, 0f, -2.5f), look.x, look.y);
            float ms = 0f;
            s.SetInput(ScenarioContext.Input(new Vector2(0.4f, 0f), use: true));
            yield return s.Frames(30);
            yield return s.MeasureFrames(240, value => ms = value);
            s.SetInput(PlayerInputState.None);
            s.Check("frame_time_while_watering", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps)");
        }

        /// <summary>Camera pitch to look at the ground <paramref name="distance"/> m away from the eye.</summary>
        static float AimPitch(float distance) => Mathf.Atan2(PlayerController.EyeHeight, distance) * Mathf.Rad2Deg;

        /// <summary>
        /// Makes an area look mown by the mower for an image comparison: passes along X, 0.5 m apart,
        /// alternating in direction (striped). Wait a few frames afterwards for the visible height to drop.
        /// </summary>
        static void PreMow(LawnState lawn, float xMin, float xMax, float zMin, float zMax)
        {
            float r = MowerTool.BladeRadius;
            int row = 0;
            for (float z = zMin + r; z <= zMax - r + 1e-3f; z += 0.5f, row++)
            {
                bool east = row % 2 == 0;
                var from = new Vector2(east ? xMin + r : xMax - r, z);
                var to = new Vector2(east ? xMax - r : xMin + r, z);
                lawn.CutSwept(from, to, r, to - from, true);
            }
        }

        static WeedState WeedAt(WeedField weeds, float x, float z)
        {
            foreach (WeedState state in weeds.States)
                if (Mathf.Abs(state.Position.x - x) < 0.01f && Mathf.Abs(state.Position.z - z) < 0.01f)
                    return state;
            throw new System.ArgumentException($"No weed at ({x}, {z}).");
        }

        /// <summary>U4: trimmer view, the fence base strip, weed pulling, ivy clearing, the mower not pulling weeds.</summary>
        static IEnumerator U4Trim(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            LawnState lawn = game.Lawn;
            WeedField weeds = game.Weeds;
            s.Equip(Tool.TrimmerSlot);
            var trimmer = (TrimmerTool)p.Tools.FindSlot(Tool.TrimmerSlot);

            // 1) View: as in the reference, the area is mown beforehand, a ~15 cm strip is left at the fence base
            PreMow(lawn, -6f, 1f, -14.8f, -9.5f);
            yield return s.Pose(new Vector3(-2.5f, 0f, -12.9f), 200f, 36f);
            yield return s.Seconds(0.3f);
            yield return s.Shot("u4_trimmer");
            // Flowerbed edge (reference frame 50): mown in front, two weeds in the edge strip
            PreMow(lawn, 8.6f, 16.4f, 9f, 12.55f);
            yield return s.Pose(new Vector3(12f, 0f, 10.6f), 345f, AimPitch(1.7f));
            yield return s.Seconds(0.3f);
            yield return s.Shot("u4_flowerbed_edge");

            // 2) Facing the fence and walking sideways, trim the fence base (this is the strip the mower leaves)
            yield return s.Pose(new Vector3(1f, 0f, -13.7f), 180f, AimPitch(1.4f));
            bool blocked = false;
            yield return s.Hold(ScenarioContext.Input(new Vector2(-1f, 0f), use: true), 1f, _ => blocked |= trimmer.HeadBlocked);
            s.Check("head_rested_on_fence", blocked, "the head did not enter the fence, it stopped in front of it");
            int edgeCut = 0, edgeTotal = 0;
            for (float x = 1.45f; x <= 2.55f; x += 0.1f)
            {
                edgeTotal++;
                if (lawn.Grid.TryGetCell(new Vector2(x, -14.94f), out int cx, out int cz) && lawn.IsCut(lawn.Grid.Index(cx, cz)))
                    edgeCut++;
            }
            s.Check("fence_base_trimmed", edgeCut >= edgeTotal * 0.9f, $"{edgeCut} of the {edgeTotal} cells 6 cm from the fence were cut");
            yield return s.Shot("u4_fence_base");

            // 3) Pulling a weed (a weed in the open)
            WeedState weed = WeedAt(weeds, 3f, -2f);
            int weedsBefore = weeds.RemainingWeeds;
            yield return s.Pose(new Vector3(3f, 0f, -3.2f), 0f, AimPitch(1.2f));
            yield return s.Shot("u4_weeds_before");
            yield return s.Hold(ScenarioContext.Input(use: true), WeedState.WeedHealth + 0.25f);
            yield return s.Seconds(0.3f);
            s.Check("weed_pulled", weed.Removed && weeds.RemainingWeeds == weedsBefore - 1,
                $"weeds left {weedsBefore} → {weeds.RemainingWeeds}");
            yield return s.Shot("u4_weeds_after");

            // 4) Ivy: thins out leaf by leaf, then is cleared completely
            WeedState vine = WeedAt(weeds, -12.3f, 8.55f);
            yield return s.Pose(new Vector3(-12.3f, 0f, 7.25f), 0f, AimPitch(1.3f));
            yield return s.Shot("u4_ivy_before");
            yield return s.Hold(ScenarioContext.Input(use: true), WeedState.OvergrowthHealth * 0.4f);
            s.Check("ivy_shrinking", !vine.Removed && vine.Fraction < 0.9f, $"{vine.Fraction * 100f:0}% left");
            yield return s.Shot("u4_ivy_half");
            yield return s.Hold(ScenarioContext.Input(use: true), WeedState.OvergrowthHealth * 0.8f);
            yield return s.Seconds(0.3f);
            s.Check("ivy_cleared", vine.Removed, $"ivy left {weeds.RemainingOvergrowth}/{weeds.TotalOvergrowth}");
            yield return s.Shot("u4_ivy_after");

            // 5) The mower passes over the weed but does not pull it (only flattens it)
            WeedState openWeed = WeedAt(weeds, -5f, 7f);
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(-5f, 0f, 4.4f), 0f, MovementProfile.Mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(Vector2.up), 2.2f);
            s.Check("mower_did_not_pull_weed", !openWeed.Removed, "the weed is in place (only the trimmer pulls)");

            // 6) Frame time while trimming
            s.Equip(Tool.TrimmerSlot);
            yield return s.Pose(new Vector3(6f, 0f, -9f), 90f, AimPitch(1.2f));
            float ms = 0f;
            s.SetInput(ScenarioContext.Input(new Vector2(0f, 0.6f), new Vector2(0f, 0f), use: true));
            yield return s.Frames(30);
            yield return s.MeasureFrames(240, value => ms = value);
            s.SetInput(PlayerInputState.None);
            s.Check("frame_time_while_trimming", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps)");
        }

        public static IEnumerable<string> Names => Scripts.Keys;

        public static bool TryGet(string name, out ScenarioScript script)
        {
            script = null;
            return !string.IsNullOrEmpty(name) && Scripts.TryGetValue(name, out script);
        }

        static IEnumerator U0Overview(ScenarioContext s)
        {
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(0f, 0f, -13f), 0f, 6f);
            yield return s.Shot("u0_overview_wide");
            yield return s.Pose(new Vector3(-4f, 0f, 1f), -40f, 8f);
            yield return s.Shot("u0_shed");
            yield return s.Pose(new Vector3(4f, 0f, -8f), 30f, -22f);
            yield return s.Shot("u0_sky");
        }

        /// <summary>U3: mown area, soft cut, clippings, the fence base strip, opposite-direction stripes, frame time.</summary>
        static IEnumerator U3Mow(ScenarioContext s)
        {
            PlayerController p = s.Player;
            GameBootstrap game = GameBootstrap.Instance;
            LawnState lawn = game.Lawn;
            float cellArea = lawn.Grid.CellSize * lawn.Grid.CellSize;
            MovementProfile mowing = MovementProfile.Mowing;
            Vector2 forward = Vector2.up;
            s.Equip(Tool.MowerSlot);
            var mower = (MowerTool)p.Tools.FindSlot(Tool.MowerSlot);

            // 1) A straight pass: mown area ≈ blade width × distance travelled
            // x must not be aligned to a cell boundary (the stripe width varies by ±1 column with the alignment)
            yield return s.Pose(new Vector3(-7.97f, 0f, -12f), 0f, mowing.EquipPitch);
            yield return s.Shot("u3_before");
            int cutBefore = lawn.TotalCutCells;
            Vector3 start = mower.transform.position;
            bool sawPartial = false;
            float maxLoad = 0f;
            yield return s.Hold(ScenarioContext.Input(forward), 1.4f, _ => { sawPartial |= HasPartiallyCutCell(lawn, mower); maxLoad = Mathf.Max(maxLoad, mower.CutLoad); });
            s.SetInput(ScenarioContext.Input(forward));
            yield return s.Shot("u3_clippings", 2);
            s.Check("clippings_fly", game.Clippings.AliveCount > 20, $"{game.Clippings.AliveCount} clippings in the air");
            yield return s.Hold(ScenarioContext.Input(forward), 2.6f, _ => { sawPartial |= HasPartiallyCutCell(lawn, mower); maxLoad = Mathf.Max(maxLoad, mower.CutLoad); });
            yield return s.Seconds(0.8f);
            float travelled = Vector3.Distance(start, mower.transform.position);
            float cutArea = (lawn.TotalCutCells - cutBefore) * cellArea;
            // Grid margin: because of the 10 cm cells the stripe width varies by ±1 cell column with the alignment.
            float r = MowerTool.BladeRadius, cell = lawn.Grid.CellSize, disc = Mathf.PI * r * r;
            float minArea = (2f * r - cell) * travelled + disc * 0.8f;
            float maxArea = (2f * r + cell) * travelled + disc * 1.2f;
            s.Check("mowed_area", cutArea >= minArea && cutArea <= maxArea,
                $"{cutArea:F2} m² (expected {minArea:F2}..{maxArea:F2}, path {travelled:F2} m)");
            s.Check("soft_cut", sawPartial, "the cut grass's height drops gradually, not at once");
            s.Check("mowing_load", maxLoad > 0.5f, $"highest load {maxLoad:F2} (in long grass the mower slows down, the vibration grows)");

            // Turning back to see the trail
            Vector3 end = p.transform.position;
            yield return s.Pose(end + new Vector3(0f, 0f, 2.6f), 180f, mowing.EquipPitch);
            yield return s.Shot("u3_trail");

            // 2) Side-by-side passes in opposite directions → stripes
            float x0 = -2f;
            for (int pass = 0; pass < 5; pass++)
            {
                bool north = pass % 2 == 0;
                float x = x0 + pass * 0.46f;
                yield return s.Pose(new Vector3(x, 0f, north ? -13.2f : -4.6f), north ? 0f : 180f, mowing.EquipPitch);
                yield return s.Hold(ScenarioContext.Input(forward), 4.2f);
                yield return s.Seconds(0.3f);
            }
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-1.1f, 0f, -14.4f), 4f, 14f);
            yield return s.Shot("u3_stripes");
            yield return s.Pose(new Vector3(-0.4f, 0f, -2.2f), 180f, 28f);
            yield return s.Shot("u3_stripes_reverse");

            // 3) Fence base: when the mower leans on the fence the blade cannot reach the base, a thin strip is left
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(8f, 0f, -11.5f), 180f, mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(forward), 3f);
            yield return s.Seconds(0.3f);
            const float fenceZ = -15f;
            bool edgeTall = IsTallAt(lawn, new Vector2(8f, fenceZ + 0.06f));
            bool frontCut = !IsTallAt(lawn, new Vector2(8f, fenceZ + 0.35f));
            s.Check("fence_base_not_mowed", edgeTall, "the grass 6 cm from the fence stayed long (the trimmer's job)");
            s.Check("fence_front_mowed", frontCut, "the grass 35 cm from the fence was mowed");
            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(9.2f, 0f, -13.6f), 215f, 45f);
            yield return s.Shot("u3_fence_base");

            // 4) Frame time while mowing (clippings and texture uploads included)
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(10f, 0f, -13f), 0f, mowing.EquipPitch);
            float ms = 0f;
            s.SetInput(ScenarioContext.Input(forward));
            yield return s.Frames(30);
            yield return s.MeasureFrames(240, value => ms = value);
            s.SetInput(PlayerInputState.None);
            s.Check("frame_time_while_mowing", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps)");
        }

        static bool IsTallAt(LawnState lawn, Vector2 world)
        {
            return lawn.Grid.TryGetCell(world, out int x, out int z) && lawn.CellAt(lawn.Grid.Index(x, z)).r == LawnState.Tall;
        }

        /// <summary>Is there a cell under the blade whose height has partly dropped (an intermediate value)?</summary>
        static bool HasPartiallyCutCell(LawnState lawn, MowerTool mower)
        {
            Vector3 c = mower.transform.position;
            if (!lawn.Grid.TryGetCellRange(new Vector2(c.x - 0.4f, c.z - 0.4f), new Vector2(c.x + 0.4f, c.z + 0.6f), out Vector2Int lo, out Vector2Int hi))
                return false;
            for (int z = lo.y; z <= hi.y; z++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                byte h = lawn.CellAt(lawn.Grid.Index(x, z)).r;
                if (h > 20 && h < 235)
                    return true;
            }
            return false;
        }

        /// <summary>U2: patch quilt and obstacle mask checks, lawn shots, 1080p frame time.</summary>
        static IEnumerator U2Grass(ScenarioContext s)
        {
            GameBootstrap game = GameBootstrap.Instance;
            PatchLayout layout = game.Lawn.Layout;
            GrassField grass = game.Grass;
            float cellArea = layout.Grid.CellSize * layout.Grid.CellSize;

            s.Check("patch_count", layout.PatchCount >= 12, $"{layout.PatchCount} patches");
            int smallest = int.MaxValue;
            for (int id = 1; id <= layout.PatchCount; id++)
                smallest = Mathf.Min(smallest, layout.CountCells(id));
            s.Check("patches_filled", smallest * cellArea >= 15f, $"smallest patch {smallest * cellArea:F0} m²");
            s.Check("no_grass_under_shed", layout.PatchAt(new Vector2(-13f, 10f)) == PatchLayout.NoGrass, "middle of the shed");
            s.Check("no_grass_under_flowerbed", layout.PatchAt(new Vector2(12.5f, 13.6f)) == PatchLayout.NoGrass, "middle of the flowerbed");
            s.Check("no_grass_under_tree", layout.PatchAt(new Vector2(12f, -5f)) == PatchLayout.NoGrass, "tree trunk");
            s.Check("grass_in_open_area", layout.PatchAt(new Vector2(0f, -5f)) != PatchLayout.NoGrass, "middle of the garden");
            s.Check("dry_area_exists", layout.CountDryCells() * cellArea > 20f, $"{layout.CountDryCells() * cellArea:F0} m² dry");

            MovementProfile mowing = MovementProfile.Mowing;
            s.Equip(Tool.MowerSlot);
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, mowing.EquipPitch);
            yield return s.Shot("u2_mower");
            s.Check("grass_drawn", grass.DrawnChunks > 0 && grass.DrawnTufts > 1000, $"{grass.DrawnChunks} chunks, {grass.DrawnTufts} tufts");
            yield return s.Pose(new Vector3(-6f, 0f, -1f), 30f, 30f);
            yield return s.Shot("u2_mower_edge");

            s.Equip(Tool.HandSlot);
            yield return s.Pose(new Vector3(-2f, 0f, -13.5f), 12f, 6f);
            yield return s.Shot("u2_quilt");
            yield return s.Pose(new Vector3(3f, 0f, -8f), 50f, 35f);
            yield return s.Shot("u2_close");
            yield return s.Pose(new Vector3(-10f, 0f, -12f), 25f, 12f);
            yield return s.Shot("u2_toward_shed");

            // Performance: the view where the most grass is visible, 1080p
            yield return s.Pose(new Vector3(0f, 0f, -13.5f), 0f, 8f);
            foreach (float density in new[] { 1f, 0.5f })
            {
                grass.Density = density;
                yield return s.Frames(30);
                float ms = 0f;
                yield return s.MeasureFrames(240, value => ms = value);
                s.Check($"frame_time_density_{density * 100f:0}", ms < 16.6f, $"{ms:F2} ms ({1000f / ms:F0} fps), {grass.DrawnTufts} tufts");
            }
            grass.Density = 1f;
        }

        /// <summary>Framing tuning: a field of view × camera pitch grid (to compare with reference frame 46).</summary>
        static IEnumerator U1Framing(ScenarioContext s)
        {
            PlayerController p = s.Player;
            s.Equip(Tool.MowerSlot);
            foreach (float fov in new[] { 48f, 52f, 56f })
            {
                foreach (float pitch in new[] { 36f, 40f, 44f })
                {
                    p.FieldOfViewOverride = fov;
                    yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, pitch);
                    yield return s.Shot($"framing_f{fov:0}_p{pitch:0}");
                }
            }
            p.FieldOfViewOverride = float.NaN;
        }

        /// <summary>U1: mower framing, acceleration/coasting, resting against the fence and the flowerbed, collisions while turning, tool switch.</summary>
        static IEnumerator U1Mower(ScenarioContext s)
        {
            PlayerController p = s.Player;
            MovementProfile mowing = MovementProfile.Mowing;
            MovementProfile walking = MovementProfile.Walking;
            Vector2 forward = Vector2.up;
            s.Equip(Tool.MowerSlot);
            var mower = (MowerTool)p.Tools.FindSlot(Tool.MowerSlot);

            // 1) Framing: a fixed shot to compare with the reference
            yield return s.Pose(new Vector3(0f, 0f, -12f), 0f, mowing.EquipPitch);
            yield return s.Shot("u1_mower_view");

            // 2) Heavy start, top speed, coasting to a stop
            yield return s.Pose(new Vector3(-4f, 0f, -11f), 0f, mowing.EquipPitch);
            yield return s.Hold(ScenarioContext.Input(forward), 0.5f);
            float early = p.LocalVelocity.y;
            float expectedEarly = mowing.Acceleration * 0.5f;
            s.Check("acceleration_half_second", Mathf.Abs(early - expectedEarly) < 0.2f,
                $"{early:F2} m/s (expected ~{expectedEarly:F2})");

            yield return s.Hold(ScenarioContext.Input(forward), 1.5f);
            float top = p.LocalVelocity.y;
            // While mowing long grass the mower slows down by at most 10%.
            float slowest = mowing.ForwardSpeed * (1f - MowerTool.CuttingSlowdown) - 0.02f;
            s.Check("max_speed", top >= slowest && top <= mowing.ForwardSpeed + 0.01f,
                $"{top:F2} m/s (expected {slowest:F2}..{mowing.ForwardSpeed:F2})");

            Vector3 releasedAt = p.transform.position;
            yield return s.Seconds(1.2f);
            float coast = Vector3.Distance(releasedAt, p.transform.position);
            float expectedCoast = MotionMath.StoppingDistance(top, mowing.Deceleration);
            s.Check("coast_distance", Mathf.Abs(coast - expectedCoast) < 0.12f, $"{coast:F2} m (expected ~{expectedCoast:F2})");
            s.Check("stopped", p.LocalVelocity.magnitude < 0.01f, $"{p.LocalVelocity.magnitude:F3} m/s");

            // 3) Resting against the south fence: the housing must not enter the fence, the front end must lean on it
            yield return s.Pose(new Vector3(-2f, 0f, -11f), 180f, mowing.EquipPitch);
            bool overlapped = false;
            yield return s.Hold(ScenarioContext.Input(forward), 3.5f, _ => overlapped |= mower.OverlapsObstacle(0.02f));
            const float fenceInnerZ = -15f + 0.03f;
            float fenceGap = mower.FrontTip.z - fenceInnerZ;
            s.Check("stayed_out_of_fence", !overlapped && !mower.OverlapsObstacle(), "the housing did not enter the fence while pushing");
            s.Check("leaned_on_fence", fenceGap > -0.01f && fenceGap < 0.08f, $"gap between front end and fence {fenceGap * 100f:F1} cm");
            s.Check("stopped_at_fence", p.LocalVelocity.magnitude < 0.05f, $"{p.LocalVelocity.magnitude:F3} m/sn");
            yield return s.Shot("u1_fence");

            // 4) Turning right while pushing forward at the fence base
            overlapped = false;
            yield return s.Hold(ScenarioContext.Input(forward, new Vector2(90f, 0f)), 1.5f, _ => overlapped |= mower.OverlapsObstacle(0.02f));
            s.Check("stayed_out_of_fence_while_turning", !overlapped, "the housing did not enter the fence during the turn");

            // 5) Flowerbed edge: the mower does not enter the flowerbed's white curb, an unmowable strip is left in front of it
            yield return s.Pose(new Vector3(12.5f, 0f, 8.5f), 0f, mowing.EquipPitch);
            overlapped = false;
            yield return s.Hold(ScenarioContext.Input(forward), 3.5f, _ => overlapped |= mower.OverlapsObstacle(0.02f));
            const float bedEdgeZ = 13.6f - 0.9f;
            float bedGap = bedEdgeZ - mower.FrontTip.z;
            s.Check("stayed_out_of_flowerbed", !overlapped && bedGap > -0.01f && bedGap < 0.08f, $"gap between front end and flowerbed edge {bedGap * 100f:F1} cm");
            yield return s.Shot("u1_flowerbed");

            // 6) Switching to the hand: the mower disappears, the walking speed comes back
            yield return s.Hold(ScenarioContext.Input(selectSlot: Tool.HandSlot), 0.6f);
            s.Check("switched_to_hand", p.Tools.Current is HandTool && mower.Presence <= 0f, $"tool: {p.Tools.Current?.DisplayName}, mower {mower.Presence:F2}");
            yield return s.Pose(new Vector3(4f, 0f, -11f), 0f, 5f);
            yield return s.Hold(ScenarioContext.Input(forward), 1f);
            s.Check("walking_speed", Mathf.Abs(p.LocalVelocity.y - walking.ForwardSpeed) < 0.05f, $"{p.LocalVelocity.y:F2} m/s");
            yield return s.Shot("u1_walking");

            // 7) Back to the mower: the camera drops to the mower pitch
            yield return s.Hold(ScenarioContext.Input(selectSlot: Tool.MowerSlot), 1f);
            s.Check("back_to_mower", p.Tools.Current is MowerTool && mower.Presence >= 1f && Mathf.Abs(p.Pitch - mowing.EquipPitch) < 2f
                    && Mathf.Abs(p.WorldCamera.fieldOfView - mowing.FieldOfView) < 1f,
                $"mower {mower.Presence:F2}, pitch {p.Pitch:F1}°, field of view {p.WorldCamera.fieldOfView:F1}°");
        }
    }
}
