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
| 测试 `dotnet test` | **5 481 绿**（Core 284 / Assets 425 / Game 3 266 / UI 1 506） |
| 冒烟 `--enter-map --smoke-frames 60 --no-audio` | 72.6 FPS，退出 0 |
| `throw new NotImplementedException` | **8 处**（清单见 §7.2） |
| 冒险地图基线 `VerifyAdventureMapBaseline.ps1` | 139 779 / 307 200 像素不同——**已改为「与参考一致」的目标，不再当回归** |
| 当前工序 | **P0 完成**（G6.9b 第 1–6 步 + J 全绿，§6.1）；下一步 **P1 冒险层闭环** |
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
  **G6.9b（场景换桩）已收口**（P0），见 §6。

## 6. 当前工序：G6.9b（场景换桩）

### 6.1 六步

| 步 | 内容 | 状态 |
| --- | --- | --- |
| 1 | `AdventureMap` 收 `AudioManager?`，null 即 `Audio::isValid()==false` | ✅ |
| 2 | 场景 `DrawFrame` → `Interface::AdventureMap.RedrawInterface` | ✅ |
| 3 | 滚动源 `AdventureMapView` → `GameArea` | ✅ |
| 4 | `Update` → `ProcessTurnFrame` + `FinishHumanTurn` | ✅ |
| 5 | 输入三路（光标/左键/右键）→ `MouseCursorArea*` | ✅（§6.1c） |
| 6 | 删六份自建绘制 | ✅（§6.1c） |

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

### 6.1c 第 5–6 步与 J（2026-11-22，P0 收口）

输入全部交给 `LocalEvent`：`GameHost` 不再调 `AdventureMapScene.SetCursor` / `SetLeftButton` /
`SetRightButton`（三方法已删），指针在事件批次之后用 `_le.OnMouseMotion` 推进，连同左右键一起由
`ProcessTurnFrame` 派给 `MouseCursorArea*`（`interface_events.cpp`）。附带结果是 `--cursor` 终于能进
冒烟画面（之前合成光标只写场景字段，屏幕读不到，冒烟里指针恒为 `(0,0)`）。

两处「参考是阻塞循环、端口不能阻塞」的交接，都在这一批补上，都是显式委托，不是兜底：

* **速览**：`AdventureMapDialogs.PresentQuickInfo` 把 `Dialog::QuickInfo()` 建好的面板交回场景，场景
  `UpdateQuickInfo` 在松手那一帧收掉；`--quick-info` 改走 `ShowQuickInfoAt`。
* **城堡屏**：`AdventureMap.OpenCastleScreen` 委托 → 场景 `RequestCastleScreen` 置 `_castleRequest`，
  宿主换 `CastleScene`；没人接管时退回 `_dialogs.OpenCastleDialog`。五处调用点统一走 `OpenCastleScreenOf`。

删件：`UI/Interface/StatusPanelPainter.cs`（396 行）、场景的诊断状态行
（`StatusBarHeight`/`StatusBarArea`/`DrawStatusBar`/`_statusBar`/`_dark`）、`StatusPanelPainterTests.cs`
（16 条）。后者的行为由 `StatusPanelTests` 覆盖，不是净损失（5 497 → 5 481）。`MapFrame`/`IconBar`/
`Minimap`/`ButtonsPanel`（`Game/AdventureMap/`）只剩 `EditorScene` 在用，随 P8 删。

顺手修好的两个真 bug：

* 雷达 `_hide` 默认 `true`（`Radar.cs:124`）→ `false`：参考默认未折叠，之前每帧只画 logo 与黑底，
  就是 §6.2 第 4 条「雷达全黑」的成因；
* 雷达迷雾色 `Players.HumanColors()` → `FriendColors()`，对齐 `interface_radar.cpp:243`。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿；`--enter-map --smoke-frames 60` 退出 0（72.6 FPS）；
`--quick-info` 打出「the quick info … is open」；基线 139 779（换桩前后同量级，画面主体未动）。

### 6.1d P1 第 1 批：三类遭遇 + 冒险施法（2026-11-22）

P1 的头两件事一次做完：`heroes_action.cpp` 的三类遭遇与 `heroes_spell.cpp` 的施法分派。

三类遭遇（`Heroes/HeroActions.Battle.cs`，分派在 `HeroActions.Action.cs`）：

* **怪物**（`ActionToMonster`）——占位判定、`Army::GetJoinSolution` 的四条路（免费加入 / 收费加入 / 逃跑 /
  开战）、战败把残兵写回瓦片并把 `Free` 改 `Money`、全灭才 `RemoveObjects` + `ResetObjectMetadata`。
* **城堡**（`ActionToCastle`）——无主城四步：旧主 `RemoveCastle` → 新主 `AddCastle` → `World.CaptureObject` →
  `Castle.Scout`；有主城按参考走 `ActionPreBattle`/`ActionAfterBattle`，顺序不可换。
* **英雄**（`ActionToHeroes`）——会面判负、城内敌方英雄转交攻城、胜方夺宝与残兵。

「必须当场拿答案」的两处收进 `IHeroActionHost`（问「加不加入」「追不追」），缺省 `NullHeroActionHost`
全答否；人类、AI、测试共用同一段逻辑。附带 `Hero.Dismiss()`（`heroes.cpp:1959`），战败与投降都走它。

冒险施法（`Heroes/Hero.Spells.cs` + `Heroes/IHeroSpellCastHost.cs`）：

* `CanCastSpell()` 按参考修了一处真错：原来「船长不能施任何冒险法术」，参考只拦召船、城镇之门、城镇门户；
  另加世界层重载 `CanCastSpell(Spell, World)`，六个法术各自的「现在施了有没有用」全部对齐（近海、有船可召、
  最近的城不是脚下且无驻守英雄、有空城、对手还有英雄、视野有怪、脚下是矿且新守卫更强）。
* `ActionSpellCast()` 的七个支全落：观地（六种模式）、鉴定、召船、次元门、城镇之门、城镇门户、视野、守卫。
  纯状态照参考写；次元门保留参考「自己收费并返回假以免外层再收」的约定。
* 需要玩家给答案的四类点进 `IHeroSpellCastHost`：次元门落点、城镇门户选城、观地窗口、视野的每堆提示。
  界面侧 `UI/Interface/AdventureMap.SpellCast.cs` 用嵌套类实现，观地直接复用 `_dialogs.OpenViewWorld`，
  视野文案复用参考原文；次元门与城镇门户的交互模式（虚线框、城列表）还没做，现答「取消」、不扣法力。
* 参考给英雄置的 `VISIONS` 位**没有读者**（只在 `ActionNewPosition` 被清），端口不建。

验证：`dotnet build` 0 警告 0 错误；`dotnet test` 5 480 全绿（删掉一条「怪物=不支持」的过时断言，减 1）。
偏差：`HeroModes.Visions`、`SAVEMP` 位、王国投降名册、视野的怪物画像仍在账上（§7.4）。

### 6.1e P1 第 2 批：可取对象与宝藏点（2026-11-22）

这一批全是「踩上去可能打一场，然后给东西」的瓦片，共用第 1 批的 `Fight`/`BattleLose` 与 `IHeroActionHost` 问句。

`Heroes/HeroActions.Capture.cs`：

* **矿 / 锯木厂 / 炼金实验室 / 灯塔**（`ActionToCaptureObject`，`heroes_action.cpp:2022`）。参考的所有权是<b>三层</b>：
  守军住在 `CapturedObject.Guardians`、颜色在同一张表的另一格、矿上还多一层守卫法术记在瓦片元数据上。所以
  「守军全灭但英雄输了」只清守军与那层法术、颜色不动（`2167-2174`）；接管时才按
  `removeObjectProtection → setColorOnTile` 的顺序（`2070-2075`）。已经是我方（含盟友）的直接返回，**参考连一句
  提示都没有**。
* **废弃金矿**（`ActionToAbandonedMine`，`2185`）。问「进不进去」→ 打幽灵 → 赢了 `RestoreAbandonedMine` →
  把英雄脚下对象改回矿 → 改颜色，三步顺序不能换（否则「透过英雄看」还是废墟）。
* **沉船 / 坟场 / 弃船**（`ActionToPoorMoraleObject`，`1354`）。有金币才有守军；<b>没金币就什么都没发生</b>，
  只记本地与全局访问、弹一段士气警告，而且<b>参考不清元数据</b>，可以反复搜——照抄。神器与金币可能同时到手，
  背包满则按 `ArtifactGold.InsteadOf` 折钱。

`Heroes/HeroActions.Treasure.cs`：

* **金字塔**（`ActionToPyramid`，`1159`）。有法术才有守军；空金字塔只记访问、不清元数据（同上）。学到法术要
  <b>魔法书 + 专家 Wisdom</b> 两个硬条件，缺哪个都只给提示；法术名必须跟着结果回去，因为成功后元数据就清了。
* **恶魔洞窟**（`ActionToDaemonCave`，`3042`）。四支赏赐（纯经验 / 经验加金币 / 经验加神器 / 拿钱赎命）全落，
  背包满会把神器那支折成金币支。**除了「不进去」都要清元数据并记全局访问**，包括打输与英雄战死——参考特意在
  注释里说明「王国可能都没了，瓦片还是记在前主人账上」。

接口两侧：`IHeroActionHost` 多了一个不带兵种的重载 `Ask(question, gold = 0)`（搜沉船、进废矿、搜金字塔、进洞窟、
打仆从、赎命六处只问是与否，其中赎命要把价钱念出来）；`HeroActionQuestion` 加 6 个值，`HeroActionKind` 加 3 个。

验证：构建 0 警告 0 错误；`dotnet test` 5 480 全绿。

### 6.1f P1 第 3 批：怪物居所（2026-11-22）

`Heroes/HeroActions.Dwelling.cs`，参考 `heroes_action.cpp:2237-2522` + 共用的 `RecruitMonsterFromTile`（`262-330`）。
三支按「白送 / 花钱买 / 先打服」分开，共二十一座瓦片：

* **白送**（`ActionToDwellingJoinMonster`，九座：树屋、弓箭手小屋、哥布林窝棚、矮人茅舍、半身人洞、农舍、守望塔、
  发掘场、洞穴）。三处细节：兵营满时居所原封不动；收下之后是<b>数量清零而不是清元数据</b>（种类留在瓦片上）；
  不管哪条路都记全局访问。
* **花钱买**（`ActionToDwellingRecruitMonster`，九座：废墟、树城、篷车队、沙漠帐篷 + 四座祭坛、荒冢）。派错了对象
  类型就直接返回，<b>连访问都不记</b>（参考 `2370` 的 `default: return`）。
* **先打服**（`ActionToDwellingBattleMonster`，三座：龙之城、亡灵之城、巨魔桥）。颜色不是 `None` 就说明守卫已被清过
  （记成 `PlayerColor.Unused`），跳过战斗直接进招募；「不打」与「打输」两支都<b>不记访问</b>。

共用的买卖（`RecruitMonsterFromTile`）顺序照抄：兵营满先回绝（连数量窗都不开）→ 问数量 → 改瓦片（买光且 `remove`
才清元数据，否则减数量）→ 扣钱 → 加兵。**`remove = true` 只有还没转的精灵灯会用**，参数先留着对齐签名。

两处与其它分支不同的地方值得记：<b>战斗不消耗居所存货</b>（守军是瓦片人口的副本，参考在胜利后重读人口并断言它
仍有效，端口不写回残兵）；<b>`PlayerColor.Unused`（0x80）补进了枚举</b>——之前端口用「按后果推断」绕开了它，居所
这一支必须读回这个值才知道守卫清没清。

接口：`IHeroActionHost` 加 `AskRecruitCount(monster, available, kingdom)`（唯一一个要玩家给**数字**的窗）；`HeroActionQuestion`
加 4 个值；`HeroActionKind` 加 3 个；`NullHeroActionHost` 一律答 0（AI 不买兵）。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿（`ColorToOffsetIcn` 的用例补了一行 `Unused → 6`）。

### 6.1g P1 第 4 批：设施类对象（2026-11-22）

`Heroes/HeroActions.Facility.cs`，参考 `heroes_action.cpp` 七处分散的函数。这一批的共同点是**都不打、都不占**——

要么是「加一点什么」，要么干脆只是开个窗口：

* **精灵灯**（`ActionToGenieLamp`，`1912`）。`RecruitMonsterFromTile` 的 `remove = true` 终于有调用者了：买光之后
  连元数据一起清掉，灯彻底消失。它<b>没有访问记录</b>——灯用「消失」表达「用过了」。
* **贸易站**（`ActionToTradingPost`，`1284`）。参考只有一行 `Dialog::Marketplace( kingdom, true )`，所以端口也只是把
  市场窗口转给宿主。**不记访问、不改状态**。
* **属性四座**（`ActionToPrimarySkillObject`，`1291`）：堡垒 +1 防御、雇佣兵营 +1 攻击、巫医小屋 +1 知识、石圈
  +1 力量。判断用<b>英雄级</b>记录而非王国级：同一位英雄拿不到第二次，另一位还可以。
* **炼金术士塔**（`ActionToAlchemistTower`，`3258`）。四道门：没有诅咒物品直接赶人 → 玩家拒绝 → 钱不够（隔门笑你）
  → 扣 750 金、把每件诅咒物品就地清成未知（<b>槽位不动</b>，位置有意义）。价钱与件数无关；**既不记访问也不清元数据**。
* **法师小屋**（`ActionToHutMagi`，`3436`）。话每次都说，揭图只揭一次（**王国级**记录）；揭的是<b>所有法师之眼</b>
  周围 9 格的圆。参考随后把镜头一片片摇过去，那是动画，端口只报揭了多少格。
* **法师之眼**（`ActionToEyeMagi`，`3512`）与**神谕**（`ActionToOracle`，`3023`）。前者只有一句话，后者是一句话加一张
  盗贼公会报表（<c>Dialog::ThievesGuild(true)</c>）。两者都只读，**不记访问**，随时可以再来。

接口：`IHeroActionHost` 加 `OpenMarketplace(kingdom, fromTradingPost)`、`OpenThievesGuild(kingdom)` 两个窗口入口；
`HeroActionQuestion` 加 `RubGenieLamp`、`PayAlchemist`；`HeroActionKind` 加 7 个。`NullHeroActionHost` 的开窗方法空转——
AI 本来就不逛市场（它在 `ai_hero_action.cpp` 里自己算）。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿。

### 6.1h P1 第 5 批：狮身人面像与事件瓦片（2026-11-22）

`Heroes/HeroActions.Landmark.cs`，参考 `heroes_action.cpp:3522`（`ActionToSphinx`）与 `2811`（`ActionToEvent`）。
两支都是「读了瓦片上的数据模型才知道要给什么」的对象，于是顺手把两个模型补成参考的样子：

* **狮身人面像**（`ActionToSphinx`）。五条出路：没有谜语或已答过 → 只记访问；拒答 → **什么都不记**（可以回来再答）；
  答错 → 英雄被吞（`BattleLose` 一支，全军覆没）；答对 → 给资源与神器、`MapSphinx.Reset()`、记访问。
  答对之后 `valid` 变假，第二个人只会听到「它安静地待着」；谜语与答案留在瓦片上，参考也不擦。
  *偏差*：参考在「答对了但没奖」那一支直接 `assert(0)` 什么都不做；端口按「答对」处理（清奖品 + 记访问），
  否则英雄会被同一道题反复拦住。那是存档里半个谜题才会出现的状态。
* **事件瓦片**（`ActionToEvent`）。顺序照抄：判颜色 → `SetMove(false)`（事件不耗移动力）→ 给资源（先按钱袋上限截，
  界面画的是截过的那份）→ 给技能（学不会就悄悄不给，学会侦察要立刻揭图）→ 给神器 → 给经验 → `SetVisited()`。
  **一次性事件要连瓦片一起擦**（先清英雄脚下的对象类型再 `MapObjects.Remove`），否则「透过英雄看」还是那个事件；
  常驻事件（收过路费那种）只交数量。数据缺失（被改过的地图）时清掉脚下对象类型就走人——也照抄，否则会被反复触发。

模型侧补齐（都是参考有、端口当初留空的）：`MapEvent.IsAllow`/`SetVisited`（`Colors` 改成可写，用尽即清零）、
`MapSphinx.Reset()`（`valid`/`resources`/`artifact` 改成可写）、`Kingdom.GetFunds()`。

接口：`IHeroActionHost` 加 `AskRiddle(riddle) → string`——全项目**唯一**一个要玩家敲字的窗；`HeroActionQuestion`
加 `AcceptSphinxChallenge`；`HeroActionKind` 加 2 个。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿。

### 6.1i P1 第 6 批（收口）：上下船与海岸、军队升级对象（2026-11-22）

`Heroes/HeroActions.Boat.cs`（参考 `heroes_action.cpp:668`/`718`）与 `Heroes/HeroActions.ArmyUpgrade.cs`（参考
`2642`）。

* **上船**（`ActionToBoat`）。顺序照抄且容易写错：记下上船前的陆地（`Hero.LastGroundRegion`）→ 关路径 → 侦察 →
  `MoveToDest` 站上船 → 清移动点 → 清路径 → alpha 拉满 → **方向随船**（船不会转）→ 擦掉船的部件 → 重算瓦片类型 →
  把「船底下的东西」显式写回英雄 → 瓦片改记 `OBJ_HERO` → 成为船长 → 清船的原主人颜色。
  关键：`Move2Dest` 里 `SetHero` 记下的是**船**，所以「擦船 + 改写 `ObjectTypeUnderHero`」必须落在它之后；
  先擦后站看着结果一样，但会随 `SetHero` 的改写时机静默出错。
* **下船**（`ActionToCoast`）。船留在原地，船头朝「船→岸」的方向；落脚后调一次 `ActionNewPosition`，把岸上那格的
  资源/宝箱顺手捡掉（参考 749）。这是动作里唯一一次嵌套分派，不会递归——海岸那一支只在船长身份下成立。
* **军队升级对象**（`ActionToUpgradeArmyObject`）：山丘堡垒（食人魔/兽人/矮人）、自由民铸造厂（剑士/长矛兵/石魔）、
  马厩（骑兵）。一律免费、不看钱不问话，**没有任何访问记录**，所以路过一次升一次。
  马厩据此补上「有骑兵就升级」那一半——它的两句记录是分开的：访问只管加 400 移动点，升级只看队伍里有没有骑兵。
* **判定补齐**：`Hero.IsNeedStayFrontObject` 加城堡支（非友方且城里有人才停门口）与船长的靠岸支
  （下一步是可上岸的陆地就停在水上）；分派器按身份挑对象表（`IsInGameActionObject(type, IsShipMaster)`），
  并把「船长靠岸」也算成一次动作，哪怕那格空无一物。

接口：`HeroActionKind` 加 `Boat`/`Coast`/`ArmyUpgrade`（共 3 个）；`ActionToStables` 的返回值由 `bool` 变成
`StablesResult`（测试跟着改）。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿。

### 6.2 已知回退与缺陷（用户同意留到最后统一修）

1. **面板拖动只画轮廓、松手不落位**。参考在松手时调 `BorderWindow::SetPosition`，而端口的
   `AdventurePanelResult` 只带 `BorderDrag` 不带「是哪个面板」。只有隐藏界面模式抓得到面板，正常布局
   永不触发；给结果补一处 `Commit` 即可。
2. **热键能返回、但值守不全**。`FinishHumanTurn` 可能回 `Win`/`Lose`/`HighScore`/战役那几支，端口的
   `HandleResult` 只接了 `EndTurn` 与「换局」三个值，其余记一行日志后忽略（§7.4）。
3. 视野中心与英雄差约 1.5 格（横向更多）。**干扰项已排除**：第 5 步之后 `--cursor` 会进 `LocalEvent`，
   第 6 步的截图（139 779）不再有「每帧往左上卷」的漂移；剩下的偏移要拿 `SetCenter(GetCenterOf(tile))`
   与瓦片绘制原点对拍。
4. 地图上的零星条带/方块。
5. 迷雾存了两份（`MapFog` 与 `Tile._fogColors`），`World.SyncTilesFog` 只是治标，应合并成参考的单份。
6. 基线改为「与参考一致」后需重新生成，差异记进 `tests/baselines/README.md`。第 5–6 步前后差异只从
   140 015 变到 139 779 像素，画面主体未动，仍是那批老缺陷（含状态面板高度与日期排版）。

## 7. 未完成清单（唯一列表）

### 7.1 主线

次序按 `PLAN.md` §5.2 的 P0–P8：**P0** 收桩（§6.1，**已收口**）→ **P1** 冒险层闭环（三类遭遇、冒险施法、可取对象与
宝藏点、怪物居所、设施类对象、狮身人面像与事件瓦片、上下船与海岸、军队升级对象已落，见 §6.1d–§6.1i，
**P1 收口**）→ **P2** AI 收口（`ai_hero_action` 的解耦与 B7 两个入口）→ **P3** 战斗界面四篇 → **P4** 战役 →
**P5** 高分屏与结局 → **P6** 启动缺口 → **P7** 随机地图生成器 → **P8** 编辑器。P3/P4 在 P2 之后可并行，P6 随时可插。

### 7.2 仍抛 `NotImplementedException` 的 7 处

| 位置 | 缺什么 | 依赖 |
| --- | --- | --- |
| `Game/Ai/AiHeroAction.cs:29` | `HeroesAction` | B7 / X4 |
| `Game/Ai/AiHeroAction.cs:37` | `HeroesMove` | B7 / X4 |
| `Game/Artifacts/UltimateArtifact.cs:165` | `GetPuzzleMapSurface()` | `GameArea.GenerateUltimateArtifactAreaSurface` 已在，直接接 |
| `Game/Campaign/CampaignData.cs:212` | 战役联盟表合并（`Mp2FileInfo.PlayerUnions` 已存在，可直接写） | P4 |
| `Game/Editor/HistoryManager.cs:409` | 端口地图格式缺 per-object 元数据表 | 编辑器（S16 余支） |
| `UI/Interface/BaseInterface.cs:142` | `renderWithFadeInOrPlanRender` | Game 淡入状态（S14） |
| `UI/Interface/BaseInterface.cs:149` | `validateFadeInAndRender` | 同上 |

### 7.3 AI 剩余

只有 B7 + X4（§4）：`ai_hero_action.cpp` 的 `HeroesAction` / `HeroesMove` 两个入口，以及它们依赖的对象交互
分派与 `Interface::AdventureMap::Get()` 解耦。其余批次全部完成。

### 7.4 界面缺口（非主线，按里程碑挂账）

语言选择窗口（M2）、难度与标准局-战役分叉（M3）、战役界面接线（M9）、SMK 视频（M9）、
随机地图生成器（S8 旁支）、编辑器窗口按参考重写（S16）；`FinishHumanTurn` 的 `Win`/`Lose`（胜负屏）、
`HighScore`、战役分支还没有处理器，现在只记一行日志。

P1 施法留下的三处界面缺口：次元门的目的地框（`Interface::AdventureMap::GetDimensionDoorDestination`，
一圈虚线框内的点选模式，`game_interface.cpp:175-323`）、城镇门户的城列表
（`Dialog::selectKingdomCastle`，要带城堡图标的列表框）、视野提示里的怪物画像——两者现在答「取消」，
第三项只有文字。三条都随冒险地图的交互模式与对话元素一起做。

P1 居所留下的界面缺口：`IHeroActionHost` 目前**没有任何界面实现**（连旧的三类遭遇都还没有），所以
`AskRecruitCount` 只能由将来的冒险地图交互层回答。参考的 `Dialog::RecruitMonster` 是「怪物头像 + 数量滑条 +
总价」的独立窗口，端口的 `RecruitDialog`/`RecruitWindow` 是给城堡屏写的（要城堡的种族背景），地图居所需要一个
不带城堡的变体——`RecruitWindow.TryCreate` 的 `castle` 参数得先松成「种族 + 钱袋」。

设施类（§6.1g）另留两个窗：`OpenMarketplace` 与 `OpenThievesGuild`。前者的数据早就有了（`ResourceTrading` 那套
兑换率），缺的是「从冒险地图打开」的入口与「买卖完成后再刷英雄面板」的回路；后者端口已有 `ThievesGuildView`
（城堡屏在用），只要允许它带 `oracle: true` 的标题从任何地方打开即可。两者都**不写世界状态**，接上界面就能用。

狮身人面像（§6.1h）要一个**文字输入框**：`AskRiddle` 是全项目唯一需要键盘输入的窗口。参考用现成的
`Dialog::inputString`（带一行提示、一个输入框、最长 6 个字符的老限制），端口没有对应控件——Avalonia 的
`TextBox` 能直接用，但要新写一个模态小窗；答案比对（`MapSphinx.IsCorrectAnswer` 的前四字母规则）已经在游戏层。

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
