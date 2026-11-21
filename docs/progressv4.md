# 进度 v4：按目录整块转换

> 计划见 `docs/planv4.md`。**本文件只追加，不改已写下的记录**；状态表随每步更新。
> 格式：每完成一个小计划追加一节，写「做完什么 / 与参考的偏差 / 本次留下的 `NotImplementedException` / 验证」。

## 基线（2026-11-21，v4 起点）

* 构建：`dotnet build FHeroes2Sharp.slnx` 0 错误 0 警告。
* 测试：4 554 全绿（Core 236 + Assets 416 + UI 875 + Game 3 027）。
* 已完成口径（v3 遗留）：`battle/` 内核 14 篇收口；`ai/` B1–B4 与 X1；`castle/` 界面十八批；
  `kingdom/view_world` 全部；`maps/` 大部分；`world/` 大部分。
* 端口 `Game` 项目内 `NotImplementedException`：3 处，全在 `Ai/`。
* `docs/PROGRESS.md` 的覆盖率快照止于 2026-11-15，口径与 v4 不同，**不再作为状态依据**。

## 状态表

| 阶段 | 目录 | 小计划 | 状态 |
| --- | --- | --- | --- |
| S0 | `agg` `image` `h2d` `audio` `system` | 5 | 完成（2026-11-21） |
| S1 | `monster` | 3 | 完成（2026-11-21） |
| S2 | `spell` | 3 | 完成（界面篇为骨架） |
| S3 | `resource` | 4 | 完成（2026-11-21） |
| S4 | `army` | 4 | 完成（2026-11-21） |
| S5 | `heroes` | 9 | 完成（2026-11-21） |
| S6 | `castle` | 7 | 完成（2026-11-21） |
| S7 | `kingdom` | 6 | 完成（2026-11-21） |
| S8 | `maps` | 6 | 完成（S8-6 随机地图生成器旁支推迟） |
| S9 | `world` | 4 | 完成（2026-11-21） |
| S10 | `battle` | 5 | 完成（2026-11-21） |
| S11 | `ai` | 9 | 完成（2026-11-21） |
| S12 | `gui` | 9 | 完成（端口自建界面架构，2026-11-21） |
| S13 | `dialog` | 6 | 完成（2026-11-21） |
| S14 | `game` | 6 | 完成（主循环/菜单/视频为界面层；主菜单编辑器招牌已接 `resurrection.h2d`，2026-11-21） |
| S15 | `campaign` | 1 | 完成（2026-11-21） |
| S16 | `editor` | 5 | 完成（模型层；主菜单已改按参考移植，其余窗口暂为端口自建，2026-11-21） |

## 2026-11-21 S0 `agg` `image` `h2d` `audio` `system`

11 个 cpp 逐个核对函数落点（`image/` 只有头文件），**文件级无缺口**。

补：`Settings.IsTextSupportMode` / `SetTextSupportMode`（模式位早就在，缺读写属性；参考 `settings.cpp:824-836`、`969-972`）。

记下的偏差（判为 C# 化的等价物，不补）：

| 参考 | 端口做法 |
| --- | --- |
| `AGGInitializer`、`AudioInitializer` 两个 RAII 初始化器 | 资源对象构造即加载，没有「先构造后 Init」的中间态 |
| `AudioManager::setVolume`、`setMidiSoundFonts`、`setMidiTimidityCfg` | SDL_mixer 专属后端配置，下沉到 `IAudioDevice` 实现 |
| `GetConfigDirectory`、`GetDataDirectory`、`SetProgramPath`、`IsFile`、`IsDirectory` | `AssetLocator` |
| `getDebugLevel`、`setDebug`、`setDebugLevel` | 端口没有命令行调试级别，日志走 `Debug`/`Debug.Assert` |
| `GetVersionOfCurrentSaveFile` | 归 S14-2 的存档读取（端口 `SaveFileFormat` 目前只有枚举） |

## 2026-11-21 S1 `monster`

3 个 cpp 全部有落点。补：

* `Battle/RandomMonsterAnimation.cs`：随机待机动画（`monster_anim.cpp:41-189`）——帧队列 + 偏移队列 + 按权重掷下一段。
  `Static` 与 `Moving` 重复入表两次是参考给的权重，注释里标了「不是笔误」。
* `Monsters/MonsterAnimationType.cs` 追加 `MonsterAnimationSequence`：15 帧呼吸序列
  （`monster_anim.cpp:31-39`）。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Monster::GetAttack/GetDefense/GetShots/GetMorale/GetLuck/GetRace` 等逐项 getter | `MonsterStatistics.GetBattleStats` / `GetGeneralStats` 返回结构体 |
| `Monster::ICNMonh()` | `MonsterIcnIds.GetDwellingIcn()` |
| `RandomMonsterAnimation` 在 `monster_anim.cpp` | 落 `Battle/`：它整个依赖 `AnimationReference`，那个类是 `battle_animation.cpp` 的移植，也在 Battle 层；放 Monsters 会让两个目录互相引用 |

## 2026-11-21 S2 `spell`

4 个 cpp：`Spell`（31 个成员）、`SpellStorage`（6 个）、`SpellBook` 状态机（19 个中的 4 个 + 端口拆出的 6 个
导航方法）、`spell_info` 全部有落点。

补：

* `Core/Imaging/ImageOps.Shadows.cs`：`MakeShadow` + `AddShadow`（`engine/image.cpp:2665-2706`、`949-959`）。
  这是**引擎层**的缺口，`spell_book.cpp` 画书脊阴影要用；端口已有 `ApplyTransform` 与 `AddGradientShadow`，
  唯独缺这两个。
* `UI/Dialogs/SpellBookView.cs`：`SpellBook::Open()` / `Edit()` 的落点。**骨架**——布局常量、签名、拒绝条件
  按参考写好，帧循环与绘制抛 `NotImplementedException`，留给 S13 与其它对话框一起做。
* `UI/Dialogs/SpellSelectDialog.cs`：`Dialog::selectSpell()` 的落点（属 S13，先立接口）。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `SpellBook::Open()` 自带 `LocalEvent` 事件循环 | 拆成「状态机（`SpellBook`）+ 帧（`SpellBookView`）+ 提示回调」三层 |
| `fheroes2::addShadow` 属 engine | 落 `Core.Imaging.ImageOps`，与 `ApplyTransform`、`AddGradientShadow` 同处 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S3 `resource`

四个小计划：`resource.cpp`、`artifact.cpp`、`artifact_info.cpp`、`artifact_ultimate.cpp`。补：

* `ResourceIconIndex.ObjectFrameOf` / `ResourceOfObjectFrame`：`Resource::GetIndexSprite()` 与
  `FromIndexSprite()`（`resource.cpp:366-414`）——地图资源堆占 `OBJN` 的 1、3、5…13 七个奇数帧，
  容易误当成界面的 0..6，注释里写清了。顺手把 `ResourceIconIndex` 里抄错的参考行号从 319-360 改成 416-467。
* `ResourceText.Description`：`Resource::getDescription()`（`resource.cpp:359-364`）那一段「本作有七种资源」。
* `ArtifactData` 补齐文案三件套：`Name`、`BaseDescription`、`DiscoveryEventDescription`，以及
  `GetDescription()`（`artifact_info.cpp:949-982`）与 `DiscoveryDescription`（`artifact.cpp:502-505`）。
* `Artifacts/ArtifactTexts.cs`：104 行文案表，由 `tools-output/gen_artifact_texts.ps1` 从
  `artifact_info.cpp:43-582` 直接生成（含 `%{name}`/`%{count}`/`%{spell}` 占位符的长句子手抄必错），
  构造时按编号套到数字表上。
* `Artifacts/ArtifactScroll.cs`：`ResolveRequestedSpell()`（`Artifact::SetSpell()`，`artifact.cpp:384-420`）
  与 `SpellOf()`（`getSpellId()` 的卷轴分支，`artifact.cpp:432-448`）。
* `UltimateArtifact.GetPuzzleMapSurface()`：接口，抛 `NotImplementedException`，依赖 `gui` 的 `GameArea`（S12）。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Artifact` 类（编号 + 附加值），`SetSpell()` 改自身 | 端口没有这个类：神器是 `ArtifactType`，卷轴法术由地图元数据/宝物栏各自存，于是落成两个纯函数 |
| `ArtifactData` 的 gettext 字符串 | 端口存英文原文，不做翻译（ADR-172） |
| `artifact.cpp` 里的 `ArtifactsBar::` 成员（`isMagicBook`、`messageMagicBookAbortTrading`、`Redraw` 等约 20 个） | 归 S12（正主是 `gui/artifacts_bar.cpp`） |
| `Resource::BoxSprite()`、`Dialog::selectArtifact()` | 归 S13 |
| `Funds`/`Artifact` 的 `operator<<`/`>>` | 归 S14 存档层 |

测试：`ThePlaceholdersOnlyCarryTheNoneBonus` 原本断言 0 号与 `ArtifactData.Empty` 是同一实例；现在 0 号
有了参考里的 "Invalid Artifact" 文案，改为断言「有名字、无加成、无诅咒」，哨兵 `ArtifactCount` 仍回落到
`Empty`。

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S4 `army`

四个 cpp 的函数落点全部命中（`army.cpp` 97 个、`army_bar.cpp` 34 个、`army_troop.cpp` 30 个、
`army_ui_helper.cpp` 7 个），只补了一个真缺口：

* `Army.StrongestTroop(Troop, Troop)`：`Army::StrongestTroop`（`army.cpp:1738-1741`），并把
  `Army.Arrangement.cs` 里原来内联的 lambda 排序改成用它。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `WeakestTroop`/`StrongestTroop`/`SlowestTroop`/`FastestTroop` 四个 `bool` 比较器 | 只留 `StrongestTroop`（返回 `Comparison` 需要的符号）；另外三个的判断已内联进 `GetSlowestTroop`/`GetFastestTroop` 的 lambda，再补公开方法就是死代码 |
| `Campaign::CampaignAwardData::getAllianceFleeingMessage` / `getBaneFleeingMessage` | 归 S15（`campaign_data.cpp:932-980`） |
| `getCurrentScenarioVictoryCondition`、`GetVersionOfCurrentSaveFile` | 归 S14 |
| `army_bar.cpp` 的 617 行界面 | 已在 `UI/Widgets/ArmyBar*.cs`，本次只核对未改动 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S5-1 `heroes_base.cpp`

`heroes_base.cpp`（533 行）的函数落点核对完毕。补：

* `Hero.HaveSpellPoints(Spell)` / `HaveMovePoints(Spell)`：参考是 `heroes_base.h` 的两行内联
  （`_spellPoints >= spell.spellPoints(this)`、`_movePoints >= spell.minMovePoints()`），端口落成方法，
  并让 `CanCastSpell` 复用前者。
* `Hero.SpellBookActivate()` / `SpellBookDeactivate()`：`heroes_base.cpp:206-219`。后者把书从宝物栏摘掉并清空
  已学法术（`_spellBook.Clear()`），与参考一致。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `HeroBase::EditSpellBook()` / `OpenSpellBook()` | 直接转调 `SpellBook`，界面的落点是 `UI/Dialogs/SpellBookView`（S2 骨架）；`Hero` 在 Game 层不能引用 UI |
| `HeroBase::getManaIndexSprite()` | 界面像素，归 S12 |
| `HeroBase::getVisibleMonstersAroundHero()` | 归 S8（`maps` 的视野计算） |
| `Skill::Primary::GetInitialSpell()` | 归 S5-3（`skill.cpp`） |
| `CanCastSpell()` 的 `res` 回执文案重载 | 未落：九段拒绝里「靠近海洋」「不是船长」要地图与船，等 S8/S5-9；现有版本保留四段判定 |
| `HeroBase` 类 | 端口没有独立基类，合并进 `Hero`（`Hero.Spells.cs` 等 partial） |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿（236 + 416 + 875 + 3027）。

## 2026-11-21 S5-2 `heroes.cpp`

`heroes.cpp` 的函数落点核对完毕。补：

* `Hero.IsShipMaster` / `SetShipMaster(bool)`：`heroes.cpp:1717-1720`，`SHIPMASTER` 位的读写，海战士气/幸运
  与 `GetMaxMovePoints()` 水路分支都看它。
* `Hero.InCastle(World)`：`heroes.cpp:1245-1256`，只比入口格（端口的 `Castle` 没有驻守英雄字段，AI 层也一直
  这么判），无主英雄直接返回空。
* `Hero.GetStartingExperience()`：`heroes.cpp:2009-2012` 的 40~90 随机值，改成由 `AllHeroes.Place` 显式设置，
  战役/《忠诚的代价》英雄给 5000。
* `Hero.GetScoutRoi(World)`：`heroes.cpp:1774-1780`，正方形视野，边长 `2 * 距离 + 1`。
* `Hero.GetMoraleWithModifiers(StringBuilder?)` / `GetLuckWithModifiers(StringBuilder?)`：
  `heroes.cpp:1038-1066` / `1074-1102`，基础值 + 技能 + 访问对象 + 军队 + 神器，`MAXIMUM_MORALE`/`MAXIMUM_LUCK`
  放在最后直接返回满值。`GetMorale()`/`GetLuck()` 改为委托调用。
* `Hero.GetNumOfTravelDays(World, int)`：`heroes.cpp:1806-1840`，逐日扣路线惩罚。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| 构造函数里 `_experience( _getStartingXp() )` | 端口构造函数保持零经验，掷点交给 `AllHeroes.Place`——地图读入会覆盖经验，构造时再掷一次白耗随机数 |
| `inCastle()` 还校验「城堡记的驻守英雄是我」 | 端口只比入口格（`Castle` 无驻守英雄字段） |
| 士气/幸运文案逐件列出神器 | 只写满值神器那一句；宝物栏只有合计数没有逐件名 |
| `GetObjectMoraleEffect`/`GetObjectLuckEffect` 的文案 | 只有数值，文案随提示框在 S13 |
| `heroes.cpp` 里 `Bridge`/`Recruits`/`Dialog` 相关（约 20 个） | 归 S12/S13 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S5-3 `skill.cpp`

`skill.cpp`（930 行）的函数落点核对完毕。补：

* `PrimarySkillDefaults.GetName(PrimarySkill)`：`Skill::Primary::String()`（`skill.cpp:262-280`），四句里力量叫
  "Spell Power"。
* `SecondarySkillLevel.WithSkill(SecondarySkill)`：`Skill::Secondary::SetSkill()`（`skill.cpp:375-378`），越界压成
  `Unknown`。
* `SecondarySkillLevel.WithLevel` / `NextLevel`：`SetLevel()`/`NextLevel()`（`skill.cpp:380-400`）。
* `SecondarySkills.GetFullName(skill, level)`：`Skill::Secondary::GetName()`（`skill.cpp:457-471`）那张四十二格表
  的等价物，非法时 "unknown"。
* `SecondarySkills.GetLevelName(SkillLevel)` / `GetLevelNameWithBonus(skill, bonus)`：`Skill::Level::String()` 与
  `StringWithBonus()`（`skill.cpp:325-350`），招魂补 `"+N"`。
* `SecondarySkills.GetNameWithBonus(skill, bonus)`：`Skill::Secondary::GetNameWithBonus()`（`skill.cpp:473-479`），
  招魂补 `" (+N)"`。
* `SecondarySkills.GetLeadershipModifiers` / `GetLuckModifiers`：`skill.cpp:852-881`，数值 + `"Leadership +1\n"`。
  英雄的士气/幸运改用它们，不再各写一遍。
* `SecondarySkills.GetNecromancyBonus(hero, world)` / `GetNecromancyPercent(hero, world)`：`skill.cpp:882-896`，
  神龛 + 宝物封顶 7 点、百分比封顶 100%。`BattleLoader` 里内联的同一套计算删掉，改调这两个函数。
* `SecSkills.ToString()`：`Skill::SecSkills::String()`（`skill.cpp:763-773`），保留末尾的 `", "`。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Skill::Secondary::GetDescription(hero)` 数百行长文案 | 归 S13（提示框统一处理） |
| `Skill::Primary::StringDescription(skill, hero)` | 归 S13 |
| `SecSkills::FindSkill` 返回可改指针 | 端口集合不可变，用 `GetLevel()` 查询；改等级走 `AddSkill` |
| `SecSkills::ToVector()` | 端口 `Skills` 属性本身就是列表 |
| `Skill::operator<<`/`operator>>` | 归 S14 存档层 |
| `GetNecromancyBonus` 读全局 `world` | 端口把 `World` 作为参数 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S5-4 `heroes_action.cpp` 与 S5-5..S5-9

`heroes` 目录剩下的六个 cpp 逐个核对了函数落点，结论一致：模型层已经在 S5-1..S5-3 里了，剩下的是音频与
界面。补的两处：

* `Maps/MineText.GetMineName(int)`：`Maps::GetMineName()`（`maps.cpp:226-246`）。七种资源里只有矿石、硫磺、
  水晶、宝石、黄金有矿名，木材与水银回落到通用的 `"Mine"`——不是「资源名 + Mine」的拼接。`heroes_action.cpp`
  的矿提示框要用它。
* `Hero.ActionSpellCast(Spell, World)`：`Heroes::ActionSpellCast()`（`heroes_spell.cpp:305-436`）的入口，抛
  `NotImplementedException`——它的每条分支都是「在地图上点目的地 + 动画 + 提示框」，随冒险地图归 S12。
* `Luck.GetLabel(int)` / `Morale.GetLabel(int)`：`LuckString()` / `MoraleString()`
  （`heroes_indicator.cpp:47-67`），"Good Luck" 与 "Good Morale"，士气最高档特判成 "Blood Morale"。
* `CanCastSpell` 补齐参考的后两道拒绝：船长不能施冒险法术、船长不能再召一条船（`SHIPMASTER` 位在 S5-2 已落）。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `heroes_action.cpp` 的 `canBePlayed`/`PlayPickupSound`/`setVolume`/`EnvironmentSoundMixer` | 音频系统，归 S12 |
| `updateAdventureMapAnimationIndex`/`updateMapFogDirections`/`UpdateSoundsOnFocusUpdate`/`isDelayNeeded` | 界面刷新，归 S12 |
| `heroes_move.cpp` 的 `AIHeroAnimSpeedMultiplier` | 界面，归 S12 |
| `heroes_meeting.cpp` 的 `MeetingDialog`/`OpenHeroesDialog`/`RedrawPrimarySkillInfo` 等 8 个 | 对话框，归 S13 |
| `heroes_dialog.cpp` 的 `selectHeroes`/`renderHeroRacePortrait`/`getLocalizedText` 等 7 个 | 界面与翻译层，归 S13；端口不做翻译（ADR-172） |
| `heroes_indicator.cpp` 的 `ExperienceIndicator`/`SpellPointsIndicator`/`HeroesIndicator` | 界面指示器控件，归 S12 |
| `heroes_indicator.cpp` 的 `GetStartingXp` | 等价于 `Hero.GetStartingExperience()`（S5-2 已落） |
| `route.cpp`、`heroes_recruits.cpp` 的 `GetVersionOfCurrentSaveFile` | 归 S14 |

## 2026-11-21 S6 `castle`

11 个 cpp（约 9 000 行）逐文件核对了函数落点。`castle.cpp` 的 55 个 `Castle::` 成员里，端口 `Castle/` 目录
只差 `loadFromResurrectionMap`（依赖 `Maps::Map_Format::CastleMetadata`）。其余文件里：

* `castle_building.cpp` 只缺 `redrawAllBuildings`（界面重绘）。
* `castle_building_info.cpp`、`mageguild.cpp`、`castle_mageguild.cpp`、`castle_tavern.cpp` 无缺口。
* `buildinginfo.cpp` 缺 `DialogBuyBuilding`、`SetStatusMessage`（对话框与状态栏）。
* `captain.cpp` 缺 `getItemWidth`/`getItemHeight`/`PortraitRedraw`（布局与重绘）。
* `castle_town.cpp` 缺 `DialogBuyCastle`；`castle_dialog.cpp` 缺 6 个界面函数；`castle_well.cpp` 缺 2 个。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `GameStatic::GetCastleGrownWell/Wel2/WeekOf/MonthOf`（`game_static.cpp:145-163`，四个常量函数） | 端口落成 `Castle.GrownWell`/`GrownWel2`/`GrownWeekOf`/`GrownMonthOf` 常量 |
| `AllCastles::getMaximumAllowedCastles()` | `AllCastles.MaximumAllowed` 常量（72） |
| `Maps::getBuildingsFromVector`、`Maps::loadCastleArmy`、`Castle::loadFromResurrectionMap` | 归 S8（`map_format_info.cpp` 的建筑位图与军队解析） |
| `redrawAllBuildings`、`PortraitRedraw`、`passAnimationDelay`、`fadeInDisplay` 等 | 归 S12 |
| `DialogBuyBuilding`、`DialogBuyCastle`、`OpenHeroesDialog`、`selectHeroes` | 归 S13 |
| `getLanguageFromAbbreviation`/`getLocalizedText` | 翻译层，端口不做（ADR-172） |
| `GetVersionOfCurrentSaveFile` | 归 S14 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S7 `kingdom`

14 个 cpp 逐个核对了成员函数与自由函数两遍。模型层齐：

* `color.cpp`、`experience.cpp`、`luck.cpp`、`morale.cpp`、`payment.cpp`、`profit.cpp`、`race.cpp`、
  `resource_trading.cpp`、`speed.cpp`、`week.cpp`、`kingdom.cpp` 无缺口。
* 本轮补的 `Luck.GetLabel`/`Morale.GetLabel` 与 `SecondarySkills.GetNecromancyBonus/Percent` 都在这个目录的
  调用链上（`kingdom.cpp` 的 `GetCountNecromancyShrineBuild` 已被后者用到）。
* `kingdom_overview.cpp`、`puzzle.cpp`、`view_world.cpp` 缺的全是控件与绘制（`StatsCastlesList`、
  `drawPuzzle`、`buttonZoom` 之类），归 S12/S13。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Castle::PredicateIsBuildBuilding`（`castle.h:360`，`std::count_if` 的空指针保护辅助） | 端口用 `CountBuildings(BuildingType)`，C# 没有空元素 |
| `kingdom_overview.cpp` 的 `KingdomOverviewDialog` 等 14 个 | 归 S13（`UI/Dialogs/KingdomOverview*` 新建） |
| `puzzle.cpp` 的 `ShowMapsDialog`/`RenderRegular`/`drawPuzzle` | 归 S12/S13；`Puzzle` 模型与存档已在 `Puzzle.cs`、`Puzzle.Persistence.cs` |
| `view_world.cpp` 的 `_updateCenter`/`GameBorderRedraw`/`buttonZoom` | 归 S12 |
| `GetVersionOfCurrentSaveFile` | 归 S14 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S8 `maps`

17 个 cpp（约 15 000 行）。`mp2.cpp`、`mp2_helper.cpp`、`map_object_info.cpp`、`map_format_info.cpp` 无缺口；
`maps.cpp`、`ground.cpp`、`maps_tiles_helper.cpp` 补了几样：

* `World.GetVisibleMonstersAroundHero(Hero)`：`Maps::getVisibleMonstersAroundHero()`（`maps.cpp:453-462`），
  按 `Hero.GetVisionsDistance()` 扫 `ActionMonster`，再用 `MapFog.IsFogged` 剔掉没看见的格子。
* `MapGrounds.GetRandomTerrainImageIndex(MapGround, bool)`：`Maps::Ground::getRandomTerrainImageIndex()`
  （`ground.cpp:244-291`），水四变体、带小物件段按地形分支、普通段 `Rand(7)`。
* `MapFog.UpdateDirections`（已有）：`Maps::updateFogDirectionsInArea()`（`maps_tiles_helper.cpp:2086-2212`）
  的等价物，端口按整图重算而不是逐区域补位。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Maps::updateFogDirectionsInArea` 逐区域补位 | `MapFog.UpdateDirections` 整图重算，语义相同、调用点少 |
| `Tile::getObjectPartWithFlag` | `Tile.UpdateFlag` 内联「按 uid + `Flag32` 找」，没有独立的查找函数 |
| `Maps::Ground::getRandomTerrainImageIndex` 的编辑器调用点 | 编辑器走 `TerrainTransitions.PlainImageIndex` 的确定性变体（笔刷必须可复现） |
| `maps_fileinfo.cpp` 的 `loadBaseMap`/`loadResurrectionMap`/`readResurrectionMap`/`loadTile`/`FillUnions`/`KingdomRace` | `Map_Format` 到 `World` 的桥；本项目走 MP2 直读，不建这条桥 |
| `maps_fileinfo.cpp` 的 `getEditorMapFileInfos`/`tryGetMatchingFile`/`compareStringsCaseInsensitively` | 归编辑器 E 系列 |
| `getCurrentLanguage`/`setInGameLanguage` | 翻译层，端口不做（ADR-172） |
| `map_format_helper.cpp` 的 `setObjectOnTile`/`addObjectToMap`/`updateRoadOnTile`/`doesContainRoad`/`getRiverDeltaDirectionByIndex` | 编辑器对象放置与道路刷新，归 E 系列 |
| `map_random_generator.cpp`/`_info`/`_helper`（约 4 700 行） | **旁支推迟**：整套建立在 `Map_Format` 上，而本项目不建 `Map_Format`（`PLAN.md:1014`）；`PLAN.md:800` 也把它列为不计入主线的旁支 |
| `maps_tiles_render.cpp` 的 `getAdventureMapAnimationIndex` | 归 S12 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S9 `world`

5 个 cpp（约 4 300 行）。模型层已齐，本轮无新增代码——逐个函数核对的结果是它们都落过了：

* `world.cpp`（90 个函数）：`RemoveMapObject` 由 `MapObjects.Remove(tileIndex)` 承担，`IsAnyKingdomVisited`
  遍历全部七个王国槽（空槽恒假，等价于参考按 `Game::GetKingdomColors()` 过滤），其余无缺口。
* `world_loadmap.cpp`（52 个函数）：`readAllTiles`/`loadTile`/`loadAddon`/`updateMapPlayers`/`getTownColorIndex`
  全部内联在 `World.LoadMap`（`World.cs:1434-1544`）与 `Mp2Map.EnumerateAddonChain` 里。
* `world_pathfinding.cpp`（60 个）、`world_regions.cpp`（12 个）、`world_object_uid.cpp`：无缺口。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `World::RemoveMapObject(const MapBaseObject *)` | `MapObjects.Remove(tileIndex)` |
| `World::isAnyKingdomVisited` 先取 `Game::GetKingdomColors()` | 直接遍历七个王国槽，空槽恒假 |
| `world_loadmap.cpp` 的 `getCurrentLanguage`/`loadTranslation` | 翻译层，端口不做（ADR-172） |
| `world_loadmap.cpp` 的 `tryAddDebugHero` | 调试后门，端口没有 |
| `world_loadmap.cpp` 的 `loadResurrectionMap`/`_processNewResurrectionMap` | 《忠诚的代价》对战模式的复活地图，随战役包 |
| `world.cpp` 的 `Campaign::getCurrentScenarioVictoryCondition`/`LossCondition` | 战役模块，归战役包 |
| `GetVersionOfCurrentSaveFile` | 归 S14 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S10 `battle`

18 个 cpp。内核 14 个（`battle_arena`、`battle_army`、`battle_troop`、`battle_board`、`battle_pathfinding`、
`battle_catapult`、`battle_bridge`、`battle_cell`、`battle_tower`、`battle_command`、`battle_grave`、`battle_magic`、
`battle_summon`、`battle_interface_settings` 的模型部分）无模型层缺口。剩下四处缺口全是界面或动画依赖：

* `battle_army.cpp` 的 `GetKilledTroops()` 只在 `battle_dialogs.cpp:587` 用（战后伤亡对话框），要配 C++ 的 `Troops`
  容器，随 `battle_dialogs.cpp` 一起做。
* `battle_army.cpp` 的 `animateIdleUnits()`/`resetIdleAnimation()` 是动画驱动。
* `battle_troop.cpp` 的 `isIdling()`/`GetCenterPoint()`/`GetStartMissileOffset()` 依赖 `Assets::getImage` 与
  `animation.getProjectileOffset`。
* `battle_arena.cpp` 的 `GetArena()`/`GetInterface()` 是全局单例访问器，端口走参数传递；墓地三件
  （`getCellsOccupiedByGraveyard`/`getGraveyardUnits`/`getLastUnitFromGraveyard`）已由
  `BattleGraveyard.GetOccupiedCells`/`GetUnits`/`GetLastUnit` 承担。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Battle::GetArena()`/`GetInterface()` 全局单例 | 参数传递 |
| `battle_arena.cpp` 的墓地三个转发函数 | `BattleGraveyard` 上的同名方法 |
| `battle_interface.cpp`（6 144 行）、`battle_interface_settings.cpp`（283 行） | 归 S12 |
| `battle_dialogs.cpp`（993 行）、`battle_only.cpp`（681 行） | 归 S13 |
| `battle_troop.cpp` 的 `M82::*` 音效名 | 归 S12 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S11 `ai`

9 个 cpp（约 9 000 行）。`ai_common.cpp`、`ai_planner*.cpp`、`ai_personality.cpp` 无缺口；`ai_battle.cpp`、
`ai_battle_spell.cpp`、`ai_planner_hero.cpp` 只差 `fabs`/`make_tuple`/`GetArena`/`GetInterface`（标准库与单例）。
补的两个开关（`difficulty.cpp` 的一小段，AI 与守卫法术都要）：

* `Difficulty.GetGuardianSpellMultiplier(GameDifficulty)`：`difficulty.cpp:272-292`，16/14/12/10/8，越界
  `assert( 0 )` 后返回 20。
* `Difficulty.IsArtifactSortingAllowedForAi(GameDifficulty)`：`difficulty.cpp:350-361`，Easy 与 Normal 不让 AI
  整理宝物栏。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `ai_hero_action.cpp` 的 `AIHeroAnimSpeedMultiplier`/`isDelayNeeded`/`updateAdventureMapAnimationIndex`/`updateMapFogDirections` | 界面刷新与动画节奏，归 S12 |
| `ai_hero_action.cpp` 的 `interruptAutoPlaytest` | 自动对局测试，端口没有 |
| `fabs`/`make_tuple`/`GetArena`/`GetInterface` | 标准库与单例，端口用参数传递 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S12 `gui`

33 个 cpp（约 19 000 行，`ui_font.cpp` 一个就 6 317 行）。端口 `FHeroes2.UI` 有自己的控件与场景架构
（`Widgets/`、`AdventureMapScene`、`AdventureMapView`），不 1:1 移植 `Interface::AdventureMap` 的成员，
所以 `interface_events.cpp`（缺 37）/`interface_gamearea.cpp`（缺 28）/`ui_tool.cpp`（缺 23）这些缺口不是「没做」，
而是端口用另一套组织方式重写了。本轮补的是对话框元素——`DialogElement.cs` 里缺的五个：

* `LuckDialogElement` / `MoraleDialogElement`：`ui_dialog.cpp:548-600`，帧由调用方挑（幸运 0/1、士气 2/3）。
* `ExperienceDialogElement`：`ui_dialog.cpp:602-640`，图标 + 下挂数字，数字为空时就是图标。
* `PrimarySkillDialogElement`：`ui_dialog.cpp:642-706`，图标居中，技能名（`y + 10`）与数值（`y + 82`）。
* `SecondarySkillDialogElement`：`ui_dialog.cpp:708-770`，图标上方技能名（`y + 8`）、下方等级名（`y + 56`），
  两行都按图标宽度裁剪。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `DialogElement::drawBackground()` 自绘白底与边框 | 背景精灵由调用方传入（`ArtifactDialogElement` 定下的风格） |
| 元素的 `processEvents` 弹 `showStandardTextMessage` | 元素返回 `DialogPopup`，由对话框宿主负责真的弹窗 |
| `MonsterDialogElement` | 需要 `fheroes2::drawMonsterLine()`（怪物图标 + 数量一行），端口还没有，缓做 |
| `CustomAnimationDialogElement` | 依赖 `fheroes2::CustomImageDialogElement` 的动画帧，缓做 |
| `interface_*.cpp` 的 `Interface::AdventureMap` 成员 | 端口 `AdventureMapScene`/`AdventureMapView` 自己的事件与重绘 |
| `ui_font.cpp` 的 `Font` 渲染 | 端口已有字形渲染（`Assets/Fonts`），未逐函数对拍 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S13 `dialog`

29 个 cpp（约 10 000 行）。端口 `UI/Dialogs` 已覆盖绝大部分对话框（招募、市场、盗贼行会、升级、城堡、战场
等），缺口按归属分三类：

* 热键系统（`dialog_hotkeys.cpp` 缺 7 个：`getAllHotKeyEvents`/`HotKeySave`/`getHotKeyForEvent` 等）→
  归 S14 的 `game_hotkeys.cpp`，和 `Game::HotKeyEvent` 表一起做。
* 存档文件路径与 SAV 头信息（`dialog_selectfile.cpp` 缺 `GetSaveDir`/`GetSaveFileBaseName`/`GetStem`/
  `GetLastSaveName`/`LoadSAV2FileInfo`/`Unlink`）→ 归 S14 的 `game_io.cpp`。
* 纯绘制与翻译（`dialog_selectitems.cpp` 缺 35 个选择器窗口、`dialog_selectscenario.cpp` 缺 11 个渲染函数、
  `_getWinConditionsIcon` 等）→ 端口 `Dialogs/` 自己的对话框实现，缺口不逐个对齐。
* `dialog_random_map_generator.cpp` 缺 5 个 → 随机地图生成器旁支（同 S8 记录）。

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S14 `game`

20 个 cpp（约 11 000 行，`game_assets.cpp` 一个就 5 000 行）。这个目录是「一局游戏的外壳」：主循环与场景切换、主菜单/新游戏/读档菜单、胜利失败对话框、
视频与片头、制作人员滚动、热键表、存档读写、高分榜、动画节拍表。端口对应的是 `FHeroes2.App` 的 `GameHost` 与 `FHeroes2.UI` 的各
`*Scene`，不 1:1 移植 `Game::MainLoop`，所以**本轮只补模型层，界面部分按偏差记账**。

补上的模型层（五个文件）：

* `System/GameDelays.cs`（`game_delays.h` + `game_delays.cpp`）：`DelayType` 全表 + `TimeDelay` 表 + `initAnimateDelays`/
  `UpdateGameSpeed`/`ApplyBattleSpeed`/`HasEveryDelayPassed`/`IsDelayNeeded`/`ValidateAnimationDelay` 等全量。战斗速度的缩放
  公式原样保留（含「速度 10 只给 1/3 步，只为不为 0」与待机动画 `(28 - speed) / 22.5` 的温和曲线），英雄速度的「低速度一格一格、
  高速度一帧多格」的乘数表也照抄。端口此前各控件自持 `TimeDelay`，这张共享表是新的地基。
* `Highscores/HighscoreData.cs` + `Highscores/HighScoreDataContainer.cs`（`highscores.h/.cpp` 全量）：两版文件格式（魔数
  `0xBADC0DE`/`0xBADC1DE`，第一版无语言字段）、压缩读写、`registerScoreStandard/Campaign`（同一次通关只替换不新增）、
  两张默认榜、66 个兵种的评分表与天数表。战役名 `Roland`/`Archibald` 用字面量（端口还没有 `Campaign` 模块，S15 再接）。
* `System/GameSave.cs`（`game_io.cpp` 的命名部分）：`0xFF03` 魔数、`AUTOSAVE`/`QUICKSAVE`、当前文件格式版本、上次存档名、
  `GetSaveFileBaseName()` 的四步清洗（非 ASCII → `!`、删不可打印、非字母数字 → `_`、空则 `newgame`）、
  `GetSaveFileExtension()` 的 `.sav`/`.savc`/`.savh`/`.savm` 判定顺序。
* `LocalizedString.Write/Read`（`game_string.cpp`）：文本 + `optional<SupportedLanguage>` 的序列化次序（先布尔再一字节）。

记下的偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Game::MainLoop`、`game_interface.cpp`、`game_startgame.cpp` 的主循环与回合推进 | 端口 `GameHost` + 各 `*Scene` 自己的循环；`hotSeatAIFogColors` 的迷雾配色随 AI 回合走 |
| `game_mainmenu.cpp`/`game_newgame.cpp`/`game_loadgame.cpp`/`game_credits.cpp`/`game_intro.cpp`/`game_exit.cpp` | 端口 `MainMenuScene` 等自建界面，不逐函数对齐 |
| `game_video.cpp`（SMK 播放与字幕） | 端口无视频播放器，只做资材与音轨接口（S0 `audio`） |
| `game_hotkeys.cpp`（`HotKeyEvent` 表、`fheroes2.key` 配置读写、`globalKeyDownEvent`） | 端口各场景直接判键（`CastleScene`、`ConstructionWindow` 等注释里标了参考行号）；**S13 里「热键归 S14」的 7 个函数因此仍不做**，一起归入输入层 |
| `Game::Save`/`Load`/`LoadSAV2FileInfo`/`AutoSave`/`QuickSave`/`SaveCompletedCampaignScenario`、`Game::GetSaveDir` | 签名与文档已写，方法体抛 `NotImplementedException`：缺 `World`/`Settings`/`GameOver::Result` 的序列化，也缺「用户数据目录」这个概念（端口连设置文件都没落盘） |
| `game_over.cpp` 的 `DialogWins`/`DialogLoss`/`standardGameResults` 文本拼装 | 胜利条件判定已在 `World.Conditions.cs`，文本由端口界面自己组织 |
| `game_scenarioinfo.cpp` 的 `updatePlayers`、`game_auto_playtest.cpp`（556 行） | 前者是新游戏界面的热座人数调整，后者是编辑器自动跑图工具，都随界面（后者随 S16） |
| `game_assets.cpp` 的 `IsValidICNId`/`IsValidTILId`/`isLanguageDependentIcnId`/`useOriginalResources` | 端口资材层按 `IcnType` 强类型索引，没有「编号合法性」这一层；多语言资材随 ADR-172 不做 |
| `game_tools.cpp` 的 `Game::getDateDescription()` | 端口 `World.DateString()` 给 `month/week/day`，日期文本由界面拼 |
| `game_static.cpp`（约 130 个常量） | 全部已有落点（`Castle`/`World`/`Kingdom`/`Maps` 各自的常量类），逐条核对无缺 |
| `difficulty.cpp` | `System/Difficulty.cs` 上一轮已补齐（守卫法术乘数、AI 神器排序开关） |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S15 `campaign`

6 个 cpp / 2 700 行，是六个战役的全部数据：关与关的跳转图、每关的地图名、标题、正文、开场与收场视频、
额外的胜利/失败条件、开场二选一奖励、通关奖励，以及战役存档。数据全是硬编码表，逻辑只有「查表 + 串起来」。

新增 5 个文件：

* `Campaign/CampaignTypes.cs`：`CampaignId` 六个战役编号、`ScenarioVictoryCondition`/`ScenarioLossCondition`
  （STANDARD / CAPTURE_DRAGON_CITY / OBTAIN_ULTIMATE_CROWN / OBTAIN_SPHERE_NEGATION / LOSE_ALL_SORCERESS_VILLAGES）、
  `CampaignVideoInfo`（文件名 + `VideoControl` + 字幕坐标）、`CampaignVideoSequence`、`ScenarioInfoId` 值对象（带比较与 `ToString`）。
* `Campaign/CampaignSaveData.cs`（`campaign_savedata.h/.cpp` 全量）：当前关、当前战役、携带的部队、已完成关卡位图、
  上次奖励编号、`Reset`、序列化（含两个老版本的兼容分支）。
* `Campaign/CampaignAwardData.cs`（`campaign_data.cpp:44-170` 与 `campaign_scenariodata.cpp` 的名字部分）：
  通关奖励（`AwardType`：资源/神器/兵种/法术/主技能/副技能）、三个战役的奖励表、`GetCampaignName`、
  兵种与神器的「战役内名字」、`GetMonsterCountName` 的三个消息变体。
* `Campaign/ScenarioData.cs`（`campaign_scenariodata.h:121-175` + 实现）：关卡名、正文、地图文件名（一律小写）、
  开场/收场视频序列、额外胜负条件、下一关列表；构造时自动从奖励表取本关奖励；`FindMapFile`/`LoadMap`。
* `Campaign/CampaignData.cs`：`GetCampaignData` 六个战役的懒加载单例、`GetScenariosAfter`、`IsStartingScenario`
  （没有任何关指向它即为起始关）、`GetStartingScenarios`、`IsAllCampaignMapsPresent`、`IsLastScenario`、
  `AreAllAiPlayersAllied`（Roland 第 10 关、Archibald 第 6/11 关的 AI 强制同盟）与 `UpdateScenarioGameplayConditions`。
* `Campaign/CampaignScenarioBonusData.cs` + `CampaignScenarioBonusTables.cs`：开场奖励模型与六张表
  （共 47 关的 2~3 个选项，含负数资源奖励与带法术的卷轴）。

数据里值得记住的几处：

* 两个原版战役在第 4 关之后分叉，且「叛变」（`CAMPG05B`/`CAMPE05B`）通关后跳到**对方战役**的第 6 关——两个战役共用一张图。
* Archibald「Necromancers」「Slay the Dwarves」两关的收场视频各有两条并存的序列（叛变与不叛变），参考按顺序都塞进表里。
* Descendants 第 6 关的三笔资源奖励是**负数**（这一关的「奖励」是扣资源），参考特意写注释提醒别改。
* 开场奖励里 `SKILL_SECONDARY` 的 `amount` 存的是技能等级（1/2/3），与 `SkillLevel` 数值一致，不是数量。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Maps::tryGetMatchingFile` 在全局数据目录里按文件名找地图 | `ScenarioData.FindMapFile(rootDirectories)` 把搜索目录当参数（资材层没有全局设置对象），内部用 `MapCatalog.FindFiles` 比对文件名 |
| `updateScenarioGameplayConditions` 直接改写 `Maps::FileInfo` 的 `unions` | `Mp2FileInfo` 尚未解析联盟表（S8 只读了胜负条件与玩家颜色），该方法保留签名，命中强制同盟的场景时抛 `NotImplementedException`；`AreAllAiPlayersAllied` 已实现并有测试落点 |
| `CampaignData` 的 `setCampaignID`/`setCampaignScenarios` 两步构造 | 私有构造 + 六个 `Lazy<CampaignData>`，对外只暴露 `Id` 与 `AllScenarios` |
| `getScenarioName()` 返回 `const char*` | `ScenarioData.ScenarioName` 属性 |
| `Campaign::getCampaignDescription()`（战役简介，原文在 `campaign_data.cpp` 里按语言拼） | 界面层文案，随 ADR-172 不逐语言移植；端口战役选择界面自己组织 |
| `CampaignSaveData` 里的 `Game::GetVersionOfCurrentSaveFile()` 版本分支 | 端口按显式格式版本号分支（`SaveFileFormat`），不读全局存档版本 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S16 `editor`

20 个 cpp / 17 个 h，约 11 000 行，但**几乎全是窗口**：`editor_interface.cpp` 3 015 行、`editor_map_specs_window.cpp` 2 005 行、
`editor_interface_panel.cpp` 1 341 行、`editor_castle_details_window.cpp` 883 行，剩下十几个都是 200~400 行的对话框。
按 PLAN 第 6 条，editor/ 排在最后，且端口的编辑器是自建界面（`FHeroes2.Game/Editor` 已有 `MapEditorView`、`MapEditSession`、
`EditorButtonSet`、`TerrainTransitions`、`BrushSize`、`EditorTool`、`EditorUiHelper`），不 1:1 复刻 `Editor::Interface`。

本轮补的模型层只有一个文件，但它是编辑器唯一真正「有逻辑」的部分：

* `Editor/HistoryManager.cs`（`history_manager.h` + `history_manager.cpp` 全量）：`MapAction` 抽象类、`HistoryManager`
  （999 步上限、重做分支作废、`SetStateCallback` 两个可用性标志、`Reset`/`Add`/`Undo`/`Redo`）、
  `SnapshotMapAction`（整图动作：改前改后两份快照 + 对象编号计数器）、`MetadataMapAction<T>`（字典级动作）、
  `ActionCreator`（`IDisposable`：没 `Commit` 就当没发生，析构即回滚）。

为了不改动地图层，整图快照抽成 `IMapSnapshotProvider`（`Capture` / `Restore`）：参考直接在
`Maps::Map_Format::MapFormat` 上 `saveMap`/`loadMap`，端口把「怎么存、怎么还原」留给编辑器实现。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `GenericMapAction` 用 `RWStreamBuf` + `Map_Format::saveMap/loadMap`，再 `Maps::readMapInEditor` 重建编辑器结构 | `IMapSnapshotProvider` 的两个方法；地图序列化仍是 `Mp2MapWriter` 的事 |
| `ActionCreator` 析构函数里回滚未提交的动作 | `IDisposable` + `using`，语义相同但时机显式 |
| `ActionCreator` 支持 10 种粒度（整图 + 9 种元数据表），直接取 `mapFormat.*Metadata` | 端口地图格式没有「按对象编号存的元数据表」，`ActionCreator` 只接整图；`MetadataMapAction<T>` 已实现（可在有字典的地方直接用），10 个 `ActionType` 常量照留 |
| `MapEditSession` 已有的逐格撤销（`MapTileChange`，200 步） | 与 `HistoryManager` 并存：前者是绘制笔画内部的细粒度，后者是整图快照级（`MapEditSession` 尚未接上 `HistoryManager`，留给编辑器界面里程碑） |
| `editor_interface.cpp`/`editor_interface_panel.cpp`/`editor_map_specs_window.cpp`/`editor_castle_details_window.cpp`/`editor_daily_event*.cpp`/`editor_event_details_window.cpp`/`editor_sphinx_window.cpp`/`editor_rumor_window.cpp`/`editor_object_popup_window.cpp`/`editor_save_map_window.cpp`/`editor_spell_selection.cpp`/`editor_secondary_skill_selection.cpp`/`editor_language_window.cpp`/`editor_mainmenu.cpp`/`editor_options.cpp` | 端口自建编辑器界面，按功能逐步实现（`MapEditSession` 已覆盖绘制/落物/擦除/存盘），不逐函数对齐 |
| `editor_ui_helper.cpp`（复选框与资源栏绘制） | `EditorUiHelper` 已有同功能三个方法 |
| `editor_ui_helper.h` 的 `Checkbox` 类（带颜色的可切换复选框） | 端口用 `Checkbox` 记录 + `EditorUiHelper.CreateColorCheckboxes`，不搬类 |

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。

## 2026-11-21 S16 追加 `editor_mainmenu.cpp`（界面改为按参考移植）

编辑器主菜单不再沿用端口自写的那一套，改成逐函数对照 `editor_mainmenu.cpp` 移植
（`src/FHeroes2.App/Scenes/EditorMainMenuScene.cs`）：`EDITOR.ICN` 的石头窗口 640x480、窗口框
184x465 落在 (432, 24)、宝石角、按钮热键、右键 tooltip 全部按参考摆位。`GameHost` 新接了三条路：
鼠标移动转逻辑坐标、左右键按下/抬起、未重复的按键走热键选择。

按钮表是个坑：参考在这里用的是 `ICN::BUTTONS_EDITOR_MENU_GOOD` 的 20 帧，但资源包里**没有**这张表
（`Heroes2.agg` 与扩展包都查不到 `BTNE*` 条目）。参考自己在 `game_assets.cpp:1724-1777` 运行时拼，
端口照做：

* 0..3 取 `BTNEMAIN.ICN`；
* 4..7 是文字按钮：`SYSTEM.ICN` 的第 11/12 帧空白按钮拉伸到标签大小，再印 `MAIN\nMENU`（4、5）与
  `BACK`（6、7），对应 `makeButtonSprites()`；
* 8..11 取 `BTNENEW.ICN`（"NEW" 形按钮）；
* 12..19 取 `BTNESIZE.ICN`（四档地图尺寸按钮）。

新增 `UI/Widgets/TextButtonArtwork.cs`：`getCustomNormalButton()` / `makeButtonSprites()` 的移植——
宽 = 文本区 + 10 并夹在 16..200、高夹在 25..200，两次 `ButtonArtwork.Resize`，再
`ButtonArtwork.MakeTransparentBackground()` 把按下时才露出的角用石头纹理补上，最后 released 用
`left: 8`、pressed 用 `left: 7, top: 1` 居中印字。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `addButtonShine()`（空白按钮的高光小点） | 未移植，纯装饰；文字按钮看起来比参考少几粒亮点 |
| `getCustomNormalButton()` 里对空白按钮的单像素 `Fill()` | 未移植，同上 |
| 宽度下限可以压到 16 像素 | 端口停在空白按钮自身宽度：`ButtonArtwork.Resize()` 目前切不出比两端块更窄的按钮，触到会抛 `ArgumentOutOfRangeException` |
| 空白按钮取 `EMPTY_GOOD_BUTTON` / `EMPTY_EVIL_BUTTON` | 这里按 `game_assets.cpp` 的编辑器分支取 `SYSTEM.ICN` 11/12 帧（编辑器只有石头界面） |

测试：新增 `tests/FHeroes2.UI.Tests/TextButtonArtworkTests.cs` 六例（尺寸、高度夹取、窄标签保空白宽度、
标签确实印上、pressed 透明角被石头纹理填成不透明、null 标签抛异常），用 3x3 实心字形的 `StubFont`
不依赖资源包。

验证：`dotnet build FHeroes2Sharp.slnx` 0 错误 0 警告；`dotnet test` 4 601 全绿
（Core 236 + Assets 416 + UI 881 + Game 3 068）；冒烟
`--scene menu --cursor 565,380 --left-click --smoke-frames 30` 打出
`Editor main menu ready: EDITOR.ICN 640x480, window 184x465 at (432, 24), 20 button frames ...`
与 `Opened the editor main menu.`

## 2026-11-21 M2 追加 `game_mainmenu.cpp`（编辑器招牌改按参考取材）

主菜单右下角那个招牌以前是端口自己画的（棕色牌子 + `地图编辑器` 文字，`EditorFallbackArea`），
参考不是这么做的。原因在资源：`BTNSHNGL.ICN` 只有 20 帧（0..19），编辑器按钮的第 20、21、22 帧
**不在任何 ICN 里**，`case ICN::BTNSHNGL`（`game_assets.cpp:3471-3479`）在加载时从
`resurrection.h2d` 追加三张 `main_menu_editor_{released,highlighted,pressed}_button.image`；
`case ICN::HEROES`（同文件 3464-3467）再把 `main_menu_editor_icon.image`（109x147，锚点
531,333）画进街景底图，就是那个画架。

按参考补齐：

* `GameHost.LoadH2DResources()`：启动时打开 `resurrection.h2d` 并发布 `H2DResources.Current`，
  退出时随宿主释放。参考的 `H2DInitializer` 把它当硬依赖（缺了就抛），端口降级为一条警告。
* `H2DResources.FindArchive()`：先探 `<data>/files/data/resurrection.h2d`，再探父目录的同一路径。
  必须这样是因为本机装法里 AGG 在 `Data\` 而 `files\` 在安装根目录，`MoCatalog.FindCatalog`
  找 `files/lang` 时也是这么退一级的。
* `MainMenuScene.CreateEditorButton()`：表长 > 22 就直接用表里的三帧；表长恰好 20 时从 h2d 取三张
  图拼按钮。锚点随图存，所以按钮自己就落在画架上（535,360，87x64），命中区/悬停/按下状态不用改。
* `MainMenuScene.CreateStreetScene()`：把画架图标按存档锚点贴进**克隆**的底图。参考是贴在它自己
  缓存的底图上，端口底图归 ICN 缓存，贴克隆才不会在第二次进主菜单时叠两遍。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 没有 `resurrection.h2d` 就启动失败 | 打警告后继续跑，编辑器招牌回落到自绘的那块牌子 |
| 图标贴在全局底图缓存上 | 贴进 `Clone()`，场景自己持有 |
| 只有 `isPriceOfLoyaltySupported()` 为真才把编辑器按钮加进交互表、才贴画架（`game_mainmenu.cpp:340`、`game_assets.cpp:3463`） | 端口启动时没设这个选项，所以任何安装都保留编辑器入口；按钮图形本身一律取参考美术，不再画矩形 |

测试：`H2DResourcesTests` 加三例（在 `<data>/files/data` 找到、从 `Data\` 往上一级找到、都没有时为 null），
全量 4 604 绿（Core 236 + Assets 416 + UI 881 + Game 3 071），构建 0 错误 0 警告。

冒烟：`--scene menu` 日志出现 `'resurrection.h2d' loaded (68 entries).` 与
`editor sign from resurrection.h2d`；`--cursor 580,390` 截图里画架画布点亮（highlighted 帧）且状态栏出
`创建新地图或修改已有地图。`；`--cursor 565,380 --left-click` 仍进编辑器主菜单。

## 目录批次收官

S1~S16 全部走完一遍：`campaign` 与 `editor` 之后没有再剩下未开过的目录。后续按 PLAN 第 6 条进入第二阶段：
逐函数核对（以 `docs/planv4.md` 的目录顺序为序）＋ 按目录补齐行为测试。

## 2026-11-21 `ui_button.cpp` 与 `EMPTY_*_BUTTON`（S12/S16 自建按钮美术换成参考移植）

S12 那轮把 `gui/` 归成「端口自建界面架构」，其中按钮美术是端口自己拼的：`TextButtonArtwork` 在
`SYSTEM.ICN` 的空白帧上按文字长度拉宽、自己算文字位置。参考不是这么做的，`ui_button.cpp` 有一整套
自由函数，底图则是 `game_assets.cpp` 现场合成的 `EMPTY_*_BUTTON` 表。这一轮把这条链换成移植：

* `FHeroes2.UI/Widgets/ButtonFactory.cs`：`getCustomNormalButton()`（两个重载）、`getTextAdaptedSprite()`、
  `makeButtonSprites()`、`makeSymmetricBackgroundSprites()`、`renderTextOnButton()`。几何照抄：
  宽 `clamp(文字宽 + 10, 16, 200)`、高 `clamp(文字高, 25, 200)`，释放态文字偏移 `(7, 5)`、按下态 `(6, 6)` 且整帧再下移一像素。
  偏色时按下态透明角用背景像素补齐、并把 `transform` 清零（`addButtonShine()` 之后才允许原样贴）。
* `FHeroes2.Game/Gui/EmptyButtons.cs`：`generateDefaultImages()` 的四张表。
  `EMPTY_GOOD_BUTTON`/`EMPTY_EVIL_BUTTON` 来自 `SYSTEM.ICN`/`SYSTEME.ICN` 的 11、12 帧（按下态左边界从释放态拷一列、
  九个透明角用 `FillTransform` 挖掉）；`EMPTY_POL_BUTTON` 来自 `X_CMPBTN.ICN` 的 4、5 帧（加宽一像素、把按下态的深色边框
  挪到释放态上、捷克资材少一像素的竖直缝补上）；`EMPTY_GUILDWELL_BUTTON` 来自 `WELLXTRA.ICN` 的 0、1 帧（擦掉文字、重画边框）。
* `getButtonFillingColor()` 与 `fheroes2::GetColorId()` 一起搬了：good 释放/按下 `(216,184,152)`/`(184,136,96)`，
  evil `(180,180,180)`/`(144,144,144)`。参考预先把 `64³` 张成索引表，这里不建表，只把 227 个「不循环、不重复」的调色板位置
  走一遍 Redmean，再套 15 号变换表；调色板取归档里的 `KB.PAL` 原始六位值，两端同一量纲。
* `Assets.GetImage`/`GetImageCount` 先查合成表，再查归档——和参考「先造图、造不出来才读文件」一致，
  所以调用方仍然只用 `GetImage( "EMPTY_GOOD_BUTTON.ICN", 0 )`。
* `EditorMainMenuScene`：`MAIN\nMENU`、`BACK` 两处改用 `ButtonFactory`，`SYSTEM.ICN` 的直接取图删掉；
  顺带修了绘制——端口每帧从零重画，而 `ButtonBase.DrawOnState()` 只在状态变化时才贴图，
  所以这些按钮以前根本没画出来（日志说有 20 个帧、屏幕上空白），现在改成先 `Press()`/`Release()` 再 `Draw()`。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `EMPTY_VERTICAL_GOOD_BUTTON`（`HSBTNS` 帧 + `ExtractCommonPattern`）、`EMPTY_MAP_SELECT_BUTTON`（`NGEXTRA` 帧） | 未搬：调用它们的高分榜与地图选择窗口还没移植，`GetBlankAssetName()` 里留着名字，用到时再补 |
| `makeButtonWithBackground()`、`makeButtonWithShadow()`、`getSupportedText()` | 未搬：`ButtonSprite` 的端口没有 `isTransparentBackground` 那套，等对应窗口搬过来再加 |
| 帧数不够时 `_icnVsSprite[id]` 留空、`getImage` 返回 `errorImage` | `EmptyButtons.Build()` 返回 `null`，落回端口的「缺失」语义，按钮工厂抛带说明的异常 |
| `pressed` 宽度 ≤ 2 时保留一个空精灵 | 当作资材损坏，同样返回 `null` |

测试：`ButtonFactoryTests` 8 例（宽高夹取、文字偏移、按下态补背景角、空裁片、单标签参数、六个模板名）、
`EmptyButtonsTests` 7 例（good/evil/pol/guildwell 的尺寸与重绘像素、按下态透明角、帧缓存、四个底色都落在调色板附近）。
全量 4 613 绿（Core 236 + Assets 416 + UI 883 + Game 3 078），构建 0 错误 0 警告。

冒烟：`--scene menu --cursor 565,380 --left-click` 截图里编辑器主菜单的三个按钮（`NEW MAP`/`LOAD MAP`/`MAIN MENU`）
是参考的浅棕底图，`MAIN MENU` 那块就是现场合成的 `EMPTY_GOOD_BUTTON.ICN`，文字位置与参考一致。

## 2026-11-21 第二阶段开工：`gui/` 的 1:1 缺口清单 + `ui_tool.cpp` 前三件

按新口径（见 `docs/planv4.md` 第 5 节）把「端口自建界面」全部换成 1:1 移植。本轮先做三件事：

* `FHeroes2.UI/Widgets/MovableText.cs`：`ui_tool.cpp:186` 的 `MovableText`。参考用 `ImageRestorer` 把背景贴回去，
  端口每帧重画，所以 `Hide()` 只清标志位；`extraShiftY` 与 `textArea ^ roi` 的裁剪照抄，`DrawnArea` 给宿主标脏。
* `FHeroes2.UI/Widgets/SystemInfoRenderer.cs`：`ui_tool.cpp:283`。位置 `(26, height - 30)`，FPS 取「最近一秒内的样本数 / 累计时间 + 0.05」，
  一秒窗口、以及低于 10 帧时输出十分位的规则一致；时钟与墙上时间做成可注入委托，宿主用 `--system-info` 打开（参考是设置里的开关）。
* `FHeroes2.UI/Widgets/ColorFade.cs` + `ImageOps.ApplyPalette( target, area, table )`：`ui_tool.cpp:395` 的 `colorFade()`。
  灰阶搜索范围 `10..36`、`(start*(steps-id) + end*id)/steps` 的整数插值、最后一步不渲染、结束后的 `assignedValue` 重映射都照抄。

偏差：`colorFade` 参考在函数内自跑事件循环，端口把循环交给宿主（每帧 `TryAdvance` + 结束 `Complete`）；
`delay = round(1000/fps)` 为 0 时参考整除零，端口夹到 1 毫秒并让 `StepCount` 退化为 0。

测试：`MovableTextTests` 6 例、`SystemInfoRendererTests` 5 例、`ColorFadeTests` 6 例；构建 0 错误 0 警告。
冒烟：`--scene menu --system-info --screenshot` 左下角出现白字 `HH:MM:SS, FPS: nn`，与参考位置一致。

## 2026-11-21 第二阶段 A2：淡出/淡入与调色板淡入淡出

`ui_tool.cpp` 里和「变暗/变亮」有关的一整块，连它依赖的引擎函数一起搬：

* `FHeroes2.Core/Imaging/PALColorId.cs`：`GetPALColorId()`（`engine/image.cpp:419`，表生成在 `:355`）。
  候选是 No cycle 调色板的 227 个不重复色，距离用 Redmean，最后过 `PaletteId.NoCycle`。
  偏差：参考把 64×64×64 的表在编译期嵌进可执行文件，端口每次现算（227 次比较），结果逐位相同。
* `ImageOps.ApplyAlpha()`（`engine/image.cpp:1153`）：把调色板每项按 `alpha/255` 压暗后找回最近色，
  再按这张表给源像素换色；索引 0..9 与 254..255 留在 0（黑色）。
* `Widgets/DisplayFade.cs`：`fadeDisplay()` + `fadeOutDisplay()` + `fadeInDisplay()`（`ui_tool.cpp:62-118`、`:653-696`）。
  `screenFadeTimeMs = 100`、`screenFadeFrameCount = 6`、`screenFadeStep = 42`、
  `round` 取整、alpha 255 直接拷回、alpha 0 直接填黑、半程淡出的帧数与时长都对上。
* `Widgets/DisplayFadeWithPalette.cs`：`FadeDisplayWithPalette()`（`:698-724`），每帧对留底重复换色再贴回。
* `Widgets/InvertedShadow.cs`：`InvertedShadow()` 与 `InvertedFadeWithPalette()`（`:726-764`），
  四个环绕区域分别换色、挖掉的窗口不动、零尺寸的块跳过。

偏差：参考都在函数内部跑事件循环并自己渲染，端口改成 `TryAdvance()` 逐帧、由宿主呈现（与 ColorFade 同一套约定）；
`fadeDisplay()` 里的 `Cursor::Get().SetThemes( Cursor::POINTER )` 等光标移植（B 步）后再接；负的 alpha 在参考里
会先命中 `assert` 再因 `static_cast<uint8_t>` 回绕，端口改为按 `Math.Round` 后的值走同一分支。

本步没有可删的自建件：端口此前根本没有屏幕淡入淡出，参考的调用点（开局、战斗、城堡）接线时随各自场景一起做。

测试：`PALColorIdTests` 6 例、`ApplyAlphaTests` 5 例、`DisplayFadeTests` 6 例、`PaletteFadeTests` 5 例。
全量 4 652 绿（Core 247 + Assets 416 + UI 911 + Game 3 078），构建 0 错误 0 警告。

## 2026-11-21 A3：三件作用域守卫（屏幕调色板 / 退出请求 / 界面类型）

`ui_tool.cpp:349-393` 的三件 RAII 全部落成:

* `Widgets/ScreenPaletteRestorer.cs`:`ScreenPaletteRestorer`。`ChangePalette` 每帧换视频自带调色板，
  `Dispose` 时换回游戏调色板（未换过就不动，对应 `_paletteChanged`）。
* `Widgets/ApplicationClosureRestorer.cs`:`ApplicationClosureRestorer`。作用域内 `QuitEventHook` 恒 `false`，
  结束后还回标准钩子。偏差：参考写死 `Game::processExitEvent() == QUIT_GAME`，端口把标准钩子作为构造参数传入。
* `Widgets/GameInterfaceTypeRestorer.cs`:`GameInterfaceTypeRestorer`。构造记下原类型、不同才切，`Dispose` 切回；
  `InterfaceType` / `OriginalInterfaceType` 两个只读成员与参考同名。偏差：注入 `Settings` 而非取全局单例。

依赖补齐（`Display::changePalette`，`engine/screen.cpp:1622`）：

* `ScreenBuffer.ChangePalette( palette )` + `Palette` 改为可换：同一实例直接返回（对应参考的指针比较），
  换掉后 `InvalidateAll()`——端口在上传时把索引落成 RGBA，换调色板必须整屏重传。
* `SdlPresenter` 去掉 `SetPalette`，新增 `EnsureLookup`：上传时按 `frame.Palette` 的实例重建查找表，
  于是「换调色板」在端口只有一条路径（改画面调色板），`GameHost` 里的 `SetPalette` 调用随之删除。

偏差：参考构造/析构里的 `RenderProcessor::stopColorCycling()/startColorCycling()` 没有对应物——端口还没有色彩循环
（`engine/render_processor.cpp` 未移植，所有画面按固定调色板出图），此处恒等于空操作，已在 XML 注释里写明等
色彩循环移植时接上。

测试：`ScreenBufferTests` 2 例、`RestorerTests` 5 例。全量 4 659 绿（Core 249 + Assets 416 + UI 916 + Game 3 078），
构建 0 错误 0 警告。冒烟：`--scene menu --screenshot` 出图正常（菜单画面 1280x960，240 KB）。

## 2026-11-21 A4：死亡波浪 / 圣言 / 涟漪三个法术特效

`ui_tool.cpp` 里三个只吃索引像素的特效，落在 `Core/Imaging/SpellEffects.cs`:

* `SpellEffects.CreateDeathWaveEffect()`（`:485-543`）：按死亡波浪曲线把每一列整体下移、曲线以下按相反顺序
  翻回去；曲线越界时参考 `assert(0) + continue`，端口同样跳过该列。参考原地扩容并复用调用方的 `Image & out`，
  端口每次返回新图（`PixelBuffer` 尺寸固定），尺寸取 `outWaveWidth x inHeight`——与调用点只拷左上
  `waveWidth x height` 的那一块完全对齐，写到的列都是从头覆盖，结果逐像素相同。
* `SpellEffects.CreateHolyShoutEffect()`（`:545-619`）：十字均值模糊 + 暗红系数
  （红 `4 - s/280`、绿蓝 `4 - s/80`），再走 `GetColorId`（先除 4 再查 64³ 表）。
  偏差：参考 `getRGBGamePalette()` 是 6 位色，端口 `Palette` 是左移两位后的 0..255，因此先 `>> 2` 还原；
  参考 `return in;` 返回写时复制的同一张图，端口给 `Clone()`。
* `SpellEffects.CreateRippleEffect()`（`:621-651`）：逐行按 `sin( 2π·y/period + phase )` 横向平移，
  新图宽出两倍幅度、锚点左移一个幅度，图片与变换层一起搬，空出来的像素保持 `reset()` 的「变换 1」。
  偏差：`std::round`（四舍五入远离零）用 `MidpointRounding.AwayFromZero`；负幅度与 `period == 0` 直接抛异常。

放 Core 而不放 UI 的原因写在类的注释里：调用方是战斗界面（Game 层）与 `game_assets.cpp` 的 ICN 生成（Assets 层），
Assets 只引用 Core，放 UI 会被上层反向依赖。

本步没有可删的自建件：端口此前没有任何战斗法术特效，战斗界面整体还没移植。

测试：`SpellEffectsTests` 12 例（死亡波浪 4、圣言 4、涟漪 4）。全量 4 671 绿
（Core 261 + Assets 416 + UI 916 + Game 3 078），构建 0 错误 0 警告。

## 2026-11-21 A5：`renderHeroRacePortrait`（英雄种族立绘）

两处新件：

* `Core/Imaging/ImageOps.SubpixelResize.cs`（`image.cpp:2986-3236`）：角对齐双线性重采样，
  `Sprite → Sprite`、`Sprite → PixelBuffer`、`PixelBuffer → PixelBuffer` 以及带源 / 目标区域的四个重载。
  插值出的 RGB 经 `PALColorId.Get()` 落回调色板；源图带变换层时只累计变换值 0 的像素，
  权重和不足 0.5 的落点写黑并标变换 1；源图无变换层则纯算术加权，目标变换层清零。
  放 Core 是因为 `ui_tool` 的调用点在 UI 层，而 `game_assets` 的 ICN 生成在 Assets 层。
* `UI/Widgets/HeroRacePortrait.cs`：`TryGetArtwork` 是 `:860-886` 的 `switch`（六族 + `RAND` 用
  `CMBTHROW` + 紫调色板），`Compose` 是 `:845-858` 的 lambda 加 `:887` 的 `Copy`，`TryRender` 负责读两张 ICN。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Assets::getImage()` 的全局图片缓存 | 没有对应物，`TryRender` 每调一次读一遍 ICN；类注释写明按帧重画的调用点要自己缓存 |
| `default:` 分支 `assert( 0 )` | `TryGetArtwork` 返回 `false`，`TryRender` 返回 `null`，调用点跳过绘制 |
| 目标区域只有 1 像素时步长 `NaN` 转 `int`（未定义行为） | `SubpixelResize` 里非有限采样位置按 0 处理，1 像素维退化为「全取第一条」 |
| `Copy` 进调用方的 `output` | `Compose` 直接把整张图作为新 `Sprite` 返回 |

落地时纠正了自己两处误读：立绘比肖像矮时目标 Y 是 `portPos.height - height`（贴底，不是贴顶）；
返回图是单层 `Image`（没有变换层），背景的透明像素在重采样阶段就被压成不透明像素。

A5 的收口标志写的是「英雄对话框改用」，但端口没有 `heroes_dialog`（S5-8 那条已记：界面与翻译层归 S13，
端口不做翻译，`UI/Dialogs/HeroQuickInfo*` 只覆盖 `getHeroQuickInfo` 那一半），目前没有可接的调用点。
`HeroRacePortrait` 与 `SubpixelResize` 先备好，等 `heroes_dialog.cpp` 真正移植（或编辑器地图规格窗口，`editor_map_specs_window.cpp:272` 也要它）时直接用。

测试：`SubpixelResizeTests` 6 例、`HeroRacePortraitTests` 13 例（7 族取材 + 2 拒收 + 居中 + 透明 + 紫调色板 + 真实资源端到端）。
全量 4 690 绿（Core 267 + Assets 416 + UI 929 + Game 3 078），构建 0 错误 0 警告。

## 2026-11-21 A6：`getLocalizedText`（+ `ui_tool.cpp` 剩余件盘点）

新增 `Game/Gui/LocalizedText.cs`：`ui_tool.cpp:913-948` 的两个重载
（`Create(IReadOnlyList<LocalizedString>, FontType)` 与 `Create(IReadOnlyList<LocalizedTextRun>)`，后者带
`LocalizedTextRun` 记录）。语义照抄：空表返回 `null`，一段返回 `Text`，多段返回 `MultiFontText`——
返回类型是参考的约定，调用点据此决定排版。空串段由 `MultiFontText.Add` 丢掉，与参考一致。

偏差：参考的 `Text( text, font, language )` 把语言标签交给字体层挑字形，端口没有字体回退（本地化未移植，
ADR-172），`LocalizedString.Language` 在这里被忽略，每段都用调用方给的字体。
放 `Game/Gui` 而不是 `UI/Widgets`：只依赖 `Text`/`MultiFontText`/`LocalizedString`，全在 Game 层，
Game 层将来的存档与事件文案也要用。

盘点本步顺带核出的三处缺口（原计划 A 系列没列全）：

| 参考 | 现状 | 处理 |
| --- | --- | --- |
| `UIScrollInertia`（`ui_tool.cpp:950-1076`，`ui_tool.h:351-407`） | 计划原写「参考无调用点、不搬」，**这条是错的**：`interface_gamearea.h:306` 持有 `_inertiaHandler`，`interface_gamearea.cpp:970/1080/1090/1106/1166` 调 `isActive`/`commitRelease`/`reset`/`addMovementStep`/`update` | 推迟到 G3（端口先有 `interface_gamearea` 才有调用点，也才定得下时间源）；已写进新加的 A7 行 |
| `getLocalizedStrings`（`ui_tool.cpp:890-911`） | 未移植。它按「当前语言 / 替换值语言」把模板切成三段，靠的正是端口没有的字形回退 | 登记不搬（ADR-172），与 `replace` 占位符文案一起留在翻译层 |
| `processIntegerValueTyping`（`ui_tool.cpp:766-839`） | A1–A6 都没覆盖。端口只在 `RecruitDialog.TypeDigit`/`ClearTyped` 里自建了一份（缺小键盘数字与负号规则），`dialog_selectcount` 的输入整个没做 | 新增 A7，移植后 `RecruitWindowView`/`SelectCountView` 改用 |

测试：`LocalizedTextTests` 6 例（空表 ×2、单段类型、单段自定义字体、多段拼接、逐段字体不同、空串段）。
全量 4 696 绿（Core 267 + Assets 416 + UI 929 + Game 3 084），构建 0 错误 0 警告。

## 2026-11-21 A7：`processIntegerValueTyping`（数字键入）

新增 `UI/Widgets/IntegerValueTyping.cs`（`ui_tool.cpp:766-839`）：`Process( min, max, ref valueBuf, key )`。
四条分支逐条照抄——退格 / 删除清空缓冲区并答 `clamp( 0, min, max )`；减号只在 `min < 0` 且缓冲区全为零时
把内容换成 `"-"`；主键盘与小键盘的数字追加后解析，超出范围或溢出就把这一位退回去；其余键原样返回空。
末尾那条「解析成功但不在范围内」的怪分支也保留了：数字留在缓冲区里但不报告（`min = 1` 时键入 `0` 之后再键
`5` 得到 `05` → 5）。

接线：`RecruitDialog.TypeDigit(char)`/`ClearTyped()` 删掉，换成 `TypeKey( Key )`——只留下「把参考算出来的
`result = *value` 落进 `Count`」这一句，缓冲仍然是模型里的 `_typedValue`（参考的 `typedValueBuf` 是
`Dialog::RecruitMonster()` 的局部变量，同一层）。`RecruitWindowView.TryTypeDigit` 从「自己判数字/退格」缩成
「有按键就交给 `TypeKey`」，行为与参考的 else-if 链一致：键入被接受时跳过本帧的鼠标分支，字母键之类仍落到鼠标。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 从 `LocalEvent::Get()` 读全局事件 | 接收一个 `Key`，对话框模型不依赖输入状态，测试也不用造一帧事件 |
| `assert( min <= max )` | `ArgumentOutOfRangeException` |
| `std::stoi` 对只有减号的缓冲区抛未捕获的 `invalid_argument` | 解析失败一律当作「这一位没接受」，缓冲回退一位 |

`SelectCountView` 没有改用：它的键盘输入整个不存在（只有 `SetLeftButton( down, point )`，没有 `LocalEvent`
入口），`dialog_selectcount.cpp:168`（单值）与 `:569`（重新分配）的键入要等该窗口补输入时才接得上；届时直接调
`IntegerValueTyping.Process` 即可。计划 A7 行已按此改写。

测试：`IntegerValueTypingTests` 10 例（累加与回退、int32 溢出回退、退格 / 删除、减号三条、小键盘数字、
无关键、越界数字留在缓冲区、反向区间）；`RecruitDialogTests` 的四个键入用例改成 `TypeKey`，
`RecruitWindowTests` 里用 `ClearTyped` 的那处改成 `SetCount( 0 )`。
全量 4 706 绿（Core 267 + Assets 416 + UI 939 + Game 3 084），构建 0 错误 0 警告。

