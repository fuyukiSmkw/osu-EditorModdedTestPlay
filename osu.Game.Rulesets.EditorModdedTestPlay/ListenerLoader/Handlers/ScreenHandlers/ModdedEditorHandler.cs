using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit;
using osu.Game.Screens.Edit;

#nullable enable

namespace osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Handlers.ScreenHandlers;

public partial class ModdedEditorHandler : AbstractScreenHandler
{
    public override void Handle(IScreen prev, IScreen next)
    {
        if (next is not EditorLoader editorLoader || next is ModdedEditorLoader) return;

        waitUntilEditorLoaderReady(editorLoader, () =>
        {
            (ScreenStack ?? throw new NullDependencyException("ScreenStack is null!")).Exit();
            ScreenStack.Push(new ModdedEditorLoader());
        });
    }

    private void waitUntilEditorLoaderReady(EditorLoader el, Action action)
    {
        if (!el.IsLoaded)
            this.Delay(10).Schedule(() => waitUntilEditorLoaderReady(el, action));

        this.Delay(1).Schedule(action);
    }
}
