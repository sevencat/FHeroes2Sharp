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
* **既有代码不回炉**：端口已有的、比参考更细的拆分（如 `Hero.*.cs`、`World.*.cs`）保持不动，只补缺失成员。
* **不被「已完成」误导**：`docs/PROGRESS.md` 的覆盖率快照止于 2026-11-15 且按 v3 批次口径统计，本计划以
  `progressv4.md` 的目录状态表为准。

