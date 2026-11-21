#nullable enable

using System;
using System.Linq;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// A slider head which keeps the raw timing judgement separate from the
    /// <see cref="HitResult.LargeTickHit"/>/<see cref="HitResult.LargeTickMiss"/> result used by
    /// classic sliders.
    /// </summary>
    /// <remarks>
    /// osu!stable's ScoreV2 slider aggregate combines the head's raw 300/100/50 tier with the
    /// slider's tracking tier. lazer's classic head judgement only exposes a hit/miss large-tick
    /// result, so the raw tier has to be captured before it is converted.
    /// </remarks>
    internal sealed partial class StableDrawableSliderHead : DrawableSliderHead
    {
        public HitResult RawHitResult { get; private set; } = HitResult.None;

        // The real head result must stay LargeTickHit/Miss for scoring and the parent aggregate.
        // The playfield emits a separate raw 100/50/miss judgement for display instead.
        public override bool DisplayResult => false;

        public StableDrawableSliderHead()
        {
        }

        public StableDrawableSliderHead(SliderHeadCircle head)
            : base(head)
        {
        }

        protected override void OnApply()
        {
            base.OnApply();
            RawHitResult = HitResult.None;
        }

        protected override HitResult ResultFor(double timeOffset)
        {
            // Do not call base: DrawableSliderHead converts classic results to LargeTick*.
            HitResult raw = HitObject.HitWindows.ResultFor(timeOffset);
            RawHitResult = raw;

            if (HitObject.ClassicSliderBehaviour)
                return raw.IsHit() ? HitResult.LargeTickHit : HitResult.LargeTickMiss;

            return raw;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // The base class does not call ResultFor() when the last hit window has already passed,
            // so record the miss here to keep the raw head tier in sync.
            if (!userTriggered && !Judged && !HitObject.HitWindows.CanBeHit(timeOffset))
                RawHitResult = HitResult.Miss;

            // DrawableSliderHead already calls SliderInputManager.PostProcessHeadJudgement().
            base.CheckForResult(userTriggered, timeOffset);
        }
    }

    /// <summary>
    /// A slider whose aggregate judgement follows osu!stable's ScoreV2 rules.
    /// </summary>
    /// <remarks>
    /// The parent result is written inside <see cref="CheckForResult"/> because
    /// <see cref="DrawableHitObject.ApplyResult{T}"/> immediately raises the new-result event which
    /// feeds the score processor and accuracy bindables.
    /// </remarks>
    internal sealed partial class StableDrawableSlider : DrawableSlider
    {
        public StableDrawableSlider()
        {
        }

        public StableDrawableSlider(Slider slider)
            : base(slider)
        {
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            if (hitObject is SliderHeadCircle head)
                return new StableDrawableSliderHead(head);

            return base.CreateNestedHitObject(hitObject);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered || !TailCircle.Judged || Time.Current < HitObject.EndTime)
                return;

            if (!HitObject.ClassicSliderBehaviour)
            {
                base.CheckForResult(userTriggered, timeOffset);
                return;
            }

            ApplyResult(static (result, drawable) =>
            {
                var slider = (StableDrawableSlider)drawable;

                HitResult headResult = slider.HeadCircle is StableDrawableSliderHead head
                    ? head.RawHitResult
                    : HitResult.None;

                if (headResult == HitResult.None)
                    headResult = slider.HeadCircle?.IsHit == true ? HitResult.Great : HitResult.Miss;

                result.Type = StableSliderJudgement.ComputeAggregate(headResult, slider.NestedHitObjects.Count(h => h.IsHit), slider.NestedHitObjects.Count);
            });
        }
    }

    /// <summary>
    /// Pure judgement rules used by <see cref="StableDrawableSlider"/>.
    /// </summary>
    internal static class StableSliderJudgement
    {
        /// <summary>
        /// Computes stable's slider tracking tier from the number of hit nested drawables.
        /// </summary>
        public static HitResult ComputeTracking(int hitCount, int totalCount)
        {
            if (hitCount == totalCount)
                return HitResult.Great;

            if (hitCount == 0)
                return HitResult.Miss;

            return (double)hitCount / totalCount >= 0.5 ? HitResult.Ok : HitResult.Meh;
        }

        /// <summary>
        /// Combines the slider head's raw timing tier with the tracking tier.
        /// </summary>
        public static HitResult ComputeAggregate(HitResult headResult, int hitCount, int totalCount)
        {
            HitResult trackingResult = ComputeTracking(hitCount, totalCount);

            if (trackingResult == HitResult.Miss)
                return HitResult.Miss;

            if (headResult == HitResult.Miss)
                return HitResult.Meh;

            return (HitResult)Math.Min((int)headResult, (int)trackingResult);
        }
    }
}
