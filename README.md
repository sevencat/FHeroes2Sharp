# FHeroes2Sharp

C# / .NET 10 / MonoGame 版的《魔法门之英雄无敌 II》引擎重制，移植自
[fheroes2](https://github.com/ihhub/fheroes2)（C++，GPLv2）。

* 目标框架：**.NET 10**（`net10.0`），渲染后端 **MonoGame 3.8.6 (DesktopGL)**，
  字体使用 **FontStashSharp**（TrueType，支持中文）。
* 分辨率：内部逻辑分辨率固定 **640×480**，按整数倍放大并加黑边（默认 2x = 1280×960）。
* 策略：解析层对原版资源格式 **逐字节一致**，工程结构用现代 C# 分层重写。
* NativeAOT：**暂不作为支持目标**，但已开启 AOT / 裁剪分析器（`IsAotCompatible`，`IL2xxx`/`IL3xxx`
  为错误），代码保持「反射自由」以便将来切换（见 ADR-011）。

## 当前状态

**M0 已完成**：解决方案骨架、AGG 归档解析、调色板加载、640×480 索引帧缓冲、
整数倍呈现、FontStashSharp 文本、诊断场景（含截图）、AssetProbe 工具、108 个单元/集成测试。

**M1 进行中**：ICN 精灵与 TIL 图块的逐字节移植解码（含变换层与调色板转换表）、变换层感知的
`sprite` 贴图、PNG 导出与读回、逐像素比对工具（`AssetProbe compare`），以及钉住真实资源解码结果的摘要测试。

## 快速开始

```powershell
# 需要本机已安装原版游戏数据（本项目不附带任何游戏资源）
dotnet build FHeroes2Sharp.slnx
dotnet test  FHeroes2Sharp.slnx

# 运行客户端（自动探测数据目录，也可显式指定）
dotnet run --project src/FHeroes2.Client.Desktop -- --data "F:\game\fh2"

# 渲染 40 帧后截图退出（CI / 回归用）
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --screenshot out.png

# 资产归档检查
dotnet run --project tools/FHeroes2.AssetProbe -- info
dotnet run --project tools/FHeroes2.AssetProbe -- extract KB.PAL kb.pal

# 导出精灵 / 图块为 PNG（默认调色板 KB.PAL，可用 --palette 指定）
dotnet run --project tools/FHeroes2.AssetProbe -- sprites HEROES.ICN tools-output/heroes
dotnet run --project tools/FHeroes2.AssetProbe -- tiles GROUND32.TIL tools-output/tiles

# 逐像素比对两个 PNG 文件或两个目录（0 = 全同、3 = 有差异）
dotnet run --project tools/FHeroes2.AssetProbe -- compare tools-output/tiles tools-output/tiles-ref
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
src/FHeroes2.Client.Desktop  MonoGame 宿主、SDL 呈现、字体、场景
tests/…                  xunit 测试
tools/FHeroes2.AssetProbe    归档检查工具
docs/                    计划、进度、决策与参考笔记
```

依赖方向单向：`Core ← Assets ← Rendering`；`Game` 依赖 Core/Assets/Rendering，`UI` 依赖 Core/Rendering/Game；
客户端引用全部。任何反向引用都会破坏可测试性（`Core` 里不允许出现 MonoGame 类型）。

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
