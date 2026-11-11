#nullable enable

using System;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Mods;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// The osu! ScoreV2 multiplier as used by osu!stable.
    /// </summary>
    /// <remarks>
    /// Compared to lazer's current V2 multiplier, stable ScoreV2:
    /// <list type="bullet">
    /// <item><description>ignores NoFail (x1.0 instead of x0.5),</description></item>
    /// <item><description>uses a flat x1.1 for HardRock,</description></item>
    /// <item><description>uses a flat x1.2 for DoubleTime/Nightcore,</description></item>
    /// <item><description>uses a flat x0.3 for HalfTime/Daycore,</description></item>
    /// <item><description>gives the Classic mod x1.0.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class StableOsuScoreV2MultiplierCalculator : ScoreMultiplierCalculator
    {
        public StableOsuScoreV2MultiplierCalculator(ScoreMultiplierContext context)
            : base(context)
        {
            #region Difficulty Reduction

            Single<OsuModEasy>(hasMultiplier: 0.5);
            // NoFail is intentionally not registered: stable ScoreV2 gives it x1.0.
            Single<OsuModHalfTime>(hasMultiplier: 0.3);
            Single<OsuModDaycore>(hasMultiplier: 0.3);

            #endregion

            #region Difficulty Increase

            Single<OsuModHardRock>(hasMultiplier: 1.1);
            Single<OsuModDoubleTime>(hasMultiplier: 1.2);
            Single<OsuModNightcore>(hasMultiplier: 1.2);
            Single<OsuModHidden>(hasMultiplier: 1.06);
            Single<OsuModFlashlight>(hasMultiplier: 1.12);

            #endregion

            #region Automation

            Single<OsuModRelax>(hasMultiplier: 0);
            Single<OsuModAutopilot>(hasMultiplier: 0);
            Single<OsuModSpunOut>(hasMultiplier: 0.9);

            #endregion

            // OsuModClassic and every other mod keep the default x1.0 multiplier.
        }
    }

    /// <summary>
    /// The taiko ScoreV2 multiplier as used by osu!stable.
    /// </summary>
    /// <remarks>
    /// stable applies the same generic multipliers to taiko, but without the osu!-specific
    /// HardRock/DoubleTime special cases. NoFail is removed before the calculation, so it is x1.0.
    /// </remarks>
    internal sealed class StableTaikoScoreV2MultiplierCalculator : ScoreMultiplierCalculator
    {
        public StableTaikoScoreV2MultiplierCalculator(ScoreMultiplierContext context)
            : base(context)
        {
            Single<TaikoModEasy>(hasMultiplier: 0.5);
            // NoFail is intentionally not registered: stable ScoreV2 gives it x1.0.
            Single<TaikoModHalfTime>(hasMultiplier: 0.3);
            Single<TaikoModDaycore>(hasMultiplier: 0.3);

            Single<TaikoModHidden>(hasMultiplier: 1.06);
            Single<TaikoModHardRock>(hasMultiplier: 1.06);
            Single<TaikoModDoubleTime>(hasMultiplier: 1.12);
            Single<TaikoModNightcore>(hasMultiplier: 1.12);
            Single<TaikoModFlashlight>(hasMultiplier: 1.12);

            Single<TaikoModRelax>(hasMultiplier: 0);

            // TaikoModClassic and every other mod keep the default x1.0 multiplier.
        }
    }

    /// <summary>
    /// The catch ScoreV2 multiplier as used by osu!stable.
    /// </summary>
    /// <remarks>
    /// stable removes HardRock, Hidden, DoubleTime and Nightcore before calculating the multiplier,
    /// so they are all x1.0 under ScoreV2. NoFail is also removed.
    /// </remarks>
    internal sealed class StableCatchScoreV2MultiplierCalculator : ScoreMultiplierCalculator
    {
        public StableCatchScoreV2MultiplierCalculator(ScoreMultiplierContext context)
            : base(context)
        {
            Single<CatchModEasy>(hasMultiplier: 0.5);
            // NoFail is intentionally not registered: stable ScoreV2 gives it x1.0.
            Single<CatchModHalfTime>(hasMultiplier: 0.3);
            Single<CatchModDaycore>(hasMultiplier: 0.3);

            Single<CatchModFlashlight>(hasMultiplier: 1.12);
            Single<CatchModRelax>(hasMultiplier: 0);

            // HardRock/Hidden/DoubleTime/Nightcore are intentionally not registered: stable
            // removes them under ScoreV2, so they keep the default x1.0 multiplier.
        }
    }

    /// <summary>
    /// The mania ScoreV2 multiplier as used by osu!stable.
    /// </summary>
    /// <remarks>
    /// stable gives mania a x0.5 HalfTime multiplier (unlike x0.3 for other modes), removes NoFail,
    /// and scales score depending on whether the selected key mod increases or decreases the key count.
    /// </remarks>
    internal sealed class StableManiaScoreV2MultiplierCalculator : ScoreMultiplierCalculator
    {
        public StableManiaScoreV2MultiplierCalculator(ScoreMultiplierContext context)
            : base(context)
        {
            Single<ManiaModEasy>(hasMultiplier: 0.5);
            // NoFail is intentionally not registered: stable ScoreV2 gives it x1.0.
            Single<ManiaModHalfTime>(hasMultiplier: 0.5);
            Single<ManiaModDaycore>(hasMultiplier: 0.5);

            // All key mods share the same key-count-dependent multiplier.
            Single<ManiaModKey1>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey2>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey3>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey4>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey5>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey6>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey7>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey8>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey9>(hasMultiplier: m => keyModMultiplier(m));
            Single<ManiaModKey10>(hasMultiplier: m => keyModMultiplier(m));

            // ManiaModClassic and every other mod keep the default x1.0 multiplier.
        }

        private double keyModMultiplier(ManiaKeyMod keyMod)
        {
            int originalKeyCount = (int)Math.Round(Context.BeatmapDifficultyWithoutMods.CircleSize);
            int newKeyCount = keyMod.KeyCount;

            if (newKeyCount > originalKeyCount)
                return 0.9;

            if (newKeyCount < originalKeyCount)
                return 0.9 - 0.04 * (originalKeyCount - newKeyCount);

            return 1;
        }
    }
}
