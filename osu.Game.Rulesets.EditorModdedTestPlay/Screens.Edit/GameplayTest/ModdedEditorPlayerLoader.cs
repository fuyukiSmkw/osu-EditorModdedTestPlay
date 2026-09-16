// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest
{
    public partial class ModdedEditorPlayerLoader : PlayerLoader
    {
        protected override double PlayerPushDelay => 1; // instant start

        [Resolved]
        private OsuLogo osuLogo { get; set; } = null!;

        public ModdedEditorPlayerLoader(Editor editor)
            : base(() => new ModdedEditorPlayer(editor))
        {
            this.SetPropertyValue("QuickRestart", true);
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
    }
}
