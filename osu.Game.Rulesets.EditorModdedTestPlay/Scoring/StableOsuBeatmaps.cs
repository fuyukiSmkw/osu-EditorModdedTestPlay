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
    /// A spinner which generates osu!stable's half-spin based ScoreV2 ticks.
    /// </summary>
    /// <remarks>
    /// stable tracks rotation at a nominal maximum of 0.05 radians per millisecond and awards
    /// score per half-spin. Normal ticks are awarded on complete spins (even half-spin indices)
    /// and bonus ticks alternate every second half-spin after <c>R + 3</c>, where <c>R</c> is the
    /// number of half-spins required to clear the spinner. Unlike lazer's default model this is
    /// not capped by the spinner's full-score RPM.
    /// </remarks>
    internal sealed class StableSpinner : Spinner
    {
        private const double half_spin_rotation_speed = 0.05; // radians per millisecond
        private const int bonus_spins_gap = 3;

        /// <summary>
        /// The number of half-spins required to clear the spinner, using stable's integer truncation.
        /// </summary>
        public int LegacyHalfSpinsRequired { get; private set; }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            double clearRps = IBeatmapDifficultyInfo.DifficultyRange(difficulty.OverallDifficulty, CLEAR_RPM_RANGE) / 60;
            LegacyHalfSpinsRequired = (int)(clearRps * (Duration / 1000) * 2);
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            // Intentionally does not call base: lazer's tick model is capped at the spinner's
            // full-score RPM and replaces normal ticks with bonus ticks.
            int bonusThreshold = LegacyHalfSpinsRequired + bonus_spins_gap;
            int halfSpins = (int)(Duration * half_spin_rotation_speed / Math.PI);

            for (int i = 1; i <= halfSpins; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool bonus = i > bonusThreshold && (i - bonusThreshold) % 2 == 0;

                if (!bonus && i % 2 != 0)
                    continue;

                SpinnerTick tick = bonus
                    ? new StableSpinnerBonusTick { Samples = new[] { CreateHitSampleInfo("spinnerbonus") } }
                    : new StableSpinnerTick();

                tick.StartTime = StartTime + i * Math.PI / half_spin_rotation_speed;
                tick.SpinnerDuration = Duration;
                ((ILegacyHalfSpinTick)tick).LegacyHalfSpinIndex = i;
                AddNested(tick);
            }
        }
    }

    /// <summary>
    /// Exposes the stable half-spin index on both spinner tick types.
    /// </summary>
    internal interface ILegacyHalfSpinTick
    {
        int LegacyHalfSpinIndex { get; set; }
    }

    internal sealed class StableSpinnerTick : SpinnerTick, ILegacyHalfSpinTick
    {
        public int LegacyHalfSpinIndex { get; set; }
    }

    internal sealed class StableSpinnerBonusTick : SpinnerBonusTick, ILegacyHalfSpinTick
    {
        public int LegacyHalfSpinIndex { get; set; }
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
                    yield return createStableSpinner(spinner);
                else
                    yield return hitObject;
            }
        }

        protected override Beatmap<OsuHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            Beatmap<OsuHitObject> beatmap = base.ConvertBeatmap(original, cancellationToken);

            // Editor beatmaps already contain OsuHitObject instances. BeatmapConverter keeps those
            // as-is and never calls ConvertHitObject, so replace any remaining spinners here.
            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                if (beatmap.HitObjects[i] is Spinner spinner && spinner is not StableSpinner)
                    beatmap.HitObjects[i] = createStableSpinner(spinner);
            }

            return beatmap;
        }

        private static StableSpinner createStableSpinner(Spinner spinner)
        {
            return new StableSpinner
            {
                StartTime = spinner.StartTime,
                Samples = spinner.Samples,
                EndTime = spinner.EndTime,
                Position = spinner.Position,
                NewCombo = spinner.NewCombo,
                ComboOffset = spinner.ComboOffset,
            };
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
    /// <para>
    /// g0v0's Classic-only fixed-checkpoint/tail-leniency behaviour (<c>ClassicSliderJudgement</c>)
    /// is not reproduced: NuGet 2026.916.0 does not expose the required private
    /// <c>SliderInputManager</c> state. This only affects late/early tick recovery, not the scoring
    /// formula or the aggregate result types.
    /// </para>
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
