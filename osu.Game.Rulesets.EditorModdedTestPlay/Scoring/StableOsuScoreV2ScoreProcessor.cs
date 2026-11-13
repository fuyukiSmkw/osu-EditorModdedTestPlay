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
    /// <para>
    /// Slider and spinner aggregate tiers are recomputed from stable's rules (which consider the
    /// slider head's timing tier and the spinner's half-spin count) because lazer's classic
    /// aggregates tier them differently near the boundaries. lazer's own aggregate result type is
    /// left untouched, so the <see cref="ScoreProcessor.Accuracy"/> bindable can still diverge from
    /// stable in those edge cases.
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

        /// <summary>
        /// Per top-level slider/spinner progress used to recompute stable's aggregate judgement
        /// tiers (see <see cref="getSliderAggregateValue"/> and <see cref="getSpinnerAggregateValue"/>).
        /// </summary>
        private readonly Dictionary<HitObject, TopLevelProgress> topLevelProgress = new Dictionary<HitObject, TopLevelProgress>();

        /// <summary>
        /// Maps nested hit objects to their top-level parent, built lazily from <see cref="Beatmap"/>.
        /// Nested results can interleave in time (e.g. overlapping sliders), so progress is keyed by
        /// the parent object rather than tracked globally.
        /// </summary>
        private Dictionary<HitObject, HitObject>? nestedParents;
        private IBeatmap? nestedParentsBeatmap;

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
            // Nested results (slider parts / spinner ticks) mutate their parent's progress entry,
            // so capture the previous state for rewind before applying the result.
            ProgressSnapshot progressSnapshot = default;
            bool hasProgressSnapshot = false;

            if (getNestedParents().TryGetValue(result.HitObject, out HitObject? parent))
            {
                topLevelProgress.TryGetValue(parent, out TopLevelProgress? entry);
                progressSnapshot = new ProgressSnapshot(parent, entry);
                hasProgressSnapshot = true;
            }

            stateHistory.Add(new StableState(stableScore, stableBonus, stableCombo, stableHighestCombo, topLevelJudged, progressSnapshot, hasProgressSnapshot));

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

            if (state.HasProgressSnapshot)
            {
                if (state.Progress.Existed)
                {
                    TopLevelProgress progress = getOrCreateProgress(state.Progress.Key);
                    progress.NestedHitCount = state.Progress.NestedHitCount;
                    progress.HeadJudged = state.Progress.HeadJudged;
                    progress.HeadHit = state.Progress.HeadHit;
                    progress.HeadTimeOffset = state.Progress.HeadTimeOffset;
                    progress.SpinnerHitTicks = state.Progress.SpinnerHitTicks;
                }
                else
                    topLevelProgress.Remove(state.Progress.Key);
            }
        }

        private void applyResult(JudgementResult result)
        {
            HitResult type = result.Type;

            // IgnoreHit / IgnoreMiss are not scorable and must not affect the stable counters.
            if (!type.IsScorable())
                return;

            // Spinner ticks and spinner bonus ticks are added straight to stable's bonus portion (B).
            // Every hit tick also represents one completed full rotation, which feeds into stable's
            // spinner aggregate tier.
            if (type == HitResult.SmallBonus)
            {
                stableBonus += spinner_tick_value;
                addSpinnerHitTick(result.HitObject);
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

                addSpinnerHitTick(result.HitObject);
                return;
            }

            HitObject hitObject = result.HitObject;

            int value;
            bool increasesCombo;
            bool isTopLevel;

            if (hitObject is Slider slider)
            {
                // stable's slider aggregate does not increase combo, but still contributes to S using
                // the combo number inherited from the last nested result. Its tier is recomputed from
                // stable's rules rather than lazer's, since lazer's classic aggregate ignores the
                // slider head's timing tier.
                value = getSliderAggregateValue(slider, type);
                increasesCombo = false;
                isTopLevel = true;
            }
            else if (hitObject is Spinner spinner)
            {
                // stable tiers spinners by half-spins achieved, which can differ from lazer's
                // progress-based tiers near the boundaries.
                value = getSpinnerAggregateValue(spinner, type);
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

                // stable's slider tail miss (-524288) only drains HP without resetting the combo,
                // unlike head/repeat misses (-262144).
                increasesCombo = hitObject is SliderTailCircle ? type.IsHit() : true;
                isTopLevel = false;
            }
            else
            {
                // Unknown object type: fall back to the top-level value without affecting completion.
                value = getTopLevelValue(type);
                increasesCombo = type.IncreasesCombo();
                isTopLevel = false;
            }

            if (hitObject is SliderHeadCircle || hitObject is SliderRepeat || hitObject is SliderTailCircle || hitObject is SliderTick)
                updateSliderProgress(result, hitObject);

            if (increasesCombo)
                stableCombo = value > 0 ? stableCombo + 1 : 0;

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

        /// <summary>
        /// Recomputes stable's slider aggregate tier from the tracked nested results.
        /// </summary>
        /// <remarks>
        /// stable (SliderOsu.ScoreIncrease) awards 300 only when every nested object was hit and the
        /// head was a 300; 100 when at least half were hit and the head was 100-or-better; 50 when
        /// anything was hit; and a miss otherwise. lazer's classic aggregate ignores the head's
        /// timing tier, so the tier has to be recomputed here from the head's time offset.
        /// </remarks>
        private int getSliderAggregateValue(Slider slider, HitResult lazerType)
        {
            if (!topLevelProgress.TryGetValue(slider, out TopLevelProgress? progress) || !progress.HeadJudged)
                return getTopLevelValue(lazerType);

            int total = slider.NestedHitObjects.Count;

            if (total <= 0)
                return getTopLevelValue(lazerType);

            int hit = progress.NestedHitCount;
            int headTier = getHeadTier(progress);

            if (hit == total && headTier >= 300)
                return 300;

            if (hit * 2 >= total && headTier >= 100)
                return 100;

            if (hit > 0)
                return 50;

            return 0;
        }

        /// <summary>
        /// Determines the stable tier of the slider head from its time offset, using stable's
        /// integer-truncated hit windows (strictly less-than comparisons).
        /// </summary>
        private int getHeadTier(TopLevelProgress progress)
        {
            if (!progress.HeadJudged || !progress.HeadHit)
                return 0;

            (int window300, int window100, int window50) = getStableHitWindows();
            double error = Math.Abs(progress.HeadTimeOffset);

            if (error < window300)
                return 300;

            if (error < window100)
                return 100;

            if (error < window50)
                return 50;

            return 0;
        }

        /// <summary>
        /// Recomputes stable's spinner aggregate tier from the tracked hit ticks.
        /// </summary>
        /// <remarks>
        /// stable (SpinnerOsu.ScoreIncrease) scores spinners in half-spins: 300 for more than
        /// <c>R</c> half-spins, 100 for more than <c>R - 2</c>, 50 for at least <c>R / 4</c> and a
        /// miss below that, where <c>R</c> is the half-spin requirement. Each lazer hit tick
        /// represents one completed full rotation, i.e. two half-spins.
        /// </remarks>
        private int getSpinnerAggregateValue(Spinner spinner, HitResult lazerType)
        {
            if (!topLevelProgress.TryGetValue(spinner, out TopLevelProgress? progress))
                return getTopLevelValue(lazerType);

            double od = Beatmap.Value?.BeatmapInfo.Difficulty.OverallDifficulty ?? 5;
            int rotationRequirement = (int)(spinner.Duration / 1000 * IBeatmapDifficultyInfo.DifficultyRange(od, 3.0, 5.0, 7.5));
            int halfSpins = progress.SpinnerHitTicks * 2;

            if (rotationRequirement == 0)
                return 300;

            if (halfSpins < Math.Max(0, rotationRequirement / 4))
                return 0;

            if (halfSpins > rotationRequirement)
                return 300;

            if (halfSpins > rotationRequirement - 2)
                return 100;

            return 50;
        }

        /// <summary>
        /// Returns stable's osu! hit windows, which are truncated to integers
        /// (HitObjectManager.cs: <c>(int)MapDifficultyRange(...)</c>).
        /// </summary>
        private (int Window300, int Window100, int Window50) getStableHitWindows()
        {
            double od = Beatmap.Value?.BeatmapInfo.Difficulty.OverallDifficulty ?? 5;

            int window300 = (int)IBeatmapDifficultyInfo.DifficultyRange(od, 80.0, 50.0, 20.0);
            int window100 = (int)IBeatmapDifficultyInfo.DifficultyRange(od, 140.0, 100.0, 60.0);
            int window50 = (int)IBeatmapDifficultyInfo.DifficultyRange(od, 200.0, 150.0, 100.0);

            return (window300, window100, window50);
        }

        private void updateSliderProgress(JudgementResult result, HitObject nested)
        {
            if (!getNestedParents().TryGetValue(nested, out HitObject? parent) || parent is not Slider)
                return;

            TopLevelProgress progress = getOrCreateProgress(parent);

            if (result.Type.IsHit())
                progress.NestedHitCount++;

            if (nested is SliderHeadCircle)
            {
                progress.HeadJudged = true;
                progress.HeadHit = result.Type.IsHit();
                progress.HeadTimeOffset = result.TimeOffset;
            }
        }

        private void addSpinnerHitTick(HitObject tick)
        {
            if (!getNestedParents().TryGetValue(tick, out HitObject? parent) || parent is not Spinner)
                return;

            getOrCreateProgress(parent).SpinnerHitTicks++;
        }

        private Dictionary<HitObject, HitObject> getNestedParents()
        {
            IBeatmap? beatmap = Beatmap.Value;

            if (nestedParents != null && ReferenceEquals(nestedParentsBeatmap, beatmap))
                return nestedParents;

            var parents = new Dictionary<HitObject, HitObject>();

            if (beatmap != null)
            {
                foreach (HitObject hitObject in beatmap.HitObjects)
                {
                    foreach (HitObject nested in hitObject.NestedHitObjects)
                        parents[nested] = hitObject;
                }
            }

            nestedParents = parents;
            nestedParentsBeatmap = beatmap;

            return parents;
        }

        private TopLevelProgress getOrCreateProgress(HitObject key)
        {
            if (!topLevelProgress.TryGetValue(key, out TopLevelProgress? progress))
            {
                progress = new TopLevelProgress();
                topLevelProgress[key] = progress;
            }

            return progress;
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
            topLevelProgress.Clear();
            nestedParents = null;
            nestedParentsBeatmap = null;
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
            public readonly bool HasProgressSnapshot;
            public readonly ProgressSnapshot Progress;

            public StableState(double score, double bonus, int combo, int highestCombo, int topLevelJudged, ProgressSnapshot progress, bool hasProgressSnapshot)
            {
                Score = score;
                Bonus = bonus;
                Combo = combo;
                HighestCombo = highestCombo;
                TopLevelJudged = topLevelJudged;
                Progress = progress;
                HasProgressSnapshot = hasProgressSnapshot;
            }
        }

        /// <summary>
        /// A copy of a top-level object's <see cref="TopLevelProgress"/> taken before a nested
        /// result is applied, used to rewind the tracking state.
        /// </summary>
        private readonly struct ProgressSnapshot
        {
            public readonly HitObject Key;
            public readonly bool Existed;
            public readonly int NestedHitCount;
            public readonly bool HeadJudged;
            public readonly bool HeadHit;
            public readonly double HeadTimeOffset;
            public readonly int SpinnerHitTicks;

            public ProgressSnapshot(HitObject key, TopLevelProgress? progress)
            {
                Key = key;
                Existed = progress != null;
                NestedHitCount = progress?.NestedHitCount ?? 0;
                HeadJudged = progress?.HeadJudged ?? false;
                HeadHit = progress?.HeadHit ?? false;
                HeadTimeOffset = progress?.HeadTimeOffset ?? 0;
                SpinnerHitTicks = progress?.SpinnerHitTicks ?? 0;
            }
        }

        /// <summary>
        /// Stable-side per-object progress needed to recompute aggregate judgement tiers.
        /// </summary>
        private sealed class TopLevelProgress
        {
            /// <summary>The number of nested slider objects (head/ticks/repeats/tail) judged as hits.</summary>
            public int NestedHitCount;

            /// <summary>Whether the slider head has been judged, and its result and time offset.</summary>
            public bool HeadJudged;
            public bool HeadHit;
            public double HeadTimeOffset;

            /// <summary>The number of spinner ticks judged as hits (one completed full rotation each).</summary>
            public int SpinnerHitTicks;
        }
    }
}
