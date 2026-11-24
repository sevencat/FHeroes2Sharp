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
| 测试 `dotnet test` | **5 513 绿**（Core 284 / Assets 428 / Game 3 295 / UI 1 506） |
| 冒烟 `--enter-map --smoke-frames 60 --no-audio --no-config` | 72.6 FPS，退出 0 |
| 像素基线 `menu.png`/`settings*.png`/`scenario*.png` | 2026-11-24 重建（Editor 招牌进基线），五张全部 identical |
| `throw new NotImplementedException` | **5 处**（清单见 §7.2） |
| 冒险地图基线 `VerifyAdventureMapBaseline.ps1` | 139 779 / 307 200 像素不同——**已改为「与参考一致」的目标，不再当回归** |
| 当前工序 | **P0/P1 完成**；**P2 AI 收口**——动作链已接通并通过 7 天冒烟（§6.3），余 App 的 AI 回合循环（§7.3）与估值数值核对；**P6 起步**——配置读写与接线（§6.5）、首运语言窗与问候（§6.6）已落 |
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
* `ai`：两个入口（B7/X4）已补齐，动作链走通，见 §4、§6.1k。
* `gui`/`dialog`/`game`：只做完第一遍，第二阶段按 1:1 重做（§5）。
* `editor`：模型层按参考，**窗口未按参考**；`HistoryManager` 一处抛（§7.2）。

## 4. `ai` 目录（B / X 批次）

B0–B6、B8、X1–X3 全部完成（与 `PLAN.md` §4 同一份账）。2026-11-22 复核时改正了三处过时记录：

* **B4 已是 11/11**：`KingdomTurn` 的回合循环已随 `AiPlanner.Hero.cs` 落地，不再抛。
* **B8 全落**：`BattleArena.Action` 的 `ApplyAction` 十一个分支齐（含 `Spellcast`/`Tower`/`Catapult`），
  `BattleLoader` 三处正文也补完了；唯一残留是 default 分支里一句过时的异常文案。
* **X3 全落**：`battle_arena` 的十个查询（`GetTargetsForSpell`、`CanRetreatOpponent`、`IsSpellcastDisabled`、
  `IsAbleToResurrectFromGraveyard` 等）都在。

**B7 + X4 也已落地**（2026-11-24，§6.3）：`AiHeroAction.HeroesAction` / `HeroesMove` 都在，
`Ai/` 目录现在没有 `NotImplementedException`。`--skip-turns 20` 让三个电脑色连续跑满 7 天，无断言、无异常。

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

### 6.1j P2：`ai_hero_action` 解耦与两个入口（2026-11-24）

参考 `ai/ai_hero_action.cpp`（2 504 行）。落点四个文件：

| 文件 | 内容 |
| --- | --- |
| `Ai/AiHeroAction.cs` | 入口 `HeroesAction` / `HeroesMove`，以及施法（次元门/召船）、`Meeting`、`BattleLose`、`SelectSkillFromArena`、`CanMonsterJoinHero`、`CastGuardianSpellOnCapturedObject` |
| `Ai/AiHeroAction.Object.cs` | 资源/营地/标牌/瞭望塔/路障/军队升级/沉船/上下船/神龛/女巫小屋/方尖碑 |
| `Ai/AiHeroAction.Object.Ai.cs` | 城堡、英雄、野外怪物、宝箱、地面神器 |
| `Ai/AiHeroAction.Object.Landmark.cs` | 占领对象、坍废矿、事件、玛格丽特地图、漩涡、主技能对象、魔法井、废弃营地、金字塔、知识之树、恶魔洞、三类居所、监狱、炼金塔 |

关键决定：

* **凡玩家侧会问宿主的动作都不能直接复用**。`NullHeroActionHost` 对 `Ask` 一律答「否」，于是宝箱选择、野怪加入、
  攻城、坍废矿、废墟/墓园/残船、三类居所、精灵灯、监狱、炼金塔、金字塔、恶魔洞、市场、盗贼公会、谜语、招募数量
  这些分支在 AI 侧都是独占实现。反过来，「模型改动与玩家点『好』完全一样」的对象（资源、营地、标牌、瞭望塔、
  漩涡、魔法井、知识之树、光环对象等）直接转调玩家侧的 `HeroActions.ActionTo*`，注释里写明为什么可以复用。
* **AI 独有的模型改动**：占领对象后自动施守卫法术（`ai_hero_action.cpp:883` 的 `castGuardianSpellOnCapturedObject()`）；
  山丘堡垒/自由民铸造厂**免费**升级；宝箱按「要经验的概率」掷骰而不是问玩家；监狱先看英雄名额（
  `Kingdom.MaxHeroes`）再放人；漩涡先用 `Army.ArrangeForWhirlpool()` 重排队伍；捡地面神器前查背包满与已有法术书。
* **会面**：新增 `Ai/AiHeroMeetingUpdater.cs`（参考 `heroes.h` 的同名 RAII 类）——构造时互相标记会面，
  `Dispose` 时遍历王国英雄清掉这些标记。整个动作包在 `using` 里，异常路径也不会留下假会面记录。
* **全局访问 = 王国名册**：参考的 `hero.SetVisited( index, Visit::GLOBAL )` 在端口对应 `Kingdom.SetVisited`，
  不要和英雄本地名册（`Hero.SetVisited`）混用；标牌、瞭望塔、玛格丽特地图、居所、监狱、恶魔洞都走王国名册。
* `Hero.GetCurrentMapVersion()` 由 `private` 改 `internal`（AI 选船位要判地图版本）；
  `AiHeroAction.HeroesCastSummonBoat` 的返回值由 `bool` 改为 `int`——参考返回的是**船原来的格子**，
  AI 要拿它清路径缓存里的船记录（`ai_hero_action.cpp:2221-2225`）。

验证：构建 0 警告 0 错误；`dotnet test` 5 481 全绿。

### 6.1k P2 收口：把 AI 动作接进走子（2026-11-24）

§6.1j 之后两个入口是写好了，但没人调用：参考里 `AI::HeroesMove()` 只反复调 `hero.Move()`，脚下的对象
由 `Heroes::Action()` 自己按 `GetKingdom().isControlAI()` 分派（`heroes_action.cpp:3778-3785`）。端口把这道
分岔留在 `IHeroMovementHandler` 这个接缝上，而冒险会话给所有英雄装的是「只开图」的
`FogHeroMovementHandler`，于是 AI 走过资源、宝箱、矿场时什么都不会发生。

本轮改五处：

| 位置 | 改动 |
| --- | --- |
| `Ai/AiHeroMovementHandler.cs`（新） | AI 英雄的四个回调：开图、`AiHeroAction.HeroesAction`、`ActionNewPosition`（守卫扫描 + `AiPlanner.HeroesActionNewPosition`）、雷达 |
| `Game/AdventureSession.cs` | 按 `Kingdom.IsControlHuman` 选处理器：人类装 `FogHeroMovementHandler`，电脑装 `AiHeroMovementHandler` |
| `Heroes/IHeroMovementHandler.cs` | `ActionNewPosition` 加 `allowMonsterAttack` 参数，对齐参考的 `Heroes::ActionNewPosition( bool )` |
| `Heroes/Hero.Movement.cs`、`Heroes/HeroActions.Boat.cs` | 传 `true`（`heroes_move.cpp:232`、`heroes_action.cpp:749`） |
| `Heroes/Hero.Spells.cs` | 次元门落地传 `false`（`heroes_spell.cpp:227`） |

关键决定：

* **分岔放在安装时，不放进英雄里**。参考的 `Heroes::Action()` 自己判断控制权；端口让「装哪个处理器」表达同一件事，
  这样 `Hero.ActionNewPosition()` 的调用方不用各自再判一次。人类那一支仍走 `FogHeroMovementHandler`——它的动作
  要问 `IHeroActionHost`，界面实现还没有（§7.4），装上去只会得到一串「否」。
* **守卫扫描是 `ActionNewPosition` 的前半段，对人对 AI 都做**，只有后半段（`AI::Planner::HeroesActionNewPosition`）
  分控制权。先打路线终点那格的守卫，没有才打第一组邻格的（`heroes.cpp:2031-2041`，这时路线还没弹下一步）。
* **瞬移不引战**：`allowMonsterAttack = false` 出现在次元门与两个传送点（`heroes_action.cpp:1978`、`:2019`），
  落地后只结算脚下的对象，不因为旁边有游荡怪就开战。
* **`AI::HeroesCastDimensionDoor` 两处漏掉的尾巴一并补上**：`++hero.DimensionDoorUses`
  （`ai_hero_action.cpp:2445`，否则一天的次元门次数不递减）与落地后的 `ActionNewPosition( false )`（`:2456`）；
  同时删掉端口多写的一次重复 `Scout`。
* **`AdventureSession` 仍不排电脑回合**。这一轮只保证「电脑英雄一旦走起来就会处理对象」，把整局交给
  `AiPlanner.KingdomTurn` 需要的画面循环（走子动画 + 进度条）记在 §7.3。

验证：构建 0 警告 0 错误；`dotnet test` 5 482 全绿（新增 `AiHeroMovementHandlerTests`，让 AI 英雄沿一格路线
走到木材上，断言国库 +5、路线清空、瓦片主物件被抹掉）。§7.2 的 `NotImplementedException` 相应从 7 处降到 5 处。

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

### 6.3 冒烟驱动的 AI 收口（2026-11-24）

口径：`--skip-turns N --smoke-frames 200`（Alteris.mp2）连续跑电脑回合，一个断言修一处，直到连跑 7 天全绿。
`--skip-turns` 现在在 `IsAiTurn` 时走 `RunAiTurn()`（以前只结束人类回合，AI 根本不动）。这一轮抓到的都是真缺陷：

| 症状 | 根因 | 修法 |
| --- | --- | --- |
| 英雄站在已占矿上，捕获与守卫查询判否 | 瓦片主物件被换成了 `ActionHero` | `Capture`、`TileTroops.GetTroop`、`World.IsValidCastleEntrance` 一律改用「脚下那个物件」的读取 |
| `IsCaptureObject` 在带动作位的值上恒假 | 直接比较裸枚举 | `Mp2ObjectTypes.IsCaptureObject` 先 `NonActionType` 再比 |
| 城堡动作被派发到塔/山体（裸 35） | 折位前没过滤 | `AiHeroAction.HeroesAction` 先用 `IsActionObject(storedType)` 筛，再折位 |
| 第 2 天 `objectType == NonActionType(tile.MainObjectType)` 断言 | 路上对象的估值传了原值 | `AiPlanner.GetPriorityTarget` 路上对象先折位再估值 |
| `Unexpected resource type 0` | 资源堆按图标反推时**折了两次**（`ToResource(ResourceOfObjectFrame(i))`，把资源位当成 RESOURCE.ICN 帧号） | `Tile.InitialObjectInfo` 直接用 `ResourceOfObjectFrame` |
| 同一天把刚雇到的英雄再雇一次 | AI 用了不刷新的 `GetRecruits()` | 改 `GetRecruits(world)`（参考 `Kingdom::GetRecruits()` 本身就刷新槽位） |
| 新雇英雄撞在矿前不动，移动力一格格耗光 | 开局装载只给当时的名册装处理器；新英雄拿着 `NullHeroMovementHandler`，动作全落空 | `AdventureSession.AttachMovementHandler` 抽出，`Castle.RecruitHero` 成功后再装一次；`Hero.Recruit` 补抄 `IsControlHuman`（默认值是 `true`，电脑英雄会被当成人类） |
| 拾取后瓦片仍报告「宝物还在」，AI 缓存不更新 | `isActing` 用折过位的类型判定，`IsInGameActionObject` 要求带位，恒 false | 传 `storedType`（原值）；`GetDistanceModifier` 同样把 `IsInGameActionObject` 换成折位后的等价判定 |
| 物件摘掉后类型与通行性不更新（AI 反复撞同一格） | 动作代码用 `Tile.RemoveObjects(icnType)`（只摘部件） | AI 的十个动作点统一改成 `Tile.RemoveMainObject(world)`（按 UID 删、重算类型与通行性） |
| 地图上的随机资源/神器/怪物一直是占位类型，AI 与渲染都不认 | 参考在载入时当场抽定（`updateObjectInfoTile()` 里的 `updateRandomResource/Artifact/Monster`，`maps_tiles_helper.cpp:61-193`），端口没移植 | `Tile.InitialObjectInfo` 补三个 `UpdateRandom*`：抽定后置 `ActionResource`/`ActionArtifact`/`ActionMonster`，本体帧与**左格的影子**用同一 UID 一起换（资源堆帧由种类定、神器 `(id-1)*2+1`、怪物 `id-1`），再递归走一遍 `SetInitialObjectInfo` 按真对象填元数据；fh2 地图优先用 `Metadata[1]` 并清零 |
| 英雄踩在矿/怪物/沉船上时读取器全部判否 | 参考读的是 `getMainObjectType(false)`（看穿英雄），端口读带英雄的类型 | 读取器统一走 `GetNonActionObjectType(world)`：`TileMetadata.Mutators` 的 mine/daemon cave/shipwreck/monster 读取器、`TileTroops` 与 UI 的矿守卫渲染都改成收 `World` |
| 1.1.08 以前的旧存档里被编辑器 hack 过的矿读回来是空的 | 参考在 `Tile::operator>>` 里对非 `EXTRAOVR` 的矿调 `setInitialObjectInfo()`（`maps_tiles.cpp:1956-1961`），端口只留了个空注释块 | 把这一步挪到 `World.ReadFrom` 的瓦片循环后（`RepairHackedMines()`，端口同名方法要 `World`），版本条件与参考一致 |
| 同盟玩家互相视为敌人（通行、战斗、AI 都按「同色才算朋友」） | 参考的联盟表 `fi.unions` 在玩家名册上，`Player::SetFriends( unions[i] \| color )`（`players.cpp:325`）；端口 `Mp2FileInfo` 只有 `PlayerUnions` 字段，没算、也没人用 | MP2 头补 `FillUnions()` 与「击败另一方」的切边（`maps_fileinfo.cpp:341-371`），`PlayerUnions` 默认值改成每色自己；`Players.Init` 把 `unions[i] \| color` 写进 `Friends`；`Hero.IsFriends` 改为查名册；`CampaignData.UpdateScenarioGameplayConditions` 把 AI 并成一方（`campaign_data.cpp:697-747`） |
| 英雄走到漂流瓶上没有反应 | 参考里瓶子与散落资源共用 `ActionToPickupResource()`，但走的是不付钱、只弹留言的 `OBJ_BOTTLE` 分支（`heroes_action.cpp:758`）；端口的分发器 `switch` 里没有 `Bottle`，落到 `Unsupported` 之外的 `None` | 拆出 `HeroActions.ActionToBottle`（读 `World.GetMapSign` 的留言、清瓦片元数据），新增 `HeroActionKind.Bottle`，分发器单独一支；AI 侧原有 `AiToPickupResource` 已抹瓶子，不读留言（与参考一致） |

**AI 打起来之后的战斗链**（`AdventureSessionTests.RunAiTurnPlaysTheWholeComputerTurn` 让两台电脑英雄撞上，一个断言修一处）：

| 症状 | 根因 | 修法 |
| --- | --- | --- |
| `BattleTactics.GetUnitMovementTarget` 断言「单位总有地方站」 | 缺了参考的两段式：先问「本回合走不走得到」，问不到才退到「路上最远能站哪」。端口直接拿「本回合不可达的空位置」去问后者，答案恒空 | 按 `ai_battle.cpp:509-529` 重写成两段，第一段命中就返回 |
| `MeleeUnitOffense` 断言「选中的邻格对单位有效」 | `FindNearestCellNextToUnit` 是简化版：只按格子距离取最近，不检查能否为它构造出合法站位、也不检查走不走得到 | 按 `ai_battle.cpp:477-507` 重写：先 `Position::GetPosition` 拿站位（拿不到就跳过），再要求与目标**站位**相邻、`isPositionReachable` 可达，最后取移动代价最小的那格（`Distance` 是移动代价，不是格数） |
| 「士气高涨却什么都没做」，回合空转 | `BattleArena.UnitTurn` 自己加的兜底把「没动过」硬记成动过，正好吃掉 `ApplyActionMorale(1)` 清掉 `Moved` 换来的额外行动 | 删掉兜底，让参考的退出条件管循环：动过 → 速度归零 → 下一圈 `GetSpeed() == Speed.Standing` 置 `endOfTurn` |
| 打完一仗后下一个英雄估威胁时寻路器收到 `MapIndex == -1` | `UpdatePriorityTargets` 拿 Castle/ActionHero 判分支，而调用点传的是**折过位**的 Hero 类型，英雄分支永远进不去，输掉的英雄残影留在 `_enemyArmies` | 入口先 `NonActionType` 再判；顺带把「他守的城堡还在吗」也折位比较（城门格存带位城堡） |
| 「射手在没有箭的时候开火」 | `BattleUnit.IsArchers` 读的是兵种表（`_troop.IsArchers`），而参考的 `isArchers()` 是 **虚函数链**：`Monster::isArchers() = GetShots() > 0`，`Battle::Unit` 把 `GetShots()` 重写成剩余箭数 | 改判 `_shotsLeft > 0`；打光箭的弓箭手按参考退回近战，AI 的十余处 `unit.IsArchers` 判定随之正确 |

验证：`dotnet build FHeroes2Sharp.slnx` 0 错误 0 警告；`--skip-turns 20 --smoke-frames 200` 退出 0
（day 1 → day 7，蓝/黄/紫三色，AI 每日有真实移动与拾取），`--skip-turns 60 --smoke-frames 60`
退出 0（day 21）；全量测试 5 506 通过、0 失败（其中 `AdventureSessionTests` 里的 AI 回合已打过一场
英雄对英雄的战斗，`WorldLoadMapTests` 的三条随机物件测试断言抽定后的类型/帧/元数据，
`Mp2FileInfoTests` 与 `CampaignTests` 钉住联盟表的默认值、切边、非法参数与战役并方，
`HeroSignTests` 的两条漂流瓶测试钉住留言、元数据清零与「国库不动、名册不记」，
`TinyConfigTests` 与 `SettingsTests` 钉住配置文件的解析与往返）。
**未做**：估值数字与参考对拍；战役开局流程仍未接 `UpdateScenarioGameplayConditions`（等 P4 接线）。

### 6.4 配置文件读写（2026-11-24）

`TinyConfig` 与 `Settings::Read()` / `Save()` / `getOptionsString()` 落地（`settings.cpp:145-583`、
`engine/tinyconfig.cpp`）。参考借 `fheroes2.cfg` 记住音量、速度、开关与「隐藏界面」时四个窗口的位置；端口此前只有
内存态，`IsFirstGameRun` 恒为 true——没有人清，也没有文件可清。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 解析 | `TinyConfig( '=', '#' )`：键小写 + 去空白 + 连续空白压一个、重复键留第一个、无分隔符的行跳过 | `Game/System/TinyConfig.cs` 同名同义；`IntParams` 整串必须是数字否则 0，`PointParams` 只认 `[ x, y ]`，`ResolutionParams` 认 `宽x高:宽x高` 与旧格式 `宽x高x倍数`（倍数为 0 或乘出溢出时回退） |
| 读 | 每个键一个默认值 + `Exists` 检查；`music` 认不出就是 external、`battle turn order` 认不出就是 off、`interface type` 认不出就是 dynamic；`first time game run = off` 才清首运标记 | `Settings.Read(string)` 逐键照抄，读不到文件返回 false 且一个字段都不动 |
| 写 | `GetConfigDirectory( "fheroes2" )` 下拼文件名 | `Settings.Save(string)` 收完整路径，位置归宿主（与 `GameHotKeys` 的热键文件一致） |
| 键集合 | 音量、速度、全部开关、四个隐藏界面窗口位置、语言、首运标志、存档排序、兵力估算 | 同上，除 `videomode`、`game window position`（UI 层持有，`Save`/`GetOptionsString` 的可选参数写出）与 `debug`（日志层未移植）。`videomode`/窗口位置在 `Read` 里读到也跳过，不留半截状态 |

**验证**：构建 0 错误 0 警告；全量测试 5 506 通过（`TinyConfigTests` 10 条 + `SettingsTests` 4 条，含一次
「改字段 → 存盘 → 重置 → 读回」的往返）；冒烟 20 回合到 day 8。
**未接**：启动时读、退出时写还没进 `GameHost`——分辨率与全屏真正生效要等 UI 层消费这些设置，接线与
语言选择窗、`BaseInterface` 的淡入一起做（P6）。

> 2026-11-24 已接：启动读、首次运行写默认、退出与设置应用时写都进了 `GameHost`，见 §6.5；分辨率/全屏的
> **生效**仍等 UI 层，`videomode` 只写不读。

### 6.5 配置文件接线（2026-11-24）

`Settings.Read`/`Save` 接进客户端启动流程，对应参考 `game_init.cpp:217-230` 的 `initConfig()` 与
`Settings::Save()` 的调用点。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 路径 | `System::GetConfigDirectory( "fheroes2" )`：Windows 为 `%APPDATA%\fheroes2`，再拼 `fheroes2.cfg` | `Game/System/ConfigurationFile.cs`（`DefaultPath`/`Resolve`/`EnsureDirectory` 纯函数，取 `%APPDATA%` 留给宿主）；`--config <file>` 换一份 |
| 读 | 读不到就写一份默认的（`initConfig` 的 else 分支） | `GameHost.LoadSettings()`：`Read` 成功就记日志，失败/缺文件则 `EnsureDirectory` + `Save`，写不出去只降级（偏好不保存，客户端照常跑） |
| 写 | 设置窗改完、热键改完、雷达拖完各处 `conf.Save()` | `GameHost.SaveSettings()` 在退出与语言变更两处调用（所有修改点的超集）；`videomode` 写当前逻辑尺寸×窗口尺寸 |
| 语言 | `setGameLanguage` 后 `Translation::reset()` | `--lang` > 配置文件 `lang` > 英文；`MainMenuScene.LanguageChanged` 回调让宿主把对话框里选的语言记进配置 |
| 基线 | 无此概念 | `--no-config` 完全不碰文件；五个像素基线脚本都带它，开发机上的偏好影响不到基线 |

**验证**：构建 0 错误 0 警告；全量测试 5 512 通过（`ConfigurationFileTests` 6 条）；`--config <temp>` 首次运行生成
默认文件（`videomode = 640x480:1280x960`、`sound volume = 6`、`lang =`、`first time game run = on`），第二次启动
日志 `Configuration read from ...`，退出时再写一次；`--no-config` 冒烟 20 回合到 day 8，日志只有
`Configuration file disabled`。
**顺带修**：五张像素基线自 2026-11-14 起就落后（主菜单的 Editor 招牌没进基线，恒差 9 867 像素），已重建并复验
`identical`，记在 `tests/baselines/README.md`。
**未做**：首次运行的语言选择窗 + 欢迎消息（参考 `game_mainmenu.cpp:265-300`）、`videomode`/`fullscreen` 的**生效**、
`IsFirstGameRun` 的清除（跟着首运 UI 一起做）。

> 2026-11-24 已接：语言选择窗、问候消息与旗标清除都进了 `MainMenuScene`/`GameHost`，见 §6.6；`videomode`/`fullscreen`
> 的生效仍等 UI 层。

### 6.6 首次运行的语言选择与问候（2026-11-24）

参考 `game_mainmenu.cpp:265-300` 的 `isFirstGameRun` 分支：`selectLanguage( ..., true )` → 语言窗，欢迎消息
→ `Dialog::SelectResolution()` → `conf.resetFirstGameRun(); conf.Save()`。端口的这套流程放在主菜单场景里，用
**场景级模态**（跟设置窗、场景列表同一套输入路由），不是 `GameHost.RunModal` 的阻塞循环——这样 `--screenshot`、
`--smoke-frames` 照样能出帧和退出。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 触发 | `Game::MainMenu( isFirstGameRun )` 的参数，源头是 `conf.isFirstGameRun()` | `GameHost.ShouldStartFirstRun()`：`_configFile is not null` 且 `Settings.IsFirstGameRun`；`--no-config` 下旗标停在默认 `true` 上，若照它开窗，五张像素基线和所有脚本都会停在语言窗（ADR-202） |
| 语言窗 | `fheroes2::selectLanguage( ..., true )`，OKAY + CANCEL | `MainMenuScene.BeginFirstRun()`：`LanguageSelectionDialog` 居中，按钮取 `SettingsArtwork` 的 OKAY（帧 1–2）与 CANCEL（帧 3–4，缺帧时退回 OK）；上下键走 `MoveLanguageSelection` |
| 问候 | `Greetings!` + 欢迎段 +（触摸段）+（分辨率段） | `ShowWelcome()` 只写欢迎段：端口没有分辨率选择窗（`videomode` 只写不读），也没有触摸输入，写出来就是空承诺，差异记在 ADR-202 |
| 收尾 | `conf.resetFirstGameRun(); conf.Save()` | `FirstRunFinished` → `GameHost.OnFirstRunFinished()`：`SetGameLanguage( menu.Text.Culture )`、`ResetFirstGameRun()`、`SaveSettings()` |
| 语言记忆 | `selectLanguage` 直接改 `Settings` | 确认时 `SetLanguage` 切场景语言（CANCEL 保持原语言），收尾时宿主按菜单当前语言落盘；不论确认还是取消都走向问候，与参考一致 |
| 语言落地 | 无 | 菜单在既无 `--lang`、配置里也没有 `lang` 时会自己落到系统语言上，`CreateScenes` 把这个结果记进 `Settings`，文件里的 `lang` 才不会和界面打架 |
| 基线 | 无此概念 | `--no-config` 完全不触发；用 `--config <file>` 的脚本要先把 `first time game run` 写成 `off`，否则会停在语言窗上 |

**验证**：`--config <temp>` 首启日志 `First run: language picker opened, 'zh_CN' selected of 2 installed, content 345x306`，
截图 `tools-output/firstrun-picker.png`（标题「选择游戏语言：」、English/简体中文两行、现状中文在下方的选中栏）；
`--cursor 245,378 --left-click` 落在 OKAY 上，日志 `language 'zh_CN' -> 'zh_CN'`、`greeting shown`，截图
`tools-output/firstrun-greeting.png`；退出后配置里 `first time game run = on`（问候没关）、`lang = zh_CN` 已写下；
配置改成 `off` 或加 `--no-config` 都不再出窗。收尾那一步（清旗标 + 写文件）由
`SystemSettingsTests.ASavedResetFirstGameRunStaysClearedInTheFile` 守住：写出 `off`、读回仍清着。
**没验到的**：合成点击一轮只发一次（`--left-click` 固定在第十帧、只有一个 `--cursor`），所以「点问候的 OKAY 之后旗标被清」
没有端到端截图，只有上面那条持久化测试加四行接线；真要端到端点，得让脚本能连点两个位置。
**未做**：分辨率选择窗与 `videomode` 的生效、触摸输入的检测、问候里的另两段、`MainMenu` 退出时的窗口位置保存
（`game_mainmenu.cpp:240-244`，等窗口位置能被 UI 消费）。

## 7. 未完成清单（唯一列表）

### 7.1 主线

次序按 `PLAN.md` §5.2 的 P0–P8：**P0** 收桩（§6.1，**已收口**）→ **P1** 冒险层闭环（三类遭遇、冒险施法、可取对象与
宝藏点、怪物居所、设施类对象、狮身人面像与事件瓦片、上下船与海岸、军队升级对象已落，见 §6.1d–§6.1i，
**P1 收口**）→ **P2** AI 收口（`ai_hero_action` 的解耦与 B7 两个入口）→ **P3** 战斗界面四篇 → **P4** 战役 →
**P5** 高分屏与结局 → **P6** 启动缺口 → **P7** 随机地图生成器 → **P8** 编辑器。P3/P4 在 P2 之后可并行，P6 随时可插。

### 7.2 仍抛 `NotImplementedException` 的 4 处

| 位置 | 缺什么 | 依赖 |
| --- | --- | --- |
| `Game/Artifacts/UltimateArtifact.cs:165` | `GetPuzzleMapSurface()` | **有意留**：`GameArea` 在 UI 层，Game 不能反向依赖；UI 的 `AdventureMapDialogs.ShowPuzzleMap` 已经做了同一件事（拼出拼图并开窗），保留抛是为了不把界面搬进模型层 |
| `Game/Editor/HistoryManager.cs:409` | 端口地图格式缺 per-object 元数据表 | 编辑器（S16 余支） |
| `UI/Interface/BaseInterface.cs:142` | `renderWithFadeInOrPlanRender` | Game 淡入状态（S14） |
| `UI/Interface/BaseInterface.cs:149` | `validateFadeInAndRender` | 同上 |

### 7.3 AI 剩余

**AI 侧的代码全部落地**（§6.1j、§6.1k、§6.3）：`HeroesAction` / `HeroesMove`、回合与英雄回合、动作分派、
移动回调（`AiHeroMovementHandler`）都在，`KingdomTurn → HeroesTurn → HeroesMove → HeroesAction`
这条链在测试里能整段跑完，冒烟里能连跑 7 天。估值数字（距离/时间缩放的实际量级）还没和参考对拍。

剩下的是**驱动方**：`AdventureSession` 仍然只排人类玩家（`EndTurn` 跳过电脑色），因为参考里电脑回合是
`AI::Planner::KingdomTurn()` 外加一轮走子动画与进度条（`game_startgame.cpp:800-1000`），端口的画面循环
还没有「电脑同盟回合」这一态。要做的是给 `AdventureMap` 加一个 AI 回合阶段：逐个电脑色跑 `KingdomTurn`，
其间把 `hero.Move` 的走子按帧推进（同人类的 `AnimateHeroMovement`），跑完再回 `EndTurn`。
这一步与 §7.4 的 `FinishHumanTurn` 胜负屏同属冒险地图交互层，一起做。

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
