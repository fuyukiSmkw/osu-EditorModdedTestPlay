using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Play;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest
{
    public partial class ModdedEditorPlayer : EditorPlayer
    {
        private bool isInitiallyAutoplay => GameplayState.Mods.OfType<ModAutoplay>().Any();

        public ModdedEditorPlayer(Editor editor)
            : base(editor) { }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Start with autoplay if autoplay mod found
            var toggleAutoplayMethod = this.FindMethod("toggleAutoplay", typeof(void))!;
            if (isInitiallyAutoplay)
                toggleAutoplayMethod.Invoke([]);
        }
    }
}
