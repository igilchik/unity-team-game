using System;
using System.Collections.Generic;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-102: rewards of a finished wave, computed only from its numbers, so the same
    /// statistics always give the same reward (the result screen, Claim Rewards and tests agree).
    /// <list type="bullet">
    /// <item>Coins: coinsPerServe of every served request + satisfaction bonus growing with the wave number</item>
    /// <item>Energy: energyPerStar for every star of the wave rating</item>
    /// <item>Resort Value: valuePerServe per served request + valuePerStar per star</item>
    /// </list>
    /// </summary>
    public static class RewardCalculator
    {
        /// <param name="stats">numbers of every need in the wave (index = NeedType)</param>
        /// <param name="satisfaction">final Wave Satisfaction 0..100</param>
        /// <param name="waveIndex">0-based wave</param>
        public static WaveReward Calculate(GameConfig cfg, IList<NeedStats> stats, float satisfaction, int waveIndex)
        {
            var r = new WaveReward();
            if (cfg == null || stats == null) return r;
            satisfaction = Math.Max(0f, Math.Min(100f, satisfaction));

            int served = 0, coins = 0;
            for (int n = 0; n < stats.Count; n++)
            {
                if (stats[n] == null) continue;
                served += stats[n].served;
                coins += stats[n].served * Math.Max(0, cfg.Need(n).coinsPerServe);
            }
            int stars = cfg.Stars(satisfaction);
            r.coins = coins + SatisfactionBonus(cfg, satisfaction, waveIndex);
            r.energy = stars * Math.Max(0, cfg.energyPerStar);
            r.resortValue = served * Math.Max(0, cfg.valuePerServe) + stars * Math.Max(0, cfg.valuePerStar);
            return r;
        }

        /// <summary>Bonus coins for the satisfaction; later waves pay more (bonusGrowthPerWave per wave).</summary>
        public static int SatisfactionBonus(GameConfig cfg, float satisfaction, int waveIndex) =>
            (int)Math.Round(Math.Max(0f, satisfaction) * cfg.satisfactionCoinBonus * (1f + Math.Max(0, waveIndex) * cfg.bonusGrowthPerWave));
    }
}
