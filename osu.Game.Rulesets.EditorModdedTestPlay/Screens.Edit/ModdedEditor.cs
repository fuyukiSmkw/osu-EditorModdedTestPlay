using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Utils;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Menus;
using osuTK.Graphics;

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

    public partial class ModdedPlaybackTabControl : PlaybackControl.PlaybackTabControl
    {
        private static readonly double[] old_tempo_values = [0.25, 0.5, 0.75, 1];
        private static readonly double[] new_tempo_values = [0.25, 0.5, 0.75, 1, 1.5, 3];

        protected override TabItem<double> CreateTabItem(double value) => new ModdedPlaybackTabItem(value);

        public ModdedPlaybackTabControl(BindableNumber<double> tempoAdjustment)
        {
            old_tempo_values.ForEach(RemoveItem);
            new_tempo_values.ForEach(AddItem);
            RelativeSizeAxes = Axes.X;
            Height = 16;
            Current = tempoAdjustment;
        }

        public partial class ModdedPlaybackTabItem(double value) : PlaybackTabItem(value)
        {
            private double v = value;

            [BackgroundDependencyLoader]
            private void load()
            {
                Width = 1f / new_tempo_values.Length;
                var text = (OsuSpriteText)this.FindInstance("text")!;
                var textBold = (OsuSpriteText)this.FindInstance("textBold")!;
                text.Text = textBold.Text = v.ToString("#.###", CultureInfo.InvariantCulture).TrimEnd('.');
            }
        }
    }

    public partial class PlaybackSpeedSliderBar : FormSliderBar<double>
    {
        public PlaybackSpeedSliderBar(BindableNumber<double> tempoAdjustment)
        {
            tempoAdjustment.MinValue = 0.1; // to avoid "TrackBass does not support Tempo specifications below 0.05"
            tempoAdjustment.MaxValue = 3.00;
            tempoAdjustment.Precision = 0.01;

            Current = tempoAdjustment;
            RelativeSizeAxes = Axes.X;
            // Caption = "Speed";
            TransferValueOnCommit = false;
            KeyboardStep = 0.1f; // 0.1x per <-/-> press
            LabelFormat = v => $"{v:N2}x";
            Height = 36;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var c = (Container)InternalChildren[1];
            c.Padding = new MarginPadding
            {
                Left = 8,
                Right = 4,
                Vertical = 4,
            };
            var s = c.ChildrenOfType<InnerSlider>().FirstOrDefault()!;
            s.Height = 20;
            s.Width = 0.67f;
            var f = c.ChildrenOfType<FillFlowContainer>().FirstOrDefault()!;
            f.Width = 0.33f;
            f.Padding = new MarginPadding(0);
            f.Anchor = Anchor.CentreLeft;
            f.Origin = Anchor.CentreLeft;
            var cap = f.ChildrenOfType<FormFieldCaption>().FirstOrDefault();
            cap?.Expire();
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

        (playbackSpeedControl.FindMethod("ClearInternal", typeof(bool)) ?? throw new NullDependencyException("Method ClearInternal not found in PlaybackSpeedControl!"))([true]); // remove all children
        FillFlowContainer c = new()
        {
            Direction = FillDirection.Vertical,
            RelativeSizeAxes = Axes.X,
            Height = 50,
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Children =
            [
                new ModdedPlaybackTabControl(tempoAdjustment),
                new PlaybackSpeedSliderBar(tempoAdjustment),
            ]
        };
        (playbackSpeedControl.FindMethod("AddInternal", typeof(bool)) ?? throw new NullDependencyException("Method AddInternal not found in PlaybackSpeedControl!"))([c]);
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
