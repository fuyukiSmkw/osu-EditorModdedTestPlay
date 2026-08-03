// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.EditorModdedTestPlay.Objects;
using osu.Game.Rulesets.EditorModdedTestPlay.Objects.Drawables;
using osu.Game.Rulesets.EditorModdedTestPlay.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorModdedTestPlay.UI
{
    [Cached]
    public partial class DrawableEditorModdedTestPlayRuleset(EditorModdedTestPlayRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods = null) : DrawableRuleset<EditorModdedTestPlayHitObject>(ruleset, beatmap, mods)
    {
        protected override Playfield CreatePlayfield() => new EditorModdedTestPlayPlayfield();

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new EditorModdedTestPlayFramedReplayInputHandler(replay);

        public override DrawableHitObject<EditorModdedTestPlayHitObject> CreateDrawableRepresentation(EditorModdedTestPlayHitObject h) => new DrawableEditorModdedTestPlayHitObject(h);

        protected override PassThroughInputManager CreateInputManager() => new EditorModdedTestPlayInputManager(Ruleset?.RulesetInfo);
    }
}
