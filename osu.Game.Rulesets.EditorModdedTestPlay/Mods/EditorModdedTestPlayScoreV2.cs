using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Mods
{
    /// <summary>
    /// An editor-test-play-only ScoreV2 toggle.
    /// Selecting it makes <c>ModdedEditorPlayerLoader</c> use the hidden stable-ScoreV2 rulesets,
    /// and it remains in the player's mod list so the HUD and results screen can display it.
    /// </summary>
    /// <remarks>
    /// This derives from <see cref="ManiaModScoreV2"/> rather than <see cref="ModScoreV2"/> because
    /// <see cref="ManiaModScoreV2"/> itself derives from <see cref="ModScoreV2"/>. That makes the mod
    /// assignable to a mod type present in every ruleset's <see cref="Ruleset.AllMods"/> list
    /// (osu!/taiko/catch expose <see cref="ModScoreV2"/>, mania exposes <see cref="ManiaModScoreV2"/>),
    /// which is what <c>Player</c> validates against.
    /// </remarks>
    public class EditorModdedTestPlayScoreV2 : ManiaModScoreV2, IApplicableToBeatmap
    {
        // Keep the same name/acronym/icon as the real ScoreV2 marker, but make it selectable
        // in the editor's mod select overlay. It is deliberately not added to the global mod list.
        public override ModType Type => ModType.Conversion;
        public override bool UserPlayable => true;
        public override LocalisableString Description => "Use osu!stable ScoreV2 scoring in editor test play.";

        // ManiaModScoreV2 changes mania hit windows to their ScoreV2 variants. stable scoring is
        // implemented by the hidden ruleset's score processor, and editor test play intends to keep
        // the beatmap's existing hit windows, so leave them untouched here.
        void IApplicableToBeatmap.ApplyToBeatmap(IBeatmap beatmap)
        {
        }
    }
}
