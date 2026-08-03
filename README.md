> [!CAUTION]
> # USE AT YOUR OWN RISK!
> Using this ruleset may result in an **account ban**, as [warned by peppy](https://github.com/Cai1Hsu/osu-plugins/issues/93).
>
> Although no ban has been reported for such case, the possibility still exists. \
> Do **NOT** use this ruleset in an online/release lazer build, and do **NOT** try to submit any scores to the official server with the ruleset running. \
> Refrain from use until peppy provides further clarification on the permitted scope for custom rulesets.

# osu-EditorModdedTestPlay

An osu!lazer custom ruleset that adds Mod Select into Editor so you can test play with mods.

## Showcase

![](https://github.com/fuyukiSmkw/picx-images-hosting/raw/master/osu-EditorModdedTestPlay.6bhsbf2ckn.gif)

## Install & Uninstall

### Installation script (recommended)

Download and run the [`installer script` (Windows)](https://github.com/fuyukiSmkw/osu-EditorModdedTestPlay/releases/latest/download/osu.Game.Rulesets.EditorModdedTestPlay.installer.bat) or [`installer script` (Linux/Mac)](https://github.com/fuyukiSmkw/osu-EditorModdedTestPlay/releases/latest/download/osu.Game.Rulesets.EditorModdedTestPlay.installer.sh).

Similarly you can use [`uninstaller script` (Windows)](https://github.com/fuyukiSmkw/osu-EditorModdedTestPlay/releases/latest/download/osu.Game.Rulesets.EditorModdedTestPlay.uninstaller.bat) or [`uninstaller script` (Linux/Mac)](https://github.com/fuyukiSmkw/osu-EditorModdedTestPlay/releases/latest/download/osu.Game.Rulesets.EditorModdedTestPlay.uninstaller.sh).

### Manual install

The script above does the same as follows.

Go to Releases, download the [`.dll` file](https://github.com/fuyukiSmkw/osu-EditorModdedTestPlay/releases/latest/download/osu.Game.Rulesets.EditorModdedTestPlay.dll), and place it in the `rulesets` subfolder of your osu!lazer directory.

* On Windows, this is usually located at `%APPDATA%\osu\rulesets`
* On Linux, it's usually at `~/.local/share/osu/rulesets`

For uninstallation, delete the `.dll` file.

## Build

Requires .NET `8.0` or higher.

```bash
dotnet build -c Release
```

Or `dotnet build` for a debug build.

## Acknowledgements

* [ppy/osu](https://github.com/ppy/osu): The official osu!lazer project
* [MATRIX-feather/LLin](https://github.com/MATRIX-feather/LLin): A custom ruleset that provides beatmap downloads from a mirror site and includes a music player
