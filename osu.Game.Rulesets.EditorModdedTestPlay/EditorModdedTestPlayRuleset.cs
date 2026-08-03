// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.EditorModdedTestPlay.Beatmaps;
using osu.Game.Rulesets.EditorModdedTestPlay.Graphics;
using osu.Game.Rulesets.EditorModdedTestPlay.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorModdedTestPlay
{
    public partial class EditorModdedTestPlayRuleset : Ruleset
    {
        public override string Description => "Modded Editor";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null) =>
            new DrawableEditorModdedTestPlayRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) =>
            new EditorModdedTestPlayBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new EditorModdedTestPlayDifficultyCalculator(RulesetInfo, beatmap);

        public override IEnumerable<Mod> GetModsFor(ModType type) => [];

        public override string ShortName => SHORT_NAME;

        public static readonly string SHORT_NAME = "editormoddedtestplayruleset";

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => [];

        public override Drawable CreateIcon() => new IconWithListenerLoader();

        public partial class IconWithListenerLoader : EditorModdedTestPlayIcon
        {
            public IconWithListenerLoader()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader(permitNulls: true)]
            private void load(OsuGame game, Storage storage, IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api)
            {
                try
                {
                    Logging.Log("Begin init ListenerLoader");
                    Logging.Log($"Deps: Game = '{game}' :: Storage = '{storage}' :: Importer = '{beatmapImporter}' :: IAPIProvider = '{api}'");

                    if (!ListenerLoader.ListenerLoader.INSTANCE.BeginInject(storage, game, Scheduler))
                    {
                        Logging.Log("Injection failed!", level: LogLevel.Error);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logging.LogError(e, "Unknown exception");
                }
            }
        }

        // Leave this line intact. It will bake the correct version into the ruleset on each build/release.
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}
