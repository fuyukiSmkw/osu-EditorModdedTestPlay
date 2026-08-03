using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Screens.Edit;
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

    protected override void LoadComplete()
    {
        base.LoadComplete();

        modSelectOverlayRegistration = registerBlockingOverlay(modSelectOverlay);

        var item = new EditorMenuItem("Mod Select", MenuItemType.Standard, modSelectOverlay.ToggleVisibility);
        var menuBar = this.ChildrenOfType<EditorMenuBar>().FirstOrDefault() ?? throw new NullDependencyException("EditorMenuBar not found in Editor!");
        menuBar.Add(item);
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
