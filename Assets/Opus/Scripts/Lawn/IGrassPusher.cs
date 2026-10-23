namespace Opus.Lawn
{
    /// <summary>Something that pushes the grass aside (the player's feet, the mower's housing).</summary>
    public interface IGrassPusher
    {
        /// <param name="push">(x, z, radius m, strength 0..1)</param>
        bool TryGetGrassPush(out UnityEngine.Vector4 push);
    }
}
