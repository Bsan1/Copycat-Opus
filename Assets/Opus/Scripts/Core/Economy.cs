using System;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>Points earned by the work (§3-D).</summary>
    public static class Rewards
    {
        public const float PerSquareMeterMowed = 1f;
        public const float PerSquareMeterWatered = 2f;
        public const int Weed = 8;
        public const int Overgrowth = 20;
        public const int Patch = 40;
        public const int Area = 250;
        public const int Find = 60;
        public const int Apple = 12;
    }

    /// <summary>Points wallet; fractional earnings (per m²) accumulate and turn into whole points.</summary>
    public sealed class Wallet
    {
        float pending;

        public int Points { get; private set; }
        /// <summary>Total earned in this session (before spending).</summary>
        public int Earned { get; private set; }
        public event Action<int> Changed;

        public void Add(float amount)
        {
            if (amount <= 0f)
                return;
            pending += amount;
            int whole = Mathf.FloorToInt(pending);
            if (whole <= 0)
                return;
            pending -= whole;
            Points += whole;
            Earned += whole;
            Changed?.Invoke(Points);
        }

        public bool TrySpend(int cost)
        {
            if (cost < 0 || Points < cost)
                return false;
            Points -= cost;
            Changed?.Invoke(Points);
            return true;
        }

        public void Restore(int points)
        {
            Points = Mathf.Max(0, points);
            pending = 0f;
            Changed?.Invoke(Points);
        }
    }

    public enum UpgradeId
    {
        WideDeck,
        StrongEngine,
        ThickLine,
        Pressure,
        RainHead,
    }

    public sealed class UpgradeDef
    {
        public UpgradeId Id;
        public string Name;
        public string[] LevelTexts;
        public int[] Costs;
        public int MaxLevel => Costs.Length;
    }

    /// <summary>
    /// Tool upgrades: bought with points in the workshop, levels kept in the save. The effects are applied to the tools
    /// with <see cref="Changed"/> (YardSession).
    /// </summary>
    public sealed class Upgrades
    {
        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef { Id = UpgradeId.WideDeck, Name = "Wide deck", Costs = new[] { 220, 520 },
                LevelTexts = new[] { "The mower cuts 15% wider in one pass.", "The mower cuts 30% wider in one pass." } },
            new UpgradeDef { Id = UpgradeId.StrongEngine, Name = "Strong engine", Costs = new[] { 180, 420 },
                LevelTexts = new[] { "Slows down less in long grass, 8% faster.", "No slowdown in long grass, 16% faster." } },
            new UpgradeDef { Id = UpgradeId.ThickLine, Name = "Thick line", Costs = new[] { 150, 380 },
                LevelTexts = new[] { "The trimmer cuts 25% wider and pulls weeds faster.", "The trimmer cuts 50% wider and pulls weeds twice as fast." } },
            new UpgradeDef { Id = UpgradeId.Pressure, Name = "Pressure hose", Costs = new[] { 160, 400 },
                LevelTexts = new[] { "Water 25% stronger, the narrow spray reaches 1 m further.", "Water 50% stronger, the narrow spray reaches 2 m further." } },
            new UpgradeDef { Id = UpgradeId.RainHead, Name = "Sprinkler head", Costs = new[] { 350 },
                LevelTexts = new[] { "A third spray mode for the hose that gently waters a wide area." } },
        };

        readonly int[] levels = new int[All.Length];

        public event Action<UpgradeId> Changed;

        public static UpgradeDef Def(UpgradeId id) => All[(int)id];
        public int Level(UpgradeId id) => levels[(int)id];
        public bool IsMaxed(UpgradeId id) => Level(id) >= Def(id).MaxLevel;
        /// <summary>Price of the next level (-1 if at the top).</summary>
        public int NextCost(UpgradeId id) => IsMaxed(id) ? -1 : Def(id).Costs[Level(id)];

        public bool TryBuy(UpgradeId id, Wallet wallet)
        {
            int cost = NextCost(id);
            if (cost < 0 || !wallet.TrySpend(cost))
                return false;
            levels[(int)id]++;
            Changed?.Invoke(id);
            return true;
        }

        public int[] Capture() => (int[])levels.Clone();

        public void Restore(int[] saved)
        {
            for (int i = 0; i < levels.Length; i++)
                levels[i] = saved != null && i < saved.Length ? Mathf.Clamp(saved[i], 0, All[i].MaxLevel) : 0;
            foreach (UpgradeDef def in All)
                Changed?.Invoke(def.Id);
        }

        // Effects
        public float DeckScale => 1f + 0.15f * Level(UpgradeId.WideDeck);
        public float MowerSpeedBonus => 0.08f * Level(UpgradeId.StrongEngine);
        /// <summary>Largest slowdown in long grass (base 0.1).</summary>
        public float MowerSlowdown => Level(UpgradeId.StrongEngine) switch { 0 => 0.1f, 1 => 0.05f, _ => 0f };
        public float TrimmerRadiusScale => 1f + 0.25f * Level(UpgradeId.ThickLine);
        public float TrimmerDamageScale => 1f + 0.5f * Level(UpgradeId.ThickLine);
        public float HoseRateScale => 1f + 0.25f * Level(UpgradeId.Pressure);
        public float HoseReachBonus => 1f * Level(UpgradeId.Pressure);
        public bool HasRainHead => Level(UpgradeId.RainHead) > 0;
    }
}
