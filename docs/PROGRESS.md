# 进度记录

> 唯一账本。计划见 [`PLAN.md`](PLAN.md)，决策见 [`DECISIONS.md`](DECISIONS.md)，架构约定见 [`ARCHITECTURE.md`](ARCHITECTURE.md)。
> **规则：只追加结论，不改已写下的结论**；状态表随每步更新，历史条目不重写。
>
> 2026-11-22 两次压缩：先合并四代账本（第一代逐日流水、AI 批次账、目录推进账），再把已完成的里程碑与批次
> 压成「结论 + 偏差 + 遗留 + 数字」，过程叙述与中间截图描述删除；随后把旧计划（`planv3`/`planv4`）与旧账本
> 四份删掉，剩余工作改由 `PLAN.md` §5 的 P0–P8 承载。原始措辞查 git history，压缩前最后提交 `673645e`。

## 1. 现状（2026-11-22）

| 项 | 数字 |
| --- | --- |
| 构建 `dotnet build FHeroes2Sharp.slnx` | 0 错误 / 0 警告 |
| 测试 `dotnet test` | **5 497 绿**（Core 284 / Assets 425 / Game 3 266 / UI 1 522） |
| 冒烟 `--enter-map --smoke-frames 30 --no-audio` | 59–72 FPS（机器波动），退出 0 |
| `throw new NotImplementedException` | **8 处**（清单见 §7.2） |
| 冒险地图基线 `VerifyAdventureMapBaseline.ps1` | 140 015 / 307 200 像素不同——**已改为「与参考一致」的目标，不再当回归** |
| 当前工序 | **G6.9b 收尾**：第 1–4 步完成，第 5 步（输入三路 → `MouseCursorArea*`）未开始 |
| 参考实现 | fheroes2 1.1.18（`6c51acfdc`），源码 `F:\code\fheroes2` |

## 2. 里程碑（M0–M8、E1–E3b 全绿）

全部里程碑均已完成，结论与偏差记在这里、细节不再展开：

* **M1 图像栈**：与原版 C++ 对拍 14 项 / 1 204 张 BMP 逐字节相同——这是整个移植的验证基准。
* **M2 主菜单 → M4 冒险地图 → M6 城堡屏 → M8 存档**：界面部分后来被第二阶段的参考移植件重做，
  M4 的自建渲染已不在了（见 §6）；M8 的序列化是 G6.5/G6.6 接的。
* **M7 战斗**：规则层收口，`battle_interface.cpp`（6 144 行）等界面归第二阶段。
* **M9**：战役数据层已落、界面未接；SMK 视频、NativeAOT 评估未开始。
* **E1–E3b**：只读可视化、地形笔刷/接缝/撤销、保存 MP2（原地补丁 + 全量写出）都能被参考打开。

## 3. 第一阶段 S0–S16（2026-11-21，全绿）

整目录口径见 `PLAN.md` 原则 6，17 个批次全部完成，落点见 `PLAN.md` §3。留下的偏差只有四处：

* `maps`：随机地图生成器（旁支）推迟。
* `ai`：只剩 `HeroesAction`/`HeroesMove` 两个入口（B7/X4），见 §4。
* `gui`/`dialog`/`game`：只做完第一遍，第二阶段按 1:1 重做（§5）。
* `editor`：模型层按参考，**窗口未按参考**；`HistoryManager` 一处抛（§7.2）。

## 4. `ai` 目录（B / X 批次）

B0–B6、B8、X1–X3 全部完成（与 `PLAN.md` §4 同一份账）。2026-11-22 复核时改正了三处过时记录：

* **B4 已是 11/11**：`KingdomTurn` 的回合循环已随 `AiPlanner.Hero.cs` 落地，不再抛。
* **B8 全落**：`BattleArena.Action` 的 `ApplyAction` 十一个分支齐（含 `Spellcast`/`Tower`/`Catapult`），
  `BattleLoader` 三处正文也补完了；唯一残留是 default 分支里一句过时的异常文案。
* **X3 全落**：`battle_arena` 的十个查询（`GetTargetsForSpell`、`CanRetreatOpponent`、`IsSpellcastDisabled`、
  `IsAbleToResurrectFromGraveyard` 等）都在。

**只剩 B7 + X4**：`AiHeroAction.HeroesAction` / `HeroesMove` 仍抛，而 `KingdomTurn` → `HeroesTurn` → `HeroesMove`
这条链一跑就到它们，所以这是 AI 唯一的阻塞点；仓库里 AI 目录也只剩这两处 `NotImplementedException`。

## 5. 第二阶段：1:1 还原（2026-11-21 起）

分组 A–F、G1–G5、H、I 全部完成（清单见 `PLAN.md` §5.2）。其中值得记的结论：

* **E-2 删掉两套自建窗框**（`FrameBoxChrome`/`StandardWindowChrome`），`Dialog::FrameBox` 与 `StandardWindow` 接管。
* **G2 删掉自建 `ItemBarLayout`**，`ItemsBar` 接管。
* **G3 统一坐标**：`maps_tiles_render.cpp`（ADR-197）与 `GameArea` 接管绘制，为 §6 的换桩铺路。
* **G6 是最大的一段**：面板/雷达/按钮回路（G6.1–G6.3）→ 场景信息与存档选择（G6.4，`IAdventureHost` 收到
  13 项，补 71 条测试）→ 存档序列化全链路（G6.5/G6.6）→ 冒险地图对话框群（G6.8：存档游戏、快速信息、
  胜负判定、拼图地图、魔法书、文件菜单、英雄窗口、王国总览，补 24 条测试）→ `HumanTurn()` 循环体（G6.9）。
  **G6.9b（场景换桩）是当前工序**，见 §6。

## 6. 当前工序：G6.9b（场景换桩）

### 6.1 六步

| 步 | 内容 | 状态 |
| --- | --- | --- |
| 1 | `AdventureMap` 收 `AudioManager?`，null 即 `Audio::isValid()==false` | ✅ |
| 2 | 场景 `DrawFrame` → `Interface::AdventureMap.RedrawInterface` | ✅ |
| 3 | 滚动源 `AdventureMapView` → `GameArea` | ✅ |
| 4 | `Update` → `ProcessTurnFrame` + `FinishHumanTurn` | ✅ |
| 5 | 输入三路（光标/左键/右键）→ `MouseCursorArea*` | ⬜ **下一步** |
| 6 | 删六份自建绘制 | ⚠️ 三份已删，余三份待第 5 步后收 |

第 3 步把滚动交出去（`ScrollBy`→`ShiftCenter`、`CentreOnHero`/雷达拖动→`SetCenter(tile)`、滚轮→
`SetCenterInPixels`，位移与惯性归 `ScrollGameArea`），场景的 `_mouse` 换成 `Point? _cursor`；
`MapScrollController` 类保留给 `EditorScene`。效果是**英雄终于画在地图上了**——之前绘制与滚动各推一套坐标。

### 6.1b 第 4 步（2026-11-22）

场景 `Update` 只剩三件事：`ProcessTurnFrame`（收键、派面板、动画、卷屏）→ `PumpDrag`（面板拖动，只有隐藏
界面模式有）→ `FinishHumanTurn`（结论；`Cancel` 即换场景）→ `FollowInterfaceFocus`（界面焦点回写会话）。

一次接回来的东西：**拖拽与边缘卷屏**（`GameArea.QueueEventProcessing`、`ProcessScrollMargins`）、
**英雄走动动画**（`AnimateHeroMovement`，32 帧）、**全部热键**（`ProcessHotKeys`）、**按钮面板与状态石点击**、
**雷达点击/拖动**；对应的场景自建件同批删除（`UpdateScroll`、`ClickPanel`、`CurrentButtonStatus`、
`_statusState`、`_minimap`、`CentreOnRadar`，以及 `GameHost.HandleAdventureKey` 的热键 switch）。

两条新接口与修正：

* `AdventureMapScene.ExitEvent`（← `GameHost.LeaveAdventureMap()`）。参考把 `Game::processExitEvent()` 递给
  `HumanTurn()`；端口里「回主菜单」要换场景，场景换不了自己，所以由宿主接这一手（回主菜单、返回 `Cancel`）。
  Escape、菜单退出、`NewGame`/`LoadGame` 三个值都走它。
* 瓦片换算改走 `GameArea.GetValidTileIdFromPoint()`。场景原来自己减视口原点，而 `_view.Scroll` 自第 3 步起不再
  更新，点城与右键小贴士会偏半格。

### 6.2 已知回退与缺陷（用户同意留到最后统一修）

1. **面板拖动只画轮廓、松手不落位**。参考在松手时调 `BorderWindow::SetPosition`，而端口的
   `AdventurePanelResult` 只带 `BorderDrag` 不带「是哪个面板」。只有隐藏界面模式抓得到面板，正常布局
   永不触发；给结果补一处 `Commit` 即可。
2. **热键能返回、但值守不全**。`FinishHumanTurn` 可能回 `Win`/`Lose`/`HighScore`/战役那几支，端口的
   `HandleResult` 只接了 `EndTurn` 与「换局」三个值，其余记一行日志后忽略（§7.4）。
3. 视野中心与英雄差约 1.5 格（横向更多）。**先排除干扰项**：冒烟模式无真实光标，
   `LocalEvent.Current.CursorPosition` 是 `(0,0)`、正好落在左边带，于是每帧往左上卷，
   截图里的「不居中」有一部分是这个漂移，不是 `SetCenter(GetCenterOf(tile))` 的错。
4. 雷达全黑（`Radar.Build` 之后仍空）。
5. 地图上的零星条带/方块。
6. 迷雾存了两份（`MapFog` 与 `Tile._fogColors`），`World.SyncTilesFog` 只是治标，应合并成参考的单份。
7. 基线改为「与参考一致」后需重新生成，差异记进 `tests/baselines/README.md`。第 4 步前后差异只从
   139 753 变到 140 015 像素，说明这轮换桩没动到画面主体，仍是那批老缺陷。

## 7. 未完成清单（唯一列表）

### 7.1 主线

次序按 `PLAN.md` §5.2 的 P0–P8：**P0** 收桩（G6.9b 第 5–6 步 + J，§6.1）→ **P1** 冒险层闭环（对象交互、冒险施法、
Interface 解耦）→ **P2** AI 收口（B7 两个入口）→ **P3** 战斗界面四篇 → **P4** 战役 → **P5** 高分屏与结局 →
**P6** 启动缺口 → **P7** 随机地图生成器 → **P8** 编辑器。P3/P4 在 P2 之后可并行，P6 随时可插。

### 7.2 仍抛 `NotImplementedException` 的 8 处

| 位置 | 缺什么 | 依赖 |
| --- | --- | --- |
| `Game/Ai/AiHeroAction.cs:29` | `HeroesAction` | B7 / X4 |
| `Game/Ai/AiHeroAction.cs:37` | `HeroesMove` | B7 / X4 |
| `Game/Artifacts/UltimateArtifact.cs:165` | `GetPuzzleMapSurface()` | `GameArea.GenerateUltimateArtifactAreaSurface` 已在，直接接 |
| `Game/Campaign/CampaignData.cs:212` | 战役联盟表合并（`Mp2FileInfo.PlayerUnions` 已存在，可直接写） | P4 |
| `Game/Editor/HistoryManager.cs:409` | 端口地图格式缺 per-object 元数据表 | 编辑器（S16 余支） |
| `Game/Heroes/Hero.Spells.cs:289` | `ActionSpellCast()` | 冒险地图目的地选取与动画（第二阶段） |
| `UI/Interface/BaseInterface.cs:142` | `renderWithFadeInOrPlanRender` | Game 淡入状态（S14） |
| `UI/Interface/BaseInterface.cs:149` | `validateFadeInAndRender` | 同上 |

### 7.3 AI 剩余

只有 B7 + X4（§4）：`ai_hero_action.cpp` 的 `HeroesAction` / `HeroesMove` 两个入口，以及它们依赖的对象交互
分派与 `Interface::AdventureMap::Get()` 解耦。其余批次全部完成。

### 7.4 界面缺口（非主线，按里程碑挂账）

语言选择窗口（M2）、难度与标准局-战役分叉（M3）、战役界面接线（M9）、SMK 视频（M9）、
随机地图生成器（S8 旁支）、编辑器窗口按参考重写（S16）；`FinishHumanTurn` 的 `Win`/`Lose`（胜负屏）、
`HighScore`、战役分支还没有处理器，现在只记一行日志。

### 7.5 账目类

`DECISIONS.md` 691 KB 未压缩（ADR 条目还在快速增长）；`REFERENCE.md` 64 KB 的参考件位置表与本文件
同源，两者谁先失真以 `REFERENCE.md` 为准。

## 8. 保留的坑与教训

1. **依赖先落接口**：S12 转 `interface_gamearea.cpp` 时踩到尚未转的 `maps_tiles_render.cpp`，临时替代后来全要拆。
2. **两套坐标必然打架**：自建 `AdventureMapView` 与 `GameArea` 并存期间滚动与绘制各推一套，表现是
   「滚了但画没动」「英雄不在视野」。参考里只有一个位置的量（视口中心、焦点），端口也只能有一个。
3. **自建绘制会静默失真**：`StatusPanelPainter` 少画当前玩家的英雄，就是「自己拼」的典型症状。
4. **`.h` 里的内联函数也是移植对象**：G1–G5 里几次漏项都出在头文件的 inline 方法上。
5. **冒烟模式没有光标**：依赖鼠标位置的逻辑在 `--smoke-frames` 下都看到 `(0,0)`，排查画面问题前先排除它。
6. **基线漂移要先定性再定量**：139 753 这个数字长期被当回归，实际是「基线来自自建渲染」，定性错误修错方向。
7. **换桩时同一入口的两条路要一起掐**：第 4 步接上 `ProcessPanels` 后，场景的 `ClickPanel` 与屏幕的
   `ButtonsPanel` 会同时收到同一次点击，按一次「结束回合」结束两个回合。输入归属转移时，旧那一路必须在
   同一提交里关掉。

## 9. 记账规则

* 每完成一个可验证单元：本文件追加一节（做了什么、与参考的偏差、留下的异常、验证数字）。
* 跨文件取舍：`DECISIONS.md` 一条 ADR，编号接续。
* 计划次序变更：改 `PLAN.md`，并在本文件记一句「为什么改」。
* 验证三件套：`dotnet build` 0 警告、`dotnet test` 全绿、`--smoke-frames` 退出 0；
  像素相关的再跑 `tools/scripts/Verify*Baseline.ps1`。
* 压缩本文件时：保留结论、偏差、遗留与数字，删过程；被删内容的出处写在这里（commit 号）。
