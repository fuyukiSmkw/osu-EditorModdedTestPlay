# AGENTS.md

## 项目是什么

osu!lazer 自定义 ruleset 插件：在 Editor 中加入 **Mod Select**，并让 editor test play 可选启用**按 osu!stable 公式实现的 ScoreV2**（osu! / taiko / catch / mania 四种模式），同时显示 results screen。

- 交付物是单个 `osu.Game.Rulesets.EditorModdedTestPlay.dll`，放进 lazer 的 `rulesets` 目录即可。
- `EditorModdedTestPlayRuleset` 本身**不是可游玩规则集**：`CreateBeatmapConverter().CanConvert()` 返回 `false`，`GetModsFor()` 为空。它只是一个入口图标，用来触发运行时注入。
- ⚠️ 仅限本地/编辑器使用：不要在线游玩或提交成绩（见 README 的封号警告）。Test play 分数不会写入本地成绩库。

## 技术栈与版本

- C# / .NET `10.0` (`net10.0`)，解决方案含两个项目（ruleset + tests）。
- NuGet 依赖：`ppy.osu.Game` 及四个内置 ruleset 包，版本统一为 **`2026.916.0`**；升级时必须同步改这 5 个版本。
- `AutoMapper 15.1.3` 是为了规避旧版 `NU1903` 漏洞告警。
- `../osu` 是 osu!lazer 源码参考 checkout。它可能比 NuGet 包新，**以实际引用的包 API 为准**。
- Installer/uninstaller 脚本下载的是 GitHub Releases 里的 DLL，不会安装本地构建产物。

## 构建 / 运行

```bash
dotnet build                      # Debug
dotnet build -c Release           # 发布构建

# 可视化测试浏览器（需要桌面环境）：
dotnet run --project osu.Game.Rulesets.EditorModdedTestPlay.Tests -c Debug
```

手动联调：

1. `dotnet build -c Release`
2. 把 `osu.Game.Rulesets.EditorModdedTestPlay/bin/Release/net10.0/osu.Game.Rulesets.EditorModdedTestPlay.dll` 复制到 lazer 的 `rulesets` 目录（Windows `%APPDATA%\osu\rulesets`，Linux `~/.local/share/osu/rulesets`）。
3. 启动 lazer，进入 ruleset 选择界面让插件图标加载（注入由图标触发，见下）。

注意：

- 构建会出现来自传递依赖的 `NU1902`/`NU1903` 漏洞告警，属已知现象，不要把这类告警当成自己改动引入的问题。
- `osu.Game.Rulesets.EditorModdedTestPlay.Tests/.vscode` 下的配置已过期（仍指向 net8.0 和旧项目名），用 CLI 或自行更新后再用。
- Test 项目是 osu!framework 的 visual test browser，**没有覆盖计分逻辑的单元测试**，主要依赖 ingame 手动验证。

## 运行时架构（关键）

1. **注入入口**：lazer 显示 ruleset 列表时会创建 `EditorModdedTestPlayRuleset.CreateIcon()` 返回的 `IconWithListenerLoader`。它的 `BackgroundDependencyLoader` 调用 `ListenerLoader.INSTANCE.BeginInject(...)`，每个游戏会话只执行一次（用 Toolbar hash 去重）。
2. **挂载处理器**：`ListenerLoader` 把 DLL 资源加入 `OsuGame.Resources`，并向 `OsuGame` 添加 `EditorModdedTestPlayRulesetIconListener` 和 `ScreenHandlerManager`。
3. **规则集图标监听**：用户选中本 ruleset 时，立即切回之前的规则集，并 push `ModdedEditorLoader`。
4. **屏幕拦截**：`ScreenHandlerManager` 挂接 `OsuScreenStack` 的 screen 切换事件：
   - `ModdedEditorHandler`：把内置 `EditorLoader` 替换成 `ModdedEditorLoader`；
   - `ModdedEditorPlayerLoaderHandler`：把 `EditorPlayerLoader` 替换成 `ModdedEditorPlayerLoader`。
5. **Mod Select**：`ModdedEditor` 额外加载 `EditorModSelectOverlay`（继承 `UserModSelectOverlay`），添加编辑器专用的 ScoreV2 开关，并扩展 playback 速度控件（0.1x–3.0x）。
6. **ScoreV2 切换**：`EditorModSelectOverlay` 把 `EditorModdedTestPlayScoreV2` 注入 `ModType.Conversion` 列（**不注册到全局 mod 列表**）。选中后，`ModdedEditorPlayerLoader` 通过 scoped `DependencyContainer` 给本次 player 租借一个 `Bindable<RulesetInfo>`，指向 `HiddenScoreV2Rulesets` 生成的隐藏规则集；全局/编辑器的 `Ruleset` 不被修改。
7. **隐藏规则集**：`HiddenOsuRuleset` / `HiddenTaikoRuleset` / `HiddenCatchRuleset` / `HiddenManiaRuleset` 分别派生自四个内置 ruleset，只覆盖 `CreateScoreProcessor()`、`CreateScoreMultiplierCalculator()`；osu! 还额外覆盖 beatmap converter/processor 以复刻 classic slider 和 spinner bonus。
8. **Player / Results**：`ModdedEditorPlayer` 打开 `ShowResults`，把 `EditorPlayer` 的自动退出转成 results 显示；`ImportScore` 为空，分数不落库；`ModdedEditorResultsScreen` 只显示本地结果，不请求在线排行榜。

## 计分实现（ScoreV2）

- `EditorModdedTestPlayScoreV2` 只是开关，本身不计分（`IApplicableToBeatmap` 故意为空，避免 mania 改动 hit window）。它继承 `ManiaModScoreV2` 是为了让所有四个规则集的 `Player` mod 校验都能接受它。
- 四个 `Stable*ScoreV2ScoreProcessor` **直接继承 `ScoreProcessor`**，不继承模式专用 processor（模式 processor 会固定自己的 ruleset，导致自定义 multiplier 不生效；背景见 `sv2-report.md`）。
- 公式（`S` 为连击加权和，`B` 为 bonus，`completion` 为顶层物件完成比例）：
  - osu!：`round((700000*S/maxSum + 300000*acc^10*completion + B) * mult)`
  - taiko：`round((250000*S/maxSum + 750000*acc^3.6*completion + B) * mult)`
  - catch：`round(((600000 + 400000*(1-f))*S/maxSum + 400000*f*tinyProgress + B) * mult)`
  - mania：`round((200000*S/maxSum + 800000*acc^(2+2*acc)*completion) * mult)`
- 回滚一致性：processor 在 `ApplyScoreChange` 里把状态快照压入 `stateHistory`，在 `RemoveScoreChange` 里弹出并恢复。新增任何可变计分状态时，必须保证能被这套机制回滚。
- `maxSum` 来自 autoplay 模拟后的 `Reset(storeResults: true)`。osu! 通过重写 `EnumerateHitObjects` 复刻 stable 的模拟顺序（slider 30 分部件 → tick → slider 聚合）；spinner 用 `StableSpinner` 扩展 bonus tick；slider 由 `StableOsuBeatmapProcessor` 强制 `ClassicSliderBehaviour = true`。
- `PopulateScore` 用自定义公式和 multiplier 重算 `TotalScore` / `TotalScoreWithoutMods`，并写回 stable 口径的 combo。不要依赖 base 的 `MaximumCombo` 等值。
- 倍率在 `StableScoreV2MultiplierCalculator.cs` 中按模式硬编码：stable ScoreV2 下 NoFail 恒为 1.0，DT/HT/HR 等与 lazer 默认不同，catch 的 HR/HD/DT/NC 也按 1.0 处理。

## 代码地图

| 路径 | 作用 |
|---|---|
| `osu.Game.Rulesets.EditorModdedTestPlay/EditorModdedTestPlayRuleset.cs` | 假规则集入口，图标加载时触发注入 |
| `ListenerLoader/ListenerLoader.cs` | 单例引导器，向 `OsuGame` 挂处理器 |
| `ListenerLoader/Handlers/EditorModdedTestPlayRulesetIconListener.cs` | 选中本 ruleset 时切回并打开编辑器 |
| `ListenerLoader/Handlers/ScreenHandlerManager.cs` | 监听 screen 栈切换 |
| `ListenerLoader/Handlers/ScreenHandlers/*` | 替换 Editor / EditorPlayerLoader |
| `Screens.Edit/ModdedEditor.cs` | 改造版 Editor（Mod Select、播放速度） |
| `Screens.Edit/EditorModSelectOverlay.cs` | 编辑器专用 Mod Select 与 ScoreV2 开关 |
| `Screens.Edit/GameplayTest/ModdedEditorPlayerLoader.cs` | 为单次 player 租借隐藏 ScoreV2 ruleset |
| `Screens.Edit/GameplayTest/ModdedEditorPlayer.cs` | test play + results 流程 |
| `Mods/EditorModdedTestPlayScoreV2.cs` | 编辑器专用 ScoreV2 开关 |
| `Scoring/HiddenScoreV2Rulesets.cs` | 四个隐藏规则集及 processor/multiplier 绑定 |
| `Scoring/Stable*ScoreV2ScoreProcessor.cs` | 四模式 stable ScoreV2 计分 |
| `Scoring/StableOsuBeatmaps.cs` | osu! slider/spinner 的 stable 兼容处理 |
| `Scoring/StableScoreV2MultiplierCalculator.cs` | 四模式 stable ScoreV2 倍率表 |
| `osu.Game.Rulesets.EditorModdedTestPlay.Tests/` | visual test browser（几乎无回归测试） |
| `sv2-report.md`, `sv2-2-paths.md` | SV2 可行性与实现分析文档（背景资料，非运行时依赖） |

## 开发约束与陷阱

- **反射是最大脆弱点**。代码通过私有成员/方法访问 lazer 内部：`IOverlayManager.RegisterBlockingOverlay`、`PlaybackControl` 的 `tempoAdjustment` / `playbackSpeedControl` / `ClearInternal` / `AddInternal`、`Player.toggleAutoplay`、`QuickRestart`、`DependencyContainer.cache` 等。升级 `ppy.osu.Game` 后必须重新验证：打开 ruleset 选择界面 → 编辑器 → Mod Select → test play → 重试/结果页 → 四模式 ScoreV2 → 播放速度控件。
- **隐藏规则集必须保持 nested**，否则会被 `RulesetStore` 扫到并出现在规则集列表里。
- **不要**把 `EditorModdedTestPlayScoreV2` 加进任何 `GetModsFor()` 或全局 mod 列表；它只应存在于编辑器 overlay 中。
- **不要**破坏 `RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION` 这一行（构建时会写入版本，注释已说明）。
- 计分状态必须保持可回滚；涉及 `maxSum` 的模拟顺序、classic slider、spinner tick 数量时，先在 `sv2-report.md` 的结论基础上验证，不要随意改回 base 顺序。
- Test play 结果禁止导入/上传；改动 player 流程时保留 `ImportScore` 的空实现和结果页的本地模式。
- 代码风格跟随 osu! 约定与 `.editorconfig`：4 空格缩进；私有字段/方法 `camelCase`；私有 const / static readonly 使用 `lower_snake_case`；非私有常量 `UPPER_SNAKE_CASE`。不要删除源自 ppy/osu 或 LLin 的既有版权头。
- 不要提交 `bin/`、`obj/`。
- 日志统一走 `Logging.Log()`，前缀 `[EditorModdedTestPlay]`；排查时先看该前缀的日志。
- 功能完全不生效时，先确认是否进入过 ruleset 选择界面（注入由图标 BDL 触发，而不是程序集加载时）。

## 许可证

MIT，见 `LICENCE`。本项目包含/派生自 ppy/osu 与 MATRIX-feather/LLin 的代码，修改或搬运时保留对应版权声明。
