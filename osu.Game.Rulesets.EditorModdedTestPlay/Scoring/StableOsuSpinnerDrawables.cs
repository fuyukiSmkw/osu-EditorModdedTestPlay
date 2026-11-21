#nullable enable

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    internal sealed partial class StableDrawableSpinnerTick : DrawableSpinnerTick
    {
        public StableDrawableSpinnerTick()
        {
        }

        public StableDrawableSpinnerTick(StableSpinnerTick tick)
            : base(tick)
        {
        }

        public void TriggerHit(bool hit)
        {
            if (hit)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }
    }

    internal sealed partial class StableDrawableSpinnerBonusTick : DrawableSpinnerBonusTick
    {
        public StableDrawableSpinnerBonusTick()
        {
        }

        public StableDrawableSpinnerBonusTick(StableSpinnerBonusTick tick)
            : base(tick)
        {
        }

        public void TriggerHit(bool hit)
        {
            if (hit)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }
    }

    /// <summary>
    /// A spinner whose ticks and aggregate judgement follow osu!stable's ScoreV2 half-spin model.
    /// </summary>
    /// <remarks>
    /// g0v0's Classic-only <c>ExclusiveLegacyInput</c> (restricting overlapping spinner input to the
    /// first active spinner) is not reproduced because NuGet 2026.916.0 lacks the corresponding
    /// <c>DrawableSpinner.CanTrack</c> hook. This only affects maps with simultaneous spinners.
    /// </remarks>
    internal sealed partial class StableDrawableSpinner : DrawableSpinner
    {
        private readonly List<DrawableSpinnerTick> stableTicks = new List<DrawableSpinnerTick>();

        public StableDrawableSpinner()
        {
        }

        public StableDrawableSpinner(StableSpinner spinner)
            : base(spinner)
        {
        }

        protected override void OnApply()
        {
            base.OnApply();
            setCompletedFullSpins(0);
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            switch (hitObject)
            {
                case StableSpinnerBonusTick bonusTick:
                    return new StableDrawableSpinnerBonusTick(bonusTick);

                case StableSpinnerTick tick:
                    return new StableDrawableSpinnerTick(tick);

                default:
                    return base.CreateNestedHitObject(hitObject);
            }
        }

        protected override void AddNestedHitObject(DrawableHitObject hitObject)
        {
            base.AddNestedHitObject(hitObject);

            if (hitObject is DrawableSpinnerTick tick)
                stableTicks.Add(tick);
        }

        protected override void ClearNestedHitObjects()
        {
            base.ClearNestedHitObjects();
            stableTicks.Clear();
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Time.Current < HitObject.StartTime)
                return;

            int requiredHalfSpins = ((StableSpinner)HitObject).LegacyHalfSpinsRequired;
            int halfSpins = (int)(Result.TotalRotation / 180);

            if (requiredHalfSpins == 0 || halfSpins >= requiredHalfSpins)
                Result.TimeCompleted ??= Time.Current;

            if (userTriggered || Time.Current < HitObject.EndTime)
                return;

            foreach (DrawableSpinnerTick tick in stableTicks)
            {
                if (!tick.Result.HasResult)
                    triggerTick(tick, false);
            }

            ApplyResult(static (result, drawable) =>
            {
                var spinner = (StableDrawableSpinner)drawable;
                int halfSpins = (int)(spinner.Result.TotalRotation / 180);
                int required = ((StableSpinner)spinner.HitObject).LegacyHalfSpinsRequired;

                result.Type = required == 0 || halfSpins > required ? HitResult.Great
                    : halfSpins >= required - 1 ? HitResult.Ok
                    : halfSpins >= Math.Max(0, required / 4) ? HitResult.Meh
                    : HitResult.Miss;
            });
        }

        protected override void UpdateAfterChildren()
        {
            // Keep DrawableSpinner's private whole-spin counter in sync before its update runs.
            // This makes the stock updateBonusScore() a no-op, so it cannot consume our stable
            // half-spin ticks one full spin at a time. The stock counter still drives the skin's
            // spin progress display, so it must continue to receive the right value.
            setCompletedFullSpins((int)(Result.TotalRotation / 360f));

            base.UpdateAfterChildren();

            int halfSpins = (int)(Result.TotalRotation / 180);

            foreach (DrawableSpinnerTick tick in stableTicks)
            {
                if (!tick.Result.HasResult && tick.HitObject is ILegacyHalfSpinTick legacyTick && legacyTick.LegacyHalfSpinIndex <= halfSpins)
                    triggerTick(tick, true);
            }

            // Match g0v0's half-spin based skin counter after tick processing.
            int fullSpins = halfSpins / 2;

            if (CompletedFullSpins.Value != fullSpins)
                setCompletedFullSpins(fullSpins);
        }

        private void setCompletedFullSpins(int value)
        {
            // DrawableSpinner creates this as a plain Bindable<int>; the public property is
            // read-only to prevent external mutation, but the skin counter still needs updating.
            if (CompletedFullSpins is Bindable<int> bindable)
                bindable.Value = value;
        }

        private static void triggerTick(DrawableSpinnerTick tick, bool hit)
        {
            switch (tick)
            {
                case StableDrawableSpinnerBonusTick bonusTick:
                    bonusTick.TriggerHit(hit);
                    break;

                case StableDrawableSpinnerTick normalTick:
                    normalTick.TriggerHit(hit);
                    break;
            }
        }
    }
}
