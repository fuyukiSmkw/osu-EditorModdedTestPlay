using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest
{
    /// <summary>
    /// Results screen for editor test play. It deliberately does not fetch online leaderboards.
    /// </summary>
    public partial class ModdedEditorResultsScreen : ResultsScreen
    {
        public ModdedEditorResultsScreen(ScoreInfo score)
            : base(score)
        {
            IsLocalPlay = true;
            AllowRetry = true;
            AllowWatchingReplay = false;
        }
    }
}
