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
快照于 2026-11-15（M4 完成、E3b 之后），**每个里程碑收尾时重算一次**；`agg`、`castle`、`system`、`audio`、
`h2d`、`resource` 与 `army` 七行，以及 `engine` 行里的 `h2d_file.cpp`，于 2026-11-17（ADR-134、ADR-135、
ADR-139、ADR-140、ADR-141、ADR-142、ADR-144）单独重算过；`army` 行又于 2026-11-19 按 ADR-182 重算（1/3/0）。

| 目录 | 文件数 | 已转 | 部分 | 未转 |
| --- | --- | --- | --- | --- |
| `engine` | 26 | 8 | 7 | 11 |
| `agg` | 6 | 6 | 0 | 0 |
| `ai` | 9 | 0 | 0 | 9 |
| `army` | 4 | 1 | 3 | 0 |
| `audio` | 1 | 1 | 0 | 0 |
| `battle` | 18 | 8 | 5 | 5 |
| `campaign` | 3 | 0 | 0 | 3 |
| `castle` | 11 | 3 | 8 | 0 |
| `dialog` | 29 | 2 | 4 | 23 |
| `editor` | 18 | 1 | 4 | 13 |
| `game` | 28 | 2 | 6 | 20 |
| `gui` | 33 | 7 | 12 | 14 |
| `h2d` | 1 | 1 | 0 | 0 |
| `heroes` | 12 | 2 | 5 | 5 |
| `kingdom` | 14 | 6 | 6 | 2 |
| `maps` | 17 | 5 | 7 | 5 |
| `monster` | 3 | 0 | 2 | 1 |
| `resource` | 4 | 2 | 2 | 0 |
| `spell` | 4 | 1 | 3 | 0 |
| `system` | 3 | 3 | 0 | 0 |
| `world` | 5 | 1 | 4 | 0 |
| **合计** | **249** | **49** | **75** | **125** |

2026-11-19 重算了四行（都是「下面逐文件清单早就写了，表格没跟上」）：

* `battle` 8/5/5 —— `battle_animation`、`battle_command`、`battle_pathfinding`、`battle_troop`、`battle_grave`、
  `battle_bridge`、`battle_catapult`、`battle_tower` 八个在 ADR-148/154/155/156/157 里已经是 ✔，表里还记着 0。
* `dialog` 2/4/23 —— `dialog_armyinfo.cpp` 在 ADR-174 已转部分（排版、绘制、消息循环）。
* `gui` 7/12/14 —— ✅ 里的 `interface_font.cpp` 在参考树里根本不存在（那是 `ui_font.cpp` 的一部分），删掉；
  `ui_mage_guild.cpp`（ADR-175/178 转了 `SpellsInOneRow`、行偏移与绘制）与 `ui_language.cpp`（CRC32 指纹表在
  `System.ResourceLanguage`，列表控件未转）都改记部分。
* `heroes` 2/5/5 —— `route.cpp`、`skill.cpp` 在 ADR-138/089 之后早该算部分。

合计 41/64/144 → **48/74/127**（这一轮改的四处）。

2026-11-19 夜：`game_startgame.cpp`（`StartGame()` 的回合循环、`humanTurn()` 的开头两句）与 `game.cpp`
（每帧的英雄移动）从未转转为部分（ADR-179），`game` 行 2/4/22 → 2/6/20，合计 **48/74/127**。

2026-11-19 收尾：`army` 目录四个文件全量盘点（ADR-182）—— `army_ui_helper.cpp` 已转，`army_bar.cpp` 从「未转」
转「部分」（规则、绘制与事件都在，缺的是调用方），`army.cpp`/`army_troop.cpp` 仍是「部分」（缺加成文案与
`*String`）。`army` 行 0/2/2 → **1/3/0**，合计 **49/75/125**。

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
`combineSeedWithValueHash`，ADR-113 补上）；覆盖率不变，`world.cpp` 仍是「部分」。

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

2026-11-16：castle 目录的整套逻辑（ADR-134）：`CastleBuildingInfo`（`castle_building_info.cpp:806-1438`：
面积/名字/描述/前置文字/图标序号/绘制优先级，六族三张大表与绘制顺序由 PowerShell 脚本从参考 `.cpp` 生成，
`CastleBuildingInfo.Tables.cs`）、`MageGuild`（`mageguild.cpp`）、`Captain`（`captain.cpp`）、`ISpellBookOwner`
（参考 `HeroBase` 的那一半，英雄与队长共用）、`Hero.CanLearnSpell`/`AppendSpellToBook`；`Castle` 补
`GetMoraleModificator`/`GetLuckModificator`/`GetPowerModificator`、`GetAvailableArmy`/
`GetArmyRecruitmentValue`/`GetVisitValue`/`GetGarrisonStrength`、`AllowBuyHero`/`RecruitHero`、
`TrainHeroInMageGuild`/`TrainGuestHeroAndCaptainInMageGuild`/`InitializeMageGuild`、`GetDescriptionBuilding`，
并把 `_mageGuild`/`_captain` 挂进 `PostLoad` 与 `BuyBuilding`。顺手修掉法师塔的一个真 bug：免费槽数没算图书馆那格
（`mageguild.cpp:91` 的 `getMaxSpellsCount( level, hasLibrary )`），图书馆镇 1 级少一个法术。`castle` 行
0/4/7 → **3/5/3**（合计 +3 完成 / +1 部分 / −4 未转）。测试 19 条（`CastleInfoTests`）。剩七个界面/绘制文件，见下。

2026-11-16：城堡招募与升级（ADR-108）：`Castle.GetRecruitLimit`/`RecruitMonster`（`castle.cpp:1025-1106`）、
`DwellingMonsters.GetDwelling`（`Monster::GetDwelling`，`monster.cpp:691`）与 `Troop.GetDwelling`、
`Army.UpgradeTroops`（`army.cpp:528-560`）。货架与款项的钳制按参考的不对称照抄（只钳货架，价与入队用调用方
给的数），`getLowestQuotient` 的负数进 `uint` 也照抄。`castle`/`army`/`monster` 行都已是「部分」，行数与
合计不变（27/61/161）。测试 14 条，含一条「六族 × 12 个住所位：反查回来必在同一货架上」的整表不变量。

2026-11-16：城堡的 70 字节块（ADR-109）：`Castle.LoadFromMP2`（`castle.cpp:102-395`）按顺序读完全部字段，
`PostLoad`（`castle.cpp:428-535`）做那六步修复，`JoinRandomArmy`（`castle.cpp:2218-2247`）给中立城补守军，
`Army.Assign`（`army.cpp:493-501`）按位放堆。世界缺席的地方一律变参数：难度、总天数、所属王国的种族、
随机源；船坞规则要的 `HasSeaAccess` 由调用方先设。法师公会的法术与船长两步仍未转，代码里点名。行数与合计
不变（27/61/161）。测试 19 条，全部用手搓的 70 字节块，不依赖地图文件。

2026-11-16：王国与七槽容器（ADR-111）：`Game.Kingdom.Kingdom` 落地 `kingdom.cpp` 里「账本 + 名册」这层
骨架 —— `Treasury`（`ref Funds`，接得上 ADR-107/108 的 `ref Funds` 下单接口）、`AllowPayment`、
`AddFundsResource`/`OddFundsResource`、`GetStartingResources`（玩家/电脑两张表 × 五档难度）、
`AddCastle`/`RemoveCastle`/`AddHero`/`RemoveHero`、`IsLoss`/`IsLosingGame`/`HasHeroes`/`CanRecruitHeroes`、
`LostTownDays` 与五个计数；`Game.Kingdom.Kingdoms` 落地七槽容器（`GetKingdom` 的非法颜色落第七槽、
`Init`/`Clear`/`AddCastles`/`AddHeroes`/`GetNotLossColors`）。世界装载时按地图文件头的颜色与难度起王国
（`World.Kingdoms.cs`），瓦片读完后再把英雄与城塞进名册。周产/税收、招募、访问记录、解谜地图、
`GetCountArtifacts` 与 `LossPostActions` 仍未转，代码里逐条点名。`kingdom` 行 0/4/10 → **0/5/9**，合计
27/61/161 → **27/62/160**。测试 22 条，含一条真实装载（合成地图 + 70 字节块）验证颜色→王国的接线。

2026-11-16：日收入（ADR-112）：`Game.Kingdom.ProfitConditions` 整篇落地 `profit.cpp` —— 矿井（七种资源的日产）、
建筑（城堡 1000、帐篷/雕像 250、术士的 `Spec` 500）与神器（`ArtifactBonus` 里八种资源奖励 + 税单的
`GoldPenalty`）三张表；`Game.Kingdom.Kingdom.GetIncome( countCapturedMines, IncomeType )` 落地
`kingdom.cpp:564-646` 里能算的两项（矿井与城镇），神器与技能两项要英雄的神器袋与技能（都还没落），电脑难度
加成与人类折扣要 `players.cpp`，逐条在注释与 `IncomeType` 上点名。`profit` 行由「未转」转「已转」：
`kingdom` 行 0/5/9 → **1/5/8**，合计 27/62/160 → **28/62/159**。测试 14 条（三张表逐项、未知矿与未知神器的
空答案、金鹅 10000、税单 -250、七只无尽袋子、回调被问到的七种资源、帐篷/城堡/雕像/术士地牢的累加、
`IncomeType` 位掩码过滤）。

2026-11-16：周与月（ADR-113）：`Game.Kingdom.Week`/`WeekName` 落地 `week.h` + `week.cpp` —— 34 个周名
（15 个动物周 / 10 个昆虫月 / 怪物之周 / 瘟疫之月）、`RandomWeek( isNewMonth, weekSeed )` 的两条抽签表
（周 75/25，月 50/40/10）与十二种月怪；`Rand.CombineSeedWithValueHash`（`rand.h:260`）补进 Core 的 `Rand`
门面，六个盐值（367245 / 1946256 / 9536582 / 5544783 / 886473 / 1130906）随之可复现。`Week::GetName()`
未转（要翻译层与 `Monster::GetName()`），`week` 由「未转」转「部分」：`kingdom` 行 1/5/8 → **1/6/7**，合计
28/62/159 → **28/63/158**。测试 17 条 + Core 1 条（默认值、同种子稳定、11 个种子的快照钉住六个盐、
两条扫描验证只有怪物周/月带怪物、月怪只在十二种里、四种结果的概率）。

2026-11-16：把周接进世界、日入账（ADR-114）：`World.MapSeed`（`world.h:504`，`Reset` 归零，`Defaults` 的随机
取值等会话）、`World.WeekSeed`（`world.cpp:1263` 的加盐）与 `World.WeekType`（`world.cpp:410`，按需算不缓存）、
`World.GetEventsDate( color )`（`world.cpp:875` 的过滤）；`Kingdom.ActionNewDayResourceUpdate( dayCount,
countCapturedMines, events, isAiControlled, displayEventDialog )` 落地 `kingdom.cpp:225-250`（首日不结算、
收入 + 当日事件、AI 跳过人类专属事件、金额按金库钳制并回报对话框）。覆盖率不动（`week` 仍「部分」、
`kingdom` 仍「部分」）：合计仍 **28/63/158**；测试 15 条（周种子的加盐与归零、周类型随周计数与「月初」标志
走两条表、九周不是同一个抽签、`GetEventsDate` 的颜色/日期/重复/顺序、首日不给钱、次日起矿井付钱、事件给钱
与负事件的钳制、AI 跳过人类专属、对话框看到的是实付金额）。

2026-11-16：城堡的周产（ADR-115）：`Castle.ActionNewWeek( weekCount, week, isBeginMonth, dayCount, generator )`
落地 `castle.cpp:838-940` —— 首周跳过、六个基础住所按 `GetActualDwelling` 的生长加一周（井 +2、第一住所的
`Well2` +8）、中立城减半并收随机守军（城堡两次、普通城 40% 才第二次）、怪物周只喂「精确建成」的匹配住所
（+5）、月初的怪物月按库存再加 100%、瘟疫月的月初把全部库存减半；`GrownWell`/`GrownWel2`/`GrownWeekOf`/
`GrownMonthOf` 在参考里是 `game_static.cpp` 的函数，端口按「还没有设置层」的理由落成 `const`；
`AllCastles.NewWeek`/`NewDay` 与 `World.NewDay`（`world.cpp:427-459`：月 → 周 → 日）的接线同批完成，顺手修掉
`AllCastles.Add` 在宽度为 0 时先算坐标的除零。行数与合计不变（`castle.cpp` 与 `world.cpp` 早就在「部分」里），
仍 **28/63/158**。测试 13 条。

2026-11-16：世界的区域划分（ADR-116）：`world_regions.cpp` 整篇落地 —— `MapRegion`/`MapRegionNode`/
`MapRegionNodes` 三个类型（`world_regions.h:28-65`）与 `World.ComputeStaticAnalysis()`（九步：数障碍 →
挑最空的每七行/列作种子线 → 城堡当种子 → 补种子 → 建带一圈 padding 的工作副本 → 每个种子一个区域 →
每轮各长一格 → 剩下的自己成区 → 写回瓦片并把邻居连起来，含石阵/漩涡）。同批加 `Tile.Region`/`UpdateRegion`
（`maps_tiles.cpp:70-78`）、`World.WaterPercentage`/`LandRoughness`/`RegionCount`/`GetRegion`，`PostLoad`
末尾按 `world.cpp:1233` 调用，`Reset` 顺手清掉这些格子级缓存。`world` 目录 0/4/1 → **1/4/0**，合计
**28/63/158** → **29/63/157**。测试 8 条（含一条真实地图的不变量测试）。

2026-11-16：王国的价目与市场汇率（ADR-117）：`payment.cpp` 整篇落地成 `PaymentConditions`（船 1000 金 + 10 木、
法术书 500 金、酒馆英雄 2500 金、炼金术士 750 金、麦哲伦地图 1000 金，建筑造价转发 `CastleBuildings.GetCost`），
`resource_trading.cpp` 整篇落地成 `ResourceTrading.GetTradeCost`（common/rare/gold 三个类别的七张 9 列汇率表，
市场数 0 返回 0、超过 9 钳到 9、非法资源位返回 0）。`kingdom` 目录 1/6/7 → **3/6/5**，合计
**29/63/157** → **31/63/155**。测试 11 条。同时更正一条旧结论：1.1.18 的 `PostLoad` 里没有「漩涡带船的修复」，
带船的漩涡格被记成船对象、因此按部件查找，端口本来就是这么做的，`PostLoad` 到此已经完整。

2026-11-16：酒馆的那一对英雄（ADR-118）：`heroes_recruits.cpp`+`heroes_recruits.h` 整篇落地成
`Recruit`（id + 投降日，`default` 就是参考的「UNKNOWN 与 0」）与 `Recruits`（两个槽位、`SetHero1`/`SetHero2`、
`AppendSurrenderedHero` 的「替换更旧那一格」规则——同一天时替换第一格，这是参考的 `>` 在并列时为假），
`Kingdom` 新增 `GetRecruits()` 并在 `Clear()` 里重置（`kingdom.cpp:132`）。参考 `GetRecruits()` 还会按需补位，
那要 `Settings`/战役存档/`Players`，留给后面。`heroes` 目录 1/4/7 → **2/4/6**，合计 **31/63/155** →
**32/63/154**。测试 7 条。

2026-11-16：石阵的拼图（ADR-119）：`puzzle.h`+`puzzle.cpp` 的位集部分落地成 `Puzzle`（48 格、四条区带在
创建时洗牌、`Update` 按「外框 → 内环 → 两个 2×2」的顺序翻格、只增不减），洗牌走 `Pcg32` 参数（存档可复现），
`Kingdom` 新增 `GetPuzzleMaps()` 并在 `Clear()` 里 `Reset()`（`kingdom.cpp:134`，只清格子不重洗）。
对话框（`ShowMapsDialog`）与存档读写留给渲染层与存档层。`kingdom` 目录 3/6/5 → **4/6/4**，
合计 **32/63/154** → **33/63/153**。测试 8 条。

2026-11-16：王国的访问名册与日/周推进（ADR-120）：`visit.cpp`+`visit.h`→`IndexObject`/`VisitScope`/`Visit`
（三条寿命谓词），`Kingdom` 有了 `SetVisited`/`IsVisited`×3/`CountVisitedObjects`、旅行者帐篷位图、
`KingdomModes`、`IsPlay`/`IsControlHuman`、失城倒计时与每日/每周清理、调试赠礼；`World.NewDay` 接上
`Kingdoms.NewDay/NewWeek`。石阵那一行动作也落了（`Kingdom.ActionObelisk`，ADR-121），拼图终于有了调用方。
`maps` 目录 3/7/7 → **4/7/6**（`visit` 由「未转」变 ✔；`position.cpp` 仍缺），合计 **33/63/153** →
**34/63/152**。测试 17 条。

2026-11-16：英雄侧的访问名册、动作层与运气/士气重算（ADR-122）：`Hero.Visit.cs`（`LOCAL`/`GLOBAL`
两层名册、`SetVisited`/`IsVisited`×3/`GetAllVisitedObjectTypes`、日/周/战后三个清理、每日回蓝与神秘术加成）、
`GameStatic.cs`（每日回蓝、运气/士气修正、移动点加成四张表）、`HeroActions.cs`（魔法井、泉眼、运气/士气建筑、
黑猫、风车/水车/小屋/魔法花园、马厩、旅行者帐篷、石阵、竞技场、屏障、免费取宝十二支 + `ArtifactPickupOutcome`
+ 三张对象清单）、`Tile.GetArtifactCaptureCondition`，`GetMorale`/`GetLuck` 改为按访问类型集合重算；
`Kingdom.AddHero` 补 `SetColor`，`Mp2ObjectTypes.IsDayLife` 的比较值改回不带动作位的 `MagicWell`。
`heroes.cpp`、`heroes_action.cpp`、`game_static.cpp`、`maps_tiles_helper.cpp` 本来都在「部分」里，合计仍
**34/63/152**。测试 37 条。

2026-11-16（续）：英雄的经验与升级（ADR-123）：`Hero.Experience.cs`（经验 → 等级反查、连升逐级压门槛、
四个种子由地图种子与英雄状态混合、`autoselect` 抽牌与可注入的选择委托）、`SkillLevelUp.cs`（主技能加权抽签、
副技能两候选的黑名单与魔法职业每三级强制智慧、`IsMagicalRace`）、`HeroActions.cs` 补凉亭（1000 点、瓦片级
一次）与知识树（正好一级经验、可收费、两份名册分开写）、`Tile.GetTreeOfKnowledgeRequirement`。
`skill.cpp` 也进了「部分」列表，合计仍 **34/63/152**（`skill.cpp` 本来算在「部分」）。测试 47 条。

2026-11-16（再续）：神殿与女巫小屋（ADR-124）：`TileMetadata.GetSpellFromTile` 与
`GetSecondarySkillFromWitchsHut`（车道读取）、`Hero.SecondarySkills.cs`（`HasSecondarySkill`/`GetLevelSkill`/
`HasMaxSecondarySkill`/`LearnSkill`）、`HeroActions.ActionToShrine`（无书/无智慧/已有/学会四道门）、
`HeroActions.ActionToWitchsHut`（满八槽/已会/学会三道门），两个结果枚举；名册一律写在分支之外、只写王国。
合计仍 **34/63/152**。测试 57 条。

2026-11-16（三续）：宝箱与仙那度（ADR-125）：`ArtifactGold.InsteadOf`（神奇折金表）、
`GameStatic.IsHeroWorthyToVisitXanadu`（等级 + 外交术门槛）、`HeroActions.ActionToTreasureChest`（海上/陆地有钱/
只有神器三条分支，二选一交给委托）、`HeroActions.ActionToXanadu`（三道门，四主技能各 +1）。更正：fheroes2 里
没有「学者」对象。合计仍 **34/63/152**。测试 70 条。

2026-11-16（四续）：「钱或神器」一簇（ADR-126）：`HeroActions.ActionToPickupResource`（散落资源/营火/桶）、
`ActionToSkeleton`、`ActionToWagon`、`ActionToFlotsam`、`ActionToShipwreckSurvivor` 与 `IsPickupResource`、
结果类型 `ObjectPickupReward`。四支的「满包 / 清元数据 / 记名册」规则各不相同，货车是唯一不折金的。合计仍
**34/63/152**。测试 84 条。

2026-11-16（五续）：监牢与英雄入伙（ADR-127）：`HeroActions.ActionToJail` + `JailOutcome`、`Hero.Recruit`
（`Hero.Recruit.cs`）、`AllHeroes.FromJail`、`World.FromJailHeroes`、`Kingdom.AllowRecruitHero`。顺序是先查
名额再找囚犯，监牢不查钱、不清元数据、不记名册。合计仍 **34/63/152**。测试 94 条。

2026-11-16（六续）：塞伦与告示牌（ADR-128）：`Army.ActionToSirens`（30% / 至少 1 个 / 独苗不动，按血量折经验）、
`HeroActions.ActionToSirens` + `SirensReward`、`HeroActions.ActionToSign`（取 `World.GetMapSign` 的留言，不改
状态）。合计仍 **34/63/152**。测试 105 条。

2026-11-16（七续）：迷雾入世界（ADR-129）：`World.Fog`（按需创建、`Reset` 丢弃）、`World.ClearFog`
（`maps.cpp:464`）、`World.ActionForMagellanMaps`（`world.cpp:906`）、`MapFog.Reveal` 改返回揭开数、
`ObservationTowerRevealDistance`/`MagiEyesRevealDistance` 两个常量，以及 `HeroActions.ActionToObservationTower`、
`HeroActions.ActionToMagellanMaps` + `MagellansMapsOutcome`（两段式确认）。合计仍 **34/63/152**。测试 118 条。

2026-11-16（八续）：开局侦察（ADR-130）：`Hero.GetScoutingDistance`（神器 + 4 + 侦察术数值）、
`Hero.Scout`、`AllHeroes.Scout`、`Castle.Scout`、`AllCastles.Scout`、`World.ClearFog( PlayerColor )`
（`world.cpp:838`）与第一个真实的移动处理器 `FogHeroMovementHandler`（走路即开图）。合计仍 **34/63/152**。
测试 130 条。

2026-11-16（九续）：场景接上世界迷雾（ADR-131）：`AdventureMapScene` 把地图装进 `World.Current` 并画
`World.Fog`，`ClearFog( observers )` + 聚焦城堡占位（`ScoutStart`）；`World.ClearFog( PlayerColor )` 末尾补
`UpdateDirections`（修「零揭开时一格雾都不画」）；删除 `MapFog.CreateForMap`。冒险地图像素基线与
`tests/baselines/adventure-map.png` 逐字节一致。合计仍 **34/63/152**。测试 133 条。

2026-11-16（十）：传送门与漩涡（ADR-132）：`HeroActions.ActionToTeleports`、`ActionToWhirlpools`（找出口 →
`Scout` → `MoveToDest` → 清路线）、`MoveThroughTeleport`、`LoseTroopsToWhirlpool`（`heroes_action.cpp:300`）、
`Troop.GetCountFromHitPoints`（`monster.cpp:840`）、`GameStatic.LostOnWhirlpoolPercent`、`TeleportOutcome`。
合计仍 **34/63/152**。测试 141 条。

2026-11-17：`audio` 目录（ADR-140）：`audio_manager.cpp` 整个落地到 `Game/Audio/`——`AudioManager`（WAV 头包装、
两级缓存、音乐三级回退、`PlayCurrentMusic`/`StopSounds`/`ResetAudio`）、`AudioTaskQueue`（`AsyncSoundManager` 的
三种优先级与「取出即出队」，工作线程换成每帧一次的 `Update()`）、`AudioLoopEffects.Plan`（角度优先、距离破平、
并列取后者，3D 开关翻转时全清）、`MusicIdentity`、`IAudioDevice`/`IAudioSource`、`MusicRestorer`。`audio` 行
0/0/1 → **1/0/0**，合计 **37/61/151** → **38/61/150**。测试 37 条（`AudioLoopEffectsTests` 10、
`AudioTaskQueueTests` 8、`MusicIdentityTests` 3、`AudioManagerTests` 16）。

2026-11-17：`h2d` 目录（ADR-141）：`h2d.cpp`→`Game.H2D.H2DResources`（68 个文件名的样本表与开档校验、
`Current` 门面、`TryReadCurrentImage`），底层 `engine/h2d_file.cpp`→`H2DArchive`/`H2DWriter`/`H2DImageCodec`
（记录表、zlib 体、17 字节图像头与单层标志，写回按名字序重排）。`h2d` 行 0/0/1 → **1/0/0**，`engine` 行
7/7/12 → **8/7/11**，合计 **38/61/150** → **40/61/148**。测试 37 条（`H2DArchiveTests` 19、
`H2DImageCodecTests` 10、`H2DResourcesTests` 8）；`AssetProbe h2d` 可用真实 `resurrection.h2d` 对拍。

2026-11-17：`resource` 目录收尾（ADR-142）：`artifact.cpp` 剩下的半边全部落地——`ArtifactLevels`（`Level`/
`LoyaltyLevel`/`IsUltimate`/`IsPriceOfLoyalty`，105 行的档位表与 `.mp2` 上资料片宝物只有 `Loyalty` 位）、
`ArtifactValues.Value`（`getArtifactValue` 的权重表）、`ArtifactRandom`（`Rand` 的两遍抽取 + `ART_RNDDISABLED`/
`ART_RNDUSED` 一张位图，`Reset`/`Exclude`）、`ArtifactMapSprites.FromMapIndex`（旧格式的精灵索引反查、
五个随机对象、207 只在 Resurrection 里是法术书）、`ArtifactSets`+`ArtifactBag.AssembleArtifactSets`（Anduran
三件 → BattleGarb）、`ArtifactBag.SortFromWorstToBest`、`GetTotalMultipliedPercent` 两个重载、
`GetArtifactValue`/`ContainsUltimateArtifact`/`GetFirstArtifactWith*`/`IsBonusPresent`/`IsCursePresent`，以及
`ArtifactExchange`（`exchangeArtifacts` 的池化、三条让与规则、按价值发牌与回吐）；`artifact_ultimate.cpp` →
`UltimateArtifact`（`Set`/`IsAt`/`MarkAsFound`/`Reset` 与 ±2/±1 的偏移）。`resource` 行 1/2/1 → **2/2/0**，
合计 **40/61/148** → **41/61/147**。测试 106 条（`ArtifactLevelTests` 35、`ArtifactValueTests` 24、
`ArtifactRandomTests` 10、`ArtifactMapSpritesTests` 19、`UltimateArtifactTests` 6、`ArtifactExchangeTests` 7、
`ArtifactBagTests` 追加 5）；Game 1889 → **1995**，全解决方案 2639 → **2745** 全绿。`artifact.cpp` 仍记 ◐：`Artifact` 类的名字/描述文案、`String()` 与 `description` 重载、`ArtifactsBar`
（UI）没转。

2026-11-17：`army` 收尾（ADR-144）：军队形状那批规则全落 —— `Army.Size.cs` 的九档档位与两条文案
（`SizeRangeString`/`SizeString`）、`Army.Split.cs` 的分兵/合并七件套、`Army.JoinSolution.cs` 的
`GetJoinSolution`（免费/外交/逃跑/不加入）、`Army.Arrangement.cs` 的 `SetFromTile` 九种固定守卫与三个
`ArrangeFor*`，外加 `Army.AreAllTroopsUnique`。**行数与合计不变**（`army` 仍 0/2/2，合计 41/63/145）：
`army.cpp` 剩的是文案/绘制/序列化/`GetControl`（与 `spell.cpp` 记 ◐ 同一口径），`army_bar.cpp` 与
`army_ui_helper.cpp` 整份是界面。测试 86 条新增，Game 2022 → **2108**，全解决方案 2772 → **2858** 全绿。

2026-11-17：`castle` 收尾（ADR-145）：`castle.cpp` 剩下的规则落地 —— 队长变成指挥官（`IArmyCommander`，
攻防知吃城的 modificator、力量钳到 255、士气/幸运走 `Normalize`）、四条船坞方法、`ActionPreBattle`/
`ActionAfterBattle`、`IsPosition`/`IsControlAi`、`GetGarrisonStrength` 的客将合军，以及六族的 `GetICNBuilding`/
`GetICNBoat` 表（`Castle.IcnTables.cs`，147 条用脚本逐条对过参考）。顺带补上 ADR-144 留下的两项遗留：
`Army.GetMoraleModificator`/`GetLuckModificator` 的城堡与神器项，新增 `ArtifactBag.IsArtifactCursePresent`
（非累计诅咒不能走 `GetTotalArtifactEffectValue`，那有 cumulative 断言）。**行数与合计不变**（`castle` 仍
3/5/3，合计 41/63/145）：`castle.cpp` 剩下的是塔的守城强度、`ActionNewMonth` 的维护费与税收、`DrawImageCastle`
与 `ActionPreBattle` 的 AI 分支，七个界面文件整份未转。测试 37 条新增，Game 2108 → **2145**，全解决方案
2858 → **2895** 全绿。

2026-11-17：`monster` 目录的规则面（ADR-146）：弱点表（只有四个元素人，顺序照抄）、随机分桶
（`GetRandomUnitLevel`/`Rand`，四桶 10/15/27/14 共 66 只，`Any` 直接掷 `Peasant..WaterElement`）、
弹道与住所图标（`GetMissileICN`/`ICNMonh`，半人马借精灵的箭）、`Monster( const Spell & )` 的召唤映射、
`getSpellResistance`（判定顺序不能换：龙的 100 魔抗必须晚于三项心灵/亡灵免疫）与 `isRandomMonster`。
**覆盖率不动**：两份 `.cpp` 只剩文案（要 `Spell.GetName()` 与 `_()`）与只被战斗侧读的 `icnId`/`sounds`，
`monster_anim.cpp` 依赖 `battle_animation.h`。测试 61 条新增，Game 2145 → **2206**，全解决方案
2895 → **2956** 全绿。

2026-11-17：`battle` 起步（ADR-147）：`battle.h` 的方向环与 `Result`、`battle_board.cpp` 的纯几何先搬过来 ——
11×9 偏移六角格的 `isValidDirection`/`GetIndexDirection`/`GetDirection`/`GetDistance`、城堡/墙外/护城河三张
索引表、宽单位的四个候选格与八格环、半径环。**行数动了**：`battle` 0/0/18 → **0/1/17**，合计 41/63/145 →
**41/64/144**（`battle_board.cpp` 的几何已全，剩绘制与单位相关部分）。测试 33 条新增，Game 2206 → **2239**，
全解决方案 2956 → **2989** 全绿。

2026-11-17：`battle` 的第二批（ADR-148）：先补上压在 `Unit` 底下、却不依赖 `Unit` 的三层 ——
`engine/timing.h` 的 `TimeDelay`（`>=` 判过期、`Pass()` 把起点拨到 `now - 2*delay`）、
`battle_animation.cpp` 的 `RandomizedDelay`/`AnimationSequence`/`AnimationReference`/`AnimationState`
（33 支动画拼战斗序列，远程优先 `Shoot*`、否则 `DoubleHex*` 当吐息，无 STATIC 时兜底 `[1]`；状态机与
多状态拼接的帧偏移），以及 `battle_command.cpp` 的 `BattleCommand`（参数**倒着存**，`GetNextValue` 从尾部弹，
`UpdatePcg32Stream` 在攻击时只混移动格与双方 UID，好让人机与人类掷同一条序列）。同时 `battle_cell.cpp` 的
`BattleCell` 几何落地（44×52 的格、42 的行距、奇数行左移 22、六边形八切片与三角判定）。
另有两条测试期望被参考纠正：`IDLE` 取不到待机时回落静态图所以不算缺失；偏移行在 `bin_info.cpp:315-317`
已被裁到对应动画帧数，因此只设起步帧时拿不到三段 48 个偏移。**行数动了**：`battle` 0/1/17 → **2/2/14**
（`battle_animation.cpp`、`battle_command.cpp` 由未转变 ✔，`battle_cell.cpp` 转 ◐），合计 **41/64/144** →
**43/65/142**。测试 47 条新增（`BattleAnimationTests` 23、`BattleCommandTests` 9、`BattleCellTests` 8、
`TimeDelayTests` 7），Game 2239 → **2279**，全解决方案 2989 → **3036** 全绿。

2026-11-17：`battle` 的第三批（ADR-149）：开门的就是 `Unit` 本身。先把它脚下的东西铺平 —— `BattleBoardCells`
（99 格数组，替代 `Board::GetCell` 那个竞技场单例转发）、`BattlePosition`（头尾两格、`Set`/`Swap`/`GetRect`/
`GetPosition`/`CompareTo`，只差要问竞技场的 `GetReachable`）、`BattleMonsterState`（`SP_`/`TR_`/`CAP_` 那张
标志位表）与 `BattleModesAffected`/`BattleModeDuration`（限时法术效果的时钟），再落 `BattleUnit` 的第一层：
位置与朝向（含格子占位）、兵力/血量/箭矢、模式位、`GetSpeed`（减速与加速互斥、定身与「已行动」返回 0）、
飞行/双击/反击/再生/定身这些能力判定。伤害结算（`ApplyDamage`/`CalculateDamageUnit`）、`GetDefense` 的城堡与
护城河、`GetMorale`/`GetControl`、`ApplySpell` 族与 `isHandFighting` 留到下一批 —— 它们各自要
`battle_arena`、`HeroBase` 与法术应用层。**行数**：`battle` 2/2/14 → **2/3/13**，合计 **43/65/142** →
**43/66/141**。测试 29 条新增（`BattleUnitTests` 12、`BattlePositionTests` 12、`BattleModesAffectedTests` 5），
Game 2279 → **2308**，全解决方案 3036 → **3065** 全绿。

2026-11-17：`battle` 的第四批（ADR-150）：`Unit` 的第二层——战斗算术。`GetAttack`/`GetDefense`
（血 lust、石肤/钢肤的 else-if、每道瓦解射线 −3 防御且不到 0、护城河 −3）、`CalculateDamageUnit`
（射手近战减半且 `NO_MELEE_PENALTY` 免疫、箭术加成、城墙射击惩罚、护盾只挡射击、致盲反击减伤、石化减半、
对亡灵与四系元素弱点只翻一倍、攻防差 +10%/−5% 各自封顶）、`GetDamage`（祝福取顶/诅咒取底、幸运翻倍/减半）、
`HowManyWillBeKilled`、`ApplyDamage`（噬魂可超上限、吸血按对方单兵血量且封顶）、`Resurrect`（不许超上限时截断）、
`NewTurn`（再生补满、六个「本回合用过」标记清零、限时法术减时并在到期时摘掉、镜像到期连镜像一起打死）与
`PostKilledAction`（解镜像、摘法术、交还格子；坟场登记等 `battle_grave`）。竞技场那侧的四件事收进 `IBattleField`。
同时**纠正第三批的 `GetSpeed`**：参考不看 `CAP_TOWER`（塔不动是竞技场不让它动），且加速先判、减速后判；
逐行对齐 `battle_troop.cpp:424-447`。测试 24 条新增（`BattleUnitCombatTests`），Game 2308 → **2332**，
全解决方案 3065 → **3089** 全绿。

2026-11-17：`battle` 的第五批（ADR-151）：坟场、桥与单位集合 —— 三份围着 `Unit` 转、却不依赖竞技场的小对象。
`BattleGraveyard`（`std::map` 的格子→尸体链，宽单位压两格，`MoveToLastIfPresent` 用 rotate 语义把尸体挪到最上面）
接进 `BattleUnit.PostKilledAction`（先登记再交还格子；镜像与元素不进坟场）；`BattleBridge`（六格常量、
`NeedDown` 的六个落点分支、`AllowUp` 的「桥上有活人有死尸都不许拉」、`SetPassability` 把 0/1 写进闸门格、
`SetDestroyed` 的断桥终态）；`BattleUnits`（FindUid/FindMode/稳定降序的 `SortFastest`——按**战斗速度**排，
与参考里 `Troop::GetSpeed` 是虚函数一致——以及三个筛选副本）。桥与坟场之间只靠「闸门/护城河上有没有东西」耦合，
所以桥的构造参数就是格盘、坟场与守城方颜色。测试 27 条新增（`BattleGraveyardTests` 9、`BattleBridgeTests` 10、
`BattleUnitsTests` 8），Game 2332 → **2359**，全解决方案 3089 → **3116** 全绿。

2026-11-17：`battle` 的第六批（ADR-152）：`BattleForce` —— 把一支 `Army` 摆上战场。摆位公式逐字照抄
（列间距 11/铺开 22、镜像侧 +10、宽单位让一格），UID 记在槽位表里好与「原始军队」对号；伤亡清点、亡灵巫术的
「只算本场真死的人」、按兵种表血量算经验、战后写回人数、每回合清英雄的 `SPELLCASTED`、投降价（英雄基数 0.5、
宝物连乘、外交再乘一档、四舍五入且不低于 1）。顺带给 `BattleUnit` 补上 `Army` 回指、`IsMonster`、`GetCost`、
`GetSurrenderCost`、`GetControl`/`GetCurrentControl`；`Army` 补 `IsSpreadFormation` 与 `GetControl`；
`IArmyCommander` 补 `GetLevelSkill`（默认 None）与 `ResetModes`（默认空实现），英雄与队长各自实现
`GetControl`；`HeroModes` 补 `Spellcasted = 0x4`。测试 16 条新增（`BattleForceTests`），Game 2359 → **2375**，
全解决方案 3116 → **3132** 全绿。

2026-11-17：`world` 收尾（ADR-160）：上一批留下的四条 `world.cpp` 尾巴一起去掉 —— **周与月**
（`NewDay` 的 `++_week`/`++_month`、`NewWeek` 的招募池重置与周命对象刷新、`NewMonth` 的怪物月
`MonthOfMonstersAction` 与它的 85/10/5 比例与数量下限）、**埋宝**（`setUltimateArtifact`：读占位对象
`ActionRandomUltimateArtifact` 的半径与神器 id、半径内挑格、占位物与其左格影子一起移除、`Width/9` 的边缘
不可用）、**只打一场的世界**（`generateBattleOnlyMap` 2×2 + `setUniformTerrain`）与**法文地图的字符修复**
（`ui_font.cpp:7190` 的 16 项表 + `fixFrenchCharactersInStrings` 走英雄/城堡/传闻/事件/牌瓶/地面事件/斯芬克斯）；
`world_loadmap` 的**可占领对象表**也在这一批（锯木厂、炼金实验室、五座矿、灯塔、龙城、废矿，中立主）。
顺带修掉一个真 bug：`TileTroops.SetMonster` 原先写非动作值 `Monster`，而 `NewWeek` 的怪物成长与怪物月的邻格
排除都按 `ActionMonster` 判，等于永不生效；现在按参考（`maps_tiles_helper.cpp:1727`）写 `ActionMonster`，
寻路里那条 `Monster` 分支同步改，两条既有测试随之更正。合计仍 **48/69/135**（都还在「部分」里），
测试 Game 2538 → **2565**、Assets 412 → **416**，合计 3295 → **3326** 全绿。

2026-11-17：`ai` 只落契约（ADR-161）：`ai_planner.h` 的公开半（`AiPlanner` 十五个成员 + 五个数据结构）、
`ai_battle.h`（四个结构 + `BattlePlanner` + `getBerserkerTarget`）、`ai_common.h` 的九个自由函数、
`ai_hero_action.h` 的四个入口、`ai_planner_internals.h` 的四个常量全部搬进 `Game/Ai/`；签名照抄参考，行为留
`NotImplementedException`（消息带参考文件），只把没有依赖的真行为写掉：`RegionStats` 的未求值默认值、
`BudgetEntry.Reset`、`SpellcastOutcome.UpdateOutcome` 的三条语义、两个单例、四个优势阈值。三处**端口化改写**
记在 ADR 里：`GameMode` → `AiTurnOutcome`（只有 `EndTurn`/`QuitGame` 两个值）、`HeroBase&` → `IArmyCommander`、
`Battle::Actions&` → `List<BattleCommand>`。私有方法一律不声明（三十来个，参数依赖未落地的对象价值表）；
`AIWorldPathfinderStateRestorer` 不落（要 `AIWorldPathfinder`）。测试 13 条（`AiContractTests`），Game 2565 →
**2578** 全绿。2026-11-19 补：私有半边照 `ai_planner.h:200-281`、`ai_battle.h:97-165` 全部声明，`BattleBegins()`
与 `ResetTurnCaches()` 等四处带代码；`AIWorldPathfinderStateRestorer` 已能实现并落地（ADR-180）。

2026-11-17：`dialog` 的 24 个入口落成契约（ADR-163）：`dialog.h` 的 `AdventureOptions`…`ArtifactSetAssembled`
搬进 `UI/Dialogs/` 的 20 个模型（`QuickInfo` 七个入口合一个类、`SelectFileLoad`/`Save` 合一个、`ArmyJoinFree`/
`WithCost` 合一个），每个只有 `Create(...)` + 结果类型，`Area`/`Render`/事件留给控件层（`Create` 签名不会因
此改变）。`DialogButton` 从 4 个值扩到参考的 14 个（`Dismiss`/`Upgrade`/`Max`/`Prev`/`Next`/`World`/`Puzzle`/
`Info`/`Dig`），因为 `AdventureOptions` 与 `ArmyInfo` 的答案就是它们。三处类型改写：`GameMode` →
`FileOptionsResult`（六个值，含两个存档动作）、`bool` → `GoldOrExperience`、哨兵值（`-1`/`UNKNOWN`/空 `Troop`）
→ `null`，每个都写在 ADR 里。`FromButton(int)` 的按钮索引映射与 `Create` 的参数校验（`min <= max`、`step > 0`、
`join > 0`、负值拒绝）是真行为，其余是纯 DTO。占位三处：`inputString` 的 `TextBase` → `string`、`SelectCount`
的上下控件与屏幕偏移不建模、`SelectFile` 的列表不建模。测试 25 条（`DialogModelsTests`），UI 145 → **170**，
全解决方案 3 339 → **3 364** 全绿。

2026-11-17：`world_pathfinding` 的 AI 侧（ADR-164）：`AIWorldPathfinder`（`world_pathfinding.cpp:543-1434`）整块
落下 —— AI 的成本函数与无移动点上限、守怪强度（`WorldMapScan.IsTileProtectionStrongerThan`，`maps.cpp:644`）、
节点里的三份 `bool?` 记忆槽、城门户播种与石柱/漩涡零成本跳转「到了不许再走」、需正面交互对象离开时补计一次、
`BuildPath( target, accountNearestObject )` 的裁剪。基类 `ProcessWorldMap` 改 `protected virtual` 并拆出
`ClearCache`/`SeedStartNode`/`ProcessNodes`/`ResetNode` 与六个缓存访问器；`World.AiPathfinder` 惰性创建并交给
`AiPlanner.AttachPathfinder`，`ResetPathfinder()` 同时重置两份缓存。英雄相关的六个入口（`GetFogDiscoveryTile`/
`GetNearestTileToMove`/`IsHeroPossiblyBlockingWay`/`BuildDimensionDoorPath`/`GetObjectsOnTheWay`/
`ReEvaluateIfNeeded(Hero, double)`）仍抛异常，注释写明缺的数据。测试 13 条（`AIWorldPathfinderTests`），
Game 2578 → **2591**，全解决方案 3 364 → **3 377** 全绿。

2026-11-17：`castle` 的界面侧开工（ADR-165）：先把 `buildinginfo.cpp` 里与贴图无关的三块落下 ——
`BuildingInfo`（四个改写：实际住所 → 可升级 → 亡灵酒馆 → 状态；`Cost`/`Name`/`Description`/`Area`；
`getBuildingDescription` 的住所分支与 `%{count}` 替换）、`DwellingsBar`（六个住所、`ItemsBar` 的
`calculateRoi`/`calculateItemsPos` 布局、命中、周产含水井与地标、左键三态与右键意图）、`MonsterNames`
（`monsterGeneralStats` 的名称两列 72 条、`GetMultiName` 的不规则复数、随机城镇的 level 文案）。
唯一行为偏离是 `RenderingOffset` 连整栏矩形一起重算，避免 setter 顺序敏感；队长矩形仍留默认 `135x70`。
`castle` 文件统计从 3/5/3 变 3/6/2（`buildinginfo.cpp` 记部分）。测试 21 条，Game 2591 → **2612**，
全解决方案 3 377 → **3 398** 全绿。

2026-11-17：`castle_building.cpp` 的非绘制部分（ADR-166）：`FadeBuilding`（0 起步、`< 246` 加 9 否则 255、
船的 `IsOnlyBoat`、`StopFade`，帧钟当 `Update(bool)` 的入参）、`BuildingsRenderQueue`（按
`GetDrawingPriorities` 建 `BuildingRenderInfo`，构造时把 `top` 加进每个矩形）、`CastleBuildingRender`
（`IsBuildingConnectionNeeded` 的野蛮人法师塔/盗贼公会与亡灵队长室、`IsBuildingFullyBuilt`、`ShouldDrawBuilding`
把参考的两支合并成一支，含「升级版正在淡入时基础建筑还画」的例外）。`CastleGetMaxArea`/`getTownIcnId` 要贴图
尺寸与 ICN 编号，blit 三个函数要 ICN，都没做。`castle` 统计 3/6/2 → 3/7/1。测试 9 条
（`CastleBuildingRenderTests`），Game 2612 → **2621**，全解决方案 3 398 → **3 407** 全绿。

2026-11-19：`castle_mageguild.cpp` 与 `castle_well.cpp` 的规则部分（ADR-167）：`Castle.GetMageGuildSpellRows`
（五层，槽位数问 `HasLibraryCapability`、法术问 `IsLibraryBuilt`，行 `0` 是底部的一级）、
`UI/Dialogs/MageGuildDialog`（行偏移 `365 - 90 * i`、单城禁用城镇切换、两句状态栏）、
`CastleMaxRecruitment`（从六级住所示弱到一级分配、`Refusal` 三态与三句文案、`Summary` 单复数、草稿军队）。
两个文件的绘制循环都没转。`castle` 统计按标记重数为 3/5/3（已转 3、部分 5、未转 3；上一轮写的 3/7/1 与标记
对不上，这里以标记为准）。测试 15 条（`CastleMageGuildViewTests` 4、`MageGuildDialogTests` 4、
`CastleMaxRecruitmentTests` 7），Game 2621 → **2632**，UI 170 → **174**，全解决方案 3 407 → **3 422** 全绿。

2026-11-19：`castle_dialog.cpp` 与 `castle_town.cpp` 的文字部分（ADR-168）：`BuildingInfo.GetStatusLine`
（悬停状态行，住所报 `"Recruit {复数小写}"`，随机城镇退化到等级）、`UI/Dialogs/CastleDialog`（屏幕日期行大写
形式、八种 `Result` 含 `DoNothing`）、`UI/Dialogs/RecruitHeroDialog`（`"%{name} is a level %{value} %{race} "`
加三档宝物句，魔法书从计数里扣掉，价格取 `PaymentConditions.RecruitHero()`，可用状态由调用方给）。两个文件的
绘制与事件循环仍未转。`castle` 统计 3/5/3 → 3/7/1（只剩 `castle_tavern.cpp`，它要等 `world.cpp` 的传闻池）。
测试 17 条（`BuildingInfoTests` 增 4、`CastleDialogTests` 4、`RecruitHeroDialogTests` 9），Game 2632 →
**2636**，UI 174 → **187**，全解决方案 3 422 → **3 439** 全绿。

2026-11-19：传闻池先立形状（ADR-169）：`LocalizedString`（`text` + 可选语言，`game_string.h:30-50`）、
`World.Rumors.cs`（`StandardRumorCount` = 9、九条内置文案、九个方向词、`TotalRumorCount`；`GetCurrentRumor`/
`ChooseRumorId`/`GetRegionName` 三个函数体留空抛出）；`castle_tavern.cpp` 的文字转成
`UI/Dialogs/TavernDialog`（标题「Tavern」、开场句、空白行、传闻及其语言标签、只有 OK）。`castle` 统计
3/7/1 → 3/8/0，目录里没有完全未转的文件了。测试 13 条（`RumorPoolTests` 7、`TavernDialogTests` 6），
Game 2636 → **2643**，UI 187 → **193**，全解决方案 3 439 → **3 452** 全绿。

2026-11-19：传闻池补齐（ADR-170）：`GetCurrentRumor` 接周种子与世界，`ChooseRumorId` 走
`Rand.GetWithSeed( 0, total - 1, WeekSeed )`，`GetRegionName` 把地图切三份（严格比较、先行后列），句子抽成
静态 `Describe`；`UltimateArtifact.Name` 给出八个终极宝物的名字与参考的 `"Invalid Artifact"` 占位；
`TavernDialog.Create( World )` 转发。Game 2643 → **2684**，UI 193 → **194**，全解决方案 3 452 → **3 494** 全绿。

2026-11-19：王国的神器收入与周名（ADR-171）：`Kingdom.GetIncome` 补上 `INCOME_ARTIFACTS`（走每个英雄的整只
神器袋，空格与魔法书都交给表回答 0），`Week.GetName()` 给出 34 个周名词（怪物周取生物单数名、瘟疫是参考自己的
全大写、表外用 `Unnamed`），`MonsterNames.GetPluralName( count )` 顺手补上。同时更正三处过时注释：`IncomeType`
与 `GetIncome` 还写着「英雄没有神器袋」，`MonsterNamesTests` 还写着「金鹅是唯一带收入的神器」。`kingdom` 统计
仍 6/6/2（两处都在 ◐ 的文件里），`monster` 仍 0/2/1。Game 2684 → **2697**，全解决方案 3 494 → **3 507** 全绿。

2026-11-19：法术的文案两列（ADR-172）：`spell.cpp` 的 `spells` 表里名字与描述两列落成 `SpellText`（73 行，
`spell|` 上下文前缀按 gettext 的显示规则去掉，`FireBlast` 仍拼 `Fireblast`、`StoneSkin` 仍拼 `Stoneskin`），
`Spell.GetName()`/`GetDescription()` 各是一次查表；`SpellTable` 与 `Spell` 里「文案属于翻译层」的旧说法改成指向
新表。`spell_info.cpp` 的 `getSpellDescription` 仍留（要 `HeroBase` 与地图）。`spell` 统计仍 1/3/0。Game
2697 → **2714**，全解决方案 3 507 → **3 524** 全绿。

2026-11-19：怪物信息屏的三支文案（ADR-173）：`MonsterDescriptions` 转出 `getMonsterAbilityDescription`
（33 个分支，其中五支特例施法句、`%{spell}` 就地替换成 `SpellText` 的名字）、`getMonsterWeaknessDescription`
与 `getMonsterPropertiesDescription`（按百分比分组合并、`replaceMassSpells` 去重、闪电箭印作 `Lightning`），
`MonsterAbilities.IsBasic` 补上「形状类能力」那条线。同文件的 `getMonsterDescription` 是英文标签的调试转储，
明确不转。`monster` 统计仍 0/2/1（`monster_info.cpp` 里还剩战斗侧两张表）。Game 2714 → **2766**，全解决方案
3 524 → **3 576** 全绿。

2026-11-19：怪物信息屏的排版与绘制（ADR-174）：`dialog_armyinfo.cpp` 拆成三件——`ArmyInfoLayout`（窗口/
阴影/`pos_rt` 三个矩形加属性块、名字、描述、数量、三个按钮的全部偏移，参考的匿名 namespace 换成常量）、
`ArmyInfoArtwork`（`VIEWARMY`/`VIEWARME` 的 0 与 7 两帧加三个按钮的两帧）、`ArmyInfoDialog`（属性行文本、
描述块的两形状、`Draw` 与 `CreateButtons`）。顺带补上它们的前置：`Speed`/`Luck`/`Morale` 的 `String`
（`GetName`）、`Troop` 的四个属性字符串、`monster_info.cpp` 的 `icnId` 表（`MonsterIcnIds.GetSpriteIcn`，
66 行 switch）。Game 2766 → **2788**，UI 195 → **209**，全解决方案 3 576 → **3 613** 全绿。

2026-11-19：法师塔窗口的排版与绘制（ADR-175）：`castle_mageguild.cpp` 的窗口拆成四件 —— `MageGuildLayout`
（640×480 居中、底栏 `y = 461`、三段分栏与状态栏 ROI）、`MageGuildArtwork`（`STONEBAK(_EVIL)`、`SMALLBAR` 五帧、
`TOWNWIND` 两帧、六种族 `MAGEGLD*`、退出按钮）、`SpellScrollRow`（一行卷轴的 110 步长、折叠下移 7、图标与名字、
命中与当前法术）、`MageGuildDialog`（五行锚点 `365 - 90 * i`、建筑按已绘包围盒横向居中而按精灵高度站基线、裁剪
到左侧 200 列、底栏两段源列、三个按钮、状态行）。顺带补 `ImageOps.GetActiveRoi`（只读 transform 层）与
`ImageOps.BlitSpriteClipped`（源区左上角落到目标点再裁剪）。事件循环与法术说明对话框仍未转。Core 200 →
**209**，UI 209 → **234**，全解决方案 3 613 → **3 647** 全绿。

2026-11-19：水井窗口的数据层与绘制（ADR-176）：`castle_well.cpp` 拆成四件 —— `CastleWellDwelling` +
`Castle.GetWellDwellings()`/`GetWellGrowth()`（六行：建没建、库存、周产含井与魔法泉、生物取自已建住宅）、
`WellLayout`（640×480 居中、底栏 `y = 461`、两列三行住宅格与七类锚点、两段拼接的源列、x = 639 的填黑列）、
`WellArtwork`（`WELLBKG(_EVIL)`、`SMALLBAR`、MAX 与退出按钮、种族 `CSTL*` 的第 19–30 帧共 12 帧）、
`WellDialog`（背景与底栏、标题、九行统计文案用空串表示跳行、`Available:` 黄字、按钮与 tooltip、点击命中只认已建
住宅）。保留了参考的两处不一致（生物取升级住宅而成长行取基础住宅位）。Game 2788 → **2794**，UI 234 → **272**，
全解决方案 3 647 → **3 691** 全绿。

2026-11-19：输入状态机与按钮组的事件驱动（ADR-177）：`engine/localevent.cpp` 只转状态机那一半 ——
`UI/Input/Key.cs`（`Key` 枚举逐值照抄 + `KeyModifier` 位）、`UI/Input/LocalEvent.cs`（八个动作状态位、三键位置、
滚轮、850 ms 长按、拖拽、三个钩子与全部查询，`BeginFrame` 对应 `HandleEvents` 开头的清理）、
`UI/Input/KeyText.cs`（`getCharacterFromPressedKey` 与 `InsertKeySym`，Shift/Caps 互消、小键盘 Num Lock），
`ButtonGroup.ProcessEvents( LocalEvent, ButtonHotKey? )` 接上参考的 `processEvents()`（按下外观、点击、热键后
`reset`）。`HandleEvents` 本体、手柄、双指触摸、`KeySymGetName` 仍未转。UI 272 → **316**，全解决方案 3 691 →
**3 735** 全绿。

2026-11-19：三个窗口的消息循环（ADR-178）：`WellDialog.ProcessEvents`、`MageGuildDialog.ProcessEvents` 与
`ArmyInfoDialog.ProcessEvents` 把参考的 `while ( le.HandleEvents() )` 折成「一帧一次、返回要做什么」的纯函数
（`WellDialogFrame`/`MageGuildFrame`/`ArmyInfoFrame`），顺序与短路照抄（EXIT → MAX/箭头 → 六格或五行 → tooltip；
升级 → 解散 → 退出 → 三个 tooltip），点击的消费语义不变；购买、招募、法术说明框与军队信息的三个消息框留给调用方。
热键以 `WellHotKeys`/`MageGuildHotKeys`/`ArmyInfoHotKeys` 传入；`MageGuildDialogAction` 补 `None`/`ShowSpell`，
`ArmyInfoAction` 以 `None` 打头；可选的升级/解散按钮用 `ButtonSprite?` 复现 `std::unique_ptr` 的守卫；
`ButtonBase.UpdatePressState` 把「按住且在区内」的实参逻辑提成按钮方法，`ButtonGroup.ProcessEvents` 也改用它。
同批补上 `Widgets/TimedEventValidator`（参考 `gui/ui_tool.h:227-259`，首按 500 ms + 之后每 100 ms 的连发）并接进
魔法塔的两支箭头：参考用 `ActionObject` 订阅回调重置计时器，端口把「这一帧是否按住」作为参数传入，由计时器自己
检测状态变化；两个参数可空，不传就只答点击与热键。UI 316 → **358**，全解决方案 3 735 → **3 777** 全绿。



### 剩余工作量（按行数，2026-11-19）

文件计数不适合估工作量：`dialog` 那 23 个未转文件合计 10 404 行，而 `battle_interface.cpp` 一个就 7 538 行，比
`army` 整个目录还大。这一节按参考 `.cpp` 的行数排一遍，口径只数「整文件未转」与「部分文件里点明的缺口」，排除
ADR 里写明不适用或不移植的（`map_format_helper.cpp` 2 085 + `map_format_info.cpp` 853 走 MP2 用不到，`dir.cpp`、
`tinyconfig.cpp` 与翻译层按 ADR-139 不移植）。

| 工作包 | 参考（行数） | 规模 | 前置 / 备注 |
| --- | --- | --- | --- |
| 战斗界面与结算 | `battle_interface` 7 538、`battle_dialogs` 1 220、`battle_only` 843、`battle_main` 507、`battle_interface_settings` 334、`battle_action` 剩五支 ≈1 200、`battle_arena.Turns`/`battle_board`/`battle_army` 的尾巴 | ≈12 000 | 单位、寻路、桥/塔/投石车/墓与动作循环已就绪，缺的是「画」与「结算」 |
| AI | 9 个 `.cpp`：`ai_planner_hero` 3 309、`ai_hero_action` 2 504、`ai_battle` 2 152、`ai_battle_spell` 984、`ai_planner_kingdom` 987、`ai_planner_castle` 536、`ai_common` 525 … | 11 230 | 契约已落（ADR-161），要完整战力、英雄动作与战斗决策 |
| 对话框实现 | 23 个：`selectitems` 2 220、`quickinfo` 967、`selectfile` 753、`recruit` 694、`marketplace` 685、`selectcount` 652、`thievesguild` 486、`hotkeys` 484 … | 10 404 | 全部卡在同一层：`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/`OptionButtonGroup`/`TextBox` |
| `game` 会话层 | 22 个：`game_startgame` 1 735、`game_campaign` 1 710、`game_credits` 817、`game_over` 702、`game_auto_playtest` 698、`game_hotkeys` 597、`game.cpp` 502、`highscores` 476+378 … | 10 141 | 场景循环的宿主，窗口最终要接进这里 |
| `editor` 窗口 | 13 个：`editor_map_specs_window` 2 518、`editor_castle_details_window` 1 065、`event_details` 504、`save_map` 487、`sphinx` 471、`spell_selection` 436、`options` 379、`daily_event_spec` 374 … | 7 978 | `EditorScene`/`EditorPanel`/`EditorInterface` 已有骨架 |
| `gui` 剩余 | 14 个：`ui_keyboard` 1 360、`interface_status` 524、`player_info` 462、`skill_bar` 458、`ui_map_object` 366、`ui_castle` 324、`interface_focus` 287、`ui_campaign` 218 … | 4 604 | 软键盘最重，其余是面板与状态栏 |
| `engine` 剩余 | 10 个：`image_color_conversion` 1 369、`audio_xmi2mid` 692、`system` 615、`smk_decoder` 325、`core` 194、`dir` 190 … | 4 039 | `serialize` 的流层已转（ADR-181）；`dir`/`tinyconfig` 明确不移植 |
| 城堡屏 / 城镇屏 / 王国屏 | `castle_dialog` 859、`castle_town` 772、`kingdom_overview` 959、`view_world` 795、`castle_building`/`buildinginfo` 的 blit | ≈3 400 | 规则部分都已有，缺绘制与事件循环 |
| `heroes` 剩余 | `heroes_dialog` 831、`heroes_meeting` 869、`heroes_base` 622、`heroes_spell` 421、`heroes_indicator` 333 | 3 076 | 另加 `heroes_action` 的军队/战斗分支 |
| 随机地图 | `map_random_generator` 982 + `_helper` 1 236 + `_info` 237 | 2 455 | 与编辑器无关，可独立做 |
| `campaign` | `campaign_data` 971、`campaign_scenariodata` 831、`campaign_savedata` 270 | 2 072 | 要战役数据与对话框 |
| `army` 的两个界面文件 | `army_bar` 741、`army_ui_helper` 123 | 864 | `Army`/`Troop` 规则已全 |
| `monster_anim` | — | 190 | 随战斗批次一起做 |

合计 **≈ 7.5 万行**，是 249 个参考 `.cpp`（≈18.3 万行）的四成。近期批量每批 800~1 500 行（ADR-174~178 是「一个
窗口 + 测试」一批），按这个速度还要 **60~70 批**；「对话框控件层」与「战斗界面」两块各自就要吃掉十批以上。

还有一类不计入上表的缺口 —— 部分文件里没转的那几支，往往比一个整文件还大：`map_object_info.cpp` 的拾取与信息框
（文件 6 295 行，表已全、缺的几支是拾取、宝物说明与右击）、`game_assets.cpp` 的 `getXxx` 族（5 920 行）、
`ui_font.cpp` 的代码页补充（7 263 行里剩一小段）、`interface_gamearea` 的 AUTO 组合与动画、`statusbar` 的滚动文字、
`spell_book`/`spell_info`/`artifact` 的文案、`heroes_action.cpp` 的军队/战斗分支、`army.cpp` 的 `GetCurrentUnit`/
`GetControl`/序列化。

### `engine`（26）

* ✅ `agg_file.cpp`→`AssetArchive`/`AggFileNameHash`；`h2d_file.cpp`→`H2DArchive`/`H2DWriter`/`H2DImageCodec`
  （ADR-141）；`image_palette.cpp`→`Palette`；
  `image_tool.cpp`→`IcnSpriteDecoder`+`PngWriter`/`PngReader`+`BmpWriter`+`MonoCursorFile`+TIL 负载读取；
  `logging.cpp`→`GameLog`；`pal.cpp`→`PaletteTable`；`rand.cpp`→`Pcg32` + `Rand` 门面（门面在
  ADR-070；`combineSeedWithValueHash` 在 ADR-113）；`timing.cpp`→`FrameTimer`
* ◐ `image.cpp`（`Blit`/`Copy`/`Fill`/`ApplyTransform`/`Resize`/`CreateContour`/`Dithering` 已转，
  带源区与裁剪的 `Blit( in, inPos, inSize, out, outPos )`→`ImageOps.BlitSpriteClipped` 与 `GetActiveROI` 在 ADR-175，
  `SubpixelResize` 与重采样分支未）；`screen.cpp`（只转了呈现层：`PresentationViewport` + `SdlPresenter`）；
  `localevent.cpp`（**状态机**已转：`UI/Input/Key`、`UI/Input/LocalEvent`、`UI/Input/KeyText`，`ButtonGroup.ProcessEvents`
  接上 `processEvents()`，ADR-177；`HandleEvents` 本体、手柄、双指触摸与 `KeySymGetName` 未转，SDL 事件→场景分派
  落在 `App/GameHost`）；`audio.cpp`（`AudioPlayer`）；
  `translations.cpp`（只 `MOFile`→`MoCatalog`，`Translation::` 其余未）；`tools.cpp`（`Trim`/`GetString`
  这类零散助力，用 BCL 替代）；`zzlib.cpp`→`Compression`（`ZipData`/`UnzipData`、`ZipStreamBuf`/`UnzipStream`
  与存档块格式，ADR-181）；`serialize.cpp`→`Core/Io` 的四个流类
  （`StreamBase`/`StreamBuf`/`RoStreamBuf`/`StreamFile`，ADR-181；这一版参考里没有 `ZStreamBuf`）
* ❌ `audio_xmi2mid.cpp`、`core.cpp`、`dir.cpp`、`image_color_conversion.cpp`、`math_tools.cpp`、
  `render_processor.cpp`、`smk_decoder.cpp`、`system.cpp`、`thread.cpp`、`tinyconfig.cpp`

### `agg`（6）

* ✅ `agg.cpp`→`AssetBundle`（资料片优先/兜底）
* ✅ `icn.cpp`→`IcnType`/`IcnFileNames`/`IcnCatalog`（`GetString`、`getAnimatedIcnIndex`、四族 ICN 的
  Captain/Building/Castle 与 `getFlagIcnId`）+ `HeroPortraitIcn`（`getHeroPortraitIcnId`）；
  `CopyICNWithPalette` 与字号派生按端口的路子挪到 `FontGlyphTable`
* ✅ `bin_info.cpp`→`MonsterAnimInfo`（821 字节记录 + 原版数据十余处修补）+ `MonsterAnimInfoCache`
  （`GetMonsterInfo` 的缓存）；`MonsterBinFileNames` 由脚本翻
* ✅ `m82.cpp`→`M82Sound`/`M82FileNames`/`M82Catalog`（`GetString`）+ `SpellSound`（`FromSpell`）+
  `TileAmbientSound`（`getAdventureMapTileSound`）
* ✅ `mus.cpp`→`MusicThemes`（`FromGround`/`FromRace`/`GetBattleRandom`）
* ✅ `xmi.cpp`→`XmiTrack`/`XmiFileNames`/`XmiCatalog`（`GetString` + `FromMUS` 十七支）

> 到 `agg` 边界为止：**播放**不在这个目录（`engine/audio.cpp` 与 `audio_xmi2mid.cpp` 仍是未转项），
> `bin_info` 的消费者（怪物信息窗的 `MINIMON` 帧号）也还没接上，见 ADR-135 的遗留。

### `dialog`（29）

* ✅ `dialog_box.cpp`→`FrameBox`；`dialog_frameborder.cpp`→`FrameBoxChrome`
* ◐ `dialog_game_settings.cpp`→`SettingsDialog`（只有 Language 一行）；`dialog_selectscenario.cpp`→
  `ScenarioSelectionDialog`（列表/缩略图子集）；`dialog_language_selection.cpp`→`LanguageSelectionDialog`
* ◐ `dialog_armyinfo.cpp`→`ArmyInfoDialog`/`ArmyInfoLayout`/`ArmyInfoArtwork`（ADR-174）：`520×256` 窗口、
  属性块三段布局（标签右对齐到冒号列、值左对齐）、名字居中、描述块两形状（逐句或拼接换行、底部对齐）、
  数量框与三个按钮的位置与绘制已转；**消息循环**在 ADR-178 补齐（`ProcessEvents`：升级/解散/退出 +
  三个 tooltip，消息框与重画计时器留给调用方）；缺 `Monster` 的动画帧（要 `monster_anim`）、战斗型的
  `Shots Left`/`Hit Points Left` 两行与 `DrawBattleStats()` 的法术 tooltip（`Troop` 还没有战斗状态）
* ◐ **24 个入口的契约**（ADR-163，只有契约、无实现）：`dialog.h` 的 24 个函数落成 20 个模型，每个带
  `Create(...)` 与结果类型（列表见下），`FromButton` 的索引映射与 `Create` 的参数校验是真行为；`DialogButton`
  补齐参考的 14 个值。`Area`/`Render`/事件与控件层（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/
  `OptionButtonGroup`/`TextBox`）未转，所以覆盖率不动。模型：`AdventureOptionsDialog`、`FileOptionsDialog`、
  `SelectFileDialog`、`QuickInfoDialog`（七个入口一个类）、`LevelUpDialog`、`GoldOrExperienceDialog`、
  `ArenaDialog`、`SelectCountDialog`、`InputStringDialog`、`ArmySplitDialog`、`RecruitDialog`、
  `DwellingInfoDialog`、`ArmyInfoDialog`（排版与绘制已接上，ADR-174）、`ArmyJoinDialog`（两个入口一个类）、
  `MarketplaceDialog`、`GiftResourcesDialog`、`BuyBoatDialog`、`ThievesGuildDialog`、`GameInfoDialog`、
  `ArtifactSetDialog`
* ❌ 上述 24 个的**实现**（各文件都要先有控件层）：`adventure`、`arena`、`artifact`、`audio`、
  `buyboat`、`chest`、`file`、`gameinfo`、`giftresources`、`graphics_settings`、`hotkeys`、
  `interface_settings`、`levelup`、`marketplace`、`quickinfo`、`random_map_generator`、`recruit`、
  `resolution`、`selectcount`、`selectfile`、`selectitems`、`system_options`、`thievesguild`

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
  `getSecondarySkillsForWitchsHut` 三支，护城河、城墙、绿洲、中立怪、漩涡、雾距离未转）；
  `game_startgame.cpp`→`AdventureSession`（`StartGame()` 的回合循环、`humanTurn()` 开头的收入与路线、
  TAB/结束回合；`AdventureMap` 的绘制与输入接在 `AdventureMapScene`/`GameHost`）；`game.cpp`→
  `Hero.Move`/`AdventureMapScene.Update`（每帧的英雄移动、日期推进、`CheckGameOver`；过场动画、
  `Game::Init` 的资源装载、自动存档与 `ShowControlPanel` 未转）
* ❌ 其余 20：`difficulty`、`fheroes2`、`game_auto_playtest`、`game_campaign`、`game_credits`、
  `game_exit`、`game_highscores`、`game_hotkeys`、`game_init`、`game_interface`、`game_intro`、
  `game_invalid_assets`、`game_io`、`game_loadgame`、`game_mainmenu_ui`、`game_over`、
  `game_string`、`game_tools`、`game_video`、`highscores`

### `gui`（33）

* ✅ `cursor.cpp`→`CursorTheme`/`CursorLayout`；`ui_button.cpp`→`Button`/`ButtonBase`/`ButtonSprite`/
  `ButtonGroup`/`ButtonArtwork`/`DialogButtonTemplate`；`ui_dialog.cpp`→`MessageBox`；
  `ui_window.cpp`→`StandardWindow`；`ui_text.cpp`→`Game.Gui.*` 全套；`ui_tool.cpp`→`MovableSprite`；
  `ui_scrollbar.cpp`→`ScrollSlider`（贴图生成）+ `ScrollBar`（索引↔像素映射）；`interface_font.cpp`→`CompositeGlyphSource`；
  `interface_list.h`→`ListBoxBase`/`ListBox<T>`（选择、翻页、滚轮、拖拽滚屏与按键，行绘制留给子类）
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
  `isCoast`/`updateFogDirection` 部分；迷雾位与 `ClearFog` 已转（ADR-079）；
  对象摘除 `removeObjectPartsByUID`、所有权旗 `setOwnershipFlag`/`updateFlag`、船 `setBoat`/
  `getBoatDirection`/`resetBoatOwnerColor`、`GetCenter` 与 `String()`（`TileOwnership`/`TileObjectParts`，
  ADR-136）已转，只剩迷雾位）；
  `maps_tiles_helper.cpp`（`updateTerrainTransitionOnTile`/`getObjectPartByActionType` 已转；
  `getFundsFromTile`/`getArtifactFromTile`/`getBarrierColorFromTile`/`doesTileContainValuableItems`/
  `setResourceOnTile`/`setBarrierColorOnTile`/`resetObjectMetadata` 已转，ADR-080；
  废矿恢复 `restoreAbandonedMine` 与按类型/按 UID 摘除 `removeMainObjectFromTile`/
  `removeObjectFromTileByType`/`removeObjectFromMapByUID` 已转（`TileRemoval`，ADR-136）；
  军队三支 `getTroopFromTile`/`updateMonstersOnTile`/`setMonsterOnTile` 已转（`Tile.GetTroop`/
  `UpdateMonsters`/`SetMonster`，ADR-137）；半径 `ClearFog` 与 `updateFogDirectionsInArea` 由
  `World.Fog`/`MapFog` 承担（ADR-131）；只剩编辑器的 `setInitialObjectInfo`/`updateObjectInfoTile`/
  `updateRandomXxx`/`placeObjectOnTile`）；
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
  ✅ `visit.cpp`+`visit.h`→`IndexObject`/`VisitScope`/`Visit`（三条寿命谓词转发 `Mp2ObjectTypes`，
  ADR-120），`Kingdom` 有了访问名册与旅行者帐篷位图
* ✅ `position.cpp`→`Game/Maps/MapPosition`：`center` 点、`GetIndex`/`SetIndex`（几何由参数带入）、
  `isPosition`（ADR-136；参考的 `MapBaseObject`/`ActionObject`/`ObjectInfo` 在端口仍是「按瓦片号」的
  `MapObject`，因为装载时还没有几何）
* ❌ `map_format_helper.cpp`、`map_format_info.cpp`（编辑器自有格式，本项目走 MP2，**不适用**）、
  `map_random_generator.cpp`、`map_random_generator_helper.cpp`、`map_random_generator_info.cpp`

### 已落契约、补实现与测试的清单（2026-11-17 起）

`ai` 与 `dialog` 两批只落了签名/模型，**实现与测试都还欠着**，按下面的顺序补：

| 目录 | 已落 | 还欠的实现 | 还欠的测试 |
| --- | --- | --- | --- |
| `ai`（ADR-161） | `Game/Ai/` 六个文件：`AiPlanner` 的十五个成员、五种数据结构、九个自由函数、四个入口、四个阈值；签名照抄参考，真行为只有数据默认值/`Reset`/`UpdateOutcome`/单例/常量 | `ai_battle.cpp`（`BattlePlanner` 的决策）、`ai_common.cpp`（九个函数）、`ai_hero_action.cpp`（四个入口）、`ai_planner_hero.cpp`/`_castle`/`_kingdom`/`_internals`；现在除上面几处以外全是 `NotImplementedException` | 每换掉一个 `NotImplementedException` 配一条行为测试。`AiContractTests` 只钉住数据结构、单例、常量与「未移植会抛」，不能替代行为测试 |
| `dialog`（ADR-163） | 20 个模型 + `DialogButton` 的 14 个位值；`Create` 的参数校验与 `FromButton` 的按钮映射是真行为 | 24 个入口的 `Area`/`Render`/事件；前置是控件层 `DialogElement`/`TextBase`/`Slider`/`OptionButtonGroup`/`TextBox`（`ScrollBar` 与 `ListBox` 已就位）；另欠 `SelectFile` 的列表接线、`SelectCount` 的上下控件、各对话框的屏幕偏移 | 模型层已有 25 条（`DialogModelsTests`）。渲染与交互要等控件层，届时按基线图（`tools/Verify*Baseline.ps1`）补像素与点击测试 |

这两张表在实现批次完成后删除并把内容并回各自目录的条目；在此之前，`ai` 与 `dialog` 在覆盖率表里继续按「无整篇落地」计。

### 其余目录

* `world`（5）：◐ `world.cpp` 搬了骨架与通行重算——`World` 的 `w()`/`h()`、瓦片表与两个 `getTile`、
  `generateUninitializedMap`、`Reset`、`ResetPathfinder` 占位（ADR-065）、`updatePassabilities`（ADR-066）、
  对象查询与石阵/漩涡（ADR-069/070）、地图对象与每日事件/传闻容器（ADR-074/075）、
  日期（`_day`/`_week`/`_month`、派生显示值、`DateString`、`NewDay` 的时钟与剪枝，ADR-076）、
  `CountObeliskOnMaps`（ADR-077）与俘获对象（`map_captureobj`、`CaptureObject`/`CountCapturedMines`/
  `ResetCapturedObjects`，ADR-078）；**城堡表**（`AllCastles`、`getCastle`/`getCastleEntrance`/
  `isValidCastleEntrance` 与 `DoesCastleHaveSeaAccess`，ADR-110）与**王国表**（`Kingdoms` 七槽 +
  `CreateMapKingdoms`/`AttachMapRosters`，ADR-111）已转；**周**（`MapSeed`/`WeekSeed`/`WeekType` 与
  `GetEventsDate`，ADR-114）已接，`NewDay` 里的周推进也把周交给城表（ADR-115）；**周与月**（`NewWeek` 的招募池
  重置与周命对象刷新、`NewMonth` 的怪物月、`NewDay` 的 `++_week`/`++_month`，ADR-160）与**埋宝**
  （`PlaceUltimateArtifact`）已转，只打一场的 2×2 世界（`GenerateBattleOnlyMap`/`SetUniformTerrain`）与
  法文地图的字符修复（`FixFrenchCharactersInStrings`）同批落地；**区域图**已转（`MapRegion`/`MapRegionNode`
  与 `ComputeStaticAnalysis`，ADR-116，`PostLoad` 末尾调用）；静态分析另外两项统计（`WaterPercentage`/
  `LandRoughness`）同批落地；**传闻池**已实现（`World.Rumors.cs`：`StandardRumorCount`、九条内置文案、九个
  方向词、`TotalRumorCount`，以及 `GetCurrentRumor`/`ChooseRumorId`/`GetRegionName`/`Describe` 与
  `UltimateArtifact.Name`，ADR-169/170）；
  ◐ `world_loadmap` 搬了瓦片循环、add-on 链、`fixMP2MapTileObjectType`、`MP2::OBJ_HERO` 与
  `MP2::OBJ_JAIL` 的信息块建人（ADR-067/072/073）、`MP2::OBJ_SIGN`/`OBJ_BOTTLE`/`OBJ_EVENT` 的
  对象装载（ADR-074）、`MP2::OBJ_SPHINX` 与无瓦片的每日事件/传闻块（ADR-075）与 `PostLoad` 的
  可运行子集；**城堡表落格 + 两种城堡块 + 随机城重绘 + 默认城名**也已接（ADR-110），**可占领对象表**
  （锯木厂/炼金实验室/五矿/灯塔/龙城/废矿）随 ADR-160 落地，AI 区域未转；`PostLoad` 的石阵/漩涡/
  法师之眼缓存另在 `World` 上落地（ADR-069），读取端 `GetTeleportEndPoints`/`GetWhirlpoolEndPoints`/
  `NextTeleport`/`NextWhirlpool` 已接（ADR-070）；AI 区域地图的「种子」也就位了（`PostLoad` 调
  `ComputeStaticAnalysis`）；
  ◐ `world_object_uid` 的计数器改挂在 `World` 上（`LastObjectUid`/`GetNewObjectUid`，ADR-067）；
  ◐ `world_pathfinding` 的**两侧**都已转：`WorldPathfinder` 抽象基类包住缓存、标签修正式搜索、
  「最后一格」规则、船的对角切角与怪物「只到不穿」（ADR-091），`PlayerWorldPathfinder` 是五元组
  缓存 + `BuildPath` 反向回溯（ADR-092），`AIWorldPathfinder` 是 AI 的成本/通行判定、三份节点缓存、
  传送播种与 `BuildPath` 裁剪（ADR-164）；入口挂 `World.Pathfinder`/`World.AiPathfinder`，
  `ResetPathfinder()`/`LoadMap` 都会丢缓存；只剩 `ObjectValueStorage`（要对象价值表）；
  ✔ `world_regions`（`MapRegion`/`MapRegionNode`/`ComputeStaticAnalysis` 与两项地形统计，ADR-116）
* `ai`（9）：◐ **只有契约**（ADR-161）—— `ai_planner.h` 的公开半（`AiPlanner` 的十五个成员、
  `RegionStats`/`AiCastle`/`BudgetEntry`/`EnemyArmy`/`PriorityTask`）、`ai_battle.h` 的四个结构与
  `BattlePlanner`、`ai_common.h` 的九个自由函数、`ai_hero_action.h` 的四个入口、`ai_planner_internals.h`
  的四个常量都落在 `Game/Ai/`，签名照抄、行为未转（抛 `NotImplementedException`），真行为只落数据结构的默认值、
  `BudgetEntry.Reset`、`SpellcastOutcome.UpdateOutcome` 与单例；`ai_personality.cpp` 的枚举与随机/取名在
  `System/AiPersonality.cs`；`ai_battle.cpp`/`ai_common.cpp`/`ai_hero_action.cpp`/`ai_planner*.cpp` 的实现在
  `ai` 的下一批；`AIWorldPathfinderStateRestorer` 的两个开关现在有地方挂了（`AIWorldPathfinder` 已落），
  本轮未做。
* `campaign`（3）：**全部未转**——它要的多半是 `dialog` 与 `Maps` 的战役数据，等 `dialog` 控件层落地再看。
* `battle`（18）：◐ 已起步（ADR-147）—— `battle.h` 的 `Result` 与胜负位（`BattleOutcome`/`BattleResult`）、
  两个方向环（`CellDirection`/`AttackDirection` 与其环运算）、`battle_board.cpp` 的**纯几何**（11×9 偏移
  六角格、两格之间的方向、六向距离、城堡/墙外/护城河三张表、宽单位的环与半径环）落在 `Game.Battle`；
  ✅ `battle_animation.cpp`→`RandomizedDelay`/`AnimationSequence`/`AnimationReference`/`AnimationState`
  （33 支 `.BIN` 动画拼序列、无 STATIC 兜底 `[1]`、吐息当远程、多状态拼接与帧偏移，ADR-148）与底层
  `fheroes2::TimeDelay`；✅ `battle_command.cpp`→`BattleCommand`（参数倒着存、`GetNextValue` 从尾部弹、
  `UpdatePcg32Stream` 的攻击只混移动格与双方 UID）；◐ `battle_cell.cpp` 的 `BattleCell` 几何（格位、行距 42、
  六边形八切片与三角判定）+ 格上单位与 `IsPassable`/`IsPassableFromAdjacent`/`IsPassableForUnit`；
  ✅ `battle_pathfinding.cpp`→`BattlePathfinder`（`BattleNodeIndex`/`BattlePathNode` 与走法图、飞行一次落地、
  护城河 65535 的罚款记在「迈出护城河」那一步、宽单位原地转身不计步、五项缓存键加棋盘通行性快照，ADR-154），
  `Position` 除 `GetReachable`（要竞技场问移动力）外已全；
  ✅ `battle_troop.cpp`→`BattleUnit`（`BattleMonsterState` 标志、`BattleModesAffected`、位置/兵力/血量/模式/速度/
  能力判定、`GetAttack`/`GetDefense`（含军团指挥官的攻击/防御，即 `ArmyTroop` 那一项）、`CalculateDamageUnit`、
  `GetDamage`、`ApplyDamage`/`Resurrect`、`NewTurn`、`PostKilledAction`、`GetMorale`/`GetLuck` 与掷骰，以及法术
  应用层 `AllowApplySpell`/`ApplySpell`/`GetMagicResist`/`CalculateSpellDamage`/`GetCurrentSpellEffects` 与
  `TargetInfo`→`BattleTargetInfo`；竞技场那侧的护城河/城墙射击惩罚/箭术/反向颜色/敌方骨龙由 `IBattleField` 注入，
  `GetControl` 随同留待竞技场）；✅ `battle_grave.cpp`→`BattleGraveyard`
  （格子→尸体链、宽单位压两格、把尸体挪到最上面）与 `battle_bridge.cpp`→`BattleBridge`（六格常量、`NeedDown`
  的六个落点分支、`AllowUp` 的占用判据、闸门格上的 0/1 通行性、断桥终态；动画归界面）；◐ `battle_army.cpp` 转了
  `Units`（`BattleUnits`：按 UID/模式查找、稳定降序的战斗速度排序、三个筛选副本）与 `Force`（`BattleTroopsUidGenerator`、
  按阵形摆位、伤亡/亡灵/经验统计、投降价、`SyncOriginalArmy`、`NewTurn`），还差 `GetKilledTroops`（要 `Troops` 容器）
  与 `Force` 继承来的两个「撤退/投降过」标记（归 `battle_result`）；
  ✅ `battle_catapult.cpp`→`BattleCatapult`（弹道术定射数与双倍概率/命中，`GetAllowedTargets` 八项、目标优先级
  墙→塔→桥→主堡、掷 1..20 需 ≥6 命中）与 `battle_tower.cpp`→`BattleTower`（箭手数 `min(CountBuildings,20)`、
  炮塔折半但血量不折、攻击加成 = 法师塔等级、`GetInfo` 的三段描述与「已摧毁」、摧毁炮塔把 19/85 变为障碍），
  `CastleDefenseStructure`/`BattleTowerType` 两个枚举同批；顺带补上 `Board::GetMoatInfo`、城堡描述里塔与护城河
  两段，并把 `GetGarrisonStrength` 原先缺的塔强度项接上（ADR-155）；◐ `battle_arena.cpp`→`BattleArena`（三件套：
  野战/围城两套装配、`IsTown` 与 `Castle` 分开、九个格号、`FromTile` 的独立种子铺障碍、`IBattleField` 的几问、
  `IsShootingPenalty` 的豁免顺序与穿墙采样、结构耐久与四类摧毁、地震目标与伤害区间；`Turns` 未转），
  `BattleBoardCells` 补 `SetCovrObjects`/`SetCobjObjects`，`SiegeWeaponType` 同批（ADR-156）；◐ `battle_action.cpp`
  →`BattleArena.Action`（`checkMoveParams`、`moveUnit` 的步行/飞行两支、`ApplyAction` 分发器与 `MOVE`/`SKIP`/`MORALE`，
  文件其余部分显式抛 `NotSupportedException`；同批补 `Position::GetReachable`、`Unit::UpdateDirection` 与
  `IBattleField.AttackingArmyColor`，ADR-157）。剩余 5 份未转：`battle_action` 的攻击/法术/塔/投石车/撤退/投降
  与回合循环。下一批进攻击与伤害结算（`calculateAttackTarget`、反击、`TargetsApplyDamage`），之后是法术、AI 与结算
* `h2d`（1）：✅ `h2d.cpp`→`Game.H2D.H2DResources`（68 个文件名的样本表、开档三处失败、`Current` 门面，
  ADR-141）；读取本身在 `Assets.H2D`（`H2DArchive`/`H2DWriter`/`H2DImageCodec`，即 `engine/h2d_file.cpp`）。
  `Settings::findFile` 的文件搜索没搬，路径由组合根给出（`GetDefaultPath`）
* `audio`（1）：✅ `audio_manager.cpp`→`Audio.AudioManager`（WAV 头包装与两级缓存、音乐的三级回退、
  `PlayCurrentMusic` 重放、`StopSounds`/`ResetAudio`，ADR-140）；`AsyncSoundManager` 的工作线程换成每帧一次的
  `Update()`（`AudioTaskQueue`：音乐 → 音效 FIFO → 循环音效，取出即出队），循环音效的配对与复用落在
  `AudioLoopEffects.Plan`（角度优先、距离破平、并列取后者，3D 开关翻转时全清），设备与归档读取抽成
  `IAudioDevice`/`IAudioSource`，`MusicRestorer` 是作用域重放；真正对着 SDL3_mixer 的实现仍在
  `App/Audio/AudioPlayer.cs`，未接到这一层
* `castle`（11）：✔ `castle_building_info.cpp`→`CastleBuildingInfo`（面积、名字、描述、前置条件文字、
  图标序号、绘制优先级，六族三张大表由脚本从参考 `.cpp` 生成，ADR-134）、✔ `captain.cpp`→`Captain`
  （主技能、法术点、只学 1–2 级，并作为 `IArmyCommander` 接管守军的士气/幸运/力量修正，ADR-134/145）、
  ✔ `mageguild.cpp`→`MageGuild`（布局 `[3,3,2,2,1]`、图书馆各层 +1、必有伤害/取消法术、冒险法术每层一个，
  ADR-134）；◐ `castle.h` 的 `BuildingType` 位掩码已转（`Game.Castle.BuildingType`，33 项含三个组合掩码，
  ADR-106），`AllCastles` 这个城表也已转（含 `FirstCastle`，ADR-110/145）；◐ `castle.cpp`→`Game.Castle.Castle`
  已转：构造 + `_postLoad()`、`isBuild`/`isCastle`/`isBuildingDisabled`/三个种族 `Spec` 谓词、
  `GetLevelMageGuild`、`CountBuildings`、`GetBuildingValue`、`GetActualDwelling`/`GetUpgradeBuilding`/
  `GetMonstersInDwelling`/`IsExactBuildingBuilt`、`CheckBuyBuilding`/`AllowBuyBuilding`/`GetAllBuildingStatus`、
  `BuyBuilding`（含购买后的训练）、`ActionNewDay`/`ActionNewWeek`、`AddBuildings`、`SetDefaultBuildings`、
  `ChangeColor`、招募的 `GetRecruitLimit`/`RecruitMonster`、70 字节块的 `LoadFromMP2` + `_postLoad()`、
  默认城名表、周产、`GetMoraleModificator`/`GetLuckModificator`/`GetPowerModificator`、`GetAvailableArmy`/
  `GetArmyRecruitmentValue`/`GetVisitValue`/`GetGarrisonStrength`（含客将合军）、`AllowBuyHero`/`RecruitHero`、
  `TrainHeroInMageGuild`/`TrainGuestHeroAndCaptainInMageGuild`、`GetDescriptionBuilding`（ADR-134）、
  `IsPosition`/`IsControlAi`、船坞四方法（`AllowBuyBoat`/`BuyBoat`/`HasBoatNearby`/`GetTileIndexToPlaceBoat`）、
  `ActionPreBattle`/`ActionAfterBattle`、`GetICNBuilding`/`GetICNBoat`（ADR-145）；
  剩余缺口写明白：塔的守城强度（`Battle::Tower`）、`ActionNewMonth` 的维护费与税收、`DrawImageCastle`
  （城堡屏）、`ActionPreBattle` 的 AI 分支（`AI::Planner::CastlePreBattle`）；
  ◐ `buildinginfo.cpp` 的**数据层**已转：`BuildingInfo`（四个改写、状态、成本/名字/描述/矩形、
  `getBuildingDescription` 的住所分支）、`DwellingsBar`（六项、布局、命中、周产、左右键意图），红黑绘制与
  招募/购买弹窗未转（ADR-165）；◐ `castle_building.cpp` 的**非绘制部分**已转：`FadeBuilding` 的 9 步淡入
  （帧钟当参数）、`BuildingsRenderQueue`（绘制优先级 + 屏幕偏移）、`CastleBuildingRender` 的三个判定
  （连接件、是否建完、这栋画不画，含升级替代基础建筑的淡入例外），blit 未转（ADR-166）；
  ◐ `castle_mageguild.cpp`/`castle_well.cpp` 的**规则部分**已转：`Castle.GetMageGuildSpellRows` 的五层行
  （槽位问种族、法术问种族+图书馆）、`UI/Dialogs/MageGuildDialog`（行偏移、城镇切换、两句状态栏）、
  `CastleMaxRecruitment`（强到弱分配、三态拒绝文案、汇总行），绘制循环未转（ADR-167）；
  魔法塔的**绘制与布局**在 ADR-175 补齐：`MageGuildLayout`/`MageGuildArtwork`/`SpellScrollRow` 与
  `MageGuildDialog.Draw`（建筑居中与裁剪、底栏两段、三个按钮）；**消息循环**在 ADR-178 补齐
  （`ProcessEvents`：EXIT/两城/五行法术/tooltip，两支箭头按住连发经 `Widgets/TimedEventValidator`）；法术说明框未转；
  `castle_well.cpp` 的**数据与绘制**在 ADR-176 补齐：`CastleWellDwelling`/`GetWellDwellings()`、
  `WellLayout`/`WellArtwork` 与 `WellDialog`（六格住宅、九行统计、`Available:`、底栏两段与两个按钮）；
  **消息循环**在 ADR-178 补齐（`ProcessEvents`：EXIT/MAX/六格/tooltip），只剩生物动画；
  ◐ `castle_dialog.cpp`/`castle_town.cpp` 的**文字与返回值**已转：`BuildingInfo.GetStatusLine`（住所
  `"Recruit {复数小写}"`、其余建筑报名字、随机城镇退化到等级）、`UI/Dialogs/CastleDialog`（日期行、八种返回值）、
  `UI/Dialogs/RecruitHeroDialog`（招募英雄的句子、魔法书不计宝物、价格），绘制与事件循环未转（ADR-168）；
  ◐ `castle_tavern.cpp` 的**文字**已转：`UI/Dialogs/TavernDialog`（标题、开场句、空白行、传闻及其语言标签，
  另可从世界直接取本周传闻），动画与布局未转（ADR-169/170）；**castle 目录至此没有完全未转的文件**，剩下的都是 blit 与事件循环，端口 UI 层还没有
  城堡屏
* `army`（4）：◐ `army.cpp`→`Army`（五槽、取用、合并、清空、`Reset` 与初始兵数、
  `AllTroopsAreTheSame`/`AllTroopsAreUndead`/`AreAllTroopsUnique`、士气修正、含英雄箭术/攻防/魔法战略值的 `GetStrength`，
  ADR-095/099/100/102；`Army.Troops.cs` 的容器半边：`GetOptimized`/`MergeSameMonsterOnce`/
  `MergeSameMonsterTroops`/`SortStrongest`/`GetWeakestTroop`/`GetStrongestTroop`/`GetCountMonsters`/
  `GetTroopsStrength`/`GetReinforcementValue`/`IsStrongerThan`/`IsMeleeDominantArmy`/
  `GetStrongestMonster`/`ResetInvalidMonsters`/`UpgradeMonsters`/`Assign`，ADR-103/105/109；`Army.Transfer.cs` 的两军
  调兵：`JoinStrongest`/`JoinStrongestFromArmy`/`MoveTroops`/两个 `SwapTroops`/`SaveLastTroop`，
  `IArmyCommander.IsHeroes`，ADR-104）、
  `army_troop.cpp`→`Troop`（数值、血伤造价、种族/亡灵/士气/飞行四条能力查询、`GetStrength*`、
  `IsAllowUpgrade`/`GetUpgrade`/`Upgrade`/`GetUpgradeCost*`/`GetDwelling`，ADR-095/099/100/103/105/108；
  `UpgradeTroops` 已转，ADR-108；信息屏的四个属性字符串 `GetAttackString`/`GetDefenseString`/
  `GetShotString`/`GetSpeedString` 与 `GetAttack`/`GetDefense`/`GetShots`，ADR-174）；`Army.Size.cs`/
  `Army.Split.cs`/`Army.JoinSolution.cs`/
  `Army.Arrangement.cs` 补齐最后一批规则：九档兵力档位与两条文案表、分兵与合并七件套、
  `GetJoinSolution` 的四种判定、`SetFromTile` 的九种固定守卫与三个 `ArrangeFor*`，ADR-144。
  仍缺：`TroopSizeString`/`String` 的文案、三个绘制函数、士气/幸运修正里的城堡与神器两项、
  `GetCurrentUnit`、`GetControl`、流序列化；❌ `army_bar.cpp`、`army_ui_helper.cpp`
* `spell`（4）：✅ `spell_storage.cpp`→`SpellStorage`（去重、书后追加神器上的 `ADD_SPELL`、
  `Remove`/`IsPresent`/`GetSpells`，ADR-102）；◐ `spell.cpp`→`Spell`（73 行数值表、等级、全部特性/指向/
  元素/守卫谓词、`GetStrategicValue`、`SpellPoints(bag)` 的四类神器减耗、`SuitableForSpellBook`/
  `TotalSpellsPerLevel`/`RandomSpell`，ADR-102/143），名字与描述两列在 `SpellText`
  （73 行，`Spell.GetName()`/`GetDescription()` 就是两次查表，ADR-172），只剩流序列化；
  ◐ `spell_info.cpp`→`SpellInfo`（六条数值公式与四条地图查询：最近城堡、召唤船位置、可召唤船、
  英雄是否近水，ADR-143），只剩 `getSpellDescription` 的文案拼装；
  ◐ `spell_book.cpp`→`SpellBook`（过滤、翻页、会话起始状态与 `SetFilter` 列表，ADR-143），
  绘制与输入循环留给 UI 批次
* `heroes`（12）：◐ `direction.cpp` 已转；`heroes.cpp` 转成
  「部分」：`AllHeroes::Init` / `Clear` / `GetHeroForHire`、`isAvailableForHire`、`getHeroIdRangeForRace`
  （`Game/Heroes/AllHeroes`、`HeroRaces`，ADR-071）与 `Heroes::LoadFromMP2` 的前半（位置/颜色/种族改判/
  肖像/名字/巡逻，ADR-072）已落，另有 `Hero` 的 id 取值域、种族/颜色/肖像、mode 位、`isFriends`（暂等价于
  同色，ADR-083）、瓦片占用契约（ADR-062/068/071）、经验/等级表（`HeroExperience`，ADR-090）、
  军队与移动点（ADR-095/096）、`GetLuck`/`GetMorale` 的技能项（ADR-099）与 `heroes_base` 的法术书/
  法力点/`GetMagicStrategicValue`（ADR-102）、英雄侧的访问名册两层（`LOCAL` 自己记 / `GLOBAL` 转王国）、
  每日回蓝（含神秘术加成）与日/周/战后三个清理、运气/士气按访问类型集合重算（`Hero.Visit`、`Hero.Morale`、
  `GameStatic`，ADR-122）；
  `skill.cpp` 也记「部分」：14 项枚举、数值表、种族初始技能、`SecSkills`、外交折扣已转（ADR-087~089），
  技能名（`SecondarySkills.GetName`，英文原文）与两套图标索引（`GetIndexSprite1`/`GetIndexSprite2`，
  ADR-137）也已转，只剩描述文案；`route.cpp` 也记「部分」：
  `Route::Step`→`RouteStep`（ADR-093）与 `Route::Path`→`HeroPath` 的队列语义
  （`FrontIndex`/`FrontFrom`/`FrontDirection`/`FrontPenalty`/`NextStepDirection`、`Truncate`/
  `PopFront`/`Show`/`Hide`/`IsValidForMovement`/`IsValidForTeleportation`/`GetAllowedSteps`，
  ADR-094）已落，`_hero` 指针换成 `movePoints`/`heroIndex` 参数；`heroes_move.cpp` 也记「部分」：
  九帧状态机、转向块、像素偏移、帧循环（`Hero.Movement`，ADR-097），清雾/对象动作/小地图/渲染四个
  缺失子系统走 `IHeroMovementHandler` 接缝（ADR-098）；`route.cpp` 的 `GetIndexSprite`（`ICN::ROUTE`
  六块二十四枚箭头，`HeroPath.GetIndexSprite`，ADR-138）已转，只剩 `Route::Path::String()`、行走音效与
  淡入淡出；法术书、AI 动作仍未转；`heroes_action.cpp` 搬了
  石阵那一支（王国侧的登记 + 拼图重算，`Kingdom.ActionObelisk`，ADR-121）与魔法井、泉眼、运气/士气建筑、
  黑猫、风车/水车/小屋/魔法花园、马厩、旅行者帐篷、竞技场、屏障、免费取宝、凉亭、知识树、神殿、女巫小屋、
  宝箱、仙那度、散落资源/营火/桶、骷髅、货车、浮木、沉船幸存者、监牢、塞伦、告示牌、观察塔、麦哲伦地图
  二十七支（`HeroActions`，ADR-122 至 ADR-129，用 `Kingdoms` + 英雄颜色代替 `hero.GetKingdom()`），付费取宝
  与麦哲伦地图只返回 `NeedsPayment`/`NeedsConfirmation`，其余分支还要等军队/战斗这些前置；迷雾已进
  `World.Fog`，开局侦察也已搬（`Hero.GetScoutingDistance`/`Scout`、`AllHeroes`/`AllCastles.Scout`、
  `World.ClearFog(color)`、`FogHeroMovementHandler`），冒险地图场景已改画 `World.Fog`（ADR-131）；
  `Hero.Recruit` 只差 `SAVEMP`、透明值与迷雾侦察三件事；
  ✅ `heroes_recruits.cpp`+`heroes_recruits.h`→`Recruit`/`Recruits`（酒馆那一对、投降日的替换规则，
  ADR-118），`Kingdom` 有了 `GetRecruits()` 与 `clear` 里的重置，只差参考 `GetRecruits()` 的按需补位
  （要 `Settings`/战役存档/`Players`）
* `kingdom`（14）：◐ `color.cpp`→`PlayerColor`、`race.cpp`→`MapRace`、`luck.cpp`→`Luck`、
  `morale.cpp`→`Morale`（两把七档尺子、`Normalize` 与 `String`→`GetName`，ADR-099/174，只剩
  `Description` 那段文案）；✅ `profit.cpp`→`ProfitConditions`
  （矿/建筑/神器三张日收入表，ADR-112）；✅ `payment.cpp`→`PaymentConditions`（五个固定价 + 建筑造价转发）、
  ✅ `resource_trading.cpp`→`ResourceTrading`（七个类别组合的 9 列汇率表，ADR-117）；✅ `puzzle.cpp`+`puzzle.h`→
  `Puzzle`（48 格位集、四条区带的洗牌与「先外框后中心」的揭示顺序，ADR-119；对话框与存档未转）；◐ `week.cpp`+`week.h`→
  `Week`/`WeekName`：34 个周名、周/月两条
  抽签表与十二种月怪（ADR-113）、`Week::GetName()`（34 个词，怪物周取生物单数名， plague 大写照参考，
  ADR-171）；◐ `kingdom.cpp`+`kingdom.h`：
  账本（`Treasury` 的 `ref Funds`、`AllowPayment`、`AddFundsResource`/`OddFundsResource`、起始资源表
  `GetStartingResources`）、日收入 `GetIncome`（矿井、城镇、神器三项已算，技能一项等英雄学技能，ADR-171）、
  两张名册（`AddCastle`/`RemoveCastle`/`AddHero`/`RemoveHero`、`IsLoss`/`IsLosingGame`/`HasHeroes`/
  `CanRecruitHeroes`、`LostTownDays`、五个计数）与七槽容器 `Kingdoms`（`GetKingdom`/`Init`/`Clear`/
  `AddCastles`/`AddHeroes`/`GetNotLossColors`）已转（ADR-111/112），世界装载时按文件头的颜色起王国并填名册；
  **每日入账** `ActionNewDayResourceUpdate`（首日不结算、收入 + 当日事件、AI 跳过人类专属事件、金额按金库
  钳制）已转（ADR-114）；`ActionNewDay`/`ActionNewWeek` 与访问名册（`SetVisited`/`IsVisited`×3/
  `CountVisitedObjects`、`Visit` 的三条寿命、旅行者帐篷位图、`KingdomModes`、失城倒计时、调试赠礼，
  ADR-120）也已转，石阵分支 `ActionObelisk` 顺带落地（ADR-121）；未转 `ActionNewMonth`（要 `Settings`）、
  电脑难度加成与人类折扣、招募补位（`GetRecruits()` 的刷新）、`GetCountArtifacts`、`LossPostActions`
  （端口改成可选回调，见 ADR-120）；✅ `speed.cpp`→`Speed`（十档尺子、慢/急两步与两法 spell 的
  `extraValue` 分支，ADR-137；`String`→`GetName` 十档英文，ADR-174）、✅ `experience.cpp`→`Experience`（两条文案；参考走 `_()`，端口返回原文，
  ADR-137）；❌ `kingdom_overview`、`view_world`（两者都是渲染 + 对话框）
* `monster`（3）：◐ `monster.h` 的 `MonsterType` id 枚举已转（`Game.Monsters.MonsterType`，73 项），
  `monster.cpp` 已转 ID、`MINIMON` 帧号、`GetMonsterStrength`、升级关系（`MonsterUpgrades` 的
  `GetUpgrade`/`GetDowngrade`/`IsAllowUpgrade`/`GetUpgradeCost`，ADR-105）、住所反查
  （`DwellingMonsters.GetDwelling`，ADR-108）、随机堆叠数量 `Monster::GetRNDSize`→
  `MonsterStatistics.GetRandomCount`（七级表 + 十六种特例，ADR-137）、`GetMissileICN`/`ICNMonh`
  （`MonsterIcnIds`）、随机分桶 `GetRandomUnitLevel`/`Rand`（`MonsterLevels`，10/15/27/14 四桶共 66 只）、
  `Monster( const Spell & )`（`MonsterSummons`）与 `isRandomMonster`（ADR-146），文案
  `GetName`/`GetMultiName`/`GetPluralName`/`getRandomRaceMonstersName` 也已在 `MonsterNames`（ADR-171 补了
  `GetPluralName`）；◐ `monster_info.cpp`：
  通用数值表、战斗数值表、住所映射（`DwellingMonsters`，ADR-095；住所位本身已搬进 `BuildingType`，
  ADR-106）、32 项能力表与载荷（`MonsterAbilities`，ADR-099/100）、两条强度
  （`GetMonsterBaseStrength`/`GetMonsterStrength`，ADR-100）、`weaknesses` 表（`MonsterWeaknesses`）、
  `getSpellResistance`（`MonsterSpellResistance`，ADR-146）与三支界面文案
  （`MonsterDescriptions`：能力句、弱点句、`getMonsterPropertiesDescription` 的分组合并，
  `replaceMassSpells` 一并转，ADR-173）、`icnId` 表（`MonsterIcnIds.GetSpriteIcn`，66 行 switch，
  ADR-174）已转，只剩只被战斗侧读的 `sounds` 表与英文标签的调试转储 `getMonsterDescription`
  （不面向玩家，端口以测试代替）；❌ `monster_anim`
  （`RandomMonsterAnimation` 依赖 `battle_animation.h`，随战斗批次）
* `resource`（4）：✅ `resource.cpp`→`Funds`/`Resource`/`Cost`；◐ `artifact.cpp`→`ArtifactBag`（平铺 14 格、
  魔法书置顶右移、`Count`/`IsPresent`/`ContainSpell`、两种求和、`getTotalArtifactMultipliedPercent` 两个重载、
  `getArtifactValue`、`GetFirstArtifactWith*` 与两个 `isArtifact*Present`、`assembleArtifactSetIfPossible`、
  `sortFromWorstToBest`，ADR-101/ADR-142）、`ArtifactLevels`（`Level`/`LoyaltyLevel`/`isUltimate`/
  `isPriceOfLoyaltyArtifact`）、`ArtifactValues`（`getArtifactValue`）、`ArtifactRandom`（`Rand`+`ResetArtifactStats`+
  `ExcludeArtifactFromRandom`）、`ArtifactMapSprites`（`getArtifactFromMapSpriteIndex`）、`ArtifactSets`、
  `ArtifactExchange`（`exchangeArtifacts`）与 `artifact.h` 的 id 枚举（`Game.Resources.ArtifactType`，105 项含
  `ArtifactCount`，`ArtifactTypes.IsValid`）；`Artifact` 类的名字/描述文案、`String()`/`description` 重载与
  `ArtifactsBar`（UI）未转；◐ `artifact_info.cpp`→`ArtifactTable`/`ArtifactBonuses`（105 行数值表、
  `getArtifactData`、六个 `isBonus*`/`isCurse*` 谓词，ADR-101），`ArtifactData::getDescription`/
  `getArtifactDescription` 两支文案未转；✅ `artifact_ultimate.cpp`→`UltimateArtifact`（`Set` 的无效 id 改抽终极、
  ±2/±1 偏移、`IsAt`/`MarkAsFound`/`Reset`，ADR-142），`GetPuzzleMapSurface`（拼图界面）与序列化按惯例留在
  UI/serialize 批次
* `system`（3）：✅ `players.cpp`→`System.Player`/`Players`（两种建表、`SetStartGame` 的四遍、控制者过滤、
  按色静态表、`Focus` 的两类型化引用，ADR-139）；✅ `settings.cpp`→`System.Settings`（单例、`GameOptions`/
  `EditorOptions` 全部位值、各 setter 的钳制、`IsEvilInterfaceEnabled` 的三种模式、两个 `SwitchToNext*`，
  ADR-139）；✅ `bitmodes`→`System.BitModes`（ADR-139）。两个版本头 `version.h`/`save_format_version.h`
  顺带落地（`EngineVersion`/`SaveFileFormat`），`ai_personality` 一并搬进 `System`；
  配置文件读写（`tinyconfig`）、`dir.cpp` 的文件搜索与翻译层按 ADR-139 不移植

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
   狮身人面与无瓦片的每日事件/传闻块同样已落（ADR-075）。信息块的六个分支到这里全部落地，
   城堡表也已在装载流程里落格（ADR-110：`CreateMapCastles` → 读块 → `UpdateCastleNames`），王国（资源簿 + 名册 +
   七槽容器）也已在装载时按文件头起好并填名册（ADR-111），日收入表与 `GetIncome`（ADR-112）、周/月类型与
   抽签（ADR-113）、世界的周与当日事件（`WeekType`/`GetEventsDate`）与王国的每日入账（ADR-114）、城的周产
   （`ActionNewWeek` 与 `World.NewDay` 的「月 → 周 → 日」顺序，ADR-115）、区域图与地形统计
   （`ComputeStaticAnalysis`，ADR-116）也已落，`PostLoad` 到这里已经完整（1.1.18 的 `PostLoad` 没有别的尾巴：
   带船的漩涡格被记成船对象、于是按部件查找，端口本来就是这么做的）。
   市场的价目与汇率也已落（`PaymentConditions`/`ResourceTrading`，ADR-117）。胜负条件这一段（`KingdomIsWins`/
  `KingdomIsLoss`/`CheckKingdomWins`/`CheckKingdomLoss` + 条件位掩码与其参数翻译 + 条件英雄反查）与按英雄的两个
  寻路入口（`GetDistance`/`GetPath`）也已落（ADR-158）。挖终极神器这一条链的前半（坑贴图表 +
  `DiggingForUltimateArtifact` + 世界持有那份神器）也已落（ADR-159），后半（`setUltimateArtifact` 挑格子埋下去）需要
  对象的扩展 metadata。下一步是给每日入账与胜负判定找调用方——那需要 `Game::NewDay`/`Game::CheckKingdom*` 那一层的
  会话循环（`Settings` 与 `Players`），或者按第 4 条的依赖顺序往 `kingdom/*`/`castle/*` 里走。
4. `kingdom/*` → `castle/*` → `army/*` → `battle/*` —— 顺序不能反，`battle` 依赖 `army` 与 `castle`。
5. 旁支：`engine/serialize.cpp`（存档，M8；流层与压缩块已落，ADR-181）、`smk_decoder.cpp`（过场动画）；
   `h2d_file.cpp` 与 `h2d.cpp` 已转（ADR-141，`AssetProbe h2d` 可对拍真实档），
   `audio_manager.cpp` 的数据层也已转（ADR-140），剩下的是把 `AudioPlayer` 换成
   `IAudioDevice`/`IAudioSource` 的 SDL3_mixer 实现。

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
