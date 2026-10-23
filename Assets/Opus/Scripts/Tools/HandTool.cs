using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>Empty hand: free walking. Interaction (opening gates etc.) comes in U8.</summary>
    public sealed class HandTool : Tool
    {
        public override int Slot => HandSlot;
        public override string DisplayName => "Hand";
        public override string Hint => "WASD walk · Mouse look · 1 mower · 2 trimmer · 3 hose";
        public override MovementProfile Movement => MovementProfile.Walking;

        public static HandTool Create(PlayerController player)
        {
            var go = new GameObject("Hand");
            go.transform.SetParent(player.transform, false);
            return go.AddComponent<HandTool>();
        }
    }
}
