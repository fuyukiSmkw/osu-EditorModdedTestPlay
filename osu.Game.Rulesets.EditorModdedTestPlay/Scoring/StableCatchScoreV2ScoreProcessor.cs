#nullable enable

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A ScoreV2 processor that reproduces osu!stable's osu!catch scoring for editor test play.
    /// </summary>
    /// <remarks>
    /// stable's formula is
    /// <c>round(((600000 + 400000 * (1 - f)) * S / maxSum + 400000 * f * tinyProgress + B) * multiplier)</c>,
    /// where <c>S</c> is the combo-weighted sum of fruits and droplets, <c>B</c> is the banana
    /// bonus, <c>tinyProgress</c> is the fraction of tiny droplets caught and
    /// <c>f = maxTiny / (maxTiny + maxFruit)</c> splits the score between the combo and
    /// tiny-droplet portions.
    /// <para>
    /// Fruits are worth 300 and droplets 100 in <c>S</c>; tiny droplets only contribute to the
    /// tiny-droplet ratio, and bananas only contribute to the bonus. Fruits and droplets use
    /// <c>clamp(log_4(combo), 0.5, log_4(200))</c> as their combo weight.
    /// </para>
    /// </remarks>
    internal sealed partial class StableCatchScoreV2ScoreProcessor : ScoreProcessor
    {
        private const double combo_base = 4;
        private const double combo_cap = 200;

        // stable's catch values. Tiny droplets do not contribute to the combo sum.
        private const int fruit_value = 300;
        private const int droplet_value = 100;
        private const int banana_value = 200;

        private const double accuracy_cutoff_x = 1;
        private const double accuracy_cutoff_s = 0.98;
        private const double accuracy_cutoff_a = 0.94;
        private const double accuracy_cutoff_b = 0.9;
        private const double accuracy_cutoff_c = 0.85;
        private const double accuracy_cutoff_d = 0;

        /// <summary>
        /// The combo-weighted score sum (<c>S</c> in stable's formula).
        /// </summary>
        private double stableScore;

        /// <summary>
        /// The bonus score sum (<c>B</c> in stable's formula).
        /// </summary>
        private double stableBonus;

        /// <summary>
        /// The number of tiny droplets caught during the current play.
        /// </summary>
        private int caughtTiny;

        private double maximumStableScore;
        private int maximumFruit;
        private int maximumTiny;

        /// <summary>
        /// The split between the combo and tiny-droplet portions (<c>f</c> in stable's formula).
        /// </summary>
        private double fruitTinyScale;

        /// <summary>
        /// Snapshots recorded for rewind support. Reverts are applied in reverse order.
        /// </summary>
        private readonly List<StableState> stateHistory = new List<StableState>();

        public StableCatchScoreV2ScoreProcessor(Ruleset ruleset)
            : base(ruleset)
        {
        }

        /// <summary>
        /// stable builds its maximum-score snapshot by walking the flat, time-sorted object list
        /// (its HP-drain simulation), not by recursively visiting each container's nested objects first.
        /// This distinction changes the combo values assigned to objects when a juice stream overlaps
        /// other top-level objects, and therefore changes <c>maxSum</c> and the final score.
        /// </summary>
        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
            => CatchBeatmap.GetPalpableObjects(beatmap.HitObjects);

        public override int GetBaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                // stable weights every caught fruit, droplet and tiny droplet equally in the
                // accuracy portion, so lazer's base accuracy calculation already matches it.
                case HitResult.Great:
                case HitResult.LargeTickHit:
                case HitResult.SmallTickHit:
                    return 300;

                // stable's ScoreChangeFruits gives bananas 200 base score, although bonus
                // judgements never enter the accuracy portion.
                case HitResult.LargeBonus:
                    return banana_value;

                default:
                    return 0;
            }
        }

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // comboProgress/accuracyProgress/bonusPortion are lazer's V1 accounting; ignore them.
            return computeUnmodifiedScore();
        }

        private double computeUnmodifiedScore()
        {
            // stable returns 0 for fruitless maps (maxCombo == 0).
            if (maximumFruit <= 0)
                return 0;

            double comboProgress = maximumStableScore > 0 ? stableScore / maximumStableScore : 0;
            double tinyProgress = maximumTiny > 0 ? (double)caughtTiny / maximumTiny : 0;

            return (600000 + 400000 * (1 - fruitTinyScale)) * comboProgress
                   + 400000 * fruitTinyScale * tinyProgress
                   + stableBonus;
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            stateHistory.Add(new StableState(stableScore, stableBonus, caughtTiny));

            applyResult(result);
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            if (stateHistory.Count == 0)
                return;

            StableState state = stateHistory[^1];
            stateHistory.RemoveAt(stateHistory.Count - 1);

            stableScore = state.Score;
            stableBonus = state.Bonus;
            caughtTiny = state.CaughtTiny;
        }

        private void applyResult(JudgementResult result)
        {
            switch (result.Type)
            {
                case HitResult.Great:
                    stableScore += fruit_value * comboFactor(result.ComboAfterJudgement);
                    break;

                case HitResult.LargeTickHit:
                    stableScore += droplet_value * comboFactor(result.ComboAfterJudgement);
                    break;

                case HitResult.SmallTickHit:
                    // Tiny droplets only feed the tiny-droplet ratio.
                    caughtTiny++;
                    break;

                case HitResult.LargeBonus:
                    // Bananas contribute to the bonus portion only (no combo weight).
                    stableBonus += banana_value;
                    break;
            }
        }

        /// <summary>
        /// stable's catch combo weight: <c>clamp(log_4(combo), 0.5, log_4(200))</c>.
        /// </summary>
        private static double comboFactor(int combo)
        {
            return Math.Min(Math.Max(0.5, Math.Log(combo, combo_base)), Math.Log(combo_cap, combo_base));
        }

        protected override void Reset(bool storeResults)
        {
            if (storeResults)
            {
                maximumStableScore = stableScore;

                // The autoplay simulation judges every fruit, droplet and tiny droplet.
                // Large droplets are *purposefully* not counted in the split, to match stable.
                maximumFruit = ScoreResultCounts.GetValueOrDefault(HitResult.Great);
                maximumTiny = ScoreResultCounts.GetValueOrDefault(HitResult.SmallTickHit);

                int divisor = maximumFruit + maximumTiny;
                fruitTinyScale = divisor == 0 ? 0 : (double)maximumTiny / divisor;

                // Banana bonus is not part of stable's normalised maximum.
                stableBonus = 0;
            }

            // base.Reset() calls updateScore() before clearing the simulation state. The maximum
            // fields must already be populated by then, otherwise MaximumTotalScore is stored as 0.
            base.Reset(storeResults);

            stableScore = 0;
            stableBonus = 0;
            caughtTiny = 0;
            stateHistory.Clear();
        }

        public override void PopulateScore(ScoreInfo score)
        {
            base.PopulateScore(score);

            double unmodified = computeUnmodifiedScore();
            double multiplier = getScoreMultiplier();

            score.TotalScoreWithoutMods = (long)Math.Round(unmodified);
            // stable rounds the final multiplied score only once.
            score.TotalScore = (long)Math.Round(unmodified * multiplier);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            // stable computes accuracy as a float from the raw result counts and uses strict
            // greater-than comparisons. Reproducing the float arithmetic matters at exact
            // boundaries (for example 47/50 = 94% is a B in stable, not an A).
            float stableAccuracy = computeStableAccuracy(results);

            if (stableAccuracy == 1f)
                return ScoreRank.X;
            if ((double)stableAccuracy > accuracy_cutoff_s)
                return ScoreRank.S;
            if ((double)stableAccuracy > accuracy_cutoff_a)
                return ScoreRank.A;
            if ((double)stableAccuracy > accuracy_cutoff_b)
                return ScoreRank.B;
            if ((double)stableAccuracy > accuracy_cutoff_c)
                return ScoreRank.C;

            return ScoreRank.D;
        }

        /// <summary>
        /// stable's catch accuracy: every caught fruit, droplet and tiny droplet contributes
        /// equally. Missed droplets count as misses, while missed tiny droplets count as katu.
        /// </summary>
        private static float computeStableAccuracy(IReadOnlyDictionary<HitResult, int> results)
        {
            int count300 = results.GetValueOrDefault(HitResult.Great);
            int count100 = results.GetValueOrDefault(HitResult.LargeTickHit);
            int count50 = results.GetValueOrDefault(HitResult.SmallTickHit);
            int countMiss = results.GetValueOrDefault(HitResult.Miss) + results.GetValueOrDefault(HitResult.LargeTickMiss);
            int countKatu = results.GetValueOrDefault(HitResult.SmallTickMiss);

            int total = count300 + count100 + count50 + countMiss + countKatu;

            return total == 0
                ? 1f
                : (float)(count300 + count100 + count50) / (float)total;
        }

        public override double AccuracyCutoffFromRank(ScoreRank rank)
        {
            switch (rank)
            {
                case ScoreRank.X:
                case ScoreRank.XH:
                    return accuracy_cutoff_x;

                case ScoreRank.S:
                case ScoreRank.SH:
                    return accuracy_cutoff_s;

                case ScoreRank.A:
                    return accuracy_cutoff_a;

                case ScoreRank.B:
                    return accuracy_cutoff_b;

                case ScoreRank.C:
                    return accuracy_cutoff_c;

                case ScoreRank.D:
                    return accuracy_cutoff_d;

                default:
                    throw new ArgumentOutOfRangeException(nameof(rank), rank, null);
            }
        }

        private double getScoreMultiplier()
        {
            IBeatmap? beatmap = Beatmap.Value;

            if (beatmap == null)
                return 1;

            var calculator = Ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmap.BeatmapInfo.Difficulty));
            return calculator.CalculateFor(Mods.Value);
        }

        private readonly struct StableState
        {
            public readonly double Score;
            public readonly double Bonus;
            public readonly int CaughtTiny;

            public StableState(double score, double bonus, int caughtTiny)
            {
                Score = score;
                Bonus = bonus;
                CaughtTiny = caughtTiny;
            }
        }
    }
}
