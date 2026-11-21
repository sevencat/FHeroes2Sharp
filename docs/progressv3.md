# AI 实现进度（v3）

计划见 `docs/planv3.md`。本文件只记「哪一批做完了」与偏差，不重复计划内容。

## 状态总表

| 批次 | 范围 | 状态 | ADR | 测试 | 日期 |
| --- | --- | --- | --- | --- | --- |
| B0 | 契约（签名、数据模型、契约测试） | ✅ | ADR-161 / ADR-180 | `AiContractTests` 169 行 | 2026-11-17 |
| B1 | `ai_common.cpp` 九个函数 | ✅ | — | `AiCommonTests` 19 条 | 2026-11-21 |
| B2 | `ai_planner.cpp` 五个短函数 | ⚠️ 4/5（`revealFog` 归 B4） | — | `AiPlannerTests` 4 条 | 2026-11-21 |
| B3 | `ai_planner_castle.cpp` 四个成员 | ✅ 4/4 | — | `AiCastlePlannerTests` 10 条 | 2026-11-21 |
| B4 | `ai_planner_kingdom.cpp` 十一 + 一个成员 | ❌ 未开始 | — | `AiKingdomPlannerTests` | — |
| B5 | `ai_planner_hero.cpp` 十六个成员 | ❌ 未开始 | — | `AiHeroPlannerTests` | — |
| B6 | `ai_battle.cpp` + `ai_battle_spell.cpp` | ❌ 未开始 | — | `AiBattlePlannerTests` | — |
| B7 | `ai_hero_action.cpp` 四个入口 | ❌ 未开始 | — | `AiHeroActionTests` | — |
| X1 | `AIWorldPathfinder` 六个方法 | ❌ 未开始 | — | `AIWorldPathfinderTests` 增补 | — |
| X2 | `ObjectValueStorage` 与对象价值表 | ❌ 未开始 | — | — | — |
| X3 | `battle_action` 法术目标与生效 | ❌ 未开始 | — | — | — |
| X4 | `heroes_action` + 对象交互 + Interface 解耦 | ❌ 未开始 | — | — | — |

覆盖率基线（`docs/PLAN.md` 的 `ai` 行）：**3/0/6**，71 处 `NotImplementedException`（AI 目录 58 + `AIWorldPathfinder` 13）。

## 流水

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
