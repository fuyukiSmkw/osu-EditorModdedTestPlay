#nullable enable

using System;
using System.Linq;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Objects.Drawables;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Drum roll drawable which delays its completion result until stable's extended hittable end.
    /// </summary>
    internal sealed partial class StableDrawableDrumRoll : DrawableDrumRoll
    {
        public StableDrawableDrumRoll()
            : this(null)
        {
        }

        public StableDrawableDrumRoll(DrumRoll? drumRoll)
            : base(drumRoll)
        {
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            if (hitObject is StableDrumRollTick tick)
                return new StableDrawableDrumRollTick(tick);

            return base.CreateNestedHitObject(hitObject);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
                return;

            if (HitObject is StableTaikoDrumRoll stableDrumRoll && !double.IsNaN(stableDrumRoll.StableHittableEndTime))
            {
                if (Time.Current < stableDrumRoll.StableHittableEndTime)
                    return;

                ApplyMaxResult();
                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }

    /// <summary>
    /// Drum roll tick drawable using stable's strict hit window (stable requires &lt; half spacing).
    /// </summary>
    internal sealed partial class StableDrawableDrumRollTick : DrawableDrumRollTick
    {
        public StableDrawableDrumRollTick()
            : this(null)
        {
        }

        public StableDrawableDrumRollTick(DrumRollTick? tick)
            : base(tick)
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (timeOffset >= HitObject.HitWindow)
                    ApplyMinResult();

                return;
            }

            if (Math.Abs(timeOffset) >= HitObject.HitWindow)
                return;

            ApplyMaxResult();
        }
    }

    internal sealed partial class StableDrawableHit : DrawableHit
    {
        public StableDrawableHit()
            : this(null)
        {
        }

        public StableDrawableHit(Hit? hit)
            : base(hit)
        {
        }

        protected override DrawableStrongNestedHit CreateStrongNestedHit(Hit.StrongNestedHit hitObject) => new StableStrongNestedHit(hitObject);

        public partial class StableStrongNestedHit : StrongNestedHit
        {
            public StableStrongNestedHit()
                : this(null)
            {
            }

            public StableStrongNestedHit(Hit.StrongNestedHit? hitObject)
                : base(hitObject)
            {
            }

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (!ParentHitObject.Result.HasResult)
                {
                    base.CheckForResult(userTriggered, timeOffset);
                    return;
                }

                if (!ParentHitObject.Result.IsHit)
                {
                    ApplyMinResult();
                    return;
                }

                double timeSinceFirstHit = timeOffset - ParentHitObject.Result.TimeOffset;

                if (!userTriggered)
                {
                    // stable uses a strict < 30ms window.
                    if (timeSinceFirstHit >= 30)
                        ApplyMinResult();

                    return;
                }

                if (Math.Abs(timeSinceFirstHit) < 30)
                    ApplyMaxResult();
            }

            public override bool OnPressed(KeyBindingPressEvent<TaikoAction> e)
            {
                // Don't process actions until the main hitobject is hit.
                if (!ParentHitObject.IsHit)
                    return false;

                // stable allows a release + repress (or the other button) within the window,
                // so unlike lazer's stock implementation this does not require HitAction != null.
                if (ParentHitObject.HitActions?.Contains(e.Action) != true)
                    return false;

                return UpdateResult(true);
            }
        }
    }
}
