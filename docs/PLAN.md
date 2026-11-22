# 移植计划

> 参考实现：**fheroes2 1.1.18（commit `6c51acfdc`）**，源码在 `F:\code\fheroes2`
> 目标：C# / .NET 10 / SDL3 / SDL3_ttf，逻辑分辨率 640×480。
>
> 本文件是**唯一的计划**。进度在 [`PROGRESS.md`](PROGRESS.md)，决策在 [`DECISIONS.md`](DECISIONS.md)（ADR），
> 架构约定在 [`ARCHITECTURE.md`](ARCHITECTURE.md)，参考件位置在 [`REFERENCE.md`](REFERENCE.md)。
>
> 2026-11-22 合并：此前的 `planv3`/`progressv3`（AI 批次）与 `planv4`/`progressv4`（目录整块推进 + 第二阶段）
> 已全部并入本文件——完成的东西只留结论，没做完的按 §5 的 P0–P8 重排；四份旧文件已删除，原始措辞查 git
> history（删除前最后提交 `673645e`）。

## 1. 总体原则

1. **外部资源格式逐字节一致**：AGG / ICN / TIL / MP2 / H2D / SMK / `.mo` / `KB.PAL` 的解析与 C++ 完全一致，
   包括名称哈希、条目顺序、裁剪/透明规则、字节序。
2. **内部工程现代重构**：`readonly record struct` 几何、`sealed` 服务类、`Nullable=enable`、分层命名空间、
   显式依赖注入（无 DI 容器）、可单元测试。
3. **参考逻辑函数级 1:1 移植**（ADR-057）：同名函数、同顺序、同常量，允许结构差异（分层、不可变、无指针），
   **不做行级 1:1**；推理顺序与分支要能指回参考的文件名与函数名。偏离规则见 `ARCHITECTURE.md`。
4. **不提交任何原版资源**：GPLv2，仓库只含代码与可再分发字体。
5. **每个里程碑都能运行、能验证**：可执行 + 测试 + 截图/日志证据。
6. **整目录推进**（2026-11-21 起）：
   * 以参考源码目录（`F:\code\fheroes2\src\fheroes2\<dir>`）为单位，一个目录全转完才进下一个。
   * **依赖先落接口**：先写好签名 + 文档注释，方法体抛 `NotImplementedException`；**禁止在调用方写临时替代或兜底**。
   * **第一遍只求转完**（能编译、0 警告）；核对与行为测试放全部目录转完之后；**`editor/` 最后**。
7. **自建件必须记账**：任何「自己拼的」绘制/事件/控制器都进删除台账（§5.12），被参考件接管后立即删，
   不许两边并存长期运行。

## 2. 里程碑

**M0–M8、E1–E3b 全部完成**：解决方案骨架与 SDL3 640×480 + 2x（M0）；图像栈，与原版 C++ 对拍 14 项 /
1 204 张 BMP 逐字节相同（M1）；主菜单、位图字体、CJK 补位、`.mo` 本地化、BGM（M2）；MP2 头与场景列表（M3）；
冒险地图渲染（M4，已被第二阶段的 `Interface::AdventureMap` 接管重写）；回合与英雄规则（M5）；城镇规则与
城堡屏（M6）；存档序列化全链路（M8）；编辑器只读可视化、可改、保存 MP2（E1–E3b）。

**未完成**：M7 只剩 `battle_interface.cpp`（6 144 行）等战斗界面（§5.6）；M9 的战役界面与 SMK 视频未做
（§5.7、§5.9）；M2 语言选择窗口、M3 难度与标准局-战役分叉（§5.9）。

## 3. 已完成的第一阶段：S0–S16 按目录整块转换

口径见原则 6，**S0–S16 全部转完**，落点如下（偏差见 `PROGRESS.md` §3）：

`agg`/`image`/`h2d`/`audio`/`system` → `Assets/` + `Core/`；`monster` → `Game/Monsters/`；
`spell` → `Game/Spells/`；`resource` → `Game/Resources/` + `Game/Artifacts/`；`army` → `Game/Army/`；
`heroes` → `Game/Heroes/`；`castle` → `Game/Castles/`；`kingdom` → `Game/Kingdom/`；`maps` → `Game/Maps/`；
`world` → `Game/World/`；`battle` → `Game/Battle/`（非界面）；`ai` → `Game/Ai/`（剩 §4）；
`campaign` → `Game/Campaign/`；`gui` → `UI/` + `Game/Gui/`、`dialog` → `UI/Dialogs/`、
`game` → `App/` + `UI/Interface/`（这三处第一遍，第二阶段按 1:1 重做，§5）；
`editor` → `Game/Editor/` + `App/Scenes/Editor*`（模型层按参考，窗口未按参考，§5.11）。

## 4. AI：只剩两个入口

契约先行（ADR-161、ADR-180、`AiContractTests`）与实现都已落：**B0–B6、B8、X1–X3 全部完成**——
`ai_common`/`ai_planner`/`ai_planner_castle`/`ai_planner_hero` 的函数、`ai_battle` 的 25 个决策与 15 个法术估值、
`KingdomTurn` 的整条回合循环（含 `ScanMap`）、战斗的回合循环/近战/施法/塔/投石车/`BattleLoader`、
`AIWorldPathfinder` 六方法、`ObjectValueStorage`、`battle_arena` 的十个法术查询。

**剩余**：`ai_hero_action.cpp` 的 `HeroesAction` / `HeroesMove`（B7 的 2/4；次元门与召船已落），
它卡在 `heroes_action` 的对象交互分派与 `Interface::AdventureMap::Get()` 的解耦上（§5.4 的 P1）。
`KingdomTurn` 已经在调 `HeroesTurn`，而 `HeroesTurn` 调 `HeroesMove` 就会抛——这是 AI 唯一的阻塞点，
也是仓库里 AI 仅剩的两处 `NotImplementedException`。原则：一个占位是一个原子任务，禁止静默兜底。

## 5. 剩余工作总计划

### 5.1 口径

* **1:1 的判据**：参考的每个非内联函数在端口都有同名或同义成员，函数体逐段对应——循环边界、魔数、
  绘制顺序、早退条件一致；文档注释写清参考文件与行号。
* **三类差距**：①部分（有文件，缺函数或分支）②未移植（用自建件顶替）③纯缺失。
* **删除顺序**：写参考移植 → 调用方切换 → 测试迁移 → 删自建文件。任何一步没做完就不删。
* **不做的事**：不为了对齐而重命名已 1:1 的文件；`interface_*` 一个类拆多文件可以保留，
  但类与函数必须与参考对得上。

### 5.2 阶段与次序

按依赖排：先收桩让主线可跑，再补冒险层与 AI，然后是最重的战斗界面，剩下四块彼此独立。

| 阶段 | 内容 | 前置 | 规模 | 收口标志 |
| --- | --- | --- | --- | --- |
| **P0** ✅ | 收桩：G6.9b 第 5–6 步 + J（§5.3） | — | 小 | 冒烟/git 里没有自建绘制，全量测试绿 |
| **P1** | 冒险层闭环：对象交互 + 冒险施法 + Interface 解耦（§5.4） | P0 | 中 | 拾取/访问/施法都能走参考路径 |
| **P2** | AI 收口：`B7` 的 `HeroesAction`/`HeroesMove`（§4） | P1 | 中 | AI 回合能整局跑完 |
| **P3** | 战斗界面四篇（§5.6） | P0、P2 | **最大**（约 6 800 行） | 手动打完整场战斗 |
| **P4** | 战役：联盟表、存档、主流程、界面（§5.7） | P2、P3 | 中 | 战役能开局到结局 |
| **P5** | 高分屏与结局屏（§5.8） | P4 | 小 | 胜负后能进榜 |
| **P6** | 启动缺口：语言选择、难度分叉、SMK、淡入（§5.9） | P0 | 小 | 启动流程与参考一致 |
| **P7** | 随机地图生成器（§5.10） | P6 | 大 | 能生成并开局随机图 |
| **P8** | 编辑器窗口（§5.11） | 其余 | 大 | 编辑器窗口与参考同构 |

P0 → P1 → P2 是一条链；P3 与 P4 可以在 P2 之后并行开工；P6 随时可做（只碰启动与主菜单）。

### 5.3 P0：收桩（G6.9b + J）

`HumanTurn()` 的循环体（G6.9）已整条齐了，剩下的是把**场景**完全交给它：

| 步 | 内容 | 状态 |
| --- | --- | --- |
| 1 | 声卡可以是空的（`AdventureMap` 收 `AudioManager?`） | ✅ |
| 2 | `DrawFrame` → `RedrawInterface` | ✅ |
| 3 | 滚动源 `AdventureMapView` → `GameArea` | ✅（`MapScrollController` 因编辑器仍在用而保留） |
| 4 | `Update` → `ProcessTurnFrame` + `FinishHumanTurn` | ✅ 拖拽/边缘卷屏、英雄动画、热键、按钮与状态石点击随之回来 |
| 5 | 输入三路（光标/左键/右键）→ `MouseCursorArea*` | ✅ 全走 `LocalEvent`，场景三方法已删 |
| 6 | 删六份自建绘制（`MapFrame`/`IconBar`/`Minimap`/`ButtonsPanel`/`StatusPanelPainter` + 场景面板段） | ✅ `StatusPanelPainter` + 场景状态行删除；雷达/按钮面板/图标条随第 4 步删；`Game/AdventureMap` 四件留到 P8 给编辑器 |

第 5 步的落点：`UI/Interface/AdventureMap.Mouse.cs` 里已有 `game_startgame.cpp:1615-1736` 的三个鼠标处理，
场景的三路输入改调它，光标主题由 `AdventureMap.Cursor.cs` 的 `UpdateCursor` 给出；`AdventureMapView` 的事件段
随之删除。第 6 步删 `StatusPanelPainter.cs` 类型、`MapFrame` 的美术绘制与诊断状态行。

**收桩之后立刻要还的账**（用户同意放到最后统一修）：

1. 视野中心与英雄差约 1.5 格——冒烟漂移已排除（`--cursor` 第 5 步起进 `LocalEvent`），
   剩下的对拍 `SetCenter(GetCenterOf(tile))` 与瓦片绘制原点；
2. ~~雷达全黑~~ 已修：`Radar._hide` 默认值错（`true` → `false`），另把雾色改 `FriendColors()`；
3. 地图上的零星条带；
4. 迷雾在端口里存了两份（`MapFog` 与 `Tile._fogColors`），`World.SyncTilesFog` 只是治标，合并成参考的单份；
5. 面板拖动只画轮廓、松手不落位（`AdventurePanelResult` 补一个 `Commit` 字段）；
6. 基线改为「与参考一致」，按参考渲染重新生成并在 `tests/baselines/README.md` 记明差异；
7. `FinishHumanTurn` 的 `Win`/`Lose`/`HighScore`/战役四支现在只记一行日志，由 P5 与 P4 接管。

**J** ✅：`StatusPanelPainterTests`（16 条）随类型一起删，行为由 `StatusPanelTests` 覆盖；其余用例只依赖
仍保留的 `Game/AdventureMap` 几何。三件验证：build 0 警告 / `dotnet test` 5 481 全绿 / `--enter-map
--smoke-frames 60` 退出 0。

### 5.4 P1：冒险层闭环

参考 `heroes/heroes_action.cpp`（2 504 行）+ `heroes_spell.cpp` + `maps/maps_tiles_helper.cpp` 的分派段：

* `HeroActions.cs`（2 164/2 504）缺的拾取、访问、交互分支补齐；
* `ActionToHeroes` 级别的对象分派（现在 0 引用），这是地图物件「谁去处理」的总入口；
* `Hero.ActionSpellCast()`（`heroes_spell.cpp:305-436`）——观地、观矿、次元门、召船、城镇门户的
  目的地选取与播放动画；现在直接抛，是 P2 与 AI 施法的共同前置；
* 解耦：参考 `ai_hero_action.cpp` 的 34 处 `Interface::AdventureMap::Get()`（居中、`setRedraw`、雾方向）
  改成事件或回调，**先定接口再写分支**（这是唯一带架构性的改动）。

**已完成**：

| 项 | 落点 | 状态 |
| --- | --- | --- |
| 怪物/城堡/英雄三类遭遇（含战斗入口、占领、失败结算） | `Heroes/HeroActions.Battle.cs`、`HeroActions.Action.cs` 分派 | ✅ |
| 英雄遣散（战败 / 投降） | `Heroes/Hero.Dismiss.cs` | ✅ |
| 施法分派与纯状态支（观地、鉴定、召船、次元门、城镇之门/门户、视野、守卫） | `Heroes/Hero.Spells.cs`、`Heroes/IHeroSpellCastHost.cs` | ✅ |
| 界面侧施法宿主（观地窗口 + 视野文案） | `UI/Interface/AdventureMap.SpellCast.cs` | ✅ |
| 可取对象：矿/锯木厂/炼金实验室/灯塔、废弃金矿、沉船·坟场·弃船 | `Heroes/HeroActions.Capture.cs` | ✅ |
| 宝藏点：金字塔、恶魔洞窟 | `Heroes/HeroActions.Treasure.cs` | ✅ |
| 怪物居所二十一座（白送 / 花钱买 / 先打服）+ 共用的买卖 | `Heroes/HeroActions.Dwelling.cs` | ✅ |
| 设施类：精灵灯、贸易站、属性四座、炼金术士塔、法师小屋、法师之眼、神谕 | `Heroes/HeroActions.Facility.cs` | ✅ |
| 狮身人面像、事件瓦片（含 `MapEvent`/`MapSphinx` 两个模型补全） | `Heroes/HeroActions.Landmark.cs` | ✅ |
| 上下船与海岸、军队升级对象（山丘堡垒/铸造厂/马厩） | `Heroes/HeroActions.Boat.cs`、`Heroes/HeroActions.ArmyUpgrade.cs` | ✅ |

**P1 残留**：漂流瓶（`OBJ_BOTTLE`，要瓦片的 `objectInfo` 留言，数据层还没搬）；次元门目的地框与城镇门户城列表两个
交互模式（`SpellCastHost` 现答「取消」，不扣法力）；招募窗的地图版（`AskRecruitCount`）、谜语输入框
（`AskRiddle`）、市场与盗贼公会两份窗口（`OpenMarketplace`/`OpenThievesGuild`）都没有实现方，见 `PROGRESS.md`
§7.4；`Visions` 的怪物画像元素、`HeroModes.Visions`（参考里没有读者，端口不建）。

### 5.5 P2：AI 收口

只做 `AiHeroAction.HeroesAction` / `HeroesMove` 两个入口（§4），按 `ai_hero_action.cpp` 的 2 504 行分两半：
`HeroesAction` 的对象分支（拾取/访问/招降/守卫，走 P1 的 `ActionToHeroes`）与 `HeroesMove` 的移动循环
（路径 → 逐步走 → 到达后动作 → 目标失效则重规划）。完成后 `KingdomTurn` → `HeroesTurn` 这条链第一次能跑通。

### 5.6 P3：战斗界面（最重的一块）

参考 `battle/` 目录里界面四篇，端口目前一个文件都没有：

| 参考 | 行数量级 | 端口落点 | 内容 |
| --- | --- | --- | --- |
| `battle_interface.cpp` | 6 144 | `Battle/BattleInterface*.cs`（新建） | 战场绘制、单位动画、行动交互、布阵、自动战斗、胜负结算 |
| `battle_interface_settings.cpp` | 小 | `Battle/BattleInterfaceSettings.cs` | 战斗设置窗（速度、网格、咒语提示） |
| `battle_dialogs.cpp` | 中 | `Battle/BattleDialogs*.cs` | 单位信息、军队顺序、投降/撤退确认 |
| `battle_only.cpp` | 小 | `Battle/BattleOnly*.cs` | 快速战斗的无人值守循环 |

依赖都已就位：`BattleArena` 的回合/近战/施法/塔/投石车、`BattleLoader`、`LocalEvent` + `MessageBox.Show`
（G6.1–G6.3）、`ArmyBar`/`ItemsBar`/`SkillBars`。建议切五步，每步能单独截图：
① 战场与边框绘制（含雾/掩体/桥）② 单位精灵与动画（`RandomMonsterAnimation` 已有）
③ 行动交互（移动/攻击/施法/等待/防御）④ 布阵与自动战斗（`QuickCombat`/`ToggleAutoCombat` 命令已通）
⑤ 胜负结算、经验与升级窗口（`LevelUpDialog` 已有）。

### 5.7 P4：战役

模型层已在（`Game/Campaign/` 七个文件），缺的是接线与两处正文：

* `CampaignData.ApplyScenarioAlliances` 现在抛——`Mp2FileInfo.PlayerUnions` 已经存在，直接合并即可（小）；
* 战役存档字段（`CampaignData` 的序列化缺口 + `campaign_savedata.cpp` 的对应段）；
* `game_campaign.cpp` 的主流程：开局选择战役 → 选关 → 奖励/场景奖励 → 进入地图 → 回战役层；
  `CampaignPopupWindows`（`ui_campaign.cpp`）已转完，可直接用；
* 结局分支：`FinishHumanTurn` 的战役那一支（P0 账 7）。

### 5.8 P5：高分屏与结局屏

`highscores.cpp` + `game_highscores.cpp` → `Game/Highscores/`（现在只有 `HighscoreData.cs` 与
`HighScoreDataContainer.cs`，界面全无）+ App 场景。`GameOverResult`（G6.7）已能给条件与文案，
这里补「进榜 → 展示 → 回主菜单」的流程，并接管 `Win`/`Lose`/`HighScore` 三支。

### 5.9 P6：启动缺口

* **语言选择窗口**（M2）：`LanguageSelectionDialog` 已存在但无人调用，接进 `GameHost` 的启动流程
  （参考 `dialog_language_selection.cpp` / `game_init.cpp` 的语言选择），并让 `--lang` 与它共用同一份校验；
* **难度与标准局-战役分叉**（M3）：`game_scenarioinfo.cpp` 的难度行与「标准游戏 / 战役」两个入口，
  难度模型 `Difficulty.cs` 已在；
* **SMK 视频**（M9）：解码器 + 播放（`game_video.cpp` 的对应段），现在只在 `GameOverResult` 里提了一句；
* **`BaseInterface` 的两个淡入方法**：等 S14 的 `Game::validateDisplayFadeIn` 状态落地后再实现，
  现在抛（`BaseInterface.cs:142/149`）。

### 5.10 P7：随机地图生成器

`maps/map_random_generator.cpp` + `_info` + `_helper`（`S8-6`，端口 0 引用）→ `Game/Maps/MapRandomGenerator*.cs`，
连同 `dialog_random_map_generator.cpp` 的对话框（`UI/Dialogs/` 里还没有）。这是一整块独立功能：
地形与物件生成 → 平衡性调整 → 存成 MP2 → 进开局流程。

### 5.11 P8：编辑器（最后）

模型层与主菜单已在（`Game/Editor/` 八个文件、`EditorMainMenuScene`、`EditorScene`、`EditorPanel`），
**窗口全缺**：参考 `editor/` 的 18 个 cpp 里，以下五组要新建到 `Game/Editor/` + `App/`：

| 组 | 参考 | 端口落点 |
| --- | --- | --- |
| 主界面 | `editor_interface.cpp`、`editor_interface_panel.cpp`、`editor_ui_helper.cpp`（`EditorUiHelper` 已有） | `EditorInterface*.cs` |
| 历史与选项 | `history_manager.cpp`、`editor_options.cpp` | `HistoryManager` 补 per-object 元数据表（现在抛） |
| 城堡与地图 | `editor_castle_details_window.cpp`、`editor_map_specs_window.cpp`、`editor_object_popup_window.cpp` | 三个窗口 |
| 事件类 | `editor_daily_event_spec_window.cpp`、`editor_daily_events_window.cpp`、`editor_event_details_window.cpp`、`editor_rumor_window.cpp`、`editor_sphinx_window.cpp` | 五个窗口 |
| 菜单与选择 | `editor_mainmenu.cpp`、`editor_language_window.cpp`、`editor_save_map_window.cpp`、`editor_secondary_skill_selection.cpp`、`editor_spell_selection.cpp` | 主菜单 + 四个窗口 |

编辑器还要把 `MapScrollController`、`EditorScene` 的输入换成 `LocalEvent` 那套（与 P0 同步，届时才能删
`MapScrollController`）。

### 5.12 自建删除台账

已删：`Dialogs/FrameBoxChrome.cs`、`Dialogs/StandardWindowChrome.cs`（E-2）、`Widgets/ItemBarLayout.cs`（G2）、
冒险场景的雷达/按钮面板/状态石三段（P0 第 4 步）、`Interface/StatusPanelPainter.cs` 与场景的诊断状态行
（P0 第 6 步）、`AdventureMapView` 的事件段（P0 第 5 步）。在账：

| 端口文件 | 现在的样子 | 由谁接管 | 状态 |
| --- | --- | --- | --- |
| `Game/AdventureMap/MapScrollController.cs` | 自建拖动/边带/滚轮 | `GameArea::QueueEventProcessing` + `ScrollGameArea` | 冒险场景已不用；编辑器仍用，随 P8 删 |
| `Game/AdventureMap/{MapFrame,IconBar,Minimap,ButtonsPanel}.cs` | 自建绘制/几何 | `interface_*.cpp` | 冒险场景只借 `MapFrame.Viewport` 几何；编辑器仍用，随 P8 删 |
| `Scenes/MageGuildWindow.cs` | 自建窗口 | `ui_mage_guild.cpp` | 待删（核对后） |

`Widgets/DialogButtonArtwork.cs`、`Widgets/ButtonArtwork.cs` 复核后**保留**（只是取图缝与 1:1 移植，
不含画法）。`Dialogs/` 下「未指名参考」的对话框（`ArenaDialog`、`TavernDialog`、`WellDialog` 等）对应
`dialog/` 目录，多数已按参考落地，逐个复核即可，不在本表内。

## 6. 依赖方向

`Core` ← `Assets` ← `Rendering` ← `Game` ← `UI` ← `App`。反向引用一律不许（`Game` 不能引 `UI`，
所以 `MapScrollController` 这类共享件要么留在 `Game`、要么整体上移，不许两边各一份）。

## 7. 记账规则

* **每完成一个可验证单元**：`PROGRESS.md` 加一行/一节（做了什么、与参考的偏差、留下的异常、验证数字）。
* **每个跨文件的取舍**：`DECISIONS.md` 一条 ADR，编号接续。
* **计划变更**：改本文件，并在 `PROGRESS.md` 记一句「为什么改」。
* **验证三件套**：`dotnet build` 0 警告、`dotnet test` 全绿、相关冒烟（`--smoke-frames`）退出 0；
  像素相关的还要跑对应的 `tools/scripts/Verify*Baseline.ps1`。
* **压缩**：已完成的历史只留结论、偏差、遗留与数字，删过程叙述。
