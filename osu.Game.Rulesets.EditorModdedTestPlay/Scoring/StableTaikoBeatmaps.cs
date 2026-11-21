#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Shared state used while creating the stable-compatible taiko beatmap.
    /// </summary>
    /// <remarks>
    /// <see cref="BeatmapProcessor"/> does not receive the selected mods, but stable's swell
    /// rotation requirement depends on DoubleTime/HalfTime. The editor player loader populates
    /// this before the playable beatmap is created.
    /// </remarks>
    internal static class StableTaikoBeatmaps
    {
        public static IReadOnlyList<Mod>? SelectedMods;
    }

    /// <summary>
    /// Replaces taiko drum rolls and swells with stable-compatible objects before defaults are applied.
    /// </summary>
    internal sealed class StableTaikoBeatmapProcessor : BeatmapProcessor
    {
        public StableTaikoBeatmapProcessor(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        public override void PreProcess()
        {
            if (Beatmap is Beatmap<TaikoHitObject> beatmap)
            {
                for (int i = 0; i < beatmap.HitObjects.Count; i++)
                {
                    switch (beatmap.HitObjects[i])
                    {
                        case StableTaikoDrumRoll:
                        case StableTaikoSwell:
                            break;

                        case DrumRoll drumRoll:
                            beatmap.HitObjects[i] = createStableDrumRoll(beatmap, drumRoll, i);
                            break;

                        case Swell swell:
                            beatmap.HitObjects[i] = createStableSwell(swell, beatmap.Difficulty.OverallDifficulty);
                            break;
                    }
                }
            }

            base.PreProcess();
        }

        private static StableTaikoDrumRoll createStableDrumRoll(Beatmap<TaikoHitObject> beatmap, DrumRoll drumRoll, int index)
        {
            double endTime = drumRoll.StartTime + drumRoll.Duration;

            TimingControlPoint timingPoint = beatmap.ControlPointInfo.TimingPointAt(drumRoll.StartTime);
            int tickRate = beatmap.Difficulty.SliderTickRate == 3 ? 3 : 4;
            double tickSpacing = timingPoint.BeatLength / tickRate;

            TaikoHitObject? nextObject = null;

            for (int i = index + 1; i < beatmap.HitObjects.Count; i++)
            {
                if (beatmap.HitObjects[i] is BarLine)
                    continue;

                nextObject = beatmap.HitObjects[i];
                break;
            }

            // stable extends the hittable end by one tick spacing unless the next object is close.
            int extension = (int)tickSpacing;
            bool extendHittableEnd = nextObject == null || nextObject.StartTime > endTime + extension * 2;

            return new StableTaikoDrumRoll
            {
                StartTime = drumRoll.StartTime,
                Duration = drumRoll.Duration,
                IsStrong = drumRoll.IsStrong,
                Samples = drumRoll.Samples,
                StableTickSpacing = tickSpacing,
                StableHittableEndTime = extendHittableEnd ? endTime + extension : endTime,
            };
        }

        private static StableTaikoSwell createStableSwell(Swell swell, double overallDifficulty)
        {
            int rotationRequirement = getStableRotationRequirement(swell, overallDifficulty);

            return new StableTaikoSwell
            {
                StartTime = swell.StartTime,
                Duration = swell.Duration,
                Samples = swell.Samples,
                StableRotationRequirement = rotationRequirement,
                // stable requires requiredHits + 1 successful presses for a 300.
                RequiredHits = rotationRequirement + 1,
            };
        }

        private static int getStableRotationRequirement(Swell swell, double overallDifficulty)
        {
            double rotationRatio = IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, 3, 5, 7.5);

            // Match stable's truncation order: SpinnerOsu truncates before SpinnerTaiko applies 1.65.
            int baseRequirement = (int)((float)(int)swell.Duration / 1000f * rotationRatio);
            int requirement = (int)Math.Max(1f, (float)baseRequirement * 1.65f);

            IReadOnlyList<Mod>? mods = StableTaikoBeatmaps.SelectedMods;

            if (mods != null && mods.Any(m => m is ModDoubleTime || m is ModNightcore))
                requirement = Math.Max(1, (int)((float)requirement * 0.75f));

            if (mods != null && mods.Any(m => m is ModHalfTime || m is ModDaycore))
                requirement = Math.Max(1, (int)((float)requirement * 1.5f));

            return requirement;
        }
    }

    /// <summary>
    /// Drum roll whose tick generation matches osu!stable's HittableEndTime behaviour.
    /// </summary>
    internal sealed class StableTaikoDrumRoll : DrumRoll
    {
        public double StableTickSpacing = double.NaN;
        public double StableHittableEndTime = double.NaN;

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            if (double.IsNaN(StableTickSpacing))
            {
                TimingControlPoint timingPoint = controlPointInfo.TimingPointAt(StartTime);
                StableTickSpacing = timingPoint.BeatLength / TickRate;
            }

            if (double.IsNaN(StableHittableEndTime))
                StableHittableEndTime = EndTime + StableTickSpacing / 2;
        }

        public override double MaximumJudgementOffset
            => double.IsNaN(StableHittableEndTime) ? 0 : Math.Max(0, StableHittableEndTime - EndTime);

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            if (StableTickSpacing > 0)
            {
                bool first = true;

                for (double t = StartTime; t < StableHittableEndTime; t += StableTickSpacing)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    AddNested(new StableDrumRollTick(this)
                    {
                        FirstTick = first,
                        TickSpacing = StableTickSpacing,
                        StartTime = (int)t,
                        IsStrong = IsStrong,
                        Samples = Samples,
                    });

                    first = false;
                }
            }

            if (IsStrong)
                AddNested(CreateStrongNestedHit(EndTime));
        }
    }

    /// <summary>
    /// Drum roll tick which can be given a stable-compatible drawable.
    /// </summary>
    internal sealed class StableDrumRollTick : DrumRollTick
    {
        public StableDrumRollTick(DrumRoll parent)
            : base(parent)
        {
        }
    }

    /// <summary>
    /// Swell using stable's rotation requirement (which additionally needs one extra hit for a 300).
    /// </summary>
    internal sealed class StableTaikoSwell : Swell
    {
        public int StableRotationRequirement;

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            for (int i = 0; i < RequiredHits; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AddNested(new StableSwellTick(this)
                {
                    StartTime = StartTime,
                    Samples = Samples,
                });
            }
        }
    }

    /// <summary>
    /// Swell tick which keeps a reference to its parent so the score processor can count hits.
    /// </summary>
    internal sealed class StableSwellTick : SwellTick
    {
        public readonly Swell Parent;

        public StableSwellTick(Swell parent)
        {
            Parent = parent;
        }
    }
}
