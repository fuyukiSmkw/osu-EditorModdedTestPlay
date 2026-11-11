#nullable enable

using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Rulesets used only for editor test play when the ScoreV2 toggle is active.
    /// They are intentionally nested (and therefore not picked up by <c>RulesetStore</c>)
    /// so the user still sees only the four regular rulesets.
    /// </summary>
    public static partial class HiddenScoreV2Rulesets
    {
        public static RulesetInfo? CreateInfo(RulesetInfo? original)
        {
            if (original == null)
                return null;

            Ruleset? hidden = original.ShortName switch
            {
                OsuRuleset.SHORT_NAME => new HiddenOsuRuleset(),
                TaikoRuleset.SHORT_NAME => new HiddenTaikoRuleset(),
                CatchRuleset.SHORT_NAME => new HiddenCatchRuleset(),
                ManiaRuleset.SHORT_NAME => new HiddenManiaRuleset(),
                _ => null
            };

            return hidden?.RulesetInfo;
        }

        /// <summary>
        /// Creates a <see cref="RulesetInfo"/> for the hidden ruleset which can be used where a
        /// distinct <see cref="RulesetInfo"/> instance is required.
        /// </summary>
        /// <remarks>
        /// <see cref="RulesetInfo"/> equality is based on <see cref="RulesetInfo.ShortName"/>, so the
        /// short name is made distinct here. This allows binding the result into a
        /// <c>Bindable&lt;RulesetInfo&gt;</c> (for example the mod select footer's multiplier source)
        /// and actually receiving a change notification when switching between normal and stable-V2 rulesets.
        /// </remarks>
        public static RulesetInfo? CreateDisplayInfo(RulesetInfo? original)
        {
            RulesetInfo? info = CreateInfo(original);
            if (info == null)
                return null;

            info.ShortName += "-scorev2-display";
            return info;
        }

        public sealed class HiddenOsuRuleset : OsuRuleset
        {
            public override ScoreProcessor CreateScoreProcessor() => new StableOsuScoreV2ScoreProcessor(this);
            public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => new StableOsuScoreV2MultiplierCalculator(context);
            public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new StableOsuBeatmapConverter(beatmap, this);
            public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => new StableOsuBeatmapProcessor(beatmap);
        }

        public sealed class HiddenTaikoRuleset : TaikoRuleset
        {
            public override ScoreProcessor CreateScoreProcessor() => new StableTaikoScoreV2ScoreProcessor(this);
            public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => new StableTaikoScoreV2MultiplierCalculator(context);
        }

        public sealed class HiddenCatchRuleset : CatchRuleset
        {
            public override ScoreProcessor CreateScoreProcessor() => new FixedScoreProcessor(this);
            public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => new StableCatchScoreV2MultiplierCalculator(context);
        }

        public sealed class HiddenManiaRuleset : ManiaRuleset
        {
            public override ScoreProcessor CreateScoreProcessor() => new FixedScoreProcessor(this);
            public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => new StableManiaScoreV2MultiplierCalculator(context);
        }

        /// <summary>
        /// Temporary processor used to verify that the hidden ruleset's score processor is actually in use.
        /// The real stable ScoreV2 formulas will replace this later.
        /// </summary>
        internal sealed partial class FixedScoreProcessor : ScoreProcessor
        {
            public const long FIXED_SCORE = 114514;

            public FixedScoreProcessor(Ruleset ruleset)
                : base(ruleset)
            {
            }

            protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
                => FIXED_SCORE;

            // Keep the displayed score fixed before any judgement is applied (and for empty beatmaps).
            protected override void Reset(bool storeResults)
            {
                base.Reset(storeResults);
                TotalScore.Value = FIXED_SCORE;
            }

            public override void PopulateScore(ScoreInfo score)
            {
                base.PopulateScore(score);
                score.TotalScoreWithoutMods = FIXED_SCORE;
                score.TotalScore = FIXED_SCORE;
            }
        }
    }
}
