using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.EditorModdedTestPlay.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit
{
    /// <summary>
    /// The editor's mod select overlay.
    /// Adds the editor-only ScoreV2 toggle without exposing it in the normal game-wide mod select.
    /// </summary>
    public partial class EditorModSelectOverlay : UserModSelectOverlay
    {
        private readonly ModState scoreV2State = new ModState(new EditorModdedTestPlayScoreV2());
        private bool synchronisingSelection;

        public EditorModSelectOverlay()
            : base(OverlayColourScheme.Green)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scoreV2State.Active.BindValueChanged(onScoreV2ActiveChanged);
            SelectedMods.BindValueChanged(_ => updateScoreV2State(), true);
            AvailableMods.BindValueChanged(_ => Schedule(injectScoreV2));

            injectScoreV2();
        }

        private void injectScoreV2()
        {
            var availableMods = AvailableMods.Value;

            if (!availableMods.TryGetValue(ModType.Conversion, out var conversionMods))
                conversionMods = Array.Empty<ModState>();

            if (!conversionMods.Contains(scoreV2State))
            {
                var updatedMods = conversionMods.ToList();
                updatedMods.Add(scoreV2State);
                availableMods[ModType.Conversion] = updatedMods;
            }

            foreach (var column in this.ChildrenOfType<ModColumn>().Where(c => c.ModType == ModType.Conversion))
            {
                if (!column.AvailableMods.Contains(scoreV2State))
                    column.AvailableMods = availableMods[ModType.Conversion];
            }

            updateScoreV2State();
        }

        private void updateScoreV2State()
        {
            if (synchronisingSelection)
                return;

            synchronisingSelection = true;
            scoreV2State.Active.Value = SelectedMods.Value.Any(m => m is EditorModdedTestPlayScoreV2);
            synchronisingSelection = false;
        }

        private void onScoreV2ActiveChanged(ValueChangedEvent<bool> active)
        {
            if (synchronisingSelection)
                return;

            var selectedMods = SelectedMods.Value;
            bool currentlySelected = selectedMods.Any(m => m is EditorModdedTestPlayScoreV2);

            if (active.NewValue == currentlySelected)
                return;

            synchronisingSelection = true;

            var newSelection = selectedMods.Where(m => m is not EditorModdedTestPlayScoreV2).ToList();

            if (active.NewValue)
                newSelection.Add(scoreV2State.Mod);

            SelectedMods.Value = newSelection;

            synchronisingSelection = false;
        }
    }
}
