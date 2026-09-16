using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Menus;

#nullable enable

namespace osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit;

public partial class ModdedEditorLoader : EditorLoader
{
    protected override Editor CreateEditor() => new ModdedEditor(this);
}

public partial class ModdedEditor(EditorLoader? loader = null) : Editor(loader)
{
    private ModSelectOverlay modSelectOverlay = null!;
    private IDisposable? modSelectOverlayRegistration;
    [Resolved]
    private OsuGame game { get; set; } = null!;

    private IDisposable registerBlockingOverlay(OverlayContainer overlay)
    {
        var asm = typeof(OsuGame).Assembly;
        var iOverlayManagerType = asm.GetType("osu.Game.Overlays.IOverlayManager") ?? throw new NullDependencyException("osu.Game.Overlays.IOverlayManager type not found!");
        var method = iOverlayManagerType.GetMethod("RegisterBlockingOverlay") ?? throw new NullDependencyException("RegisterBlockingOverlay method not found!");
        return (IDisposable)method.Invoke(game, [overlay])!;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        LoadComponent(modSelectOverlay = new UserModSelectOverlay
        {
            ShowPresets = true,
        });
        Mods.Disabled = false;
    }

    // the old way: modified tabs
    /* public partial class ModdedPlaybackTabControl : PlaybackControl.PlaybackTabControl
    {
        private static readonly double[] old_tempo_values = [0.25, 0.5, 0.75, 1];
        private static readonly double[] new_tempo_values = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];

        public partial class ModdedPlaybackTabItem(double value) : PlaybackTabItem(value)
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                Width = 1f / new_tempo_values.Length;
            }
        }

        protected override TabItem<double> CreateTabItem(double value) => new ModdedPlaybackTabItem(value);

        public ModdedPlaybackTabControl(BindableNumber<double> tempoAdjustment)
        {
            old_tempo_values.ForEach(RemoveItem);
            new_tempo_values.ForEach(AddItem);
            RelativeSizeAxes = Axes.X;
            Height = 16;
            Current = tempoAdjustment;
        }
    }*/

    public partial class PlaybackSpeedSliderBar : FormSliderBar<double>
    {
        public PlaybackSpeedSliderBar(BindableNumber<double> tempoAdjustment)
        {
            tempoAdjustment.MinValue = 0.1; // to avoid "TrackBass does not support Tempo specifications below 0.05"
            tempoAdjustment.MaxValue = 3.00;
            tempoAdjustment.Precision = 0.01;

            Current = tempoAdjustment;
            RelativeSizeAxes = Axes.X;
            Caption = "Speed";
            TransferValueOnCommit = false;
            KeyboardStep = 0.1f; // 0.1x per <-/-> press
            LabelFormat = v => $"{v:N2}x";
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        modSelectOverlayRegistration = registerBlockingOverlay(modSelectOverlay);

        // Add menuBar Mod Select item
        var item = new EditorMenuItem("Mod Select", MenuItemType.Standard, modSelectOverlay.ToggleVisibility);
        var menuBar = this.ChildrenOfType<EditorMenuBar>().FirstOrDefault() ?? throw new NullDependencyException("EditorMenuBar not found in Editor!");
        menuBar.Add(item);

        var playbackControl = this.ChildrenOfType<PlaybackControl>().FirstOrDefault() ?? throw new NullDependencyException("PlaybackControl type not found in Editor!");
        var tempoAdjustment = (BindableNumber<double>)(playbackControl.FindInstance("tempoAdjustment") ?? throw new NullDependencyException("tempoAdjustment not found in playbackControl!"));
        var playbackSpeedControl = (CompositeDrawable)(playbackControl.FindInstance("playbackSpeedControl") ?? throw new NullDependencyException("playbackSpeedControl not found in playbackControl!"));

        // the old way: modified tabs
        /* var oldPlaybackTabControl = playbackSpeedControl.ChildrenOfType<PlaybackControl.PlaybackTabControl>().FirstOrDefault() ?? throw new NullDependencyException("PlaybackTabControl not found in Editor!");
        (playbackSpeedControl.FindMethod("RemoveInternal", typeof(bool)) ?? throw new NullDependencyException("Method RemoveInternal not found in PlaybackSpeedControl!"))([oldPlaybackTabControl, true]);
        // (playbackSpeedControl.FindMethod("AddInternal", typeof(bool)) ?? throw new NullDependencyException("Method AddInternal not found in PlaybackSpeedControl!"))([new ModdedPlaybackTabControl(tempoAdjustment)]); */
        (playbackSpeedControl.FindMethod("ClearInternal", typeof(bool)) ?? throw new NullDependencyException("Method ClearInternal not found in PlaybackSpeedControl!"))([true]); // remove all children
        (playbackSpeedControl.FindMethod("AddInternal", typeof(bool)) ?? throw new NullDependencyException("Method AddInternal not found in PlaybackSpeedControl!"))([new PlaybackSpeedSliderBar(tempoAdjustment)]);
    }


    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        modSelectOverlayRegistration?.Dispose();
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        onArrivingAtScreen();
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        onArrivingAtScreen();
    }

    private void onArrivingAtScreen()
    {
        modSelectOverlay.Beatmap.Disabled = false;
        modSelectOverlay.Beatmap.BindTo(Beatmap);
        modSelectOverlay.Ruleset.Disabled = false;
        modSelectOverlay.Ruleset.BindTo(Ruleset);
        modSelectOverlay.SelectedMods.Disabled = false;
        modSelectOverlay.SelectedMods.BindTo(Mods);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        onLeavingScreen();
        return base.OnExiting(e);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        onLeavingScreen();
        base.OnSuspending(e);
    }

    private void onLeavingScreen()
    {
        modSelectOverlay.SelectedMods.UnbindFrom(Mods);
        modSelectOverlay.Ruleset.UnbindFrom(Ruleset);
        modSelectOverlay.Beatmap.UnbindFrom(Beatmap);
    }

}
