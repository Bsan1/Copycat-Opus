namespace Opus.Core
{
    /// <summary>Physics/render layers. The names of 8 and 9 are written to the TagManager by Setup.ConfigureProject.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int UI = 5;
        public const int Player = 8;
        public const int Tool = 9;

        /// <summary>Layers counted as obstacles in the collision queries of the tools held in hand.</summary>
        public const int ObstacleMask = ~((1 << IgnoreRaycast) | (1 << UI) | (1 << Player) | (1 << Tool));
    }
}
