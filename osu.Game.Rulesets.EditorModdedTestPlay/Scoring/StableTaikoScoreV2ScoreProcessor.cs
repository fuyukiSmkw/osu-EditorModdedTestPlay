#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A ScoreV2 processor that reproduces osu!stable's taiko scoring for editor test play.
    /// </summary>
    /// <remarks>
    /// stable's formula is
    /// <c>round((250000 * S / maxSum + 750000 * accuracy^3.6 * completion + B) * multiplier)</c>,
    /// where <c>S</c> is the combo-weighted sum of 300-value hits (both 300s and 100s are worth
    /// 300 there), <c>B</c> is the bonus sum (drumroll ticks, ...) and <c>completion</c> is the
    /// fraction of top-level (hit/drumroll/swell) judgements which have been applied.
    /// <para>
    /// stable treats a strong hit as a single result whose value is eight times a normal hit,
    /// and a strong drumroll tick as a single four-times bonus result.
    /// lazer instead emits a normal judgement followed by a nested "strong" bonus judgement, so the
    /// nested parts are folded back into the parent value here.
    /// </para>
    /// </remarks>
    internal sealed partial class StableTaikoScoreV2ScoreProcessor : ScoreProcessor
    {
        private const double combo_base = 4;
        private const double combo_cap = 400;

        // stable hit values for the taiko ruleset. A 100 is worth 300 for scoring purposes
        // (only its accuracy contribution differs), so both Great and Ok use the same value.
        private const int great_value = 300;
        private const int drumroll_tick_value = 200;
        private const int drumroll_tick_strong_bonus = 600;

        // stable multiplies strong results by 8 (7 extra parts on top of the normal value).
        private const int strong_hit_extra = 2100;
        private const int drumroll_strong_multiplier = 8;

        /// <summary>
        /// The combo-weighted score sum (<c>S</c> in stable's formula).
        /// </summary>
        private double stableScore;

        /// <summary>
        /// The bonus score sum (<c>B</c> in stable's formula).
        /// </summary>
        private double stableBonus;

        /// <summary>
        /// stable's per-result combo counter.
        /// </summary>
        private int stableCombo;

        /// <summary>
        /// stable's highest combo achieved during the current play.
        /// </summary>
        private int stableHighestCombo;

        /// <summary>
        /// The number of top-level (hit/drumroll/swell) judgements applied so far.
        /// </summary>
        private int topLevelJudged;

        private int count300;
        private int count100;
        private int countMiss;

        private double maximumStableScore;
        private int maximumTopLevelJudged;

        /// <summary>
        /// The number of ticks hit for each drumroll, used to determine its completion result.
        /// </summary>
        private readonly Dictionary<DrumRoll, int> drumRollHitCounts = new Dictionary<DrumRoll, int>();

        /// <summary>
        /// The combo and result of each strong hit's parent, used when the nested strong hit is judged.
        /// </summary>
        private readonly Dictionary<Hit, (HitResult Result, int Combo)> strongHitParents = new Dictionary<Hit, (HitResult Result, int Combo)>();

        /// <summary>
        /// Snapshots recorded for rewind support. Reverts are applied in reverse order.
        /// </summary>
        private readonly List<StableState> stateHistory = new List<StableState>();

        public StableTaikoScoreV2ScoreProcessor(Ruleset ruleset)
            : base(ruleset)
        {
            // lazer's own accuracy/combo accounting does not include drumroll/swell completions.
            // Keep the bindables in sync with stable's accounting after each judgement so the HUD
            // matches the value used by the final score.
            NewJudgement += _ => syncDisplayedStats();
            JudgementReverted += _ => syncDisplayedStats();
        }

        /// <summary>
        /// Enumerates hit objects with their parent before their nested objects.
        /// </summary>
        /// <remarks>
        /// stable scores a strong hit as one combined result, so the parent's combo has to be
        /// incremented before the nested strong bonus is processed. The exact order of the remaining
        /// nested objects does not affect the maximum sum because drumroll ticks only contribute to
        /// the bonus portion (which is not combo-weighted).
        /// </remarks>
        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
        {
            foreach (HitObject hitObject in beatmap.HitObjects)
            {
                foreach (HitObject obj in enumerateTopLevelFirst(hitObject))
                    yield return obj;
            }
        }

        private static IEnumerable<HitObject> enumerateTopLevelFirst(HitObject hitObject)
        {
            yield return hitObject;

            foreach (HitObject nested in hitObject.NestedHitObjects)
            {
                foreach (HitObject deeper in enumerateTopLevelFirst(nested))
                    yield return deeper;
            }
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                case HitResult.Perfect:
                    return great_value;

                case HitResult.Ok:
                    // stable weights a taiko 100 as half of a 300 for accuracy purposes.
                    return 150;

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

            return 250000 * stableScore / maximumStableScore
                   + 750000 * Math.Pow(stableAccuracy, 3.6) * completion
                   + stableBonus;
        }

        private double stableAccuracy
        {
            get
            {
                int totalHits = count300 + count100 + countMiss;

                if (totalHits == 0)
                    return 1;

                return (count100 * 150.0 + count300 * 300.0) / (totalHits * 300.0);
            }
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            DrumRoll? tickHitParent = null;

            if (result.Type == HitResult.SmallBonus && result.HitObject is DrumRollTick tick)
                tickHitParent = tick.Parent;

            stateHistory.Add(new StableState(stableScore, stableBonus, stableCombo, stableHighestCombo, topLevelJudged,
                count300, count100, countMiss, tickHitParent));

            applyResult(result);
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            if (stateHistory.Count == 0)
                return;

            StableState state = stateHistory[^1];
            stateHistory.RemoveAt(stateHistory.Count - 1);

            if (state.TickHitParent != null)
            {
                int hits = drumRollHitCounts.GetValueOrDefault(state.TickHitParent) - 1;
                if (hits <= 0)
                    drumRollHitCounts.Remove(state.TickHitParent);
                else
                    drumRollHitCounts[state.TickHitParent] = hits;
            }

            stableScore = state.Score;
            stableBonus = state.Bonus;
            stableCombo = state.Combo;
            stableHighestCombo = state.HighestCombo;
            topLevelJudged = state.TopLevelJudged;
            count300 = state.Count300;
            count100 = state.Count100;
            countMiss = state.CountMiss;
        }

        private void applyResult(JudgementResult result)
        {
            HitResult type = result.Type;

            // A nested strong hit is folded into its parent's value below.
            if (result.HitObject is StrongNestedHitObject strong)
            {
                applyStrongNested(strong, type);
                return;
            }

            // Drumroll ticks contribute to the bonus portion only. They also count towards the
            // drumroll's completion, which is judged separately when the drumroll ends.
            if (result.HitObject is DrumRollTick tick)
            {
                if (type == HitResult.SmallBonus)
                {
                    stableBonus += drumroll_tick_value;
                    drumRollHitCounts[tick.Parent] = drumRollHitCounts.GetValueOrDefault(tick.Parent) + 1;
                }

                return;
            }

            // lazer judges a drumroll with an IgnoreHit at its end (it has no visible judgement).
            if (result.HitObject is DrumRoll drumRoll)
            {
                if (type == HitResult.IgnoreHit)
                    applyDrumRollCompletion(drumRoll);

                return;
            }

            if (result.HitObject is Swell)
            {
                if (type == HitResult.LargeBonus)
                {
                    // stable awards a regular 300 result for a completed swell.
                    increaseCombo();
                    count300++;
                    stableScore += great_value * comboFactor(stableCombo);
                    topLevelJudged++;
                }
                else if (type == HitResult.IgnoreMiss)
                {
                    stableCombo = 0;
                    countMiss++;
                    topLevelJudged++;
                }

                updateHighestCombo();
                return;
            }

            if (result.HitObject is SwellTick || result.HitObject is BarLine)
                return;

            if (type == HitResult.IgnoreHit || type == HitResult.IgnoreMiss)
                return;

            if (result.HitObject is Hit hit)
            {
                switch (type)
                {
                    case HitResult.Great:
                    case HitResult.Perfect:
                        increaseCombo();
                        count300++;
                        stableScore += great_value * comboFactor(stableCombo);
                        topLevelJudged++;
                        strongHitParents[hit] = (type, stableCombo);
                        break;

                    case HitResult.Ok:
                        increaseCombo();
                        count100++;
                        // stable's 100 is worth a full 300 in S (only accuracy halves it).
                        stableScore += great_value * comboFactor(stableCombo);
                        topLevelJudged++;
                        strongHitParents[hit] = (type, stableCombo);
                        break;

                    case HitResult.Miss:
                        stableCombo = 0;
                        countMiss++;
                        topLevelJudged++;
                        break;
                }

                updateHighestCombo();
            }
        }

        private void applyStrongNested(StrongNestedHitObject strong, HitResult type)
        {
            if (type != HitResult.LargeBonus)
                return;

            switch (strong.Parent)
            {
                case Hit parentHit:
                    if (!strongHitParents.TryGetValue(parentHit, out var parent))
                        return;

                    // A strong hit (300 or 100) is worth eight times the normal value in S.
                    if (parent.Result == HitResult.Great || parent.Result == HitResult.Perfect || parent.Result == HitResult.Ok)
                        stableScore += strong_hit_extra * comboFactor(parent.Combo);
                    break;

                case DrumRollTick:
                    // A strong drumroll tick is worth four times a normal tick in total.
                    stableBonus += drumroll_tick_strong_bonus;
                    break;
            }
        }

        private void applyDrumRollCompletion(DrumRoll drumRoll)
        {
            int totalTicks = drumRoll.NestedHitObjects.Count(o => o is DrumRollTick);
            int hitTicks = IsSimulating ? totalTicks : drumRollHitCounts.GetValueOrDefault(drumRoll);

            // stable's required hit fraction is min(0.3, 0.1 + OD / 30).
            double overallDifficulty = Beatmap.Value?.BeatmapInfo.Difficulty.OverallDifficulty ?? 5;
            double requiredFraction = Math.Min(0.3, 0.1 + overallDifficulty / 30.0);
            bool complete = totalTicks == 0 || hitTicks >= totalTicks * requiredFraction;

            if (complete || hitTicks >= 1)
            {
                increaseCombo();

                if (complete)
                    count300++;
                else
                    count100++;

                // A strong drumroll completion is worth eight times a normal 300.
                stableScore += great_value * comboFactor(stableCombo) * (drumRoll.IsStrong ? drumroll_strong_multiplier : 1);
                topLevelJudged++;
            }
            else
            {
                stableCombo = 0;
                countMiss++;
                topLevelJudged++;
            }

            updateHighestCombo();
        }

        /// <summary>
        /// stable's taiko combo weight: <c>clamp(log_4(combo), 0.5, log_4(400))</c>.
        /// </summary>
        private static double comboFactor(int combo)
        {
            return Math.Min(Math.Max(0.5, Math.Log(combo, combo_base)), Math.Log(combo_cap, combo_base));
        }

        private void increaseCombo()
        {
            stableCombo++;
        }

        private void updateHighestCombo()
        {
            // Make sure a miss doesn't leave a stale highest combo behind after a rewind.
            stableHighestCombo = Math.Max(stableHighestCombo, stableCombo);
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
            count300 = 0;
            count100 = 0;
            countMiss = 0;
            drumRollHitCounts.Clear();
            strongHitParents.Clear();
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

            score.Accuracy = stableAccuracy;
            score.Combo = stableCombo;
            score.MaxCombo = stableHighestCombo;

            // stable's hit counters include drumroll completions and successful swells, which lazer
            // reports through separate result types. Expose stable's counts so the results screen and
            // its maximum-combo display agree with the score that was calculated above.
            score.Statistics[HitResult.Great] = count300;
            score.Statistics[HitResult.Ok] = count100;
            score.Statistics[HitResult.Miss] = countMiss;
            score.MaximumStatistics[HitResult.Great] = maximumTopLevelJudged;
            score.MaximumStatistics[HitResult.Ok] = 0;
            score.MaximumStatistics[HitResult.Miss] = 0;

            score.Rank = RankFromScore(stableAccuracy, score.Statistics);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results);

            // Match taiko's rank handling: a score with misses cannot be S or SS.
            if ((rank == ScoreRank.S || rank == ScoreRank.X) && countMiss > 0)
                rank = ScoreRank.A;

            return rank;
        }

        private void syncDisplayedStats()
        {
            if (IsSimulating)
                return;

            Accuracy.Value = stableAccuracy;
            Combo.Value = stableCombo;
            HighestCombo.Value = stableHighestCombo;
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
            public readonly int Combo;
            public readonly int HighestCombo;
            public readonly int TopLevelJudged;
            public readonly int Count300;
            public readonly int Count100;
            public readonly int CountMiss;
            public readonly DrumRoll? TickHitParent;

            public StableState(double score, double bonus, int combo, int highestCombo, int topLevelJudged,
                int count300, int count100, int countMiss, DrumRoll? tickHitParent)
            {
                Score = score;
                Bonus = bonus;
                Combo = combo;
                HighestCombo = highestCombo;
                TopLevelJudged = topLevelJudged;
                Count300 = count300;
                Count100 = count100;
                CountMiss = countMiss;
                TickHitParent = tickHitParent;
            }
        }
    }
}
