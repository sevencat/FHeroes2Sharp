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
| `army` | 4 | 0 | 0 | 4 |
| `audio` | 1 | 0 | 0 | 1 |
| `battle` | 18 | 0 | 0 | 18 |
| `campaign` | 3 | 0 | 0 | 3 |
| `castle` | 11 | 0 | 0 | 11 |
| `dialog` | 29 | 2 | 3 | 24 |
| `editor` | 18 | 1 | 4 | 13 |
| `game` | 28 | 2 | 3 | 23 |
| `gui` | 33 | 8 | 10 | 15 |
| `h2d` | 1 | 0 | 0 | 1 |
| `heroes` | 12 | 1 | 0 | 11 |
| `kingdom` | 14 | 0 | 2 | 12 |
| `maps` | 17 | 3 | 7 | 7 |
| `monster` | 3 | 0 | 1 | 2 |
| `resource` | 4 | 1 | 1 | 2 |
| `spell` | 4 | 0 | 0 | 4 |
| `system` | 3 | 0 | 2 | 1 |
| `world` | 5 | 0 | 1 | 4 |
| **合计** | **249** | **26** | **43** | **180** |

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

### `engine`（26）

* ✅ `agg_file.cpp`→`AssetArchive`/`AggFileNameHash`；`image_palette.cpp`→`Palette`；
  `image_tool.cpp`→`IcnSpriteDecoder`+`PngWriter`/`PngReader`+`BmpWriter`+`MonoCursorFile`+TIL 负载读取；
  `logging.cpp`→`GameLog`；`pal.cpp`→`PaletteTable`；`rand.cpp`→`Pcg32`；`timing.cpp`→`FrameTimer`
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
  `game_newgame.cpp`→`GameHost` 的 NEW GAME 流程
* ❌ 其余 23：`difficulty`、`fheroes2`、`game_auto_playtest`、`game_campaign`、`game_credits`、
  `game_exit`、`game_highscores`、`game_hotkeys`、`game_init`、`game_interface`、`game_intro`、
  `game_invalid_assets`、`game_io`、`game_loadgame`、`game_mainmenu_ui`、`game_over`、
  `game_startgame`、`game_static`、`game_string`、`game_tools`、`game_video`、`game`、`highscores`

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

* ✅ `ground.cpp`→`MapGround`；`mp2.cpp`+`mp2_helper.cpp`→`Mp2Map`/`Mp2TileInfo`/`Mp2AddonInfo`/
  `Mp2ObjectData`
* ◐ `maps.cpp`（几何部分全部已转：`Game/Maps/MapGeometry` 的 index ↔ point、`GetDirection`、
  `GetDirectionIndex`、`getDirectionPoint`、`isValidDirection`、`isValidAbs(Index|Point)`、`getAroundIndexes`，
  外加 `Game/Maps/Direction`+`Directions` 照 `direction.h/.cpp` 逐条移植；渲染侧只有
  `drawFog`→`MapFog`/`DrawFog` 与部分 `ClearFog`）；`maps_fileinfo.cpp`→
  `Mp2FileInfo`/`MapCatalog`；`maps_objects.cpp`→`Mp2ObjectPreset`/`Mp2ObjectName`（对象表子集）；
  `maps_tiles.cpp`（雾位 + `Tile` 的对象层/地形位/道路位/通行掩码/`Init`/独立通行计算/`isStream`/
  `isShadow`/`updatePassability`/`updateObjectType`/`isAnyTallObjectOnTile`/`isDetachedObject`/
  `getIndexOfMainTile` 已转，`isCoast`/`updateFogDirection` 部分；只剩英雄占用、船的、迷雾位）；
  `maps_tiles_helper.cpp`（`updateFogDirectionsInArea`、`updateTerrainTransitionOnTile` 已转）；
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
  `generateUninitializedMap`、`Reset`、`ResetPathfinder` 占位（ADR-065）、`updatePassabilities`（ADR-066）；
  英雄/城堡/王国/俘获对象/地图对象这些容器、日期与事件、区域图、静态分析都未转；
  ❌ `world_loadmap`、`world_pathfinding`、`world_object_uid`、`world_regions`
* `ai`（9）、`army`（4）、`battle`（18）、`campaign`（3）、`castle`（11）、`heroes`（12）、`spell`（4）、
  `h2d`（1）、`audio`（1）：**全部未转**
* `kingdom`（14）：◐ `color.cpp`→`PlayerColor`、`race.cpp`→`MapRace`；❌ `experience`、
  `kingdom_overview`、`kingdom`、`luck`、`morale`、`payment`、`profit`、`puzzle`、`resource_trading`、
  `speed`、`view_world`、`week`
* `monster`（3）：◐ `monster.h` 的 `MonsterType` id 枚举已转（`Game.Monsters.MonsterType`，73 项），
  `monster.cpp` 只有 ID 与 `MINIMON` 帧号；❌ `monster_anim`、`monster_info`
* `resource`（4）：✅ `resource.cpp`→`Funds`/`Resource`/`Cost`；◐ `artifact.h` 的 id 枚举已转
  （`Game.Resources.ArtifactType`，105 项含 `ArtifactCount`），类行为未转；❌ `artifact_info`、
  `artifact_ultimate`
* `system`（3）：◐ `players.cpp`→`PlayerColor`/`HumanPlayers`、`settings.cpp`（只有语言与分辨率种子）；
  ❌ `bitmodes`

### 还差的主干（按依赖顺序）

1. `maps/map_object_info.cpp` —— 拾取、信息框与 `ActionToObject` 的总闸。类型、全部 21 组表与它们依赖的
   `MonsterType` / `ArtifactType` 两个 id 枚举都已落地（表大小与枚举锚点分别钉在 `MapObjectCatalogTests`、
   `IdEnumTests`）。`Tile::getMainObjectType` 的缓存字段也已随第 3 条落地（ADR-066）。
2. `heroes/heroes_move.cpp` + `world/world_pathfinding.cpp` + `maps/visit.cpp` —— 英雄移动与路径预览，
   `MapFog` 的增量更新挂在这里。
3. `world/world.cpp`、`world_loadmap.cpp`、`world_object_uid.cpp` —— 可变世界状态，`castle`/`kingdom`/
   `army` 都依赖它。`World` 的骨架与 `updatePassabilities` 已立（ADR-065/066），`Tile` 的邻居通行也已
   接上；下一步是 `world_loadmap` 的对象表（让整条链吃上真数据）与 `Tile.setHero`/`getHero`。
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
