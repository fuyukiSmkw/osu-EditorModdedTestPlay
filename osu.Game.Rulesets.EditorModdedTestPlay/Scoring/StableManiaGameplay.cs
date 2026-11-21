#nullable enable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Mania drawables and playfield used only by the hidden stable ScoreV2 ruleset.
    /// </summary>
    /// <remarks>
    /// These reproduce osu!stable's mania judgement quirks which lazer deliberately does not implement:
    /// <list type="bullet">
    /// <item><description>the late OK window is exclusive and a late MEH result is impossible,</description></item>
    /// <item><description>a missed hold note head does not cap the tail judgement,</description></item>
    /// <item><description>releasing a hold earlier than its 50 window breaks it even when the release is still
    /// within the 1.5x release lenience.</description></item>
    /// </list>
    /// </remarks>
    internal sealed partial class StableDrawableManiaRuleset : DrawableManiaRuleset
    {
        public StableDrawableManiaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new StableManiaPlayfield(Beatmap.Stages);
    }

    /// <summary>
    /// Creates the stable ScoreV2 column type so that its pools can be overridden.
    /// </summary>
    internal sealed partial class StableManiaPlayfield : ManiaPlayfield
    {
        public StableManiaPlayfield(List<StageDefinition> stageDefinitions)
            : base(stageDefinitions)
        {
        }

        protected override Stage CreateStage(int firstColumnIndex, StageDefinition stageDefinition, ref ManiaAction columnAction)
            => new StableManiaStage(firstColumnIndex, stageDefinition, ref columnAction);
    }

    /// <summary>
    /// Creates the stable ScoreV2 column type so that its pools can be overridden.
    /// </summary>
    internal sealed partial class StableManiaStage : Stage
    {
        public StableManiaStage(int firstColumnIndex, StageDefinition definition, ref ManiaAction columnStartAction)
            : base(firstColumnIndex, definition, ref columnStartAction)
        {
        }

        protected override Column CreateColumn(int index, bool isSpecial) => new StableManiaColumn(index, isSpecial);
    }

    /// <summary>
    /// Replaces the mania drawable pools with the stable ScoreV2 variants.
    /// </summary>
    /// <remarks>
    /// The stock <see cref="Column"/> registers its pools in a private dependency loader, which runs
    /// before this one. Registering the same hit object types here therefore replaces them.
    /// </remarks>
    internal sealed partial class StableManiaColumn : Column
    {
        public StableManiaColumn(int index, bool isSpecial)
            : base(index, isSpecial)
        {
        }

        [BackgroundDependencyLoader]
        private void loadStablePools()
        {
            RegisterPool<Note, StableDrawableNote>(10, 50);
            RegisterPool<HoldNote, StableDrawableHoldNote>(10, 50);
            RegisterPool<HeadNote, StableDrawableHoldNoteHead>(10, 50);
            RegisterPool<TailNote, StableDrawableHoldNoteTail>(10, 50);
        }
    }

    internal static class StableManiaJudgement
    {
        /// <summary>
        /// Whether a late time offset has passed osu!stable's OK hit window.
        /// </summary>
        /// <remarks>
        /// stable auto-misses a mania object at <c>StartTime + HitWindow100</c>, making the late OK
        /// window exclusive and a late MEH result impossible. lazer's mania windows are floored and
        /// then raised by 0.5ms, so the 0.5 is subtracted to recover stable's integer boundary.
        /// </remarks>
        public static bool HasPassedLateWindow(HitWindows? windows, double timeOffset)
            => windows != null && timeOffset >= windows.WindowFor(HitResult.Ok) - 0.5;

        /// <summary>
        /// Whether releasing at the given offset (before the tail) should break the hold.
        /// </summary>
        /// <remarks>
        /// stable breaks the hold as soon as the release is earlier than <c>EndTime - HitWindow50</c>,
        /// while lazer only breaks once the release falls outside the 1.5x tail lenience.
        /// </remarks>
        public static bool IsEarlyHoldBreak(HitWindows? windows, double timeOffset)
            => windows != null && timeOffset < -(windows.WindowFor(HitResult.Meh) - 0.5);
    }

    /// <summary>
    /// A mania note which uses osu!stable's late hit window handling.
    /// </summary>
    internal sealed partial class StableDrawableNote : DrawableNote
    {
        public StableDrawableNote()
        {
        }

        public StableDrawableNote(Note note)
            : base(note)
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (StableManiaJudgement.HasPassedLateWindow(HitObject.HitWindows, timeOffset))
            {
                ApplyMinResult();
                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }

    /// <summary>
    /// A mania hold note which creates stable ScoreV2 versions of its nested objects when pools are unavailable.
    /// </summary>
    internal sealed partial class StableDrawableHoldNote : DrawableHoldNote
    {
        public StableDrawableHoldNote()
        {
        }

        public StableDrawableHoldNote(HoldNote holdNote)
            : base(holdNote)
        {
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            switch (hitObject)
            {
                case TailNote tail:
                    return new StableDrawableHoldNoteTail(tail);

                case HeadNote head:
                    return new StableDrawableHoldNoteHead(head);

                default:
                    return base.CreateNestedHitObject(hitObject);
            }
        }
    }

    /// <summary>
    /// The head of a <see cref="StableDrawableHoldNote"/>.
    /// </summary>
    internal sealed partial class StableDrawableHoldNoteHead : DrawableHoldNoteHead
    {
        public StableDrawableHoldNoteHead()
        {
        }

        public StableDrawableHoldNoteHead(HeadNote headNote)
            : base(headNote)
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (StableManiaJudgement.HasPassedLateWindow(HitObject.HitWindows, timeOffset))
            {
                ApplyMinResult();
                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }
    }

    /// <summary>
    /// The tail of a <see cref="StableDrawableHoldNote"/>.
    /// </summary>
    internal sealed partial class StableDrawableHoldNoteTail : DrawableHoldNoteTail
    {
        public StableDrawableHoldNoteTail()
        {
        }

        public StableDrawableHoldNoteTail(TailNote tailNote)
            : base(tailNote)
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered
                && StableManiaJudgement.IsEarlyHoldBreak(HitObject.HitWindows, timeOffset)
                && ParentHitObject is DrawableHoldNote holdNote
                && !holdNote.Body.AllJudged)
            {
                holdNote.Body.MissForcefully();
            }

            double adjustedTimeOffset = timeOffset / TailNote.RELEASE_WINDOW_LENIENCE;

            if (StableManiaJudgement.HasPassedLateWindow(HitObject.HitWindows, adjustedTimeOffset))
            {
                ApplyMinResult();
                return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }

        protected override HitResult GetCappedResult(HitResult result)
        {
            // stable only caps the tail when the hold was actually broken. A missed head does not cap it.
            if (result > HitResult.Meh && HoldNote.Body.HasHoldBreak)
                return HitResult.Meh;

            return result;
        }
    }
}
