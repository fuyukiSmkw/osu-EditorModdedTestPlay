#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A ScoreV2 processor that reproduces osu!stable's osu! standard scoring for editor test play.
    /// </summary>
    /// <remarks>
    /// stable's formula is
    /// <c>round((700000 * S / maxSum + 300000 * accuracy^10 * completion + B) * multiplier)</c>,
    /// where <c>S</c> is the combo-weighted sum of hit values, <c>B</c> is the bonus sum and
    /// <c>completion</c> is the fraction of top-level hit objects which have been judged.
    /// <para>
    /// Sliders are forced into classic behaviour by <see cref="StableOsuBeatmapProcessor"/>, and the
    /// custom slider/spinner drawables emit stable's aggregate result types directly. The processor
    /// can therefore use the parent result type as-is for the combo-weighted sum, and delegate combo,
    /// accuracy and raw portion bookkeeping (including rewinds) to the base <see cref="ScoreProcessor"/>.
    /// </para>
    /// </remarks>
    internal sealed partial class StableOsuScoreV2ScoreProcessor : ScoreProcessor
    {
        // stable weights every result by `1 + combo / 10`.
        private const double combo_weight_divisor = 10;

        // stable hit values for the osu! ruleset.
        private const int slider_tick_value = 10;
        private const int slider_part_value = 30;

        // stable's ScoreV2 result for a spinner tick (0x1) and bonus spin (0x4000). Note that the
        // bonus value differs from the 1100 used by the legacy (ScoreV1) simulator.
        private const int spinner_tick_value = 100;
        private const int spinner_bonus_tick_value = 500;

        /// <summary>
        /// Slider tails which were missed. stable still awards the slider parent's 300/100/50 value,
        /// but the parent's combo increment only stands in for the tail when the tail was hit.
        /// </summary>
        private readonly HashSet<SliderTailCircle> missedTails = new HashSet<SliderTailCircle>();

        /// <summary>
        /// The most recently computed unrounded score. <see cref="ScoreInfo.TotalScore"/> is rounded
        /// from this value once, matching stable's single-rounding rule.
        /// </summary>
        private double lastUnmodifiedScore;

        // The NuGet base class snapshots ComboAfterJudgement before ApplyScoreChange(), while
        // g0v0 snapshots it after. The setters are internal, so the tail correction has to be
        // written back through reflection for delta-based rewinds to restore the right combo.
        private static readonly PropertyInfo combo_after_judgement = typeof(JudgementResult).GetProperty(nameof(JudgementResult.ComboAfterJudgement))!;
        private static readonly PropertyInfo highest_combo_after_judgement = typeof(JudgementResult).GetProperty(nameof(JudgementResult.HighestComboAfterJudgement))!;

        public StableOsuScoreV2ScoreProcessor(Ruleset ruleset)
            : base(ruleset)
        {
        }

        /// <summary>
        /// Enumerates hit objects in the order osu!stable's max-score simulation uses.
        /// </summary>
        /// <remarks>
        /// stable's simulation groups the 30-point slider head/repeats before the 10-point ticks,
        /// then judges the tail last (the parent supplies its combo). Because the score is normalised
        /// against the maximum produced by that simulation, the same ordering has to be reproduced
        /// here for the final score to match stable exactly.
        /// </remarks>
        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
        {
            foreach (HitObject hitObject in beatmap.HitObjects)
            {
                if (hitObject is Slider slider)
                {
                    // stable's simulation keeps the tail last: its combo is supplied by the parent
                    // judgement, and the tick `-2` adjustment below accounts for moving the tail
                    // after the ticks.
                    foreach (HitObject nested in slider.NestedHitObjects.OrderBy(o => o is SliderTailCircle ? 2 : o is SliderTick ? 1 : 0))
                        yield return nested;

                    yield return slider;
                }
                else
                {
                    foreach (HitObject nested in enumerateNested(hitObject))
                        yield return nested;

                    yield return hitObject;
                }
            }
        }

        private static IEnumerable<HitObject> enumerateNested(HitObject hitObject)
        {
            foreach (HitObject nested in hitObject.NestedHitObjects)
            {
                foreach (HitObject deeper in enumerateNested(nested))
                    yield return deeper;

                yield return nested;
            }
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            // Accuracy in stable osu! ScoreV2 only considers top-level (circle/slider/spinner) judgements.
            // Returning 0 for ticks and bonuses keeps the base Accuracy bindable equal to stable's accuracy.
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return 300;

                case HitResult.Good:
                    return 200;

                case HitResult.Ok:
                    return 100;

                case HitResult.Meh:
                    return 50;

                default:
                    return 0;
            }
        }

        protected override double GetComboScoreChange(JudgementResult result)
        {
            if (!result.IsHit)
                return 0;

            double baseScore;

            if (result.HitObject is SliderTick)
                baseScore = slider_tick_value;
            else if (result.HitObject is SliderHeadCircle || result.HitObject is SliderTailCircle || result.HitObject is SliderRepeat)
                baseScore = slider_part_value;
            else if (result.HitObject is HitCircle || result.HitObject is Slider || result.HitObject is Spinner)
            {
                baseScore = result.Type switch
                {
                    HitResult.Great => 300,
                    HitResult.Ok => 100,
                    HitResult.Meh => 50,
                    _ => 0,
                };
            }
            else
                baseScore = GetBaseScoreForResult(result.Type);

            if (baseScore <= 0)
                return 0;

            int combo = result.ComboAfterJudgement;

            // A missed tail prevents the parent from supplying the tail's combo increment.
            if (result.HitObject is Slider slider && missedTails.Contains(slider.TailCircle))
                combo = result.ComboAtJudgement;

            if (result.HitObject is SliderTailCircle)
                combo++;

            // stable's simulation judges the 30-point checkpoints before the 10-point ticks.
            // This adjustment keeps the simulated tick weighting equal to stable's ordering.
            if (result.HitObject is SliderTick && IsSimulating)
                combo = Math.Max(0, combo - 2);

            return baseScore * (1 + combo / combo_weight_divisor);
        }

        protected override double GetBonusScoreChange(JudgementResult result)
        {
            if (result.HitObject is SpinnerBonusTick)
                return spinner_bonus_tick_value;

            if (result.HitObject is SpinnerTick)
                return spinner_tick_value;

            return base.GetBonusScoreChange(result);
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            if (result.HitObject is SliderTailCircle tail && !result.IsHit)
                missedTails.Add(tail);

            // The classic parent judgement supplies the tail combo only when the tail was hit.
            if (result.HitObject is Slider slider && result.IsHit && missedTails.Contains(slider.TailCircle))
            {
                Combo.Value--;
                HighestCombo.Value = Math.Max(result.HighestComboAtJudgement, Combo.Value);

                // Unlike g0v0's base class, the NuGet base snapshots ComboAfterJudgement before
                // ApplyScoreChange, so the tail correction has to be written back manually for
                // delta-based rewinds to restore the right combo.
                combo_after_judgement.SetValue(result, Combo.Value);
                highest_combo_after_judgement.SetValue(result, HighestCombo.Value);
            }
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            if (result.HitObject is SliderTailCircle tail)
                missedTails.Remove(tail);
        }

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // Under ScoreV2, only top-level judgements count towards completion. The base
            // accuracyProgress also counts nested slider ticks, so it cannot be used directly.
            int maximumHits = MaximumResultCounts.Where(r => r.Key.IsBasic()).Sum(r => r.Value);
            int hits = ScoreResultCounts.Where(r => r.Key.IsBasic()).Sum(r => r.Value);
            double completion = maximumHits > 0 ? (double)hits / maximumHits : 1;

            lastUnmodifiedScore = 700000 * comboProgress
                                  + 300000 * Math.Pow(Accuracy.Value, 10) * completion
                                  + bonusPortion;

            return lastUnmodifiedScore;
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);

            missedTails.Clear();
            lastUnmodifiedScore = 0;
        }

        public override void PopulateScore(ScoreInfo score)
        {
            base.PopulateScore(score);

            // base.PopulateScore applies the multiplier to the already-rounded unmodified score.
            // stable rounds the unmodified score and the multiplied score independently.
            score.TotalScore = (long)Math.Round(lastUnmodifiedScore * getScoreMultiplier());
        }

        private double getScoreMultiplier()
        {
            IBeatmap? beatmap = Beatmap.Value;

            if (beatmap == null)
                return 1;

            var calculator = Ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmap.BeatmapInfo.Difficulty));
            return calculator.CalculateFor(Mods.Value);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results);

            // Match osu!'s rank handling: a score with misses cannot be S or SS.
            if ((rank == ScoreRank.S || rank == ScoreRank.X) && results.GetValueOrDefault(HitResult.Miss) > 0)
                rank = ScoreRank.A;

            return rank;
        }
    }
}
