using System;
using System.Collections.Generic;
using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Holds the tools and switches between them: the old tool is lowered, then the new one is raised.
    /// </summary>
    public sealed class ToolBelt : MonoBehaviour
    {
        public const float HalfSwitchSeconds = 0.2f;

        readonly List<Tool> tools = new List<Tool>();
        Tool pending;
        bool lowering;

        public Tool Current { get; private set; }
        public IReadOnlyList<Tool> Tools => tools;
        public bool IsSwitching => pending != null || (Current != null && Current.Presence < 1f);
        public MovementProfile Movement => Current != null ? Current.Movement : MovementProfile.Walking;

        /// <summary>Raised when the selected tool changes (when the lowering ends and the new one is taken in hand).</summary>
        public event Action<Tool> Changed;

        public void Add(Tool tool)
        {
            tools.Add(tool);
            tools.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            if (tool != Current)
                tool.SetPresence(0f);
        }

        public Tool FindSlot(int slot) => tools.Find(t => t.Slot == slot);

        /// <summary>Switch without animation (for game start and scenarios).</summary>
        public void EquipImmediate(int slot, PlayerController player)
        {
            Tool tool = FindSlot(slot);
            if (tool == null)
                return;
            if (Current != null && Current != tool)
            {
                Current.SetPresence(0f);
                Current.OnUnequip(player);
            }
            pending = null;
            lowering = false;
            bool changed = Current != tool;
            Current = tool;
            if (changed)
            {
                tool.OnEquip(player);
                Changed?.Invoke(tool);
            }
            tool.SetPresence(1f);
        }

        /// <summary>Starts switching to the tool in the slot; false if the slot is empty or already selected.</summary>
        public bool Request(int slot)
        {
            Tool tool = FindSlot(slot);
            if (tool == null || tool == (pending ?? Current))
                return false;
            pending = tool;
            lowering = Current != null;
            return true;
        }

        public bool Cycle(int direction)
        {
            if (tools.Count < 2 || direction == 0)
                return false;
            Tool from = pending ?? Current;
            int index = from == null ? 0 : tools.IndexOf(from);
            return Request(tools[NextIndex(index, direction, tools.Count)].Slot);
        }

        public static int NextIndex(int current, int direction, int count)
        {
            if (count <= 0) return -1;
            return ((current + direction) % count + count) % count;
        }

        internal void Tick(PlayerController player, in PlayerInputState input, float deltaTime)
        {
            if (input.SelectSlot >= 0)
                Request(input.SelectSlot);
            else if (input.CycleTool != 0)
                Cycle(input.CycleTool);

            float step = deltaTime / HalfSwitchSeconds;
            if (pending != null && lowering && Current != null)
            {
                Current.SetPresence(Current.Presence - step);
                if (Current.Presence > 0f)
                    return;
                Current.OnUnequip(player);
                lowering = false;
            }

            if (pending != null)
            {
                Current = pending;
                pending = null;
                Current.SetPresence(0f);
                Current.OnEquip(player);
                Changed?.Invoke(Current);
            }

            if (Current != null && Current.Presence < 1f)
                Current.SetPresence(Current.Presence + step);
        }
    }
}
