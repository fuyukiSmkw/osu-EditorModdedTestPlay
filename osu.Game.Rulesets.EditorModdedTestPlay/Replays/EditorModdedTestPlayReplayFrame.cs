// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Replays
{
    public class EditorModdedTestPlayReplayFrame : ReplayFrame
    {
        public List<EditorModdedTestPlayAction> Actions = [];
        public Vector2 Position;

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is EditorModdedTestPlayReplayFrame freeformFrame && Time == freeformFrame.Time && Position == freeformFrame.Position && Actions.SequenceEqual(freeformFrame.Actions);
    }
}
