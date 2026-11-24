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
| 测试 `dotnet test` | **5 566 绿**（Core 292 / Assets 428 / Game 3 305 / UI 1 541） |
| 冒烟 `--enter-map --smoke-frames 60 --no-audio --no-config` | 72.6 FPS，退出 0 |
| 首运冒烟 `SmokeFirstRun.ps1` | 2026-11-24 通过：`--left-click-at` 四击从语言窗走到清旗标（§6.11），CI 一步 |
| 像素基线 `menu.png`/`settings*.png`/`scenario*.png` | 2026-11-24 重建（Editor 招牌进基线），五张全部 identical |
| `throw new NotImplementedException` | **5 处**（清单见 §7.2） |
| 冒险地图基线 `VerifyAdventureMapBaseline.ps1` | 2026-11-24 结案：按 §7.5 的三条差异核对（英雄居中 / 迷雾生效 / 部队状态栏）后 `-Update`，加 `--seed 1` 固定随机，连跑三次 `identical`；同日 `adventure-map-boats` / `adventure-options` / `quick-info` 三张也重建（提示框靠 `--right-button` 按住，ADR-208），四张连跑两次 `identical` |
| 战斗屏幕基线 `VerifyBattleBaseline.ps1` | 2026-11-24 新增两张：`battle.png`（野战部署）与 `battle-siege.png`（攻城，多出城堡地面/护城河/城墙/塔/攻城车），各连跑两次 `identical`（§6.14、ADR-212） |
| CI | `ci.yml` 的 `menu-baseline` job 十二条命令覆盖全部十二张基线 |
| 当前工序 | **P0/P1/P6 完成**；**P2** 动作链接通并通过 7 天冒烟（§6.3），余估值数值核对；**P3 进行中**——战斗屏幕的静态层、待机动画、兵力计数条与逐单位推进接缝已落地（§6.14–§6.16），下一步是人类的行动交互（§7.6）；P4 战役、P5 高分屏、P7 随机地图生成器、P8 编辑器仍全缺 |

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
| 键集合 | 音量、速度、全部开关、四个隐藏界面窗口位置、语言、首运标志、存档排序、兵力估算 | 同上，除 `debug`（日志层未移植）。`videomode`/`game window position` 读进 `CurrentResolutionInfo`/`SavedWindowPosition`，写回时由宿主把窗口的现状交进来（参考 `settings.cpp:303-309`、`:427-433`，见 §6.8） |

**验证**：构建 0 错误 0 警告；全量测试 5 506 通过（`TinyConfigTests` 10 条 + `SettingsTests` 4 条，含一次
「改字段 → 存盘 → 重置 → 读回」的往返）；冒烟 20 回合到 day 8。
**未接**：启动时读、退出时写还没进 `GameHost`——分辨率与全屏真正生效要等 UI 层消费这些设置，接线与
语言选择窗、`BaseInterface` 的淡入一起做（P6）。

> 2026-11-24 已接：启动读、首次运行写默认、退出与设置应用时写都进了 `GameHost`，见 §6.5；分辨率/全屏的
> **生效**也已接，见 §6.8。

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
> 的生效见 §6.8。

### 6.6 首次运行的语言选择与问候（2026-11-24）

参考 `game_mainmenu.cpp:265-300` 的 `isFirstGameRun` 分支：`selectLanguage( ..., true )` → 语言窗，欢迎消息
→ `Dialog::SelectResolution()` → `conf.resetFirstGameRun(); conf.Save()`。端口的这套流程放在主菜单场景里，用
**场景级模态**（跟设置窗、场景列表同一套输入路由），不是 `GameHost.RunModal` 的阻塞循环——这样 `--screenshot`、
`--smoke-frames` 照样能出帧和退出。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 触发 | `Game::MainMenu( isFirstGameRun )` 的参数，源头是 `conf.isFirstGameRun()` | `GameHost.ShouldStartFirstRun()`：`_configFile is not null` 且 `Settings.IsFirstGameRun`；`--no-config` 下旗标停在默认 `true` 上，若照它开窗，五张像素基线和所有脚本都会停在语言窗（ADR-202） |
| 语言窗 | `fheroes2::selectLanguage( ..., true )`，OKAY + CANCEL | `MainMenuScene.BeginFirstRun()`：`LanguageSelectionDialog` 居中，按钮取 `SettingsArtwork` 的 OKAY（帧 1–2）与 CANCEL（帧 3–4，缺帧时退回 OK）；上下键走 `MoveLanguageSelection` |
| 问候 | `Greetings!` + 欢迎段 +（触摸段）+（分辨率段） | `ShowWelcome()` 写欢迎段与分辨率段（后面真的会打开选择窗，见 §6.9）；触摸段不写，端口没有触摸输入，差异记在 ADR-202 |
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
**未做**：触摸输入的检测与问候里的触摸段（分辨率选择窗见 §6.9，`videomode` 的生效与退出时的窗口位置保存见 §6.8）。

> 2026-11-24 已接：`--left-click-at` 让一轮里连点四个位置，上面的「没验到的」由 `tools/scripts/SmokeFirstRun.ps1`
> 端到端走通（语言窗 → 问候 → 分辨率窗 → 清旗标），见 §6.11 与 ADR-209。

> 2026-11-24 已接：触摸输入进了 `LocalEvent`，问候的触摸段按 `SDL_GetTouchDevices` 的结果写（上面表格里的
> 「触摸段不写」作废），见 §6.12 与 ADR-210；带 `--no-touch` 时保持原来的两段布局。

### 6.7 会话难度：`--difficulty` 与 `Game::getDifficulty()` 的口径（2026-11-24）

参考里难度是玩家在剧本信息屏上挑的（`game_scenarioinfo.cpp` 的难度光标），进 `Game::getDifficulty()` 之后被两处
消费：`Kingdom::Init()` 发起始资源（`kingdom.cpp:103`）、`Castle::_setDefaultBuildings()` 抽二级兵营
（`castle.cpp:542`）。端口还没有剧本信息屏，所以先用命令行开关把这份选择补上，规则层不再读地图头那一档。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 入口 | 剧本信息屏的难度光标 | `--difficulty <easy|normal|hard|expert|impossible|0..4>`，`CommandLineOptions.ParseDifficulty`；会话级开关，**不写配置文件**，`GameHost.ApplySessionOptions()` 写进 `Settings` |
| 记录 | `Game::getDifficulty()` 全局 | `World.LoadMap( map, difficulty )` 存进 `World._difficulty`（参考的全局在端口没有对应物）；`Reset()` 复位成 `Normal` |
| 起始资源 | `Kingdom::Init()`（`kingdom.cpp:103`） | `CreateMapKingdoms( info, _difficulty )` → `Kingdom.GetStartingResources` 的玩家/电脑两张表 |
| 城堡默认建筑 | `Castle::_setDefaultBuildings()`（`castle.cpp:542`） | `ReadCastleBlock` 把 `_difficulty` 传给 `Castle.LoadFromMP2`：Easy 75% / Normal 50% / Hard 25% / Expert 10% / Impossible 0% 出二级兵营 |
| 地图头难度 | 只参与 `Game::GetRating()` | 同上；`LoadMap( map )` 不传时才退回地图头那一档，那是测试与工具的老口径 |

**验证**：构建 0 错误 0 警告；全量测试 5 515 通过（`TheDifficultyPassedInDecidesTheStartingResources`、
`TheDifficultyOfTheGameDecidesTheDefaultBuildingsOfATown`）；`--difficulty impossible --enter-map --screenshot`
冒烟通过，日志 `Difficulty set to Impossible on the command line.`，选中的 Alteris 地图头是 Hard，截图
`tools-output/difficulty-town.png`；非法值（`bogus`）在参数解析阶段报错并打印用法。
**未做**：剧本信息屏本身（难度光标、`NGHSBKG`、难度图标、对手/职业/评分绘制），`--difficulty` 只是过渡口子。

### 6.8 窗口状态的恢复与保存：`videomode` / `fullscreen` / 窗口位置（2026-11-24）

§6.4 之后 `videomode` 与 `game window position` 只写不读，`fullscreen` 读了不用：启动永远 2 倍窗口、永远窗口化。
参考的这份活在 `DisplayInitializer` 里（`game_init.cpp:70-99`），退出时在 `MainMenu` 关闭前记下窗口位置
（`game_mainmenu.cpp:239-244`）。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 读 | `conf.Read()` 存 `_resolutionInfo`/`_windowPos`（`settings.cpp:303-309`） | `Settings.Read`：`CurrentResolutionInfo`（默认 `640x480:640x480`）、`SavedWindowPosition`（默认 `(-1, -1)`） |
| 应用顺序 | 先全屏（`engine().isFullScreen() != conf.FullScreen()`，`:89`）、再窗口位置（`:96`）、最后分辨率（`:98`） | `GameHost.CreatePresenter()`：`--fullscreen \|\| Settings.IsFullScreen` 交给 `SdlPresenter` 构造；`SdlPresenter` 新增可选 `windowPosition`，在 `SDL.ShowWindow` 之前摆好，不会先闪一下旧位置；随后 `SetResolution` |
| 帧缓冲 | `Display::setResolution` 可以换逻辑尺寸 | 端口只画 640x480，`videomode` 的游戏尺寸不是 640x480 时打一行警告、只应用窗口那一半（ADR-204） |
| 优先级 | 无命令行开关 | `--scale` 给了就不应用文件里的窗口尺寸（本次运行的倍数压过偏好）；`--fullscreen` 只会打开全屏；都没有时窗口倍数听文件，文件缺键才回落到默认 2 倍 |
| 退出 | `MainMenu` 关闭前记窗口位置 + `conf.Save()` | `GameHost.SaveSettings()` 现场问窗口：`videomode = 640x480:当前窗口`、`game window position = 当前坐标`、`fullscreen = 当前状态`；`F11` 切换后同步 `Settings.SetFullScreen` |

**验证**：构建 0 错误 0 警告；全量测试 5 517 通过（`SystemSettingsTests` 新增两条：读写 `videomode`/窗口位置、
以及「写出去的是显示层给的现状、不改读进来的字段」）；三组冒烟（`--config <temp>`）：
`videomode = 640x480:1600x1200` + 位置 `[150, 90]` → 日志 `The window is at [ 150, 90 ] ...`、`The window is 1600x1200 ...`，
退出后文件仍写 `1600x1200` / `[ 150, 90 ]`；`videomode = 800x600:1280x960` → 警告「asks for a 800x600 frame buffer…only
the window size is applied」；`videomode = 640x480:1600x1200 --scale 3` → 文件被改写成 `640x480:1920x1440`（命令行压过文件）。
菜单与设置两张像素基线仍 `identical`。
**已知未解**：冒险地图基线 `adventure-map.png` 当时在本机不过（§7.5 已结案：三条差异都是正确行为，加 `--seed`
固定随机后已更新基线）。

> 2026-11-24 修：窗口尺寸回读后若不是帧缓冲的整数倍，画面会缩成 640x480 并留大片黑边（本机配置
> `640x480:1280x800`）。现在一律取最大等比缩放，见 §6.13 与 ADR-211。

### 6.9 首运的分辨率选择窗（2026-11-24）

§6.6 的首运只剩语言选择与问候，问候里少了「请选分辨率」那一段，因为端口没有那个窗口。这一轮补上：参考
`game_mainmenu.cpp:265-300` 在问候之后调 `Dialog::SelectResolution()`（`dialog_resolution.cpp`），端口照做。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 触发 | `showGameResolutionSelection()`，只在首运跑一次 | `MainMenuScene.CloseWelcome()` → `TryOpenResolutionPicker()`；问候关掉就开，开不出来（缺素材/没显示层/没有可用的行）就跳过这一步直接收尾，并打一行警告 |
| 窗口本身 | `Dialog::SelectResolution()`，阻塞 | 复用已转好的 `ResolutionDialog`（`Dialog::SelectResolution` 的移植，图形/系统选项页也在用），当场景级模态挂在场景上 |
| 输入 | 阻塞循环里的 `LocalEvent` | `MainMenuScene.Update` 每帧 `ProcessEvents(LocalEvent.Current)`：`ListBox` 的滚动条拖动、双击、键盘箭头全在那一套输入上，场景只在窗口打开时挡住 `SetCursor`/`SetLeftButton`/`Select`，免得点到背后的招牌 |
| 行 | `engine().getAvailableResolutions()`（游戏尺寸=窗口尺寸） | 每行是「640x480 帧缓冲 + 某窗口尺寸」（`SdlDisplayControl.GetAvailableResolutions`，ADR-205）；场景只留 `GameSize == 640x480` 的行，其余 `SetResolution` 会拒绝 |
| 选中行 | 当前分辨率所在行，找不到就停在第一行 | 同上；`ResolutionDialog` 的预选与回退有测试（`TheRowOfTheCurrentWindowStartsSelected`、`AWindowThatIsNotAModeStartsAtTheTopRow`） |
| OK | `engine().setResolution()` | `IDisplayControl.SetResolution`，随后 `FirstRunFinished` → 宿主 `SaveSettings()` 把新窗口写进 `videomode` |
| 取消 | 保持当前分辨率 | 同左，日志留痕 |
| 问候 | 分辨率段只在非手持设备上写 | 端口的桌面版一直写，触摸段仍不写（ADR-202 记的差异） |

**验证**：构建 0 错误 0 警告；全量测试 5 519 通过（`ResolutionDialogTests` 新增两条预选测试）；首运冒烟（`--config <temp>`，
`first time game run = on`、`videomode = 640x480:1280x960`，临时补丁把首运直接推进到该窗口）：日志
`First run: resolution chooser open, 13 window(s), current 1280x960.`，截图里是 `Select Game Resolution:` 加 13 行
`640 x 480 (x4.0…)`，注入 Enter 后 `First run: window resolution 2560x1600.`、文件写成 `videomode = 640x480:2560x1600`、
`first time game run` 清掉。临时补丁已撤（`git diff` 里没有 `TEMP-SMOKE` 残留）。菜单/设置/剧本三张像素基线仍 `identical`。
**顺带修正**：`SdlDisplayControl.GetAvailableResolutions()` 以前返回「游戏尺寸=窗口尺寸」的行，而端口的
`SdlPresenter.SetResolution` 只接受 640x480 的帧缓冲，图形选项页里挑分辨率会抛 `ArgumentException`；现在统一成
「帧缓冲 + 窗口」的配对，首运与图形选项页共用同一份列表（ADR-205）。
**没做**：右键的解释弹窗当时被丢掉（§6.10 已补），首运那一步的 `--left-click` 脚本点击（窗口读 `LocalEvent`，
合成点击只进场景接口）。

> 2026-11-24 已接：`--left-click-at` 在窗口开着时把点击推进 `LocalEvent`，且按下与释放分两帧（列表在释放时触发），
> 四击脚本走通首运，见 §6.11 与 ADR-209。

### 6.10 首运分辨率窗的右键解释（2026-11-24）

§6.9 的「没做」第一项。参考在 `Dialog::SelectResolution()` 的循环里对行做
`showStandardTextMessage( rowText, explanation, Dialog::ZERO )`（`dialog_resolution.cpp:124-145`、`:291-296`）；
端口的 `ResolutionDialog` 早就会把右键变成 `DialogPopup`（`TakePopup()`），只是首运那一步没人接。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 行 | `ActionListPressRight` 拼出「行文本」与缩放说明 | 同左（`GetDescription`）；`TakePopup()` 把它交出来 |
| 承载 | `showStandardTextMessage`，阻塞在窗口循环里 | `MainMenuScene.ShowResolutionTip`：`MessageBox.Create(..., DialogButton.Ok, ResolutionArtwork.Buttons)`，挂在场景上当第二层模态（`_resolutionTip`） |
| 输入 | 消息框自己转事件 | 与问候同一个消息框：`SetCursor`、`SetLeftButton`（OK）、`Select`（Enter/Esc）先让给它；`DriveResolutionPicker` 在它开着时不喂列表 |
| 绘制 | 消息框盖住窗口 | `DrawFrame` 在 `_resolutionPicker` 之后画 `_resolutionTip` |
| 按钮 | `Okay`/`Cancel` 也有右键解释 | 同左，文案同时对齐参考（`Click to apply the selected resolution.` / `Exit this menu without doing anything.`） |

**一帧只关一层**：`TryOpenResolutionPicker` 打开窗口的那一帧，关闭问候的同一个 Enter/Esc 还在 `LocalEvent` 里；
关闭解释框的那一次点击/按键也还在。`_skipResolutionInput` 让 `DriveResolutionPicker` 在「刚打开窗口」或
「刚关掉解释框」的那一帧不把输入喂给列表——否则一次 Enter 会连过「问候 → 分辨率窗」两步。

**验证**：构建 0 错误 0 警告；全量测试 5 521 通过（`ResolutionDialogTests` 新增两条：右键行的标题与正文、
当前窗口行回 `Select this game resolution.`）；首运冒烟（临时补丁：语言 → 问候 → 分辨率窗 → 右键列表行 →
释放 → 截图 → Enter 关解释框 → Enter 确认）：日志 `First run: resolution explanation '640 x 480 (x4.0)'.`，
截图是标题 + 说明 + OKAY 的框盖在列表上，随后的 Enter 只关掉解释框（分辨率窗还在），再一次 Enter 才
`First run: window resolution 1280x800.` 并收尾。补丁已撤（无 `TEMP-SMOKE` 残留）。菜单/设置/剧本三张像素基线
仍 `identical`。
**没做**：首运那一步的 `--left-click` 脚本点击（窗口读 `LocalEvent`，合成点击只进场景接口）。

> 2026-11-24 已接：见 §6.11（`--left-click-at` + `SmokeFirstRun.ps1`，ADR-209）。

### 6.11 首运的端到端脚本点击：`--left-click-at`（2026-11-24）

§6.6、§6.9、§6.10 都记着同一项没做：首运是纯点击流程，而合成点击一轮只发一次，点不到第二层窗口。这一轮补的是
**钩子**，不是场景：新增一次运行内可重放多点点击，首运从语言窗一路点到旗标清除，不再靠临时补丁。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 入口 | 无（参考的测试不做逐点驱动） | `--left-click-at <frame:x,y[;frame:x,y…]>`，`ScriptedClick`；帧 ≥1 且严格递增，`AddScriptedClicks` 在解析期报错；`--left-click` 是它的单点特例（第十帧 + `--cursor`），语义不变 |
| 重放时机 | 玩家在两帧之间产生的事件 | `GameHost.Loop`：事件批之后、场景 `Update` 之前；不在 `CreateScenes`，否则会跳过新窗口的头几帧 |
| 菜单/城镇屏 | 各自的 `SetLeftButton` 一路 | 菜单 `SetCursor` + `SetLeftButton` + `HandleMenuAction`；城镇屏 `SetCursor` + 按下/释放同帧 |
| 分辨率窗 | 阻塞循环里的 `LocalEvent` | 窗口开着时同时推进 `_le.OnMouseButton`：按下帧记 `_pendingClickRelease`，**下一帧**发释放——列表与按钮行都在释放时触发 |
| 冒烟 | 无 | `tools/scripts/SmokeFirstRun.ps1`：写临时配置（`lang = en`、`first time game run = on`），跑四击，按五个日志标记核对，非 `-KeepRunning` 时恢复配置 |
| 四击坐标 | — | `10:236,380` 语言 OKAY、`20:320,322` 问候 OKAY、`30:300,114` 分辨率列表第一行、`40:236,356` 分辨率 OKAY（取自 345x306 居中布局，布局变了要改脚本） |
| CI | — | `ci.yml` 加一步 `Walk the first run with scripted clicks`，条件 `vars.FHEROES2_DATA != ''`，跑 `SmokeFirstRun.ps1 -SkipBuild` |

**验证**：构建 0 错误 0 警告；全量测试 5 523 绿；十张像素基线一次性复验全部 `identical`；
`SmokeFirstRun.ps1 -SkipBuild` 输出 `the first run walked from the language picker to the cleared flag, match`；
`--left-click` 单点回归日志仍为 `language 'en' -> 'en'.` + `greeting shown.`；无 `TEMP-` 残留。
**没做**：问候里的触摸段（端口没有触摸输入）、隐藏界面的四个窗口位置——P6 就剩这两项。

> 2026-11-24 已接：两项都在 §6.12 补上（触摸输入与问候触摸段、拖面板落位并落盘），P6 清空，见 ADR-210。

### 6.12 触摸输入、问候的触摸段与隐藏界面面板落位（2026-11-24）

§6.11 之后 P6 剩下的两项：问候缺「两指右键」段，根子是端口没有触摸输入（§6.6 记的差异）；四个隐藏界面面板的
位置只读不写——拖面板时只画轮廓，松手丢掉，`pos_*` 永远是默认 `-1`，读取那一半等于死代码。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 手指状态 | `onTouchFingerEvent`（`localevent.cpp:1358-1430`），两根手指的 `_fingerIds` | `LocalEvent.OnTouchFinger` + `TouchFinger`/`TouchEmulation`；第三指与不在屏上的手指忽略 |
| 第一指 | 驱动光标，仿真左键 | 同左；每次仿真置 `MOUSE_TOUCH`，`IsMouseEventFromTouchpad` 真的能为真 |
| 第二指 | 第一指还在时按下 = 右键；抬起第一指不释放右键 | 同左，两指状态 `_twoFingerGestureInProgress` 照抄 |
| 返回值 | 无（按钮直接读 `LocalEvent`） | 返回 `None/Motion/Left\|Right Pressed/Released`，宿主据此调 `MoveSceneCursor`/`DispatchButton`，场景看到的与鼠标一致 |
| SDL 合成 | 关掉触摸→鼠标合成 | `SDL_Init` 后设 `MouseTouchEvents = 0`、`TouchMouseEvents = 0`，否则一次点按来两遍 |
| 坐标 | 归一化 × 显示尺寸 | 归一化 × `SdlPresenter.OutputSize` 再 `ToCursor`（视口换算） |
| 检测 | `System::isTouchInputAvailable()` | `SDL.GetTouchDevices`（托管数组）→ `MainMenuScene.TouchInputAvailable`；设备用过之前可能报告不到，与参考同 |
| 问候 | 欢迎 → 触摸（有设备时）→ 分辨率 | `WelcomeTouchId` 照抄文案，`ShowWelcome` 按同一顺序拼 |
| 布局开关 | 无 | `--no-touch`：只关检测与段落，不关手指翻译。本机有设备上报时问候变高、OKAY 下移，`SmokeFirstRun.ps1` 的固定坐标会点空（本轮实际踩到），脚本已带上它 |
| 面板落位 | 松手 `SetPos( mp.x - ox, mp.y - oy )` | `BorderDrag.Owner` + `Drop(cursor)`；`AdventureMapScene.PumpDrag` 在释放帧调，`SetPos` 夹进屏幕并 `SavePosition` 进 `Settings`，退出写配置 |

**验证**：构建 0 错误 0 警告；全量测试 5 529 通过（`LocalEventTests` +4 条触摸语义：第一指左键、移动、第二指右键
在第一指抬起后仍按住、第三指/离屏手指被忽略；`BorderWindowTests` +2 条：松手落位并保存、无主轮廓只画）；
十张像素基线一次性复验全部 `identical`；`SmokeFirstRun.ps1 -SkipBuild` 通过；不带 `--no-touch` 截的问候图是三段
（欢迎、触摸、分辨率），带开关时是两段。
**没做**：真机两指手势未实际点过（本机只报告了设备，没有手指输入）；捏合/滑动等指针手势（参考也只在未移植的
battle 里读 `IsMouseEventFromTouchpad`）；手持设备的问候/分辨率差异。

### 6.13 非整数倍窗口的缩放（2026-11-24）

用户报「启动的界面变小了，而且黑边」。查配置：`videomode = 640x480:1280x800`（§6.8 退出时写回的窗口尺寸），
而 `PresentationViewport` 只做整数倍缩放——1280/640=2、800/480=1.67，只能取 1 倍，于是 640x480 的画面缩在
1280x800 窗口中间，四周全是黑边，日志是 `x1.00 scale`。

| 项 | 旧 | 现在 |
| --- | --- | --- |
| 缩放 | 只试整数倍，取到 1 就是 1 | 取能放下的最大等比缩放：整数倍窗口结果仍是整数，其余取小数（1280x800 → 1067x800，x1.67） |
| 画面 | 640x480 居中 | 按等比铺到窗口能容纳的最大尺寸，居中信箱 |
| 点击映射 | `TryToLogical` 反算 | 目标矩形按整像素取整，远边会算出 640，改为 clamp 到 639/479 |
| 过滤 | `_logicalTarget` 用默认 | 显式 linear（整数倍时一一映射，非整数倍时比最近邻均匀） |
| 铺满 | — | **不铺满**：参考的 `SDL_RenderCopy(..., nullptr)` 会把 4:3 拉成 16:10，压扁 17%；端口固定帧缓冲，宁可留边 |

**验证**：本机日志 `x1.00` → `x1.67`；全量测试 5 535 通过（`GeometryTests` 改 4 条、新增 3 条：非整数倍取
最大等比、整数倍铺满、非整数倍点击往返 ≤1 像素、取整边沿 clamp）；十张像素基线全部 `identical`；
`SmokeFirstRun.ps1 -SkipBuild` 通过。
**没做**：窗口完全铺满（要变形，或者像参考那样让帧缓冲随窗口尺寸变、场景按任意尺寸重排——属 P7 之后）。

### 6.14 P3 起步：战斗屏幕的静态层（2026-11-24）

`PLAN.md` §5.6 的 P3 从此有一个文件：参考 `battle_interface.cpp`（6 144 行）的**绘制**那半按帧搬来了第一层，
战斗终于有画面。规则层一行没动（`BattleArena`/`BattleLoader` 早就齐了），落点是新的 `UI/Battle/` 两个文件与
`App/Scenes/BattleScene`。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 底图与边框杂物 | `_redrawBattleGround()`（`2477-2556`）：按地形挑 `CBKG*`/`FRNG*`，有树换一版 | `BattleFieldStyle.ForGround`（纯函数，九种地形 + 草/土/雪的树版）；`BattleFieldPainter` 用 `Copy` 铺底图、`Blit` 叠边框 |
| 地表装饰与障碍 | 同一函数：`arena.GetICNCovr()`、`COBJ` 的 0x90/0x9E/0x9F 走地面、0x80–0x8F 与 0x91–0x9D 走高障碍 | 照抄两张映射表，偏移都是「格子中心 + 格底 + `cellYOffset(-9)`」 |
| 攻城 | 城堡地面 `CASTBKG*` 第 1 帧、护城河 `MOATWHOL`、顶墙第 3/4 帧、城墙/塔/吊桥 `CASTLE*`、攻城车 `CATAPULT` | `RedrawCastle`/`RedrawCastleMainTower`/`DrawCastleWall`/`DrawCastleArcherTower` 逐支搬来，含 `isCellBeforeWall` 的折返规则 |
| 单位 | `RedrawArmies()` 逐行：先高障碍、再墙、再本行单位；`GetTroopPosition` 按格宽与精灵偏移算锚点，防守方镜像 | 同一套行循环；每个单位一个 `AnimationState`，取 `Static` 首帧，`Sprite.Flip(true,false)` 表达镜像 |
| 状态栏 | `TEXTBAR` 8/9 号两块 + Auto(4)/设置(6)/Skip(0) 三个按钮 | 只画底图，位置按参考的 `(0,443)`、`(0,461)`、`(49,443)`、`(49,463)`、`(591,443)` |
| 竞技场摆格子 | `battle_arena.cpp:366` 的 `board.SetArea( _interface->GetArea() )`（界面坐标系） | `BattleFieldPainter` 构造函数里 `BattleArena.Board.SetArea(0,0,640,480)` |
| 谁去打 | 英雄走到怪物格/城门格触发 `Battle::Loader` | `--battle`（第一只 `ActionMonster`）/`--battle-siege`（第一座别的颜色的城），场景一帧一个整回合；`--freeze-animation` 不推进 |
| 城堡图 | `ICN::getCastleIcnId()` → `CASTLEB/K/N/S/W/Z`（`icn.cpp:928-948`） | 一开始误用 `CSTL*`（那是 `getBuildingIcnId` 的城镇建筑图，偏移全 0），整面墙画到左上角；截图上发现后改回 `CASTLE*` |

**偏差（本步有意留的）**：六角网格（`Settings::BattleShowGrid` 的 `DrawHexagon` 未移植）、坟场
（`RedrawKilled`，开战时本来没有尸体）、兵力计数条（要 `TEXTBAR` 指示帧与位图字体）、顺序条、对手英雄立绘与
旗帜、所有动画与法术特效。人类的点选与 `IBattlePresenter.HumanTurn` 也还没接，两边都由 AI 打。

**验证**：构建 0 错误 0 警告；`dotnet test` **5 560 绿**（Core 292 / Assets 428 / Game 3 299 / UI 1 541）；`BattleFieldStyleTests` 25 条（九种地形 × 背景/边框、
草/土/雪的树版、六族城堡地面、六族战场城堡图、未知地形与未知种族、攻城沿用地形）；两张新基线
`tests/baselines/battle.png`（兽人 27、沙漠）与 `battle-siege.png`（野蛮人 Visimir、护城河与城墙）各连跑两次
`identical`，脚本 `VerifyBattleBaseline.ps1`（`-Siege` 切换）已进 CI；`--battle --smoke-frames 40` 打完整场并
回地图（`The battle is over: attacker 'Loss', defender 'Wins'`）。

### 6.15 P3 第二步：待机动画与兵力计数条（2026-11-24）

战斗屏幕上的单位开始「呼吸」了，每队脚边也有了带数字的计数条——参考 `battle_interface.cpp` 的
`IdleTroopsAnimation()`（`7199-7207`）与 `RedrawTroopCount()`（`2230-2267`）两段。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 节拍 | `Game::validateAnimationDelay( BATTLE_IDLE_DELAY )`，速度 4 约 160 毫秒（`game_delays.cpp:200`） | `GameDelays.ValidateAnimationDelay(DelayType.BattleIdleDelay)`，公式早已在 `GameDelays.UpdateGameSpeed` 里 |
| 状态机 | `Force::animateIdleUnits()`（`battle_army.cpp:232-257`）：`IDLE` 播完或定身回 `STATIC`，否则推进一帧；停在 `STATIC` 且随机延迟到点才切 `IDLE` | `BattleFieldPainter.AnimateIdleUnits` 逐字照抄；随机延迟用 `RandomizedDelay(AnimationState.IdleDelay)`（75%–125%，`Battle::Unit::_idleTimer`） |
| 状态放在哪 | `Battle::Unit::animation` 是单位成员 | **放在绘制器**（`Dictionary<uid, UnitAnimation>`）。见 ADR-213：`BattleUnit` 不拿资源，`Game` 层不引 `Assets` 的 `.BIN` 缓存 |
| 计数条 | `TEXTBAR[GetIndexIndicator(unit)]`：10 紫 / 12 绿 / 13 黄 / 14 红（按好/坏法术位挑） | 同表；位置 `rt.x + (isReflect ? -7 : rt.width - 13)`、`rt.y + rt.height - bar.height - (isReflect ? 21 : 9)`，再按 `getTroopCountOffset` 平移 |
| 数字 | `abbreviateNumber( count )`，`smallWhite`，条子正中偏下两像素 | `Army.AbbreviateNumber` + `IGlyphSource`（`GameFonts.SmallWhite` 由场景注入），`GlyphText.Measure` 居中 |
| 邻居让位 | `(monsterIndex / width) == (tileInFront == width)` 才把偏移清零 | 原样照抄（那条表达式看起来是笔误，但改动会改变像素；注释里写明） |
| 血量条 | `drawHitPointsBar`，两色由 `fheroes2::GetColorId` 从游戏调色板取 | **未做**：要调色板注入，且该设置默认关闭（`Settings` 默认选项不含 `BattleShowHitPointsBar`），随战斗设置窗一起补 |

**验证**：构建 0 错误 0 警告；两张战斗基线重建（多了计数条）并各连跑两次 `identical`；待机动画用临时日志
验证过状态机（把 `_idleTimer` 临时改成 50 毫秒、并临时绕过节拍门，120 帧内待机单位从 0 涨到 5；两处
`TEMP-` 补丁都已撤，`git diff` 里没有残留）。真实延迟是怪物 `.BIN` 里的 9000 毫秒，所以一局短战斗里
通常看不到待机动作——与参考一致。`dotnet test` 全绿（§1）。

### 6.16 P3 第三步前置：给 `BattleArena` 加逐单位推进的接缝（2026-11-24）

行动交互要等玩家点，而参考的 `Arena::Turns()` 是「一口气跑完一轮」——人类的 `Interface::HumanTurn()` 自己
带着阻塞的事件循环。端口是帧驱动的场景，不能在里面阻塞，所以先把回合拆成可以在两帧之间停下来的几步。

| 新接口 | 参考 | 说明 |
| --- | --- | --- |
| `BeginRound()` | `Turns()` 开头的 `StartNewRound()` + 两军 `NewTurn()` | 回合标志（投石车/塔各一次）也在这里清 |
| `SelectNextUnit()` | `Turns()` 里的 `_currentUnit = GetCurrentUnit()` | 返回这一手出手的单位，`null` 表示这一轮没人能动 |
| `PlayCastleActions()` | `Turns()` 里的投石车与三座塔 | 只在攻城里有事做；攻/守方第一个单位（或没人能动）时各一次 |
| `PlayCurrentUnitTurn(handler)` | `UnitTurn()` | 原样转发，AI 那一手走这里 |
| `PlayCurrentUnitTurn(commands)` | `Interface::HumanTurn()` 收齐的 `Actions` | **新增的路径**：玩家在别的帧里点好的命令直接执行，士气/行动位/随机流仍走同一条 `UnitTurn()` |
| `EndRound()` | `Turns()` 末尾的 `SettleResult()` | 把「场上没人了」翻成战果 |

`Turns()` 现在就是这套调用的循环封装，语义与之前逐字相同（`TheSteppingApiPlaysTheSameRoundAsTurns` 用同一
颗种子跑两条路，比较战果、经验与两队总兵数）。`BattleScene` 改成**一帧推进一个单位**：`--battle
--smoke-frames 400` 打完整场并回地图（日志里 `attacker 'Loss', defender 'Wins'`）。踩到并修好一处：战果要
在离开前先 `EndRound()`，否则 `SettleResult()` 没跑，日志里是「nobody won」。

**验证**：构建 0 错误 0 警告；`BattleArenaTurnTests` 新增 2 条（步进与整轮等价、玩家预备命令不需要控制器），
全量 **5 562 绿**；四张相关基线（两张战斗、冒险地图、编辑器）复验 `identical`。

### 6.17 P3 第三步（首批）：六角网格、悬停阴影、光标主题与点击下命令（2026-11-24）

战斗屏幕第一次「能点」：鼠标移到格子上会暗一格，点空格子走、点敌人格打，空格跳过。参考的
`DrawHexagon*`（`battle_interface.cpp:462-500`）、`RedrawCover()` 的悬停高亮（`:2285-2474`）、
`GetBattleCursor()`（`:2922-3064`）与 `HumanBattleTurn()`（`:3210` 起）各取了一段。

| 项 | 参考 | 端口 |
| --- | --- | --- |
| 网格 | `CMBTMISC` 第 0 帧整体下移 1 像素、顶行透明、`ReplaceColorId(0, GetColorId(0x68,0x8C,0x04))` | `EnsureHexagonGrid()` 照做；颜色走 `PALColorId.Get(26,35,1, frame.Palette)`，调色板由场景从 `ScreenBuffer.Palette` 注入 |
| 网格开关 | `Settings::BattleShowGrid()`（默认开） | `Settings.Get().IsBattleShowGrid` |
| 悬停阴影 | `DrawHexagonShadow(2,1)`，画在单位之下 | `BuildHexagonShadow(2,1)` 逐圈内缩；在 `Redraw()`, 铺完底图、画单位之前叠上，只在「有当前单位 + 开关打开」时 |
| 命中测试 | `Board::GetIndexAbsPosition()` | `BattleBoard.GetIndexAbsPosition(board, cursor)`（点不在任何六边形里答 -1） |
| 光标主题 | 走 / 飞 / 箭 / 八向剑 / 施法，按「能不能走、能不能打、从哪边打」挑 | 先分三种：可走 `CombatMove`（飞兵 `CombatFly`）、可打 `CombatArrow`、其余 `CombatPointer`；只在悬停格变化时重算（可达性查询不便宜） |
| 下命令 | `HumanBattleTurn()` 按光标拼 `BattleCommand` | 点空格子 → `BattleArena.CreateMoveCommand`；点敌人格 → `CreateAttackCommand`（贴身直接出刀，否则绕圈找最便宜的可站邻格）；空格 → `BattleCommand.Skip`，走 `PlayCurrentUnitTurn(commands)` |
| 无人值守 | `EnableAutoCombatForHumanSides()` | `--battle-auto`（隐含 `--battle`）把两边都交给电脑；不带它时人类那一栈会停下来等点击（冒烟必须带） |
| 输入来源 | 阻塞的 `LocalEvent` 循环 | 场景每帧读 `LocalEvent.Current`：`CursorPosition` 定悬停格，`MouseClickLeft(battlefield)` 定点击；`--cursor` 与新增的 `--left-click-at` 战斗转发让脚本也能点 |

**未做**：等待/防御、施法（魔法书与施法光标）、八向剑光标、撤退/投降、Auto/设置/Skip 三个按钮的行为、
路径预览箭头、状态栏文字与战斗日志、攻击/受击/移动动画。

**验证**：构建 0 错误 0 警告；`BattleArenaCommandTests` 4 条（可达格出 Move、远角不出命令、敌人格出 Attack、
友军/空格/越界不出 Attack）；全量 **5 566 绿**；两张战斗基线重建（多了网格）后各连跑两次 `identical`，
其余五张基线仍 `identical`；`--battle --cursor 320,240 --freeze-animation` 的截图里网格与悬停阴影都在；
`--battle --cursor 199,172 --left-click-at '20:199,172'` 的日志出现 `The player's Sprite acts: move to 24.`
（脚本点击经 `LocalEvent` 驱动了人类回合），不带点击时 60 帧内不会结束（在等玩家）；
`--battle --battle-auto --smoke-frames 400` 仍能打完整场并回地图。



## 7. 未完成清单（唯一列表）

### 7.1 主线

次序按 `PLAN.md` §5.2 的 P0–P8：**P0** 收桩（§6.1，**已收口**）→ **P1** 冒险层闭环（三类遭遇、冒险施法、可取对象与
宝藏点、怪物居所、设施类对象、狮身人面像与事件瓦片、上下船与海岸、军队升级对象已落，见 §6.1d–§6.1i，
**P1 收口**）→ **P2** AI 收口（`ai_hero_action` 的解耦与 B7 两个入口）→ **P3** 战斗界面四篇 → **P4** 战役 →
**P5** 高分屏与结局 → **P6** 启动缺口 → **P7** 随机地图生成器 → **P8** 编辑器。P3/P4 在 P2 之后可并行，P6 随时可插。

**P6 现状（完成）**：配置读写（§6.5）、首运语言窗/问候/分辨率窗（§6.6、§6.9、§6.10）、会话难度（§6.7）、
窗口状态恢复（§6.8）、首运脚本点击与冒烟（§6.11）、触摸输入与问候触摸段、隐藏界面面板落位（§6.12）全部已落，
P6 没有剩余项。

**P3 现状（进行中）**：静态层、待机动画、兵力计数条、逐单位推进接缝（§6.14–§6.16）与**首批行动交互**
（网格 / 悬停阴影 / 三种光标 / 点击走打 / 空格跳过，§6.17）已落地；余施法、等待防御、按钮、路径预览、
状态栏文字与动画见 §7.6。

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

语言选择窗口（M2）、难度与标准局-战役分叉（M3，难度选择已由 `--difficulty` 过渡到规则层，见 §6.7，界面未做）、
战役界面接线（M9）、SMK 视频（M9）、
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

2026-11-24 发现：`adventure-map.png` 与 HEAD（88ac029）自己的渲染不一致——本机跑 `VerifyAdventureMapBaseline.ps1`
得 165 198 像素差（视口偏移与缩放都不一样，物件层与状态栏文字缺失）。在 `git worktree` 里重建 HEAD 复现同一帧，
所以不是 §6.8 的回归，也不是启动慢（换 `--screenshot-frame 60` 也一样）。两条线索：基线可能是在别的渲染状态下
截的，或者物件层在 HEAD 上没画完。查清之前不要 `-Update`。

**2026-11-24 稍后：结案。** 用 worktree 二分：基线最后一次更新在 `11e6a51`，在该提交上重建跑同一脚本得
`identical`；到 `748904e`（「冒险场景输入三路交给 LocalEvent 并删除 `StatusPanelPainter` 与场景诊断状态行」）
就变成 139 785 像素差，`88ac029` 再变。逐项核对后的三条差异全是**正确行为**，不是回归：

| 差异 | 原因 | 判定 |
| --- | --- | --- |
| 视口不是基线那一块 | 基线时代场景自己拿「最近的城堡」`SetCenter`；现在 `AdventureSession.Start` 成功后有焦点英雄，`Interface::AdventureMap::SetFocus` 与参考 `interface_base.cpp:76-79` 一样**总是居中于英雄**，并 `SetState(STATUS_ARMY)` | 正确 |
| 雷达只有零星亮点 | `Radar` 的迷雾生效了（`748904e` 把 `_hide` 默认值与迷雾色修对），只画已探索区域；基线是全地形，那是没有雾的旧图 | 正确 |
| 状态栏没有「Month/Week/Day」 | 同第一行：`STATUS_ARMY` 时状态栏显示焦点英雄的部队（放大后是「19 精灵 + 4 兵」），日期是 `11e6a51` 场景自绘 `StatusPanelPainter` 的产物 | 正确 |

注意「物件层缺失」是误判：把当前渲染中心放大后城堡、矿、独眼巨人都在，只是视口不同。真正要修的是
**渲染的非确定性**：同一脚本连跑两次差 27～40 个像素，位置都在状态栏部队那一格——地图载入时英雄的初始部队
数量由 `Rand` 抽（OS 播种），所以两次运行的兵力不同。补丁：`Rand.SeedThread(seed)` + `--seed <n>`
（`game_init` 之前应用，`Rand.cs` 新增入口，参考没有对应物，是端口测试设施的接缝），基线脚本加 `--seed 1`。
之后 `-Update` 冒险地图基线，连跑三次 `identical`；菜单/设置/剧本三张仍 `identical`。测试 5 523（`RandTests` 新增两条）。

**同日再补一张：`quick-info.png`。** 重建基线时发现提示框根本没在图上——`--quick-info` 只在启动时开一次，
而 `AdventureMapScene.UpdateQuickInfo` 每帧按 `LocalEvent` 判「右键还按着吗」，没有按住就收掉，所以那一帧
退化成普通地图（与 `adventure-map.png` 逐字节相同）。参考的 `Dialog::QuickInfo()` 本来就是「按住期间阻塞」，
所以修的是**钩子**而不是场景：`--right-button` 现在会为冒险地图把右键按进 `LocalEvent`（菜单与城镇屏各
有 `SetRightButton`，冒险地图读事件状态，见 ADR-208），基线脚本同时带 `--quick-info` 与 `--right-button`。
重建后框回来了（「Ore Mine」，`MineText.GetMineName`），四张冒险地图基线连跑两次全部 `identical`。

### 7.6 战斗界面（P3）剩余

§6.14、§6.15 落地的是静态层、待机动画与计数条。参考 `battle_interface.cpp` 6 144 行里剩下的按
`PLAN.md` §5.6 的五步排：

1. **单位动画的其余部分**（第二步的后半）：移动/飞行分支（`_drawTroopSprite` 的位移与 `movementProgress`）、
   `RandomMonsterAnimation` 之外的受击/死亡/远程序列、`TargetInfo` 的弹道与命中动画、坟场（`RedrawKilled`）、
   血量条（`drawHitPointsBar`，要游戏调色板取两色）。
2. **行动交互的其余部分**（第三步）：等待/防御、施法（魔法书、施法光标与目标高亮）、撤退/投降、
   八向剑光标（`GetBattleCursor` 的方向分支）、Auto/设置/Skip 三个按钮的行为、路径预览箭头、
   状态栏文字与战斗日志（`Status`/`StatusListBox`）。已落的首批见 §6.17：网格、悬停阴影、三种光标、
   点击走打、空格跳过、`--battle-auto`。
3. **布阵与自动战斗**（第四步）：`battle_only.cpp` 的无人值守循环、布阵拖拽、`ToggleAutoCombat`/`QuickCombat`
   按钮行为。
4. **胜负与经验**（第五步）：`DialogBattleSummary`（`IBattlePresenter.ShowSummary` 的界面）、`FadeArena`、
   招魂/鹰眼/套装提示，以及从冒险地图走进战斗的那条链（`Heroes::Action` → `Battle::Loader`）。
5. **边框配件**：顺序条（`TurnOrder`）、对手英雄立绘与旗帜（`OpponentSprite`）、`Status` 的状态文字与
   战斗日志（`StatusListBox`）、战斗设置窗（`battle_interface_settings.cpp`）、伤害预览（`PopupDamageInfo`）、
   六角网格与闪避阴影（`DrawHexagon*`）、`battle_dialogs.cpp` 的三个窗口。

依赖都已就位：`BattleArena` 的回合/近战/施法/塔/投石车、`BattleLoader`、`LocalEvent`、`MessageBox`、
`ArmyBar`/`ItemsBar`/`SkillBars` 与新增的 `BattleFieldPainter`。

## 8. 保留的坑与教训

1. **依赖先落接口**：S12 转 `interface_gamearea.cpp` 时踩到尚未转的 `maps_tiles_render.cpp`，临时替代后来全要拆。
2. **两套坐标必然打架**：自建 `AdventureMapView` 与 `GameArea` 并存期间滚动与绘制各推一套，表现是
   「滚了但画没动」「英雄不在视野」。参考里只有一个位置的量（视口中心、焦点），端口也只能有一个。
3. **自建绘制会静默失真**：`StatusPanelPainter` 少画当前玩家的英雄，就是「自己拼」的典型症状。
4. **`.h` 里的内联函数也是移植对象**：G1–G5 里几次漏项都出在头文件的 inline 方法上。
5. **冒烟模式没有光标**：依赖鼠标位置的逻辑在 `--smoke-frames` 下都看到 `(0,0)`，排查画面问题前先排除它。
6. **基线漂移要先定性再定量**：139 753 这个数字长期被当回归，实际是「基线来自自建渲染」；2026-11-24 用 worktree
   二分才定性（§7.5 结案），顺带挖出渲染的非确定性（英雄初始部队没固定种子）。
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
