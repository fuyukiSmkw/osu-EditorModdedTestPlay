// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Replays
{
    public class EditorModdedTestPlayFramedReplayInputHandler(Replay replay) : FramedReplayInputHandler<EditorModdedTestPlayReplayFrame>(replay)
    {
        protected override bool IsImportant(EditorModdedTestPlayReplayFrame frame) => false;
    }
}
