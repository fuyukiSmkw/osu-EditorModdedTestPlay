#nullable enable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Scoring
{
    /// <summary>
    /// Registers the stable ScoreV2 slider/spinner drawables on top of the regular osu! playfield.
    /// </summary>
    internal sealed partial class StableOsuPlayfield : OsuPlayfield
    {
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
