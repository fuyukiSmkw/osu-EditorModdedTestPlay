#nullable enable

using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Screens;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest
{
    public partial class ModdedEditorPlayer : EditorPlayer
    {
        private bool isInitiallyAutoplay => GameplayState.Mods.OfType<ModAutoplay>().Any();

        /// <summary>
        /// Whether the automatic exit at the end of gameplay was already converted into a results screen display.
        /// </summary>
        private bool resultsRequested;

        public ModdedEditorPlayer(Editor editor)
            : base(editor)
        {
            // EditorPlayer hardcodes ShowResults = false; enable the results screen for ModdedEditor test play.
            Configuration.ShowResults = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // If results manage to display through the normal Player flow (before EditorPlayer's auto-exit),
            // make sure the eventual exit from the results screen is not misinterpreted as an automatic one.
            OnShowingResults += () => resultsRequested = true;

            // Start with autoplay if autoplay mod found
            var toggleAutoplayMethod = this.FindMethod("toggleAutoplay", typeof(void))!;
            if (isInitiallyAutoplay)
                toggleAutoplayMethod.Invoke([]);
        }

        protected override ResultsScreen CreateResults(ScoreInfo score) => new ModdedEditorResultsScreen(score);

        // Test play scores are only for previewing; don't write them to the local score database.
        protected override Task ImportScore(Score score) => Task.CompletedTask;

        public override bool OnExiting(ScreenExitEvent e)
        {
            // EditorPlayer exits as soon as gameplay completes, which happens before the normal
            // results progression can run. Convert that single automatic exit into a results display.
            // A restart requested after completion (e.g. from the pause menu) is left untouched: in that
            // case the parent loader has already been made resumable by `prepareForRestart()`.
            if (!resultsRequested && Configuration.ShowResults && GameplayState.HasPassed && this.IsCurrentScreen() && !e.Next.ValidForResume)
            {
                resultsRequested = true;

                // PerformExit() calls progressToResults() while gameplay has passed, and does not exit.
                PerformExit();

                // Block this exit; the results screen is now queued and will be pushed once ready.
                return true;
            }

            return base.OnExiting(e);
        }
    }
}
