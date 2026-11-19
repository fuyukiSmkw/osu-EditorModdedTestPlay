#nullable enable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Registers the stable ScoreV2 slider/spinner drawables on top of the regular osu! playfield.
    /// </summary>
    internal sealed partial class StableOsuPlayfield : OsuPlayfield
    {
        private JudgementPooler<DrawableOsuJudgement> rawHeadJudgementPooler = null!;

        private readonly Dictionary<HitObject, (Container container, DrawableOsuJudgement judgement)> rawHeadJudgements
            = new Dictionary<HitObject, (Container container, DrawableOsuJudgement judgement)>();

        [BackgroundDependencyLoader]
        private void loadStablePools()
        {
            // OsuPlayfield.load() has already registered the stock drawables. Registering these
            // afterwards replaces the pool entries used for the corresponding hit object types.
            RegisterPool<Slider, StableDrawableSlider>(20, 100);
            RegisterPool<SliderHeadCircle, StableDrawableSliderHead>(20, 100);

            RegisterPool<StableSpinner, StableDrawableSpinner>(2, 20);
            RegisterPool<StableSpinnerTick, StableDrawableSpinnerTick>(20, 200);
            RegisterPool<StableSpinnerBonusTick, StableDrawableSpinnerBonusTick>(20, 200);

            rawHeadJudgementPooler = new JudgementPooler<DrawableOsuJudgement>(new[]
            {
                HitResult.Great,
                HitResult.Ok,
                HitResult.Meh,
                HitResult.Miss,
            });

            AddInternal(rawHeadJudgementPooler);

            NewResult += onStableNewResult;
            RevertResult += onStableRevertResult;
        }

        private void onStableNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!DisplayJudgements.Value || judgedObject is not StableDrawableSliderHead head)
                return;

            HitResult raw = head.RawHitResult;

            if (raw == HitResult.None)
                raw = result.IsHit ? HitResult.Great : HitResult.Miss;

            // stable hides successful 300 slider heads; the parent slider already shows its aggregate.
            if (raw == HitResult.Great)
                return;

            var displayResult = new OsuHitCircleJudgementResult(head.HitObject, new OsuJudgement()) { Type = raw };
            var judgement = rawHeadJudgementPooler.Get(raw, j => j.Apply(displayResult, head));

            if (judgement == null)
                return;

            Container container = head.DrawableSlider.OverlayElementContainer;

            if (rawHeadJudgements.Remove(head.HitObject, out var previous))
                previous.container.Remove(previous.judgement, false);

            rawHeadJudgements[head.HitObject] = (container, judgement);
            container.Add(judgement);
        }

        private void onStableRevertResult(JudgementResult result)
        {
            if (rawHeadJudgements.Remove(result.HitObject, out var entry))
                entry.container.Remove(entry.judgement, false);
        }
    }

    /// <summary>
    /// The drawable ruleset used for editor ScoreV2 test play.
    /// </summary>
    internal sealed partial class StableDrawableOsuRuleset : DrawableOsuRuleset
    {
        public StableDrawableOsuRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new StableOsuPlayfield();
    }
}
