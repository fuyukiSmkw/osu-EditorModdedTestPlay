using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets.EditorModdedTestPlay.Screens.Edit.GameplayTest;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;

#nullable enable

namespace osu.Game.Rulesets.EditorModdedTestPlay.ListenerLoader.Handlers.ScreenHandlers;

public partial class ModdedEditorPlayerLoaderHandler : AbstractScreenHandler
{
    public override void Handle(IScreen prev, IScreen next)
    {
        if (next is not EditorPlayerLoader editorPlayerLoader || next is ModdedEditorPlayerLoader) return;

        Logging.Log("ModdedEditorPlayerLoaderHandler: next is EPL!");

        Editor editor = (Editor)prev!; // prev is Editor

        waitUntilEditorLoaderReady(editorPlayerLoader, () =>
        {
            Logging.Log("ModdedEditorPlayerLoaderHandler: Waited!");
            (ScreenStack ?? throw new NullDependencyException("ScreenStack is null!")).Exit();
            ScreenStack.Push(new ModdedEditorPlayerLoader(editor));
        });
    }

    private void waitUntilEditorLoaderReady(EditorPlayerLoader epl, Action action)
    {
        if (!epl.IsLoaded)
            this.Delay(10).Schedule(() => waitUntilEditorLoaderReady(epl, action));

        this.Delay(1).Schedule(action);
    }
}
