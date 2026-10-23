using System.Collections.Generic;
using Opus.Lawn;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Client gardens (the jobs on the job board). Each has a single area, fenced in, with the house to the north; the player enters from the south.
    /// The city garden is small but busy (many obstacles), the manor is wide and flat (requested pattern), the country house is neglected (many weeds,
    /// ivy, dry spots, long grass).
    /// </summary>
    public static class ClientYards
    {
        // The ids are save slot names from the Turkish build, kept so existing saves still load.
        public const string CityId = "is_sehir";
        public const string ManorId = "is_malikane";
        public const string CountryId = "is_kir";

        /// <summary>Seed of the patch where the manor wants the pattern ("Front lawn").</summary>
        public static readonly Vector2 ManorPatternSeed = new Vector2(5f, -9f);

        /// <summary>The front of the house is at <paramref name="northZ"/> + 0.5 to the north; the east/west fences run up to the house.</summary>
        static PropPlacement House(float northZ, int variant) => new PropPlacement(PropKind.House, new Vector3(0f, 0f, northZ + 5.5f), variant: variant);

        /// <summary>City garden: 16 × 12 m, tree and swing, birdbath, two flowerbeds, a vegetable bed, a barrel.</summary>
        public static YardDefinition City()
        {
            const float x = 8f, zMin = -6f, zMax = 6f;
            return new YardDefinition
            {
                Id = CityId,
                Version = 1,
                Areas = new[] { new LawnArea(1, "City garden", -x, zMin, x, zMax) },
                PatchSeeds = new[]
                {
                    new PatchSeed(-4.5f, -3.5f, 4), new PatchSeed(3.5f, -3.5f, 1), new PatchSeed(0f, 0.8f, 0),
                    new PatchSeed(-6f, 3.5f, 2), new PatchSeed(6f, 1.8f, 3),
                },
                PatchInfos = new[]
                {
                    new PatchInfo("Under the tree", "#5C8FC9"), new PatchInfo("By the birdbath", "#C94F6D"), new PatchInfo("Veranda front", "#E0853A"),
                    new PatchInfo("West corner", "#9AA543"), new PatchInfo("East corner", "#8E6CB8"),
                },
                DrySpots = new[] { new DrySpot(1.5f, -2f, 0.9f) },
                WeedSpots = new[]
                {
                    new WeedSpot(-7.5f, -5.5f, 1.05f), new WeedSpot(7.5f, -5.5f), new WeedSpot(-7.55f, 0.5f, 0.95f),
                    new WeedSpot(7.55f, -1f), new WeedSpot(-4.4f, -2f, 0.9f), new WeedSpot(2.2f, 2.3f),
                    new WeedSpot(0.5f, -5.75f, 1.0f, 0.2f),
                },
                FenceLines = new[]
                {
                    (new Vector3(-x, 0f, zMin), new Vector3(x, 0f, zMin)),
                    (new Vector3(-x, 0f, zMin), new Vector3(-x, 0f, zMax + 0.6f)),
                    (new Vector3(x, 0f, zMin), new Vector3(x, 0f, zMax + 0.6f)),
                },
                Props = new[]
                {
                    House(zMax, 1),
                    new PropPlacement(PropKind.Flowerbed, new Vector3(-6f, 0f, 5f), new Vector2(3.4f, 1f)),
                    new PropPlacement(PropKind.Flowerbed, new Vector3(6f, 0f, 5f), new Vector2(3.4f, 1f)),
                    new PropPlacement(PropKind.Tree, new Vector3(-5f, 0f, -2f)),
                    new PropPlacement(PropKind.Birdbath, new Vector3(4.5f, 0f, -2.5f)),
                    new PropPlacement(PropKind.RaisedBed, new Vector3(5.8f, 0f, -4.4f), new Vector2(1.2f, 2.4f), variant: 2),
                    new PropPlacement(PropKind.RainBarrel, new Vector3(7.3f, 0f, 1.5f)),
                },
                SpawnFeet = new Vector3(1.5f, 0.05f, -5f),
                SpawnYaw = 0f,
            };
        }

        /// <summary>Manor lawn: 40 × 28 m, wide and flat; two big trees, a birdbath in the middle, flowerbeds on both sides of the house.</summary>
        public static YardDefinition Manor()
        {
            const float x = 20f, z = 14f;
            return new YardDefinition
            {
                Id = ManorId,
                Version = 1,
                Areas = new[] { new LawnArea(1, "Manor lawn", -x, -z, x, z) },
                PatchSeeds = new[]
                {
                    new PatchSeed(-15f, -9f, 1), new PatchSeed(-5f, -9.5f, 0), new PatchSeed(ManorPatternSeed.x, ManorPatternSeed.y, 1),
                    new PatchSeed(15f, -9f, 2), new PatchSeed(-14f, 4f, 4), new PatchSeed(0f, 5f, 0), new PatchSeed(14f, 6f, 3),
                },
                PatchInfos = new[]
                {
                    new PatchInfo("West lawn", "#5C8FC9"), new PatchInfo("South lawn", "#D9A93A"), new PatchInfo("Front lawn", "#C94F6D"),
                    new PatchInfo("East lawn", "#4FA3A5"), new PatchInfo("Around the trees", "#9AA543"), new PatchInfo("Middle lawn", "#E0853A"),
                    new PatchInfo("Rose bed front", "#8E6CB8"),
                },
                DrySpots = new[] { new DrySpot(-6f, 2f, 1.2f), new DrySpot(9f, -3f, 1.0f) },
                WeedSpots = new[]
                {
                    new WeedSpot(-19.5f, -13.5f), new WeedSpot(19.5f, -13.5f, 1.05f), new WeedSpot(-19.55f, 6f),
                    new WeedSpot(19.55f, -4f, 0.95f), new WeedSpot(-11.4f, -2.7f), new WeedSpot(0.5f, -0.4f, 0.9f),
                    new WeedSpot(8f, -13.75f, 1.2f, 0.2f),
                },
                FenceLines = new[]
                {
                    (new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z)),
                    (new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z)),
                    (new Vector3(x, 0f, -z), new Vector3(x, 0f, z)),
                    (new Vector3(-x, 0f, z), new Vector3(-8.5f, 0f, z)),
                    (new Vector3(8.5f, 0f, z), new Vector3(x, 0f, z)),
                },
                Props = new[]
                {
                    House(z, 2),
                    new PropPlacement(PropKind.Tree, new Vector3(-12f, 0f, -3f)),
                    new PropPlacement(PropKind.Tree, new Vector3(12f, 0f, 5f)),
                    new PropPlacement(PropKind.Birdbath, new Vector3(0f, 0f, -1f)),
                    new PropPlacement(PropKind.Flowerbed, new Vector3(-12.5f, 0f, 12.4f), new Vector2(8f, 1.2f)),
                    new PropPlacement(PropKind.Flowerbed, new Vector3(12.5f, 0f, 12.4f), new Vector2(8f, 1.2f)),
                },
                SpawnFeet = new Vector3(0f, 0.05f, -12.5f),
                SpawnYaw = 0f,
            };
        }

        /// <summary>Neglected country house: 26 × 20 m, long straw meadow, shed, compost, wheelbarrow, barrel; full of weeds and ivy.</summary>
        public static YardDefinition Country()
        {
            const float x = 13f, z = 10f;
            return new YardDefinition
            {
                Id = CountryId,
                Version = 1,
                Areas = new[] { new LawnArea(1, "Country house garden", -x, -z, x, z) },
                PatchSeeds = new[]
                {
                    new PatchSeed(-8f, -2f, 5), new PatchSeed(0f, -6f, 0, 1f), new PatchSeed(8f, -7f, 5),
                    new PatchSeed(-6f, 6f, 1), new PatchSeed(5f, 4f, 0),
                },
                PatchInfos = new[]
                {
                    new PatchInfo("Shed front", "#D9A93A"), new PatchInfo("South meadow", "#C94F6D"), new PatchInfo("East meadow", "#4FA3A5"),
                    new PatchInfo("Barrel corner", "#5C8FC9"), new PatchInfo("Compost path", "#9AA543"),
                },
                DrySpots = new[] { new DrySpot(-3f, -1f, 1.2f), new DrySpot(3f, 2f, 1.0f), new DrySpot(-10f, 2f, 0.9f), new DrySpot(10.5f, -6f, 0.9f) },
                WeedSpots = new[]
                {
                    new WeedSpot(-12.5f, -9.5f, 1.1f), new WeedSpot(12.5f, -9.5f, 1.05f), new WeedSpot(-12.55f, -3f),
                    new WeedSpot(12.55f, 2f), new WeedSpot(-12.55f, 4.5f, 0.9f), new WeedSpot(0f, -9.55f),
                    new WeedSpot(6f, -9.55f, 1.1f), new WeedSpot(-5f, -9.55f), new WeedSpot(-7.6f, -6f, 0.95f),
                    new WeedSpot(9.1f, -1.7f), new WeedSpot(-2f, 3f, 1.15f), new WeedSpot(4f, -3f, 1.2f),
                    new WeedSpot(-4f, -5f), new WeedSpot(8.4f, 6.6f, 0.9f),
                    // Ivy: front of the house, west face of the shed, east and west fences, front of the compost
                    new WeedSpot(-6f, 9.75f, 1.5f, 0.2f), new WeedSpot(-11.25f, -6.5f, 0.2f, 1.1f), new WeedSpot(12.75f, -5f, 0.2f, 1.4f),
                    new WeedSpot(-12.75f, 0f, 0.2f, 1.2f), new WeedSpot(9.8f, 6.75f, 0.8f, 0.2f),
                },
                FenceLines = new[]
                {
                    (new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z)),
                    (new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z)),
                    (new Vector3(x, 0f, -z), new Vector3(x, 0f, z)),
                    (new Vector3(-x, 0f, z), new Vector3(-8.5f, 0f, z)),
                    (new Vector3(8.5f, 0f, z), new Vector3(x, 0f, z)),
                },
                Props = new[]
                {
                    House(z, 3),
                    new PropPlacement(PropKind.Shed, new Vector3(-9.5f, 0f, -6.5f)),
                    new PropPlacement(PropKind.Compost, new Vector3(9.8f, 0f, 7.6f)),
                    new PropPlacement(PropKind.Wheelbarrow, new Vector3(5f, 0f, -7.2f), yaw: 110f),
                    new PropPlacement(PropKind.RainBarrel, new Vector3(-12.2f, 0f, 8.6f)),
                    new PropPlacement(PropKind.Tree, new Vector3(8.5f, 0f, -2f)),
                },
                SpawnFeet = new Vector3(0f, 0.05f, -8.5f),
                SpawnYaw = 0f,
            };
        }

        public static IEnumerable<YardDefinition> All()
        {
            yield return City();
            yield return Manor();
            yield return Country();
        }
    }
}
