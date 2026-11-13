# FHeroes2Sharp

[![CI](https://github.com/sevencat/FHeroes2Sharp/actions/workflows/ci.yml/badge.svg)](https://github.com/sevencat/FHeroes2Sharp/actions/workflows/ci.yml)

C# / .NET 10 / SDL3 版的《魔法门之英雄无敌 II》引擎重制，移植自
[fheroes2](https://github.com/ihhub/fheroes2)（C++，GPLv2）。

* 目标框架：**.NET 10**（`net10.0`），渲染后端 **SDL3 3.4（SDL3-CS 绑定）**，
  诊断文本使用 **SDL3_ttf**（FreeType，支持中文）。
* 游戏界面文本：原版 `FONT.ICN` / `SMALFONT.ICN` 位图字体（`BitmapFont`）；CJK 等 ICN 表外的字符由
  `TrueTypeGlyphSource` 补位，光栅化用纯托管的 `Sevencat.GlyphRaster`（内嵌文泉驿微米黑，见 ADR-019），
  两种字形共用同一套布局（`GlyphText`），由 `CompositeGlyphSource` 分工：位图字体定行高/advance，
  TrueType 只补 ICN 表没有的码位（ADR-021）。文字颜色不是第二套资源：白/黄/灰都是同一份字形过一遍
  `Core.Imaging.PaletteTable`（原版 `PAL::GetPalette` 的索引重映射表，ADR-020），所以中英文混排同色。
* 分辨率：内部逻辑分辨率固定 **640×480**，按整数倍放大并加黑边（默认 2x = 1280×960）。
* 策略：解析层对原版资源格式 **逐字节一致**，工程结构用现代 C# 分层重写。
* NativeAOT：**暂不作为支持目标**，但已开启 AOT / 裁剪分析器（`IsAotCompatible`，`IL2xxx`/`IL3xxx`
  为错误），代码保持「反射自由」以便将来切换（见 ADR-011）。

## 当前状态

**M0 已完成**：解决方案骨架、AGG 归档解析、调色板加载、640×480 索引帧缓冲、
整数倍呈现、SDL3_ttf 文本、诊断场景（含截图）、AssetProbe 工具。

**M1 已完成**：ICN 精灵与 TIL 图块的逐字节移植解码（含变换层与调色板转换表）、变换层感知的
`sprite` 贴图、单色光标与光标主题/热点、PNG 读写与 BMP 导出、逐像素比对工具（`AssetProbe compare`），
以及钉住真实资源解码结果的摘要测试（当前套件规模 Core 136 + Assets 120 + UI 16 = 272 个测试）。
导出结果已与原版 C++ 实现**逐字节对拍**：`tools/reference` 把引擎源码编成参考导出器，
`tools/scripts/CompareWithReference.ps1` 默认比对 14 个条目 / 1204 个 BMP，全部相同。

**M2 进行中（主菜单已可玩）**：`.mo` 本地化已落地（`Assets.Localization.MoCatalog` 读取 GNU MO 目录，
`translations/zh_CN.po` + `tools/scripts/BuildTranslations.ps1` 生成并安装
`<data>/files/lang/zh_CN.mo`，当前覆盖主菜单 / 设置 / 语言选择 / 快捷键 / 分辨率共 95 条，
未翻译项回退英文）。文字颜色也已落地：`Core.Imaging.PaletteTable`（原版 13 张索引重映射表）+
`BitmapFont.WithPalette` / `GlyphCoverageRamp.WithPalette`，白/黄/灰文字与 CJK 混排同色（ADR-020）。
界面文本接的是真实字体：`CompositeGlyphSource` 把 `FONT.ICN`/`SMALFONT.ICN` 与内嵌 CJK 字体合成，
`GameFonts` 在启动时装载（ADR-021）。**主菜单已经上屏**：`HEROES.ICN` 背景 + `BTNSHNGL.ICN` 五块招牌
（悬停/按下由 `UI.Widgets.Button` 处理，鼠标命中靠 `PresentationViewport.TryToLogical`），
底部 11 px 状态栏用 `SMALFONT.ICN` 画 `.mo` 译文与语言标记，`F2` 切换中英文（ADR-022）。
灯笼光晕也已接上：`SHNGANIM.ICN` 第 0 帧做灯体、第 1…39 帧每 250 ms 换帧（`Assets.Icn.IcnAnimation`）；
左下角的设置门在光标扫过时发光（`UI.Widgets.DoorHighlight`，画的是同一张表第 18 帧的下半部分）。
像素回归有了基线门禁：`tests/baselines/menu.png` 入库，`tools/scripts/VerifyMenuBaseline.ps1`
用「冻结动画 + 人造光标」渲染一帧后逐像素比对（状态栏的 FPS/版本号由 `compare --ignore` 排除，ADR-023）。
CI（`.github/workflows/ci.yml`）跑编译 + 全部测试，并调这个脚本做像素门禁：托管 runner 没有游戏数据，
只校验基线文件本身（PNG 魔数 + 640×480），像素比对留给装了数据的自建 runner（仓库变量
`FHEROES2_RUNNER` + `FHEROES2_DATA`）。
待办：语言选择对话框、BGM、设置对话框（门已经会发光，点它还没反应）。

## 快速开始

```powershell
# 需要本机已安装原版游戏数据（本项目不附带任何游戏资源）
dotnet build FHeroes2Sharp.slnx
dotnet test  FHeroes2Sharp.slnx

# 运行客户端（自动探测数据目录，也可显式指定）
dotnet run --project src/FHeroes2.Client.Desktop -- --data "F:\game\fh2"

# 主菜单默认上屏；按键：ESC 退出、F2 切换中英文、N/L/H/C/Q 选招牌、F11 全屏、F12 截图
dotnet run --project src/FHeroes2.Client.Desktop -- --data "F:\game\fh2" --lang zh_CN
dotnet run --project src/FHeroes2.Client.Desktop -- --data "F:\game\fh2" --scene diagnostics

# 渲染 40 帧后截图退出（CI / 回归用）；--cursor 定格 hover 态
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --screenshot out.png
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --cursor 230,210 --screenshot hover.png

# 资产归档检查
dotnet run --project tools/FHeroes2.AssetProbe -- info
dotnet run --project tools/FHeroes2.AssetProbe -- extract KB.PAL kb.pal

# 导出精灵 / 图块为 PNG（默认调色板 KB.PAL，可用 --palette 指定）
dotnet run --project tools/FHeroes2.AssetProbe -- sprites HEROES.ICN tools-output/heroes
dotnet run --project tools/FHeroes2.AssetProbe -- tiles GROUND32.TIL tools-output/tiles

# 导出单色光标；--format bmp 改成 8 bit 索引位图（--background 指定透明处的调色板索引）
dotnet run --project tools/FHeroes2.AssetProbe -- cursor ADVMBW01.BMP tools-output/cursor
dotnet run --project tools/FHeroes2.AssetProbe -- sprites ADVMCO.ICN tools-output/advmco --format bmp --background 23

# 逐像素比对两个 PNG 文件或两个目录（0 = 全同、3 = 有差异）
dotnet run --project tools/FHeroes2.AssetProbe -- compare tools-output/tiles tools-output/tiles-ref

# 与原版 C++ 实现逐字节对拍（需要 MSYS2 UCRT64 的 g++ 与一份 fheroes2 源码，见 docs/MISC.md）
pwsh tools/scripts/BuildReferenceTool.ps1
pwsh tools/scripts/CompareWithReference.ps1

# 主菜单像素基线门禁：渲染一帧与 tests/baselines/menu.png 逐像素比对（0 = 一致）
pwsh tools/scripts/VerifyMenuBaseline.ps1
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update      # 有意改了视觉后重建基线

# 编译语言包（translations/*.po → <data>/files/lang/*.mo，需要 GNU gettext 的 msgfmt）
pwsh tools/scripts/BuildTranslations.ps1
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN -NoInstall   # 只校验，不安装
```

数据目录解析顺序：`--data` → 环境变量 `FHEROES2_DATA` → 可执行文件目录（及其 `Data` 子目录）
→ 开发机常见安装位置（`F:\game\fh2` 等）。

## 目录结构

```
src/FHeroes2.Core        几何、随机数、二进制 IO、图像缓冲、日志（无外部依赖）
src/FHeroes2.Assets      AGG 归档、调色板/资源定位（依赖 Core）
src/FHeroes2.Rendering   呈现接口、文本抽象（依赖 Core）
src/FHeroes2.Game        游戏世界、回合、AI、存档（依赖 Core/Assets/Rendering）
src/FHeroes2.UI          对话框栈与控件（依赖 Game/Rendering/Core）
src/FHeroes2.Client.Desktop  SDL3 宿主、呈现、字体、场景
tests/…                  xunit 测试
tools/FHeroes2.AssetProbe    归档检查工具
tools/reference          参考引擎导出器（C++，链接 fheroes2 源码）
tools/scripts            调色板表生成、参考导出器编译、参考对拍、语言包编译、主菜单像素基线脚本
tests/baselines          入库的回归基线 PNG（menu.png + 说明），像素门禁的期望图像
translations/            语言包源文件（.po）；编译产物 .mo 安装到数据目录，不入库
docs/                    计划、进度、决策与参考笔记
```

依赖方向单向：`Core ← Assets ← Rendering`；`Game` 依赖 Core/Assets/Rendering，`UI` 依赖 Core/Rendering/Game；
客户端引用全部。任何反向引用都会破坏可测试性（`Core` 里不允许出现 SDL 或 XNA 类型）。

## 文档

* [docs/PLAN.md](docs/PLAN.md) — 里程碑计划
* [docs/PROGRESS.md](docs/PROGRESS.md) — 逐里程碑完成情况与验证记录
* [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — 分层、数据流、线程与资源模型
* [docs/DECISIONS.md](docs/DECISIONS.md) — 关键技术决策（ADR 风格）
* [docs/REFERENCE.md](docs/REFERENCE.md) — C++ 源码对照表（文件/类 → C# 类型）
* [docs/MISC.md](docs/MISC.md) — 构建约定、坑位记录、数据依赖测试说明
* [docs/OTHER.md](docs/OTHER.md) — 许可、资源归属、后续想法

## 许可

本项目为 GPLv2（与原版 fheroes2 一致），**不包含**任何原版游戏资源；
运行需要玩家自备正版《英雄无敌 II》。原版位图字体、`.agg`、`.h2d` 等资源永远不会进入仓库。
