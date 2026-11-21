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
| S6 | `castle` | 7 | 待办 |
| S7 | `kingdom` | 6 | 待办 |
| S8 | `maps` | 6 | 待办 |
| S9 | `world` | 4 | 待办 |
| S10 | `battle` | 5 | 待办 |
| S11 | `ai` | 9 | 待办 |
| S12 | `gui` | 9 | 待办 |
| S13 | `dialog` | 6 | 待办 |
| S14 | `game` | 6 | 待办 |
| S15 | `campaign` | 1 | 待办 |
| S16 | `editor` | 5 | 待办 |

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

验证：`dotnet build` 0 错误 0 警告；`dotnet test` 4 554 全绿。
