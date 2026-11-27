# 技术决策（DECISIONS）


> **2026-11-24 起**：`PLAN.md` 与 `PROGRESS.md` 已退役并从仓库删除，任务、进度与剩余工作以 **bd (beads)**
> 为准（`bd ready` / `bd list` / `bd stats`）。两份文档删除前的全文归档为 closed issue
> `FHeroes2Sharp-3so`（PLAN）与 `FHeroes2Sharp-jwk`（PROGRESS）；下面 ADR 里引用的 `PROGRESS.md §x`/`PLAN.md §x`
> 均指那份归档，用 `bd show FHeroes2Sharp-3so` / `bd show FHeroes2Sharp-jwk` 取全文。

> **份量与写法（2026-11-25 整理，见 `FHeroes2Sharp-6y3.7`）**：本文件只保留**当前阶段**的 ADR（`ADR-151` 起）。
> `ADR-001…150`（到 `battle` 第四批为止，约 500 KB）已冻结进 [`DECISIONS-archive.md`](DECISIONS-archive.md)，
> 正文一字未改；要追溯历史决策时按编号去那边搜，链式引用（「见 ADR-107」）照旧有效。
>
> 新增 ADR 的约定，目的是让它别再失控增长：
>
> 1. 只写 **背景 / 决策 / 理由 / 后果（含「没做」）**，不写测试计数、行数统计与「N 条全绿」——这些事实属于 bd；
> 2. 「仍未转」清单不在这里维护，未转内容以 bd 的 issue 为准；
> 3. 单条目标 1.5 KB 以内；超了就该拆成多条，或把细节挪进代码注释；
> 4. 本文件再次超过 400 KB 时，把最早的一半按编号切进归档（切点必须落在 `## ADR-` 边界上），
>    并更新归档头部的区间。

## ADR-151：`battle` 第五批——坟场、桥与单位集合 ✅

**背景**：ADR-150 把 `Unit` 的算术补全了，但它周围还有三个小对象没落地：尸体躺在哪（`Graveyard`）、护城河上的桥
开没开（`Bridge`）、以及一堆单位怎么按 UID/模式找、怎么按速度排序（`Units`）。三者都不需要竞技场，而且正好是
`battle_cell` 的桥通行性与 `battle_army` 的 `Force` 的前置。

**决定**：

1. **坟场是 `SortedDictionary<int, List<BattleUnit>>`**：参考是 `std::map`，键有序遍历才有意义（`getOccupiedCells`
   按格子升序）。空链留着不删（与参考一致），查询时跳过；`Count` 数的是「有尸体的格子」而不是尸体数。
2. **宽单位压两格，尸体排序用 rotate 语义**：`MoveToLastIfPresent` 把该单位挪到链尾、其余保持相对顺序——对尸体
   施法时它的动画要叠在最上面。用 `RemoveAt` + `Add` 复刻；不能用「与末尾交换」那种会打乱其余顺序的写法。
3. **坟场登记接进 `PostKilledAction`，且必须在交还格子之前**：参考在 `removeAffection( IS_MAGIC )` 之后、
   `head->SetUnit( nullptr )` 之前调 `graveyard->addUnit( this )`，因为坟场按格子下标归档。两个例外——镜像
   （`CAP_MIRRORIMAGE`）与元素（`isElemental`）——不进坟场。端口把 `Graveyard` 做成可空属性，单测造单位不必先造战场。
4. **桥的三个依赖做成构造参数**：格盘、坟场、守城方颜色。参考从 `Arena` 单例取，端口显式注入；颜色可写，因为
   竞技场通常是先建桥再建城堡。`NeedDown` 的六个落点分支逐条照抄。
5. **桥认 `GetColor()` 而不是 `GetCurrentColor()`**：被催眠的守城兵按参考不算守城方，过不了升起的桥。这类
   「看起来应该用 current」的地方要照抄参考，否则催眠+桥的组合会与参考分叉。
6. **`ActionUp`/`ActionDown` 返回布尔**：参考用 `assert` 卡前置条件（失败即崩），端口返回「到底动没动」。
   参考在这里还会触发 `Interface::RedrawBridgeAnimation`，动画归界面批次，不进这个类。
7. **`SortFastest` 用稳定排序，且比的是战斗速度**：参考把 `Unit*` 传给 `Army::FastestTroop( const Troop * )`，
   而 `Troop::GetSpeed` 是虚函数、被 `Unit::GetSpeed()` 覆盖，所以减速/加速会改变出手顺序（不是兵种表上的数字）。
   `List.Sort` 不稳定，用 LINQ 的稳定排序再写回；注释与测试都钉住「同速保持原序」。

**后果**：新增 `BattleGraveyard`、`BattleBridge`、`BattleUnits`；`BattleUnit` 加了 `Graveyard` 并在
`PostKilledAction` 里登记尸体。测试 27 条（`BattleGraveyardTests` 9、`BattleBridgeTests` 10、`BattleUnitsTests` 8）。
Game 2332 → **2359**，全解决方案 3089 → **3116** 全绿，`-warnaserror` 0 警告。覆盖率：`battle` 2/3/13 → **4/4/10**，
合计 43/66/141 → **45/67/140**。

**遗留**：`battle_army.cpp` 只转了 `Units`；`Force` 还差 `TroopsUidGenerator`（参考在 `battle_arena.h`，只是个计数器）、
按阵形摆位、`GetTotalNumberOfDeadUnitsForNecromancy`/`CalculateExperienceBasedOnLosses`、`syncOriginalArmy` 与
`GetSurrenderCost`（要英雄的宝物与外交技能）。`battle_bridge` 的动画与 `battle_grave` 的对尸体施法接口随界面与
法术批次补。

## ADR-152：`battle` 第六批——把一支军队摆上战场 ✅

**背景**：ADR-151 落地后，`BattleUnits` 只是「一堆单位」。可战斗从来不操作裸单位：它操作的是两片军队，要按阵形
摆位、要知道某个单位属于哪个槽位（伤亡回写、投降价、经验都靠这条对号），还要在每回合开头把英雄的「本回合已
施法」清掉。这就是 `Battle::Force`。

**决定**：

1. **`BattleForce` 继承 `BattleUnits`**，而不是包一层。参考里 `Force : public Units`，端口把 `BattleUnits` 去封即可——
   「一支军队」本来就是「一堆单位」，多了摆位与对号。`Force` 继承的 `BitModes`（撤退/投降标记）暂不跟，它们只影响
   战斗结果与经验加成，等 `battle_result` 落地再加。
2. **摆位公式逐字照抄**：`22 + 11*i`（铺开阵型 `22*i`），镜像侧 `+10`，宽单位上侧 `+9`、下侧 `+1`。这些不是随手挑的
   间距，而是棋盘上两片出发区的真实坐标；改一个数字就会压格或越界，所以宁可留硬编码也不「抽象成参数」。
3. **`_uids` 与 `Army` 槽位一一对应，空槽存 0**：所有「场上 ↔ 原始军队」的换算都走这张表。这样召唤物、镜像顶替、
   中途被清场的单位不会污染伤亡统计，`GetSurrenderCost` 也不会把召唤出来的兵算进价钱。
4. **伤亡统计一律夹在 `min( initialCount, dead )`**：死亡数可能因为吸血超过初始人数（`ApplyDamage` 允许），
   不夹的话亡灵巫术与经验都会凭空变大。
5. **投降价照抄三段式**：先按活着的单位累加 `cost * 存活数`；有指挥官时基数从 `0.5` 起步、宝物按百分比连乘、
   外交术先乘 2 再乘一档折扣；最后四舍五入且**不低于 1**。那个 `0.5` 是参考给的「英雄在场就打对折」，不是
   「没有折扣」，很容易抄错成 1。
6. **`IArmyCommander` 从只读扩出两个成员**：`GetLevelSkill`（默认 `None`，队长本来就没技能）与
   `ResetModes`（默认空，只有英雄有模式字）。这是唯一一处战斗要写指挥官的地方——清 `SPELLCASTED`；不开一个
   通用的「拿 HeroBase 指针」口子。
7. **`GetControl` 走 `Players`**：参考里英雄问王国、队长问城堡，两条路最后都落到 `Players::GetPlayerControl( color )`。
   端口没有「英雄→王国」的反向指针（同色只有一份玩家记录），于是两边都直接问 `Players`，答案相同、依赖更少。
8. **类型别名 `GameArmy`**：`FHeroes2.Game.Army` 既是命名空间又有同名类。`BattleForce`/`BattleUnit` 里统一
   `using GameArmy = FHeroes2.Game.Army.Army;`，同时保留 `Army` 作为属性名（与参考 `GetArmy()` 对应）。

**后果**：新增 `BattleForce`、`BattleTroopsUidGenerator`；`BattleUnits` 去封；`BattleUnit` 加 `Army` 回指、
`IsMonster`、`GetCost`、`GetSurrenderCost`、`GetControl`、`GetCurrentControl`；`Army` 加 `IsSpreadFormation` 与
`GetControl`；`IArmyCommander`、`Hero`、`Captain`、`HeroModes` 各加一到两个成员。测试 16 条（`BattleForceTests`）。
Game 2359 → **2375**，全解决方案 3116 → **3132** 全绿，`-warnaserror` 0 警告。覆盖率不变：`battle` 4/4/10，
合计 45/67/140（本批补的是 ◐ 文件的内部，没有新增 ✅ 文件）。

**遗留**：`GetKilledTroops`（要参考的 `Troops` 容器做同兵种合并）、`Force` 的撤退/投降标记；`Unit` 侧还差
`ApplySpell` 族与 `GetMorale`/`GetLuck`——所属军队已接通，可以顺着做。

## ADR-153：`battle` 第七/八批——士气运气与法术应用层 ✅

**背景**：`BattleForce`（ADR-152）让单位知道自己属于哪支军队之后，`battle_troop.cpp` 只剩两块：
士气/运气的来源与掷骰（`.cpp:276-329`），以及法术应用层（`.cpp:65-98`、`793-890`、`1174-1534`）。
后者是整个战斗最密的算术之一：能落不能落、落了打多少、打完治多少，全都写在这几百行里。

**决定**：

1. **`Army.GetMorale()` 先问指挥官，英雄侧自己补军队修正**。参考的 `Army::GetMorale()` 是
   `commander ? commander->GetMorale() : GetMoraleModificator()`（`army.cpp:1220-1224`），
   `Heroes::GetMorale()` 再自己叠 `GetArmy().GetMoraleModificator()`。两条必须同时照抄：
   只做前一条，有指挥官的军队会把「多种族 -1」与亡灵惩罚静默吃掉；两边互相回调则成环。
2. **骨龙减士气走 `IBattleField.EnemyHasMonster( color, monster )`**。参考在 `Unit::GetMorale()` 里
   直接问竞技场「对面有没有骨龙」，端口不因此把敌方军队塞进 `BattleUnit`——`IBattleField` 本来就是
   「单位需要问竞技场的那几件事」的集合，加一条比多一条反向引用便宜。
3. **随机序列逐字照抄**：好士气 `if`、坏士气 `else if`、AI 豁免只在坏分支。换成两个独立 `if` 会多掷一次骰，
   整场战斗的随机序列随之错位——这类错误不会在单条测试里暴露，只会在长战斗里「AI 运气怪怪的」。
4. **法术应用层留在 `BattleUnit`**，分成三条私有路：`SpellApplyDamage`、`SpellRestoreAction`、
   `SpellModesAction`。公共入口只有 `AllowApplySpell`/`ApplySpell`/`GetMagicResist`/`CalculateSpellDamage`
   与 `GetCurrentSpellEffects`，签名与参考一一对应。
5. **`TargetInfo` 用类（`BattleTargetInfo`）**。参考按引用传 `TargetInfo &`；C# 里结构体会让每个签名都加 `ref`，
   而类天然是引用语义。伤害与击杀数写回同一个对象，测试可以直接断言 `target.Damage`/`target.Killed`。
6. **`GetMagicResist` 先判「法术有没有意义」**：反魔 100%、治疗满血且干净 100%、复活满员 100%、驱散无可摘 100%、
   催眠目标超过法术上限 100%，然后才是宝物免疫与怪物抗魔。顺序不能换——「反魔单位对治疗也免疫」与
   「对满员的队伍不必复活」是两种不同的 100%，测试要能分辨。
7. **`CalculateSpellDamage` 的四分支是 `if/else if` 链**：指定法术减伤 → 元素法术减伤（附 `IsElementalSpell()`
   条件）→ 指定法术弱点 → 火焰/寒冷弱点，只取最具体的一条；元素那一条的条件不成立时要**继续**落到弱点分支。
   宝物加成则逐项连乘（不是把百分比相加），连锁闪电的 `/2`、`/4`、`/8` 写在**有施法者**的分支里——参考就是
   这样写的，没有英雄时不减半，照着留。
8. **宝物免疫只认参考的八种**：诅咒、催眠、死亡、狂暴、致盲、麻痹、神圣、驱散。没有「寒冷免疫」这类
   `ArtifactBonusType`，不要凭直觉补。

**后果**：新增 `BattleTargetInfo`；`BattleUnit` 加 `GetCommander`、`IsHaveDamage`、`AddAffection`、
`AllowApplySpell`、`ApplySpell`、`GetMagicResist`、`CalculateSpellDamage`、`GetCurrentSpellEffects` 与三条私有路；
`IBattleField` 加 `EnemyHasMonster`；`IArmyCommander` 加 `GetColor`/`GetPower`/`GetMorale`/`GetLuck`；
`Army`/`Hero`/`Captain` 接线。测试 13 + 24 条（`BattleUnitMoraleTests`、`BattleUnitSpellTests`）。
Game 2375 → **2412**，全解决方案 3132 → **3169** 全绿，`-warnaserror` 0 警告。覆盖率不变：
`battle` 4/4/10，合计 45/67/140（补的是 ◐ 文件的内部）。

**遗留**：`battle_troop.cpp` 已无未转函数；下一步是 `battle_arena`/`battle_action`（选目标、法术路径、行动执行），
那才是真正需要棋盘与单位集合的地方。

## ADR-154：`battle` 第九批——寻路（`BattlePathfinder`）✅

**背景**：`battle_pathfinding.h/.cpp`（124 + 416 行）是竞技场与 AI 共同的前置：谁走得到哪儿、走过去要多少移动力、
路径长什么样。它只依赖棋盘几何与一组单位查询，正好是单位层收尾后最干净的一块。

**决定**：

1. **竞技场依赖压成三个构造参数**：棋盘（`BattleBoardCells`）、有没有护城河、桥对这个单位通不通（`Func<BattleUnit,bool>`）。
   参考直接调 `Arena::GetBoard()`/`GetCastle()`/`Bridge::isPassable( unit )`；`isPassable` 要问单位，所以只能是委托。
   桥不存在与桥吊起给同一个答案（都不可通过），这与参考里 `bridge == nullptr` 的处理一致。
2. **「未到达」必须显式构造**。参考用父节点 `{ -1, -1 }` 当哨兵，C# 的 `default(BattlePathNode)` 是全零，父节点成了
   `( 0, 0 )`——一个合法格子。若照抄 `TryGetValue(...) ? existing : default`，所有节点都会被当成「已到达、代价 0」，
   于是没有任何松弛能成立，寻路结果恒为空，而且不会报错。引入 `BattlePathNode.None` 并在测试里把这条钉死。
3. **可变结构体出字典要「取出→改→写回」**。`Dictionary[key].Update(...)` 是对副本调用方法，编译器不吭声、运行期白干。
   飞行分支最初就是这么写的，改成局部变量后赋回。
4. **缓存键含整张棋盘的通行性快照**（起点、速度、宽窄、飞行、颜色、99 格布尔）。参考注释说明了理由：单位自己的回合里
   棋盘也会变（英雄施法、尸体挪位），不重算就会沿着一条已经不存在的路走。端口额外记一个 `_hasCache` 布尔——参考靠
   哨兵排除首轮，若单位恰好不在棋盘上（头格 -1）且其余参数都等于初值，会退化成「空缓存被当成有效缓存」。
5. **护城河罚款记在「迈出护城河」那一步**（`MOAT_PENALTY = UINT16_MAX = 65535`）。所以走进护城河只要 1，从护城河
   迈出去要 65535，而「本就站在护城河里」的那一格在起点上被豁免一次。直觉相反，但这是参考的可观测行为，测试按
   这个语义写，别按直觉「修正」。
6. **宽单位原地转身代价为 0**（`newNodeIdx == flippedCurrentNodeIdx`），横移半步代价 1，斜向走时尾巴必须落在非移动
   方向那一侧（`Cell::isPassableFromAdjacent`）。`buildPath` 末尾补一步转身，`getClosestReachablePosition` 里同样
   先 `Set` 再按需 `Swap`。
7. **飞行单位一次落地**：代价恒为 1，步数取 `max(GetDistance, 1)`——两个位置可以重叠，实际距离为零但确实动了一步。
8. **非飞行用「带重入的广度优先」**（发现更便宜才重新排队），照抄参考。因为每步代价非负且护城河惩罚是常数，结果与
   Dijkstra 相同，但探索顺序与参考一致，便于逐例对拍。

**后果**：新增 `BattlePathfinder`（六个公开查询：`IsPositionReachable`/`GetCost`/`GetDistance`/`BuildPath`/
`GetAllAvailableMoves`/`GetClosestReachablePosition`）与 `BattleNodeIndex`/`BattlePathNode`；`BattleCell` 补
`IsPassableFromAdjacent`/`IsPassableForUnit`（用 `unit.Position.Board` 找邻居，格子本身不持有棋盘）。测试 13 条
（`BattlePathfinderTests`）。Game 2412 → **2425**，全解决方案 3169 → **3182** 全绿，`-warnaserror` 0 警告。

**遗留**：`Position::GetReachable`（要问竞技场这个单位有多少移动力），以及 `battle_arena` 自己的选目标与法术路径。

## ADR-155：`battle` 第十批——投石车与箭塔（`BattleCatapult`/`BattleTower`）✅

**背景**：`battle_catapult.h/.cpp`（73 + 178 行）与 `battle_tower.h/.cpp`（78 + 192 行）是围城的两件器械。
两者都不在棋盘上活动（投石车在城外，箭塔的箭手是凭空的），依赖只有城堡、单位和随机数，是竞技场之前最后
一批能独立测的东西。

**决定**：

1. **塔用组合而不是继承**。参考的 `Tower : public Unit` 只为覆盖 `isValid()`——被砸掉的塔人还在但不打了。端口
   的 `BattleUnit` 是 sealed，于是 `BattleTower` 持有自己的 `BattleUnit`（构造过程与参考逐句对应）并用一个自己的
   `_isValid` 回答 `IsValid`。塔的「位置」是一张一次性的空棋盘，单位因此永远不占格，与参考的 `Position{}` 同效。
2. **`SetCount` 的怪癖照抄**：参考先按 `min(CountBuildings,20)` 造单位、再 `SetCount` 折半，而 `SetCount` 只改数量
   不动血量，于是炮塔是「半队人、满血」。端口先构造再改 `Troop.Count`，得到同一结果；测试同时断言数量与血量。
3. **攻击加成取法师塔等级，不是弹道术**。`GetAttackBonus() = castle.GetLevelMageGuild()`；英雄的弹道术只作用于
   投石车（射数、双倍概率、能否失手）。
4. **补上 `ArmyTroop` 那一项**。参考 `Unit::GetAttack()/GetDefense()` 是 `Troop::… + 指挥官…`，端口此前只算了怪物
   基础值。这不只是塔的问题：没这一项，英雄带的兵与地图上的野兵攻击完全相同。攻击与防御一起补，并把塔的
   「箭手继承指挥官攻击」当成它的第一个使用者。
5. **投石车的优先级照抄**：墙（四段）→ 塔（两座）→ 桥 → 主堡，每一级内部才随机，每一发都重新挑级。`GetTarget`
   的目标表由调用方给出「还剩多少」，缺键是调用方的错（参考 `assert`）。`GetAllowedTargets` 只有八项：桥头两座
   小塔不是目标。
6. **能不失手就不掷骰**：`IsNextShotHit` 在 `CanMiss == false` 时直接返回真，不消耗随机数——回放依赖这一点。
   命中率是掷 1..20 取 ≥ 6。
7. **顺手补三个留白**：`Board::GetMoatInfo()`（城堡描述要它）、`Castle::GetDescriptionBuilding` 里主堡之后接的
   塔与护城河两段（`castle.cpp:2544-2566`）、以及 `Castle::GetGarrisonStrength` 的塔强度项（`castle.cpp:2100-2111`，
   主堡一座、炮塔各半座，加在乘系数之前）。这三处此前都是「等 `Battle::Tower`/`Board` 就位再说」，现在一并接上。

**后果**：新增 `BattleCatapult`、`BattleTower`、`CastleDefenseStructure`、`BattleTowerType`；`BattleUnit` 补
`GetStrengthWithBonus` 并修正 `GetAttack`/`GetDefense` 的指挥官项；`BattleBoard` 补 `GetMoatInfo`；城堡的描述与
驻军强度各补一段。测试 26 条（`BattleTowerTests` 14、`BattleCatapultTests` 12）。Game 2425 → **2451**，全解决
方案 3182 → **3208** 全绿，`-warnaserror` 0 警告。覆盖率 `battle` 5/4/9 → **7/4/7**，合计 46/67/139 → **48/67/137**。

**遗留**：`battle_arena` 与 `battle_action`——塔与投石车在这两处各只剩一句调用，选目标与结算都在竞技场里。

## ADR-156：`battle` 第十一批——竞技场的装配与查询（`BattleArena`）◐

**背景**：`battle_arena.h/.cpp` 约 1000 + 1500 行，是战斗层的枢纽。它同时承担四件事：装配战场（棋盘、军队、城堡、
塔、桥、投石车、寻路器）、回答战斗中的事实（颜色、指挥官、护城河、射击惩罚）、推进回合，以及执行行动命令。
第三、四件事的全部实现都落在 `battle_action.cpp` 与 `Battle::Interface` 上。

**决定**：

1. **这一批只做前两件事**。`Turns()`、`UnitTurn()`、`ApplyAction()`、`TowerAction()`、`CatapultAction()` 要等
   `battle_action` 的命令结构落地；现在写只能是空壳，还会把「当前单位怎么被推进」写死在错误的位置上。状态与判据
   先写全，行动层落地时只需调用它们。
2. **竞技场不做单例**。参考在构造函数里把自己登记进文件静态的 `arena`，之后 `Arena::Get()` 全局可取，连
   `Unit::isMoatBonus()` 都靠这条路。端口让战场成为普通对象，把逆向依赖压缩成 `IBattleField` 的五个成员（`IsOnMoat`、
   `GetShootingPenaltyPercent`、`GetArcheryBonusPercent`、`GetOppositeColor`、`EnemyHasMonster`），并在构造末尾把
   `Field`/`Graveyard` 注入每个单位（含塔内部的单位）。
3. **`IsTown` 与 `Castle` 分开**。没有 `BUILD_CASTLE` 的村庄仍算攻城（多 500 经验），但棋盘上没有墙；参考在
   `battle_arena.cpp:360-362` 把城堡指针置空、`_isTown` 保留为真。端口照此实现，`Castle == null` 时才铺野战障碍。
4. **塔的军队取「实际守城的那支」**。参考向城堡要 `GetActualArmy()`（客将优先，否则驻军）。端口的城堡没有指向客将的
   回指，而竞技场收到的防守军本来就是那支军队，于是构造后把 `tower.Unit.Army` 指向 `DefendingForce.OriginalArmy`；
   测试断言「塔的箭手拿到哪个指挥官的攻击」。
5. **四类结构、三种摧毁语义**。墙可打穿到 0（格变可通行）；桥头塔与箭塔最低停在 1（掉一级、永久挡路）；桥一下到底
   （格变 0）。有炮塔建筑的箭塔把伤害记在 `BattleTower` 上、格子写瓦砾 1；没有炮塔的箭塔直接扣格子数。中央塔不在
   地震目标里，`getCastleDefenseStructureCondition` 的「地震打中央塔」分支照参考读 0 号格而不抛错。
6. **墙的耐久起点看 `Spec` 而不是主堡**。`IsFortificationBuilt` 只对骑士且建了 `Spec` 成立（`castle.cpp`），此前按
   「有主堡就是 3」的直觉写测试会失败——以参考为准，`3`/`2` 两档都测。
7. **射击惩罚：先豁免、后几何**。顺序是无城堡 → 塔 → 黄金弓/箭术 → 射手在墙内 → 双方同侧，最后才沿两支单位的背点
   连线以 `Cell::widthPx/3` 采样，检查是否从已打穿的墙格看过去。先算几何会把前四条豁免埋掉。新增
   `LinePoints.GetLinePoints`（`getLinePoints()` 移植）与 `BattleUnit.GetBackPoint`/`IsOutOfCastleWalls`/
   `GetCurrentOrArmyColor` 作为配套。
8. **障碍用独立随机源**。`PCG32( world.GetMapSeed() + tileIndex )`，与战斗随机数无关：同一格野战的地形永远一样。
   放置上限按已有大障碍（`0x40`）的格数分四档（6/4/3/2），并保留两条硬约束（双格障碍不从第 8 列起、高障碍不在最上
   两行）。`SetCobjObjects` 为免让棋盘认识世界，收「主对象类型 + 地面」而不是 `Tile`。
9. **护城河同时问城堡与桥**。城堡回答有没有建护城河，棋盘回答「飞行/桥面可通行」；桥的 `IsPassable` 还看闸门与
   护城河格是否被占——所以自己站在护城河格上时桥被自己占住，照样吃罚款，与参考一致。

**后果**：新增 `BattleArena`（三个 partial：主文件、`.Castle.cs`、`.Combat.cs`）与 `SiegeWeaponType`；
`BattleBoardCells` 补 `SetCovrObjects`/`SetCobjObjects`。测试 31 条新增（`BattleArenaTests` 24、
`BattleBoardObstacleTests` 7）。Game 2451 → **2482**，全解决方案 3208 → **3239** 全绿，`-warnaserror` 0 警告。
覆盖率 `battle` 7/4/7 → **7/5/6**，合计 48/67/137 → **48/68/136**。

**遗留**：`Turns()`/`UnitTurn()`/`ApplyAction()`/`TowerAction()`/`CatapultAction()`（等 `battle_action`）；界面、
音乐等待、AI 规划器、自动战斗；`FromTile` 的棋盘层测试已有，但缺一个真实 `World` 的端到端用例。

## ADR-157：`battle` 第十二批——行动层的移动与回合动作 ◐

**背景**：`battle_action.cpp` 有 1274 行，一半是界面动画（逐帧等待、三段攻击动画），另一半改格子与模式位。
`battle_arena` 的请求队列与 AI 都从 `ApplyAction` 进来，所以这一层是战斗可玩性的第一道门。

**决定**：

1. **先取与画面无关的一段**：分发器加上 `MOVE`/`SKIP`/`MORALE`。攻击要 `BattleUnit` 的反击判定、
   `TargetsApplyDamage`、`calculateAttackTarget` 三样都齐；塔与投石车要等这一批的命令结构被真正喂过一遍；
   撤退与投降要 `World`→`Kingdom` 的扣金链路（`World` 目前没有 `GetKingdom` 的公开入口）。先做能自洽验证的。
2. **未移植的命令显式抛 `NotSupportedException`**，消息里点名还差什么。分发器静默忽略一条 `ATTACK` 会让战斗停在
   原地而无任何迹象——那类 bug 比异常难查。非法参数同理，一律抛 `ArgumentException`（参考是打日志 + `assert`）。
3. **`Position::GetReachable` 由竞技场作参数**。它要问「走不走得到、花多少移动力」，参考向单例要；端口把
   `BattleArena` 传进去。这是 `BattlePosition` 第一次依赖 `battle_arena`，也让 `BattlePosition` 的文档承诺
   （「随 battle_arena 一起补」）兑现。
4. **`checkMoveParams` 要求头格命中**。宽单位在 `GetReachable` 里允许「尾巴落在 dst、头在身前」的倒行解，但
   `checkMoveParams` 又要求 `pos.GetHead()->GetIndex() == dst`，于是那条解在真实命令里被否掉。两条规则看似矛盾，
   其实是「寻路能想到」与「命令允许」的区别，测试里各覆盖一条。
5. **`moveUnit` 的逐格挪提成主路径**。参考只在有桥时真的逐格挪（`battle_action.cpp:333-350`），其余情况交给界面动画；
   端口没有界面，于是步行一律逐格挪，桥的升降（`NeedDown`/`AllowUp`）作为附加动作挂在每一步前后。
6. **桥的通不通必须由调用方烘进闸门格**。`Bridge::SetPassability()` 把答案写进 `objectType`，寻路器只读格子、不回头
   问桥。参考在 `Turns()` 的每个单位行动前刷一次；端口没有回合循环，把刷新挂在 `ApplyActionMove` 与 `MoveUnit`
   两个入口上，并注明「按正在行动的那一支重算」这个与参考一致的副作用。
7. **`UpdateDirection()` 需要竞技场回答攻方颜色**，于是 `IBattleField` 加了第五个成员 `AttackingArmyColor`。这是
   参考 `getAttackingArmyColor()` 的直接映射，也是单位对竞技场的最后一项反向依赖。
8. **士气语义照抄**：好士气清 `Moved|MoraleGood`（还回一次行动），坏士气清 `MoraleBad` 再打 `Moved`（吞掉这次）。
   两条都校验「单位确实在等这个答案」，否则抛异常。

**后果**：新增 `BattleArena.Action.cs`；`BattlePosition` 加 `GetReachable`；`BattleUnit` 加两个 `UpdateDirection`；
`IBattleField` 加一个成员（三个测试桩同步补上）。测试 11 条新增。Game 2482 → **2493**，全解决方案 3239 → **3250**，
`-warnaserror` 0 警告。覆盖率 `battle` 7/5/6 → **7/6/5**，合计 48/68/136 → **48/69/135**。

**遗留**：攻击（`calculateAttackTarget`、反击、`TargetsApplyDamage`）、法术、塔与投石车的执行、撤退/投降（需
`World` 的王国入口）、`Turns`/`UnitTurn` 的回合循环、界面与 AI。

## ADR-158：`world` 第三批——胜负条件、英雄查询与按英雄寻路 ◐

**背景**：`world.cpp` 里「谁赢了」这一段（`941-1157`）是 `world` 目录最后一块纯逻辑：地图文件头给出条件位掩码与
参数，`World` 拿当前局面去比对。它上接 `battle` 的结束判定与 `kingdom` 的存亡，下接 AI 的「我还有没有赢面」。
`getDistance`/`getPath` 则是参考给英雄留的唯一两个寻路入口（`1159-1169`），界面、AI、`Route` 都从这里走。

**决定**：

1. **条件位掩码与其参数翻译单独成两层**。`GameOver`（`Game/GameOver.cs`）只有常量，一字节对一位；`MapConditions`
   （`Game/Maps/MapConditions.cs`）把 `Mp2FileInfo` 的两个条件块翻成「位掩码 + 坐标/神器/千枚金币/天数」。
   翻译不放在 `Assets` 里：`FHeroes2.Assets` 不依赖 `FHeroes2.Game`，而 `GameOver` 的位定义属于游戏语义。
2. **文件头存在世界里，不每次去 `Settings` 拿**。参考每回判定都问 `Settings::Get().getCurrentMapInfo()`；端口的文件头
   就是 `LoadMap(map)` 的入参，`World.MapInfo` 收下它，`Reset()` 清掉。好处是判定在测试里不需要初始化单例，坏处是
   「运行期改 `Settings` 里的文件头」这类参考行为端口看不见——目前没有这种调用者。
3. **「实际在场的颜色」直接问王国**。`WINS_ALL`/`WINS_SIDE` 在参考里问 `Game::GetActualKingdomColors()`，它取的是
   `Settings` 里的玩家槽；端口还没有开局流程去填槽，而槽里的颜色正是同一份文件头建出来的，于是改用
   `Kingdoms.GetColors()`（颜色不为 `None` 的槽）。
4. **`WINS_SIDE` 在没有玩家槽时把自己当盟友**。参考的 `Players::GetPlayerFriends` 直接解引用玩家槽，没有槽就崩；
   端口在没有槽（`None`）时退化成 `friends = kingdom.Color`，等价于「单人、没有热座共享」的取值。
5. **`WINS_HERO` 的方向照抄参考，不做「直觉修正」**：`hero.isAvailableForHire() || GetKingdom(hero.Color).isControlHuman()`。
   读起来像是「英雄被电脑带走就赢」，但参考要的正是这一条，测试把它钉死，免得下一个人凭直觉改成 `!isControlHuman`。
6. **缺英雄不让装载失败**。参考在 `setHeroIdsForMapConditions()` 里找不到英雄就返回 `false`，整张图装载失败。端口的
   `LoadMap` 契约是「坏输入抛异常」，而这时世界已经装了一半，抛出去只会留下半个世界；于是记一条 `GameLog.Error` 并
   继续，效果是该条件永远不满足。等有开局错误对话框时把这里改成让装载失败。
7. **战役的两条特殊条件跳过**。`Campaign` 模块还没有：「占龙城即胜」与「女巫村全失即负」两条分支不写，`Switch` 里
   也没有它们的位置——写了只会是永远为假的死代码。
8. **`GetDistance`/`GetPath` 由 `World` 拆五个值喂寻路器**。参考有 `reEvaluateIfNeeded( const Heroes & )` 的重载；
   端口的寻路器只收拆开的五个值（它出现时 `Hero` 还没有移动力），所以这两个入口负责用户侧的便利。英雄不在图上时抛
   `ArgumentException`，与「非法输入不静默」的既定做法一致。
9. **`getCastleEntrance` 补一个以 `Point` 为参数的版本**，先查图内再换算下标。条件里存的正是 `Point`，而且可能是
   (-1, -1)（条件没有坐标时），参考那版对图外的点会断言。
10. **`HeroModes.NotDismiss` 补上**（`heroes.h:212`，`0x2000`）。它是条件英雄的「不许解雇」标记，与 `Custom` 一起
    打在守的那位英雄身上。

**后果**：新增 `Game/GameOver.cs`、`Game/Maps/MapConditions.cs`、`Game/World/World.Conditions.cs`；`World` 加
`MapInfo`/`GetHeroesCondWins`/`GetHeroesCondLoss`/`KingdomIsWins`/`KingdomIsLoss`/`CheckKingdomWins`/
`CheckKingdomLoss`/`GetKingdom`/`GetDistance`/`GetPath`/`GetCastleEntrance(Point)`，`LoadMap` 末尾接上
`SetHeroIdsForMapConditions()`；`Kingdoms` 加 `GetColors`/`FindWins`；`HeroModes` 加 `NotDismiss`。
测试 38 条新增（`MapConditionsTests` 18、`WorldConditionsTests` 20）。Game 2493 → **2531**，全解决方案 3250 →
**3288**，`-warnaserror` 0 警告。覆盖率不变：`world` 仍是 1/4/0（`world.cpp` 本来就记 ◐，这一批填的是它的内部函数，
没有让任何文件变 ✔），合计仍 **48/69/135**。

**遗留**：`world.cpp` 的 `NewMonth`/`_monthOfMonstersAction`、`getCurrentRumor`、
`fixFrenchCharactersInStrings`、`generateBattleOnlyMap`；AI 侧的王国动作与 `world_loadmap` 里的可占领对象表与终极
神器的埋放；战役的两条特殊条件。

## ADR-159：`world` 第四批——挖终极神器 ◐

**背景**：`world.cpp:850-873` 的 `DiggingForUltimateArtifact()` 是玩法里最直白的一步：英雄花一天挖一格，地上留一个
坑；坑下面有神器就归他。它需要的只有两样东西——坑的贴图（`MP2::getDiggingHoleSprite()`，`mp2.cpp:937`）和神器埋在
哪儿（`_ultimateArtifact`）。同一段里还有一个跨王国的「这个对象被碰过吗」（`isAnyKingdomVisited()`，`world.cpp:1272`）。

**决定**：

1. **坑贴图表放 Game 层，`Game/Maps/DiggingHole.cs`**。参考的表在 `mp2.cpp`，参数是 `Maps::Ground`；端口的
   `MapGround` 在 `FHeroes2.Game.AdventureMap`，`FHeroes2.Assets` 不能反向依赖它，所以表跟着枚举走。
2. **水面挖不了：返回 `false`，不 assert、不留对象部分**。参考在 `getDiggingHoleSprite` 的 `WATER` 分支和调用处各写
   一个 `assert(0)`；端口把「挖不了」当成正常输入处理，因为地图上水面就在那里，英雄站在船上挖是玩家会做的事。
3. **坑是地形层的一个部分，带新 UID**。它不是纯装饰：地形层决定通行与渲染，UID 让它和同一格上别的对象区分开。
4. **神器只标「已找到」，不从地图上移除**。参考的 `_ultimateArtifact` 记的是位置，不是地图对象；`markAsFound()` 之后
   同一格再挖返回 `false`，坑照留。
5. **神器归世界所有**（`World.UltimateArtifact`，`world.h:358`），因为它同时回答三个问题：`WINS_ARTIFACT` 条件、
   法师之眼的拼图提示、挖掘。归英雄会因为换手而丢。
6. **`isDiggingHoleSprite` 先保留、暂不接线**。它唯一的调用者是拼图模型的 `MP2::isHiddenForPuzzle()`，那个模型没移植；
   两张表是一对，拆开放反而要将来再找回来。文件注释写明了这一点，等拼图落地时接上。
7. **神器埋放不进这一批**。装载器要读对象的扩展 metadata（半径 + 神器 id）并挑格子（`setUltimateArtifact()`，
   `world_loadmap.cpp:1596-1662`），与 metadata 一起做更完整；在此之前 `UltimateArtifact.Position == -1`，
   `DiggingForUltimateArtifact()` 永远挖不到东西——这正是「地图里没有埋宝」的正常状态。
8. **`IsAnyKingdomVisited` 只是委托**。参考自己循环王国，端口那份循环已经在 `Kingdoms.IsAnyVisited`（`kingdom.cpp`），
   这里换一次参数顺序（参考是 `(type, index)`，端口是 `(index, type)`）交给它，不复制第二个循环。

**后果**：新增 `Game/Maps/DiggingHole.cs`；`World` 加 `_ultimateArtifact`/`UltimateArtifact`/
`DiggingForUltimateArtifact`/`IsAnyKingdomVisited`，`Reset()` 清神器。测试 7 条（`WorldUltimateArtifactTests`）。
Game 2531 → **2538**，全解决方案 3288 → **3295**，`-warnaserror` 0 警告。覆盖率不变：`world` 仍 1/4/0，合计仍
**48/69/135**。

**遗留**：`setUltimateArtifact()` 与它需要的对象 metadata、拼图模型（接 `DiggingHole.IsSprite`）、`NewMonth`/
`_monthOfMonstersAction`、`getCurrentRumor`、`generateBattleOnlyMap`。

## ADR-160：`world` 第五批——周与月、埋宝、可占领对象、战棋世界与法文文本 ◐

**背景**：ADR-159 收尾时点名的四条尾巴（`setUltimateArtifact`、`NewMonth`/`_monthOfMonstersAction`、
`generateBattleOnlyMap`、`fixFrenchCharactersInStrings`）加上 `world_loadmap` 的可占领对象表，是 `world`
目录最后一批还能靠现有系统立起来的函数。`getCurrentRumor` 是唯一一个依赖外部字符串系统的（见第 8 条）。

**决定**：

1. **周与月的钟表照抄参考的取模**：`BeginWeek()` 是 `day % 7 == 1`、`BeginMonth()` 是 `day % 28 == 1`
   (`world.h`)，时钟从 0 起，所以**第 8 天才开第二周、第 29 天才开第二月**；`NewDay` 先 `++_day`，再按两位
   推进 `_week`/`_month`，然后按「月 → 周 → 城/王国」的顺序调。第一周不刷新周命对象（参考的 `_week > 1`
   守卫），但招募池的清空在第一周也做——那两句在 `NewWeek` 的守卫之前。
2. **`World.Week.cs` 装日历与怪物月、`Maps/TileWeekLife.cs` 装瓦片刷新**。`updateObjectInfoTile()`
   (`maps_tiles_helper.cpp:1641`) 是瓦片自己的事（魔法花园 5 宝石或 500 金、水车首日 500 之后 1000、风车
   循环抽到非木再给 2 份、二十种住宅加人口），跟着 `Tile` 的其它 partial 走比塞进 `World` 更贴近参考。
3. **埋宝叫 `PlaceUltimateArtifact` 而不是 `setUltimateArtifact`**：端口已经有 `World.UltimateArtifact` 属性，
   C# 里属性和方法不能同名。行为逐条对齐 `world_loadmap.cpp:1596-1662`：找 `ActionRandomUltimateArtifact`
   占位物 → 读 metadata（`[0]` 半径、`[1]` 神器 id）→ 占位物与其**左格影子**一起用 `RemoveObjectPartsByUID`
   移除 → 半径 0 表示全图、半径 >0 时取 `GetAroundIndexes` 的邻格加自身 → `Width / 9` 的边缘一律不用 →
   挑不到就落回占位格。装载时在瓦片定稿之后、`PostLoad` 建缓存之前调用（`world_loadmap.cpp:1451`）。
   `UltimateArtifact` 归 `World`（ADR-159），这里只负责放。
4. **`SetMonster` 改存 `ActionMonster`**。这是一处真 bug：端口一直写非动作值 `Monster`，而
   `NewWeek` 的怪物成长、怪物月的邻格排除、`GetMonstersProtectingTile` 的落格判定都按 `ActionMonster` 找，
   于是「中立怪每周成长」与「怪物月避开已有怪」永远不生效（更早的寻路测试也只好用 `Monster` 当挡路样本）。
   参考的 `setMonsterOnTile()` 写的是 `MP2::OBJ_MONSTER`（带动作位，`maps_tiles_helper.cpp:1727`），与
   ADR-075「瓦片存文件给的值」一致，所以现在写 `ActionMonster`；寻路里那条 `Monster` 分支同步改成
   `ActionMonster`，两条既有测试（`TileTroopsTests`、`WorldPathfinderTests`）随之更正，
   `ReusesTheSearchWhenNothingChanged` 的期望也从「绕过去 >300」变成「怪物守护的格不再展开 → 0」，正是参考的
   `processCurrentNode` 行为。
5. **可占领对象表照表逐项落**：锯木厂 `0x00`、炼金实验室 `0x01`、五座矿 `0x02…0x06`、灯塔 `0x64`、龙城
   `0x65`、废矿 `0x67`，一律 `PlayerColor.None`；解析器已经把字节换成对象类型，所以端口只判「未知类型」
   （`Mp2ObjectType.None`）与「越界」两件事，各留一条警告并跳过。**不在这里写 ownership flag**——参考也不写，
   flag 归信息块（`world_loadmap.cpp:498` 那段）。
6. **只打一场的世界不经过 `LoadMap`**。`GenerateBattleOnlyMap` 直接调 `GenerateUninitializedMap(2)`、自己给
   四个瓦片编号、再 `SetUniformTerrain`（`world.cpp:338-357`）。绕开 `LoadMap` 有两个理由：参考的
   `generateUninitializedMap` 不做埋宝，而端口的 `LoadMap` 会；2×2 也不是合法的 MP2 尺寸。
   `SetUniformTerrain` 取 `MapGrounds.StartImageIndex`（参考是 `getTerrainStartImageIndex`），并把 shape 位清 0。
7. **法文文本修复：表是纯函数，门控交给调用方**。表落在 `MapTextCodec.FixFrenchCharacters`（`ui_font.cpp:7190`
   的 16 项，返回新串而不是原地改字节）；`World.FixFrenchCharactersInStrings` 覆盖英雄名、城堡名、传闻、每日
   事件、指示牌/漂流瓶、地面事件与斯芬克斯的谜面与答案。参考只在**界面语言是法语**时从装载器调用它，端口没有
   语言层，所以不在 `LoadMap` 里自动跑——对英文/中文地图跑一遍会把标点变成重音。为此把
   `MapSign.Message`/`MapEvent.Message`/`MapSphinx.Answers`/`MapSphinx.Riddle`/`EventDate.Message` 的属性从
   `private init` 放宽到 `private set`（构造仍走各自的 `TryRead`，外部看不出区别）。
8. **`getCurrentRumor` 不转**。它四分之一的分支要 `UltimateArtifact.GetName()`（神器名表）和区域名
   （`getUltimateArtifactRegion()` 一类），这两套字符串属于资源/本地化模块，端口都还没有；半截实现只会返回
   `UltimateBook` 这样的枚举名。等神器名表落地再补，届时它是一段纯粹的拼接。

**后果**：新增 `Game/World/World.Week.cs`、`Game/World/World.UltimateArtifact.cs`、
`Game/World/World.FrenchText.cs`、`Game/Maps/TileWeekLife.cs`;`World.LoadMap` 加埋宝与可占领对象表，`NewDay`
接上周/月，`World` 加 `GenerateBattleOnlyMap`/`SetUniformTerrain`/`FixFrenchCharactersInStrings`；
`MapTextCodec` 加 `FixFrenchCharacters`，`Funds` 加 `Rand`，`Kingdoms` 加 `ResetRecruits`，`AllHeroes` 加
`ResetModes`。新增测试 31 条（`WorldWeekTests` 9、`WorldCaptureObjectsTests` 10、`WorldTests` 3、
`WorldSphinxAndEventsTests` 5、`MapTextCodecTests` 4）。Game 2538 → **2565**，Assets 412 → **416**，全解决方案
3295 → **3326** 全绿，`-warnaserror` 0 警告。覆盖率不变：`world` 仍 1/4/0，合计仍 **48/69/135**（`world.cpp`
与 `world_loadmap.cpp` 都还在「部分」里，剩下的是 AI 侧与区域）。

**遗留**：`getCurrentRumor`（要神器名表与区域名）、`world_loadmap` 的 AI 区域、`Kingdom::ActionAI*` 那一半王国
动作、战役的两条特殊条件、拼图模型（接 `DiggingHole.IsSprite`）。

## ADR-161：`ai` 的契约层——先把签名立起来 ◐

**背景**：`ai` 目录的九个文件（`ai_battle`/`ai_common`/`ai_hero_action`/`ai_personality`/`ai_planner` 及其
`ai_planner_{castle,hero,kingdom,internals}`）此前一个都没转（`ai_personality.h` 的枚举除外，它落在
`System/AiPersonality.cs`）。这些文件的好处是**接口几乎全在头文件里**：`ai_planner.h` 的 281 行里公开契约与
数据结构占了 160 行，`ai_battle.h`/`ai_common.h`/`ai_hero_action.h` 同理。因此先把签名照抄成 C#，让后面写
`ai_planner_hero.cpp` 那几千行动作代码时有地方落、有地方调，比边写边改签名便宜。

**决定**：

1. **只落公开契约，不落私有方法**。`Planner` 有三十来个小方法（`getObjectValue` 的值表旁支、`evaluateRegionSafety`、
   `getMineralValue`、`followHero` 之类），它们全是 `ai_planner_{hero,castle,kingdom}.cpp` 的内部细节：没有跨文件
   调用者，参数形状也依赖尚未落地的东西（对象价值表、AI 寻路器）。提前声明等于猜，落地时必然改签名，所以干脆
   不写——公开契约是「谁调用 AI」，私有方法是「AI 怎么写」，后者只能在写实现的那一批一起出现。
2. **`fheroes2::GameMode` 折成 `AiTurnOutcome`**。`Planner::KingdomTurn()` 返回 `GameMode`，但它只会产出两个值：
   `END_TURN` 与 `QUIT_GAME`（AI 认输、自动战斗超时）。端口还没有 `GameMode`（场景是 `App/SceneKind` + 各 Scene
   自己决定下一幕），把整个 `game_mode.h` 拉进来只为了两个值不值得，因此定义 `AiTurnOutcome { EndTurn, QuitGame }`，
   并在文档里写明它对应哪两个参考值。等游戏的回合循环（`Game::StartGame`）落地，如果那时确实需要完整的
   `GameMode`，换过来只是一处 `switch` 的改动。
3. **`HeroBase&` 折成 `IArmyCommander`**。`HeroesPreBattle(HeroBase&, bool)` 的实参既可能是冒险地图英雄，也可能是城堡
   队长；参考用基类统一，端口没有 `HeroBase`（`Hero` 与 `Captain` 各写各的），但两者都实现了
   `IArmyCommander`（战斗侧已有的接口）。签名取 `IArmyCommander`，注释里说明「实现若真需要英雄专属成员再窄化
   成 `Hero`」。
4. **`Battle::Actions&` 折成 `List<BattleCommand>`**。参考的 `BattlePlanner::BattleTurn(arena, unit, Actions&)` 往一个
   动作队列里塞若干命令，由战斗循环稍后执行；端口没有队列，`BattleArena.ApplyAction(BattleCommand)` 是一次一条。
   所以 `BattleTurn` 收一个 `List<BattleCommand>`，语义一致（一次决策一整回合，按序执行），只是容器换了。
5. **真行为照写，其余 `throw new NotImplementedException`**。契约层不是一行不写：`RegionStats` 的默认值
   （`highestThreat = -1`、`spellLevel = 2`）、`BudgetEntry.Reset()`、`PriorityTask` 的构造函数、
   `SpellcastOutcome.UpdateOutcome()`（含「群体法术累加、单体取最大、不碰 `destinationCell`」这三条）、四个
   `ARMY_ADVANTAGE_*` 常量都是照参考抄的真代码，可以直接写。只有「要读地图/要算价值/要改游戏状态」的成员才抛，
   且抛的消息里带参考文件名，方便 grep 回源头。
6. **单例照旧**：`AiPlanner.Instance`（`Planner::Get()`）与 `BattlePlanner.Instance` 都是懒汉单例，与参考一样保存
   一整个回合的状态（优先级目标、敌人军队、预算、寻路器缓存）。`ResetPathfinder()` 现在是空实现——AI 侧寻路器
   （`AIWorldPathfinder`）还没转，planner 手里没有可清的东西；玩家侧的 `World.ResetPathfinder()` 已接（ADR-091）。
7. **`AIWorldPathfinderStateRestorer` 不落**（`ai_planner_internals.h:32-56`）。它的全部工作就是保存/恢复
   `AIWorldPathfinder` 的两个开关（`isOnBeach` 一览、`debug`），没有那个类它无事可做；`ai_planner_internals.h`
   这一批只落四个常量。
8. **文件名对齐参考**：`AiPlanner.cs`（`ai_planner.h`）、`AiPlannerTypes.cs`（同头里的五个数据结构）、
   `AiCommon.cs`、`AiBattlePlanner.cs`（`ai_battle.h` + 那个自由函数 `getBerserkerTarget`）、`AiHeroAction.cs`、
   `AiPlannerInternals.cs`。放在 `Game/Ai/` 而不是新开项目：契约依赖 `Kingdom`/`Castle`/`Hero`/`Army`/`Funds`/
   `UltimateArtifact`/`Tile`，全部在 `FHeroes2.Game` 里。
9. **`dialog` 这一轮不写代码骨架**，理由与替代路径见 ADR-162。

**后果**：新增 `Game/Ai/AiPlanner.cs`、`AiPlannerTypes.cs`、`AiCommon.cs`、`AiBattlePlanner.cs`、`AiHeroAction.cs`、
`AiPlannerInternals.cs`（6 个文件，约 640 行）。新增测试 13 条（`AiContractTests`）：三个枚举值、`RegionStats` 的
未求值状态、`BudgetEntry.Reset`、`EnemyArmy` 的两个构造、`PriorityTask` 的两种构造、`AiCastle` 的空值检查、
`UpdateOutcome` 的三条语义、两个单例、四个常量、一条「未移植的成员会显式抛」。Game 2565 → **2578** 全绿，
全解决方案 `-warnaserror` 0 警告。覆盖率不变（`ai` 仍 0 个整篇落地：`ai_personality.cpp` 在 `System/` 里只落了
枚举与随机/取名两个函数）。

**遗留**：`ai_battle.cpp`/`ai_common.cpp`/`ai_hero_action.cpp`/`ai_planner*.cpp` 的实现（契约已就位，填实现不需要
再动签名）；`AIWorldPathfinder`（契约里的 `ResetPathfinder` 等它）；`AiCommon` 的 `Funds?` 返回值等
`Economy`/市场规则落地后一起校。

## ADR-162：`dialog` 暂不写代码骨架——它是阻塞式门面，端口是场景式 ◐

**背景**：用户希望 `ai` 与 `dialog` 都先把接口写好。`ai` 的头文件天然是契约，照抄即可（ADR-161）；`dialog` 不是：
`dialog.h` 的 40 余个函数全是**阻塞式**的——`int SelectCount(...)` 内部跑一遍事件循环、返回按钮位；
`Dialog::GameInfo()` 自己读 `world.GetKingdoms()` 并画完整个窗口。端口从第一个 `SettingsDialog` 起就没有
事件循环：对话框是**不可变模型 + Scene 分派事件**（`Area`/`Buttons`/`Render`，按下哪个按钮由 Scene 读
`ButtonGroup` 的返回值）。照抄 `dialog.h` 的签名会得到一堆「阻塞函数」，与端口范式正面冲突，等于写一批注定要
重写的死代码。

**决定**：

1. **不照抄 `dialog.h` 的函数签名**。端口里「一个对话框」= 一个模型类（`Create(...)` + `Area` + `Render`）+
   一个结果表达（`DialogButton` 或该对话框自己的枚举），事件由 Scene 处理。`SettingsDialog`、`MessageBox`、
   `ScenarioSelectionDialog`、`LanguageSelectionDialog` 都已经是这样，`dialog.h` 的 24 个文件落地时照此办理。
2. **公共结构层已经齐了，不用再造**：`dialog.h` 里的 `ResizableFrameBox`/`FrameBox`/`FrameBorder` 在端口对应
   `Dialogs/FrameBox.cs` 与 `Dialogs/FrameBoxChrome.cs`（参考的继承层次被折成了组合，行为已在
   `FrameBox` 的构造与 `Chrome` 的九宫格里）；`StandardWindow`/`StandardWindowChrome` 对应 9-patch 窗口。
   所以 dialog 的「接口」不缺口在这一层。
3. **真正的缺口是控件**。24 个对话框全都站在 `fheroes2::DialogElement`/`TextBase`/`Slider`/`Scrollbar`/
   `OptionButtonGroup`/`TextBox` 上，这些一个都没转（端口现有 `Button`/`ButtonGroup`/`ScrollSlider`/`ButtonSprite`）。
   没有它们，`ArmyInfo`、`Marketplace`、`SelectCount` 的签名连参数都写不出来（`SelectCount` 收
   `const DialogElement* topUiElement`，`inputString` 收两个 `TextBase`），硬写只能靠猜控件形状，必然是返工。
4. **顺带确认的既有原则**：`Dialogs/DialogButton.cs` 的注释写着「只落四个按钮位，布局位（`Dismiss`/`Upgrade`/
   `Prev`/`Next`）跟用到它们的对话框一起加，不提前造值」。这条正好适用于现在的局面：`dialog.h` 的
   `DISMISS/UPGRADE/MAX/PREV/NEXT/WORLD/PUZZLE/INFO/DIG` 九个位值虽然照抄无风险，但没有任何调用者，先加就是死值。
   等第一个用到它们的对话框落地时一起补，那时还能顺手为 `ButtonGroup` 的布局参数写测试。

**后果**：本轮 `dialog` 目录零代码改动。下一次动 `dialog` 的正确顺序是：先落**控件契约**
（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/`OptionButtonGroup` 的端口形态，含 `Rect` + 渲染 + 事件三件套），
再按 `dialog.h` 的清单从最小的对话框（`ArtifactSetAssembled`、`BuyBoat`）开始验证范式，最后才是 `ArmyInfo`/
`Marketplace`/`KingdomOverview` 这些互相牵连的大块。

**遗留**：`dialog` 的 29 个文件全部未转；它们的共同前置是上面那批控件。

**后续**：第 3、4 条的判断没有变，但用户选择先要入口契约再做控件层，因此 `dialog.h` 的 24 个入口按
「模型 + `Create` + 结果类型」直接落下，见 ADR-163。`DialogButton` 的布局位也随之补齐——理由从「等调用者」
变成「已经有调用者」。上面「下一次动 `dialog` 的顺序」一条作废。

## ADR-163：`dialog` 的 24 个入口契约层 ◐

**背景**：ADR-162 判断 `dialog` 的代码骨架要等控件层，用户选择反向推进：先把 `dialog.h` 的 24 个入口按
「模型 + `Create` + 结果类型」落成可编译的契约，控件参数先占位，接受后续返工。理由也站得住——入口形状决定
`App` 层的场景怎么调它，这件事不依赖 `DialogElement` 长什么样。

**决定**：

1. **24 个入口落成 20 个模型文件**（另扩 `DialogButton`），放在 `src/FHeroes2.UI/Dialogs/`，命名与参考的
   `.cpp` 对齐：
   `AdventureOptionsDialog`（`dialog_adventure.cpp`）、`FileOptionsDialog`（`dialog_file.cpp`）、
   `SelectFileDialog`（`dialog_selectfile.cpp`，两个入口一个类）、`QuickInfoDialog`（`dialog_quickinfo.cpp`，七个
   入口一个类）、`LevelUpDialog`（`dialog_levelup.cpp`）、`GoldOrExperienceDialog`（`dialog_chest.cpp`）、
   `ArenaDialog`（`dialog_arena.cpp`）、`SelectCountDialog`（`dialog_selectcount.cpp`）、`InputStringDialog`
   （同文件）、`ArmySplitDialog`（同文件）、`RecruitDialog`（`dialog_recruit.cpp`）、`DwellingInfoDialog`（同）、
   `ArmyInfoDialog`（`dialog_armyinfo.cpp`）、`ArmyJoinDialog`（同，两个入口一个类）、`MarketplaceDialog`
   （`dialog_marketplace.cpp`）、`GiftResourcesDialog`（`dialog_giftresources.cpp`）、`BuyBoatDialog`
   （`dialog_buyboat.cpp`）、`ThievesGuildDialog`（`dialog_thievesguild.cpp`）、`GameInfoDialog`
   （`dialog_gameinfo.cpp`）、`ArtifactSetDialog`（`dialog_artifact.cpp`）。`ResizableFrameBox`/`FrameBox`/
   `FrameBorder` 三个类不重复实现（`FrameBox`/`FrameBoxChrome` 已有，ADR-162 第 2 条）。
2. **模型只承载「输入 + 答案」，不写 `Area`/`Render`**。参考的对话框自己画、自己跑事件循环；端口的场景发事件、
   模型描述内容。所以这一批只定 `Create(...)` 与结果，布局与渲染等控件层（`DialogElement`/`TextBase`/`Slider`/
   `Scrollbar`/`OptionButtonGroup`）落地时补——**补的时候 `Create` 的签名不变**，因为它的参数全是业务值。
3. **`DialogButton` 补齐到参考的 14 个值**（`dialog.h:65-85`）：四个按钮位 + `Dismiss`/`Upgrade`/`Max`/`Prev`/
   `Next`/`World`/`Puzzle`/`Info`/`Dig`。这不再是「提前造值」：`AdventureOptions` 的答案就是 `WORLD|PUZZLE|INFO|DIG`，
   `ArmyInfo` 的按钮就是 `UPGRADE|DISMISS`，有真实调用者。数值照抄参考，测试逐值钉住。
4. **`GameMode` 折成 `FileOptionsResult`**。`Dialog::FileOptions()` 返回 `fheroes2::GameMode`，但它只产出
   `NEW_GAME`/`LOAD_GAME`/`QUIT_GAME`/`CANCEL` 加上两个存档动作（`EventSaveGame`、`Game::QuickSave`），
   `MAIN_MENU`/`LOAD_STANDARD`/战役值都不会出现。端口按场景组织，「存档」「快速存档」是两件不同的事，所以结果
   是六个值：`Cancel`/`NewGame`/`LoadGame`/`SaveGame`/`QuickSave`/`Quit`。
5. **结果类型一律表达参考的返回语义**：返回 `bool` 的（`SelectCount`/`inputString`/`SelectGoldOrExp`）改成可空的
   值（`int?`/`string?`/`GoldOrExperience`），返回 `-1`/`UNKNOWN` 表示取消的改成 `null`（`MakeGiftResource`、
   `LevelUpSelectSkill`、`SelectSkillFromArena`），返回空 `Troop` 的改成 `Troop?`（`RecruitMonster`）。理由一致：
   参考用哨兵值表达「没有」，C# 有 `null`，用 `null` 可以少一类误读。
6. **`FromButton(int)` 是纯映射，属于真行为**。参考把「按了第几个按钮 → 返回什么」写成事件循环里的
   `if (le.MouseClickLeft(button[i])) return ...;`，端口把它抽成静态方法，逐条照抄按钮顺序，测试直接钉住
   （`AdventureOptions` 4 项、`FileOptions` 6 项、`BuyBoat`、`ArmyInfo`、`ArmyJoin`、`ArmySplit`、`ArmyInfo` 等）。
7. **参数校验按参考的断言补**：`SelectCount` 的 `min <= max`、`step > 0`（参考的 `step` 若为 0 会死循环），
   `ArmySplitTroop` 的两个非负值，`ArmyJoinWithCost` 的 `join > 0`。这些是 `Create` 里唯一的真逻辑，都写成
   `ArgumentOutOfRangeException`。
8. **三处占位，都在注释里点名**：（a）`inputString` 的 `fheroes2::TextBase` 用 `string` 代替（端口没有 `TextBase`）；
   （b）`SelectCount` 的 `topUiElement`/`bottomUiElement` 与 `ArmyInfo`/`RecruitMonster`/`QuickInfo` 的屏幕偏移
   `offsetY` 不建模（纯渲染参数）；（c）`SelectFileLoad`/`Save` 的文件列表不建模（存档目录还没有端口），
   `Choose(string?)` 先由场景把选中项写回。
9. **纯显示类不给结果**：`DwellingInfo`、`ThievesGuild`、`GameInfo`、`ArtifactSetAssembled` 只有 `Create`，
   因为参考返回 `void`，它们的效果全在画面上。

**后果**：新增 20 个模型文件（`src/FHeroes2.UI/Dialogs/`，1 225 行），`DialogButton` 从 4 个值扩到 14 个。
新增测试 25 条（`DialogModelsTests`：14 个位值、`AdventureOptions` 的两种按钮集与 6 条映射、`FileOptions` 的 6 条
映射、`BuyBoat`/`ArmySplit`/`ArmyJoin`/`ArmyInfo` 的映射与 `HasButtons`、`SelectCount` 的 clamp 与两条拒绝、
`inputString` 的上限、`Recruit` 的空栈、`LevelUp` 的未提供技能、`GiftResources` 的越界、`QuickInfo` 的三种目标）。
UI 145 → **170**，全解决方案 3 339 → **3 364** 全绿，`-warnaserror` 0 警告。覆盖率不变（`dialog` 仍 0/0/29）：这一批是契约，不是
实现。

**遗留**：全部 `Render`/`Area`/事件处理与控件层（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/
`OptionButtonGroup`/`TextBox`）；`PaymentConditions`（`BuyBoat` 的价格与能否购买）；存档目录（`SelectFile`）；
`QuickInfo` 的雷达与定位绘制；`GameInfo`/`ThievesGuild` 要读的世界数据（端口 `World`/`Kingdoms` 已有，实现时传）。

## ADR-164：`AIWorldPathfinder` 与玩家寻路器共用骨架 ◐

**背景**：`world_pathfinding.cpp:543-1434` 的 AI 侧一直以「要军队与对象价值表」为由押后。军队、逐格成本与
守怪强度（`WorldMapScan.IsTileProtectionStrongerThan`）就位后，它只剩自己的通行判定与成本记账，不必再等
对象价值表——那是 `ObjectValueStorage`，AI 计划器的事。

**决定**：

1. **继承 `WorldPathfinder`，不复制搜索**。参考的 `AIWorldPathfinder : PathfinderBase` 与玩家侧共用缓存、标签
   修正、邻居展开和「最后一格」；端口把同一骨架留在基类，只放开四个钩子（`ProcessWorldMap`、
   `ProcessCurrentNode`、`GetMovementPenalty`、`GetMaxMovePoints`）并让 `IsMovementAllowed` 可覆盖。基类
   `GetMaxMovePoints` 的「最后一格」判据在 AI 报 0 时自动失效，AI 侧不重写。
2. **AI 的三份重计算缓存放进 `WorldNode`**。参考把 `std::optional<bool> _isAccessibleForAI` 与
   `_isAvailableForWalkThroughForAI.{fromWater,fromLand}` 放在节点里，端口原样落成三个 `bool?` 字段。语义也照
   抄：`reset()` **不清**它们，整表失效发生在 `ProcessWorldMap` 的 `ClearCache()`——这一点容易看错，测试
   `TheWorldOwnsOneAiPathfinderAndHandsItToThePlanner` 因此改用可调参数观察 `Reset()`，而不是看距离。
3. **全局单例换成「世界持有 + 交给计划器」**。参考各处调用 `AIWorldPathfinder::Get()`，它背后是唯一的全局
   `world`。端口由 `World.AiPathfinder` 惰性创建，并在创建时 `AiPlanner.Instance.AttachPathfinder(...)`，让计划器
   仍有一个入口可取；`World.ResetPathfinder()` 重置这一份，避免多世界测试里重置到别人的缓存。
4. **没数据的成员不猜**。`ReEvaluateIfNeeded(Hero, double)`、`GetFogDiscoveryTile`、`GetNearestTileToMove`、
   `IsHeroPossiblyBlockingWay`、`BuildDimensionDoorPath`、`GetObjectsOnTheWay` 需要英雄↔城堡关系、集合点、
   装备或冒险地图对象扫描；一律抛 `NotImplementedException`，XML 注释写明缺哪份数据。
5. **`BuildPath` 的裁剪只在 `accountNearestObject` 时发生**。参考在回溯时用基类通行判定记下最后一个「不可穿越」
   的格，再把从它出发的步骤删光；`accountNearestObject == false` 时原样返回整条链。初版把裁剪做成了无条件的，
   会让「目标本身是拾取物」的路径少一步；本轮按参考改回，并补测试钉住两种形态。裁剪判定的水陆身份取**整条路
   起点**，不是逐格 `from`。

**后果**：新增 `src/FHeroes2.Game/World/AIWorldPathfinder.cs`（约 900 行）与 `WorldMapScan` 的守怪强度；`WorldNode`
多三个 `bool?`；`World` 多一个 `AiPathfinder`。新增测试 13 条（`AIWorldPathfinderTests`）。Game 2578 → **2591**，
全解决方案 3 364 → **3 377** 全绿，`-warnaserror` 0 警告。

**遗留**：`ai` 的九个 `.cpp` 实现（含 `ObjectValueStorage` 与 AI 计划器私有方法）；上面第 4 条那一串入口。

## ADR-165：城堡视图先落数据层，界面等渲染底子 ◐

**背景**：`castle` 目录的数值与规则早已落地，剩下的全是 `buildinginfo.cpp` 与五个界面 `.cpp`。界面要的控件层
（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/`TextBox`）与城堡屏都还没有，整块搬过来会一行也跑不通、
一条也测不了。但 `buildinginfo.cpp` 里有一半与贴图无关：`BuildingInfo` 的四个改写与两段文案、`DwellingsBar`
的条目与布局、以及住所描述要用的怪物复数名。

**决定**：

1. **先把与贴图无关的三块落下**：`Castle/BuildingInfo.cs`、`Castle/DwellingsBar.cs`、`Monsters/MonsterNames.cs`。
   绘制（`BuildingInfo::Redraw`/`Reset`）、弹窗（`QueueEventProcessing` 打开的招募与购买窗）留给城堡屏。
2. **`BuildingInfo` 是独立类，不是 `Castle` 上的方法**。参考即如此，且一个城堡要持有一屏建筑（每个带自己的
   位置与状态），挂在 `Castle` 上会把位置也塞进存档模型。
3. **状态只在两处换条件文案**。参考的构造函数仅对 `BuildDisabled`/`ShipyardNotAllowed` 调
   `GetConditionDescription()`；剩下五个分支（缺钱、已建、前置、可建、需要城堡）在这条路径上走不到。端口照抄
   这些分支并注释，不去「修正」——它们是界面别处的文案来源，擅自接上会改变状态栏的显示。
4. **`DwellingsBar` 不抽象成 `ItemsBar<T>`**。参考的 `ItemsBar` 是模板，目前只有一个用户；`KingdomOverview`
   将来也要用时再抽，现在抽只会为一个用户写一套泛型布局。
5. **`RenderingOffset` 重算整栏矩形**（唯一的行为偏离）。参考的 `setRenderingOffset()` 只重算图标位置，若先设
   表格再移动整栏，`getArea()` 就停在旧坐标。端口让两者一起更新：`Area` 回答的是「此刻」的栏，与 setter
   的调用顺序无关。代价是与参考在「中途移动」这一种情况下的 `getArea()` 不同。
6. **怪物名单独成表**。`MonsterStatistics` 明确只存数值（注释写着 minus the two names），名字放
   `MonsterNames`，与参考同一张表的顺序，`MonsterType.MonsterCount` 是长度并在静态构造里断言。复数取参考的
   原文（`Magi`/`Archmagi`/`Cavalries`），不做机械加 s。
7. **队长的矩形保留 `135x70`**。参考按 `ICN::getCaptainIcnId( race )` 的贴图尺寸算，端口在 `BuildingInfo` 附近
   拿不到贴图，注释写明等城堡屏接上渲染器再算。

**后果**：新增 `BuildingInfo`（约 230 行）、`DwellingsBar`（约 300 行）、`MonsterNames`（约 150 行数据）。
测试 21 条（`BuildingInfoTests` 10、`DwellingsBarTests` 7、`MonsterNamesTests` 4）。Game 2591 → **2612**，
全解决方案 3 377 → **3 398** 全绿，`-warnaserror` 0 警告。

**遗留**：`BuildingInfo` 的绘制与交互；`castle_building.cpp` 的 blit 与五个界面 `.cpp`；控件层与城堡屏。

## ADR-166：建筑绘制队列与淡入落在数据层，帧钟当参数 ◐

**背景**：`castle_building.cpp`（500 行）整份是绘制，但其中三块与 blit 无关：建筑淡入的 alpha 状态机、
「按绘制优先级 + 屏幕偏移」排好的矩形队列、以及绘制循环开头「这栋画不画 / 要不要补一段墙或桥」的判定。
不把它们落下，城堡屏接上渲染器时还要重新推一遍这些规则；而且这些规则里有一处（升级替代基础建筑）的例外
很容易做错。

**决定**：

1. **`FadeBuilding` 独立成类，帧钟当参数**。参考的 `updateFadeAlpha()` 先问
   `Game::validateAnimationDelay( CASTLE_ITEM_UPDATE_DELAY )`；端口没有帧循环，也没有延迟抽象（搜遍 `src` 无
   `DelayType`），所以 `Update( bool delayElapsed )` 把「这一帧是否到期」收成入参，返回值表示 alpha 是否推进。
   其余（0 起步、`< 246` 加 9 否则 255、`IsOnlyBoat`）照抄。
2. **队列在构造时就把 `top` 加进矩形**。参考 `BuildingsRenderQueue( castle, top )` 对每个 area 做 `area + top`，
   绘制侧不再补偏移。端口保持这个分工，`Areas` 一律是屏幕坐标——测试因此能直接断言偏移结果。
3. **判定抽成 `CastleBuildingRender` 的静态方法，而不是留给绘制循环**。参考把「未建不画」「升级版已建则不画
   基础」「升级版正在淡入则是例外」写在 `redrawCastleBuildings()` 的两个分支里，两处条件只差一个
   `upgrade != fadingInBuildingId`。端口合并成一个 `ShouldDrawBuilding( castle, building, fadingIn )`：
   `fadingIn == Nothing` 时第三条件恒真，等价于无淡入那支。连接件判定 `IsBuildingConnectionNeeded` 同样抽出，
   它只读 `Race`、`GetLevelMageGuild()` 与建筑位，不需要贴图。
4. **不搬 `CastleGetMaxArea` 与 `getTownIcnId`**。这两者要背景贴图的尺寸与 ICN 编号，端口在数据层拿不到；
   等城堡屏能从渲染器问尺寸时再加。
5. **blit 部分（`redrawCastleBuilding`/`redrawBuildingConnection`/`redrawAllBuildings`）不做**。它们的每个分支都
   以 `drawCastleDialogBuilding( icn, index, ... )` 结尾，没有 ICN 就无法验证任何一行。

**后果**：新增 `FadeBuilding`（约 110 行）与 `BuildingsRenderQueue`（约 180 行，含 `BuildingRenderInfo` 与
`CastleBuildingRender`）。测试 9 条（`CastleBuildingRenderTests`）。Game 2612 → **2621**，全解决方案
3 398 → **3 407** 全绿，`-warnaserror` 0 警告。

**遗留**：`redrawAllBuildings` 的三个绘制函数；`castle_dialog.cpp` 及其余四个界面 `.cpp`；控件层与城堡屏。

## ADR-167：魔法塔行与一键招满落在数据层与 UI 契约，钱与王国由调用方给 ◐

**背景**：`castle_mageguild.cpp` 与 `castle_well.cpp` 的主体是绘制循环，但有两个规则块与 blit 无关：魔法塔窗口
每层摆哪几个法术、水井 MAX 按钮怎么把国库分配掉。前者决定窗口内容，后者的分配顺序与「为什么买不了」的三句话
决定玩家看到的反馈，都是可测的规则。

**决定**：

1. **魔法塔的行数据放 `Castle`，窗口布局放 `UI/Dialogs/MageGuildDialog`**。行的槽位数来自
   `GetMaxSpellsCount( level, HasLibraryCapability )`（只看种族），法术本身来自
   `GetSpells( guildLevel, IsLibraryBuilt, level )`（种族且建了图书馆）——参考就是这样分开问的，端口照抄，
   不合并成一个「能不能多一格」的布尔。行列表保持等级顺序（`Rows[0]` 是一级），屏幕顺序由
   `GetRowTopOffset` 的 `365 - 90 * i` 解释，因为参考把一级画在底部。
2. **窗口不读王国**：参考用 `GetKingdom().GetCastles().size() < 2` 决定前后城按钮是否可点，端口 `Castle`
   没有王国，`Create` 收 `castleCount`。状态栏的两句话按「英雄有没有魔法书」二选一，与参考一致。
3. **一键招满做成纯规划对象**：`CastleMaxRecruitment.Create( castle, castleArmy, guestArmy, treasury )` 返回
   `Entries`/`Cost`/`Summary`/`Refusal`，不弹窗、不扣钱、不 `RecruitMonster`。参考用
   `GetKingdom().AddFundsResource()` 付账，端口让调用方在确认后自己扣，理由同 ADR-164 的 `BuyBuilding`。
4. **两个军是草稿副本**：参考把买到的兵 `JoinTroop` 进 `tempCastleArmy`/`tempGuestArmy`，好让后面的住所
   看见前一步占掉的槽位；端口保持这个副作用，并在文档里写明「传副本」。这也是为何它不能是 `Castle` 的纯函数。
5. **「为什么买不了」只看第一个有库存的住所**：参考在 `break` 上定了性，端口 `GetRefusal` 照抄；并且先判
   钱、再判队伍满，所以「既没钱又满编」报缺钱。`Summary` 的单复数按 `GetPluralName( count )` 的规则取。

**后果**：新增 `Castle/Castle.MageGuildView.cs`（约 60 行）、`UI/Dialogs/MageGuildDialog.cs`（约 110 行）、
`Castle/CastleMaxRecruitment.cs`（约 250 行）。测试 15 条（`CastleMageGuildViewTests` 4、
`MageGuildDialogTests` 4、`CastleMaxRecruitmentTests` 7）。Game 2621 → **2632**，UI 170 → **174**，
全解决方案 3 407 → **3 422** 全绿，`-warnaserror` 0 警告。

**遗留**：`castle_mageguild.cpp`/`castle_well.cpp` 的绘制循环；`castle_dialog.cpp`/`castle_town.cpp`/
`castle_tavern.cpp`；城堡屏与控件层。

## ADR-168：城堡屏只搬文字与返回值，招募英雄的句子抽成静态函数 ◐

**背景**：`castle_dialog.cpp`（859 行）与 `castle_town.cpp`（772 行）几乎全是绘制与事件循环，但里面夹着三处
会改变玩家所见所知的规则：悬停状态行怎么称呼一个住所、屏幕顶端的日期怎么写、招募英雄的窗口说什么话。

**决定**：

1. **状态行放 `BuildingInfo.GetStatusLine`，不放 UI**。它只依赖种族与建筑号，是「这栋房子是什么」的另一种说法，
   与 `GetBuildingDescription` 同类；`UI/Dialogs/CastleDialog` 只是转交。兵种取**复数小写**，因为参考用的是
   `StringReplaceWithLowercase`；随机城镇没有具体兵种，退化成 `"Recruit level 3 creatures"`。
2. **日期行与 `World.DateString()` 并存**。屏幕上是 `"Month: 1, Week: 3, Day: 5"`，日志里是小写形式；两个都是
   参考的原文，合并成一个会让其中一处失真。端口取三个整数作参数，不拉 `World`，便于测试。
3. **返回值八种，`DoNothing` 也算一种**。参考的 `CastleDialogReturnValue` 里「换一座城」「换一个窗口」与「关闭」
   并列，调用方必须能区分「关闭」与「关闭后去下一座城」，所以不做成 `bool` + 附加参数。
4. **招募英雄的文案抽成 `Describe( name, level, race, count )` 静态函数**。端口的 `Hero.Name` 只由 `LoadFromMP2`
   填，新造的英雄名字为空；写成静态函数后，测试能校验整句而不用伪造地图数据，实例属性只是把英雄的四个字段
   喂进去。
5. **魔法书从宝物计数里扣掉**：参考先 `GetCountArtifacts()` 再 `hasArtifact( MAGIC_BOOK )` 减一，因为书放在同一
   个背包里。不减会让「只有一本书」的英雄被说成「with 1 artifact」。三档文案是三条独立句子，不是带数字的模板。
6. **能不能招由调用方给**：`Castle::AllowBuyHero()` 要王国与访客英雄，窗口模型两个都没有，收 `canRecruit`。

**后果**：新增 `UI/Dialogs/CastleDialog.cs`（约 100 行）、`UI/Dialogs/RecruitHeroDialog.cs`（约 150 行），
`BuildingInfo` 追加 `GetStatusLine`（约 25 行）。测试 17 条（`BuildingInfoTests` 增 4、`CastleDialogTests` 4、
`RecruitHeroDialogTests` 9）。Game 2632 → **2636**，UI 174 → **187**，全解决方案 3 422 → **3 439** 全绿，
`-warnaserror` 0 警告。

**遗留**：`castle_dialog.cpp`/`castle_town.cpp` 的绘制与事件循环；`castle_tavern.cpp` 的传闻窗口（要等
`world.cpp` 的传闻池）；`buildinginfo.cpp` 的 `Redraw`/`QueueEventProcessing`；城堡屏与控件层。

## ADR-169：传闻池先立形状，酒馆窗口收一句话 ◐

**背景**：`castle_tavern.cpp`（54 行）只做两件事——取本周传闻、把它和开场句放进一个窗口。传闻来自
`World::getCurrentRumor()`（`world.cpp:632-703`），而端口的 `World` 已经有 `Rumors`（地图带来的原文）与
`WeekSeed`，独缺那个选择函数。按「先城堡、后世界」的顺序，这批要么把世界一起补完，要么让窗口不依赖世界。

**决定**：

1. **传闻池先立形状，函数体不写**。`World/World.Rumors.cs` 落 `StandardRumorCount`（= 9，`switch` 的分支数，
   不是地图上限）、九条内置文案、九个方向词、`TotalRumorCount`；`GetCurrentRumor`/`ChooseRumorId`/
   `GetRegionName` 抛 `NotImplementedException`，注释写参考行号与该做什么。这么做是因为池子的**内容**（文案、
   方向词、条数）是渲染与窗口真正要读的，而**选择**（周种子怎么喂给随机数、地图三等分怎么判边界）可以后补，
   两者的风险不同：前者错了整句都错，后者错了只是换了句传闻。
2. **未实现的函数显式抛异常，不返回空串**。返回空传闻会让酒馆安静地显示一句「…」，玩家与测试都看不出问题；
   抛异常至少会立刻暴露调用点。（更正：AI 层按 ADR-161 本来就有一组这样的桩，所以「唯一一处」的说法不成立；
  这里只是传闻池的首例，注释与文档都指向 ADR-169。ADR-170 已把它补齐。）
3. **语言标签单独成类型**。`LocalizedString`（`Text` + 可选 `SupportedLanguage`）对应参考的 `LocalizedString`
   （`game_string.h:30-50`）：内置九条由界面翻译、语言为空，地图带来的原文带自己的语言，渲染时该用对应字体。
   窗口现在就带着这个标签，字体层学会切换时不用改窗口。
4. **酒馆窗口收 `LocalizedString`，不读 `World`**。这样窗口与未实现的池子解耦：池子晚一点补，窗口照常可用。
   `Create` 拒绝空文本——载入器从不存空传闻，空的说明是 bug 而不是状态，拒绝比画一个空段落好。
5. **标题取建筑名而不是新常量**。参考是 `GetStringBuilding( BUILD_TAVERN )`，而六个种族都叫「Tavern」；端口用
   常量，但测试对着 `CastleBuildingInfo.GetName` 逐个种族核验，避免以后与表脱节。

**后果**：新增 `LocalizedString.cs`（约 30 行）、`World/World.Rumors.cs`（约 250 行，三个函数未实现）、
`UI/Dialogs/TavernDialog.cs`（约 90 行）。测试 13 条（`RumorPoolTests` 7、`TavernDialogTests` 6）。Game 2636 →
**2643**，UI 187 → **193**，全解决方案 3 439 → **3 452** 全绿，`-warnaserror` 0 警告。`castle` 统计 3/7/1 →
3/8/0：目录里已无完全未转的文件。

**遗留**：`World::getCurrentRumor()` 的三个函数体（`GetCurrentRumor`/`ChooseRumorId`/`GetRegionName`）；
`castle_tavern.cpp` 的 `TAVWIN` 动画与布局；`castle_dialog.cpp`/`castle_town.cpp` 的绘制与事件循环；城堡屏与控件层。

## ADR-170：传闻池补齐，句子抽成 `Describe` ◐

**背景**：ADR-169 只立了传闻池的形状，三个函数体抛 `NotImplementedException`。要让酒馆显示真传闻，就得实现
`World::getCurrentRumor()`；而它有两个外部缺口：终极宝物的名字，与自定义传闻的语言标签。

**决定**：

1. **句子抽成 `World.Describe( rumorId, artifactName, region, customRumors )` 静态函数**。参考是一个 `switch` 里
   边选边拼，端口把「拼」拿出来：九个分支与自定义回退都能在无地图、无周数、无宝物的条件下断言，实例方法只剩
   「这两个参数从哪来」。与 `RecruitHeroDialog.Describe`（ADR-168）同一手法。
2. **区域只在 id 为 1 时计算**。tile 下标要除地图宽度才换成坐标，而没载入地图的世界宽度为零；参考不判断是因为
   它总在地图里被调用，端口把这个前提收紧成「只有需要时才碰它」，于是那七句固定文案在空世界上也能回答。
3. **三等分用严格比较，且先判行再判列**。`height / 3 > y` 而非 `>=`，30 格地图的第 10 行已属中间带；行决定南北
   中，列再决定东西，所以中间带回答 `west`/`center`/`east` 而不是自己再切九份。测试钉住四条边界与小地图。
4. **宝物名字只覆盖八个终极 + 占位**。参考读 `getArtifactData( id ).untranslatedName`，那是 `artifact_info.cpp`
   的 104 行表；端口 `ArtifactTable` 只有数值列（脚本生成），`ArtifactBag.String()` 也因「还不画名字」没转。
   而地图只会埋八个终极之一（`UltimateArtifact::Set` 在没指定时从那一池里抽，编辑器也只提供「随机终极宝」），
   所以 `UltimateArtifact.Name` 只列这八个，表外（含 `Unknown`）返回参考自己的 `"Invalid Artifact"` 占位——
   与其编造一个会显示错的名字，不如露出参考的占位词。完整名字表属于文本层，将来连着 `ArtifactBag.String()`
   一起补。
5. **自定义传闻的语言标签返回空**。参考带 `getCurrentMapInfo().getSupportedLanguage()`；端口没有「地图语言」这个
   概念（`MapTextCodec` 解完码就丢，只留法文字符修正），所以给 `null` 而不是拿 `ResourceLanguage.Get()` 冒充——
   那是资材的语言、不是地图的语言，用错会让俄文地图配英文字体。`LocalizedString` 已经在位，等地图记住语言。
6. **越界抛异常而不是断言**。参考 `assert` 后直接索引，release 下是 UB；端口对 `rumorId` 越界与
   `totalRumorCount <= 0` 抛 `ArgumentOutOfRangeException`，消息里写出池子的构成。
7. **酒馆加深 `Create( World )` 重载**。窗口仍不读世界，重载只是替调用方做 `world.GetCurrentRumor()`；城堡屏接
   进来时不用改窗口，也不用在调用点重复这句。

**后果**：`World/World.Rumors.cs` 由骨架改为实现（约 330 行），`UltimateArtifact` 增 `Name`（约 30 行），
`TavernDialog` 增一个转发重载。测试 42 条（`RumorPoolTests` 增 31、`UltimateArtifactTests` 增 10、
`TavernDialogTests` 增 1）。Game 2643 → **2684**，UI 193 → **194**，全解决方案 3 452 → **3 494** 全绿，
`-warnaserror` 0 警告。传闻池里不再有桩（AI 层按 ADR-161 仍有成组的桩，与本批无关）。

**遗留**：地图语言（**已查明不需要补**：`maps_fileinfo.h:148` 的 `getSupportedLanguage()` 只对 Resurrection 地图
返回语言，经典 MP2 地图一律返回空——而端口今天只读 MP2，所以 `null` 就是正确答案，等 `.fh2m` 支持再说）；完整宝物名字表与
`ArtifactBag.String()`；`castle_tavern.cpp` 的 `TAVWIN` 动画与布局；`castle_dialog.cpp`/`castle_town.cpp` 的绘制
与事件循环；城堡屏与控件层。

## ADR-171：神器收入接进王国账本，补两个文案缺口 ◐

**背景**：`Kingdom.GetIncome` 的文档写着「神器与技能两项等英雄的神器袋与技能」，`IncomeType.Artifacts` 也写着
「英雄还没有神器袋，这一位今天加不出东西」——两句都过期了：`Hero.BagArtifacts` 早在 `hero` 批次就落地，
`ProfitConditions.FromArtifact` 也早就把 `artifact_info.cpp` 的收入与惩罚转完。另一边，周名
（`Week::GetName()`）与 `Monster::GetPluralName()` 的注释各自以一个「还缺依赖」的理由停在 ◐，而依赖也都已经在了
（`MonsterNames`、`WeekName`）。

**决定**：

1. **神器收入按整只袋遍历，空格不跳过**。参考是 `for ( const Artifact & artifact : bag )`，14 格全走；空格是
   `UNKNOWN`，表里那行没有 bonus，自然为 0。照做之后「魔法书算不算」这个问题消失了——魔法书是袋里的一格，
   不是另一样东西，它的行同样没有收入。
2. **求和允许为负**。税票 -250 与金袋 +1000 相加是 750；若英雄只带税票，这一位就是 -250。钳制是账本入账时的
   事（`AddFundsResource` 不让金库掉到零以下），`GetIncome` 只负责求和，否则「这一项到底多少」就说不清了。
3. **技能那一项仍留空，但理由改成实话**。英雄今天有袋子没有技能，`HeroSkills` 还是 0；文档从「袋子也没有」
   改成「有袋子、没技能」，并指向 ADR-112 的修正。
4. **周名返回参考的英文原文**，与 `Experience`（ADR-137）一致：端口没有翻译层，返回 key 会让调用处的句子
   （`"Astrologers proclaim the Week of the %{name}."`）读不通。两处反直觉照抄：怪物周返回生物单数名，瘟疫是
   全大写 `PLAGUE`（原版游戏就这么印）。表外回答 `Unnamed`——它只用于打印，与参考的 assert-then-default 同效。
5. **`MonsterNames.GetPluralName( count )` 取 `count == 1` 而不是 `count > 1`**：0 也用复数，参考就是这么写的，
   且 `"0 Peasants"` 正是对话框要的字。
6. **顺手更正两处错注释**，因为它们会让后来的人做错判断：`KingdomIncomeTests` 里「金鹅是唯一带收入的神器」
   （无尽钱袋 500/750/1000 也带），以及 `IncomeType`/`GetIncome` 的「英雄还没有神器袋」。

**后果**：`Kingdom.GetIncome` 增约 12 行，`Week` 增 `GetName()`（约 60 行含注释），`MonsterNames` 增
`GetPluralName`（约 13 行）。测试 13 条（`KingdomIncomeTests` 4、`WeekTests` 8、`MonsterNamesTests` 1）。
Game 2684 → **2697**，全解决方案 3 494 → **3 507** 全绿，`-warnaserror` 0 警告。`kingdom` 与 `monster` 两个
目录的统计不变（改动都落在已经是 ◐ 的文件里）。

**遗留**：`IncomeType.HeroSkills`（等英雄技能）；`IncomeType.CampaignBonus` 与电脑难度加成（等 `Players`/战役存档）；
`kingdom_overview`/`view_world`（渲染 + 对话框）；`monster_info.cpp` 的三支界面文案
（`getMonsterAbilityDescription`、`getMonsterWeaknessDescription`、`getMonsterPropertiesDescription`——ADR-172
补了 `Spell.GetName()`，ADR-173 把三支都转进 `MonsterDescriptions`；同文件的 `getMonsterDescription` 是英文
标签的调试转储，不面向玩家，端口以测试代替）。

## ADR-172：法术的名字与描述也返回参考原文 ◐

**背景**：`spell.cpp` 的 `spells` 表每行七列，`SpellTable` 只搬了五列，注释的理由是「名字与描述是 gettext
字符串，属于翻译层」。这个理由在 ADR-137（`Experience`）与 ADR-171（周名、怪物名）之后已经不成立：端口的做法是
**返回参考的英文原文、不返回 key**，翻译层等它自己的批次。

**决定**：

1. **新表 `SpellText`，一行两列**，与 `SpellTable` 的数值行同序同下标。不把两列塞进 `SpellStats`：数值表的用途是
   「对数字」的测试与战斗公式，文本进去了会让每个数值断言都拖着字符串。也不拆成名字表与描述表：参考里它们
   是同一行的两个字段。
2. **上下文前缀按显示规则去掉**。参考写 `gettext_noop( "spell|Slow" )`，`spell|` 只是给翻译者的上文，gettext
   显示时去掉；端口若照抄会在界面上印出 `spell|Slow`。全表只有 Slow 与 Blind 两处。
3. **参考的拼写优先于枚举名**：`FireBlast`→`"Fireblast"`、`StoneSkin`→`"Stoneskin"`。改词会与原版界面不一致，
   改 enum 会与存档 id 不一致，所以各留原样并在注释里点明。
4. **`%{count}` 不替换**。`Cure`、`Haste`、`Blind` 的句子带占位符，数字由法术自己的代码给；参考的
   `Spell::GetDescription()` 也只返回模板。
5. **六个随机占位与 Petrify 也进表**。它们不可施放，但表要能回答任意 id（调试窗口、坏存档），参考同样有句子。
6. **顺手改三处过时注释**：`SpellTable`/`Spell` 的「文案属于翻译层」，`SpellInfo` 对 `getSpellDescription` 的
   描述（它不只是拼句子，还要 `HeroBase`、最近的城堡与地图，行号也从 214-321 更正为 214-427）。

**后果**：新增 `SpellText.cs`（73 行表 + 两个方法，约 140 行），`Spell` 增 `GetName`/`GetDescription`，
测试新建 `SpellTextTests` 17 条。Game 2697 → **2714**，全解决方案 3 507 → **3 524** 全绿，`-warnaserror` 0 警告。
`spell` 目录统计不变（`spell.cpp` 仍因流序列化留在 ◐）。

**遗留**：`Spell::GetDescription()` 之外的 `getSpellDescription`（`spell_info.cpp:214-427`，要法术书屏）；
`spell.cpp` 的流序列化；`spell_book.cpp` 的绘制与输入循环。

## ADR-173：怪物信息屏的三支文案进 `MonsterDescriptions` ◐

**背景**：`monster_info.cpp` 的缺口一直被记成「四个描述函数（要 `Spell.GetName()` 与 `_()`）」。ADR-172 补上
`Spell.GetName()` 之后去核对，发现记错了：本版参考里面向玩家的只有三支，第四支 `getMonsterDescription` 是
带 `Name: / Base growth: / Cost:` 英文标签的调试转储，唯一调用者是开发者的工具而不是游戏。全游戏的调用点只有
一处——`dialog_armyinfo.cpp:431` 调 `getMonsterPropertiesDescription( troop.GetID() )`。

**决定**：

1. **三支函数一个文件**：`MonsterDescriptions` 装 `GetAbilityDescription`、`GetWeaknessDescription`、
   `GetPropertiesDescription`，加上它们共用的 `ReplaceMassSpells`/`ListSpells`。分到 `MonsterAbilities` 与
   `MonsterWeaknesses` 里会让「信息屏的一句话」这个概念被两处代码拼出来，而它其实是一个流程。
2. **`%{spell}` 就地替换，`%{count}` 不替换**。法术描述是模板（数字由战斗侧给），能力句里的法术名当场从
   `SpellText` 取，`"50% immunity to Fireball spell"` 是成品句子。数字同理：`"25% magic resistance"`。
3. **句尾不带句号**。`GetPropertiesDescription` 自己补 `.`，两支单句函数不补——照抄参考，否则分组行会有两个点。
4. **`MonsterAbilities.IsBasic` 与 `MonsterWeaknesses.IsBasic` 对称**：两格、飞行、龙、四种元素生物这八项
   信息屏一律不显示（玩家在战场上看得见）。`None` 也算隐藏项，因为忽略时它返回空串而不是 `"None"`。
5. **`Elemental` 是改写项不是隐藏项**：隐藏时返回 `"No Morale"`。这是参考的原文，也是玩家真正需要知道的那条。
6. **分组用有序容器**：参考是 `std::map<uint32_t, vector<int>>` + `std::set<int>`，端口用 `SortedDictionary`
   与 `SortedSet`，于是土元素的三条免疫合成 `"Immune to Lightning, Elemental Storm."`——连锁闪电因为
   闪电箭在场被 `replaceMassSpells` 去掉，剩下按 id 排序。闪电箭印作 `"Lightning"`，全表唯一的改名。
7. **额外伤害印总数**：表里存 100（+100%），句子是 `"200% damage from ..."`。
8. **调试转储明确不转**：`getMonsterDescription` 的标签是英文常量而非 gettext 串，转它只会骗过「文案已齐」
   的统计；端口用测试代替。

**后果**：新增 `MonsterDescriptions.cs`（约 300 行含注释），`MonsterAbilities` 增 `IsBasic`（约 20 行），
新建 `MonsterDescriptionsTests` 52 条。Game 2714 → **2766**，全解决方案 3 524 → **3 576** 全绿，
`-warnaserror` 0 警告。`monster` 目录统计不变（`monster_info.cpp` 里还剩只被战斗侧读的 `icnId`/`sounds`）。

**遗留**：`monster_info.cpp` 的 `icnId`/`sounds` 两张表（战斗批次；`icnId` 已在 ADR-174 转出）；`dialog_armyinfo.cpp`
的排版与绘制（已在 ADR-174 转出）；`spell_info.cpp` 的 `getSpellDescription`。

## ADR-174：军队信息窗口按「布局 / 美术 / 模型」三件拆开 ◐

**背景**：`dialog_armyinfo.cpp` 里有四种东西混在一个函数里——窗口与阴影的矩形算术、`ICN` 帧的选取、属性与描述
的文案、以及一个阻塞的输入循环。上一批（ADR-173）把文案备好之后，剩下的是排版。直接照抄成一个大
`ArmyInfoDialog.Draw()` 会把「数字」和「像素」焊死：没有显示器的测试只能整块跑一遍，偏移错一个像素也说不清
是哪一步。

**决定**：

1. **`ArmyInfoLayout` 只算矩形**。窗口、阴影、`pos_rt` 三个矩形，加上属性块、名字带、描述块、数量框与三个
   按钮的矩形，全部来自参考匿名 namespace 的常量。`Create( screen, ... )` 是纯函数，测试拿 640×480 一串
   数字就能核对 60/112/460/149 这些值，不需要 `PixelBuffer`。
2. **阴影锚点当参数传**，不放常量。参考的阴影帧存在 `(2,2)` 一类的锚点上，`(0,0)` 是猜；`ArmyInfoArtwork`
   用 `GetShadowShift()` 从两张精灵里读出来，布局收下。
3. **`ArmyInfoArtwork` 记 `EvilInterface`**。邪恶界面不只是换 `ICN::VIEWARME`：窗口还下移 3 像素、`pos_rt`
   右移 9，退出按钮少了 18 像素的余量。把这个布尔留在美术对象上，布局才不用再问一次调用方。
4. **`Draw` 不画按钮**。参考的按钮是可悬停的 `Button`，被主循环反复重画；端口沿用 `ButtonSprite`，由
   `CreateButtons` 返回三个（顺序 upgrade/dismiss/exit，与 `FromButton` 的索引一致），调用方负责绘制与命中。
5. **描述块返回 `(FirstRow, Lines)` 而不是补空行的列表**。参考从 `3 - 行数` 起画，行数超过 3 时是负数，文字
   向上长进名字带；用一个可为负的起始行表达这件事，比补前置空行更难写错，也把「截断」这个选项排除在外。
6. **属性块画「标签 / 冒号 / 值」三段**。参考把标签右对齐到 `dst.x - 3`、冒号画在那里、值从 `dst.x + 6` 起。
   标签超宽时参考会缩放到一行（`fitToOneRow`），端口不做缩放，注释说明这一处差异。
7. **伤害与生命值取单只**。`Monster( troop.GetMonster() )` 而不是 `troop.GetDamageMin()`——后者乘了数量。
   这条容易写错，测试里专门断言弓箭手 10 只时伤害仍是 `2-3`、生命值仍是 `10`。
8. **`icnId` 表写成 switch**。前十一只的升级与本体不一一对应（游侠读 `ARCHER2`、十字军读 `PALADIN2`、
   冠军骑士读 `CAVALRYB`——那是颜色不是升级），算术只会偶尔对。
9. **`Speed`/`Luck`/`Morale` 的 `String` 只转英文原文**，与 ADR-172 对 `Spell.GetName()` 的处理一致；三支
   `Description` 与 `getMonsterDescription` 仍不转。

**后果**：新增 `ArmyInfoLayout.cs`（约 260 行含注释）、`ArmyInfoArtwork.cs`（约 140 行）、`ArmyInfoDialog.cs`
从 41 行扩到约 500 行；`Speed`/`Luck`/`Morale` 各加 `GetName`，`Troop` 加 7 个成员，`MonsterIcnIds` 加
66 行 switch。新建 `ArmyInfoDialogTests` 14 条，`TroopTests` +3，`MonsterIcnIdTests` +3。Game 2766 → **2788**，
UI 195 → **209**，全解决方案 3 576 → **3 613** 全绿，`-warnaserror` 0 警告。

**遗留**：`dialog_armyinfo.cpp` 的输入循环与事件（要 `LocalEvent` 与 `ButtonGroup` 的主循环）；单位精灵的动画帧
（`monster_anim`，随战斗批次）；战斗型的 `Shots Left`/`Hit Points Left` 两行（`Troop` 还没有战斗状态，要
`BattleTroop`）；`monster_info.cpp` 只剩 `sounds` 表。

## ADR-175：法师塔窗口拆四件，补两个绘制件 ◐

**背景**：魔法塔的规则（五行数据、两句状态栏）在 ADR-167 已转，留在 `castle_mageguild.cpp` 的是窗口本身：
`STONEBAK` 底图、`MAGEGLD*` 公会建筑、五行卷轴（`ui_mage_guild.cpp` 的 `SpellsInOneRow`）、`SMALLBAR` 底栏的
两段拷贝与三个按钮。和军队信息窗口一样，把矩形算术、`ICN` 帧号、像素绘制、按钮顺序混在一个 `Draw()` 里，测试
就只能整块跑；而这里的像素逻辑又比军队窗口更绕——建筑的横向居中要读「已绘像素」的包围盒，纵向却读整张精灵的
高度。

**决定**：

1. **四件拆开**：`MageGuildLayout` 只算矩形（窗口、底栏、三段分栏、状态栏 ROI）；`MageGuildArtwork` 只管帧号与
   两个派生（`GetGuildBuildingIcn`、`GetSpellIcon`）；`SpellScrollRow` 是一行的控件（槽位、命中、当前法术、
   自绘）；`MageGuildDialog` 收模型、拼绘制、发按钮、吐状态行。行控件独立成件是因为它的几何（110 步长、折叠
   下移 7、图标内缩 3、名字左下角 18/57）在参考里也是独立的类，而且五行的差别只有数据。
2. **补 `ImageOps.GetActiveRoi`，语义按参考的 transform 判定**：`value == 0 || value >= minTransformValue`（默认
   6）算已绘。不读图像层颜色——「transparent」是 transform 层的事，读颜色会把合法的调色板 0 当成空白。单层
   精灵返回空矩形，与参考一致（调用方 `FitToRoi` 也不受影响）。一处小差异：参考在「整张都跳过」时
   `area.height` 会停在 `-1`，端口以 `bottom = top` 兜底成高度 1 的退化框，调用方拿到的仍是 `IsEmpty`。
3. **补 `ImageOps.BlitSpriteClipped`，因为原有重载的语义是另一件事**：`BlitSprite( dest, offset, sprite, sourceArea )`
   把**精灵原点**放在 `offset`，源区随之平移；底栏与建筑要的是「源区左上角落到目标点」。用前者会整体偏一列
   （底栏，源从第 1 列起）或偏半个源区宽（建筑）。新重载收「目标点 + 源区 + 裁剪区」，内部把裁剪折算回源区，
   再交给旧重载完成逐像素拷贝。先写错再补，注释里点了名，免得后来的人以为是冗余 API。
4. **建筑的纵向基线用整张精灵**：`y = dialog.y + 290 - sprite.height`，横向才是
   `dialog.x + 100 - area.x - area.width / 2`。这一条最容易顺手写错（两处都用 `area`），测试专门用「只有顶部
   10 行有内容、精灵 40 行高」的假精灵把它钉住。
5. **建筑裁剪到左侧 200 列**：高层公会的图比底板高，不裁会盖住法术行。裁剪走 `BlitSpriteClipped` 的 `clip`，
   而不是先算可见矩形再选源区——后者要维护三套坐标，正是第 3 条里出错的地方。
6. **随机种族在画的时候重上色**：`MapRace.Random` 取 `MAGEGLDZ`（与 Wizard 同图），再用 `PaletteId.Purple` 过一遍；
   先 `Clone()` 再改，调用方传进来的帧不被改脏。
7. **单城时箭头真的禁用**：不只把 released 图当禁用图，还调 `ButtonSprite.Disable()`，于是 `Press()` 拒绝、
   `GetStatusText` 回默认句，与参考的 `button.disable()` 一致。
8. **底栏两段各带自己的源列**：左段源列 `1`（第 0 列是黑的），右段源列 `宽度 - 右段宽`（保住圆角）；奇数宽度
   由 `left = width / 2` 决定，余数落给右段。
9. **不转的部分写明白**：消息循环与 `LocalEvent`、`SpellsInOneRow::queueEventProcessing` 的法术说明对话框、
   `redrawCurrentSpell` 的增量重绘（端口整行重画，省掉「记住上一格」的状态）。

**后果**：新增 `UI/Widgets/SpellScrollRow.cs`（约 200 行含注释）、`UI/Dialogs/MageGuildArtwork.cs`（约 130 行）、
`UI/Dialogs/MageGuildLayout.cs`（约 120 行），`MageGuildDialog` 从 110 行扩到约 460 行；`ImageOps` 增
`GetActiveRoi`（约 100 行）与 `BlitSpriteClipped`（约 45 行）。新建 `ImageOpsActiveRoiTests` 5 条、
`ImageOpsBlitClippedTests` 4 条，`MageGuildDialogTests` 从 4 条扩到 29 条。Core 200 → **209**，UI 209 → **234**，
全解决方案 3 613 → **3 647** 全绿，`-warnaserror` 0 警告。`castle` 目录统计不变（改动落在已是 ◐ 的文件里）。

**遗留**：`castle_mageguild.cpp` 的事件循环（要 `LocalEvent` 与 `ButtonGroup`）；法术说明对话框（要
`spell_info.cpp` 的 `getSpellDescription`）；`castle_well.cpp` 的绘制循环与 MAX 按钮的重画。

## ADR-176：水井窗口分数据/布局/美术/绘制，保留参考的两处不一致 ◐

**背景**：`castle_well.cpp` 是 `castle` 目录里最后一份「规则已转、窗口未转」的文件：`CastleMaxRecruitment`
（MAX 按钮的分配算法）在 ADR-167 落地，窗口本身没有。它有六格住宅、每格九行文字、底栏两段拼接，还有一处
只有原版才有的手工填黑列。上一批补的 `ImageOps.BlitSpriteClipped` 正好是它需要的（源区左上角落点 + 裁剪）。

**决定**：

1. **四件拆开**：`CastleWellDwelling` + `Castle.GetWellDwellings()` 只回答数据（建没建、库存多少、周产多少、
   生物是谁）；`WellLayout` 只算矩形与锚点；`WellArtwork` 只管帧号与归档；`WellDialog` 拼文字、发按钮、判命中。
   与魔法塔同一套切法，好处是六格几何（七组常量）能被单测逐条钉住。
2. **「格坐标 = 格原点 + 常量」显式化**。参考把 314/150 的步进和七组 `+ (21,35)`、`+ 86`、`+ 122`、`+ 269`
   散在 60 行里，端口收成 `GetCellOrigin/GetPictureOrigin/GetDwellingNameAnchor/…`，越界级别抛
   `ArgumentOutOfRangeException`。
3. **保留「同一行问两次住宅」的不一致**。生物、名字、图画取 `GetActualDwelling()`（建了就升级），
   Growth 两行却用**基础**住宅位判定是否显示。只买了升级位的存档因此会显示升级后的生物、却没有成长行。
   参考如此，端口照抄并在 `CastleWellDwelling` 的备注里写明两处取值的来源。
4. **成长量另立 `Castle.GetWellGrowth()`**，与 `DwellingsBar.GetGrowth()` 同一套算式（井 +2 全员、魔法泉 +8 只给
   一级），注释里互相指认。没有合并二者，因为 `DwellingsBar` 按 item 解析、这里按基础住宅位解析，合并需要多一层
   映射，收益不抵风险。
5. **「跳过一行」用空字符串**。`DescribeStats` 返回 8 条（未建）或 10 条（已建），空串占一行高度，调用方只按
   `SmallWhite.LineHeight` 往下挪。这样「缝在哪」不进绘制代码，测试也能直接比较字符串列表。
6. **三个字体面收成 `WellFonts` 记录**。参考用 `FontType` 组合（smallWhite / normalWhite / normalYellow）；
   端口一个 `IGlyphSource` 带一种调色板，于是用一个三元记录传参，而不是给 `Draw` 加三个参数。
7. **美术表要求完整的 12 帧**（19–30），缺一帧 `TryCreate` 返回 `null`；未加载的按钮帧是 `null` 也算缺。
   这里踩过一次：`Sprite` 是引用类型而 `DialogButtonArtwork` 是值类型，`default(DialogButtonArtwork)` 的
   `Released` 是 `null`，判空必须写成 `sprite is null || sprite.Image.IsEmpty`。
8. **底栏两段 + 手工填黑列**：左段源列 1（第 0 列全黑）、右段源列 `宽 - 320 + 退出宽 - 1`（保圆角），
   最右一列（x = 639）填 `0`；底栏到第 476 行，477–479 三行参考也不画，端口照旧留空。
9. **不转的部分写明白**：`_wellRedrawMonsterAnimation`（生物动画，要战斗动画档案）、`_openWell` 的消息循环、
   招募动作本身（交给 `CastleMaxRecruitment` 与住宅招募对话框）；标题与 `Available:` 的裁剪/`fitToOneRow`
   因 `GlyphText` 无裁剪而不做，英文串装得下。

**后果**：新增 `Game/Castle/Castle.WellView.cs`（约 150 行）、`UI/Dialogs/WellLayout.cs`（约 340 行）、
`UI/Dialogs/WellArtwork.cs`（约 190 行）、`UI/Dialogs/WellDialog.cs`（约 380 行）。新建 `CastleWellViewTests` 6 条、
`WellDialogTests` 38 条。Game 2788 → **2794**，UI 234 → **272**，全解决方案 3 647 → **3 691** 全绿，
`-warnaserror` 0 警告。

**遗留**：生物动画（`_wellRedrawMonsterAnimation`）；窗口消息循环与 `LocalEvent`；`castle_well.cpp` 的招募调用链；
`castle` 目录至此六个文件全部 ◐ 或 ✔。

## ADR-177：`LocalEvent` 只转状态机，按钮组用 `ProcessEvents` 接上 ◐

**背景**：ADR-174/175/176 三个窗口（军队信息、魔法塔、水井）都把绘制做完了，却都留了同一句「事件循环未转」。
`engine/localevent.cpp` 是它们的共同前置：它把 SDL 事件变成「这一帧点了哪个按钮、按了哪个键、滚了多少」。
但这份文件 77 KB，一半篇幅是 `HandleEvents`（SDK 轮询、`EventEngine`、光标绘制、音频混音）、手柄映射与双指
触摸手势 —— 那些都要 SDL 设备与渲染循环，与端口「场景推事件、`PixelBuffer` 出图」的分层不合。

**决定**：

1. **只转状态机**。`LocalEvent` 的八个动作状态位、三键的按下/松开位置、滚轮增量、长按、拖拽标志、三个钩子与
   全部查询都照抄；`HandleEvents` 本体不转，改为 `BeginFrame()` + 四个 `On…` 推送方法（`OnMouseMotion`、
   `OnMouseButton`、`OnKeyboard`、`OnMouseWheel`）。参考里这些 `on…Event` 是私有成员，端口只能把它们提成
   public，注释逐个指回 `localevent.cpp` 的行号。
2. **`BeginFrame` 对应 `HandleEvents` 开头，而不是 `reset()`**。前者清的是四个一次性状态 + 触摸标志，保留「按住
   的按钮」与「按住的键」；后者清空一切。两个方法都留着：`Reset()` 是参考 `localevent.h:201` 的语义，
   `ButtonGroup.ProcessEvents` 在热键命中后调它，行为与参考的 `le.reset()` 一致。
3. **`Key` 枚举逐值照抄**。C# 侧只去掉 `KEY_` 前缀（`KEY_KP_0` → `KeyPad0`，`KEY_0` → `Digit0`），顺序与值不变，
   `LastKey` 收尾兼作成员计数（测试钉住）。热键表按值索引，重排会静默错位，所以这条写进注释。
4. **`KeyModifier` 做成 `[Flags]`**。参考是裸 `int32_t` 常量位；端口给枚举是为了让 `getCharacterFromPressedKey`
   的签名说得清楚，值仍是 1/2/4/8/16。
5. **`KeyText` 提成公开静态类**。参考的 `getCharacterFromPressedKey`/`InsertKeySym` 在 `localevent.cpp` 的匿名
   命名空间里，唯一调用方是 `InsertKeySym`；端口公开它们是因为二者是「键 + 修饰位 → 字符」的纯函数，不碰窗口
   就能测。字母的 26 个 case 折成 `>= Key.A and <= Key.Z` 一段（依赖枚举连续性，注释写明）。
6. **`InsertKeySym` 钳制光标位置**。参考把 `position` 当 `size_t` 并信任调用方，`pos > size` 时 `insert` 会抛。
   端口钳到 `[0, Length]`：退化位置只影响这一次按键，不会从文本框里抛异常出来。参考「退格时 `pos >= size`
   就缩短字符串」的分支原样保留。
7. **`ButtonGroup.ProcessEvents( LocalEvent, ButtonHotKey? )` 是第三套入口**（前两套是 `SetCursor`/`SetLeftButton`
   与 `ProcessHotKey`）。它照抄 `ui_button.cpp:598-641` 的三段：先按 `isMouseLeftButtonPressedAndHeldInArea`
   更新按下外观，再按 `MouseClickLeft( area )` 返回按钮值，最后把热键交给 `ProcessHotKey`。热键命中后调
   `events.Reset()`，与参考 `le.reset()` 同理（同一个键不该顺手关掉下一个窗口）。
8. **`hotKey` 用可空表示「这一帧没有热键」**。`ButtonHotKey` 的零值是 `Okay`，不能拿它当「没有」；参考在这里调
   `Game::HotKeyPressEvent( DEFAULT_OKAY )`，在没有按键时返回 false。少了这层，只有一个 OK 按钮的对话框会在
   第一帧自己关掉。`ProcessHotKey` 也因此只能在确实有键时调用。
9. **不转的部分写明**：`HandleEvents`（SDL 轮询、`EventEngine`、光标增量绘制、音频）、手柄、双指触摸、
   `KeySymGetName`（`SDL_GetKeyName`）、`getKeyName`（按键英文名表）。`IsMouseEventFromTouchpad` 因此恒为 false，
   状态位与查询都留着，等手势移植后自然接通。

**后果**：新增 `UI/Input/Key.cs`（约 180 行）、`UI/Input/LocalEvent.cs`（约 670 行）、`UI/Input/KeyText.cs`
（约 270 行），`Widgets/ButtonGroup.cs` 增加约 75 行。新建 `LocalEventTests` 23 条、`KeyTextTests` 16 条，
`ButtonGroupTests` 加 5 条。UI 272 → **316**，全解决方案 3 691 → **3 735** 全绿，`-warnaserror` 0 警告。

**遗留**：`HandleEvents` 与 SDL 侧的翻译层（落在 `App`）；手柄与双指手势；`KeySymGetName`/`getKeyName`；
把三个窗口（军队信息、魔法塔、水井）的消息循环接到 `LocalEvent` + `ButtonGroup.ProcessEvents`。

## ADR-178：窗口消息循环折成 `ProcessEvents` 的纯函数 ◐

**背景**：ADR-177 把 `LocalEvent` 的状态机立起来后，魔法塔、水井与军队信息这三个窗口各缺最后一块：
`while ( le.HandleEvents() )` 里那三十行「谁被点了、接下来做什么」。照抄这个循环是做不到的 —— 它夹着 `Display`、
`ImageRestorer`、`StatusBar`、`Dialog::RecruitMonster` 与 `fheroes2::showStandardTextMessage`，全是场景与模态框的
东西；而参考的每一轮又必须按固定顺序问一遍那些查询，顺序本身带语义（点击会被第一个问出 `true` 的分支吃掉）。

**决定**：

1. **一帧一次，返回「要做什么」**。`WellDialog.ProcessEvents( LocalEvent, WellLayout, 两个按钮, WellHotKeys )`
   返回 `WellDialogFrame`，`MageGuildDialog.ProcessEvents( LocalEvent, MageGuildLayout, 三个按钮, 五行, MageGuildHotKeys )`
   返回 `MageGuildFrame`，`ArmyInfoDialog.ProcessEvents( LocalEvent, 退出按钮, 升级按钮?, 解散按钮?, ArmyInfoHotKeys )`
   返回 `ArmyInfoFrame`。函数不绘制、不弹窗、不动城堡；参考里的 `break`/`return` 变成同一个返回。
2. **顺序与短路照抄**。水井：EXIT → MAX → 六格（每格先点击后右击，命中即 `break`）→ 两个 tooltip；魔法塔：
   EXIT → 上一城 → 下一城 → 五行的 `queueEventProcessing` → tooltip 的 else-if 链；军队信息：升级 → 解散 → 退出 →
   三个 tooltip 的 else-if 链。参考的 `||` 链在第一个命中的行就停，所以后面的行保留旧的 `CurrentIndex`，端口用
   `break` 复现；点击被消费这一点也有用例钉住（升级吃掉的点击不会再被退出读到）。
3. **外包的四件事**。购买计划（`CastleMaxRecruitment`）、招募对话框、法术说明框（`SpellDialogElement`）、军队信息
   的三个消息框留在调用方；帧只带数据：`WellDialogFrame.DwellingLevel`、`MageGuildFrame.Spell` 与
   `SpellByRightClick`、`ArmyInfoAction` 与 `ArmyInfoButton`。右击法术在参考里是 `Dialog::ZERO`（没有按钮），左击是
   `Dialog::OK`，这一位就是那个区别；军队信息的三句文案（升级提问、钱不够、解散提问）作为常量留在
   `ArmyInfoDialog` 上，免得到处抄。
4. **热键用参数传入**。端口没有 `Game::HotKeyPressEvent` 的热键表，所以
   `WellHotKeys( CloseWindow, BuyAll, Dwelling )`、`MageGuildHotKeys( CloseWindow, PreviousTown, NextTown )` 与
   `ArmyInfoHotKeys( Upgrade, Dismiss, CloseWindow )` 由场景解码；`Dwelling` 是 1..6 的等级而不是建筑位。
5. **`MageGuildDialogAction` 补两个值并让 `None` 取零**。新增 `None` 与 `ShowSpell`，并把 `None` 放在最前 ——
   枚举值不落盘，所以顺序不是契约；而 `default(MageGuildFrame)` 必须是「什么也没发生」，不能是「退出」。
   `ArmyInfoAction` 同样以 `None` 打头。两个 `FromButton` 仍然只回答按钮值，为自行驱动按钮的调用方保留。
6. **可为空的按钮用可空参数**。军队信息的升级与解散按钮在参考里是 `std::unique_ptr<Button>`，由 flag 与贴图共同
   决定有没有；端口把 `ArmyInfoDialog.ProcessEvents` 的这两个参数做成 `ButtonSprite?`，`null` 时既不亮也不响应，
   正好复现 `if ( buttonUpgrade )` 的三处守卫。退出按钮永远存在，所以是必填参数。
7. **`ButtonBase.UpdatePressState( LocalEvent )`**。四处 `drawOnState( le.isMouseLeftButtonPressedAndHeldInArea(
   area() ) )` 的实参逻辑提成按钮自己的方法（按住且在区内→按下，否则松开，禁用时不动），按钮组、水井、魔法塔、
   军队信息共用；它只改状态不绘制，因为水井与军队信息的按钮要等各自的动画计时器到了才重画
   （`castle_well.cpp:289-296`、`dialog_armyinfo.cpp:696-712`）。
8. **不转的部分写明**：`StatusBar` 的滚动文字、光标形状（`CursorRestorer`）、`ImageRestorer`、
   `DrawBattleStats()` 的两块战斗属性与它们的法术 tooltip、无按钮时「等右键松开」的读法。
9. **按住箭头连发另行补上（`Widgets/TimedEventValidator.cs`）**。参考的 `fheroes2::TimedEventValidator`
   （`gui/ui_tool.h:227-259`）用 `ActionObject::subscribe` 让按钮的每次状态变化回调 `senderUpdate()` 重置两个
   `TimeDelay`，`isDelayPassed()` 要求「首按延迟已过 + 间隔延迟已过 + 按钮按住」。端口没有观察者（`ButtonBase`
   的文档早写明这一点），于是把「这一帧是否按住」作为参数传给 `IsDelayPassed( bool isHeld )`，由计时器自己比较
   上一帧的状态来复现通知：`press()` 与 `release()` 都会通知，所以「按下」和「松开」两帧都重置两个计时器。首按
   500 ms、之后每 100 ms，触发时只重置间隔那一只 —— 与参考逐字一致。魔法塔把它接在参考的第三项上
   （`ProcessEvents( ..., previousTownRepeat, nextTownRepeat )`），为 `null` 时只答点击与热键。

**后果**：`WellDialog.cs` 增加约 110 行，`MageGuildDialog.cs` 增加约 180 行，`ArmyInfoDialog.cs` 增加约 190 行，
`TimedEventValidator.cs` 新增约 110 行，`ButtonBase.cs` 增加约 30 行、`ButtonGroup.cs` 减少 12 行。
`WellDialogTests` 加 12 条、`MageGuildDialogTests` 加 12 条、`ArmyInfoDialogTests` 加 12 条、
`TimedEventValidatorTests` 新增 6 条。UI 316 → **358**，全解决方案 3 735 → **3 777** 全绿，`-warnaserror` 0 警告。

**遗留**：`_wellRedrawMonsterAnimation`；法术说明框；军队信息的 `DrawBattleStats()`；把三个窗口接进 `App` 的场景
循环。`TimedEventValidator` 另外五个用点（`castle_town.cpp`、`castle_dialog.cpp`、`heroes_dialog.cpp`、
`dialog_marketplace.cpp`、`dialog_recruit.cpp`）要等对应窗口移植时再接。

## ADR-179：冒险模式的回合骨架折成 `AdventureSession` ◐

**背景**：到 ADR-178 为止冒险地图已经能画、能滚、能开图（ADR-131、ADR-169、ADR-170），但它仍然只是个查看器：
没有玩家、没有日期、英雄不会走。缺的正是 `Interface::AdventureMap::StartGame()` 的那个 `while`
（`game_startgame.cpp:732-790`）、`humanTurn()` 开头的两句（`:1039-1043`）以及 `game.cpp` 里每帧挪一下英雄的那几行。
参考把这三样与界面挤在同一个类上 —— 它的事件循环就是界面循环 —— 端口照抄不了：`FHeroes2.Game` 不认识 SDL，
也不该认识画布。

**决定**：

1. **循环独立成类**。`Game/AdventureSession.cs` 只管「谁在行动、什么时候换人、什么时候换天」：
   `Start` 开一局并把世界推进到第 1 天，`EndTurn` 换人或换天，`NextHero`/`SetFocus` 管焦点，
   `SetPath` 是 `MouseLeftClick()` 里 `SetPath( world.getPath( ... ) )` 加 `SetMove( true )` 那一对。
   参考里这些是 `Interface::AdventureMap` 的成员，端口把它们从界面里拆出来，场景只剩「问会话 + 画」。
2. **一天的结构照抄**。新的一天发生在所有玩家都行动完之后（`StartGame()` 循环顶部的 `world.NewDay()`），
   `EndTurn` 只换人；`_acted[]` 记住这一圈谁已经动过，转一圈才清空并推进日期。
3. **电脑玩家被跳过**。`AI::Planner` 在端口只有契约（ADR-161），电脑回合的全部内容都在
   `AI::Planner::KingdomTurn()` 里，没有它就没有可执行的动作。跳过至少让日期与收入继续走、人类玩家的城照常可访问；
   等 AI 落地时把 `EndTurn` 里的那个 `continue` 换成一次 `KingdomTurn()` 就回到了参考的形状。
4. **回合开始的两件事按参考顺序**：先 `Kingdom.ActionNewDayResourceUpdate`（收入 + 当天事件），再
   `Kingdom.ActionBeforeTurn`（人类英雄 `calculatePath()`，AI 英雄清路线）。`Hero.CalculatePath` 是那次
   重算的实现：取旧目的地 → 两次 `IsValidForMovement` 校验（旧路线在中转格重算后就可能失效）→ `SetPath`。
   焦点在这里跟着回合走，落在当前王国的第一个英雄上（参考的 `_statusPanel.SetHero`）。
5. **玩家槽位在「开一局」时建，不在「读一张图」时建**。`Settings.Players` 是进程级的静态表
   （`Hero.GetControl()` 问它，以后的 AI 也会问），而地图文件头说的是「谁可以让人类玩、谁可以让电脑玩」，
   两张表经常重叠，直接拿 `ComputerPlayers` 当 AI 会把整张图判成电脑。参考的答案在
   `Players::Init( fi )` + `SetStartGame()`（`players.cpp:299-352`、`:487-508`）：热座时只留 human-only 色，
   否则把第一个「可以让人类玩」的色提升成唯一的人类。于是有两条规则：
   `World.Kingdoms.cs` 的 `ComputerColorsOf` 在装载地图时按同一规则算出王国的 AI 色集合（**不写全局表**：
   两个世界同时装载就会互相覆盖，测试正是这样）；`AdventureSession.Start` 在真正开局时
   `Players.Init( info )` + `SetStartGame()` 填好全局表。`Start` 因此多收一个 `Mp2FileInfo`。
   `Players.Init` 里原本问 `SharedColors` 的地方改成问 `HumanPlayers`（`players.cpp:330` 问的是
   `colorsAvailableForHumans`）：只允许人类的色同样该参与那唯一一次提升，否则单机图没有人能行动。
6. **英雄得有部队**。`Hero.LoadFromMP2` 现在补 `Army.Reset( true )`（`heroes.cpp:453`），`Army.Color`/`Army.Race`
   跟着英雄走（`SetColor`、换种族）。理由很实际：`GetMaxMovePoints()` 由部队里最慢的兵决定，空部队的移动力是 0，
   从地图上读出来的英雄一步也走不了。主技能、初始法术、二级技能、自定义部队仍然没读，文档里写明理由。
7. **画面这半**：`AdventureMapView` 增加世界英雄的绘制（身体与影子），帧取 `Hero.SpriteIndex`
   （`direction * 9 + frame`），位置叠加 `Hero.Offset`（走到两格中间的那半步），雾下不画；
   `IHeroSpriteSource.GetBody( race, frame )` 用默认接口方法实现，测试替身不必跟着改。
   一旦世界里已经有英雄就只画世界这一批，否则静态英雄表会在同一个格子上画出第二个人。
8. **场景这半**：`AdventureMapScene` 持有会话，开局时把世界传给视图并以焦点英雄居中；点地形 = 焦点英雄铺路，
   按钮面板改由 `CurrentButtonStatus()`（`_setButtonStatus` 的四问）决定，状态栏写日期与当前玩家，
   `EndTurn`/`NextHero` 收口在场景上；`GameHost` 绑 `TAB`/`H`（下一个英雄）与 `E`（结束回合）。
   没有人类玩家的地图仍然退回查看器，行为与 ADR-169 时一致。
9. **不转的部分写明**：`Interface::AdventureMap` 的其余分支（城堡与英雄对话框、系统菜单、`ShowControlPanel`）、
   `AI::Planner`、`Game::LoadGame`/`AutoSave`、过场动画、`StatusPanel` 的旗帜与地图名贴图、`game_hotkeys.cpp`
   的热键表（端口直接绑键）。

**后果**：`AdventureSession.cs` 新增约 345 行；`Hero.Movement.cs` 约 25 行（`CalculatePath`）、`Kingdom.cs` 约 30 行
（`ActionBeforeTurn`）、`Hero.cs` 约 20 行（默认部队与部队的色/种族）、`World.Kingdoms.cs` 约 45 行
（`ComputerColorsOf`）、`Players.cs` 一处判定；`AdventureMapView.cs` 约 100 行（世界英雄与影子）、
`HeroSpriteSet.cs` 约 10 行；`AdventureMapScene.cs` 约 190 行、`GameHost.cs` 约 10 行。
新建 `AdventureSessionTests` 6 条、`WorldHeroLayerTests` 2 条。全解决方案 3 777 → **3 785** 全绿，`-warnaserror` 0 警告。

**遗留**：`AI::Planner` 的电脑回合、存读档（`game_io`/`serialize`）、当天事件的对话框、状态栏的国旗与地图名贴图、
`game_hotkeys.cpp` 的热键表、`Game::AutoSave` 与过场动画。`Settings` 与 `Players` 都是进程级静态表，
因此按 `Settings` 的用例被收进同一条 xunit collection 串行执行。

## ADR-180：AI 层先把形状定死，实现留到最后 ◐

**背景**：ADR-161 给 `FHeroes2.Game.Ai` 立的是「别人要用的那一半」：`KingdomTurn`、`BattleTurn`、`HeroesAction`
这些入口都在，正文一律 `throw`，私有半边特意没声明，理由是三十来个私有方法的参数形状依赖还没落地的对象价值表。
先补其它子系统、AI 压到最后，这个划分就露了短板：真到写 `ai_planner_hero.cpp` 那天，要先回头补十几个私有声明，
而那时的形状只能靠当下的记忆去猜。参考的头文件早把形状写清楚了（`ai_planner.h:200-281`、`ai_battle.h:97-165`），
不如现在照抄，正文留空。

**决定**：

1. **私有半边照抄声明**。`AiPlanner` 的 `CastleTurn`、`HeroesTurn`、`GetPriorityTarget`、`EvaluateRegionSafety`、
   `FindCastlesInDanger`…… 26 个成员照 `ai_planner.h:200-281` 的顺序与参数声明；`BattlePlanner` 的
   `PlanUnitTurn`、`SelectBestSpell`、`SpellDamageValue`、`GetSpellSlowRatio`…… 23 个照 `ai_battle.h:97-165`。
   正文仍是 `NotImplementedException`，消息里带将来要移植的 `.cpp` 与 ADR 编号。
2. **状态字段一起补**：`_mapActionObjects`、`_priorityTargets`、`_enemyArmies`、`_tileArmyStrengthValues`、
   `_regions`、`_budget`（顺序照 `ai_planner.h:265-281`）。参考里这六个是整场游戏有效的缓存，正是「回合开始时清掉」
   那一段操作的对象。
3. **只有四处带代码**，理由统一：声明出来的字段必须有读点，否则 `-warnaserror` 下 CS0169/CS0414 会直接打断构建，
   而这四处的正文本来就是最终形态 —— `IsPriorityTask`/`IsCriticalTask`（header 里就是内联的两三行）、
   `BattlePlanner.BattleBegins()`（四个计数器归零，`ai_battle.cpp`）、`AiPlanner.ResetTurnCaches()`
   （`ai_planner_kingdom.cpp:652-659` 的清理块，收成一个方法）、`UpdateKingdomBudget()` 开头的预算清零
   （`ai_planner_castle.cpp:270-275`）。
4. **一次战斗内有效的分析数据不声明**：`_commander`、`_myArmyStrength`、`_enemyArmyStrength`、`_defensiveTactics`
   这些唯一读者是还在 `throw` 的规划方法，声明出来就是死字段，留到 `AnalyzeBattleState` 落地时一起加。
5. **`AIWorldPathfinderStateRestorer` 直接实现完**。ADR-161 时它还落不了地（要 `AIWorldPathfinder`），ADR-091 之后
   世界寻路器已经完整（`Reset()`、两个设置器的读写都在），这个守卫的全部内容就是「存两个数、放回去」，没有可留
   的决策；`Dispose()` 幂等，参考用析构函数天然只跑一次。
6. **`BattlePlanner.TurnLimitCounters`（internal 只读元组）只为测试存在**：`IsLimitOfTurnsExceeded()` 落地前那四个
   计数器没有任何读者，而 `battleBegins()` 已经把 50 这个数写进来了，值得钉住。测试项目本来就有
   `InternalsVisibleTo`。

**后果**：`AiPlanner.cs` 260 → **454** 行、`AiBattlePlanner.cs` 105 → **369** 行、新增
`AIWorldPathfinderStateRestorer.cs` 54 行；测试 +2（`ABattleStartsWithAFreshTurnLimit`、
`TheStateRestorerPutsBothTunablesBack`）。Game 2 802 → **2 804**，合计 3 785 → **3 787** 全绿，
`-warnaserror` 0 警告。

**遗留**：`ai_planner_hero.cpp`/`ai_planner_castle.cpp`/`ai_planner_kingdom.cpp`/`ai_battle.cpp`/`ai_battle_spell.cpp`/
`ai_common.cpp`/`ai_hero_action.cpp` 七个文件的正文；战斗内的分析字段；若实现里要就地改 `EnemyArmy` 的字段，再放开
它的设置器。

## ADR-181：存读档的地基是 `Core/Io` 里的流与压缩块 ◐

**背景**：`game_io.cpp` 的存读档全文压在 M8，但它下面有两层不是游戏规则、也没有任何游戏依赖的东西：参考
`engine/serialize.h`+`.cpp`（687 行）的四个流类，与 `engine/zzlib.cpp`（zlib 包装 + 存档块格式）。这两层不落，
后面无论是写 `Players` 的序列化、`Heroes`/`Castle` 的序列化还是 AI 的存读，每一处都要先自己搓一个流出来。
`Core/Io` 目录此前只有 `Crc32`、`LittleEndianReader/Writer` 与 `BinaryFormatException`，正是放它们的地方。

**决定**：

1. **一个抽象基类，不是两个**。参考用虚继承把 `IStreamBase`（读）与 `OStreamBase`（写）拼成 `StreamBase`，只读流
   因此不提供写侧。C# 没有虚继承，照搬只能得到「两个接口 + 一堆转发」。端口改为：`StreamBase` 负责字节序、
   字符串、失败旗和全部读写辅助，只向具体流要两个原语 —— `Get8()` 与 `PutRaw()`，外加 `Size`/`Tell`/`Seek`/
   `Skip`/`GetRaw`。只读的 `RoStreamBuf` 把 `Put8`/`PutRaw` 覆盖成 `NotSupportedException`：写只读流是程序错误，
   参考那里是编译不过，端口里是运行时立刻炸，比静默丢数据好。
2. **命名换成动词**。`stream << x` / `stream >> x` 在 C# 里没有对应写法（`<<` 不能重载成接受 `byte`），所以写侧
   一律 `Write( ... )`（每种类型一个重载，顺序与参考一致），读侧一律 `ReadUInt16()`/`ReadString()` 这种带类型名的。
   `fail()` 是属性 `Fail`，`setFail()` 是方法 `SetFail()`。
3. **短读语义各自照抄**：`StreamBuf`（参考 `RWStreamBuf`）读到不够就补零、不置失败；`StreamFile` 读不够返回空数组
   并置 `Fail`。参考里这两个类本来就分裂，统一成一种反而会让「存档被截断」这件事读不出来。
4. **字符串是「4 字节长度 + 原始字节」，没有零终止**。参考 `operator<<( StreamBase &, const std::string & )` 先写
   `size()` 再写字节；`ReadString()` 读到第一个 `\0` 为止（旧版本写进来的名字可能带填充）。编码用
   `Encoding.Latin1` —— 它把 0~255 一对一映射成字符，正好是「按字节搬运」的原意，不像 UTF-8 那样会因为非法字节
   改变长度。
5. **压缩走 `ZLibStream`，不 P/Invoke zlib**。与 ADR-062（PNG 编码器）同一个选择：框架自带的 zlib 就是 zlib，
   存档里那串字节与 `compress2()` 写出来的是同一种流。压缩级别只映射两档（`Z_BEST_COMPRESSION` →
   `CompressionLevel.SmallestSize`，`Z_DEFAULT_COMPRESSION` → `Optimal`），参考的 `Z_NO_COMPRESSION` 等档位没人用。
6. **块格式照 `zipStreamBuf()` 写死**：`rawSize`(4) + `zipSize`(4) + `formatVersion`(2，只有 0) + 两个未用字节 +
   zlib 流。两个尺寸让读的一侧能一次分配，`UnzipStream()` 因此可以要求「解压出来的长度必须正好等于 rawSize」，
   而不是像参考那样解完再比。
7. **不抛异常**：损坏的存档一律返回空数组 / `false`，并 `GameLog.Error`。调用方是对话框，异常会把要显示给玩家的
   那句话一起带走。
8. **解压有上限**：`unzipData()` 不知道结果多大时，端口按流式读并卡在 512 MB。参考交给 zlib 的 `Z_MAX` 去管，
   而存档是可以被玩家改的，这里宁可多写一行。带 `realSize` 的重载不设上限 —— 尺寸是块头里写的，分配前已经知道。

**后果**：新增 `src/FHeroes2.Core/Io/` 五个文件，共 **932** 行 —— `StreamBase.cs` 281、`StreamBuf.cs` 152、
`RoStreamBuf.cs` 108、`StreamFile.cs` 165、`Compression.cs` 226。测试 +24（`StreamTests` 16 条、`CompressionTests`
8 条），Core 209 → **233**，合计 3 787 → **3 811** 全绿，`-warnaserror` 0 警告。

**遗留**：`game_io.cpp`/`game_loadgame.cpp` 的正文（M8 的主体）；`game_over`/`highscores` 的存档格式；`Players`、
`Heroes`、`Castle` 等类型各自的 `operator<<`/`operator>>` 移植 —— 这次只给它们准备好了「往哪写」。


## ADR-182：army 目录全量移植：小怪物线、两处存档运算符与军队栏 ◐

**背景**：`army/` 的四个 `.cpp` 在此之前是 0 已转 / 2 部分 / 2 未转。`army.cpp` 缺的是两张怪物线与存档运算符，
`army_troop.cpp` 缺 `operator<<`/`operator>>`，`army_bar.cpp` 与 `army_ui_helper.cpp` 完全没有对应实现。最后这一对
是「军队」这件事唯一对玩家可见的一面：城堡里、英雄界面上、编辑器里的五个格子。它们不依赖战斗，也不该等
战斗层，`army` 目录于是可以先收口。

**决定**：

1. **`army_ui_helper.cpp` 只有一个函数，照抄进 `Game/Army/MiniMonsters.cs`**：`drawMiniMonsters()` 的窗口切块、
   居中、精灵高度偏移一像素、以及三处怪癖全部保留 —— 每个 chunk 按「本行剩余栈数」均分；`first == 0 &&
   count == 0`（不指定窗口）时**从右往左**画，于是最后一栈落在最右；盗贼公会只显示第一栈时其余栈画 `???`。
   精灵来源走接口 `IMiniMonsterSprites`，默认实现 `Mons32Sprites` 读 `MONS32.ICN`、帧号 = 怪物 id - 1。
2. **`army.cpp` 的两张怪物线进 `Game/Army/Army.MonsterLines.cs`**：`DrawSingleDetailedMonsterLine()` 与
   `DrawMultipleMonsterLines()`。<3 栈一行居中（下移 `offsetY / 2 + 1`、宽 2/3），4 栈以上前两栈一行、其余第二行
   （第二行两栈时宽 2/3）。这些数字都是参考里的原数，没有重算。
3. **存档运算符进两个 `Persistence` 分片**：`Troop.Persistence.cs` 写 `(int)Monster` + `Count`，读失败清成
   `Unknown`/0；`Army.Persistence.cs` 写槽位数 → 每栈 → 阵型布尔 → 颜色。**颜色宽度按存档版本分叉**：
   `version < Release1_1_09` 读 4 字节整数，之后读 1 字节 —— 这是参考 `army.cpp` 里唯一一处版本判断，也是这一层
   存在的理由。槽位数与常量不符即 `SetFail()` + 清空，不抛异常。读完后 `Commander` 置空：归属者由外面的装配层
   重新挂上，存档里没有指挥官的身份。
4. **`army_bar.cpp` 拆成四个 partial 文件 + 一个几何类**：
   * `ArmyBar.cs` —— 状态、`SetArmy`、`SetBackground`（迷你 43×43 / 详细 43×53）、两个 `Redraw*`、光标框；
   * `ArmyBar.Input.cs` —— 参考里全部 `ActionBar*()` 处理（光标文案、左键单击/双击/拖动/右键）；
   * `ArmyBar.Events.cs` —— `QueueEventProcessing()` 与 `ActionCursorItemIter()` 的分派、`GetItemIndexAt`；
   * `ArmyBar.Redistribute.cs` —— 参考匿名命名空间里那六条重分配规则；
   * `ItemsBarLayout.cs` —— `gui/interface_itemsbar.h` 的表格几何（单格尺寸、表尺寸、格间距、渲染偏移、每行格数、
     `GetItemRect`/`IndexAt`/`RenderArea`）。抽出来的理由是它是纯几何、与「格子里装什么」无关，后面的
     宝物栏、法术栏、城堡建筑栏都要用它。
5. **对话框不做阻塞调用**。参考在重分配里 `Dialog::ArmySplitTroop` 拿一个数、在双击里 `Dialog::ArmyInfo` 拿一个
   动作，端口把两者都变成「问题描述」：`RedistributeArmy()` 返回 `ArmyBarSplit`（来源、目标、同一怪物与否、
   总数、可分的格数、上限、建议值、是否均分），调用方拿去问玩家，再用 `ArmyBar.ApplySplit()` 执行；
   双击返回 `ArmyBarEvent.Info`（`ArmyInfoDialog` 模型），答案交给 `ArmyBar.ApplyInfoAnswer()` 消费
   （`Dismiss` 清空、`Upgrade` 付钱再升级）。
6. **右键的两个分支按参考分**：`MouseClickRight` 走 `ActionBarRightMouseSingleClick`（只做重分配，不做别的）；
   按住的那一帧走 `ActionBarRightMouseHold`（解除整栈 / 打开信息窗）。因此「按下右键立刻提示、松开不算」
   的时序与参考一致，而不是合成一次点击。
7. **世界的依赖外置成委托**。参考里 `world.GetKingdom( color ).AllowPayment()` 与 `OddFundsResource()` 直接读
   世界；端口不把世界塞进控件，改由拥有王国的一侧提供 `CanAffordUpgrade`（决定升级按钮是否灰）与
   `payForUpgrade`（执行扣款）。两者都可以不提供，缺省行为与参考「付得起」的一支相同。
8. **缺图不停机**。`MiniMonsters` 里参考写的是 `assert( monster != nullptr )`；端口改成 `GameLog.Warn` 后跳过该栈。
   缺一张 `MONS32` 画面不该把玩家从界面上打下来，也不该让存档一致性检查在测试里炸掉。
9. **`ImageOps.Borders.cs` 补上 `DrawBorder()`**：参考 `fheroes2::DrawBorder( image, color, thickness )` 是各条
   栏共用的小工具，端口此前没有；这次顺带落了，`ArmyBar` 的格子框与后面的栏都能用。

**后果**：新增 **11** 个文件、共 **2 091** 行：`Core/Imaging/ImageOps.Borders.cs` 83；`Game/Army/MiniMonsters.cs` 221、
`Army.MonsterLines.cs` 125、`Army.Persistence.cs` 89、`Troop.Persistence.cs` 62；`UI/Widgets/ArmyBar.cs` 388、
`ArmyBar.Input.cs` 423、`ArmyBar.Events.cs` 224、`ArmyBar.Redistribute.cs` 223、`ArmyBarSplit.cs` 79、
`ItemsBarLayout.cs` 174。测试 +33（`ArmyPersistenceTests` 8 条、`MiniMonstersTests` 6 条、`ArmyBarTests` 19 条），
Game 2 804 → **2 818**、UI 358 → **377**，合计 3 811 → **3 844** 全绿，`-warnaserror` 0 警告。
`army` 目录改记 1 已转 / 3 部分 / 0 未转（`army_ui_helper.cpp` 已转；`army.cpp`、`army_troop.cpp`、`army_bar.cpp`
各留一段，见遗留）。

**遗留**：`army.cpp` 的 `GetLuckModificator()`/`GetMoraleModificator()`（要 `Kingdom`/`Castle` 的加成链）与
`Army::String()`（本地化拼接）；`army_troop.cpp` 的 `GetAttackString()`/`GetSpeedString()`/`GetShotString()` 等
文字行；`army_bar.cpp` 的调用方 —— 英雄界面、城堡界面与编辑器的那几处 `QueueEventProcessing()` 还没接上；
存档的整体装配（`game_io.cpp` 的会话层）仍属 M8。

## ADR-183：列表控件只做状态机，贴图与行绘制由调用方给 ◐

**背景**：`kingdom` 目录剩下的两篇都是界面，其中 `kingdom_overview.cpp` 的英雄/城堡两个列表建立在参考的
`Interface::ListBox<Item>`（`gui/interface_list.h`）之上，而滚动条的索引↔像素映射在 `gui/ui_scrollbar.*` 里。
这两样是所有「多行可滚」窗口的公共前置（场景选择、法术书、宝物列表、招募、市场都会用到），但仓库里此前
只有 `ScrollSlider`（贴图拉伸），没有列表。

**决定**：

1. **`ListBox<T>` 按参考拆两层**：`ListBoxBase` 对应 `ListBasic`（非泛型，王国总览要靠它用一个指针在英雄表与
   城堡表之间切换），`ListBox<T>` 对应 `ListBox<Item>`。`T` 限 `class`，与参考「行以指针交出」一致。
2. **行内容一律由子类画**：`RedrawItem`/`RedrawBackground`/`ActionCurrentUp`/`ActionCurrentDn`/
   `ActionListSingleClick`/`DoubleClick`/`PressRight`/`LongPress`/`Cursor` 全是抽象或虚方法，签名照抄参考，
   但多一个 `PixelBuffer output` —— 端口没有全局 `Display`，帧缓冲从外面传。
3. **两个步进按钮不取图**：参考的 `SetScrollButtonUp( icn, index1, index2, pos )` 在端口里变成
   `SetScrollButtonUp( ButtonBase )`。控件层不碰 `ICN::`，调用方（对话框）自己按美术表造 `ButtonSprite`；
   按钮的按下外观由 `UpdatePressState(events)` 更新，不再在事件里顺手重绘。
4. **滚动条独立成类**：`ScrollBar` 只管 `Area`/滑块尺寸/`Position`/`min..max` 与五个移动方法，不继承贴图。
   `ListBox.UpdateScrollBarImage()` 仍用参考的 `ScrollSlider.Create(..., 0-8, 7-15)` 切拉伸，保持与
   `ui_scrollbar.cpp` 同一份几何。
5. **整数除法一处不改**：`MoveToIndex` 的截断、`MoveToPosition` 的 `+ length / 2` 与「单档返回 false」全部照抄，
   因为按下块靠返回值判断是否重画，行命中框也直接由同一套除法得出。
6. **两处必要的偏离**：`VisibleItemCount` 由 `protected` 放宽为可读（对话框算边框要用）；行区域小于行数时
   拖动的整除会除零，端口跳过那一段而不是像参考一样崩。

**后果**：新增 `UI/Widgets/ScrollBar.cs`（约 250 行）、`ListBoxBase.cs`（约 45 行）、`ListBox.cs`（约 700 行）；
测试 +27（`ScrollBarTests` 8、`ListBoxTests` 19），UI 377 → **404**，合计 3 844 → **3 871** 全绿，
`-warnaserror` 0 警告 0 错误。`gui` 目录的 `ui_scrollbar.cpp` 由「已转」补上另一半，`interface_list.h`
从「无」改记已转。

**遗留**：`kingdom_overview.cpp` 与 `view_world.cpp` 本身；`ListBox` 在场景选择/法术书/市场等窗口上的接线。

## ADR-184：世界视图先落档位模型，地图尺寸由调用方给 ◐

**背景**：`view_world.cpp`（795 行）是「世界视图」窗口：把整张地图按 1:4、1:6、1:12 各缓存一份（另加 1:1 调试档），
再按当前档位与中心从缓存里裁一块贴到屏幕上。它同时依赖 `ICN::MISC4/6/12`、`LETTER*`、`VWFLAG*` 六套图和
`Interface::GameArea` 的分块重画，整篇一次落不了；但其中「档位表 + 可见矩形 + 夹中心」是纯算术，且是所有
后续窗口（小地图点击、缩放按钮、光标定位）都要用的坐标系。

**决定**：

1. **拆成 `ViewWorldZoomLevels`（静态表与纯函数）+ `ViewWorldZoom`（有状态：档位 + 中心 + 每档矩形）**，
   对应参考的匿名常量、`computeROI`、`getNextZoomLevel`/`getPreviousZoomLevel` 与 `ZoomROIs`。
2. **地图尺寸走参数**：`ViewWorldZoom` 的构造要 `mapWidthInTiles`/`mapHeightInTiles`（参考读 `world.w()/h()`），
   与 ADR-111 一致；`tileWidthPx = 32` 仍是常量（`ui_constants.h` 的值）。
3. **三级视图的起始档位要校验**：参考只在开放调试档时才传 `ZoomLevel3`，三级数组里它越界；端口在构造时抛
   `ArgumentOutOfRangeException`，把参考的「调用方约定」变成显式契约。
4. **整数截断与 `( size - 1 ) / 2` 一处不改**：`computeROI` 的两步除法、`ChangeCenter` 的减一、`RoiInTiles`
   的 `+ 16` 全部照抄。这三处任意一处「修正」都会让四档的像素边界互相错开一格。
5. **状态变化才重算**：`ChangeCenter` 夹出来的中心与当前相同就返回 `false` 且不刷四档矩形；`ChangeZoom`
   先换档位再夹中心，复现「缩小时贴边中心被推回」。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorld.cs`（约 340 行）与 `ViewWorldZoomTests` 32 条；
Game 2 818 → **2 850**，合计 3 871 → **3 903** 全绿，`-warnaserror` 0 警告 0 错误。`kingdom/view_world.cpp`
在 PLAN 里由 ❌ 改记 ◐（模型已转，像素层未转）。

**遗留**：`CacheForMapWithResources`（18×18 分块重画 + `Resize` 四份缓存）、`DrawWorld` 的黑边填充、
`DrawObjectsIcons`（`renderCastleFlags`/`renderIcon`/`renderResourceIcon`）、`renderAI`/`renderCursor`、
`ViewWorldWindow` 的事件循环与小地图联动。

## ADR-185：世界视图的像素层靠注入绘图回调与精灵源，不引全局 ◐

**背景**：ADR-184 只转了 `view_world.cpp` 的坐标系。剩下的绘制部分有三处硬依赖：`CacheForMapWithResources` 里调
`Interface::GameArea::Redraw`（需要整个冒险地图的绘制管线）、`Assets::GetICN`（端口是 `IcnSpriteCache`）、以及
`DrawWorld` 里直接读 `Display` 与 `GetGameArea().GetROI()`。端口没有全局 `Display`/`GameArea`，硬拷贝会把单例
带进 `FHeroes2.Game`。

**决定**：

1. **分块重画做成回调**：`ViewWorldMapCache` 收一个 `ViewWorldBlockDrawer(PixelBuffer block, Point centre, ViewWorldDrawLayers layers)`
   委托。谁调谁来画一块 576×576 的地图（冒险地图渲染器或测试里的假画手），缓存的职责只剩「逐块重画 → 缩到四档」。
2. **精灵来源做成接口**：新增 `IViewWorldSpriteSource`（`GetSprite(IcnType, int)`），实现 `IcnViewWorldSpriteSource`
   包一个 `IcnSpriteCache`；测试用返回 2×2 纯色图标的假实现，断言的是「要了哪张图、第几帧」。
3. **屏幕是参数**：`DrawWorld(zoom, cache, display, visibleRoi, borderWidthPx, radarWidthPx)`，黑边宽度与雷达宽度
   默认取 `ui_constants.h` 的 16 / 144，但可覆盖；雷达区与窗口外一律不写。
4. **黑边不 assert，先求交**：参考假定整块贴图在屏内（`assert`），端口的 `FillBlack` 与 `display.Bounds` 求交，
   空区域直接返回，窄屏下右黑条算成负数也不会越界。
5. **对象类型先抹动作位再比**：参考 `switch` 用的是 `MP2::OBJ_*`（带动作位，`OBJ_CASTLE` = 206）；端口瓦片里
   两种值都能出现，统一 `Mp2ObjectTypes.NonActionType(...)` 后再比，避免「城门类型 35 的城镇不画旗」。
6. **城镇旗子仍只认城门格**：`GetCastleEntrance(tile.GetCenter(world))` 原样保留。端口的 `IsValidCastleEntrance`
   只认非动作值 `Castle`（与参考的 `OBJ_CASTLE` 判定相反），这是既有行为，不在渲染器里绕开；要改应改判定本身。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldMapCache.cs`（179 行）、`IViewWorldSpriteSource.cs`（46 行）、
`ViewWorldRenderer.cs`（375 行），测试 `ViewWorldMapCacheTests` 11 条 + `ViewWorldRendererTests` 23 条。Game
2 850 → **2 884**，合计 3 903 → **3 937** 全绿，`-warnaserror` 0 警告 0 错误。

**遗留**：`renderCursor` 的四种光标、`ViewWorldWindow` 的事件循环（换档/换模式/滚动），以及
`kingdom_overview.cpp` 整体。

## ADR-186：世界视图的雷达单独立类，缩放与边框复用冒险地图雷达的静态件 ◐

**背景**：`ViewWorldWindow` 的雷达在参考里是 `Radar` 的第二个实例（`RadarType::ViewWorld`），ROI 固定为整图，
但构造函数、`Build()` 与绘制都吃 `Mp2Map`。端口的 `Minimap` 是 `Game.AdventureMap` 里从 `Mp2Map` 建的一次性
面板（`ColorAt` 只读），既拿不到 `World` 的占领色/迷雾，也没有「按模式换一套颜色」的入口。

**决定**：

1. **单独立 `ViewWorldRadar`**（`Game/Kingdom`），构造只收 `World`。它持有「每格一个调色板索引」的数组与
   144×144 面板；`RedrawObjects(friends, mode)` 每次从头着色并重缩面板，语义等同参考的
   `RedrawObjects( Players::FriendColors(), mode )`。
2. **不复制 `Minimap` 的构建逻辑，只复制规则**：地面/王国/道路色与 `COLOR_GRAY` 仍是 `Minimap` 的公开常量，
   山的加深色阶也照它的 `HillShadeOffset`。
3. **缩放、边框、可见框抽成 `Minimap` 的静态方法**（`ScaleColors`、`DrawCursorFrame`、`CursorArea(roi, w, h)`），
   实例方法转调同一份实现。理由：两处雷达要用同一种点状边框与同一种「裁到世界再缩放」的取整；参考里它们就是
   同一个函数，端口只是被两个类共享。
4. **点击换算放在雷达里**：`CenterOn(zoom, panelPoint)` 把面板点换算成格、再换算成像素中心调
   `ViewWorldZoom.ChangeCenter`，返回值即「视图是否真的动了」。参考在此前用不同量纲比较过一次上左角，端口不照抄。
5. **城镇用 `world.Castles.Get(x, y)` 而不是城门判定**：参考的 `world.getCastle( point )` 命中的是城镇精灵
   覆盖的任意一格，端口 `AllCastles` 的占地表与之对应；`GetCastleEntrance` 只认城门格，用在雷达上会把 5×5
   的占地涂成草地色。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldRadar.cs`（约 270 行）与 `ViewWorldRadarTests` 16 条；
`Minimap` 多了三个静态方法（`GrayColor` 常量、`ScaleColors`、`DrawCursorFrame`、`CursorArea` 重载），
实例行为不变，原有 404 + 2 900 条测试全绿。Game 2 884 → **2 900**，合计 3 937 → **3 953**。

**遗留**：`ViewWorldWindow` 外壳（淡入淡出、缩放/退出按钮、侧栏整图、拖动与滚轮、退出记档位）与其在冒险地图
场景里的入口。雷达已可直接嵌入。

## ADR-187：世界视图窗口拆成「模型 + 调用方循环」，缓存由调用方建 ◐

**背景**：参考的 `ViewWorld::ViewWorldWindow()` 是一个 200 行的模态循环，里面混着淡入淡出、`Display` 的整屏
操作、`LocalEvent` 轮询、`Assets` 取图与 `Interface::GameArea` 的读写。端口的 App 层（`AdventureMapScene`）
已经是「场景收事件、`IScene` 分发」的结构，UI 层的对话框按 ADR-163 只是模型。

**决定**：

1. **`ViewWorldWindow`（`Game/Kingdom`）只做状态与几何**：淡出矩形、雷达/缩放按钮/退出按钮/侧栏的位置、初始
   视图（含「整图放得下就居中」）、拖动/滚轮/按钮/雷达点击的换算，以及侧栏与两个按钮的 `PixelBuffer` 绘制。
   它不轮询事件、不淡入淡出、不持有 `Display`。
2. **缓存不在窗口里建**：参考的 `CacheForMapWithResources` 需要一层四档地形渲染器（MISC4/6/12 + 迷雾 +
   城镇/英雄图标），端口还没有；窗口因此**接收**已建好的 `ViewWorldMapCache`，`DrawWorld` 只负责贴图。
   这条边界让窗口现在就能测，也让配色渲染器可以单独落地。
3. **按钮区来自精灵尺寸**：参考 `Button::area()` 就是精灵的宽高，端口的窗口在构造时用
   `IViewWorldSpriteSource` 读出两张按钮图的尺寸；取不到就是空矩形，点击自然落空——比参考的断言宽松，比崩溃好。
4. **雷达只在开窗时重新着色**：`DrawRadar(display, renderMapObjects)` 的参数对应参考
   `RedrawForViewWorld( roi, mode, renderMapObjects )`：`true` 整图重着色（开窗一次），`false` 只重画光标框。
5. **档位夹取**：`ViewWorldZoom` 的构造对「档位 ≥ 档位数」抛异常（照抄参考的 `assert`），而设置文件里可能存着
   编辑器用过的第 4 档；窗口在构造前把它夹到最后一档，调用方不必先判断。
6. **`Close()` 就是持久化点**：它返回 `Zoom.ZoomLevel`，对应参考的 `conf.SetViewWorldZoomLevel(...)`；端口没有
   `Settings` 的等价项，写回留给 App（`IScene` 或 `GameHost` 的配置存储）。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldWindow.cs`（约 430 行）与 `ViewWorldWindowTests` 21 条；
Game 2 900 → **2 921**，合计 3 953 → **3 974** 全绿，`-warnaserror` 0 警告。

**遗留**：`CacheForMapWithResources` 的分块绘制器（四档地形 + 迷雾 + 图标）与冒险地图场景里的入口按钮，
两者都还没有；窗口本身已经可以被这些代码直接驱动。

## ADR-188：世界视图的缓存由「一个 576×576 的冒险地图视图」逐块渲染，城镇默认不透过迷雾 ◐

**背景**：`ViewWorldMapCache` 早已转好（18 格一块、缩到三档、贴到缓存图），但它要一个
`ViewWorldBlockDrawer`——参考里那是「把 `Interface::GameArea` 的 ROI 临时改成 576×576、逐块设中心、`Redraw`」。
端口直到现在只有测试里的假绘制器，所以世界视图还不能真正出图。

**决定**：

1. **`ViewWorldMapBuilder`（`Game/Kingdom`）**：持 `Mp2Map` + `TerrainTileSet` + `World` + 四个精灵源，
   `CreateView(mode)` 返回一个 576×576 的 `AdventureMapView`，`Build(mode, levels)` 把闭包
   `(block, center, layers) => { view.ScrollTo(center - 288); view.Draw(block, layers.Towns); }` 交给缓存。
   **一个缓存配一个新视图**，构建完即弃：端口没有全局地图区域，也就不存在「世界视图改掉冒险地图 ROI」的风险。
2. **英雄不进缓存**：参考把 `LEVEL_HEROES` 异或掉、改由 `DrawObjectsIcons` 贴图标，所以视图固定 `heroes: null`。
3. **迷雾随模式**：`layers.Fog` 决定传不传 `World.Fog`（全图模式传 `null`）。
4. **`AdventureMapView.Draw(target, drawTownsOverFog)`**：把参考的 `LEVEL_TOWNS` 显式化，**默认关闭**。
   端口原先的迷雾通道无条件把 `OBJNTWBA`/`OBJNTOWN` 补在迷雾上，等于冒险地图也把未侦察的城镇显形；
   参考里那个位只有「看城镇」「看全图」两个法术会设，冒险地图的主重绘从来不带。这是一次保真度修复，
   顺带让 `ViewTowns` 模式有了实现途径。
5. **不预校验地图边长**：`ViewWorldMapCache` 已经对「不是 18 的倍数」抛 `ArgumentException`，构建器不重复。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldMapBuilder.cs`（约 150 行）与 `ViewWorldMapBuilderTests` 8 条；
`AdventureMapView.Draw` 多一个参数（默认值保持「不画迷雾里的城镇」），`FogLayerTests` 拆成默认/显式两条。
Game 2 921 → **2 930**，合计 3 974 → **3 983** 全绿，`-warnaserror` 0 警告。

**遗留**：App 侧没有任何模态对话框设施，也没有淡入淡出，所以世界视图还没接到界面上；接入需要先有
「覆盖冒险地图的模态场景」这一层，再谈打开入口（参考从冒险选项对话框的世界地图按钮进）。

## ADR-189：世界视图的模态由「覆盖层 + 主机分流」拼成；产出型对象的收入在装图时写进元数据 ◐

**背景**：世界视图的模型、缓存、绘制、雷达与窗口都转好了，但它们只被测试驱动，界面上没有入口。参考的
`ViewWorldWindow()` 是**阻塞**循环（`view_world.cpp:589-795`），端口的 App 是「主机收 SDL 事件、场景收调用」，
没有嵌套循环；同时真实地图第一次冒烟就崩在两个 `GetValidItemsCount() == 1` 断言上。

**决定**：

1. **不引入嵌套循环**：`ViewWorldOverlay`（`App/Scenes`）只做循环里的两件事——把事件翻译成窗口调用、把窗口画满
   屏幕。开关（建缓存、写回档位）留给 `AdventureMapScene`，因为开窗要重建一张缓存，那属于场景的状态。
2. **主机按事件类型分流，不按「是否有对话框」分流**：`GameHost` 在世界视图打开时把鼠标移动/离开、滚轮、左键喂给
   覆盖层，键盘在 `HandleAdventureKey` 最前面拦截（`Escape` 关闭，其余忽略）。参考的循环也只认左键、滚轮、
   `Exit`/`Escape`；把拦截写进已经存在的几个 `if` 里，比给 App 造一套通用模态栈便宜。
3. **点击按「按下与松开同在一个按钮」判定**：照抄 `MouseClickLeft` 的语义，覆盖层记住按下位置再在松开时比较；
   地图拖拽则从按下就开始，中心由 `ViewWorldWindow.DragTo` 推。这样「按下 EXIT 再拖出去松开」不会误关。
4. **档位写回场景字段**：`CloseViewWorld()` 返回 `ZoomLevel?`，场景把它存进 `_viewWorldZoom`（参考的
   `conf.SetViewWorldZoomLevel`）。端口没有 `Settings`/配置文件对象，所以只留在内存里，下次开窗沿用。
5. **入口临时化**：参考从冒险选项对话框的「世界地图」按钮进，那个对话框没转；先用 `V` 键与 `--view-world`
   命令行开关顶上，两处代码注释写明是临时入口，等对话框落地再撤。
6. **收入元数据在装图时补**：`Tile.Init` 末尾调 `InitIncomeObjectMetadata`，按对象类型与 `EXTRAOVR` 车架号照参考
   (`map_object_info.cpp:4256-4277/4403/4423`) 填 `metadata[0]`（资源位标志）与 `metadata[1]`（每日产量）。MP2 文件
   没有元数据块，`metadata[0]` 只是 `quantity >> 3`，把对象信息里的事实放在装图路径上是唯一不散落的地方。
7. **多格对象「没有资源就不画」**：参考给整个对象的所有格子写同一份元数据；端口按格读 MP2，矿井邻格与资源堆邻格
   读到空 `Funds`。渲染器把 `assert( GetValidItemsCount() == 1 )` 换成「不是恰好一项就跳过」，宁可少画一个邻格图标，
   也不崩、也不画五份。

**后果**：新增 `src/FHeroes2.App/Scenes/ViewWorldOverlay.cs`（约 155 行）；`AdventureMapScene` 加
`OpenViewWorld`/`CloseViewWorld`（后者返回档位）；`GameHost` 加四处分流与 `V` 键；`CommandLineOptions` 加
`--view-world`；`Tile` 加 `InitIncomeObjectMetadata`/`SetIncomeMetadata`；`ViewWorldRenderer` 两处断言改守卫。
`TileMetadataTests` +7（装图路径的收入表），`ViewWorldRendererTests` +1（`metadata` 全零不请求精灵）。
Game 2 930 → **2 938**，合计 3 983 → **3 991** 全绿，`-warnaserror` 0 警告。App 层没有自动化测试，用
`--view-world` 冒烟截图（`artifacts/viewworld.png`）验证。

**遗留**：淡入淡出没做；正式入口（冒险选项对话框）未转；档位没有落地到配置；`OBJ_RESOURCE` 的多格邻格在参考里也会
各画一次图标（因为元数据被复制），端口现在只画带精灵的那一格——若要完全对齐，得在装图时把整组对象的元数据分发到
全部格子，这需要先有「对象 → 格子」的映射，暂不做。

## ADR-190：招募英雄确认框复用建造框的骨架；英雄默认名做成 `Hero.Name` 的回落；酒馆补位交给带 `World` 的重载 ◐

**背景**：建造列表的两个英雄位在第十三批只上报「点了哪个位」。参考在那里的下一步是
`Castle::DialogBuyHero( hero )`（`castle_town.cpp:66-160`）：标题、`SURRENDR.ICN` 帧 4 的 111×105 相框、大肖像、一句
说明、`PaymentConditions::RecruitHero()` 的 2500 金币价格框、OKAY/CANCEL。模型 `RecruitHeroDialog` 早在 ADR-163 就有
（`Title`/`Description`/`Cost`/`ConfirmText`/`CanRecruit`），缺的是视图、窗口与入口。

冒烟时另外撞到两个洞：(1) 两个英雄位永远是空的——`Kingdom.GetRecruits( World, ... )` 的补位重载写好了却**没有人调**，
`ActionNewWeek` 结尾叫的是不带世界的那个；(2) 对话框里写着「 is a level 1 Wizard without artifacts.」——`Hero.Name`
只有地图的自定义名字，73 个默认名字那张表（`heroes.cpp:84-121`）没搬。

**决定**：

1. **视图照搬建造说明框的分工**：`RecruitHeroLayout`（几何）、`RecruitHeroArtwork`（`SURRENDR` 帧 4 + `RESOURCE` 0~6 +
   `BUYBUILD` 六块边框）、`RecruitHeroView`（绘制与按钮）、`App/Scenes/RecruitHeroWindow`（窗口：光标、按键、说明框）。
   大肖像不进素材类——它是每个英雄的 `PORT00xx` 帧 0，由窗口按英雄取，所以同一个对话框可以给任何英雄用。
2. **高度不带领先的 10 像素**：`DialogBuyHero` 的标题写在 `dialogRoi.y + 2`，相框从标题高度之后才开始
   （`castle_town.cpp:104-114`），与建造框「先让 10 像素再放标题」不同，`GetContentHeight` 照参考只加四段与三个间隔。
3. **相框右键只上报**：英雄速览（`Dialog::QuickInfo`）没搬，窗口用 `IsPortraitUnder` 回答「是否点在相框上」，
   由场景记一条日志；两个按钮的右键说明（`Recruit X the Y.` / `Exit this menu without doing anything.`）照参考写。
4. **招人之后整屏回城镇**：参考在 `recruitHeroDialog()` 里 `castle->RecruitHero( hero )`，`_openConstructionDialog`
   随即返回，建造列表关掉、城镇屏留着并重画英雄像（`castle_dialog.cpp` 的 `RecruitHero` 分支把 `alphaHero` 归零）。
   端口因此是「关确认框 + 关列表 + `DrawTown()`」，不重开列表。
5. **默认名字做成 `Hero.Name` 的回落**：新增 `HeroNames`（73 条英文原文，顺序即编号），`Name` 变成
   `_customName` 为空时取表——这正是参考 `Heroes::GetName()` 的形状，改一处就修好所有要显示英雄名的地方。
   `FixFrenchCharactersInName` 同时收窄成只改自定义名字，否则会把默认名字写进 `_customName`，之后再也回不到表。
6. **补位放进带 `World` 的重载**：`Kingdom.ActionNewWeek( World, uint, bool )` 先跑原来的逻辑，再 `GetRecruits( World )`；
   `Kingdoms.NewWeek( World, uint, bool )` 把世界透传下去（七个王国自己不持有世界，ADR-111 不变）；
   `ConstructionWindow` 取英雄时改用带世界的重载——参考的 `castle_town.cpp:193` 叫的就是会补位的那个。
7. **脚本入口加 `--town-hero <0|1>`**：对话框正常要两次点击（列表的英雄位、再 OKAY），`--left-click` 只有一个，
   所以给一条命令行直接开框；同一次跑里再点 OKAY 就能验证整条「开框 → 扣 2500 → 回城镇」。

**后果**：新增 `src/FHeroes2.UI/Dialogs/RecruitHeroLayout.cs`、`RecruitHeroArtwork.cs`、`RecruitHeroView.cs`，
`src/FHeroes2.App/Scenes/RecruitHeroWindow.cs`，`src/FHeroes2.Game/Heroes/HeroNames.cs`；`CastleScene` 加
`_recruitHero`/`OpenRecruitHero( Hero )`/`OpenRecruitHero( int )`/`FollowRecruitHero`/`HireRecruitedHero`/
`ShowRecruitHeroHelp` 与四处分流；`ConstructionWindow` 加 `FirstHero`/`SecondHero`、取英雄改带世界；
`Kingdom` 加带世界的 `ActionNewWeek`，`Kingdoms.NewWeek` 改签名（唯一调用点在 `World.NewWeek`）；
`CommandLineOptions` 加 `--town-hero`。测试：`RecruitHeroViewTests` +6（布局算术、相框居中、按钮灰化、点击回答、
绘制落在框内），`HeroNamesTests` +12（表长与编号对位、越界、默认名回落）；Game 2 938 → **2 950**，UI 818 → **824**，
合计 4 408 → **4 426** 全绿，`-warnaserror` 0 警告。截图 `artifacts/recruit-hero.png`（对话框）与
`artifacts/recruit-hero-hired.png`（招完回城镇，金币 4500 → 2000，卫戍栏换成新英雄的像）。

**遗留**：相框右键的英雄速览（`Dialog::QuickInfo`）未搬；酒馆（`TavernWindow`）还没接这份名单；`AllHeroes` 的
`RECRUIT` 模式位与投降英雄的处理仍是 ADR-118 的状态；英雄名只有英文原文，没有翻译层。

## ADR-191：英雄速览框照参考的「固定内容框」排布；右键的「按住才显示」用松开清空实现；亡灵士气图标先灰再暗 ◐

**背景**：城堡屏三处右键都该弹英雄速览——招募确认框的相框（`castle_town.cpp:148`）、建造列表的两个英雄位、
城镇屏卫戍栏的英雄像（`castle_dialog.cpp:551-553`），而船长那一处（`:549-550`）端口还没有船长像。参考的
`Dialog::QuickInfo( const Hero & )` 是个**阻塞**模态框：`while ( le.HandleEvents() && le.isMouseRightButtonPressed() )`
——按住右键期间一直显示、松开就退出（`dialog_quickinfo.cpp:886-911`）。框里的东西全照 `showQuickInfo( HeroBase )`
（`:693-911`）：`QWIKHERO` 帧 0 当底板、`MINIPORT` 小肖像配 `LOCATORS` 帧 22 的相框、`MINILKMR` 六帧士气/幸运、
`FLAG32` 两面阵营旗、四维 + 魔法点 + 移动力六行、最后一行 `DrawSingleDetailedMonsterLine` 的军队。

**决定**：

1. **内容框是常数，底板尺寸只当输入**：参考把 `cur_rt` 重写成底板 `+28,+10` 起的 146x144，之后每个部件都是相对
   这个矩形的固定偏移。端口因此不写死 236x160，`HeroQuickInfoLayout.Create( box, 相框尺寸, 旗子尺寸 )` 只吃位置与
   两个会变化的尺寸，测试可以用任意合成底板验算术。
2. **「按住才显示」放在右键的两个端点**：`SetRightButton( down )` 开头 `if (! down) _heroInfo = null;`，按下时按命中
   建框。参考的模态循环换成了浮层，`DrawFrame` 拆成 `DrawWindows()` + 最后画速览框 + `InvalidateAll()`，
   这样三处入口共用一条绘制路径，也不会被各个 `return` 分支挡住。
3. **亡灵士气先灰再暗**：参考对 `MINILKMR` 的士气帧连续两次 `ApplyPalette`（GRAY、DARKENING）表示「亡灵没有士气」，
   端口把图标复制进临时 `PixelBuffer` 再调两次 `ImageOps.ApplyPalette`，不动原件。
4. **小肖像不进素材类**：`MINIPORT` 的「编号 - 1」帧属于每个英雄，跟招募框的大肖像一样由调用方给，缺了就不画。
5. **凑不出颜色的英雄不画旗**：`getCastleLeftFlagIcnIndex()` 只认六色与中立（`:196-220`，其余 `assert`），端口遇到
   组合色返回 `null`，宁可少画两面旗也不猜颜色。
6. **位置照 `makeRectQuickInfo`**：`StandardPopupWindow.PositionOf( 光标, 界面矩形, 尺寸 )`，界面矩形给整屏 640x480
   ——参考在城堡界面里也这么问（它拿的是冒险地图那块区域），端口不另造规则。

**后果**：新增 `src/FHeroes2.UI/Dialogs/HeroQuickInfoLayout.cs`、`HeroQuickInfoArtwork.cs`、`HeroQuickInfoView.cs`；
`CastleScene` 加 `_heroInfo`/`_heroInfoArtwork`/`_miniMonsters` 与 `ShowHeroQuickInfo( Hero )`，`DrawFrame` 拆成
`DrawWindows` + 覆盖层，`SetRightButton` 三处接线（招募框相框、建造列表英雄位、城镇屏英雄像）。测试：`HeroQuickInfoTests`
+15（内容框算术、相框居中、图标列上下移、旗子锚点、六行四维、军队线、图标数量、旗号映射、界面太小时拒绝、真实素材下
贴边与底板像素、名字落在内容框里）。UI 824 → **839**，合计 4 426 → **4 441** 全绿，`-warnaserror` 0 警告。
截图 `artifacts/hero-quick-info.png`（招募框上右键相框）与 `artifacts/hero-quick-info-list.png`（建造列表右键英雄位）。

**遗留**：城镇屏船长像的速览（参考 `:549-550`，端口还没画船长像）；英雄位的士气/幸运小图标；
`isFullInfo == false` 的简化版只有一条代码路径、没有入口去触发（敌方英雄的速览要等冒险地图的英雄交互）；
英雄速览里的阵营旗用的是 `FLAG32` 的左右两帧，与城堡图上的旗子同源，但玩家配色的淡入仍未搬。

## ADR-192：速览框改吃「英雄/船长快照」；船长大头像在 UI 层合成、画的时候才建；右键先徽记后英雄像 ✅

**背景**：参考的速览框一直吃 `const HeroBase &`（`dialog_quickinfo.cpp:693`），船长的入口是城镇屏左侧徽记上的右键
（`castle_dialog.cpp:547-548` 的 `Dialog::QuickInfo( GetCaptain() )`）。船长是 `HeroBase` 而不是英雄：`isActiveHero`
为假，所以名字后面没有 `" (Level N)"`、六行四维只画五行（没有 `Move Points:`），名字取城镇名、军队就是卫戍部队本身。
它的头像是 `Captain::GetPortrait()`（`castle/captain.cpp:200-250`）：大头像是 `PORT0090`~`PORT0095` 再贴该色的 `HEROFL`
帧 0（偏移见 `:64-84`），小头像是 `MINICAPT` 帧 0~5。城镇屏左侧立的那张也是大头像（`:138-145` 在船长住处建好后用船长像
替换徽记），端口此前只有徽记。

**决定**：

1. **端口没有 `HeroBase`，改成一份快照**：新增 `UI.Dialogs.HeroQuickInfoSubject`，由 `FromHero( Hero, 小肖像 )` 与
   `FromCaptain( Captain, 小肖像 )` 两个工厂取数，`HeroQuickInfoView.TryCreate` 只认它。判据是 `Level` 与 `MovePoints`
   为 `null`——正好对应参考的 `isActiveHero`：为空就不加等级后缀、不画移动力那一行，其余（幸运、士气、双旗、四项基本
   技能、魔法点、军队）一律照旧。用小肖像当参数外的第二份输入，是因为 `MINIPORT` 与 `MINICAPT` 是两套素材，素材读取留在
   调用方。
2. **肖像不进 `Game` 层**：`Captain.GetPortrait()` 拆成 `UI.Dialogs.CaptainPortrait`——它要读归档，而 `Captain` 只该
   管数值。`TryCreateBig` 克隆 `PORT00xx` 帧 0 再把 `ICN::getFlagIcnId( 颜色 )` 帧 0 贴到种族偏移上（参考也是把旗子贴到
   底图的拷贝上，原件不动），`TryCreateSmall` 取 `MINICAPT` 的种族帧。没有船长的种族（`MapRace.None`）一律返回 null。
3. **大头像懒加载、按「城 + 颜色」缓存**：船长住处是城镇屏里建的，所以不能在切城时就读；`TownCaptainPortrait( castle )`
   在画的时候才合成，同一座城、同一个颜色直接复用，城易主（颜色变了）才重贴旗子。
4. **右键顺序照参考**：先 `rectSign1`（徽记 → 船长）后 `rectSign2`（英雄像 → 英雄），命中区就用 `CrestArea`——参考的
   `rectSign1` 本来也是 `CREST` 的尺寸。船长没建出来时按键照旧落到英雄像上。
5. **`CrestArea` 只当命中区**：大头像 101×93 比 `CREST` 的 100×92 各多一像素，参考也是从 `+5,+262` 原样贴、不裁，端口
   照做；命中判定仍按 `CREST` 尺寸，与参考一致。

**后果**：新增 `src/FHeroes2.UI/Dialogs/CaptainPortrait.cs`、`HeroQuickInfoSubject.cs`；`HeroQuickInfoView` 改吃快照并
去掉 `portrait` 参数（`Hero` 属性换成 `Subject`）；`CastleScene` 加 `TownCaptainPortrait` 缓存与 `ShowCaptainQuickInfo`，
`ShowHeroQuickInfo`/`ShowCaptainQuickInfo` 共用 `ShowQuickInfo( subject )`；`CastleScreenFrame.CaptainPortrait` 终于有值；
`Captain` 的过时注释（「`MINICAPT` 没有枚举」）改掉。测试：新增 8 条（英雄快照与船长快照的差异、种族→头像/帧/旗偏移、
无船长种族、真实素材下大头像尺寸与小头像、旗子确实贴上且原件不动、船长只画五行、英雄仍画六行）。UI 839 → **847**，
合计 4 441 → **4 449** 全绿，`-warnaserror` 0 警告。截图 `artifacts/captain-portrait.png`（骑士船长与红旗）与
`artifacts/captain-quick-info.png`（右键徽记弹出的船长速览：城名、小肖像、两面紫旗、五行、军队行）。

**遗留**：船长像的淡入（参考 `RedrawIcons` 用 `AlphaBlit`，端口直接换）；`--town-build Captain` 之后 `Captain.LoadDefaults`
是构造时做的，参考在住处建好时会再灌一次默认（当前数值相同，等有增益建筑再补）；船长的战斗界面（`battle_only` 模式）
未接；酒馆名单、`RECRUIT` 模式位、敌方英雄的 `isFullInfo == false` 路径照旧。

## ADR-193：酒馆窗口复用 MessageBox 的两段 `MultiFontText` + `TAVWIN` 动画；模态框压过城镇屏、右键不关 ✅

**背景**：`castle_dialog.cpp:697-701` 的 `BUILD_TAVERN` 分支调 `Castle::_openTavern()`
（`castle_tavern.cpp:39-54`）：本周传闻加一句固定引子，配 `TAVWIN.ICN` 的吧台手动画，再用
`showStandardTextMessage( GetStringBuilding( BUILD_TAVERN ), {}, Dialog::OK, { &imageUI, &textBodyUI } )` 弹框。
参考只组两个元素——`AnimationDialogElement( ICN::TAVWIN, { 0, 1 }, 0, 75ms )` 与一个装了两段 `MultiFontText` 的
`TextDialogElement`——没有独立的窗口类。亡灵的酒馆位是 `BUILD_SHRINE`，走的是说明框，不是这个函数。

**决定**：

1. **模型与渲染分开，渲染复用 `MessageBox`**：`TavernDialog` 只给标题、引子、本周传闻与 `DialogButton.Ok`；
   `TavernView.TryCreate` 把 `TAVWIN` 精灵与 `BUYBUILD` 框、两套字体、`SYSTEM` 按钮拼成 `MessageBox`。
   `frameCount = min( 20, 精灵数 - 2 )`，连一帧吧台手都没有（少于 3 张）就返回 `null`，不画半截动画。
2. **两段文字仍是两个 run**：引子与传闻各是一个 `Text`，中间 `"\n\n"`；因为没有移植本地化子系统（ADR-139），
   两段都用 `FontType.NormalWhite`，传闻的语言标签只留在 `LocalizedString` 里。空传闻由 `TavernDialog.Create`
   直接拒绝——地图 loader 从不存空传闻，空值只可能是 bug。
3. **动画参数照参考当常数**：背景帧 `{ 0, 1 }`、首帧 2、20 帧、每帧 75ms（`DelayType::CASTLE_TAVERN_DELAY`）。
   参考的 `ICN::getAnimatedIcnIndex()`（`2 + n % 20`）由 `AnimationDialogElement` 的「首帧 + 帧数」表达，
   不回头看 ICN 文件；素材读取留给调用方，和其它窗口一致。
4. **模态层级最高**：`CastleScene` 的 `_tavern` 在说明框、英雄速览和所有建筑窗口之前判；`SetCursor`、`SetLeftButton`、
   键盘、`Update`、`DrawWindows` 五处都先看它。`Update` 只推吧台手的动画，城镇屏自己的帧不动；
   `DrawWindows` 每帧先把城镇屏照常重画一遍再叠框，因为参考的 `showStandardTextMessage` 是「在当前画面上摆框」。
5. **只有 OK 能关**：左键在按钮上完成按下/松开、ESC/Enter/小键盘 Enter 三键都当 OK；右键直接忽略（`Dialog::OK`
   不认右键），不会像收入框那样按住右键显示。开框时顺手清掉 `_message`/`_messageHeldByRightButton`/`_heroInfo`。
6. **入口走 `OpenBuilding` 的 `Tavern` 分支**：`OpenTavern()` 要求城镇已建酒馆、`BUYBUILD` 框、`TAVWIN` 精灵与
   `SYSTEM` 按钮四样齐备，缺一样只记日志不开框。亡灵建的是 `Shrine`（`castle_dialog.cpp` 的说明框分支），
   所以这个 `Tavern` 分支不会顶掉亡灵的神龛。
7. **冒烟入口**：`--town-window tavern`、`--town-building Tavern`；未建酒馆时先加 `--town-build Tavern`。
   城镇屏点击坐标依赖种族，而 Beltway 的玩家城是随机城、载入时重掷种族（`World.Castles.cs`），固定坐标只在
   对应种族的那次启动命中——实测骑士酒馆区中心 `(386,145)` 开窗成功，其余几次启动是别的种族、点在同一处不会开。

**后果**：新增 `src/FHeroes2.UI/Dialogs/TavernView.cs`；`CastleScene` 加 `_tavern`、`OpenTavern()`、`CloseTavern()`
与五处优先级判断，`GameHost` 的 `--town-window` 加 `tavern`。测试：`TavernViewTests` +7（常数与参考一致、
少于 3 帧拒绝、两个元素与吧台手尺寸、单个居中的 OK、按下松开返回 OK、75ms 后换帧、六个参数都不许空）。
UI 847 → **854**，合计 4 449 → **4 456** 全绿，`-warnaserror` 0 警告。截图 `artifacts/tavern.png`（酒馆框）、
`artifacts/tavern-from-click.png`（点城镇屏酒馆建筑开框）、`artifacts/tavern-closed.png`（点 OK 后回到城镇屏）。

**遗留**：玩家配色与淡入（城堡屏主线只剩这一项）；船长战斗界面（`battle_only`）；随机城的固定坐标冒烟不可复现，
要么等地图随机种子可设，要么给 CLI 加按建筑名取中心的点击入口；酒馆传闻的多语言字体仍随本地化一起缺。

## ADR-194：城堡旗帜按玩家配色——152~174 整段线性映射到六色色阶，只有六张图、按帧裁矩形 ✅

**背景**：`drawCastleDialogBuilding`（`ui_castle.cpp:110-148`）在 blit 之前先问
`isImagePlayerColorDependent()`（`ui_castle.cpp:150-165`），命中就用 `getModifiedByColorImage()` 现做一张换过
调色板的图：`getColorEffectiveAreas()`（`:85-134`）给出这一帧里旗帜所在的矩形，`getModifiedPaletteByPlayerColor()`
（`:61-83`）给出从调色板 152~174（十六色加七级阴影）线性映射到该玩家色十六色色阶的 256 项表。命中名单只有六张：
骑士的城堡、左塔、右塔、五级住宅 `TWNKDW_4` 与升级版 `TWNKUP_4`、法师的城堡；死灵、野蛮人、女巫、术士的城堡
本来就没有玩家色的旗。无主的城（`PlayerColor::NONE`）在参考里会断言失败，正式版本退化为恒等映射。

**决定**：

1. **表就是恒等表加一段**：`CastleFlagPalette.ForColor()` 建 256 项表，只重写 152~174 这 23 项，其余保持下标。
   映射公式照参考 `outputStart + i * outputLength / inputLength`；六个玩家色的段是蓝 `63/22`、绿 `85/23`、
   黄 `108/23`、紫 `132/21`、红 `175/23`、橙 `199/16`。无主直接给恒等表，不抛断言。
2. **只改旗帜所在的矩形**：`EffectiveAreas()` 逐图逐帧照参考写死——法师城堡帧 0 是 `(76,7,3,3)`、`(96,7,3,3)`、
   `(178,11,3,3)`；骑士城堡帧 0 是 `(127,36,3,3)`、`(287,6,3,3)`；骑士左/右塔帧 0 是 `(5,6,3,3)` / `(55,6,3,3)`；
   五级住宅两种帧 0 是一像素 `(61,3,1,1)`、其余帧 `(59,0,6,5)`；其它帧一律整帧。整帧也不怕，因为表在 152~174
   之外是恒等。
3. **按「图 + 帧 + 颜色」缓存**：参考每画一次就新建一张，端口把结果留在 `CastleFlagPalette` 的字典里，动画二十帧
   与六个颜色最多一百多张，缓存的图与原图一样只读。`Get()` 对无主与非旗帜图直接返回原精灵，连克隆都不做。
4. **颜色一路从城堡传下来**：`CastleScreenPainter.DrawBuilding()` 末尾加 `PlayerColor color = PlayerColor.None`，
   全部二十一处在调用时补 `castle.Color`（原来带 `alpha` 的位置参数调用改成显式尾部实参，测试里的 `alpha:` 具名
   调用不受影响）。构造窗那侧的 `CastleImagePainter` 用 `OBJNTOWN`，参考不换色，保持不动。
5. **顺手让冒烟日志带上种族**：`--enter-town` 的日志改成
   `Opened the town screen of 'Quick Silver' (Wizard) from the command line.`，因为随机城每次载入重掷种族，
   点固定坐标的冒烟必须知道这一局是哪个族（上一条遗留的坑）。

**后果**：新增 `src/FHeroes2.UI/Dialogs/CastleFlagPalette.cs`；`CastleScreenPainter` 的 `DrawBuilding` 与二十一
处调用改签名，类注释从「未搬玩家配色」改成「配色见 `CastleFlagPalette`，未搬的只剩 `AlphaBlit`」。测试新增
`CastleFlagPaletteTests` 21 条（常量与六色段的首尾值、段外恒等、无主恒等、六张图名单、逐图逐帧的矩形、
只改旗像素、动画帧整帧、无旗帜图原样返回、缓存按颜色共享）。UI 854 → **875**，合计 4 456 → **4 477** 全绿，
`-warnaserror` 0 警告。截图 `artifacts/town-flag-color.png`：红玩家的法师城，三面旗帜是红的。

**遗留**：`AlphaBlit` 的半透明混合（新建筑淡入仍是一步到位，城堡屏主线只剩这一项）；船长战斗界面
（`battle_only`）；酒馆传闻的多语言字体随本地化一起缺。

## ADR-195：王国侧 AI 按「扫描 → 评估 → 决策」三段落地，KingdomTurn 的回合循环等 B5 ✅


**背景**：`ai_planner_kingdom.cpp` 的 `Planner::KingdomTurn()`（`:660-900`）是一整段：清缓存、数英雄、
逐瓦片扫描、`evaluateRegionSafety`、`setHeroRoles`、优先级目标的重算、城堡循环、英雄循环。端口里英雄循环的
第一个调用就是 `HeroesActionComplete`（`ai_planner_hero.cpp`，B5 范围），所以这段在 B4 无法整段落。

**决定**：

1. **扫描段抽成 `internal int ScanMap(GameKingdom)`**：清缓存 → 按 `world.RegionCount` 建 `_regions`（瓦片
   的 `Region` 直接做下标）→ 数非巡逻英雄 → 逐瓦片填 `_mapActionObjects`/`_enemyArmies`/区域统计 →
   收尾调 `EvaluateRegionSafety` 与 `UpdateKingdomBudget` → 返回 `availableHeroCount`。参考把这些写在循环里，
   端口分开是因为「`evaluateRegionSafety` 的产物只被扫描产生」——写在两处容易漏；B5 的 `KingdomTurn` 只剩
   「`ScanMap` → `SetHeroRoles` → 城堡循环 → 英雄循环」。
2. **对象类型一律先折叠（ADR-133）**：瓦片缓存的是带动作位的值（英雄 `ActionHero` = 183），
   `GetEnemyArmyOnTile` 原先拿它比 `Mp2ObjectType.Hero` 永不成立，本批改成 `Mp2ObjectTypes.NonActionType`；
   `_mapActionObjects` 仍存瓦片原值，与 `UpdateMapActionObjectCache` 保持同一口径（两者在同一字典里混存会不自洽）。
3. **`View All`、`SLEEPER`、次元门次数不移植**：端口没有冒险地图施法，扫描只认「不在雾里」的瓦片，
   参考里的 `bestHeroToViewAll` 与每英雄的 `ResetModes(SLEEPER)`/`setDimensionDoorUsage(0)` 都没有对应状态，
   注释里逐条写明（有状态时再补，不预留空方法）。
4. **测试窗口只开必要的四个**：`Regions` 只读属性 + `SetHeroRoles`/`GetSortedCastleList`/
   `PurchaseNewHeroes` 由 private 改 `internal`。生成的空地图没有城堡表（`GetCastleEntrance` 只在装图时
   登记），所以「友方城堡 +100 / 敌堡 -100」两支在合成世界上跑不到，测试覆盖「敌方英雄 → -50 → 邻区取份额」、
   撤走英雄后归零、受威胁城堡排前与招募上限。

**后果**：新增 `src/FHeroes2.Game/Ai/AiPlanner.Kingdom.cs`（十个成员 + `ScanMap`）与
`tests/FHeroes2.Game.Tests/AiKingdomPlannerTests.cs` 8 条；`AiPlanner` 改 `partial` 并删掉十个占位；
`ai_planner.cpp` 的最后一件 `revealFog` 一并落地，该文件全转。`ai` 行 4/1/4，合计 68/84/97，
剩余 `NotImplementedException` 60 处。`KingdomTurn` 的回合循环与 `HeroesActionComplete` 留待 B5。

## ADR-196：`AIWorldPathfinder` 的六个英雄入口由调用方显式给英雄，迷雾有两张表 ✅


**背景**：`world_pathfinding.cpp:566-1434` 的 AI 寻路器在端口里早已落地，只有六个英雄相关入口还抛异常：
`ReEvaluateIfNeeded`、`getFogDiscoveryTile`、`getNearestTileToMove`、`isHeroPossiblyBlockingWay`、
`buildDimensionDoorPath`、`getObjectsOnTheWay`（参考里这些方法大多直接读缓存的 `_pathStart`/`_pathColor`）。
B5 的 `ai_planner_hero.cpp` 依赖其中五个，所以必须先收。

**决定**：

1. **六个入口都接收 `Hero` 参数**：参考在 `reEvaluateIfNeeded` 里把英雄写进搜索上下文，之后的方法只读缓存。
   端口把英雄一路显式传下去，因为「缓存里哪一任英雄」在测试和未来的多英雄规划里都不可靠；单参
   `ReEvaluateIfNeeded(Hero)` 保留参考语义（军力自家算），双参版留给已知军力的调用方。
2. **`GetFogDiscoveryTile` 的 bool 改名为 `IsTerritoryExpansion`**：参考的注释写明第二项表示「雾就在这个瓦片
   旁边」，用于决定扩张还是探路，与坐船无关；原命名 `IsBoat` 会误导 B5 的调用方，行为未改。
3. **迷雾仍按两张表走**：`World.ClearFog` 只写 `MapFog`，寻路器只读 `Tile._fogColors`（ADR-061/ADR-079 的
   历史结果）。本批不改这条接线：`GetFogDiscoveryTile` 照参考「瓦片雾判相邻、`MapFog.CountFoggedAround`
   数可揭示格」，测试自己把两张表都写一遍，并在注释里说明原因。合并两张表要单独一条 ADR（会动 `World`、
   渲染与存档），不混在本批。
4. **次元门的空路由直接返回空表**：参考在 `_dimensionDoorMovementCost == 0 || _dimensionDoorSpCost == 0`
   处是 `assert`；端口返回空列表，避免调用方（B7 的 `HeroesCastDimensionDoor`）拿到死循环的落点序列。
5. **测试用竖长合成图**：`World.ComputeStaticAnalysis` 用列号乘宽当行首（与参考同款转置），宽度大于高度的
   合成图会越界。本批测试统一用 1×N 走廊，不修这个遗留问题（与 X1 无关）。

**后果**：`AIWorldPathfinder` 的 13 处 `NotImplementedException` 归零，`AIWorldPathfinderTests` 13 → 26 条；
随批补 `Hero.CanCastSpell`、`Hero.DimensionDoorUses`、`Difficulty.GetDimensionDoorLimitForAi`。
Game 测试 2991 → 3003，全量 4 530 条全绿；全解决方案 0 警告。`world_pathfinding` 的 AI 侧不再有占位，
B5 只剩对象价值表（X2）。


## ADR-197：被依赖文件先落接口骨架（`maps_tiles_render` 先行，G3 才动 `GameArea`） ✅


**背景**：G3 是 `gui/interface_gamearea.cpp`（1 053 行），但它的四个绘制入口——`Redraw`、`redrawOnlyFog`、
`renderTileAreaSelect`、`generateUltimateArtifactAreaSurface`——全部踩在 `maps/maps_tiles_render.cpp`
（1 327 行 / 1 118 非空行）上（`getTileSurface`、`drawFog`、`redrawBottomLayerObjects`、
`getMonsterSpritesPerTile` 等）。`docs/PLAN.md` 的「还差的主干」一直把这个文件记成 `maps` 目录最后一块
「地形绘制端口还没有等价物」。现有 `Game/AdventureMap/AdventureMapView.cs` 走的是 `Mp2Map`/`Mp2TileInfo`
的过渡渲染器（ADR-035/036），不是 `Maps::Tile` 部件模型的移植，不能当等价物。

**决定**（用户 2026-11-22 明确）：

1. **依赖顺序不破**：先把被依赖文件的**接口骨架**落下（签名 + 文档注释，方法体抛 `NotImplementedException`），
   再写调用方；调用方按参考逐行写调用，不写兜底，也不复制一份「先能跑」的近似实现。
2. **这条对目录内的单个文件同样成立**，不只对目录。今后凡是「已转目录里的文件踩到还没转的文件」，一律照此
   办理：被依赖文件先落骨架、调用方照参考写、实现随后补。已写进 `docs/PLAN.md` 总体原则第 6 条的「依赖先落接口」。
3. **落地**：`UI/Interface/TileRendering.cs` = `maps_tiles_render.cpp` 的 16 个入口（空瓦片 / 飞行幽灵 / 顶层部件 /
   雾 / 可通行性 / 四层地面部件 / 按 ICN 类型绘制 / 怪物·空船·矿守卫·英雄的四组精灵清单 / `getTileSurface`），
   名字、参数顺序、含义与参考一致，`Maps::` 限定符按端口命名空间去掉；`Game/Maps/ObjectRenderingInfo.cs` =
   `gui/ui_object_rendering.h` 的 DTO（参考本来就是 header 内实现，所以直接写全，不是桩，H 行
   「`+ui_object_rendering.h`」这一半提前完成）。

**记在骨架里的两处结构偏差**：

- **`TileRendering` 落在 UI 层**：参考文件在 `maps/`，但每个入口都收 `const Interface::GameArea &`，而端口的
  `GameArea` 属 `FHeroes2.UI`、`FHeroes2.Game` 不引用 `FHeroes2.UI`。`editor/` 阶段若要复用
  （`redrawPassable` 等），届时再定第二处落点，或改成显式几何参数——不在本批预先抽象。
- **`ObjectRenderingInfo.icnId` 用 `IcnType` 而非 `int`**：端口有 `ICN::` 枚举（`agg/icn.h` 的 1:1 表），参考的
  默认值 `-1` 对应 `IcnType.Unknown`（`IcnCatalog` 唯一没有文件名的成员）。

**后果**：G3 可以整段按参考写调用并能编译（运行时抛「未移植」，不会把过渡渲染器当结果）；`TileRendering` 的十个
匿名命名空间助手属实现细节，骨架里不出现死代码。构建 0 错误 0 警告，测试基线不变（4 945 绿）。

## ADR-198：联盟表按参考落地（MP2 头算、存档读、名册用） ✅

**背景**：参考把「谁是朋友」放在玩家名册上：`FileInfo::unions` 是一张按颜色索引的集合表，地图载入后
`player->SetFriends( fi.unions[index] | color )`（`players.cpp:325`）；MP2 头在「击败另一方」的地图上按胜利条件的
第 0 个参数把王国颜色切成两侧（`maps_fileinfo.cpp:341-371`），存档文件则整表存（`maps_fileinfo.cpp:664-675`）。
端口的 `Mp2FileInfo.PlayerUnions` 有字段但没有计算，`Players.Init` 只写「自己」，`Hero.IsFriends` 退回同色比较，
`CampaignData.UpdateScenarioGameplayConditions` 抛 `NotImplementedException`——同盟玩家互相视为敌人。

**决定**：

1. `Mp2FileInfo.PlayerUnions` 就是参考的 `unions`：`PlayerColor` 是位标志，每项是一个颜色集合；默认
   `PlayerColors.Ordered`（每人自己）。`Read` 的 `DefeatOtherSide` 分支按参数 0 切边，参数非 0 且小于王国色数才
   合法，否则整张图读不出来（参考 `return false`，端口抛 `BinaryFormatException`）。
2. `Players.Init` 写 `unions[i] | color`（自己也并进去，参考如此），`Hero.IsFriends` 改为问名册；名册没建好时
   保持参考的 false。
3. `CampaignData.UpdateScenarioGameplayConditions` 把「含 AI 颜色、又不是人类那一方」的项改成全部 AI 颜色，
   人类项不动。战役开局流程（`game_campaign.cpp`）还没接，等 P4 接线，不做临时调用。

**后果**：同盟地图上通行、战斗与 AI 敌友都走同一张表；「击败另一方」的地图以前被当成人人互相为敌，现在按地图
参数分边。`AiHeroMovementHandlerTests` / `AIWorldPathfinderTests` 改挂 `SettingsSingletonCollection`：AI 动作后要按
进程级的 `World.Current` 刷缓存，此前与换世界的测试并行跑会偶发「世界 0 格」。构建 0 错误 0 警告；测试 5 490 绿
（新增 3 条 MP2 头联盟测试、2 条战役并方测试）。

## ADR-199：漂流瓶走自己的动作分支，不混进取物 ✅

**背景**：参考里 `ActionToPickupResource()`（`heroes_action.cpp:752-793`）一处处理四种对象：散落资源、营火、木桶、
漂流瓶。前三者算钱进国库，漂流瓶只做 `showSignMessage( dst_index )`；四者在函数末尾一起播拾取音效、跑淡出动画、
`resetObjectMetadata()`。取物那条 ADR 曾把漂流瓶挡在 `IsPickupResource()` 之外，理由是「留言文本系统还没落地」，
于是英雄走到瓶子上返回 `HeroActionKind.None`——什么也不发生。

**决定**：

1. **数据层早就在**：`World.MapObjects` 里的 `MapSign` 同时承载 `OBJ_SIGN` 与 `OBJ_BOTTLE` 的信息块
   （`world_loadmap.cpp:624-629`），`MapTextCodec` 的中文/法文修复也早把漂流瓶串算进去了。缺的只是动作分支。
2. **单独一支 `HeroActionKind.Bottle`**，不借用 `Sign`：牌子可以反复读、瓶子读一次就没了，调用方（将来的界面）
   要据此决定「要不要播淡出、要不要重绘瓦片」，混成一个 kind 就分不出来了。
3. **`ActionToBottle` 自己清元数据**：`Tile.ResetObjectMetadata()`。参考把它写在函数末尾（所有分支共用），端口
   在每个分支自己的函数里收口，行为一致；淡出与移除主物件仍归界面（`ObjectFadingOutInfo`），与资源、骷髅一致。
4. **不 `SetVisited`、不动国库**：参考没有访问名册这一步，瓶子靠「元数据清空 + 物件移除」消失。
5. **AI 侧不改**：`AiToPickupResource()` 早已把 `Bottle` 当资源抹掉（不读留言，参考也不读）。

**后果**：英雄读到漂流瓶会拿到留言文本（`Payload` 是 `string?`）并清掉瓦片元数据；`HeroSignTests` 加两条
（直接调用 `ActionToBottle` 与走分发器），全量测试 5 492。界面消费 `HeroActionKind.Bottle`（弹留言框 + 淡出）仍待
App 的消息层，与 `Sign` 同一个缺口。

## ADR-200：配置文件读写照参考落进 `Settings`，键集合按端口已移植的设置项 ✅

**背景**：参考用 `fheroes2.cfg` 记住这台机器的偏好——音量、速度、全部开关、隐藏界面时四个窗口的位置、语言、
「第一次运行」标志（`Settings::Read()`/`Save()`，`settings.cpp:145-583`），格式由 `engine/tinyconfig.cpp` 的
`TinyConfig( '=', '#' )` 解析。端口 `Settings` 只有内存态，类注释里明写 `Read`/`Save`/`getOptionsString` 未移植，
连带 `IsFirstGameRun` 恒为 true（没人清、也没有文件可清），首运提示与语言选择窗都没法接。

**决定**：

1. **`TinyConfig` 单独成类**（`Game/System/TinyConfig.cs`），读文件与解析分开：`LoadFile` 碰磁盘，`Parse` 只吃文本。
   解析规则照参考：键小写 + 去首尾空白 + 连续空白压一个、重复键留第一个（`emplace` 语义）、无分隔符的行跳过；
   `IntParams` 整串必须是数字否则 0（`from_chars` 不接受尾随垃圾），`PointParams` 只认 `[ x, y ]`，
   `ResolutionParams` 先认 `宽x高:宽x高`，再认旧格式 `宽x高x倍数`，倍数为 0 或乘出溢出时回退。
2. **配置读写放 `Settings.Configuration.cs`**（partial，与 `Settings.Persistence.cs` 并列）。`Persistence` 那份是
   跟着一局游戏走的五个字段，配置文件是这台机器的偏好，读档不覆盖，两者不混。
3. **路径归宿主**：参考的 `Save()` 自己拼 `GetConfigDirectory( "fheroes2" )`，端口没有 `dir.cpp`，所以 `Save(string)`
   收完整路径；热键文件已是这个分工（`GameHotKeys` 收路径），保持一致。
4. **三个键按「有状态才写、有状态才读」处理**：`videomode` 与 `game window position` 属于 UI 层的分辨率与窗口，
   通过 `Save`/`GetOptionsString` 的可选参数写出去，`Read` 读到也跳过；`debug` 属于未移植的日志层，不写不读。
   其余键全部照参考的键名、默认值与注释写，两份文件可以并排 diff。
5. **`first time game run` 只认 `off`**：缺键、`on`、其它值都不动标志——参考只在读到 `off` 时 `resetFirstGameRun()`。

**后果**：`Settings` 能把自己完整写出去再读回来（`SettingsTests` 的往返用例逐字段断言）；`TinyConfig` 的边界
（缺文件、注释、重复键、非数字、溢出的旧分辨率）各有测试，全量 5 506 绿。**启动时读、退出时写尚未接线**：
分辨率/全屏/隐藏界面位置的真正生效要等 UI 层消费这些设置，与语言选择窗、`BaseInterface` 的淡入一起做（P6）。

## ADR-201：配置文件接进启动流程，加 `--config`/`--no-config`，语言以 `--lang` 为先 ✅

**背景**：ADR-200 落了 `Settings.Read/Save`，但没人调；参考在 `Game::Init()` 的 `initConfig()`
（`game_init.cpp:217-230`）里读它，读不到就立刻写一份默认的，之后由设置窗、热键、雷达拖动各处
`conf.Save()` 逐步落盘。端口要接这条链，同时不能让它干扰像素基线。

**决定**：

1. **路径是纯函数**（`Game/System/ConfigurationFile.cs`）：`DefaultPath( appData )` 拼
   `<appData>\fheroes2\fheroes2.cfg`（对应 `GetConfigDirectory( "fheroes2" )` + `Settings::configFileName`），
   `Resolve( appData, explicit )` 让显式路径优先，`EnsureDirectory` 对应 `MakeDirectory( configDir )`。
   「`%APPDATA%` 在哪」留给宿主（`Environment.GetFolderPath`），Game 层不读环境变量，测试直接喂临时目录。
2. **两个新开关**：`--config <file>` 与 `--no-config`。参考没有它们（只在固定目录里找），端口加是因为同一个 exe
   既跑游戏又跑像素基线：开发机上的 `hide interface = on`、`system info = off` 会把基线搅乱。五个
   `Verify*Baseline.ps1` 一律带 `--no-config`，基线从此与「这台机器有没有配置文件」无关。
3. **读不到就写默认**：`GameHost.LoadSettings()` 在 `Read` 失败或缺文件时 `EnsureDirectory` + `Save`，与
   `initConfig` 的 else 分支一致；写不出去（只读目录）只降级成「偏好不保存」，客户端照常跑。
4. **保存在退出与语言变更两处**：参考在各修改点各自保存，端口没有那么多修改点（设置窗只有语言行），统一在
   `Loop()` 结束与 `MainMenuScene.LanguageChanged` 各写一次，是参考所有保存点的超集；`videomode` 写
   `<640x480>:<当前窗口>`。
5. **语言优先级 `--lang` > 配置文件 > 英文**，且 `StartLanguage` 要把 `--lang` 单独再取一次——`--no-config` 时
   `LoadSettings` 提前返回，配置文件里的语言根本没被读进来，而基线正是「`--no-config` + `--lang zh_CN`」。

**后果**：语言一旦在设置窗里改过就会被记住，`first time game run` 留在文件里等首运 UI 消费（§7 的 P6 剩余项）。
顺带发现五张像素基线从 2026-11-14 起就落后（主菜单右下角的 Editor 招牌没进基线，恒差 9 867 像素），已重建并
复验 `identical`，记在 `tests/baselines/README.md` 的「2026-11-24 的重建」。
**没做**：首运的语言选择窗与欢迎消息、`videomode`/`fullscreen`/隐藏界面位置的生效、`IsFirstGameRun` 的清除。

## ADR-202：首运的窗口做成场景级模态，触发条件是「真的读到了配置文件」 ✅

**背景**：参考 `game_mainmenu.cpp:265-300` 在进事件循环之前阻塞地跑 `selectLanguage`、问候消息、分辨率选择，
最后清旗标并保存。端口的主菜单是帧驱动的场景，`GameHost.RunModal` 那套阻塞循环只适合 `showStandardTextMessage`
这类一条命的消息；把整个首运塞进去，`--screenshot` 和 `--smoke-frames` 就没法再拍照和退出（窗口在等输入）。

**决定**：

1. **场景级模态**：`MainMenuScene.BeginFirstRun()` 把 `LanguageSelectionDialog` 放在菜单上，输入路由、绘制、
   `HasModalDialog` 都沿用设置窗那一套；问候用 `MessageBox` 加一个 OK 按钮，同样由场景收键。帧循环照跑，
   所以自动化开关仍然可用。参考里「窗口下方是菜单」的视觉顺序也因此保留。
2. **触发条件是 `_configFile is not null && Settings.IsFirstGameRun`**，而不是只看旗标：`--no-config` 时
   `LoadSettings` 连文件都没碰，`IsFirstGameRun` 停在默认 `true` 上；只看旗标的话，五个像素基线和所有脚本
   都会停在语言选择窗上。旗标是配置文件的产物，就跟着配置文件走（与 ADR-201 的 `--no-config` 同一条线）。
   `--open-settings`/`--open-scenario` 先占了窗口时 `BeginFirstRun()` 让路，旗标留到下一次启动。
3. **问候只写欢迎段**：参考的三段里，「请选择游戏分辨率」后面紧跟 `Dialog::SelectResolution()`，「触摸输入」
   跟着触摸检测；端口两样都没有（`videomode` 只写不读，也没有触摸输入），照抄就是空承诺。少写两段而不是
   写一段假的，差异记在 PROGRESS §6.6。
4. **CANCEL 帧进 `SettingsArtwork`**：`SYSTEM.ICN` 的第 3、4 帧是 CANCEL 按钮，语言窗是第一个真正需要它的窗口；
   档案里缺帧时 `HasCancel` 为假，语言窗退回只画 OK，不崩。
5. **语言落地**：菜单在既无 `--lang`、配置里也没有 `lang` 时会自己落到系统语言上，宿主在 `CreateScenes` 里把这个
   结果记进 `Settings`，否则文件里的 `lang` 空着、界面却是中文，下一次启动又得重新猜。

**后果**：首运的语言窗与问候能出能关，`--config` 脚本要先写 `first time game run = off` 才不会被窗口拦住（文档已写）。
**验证边界**：合成点击一轮只有一次（`--left-click` 定在第十帧、`--cursor` 只有一个），语言窗 → 问候的截图与日志
都抓到了，但「点问候的 OK 之后清旗标」没有端到端截图；那一步由 `Settings.ResetFirstGameRun()` + `Save` 的持久化测试
（`SystemSettingsTests.ASavedResetFirstGameRunStaysClearedInTheFile`）加四行接线守住，要端到端就得让脚本能连点两个位置。
**没做**：分辨率选择窗与 `videomode` 的生效、触摸输入、`MainMenu` 退出时的窗口位置保存。
（2026-11-24：`videomode` 与窗口位置见 ADR-204，分辨率选择窗见 ADR-205，只剩触摸输入没人用。）

## ADR-203：会话难度用 `--difficulty` 先落地，起始资源与城堡默认建筑都跟随 `Game::getDifficulty()` ✅

**背景**：参考在剧本信息屏上用难度光标选难度（`game_scenarioinfo.cpp` 的 `ActionCursor` 分支），选中的那一档
进 `Game::getDifficulty()`，之后 `Kingdom::Init()` 拿它发起始资源（`kingdom.cpp:103`）、`Castle::_setDefaultBuildings()`
拿它抽二级兵营（`castle.cpp:542`）。端口还没有剧本信息屏（没有 `NGHSBKG`、没有难度图标），但 `Kingdom.GetStartingResources`
与 `Castle.SetDefaultBuildings` 都已经按难度分表，缺的只是「谁把这一档告诉它们」。在此之前 `World.LoadMap` 拿地图头里
的 `Mp2FileInfo.Difficulty` 顶上，这跟参考口径不一致：地图头那一档只参与 `Game::GetRating()`。

**决定**：

1. **`--difficulty <easy|normal|hard|expert|impossible|0..4>`** 作为玩家的选择的过渡入口，解析在
   `CommandLineOptions.ParseDifficulty`，非法值在参数解析阶段就报错。它是会话级开关，**不写进配置文件**
   （与 ADR-201 的 `--lang` 同一条线：命令行优先且不落盘），`GameHost.ApplySessionOptions()` 把它写进
   `Settings.SetGameDifficulty`，日志留一行 `Difficulty set to ... on the command line.`。
2. **`World.LoadMap( map, difficulty )` 记下这一档**：新增 `World._difficulty`（参考的 `Game::getDifficulty()`
   在端口没有对应的全局对象，装载时记在世界里），`CreateMapKingdoms` 用它发起始资源，`ReadCastleBlock` 用它抽
   城堡默认建筑（`Castle.LoadFromMP2` 的 `difficulty` 参数）。
3. **不传就退回地图头**：`LoadMap( map )` 保持老口径（`map.Info.Difficulty`），测试与工具不必改；`Reset()` 把
   它复位成 `Normal`，空世界不再带着上一张图的答案。
4. **`AdventureMapScene` 开局时传 `Settings.Get().GameDifficulty`**，所以 `--difficulty` 一路贯通到资源与城堡。

**后果**：`--difficulty impossible` 下起始金币为 0、任何城的二级兵营都不会出现；`--difficulty easy` 下 75% 的城
会带二级兵营。地图头难度退为评分口径，与参考一致。
**验证**：`TheDifficultyPassedInDecidesTheStartingResources`（资源）与
`TheDifficultyOfTheGameDecidesTheDefaultBuildingsOfATown`（城堡，八次装载 + 地图头 Easy / 会话 Impossible 对拍）；
全量 5515 测试绿；`--difficulty impossible --enter-map --screenshot` 冒烟通过（地图头 Hard、会话 Impossible 同时存在）。
**没做**：剧本信息屏的难度光标、`NGHSBKG`、难度图标、对手/职业/评分绘制——`--difficulty` 只是这些界面落地之前的过渡口子。

## ADR-204：窗口状态（`videomode` / `fullscreen` / 窗口位置）启动恢复、退出写回 ✅

**背景**：参考的 `DisplayInitializer`（`game_init.cpp:70-99`）在窗口创建后按配置恢复三样东西——全屏状态
（`:89`）、窗口位置（`:96`，`display.setWindowPos( conf.getSavedWindowPos() )`）、分辨率（`:98`，
`display.setResolution( conf.currentResolutionInfo() )`）；`MainMenu` 关闭前把窗口位置记回去（`game_mainmenu.cpp:239-244`），
`conf.Save()` 写文件时也是现场问 `Display::instance()`（`settings.cpp:427-433`）。端口此前只写不读：配置文件里
的 `videomode`、`game window position` 被 `Read` 跳过，`fullscreen` 读了却没人用，启动永远是 2 倍窗口、永远窗口化。

**决定**：

1. **读进设置**：`Settings.CurrentResolutionInfo`（默认 `640x480:640x480`，同 `settings.cpp:95`）与
   `Settings.SavedWindowPosition`（默认 `(-1, -1)`，意思是「没记过」），`Read` 见键才改，与其余键一致。
2. **恢复交给宿主**：`GameHost.CreatePresenter()` 按参考的次序来——全屏（`--fullscreen || Settings.IsFullScreen`）
   随 `SdlPresenter` 构造一起设好，窗口位置作为构造的可选参数在 `SDL.ShowWindow` **之前**应用（否则先闪一下
   默认位置），最后 `SetResolution` 应用 `videomode` 的窗口尺寸。
3. **只应用窗口那一半**：参考的帧缓冲可以换尺寸（`Display::setResolution` 重建渲染目标），端口的场景全部按
   640x480 画；`videomode` 的游戏尺寸不是 640x480 时打一行警告、只应用窗口尺寸，而不是抛异常。等场景与界面
   支持别的逻辑尺寸再放开。
4. **命令行压过文件**：`--scale` 从「默认 2」改成可空——给了就不看文件里的窗口尺寸（基线脚本靠默认 2 倍与
   `--no-config` 固定窗口）；`--fullscreen` 只会打开全屏。文件缺键时仍是 2 倍。
5. **写回的是现状**：`SaveSettings()` 现场问窗口（`videomode = 640x480:当前窗口`、`game window position = 当前坐标`、
   `fullscreen = 当前状态`），不把读进来的值原样写回；`F11` 切换后同步 `Settings.SetFullScreen`，文件里才不会
   永远停在开窗时那一档。`SdlPresenter` 相应加了 `WindowPosition` 读取与构造参数。

**后果**：改过窗口大小的玩家下次启动就在自己习惯的尺寸里；全屏状态、窗口位置都能记住；配置文件里三项与窗口实际
状态一致（此前的写入是「猜」的）。
**验证**：全量 5 517 测试绿（新增两条设置测试）；三组 `--config` 冒烟（1600x1200 生效并写回、800x600 游戏尺寸
只取窗口那一半并警告、`--scale 3` 压过文件的 1600x1200 写成 1920x1440）；菜单与设置像素基线仍 `identical`。
**没做**：分辨率选择窗（首运那次挑分辨率，`Dialog::SelectResolution`）、问候里的分辨率段、隐藏界面四个窗口位置的
生效；`videomode` 的游戏尺寸仍锁 640x480。另：冒险地图基线在本机不过，已用 HEAD 的 worktree 复现，与本条无关
（PROGRESS §7.5）。（2026-11-24 后续：首运的分辨率窗与问候里的那一段见 ADR-205。）

## ADR-205：首运的分辨率选择窗，以及「帧缓冲 + 窗口」的分辨率配对 ✅

**背景**：参考的首运（`game_mainmenu.cpp:265-300`）在问候之后调 `Dialog::SelectResolution()`，窗口列出
`engine().getAvailableResolutions()`（`dialog_resolution.cpp`）。端口的 `ResolutionDialog` 早就转好了（图形/系统选项页
在用），但没人调得动它：`IDisplayControl.GetAvailableResolutions()` 返回的是「游戏尺寸 = 窗口尺寸」的行，而
`SdlPresenter.SetResolution` 只认 640x480 的帧缓冲，挑一行就抛 `ArgumentException`；首运也缺这一步。

**决定**：

1. **统一成「帧缓冲 + 窗口」的配对**：`SdlDisplayControl.GetAvailableResolutions()` 用当前帧缓冲的尺寸当
   `GameSize`、显示模式当 `ScreenSize`。参考的现代版本让游戏尺寸跟着窗口走；端口所有场景按 640x480 画
   （ADR-204），所以只有窗口那一半能变。这样同一个列表既喂首运、也喂图形选项页，`SetResolution` 不再拒收。
2. **首运那一步照参考的顺序与文案**：语言选择 → 问候（欢迎段 + 「请选分辨率」段）→ 分辨率窗口 → 收尾（清旗标、
   保存）。问候里的触摸段仍不写，端口没有触摸输入。
3. **场景级模态，不阻塞**：参考的窗口是阻塞的，端口沿用 ADR-202 给语言窗定的做法——`ResolutionDialog` 挂在
   `MainMenuScene` 上，`Update` 每帧把 `LocalEvent.Current` 喂给 `ProcessEvents`。这样 `ListBox` 的滚动条拖动、
   双击、键盘全不用重写，`--smoke-frames`/`--screenshot` 也照常跑得下去。
4. **预选与回退按参考**：当前窗口所在行预选；当前窗口不是某个显示模式（端口开窗默认是 640x480 放大 2 倍，
   1280x960 往往不是一个模式）时停在第一行，参考同样如此。
5. **开不出来就跳过**：缺 `SCROLL.ICN`/`ADVBORD.ICN`、没有显示层、没有可用行，都只打警告并直接收尾，首运不会卡住。

**后果**：首运真的会让玩家挑窗口尺寸，挑中的写进 `videomode`；图形选项页里挑分辨率不再抛异常。
**验证**：全量 5 519 测试绿（`ResolutionDialogTests` 新增预选/回退两条）；首运冒烟（临时补丁把首运推进到该窗口）
日志 `resolution chooser open, 13 window(s), current 1280x960`、注入 Enter 后 `window resolution 2560x1600` 且
`videomode` 写成 `640x480:2560x1600`、`first time game run` 清掉；菜单/设置/剧本三张像素基线 `identical`。
**没做**：右键解释弹窗在首运里被丢弃（ADR-206 已补）；首运这一步的脚本点击（窗口读 `LocalEvent`，
合成的 `--left-click` 只进场景接口）。

## ADR-206：首运分辨率窗的右键解释，用第二层场景级消息框 ✅

**背景**：`ResolutionDialog` 早就把右键变成 `DialogPopup`（行文本 + 缩放说明），参考在
`Dialog::SelectResolution()` 的阻塞循环里用 `showStandardTextMessage( ..., Dialog::ZERO )` 弹出来
（`dialog_resolution.cpp:124-145`、`:291-296`）。ADR-205 把窗口挂进首运后，`TakePopup()` 被丢掉，右键没有反应。

**决定**：

1. **不改成阻塞的 `MessageBox.Show`**：首运的三个窗口都按 ADR-202/ADR-205 的分帧做法（`Update` 每帧喂输入）。
   中途起一层阻塞循环会让 `--smoke-frames`/`--screenshot` 的帧计数与事件批错位，也让「场景级模态」出现两种实现。
2. **消息框当第二层场景级模态**：`MainMenuScene._resolutionTip` 由 `MessageBox.Create` 拼出（OK 按钮，按钮素材复用
   `ResolutionArtwork.Buttons`）。`SetCursor`/`SetLeftButton`/`Select` 先让给它，`HasModalDialog` 计入它，
   `DrawFrame` 在分辨率窗之后画它。
3. **一帧只关一层**：`_skipResolutionInput` 让 `DriveResolutionPicker` 在「刚打开分辨率窗」或「刚关掉解释框」的那一帧
   不把 `LocalEvent` 喂给列表。否则关闭问候的 Enter 会直接确认分辨率窗，关闭解释框的 Esc 会直接取消它。
4. **文案照参考**：按钮的右键解释同时对齐原文（`Okay` → `Click to apply the selected resolution.`、
   `Cancel` → `Exit this menu without doing anything.`）。

**后果**：首运里右键任意一行（或 OK/Cancel 按钮）弹出与参考同文案的解释框，Enter/Esc/点的 OK 都能关；
列表在关闭的那一帧不会收到同一个按键。
**验证**：见 PROGRESS §6.10（5 521 测试绿、首运冒烟截图、菜单/设置/剧本三张基线 `identical`）。
**没做**：首运那一步的脚本化点击（窗口读 `LocalEvent`，合成的 `--left-click` 只进场景接口）。

## ADR-207：冒险地图基线按「参考行为」更新，并给随机源加 `--seed` ✅

**背景**：`adventure-map.png` 长期与 HEAD 的渲染差 165 198 个像素（PROGRESS §7.5 记为「已知未解，查清前不要 `-Update`」）。
worktree 二分：基线在 `11e6a51` 上 `identical`，`748904e`（冒险场景改为整屏交给 `Interface::AdventureMap::RedrawInterface`、
删掉场景自绘的状态行）之后才对不上。逐项核对后三条差异都是**正确行为**：

| 差异 | 参考 | 端口 |
| --- | --- | --- |
| 视口不居中在「最近的城堡」 | `game_startgame.cpp` 开局 `ResetFocus(FIRSTHERO)` → `interface_base.cpp:76-79` 的 `SetFocus` 总是 `SetCenter(hero)` | `AdventureSession.Start` 成功就 `SetFocus(actingHero, false)`（第二参数是「保留滚动条位置」，不是「不居中」） |
| 雷达只有零星亮点 | 雷达只画已探索区域 | 迷雾生效（`748904e` 修了 `Radar._hide` 默认值与迷雾色） |
| 状态栏是「19 精灵 + 4 兵」而不是日期 | `SetFocus` 里 `statusPanel.SetState(STATUS_ARMY)` | 同左；日期是旧的场景自绘 `StatusPanelPainter` 的产物，那个类已删除 |

**决定**：
1. **更新基线**，不把正确的参考行为当成回归去迁就旧图。
2. **补 `--seed <n>`**：更新后同一脚本连跑两次仍差 27～40 个像素，位置都在状态栏部队那一格——地图载入时英雄
   初始部队数量由 `Rand` 抽（OS 播种）。新增 `Rand.SeedThread(seed)` 与命令行 `--seed`（在
   `CreateScenes()` 之前应用），基线脚本带 `--seed 1`。参考没有这个开关（它的测试不做逐像素断言），这是端口
   像素回归设施的接缝，不改默认行为。
3. **不把随机写进 `Settings`**：种子是「本次运行」的东西，跟 `fheroes2.cfg` 无关。

**后果**：冒险地图基线可复现（连跑三次 `identical`），四张像素基线全部 `identical`；`--map` 的截图/冒烟从此确定。
**验证**：见 PROGRESS §7.5 结案段（5 523 测试绿，`RandTests` 两条）。
**没做**：多线程（AI）下的随机复现——`SeedThread` 只覆盖调用线程，AI 开工后各线程仍各自 OS 播种。

## ADR-208：`--right-button` 也为冒险地图按住右键（提示框基线） ✅

**背景**：重建 `quick-info.png` 时发现它其实是「没有提示框的地图」——与 `adventure-map.png` 逐字节相同。
`--quick-info` 只在 `GameHost.CreateScenes()` 里调一次 `ShowQuickInfoAt`，而
`AdventureMapScene.UpdateQuickInfo` 每帧检查 `LocalEvent.IsMouseRightButtonPressed`，没有按住就 `_quickInfo = null`，
于是框活不过第一帧。参考的 `Dialog::QuickInfo()` 是按住右键期间的阻塞循环，所以「框跟着松手消失」本身是对的，
错的是钩子：脚本从没真的按住过按钮。

**决定**：
1. **不改场景语义**，改钩子：`--right-button` 在 `GameHost.Loop` 里把「按住右键」推进 `LocalEvent`
   （`_le.OnMouseButton(true, Right, cursor)`，每帧在合成光标之后）。菜单与城镇屏各有自己的 `SetRightButton`，
   冒险地图读全局事件状态，所以要补在事件这一层；只在冒险地图场景存在时生效，不影响菜单基线。
2. **基线脚本同时带 `--quick-info` 与 `--right-button`**：前者负责「开一次 + 日志」，后者负责「按住」。
   CI 也补了这一条与 `-AdventureOptions`。
3. 不改 `UpdateQuickInfo`：它是参考语义（松手收框），为截图放宽会掩盖真问题。

**后果**：`quick-info.png` 重新带框（「Ore Mine」，`MineText.GetMineName` 按矿的资源取名，与参考
`showMineInfo` 一致）；四张冒险地图基线连跑两次 `identical`。`--right-button` 从此在冒险地图上也能用于
其他「按住才出现」的提示（状态面板、雷达的右键说明）。
**验证**：`VerifyAdventureMapBaseline.ps1 -QuickInfo` 连跑两次 `identical`；其余九张基线不受影响（全部 `identical`）。
**没做**：右键按住期间的拖拽卷屏（参考 `MouseRightClick` 的拖动分支）还没有端口的截图用例。

## ADR-209：`--left-click-at` 多点脚本点击与首运端到端冒烟 ✅

**背景**：首运是纯点击流程（语言窗 → 问候 → 分辨率窗），但 `--left-click` 固定在第十帧、只认一个 `--cursor`，
一次运行只能点一下，所以 §6.6/§6.9/§6.10 一直记着「首运那一步的脚本点击没做」，之前那次冒烟靠临时补丁直接跳过
前两个窗口。缺口不在场景，在钩子：脚本没法在一轮里连点四个位置。

**决定**：
1. **新增 `--left-click-at <frame:x,y[;frame:x,y…]>`**：一次运行里按帧重放任意多个左键点击，记录成
   `ScriptedClick(int Frame, Point Point)`。帧从第一帧起数（与 `--smoke-frames` 同口径），必须 ≥1 且**严格递增**，
   否则在参数解析阶段抛 `ArgumentException`——乱序的点击没有对应的玩家操作，静默重排只会掩盖脚本写错。
2. **`--left-click` 成为它的单点特例**：构造时若没有 `--left-click-at`，`--left-click` 合成
   `ScriptedClick(LeftClickFrame, --cursor)`；已有的单点用法与基线脚本不受影响。
3. **点击仍在主循环里重放**（事件批之后、场景 `Update` 之前），不在 `CreateScenes` 里——后者会跳过新场景的头几帧，
   而首运的每个窗口都是打开后先画一帧再收输入。
4. **分辨率窗单独走 `LocalEvent`**：首运的分辨率窗（`ResolutionDialog`）的列表滚动、双击、右键解释都读全局事件，
   而菜单/城镇屏读各自的 `SetLeftButton`。所以菜单那一路照旧（`SetCursor` + `SetLeftButton` + `HandleMenuAction`），
   另在分辨率窗开着时把点击推进 `_le.OnMouseButton`。
5. **按下与释放分两帧**：一次物理点击本来就跨两帧，列表与按钮行都在**释放**时触发；按下帧记
   `_pendingClickRelease`，下一帧先发释放再处理本帧点击。同帧按下+释放对列表完全无效（本轮踩过这个坑）。
6. **冒烟脚本 `tools/scripts/SmokeFirstRun.ps1`**：写一个仍带 `first time game run = on` 的临时配置，四击
   `10:236,380`（语言 OKAY）、`20:320,322`（问候 OKAY）、`30:300,114`（分辨率列表第一行）、`40:236,356`
   （分辨率 OKAY），然后按日志五个标记逐步核对；不带 `-KeepRunning` 时把配置恢复成首运状态。坐标取自窗口布局
   （两个窗的内容都是 345x306 居中），布局改动要同步改脚本里的数字。
7. **接进 CI**：`ci.yml` 末尾加一步 `Walk the first run with scripted clicks`，条件同其他数据依赖步骤
   （`vars.FHEROES2_DATA != ''`），跑 `SmokeFirstRun.ps1 -SkipBuild`。

**后果**：首运从语言窗到旗标清除有了不靠补丁的端到端冒烟；任何一步的输入接线断了，脚本会在对应标记上报缺失。
`--left-click-at` 也可以用于其他多步点击流程（设置页、剧本屏），但城镇屏内部仍只有「按下+释放同帧」的出口，
多点连点在那里的语义未验证。

**验证**：构建 0 错误 0 警告；全量测试 5 523 绿；十张像素基线一次性复验全部 `identical`；
`SmokeFirstRun.ps1 -SkipBuild` 输出 `the first run walked from the language picker to the cleared flag, match`；
`--left-click` 单点回归日志仍是 `language 'en' -> 'en'.` + `greeting shown.`；日志里没有 `TEMP-` 残留。

**没做**：问候里的触摸段（端口没有触摸输入）、隐藏界面的四个窗口位置。

## ADR-210：触摸输入、问候里的触摸段与隐藏界面面板的落位 ✅

**背景**：P6 就剩两项。其一，问候少一「两指右键」段（ADR-202 记的差异），根子是端口没有触摸输入：
参考在 `onTouchFingerEvent`（`localevent.cpp:1358-1430`）里自己把手指翻译成左右键，并关掉 SDL 的触摸合成，
所以那一段讲的手势真的能用。其二，隐藏界面模式下四个面板（雷达/按钮/图标/状态栏）从配置里读出的位置
（§6.8 读取、`AdventureMap.Reset` 应用）没人写过：拖面板时只画轮廓，松手丢掉，`pos_*` 永远是默认 `-1`，
读取那一半等于死代码。

**决定**：
1. **触摸状态进 `LocalEvent`**：新增 `TouchFinger`（Down/Up/Motion）、`TouchEmulation` 与两根手指的 ID 状态，
   `OnTouchFinger` 逐行照参考：第一指驱动光标并仿真左键；第一指还在时第二指按下仿真右键，抬起第一指**不**释放
   右键（这就是「按住物件，再看别处」）；第三指、以及不在屏上的手指事件一律忽略；每次仿真都置 `MOUSE_TOUCH`，
   `IsMouseEventFromTouchpad` 从此能真为 `true`。
2. **返回仿真结果**：参考的按钮直接读 `LocalEvent`，方法不必返回；端口的场景由宿主送输入，所以 `OnTouchFinger`
   返回 `None/Motion/Left|Right Pressed/Released`，宿主据此调 `MoveSceneCursor` 或 `DispatchButton`，
   场景看到的东西与鼠标事件相同。
3. **关掉 SDL 的合成**：启动 `SDL_Init` 之后设 `MouseTouchEvents = 0`、`TouchMouseEvents = 0`，否则一次点按会以
   「合成鼠标 + 手指事件」两条路各来一遍。手指坐标按窗口归一化，先乘 `OutputSize` 再走 `ToCursor`。
4. **检测**：`SDL_GetTouchDevices`（绑定返回托管数组，无需释放），结果交给 `MainMenuScene.TouchInputAvailable`。
   SDL 只在设备被用过之后才报告，所以首运仍可能看不到触摸屏——参考同样如此，段落只是少写一段。
5. **问候段**：`WelcomeTouchId` 照抄参考文案，按 `欢迎 → 触摸 → 分辨率` 的顺序拼；分辨率段照写（桌面版永远不是
   手持设备）。
6. **`--no-touch`**：本机有设备上报时，问候会因为多一段而变高、OKAY 下移，`SmokeFirstRun.ps1` 的固定坐标就会点空
   （本轮实际踩到，冒烟停在语言窗之后）。这个开关只关「检测 + 段落」，不关手指翻译，给脚本一个稳定的布局；
   首运冒烟已带上它。
7. **面板落位**：`BorderDrag` 带上被拖的 `Owner`，并新增 `Drop(cursor)`（参考松手时的
   `SetPos( mp.x - ox, mp.y - oy )`）；`AdventureMapScene.PumpDrag` 在释放帧调它。面板的 `SetPos` 本就会夹进屏幕
   并 `SavePosition()` 进 `Settings`，退出时随配置写出，于是「读位置 → 拖 → 落盘 → 下次启动复位」闭环。

**后果**：触摸屏上单指即鼠标、两指即右键，问候里的手势说明是真的；隐藏界面面板拖完会停在新位置并记住。
`Drop` 对没有 `Owner` 的轮廓（只画轮廓的测试）什么都不做。

**验证**：构建 0 错误 0 警告；全量测试 5 529 通过（`LocalEventTests` 新增 4 条触摸语义，`BorderWindowTests`
新增 2 条落位/无主轮廓）；十张像素基线一次性复验全部 `identical`；`SmokeFirstRun.ps1 -SkipBuild` 通过；
本机（有触摸设备上报）不带 `--no-touch` 截的问候图是三段（欢迎、触摸、分辨率），带 `--no-touch` 时回到两段。

**没做**：真机两指手势未实际点过（本机只报告了设备，没有手指输入）；捏合/滑动等指针手势（参考也只在未移植的
battle 里读 `IsMouseEventFromTouchpad`）；手持设备的问候/分辨率差异。

## ADR-211：非整数倍的窗口按最大等比缩放，不再只画 1 倍

**背景**：§6.8 让窗口尺寸从 `videomode` 恢复、退出时写回，于是配置里可能是任意尺寸——用户拖过的、
或者首运分辨率窗里选中的屏幕模式（本机配置就是 `640x480:1280x800`）。同一轮引入的
`PresentationViewport` 只做整数倍缩放：1280x800 的窗口除以 640x480 得 2 和 1.67，只能取 1 倍，于是
640x480 的画面缩在窗口中间，四周全是黑边（日志 `x1.00 scale`），下次启动就这样。参考没有这个问题，它
的游戏帧缓冲跟着窗口走（`Display::setResolution` 改 `Image::resize`，任意窗口尺寸都铺满），端口固定
640x480（ADR-204、ADR-205），所以必须自己决定怎么把 4:3 的画面放进别的窗口。

**决定**：
1. `PresentationViewport.Compute` 一律取「能放下的最大等比缩放」：窗口恰好是帧缓冲整数倍时结果就是整数
   （1280x960 → 2.0，像素完美、基线不变）；不是整数倍时取小数（1280x800 → 1.6667，画面 1067x800）。
2. 画面始终等比、居中，剩下的地方是黑边。**不**拉伸铺满：参考的 `SDL_RenderCopy( ..., nullptr )` 确实铺满，
   但那是把 640x480 的 4:3 画面拉成 16:10，界面纵向压扁 17%；端口的帧缓冲固定，宁可留边也不变形。
3. 目标矩形按整像素取整，所以 `TryToLogical` 在远边可能算出 640 这种越界值，改为 clamp 到最后一列/行，
   点击不会因为取整落到画面外。
4. `_logicalTarget` 的缩放过滤显式设为 linear：整数倍时是一一映射，非整数倍时比最近邻少一些粗细不均。

**后果**：配置里的窗口尺寸再离谱也不会缩成 1 倍；4:3 内容在 16:10 窗口里必然有左右黑边，这是等比换来的。
想让画面完全铺满，只能走参考的完整做法——帧缓冲随窗口尺寸变、场景按任意尺寸重排，那是 P7 之后的事。

**验证**：本机配置 `640x480:1280x800` 的日志从 `x1.00 scale` 变成 `x1.67`；全量测试 5 535 通过
（`GeometryTests` 改 4 条并新增 3 条：非整数倍窗口取最大等比、整数倍窗口铺满、非整数倍点击往返不超过
1 像素、取整边沿被 clamp）；十张像素基线全部 `identical`；`SmokeFirstRun.ps1 -SkipBuild` 通过（脚本点击
用的是 640x480 的逻辑坐标，不受缩放影响）。

## ADR-212：战斗界面从 UI 层起步——静态战场先落地，引擎的回合留在 Game 层

**背景**：`PLAN.md` §5.6 把 `battle/battle_interface.cpp`（6 144 行）等四篇列为 P3，端口此前一个文件都没有。
战斗规则层（`BattleArena`、`BattleLoader`、`BattleUnit`、AI）已经齐了，`IBattlePresenter` 也留好了
「人类那一手怎么打」的回调，缺的正是「看得见」。摆位置时有个分层问题：参考的界面层直接摸 `Arena` 单例与
`Settings` 全局，端口不允许（`Game` 不能引 `UI`）。绘制本身还需要 `LocalEvent`、`Dialog`、`ImageOps`，
这些都只在上层。

**决定**：
1. 战斗的绘制落在 `src/FHeroes2.UI/Battle/`（`BattleFieldPainter`、`BattleFieldStyle`），与冒险地图的
   `UI/Interface/` 同一层；引擎与规则一行都不动。参考的「界面给竞技场摆格子」这一步照样保留
   （`board.SetArea(...)` 在绘制器的构造函数里）。
2. 第一步只做静态层：底图、边框杂物、地表装饰、地图障碍、城堡地面/护城河/城墙/塔/攻城车、双方单位的
   待机首帧，以及状态栏与三个按钮的**底图**。动画、按帧推进的走子、顺序条、状态栏文字、法术特效、
   坟场与六角网格都不在这一步。
3. 「谁去打」由命令行挑：`--battle` 取「地图里第一只游荡怪」，`--battle-siege` 取「第一座别的颜色的城」。
   参考是英雄走到怪物格/城门格触发，端口的人类动作链还没有界面宿主（PROGRESS §7.4），所以先给画面一个
   确定的入口；两者都直接跑 AI，等价于参考的无界面自动战斗。
4. 场景 `BattleScene` 一帧推进一个整回合（`arena.Turns()`），`--freeze-animation` 时一帧都不推进：
   基线要的是部署那一刻，冒烟要的是「能打完整场」。这两种口径都记在 `tests/baselines/README.md`。
5. **战场城堡图是 `CASTLE*`，不是 `CSTL*`**：`ICN::getCastleIcnId()`（`icn.cpp:928-948`）给的是
   `CASTLEB/K/N/S/W/Z`，`CSTL*` 属于城镇建筑的 `ICN::getBuildingIcnId()`；两套图帧数不同
   （`CASTLEB` 有 31 帧、`CSTLBARB` 也有 31 帧但偏移全为 0），拿错的结果是所有墙都画在屏幕左上角。
6. 势力计数条要 `TEXTBAR` 的指示帧与位图字体，随第二步（单位与动画）一起补；坟场（`RedrawKilled`）
   同理，开战时本来没有尸体。

**后果**：战斗第一次有画面，野战场地与攻城两套静态层都能截图；回合仍然是自动的，人类的点选与动画缺席。
`--battle` 直接跑在英雄的真实军队上（AI 打完会真的损兵），它是画面冒烟入口而不是玩法入口，将来接上
`BattleLoader` 与人类动作链后这一段自然收归那里。

**验证**：构建 0 错误 0 警告；`BattleFieldStyleTests` 25 条全绿（九种地形的背景与边框、草/土/雪的有树与
无树两版、六族的城堡地面、六族的战场城堡图、未知地形与未知种族的空答案、攻城时沿用地形底图）；
`--battle --freeze-animation` 截图 `tests/baselines/battle.png`（兽人 27、沙漠地形），
`--battle-siege` 截图 `tests/baselines/battle-siege.png`（野蛮人城、护城河与城墙），两张基线各连跑两次
`identical`；`--battle --smoke-frames 40` 能把整场打完并回到地图（日志 `The battle is over ...`）。

## ADR-213：单位的动画状态留在绘制器，`BattleUnit` 不拿资源；计数条的数字由场景注入字体

**背景**：参考的 `Battle::Unit` 自带 `AnimationState animation` 与 `RandomizedDelay _idleTimer`
（`battle_troop.h:349`、`:383`），构造函数从 `Bin_Info` 全局缓存里取怪物的 `.BIN` 记录。端口没有全局缓存：
`AnimationState` 需要 `MonsterAnimInfoCache`，后者需要 `AssetBundle`，而 `Game` 层不碰资源定位，
`BattleUnit` 又是由 `BattleForce` 从 `Army` 直接摆出来的（`battle_army.cpp:80-105` 的那套公式），中间没有
资源可用。计数条上的数字要位图字体，字体同样只在上层（`GameFonts` 在 `App`）。

**决定**：
1. 动画状态放在 `BattleFieldPainter`：一个 `Dictionary<uid, UnitAnimation>`，`UnitAnimation` 持有
   `AnimationState` 与 `RandomizedDelay(state.IdleDelay)`。推进由 `BattleFieldPainter.Update()` 负责，
   节拍照参考问 `GameDelays.ValidateAnimationDelay(DelayType.BattleIdleDelay)`。
2. 状态机本身逐字照抄 `Force::animateIdleUnits()`；将来行动/受击动画需要切换状态时，绘制器按引擎给出的
   当前动作切序列，`BattleUnit` 仍然只回答「我是什么、站在哪、还剩多少」。
3. 计数条的数字用可空的 `IGlyphSource` 注入（`BattleScene` 传 `GameFonts.SmallWhite`）；字体缺失时只画条子
   不画数字，而不是崩掉。
4. **血量条不做**：它要按游戏调色板取绿/红两个索引（`fheroes2::GetColorId`），而绘制器没有调色板；
   该设置默认关闭，等战斗设置窗（`battle_interface_settings.cpp`）那一步把调色板与开关一起接上。
5. `(monsterIndex / width) == (tileInFront == width)` 这条参考表达式原样保留：它看起来是笔误（比较的是
   「行号」与「前格是否等于 11」），但它决定相邻单位的计数条会不会叠过来，改动会直接改像素。

**后果**：`BattleUnit` 依旧没有资源依赖，单测里照常能造；代价是「谁在播什么动画」暂时只存在于界面这一侧，
将来要把战斗动画接进行动循环时，得让绘制器接受引擎的动作通知（`SwitchAnimation(state)` 的入口已经在那里）。

**验证**：构建 0 错误 0 警告；两张战斗基线重建（多了计数条）后各连跑两次 `identical`；待机状态机用临时日志
验证（把 `_idleTimer` 临时改成 50 毫秒并绕过节拍门，120 帧内待机单位 0 → 5），补丁已撤；
全量测试 5 560 绿。

## ADR-214：`BattleArena` 拆出逐单位推进的五个入口，`Turns()` 变成它们的循环封装

**背景**：P3 的行动交互卡在一个结构问题上。参考的 `Arena::Turns()` 里，人类的 `Interface::HumanTurn()` 是
**阻塞**的（自己跑 `LocalEvent` 循环），所以整轮可以在一个函数里走完。端口的主循环是帧驱动的：场景每帧
`Update` 一次、`DrawFrame` 一次，任何一处阻塞都会让窗口停摆。要接人类点选，就必须能在「轮到人类」处停下。

**决定**：
1. 把 `Turns()` 拆成 `BeginRound()`（开轮）、`SelectNextUnit()`（挑出手单位）、`PlayCastleActions()`
   （投石车与三座塔，每轮各一次）、`PlayCurrentUnitTurn(handler)`（AI 那一手）、`PlayCurrentUnitTurn(commands)`
   （玩家在别的帧里点好的那一手）与 `EndRound()`（结算）。
2. `Turns()` 保留原签名与原语义，内部改成这五个调用的循环；`BattleLoader.RunToEnd`、AI 回合测试都不用改。
3. `PlayCurrentUnitTurn(commands)` 走同一条 `UnitTurn()`：好士气带来的额外行动如果玩家没再给命令，就交给
   规划器代打——这条偏差记在方法注释里，等界面真的能连点两下再收。
4. 结算必须显式：`BattleScene` 在离开前调 `EndRound()`。忘了这一步的直接症状是战果全空（日志「nobody won」），
   本次冒烟踩到了，已修。
5. 场景改成**一帧推进一个单位**（以前是一帧一整轮）。这既让 AI 的每一手有独立的绘制帧，也为「轮到人类时
   停住」留出了位置。

**后果**：战斗的画面推进变慢（一场野战约几十帧），冒烟要相应放宽帧数（`--smoke-frames 400`）；
`Turns()` 的行为由新增的等价测试钉住（同一颗种子，两条路得到同样的战果、经验与总兵力）。

**验证**：构建 0 错误 0 警告；`BattleArenaTurnTests` 新增 2 条；全量 5 562 绿；两张战斗基线、冒险地图与
编辑器基线复验 `identical`；`--battle --smoke-frames 400` 与 `--battle-siege --smoke-frames 600` 都打完整场
并回到地图，日志 `attacker 'Loss', defender 'Wins'`。

## ADR-215：战斗的人类输入读 `LocalEvent`、命令由 `BattleArena` 给出，自动驾驶另开 `--battle-auto`

**背景**：参考的人类回合是阻塞的 `Interface::HumanBattleTurn()`：它自己跑事件循环、按光标主题拼
`BattleCommand`、再交给 `Arena`。端口的屏幕是帧驱动的，而且游戏层不该认识像素或光标。§6.16 已经把
「执行一手命令」做成 `PlayCurrentUnitTurn(commands)`，这一批要定的是**谁把点击变成命令**。

**决定**：
1. 场景每帧读 `LocalEvent.Current`：`CursorPosition` 交给 `BattleBoard.GetIndexAbsPosition()` 得到格子，
   `MouseClickLeft(battlefieldArea)` 判定一次点击。**不新增** `GameHost` 的鼠标路由分支——冒险地图也是这么
   读事件的，两屏一致。
2. 「点哪一格 → 下什么命令」放进 `Game` 层的新文件 `BattleArena.Commands.cs`（`CreateMoveCommand` /
   `CreateAttackCommand`），因为它要用寻路器与 `BattleTactics`。攻击命令：贴身直接出刀（方向让竞技场算），
   否则绕目标一圈找「这一回合最便宜能站住」的邻格；**不用** AI 的 `GetUnitMovementTarget()`——它对
   「目标格本身站不下」是断言，而点击只能回答「没有命令」。
3. 光标主题暂时只分三类（走 / 飞 / 打），每次悬停格变化才重算：可达性查询带寻路，不能每帧跑。
   八向剑与施法光标留给施法那一步。
4. **交互与冒烟分开**：`--battle` 现在是真的人类可玩（人类那一栈停下来等点击），无人值守的冒烟与脚本
   必须加 `--battle-auto`（等价于参考在没有界面时的 `EnableAutoCombatForHumanSides()`）。像素基线不受影响：
   它们带 `--freeze-animation`，一帧都不推进。
5. 网格与悬停阴影要游戏调色板里的两个索引，绘制器因此收一个可空的 `Palette`（场景传
   `ScreenBuffer.Palette`，即 `Palette.FromGamePalette(KB.PAL)`），颜色走 `PALColorId.Get`。

**后果**：战斗屏幕第一次可以只用鼠标打完一局（走、打、跳过）。代价是输入路径多了一条：
`LocalEvent` → 场景 → `BattleArena.Commands` → `PlayCurrentUnitTurn`，三处都要有测试或冒烟证据
（命令侧有 4 条单测，端到端靠 `--left-click-at` 的日志）。`--battle` 的语义变了，文档与脚本都要带
`--battle-auto` 才能无人值守。

**验证**：构建 0 错误 0 警告；`BattleArenaCommandTests` 4 条；全量 5 566 绿；七张基线全部 `identical`
（两张战斗重建后各连跑两次）；脚本点击日志 `The player's Sprite acts: move to 24.`；无点击时 60 帧不结束。

## ADR-216：状态栏文字走 `normalWhite` 字体注入，截断规则落进 `GlyphText`；命令文字必须在执行前取好

**背景**：参考的状态栏是 `Battle::Status` 两行文字（`battle_interface.cpp:1138-1185`），每行用
`fheroes2::Text::fitToOneRow()` 截断（`ui_text.cpp:495-522`），点击状态栏还能展开 6 行战斗日志
（`StatusListBox`）。端口此前只画了状态栏的底图（`TEXTBAR` 8/9 与三个按钮），既没有文字也没有日志。

**决定**：
1. **截断规则放进 Assets 层**：`GlyphText.FitToOneRow(source, text, maxWidth)` 逐条照参考（放得下原样返回、
   否则切到「maxWidth − 省略号」并补 `"..."`，代理对不拆、行分隔符不计宽）。战斗状态行的两处与以后所有
   「一行放不下就省略」的窗口都能复用它，而不是在 `BattleFieldPainter` 里写一份私有逻辑。
2. **字体由场景注入**：状态两行用 `FontType::normalWhite()`，端口即 `GameFonts.White`；与计数条的
   `SmallWhite` 分开传（ADR-213 的同类做法）。字体缺失时只画底图、不画字。
3. **上一行是动作、下一行是提示**：`SetStatus(message, top)` 与参考同构——上行写入并压进日志、同时清掉下行；
   下行内容不变时不重算（`_lastStatusLower`）。提示文案先用参考的英文串（`Move` / `Flying` / `Attack`），
   本地化随 `.mo` 一起做。
4. **日志只有缓冲，没有窗口**（窗口已由 ADR-217 补上）：`AddLogMessage` 按参考的「最后一个放得下的空格」断行、长词用
   `FitToOneRow` 截断，存进 `LogMessages`；6 行窗口、滚动条与「点状态栏展开」当时还没做。
5. **命令文字必须在执行前取好**：`BattleArena` 应用命令时会消耗命令的参数（参考的 `Command` 继承
   `std::vector` 并从尾部弹出），所以一旦 `PlayCurrentUnitTurn(commands)` 跑过，`commands[0]` 的
   `[0]` 就可能越界。冒烟里确实踩到过一次 `ArgumentOutOfRangeException`；现在先把文字算成局部变量再执行。

**后果**：状态栏第一次有内容；顺带得到一个可复用的单行截断工具与 6 条测试。代价是战斗日志窗口仍是
「有数据没界面」，且状态文案暂为英文硬编码。

**验证**：构建 0 错误 0 警告；`GlyphTextFitTests` 6 条；全量 5 572 绿（Assets 428 → 434）；四张相关基线
`identical`；截图里上行 `Move to 24`、下行 `Flying`；临时日志确认日志缓冲收进同一句（补丁已撤）。

## ADR-217：战斗日志窗口复用 `ListBox<string>`，断行随窗口走；点状态栏展开，窗口先吃点击

**背景**：ADR-216 只做了日志缓冲。参考的 `Battle::StatusListBox`（`battle_interface.cpp:803-980`）是
`Interface::ListBox<std::string>` 的子类：6 行 × 17 像素、`TEXTBAK2` 拉伸成框、右侧 `DROPLISL` 的滚动条
（6/7 上行、8/9 下行、10/11/12 装饰、13 滑块），点击状态栏 `SetOpenLog()` 切换。

**决定**：
1. **`BattleStatusLog : ListBox<string>`** 放在 `FHeroes2.UI/Battle/`，几何逐项对齐参考：
   `totalElementHeight = 6*17 - 4 = 98`，框再加 16 像素边，所以 `SetPosition(px, py)` 把窗口底边压到 `py`
   （状态栏上沿）；行区高 `98 + 4 = 102`，最后一行故意压住边框。
2. **断行留在窗口里**：`AddMessage()` 照搬参考「只切一次」的算法——找最长的、放得下的、以空格结尾的前缀进
   第一行，剩下的整段做第二行；单词本身超宽时用 `FitToOneRow` 截断。`BattleFieldPainter` 里原来那份
   「一直切到放得下」的实现删掉：它多切了几行，和参考的六行窗口对不上。
3. **滑块按需重做**：`ScrollSlider.Create` 用 `DROPLISL` 13 号、上下限 `(0,0,w,4)`/`(0,4,w,8)`；日志合上时
   `SetScrollBarImage(null)`，打开时按当前行数重做（参考是 `_scrollbar.hide()` + `SetOpenLog()`）。
4. **输入先给日志**：`BattleScene.UpdateBattleLog()` 用 `BattleFieldPainter.StatusBarArea` 接左键切换；展开时
   先跑 `QueueEventProcessing()`（行与滚动条），再补一发 `MouseClickLeft(Frame)` 兜住外框与滚动条列。
   `LocalEvent` 的区域点击查询会吃掉这次 release，战场因此收不到——不会「点一下日志，单位走出去」。
5. **状态栏提示**：指针停在状态栏上时，下行给「Show logs」/「Hide logs」，对应参考在同一分支里设的
   `msg`（`battle_interface.cpp:3511`）；离开状态栏且不在战场上时清空下行，避免提示黏住。

**后果**：日志能开、能滚、能读；`--battle` 下点状态栏即可看到。代价是行文本仍是英文（本地化随 `.mo`），
窗口位置固定在状态栏正上方（参考也是硬编码）。

**验证**：构建 0 错误 0 警告；`BattleStatusLogTests` 12 条（几何、`DROPLISL` 滚动条、断行、长词截断、
最新行可见、有/无资源两种重绘）；全量 5 584 绿（UI 1 541 → 1 553）；`battle.png`、`battle-siege.png`、
`adventure-map.png` 三张基线 `identical`；截图 `tools-output/battle-log.png` 是点击状态栏后的展开态，
下行同时显示「Hide logs」。


## ADR-218：状态栏三个按钮单独成 `BattleStatusButtons`，Auto 落到 `TOGGLE_AUTO_COMBAT`；顺带修掉脚本点击漏进主菜单

**背景**：状态栏的三块底图（`TEXTBAR` 4/6/0）早就在画，但它们是硬编码的两个坐标画上去的：没有点击区、
没有悬停提示、没有按下态，`AUTO`/`SETTINGS`/`SKIP` 点了没反应。参考把它们做成 `Button`
（`battle_interface.cpp:1467-1490`），每帧按 `isMouseLeftButtonPressedAndHeldInArea()` 决定画
4/5、6/7、0/1 哪一帧，点击分支在 `MouseCursorArea…`（`:3317-3361`）。

**决定**：
1. **几何单独一层**：新增 `FHeroes2.UI/Battle/BattleStatusButtons.cs`，按参考的三个 `setPosition` 算框——
   设置贴左下角（`480 - 高`）、Auto 压在设置上面、Skip 贴右下角（`640 - 宽`），并且**把精灵自己的
   `OffsetX/OffsetY` 算进框里**：参考的 `Button::area()` 就是这么来的，`TEXTBAR` 0 号实测是 49×37，
   与旧代码里写死的 49×18 不同。缺资源时退回同一组常量，无数据的构建也能点。
2. **状态与绘制分开**：`BattleFieldPainter.PressedButton` 只回答「画哪一帧」，按键判断留在场景里
   （参考也是每帧先 `drawOnState()`，再在鼠标分支里处理点击）。
3. **Auto 与 Quick 落到引擎已有的命令**：这一层不做策略，场景发
   `BattleCommand.ToggleAutoCombat(color)`（`ApplyActionToggleAutoCombat` 已经在了），`Q` 键发
   `BattleCommand.QuickCombat()`。参考的两档选择窗（`OpenAutoModeDialog`）留给 `ukw.25`，设置窗
   （`_openBattleSettingsDialog`）留给 `ukw.24`——本步只把设置按钮的入口接上并记一条日志。
4. **宿主修正**：`--left-click-at` 的脚本点击原来无条件喂给主菜单（`GameHost` 的 replay 分支只挡了
   城镇屏），于是「在战斗里点 Auto」会顺手把客户端点成 `Quit`。补上 `_battle is null` 判断后，战斗期间的
   脚本点击只进 `LocalEvent`。

**后果**：三个按钮能悬停、能按下、能点；`SKIP` 直接跳过、`AUTO` 立刻托管（再点收回）、`Q` 快速战斗、
设置按钮先记日志。空格跳过改为走 `HotKeyEvent::BattleSkip`（默认仍是空格），与 `SKIP` 按钮共用同一条命令路径
`PlayCommands()`。

**验证**：构建 0 错误 0 警告；`BattleStatusButtonsTests` 6 条（三块几何、命中测试、按下帧、缺图退化）；
全量 5 590 绿（UI 1 553 → 1 559）；`battle.png`、`battle-siege.png`、`adventure-map.png` 均 `identical`；
冒烟：`--left-click-at 6:600,460;20:…` 打出三次 `acts: Skip.`，`8:10,450` 打出
`Auto combat for Blue is now on.` 并跑到战斗结束，`6:10,470` 打出设置窗未移植的日志；截图
`tools-output/battle-buttons.png` 是悬停 `SKIP` 时的 `Skip this unit` 提示。


## ADR-219：战斗设置窗复用 `StandardWindow` + `OptionItem`，七档开关就地写回 `Settings`

**背景**：`SETTINGS` 按钮在 ADR-218 里只接了入口。参考的窗口在 `battle_interface_settings.cpp`：
`StandardWindow( 289, optionsStepY * 3 + 52 )` 里摆两行三列加第三行中间一个图标——回合顺序 / 网格 /
伤害数字、移动阴影 / 鼠标阴影 / 移动范围、血量条；每个图标是 `CSPANEL` 的一对帧（移动范围与血量条是
合成图），点一下就地切一个 `Settings` 标志，右击给说明，OK/Esc 关闭。

**决定**：
1. **窗口自己管**：`BattleSettingsWindow` 建在 UI 层，`TryCreate` 拿不到 `CSPANEL` 与 OK 按钮就返回
   `null`；几何用 `OptionItem` 的常量（`threeOptionsOffsetX = 20`、`stepX = 92`、`optionsOffsetY = 31`、
   `stepY = 110`、图标 65），三列两行加中间一格全部对得上参考。
2. **输入逐帧**：`ProcessEvents(LocalEvent)` 读七个框与 OK，Esc 走 `HotKeyEvent::DefaultCancel`；
   场景在 `_settings is not null` 时把整帧交给它、战场不再推进——参考那边是个阻塞对话框。
3. **开关直接写 `Settings`**：网格与血量条本来就是 `Settings` 的位，画战场的 `BattleFieldPainter` 每帧读
   `IsBattleShowGrid`，所以点完立刻生效；`Changed` 只留给调用方知道「有改动」。
4. **数据缺口两处**：这套数据里没有 `ICN::BUTTON_SMALL_OKAY_GOOD`，OK 按钮与系统选项窗一样取
   `SYSTEM.ICN` 的 1/2 号（`SettingsArtwork` 的同一做法）；`EMPTY_OPTION_ICON_BACKGROUND` 也没有，
   两个合成图标退回一块空底板（黑底上的生物图），布局与文字不变。

**后果**：`SETTINGS` 能开、七个开关能点、网格之类立刻反映到战场上；右击说明框与血量条图标里示意用的
血量条（依赖 `ukw.21` 的 `drawHitPointsBar`）还没做。

**验证**：构建 0 错误 0 警告；`BattleSettingsWindowTests` 3 条（窗口尺寸、第一行三格、血量条居中）；
全量 5 593 绿（UI 1 559 → 1 562）；`battle.png`、`battle-siege.png`、`adventure-map.png` 均 `identical`；
截图 `tools-output/battle-settings.png` 是窗口本身，`battle-settings-off.png` 是「点 Grid 关掉 + OKAY 关闭」
之后的战场（网格消失、提示回到 `Flying`）。


## ADR-220：战斗施法——魔法书复用 `SpellBookWindow`，瞄准光标与目标高亮各自成一层

**背景**：战斗里按 `C`（`HotKeyEvent::BattleCastSpell`）到 `Interface::HumanBattleTurn` 的 `cast` 分支
（`battle_interface.cpp:7295-7345`）打开魔法书，选完法术进入 `humanturn_spell` 瞄准态；瞄准时光标换成
`SPELCO` 的对应帧、下行写提示（`GetBattleSpellCursor()`，`:3066-3117`），阴影圈出法术覆盖的格子
（`RedrawCover()` 的施法分支，`:2288-2474`），左键落子、右键/Esc 取消（`HumanCastSpellTurn()`，`:3531-3630`）。
端口里 `SpellBookWindow`、`Spell`、`BattleCommand.SpellCast`、`IsDisableCastSpell`、`AllowApplySpell`、
墓地复活判定都已经在了，缺的只是界面这一层。

**决定**：
1. **魔法书直接复用**：`SpellBookWindow.TryOpen( owner, book, artwork, fonts, CMBT, canCastSpell: true,
   restorePreviousState: true, 640x480 )`。战斗场景自己拿着一个 `SpellBook` 页状态（参考把页与过滤器存在
   英雄身上，端口存在场景里，战斗一结束就丢）；`BOOK`/`SPELLS` 的取图收进
   `SpellBookArtwork.TryCreate( assets )`，冒险地图与战斗共用同一套帧索引。
2. **书是模态但有帧循环**：`_spellBook` 非空时 `Update` 把整帧交给 `ProcessEvents`，战场不推进、光标固定箭头；
   `Draw` 在战场之上、设置窗之下。书里的 `statusCallback` 写到战斗下行状态栏。
3. **光标与提示做成纯函数**：`FHeroes2.UI/Battle/BattleSpellCursor.cs` 搬 `getCursorForSpell()` 与
   `GetBattleSpellCursor()`，只收竞技场、当前单位、法术、鼠标格与传送起点，不碰图片，直接对着竞技场测试。
   `ThemeFor` 不补参考表里没有的群体法术（它们本来就不进瞄准态）。
4. **高亮只算格子**：`BattleSpellHighlight.GetCells()` 搬参考的 `std::set<const Cell*>` 攒格子那一段，
   `BattleFieldPainter.RedrawHoverShadow()` 负责「格子能过、格子上的兵吃这个法术」的过滤与贴图；画的时候
   `PendingSpell`/`TeleportSourceCell`/`CursorTheme` 三个属性由场景每帧写入。
5. **落子规则照参考**：只有光标不是 `CombatNone` 的点击才落子；传送要点两次（第一击选起点、不消耗回合）；
   不需要目标的法术（群体、地震、招元素…）在选完的那一刻直接发 `SpellCast(spell, -1)`。
   书里选中后还要过 `IsDisableCastSpell` 与 `CanCastSpell` 两道门，拒绝时文案取 `SpellBookWindow.DescribeCantCast`。

**后果**：有魔法书的英雄在战斗里能开书、选法术、看光标和范围、落下命令；交回电脑（`AUTO`）会取消瞄准。
两处与参考不同、都已记在代码注释里：竞技场的 `IsDisableCastSpell` 只回布尔，带原因的提示统一写成
「You are not able to cast spells right now.」；参考用文本对话框弹「没有魔法书」「没有可施放的法术」，
端口写到上位状态行与日志（战斗场景还没有消息框）。

**验证**：构建 0 错误 0 警告；`BattleSpellCursorTests` 16 条（`SPELCO` 映射、友军/空地/传送起点与终点、
无家可归的群体法术）、`BattleSpellHighlightTests` 9 条（单体、火球/流星雨 7 格、冷环不含中心、火焰爆裂
13 格、传送落点、界外）；全量 5 619 绿（UI 1 562 → 1 588）；`battle.png`、`battle-siege.png` 两张基线
`identical`（施法分支只在瞄准态出现，静止帧不变）。


## ADR-221：等待与防御在参考里不存在，补的是跳过的那句状态文案

**背景**：`bd` 里的 `ukw.13` 怀疑战斗界面还有「等待」「防御」两个动作没接。核对参考后答案是否定的：

1. `Battle::CommandType`（`battle_command.h:38-51`）只有 `MOVE / ATTACK / SPELLCAST / MORALE / CATAPULT /
   TOWER / RETREAT / SURRENDER / SKIP / TOGGLE_AUTO_COMBAT / QUICK_COMBAT`，没有 `WAIT`、没有 `DEFEND`。
2. 热键表（`game_hotkeys.h:160-165`、`game_hotkeys.cpp:298-304`）只有 `BATTLE_SKIP`（空格）、
   `BATTLE_CAST_SPELL`（C）等，没有等待/防御的键位——端口的 `HotKeyEvent` 逐项一致。
3. 界面只有一个 `_buttonSkip`（`battle_interface.h:509-511`，与 `_buttonAuto`、`_buttonSettings` 并列），
   没有等待/防御按钮；本地化表里也没有对应的模板，只有 `"The %{name} skips their turn."`。
4. `ApplyActionSkip()`（`battle_action.cpp:668-705`）打 `TR_SKIP | TR_MOVED`，就是「本回合不再动」，
   端口的 `ApplyActionSkip` 已经逐行对齐。

原版《英雄无敌 II》本来也没有等待（那是三代的东西），站在原地的单位就是靠不移动来保防御。

**决定**：
1. **不新增命令**：等待/防御不建模，`BattleCommandType` 与竞技场保持一致；`ukw.13` 以「参考无此动作」结案。
2. **补上跳过的那句文案**：参考的 `Interface::RedrawActionSkipStatus()`（`battle_interface.cpp:4005-4011`）
   把 `_n( "The %{name} skips their turn.", "The %{name} skip their turn.", count )` 写进上位状态行；端口原来
   只写短标签 `Skip`。新增 `FHeroes2.UI/Battle/BattleActionText.Skip(unit)`：按数量挑单/复句模板，
   名字走 `MonsterNames.GetPluralName()`，再交给 `Army.StringReplaceWithLowercase()` 改成句中小写
   （「The peasants skip their turn.」），与参考的输出逐字一致。
3. **只改跳过的显示**：`BattleScene.PlayCommands` 仍用 `StatusOf()` 记日志（`acts: Skip.` 保持不变，
   冒烟脚本还在用），只有状态行换成整句。

**后果**：无人控制的「等待/防御」不再被当成欠账；跳过的提示从 `Skip` 变成参考的句子。电脑那一侧跳过时
界面仍然没有反馈——端口的 AI 回合不经过界面（见 `ukw.19` 的动作动画），不是这一条的事。

**验证**：构建 0 错误 0 警告；`BattleActionTextTests` 2 条（十人队 → 「The peasants skip their turn.」、
单人 → 「The peasant skips their turn.」）；全量 5 621 绿（UI 1 588 → 1 590）；`battle.png`、
`battle-siege.png` 两张基线 `identical`；冒烟 `--battle --left-click-at 6:600,460` 点击 `SKIP` 后截图
`tools-output/battle-skip.png`，状态栏显示「The sprites skip their turn.」。


## ADR-222：撤退/投降走热键 + 模态窗，三个按钮按 `useOriginalResources()` 现拼；冒烟加 `--key-at`

**背景**：`bd` 里 `ukw.14` 要补战斗界面的「撤退」与「投降」。参考分布在三处：

1. 热键分发在 `battle_interface.cpp`：`HotKeyEvent::BATTLE_RETREAT` → `Dialog::Message( "", _( "Are you sure you want to retreat?" ), Font::BIG, Dialog::YES | Dialog::NO )`，
   `HotKeyEvent::BATTLE_SURRENDER` → 先 `arena.CanSurrenderOpponent()` 判「敌我都是英雄」，不成立就
   `Dialog::Message( "", _( "Surrender is disabled" ), ... )`，成立才开 `DialogBattleSurrender()`（`battle_dialogs.cpp:1087-1220`）。
2. 报价来自 `arena.GetCurrentForce().GetSurrenderCost()`——敌方按自己部队市价折算、要价受`Hero::GetLevelSkill()` 打折，
   端口 `GetSurrenderCost()` 已逐行对齐。
3. 三个按钮（ACCEPT / DECLINE / MARKET）在原版里都**不是归档条目**：`icn.h` 的 `BUTTON_SMALL_ACCEPT_GOOD`、
   `BUTTON_SMALL_DECLINE_GOOD`、`GOOD_MARKET_BUTTON` 属「自定义 ICN」，`useOriginalResources()` 分支用
   `SURRENDR` 的 0/1、2/3 与 `ADVBTNS` 的 4/5 现拼，还要 `ReplaceColorId( ..., 28, 56 )` 修字色、
   `copyTransformLayer()` 对齐透明层、`setButtonCornersTransparent()` 切角、`addTransparency( 36/61 )` 抠黑底。

**决定**：

1. **窗口**：新增 `FHeroes2.UI/Battle/BattleSurrenderWindow.cs`，逐项对齐 `DialogBattleSurrender()`——底图
   `SURDRBKG`、头像框 `SURRENDR` 4、`PORT_BIG` 大头像、`pos_rt = ( ( 640 - w + 16 ) / 2, ( 480 - h + 16 ) / 2 )`
   的居中偏置、标题/报价/金币警告的字位与 `Font::BIG` 折行宽度 275。市场返回后调 `Refresh()` 重算可否支付。
2. **按钮现拼**：`TryCreate()` 只用真实存在的归档帧（`SURRENDR` 0-3、`ADVBTNS` 4/5）在本地拼出三个按钮，
   克隆后再改，绝不动 `IcnSpriteCache` 里的共享帧。第一版按归档名读这三个 id，读到的是不存在的 `CUSTOM`，
   整个窗口建不起来——这是本轮修掉的真错误。
3. **YES/NO 同理**：`SystemButtonArtwork` 的 YES/NO 也从归档名改成 `SYSTEM.ICN` 的 5/6 与 7/8；第一版读到
   `CUSTOM` 后静默退回 OKAY，冒烟里撤退确认框两个按钮都写着 OKAY。
4. **模态但不开循环**：窗口不开自己的事件循环，场景每帧把 `LocalEvent` 喂给 `ProcessEvents()`，参考的
   `while ( le.HandleEvents() && !result )` 由帧循环顶替——与设置窗、魔法书一致。消息框同样处理，
   顺序是「消息框 → 市场 → 投降窗 → 设置窗」。
5. **冒烟加 `--key-at <frame:Key[;…]>`**：`R`/`S`/`C`/空格只走 `Game::HotKeyPressEvent()` 读 `LocalEvent`
   按键状态，没有可点的入口。新开关按帧把键按下、下一帧抬起（与 `--left-click-at` 的按下/松开同构），
   `--key-at 12:R;40:S` 就能在冒烟里打开这两个窗口。

**后果**：人类侧撤退/投降完整可用（确认 → 命令 → 竞技场结算 → 回冒险地图）；参考的 EVIL 版按钮与
`makeButtonWithShadow()` 的阴影仍是差异（端口无全局好坏界面开关，阴影由底图自带）。投降窗对**英雄对英雄**才有意义，
而端口目前只能开「英雄打野怪」与「英雄攻城」两种战斗，`CanSurrenderOpponent()` 在这两种里都为假，
所以投降窗只有单测覆盖、没有真机截图；`S` 在冒烟里得到的是参考的 `"Surrender is disabled"`。

**验证**：构建 0 错误 0 警告；`BattleSurrenderWindowTests` 7 条（新增「按钮由原版帧拼装」「无底图不建窗」两条，
证明不再走 `CUSTOM` 归档查找）；全量 5 628 绿（UI 1 597）；`battle.png`、`battle-siege.png` 两张基线 `identical`；
冒烟 `--battle --key-at 12:R --screenshot-frame 20` 截到 YES/NO 版确认框（`tools-output/smoke/retreat2.png`），
追加 `--left-click-at 18:267,258` 后日志依次是 `Are you sure you want to retreat?` →
`The player's Sprite acts: Retreat.` → `attacker 'Loss, Retreat'` → `the adventure map is back`；
`--key-at 12:S` 得到 `Surrender disabled`。

## ADR-223：八向剑光标按「花瓣 + 可打方向集合」挑，`GetBattleCursor()` 整段搬成纯函数

**背景**：`bd` 里 `ukw.16` 要补战斗的八向剑光标。参考的 `Interface::GetBattleCursor()`
（`battle_interface.cpp:2922-3064`）与 `getSwordCursorForAttackDirection()`（`:637-661`）合起来是这么一件事：
鼠标落在目标格八个花瓣的哪一片决定「刀从哪边来」，但八片里只有真的打得着的那几片算数，所以要先把
「这一格可打的方向集合」算出来，再把鼠标方向吸附到其中最近的一个，最后换成对应的那把剑。

三处细节在位：

1. 集合问的是**目标格**的六个邻格（`Board::GetIndexDirection( _currentCellIndex, dir )`），不是攻方的邻格；
   判据是 `Board::CanAttackFromCell`——攻方能不能站到那个邻格上出手（含护城河规则）。
2. 宽单位多两支「正上/正下」：条件是目标格的两个斜邻都在盘上，且攻方的头正好能落在那个邻格上
   （`Position::GetReachable( unit, dst ).GetHead()->GetIndex() == dst`，尾巴塞过去的那一刀已被六邻格覆盖）。
   身体朝右时走右上、朝左时走左上（`unit->isReflect()`）。
3. 吸附顺序：鼠标方向若可用就原样用；只有一个候选就取它；否则从鼠标方向出发**每一步先顺时针再逆时针**，
   两边同时有解时取顺时针。

**决定**：

1. **搬成纯函数**：新增 `src/FHeroes2.UI/Battle/BattleCursor.cs`，`Describe( arena, currentUnit, hoveredCell, cursor )`
   返回 `BattleHover( theme, status )`，与 `BattleSpellCursor` 同一套写法。它只问竞技场、单位和鼠标，不碰一张图，
   于是能在 `tests/FHeroes2.UI.Tests` 里对着真竞技场断言。
2. **整段而不是只补剑**：`GetBattleCursor()` 顺带补上了此前缺的三支——`WAR_INFO`（自己格/同阵营格，
   `«View %{monster} info»`）、`WAR_ARROW` / `WAR_BROKENARROW`（弓手未被贴身时 `«Shoot %{monster}»` 加弹药句，
   `isShootingPenalty` 决定是否断箭）、以及兜底的 `WAR_NONE` + `«Turn %{turn}»`。移动/飞行提示也从
   原来的 `"Move"` / `"Flying"` 改成参考的 `«Move %{monster} here»` / `«Fly %{monster} here»`。
3. **场景侧只递参数**：`BattleScene.UpdateCursorTheme( cell, cursor )` 调 `BattleCursor.Describe` 并把结果交给
   `CursorState` 与状态行；指针离开战场、或当下不是人类单位时仍留 `CombatPointer`。
4. **鼠标在格内移动也算变化**：八把剑取决于落在哪个花瓣，所以 `UpdateHover` 的缓存从「悬停格变化」扩成
   「悬停格或指针位置变化」，与参考「每次鼠标移动重算」等价。
5. **点击路径不动**：参考用剑的方向去算 `calculateAttackPosition`，端口仍走 `CreateAttackCommand` 的
   「最便宜的邻格 + 最优攻击向量」。两条路在多数站位给出同一个向量，但玩家没法靠瞄准指定宽单位从上半还是
   下半下手——这是本轮保留的差异，记在这里而不是悄悄改掉。

**后果**：八向剑、信息、射击、断箭四种光标都能出了；提示文案与参考一致（不翻译）。冒烟截图里看不到光标本身
（端口不把软件光标画进画布），状态行文案在烟雾里也没截到——`--cursor` 悬停期间要么是人类回合尚未开始、
要么确认框把状态行清空，所以这一条只有单测覆盖。

**验证**：构建 0 错误 0 警告；`BattleCursorTests` 20 条（8 向剑映射、中心/未知无剑、吸附的顺时针优先与
逆时针兜底、单候选、无候选、自家格 → `View peasants info`、可走格 → `Move peasant here`、相邻敌人 →
按邻格方向出剑且提示 `Attack peasant`、够不着 → `CombatNone` + `Turn N`、真实竞技场的方向集合包含攻方那一侧）；
全量 5 648 绿（UI 1 617）；`battle.png`、`battle-siege.png` 两张基线 `identical`。

## ADR-224：战斗路径预览——可走范围阴影与悬停高亮按「人站哪、还打到谁」摊开

**背景**：`bd` 里 `ukw.17` 要把参考 `Interface::RedrawCover()` 的静态那半补上。它其实是三件事：

1. **当前单位的可走范围**（`_redrawCoverStatic()`，`battle_interface.cpp:2558-2641`）：只对**人类**的当前单位画，
   受 `BattleShowMoveShadow` 控制；开着格子网格时用「带网格镂空」的阴影，否则网格会被阴影整块盖掉
   （参考 `:2632` 的注释）。鼠标下的那只兵另有一段，受 `BattleMovementAreaHighlight` 控制，阴影叠画两份所以更亮；
   已经出手的单位速度被 `TR_MOVED` 归零，参考临时清掉模式、画完再放回，才显示得出它真实的行动范围。
2. **悬停高亮**（`RedrawCover()` 的 `:2360-2453`）：光标除了说「按下去会发生什么」，还要摊开「发生在哪儿」。
   普通光标只亮鼠标格；剑光标亮的是**人真正站上去的邻格**，不是鼠标指的格子
   （`Board::CalculateAttackPosition`）；宽单位走两步时头尾两格都占；双击攻击再亮出背对侧的第二个被攻格；
   全域攻击把攻方站位周围的敌兵全圈起来。
3. `_highlightUnitMovementArea` 每帧先清空、再由鼠标处设成鼠标下的兵（`battle_interface.cpp:2940`、`:3219`）。

**决定**：

1. **「亮哪些格」搬成纯静态类**：新增 `src/FHeroes2.UI/Battle/BattleCoverHighlight.cs`，
   `GetCells( arena, currentUnit, cellIndex, cursorTheme )` 返回格子列表，与 `BattleSpellHighlight` 同一套写法。
   分支次序照参考：宽单位 + `CombatMove/CombatFly` → 头尾；`AttackDirection.Unknown` → 只有鼠标格；
   剑 → `CalculateAttackPosition` 的头/尾，`IsDoubleCellAttack` 追加
   `GetIndexDirection( cellIndex, GetReflectDirection( attackingCellDirection ) )`，
   `IsAllAdjacentCellsAttack` 追加 `GetAroundIndexes( attackPosition )` 里不同阵营的兵；空则回退鼠标格。
   `BattleFieldPainter.RedrawHoverShadow` 只剩「拿格子贴阴影」。
2. **`BattleBoard` 公开一个方向查表**：原私有 `AttackingCellDirection( unit, dir )` 提成
   `public static CellDirection GetAttackingCellDirection( BattleUnit, AttackDirection )`，带 null 检查；
   `Top`/`Bottom` 两支按单位朝向（`isReflect`）摊到斜后方，其余七支原样映射，`Center` 原样返回 `Center`。
3. **剑 → 攻击方向的反表**：`BattleCursor.AttackDirectionOf( theme )` 是 `SwordFor` 的逆表
   （`SwordTopLeft→BottomRight`、`SwordTop→Bottom`、`SwordLeft→Right`、`SwordBottomRight→TopLeft`…），
   非剑一律 `AttackDirection.Unknown`。反向写一份而不是从 `SwordFor` 反查，是为了让「光标 → 方向」也能被单测钉住。
4. **画布只贴图**：painter 新增 `SetMovementAreaUnit( BattleUnit? )`（场景每帧先清空再设，与参考同拍）与
   `RedrawMovementArea()`，插在 `RedrawHoverShadow()` 之前、`RedrawArmies()` 之前（阴影必须压在单位下面）。
   三张阴影一次生成后缓存：普通 `BuildHexagonShadow( 4, 2 )`、带网格 `( 4, 1 )`、高亮双份 `( 2, 2 )`；
   鼠标格沿用已有的 `( 2, 1 )`。格子开关 `IsBattleShowGrid` 决定用哪一张。
5. **场景侧只递参数**：`BattleScene.UpdateHover` 在 `SetHoveredCell` 之后按
   `IsBattleMovementAreaHighlight` 把鼠标格上的兵交给 `SetMovementAreaUnit`，指针离开战场即为 `null`。

**后果**：野战与攻城基线多了当前人类单位的可走范围阴影，所以两张都按新像素重建
（`battle.png` 变了，`battle-siege.png` 重写但字节相同——攻城方的人类单位周围恰好没有可走格）。
悬停高亮只在有指针时出现，基线里没有指针，因此不体现在基线上，由单测钉住。

**验证**：构建 0 错误 0 警告；`BattleCoverHighlightTests` 8 条（普通光标只亮鼠标格 3 例、剑亮站位格、
宽单位走两步亮头尾、双击攻击亮背后第二格、全域攻击亮相邻敌人、农民可达格数在 1..40 之间防退化）；
`BattleAttackPositionTests` 11 条（正上/正下按朝向摊斜、环上六支原样、单格单位站在邻格、`Center` 原样、
宽单位尾侧、够不着给空）；`BattleCursorTests` 增 12 条（`AttackDirectionOf` 是 `SwordFor` 的逆，8 向；
四个非剑光标为 `Unknown`）；全量 **5 679** 绿（Core 292 / Assets 434 / Game 3 316 / UI 1 637）；
`battle.png`（170,009 字节，`438AAB4E…`）与 `battle-siege.png` 两张基线不带 `-Update` 重跑均 `identical`。

## ADR-225：移动动画——先播后交命令，位置按格改，偏移单独算

**背景**：`ukw.19` 要补 ADR-213 后果里记的那处接缝：参考在 `Arena::ApplyActionMove()` 里
`_interface->RedrawActionMove()` 一路阻塞着画完、每格画完才 `unit.SetPosition( *dst )`
（`battle_interface.cpp:4524-4704`），飞兵走 `RedrawActionFly()`（`:4715-4830`）。端口没有能阻塞的事件循环：
`BattleScene.Update()` 每帧推进，竞技场一旦 `ApplyAction` 就当场改完状态。若先改再画，兵已经站在终点，
偏移公式没有起点可用；若只画不改，下一格的水平偏移又会从起点算起，画面上越走越落后。

**决定**：

1. **顺序反过来：先播，播完再交命令**。`BattleScene.PlayCommands()` 遇到单条 `Move` 命令且
   `BattleFieldPainter.StartMovement()` 能开播时，把命令与状态行存进 `_pendingAction` 直接返回；
   `Update()` 在输入与回合推进之前先推进这场行军，播完才调 `ApplyTurn()` 把命令交给竞技场。
   行军期间指针不更新、按钮不响应，与参考阻塞时的表现一致。应用命令这一拍把位置从起点跳到终点，
   而视觉上最后一帧已经贴着终点，所以看不到跳变。
2. **按格改位置，和参考同拍**。`MovePlayback` 每格序列播完就 `unit.SetPosition( cell )`，宽单位的转身格
   只翻面（`SetReflection`）；非宽单位每格开局 `UpdateDirection( cell.Rect )`。
3. **计划与绘制分开**：`BattleMoveAnimation.Create()` 只算「播哪几步」——步行取
   `GetPath( unit, BattlePosition.GetReachable() )` 的格子，`isOneStepPath` 用 `MoveQuick`，其余首格
   `MoveStart`、末格 `MoveEnd`、中间 `Moving`；飞行按 `getLinePoints()`（`math_tools.cpp:47-74`）把直线切成
   `FlightSpeed` 一段，`FlyUp` + 每段一个 `Moving` + `FlyLand`。`BattleMoveStep` 带上这一步的 `_movingPos`
   与 `_flyingPos`，绘制层不必回头查棋盘。
4. **偏移抽成纯函数**：`BattleTroopDisplacement.GetOffset()` 是 `_drawTroopSprite()` 的
   `:2069-2110` 那几十行——飞兵在空中时按 `_flyingPos → _movingPos` 插值；飞兵在地面移动时两向滑行；
   走兵横向那一步交给帧本身、纵向按 `movementProgress`、对角再叠 `-sign( moveX ) * frameXOffset / 2`。
5. **节拍共用一条自定义延迟**：每帧延迟 = `ApplyBattleSpeed( MoveSpeed )`（急速 ×0.8、迟缓 ×1.2）除以
   `MOVING` 序列帧数，写进 `GameDelays.SetCustomUnitMovementDelay`，行军期间由它而不是待机延迟推进。
6. **空中的兵最后画**：行内收集时跳过 `FlyingPosition != null` 的兵，铺完两军再单独画一次
   （`battle_interface.cpp:1955-1975`），免得被前排大只的兵挡住。

**偏差（记在 `ukw.19` 的子任务里）**：AI 与自动战斗仍即时结算，只有玩家的单条 `Move` 会播；攻击、受击、
死亡与弹道还没画（`ukw.19` 剩余子任务）；护城河裁剪 `_drawTroopSpriteWithMoatMask` 未搬；飞行途中不翻面
（参考也不在 `RedrawActionFly` 里翻，落点由竞技场应用命令时定）。

**验证**：构建 0 错误 0 警告；`BattleMoveAnimationTests` 8 条（原地不动为空、一格是 `MoveQuick`、
三格是 `MoveStart`/`Moving`/`MoveEnd`、每步瞄自己格子、规划不动棋盘、飞兵 `FlyUp`→`Moving`→`FlyLand`、
线段切段数与两端）；`BattleTroopDisplacementTests` 7 条（静止/水平无偏移、右下与左上对角漂移、
飞兵滑行、空中插值、零帧无偏移）；全量 **5 694** 绿（Core 292 / Assets 434 / Game 3 316 / UI 1 652）；
两张战斗基线不带动画、不带指针，重跑仍 `identical`。

## ADR-226：攻击动画——先记一笔，事后回放，起手仍赶在结算前

**背景**：ADR-225 只补了位移。参考的攻击在 `BattleProcess()` 里分两拍调界面：先 `RedrawActionAttackPart1()`
（起手帧，`battle_interface.cpp:4088-4175`），伤害落完再 `RedrawActionAttackPart2()` 起受击/死亡循环
（`:4185-4439`）；`battle_action.cpp:198-210` 的顺序也是「画起手 → TargetsApplyDamage → 画受击」。
端口一次 `ApplyActionAttack` 就把伤害、反击、双击全结算完，界面手上什么都没有。

**决定**：

1. **竞技场记账**：新增 `BattleAttackBlow`（攻方、受方、是否射击、这一击的所有 `BattleDamageTarget`），
   每次 `ApplyActionAttack` 开头清空、`BattleProcess` 里落完伤害追加一条——主击、反击、双击各一条，
   和参考「每个 `BattleProcess()` 播一遍」一一对应。对外只暴露 `LastAttackBlows`。
2. **起手赶在结算前播**：`BattleScene` 对单条 `Attack` 命令先 `StartAttackSwing()`（此时还不知道掉多少血、
   死没死），用 `_pendingAction` 挡住输入；播完 `ApplyTurn` 结算，再从 `LastAttackBlows` 建回放脚本
   `StartAttackReactions()`，用 `_pendingReactions` 挡住第二轮输入。
3. **序列选择照抄行算术**：近战基 `MeleeFront`、双击打两格改 `RangFront`，目标在画面上更高时 -2、更低时 +2；
   射击基 `RangFront` 同样按行 ±2。`MonsterAnimationType` 的排列与参考 `MELEE_TOP..RANG_BOT` 一致
   （每支占两个号），所以加减二就是上/下两支。后半段是 `+1`（`End`），播完回 `Static`。
4. **受击只分三种**：`!isValid()` → `Kill`，`Damage > 0` → `Wnce`，否则不播（抗魔/免疫）。掉几个兵但部队
   还在是抖一下，不是倒下——参考也是先看整支部队是否还在。
5. **倒地的那支自己画**：`ApplyActionAttack` 之后死掉的部队已被 `RemoveDeadUnits()` 摘离棋盘，按格子找不到了，
   所以 `AttackPlayback` 把 `Kill` 步骤涉及的部队记成 `DyingUnits`，`RedrawArmies` 铺完两军后再照着它最后的
   位置单独画一次——这样它在起手期间仍然站着，到自己的 `Kill` 步骤才倒。
6. **节拍**：射击按 `ApplyBattleSpeed(ShootSpeed) / 动画帧数`（`:4112-4119`），其余按 `ApplyBattleSpeed(120)`
   一帧；都用 `CUSTOM` 时钟，与行军共用但不会同时跑。

**偏差**：
- 一次只推进一个单位（参考把攻方与受方放同一循环），所以是「砍完再挨打」而不是「砍到一半对方在抖」。
- 双击是否真的扫到两格要结算后才知道，起手先按 `IsDoubleAttack` 猜，只影响前一半姿势。
- 射击的上/中/下用行比较代替参考那道 25° 发射角，宽目标时略粗。
- AI 与自动战斗仍即时结算；远程没有箭矢（`ukw.19.2`）；护城河裁剪未搬（`ukw.19.3`）。

**验证**：构建 0 错误 0 警告；`BattleAttackBlowTests` 5 条（近战记账、射击标记、反击各一条、射击不引反击、
下一次攻击清空）；`BattleAttackAnimationTests` 9 条（同排/下方/上方近战、双击改宽序列、射击三支、后半段 +1
与非攻击序列原样、击杀与受击、单击只剩受击、反击回放「起手+收势+受击」）；全量 **5 708** 绿
（Core 292 / Assets 434 / Game 3 321 / UI 1 661）；两张战斗基线仍 `identical`。

## ADR-227：远程弹道——几何一次算完，画面一格一格走

**背景**：ADR-226 补上了射击的起手序列与受击，但箭本身不飞。参考把它拆成两头：`RedrawActionAttackPart1()`
算路线（`battle_interface.cpp:4120-4156`），`RedrawMissileAnimation()` 按 `BATTLE_MISSILE_DELAY` 一格一格
blit（`:1062-1136`）。

**决定**：

1. **单位位置算术抽出来**：新建 `BattleTroopSprite`，把原先 `BattleFieldPainter` 私有的 `GetTroopPosition()`
   与 `GetCenterPoint()` 搬进去，绘制与弹道共用；`cellYOffset` 也一并挪过去。
2. **`BattleMissile` 只算几何**：`GetAngle()` 照 `math_tools.cpp:20-33`（`dx < 0` 的那支归一化），
   `GetDirection()` 用参考的 25° 阈值分上/平/下；`AimShot()` 先按正面偏移求角度、同排强制平射
   （`:4126-4128`），再按实际方向的 `projectileOffset` 定起点，宽单位镜像时从 `width*3/4` 出手。
   路线 = `LinePoints.GetLinePoints(起点, 终点 ± 半个弹体, max(弹体宽, 25))`，除法师与巨魔外丢掉第一个采样点
   （`:1080-1096`）；blit 偏移按 `reverse` 让出整个弹体宽、按角度正负让出整个弹体高（`:1121-1135`）。
3. **画面按帧推进**：`BattleFieldPainter.MissilePlayback` 每个 `BATTLE_MISSILE_DELAY` 前进一个采样点，
   `RedrawMissile()` 在所有单位之后贴图（镜像用 `Sprite.Flip`）；起手播放完才放箭（`_pendingMissile`），
   `IsAnimating` 把弹道也算进去，场景据此继续挡住输入。

**偏差**：
- 法师（Mage/Archmage）的「弹道」是没有 sprite 的闪电线，端口这段不画，只算几何后跳过；其余射手正常。
- 参考在弹道循环里先 `WaitForAllActionDelays()`，端口没有统一延迟队列，起手已经播完才放箭，效果等价。
- 飞行途中的 `redrawTroopSprite` 累积去重（同一帧内多次落点覆盖）未搬，采样点密度已经覆盖。

**验证**：`BattleMissileTests` 11 条（角度四向、25° 阈值六点、平射路线与跳首点、下射终点半格、左射镜像与
偏移、上射垂偏移、法师到中心不跳点、巨魔保留首点、同排平射、下一行向下射）；构建 0 错误 0 警告；全量
**5 719** 绿（Core 292 / Assets 434 / Game 3 321 / UI 1 672）；两张战斗基线仍 `identical`。

## ADR-228：护城河裁剪——水面带走一格，sprite 切三块

**背景**：单位走进护城河时，参考把水面从 sprite 里挖掉（`_drawTroopSpriteWithMoatMask()`，`battle_interface.cpp:2121-2200`），
让脚「泡」在水下；端口此前整块 blit，单位像站在水面上。

**决定**：

1. **判定放游戏层**：新增 `BattleMoatMasking`。`GetMask()` 依次判：没建护城河 / 这只正在天上飞 / 前后都不是河格 → 不裁；
   斜向出河、或从右上（`TopRight`）斜向进河 → 不裁；进河看 `next` 格、其余看脚下；`GetMoatCellMask()` 拿到水面矩形后，
   「在河里」或「非斜向」时按 `movementDelta` 平移（`x -= delta.x`、`y += delta.y`）；与 sprite 求交，空则不裁；
   `sprite.height - mask.height < 10` 的矮 sprite 放弃。返回的是**已裁剪到 sprite 的水面矩形**。
2. **切块也放游戏层**：`GetVisiblePieces()` 返回 `Top`/`Left`/`Right` 三个 sprite 局部矩形，绘制器只管依次 blit；
   水面之下不画，正好是「脚在水里」。
3. **位移的方向与漂移补齐**：`BattleTroopDisplacement.GetMovement()` 给出参考的 `movementDirection`（`GetDirectionFromDelta`）
   与 `movementDelta`（只有对角步漂移；水平步与飞行都不漂）。ADR-225 当初只算了位移本身。
4. **绘制器接入**：`RedrawTroopSprite()` 在位移之后求 `spriteArea`，`HasBuiltMoat()` 查 `Arena.Castle.IsBuild(BuildingType.Moat)`，
   `isFlyingInMotion` 取 `_move.FlyingPosition != null`；命中掩码就走 `RedrawTroopSpriteWithMoatMask()`。

**偏差**：
- 参考用 `AlphaBlit(..., unit.GetCustomAlpha())`；端口没有「自定义 alpha」概念，直接 blit。
- 参考三块 blit 每块自带 `reflect`；端口先 `Sprite.Flip` 整幅再切块，坐标等价（`Flip` 保持尺寸与偏移）。
- 对角的 `movementDelta` 只在「正在走的单位」上非零，与参考一致；静止单位水面不随镜头动（镜头本就不动）。

**验证**：`BattleMoatMaskingTests` 12 条（无河、飞行、陆地、河里水带、对角漂移、矮 sprite、sprite 够不着水面、
斜向出河、斜向右下进河被裁、斜向右上进河不裁、三块切分与面积、水带与 sprite 同宽），
`BattleTroopDisplacementTests` 新增 4 条（对角漂移与方向、水平只报方向、飞行不漂、静止无方向）；
构建 0 错误 0 警告；全量 **5 735** 绿（Core 292 / Assets 434 / Game 3 333 / UI 1 676）；两张战斗基线仍 `identical`
（部署阶段没有单位站在河里，裁剪未触发，属预期）。

## ADR-229：尸体留在场上——坟场重画与死亡末帧

**背景**：结算后 `RemoveDeadUnits()` 把阵亡单位摘盘，此前端口只在本轮攻击回放期间自绘离场单位（ADR-226），
回放一结束尸体就消失。参考把尸体留在 `Battle::Graveyard` 里，并在每次重画战场时补画
（`RedrawKilled()`，`battle_interface.cpp:2910-2920`）。

**决定**：

1. **去重规则入游戏层**：`BattleGraveyard.GetUnitsForRedraw(cell)` 返回该格要画的尸体，排除宽单位的**尾巴格**
   （`:2914-2919`，否则一个身体会被画两次）。
2. **两条重画路径**：野外战在 `RedrawArmies()` 开头一次性铺底（`:1703-1705`）；城战在行循环里按城墙前后
   `CollectRowDead()` 分成两批，垫在该侧单位之前画（`:1758-1766`）。
3. **死亡序列停在末帧**：`AttackPlayback` 新增 `_isDyingStep`，死亡步骤播完后不再切回 `Static`，
   这与参考一致（被杀的单位从不被重置）。
4. **没播过动画的尸体补帧**：AI 回合即时结算时没人播死亡序列，`RedrawCorpse()` 首次绘制时把它切到 `Kill`
   并推进到末帧，免得尸体以站姿躺在地上。
5. **复活兜底**：`AnimateIdleUnits()` 若发现**活着的**单位还停在 `Kill` 状态（刚被复活），切回 `Static`。

**偏差**：
- 参考城战把尸体、移动单位、飞行单位一起在行内排序；端口移动单位仍由 `_move` 单独处理（ADR-225 的既有简化）。
- 法术覆盖层（`_redrawActionSpellCast*`）仍未搬，与尸体无关。
- 尸体不带 `GetCustomAlpha()` 的淡出，端口没有自定义 alpha。

**验证**：`BattleGraveyardTests` 新增 3 条（窄单位画一次、宽单位只在头格画、空格无内容）；构建 0 错误 0 警告；
全量 **5 738** 绿（Core 292 / Assets 434 / Game 3 336 / UI 1 676）；两张战斗基线仍 `identical`
（部署阶段没有尸体）。

## ADR-230：血量条——几何入游戏层、填色入界面层

**背景**：`drawHitPointsBar()`（`battle_interface.cpp:2202-2228`）一直没搬：它要按调色板取
`GetColorId( 0, 200, 0 )` / `GetColorId( 200, 0, 0 )` 两个索引，而绘制器此前没有调色板。调色板随 ADR-228 的
网格注入之后（`BattleFieldPainter` 的 `palette` 参数），这一档就能补了。开关默认关闭
（`Settings` 的默认选项不含 `BattleShowHitPointsBar`），所以两张基线不受影响。

**决定**：

1. **几何入游戏层**：`BattleHitPointsBar` 放 `Height = 4`、`Frame = 1`、`OutlineColor = 117`、绿红两组 8 位分量，
   `GetInnerWidth()` 与 `GetRemainingWidth()` 复刻参考的 `uint64` 乘法与 `clamp( 1, innerWidth )`。
2. **填色入界面层**：`BattleHitPointsBarPainter.Draw()` 拿计数条的位置与尺寸当锚，`y` 取计数条底边，
   三层 `FillRect`（117 外框、红内层、绿内层），不碰资产，可以单测。
3. **绘制器接入**：`RedrawArmies()` 每帧读一次 `Settings.Get().IsBattleShowHitPointsBar`（对应参考的
   `:1694` 局部量），传进 `RedrawTroopCount( unit, hitPointsBarEnabled )`；开关打开时计数条先上移四像素
   （镜像上移八像素），血量条挂在它下方（`:2250-2261`）。绿红两色由注入的调色板经 `PALColorId.Get()` 解析，
   按实例缓存；没有调色板时整档退化为不画，与网格的降级一致。
4. **设置窗示意条**：`ComposeHitPointsIcon( enabled )` 按参考把计数条贴底
   （`image.Height - barHeight - 4 - 5`，那四像素就是留给血量条的位置），开关打开时以「三分之二血」画一条示意；
   颜色走 `EmptyButtons.GetColorId()`（读 `KB.PAL`）。

**偏差**：

- 参考用 `assert` 要求剩余血量与单体血量大于零且剩余不超过最大；端口在剩余为零或单体血量为零时返回宽度 0，
  因为死掉的队伍也会走到这段绘制，发布版里不该变成除零。
- 参考的两个颜色是函数内 `static` 索引，首次调用时算；端口按实例缓存（绘制器用注入调色板、设置窗用 `KB.PAL`），
  取值相同。
- 设置窗图标的计数条原来摆在图标垂直居中，现改为参考的贴底位置——原来那里是没接血量条时的临时摆法。

**验证**：`BattleHitPointsBarTests` 9 条（常量、内宽、满血、三分之二、最后一点血、超出上限、无容身之处、
零剩余/零上限、颜色值）；`BattleHitPointsBarPainterTests` 5 条（外框包围内层、绿占三分之二、满血无红、
空血只有红与框、两像素宽的条只剩框）；构建 0 错误 0 警告；全量 **5 752** 绿
（Core 292 / Assets 434 / Game 3 345 / UI 1 681）；两张战斗基线仍 `identical`（开关默认关闭）。

## ADR-231：行动顺序条——预测入游戏层、布局与图标入界面层

**背景**：`Battle::TurnOrder`（`battle_interface.cpp:1187-1379`）画的是「这一轮谁先谁后」：一串 43 像素的
`MONS32` 头像，每人一个，按行动先后从左排到右，已行动的被压成灰。它依赖两份数据——界面私有的快照
`_orderOfUnits`，和 `Turns()` 里维护的 `orderHistory`——而端口此前两者都没有，只有「谁在行动」。开关
`Settings::BattleTurnOrder` 三档（off/top/bottom），默认 off，所以两张基线不受影响。

**决定**：

1. **预测入游戏层**：`BattleOrderOfUnits.Predict()` 复刻 `UpdateOrderOfUnits()`（`battle_arena.cpp:223-250`）：
   先把 `orderHistory` 整段抄到前面，再把两军按速度降序（`BattleUnits.SortFastest`，稳定排序）各取第一个
   「速度大于 `Speed.Standing`」的比一下，取中的只从自己那一边删掉，同速看 `preferredColor != 守方色`；
   取到的如果是当前单位就跳过（它已进历史），每排进一个就把先手判给对面。`TakeNext()` 对应
   `GetCurrentUnit( ..., ordersMode = true )`（`:169-203`）。
2. **数据归竞技场**：`BattleArena` 加 `_orderHistory` / `_orderOfUnits` 与只读属性 `OrderOfUnits`，在四个
   与参考一致的位置刷新：新一轮开始前（`orderHistory` 清空 + `UpdateOrderOfUnits( 对面色 )`）、选中下一个
   单位后（入历史 + 重排）、每座塔开火后、每条动作结算并清完尸体后。
3. **布局入界面层**：`BattleTurnOrderLayout` 只算几何与索引——`IconSize = 43`、
   `GetSlotCount() = min( 战场宽度 / 43, 有效单位数 )`、`GetArea()` 把整条居中于战场顶边、
   `SelectSlots()` 复刻 `:1349-1375` 的挑选（跳过阵亡；已行动者只在「位置有富余」时保留，
   判据里的计数用参考的 `unitsProcessed`，跳过时也加一）。
4. **图标入绘制器**：`RedrawTurnOrderBar()` 读设置档位，悬停/当前单位用 110 号描边色，其余取
   `BattleArmyColor.GetPaletteIndex( unit.GetCurrentColor() )`；`RedrawTurnOrderIcon()` 铺 `SWAPWIN` 第 0 帧
   `(37,268)` 内缩一像素的底、居中偏下三像素贴头像、左上 `(2,4)` 画缩略数量、描边、最后按 `TR_MOVED`
   两道 `ApplyPalette`（`GRAY` 再 `GRAY_FONT`，参考直接写 3，正好是同一档）。头像按（兵种，镜像，翻转）
   三元组缓存：`CAP_MIRRORIMAGE` 走 `MIRROR_IMAGE` 调色板，守方（`unit.Color == 守方军队色`）水平翻转。
5. **颜色表入游戏层**：`BattleArmyColor` 放八个 `ARMY_COLOR_*` 常量与
   `GetPaletteIndex()`（`battle_interface.cpp:723-750`）：六个玩家色各归各档，`UNUSED`（狂暴）→ 黑，
   `None` → 灰，其它 → 灰。

**偏差**：

- 参考只在界面要的时候（`_orderOfUnits` 非空）才维护顺序表，端口一律维护：每次动作多排一遍最多二十个单位，
  换来「没有界面也能在测试里断言顺序」。
- 只实现「战场窗口内、顶边」一档：屏幕固定 640x480，窗口外没有第二块地方可放，`Bottom` 等同不画
  （战斗设置窗因此也不会让玩家选到它）。
- 当前/悬停单位的描边固定 110，参考在 110/114/118/122/126 之间按 `BATTLE_SELECTED_UNIT_DELAY` 循环——
  那套节奏跟着单位描边动画走，而描边动画还没搬。
- 图标的鼠标处理（左键开兵种信息、右键同样）与条自身的 `ImageRestorer` 还没搬：这一档只画，不响应。

**验证**：`BattleOrderOfUnitsTests` 6 条（快者先、同速按先手色、先手逐位交替、当前单位不进列表、历史在最前、
空场为空）；`BattleArmyColorTests` 4 条（八个常量值、六个玩家色、`None`/`Unused` 两个特例、未知色落灰）；
`BattleTurnOrderLayoutTests` 8 条（图标 43、槽数取小、整条居中、随战场顶边、无位置、位置够全画、
已行动者先让位、阵亡者跳过）；构建 0 错误 0 警告；全量 **5 770** 绿
（Core 292 / Assets 434 / Game 3 355 / UI 1 689）；两张战斗基线仍 `identical`（开关默认关闭）。

## ADR-232：英雄立绘与旗帜——帧表与状态机入游戏层、贴图与翻转入界面层

**背景**：`Battle::OpponentSprite`（`battle_interface.cpp:987-1140`）一直没搬，战斗画面两侧因此是空的。
它要三样东西：从种族与「英雄还是队长」推出用哪个 `CMBTHRO*` / `CMBTCAP*` 图档、每个姿势（欢呼/施法/待机/
叹气）的帧序列、以及立绘与施法手位的几何；再加上 `RedrawOpponents()`（`:1982-1988`）、`RedrawOpponentsFlags()`
（`:1990-2026`）和阵亡时的表情反应（`:4441-4456`）。这一档不新增开关，画面直接多出立绘与旗帜，所以两张
战斗基线都要重建。

**决定**：

1. **纯查表入游戏层**：`BattleHeroSprites` 放 `GetIcn()`（12 组种族×队长 → 图档，`matchHeroType`）、
   `GetFrames()`（`heroTypeAnim[7][9]` 全表 + `STATIC = { 1 }`、`SORROW = { 2,3,4,5,4,5,4,3,2 }` 两个常量，
   队长叹气退化为站立）、`GetCastOffset()`、`GetPosition()` 与 `GetCastPosition()`。姿势枚举是
   `BattleHeroAnimation`（顺序即 `heroTypeAnim` 的列序）。
2. **状态机入游戏层**：`BattleOpponentAnimation` 复刻 `updateAnimationState()`——站立播放完等
   `RandomizedDelay(8000)` 到点，再按 `Rand::Get( 1, 3 ) < 2` 挑 `IDLE2` 或 `IDLE`；非站立姿势播完回站立，
   但施法的「去」阶段（`CastMass`/`CastUp`/`CastDown`）停在最后一帧，等外部切到「回」阶段。时钟与随机源
   可注入，测试不需要等待也不需要真随机。
3. **几何与贴图入界面层**：`BattleOpponentSprite` 只留 `Area`、`Offset`、`IsFlipped` 与 `Redraw()`。面积在
   构造时用站立帧算（左侧 x 取 `LeftHeroXOffset + frame.OffsetX`，右侧从屏幕右沿量
   `640 - 29 - (frame.OffsetX + frame.Width)`），队长先整体偏 `(-6, -13)`、右侧偏 `(+6, -13)`。
4. **翻转必须连 transform 层**：ICN 的透明不是 alpha，而是 transform 字节；只翻图像会把透明像素画成
   0 号色（黑）。所以守方立绘与右旗都走 `Sprite.Flip(horizontal: true, vertical: false)`，结果按帧号
   （旗子再带上图档号）缓存，绘制统一用 `ImageOps.BlitSprite`。
5. **绘制器接入**：构造时按 `_arena.AttackingArmyCommander` / `DefendingArmyCommander` 各建一个（没有英雄
   或队长的一侧就没有立绘，例如只有游荡怪物的守军）；在中排（`cellRowId == 2`）画完单位之后画立绘与旗，
   与参考的 `:1972-1977` 同序。旗帜颜色取自 `IcnCatalog.GetFlagIcnId( armyColor )`，帧号走
   `IcnCatalog.GetAnimatedIndex` 的计数器。
6. **时基归 `Update()`**：`UpdateOpponents()` 每 tick 检查 `BattleFlagsDelay`（推旗子一帧）与
   `BattleOpponentsDelay`（推两侧姿势一步），对应 `Interface::Events()` 的 `:3940-3945`。
7. **死亡反应**：`SetHeroAnimationReactionToTroopDeath()` 在 `StartAttackReactions()` 开头调用；死的是守方就是
   攻方在打，攻方 60%（`Rand::Get( 1, 5 ) < 4`）欢呼，否则守方 80%（`< 5`）叹气。一次回放整段攻击，所以用
   `SelectDeathColor()` 取最后一个被全歼的单位的颜色，等价于参考循环里最后一次赋值。

**偏差**：

- 参考一个类里既放状态又放绘制；端口拆成游戏层状态机 + 界面层贴图，为的是没有资产也能断言帧序列。
- 参考对不认识的种族 `assert( 0 )`；端口回 `IcnType.Unknown`，帧表按骑士算，发布版不断言。
- 施法姿势的驱动（谁在什么时候切到 `CastUp` 再退回）与鼠标停在立绘上显示英雄名的处理还没接：这一档
  只把姿势机与死亡反应接上，跳过与悬停留到法术/提示框那一档。
- 缺图档时立绘与旗帜静默跳过，不抛异常。

**验证**：`BattleHeroSpritesTests` 22 条（12 组种族×队长 → 图档、未知种族、STATIC/JOY/SORROW/CastUp 帧序列、
队长叹气退化、四组施法偏移与未知种族、左/右/队长/带界面偏移的定位、施法点镜像）；`BattleOpponentAnimationTests`
15 条（初始站立、待机计时到点、`IDLE`/`IDLE2` 按随机数挑、`Joy` 从 6 起播、逐帧推进、播完回站立、
`CastDown` 停在第 14 帧等外部、叹气播完回站立、队长不叹气）；构建 0 错误 0 警告；全量 **5 807** 绿
（Core 292 / Assets 434 / Game 3 392 / UI 1 689）；两张战斗基线以 `-Update` 重建后复跑 `identical`
（`battle.png` 172,852 字节、`battle-siege.png` 210,501 字节，属新增绘制，见 `tests/baselines/README.md`）。

## ADR-233：战斗三窗——动画与文案入游戏层、窗口入界面层

**背景**：`battle_dialogs.cpp` 里战斗自己弹的三个窗一直没搬：战后结果窗（`DialogBattleSummary`，含战利品交接）、
招魂提示窗（`DialogBattleNecromancy`）、英雄/队长选项窗（`DialogBattleHero`，`:855-1085`）。它们都要「循环帧动画
+ 一段只等右键松开的只读走法」，且结果窗的阵亡名单长度不受七条限制。

**决定**：

1. **动画与文案入游戏层**：`BattleDialogAnimation` 复刻循环帧动画与 `push( icn, loop )` 队列，`Advance()` 一步推进、
   播完整段丢弃；`BattleSummaryText` 复刻 `GetSummaryParams`（标题、投降赔款、战功、高亮标题的判断与动画表），
   只产出数据，不碰画面。
2. **阵亡名单用列表，不用军队**：参考的 `Troops` 是不限长容器，端口 `GameArmy` 固定七槽，召唤物阵亡后条目会溢出。
   于是 `Force.GetKilledTroops()` 返回 `IReadOnlyList<Troop>`（`min( InitialCount, Dead )`、跳过 0 与未知兵种），
   并给 `MiniMonsters.Draw` 与 `Army.DrawSingleDetailedMonsterLine` 各加一个列表重载（无窗口、右起、`detailedView`）。
3. **结果窗拆成两段**：参考一个模态循环里先「摘要 + OK/重开」再「战利品 + OK」，端口按既有惯例拆成
   `ProcessEvents`（摘要段，返回 `BattleSummaryWindowAction`）与 `ProcessArtifactEvents`（战利品段，含
   `AdvanceArtifact`），由调用方驱动；神器段的规则照抄——AI 胜者只显示终极神器、同一件连续出现只补音效、
   普通神器标题只写一次、进入战利品段时把 OK 从 `BottomLeft` 挪到 `BottomCenter`。
4. **选项窗只信场合给的开关**：`TryCreate` 收 `canRetreat` / `canSurrender` / `currentColor` 三个由竞技场算出的值，
   窗口不自己判断「队长不能撤退」——参考也是靠 `CanRetreatOpponent()`（`battle_arena.cpp:899-910`）回答 false。
   `readonly` 就是参考的 `currentColor != hero.GetColor() || !buttons`；`buttons = false` 时四钮全暗、只等右键松开。
5. **队长头像复用城堡屏**：`CaptainPortrait.TryCreateBig( IViewWorldSpriteSource, Captain )` 与既有的 `AssetBundle`
   版本同法——种族大头像克隆后把旗帜贴到 `GetFlagOffset()`；英雄走 `HeroPortraitIcn.Get( PortraitId )`。
6. **补开 `TextBase.SetUniformVerticalAlignment`**：参考在士气一行关掉行均衡，并让幸运、法力继承同一个 `Text` 对象的
   状态，端口没有这个开关就没法照抄折行高度。

**偏差**：

- 音效（`AudioManager::PlaySound`）没有接；音效只在「同一件神器连出」时有可观察效果，端口以跳过重绘代替。
- 右键四钮与立绘的帮助说明、立绘点头像打开英雄屏都没有接：端口还没有战斗里的通用消息框与英雄屏入口，
  `BattleHeroOptionsWindow` 把这一帧的点击暴露成 `PortraitClicked` 交给调用方。
- 参考的 `ImageRestorer` 不需要：端口每帧从底图重画整块窗口。
- 缺图档时窗口返回 `null`（招魂窗与选项窗）或静默跳过该帧（结果窗的神器图标），不抛异常。

**验证**：新增 `BattleHeroOptionsWindowTests` 17 条（居中与左影偏移、四钮几何、标题两种写法、色块帧号、缺底图/
缺按钮帧、法力书与 `SPELLCASTED` 的施法禁用、撤退/投降开关、队长、敌方只读、无按钮等右键、关闭/施法与立绘点击、
三条状态栏文案、真实档案贴底图）；构建 0 错误 0 警告；全量 **5 824** 绿（Core 292 / Assets 434 / Game 3 392 /
UI 1 706）。三个窗口尚未接进战斗场景，没有画面变化，两张像素基线不需要重建
（`InterfaceSettingsDialogTests.ClickingTheScrollSpeedCyclesItInPlace` 偶发失败一次，单跑与整项目复跑均通过，
与本次改动无关）。

## ADR-234：拒施理由入竞技场、两句提示与法术说明走消息框

**背景**：战斗里的「不能施法」一直只说一句笼统的话，因为 `Arena::isDisableCastSpell()` 只回答真假；参考则在
`msg` 里带一句话，每个原因一句（`battle_arena.cpp:927-1008`），由界面丢进 `Dialog::Message`。法术书的两类提示
（法力值/页签/翻页、法术太贵）与法术说明（`SpellDialogElement::showPopup`）同样被写在状态行或只写日志，
玩家看不到。

**决定**：

1. **理由由竞技场给**：`IsDisableCastSpell( spell, out string? reason )` 逐条赋值，六句话作为
   `BattleArena` 的公开常量（`SphereOfNegationMessage`、`NoCommanderMessage`、`AlreadyCastMessage`、
   `NoEffectMessage`、`OneElementalTypeMessage`、`NoSpaceForElementalMessage`），逐字照抄参考；原来的
   `IsDisableCastSpell( spell )` 保留为 `out _` 的简写。可以施法时 `reason` 为 `null`（参考不动 `msg`）。
2. **指挥官检查改用当前单位**：参考先判 `_currentUnit == nullptr` 再问指挥官，端口原来走
   `GetCurrentCommander()`（内部 `GetCurrentColor()` 在没有行动单位时会断言）。改成
   `_currentUnit?.GetCurrentOrArmyCommander()`，与参考同序，也让「战斗还没开打」这一态安全地答「没有指挥官」。
3. **状态行换成消息框**：战斗场景的 `ShowMessage` 删除，改成三个口子——
   `ShowMessageBox( header, body, answer, elements )`（参考 `Dialog::OK` / `YES|NO`）、
   `ShowHeldMessageBox( header, body, elements )`（参考 `Dialog::ZERO`：没有按钮，按住右键期间显示、松开收起，
   与 `CastleScene` 的 `_messageHeldByRightButton` 同一套写法）、以及按 `ByRightClick` 二选一的
   `ShowTextMessage`。所有开框的地方仍保留「没有按钮美术时退回状态行」的兜底。
4. **法术说明照参考拼**：正文是既有的 `SpellInfo.GetDescription( spell, owner, world )`（与
   `getSpellDescription` 同源），元素是 `SpellDialogElement( icon, spell, points, font )`，图标取
   `SpellBookArtwork.SpellIcons[ spell.IndexSprite() ]`、花费取 `spell.SpellPoints( owner.BagArtifacts )`，
   标题照参考用法术名。施法者从竞技场当前行动单位取——书只能替它打开。
5. **提示的标题分两种**：参考只有「法术太贵」那句用法术名做标题（`spell_book.cpp:326`），法力值/页签/翻页与
   右键提示都是空标题（`spell_book.cpp:592-604`）；端口按 `SpellBookFrame.Spell` 是否为空区分。

**偏差**：

- 参考消息框的标题用 `FontType::normalYellow()`，端口沿用战斗已有的 `_whiteFont`（场景拿不到黄色常规字体）。
- 参考 `Dialog::ZERO` 的框在松开右键的同一帧还可能重入法术书循环，端口在松开那一帧直接收起并跳过本帧，
  下一帧才回到书（不可见差别）。
- 召唤的两句理由（已有另一种元素、英雄旁边没有空格）没有测试：要摆出相应战场，留给召唤的测试。

**验证**：新增 `BattleArenaSpellRefusalTests` 6 条（无指挥官、本轮已施法、否定之球优先级、无城堡的地震、
空法术只是探路、简写与 `out` 形式一致）；构建 0 错误 0 警告；全量 **5 830** 绿（Core 292 / Assets 434 /
Game 3 398 / UI 1 706）；两张战斗像素基线复跑 identical（消息框只在交互时出现，逐帧基线不变）。

## ADR-235：战后结果窗接进战斗场景、`--battle-only` 无人值守入口

**背景**：`DialogBattleSummary()` 在 ADR-233 里已移植成 `BattleSummaryWindow`，但没接进战斗场景——
`BattleScene.Finish()` 只写一行日志就 `Close()` 回地图，玩家看不到「谁赢、损失多少、拿了多少经验」。
另外 `--battle-auto` 只是把两边交给 AI，跑完仍然没有结果，也没有「打完一场就收工」的入口（参考的
`TYPE_BATTLEONLY` 会话）。

**决定**：

1. **结果窗由场景收尾**：`Finish()` 结算之后调 `TryOpenSummary( result )`，按参考 `Battle::Loader`
   的做法（`battle_main.cpp:390-401`）开窗；`_summary` 非空时 `Update()` 先喂它、逐帧推进，`DrawFrame`
   把它画在最上面（先于投降报价与消息框）。
2. **视角固定攻方**：战斗场景的攻方永远是玩家英雄，所以 `BattleSummaryText.Create` 的 `own` 传
   `result.Attacker`，标题高亮就是「攻方赢」，赔款取守军 `GetSurrenderCost()`（攻方输时传 0）——正是参考
   `attackerIsHuman` 那一支（`battle_dialogs.cpp:508-527`）。
3. **重开按钮不给**：参考 `allowToRestart = !showBattle`，而这个窗本身就是显示出来的战斗，所以传假；
   `Restart` 分支只留注释（真重开要重建竞技场，属冒险→战斗那条链）。
4. **神器段走空表**：守方是游荡怪或城镇守军，没有英雄也就没有神器可交接，`BeginArtifacts( [] )` 立刻答假，
   窗口就此关掉；段本身照参考实现留着，等敌方英雄参战时自然生效。
5. **无人值守不开窗**：`--battle-auto` 与新增的 `--battle-only` 都不开窗（参考 `isHumanBattle` 为假时同样
   不开），结果只进日志。`--battle-only` 隐含 `--battle` + `--battle-auto`，并在战斗结束后直接把客户端关掉
   （`GameHost.CloseBattleWhenDone`），一条命令跑完一场并留下结果。

**偏差**：

- 招魂提示窗（`BattleNecromancyWindow`）在场景路径上接不上：招魂结算在 `BattleLoader` 里，而场景是自己
  驱动竞技场的；等冒险地图→战斗那条链（ukw.27）把战后写回接上再补。
- 结果窗的按钮美术沿用场景已有的 `SYSTEM.ICN` 帧；参考的 `<okay>`/`<restart>` 两套字用端口现有字体兜住。
- 交互路径（真人对局打到结束）没有自动化：战斗打完需要真实操作，本轮只验证了窗口本身与无人值守整链。

**验证**：新增 `BattleSummaryWindowTests` 8 条（动画/摘要偏移、OK 居中、重开缺图返回空、双钮左右分布、
无输入不动作、点 OK 关闭、空神器表立刻结束、缺底图仍建窗）与 `BattleSummaryTextTests` 9 条（全胜/敌逃/敌降
赔款、队长无战功、撤退三段、投降循环、全灭两段加循环、无名字退化、输了不给经验）；构建 0 错误 0 警告；
全量 **5 847** 绿（Core 292 / Assets 434 / Game 3 407 / UI 1 714）；两张战斗像素基线 identical；
`--battle-only` 实机跑通（攻击方 Ariel 战败、退出码 0，日志留 `The battle is over: attacker 'Loss',
defender 'Wins', the defender won.` 与 `--battle-only: the single battle is finished, closing the client.`）。

## ADR-236：人类英雄的动作接缝（`HumanHeroMovementHandler` + `World.HeroActionHost`）

**背景**：动作表（`HeroActions` 的 `ActionTo*` / 战斗）早就照 `heroes_action.cpp` 写完了，`BattleLoader.Load` 也
能结算军队、瓦片、城堡与经验，但**人类英雄走的不是这条路**：`AdventureSession.AttachMovementHandler` 给人类装的是
`FogHeroMovementHandler`——只开图，走到怪物、资源、城堡上都什么也不发生。另外 `HeroActions.Fight` 的三处调用都
把 presenter 留空，`IHeroActionHost.BattlePresenter` 没有任何消费者，人类战斗即使打起来也没有画面。

**决定**：

1. **界面宿主挂在世界上**：`World.HeroActionHost`（`IHeroActionHost`，缺省 `NullHeroActionHost.Instance`）。
   参考里这些点直接调全局的 `Interface::AdventureMap::Get()`；端口没有那个全局，而世界本来就是这一局运行时的家
   （雾、被占领对象、每日事件都挂在这里）。换图时 `World.Reset()` 把它复位，存档不写它。
2. **人类四件事一个处理器**：`HumanHeroMovementHandler`——`Scout` 开图、`Action` 转给
   `new HeroActions(hero, world.Kingdoms, world.HeroActionHost).Action(...)`、`ActionNewPosition` 照抄
   `heroes.cpp:2019-2046` 的人类那一半（守卫扫描 → 先打路线终点那格，没有才打第一组邻格）、`ScoutRadar` 留给
   界面层的小地图。
3. **装配按「有没有界面」三选一**：`AdventureSession.Start( world, info, host )` 把 `host` 放上世界；
   `AttachMovementHandler` 随后按下表装处理器：

   | 谁 | 有宿主 | 没宿主 |
   |---|---|---|
   | 人类 | `HumanHeroMovementHandler` | `FogHeroMovementHandler` |
   | 电脑 | `AiHeroMovementHandler` | `AiHeroMovementHandler` |

   没宿主时人类仍只开图：命令行与测试里没有玩家在点，走到怪物格上直接开打既没画面也没提示，等于参考里
   `isHumanBattle == false` 的状态。
4. **presenter 接上了但还没有消费者**：`Fight` 的三处调用（打野怪、攻城、英雄对战）都传 `_host.BattlePresenter`。
   App 层还没有 `IBattlePresenter` 的实现（见 `ukw.29`），所以 App 眼下仍不装宿主，行为与之前完全一致。

**偏差**：

- 人类动作里要问玩家的点（宝箱拿钱/拿经验、怪物加入、招募所、谜语、市场、盗贼公会、开城堡）全部走
  `IHeroActionHost`，App 层的实现排在 `0lm.12`（已依赖 `ukw.29`）。
- 人类战斗的画面与「走到怪物格自动开战」要等 `ukw.29` 把 `BattleArena` 的动作执行与界面动画解耦之后才谈得上；
  在那之前 App 不装宿主，人类英雄不会自己开战。
- `ActionNewPosition` 的人类版没有 AI 那半段规划，与参考一致。

**验证**：新增 `HumanHeroMovementHandlerTests` 4 条（走到资源拿走、传送不开战、终点格守卫真的打起来且给经验、
无守卫不动）与 `AdventureSessionTests` 2 条（无宿主 → `NullHeroActionHost` + `FogHeroMovementHandler`；有宿主 →
同一个 host + `HumanHeroMovementHandler`，电脑英雄仍是 `AiHeroMovementHandler`）；全量 **5 853** 绿
（Core 292 / Assets 434 / Game 3 413 / UI 1 714）；两张战斗像素基线 identical；`--map` 冒烟 120 帧正常退出。

## ADR-237：面板拖动独占一帧、光标钉指针、落位后整屏重绘

**背景**：隐藏界面模式下四个面板（雷达/按钮/图标/状态栏）可按边框拖动，落位（`BorderDrag.Drop`）与
配置持久化已在 ADR-210 完成。但参考的拖动是**阻塞**的：`BorderWindow::QueueEventProcessing()`
（`interface_border.cpp:456-523`）从按下到松手都在自己的循环里 `le.HandleEvents()`，期间不处理任何别的事件，
并在一开始就 `Cursor::Get().SetThemes( Cursor::POINTER )`——代码注释写明理由：面板贴近游戏窗口边缘时，
鼠标会被换成卷屏光标。端口每帧在外面转，于是拖动期间仍跑 `ProcessTurnFrame()` 与边缘卷屏：光标被换掉，
覆盖在面板底下的按钮/图标也可能在同一帧再被判一次。

**决定**：

1. `AdventureMapScene.Update` 把拖动帧**独占**：`_drag` 非空时只泵拖动、立刻返回；拖动**开始帧**同样返回，
   对应参考的 `return true`（这一帧不再判任何面板按钮，也不跑 `FinishHumanTurn`）。
2. `PumpDrag` 在拖动开始时设 `CursorTheme.Pointer`。分层上 `BorderWindow` 摸不到光标层（光标层在 UI 之上），
   所以由场景做；`BorderWindow.QueueEventProcessing` 的 remarks 随之改成这个口径。
3. 落位后 `SetRedraw(Redraw.All)`：各面板的 `SetPos` 只挪矩形、不设重绘标记（参考也是），不补这一句画面会停在旧像素上。

**理由**：参考的「拖动期间世界静止」是语义的一部分，不是实现细节。端口把阻塞循环拆成事件状态机之后，
独占必须显式写出来，否则每帧的悬停与卷屏判断都会漏进来。光标与整屏重绘都是这条独占的直接后果，
分开放会留下「位置对了、画面没跟上」的半截状态。

**后果**：拖面板时指针恒定、面板不再被误点、松手即停在新位置并整屏刷新。拖动中不能开别的窗口——与参考一致；
若窗口失焦丢了释放事件，拖动会像参考一样一直挂着（`PumpDrag` 只在鼠标键抬起时收手）。

**没做**：不可见界面（正常布局）下的拖动——参考的守卫是 `!isHideInterfaceEnabled()` 直接返回，
端口 `QueueEventProcessing` 同样返回 `null`，两边一致，`BorderWindowTests` 有对应用例，所以这不是缺陷而是原样。
拖动期间的键盘热键也没有专门测例（独占分支不再读它们，效果与参考的阻塞循环相同）。

## ADR-238：迷雾只存一份——Tile 是唯一存储，MapFog 是视图

**背景**：端口把迷雾存了两份：`MapFog` 自己的 `byte[] _fog` 加一份方向表，以及参考原本就有的
`Tile._fogColors` / `Tile._fogDirection`。谁改了其中一份，另一份就过时——`World.ClearFog` 末尾要调
`SyncTilesFog` 把表抄回瓦片，存档读写在两个方向各搬一次（`StoreFogIntoTiles` / `RestoreFogFromTiles`），
`AIWorldPathfinderTests` 的助手必须写两遍才算揭开。参考只有瓦片那一份（`maps_tiles.h:421`，
`Maps::ClearFog` 直接改瓦片）。

**决定**：

1. `Tile.FogColors` / `Tile.FogDirection` 是唯一存储。`MapFog` 改成持有瓦片列表的**视图**：
   `MapFog(IReadOnlyList<Tile>, width, height)`，`IsFogged` / `GetDirection` / `Reveal` / `UpdateDirections`
   全部转发到瓦片，自己不存字节。
2. 删掉 `World.SyncTilesFog`、`StoreFogIntoTiles`、`RestoreFogFromTiles` 与它们的三个调用点。
3. 方向计算也合并成一处：`TileFog.UpdateFogDirectionsInArea` 抽出接受瓦片网格的重载，
   `MapFog.UpdateDirections` 用整图矩形调它，`Interface::GameArea` 的每帧调用走同一个实现，
   两份同义算法不会各改一半。
4. `World.Fog` 在瓦片没铺满 `Width × Height` 时返回 null（加载中），取代原来「有尺寸就建表」。

**理由**：两份状态必然漂移，而同步点是「谁记得调」。把 `MapFog` 降级为视图之后，编译期就拿不到第二份存储，
忘调同步不再是可能的 bug；顺带删掉 60 行与 `TileFog` 重复的方向算法。

**后果**：`World.ClearFog` 一次写入就是最终状态，存档不需要搬运，AI 测试的 `Reveal` 少写一行。
代价是 `MapFog` 的构造需要瓦片，测试改用 `FogTestGrid.Create(w, h)` 造网格；`CountFogged` 仍读派生掩码。

**没做**：`TileFog` 的局部矩形快路径与整图重算仍是两条路径（结果同源，局部那条只算脏矩形，是否保留看将来
的每帧开销）；`World.Fog` 的判空从「有尺寸」收紧到「瓦片铺满」，加载中途读迷雾会拿到 null 而不是全黑表——
目前没有这样的调用者，若将来出现，应在调用点显式按 null 处理而不是放宽这个判断。

## ADR-239：`--map` 打开的地图必须登记为「当前地图」，战斗不进存档，冒烟补「打完回地图 + F2 存档」

**背景**：一边补 `6y3.8` 的冒烟覆盖，一边发现两件事。

1. `--map` 只把 `Mp2FileInfo` 交给场景构造，**没有** `Settings.SetCurrentMapFileInfo`。而 `GameSave.Save`
   的第一道守卫就是 `settings.CurrentMapFileInfo`（GameSave.cs:184），为空时打 `There is no game to save.`
   并拒写。于是 `--map` 打开的地图按 F2 什么也不会发生——参考里 `Game::SelectScenario` 会把选中的地图记进
   settings（scenarioinfo.cpp），存档、AI 的地图版本判断、英雄的法术表都读它，端口少了这一步就等于「这张图
   不像是被选中的」。
2. 战斗里的存档：参考 `battle_interface.cpp` 没有任何存档入口，`Game::QuickSave` 只挂在冒险地图的按键上，
   所以战斗中途不能存档是**参考行为**，不是端口缺失；战斗中存档要保存的也只是它所在的世界（存档格式里没有
   战斗状态）。

**决定**：

1. `GameHost.OpenMapFile` 在读档成功后调 `Settings.Get().SetCurrentMapFileInfo(mapInfo)`，让 `--map` 与
   「选择场景后开局」走同一份登记。
2. `GameSave.Save` 成功写盘后补一条 `GameLog.Info("The game was saved to '<path>'.")`。参考成功时静默，但端口
   的失败路径有 `GameLog.Error`、成功路径却没有任何痕迹，冒烟与玩家都无从确认。
3. 新增 `tools/scripts/SmokeBattleToMap.ps1`：第一趟 `--battle` 开战，玩家点敌方一队发起攻击（走
   `Battle::Interface::HumanTurn` 的命中测试），400 帧后点状态栏 Auto 收尾，连按 Enter 关结算窗，断言日志
   里 `acts: Attack`、`The battle is over`、`the adventure map is back`；第二趟 `--map` + F2，断言存档日志与
   文件非空，并删掉自己写出的 `QUICKSAVE.savm`（不覆盖玩家真有的快速存档）。

**理由**：结果窗是模态的（`BattleSummaryWindow.ProcessEvents` 只认 OK 按钮、Enter、Escape），战斗结束时它
会一直开着等确认——看起来像「卡住」，其实是参考的结算对话框；冒烟按固定间隔连按 Enter，落在地图上也无副作用
（`HotKeyEvent.DefaultOkay` 只被四个战斗对话框读取）。

**后果**：`--map` 打开的地图现在能存档、能按 F2 快速存档；`SmokeBattleToMap.ps1` 成为第一条覆盖「人类打一场
并回到地图」的自动化。战斗结束的帧随战斗过程浮动，脚本用 5000 帧预算 + 每 700 帧一次的 Enter 兜住。

**没做**：读档端到端还没打通——冒险地图的 `L` 只弹「load a new game」确认，主菜单的 Load Game 屏尚未接线，
所以存档只验证到「写出且非空」；读回那一半等 Load Game 接线后补进同一条冒烟。
## ADR-240：AI 估值的距离/时间缩放按参考公式冻结成 golden，次元门距离按 225 计价

**背景**：`8at.5` 收口 `ai_hero_action` 时只验证了「AI 不崩、能连续行动」，`scaleWithDistanceAndTime()`
（ai_planner_hero.cpp:1007）算出来的量级一直没人对过。这个函数决定「远的东西值多少分」，是 AI 在两个目标
之间取舍的唯一依据；它错了不会崩，只会让 AI 的行为悄悄偏掉。

对拍时找到一处实际偏差：`AiPlanner.GetDistanceToTile` 把次元门的距离算成 `path.Count * 200`，而参考用的是
`Route::calculatePathPenalty()`（route.cpp:411）= 每条步的 penalty 之和，次元门每一步的 penalty 是
`Spell( DIMENSIONDOOR ).movePoints()` = **225**（spell.cpp:131）。少算 11% 会让 AI 比参考更爱跳次元门。

**决定**：

1. 次元门距离改用 `HeroPath.CalculatePenalty(dimensionDoorPath)`，与参考的 `calculatePathPenalty` 一一对应。
2. `ScaleWithDistanceAndTime` 增加一个带 `dayCount` 参数的重载并标 `internal`，生产调用仍走读
   `World.Current.DayCount` 的私有重载；`GetDistanceModifier` 同时标 `internal`。参考从 `world.CountDay()`
   直接读天数，端口把天数提成参数只是为了让测试能在固定天数下对拍，公式一字未改。
3. 新增 `AiPlannerValueScalingTests`（38 个用例）：期望值按参考公式算一次后**冻结成字面量**，覆盖距离
   （0/1/2/10/100/300）、`getDistanceModifier` 全部分组（0.8 / 0.9 / 0.95 / 1.1 / 默认 1.0，含 Event 的例外）、
   天数修正（0 / 1000 / 5000 / 9000，含 0.5 封顶）以及负值。
4. `AIWorldPathfinderTests.ADimensionDoorRouteJumpsTowardsTheTarget` 补两行断言：次元门每一步的 penalty 是
   225、整条路径的 `CalculatePenalty` 也是 225——把「不是 200」钉在路径层。

**理由**：这是能拿到「参考算出的数」的唯一可靠方式——不运行 C++，但公式是确定的，把参考公式算出的结果当
golden 存下来，实现漂移（少乘时间系数、读错天数、丢掉 log10、表里漏一组类型）就会立刻挂；而端到端的
「AI 挑了哪个目标」既依赖地图也不容易和参考对齐，不适合当数值基准。

**后果**：AI 对次元门目标的距离估计上调 12.5%，会比之前略微保守；估值缩放的量级从此有 38 个用例看着。
`8at.5` 关闭。

**没做**：`objectValue` 里各物件的基础分（artifact/resource/怪物强度折算）没有逐表对拍，那是另一批数，
本任务只到「距离与时间怎么缩放」这一层。

## ADR-241：读档端到端——玩法类型先定、地图路径按数据目录还原、读档窗口自己扣帧

**背景**：`6y3.11` 要把「存档 → 重启 → 读回 → 进图」走通。第一次实跑暴露三处断点：F2 写出的档是
`QUICKSAVE.savm`，`GameSave.Load` 以「belongs to another type of game」拒收自己写的档；即便绕过类型，
存档头里的地图只有文件名，地图文件打不开；从主菜单点 LOAD GAME 后窗口再也退不出来。

**决定**：

1. **玩法类型在开局前就定下来**：`--map`／主菜单选图写 `GameTypes.Standard`（对应 `Game::NewStandard()`,
   game_newgame.cpp:149-157），主菜单 LOAD GAME 在列档前写 Standard（`Game::LoadStandard()`,
   game_loadgame.cpp:209-213）。此前一直是 `None`（TYPE_MENU=0），于是扩展名落到 `.savm`、存档头里的类型是 0，
   读档的类型校验（game_io.cpp:227-231）谁也不匹配——自己写的档自己读不回。
2. **`--load` 按扩展名反推类型**：`.sav`/`.savc`/`.savh` → 单图/战役/热座，`.savm` 直接拒绝并告警。反推表与
   写档用的 `GameSave.GetSaveFileExtension` 成对；`.savm` 装的是网络、单场战斗与自动试玩，端口还不能继续玩，
   **不猜**。
3. **地图路径按数据目录还原**：存档头只存文件名（maps_fileinfo.cpp:666-667），参考靠
   `Settings::GetLastFile( "maps", name )` 还原（game_io.cpp:275）。端口没有那张表，改用
   `MapCatalog.FindDirectories([数据目录])` 探「数据目录」与「数据目录/maps」，命中就把完整路径写回
   `CurrentMapFileInfo`；不命中照旧报错，不做近似匹配。
4. **读档窗口的帧算作界面自己的帧**：窗口是从主循环事件处理里开出来的（点标牌那一下就在 `HandleEvents` 内），
   所以它的嵌套帧里 `_mainLoopFrame` 仍是 true，`OnFrameEnded` 会提前返回、冒烟预算永远扣不掉 → 窗口无限等。
   现在 `OnFrameEnded` 里那道判断改成 `!_mainLoopFrame || _loadDialogOpen`，窗口每帧照常扣预算、重放脚本输入。
5. **冒烟补第三节**：`SmokeBattleToMap.ps1` 在删档之前，以「无 `--map`/`--load`」启动 → 点 LOAD GAME 标牌
   (235,218) → Enter 确认列表首行（刚写的 QUICKSAVE）→ 断言「存档文件列表打开」「地图名相同」
   「reopened from a save」「Loaded the game from …」「Drawing the 'AdventureMapScene'」。
   「英雄位置没变」靠日志：地图开场的状态行补上焦点英雄与它的格子（`focus hero 'Ariel' at 5591`）——
   存档前与读档后必须逐字相同，脚本直接拿前一次的尾巴当后一次的期望。

**理由**：类型与路径都是**存档头**里的数据，缺一项就只能靠猜；而窗口退不出来是端口自己的冒烟工具问题，不该
用「再点一下」掩盖。读档成功后 `Settings` 从存档里读回 GameType，所以第 1、2 条的设置只是把校验放行，不会
把玩家的类型改掉。

**后果**：F2 写 `.sav`（不再是 `.savm`）；读档能直接进图，且地图路径不再依赖启动时的工作目录。旧版本写出的
`.savm`（头里类型为 0）无法修复，需要重新存档。

**没做**：主菜单的 Standard / Campaign / Multi-Player 子菜单没有拆，LOAD GAME 固定按单图列档（端口只有单图
可玩）；`Settings::GetLastFile` 那张「最近用过的文件」表没有移植，只做了读档需要的那一处查找。

## ADR-242：淡入的三段——调用点、设置点、宿主播放

**背景**：`6y3.5` 把 `DisplayFade` 与 `BaseInterface.RenderWithFadeInOrPlanRender` / `ValidateFadeInAndRender`
做出来了，但没有调用点、没有 `Game::setDisplayFadeIn()` 的设置点、也没有人逐帧播放，所以端口从来不会淡入。

**决定**：

1. **调用点**：`AdventureMap.Events.cs` 的 `EventDefaultAction` 在 `HeroActions.Action(...)` 之后补
   `RenderWithFadeInOrPlanRender(Redraw.Heroes | Redraw.Buttons | Redraw.Status)`。这对应参考
   `heroes_action.cpp` 的 10 处与 `ai_hero_action.cpp` 的 2 处（446、569、644、1187、1404、1745、2155、
   2208、2492、3190；472、565），每一处都在 `ActionToXxx` 打完仗的收尾位置。
2. **设置点**（`GameDisplayFadeIn.SetDisplayFadeIn()`）四处落地、两处无落点：
   - 进地图：`AdventureMapScene.TryCreate` 收尾（`game_startgame.cpp:759`），开局与读档共用一条 StartGame；
   - 城堡屏关闭回地图：`GameHost.CloseCastleWhenDone`（`game_startgame.cpp:376`）；
   - 战斗结束回地图：`GameHost.CloseBattleWhenDone`（`battle_interface.cpp:1534`）；
   - 盗贼公会关闭：`AdventureHeroActionHost.OpenThievesGuild`（`dialog_thievesguild.cpp:476`）；
   - 王国总览关闭：`AdventureMapDialogs.OpenKingdomOverview`（`kingdom_overview.cpp:874`）；
   - **英雄会面**（`heroes_meeting.cpp:863`）与**战役**（`interface_events.cpp:374`、`381`）没有落点：
     端口还没移植英雄会面窗与战役界面，设了也没人播，留给对应的移植任务。
3. **宿主播放**：`IScene.TakePendingFadeIn()` 默认返回 `null`，`AdventureMapScene` 每帧画完调
   `ValidateFadeInAndRender()`（参考 `game_startgame.cpp:1580` 是 `redraw(0)` 之后紧跟它）再把淡入交给宿主；
   `GameHost.Draw` 在 `_presenter.Upload` 之前 `TryAdvance(_palette)`，一帧推进一轮，六帧约 100 毫秒。淡入直接改
   索引像素，脏区报不了自己，所以播放那一帧整帧标脏。
4. **诊断**：开始播放时记一行 `Fading the screen in over 6 frames, 640x480`，与 `Drawing the 'X'.` 同级；
   `SmokeBattleToMap.ps1` 拿它断言「战斗结束后地图淡回来」——没有这一行，脚本分不清「标志设了」与「有人取了并播了」。

**理由**：参考的淡入是阻塞的六帧、每帧自己 `Display::render()`；端口的呈现集中在宿主，照搬会把渲染拆成两处。
把「谁要淡入（设置点）」「谁把淡入取出来（场景）」「谁推上屏（宿主）」分成三段，每一段都能单独测：
UI 层已有 `InterfaceBaseTests` 覆盖 `RenderWithFadeInOrPlanRender`/`ValidateFadeInAndRender`，端到端由冒烟覆盖。

**后果**：进图、战斗结束、关城堡屏、关盗贼公会、关王国总览之后地图会淡回来。冒烟里淡入是可断言的：
实拍帧 2 平均亮度 24.5、帧 20 为 77.6（同一张地图，640x480 PNG 采样）。

**没做**：主菜单没有淡入（参考的主菜单淡入由 SDL 渲染层做，端口的主菜单场景还没有 `DisplayFade`），所以
「离开地图回主菜单」那次 `setDisplayFadeIn`（`game_startgame.cpp:996`）没有接——设了也没有人播。

## ADR-243：地图上的 L 确认后直接进读档屏——场景只交回调，宿主按参考的两步走

**背景**：`6y3.12`。参考的 `Interface::AdventureMap::EventLoadGame` 只是确认框（`interface_events.cpp:352-359`），
答 YES 返回 `GameMode::LOAD_GAME`；`StartGame()` 把它交回主循环，主循环直接调 `Game::LoadGame()`
（`game_mainmenu.cpp:154-155`），后者先画主菜单背景再开 `Dialog::SelectFileLoad()`（`game_loadgame.cpp:215-246`）。
端口把 `AdventureEventResult.LoadGame` 走成 `RequestExit()`，玩家答完 YES 却回到主菜单，还得再点一次 LOAD GAME。

**决定**：
1. **场景不换场景**：`AdventureMapScene` 新增 `LoadGameEvent`（`Action?`），`HandleResult` 的 `LoadGame` 分支优先调它；
   没接时退回原来的 `RequestExit()`。日志分两句——接了写「the save file list is opened」，没接写「the map is left」，
   冒烟据此分辨走的是哪条路。
2. **宿主做参考的两步**：`GameHost.LeaveAdventureMapForLoadGame()` 先 `ReturnToMenu()` 再 `OpenLoadGame()`。
   顺序照抄参考：地图离开之后列表才画在主菜单背景上；列表取消停在主菜单——参考回的是 Load Game 子菜单，
   端口还没有那个子菜单。
3. **装配收拢到 `ShowAdventureScene`**：`TryEnterAdventureMap` 原来自己设 `ExitEvent` 与 `host.RunModal`，
   与 `ShowAdventureScene` 重复；现在只有一处装配，两个回调（退出、读档）都接在那里。
4. **冒烟补第四节**：`SmokeBattleToMap.ps1` 在删快存之前，以 `--map` 启动 → `20:L` → `40:Enter`（确认框 YES）→
   `70:Enter`（列表首行）→ 断言「the save file list is opened」「Leaving the adventure map」
   「reopened from a save」「Loaded the game from <路径>」「The saved game continues」「Drawing the 'AdventureMapScene'」。

**理由**：地图的 `Update` 里开读档窗口是安全的：`OnFrameEnded` 对 `_loadDialogOpen` 的帧照常计时并回放脚本输入
（ADR-241 第 4 条），所以脚本按键能在这条路上落地。把「谁要读档」与「怎么开读档屏」分开，场景不必知道主菜单存在。

**后果**：地图上按 L 答 YES 直接进存档列表，读到档就进那张图；取消列表停在主菜单。

**没做**：`Game::LoadGame()` 的 Standard / Campaign / Multi-Player 子菜单仍未拆（见 ADR-241），所以列表取消无法回到
「Load Game 子菜单」这一层；这一步不影响地图上的 L。

## ADR-244：野怪逃跑问句的两支，以及一路走出来的三个崩溃

**背景**：`ig3`。问句窗的 YES/NO 与点击在城堡、招募、盗贼公会那几处都验过（ADR-236 一带的证据），剩下没验的是
`HeroActions.ActionToMonster()` 里野怪「逃跑/加入」这一支。为了让英雄真的走到野怪格上触发它，把地图上的点击打通，
顺带撞出三个崩溃。

**三个崩溃与修法**：

1. **`MovePlayback` 起始索引写到 -1**（`BattleFieldPainter.cs:2165`）。构造时 `_stepIndex = -1` 紧接着
   `BeginStep()`，而 `BeginStep()` 的循环条件是 `_stepIndex < Steps.Count`，`-1` 照样进循环，`Current` 就去读
   `Steps[-1]`——**任何会走路的单位一动就抛 IndexOutOfRange**。改成 `0`：`BeginStep()` 的契约是「索引已经指向待播
   的那一步」，`NextStep()` 才是先 `++` 再调它；`Steps` 为空时 `0 < 0` 不成立，直接 `Finish()`，也是对的。
   之前的战斗冒烟没暴露它，是因为那场仗的兵都站在原地打完。
2. **英雄在路上阵亡后地图还在拿他算光标**（`AdventureMap.Turn.cs` 的 `AnimateHeroMovement`）。输掉战斗的人会被
   `Dismiss` 掉（`heroes.cpp:1959-2002`）：`MapIndex` 变 `-1`、移动状态清空。参考紧接着问 `hero->isAction()` 判
   整局胜负（`game_startgame.cpp:1521-1529`），输了最后一个英雄就回主菜单，走不到 `SetCenter()` 与 `updateCursor()`；
   端口还没有那一步，于是同一帧里 `SetCenter(-1)`，下一帧 `World.GetPath()` 以「The hero stands outside the map」
   拒绝站不在图上的英雄。修法：`hero.Move()` 之后发现人已离场就收工——不居中、不更新光标、不再动画，并调用
   `ResetFocus(GameFocus.Heroes, true)`；这个分支的注释本来就写着「当前英雄可能刚输掉战斗、已经不在英雄列表里」。
   整局胜负那一步仍然没做（见「没做」）。
3. **战斗规划器的移动命令结算时不可达**（`BattleArena.Action.cs:280`）。`BattlePlanner` 给出的 Move 命令在
   `ApplyAction` 里过 `CheckMoveParams` 时抛「The unit cannot move to that cell this turn」，九头骷髅那场约三次一遇。
   **没修**，登记为 `FHeroes2Sharp-6ms`；怀疑规划器读的可达格与 `BattleArena.Turns.cs:334` 烘进去的桥通行状态不同步。

**问句两支的验证**：新增 `NeutralMonsterFleeQuestionTests`（`tests/FHeroes2.Game.Tests`），五个格子的小地图上
10 农民对 1 农民（比值 10 > 5，人类英雄才判逃跑，`army.cpp:1725`），走 `HumanHeroMovementHandler.ActionNewPosition()`
这条真实入口：

- 答 YES：宿主收到的问句是 `HeroActionQuestion.PursueFleeingMonsters`，兵力是 1 农民，战斗真的打完、给经验，格子清空；
- 答 NO：同一句问句，经验不变、`AttackedMonsterTileIndex` 仍是 -1、格子也清空（`destroy = true` 那一支）。

问句窗本身（窗口、两个按钮、鼠标点击）由界面层的既有证据覆盖；这里补的是「拿到答案之后走哪一支」。

**没做**：
1. 整局胜负（`GameResult::checkGameOver`）：现在丢了最后一个英雄只是当场停下、把焦点重新挑一遍，不会判负回主菜单。
   这是独立的一步，排在 `6y3` 的后期。
2. `FHeroes2Sharp-6ms`：规划器与结算的可达格不同步，会让脚本化的野怪战斗约三次一崩一次；地图冒烟目前不会碰到它。

## ADR-245：移动命令被应用两次（`ApplyActionMove` 判「原地移动」非法）

**症状**：有观战者（`--map` 里点野怪开打）的战场上，AI 单位一行走就抛
`ArgumentException: The unit cannot move to that cell this turn.`（`BattleArena.Action.cs:280` 的 `CheckMoveParams`）。
`MerryMen.mp2` 那场脚本化战斗修前 8 次崩 4 次，无观战者的战斗冒烟一次都不崩——所以一直没被发现。

**取到的现场**（临时插桩打印）：

```
monster 'Skeleton' at head 40 tail -1, destination 40, reachable head 40 cost 0,
available 4..9,15..21,25..31,36..39,41,42,47...
```

目的地就是单位自己站的格子，`GetAllAvailableMoves()` 里也没有它（起点被排掉）。也就是说命令本身没错，是
**这个单位在命令被应用之前就已经站在终点了**。

**根因**：`BattleFieldPainter` 的 `MovePlayback` 每播完一格就 `unit.SetPosition(...)`（画面上要按单位当前格算插值
偏移），播到终点时模型已经在目的地。随后场景才把同一条命令交给 `BattleArena.ApplyAction`，`ApplyActionMove`
再校验一次就撞上 `unit.HeadIndex == destination`，`CheckMoveParams` 明写「原地移动不是移动」。参考里没有这个缝：
播动画与改位置都在 `moveUnit` 一个函数里（`battle_action.cpp` 的 `ApplyActionMove` → `moveUnit`），只在
`battle_interface.cpp` 的阻塞循环里逐格推进。端口把它拆成「先播（界面）、后结算（竞技场）」，两半就都动了手。

**修法**：`BattleArena.ApplyActionMove()` 在 `RefreshBridgePassability` 之后、`CheckMoveParams` 之前加一条：
单位已经站在 `destination` 就只补 `BattleMonsterState.Moved` 然后返回——另一半已经做完，不必也不该再走一次。
`CheckMoveParams` 本身保持严格（「原地移动」仍然是非法参数），只有「应用一条已经生效的移动」被容忍。

**测试**：`BattleArenaActionTests.AMoveTheAnimationAlreadyWalkedIsOnlyMarkedAsSpent`（把单位摆在终点再交 Move 命令，
断言位置不变、模式位有 `Moved`）。现场复跑：`--map MerryMen.mp2 --cursor 112,304 --left-click-at '40:112,304;60:112,304'`
连跑 10 次 0 崩溃（修前 4/8），`--battle-siege` 连跑 3 次也干净。

**留下的坑**（已由 ADR-246 修掉）：`qa6`。这条分支跳过的不只是「再走一次」，还有 `MoveUnit` 里那套吊桥收放
（`BattleArena.Action.cs:100-150` 的 `_bridge.ActionDown/ActionUp`），而界面层从不碰桥。于是有观战者的攻城场里
吊桥永远是升起的：本城单位出城没问题（升起的桥只对本城放行），攻方却进不了城门。参考把桥混在逐格循环里，
正确的补法是把 `NeedDown/ActionDown/ActionUp` 搬进 `MovePlayback.NextStep`。

## ADR-246：有观战者的攻城不放下吊桥（`qa6`）

**症状**：`--battle-siege` 里守城方从城里走出来时，吊桥一直挂在升起的状态。城里的兵照样能过（升起的桥只对本城
单位放行，`Bridge::isPassable`），攻方却永远进不了城门：每回合 `BattleArena.RefreshBridgePassability` 按
「桥是升的」重算闸门通行性，而没有任何东西把它放下来。

**根因**：放桥收桥写在 `Arena::moveUnit()` 的逐格循环里（`battle_action.cpp:333-350`）。ADR-245 之后，有观战者的
移动由 `BattleFieldPainter` 的 `MovePlayback` 一格格 `SetPosition` 走完，竞技场只在「已经站在终点」时补一个
`Moved` 模式位就走了——那条逐格循环整个被跳过，于是桥段跟着一起没了。界面层自己从不碰桥。

**修法**：把桥段从 `MoveUnit` 里抽出来，给两边共用：

- `BattleArena.LowerBridgeForStep(unit, cellIndex, tailIndex = -1)`：`Bridge::NeedDown` 成立就 `ActionDown()`；
- `BattleArena.RaiseBridgeAfterStep()`：`Bridge::AllowUp` 成立就 `ActionUp()`。

`MoveUnit` 的两个分支改为调用它们（行为不变），`MovePlayback` 在自己的那一格补上同样的调用：
`NextStep()` 里「放桥 → `SetPosition` → 收桥」，飞行单位则集中到新的 `Land()`（`FlyLand` 帧与 `Finish()` 都走它），
顺序与参考一致——先看落点（宽单位连尾巴）要不要放桥，落位之后再看能不能升起。

**测试**：`BattleArenaTests.AWalkedStepTurnsTheDrawbridgeAroundIt`——守城方站在闸门内侧，`LowerBridgeForStep` 之后桥
放下；站到闸门上时 `RaiseBridgeAfterStep` 不升（桥上有人）；走到闸门后面再升。

**验证**：全量测试 441 / 3462 / 1779 全过；`--battle-siege` 连跑 3 次干净；`SmokeBattleToMap.ps1` 通过。

**没做**：吊桥升降的**动画帧**。参考的 `Bridge::ActionDown/ActionUp` 是带逐帧重绘的阻塞动画，端口的
`BattleBridge.ActionDown/ActionUp` 只改状态，桥会瞬间换图。要让那段动画也同步进来，得给播放加一个「桥步」，
排在行军步之间——等攻城的美术核对时再一起做。

## ADR-247：战役开局的表头准备（`nh9.1`）

**背景**：`campaign_data.cpp:697-747` 的 `updateScenarioGameplayConditions()` 早就搬好了（`CampaignData.UpdateScenarioGameplayConditions`）
也有测试，缺的是**谁来调**。参考只有一个调用点：`game_campaign.cpp:1604-1615`，`Dialog::CampaignInfo` 的 OK 分支里
`scenario.loadMap()` 之后——先修法语字符，再补联盟，然后 `conf.setCurrentMapInfo()`。

**问题**：端口还没有战役主流程（`nh9.3`），所以这段接线没有现成的落点。选项是等 `nh9.3`（那样 `nh9.1` 就只能空转），
或者先把「读图 + 准备表头」合成一个入口，让 `nh9.3` 只能照它写。选了后者——项目一贯的做法是先立接口、后接调用方。

**做法**：

- `CampaignData.PrepareScenarioMap(scenarioInfoId, mapInfo, interfaceLanguage, resourceLanguage)`：参考那两句的合并版。
  法语字符那一步照参考的三重门槛——地图不是 `RESURRECTION` 格式、**界面**说法语、**资材**也是法语版，缺一不可；
  然后接既有的联盟合并。两个语言做成参数而不是内部读全局，参考在调用点读的也是这两个全局量，做成参数才测得动。
- `ScenarioData.LoadPlayableMap(rootDirectories)`：`LoadMap()` + 上面那步，并在里面取 `GameLanguage.Current` 与
  `ResourceLanguage.Get()`。参考 `loadMap()` 只有这一个调用者，紧跟其后的就是这两步，所以合成一个入口不会漏。

**测试**：`CampaignTests.APreparedScenarioMapBringsTheAllianceAndTheFrenchAccents`（`C#te` → `Côte`，两个网家并成一方）；
`TheFrenchFixNeedsBothLanguagesAndALegacyMap` 三条反例（界面英文 / 资材英文 / 新格式地图，文本都不动，联盟照补）。

**没做**：
1. `LoadPlayableMap` 还没有调用者，等 `nh9.3` 的战役主流程。它只有两行（读 + 准备），两块都被单测盖住了；
   要给它补端到端测试得先在测试工程里造一份可读的 `.h2c`，`TemporaryMap` 现在只在资材测试里是 `internal`。
2. 战役地图（`CAMPG01.H2C` 那一批）不在数据目录里，本机只装了 `.mp2` 地图，跑不到真实战役图。

## ADR-248：战役存档的派生字段（`nh9.2`）

**核对**：`campaign_savedata.cpp:165-210` 的 `operator<<` / `operator>>` 与端口 `CampaignSaveData.Write/Read` 逐字段对过，
次序、版本分支（1.1.11.1 之前的天数摊平、1.1.08 之前没有最小难度）、`resize` 补齐全都一致；`GameSave` 在
`IsCampaignGameType` 时也早就把这一段写进 `.savc` / 读回来。所以序列化本身没有缺口，缺的是文件尾部的三个派生函数。

**补上**（`CampaignSaveData` 的实例方法，对应参考里 `Campaign::` 命名空间的三个自由函数）：

- `GetCurrentScenarioVictoryCondition()`（`campaign_savedata.cpp:212-227`）：按存档里的关卡编号去战役表取胜利条件。
  编号是读回来的，手改过的存档会越界，参考此时断言并退回 `STANDARD`，端口照做（`Debug.Assert` + 三目）。
- `GetCurrentScenarioLossCondition()`（`229-244`）：同上。
- `GetCurrentScenarioDifficultyLevel()`（`246-268`）：原版把十关的地图难度写死在 `adjustedDifficultyLevels` 里
  （罗兰/阿基巴德各 1 关、Descendants 2 关、巫师岛 1 关、回乡 1 关、忠诚的代价 4 关）。表外的关返回
  `null`，调用方用地图表头自己的难度。

**测试**（`CampaignTests`，4 个新测试）：

- `CampaignSaveDataRoundTripsThroughAStream`：写一遍再读回来，逐字段比对（含五格携带部队的号位形状）。之前只有
  `ScenarioInfoId` 有来回测试，战役状态本身没有。
- `TheCurrentScenarioDrawsItsConditionsFromTheCampaignTable`：罗兰第三关（丢光女巫村就输）、第八关（拿终极王冠才赢）。
- `TenScenariosHardCodeTheirMapDifficulty`：十条例外逐条表格化。
- `ScenariosWithoutAHardCodedDifficultyKeepTheMapOne`：表外的关拿到 `null`。

**踩到的坑**：`CampaignSaveData` 自己有个 `CampaignId` 属性，类内的字段初始值设定项里写 `CampaignId.Roland` 会被解析成
那个属性（CS0236）。给常量类加了个 `CampaignIds` 别名。

**没做**：这三个函数现在没有调用方——胜负判定（`game_startgame.cpp` 的 `GameResult`）与地图难度的选择分别属于
`nh9.3` 与 `6y3` 的收尾，到时候接上去即可。

## ADR-249：战役主流程拆成五块，先落奖励那一块（`nh9.3`）

**背景**：`game_campaign.cpp` 一共 1170 行，去掉匿名命名空间里的绘制与界面壳，真正属于「流程」的是两段：选关时把玩家挑的奖励
落到人和王国身上（`SetScenarioBonus` / `applyObtainedCampaignAwards` 及其三个辅助函数，501-780），以及选关界面与
`CompleteCampaignScenario` 的状态推进。前者是纯数据动作、能单独测；后者要界面、原版按钮与图标、以及 App 的场景切换。
一口吃不下，按能独立验收的边界拆成五块：

1. **奖励与奖励物的落账**（本轮完成）：`CampaignBonuses`。
2. 选关流程的状态推进：`SelectCampaignScenario` 的「点哪个图标 = 下一步干什么」与 `CompleteCampaignScenario` 的
   「打完一关怎么记账、什么时候算战役结束」。
3. 选关界面的绘制与按钮：`DrawCampaignScenarioIcons` / `addScenarioButton` / `DrawCampaignScenarioDescription` /
   `drawCampaignNameHeader` / `drawObtainedCampaignAwards` / 影片与音乐。
4. 新战役入口与难度选择：`NewSuccessionWarsCampaign` / `NewPriceOfLoyaltyCampaign`（在 `game_newgame.cpp`）、
   `setCampaignDifficulty`、App 场景接线与读档分支（`--campaign` 之类）。
5. TextSupport 模式的战役输出：`outputCampaignScenarioInfoInTextSupportMode`。

**本轮做了第 1 块**，新文件 `src/FHeroes2.Game/Campaign/CampaignBonuses.cs`：

- `SetScenarioBonus(world, scenarioInfoId, bonus)`：只给人控制的玩家加；八种奖励（资源、宝物、兵、法术、起始种族、
  起始种族兼兵表、主技能、副技能）逐条对应 `game_campaign.cpp:605-702`。
- `ApplyObtainedCampaignAwards(world, currentScenarioInfoId, awards)`：只兑现「开局立刻要生效」的四类（宝物、法术、
  消灭指定敌方英雄、携带部队）。奖励物从它自己的 `StartScenarioId` 那一关起生效，早于它的关卡跳过。
- `ReplaceArmy` / `SetHeroAndArmyBonus` / `GetHeroToApplyBonusOrAwards`：后两个是参考的私有辅助，端口保持私有。

**踩到的三个坑**（都写进了测试的注释）：

1. `Army.Reset()` 不是「清空」。参考里「没有携带部队」时调的就是 `Army::Reset()`，那是「给一个一级兵」——端口
   `Reset(defaultArmy: false)` 语义一致，所以断言要的是「一个一级兵、数量 1」，不是空军队。
2. 王国的开局资金不是 0（难度不同数量不同），测资源奖励只能比增量。
3. 地图生成时英雄自带一队兵，测试里要先 `Army.Clean()` 才能看清奖励自己动的手。

**没做 / 已知偏差**：

- 宝物奖励里卷轴携带的法术（`Artifact::SetSpell()`）：端口的宝物栏槽位只存 `ArtifactType`，没有地方放「这一份卷轴
  携带哪个法术」，所以只发卷轴本体。这与 `ArtifactScroll` 的既有说明同源，不是本轮新欠的债。
- `GetHeroToApplyBonusOrAwards` 的前两张表（按头像定人、按种族定人）没测到：地图里造不出战役专属英雄，种族表要的
  术士英雄也没有稳定的编号入口。表是照参考逐条抄的，等第 2 块接上流程时用真实存档验。

## ADR-250：战役记帐与叛变关判定（`nh9.3` 第 2 块）

按 ADR-249 的五块拆分，本轮做第 2 块「选关流程的状态推进」，再把纯逻辑的部分落成两个可测单元：

**`CampaignProgress.CompleteScenario(world, isLoadingSaveFile)`**——`CompleteCampaignScenario()`（`game_campaign.cpp:1160-1282`）
里只管记帐的那一半：

- `isLoadingSaveFile` 为假时记完成记录、加天数；非最后一关才需要写「下一关的存档」。
- 逐条发这一关的奖励物：`DefeatEnemyHero` 跳过（必须在战斗里发），`CarryOverForces` 先抄人类王国里最老的英雄
  （`Kingdom.Heroes[0]`，与参考 `VecHeroes.front()` 同序），`GetArtifact` 触发组套。
- 组套照参考：把「已拿到的全部宝物奖励」摊进临时背包、从存档摘掉、`AssembleArtifactSets()`，再把背包里剩下的宝物
  按编号找回奖励编号加回去。额外奖励表（`GetExtraCampaignAwardData`）提供成品（战斗装束）的编号，所以三件安德兰
  会变成 `10`。
- 最后一关 → 战役结束；否则把当前关推进到 `GetScenariosAfter(last).front()`。

**返回值 `CampaignScenarioCompletion`**：参考里「非最后一关就存一次档」是 `Game::SaveCompletedCampaignScenario()`，
端口没有写文件的责任，改成 `ShouldSave` 交回调用方（等 App 接线的第 4 块）。

**`CampaignProgress.GetSelectableScenarios(saveData, allowToRestart)`**——`SelectCampaignScenario()`（`1471-1478`）
里「哪几关可点」的三分支，界面那一块直接拿来画图标。

**`CampaignData.IsBetrayalScenario(ScenarioInfoId)`**——参考放在 `game_campaign.cpp:122-132` 的匿名命名空间里，但它
只查战役表、与界面无关，都放在 `CampaignData`（界面与流程都要用）。判定为「后续关卡跨了战役号」，即罗兰第十一关
「Betrayal」与阿基巴德第十二关。

**测试**（`CampaignProgressTests`，7 个）：叛变关正反例、完成一关加天数并推进、最后一关结束且不写档、读档不重复记帐、
携带部队取最老英雄的部队、三关打完后合出战斗装束、可点关卡三分支。

**踩到的坑**：这三个战役测试类共用两个进程级单例（`CampaignSaveData.Get()`、`Settings.Get().Players`），xUnit 默认
按类并行，单跑各自的类都过、全量一起跑就有 6 个倒在别人的中间状态上。加 `CampaignCollection`（`DisableParallelization
= true`）把三个类收进同一个集合，连跑两遍确认稳定。

## ADR-251：战役选关界面的分层（`nh9.3` 第 3 块）

按 ADR-249 的五块拆分，本轮做第 3 块：`SelectCampaignScenario()`（`game_campaign.cpp:1284-1710`）里「画出来 + 按钮」
的那部分。参考是一个 420 行的死循环，端口按既有惯例把它切成不含循环的几块：

- `src/FHeroes2.UI/Dialogs/CampaignScenarioTrack.cs`：关卡轨道。`Create()` 决定哪些关可点（用第 2 块的
  `CampaignProgress.GetSelectableScenarios()`）、把图标按 `getCampaignIconOffsets()` 的格子摆好（一格 37 × 42 像素，
  格坐标从 `addScenarioButton()` 的 184-251 行抄）、`HitTest()` 报「点到了哪一关」、`DrawButtons()` 画按钮、
  `Press()` 把一关的按钮按下去。
- `src/FHeroes2.UI/Dialogs/CampaignScenarioSelectionDialog.cs`：背景、五个按钮（VIEW INTRO / OK 或 RESTART /
  CANCEL / DIFFICULTY）、奖励单选列、天数、关卡名、描述、已得奖励。对外只有
  `Draw(PixelBuffer)` / `PressButtons(LocalEvent)` / `ProcessLeftClick(LocalEvent, HotKeys)` /
  `ProcessRightClick(LocalEvent)` 四个动作，淡入淡出、影片、音乐与循环都留给宿主（App 场景，第 4 块）。

**奖励单选怎么在没有 `OptionButtonGroup` 的情况下做**：参考用一个互斥按钮组，按下哪个就 `press()` 哪个，未按下的
按钮贴的是「从背景图上按按钮 anchor 挖下来的一块」（`game_campaign.cpp:1382` 的 `Copy`）。端口没有按钮组，改成
「当前选中下标 + 一张从背景挖下的未按下贴片」：选中的行贴 `CAMPXTRG` 第 8 帧（按下态），其余行贴那块背景。
按下即生效（参考用的就是 `isMouseLeftButtonPressedInArea`，不等抬起），点击区是按钮区域再往左扩 170 像素，
所以点奖励名称和点那块小按钮是一回事。

**右键不弹窗，只报「点到了哪一块」**：参考在循环里直接 `displayScenarioBonusPopupWindow()` /
`displayScenarioAwardsPopupWindow()`。端口把弹窗留给宿主（弹窗要字体与美术装配），界面层返回
`CampaignSelectionHelpRequest(Target, Index)`：`Index` 是奖励选项或已得奖励的行号，其它块为 -1。已得奖励的行也是
一块一块报的（参考的 `{ top.x + 414, top.y + 100 - yOffset / 2 + yOffset * i, 200, yOffset }`），不是整片。
天数、关卡名各一块。

**按钮细节**：五个按钮同高、同 y（`top.y + 431`），x 由「VIEW INTRO 左边缘到 CANCEL 右边缘的剩余空间三等分」推出；
OK 与 RESTART 是同一个位置的两张脸（新开局 = 第 4 帧，重开 = 第 2 帧），地图文件缺失时 `disable` 而不是隐藏。
影子用的是第 0 帧的轮廓、摆放点各用各的（参考对四个按钮都传 `getImage(buttonIconID, 0)`）。

**两处与参考不同的地方，都是为了让端口不炸**：

1. 点「当前这一关」不再重新 `setCurrentScenarioInfo()`。参考会顺手把存下来的奖励编号清成 -1；重开界面上点自己
   没有意义，端口直接返回 `None`。
2. 新战役还没选第一关时存档里是 `ScenarioInfoId.NotStarted`（`{-1, -1}`），参考照样拿它去查「叛变关」；端口的
   战役表对未知战役断言，所以 `ObtainedAwards` 在这一状态下直接给空表。

**踩到的坑**：先前以为按钮条是「一张图竖排十张」的合成图、要按 `index * (width + 1)` 手工切帧；实际现在参考已经
是 `Assets::getImage( buttonIconID, index )` 的十帧集合，端口的 `ICampaignScenarioArtwork.GetIcon()` 也按帧给，
所以切帧那套代码删掉了。

**测试**（`CampaignScenarioSelectionDialogTests`，16 个）：新开局预选第一个奖励、重开按存档里的奖励编号预选（含
编号越界给 -1）、奖励行 22 像素步进、新开局画 OK / 重开画 RESTART（取色）、只给选中的奖励贴按下帧、地图缺失时
确定键点不动、热键与按钮同语义、重开时 OK 隐藏而 RESTART 生效、右键报奖励/已得奖励/天数/关卡名、叛变关不显示
已得奖励、老战役的关卡名帮助区 25 高而资料片 22、点另一关会记进存档。全量 `FHeroes2.UI.Tests` 1795 个与
`FHeroes2.Game.Tests` 3496 个都通过。

## ADR-252：新游戏菜单（`nh9.3` 第 4 块之一）

`nh9.3` 第 4 块的第一件东西是战役的入口，也就是参考的 `Game::NewGame()`
（`game_newgame.cpp:416-648`）：主菜单上点 New Game 出来的那个子菜单，标准游戏 / 战役 / 多人 /
仅战斗 / 设置 / 取消六条路，战役那条再分原版与资料片。端口把它做成
`src/FHeroes2.UI/Dialogs/NewGameMenuView.cs`。

**状态机不靠「哪组按钮 enabled」猜**：参考把四组按钮同时建好，用 `while` 里
`if (buttonStandardGame.isEnabled()) ... else if (playerCountButtons.button(0).isEnabled()) ...`
的阶梯判断当前在哪个界面。端口用显式 `Page`（`Modes` / `HotSeat` / `PlayerCount` / `Campaigns`）
四态，切页时 disable 旧组、enable 新组，外加 `IsChoosingCampaign` 等三个只读属性——语义与参考一致，
但宿主不用去问按钮。

**布局全按参考的算式**：窗口由五个主按钮撑出（`CreateForSymmetricButtons(singleColumn: true,
extraHeight: 按钮高 + 10)`），单列从活跃区 (25,22) 开始、行距 10；取消按钮在
`activeArea.y * 2 + activeArea.height - 按钮.y - 按钮.height` 处，就是「与第一个按钮距顶边等距地贴底边」
（`game_newgame.cpp:467`）。热座按钮盖在第一个主按钮上，两个战役按钮盖在前两个主按钮上，五个玩家人数
按钮从第一个主按钮起按「按钮高 + 10」往下叠。帧表就是归档里的原顺序：0-9 五个模式、10/11 取消、
12/13 热座、14-23 人数、24-27 两个战役，都是「先松后按」成对。

**`ButtonGroup` 只借来排版和画**：它的 `SetCursor`/`SetLeftButton` 靠按钮的 `DialogButton` 值判点击，
而新游戏菜单要回答的是「第几个按钮」，不是 `DialogButton` 值。端口给五个主按钮填合法但不读的占位值
（`Ok/Cancel/Yes/No/Ok`，避免 `FindClicked()` 撞上同一值），点击判定自己按 `ButtonBase.Area` 做：
按下记下标并 `Press()`，抬起时同一按钮内才结算，随后按页 `Activate(index)`。切页和取消都在
`Activate`/`SetLeftButton` 里，与参考「先处理当前组、最后统一处理取消」的顺序一致。

**热键单独列一套**：参考用 `HotKeyEvent::MAIN_MENU_STANDARD` 等，端口没有热键表，照
`CampaignSelectionHotKeys` 的做法定义 `NewGameMenuHotKey`，只在对应页生效（参考把热键判断写在
「当前组 enabled」的分支里），`Cancel` 全局。主菜单热键解码后交给宿主。

**右键只报块**：参考直接 `showStandardTextMessage()`，端口返回 `NewGameMenuHelpRequest`
（标准/战役/多人/仅战斗/设置/热座/原版/资料片/人数第几个/取消），由宿主弹窗——与第 3 块的
`CampaignSelectionHelpRequest` 同一套理由，弹窗要字体与美术装配。

**还没接线的部分**：主菜单的 New Game 仍然是直接开单人场景列表（M3 的简化）。本轮只落地了这个子菜单
本身，接线（主菜单改成 NewGameMenuView、战役难度对话框、战役选关场景、战役开局与读档分支）留给第 4 块
的下一段，见 `bd` 上的 `FHeroes2Sharp-nh9.3`。

**测试**（`NewGameMenuViewTests`，24 个）：缺帧返回 null、五个主按钮的单列位置、取消按钮的镜像位置、
热座与两个战役按钮叠在主按钮上、没有资料片时没有战役按钮、没有原版战役数据时战役键 disable、
初始停在模式页、标准/仅战斗/设置三条路、抬起落在别的按钮上不算、有资料片时点战役进选择页、
没资料片时直接返回原版战役、选择页两个按钮分别返回原版/资料片、多人 → 热座 → 人数三级下钻并返回
人数 2-6、每页都能取消、disable 的战役键点不动、热键按页生效（战役键切页、人数键 2/6、取消全局）、
右键块判定（模式页、disable 的战役键、战役页、人数页）、`Draw` 画出当前页与取消按钮。
全量 `FHeroes2.UI.Tests` 1819 个通过。

## ADR-253：新游戏菜单的接线与素材组装（`nh9.3` 第 4 块之二）

**难度窗口先落地**：`setCampaignDifficulty()`（`game_campaign.cpp:900-1130`）做成
`src/FHeroes2.UI/Dialogs/CampaignDifficultyView.cs`，`TryCreate(frame, font, captionFont, icons,
selection, okayReleased, okayPressed, currentDifficulty)` 在素材不全时返回 `null`。三档图标
（`DIFFICULTY_ICON_EASY/NORMAL/HARD`）、选择框、黄字标题和 `NGEXTRA` 62 的 Okay 按钮，位置全按参考的
`CreateForSymmetricButtons` 算式。选择框是两层 Sprite：图像整片填色，`transform` 全 1（保留目标像素），
边框一圈改 0——单层 Sprite 的 `null` 变换等于「全部拷贝」，表达不了这个镂空。它还没接进战役流程，
因为流程本体（开局、选关、读档分支）排在下一段。

**素材在 App 层组装**：`ICN::BUTTONS_NEW_GAME_MENU_GOOD/EVIL` 是参考运行时生成的表
（`game_assets.cpp:1657-1710`），AGG 里没有。端口新增 `Scenes/NewGameMenuArtwork.TryCreate(assets,
text, isEvilInterface)`：`BTNNEWGM.ICN` 出标准/战役/多人/取消，`BTNMP.ICN` 出热座，`BTNHOTST.ICN` 出人数，
`X_LOADCM.ICN` 出两个战役按钮，仅战斗与设置用 `ButtonFactory.MakeButtonSprites` 以 `STONEBAK` 为底生成
（与参考同一套 `makeButtonSprites`）。坏界面不单独生成，照参考把好界面整表过
`GOOD_TO_EVIL_BUTTON`（`convertToEvilButtonBackground`），所以先生成好表再逐帧 `Clone()` 上色。
缺 `X_LOADCM` 时 24-27 帧填透明占位——视图在 `isPriceOfLoyaltyPresent == false` 时不会画它们。

**接线做成场景级模态**：`MainMenuScene.OpenNewGameMenu()` 组装帧并调
`NewGameMenuView.TryCreate`，`NewGameMenu`/`NewGameTip` 两个只读属性暴露给宿主，`HasModalDialog`
把两者算进去；光标、左键、字符键、右键四个入口都排在设置窗口之后、其它模态之前（设置可以从新游戏
菜单里开）。`MainMenuAction.NewGame` 改成先开新游戏菜单，素材缺失时退回原来的直接开场景列表，坏安装
仍有可玩路径。标准游戏 → 关菜单再开场景列表并返回 `NewGame` 给宿主；设置 → 关菜单再开设置窗口
（参考设置窗口返回后回主菜单，不回新游戏菜单）；取消/Escape → 关菜单。仅战斗、热座人数与两个战役
仍只写日志与状态栏，等各自的场景。

**战役可用性照参考的两层检查**：原版看 Archibald 与 Roland 地图是否齐；资料片还要 `X_LOADCM.ICN` 在
包里且四套地图都在（`IsAllCampaignMapsPresent`，对应 `game_campaign.cpp:1141-1157`）。

**右键帮助**：视图返回 `NewGameMenuHelpRequest`，场景按块弹 `MessageBox`（标题 + 参考原文），
按下弹出、松开消失，即参考的 `Dialog::ZERO`。帮助框开着时按钮不亮，光标归它。

**验证**：`--open-new-game` 新增命令行开关（与 `--open-settings`/`--open-scenario` 同用途），用
`F:\game\fh2` 截图核对模式页、战役页、Standard Game → 选关列表、右键帮助框四张；全量 6068 个测试通过。

## ADR-254：战役选关场景接入与 StartWithHeroInFirstCastle（`nh9.3` 第 4 块之三）

**选关屏接进宿主**：`CampaignScenarioSelectionScreen.TryCreate(assets, text, fonts, saveData, mapDirectories,
screenSize, allowToRestart, out error)` 由 `GameHost.RunCampaignScenarioSelection` 驱动，做成场景级模态。
`--campaign <id>` 只登记 `_pendingCampaign`，主循环首帧再开——与 `--cast-spell` / 梦魇谜题同一条延迟路径，
避免在事件栈里再压一层循环。动作照 `SelectCampaignScenario()` 分：Cancel 退出、ChooseDifficulty 开难度窗、
ViewIntro 播当前关的 `CampaignVideoInfo`、StartScenario 进地图、其余（SelectOtherScenario）重建一屏。

**开局照参考顺序**：`StartCampaignScenarioMap` 先 `scenario.LoadPlayableMap(mapDirectories)`，再
`Settings.SetCurrentMapFileInfo` 与 `SetGameType(GameTypes.Campaign)`（存档扩展名与存档头里的游戏类型靠它分家，
`game_io.cpp:227-231`），最后 `AdventureMapScene.TryCreate` + `CampaignBonuses.SetScenarioBonus` +
`ApplyObtainedCampaignAwards`。难度窗按 `setCampaignDifficulty()` 落到 `CampaignSaveData.SetDifficulty(d,
resetMinDifficulty: true)`。

**开局没有英雄的根因**：`CAMPG01.H2C` 的头部 `startWithHeroInFirstCastle = 0`（真值），而地图上既没有
`MP2::OBJ_HERO` 瓦片也没有 `MINIHERO` 精灵——原版编辑器做战役地图时常把「开局给英雄」写在头部。参考在
`world_loadmap.cpp:1456` 装载末尾调 `vec_kingdoms.ApplyPlayWithStartingHero()`（`kingdom.cpp:701`），
端口有 `Kingdom.ApplyPlayWithStartingHero` 却**从没有调用点**，于是地图开出来 0 英雄、焦点英雄为空。
补齐 `Kingdoms.ApplyPlayWithStartingHero(world, startWithHeroInFirstCastle)` 并在 `World.LoadMap` 的
`PostLoad` 之后调用；两条分支照抄（城门口的己方英雄进城 / 第一个有城的王国 `GetHeroForHire(城种族)` 现招）。
修完日志从 `0 heroes in the kingdom, no focus hero` 变成 `1 hero in the kingdom, focus hero 'Halon'`，
城堡门口站着蓝方法师英雄。

**难度窗素材补全**：`DIFFICULTY_ICON_EASY/NORMAL/HARD` 不是档案条目，是 `ScenarioInfoIcons` 从 `NGHSBKG`
第一帧裁的 65×65 小块（`game_assets.cpp:3993-4042`），按它给的缓存名取图即可（用 `IcnType` 查会落到
`CUSTOM`）。`BUTTON_SMALL_OKAY_GOOD/EVIL` 走端口既有惯例 `SPANBTN`/`SPANBTNE`，再没有才用
`ButtonFactory` 现拼。三张图标缺了用同尺寸透明图（参考缺资源也是空图），只有 `NGEXTRA` 62 帧的选择框缺了
才返回 `null`——玩家看不出选中哪一档时窗不如不开。

**验证**：`--campaign 0 --left-click-at "4:300,440;6:300,170;8:320,352"` 走完「难度窗 → 改档 → 确认 →
选关 → 开局」；截图 `f:\tmp\campaign-difficulty3.png`（图标、选择框、OKAY 全在）与 `f:\tmp\campaign-start2.png`
（蓝方城门口的英雄）。新增 `tests/FHeroes2.Game.Tests/WorldStartingHeroTests.cs` 三个用例：头部置位现招、
不置位不招、城门口英雄进城。全量 6071 个测试通过、0 警告。

## ADR-255：战役一关打完之后的结算与去向（`nh9.3` / `nh9.4`）

**接线**：`AdventureMapScene.HandleResult` 收到 `AdventureEventResult.Win`（`GameOverMode.CompleteCampaignScenario`
的端口名）时不再只记一行日志，而是转交给 `CampaignScenarioCompleted`；`GameHost` 把它装配成
`CompleteCampaignScenario()`。场景只转达、不换场景，与 `ExitEvent` / `LoadGameEvent` 同一条分工。

**四步按参考顺序**（`game_campaign.cpp:1160-1282`）：

1. `CampaignProgress.RecordFinishedScenario(world)`：本关写进已完成列表、累加天数，返回「是否还有下一关」。
   调用后 `CurrentScenarioInfoId` 仍指着刚打完的这一关，而 `GetLastCompletedScenarioInfoId()` 已经指向它。
2. `GameSave.SaveCompletedCampaignScenario()`（`game_io.cpp:419-422`，文件名 `地图名_Complete.savc`）：
   **必须夹在 1 与 3 之间、且在 `ReturnToMenu()` 之前**——前半句是因为 `game_io.cpp:261-265` 靠
   `current == lastCompleted` 认出「这一关打完了」，顺序写反就算不出这个状态；后半句是因为文件名与存档头
   都取自 `Settings.CurrentMapFileInfo` 与 `Settings.GameType`，一换场景就变。
3. `CampaignProgress.CollectScenarioAwards(world)`：发这一关的奖励与携带部队。参考也把它放在写档之后，
   所以「下一关」存档里不含本关奖励，读档时补发（见下）。
4. `CampaignProgress.AdvanceScenario(shouldSave)`：最后一关返回「已结束」，否则把当前关挪到
   `GetScenariosAfter().front()`（`:1279-1281`）。非最后一关回选关屏：端口用 `_pendingCampaignSelection`
   排到主循环下一帧，与 `_pendingCampaign` 同一条「等这一帧画完」的理由；最后一关在参考里进片尾与战役高分榜
   （`:1242-1277`），属于 P5 结局屏，端口先停在主菜单。

原来合在一起的 `CampaignProgress.CompleteScenario(world, isLoadingSaveFile)` 保留为不写档的便捷入口（三段串联），
测试与只关心最终状态的调用方用它；`GameHost` 走四段，因为它必须把写档插在中间。

**读档那一支**：`GameHost.OpenLoadFile` 在 `GameSave.Load` 之后照 `game_io.cpp:261-265` 判一次
`GameType == Campaign && !IsStarting && CurrentScenarioInfoId == GetLastCompletedScenarioInfoId()`，命中就补发奖励
（`CollectScenarioAwards`）、`AdvanceScenario(shouldSave: false)`、`ReturnToMenu()` 并排一帧选关屏，
即参考的 `COMPLETE_CAMPAIGN_SCENARIO_FROM_LOAD_FILE` → `CompleteCampaignScenario(true)`
（`game_mainmenu.cpp:208-213`）。普通中途存档（`IsStarting`，或已打完前一关但当前关不同）不受影响。

**冒烟开关**：`--complete-campaign-scenario` 在地图建立后立即宣布本关打赢。战役地图的胜利条件通常要打几十个
回合，截图与冒烟等不起；它走的仍是真实的结算、写档与回选关屏，只是跳过战斗，与 `--enter-town` 同类。

**验证**：

- `--campaign 0 --complete-campaign-scenario --left-click-at "4:300,440;8:320,352;14:402,440"`：日志出现
  `Saved the game to '…\Good_1_Complete.savc'` 与 `next scenario … ScenarioId = 1`，截图
  `f:\tmp\campaign-complete2.png` 停在选关屏的 Scenario 2「Annexation」（Scenario 1 打勾、Days Spent 1）。
- `--load "…\Good_1_Complete.savc"`：日志 `ends a campaign scenario, so the campaign returns to its scenario list`，
  截图 `f:\tmp\campaign-from-load.png` 同样是选关屏而不是旧地图。
- 新增 `TheInterScenarioSaveIsNamedAfterTheMap`（文件名与目录）与 `TheInterScenarioSaveHoldsTheScenarioThatWasJustFinished`
  （写档时 `current == lastCompleted`，顺序写反就红）。全量 6073 通过、0 警告。

**未接**：上一关的过场影片（`playPreviousScenarioVideo()`，`game_campaign.cpp:1240`）、片尾与战役高分榜
（`:1242-1277`）。前者要视频播放，后两者属于 P5 结局屏。
## ADR-256：战役开局的奖励三选一接到世界（`nh9.3`）

**接线**：选关屏早就画出了 CHOICE 三行并且能切换选中态（`CampaignScenarioSelectionDialog.SelectedBonusIndex`），
但宿主一直把它丢掉——`StartCampaignScenarioMap` 从存档读 `CurrentScenarioBonusId`，而那个值在开局前恒为 -1，
于是奖励永远不生效。现在 `CampaignSelectionAction.StartScenario` 把 `SelectedBonusIndex` 传进
`StartCampaignScenarioMap(saveData, mapDirectories, bonusIndex)`，顺序照 `game_campaign.cpp:1631-1671`：

1. **起始种族在 LoadMap 之前**（`:1631-1635`）：`StartingRace` / `StartingRaceAndArmy` 先按
   `CampaignBonuses.SetStartingRaceBonus()` 改玩家种族——地图一读进来，玩家阵容与英雄就按这个种族定了。
2. **叛变关先清空奖励**（`:1638-1640`）：`CampaignData.IsBetrayalScenario` 命中时 `RemoveAllAwards()`。
3. **LoadMap**（原有路径，`AdventureMapScene.TryCreate`）。
4. **其余奖励在 LoadMap 之后**（`:1665-1669`）：`ApplyCampaignStartBonuses` 接收 `bonusIndex` 与本帧算出的
   `ScenarioBonusData`，跳过纯 `StartingRace`（第 1 步已设），其余交给 `CampaignBonuses.SetScenarioBonus`；
   接着照旧 `ApplyObtainedCampaignAwards` 发前几关攒的奖励。
5. **最后才记账**（`:1671`）：`saveData.SetCurrentScenarioInfo(scenarioInfo, bonusIndex)`，这样
   `_bonusesForFinishedMaps` 记的是玩家这次的选择。参考也放在最后，读地图失败时存档不会被写脏。

**顺带修掉的真 bug**：`CampaignSelectionHotKeys.Bonus` 原来是 `int Bonus = -1`，但它是 `readonly record struct`，
`default(T)` 是「所有字段全零」而不是构造参数的默认值，也就是 `Bonus == 0`。选关屏每帧末尾都
`_hotKeys = default`，于是**每一帧都假装第一项奖励被按下**，玩家点或按到第二、三项后立刻被打回第一项——
真人玩也一样（不只是冒烟脚本）。改成 `int? Bonus = null`（零值即 `null`，与「按第一项」区分开），
并补测试 `APickedBonus_SurvivesFramesWithoutHotKeys`。冒烟之所以一直显示 `bonus 0`，根因就是这个，不是点击通道的问题。

**可观测性**：选关屏建好后打一行 `The campaign selection shows scenario '...' with bonus areas [...]`，
冒烟要精确点中单选按钮时不用再猜坐标（与主菜单的 `signs at [...]` 同一性质）。

**验证**：

- `--campaign 0 --key-at "2:Digit2;6:Enter"`：日志 `Started the campaign scenario 'Force of Arms' (campaign 0,
  scenario 0, bonus 1 'Thunder Mace')`——数字键 2 选中第二项，Enter 开局，编号一路带到世界。
- `--key-at "2:Digit3;6:Enter"`：截图 `f:\tmp\campaign-bonus6.png`，地图与英雄正常登场。
- 新增与既有 UI 测试全绿；全量 6074 通过、0 警告。

**未接**：

- 换难度时的确认弹窗（`game_campaign.cpp:1589-1596`）：端口目前直接改难度。
- `allowToRestart = true` 的入口（`interface_events.cpp:378` 的 Scenario Information，即地图里打开选关屏并允许
  RESTART）；参考的主菜单与读档两条都是 `false`，所以选关屏仍固定 `allowToRestart: false`。
- 重开确认弹窗（`:1598-1602`）与`上一关过场影片`（`:1240`）。
## ADR-257：战役影片接上主流程（`nh9.3`）

**问题**：`ScenarioData` 的 `StartScenarioVideoPlayback` / `EndScenarioVideoPlayback` 早就随战役数据表落了地，
但没有任何地方去播——`PlayCampaignScenarioIntro` 是唯一入口（选关屏的 View Intro），而且它只放本关开场，
漏了上一关的结尾。参考有三处（`game_campaign.cpp`）：

| 参考 | 时机 | 端口 |
|---|---|---|
| `:1305-1307` | 进选关屏（`!allowToRestart`）先放本关开场 | `RunCampaignScenarioSelection` 的循环之前 |
| `:1675-1689` | View Intro：先上一关结尾，再本关开场 | `CampaignSelectionAction.ViewIntro` 分支 |
| `:1240` | 打完一关、发完奖励后放这一关的结尾 | `CompleteCampaignScenario()` 里 `CollectScenarioAwards` 之后 |

**分层**：「要播哪几个文件」放进 Game 层的 `CampaignVideoPlayback`（`GetPreviousScenarioVideoFiles` /
`GetCurrentScenarioVideoFiles`），界面层只负责 `PlayVideo()`。这样清单可以用单元测试钉住
（`CampaignVideoPlaybackTests`：新战役只有开场、打完第二关才有 `GOOD03.SMK`、第一关没有结尾片段），
不必在测试里准备真实的 `.SMK`。

**顺序要点**：进选关屏的播放在 `while` **外面**——`SelectOtherScenario` 会重建整屏，参考的
`SelectCampaignScenario` 一个函数只进一次，重放会让每次换关都多响一遍开场。

**冒烟**：`--smoke-frames` / `--screenshot` 时只记
`Skipping the campaign video '<file>' because the run is scripted.`，与启动 logo 同一判据。
否则脚本会停在视频模态窗里等按键，把预算烧在与要截的画面无关的动画上。

**验证**：

- `--campaign 0 --key-at "4:V"`：进屏一次 `GOOD01.SMK`/`GOOD01V.SMK`，按 V 后再一次——View Intro
  走的是「上一关结尾（空，还没打完）+ 本关开场」。
- `--campaign 1 --complete-campaign-scenario --key-at "2:Digit1;6:Enter"`：日志依次是进屏开场
  `EVIL01.SMK`/`EVIL01V.SMK` → `Started the campaign scenario 'First Blood'` → `The campaign scenario is complete`
  → `Skipping the campaign video 'EVIL02.SMK'`/`'EVIL02W.SMK'`（第 0 关的结尾）→ 第 2 关选关屏。截图
  `f:\tmp\campaign-video4.png`。

**已知偏差**：参考 `Video::ShowVideo( vector<VideoInfo> )`（`game_video.cpp:103-300`）把一组内的多轨**同时**
播放并按 `offset` 叠加、按 `VideoControl` 决定画面/音轨/循环/等待；端口把组摊平成顺序列表逐条播一遍，
`VideoLoop` 不真的循环。对战役数据表里「循环背景 + 叠加动画」那种组合，观感与参考不同，但文件与顺序是对的。
合成播放器等 `VideoModalScreen` 支持多序列混播时再补（记为 P5 视频播放器的活）。
## ADR-258：换难度确认框与「最低难度」语义（`nh9.3`）

**问题**：选关屏按 `D` 换难度时，端口挑完就 `SetDifficulty(..., resetMinDifficulty: true)` 立刻写档。
参考不是这样：`setCampaignDifficulty()`（`game_campaign.cpp:912-1082`）只把选择**返回**给选关屏的局部变量
`currentDifficulty`（`:1513`、`:1698-1699`），真正写档在点 OK 开局之后（`:1643`）：

```cpp
campaignSaveData.setDifficulty( currentDifficulty, campaignSaveData.isStarting() && !allowToRestart );
```

后一个参数是「是否把最低难度重置成当前值」。端口恒传 `true`，于是**中途换难度会把已经记下的更低难度抹平**，
最终评分算高——这是会算错分的 bug，不只是缺个弹窗。

**改了什么**

| 位置 | 内容 |
|---|---|
| `RunCampaignScenarioSelection` | 循环外 `int campaignDifficulty = saveData.Difficulty;`，`ChooseDifficulty` 只更新它 |
| 同上 `StartScenario` | 照 `:1589-1596`：`!IsStarting && campaignDifficulty != saveData.Difficulty` 时弹 YES/NO，NO 就 `continue` 留在选关屏 |
| `StartCampaignScenarioMap(..., int difficulty, bool resetMinDifficulty)` | 在 `LoadPlayableMap` 拿到 `mapInfo` 之后才 `SetDifficulty(difficulty, resetMinDifficulty)`，与 `:1643` 同序（地图读不到就不动存档） |
| `ChooseCampaignDifficulty` | 返回值从 `void` 变 `int`，不再写档 |
| `ShowCampaignYesNo` | 用 `MessageBoxModal` + `SystemButtonArtwork` 拼标准 YES/NO 框，按钮素材缓存在 `_systemButtons` |

**最低难度只在第一关重置**：`resetMinDifficulty = saveData.IsStarting`（参考的 `isStarting() && !allowToRestart`，
端口 `allowToRestart` 恒为 false）。

**验证**（`--campaign 0 --complete-campaign-scenario` 让第一关自动打完再回选关屏）：

- 新战役换难度（`IsStarting`）：日志 `The campaign difficulty is 1 with a minimum of 1.` → `Started ... 'Force of Arms'`，
  **没有**确认框（参考只在 `!isStarting` 时问）。
- 第 2 关换难度后开局：先弹 `Difficulty` / "You have changed the campaign difficulty. …" 的 YES/NO
  （截图 `f:\tmp\campaign-duty-confirm.png`）；点 YES → `The campaign difficulty is 1 with a minimum of 0.`
  且 `Started ... 'Annexation'`（截图 `f:\tmp\campaign-duty-yes.png`）；点 NO →
  `The campaign difficulty change was cancelled; the scenario is not started.`，留在选关屏（无 `Annexation` 日志）。
- 全量 6077 通过、0 警告。

**备注**：`CampaignSaveData.SetDifficulty` 的最小值语义早就有单元测试（`MinDifficulty`），本轮的坑在**调用点**——传了常量 `true`。
这类「参数写死常量」的错误测试抓不到，只能对着参考逐行核。
## ADR-259：冒险地图的 Scenario Information 与重开（`nh9.3` 收尾）

**问题**：`EventScenarioInformation()`（`interface_events.cpp:366-396`）在战役局里不弹信息表，它淡出画面后调
`Game::SelectCampaignScenario( CANCEL, true )` —— 就是主菜单那张选关屏，但 `allowToRestart` 为真。端口此前
`allowToRestart` 恒为 false，`EventScenarioInformation()` 也一律只开单图的信息表，注释写着「选关界面还没搬」。

`allowToRestart` 的四处差别（`game_campaign.cpp`）：

| 行 | 差别 |
|---|---|
| `:1305` | 为真时不播本关开场 |
| `:1402-1405` | 奖励必须已选过（新战役则强制选第一项） |
| `:1420-1429` | OKAY 隐藏、RESTART 显示；地图文件缺了则禁用 RESTART |
| `:1484` | 高亮当前关而不是不选 |
| `:1589` | 难度变了要不要问 —— 条件 `(!isStarting() \|\| allowToRestart)` |
| `:1598-1602` | 点 RESTART 再确认一次 |
| `:1643` | `resetMinDifficulty` 取 `isStarting() && !allowToRestart` |

`CampaignScenarioSelectionDialog` 早就实现了 2/3/4（上一轮核过），缺的是把它们接出来。

**改了什么**

| 文件 | 内容 |
|---|---|
| `AdventureMapResults.cs` | 加 `AdventureEventResult.CampaignSelection = 8`，注释写明它不是 `GameMode`，而是端口的「请宿主开选关屏」 |
| `AdventureMap.Events.cs` | `EventScenarioInformation()`：`Settings.IsCampaignGameType` 为真时返回 `CampaignSelection`，否则照旧 `ShowGameInfo()` |
| `AdventureMapScene.cs` | 加 `CampaignSelectionRequested` 回调；`HandleResult` 里 `case CampaignSelection` 转达给宿主 |
| `GameHost.ShowAdventureScene` | 接 `CampaignSelectionRequested = () => RunCampaignScenarioSelection(CampaignSaveData.Get(), allowToRestart: true)` |
| `RunCampaignScenarioSelection(saveData, allowToRestart = false)` | 播片跳过、`TryCreate` 传参、难度条件加 `\|\| allowToRestart`、加 RESTART 确认、`resetMinDifficulty: IsStarting && !allowToRestart` |
| `CampaignMusic.cs`（新增）+ `RunCampaignScenarioSelection` | `:1311` 的 `playCampaignMusic()`：罗兰/阿基巴德各一首专属曲，其余战役返回 `null`、保持当前音乐；`CampaignMusicTests` 六个断言钉住这张表 |

**为什么用「请求」值而不是直接返回 `START_GAME`**：参考的 `EventScenarioInformation` 在 UI 层就能调游戏层的
`SelectCampaignScenario`（同一个进程、同一个全局 `conf`），端口把选关屏放在 App 层（它有场景、字体、美术），
UI 层引用不到。所以让 UI 层只报「该开选关屏」，宿主跑完后自己把地图换掉（重开）或什么也不做（取消），
与 `LoadGameEvent` / `CampaignScenarioCompleted` 是同一条接缝。

**重开靠 `allowToRestart` 分辨**：`CampaignSelectionAction.StartScenario` 同时覆盖 OKAY 与 RESTART，但重开模式下
OKAY 是隐藏的（`:1420-1429`），所以 `allowToRestart && Result == StartScenario` 就是点了 RESTART，不必再加枚举值。

**验证**（`--campaign 0`，帧号是脚本帧）：

- `--key-at "2:Enter;8:I"`：开局后在地图上按 `I` → `The adventure map asked for the campaign selection, so the host opens it.`
  → 选关屏，按钮是 `VIEW INTRO / DIFFICULTY / RESTART / CANCEL`，当前关高亮、奖励已选（截图
  `f:\tmp\campaign-restart1.png`）。
- 点 RESTART → `Restart` / "Are you sure you want to restart this scenario?"（截图 `f:\tmp\campaign-restart2.png`）；
  不答 = NO → `The scenario restart was cancelled.`；点 YES → 第二次 `Started the campaign scenario 'Force of Arms'`，
  地图重开（截图 `f:\tmp\campaign-restart3.png`）。
- 重开模式里改难度再点 RESTART：先弹难度确认（`:1589` 的 `|| allowToRestart` 生效，截图
  `f:\tmp\campaign-restart4.png`），答 YES 后才是重开确认（`f:\tmp\campaign-restart5.png`），两个都 YES →
  `The campaign difficulty is 1 with a minimum of 0.` —— 重开**不重置**最低难度（`:1643`）。
- 单图局（`--map Beltway.mp2`）按 `I` 仍走 `ShowGameInfo()`，没有变化。
- 音乐只订了映射表（单测），没在冒烟里听：`AudioPlayer.PlayMusic` 无声卡时静默失败，日志里看不出区别。

**仍未转的一块**：`outputCampaignScenarioInfoInTextSupportMode()`（`game_campaign.cpp:1084-1158`）要的是全局的
text support 输出层（把场景名、奖励、日期写成机器可读的行），端口只有 `Settings.IsTextSupportMode` 这个模式位、
没有输出层。等那个层落地再补，与 `nh9.3` 的界面流程无关。
## ADR-260：text support 输出层与战役选关屏的文本块（`nh9.5`）

**问题**：`outputCampaignScenarioInfoInTextSupportMode()`（`game_campaign.cpp:1084-1138`）把选关屏的场景名、奖励、
奖励选项与按键提示写成机器可读的行，靠的是 `START_TEXT_SUPPORT_MODE`（`logging.h:197-201`）加一串 `COUT`。
端口此前只有 `Settings.IsTextSupportMode` 这个模式位（F10 切、系统选项切、配置读），**没有任何输出通道**，
`SetTextSupportMode` 的注释直接写着「端口没有日志层，只留模式位」。

**通道本身**（参照 `logging.h:179-201` 与 `logging.cpp:148-155`）：

| 参考 | 端口 |
|---|---|
| `Logging::textSupportMode` 静态开关 | `TextSupport.IsEnabled` |
| `Logging::setTextSupportMode()` | `Settings.SetTextSupportMode()` 里一起翻（`settings.cpp:824-836` 也是两处一起改） |
| `COUT` 的默认分支写 stderr | `TextSupport.Output`（默认 null = `Console.Out`，测试可指向 `StringWriter`） |
| `Logging::TextSupportLogger` 在块首尾各写 `----------` | `TextSupport.WriteBlock()` 前后各写 `TextSupport.Separator` |
| `if (!isTextSupportModeEnabled()) return;` | `WriteBlock` 关着就什么都不写 |

**特意不用 `GameLog`**：它给每行加 `[INFO   ]` 前缀，而 text support 的行是给外部工具原样读的（参考直接
`COUT` 裸行），加了前缀就等于改了格式。

**文本内容**（`CampaignScenarioTextSupport.Describe`，纯函数，不碰控制台）：

- `Scenario Information`、`'Roland' campaign, scenario 1: Force of Arms`（关号是 `scenarioId + 1`，`:1096`）；
- 描述行后跟一个空行 —— `:1097` 的行尾自带 `'\n'`；
- `Awards: None` 或 `Awards:` 加每项 `- 名字 : 描述`；奖励从**已打完的关**翻译出来（`GetObtainedCampaignAwards`）；
- `Bonuses:` 加每项，再按个数决定三条 `-  Press X to select the … bonus.`（短横线后是两个空格，与奖励行不同，照抄）；
- 最后四行：`Press X to Start scenario.` 或 `Press X to Restart scenario.`、Intro、Difficulty、Exit。

**键名为什么由调用方传**：参考直接问 `Game::getHotKeyNameByEventId()`，端口的热键表在 UI 层
（`GameHotKeys`），Game 层不能引用 UI。所以 `Describe` 收一个 `CampaignScenarioTextSupportKeys` 记录，由
`GameHost.WriteCampaignSelectionTextSupport()` 从 `GameHotKeys` 取值填好，这样文本组合留在 Game 层、可单测。

**插在哪**：`RunCampaignScenarioSelection()` 里播片之后、主题曲之前，就是参考 `:1309`（文本）与 `:1311`（音乐）
的顺序。

**验证**：

- `f:\tmp\fh2-textsupport.cfg` 只写一行 `text support mode = on`，用 `--config` 跑 `--campaign 0`，控制台里出现
  完整的 `----------` 块，逐行与参考对得上（含空行、两个空格的 `-  Press`、`RETURN`/`V`/`D`/`ESCAPE`）；
- 同一局按 `I` 走到重开模式，块里换成 `Press R to Restart scenario.`；
- 新增 `CampaignScenarioTextSupportTests`（6 条：表头、一基关号、无奖励、有奖励、奖励选项与按键、两条结尾）与
  `TextSupportTests`（3 条：关着不写、块首尾短横线、空块）。两个全局开关的测试走 `TextSupportCollection`
  串行。
- 全量 6092 通过（295 + 441 + 3516 + 1840），0 警告。

**剩下的**：参考里其它模块也有 `START_TEXT_SUPPORT_MODE` 的函数（主菜单、战斗、冒险地图等）。通道现在有了，
那些地方可以按同样的写法逐个补，但不在 `nh9.5` 的范围内。
## ADR-261：高分榜数据层的逐函数核对（`d4k.1`）

`HighscoreData` / `HighScoreDataContainer` 早就转完了，但没有任何测试，也没有调用方 —— 谁也不知道格式、容量与排序
细节对不对。这一轮逐个函数对了一遍 `highscores.cpp`，并补上行为测试。

**逐项核对结果**（都对得上，只有两处有意的偏差）：

| 参考 | 端口 | 结论 |
|---|---|---|
| 文件头 `0xBADC0DE`（V1，无语言字段）/ `0xBADC1DE`（V2） | `MagicValueV1` / `MagicValueV2` | ✓ |
| `load()` V1：读 size、`resize(size)`、逐条 `loadV1()` | 循环 `i < size && !hdata.Fail` | 偏差：尺寸荒谬时端口不会去分配那一大块；格式本身一致 |
| `load()` V2：`hdata >> vector >> vector`（先 uint32 条数） | `ReadList` / `WriteList` 同样是先写 uint32 条数 | ✓ |
| V1 战役记录 `rating == 0` 时用 `dayCount` 顶上（`:178-184`） | 同 | ✓ |
| 不足十条补默认行、超过十条截断（`:186-198`） | 同（`<` 补、`>` 截） | ✓ |
| `saveHighscoreEntry`：同一次通关替换原地、否则插入后重排并截十、掉了返回 -1 | `SaveEntry` 同 | ✓ |
| 标准榜分数降序、战役榜升序（`:87-104`） | 同 | ✓ |
| 默认行：标准 150 分每行减 10、战役 600 到 3000 天 | 同 | ✓ |
| 战役默认行的场景名来自 `Campaign::getCampaignName()` | 原来写死 `"Roland"`/`"Archibald"` | **改**：改用 `CampaignAwardData.GetCampaignName()`，单一来源 |
| `getMonsterByRating` / `getMonsterByDay` 的两张阈值表（66 个兵种、按步长累加） | `BuildRatingTable` / `BuildDayTable` 同 | ✓ |
| `load()` 不清空目标容器 | 端口先 `Clear()` | 偏差：参考对同一对象二次 `load()` 会翻倍，端口不会；更安全，记在这里 |

**阈值表的真相**：参考注释写的「301 to 1000 → 泰坦」是**步长所在区间**，不是实际档位。实际阈值是「本级起点 + 步长」，
所以泰坦只有 301..320，1000 天是钢魔像、1001 天是德鲁伊、2000 天是游牧骑手、5800 天是妖精、5801 天起才是农民。
测试按真实阈值钉，别照注释写。

**新增 `HighScoreDataContainerTests`（26 条）**：字段往返、坏文件与缺文件、V1 布局补语言与战役评分、补满与截断、
标准榜留前十并返回 -1、战役榜反向排序、同一次通关替换原地、默认行数值、评分与天数两张阈值表的边界。

**验证**：全量 6118 通过（295 + 441 + 3542 + 1840），0 警告。
## ADR-262：P5 高分屏：进榜、两张榜与名字输入（`d4k.2`）

`d4k.1` 把数据层逐个函数核对完了，本层把 `game_highscores.cpp` 的界面接上。主菜单的 HIGH SCORES 招牌现在真的能开榜，
打完一局的高分也能进榜落位。

**分层与落点**：

| 参考 | 端口 |
|---|---|
| `highScoreDataContainer` 静态量 + `openHighScores()` 的读一次、换表、登记、存盘 | `Game/Highscores/HighScoreBoard.cs` |
| `redrawHighScoreScreen()` / `redrawMonstersAnimation()` | `UI/Dialogs/HighScoresView.cs` |
| `initialHighScoreEntryOffsetY` … `monsterOffsetY` 与两颗竖排按钮的位置 | `UI/Dialogs/HighScoresLayout.cs` |
| `ICN::HSBKG` / `HISCORE` / `MINIMON` | `UI/Dialogs/HighScoresArtwork.cs` |
| `openHighScores()` 的循环与 `DisplayHighScores()` 的换表 | `App/Scenes/HighScoresScreen.cs`（`IModalScreen`） |
| `Game::DisplayHighScores( false )`（主菜单那一支） | `GameHost.ShowHighScores()` + `_pendingHighScores` |

**逐项对照**（都对得上）：

| 参考 | 端口 |
|---|---|
| 标题 `top+50/+31`，标准榜第 6 帧、战役榜第 7 帧 | 同 |
| 行距 40、首行 72；人名 88、地图名 244、天数 403、评分 484、兵种 554/91 | 同（`HighScoresLayout`） |
| 人名截到 `244-88`、地图名截到 `403-244`，用本行字体量 | `FitToOneRow` |
| 选中行 `normalYellow()`，其余 `normalWhite()` | `HighScoresFonts` 的两张脸 |
| 标准榜 `getMonsterByRating(rating)`、战役榜 `getMonsterByDay(rating)` | `MonsterFor()` |
| 帧 `GetSpriteIndex()*9 + 1 + sequence[(top.x + offsetY + dayCount + frameId) % 15]` | `MonsterFrameBase()` + `AnimationFrame()` |
| 动画每 `DelayType::MAPS_DELAY`（250 ms）一帧 | `GameDelays.ValidateAnimationDelay( DelayType.MapsDelay )` |
| 战役数据不全时切换键 `disable()` | `IsSuccessionWarsCampaignPresent(assets)` 传进 `TryCreate` |
| 标准榜最后一行 `<= rating` 才上榜、战役榜 `>= rating` 才上榜 | `HighScoreBoard.Accepts()` |
| 名字空则写 `Unknown Hero`，限 15 字 | `NormalizePlayerName()` / `PlayerNameLength` |
| 标准榜分 `GetRating() * getGameOverScoreFactor() / 100`；战役榜分 `days * percent / 100` | `GetStandardRating()` / `GetCampaignRating()` |

`Game::getGameOverScoreFactor()`（`game.cpp:397-439`）此前没有移植，这一层补上了：四个尺寸系数 140/100/80/60，
天数分四段递减，六百天以上一律扣 180。

**偏差**（都记在这里）：

1. **名字输入不另开模态**：参考的 `Dialog::inputString()` 自己转循环，而 `GameHost.RunModal` 不允许嵌套，所以名字框以
   `InputStringView` 压在同一屏上，输入、光标、闪烁先给它，关掉之后再登记并换成黄色行。玩家看到的顺序与参考一致。
2. **换表不重建屏幕**：参考靠外层循环再开一次 `openHighScores( true )`；端口在同一屏里 `SetTable()`，省掉一次读文件
   （参考第二次也不读，`isInternalUpdate` 正是这个意思）。
3. **每帧整屏重画**：参考存底后只重画兵种那块矩形；端口本来就每帧全量推屏，像素一致，少一层 `ImageRestorer`。
4. **按钮文案 = 当前榜**：参考 `:376-381` 就是这么取素材的（标准榜配 STANDARD、战役榜配 CAMPAIGN），点它才换到另一张；
   右键帮助反而说目标榜（`:437-444`）。照抄，不「修」。
5. **右键帮助只写日志**：端口的模态里没有消息窗，`HighScoresScreen.LastHelpText` 把文案留给宿主。

**验证**：

- 构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；
- `HighScoreBoardTests` 15 条：缺文件补十行默认榜且不生成文件、文件只读一次、两张榜合格线的相反方向、登记落位与字段、
  空名写 `Unknown Hero`、存写回环、四个天数分段、两条评分乘积；
- `HighScoresViewTests` 19 条：缺素材返回 null、背景居中、标题与四列偏移、行距、黄色行、两张榜各自的兵种、两颗按钮的位置与
  按下态、点击 / 越界点击 / 右键帮助 / 禁用态、列宽截断、九帧起点、动画表达式；
- 全量 6152 通过（295 + 441 + 3557 + 1859），0 警告。

**剩下的**：像素基线要游戏数据才生成得到（本机没有 AGG，脚本按 ADR-010 跳过）；一局打完之后的
Win / Lose / HighScore 三支与 `FinishHumanTurn` 的接线属于 `d4k.3`，它只需把 `HighScoreCompletion` 交给
`HighScoresScreen.TryCreate`。
## ADR-263：结局三支的接线：胜、败与高分（`d4k.3`）

`GameOverResult` 早已能算出条件并说明文案，`AdventureMapDialogs.CheckGameOver()` 也早就把窗口都画出来了；缺的是
**谁来看这个返回值**——`AdventureMapScene.HandleResult` 的 `Lose` / `HighScore` 一直落在 `default` 分支里只写日志。

**这一轮接了什么**：

| 位置 | 改动 |
|---|---|
| `AdventureMapScene.HandleResult` | `HighScore`：把 `DescribeFinishedGame()` 算出的 `HighScoreCompletion` 交给宿主（新属性 `HighScoreRequested`），没有宿主时按「离开地图」处理；`Lose`：回主菜单，不再落进 `default` |
| `AdventureMapScene.DescribeFinishedGame` | 参考 `game_highscores.cpp:322-337` 的三个数：场景名取 `Settings.CurrentMapFileInfo.Name`、天数取 `World.DayCount`、分数取 `HighScoreBoard.GetStandardRating(map, 难度, 天数)`、种子取 `World.MapSeed` |
| `AdventureMapScene.Update` | 新增 `CheckGameOverOnNewTurn()`：每 (颜色, 天) 判一次，补上参考 `game_startgame.cpp:1062-1065` 那个「人类回合开始时判胜负」的调用点。少了它，「上一回合被灭、这一回合才开始」的局永远等不到失败屏 |
| `AdventureInterface.CheckGameOver()` | 把 `AdventureMapDialogs.CheckGameOver()` 包一层公开方法，场景才够得到判定 |
| `AdventureSession.Create` | 新开局时 `GameOverResult.Current.Reset()`，参考 `game_over.cpp:475-479`。**这一步是这个 ADR 的关键**：`Reset()` 记的是「还在局中的颜色」，不清的话热座局第一回合就会被判成「所有人类玩家都出局」；读档不走它，颜色集合由存档里的那份恢复 |
| `GameHost.ShowHighScoresAfterWin` | 先 `ReturnToMenu()`（那里放 `WIN.SMK`），再把开榜排到下一帧——`RunModal` 不能在场景 `Update` 的栈上压 |
| `GameHost.CompleteCampaignScenario` | 战役最后一关走 `HIGHSCORES_CAMPAIGN`：表名用 `CampaignAwardData.GetCampaignName()`、分数用 `days × 难度百分比`，`IsCampaign` 为真 |
| `GameHost` 的 pending | `bool _pendingHighScores` 换成 `PendingHighScores?(IsCampaign, Completion)`：主菜单那一支是「只看榜」，结局两支各带一张榜与一局成绩 |

**顺序与参考一致**：胜负框（`AdventureMapDialogs`）→ 离开地图（`ReturnToMenu` 放 `LOSE.SMK`/`WIN.SMK`）→ 主菜单 →
下一帧开高分屏（问名字、登记、黄色行）→ 关掉回主菜单。

**偏差**：

1. 战役最后一关原本「片尾与战役高分榜没有，先停在主菜单」；现在高分榜接了，片尾（credits）仍然缺，属于 P4。
2. `ReportGameOver()`（电脑回合里发现人类全灭的那一支）仍只离开地图：它和 `HandleResult(Lose)` 是两条路，参考里前者也
   走 `checkGameOver()`，但那条支路要重跑 AI 的一帧判断，留给以后；这一轮只保证人类回合的三支。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6152 通过（295 + 441 + 3557 + 1859）。App 层没有测试工程，
这三处接线的行为与既有冒烟脚本一样要靠带游戏数据的运行验证（`--map` 开一张图、打完一局看日志里的
「the high score table follows」与「took row N of the standard table」）。
## ADR-264：P7 随机地图：先拆四步，再做配置与尺寸（`4f2.1.4`）

`maps/map_random_generator*.cpp` 一共约 113 KB，一次吞不下。先按参考的三个文件与生成阶段拆成四步，各自独立可测：

| 子项 | 内容 | 依赖 |
|---|---|---|
| `4f2.1.4` | 配置与尺寸：`Configuration` 的五个旋钮、三组枚举、三个名字、`calculateMaximumWaterPercentage`、`calculateRegionSizeLimit`（本轮） | — |
| `4f2.1.1` | 地图状态网格与区域：`MapStateManager`/`Node`/事务、`Region` 的扩张与连接 | ← `4f2.1.4` |
| `4f2.1.2` | 地形、区域布局与道路连通 | ← `4f2.1.1` |
| `4f2.1.3` | 物件、经济平衡与玩家起点，`generateMap` 收口 | ← `4f2.1.2` |

`4f2.2`（生成对话框）本来就挂在 `4f2.1` 下面，四个子项做完它才有得接。（期间 `bd create` 撞上远程超时重试，
误生成了一个重复项 `4f2.1.5`，已 `bd supersede` 到 `4f2.1.2`。）

**本轮落地**：`Game/Maps/RandomMapConfig.cs`（`RandomMapConfig`、`RandomMapLayout`、`RandomMapResourceDensity`、
`RandomMapMonsterStrength`、`RandomMapRegionalObjects.For`、`RandomMapSizing`）。

**逐项对照**：

| 参考 | 端口 |
|---|---|
| `Configuration{ playerCount 2, waterPercentage 0, seed 0, MIRRORED, NORMAL, NORMAL }` | `RandomMapConfig` 的同名默认值 |
| `regionObjectSetup` 三行 `{1,2,0,1,1,2,8500}` / `{1,4,0,2,1,3,15000}` / `{1,4,1,2,2,5,25000}` | `RandomMapRegionalObjects.For` |
| `layoutToString` / `resourceDensityToString` / `monsterStrengthToString` | `LayoutName` / `ResourceDensityName` / `MonsterStrengthName`，返回参考的 msgid |
| `calculateMaximumWaterPercentage`：`tileCount - 200*(players+1)` 后取百分比并夹到 0 | `MaximumWaterPercentage` |
| `calculateRegionSizeLimit`：占地权重 49/15/6/9/16 → 补 40% 空地 → 圆形边距 → 夹在 `players+1` 与 `players*3` 之间 | `RegionSizeLimit`，整数运算顺序逐字照抄 |

**两个算出来的事实**（写进测试，免得以后「优化」掉）：

1. 默认配置（2 玩家、正常密度、72×72、无水）的区域是 **864** 格：187 格物件 → 311 → 加 69 格边距 = 380；
   5184 / 380 = 13 个区域，被上限 6 压住，于是 5184 / 6。
2. **36×36 上「正常」与「丰富」给出同一个 432**：两者目标区域（380 与 430）都让平均区域数落到「玩家数 + 1」这个
   地板之下，被地板抬成同一个数。所以严格的三档大小关系要在 45×45 上验（337 / 405 / 506），36×36 只用来钉住
   「稀缺 = 324」。

**验证**：`RandomMapConfigTests` 20 条（默认值、三行密度表、三个 msgid、水面上限 53/93/0、默认区域 864、
36×36 三档、45×45 严格序、水面占块、六玩家 144×144、非法玩家数与水面百分比返回 0）；
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6172 通过（295 + 441 + 3577 + 1859）。
## ADR-265：电脑回合里的结局判定（`fkz`）

`AdventureMapScene.ReportGameOver()`（电脑回合里发现人类全灭）原来只写一行日志再 `RequestExit()`。参考在
`game_startgame.cpp:960-976` 的那条支路里同样调 `checkGameOver()` 并解释返回值，所以端口的后果是：热座局被电脑灭掉时，
`GameOverResult.Requests` 一直是空的——**没有失败屏，也没有 `LOSE.SMK`**，玩家直接掉回主菜单。

**改动**：`ReportGameOver()` 现在先 `_interface.CheckGameOver()`，结论非 `Cancel` 时交给 `HandleResult`（与人类回合的
三支同一条路：`Lose` 回主菜单并在 `ReturnToMenu()` 里放失败片，`HighScore` 开榜），判不出结论才退回原来的
`RequestExit()`。仍然只做一次（`_gameOverReported`），因为宿主不换场景时这条分支每帧都会被走到。

**验证**：构建 0 警告 0 错误；全量 6172 通过（295 + 441 + 3577 + 1859）。App 层无测试工程，实际行为要带游戏数据跑
一局热座（或 `--map` + 让 AI 拿下最后一座城）看日志里的「the game is over on day N」与随后的「the scenario is lost」。
## ADR-266：P7 随机地图：状态网格、区域扩张与矿物经济（`4f2.1.1`）

参考的 `map_random_generator_info.h/.cpp`（约 19 KB）搬完：网格、事务、区域与矿的均衡。落点两个文件：

| 参考 | 端口 |
|---|---|
| `NodeType` / `Node` | `RandomMapNodeType` / `RandomMapNode`（引用类型，见下） |
| `MapStateManager`（构造函数、`commitTransaction`、`rollbackTransaction`、四个 `getNode*`） | `RandomMapState` |
| `MapStateTransaction`（析构回滚、`commit()`） | `RandomMapStateTransaction`（`IDisposable` + `Commit()`） |
| `Region`（构造、`checkAdjacentTiles`、`regionExpansion`、`checkNodeForConnections`、`adjustRegionToFitCastle`） | `RandomMapRegion` |
| `MapEconomy` | `RandomMapEconomy`（键用 `Mp2ResourceType`） |

**几处必须说清楚的设计**：

1. **网格自己带宽高**：参考的 `Maps::GetPoint/GetIndexFromAbsPoint` 读的是 `world.w()`，生成器因此离不开世界。
   端口把宽高收进 `RandomMapState`，`GetPoint`/`IndexAt`/`ApproximateDistance` 都是它的成员——生成器才能在单测里跑。
   索引约定不变（`y * width + x`），`ApproximateDistance` 照抄八方向距离 `max + min / 2`。
2. **`Node` 是引用类型**：参考的 `Region.nodes` 存的是 `std::vector<Node>` 元素的引用，写进去就是改网格那一格。
   C# 里只有类能保住这个语义，所以 `RandomMapNode` 是类，`GetNode*` 返回同一个对象。事务的历史因此存的是
   `(index, region, type)` 三元组而不是节点快照。
3. **事务用 `IDisposable`**：`using` 里没 `Commit()` 就回滚，对应参考的析构回滚；嵌套由 `_transactionRecords` 的栈维护，
   与 `record` 比较后只回滚本层。
4. **越界**：返回共享的 `_outOfBounds`（索引 `-1`、`BORDER`），与参考的断言 + 返回同一对象一致；写它等于丢进黑洞。
5. **区域 id 就是列表下标**：`checkNodeForConnections` 里参考直接 `mapRegions[node.region]`，端口照抄，测试里因此
   给列表补了一个 id 0 的中立水区占位。
6. **`adjustRegionToFitCastle` 收 `RandomMapState`** 而不是参考的 `Map_Format::MapFormat`：后者在参考里只用到了
   `width`，端口直接问网格要，省掉一个还没移植的格式类型。

**两个从参考抠出来的细节**（都进了测试）：

- `checkAdjacentTiles` 的「相邻」判定把 **region 0（空闲）也算作一个不同的邻居**：`seen` 初值是节点自己的 region，
  任何不同的值都会 `++distinctNeighbours`。所以一个能连路的边界格，八邻域里只能有「自己 + 一个连续的别区」，
  中间夹着一个空闲格就会被判成「这不是能放一个路口的地方」。
- 扩张半径是 `sqrt( sizeLimit / π ) * 1.85`：40 格的区域约 6.6 格半径，测试按这个上限断言没有节点跑得更远。

**验证**：新增 19 条测试——网格编号与越界哨兵、坐标往返、八方向距离、回滚 / 提交 / 嵌套 / 点重载回滚、
区域占用中心、非法 size、扩张（节点数、region 标记、半径、`sizeLimit` 上限）、碰到别区变边界并入邻居、
连接把四个格子标成路口并把 `connections` 双向登记、水邻域变海岸、非边界格不动、城堡贴合钳到四格、
矿物轮转顺序（Wood → Ore → Crystal → Sulfur → Gems → Mercury → Wood）、金矿只计数不被选、非法资源被拒。
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6191 通过（295 + 441 + 3596 + 1859）。

**下一步**：`4f2.1.2`（地形、区域布局与道路连通），它要用这里的事务做「试探失败就还原」。
## ADR-267：P7 随机地图：区域布局与地形铺设（`4f2.1.2` 前半）

参考 `generateMap()` 的**步 1 到步 4** 落地：区域怎么摆、怎么长、地形怎么铺。落点
`Game/Maps/RandomMapTerrain.cs`（`RandomMapTerrain` + `RandomMapTerrainGenerator`）。

| 参考 | 端口 |
|---|---|
| 步 1：`groundTiles`、`expectedRegionCount`、`regionObjectSetup` 行 | `Generate()` 开头，复用 `RandomMapSizing.RegionSizeLimit` 与 `RandomMapRegionalObjects.For` |
| 步 2：`mapLayers` 内外两圈、`startingAngle`/`offsetAngle`、玩家区判定 `factor`、起点地形两条表、宝藏上限 ×2、区域大小 ×6/5 | `PlaceRegions()` |
| `mapBoundsCheck` lambda（`x*width + y`） | `ClampedIndex()` |
| 步 3：所有区域轮流各长一圈 | `GrowRegions()` |
| 步 4：`setTerrainOnTile` + 补反向邻居 | `PaintTerrain()`，写进 `RandomMapTerrain` 的 `MapGround[]` |

**必须记下的几件事**：

1. **端口没有 <c>MapFormat</c>**：参考把地形直接写进编辑器格式；端口先产出一张 `MapGround[]` 与区域表，写文件留给
   `4f2.1.3`。未铺过的格子是草地——参考的新图本来就是整张草地。
2. **`mapBoundsCheck` 是转置的**：参考算的是 `x * width + y`，不是通常的 `y * width + x`。方形地图上两种写法都在界内，
   只是中心点关于对角线镜像。端口照抄并写进注释，否则区域落点与参考不同。
3. **`assert( placedPlayers == playerCount )` 换成返回值**：摆不下就返回 `null`，不把进程带走。
4. **玩家区的地形表与中立区不同**：`{Grass, Dirt, Snow, Lava, Wasteland}` 对
   `{Grass, Dirt, Snow, Lava, Wasteland, Beach, Swamp, Desert}`，参考的两条表原样搬过来。
5. **区域大小**：稀缺资源下玩家起点就是 `regionSizeLimit`，其余一律 `×6/5`，因为区域里要装下城堡、矿与宝箱。

**算出来的形状**（进测试）：72×72、两名玩家、正常密度 → 区域 864 格、中立区 4 个、内圈 2 个、外圈 4 个，
共 **7 个区域**（含 id 0 的水域占位）；每个玩家一个 `STARTING` 区，颜色 0..n-1，宝藏上限 15000，中立区 30000。

**验证**：`RandomMapTerrainTests` 10 条——非法配置返回 null、区域表（7 个、id 0 是水域且只有一个节点）、
每个玩家恰好一个起点区与颜色、区域地形逐格一致且越界是 `Unknown`、起点表/中立表取值、稀缺下的区域大小、
邻居双向、同种子同图、不同种子中心不同、坐标按宽度换算。
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6201 通过（295 + 441 + 3606 + 1859）。

**移出去的**：步 5（地形过渡、水面百分比、城堡）与步 6（按边界长度连路）都要在物件落位之后才有意义——参考自己
就把连路放在「摆完矿与城堡之后，免得路从物件下面穿过」。所以这两步并进 `4f2.1.3`（物件、经济平衡与玩家起点），
`4f2.1.2` 以「布局 + 地形」收口，免得一个 issue 挂着两件互不相干的事。
## ADR-268：战役结局的顺序：评分字幕、WIN.SMK 与胜利音乐（`urk` 的前半）

上一轮把 `nh9.4` 收口时，战役最后一关只是「直接开战役高分榜」；参考在那里还有三步
（`game_campaign.cpp:1240-1276`）。这一轮把不需要新界面的那两步补齐：

| 参考 | 端口 |
|---|---|
| `playPreviousScenarioVideo()` | 早就在 `CompleteCampaignScenario()` 里（`CampaignVideoPlayback`） |
| `Game::ShowCredits( false )`（`game_credits.cpp`，35 KB 的滚动致谢） | **仍未移植**，留给 `urk` |
| `ratingText`：`Congratulations!\n\nDays: N` + `\nDifficulty: X\n\n` + `Score: S\n\nRating:\n<按天数反查的兵种名>` | `Game/Campaign/CampaignEndingText.cs`（三段分开 + `Describe` 拼合） |
| `Video::Subtitle( ratingText, 5000, UINT32_MAX, {475,110}, 140 )` 压在 `WIN.SMK` 上 | `UI/Widgets/VideoSubtitle.cs`（`GlyphText.DrawMultiline` 的换行 + 居中）+ `VideoModalScreen` 的字幕参数 |
| `PLAY_CUTSCENE_WAIT`：放完停在最后一帧等按键 | `VideoModalScreen.waitForKey` |
| `AudioManager::PlayMusicAsync( MUS::VICTORY )` | `_audio?.PlayMusic( MusicTrack.Victory )` |
| `return HIGHSCORES_CAMPAIGN` | `_pendingHighScores = new PendingHighScores(IsCampaign: true, …)`（ADR-263 已接） |

**落在哪**：`GameHost.CompleteCampaignScenario()` 的 `completion.IsCampaignFinished` 分支，顺序就是参考的顺序：
算分数 → 组字幕 → `PlayVideo("WIN.SMK", waitForKey: true, subtitle: …)` → 胜利音乐 → 排下一帧开战役榜。
字幕里难度的名字走 `GameText`（`campaignDifficulty|Default` 这类 msgid，翻译层会去掉上下文），分数与兵种名来自
`HighScoreBoard.GetCampaignRating()` 与 `HighScoreDataContainer.GetMonsterByDay()`。

**几个细节**：

1. **字幕不换字体**：参考用三张脸（`normalWhite` / `smallWhite` / `normalWhite`）拼一段 `MultiFontText`；端口把三段
   分开放出来，`VideoSubtitle.Draw` 只按一种脸画。要还原三种字号，得先把 `MultiFontText` 接进字幕，暂无必要。
2. **字幕位置与延时照抄**：第 5 秒起、`(475,110)`、最宽 140 像素——那是 `WIN.SMK` 里刚烧掉的那块黑方框。
3. **字幕用画面的索引表**：字幕画在贴完帧、换完调色板之后，所以白色字映射的是片子的调色板（参考同样如此）。
4. **`waitForKey` 只对「放完」生效**：中途按键仍然直接跳过，与参考「任意键结束」一致。

**验证**：`CampaignEndingTextTests` 11 条（三段原文、拼合无多余空格、难度 msgid、兵种名按天数的四个边界）；
`VideoSubtitleTests` 6 条（空文本、两行落点、长词折行、居中、无宽度时不偏移、参考宽度 140）。
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6218 通过（295 + 441 + 3617 + 1865）。

**剩下的**：`Game::ShowCredits( false )` 那一屏（滚动致谢 + `CBKGLAVA` 背景 + 动画序列，35 KB）仍在 `urk` 里，
它的标题与描述这一轮已改成「只做 credits 屏」。端到端的片尾顺序要带游戏数据跑（`--campaign 0` 打完最后一关）。
## ADR-269：P7 随机地图：物件估价表与守卫强度（`4f2.1.3` 第一刀）

`4f2.1.3`（物件阶段）比前面三块都大：参考的 `map_random_generator_helper.cpp` 有 53 KB，而它的落子函数
（`putObjectOnMap`、`placeCastle`、`forceTempRoadOnTile`）全都吃 `Map_Format::MapFormat`——端口还没有那个格式。
所以先切掉不需要落子、纯查表的两块，把「物件值多少、守卫多强」先钉死：

| 参考 | 端口 |
|---|---|
| `objectGoldValue`（`map_random_generator_helper.cpp:79-126`） | `Game/Maps/RandomMapObjectValues.cs` 的 `GoldValue(Mp2ObjectType)`，键直接用端口的 `Mp2ObjectType` |
| `getObjectGoldValue()` 的断言 | 换成 `GameLog.Warn` + 返回 0：一个没列进表的装饰物不该把生成器带走（测试宿主里 `Debug.Assert` 会直接抛） |
| `minimalTreasureValue` / `maximumTreasureGroupValue` | `MinimumTreasureValue = 650` / `MaximumTreasureGroupValue = 14000` |
| `MonsterSelection{ monsterId, allowedMonsters }` | `RandomMapMonsterSelection` 记录 + `None` |
| `getMonstersByValue()`（`:304-363`） | `Game/Maps/RandomMapMonsterPools.cs` 的 `ByValue(strength, protectedObjectValue)` |

**逐档对照**（价值 → 随机兵种 + 名单长度）：13000 七种、11500 九种、10000 十三种、8500 / 7000 各十五种、
5500 十七种、3500 二级随机、2000 一级随机、2000 以下不放守卫。玩家的强度设置先挪价值再查表：
`DEADLY +2500`、`STRONG +1500`、`WEAK -1500`。

**还没做的**（仍是 `4f2.1.3` 的范围）：`prefabObjectSets` / `powerupObjectSets` / `decorationsPerGround` 三张表、
`canPlaceObject` 系列的空间判定、`getRandomTreasure`、把物件写进 `Mp2Map`（`Mp2Map.CreateBlank` 已经在
`EditorScene.CreateNew` 里用过，是可行的落点）、水面百分比与地形过渡、按边界长度连路。

**验证**：`RandomMapObjectValuesTests` 35 条——十五个物件的估价、表外物件为 0、两个常量、十一条阶梯边界
（含每一档的首尾兵种与名单长度）、四档低价值的随机兵种名、低于 2000 无守卫、三种强度设置挪动价值的四个例子、
`Normal` 不挪价值。构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6253 通过（295 + 441 + 3652 + 1865）。
## ADR-270：P7 随机地图：宝物组与能力点组的预制表（`4f2.1.3` 第二刀）

继续把物件阶段拆成「先搬表、再搬逻辑」。这一刀是落子要用的**预制小景**：

| 参考 | 端口 |
|---|---|
| `ObjectPlacement{ offset, groupType, objectIndex }` | `RandomMapObjectPlacement(Point Offset, ObjectGroup Group, int ObjectIndex)` |
| `ObjectSet{ obstacles, valuables, entranceCheck }` | `RandomMapObjectSet(Obstacles, Valuables, EntranceCheck)` |
| `prefabObjectSets`（五组） | `RandomMapObjectSets.Prefabs` |
| `powerupObjectSets`（五组） | `RandomMapObjectSets.PowerUps` |
| `ObjectGroup` 枚举 | **端口已有**（`Game/Maps/ObjectGroup.cs`，名字与参考一一对应：`LandscapeTrees`、`LandscapeMountains`、`AdventureTreasures`、`AdventurePowerUps`） |

**几处抄表时的发现**（都写进了测试，越界的抄写会立刻红）：

1. 五组宝物组都是「四个宝箱围成一圈 + 三到四棵树封边 + 三到五个入口格」，宝箱统一是
   `ADVENTURE_TREASURES` 第 9 号；能力点组骨架一样，只把一个宝箱换成 `ADVENTURE_POWER_UPS`，编号依次是
   **10 / 18 / 12 / 13 / 16**（第五组的外圈还换成了两座山）。
2. 入口格与宝物格都不与障碍格重合、宝物之间也不共格——这两条是我抄完表后加的**数据自检**，不是参考里的断言，
   但它们正是「落子时不要把玩家堵在外面」这件事的最低价保险。
3. `decorationsPerGround`（`map_random_generator.cpp:213-550`，约 330 行）这次没搬：它是纯装饰，
   而且量比前两张表大得多，单独一刀更稳。

**下一步的关键情报**：端口**已经有** `ObjectGroup` 与 `MapObjectCatalog`（`GetObjectsByGroup` / `GetObjectInfo`），
也就是「物件组里第 N 个物件的占地」已经拿得到；再加上 `Mp2Map.CreateBlank` 与 `TerrainTransitions.TryGetBlend`，
落子这一半并不需要先造 `Map_Format::MapFormat`——直接在 `Mp2Map` 上写瓦片即可。

**还没做的**（仍是 `4f2.1.3`）：`decorationsPerGround`、`canPlaceObject` 系列空间判定、`getRandomTreasure`、
把预制与宝物写进 `Mp2Map`、水面百分比与地形过渡、按边界长度连路。

**验证**：`RandomMapObjectSetsTests` 8 条——五加五组、第一组逐格对照、宝物组的奖品一致、能力点组「一换二」、
能力点编号序列、只用到四个组、入口与奖品不压障碍、奖品互不共格。构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；
全量 6261 通过（295 + 441 + 3660 + 1865）。
## ADR-271：P7 随机地图：落子的两条格规则（`4f2.1.3` 第三刀）

表的下一刀是**逻辑**：格子怎么被占、物件能不能放。落点 `Game/Maps/RandomMapObjectRules.cs`。

| 参考 | 端口 |
|---|---|
| `markNodeAsType()` / `markNodeIndexAsType()`（`:221-237`） | `RandomMapObjectRules.MarkNodeAsType` / `MarkNodeIndexAsType` |
| `canPlaceObject()`（`:239-281`） | `RandomMapObjectRules.CanPlaceObject` |
| `iterateOverObjectParts()`（`:209-219`） | `CanPlaceObject` 里的地面层循环（跳过 `Shadow` / `Terrain` 两层） |

**三条优先级规则**（参考注释里点名的）：

1. `BORDER` 与 `ACTION` 从不被后来的标记覆盖——区域边界与「留给可交互物件的位置」优先。
2. `PATH` 不许盖到 `OBSTACLE` 上：路不能从石头里穿过去。
3. 反过来，`OBSTACLE` **可以**盖住 `PATH`（参考的条件就是这么写的），所以落树时不必先清路。

**`CanPlaceObject` 的判据**：地面层每一格都要「在地图内、有主人（region 非 0）、不是 `ACTION`/`PATH`、且要么是
`OPEN`、要么是 `BORDER` 而这个物件不是可交互物件」；顶层（悬空树冠、屋顶）只要求在地图内且有主人；可交互物件
还要正下方那一格是 `OPEN` 或 `PATH`，玩家才走得到跟前。

**一处测试教给我们的细节**：参考的 `info.objectType` 是**动作类型**（`MP2::OBJ_TREASURE_CHEST` 带访问位），
端口的 `Mp2ObjectType` 把两者拆成 `TreasureChest` 与 `ActionTreasureChest`；`Mp2ObjectTypes.IsInGameActionObject`
对前者答 `false`、对后者答 `true`。用错了类型，「可交互物件压在边界上」这条规则根本不会触发。

**验证**：`RandomMapObjectRulesTests` 12 条——区域内放得下、无主格子拒绝、出界拒绝、影子与地表层不占格、
障碍挡住装饰物、边界容装饰物但不容可交互物件、`ACTION`/`PATH` 不许盖、可交互物件要下方可走、顶层只要格子、
标记不覆盖边界与 `ACTION`、路不穿障碍而障碍可盖路、越界标记无副作用。构建 0 警告 0 错误；
全量 6273 通过（295 + 441 + 3672 + 1865）。

**还没做的**（仍是 `4f2.1.3`）：`decorationsPerGround`、`getRandomTreasure` / `convertMP2ToObjectInfo`、
把预制与宝物写进 `Mp2Map`（`Mp2Map.CreateBlank` + `TerrainTransitions.TryGetBlend` 已验证可用）、
水面百分比、按边界长度连路。
## ADR-272：P7 随机地图：宝物池与类型换算（`4f2.1.3` 第四刀）

物件阶段的第四刀：一个区域「还能花多少钱」时，挑什么宝物放下去。

| 参考 | 端口 |
|---|---|
| `possibilities` 八项宝物池（`:367-370`） | `RandomMapTreasureSelection.Candidates`（宝箱与资源各两份，所以权重是别的两倍） |
| 按 `getObjectGoldValue(object) > goldValueLimit` 过滤（`:372-378`） | `TryPick()` 里的 `Where`，池子空了返回 `false` |
| `convertMP2ToObjectInfo()` + 缓存表（`:383-406`） | `TryConvert()` + 静态 `Lookup` |
| `limitedGroupList` 六张表、`try_emplace` 先到先得（`:388-396`） | `LookupGroups`：饰品 → 住处 → 矿 → 能力点 → 宝物 → 怪物 |

**这一刀挖出的一个真问题**：端口把 `MP2::OBJ_*` 的「动作位」拆成两个成员——`TreasureChest` 与
`ActionTreasureChest`——而**物件目录里存的都是带动作位的那一个**（`Mp2ObjectCatalog.Generated.cs` 里全是
`Mp2ObjectType.ActionTreasureChest` / `ActionRandomResource` / `ActionCampfire` / `ActionRandomArtifact*`）。
估价表当初是按不带动作位的写法建的，于是换算一查就落空。修法：两处都先 `Mp2ObjectTypes.NonActionType()` 归一——

1. `RandomMapObjectValues.GoldValue/HasGoldValue` 查表前归一，两种写法查到同一行；
2. `RandomMapTreasureSelection` 建表与查询时都归一。

这样「估价表按可读的写法维护、目录按文件的写法存储、换算处对齐」三件事各自自洽。

**验证**：`RandomMapTreasureSelectionTests` 7 条——八项池子的顺序、每个候选都能换算回自身（并验证带动作位的写法
指向同一格）、六张表之外的物件换算失败、650 上限只买得到资源堆、0 与 649 什么都买不到、1000 上限能出宝箱/篝火/资源
但买不起次级宝物、同种子同选择；`RandomMapObjectValuesTests` 加一条「动作写法与平写法同价」。
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6281 通过（295 + 441 + 3680 + 1865）。

**还没做的**（仍是 `4f2.1.3`）：`decorationsPerGround`（约 330 行）、把预制与宝物真正写进 `Mp2Map`
（落点与瓦片写入路径已验证可行）、水面百分比、按边界长度连路、`generateMap` 收口。
## ADR-273：P7 随机地图：装饰物分块与矿的图形（`4f2.1.3` 第五刀）

两张按地形查的小表，落的都是落子阶段要用的常量：

| 参考 | 端口 |
|---|---|
| `treeTypeFromGroundType()`（`:128-156`） | `RandomMapDecorations.TreeIndexFor(MapGround)` |
| `mountainTypeFromGroundType()`（`:158-186`） | `RandomMapDecorations.MountainIndexFor(MapGround)` |
| `getFakeMP2MineType()`（`:188-207`） | `RandomMapEconomy.MineObjectType(Mp2ResourceType)` |

抄表记下的几个「反直觉但要照抄」：

- 树：沼泽与草地共用第 0 块，沙滩与沙漠共用第 24 块；水面没有树（参考断言后回 0）。
- 山：沙滩是第 0 块而不是沙漠的第 30 块；荒野跳到第 44 块，是九档里最大的一跳。
- 矿：只有三种图形——锯木厂（木/铁）、矿（硫磺/水晶/宝石/水银）、废弃矿（金）；别的资源回 `None`（参考断言）。

**验证**：`RandomMapDecorationsTests` 27 条（树的十种地形、山的九种地形、矿的八种资源，含水面与未知地形的 0）。
构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6308 通过（295 + 441 + 3707 + 1865）。
## ADR-274：P7 随机地图：把地形写进真正的 MP2 地图（`4f2.1.3` 第六刀）

`RandomMapTerrain` 之前只是「一张 `MapGround` 表」；这一刀把它变成**可保存的真地图**。落点
`Game/Maps/RandomMapBuilder.cs`，`Build(RandomMapTerrain) → Mp2Map?`。

| 参考 | 端口 |
|---|---|
| `Maps::setTerrainOnTile( mapFormat, index, ground )`（`:738-740`） | `Mp2Map.CreateBlank(size, (x, y) => TerrainTransitions.PlainImageIndex(ground, tile))` |
| `Maps::setTerrainBoundaries( mapFormat )`（步 5 的过渡修复） | `BlendTransitions()`：逐格 `TerrainTransitions.TryGetBlend` → `SetTile(tile with { TerrainImageIndex, TerrainFlags })` |

**三处需要说明的判断**：

1. **两遍而不是一遍**：先用 `PlainImageIndex` 把每格铺成纯色图（八变体按坐标取，可复现），再整体混一次过渡。
   第二遍可以顺序走——混出来的图**永远落在本格的地形块内**，不会改变邻格看到的地形，所以前后顺序不影响结果。
   这一点也是「`TheMapKeepsTheGroundOfEveryTile` 能对 5184 格全量断言」的前提。
2. **尺寸只认四种**：MP2 的边长只有 36 / 72 / 108 / 144，别的尺寸 `Build` 返回 `null` 并记警告（参考的编辑器格式没有这条限制）。
3. **落盘要用 `WriteAll` 不是 `Save`**：`Mp2MapWriter.Save(filePath, map)` 的语义是「把改动补丁回源文件」，它会去读
   `sourcePath`，从零生成的图没有源文件；`WriteAll` 才是「按当前字段造一个完整镜像」。这一条是测试逼出来的
   （`Save` 直接抛 `ArgumentException: The path cannot be empty`）。

**端到端验证**（`RandomMapBuilderTests` 5 条）：非法尺寸返回 `null`、72×72 的每一格地面与区域表一致、每格都有图且小于
`MaxImageIndex`、同地形两次构建逐格相同、**生成 → 构建 → `WriteAll` 写盘 → `Mp2Map.Read` 读回 → 每一格地面仍一致**。
构建 0 警告 0 错误；全量 6313 通过（295 + 441 + 3712 + 1865）。

**还没做的**（仍是 `4f2.1.3`）：把**物件**（预制 / 宝物 / 矿 / 城堡 / 英雄 / 守卫 / 装饰）写进这张图、
`decorationsPerGround` 表、水面百分比、按边界长度连路、`generateMap` 收口；以及把 `CompleteCampaignScenario`
之外的入口（编辑器主菜单的「随机地图」）接到生成器上。
## ADR-275：P7 随机地图：编辑器主菜单的「随机地图」接上生成器

生成器能出图之后，让它真正被用户碰到：编辑器主菜单的 **New Random Map** 以前只记一句「生成器未移植，先给一张空白图」，
现在会**生成一张真地图**再进编辑器。

| 位置 | 改动 |
|---|---|
| `GameHost.HandleEditorMenuRequest` | `NewRandomMap` 分支先试 `CreateRandomMapScene(edgeLength)`，失败才 `??=` 回落空白图（原有行为保留） |
| `GameHost.CreateRandomMapScene` | 默认配置（两玩家、无水、镜像、正常资源、正常怪物）+ 种子 0（让生成器现摇）→ `RandomMapGenerator.Generate` → `EditorScene.CreateFromMap` |
| `EditorScene.CreateFromMap` | 新公开工厂：把内存里的 `Mp2Map` 交给编辑器（此前只有「读文件」与「空白图」两个入口） |
| `Game/Maps/RandomMapGenerator.cs` | 生成入口：`Generate(config, w, h, out terrain)`，按参考的规矩处理种子（大于 0 就用，否则现摇 0…999999），把「地形 → 地图」串成一次调用 |

**两处刻意的取舍**：

1. **配置对话框还没有**（`bd 4f2.2` 就是它），所以用默认配置并把种子取自时钟。参考是先 `updateRandomMapConfiguration()`
   再生成；等 4f2.2 落地，这里只是把 `new RandomMapConfig()` 换成对话框的结果。
2. **生成的图只有地形**：物件那一段（`4f2.1.3` 剩的部分）没做完，所以玩家起点只是「一块属于某色玩家的区域」，还没有城堡
   与英雄。日志把这一点写清楚（`N regions, M player starts`），免得把「地形对了」当成「图能玩了」。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6318 通过（295 + 441 + 3717 + 1865）。App 层没有测试工程，
端到端要带游戏数据跑：编辑器主菜单点「New Random Map」→ 日志里出现
`Generating a random map with seed N, 72x72 tiles.`、`The random map generator laid out 7 region(s) on a 72x72 map.`、
`The generated map has 2 player starts; the object phase is not ported yet…` 与
`Editor opened on a map built in memory (72x72, no file behind it).`，随后 Ctrl+S 保存出的 `.mp2` 应能被场景列表读到。

## ADR-276：P7 随机地图：预制落子的判定与占格（`4f2.1.3` 第七刀）

上一刀能让生成器出一张**有地形**的图，但没有物件。物件落子要回答两个问题——「这里放得下吗」与「放下去占了哪些格」。
参考把这两个问题写成 `canPlaceObject` / `markObjectPlacement` 两个自由函数，`4f2.1.3` 第三刀已经搬了前者的单物件版本
（`RandomMapObjectRules.CanPlaceObject`）；这一刀把**成组**的那一半补齐，落子循环因此只差「写进 `Mp2Map`」一步。

| 新增 | 参考 | 作用 |
|---|---|---|
| `SelectTerrainVariant(group, index, ground)` | `selectTerrainVariantForObject()` `:408-418` | 树前六种、山前八种按地带换块（`TreeIndexFor` / `MountainIndexFor`），其余原样 |
| `CanPlaceAllObjects(state, objects, position, ground)` | `canPlaceAllObjects()` `:689-695` | 一组摆放逐个换地形变体后再查 `CanPlaceObject` |
| `CanFitObjectSet(state, set, position, ground)` | `canFitObjectSet()` `:697-713` | 入口格必须仍为空地；障碍、奖品与守宝物的怪物都要放得下 |
| `MarkObjectPlacement(state, info, position)` | `markObjectPlacement()` `:715-727` | 地面层逐格标 `OBSTACLE`，带动作位的物件把锚点标 `ACTION` |
| `CanPlaceBorderObstacle(state, info, position)` | `canPlaceBorderObstacle()` `:660-687` | 装饰物专用：**允许压在已有障碍上** |
| `RandomMonsterPlacement` | `randomMonsterSet` `:74` | 每个预制都附带的那只 (0,0) 守卫怪 |

**三个值得记下来的判断**：

1. **同一个物件，两条规则用的是不同的谓词**。`canPlaceObject` 判「BORDER 能不能压」时用的是
   `MP2::isInGameActionObject`（英雄能不能访问），而 `markObjectPlacement` 判「锚点要不要留 ACTION」时用的是
   `MP2::isOffGameActionObject`——后者在 `mp2.cpp:650-653` 里**只是动作位测试**。端口里正好两个都有
   （`IsInGameActionObject` 与 `Mp2ObjectTypes.IsActionObject`），照抄，不合并：合并会让「带动作位但不可访问」
   的那批物件（`OBJ_EVENT` 一类）少标一个 `ACTION` 位，路就会从它身上穿过去。
2. **装饰物可以长在装饰物上**。`CanPlaceBorderObstacle` 的境界面比 `CanPlaceObject` 多一个 `OBSTACLE`，
   因为区域边缘的树丛本来就是一丛压一丛；`ACTION` 与 `PATH` 仍然拒绝，否则会盖掉可交互物件的站位或已修好的路。
3. **目录越界退成警告而不是断言**。参考在 `canPlaceAllObjects` 里断言编号永远合法（编号来自它自己的常量表），
   但端口的生成器可以被编辑器以任意设置驱动；越界时 `TryGetObjectInfo` 记一条 `GameLog.Warn` 并让整组判为放不下，
   一次坏编号不该让编辑器崩在菜单里。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6330 通过（295 + 441 + 3729 + 1865）。
新增 12 条测试：树的雪地变体落在 33、山按荒野换块、非分块物件保持编号；多格物件逐格占格、动作位不可被后来者覆盖、
影子不占格；`Prefabs[0]` 在开阔区域放得下、入口被堵 / 奖品被压 / 锚点贴地图边时放不下；
装饰物能压障碍但不能压路、仍然要求每个格子有主人；目录外编号返回 `false` 且不抛异常。

**还没做的**（仍是 `4f2.1.3`）：把预制 / 宝物 / 矿 / 城堡 / 英雄真正**写进 `Mp2Map`**（参考的
`putObjectOnMap` → `Maps::setObjectOnTile` + `Maps::addObjectToMap`，端口缺一个「`ObjectInfo` → 瓦片字段 + 附加项链」
的写法）、`decorationsPerGround` 表（约 330 行）、水面百分比、按边界长度连路。


## ADR-277：P7 随机地图：把物件写进 MP2 的瓦片（`4f2.1.3.1`）

物件阶段最后一块硬骨头：生成器终于能把目录里的一件东西**真的写进地图**。参考的链是
`putObjectOnMap()` → `Maps::setObjectOnTile()` → `placeObjectOnTile()`，但它写的是**运行时 `Tile` 的 `ObjectPart` 列表**，
MP2 字段是存档时由 `Map_Format` 的对象表反推的；端口没有 `Map_Format`（编辑器一直直接写 `Mp2TileInfo`），所以这一层翻译
必须自己写。新类 `Game/Maps/RandomMapObjectWriter.cs`：

| 目标 | 参考 |
|---|---|
| `TryWrite(map, position, ObjectGroup, index)` / `TryWrite(map, position, ObjectInfo)` | `putObjectOnMap()` `:761-796` |
| 逐瓦片分组 → 底/顶面片 + 附加链 | `placeObjectOnTile()` `maps_tiles_helper.cpp:195-334` |
| metadata 分支（矿的资源、神器的编号、怪物的数量） | `setObjectOnTile()` `:2214-2284` |

**形状是先看读入侧定下来的**，不是猜的：`Tile.Init()` 从瓦片取一个底面片（`ObjectName1`、`BottomIcnImageIndex`、
层在 `Quantity1` 低两位）和一个顶面片，再把 `EnumerateAddonChain()` 里的每一件追加成额外的面片，最后
`SortObjectParts()`。于是规则只有一条：**一格最多一个底 + 一个顶，其余进附加链**。底面片按层号升序选第一个
（`Object 0` → `Terrain 3`，与 `sortObjectParts()` 一致），影子因此会落到附加链上而不是抢走瓦片本体。

**四个决定**：

1. **一件物件只吃一个 UID**（`TakeObjectUid()` 一次），底/顶面片与每个附加件都带它——参考在 `placeObjectOnTile` 里
   就是这样；读回时这件东西还是一个整体。
2. **只写空瓦片**。参考能把新面片并进已有物件（`moveMainObjectPartToGroundLevel` 加那套「谁的类型说了算」的判断），
   但生成器的落子规则已经保证目标格是空地；把「非空」当调用方错误直接拒绝，比悄悄写坏一格便宜得多。据此那条
   「更靠上的面片不改写类型」的判断在空瓦片上恒不成立，类型规则收敛成两句：底面片里第一个有类型的说了算、
   动作位类型可覆盖；顶面片只在当前不是动作类型时覆盖（与参考逐字对应的部分都写进了代码注释）。
3. **metadata 按类型分流，不是照抄目录**。MP2 的瓦片只有一个 13 位字（`Metadata`），而物件目录的 `ObjectInfo.Metadata`
   一半根本不是文件字段：怪物存的是**怪物编号**、旗帜存的是颜色、神龛存的是法术（那几样是 fh2 存档自己的东西）。所以照
   `setObjectOnTile` 的分支写：怪物写 0（读图时 0 表示数量随机，守卫的实际数量由生成器自己算）、神器写编号、矿/锯木厂/
   炼金实验室写资源类型、魔法花园写 1、其余写 0。参考的 `metadata[1]`（矿的日收入）在文件里没有位置，读图时由
   `setInitialObjectInfo` 重算。
4. **「空瓦片」要按面片算，不能只看类型**。`Mp2TileInfo.IsEmpty` 只测 `ObjectType == None`，一片没有类型的装饰物
   照样能过——覆盖它就等于把它吞掉。`IsBlank` 同时要求没有底面片和顶面片。
5. **`TryGetObjectInfo` 提到 `internal`**，让写入侧与判定侧共用同一张目录查找和同一套越界警告。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6341 通过（295 + 441 + 3740 + 1865）。11 条新测试：树进底面片、
一件物件的所有面片共用一个 UID、顶面片进顶面片、同格第二件面片进附加链（层号小的当本体）、13 位 metadata 往返
（8191 上限）、动作类型写在瓦片上、怪物不把编号写进数量、只有装饰面片的瓦片不算空、出界的物件被拒且
`LastObjectUid` 不动、已占的瓦片被拒、**真实目录物件（`LANDSCAPE_TREES` 第 3 号）写完后 `WriteAll` 落盘 →
`Mp2Map.Read` 读回字段一致**。

**下一步**（仍是 `4f2.1.3`）：用这个原语把预制组、宝物、矿、城堡、英雄、守卫与装饰写进区域；
`decorationsPerGround` 表、水面百分比、按边界长度连路随后跟上。


## ADR-278：P7 随机地图：把预制组真正撒到地图上（`4f2.1.3` 第八刀）

三类原语（判定 / 写入 / 占格）齐了之后，接上参考的落子循环：新增 `Game/Maps/RandomMapObjectPhase.cs`。

| 成员 | 参考 |
|---|---|
| `PlaceSimpleObject(map, state, anchor, placement, ground)` | `placeSimpleObject()` `:958-973` |
| `PlaceTreasureGroup(map, state, region, set, anchor, strength, rng)` | `placeValidTreasures()` `:1080-1112` |
| `PlaceMonster(map, state, position, selection)` | `placeMonster()` `:936-956` |

一组预制按参考的次序落地：障碍 → 四个奖品（能力点原样放，宝箱位按剩余预算现摇）→ 守卫。每摇一个奖品，组预算就减掉
已放下的价值（`MinimumTreasureValue` 是地板，允许超支——参考注释写明这是故意的），最后 `region.TreasureLimit -= 组价值`，
守卫则按**这一组的总价值**查强度表。怪物条目靠 `ObjectInfo.Metadata[0]` 里的怪物编号反查（与参考的 `GetSpriteIndex()`
同一个顺序，四个「随机档位」也是表里真实存在的条目）。

**这一刀改了写入原语的三条规矩**，全部由测试逼出来——都是「参考允许、我原来禁止」的假冲突：

1. **一格可以合并**。参考的 `placeObjectOnTile` 把新面片**并进**已有格子再排序，不是覆盖。原来「只写空瓦片」的规矩让
   **守卫放不下去**：宝物的精灵有 `@-1,0` 的边缘片，正好压在锚点邻居上，锚点自己也被前一件宝物的影子占了。
   现在 `WriteTile` 会先读回该格原有的面片（`GroundPiecesOf`/`TopPiecesOf`，含附加链），与新面片一起按层号排序再落盘。
2. **两条前置只看锚点格**。`putObjectOnMap` 只对**锚点**判「一格不许两个动作物件」与「同类可交互物件不重复放」。
   我最初对每个覆盖格都判，结果预制里相邻的两件宝物（影子互相压）被判成冲突而整件丢弃。
3. **类型判定从「这一格已有的类型」起算**，再按参考的「动作位压过一切、顶面片只压非动作类型」两条走。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6348 通过（295 + 441 + 3747 + 1865）。新增 7 条落子测试：
一整组（三丛树 + 四件奖品 + 守卫）真的落到地图上、预算按组价值缩水、障碍占格且锚点变 `ACTION`、便宜的一组不设守卫、
同一棵树在草地与雪地上落到不同图、出界的物件整件被拒且不改格子、守卫站在 `ACTION` 格上（空选择一个也不放）；
写入侧另有两条改成新语义的测试（同类动作物件不重复放、装饰面片在物件落下时被保留）。

**还没做的**（仍是 `4f2.1.3`）：把这两段接进 `planObjectPlacement`/`placeObjectSet` 的**择位**循环（它们要求「先能修一条到
最近的路」才算过关，得先有修路那一段）、矿、城堡与英雄、`decorationsPerGround` 表、水面百分比、按边界长度连路。


## ADR-279：P7 随机地图：到最近那条路的路由（`4f2.1.3` 第九刀）

择位循环（`planObjectPlacement` / `placeObjectSet`）的硬门槛是「放下之后还能修出一条到最近的路」，修不出来整件撤掉。
所以修路不是收尾美化，是「能不能放」的一部分。这一刀搬的是搜索本身：`Game/Maps/RandomMapRoadBuilder.cs`
（`findPathToNearestRoad()`，`map_random_generator_helper.cpp:420-537`）。

| 要点 | 参考 |
|---|---|
| 带代价的 BFS，直走 100、斜走 150 | `defaultGroundPenalty`、`defaultGroundPenalty * 3 / 2` |
| 找到第一条路后继续推进 `roadBuilderMargin`（= 300） | `:443-446` |
| 只认「从直方向到达」的路 | `:448-453` |
| 只走本区域的 `OPEN` / `PATH` / `CONNECTOR` | `:483-491` |
| 从可交互物件出发第一步不许斜走、不许朝上 | `:461-464` |
| 斜走的两步不许拼成急转弯、`Bottom` 之后不许横折 | `:466-476` |
| 斜接路面补一格过渡路 | `:517-525` |

**三个照抄而不「优化」的地方**：一是**不用优先队列**——参考按入队顺序处理节点（它自己的注释也承认这不是严格最短路），
换成 Dijkstra 会让同一张种子生成出不同的地图；二是**找到路后继续跑一个 margin**，那几格的多余探索会改变后续节点的
到达次序，进而改变路径形状；三是结果**从路那一端往回走**，返回的列表是「路 → 起点」，补过渡格也插在路那一侧。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6354 通过（295 + 441 + 3753 + 1865）。新增 6 条测试：一条直路
（11 格、首格是路、末格是起点）、区域里没有路返回空、别的区域的路不算、障碍切断返回空、从 `ACTION` 格出发第一步不斜
不朝上、两个代价常量等于参考值。

**还没做的**（仍是 `4f2.1.3`）：接上 `planObjectPlacement`/`placeObjectSet`、矿、城堡与英雄、
`decorationsPerGround` 表、水面百分比。

**补完（同一天的第十刀）**：路由接上之后，择位与铺路也一起落地了，仍在 `RandomMapObjectPhase` 里：

| 新增 | 参考 |
|---|---|
| `ForceTempRoadOnTile(map, state, index)` | `forceTempRoadOnTile()` `:729-757` |
| `PlanObjectPlacement(state, region, sets, rng)` | `planObjectPlacement()` `:1014-1078` |
| `PlaceObjectSet(map, state, region, sets, strength, expectedCount, rng)` | `placeObjectSet()` `:1114-1176` |

两处值得记下的细节：

1. **规划什么也不改**。参考用两个 `MapStateTransaction`：外层在函数返回时把所有标记撤回，内层只在「这一组确实成立」时
   提交。端口的 `RandomMapStateTransaction` 本来就支持嵌套（按历史长度记录起点），照搬即可；测试用「整张图的
   `区域:类型` 快照前后一致 + `LastObjectUid` 不动」证明规划是纯的。
2. **路面先铺，物件后写**。`placeObjectSet` 在有路可修之后先 `ForceTempRoadOnTile` 写 `ROADS` 组第 2 号（直路）并标
   `PATH`，再让 `PlaceTreasureGroup` 落障碍、奖品与守卫。地面图自带装饰时先换成干净的变体（确定性版本，见 ADR-274）；
   这些路只是「路由成立」的凭证，真正的路面最后由 `updateAllRoads` 按相邻格重排。

**验证（第十刀）**：全量 6363 通过（295 + 441 + 3762 + 1865），构建 0 警告 0 错误。新增 9 条测试：临时路面写进瓦片、
障碍格不给路、自带装饰的地面被换成干净图、已有路不重写、规划不改格子、没有路的区域规划为空、没有预算的区域规划为空、
两组宝物连同路面一起落地、区域要 0 组时一个 UID 都不花。


## ADR-280：P7 随机地图：矿、选格与「先有路才落地」（`4f2.1.3` 第十一刀）

矿是第一个「自带入口」的物件：玩家走到矿前才占领，所以它**必须**有路可达。这一刀把这条规矩做成代码，并补上它需要的
三张表与三个函数。

| 新增 | 参考 |
|---|---|
| `RandomMapMines.ObjectInfoIndex/MineBlock/SawmillOf` | `fheroes2::getMineObjectInfoId()` 与两张换块表（`gui/ui_map_object.cpp:91-150`, `:328-366`） |
| `RandomMapTileSelection.FindTilesByType` | `findTilesByType()` `:578-587` |
| `RandomMapTileSelection.FindOpenTilesSortedJittered` | `:539-576` |
| `RandomMapTileSelection.PickEvenlySpacedTiles` | `:589-658` |
| `RandomMapObjectPhase.PlaceActionObject` | `placeActionObject()` `:797-822` |
| `RandomMapObjectPhase.FindTilesForPlacement` | `findTilesForPlacement()` `:975-1012` |
| `RandomMapObjectPhase.PlaceMine` | `placeMine()` `:897-912` |

**矿的图形是「资源 × 地面」**：目录里 `ADVENTURE_MINES` 正好 56 条，排法是八块矿（通用、草地、雪地、沼泽、熔岩、沙漠、
泥土、荒野）× 五资源 = 40，接着 8 座废弃矿、6 座锯木厂、2 座炼金实验室。参考在这里写着「若新增矿种请更新这段逻辑」的
警告，端口照抄并用「遍历所有地面与资源，索引必须落在 0…55」的测试把它钉住——顺带验证了目录真的就是 56 条。

**三个可复用的选择器**：按类型取格、按「离区域中心的抖动距离」分环、按「越散越好」的贪心挑格。抖动是给每个格子加一个
0…2 的随机量再分环，所以环与环之间会互相渗透，铺出来的东西不像同心圆；贪心挑格先算每个候选格到「被避开那些格」的最近
距离，每次取最远的那一个再刷新其余格子的距离。

**`placeActionObject` 与 `placeSimpleObject` 的分工**：前者「先要有路，才允许落盘」——先标格、再找路，路找不到就整件撤掉，
找到才写对象并把路铺好；后者只管把东西放下去。矿、城堡这类带入口的走前者。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6375 通过（295 + 441 + 3774 + 1865）。新增 12 条测试：56 条目录
自检、矿按地面分块与块内资源槽位、木/铁/水银/废弃矿各自那一块、所有（地面,资源）组合的索引都在目录内；按类型取格；
挑格先取最远的、要得多就全给；分环覆盖每个空地各一次、抖动把距离 4 的格子放在第 4…6 环；矿连同守卫（正下方一格）与
路面一起落地、修不出路的矿被跳过且不计数。

**还没做的**（仍是 `4f2.1.3`）：矿的整体投放（`mineCount`/`goldMineCount`/`secondaryMineCount` 与主矿的木铁优先）、
城堡与英雄起点、`decorationsPerGround` 表、水面百分比、区域连接与边界装饰、`generateMap` 收口。


## ADR-281：P7 随机地图：矿的整体投放与区域连接（`4f2.1.3` 第十二刀）

上一刀把「一座矿怎么落地」做完了，这一刀补的是**谁来点这个名**：玩家起点的木矿与铁矿，以及每个区域按面积与配置摊派
的副矿与金矿；顺带把步 7 的最后一段「区域连接」也搬了过来。新增 `Game/Maps/RandomMapMinePhase.cs`：

| 新增 | 参考 |
|---|---|
| `RandomMapMinePhase.PlacePrimaryMines` | `generateMap()` 步 5 的 `if ( region.type == STARTING )` 块（`:806-820`） |
| `RandomMapMinePhase.FillRegion` | 步 7 的矿投放（`:843-871`） |
| `RandomMapMinePhase.ConnectRegion` | 步 7 末尾的 `region.connections` 循环（`:873-880`） |
| `RegionSizeForSecondaryMines` / `RegionSizeForGoldMine` / `FirstPrimaryMineRing` | `:60-61`、`:812` |

**两次投放的次序就是策略**：步 5 先给每个玩家起点找一块木矿与一块铁矿——从离中心第 4 环以外的空地开始，一圈一圈往外试，
**找到第一块就换下一种资源**；这些位置进 `primaryMineLocations`，步 7 挑副矿时按「越散越好」躲开它们。这样玩家门口的
主矿不会被一座宝石矿挤掉，四种宝石矿也不会挤在一起。

**「小区域一座、中立区给满」**：`secondaryMineCount` 在 `regionSizeLimit > 250` 或区域是 `NEUTRAL` 时取配置里的
`mineCount`，否则只给 1。参考的注释解释得很直白：中立区是玩家要去抢的地方，再小也得有东西可抢；玩家自己的副矿按面积给，
免得小图上一步一矿。金矿另有 `regionSizeLimit > 350` 的门槛（`goldMineCount` 在 `SCARCE`/`NORMAL` 下本来就是 0）。

**连接排在矿之后**：连接格先标 `PATH`，再从那里修一条到最近路面的路。参考特意把它放在矿与城堡之后——先修路会让路从
矿与城堡要占的格子上穿过去。修不出路时连接格仍留在 `PATH` 上（参考也不撤回），等后面落的矿或宝物从旁边经过时接上。

**与参考的偏差**：参考在步 5 先摆城堡（`adjustRegionToFitCastle` 会把区域中心挪到城堡下方两格）、再摆主矿；端口还没有
城堡那一段，所以 `PlacePrimaryMines` 用的是未调整的中心。城堡落地时必须按参考的次序接在它前面，否则同一种子会摇出不同图。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6385 通过（295 + 441 + 3784 + 1865）。新增 10 条测试：非起点区域
不放主矿、起点区域木与铁各一座且都标成 `ACTION`、宽区域放满 `mineCount` 且四种资源轮流各一、小区域只放一座而小中立区放满、
金矿的 350 门槛、水面区域不放矿、没有区域归属的格子不放矿、连接格连同路面一起落地、修不出路的连接格留在 `PATH` 且不花 UID、
三个门槛常量等于参考值。

**还没做的**（仍是 `4f2.1.3`）：城堡与英雄起点、`decorationsPerGround` 表、水面百分比、缺的守卫怪与免费拾取物、
`updateAllRoads`、`generateMap` 收口。


## ADR-282：P7 随机地图：城堡与英雄起点（`4f2.1.3` 第十三刀）

上一刀的矿在等城堡：参考的步 5 先摆城堡（`adjustRegionToFitCastle` 会把区域中心挪到城堡下方两格），再摆玩家的木矿与
铁矿。这一刀把城堡补齐，新增 `Game/Maps/RandomMapCastlePhase.cs`。

| 新增 | 参考 |
|---|---|
| `RandomMapCastlePhase.PlaceCastle` | `placeCastle()` `:824-895` |
| `RandomMapCastlePhase.PlaceCastles` | `generateMap()` 步 5 的城堡分支 `:788-804` |
| `RandomMapCastlePhase.BasementIndex` | `fheroes2::getTownBasementId()`（`gui/ui_map_object.cpp:295-326`） |
| `Mp2Map.AddCastle` / `Mp2Map.AddInfoBlock` | 文件里那两张表与 72 条的城镇位置表 |
| `RandomMapObjectWriter.TryWrite(…, uid)` / `SetMetadata` | `Maps::setLastObjectUID()` 与瓦片的 13 位 metadata |

**一座城堡是五件东西加两段路**：地基（`LANDSCAPE_TOWN_BASEMENTS`，按地面挑第 0…7 块）、城镇本体（`KINGDOM_TOWNS`，
六族各「城堡/城镇」两版）、左右两面旗（`LANDSCAPE_FLAGS`，颜色 × 2）、城堡下方一格的英雄（`KINGDOM_HEROES`，每种颜色
七个精灵、最后一个是「随机英雄」）。参考让它们共用一个 UID——每次落子前把计数器退回上一个值——所以
`RandomMapObjectWriter` 多了个能传 UID 的重载；`TryWrite` 的校验仍然全在领号之前，被拒绝的落子不许弄脏
`LastObjectUid`（否则同一个种子会生成出不同的图）。

**文件里还有两处外挂**：城堡要进 72 条的城镇位置表（`Mp2Map.AddCastle`），颜色要写进它自己的 70 字节信息块；
英雄光站上瓦片不够，还要一段 76 字节的英雄块，否则 `World.ReadInfoBlocks` 根本不会为它建对象。两段块都由瓦片的 metadata
指过来（`Mp2Map.AddInfoBlock` + `RandomMapObjectWriter.SetMetadata`），而 **metadata 必须在铺路之后写**——`forceTempRoadOnTile`
会整张重写瓦片，路的目录数据里没有 metadata 字段。

**测试逼出来的三件事**：

1. **中立的颜色索引是 6，不是 0**。参考写的是 `neutralColorIndex = Color::GetIndex( PlayerColor::UNUSED )`，而
   `Color::GetIndex` 的默认分支回 6；0 是蓝色，也就是第一个玩家。端口一直把 0 当中立，于是玩家 0 的区域既算起点又算
   中立——这一刀把 `RandomMapTerrainGenerator.NeutralColorIndex` 改成 6，玩家区取 0…5、中立区取 6。
2. **`canPlaceObject` 不查地图边界，MP2 的瓦片却必须在地图里**。城堡的影子往左伸五格、屋顶往上伸三格，而
   `adjustRegionToFitCastle` 只保证四格余量（clamp 到 `[4, width-4]`）。第一版照抄，结果 72×72 的图上玩家区中心落在边上时
   整张图生成失败。端口按两件物件的实际包围盒把锚点收进地图（`TryFitIntoMap`），挪过之后重挑地基图（地面可能换了），
   并把区域中心跟到城堡下方两格，主矿的搜索环才不会错位。
3. **树冠可以盖在障碍上，地面层不行**。同一件物件「检查」与「写入」用的层不一样：`CanPlaceObject` 跳过影子与地表层，
   `RandomMapObjectWriter` 却把影子的面片也写进瓦片。装饰阶段因此能在障碍块上留下树冠——参考也是如此。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6412 通过（295 + 441 + 3811 + 1865）。新增 11 条测试：起点城堡
进了城镇表且主格的 metadata 指向 70 字节的块（主人、种族、城堡位与表一致）、英雄站在城堡下方一格且有自己的 76 字节块
（全零＝默认兵力）、两面旗一左一右且帧号不同、门口两格铺上路、中立城镇没有主人也没有英雄、水面格子不放城堡、小中立区
只留下中心的一格路、大中立区放一座无主城镇、起点城堡放不下时整趟生成失败、「生成 → 写盘 → 读回」之后城镇表与英雄表
都由文件重建，以及最终的一条端到端：生成的图落盘后用 `World.LoadMap()` 载入，每个玩家王国都有城有英雄、城的颜色就是
自己的颜色。

顺带修了两条装饰阶段的测试：它们原来把一条**一格宽**的纵向路当成「贴着已占格子的空地」，但参考的每一组景都占三到四格，
紧贴单格缝隙时整组都放不下（`canPlaceObject` 对 `PATH` 一律拒绝）。改成中间一小块 3×3 的障碍，语义不变，装饰才落得下来。

**还没做的**（仍是 `4f2.1.3`）：把矿、宝物、装饰按参考的次序接进 `RandomMapGenerator`、水面百分比与地形过渡、缺的守卫怪与
免费拾取物、`updateAllRoads`、`Mp2MapWriter` 落盘前的 `updatePlayerRelatedObjects`（地图头里的玩家颜色与种族）。
（后半段已在 ADR-283 补掉，只剩 `updateAllRoads`。）


## ADR-283：P7 随机地图：把物件阶段接进 generateMap（`4f2.1.3` 第十四刀）

前面的刀都是「单个阶段能单独跑」，这一刀把它们按参考的次序接进一次生成，并补上地图头。新增
`Game/Maps/RandomMapObjectPasses.cs` 与 `Game/Maps/RandomMapHeader.cs`。

| 新增 | 参考 |
|---|---|
| `RandomMapObjectPasses.Run` | `generateMap()` 的步 5–11（`:751-957`） |
| `RandomMapCastlePhase.PlaceRegionCastle` | 步 5 里「哪种区域放什么」的那段判断（`:788-804`） |
| `RandomMapHeader.Apply` | `updatePlayerRelatedObjects()` + `updateMapPlayers()`（`map_format_helper.cpp:1287-1490`）、名字与描述（`:973-978`） |

**次序就是这一刀的全部内容**：城堡（连同门口的路）→ 玩家区的木矿铁矿 → 连接格与边框树 → 副矿 → 区域之间的路 →
能力点组 → 宝物组 → 装饰 → 接口的守军 → 路边的免费资源。每一步都假设前面的已经占好格子：连接格先算出来路面最后才铺，
矿与城堡之后才连路（否则路从它们身上穿过去），宝物与装饰排在最后（只要有空地就能长）。

**宝物先规划后落**：`PlanObjectPlacement` 在不改地图的前提下算出哪些格子放得下整组，再用 `PickEvenlySpacedTiles` 从这批格子里
按「越散越好」挑 `treasureCount` 个真正落下去。挑选时整片区域都还是空的，不会因为前面落的组把后面的挤到角落。

**一个真问题：metadata 被邻居的精灵抹掉。** 一件宝物的影子常常落在相邻的格子上，而 `WriteTile` 原来无条件写
`MetadataOf(info)`——大多数物件是 0，于是正好压在**英雄格**上的影子把「这格指向哪个信息块」抹成了 0，读图时英雄瓦片
还在（`ActionHero`）、英雄对象却建不出来，`World` 的通行判定直接断言。改成 `MetadataOf` 返回 `uint?`，`null` 表示
「这一格原来的值留着」：只有真正带 metadata 的四种物件（怪物、宝物、矿、魔法花园）才写。

**地图头必须重算。** `Mp2Map.CreateBlank` 给的是「六个颜色都在局中」的空白头，生成器只造玩家数那么几个王国，不重算的话
`World` 会给没人坐的颜色也建王国。端口照参考从**物件**而不是从配置推：英雄的 `MINIHERO` 精灵给出颜色与种族，城镇两侧的
旗子（`FLAG32`，左侧那面 `index / 2` 就是颜色）给出「这座城归谁」。名字与描述也照参考写，但 MP2 的名字字段只有 16 字节，
「Random map 20261126」放不下，所以在内存里就按字段截断，免得盘上与内存不一致（参考用的是自己的编辑器格式，没有这个限制）。

**两种「读图时会变」的东西，端口保持原样**：英雄用的是每种颜色第 7 个精灵（参考的 `randomHeroIndex - 1`），所以
`PlayerRaces` 里那个玩家读出来是 `Random`，由读图时按王国补；不在局中的颜色，头里那六个种族槽仍是「骑士」，只有
`KingdomColors` 能区分。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6421 通过（295 + 441 + 3820 + 1865）。新增 9 条测试：生成的图上有矿、
有装饰、有宝物、有守军、有路边资源；同种子同物件（逐格的类型、两层面片与 metadata 都比）；生成 → 写盘 → 读回后物件
与 UID 计数一致；每一格最多一件动作物件；36×36 也生成得出来；只有配置里的玩家在局中；每个玩家都有种族；名字与描述带上
种子与配置；头在往返之后（在局颜色范围内）不变。


## ADR-284：P7 随机地图：路面重排（`4f2.1.3` 第十五刀，收口）

择位时铺的路都是为了「这个位置修得出路」而放的同一条直路。最后一遍把它们全部改写成真正的直/弯/三叉/十字：
`Game/Maps/RandomMapRoads.cs`（`updateAllRoads()`、`getRoadObjectIndex()`、`updateRoadOnTile()`，
`map_format_helper.cpp:858-882`、`:1848-1897`）。

| 新增 | 参考 |
|---|---|
| `RandomMapRoads.UpdateAll` / `UpdateTile` / `ObjectIndexAt` | `updateAllRoads()`、`updateRoadOnTile()`、`getRoadObjectIndex()` |
| `RandomMapState.Roads` + `RandomMapRoadPlacement` | 对象表里 `group == ROADS` 的那一条 |
| `RandomMapObjectWriter.Remove(map, uid)` | `removeObjectFromMapByUID()` |
| `RandomMapObjectWriter.TryWriteRoad` | `placeObjectOnTile()` 的「地图外的面片跳过」分支（`:233-239`） |

**「有没有路」必须按对象表算，不能按画面算。** 一件路面物件除了锚点那一格，还会把接头面片写到相邻格（`ROADS` 第 0 号
就有五片）。相邻格因此会画出路面，但它**没有路**——参考的 `doesContainRoad()` 查的是对象表。端口把「哪一格真的有路」
记进 `RandomMapState.Roads`（tile → UID + 目录编号），方向掩码只数这个集合，于是掩码与处理顺序无关、也就与参考一致。

**旧件按 UID 整件撤掉，新件用同一个 UID 写上。** 参考在 `updateRoadOnTile()` 里把 UID 计数器退回去再写，就是为了让对象表
里的 id 一直有效。端口照做：`Remove(uid)` 扫整张图、挑出属于这个 id 的所有面片（锚点 + 撒在邻居上的接头），把剩下的重新
铺一遍；`TryWriteRoad` 再用同一个号写上。副作用是 `LastObjectUid` 一次都不动——测试直接钉住了这一点。

**地图外的面片跳过，而不是整件拒掉。** 一个方向掩码带的接头最多铺到锚点周围五格，而地图最后一排照样得有路。参考在那里写的
是「Ignore this tile since it is out of the map」并 `continue`。端口因此多了一个 `TryWriteRoad` 入口：只有它跳过越界面片，
其它物件越界仍然整件拒绝（那是落子逻辑出错，`RandomMapObjectWriterTests` 一直钉着这条）。

**两处刻意的偏差**：一是精灵变体 `mask + 256 * n` 的 `n` 用生成器而不是参考的全局 `Rand::Get( 1 )`——同一种子才画得出同一张
图（ADR-274 的同一立场）；二是城堡门口那一格（`ROADS` 第 512 号）按「正上方是城镇」判定，参考查的是对象表里的
`KINGDOM_TOWNS` 条目，端口查瓦片上的城镇类型，等价。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6430 通过（295 + 441 + 3829 + 1865）。新增 9 条测试：直路中间那格
掩码是「左 | 右」、路的一端只连进来的那一侧、孤立的路掩码为 0、城镇正下方那格用城堡门口那一条、没有路的格子不被邻居带偏、
重排后 UID 与计数器都不变、重排两遍不再改动、`Remove` 只撤掉那一件（同一格的路面还在、动作位没了）、以及端到端：
生成的图上每一格的编号都等于按它现在的邻居算出来的那个，且至少有一格是真正的岔路口。


## ADR-285：P7 随机地图：生成配置对话框（`4f2.2`）

参考在生成前先开一个配置对话框（`Maps::updateRandomMapConfiguration()` → `fheroes2::randomMapGeneratorDialog()`），
玩家调完参数才生成。这一刀把它搬过来：`UI/Dialogs/RandomMapGeneratorDialog.cs`（模型）与
`UI/Dialogs/RandomMapGeneratorView.cs`（布局、输入、绘制），并在 `GameHost` 里接上编辑器的「新建随机地图」。

| 新增 | 参考 |
|---|---|
| `RandomMapGeneratorDialog` | `Configuration` 的六个旋钮 + 两条夹取规则（`:74-86`、`:216-227`） |
| `RandomMapGeneratorView` | 六行的布局、三根滑块、种子框、两个按钮（`:96-195`） |
| `RandomMapGeneratorView.ProcessEvents` / `Show` | 循环体与模态转帧（`:198-251`） |
| `RandomMapGeneratorDialogText` | `_( "rmg|..." )` 那几句，默认英文、可按 msgid 覆盖 |
| `GameHost.TryShowRandomMapDialog` | `EditorInterface::updateRandomMapConfiguration()` |

**两条夹取是模型的事**：玩家数夹在 2…6；水量不超过 `calculateMaximumWaterPercentage( 玩家数, 边長 )`。玩家数一变，上限跟着变，
水量再被夹一次、水量的滑块范围也一起改小——参考在 `playerCountSlider` 分支里同时做这三件事，端口把它们收进
`SetPlayerCount`，界面只负责在值变了之后调 `SetRange`。

**布局那一行照抄成「假的」**：参考画了 `DROPLISL` 的底与按钮，然后把整块压一层暗色并把按钮 `disable()`，因为生成器只会
`MIRRORED`（`map_random_generator.cpp:658` 的 TODO）。端口一样画、一样暗、一样禁用；模型的 `SetLayout` 留着，等下拉框真的
做出来再用。

**一处行为改动**：以前「新建随机地图」失败会退化成一张空白图，现在对话框取消、美术缺件、生成失败三种情况都**停在主菜单**
（`bd 4f2.1.3` 那次留下的空白图兜底不再需要）。生成失败本身就是「不该留下一张没法开局的图」的结论，拿空白图顶替反而是另一
种错误。配置与参考一样在多次打开之间保留（`GameHost._randomMapConfig`）。

**美术从归档读**：`STONEBAK`（石面）、`METALLIC_BORDERED_TEXTBOX_GOOD`（标题框）、`TRADPOST`（三根滑块）、`TOWNWIND`（种子框）、
`DROPLISL`（布局那一行），按钮复用主菜单的 `SettingsArtwork`。缺任何一张就记一条警告并停在主菜单——`TryCreate` 给
`null`，不会画出半个窗口。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6445 通过（295 + 441 + 1880 + 3829）。新增 15 条测试：玩家数夹取、
水量不超过 36×36 的上限（53%）、玩家越多上限越小、连点玩家数滑块把水量上限与滑块范围一起推小、水量推到头正好是上限、
取消没有配置、接受保留六个值、种子的上下界、缺帧就没有窗口、六行的偏移、两个按钮在两角、「没有引擎时窗口也画得出来」
（有游戏数据的机器上才真跑）、ESC 取消、回车接受、布局那一行动不了。


## ADR-286：P7 随机地图：存进 maps 目录并接进开局（`4f2.3`）

生成的图要在选关列表里看得见、点得开，才算「生成完了」。参考的编辑器把新图写进 `maps` 目录，而
`Maps::getValidMaps()` 每次都扫那个目录；端口的主菜单把列表缓存在内存里，所以存完还得让缓存失效。新增
`Game/Maps/RandomMapArchive.cs`，并在 `GameHost` 里接上。

| 新增 | 参考 |
|---|---|
| `RandomMapArchive.FileNameFor` / `Save` / `ResolveDirectory` | `World::saveMapMP2()` 的整份写 + `Settings::GetMapsDirectories()` |
| `RandomMapArchive.Describe` | 编辑器存图后打的那行 `xx castle(s), yy add-on(s)` |
| `RandomMapGenerator.Generate( …, out uint seed )` | `generateMap()` 里 `config.seed > 0 ? seed : Rand::Get( 999999 )` 的答案 |
| `MainMenuScene.InvalidateMapList` | 参考每次开选关屏都重新扫目录 |

**文件名带种子**：`random_map_<seed>.mp2`。同一个种子重新生成会覆盖自己（不会越存越多），不同种子互不干扰。种子由生成器
回报（新增的 `out uint seed` 重载）——**不能从地图名字里读**，因为 MP2 的场景名字段只有 16 字节（ADR-283），
`Random map 20261126` 存下来只剩 `Random map 2026`，读回去会读错、进而把两张不同的图写进同一个文件。

**保存失败不影响生成**：目录不可写（只读安装）或写盘出错时只记一条警告，编辑器照常打开那张图——玩家至少还能在编辑器里
继续改、另存到别处。图在内存里已经是完整的，磁盘只是它的第二个去处。

**「可见」与「可开局」是两条链路**：可见走 `MapCatalog.Scan`（读文件头，按人数过滤），可开局走 `Mp2Map.Read` +
`World.LoadMap`（建城镇、英雄与信息块）。两条都由测试钉住：`TheSavedMapIsVisibleInTheScenarioList` 用
`MapCatalog.ReadAll` 验证前者，`TheSavedMapCanBeStarted` 从文件读回来喂给 `World`，断言两个玩家的王国都有城有英雄。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6453 通过（295 + 441 + 1880 + 3837）。新增 8 条测试：文件名格式、
`maps` 目录的创建与幂等、存下来再读回（尺寸/城堡/名字一致）、出现在 `MapCatalog` 的列表里（含文件路径与两个玩家颜色）、
读回来能 `World.LoadMap` 且两个玩家都有城有英雄、同种子覆盖同一文件、不同种子两个文件、描述里带上尺寸与文件名。






## ADR-287：P8 编辑器：九个仪器与两级物件页（`a7l.1` 第一刀）

参考的面板有九个仪器、四把物件刷子。这一刀先补齐这两件：`EditorTool` 从三个值扩到九个，面板的九个格子按参考的
顺序（`editor/editor_interface_panel.h:171-186`，那里写着「这个枚举对应资产里图标的顺序，不要改」）逐个选中仪器。
新增 `Game/Editor/EditorObjectBrushes.cs` 与 `Game/Editor/MapObjectCatalogPresets.cs`。

| 新增 | 参考 |
|---|---|
| `EditorTool` 的九个值（Terrain…Erase） | `EditorPanel::Instrument`（`editor_interface_panel.h:171-186`） |
| `EditorObjectBrushes.Landscape` / `Adventure` / `Kingdom` / `Monsters` | `LandscapeObjectBrush`、`AdventureObjectBrush`、`KingdomObjectBrush`（`:205-235`） |
| `MapObjectCatalogPresets.For` | 按照分组填物件页的 `getObjectsByGroup()` |
| `RandomMapObjectWriter.TryBuildTemplate` | 目录条目 → 落子模板（与生成器共用同一套面片换算） |

**两级页面照参考**：物件仪器先选「哪一类」（山、石头、树……），页面里才有物件；换类别清掉下一层的选择，否则会
「显示矿、落子却是宝物」。四把刷子各自记住自己的选择。

**落子模板复用生成器**：`TryBuildTemplate` 把目录条目变成「放在空地上长什么样」的瓦片加附加链，走的是生成器的
`Collect` 与面片换算，所以编辑器摆出的物件与生成器摆出的完全一致，不维护第二份换算表。模板按分组缓存：一个分组
可以有几百个条目，每次画面板都重搭会明显拖帧。

**验证**：构建 0 警告 0 错误；全量 6463 通过（295 + 441 + 1880 + 3847）。新增 10 条测试（`EditorInstrumentTests`）：
仪器顺序、四族分组与参考一致、只有四个物件仪器有刷子、目录条目数与顺序、模板带对象类型、选类别清空下层、选物件
成为刷子、越界选择不改状态、四把刷子各自记忆、任意族都能落子。


## ADR-288：P8 编辑器：十一类擦除（`a7l.1` 第二刀）

参考的橡皮不是一个「清空这一格」的开关：面板上有十一个格子，每一类物件可以单独排除（`ObjectErasureType`，
`editor/editor_interface_panel.h:249-275`；点一下翻一位，`editor_interface_panel.cpp:1383`）。这一刀把「擦什么」
从「全部」变成可勾选的十一位，擦除页也照参考画出来。

| 新增 | 参考 |
|---|---|
| `EditorEraseType` / `EditorEraseTypes.Buttons` | `ObjectErasureType` 的十一位与 `_eraseButtonObjectTypes`（`:282-292`） |
| `EditorEraseTypes.Groups` | `getEraseObjectGroups()`（`:268-320`） |
| `EditorEraseTypes.Name` | `_getEraseObjectTypeName()`（`:902-930`） |
| `MapObjectCatalog.GetObjectGroupByIcn` / `GetGroundGroup` / `GetTopGroup` | 参考按物件表查 `group`；端口只有面片，于是反查 |
| `MapEditSession.EraseTypes` / `SetEraseTypes` / `ToggleEraseType` | `_eraseTypes`、`_eraseTypes ^= …`、默认 `ERASE_ALL_OBJECTS`（`:337`） |
| `EditorPanel` 的擦除页（十一个 27×27 图标 + `TOWNWIND` 第 11 帧的勾） | `_eraseButtonsRect` 与那十一个图标的取法（`:436-447`、`:600-631`） |

**一位不止一个分组**：`ERASE_LANDSCAPE` 管水面与杂物两个分组，`ERASE_ADVENTURE_NON_PICKABLE` 管五个，而
`ADVENTURE_WATER` 同时属于「可捡」与「不可捡」——照参考原样重叠，全选是十七个分组而不是十一个。

**类别从面片反查**：参考给每个物件记着 `group`，而 MP2 的瓦片只有 ICN 类型与精灵序号。`MapObjectCatalog` 在建
`objectInfoByIcn` 的同一趟里多记一份「这张图属于哪个分组」（先注册者胜，与那个索引同一条规则），橡皮就按这一格的
地面层、顶层面片各查一次。查不到分组的部件按参考的做法跳过——参考也只删它认得的物件。

**一格两半**：一格可以同时有地面层与顶层部件，两者类别未必相同，所以只清掉勾中的那一半。清地面那一半时附加链的
引用一并断开（链上的面片都盖着地面物件的 UID），物件类型按剩下的那一半重算。参考的编辑器模板只落地面层（顶层
部件都在附加链里），因此这条路径真正管用的是从文件读进来的地图。

**图标不另画**：十一个图标全部取自 `EDITPANL`——第 6、7、8、10、13、16、17 帧，以及第 1 帧上裁出的四块
（城镇、可捡的冒险物件、怪物、英雄）；勾用 `TOWNWIND` 第 11 帧。与地形页、物件页同一个办法：只用资产里有的图，
缺图就退回文字。

**没做的**：右键提示与长按「只留这一类」（`editor_interface_panel.cpp:1386-1402`）没有对应的输入通道，先只做左键翻位。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；全量 6473 通过（295 + 441 + 1880 + 3857）。新增 10 条测试：位值与
参考一致、页面顺序、十一类各覆盖哪些分组（含重叠与全集十七个）、名称、默认全选、翻位只翻一位、一位都不勾时什么也
不删、只删勾中的类别（未勾时一步也不进撤销历史）、只清勾中的那一半、目录认不出的部件留着不动。另改一条旧测试：
「橡皮按面积清格」改成先用真物件落子再擦，因为手搭的字节没有分组、按参考的做法不会再被删。

## ADR-289：P8 编辑器：详情仪器的三个模式与搬移/复制（`a7l.1` 第三刀）

参考的详情仪器不是「点开物件」一件事：面板上有三个模式，编辑（点开物件自己的窗）、搬移（按住拖走）、复制（按住拖出
一份），`DetailBrushType`（`editor/editor_interface_panel.h:269-278`）。这一刀把三个模式和搬移、复制都落地。

| 新增 | 参考 |
|---|---|
| `EditorDetailMode` / `EditorDetailModes` | `DetailBrushType` 与 `_getDetailModeTypeName` 的名字、说明文案 |
| `MapEditSession.DetailMode` / `SetDetailMode` | `_selectedDetailBrushType`、三个详情按钮（`.cpp:1272-1280`） |
| `MapEditSession.GrabObject` / `ReleaseObject` / `DetailSource` | `_movableObjectInfo` 的一按一放（`.cpp:2906-2935`） |
| `MapEditSession.MoveObject` / `CopyObject` | `_tryToMoveObject` / `_tryToCopyObject`（`.cpp:1675-1780`） |
| `EditorPanel` 的详情页（`EDITPANL` 18/19/20 + `TERRAINS` 第 9 帧黄框） | `.cpp:523-529` |

**搬的是什么**：端口的一格有地面层与顶层两个部件、一段附加链、两个 UID，而参考的物件是一组「主格 + 附属格」。搬移就
按格记录搬：两个部件的类型与图号、层号、数量、物件类型、UID 原样写进目标格，源格清成空件（`ObjectType` 也归
`None`），两张格的地形图与地形位都不动。多格的物件在端口里本来就只占一张格记录（其余格子的面片挂在附加链上，见
ADR-045），整件东西跟着走。

**附加链：搬不复制，复制才复制**。搬移时目标格直接指向原来那条链，链上的面片一个不增——搬的是同一件东西，UID 不变，
链仍旧属于它。复制时目标格拿新 UID，链整条按原顺序复制一份（每张面片挂新 UID），否则两张格会共用一条链，改其中
一个的旗子会连带改另一个。

**目标格已有同样的东西就拒绝**：参考在目标格找到同组同序号的物件时提示 "The same object exists on the tile." 并放弃。
端口没有分组序号可比，比的是画出来的东西（两个部件的类型与图号、物件类型）；数量与元数据不比——那是同一件东西的
状态，不构成「另一件」。拖回原格、拖到地图外、空格按下都算空操作，一个字节也不写。

**一次搬移是一笔历史**：源格与目标格各自 `Record` 到同一条 stroke 里，撤销一次就把两格都还原。

**详情页没有图**：参考画的那三帧（`EDITPANL` 18/19/20）在本仓库那套六页 `EDITPANL` 里不存在（地形、擦除图标、
Cell Details、Streams、Roads、Erase），于是这一页退回三行文字（`1 Edit` / `2 Move` / `3 Copy`，选中行加框），热区
同步换成行；拿到有那三帧的档案仍走图标。这与擦除页「缺图退回文字」是同一条规矩。

**编辑模式开的是信息框**：参考左键点开物件自己的属性窗（城堡、事件等），那些窗口属于后面的 issue，端口先打开物件
信息框——同样的时机、同样的入口，等属性窗落地再换。

**没做的**：流与路的拖拽铺设（参考走 `Map_Format` 的物件模型与方向重算，端口只有 MP2 格记录，要先定道路/河流在端口
里怎么表示）；系统按钮里的放大镜（View World）、规格、文件、菜单，分别属于 `a7l.2` / `a7l.3` / `a7l.5`。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；Core 295、Assets 441、Game 3868、UI 1879 通过（UI 的
`SkillBarsTests.PrimaryFull_Redraw_PaintsThePlateOfEverySkill` 在全量并行跑时偶发失败一次，单跑通过，与本刀无关）。
新增 `EditorDetailTests` 11 条：模式编号与顺序、名字与说明、默认编辑模式、`Apply` 不改编地图、空格抓不到东西、
换工具/换模式丢掉手上的物件、搬移带附加链且地形不动、搬移是一笔撤销、复制拿新 UID 并复制整条链、同物件拒绝、
地图外落空。像素基线 `tests/baselines/editor.png` 完全一致；`--editor-panel-click "121,24;72,122"` 的截图确认详情页
画出来并选中 Move。

## ADR-290：P8 编辑器：道路与河流两条仪器的拖拽铺设（`a7l.1` 第四刀）

参考的 Roads / Streams 不是在格子上涂贴图，而是改地图的物件模型：一格有路，就把周围格的路按方向掩码重新排一遍
（`getRoadObjectIndex` → `setRoadOnTile` → 对八个邻居 `updateRoadOnTile`），河流同理但只看四边（`getStreamDirecton`
→ `addStream` → `updateStreamObjectOnMapTile`）。端口只有 MP2 格记录，所以先定死「路 / 河在端口里是什么」，再照
参考的顺序改它。

| 新增 | 参考 |
|---|---|
| `Editor/EditorPaths.cs` 的 `LayRoad` / `LayStream` | `setRoadOnTile` / `addStream`（`maps/map_format_helper.cpp`） |
| `RoadObjectIndex` / `RedrawRoad` / `MatchesRoadLayout` | `getRoadObjectIndex` / `updateRoadOnTile` |
| `StreamDirection` / `StreamSprite` / `RedrawStream` | `getStreamDirecton` / `getStreamIndex` / `updateStreamObjectOnMapTile` |
| `MapEditSession.LayRoad` / `LayStream` / `LayPath` | 两条仪器的左键（`editor_interface_panel.cpp` 的 Roads / Streams 页） |

**路在端口里就是一张地面面片**：`GroundIcnType = Road`，`BottomIcnImageIndex` 是物件索引 0..511 —— 索引本身就是八
方向位掩码，表尾的 512 是城堡门口那条引道。一件路会把面片撒到相邻格去（接头），所以「这一格有路」不等于「这一格
的记录里有 `Road`」，也不等于「只有这一格有这件的面片」。判定照参考的 `isSpriteRoad`：邻格伸过来的接头面片不算这一
格自己有路，否则一条路会把整条街都点亮。

**铺一格 = 写自己 + 重画邻居**：`LayRoad` 先按掩码给这一格挑一件写下去（掩码由周围八格有没有路算出来），再把八个
邻居的路按新掩码拆了重铺，拆与铺用同一个 UID——路还是那条路，只是接头变了。河流只重画四边，因为它没有对角面片。

**重铺之前先比整件**：掩码 0 与掩码 8 的锚点贴图都是第 2 帧，只比锚点会把「该补接头」误判成「已经对」。`MatchesRoadLayout`
按格比整件：锚点周围 3x3 里属于这个 UID 的路面片（记录里那张加上附加链里的）要与目录里那一件的部件一一对上，多
一张少一张都重铺。道路面片的偏移都在一格之内，扫 3x3 就够。

**一次拖拽是一笔历史**：路面改的格子不只在指针下——拆旧路会扫全图，新铺的一件又只带一个 UID。所以每一步先按指针
存下窗口（路 2 圈、河 1 圈），改完把窗口与改前比，差多少记多少；同一步里同一格被改两次也只留第一次的值。
`LayPath` 把这一步塞进拖拽已经开着的那条 stroke，并置 `_addonsChanged`（路面往附加链里塞件，存盘不能走「只补地形」
的近路）。

**河只在旱地上走**：水格拒绝、已经有河的格子跳过，与参考一致。旱地上的花草石头会被抹掉——河把地形图换成「素」的
那一张，参考是 `Ground::getRandomTerrainImageIndex(ground, false)`；端口取那张素图而不是随机挑一张，所以同一次拖拽
画出来的图每次一样。

**两处「随机」都取第一个**：参考每个掩码有变体、直段与孤头各有两个河图，端口一律取第一种，理由与地形变体一样——
重画与撤销要能还原成同样的贴图。

**没做的**：三角洲（delta）河段；`Map_Format` 物件模型里道路以外的连带（如 `isSpriteRoad` 之外的图块分类）。

**验证**：构建 `FHeroes2Sharp.slnx` 0 警告 0 错误；Core 295、Assets 441、UI 1880、Game 3882 通过，合计 6498。
新增 `EditorPathsTests` 14 条：空地第一段路的孤段贴图、两段并行互相接头、重画后 UID 不变、城堡门口的引道、
引道旁边那条照常接、接头面片不算「这一格有路」、一条路一笔撤销（连伸进邻居的接头面片一起收回）、一次拖两格
是一笔撤销、河流的孤段、两段并流成直段、水上不铺、已有河不重铺、河抹掉格上的花草石头、地图外落空。
像素基线 `tests/baselines/editor.png` 一致（有一次跑到指针那一格带着画笔地形，重跑与手动同参渲染都与基线逐字节
相同，判定为偶发，未再复现）。

## ADR-291：P8 编辑器：历史管理器的元数据表与编辑器的选项窗（`a7l.2`）

参考的 `HistoryManager` 有两级粒度：`GenericMapAction` 存整图的快照，`MetadataMapAction` 只存「某一本按对象编号
存的数据表」的前后副本——改一个城堡的名字不必复制整张地图。端口的 `HistoryManager` 早就转完了，只有非整图那一支
抛 `NotImplementedException`，因为 MP2 里没有这套表。这一刀把表补上，并把 `editor_options.cpp` 的选项窗转出来。

| 新增 | 参考 |
|---|---|
| `Editor/MapObjectMetadata.cs`（九个结构与 `MapObjectMetadataStore`） | `Maps::Map_Format` 的九本 `*Metadata`（`map_format_info.h:62-419`） |
| `ActionCreator` 的非整图分支 | `history_manager.cpp:167-202` 的 `switch` |
| `UI/Dialogs/EditorOptionsDialog.cs` | `editor_options.cpp:113-233` 的 `openEditorOptionsDialog()` |
| `OptionItem.DrawAnimation` / `DrawPassability` | `ui_option_item.cpp` 的 `drawAnimationOptions` / `drawPassabilityOptions` |

**九本表照原样搬**：城堡、英雄、狮身人面像、路牌、地图事件、「几选一」物件、怪物堆、神器、资源堆，键都是物件 UID，
与 `Mp2Map.TakeObjectUid` 同源。用有序字典而不是哈希表，跟参考的 `std::map` 一样按键遍历：存档要按编号顺序写，
撤销要能稳定地比出前后两份。

**只有四个结构比内容**：参考给城堡、英雄、狮身人面像、地图事件写了 `operator==`（容器逐元素比），其余五个没有。
端口照办——那四个实现 `IEquatable` 并逐元素比、哈希也跟着一致，其余五个只做数据容器。窗口判断「作者到底改没改」
靠的就是这层比较。

**提交失败要留在手上**：`MapAction` 添了 `Prepare(uint)`，整图动作拿它存「改完」的快照并记下 UID 计数器，元数据动作
忽略这个参数只拷贝字典。`Commit` 只有在 `Prepare` 成功时才把动作交给管理器，失败就留在手上等析构撤回——这是参考
`commit()` 与析构的配合（`history_manager.cpp:205-217`），端口原来的写法失败时会把动作丢掉、什么都不撤回。

**选项窗是九格三行**：语言、图形、音频；热键、对象动画、通行性；界面风格、光标、滚动速度。窗口 289×(77×3+52)，
与系统选项窗同一套格子。四个「开一页」的（语言、图形、音频、热键）把动作交回调用方；另外五个也只上报，由调用方改
设置再重画——参考在 `openEditorSettings()` 里正是这么做的，因为动画与通行性开关要重画地图、界面风格要重建面板。
滚速的图标照参考写死成两格宽（`ui_option_item.cpp:63`），这一行偏偏是三格；顺手把参考的 `Passabiility` 拼写修正了。

**没做的**：`openEditorSettings()` 的循环与面板系统按钮的接线。它要编辑器场景的事件泵与重画入口，跟另外三个系统
按钮一起在 `a7l.7` 落；这一刀只交出窗口本身与它需要的两个绘制助手。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1888、Game 3885 通过，合计 6509。新增 `EditorOptionsDialogTests`
8 条（缺帧不开窗、窗口尺寸、九格坐标与行距、四个开页动作、五项只上报不动设置、滚轮走档、回车/取消关门、右键说明），
`EditorHistoryManagerTests` 增 3 条（九种动作各自进自己那本表并撤销重做、未知动作类型被拒、四个结构逐元素相等）。
像素基线 `tests/baselines/editor.png` 一致。

## ADR-292：P8 编辑器：物件说明弹窗的文字与第十本元数据表（`a7l.3` 第一刀）

`a7l.3` 是三张窗：城堡详情（53 KB）、地图规格（119 KB）、物件说明（12 KB）。这一刀先做最小的那张——它不依赖另外
两张，也不依赖城堡与地图窗口的组件。

| 新增 | 参考 |
|---|---|
| `Editor/EditorObjectPopup.cs` | `editor_object_popup_window.cpp:57-267` |
| `MapObjectMetadataStore.CapturableObjects` 与 `CapturableObjectMetadata` | `map_format_info.h:297-300` 的 `capturableObjectsMetadata` |

**文字是「认领」出来的**：参考拿这一格的主部件去物件表里比对，认得资源堆就报「几个什么」，认得神器就报名字，认得
矿洞就报矿种加归属，认不得就报物件类型名。端口的地图是 MP2 记录，一格里只有地面部件与顶层部件的 ICN 与图片号，认
领的办法照旧，只是拿地面部件比；物件表还是同一份 `MapObjectCatalog`。

**两边类型要抹平**：物件表记的是带动作位的类型（`ActionResource`、`ActionAlchemistLab`），MP2 格上记的是不带的那
一个。直接比永远不相等，所以两边都过一遍 `Mp2ObjectTypes.NonActionType`。这一点是被用例逼出来的——第一版把类型当
成相等，资源堆的用例得到的是「Resource」而不是「Wood」。

**矿洞比最后一个部件**：矿洞的矿种写在背景层的最后一个部件上，参考比的就是它（`:159-160`）；其余物件比第一个。

**没有「主格」这一步**：参考先算 `Tile::getIndexOfMainTile` 再到那一格上取文字，因为部件的偏移只存在运行时的瓦片
里。端口的格式层没有偏移，但数量、归属、矿种都按物件 UID 取，同一件物件的每一格拿到的是同一份数据，所以直接描述被
点的那一格；末尾的 `[x; y]` 本来也是被点的那一格。

**认不出不抛**：参考在这里是 `assert(0)`，release 构建里断言是空的、于是退回物件类型名。端口按 release 走——弹窗
是只读的帮手，不该因为目录里没有这件东西就让编辑器崩掉。

**补上第十本表**：上一刀（ADR-291）说「九本表」，漏了 `capturableObjectsMetadata`。它没有动作类型，参考的注释写明
了原因（`history_manager.h:80-82`）：改归属要连地图上的旗子一起重画，所以走整图快照那一档。端口照办：表在
`MapObjectMetadataStore` 里，`ActionCreator` 不为它开分支。

**没接线的**：显示层。参考在 `mouseCursorAreaPressRight` 里用 `displayStandardPopupWindow` 弹出这段文字；端口的
编辑器右键现在是「平移 + 抬笔吸地形」，右键语义本身要等 `a7l.6`（编辑器输入收进 `LocalEvent`）一起改，所以这一刀
只交出文字。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1888、Game 3895 通过，合计 6519。新增
`EditorObjectPopupTests` 10 条：路与空地的说法、资源堆带不带数量、怪物单复数、怪物没记数量时退回类型加复数名、神器的
两行、矿洞的矿种与归属、可占建筑的归属、关卡与帐篷的颜色名、其它物件退回类型名、地图重载与两个空参数。渲染未变动，
未重跑像素基线。

## ADR-293：P8 编辑器：城堡详情窗的一格（`a7l.3` 第二刀）

参考的城堡详情窗 1065 行，最大的一块是「一格」——`BuildingData`（`:79-307`）：一条升级链的读写、点击怎么移动
两个等级、这一格怎么画。窗口本体（网格布局、名字输入、英雄与军队、法师公会的法术表、两个子窗口）在下一刀。这一刀
交出格子与它要的素材表。

| 新增 | 参考 |
|---|---|
| `UI/Widgets/CastleBuildingCell.cs` | `BuildingData`（`editor_castle_details_window.cpp:79-307`） |
| `UI/Widgets/CastleBuildingCellArtwork.cs` | `redraw()` 里那几张 `Assets::getImage()`（`:201-273`） |

**两个等级都是 -1 起算**：`_builtId` / `_restrictedId` 是 `int8_t`，-1 表示「没有」，所以 0 是「第一级」。端口照搬，
`BuiltLevel` 的判断是 `>= 0` 而不是 `!= 0`，`BuiltChain` 也按这条线展开。

**掩码里禁了好几级时记最高那一级**：构造时从链的底往顶扫，每遇到一个在掩码里的就覆盖一次，所以给一本「第 2 级到第
5 级都禁」的掩码，记下来的是第 5 级。这一条一开始写反了（以为是记最低的），被用例抓出来。

**建与禁互相让路**：往上建时若顶到禁线，把禁线往上推一格（顶到链尾就取消禁）；往上禁时若压到已建，把已建压到禁线
之下。这段就是参考 `queueEventProcessing()` 的两个分支，没有别的语义。

**链走到底就停**：`getUpgradeForBuilding()` 约定「没有更高一级就返回自己」，所以循环条件直接比建筑本身。

**用例挑法师公会当链**：骑士的第一住所（农民小屋）根本没有升级，用它会得到长度 1 的链、用例什么也证明不了；法师公会
五级、每个种族一样，正好当长链。

**两张灰图在素材表里一次压好**：参考每次重画都复制一张再 `ApplyPalette`（`:228-229`、`:251-253`），结果一样，省掉
每次重画的两份复制。

**画法没有单测**：这一格要出像素，靠 `tests/baselines/editor.png` 检查，等下一刀把它接进窗口时一起看。素材表是私有
构造 + `required`，测试造不出假件，跟 `ConstructionArtwork` 一样。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1900、Game 3895 通过，合计 6531。新增
`CastleBuildingCellTests` 12 条：链长与初值、掩码读最高一级（已建/已禁各一条）、两个掩码各归各的、往下建并回绕、
往上禁并回绕、建不能越过禁（禁线被顶上去）、禁会压下已建、`Reset`、格子与两个半格的坐标、绘制拒绝空参数。
渲染未变动，未重跑像素基线。

## ADR-294：P8 编辑器：城堡元数据与军队、建筑掩码的换算（`a7l.3` 第三刀）

上一刀交出了城堡详情窗里的一格，这一刀交出格子吃的东西：城堡与英雄的元数据怎么写进运行时对象、运行时对象又怎么写
回去。参考在这块是 `maps/map_format_helper.cpp` 里的一串自由函数，端口一个都还没有。

| 新增 | 参考 |
|---|---|
| `Game/Editor/MapFormatArmy.cs` | `getBuildingsFromVector`（`:1749-1757`）、`setDefaultCastleDefenderArmy`（`:1759-1767`）、`isDefaultCastleDefenderArmy`（`:1769-1772`）、`loadCastleArmy` / `saveCastleArmy`（`:1774-1787`）、`loadHeroArmy` / `saveHeroArmy`（`:1789-1803`）、`loadArmyFromMetadata` / `saveArmyToMetadata`（`:75-99`） |

**两个「默认」哨兵不一样**：城堡的守军「类型全为负」表示按种族配，英雄的军队「类型全 `<= 0`」表示没改过——因为英雄
那一排的 0 是空槽。这两个判断分开写，混用会让「作者清空了军队」和「作者没碰过」变成同一件事。

**负数按空槽读，不断言**：参考在 `loadArmyFromMetadata` 里对着 `-1` 写 `assert`，端口把它当空槽——元数据可能来自手改
过的地图，读的时候宽容一点比让编辑器崩掉好。

**写回按排的长度走**：参考断言军队与两排等长；端口按排的长度遍历，军队更短时多出来的槽写 0。

**类型名撞了命名空间**：`Army` 类在 `FHeroes2.Game.Army` 命名空间里，而 `FHeroes2.Game.Editor` 是同一个根的兄弟，
裸写 `Army` 会先解析成命名空间。文件里用 `using ArmyStack = FHeroes2.Game.Army.Army;` 绕开，和 UI 那边处理同名类型
的办法一致。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1900、Game 3906 通过，合计 6542。新增 `MapFormatArmyTests` 11
条：建筑列表并为掩码、空列表、已建与禁用各取各的、新城堡不是默认守军、默认守军写 -1/0、默认守军不动军队、自定义守
军逐槽装载、写回两排、负数按空槽、英雄无自定义军队不装、英雄军队往返。渲染未变动，未重跑像素基线。

另记一笔：全量首轮跑时 `SkillBarsTests.PrimaryFull_Redraw_PaintsThePlateOfEverySkill` 偶发失败一次，单独跑与整库复跑
均通过，与本刀改动无关（本刀只动了 Game 里的新文件）。

## ADR-295：P8 编辑器：法师公会法术表与十九格布局（`a7l.3` 第四刀）

这一刀把窗口本体里不依赖像素与事件的两块拆出来了：法师公会子窗的法术表，和十九格建筑的位置与显示开关。

| 新增 | 参考 |
|---|---|
| `Game/Editor/MageGuildSpellTable.cs` | `mageGuildSpellsDialog()` 的表格与写回（`:340-361`、`:449-483`、`:487-529`、`:532-545`） |
| `Game/Editor/CastleDetailsLabels.cs` | `getCastleName()`（`:309-316`）、`getMageGuildTitle()`（`:496-512`） |
| `UI/Dialogs/CastleDetailsGrid.cs` | `buildings[]` 与它们的 `setPosition` / 显示开关（`:729-768`、`:645`、`:683`、`:670-675`） |

**格子数只看种族能力**：参考传的是 `hasLibraryCapability`（法师族或随机族），不是「这张图上图书馆建了没有」，所以随机族
的城堡在预览里也按六格排。端口 `Castle.HasLibraryCapability` 只认 `Wizard`，那是运行时城堡的语义（随机族早就定下来了），
编辑器不能复用它——`MageGuildSpellTable` 自己按参考判 `Wizard or Random`。

**占位符写回等于删除**：格子里是「第 N 级随机」时，参考在 OK 里把这格从 `mustHaveSpells` 删掉，而不是存一个占位符值；
`Spell.Level()` 对占位符返回 0，正好是这条判据。

**禁用列表是一串编号**：原存储不分等级，打开禁用子窗时才按等级摊成五个容器、确认后拼回一串。这个往返做成一对静态方法，
子窗真的接进 UI 时直接用。

**十九格里队长不在网格里**：前十八格三列六行、行距 75，第一行与第四行之后多 5 像素；队长单独钉在右侧面板
`(438+33, 232)`。行距靠循环累加算，不写死成查表，免得以后改行高时表与代码对不上。

**「town」是没建要塞的那个**：`isTown` 判的是 `builtBuildings` 里**没有** `BUILD_CASTLE`，同时决定名字框的占位提示、
军队标题与「允许建城堡」勾选框；这条容易被读反，注释里写清了。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1909、Game 3919 通过，合计 6564。新增
`MageGuildSpellTableTests` 13 条（格数随种族、每排格数、空表全是占位符、按元数据装、写回与幂等、取消钉选会删条目、
禁用换占位符、同列表不算改动、禁用不影响别级、分组往返、非公会法术被忽略、名字占位、公会标题）与
`CastleDetailsGridTests` 9 条（十九格与队长、亡灵神龛、行列坐标与两处 5 像素、越界、队长位置、两本掩码读数、
town 判定、默认建筑开关、禁建要塞）。渲染未变动，未重跑像素基线。

## ADR-300：城堡详情窗的事件处理（`a7l.3.2`）

新增 `UI/Dialogs/CastleDetailsInput.cs`，对应 `editor_castle_details_window.cpp:844-1059` 里除绘制之外的全部逻辑：
光标停在哪个控件上、状态栏说什么、点了之后改什么、什么时候关窗。

| 新增 | 参考 |
|---|---|
| `Process(LocalEvent)` | 每帧一次的整条判断链 |
| `Cursor` / `CursorSlot` | 光标所在的控件与建筑格序号 |
| `Message` | 已解析好的状态栏文字，没指着任何东西时是 `Castle Settings` |
| `CastleDetailsRequest` | 交回宿主的四件事：改名字、弹提示、开法师公会、让军队条吃事件 |
| `CastleDetailsOutcome` | OKAY / EXIT 的返回码 |
| `ResolveUnsavedChanges(bool)` | 退出确认的第二次问答 |

**模态对话框一律交回宿主。** `Dialog::inputString`、`showStandardTextMessage`、`mageGuildSpellsDialog` 与
`armyBar.QueueEventProcessing` 在端口要么不存在、要么在 `FHeroes2.App` 那一层。硬在这里调会让这一层离不开引擎，
也没法单独测。所以 `Process` 一次只处理「这一帧作者想干什么」，要弹窗就发 `CastleDetailsRequest`，宿主做完再回来问
`Outcome`——和第 5 阶段的 `CastleScreenInput.TakeAction` 是同一个套路。

**退出确认因此要分两步。** 参考里 EXIT 一按就同步弹框、同步拿结果；端口先 `TryDiscard()`：没改动直接
`Discarded`，有改动只发一条 `ShowMessage`，等宿主弹完框调 `ResolveUnsavedChanges(discard)` 才定结局。

**「有没有改动」靠深拷快照。** `CastleDetailsState` 构造时留 `_onOpen = metadata.Clone()`，`HasUnsavedChanges()`
先 `WriteMetadata()` 再和快照比。为此给 `CastleMetadata` 补了 `Clone()`——参考的拷贝构造对 `std::vector`、`std::map`
是逐元素拷贝，浅拷的话快照会跟着元数据一起变，答案永远是否。

**判断链的顺序是有意义的**：OKAY 与 EXIT 排在最前（点下去这一帧不再处理别的），RESTRICT 与 SET SPELLS 的点击在其后，
然后才是一串 `else if` 的悬停链：名字 → 复位名字 → Allow Castle（只有城镇）→ Default Buildings → RESTRICT →
Default Army（只有中立）→ 复位军队 → 军队条 → SET SPELLS → OKAY → EXIT → 十九格。挨个 `if` 而不是串起来的话，
同时压着两个控件会触发两件事。

**建筑格的 `Cycle` 改成返回 `bool`**：参考的 `BuildingData::queueEventProcessing` 返回 `needRedraw`，调用方据此把
「默认建筑」标记摘掉；只在真点了格子时才摘，光悬停不摘。

**用例上的三个坑**（都是跑出来才知道的）：
1. 命中判定读的是**绘制时算出来的区域**，所以每个用例都必须先 `Paint` 一帧再 `Process`，否则所有区域都是空的。
2. 夹具必须是**真实的城镇**（`Tent` 已建）。拿一个空空如也的 `CastleMetadata` 去测，`WriteMetadata()` 会把 `Tent`
   补进去，于是「打开窗口什么都没动」被算成有改动，EXIT 立刻要确认框——问题不在被测代码，在夹具不真实。
3. 军队条要先用 `MapFormatArmy.LoadCastleArmy` 从元数据读进条里。参考也是这么做的；塞一个空军队进去，写回时会把
   元数据里的守军清掉，同样伪造出「有改动」。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1954、Game 3919 通过，合计 6609（较上轮 +17）。新增
`CastleDetailsInputTests` 17 条（空闲文案、名字/复位/两个勾选框/RESTRICT/军队条/OKAY/EXIT 的悬停与点击、EXIT 的
两步确认、点格子改建筑并摘掉默认标记、右键格子的描述、以及有/无改动的判定）。

**还没做**（挪到 `a7l.3.7`，等编辑器宿主）：按钮的按压态绘制、`DEFAULT_OKAY`/`EXIT`/`CANCEL` 热键、以及把
`CastleDetailsRequest` 接到真正的模态对话框上。

## ADR-299：城堡详情窗的整块绘制（`a7l.3.1` 第二刀）

`a7l.3.1` 收尾：新增 `UI/Dialogs/CastleDetailsView.cs`，把 `castleDetailsDialog()` 的画面从背景到状态栏文字
整个串起来（`:619-810`）。`CastleDetailsFrame` 继续管窗口壳与底部按钮，这一层管壳里的东西。

| 新增 | 参考 |
|---|---|
| `Paint()` | 背景 → 名字 → SET SPELLS → 两个勾选框 → RESTRICT → 军队条 → 建筑格 → 底部按钮 → 状态栏文字 |
| `PaintAllowCastle()` / `PaintDefaultBuildings()` / `PaintArmy()` | `:664-722` 的三个勾选框与 army bar |
| `PaintBuildings()` | 19 格 + `BLDGXTRA` 队长框 `:751-768` |
| `PaintStatusMessage()` | 状态栏消息 `:801-810` |
| `CastleDetailsFrame.StatusBarMessageArea` | `statusBar.setRoi()` 那条没有高度的条 |
| `StatusBar.Reset()` | 端口专用：整块重绘后作废消息缓存 |

**最大的坑：那三个按钮在存档里根本不存在。** 我照着参考的 `ICN::BUTTON_OKAY_TOWN` / `BUTTON_GUILDWELL_EXIT` /
`BUTTON_RESET_GOOD|EVIL` 去 `TryReadIcn`，四个全部返回空。它们在 `game_assets.cpp:933` 与 `:1886` 是**运行时合成**的：
OKAY 与 EXIT 是 `EMPTY_GUILDWELL_BUTTON` + 文字，RESET 是 `EMPTY_GOOD/EVIL_BUTTON` + 文字、底纹取 `STONEBAK`。
端口对应 `ButtonFactory.GetTextAdaptedSprite(text, DialogButtonTemplate.GuildWell | Good | Evil, stone)`。
**以后遇到 `ICN::` 里带 `<按钮>` 字样的编号，先去 `game_assets.cpp` 确认它是不是现场合成的。**

**这个坑被测试藏了一轮**：素材读不到时 `TryCreate` 返回 `null`，两处的辅助方法都写成「`TryCreate` 是 null 就跳过」，
于是 8 条城堡详情用例静默通过。现在两个夹具都在「存档在、但拼不出来」时抛 `InvalidOperationException`——
有数据却画不出窗口是真问题，不该当作「这台机器没装游戏」放过去。

**三个测试侧的坑**（都是跑出来才发现的）：
1. `Sprite.Transform[y * Width + x] == 1` 的像素不会被画上去，拿源图逐像素对拍时必须跳过，否则会拿透明色当期望值。
2. 名字条盖住右侧面板头 19 行，抽行对拍右半时要避开，不然比的是 `CASLBAR`。
3. `BOTTOM_RIGHT` 对齐的是**活动区底边**（`dialogRoi.y + dialogRoi.height - 24`），不是状态栏上沿；两者差一个
   状态栏高度，我第一版按状态栏上沿写，差 19 像素。

**`StatusBar.Reset()` 是端口专有的**：参考的状态栏贴在显示层上，只有它自己改那块像素，所以记住上一条消息就够；
端口每帧整块重绘窗口，`CASLBAR` 会被重新盖一遍，缓存不作废的话同一条消息第二次就画不出来。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1937、Game 3919 通过，合计 6592（较上轮 +8，且这 8 条
是真正执行过的——上一轮它们在静默跳过）。新增 `CastleDetailsViewTests` 8 条（三个勾选框的位置与「废墟没有
Allow Castle」「已建要塞没有 Allow Castle」「有主城堡没有 Default Army」、名字写进名字条、状态栏消息在整块重绘后
仍能重画、`SetMessage` 的内容真的落到条上）。

## ADR-298：城堡详情窗的窗口框（`a7l.3.1` 第一刀）

`a7l.3` 拆成六条子 issue 之后，先在 `a7l.3.1` 上把窗口的「壳」立起来：新增
`UI/Dialogs/CastleDetailsArtwork.cs` 与 `UI/Dialogs/CastleDetailsFrame.cs`，对应
`castleDetailsDialog()` 的 `:619-798` 里与具体控件无关的部分。

| 新增 | 参考 |
|---|---|
| `CastleDetailsArtwork.TryCreate(assets, isEvil)` | `:625-643` 的取图 |
| `CastleDetailsFrame.CreateWindow()` | `StandardWindow( 640, 480, false )` `:619` |
| `PaintBackdrop` | 两半背景 + 两条分隔线 + 名字条 `:619-646` |
| `PaintName` | `drawCastleName()` `:648-655` |
| `PaintSpellsButton` / `PaintRestrictButton` | `:657-662`、`:685-689` |
| `PaintBottomButtons` | 两个复位按钮 + OKAY/EXIT + 状态栏 `:770-798` |

**满屏窗口 = 608x448 的活动区**：参考的 `StandardWindow( DEFAULT_WIDTH, DEFAULT_HEIGHT, false )` 给的是窗口尺寸，
活动区要减掉两侧各 16 像素的边框。端口照此 `Create(new Rect(16,16,608,448), renderBackground:false)`，
`NameArea`/`StatusBarArea` 都从这个活动区推，不硬编码 640/480。

**两半背景用的不是同一种拷贝**：左半是 `Blit`（尊重透明），右半、两条分隔线与状态栏是 `Copy`（不透明）。
端口分别对应 `ImageOps.BlitSpriteClipped` 与 `ImageOps.Blit(PixelBuffer, …)`。混用会让右半出现背景色透出来的洞。

**状态栏必须最后画**：它填在 OKAY 与 EXIT 之间，画早了就被按钮盖住；参考的顺序也是先 `buttonOkay.draw()`、
再 `Copy` 两段 `CASLBAR`。所以 `PaintBottomButtons` 把三件事绑在一起，不拆。

**文字自适应按钮的板子只切一次**：切一次要读三份素材，循环里每帧重切太浪费；但每帧仍要走
`StandardWindow.RenderButton` 重画（窗口每帧整块重绘，阴影不重画就丢了）。`Spells`/`Restrict` 的
`ButtonSprite` 也只在第一次创建。

**用例写法上的坑**：`PixelBuffer` 没有 `Get(x, y)`，读像素用索引器 `buffer[x, y]`。绘制用例是拿存档里的
`CASLWIND`/`STONEBAK`/`CASLBAR` 逐像素对拍，比「和背景色不同」这类弱断言可靠得多（石头底本身几乎是一个颜色）。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1929、Game 3919 通过，合计 6584（较上轮 +8）。新增
`CastleDetailsFrameTests` 8 条（满屏活动区、名字条与状态栏几何、两半拼在 438 列、两条 4 像素分隔线、名字条取
`CASLBAR` 第 17 列、OKAY/EXIT 与两个复位按钮的位置、SET SPELLS 与 RESTRICT 的居中位置且不重复切板）。

**仍未做**（同属 `a7l.3.1`）：四个勾选框与勾选标记、名字输入框、军队条与旗帜、十九个建筑格与队长框、状态栏
提示文字、`PaintName` 之后的绘制顺序接线。事件循环在 `a7l.3.2`。

## ADR-297：`StandardWindow` 的三个按钮渲染助手（`a7l.3` 第六刀）

补齐 `ui_window.cpp:433-474` 里 `StandardWindow` 剩下的三个成员：

| 新增 | 参考 |
|---|---|
| `RenderButton(button, output, released, pressed, offset, padding)` | `renderButton()` `:433-444` |
| `RenderOkayCancelButtons(okay, cancel, output, 四张图)` | `renderOkayCancelButtons()` `:446-457` |
| `RenderTextAdaptedButtonSprite(button, output, text, offset, padding, isEvil, background)` | `renderTextAdaptedButtonSprite()` `:459-474` |

**为什么要先补这三个**：编辑器剩下的三个窗（城堡详情、法师公会、地图规格）都要在窗口里摆按钮——固定尺寸的
OKAY/EXIT、名字与军队的复位小按钮、以及按文字宽度长出来的 SET SPELLS / RESTRICT。参考这三个助手都长在
`StandardWindow` 上，端口一直缺。

**素材由调用方递进来**：参考按 `ICN` 编号自己取图，这个仓库没有全局图形库，所以签名换成收 `Sprite`。文字自适应那个
只需要「主题 + 可选背景纹理」，因为 `ButtonFactory.GetTextAdaptedSprite()` 已经把 `getTextAdaptedSprite()` 整个搬过来了。

**三步的顺序不能换**：先 `SetSprite`（它按新尺寸重算命中区），再 `SetPosition`，再画阴影（用的是释放态的样子，
偏移 `(-5,5)`），最后 `Draw`。参考在这三步里读写同一个 `_output`，阴影如果画在按钮之后就会被按钮盖掉一半。

**用例踩到的算术坑**：`_getRenderPos()` 的底部对齐是 `Height - itemHeight - offset.Y`，所以 300x200 的活动区、
27 高的按钮、7 像素的下边距落在 `y=306`，我第一版按 `140+200-27-7` 推成 313（把「减 27 再减 7」算成了「减 14」之外
的东西），跑一遍才改对。底部对齐别再手推，直接照着公式写。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1921、Game 3919 通过，合计 6576（较上轮 +3）。新增
`StandardWindowTests` 3 条（按 padding 定位、OKAY/EXIT 落在下两角、文字自适应按钮按标签长度长宽并落在指定偏移）。
第一轮全量测试里 UI 有一条用例失败，随后的三次单跑与一次全量跑都通过，判为偶发（未留下用例名，无法复现）。

## ADR-296：P8 编辑器：城堡详情的窗口状态与写回（`a7l.3` 第五刀）

上一刀交出了格子的位置，这一刀交出窗口里会变的东西与它最后怎么写回地图：
`UI/Dialogs/CastleDetailsState.cs`，对应 `castleDetailsDialog()` 的 `updateMetadata()`（`:810-842`）、
「默认建筑」勾选框（`:927-950`）、「默认守军」勾选框（`:962-979`）与复位按钮（`:981-991`）。

**状态必须搬出勾选标记**：参考的四个开关就是四个 `MovableSprite` 的显隐（`isHidden()`），勾选标记自己
就是状态。端口没有 Display 层，勾选标记不能当状态用，所以开关搬进 `CastleDetailsState`，画的时候读它。

**town 只判一次**：`isTown` 在参考 `:645` 用当时的已建掩码算一次，之后哪怕作者在格子里把要塞建起来也不重算，写回时
照样塞 `BUILD_TENT`。这是参考的行为，照抄并在注释里写明，免得后来者「顺手修正」。

**建筑两个列表是整体重建**：先清空，再放「town 的帐篷 / castle 的要塞」，再放禁建的要塞，最后仅当
`CustomBuildings` 为真时追加每格的升级链与限制位——所以勾回「默认建筑」时元数据里只剩要塞那一条，这正是参考的意思。

**两个勾选框切过去会顺手改东西**：切回「默认建筑」把十九格全部复位（回不到刚挑的建筑）；勾上「默认守军」把军队清空
（地图里要存那串负数哨兵，军队那一排显示空槽才自洽）。取消勾选都不复原。

**新元数据的守军不是「默认」**：`DefenderMonsterType` 初始是 0 而不是 -1，所以新城堡打开时「默认守军」是没勾的——
一开始以为哨兵是默认值，用例先写错了一次，改成断言「没勾」。

**验证**：构建 0 警告 0 错误；Core 295、Assets 441、UI 1918、Game 3919 通过，合计 6573（较上轮 +9）。新增
`CastleDetailsStateTests` 9 条（开关初值、要塞与有主、town 写回帐篷加禁建、castle 保留要塞、自定义建筑写链与限制、
切回默认复位格子、勾默认守军清空军队、复位军队、十九格布局含队长）。

**仍未做**：`castleDetailsDialog()` 的画与事件循环本身（背景拼接、军队条、名字输入、状态栏、OK/EXIT），
`mageGuildSpellsDialog()` 的画与事件。两处都还有前置：前者要 `StandardWindow` 的全屏尺寸与按钮素材表装配，后者要
`editor_spell_selection.cpp`（`Editor::openSpellSelectionWindow`，端口完全没有）与 `SpellsInOneRow` 控件。显示层仍等
`a7l.6`。

## ADR-301：编辑器法术选择窗（`a7l.8`）

`editor_spell_selection.cpp` 全文落地，拆成四件：

| 新增 | 参考 |
|---|---|
| `UI/Widgets/EditorSpellSelectionGrid.cs` | `SpellContainerUI`（`:51-167`） |
| `UI/Dialogs/EditorSpellSelectionArtwork.cs` | 素材装配（`:207-259`） |
| `UI/Dialogs/EditorSpellSelectionWindow.cs` | 满屏窗的布局、整块绘制与状态 |
| `UI/Dialogs/EditorSpellSelectionInput.cs` | 事件循环（`:279-416`） |

**名字必须带 `Editor`**：`UI.Dialogs` 里早有一个 `SpellSelectionWindow`（英雄学法术的列表窗）。不加前缀就撞名——真的
撞过一次，`write_to_file` 把那个文件覆盖了，靠 `git checkout --` 才捞回来。新建类型前先 grep 定义，别信「这个名字应该
没人用」。

**一个等级一次打开**：参考按等级按钮是 `break` 出循环再 `return true`，由调用方拿新等级重开（`:324-326`）。所以窗口
不重建内容，只改 `Level` 并回 `LevelChanged`，宿主自己新建一个——省掉窗口里「重算格子」的第二条路径。

**整块重画取代 `ImageRestorer`**：参考只重画改动过的一小块（`:365-415`），端口每帧 `Paint` 全窗。省掉一整类「忘了
restore」的 bug，代价是每帧多画十几个精灵，而满屏窗本来就要整块交给 `display.render( background.totalArea() )`
（`:276`）。

**两个 Toggle 与五个等级按钮的素材存档里没有**：`BUTTON_TOGGLE_ALL_*`、`BUTTON_1_GOOD..5_GOOD` 由
`ButtonFactory.MakeButtonSprites` 现场合成（空文本或数字 + 86x25/46x25 + STONEBAK 边框），Toggle 的图案再叠 h2d 的
`toggle-all-*-{released,pressed}.image`；h2d 缺席时退化成空板子，位置与尺寸不变（ADR-299 的「运行期合成」同一套路）。

**`pickDisabledSpells` 是取反**：参考 `:191` 是「在清单里 `!= pickDisabledSpells`」。第一版照直觉写成「在清单里就算
选中」，用例立刻红——调用方给的清单可能是禁用清单，也可能是启用清单，全靠这个布尔区分。

**禁用的法术是同一张卷轴过灰度调色板**（`:105-106`），图标同理（`:119-125`）；等级按钮的启用态跟着「选中的法术够不够
最小数」走（`:399-412`）。五处右键说明与法术说明一律发 `EditorSpellSelectionRequest` 交回宿主，和 ADR-300 的
`CastleDetailsRequest` 同一套路。

**测试夹具要还原而不是置空**：这些用例要临时把 `GameAssets.Current` 指到游戏数据上。第一版结尾写的是
`Current = null`，单跑全绿，跑全量时同一个集合里的 `SkillBarsTests` 红了——它排在后面，拿到的资源被前一个用例清掉了。
改成「记住进来时的值、走的时候放回去」（`AdventureMapTests` 的写法），一次就过。

**验证**：构建 0 警告 0 错误；全量 6627 通过（较上轮 +18，新增 `EditorSpellSelectionTests`）。其中三条是布局公式的
纯算术用例（九张出三列、十一张的最后一行右移半格、二十五张报错），其余走真实游戏数据。

**还没做**（`a7l.9`）：接进编辑器宿主的模态循环——`editor_interface.cpp:2490/2523` 两处调用、`DEFAULT_OKAY`/
`DEFAULT_CANCEL` 热键、`ShowMessage`/`ShowSpell` 接真正的弹窗、以及选中结果回写 `selectedSpells`/`spellLevel`。

## ADR-302：法师公会法术子窗（`a7l.3.3`）

`mageGuildSpellsDialog()` 落地，三个新件 + 两处小改：

| 新增 | 参考 |
|---|---|
| `UI/Dialogs/MageGuildSpellsArtwork.cs` | 石头底/状态栏/OKAY/EXIT 借用 `CastleDetailsArtwork`，只补公会建筑、`TOWNWIND` 两种卷轴、BANNED SPELLS 按钮、法术图标 |
| `UI/Dialogs/MageGuildSpellsView.cs` | 布局与整块绘制（`:331-403`） |
| `UI/Dialogs/MageGuildSpellsInput.cs` | 事件链、三处提示、请求与回写（`:437-600`） |

**控件不用新写**：`SpellsInOneRow` 就是 `a7l.8` 里那个 `SpellScrollRow`，连 <code>setCoords()</code> 的中心对齐都一模一样，
直接复用。行是表的视图——`Paint` 每帧从 `MageGuildSpellTable` 重建五排，表是唯一真相，省掉 `redrawCurrentSpell()` 那条
「只重画一格」的路径。

**改了两处既有代码**：`MageGuildDialog.DrawGuildBuilding` 加了一个「位置 + 裁切高度」的重载（编辑器没有
`MageGuildLayout`，参考传的是 `dialogRoi.getPosition()` 与 `Display::DEFAULT_HEIGHT`），旧重载转手调它；
`MageGuildSpellTable` 补了 `IsPlaceholder()` 与 `GetExcludedSpells(levelIndex, slot)`——参考 `:413-425` 那张排除集，
占位符永远排掉（连当前那一格也是），其余只排别人的。

**模态还是留在宿主**：点格子发 `SelectSpell`（带等级、当前法术、排除集），点 BANNED SPELLS 发 `SelectBannedSpells`
（带该等级标题与清单），宿主开完窗调 `ApplySelectedSpell` / `ApplyBannedSpells` 塞回来。参考里禁用清单是
「确认就生效、Escape 才退出」的 while 循环（`:515-555`），端口把这个循环也交给宿主，`ApplyBannedSpells` 每调一次等于确认
一次。

**两个被用例纠正的直觉**：卷轴坐标给的是「这一排的中心」，`Coords[i].X` 是减掉半宽之后的左上角；排除集在「当前格是
占位符」时是**整排**都被排掉，不是「槽位数减一」。

**验证**：构建 0 警告 0 错误；全量 6645 通过（较上轮 +18：`MageGuildSpellsTests` 15 条、`MageGuildSpellTableTests` 补 3 条）。

**还没做**（`a7l.3.1`）：把子窗接进 `CastleDetailsInput` 的 `SetSpells` 请求上——宿主侧的循环、等级切换后的重开、以及
`MageGuildSpellsRequest` 的三种弹窗落地。

## ADR-303：地图规格窗的数据半（`a7l.3.4`）

`mapSpecificationsDialog()` 的状态与写回，一个新增件 + 三处小改：

| 改动 | 内容 |
|---|---|
| `Game/Editor/MapSpecsState.cs` | 名称、描述、难度、三种颜色、两个条件与参数、两个胜利开关；`TogglePlayerColor`、两个 `Update*Condition`、`CreateInfo` |
| `Mp2Map.Info` | `init` → `set`：头部只有一个，换了就是换了，不进撤销历史 |
| `MapEditSession` | 新增 `ApplyMapInfo`（拒收不同尺寸的头部）与 `_headerChanged`，`IsDirty` 认它 |
| `Mp2MapWriter.Save` | 新增 `writeHeader` 参数；补丁式保存本来只改地形，不写回头部就丢掉新名称 |

**为什么状态是可变类而不是 record**：参考直接改 `MapFormat`；MP2 头部是 `Mp2FileInfo` 记录，于是做一层可变状态，
`CreateInfo()` 生成新头部、`MapEditSession.ApplyMapInfo` 装回去。安装只此一条路径（会话负责脏标记与写回），
`CreateInfo` 本身不改地图——`WriteBack` 那种「生成 + 就地装回」会让会话的脏标记漏掉头部。

**四处 MP2 装不下 fh2m 的折衷**（都写进了类注释）：胜利/失败目标只有坐标（fh2m 是「格索引 + 颜色」）；
金额按千枚存（fh2m 是枚数，`MinVictoryGoldInThousands = 10` 对应参考的 10 000 起步）；联盟只存「第一方几个颜色」，
写回时用 `Mp2FileInfo.FillUnions` 重建整张表（为此把它转公开）；城镇归属存在信息块里而编辑器不维护，所以城镇候选不按
归属筛，「占领城镇」的 AI 胜利位恒 false——参考对非中立城镇也这么办。

**`updateCondition` 的微小差别**：参考只在换条件类型时返回 true，「换成第一个候选」仍返回 false；端口只要有东西被改就
返回 true，调用方拿它决定重画。`CreateInfo` 里再跑一遍，兜住「宿主换完颜色没调 Update」的路径。

**没做**：绘制与事件循环（`a7l.3.5`）；参考里「禁止法术 / 禁止神器位图」是多选窗（`a7l.8` 的选择器），截图里的位图只是
一张状态图，取值不在数据半。

## ADR-304：物件弹窗接上显示层（`a7l.3.6`）

`EditorObjectPopup` 的文字终于有了去处。参考的右键是 `mouseCursorAreaPressRight()` → `Editor::showPopupWindow()` 取文字、
`displayStandardPopupWindow()` 开窗（`editor_interface.cpp:3080-3083`、`ui_map_interface.cpp:56-81`），后者自己 `while` 到松手。
端口不能阻塞，于是拆成三块：

| 件 | 职责 |
|---|---|
| `EditorObjectPopup`（已有） | 一格的字：名字、数量、归属、坐标 |
| `EditorObjectPopupView.TryCreate` | 把字交给 `StandardPopupWindow`：`QWIKINFO.ICN` 底板 + 小号白字 + 摆放 |
| `EditorScene` | 生命期：右键按下造，松手 / 拖动 / 点信息盒就收 |

**界面区要换算，不能直接传视口**：`getPopupWindowPosition()` 拿的是鼠标的绝对坐标，只用界面区的**宽高**夹边（`:39-54`）。
端口的视口是边框内的矩形（左上角 (16,16)），直接传会多减一次边框、弹窗往左上偏 16 像素；`InterfaceAreaOf()` 把它换成
「从原点起、右下角与视口相同」的那块 (0,0,464,464)。

**右键现在一心三用**：参考的平移在左键上（`interface_gamearea.cpp:1098`），端口左键要画画，平移只能让给右键。于是按下这一帧
就弹窗（与参考同刻），指针拖过 `MapScrollController` 的阈值后收窗、交给平移，松手再按老规矩吸地形。拖动开始收窗这条写在
`SetCursor` 里——弹窗留在屏幕上会指着一格指针已经离开的地方。

**生命期给了 2 秒上限**：参考按住就一直画，端口没有「按住期间」可等，也就没有可靠的松手事件（焦点一丢就没了）。所以按下即造，
`Update` 里倒计时，`ObjectPopupSeconds = 2.0` 到期自收；`ObjectPopupRemaining` 只是给日志看的。

**验证**：新增 `EditorObjectPopupViewTests` 7 条（界面区换算、文字、位置 (17,49)、夹边、越界格、装不下的底板、空参）；
`--scene editor --editor-new 36 --cursor 100,100 --editor-popup 3,4` 的真实数据截图里，弹窗落在指针右下、写着 `Grass [3; 4]`。
全量 6852 通过（+7），构建 0 警告 0 错误。

**没做**：信息盒（`I` 键）与弹窗还是两条路——参考只有弹窗，信息盒是端口为 MP2 的物件部件与数据字加的，等 `a7l.6` 把编辑器
输入换成 `LocalEvent`、右键语义定案时再合。

## ADR-305：NGEXTRA 两组宽图的杂边改在取帧处（`a7l.3.5.3.2.5`）

原版 `NGEXTRA.ICN` 的 27-33（电脑控制玩家）与 39-44（职业）这两组 62×58 的宽图，右缘多一列 1 像素宽的杂边。参考在
`loadICN()` 解完归档后**原位**改图：第 58 列第 44-54 行抄到第 59 列（`game_assets.cpp:3948-3959`），此后所有人拿到的都是改过的帧。

端口的归档缓存（`IcnSpriteFile`）是整局共用、有「只读」约定的，写脏它会连累别的画法。所以新增
`src/FHeroes2.Game/Gui/NgExtraWideIcons.cs`：`Patch(archiveFrames, frameId)` 回一份改过的新图，三个门与参考一致——表名
`NGEXTRA.ICN`、正好 82 帧（`if ( images.size() != 82 ) break;`）、帧必须 62×58（两组里夹着 62×45 的窄图，参考只是跳过）。

**两个取帧点各套一次**，因为这张表有两条路：

| 调用点 | 谁在用 |
|---|---|
| `Assets.GetArchiveFrame` | 场景信息屏（`ScenarioInfoArtwork.GetIcon` → `GameAssets.GetImage`） |
| `AdventureMapDialogs.NgextraArtwork.GetIcon` | 冒险地图的玩家面板——它直接读帧表，不经过 `Assets` |

只改前者的话玩家面板那半边照旧多一列，正是 `a7l.3.5.3.2.4` 踩过的那类「两层素材」坑。

**顺手改的既有断言**：`EditorPlayerIconsTests.TheArchivedFramesAreLeftAlone` 原来要求 0-81 逐帧与归档相等，现在这 13 帧本来
就该不一样，跳过并把说明指向 `NgExtraWideIconsTests`。

**验证**：新增 7 条 Game 测试（抄列范围与其它像素不动、归档不被写脏、两组都改、窄图跳过、帧数不对不碰、表名匹配、真实资材
逐帧比对）+ 1 条 UI 测试（场景信息屏交付的图标列已对齐）。真实资材里断言至少有一帧真被改了，否则比对是空转；归档那 82 帧
像素在取帧前后完全一致。全量 6860 通过（+8），构建 0 警告 0 错误。截图没做：场景信息屏要 `Settings` 里有地图目录才列得出剧本，
这台机器上没有配。

## ADR-306：数值控件的整帧输入与它要开的两扇窗（`a7l.3.5.3.2.7`）

`ValueSelectionDialogElement::processEvents()`（`ui_dialog.cpp:986-1019`）一件事做四样：按下的箭头画成按下样、点击与按住
重复加一步（`TimedEventValidator` 默认 500 ms 后开始、之后每 100 ms）、滚轮与上下键（只算落在数字框里，除非
`setIgnoreMouseWheelEventRoiCheck()`）、点数字框开数字键盘、右键数字框出范围提示。

端口的 `ValueSelectionElement` 原来只有「按下/松开」两步（`SetLeftButton`），调用方自己在外面判点击，于是没有重复、没有滚轮
与按键。现在加 `ProcessEvents(LocalEvent)` 逐帧照抄参考的四样，返回 `ValueSelectionFrame(Changed, Request)`：值变没变，
以及要开哪扇窗（`ValueSelectionRequest.Numpad` / `.Range`）。**窗本身不在这里开**——参考是在控件里阻塞跑
`openVirtualNumpad()` 与 `showStandardTextMessage()`，端口按既有分工把窗交给宿主：范围与当前值从 `Minimum` / `Maximum` /
`Value` 取，右键那段话就是 `RangeMessage`（`"The available values range from %{min} to %{max}."`）。

**两种进法不许混用**：`SetLeftButton` 留着给只把按钮映射成布尔的宿主（`SelectCountView`），它在一次按下里只走一步；
`ProcessEvents` 给拿得到整帧 `LocalEvent` 的调用方（`EditorMapSpecsInput` 的金币与天数、`RandomMapGeneratorView` 的种子，
后者正是参考 `dialog_random_map_generator.cpp:243` 的写法）。同一个元素同时喂两条路会把一次点击算成两步。

**宿主还没接**：`EditorMapSpecsInput` 现在会发 `EditorMapSpecsRequestKind.EditValue`（带 `ValueField` 指明是金币还是天数），
宿主跑完键盘把数交给新增的 `ResolveValue(int?)` 写回元素与状态；`null` 等于 Escape，什么都不改。但没有哪个场景跑
`EditorMapSpecsInput`，所以这条链和 `InputStringDialog` 一样停在契约上，等编辑器地图规格窗接进宿主时一起跑通。

**验证**：+10 条控件测试（点击一步、按住 500 ms 与之后的 100 ms 节奏、松开停止、上下两个箭头、上下限、滚轮只在框内、
关掉 ROI 检查、上下键、点框要键盘、右键要范围提示与文案、空参）+ 4 条地图规格测试（金币框要键盘并写回、天数框要键盘、
右键出范围文案、键盘被取消则不改值）。全量 6874 通过（+14），构建 0 警告 0 错误。截图没做：没有场景跑这两个窗口。

## ADR-307：编辑器的鼠标与滚轮改从 `LocalEvent` 读（`a7l.6.1`）

这里说的「参考」是 `editor/` 目录自己那套输入：`Editor::processEvents` 每帧从 `LocalEvent`（`Interface::Basic` 之外的一份
全局事件状态）里读指针、`MouseClickLeft/MouseClickRight` 与滚轮，编辑场景没有自己的 `Interface::AdventureMap` 去转发。

端口的 `EditorScene` 原来只有四个「显式动作」入口——`SetCursor`、`SetLeftButton`、`SetRightButton`、`ScrollByWheel`，
真实鼠标靠 `GameHost` 在 `PumpEvents` / `HandleMouseButton` 里一块块转进来，脚本靠 `ClickRadar` / `ClickPanel` 直接调。
两个问题：一帧里「按下+松开」这类组合在宿主那层就被拆没了，手感与参考对不上；宿主与场景各留一半状态（按住的键、
采样中的笔刷），谁先谁后写在宿主里，`a7l.6.3` 要搬的 `MapScrollController` 也卡在这层。

所以 `EditorScene` 加了 `ProcessEvents(LocalEvent) → bool`（`Update` 开头读 `LocalEvent.Current`），四样一次读完：

- **指针**：只在 `events.HasMouseMoved`、或者这一帧按钮状态有变化 / 有快击时才 `SetCursor`。**不能每帧无条件取**——
  `LocalEvent` 一直记着最后一次位置，每帧取会把宿主「指针离开窗口」时置的 `null` 又填回旧位置，还会让边缘滚动在鼠标
  没动过的时候一直生效（滚到地图角停不下来）。
- **左键**：与上一帧的 `_leftHeld` 比出按下与松开；两者落在同一帧（`LocalEvent` 把一对首尾相接的鼠标事件合成一帧）时
  状态没变，靠 `MouseClickLeft()` 补一次「按下+松开」——参考每帧看的就是 `MouseClickLeft`，一笔快击正好画一格。
- **右键**：同法，新增 `_rightHeld` 字段；快击也要走完「按下弹物件框、松开取笔刷」两步。
- **滚轮**：`LocalEvent` 原来只把滚轮偏移记在私有字段里没有出口，新增 `MouseWheelMovement` 属性；`ScrollByWheel(0, -y)`
  沿用宿主原来的取负（向上滚 = 视图往地图上方走），高分辨率滚轮的半格仍被折成 0。

**留下的两处不在场景里**，都是 `LocalEvent` 表达不了的窗口级事件：

1. **指针离开窗口**：`PumpEvents` 的 `WindowMouseLeave` 仍然直接 `_editor.SetCursor(null)`（模态窗、城镇屏、编辑器菜单
   同一条分支）。事件里没有「离开」这个概念，硬塞进去要把窗口生命周期混进输入状态。
2. **键盘**：仍在 `GameHost.HandleEditorKey`。Esc 要开编辑器菜单、Ctrl+S 要存盘、信息盒的模态优先级都在宿主那一层；
   `LocalEvent` 里也没有 `key.Repeat`（按住连发）与修饰键的可靠信息。

宿主那边删掉的只有转发：`HandleMouseButton` 里编辑器那条变成 `return false`（只为别让按钮落到没关掉的编辑器菜单上），
指针与滚轮分支留空并注明原因。三条日志（`Painted with ...`、`Picked the ... brush`、细节仪器取样）跟着搬进场景，
它们原来是宿主在调完动作之后打的。`--left-click` 也补了一支：`ReplayScriptedInput` 原来没有编辑器，脚本点击会掉到菜单，
现在照冒险地图的写法把「移动 + 按下 + 抬起」推进 `LocalEvent`，于是编辑场景里的脚本点击真的能画一格。

**验证**：`EditorScene` 在 `FHeroes2.App`，四个测试工程都引用不到它，所以没加单测，改用脚本 A/B 对拍（同一份源码
`git stash` 出改动前后两个构建，同参数跑）：`--editor-paint` + `--editor-panel-click`（四个面板格子）+ `--editor-radar-click`
拖拽 + `--editor-data` + `--editor-popup` + `--editor-info` 一次跑完，截图与 `--editor-save` 出来的 `.mp2` **逐字节相同**；
再拆成单动作与两两组合各跑一遍，也都相同（这份脚本本身是确定的：同参数连跑两次哈希一致）。新能力另测：`--editor-new 36
--editor-paint 20,20,2,2,Snow --cursor 300,300 --left-click` 打出 `Painted with 'Snow'; 2 undoable strokes.`，说明脚本点击
确实穿过了 `ProcessEvents`。全量 6874 通过，构建 0 警告 0 错误。

## ADR-308：编辑器雷达独立成一份，`Minimap` 拆掉（`a7l.6.2`）

参考里只有一份 `Radar`（`gui/interface_radar.cpp`，`fheroes2::radarWidthPx` = 144）：冒险地图与编辑器用的是同一个类，只是
喂给它的 `world` 不同。端口里三份雷达各自长出来了——`UI/Interface/Radar`（冒险地图）、`Game/Kingdom/ViewWorldRadar`
（世界视图）、`Game/AdventureMap/Minimap`（编辑器），后两份把调色板、`zoom` 放大与虚线框又抄了一遍，`Minimap` 还躺在
模型层里却只有一个视图用户（`EditorScene`）。

拆法：把「每格一个调色板下标 → 最近邻放大到 144 见方 → 虚线框」这半份抽出来，另一半归各自的着色规则。

- **`MapFrame.PanelSize`**（原来是 `Minimap.PanelSize`）：边框的雷达湾、两份面板的边长、虚线框的坐标系都是它。
  144 这个词也顺手替掉了 `MapFrame` 里的字面量注释。
- **`Game/AdventureMap/RadarPanel`**：三个雷达共用的静态一半——18 个调色板下标、`GetGroundColor`、`GetKingdomColor`、
  `ScaleColors`（含 `min(..., 宽-1)` 的钳位）、`CursorArea`（先裁到地图再缩放）、`DrawCursorFrame`（每六点留一点的虚线），
  以及 `HillShadeOffset`——`ViewWorldRadar` 里原来写死的 `+ 3`。
- **`UI/Interface/EditorRadar`**：编辑器那份——`Build(Mp2Map)`（地面按 `MapGrounds.FromTerrainImageIndex` 上色，山与树加亮
  三档，道路用道路色，英雄按所属色盖在最后）、`Width/Height/Zoom/Image/ColorAt/TileAt/CursorArea/Draw/DrawCursor`。放在
  UI 层是因为参考里雷达本来就在 `gui/`；`EditorScene`（App）是它唯一用户。读 `Mp2Map` 而不是 `World` 是这份雷达与另外两份
  唯一的实质差别：编辑中的地图没有冒险地图那套模型。
- 删掉 `Game/AdventureMap/Minimap.cs`；用例按归属拆开：地面表进 `MapGroundsTests`，调色板与虚线框进 `RadarPanelTests`
  （都在 Game.Tests），MP2 上色、英雄盖章、面板几何与 `TileAt` 进 `EditorRadarTests`（UI.Tests，因为类型在 UI）。

**验证**：把旧 `Minimap.cs` 临时放回去、`EditorScene` 改回旧类型，重建出「重构前」的二进制，与重构后同参数各跑一遍
（`--editor-new 72` + `--editor-paint` + 四个 `--editor-panel-click` + `--editor-radar-click` 拖拽 + `--editor-data` +
`--editor-popup` + `--editor-save`）：截图**逐字节相同**，`.mp2` 也**逐字节相同**。全量 6881 通过（UI +16、Game −9，净 +7），
0 警告 0 错误。

**踩到的坑（写下来免得再踩）**：`--editor-popup` 的窗口只活 `ObjectPopupSeconds`（2 s）并按每帧 `dt` 递减，`--smoke-frames 10`
截出来的图取决于这一跑有多快——同一份源码不同构建能一个拍到窗口、一个拍不到。凡是要截图对拍的脚本都别带 `--editor-popup`，
或者在 `--editor-popup` 之后立刻截。这次差点把它当成重构引入的回归，白查了一轮。

## ADR-309：拖拽、边缘滚动、滚轮收进 `EditorScene`，删掉 `MapScrollController`（`a7l.6.3`）

ADR-306 把指针的「两级坐标」留在了 `Game/AdventureMap/MapScrollController` 里：视口像素、拖拽阈值、边缘带、卷屏节拍全在
一个只为编辑器服务的类里。`a7l.6.1` 之后输入已经整帧从 `LocalEvent` 读，那一层就只剩「上一格指针」的转手工作，于是把它并回
场景：谁读事件，谁就管指针。

搬过去的四个动作，逐条对着参考：

- **`DragTo`（平移）**：按下的那点记作 `_panOrigin`，位移超过 `DragThreshold` 才算拖动，此后每步按 `anchor - current` 挪视图
  （参考的 `SetCenterInPixels( center + last - current )`，地形跟着指针走），第一次位移从按下点算起，所以越过阈值的那段不丢。
  `DragThreshold = 10` 是 `interface_gamearea.cpp:68` 的 `minimalRequiredDraggingMovement`，比较是严格的：正好十像素还是点击。
- **`UpdateEdgeScroll`（边缘滚动）**：指针落在视口四边各 `BorderWidth`（16 px，`fheroes2::borderWidthPx`）以内，每
  `EdgeScrollInterval` 挪一次 `EdgeScrollStep`；拖动中不滚（参考的 `assert( !isDragScroll() )`），贴边或 `IsScrollingPossible`
  为假不累计，一帧最多补 4 步、补不上的余量丢掉。节拍沿用移植前的做法：按帧 `dt` 自己累计，不碰全局限时表。
- **`ScrollByWheel`（滚轮）**：一格一块 tile，交给 `AdventureMapView.ScrollBy` 钳位。
- **`SetPanButton`**：拖拽用右键，左键让给笔刷（E1 的定制，见 ADR-306）。

**共享常量往哪儿放**。20 ms 这个数在参考里是 `game_delays.cpp:96` 的 `setDelay( DelayType::SCROLL_DELAY, 20 )`，所以它进了
`GameDelays.ScrollDelayMilliseconds`，`InitAnimateDelays` 和 `EditorScene.EdgeScrollInterval` 都从它算，App 层不再出现字面量
20；16 px 用场景里已有的 `BorderWidth`（`MapFrame.BorderWidth`）。`10` 和 `2 << 2` 是编辑器这一处独占的，留在 `EditorScene`
的公开常量上，附参考出处。滚轮速度档端口还没有设置项，`2 << 2` 就是 `NORMAL` 档，注释写清。

**顺带补的验证口子**：`--editor-drag <x1,y1,x2,y2>`（视口像素）与 `EditorScene.DragMap`。拖拽要「按住跨帧」，脚本合成的
点击做不到，而这个动作恰恰是这次唯一换了家的手感，不给它一个脚本口子就只剩肉眼。日志直接报落点：
`--editor-radar-click 40,30,90,80` 把视图推到 (1216; 1056)，再 `--editor-drag 120,140,60,100` 得 (1276; 1096)，正好 +60/+40；
单独 `--editor-drag 100,100,60,100` 得 (968; 928)，从 (928; 928) 起正好 +40，方向与幅度都对；`100,100,108,100`（八像素）
一步不挪，日志是「where it already was」，说明阈值和「松手不算拖动」都在。

**边缘滚动的验证没法靠截图**：`--cursor` 设的合成指针会被 `LocalEvent` 里的真实指针顶掉（`ProcessEvents` 的
`HasMouseMoved` 分支），滚几十帧后视图冻在原地，冻在哪儿取决于跑的时候鼠标在屏幕哪儿——不是这次的回归，是脚本本身的
不确定。改用临时日志量了节拍：`dir=5`（左|上）、每步 8 px、约 20 ms 一步、`IsScrollingPossible` 为假就停，与移植前一致。

**用例**：`MapScrollControllerTests` 的 13 条随类删除——`EditorScene` 在 `FHeroes2.App`，而四个测试工程都不引用它，没有地方
可搬。全量 6868 通过（Game −13），0 警告 0 错误；另开 `a7l.6.4` 记一笔「App 层没有测试工程、编辑器场景的手感只能靠脚本」。

## ADR-310：`Game/AdventureMap/ButtonsPanel` 只留数据，面板类删掉（`a7l.6.6`）

epic 里写的是「`Game/AdventureMap/ButtonsPanel.cs` 零引用，直接删」，实测只对了一半：这个文件里躺着四个类型，其中只有
**面板类**是死的。

- **死的那一半**：`ButtonsPanel`（布局、`AreaOf`、`Draw`、帧号换算）。它的活全在 `UI/Interface/ButtonsPanel`——ADR-058 时
  面板还只有布局与绘制，后来 UI 层长出了带 `BorderWindow`、点击、长按、拖动的完整版，模型层这份就没人实例化了。
- **活的那一半**：`AdventureButton`、`HeroMovementIcon`、`AdventureButtonStatus`。UI 层 `using FHeroes2.Game.AdventureMap`
  正是为了这三个：按钮的编号既算格子位置也算帧号（`2 * index`），`_setButtonStatus` 的结果就是那个 record struct。
  这三个的语义属于冒险地图而不是某块砖，所以不跟着面板搬去 UI。

于是把文件拆成两半：数据留在原目录的 `AdventureButtons.cs`（`git mv` 后重写，注释里点明「面板在 UI 层」），面板类删掉。
同一个文件里的 `AssetName = "ADVBTNS.ICN"` 跟着它唯一的读者走，落到 `ButtonSpriteSet.AssetName`（`EvilAssetName` 无人引用，
随类删除）；UI 层那两个 `GoodInterfaceAssetName` 一个字没动。

**用例**：`Game.Tests/ButtonsPanelTests` 的 11 条测的都是被删的那个类，随类删除；其中真正守活的类型的只有「没有英雄时只剩
五个按钮」与「运动按钮三种图标」两条——后者测的 `ButtonsPanel.ReleasedFrame(HeroMovementIcon)` 也随类没了（UI 层自己算帧号），
所以只把那一条搬进新的 `AdventureButtonsTests`，另补一条守八个按钮的编号顺序（UI 的格子与帧号都吃这个数）。
`UI.Tests/ButtonsPanelTests` 的 20 条不动。全量 6859 通过（Game −11 +2），0 警告 0 错误。

## ADR-311：编辑器副技选择窗按 `SecondarySkillContainerUI` 摆格，开关按钮收进 `ButtonFactory`（`a7l.5.1`）

`editor_secondary_skill_selection.cpp` 的窗口（`EditorSecondarySkillSelectionWindow`）与它里面的
`SecondarySkillContainerUI` 拆成两块：格子在 `UI/Widgets/EditorSecondarySkillSelectionGrid`，窗口只管装配、按钮与回填，
事件与弹窗请求在 `EditorSecondarySkillSelectionInput`——与法术选择窗（ADR 里那一刀）同一套分法。

参考里几处看着像笔误、其实是行为的地方照抄：

- **垂直居中用的是整数除法的行数**：`skillRowOffsetY * (size / columns)`（`editor_secondary_skill_selection.cpp:75`）。
  十二个技能排四列只占三行，正好整除；末行不满时（如三个技能排两列）这一项仍只算整行，块会略微偏上。
- **末行的判据是 `row == size / columns`，不是「最后一行」**（`:83`）。整除时这个下标不存在，也就不偏移。
- **列的数目是最小的 `i` 使 `i * i >= size`，上限四**（`:67-72`），不是固定四列。

**灰度**：参考对已经画进目标的那块区域调 `ApplyPalette( ..., GRAY )`（`:120-122`）。端口不二次扫像素，而是先把图标
克隆一份过灰度、帧也预先做一份 `InactiveFrame`，再按「图标 → 帧」的顺序贴；帧的中间是透的，与参考的观感一致。

**两个开关按钮**：参考把 `BUTTON_TOGGLE_ALL_ON/OFF` 都摆在 `BOTTOM_CENTER`、`{0,7}`（`:260-265`），两个框完全重叠，
靠「后画的盖住先画的」达到只看见一个的效果。端口两个 `ButtonSprite` 也放在同一点，`Paint` 只画启用的那个。

这两个按钮的图案不在归档里——`game_assets.cpp` 里它们是 86x25 空板子加 h2d 图案合成的，法术窗当时把这段合成抄进了自己的
`CreateToggleButton`。这次收进 `ButtonFactory.MakeToggleAllButton`（尺寸常量 `ToggleAllButtonSize`），法术窗改调它，
副技窗直接用，两处不再各写一份。

**回填**：确定的返回值是全选的技能；全选折成空表（`:387-390`），因为游戏把空表读作「按默认」。OKAY 在所有技能都关掉时
置灰（`:353-373`），此时点它无事发生——端口在 `EditorSecondarySkillSelectionInput` 里先问 `IsEnabled` 再认点击。

**弹窗**：四个按钮的说明与技能说明（`SecondarySkillDialogElement`）按 `EditorSecondarySkillSelectionRequest` 交回宿主，
与 `EditorMapSpecsInput`、`EditorSpellSelectionInput` 一致；`DEFAULT_OKAY/CANCEL` 热键（`:288`、`:292`）与法术窗一样留给宿主。

**用例**：`UI.Tests/EditorSecondarySkillSelectionTests` 15 条——列数、末行居中、超量与空表的拒绝、初始全选、只保留元数据里
的那几个、单格切换、全关后 OKAY 置灰、全开复位、确定/取消、关掉全部时点确定无事、右键技能出说明、右键全关出提示、
关一格后画面与全选不同。全量 6874 通过（UI +15），0 警告 0 错误。

## ADR-312：编辑器保存地图窗是 `ListBox<Mp2FileInfo>`，列表与写盘各管一段（`a7l.5.2`）

`editor_save_map_window.cpp` 的 `mapSaveSelectFile()` 拆成 `Dialogs/EditorSaveMapWindow`（列表 + 输入 + 四个按钮）与
`Dialogs/EditorSaveMapArtwork`，逐帧 `Process()` / `Draw()`，与编辑器其它窗一致。

**尺寸照抄**：`listWidth = maxFileNameWidth + 9`、窗口宽 `listWidth + 75`、`listAreaOffsetY = 6`、
`listAreaHeightDeduction = 8`、`listHeightDeduction = 129`；可见行 `clamp(地图数, 11, 22)`，行高是小字高 + 2，
再受屏幕高度限制（480 高的屏只够 14 行，参考同样如此）。列表区在 `activeArea.x + 24, y + 54`，
地图名一行在 `y + 28`、纵向 +4 左对齐。

**列表**：`FileInfoListBox` 的每行是去掉扩展名的文件名加两张 `REQUESTS` 小图标——玩家人数从 19 起、地图大小从 25 起
（宽度 / 36，`Maps::SMALL`）。图标缺席（`REQUESTS` 帧不够）就只画名字，不抛。

**文件名**：固定后缀、输入上限 255、点列表某行就把文件名换成它、点输入框撤掉列表选中并把光标落到点上、
按 `InsertKeySym` 插字符；`Delete` 在有选中行时是「删这个文件」的请求，没有选中行时是删字符。
名字空了 OKAY 置灰（`:330-340`）。参考的虚拟键盘按钮（`BUTTON_VIRTUAL_KEYBOARD_*`）在归档里没有自己的 `.ICN`，
`game_assets.cpp:5465-5498` 是空板子加 `keyboard_button_*` h2d 图案拼的——端口直接调
`ButtonFactory.MakeVirtualKeyboardButton`（那个方法早就有），h2d 缺席时按钮整个不画。

**偏差**：列表只列 `.mp2`（参考列它自家的 `.fh2m`，端口没有那种格式，编辑器写的就是 MP2）；参考的
「触屏必开虚拟键盘时点输入框自动开键盘」没有对应判据（`System::isVirtualKeyboardSupported` 在端口里不存在），故不做。
列表内容由调用方给（参考是 `getEditorMapFileInfos()` 扫目录、按文件名排序），窗口不扫盘。

**写盘**：窗口只回 `FileName`；`EditorSaveMapWindow.SanitizeFileName` 去掉扩展名、把文件系统不认的字符换成 `_`，
`ComposeFilePath` 拼 `目录/名字.mp2`，`SaveMap` 建目录并调 `Mp2MapWriter.Save(..., writeHeader: true)` 覆盖同名文件。
参考把这段放在 `editor_interface.cpp:3238-3256`（`saveMapToFile`），端口等编辑器接口那块（`a7l.7`）再接。
净化这一步是端口加的：参考直接把输入拼进路径，Windows 下会把 `:`、`/` 写进文件名。

**弹窗**：CANCEL/OKAY/地图名/虚拟键盘四个按钮的说明与单行地图说明都按 `Popup` 交宿主。单行说明照 `:99-111` 写成
「Map Type / Size / Description / Location」，其中 Map Type 写「Price of Loyalty」（列表只收 mp2，参考写它自己的
「Resurrection」）；「Language」一行略去——`Mp2FileInfo.mainLanguage` 只是原始字节，端口还没有到 `SupportedLanguage`
的映射。参考的分段字体（黄标白值）在端口一律是纯文本。

**用例**：`UI.Tests/EditorSaveMapWindowTests` 21 条——默认名回填、255 截断、空名置灰 OKAY 且点它不生效、
11/22 行两档高度、点行换名、同一行点两次即确定、CANCEL/OKAY、点地图名请求改名、无选中行时 Delete 删字符、
有选中行时 Delete 请求删文件、删行后选中邻居、按名字选中（不分大小写）、右键单行/取消/虚拟键盘出说明、
虚拟键盘点击请求、净化文件名、拼路径、空名字拒绝写盘、画一帧有变化。全量 6895 通过（UI +21），0 警告 0 错误。

## ADR-313：编辑器传闻窗是一个 `ListBox<string>` 加一排 `CELLWIN` 小按钮（`a7l.4.1`）

`editor_rumor_window.cpp` 的 `openRumorWindow()` 拆成 `Dialogs/EditorRumorWindow`（600×400 标准窗、列表、三个小按钮、
OKAY/CANCEL）与 `Dialogs/EditorRumorArtwork`，逐帧 `Process()` / `Draw()`，与编辑器其它窗一致。

**尺寸照抄**：活动区 `550+50` × `300+100`；标题离顶 9 像素居中，列表 `active.x + 9, y + 9 + 行高 + 9`，`550×300`；
列表的可见区先砍掉底部 8 像素（`listAreaHeightDeduction`），`SetAreaMaxItems(10)`；滚动条在列表右侧 5 像素，
上下按钮各 19 高、槽 `x+8, y+19, 10, h-38`；三个小按钮在 `列表底 + 5`，中间那个落在
`( 550 - 3 * 按钮宽 ) / 2 + 按钮宽`。

**列表**：`ListBox<string>`，行文本左/上各缩进 5 像素、`fitToOneRow(550 - 10)`，选中行黄字、其余白字。
列表直接持有 `_rumors` 实例（参考传的是引用），所以增删就地生效、选中不丢。

**增改删**：增/改都是宿主弹输入框（`InputStringDialog.Create(InputTitle, InputBody, LongestRumor, isMultiLine: true)`），
窗口只发 `AddRequested` / `EditRequested` 并把结果吃进 `TryAddRumor` / `TryReplaceSelected`；重复时返回 `false` 并把
「This rumor already exists in the list.」放进 `Popup`，由宿主显示。改写自己那行不算重复（参考
`GetCurrent() != temp && count > 0`）。删除走 `RemoveSelected()`，选中自动落到邻居。

**偏差**：参考的 `Text( rumor, font, language )` 多带一个语言参数，端口没有按语言的断行/整形，故 `Create` 不收 language；
参考用 `ImageRestorer` 复原列表背景，端口在 `RedrawBackground` 里重刷阴影，效果一致；热键（`DEFAULT_OKAY`/`DEFAULT_CANCEL`）
与输入框、提示框一样交宿主。

**用例**：`UI.Tests/EditorRumorWindowTests` 24 条（19 个事实 + 5 个右键分例）——空传闻被丢掉、列表位置与尺寸、
三个小按钮的摆法、点增加发请求、追加、重复拒绝、双击行请求改写、改写需选中、改写覆盖、改写重复拒绝、改写自己不变、
无选中时改写不做事、删选中行并选中邻居、无选中时删除不做事、删空列表、CANCEL/OKAY、五个按钮的右键说明、
`SetRumors` 换内容并清选中、画一帧有像素变化。全量 6919 通过（UI +24），0 警告 0 错误。
（后续：ADR-314 把素材类改名为两个列表窗共用的 `EditorListArtwork`，结果枚举改名为 `EditorListOutcome`。）

## ADR-314：每日事件列表窗是第二个 `ListBox` 窗口，素材与结果枚举抽成共用件（`a7l.4.2`）

`editor_daily_events_window.cpp` 的 `openDailyEventsWindow()` 拆成 `Dialogs/EditorDailyEventsWindow`（550×415 标准窗、
500×315 列表、三个小按钮、OKAY/CANCEL）与地图格式模型 `Game/Editor/MapDailyEvent`。

**共用件**：传闻窗与事件窗要的素材完全一样（`SCROLL` 0..4 + `ADVBORD` 槽 + `SYSTEM` 1..4 + `CELLWIN` 13..18），
`EditorRumorArtwork` 改名 `EditorListArtwork`、`EditorRumorOutcome` 改名 `EditorListOutcome` 并单独成文件，
两个窗口共用；ADR-313 里那 24 条用例跟着改名后照跑。

**尺寸照抄**：活动区 `500+50` × `315+100`；标题离顶 9 像素居中，列表 `active.x+9, y+9+行高+9`，`500×315`，
可见区砍掉底部 8 像素，`SetAreaMaxItems(10)`；滚动条与三个小按钮的摆法和传闻窗一模一样，只是宽度基准换成 500。

**模型**：老端口没有地图格式（`Maps::Map_Format`）这一层，于是新增 `MapDailyEvent`——`Message`、
`HumanPlayerColors`、`ComputerPlayerColors`、`FirstOccurrenceDay`(默认 1)、`RepeatPeriodInDays`(0 表示不重复)、
`Resources`(<c>Funds</c>)，默认值照 `map_format_info.h:297-311`。窗口收的是副本（`Clone()`），改窗口不动调用方的对象。

**行文本**：参考用一个 `MultiFontText` 拼两段——「Day: N」（有消息时补一个开引号）按原样画，消息 + 闭引号按
`500 - 10 - 日期宽` 截一行。端口没有多段文本，于是先 `Measure` 日期段，再紧接着画第二段，效果相同。

**排序**：参考在增、改、删之后都对整个 vector 按 `firstOccurrenceDay` 调一次 `std::sort`（不稳定），端口用
`List.Sort` 就地排同一份列表，所以列表始终按天递增；选中项不按对象重锚，和参考一样只保留行号。

**偏差**：参考的 `Text( ..., language )` 与 `MultiFontText` 带语言整形，端口没有；`ImageRestorer` 换成
`RedrawBackground` 重刷阴影；规格窗（`editor_daily_event_spec_window.cpp`，`a7l.4.3`）与热键都交宿主，
窗口只发 `AddRequested` / `EditRequested`，再吃回 `AddEvent` / `ReplaceSelected`。

**用例**：`UI.Tests/EditorDailyEventsWindowTests` 22 条（17 个事实 + 5 个右键分例）——建窗即按天排序、收副本、
列表位置与尺寸、三个小按钮的摆法、点增加发请求、插入后仍有序、双击行请求改写、改写需选中、改写后重排、
无选中时改写不做事、删选中行并选中邻居、无选中时删除不做事、删空列表、CANCEL/OKAY、五个按钮的右键说明、
`SetEvents` 换内容并清选中、画一帧有像素变化。全量 6941 通过（UI +22），0 警告 0 错误。

## ADR-315：每日事件规格窗是「两列面板 + 两个数字控件 + 两组颜色框」（`a7l.4.3`）

`editor_daily_event_spec_window.cpp` 的 `editDailyEvent()` 拆成 `Dialogs/EditorDailyEventSpecWindow`（608×430 标准窗）、
`Dialogs/EditorDailyEventSpecArtwork`（`TOWNWIND` 0..8、`BUTTON_SMALL_OKAY_*`/`BUTTON_SMALL_CANCEL_*` 0..1），
以及模型上的 `MapDailyEvent.Clamp()`。

**进来先规整**：`Clamp()` 干参考 `:70-77` 那四件事——两个颜色集与地图允许集求交、首次发生日收进
`1..lastDayForEvents`(33600)、重复周期收到一年(336)。天数常量放在模型上（`DaysInYear`/`LastDayForEvents`），
窗口与用例都引它。

**布局照抄**：`WindowWidth = 640 - 2 * borderWidthPx`(608)、`WindowHeight = 210 + 220`(430)；左列消息区
`active.x+9, y+9+行高+9`、`300×210`，奖励面板在消息区底边 + 18、`300×99`；右列从
`active.x + 9 + 300 + 9` 起、宽 281，标签先按「不断缩窄到两行」求出宽度再整体居中，颜色框居中排、间距 32，
首次日控件居中、底部 +10 之后是 35 像素的日期说明，再是重复周期控件。

**控件都用现成的**：`ValueSelectionElement`（`TOWNWIND` 4..8 帧，1..33600 与 0..336）、
`EditorUiHelper.CreateColorCheckboxes`、`EditorUiHelper.RenderResources`、`GameDate.Describe`、
`SelectCountDialog`。复选框的方框画在一层常驻缓冲里每帧贴回、勾按状态单独贴；窗口没有 `ImageRestorer`，
`Draw()` 整窗重画。

**输入框/键盘/数量框交宿主**：参考在窗口里直接调 `Dialog::inputString`、`openVirtualNumpad`、
`Dialog::SelectCount`，端口改成发 `MessageRequested`、`NumpadRequest`、`ResourceCountRequest`，宿主弹完再喂
`SetMessage`、`SetValueField`、`SetResourceCount`。

**偏差**：①参考「勾上就 `|=`、取消就 `^=`」的行为照抄（等价于集合增删）；②标签的两行逼宽循环遇到单个词会死循环，
端口加了 `width > 1` 的护栏；③资源面板右键参考弹画着图标的 `showResourceMessage()`，端口还没有那个窗口，
改成同一个提示里按文本列出金额；④`Text( ..., language )` 的语言整形端口没有。

**用例**：`UI.Tests/EditorDailyEventSpecWindowTests` 25 条（23 事实 + 2 右键分例）与
`Game.Tests/MapDailyEventTests` 6 条——建窗即规整、收副本、两列布局、颜色框摆法与初始勾选、点框改颜色、
两个数字控件的初值/上下限/写回/越界收口、点编辑框要键盘、点消息区要输入框、消息截断到 999、点资源图标要数量框、
写回金额、三个面板的右键说明、CANCEL/OKAY、画一帧有像素变化；模型的默认值、天数常量、`Clamp` 三项与 `Clone`。
全量 6972 通过（UI +25、Game +6），0 警告 0 错误。

## ADR-316：事件详情窗是「两列四段 + 三个元素 + 条件区」（`a7l.4.4`）

`editor_event_details_window.cpp` 的 `eventDetailsDialog()` 拆成 `Dialogs/EditorEventDetailsWindow`（639×480 标准窗）
与 `Dialogs/EditorEventDetailsArtwork`（`RESOURCE` 7、`EXPMRL` 4、`SECSKILL` 15/0、`ARTIFACT` 0、`CELLWIN` 1/2/17/18、
`BUTTON_SMALL_OKAY_*`/`BUTTON_SMALL_CANCEL_*`），模型侧只给 `AdventureMapEventMetadata` 补了一个 `Clone()`。

**布局照抄**：`sectionWidth = (640 - 3 * 9) / 2`(306)、窗宽 `306 * 2 + 27`(639)、高 480；左列消息区
`active.x+9, y+9+标题行高+9+正文行高`、`306×207`，条件区在其下 +18：「Conditions:」、勾选框、人类颜色标签与框、
电脑颜色标签与框（行距 35）；右列从 `active.x + 306 + 18` 起：先「Effects:」与 `306×99` 奖励面板，
再是神器元素与删除按钮、技能元素（`(artifact.x + artifact.width + experience.x - width) / 2` 居中）与删除按钮、
经验图标与底下的数值（删除按钮同高）。

**三个元素直接用现成的**：`ArtifactDialogElement`、`ExperienceDialogElement`、`SecondarySkillDialogElement`；
换神器/技能时重建元素（图标与提示随事件走）。右键提示走元素的 `ProcessEvents(events, offset).Popup`，
没有元素覆盖的地方（空槽、删按钮、消息区、条件区、奖励面板、两个主按钮）给固定文案。

**点击都交宿主**：参考在窗口里直接调 `Dialog::inputString`/`selectArtifact`/`selectSecondarySkill`/`SelectCount`，
其中神器与技能的单选窗端口还没有，所以消息、神器、技能、经验、资源全部只发
`MessageRequested`/`ArtifactRequested`/`SecondarySkillRequested`/`ExperienceRequest`/`ResourceCountRequest`，
宿主弹完再喂 `SetMessage`/`SetArtifact`/`SetSecondarySkill`/`SetExperience`/`SetResourceCount`。

**偏差**：①颜色位集「勾上 `|=`、取消 `&= ~`」（参考的 `^=` 只在勾选状态与位集一致时才等价，端口取更稳的写法）；
②「首次访问后取消」的勾选框自己画（`CELLWIN` 1/2 从素材里取），不调 `EditorUiHelper.DrawCheckboxWithText`：
那个每帧去归档现取两帧、用的是全局字体表，和窗口 `IGlyphSource` 量出来的区域对不上；
③参考的 `CELLWIN_EVIL` 是启动期从 `CELLWIN` 生成的，生成器未移植，所以框、勾、删除按钮一律用普通版，
`isEvilInterface` 只影响 OKAY/CANCEL；④OKAY 的可用性判断在参考 `:424` 就被注释掉了，端口照样一直可用；
⑤奖励面板右键参考弹画着图标的 `showResourceMessage()`，端口改成同一提示里按文本列出金额。

**用例**：`UI.Tests/EditorEventDetailsWindowTests` 35 条（31 事实 + 2 理论各 2 分例）——建窗颜色求交与收副本、
两列布局、颜色框摆法与初始勾选、勾选框/颜色框的点击写回、消息截断到 999、七种点击要宿主弹窗（消息/资源/神器/
技能/经验图标与数值）、写回与清空、经验与资源的越界收口、十条右键说明（空神器/有神器/空技能/有技能/勾选框/
消息区/空面板/有资源面板/两个主按钮）、CANCEL/OKAY、画一帧有像素变化。
全量 7007 通过（UI +35），0 警告 0 错误。

## ADR-317：狮身人面像窗是「谜题 + 答案列表 + 奖励」，输入交宿主（`a7l.4.5`）

`editor_sphinx_window.cpp` 的 `openSphinxWindow()` 拆成 `Dialogs/EditorSphinxWindow`（继承 `ListBox<string>`，550×400）
与 `Dialogs/EditorSphinxArtwork`（借 `EditorListArtwork` 的滚动条/OKAY/CANCEL/三个小按钮，自己只加 `RESOURCE` 7 的
神器槽与 `ARTIFACT` 图标）。

**布局照抄**：`riddleArea` 250×180、`answerArea` 250×155、窗宽 `riddle.width + answer.width + 50`(550)、高 400；
标题「Sphinx:」离顶 9 居中，两个标签与各自区域同高并列；「Reward:」在谜题区下方 +9，往下一行是神器槽（250 居中）与
250×99 的资源面板；列表底下 5 像素是增/改/删三个 `CELLWIN` 小按钮（中间那个 `(width - 3*buttonWidth)/2 + buttonWidth`），
删除神器按钮在槽下 5 像素居中。

**答案列表**：`SetAreaItems(answerRoi - 8)`、`SetAreaMaxItems(10)`、滚动条槽在窗口右边缘；行文本 5 像素内缩、选中行用黄字、
`FitToOneRow` 折到 `answerArea.width - 10`；双击的语义沿用 `ListBox`——第二下落在已选中的行上（`id == CurrentId`）。

**点击都交宿主**：参考在窗口里调 `Dialog::inputString`/`selectArtifact`/`SelectCount`，端口只发
`RiddleRequested`、`AnswerAddRequested`、`AnswerEditRequested`、`ArtifactRequested`、`ResourceCountRequest`，
宿主弹完再喂 `SetRiddle`/`AddAnswer`/`ReplaceAnswer`/`SetArtifact`/`SetResourceCount`。答案的两条规矩放在 Setter 里：
截到 64 字；与列表其它项重复就返回 `false` 并把「Answer / This answer exists in the list.」放进 `Popup`（改的那行与自身
相同时不算重复，同参考 `:355`）。

**偏差**：①滚动条槽照抄参考 `:206` 的 `answerRoi.width` 会从窗口右缘往外铺 250 宽，端口收在窗口内、只留 14 像素；
②参考为法术卷轴神器再弹选法术窗，端口把这个选择也交给宿主；③资源面板右键参考弹画图标的 `showResourceMessage()`，
端口在提示里按文本列出金额；④`CELLWIN_EVIL` 生成器未移植，小按钮用普通版。

**用例**：`UI.Tests/EditorSphinxWindowTests` 35 条（28 事实 + 2 理论共 7 分例）——建窗收副本、两列布局与按钮摆法、
谜题截断 999、答案新增/截断/查重、换行（未选中时拒绝、与其它行重复时拒绝、与自己相同允许）、删除选中行、
神器写回与清空、资源写回与上限收口、七种点击要宿主弹窗（谜题/新增/修改/双击/删除/神器/资源）、九条右键说明
（五个按钮/谜题/空神器/有神器/空资源/有资源）、CANCEL/OKAY、画一帧有像素变化。
全量 7042 通过（UI +35），0 警告 0 错误。

## ADR-318：地图载入不再读进程级「当前地图」，随机神器按地图自己的版本抽（`a7l.6.5`）

**症状**：`dotnet test FHeroes2Sharp.slnx` 全量连跑偶尔有 1 条失败，单工程单跑不复现。抓到两条：
`WorldLoadMapTests.LoadMapRollsARandomArtifactIntoARealOne`（抽到 Unknown）与
`InterfaceSettingsDialogTests.ClickingTheScrollSpeedCyclesItInPlace`（期望 Fast 实得 Normal）。

**根因一（版本错位）**：`Tile.SetInitialObjectInfo()` 与 `UpdateRandomArtifact()` 照参考读了
`Hero.GetCurrentMapVersion()`，那是 `Settings.Get().CurrentMapFileInfo.Version` —— 全进程共享。并行测试里
`SettingsTests` 会 `Settings.Reset()`、`GameSaveTests` 会 `SetCurrentMapFileInfo(PoL)`，于是同一个用例前半段按
一个版本抽、后半段按另一个版本反推。

**修复**：`World.LoadMap()` 本来就把 `MapInfo` 设成 `map.Info`，两处改读 `world.MapInfo?.Version ??
MapVersion.SuccessionWars`。真实流程里两者恒等（参考的 `Game::LoadMaps` 会在载入前设当前地图），所以语义不变、
并行时不被别人的地图带偏。

**根因二（扩展神器）**：`ArtifactMapSprites.FromMapIndex(index, version, priceOfLoyaltySupported)` 的第三参在
`Tile.InitialObjectInfo.cs` 里硬编码成 `false`（参考传 `Settings::isPriceOfLoyaltySupported()`）。随机神器在 PoL 地图上
抽到扩展神器时帧号落进 172..205，反推时被当成「不是神器」→ `Unknown` → 元数据被清空。端口没有设置层，改成按地图
版本推断：`version != MapVersion.SuccessionWars`（PoL / Resurrection 地图只可能来自扩展）。

**根因三（UI 全局设置）**：`InterfaceSettingsDialogTests` 与 `EditorOptionsDialogTests` 都写全局
`Settings.ScrollSpeed` 再读回断言，两类并行互踩。后者已在 `GlobalArchiveCollection`，前者并入同一集合串行。

**回归用例**：`WorldLoadMapTests.LoadMapRollsTheRandomArtifactFromTheMapsOwnVersion` —— 把空白地图的版本改成
Price of Loyalty，放一个随机神器对象，断言抽到的不为 `Unknown`、等级落在该版本的常规档、帧号按 PoL 布局反推回同一件。

**验证**：连续 8 轮 `dotnet test FHeroes2Sharp.slnx`（含构建、四工程并行）无失败；修复前 10 轮里 2 轮各挂 1 条。
全量 7043 通过（Game +1），0 警告 0 错误。

## ADR-319：编辑器手感落回 App 层测试工程（`a7l.6.4`）

**背景**：编辑器的手感（快击画一格、按住拖一串、拖拽阈值、边缘滚动节拍、滚轮整格、雷达点击、右键取色）在
`a7l.6.1` 之后都汇总到 `EditorScene.ProcessEvents(LocalEvent)`，但 `EditorScene` 在 `FHeroes2.App` 里，
`FHeroes2.UI.Tests` 引用不到（依赖方向是 App → UI）。手感一直靠 `--editor-paint` 之类的脚本动作与人工点验。

**做法**：新建 `tests/FHeroes2.App.Tests`（`FHeroes2.Slnx` 的 `/tests/` 下），引用 App 工程，直接驱动场景：

- 三处工程设置是必须的：`RuntimeIdentifier=win-x64`（App 是 RID 特定的可执行文件，引用它的工程要同名 RID）、
  `AppendRuntimeIdentifierToOutputPath=false`（输出仍落 `bin/Debug/net10.0`，`dotnet test` 照常发现用例）、
  程序集级 `SupportedOSPlatform("windows")`（`GameFonts` / 呈现层带该标注，否则每条用例都要挂 CA1416）。
- 用例填 `LocalEvent.Current` 的 `OnMouseMotion` / `OnMouseButton` / `OnMouseWheel`，再交给
  `ProcessEvents`（无时钟）或 `Update(dt)`（边缘滚动要时钟），与宿主每帧做的事一致；断言落在场景暴露的状态上
  （`Edits.UndoDepth`、`Edits.Brush`、`View.Scroll`、`View.TileAt`）。
- `LocalEvent.Current` 是全进程共享的，所以这些用例装在 `LocalEventCollection`（`DisableParallelization = true`）
  里串行跑，否则两条用例的光标会互相踩——和 ADR-318 是同一类坑。
- 素材照 UI 测试的老办法取 `AssetLocator.Resolve()`；机器上没有游戏数据时用例静默返回，不假装通过也不红。

**用例**：`EditorSceneSmokeTests`（建图即开编辑器的冒烟）与 `EditorSceneInputTests` 十条——视口/面板与
`MapFrame` 一致且 `DragThreshold=10`、`EdgeScrollStep=8`、`EdgeScrollInterval=20 ms`；同帧按下松开的快击画一格；
跨格笔画只记一步撤销；拖拽 10 px 内不平移、11 px 起按整个偏移平移；边缘每 20 ms 一步、少半个间隔要攒、长帧最多
四步且残留只保留一个间隔；滚轮整格；雷达点击改变滚动且不记笔画；右键取走指针下的地面。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7054 通过（Core 301 + Assets 441 + App 11 + UI 2283 + Game 4018），
0 警告 0 错误。

## ADR-320：地图翻译模型先做内存层，`.fh2m` 落盘另立（`a7l.5.3.1`）

**背景**：语言窗（`editor_language_window.cpp`）操作的是 `BaseMapFormat::mainLanguage` 与 `translations`，可这两样在
fheroes2 里只属于编辑器自有的 `.fh2m`（`map_format_info.h:375-386`）。端口只读写 MP2，`readMP2Map`
（`maps_fileinfo.cpp:190` 起）根本不填 translations —— MP2 里只有一套文本。所以「补齐语言窗」实际要先用一套 `.fh2m`
容器，那是 1000+ 行序列化加压缩容器的独立工程。

**做法**：先把模型落成内存态，按参考的分工拆开：

- `MapTranslationText` 对 `TranslationBaseMapMetadata`（`map_format_info.h:321-326`）：地图名、描述、创作者注记。
  参考的 `TranslationFormat`（`:328-343`）还有每日事件、传闻、城镇 / 英雄自定义名、斯芬克斯谜题与答案、路牌、地图事件
  的文本，端口编辑器尚未建模这些字段，等它们进来再接。
- `MapTranslationState` 三件事照抄参考：`ChangeLanguage` 走 `changeLanguage`（`map_format_helper.cpp:1904`）的三步——
  存旧主语言文本、载入目标语言存文本（空串也算有意留空，`:1991-1997`）、删掉目标语言槽再换主语言；`Add` 只入列留空文本
  （窗口 ADD，`:267-270`）；`RemoveTranslation` 拒绝主语言（`:2080-2084`）。`Languages` 主语言在前、其余按
  `SupportedLanguage` 枚举值升序，与参考的 `std::map` 一致。
- `MapSpecsState` 的 `Name` / `Description` 改为托管在 `translations.Current` 上，规格窗照旧读写，`CreateInfo` 出去的是
  当前主语言的文本。参考把这两样摊在 `MapFormat` 上，端口没有 `MapFormat`，只能这么落地。

**代价**：窗口切出来的其它语言文本只在内存里，存 MP2 时只剩主语言。这条缺口单独开 `FHeroes2Sharp-lg0`（`.fh2m` 最小
读写），不阻塞窗口本身。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7057 通过（Core 301 + Assets 441 + App 11 + UI 2283 + Game 4021），
新增 `MapTranslationStateTests` 八条，0 警告 0 错误。

## ADR-321：语言窗把「选语言」交回宿主，`ListBox<T>` 只收引用类型（`a7l.5.3.2`）

**背景**：`Editor::openLanguageWindow()`（`editor_language_window.cpp:133-306`）内部直接调
`fheroes2::selectLanguage()` 弹语言选择。端口的 `LanguageSelectionDialog` 是个需要事件循环的模态窗，而
`EditorSphinxWindow` 这类窗口一律只发请求旗标（`AnswerEditRequested`）、由宿主开子窗——照这个规矩，语言窗点 ADD 只
置 `LanguageAddRequested`，宿主拿去弹选择窗再喂回 `AddLanguage()`（`a7l.5.3.3` 接）。

**两处实现细节**：

- `ListBox<T>` 带 `where T : class`（`GetFromPosition` 要返回 `T?`），`SupportedLanguage` 是枚举，进不去。没去动共享
  控件的约束——那会波及八个派生类与两处泛型转发——而是在窗口里套一层 `EditorLanguageWindow.Row`（一行一个语言）。
- 写回照 `:222-241` 落在 `ApplyTo(MapTranslationState)` 上：列表没动且选中还是原主语言就直接返回；否则先把非当前项逐个
  `ChangeLanguage`，再把选中项设成主语言，最后把原列表里消失的语言 `RemoveTranslation`。这样窗口自洽，宿主只负责
  `Result == Accepted` 时调一次。

**已知偏差**：参考把滚动条槽画成 `roi.width`（300 宽），照抄会整条压在窗外；端口按 `EditorSphinxWindow` 的先例把槽收成
窗口内可用宽度。ADD/DELETE 按钮、标题居中、双击确定、三个右键说明文案都照参考。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7079 通过（Core 301 + Assets 441 + App 11 + UI 2300 + Game 4026），
新增 `EditorLanguageWindowTests` 十七条（布局、行高、增删、去重、主语言不可加、双击确定、ADD 请求、写回顺序、无改动
不写、消失的语言被删、绘制），0 警告 0 错误。

## ADR-322：语言窗只做宿主桥，按钮接线留给规格窗任务（`a7l.5.3.3`）

**背景**：`a7l.5.3.3` 原本写的是「编辑器菜单/地图规格窗加语言入口，并把 App 层 `EditorScene` 的窗口链路接上」。勘察
后发现两件事：参考里语言窗**只能**从地图规格窗的 LANGUAGE 按钮进（`editor_map_specs_window.cpp:2297`），而端口的
`EditorMapSpecsInput.Process` 明确把 RUMORS/EVENTS/LANGUAGE/ABOUT 四个按钮留给 `a7l.3.5.3.3`（那条被 `a7l.5` epic
阻塞，只等语言窗关闭就绪）；`EditorScene` 至今没有对话框链路，也没有一句 `MapSpecsState` 在场，硬接等于先造一个
不属于本任务的窗口系统。

**做法**：本任务只补「语言窗这一侧」的宿主桥，把接线压到几十行：

- `Create( MapSpecsState, ... )` 从规格状态直接建窗（内部走 `specs.Translations`），`ApplyTo( MapSpecsState )` 写回。
  因为 `MapSpecsState.Name` / `Description` 已经托管在主语言的文本槽里（ADR-320），写回后规格窗**不用搬文本**，重读
  一遍就是新主语言的内容——这一点与参考的 `renderMapName()` 等价，且有测试钉住。
- `CreateLanguagePicker( Size )` 把 ADD 要弹的选择窗装配好：`SupportedLanguage` 全集、初始选中主语言、标题
  `Select Language:`（对应 `selectLanguage( ..., isGameLanguage = false )`）、按钮组复用 `EditorListArtwork` 的
  OKAY/CANCEL 帧（私有 `EditorButtonArtwork` 适配 `IDialogButtonArtworkSource`，不去碰 `SystemButtons`）。
- 参考 `:253-256` 的「选回主语言直接忽略」仍由 `AddLanguage` 拒绝，选择窗无过滤，行为一致。

**交接**：`a7l.3.5.3.3` 里只剩余「LANGUAGE 分支 → 自旋 → `AddLanguage` / `ApplyTo` → 重画名字描述」与 App 层 smoke，
已用 `bd note` 把调用顺序写在那条 issue 上。MP2 存档只有主语言文本这条限制见 ADR-320 与 `FHeroes2Sharp-lg0`。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7083 通过（Core 301 + Assets 441 + App 11 + Game 4026 + UI 2304），
`EditorLanguageWindowTests` 增至二十一条（新增从规格状态建窗、写回后名字跟着换主语言、选择窗全集与选中主语言、主语言
不在首位时选中仍正确），0 警告 0 错误。



## ADR-323：规格窗把四个子窗都交回宿主，注记与传闻先进状态（`a7l.3.5.3.3`）

**背景**：参考的规格窗在事件循环里直接调 `openRumorWindow` / `openDailyEventsWindow` / `openLanguageWindow`
并把 `mapFormat` 传进去就地改（`editor_map_specs_window.cpp:2282-2311`、`:2370-2377`）。端口的规矩相反：窗口只发
请求、宿主开子窗（`EditorSphinxWindow` 的 `AnswerEditRequested` 就是这样）。于是这一层要补的不是「开窗」而是
**请求种类 + 工厂 + 写回**三件套。

**做法**：

- `EditorMapSpecsRequestKind` 增加 `OpenRumors` / `OpenDailyEvents` / `OpenLanguage` / `EditCreatorNotes`。
  前三个点击只置请求（`Body` 为空、`Dialog` 为 null），第四个与名称/描述同路：`CreateTextInput( ..., isMultiLine: true,
  MessageCharLimit )`。
- 工厂 `CreateRumorWindow` / `CreateDailyEventsWindow` / `CreateLanguageWindow` 只在 `Request.Kind` 对得上时返回
  窗口，否则 null——宿主按请求种类 switch 一次，不用自己拼参数。`ScreenBuffer.DefaultSize` 是省略屏幕尺寸时的默认值。
- 写回：`ResolveRumors( IReadOnlyList<string>? )` / `ResolveDailyEvents( ... )` 传 null 表示取消（参考取消时不动
  `mapFormat`）；`ResolveLanguage( window )` 内部判 `Accepted` 再 `ApplyTo` 状态。三者都清空 `Request`。
- 语言窗关掉后的「重画名称/描述/两个条件面板」在端口退化成一次重读：`MapSpecsState.Name`/`Description` 本来就存在
  当前主语言的文本槽里（ADR-320），条件面板每帧从状态重画，所以 `ResolveLanguage` 返回的 bool 只用来触发整页重画。
- 数据侧：`MapSpecsState` 增加 `CreatorNotes`（存在主语言文本槽，`CreateInfo` 带进 `Mp2FileInfo.CreatorNotes`，
  MP2 的 428 字节头部不写它）、`Rumors` / `SetRumors`、`DailyEvents` / `SetDailyEvents`（存副本，窗口自己那条
  再改不影响状态）。参考把传闻与每日事件按语言各存一份，端口的 `.fh2m` 还没做（`FHeroes2Sharp-lg0`），所以先只存
  主语言那一份，注释里写明。

**App 层为何没有 smoke**：`EditorScene` 至今不挂规格窗，也没有对话框链路；App 侧的入口是 `a7l.7`（面板四个系统按钮
接线）。已经用 `bd note` 把宿主调用顺序写在那条 issue 上，本任务的测试覆盖到「点击 → 请求 → 工厂 → 写回 → 状态」整链。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7094 通过（Core 301 + Assets 441 + App 11 + Game 4030 + UI 2311）。
UI 侧 `EditorMapSpecsInputTests` 增七条（三个按钮各自的请求、ABOUT 的多行输入与 999 上限、注记写回、传闻写回含取消、
每日事件写回含取消、三个工厂只认自己的请求、`ResolveLanguage` 应用 Accepted 的主语言），Game 侧
`MapSpecsStateTests` 增四条（新状态三样为空、传闻存副本、每日事件存副本、注记进头部），0 警告 0 错误。

## ADR-324：编辑器场景有一条宿主缝，面板右键先用它弹提示（`a7l.7.1`）

**背景**：参考的编辑器直接调阻塞窗口函数（`showStandardTextMessage`、`openMapSpecificationsDialog`、
`eventFileDialog`、`openEditorSettings`），端口只有一条帧循环，窗口必须进内层循环。冒险地图早就这么干了
（`AdventureHeroActionHost.RunModal` 由 `GameHost.cs:3257` 接上），编辑器场景却一直没有这条缝——四个系统按钮
因此全空着。

**做法**：

- 新增 `EditorSceneHost`（`src/FHeroes2.App/Scenes/EditorSceneHost.cs`）：收下 `AssetBundle` 与 `GameFonts`，
  暴露可设的 `RunModal`，以及 `ShowMessage( title, body )`——就是参考的 `showStandardTextMessage`，OK 单按钮，
  按钮美术走 `SystemButtonArtwork.TryCreate`。缺帧泵、缺字体或缺按钮时只写一行 `GameLog.Warn` 并返回 false，
  不抛异常：无数据环境下右键是安全的。
- `EditorScene` 自己造宿主（`Assemble` 里已有 assets/fonts），以 `public Action<IModalScreen>? RunModal` 转出去，
  `GameHost` 在两处 `_editor = scene;` 之后接 `RunModal`（新建地图、打开地图各一处）。之所以不用构造参数：
  场景由静态工厂在宿主之外造出来，接缝只能后置。
- 面板提示：`EditorPanel.SystemHint(EditorSystemButton)` 收六条文案（Magnify/Undo/Redo/Specifications/File Options/
  System Options，文案照抄 `editor_interface_panel.cpp:1461-1479`）。`EditorScene.SetRightButton` 在物件窗之前先问面板：
  命中系统按钮就弹提示、这一下右键到此为止；其余控件（仪器、地形、笔刷）保持端口原有行为（右键即平移），
  参考给它们也配了提示，留待后补。

**测试**：App.Tests 第一次能在没有 SDL 的情况下弹真窗口——`GameFonts.TryLoad( assets )` 不碰 SDL，消息框的样式框走
`GameAssets.Current`（用例自己换上再换回），于是 `EditorPanelHintTests` 能造真场景、算六个按钮的像素中心、给
`RunModal` 装一个只记数的假泵，逐个右键断言「一下一个框」；另一条用 `fonts: null` 断言缺字体时一个框也不弹。
按钮中心按 48x36 与「面板底两行」算，和 `EditorPanel.HitTest` 的布局同源。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7096 通过（Core 301 + Assets 441 + App 13 + Game 4030 + UI 2311），
0 警告 0 错误；`--scene editor --smoke-frames 30` 正常退出。

## ADR-325：规格窗在宿主帧泵上跑，内层窗摞在自己的栈上（`a7l.7.2`）

**背景**：`EditorMapSpecsInput`/`EditorMapSpecsWindow` 在 UI 层早就齐了（ADR-319~323），缺的只有 App 层的宿主：
谁来开窗、谁来喂事件、关窗后谁来写回。参考里 `openMapSpecificationsDialog()` 是个阻塞函数，里面再用
`Dialog::inputString` / `showStandardTextMessage` 套内层 while。

**做法**：

- `EditorSceneHost` 长出三个造窗口的方法：`TryCreateMessageBox`（OK 单按钮，`SystemButtonArtwork`）、
  `TryCreateInput`（`InputStringWindow`，吃请求里带来的 `InputStringDialog`）、`RunMapSpecs(session, palette)`
  （造 `EditorMapSpecsWindow` + `EditorMapSpecsInput` + 模态并跑到底，失败再弹错误框）。字体/按钮/页面美术的
  取值都留在宿主，场景只留一句 `case EditorSystemButton.Specs: _host.RunMapSpecs(_edits, _frame.Palette)`。
- `EditorMapSpecsModal`（`src/FHeroes2.App/Modals/`）：每帧读宿主那份 `LocalEvent.Current`——
  和 `EditorScene` 读的是同一个单例，`GameHost.PumpEvents` 先填 `_le` 再转发模态，所以不必自己合成事件。
  内层窗（消息框、文本输入，后面还有子窗）摞在模态自己的 `Stack<IModalScreen>` 上：栈上有窗口时这一帧只更新栈顶、
  只把 `SetCursor`/`SetLeftButton` 转给栈顶，栈空了才轮到规格页的 `Process`。为什么不递归
  `GameHost.RunModal`：那里 `_modal is not null` 就抛，参考的内层 while 在端口只能靠这一层栈表示。
- 关窗两步照参考：`Accepted` 就 `EditorMapSpecsInput.Commit(session)` 把 `MapSpecsState` 写回地图，写不进去
  （只有尺寸对不上）把消息留在 `Error` 上，宿主在模态结束后弹 `Error` 框；`Cancelled` 什么都不写。
- `GameHost` 里那处 `scene.RunModal = RunModal` 从方法末尾挪到**脚本交互之前**：`--editor-panel-click` 点到的
  按钮会开窗，帧泵没接上就只能写一行「no frame pump」。

**测试**：`EditorMapSpecsModalTests` 三条，用例自己当帧泵（`scene.RunModal` 换成一段直接推进模态的代码，
事件塞进 `LocalEvent.Current`，关闭用回车 / ESC——参考 `:2266-2272` 认的就是这两个键）：确定后
`Edits.IsDirty` 变真、地图名不变；取消后 `IsDirty` 仍为假（顺带证明点面板没有误画到地图上）；帧泵没接上时点按钮
什么都不发生。三条都要真数据与 `GameFonts.TryLoad`，没有就跳过；与 `EditorPanelHintTests` 一起放进
`LocalEventCollection`，因为它们都改进程级的 `LocalEvent.Current` / `GameAssets.Current`。

**已知缺口**（都建了 `discovered-from` 的 issue）：传闻/每日事件/语言三个子窗与其 Add/Edit 输入循环归 `qto`；
条件的目标选择器与数值键盘（UI 层还没有这块窗）归 `n0d`；`OnFrameEnded` 在 `_modal` 非空时直接返回，导致
`--screenshot-frame` 拍不到内层循环里的窗口，归 `74t`——所以这一层没有截图基线，只有 App 测试。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7099 通过（Core 301 + Assets 441 + App 16 + Game 4030 + UI 2311），
0 警告 0 错误；`--scene editor --smoke-frames 30` 正常退出；`--editor-panel-click 24,302`（SPECS 格）会进规格窗，
跑满预算后退出，日志里不再出现「no frame pump」。

## ADR-326：法术选择窗的宿主与「换等级」的重开循环（`a7l.9.1`）

**背景**：`EditorSpellSelectionWindow`/`EditorSpellSelectionInput` 在 UI 层齐了，缺的还是 App 层那句「谁来开窗、
谁来喂事件、谁在换等级时重开」。参考里这是 `openSpellSelectionWindow()`：一个阻塞函数，循环里先认
`DEFAULT_CANCEL` 再认 `DEFAULT_OKAY`（`editor_spell_selection.cpp:280-284`），等级键上直接 `break`
（`:324-326`），重开的 `while` 在调用方（`editor_castle_details_window.cpp:515`）。

**做法**：

- `EditorSceneHost.RunSpellSelection(title, spellLevel, selectedSpells, isMultiLevelSelectionEnabled,
  minimumEnabledSpells, pickDisabledSpells)` 返回 `EditorSpellSelectionDialogResult(Accepted, SpellLevel, Spells)`：
  参考的 `bool`、被引用改写的 `spellLevel`、被写回的列表，三样都收进一个记录。参考的两层循环
  （窗口 `break` + 调用方 `while`）合成宿主里的一个循环——换等级就按新等级重建窗口再跑一层，调用方只看结果。
  「等级没变却说要换」当取消收场，避免死循环。
- `EditorSpellSelectionModal`（`src/FHeroes2.App/Modals/`）：每帧先认两个热键、再交给
  `EditorSpellSelectionInput.Process`——顺序与参考一致，右键提示那几处插在关窗之前。内层窗（说明框）摞在自己的
  `Stack<IModalScreen>` 上，同 `EditorMapSpecsModal`，栈上有东西时这一帧只归栈顶。`Accepted` 时读
  `GetSelectedSpells()`，`Cancelled` 与换等级都留空列表——参考在换等级前就 `break` 了，什么都没写回。
- `FHeroes2.App.csproj` 加 `InternalsVisibleTo("FHeroes2.App.Tests")`：宿主与模态都是 App 的内部缝，走整个
  场景去够一个窗口反而把被测的东西藏起来了。`EditorSceneHost` 保持 `internal`。

**测试**：`EditorSpellSelectionModalTests` 三条，用例直接当帧泵喂宿主：ESC 取消（结果不接受、列表空、等级不变）、
回车确定（先点掉一个法术，结果条数 = `Grid.Count - 1`，验的是「关掉的那个没被算进去」）、点等级键 2 之后
宿主重开一层（级数 2、窗口开了两次、第二次的等级是 2）。用例自己确认数据真的在（`_assets`/`_fonts` 缺了就让路，
和 smoke 测试一个风格）。

**已知缺口**（都挂在 `a7l.9` 下）：`ShowSpell` 请求还没有法术说明窗可接（`a7l.9.2`）；两处真正的调用点
（神殿/金字塔的物件元数据、城堡详情的禁法 `while`）要等各自的窗移植（`a7l.9.3`）——这层现在只有接口与测试。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7102 通过（Core 301 + Assets 441 + App 19 + Game 4030 + UI 2311），
`dotnet build FHeroes2Sharp.slnx` 0 警告 0 错误；`--scene editor --smoke-frames 30` 正常退出（74.3 FPS）。

## ADR-327：按压态与绘制、判定分开（`bpn.1`）

**背景**：参考的城堡详情窗在同一个 `while` 里依次做三件事：`drawOnState( isMouseLeftButtonPressedAndHeldInArea( area ) )`
（`:845-849`，RESTRICT 那颗是 `buildingRestriction || ...`，`:876`）、`MouseClickLeft` 判定、`display.render()`。
端口早先把窗口拆成三块：`CastleDetailsView` 只画、`CastleDetailsInput` 只判、`CastleDetailsState` 只存。

**做法**：按压态单独一个入口 `CastleDetailsView.UpdateButtonPressStates(LocalEvent)`，六颗按钮各
`ButtonBase.UpdatePressState(events)`（本来就只改状态不画），RESTRICT 在 `State.BuildingRestriction` 为真时直接
`Press()`。`Paint` 不读事件，画的是「此刻的状态」，所以宿主每帧的顺序是 `UpdateButtonPressStates` → `Paint` →
`Input.Process`。

**为什么不给 `Paint` 加一个 `LocalEvent` 参数**：那样绘制就同时依赖输入，六颗按钮的状态要在 Frame 的三个
`Paint*Button` 里各算一次，按钮还得先造好（现在是第一次 `Paint` 才造）；`Paint` 的六个现有调用点也都要改。
分开之后 `UpdateButtonPressStates` 在按钮造出来之前是空转，第一帧的差别看不出来（参考的按钮在循环之前就摆好，
但第一帧之前没人看画面）。

**测试**：`CastleDetailsViewTests` 三条——按住 OKAY 只有它按下、RESTRICT 在限制模式下不看鼠标也亮、
按下态确实进画面（两帧同一区域像素不同；状态没传下去这条会自己失败）。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7105 通过（Core 301 + Assets 441 + App 19 + Game 4030 + UI 2314），
0 警告 0 错误。

## ADR-328：编辑器选项页的三层——一轮的模态、宿主的窗口、场景的循环（`a7l.7.4.1`）

**背景**：`EditorOptionsDialog`（UI，455 行）早就齐了，缺的是「谁来开它、谁来喂事件、动作回来之后谁去改设置并重开」。
参考是 `openEditorSettings()`（`editor_options.cpp:235-378`）：一个 `while`，每圈 `openEditorOptionsDialog()`
阻塞跑完、关窗则跳出，动作则改设置（并 `redrawEditor`/`rebuildEditor`），最后按需 `conf.Save`。

**做法**：

- **一轮 = 一个模态**。`EditorSceneHost.RunEditorOptionsDialog()` 建美术、建页、跑一层 `EditorOptionsModal`，
  返回 `EditorOptionsAction`。参考的「重开一层」没有塞进宿主——宿主只管窗口。
- **循环在场景里**。`EditorScene.OpenEditorOptions()` 才是 `openEditorSettings()` 的对应物：五个开关
  （动画、通行性、界面风格、指针、滚动速度）改完 `Settings` 再来一层，`Exit` 时按需
  `SavePreferences?.Invoke()`。放在场景里的原因：参考循环体里那两个 `redrawEditor`/`rebuildEditor` 是地图与
  面板的活，放进宿主就要给宿主塞两三个回调。
- **落盘留给宿主**。`SavePreferences` 是个 `Action?`，`GameHost` 在两处场景装配点上接 `SaveSettings`；
  没接上时设置只留在内存里，和 `--no-config` 一样。编辑器不该知道配置文件的路径。
- **右键说明摞在模态自己的栈上**，同 `EditorMapSpecsModal`：`EditorOptionsDialog.TakePopup()` 拿到的说明用
  `EditorSceneHost.TryCreateMessageBox` 建窗，栈上有东西时这一帧只归栈顶。

**美术的真相比预期麻烦**：`GAME_OPTION_ICON` 不是归档里的帧——参考在载入时用 `ESPANBKG` 的 (69, 47) 那块
65×65 的底加 H2D 的 `hotkeys_option_icon.image`/`graphics_option_icon.image` 合成
（`game_assets.cpp:5187-5203`）。这一层于是也照做（`ComposeGameOptionFrames`），H2D 没加载时留底不报错。

**测试**：`EditorOptionsModalTests` 六条，用真归档打：四个归档读得全；ESC 退出；点通行性报 `Passability`；
右键说明摞在上面（页面既不结束也不产出动作，关掉说明框后 ESC 才结束）；`System` 按钮走完整循环（窗口开两次、
设置翻转、落盘一次）；什么都没改就关窗不落盘。

**已知缺口**（都挂在 `a7l.7.4` 下）：换界面风格只改了设置位，面板与边框没有换皮（`a7l.7.4.2`）；通行性叠层与
对象动画在编辑器渲染里根本不存在，两个开关现在只翻位（`a7l.7.4.3`）；语言/图形/音频/热键四个子页的窗 UI 层都有
但没有模态（`a7l.7.4.4`）。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7111 通过（Core 301 + Assets 441 + App 25 + Game 4030 + UI 2314），
`dotnet build` 0 警告 0 错误，`--scene editor --smoke-frames 30` 正常（75.9 FPS）。
`--editor-panel-click` 点 System 按钮那条路在 smoke 里开不出窗（`RunModal` 的循环要求 `_running`，脚本点击
发生在主循环之前），和规格窗一样只能靠 App 测试验证。

## ADR-329：编辑器文件菜单是一块自己排版的窗（`a7l.7.3`）

**背景**：参考的 `EditorInterface::eventFileDialog()`（`editor_interface.cpp:2068-2181`）用 `StandardWindow`
把七颗 `BUTTONS_EDITOR_FILE_DIALOG` 摆成三列，底边中上方 11 像素挂一颗小取消，然后一个 `while` 认九处右键、
五组热键、七颗按钮，最后回一个 `GameMode`。端口的 `DialogButton` 是 `Dialog::Button` 的照搬，没有
「开始地图 / 主菜单 / 自动测试」三个位；`IcnType` 里也没有 `ButtonsEditorFileDialogGood`——那是参考在载入时用
`makeSymmetricBackgroundSprites` 把标签压进底图现做的（`game_assets.cpp:1579-1593`）。

**做法**：

- **窗只管排版与判定**（`EditorFileDialog`）。它不读归档、不认设置：十四块牌子与两帧取消键由调用方给。这与
  `FileOptionsView` 对游戏那张文件菜单的处理一致，只是这边取消键照参考取 `BUTTON_SMALL_CANCEL` 的 0/1 帧
  （`:2090`），游戏那张取 `SYSTEM.ICN` 的 3/4 帧。
- **`DialogButton` 补三个位**（`EditorStartMap` / `EditorToMainMenu` / `EditorAutoPlaytest`），新图/载图/
  存图/退出复用上面已有的四个位——语义本来就一样。参考这一处是按序号认按钮的，端口没有序号通道，所以只能把
  这三颗补进位集。
- **热键拿这一帧的事件比**，不是 `GameHotKeys.HotKeyPressEvent`（那个读全局输入单例）。同
  `AdventureMap.Turn.HotKeyPressed`：`events.IsAnyKeyPressed && events.PressedKeyValue == GetHotKeyForEvent(id)`。
  好处是窗口在测试里能被喂事件；`EditorOptionsDialog` 的 OK/CANCEL 也是这个路子。
- **模态照 a7l.7.4 的分层**：`EditorFileModal` 每帧 `ProcessEvents` 一次、拿到结论就收摊，右键说明摞在它自己的栈
  上；存图前要 `hideWindow()` 这一步留给调用方——模态结束的那一帧菜单就没了，下一帧重画场景，保存窗自然压不着它。

**测试**：`EditorFileDialogTests` 十三条（三列排布、次序、取消键位置、四种残缺输入、八次点击、按下落在别的按钮上
不算、七组热键、八处右键说明、`FromDialogButton` 映射、真路径合成七块牌子、绘制）；`EditorFileMenuModalTests`
六条（七块牌子由真字体压出来且取消键落在窗内、SAVE MAP 与 QUIT 与取消各自报得出来、右键说明摞在上面不关菜单、
字体缺失时压根不开窗）。

**这次的缺口**（都记成了 `discovered-from`）：存图链路 `fsk`（`EditorSaveMapWindow` 的 App 模态、文件名、
覆盖确认、落盘）；其余六颗 `beb`（确认框、开始地图与自动测试要 World、主菜单与退出要与编辑器的退出确认打通）。
`EditorScene.OpenFileMenu()` 现在对这六个动作留一行 `Warn`——不静默，也不假装做成了。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7130 通过（Core 301 + Assets 441 + App 31 + Game 4030 + UI 2327），
`dotnet build` 0 警告 0 错误，`--scene editor --smoke-frames 30` 正常（78.0 FPS）。

## ADR-330：动 `GameAssets.Current` 的用例必须并进串行集合（`bp7`）

**现象**：`dotnet test FHeroes2Sharp.slnx`（五工程一起跑）时，UI.Tests 约每 3~5 轮挂一条，**没有 Error Message**。

**查法**：先按 `bp7` 的建议连跑全量抓 `[FAIL]`——13 轮全绿（每轮 UI.Tests 2327），单跑更不会挂；再挂 4 个
忙循环制造 CPU 压力跑 3 轮，还是全绿。靠运气抓不到，于是改成静态审计全局态：

- `GameAssets.Current` 是进程级静态槽。`GlobalArchiveCollection` 的定义带 `DisableParallelization = true`，
  也就是进这个 collection 的类既不互相并行，也不和别的 collection 并行。
- 扫全部 160 个 UI.Tests 文件：**唯一**一个「改 `GameAssets.Current` 却没进 collection」的是
  `RandomMapGeneratorDialogTests`。它的 `TryWithArchives` 自己 `AssetBundle.Open`、装进 `Current`、
  跑完 `= null`。并行池里别的用例（尤其断言像素那几条）恰好在这一小段里读素材，就会读到别人的归档或者空槽
  ——像素断言挂了本来就不带异常信息，正好对上「无 Error Message」。

**改法**：

1. `RandomMapGeneratorDialogTests` 加 `[Collection(GlobalArchiveCollection.Name)]`。改完再扫一遍，**全装配里
   已经没有不串行的全局态写入者**（App.Tests 那边八个类本来就都并进了 `LocalEventCollection`）。
2. `TryWithArchives` 的 `finally` 从 `GameAssets.Current = null` 改成还回原值——置空会把别人装好的归档
   一起拆掉，就算以后并行策略变了也不该这样。
3. 新增 `GlobalStateUsageTests`：用 `[CallerFilePath]` 找到本目录源码，扫 `GameAssets.Current =` 却没带
   `[Collection(` 的文件，报出来。这条规则以前只靠自觉，现在有测试守。用一个临时的犯规文件验证过它真的会红
   （跑完即删）。

**为什么用源码扫描**：「有没有挂 attribute」是编译期事实，反射看不出来。用例在源码目录不在时直接跳过，读 160 个
文件 33 毫秒。

**没做到的诚实交代**：这条偶发失败本身**没能复现**（16 轮全量，含 3 轮带 CPU 压力）。上面那条竞态是审计出来的、
确实存在、确实符合症状，但它是「最可能」而不是「已证实」的成因。若再出现，抓 `[FAIL]` 行即可（`-v n` 的日志
里有用例名，`bp7` 当初没抓到是因为没存日志）。

**验证**：`dotnet test FHeroes2Sharp.slnx` 全量 7131 通过（Core 301 + Assets 441 + App 31 + Game 4030 +
UI 2328），`dotnet build` 0 警告 0 错误。

## ADR-331：传闻子窗摞在规格页上，输入框再摞在传闻窗上（`qto.1`）

**参考**：`editor_map_specs_window.cpp:2282-2288` 里 `RUMORS` 一段调 `openRumorWindow()`，那是个阻塞循环
（`editor_rumor_window.cpp:135-305`）：加一条、改一条各在循环里 `Dialog::inputString` 再阻塞一层，重复了
`showStandardTextMessage` 骂一句（`:237`、`:261`），最后 `return true` 才写回。三层嵌套，端口只有一层栈。

**做法**：

1. `EditorRumorModal : IModalScreen` 包住 `EditorRumorWindow`，自己再持一个 `Stack<IModalScreen>`。栈上有窗口时
   这一帧只推进它，`EditorRumorWindow.Process` 一个事件都不读——同 `EditorMapSpecsModal` 的规矩。
2. 列表自己举旗（`AddRequested` / `EditRequested` / `Popup`），这里只答：加/改开
   `InputStringDialog.Create(InputTitle, InputBody, LongestRumor, isMultiLine: true, ...)`，答案喂
   `TryAddRumor` / `TryReplaceSelected`，被拒就把列表刚设好的 `Popup` 用 `MessageBoxModal` 弹出来。
3. `AcceptedRumors` 只在 OKAY 时非 null，取消就是参考的 `return false`——`EditorMapSpecsModal.Resolve` 拿它
   喂 `ResolveRumors(null)`，地图不动。
4. 宿主新增 `TryCreateListArtwork()` 与 `TryGetFonts(out GameFonts?)`：三个列表窗共用同一份
   `EditorListArtwork`（`TryCreate(IsEvilInterfaceEnabled)`），字体从既有 `_fonts` 借。

**踩到的坑（写进测试）**：多行输入框**回车不能收窗**——`InputStringView.ProcessHotKey` 里
`case ButtonHotKey.Okay when !_dialog.IsMultiLine`，参考多行输入里 Enter 是换行，只能点 OKAY。为此给
`InputStringWindow` 加了 `OkayArea`（`EditorRumorWindow` 早就把 `Okay` / `Cancel` 摆出来了）。另外内层窗
不吃 `LocalEvent`：宿主的 `SetCursor` / `SetLeftButton` 才是它的点击路径，用例里两种点击分开写。

**验证**：`EditorRumorModalTests` 六条——打字确认后列表多一条并写回、取消不写、取消输入框不动列表、重复条目弹
提示且列表不长、改选中的那条（退三格再打一个字母）换掉整条、以及从面板 SPECS 一路点到传闻窗（`TopWindow is
EditorRumorModal`）再两层 ESC 退出、地图未变脏。全量 7137 通过（Core 301 + Assets 441 + App 37 + Game 4030 +
UI 2328），`dotnet build` 0 警告 0 错误，`--scene editor --smoke-frames 30` 正常（67.5 FPS）。

**没接的**：每日事件窗与语言窗（`qto.2` / `qto.3`，已带依赖排在后面），条件的目标选择器仍走 `Warn`。

## ADR-332：每日事件窗摞进规格页，事件窗再摞消息输入框（`qto.2`）

**参考**：`editor_map_specs_window.cpp:2290-2297` 的 `EVENTS` 一段调 `openDailyEventsWindow()`
（`editor_daily_events_window.cpp:160-318`）；它的加/改都走 `editDailyEvent()`（`:252-281`），那个函数自己阻塞开
608×430 的事件窗（`editor_daily_event_spec_window.cpp:67-372`），事件窗里点消息区再阻塞开 `Dialog::inputString`
（`:332-344`）。三层嵌套。

**做法**：

1. `EditorDailyEventsModal` 包列表窗，自持一层栈；栈上非空这一帧只推进栈顶，列表不读事件。
   `AddRequested` 开空事件、`EditRequested` 开选中项的副本（`SelectedEvent.Clone()`），
   换/加在事件窗点头之后（`:254-261`、`:273-278`），取消就什么都不写。
2. `EditorDailyEventSpecModal` 包事件窗，自己再持一层栈：`MessageRequested` 摞 `InputStringDialog.Create(
   "", "Message:", MessageCharLimit, isMultiLine: true, 当前消息)`，答完 `SetMessage`（`:337`）。
   `ResourceCountRequest`（`SelectCountDialog`）与 `NumpadRequest` 宿主还没有窗口，落 `GameLog.Warn`。
3. 事件窗的颜色集交给 `EditorDailyEventSpecWindow.Create` 的 `Clamp`，`EditorMapSpecsModal` 只把
   `State.HumanPlayers` / `ComputerPlayers` 递下去（参考 `:71-72`）。
4. 宿主新增 `TryCreateDailyEventArtwork()`，与 `_listArtwork` 一样按界面风格缓存一次。

**踩到的坑**：内层窗的点击要等外层 `Update` 把栈弹掉才生效——测试里 `AcceptInput` 之后必须 `Pump` 一帧，
再断言 `HasInnerWindow`；否则 `ClickLeft` 已经关了输入框，但事件窗还压着它，消息也没回写。这条写进了用例注释。

**验证**：`EditorDailyEventsModalTests` 六条——加一条原样收下、取消事件窗不加、取消列表不写、
消息区打字的字落到事件上、改选中那条（退三格再打一个字母）换掉整条、从面板 SPECS 点 EVENTS 后
`TopWindow is EditorDailyEventsModal` 再两层 ESC 退出且地图不变脏。全量 **7143 通过**（Core 301 + Assets 441 +
App 43 + Game 4030 + UI 2328），0 警告 0 错误，编辑器冒烟 75.6 FPS。

**拆出去的**：`7we`——每日事件窗的数量窗与数字键盘（与 `n0d` 的 `EditValue` 同类）。
**没接的**：语言窗（`qto.3`，已带依赖排在后面），条件的目标选择器仍走 `Warn`。

## ADR-333：语言窗摞进规格页，ADD 的语言选择器再摞一层（`qto.3`）

**参考**：`editor_map_specs_window.cpp:2298-2311` 的 `LANGUAGE` 一段调 `openLanguageWindow()`
（`editor_language_window.cpp:133-306`）；它的 ADD 里阻塞调 `fheroes2::selectLanguage( ..., isGameLanguage = false )`
（`:253-269`），选中的语言再 `languageList.Add()`，重复的骂一句（`:261`）。

**做法**：

1. `EditorLanguageModal` 包 `EditorLanguageWindow`，自持一层栈：`LanguageAddRequested` 摞
   `LanguagePickerModal`（包 `EditorLanguageWindow.CreateLanguagePicker` 造的 `LanguageSelectionDialog`），
   点头后 `AddLanguage`；被拒且 `Popup` 非空时再摞 `MessageBoxModal`（与 `EditorRumorModal` 同一手法，
   在 `Resolve` 里立刻读 `Popup`，不等下一帧 `Process` 把它清掉）。
2. `LanguagePickerModal` 是 `IModalScreen` 的最小包装：`LanguageSelectionDialog` 只吃
   `SetCursor` / `SetLeftButton` / `ProcessHotKey`，不读 `LocalEvent`，所以当内层窗正合适；
   索引到 `SupportedLanguage` 的映射与 `CreateLanguagePicker` 造名字时用的 `Enum.GetValues` 同一顺序。
3. 写回仍交给 `EditorMapSpecsInput.ResolveLanguage(window)`：它读 `window.Result` 与 `ApplyTo`，
   所以 `EditorMapSpecsModal.Resolve` 只加一个 `EditorLanguageModal` 分支。

**验证**：`EditorLanguageModalTests` 七条——加一门进列表、取消选择器不加、选主语言静默忽略、重复加弹提示、
接受后 `ApplyTo` 写回两门、取消不写、从面板 SPECS 点 LANGUAGE 加一门再两层 OKAY 且地图被标脏
（证明 `ResolveLanguage` 真被走到）。全量 **7150 通过**（Core 301 + Assets 441 + App 50 + Game 4030 + UI 2328），
0 警告 0 错误，编辑器冒烟 80.9 FPS。

**收尾**：`qto` 三个子窗（传闻 / 每日事件 / 语言）到此全部接进 App；剩下的条件目标选择器与数值输入在 `n0d`，
每日事件的数量窗与数字键盘在 `7we`。

## ADR-334：法术选择窗的右键说明框（`a7l.9.2`）

**参考**：`editor_spell_selection.cpp:155` 右键点法术那一格调 `SpellDialogElement( ... ).showPopup( Dialog::ZERO )`；
`ui_dialog.cpp:543-546` 的实现是 `showStandardTextMessage( _spell.GetName(), getSpellDescription( _spell, _hero ), buttons, { this } )`——
标题黄字是法术名、正文白字是说明、元素就是法术自己（图标 + 小字名），一个 OKAY 关掉。

**做法**：

1. UI 层新增 `SpellDescriptionDialog.Create(spell, icon, headerFont, bodyFont, labelFont, buttonArtwork, screen?)`：
   把参考那四个参数凑成 `MessageBox.Create(名字, 说明, 黄, 白, [new SpellDialogElement(icon, spell, 0, 小字白)], buttons: Ok)`。
   排版复用 `MessageBox`（它本来就把元素单独排一行放在文字下面），不新造布局。
2. 宿主新增 `TryCreateSpellInfo(SpellType)`：字体缺失、按钮美术缺失、`ICN::SPELLS` 缺帧时各自 `GameLog.Warn` 返回 null；
   图标下标与参考同一个 `Spell::IndexSprite()`，按需从归档取，不缓存整份法术美术。
3. `EditorSpellSelectionModal.Open` 改成 switch：`ShowMessage` 走消息框、`ShowSpell` 走说明框，谁也给不出才落 Warn。
   `MessageBoxModal` 加 `Box` 属性，`EditorSpellSelectionModal` 加 `HasInnerWindow` / `TopWindow`，供测试与后续子窗复用。

**验证**：`EditorSpellSelectionModalTests` 四条（新增 `RightClickingASpellShowsItsDescription`）——右键第一格弹出
`MessageBoxModal`，标题等于 `new Spell(type).GetName()`、正文等于 `GetDescription()`、框里正好一个元素、按钮行非空；
回车关掉说明框后选择窗还在，ESC 退出且 `Accepted` 为假。全量 **7151 通过**（Core 301 + Assets 441 + App 51 +
Game 4030 + UI 2328），0 警告 0 错误，编辑器冒烟 84.6 FPS。

**口径**：右键判定是「按住且在区域内」，测试里右键那一帧只按下，抬起留到帧后补——左键的 `MouseClickLeft` 是边沿触发，
两者不能共用同一个 helper。

## ADR-335：条件目标选择器接进规格页（`n0d.1`）

**参考**：条件面板的行被点中后调 `selectItems` 风格的模态（`editor_map_specs_window.cpp:1252-1264`）：
`SelectMapCastle` / `SelectMapHero` 把候选列成 `ItemSelectionWindow`，阻塞跑完再拿 `GetCenter()` 写回条件目标，
取消时那几个写回被 `if` 挡掉。

**拆分**：`n0d` 升级为 epic。原先一条 issue 里塞了两件互不依赖的事，数字键盘那半还有一层新窗口要造，
所以拆成 `n0d.1`（本 ADR）与 `n0d.2`（`EditValue` 的数字键盘，UI 层已有 `VirtualKeyboardView.TryCreateNumpad` /
`ReadNumpad`，差 App 侧接线）。

**做法**：

1. 宿主新增 `TryCreateItemSelectionArtwork()`：`new IcnViewWorldSpriteSource(_assets)` 交给
   `ItemSelectionWindowArtwork.TryCreate`，缓存在 `_itemSelectionArtwork` / `_sprites` 两个字段里，
   坏界面按 `Settings.Get().IsEvilInterfaceEnabled` 走；缺归档或缺帧都 `Warn` 返回 null。
2. 新增 `EditorMapSpecsTargetModal`（`IModalScreen`）包 `EditorMapSpecsTargetPicker`：
   `Update` 把 `LocalEvent.Current` 交给 `ProcessEvents`，`IsFinished` 取窗口的 `IsClosed`，
   `SelectedPosition` 只在 `Result == Ok` 时给坐标——取消与「列表空」都落成 null，正好对上参考的 `if` 门槛。
   `SetCursor` / `SetLeftButton` / `SetRightButton` 不做转发：列表窗本来就直接读 `LocalEvent`。
3. 热键不能当场交给窗口，否则同一帧的鼠标被读两遍（列表的一次单击会变成双击），所以 `HotKey` 只记下，
   下一次 `Update` 连同鼠标一起交过去。
4. `EditorMapSpecsModal` 的 switch 加 `PickConditionTarget` 分支（`TryCreateTargetPicker`），
   `Resolve` 加 `EditorMapSpecsTargetModal` 分支（`_input.ResolveTarget(SelectedPosition)`）。

**验证**：新增 `EditorMapSpecsTargetModalTests` 六条——宿主能从真实归档造出列表美术；两座城堡都在列表里且
起始选中当前目标；OKAY 交回 `state.VictoryTarget`；点第二行再 OKAY 交回第二座城的格子；CANCEL 交回 null；
回车接受、ESC 取消。全量 **7163 通过**（Core 301 + Assets 441 + App 57 + Game 4030 + UI 2328），
0 警告 0 错误，编辑器冒烟 77.2 FPS。

**遗留**：`EditorMapSpecsInput.ResolveTarget` 的写回语义由既有 UI 测试盯着（`TheTownPickerHandsTheNewTargetBackToTheInput`）；
App 侧这条分支走 compile-time switch，新建地图没有城堡，端到端要等有城镇的地图夹具再补。

## ADR-336：数值输入的数字键盘接进规格页（`n0d.2`）

**参考**：`openVirtualNumpad( value, minimum, maximum )`（`ui_keyboard.cpp:1295-1370`）是一个阻塞循环——
`while ( action != DialogAction::Close )` 里只驱动键盘本身；关窗（点确定，或 `Game::HotKeyCloseWindow()`，
回车与 ESC 都算）之后才 `std::stoi` 并比范围，打不成数字就 `showStandardTextMessage` 说「invalid」，
越界就说「out of range」，然后把 action 改回 `DoNothing`——**键盘不关、数字不清，可以接着改**。
金币与「失城/超时」两处分别调它（`:1465`、`:1885`），上限下限就是元素自己的 `_minimum` / `_maximum`。

**做法**：

1. 宿主新增 `TryCreateNumpad(value, minimum, maximum)`：`VirtualKeyboardArtwork.TryCreate` 的四张帧
   （`STONEBKG` 底、`REQBKG` 输入条、`SPANBTN` 0/1 帧的确定键）按 `IsEvilInterfaceEnabled` 选坏界面那套，
   缓存在 `_keyboardArtwork`；缺帧就不调工厂（`TryCreate` 的参数非空，先各自判空），
   然后 `VirtualKeyboardView.TryCreateNumpad(artwork, ScreenBuffer.DefaultSize, ..., minimum < 0, isEvil)`
   ——负下限才给正负号键，与参考 `( minValue < 0 )` 一致。
2. 新增 `EditorMapSpecsValueModal`（`IModalScreen`）包键盘：`Update` 把 `LocalEvent.Current` 交给
   `ProcessEvents`，`HotKey` 里 `Okay` / `Cancel` 都折成 `closeRequested`（参考没有「取消」这条出口）；
   关窗后 `ReadNumpad` 校验——Valid 才置 `Value` 并收尾，Invalid / OutOfRange 各自摞一个错误框
   （`Labels.ErrorTitle` / `Labels.InvalidValue` / `Labels.GetOutOfRangeMessage(min, max)`），键盘留着。
   错误框自己一层栈（`_inner`），与 `EditorMapSpecsModal` 同一个思路：`HotKey` / `PressKey` / `SetCursor` /
   鼠标按键在有内层窗时全归内层，关掉再接着打。
3. 元素与字段的对应下沉到 `EditorMapSpecsWindow.GetValueElement(field)`：宿主与模态都不该知道私有字段名，
   `EditorMapSpecsModal.TryCreateValuePad` 只是「取元素 → 造键盘 → 包模态」。
4. `EditorMapSpecsModal` 的 switch 加 `EditValue` 分支，`Resolve` 加 `EditorMapSpecsValueModal` 分支
   （`_input.ResolveValue(Value)`；没通过校验时窗还开着，走不到这里）。

**与 issue 描述的两处出入**：issue 写「越界或非法照参考弹 Error 消息框且不关窗」——照做了；但 issue 还写「Cancel 写回 null」，
参考的键盘**没有取消**：`Game::HotKeyCloseWindow()` 把 `DEFAULT_CANCEL` 也算关窗，一样走校验。端口按参考来，
ESC 与回车交出同一个数，`Value` 只在校验通过时有值。

**验证**：新增 `EditorMapSpecsValueModalTests` 六条——宿主按范围造键盘（`500` 无符号、`-100..100` 有符号）；
点确定交回 500；回车与 ESC 都交回 500；打成 `1009`（上限 199）后点确定会摞错误框、`Value` 还空着、数字不被清掉，
关掉错误框键盘仍开着；退成空串后弹的是 invalid 那条；规格页点金币数字框发出 `EditValue/VictoryGold`，
`GetValueElement` 取到的元素值就是键盘首显值，`ResolveValue` 写回 `VictoryGoldInThousands`。
全量 **7163 通过**（Core 301 + Assets 441 + App 63 + Game 4030 + UI 2328），0 警告 0 错误，编辑器冒烟 72.5 FPS。

**坑两处**：① 写测试时窗口要传调色板——`_gamePalette` 为空时 `DrawVictory` 直接返回，金币元素根本不会被建出来；
② 金币上限是 `1_000_000 / GoldThousand`（=1000 千），写回会被元素夹到上限，用例别挑 1234 这种超界值。

## ADR-337：换界面风格要重建编辑器的边框与面板（`a7l.7.4.2`）

**参考**：选项页的 `InterfaceType` 那一档在 `editor_options.cpp:281-285` 里 `Settings::Get().setBattleInterfaceType()` 之后调
`Editor::Interface::reset()`（`editor_interface.cpp:1413-1470`，同一函数也供 `rebuildEditor( true )` 用）——重算雷达、
控制面板与地图区的位置，保留地图中心再夹一次边界。**参考不需要重读任何图**：边框、雷达、面板的图本来就每帧现取
（`interface_border.cpp` 按 `isEvilInterfaceEnabled()` 取 `ICN::ADVBORD` / `ICN::ADVBORDE`），换主题下一帧自然就换了。
端口把边框缓存在 `MapFrame`、把按钮与面板底缓存在 `EditorPanel`，所以换主题必须换对象。

**做法**：

1. `MapFrame.Load(assets, isEvilInterface = false)`：坏界面走归档里独立的 `ADVBORDE.ICN`（`EvilAssetName`），
   不是换色算出来的。布局常量与主题无关，两块边框都是 640x480、边宽 16。
2. `EditorButtonSet.TryCreate(assets, isEvilInterface = false)`：参考的 `ICN::EDITBTNS_EVIL` 归档里**没有条目**——
   `game_assets.cpp:3288-3296` 是先把好界面 45 帧全生成完，再整体 `convertToEvilInterface()`。端口照抄这个顺序
   （先建表，再对全部 45 帧跑 `PaletteTable.Get(PaletteId.GoodToEvilInterface)`）——反过来的话，后补那十帧里的石头面色
   41/46 会漏掉。
3. `EditorPanel.TryCreate(area, assets, isEvilInterface = false)`：按钮集跟着走；面板底在坏界面下取
   `EvilInterfaceImages.StonebakEvilFileName`（`STONEBAK_EVIL` 同样是现算的合成表，归档里没有）。
4. `EditorScene` 新增 `_assets` 字段与 `RebuildInterface()`：按当前 `Settings` 重读边框、重建 `MapFrame` 与
   `EditorPanel`，重算 `_viewport` / `_radar` / `_panel`；`_viewportPixels` 只在视口尺寸真的变了才换缓冲
   （主题不影响尺寸，这条是保险）。地图视图、滚动位置、雷达底图都不动——与参考 `reset()` 保留中心一致。
5. `OpenEditorOptions()` 的 `InterfaceType` 分支在 `SwitchToNextInterfaceType()` 之后调 `RebuildInterface()`。

**没做**：`Dynamic` 档只在 `IsEvilInterfaceEnabled` 里按语言解析，重建时读的就是解析后的结果，循环顺序仍是
`Good → Evil → Dynamic`。

**验证**：新增 `EditorSceneInterfaceRebuildTests` 两条——好界面画一帧记下顶边框与面板区的像素指纹，切到坏界面
`RebuildInterface()` 再画：`PanelArea` 与 `View.Scroll` 一丝不动，两处指纹都变了；另一条确认坏按钮集 45 帧齐全、
尺寸不变而像素不同，且坏面板 `HasBackground` 为真（`STONEBAK_EVIL` 真取得到）。全量 **7165 通过**
（Core 301 + Assets 441 + App 65 + Game 4030 + UI 2328），0 警告 0 错误，编辑器冒烟 77.7 FPS。

**坑一处**：`EditorPanel` 在坏界面下用 `GameAssets.GetImage` 取合成石头，这条依赖 `Assets.Current` 已被宿主设好；
用例里必须先设 `GameAssets.Current` 再建面板，否则 `HasBackground` 是假、面板回落成平色。

## ADR-338：编辑器对象动画接进 Mp2 渲染器（`a7l.7.4.3.1`）

**任务拆分**：`a7l.7.4.3` 原本把「通行性叠层」与「对象动画」写在一起。开工发现两者前置条件不同：动画只差渲染侧那一帧，
通行性却要每个地块的方向通行性，而端口的 `Tile.GetTileIndependentPassability()` / `UpdatePassability()` 都挂在
`World` 上、编辑器没有 World。按「发现隐藏依赖就拆」的规矩，`a7l.7.4.3` 升成 Epic，拆出：

- `a7l.7.4.3.1`（本次完成）：对象动画。
- `a7l.7.4.3.2`：通行性叠层，先要定「把当前 Mp2Map 装成只读 World 镜像」（`a7l.7.5` View World 也要）还是另写一条
  只吃 `Mp2TileInfo` + `MapObjectCatalog` 的通行性计算；标记贴图也不能借用 UI 的 `TileRendering`（Game 不能引 UI）。

**参考**：

- 节拍：`editor_interface.cpp:2017-2024` —— 每 `Game::validateAnimationDelay( MAPS_DELAY )` 一次，若
  `conf.isEditorAnimationEnabled()` 就 `Game::updateAdventureMapAnimationIndex()`；同支的 `_redraw |= REDRAW_GAMEAREA`
  端口不需要（场景每帧整幅重画）。
- 画：`maps_tiles_render.cpp:60-80`（`renderObjectPart`）与 `:142-190`（`renderMainObject`）在
  `objectInfo->animationFrames > 0` 时再画一帧 `icnIndex + (animationIndex % frames) + 1`；魔法花园被访问过
  （`objectType == OBJ_MAGIC_GARDEN && metadata()[1] == 0`）取整轮之后那帧 `icnIndex + frames + 1`，且只对主物件判断。
- 颜色轮转（`RenderProcessor::startColorCycling` / `stopColorCycling`）在端口没有对应物，也不需要：合成表把轮转色并进
  了静态槽（`PaletteId.CyclingToStatic`），水面与火焰本就是死图。这条差异写进了代码注释。

**做法**：

1. `AdventureMapView.Draw(target, drawTownsOverFog, animationPhase = 0)`：相位显式传入（参考读全局），存进私有
   `_animationPhase` 供 blitter 用——用例与截图因此不依赖全局计数器。
2. `BlitPart` 拆成 `BlitAnimatedPart`：先画主图，名册里有动画帧再画次级帧；`BlitMainObject` 额外把 `tile.Quantity2`
   （`metadata()[1]`）带进去，判魔法花园。主物件走的仍是 `DrawGroundLayer` / `DrawTopLayer` 里地块自己那一格，
   addon 链照旧走部件路径，与参考一致。跳过的图集（`MINIHERO` / `MONS32` 等）逻辑不变。
3. `GameDelays` 新增 `MapsDelayMilliseconds = 250` 常量并让 `SetDelay` 用它（原先写死 250，与
   `ScrollDelayMilliseconds` 同一模式），`EditorScene.AnimationInterval` 直接引用它。
4. `EditorScene` 新增 `_animationPhase` / `_animationElapsed` 与 `UpdateAnimation(dt)`：只在
   `Settings.IsEditorAnimationEnabled` 时累计，每满一格推进一步，单帧最多四步（与卷屏的
   `MaxEdgeStepsPerUpdate` 同理，防卡顿时跳过整轮）；关掉开关索引停在原处而不是归零，与参考一致。绘制时把相位传进
   `View.Draw`。`AnimationPhase` 属性给用例与日志看。

**验证**：`AdventureMapAnimationTests` 四条（雪地水车 177 六帧：相位 0/1/6 分别落在 178/179/178；喷泉 15 无动画帧只画主图；
魔法花园 `Quantity2 == 0` 走 `190+6+1` 而 `== 1` 走动画；图集缺次级帧时停在主图），`EditorSceneAnimationTests` 一条
（不足一格不推进、关掉停住、再开继续）。全量 **7170 通过**（Core 301 + Assets 441 + App 66 + Game 4034 + UI 2328），
0 警告 0 错误，编辑器冒烟 67.0 FPS（40 帧）。

**坑**：`Mp2TileInfo.GroundLayer` / `GroundIcnType` 是只读派生属性，用例里只能写 `ObjectName1 = icnType << 2` 与
`Quantity1` 的层位；`Quantity2` 才是 `metadata()[1]`（`Metadata` 是右移三位后的打包值，别混）。

## ADR-339：无 World 的 Mp2 地块通行性（`a7l.7.4.3.2.1`）

**问题**：参考的编辑器是 `world.LoadMapMP2` 起来的一个真 World，通行性叠层因此能直接调 `Tile::updatePassability()`。
端口的编辑器只拿着一张 `Mp2Map`，没有 World；而 `Tile.UpdatePassability` / `IsAnyTallObjectOnTile` /
`IsDetachedObject` / `GetIndexOfMainTile` / `GetHero` / `FixMP2MapTileObjectType` 的入参都是 `World`。

**先数清楚 World 到底被用了哪几样**：对 `Tile.cs` 里所有 `world.` 调用做了清点，只有 `Geometry`、`GetTile(index)`、
`GetHero(id)`、`GetMapEvent(index)` 四样（外加 `SetHero` 里的 `ResetPathfinder`，那条不在通行性路径上）。于是：

1. 新增 `FHeroes2.Game.Maps.ITileNeighbourhood`（`MapGeometry? Geometry` / `Tile GetTile(int)` /
   `Hero? GetHero(int)` / `MapEvent? GetMapEvent(int)`），`World` 声明实现它；上列方法连 `UpdateObjectType` /
   `UpdatePriceOfLoyaltyObjectType` / `GetDailyIncomeObjectResources` 一起改成吃接口。`SetHero(World, Hero)` 保持原样
   （要重置寻路器）。全部调用点传的是 `World`，编译与行为不变——2328 条 UI 测试与 4039 条 Game 测试原样通过。
2. 新增 `FHeroes2.Game.Maps.MapTileSet : ITileNeighbourhood`：从 `Mp2Map` 按 `World.LoadMap` 同样的顺序建 `Tile[]`
   （`SetTerrain` → `Init` → addon 链 → `SortObjectParts` → `FixMP2MapTileObjectType`），再跑
   `World.UpdatePassabilities` 一模一样的两遍（主物件类型为空先 `UpdateObjectType`，全体 `SetInitialPassability`，
   再全体 `UpdatePassability`）。`GetHero` / `GetMapEvent` 恒 null：编辑器地块不驻英雄、也没有地面事件。
3. `MapTileSet.Refresh( Mp2Map map, int index )`：重建那一格，然后对 `getAroundIndexes( _index, 1 )` 的八个邻居与它自己
   各跑一遍 `SetInitialPassability` + `UpdatePassability`（`maps_tiles.cpp:1479-1493` 的口径）。地图参数要传：`Mp2Map`
   是 record，编辑器改一格是换出一张新地图。

**为什么不做「把 Mp2Map 装成 World 镜像」那条路**：`World.LoadMap` 会连带英雄、城堡、王国、法术、地面事件与 `PostLoad`
（区域划分与寻路器都跑一遍），每次编辑后重跑不可接受。接口抽取让同一套算法能在「只有地块」的上下文里跑，也不产生第二份实现；
`a7l.7.5` 的 View World 若需要完整 World，走的仍是 `LoadMap`，两条路互不妨碍。

**已知偏差**：① 地块里存着 `MP2::OBJ_ACTION_HERO`（地图文件留下的英雄标记）时，参考会透过英雄看到下面的物件，端口这里
`GetHero` 恒 null，于是读成空地。② 合成地图的物件 `uid` 是 0 时 `DoesObjectExist(0)` 会命中空白地块的默认部件，把整格判成
不可进——真实地图的 uid 从 1 起，这是用例踩到的坑，不是端口 bug（用例里给了 uid 1）。

**验证**：`MapTileSetTests` 五条——空地全通；挡路山只放行中排与下排；压在地图底边整格不可进；`Refresh` 画上山后变化、
擦掉后回到全通；`GetHero` / `GetMapEvent` 恒 null 且尺寸正确。全量 **7175 通过**（301 + 441 + 66 + 4039 + 2328），0 警告 0 错误。

## ADR-340：编辑器通行性叠层（`a7l.7.4.3.2.2`）

**参考**：`Interface::GameArea::Redraw()` 里 `LEVEL_PASSABILITIES` 与战争迷雾互斥（`interface_gamearea.cpp:768-793`：打开叠层时雾根本不画）；叠层本身是 `Maps::redrawPassable()`（`maps_tiles_render.cpp:916-929`）逐格 `BlitOnTile( surface, 0, 0, mp )`，判定为 `isActionObject || passable != Direction::ALL`，编辑器侧用 `isOffGameActionObject`（纯动作位），游戏内用 `isInGameActionObject`。标记帧 `PassableViewSurface()`（`:188-277`）是一像素宽的框：全通全绿、走不通全红、部分可走时把每条边切三段，斜角方向的开合决定外侧三分之一，直走方向决定中间三分之一。

**做法**：

1. `FHeroes2.Game.AdventureMap.PassabilityOverlay`：把 UI `TileRendering` 里的 `GetPassableViewSurface` 与 512 帧缓存整块搬过来（Game 不能引 UI，而编辑器要画），另给 `IsMarked` / `IsActionObject` / `Draw`。像素逻辑逐行照搬，`Surfaces` 仍是 `(mask, isActionObject)` 键的进程内缓存；UI 的私有方法改成转调，`RedrawPassable` 的动作对象判定也改用 `IsActionObject`，两处只剩一份实现。
2. `AdventureMapView.Draw( target, drawTownsOverFog, animationPhase, MapTileSet? passability = null )`：非空时在物件层之后画标记并**跳过迷雾**，与参考的互斥一致；地块集尺寸与地图不符时抛 `ArgumentException`。
3. `EditorScene.PassabilityTileSet()`：`Settings::isEditorPassabilityEnabled` 关着返回 `null`。打开时第一次 `MapTileSet.Create`，之后每帧比对 `Mp2Map` 对象引用——`Mp2Map` 是 record，编辑、撤销、重做都只是换出一张新地图——变了就逐格比一遍 `Mp2TileInfo`，只对真正变过的格子 `Refresh`。比给每条编辑路径挂回调稳妥（撤销、重做、将来的粘贴都自动覆盖），代价是每帧一次二十万字节级的结构比较，实测冒烟 61.8 FPS 无感。
4. 选项对话框的 `Passability` 早就写 `SetEditorPassability`，不需要新开关；参考在切换后置 `REDRAW_GAMEAREA`，端口每帧整幅重画，注释里说明为什么不需要重画位。

**验证**：`PassabilityOverlayTests` 六条（全通绿框且内部透明、全封红框、动作对象换 115 的绿、`TopLeft|Right` 只让左上角与右边中段变绿——三段划分、`IsMarked` 的两种触发、`Draw` 只画框不动内部）、`AdventureMapPassabilityTests` 四条（无叠层画地形、有叠层画山的红框且内部留地形、全通地块没有标记、尺寸不符抛 `ArgumentException`）、`EditorScenePassabilityTests` 一条端到端（开关来回，视口里标记像素在 90 与地形色之间切换）。全量 **7186 通过**（301+441+67+4049+2328），0 警告 0 错误，编辑器冒烟 61.8 FPS。

**坑**：编辑器网格线正好压在标记框的上边与左边（`DrawGrid` 在合并后的帧上再画一遍），用例读像素会读到网格色；`AdventureMapView` 的标记按地块左上角画，所以取像素要取底边（`y = 30`）中段才可靠。

## ADR-341：编辑器选项页的语言子页（`a7l.7.4.4.1`）

**参考**：`editor_options.cpp:283-296`。多于一个语言时开 `fheroes2::selectLanguage( getSupportedLanguages(), getGameLanguage(), true )`，换了语言就 `clearWarningMessage` + `redrawEditor` + `saveConfiguration = true`；只剩英文时把语言钉成英文并弹 `Attention`：「Your version of Heroes of Might and Magic II does not support any other languages than English.」。

**拆分**：`a7l.7.4.4` 体量超一次会话（四个子页各有自己的模态与请求循环），按规则升为 epic，拆成 `.4.4.1` 语言（本次）、`.4.4.2` 图形、`.4.4.3` 音频、`.4.4.4` 热键，彼此无依赖。

**做法**：

1. `FHeroes2.App.Localization.LanguageCatalog`：主菜单原本私有的 `SelectLanguages` / `IndexOfCulture` 搬出来共用——能画出来的语言只能是有 CJK 字形的那些（`fonts.SupportsCjk`），选中的语言按文化名找、找不到退回第一个本地化目录再退回 0。主菜单两处调用点改成转调，删掉私有副本，避免两边走岔。
2. `FHeroes2.App.Modals.GameLanguagePickerModal`：把 `LanguageSelectionDialog` 摞进模态栈（它走 `SetCursor` / `SetLeftButton` 那一套，不读 `LocalEvent`，天然适合当内层窗）；`Okay` 收下、`Cancel` 取消，取消时返回 -1。与 `LanguagePickerModal`（地图规格窗的「附加语言」，选项来自 `SupportedLanguage` 枚举）不是一回事，注释里写清了。
3. `EditorSceneHost.RunLanguagePicker( names, selectedIndex, title )` 只收**显示名**与标题，不认识 `GameText`——选择器因此不依赖装了几份译文就能测；窗口、按钮行（SYSTEM.ICN 的 OKAY+CANCEL）都在这里建，缺素材/缺帧泵时打日志返回 -1。
4. `EditorScene.SwitchInterfaceLanguage()`：`EditorOptionsAction.Language` 接上。多于一个语言开选择器、写 `Settings.SetGameLanguage`、返回 true 让外层落盘；只有一个语言走参考的提示分支。参考的 `clearWarningMessage` 端口没有（编辑器还没有警告条），`redrawEditor` 也不需要——场景每帧整幅重画，字形表按 `GameLanguage.Current` 现查（`FontGlyphTable`），换完语言下一帧就是新译文，**不用重载字体**。

**已知偏差**：编辑器界面文案目前是英文字面量（参考的 `_()` 还没落到编辑器 chrome 上），所以这一页改的是整个游戏的语言设置与已本地化的那部分文案，编辑器自己的字样要等本地化那一轮才跟着变。

**验证**：`EditorLanguageOptionTests` 六条——无帧泵返回 -1、语言表为空时不开窗、取消返回 -1 且高亮的是当前语言、选中第 1 行返回 1、`SwitchInterfaceLanguage` 端到端（按实际装的语言数走对应分支：装了两份时选中项写进设置并返回 true，只有英文时钉成英文且弹一个窗）、没有数据时不崩。全量 **7192 通过**（301+441+73+4049+2328），0 警告 0 错误，编辑器冒烟 62.1 FPS。

## ADR-342：编辑器选项页的图形子页（`a7l.7.4.4.2`）

**参考**：`editor_options.cpp:307-311` 调 `openGraphicsSettingsDialog( rebuildEditor )`；`dialog_graphics_settings.cpp:238-295` 是个 `SelectedWindow` 状态机——五处选项（模式、分辨率、屏幕缩放、垂直同步、系统信息）任选一项后 `Apply` 再回到 `Configuration` 重开同一页，关窗才是 `Exit`；`updateUI` 只在改分辨率后调（缩放只对主菜单重画，`:274-277`）。

**做法**：

1. `App.Modals.GraphicsSettingsModal`：把 `GraphicsSettingsDialog` 包成一轮模态，同 `EditorOptionsModal` 的缝——一帧画一次、拿到叶子就收摊，交给调用方 `Apply` 再重开。右键的五处说明摞在模态自己的栈上（参考在循环里直接阻塞开窗，端口不能在这里递归 `GameHost.RunModal`）。
2. `EditorSceneHost.TryCreateGraphicsSettingsDialog` 装美术：`SPANEL` 必须有；`RESOLUTION_ICON`、`GAME_OPTION_ICON`、`EMPTY_OPTION_ICON_BACKGROUND` 这套数据里都没有（新版资源，同 `BattleSettingsWindow` 的旧数据坑），缺哪张退回同尺寸空图标并打一条 warn，页面的字与状态照样画得出来。窗口控制取自 `LocalEvent.Current.Engine?.Display`，没窗口时页面仍画得出来、只是全屏/分辨率不真的生效。
3. `EditorScene.OpenGraphicsSettings()`：一轮一个模态，`Exit` 跳出，否则 `GraphicsSettingsDialog.Apply(page, option, RebuildInterface, SelectResolution)` 累加脏标记。`updateUI` 就是 `RebuildInterface`，与参考「只在分辨率后 rebuildEditor」一致，模式/缩放/垂直同步/系统信息四项不重画整套界面。

**已知缺口**：`EditorSceneHost.SelectResolution` 是留好的缝，目前为 null，所以 Resolution 一行点了不变——接它需要把主菜单首运流程那套（`ResolutionArtwork` + `ResolutionDialog` + 提示框 + 引擎模式切换）抽成可复用模态，已开 `a7l.7.4.4.5`。

**验证**：`EditorGraphicsOptionTests` 七条——无数据时返回 null 且跑页面得到 `Exit`、有数据时页面按标准尺寸建出来、无帧泵时跳过、点 V-Sync 行叶子是 `VSync`、点 OK 是 `Exit`、`OpenGraphicsSettings` 两轮（V-Sync→OK）真的翻转 `Settings.IsVSyncEnabled` 并返回 true、直接关窗返回 false 且设置不变。另：两个新测试类都挂 `LocalEventCollection`——它们共写 `LocalEvent.Current` / `GameAssets.Current`，并行跑会互相拆台（第一次全量跑就撞出过一条失败）。全量 **7199 通过**（301+441+80+4049+2328），0 警告 0 错误，编辑器冒烟 59.5 FPS。

## ADR-343：编辑器选项页的音频子页（`a7l.7.4.4.3`）

**参考**：`editor_options.cpp:313-316` 调 `Dialog::openAudioSettingsDialog( false )`，返回值并进 `saveConfiguration`，然后 `action = Configuration` 回选项页；`dialog_audio.cpp:149-259` 是整窗阻塞循环，音量按 `( volume + 1 ) % 11` 原地翻、音乐类型与 3D 音效照翻，右键走说明框。

**做法**：

1. `App.Modals.AudioSettingsModal`：把 `AudioSettingsDialog` 包成一轮模态，与 `GraphicsSettingsModal` 同构——一帧画一次，`ProcessEvents` 说关窗就收摊，`IsDirty` 直接透出去当 `saveConfiguration`（这一页的设置都在页内当场写，不需要像图形页那样再 `Apply`）。右键说明同样摞在模态自己的栈上。
2. `EditorSceneHost.TryCreateAudioSettingsDialog`：美术只有 `SPANEL`（四处图标）与共用按钮，字体给说明框标题/正文；`RunAudioSettingsDialog` 跑一轮返回 `IsFinished && IsDirty`，无帧泵时 `false`。
3. `EditorScene.OpenAudioSettings()` 接 `EditorOptionsAction.AudioSettings` → `saveConfiguration |=`。

**已知缺口**：App 侧还没有混音器（只有放音乐的 `AudioPlayer`），所以 `EditorSceneHost.Audio` 默认 null——两处音量点不动、图标恒显示 off，与参考在没有声卡时的表现一致；音乐类型与 3D 音效照常生效。接混音器后由宿主赋值即可，已在属性文档里写明。

**验证**：`EditorAudioOptionTests` 七条——无数据返回 null 且跑页面为 false、有数据按 289x272 建出来、无帧泵跳过、无设备时页面仍开且点音乐类型能翻、直接关窗不动设置、`OpenAudioSettings` 翻音乐类型后返回 true、直接关窗返回 false。全量 **7206 通过**（301+441+87+4049+2328），0 警告 0 错误，编辑器冒烟 60.0 FPS。

## ADR-344：编辑器选项页的热键子页（`a7l.7.4.4.4`）

**参考**：`editor_options.cpp:317-321` 调 `fheroes2::openHotkeysDialog()`，无返回值、不碰 `saveConfiguration`，然后 `action = Configuration` 回选项页；`dialog_hotkeys.cpp:415-482` 是整窗阻塞循环——列表滚动、单击选类别、双击等按键改写、右键弹说明。

**做法**：

1. `App.Modals.HotKeysModal`：把 `HotKeysDialog`（UI 层的 `ListBox<HotKeyRow>`）包成一轮模态，与前两页同构：一帧画一次，`ProcessEvents` 返回 OK 或 Cancel 就收摊，右键说明摞在自己的栈上。这一页 `IsDirty` 不往外传——参考改键是当场写进热键表、不落配置。
2. `EditorSceneHost.TryCreateHotKeysDialog`：美术是 `SCROLL`（滚动条五帧）、`ADVBORD` 第 0 帧（窗底）与共用按钮，与分辨率窗同一套；字体黄/白/黄（标题、行、选中行）。`RunHotKeysDialog` 跑一轮，无帧泵就跳过。
3. `EditorScene.OpenHotKeys()` 接 `EditorOptionsAction.HotKeys`，不带 `saveConfiguration`。

到此选项页四格（语言、图形、音频、热键）全部接上。

**验证**：`EditorHotKeyOptionTests` 六条——无数据返回 null、有数据建出页面且行表非空、点 OK 结束模态、Escape 结束模态、无帧泵时跳过、`OpenHotKeys` 经场景跑通一轮。全量 **7212 通过**（301+441+93+4049+2328），0 警告 0 错误，编辑器冒烟 62.0 FPS。

## ADR-345：编辑器图形页的分辨率选择窗（`a7l.7.4.4.5`）

**参考**：`dialog_graphics_settings.cpp:255-269` 的 Resolution 一项调 `Dialog::SelectResolution()`，返回 `true` 才 `updateUI()`；`dialog_resolution.cpp:196-300` 是阻塞的列表窗（滚动、双击=选中并确认、右键解释），确认时 `engine().setResolution()`。

**做法**：

1. `App.Modals.ResolutionPickerModal`：把 `ResolutionDialog` 包成一轮模态，与音频/热键页同构：一帧画一次，OK 且 `IsSelected` 时先 `IDisplayControl.SetResolution` 再收摊并记 `Accepted`（引擎拒绝只留一条日志，与主菜单首运流程一致），Cancel/Escape 直接收摊。右键解释摞在自己的栈上。
2. `EditorSceneHost.TryCreateResolutionDialog`：美术与主菜单同一套（`SCROLL` 五帧 + `ADVBORD[0]` + 共用按钮），行只留 `GameSize == 640x480` 的那些（其余 `SetResolution` 会拒绝，ADR-205），没有显示设备或一条可用行都没有时返回 `null` 并 warn。
3. `EditorSceneHost.RunResolutionPicker` 跑一轮返回 `IsFinished && Accepted`；`SelectResolution` 在构造函数里默认接到它，仍是可置 `null` 的缝（置空就退回「点了不变」）。`OpenGraphicsSettings` 里 `Apply` 收到 `true` 后照旧 `RebuildInterface`（`updateUI`）。

**验证**：`EditorResolutionOptionTests` 八条——无数据/无显示设备时返回 null 且跑页面为 false、列表只留帧缓冲尺寸的行（三条可用行滤掉 800x600 那条）、`SelectResolution` 默认非空、OK 换窗成功、Escape 不换、`Apply( Resolution )` 换窗并触发 `updateUI`、`OpenGraphicsSettings` 经场景两轮（分辨率→OK）换窗并返回 true。全量 **7220 通过**（301+441+101+4049+2328），0 警告 0 错误，编辑器冒烟 63.5 FPS。

**收尾**：`a7l.7.4.4`（选项页四个子页）四个子页与分辨率缝全部接上，epic 随之关闭。

## ADR-346：城堡详情窗的 App 模态骨架（`bpn.2.1`）

**参考**：`editor_castle_details_window.cpp:790-1100` 是个阻塞函数，循环里再开`Dialog::inputString`（改名，`:882-890`）、`showStandardTextMessage`（右键说明与`Dialog::OK | CANCEL`的未保存确认，`:855-867`）、`mageGuildSpellsDialog`（禁法，`:873`）；军队条不是窗口，光标落进条里就`armyBar.QueueEventProcessing( &message )`（`:992-1003`）。OKAY/EXIT 各有对应热键（`DEFAULT_OKAY`/`DEFAULT_CANCEL`），EXIT 与热键都先比对改动再弹确认。

**做法**：

1. `CastleDetailsInput` 加`Accept()`与`RequestDiscard()`：热键没有鼠标坐标可判，把`:851`/`:855` 两支单拎成显式入口，Process 里的同一处也改调它们，语义与点按钮完全一致（EXIT 那支依旧走`TryDiscard`，未保存确认从热键来也一样弹）。
2. `App.Modals.EditorCastleDetailsModal`：与`EditorMapSpecsModal`同构——每帧`UpdateButtonPressStates` 再`Process`，内层窗摞在自己的`Stack<IModalScreen>`上；`HotKey` 在栈空时映射 Okay→`Accept`、Cancel→`RequestDiscard`（后者可能只留下确认请求，这里立刻把它开出来）；EditName →`Dialog::inputString`（标题空、正文`Enter Castle name:`、上限`nameCharLimit = 30`，确定且非空才写`CustomName`，`:882-890`）；ShowMessage → `TryCreateMessageBox`，未保存确认用`OK | CANCEL` 且 OK 表示放弃（`:862-866`）；Army → 把这一帧喂给`CastleDetailsView.ArmyBar`，它自己管选中与拖放，要宿主回答的事件留在`ArmyEvent` 上。
3. `CastleDetailsView` 暴露出`ArmyBar`、`OkayArea`、`ExitArea`（军队条要吃事件、测试要点按钮）。
4. `EditorSceneHost.TryCreateCastleDetails(state, gamePalette)` + `RunCastleDetails` 返回`Outcome`（`Accepted` = 调用方把状态写回地图）；`TryCreateMessageBox` 加可选的按钮参数，旧的 OK 单键调用不受影响。

**已知缺口**（各自成子项）：Set Spells 的魔法行会窗还没接（`bpn.2.2`），军队条的拆兵/兵种说明/加兵三个回答留在`ArmyEvent` 上（`bpn.2.3`）。

**拆分**：`bpn.2` 升级为 epic，子项`.2.1`（本条）、`.2.2`、`.2.3`，后两者依赖`.2.1`；`bpn.3` 仍挂在 epic 上。

**验证**：`EditorCastleDetailsModalTests` 八条——无数据返回 null 且跑窗为 `None`、点 OKAY / 热键 Okay 都是`Accepted`、干净窗口 Escape 不问直接`Discarded`、有改动 Escape 先弹确认且 OK 放弃、确认里 Cancel 回到窗口再 Okay 是`Accepted`、改名写进`CustomName`、右键名字弹说明。全量 **7228 通过**（301+441+109+4049+2328），0 警告 0 错误，编辑器冒烟 58.2 FPS。

## ADR-347：编辑器放大镜的 View World（`a7l.7.5`）

**参考**：`editor_interface.cpp:2183-2186` 的 `eventViewWorld()` 只有一句 `ViewWorld::ViewWorldWindow( PlayerColor::NONE, ViewWorldMode::ViewAll, *this )`——编辑器不裁雾，看的是整张图。窗口的循环（`view_world.cpp:700-795`）里滚轮换档（713-716）、按住拖拽平移（734-745）、右键只对退出按钮弹说明（753-758）、退出按钮与 ESC 关窗并把当前档位写回 conf（771-772）。

**做法**：

1. `World.CreatePreview(Mp2Map)`：编辑器手上只有地图对象，而窗口、雷达与图标层都按 `World` 写。工厂现装一个一次性世界（`LoadMap` 只读地图），不碰 `World.Current`，尺寸对不上就照 `LoadMap` 的规矩抛 `ArgumentException`。
2. `EditorSceneHost.RunViewWorld(view, viewport, frame, zoomLevel)`：`ViewWorldMapBuilder` 按 `ViewAll` 建整图缓存，`ViewWorldRenderer.DrawObjectsIcons` 补图标层，`ViewWorldWindow` 与冒险地图同一套参数（`PlayerColor.None`、编辑器视口、整屏尺寸、`Settings.IsEvilInterfaceEnabled`），返回窗口离场时的档位。没有帧泵或数据不全时只 warn 返回 `null`，不打断编辑器。
3. `App.Modals.ViewWorldModal`：把 `ViewWorldOverlay` 包成模态，编辑器因此不必像 `AdventureMapScene` 那样在 `GameHost` 里特判三处输入。`SetLeftButton` 不带坐标，落点按最近一次光标算；滚轮不在 `IModalScreen` 的输入面上，`Update` 每帧读 `LocalEvent`（与参考在循环里读同一份状态）；ESC 与退出按钮同一条收摊路径。
4. `EditorScene`：`ClickSystemButton(Magnify)` 走 `OpenViewWorld()`；档位记在场景字段（与 `AdventureMapScene` 同口径，不进 `Settings`），下一回从这一档开。世界与缓存随模态一起丢，地图一格都不被回写。
5. 测试：`WorldPreviewTests` 四条（建世界且不换 `Current`、地图只被读、雾层可读、尺寸不符与 null 拒绝）；`EditorViewWorldTests` 两条（点放大镜开出整图窗口且帧被整屏覆盖、退出后回到原帧、档位记忆到下一回）。App 测试新增 `AssemblyInfo.cs` 按类串行：这批用例共用 `GameAssets.Current` 与 `Settings.Get()`，并行时 View World 的合成表按钮会撞上别的类关掉的归档。

**验证**：全量 **7234 通过**（301+441+111+4053+2328），0 警告 0 错误。

**收尾**：`a7l.7`（面板系统按钮接线）全部子项完成，epic 随之关闭。

## ADR-348：城堡详情窗的 SET SPELLS（`bpn.2.2`）

**参考**：`editor_castle_details_window.cpp:383-605` 的 `mageGuildSpellsDialog()`。五行卷轴每格点开 `Dialog::selectSpell()`（`dialog_selectitems.cpp:1117-1141`），BANNED SPELLS 点开 `Editor::openSpellSelectionWindow()`（`editor_spell_selection.cpp:172-208`，`pickDisabledSpells = true`），换等级时改 `currentBannedSpellsLevel` 再重开一层（`:515-521`）；热键先 Cancel 后 Okay（`:443-448`），两个按钮的悬停提示写在状态栏。

**做法**：

1. `MageGuildSpellsInput` 补三处：`Accept()` / `Cancel()` 作为按钮与默认热键的共同入口（原来只在 `Process` 里内联），`FocusBannedSpellsLevel()` 记下 `currentBannedSpellsLevel`——参考在换等级时改它，端口由宿主在重开前调用。
2. `EditorSceneHost.RunSpellSelection` 拆成两半：`TryCreateSpellSelection(...)` 只造窗（原来的字体、美术检查搬进去），`RunSpellSelection` 保留「跑一层、换等级再来一层」的循环。已在模态里的调用方（魔法行会子窗）不能走 `RunModal`——那会在城堡详情窗开着时把帧泵套成两层。
3. `EditorSceneHost.TryCreateSpellSelect(...)` 造 `Dialog::selectSpell()`：列表内容 `SpellSelectDialog.BuildSpellList(includeRandomSpells: true, …)`、图标 `ICN::SPELLS`（首次读入后缓存在 `_spellIcons`）、列表美术与标题/正文两套字体，最后包成 `App.Modals.SpellSelectModal`。
4. `EditorSceneHost.TryCreateMageGuildSpells(state, dialogArea)` 造子窗：`MageGuildSpellTable` 按种族建表并从元数据 `Load`，`MageGuildSpellsView` 摆在城堡窗的 `dialogRoi` 上，`MageGuildSpellsInput` 拿同一份元数据。
5. `App.Modals.MageGuildSpellsModal` 自带内层栈：`SelectSpell` → `SpellSelectModal`、`SelectBannedSpells` → `EditorSpellSelectionModal`、`ShowMessage` / `ShowSpell` → 消息框与法术说明。内层关掉时按类型回填（`ApplySelectedSpell` / `ApplyBannedSpells`），选法术列表为空时照参考弹 `Warning` / `There are no spells to select.`（`includeRandomSpells` 为真时其实进不去这条路，留着对齐结构）。禁法窗回 `LevelChanged` 时不写回这一层，记下新等级后在栈上再压一层，旧层结果丢掉。
6. `EditorCastleDetailsModal` 的 `SetSpells` 分支改走 `TryCreateMageGuildSpells`；关窗时没有要回填的（元数据在 OKAY 时已写回）。`CastleDetailsView` 补 `DialogArea`（`dialogRoi`）与 `SpellsArea`（SET SPELLS 按钮区域）。
7. 测试：`MageGuildSpellsTests` 加三条（默认 Okay / 默认 Cancel 两个入口、`FocusBannedSpellsLevel` 之后按钮在那一级开）；新增 `EditorCastleDetailsSetSpellsTests` 四条（点 SET SPELLS 开五行子窗、点格子把选择器压到子窗栈上、点 BANNED SPELLS 开禁法窗且等级为 1、子窗 OKAY 写回元数据）。

**验证**：全量 **7241 通过**（301+441+115+4053+2331），0 警告 0 错误。

## ADR-349：城堡详情窗的兵种说明（`bpn.2.3.1`）

**参考**：`dialog_armyinfo.cpp:518-712` 的 `Dialog::ArmyInfo()`。窗口自己转循环：每帧 `Redraw`，`ProcessEvents` 里处理退出 / 升级 / 解散三个按钮与三个热键（`:599-660`），升级与解散先弹 YES/NO 询问框（`:604-640`），升级买不起时弹带资源图标的提示（`:628-641`），右键点在各处弹 `getTooltip` 的说明（`:660-700`）。没有按钮的形态（`armyInfo(troop, 0)`）在右键松开时关窗（`:556-570`）。关窗后调用方只拿一个 `DialogButton` 去喂 `ArmyBar::ApplyInfoAnswer()`。

**做法**：

1. `EmptyButtons` 补齐小型文字按钮的三张表：`BUTTON_SMALL_DISMISS_*`、`BUTTON_SMALL_EXIT_*`、`BUTTON_SMALL_UPGRADE_*` 共用 `BuildSmallTextButton(isGoodInterface, releasedFrame)`，帧下标分别是 `VIEWARMY`/`VIEWARME` 的 1/2、3/4、5/6（`game_assets.cpp:1194-1205`），裁剪与把四角置透明的步骤与原来一致。`Assets.GetFileName` 把这六个 `IcnType` 接到 `EmptyButtons` 的名字常量上，合成表由 `GetSynthetic` 的既有分支建出来。
2. `App.Modals.ArmyInfoModal` 包住 `ArmyInfoDialog`：`Draw` 画窗口与三个按钮，`Update` 把 `LocalEvent.Current` 与热键交给 `ProcessEvents`；按钮事件按 `ArmyInfoAction` 分派——`Exit`/`Cancel` 直接收摊，`Upgrade`/`Dismiss` 先压一层 YES/NO 询问框，`UpgradeDisabled` 与右键提示压一层 OK 框。询问框只有 YES 才真的关窗并回填答案，NO 只是把框弹掉。
3. 内层消息框走 `_inner` 栈，与 `MageGuildSpellsModal` 同一套写法：有内层窗时鼠标、热键、按键全归它，画也画在窗口之上。窗口没有按钮时按 `LocalEvent.Current.IsMouseRightButtonPressed` 松开判定关窗。
4. `EditorSceneHost.TryCreateArmyInfo(dialog)` 造窗：`VIEWARMY`/`VIEWARME` 八帧读成 `ArmyInfoArtwork`，退出 / 升级 / 解散按钮各读两帧（升级、解散只在 `dialog.CanUpgrade` / `CanDismiss` 为真时读），`ArmyInfoLayout.Create(整屏) ` 排位置，最后包成 `ArmyInfoModal`。资材缺失时只 warn 回 `null`。
5. `EditorCastleDetailsModal` 的 `Army` 分支接 `ArmyBarEvent.Info`：把当前请求记成 `_pending`，造窗压栈；内层窗关掉时 `ArmyBar.ApplyInfoAnswer(dialog.Troop, modal.Answer)`（静态方法，升级走 `troop.Upgrade()`、解散走 `troop.Reset()`）。造不出来只 warn，军队条照旧留在窗口上。
6. 已知缺口：单位精灵还没画（怪兽动画未转，`Draw` 传 `null`）；升级买不起的提示没有资源图标与花费，只有文字。

**测试**：新增 `EditorCastleDetailsArmyInfoTests` 三条——双击军队条把兵种说明压到城堡窗栈上、解散先问再清空槽位、退出按钮不动兵力；`EmptyButtonsTests` 加一条核对三张表分别取 1/2、3/4、5/6 帧。

**验证**：全量 **7245 通过**（301+441+118+4054+2331），0 警告 0 错误。
