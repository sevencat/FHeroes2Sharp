# 架构（ARCHITECTURE）

## 分层

| 程序集 | 职责 | 允许依赖 |
| --- | --- | --- |
| `FHeroes2.Core` | 几何、随机数、二进制 IO、索引图像与调色板、精灵与变换层、重着色调色板表、PNG 导出/读回、BMP 导出、光标主题与热点、日志、计时、版本信息 | 无 |
| `FHeroes2.Assets` | AGG 归档、资源定位、资源聚合（base + 资料片）、ICN 精灵与帧动画、TIL 图块与单色光标解码 | Core |
| `FHeroes2.Rendering` | 呈现与文本的后端无关抽象 | Core |
| `FHeroes2.Game` | 世界状态、回合、AI、存档 | Core, Assets, Rendering |
| `FHeroes2.UI` | 对话框栈、控件（`Widgets.Button` / `Widgets.ButtonBase` / `Widgets.ButtonSprite` / `Widgets.ButtonGroup` / `Widgets.DoorHighlight` / `Dialogs.FrameBox` / `Dialogs.StandardWindow` / `Dialogs.MessageBox`）、界面流转 | Core, Assets, Rendering, Game |
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
            CreateScenes()：DiagnosticsScene 总是建好，--scene menu 时再建 MainMenuScene（缺资源则为 null）
Loop()    → PollEvent（ESC 退出、F2 切语言、F11 全屏、F12 截图、鼠标移动/按键 → MainMenuScene、窗口关闭）、
            FrameTimer、场景动画、smoke 帧数控制
Draw()    → 组装 DiagnosticsState → 场景绘制 → 上传脏区 → 绑定目标纹理 → 帧纹理 1:1 → 覆盖层
            → 截图 → 放大呈现 → RenderPresent
Dispose() → 逆序释放全部 IDisposable（文本、呈现器、归档、TTF、SDL）
```

不使用 DI 容器；所有依赖在构造函数中显式传入。

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
大字号由 `FONT.ICN` 运行时放大，见 ADR-018），布局与绘制都产出 8 bit 索引像素，因此与调色板/变换层天然一致。
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

版式（`ButtonGroup( area, Dialog::OK | Dialog::CANCEL )` 那种自动摆位）依赖 `ui_button.cpp` 里
把文字画到按钮上的工厂函数，等设置对话框时一起移植，现在由调用方按自己的美术坐标摆放。


## 错误处理

* 外部数据格式错误 → `BinaryFormatException`（`Core.Io`），消息包含文件名、条目名与偏移。
* 归档缺失/损坏 → `AssetBundle.Open` 抛 `DirectoryNotFoundException` / `FileNotFoundException`，
  客户端降级运行并设置 `ExitCode = 1`；资料片损坏只记录警告（与原版行为一致：资料片可选）。
* 日志 → `GameLog`（`FHEROES2_LOG_LEVEL` 可调），`Warning` 及以上立即 flush。

## 线程模型

M0 全单线程。计划中的并行点：音频流播放（独立线程 + 环形缓冲）、
资源解码（`Parallel.For` 解码 ICN 图集）、AI 评估（不可访问渲染状态）。
所有 `AssetArchive` 的读取都经过内部锁，允许后续从其他线程读取原始字节。
