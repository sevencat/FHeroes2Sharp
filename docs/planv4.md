# 计划 v4：按目录整块转换

> 起于 2026-11-21。v3（`docs/planv3.md`）按「功能批次 + 逐函数核对」推进，本轮改为**按参考目录整块推进**：
> `F:\code\fheroes2\src\fheroes2\<dir>` 一个目录全部落地才进下一个。进度记在 `docs/progressv4.md`。
> 本文件只写次序与拆分；每步的实际结果、偏差与遗留由 `progressv4.md` 追加，**不改本文件的次序**。

## 1. 执行约定

1. **整目录推进**：一个目录的每个 `.cpp` 都要有落点（文件 / 类型 / 成员签名 + 文档注释），才进下一个目录。
2. **第一遍只求转完**：方法体可以抛 `NotImplementedException`，但签名、文档注释、参数校验照写。不写测试、
   不做数值核对、不追像素级行为。
3. **依赖先写接口**：待转目录依赖尚未转的目录时，先去被依赖目录写好接口（签名 + 注释），方法体照旧抛
   `NotImplementedException`。**禁止**在调用方写临时替代、兜底分支或「先按别的值算」。
4. **界面篇留在本目录**：`heroes_dialog`、`castle_tavern`、`battle_interface` 这类界面文件属于各自目录，不单独
   抽一期。它们依赖的控件与对话框地基已在端口存在（`FHeroes2.UI`），不构成阻塞。
5. **全员核对与测试放最后**：所有目录转完后统一做逐函数核对、补行为测试、对拍数值与画面。
6. **`editor/` 最后实现**。
7. **每步收口条件**：`dotnet build FHeroes2Sharp.slnx` 0 错误 0 警告；`dotnet test` 全绿（第一遍只要求既有用例
   不回归）。

## 2. 目录次序

次序按「被依赖的在前」排：数据表与资源 → 容器与实体 → 地图与世界 → 战斗 → AI → 控件 → 对话框 → 主循环 →
战役 → 编辑器。界面篇不单独排队，跟着自己的目录走。

| 阶段 | 参考目录 | cpp | 端口落点 | 小计划 | 起步状态 |
| --- | --- | --- | --- | --- | --- |
| S0 | `agg` `image` `h2d` `audio` `system` | 11 | `Assets/Agg` `Assets/Icn` `Assets/H2D` `Game/Audio` `Game/System` | 5 | 已基本完成，核对收口 |
| S1 | `monster` | 3 | `Game/Monsters` | 3 | 有落点，待收口 |
| S2 | `spell` | 4 | `Game/Spells` | 3 | 有落点，待收口 |
| S3 | `resource` | 4 | `Game/Resources` `Game/Artifacts` | 4 | 有落点，待收口 |
| S4 | `army` | 4 | `Game/Army` `UI/Widgets/ArmyBar*` | 4 | 模型已成，界面篇待收口 |
| S5 | `heroes` | 12 | `Game/Heroes` `UI/Dialogs/*Hero*` | 9 | 模型大半，动作/移动待收口 |
| S6 | `castle` | 11 | `Game/Castle` `UI/Dialogs/Castle*` | 7 | 模型大半，窗口待收口 |
| S7 | `kingdom` | 14 | `Game/Kingdom` | 6 | 模型大半，总览/世界视图待收口 |
| S8 | `maps` | 17 | `Game/Maps` `Assets/Maps` | 6 | 大半，格式与随机生成待收口 |
| S9 | `world` | 5 | `Game/World` | 4 | 大半，装载待收口 |
| S10 | `battle` | 18 | `Game/Battle` | 5 | 内核已收口，界面四篇待做 |
| S11 | `ai` | 9 | `Game/Ai` | 9 | B1–B4 已落，战斗 AI 待做 |
| S12 | `gui` | 33 | `UI/Widgets` `UI/Interface` `UI/Input` `Game/Gui` | 9 | 控件大半，界面组待收口 |
| S13 | `dialog` | 29 | `UI/Dialogs` | 6 | 十余篇已成 |
| S14 | `game` | 28 | `Game` 根、`FHeroes2.App` | 6 | 主循环/存档/菜单大半未做 |
| S15 | `campaign` | 3 | 无 | 1 | 未开始 |
| S16 | `editor` | 18 | `Game/Editor` `FHeroes2.App` | 5 | 部分，**最后做** |

`image/` 只有 `image.h`（0 个 cpp），随 S0 核对，不单独设计划。

## 3. 小计划明细

每个小计划是「若干参考文件 → 端口落点」，一次做完再进下一个。超过 2 000 行的文件单独成计划。

### S0 基线（11 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S0-1 | `agg/agg.cpp` `agg/bin_info.cpp` `agg/icn.cpp` | `Assets/Agg/AssetArchive.cs`、`Assets/Icn/*` |
| S0-2 | `agg/m82.cpp` `agg/mus.cpp` `agg/xmi.cpp` | `Assets/Audio/M82*`、`Music*`、`Xmi*` |
| S0-3 | `h2d/h2d.cpp`、`image/image.h` | `Assets/H2D/*`、`FHeroes2.Rendering` |
| S0-4 | `audio/audio_manager.cpp` | `Game/Audio/*` |
| S0-5 | `system/bitmodes.cpp` `system/players.cpp` `system/settings.cpp` | `Game/System/*` |

### S1 `monster`（3 个 cpp，约 3 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S1-1 | `monster.cpp` | `Monsters/MonsterType.cs` 及其分区文件 |
| S1-2 | `monster_info.cpp` | `MonsterDescriptions.cs`、`MonsterStatistics.cs` |
| S1-3 | `monster_anim.cpp` | `MonsterAnimInfo*.cs`、`MonsterAnimationType.cs` |

### S2 `spell`（4 个 cpp，约 5 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S2-1 | `spell.cpp`（主体 + 各法术判定，最大一篇） | `Spells/Spell.cs` 按法术类型分区 |
| S2-2 | `spell_storage.cpp` `spell_book.cpp` | `SpellStorage.cs`、`SpellBook.cs` |
| S2-3 | `spell_info.cpp` | `SpellInfo.cs`、`SpellText.cs`、`SpellTable.cs` |

### S3 `resource`（4 个 cpp，约 2 500 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S3-1 | `resource.cpp` | `Resources/Funds.cs`、`ResourceText.cs` |
| S3-2 | `artifact.cpp` | `Artifacts/ArtifactTable.cs`、`ArtifactBag.cs` |
| S3-3 | `artifact_info.cpp` | `ArtifactValue.cs`、`ArtifactBonuses.cs` |
| S3-4 | `artifact_ultimate.cpp` | `UltimateArtifact.cs` |

### S4 `army`（4 个 cpp，约 3 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S4-1 | `army.cpp` | `Army/Army*.cs` |
| S4-2 | `army_troop.cpp` | `Army/Troop.cs`、`Troop.Persistence.cs` |
| S4-3 | `army_bar.cpp` | `UI/Widgets/ArmyBar*.cs` |
| S4-4 | `army_ui_helper.cpp` | `UI/Widgets/ArmyBarArtwork.cs`、`ArmyBarSplit.cs` |

### S5 `heroes`（12 个 cpp，约 9 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S5-1 | `heroes_base.cpp` | `Heroes/Hero.cs` 基础段 |
| S5-2 | `heroes.cpp` | `Heroes/Hero.*.cs` |
| S5-3 | `skill.cpp` `direction.cpp` | `SecondarySkills.cs`、`SecSkills.cs`、`Maps/Direction.cs` |
| S5-4 | `heroes_action.cpp`（最大一篇） | `HeroActions.cs`、`HeroActions.Action.cs` |
| S5-5 | `heroes_move.cpp` | `Hero.Movement.cs`、`IHeroMovementHandler.cs` |
| S5-6 | `heroes_spell.cpp` | `Hero.Spells.cs` |
| S5-7 | `route.cpp` | `HeroPath.cs`、`RouteStep.cs` |
| S5-8 | `heroes_dialog.cpp` `heroes_indicator.cpp` | `UI/Dialogs/HeroQuickInfo*` |
| S5-9 | `heroes_meeting.cpp` `heroes_recruits.cpp` | `Heroes/Recruits.cs`、`UI/Dialogs/RecruitHero*` |

### S6 `castle`（11 个 cpp，约 9 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S6-1 | `castle.cpp` | `Castle/Castle*.cs` |
| S6-2 | `castle_building.cpp` `castle_building_info.cpp` `buildinginfo.cpp` | `CastleBuildings.cs`、`CastleBuildingInfo*.cs`、`BuildingInfo.cs` |
| S6-3 | `captain.cpp` `mageguild.cpp` | `Captain.cs`、`MageGuild.cs` |
| S6-4 | `castle_town.cpp` | `UI/Dialogs/CastleScreen*` |
| S6-5 | `castle_dialog.cpp` | `UI/Dialogs/CastleDialog*.cs` |
| S6-6 | `castle_mageguild.cpp` `castle_well.cpp` | `MageGuild*.cs`、`Well*.cs` |
| S6-7 | `castle_tavern.cpp` | `TavernDialog.cs`、`TavernView.cs` |

### S7 `kingdom`（14 个 cpp，约 8 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S7-1 | `kingdom.cpp` `color.cpp` | `Kingdom*.cs`、`Assets/Maps/PlayerColor.cs` |
| S7-2 | `payment.cpp` `profit.cpp` `resource_trading.cpp` | `Kingdom.Income.cs`、`PaymentConditions.cs`、`ProfitConditions.cs`、`ResourceTrading.cs` |
| S7-3 | `experience.cpp` `luck.cpp` `morale.cpp` `speed.cpp` `race.cpp` `week.cpp` | `Experience.cs`、`Luck.cs`、`Morale.cs`、`Speed.cs`、`Week*.cs` |
| S7-4 | `puzzle.cpp` | `Puzzle.cs`、`Puzzle.Persistence.cs` |
| S7-5 | `kingdom_overview.cpp` | `UI/Dialogs/KingdomOverview*`（新建） |
| S7-6 | `view_world.cpp` | `Kingdom/ViewWorld*.cs`（已落大半） |

### S8 `maps`（17 个 cpp，约 12 000 行，最大目录）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S8-1 | `maps.cpp` `position.cpp` `visit.cpp` `ground.cpp` | `Maps/MapGeometry.cs`、`MapPosition.cs`、`Visit.cs`、`GameStatic.cs` |
| S8-2 | `maps_tiles.cpp` `maps_tiles_helper.cpp` `maps_tiles_render.cpp` | `Tile*.cs`、`MapGround.cs` |
| S8-3 | `maps_objects.cpp` `map_object_info.cpp` | `MapObject*.cs`、`ObjectInfo.cs` |
| S8-4 | `mp2.cpp` `mp2_helper.cpp` | `Assets/Maps/Mp2*`、`Maps/MapObjectCatalog*` |
| S8-5 | `maps_fileinfo.cpp` `map_format_info.cpp` `map_format_helper.cpp` | `Assets/Maps/MapCatalog.cs`、`Editor/TerrainTransitions.cs` |
| S8-6 | `map_random_generator.cpp` `_info` `_helper` | `Maps/MapRandomGenerator*.cs`（新建，同时做 S13 的对话框接口） |

### S9 `world`（5 个 cpp，约 7 000 行）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S9-1 | `world.cpp` | `World/World*.cs` |
| S9-2 | `world_loadmap.cpp` | `World/World.cs` 装载段（新建 `World.LoadMap.cs`） |
| S9-3 | `world_pathfinding.cpp` | `WorldPathfinder.cs`、`PlayerWorldPathfinder.cs`、`AIWorldPathfinder.cs` |
| S9-4 | `world_regions.cpp` `world_object_uid.cpp` | `World.Regions.cs`、`MapRegion.cs`、`CapturedObject.cs` |

### S10 `battle`（18 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S10-1 | 内核 14 篇（`action` `arena` `army` `board` `bridge` `catapult` `cell` `command` `grave` `main` `pathfinding` `tower` `troop` `animation`） | `Battle/*.cs`，**已收口** |
| S10-2 | `battle_interface.cpp` | `Battle/BattleInterface*.cs`（新建） |
| S10-3 | `battle_interface_settings.cpp` | `Battle/BattleInterfaceSettings.cs`（新建） |
| S10-4 | `battle_dialogs.cpp` | `Battle/BattleDialogs*.cs`（新建） |
| S10-5 | `battle_only.cpp` | `Battle/BattleOnly*.cs`（新建） |

### S11 `ai`（9 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S11-1 | `ai_common.cpp` | `Ai/AiCommon.cs`，**已成** |
| S11-2 | `ai_planner.cpp` | `Ai/AiPlanner.cs`，B2 4/5 |
| S11-3 | `ai_planner_castle.cpp` | `Ai/AiPlanner.cs` 城堡段，**已成** |
| S11-4 | `ai_planner_hero.cpp` | `Ai/AiPlanner.Hero.cs`，`HeroesTurn` 待做 |
| S11-5 | `ai_planner_kingdom.cpp` | `Ai/AiPlanner.Kingdom.cs`，`KingdomTurn` 待做 |
| S11-6 | `ai_personality.cpp` | `System/AiPersonality.cs` |
| S11-7 | `ai_battle.cpp` | `Ai/AiBattlePlanner.cs`（约 2 000 行，再拆两次） |
| S11-8 | `ai_battle_spell.cpp` | `Ai/AiBattlePlanner.cs` 法术估值段 |
| S11-9 | `ai_hero_action.cpp` | `Ai/AiHeroAction.cs` |

### S12 `gui`（33 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S12-1 | `cursor.cpp` `ui_base.cpp` `ui_button.cpp` `ui_window.cpp` `ui_dialog.cpp` | `UI/Widgets/*`、`Game/Gui/*` |
| S12-2 | `ui_text.cpp` `ui_font.cpp` `ui_keyboard.cpp` `ui_tool.cpp` | `Game/Gui/Text*.cs`、`UI/Input/*` |
| S12-3 | `ui_scrollbar.cpp` `ui_slider.cpp` `ui_option_item.cpp` `ui_language.cpp` | `Widgets/ScrollBar.cs`、`Slider.cs`、`OptionItem.cs` |
| S12-4 | `interface_base.cpp` `interface_border.cpp` `interface_buttons.cpp` `interface_events.cpp` `interface_focus.cpp` | `UI/Interface/*` |
| S12-5 | `interface_cpanel.cpp` `interface_icons.cpp` `interface_status.cpp` | `UI/Interface/*`（新建） |
| S12-6 | `interface_radar.cpp` `interface_gamearea.cpp` | `UI/Interface/*` |
| S12-7 | `ui_castle.cpp` `ui_kingdom.cpp` `ui_campaign.cpp` | `UI/Dialogs/*`（多为绘制辅助） |
| S12-8 | `ui_mage_guild.cpp` `ui_monster.cpp` `skill_bar.cpp` `statusbar.cpp` `player_info.cpp` | `UI/Widgets/*`、`UI/Dialogs/*` |
| S12-9 | `ui_map_interface.cpp` `ui_map_object.cpp` | `AdventureMap/*` |

### S13 `dialog`（29 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S13-1 | `dialog_box.cpp` `dialog_frameborder.cpp` `dialog_quickinfo.cpp` `dialog_selectcount.cpp` | `UI/Dialogs/*`，多为已成 |
| S13-2 | `dialog_adventure.cpp` `dialog_armyinfo.cpp` `dialog_artifact.cpp` `dialog_chest.cpp` `dialog_giftresources.cpp` | `UI/Dialogs/*` |
| S13-3 | `dialog_arena.cpp` `dialog_buyboat.cpp` `dialog_marketplace.cpp` `dialog_recruit.cpp` `dialog_thievesguild.cpp` | `UI/Dialogs/*` |
| S13-4 | `dialog_levelup.cpp` `dialog_selectitems.cpp` `dialog_selectscenario.cpp` `dialog_random_map_generator.cpp` | `UI/Dialogs/*` |
| S13-5 | `dialog_file.cpp` `dialog_selectfile.cpp` `dialog_gameinfo.cpp` `dialog_game_settings.cpp` | `UI/Dialogs/*` |
| S13-6 | `dialog_audio.cpp` `dialog_graphics_settings.cpp` `dialog_hotkeys.cpp` `dialog_interface_settings.cpp` `dialog_language_selection.cpp` `dialog_resolution.cpp` `dialog_system_options.cpp` | `UI/Dialogs/*` |

### S14 `game`（28 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S14-1 | `game.cpp` `game_startgame.cpp` `game_newgame.cpp` `game_scenarioinfo.cpp` | `Game` 根 |
| S14-2 | `game_io.cpp` `game_loadgame.cpp` `highscores.cpp` `game_highscores.cpp` | `Game/Save*`（新建） |
| S14-3 | `game_mainmenu.cpp` `game_mainmenu_ui.cpp` `game_intro.cpp` `game_credits.cpp` | `FHeroes2.App` |
| S14-4 | `game_static.cpp` `game_string.cpp` `game_delays.cpp` `game_init.cpp` `game_exit.cpp` `game_over.cpp` | `Maps/GameStatic.cs`、`Game/*` |
| S14-5 | `game_interface.cpp` `game_hotkeys.cpp` `game_assets.cpp` `game_invalid_assets.cpp` `game_tools.cpp` `game_video.cpp` | `Game/*`（新建） |
| S14-6 | `game_campaign.cpp` `game_auto_playtest.cpp` `difficulty.cpp` | `Game/Campaign*`、`System/Difficulty.cs` |

### S15 `campaign`（3 个 cpp）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S15-1 | `campaign_data.cpp` `campaign_savedata.cpp` `campaign_scenariodata.cpp` | `Game/Campaign/*`（新建） |

### S16 `editor`（18 个 cpp，最后）

| 编号 | 参考文件 | 端口落点 |
| --- | --- | --- |
| S16-1 | `editor_interface.cpp` `editor_interface_panel.cpp` `editor_ui_helper.cpp` | `Game/Editor/*`、`App` |
| S16-2 | `history_manager.cpp` `editor_options.cpp` | `Game/Editor/*` |
| S16-3 | `editor_castle_details_window.cpp` `editor_map_specs_window.cpp` `editor_object_popup_window.cpp` | `Game/Editor/*` |
| S16-4 | `editor_daily_event_spec_window.cpp` `editor_daily_events_window.cpp` `editor_event_details_window.cpp` `editor_rumor_window.cpp` `editor_sphinx_window.cpp` | `Game/Editor/*` |
| S16-5 | `editor_mainmenu.cpp` `editor_language_window.cpp` `editor_save_map_window.cpp` `editor_secondary_skill_selection.cpp` `editor_spell_selection.cpp` | `Game/Editor/*`、`App` |

## 4. 口径说明

* **「有落点」不等于「已实装」**：第一遍验收看的是「参考的每个非内联函数在端口有一个公开或内部的对应成员」，
  而不是每个分支都写完。实装缺口的清单由「身体是 `NotImplementedException`」这一事实自动表达，不需要另立
  台账。
* **既有代码不回炉**（**已被第 5 节改写**）：端口已有的、比参考更细的拆分（如 `Hero.*.cs`、`World.*.cs`）保持不动，
  只补缺失成员。但凡是「端口另写一套顶替参考」的界面代码，一律按第 5 节换成 1:1 移植并删掉自建件。
* **不被「已完成」误导**：`docs/PROGRESS.md` 的覆盖率快照止于 2026-11-15 且按 v3 批次口径统计，本计划以
  `progressv4.md` 的目录状态表为准。

## 5. 第二阶段：1:1 还原缺口与执行顺序（从 `gui/` 开始）

### 5.1 口径

* **1:1 的判据**：参考的每个非内联函数在端口都有同名或同义的成员，且函数体逐段对应——循环边界、魔数、
  绘制顺序、早退条件一致；文档注释里写清参考文件与行号。
* **三类差距**：①**部分**（有对应文件，缺函数或分支）②**未移植**（端口用自建件顶替）③**纯缺失**（端口没有）。
* **删除顺序**：写参考移植 → 调用方切换 → 测试迁移 → 删自建文件。任何一步没做完就不删，避免出现「两边都不完整」。
* **不做的事**：不为了对齐而重命名端口里已经 1:1 的文件；`interface_*` 的拆分方式（一个类拆多文件）可以保留，
  但类与函数必须与参考对得上。

### 5.2 `gui/` 现状判定（33 个 cpp）

| 参考文件 | 行数 | 端口对应 | 判定 |
| --- | --- | --- | --- |
| `ui_tool.cpp` | 861 | `Widgets/ColorFade.cs`、`DisplayFade*.cs`、`*Restorer.cs`、`HeroRacePortrait.cs`、`IntegerValueTyping.cs`、`TextInputField.cs`、`Game/Gui/LocalizedText.cs`、`MovableText.cs`、`SystemInfoRenderer.cs`、`TimedEventValidator.cs`、`Widgets/ScrollInertia.cs` | ①完成（2026-11-22 核实）：`UIScrollInertia` 已在 `ScrollInertia.cs`，`interface_gamearea` 已接；只剩 `getLocalizedStrings`（翻译层，不搬） |
| `cursor.cpp` | 225 | `Game/Cursor/CursorState.cs`、`CursorImages.cs`、`CursorRestorer.cs`、`ICursorRenderer.cs` | ①部分：缺 `engine/screen.cpp` 的软件光标实现（App 层），距离光标图由资材合成（`game_assets.cpp:3694-3740`）未搬 |
| `ui_base.cpp` | 27 | 无 | ③纯缺失（`setIMEText` 一类平台钩子） |
| `ui_button.cpp` | 813 | `Widgets/ButtonFactory.cs`、`ButtonBase.cs`、`ButtonSprite.cs`、`ButtonGroup.cs`、`ButtonArtwork.cs`、`ButtonRestorer.cs`、`Game/Assets/EmptyButtons.cs` | ①完成：剩 `ButtonGroup` 的文本构造函数（唯一调用方 `dialog_selectitems.cpp` 未转）与 `OptionButtonGroup`（依赖 `ActionObject` 观察者，端口无该机制） |
| `ui_window.cpp` | 541 | `Dialogs/StandardWindow.cs`、`FrameBox.cs`、`Widgets/WindowPadding.cs` | ①部分：`FrameBox` 取的是 `dialog/dialog_box.cpp`，与 `ui_window.cpp` 的 `FrameBox` 不是同一件东西 |
| `ui_dialog.cpp` | 849 | `Dialogs/DialogElement.cs` | ①完成：元素类齐全；`drawMonsterLine` 属于军队信息窗口（`dialog.cpp`），随 H 一并转 |
| `ui_text.cpp` | 1132 | `Game/Gui/Text.cs`、`TextBase.cs`、`TextInput.cs`、`MultiFontText.cs`、`TextAlgorithms.cs`、`FontCharHandler.cs` | ①部分：全转完；`getCursorSprite`/`isFontAvailable`/`cursorChar` 落在 `FontCharHandler` |
| `ui_font.cpp` | 6317 | `Game/Gui/FontAlphabet.*`（2026-11-22 新建）、`FontRepairs`、`FontAssetFixes`、`FontFrenchRepairs`、`ButtonFont*`、`FontGlyphTable` | ①部分：`resizeCodePage` / `generateAlphabet` / 八字码页 `generateCPxxxxAlphabet` / 两个阴影助手 / `removeShadows` 已 1:1 落地（四千余条纯数据语句由 `tools/GenerateFontAlphabets.ps1` 从参考逐句转写，见 `FontAlphabet.Cp*.cs`），`modifyBaseNormalFont` / `modifyBaseSmallFont` / `applyFontVerticalGradient` / `generateButtonAlphabet` / `updateButtonFont` / 按钮字体的五个 `generateCPxxxxGoodButtonFont`（`ButtonFont.Cp*.cs`，同一套转写脚本的 `GenerateButtonFontAlphabets.ps1`）/ `addContour` 也已在。**本文件无剩余** |
| `ui_keyboard.cpp` | 1146 | `Dialogs/VirtualKeyboardLayout.cs`、`VirtualKeyboardArtwork.cs`、`VirtualKeyboardRenderer.cs`、`VirtualKeyboardButton.cs`、`VirtualKeyboardView.cs` | ①全转完；阻塞事件循环改由宿主驱动，`openVirtualNumpad` 的数值校验落在 `ReadNumpad` |
| `ui_language.cpp` | 327 | `CodePage.cs`、`GameLanguage.cs`、`LanguageSwitcher.cs` | ①转完：语言名单 / 缩写 / 码页 / 切换守卫 |
| `ui_scrollbar.cpp` / `ui_slider.cpp` / `ui_option_item.cpp` | 213/129/144 | `Widgets/ScrollBar.cs`、`ScrollSlider.cs`、`Slider.cs`、`OptionItem.cs` | 已 1:1（保留） |
| `ui_castle.cpp` | 271 | `Widgets/ResourcePanel.cs`、`Dialogs/CastleFlagPalette.cs`、`Interface/CastleIconPainter.cs`、`Dialogs/CastleScreenView.cs`、`Game/AdventureMap/IconBar.cs` | ①核实 2026-11-22：9 个函数逐个比对，修正 `getCastleIcnIndex` 的 `Race::RAND` 分支（应为 25/26 的编辑器帧，端口原来返回 null 并断言文件没有这两帧） |
| `ui_kingdom.cpp` | 78 | `UI/Dialogs/LighthouseInfo.cs`、`KingdomIncome.cs` | ①完成（2026-11-22，两个函数都有落点） |
| `ui_campaign.cpp` | 190 | `UI/Dialogs/CampaignPopupWindows.cs` | ①完成（2026-11-22）：`ShowScenarioBonus` / `ShowAward` 两个窗口 + `BuildMonsterFrame`；顺带补了它依赖的 `PrimarySkillNames.GetDescription` 与 `SecondarySkills.GetDescription` |
| `ui_mage_guild.cpp` | 192 | `Dialogs/MageGuildDialog.cs`（`renderMageGuildBuilding`）、`Widgets/SpellScrollRow.cs`（`SpellsInOneRow`） | ①核实 2026-11-22：建筑居中（`GetActiveRoi`）、裁剪到左 200 列、随机种族的紫色调色板重映射、卷轴 110 步长与图标/名字位置全部一致；仅 `SpellsInOneRow::setCurrentSpell()` 未移植（只给编辑器用） |
| `ui_monster.cpp` | 56 | `UI/Widgets/MonsterFrameRenderer.cs` | ①完成（2026-11-22）：`ArmyBar` 原来的内联段改为调用它 |
| `ui_map_interface.cpp` | 67 | `Widgets/StandardPopupWindow.cs` | ①完成（2026-11-22 核实）：`displayStandardPopupWindow` 及其 `getPopupWindowPosition` 都已在内 |
| `ui_map_object.cpp` + `ui_object_rendering.h` | 316+52 | `UI/Interface/TileRendering.cs`、`Game/Maps/ObjectRenderingInfo.cs` | ①完成（2026-11-22，随 G3 落地；DTO 就是头文件那一半） |
| `interface_base.cpp` | 45 | `UI/Interface/BaseInterface.cs`、`UI/Interface/Redraw.cs` | ①部分：`Redraw` 掩码、边缘滚动判定、`GameArea`/`Radar` 持有与重绘队列已转；两个淡入方法待 `Game::validateDisplayFadeIn`（S14） |
| `interface_border.cpp` | 428 | `UI/Interface/GameBorder.cs`、`UI/Interface/BorderWindow.cs` | ①完成（拖动循环改为宿主驱动的 `BorderDrag`） |
| `interface_buttons.cpp` | 249 | `UI/Interface/ButtonsPanel.cs` | ①完成（点击返回 `ButtonsPanelAction`，事件由编排器执行；`SavePosition` 待 Settings 位置持久化） |
| `interface_cpanel.cpp` | 100 | `UI/Interface/ControlPanel.cs` | ①完成（点击返回 `ControlPanelButton`，事件由编排器执行） |
| `interface_events.cpp` | 535 | `UI/Interface/AdventureMap.Events.cs`（25 个 `Event*` + 3 个移动辅助）+ `UI/Interface/AdventureMapHost.cs`（`AdventureEventResult` / `AdventureOption` / `IAdventureHost`） | ①完成（2026-11-22） |
| `interface_focus.cpp` | 237 | `UI/Interface/AdventureMap.cs`（`SetFocus` / `ResetFocus` / `UpdateFocus` / `RedrawFocus`）+ `Game/Audio/AdventureSounds.cs`（焦点音景） | ①完成（2026-11-22） |
| `interface_gamearea.cpp` | 1053 | `UI/Interface/GameArea.cs` + `UI/Interface/GameArea.Render.cs` | ①完成：几何/滚动/拖动/惯性/命中在 `GameArea.cs`，`Redraw` / `redrawOnlyFog` / `renderTileAreaSelect` / `GenerateUltimateArtifactAreaSurface` 与 tile-unfit 分层在 `GameArea.Render.cs`；只剩 `AdventureMapScene` 的接线（G5） |
| `interface_icons.cpp` | 433 | `UI/Interface/IconsPanel.cs` | ①完成（隐藏界面时无浮动石框，同 `Radar`；`SavePosition` 只写会话内 Settings；英雄头像按 `PORTXTRA` + `MINIPORT` + 移动力/法力/睡眠标记手绘，未复用 `Hero::PortraitRedraw` 的 `PORT_SMALL`） |
| `interface_itemsbar.h` | 460 | `Widgets/ItemsBar.cs` + `Widgets/ItemsActionBar.cs` | ①完成 2026-11-22：`ItemsBar<T>`（布局/命中/Redraw/事件分发/`SetContentItems`）与 `ItemsActionBar<T>`（选中、双条拖放、14 个 ActionBar 钩子）。`skill_bar.cpp` 的两条 bar 已改为继承；`ArmyBar`/`ArtifactsBar` 仍走自建 `ItemsBarLayout`，待下一轮接入后删除该类 |
| `interface_list.h` | 540 | `Widgets/ListBox.cs`、`ListBoxBase.cs` | 已 1:1（保留） |
| `interface_radar.cpp` | 459 | `UI/Interface/Radar.cs` | ①完成（View World 的拷贝构造 / `RedrawForViewWorld` / `QueueEventProcessingForWorldView` 待 H 的 `ViewWorld::ZoomROIs`；`SavePosition` 待 Settings 位置持久化） |
| `interface_status.cpp` | 440 | `UI/Interface/StatusPanel.cs` | ①完成（`SavePosition` 只写会话内 Settings，待配置写入器；隐藏界面时无浮动石框，同 `Radar`） |
| `player_info.cpp` | 380 | `Widgets/PlayersInfo.cs` | ①完成（2026-11-22）：`UpdateInfo` / `SwapPlayers` / 四个命中测试 / `RedrawInfo` / `QueueEventProcessing` / `ReadOnlyEventProcessing` / `ApplyName`；顺带补了它依赖的 `Mp2FileInfo.AllowChangeRace` 与 `GameScenarioInfo.GetStep4Player` |
| `skill_bar.cpp` | 394 | `Widgets/SkillBars.cs`（`SkillBarArtwork`、`PrimarySkillsBar`、`SecondarySkillsBar`、`DrawSkillNames`） | ①完成（2026-11-22）：绘制部分；输入（点开说明、编辑器改等级）与 `isEditMode` 分支留给界面与 editor |
| `statusbar.cpp` | 62 | `UI/Widgets/StatusBar.cs` | ①完成（2026-11-22） |
| `ui_constants.h` | 33 | `UI/UiConstants.cs` | ①完成（2026-11-22）：五个常量收成单一来源，`GameBorder`/`Radar`/`FrameBox`/`FrameBorder`/`LighthouseInfo`/`StandardPopupWindow`/`ScenarioSelectionDialog` 各自保留自己的名字 |

### 5.3 执行顺序（一步一收口）

| 步 | 内容 | 参考行号 | 收口标志 |
| --- | --- | --- | --- |
| A1 | `colorFade`（按秒数逐帧压暗到目标调色板） | `ui_tool.cpp:395-484` | 纯函数 + 单测 |
| A2 | `fadeOutDisplay` / `fadeInDisplay` / `FadeDisplayWithPalette` / `InvertedFadeWithPalette` / `InvertedShadow` | `ui_tool.cpp:653-760` | 宿主淡入淡出改调参考实现 |
| A3 | `ScreenPaletteRestorer` / `ApplicationClosureRestorer` / `GameInterfaceTypeRestorer` | `ui_tool.cpp:349-393` | 三件 RAII 换成移植 |
| A4 | `CreateDeathWaveEffect` / `CreateHolyShoutEffect` / `createRippleEffect` | `ui_tool.cpp:485-652` | 战斗特效接入 |
| A5 | `renderHeroRacePortrait` | `ui_tool.cpp:841-912` | 英雄对话框改用 |
| A6 | `getLocalizedText` 两个重载；`getLocalizedStrings` 登记不搬（翻译层）；`UIScrollInertia` → `Widgets/ScrollInertia.cs`（2026-11-22 核实已落地并有单测） | `ui_tool.cpp:890-1077` | 偏差入档 |
| A7 | `processIntegerValueTyping`（A 系列漏项）；`RecruitDialog` 换成 `TypeKey` 并由 `RecruitWindowView` 调用。`SelectCountView` 没有键盘入口，留到该窗口补输入时接 | `ui_tool.cpp:766-839` | 招募输入走参考实现 |
| B | `cursor.cpp` 全量：`CursorState` + `CursorRestorer` + 图源解析（`AdventureMapScene` 从来没有光标选择，原计划这条前提有误） | `cursor.cpp:1-271` | 光标状态机就位，等 App 层的软件指针接 |
| C | `ui_text.cpp` 收尾：`getCursorSprite`、`isFontAvailable`、`cursorChar`；`TextInputField`（实际在 `ui_tool.cpp`，原计划归在 `ui_text` 有误）。存档 / 战役名输入对话框尚未移植，暂无调用方 | `ui_text.cpp:1398-1432`、`ui_tool.cpp:196-281` | 文本控件齐备，等 D 的屏幕键盘与存档对话框接 |
| D | `ui_keyboard.cpp` 全量（`VirtualKeyboardLayout`、`VirtualKeyboardArtwork`、`VirtualKeyboardRenderer`、`VirtualKeyboardButton`、`VirtualKeyboardView`、`ReadNumpad`）；`ui_language.cpp` 已随 C 步落在 `CodePage`/`GameLanguage`/`LanguageSwitcher` | `ui_keyboard.cpp:1-1360` | 名称输入全走参考键盘；`openVirtualKeyboard`/`openVirtualNumpad` 的阻塞循环改由宿主驱动 |
| E-1 | `ui_dialog.cpp` 收尾：补 `MonsterDialogElement`、`CustomAnimationDialogElement` | `ui_dialog.cpp:856-935` | `DialogElement` 与参考一一对应 |
| E-2 | `ui_window.cpp` 对拍：删 `StandardWindowChrome`/`FrameBoxChrome`，`StandardWindow` / `FrameBox` 自己按参考取图（`SURDRBKG`/`WINLOSE`/`STONEBAK`/`BUYBUILD`），`WindowPadding` 复核 | `ui_window.cpp:1-541` | 对话框骨架只剩参考一套 |
| F | `ui_button.cpp` 剩余：透明背景按钮、`makeButtonWithBackground/Shadow`、`EMPTY_VERTICAL_GOOD_BUTTON`/`EMPTY_MAP_SELECT_BUTTON`、`getSupportedText` | `ui_button.cpp:1-813` | 删 `DialogButtonArtwork`、`ButtonArtwork` 自算段 |
| G1 | `interface_base.cpp` / `interface_cpanel.cpp` / `interface_buttons.cpp` / `interface_border.cpp` | 共 822 行 | 冒险地图面板由参考类绘制 |
| G2 | `interface_radar.cpp` / `interface_status.cpp` / `interface_icons.cpp` | 共 1 332 行 | 雷达、状态栏、图标条 |
| G3 | `interface_gamearea.cpp`（滚动、绘制、tooltip 命中）；被它依赖的 `maps/maps_tiles_render.cpp`（1 327 行，1 118 非空行）按 ADR-197 先落接口骨架。已全部完成（2026-11-22） | 1 285 行 | 删 `AdventureMapScene` 自建视口 |
| G4 | `interface_events.cpp` / `interface_focus.cpp` | 共 772 行 | 删 `AdventureMapView` 自建事件 |
| | 进度：`interface_focus.cpp` 与 `interface_events.cpp` 均已完成（2026-11-22）。事件通过 `IAdventureHost` 契约把对话框交给场景层，未见 `fheroes2::GameMode` 全局枚举（改用 `AdventureEventResult`，与 `FileOptionsResult` 一致）；把事件接到 `AdventureMapScene` 仍属 G5 | | |
| G5 | 收口：`AdventureMapScene` 只留场景装配 | — | 自建面板代码全部删除 |
| G6 | 直接照搬 C++ 的同步对话框路径：补 `LocalEvent::HandleEvents` 的 SDL 侧，每个对话框加 `Show()` 阻塞入口，`interface_events.cpp` 的 17 个 `Event*` 逐行搬，删掉 `IAdventureHost` 抽象 | `engine/localevent.cpp:1243-1316`、`gui/interface_events.cpp`、13 个对话框源文件 | `UI/Input/EventEngine.cs`、各 `Dialogs/*.Show`、`UI/Interface/AdventureMap.Events.cs` | 事件函数与参考逐函数对应，工程里不再有「请求式」对话框契约 |
| H | `player_info.cpp`、`skill_bar.cpp`、`statusbar.cpp`、`ui_kingdom.cpp`、`ui_campaign.cpp`、`ui_monster.cpp`、`ui_mage_guild.cpp`、`ui_map_object.cpp`(+`ui_object_rendering.h`)、`ui_map_interface.cpp`、`ui_base.cpp`、`ui_constants.h` | 共 2 300 行 | 各自有 1:1 文件。已完成 2026-11-22：`skill_bar.cpp`、`ui_kingdom.cpp`、`statusbar.cpp`（`UI/Widgets/StatusBar.cs`）、`ui_monster.cpp`（`UI/Widgets/MonsterFrameRenderer.cs`，`ArmyBar` 改为调用它）、`ui_campaign.cpp`（`UI/Dialogs/CampaignPopupWindows.cs`）、`ui_constants.h`（`UI/UiConstants.cs`）、`ui_map_interface.cpp`（已核实，`StandardPopupWindow` 里齐全）；`ui_base.cpp` **不适用**（全篇只有一个空的 `ActionObject::senderUpdate()`，端口没有观察者机制）。已完成 2026-11-22 第二轮：`interface_itemsbar.h`（`ItemsBar<T>` + `ItemsActionBar<T>`）、`ArmyBar`/`ArtifactsBar` 接入并删掉自建 `ItemsBarLayout.cs`、`ui_castle.cpp` 与 `ui_mage_guild.cpp` 核实。**本组无剩余** |
| I | `ui_font.cpp` 对拍（字形、断词、语言字符表）。已完成 2026-11-22 两批：①八字码页字形生成 + `resizeCodePage` + `generateAlphabet` + 阴影/去阴影助手，接进 `FontGlyphTable` 的基表构建；②按钮字体的五个 `generateCPxxxxGoodButtonFont` + `updateButtonFont`（`ButtonFont.Create( language )`，表长随语言 128/256） | 6 317 行 | 所有绘制经由参考 `Font` |
| J | 清理与回归：删自建文件、迁测试、全量 `dotnet test` | — | 0 自建替代 |

进度：A1（`colorFade`）、A2（淡出/淡入、调色板淡入淡出、反色阴影）、A3（三件作用域守卫 +
`ScreenBuffer.ChangePalette`）、A4（死亡波浪 / 圣言 / 涟漪）、A5（`SubpixelResize` + 种族立绘）、
A6（`getLocalizedText` + `ui_tool` 剩余件盘点）、A7（`processIntegerValueTyping` + 招募窗口接线）、
B（`cursor.cpp` 全量）、C（`TextInputField` + `getCursorSprite`/`isFontAvailable`）、
D（屏幕键盘全量 + `ui_language` 收口）、E-1（`MonsterDialogElement` / `CustomAnimationDialogElement`）、
E-2（删两套自建窗框，`StandardWindow` / `FrameBox` 自己取图）已完成，2026-11-21；
F（`ui_button.cpp` 剩余：`getSupportedText` / `makeButtonWithBackground` / `makeButtonWithShadow` / `ButtonRestorer`，
`EMPTY_*_BUTTON` 六张表随 `EmptyButtons` 落地）已完成，2026-11-22；
G1 完成（4/4，2026-11-22）：`interface_border.cpp` 已全部转完（`UI/Interface/GameBorder.cs` + `UI/Interface/BorderWindow.cs`），
连带落地它缺的两个依赖 `Core/Imaging/ImageRestorer.cs`（`fheroes2::ImageRestorer`）与 `UI/Dialogs/FrameBorder.cs`
（`Dialog::FrameBorder`）；`interface_base.cpp` 的基座已落：`UI/Interface/Redraw.cs`（`Interface::Redraw` 掩码）、
`UI/Interface/BaseInterface.cs`、以及它持有的 `UI/Interface/GameArea.cs` / `UI/Interface/Radar.cs` 签名骨架
（方法体抛 `NotImplementedException`，随 G3 / G2 填）；`interface_cpanel.cpp` 已全部转完
（`UI/Interface/ControlPanel.cs`，连带 `Core/Imaging/ImageOps.AlphaBlit.cs`）；`interface_buttons.cpp` 已全部转完
（`UI/Interface/ButtonsPanel.cs`；`SavePosition` 已写进 `Settings.PosButtons`，落盘等配置写入器）。
G2 已完成（3/3，2026-11-22）：`interface_radar.cpp` 已全部转完（`UI/Interface/Radar.cs`，成像 / 光标 /
事件 / 调色板查表，View World 的拷贝构造、`RedrawForViewWorld`、`QueueEventProcessingForWorldView` 与无参
`RedrawCursor` 分别待 H 的 `ViewWorld::ZoomROIs` 与 G3 的 `GameArea.VisibleTileRoi`）；`interface_status.cpp` 已全部
转完（`UI/Interface/StatusPanel.cs`，五态绘制 / 三段布局 / 左键循环 / 「找到资源」计时 / AI 回合沙漏，隐藏界面时
无浮动石框，同 `Radar`）；`interface_icons.cpp` 已全部转完（`UI/Interface/IconsPanel.cs`，左右两列 56×32 槽 /
两个 `IconsBar` 子类 / 选中框 / 换序 / 拖动转发，英雄头像与浮动石框两处偏差见上表）。三者 `SavePosition` 都已写进
`Settings.PosRadar` / `Settings.PosStatus` / `Settings.PosIcons`，落盘等配置写入器。
G3 前置已完成（2026-11-22）：`interface_gamearea.cpp` 的 `Redraw` / `redrawOnlyFog` / `renderTileAreaSelect` /
`generateUltimateArtifactAreaSurface` 全部踩在尚未移植的 `maps/maps_tiles_render.cpp` 上（`docs/PLAN.md` 记的
`maps` 目录最后一块），按 ADR-197 先把被依赖文件的接口骨架落下来：`UI/Interface/TileRendering.cs` 收全 16 个入口
（`redrawEmptyTile` / `redrawFlyingGhostsOnMap` / `redrawTopLayerObject` / `drawFog` / `redrawPassable` /
`redrawBottomLayerObjects` / `drawByObjectIcnType` / 怪物·空船·矿守卫·英雄的四组精灵清单 / `getTileSurface`），
签名与参考一致、方法体抛 `NotImplementedException`；顺带提前落 `Game/Maps/ObjectRenderingInfo.cs`
（`gui/ui_object_rendering.h` 的 DTO，参考里本来就是 header 内实现，所以直接写全，H 行的「`+ui_object_rendering.h`」
这一半已完成）。G3 的几何 / 滚动 / 惯性 / 拖动 / 点击命中那一半也已落地（`GameArea` 328-408、862-1285 各行），
`BlitOnTile`×2 / `DrawTile` 也已接通裁剪 blit。

`TileRendering` 的实体实现（2026-11-22 第一批）：`redrawEmptyTile` / `redrawPassable` / `redrawFlyingGhostsOnMap` /
`redrawTopLayerObject` / `redrawBottomLayerObjects` / `drawByObjectIcnType` / `drawFog` / `getTileSurface` 已写全，
连带 `renderObjectPart` / `renderMainObject` / 两个 direct-rendering 判定 / `MP2::isHiddenForPuzzle` 四个私有助手，
以及进程级图集 `Game/AdventureMap/MapSprites.cs`（参考的 `Assets::getImage` 全局表）。
八个 `getMonsterSpritesPerTile` / `getMonsterShadowSpritesPerTile` / `getBoatSpritesPerTile` /
`getBoatShadowSpritesPerTile` / `getMineGuardianSpritesPerTile` / `getHeroSpritesPerTile` /
`getHeroShadowSpritesPerTile` / `getEditorHeroSpritesPerTile` 与配套的 `DivideImageBySquares`（Core）、
`GetMonsterSpriteIndices`、`getHeroSpriteInfo`/`getFlagSpriteInfo`/`getShadowSpriteInfo`/`getFrothSpriteInfo`
也已写完（2026-11-22 第二批），`maps_tiles_render.cpp` 到此整文件落地。

G3 已完成（2026-11-22）：消费方落地在新文件 `UI/Interface/GameArea.Render.cs`（`GameArea` 改 `partial`）——
`Redraw`（地形 → 下层 3/1 → tile-unfit 底部背景 → 阴影 → tile-unfit 阴影 → 英雄下半身 → 低优先级 → level 0 →
tile-unfit 中间 → 高优先级 → 顶层 tall 延后 → 飞鬼 → 路线 → 通行性/雾+城镇剪影）、`redrawOnlyFog`、
`renderTileAreaSelect`、`GenerateUltimateArtifactAreaSurface`、`runSingleObjectAnimation`（只入队，阻塞循环交给场景）
与 `updateObjectAnimationInfo`，连同七个桶的 `TileUnfitRenderObjectInfo` 与
`populateStaticTileUnfitObjectInfo` / `populateStaticTileUnfitBackgroundObjectInfo` / `populateHeroObjectInfo` /
`isTallTopLayerObject` / `renderImagesOnTiles` 五个私有助手。`ObjectFadingOutInfo` 另补 `RemoveObjectFromTile`
承接参考析构函数的摘除逻辑（C# 无确定性析构）。`interface_gamearea.cpp` 至此全部成员都有实体实现，
`GameArea.cs` 不再有 `NotImplementedException`。偏差见 `docs/progressv4.md` 的 G3 收尾一节。

G5 前半（2026-11-22）：`game/game_interface.cpp` 的三个函数与 `game/game_startgame.cpp` 的光标选择落地——
`AdventureMap.RedrawInterface`（掩码合并 + 六段绘制，锁定时只收集）、`AdventureMap.Reset`（隐藏界面模式取
`Settings.Pos*`，四个位置都有效才照搬，否则从左上角堆叠；普通模式排右边栏）、`AdventureMap.RedrawLocker`（RAII）；
光标部分落在 `UI/Interface/AdventureMap.Cursor.cs`（`GetCursorFocusCastle` / `GetCursorFocusShipmaster` /
`GetCursorFocusHeroes` / `GetCursorNoFocus` / `GetCursorTileIndex` / `UpdateCursor`），连带的
`World.IsCaptureObjectProtected`（`maps_tiles_helper.cpp:1836-1856`）、`IconsPanel.RedrawIcons`、`ButtonsPanel.SavePosition`
与 `Settings.Pos*` 的 `(-1, -1)` 初值也已补齐。`BaseInterface.UpdateCursor(int)` 与参考的双参数版本之间用
`UpdateCursor(tileIndex, false)` 转接。G5 中段（同日）：`game_startgame.cpp:1615-1736` 的三个鼠标处理落在
`UI/Interface/AdventureMap.Mouse.cs`（左键按光标主题分派、右键三选一速览、长按转 `EventSwitchFocusedHero`），
`IAdventureHost` 补三个 `ShowQuickInfo()` 重载。G5 收口第一步：`AdventureMap` 改成参考的自建装配——`public`
构造收成私有，`TryCreate( display, assets, audio, host )` 按 `isEvilInterfaceEnabled()` 选
`ADVBTNS`/`ADVEBTNS` 造四个面板并加载 `ADVBORD.ICN` 边框（`game_interface.cpp:38-61`），另补
`ControlPanelAreaForInput` 覆写。`MapSprites.Initialize` 已在 `GameHost.LoadGameData()` 里接上，`AdventureMapTests`
用两个整屏绘制用例（经典布局 / 隐藏界面）证明整屏能画出，并钉住 `Radar.Build()` 与调色板两条前置。**剩余**：App 侧
`IAdventureHost` 实现（前置是英雄对话框与城堡对话框；法术书与王国总览已于 2026-11-22 转完），最后接管
`AdventureMapScene` 并删自建面板。G5 收口第二步（同日）：宿主里的模态帧泵已落地——`App/IModalScreen.cs` 定契约，
`GameHost.RunModal()` 起内层循环（取事件 → 场景重绘 → 窗口画上去 → 上传，输入不再流进冻结的场景），
`App/Modals/MessageBoxModal.cs` 是 `Dialog::Message` / `Dialog::YES_NO` 的第一个适配器；`IAdventureHost` 的成员
现在可以按参考那样「开 → 等 → 取结果」。前置里
**拼图已转完大半**：`UI.Dialogs.PuzzleMapDialog`（`kingdom/puzzle.cpp` 的 `drawPuzzle()`/`revealPuzzle()`/
`ShowStandardDialog()` 绘制部分），只剩 `ShowExtendedDialog()`（可移动窗口版）与 App 侧 `OpenPuzzleMaps()` 的接线。
**技能条已转完**：`UI.Widgets.SkillBars`（`gui/skill_bar.cpp`，`PrimarySkillsBar`/`SecondarySkillsBar` 绘制部分 +
`RedrawPrimarySkillInfo()`），英雄对话框与王国总览共用。**宝物条已转完**：`UI.Widgets.ArtifactsBar`
（`resource/artifact.cpp:1104-1183` 绘制部分）。前置链已齐（`ArmyBar` / `ArtifactsBar` / `SkillBars` /
`PuzzleMapDialog` / `ListBox` / `ScrollBar`），`kingdom/kingdom_overview.cpp` 已于 2026-11-22 整篇转完
（`UI.Dialogs.KingdomOverviewWindow` + `KingdomOverviewLists` + `KingdomHeroRow` + `KingdomOverviewArtwork` +
`KingdomOverviewRequests`）。下一步整块转 `heroes/heroes_dialog.cpp`（831 行，`Heroes::OpenDialog()`），
它同时是 G5 与城堡对话框的前置。

### G6：直接照搬 C++ 的同步对话框路径

**判断：能搬，且只有一处硬缺口。** 参考里没有 `IAdventureHost` 这种东西——`gui/interface_events.cpp` 的 17 个
`Event*()` 直接在身体里调 `Dialog::*`、`Game::Open*Dialog()`、`Kingdom::openOverviewDialog()`，这些函数自己转
事件循环、关掉才返回。端口当初为了可测试性把这一层改成了「事件只报告要开什么、由场景实现」的请求式契约
（ADR-163），对话框因此都做成了「每帧喂事件」的形状。要回到逐行搬，唯一缺的是参考里那条自转循环的地基：

* `LocalEvent::HandleEvents` **没移植**，`Input/LocalEvent.cs:43-52` 明写「not ported」——它被判定为 SDL 侧。
  参考本体只有 74 行（`localevent.cpp:1243-1316`）：重置状态 → `EventEngine::handleEvents()` → 按需
  `display.render( roi )` → 睡 1 ms。
* 端口的 SDL 映射其实已经全在 `App/GameHost.cs`（`PumpEvents` + `HandleKeyDown` + `HandleMouseButton`，约 400 行），
  呈现全在 `App/Graphics/SdlPresenter`。把它们下沉成 `EventEngine`，`le.HandleEvents()` 就能自转。

补上之后，`interface_events.cpp` 的每个 `Event*()` 都能逐行搬，`IAdventureHost`（连同它的 17 个成员）可以整个删掉，
`AdventureEventResult` 保留下来当 `fheroes2::GameMode` 的替身。

**影响面**：`GameHost` 主循环改为 `le.HandleEvents()` 驱动（现在是自己 switch 事件）；2a 落的 `IModalScreen` /
`RunModal` 降级为过渡期的呈现钩子，G6.8 之后只剩少量「非对话框」用途。

| 编号 | 子任务 | 参考 | 端口落点 | 验收 |
| --- | --- | --- | --- | --- |
| G6.1 | 事件引擎：SDL → `LocalEvent` 状态，`le.HandleEvents( sleep, allowExit )` | `localevent.cpp:1243-1316` + 引擎侧映射 | **完成（2026-11-22）**：`UI/Input/IEventEngine.cs` + `LocalEvent.HandleEvents` + `NextRenderArea`、`App/Input/SdlEventEngine.cs`、`App/Input/SdlKeyMap.cs`、`GameHost` 填充 `LocalEvent.Current` 且主循环改 `le.HandleEvents( false, true )`；5 条单测 | `--enter-map` 53.4 FPS、`--scene editor` 49.9 FPS，三路冒烟退出 0 |
| G6.2 | 呈现与睡眠：`display.render( roi )` + 1 ms 睡眠 | `screen.cpp` 的 `render` | **引擎侧完成（2026-11-22）**：`SdlEventEngine.Render`（整帧上传）与 `Sleep`；`SdlPresenter` 未改 | **剩余**：G6.3 首个 `MessageBox.Show` 里验证每帧可见与 CPU 占用 |
| G6.3 | `MessageBox` 的阻塞入口 `Show( header, body, buttons )` | `ui_dialog.cpp` 的 `showMessage` | **UI 侧完成（2026-11-22）**：`MessageBox.Show(...)` + 私有 `Run`（`le.HandleEvents( false )` → 输入 → `Update`/`Draw` → `Render` + `Sleep`）+ 2 条测试 | **剩余**：`AdventureOptionsDialog` 的真实接入（App 现无 `RunModal` 调用点，随 G6.4 落） |
| G6.4 | 选项类对话框的 `Show`：`AdventureOptions` / `FileOptions` / `Settings` / `GameInfo` / `SelectFile` | `dialog_adventure.cpp:55-110` 等 | **3/5 完成（2026-11-22）**：`AdventureOptionsDialog.Show` + `AdventureOptionsView.SetCursor`、`SettingsDialog.Show`、`FileOptionsView`（新）+ `FileOptionsDialog.Show` + `DialogButton` 六个身份位、21 条测试；**剩余**：`GameInfo`（等全局 world）、`SelectFile`（等存档目录服务） | `--enter-map` 62.2 FPS、`--adventure-options` 54.2 FPS、`--open-settings` 76.9 FPS，退出 0 |
| G6.5 | 内容类对话框的 `Show`：`HeroDialog` / 城堡（App 的 `CastleScene`）/ `KingdomOverview` / `PuzzleMap` / `SpellBook` / `ViewWorld` / `QuickInfo` | `heroes_dialog.cpp`、`castle/castle_dialog.cpp`、`kingdom/kingdom_overview.cpp`、`kingdom/puzzle.cpp`、`spell/spell_book.cpp`、`gui/view_world.cpp`、`dialog_quickinfo.cpp` | **4/7 完成（2026-11-22）**：`PuzzleMapDialog.Show`、`SpellBookWindow.Show`、`HeroDialog.Show`（`Requests` 非空时返回 `None` 把窗口交给调用方）、`KingdomOverviewWindow.Show`（返回 `bool`，请求时先画后返回 `false`），16 条测试；**剩余**：`CastleDialog`（循环在 App 的 `CastleScene`）、`ViewWorld`（整窗未移植）、`QuickInfoDialog`（面板渲染未移植） | `--enter-map` 66.4 FPS、`--adventure-options` 51.4 FPS，退出 0 |
| G6.6 | `GameSave.Save`：World / Settings / GameOver.Result 序列化 | `game/game_save.cpp` | `Game/System/GameSave.cs`（去掉 `NotImplementedException`） | 存档→读档→状态一致的单测 |
| G6.7 | `GameOver::Result::checkGameOver` + 胜负/玩家对话框 | `game_over.cpp:71-702`、`game_startgame.cpp:251-286` | **完成（2026-11-22）**：`Game/GameOverResult.cs` + `.Persistence.cs`（判定、`GameOverMode`、`GameOverRequests`、五字节存档）、`Game/GameOverText.cs`（`GetString` / `GetDescription` / `GetVictoryMessage` / `GetLossMessage`）、`UI/Dialogs/GameOverDialogs.cs`（`ShowVictory` / `ShowDefeat` / `ShowVanquished` / `ComposeSign`）+ `MessageBox.Show( box )`，48 条单测 | 条件表与文案表单测全绿；`--enter-map` 54.8 FPS，退出 0；两窗口测试需 AGG，本机无数据按约定跳过 |
| G6.8 | 搬 `interface_events.cpp` 的 17 个 `Event*()`，删 `IAdventureHost` | `gui/interface_events.cpp`（1 063 行） | `UI/Interface/AdventureMap.Events.cs` 改为同步返回 `AdventureEventResult` | 逐函数对照，无「请求式」残留 |
| G6.9 | `AdventureMapScene` 接管 `AdventureMap`，删自建面板与 `_events` 转接 | — | `App/Scenes/AdventureMapScene.cs` | 截图与现在一致，面板代码归零 |

依赖：G6.1 → G6.2 → G6.3 → G6.4 / G6.5 → G6.8 → G6.9；G6.6 / G6.7 与 G6.4、G6.5 可并行。
规模：G6.1 约 300 行（含从 `GameHost` 迁出），G6.3~G6.5 约 13 个 `Show` 共 600 行，G6.6 约 800 行，
G6.7 约 200 行，G6.8 约 400 行，G6.9 以删改为主，合计约 2 800 行。

**法术书已转完**（2026-11-22）：`spell/spell_book.cpp` 整篇落地为 `UI.Dialogs.SpellBookWindow` +
`SpellBookRenderer` + `SpellBookArtwork`（S2 章节记的 `SpellBookView` 骨架删除，`SpellBook` 拆出
`BeginSession` / `ClampStartIndex`）。同一轮把 `spell/spell_info.cpp` 的 `getSpellDescription()` 补上：
`SpellInfo.GetDescription( spell, caster, world )`，`ISpellBookOwner` 加 `GetPower()`。剩下的接缝是把文案接到界面：
参考在 `SpellDialogElement::showPopup()` 里调它（`ui_dialog.cpp:545`），端口的元素还没拿到施法者；App 侧入口同样未接。
明细见 `docs/progressv4.md` 末尾的 S2 两节。

### 5.4 自建删除台账（`gui/` 范围）

| 端口文件 | 现在的样子 | 由谁接管 | 步骤 |
| --- | --- | --- | --- |
| `Dialogs/FrameBoxChrome.cs` | 自己拼的窗框边框 | `dialog_box.cpp` 的 `Dialog::FrameBox` | E-2，已删除（2026-11-21） |
| `Dialogs/StandardWindowChrome.cs` | 同上，另一套 | `ui_window.cpp` 的 `StandardWindow` | E-2，已删除（2026-11-21） |
| `Widgets/DialogButtonArtwork.cs` | 已核实**无自算美术**：只剩 `readonly record struct`（一对 released/pressed 精灵）+ 资产注入接口 | `ui_button.cpp` 的 `makeButtonSprites` 等 | F 复核保留（2026-11-22）：它是取图缝，不是画法 |
| `Widgets/ButtonArtwork.cs` | 已核实是 `resizeButton` / `addButtonShine` / `makeTransparentBackground` 的 1:1 移植（补齐了 `addButtonShine` 漏掉的两笔亮线） | 同上 | F 复核保留（2026-11-22） |
| `Widgets/ItemsBarLayout.cs` | 自算格子 | `interface_itemsbar.h` 的 `ItemsBar` | G2 |
| `Interface/StatusPanelPainter.cs` | 自算两态状态栏 | `interface_status.cpp` 的 `Interface::StatusPanel` | G2 已按参考写出 `Interface/StatusPanel.cs`，这个过渡画笔等 G5 删 |
| `Scenes/AdventureMapScene.cs` 的面板/雷达/视口段 | 自绘 | `interface_*.cpp` | G1~G5 |
| `AdventureMapView` 的事件段 | 自建事件循环 | `interface_events.cpp` | G4 |
| `Scenes/MageGuildWindow.cs` | 自建窗口 | `ui_mage_guild.cpp` | H |

注：`Dialogs/` 下还有一批「未指名参考」的对话框（`ArenaDialog`、`TavernDialog`、`WellDialog` 等），它们对应的是
`dialog/` 目录，等 `gui/` 收口后按同一套流程处理，不在本节台账内。

