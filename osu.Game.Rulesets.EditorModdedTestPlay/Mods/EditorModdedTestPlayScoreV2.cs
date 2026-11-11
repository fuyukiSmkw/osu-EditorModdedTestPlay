using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Mods
{
    /// <summary>
    /// An editor-test-play-only ScoreV2 toggle.
    /// Selecting it makes <c>ModdedEditorPlayerLoader</c> use the hidden stable-ScoreV2 rulesets.
    /// </summary>
    public class EditorModdedTestPlayScoreV2 : ModScoreV2, IApplicableMod
    {
        // Keep the same name/acronym/icon as the real ScoreV2 marker, but make it selectable
        // in the editor's mod select overlay. It is deliberately not added to the global mod list.
        public override ModType Type => ModType.Conversion;
        public override bool UserPlayable => true;
        public override LocalisableString Description => "Use osu!stable ScoreV2 scoring in editor test play.";
    }
}
