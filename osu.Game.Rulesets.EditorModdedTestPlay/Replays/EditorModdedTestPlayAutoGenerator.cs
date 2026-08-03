// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.EditorModdedTestPlay.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Replays
{
    public class EditorModdedTestPlayAutoGenerator : AutoGenerator<EditorModdedTestPlayReplayFrame>
    {
        public new Beatmap<EditorModdedTestPlayHitObject> Beatmap => (Beatmap<EditorModdedTestPlayHitObject>)base.Beatmap;

        public EditorModdedTestPlayAutoGenerator(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        protected override void GenerateFrames()
        {
        }
    }
}
