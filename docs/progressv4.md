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
| S2 | `spell` | 3 | 完成（法术书窗口与法术描述已收口，2026-11-22） |
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

## 2026-11-21 B：`cursor.cpp` 全量

新增 `Game/Cursor/` 四个文件（`cursor.cpp:31-271`、`cursor.h:37-240`）：

| 文件 | 参考 | 内容 |
| --- | --- | --- |
| `CursorState.cs` | `Cursor`（`cursor.cpp:31-237`） | 主题、偏移、单色开关三件状态；`SetThemes`/`SetCustomImage`/`UpdateCursorPosition`/`SetVideoPlaybackCursor`/`Refresh`；`Current` 对应 `Cursor::Get()` |
| `CursorImages.cs` | `SetThemes` 里的 `icnID` 选择 + `Assets::getImage` | `CursorImageSet` → 帧：三套彩色 ICN 走 `Assets.GetImage`，三套单色走逐帧 `.BMP`（`MonoCursorFile`，条目名照抄 `game_assets.cpp:4094-4138`：`ADVMBW01`/`CMSEBW01` 从 1 数、`SPELBW00` 从 0 数） |
| `CursorRestorer.cs` | `CursorRestorer`（`cursor.cpp:239-271`） | 构造时记下可见性与主题、析构时先恢复可见性再恢复主题 |
| `ICursorRenderer.cs` | `fheroes2::cursor()`（`engine/screen.h:289-359`） | `IsVisible`/`IsSoftwareEmulation`/`Position`/`Show`/`Update`/`SetPosition`/`KeepInScreenArea` |

几何那半边（主题→图集、精灵序号、按主题的偏移表、距离变体）A 系列已经由 `Core/Imaging/CursorLayout.cs`
覆盖，本步只做状态与图源，`DistanceThemes`/`WithoutDistanceThemes` 因此不再重复一层转发，调用方直接用
`CursorLayout.WithDistance`/`WithoutDistance`。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `SetThemes` 取图必然成功（缺图给 `errorImage` 占位） | 取不到图只记主题、不换图；`Offset`、指针位置、屏幕内夹取都保持上一次的值。当前触发点：距离光标两套图还没合成，单色 `.BMP` 缺条目 |
| 从 `LocalEvent::Get()` 读鼠标位置 | 从 `ICursorRenderer.Position` 读，模型不依赖输入状态 |
| `Cursor::~CursorRestorer` 末尾强制重绘指针下 1 像素（软件光标） | 未搬：端口没有软件光标，也没有按矩形重绘的入口；届时用 `UpdateCursorPosition` 的返回值补 |
| `MONO_CURSOR_ADVENTURE_MAP`/`COLOR_CURSOR_ADVENTURE_MAP`（距离光标） | `CursorImages` 返回 `null`：参考是 `generateDefaultImages()` 用 `ADVMCO` 4..11 帧加画出来的数字现拼的（`game_assets.cpp:3694-3740`），属于资材合成，未搬 |

两处计划前提有误，已改：其一，`AdventureMapScene` 从来没有光标选择，也没有任何代码画过光标图（端口的指针就是
操作系统自带箭头），所以「删掉场景里的自选光标」无对象；其二，B 的收口「地图上光标由 `Cursor` 决定」要等 App 层
按 `ICursorRenderer` 实现软件指针（`engine/screen.cpp`）之后才谈得上，`CursorState.Current` 现在由组合根留空。

测试：`CursorStateTests` 7 例（自定义图与偏移、缺图只记主题、软件光标移动与 1x1 报告、宿主光标不动、
视频指针仅软件光标、`CursorRestorer` 两参/一参的进出、缺 `Current` 抛错）；`CursorImagesTests` 7 例对真实归档
（三套彩色 ICN、三套单色 BMP、距离图集为空、越界帧为空、指针主题出图与去重、滚动主题夹屏内、单色开关与视频指针例外）。
全量 4 720 绿（Core 267 + Assets 416 + UI 939 + Game 3 098），构建 0 错误 0 警告。

## 2026-11-21 C：`TextInputField` + 文本层收尾

新增 `UI/Widgets/TextInputField.cs`（`ui_tool.cpp:196-281`、`ui_tool.h:147-200`）：`Text`（`TextInput`）、`Cursor`
（`MovableSprite`）、`TextInputArea`、`OverallArea`（文字区左右各外扩 1、上沿扩 2）、`IsMultiLineText`、
`IsSingleLineTextCenterAligned`、`VerticalTextOffset`、`EventProcessing()`（`CURSOR_BLINK_DELAY` 翻可见性）、
`Draw()`、`Set()`、`GetCursorInTextPosition()`、`GetCursorPositionInAdjacentLine()`、`Height()`。多行滚动照抄：光标跑到
可视区上沿之上就整块上移，跑到下沿之下就按整行数下挪，滚动因此总停在行边界。

`Game/Gui/FontCharHandler.cs` 补齐 `ui_text.cpp` 最后两个公开函数：

| 新增 | 参考 | 说明 |
| --- | --- | --- |
| `CursorChar` | `ui_text.cpp:40` 的 `cursorChar` | 从 `TextInput` 的私有常量提上来，`GetCursorSprite` 与 `TextInput` 共用 |
| `GetCursorSprite( in FontType )` | `ui_text.cpp:1425` | `'|'` 的字形，就是文字输入的光标 |
| `IsFontAvailable( ReadOnlySpan<byte>, in FontType )` | `ui_text.cpp:1398` | 空文本算可用；按字节判定（参考的文本本来一字节一字符） |

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 构造函数收 `Image & output`（给 `ImageRestorer` 用），`draw()` 先 `_background.restore()` 再画 | `output` 挪到 `Draw` 的参数（与 `MovableText` 等控件一致）；没有恢复这回事——每帧从干净缓冲重画，`Draw` 返回自己画过的矩形 |
| `MovableSprite::hide()`/`show()` 立即上屏/复原 | 光标字形由 `Draw` 按可见性贴进缓冲：进来时可见才画并重置闪烁计时，不可见就只画文字、不动计时。否则整屏重画的模型里闪烁永远关不掉 |
| 构造函数的 `std::optional<SupportedLanguage>` | 未搬，本地化子系统 `ui_language.*` 尚未移植（与 `TextInput` 同一处理） |
| `height()` 复制一份 `TextInput` 再量 | 用同样的字体与宽度新造一个临时实例，结果相同 |

收口未达成：计划写的「存档命名、战役名可用」前提不成立——`SelectFileDialog` 现在只有静态辅助（列文件、拼文件名），
`InputStringDialog` 只有签名，两个都没有名字输入这一步；整个仓库没有一处调用 `TextInputField`。等 D 的屏幕键盘
（`ui_keyboard.cpp`）与存档 / 战役名对话框移植时再接，届时直接 `new TextInputField( area, isMultiLine, isCenterAligned )`
即可。另：`TextInputField` 在参考里属于 `ui_tool.cpp`，计划原先归到 `ui_text.cpp` 已改正。

测试：`FontCharHandlerTests` 3 例（光标字形、可用文本、含不可用字节）；`TextInputFieldTests` 7 例（空字段与两块区域、
只画在字段内、光标位置与隐藏时不画、居中开关、多行按整行滚动、`Height` 不动字段自身的文字、点在字段上换算出偏移）。
全量 4 730 绿（Core 267 + Assets 416 + UI 946 + Game 3 101），构建 0 错误 0 警告。

## 2026-11-21 D：屏幕键盘 `ui_keyboard.cpp` 全量 + `ui_language` 收口

`ui_language.cpp` 的语言名单、缩写、码页与切换守卫此前已落在 `CodePage.cs` / `GameLanguage.cs` /
`LanguageSwitcher.cs`，本步只补键盘本身，新增 `UI/Dialogs/` 六个文件（`ui_keyboard.cpp:1-1360`）：

| 文件 | 参考 | 内容 |
| --- | --- | --- |
| `VirtualKeyboardLayout.cs` | `ui_keyboard.h:37-133`、`ui_keyboard.cpp:36-623` | `KeyboardDialogAction`、`KeyboardLayoutType`、`KeyboardCursorMove` 三个枚举；十七种语言的字母表（按码页 CP1250/1251/1252/1253/1254/1258/ISO-8859-3/16 解码）、数字与标点表、`GetCharacterLayout`、`GetDefaultButtonSize`、`IsLanguageSwitchingSupported`、全部布局常量 |
| `VirtualKeyboardArtwork.cs` | `ui_keyboard.cpp:1185-1200`、`1320-1328` | 窗框、`STONEBAK` 底板、`REQBKG` 第 0 帧输入框、确定键 0/1 帧 |
| `VirtualKeyboardRenderer.cs` | `KeyboardRenderer`（`ui_keyboard.cpp:137-370`） | 窗口、`TextInputField`、光标、`InsertCharacter`/`RemoveCharacter`/`SwapSign`/`Resize`/`SetCursorPosition`/`Update`，多行自动换行与滚动、光标闪烁、`InputAreaHeightIncrease` |
| `VirtualKeyboardButton.cs` | `KeyboardButton`（`ui_keyboard.cpp:372-426`） | `ButtonSprite` + 阴影 + 快捷键 + 动作 + 反相渲染标记 + 右键说明 |
| `VirtualKeyboardText.cs` | 各处 `_( ... )` | 特殊键标签与右键说明；`OutOfRange` 的占位符替换 |
| `VirtualKeyboardView.cs` | `processVirtualKeyboardEvent`（`ui_keyboard.cpp:1125-1250`）+ 两个外层循环（`1252-1480`） | 生成按键、居中排布、事件处理、布局与语言切换、确定键；`ReadNumpad`/`TryCreateNumpad`/`GetStartLanguage`/`LastSelectedLanguage` |

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `while ( le.HandleEvents() )` 阻塞循环，换布局时 `continue` 重来，按确定键才返回 | `ProcessEvents` 处理一帧并就地应用布局 / 语言动作，宿主循环到读到 `KeyboardDialogAction.Close` 为止 |
| 每个循环重画窗口并用 `ImageRestorer` 擦掉上一帧 | 每帧从干净缓冲重画：`Draw` 依次画窗口、文字、按键、确定键 |
| `generateButtons` 每次调用都重建 `KeyboardButton` 并贴图 | 布局只在 `LayoutType`/`Language` 变化时重建，`Rows`/`OkayArea`/`WindowArea` 首次访问时惰性构建 |
| 反相渲染（大写键盘上那个 `|`）每帧 `press()` 一次后由 `drawOnState` 改状态 | 构建时 `SetPressed( true )`，之后每帧按「是否按住」取反 |
| 阴影画在按键之后 | 画在按键之前（`DrawShadow` 先于 `Draw`），重叠像素因此是按键本体；隐藏按键连阴影都不画，与参考「`draw()` 返回 `false` 就跳过阴影」一致 |
| `ICN::BUTTON_SMALL_OKAY_GOOD` 由 `game_assets.cpp:1042-1054` 现拼（原始资源分支取 `SPANBTN.ICN`） | 资源层只有档案帧，确定键取 `SPANBTN.ICN` / `SPANBTNE.ICN` 的 0/1 帧，即参考 `useOriginalResources()` 那条路 |
| 参考的 `assert`（缺语言表、`ChangeLanguage` 越权、`getDefaultButtonSize` 未列语言） | 分别退成：回退英文表、忽略该动作、英文 30×25 |
| 布局生成期间的 `_( )` 由 `LanguageSwitcher` 换语言 | 标签由调用方按目标语言先行解析（`VirtualKeyboardText`），UI 层不读翻译目录 |
| `std::stoi` 家族：空串 / 只有减号抛 `invalid_argument`，溢出抛 `out_of_range`，尾随垃圾被忽略 | `ReadNumpad` 用 `int.TryParse` 区分，空串与单个符号算「非法」，溢出与越界算「越界」；尾随垃圾直接判非法（键盘打不出，走不到） |

收口：`openVirtualKeyboard`/`openVirtualNumpad` 的阻塞循环与两处 `showStandardTextMessage` 没有对应函数，
宿主读 `VirtualKeyboardResult.Popup` 自己弹窗，读 `ReadNumpad` 的返回值自己报错。存档 / 战役名对话框仍未移植，
所以屏幕键盘暂时还没有调用方——和 C 步记录的缺口同一个。

测试：`VirtualKeyboardLayoutTests` 9 例（英文三行大小写、数字与标点表、四种语言的键宽、语言切换守卫、
CP1251 解码、未知布局）；`VirtualKeyboardViewTests` 17 例（小写布局的行数与隐藏语言键、点字母打出小写、确定键与
热键关闭、布局键三态往返与反相键的按下状态、右键说明、多行多出回车键与窗口变高、语言键往返并记住选择、
数字键盘的正负号与隐藏键、`ReadNumpad` 八种输入、缺图与非法参数）。全量 **4 756 绿**
（Core 267 + Assets 416 + UI 972 + Game 3 101），构建 0 错误 0 警告。

## 2026-11-21 E-1：`ui_dialog.cpp` 收尾

`DialogElement.cs` 补齐参考里最后两个元素，`ui_dialog.cpp` 的元素类到此一一对应：

| 端口类 | 参考 | 内容 |
| --- | --- | --- |
| `CustomAnimationDialogElement` | `ui_dialog.h:317-346`、`ui_dialog.cpp:856-902` | 静态图 + 循环动画，尺寸取自静态图，动画位置是元素内偏移加帧自身锚点；`Update` 按 `IcnAnimation` 的帧时长推进并重画 |
| `MonsterDialogElement` | `ui_dialog.h:348-363`、`ui_dialog.cpp:904-935` | 底板（`STRIP` 第 12 帧，忽略变换层）+ 种族底图 + 立绘，全部叠在 `(6, 6)` 处再整体贴出；左右键都返回生物说明弹窗 |

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `CustomAnimationDialogElement` 拿 ICN id，逐帧问 `ICN::getAnimatedIcnIndex()` | 调用方直接给帧列表与区间，和 `AnimationDialogElement` 一致（端口没搬那张表） |
| `MonsterDialogElement` 自己调 `Assets::getImage( ICN::STRIP, 12 )` 与 `renderMonsterFrame()`（`ui_monster.cpp:31-59`），弹窗是 `Dialog::ArmyInfo()` | 底板 / 种族底图 / 立绘由调用方给（`MonsterIcnIds.GetDwellingIcn()` + `ArmyBarArtwork.GetRaceFrameIndex()` 取），弹窗返回 `DialogPopup` 文本。`ui_monster.cpp` 与军队信息窗口在 H 步，届时改回参考的取图方式 |
| 静态图只在 `_currentIndex == 0` 时画一次 | 每次 `Draw` 都画（整帧重画，理由同 `AnimationDialogElement`） |
| 立绘画进 `STRIP` 副本后才贴出 | 每次都从 `WithoutTransformLayer().Clone()` 的干净副本开始，图片本身不被写脏 |

测试：`DialogElementTests` 新增 7 例（静态图尺寸与首帧位置、按帧时长推进并回绕、构造参数校验、生物底板的
尺寸与 `(6, 6)` 叠加、重复绘制不写脏图源、左右键弹窗与区域外不响应、缺底板）。全量 **4 763 绿**
（Core 267 + Assets 416 + UI 979 + Game 3 101），构建 0 错误 0 警告。

## 2026-11-21 E-2：删两套自建窗框，`ui_window.cpp` 对拍

两套自建窗框工件删除，`StandardWindow` 与 `FrameBox` 回到参考的取图方式——**对话框骨架只剩一套**：

| 端口类 | 参考 | 取图方式 |
| --- | --- | --- |
| `Dialogs/StandardWindow.cs` | `ui_window.cpp:1-541` | 边框取 `SURDRBKG.ICN`（上/下）与 `WINLOSE.ICN`（角/左右，窗口过矮时退到 `SURDRBKG` 帧 4/5），内衬取 `STONEBAK.ICN`；`Settings.IsEvilInterfaceEnabled` 时换成 `SURDRBKE.ICN` / `WINLOSEE.ICN` / `STONEBAK_EVIL.ICN` |
| `Dialogs/FrameBox.cs` | `dialog_box.cpp` 的 `Dialog::FrameBox` / `redraw()` | 帧 0-2 与 4-6 取自 `BUYBUILD.ICN`（`ICN::BUYBUILD`），邪恶界面换成 `BUYBUILE.ICN`（`ICN::BUYBUILE`） |

接口收敛（调用方不再碰窗框图）：

* `MessageBox.Create` 去掉 `chrome` 形参；`KingdomIncome.Create`、`TavernView.TryCreate`、
  `SelectCountView.TryCreate`、`MarketplaceView.TryCreate`、`MarketplaceLayout.Create` 同步。
* `RecruitHeroArtwork` / `BuildDialogArtwork` 不再持有 `Chrome` 属性，`RecruitHeroView` / `BuildDialogView`
  直接用 `FrameBox.Create`。
* `FrameBox` 新增 `ArtworkFileName`、`IsArtworkAvailable`、`RequiredSpriteCount`，供 `Try*` 与场景判断能否画框。
* App 场景（`MainMenuScene`、`EditorMainMenuScene`、`EditorScene`、`CastleScene`、`MarketplaceWindow`）把
  `_messageChrome` 换成布尔 `HasMessageFrame()` / `_hasInfoFrame`，日志文案同步。
* `tools/FHeroes2.AssetProbe` 的 `window` 命令改为打开 `AssetBundle` 并设 `GameAssets.Current`，预览不再收窗框。

删除的文件：`Dialogs/StandardWindowChrome.cs`、`Dialogs/FrameBoxChrome.cs`。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `Assets::getImage` 缺图时断言 / 返回空图，`FrameBox` 直接崩 | `StandardWindow.Draw` / `FrameBox.Create` 抛 `InvalidOperationException`（带档案名与帧号）；`Try*` 系列先问 `FrameBox.IsArtworkAvailable` 返回 `null`，场景据此静默降级 |
| `FrameBox` 在 `redraw()` 里每次 `getImage`，由 ICN 缓存兜底 | 同名帧每次也走 `Assets.GetImage`（自带缓存），端口不另存副本 |
| `ui_window.cpp` 用 `ImageRestorer` 还原被覆盖的底图 | 不搬：场景整帧重画，`StandardWindow` / `FrameBox` 只负责贴图 |
| 窗框在构造时即取图 | `StandardWindow` 延迟到首次 `Draw`（布局不依赖图片尺寸）；`FrameBox` 必须在 `Create` 时取（左/右列宽决定布局） |

测试：`FrameBoxTests` 重写（常量、`BUYBUILD.ICN` / 帧号、缺数据抛错、布局与中段切片理论值、绘制只落在
`FrameArea` 内、底行位于切片之下）；`StandardWindowTests` 去掉 chrome 表测试，绘制断言改为「不再是画布底色」
与「角缝里含内衬像素（有/无内衬两次绘制对比）」；`MessageBoxTests`、`KingdomIncomeTests`、`TavernViewTests`、
`SelectCountViewTests`、`MarketplaceViewTests`、`GiftWindowTests`、`RecruitWindowTests`、`SettingsDialogTests`、
`AdventureOptionsViewTests`、`RecruitHeroViewTests`、`BuildDialogTests`、`VirtualKeyboardViewTests` 同步去掉自建窗框，
改为在 `GameAssets.Current` 作用域内创建（缺数据的机器直接返回）。全量 **4 764 绿**
（Core 267 + Assets 416 + UI 980 + Game 3 101），构建 0 错误 0 警告。

## 2026-11-22 F：`ui_button.cpp` 收尾（合成按钮 / 取词回落 / `ButtonRestorer`）

`gui/ui_button.cpp` 的剩余件全部落地，端口不再有「按钮外观自算」的代码：

| 参考 | 端口 |
| --- | --- |
| `getSupportedText()`（:959-963） | `ButtonFactory.GetSupportedText( translatedText, untranslatedText, in FontType )` |
| `makeButtonWithBackground()`（:739-768） | `ButtonFactory.MakeButtonWithBackground( offsetX, offsetY, released, pressed, background )` |
| `makeButtonWithShadow()`（:770-813） | `ButtonFactory.MakeButtonWithShadow( ..., Point shadowOffset )` |
| `ButtonRestorer`（:643-665） | `Widgets/ButtonRestorer.cs`（`IDisposable`，构造即压暗并重绘，`Dispose` 还原） |

两个合成函数的做法与参考一致：背景按 `released` 的尺寸从自身锚点处裁一块（`Crop( background, offsetX,
offsetY, released.width(), released.height() )`），把释放态与按下态按各自锚点贴上去；禁用态是 `released` 的
拷贝过 `PAL::PaletteType::DARKENING` 后贴到**同一块背景副本**上（参考把裁好的背景直接 move 给禁用态，端口也
复用 `croppedBackground.Image`，不另拷）。阴影变换号沿用参考硬编码的 `3`。

`ButtonArtwork.AddShine` 复核时补上了参考 `ui_button.cpp:241-242` 漏掉的两笔（`DrawLine( {15,4}, {13,6} )`
第一色 + `SetPixel( 12, 7 )` 末色），此前左侧长亮线少一段。

排查记录：`MakeButtonWithBackground_DarkensTheDisabledState` 一开始画不出禁用态，根因不是 `Draw` 的偏移，
而是禁用态精灵用「锚点归零」的方式克隆——参考里 `Sprite disabled( released )` 是**拷贝构造**、锚点仍是释放态
的 `(x, y)`；锚点归零后暗色按钮把整块背景盖满，背景像素被抹成暗色。改用 `released.Clone()` 后正确。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `getSupportedText` 里直接取 `gettext` 的译名 | 翻译层未搬，译名/原名由调用方成对传入 |
| `makeButtonWithBackground` 用 `PAL::GetPalette( DARKENING )` 造禁用色 | `ImageOps.ApplyPalette( image, PaletteId.Darkening )`（同一张调色板表） |
| `ButtonRestorer` 重绘到 `Display::instance()` 并 `updateNextRenderRoi( area() )` | 帧缓冲由调用方传入，宿主负责上传；无渲染 ROI 概念 |
| `ButtonGroup( const std::vector<const char *> & )` 文本构造函数 | 未搬：唯一调用方 `dialog_selectitems.cpp` 的 TOWN/CASTLE 行未转，且它把索引 `0..n-1` 当按钮值返回，端口需要 `DialogButton`；其底层 `MakeSymmetricBackgroundSprites` 已就位 |
| `OptionButtonGroup` 依赖 `ActionObject` 观察者 | 未搬：`ButtonBase` 无观察者机制（已在该类文档登记），设置对话框暂用现成选项按钮 |

台账复核（`docs/planv4.md` §5.4）：`Widgets/DialogButtonArtwork.cs` 现在只是一个 `readonly record struct`
（一对 released/pressed 精灵）+ 资产注入接口，是取图缝而不是画法；`Widgets/ButtonArtwork.cs` 已核实是
`resizeButton` / `addButtonShine` / `makeTransparentBackground` 的 1:1 移植。两者都保留。

测试：`ButtonFactoryTests` +4（背景合成、禁用态压暗、阴影锚点、`GetSupportedText` 回落），新增
`ButtonRestorerTests`（5：构造即暗、`Dispose` 还原、原本已禁用不动、可重复 `Dispose`、空参），
`ButtonArtworkTests` 同步补齐新增亮线的像素断言，`EmptyButtonsTests`（9）复跑确认六张 `EMPTY_*_BUTTON` 表。
全量 **4 775 绿**（Core 267 + Assets 416 + UI 989 + Game 3 103），构建 0 错误 0 警告。

## 2026-11-22 G1（1/4）：`interface_border.cpp` 的 `GameBorderRedraw`

`UI/Interface/GameBorder.cs`：`GameBorderRedraw()`（`interface_border.cpp:70-418`）与文件内的
`repeatPattern()`（:40-67）1:1 移植，含 `borderWidthPx`/`tileWidthPx`/`radarWidthPx` 三个常量（`BorderWidth`/
`TileWidth`/`RadarWidth`）与 `ADVBORD.ICN`/`ADVBORDE.ICN` 两个资产名。

拼边框的九段切法、四组重复块（上下 32 像素瓦片与 6 像素角边、邪恶底边的 7 像素骨节、左右 32 像素竖瓦片与
4/6/8 像素竖缝）、`topPadWidth`/`bottomPadWidth`/`vertPadHeight` 的余量分配、按多余高度选 4/7/8 行图标的
`iconsCount`、视图世界模式对中缝分叉的两块 3 像素补丁，全部按参考的 `srcrt`/`dstpt` 逐步搬，没有重排。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 内部读 `Settings::Get().isHideInterfaceEnabled()` / `isEvilInterfaceEnabled()` 与 `Display::instance()` 尺寸 | 三个开关与帧缓冲都由调用方传入（`hideInterface`/`evilInterface`/`viewWorldMode`、`PixelBuffer target`），尺寸取自 `target` |
| `ICN::ADVBORD` / `ICN::ADVBORDE` | `GameBorder.GoodAssetName` / `EvilAssetName` + `AssetBundle.TryReadIcn`（`TryLoad` 返回 `null` 而不抛） |
| `fheroes2::Blit( in, inX, inY, out, outX, outY, w, h )` | `ImageOps.Blit( target, Point, art, Rect )`，只取图像层、不带变换层 |
| `fheroes2::Rect fixrt / fixpt` 两个局部量 | 端口 `Rect`。参考把 `fixpt` 声明成 `Rect` 却当点用（`fixpt.x += 14`），端口直接写目标点 |

测试 `GameBorderTests`（7 条）：640x480 下上/左边缘条带与 `ADVBORD.ICN` 第 0 帧逐像素一致且地图区不被改写、
隐藏界面时整屏不动、672x512 时四角贴合（重复块加余量正好铺满）、视图世界模式下补丁生效、`RepeatPattern` 的
余量贴边与空请求，以及无档案时 `TryLoad` 返回 `null`。数据缺失时用例静默返回。

全量 **4 782 绿**（Core 267 + Assets 416 + Game 3 103 + UI 996），构建 0 错误 0 警告。

G1 剩余：`interface_base.cpp`（需 `GameArea`/`Radar` 类型，G2/G3）、`interface_cpanel.cpp`、`interface_buttons.cpp`、
`interface_border.cpp` 的 `BorderWindow`（需 `Dialog::FrameBorder`，H 步）。

## 2026-11-22 G1（2/4）：`Dialog::FrameBorder`、`ImageRestorer`、`Interface::BorderWindow`

G1 的第一批依赖落地，`interface_border.cpp` 至此全部转完：

| 参考 | 端口 |
| --- | --- |
| `fheroes2::ImageRestorer`（`engine/image.h:184-231`、`engine/image.cpp:665-773`） | `Core/Imaging/ImageRestorer.cs` |
| `Dialog::FrameBorder`（`dialog.h:177-225`、`dialog_frameborder.cpp:34-77`） | `UI/Dialogs/FrameBorder.cs` |
| `Interface::BorderWindow`（`interface_border.h:35-78`、`interface_border.cpp:420-524`） | `UI/Interface/BorderWindow.cs`（含 `BorderDrag`） |

`ImageRestorer` 的 `_updateRoi()` 逐分支照搬：负起点向内裁、超出远端裁剪、整块在外则塌成 `(0,0,0,0)`；`update()`
先把 `_isRestored` 置回 `false` 再存新矩形，`restore()` 可重复调用而析构不再重复。端口 `PixelBuffer` 只有一层，
参考里「单层就不拷贝变换层」的分支在这里不存在。

`FrameBorder` 的几何三段（`rect` = 内容 + 两侧边框、`area`、`top`）与「先 restore 再 update」的移动语义按参考写。
`RenderRegular` 从 `shadowWidthPx` 处开始拉伸（右侧与底部各留一条阴影），`RenderOther` 整块拉伸，两者都走
`ImageOps.Resize` + `ImageOps.Blit`。

`BorderWindow` 的 `GetRect` 就是参考的 `isHideInterfaceEnabled() && border.isValid()` 分支；隐藏界面时
`SetPosition(x,y)` 先把窗口夹进屏幕、再换算成内容原点、重放边框并调用派生类的 `SavePosition()`。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `ImageRestorer` 里 `_copy.resize()` 复用缓冲 | `PixelBuffer` 尺寸只读，`Update` 重新分配 `_copy` |
| `FrameBorder` 构造时把 restorer 绑到 `Display::instance()`，渲染时自己读 `Settings` + `Assets` | 帧缓冲由构造参数传入，主题与素材由调用方经 `FrameBorder.TryLoadArtwork` + `RenderRegular( target, artwork, rect )` 提供 |
| `BorderWindow` 内部读 `Settings::Get().isHideInterfaceEnabled()` | `HideInterfaceEnabled` 属性由场景赋值（面板重建时本来就会重读设置） |
| `QueueEventProcessing()` 是阻塞循环：虚线框 `DrawBorder` + `MovableSprite` + `while ( le.HandleEvents() … )` | 拆成 `QueueEventProcessing( events )` 返回 `BorderDrag`（`MoveTo` / `Draw`），帧循环由宿主驱动，与虚拟键盘、文字光标同一套做法 |
| 循环里 `Cursor::Get().SetThemes( Cursor::POINTER )` | 未做：光标层在 UI 程序集之上，由宿主在拿到 `BorderDrag` 时处理 |
| `DrawBorder()` 顺带清掉被画像素的变换层 | 端口 `ImageOps.DrawBorder` 只吃单层 `PixelBuffer`，`BorderDrag` 事后自己清（`MakePaintedPixelsVisible`），否则虚框整块被 `reset()` 的跳过标记吃掉 |
| `BorderDrag` 的颜色 `GetColorId( 0xD0, 0xC0, 0x48 )` 存在静态量里 | 改成属性按需解析，避免加载类型时就要求 `KB.PAL` |

测试：`ImageRestorerTests`（6：还图/两端裁剪/整块在外/`Update` 换矩形/`Restore` 可重入/显式 `Restore` 后 `Update`
恢复自动还原）、`FrameBorderTests`（7：边框几何、负尺寸沿用旧矩形、自定义边框宽、析构还图、`RenderOther` 拉伸、
`RenderRegular` 从阴影带之后起拉、空矩形不画）、`BorderWindowTests`（10：正常/隐藏两种 `GetRect`、夹屏、`SavePosition`、
顶边拖动起手与两处否决、`BorderDrag` 跟随光标并画虚线框、鼠标捕获在松开那一帧仍归属面板）。需要 `KB.PAL` 的两条
用例走 `GameAssets.Current`，数据缺失时静默返回。

全量 **4 805 绿**（Core 273 + Assets 416 + Game 3 103 + UI 1 013），构建 0 错误 0 警告。

G1 剩余：`interface_base.cpp`（需 `GameArea`/`Radar`，G2/G3）、`interface_cpanel.cpp`、`interface_buttons.cpp`
（两者都要 `Interface::AdventureMap` 编排器，属 S14-5 / G4）。


## 2026-11-22 G1（3/4）：`Interface::BaseInterface` 基座与 `GameArea`/`Radar` 签名骨架

`interface_cpanel.cpp` 与 `interface_buttons.cpp` 都要 `Interface::AdventureMap`，而编排器又挂在
`Interface::BaseInterface`（`interface_base.h:60-186`）上。按项目规则先给被依赖方落接口，本轮三件：

| 参考 | 端口 |
| --- | --- |
| `Interface::Redraw` 掩码（`interface_base.h:33-59`） | `UI/Interface/Redraw.cs`（`[Flags]`，数值逐个对齐，含 `Icons = Heroes \| Castles`） |
| `Interface::BaseInterface`（`interface_base.h:60-186`） | `UI/Interface/BaseInterface.cs` |
| `Interface::GameArea` + `ScrollingType`/`RedrawLevelType`/三种动画信息（`interface_gamearea.h:34-320`） | `UI/Interface/GameArea.cs` |
| `Interface::Radar`（`interface_radar.h:34-97`） | `UI/Interface/Radar.cs` |

已实现的真逻辑：重绘掩码的累积与 `NeedRedraw`、四条边缘滚动判定（余量就是 `GameArea` 边框的 16 像素，
即端口的 `GameBorder.BorderWidth`）、视口矩形与中点、待滚动标志、动画队列、以及两个动画步进器
（`ObjectFadingOutInfo` / `ObjectFadingInInfo`，每帧 ±20，255→0 与 0→255）。
`GameArea` 的几何（`generate`/`Scroll`/`SetCenterInPixels`/绘制）与 `Radar` 的成像（`_redraw`/`SetZoom`/光标）
方法体统一抛 `NotImplementedException`，分别归 G3 与 G2。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `BaseInterface` 用 `Display::instance()` 的尺寸做边缘滚动 | 判定拆成 `Point cursor` + `Size screenSize` 两个参数，尺寸由宿主传 |
| `_radar( *this )` 自己从 `Display` + `Settings` 取几何 | `Radar( BaseInterface &, PixelBuffer display )`：帧缓冲注入（端口无 `Display` 单例） |
| 抽象方法 `redraw( uint32_t force )` | 改名 `RedrawInterface( Redraw force )`：C# 里方法不能与它接收的枚举类型同名 |
| `renderWithFadeInOrPlanRender()` / `validateFadeInAndRender()` 读 `Game::validateDisplayFadeIn()` | 抛 `NotImplementedException`：全局 `Game` 状态未转（S14），端口淡入由宿主持有的 `DisplayFade` 驱动 |
| `Radar` 的 View World 副本构造、`RedrawForViewWorld()`、`QueueEventProcessingForWorldView()` | 暂不声明：参数类型 `ViewWorld::ZoomROIs` 属于 `kingdom/view_world.cpp`（H），到那时一并补进 `Radar` |
| `GameArea::_inertiaHandler`（`UIScrollInertia`）与 `updateInertia()` | 一起留给 G3（`ui_tool.cpp` 的 `UIScrollInertia` 尚未转） |

一律不加「临时替代」：骨架里没有自造的滚动、没有简化雷达，调用方拿到的是会立刻抛异常的签名。

测试 `InterfaceBaseTests`（22 条）：`Redraw` 十个数值与 `Icons` 别名、掩码累积与 `NeedRedraw`、四条边缘判定
（0/15/16/623/624/464 等边界）、视口矩形与中点、`IsEditor` / `UseMouseDragMovement` / `GetRadar()` 非空、
`RenderWithFadeInOrPlanRender` 抛 `NotImplementedException`、两个动画步进器的 13 帧收敛与字段携带。

全量 **4 827 绿**（Core 273 + Assets 416 + Game 3 103 + UI 1 035），构建 0 错误 0 警告。

G1 剩余：`interface_cpanel.cpp`、`interface_buttons.cpp`，两者下一步随 `Interface::AdventureMap`
（`game_interface.h`）骨架一起落。

## 2026-11-22 G1（3/4）：`AlphaBlit` 与 `Interface::ControlPanel`

隐藏界面后右下角那块 180x36、半透明的五个按钮条（`gui/interface_cpanel.cpp`）转完，连带补它唯一缺的
引擎函数 `fheroes2::AlphaBlit()`。

| 参考 | 端口 |
| --- | --- |
| `fheroes2::AlphaBlit()`（`engine/image.cpp:961-1085`） | `Core/Imaging/ImageOps.AlphaBlit.cs` |
| `Interface::ControlPanel`（`gui/interface_cpanel.h:38-90`、`interface_cpanel.cpp:34-133`） | `UI/Interface/ControlPanel.cs` + `ControlPanelButton` |

`AlphaBlit` 按参考逐像素做：变换层 `1` 跳过、`>1` 先用 `PaletteTransformTable` 把**目标色**改掉再混合；
混合在 6 位域 `( 源 * alpha + 目标 * ( 255 - alpha ) ) / 255` 整数截断，再交给 `PALColorId.Get` 回查调色板；
`alpha == 255` 退化为普通 `BlitSprite`，`alpha == 0` 直接返回。`ControlPanel` 的几何是五块 36x36 顺排、
`SetPos` 同时移动整条与五块，`_redraw` 五块都以 128 半透明叠加。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| `ControlPanel( AdventureMap & )` 记编排器引用，点击直接调 `EventSwitchShowRadar()` 等 | `QueueEventProcessing( LocalEvent )` 返回 `ControlPanelButton`（Radar/Icons/Buttons/Status/EndTurn/None），事件由编排器执行——与 `BorderDrag` 同一套「阻塞循环改宿主驱动」 |
| `ResetTheme()` 在构造里读 `Settings` + `Assets` | 静态工厂 `ControlPanel.Create( AssetBundle?, bool evilInterface )`，缺图返回 `null` |
| 取图走 `Assets::getImage( icn, frame )` | `assets.TryReadIcn( "ADVBTNS.ICN" / "ADVEBTNS.ICN" )`，帧号 4/0/12/10/8 提为常量 |
| `AlphaBlit` 用 `getRGBGamePalette()` 全局调色板 | 形参传 `Palette`（与 `ImageOps.ApplyAlpha` 一致），取 `ToRaw()` 后 `>> 2` 进 6 位域 |
| `AlphaBlit` 三个重载含 `flip` | 只落两个（整图 + 显式落点），`flip` 未用即不声明 |

测试：`AlphaBlitTests`（10：50% 混合落在中间色、通道独立混合、截断取整、`255` 等同普通 blit、`0` 不画、
变换层跳过、两侧裁剪、整块在外、偏移重载、空参抛错）、`ControlPanelTests`（14：五块布局、五块点击命中 +
两块落空、右键不认、点击被消费、半透明叠加 + 边界不出界、空参抛错、无档案返回 `null`、真档案读到按钮组）。

全量 **4 851 绿**（Core 283 + Assets 416 + Game 3 103 + UI 1 049），构建 0 错误 0 警告。

G1 只剩 `interface_buttons.cpp`：它要 `AdventureMap` 编排器与 `Kingdom`/`World`（H + G4），下一步先落
`Interface::AdventureMap`（`game_interface.h`）的签名骨架。

## 2026-11-22 G1 收官（4/4）：`Interface::ButtonsPanel`

冒险地图那八个按钮（`gui/interface_buttons.cpp`）转完，G1 四个文件全部落地。

| 参考 | 端口 |
| --- | --- |
| `Interface::ButtonsPanel`（`gui/interface_buttons.h:38-70`、`interface_buttons.cpp:34-293`） | `UI/Interface/ButtonsPanel.cs` + `ButtonsPanelAction` / `ButtonsPanelInput` |

两行四列 144x72：上排下一英雄/移动/王国/法术，下排结束回合/冒险/存档/系统。每颗按钮吃 `ADVBTNS.ICN`
（邪恶界面 `ADVEBTNS.ICN`）的 `2i` 释放帧与 `2i+1` 按下帧；只有移动按钮会换图——行走 (2,3)、行动 (16,17)、
待机 (18,19)，其中 16..19 只有扩展素材有，缺帧时按文档回落到 (2,3)。`SetButtonStatus` 对应
`_setButtonStatus()`：仅下一英雄/移动/法术三颗会变灰，其余五颗恒可用。`UpdatePressState` 对应参考里那个
`drawOnPressOrRelease` lambda，按下/抬起就把按钮直接画进目标帧并回报是否需要呈现。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 记 `AdventureMap &`，点击直接调 `EventNextHero()` / `EventEndTurn()` … | `QueueEventProcessing( LocalEvent )` 返回 `ButtonsPanelAction`（按钮 + Click/LongPress/RightClick），事件由编排器执行 |
| `_setButtonStatus()` 里查 `GetFocusHeroes()` 与 `world.GetKingdom()` | 面板收 `AdventureButtonStatus`，不碰游戏模型；为此给该记录加了 `HasValidPath`（长按判定要 `GetPath().isValidForMovement()`） |
| `BorderWindow::QueueEventProcessing()` 先行，拖动时直接返回 `GameMode::CANCEL` | 拖动留在基类（返回 `BorderDrag` 由编排器持有），同签名的面板方法用 `new` 隐藏基类成员，文档写明「先调基类」 |
| `QueueEventProcessing` 内部自己弹 tooltip（`Dialog::MessageBox`） | 返回 `RightClick` 动作，由编排器决定提示框 |
| `SavePosition()` 写 `.fh2config` 的面板偏移 | 抛 `NotImplementedException`：`Settings` 尚无位置持久化 |
| 构造函数 + `Display::instance()` | 帧缓冲与区域注入（同 `Radar`）；取图走静态 `Create( AssetBundle?, evil, display, area, artwork )` |

测试 `ButtonsPanelTests`（26 条）：两行四列布局与 `AreaOf`、帧数不足抛 `ArgumentException`、移动按钮三套帧
（行走 / 行动 / 缺帧回落）、三颗条件按钮的灰/亮、八颗按钮逐个点击命中、禁用按钮不响应、块外点击落空、
右键按下报 `RightClick`、单击不误判成长按、八颗按钮逐个绘制到正确方格、禁用帧被压暗、按下/抬起各画一次、
`SavePosition` 抛 `NotImplementedException`、无档案返回 `null`、真档案读到按钮组。

全量 **4 877 绿**（Core 283 + Assets 416 + Game 3 103 + UI 1 075），构建 0 错误 0 警告。

顺带：`FHeroes2.Game.AdventureMap.ButtonsPanel`（老场景自绘版）与新的 `UI/Interface/ButtonsPanel` 同名，
`AdventureMapScene` 与测试用 `using ButtonsPanel = …;` 别名钉住各自那一个，等场景被 `Interface::AdventureMap`
替换时一并删掉老版本。

G1 完成。下一步 G2：`interface_radar.cpp`，填 `Radar` 的成像与光标（`ViewWorld::ZoomROIs` 相关的三件留到 H）。

## 2026-11-22 G2（1/…）：`Interface::Radar`

G1 骨架里 `Radar` 的方法体全是 `NotImplementedException`，本轮把成像、光标与事件补齐，G2 首项落地。

| 参考 | 端口 |
| --- | --- |
| `Interface::Radar`（`gui/interface_radar.h:33-121`、`interface_radar.cpp:142-499`） | `UI/Interface/Radar.cs` + `RadarAction` |
| `GetPaletteIndexFromGround` / `GetPaletteIndexFromColor`（匿名命名空间） | `Radar.GetPaletteIndexOfGround` / `GetPaletteIndexOfColor`（`public static`，测试要直查） |

144x144 小地图：`SetZoom` 取 `area.width / world.w()`，36/72/144 三档地图分别 4/2/1 像素每格。
`RedrawObjects` 沿参考的判定链逐格取色——英雄色（`OBJ_HERO`）、灯塔/炼金实验室/矿场/锯木厂的占领色、
城镇色、道路色、地形色，丘陵与树木压深 3 级；`_roi` 只在覆盖全图时先刷黑，重绘完把 ROI 收回全图，
保留参考的部分重绘语义（用于滚屏时只更新露出的条带）。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 全局 `world`、`Display::instance()`、`Settings::Get()` | `Build( World )` 注入世界，帧缓冲与区域由构造函数注入，`HideInterfaceEnabled` / `ShowRadar` 做成属性 |
| `_redraw( bool )` 内部取 `Players::FriendColors()` | `RedrawRadar( bool, PlayerColor )`，友方颜色作参数；`redrawForEditor` 对应 `RedrawForEditor( bool )` |
| `QueueEventProcessing()` 里直接 `_interface.SetCenter()`、右键弹 `Dialog::MessageBox` | 同签名方法用 `new` 隐藏基类（文档写明「先调基类」），返回 `RadarAction`（`MapCentered` / `ShowTooltip` / `None`），由编排器执行 |
| `SavePosition()` 写 `.fh2config` 的雷达偏移 | 写进 `Settings.PosRadar`（会话内），落盘等配置写入器 |
| 拷贝构造 `Radar( const Radar &, const Display & )`（View World 用）、`RedrawForViewWorld`、`QueueEventProcessingForWorldView` | 留到 H：要 `ViewWorld::ZoomROIs`；本轮 `RedrawCursor()` 无参重载同理依赖 `GameArea.VisibleTileRoi`（G3），先抛 `NotImplementedException` |
| `Radar( BaseInterface & )` 自算位置 | 构造收 `Rect`，取图走静态 `Create( BaseInterface, PixelBuffer, AssetBundle?, evil )`（`HEROLOGO.ICN` / `HEROLOGE.ICN` 第 0 帧） |

顺带：`BorderDrag.MakePaintedPixelsVisible` 由 `private` 提为 `internal`，雷达画 logo 与浮动边框共用同一套「把刚画的像素转成可见」逻辑。

测试 `RadarTests`（26 条）：三档地图的 zoom 与 `RenderArea`、`Build(null)` 抛异常、`SetRedraw` 只收雷达位并累加、
`SetRenderArea` 裁剪到世界、折叠且隐藏时丢弃 render area、全雾全黑、按 zoom 缩放每格、折叠绘 logo、隐藏不绘、
光标矩形 40x30 @ (50,30) 且帧内含 `RADARCOLOR`、整图可见时无光标、无参 `RedrawCursor` 抛异常、右键报
`ShowTooltip`、区域外点击落空、六色与组合色映射、九种地面与未知地面映射、`SavePosition` 写入 `Settings.PosRadar`、
`Create` 无档案 / 真档案两路。

全量 **4 903 绿**（Core 283 + Assets 416 + Game 3 103 + UI 1 101），构建 0 错误 0 警告。

G2 剩余：`interface_status.cpp`（`Widgets/StatusPanel.cs`）、`interface_icons.cpp`（`Widgets/ItemsBarLayout.cs`）。

## 2026-11-22 G2（2/3）：`Interface::StatusPanel`

G1 骨架与 `StatusPanelPainter` 之后，状态栏按 `interface_status.cpp` 整块重写成 `UI/Interface/StatusPanel.cs`。

| 参考 | 端口 |
| --- | --- |
| `Interface::StatusPanel`（`gui/interface_status.h:33-90`、`interface_status.cpp:55-524`） | `UI/Interface/StatusPanel.cs` + `StatusType` / `StatusPanelAction` |

五个状态：日期、王国资源、焦点英雄/城堡的部队、「找到少量资源」、AI 回合沙漏。`NextState()` 只在前三态之间循环
（日期 → 资源 → 部队），左键点击切换并立刻 `SetRedraw( REDRAW_STATUS )`；`_redraw()` 按面板高度分三段布局——
装得下三态画三态、装得下两态画当前态加日期、否则只画当前态（居中），`AiTurn` 永远独占整块。
背景 `_drawBackground()` 在普通布局下把 `STONBACK` 的第 11 行起重复 46 像素，让面板一直贴到屏幕底边；
隐藏界面时改为填 `GetColorId( 0x51, 0x31, 0x18 )` 的深棕。`Reset()` 回到日期态并清掉「上次资源」，
`SetResource()` 记下资源与数量并把 `ShowLastResource` 的 2.5 秒计时器重置。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 全局 `world`、`Display::instance()`、`Assets::getImage()` | 世界、帧缓冲、`IViewWorldSpriteSource`、`IMiniMonsterSprites` 全部构造注入；取图与 `Radar` 共用同一套源接口 |
| `Settings::ShowStatus()` / `isEvilInterfaceEnabled()` / `IsGameType( TYPE_AUTO_PLAYTEST )` | `ShowStatus` / `EvilInterface` / `IsAutoPlayTest` 属性，由编排器与 `Settings` 同步（面板早于设置定型就建出来了） |
| 右键弹 `Dialog::MessageBox`，拖动存全局 `_border` | 右键返回 `StatusPanelAction.ShowTooltip`、拖动经 `Dragging` 交回编排器；同签名 `QueueEventProcessing` 用 `new` 隐藏基类并文档写明「先调基类」 |
| `SavePosition()` 写 `.fh2config` | 写进 `Settings.PosStatus`（`Settings` 为此补了 `PosStatus` / `PosRadar` 与两个 setter），落盘等配置写入器 |
| `drawAITurnProgress()` 内部拉事件队列并呈现 | 只做动画节流与 `RedrawInterface( REDRAW_STATUS )` / `SetRedraw( REDRAW_GAMEAREA )`，事件循环归宿主 |
| 隐藏界面时的浮动石框 | 无石框：九宫格边框图 `Dialog::FrameBorder` 没有可用的取图源，同 `Radar` 的偏差 |
| 焦点英雄/城堡取 `Interface::GetFocus()` | `InterfaceFocus.GetFocusHeroes()` / `GetFocusCastle()`（G4 会再对拍一遍） |

顺带修掉一个真 bug：`Copy()` 原先走 `ImageOps.BlitSprite( …, new Rect( srcX, srcY, w, h ) )`，而那个重载会把
源切片自身的偏移再加到目标点上，于是「重复中段」的每一块都向下错位——`STONBACK` 11 行之后整块背景漏画。
改用 `ImageOps.Blit( … , sprite.Image, rect )`，与 `StandardWindow.CopyArea` 同一语义。

测试 `StatusPanelTests`（22 条）：两种布局下的 `SetPos`（普通布局拉到屏幕底、隐藏布局加 16 像素边框）、
左键在日期/资源/部队之间循环、资源态下进入部队态被拒、右键报 `ShowTooltip`、区域外点击落空、隐藏且不显示时
不画任何像素、无日期时只画背景、有真实资源时日期态/资源态/AI 沙漏都画出了像素（`AssetLocator` 找不到档案就
跳过）、`Reset()` 回日期态、`DrawAiTurnProgress` 触发一次状态重绘、`SavePosition` 写进 `Settings.PosStatus`。

全量 **4 925 绿**（Core 283 + Assets 416 + Game 3 103 + UI 1 123），构建 0 错误 0 警告。

## 2026-11-22 G2（3/3）：`Interface::IconsPanel`

图标栏按 `interface_icons.cpp` 整块重写成 `UI/Interface/IconsPanel.cs`（G2 收尾）。

| 参考 | 端口 |
| --- | --- |
| `Interface::IconsBar`（`gui/interface_icons.h:30-240`）、`HeroesIcons` / `CastleIcons` | `IconsPanel` 内的 `abstract IconsBar<TRow> : ListBox<TRow>` 与 `HeroIcons` / `CastleIcons` |
| `Interface::IconsPanel`（`interface_icons.cpp:135-540`） | `IconsPanel` + `HeroesCastlesIcons` / `IconsPanelAction` |

布局按参考是**左右两列**：英雄列 `(x, y)`、城堡列 `(x+72, y)`，每列 56×32 的槽、46×22 的头像内缩 5 像素；
滚动槽 `(x+59, y+19, 10, 槽数*32-38)`，上下按钮 `(x+57, y+1)` 与 `(x+57, y+槽数*32-15)`；
`setPos` 先把当前选中存下来，重设 `SetAreaItems` / 滑块 / 按钮后重建列表内容再把选中恢复。
`resetIcons` 在「AI 控制且未授权自动模式」时直接返回。换序走 `Kingdom.MoveHeroToFront` + `MoveRowToFront`，
两侧 `Select()` 都会清掉另一侧的选中。背景仍是 `ADVBORD` 中段 `(w-160, 176, 72, 32)` 的重复 + 底部切片 + `LOCATORS` 空槽。

偏差：

| 参考 | 端口做法 |
| --- | --- |
| 全局 `world`、`Display::instance()`、`Assets::getImage()` | `World`、`PixelBuffer`、`IViewWorldSpriteSource` 构造注入 |
| `isEvilInterfaceEnabled()` / `ShowIcons()` / `isHideInterfaceEnabled()` / 拖动存全局 `_border` | `EvilInterface` / `ShowIconsEnabled` / `HideInterfaceEnabled` 属性，拖动返回 `IconsPanelAction.StartedDragging` |
| 双击/右键直接开对话框、`WORLD_MOVE_HERO_PORTRAIT_UP_MODIFIER` 由主循环查询 | 事件返回 `IconsPanelAction`（`OpenHeroDialog` / `OpenCastleDialog` / `ShowHeroQuickInfo` / `ShowCastleQuickInfo` / `FocusHero` / `FocusCastle`），修饰键状态由宿主经 `QueueEventProcessing(events, moveHeroPortraitPressed)` 传入 |
| `Hero::PortraitRedraw( PORT_SMALL )` 画头像 | 按 `PORTXTRA` 底 + `MINIPORT[PortraitId-1]` + `MOBILITY` / `MANA` / `MISC4[14]` 手绘（`Hero::PortraitRedraw` 尚未移植） |
| `SavePosition()` 写 `.fh2config` | 写进 `Settings.PosIcons`（新增 `PosIcons` / `SetPosIcons`），落盘等配置写入器 |
| 隐藏界面时的浮动石框 | 无石框：九宫格边框图没有可用的取图源，同 `Radar` / `StatusPanel` |

顺带修掉两个真 bug：骨架原先把两个列表**上下堆叠**、滚动槽 16 宽且 y 偏移 1，与参考的「左右两列 + 10 宽槽」
不符，已整段按参考重写；`SetIconsCount` 原先顺手调 `SetAreaMaxItems`，而那会 `Reset()` 清掉选中，导致
`setPos` 里的「保存-恢复选中」永远拿不到选中项——改为只记数量（参考 `setIconsCount` 也只赋值）。
另外 `ResetIcons` 不再自行请求重绘（参考调用方自己重绘），选中标记只在 `Show` 时画，`ActionCurrentDn` 也报焦点。

测试 `IconsPanelTests`（20 条）：构造参数校验、可见性设置、左右两列几何、640/736/768 三种屏高的 4/7/8 槽、
隐藏界面 2 槽、滚动槽位置与滑块、`SetPos` 保留选中、`SavePosition`、`ResetIcons` 填充与取消选中、
两侧 `Select` 互斥、单击报焦点、双击开英雄/城堡对话框、右键快捷信息、带修饰键的单击换序、`IsVisible` 三态、
`HideIcons` 只影响一列（选中框颜色查 `KB.PAL`，所以整个类加入 `GlobalArchiveCollection` 串行执行，缺数据则跳过）。

全量 **4 945 绿**（Core 283 + Assets 416 + Game 3 103 + UI 1 143），构建 0 错误 0 警告。

G2 完成。下一步 G3：`interface_gamearea.cpp`（1 053 行，滚动、绘制、tooltip 命中）。

## 2026-11-22 G3 前置：`maps_tiles_render` 接口骨架（ADR-197）

开工 G3 前先核了依赖：`interface_gamearea.cpp` 的 `Redraw` / `redrawOnlyFog` / `renderTileAreaSelect` /
`generateUltimateArtifactAreaSurface` 四个绘制入口全部调用尚未移植的 `maps/maps_tiles_render.cpp`（1 327 行），
而现有 `Game/AdventureMap/AdventureMapView.cs` 是 `Mp2Map`/`Mp2TileInfo` 的过渡渲染器，不是 `Maps::Tile` 部件
模型的移植，不能当等价物。按用户决定（选 C，2026-11-22）走「被依赖文件先落接口骨架」：

| 新增 | 内容 |
| --- | --- |
| `UI/Interface/TileRendering.cs` | `maps_tiles_render.cpp` 的 16 个入口，签名 + 文档注释，方法体抛 `NotImplementedException` |
| `Game/Maps/ObjectRenderingInfo.cs` | `gui/ui_object_rendering.h` 的 DTO，参考即 header 内实现，故直接写全（H 行「`+ui_object_rendering.h`」提前完成） |

`TileRendering` 收的是 `redrawEmptyTile` / `redrawFlyingGhostsOnMap` / `redrawTopLayerObject` / `drawFog` /
`redrawPassable` / `redrawBottomLayerObjects` / `drawByObjectIcnType` / `getMonsterSpritesPerTile` /
`getMonsterShadowSpritesPerTile` / `getBoatSpritesPerTile` / `getBoatShadowSpritesPerTile` /
`getMineGuardianSpritesPerTile` / `getHeroSpritesPerTile` / `getHeroShadowSpritesPerTile` /
`getEditorHeroSpritesPerTile` / `getTileSurface`，每个都标了参考行号；十个匿名命名空间助手属实现细节，骨架里不出现
死代码。两处结构偏差（`TileRendering` 落 UI 层、`icnId` 换成 `IcnType`）写在类注释与 ADR-197 里。

顺带把两个「小到能直接写完」的前置件按参考落了实现，不给 G3 留桩：

| 新增 | 参考 | 内容 |
| --- | --- | --- |
| `Core/Timing/Time.cs` | `engine/timing.h:31-60` | `getMs` / `getS` / `reset`，时钟可注入（与 `TimeDelay` 同款） |
| `UI/Widgets/ScrollInertia.cs` | `ui_tool.h:348-407`、`ui_tool.cpp:950-1076` | `UIScrollInertia` 全量：100 ms 窗口、1/256 px/ms 定点的释放速度、`(200-dt)/200` 衰减、5929 / 169 两个阈值、撞边单轴归零 |

`ScrollInertia.Update(getPosition, setPosition)` 保持参考的双回调形状，G3 直接把自己取/设中心的两个方法传进去即可。

这条处理方式已按用户要求固化成规矩：`docs/PLAN.md` 总体原则第 6 条的「依赖先落接口」补了「对目录内单个文件同样成立」，
ADR-197 记为后续同类情况的标准做法。

构建 0 错误 0 警告；测试基线不变（4 945 绿）。下一步：G3 本体（几何 / 滚动 / 事件 / 命中）与 `TileRendering` 的实现一起推进。

## 2026-11-22 G3 前半：`GameArea` 几何、滚动与输入

`gui/interface_gamearea.cpp` 拆成两半，先落**不依赖瓦片绘制**的那一半（参考 328-408、862-1072、1072-1285 的几何与事件，
加头文件里的内联成员）：

| 已实现 | 说明 |
| --- | --- |
| `Generate` / `SetAreaPosition` / `SetCenter` / `SetCenterInPixels` / `SetCenterToTile` | 视口放置与夹取；`_visibleTileCount` 多留一格；世界小于窗口时居中 |
| `VisibleTileRoi` / `GetStartTile` / `GetValidTileIdFromPoint` / `GetRelativeTilePosition` / `GetInternalPosition` / `ShiftCenter` | 像素↔瓦片换算，负偏移走 floor 修正 |
| `Scroll` / `SetScroll` / `GetScrollCursor` / `NeedScroll` | 四向边缘滚动，速度按 `AdventureMapScrollSpeed` 位移 `2 << speed`；惯性运行时拒绝边缘滚动 |
| `QueueEventProcessing` | 拖动阈值 10 px、平滑滚动把位移交给 `ScrollInertia`、点击/右键/长按交给接口、光标只在瓦片变化时更新 |
| `UpdateInertia` / `SetFastScrollStatus` / `MouseIndicatesFastScroll` | 快速滚动死区 3 px，参考的「参考点跟着光标走」逻辑照搬 |
| `AddObjectAnimationInfo` / `GetObjectAlphaValue`×2 | 动画队列与不透明度查询 |
| `UpdateMapFogDirections` + `Game/Maps/TileFog.cs` | `maps_tiles_helper.cpp:2086-2212` 的一格缓存扫描，直接写全（不是桩） |

**依旧抛 `NotImplementedException`**：`Redraw` / `RedrawOnlyFog` / `RenderTileAreaSelect` /
`GenerateUltimateArtifactAreaSurface` / `RunSingleObjectAnimation`——整块 `TileUnfitRenderObjectInfo` 分层逻辑要吃
`TileRendering` 的输出，等它实现一起落。`updateObjectAnimationInfo` 属同一批（只有 `Redraw` 推进动画队列）。

三处记在类注释里的偏差：`mouseIndicatesFastScroll` 的屏幕尺寸改由 `Generate` 记住（无 `Display::instance()`）；
控制面板遮挡改问 `BaseInterface.ControlPanelAreaForInput`（`AdventureMap` 属 G5）；`TileFog` 的 `worldHeight` 用
`World.Height` 而非参考笔误的 `world.w()`。`QueueEventProcessing` 里参考把 `_windowROI.x` 用在 Y 分量上，原样保留
并注明（两种布局下上下左右边框相同）。

`RadarTests.RedrawCursor_AsksTheGameAreaWithoutAnExplicitRectangle` 之前断言视口未移植，现在改成断言雷达真的画出
视口框。构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G3 后半（一）：`TileRendering` 的对象与雾

`maps/maps_tiles_render.cpp` 1 327 行按「先画一块完整的瓦片」切开，本轮落的是**不依赖精灵切分**的那一半：

| 已实现 | 参考位置 / 说明 |
| --- | --- |
| `RedrawEmptyTile` | 1 200-1 240：`STON.TIL` 索引 `20/24/28/32` + 角点 `(|y|%4)*4 + |x|%4`；取不到图直接跳过（端口图集可以为空） |
| `RedrawPassable` | 1 262-1 296：只在 `PassableDirections != ALL` 时叠 `PassableViewSurface`；游戏内用 `MP2::isInGameActionObject`，编辑器用 `isOffGameActionObject`（端口 `Mp2ObjectTypes.IsActionObject`） |
| `GetPassableViewSurface`（私有） | 1 60-277 的等价实现：31×31、`quarter = 10`、`threeQuarters = 21`，红 `0xBA`、绿 `115`/`90`；`PassableViewSurfaces` 字典缓存 |
| `RenderObjectPart` / `RenderMainObject`（私有） | 100-186：按 `part.Uid` 取不透明度、blit 本体、再按 `AnimationFrames` 叠动画帧；魔法花园被访问过后用整轮动画之后的那一帧（`metadata[1] == 0`） |
| `IsObjectPartDirectRenderingRestricted` / `IsTileDirectRenderingRestricted`（私有） | 65-98：`UNKNOWN`/`BOAT32`/`MINIHERO`（主对象另有 `MONS32`，随机怪物除外）交给专用渲染器 |
| `IsHiddenForPuzzle`（私有） | `mp2.cpp:186-222`：拼图地图只留地形与地标，篝火帧 `124-137` 单独判，其余问 `DiggingHole.IsSprite` |
| `RedrawBottomLayerObjects` | 932-979：先画本层地面部件，再画主对象，最后补 `FLAG32`——旗帜在格式里是城堡的地面部件，但必须压在城堡上面；栈上 16 槽收集，超限只断言 |
| `DrawByObjectIcnType` | 981-1000：编辑器改一格时按 ICN 重画该格的全部部件 |
| `RedrawTopLayerObject` | 667-690：拼图地图先过滤；首行 `FLAG32` 且上方仍有雾时只画边框以下的部分 |
| `DrawFog` | 692-913：`CLOF32` 整格（`(x+y)%4`）或 `CLOP32` 的 0-33 号边缘图，34 条方向判定按参考顺序逐条保留（掩码有重叠，顺序即格式），`revert` 负责左右镜像；没有命中的掩码按整格覆盖并 `GameLog.Warn`（参考是 `assert`） |
| `RedrawFlyingGhostsOnMap` | 648-665：`OBJNHAUN` 的 `index % 15`；首行 `pos.y == 1` 且上方有雾时裁掉伸进边框的部分 |

配套新增：

* `Game/AdventureMap/MapSprites.cs`：参考的 `Assets::getImage` / `getTileImage` / `GetPalette` 是进程级全局表，
  端口用静态类承担（渲染函数的签名里没有资源包可传）。`Initialize(AssetBundle?, Palette?)` 尚未在组合根接线。
* `Core/Imaging/ImageOps.AlphaBlit.cs` 的 `AlphaBlitSpriteClipped`：一次覆盖 `FitToRoi` + `AlphaBlit` + `flip`
  三种裁剪/镜像组合，`alpha == 0` 直接返回，`255` 走纯 blit。
* `GameArea.BlitOnTile`×2 / `DrawTile`：直接落到上面的裁剪 blit 与 `ImageOps.Blit`，不再抛异常。

**仍然抛 `NotImplementedException`**：无（详见下一节，八个 `Get*SpritesPerTile` 已在第二批补齐）。

构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G3 后半（二）：`TileRendering` 的精灵切分

`maps_tiles_render.cpp` 的第二半——把精灵切成瓦片大小再分格输出。至此该文件的**全部**入口都有实体实现。

| 已实现 | 参考位置 / 说明 |
| --- | --- |
| `GetMonsterSpritesPerTile` / `GetMonsterShadowSpritesPerTile` | 1 002-1 085：怪物帧号 `monsterIndex * 9`；被英雄攻击时 +7 并按方向再加 1，否则叠共享动画序列 `MonsterAnimationSequence.Frames[15]` |
| `GetBoatSpritesPerTile` / `GetBoatShadowSpritesPerTile` | 1 088-1 143：`IcnIndex == 255` 时按 18 号空船画；`index > 128` 判定右半侧镜像，锚点 `TileWidth + 1 - x - width`，纵向 `TileWidth - 11` |
| `GetMineGuardianSpritesPerTile` | 1 146-1 181：`OBJNXTRA` 的 `spellId - SetEGuardian`，即四种守卫在枚举里必须连续 |
| `GetHeroSpritesPerTile` | 1 184-1 263：本体 + 旗帜 + 深水船首浪花；站立的英雄旗帜走冒险地图动画帧，乘船时旗帜不单独摆动 |
| `GetHeroShadowSpritesPerTile` | 1 266-1 295：乘船抬 11 px，阴影图集里左向行与英雄图集顺序不同（`45/54/63` vs `77/68/59`） |
| `GetEditorHeroSpritesPerTile` | 1 298-1 321：`MINIHERO`，锚点 `(0, 32 - 50)` |
| `GetMonsterSpriteIndices`（私有） | 292-334：先 `ScanAroundObject(ActionHero, false)` 找攻击者，再按 `Hero.AttackedMonsterTileIndex` 匹配 |
| `DoesHeroImageNeedToBeReflected` / `GetHeroSpriteInfo` / `GetFlagSpriteInfo` / `GetShadowSpriteInfo` / `GetFrothSpriteInfo`（私有） | 336-624：种族→图集、颜色→旗帜图集、四个站立旗帜偏移表（9 帧）全部照搬 |
| `AppendDividedSprite` / `AppendMonsterSpritePieces`（私有） | 精灵切分与追加的公共体；参考把这两段在八个函数里各抄了一遍，端口合并成两处 |

配套新增：

* `Core/Imaging/ImageOps.Sprites.cs` 的 `ImageOps.DivideImageBySquares` + `DividedSquare` 记录：
  `engine/image.cpp:2127-2170`，含负偏移时把格号向下取整的修正（C++ 负数整除向零截断，C# 相同，所以修正是必要的），
  空图返回空列表——参考里空图同样什么也不输出。
* `Game/Heroes/Hero.cs` 的 `Hero.AttackedMonsterTileIndex` / `SetAttackedMonsterTileIndex`（`heroes.h:669-677`）：
  参考只在 `heroes_action.cpp` 攻击前后写它，那一半还没转，所以现在恒为 `-1`，怪物渲染恒走动画序列分支。

偏差（已写进代码注释）：

* 参考 `DEBUG_LOG( DBG_GAME, DBG_WARN, ... )` 的几处未知值分支（未知种族/颜色/方向）在端口走 `GameLog.Warn`
  并返回 `IcnType.Unknown`/零偏移；参考在 `assert( 0 )` 之外同样是「不画」。
* `GetMonsterSpriteIndices` 里 `mp.x * mp.y` 在参考是 uint32 乘法，端口用 `long` 承接后再取模，避免大地图溢出。

构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G3 收尾：`GameArea` 的绘制层

`gui/interface_gamearea.cpp` 的消费方落地，新文件 `src/FHeroes2.UI/Interface/GameArea.Render.cs`
（`GameArea` 改为 `partial`），至此该参考文件**全部成员**都有实体实现。

| 已实现 | 参考位置 / 说明 |
| --- | --- |
| `Redraw` | 408-805：地形 → 下层 level 3/1 → tile-unfit 底部背景 → 阴影 → tile-unfit 阴影 → 英雄下半身 → 低优先级 → level 0 → tile-unfit 中间 → 高优先级 → 顶层（tall 延后）→ 飞鬼 → 路线 → 通行性/雾+城镇剪影；最后 `updateObjectAnimationInfo()` |
| `TileUnfitRenderObjectInfo` / `PopulateStaticTileUnfitObjectInfo` / `PopulateStaticTileUnfitBackgroundObjectInfo` / `PopulateHeroObjectInfo`（私有） | 72-293：七个桶的压入规则（`x < 0` 用 `emplace_front`，否则 `emplace_back`）、越界阴影丢弃、英雄移动方向决定高/低优先级、`imagePos.Y == 1/-1` 特例、英雄在城中时下半身改走 `bottomImages` |
| `IsTallTopLayerObject`（私有） | 306-325：用下一行的 `TopObjectParts` 同 `Uid` 判定，排除 `Flag32` |
| `RenderImagesOnTiles`（私有） | 295-304 |
| `RedrawOnlyFog` | 807-835：`CLOF32` 整格 `(x + y) % 4` |
| `RenderTileAreaSelect` | 837-863：`_windowROI ^ imageRoi` + 四条 `Fill`，颜色 115 / 181 |
| `GenerateUltimateArtifactAreaSurface` | 908-937：448×448、`LEVEL_OBJECTS` + 拼图过滤、`ROUTE` 0 号 + 8 px、`PAL::TAN` |
| `RunSingleObjectAnimation` | 1257-1276：入队 |
| `UpdateObjectAnimationInfo`（私有） | 1223-1233 |
| `ObjectFadingOutInfo.RemoveObjectFromTile` | 1278-1285：参考析构函数「淡出完成后把对象从格子上摘掉」 |

偏差（已写进代码注释）：

* `std::map<Point, deque>` 没有顺序对应物：七个桶是 `Dictionary<Point, LinkedList<...>>`，绘制前按
  `OrderBy(key.Y).ThenBy(key.X)` 还原参考的先行后列顺序；`LinkedList` 同时承担 `emplace_front/back`。
* 参考的 `DIRECTION_ALL` 在端口是 `Directions.All`（`Direction` 是枚举，常量放在 `Directions` 静态类）。
* 参考雾中城镇剪影判 `OBJ_CASTLE || OBJ_NON_ACTION_CASTLE`，端口的等价对是
  `Mp2ObjectType.Castle || Mp2ObjectType.ActionCastle`（`ActionObjectType = 128`）。
* 参考 `( posY > 0 ) ? ( 上一格全雾 ) : true` 的两处（英雄在船上、空船）在端口写成 `posY == 0 || ...`，
  保持首行「上一格视作有雾」的语义。
* 取不到图集条目（`getTileSurface` / `CLOF32` / `ROUTE`）时端口直接跳过，参考对着必然存在的资源包不做判断。
* `GenerateUltimateArtifactAreaSurface` 参考拷贝当前 `GameArea`，端口按同一 `BaseInterface` 新建一个（尺寸随即被
  `SetAreaPosition` 覆盖，只有格偏移再被读）；参考的 `_disableTransformLayer()` 在端口无对应物
  （`PixelBuffer` 就是参考的非变换形态）。
* `runSingleObjectAnimation` 的阻塞循环（等延时 + 泵事件 + 提交显示）没有移植：端口由场景逐帧推进，
  本轮只入队，已在 remarks 写明调用方需要在场景侧表达「播完再走」。
* `updateObjectAnimationInfo` 里出队的条目要执行参考析构函数的摘除逻辑——C# 没有确定性析构，改为在
  `RemoveAt` 之后显式调 `ObjectFadingOutInfo.RemoveObjectFromTile()`。

**仍然抛 `NotImplementedException`**：无（`GameArea.cs` 与 `GameArea.Render.cs` 都已清空）。

构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G4 前半：`interface_focus.cpp` 与它需要的承载类

`gui/interface_focus.cpp`（287 行）全部落地，同时补上它必须挂靠的 `Interface::AdventureMap`。

| 已实现 | 参考位置 / 说明 |
| --- | --- |
| `AdventureMap`（新文件 `UI/Interface/AdventureMap.cs`） | `game/game_interface.h:73-184`：`BaseInterface` 的具体子类，持有 `_iconsPanel` / `_statusPanel` / `AudioManager`，提供 `GetIconsPanel()` / `GetStatusPanel()` |
| `SetFocus( Hero, bool )` | `interface_focus.cpp:45-86`：先隐藏旧焦点英雄的路线、人类英雄才 `calculatePath`、`ShowPath(true)`、`Focus.Set`、图标条选中、`GameArea.SetCenter`、状态栏切 ARMY、按 `UpdateSoundsOnFocusUpdate` 重算环境音并重启地形音乐 |
| `SetFocus( Castle )` | `interface_focus.cpp:88-118`：隐藏英雄路线、`Focus.Set(castle)`、图标条选中城堡、居中、状态栏切 FUNDS、同样重算音景 |
| `UpdateFocus()` | `interface_focus.cpp:120-144`：按 `Focus::first` 分派回两个 `SetFocus` |
| `ResetFocus( int, bool )` | `interface_focus.cpp:146-221`：`FIRSTHERO` 找非 SLEEPER 英雄、`HEROES` / `CASTLE` 各三分支（旧焦点仍在 → 复用；否则取列表首个；列表空 → `Focus.Reset`），并按需重置图标条滚动位置 |
| `RedrawFocus()` | `interface_focus.cpp:255-287`：图标条选中/取消选中随焦点同步，再置 `REDRAW_GAMEAREA｜REDRAW_RADAR_CURSOR` 与对应图标条、状态栏 |
| `AdventureSounds`（新文件 `Game/Audio/AdventureSounds.cs`） | `game/game.cpp:155-163` + `194-325`：`Game::UpdateSoundsOnFocusUpdate()` 开关、`Game::EnvironmentSoundMixer()` 的 ±3 格扫描、按距离稳定排序、32−2 声道分配、同向同音合并取更近、`AudioManager.PlayLoopSoundsAsync` |
| `Hero.ShowPath( bool )` | `heroes.h:518-521`（`HeroPath.Show()/Hide()` 端口本来就已存在，只缺英雄层入口） |
| `IconsPanel.SetRedraw()` / 公开 `SetRedraw( HeroesCastlesIcons )` | `interface_icons.h:189-192`：端口原来把这个重载写成 private，`redrawFocus()` 需要无参版本 |

偏差（已写进代码注释）：

* 参考的构造函数用 `*this` 就地建面板（走全局资源包）；端口面板一律通过工厂参数拿已加载的贴图，所以 `AdventureMap` 改为注入已建好的 `IconsPanel` / `StatusPanel` / `AudioManager`。按钮条与控制面板等事件那一遍再用到，暂不进构造。
* 参考是单例 `AdventureMap::Get()`；端口不加单例（测试要能独立构造），由场景层持有实例。`AdventureMapScene` 的接线在 G5。
* 参考的 `assert( hero->GetColor() == Settings::Get().CurrentColor() )` 这类断言没有移植——越权调用是调用方的 bug，面板本身能容错。
* 端口的 `Hero` / `Castle` 没有 `GetCenter()`，改用 `MapGeometry.GetPoint(index)`（与 `Hero.Fog.cs`、`Castle.Boats.cs` 现有写法一致）。
* `Game::EnvironmentSoundMixer()` 落在 `Game/Audio/` 而不是 `game.cpp` 的等价文件（端口没有 `Game` 门面）；`fheroes2::tileWidthPx` 在端口无家，先作为该文件的私有常量，并注明 UI 侧的 `GameBorder.TileWidth` 不可跨程序集使用。
* 参考 `std::lround( distance * 255 / maxDistance )` 的半数进位方向与 `Math.Round` 默认不同，改用 `MidpointRounding.AwayFromZero`；`std::stable_sort` 由 `OrderBy` 承接（LINQ 保证稳定）。
* `std::map<M82Sound, vector<...>>` 用 `SortedDictionary<M82Sound, List<...>>` 构造，最后投影成 `IReadOnlyDictionary<M82Sound, IReadOnlyList<...>>` 再交给 `PlayLoopSoundsAsync`（后者参数是只读字典）。

**仍然抛 `NotImplementedException`**：`AdventureMap.RedrawInterface` / `Reset`（`game/game_interface.cpp`）、
`MouseCursorAreaClickLeft` / `MouseCursorAreaPressRight` / `MouseCursorAreaLongPressLeft` / `UpdateCursor`
（`game/game_startgame.cpp`）——它们是 G5 的收口件。

**未开工**：`interface_events.cpp`（641 行）。它的 25 个事件几乎都返回 `fheroes2::GameMode`，端口还没有这个枚举，
也没有 `Dialog::AdventureOptions` / `Dialog::FileOptions` / `Game::OpenHeroesDialog` 这类门面，需要先落这两层骨架再搬事件体；
`FileOptionsDialog` 目前自带一套 `NEW_GAME`/`LOAD_GAME` 结果枚举，届时统一到 `GameMode`。

构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G4 后半：`interface_events.cpp`

`gui/interface_events.cpp`（641 行）全部落地，作为 `AdventureMap` 的第二个 partial（`UI/Interface/AdventureMap.Events.cs`）。

| 已实现 | 参考位置 |
| --- | --- |
| `ShowPathOrStartMoveHero()` / `MoveHeroFromArrowKeys()` / `StartHeroMove()` | `interface_events.cpp:77-131`：新目标就 `calculatePath` + 置 `REDRAW_GAMEAREA｜REDRAW_BUTTONS`；目标没变且路线可用且 `MayStillMove` 就开走（`SetFocus`+`RedrawFocus`+`SetMove(true)`+`PassAnimationDelay(CURRENT_HERO_DELAY)`） |
| `EventSwitchFocusedHero()` | `:133-141`：点的格子上的英雄属于当前玩家且不是当前焦点时才切 |
| `EventNextHero()` | `:143-179`：从焦点英雄的下一个开始环形找 `MayStillMove(true,false)`；焦点英雄已不在列表时从第一个开始 |
| `EventHeroMovement()` | `:181-198`：能走就走，否则可重访对象转 `EventDefaultAction`，否则路线有效时弹「英雄移动力用尽」 |
| `EventResetHeroPath()` | `:200-210` |
| `EventKingdomInfo()` | `:212-218` |
| `EventCastSpell()` | `:220-242`：先把地图对准英雄，再开魔法书，`ActionSpellCast` 后 `ResetFocus(HEROES, true)` |
| `EventEndTurn()` | `:244-261`：`HeroesMayStillMove` 时先问一句 |
| `EventAdventureDialog()` | `:263-285`：WORLD/PUZZLE/INFO/DIG 四分支 |
| `EventViewWorld()` / `EventFileDialog()` / `EventSystemDialog()` | `:287-300` |
| `EventNextTown()` | `:302-320`：有焦点城堡就取列表的下一个（末尾回卷），否则 `ResetFocus(CASTLE, false)` |
| `EventNewGame()` / `EventSaveGame()` / `EventLoadGame()` | `:322-359`：保存走「选文件名 → 文件已存在则问覆盖 → 存 → 报结果」的循环 |
| `EventPuzzleMaps()` / `EventScenarioInformation()` | `:361-396` |
| `EventSwitchHeroSleeping()` | `:398-408` |
| `EventDigArtifact()` | `:410-479`：船上/宝物袋满/移动力不足三种拒绝，`isSuitableForUltimateArtifact` 或「神器正踩在脚下且未挖出」才挖，`MusicRestorer` 包住 MIDI/非 MIDI 两种凯歌，成功后 `PickupArtifact` 并报名字，最后 `checkGameOver()` |
| `EventDefaultAction()` | `:481-506`：可重访对象就跑 `Heroes::Action`，动作留下 `HeroModes.Action` 时清掉并 `checkGameOver()`；无英雄时打开焦点城堡 |
| `EventOpenFocus()` | `:508-516` |
| `EventSwitchShowRadar/Buttons/Status/Icons/ControlPanel()` | `:518-590`：五个都只在 `IsHideInterface` 下起作用 |
| `EventKeyArrowPress()` | `:592-641`：有焦点英雄就交给方向键行进，否则八个方向的滚动组合（拖动滚动中或 `ScrollSpeed == None` 时不滚） |

新增契约（`UI/Interface/AdventureMapHost.cs`）：

* `AdventureEventResult`：事件会产出的结局（`Cancel`/`EndTurn`/`NewGame`/`LoadGame`/`QuitGame`/`Win`/`Lose`/`HighScore`），对应 `fheroes2::GameMode`（`game_mode.h:32-53`）。端口不引入全局 `GameMode` 枚举——`FileOptionsDialog` 早就用「每个对话框自己的结果枚举」表达过这个决定（ADR-163），这里沿用。
* `AdventureOption`：`Dialog::AdventureOptions()` 的返回值（CANCEL/WORLD/PUZZLE/INFO/DIG）。
* `IAdventureHost`：参考从事件里直接调的全局函数（`Dialog::AdventureOptions`、`Dialog::FileOptions`、`Dialog::GameInfo`、`Dialog::SelectFileSave`、`Game::OpenHeroesDialog`、`Game::OpenCastleDialog`、`Game::Save`、`Game::SelectCampaignScenario`、`Kingdom::openOverviewDialog`、`PuzzleMaps::ShowMapsDialog`、`ViewWorld::ViewWorldWindow`、`fheroes2::showSystemOptionsDialog`、`fheroes2::showStandardTextMessage`、`Heroes::OpenSpellBook`、`GameOver::Result::checkGameOver`）在这里是宿主方法。端口没有阻塞式消息框（消息框是调用方建好再喂事件的 `Dialogs.MessageBox`），所以 `ShowMessage` / `ShowYesNoMessage` 也在契约里。

偏差（已写进代码注释）：

* 事件把 `redraw(force)` 换成 `SetRedraw(mask)` 累积请求，绘制交给场景；`AdventureMap.RedrawInterface` 仍是 G5 的桩，事件里不能调它。
* `_buttonsPanel.setRedraw()` / `_radar.SetRedraw(REDRAW_RADAR)` 换成接口层的 `SetRedraw(Redraw.Buttons)` / `SetRedraw(Redraw.Radar)`——这两个面板还没注入到 `AdventureMap`（按钮条随 G5 的输入处理一起进来）。
* `hero->Action(idx)` 在端口没有英雄级入口，写成 `new HeroActions(hero, world.Kingdoms).Action(idx, world)`，并注明 `HeroActions` 目前还没有别的调用方。
* `isAction()`/`ResetAction()` 用端口已有的 `HeroModes.Action` 判定与清除。
* `GetMaxMovePoints()` 用端口同名的无参重载（陆地分支），`M82::DIGSOUND` → `M82Sound.Digsound`，`MUS::ULTIMATE_ARTIFACT` → `MusicTrack.UltimateArtifact`，`System::IsFile()` → `File.Exists()`。
* 战役分支（`Game::SelectCampaignScenario()`）未移植，`EventScenarioInformation()` 只做非战役部分并返回 `Cancel`，注释里写明这条分支暂时无法请求别的模式。
* 消息文本沿用参考英文原文（端口还没有翻译层）。

顺带补上的英雄层缺口：`Hero.CalculatePath(int destinationIndex, WorldMap world)`（`calculatePath(dst)` 重载；端口原先只有无参版，且 `HeroPath` 不存目的地，靠最后一步推导）与 `Hero.ResetMovePoints()`。

**仍然抛 `NotImplementedException`**：`AdventureMap.RedrawInterface` / `Reset` 与四个鼠标/光标处理（G5）；
`IAdventureHost` 尚无实现方——它是契约，场景在 G5 实现。

构建 0 错误 0 警告，全量 **4 945 绿**。

## 2026-11-22 G5 前半：`game_interface.cpp` 的 `reset()`/`redraw()` 与光标

`game/game_interface.cpp` 的三个函数 + `game/game_startgame.cpp` 的光标选择落地。

| 已实现 | 参考位置 |
| --- | --- |
| `AdventureMap.RedrawInterface( Redraw )` | `game_interface.cpp:130-173`：锁定时只 `SetRedraw(force)`；否则 `combined = GetRedrawMask() \| force`，按 GAMEAREA → RADAR → ICONS → BUTTONS → STATUS → BORDER 顺序绘制，末尾 `ClearRedraw()`。`REDRAW_GAMEAREA` 走 `GameArea.Redraw( All )` 加控制面板重绘，`REDRAW_RADAR` 走 `Radar.RedrawRadar( isRedrawCursor, humanColors )` |
| `AdventureMap.Reset()` | `game_interface.cpp:63-128`：先按 `Settings.IsHideInterface` 把四个面板的 `HideInterfaceEnabled` 对齐；隐藏模式用 `PosRadar/PosButtons/PosIcons/PosStatus`，四个都有效才照搬（`isPosValid()` → 坐标非负），否则从左上角起依次堆叠；普通模式按 `GameBorder.BorderWidth/RadarWidth` 排右边栏。最后 `GameArea.Generate()` 并把视野中心按新旧 ROI 差值平移 |
| `AdventureMap.RedrawLocker` | `game_interface.h:64-88`：RAII 锁，锁内只收集请求 |
| `GetCursorFocusCastle` / `GetCursorFocusShipmaster` / `GetCursorFocusHeroes` / `GetCursorNoFocus` / `GetCursorTileIndex` / `UpdateCursor` | `game_startgame.cpp:477-730`：城堡（自城/友城/敌城/带城门的空地）、船坞（有船/无船）与英雄（可走到/可攻击/可登船/下船登岸/港湾/中立或敌对的己方可交互对象）四类分派，最后 `CursorState.Current?.SetThemes(...)` |
| `World.IsCaptureObjectProtected( int )` | `maps_tiles_helper.cpp:1836-1856`：可占领对象且自带守卫才算受保护；城堡走 `GetCastleEntrance(tile.GetCenter())` 再问守军 |
| `IconsPanel.RedrawIcons( HeroesCastlesIcons )` | `interface_icons.cpp:422-447`：只重绘英雄/城堡图标行，边框另画 |
| `ButtonsPanel.SavePosition()` | `interface_buttons.cpp` 的位置存档，改为写 `Settings.SetPosButtons` |
| `Settings.PosRadar/PosButtons/PosIcons/PosStatus` | `settings.h` 的 `pos_*` 与 `settings.cpp` 里 `Config::PointParams( …, -1, -1 )` 的默认值；四个都初始化为 `(-1, -1)`，`reset()` 据此判断「从没挪过」 |

偏差（已写进代码注释）：

* 参考的 `updateCursor( tileIndex, isKeyboardCursor )` 与 `GetCursorFocusHeroes( hero, tile, isQuickSelectHeld )` 都会自己读热键；端口把「是否按住快速选择键」作为参数传进来（本文件 `UpdateCursor(int, bool)`），`BaseInterface.UpdateCursor(int)` 是不带该参数的老接口，`AdventureMap` 用 `UpdateCursor(tileIndex, false)` 转接。
* 端口把光标结果做成返回值（`GetCursorTileIndex` 返回 `CursorTheme`），`UpdateCursor` 只负责写 `CursorState`——这样光标选择逻辑不必依赖渲染就能测。
* `tile.getPassable()` 的 `== 0` 判断写成 `PassableDirections == Direction.Unknown`（端口的「无方向」值）。
* 参考 `MP2::OBJ_CASTLE` 带动作位（端口叫 `ActionCastle`），而端口的加载器把城门存成 `Mp2ObjectType.Castle`；`IsCaptureObjectProtected` 两种拼写都接受，注释里写明。
* `_buttonsPanel` / `_controlPanel` / 边框贴图在端口都是可空注入（无游戏数据时场景可以先不建），`Reset()` / `RedrawInterface()` 全程判空。
* `Settings` 只保内存：`SavePosition()` 写进 `Settings`，不落 `.fh2config`（配置写入器还没移植）。

测试：`AdventureMapTests`（6 个：两种布局下的面板排列、浮动位置取用、`RedrawLocker` 只收集不绘制、空光标状态不炸、地图外索引给指针）与 `WorldMapScanTests` 新增的 5 个 `IsCaptureObjectProtected` 用例（可占领但有/无守卫、守卫在别的格子、城堡地块无城）。

构建 0 错误 0 警告，全量 **4 956 绿**。

## 2026-11-22 G5 中段：`game_startgame.cpp` 的三个鼠标处理

落在 `UI/Interface/AdventureMap.Mouse.cs`（`game_startgame.cpp:1615-1736`）。

| 已实现 | 参考位置 |
| --- | --- |
| `AdventureMap.MouseCursorAreaClickLeft( int )` | `game_startgame.cpp:1615-1682`：按 `Cursor::WithoutDistanceThemes( Cursor::Get().Themes() )` 分派——`HEROES` 换焦点/开英雄对话框、`CASTLE` 换焦点/开城堡对话框、七种 `CURSOR_HERO_*` 走 `ShowPathOrStartMoveHero()` |
| `AdventureMap.MouseCursorAreaPressRight( int )` | `game_startgame.cpp:1684-1730`：雾格直接 `Dialog::QuickInfo( tile )`，否则按 `getMainObjectType()` 给城堡 / 英雄 / 地块三选一 |
| `AdventureMap.MouseCursorAreaLongPressLeft( int )` | `game_startgame.cpp:1732-1736`：转 `EventSwitchFocusedHero()`（G4 已落） |
| `IAdventureHost.ShowQuickInfo( Tile / Castle / Hero )` | `Dialog::QuickInfo()` 的三个重载；参考里是阻塞窗口，端口交给场景 |

偏差（已写进代码注释）：

* 三个处理都先判 `GetGeometry().IsValidAbsIndex(tileIndex)`：参考由事件循环保证索引有效，端口由场景给索引，越界指直接返回（否则 `world.getTile()` 会读到别的格子或抛异常）。
* `!IS_DEVEL() && tile.isFog(...)` 的右键盘条件只留雾判：端口没有开发版开关。
* `assert( focusedHero == nullptr || !focusedHero->Modes( ENABLEMOVE ) )` 是调试断言，端口不抄。
* `Dialog::QuickInfo()` 三个重载在端口是同一名字的三份签名（`ShowQuickInfo(Tile)` / `(Castle)` / `(Hero)`），与参考一一对应。

测试：`AdventureMapTests` 增至 10 个（新增：无光标主题时左键不动作、越界右键不弹速览、空地右键弹地块速览且拿到的是同一 `Tile` 实例、长按无英雄时焦点不变）。

**仍然抛 `NotImplementedException`**：无——`AdventureMap` 的 `BaseInterface` 抽象成员已全部有实体实现。
`IAdventureHost` 仍无实现方：它是场景契约，`AdventureMapScene` 收口时实现。

构建 0 错误 0 警告，全量 **4 960 绿**。

## 2026-11-22 G5 收口第一步：界面自建面板（`game_interface.cpp:38-61`）

`AdventureMap` 不再由外部塞入预建面板，改成参考的「界面自己造面板」：`public AdventureMap(...)` 收成私有构造，
新增 `public static AdventureMap? TryCreate( PixelBuffer, AssetBundle?, AudioManager, IAdventureHost )`。

| 已实现 | 参考位置 |
| --- | --- |
| `AdventureMap.TryCreate( ... )` | `game_interface.cpp:38-61` 的构造函数：读 `isEvilInterfaceEnabled()` 选 `ADVBTNS`/`ADVEBTNS`，造 `IconsPanel` / `StatusPanel` / `ButtonsPanel` / `ControlPanel`，`MapFrame.Load()` 取 `ADVBORD.ICN` 边框贴图 |
| `AdventureMap.CreateButtonsPanel( ... )` | `gui/interface_buttons.cpp:40-56`：`ADVBTNS` 前 16 帧按 8 个按钮的「释放/按下」成对切开，任一帧缺失就整条不建（返回 `null`） |
| `AdventureMap.CreateControlPanel( ... )` | `gui/interface_cpanel.cpp:38-58` 的 `ResetTheme()`：帧 4/0/12/10/8 拼成隐藏界面模式的五格横条 |
| `AdventureMap.ControlPanelAreaForInput` | `BaseInterface` 的钩子，返回 `_controlPanel.Area`，供 `GameArea` 吞掉落在横条上的点击 |

要点：

* 面板必须在 `this` 之后才能造（它们的重绘请求与焦点都走这个界面对象），C# 无法把 `this` 传进自己的构造参数，
  所以由静态工厂在 `new` 之后填字段；字段因此不再是 `readonly`，注释里写明了原因。
* 工厂在 `assets == null` 或缺 `MONS32.ICN` 的怪物头像时返回 `null`：状态栏没法画军队，整屏不建。参考在这种情况下会
  直接崩，端口把「没有游戏数据」当成正常情况。
* 边框贴图只加载一次：`GameBorder` 与 `ControlPanel` 用的是同一张 `ADVBORD.ICN` 帧（参考每次重绘都 `AGG::GetICN()`）。
* `IcnSpriteCache` 在端口是 `Game` 的内部类型，`UI` 侧取图统一走 `IcnViewWorldSpriteSource.GetSprite( IcnType, int )`，
  顺带把字符串文件名换成了枚举。

测试：`AdventureMapTests` 增至 13 个（新增：工厂把五个面板都造出来、无游戏数据时返回 `null`、
`ControlPanelAreaForInput` 就是隐藏界面横条的 `Area` 本身）；测试里原来的 `StubInterface` / 桩精灵 / 桩怪物集随预建面板一起删掉。

**G5 收口剩余**（`AdventureMapScene` 接管）：

1. `MapSprites.Initialize( assets, KB.PAL )` 在打开冒险地图时调用——端口的地图区渲染目前完全没跑起来过；
2. App 侧 `IAdventureHost` 实现：消息框、英雄/城堡对话框、存档/读档、世界地图可接现有 overlay，但英雄对话框
   （`Game::OpenHeroesDialog`）、法术书、王国总览、拼图都还没移植，是接管的前置；
3. 接管后删自建 `AdventureMapView` / `IconBar` / `Minimap` / `MapFrame` / `StatusPanelPainter` 与 140 处测试引用。

构建 0 错误 0 警告，全量 **4 963 绿**。

## 2026-11-22 G5 收口第二步：`MapSprites` 接线 + 整屏绘制跑通

**接线**（`src/FHeroes2.App/GameHost.cs` 的 `LoadGameData()`）：在 `GameAssets.Current` 与 `KB.PAL` 就位之后调
`MapSprites.Initialize( _assets, _palette )`——这就是 `MapSprites` 文档里说的「组合根装一次」。`KB.PAL` 缺失时仍然装
`MapSprites.Initialize( _assets, null )`：地形精灵是不透明贴图，没有调色板也能画，只有 alpha 混合的对象会显式抛
（`GameArea.BlitOnTile`）。

**验证**（`tests/FHeroes2.UI.Tests/AdventureMapTests.cs`，新增 2 个用例，共 15 个）：

* `RedrawInterface_WithTheGameData_PaintsTheWholeScreen`：经典布局下按 `StartGame()` 的前置
  （`game_startgame.cpp:740-750`：`Radar.Build()` / `SetHide(true)` / `HideIcons(Any)` / `StatusPanel.Reset()`）准备好，
  一次 `RedrawInterface( Redraw.All )` 画出边框、雷达、地图区、图标条、按钮条、状态栏，断言不是空白帧。
* `RedrawInterface_WithTheHideInterfaceMode_PaintsTheFloatingPanelsOverTheMap`：隐藏界面模式，雷达不折叠（覆盖生成地图
  图像的分支）、四个浮动面板常驻绘制、`ControlPanel` 走 `GetGamePalette()` 的 alpha 混合。

这两个用例第一次证明「移植版冒险地图整屏能画出来」，也钉住了两条此前没人走过的依赖：`MapSprites` 必须先装
（否则 `GameArea` 取不到地形图），`Radar.Build()` 必须先跑（否则 `Radar.RedrawRadar` 抛）。

**G5 收口剩余**：只剩场景接管（见上一节的三步，第 1 步已完成）。

构建 0 错误 0 警告，全量 **4 965 绿**。

## 2026-11-22 G5 前置第一步：拼图窗口（`kingdom/puzzle.cpp:85-220`）

新增 `src/FHeroes2.UI/Dialogs/PuzzleMapDialog.cs`，把 `drawPuzzle()`、`revealPuzzle()` 与 `ShowStandardDialog()`
的绘制部分转出来。`kingdom/puzzle.cpp` 里剩下的 `ShowExtendedDialog()`（可移动窗口版）尚未转。

| 已实现 | 参考位置 |
| --- | --- |
| `PuzzleMapDialog.RenderRadarCover()` | `copyToRadar()`：把整张小图 `Copy( VIEWPUZL/EVIWPUZL, radarArea )` 盖在雷达上 |
| `PuzzleMapDialog.RenderExitButton()` | `addGradientShadow( { -5, 5 } )` + `draw()`/`drawOnState()`，按钮水平居中、距雷达底边 37 px |
| `PuzzleMapDialog.RenderPicture()` / `BlitUncoveredPiece()` | `drawPuzzle()` 与 `revealPuzzle()` 的贴图循环：已揭开的块按 `alpha` 混合，未揭开的块不透明贴 |
| `PuzzleMapDialog.AdvanceReveal()` / `RestartReveal()` | `revealPuzzle()` 的动画节拍：250 起、每拍 `-10`、到 0 结束 |
| `PuzzleMapDialog.IsExitButton()` | `le.MouseClickLeft( buttonExit.area() )` |

要点：

* 块的落点是 `picturePosition + piece.Offset - BorderWidth`，照抄参考的 `dstx + piece.x() - borderWidthPx`：
  `PUZZLE.ICN` 的帧是按 640×480 整屏切的，边框已经烘进帧里。
* 参考是阻塞式模态循环，端口是一帧一画：`Render()` 画雷达盖图 + 退出按钮 + 拼图，动画由调用方按节拍调
  `AdvanceReveal()`。`RevealAlpha` 默认 0（等于动画结束态），255 退化成不透明贴图。
* `Render()` 每帧先重画雷达盖图再画按钮阴影：阴影是 transform 层，重复叠加会把按钮越涂越黑，重画盖图正好把
  那一块复位（`ShowExtendedDialog()` 的 `drawControlPanel()` 也是这么做的）。
* 混合需要调色板：没给 `Palette` 又要混合已揭开的块时抛 `InvalidOperationException`，不猜颜色。
* `UltimateArtifact.GetPuzzleMapSurface()` 仍是占位，但注释改成「层级偏差」：图的拼装在 `ui` 侧
  （`GameArea.GenerateUltimateArtifactAreaSurface()` 已转），`Game` 不能反向依赖 `UI`，不要在游戏逻辑里调它。

测试：新增 `tests/FHeroes2.UI.Tests/PuzzleMapDialogTests.cs` 11 个用例（按钮居中与命中、雷达盖图、按下态、未揭开
时块盖住图、全揭开时只剩图、遮盖混合缺调色板抛、给了调色板能混、动画 250→0 且可重复调用、三个构造参数校验）。

**仍未接上**：`AdventureMap.Events.EventPuzzleMaps()` → `IAdventureHost.OpenPuzzleMaps()` 在 App 侧还没有实现，
`ShowExtendedDialog()`（可移动窗口版）未转。

构建 0 错误 0 警告，全量 **4 976 绿**。

## 2026-11-22 技能条（`gui/skill_bar.cpp`）

新增 `src/FHeroes2.UI/Widgets/SkillBars.cs`：`ISkillBarSprites`（美术来源接口）、`SkillBarArtwork.CreateMiniPlate()`
与两条技能条 `PrimarySkillsBar` / `SecondarySkillsBar`。这是王国总览与英雄对话框的共同前置——两处都要摆 34×34 的
迷你条或 73×70/75×75 的整块条。

| 已实现 | 参考位置 |
| --- | --- |
| `SkillBarArtwork.CreateMiniPlate()` | `GetBarBackgroundSprite()`：34×34 框 + `HSICONS` 第 0 帧 (26,21) 的 32×32 |
| `PrimarySkillsBar` 迷你形 | 34×34 槽 + `SWAPWIN` 第 0 帧 (217, 52+33i) 裁出的 32×32 图标 + 底部数值（`SetTextOff` 偏移） |
| `PrimarySkillsBar` 整块形 | 73×70 `PRIMSKIL` 板 + 顶部技能名 + 底部数值 |
| `SecondarySkillsBar` 迷你形 | 34×34 槽 + `MINISS` 图标 + 右下角等级数 |
| `SecondarySkillsBar` 整块形 | `SECSKILL` 空板 + `SECSKILL` 技能板 + 名称与等级（超宽时 `FitToOneRow`） |
| `PrimarySkillsBar.DrawSkillNames()` | `fheroes2::RedrawPrimarySkillInfo()`：x+320 居中、y+62 起、每行 33 px |

要点：

* `ItemsBar::Redraw`（`interface_itemsbar.h:105-139`）是**逐格**画背景再画物品，且表尺寸为 0 时整条不画。端口照抄：
  两条技能条的 `Redraw()` 先判 `Layout.IsEmpty`，再逐格 `RedrawBackground` + `RedrawItem`（空槽保留底板）。
* `fheroes2::Blit` 的 4 参数重载（`BlitSprite`）用的是 **sprite 局部坐标**，目标偏移要减掉源矩形的原点；`fheroes2::Copy`
  对应的 `ImageOps.Blit` 则是「目标点 + 源矩形」直接落位。迷你图标那处裁剪两种语义差别明显，已按前者补偿。
* 参考里 `Skill::Level::StringWithBonus( _hero, skill )` 会去读世界算招魂加成；端口把加成做成 `NecromancyBonus` 参数，
  由知道王国的界面传入，控件本身不碰 `World`。

**本轮未做（已在类注释里写明理由）**：

* 输入（点击弹说明、编辑器里改技能等级/清空）——端口控件只负责画，点击由持有它的界面从同一个 `Layout` 驱动。
* 编辑器分支（`isEditMode` / `allowSkillReset`、变暗的「Default skill」板、`Dialog::SelectCount`）——按总体原则
  「editor 最后」，等界面接管时再补。

测试：新增 `tests/FHeroes2.UI.Tests/SkillBarsTests.cs` 11 个用例（槽尺寸来源、缺美术抛、无表尺寸不画、迷你/整块像素、
`SWAPWIN` 裁剪落点、未知技能抛、空槽不画、8 格底板、整块底板、缺板抛）。

**下一步**：`ArtifactsBar`（`resource/artifact.h` 里的 `ArtifactsBar`）——王国总览三件套的最后一块前置；之后才能整块转
`kingdom/kingdom_overview.cpp`（959 行）。

构建 0 错误 0 警告，全量 **4 987 绿**。

## 2026-11-22 宝物条（`resource/artifact.cpp:1104-1183`）

新增 `src/FHeroes2.UI/Widgets/ArtifactsBar.cs` 与 `IArtifactsBarSprites`。这是王国总览最后一个前置控件——总览给每个
英雄都建一条 `ArtifactsBar`（`kingdom_overview.cpp:152-154`：`hero, true, false, false, true, nullptr`，表 7×2、格间距
`{1,8}`）。

| 已实现 | 参考位置 |
| --- | --- |
| `IArtifactsBarSprites`：`MiniPlate` / `EmptySlot` / `GetMiniArtifact` / `GetArtifact` | `Assets::getImage( HSICONS/ARTIFACT/ARTFX )` |
| 迷你形：34×34 金框 + `ARTFX` 32×32 右上角内缩 1 px，先 `Fill` 再贴 | `artifact.cpp:1112-1127`、`1163-1167` |
| 整块形：`ARTIFACT` 64×64 空槽 + 同尺寸图标 | `artifact.cpp:1128-1137`、`1168-1172` |
| 选中框：迷你 34×34 单线 214；整块 70×70 三层 190/180/190，外挂 3 px | `artifact.cpp:1124-1136`、`1174-1181` |
| `Redraw` / `RedrawBackground` / `RedrawItem` / `ResetSelected` / `Select` | `1140-1183` |

要点：

* **光标精灵的透明**：参考用 `spcursor.reset()` 让光标除边框外全透明。端口的 transform 层里 `0` = 照抄、`1` = 跳过，
  而 `Sprite(w,h)` 默认全是 `0`（整块不透明）。所以光标必须 `Reset()` + 用 `ImageOps.DrawLine(Sprite, …)` 画四条边——
  该重载画的同时把 transform 清成 `0`，`PixelBuffer` 版的原语做不到（`ArmyBar.CreateCursor` 同法）。
* `Artifact::IndexSprite32()` / `IndexSprite64()` 在参考里是 `Artifact` 的成员；端口没有 `Artifact` 类（用 `ArtifactType`
  枚举 + `ArtifactBag`），两个索引函数放进 `ArtifactTypes`：`MiniSpriteIndex()`（空槽返回参考的 255）与 `FullSpriteIndex()`。
* 参考的 `StatusBar*` 只用来印「不能交易魔法书」，纯画图的控件不需要；输入（点开说明、互换两件宝物、开魔法书、编辑器
  里选宝物/选卷轴法术）全部依赖尚不存在的对话框，按既有做法留给界面，`Select()` 是它的接缝。

测试：`tests/FHeroes2.UI.Tests/ArtifactsBarTests.cs` 11 个用例（两种尺寸来源、缺美术抛、无表尺寸不画、迷你/整块像素、
选中框颜色与位置、空槽不画、`Select` 越界抛）；`ArtifactTests` 加 1 个用例覆盖两个图标索引。

**下一步**：前置齐了（`ArmyBar` / `ArtifactsBar` / `SkillBars` / `PuzzleMapDialog` / `ListBox` / `ScrollBar`），可以整块转
`kingdom/kingdom_overview.cpp`（959 行）。

构建 0 错误 0 警告，全量 **4 999 绿**。

## 2026-11-22 王国总览对话框（`kingdom/kingdom_overview.cpp`）

整篇 959 行转完：两个列表（英雄 / 城堡）、它们的行、收入与资金两条底栏，以及 `Kingdom::openOverviewDialog()`
的布局与事件处理。参考的模态 `while ( le.HandleEvents() )` 照例拆成「`ProcessEvents` 回答要不要重画 + `Draw` 画一帧」。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/KingdomOverviewArtwork.cs` | `Assets::getImage()` 调用点 | `OVERVIEW` 6/7/8/10/11/13/14、`OVERBACK` 0、`SCROLL` 0-4、`CSLMARKER` 0、`BUTTON_KINGDOM_*` 0/1，加大头像/技能/神器/部队/住所条所需的贴图源 |
| `UI/Dialogs/KingdomOverviewLists.cs` | `redrawCommonBackground()`（`:95-120`）、`StatsHeroesList`（`:169-394`）、`StatsCastlesList`（`:448-685`） | 滚动条装配、行板的四行循环与两条黄线、表头三行标题、`Refresh()`、`updateHeroArmyBars()`；几何常量收进 `KingdomOverviewLayout` |
| `UI/Dialogs/KingdomHeroRow.cs` | 两个列表的 `RedrawItem` / `RedrawBackground` | 英雄行（头像 + 四项主技能右对齐 + 两支部队条 + 神器）与城堡行（城名/驻军/来访英雄/周产量/住所条/指挥官头像） |
| `UI/Dialogs/KingdomOverviewRequests.cs` | 循环里打开的各个子对话框 | 请求袋：英雄/城镇对话框、两个快速信息、指挥官信息、收益、灯塔、标准消息、部队交互、`needFadeIn` |
| `UI/Dialogs/KingdomOverviewWindow.cs` | `openOverviewDialog()`（`:763-959`） | 640×480 窗口、三枚按钮（540/361、540/406、540/454）、`(1,360,535,60)` 收益区、`(124,459,289,16)` 金币区、随精灵宽的灯塔区、`RedrawIncomeInfo()`、`RedrawFundsInfo()`、`CapturedExtInfoString()` |

要点：

* **按钮状态就是列表状态**：`:810-823` 用 `press()`/`release()` 把当前列表钉在按钮上，端口每帧在 `Draw` 里走
  `DrawOnPress`/`DrawOnRelease`；点击判定用 `!IsPressed`，点已按下的那枚不会重复切换。
* **列表的点击也走请求袋**：`ActionListSingleClick`/`ActionListPressRight`（头像格 30×30）与 `ActionListCursor`（部队条）
  只填 `Requests`，由场景打开对话框；`MergeRequests()` 把列表那份并进窗口这份。
* **子窗口立刻淡入**：`needFadeIn`（`:285`、`:555`）在端口翻成「这一帧必须重画」，真正的淡入由场景做。
* **资金面板每帧重画**：参考只在城堡模式下重画（`:925-928`，资金只可能在城里变），端口每帧从 `OVERBACK` 重贴整窗，
  否则会把面板擦掉；数值与位置不变。
* **右键帮助变成 `DialogPopup`**：`:850-858` 的三句标准消息进请求袋，与其它窗口一致。
* **热键走参数**：`KingdomOverviewHotKeys.CloseWindow`（Escape 或 Enter）由场景解码后传入。
* **滚动位置存在王国上**：构造时读 `TopHeroInKingdomView`/`TopCastleInKingdomView`，`Close()` 写回（`:942-943`）。
* **冒险地图重画只报自己那一位**：英雄列表重建时置 `WorldMapRedrawMask |= Redraw.Heroes`（`:913-919`）；
  `getRedrawMask()`、`ResetFocus()`、`RedrawFocus()`、`setFastScrollStatus( false )` 与淡出属于场景，写在 `Close()` 注释里。

测试：本轮只求转完，行为测试按计划留到逐函数核对那一轮。

构建 0 错误 0 警告，`tests/FHeroes2.UI.Tests` **1 191 通过 / 0 失败**。

**遗留**：App 侧入口（`AdventureMapHost.OpenKingdomOverview()` 仍只有接口，`GameHost`/`AdventureMapScene` 没有模态覆盖层）；
`SecondarySkillsBar`/`ArtifactsBar` 的交互仍只读；行文本与两条底栏的逐像素对拍留到核对那一轮。
（`Interface::redrawHeroesIcon` 不算缺：端口由 `HeroPortraitPainter.DrawSmall` + `IconsPanel` 承担。）

## 2026-11-22 灯塔信息框与收入说明（`gui/ui_kingdom.cpp`）

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/LighthouseInfo.cs`（新） | `showLighthouseInfo()`（`ui_kingdom.cpp:51-90`） | 六个 `OBJNMUL2` 部件（阴影 60/71/72、塔身 59/61/73）按偏移拼成 160×(64+塔顶高) 的整图，灯室位置取 62 号帧的偏移；动画取 `agg/icn.cpp:549-553` 的 62-70 九帧；标题是复数 `StringObject( OBJ_LIGHTHOUSE, 2 )` |
| `UI/Dialogs/KingdomIncome.cs`（改） | `ResourceDialogElement::showPopup()`（`ui_dialog.cpp:451-454`） | 右键图标补 `ResourceText.Description`，不再只给资源名 |
| `UI/Dialogs/StandardWindow.cs`（改） | `RenderScrollbarBackground()`（`ui_window.cpp:400-431`） | 16 px 宽的列表凹槽（`ADVBORD.ICN` 0 号帧 536,176 帽 + 536,196 中段重复 + 536,285 脚）加左缘与脚下四步阴影，后面所有带滚动条的列表窗口都要用 |

要点：只做 `Dialog::ZERO` 那一版——谁来建、什么时候销毁都在场景手里；帧间隔默认 250 ms（`MAPS_DELAY` 初值），
要跟随游戏速度时由调用方传 `GameDelays.GetAnimationDelayValue( DelayType.MapsDelay )`；端口的 `Mp2ObjectName`
只出单数，复数标题因此单列常量。

测试：`LighthouseInfoTests` 5 条（帧数不足、三个空参、非正帧间隔、元素构成与 160×96、可替换文案；需要位图字体与消息框
边框的用例走 `GlobalArchiveCollection`）、`StandardWindowScrollbarTests` 3 条（三段拼接、阴影范围、空参）。

构建 0 错误 0 警告，`tests/FHeroes2.UI.Tests` **1 199 通过 / 0 失败**。

## 2026-11-22 S13 前置：物件选择窗口与选法术（`dialog/dialog_selectitems.cpp`）

`ItemSelectionWindow` 是后面法术书、宝物、怪物、技能等一系列「列表选一个东西」窗口的共同底座，本轮先按参考逐段转完，
再挂第一个子类 `SelectEnumSpell`。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/ItemSelectionWindow.cs` | `ItemSelectionWindow`（`dialog_selectitems.h:61-193`、`.cpp:889-1081`、`1013-1057`） | `ListBox<int32_t>` 的端口（行是 `ItemSelectionEntry`）+ `StandardWindow`、标题/描述、列表区、凹槽、滚动按钮、OK/CANCEL；`Draw` 画一帧，`ProcessEvents` 吃一帧 |
| `UI/Dialogs/ItemSelectionWindowArtwork.cs` | 构造里的 `AGG::GetICN()` | `SCROLL`/`SCROLLE` 0-4、`ADVBORD` 0、`BUTTON_SMALL_OKAY_GOOD` 与 `BUTTON_SMALL_CANCEL_GOOD` 0/1 |
| `UI/Dialogs/SpellSelectionWindow.cs` | `SelectEnumSpell`（`.h:366-381`、`.cpp:379-421`） | 行高 55、图标偏移 37、文字偏移 80、名字按 `activeArea.width - 80 - 55` 截断、右键弹法术说明；`SelectSpell()` 对应 `SetCurrent( spellId )` |
| `UI/Dialogs/SpellSelectDialog.cs`（重写） | `Dialog::selectSpell()`（`.cpp:1116-1160`） | `DialogHeightDeduction = 150`、`DialogSize()`、`BuildSpellList()`（随机项插最前）、`TryCreate()`（空表返回 `null`，由场景显示提示） |

要点：

* 几何逐项对齐参考：列表区 `(activeArea.x + 10, activeArea.y + 30 + listOffsetY, width - 40, height - 70 - listOffsetY)`，
  无描述时 `listOffsetY = 0`、有描述时是描述行高 + 3；行区再内缩 5；凹槽 `(activeArea.x + width - 25, listRoi.y, 16, listRoi.height)`；
  轨道 `(凹槽 x + 3, listRoi.y + 19, 10, listRoi.height - 38)`；上下按各在 `+1` 与 `-15`；OK/CANCEL 距左下/右下 `(20, 7)`。
* **循环拆成两半**：参考在 `selectItemsEventProcessing()` 里自己画自己读；端口 `Draw` 负责石框、两段文字、列表（含阴影与凹槽）
  与两枚按钮，`ProcessEvents` 负责按钮/热键、右键帮助、列表点击与双击，关闭结果落在 `Result`（`DialogButton.None` 表示还开着）。
  一帧只回答一件事，所以「右键弹帮助后同帧继续处理列表」在端口变成下一帧。
* **凹槽位置单独存一份**：`RenderScrollbarBackground()` 的矩形决定落点，不能把整个列表区传进去（会画到列表左边），
  所以 `GrooveArea` 独立于 `ListArea`。
* **双击等于 OK**：`ActionListDoubleClick` 只置标志，`ProcessEvents` 在这一帧把 `Result` 置为 OK；单键、上下键在基类里什么都不做。
* **不转 toggle 按钮**：`_buttonToggleOn/Off` 与 `enableToggleButtons()` 只有未转的 `MultiArtifactSelection` 用，虚接口按参考保留。

测试：新增 `ItemSelectionWindowTests` 16 条（窗口尺寸、列表合法性、随机项位置、等级与排除、空表返回 `null`、
列表区/行区/轨道/按钮几何、初始选中、双击回 OK、取消回 CANCEL、右键弹说明、凹槽像素落点、空参）。

**下一步**：法术书一族（见下一节）；App 侧还没有把 `SpellSelectDialog.TryCreate()` 接到场景。

构建 0 错误 0 警告，全量 **5 023 绿**（Core 283 + Assets 416 + Game 3 109 + UI 1 215）。

## 2026-11-22 S2 界面篇收口：法术书窗口（`spell/spell_book.cpp`）

S2 章节里记的 `UI/Dialogs/SpellBookView.cs` 骨架本轮被实体实现取代并删除。`SpellBook::Open()`（`:194-403`）与
`SpellBook::Edit()`（`:405-505`）合成一个双模式窗口。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/SpellBookArtwork.cs`（新） | `getSpellBookSize()`（`:61-84`）、`:225-232` 的 `getImage()` | `ICN::BOOK` 页 0、冒险签 3、战斗签 4、关书签 5、法力签 6、直角 7，加 `ICN::SPELLS` 全帧；`GetBookSize( Filter )` 取「画得出的书签里最低的底边」 |
| `UI/Dialogs/SpellBookRenderer.cs`（新） | `SpellBookRedrawSpells()`（`:86-122`）、`SpellBookRedrawManaPoints()`（`:124-141`）、`spellBookRedrawLists()`（`:143-192`） | 双栏 12 行（白/灰小字、名字 + `[消耗]`、宽 80 换行）、法力签三位数、页与四枚书签、直角拼角、`addShadow` |
| `UI/Dialogs/SpellBookWindow.cs`（新，约 740 行） | `SpellBook::Open()`、`SpellBook::Edit()` | `TryOpen()` / `TryEdit()` 两个工厂、`Draw` + `ProcessEvents` + `SpellBookFrame`、命中区与状态栏、编辑模式增删 |
| `Game/Spells/SpellBook.cs`（改） | `:200-221` | 拆出 `BeginSession( filter, restore )` 与 `ClampStartIndex( count )`，原三参重载转调二者 |
| `Game/Heroes/ISpellBookOwner.cs`（改） | `heroes_base.h` | 增 `SpellPoints`、`GetMaxSpellPoints()`、`CanCastSpell( spell )`、`BagArtifacts` |
| `App/Fonts/GameFonts.cs`（改） | `FontType::smallWhite()` | 新增 `SmallWhite`（`SMALFONT` + `PaletteId.WhiteFont`），灰色行仍用 `Small` |
| `UI/Dialogs/SpellBookView.cs`（删） | — | 占位类，被 `SpellBookWindow` 取代 |

关键点（细节见 `docs/PROGRESS.md` 的「法术书窗口」一节）：

* `Editing => _book is null` 区分两模式；施法书的页与过滤器存在传进来的 `SpellBook` 里，编辑书用私有 `_pageIndex` 且关闭不回写。
* `Filter::ALL` 无条件把过滤器打回冒险、页码归零（`:203-206`），`restorePreviousState` 只对单一标签的会话有效。
* 编辑模式点空位返回 `SpellBookAction.ChooseSpellToAdd`，由调用方用 `ExcludedSpells` 开 `SpellSelectDialog`；右键返回 `RemoveSpell`。
* 书高看标签：`CloseBookmark` 永远计入，真实资源下三种过滤器都是 320；测试用 10 px 高的关书签逼出 `All`/`Combat` 316、`Adventure` 313。
* 右页内列比左页外列高 5 px（`extraOffsetY` 减在 `oy` 上，`:97-98`），照参考保留。
* 滚轮上滚=上一屏、下滚=下一屏；「上一页」提示只在非首页出现（`:270-275`、`:347-351`）。
* 两条给路返回 `null`，句子挂在 `NoMagicBookMessage` / `NoSpellToCastMessage` 上由场景显示，不在窗口里弹框。

**仍然缺**：App 侧无调用点（窗口目前只有测试在用）；法术格的右键说明仍是「名字 + `GetDescription()`」——本轮之后由下一节的
`SpellInfo.GetDescription` 补齐。

测试：新增 `SpellBookWindowTests` 23 条（给路、默认冒险页、页记忆与越界丢弃、居中与书签几何、12 格双栏坐标、首页/末屏格数、
点选与法力不足弹原因、只读点开、右键瞬态、滚轮翻页、标签切换与半本书、关书与法力签、状态栏三句、编辑模式四条、书高随标签、
`Draw` 像素、两种法力文案、空参）。

构建 0 错误 0 警告，全量 **5 046 绿**。

## 2026-11-22 S2 收尾：法术描述（`spell/spell_info.cpp:214-321`）

`getSpellDescription()` 此前只有「模板句子」的落点，本轮补上按施法者数值拼装的完整版本。

| 端口成员 | 参考 |
| --- | --- |
| `SpellInfo.GetDescription( spell, caster = null, world = null )` | `getSpellDescription()`（`:214-321`）：表句 + 按 `IsDamage/IsSummon/IsRestore/IsResurrect/IsGuardianType/TOWNGATE/HYPNOTIZE` 追加一段 |
| `SpellInfo.FillCount( description, count )`（私有） | `updateSpellDescription()`（`:50-64`）：`%{count}` 填 `extraValue`，1/2 写成 `one`/`two`，其余用数字，全部出现处替换 |
| `SpellInfo.SummonedMonster( type )`（私有） | `Monster::Monster( const Spell & )`（`monster.cpp:77-110`）：四元素召唤/守卫共用一个生物，Haunt 是鬼，其余 `Unknown` |
| `ISpellBookOwner.GetPower()` | `HeroBase::GetPower()`（`heroes_base.h:141`）：接口新增，`Hero` / `Captain` 早已有这个公开成员，直接满足 |

偏差（已写进代码注释）：

* 参考收到的是 `HeroBase *`，自己从英雄摸到王国、地图与城堡；端口签名是 `ISpellBookOwner` + 可空 `World`，只有分支需要时才读。
* **Town Gate 只写到城名**：参考在城名后还会说「城里的英雄是谁」，端口的世界还回答不了「某城里有哪个英雄」，句子到此为止；
  另外参考走船长书时（`caster` 不是英雄）不给城名，端口同样只在 `caster is Hero` 且给了 `world` 时才追加。
* 召唤/守卫法术在参考里对「没有对应生物」是 `assert`，端口返回纯表句（等于 release 构建的行为）。
* `one` 的分支没有测试：`spell_table.cpp` 里没有 `extraValue == 1` 的法术，`FillCount` 又是私有方法。

测试：`SpellInfoTests` 8 → 15 条（无施法者的表句与 `two`/数字两种填法、伤害与火系神器加成、召唤与召唤神器加倍、
治疗与复活（含 `Ankh`）、守卫与 `Haunt` 的鬼、`Hypnotize` 的控制上限、Town Gate 取最近城与「无 world 时不追加」）。

**下一步**：把描述接到界面——参考是在 `SpellDialogElement::showPopup()` 里调 `getSpellDescription( _spell, _hero )`
（`ui_dialog.cpp:545`），端口的 `SpellDialogElement` 还没有施法者参数，所以法术书与 `SpellSelectDialog` 的右键仍是
「名字 + 模板句子」。前置链上还差英雄对话框与王国总览。

构建 0 错误 0 警告，全量 **5 053 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 238）。

> 一次 UI 全量并行运行里 `SkillBarsTests.PrimaryFull_Redraw_PaintsThePlateOfEverySkill` 偶发失败，单跑与随后的全量
> 均通过，判为既有用例的并行时序脆弱，本轮未改该测试。

> 上节末尾「前置链上还差英雄对话框与王国总览」里，**王国总览已于同一天整篇转完**（见前面的「王国总览对话框」一节）。

## 2026-11-22 英雄指示器、怪物框与状态栏（`heroes_indicator.cpp` / `ui_monster.cpp` / `statusbar.cpp`）

A→J 的 H 行散件里三个独立的界面控件，外加一处口径纠正。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Widgets/HeroIndicators.cs` | `heroes/heroes_indicator.cpp:28-333`、`.h:41-139` | `HeroIndicator` 基类（背景恢复、`SetPos`、`RedrawOnlyBackground`）、`LuckIndicator`、`MoraleIndicator`（亡灵部队时灰化 + 变暗并追加说明）、`ExperienceIndicator`（`40-90` 默认值、超宽改 `M` 缩写）、`SpellPointsIndicator`（单行/双行与背景恢复）、`HeroIndicatorArtwork`（`HSICONS` 1/2/3/4/5/6/7/8 帧）、`HeroIndicatorHelp` |
| `UI/Widgets/MonsterFrameRenderer.cs` | `ui_monster.cpp:31-59` | `IMonsterFrameSprites`（`STRIP` 种族板、`ICNMonh` 帧 0）、`Render()`：板画在槽位偏移处，像画在偏移 + 自身源偏移处 |
| `UI/Widgets/StatusBar.cs` | `gui/statusbar.cpp`、`statusbar.h:31-52` | `SetRoi`、`ShowMessage`/`UpdateMessage`：`fitToOneRow( _roi.width )` 裁切、居中、`y + 2`、同一消息返回空区；`BoundaryRect` 用 min/max（与参考一致，不用会把空矩形当「无」的 `Rect.Union`） |
| `UI/UiConstants.cs` | `ui_constants.h:23-39` | `borderWidthPx` / `radarWidthPx` / `shadowWidthPx` / `boxAreaWidthPx` / `tileWidthPx` 收成单一来源；`GameBorder`、`Radar`、`FrameBox`、`FrameBorder`、`LighthouseInfo`、`StandardPopupWindow`、`ScenarioSelectionDialog` 的常量改成引用它，名字不变 |

要点：

* **`ui_base.cpp` 判定改为「不适用」**：全篇只有一个空的 `ActionObject::senderUpdate()`，端口没有观察者机制，没有可移植的代码。
* **`MovableText` 早已转完**（`UI/Widgets/MovableText.cs`）但它是 `sealed`，所以 `StatusBar` 用组合而不是继承；偏差写在类注释里。
* **`ArmyBar` 的内联怪物框换成调用**：`IArmyBarSprites` 现在继承 `IMonsterFrameSprites`，`ArmyBar.RedrawItem` 的 else 分支调 `MonsterFrameRenderer.Render()`，与参考 `ArmyBar` 调 `renderMonsterFrame()` 对齐；`ArmyBarTests` 20 条仍绿。
* **指示器只回消息不弹框**：`QueueEventProcessing` 返回 `HeroIndicatorHelp`，由窗口转成 `DialogPopup`，与其它控件一致。
* **测试**：`MonsterFrameRendererTests` 5 条（板、源偏移、遮挡、缺图、按种族取板）；指示器与 `StatusBar` 的像素级对拍依赖全局字体与真实 ICN，留到逐函数核对那一轮。

构建 0 错误 0 警告，全量 **5 058 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 243）。

**H 行剩余**：`player_info.cpp`(380)、`ui_campaign.cpp`(190)、`ui_mage_guild.cpp`(192)、`ui_map_interface.cpp`(①部分)、
`ui_castle.cpp`(①部分)、`interface_itemsbar.h`(②)、`ui_tool.cpp` 的 `UIScrollInertia`。
之后按 planv4 的顺序：英雄对话框（G5 的硬前置）→ 城堡对话框 → G5 收口 → I（`ui_font.cpp` 对拍）→ J。

## 2026-11-22 战役奖励弹窗（`gui/ui_campaign.cpp`）

整篇 218 行转完，外加它依赖的两个技能描述函数。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/CampaignPopupWindows.cs` | `ui_campaign.cpp:52-217` | `ICampaignPopupArtwork`（取图交给宿主）、`ShowScenarioBonus()`（7 种奖励）、`ShowAward()`（7 种奖励）、`BuildMonsterFrame()`（`STRIP` 12 上画怪 + `+count`） |

要点：

* **分支照抄**：两者都是「按种类拼一组对话框元素 + `showStandardTextMessage`」。端口把绘制结果（`MessageBox`）交回调用方，不自己渲染。
* **法术书卷轴走自己的字段**：`ArtifactType.SpellScroll` 时描述取 `ArtifactData.GetDescription( bonusData.ArtifactSpellId )`，与参考 `Artifact::SetSpell()` 后再取描述等价。
* **升级链按参考终止**：`CreatureCurse`/`CreatureAlliance` 一路 `MonsterUpgrades.GetUpgrade()` 到返回自身为止。
* **带入部队默认读存档**：`CarryOverForces` 的部队列表可显式传，默认 `CampaignSaveData.Get().CarryOverTroops`。
* **两个未知种类抛异常**：参考在这两处是 `assert( 0 )`，端口 `ArgumentOutOfRangeException`。
* **顺带补的前置**（都在 `Game/Heroes`）：`PrimarySkillNames.GetDescription( skill )`（对应 `Skill::Primary::StringDescription( skill, nullptr )`，`skill.cpp:282-323` 的无英雄那一半）与 `SecondarySkills.GetDescription( skill, level )`（对应 `Skill::Secondary::GetDescription( fakeHero )`，`skill.cpp:481-619`）。两者的「带真实英雄/世界」版本要等 `skill.cpp` 逐函数核对，注释里已写明。

顺带核实了两项，文档此前标的是「部分/纯缺失」：

* `ui_map_interface.cpp` 的 `displayStandardPopupWindow` 与 `getPopupWindowPosition` 早已在 `Widgets/StandardPopupWindow.cs` 里，planv4 的判定改为①完成。
* `ui_constants.h` 五个常量已收成 `UI/UiConstants.cs`，各处只改初始化表达式，公开名字不变。

测试：`CampaignPopupWindows` 的像素对拍依赖真实 ICN 与字体，按计划留到逐函数核对那一轮；本轮只保证转完 + 0 警告。

构建 0 错误 0 警告，全量 **5 058 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 243）。
`SkillBarsTests.PrimaryFull_Redraw_PaintsThePlateOfEverySkill` 这次全量又偶发失败一次（期望 6 实得 10），单跑 11 条全绿、随后复跑全量 1 243 全绿；与上一轮同一现象，属既有用例在并行下的抖动，本轮未改。

**H 行剩余**：`player_info.cpp`(380，需要先补 `ui_dialog.cpp` 的 `PlayerTypeDialogElement`/`RaceDialogElement`)、
`interface_itemsbar.h`(460)、`ui_mage_guild.cpp` 与 `ui_castle.cpp` 的逐函数核实、`ui_tool.cpp` 的 `UIScrollInertia`。
之后仍是：英雄对话框 → 城堡对话框 → G5 → I → J。

## 2026-11-22 玩家面板（`gui/player_info.cpp`）+ 并行抖动根治

`Interface::PlayersInfo` 整篇转完（462 行），并把它依赖的两处补上。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Widgets/PlayersInfo.cs` | `player_info.cpp:47-462`、`player_info.h:36-70` | `IPlayersInfoArtwork`、`PlayerInfoEntry`（五个 ROI）、`PlayersInfoResult`（无操作/重绘/弹窗/改名）、`UpdateInfo`、`SwapPlayers`、`GetFromOpponentClick` / `GetFromOpponentNameClick` / `GetFromClassClick` / `GetPlayerFromHandicapRoi`、`RedrawInfo`、`QueueEventProcessing`、`ReadOnlyEventProcessing`、`ApplyName` |
| `Game/GameScenarioInfo.cs` | `game_scenarioinfo.cpp:526-529` | `GetStep4Player()`：`game_scenarioinfo.cpp` 整屏属 S14-1，先把排图标要用的这一个函数落在它的目标文件里 |
| `Assets/Maps/Mp2FileInfo.cs`（改） | `maps_fileinfo.h:140-143` | `AllowChangeRace( color )` |
| `UI/Dialogs/InputStringDialog.cs`（改） | `ui_dialog.cpp` 的 `inputString` | 加 `initialText`：参考的 `std::string & res` 既是初值也是结果，端口原来只有结果那一半 |

要点：

* **一排从右往左画**：阴影画在图标的 `x - 5` 处，从左往右画会让左格的阴影压到右邻的图标上。
* **`GetStep4Player` 的居中**：`id * width * 6 / count` 加上右侧剩余槽位的一半，2 个玩家时落在 1/4 与 3/4 处（已用单测钉住）。
* **单人局换人**：人类颜色多于一个时参考是 `assert( 0 )`，端口抛 `InvalidOperationException`；换人时把手气从旧颜色搬到新颜色、旧颜色清零。
* **手气只在人类格可点**：参考查 `GetControl() & CONTROL_AI`，端口用原始 `Control` 位而不是 `IsControlAi`（后者会把 `Human|AI` 归一成 AI，行为就变了）。
* **事件返回值**：参考返回 `bool`，端口要把弹窗与改名提示带出去，所以返回 `PlayersInfoResult`；改名由调用方跑 `InputStringDialog` 再调 `ApplyName`，取消与清空都保持原名。
* **测试**：`PlayersInfoTests` 6 条（`GetStep4Player` 两人/满员/零人、ROI 布局、四个命中测试、清选中、改名规则）。`RedrawInfo` 的像素对拍要真 `NGEXTRA` 与字体，留到逐函数核对那一轮。

**顺带根治了那条偶发失败**：`SkillBarsTests.PrimaryFull_Redraw_PaintsThePlateOfEverySkill` 期望 6 实得 10，
原因是技能条画技能名要用全局字体，而字体表跟着 `GameAssets.Current` 变；`BorderWindowTests`、`ButtonFactoryTests`、
`HeroQuickInfoTests`、`StatusPanelTests` 这四个类会换 `GameAssets.Current` 却没进 `GlobalArchiveCollection`，
于是并行时它们塞进去的桩资源包字体被另一条线程拿去画字，`10` 就是桩字体里的调色板值。四个类补上
`[Collection(GlobalArchiveCollection.Name)]`，`SkillBarsTests` 作为「读资源」的一方也一并进集合。改完连跑四次
UI 全量均 1 249 绿，抖动不再复现。

构建 0 错误 0 警告，全量 **5 064 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 249）。

**H 行剩余**：`interface_itemsbar.h`(460，要替换自建的 `ItemsBarLayout`，牵动 `ArmyBar`/`ArtifactsBar`/`SkillBars`)、
`ui_mage_guild.cpp` 与 `ui_castle.cpp` 的逐函数核实。
之后：英雄对话框 → 城堡对话框 → G5 → I → J。

## 2026-11-22 道具格基类（`gui/interface_itemsbar.h`）

参考的 460 行模板拆成两个 C# 类（C# 没有模板，两个都得是泛型基类）：

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `UI/Widgets/ItemsBar.cs` | `interface_itemsbar.h:35-311` | `ItemsBar<T>`：`SetContent` / `SetContentItems` / `SetItemsCountPerRow` / `SetTableSize` / `SetSingleItemSize` / `SetInBetweenItemsOffset` / `SetRenderingOffset`、`CalculateRoi` / `CalculateItemPositions`、`GetItemIterPos`、`Redraw`、`QueueEventProcessing`、`RedrawBackground` / `RedrawItem` / `RedrawItemIter` / `ActionCursorItemIter` / `ActionBarCursor` / `ActionBarLeftMouseSingleClick` / `ActionBarRightMouseHold` |
| `UI/Widgets/ItemsActionBar.cs` | `interface_itemsbar.h:313-508` | `ItemsActionBar<T>`：选中（`IsSelected` / `GetSelectedItem` / `GetSelectedPos` / `GetSelectedIndex` / `ResetSelected`）、双条 `QueueEventProcessing(other)`、`ActionCrossItemBarDrag`，以及 14 个 `ActionBar*` 钩子（单击/双击/按下/释放/按住/右键，各有一参和两参版本） |

要点：

* **items 存下标，不存拷贝**。参考 `std::vector<Item *>` 指向调用方容器；端口 `SetContent` 保存容器本身 + 下标表，`ItemAt()` 每次回查，于是英雄新学的技能、刚换的部队都立刻反映出来。`Skill::Primary::Type`、`Artifact` 这类枚举是值类型，只有下标间接才能做到这一点。
* **格子多于道具只画背景**。`Redraw` 按 `itemPos` 遍历格子，`items` 用尽后仍画背景、不再画道具；`GetItemIterPos` 同样在 items 用尽后停止推进游标，所以空槽的命中判定返回「无」。
* **`GetSelectedIndex()` 的「无选中」是道具数**，不是 -1 —— 参考取 `std::distance( begin, cur )`，`cur == end()` 时刚好等于道具数，端口照抄。
* **自定义每行格子数**：只有列表长度等于行数时才生效，否则原样保存但任何计算都不看它（参考每次用之前都先比长度）。
* **`Redraw*` / `ActionBar*` 是 public**，`RedrawItemIter` / `ActionCursorItemIter` / `SetContentItems` 是 protected——与参考 `interface_itemsbar.h` 的段划分一致。
* **`skill_bar.cpp` 的两条 bar 已接入**：`PrimarySkillsBar : ItemsBar<PrimarySkill>`、`SecondarySkillsBar : ItemsBar<SecondarySkillLevel>`，各自只留 `RedrawBackground` / `RedrawItem` 两个钩子，`Redraw` 与 `Layout` 中转全部删掉（`KingdomHeroRow.cs` 与 `SkillBarsTests.cs` 同步改为直接调用）。
* **测试**：`ItemsBarTests` 8 条（无表尺寸不画、区域与位置、格间距、整体偏移、自定义行数、空槽命中、只画有货的槽、ActionBar 初始状态与重置）。

构建 0 错误 0 警告，全量 **5 072 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 257）。

**剩余**：`ArmyBar`（441 行 + `ArmyBar.Input.cs`）与 `ArtifactsBar`（314 行）还挂在自建 `ItemsBarLayout` 上，要改成继承
`ItemsActionBar<T>`，把现有的 Redraw / 选中 / 双条拖放改成钩子——它们的 sprites 是逐次传入的（`Redraw(output, sprites)`），
需要先存成字段才能进钩子。两者迁完后 `ItemsBarLayout.cs` 即可删除。之后才是 `ui_mage_guild.cpp` / `ui_castle.cpp` 核实。

### 续：两个操作条接入 + 自建布局删除 + 两个文件核实（同日）

**`ArmyBar : ItemsActionBar<Troop>`**、**`ArtifactsBar : ItemsActionBar<ArtifactType>`**：

* 内容换成 `SetContent` 持有。`ArmyBar.SetContentForArmy()` 把军队那五个 `Troop` 对象（不是拷贝）交给基类，
  所以任何地方合并/清空部队后画出来的就是当下的值；`SetArmy` 换军队时重设内容。`ArtifactsBar` 直接交 `Artifacts`。
* 选中交给基类。`ItemsActionBar` 新增 `Select( int )`：参考的 `curItemPos` 是 protected、只由点击写，端口子类需要
  「无输入也要能框住某格」（神器条画光标、编辑器预览），所以补了这个入口，`Select( -1 )` 等于 `ResetSelected()`，
  越界抛 `ArgumentOutOfRangeException`。
* `SelectedIndex` 仍是 `-1` 表示未选：基类 `GetSelectedIndex()` 按参考语义在未选时返回道具数，这一层在子类里折回 `-1`。
* `Redraw(PixelBuffer, sprites)` 保留原签名（把 sprites 存进 `_sprites` 再调 `base.Redraw`），因为基类钩子没有 sprites 参数；
  绘制内核拆成 `DrawTroop`，`RedrawItem(int, ...)` 的公开便捷重载与 `RedrawItem(Troop, ...)` 钩子都走它。
* `ArtifactsBar` 的 `Redraw` / `Layout` / `IsSelected` / `ResetSelected` / `Select` 全部删掉改用基类。

**`ItemsBarLayout.cs` 已删除**，`ArmyBar` / `ArtifactsBar` / 两条技能条 / 三个屏幕类（`CastleScreenView`、`KingdomHeroRow`、
`KingdomOverviewLists`）与测试里的 `.Layout.` 中转全部去掉。删除时一次过宽的替换误伤了 10 个与 `CastleDialogLayout` 相关的
文件（`view.Layout.X` → `view.X`），已按 `git diff` 逐对还原，最终 `git diff --stat` 对这 10 个文件为空。

**`ui_castle.cpp` 核实**（9 个函数）：调色板四个助手与 `CastleFlagPalette` 逐条一致；`drawCastleIcon` / `drawResourcePanel` /
`drawCastleName` / `drawCastleDialogBuilding` 的落点与参数一致。**找出一处偏差并修正**：`getCastleIcnIndex` 的
`Race::RAND` 分支在参考里是 `isCastle ? 25 : 26`（编辑器「随机城堡」图标），端口原来返回 null 并注释断言「文件没有这两帧」；
按参考改为 25/26，`_ => null` 保留给数据不可能出现的种族（参考是 `assert(0)` 后回落 1，会画出空槽帧）。测试
`ARandomCastleHasNoIcon` 随之改为 `ARandomCastleUsesTheEditorFrames`。

**`ui_mage_guild.cpp` 核实**（2 个函数）：`renderMageGuildBuilding` 落在 `MageGuildDialog.DrawGuildBuilding` ——
`GetActiveRoi` 定水平居中、精灵底边压在 `offset.y + 290`、裁剪到左 200 列、随机种族复制一份再套紫色调色板，四项都对得上；
`SpellsInOneRow` 落在 `SpellScrollRow` —— 110 步长、折起 `+7`、图标 `x+3+(w-iw)/2` / `y+31-ih/2`、名字 `x+18,y+57,78` 全一致。
唯一没移植的是 `setCurrentSpell()`（只给编辑器用）。

构建 0 错误 0 警告，全量 **5 072 绿**（Core 283 + Assets 416 + Game 3 116 + UI 1 257）。

**H 行至此收口**。下一步是 I 行：英雄对话框（`heroes_dialog.cpp` + `heroes.cpp` 的对话框部分）。

## 2026-11-22 I（一）：`gui/ui_font.cpp` 的八字码页字形生成

资材里只有 ASCII 字形，西里尔、中欧、土耳其、波罗的等语言的字母由参考在启动时用「拷现成字母的像素 + 补几个点 + 重算阴影」
拼出来。这一步把那段生成器整块搬过来，并接进 `FontGlyphTable` 的基表构建。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `Game/Gui/FontAlphabet.cs` | `ui_font.cpp:168` `resizeCodePage`、`6762` `generateAlphabet`、`54`/`59`/`64` 两个阴影助手与 `removeShadows` | `Apply( language, size, table )`：判语言 → 取码页 → 扩到 256 槽 → 生成 → 折回帧表；`NeedsGeneratedAlphabet` 对应 `ui_language.cpp:291 updateAlphabet` 的判据 |
| `Game/Gui/FontAlphabet.Cp1250.cs` … `FontAlphabet.Iso8859_3.cs` | `186/1371/2398/3463/3792/4033/4519/4719` 八个 `generateCPxxxxAlphabet` | 逐句转写，1037 / 855 / 926 / 252 / 202 / 401 / 165 / 190 条语句 |
| `tools/GenerateFontAlphabets.ps1` | 同上 | 解析参考文本、按语句模板转成 C# 的生成脚本；文件头写「Generated … do not edit」 |
| `Core/Imaging/ImageOps.Sprites.cs` | `engine/image.cpp:2398` `fheroes2::Flip` | 新增 `FlipRegion( source, sourceArea, destination, destinationOffset, horizontally, vertically )` |
| `Game/Gui/FontGlyphTable.cs` | `ui_language.cpp:291` | 基表在 `FontAssetFixes.Arrange` 之后调用 `FontAlphabet.Apply( GameLanguage.Current, size, built )` |

要点：

* **两套索引**：参考的 `icnVsSprite` 以码位 0–255 索引（`resizeCodePage` 把 128 帧前置补齐到 256），端口以「字符 − 0x20」索引，
  所以生成前后各做一次 `Expand` / `Compact`；空图像（`resizeCodePage` 留下的 0×0）在折回时变 `null`，与参考的空帧同义。
* **一个参考函数拆成两个方法**：参考每个函数先处理 `ICN::FONT` 再处理 `ICN::SMALFONT`，端口两张表分开构建，
  脚本在遇到 `ICN::SMALFONT` 时切换输出列表，生成 `Generate<Cp>Normal` 与 `Generate<Cp>Small`。
* **块作用域**：参考用 `{ }` 隔离每个 `temp` 并复用 `offset`，C# 不能重复声明同名局部量；脚本丢弃花括号并给 `temp` 唯一名。
* **`font[a] = font[b]`** 是参考的按值拷贝（`vector<Sprite>`），端口写 `.Clone()`，不会把生成结果写回 ASCII 源图。
* **判据**：英文配英文资材不生成；语言与资材语言不同、或资材是法语/俄语时生成；法语资材配英文按 CP1252 生成
  （`ui_language.cpp:291-336`）。仓库自带资材是英文，所以英文流程仍是提前返回，行为不变。

测试（新 `tests/FHeroes2.Game.Tests/FontAlphabetTests.cs`，7 例）：英文不生成（同一实例返回）、俄语生成 CP1251 的 `0xC0`
且是 `A` 的副本、`0xA0` 是 6×1（小号 4×1）的不断行空格、波兰语 `0xA5` 比 `A` 高 3 像素、大号不生成、连跑两种语言不改 ASCII 字形，
以及用仓库自带 `FONT.ICN` 的真实字形跑一遍 CP1250（真实字形尺寸才是那些绝对坐标语句的输入）。

构建 0 错误 0 警告，全量 **5 100 绿**（Core 283 + Assets 416 + Game 3 123 + UI 1 278）。

## 2026-11-22 I（二）：按钮字体的非 ASCII 字母

普通字体补完字母后，按钮字体（`BUTTON_*_FONT`）也得有同一套字母，否则语言一换、按钮上的文字就断在 ASCII 边界。
这一批把参考剩下的五段 `generateCPxxxxGoodButtonFont`（`ui_font.cpp:5604-6807`）和 `updateButtonFont`（`:170`）搬完。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/Gui/ButtonFont.Cp1250.cs` … `ButtonFont.Cp1254.cs`（新） | 五个 `generateCPxxxxGoodButtonFont` | 逐句转写，各 300 余条语句；头注释标「Generated … do not edit」 |
| `tools/GenerateButtonFontAlphabets.ps1`（新） | 同上 | 转写脚本；按钮版比普通版多出 `SetPixel` / `DrawLine` / `Flip` / `Blit` / `SetTransformPixel` 与 `for ( charCode : {...} )` 循环，全部有对应模板，未知语句直接抛错 |
| `Core/Imaging/ImageOps.Sprites.cs` | `engine/image.cpp:2910` `fheroes2::SetTransformPixel` | 新增 `SetTransformPixel( sprite, x, y, transformId )`：清像素 + 写变换层，越界与单层图直接返回 |
| `Game/Gui/ButtonFont.cs` | `generateButtonAlphabet` / `updateButtonFont` | `Create( SupportedLanguage )`：按码页选补充方法 → `Widen`（128 帧按参考 `insert` 扩成 256 帧，每格一份 1×1 副本）→ 补字母 → 派生四个面；新增 `BigSpaceWidth = 8` |
| `Game/Gui/FontGlyphTable.cs` | `getCharacterLimit` | 按钮面按语言缓存（`Dictionary<SupportedLanguage, Faces>`），字符上限改成实际表长（英语 128、补过字母的语言 256） |

要点：

* 参考的 `released[N].resize( w, h )` + `released[N].reset()` 合成一次 `ButtonFont.Create( w, h, old )`，`setPosition` 走
  `Moved`（Sprite 不可变，换一个共享图层的锚点）；赋值句 `released[a] = released[b]` 一律 `.Clone()`，否则后面就地
  `DrawLine` 会把源字母一起改掉。
* `fheroes2::Blit( src, dst, true )` 与 `fheroes2::Flip(...)` 都落到 `ImageOps.FlipRegion`：按钮字母带变换层，
  FlipRegion 对多层目标会同时写颜色层与变换层，与参考一致。
* 只有 CP1250–CP1254 五个码页有按钮补充（参考的 switch 就这么选的），越南语（CP1258）、罗马尼亚语（ISO-8859-16）、
  世界语（ISO-8859-3）按参考保持 128 帧。

测试（新 `tests/FHeroes2.Game.Tests/ButtonFontTests.cs`，5 例）：英语 128 帧四个面一致、俄语 256 帧且 `0xC0` 是
10+2×2 宽的独立字形、不断行空格 8×1、土耳其语有 `0xDD`/`0xFD`、无补充的码页停在 128、俄语下四个面 0x20–0xFF 全非空。

构建 0 错误 0 警告，全量 **5 105 绿**（Core 283 + Assets 416 + Game 3 128 + UI 1 278）。

**I 行至此收口**，`ui_font.cpp` 无剩余。A→J 只剩 G5（App 侧 `IAdventureHost` 与 `AdventureMapScene` 接管、删自建面板）
与 J（清理、测试迁移、全量回归）。

## 2026-11-22 G5（2a）：宿主里的模态帧泵

`IAdventureHost` 的 17 个成员全是同步返回（`bool ShowYesNoMessage(...)`、`bool OpenSpellBook( hero, out Spell )`），
而 App 的主循环是「每帧拉一次场景」的状态机，弹窗都只是场景里的 overlay，没有同步阻塞这条路。这一批先把
「同步返回」的地基落下来。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `App/IModalScreen.cs`（新） | `Dialog::Message` / `Dialog::YES_NO` 等自己转的循环 | 窗口契约：`IsFinished` + `SetCursor` / `SetLeftButton` / `SetRightButton` / `HotKey` + `Draw` / `Update`，输入面与 UI 层对话框一致 |
| `App/GameHost.cs` | 同上 | 新增 `RunModal( IModalScreen )`：内层取事件 → 场景重绘 → 窗口画上去 → 上传，`_modal` 保证输入不再流进下面场景；`Draw()` 在 `_systemInfo` 之后、`Upload` 之前画窗口 |
| `App/Modals/MessageBoxModal.cs`（新） | `Dialog::Message` / `Dialog::YES_NO` | `MessageBox` 的适配器：按到的标准按钮写进 `Answer` 并置 `IsFinished`，`Create(...)` 直接对应两个参考函数 |

要点：

* 泵可以在事件处理里被调用——`IAdventureHost` 的方法正是从 `PumpEvents` 的调用栈里唤起；内层再调
  `PumpEvents` 是安全的（SDL 事件队列全局，`_modal` 挡住重复分派），这与参考「在事件处理里开对话框」同构。
* 鼠标移动 / 离开 / 按键 / 滚轮在窗口打开时全部改投窗口，滚轮不落到冻结的场景；`Escape` → `Cancel`、
  `Enter`/`Space` → `Okay`，与 `Dialog::YES_NO` 的键位一致。
* `F11`（全屏）、`F12`（截图）仍在窗口之前处理，属于引擎级按键。
* `_presenter` 为空的诊断屏下不开窗口；同一时刻只允许一个窗口，嵌套调用直接抛异常，不做静默叠加。

构建 0 错误 0 警告，全量 **5 105 绿**；`--enter-map --smoke-frames 30` 冒烟正常（58.6 FPS，无行为变化）。

**G5 剩余**：`IAdventureHost` 的 17 个成员接线（`HeroDialog` / `KingdomOverviewWindow` / `PuzzleMapDialog` /
`SpellBookWindow` / `GameInfoDialog` / `AdventureOptionsDialog` / `FileOptionsDialog` / `SettingsDialog` /
`SelectFileDialog` / `QuickInfoDialog` 都已在，`OpenCastleDialog` / `OpenViewWorld` 走场景回调），其中
`SaveGame` 依赖 `GameSave.Save`（仍抛 `NotImplementedException`，等世界序列化），`CheckGameOver` 依赖尚未移植的
`GameOver::Result::checkGameOver`；之后才是 `AdventureMapScene` 接管与删除自建面板。

## 2026-11-22 G6.1（前半）：LocalEvent.HandleEvents 与事件引擎契约

G6 放弃「事件只报告要开什么」的请求式契约，改回参考的同步路径：对话框自己写
`while ( le.HandleEvents() ) { ... }`。这需要把当初判定为「SDL 侧、不移植」的 `HandleEvents` 补回来。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Input/IEventEngine.cs`（新） | `EventProcessing::EventEngine`（`localevent.h:40`、`localevent.cpp:1265-1313`） | UI 层的平台侧契约：`ScreenArea` + `HandleEvents( state, allowExit, out displayRefreshRequired )` + `Render( area )` + `Sleep( ms )`，不含任何 SDL 依赖 |
| `UI/Input/LocalEvent.cs` | `HandleEvents`（`localevent.cpp:1243-1316`） | 新增 `Engine` / `NextRenderArea` / `LoopSleepTimeMs` / `HandleEvents( sleep, allowExit )`：`BeginFrame()` → 引擎取事件 → 需要整帧刷新时渲染 `ScreenArea`、否则渲染 `CursorRenderArea` → 睡 1 ms；`sleep=false` 时只记 `NextRenderArea`（`display.updateNextRenderRoi`） |
| `tests/FHeroes2.UI.Tests/LocalEventTests.cs` | 同上 | 五条用例：未挂引擎抛异常；按光标区域渲染并睡 1 ms；整帧刷新；`sleep=false` 只记账；引擎返回 false 时不渲染不睡 |

要点：

* 类注释从「`HandleEvents` 没移植」改成「状态重置是 `BeginFrame`，SDL 半是 `IEventEngine`，帧是 `HandleEvents`」，
  手柄 / 触摸板 / 模拟指针仍未移植（需要 SDL 设备）。
* 没有把 SDL 引进 `FHeroes2.UI`：应用侧将实现 `SdlEventEngine`，测试用假引擎，两条路都不改对话框代码。
* `LocalEvent.Current` 是进程级实例，`GameArea` / `ItemsBar` / `ItemsActionBar` 已经在读它，但 App 至今没填——这一批
  之后由 `SdlEventEngine` 填，那些读取路径才第一次有真实状态。

构建 0 错误 0 警告，全量 **5 110 绿**（新增 5 条）。

**G6.1 剩余（App 侧）**：`SdlEventEngine`（SDL 事件 → `OnMouseMotion` / `OnMouseButton` / `OnMouseWheel` /
`OnKeyboard`，键盘需要 `SDL.Keycode` → `UI.Input.Key` 的映射表，App 现在完全没有这张表）、`GameHost` 各 case 填充
`LocalEvent.Current`、主循环改 `le.HandleEvents( false, true )` 驱动（`Draw()` 紧接着自己上传，对应参考的
`HandleEvents(false)` + `render()`）。

## 2026-11-22 G6.1（后半）+ G6.2（引擎侧）：主循环交给 LocalEvent

前半只有契约，这一批把 SDL 那半接上，主循环第一次由 `le.HandleEvents()` 驱动。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `App/Input/SdlKeyMap.cs`（新） | `EventEngine::handleEvents` 的 `SDLK_*` 分支（`localevent.cpp:1450-1660`） | `SDL` keycode → `UI.Input.Key`：字母/数字用 ASCII 连续区段加偏移，标点逐个列，特殊键按 `SDL_SCANCODE_TO_KEYCODE` 的值（F1~F12、方向、PageUp/Down、Home/End、小键盘、左右修饰键）；`ToModifier` 把 `SDL_Keymod` 折成 `KeyModifier` |
| `App/Input/SdlEventEngine.cs`（新） | `EventProcessing::EventEngine`（`localevent.cpp:1265-1313`） | `IEventEngine` 的 SDL 实现：`HandleEvents` 调宿主事件泵（带 `allowExit`），`Render` 上传整帧，`Sleep` 走 `Thread.Sleep` |
| `App/GameHost.cs` | 同上 | 新增 `_le = LocalEvent.Current`；`Run()` 里装 `SdlEventEngine`；`Loop()` 首行改 `_le.HandleEvents( false, true )`；`PumpEvents( allowExit )` 每个事件先 `FeedLocalEvent`（鼠标/按键/滚轮），quit 在 `allowExit=false` 时问 `ProcessQuitEvent()`；`RunModal` 每帧 `BeginFrame()` |

要点：

* 键盘映射不再靠猜：SDL 的字母是小写 ASCII、数字是 `'0'..'9'`，而 `Key` 枚举的 A..Z / Digit0..9 / F1..F12 /
  KeyPad0..9 都是连续块，一段区间加偏移就够；标点与特殊键逐条列，值按 `SDL_keycode.h`。
* `FeedLocalEvent` 在任何场景分支之前执行，和参考「引擎先填状态、场景再读状态」一致；`GameArea.QueueEventProcessing`
  等 UI 读取路径第一次拿到真实状态（App 目前还没调它们，所以无行为变化）。
* quit 语义补上：`allowExit=false`（对话框）时改问 `QuitEventHook`，没有 hook 就不退出，和参考一致；主循环仍传
  `true`，点 X 直接退出。

构建 0 错误 0 警告，全量 **5 110 绿**；三路冒烟全部退出 0：`--enter-map` 30 帧 53.4 FPS、`--scene editor`
20 帧 49.9 FPS、`--enter-town` 20 帧 36.9 FPS（20 帧样本里启动开销占比大）。

**G6.2 状态**：引擎侧的 `Render` / `Sleep` 已就位，`HandleEvents( true )` 的呈现语义可用；验收里的
「对话框内每帧可见」要等 G6.3 的首个 `MessageBox.Show` 才有真实用例。

## 2026-11-22 G6.3：MessageBox.Show 的阻塞循环

`MessageBox.Show(...)` 是端口第一个自己转事件循环的对话框函数，形状照参考
`fheroes2::showMessage`（`ui_dialog.cpp:113-260`）。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/MessageBox.cs` | `ui_dialog.cpp:113-260` | 新增 `Show( target, header, body, headerFont, bodyFont, buttons, buttonArtwork, events )` 与私有 `Run`：每轮 `le.HandleEvents( false )` → `SetCursor` / `SetLeftButton` / `Enter`/`Space`/`Escape` 热键 → `Update` + `Draw` → `engine.Render( target.Bounds )` + `engine.Sleep( 1 )` |
| `tests/FHeroes2.UI.Tests/MessageBoxTests.cs` | 同上 | 两条用例：未挂引擎抛 `InvalidOperationException`；引擎返回 `false` 时 `Show` 返回 `DialogButton.None` 且不画不睡 |

细节：

* `HandleEvents( sleepAfterEventProcessing: false )` 是关键：由 `Show` 自己在「画完之后」提交整帧（参考的
  `display.render()`），否则 `HandleEvents` 只提交 `CursorRenderArea`，对话框画面不动。
* `buttons` 默认 `DialogButton.None`：端口没有参考里那份进程级按钮素材，默认 `Ok` 会让 `Create` 直接抛
  `ArgumentNullException`。要能返回的调用必须同时传 `buttons` 与 `buttonArtwork`。
* 鼠标离开窗口暂不处理（端口没有 `SDL_EVENT_WINDOW_MOUSE_LEAVE` 的翻译），参考的 `setCursor( nullptr )` 缺失。

构建 0 错误 0 警告，全量 **5 112 绿**（新增 2）；`--enter-map` 120 帧冒烟 **59.2 FPS**、退出 0，与改造前
58.6 持平。

**G6.3 剩余**：`AdventureOptionsDialog` 的提示接入——App 现在没有 `RunModal` / `MessageBoxModal` 的调用点
（两者都是定义未用），首个真实用例要等 G6.4 的 `AdventureOptions` 对话框 `Show`。

## 2026-11-22 G6.4（3/5）：AdventureOptions、Settings 与 FileOptions 的阻塞 Show

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/AdventureOptionsDialog.cs` | `dialog_adventure.cpp:55-110` | 新增 `Show( target, view, enableDig, events )`；`enableDig = false` 时 dig 的结果被丢弃，等于参考里禁用那个按钮 |
| `UI/Dialogs/AdventureOptionsView.cs` | 同上 | 新增 `SetCursor( Point? )`，循环靠它驱动 hover |
| `UI/Dialogs/SettingsDialog.cs` | `dialog_gamesettings.cpp:224-260` | 新增实例方法 `Show( target, events )`：Enter / Space → `ProcessHotKey( Okay )`，Escape → `Cancel()` |
| `UI/Dialogs/FileOptionsView.cs`（新） | `dialog_file.cpp:42-152` | 6 个按钮的窗口：`ButtonGroup.CreateFromFrames` + `StandardWindow.CreateForSymmetricButtons` + `PlaceSymmetricButtons`，取消按钮放 `BOTTOM_CENTER`；`isRestartEnabled = false` 时 `buttons[2].Disable()` |
| `UI/Dialogs/FileOptionsDialog.cs` | 同上 | 新增 `FromDialogButton( DialogButton )` 与 `Show( target, view, events )`；引擎退出返回 `Quit`（参考的 `result` 初值就是 `QUIT_GAME`） |
| `UI/Dialogs/DialogButton.cs` | `dialog_file.cpp:55-60` | 新增 `NewGame` / `LoadGame` / `Restart` / `SaveGame` / `QuickSave` / `Quit` 六个位：参考用 GUI 层 `ButtonGroup` 的索引，端口的 `ButtonGroup` 要一个身份 |
| `tests/.../AdventureOptionsViewTests.cs` | 同上 | 3 条：未挂引擎抛异常、两帧点击返回 `World`、禁用 dig 后 Escape 返回 `Cancel` |
| `tests/.../SettingsDialogTests.cs` | 同上 | 2 条：未挂引擎抛异常、两帧点击 OK 返回 `Ok` |
| `tests/.../FileOptionsViewTests.cs`（新） | 同上 | 16 条：3 列 2 行布局、按钮身份、restart 禁用、取消按钮居中、缺帧返回 null、点击/越界/禁用按钮、`Draw` 上色、`FromDialogButton` 映射表、`Show` 的三条（无引擎、点击返回 `QuickSave`、引擎退出返回 `Quit`）、真实素材下 `ButtonFactory` 生成的 12 帧能装进视图 |

要点：

* 三个 `Show` 与 `MessageBox.Show` 同形：引擎取事件 → 光标 / 左键 / 热键 → 画 → 提交 → 睡 1 ms。没有抽公共循环，
  照参考让每个对话框自己写，代价是重复 20 行，好处是逐函数对照时能一一对上。
* `AdventureOptionsView` 的按钮来自 `APANEL.ICN` 的 original-resource 分支（`game_assets.cpp:1585-1592`），
  归档里现成可用；`FileOptions` 的按钮没有 original-resource 分支，走 `ButtonFactory.MakeSymmetricBackgroundSprites`
  （`game_assets.cpp:1563-1578`），端口早有这个移植，视图只接 12 帧，生成与异常处理留给 App 层的 artwork 类。
* 6 个按钮的布局按参考是 **3 列 2 行**（`ui_window.cpp:505-511` 的 `columns = buttonCount / 2`），不是 3 行 2 列；
  测试第一版按「两列三行」写，跑出失败后才回参考确认。

构建 0 错误 0 警告，全量 **5 133 绿**（新增 21：UI 1 290 → 1 306，AdventureOptions 3、Settings 2、FileOptions 16）；
冒烟 `--enter-map` 30 帧 **62.2 FPS**、`--enter-map --adventure-options` 20 帧 **54.2 FPS**、`--open-settings` 20 帧
76.9 FPS，均退出 0。

**G6.4 剩余（2/5）与阻塞原因**：

* `GameInfo`：参考直接读全局 `world`（`dialog_gameinfo.cpp`），端口没有全局世界，等 G6.6 的世界序列化。
* `SelectFile`：需要存档目录列表与滚动条（`dialog_selectfile.cpp` 400+ 行），依赖存档路径服务。

## 2026-11-22 G6.7：GameOver 判定与序列化

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/GameOverResult.cs`（新） | `game_over.cpp:464-682` | `GameOverResult`：`Colors`（`_colors`）、`Condition`（`result`）、`Reset()`、`CheckGameOver()`；配 `GameOverMode`（四个场景切换目标）与 `GameOverRequests`（淘汰通知列表、失败/胜利提示条件、LOSE/WIN 片子、胜利音乐） |
| `Game/GameOverResult.Persistence.cs`（新） | `game_over.cpp:684-702` | `WriteTo` / `ReadFrom( stream, version )`：颜色 1 字节 + 条件 4 字节；1.1.09 之前颜色是 `int32_t`，按版本分流（与 `Army` 的颜色字段同一处收窄） |
| `tests/.../GameOverResultTests.cs`（新） | 同上 | 12 条：`Reset` 取世界颜色、平静回合继续、最后一家获胜、攒金胜利、战役胜利不播片、单机全灭回主菜单、电脑出局被通报并摘色、多人淘汰局不结束、五字节往返、老格式四字节颜色、已失败流不读、截断流补零 |

要点：

* **判定顺序照参考**：先摘出局颜色（电脑一定要通报，人类只通报「不是当前玩家的那一家」），再分单机/多人判胜负，最后把
  「该弹什么窗、该放什么片」放进 `Requests`。参考在这一步直接弹窗放片，游戏层做不到，这是端口唯一的结构性偏差。
* **全灭不是终局**（多人）：只要还有人类玩家在局中，`CheckGameOver` 返回 `Cancel` 但 `Requests.LossNotice` 有值，
  调用方必须两个一起看。淘汰通知用「调用时当前玩家是否还在颜色集合里」去重，对应参考的 `isCurrentPlayerWasActive`。
* **单机全灭的例外**：王国无城且「丢城倒计时」为 0 时参考不弹失败框，因为 `HumanTurn()` 结尾已经提示过；
  这条在端口里同样保留，测试里通过 `LostTownDays` 的初值（`GameOverLostDays + 1`）验证提示确实出现。
* `TYPE_AUTO_PLAYTEST` 分支没有移植（端口无该单例），只影响战报记录不影响判定，已在派生的 XML 注释里写明。
* `Reset()` 用 `Settings.Get().Players.GetColors()`——`Game::GetKingdomColors()` 就是它（`game.cpp:451`），
  不是「地图上实际还在的颜色」，两者在开局与玩家中途出局后会不同。

构建 0 错误 0 警告，全量 **5 161 绿**（Game 3 128 → 3 140，新增 12）；冒烟 `--enter-map` 30 帧 56.0~61.9 FPS（多次运行的
波动）、`--adventure-options --enter-map` 20 帧 55.4 FPS、`--scene editor` 20 帧 75.4 FPS，均退出 0。

### G6.7 收尾：条件文案与三个结果窗口

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/GameOverText.cs`（新） | `game_over.cpp:278-467` | `GameOver.GetString( condition )`（单条条件一句话）、`GameOver.GetDescription( conditions, info, world )`（场景信息里那句完整描述：占城/杀将/找神器/攒金/超时/同盟，外加「也可以全灭」） |
| 同上 | `game_over.cpp:71-246` | `GameOver.GetVictoryMessage` / `GetLossMessage`：胜负窗口的正文表（含 `LossEnemyWins*` 三条「对手靠某条件赢下」） |
| `UI/Dialogs/GameOverDialogs.cs`（新） | 同上 + `game_startgame.cpp:251-286` | `ShowVictory` / `ShowDefeat`（标题 `Victory!`/`Defeat!` + OK）、`ShowVanquished`（`%{color}` 替换 + 色徽）、`ComposeSign`（BRCREST 边框上按 `(4,4)` 盖色徽）、`IVanquishedSignArtworkSource` |
| `UI/Dialogs/MessageBox.cs` | `fheroes2::showMessage` 的「先拼后跑」 | 新增 `Show( target, box, events )`：跑一个已经 `Create` 好的框（带 `DialogElement` 的那种） |
| `tests/.../GameOverTextTests.cs`（新） | 同上 | 29 条：11 条 `GetString` 表 + 描述句（攒金/神器/终极神器/补「也可全灭」/超时拆月周日/占城缺对象回退/同盟两种句式）+ 胜负正文（全灭无窗、混位无窗、缺对象无窗、对手攒金照抄原文 “bow done”） |
| `tests/.../GameOverDialogsTests.cs`（新） | 同上 | 7 条：无文案时连一帧都不画（`DialogButton.None`）、`ComposeSign` 的 `(4,4)` 落位与尺寸、非单一颜色抛 `ArgumentException`、引擎停止返回 `None`、`ShowVanquished` 能画出整个窗口 |

要点：

* 参考的 `GetActualDescription` 返回 `vector<LocalizedString>`，因为地图里写死的城名/英雄名要按地图语言单独本地化，
  模板与名字各是一段；端口没有这一层，名字直接替换进句子、返回一个字符串。拼接结果一致，差的只是「每段能分别换语言」。
* 对象缺失（编辑器写坏的图、城已被拆）时参考 `assert` 后留一句空白，端口不 assert，退回 `GetString` 的通用句式或返回空串（胜负窗口）。
* 胜负正文表用 `switch` 精确匹配：判定每次只带一条条件，所以相等成立；混着别的位就没有句子——参考如此，端口照抄并写了测试。
* 三个窗口都不碰音频（参考在函数里自己 `PlayMusic( MUS::VICTORY / MUS::LOSTGAME )`），调用方按
  `GameOverRequests.VictoryMusic` 先播即可。
* 战役「龙之城」的额外提示需要战役系统的当前关条件，端口还没有战役存档，已在 `GetVictoryMessage` 的注释里标为待补。

构建 0 错误 0 警告，全量 **5 197 绿**（Game 3 140 → 3 169，UI 1 322 → 1 329，新增 36）；冒烟 `--enter-map` 30 帧 54.8 FPS、
`--scene editor` 20 帧 73.9 FPS，均退出 0。`GameOverDialogs` 的两条「真开窗口」的测试要 AGG 美术，本机无游戏数据
（`F:\game\fh2` 下没有 AGG），按既有约定直接返回、不构成假绿。

## 2026-11-22 G6.5（4/7）：HeroDialog 与 KingdomOverview 的阻塞 Show

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/HeroDialog.cs` | `heroes_dialog.cpp:435-831` | 新增 `Show( target, events )`：每帧 `ProcessEvents` + `Draw` + 提交；`Result` 非 `None` 时返回它，`Requests` 非空时返回 `None` 让调用方先开那个窗口，引擎停止按参考的关闭路径返回 `Cancel` |
| `UI/Dialogs/HeroDialogRequests`（同文件） | 同上 | 新增 `IsEmpty`，循环用它判断「这一帧有没有要交给调用方的东西」 |
| `UI/Dialogs/KingdomOverviewWindow.cs` | `kingdom_overview.cpp:847-940` | 新增 `Show( target, events )`，返回 `bool`（`true` = 窗口关闭）：Escape 走 `CloseWindow` 快捷键，`Requests` 非空时先画一帧再返回 `false`，只有需要重画时才 `Draw` |
| `tests/.../HeroDialogTests.cs` | 同上 | 4 条：未挂引擎、Escape 返回 `Cancel`、右键阵型请求让循环返回 `None` 且 `Requests.Popup` 有内容、引擎停止返回 `Cancel` |
| `tests/.../KingdomOverviewWindowTests.cs`（新） | 同上 | 4 条：未挂引擎、引擎停止返回 `false` 且未关闭、Escape 关闭、右键收益行返回 `false` 且 `Requests.Income` 为真；窗口首次被实例化（`Kingdom` + `World` + 全 640x480 单色帧的测试美术） |

要点：

* 这两个对话框的循环与前面几个不同：参考在循环里**自己**打开英雄/城镇界面、快捷信息、收益与灯塔窗、解雇确认、部队交互。
  端口把这些集中在 `Requests` 上，循环无法代替调用方开窗，于是采用「能自己答完就继续，需要别人就交还」的语义：
  `HeroDialog.Show` 返回 `None`，`KingdomOverviewWindow.Show` 返回 `false`，两者都先把当前帧画出来，屏幕不会闪。
* `KingdomOverviewWindow` 之前从未被实例化过（没有工厂，美术由场景拼），测试为此补了 `Kingdom`/`World` + 单色帧美术的构造，
  顺带覆盖了 `Draw` 在真实石框上的整条路径。
* `KingdomOverviewWindow` 的淡入淡出、难度音效与 `:949-958` 的冒险地图接口调用仍归调用方，与 `Close()` 上的说明一致。

构建 0 错误 0 警告，全量 **5 149 绿**（UI 1 314 → 1 322，新增 8）；冒烟 `--enter-map` 30 帧 **66.4 FPS**、
`--enter-map --adventure-options` 20 帧 51.4 FPS，均退出 0。

**G6.5 剩余（3/7）与阻塞原因**：

* `CastleDialog`：端口只有结果枚举与分支表，城堡画面在 `CastleScreenView` / `CastleScreenInput`（App 的 `CastleScene`），
  阻塞循环属于场景而不是 `UI/Dialogs`。
* `ViewWorld`：整窗尚未移植（`gui/view_world.cpp` 800+ 行）。
* `QuickInfo`：`QuickInfoDialog` 只有模型，面板渲染（`dialog_quickinfo.cpp` 的框、行距与图标）还没移植，`Show` 无可画之物。

## 2026-11-22 G6.5（2/7）：PuzzleMap 与 SpellBook 的阻塞 Show

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `UI/Dialogs/PuzzleMapDialog.cs` | `puzzle.cpp:119-210` | 新增 `Show( events )`：第一段循环按 `RevealStepDelayMs = 10` 推进揭示动画（`Stopwatch` 计时，对应 `Game::PUZZLE_FADE_DELAY`），第二段是纯等待；两段共用一个 `ApplyFrame`（按钮按下态 → 点击/Escape → 重画 → 提交 → 睡） |
| `UI/Dialogs/SpellBookWindow.cs` | `spell_book.cpp:269-397` | 新增 `Show( target, events )`：`ProcessEvents` 每帧读默认快捷键（Escape 关闭/取消、左右翻页），`IsClosed` 或 `ChooseSpellToAdd` 时退出，返回 `SelectedSpell` |
| `tests/.../PuzzleMapDialogTests.cs` | 同上 | 4 条：未挂引擎、点击退出按钮（检查按下态已复位、按钮回到释放帧）、Escape、引擎停止 |
| `tests/.../SpellBookWindowTests.cs` | 同上 | 4 条：未挂引擎、点击法术返回该法术且两帧都提交、Escape 返回 `None`、编辑模式在 `ChooseSpellToAdd` 停住且不关书 |

要点：

* 两个 `Show` 都没有重新实现输入：`ProcessEvents` / 现有命中判定已经完整，循环只负责「取事件 → 判定 → 重画 → 提交 → 睡眠」。
* `PuzzleMapDialog` 的右键提示（`showStandardTextMessage( "Exit", "Exit this menu." )`）没接：端口 `MessageBox.Show` 要调用方传字体与按钮素材，
  这个对话框不持有它们。右键当前无动作，注释里写明了。
* `SpellBookWindow` 的 `ShowMessage` / `ShowSpell` 两个动作同样只画不弹，`ChooseSpellToAdd` 直接退出把选法术的活留给调用方
  （参考在这里开 `Dialog::selectSpell`，端口是 `SpellSelectDialog`，属于 App 编排）。

构建 0 错误 0 警告，全量 **5 141 绿**（UI 1 306 → 1 314，新增 8）；冒烟 `--enter-map` 30 帧 **66.2 FPS**、
`--enter-map --adventure-options` 20 帧 48.0 FPS、`--open-settings` 20 帧 77.2 FPS，均退出 0。
