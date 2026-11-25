# 架构（ARCHITECTURE）

> **2026-11-24 起**：`PLAN.md` 与 `PROGRESS.md` 已退役并从仓库删除，任务、进度与剩余工作以 **bd (beads)**
> 为准（`bd ready` / `bd list` / `bd stats`）。两份文档删除前的全文归档为 closed issue
> `FHeroes2Sharp-3so`（PLAN）与 `FHeroes2Sharp-jwk`（PROGRESS）；本文中出现的 `PROGRESS.md`/`§x` 引用都指那份归档。

## 分层

| 程序集 | 职责 | 允许依赖 |
| --- | --- | --- |
| `FHeroes2.Core` | 几何、随机数、二进制 IO、索引图像与调色板、精灵与变换层、重着色调色板表、PNG 导出/读回、BMP 导出、光标主题与热点、日志、计时、版本信息 | 无 |
| `FHeroes2.Assets` | AGG 归档、资源定位、资源聚合（base + 资料片）、ICN 精灵与帧动画、TIL 图块与单色光标解码、H2D 容器读出与写出（`H2D.H2DArchive` / `H2DWriter` / `H2DImageCodec`）、MP2 地图读出与写出（`Maps.Mp2Map` / `Mp2MapWriter`）、场景目录（`Maps.Mp2FileInfo` / `MapCatalog`） | Core |
| `FHeroes2.Rendering` | 呈现与文本的后端无关抽象 | Core |
| `FHeroes2.Game` | 冒险地图视图（`AdventureMap/`：`AdventureMapView`、`MapFrame`、`Minimap`、`IconBar`、`ButtonsPanel`，M4）；参考 `gui/` 里直接依赖 `Assets` 门面的那部分（`Gui/Text.cs` 排版层、`Fonts/` 字形派生、`Editor/`），因为门面在 `FHeroes2.Game`，放 `Rendering` 会形成反向引用；世界状态、回合、AI、存档从 M5 起逐步进来 | Core, Assets, Rendering |
| `FHeroes2.UI` | 对话框栈、控件（`Widgets.Button` / `Widgets.ButtonBase` / `Widgets.ButtonSprite` / `Widgets.ButtonArtwork` / `Widgets.ButtonGroup` / `Widgets.DoorHighlight` / `Dialogs.FrameBox` / `Dialogs.StandardWindow` / `Dialogs.MessageBox` / `Dialogs.SettingsDialog` / `Dialogs.LanguageSelectionDialog` / `Dialogs.ScenarioSelectionDialog`）、界面流转 | Core, Assets, Rendering, Game |
| `FHeroes2.App` | SDL3 宿主、呈现、字体、场景、命令行 | 全部 |

依赖方向单向；`Core` 不得引用任何 SDL 类型，这样解码与算法可以在无 GPU 的测试进程里跑。

## 帧数据流（M0 已贯通）

```
数据目录解析  AssetLocator            --data / FHEROES2_DATA / exe 目录 / 已知安装位置
      ↓
归档读取      AssetArchive            heroes2.agg(1434) + heroes2x.agg(52)，逐条名称哈希校验
      ↓
调色板        Palette.FromGamePalette KB.PAL：6 bit → <<2，并复制水/岩浆循环色
      ↓
场景绘制      ScreenBuffer(640×480, byte) + ImageOps    纯 CPU、索引色、无 GC 分配
      ↓
脏区上传      SdlPresenter.Upload      uint[256] 查表（IndexedFrameConverter）→ SDL_UpdateTexture(脏矩形)
      ↓
逻辑合成      640×480 目标纹理        帧 + 可选文本叠加（只有诊断屏用 SDL3_ttf，1x 逻辑像素）
      ↓
呈现          Nearest 采样 + 整数倍放大 + 居中黑边 → 窗口
```

放大的数学在 `PresentationViewport.Compute` 中：窗口 ≥ 640×480 时取整数倍并居中；
窗口更小时退化为等比缩放（浮点），保证不裁切；`TryToLogical` 是它的逆运算，
鼠标坐标靠它从窗口空间回到 640×480（落在黑边上返回 `false`）。

画面由 `Scenes.IScene` 描述：`DrawFrame(in DiagnosticsState)` 画索引内容（背景、招牌、文字），
`DrawOverlay(in DiagnosticsState)` 画 GPU 覆盖层，`Update` 推进动画。`MainMenuScene` 的
`DrawOverlay` 是空实现——它的文字全部是索引像素；`DiagnosticsScene` 反过来只用覆盖层显示状态。

## 组合根与生命周期

`GameHost` 是唯一的组合根，同时托管 SDL 事件循环：

```
Run()     → SDL_Init(Video) / TTF_Init()；打开 AssetBundle（失败则记错误、ExitCode=1、使用灰度调色板继续）
            创建 SdlPresenter（窗口 + 渲染器 + 流式帧纹理 + 640×480 目标纹理）、SdlTextRenderer
            CreateScenes()：DiagnosticsScene 总是建好，--scene menu 时再建 MainMenuScene（缺资源则为 null），
                            --scene editor 时直接建 EditorScene（ADR-040，没有菜单就在它后面）；
                            EditorScene 持有 MapEditorView（光标/网格）与 MapEditSession（笔刷/笔划/撤销，
                            ADR-041），两个都在 Game 层、都能脱离窗口测试；
                            场景选择确认后由 ConfirmScenario() 读地图、追加 AdventureMapScene（M4）
Loop()    → PollEvent（ESC 退出、F2 切语言、F11 全屏、F12 截图、鼠标移动/按键 → 当前 IScene、窗口关闭）、
            FrameTimer、场景动画、smoke 帧数控制
Draw()    → 组装 DiagnosticsState → 场景绘制 → 上传脏区 → 绑定目标纹理 → 帧纹理 1:1 → 覆盖层
            → 截图 → 放大呈现 → RenderPresent
Dispose() → 逆序释放全部 IDisposable（文本、呈现器、归档、TTF、SDL）
```

不使用 DI 容器；依赖默认在构造函数中显式传入。唯一的例外是参考实现里做成单例的那批状态，
它们走静态门面（见下一节），门面的 `Current` 只在组合根赋值。

## 全局状态与门面（移植参考的单例）

参考实现用单例和命名空间函数携带进程级状态，量出来是这样：

```
全局变量      3 个   World & world、logMutex、logFile
单例类型     10 个   Settings / World / Cursor / AdventureMap / EditorInterface /
                     BattlePlanner / Planner / Result / LocalEvent / CampaignSaveData
::Get() 调用 832 处  业务层约 800 处：gui 150、game 136、battle 122、dialog 117、
                     editor 85、heroes 78、ai 41、kingdom 23、castle 21、world 17 …
```

函数级 1:1 移植（ADR-057）要把这 832 处逐行搬过来，靠的就是门面。规矩只有四条：

| 规则 | 说明 |
|---|---|
| 禁止可变 `static` 字段 | 静态成员只能是常量表或无状态纯函数（`MapSizes`、`PngWriter`、`GlyphText`）；状态必须挂在实例上 |
| 只允许 `static X Current { get; set; }` | `Settings::Get().foo()` → `Settings.Current.Foo`，与参考逐字对应，grep 对照时不需要翻译 |
| `Current` 只在组合根赋值 | `GameHost` 建场景 / 切场景 / 读档时赋值，业务代码只读 |
| 数据不是服务 | `World`、`EditorInterface`、`BattlePlanner` 是会话数据，`Current` 指向有 owner 的实例；不给它们造 `IWorldProvider` 之类的接口 |

第二版「改成 DI」的工作量因此被压到这两处：换 `Current` 指向的对象（或者把 `Current` 的 getter
换成容器解析）。832 个调用点不用动。

现有符号的对应关系：

| 参考符号 | C# | 状态 |
|---|---|---|
| `World & world` | `Game.World.Current`（`WorldMap`） | 待建：PLAN「还差的主干」第 3 条，随 `world.cpp` / `world_loadmap.cpp` 一起进来（M5） |
| `Settings::Get()` | `Game.Settings.Current`（`SettingsData`） | 待建；`CommandLineOptions` 目前只覆盖命令行开关 |
| `Cursor::Get()` | `CursorState.Current`（`Game.Cursor`） | 状态机已就位；`ICursorRenderer` 的 App 实现（软件光标）待补 |
| `LocalEvent` / `CampaignSaveData` | 同名门面 | 待建：键鼠事件现在由 `GameHost` 直接分派给场景；战役解析尚未开始 |
| `AdventureMap::Get()` | `App.AdventureMapScene`（实例，构造传参） | 已有；参考的 `redraw()` 对应场景的脏标记 |
| `EditorInterface::Get()` | `App.EditorScene`（实例） | 已有 |
| `BattlePlanner` / `Planner` / `Result` | — | 未移植：M7，要等 `army` 与 `castle` 落定（PLAN「还差的主干」第 4 条） |
| `AGG::` 命名空间 | `Assets.AssetBundle`（实例，组合根持有） | 已有；打开归档、读条目 |
| `Assets::getImage( ICN, frame )` | `Game.Assets.Current.GetImage( "CELLWIN.ICN", 2 )` | 已建；解码结果按 (文件, 帧) 缓存，换 `Current` 时清空 |
| `AudioManager::` | `Game.Audio.AudioManager`（实例，注入 `IAudioDevice`/`IAudioSource`） | 已有（ADR-140）；`App.Audio.AudioPlayer` 仍是独立的 SDL3_mixer 实现，未换成它 |
| `RNG::` | `Core.Random.Pcg32`（实例） | 已有 |
| `logMutex` / `logFile` | `Core.Diagnostics.GameLog`（静态，自带锁） | 已有；唯一可变静态是 `MinimumLevel`，收进门面前先别再加别的 |
| `Artifact::artifactGlobalStatus[]` | `Game.Artifacts.ArtifactRandom`（静态位图） | 已有（ADR-142）；每个 id 两位（已发/禁用），`Reset()` 全清；进程级状态，所以碰它的测试放进 `DisableParallelization` 集合 |

门面不是万能的：已经有实例传参的地方（`AudioPlayer`、`Pcg32`、两个场景）不要为了「统一」再套一层
门面——它们没有 832 处调用点要照顾，多一层只是多一个全局。`AssetBundle` 与 `Assets` 是两个东西：
前者是「打开哪个归档」（实例，组合根持有、构造传参），后者是参考的 `Assets::getImage`（全局取图 + 缓存），
所以后者是门面、前者不是。

测试注意：静态门面在 xUnit 里是进程级共享状态，动到门面的测试要挂进同一个 collection（或
`[CollectionDefinition(DisableParallelization = true)]`），否则并行跑会互相改写 `Current`。

## 移植粒度与冲突处理（ADR-057）

参考实现（快照 commit `6c51acfdc`，见 `REFERENCE.md`）与已有代码不一致时，按下面的顺序判定。
不存在「两套实现并存」，也没有「运行时开关切回自研」这种状态——冲突处理完，旧代码就删掉。

| 冲突类型 | 处理 | 例子 |
|---|---|---|
| 常量、坐标、帧号、文件格式 | **以参考为准**，改掉旧值并删除写过它的代码 | 编辑器拾取区写死 124，按 `instrumentPanelHeight` 应为 140（ADR-056） |
| 参考有、端口暂时画不出 | **占位但不生效**，不自己发明交互 | 九宫格工具按钮只做三个可点，其余 `ToolAt` 返回 `null`（ADR-049/056） |
| 结构、语言层面的差异 | **允许**，但不得为了「更像参考」而变得更差 | 实例 + 构造传参替代单例（ADR-057 第 4 条）、不可变类型替代原地改写、不引入指针与宏 |
| 参考行为本身要妥协 | 在 ADR 里写明这是一处偏离、偏了什么、为什么 | `SettingsDialog` 的选项改成 OK 时一次性应用，参考是即刻生效（ADR-027） |

三条判定依据：

1. **粒度是函数级**：同名、同顺序、同常量，`updateTerrainTransitionOnTile` /
   `makeInstrumentPanelBackground` 这类名字直接沿用，grep 两边能对上；行级 1:1 已明确否决（ADR-057 后果末条）。
2. **改粒度要两处一起改**：本节与对应 ADR 的「后果」（原 `PLAN.md` 的「总体原则」随 PLAN 退役一起冻结在
   归档 `FHeroes2Sharp-3so` 里，不再改动）。
3. **对拍是前提**：没有基线截图、逐字节比对或单测的界面不动——否则「按参考重画」只是换一种猜法。

回退靠分支而不是开关：大改造先打备份点再开分支（`backup/pre-facade-2026-11-14`、
`refactor/global-facade`），`main` 始终可运行。小改动由 ADR 的「背景 — 决策 — 后果」三段留痕：
背景写谁发现的、拿什么对的（截图、用户指出、参考行号），决策写参考出处，后果写基线变化。

编辑器是这套规则的样板：ADR-048（黑卡写字）→ ADR-049（照参考格子画、对象选择器退回名字表）→
ADR-050（笔刷行占掉名字条）→ ADR-054/056（整块按参考重画，删掉五行文字、拾取区改由舱高推导）。
至今仍与参考不同的只剩：对象拾取器是名字表（对象目录未移植，PLAN「还差的主干」第 1 条）、
六个工具按钮占位、没有悬停反馈、`EditorPanel` 在 `App` 而非 `Game`。

## 移植注释

从参考搬过来的类型与函数，注释里写清出处。行号对应 `REFERENCE.md` 记的版本（`version.txt` = `1.1.18`，
commit `6c51acfdc`），路径相对参考仓库根，固定以 `参考：` 起头，方便 grep：

```csharp
/// <remarks>参考：src/fheroes2/gui/ui_tool.h:48  class MovableSprite</remarks>

/// <summary>...</summary>
/// <remarks>参考：src/fheroes2/editor/editor_ui_helper.cpp:36  Editor::Checkbox::Checkbox</remarks>
```

* 类写 `h:行`（声明，连同里面顺手搬的小函数）与 `cpp:行`（实现）；函数写 `cpp:行`。
* **行号不是主键，函数名才是**：上游升级后行号会漂，找的时候先按名字找，行号只帮着定位。
* 只搬了一半的写清缺什么（比如「要 `Text::fitToOneRow`」），别让后来的人以为是漏抄。
* 和参考有出入的地方，同一段里以 `差异：` 起头，写明改了什么、为什么（理由进 ADR）。
* `REFERENCE.md` 的「已移植」表同步加行，表里只写文件级对应，函数级对应留在代码注释里。

## 文本

`ITextRenderer` 只暴露 `Name` 与 `Measure(string, TextStyle)`，M0 实现为
`SdlTextRenderer`（SDL3_ttf / FreeType，逻辑字号 12 px）。
绘制入口 `Draw(text, Point, TextStyle)` 属于客户端层，因为它需要 GPU 上下文：每段文字生成一张
混合纹理，按 (文本, 字号, 颜色) 缓存，整帧未被使用的缓存在下一帧释放，所以变化的 FPS 行不会让缓存无限增长。

字体解析顺序：`FHEROES2_FONT` → `assets/fonts/*.ttf|otf` → Windows 系统字体
（`simsun.ttc` → `simhei.ttf` → `msyh.ttc` → `arialuni.ttf` → `segoeui.ttf` → `arial.ttf`）。
SimSun 在 12 px 有内嵌点阵，小字号下比纯轮廓字体锐利得多；想要硬边像素可设
`FHEROES2_TEXT_CRISP=1`，把覆盖率量化为 0/255（见 ADR-017）。
M2 加入 `Assets.Fonts.BitmapFont`（`FONT.ICN`/`SMALFONT.ICN` 精灵表，`BitmapFontSize.Small|Normal`；
`BitmapFontSize.Large` 它仍然拒绝。大号以及黄/灰/白/渐变这几种面统一由 `Game.Gui.FontGlyphTable` 从
`FONT.ICN` 派生：放大两倍 + `PAL::GetPalette` 重着色 + `generateGradientFont`（ADR-018/020），
派生面按（归档包, 字号, 颜色）缓存；布局与绘制都产出 8 bit 索引像素，因此与调色板/变换层天然一致。
CJK 或 `U+00FF` 以上字符由 `Assets.Fonts.TrueTypeGlyphSource` 补位：`Sevencat.GlyphRaster`（纯托管、
内嵌文泉驿微米黑）做光栅化，覆盖率经 `GlyphCoverageRamp` 量化到 `KB.PAL` 的灰阶（ADR-019）。
两个 provider 都实现 `IGlyphSource`，`GlyphText` 负责唯一的布局实现：`Measure` / `DrawLine` /
`DrawMultiline` / `WrapLines`，换行、居中、裁剪、尾随空格规则因此只有一份。
UI 实际用的是 `Assets.Fonts.CompositeGlyphSource`：位图字体是 primary，决定
`LineHeight`/`SpaceWidth`/拉丁字符，TrueType 只在 primary 不认识的码位上补字形，
两边都不认识时退回位图的 `?`（ADR-021）。启动时由
`App.Fonts.GameFonts.TryLoad(bundle)` 一次性装好 `Small`/`Normal`/`Yellow`
三个字形源并把结果交给场景，所以绘制点只拿 `IGlyphSource`，不需要知道字符范围。
绘制走索引路径：`GlyphText.DrawLine` 写进 `ScreenBuffer`，颜色由调色板决定，上屏时再按整数倍放大
（ADR-003）；SDL_ttf 后端只剩诊断覆盖层（`SdlTextRenderer`）。
主菜单（`MainMenuScene`）是这条路径的第一个真实用户：招牌取自 `BTNSHNGL.ICN`，底部 11 px 状态栏
用 `GameFonts.Small` 画 `.mo` 译文与语言标记，中英文共用一张调色板。界面语言由
`App.Localization.GameText` 提供（一个语言一个不可变实例，英文 = msgid 本身），
`F2` 在可用语言间循环；字体没有 CJK 字形时只保留英文（ADR-022）。
颜色不是第二套字形资源：原版只有一份 `FONT.ICN`，黄色/灰色文字是把它的索引过一遍
`Core.Imaging.PaletteTable`（`PAL::GetPalette` 的 13 张重着色表）得到的。
`BitmapFont.WithPalette(PaletteId.YellowFont)` 重着色位图字体，
`GlyphCoverageRamp.WithPalette(...)` 把同一张表套到覆盖率梯度上，于是混排里的中文与英文同色（ADR-020）。
若最终字体不含 CJK，诊断文本自动切换为英文。

## 对话框：`BUYBUILD.ICN` 消息框

M2 的右键提示框是第一个真正的对话框，它由三个类型拼出来（ADR-024）：

```
Dialogs.FrameBoxChrome   BUYBUILD.ICN 的六块（右列 0/1/2，左列 4/5/6）+ 每列最大宽度
        ↓
Dialogs.FrameBox         版式 = Dialog::ResizableFrameBox：99 px 顶 + 35 px 中段切片 × n + 81 px 底；
                         给出 Area（244 宽的文字区，最小 70 高）与 FrameArea（含画了哪些像素）
        ↓
Dialogs.MessageBox       版式 = getDialogHeight() + showMessage(..., Dialog::ZERO)：
                         yellow 标题 + normal 正文，都按 244 px 折行居中，标题上/正文上各留 10 px
        ↓
MainMenuScene            右键按在招牌或门上时创建，松开就丢弃；画在状态栏之后（模态）
```

`FrameBox` 与 `MessageBox` 不碰输入：谁在什么时候创建它们由场景决定，这与参考实现
「对话框自己跑事件循环」的差别是**故意**的——端口里一帧只画一次、没有阻塞式循环，
所以对话框只是一个每帧重画的对象。切片、列宽、最小高度全部来自 C++ 的算术，
因此一像素都不需要猜：`FrameBoxTests` / `MessageBoxTests` 固定了 640×480 下的
`198,205 244×70` 与 `159,141 305×180` 这些数。

## 对话框按钮：`ui_button.cpp`

设置、确认这类对话框底部的按钮走另一套控件（`fheroes2::ButtonBase` / `ButtonSprite` /
`ButtonGroup`），主菜单招牌用的 `Widgets.Button` 是另一回事：那块逻辑在参考实现里写在
`game_mainmenu.cpp` 里（悬停换 `+1` 帧、按下换 `+2` 帧），所以端口保留了一个带 hover 状态的
`Button`。

```
Widgets.ButtonBase     偏移 + 两块命中区（released / pressed）；Press()/Release() 在禁用时不生效；
                       禁用外观 = released 过 PAL::DARKENING（PaletteId.Darkening），按精灵缓存
        ↓
Widgets.ButtonSprite   released + pressed + 可选 disabled 精灵；setSprite 后重算命中区
        ↓
Widgets.ButtonGroup    按钮 ↔ Dialogs.DialogButton 返回值：MouseClickLeft 规则（按下与松开都在区内
                       才算点击）、按住时只照亮"按下发生在它身上"的那个按钮、两个热键
```

输入模型和对话框本身一样是「每帧重画」的：`SetCursor(Point?)` + `SetLeftButton(bool)` 收事件，
`Draw(PixelBuffer)` 出门。参考实现的 `LocalEvent` 轮询、长按（`MOUSE_LONG_PRESS`）与
`ActionObject` 观察者都没移植，所以 `processEvents()` 被拆成
`SetCursor` / `SetLeftButton` / `ProcessHotKey` 三个返回值明确的入口；`ButtonGroupTests` 钉住了
「按下在 A、松手在 B 不算点击」「拖到邻居不会点亮它」「单个 OK/CANCEL 按钮两个热键都能关」
这些语义。

版式与美术：`ButtonGroup.CreateStandard( area, DialogButton.Ok | DialogButton.Cancel,
artwork )` 是 `ButtonGroup( area, types )` 的自动摆位（一对按钮左右各留四分之一空白，
单个按钮居中贴底），美术由调用方以 `IDialogButtonArtworkSource` 注入，所以 UI 层不认 ICN 编号，
测试用一块纯色精灵就能钉住坐标。文字按钮的精灵侧
（`Widgets.ButtonArtwork`：`resizeButton()` 的三等分/五等分拉伸、`addButtonShine()` 的像素表、
`makeTransparentBackground()`）已经移植并用合成精灵逐像素固定；标签部分要
`BUTTON_GOOD_FONT_RELEASED` 这类按钮专用字体表，尚未移植，所以当前对话框的 OK 直接用
`SYSTEM.ICN` 的第 1/2 帧。

设置对话框 `Dialogs.SettingsDialog` 是这些构件的第一个调用方：一个 `StandardWindow`、一行一个
`SettingsOption`（左标签 + 右对齐的值 + 两侧自绘箭头，点左半格/右半格换值）、底部
`CreateStandard` 摆出的 OK。箭头不是美术而是像素线（`ChevronWidth` 4、高 7、色号 10）：
`FONT.ICN` 自带的 `<` / `>` 只有 3×4 像素，行里几乎读不出来，而 `OptionButtonGroup` 的箭头贴图
还没移植。窗口本身是模态的，归主菜单持有（`MainMenuScene.Settings`），点街景左下角的门时创建，
Escape 取消、OK/Enter 应用（ADR-027）。这一帧同时是入库的第二份像素基线
（`tests/baselines/settings.png`，门禁见 ADR-028）。`ImageOps` 为此补了四个写双层的原语（`Copy` /
`FillSprite` / `SetPixel` / `DrawLine`），它们写颜色时把 transform 字节清 0，因此拼装出来的精灵
可以直接交给 `BlitSprite`。

## 地图文件头（M3 的起点）

场景列表不需要地图内容，只需要原版写进 MP2 文件最前面 428 字节的那份「名片」。这段读取属于资源层：

```
Assets.Maps.Mp2FileInfo.Read(path)     428 字节信息块 → 名字/描述/宽度/高度/难度/
                                        三组六色(王国·人类·电脑)/初始种族/胜负条件与参数
Assets.Maps.MapCatalog.Scan(roots, n)  找 maps 目录 → TryRead → 按 n 名人类玩家过滤/收窄 → 按场景名排序
AssetProbe maps                        把结果打成一张表（含被跳过的文件与「延迟改写」计数）
```

目录规则与 `Settings::FindFiles("maps", …)` 一致，但多了一条：只有当前目录真的是数据目录
（含 `Heroes2.agg`）才去它的父级找 `Maps/`，否则用户顺手指一个临时目录就会去扫无关位置。
`Mp2FileInfo` 不假装自己是一张地图（ADR-032）：tile / addon / 英雄位置由 M4 的地图加载器
（`Assets.Maps.Mp2Map`）读走，对象身份仍取自 tile 里的 `mapObjectType` 字节——对象目录
（参考 `maps/map_object_info.cpp`）还没移植，是 PLAN「还差的主干」第 1 条，也是拾取与信息框的前置。

## 场景选择窗口（M3 的第一个界面）

清单有了消费者：`UI.Dialogs.ScenarioSelectionDialog` 是参考 `Dialog::SelectScenario` 的等价物，
和设置对话框一样归主菜单持有（`MainMenuScene.Scenario`），但它是**纯逻辑 + 自绘**：

```
UI.Dialogs.ScenarioDialogArtwork  面板/阴影/17x17 图标/滑块/OKAY 帧 + 五张尺寸按钮的矩形
UI.Dialogs.ScenarioDialogText     难度、胜利、失败三类译文（msgid 与参考一致，交给 .mo）
UI.Dialogs.ScenarioSelectionDialog  过滤后的列表、9 行视口、滚动、详情区、OK
App.Scenes.ScenarioArtwork        ICN 名字 → 上面那份美术（ICN 编号只在这一层出现）
```

布局常量逐条抄 `dialog_selectscenario.cpp`（列表 55,55 270×175、行内图标 48/67/86/104、
胜负 266/285、详情 42,318 292×90、OKAY 140,410），尺寸按钮是 `REQUESTS.ICN` 9…13 的
17×17 图标按 62 px 步长摆开。`MAP_TYPE_ICON` 在归档里不存在：参考在启动时把 `ARTFX.ICN`
6/90/101 缩放到 15×15 再放进 17×17 的透明格，`ScenarioArtwork` 做同一件事，所以 UI 层仍只
收「三张 17×17 精灵」，不认识 ARTFX 编号。

模态输入由主菜单转发：`SetCursor` / `SetLeftButton` / `Select(char)`（Enter、Escape），
方向键与 PageUp/PageDown/Home/End 不是字符，走 `MoveScenarioSelection` / `MoveScenarioPage` /
`SelectFirst…` / `SelectLast…`，由 `GameHost.HandleKeyDown` 在窗口打开时接管。列表是 `MapCatalog.ReadAll` 的结果再按尺寸过滤，
惰性读一次、整个会话复用。
确认后 `ConfirmScenario()` 把 `Mp2FileInfo` 交回 `GameHost`，后者按它读地图并建 `AdventureMapScene`
（M4 的入口；ADR-033 说的「只记日志」到此为止）。


## 冒险地图（M4）

地图加载在资源层，视图与界面构件在 `FHeroes2.Game`，场景只做拼装：

```
Assets.Maps.Mp2Map.Read(path)       tile / addon / 英雄位置（Mp2MapHero 已带颜色）
      ↓
Game.AdventureMap.AdventureMapView  地形 → 四层地面对象（TERRAIN 3 / BACKGROUND 2 / SHADOW 1 / OBJECT 0）
                                    → 怪物 → 船 → 英雄 → 顶部对象；按层遍历整个视口，不按 tile
                                    （ADR-036/037/059/060）
      ↓
Game.AdventureMap.MapFrame          ADVBORD.ICN 外壳：448×448 视口、雷达舱、面板舱；几何只此一处
                                    （ADR-054），视口先画进独立缓冲再贴，脏区随之收敛
      ↓
Game.AdventureMap.Minimap           tile 字段直接着色（ADR-038），落点取 MapFrame.Radar
Game.AdventureMap.IconBar           LOCATORS 帧的城堡图标条（ADR-055）
Game.AdventureMap.ButtonsPanel      八格按钮面板，禁用走 DARKENING、缺帧留空（ADR-058）
      ↓
Game.AdventureMap.MapFog            CLOF32.TIL 整瓦 + CLOP32.ICN 边界件，压在地图之后、界面之前（ADR-061）
      ↓
App.Scenes.AdventureMapScene        滚动、状态栏、雷达拖拽、每帧脏标记（ADR-042）
```

状态的持有者是场景与 `MapFrame`，不是参考的 `Interface::AdventureMap` 单例：舱位矩形、滚动位置、
雷达抓取都是实例字段，所以这一层能脱开窗口单测。资源栏、路径预览、拾取与信息框、雾的增量揭示
都还没进来——它们的前提是 `world` 与对象目录，见已归档 PLAN 的「还差的主干」（`bd show FHeroes2Sharp-3so`）。

## 编辑器（E1–E3b）

`--scene editor` 直接进 `App.Scenes.EditorScene`，状态放在 `Game.Editor`：

* `MapEditorView` 管光标、网格与笔刷轮廓，`MapEditSession` 管笔刷、笔划、撤销栈与保存，两者都不碰窗口。
* 保存是「只改 tile 区」的原地补丁，新建地图才走全量写出（ADR-043/044）；对象放置抄真实地图的预设，
  add-on 只增不回收（ADR-045）；地形接缝改一格要动九格（ADR-046）。
* 外壳、面板、按钮阵列与属性框都照参考坐标摆（ADR-048…056），对象名与怪物名取参考的字符串表（ADR-053）。
* 未做的是规格窗口那一批（事件、谣言、狮身人面像、随机地图生成器），列在已归档 PLAN 的 `editor` 未转清单里
  （`bd show FHeroes2Sharp-3so`）。

## 错误处理

* 外部数据格式错误 → `BinaryFormatException`（`Core.Io`），消息包含文件名、条目名与偏移。
* 归档缺失/损坏 → `AssetBundle.Open` 抛 `DirectoryNotFoundException` / `FileNotFoundException`，
  客户端降级运行并设置 `ExitCode = 1`；资料片损坏只记录警告（与原版行为一致：资料片可选）。
* 日志 → `GameLog`（`FHEROES2_LOG_LEVEL` 可调），`Warning` 及以上立即 flush。

## 线程模型

M0 全单线程。计划中的并行点：音频流播放（独立线程 + 环形缓冲）、
资源解码（`Parallel.For` 解码 ICN 图集）、AI 评估（不可访问渲染状态）。
所有 `AssetArchive` 的读取都经过内部锁，允许后续从其他线程读取原始字节。
