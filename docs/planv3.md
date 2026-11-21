# AI 实现计划（v3）

> 定位：`docs/PLAN.md` 是总账，`docs/PROGRESS.md` 是流水。AI 这九千多行要分二十几次提交，
> 批次、前置与验收单独记在这里，总账只留一行指针。
>
> 契约基线：ADR-161（签名）与 ADR-180（`BattlePlanner` 的私有半、`AIWorldPathfinderStateRestorer`）。
> 契约在 `src/FHeroes2.Game/Ai/` 与 `src/FHeroes2.Game/World/AIWorldPathfinder.cs`，每批收尾记一条 ADR，
> 编号从 195 起接续（B4 用 ADR-195，X1 用 ADR-196，B5 起 197）。

## 0. 基线（2026-11-21 实测）

| 参考文件 | 行数 | 端口 | 契约 | 未实现 |
| --- | --- | --- | --- | --- |
| `ai_personality.cpp` | 45 | `System/AiPersonality.cs` | 已整份 | 0 |
| `ai_common.cpp` | 525 | `Ai/AiCommon.cs`（123） | 9 个自由函数 | 0（B1） |
| `ai_planner.cpp` | 188 | `Ai/AiPlanner.cs` | 5 个短函数（`Get` 已实现） | 0（B2 做 4 个，B4 做 `revealFog`） |
| `ai_planner_castle.cpp` | 536 | 同上 | 4 个成员 | 0（B3） |
| `ai_planner_kingdom.cpp` | 987 | 同上 | 11 个成员 | 1（B4 做 10 个与扫描段，`KingdomTurn` 的回合循环等 B5） |
| `ai_planner_hero.cpp` | 3 309 | 同上 | 16 个成员 | 16 |
| `ai_battle.cpp` | 2 152 | `Ai/AiBattlePlanner.cs`（418） | `BattlePlanner` 全类 + `GetBerserkerTarget` | 24 |
| `ai_battle_spell.cpp` | 984 | 同上 | 15 个法术估值 | 15 |
| `ai_hero_action.cpp` | 2 504 | `Ai/AiHeroAction.cs`（53） | 4 个入口 | 4 |

九个文件 11 230 行（`ai_personality.cpp` 的 45 行算在内）。基线 88 处 `NotImplementedException`
（AI 目录 75 处，`AIWorldPathfinder` 13 处），覆盖率口径 1/0/8。B1..B4 与 X1 之后剩 47 处（全在 AI 目录，
`AIWorldPathfinder` 0），覆盖率 4/1/4。

**契约已经先行**，这一步不是待办：39 个 `Planner` 成员、24 个 `BattlePlanner` 成员、9 个自由函数、
4 个入口、2 个结构族与 4 个阈值都在，`AiContractTests`（169 行）钉住数据结构默认值、单例、
`BudgetEntry.Reset`、`SpellcastOutcome.UpdateOutcome` 与「未移植会抛」。本文件只排**实现**。

## 1. 分批原则

1. **一个 `NotImplementedException` 是一个原子任务**。批次边界按「能被同一组测试钉住」切，不按文件强切。
2. **禁止静默兜底**。没实现就继续抛，不许返回 0 / `null` / 空集合——契约测试靠它区分真假。
3. **每批必须同时交付行为测试**（适用于 B1..B8 这类分批模式）。自 2026-11-21 的「整目录推进」起，
   第一遍只要求转完 + 0 警告，行为测试与逐函数核对统一放到全部目录转完之后补齐。
4. **前置不在 AI 目录的，先做前置批（X）**，不许在 AI 里写临时替代。
5. **后续按目录整块推进**：细则见 `PLAN.md` 总体原则第 6 条——一个参考目录转完再换下一个，
   缺依赖就先在对方目录落接口（签名 + 文档，方法体照旧抛），`editor/` 放最后。

## 2. 批次总表

| 批次 | 范围 | 数量 | 前置 | 完成后 `ai` 行 |
| --- | --- | --- | --- | --- |
| B1 | `ai_common.cpp` 九个函数 | 9 | 无（`Maps.GetFogTileCountToBeRevealed` 随批补） | 2/0/7 |
| B2 | `ai_planner.cpp` 五个短函数 | 5 | B1 | 2/1/6 |
| B3 | `ai_planner_castle.cpp` 四个成员 | 4 | B2 | 3/1/5 |
| B4 | `ai_planner_kingdom.cpp` 十一个成员 + `revealFog` | 11 + 1 | B3 | 4/1/4 |
| B5 | `ai_planner_hero.cpp` 十六个成员 | 16 | B4 + **X1 + X2** | 3/3/3 |
| B6 | `ai_battle.cpp` + `ai_battle_spell.cpp` | 39 | **X3** | 3/5/1 |
| B7 | `ai_hero_action.cpp` 四个入口 | 4 | **X4** | 3/6/0 |
| X1 ✅ | `AIWorldPathfinder` 六个方法（ADR-196） | 6 | Hero 读宝物袋、瓦片对象扫描 | — |
| X2 | `ObjectValueStorage` 与对象价值表 | — | `world_pathfinding` 遗留项 | — |
| X3 | `battle_action` 的法术目标与生效 | 10 | `battle_arena`/`battle_action` 收尾 | — |
| X4 | `heroes_action` 补全 + 对象交互 + Interface 解耦 | — | 冒险层 | — |

B1→B2→B3→B4 串行，X1 已并入；B5 等 X2；B6、B7 与上面串行链无关，可并行，但各自等 X3/X4。

## 3. 批次细节

### B1 `ai_common.cpp`（9）

`BuildIfPossible`(57) / `BuildIfEnoughFunds`(81) / `OptimizeTroopsOrder`(102) /
`TransferSlowestTroopsToGarrison`(206) / `CalculateMarketplaceTransaction`(249) / `TradeAtMarketplace`(368) /
`ShareObjectVisitInfoWithAllies`(389) / `IsUltimateArtifactAvailableToHero`(418) /
`IsValuableAdventureMapObject`(438)。

* 需要的 API 全在：`Castle.IsBuilding`/`BuyBuilding`/`GetRace`/`CheckBuyBuilding`、`Kingdom.GetFunds`/
  `OddFundsResource`/`AllowPayment`/`GetCountMarketplace`/`IsVisited`/`IsTravellerTentVisited`、
  `Kingdom.ResourceTrading.GetTradeCost`（`Game/Kingdom/ResourceTrading.cs:74`）、`UltimateArtifact`。
* **随批补一处**：观察塔分支要 `Maps.GetFogTileCountToBeRevealed`（`ai_common.cpp:492`），端口没有，
  按 `maps_tiles_helper.cpp` 的视野距离实现，约 30 行。
* 测试 `AiCommonTests`：九个函数各一组——建筑买得起/买不起/差资源可换的三种路径、
  换兵后的兵力单调、市场交易扣款后仍够付、盟友共享只对 AI 生效、观察塔在雾已开时返回 `false`。
* **2026-11-21 完成**，19 条测试。两处端口偏差（都在注释里写明）：
  * `TransferSlowestTroopsToGarrison` 对 `castle == null` 直接返回，参考是 `assert`；
  * 同一函数照参考「清空原槽位」而不是压缩数组，英雄军队里最快的栈留在原槽位，测试按「清空」断言。
  * `CountFoggedAround` 落在 `MapFog`（`AdventureMap/MapFog.cs`），按参考的平方半径算 9/21/8。

### B2 `ai_planner.cpp`（5）

`getTileArmyStrength` / `revealFog` / `getResourcePriorityModifier` / `getFundsValueBasedOnPriority` /
`updateMapActionObjectCache`。

* 都是短函数：第一个走 `Army.SetFromTile` 的估值缓存（`_tileArmyStrengthValues`），
  第二个调 `Kingdom` 的揭雾，后两个是资源权重表，最后一个是 `_mapActionObjects` 的遍历填充。
* `getTileArmyStrength` 的缓存是「同一回合内不变」的前提，测试要覆盖第二次调用不重算。
* 测试 `AiPlannerTests`。
* **2026-11-21 完成 4/5**，`AiPlannerTests` 4 条。三处要注意：
  * `revealFog` **挪到 B4**：它调 `updatePriorityAttackTarget`（`ai_planner_kingdom.cpp`），自己不含独立逻辑，
    留着抛反而让「抛」这件事有唯一解释——B4 一起做。
  * `getResourcePriorityModifier` / `getFundsValueBasedOnPriority` 改成 `internal`（参考是 private）：
    它们的调用者 `getObjectValue` 系列还在 B5，测试没有别的入口。
  * 缓存按 `Tile.Index` 落键，而 `World.GenerateUninitializedMap` 把每个瓦片的索引都留成 0（只有加载地图时
    才编号），所以测试世界自己要先把索引写好，否则 64 个瓦片共用一条缓存。
  * 资源权重表是静态数据：矿按日收入的倒数乘 2（金矿 2、木/矿 1000、稀有 2000），堆按常规堆量
    （金 1、木/矿 125、稀有 250）。`_budget` 的 `recurringCost`/`priority` 缩放已经按参考写好，等 B3
    的 `updateKingdomBudget` 来填 `_budget` 才有效。

### B3 `ai_planner_castle.cpp`（4）

`CastleTurn`(516) / `reinforceCastle`(314) / `updateKingdomBudget`(270) / `CastlePreBattle`(252)。

* `CastlePreBattle` 只做 `world.GetHero(castle)` + `Army.ArrangeForCastleDefense` + `OptimizeTroopsOrder`，
  **不碰 `BattlePlanner`**，所以不拖到 B6。
* `updateKingdomBudget` 依赖 `CheckBuyBuilding` 与 `PaymentConditions.BuyBuilding`（端口都有）。
* 测试 `AiCastlePlannerTests`：建造顺序里的优先级、差资源时 `_budget` 的 `priority` 位、
  守军与访客英雄的兵员搬运方向。
* **2026-11-21 完成 4/4**，`AiCastlePlannerTests` 10 条。六处要注意：
  * `CastleTurn` / `reinforceCastle` / `updateKingdomBudget` 改成 `internal`（参考是 public/private 混合，
    端口统一按「同回合的成员，测试要能单独驱动」处理）；`CastlePreBattle` 原本就是 `internal static`。
  * `CastleTurn` 的**非防守分支**要读 `_regions`（`evaluateRegionSafety` 的产物，B4 才填），
    本批按参考写死，只测防守分支；测试注释里写明了这条依赖。
  * `reinforceCastle` 结尾的「安全城堡留半队守军」同样读 `_regions`，端口把 `!garrison.IsValid` 提到
    区域查找之前——语义等价（都是无副作用的读），但守军还在时半移植状态下不会越界。
  * 建造顺序六张表（generic/barbarian/sorceress/knight/necromancer/wizard）逐项照抄，含 knight 的
    5 号住所降权、wizard 的图书馆优先；`Build` 一次只买一个建筑（`BuyBuilding` 会清当天建造许可），
    所以防守回合的 `Build(DefensiveStructures)` 买到第一座塔后，`Build(SupportingDefensiveStructures)` 当日无货。
  * 难度开关落地成 `Difficulty.AllowAiToDevelopCastlesOnDay` / `AllowAiToBuildCastleBuilding`
    （`difficulty.cpp:313/325`）；英雄角色用新枚举 `HeroRole` + `Hero.AiRole`（`heroes.h:217-241`）。
  * `updateKingdomBudget` 的「最强兵种」按参考取 6 号住所的**基础**兵种再 `getUpgrade()`，
    本批用 `DwellingMonsters.FromDwelling` + `MonsterUpgrades.GetUpgrade` 拼出来。

### B4 `ai_planner_kingdom.cpp`（11 + `revealFog`）

`KingdomTurn` / `getSortedCastleList` / `evaluateRegionSafety` / `findCastlesInDanger` /
`purchaseNewHeroes` / `recruitHero` / `updatePriorityForEnemyArmy` / `updatePriorityForCastle` /
`updateIndividualPriorityForCastle` / `removePriorityAttackTarget` / `updatePriorityAttackTarget`，
外加从 B2 挪过来的 `revealFog`（它只调 `updatePriorityAttackTarget` 与 `updateMapActionObjectCache`）。

* 对 `AIWorldPathfinder` 只用 `GetDistance`/`SetMinimalArmyStrengthAdvantage`/`SetSpellPointsReserveRatio`
  与 `AIWorldPathfinderStateRestorer`，三者都已实现，**不卡 X1**。
* `KingdomTurn` 的末尾会调 `HeroesTurn`（B5）：B4 落地时这一段保持抛，`KingdomTurn` 因此记「部分」，
  等 B5 一起收。
* 测试 `AiKingdomPlannerTests`：城堡排序（受威胁优先）、危险判定（敌方兵力比阈值）、
  预算与招募的联动、优先级目标的增删。
* **2026-11-21 完成 10/11 + `revealFog`**，`AiKingdomPlannerTests` 8 条。五处要注意：
  * **`KingdomTurn` 整段留到 B5**：它的循环第一句就是 `HeroesActionComplete`（`ai_planner_hero.cpp`），
    只有它和 `HeroesTurn` 都落地后这个循环才有意义，所以本批只把扫描段抽成 `ScanMap`（`internal`，
    返回 `availableHeroCount`，顺带调 `EvaluateRegionSafety`/`UpdateKingdomBudget`），其余十个成员全部落地。
  * **扫描按 ADR-133 折叠对象类型**：瓦片存的是带动作位的值（英雄是 `ActionHero`），
    `GetEnemyArmyOnTile` 原来直接比 `Mp2ObjectType.Hero` 永远不成立，本批改成先过
    `Mp2ObjectTypes.NonActionType`；`_mapActionObjects` 仍存瓦片的原值，与 `updateMapActionObjectCache` 一致。
  * **看全图（`View All`）未移植**：端口还没有冒险地图施法，扫描只认「不在雾里」的瓦片，
    参考里那个 `bestHeroToViewAll` 也不存在；`ResetModes(SLEEPER)` 与 `setDimensionDoorUsage(0)` 同理
    （端口没有这两项状态），注释里写明。
  * **`_regions` 加了只读窗口 `Regions`**：`evaluateRegionSafety` 的结果只能从 `CastleTurn`/`GetSortedCastleList`
    间接观察，测试要断言它就需要直接读；`SetHeroRoles`/`GetSortedCastleList`/`PurchaseNewHeroes` 同样由
    private 改 `internal`。
  * **合成世界没有城堡表**：`world.GetCastleEntrance` 只在装图时登记，生成的空地图里填不出「友方城堡 = +100」
    这一支，所以测试只覆盖「敌方英雄 → 区域 -50 → 邻区取份额」与岛屿 ×1.5（`ScanMap` 只数得到敌人）。

### B5 `ai_planner_hero.cpp`（16）

`HeroesBeginMovement` / `HeroesActionNewPosition` / `HeroesActionComplete` / `HeroesTurn` /
`isValidHeroObject` / `getObjectValue` / `getFutureObjectValue` / `getPriorityTarget` /
`getGeneralObjectValue` / `getFighterObjectValue` / `getCourierObjectValue` / `getScoutObjectValue` /
`getCourierMainTarget` / `updatePriorityTargets` / `pickSecondarySkill` / `HeroesPreBattle`。

* 前置：`AIWorldPathfinder` 的六个英雄入口（X1，已完成）与对象价值（X2），**这是 AI 真正可用的门槛**。
* `HeroesPreBattle` 要 `OptimizeTroopsOrder` + `BattlePlanner` 无关（只排兵），可直接落。
* 测试 `AiHeroPlannerTests`：四种角色（战士/信使/侦察/通用）对同一张对象表的估值差异、
  距离惩罚、优先级目标的选择与失效。

### B6 `ai_battle.cpp` + `ai_battle_spell.cpp`（39）

`BattleTurn` / `PlanUnitTurn` / `AnalyzeBattleState` / `ArcherDecision` / `MeleeUnitOffense` /
`MeleeUnitDefense` / `IsPositionLocatedInDefendedArea` / `SelectBestSpell` / 九个 `Spell*Value` /
`SpellEffectValue` 两个重载 / 三个 `GetSpell*Ratio` / `SpellDurationMultiplier` /
`IsSpellcastUselessForUnit` / `GetMeleeBestOutcome` / `GetBerserkerTarget` / `IsLimitOfTurnsExceeded`。

* 前置 X3：`BattleArena.GetTargetsForSpell` 等十个查询（见 §4）。
* 签名差异已定（ADR-180）：`Battle::Actions` → `List<BattleCommand>`，AI 一次算一整回合。
* 测试 `AiBattlePlannerTests`：射手决策、近战攻/守的选择、法术估值（群体累加 vs 单体取优）、
  狂暴目标、无死亡回合上限 50 的截断。

### B7 `ai_hero_action.cpp`（4）

`HeroesAction` / `HeroesMove` / `HeroesCastDimensionDoor` / `HeroesCastSummonBoat`。

* 前置 X4，且参考里 34 处 `Interface::AdventureMap::Get()`（居中、`setRedraw`、雾方向）要改成事件或回调，
  这是唯一带架构性的改动，**先定接口再写分支**。
* 测试 `AiHeroActionTests`：拾取资源/访问建筑/招降守卫三种对象分支、召船返回旧船位、
  次元门的落点校验。

## 4. 外部前置批

### X1 `AIWorldPathfinder` 的六方法 ✅（ADR-196）

`World/AIWorldPathfinder.cs` 里原本抛异常的六个：`ReEvaluateIfNeeded(Hero, strength)`、
`GetFogDiscoveryTile`、`GetNearestTileToMove`、`IsHeroPossiblyBlockingWay`、`BuildDimensionDoorPath`、
`GetObjectsOnTheWay`，**2026-11-21 全部落地**。

* 单参 `ReEvaluateIfNeeded(Hero)` 是参考的原形（军力由 `hero.Army.GetStrength()` 自己算），双参版留作测试与
  已知军力的调用方；上下文比对含召船/次元门/城门户三项缓存字段。
* 迷雾探索两遍扫描（相邻雾瓦 → 相邻不可达瓦），候选必须 `IsSafeForFogDiscoveryObject`；返回值第二项的含义
  是「雾就在旁边」（参考的 territory expansion），端口之前误名 `IsBoat`，本批改正。
* `IsHeroPossiblyBlockingWay` 十一个几何模式 + 石碑出口/踩碑两支（ADR-133 折叠后再比）。
* `BuildDimensionDoorPath` 受法术点储备、移动力与 `Difficulty.GetDimensionDoorLimitForAi` 三重限制，
  每次跳 `Spell.DimensionDoorDistance() / 2`；返回的是固定跳段，最后一小段由调用方用普通路径补齐。
* 随批补：`Hero.CanCastSpell`、`Hero.DimensionDoorUses`（`heroes.h:692`）、`Difficulty.GetDimensionDoorLimitForAi`。
* 遗留：`MapFog` 与 `Tile._fogColors` 仍是两套迷雾状态（`World.ClearFog` 只写前者，路径器只读后者），
  本批按参考把 `GetFogDiscoveryTile` 写成「瓦片雾判相邻、`MapFog` 数可揭示格」，测试同时写两张表；合并留作后续 ADR。

### X2 `ObjectValueStorage`

`world_pathfinding` 的遗留项，要一张物件价值表（`ObjectValueStorage` 在端口 0 处）。
`Planner::getObjectValue`/`getFutureObjectValue`/`getPriorityTarget` 全靠它兜底，解锁 B5。

### X3 `battle_action` 的法术目标与生效

`BattleArena` 缺的十个查询，参考实现分布得很散：

| 查询 | 参考位置 |
| --- | --- |
| `GetTargetsForSpell` | `battle_action.cpp:1046`（唯一在 `battle_action` 里的） |
| `CanSurrenderOpponent` / `CanRetreatOpponent` | `battle_arena.cpp:899` / `906` |
| `IsSpellcastDisabled` / `IsDisableCastSpell` | `battle_arena.cpp:912` / `927` |
| `IsAbleToResurrectFromGraveyard` | `battle_arena.cpp:1011` |
| `GetLastResurrectableUnitFromGraveyard` | `battle_arena.cpp:1059` / `1079`（两个重载） |
| `GetTurnNumber` / `GetCellsOccupiedByGraveyard` | 头文件内联（`.cpp` 里没有定义） |

也就是说这批的主战场是 `battle_arena`，`battle_action` 只欠 `GetTargetsForSpell` 一支；
但 `GetTargetsForSpell` 要 `TargetsInfo` 与法术生效路径，所以两处得一起收。

AI 不碰 `battle_interface`（0 引用），只要求 `Arena` 的查询齐全，所以这批做完 B6 即可落地。

### X4 `heroes_action` 补全 + 对象交互 + Interface 解耦

* `HeroActions.cs` 2 164/2 504 行，缺拾取、访问、交互几支；
* `ActionToHeroes` 级别的对象分派在端口 0 引用（`maps_tiles_helper` 侧）；
* 参考 `ai_hero_action.cpp` 的 34 处 `Interface::AdventureMap::Get()` 需要端口先有事件/回调。

## 5. 已知契约偏差（随批次修）

| # | 位置 | 偏差 | 处理 |
| --- | --- | --- | --- |
| 1 | `AiPlanner.cs` 的异常消息 | `RecruitHero` 写 castle，参考在 `ai_planner_kingdom.cpp:273` | B4 已落地，占位与消息一并删除 |
| 2 | 同上 | `PurchaseNewHeroes` 写 castle，参考在 `ai_planner_kingdom.cpp` | 同上 |
| 3 | `BattlePlanner.BattleTurn` | `List<BattleCommand>` 代替 `Battle::Actions` | 已文档化（ADR-180），不动 |
| 4 | `AiPlanner._pathfinder` | 可空 + `AttachPathfinder`，参考是值成员 | 已文档化（ADR-161），不动 |

## 6. 记账规则

* 每批收尾：`dotnet test` 全绿（新增测试类进 `tests/FHeroes2.Game.Tests/`）→ 改总账 `ai` 行与条目
  → `docs/progressv3.md` 记一行（批次、ADR、行数、测试数、遗留）→ `DECISIONS.md` 一条 ADR。
* 覆盖率只在**整份文件的方法全部落地**时把「部分」改「✅」；`KingdomTurn` 这种跨文件的，
  在两条都落地后才改。
