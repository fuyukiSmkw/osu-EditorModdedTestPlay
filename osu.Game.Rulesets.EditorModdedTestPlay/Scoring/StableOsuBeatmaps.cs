#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A <see cref="Spinner"/> which allows bonus ticks up to osu!stable's maximum rotation speed.
    /// </summary>
    /// <remarks>
    /// lazer only creates enough bonus ticks to reach its "full score" RPM (430 at OD10).
    /// stable keeps awarding bonus score for every extra spin up to a hard cap of 477 RPM,
    /// so editor test play would otherwise underestimate spinner-heavy scores.
    /// </remarks>
    internal sealed class StableSpinner : Spinner
    {
        private const double maximum_rotations_per_second = 477.0 / 60;
        private const int bonus_spins_gap = 2;

        /// <summary>
        /// Whether stable also awards the regular 100-point tick on the same full spin as a bonus tick.
        /// </summary>
        /// <remarks>
        /// stable scores spinners in half-spins. Bonus ticks begin after <c>R + 3</c> half-spins, where
        /// <c>R</c> is the number of half-spins required at the current difficulty.
        /// <list type="bullet">
        /// <item><description><c>R + 3</c> odd: bonus ticks land on odd half-spins, so the normal tick on the same full spin is also awarded.</description></item>
        /// <item><description><c>R + 3</c> even: the bonus tick replaces the normal tick on that full spin.</description></item>
        /// </list>
        /// lazer's spinner model always replaces the normal tick with the bonus tick, so the extra
        /// normal tick has to be re-added for the first case.
        /// </remarks>
        public bool BonusAwardsNormalTick { get; private set; }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            double secondsDuration = Duration / 1000;
            int totalSpins = (int)(secondsDuration * maximum_rotations_per_second + 0.0001);

            MaximumBonusSpins = Math.Max(MaximumBonusSpins, Math.Max(0, totalSpins - SpinsRequired - bonus_spins_gap));

            // This is the same half-spin rate stable uses for its spinner requirements.
            double halfSpinsPerSecond = IBeatmapDifficultyInfo.DifficultyRange(difficulty.OverallDifficulty, 3.0, 5.0, 7.5);
            int halfSpinsRequired = (int)(secondsDuration * halfSpinsPerSecond);

            BonusAwardsNormalTick = (halfSpinsRequired + 3) % 2 != 0;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            // Intentionally does not call base: Spinner already creates a tick for every spin up to
            // its own (lower) MaximumBonusSpins, and below we recreate the complete set including the
            // extended bonus spins. Calling base would duplicate every tick that both models share.
            int totalSpins = MaximumBonusSpins + SpinsRequired + bonus_spins_gap;

            for (int i = 0; i < totalSpins; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double startTime = StartTime + (float)(i + 1) / totalSpins * Duration;

                if (i < SpinsRequiredForBonus)
                {
                    AddNested(new SpinnerTick { StartTime = startTime, SpinnerDuration = Duration });
                }
                else
                {
                    AddNested(new StableSpinnerBonusTick(BonusAwardsNormalTick)
                    {
                        StartTime = startTime,
                        SpinnerDuration = Duration,
                        Samples = new[] { CreateHitSampleInfo("spinnerbonus") }
                    });
                }
            }
        }
    }

    /// <summary>
    /// A spinner bonus tick which knows whether stable would have awarded a normal tick on the same spin.
    /// </summary>
    internal sealed class StableSpinnerBonusTick : SpinnerBonusTick
    {
        public bool AwardsNormalTick { get; }

        public StableSpinnerBonusTick(bool awardsNormalTick)
        {
            AwardsNormalTick = awardsNormalTick;
        }
    }

    /// <summary>
    /// Converts spinners into <see cref="StableSpinner"/> so that stable ScoreV2 spinner bonus is achievable.
    /// </summary>
    internal sealed class StableOsuBeatmapConverter : OsuBeatmapConverter
    {
        public StableOsuBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        protected override IEnumerable<OsuHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            foreach (OsuHitObject hitObject in base.ConvertHitObject(original, beatmap, cancellationToken))
            {
                if (hitObject is Spinner spinner)
                {
                    yield return new StableSpinner
                    {
                        StartTime = spinner.StartTime,
                        Samples = spinner.Samples,
                        EndTime = spinner.EndTime,
                        Position = spinner.Position,
                        NewCombo = spinner.NewCombo,
                        ComboOffset = spinner.ComboOffset,
                    };
                }
                else
                {
                    yield return hitObject;
                }
            }
        }
    }

    /// <summary>
    /// Forces stable's classic slider behaviour for editor ScoreV2 test play.
    /// </summary>
    /// <remarks>
    /// Classic sliders judge their head/ticks/repeats/tail individually (so slider head timing
    /// contributes to the stable ScoreV2 score) while the visible judgement remains the aggregated
    /// slider result. This is applied both before nested objects are created and after mods have run,
    /// so the Classic mod's own slider head option cannot turn it off.
    /// </remarks>
    internal sealed class StableOsuBeatmapProcessor : OsuBeatmapProcessor
    {
        public StableOsuBeatmapProcessor(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        public override void PreProcess()
        {
            base.PreProcess();
            forceClassicSliders();
        }

        public override void PostProcess()
        {
            base.PostProcess();
            forceClassicSliders();
        }

        private void forceClassicSliders()
        {
            foreach (var hitObject in Beatmap.HitObjects)
            {
                if (hitObject is Slider slider)
                    slider.ClassicSliderBehaviour = true;
            }
        }
    }
}
