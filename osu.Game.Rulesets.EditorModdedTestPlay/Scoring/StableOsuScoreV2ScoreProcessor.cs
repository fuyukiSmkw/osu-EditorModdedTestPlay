#nullable enable

using System;
using System.Collections.Generic;
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
    /// Sliders are forced into classic behaviour by <see cref="StableOsuBeatmapProcessor"/>, which means
    /// the slider head/ticks/repeats/tail emit their own nested judgements (and affect score) while the
    /// displayed judgement remains the aggregated slider result.
    /// </para>
    /// </remarks>
    internal sealed partial class StableOsuScoreV2ScoreProcessor : ScoreProcessor
    {
        // stable weights every result by `1 + combo / 10`.
        private const double combo_weight_divisor = 10;

        // stable hit values for the osu! ruleset.
        private const int slider_tick_value = 10;
        private const int slider_part_value = 30;
        private const int spinner_tick_value = 100;

        // stable's ScoreV2 result for a bonus spinner spin (0x4000). Note that this differs from the
        // 1100 used by the legacy (ScoreV1) simulator.
        private const int spinner_bonus_tick_value = 500;

        /// <summary>
        /// The combo-weighted score sum (<c>S</c> in stable's formula).
        /// </summary>
        private double stableScore;

        /// <summary>
        /// The bonus score sum (<c>B</c> in stable's formula).
        /// </summary>
        private double stableBonus;

        /// <summary>
        /// stable's per-result combo counter. Unlike <see cref="ScoreProcessor.Combo"/>, this also counts
        /// nested slider objects and is only used to weight <see cref="stableScore"/>.
        /// </summary>
        private int stableCombo;

        /// <summary>
        /// stable's highest combo achieved during the current play.
        /// </summary>
        private int stableHighestCombo;

        /// <summary>
        /// The number of top-level (circle/slider/spinner) judgements applied so far.
        /// </summary>
        private int topLevelJudged;

        private double maximumStableScore;
        private int maximumTopLevelJudged;

        /// <summary>
        /// Snapshots recorded for rewind support. Reverts are applied in reverse order.
        /// </summary>
        private readonly List<StableState> stateHistory = new List<StableState>();

        public StableOsuScoreV2ScoreProcessor(Ruleset ruleset)
            : base(ruleset)
        {
        }

        /// <summary>
        /// Enumerates hit objects in the order osu!stable's max-score simulation uses.
        /// </summary>
        /// <remarks>
        /// stable's simulation adds all of a slider's 30-point parts (head, repeats, tail) before its
        /// 10-point ticks, regardless of the order in which they are judged during gameplay. Because
        /// the score is normalised against the maximum produced by that simulation, the same ordering
        /// has to be reproduced here for the final score to match stable exactly.
        /// </remarks>
        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
        {
            foreach (HitObject hitObject in beatmap.HitObjects)
            {
                if (hitObject is Slider slider)
                {
                    foreach (HitObject nested in slider.NestedHitObjects)
                    {
                        if (nested is SliderHeadCircle || nested is SliderRepeat || nested is SliderTailCircle)
                            yield return nested;
                    }

                    foreach (HitObject nested in slider.NestedHitObjects)
                    {
                        if (nested is SliderTick)
                            yield return nested;
                    }

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

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // comboProgress/accuracyProgress/bonusPortion are lazer's V1 accounting; ignore them.
            return computeUnmodifiedScore();
        }

        private double computeUnmodifiedScore()
        {
            if (maximumStableScore <= 0 || maximumTopLevelJudged <= 0)
                return 0;

            double completion = (double)Math.Min(topLevelJudged, maximumTopLevelJudged) / maximumTopLevelJudged;

            return 700000 * stableScore / maximumStableScore
                   + 300000 * Math.Pow(Accuracy.Value, 10) * completion
                   + stableBonus;
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            stateHistory.Add(new StableState(stableScore, stableBonus, stableCombo, stableHighestCombo, topLevelJudged));

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
            stableCombo = state.Combo;
            stableHighestCombo = state.HighestCombo;
            topLevelJudged = state.TopLevelJudged;
        }

        private void applyResult(JudgementResult result)
        {
            HitResult type = result.Type;

            // IgnoreHit / IgnoreMiss are not scorable and must not affect the stable counters.
            if (!type.IsScorable())
                return;

            // Spinner ticks and spinner bonus ticks are added straight to stable's bonus portion (B).
            if (type == HitResult.SmallBonus)
            {
                stableBonus += spinner_tick_value;
                return;
            }

            if (type == HitResult.LargeBonus)
            {
                stableBonus += spinner_bonus_tick_value;

                // Depending on the spinner's difficulty, stable may also award the regular
                // 100-point tick on the same full spin. lazer's spinner model replaces the normal
                // tick with the bonus tick, so it has to be added back manually where applicable.
                if (result.HitObject is StableSpinnerBonusTick { AwardsNormalTick: true })
                    stableBonus += spinner_tick_value;

                return;
            }

            HitObject hitObject = result.HitObject;

            int value;
            bool increasesCombo;
            bool isTopLevel;

            if (hitObject is Slider)
            {
                // stable's slider aggregate does not increase combo, but still contributes to S using
                // the combo number inherited from the last nested result.
                value = getTopLevelValue(type);
                increasesCombo = false;
                isTopLevel = true;
            }
            else if (hitObject is Spinner)
            {
                value = getTopLevelValue(type);
                increasesCombo = true;
                isTopLevel = true;
            }
            else if (hitObject is HitCircle && hitObject is not SliderHeadCircle && hitObject is not SliderTailCircle && hitObject is not SliderRepeat)
            {
                value = getTopLevelValue(type);
                increasesCombo = true;
                isTopLevel = true;
            }
            else if (hitObject is SliderTick)
            {
                value = type.IsHit() ? slider_tick_value : 0;
                increasesCombo = true;
                isTopLevel = false;
            }
            else if (hitObject is SliderHeadCircle || hitObject is SliderRepeat || hitObject is SliderTailCircle)
            {
                value = type.IsHit() ? slider_part_value : 0;
                increasesCombo = true;
                isTopLevel = false;
            }
            else
            {
                // Unknown object type: fall back to the top-level value without affecting completion.
                value = getTopLevelValue(type);
                increasesCombo = type.IncreasesCombo();
                isTopLevel = false;
            }

            if (increasesCombo)
                stableCombo = type.IsHit() ? stableCombo + 1 : 0;

            stableHighestCombo = Math.Max(stableHighestCombo, stableCombo);

            if (value > 0)
                stableScore += value * (1 + stableCombo / combo_weight_divisor);

            if (isTopLevel)
                topLevelJudged++;
        }

        private static int getTopLevelValue(HitResult type)
        {
            switch (type)
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

        protected override void Reset(bool storeResults)
        {
            if (storeResults)
            {
                maximumStableScore = stableScore;
                maximumTopLevelJudged = topLevelJudged;

                // Bonus score is not part of stable's normalised maximum.
                stableBonus = 0;
            }

            base.Reset(storeResults);

            stableScore = 0;
            stableBonus = 0;
            stableCombo = 0;
            stableHighestCombo = 0;
            topLevelJudged = 0;
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

            // stable's live combo counts nested slider parts but not the aggregated slider result,
            // which differs from lazer's base combo accounting.
            score.Combo = stableCombo;
            score.MaxCombo = stableHighestCombo;
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

        private readonly struct StableState
        {
            public readonly double Score;
            public readonly double Bonus;
            public readonly int Combo;
            public readonly int HighestCombo;
            public readonly int TopLevelJudged;

            public StableState(double score, double bonus, int combo, int highestCombo, int topLevelJudged)
            {
                Score = score;
                Bonus = bonus;
                Combo = combo;
                HighestCombo = highestCombo;
                TopLevelJudged = topLevelJudged;
            }
        }
    }
}
