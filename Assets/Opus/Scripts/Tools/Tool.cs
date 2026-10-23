using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// The tool in hand. Toolbar slots: 0 mower, 1 trimmer, 2 hose, 3 hand.
    /// Every frame the player controller moves in order, gets the tool's body out of obstacles with
    /// <see cref="ResolveBody"/>, then calls <see cref="Tick"/>.
    /// </summary>
    public abstract class Tool : MonoBehaviour
    {
        public const int MowerSlot = 0;
        public const int TrimmerSlot = 1;
        public const int HoseSlot = 2;
        public const int HandSlot = 3;

        public abstract int Slot { get; }
        public abstract string DisplayName { get; }
        /// <summary>The contextual prompt strip at the bottom centre (in the reference only visible for the hose/interactions); hidden if empty.</summary>
        public virtual string Prompt => string.Empty;
        /// <summary>Shortcut summary shown on the developer card.</summary>
        public virtual string Hint => string.Empty;

        /// <summary>
        /// The ground point the tool is working on right now (blade, trimmer head, where the water lands). The interface picks the active
        /// patch from here; if false the player's feet are used.
        /// </summary>
        public virtual bool TryGetWorkPoint(out Vector3 point)
        {
            point = default;
            return false;
        }

        /// <summary>Raised when the name/prompt changes while the tool is selected (e.g. the hose's spray mode).</summary>
        public event System.Action<Tool> DescriptionChanged;

        protected void RaiseDescriptionChanged() => DescriptionChanged?.Invoke(this);
        public virtual MovementProfile Movement => MovementProfile.Walking;

        /// <summary>0 = put away (invisible), 1 = in hand and ready. The switch animation drives this.</summary>
        public float Presence { get; private set; }

        internal void SetPresence(float presence)
        {
            Presence = Mathf.Clamp01(presence);
            OnPresenceChanged(Presence);
        }

        protected virtual void OnPresenceChanged(float presence) { }
        public virtual void OnEquip(PlayerController player) { }
        public virtual void OnUnequip(PlayerController player) { }
        public virtual void ResolveBody(PlayerController player) { }
        public virtual void Tick(PlayerController player, in PlayerInputState input, float deltaTime) { }
    }
}
