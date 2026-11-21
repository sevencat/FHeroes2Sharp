# AI 实现进度（v3）

计划见 `docs/planv3.md`。本文件只记「哪一批做完了」与偏差，不重复计划内容。

## 状态总表

| 批次 | 范围 | 状态 | ADR | 测试 | 日期 |
| --- | --- | --- | --- | --- | --- |
| B0 | 契约（签名、数据模型、契约测试） | ✅ | ADR-161 / ADR-180 | `AiContractTests` 169 行 | 2026-11-17 |
| B1 | `ai_common.cpp` 九个函数 | ✅ | — | `AiCommonTests` 19 条 | 2026-11-21 |
| B2 | `ai_planner.cpp` 五个短函数 | ✅ 5/5（`revealFog` 随 B4 收尾） | — | `AiPlannerTests` 4 条 | 2026-11-21 |
| B3 | `ai_planner_castle.cpp` 四个成员 | ✅ 4/4 | — | `AiCastlePlannerTests` 10 条 | 2026-11-21 |
| B4 | `ai_planner_kingdom.cpp` 十一 + 一个成员 | ⚠️ 10/11（`KingdomTurn` 的回合循环归 B5）+ `revealFog` | ADR-195 | `AiKingdomPlannerTests` 8 条 | 2026-11-21 |
| B5 | `ai_planner_hero.cpp` 十六个成员 | ✅ 16/16 | — | `AiHeroPlannerTests` 6 条 | 2026-11-21 |
| B6 | `ai_battle.cpp` 25 个决策函数 + `ai_battle_spell.cpp` 全部估值函数 | ✅ 25/25 + 15 个成员 + 1 个辅助 | — | `BattleUnitThreatTests` 10 条 | 2026-11-21 |
| B8 | 战斗主循环（`battle_arena.cpp` 的回合循环 + `battle_action.cpp` 的近战结算 + `Battle::Loader`） | ⚠️ 回合循环 `Turns()` 与近战攻击已落（含反击、幸运、伤害/击杀统计）；剩施法行动、塔与投石车、以及 `BattleLoader` 三处正文 | — | `BattleArenaTurnTests` 8 条 | 2026-11-21 |
| B7 | `ai_hero_action.cpp` 四个入口 | ⚠️ 2/4（次元门、召船；`HeroesAction`/`HeroesMove` 卡在战斗启动层与对象动作表） | — | `AiHeroActionTests` | — |
| X1 | `AIWorldPathfinder` 六个方法 | ✅ 6/6 | ADR-196 | `AIWorldPathfinderTests` 26 条（增补 12） | 2026-11-21 |
| X2 | `ObjectValueStorage` 与对象价值表 | ✅ 随 B5（`AiPlanner.Hero.cs` 的 `ObjectValidator`/`ObjectValueStorage` 两个嵌套类） | — | — | 2026-11-21 |
| X3 | `battle_action` 法术目标与生效 | ❌ 未开始 | — | — | — |
| X4 | `heroes_action` + 对象交互 + Interface 解耦 | ❌ 未开始 | — | — | — |

覆盖率基线（`docs/PLAN.md` 的 `ai` 行）：**6/0/3**，剩余 5 处 `NotImplementedException`：
`AiHeroAction` 2（`HeroesAction`/`HeroesMove`，卡战斗启动层与对象动作表）、`BattleLoader` 3（B8 契约占位，
等施法行动与 `Battle::Loader` 正文）。`AiBattlePlanner` 的 25 处已全部清零。

## 流水

### 2026-11-21 B8 第一批：战斗回合循环 + 近战攻击结算

④ 批把「一条命令怎么走完一场战斗」打通，`BattleArena` 因此能自己跑到结束：

* `BattleArena.Turns.cs`（新）：`Turns()` 的回合骨架照 `battle_arena.cpp` 的 `Arena::Turns()`——每轮按速度排序
  取仍未行动的单位，调 `UnitTurn`；`UnitTurn` 里的 `actions` 队列照参考的 `std::vector<Command>`，处理
  「同一单位的多段命令」（如战后好士气补一次行动、跳过、撤退），并在每个动作后 `RemoveDeadUnits()` 再查
  `BattleValid()`。**同速先手**用新增的 `_lastActiveUnitArmyColor`（参考 `Arena::_lastActiveUnitArmyColor`）。
* `BattleArena.Attack.cs`（新）：`BattleProcess`（`battle_action.cpp:143-278`）、`ApplyActionAttack`
  （`:481-630`）、`TargetsApplyDamage`（`:856-955`）、`ApplyDamageInDirection` 与
  `GetAttackDirectionOfBlow`/`GetDirectionOfAttackNearestToUnit` 等辅助，全部按参考落；伤害、击杀数、
  幸运（`SetRandomLuck`）、反击（`IsRetaliationAllowed`/`SetRetaliationCompleted`）、
  `NoEnemyRetaliation`、宽体双格与倒影方向都在内。
* `BattleArena.Action.cs`：`ApplyAction` 增 `BattleCommandType.Attack` 分支；默认分支只列剩下的施法/塔/投石车/
  撤退/投降/自动战斗/快速战斗。
* `BattleUnit`：补 `IsIgnoringRetaliation`、`SetRetaliationCompleted`（参考 `isIgnoringRetaliation()` /
  `setRetaliationAsCompleted()`）。
* `BattleCommand.Attack` 的方向参数由 `AttackDirection` 改回参考的 `int`（默认 `-1`）：八向 `AttackDirection`
  与六向 `CellDirection` 数值不同，混用会把「自动定方向」读成具体方向。AI 两处调用点随之改为 `(int)`/`-1`。
* `BattleForce.ApplyToTroopsFromOriginalArmy`：不再按 `troop.IsValid` 过滤。参考的战斗单位是原始兵堆的副本，
  端口直接持有 `GameArmy` 的 `Troop`，一开打原始人数就被改小；照抄过滤会把全灭兵堆当成「没上场」，
  使 `CalculateExperienceBasedOnLosses` 少算经验。判据改为「有没有对应的战斗单位」。

零警告、全套 4554 测试全绿（新增 `BattleArenaTurnTests` 8 条：回合轮转、攻击扣血、死亡清理、射手对射跑到结束）。

### 2026-11-21 B6 收尾：`ai_battle.cpp` 25/25 + `ai_battle_spell.cpp` 全部估值函数

③ 批把 `ai_battle.cpp` 剩下的决策函数与整套法术估值落完，`AiBattlePlanner` 的 25 处占位清零：

* `meleeUnitOffense`（`ai_battle.cpp:1594-1668`）：能找到人就打，找不到就退到「够不着」的位置；
  打不着会还手的最后一堆时优先「不能反击」的落点。
* `meleeUnitDefense`（`:1764-1884`）四段：先救被贴住的射手（`FindBestCoverCell` 按左→右、下→上的格序
  贪心取最优单元格），再守阵地（城内看 `IsCastleIndex`、野外看距边不超过半宽的「阵地」判据，
  `avoidStackingUnits` 时把待救单位挤到阵地外一格）。
* `findBestCoverCell`/`isPositionLocatedInDefendedArea`/`getBerserkerTarget`（`:2128-2189`、`AI::getBerserkerTarget`）。
* `BattleArena.GetTargetsForSpell`（`battle_action.cpp:1046-1174`）连同 `TargetsForChainLightning`
  （`:1014-1044`）与 `FindChainLightningTargetUnits`（`:960-1011`）：链闪电先滚抗魔再跳四跳，
  **选目标时已滚过抗魔，故返回后不再重复滚**（用 `applyRandomMagicResistance = false` 承接）。
* `ai_battle_spell.cpp` 十三个估值函数：`selectBestSpell`（:70-158，按法力折算 + 阈值 0.04·我方²/敌方、
  敌方射手过半 ×0.5、法力过半 ×2）、`spellDamageValue`（:157-285，整队打掉时加 `armyStrength×0.035/0.07`、
  镜像归并、定身单位「打醒」折价）、`spellEffectValue`（:378-580）、`spellDispelValue`（:597-645）、
  `spellResurrectValue`（:648-693）、`spellSummonValue`（:696-721）、`spellDragonSlayerValue`（:724-773）、
  `spellTeleportValue`（:869-932，临时挂 `TELEPORT_ABILITY` 重算最佳攻击目标，`try/finally` 保证撤掉）、
  `spellEarthquakeValue`（:935-990）、`getSpellDisruptingRayRatio`（:297-328）、`getSpellSlowRatio`（:329-355）、
  `getSpellHasteRatio`（:356-377）、`spellDurationMultiplier`（:285-296）、`isSpellcastUselessForUnit`（:776-866）。
* 副作用：`BattleArena.IsAbleToResurrectFromGraveyard`/`GetLastResurrectableUnitFromGraveyard` 由私有转公开
  （参考本就是 public，AI 与施法流程共用）；`SpellDispelValue`/`SpellEffectValue` 增加 `BattleArena` 参数，
  因为参考从全局 `Battle::GetArena()` 取，而端口不设全局；`BattleTactics.ReduceEffectivenessByDistance` 补入。

零警告、全套 4546 测试全绿。

### 2026-11-21 B8 接口 + B6 第二批地基：战斗启动器契约与 AI 所需的战斗几何

**B8（战斗启动层）只落接口，不落正文**，因为正文的三处前置都还空着（`BattleArena.Turns()`、
`ApplyAction` 的攻击/施法分支、`Battle::Only` 的自动战斗）。新文件 `src/FHeroes2.Game/Battle/BattleLoader.cs`：

* `BattleLoadRequest`：`World`、两军 `Army`、`TileIndex`、`Castle`、`AutoResolve`、`Presenter` 六个必填/可选字段，
  替代参考从全局取的 `Settings`/`world.GetMapSeed()`/`Army::GetCommander()`。
* `IBattlePresenter`：五个回调，逐个对应参考里的对话框调用点（`FadeArena`、`DialogBattleSummary`、
  `DialogBattleNecromancy`、`ArtifactSetAssembled`、鹰眼学法术），Game 层不碰 UI。
* `BattleLoader.Load` / `RunToEnd` / `ComputeBattleSeed` 三个成员：XML doc 写明参考四段流程（校验→战前→
  主循环→收尾）与端口缺口，正文一律 `NotImplementedException("… — battle_main.cpp:291-535 未移植（ADR-161）")`。

**B6 第二批地基**：把 `ai_battle.cpp` 用到的战斗几何补齐——`BattleBoard.CanAttackFromCell`（护城河规则）、
`CanAttackTargetFromPosition`（头尾两格试探）、`GetAroundIndexes(BattlePosition)`（顺时针并集，顺序影响
AI 选格确定性的那一条）、`GetDistanceIndexes(BattlePosition, radius)`、`GetNearestTroops`（按距离排序、
只看头格、跳过黑名单）；`BattleArena` 的 `CanRetreatOpponent`/`CanSurrenderOpponent`（人类专属选项）、
`AutoCombatInProgress`/`CanToggleAutoCombat`/`IsSpellcastDisabled`（自动战斗未接入，恒 `false`，注释写明等
B8 正文）；`BattleUnit.IsDoubleCellAttack`/`IsAllAdjacentCellsAttack`/`MissingHitPoints`。
全套测试 4546 仍全绿，`dotnet build` 0 警告。

**B6 主体（25 个决策函数）为何没有一次写完**：`ai_battle.cpp` 2152 行 + `ai_battle_spell.cpp` 984 行，
且互为一体（`planUnitTurn` 调 `selectBestSpell`，`meleeUnitOffense` 调 `getMeleeBestOutcome`，后者调匿名
命名空间的六个辅助）。逐段核对参考才能保证「同一局面同一手」，所以按依赖切成三批：
① 匿名命名空间辅助（`MeleeAttackOutcome`/`ValueHasImproved`/`optimalAttackVector`/`optimalAttackValue`/
`evaluatePotentialAttackPositions`/`bestAttackOutcome`/`findOptimalPositionForSubsequentAttack`/
`findNearestCellNextToUnit`/`getUnitMovementTarget`/`berserkTurn`）+ `AI::getBerserkerTarget`；
② `battleTurn`/`planUnitTurn`/`analyzeBattleState`/`archerDecision`；
③ `meleeUnitOffense`/`meleeUnitDefense`/`getMeleeBestOutcome`/`isPositionLocatedInDefendedArea` +
`ai_battle_spell.cpp` 的 `selectBestSpell` 与十二个 `spell*Value`。

### 2026-11-21 B6 第一批：`Battle::Unit` 的威胁评估三件套

`ai_battle.cpp` 全部 25 个决策函数都架在 `Unit::evaluateThreatForUnit()` 上，所以先把它连同两位依赖一起落到端口
（`src/FHeroes2.Game/Battle/BattleUnit.cs`）：

* `EstimateRetaliatoryDamage(uint damageTaken)`（`battle_troop.cpp:449-491`）：整队被打光 / 已反击 / 镜像 /
  被催眠 / 被定住（无限反击不看这条）都不反击；伤害是「剩余兵数 × **单个兵**伤害」，
  绕开 `Troop::GetDamageMin()` 乘数量的版本（参考显式写 `Monster::GetDamageMin()` 就是为这个）；
  被致盲时按致盲法术的 `ExtraValue()` 打折。
* `GetPotentialDamage(BattleUnit enemy)`（`504-516`）：诅咒取下限、祝福取上限、否则取中点，不掷骰。
* `EvaluateThreatForUnit(BattleUnit defender)`（`1007-1165`）：距离修正（塔内单位/飞行/射手不打折，其余按
  `1.5 × 距离 ÷ 速度`）、双击的反击折扣、半敌 ×2 / 噬魂 ×3 / 吸血 ×1.3、自带施法按触发几率折入（驱散不计、
  诅咒值十分之一）、镜像 ×10、自己人 ×−2 / 被催眠 ×−1 / 不能动归零 / 已行动 ÷1.25。

测试 `BattleUnitThreatTests` 10 条（潜力伤害三态、反击伤害 = 剩余兵数 × 单兵伤害、四种「不反击」、
自己人负值、已行动 ÷1.25、定身归零）。全套测试 4536 → **4546** 全绿，`dotnet build` 0 警告。

下一步（B6 第二批）的缺口已探明，均在 `Battle::Board` / `Battle::Unit` / `Difficulty` 一侧：
`Board::GetCell(index, direction)`、`Board::isValidDirection`、`Board::CanAttackFromCell`、
`Board::CanAttackTargetFromPosition`、`GetAroundIndexes(Position)`、`GetDistanceIndexes(Unit, radius)`、
`Position::GetPosition(unit, index)`、`Unit::GetMissingHitPoints`、`Unit::isDoubleCellAttack`、
`Unit::isAllAdjacentCellsAttack`、`Difficulty::isBasicAIBattleLogicApplicable`。补齐后才是 `ai_battle.cpp`
匿名命名空间的约 250 行辅助（`MeleeAttackOutcome`/`optimalAttackVector`/`optimalAttackValue`/
`evaluatePotentialAttackPositions`）与 `GetMeleeBestOutcome`。

### 2026-11-21 B5 完成 16/16：`ai_planner_hero.cpp` 与 X2 的价值表

* 实现（上一批）：`GetPriorityTarget`（阈值 `-SlowestMovePenalty × 地图面积`、先算敌方威胁罚分、
  `ConsiderObject` 处理顺路对象/城堡免威胁/超一回合打折）、`GetCourierMainTarget`（会面 500 / 空城 250，
  冠军 ×2.5，按 `SafetyFactor` 折半或加倍）、`IsValidHeroObject`、`GetEnemyThreatPenalties`、
  `GetDaysWhenObjectWillBeValid`、`GetApproximateDistance`、两个嵌套缓存类 `ObjectValidator`（X2）。
* 实现（本批）：`UpdatePriorityTargets`（只认城堡与英雄；两个局部函数 `UpdateCastle`/`UpdateTile`，
  城堡被夺走而英雄还在下一格时保留任务）、`AddHeroToMove`、`HeroesBeginMovement`、
  `HeroesActionNewPosition`（专程走到宝物格就睡到明天挖）、`HeroesActionComplete`（买书 → 学法术 →
  `ReinforceCastle` → 慢兵留城换明日行动力，换不回来两边还原）、`HeroesTurn`（三档寻路设定
  `0.5/0.25/0.0`，快输切 `ArmyAdvantageDesperate`；无目标时挪开挡路英雄；次元门多跳循环；
  监牢英雄中途入伙；进度刻度按参考公式推进）、`HeroesPreBattle`、`PickSecondarySkill`。
* 随批补：`Hero.BuySpellBook`（`heroes.cpp:1609-1653`，四道前置拒绝 + 扣款 + 学全城法术）、
  `Hero.SpellCasted`（`heroes_base.cpp`，法术点与移动力各取 min）、`Hero.PickupArtifact`（`heroes.cpp`，
  入包 → 合成套装 → 视野工件立即重扫）、`ScanMap(kingdom, ignoreFog)`（`View All` 生效时无视迷雾）。
* 签名偏差：`PickSecondarySkill` 由 `SecondarySkill` 改为 `SecondarySkillLevel`（参考的 `Skill::Secondary`
  带等级，端口的枚举不带）；`HeroesPreBattle` 由 `HeroBase&` 改为 `ArmyTroops`（端口无该基类，函数只动军队）。
* 未移植并注明：`KingdomTurn` 里的胜负检查（`LossPostActions` 要 Players 层）、状态栏进度与背景音乐、
  次元门/传送的淡入淡出动画。
* 测试 `AiHeroPlannerTests` 6 条（无效槽位、Wisdom > Logistics、并列保留左侧、并列时选基础级、
  `SpellCasted` 的两笔账与不欠账）。全套测试 4530 → **4536** 全绿，`dotnet build` 0 警告。

### 2026-11-21 B7 完成 2/4：`AiHeroAction` 的两次施法

* 实现：`HeroesCastDimensionDoor`（跳 → 扣点 → 计次 → 清路径 → `ActionNewPosition` 结算落点）、
  `HeroesCastSummonBoat`（召船扣点 → 原地 `RemoveMainObject` → 目的格 `SetBoat` + 清原主）。
* 阻塞（因此 `HeroesAction`/`HeroesMove` 仍未移植）：端口没有战斗启动层（`Battle::Loader` 的等价物，
  全库没有 `new BattleArena(...)` 的调用点），没有 AI 的 `IHeroMovementHandler` 实现，
  对象动作表也只覆盖了 `HeroActions` 里约四十种对象。
* 偏差：`HeroesCastDimensionDoor` 不检查「陆地英雄不能落在水面」——端口还没有 `SHIPMASTER` 模式，
  判断不出英雄是否在船上；寻路器已把水面排除在陆地英雄的路线外。

### 2026-11-21 X1 完成：`AIWorldPathfinder` 六个英雄入口

* 实现：`ReEvaluateIfNeeded`（单参为参考原形，军力取自 `hero.Army.GetStrength()`；双参版留给已知军力的调用方，
  上下文比对含召船/次元门/城门户）、`GetFogDiscoveryTile`（两遍扫描：相邻雾瓦 → 相邻不可达瓦，
  候选须 `IsSafeForFogDiscoveryObject`）、`GetNearestTileToMove`（洗牌方向、跳过动作对象、要求
  `GetNodeFrom == start`）、`IsHeroPossiblyBlockingWay`（十一个几何模式 + 石碑出口/踩碑）、
  `BuildDimensionDoorPath`（法术点储备 / 移动力 / `GetDimensionDoorLimitForAi` 三重限制，每跳半程）、
  `GetObjectsOnTheWay`（从目标回溯到 `PathStart`，`AiCommon.IsValuableAdventureMapObject` 过滤并去重）。
* 随批补：`Hero.CanCastSpell`（`heroes_base.cpp:396-430`，`SHIPMASTER` 两支未移植已注明）、
  `Hero.DimensionDoorUses`（`heroes.h:692`）、`Difficulty.GetDimensionDoorLimitForAi`。
* 改名：`GetFogDiscoveryTile` 的返回第二项由 `IsBoat` 改为 `IsTerritoryExpansion` —— 参考的含义是
  「雾就在这个瓦片旁边」，与坐船无关；返回值本身没变。
* 测试 `AIWorldPathfinderTests` 增补 12 条（单参 `ReEvaluateIfNeeded` 的军力来源、迷雾发现两遍扫描、
  最近可走瓦片两种、走廊/空地/石碑三种阻挡、次元门有/无法术、路上对象有/无目标），
  全文件 26 条；全套 Game 测试 2991 → **3003** 全绿，全量 `dotnet build` 0 警告。
* 偏差：`BuildDimensionDoorPath` 取 `(hero, target)` 而不是参考里读缓存 `_pathStart` 的 `(target)`，
  六方法都要求调用方显式给英雄；`MapFog` 与 `Tile._fogColors` 仍是两套迷雾状态
  （`World.ClearFog` 只写前者、路径器只读后者），本批不改生产代码，测试同时写两张表，合并留待后续 ADR。

### 2026-11-21 B4 完成 10/11：`ai_planner_kingdom.cpp` 与 `revealFog`

* 实现：`RecruitHero`、`SetHeroRoles`（Easy 全 Hunter → 胜利条件英雄/最强技能为 Champion → 巡逻即 Fighter
  → 多于三人时最弱两位做 Courier/Scout → 余下按兵力三倍中位数分 Fighter/Hunter）、`GetEnemyArmyOnTile`、
  `CanBecomeCastle`、`EvaluateRegionSafety`（受威胁 -50、敌堡 -100、岛屿 ×1.5、两遍传播）、
  `GetSortedCastleList`（受威胁优先，其余按 `建筑价值 × 对方安全度` 排序）、`FindCastlesInDanger`
  （`ArmyAdvantageSmall`/`ArmyAdvantageDesperate` 两档距离衰减，`ArmyAdvantageDesperate` 时把寻路器的
  `SetMinimalArmyStrengthAdvantage` 临时下调）、`UpdatePriorityForEnemyArmy`/`ForCastle`/
  `IndividualPriorityForCastle`、`RemovePriorityAttackTarget`、`UpdatePriorityAttackTarget`、`PurchaseNewHeroes`；
  另把整图扫描抽成 `internal int ScanMap(kingdom)`（填 `_regions`/`_enemyArmies`/`_mapActionObjects`，
  收尾调 `EvaluateRegionSafety` 与 `UpdateKingdomBudget`，返回 `availableHeroCount`）。
* 随批补：`ai_planner.cpp` 的最后一件 `revealFog`（动作对象才记缓存、把第一个在动的英雄的路径截到一步）；
  `Difficulty.AreAiHeroRolesAllowed` 与 `Hero.GetStatsValue`/`GetRecruitValue` 上一批已就位，本批开始被使用。
* 修：`GetEnemyArmyOnTile` 原来拿瓦片的原值比 `Mp2ObjectType.Hero`，而英雄瓦片存的是 `ActionHero`
  （183），条件永远不成立 —— 改成先 `Mp2ObjectTypes.NonActionType` 再比（ADR-133 口径）。
* 测试：`AiKingdomPlannerTests` 8 条（角色四条、扫描与区域安全度、城堡排序、招募上限、`revealFog`）。
  全套 Game 测试 2991 条全绿，全量 `dotnet build` 0 警告。
* 偏差：`KingdomTurn` 的回合循环留到 B5（第一句就是 `HeroesActionComplete`）；扫描不看 `View All`
  （端口无冒险地图施法），也不重置 `SLEEPER` 与次元门次数（端口无此状态）；`_regions` 加只读窗口
  `Regions`，`SetHeroRoles`/`GetSortedCastleList`/`PurchaseNewHeroes` 由 private 改 `internal` 供测试。

### 2026-11-21 B3 完成：`ai_planner_castle.cpp` 四个成员

* 实现：`CastlePreBattle`（访客英雄 + `ArrangeForCastleDefense` + `OptimizeTroopsOrder`）、
  `CastleTurn`（防守走 `reinforceCastle` 再买塔，平时按区域安全度开发）、
  `reinforceCastle`（升级合并 → 按住所从 7 号降级征募 → 腾槽/最弱兵解散 → 交还最强给访客 → 安全城堡留半队）、
  `updateKingdomBudget`（六套建造顺序的缺口累加 + 6 号住所的升级兵种价记为 `recurringCost`）。
* 随批补：`System/Difficulty.cs` 两个 AI 城堡开关、新建 `Heroes/HeroRole.cs` 与 `Hero.AiRole`、
  `Army.UpgradeTroops(CastleTown, ref Funds)` 复用既有实现。
* 测试 `AiCastlePlannerTests` 10 条：难度开关、战前搬运（含开槽重排）、防守回合的征募与单日单建筑、
  空守军不买塔、按住所征募、满编时解散最弱、满编时交给访客腾槽、预算的缺口/优先级/循环成本
  （用 `getResourcePriorityModifier` 的 ×1.5 / ×2 反查 `_budget`）。全套 Game 测试 2983 条全绿。
* 偏差：
  * `CastleTurn` / `reinforceCastle` / `updateKingdomBudget` 由 private 改 `internal`（测试驱动需要）。
  * 「安全城堡留半队」的区域读取把 `!garrison.IsValid` 前置：语义等价，便于半移植状态下（`_regions` 仍空）不越界。
  * 平时分支（区域安全度）未测：`_regions` 由 B4 的 `evaluateRegionSafety` 填充。
* 顺手修：`Army.Troops.cs` 里重复定义的 `UpgradeTroops` 已删，保留原有那一份。

### 2026-11-21 B2 完成 4/5：`ai_planner.cpp` 的缓存与资源权重

* 实现：`getTileArmyStrength`（`Army.SetFromTile` 估值后按 `Tile.Index` 缓存）、
  `getResourcePriorityModifier`（矿/堆两张静态权重表 + `_budget` 的 `recurringCost`×1.5 与 `priority`×2 缩放）、
  `getFundsValueBasedOnPriority`、`updateMapActionObjectCache`。
* `revealFog` 挪到 B4：它只调 `updatePriorityAttackTarget`（`ai_planner_kingdom.cpp`），没有独立逻辑。
* 测试 `AiPlannerTests` 4 条：缓存命中、四种矿权重、七种堆权重、组合价值。全套 Game 测试 2973 条全绿。
* 偏差：`getResourcePriorityModifier`/`getFundsValueBasedOnPriority` 由 private 改 `internal`（调用者在 B5，
  测试无别的入口）；缓存键依赖 `Tile.Index`，测试世界要自己编号（`GenerateUninitializedMap` 不编号）。
* 未测：`updateMapActionObjectCache` 的写入目前没有可观察的输出，等 B4 的 `revealFog` 读缓存时补测。

### 2026-11-21 B1 完成：`ai_common.cpp` 九个函数

* 实现：`Ai/AiCommon.cs` 九个函数全部落地；`BuildIfPossible` 走「`CheckBuyBuilding` → 差资源时
  `TradeAtMarketplace` → `BuyBuilding`」，`BuildIfEnoughFunds` 把价格乘 1..99 再比对，
  `OptimizeTroopsOrder` 按弓兵/速度/飞行重排并在高难度拆弱队，`CalculateMarketplaceTransaction` 用
  `ResourceTrading.GetTradeCost` 逐步挑「潜在收益最大」的资源，`IsValuableAdventureMapObject` 覆盖
  一次性对象、观察塔、金字塔、敌方英雄等分支。
* 随批补：`System/Difficulty.cs`（拆队与信息共享的难度开关）、`MapFog.CountFoggedAround`
  （`maps.cpp:531` 的 `getFogTileCountToBeRevealed`）。
* 测试：`AiCommonTests` 19 条；`AiContractTests` 里那条 `TheUnportedMembersSaySo` 改成
  `BuildIfEnoughFundsRejectsTheMultiplierOutsideTheReferenceRange`。全套 Game 测试 2969 条全绿。
* 偏差：`TransferSlowestTroopsToGarrison` 的 `null` 城堡返回而不 `assert`；清空槽位不压缩数组。

### 2026-11-21 建立本账本（B0 复核）

* 逐一核对九个 `.cpp` 的公开声明与端口契约：`Planner` 39 个成员、`BattlePlanner` 24 个成员、
  `ai_common.h` 9 个自由函数、`ai_hero_action.h` 4 个入口、4 个阈值、`AIWorldPathfinderStateRestorer`
  全部有对应，签名一一对齐，`AiContractTests` 覆盖契约可测的部分。
* 结论：**契约先行这一步已经完成**，不需要重做；实现按 B1→B7 推进，前置 X1→X4 单列。
* 新发现两处注释偏差（`RecruitHero`/`PurchaseNewHeroes` 的归属写成了 castle，参考在
  `ai_planner_kingdom.cpp`），记在 `docs/planv3.md` §5，随 B4 改。
* 总账 `ai` 行从 `0/0/9` 改为 `1/0/8`：`ai_personality.cpp` 已在 `System/AiPersonality.cs`，
  表里一直记着未转。
* 未做：B1..B7 与 X1..X4 的全部实现。`Game/Ai/` 的行为测试为零，`AiContractTests` 之外一条都没有
  （`AIWorldPathfinderTests` 374 行在 world 侧，不算）。
