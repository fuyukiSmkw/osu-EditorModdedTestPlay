// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;
using osu.Game.Rulesets.EditorModdedTestPlay.Mods;
using osu.Game.Rulesets.EditorModdedTestPlay.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest
{
    public partial class ModdedEditorPlayerLoader : PlayerLoader
    {
        private readonly Editor editor;

        private LeasedBindable<RulesetInfo>? hiddenRulesetLease;
        private LeasedBindable<IReadOnlyList<Mod>>? playerModsLease;

        protected override double PlayerPushDelay => 1; // instant start

        [Resolved]
        private OsuLogo osuLogo { get; set; } = null!;

        public ModdedEditorPlayerLoader(Editor editor)
            : base(() => new ModdedEditorPlayer(editor))
        {
            this.editor = editor;
            this.SetPropertyValue("QuickRestart", true);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = base.CreateChildDependencies(parent);

            if (editor.Mods.Value?.Any(m => m is EditorModdedTestPlayScoreV2) != true)
                return dependencies;

            var hiddenRuleset = HiddenScoreV2Rulesets.CreateInfo(Ruleset.Value);

            if (hiddenRuleset == null)
                return dependencies;

            // Shadow the ruleset bindable for the player this loader creates only.
            // The editor's (and the game's) ruleset is not modified.
            var scopedDependencies = new DependencyContainer(dependencies);

            hiddenRulesetLease = new Bindable<RulesetInfo>(hiddenRuleset).BeginLease(true);
            scopedDependencies.CacheAs(hiddenRulesetLease);

            // The ScoreV2 marker only exists to pick the hidden ruleset above. It is not part of any
            // ruleset's mod list (`Player` validates this via `ModUtils.CheckModsBelongToRuleset()`,
            // and mania's equivalent is `ManiaModScoreV2`, which our marker does not derive from),
            // so strip it from the mods handed to the player.
            IReadOnlyList<Mod> playerMods = editor.Mods.Value?.Where(m => m is not EditorModdedTestPlayScoreV2).ToArray()
                                            ?? Array.Empty<Mod>();

            playerModsLease = new Bindable<IReadOnlyList<Mod>>(playerMods).BeginLease(true);
            scopedDependencies.CacheAs(playerModsLease);

            return scopedDependencies;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            MetadataInfo.FinishTransforms(true);
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            // call base with resuming forcefully set to true to reduce logo movements.
            base.LogoArriving(logo, true);
            logo.FinishTransforms(true, nameof(Scale));
        }

        protected override void ContentOut()
        {
            base.ContentOut();
            osuLogo.FadeOut(CONTENT_OUT_DURATION, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            hiddenRulesetLease?.Return();
            playerModsLease?.Return();
        }
    }
}
