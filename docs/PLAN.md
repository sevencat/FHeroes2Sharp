# 移植计划

> 参考实现：**fheroes2 1.1.18（commit `6c51acfdc`）**，源码在 `F:\code\fheroes2`
> 目标：C# / .NET 10 / SDL3 / SDL3_ttf，逻辑分辨率 640×480。
>
> 本文件是**唯一的计划**。进度在 [`PROGRESS.md`](PROGRESS.md)，决策在 [`DECISIONS.md`](DECISIONS.md)（ADR），
> 架构约定在 [`ARCHITECTURE.md`](ARCHITECTURE.md)，参考件位置在 [`REFERENCE.md`](REFERENCE.md)。
>
> 2026-11-22 压缩：此前四代计划与账本（`planv3`/`progressv3`/`planv4`/`progressv4` 与第一代的逐文件盘点）
> 压成这两份。已完成的批次在本文件只留「归谁接管 + 偏差」，逐文件覆盖表、批次核算、目录流水不再维护；
> 原始措辞查 git history（压缩前最后提交 `673645e`）。

## 1. 总体原则

1. **外部资源格式逐字节一致**：AGG / ICN / TIL / MP2 / H2D / SMK / `.mo` / `KB.PAL` 的解析与 C++ 完全一致，
   包括名称哈希、条目顺序、裁剪/透明规则、字节序。
2. **内部工程现代重构**：`readonly record struct` 几何、`sealed` 服务类、`Nullable=enable`、分层命名空间、
   显式依赖注入（无 DI 容器）、可单元测试。
3. **参考逻辑函数级 1:1 移植**（ADR-057）：同名函数、同顺序、同常量，允许结构差异（分层、不可变、无指针），
   **不做行级 1:1**；推理顺序与分支要能指回参考的文件名与函数名。偏离规则见 `ARCHITECTURE.md`。
4. **不提交任何原版资源**：GPLv2，仓库只含代码与可再分发字体。
5. **每个里程碑都能运行、能验证**：可执行 + 测试 + 截图/日志证据。
6. **整目录推进**（2026-11-21 起）：
   * 以参考源码目录（`F:\code\fheroes2\src\fheroes2\<dir>`）为单位，一个目录全转完才进下一个。
   * **依赖先落接口**：先写好签名 + 文档注释，方法体抛 `NotImplementedException`；**禁止在调用方写临时替代或兜底**。
   * **第一遍只求转完**（能编译、0 警告）；核对与行为测试放全部目录转完之后；**`editor/` 最后**。
7. **自建件必须记账**：任何「自己拼的」绘制/事件/控制器都进删除台账（§5.4），被参考件接管后立即删，
   不许两边并存长期运行。

## 2. 里程碑

**M0–M8、E1–E3b 全部完成**：解决方案骨架与 SDL3 640×480 + 2x（M0）；图像栈，与原版 C++ 对拍 14 项 /
1 204 张 BMP 逐字节相同（M1）；主菜单、位图字体、CJK 补位、`.mo` 本地化、BGM（M2）；MP2 头与场景列表（M3）；
冒险地图渲染（M4，已被第二阶段的 `Interface::AdventureMap` 接管重写）；回合与英雄规则（M5）；城镇规则与
城堡屏（M6）；存档序列化全链路（M8）；编辑器只读可视化、可改、保存 MP2（E1–E3b）。

**未完成**：M7 只剩 `battle_interface.cpp`（6 144 行）等战斗界面归第二阶段；M9 的战役界面未接、SMK 视频与
NativeAOT 评估未开始；M2 语言选择窗口、M3 难度/标准局-战役分叉按 `PROGRESS.md` §7.4 挂账。

## 3. 第一阶段：按目录整块转换 S0–S16

口径见原则 6，**S0–S16 全部完成**，落点如下（偏差见 `PROGRESS.md` §3）：

`agg`/`image`/`h2d`/`audio`/`system` → `Assets/` + `Core/`；`monster` → `Game/Monsters/`；
`spell` → `Game/Spells/`；`resource` → `Game/Resources/` + `Game/Artifacts/`；`army` → `Game/Army/`；
`heroes` → `Game/Heroes/`；`castle` → `Game/Castles/`；`kingdom` → `Game/Kingdom/`；`maps` → `Game/Maps/`；
`world` → `Game/World/`；`battle` → `Game/Battle/`（非界面）；`ai` → `Game/Ai/`（**未转完**，§4）；
`campaign` → `Game/Campaign/`；`gui` → `UI/` + `Game/Gui/`、`dialog` → `UI/Dialogs/`、
`game` → `App/` + `UI/Interface/`（这三处第一遍，第二阶段按 1:1 重做，§5）；
`editor` → `Game/Editor/` + `App/Scenes/Editor*`（模型层按参考，窗口未按参考）。

## 4. `ai` 目录：实现批次（B / X）

契约先行已落（ADR-161、ADR-180、`AiContractTests`），**B0–B3、B5、B6、X1、X2 完成**。剩余：

| 批次 | 缺什么 | 依赖 |
| --- | --- | --- |
| B4 | `KingdomTurn` 的回合循环（10/11，ADR-195） | — |
| B7 | `HeroesAction` / `HeroesMove` 两个入口（2/4） | X4 |
| B8 | 战斗主循环里**施法行动、塔与投石车**（回合循环与近战已落） | — |
| X3 | `battle_action` 的法术目标与生效 | — |
| X4 | `heroes_action` 补全 + 对象交互 + Interface 解耦 | — |

原则：一个 `NotImplementedException` 是一个原子任务；禁止静默兜底（不许返回 0/null/空集合）。

## 5. 第二阶段：1:1 还原缺口（从 `gui/` 开始）

### 5.1 口径

* **1:1 的判据**：参考的每个非内联函数在端口都有同名或同义成员，函数体逐段对应——循环边界、魔数、
  绘制顺序、早退条件一致；文档注释写清参考文件与行号。
* **三类差距**：①部分（有文件，缺函数或分支）②未移植（用自建件顶替）③纯缺失。
* **删除顺序**：写参考移植 → 调用方切换 → 测试迁移 → 删自建文件。任何一步没做完就不删。
* **不做的事**：不为了对齐而重命名已 1:1 的文件；`interface_*` 一个类拆多文件可以保留，
  但类与函数必须与参考对得上。

### 5.2 分组与状态

`gui/`（33 个 cpp）与它牵出的 `game`/`dialog` 依赖按字母推进，**A–F、G1–G5、H、I 全部完成**
（`ui_tool`、`cursor`、`ui_text`/`ui_font` 文本层、`ui_keyboard`+`ui_language`、`ui_dialog`/`ui_window`+
两套自建窗框删除、`ui_button`、`FrameBorder`/`BaseInterface`/`ControlPanel`/`ButtonsPanel`、`interface_radar`/
`status`/`icons`、`maps_tiles_render`+`GameArea`、`interface_focus`/`events`、`game_interface` 与
`game_startgame` 的鼠标处理、英雄与城堡对话框、`ui_font` 对拍）。剩余：

| 组 | 范围 | 状态 |
| --- | --- | --- |
| G6 | 照搬 C++ 的同步对话框路径（`LocalEvent::HandleEvents` + 各 `Dialog::Show`） | ⚠️ G6.1–G6.9 完成，**G6.9b 未完成**，见 §5.3 |
| J | 清理与回归：删自建件、迁测试、全量 `dotnet test` | ❌ 随 G6.9b 一起做 |

### 5.3 当前工序：G6.9b 收尾

`HumanTurn()` 的循环体（G6.9）已整条齐了，剩下的是把**场景**接到它上面：

| 步 | 内容 | 状态 |
| --- | --- | --- |
| 1 | 声卡可以是空的（`AdventureMap` 收 `AudioManager?`） | ✅ |
| 2 | `DrawFrame` → `RedrawInterface` | ✅（画面仍有 4 处缺陷，见 PROGRESS） |
| 3 | 滚动源 `AdventureMapView` → `GameArea` | ✅（`MapScrollController` 因编辑器仍在用而保留） |
| 4 | `Update` → `ProcessTurnFrame` + `FinishHumanTurn` | ✅ 拖拽/边缘卷屏、英雄动画、全部热键、按钮与状态石点击随之回来；`GameHost` 的键分发只剩两个浮层 |
| 5 | 输入三路（光标/左键/右键）→ `MouseCursorArea*` | ⬜ **下一步** |
| 6 | 删六份自建绘制（`MapFrame`/`IconBar`/`Minimap`/`ButtonsPanel`/`StatusPanelPainter` + 场景面板段） | ⚠️ 雷达、按钮面板、状态石三份已随第 4 步删除；余三份等第 5 步后收 |

第 4 步之后场景只剩一层薄壳，`AdventureMapView` 退到只给编辑器与 View World 用。落点见 `PROGRESS.md` §6.1b。

**G6.9b 之后立刻要还的账**（用户已同意放到最后统一修）：

1. 视野中心与英雄差约 1.5 格——先排除冒烟模式里 `LocalEvent.Current.CursorPosition` 默认 `(0,0)`
   落在边带造成的漂移，再对拍 `SetCenter(GetCenterOf(tile))` 与瓦片绘制原点；
2. 雷达全黑（`Radar.Build` 之后仍空）；
3. 地图上的零星条带；
4. 迷雾在端口里存了两份（`MapFog` 与 `Tile._fogColors`），`World.SyncTilesFog` 只是治标，合并成参考的单份；
5. 基线改为「与参考一致」，按参考渲染重新生成并在 `tests/baselines/README.md` 记明差异。

### 5.4 自建删除台账

已删：`Dialogs/FrameBoxChrome.cs`、`Dialogs/StandardWindowChrome.cs`（E-2，`Dialog::FrameBox` 与
`StandardWindow` 接管）、`Widgets/ItemBarLayout.cs`（G2，`ItemsBar` 接管）；冒险场景的雷达/按钮面板/状态石
三段（G6.9b 第 4 步）。在账：

| 端口文件 | 现在的样子 | 由谁接管 | 状态 |
| --- | --- | --- | --- |
| `Interface/StatusPanelPainter.cs` | 自算两态状态栏 | `interface_status.cpp` 的 `Interface::StatusPanel` | 场景已不用（第 4 步）；类型随第 6 步删 |
| `Scenes/AdventureMapScene.cs` 的面板段 | `MapFrame` 美术 + 诊断状态行 | `interface_*.cpp` | 待删（第 6 步） |
| `AdventureMapView` 的事件段 | 自建事件循环 | `interface_events.cpp` | 待删（第 5 步） |
| `Game/AdventureMap/MapScrollController.cs` | 自建拖动/边带/滚轮 | `GameArea::QueueEventProcessing` + `ScrollGameArea` | 冒险场景已不用；编辑器仍用，随 `EditorScene` 换输入再删 |
| `Scenes/MageGuildWindow.cs` | 自建窗口 | `ui_mage_guild.cpp` | 待删（H 之后核对） |

`Widgets/DialogButtonArtwork.cs`、`Widgets/ButtonArtwork.cs` 复核后**保留**（只是取图缝与 1:1 移植，
不含画法）。`Dialogs/` 下还有一批「未指名参考」的对话框（`ArenaDialog`、`TavernDialog`、`WellDialog` 等），
对应 `dialog/` 目录，按同一套流程处理，不在本表内。

## 6. 依赖方向

`Core` ← `Assets` ← `Rendering` ← `Game` ← `UI` ← `App`。反向引用一律不许（`Game` 不能引 `UI`，
所以 `MapScrollController` 这类共享件要么留在 `Game`、要么整体上移，不许两边各一份）。

## 7. 记账规则

* **每完成一个可验证单元**：`PROGRESS.md` 加一行/一节（做了什么、与参考的偏差、留下的异常、验证数字）。
* **每个跨文件的取舍**：`DECISIONS.md` 一条 ADR，编号接续。
* **计划变更**：改本文件，并在 `PROGRESS.md` 记一句「为什么改」。
* **验证三件套**：`dotnet build` 0 警告、`dotnet test` 全绿、相关冒烟（`--smoke-frames`）退出 0；
  像素相关的还要跑对应的 `tools/scripts/Verify*Baseline.ps1`。
* **压缩**：已完成的历史只留结论、偏差、遗留与数字，删过程叙述。
