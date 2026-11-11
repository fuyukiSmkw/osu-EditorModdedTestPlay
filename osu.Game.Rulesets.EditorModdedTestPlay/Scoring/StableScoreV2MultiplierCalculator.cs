#nullable enable

using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;

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
    internal sealed class StableScoreV2MultiplierCalculator : ScoreMultiplierCalculator
    {
        public StableScoreV2MultiplierCalculator(ScoreMultiplierContext context)
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
}
