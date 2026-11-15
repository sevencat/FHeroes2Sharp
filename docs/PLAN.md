# 移植计划（PLAN）

> 参考实现：**fheroes2 1.1.18（commit `6c51acfdc`）**
> 目标：C# / .NET 10 / SDL3 / SDL3_ttf，逻辑分辨率 640×480。

## 总体原则

1. **外部资源格式逐字节一致**：AGG / ICN / TIL / MP2 / H2D / SMK / `.mo` / `KB.PAL` 的解析必须与 C++ 完全一致，
   包括名称哈希、条目顺序、裁剪/透明规则、字节序。
2. **内部工程现代重构**：`readonly record struct` 几何类型、`sealed` 服务类、`Nullable=enable`、
   分层命名空间、显式依赖注入（无 DI 容器）、可单元测试。
3. **参考逻辑函数级 1:1 移植**（ADR-057，细化 ADR-001）：新代码与参考**同名函数、同顺序、同常量**
   （`updateTerrainTransitionOnTile`、`makeInstrumentPanelBackground` 这类名字直接沿用），允许结构差异
   （分层、不可变类型、没有指针）。**不做行级 1:1**：行级翻译会继承宏、全局单例与 `SDL_Surface` 的
   原地改写语义。全局状态用 `static X Current` 门面照抄（对应 `X::Get()`），第二版改 DI 只动门面赋值处。
   推理顺序、常量、if/else 分支都要能逐条指回参考文件名与函数名（注释里写清）。
   与既有实现冲突时怎么改、哪些偏离允许，见 `ARCHITECTURE.md` 的「移植粒度与冲突处理」一节。
4. **不提交任何原版资源**：GPLv2 项目，仓库只含代码与可再分发字体。
5. **每个里程碑都能运行、能验证**：可执行 + 测试 + 截图/日志证据。

## 里程碑

| 里程碑 | 内容 | 验收标准 |
| --- | --- | --- |
| **M0** | 解决方案骨架；Core/Assets/Rendering 最小子集；SDL3 窗口 640×480 + 2x；SDL3_ttf 文本；AssetProbe；文档 | 构建 0 警告（warnings-as-errors）；测试全绿；`--smoke-frames` 可运行并出图 |
| **M1** | 图像栈：ICN/TIL 解码、变换层（palette transform 6–13）、`Cursor`、PNG/BMP 导出、黄金图像测试 | 解码 `HEROES.ICN`/`GROUND32.TIL` 与 C++ 输出逐像素一致（抽样 20 个资源） |
| **M2** | 主菜单：`HEROES.ICN` 背景 + `BTNSHNGL.ICN` 招牌按钮、原版位图字体（`FONT.ICN`/`SMALFONT.ICN`）、TrueType 补位 CJK（`Sevencat.GlyphRaster`）、`.mo` 本地化、BGM 播放 | 可从数据目录启动到主菜单，中英文切换 |
| **M3** | 新游戏/场景设置：地图尺寸、玩家数、种族、难度；H2D 场景列表 | 能创建新对局并进入冒险地图（占位渲染） |
| **M4** | 冒险地图渲染：地形、物体、英雄/城镇精灵、滚动、小地图、路径 | 与参考实现同视角截图对齐（tile 级一致） |
| **M5** | 回合与英雄：移动力、拾取资源、战斗触发、AI 基础 | 完整回合可循环，AI 可行动 |
| **M6** | 城镇：建筑树、招募、市场、城镇界面控件 | 城镇全流程可用 |
| **M7** | 战斗：战场渲染、ATB 回合、法术、怪物动画（82M） | 一场战斗可完整进行到结算 |
| **M8** | 存档/设置：版本化存档 schema、自动存档、键位与音量 | 存/读档往返一致，设置持久化 |
| **M9** | 战役、音频完善、视频（SMK）；评估 NativeAOT 发布可行性（ADR-011）。地图编辑器另立 E 系列，见下节 | 战役可通，AOT 结论有据 |
| **E1** | 地图编辑器（只读可视化）：主菜单右下角 EDITOR 按钮或 `--scene editor [--map x.mp2]` 打开 MP2，复用冒险地图渲染（地形 + 对象 + 英雄），叠加 tile 网格、光标 tile 高亮、工具条、状态栏（坐标 / 地形 / 对象）、小地图，键鼠滚动 | 能打开地图并出图；网格与高亮随光标和滚动变化；像素基线 `editor.png` 逐字节一致 |
| **E2** | 编辑器可改（内存内）：地形笔刷（✅）、地形接缝（✅，ADR-046）、撤销/重做（✅）、新建地图（✅ `--editor-new`）、对象放置/删除（✅，9 个预设，ADR-045）、小地图导航（✅，ADR-047）、`ADVBORD` 外壳与 448×448 视口（✅，ADR-048）、`EditorPanel` 按钮阵列（✅ 工具按钮 / 地面选择器 / 对象列表 / 状态与快捷键，ADR-049）、对象属性编辑（✅ 数据字，ADR-051） | 改完重开仍一致，改动可撤销 |
| **E3** | 保存 MP2：**已完成**为「只改 tile 区」的原地补丁（ADR-043）+ 新建地图的整份写出（ADR-044）；参考的 `saveMap` 写的是它自己的新格式，没有原版产物可对拍 | 写出的地图能被本客户端读回，且与原文件字节可判定地一致 |

### 编辑器（E 系列，与 M 系列并行）

* **参考对象**：fheroes2 1.1.18 **有完整编辑器**（`src/fheroes2/editor/`），入口是主菜单 EDITOR 按钮
  （`BTNSHNGL` 第 20 帧，仅 PoL 数据有）→ `Editor::menuMain()` → `Editor::Interface`；地图绘制复用同一个
  `GameArea`，保存走 `Maps::Map_Format::saveMap`。ADR-039 关于「参考没有编辑器」的说法已更正。
* **E1（已完成）只读可视化**：渲染复用 `AdventureMapView`（与参考一致），编辑器自己的东西（网格、光标
  高亮、工具条、状态栏、小地图）先以覆盖式画在整屏上；参考的 `ADVBORD` 外壳 + 448×448 视口 + `EditorPanel`
  留到 E2。理由与边界见 ADR-040。
* **入口**：主菜单右下角按钮（位置照参考；数据没有 PoL 的 20/21/22 帧时自绘一块招牌），或 `--scene editor`。
  参考点按钮先进编辑器主菜单（新建 / 载入 / 从零 / 随机），E1 没有这些，直接打开目录里第一张地图。
* **E2（已完成）能改地貌、放对象、改对象的数据字了**：`MapEditSession` 的笔刷 + 笔划 + 双栈撤销（ADR-041），
  左键刷、右键拖、右键单击吸管、`1..9` 选地面、`Ctrl+Z/Y` 撤销重做；`--editor-paint` 让截图也能验证
  「改 tile → 画面变」。`O` 循环 9 个对象预设、`X` 擦除、`T` 回地形，放置会取新 UID 并接 add-on 链
  （ADR-045）；`--editor-object` / `--editor-place` 是它的脚本版。地形接缝也接上了（`TerrainTransitions`，
  ADR-046）：刷一格会重算它和八邻的过渡。外壳与面板也照参考落位了：`ADVBORD` + 448×448 视口（ADR-048），
  面板里的 `EDITBTNS` 工具按钮、`EDITPANL` 地面选择器、四个笔刷尺寸按钮与对象列表都能点（ADR-049、ADR-050）。
  对象属性先做到「数据字」这一层：`I` 开信息框看对象与 `data N`，`[` / `]` 改那段 13 位字（ADR-051）；
  框里再补一句这个字**真正**代表什么（怪物的数量、资源堆与矿的种类、开局随机的内容，ADR-052）。
  资源堆的数量与宝箱的金币在参考自己的格式里是扩展元数据，MP2 没有这一层；框头与兵种名已按
  `MP2::StringObject` 与怪物名表给出（ADR-053），守卫、归属、事件与「日产 2 宝石」这类句子要先搬参考的
  对象目录（`icn + 帧号 → 对象身份`）。
* **E2 有参考可对拍，E3 只能自证**：改 tile 按 `Editor::Interface` 移植；写 MP2 参考已经没有原版写出
  （只写它自己的压缩格式），所以保存走"只改 tile 区的原地补丁 + 字节差"自证（ADR-043）。`AssetProbe map` 与
  `Mp2MapFileBuilder` 是读侧的第一道闸门。撤销栈与保存点脏标记是参考没有的东西，属于本项目的补充设计。

## 参考文件覆盖率（逐文件盘点）

口径同原则 3：**已转** = 有对应实现、注释里能指回参考文件名与函数名；**部分** = 主体可用，括号里写明缺口；
**未转** = 没有对应实现。范围是 `src/engine`（26 个）与 `src/fheroes2`（223 个）的 **249** 个 `.cpp`；
`src/tools` 的 8 个命令行工具不逐个对照（本项目用自研 `AssetProbe` 替代）。
快照于 2026-11-15（M4 完成、E3b 之后），**每个里程碑收尾时重算一次**。

| 目录 | 文件数 | 已转 | 部分 | 未转 |
| --- | --- | --- | --- | --- |
| `engine` | 26 | 7 | 7 | 12 |
| `agg` | 6 | 1 | 2 | 3 |
| `ai` | 9 | 0 | 0 | 9 |
| `army` | 4 | 0 | 2 | 2 |
| `audio` | 1 | 0 | 0 | 1 |
| `battle` | 18 | 0 | 0 | 18 |
| `campaign` | 3 | 0 | 0 | 3 |
| `castle` | 11 | 0 | 4 | 7 |
| `dialog` | 29 | 2 | 3 | 24 |
| `editor` | 18 | 1 | 4 | 13 |
| `game` | 28 | 2 | 4 | 22 |
| `gui` | 33 | 8 | 10 | 15 |
| `h2d` | 1 | 0 | 0 | 1 |
| `heroes` | 12 | 1 | 4 | 7 |
| `kingdom` | 14 | 0 | 4 | 10 |
| `maps` | 17 | 3 | 7 | 7 |
| `monster` | 3 | 0 | 2 | 1 |
| `resource` | 4 | 1 | 2 | 1 |
| `spell` | 4 | 1 | 1 | 2 |
| `system` | 3 | 0 | 2 | 1 |
| `world` | 5 | 0 | 4 | 1 |
| **合计** | **249** | **27** | **61** | **161** |

2026-11-15 晚：`map_object_info.cpp` 从未转转为部分（类型骨架 + 先三组、后全部 21 组表），
所以合计比 `PROGRESS.md` 里那张快照多一个「部分」、少一个「未转」。它仍是「部分」：拾取与信息框那几支没转。

2026-11-15 夜：`monster.h` / `artifact.h` 的 id 枚举生成到 `MonsterType`（73 项）与 `ArtifactType`
（105 项），`resource` 的 `artifact` 从「未转」转为「部分」（只有 id 枚举，类行为未转）。

2026-11-15 深夜：`heroes/direction.h`+`direction.cpp` 全部转完（`Game/Maps/Direction`+`Directions`，
同时把 `AdventureMap.FogDirection` 并进来，ADR-062），`maps.cpp` 的九个几何函数转完
（`Game/Maps/MapGeometry`），`heroes` 行从 0/0/12 变成 1/0/11、合计 26/42/181。
`maps.cpp` 本来就记在「部分」里，行数不变，只在部分清单里补明几何已齐。

2026-11-16 凌晨：`maps_tiles.cpp` 的 `Tile` 第一片（`ObjectPart` + 对象层 + 地形位 + 道路位 +
通行掩码 + `Init`）落地（ADR-063）。它本来就在「部分」里，行数与合计都不变，只更新部分清单；
剩下的 `Tile` 成员是 `world` 的镜像。

2026-11-16 凌晨：`Tile` 第二片（`getTileIndependentPassability`/`setInitialPassability`、`isStream`/`isShadow`
与它们依赖的对象分类表，集中在 `Game/Maps/TileObjectTraits`，ADR-064）落地。同样记在「部分」里，
行数与合计不变；现在只剩 `updatePassability` 与英雄占用等真正要 `world` 的部分。

2026-11-16 凌晨：`world.cpp` 的骨架（`World` 的 `w()`/`h()`/瓦片表/`getTile`/`generateUninitializedMap`/
`Reset()`/`ResetPathfinder()` 占位，ADR-065）落地，`world` 行从 0/0/5 变成 0/1/4，合计 26/43/180
（总数仍是 249）。`world.h` 的其余容器与日期、事件、区域、静态分析随各自系统；`world_loadmap.cpp`
与 `world_pathfinding.cpp` 仍未动。

2026-11-16 凌晨：`Tile` 第三片（`updatePassability`、`updateObjectType`、`isAnyTallObjectOnTile`、
`isDetachedObject`、`getIndexOfMainTile`）与 `World::updatePassabilities` 落地（ADR-066），并把
`Mp2ObjectTypes.BaseActionType` 修正成参考的 `getBaseActionObjectType`（补 `isObjectCanBeAction`）。
两个文件本来都在「部分」里，行数与合计不变；`maps_tiles.cpp` 现在只差英雄占用、船的、迷雾位与
`isCoast`，`world.cpp` 只差英雄/城堡/王国/事件等容器与地图装载。

2026-11-16 凌晨：`world_loadmap.cpp` 的瓦片循环 + add-on 链 + `fixMP2MapTileObjectType` + `PostLoad`
可运行子集落地，`world_object_uid.cpp` 的计数器改挂 `World`（ADR-067）。这两个文件从「未转」转为「部分」，
`world` 行 0/1/4 → 0/3/2，合计 26/43/180 → **26/45/178**（总数仍是 249）。同时修掉对象表三个手写文件把
C++ 的 `MP2::OBJ_ARTIFACT`/`OBJ_MONSTER`/`OBJ_CASTLE`（带 action 位）照名字直译成非 action 值的 bug——
它让真实地图装载时 `GetObjectTypeByIcn` 吐出 `0xFFB0` 这种环绕值。

2026-11-16：`Tile.setHero`/`getHero` 与最小 `Hero` 契约（ADR-068）：瓦片只存英雄 id，放置时把当前
对象类型停放到英雄身上，`getMainObjectType` 补上「看穿英雄」的形态，`updatePassability` 与
`fixMP2MapTileObjectType` 改读它。覆盖率**不变**（`heroes` 行仍 1/0/11）：这一片只有 `heroes.h` 的 id 表
加一个 `AllHeroes::Get` 式查找，`heroes.cpp` 的主体没动，记成「部分」会把口径弄虚。

2026-11-16：地图对象查询（`GetObjectPositions`/`getObjectParts`/`doesObjectExistOnMap`）与 `PostLoad` 的
石阵/漩涡/法师之眼三张缓存（ADR-069）。覆盖率**不变**：`maps.cpp` 与 `world.cpp` 本来就是「部分」，
这一片是把它们补厚，不新增「转」。

2026-11-16：`Rand` 门面（`src/FHeroes2.Core/Random/Rand.cs`）与 `World.NextTeleport`/`GetTeleportEndPoints`/
`GetWhirlpoolEndPoints`/`NextWhirlpool`（ADR-070）。`rand.cpp` 的 ✅ 至此名副其实（只差
`combineSeedWithValueHash`）；覆盖率不变，`world.cpp` 仍是「部分」。

2026-11-16：英雄表 `AllHeroes`（`Init`/`Clear`/`GetHeroForHire`/`isAvailableForHire`）、`HeroRaces` 与
`Hero` 的种族/颜色/肖像/mode 位（ADR-071）。`World` 的 `AddHero` 替身删除，`_heroes` 换成按 id 索引的
`AllHeroes`，`LoadMap` 调 `Init( map.Info.Version )`。`heroes` 行 1/0/11 → **1/1/10**，
合计 26/45/178 → **26/46/177**（`heroes.cpp` 进「部分」）。

2026-11-16：地图装载建英雄（ADR-072）：`Mp2HeroInfo` 拆 76 字节块、`World.CreateMapHeroes`/`PlaceMapHero`
接 `world_loadmap.cpp` 的 `MP2::OBJ_HERO` 分支与落格，`Hero.LoadFromMP2` 搬「只依赖英雄自己」的那半
（位置/颜色/种族改判/肖像/名字/巡逻），`HeroModes` 加 `Patrol`。覆盖率**不变**：`heroes.cpp` 与
`world_loadmap.cpp` 早就记在「部分」里，这一片是补薄缺口；`heroes.cpp` 现在只差军队/神器/技能/经验/
法术点与移动。

2026-11-16：牢房里的英雄（ADR-073）：`MP2::OBJ_JAIL` 分支，种族取第 60 字节、颜色 `None`、
`isInJail = true`，瓦片仍归监狱。`CreateMapHeroes` 拆成 `FindInfoBlockTile`/`CreateMapHero`/
`CreateJailHero`/`SelectMapHero`。覆盖率**不变**（都是「部分」）；`world_loadmap.cpp` 现在只差
告示/瓶子/事件/狮身人面/每日事件/传闻六支与城堡表、俘获对象、AI 区域。

2026-11-16：告示/瓶子与地面事件（ADR-074）：新增 `MapObject`/`MapSign`/`MapEvent`/`MapObjects`，
`World.ReadInfoBlocks`（原 `CreateMapHeroes`）按对象类型分派四支，`World` 加 `MapObjects`/`GetMapSign`/
`GetMapEvent`，`Tile.UpdateObjectType` 补上「事件数据不在就清成 `None`」的分支。覆盖率**不变**
（`maps_objects.cpp` 早就在「部分」里，这一片给它挂上装载）；`world_loadmap.cpp` 现在只差
狮身人面/每日事件/传闻与城堡表、俘获对象、AI 区域。

2026-11-16：狮身人面、每日事件与传闻（ADR-075）：`MapSphinx`（答案小写、只比前 4 个字符、
`validate()` → `IsValid`）、`EventDate`（可负资源、首日/周期、六色、`IsAllowed`/`IsDeprecated`）、
`World.Rumors`/`DailyEvents`/`AddEventDate`/`GetMapSphinx`；`ReadInfoBlocks` 加「空块跳过」与
「无瓦片块分流」（`ReadWorldBlock`，`[42]==1` 才是事件），`FindInfoBlockTile` 的匹配集合扩到城堡，
避免城堡块被当成传闻。覆盖率**不变**；`world_loadmap.cpp` 现在只差城堡表、俘获对象、AI 区域。

2026-11-16：日期系统（ADR-076）：`World` 的 `_day`/`_week`/`_month`、五个派生显示值、五个裸取模判定、
`DateString()` 与 `NewDay()`（三层嵌套递增 + 按 `_day - 1` 剪掉过期每日事件）；`NewMonth`/`NewWeek` 与
三张表的 `NewDay`/`NewWeek` 按参考顺序留插入点。同时补 `mp2.cpp` 的 `isWeekLife`/`isBattleLife` 两张
分类表与 `World::CountObeliskOnMaps()`（ADR-077）。覆盖率**不变**（`world.cpp` 与 `mp2.cpp` 早就在
「部分」/✅ 里）；`world.cpp` 的日期侧只剩 `GetWeekType`/`NewWeek`/`NewMonth`。

2026-11-16：俘获对象（ADR-078）、瓦片级迷雾（ADR-079）、`maps_tiles_helper.cpp` 的元数据读写
（ADR-080）：`map_captureobj` 的 `(对象类型, 颜色)` 表与 `CountCapturedMines` 的两条特殊路径、`Tile`
的 `_fogColors`/`_fogDirection` 与 `IsFog`/`ClearFog`、`GetFundsFromTile`/`GetArtifactFromTile`/
`DoesTileContainValuableItems`/`SetResourceOnTile`/`SetBarrierColorOnTile`/`ResetObjectMetadata` 与
`ArtifactCaptureCondition`。覆盖率**不变**；`world_loadmap.cpp` 现在只差城堡表与 AI 区域，
`maps_tiles_helper.cpp` 只差 `ClearFog` 半径揭示、`updateFogDirectionsInArea` 与军队/法术相关的几支。

2026-11-16：英雄技能与经验（ADR-087~090）：`SecondarySkill` 14 项、`secondarySkillValuesPerLevel`
（重排成 id 序）、`factionProperties` 六行 + `Factions.Get`、`Skill::SecSkills`（8 槽含空槽）、
`HeroExperience` 的 40 行阈值表与尾部公式。`heroes` 行 1/1/10 → **1/2/9**（`skill.cpp` 进「部分」），
`game` 行 2/3/23 → **2/4/22**（`game_static.cpp` 进「部分」），合计 26/46/177 → **26/48/175**。
`skill.cpp` 只差 `FindSkillsForLevelUp`（要随机种子）与技能名/描述那几支文案；`game_static.cpp` 只转了
数据行与三支取用入口，护城河减防、城墙远程惩罚、绿洲移动点、中立怪上限、漩涡失踪率、雾发现距离都未转；
`heroes.cpp` 仍记「部分」，但经验侧已齐，现在只差军队/神器/法术点/移动。

2026-11-16：寻路的玩家侧与路线（ADR-091~094）：`WorldPathfinder` 基类的缓存/队列/邻居松弛、
`PlayerWorldPathfinder` 的 `reEvaluateIfNeeded`/`buildPath`/怪物挡路、`Route::Step` 与 `Route::Path`
的队列（除 `GetIndexSprite` 与 `String`）。`world` 行 0/3/2 → **0/4/1**（`world_pathfinding.cpp` 进「部分」），
`heroes` 行 1/2/9 → **1/3/8**（`route.cpp` 进「部分」），合计 26/48/175 → **26/50/173**。
`world_pathfinding.cpp` 只差 AI 侧的四个子类（`AIWorldPathfinder`/`ObjectValueStorage` 那一半要军队与
对象价值表）；`route.cpp` 只差 `GetIndexSprite`（260 行 `ICN::ROUTE` 精灵表，等渲染侧）与 `String`；
`heroes_move.cpp` 仍未动（它整个是动画与移动点记账，等 `Hero` 有军队与移动点）。

2026-11-16：军队、移动点与英雄行走（ADR-095~098）：`Army`/`Troop`（五槽、最慢兵种、合并、`Reset`）、
`Hero.Army`（`GetMaxMovePoints` 的六档基数表与 `_updateMovementPoints` 的 4/3、5/3 取整、灯塔 +500）、
`Hero.Movement`（九帧状态机、45~53 转向块、像素偏移、`Move` 的 `midStep` 守卫）与 `IHeroMovementHandler`
接缝。`army` 行 0/0/4 → **0/2/2**（`army.cpp`/`army_troop.cpp` 进「部分」），`heroes` 行 1/3/8 →
**1/4/7**（`heroes_move.cpp` 进「部分」），合计 26/50/173 → **26/52/171**。`army.cpp` 还差军队强度、
调兵与 `GetJoinSolution`（都要 `Troop::GetStrength` 与城堡）；`heroes_move.cpp` 还差清雾/对象动作/
小地图/渲染四个子系统与音效——调用点已按帧序保留在接缝里。

2026-11-16：幸运、士气与怪物能力（ADR-099）：`Luck`/`Morale` 两把七档尺子与 `Normalize`、
`MonsterAbilities`（32 项能力的位标记表，108 条语句 50 个兵种）、`Troop` 的 `GetRace`/`IsUndead`/
`IsAffectedByMorale`、`Army` 的 `AllTroopsAreTheSame`/`AllTroopsAreUndead` 与 `GetMoraleModificator`
（单一种族 +1、超过两种每多一种 −1、带亡灵再 −1）、`Hero.GetLuck`/`GetMorale` 的技能项。
`kingdom` 行 0/2/12 → **0/4/10**（`luck.cpp`/`morale.cpp` 进「部分」，只差文案），`monster` 行的
`monster_info.cpp` 从「未转」补成「部分」（行数与合计不变），合计 26/52/171 → **26/55/168**。
`luck.cpp`/`morale.cpp` 只差 `String`/`Description` 的翻译文案；`monster_info.cpp` 还差能力的
数值与法术载荷（战斗侧才用）；`Army::GetStrength` 与城堡、神器两项修正仍未转。

2026-11-16：兵种强度与法术 id（ADR-100）：`SpellType`（73 id + 哨兵）、能力实例
`MonsterAbilityInstance`（掩码改为从实例派生）、`GetMonsterBaseStrength`/`GetMonsterStrength`、
`Troop.GetStrength*` 与 `Army.GetStrength`。`spell` 行 0/0/4 → **0/1/3**（`spell.h` 进「部分」），
`monster` 行 0/1/2 → **0/2/1**，合计 26/55/168 → **26/56/167**。`spell.h` 只转了 id 枚举（等级、
学派、图标、施法与整个 `spell.cpp` 都未转）；`monster_info.cpp` 只差 `weaknesses` 与战斗图标/音效；
`Army::GetStrength` 的英雄侧三项（箭术、攻防、魔法战略值）此时只留了 `IArmyCommander` 接缝。

2026-11-16：神器袋与英雄四维（ADR-101）：`ArtifactTypes.IsValid`、`ArtifactTable`（105 行数值表）、
`ArtifactBonuses`（三堆 + 第四堆「按神器类型累加」）、`ArtifactBag`（平铺 14 格、魔法书置顶右移、
两种求和）、`PrimarySkill`/`Hero.PrimarySkills`（四维 + 神器修正 + clamp）与 `IArmyCommander`
（`Army.Commander`）。`resource` 行 1/1/2 → **1/2/1**（`artifact_info.cpp` 进「部分」），
合计 26/56/167 → **26/57/166**。`artifact_info.cpp` 只差 `ArtifactData::getDescription` 与
`getArtifactDescription` 两支文案；`artifact.cpp` 从「只有 id 枚举」扩到「`BagArtifacts` 全转」，
仍差 `Artifact` 类的名字/描述/精灵索引与 `getArtifactValue`/`getTotalArtifactMultipliedPercent`；
`artifact_ultimate.cpp` 未动。`Army.GetStrength` 的箭术（√ 放大）与英雄攻防已接上，
魔法战略值仍是 0（缺法术书与法力点）。

2026-11-16：法术书、法力点与魔法战略值（ADR-102）：`SpellTable`（73 行数值表，由脚本从 `spell.cpp`
解析）、`Spell`（等级、七条特性谓词、`GetStrategicValue`）、`SpellStorage`（去重追加、把神器上的
`ADD_SPELL` 并进来）、`Hero.Spells`（法术书/卷轴、`SpellPoints`、`GetMaxSpellPoints`、`HasSpell`、
`GetMagicStrategicValue`）。`spell` 行 0/1/3 → **1/1/2**（`spell_storage.cpp` 进「已转」），
合计 26/57/166 → **27/57/165**。`spell.cpp` 只差名字/描述文案、随机取法术、战斗侧的指向谓词与
`spellPoints( hero )` 的减耗（要 `getTotalArtifactMultipliedPercent`）；`Spell::getStrategicValue` 的
召唤分支照抄参考把**法术** id 当怪物 id 用（44..47 = 法师/大法师/巨人/泰坦），是参考的 bug，
端口为保持 AI 数值一致也照抄，`SpellTests` 钉住了这条。

2026-11-16：队伍的整理、统计与比较（ADR-103）：`Army.Troops.cs` 把 `Troops` 的容器半边搬过来 ——
`GetOptimized`、`MergeSameMonsterTroops`/`MergeSameMonsterOnce`、`SortStrongest`、
`GetWeakestTroop`/`GetStrongestTroop`、`GetCountMonsters`、`GetTroopsStrength`、
`GetReinforcementValue`、`IsStrongerThan`、`IsMeleeDominantArmy`、`GetStrongestMonster`、
`ResetInvalidMonsters`，外加 `Troop.IsFlying`。`Troops::GetStrength()` 与 `Army::GetStrength()`
拆成两个方法（前者只加兵、后者再乘士气/幸运/英雄项），因为参考只在比较「容器」时用前者。
`army` 行不变（`army.cpp` 仍是「部分」），合计仍 27/57/165。两军之间的调兵
（`JoinStrongest`/`MoveTroops`/`SwapTroops`）要 `IArmyCommander.isHeroes()`，留给下一批。

2026-11-15：两军之间的调兵（ADR-104）：`IArmyCommander` 补 `IsHeroes`（`Hero` 恒真），
`Army.Transfer.cs` 落地 `JoinStrongest`（四趟：同槽、填空槽、五强者交换、退回；末尾按「二十分之一」规则
给英雄留兵）、`JoinStrongestFromArmy`、`MoveTroops`（先并自己的重复、`monsterIdToKeep` 的换位、两趟搬运、
英雄最后一堆只留一个）、`SwapTroops(Army)`、`SwapTroops(Troop, Troop)`，另有 `SaveLastTroop`。
`MoveTroops` 里的那个 lambda 拆成私有方法 `MoveTroopsInto`。`army` 行不变（仍是「部分」），
合计仍 27/57/165。剩下 `UpgradeMonsters`、`SplitTroopIntoFreeSlots` 一族与 `GetJoinSolution`。

2026-11-15：升级线（ADR-105）：`MonsterUpgrades` 落地 `GetUpgrade`/`GetDowngrade`（各 21 行，龙是唯一的
两级链）、`IsAllowUpgrade`（写成 `GetUpgrade(m) != m`，不另立表）、`GetUpgradeCost`（差价 ×2，无升级返回空
`Funds`）；`Troop` 补 `IsAllowUpgrade`/`GetUpgrade`/`Upgrade`/`GetUpgradeCost`/`GetTotalUpgradeCost`；
`Army.UpgradeMonsters` 按兵种原地升级。测试里两条是整表不变量（21 行全部保持种族与等级、更强、更贵、
降级回到原兵种，并按种族计数 5/3/4/3/3/3）。`monster` 与 `army` 行仍为「部分」，合计仍 27/57/165。
`UpgradeTroops` 仍缺 `Castle`。

2026-11-15：城堡建筑表（ADR-106）：`Game.Castle.BuildingType`（33 项位掩码，照参考的 `castle.h:68-110`，
含 `MageGuild`/`DwellingMonsters`/`DwellingUpgrades` 三个组合掩码）；`CastleBuildings` 落地
`GetCost`（86 行造价表，逐行照抄，含六行同价的 `Well2` 与六行不同的 `Spec`）、
`GetRequirement`（前置掩码，按兵营 × 种族二维）、`GetUpgradeForBuilding`（五条全种族 + 六族各自的住所链，
术士的龙是唯一两级）、`IsDwelling`（12 个单一位，掩码不算）。`Monsters.Dwelling` 那个临时类删掉，
住所位改用 `BuildingType`，`DwellingMonsters.FromDwelling` 换成收 `BuildingType`。`castle` 行
0/0/11 → **0/3/8**，合计 27/57/165 → **27/60/162**。测试里 `TheCastleUpgradesAndTheMonsterUpgradesAgree`
把城堡的住所升级与 `MonsterUpgrades` 对成同一件事（各 21 条）。

2026-11-15：城堡骨架（ADR-107）：`Game.Castle.Castle` 落地 `castle.cpp` 里**不需要世界**的那一半 ——
构造与 `_postLoad()` 的无图部分、`isBuild`/`isCastle`/`isBuildingDisabled` 与三个种族 `Spec` 谓词、
`GetLevelMageGuild`、`CountBuildings`、`GetBuildingValue`、`GetActualDwelling`/`GetUpgradeBuilding`/
`GetMonstersInDwelling`/`IsExactBuildingBuilt`、`CheckBuyBuilding`/`AllowBuyBuilding`/
`GetAllBuildingStatus`、`BuyBuilding`、`ActionNewDay`、`AddBuildings`、`SetDefaultBuildings`、
`ChangeColor`；`BuildingStatus` 枚举同批（`castle.h`）。参考的三个全局被挪成参数或属性：
`Kingdom::AllowPayment`/`OddFundsResource` → `in Funds treasury` 参数、`Settings` 的地图版本 → 构造参数、
`HasSeaAccess()` 的扫水 → 可写属性 `HasSeaAccess`。`castle` 行 0/3/8 → **0/4/7**，合计 27/60/162 →
**27/61/161**。测试 17 条，重点是 `CheckBuyBuilding` 的**判定顺序**（已建 → 禁用位 → 三个特例 → 法师
公会逐级 → 今天能否建 → 帐篷/城堡规则 → 种族特有升级 → 前置位 → 资源）与六族 × 两版本整表扫。

### `engine`（26）

* ✅ `agg_file.cpp`→`AssetArchive`/`AggFileNameHash`；`image_palette.cpp`→`Palette`；
  `image_tool.cpp`→`IcnSpriteDecoder`+`PngWriter`/`PngReader`+`BmpWriter`+`MonoCursorFile`+TIL 负载读取；
  `logging.cpp`→`GameLog`；`pal.cpp`→`PaletteTable`；`rand.cpp`→`Pcg32` + `Rand` 门面（门面在
  ADR-070；`combineSeedWithValueHash` 未转，靠 `std::hash`，跨库不可复现）；`timing.cpp`→`FrameTimer`
* ◐ `image.cpp`（`Blit`/`Copy`/`Fill`/`ApplyTransform`/`Resize`/`CreateContour`/`Dithering` 已转，
  `SubpixelResize` 与重采样分支未）；`screen.cpp`（只转了呈现层：`PresentationViewport` + `SdlPresenter`）；
  `localevent.cpp`（SDL 事件→场景分派，落在 `App/GameHost`）；`audio.cpp`（`AudioPlayer`）；
  `translations.cpp`（只 `MOFile`→`MoCatalog`，`Translation::` 其余未）；`tools.cpp`（`Trim`/`GetString`
  这类零散助力，用 BCL 替代）；`zzlib.cpp`（AGG 解压走 `ZLibStream`）
* ❌ `audio_xmi2mid.cpp`、`core.cpp`、`dir.cpp`、`h2d_file.cpp`、`image_color_conversion.cpp`、
  `math_tools.cpp`、`render_processor.cpp`、`serialize.cpp`、`smk_decoder.cpp`、`system.cpp`、
  `thread.cpp`、`tinyconfig.cpp`

### `agg`（6）

* ✅ `agg.cpp`→`AssetBundle`（资料片优先/兜底）
* ◐ `icn.cpp`（`getAnimatedIcnIndex` 与 `ICN::` 名称已转，`CopyICNWithPalette` 与字号派生挪到
  `FontGlyphTable`；`GetString` 的枚举表未全）；`m82.cpp`（`MusicTrack` 只读元数据，播放未）
* ❌ `bin_info.cpp`（`MINIMON` 帧号现在用 `(id-1)*9` 硬编码，BIN 表未解析）、`mus.cpp`、
  `xmi.cpp`（XMI→MIDI 未做，`MusicTrackCatalog` 只读目录）

### `dialog`（29）

* ✅ `dialog_box.cpp`→`FrameBox`；`dialog_frameborder.cpp`→`FrameBoxChrome`
* ◐ `dialog_game_settings.cpp`→`SettingsDialog`（只有 Language 一行）；`dialog_selectscenario.cpp`→
  `ScenarioSelectionDialog`（列表/缩略图子集）；`dialog_language_selection.cpp`→`LanguageSelectionDialog`
* ❌ 其余 24：`adventure`、`arena`、`armyinfo`、`artifact`、`audio`、`buyboat`、`chest`、`file`、
  `gameinfo`、`giftresources`、`graphics_settings`、`hotkeys`、`interface_settings`、`levelup`、
  `marketplace`、`quickinfo`、`random_map_generator`、`recruit`、`resolution`、`selectcount`、
  `selectfile`、`selectitems`、`system_options`、`thievesguild`

### `editor`（18）

* ✅ `editor_ui_helper.cpp`→`EditorUiHelper`/`Checkbox`
* ◐ `editor_interface.cpp`→`EditorScene`/`GameHost`（自研 MP2 编辑器，工具与笔刷子集）；
  `editor_interface_panel.cpp`→`EditorPanel`；`history_manager.cpp`→`MapEditSession` 的撤销；
  `editor_object_popup_window.cpp`→`EditorScene.ObjectInfoSummary`
* ❌ 其余 13：`castle_details`、`daily_event_spec`、`daily_events`、`event_details`、`language_window`、
  `mainmenu`、`map_specs`、`options`、`rumor`、`save_map`、`secondary_skill_selection`、
  `spell_selection`、`sphinx`

### `game`（28）

* ✅ `game_mainmenu.cpp`→`MainMenuScene`（招牌/灯笼/门/右键提示）；`game_delays.cpp`→`IcnAnimation` 计时
* ◐ `game_assets.cpp`→`Game.Assets` 门面 + 字体派生（`getImage`/`getICN`/`CopyICNWithPalette`/
  `generateGradientFont` 已转，其余大量 `getXxx` 未）；`game_scenarioinfo.cpp`→`ScenarioSelectionDialog`；
  `game_newgame.cpp`→`GameHost` 的 NEW GAME 流程；`game_static.cpp`→`Factions`/`FactionProperties`/
  `SecondarySkillTable`（只有 `factionProperties` 六行与 `GetFactionProperties`/`GetSecondarySkillValuesPerLevel`/
  `getSecondarySkillsForWitchsHut` 三支，护城河、城墙、绿洲、中立怪、漩涡、雾距离未转）
* ❌ 其余 22：`difficulty`、`fheroes2`、`game_auto_playtest`、`game_campaign`、`game_credits`、
  `game_exit`、`game_highscores`、`game_hotkeys`、`game_init`、`game_interface`、`game_intro`、
  `game_invalid_assets`、`game_io`、`game_loadgame`、`game_mainmenu_ui`、`game_over`、
  `game_startgame`、`game_string`、`game_tools`、`game_video`、`game`、`highscores`

### `gui`（33）

* ✅ `cursor.cpp`→`CursorTheme`/`CursorLayout`；`ui_button.cpp`→`Button`/`ButtonBase`/`ButtonSprite`/
  `ButtonGroup`/`ButtonArtwork`/`DialogButtonTemplate`；`ui_dialog.cpp`→`MessageBox`；
  `ui_window.cpp`→`StandardWindow`；`ui_text.cpp`→`Game.Gui.*` 全套；`ui_tool.cpp`→`MovableSprite`；
  `ui_scrollbar.cpp`→`ScrollSlider`；`interface_font.cpp`→`CompositeGlyphSource`
* ◐ `ui_font.cpp`（主体 + 修补 + 派生已转，`generateCP1250GoodButtonFont` 这类代码页补充未）；
  `interface_gamearea.cpp`→`AdventureMapView.Draw`（地形/对象/英雄/怪物/船/雾已转，AUTO 组合与动画未）；
  `interface_radar.cpp`→`Minimap`；`interface_icons.cpp`→`IconBar`/`IconSpriteSet`；
  `interface_border.cpp`→`MapFrame`；`interface_buttons.cpp`→`ButtonsPanel`/`ButtonSpriteSet`；
  `interface_cpanel.cpp`→状态栏占位；`interface_events.cpp`→`MapScrollController`；
  `ui_monster.cpp`→`MonsterSpriteSet`；`statusbar.cpp`→占位
* ❌ 其余 15：`interface_base`、`interface_focus`、`interface_status`、`player_info`、`skill_bar`、
  `ui_base`、`ui_campaign`、`ui_castle`、`ui_keyboard`、`ui_kingdom`、`ui_mage_guild`、
  `ui_map_interface`、`ui_map_object`、`ui_option_item`、`ui_slider`

### `maps`（17）

* ✅ `ground.cpp`→`MapGround`/`MapGrounds`：`getTerrainImageIndex` 族的起始图号、`GetPenalty` 的九行代价表与
  五个代价常量、`isTerrainTransitionImage`/`doesTerrainImageIndexContainEmbeddedObjects` 的地形图像边界
  （ADR-082）；`mp2.cpp`+`mp2_helper.cpp`→`Mp2Map`/`Mp2TileInfo`/`Mp2AddonInfo`/
  `Mp2ObjectData`，对象分类谓词 `isOffGameActionObject`/`getBaseActionObjectType`/`isObjectCanBeAction`/
  `doesObject(NeedExtended|Contain)Metadata`（ADR-066/073）、`isWeekLife`/`isBattleLife`（ADR-077）
  与其余十一个判据 `isDayLife`…`isNeedStayFront`（ADR-081）
* ◐ `maps.cpp`（几何部分全部已转：`Game/Maps/MapGeometry` 的 index ↔ point、`GetDirection`、
  `GetDirectionIndex`、`getDirectionPoint`、`isValidDirection`、`isValidAbs(Index|Point)`、`getAroundIndexes`，
  外加 `Game/Maps/Direction`+`Directions` 照 `direction.h/.cpp` 逐条移植；距离 `GetApproximateDistance`/
  `GetStraightLineDistance`/`ComparisonDistance` 的比较算术已转（ADR-085）；对象查询
  `doesObjectExistOnMap`/`GetObjectPositions`/`getObjectParts` 已转（`World` 实例方法，ADR-069），
  扫描与守卫 `scanAroundObject`（两个重载）/`hasNearbyObject`/`isValidForDimensionDoor`/
  `scanAroundObjectWithDistance`/`GetObjectPositions( center, … )`/`forEachMonsterProtectingTile`/
  `getMonstersProtectingTile`/`isTileUnderProtection` 已转（ADR-084；`isTileProtectionStrongerThan` 要
  `Army`、`getVisibleMonstersAroundHero` 要 `Heroes` 的视野距离）；`getSquaredScoutingRadiusLimit` 落在
  `MapFog.SquaredRevealLimit`（ADR-086）；渲染侧 `drawFog`→`MapFog`/`DrawFog`，`ClearFog` 的半径版与
  `getFogTileCountToBeRevealed` 仍由 `MapFog.Reveal` 承担）；`maps_fileinfo.cpp`→
  `Mp2FileInfo`/`MapCatalog`；`maps_objects.cpp`→`Mp2ObjectPreset`/`Mp2ObjectName`（对象表子集）
  与 `Game/Maps/MapObject`/`MapSign`/`MapEvent`/`MapSphinx`/`MapObjects`（告示/瓶子、地面事件与
  狮身人面的装载，ADR-074/075；拾取与问答题信息框未转）；
  `maps_tiles.cpp`（雾位 + `Tile` 的对象层/地形位/道路位/通行掩码/`Init`/独立通行计算/`isStream`/
  `isShadow`/`updatePassability`/`updateObjectType`/`isAnyTallObjectOnTile`/`isDetachedObject`/
  `getIndexOfMainTile`/`getHero`/`setHero`/`getMainObjectType`/`_getMainObjectTypeUnderHero` 已转，
  对象感知通行 `isSuitableForDisembarkation`/`isSuitableForSummoningBoat`/`isSuitableForUltimateArtifact`/
  `isPassableFrom( direction, fromWater, ignoreFog, heroColor )` 也已转（ADR-083，只差同盟城堡入口），
  `isCoast`/`updateFogDirection` 部分；迷雾位与 `ClearFog` 已转（ADR-079），只剩船）；
  `maps_tiles_helper.cpp`（`updateTerrainTransitionOnTile`/`getObjectPartByActionType` 已转；
  `getFundsFromTile`/`getArtifactFromTile`/`getBarrierColorFromTile`/`doesTileContainValuableItems`/
  `setResourceOnTile`/`setBarrierColorOnTile`/`resetObjectMetadata` 已转，ADR-080；
  只剩半径 `ClearFog`、`updateFogDirectionsInArea`（此前误记为已转，实际未搬）与法术/军队几支）；
  `maps_tiles_helper.h` 的 `ArtifactCaptureCondition` 已转（ADR-080）；
  `maps_tiles_render.cpp`（`drawByObjectIcnType`/`drawFog`/`drawMainObjectTiles` 已转）
* ◐ `map_object_info.cpp`→`Game/Maps/MapObjectCatalog`：`ObjectInfo` / `ObjectPartInfo` /
  `LayeredObjectPartInfo` / `ObjectGroup` 与 `getObjectsByGroup`、`getObjectPartByIcn`、
  `getGroundLevelOccupiedTileOffset` 等查询已转；**参考的 20 个 `populate*()` 全部落地，21 个组无空表**
  （roads 513、streams 13、landscape 3+40+69+12+147+8+14、adventure 25+56+23+13+96+13+71、
  kingdom 42+14、monsters 71、extras 7），其中 17 组由 `GenerateMapObjectTables.ps1` 逐行生成，
  kingdom towns / monsters / artifacts 手写（lambda；id 现用 `MonsterType` / `ArtifactType` 枚举，
  见 `PROGRESS.md`）；
  `Tile::getMainObjectType` 依赖 `Tile` 的缓存字段，随 `world` 一起搬
* ❌ `position.cpp`、`visit.cpp`、
  `map_format_helper.cpp`、`map_format_info.cpp`（编辑器自有格式，本项目走 MP2，**不适用**）、
  `map_random_generator.cpp`、`map_random_generator_helper.cpp`、`map_random_generator_info.cpp`

### 其余目录

* `world`（5）：◐ `world.cpp` 搬了骨架与通行重算——`World` 的 `w()`/`h()`、瓦片表与两个 `getTile`、
  `generateUninitializedMap`、`Reset`、`ResetPathfinder` 占位（ADR-065）、`updatePassabilities`（ADR-066）、
  对象查询与石阵/漩涡（ADR-069/070）、地图对象与每日事件/传闻容器（ADR-074/075）、
  日期（`_day`/`_week`/`_month`、派生显示值、`DateString`、`NewDay` 的时钟与剪枝，ADR-076）、
  `CountObeliskOnMaps`（ADR-077）与俘获对象（`map_captureobj`、`CaptureObject`/`CountCapturedMines`/
  `ResetCapturedObjects`，ADR-078）；英雄/城堡/王国这些容器、`GetWeekType`/`NewWeek`/`NewMonth`、
  区域图、静态分析都未转；
  ◐ `world_loadmap` 搬了瓦片循环、add-on 链、`fixMP2MapTileObjectType`、`MP2::OBJ_HERO` 与
  `MP2::OBJ_JAIL` 的信息块建人（ADR-067/072/073）、`MP2::OBJ_SIGN`/`OBJ_BOTTLE`/`OBJ_EVENT` 的
  对象装载（ADR-074）、`MP2::OBJ_SPHINX` 与无瓦片的每日事件/传闻块（ADR-075）与 `PostLoad` 的
  可运行子集，城堡表/AI 区域未转（俘获对象的数据结构已落，ADR-078）；`PostLoad` 的石阵/漩涡/法师之眼缓存另在
  `World` 上落地（ADR-069），读取端 `GetTeleportEndPoints`/`GetWhirlpoolEndPoints`/`NextTeleport`/
  `NextWhirlpool` 已接（ADR-070）；
  ◐ `world_object_uid` 的计数器改挂在 `World` 上（`LastObjectUid`/`GetNewObjectUid`，ADR-067）；
  ◐ `world_pathfinding` 的**玩家侧**已转：`WorldPathfinder` 抽象基类包住缓存、标签修正式搜索、
  「最后一格」规则、船的对角切角与怪物「只到不穿」（ADR-091），`PlayerWorldPathfinder` 是五元组
  缓存 + `BuildPath` 反向回溯（ADR-092），入口挂 `World.Pathfinder`，`ResetPathfinder()`/`LoadMap`
  都会丢缓存；AI 侧（`AIWorldPathfinder`/`ObjectValueStorage`）要军队与对象价值表，未转；❌ `world_regions`
* `ai`（9）、`battle`（18）、`campaign`（3）、`h2d`（1）、`audio`（1）：**全部未转**
* `castle`（11）：◐ `castle.h` 的 `BuildingType` 位掩码已转（`Game.Castle.BuildingType`，33 项含三个组合
  掩码，ADR-106）；◐ `castle_building_info.cpp` 的前置表 `GetRequirement` 与升级表
  `GetUpgradeForBuilding` 已转（ADR-106），名字/描述（译文）与图标矩形表（要 AGG）未转；
  ◐ `buildinginfo.cpp` 的 86 行造价表 `GetCost` 与 `isDwelling` 已转（ADR-106），`BuildingInfo` 那个
  界面构件（要 `Castle`）未转；◐ `castle.cpp`→`Game.Castle.Castle` 的**无世界骨架**已转（ADR-107）：
  构造 + `_postLoad()` 的无图那半、`isBuild`/`isCastle`/`isBuildingDisabled`/三个种族 `Spec` 谓词、
  `GetLevelMageGuild`、`CountBuildings`、`GetBuildingValue`、`GetActualDwelling`/`GetUpgradeBuilding`/
  `GetMonstersInDwelling`/`IsExactBuildingBuilt`、`CheckBuyBuilding`/`AllowBuyBuilding`/
  `GetAllBuildingStatus`、`BuyBuilding`、`ActionNewDay`、`AddBuildings`、`SetDefaultBuildings`、
  `ChangeColor`；未转招募（`RecruitMonster`/`getRecruitLimit`/`getAvailableArmy`）、`ActionNewWeek`/
  `ActionNewMonth` 的周产井水加成、税收与法师公会法术、`_postLoad()` 的填货尾巴、`GetPowerModificator`
  与 AI 访问价值；❌ `castle_heroes`、`castle_masons_guild`、`castle_recruit`、`castle_ui`、
  `castle_switch`、`castle_building_info` 的其余部分
* `army`（4）：◐ `army.cpp`→`Army`（五槽、取用、合并、清空、`Reset` 与初始兵数、
  `AllTroopsAreTheSame`/`AllTroopsAreUndead`、士气修正、含英雄箭术/攻防/魔法战略值的 `GetStrength`，
  ADR-095/099/100/102；`Army.Troops.cs` 的容器半边：`GetOptimized`/`MergeSameMonsterOnce`/
  `MergeSameMonsterTroops`/`SortStrongest`/`GetWeakestTroop`/`GetStrongestTroop`/`GetCountMonsters`/
  `GetTroopsStrength`/`GetReinforcementValue`/`IsStrongerThan`/`IsMeleeDominantArmy`/
  `GetStrongestMonster`/`ResetInvalidMonsters`/`UpgradeMonsters`，ADR-103/105；`Army.Transfer.cs` 的两军
  调兵：`JoinStrongest`/`JoinStrongestFromArmy`/`MoveTroops`/两个 `SwapTroops`/`SaveLastTroop`，
  `IArmyCommander.IsHeroes`，ADR-104）、
  `army_troop.cpp`→`Troop`（数值、血伤造价、种族/亡灵/士气/飞行四条能力查询、`GetStrength*`、
  `IsAllowUpgrade`/`GetUpgrade`/`Upgrade`/`GetUpgradeCost*`，ADR-095/099/100/103/105）；
  `UpgradeTroops`（要 `Castle`）、`SplitTroopIntoFreeSlots` 与三个同族（要拖单堆的界面）、
  `GetJoinSolution`、`ArrangeFor*` 与城堡/神器那两项修正未转；❌ `army_bar.cpp`、`army_ui_helper.cpp`
* `spell`（4）：✅ `spell_storage.cpp`→`SpellStorage`（去重、书后追加神器上的 `ADD_SPELL`、
  `Remove`/`IsPresent`/`GetSpells`，ADR-102）；◐ `spell.cpp`→`Spell`（73 行数值表、等级、七条特性谓词、
  `GetStrategicValue`，ADR-102），名字/描述文案、随机取法术、战斗侧的指向谓词与 `spellPoints(hero)`
  的减耗未转；❌ `spell_book.cpp`
* `heroes`（12）：◐ `direction.cpp` 已转；`heroes.cpp` 转成
  「部分」：`AllHeroes::Init` / `Clear` / `GetHeroForHire`、`isAvailableForHire`、`getHeroIdRangeForRace`
  （`Game/Heroes/AllHeroes`、`HeroRaces`，ADR-071）与 `Heroes::LoadFromMP2` 的前半（位置/颜色/种族改判/
  肖像/名字/巡逻，ADR-072）已落，另有 `Hero` 的 id 取值域、种族/颜色/肖像、mode 位、`isFriends`（暂等价于
  同色，ADR-083）、瓦片占用契约（ADR-062/068/071）、经验/等级表（`HeroExperience`，ADR-090）、
  军队与移动点（ADR-095/096）、`GetLuck`/`GetMorale` 的技能项（ADR-099）与 `heroes_base` 的法术书/
  法力点/`GetMagicStrategicValue`（ADR-102）；
  `skill.cpp` 也记「部分」：14 项枚举、数值表、种族初始技能、`SecSkills`、外交折扣已转（ADR-087~089），
  只差 `FindSkillsForLevelUp`（要随机种子）与技能名/描述文案；`route.cpp` 也记「部分」：
  `Route::Step`→`RouteStep`（ADR-093）与 `Route::Path`→`HeroPath` 的队列语义
  （`FrontIndex`/`FrontFrom`/`FrontDirection`/`FrontPenalty`/`NextStepDirection`、`Truncate`/
  `PopFront`/`Show`/`Hide`/`IsValidForMovement`/`IsValidForTeleportation`/`GetAllowedSteps`，
  ADR-094）已落，`_hero` 指针换成 `movePoints`/`heroIndex` 参数；`heroes_move.cpp` 也记「部分」：
  九帧状态机、转向块、像素偏移、帧循环（`Hero.Movement`，ADR-097），清雾/对象动作/小地图/渲染四个
  缺失子系统走 `IHeroMovementHandler` 接缝（ADR-098）；未转 `GetIndexSprite`（260 行 `ICN::ROUTE`
  精灵表，等渲染侧）、`String`、行走音效与淡入淡出；法术书、AI 动作仍未转
* `kingdom`（14）：◐ `color.cpp`→`PlayerColor`、`race.cpp`→`MapRace`、`luck.cpp`→`Luck`、
  `morale.cpp`→`Morale`（两把七档尺子与 `Normalize`，ADR-099）；❌ `experience`、
  `kingdom_overview`、`kingdom`、`payment`、`profit`、`puzzle`、`resource_trading`、
  `speed`、`view_world`、`week`
* `monster`（3）：◐ `monster.h` 的 `MonsterType` id 枚举已转（`Game.Monsters.MonsterType`，73 项），
  `monster.cpp` 只有 ID、`MINIMON` 帧号、`GetMonsterStrength` 与升级关系（`MonsterUpgrades` 的
  `GetUpgrade`/`GetDowngrade`/`IsAllowUpgrade`/`GetUpgradeCost`，ADR-105）；◐ `monster_info.cpp`：
  通用数值表、战斗数值表、住所映射（`DwellingMonsters`，ADR-095；住所位本身已搬进 `BuildingType`，
  ADR-106）、32 项能力表与载荷（`MonsterAbilities`，ADR-099/100）与两条
  强度（`GetMonsterBaseStrength`/`GetMonsterStrength`，ADR-100）已转，`weaknesses` 与战斗图标/音效未转；
  `RandomMonster`/`GetRandomMonster`/`GetDwellingFromMonster` 等随机与住所反查未转；❌ `monster_anim`
* `resource`（4）：✅ `resource.cpp`→`Funds`/`Resource`/`Cost`；◐ `artifact.cpp`→`ArtifactBag`
  （平铺 14 格、魔法书置顶右移、`Count`/`IsPresent`/`ContainSpell`、`getTotalArtifactEffectValue`
  的两种求和，ADR-101）与 `artifact.h` 的 id 枚举（`Game.Resources.ArtifactType`，105 项含
  `ArtifactCount`，`ArtifactTypes.IsValid`），`Artifact` 类自己的名字/描述/精灵索引与
  `getArtifactValue`/`getTotalArtifactMultipliedPercent` 未转；◐ `artifact_info.cpp`→`ArtifactTable`/
  `ArtifactBonuses`（105 行数值表、`getArtifactData`、六个 `isBonus*`/`isCurse*` 谓词，ADR-101），
  `ArtifactData::getDescription`/`getArtifactDescription` 两支文案未转；❌ `artifact_ultimate`
* `system`（3）：◐ `players.cpp`→`PlayerColor`/`HumanPlayers`、`settings.cpp`（只有语言与分辨率种子）；
  ❌ `bitmodes`

### 还差的主干（按依赖顺序）

1. `maps/map_object_info.cpp` —— 拾取、信息框与 `ActionToObject` 的总闸。类型、全部 21 组表与它们依赖的
   `MonsterType` / `ArtifactType` 两个 id 枚举都已落地（表大小与枚举锚点分别钉在 `MapObjectCatalogTests`、
   `IdEnumTests`）。`Tile::getMainObjectType` 的缓存字段也已随第 3 条落地（ADR-066）。
2. `heroes/heroes_move.cpp` + `maps/visit.cpp` —— 英雄移动与路径预览，`MapFog` 的增量更新挂在这里。
   `world/world_pathfinding.cpp` 的玩家侧、`route.cpp`（ADR-091~094）与 `heroes_move.cpp` 的状态机
   （ADR-095~098）都已落：路径查找、路线队列、军队与移动点、九帧动画都在。剩下的是它调用的四个子系统
   ——清雾（`Scout`）、对象动作（`Action`）、图层重绘（`ActionNewPosition`）与小地图（`ScoutRadar`），
   调用点已按帧序留在 `IHeroMovementHandler` 里，接上实现即可。
3. `world/world.cpp`、`world_loadmap.cpp`、`world_object_uid.cpp` —— 可变世界状态，`castle`/`kingdom`/
   `army` 都依赖它。   `World` 的骨架与 `updatePassabilities` 已立（ADR-065/066），`Tile` 的邻居通行也已
   接上；`world_loadmap` 的瓦片循环 + add-on 链 + 类型修复 + `PostLoad` 子集与 `world_object_uid` 的
   计数器已落（ADR-067），英雄占用（`Tile.setHero`/`getHero` + 最小 `Hero`）也已落（ADR-068），
   地图对象查询与石阵/漩涡/法师之眼缓存也已落（ADR-069），装载时按信息块建英雄并落到格子上也已落
   （ADR-072），牢房里的英雄同样已落（ADR-073），告示/瓶子与地面事件的对象装载也已落（ADR-074），
   狮身人面与无瓦片的每日事件/传闻块同样已落（ADR-075）。信息块的六个分支到这里全部落地。
   下一步是 `PostLoad` 里剩下的城堡表（`Castle` 骨架已立，ADR-107，只差按信息块建城与落格）与 AI 区域。
4. `kingdom/*` → `castle/*` → `army/*` → `battle/*` —— 顺序不能反，`battle` 依赖 `army` 与 `castle`。
5. 旁支：`engine/serialize.cpp`（存档，M8）、`h2d_file.cpp`（M3 遗留）、`smk_decoder.cpp`（过场动画）、
   `audio_manager.cpp`（音乐播放，现在只有元数据）。

## 依赖方向（单向，不允许反向引用）

```
FHeroes2.Core  ←  FHeroes2.Assets  ←  FHeroes2.Rendering  ←  FHeroes2.UI
                        ↑                    ↑                    ↑
                        └──── FHeroes2.Game ─┴────────────────────┘
FHeroes2.App  →  引用以上全部（组合根）
```

## 主要风险与对策

| 风险 | 对策 |
| --- | --- |
| ICN 变换层/裁剪语义复杂 | M1 已落地「参考导出对比」工具链：C++ 侧链接原版源码导出 BMP，C# 侧逐字节比较（ADR-015） |
| 字体：原版位图字体 vs TrueType 度量差异 | `IGlyphSource` 抽象（ADR-018）：`BitmapFont` 走原版 ICN 字形，`TrueTypeGlyphSource` 只补 CJK／非单字节字符，两者共用 `GlyphText` 的布局；布局统一用逻辑像素。颜色统一走 `PaletteTable` 索引重映射（ADR-020），中英文同色。许可已澄清（该包由本项目作者维护，ADR-019），剩余风险只是无 hinting 的糊边与渐变字体尚未移植 |
| 大端/字节对齐陷阱 | 统一 `LittleEndianReader`，禁止 `BinaryReader` |
| 存档与 C++ 不互通 | 自研版本化 schema（`CURRENT_FORMAT_VERSION` 仅作参考），不做互操作 |
| 音频/视频后端差异 | SMK 与 82M 解码作为独立可测试组件，先做数据层再接入播放 |
| 未来可能需要 NativeAOT（单文件/无 JIT 冷启动） | M0 起开启 AOT/裁剪分析器并禁止反射（ADR-011），暂不提供发布配置；M9 评估 |
