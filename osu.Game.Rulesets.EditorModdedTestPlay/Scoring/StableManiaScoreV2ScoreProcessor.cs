#nullable enable

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A ScoreV2 processor that reproduces osu!stable's osu!mania scoring for editor test play.
    /// </summary>
    /// <remarks>
    /// stable's formula is
    /// <c>round((200000 * S / maxSum + 800000 * accuracy^(2 + 2 * accuracy) * completion) * multiplier)</c>,
    /// where <c>S</c> is the combo-weighted sum of hit values and <c>completion</c> is the
    /// fraction of top-level (note/hold head/hold tail) judgements which have been applied.
    /// mania has no bonus portion.
    /// <para>
    /// In stable's ScoreV2 accuracy, a MAX (320) judgement is worth 305 while still only being
    /// worth 300 in <c>S</c>. lazer models MAX as <see cref="HitResult.Perfect"/>, so the accuracy
    /// weight is overridden below while <see cref="computeComboValue"/> keeps the 300 combo value.
    /// </para>
    /// </remarks>
    internal sealed partial class StableManiaScoreV2ScoreProcessor : ScoreProcessor
    {
        private const double combo_base = 4;
        private const double combo_cap = 400;

        // stable values a MAX (lazer Perfect) as 305 for accuracy but 300 for the combo sum.
        private const int perfect_accuracy_value = 305;

        /// <summary>
        /// The combo-weighted score sum (<c>S</c> in stable's formula).
        /// </summary>
        private double stableScore;

        /// <summary>
        /// stable's per-result combo counter.
        /// </summary>
        private int stableCombo;

        /// <summary>
        /// stable's highest combo achieved during the current play.
        /// </summary>
        private int stableHighestCombo;

        /// <summary>
        /// The number of top-level (note/hold head/hold tail) judgements applied so far.
        /// </summary>
        private int topLevelJudged;

        private double maximumStableScore;
        private int maximumTopLevelJudged;

        /// <summary>
        /// Snapshots recorded for rewind support. Reverts are applied in reverse order.
        /// </summary>
        private readonly List<StableState> stateHistory = new List<StableState>();

        public StableManiaScoreV2ScoreProcessor(Ruleset ruleset)
            : base(ruleset)
        {
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                // stable weights a MAX as 305 in ScoreV2 accuracy.
                case HitResult.Perfect:
                    return perfect_accuracy_value;

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
            double accuracy = Accuracy.Value;

            return 200000 * stableScore / maximumStableScore
                   + 800000 * Math.Pow(accuracy, 2 + 2 * accuracy) * completion;
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            stateHistory.Add(new StableState(stableScore, stableCombo, stableHighestCombo, topLevelJudged));

            applyResult(result);
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            if (stateHistory.Count == 0)
                return;

            StableState state = stateHistory[^1];
            stateHistory.RemoveAt(stateHistory.Count - 1);

            stableScore = state.Score;
            stableCombo = state.Combo;
            stableHighestCombo = state.HighestCombo;
            topLevelJudged = state.TopLevelJudged;
        }

        private void applyResult(JudgementResult result)
        {
            // A hold note body only breaks combo (when the hold is released early); stable does not
            // score it as a judgement. A missed tail already resets the combo as well.
            if (result.HitObject is HoldNoteBody)
            {
                if (result.Type == HitResult.ComboBreak)
                    stableCombo = 0;

                return;
            }

            // Only notes (including hold heads and tails) produce top-level judgements in mania.
            // The hold note parent and body are judged with IgnoreJudgement.
            if (result.HitObject is not Note)
                return;

            HitResult type = result.Type;

            switch (type)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                    stableCombo++;
                    stableScore += computeComboValue(type) * comboFactor(stableCombo);
                    topLevelJudged++;
                    break;

                case HitResult.Miss:
                    stableCombo = 0;
                    topLevelJudged++;
                    break;

                default:
                    return;
            }

            stableHighestCombo = Math.Max(stableHighestCombo, stableCombo);
        }

        /// <summary>
        /// stable's value for a hit in the combo-weighted sum. A MAX is worth the same as a 300 here.
        /// </summary>
        private static int computeComboValue(HitResult result)
        {
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

        /// <summary>
        /// stable's mania combo weight: <c>clamp(log_4(combo), 0.5, log_4(400))</c>.
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
                maximumTopLevelJudged = topLevelJudged;
            }

            base.Reset(storeResults);

            stableScore = 0;
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

            score.Combo = stableCombo;
            score.MaxCombo = stableHighestCombo;
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results);

            if (rank != ScoreRank.S)
                return rank;

            // Match lazer mania's rank handling: SS requires every note to be at least a 300.
            bool anyImperfect =
                results.GetValueOrDefault(HitResult.Good) > 0
                || results.GetValueOrDefault(HitResult.Ok) > 0
                || results.GetValueOrDefault(HitResult.Meh) > 0
                || results.GetValueOrDefault(HitResult.Miss) > 0;

            return anyImperfect ? rank : ScoreRank.X;
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
            public readonly int Combo;
            public readonly int HighestCombo;
            public readonly int TopLevelJudged;

            public StableState(double score, int combo, int highestCombo, int topLevelJudged)
            {
                Score = score;
                Combo = combo;
                HighestCombo = highestCombo;
                TopLevelJudged = topLevelJudged;
            }
        }
    }
}
