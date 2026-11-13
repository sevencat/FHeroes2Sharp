# 技术决策（DECISIONS）

## ADR-001：现代化重构，而非逐行翻译

**决策**：C++ 的**资源解析**逐字节对齐；**工程结构**用现代 C# 重写（分层、不可变几何类型、
可测试、显式依赖）。

**理由**：逐行翻译会继承 C++ 的宏、全局状态与 `goto` 式控制流，在 C# 中既难维护也无法测试。
外部文件格式是唯一必须完全一致的边界，其余部分是自由度。

**影响**：C# 类型与 C++ 类不一一对应；对照关系记录在 `REFERENCE.md`。

## ADR-002：保留 8 bit 索引色管线

**决策**：内部帧缓冲仍是 `byte[]` + 256 色调色板；调色板查表只在呈现前发生一次。

**理由**：原版所有资源都是索引色，并且变换层（palette transform 6–13）语义依赖调色板索引；
提前转 RGBA 会丢失索引、放大约 4 倍内存带宽，并让「变色/闪烁/施法高亮」等效果实现变复杂。

**代价**：CPU 每帧做一次查表转换（640×480 ≈ 30 万次，M0 实测可忽略）；后续可用着色器做调色板查找。

## ADR-003：逻辑渲染目标 + 整数倍放大

**决策**：640×480 `RenderTarget2D` 承载帧与文本，最后用 `PointClamp` 整数倍放大并加黑边。

**理由**：像素风必须在整数倍下用最近邻放大；文本在逻辑像素空间绘制才能与位图字体度量一致。
放大数学集中在 `PresentationViewport`，可单元测试。

## ADR-004：FontStashSharp 作为首选文本后端

**决策**：M0 用 FontStashSharp 渲染 TrueType（含 CJK）；原版位图字体作为第二实现延后接入。

**理由**：早期需要可读的中英文调试界面与 UI 文案，TrueType 无需 MGCB 打包；
抽象 `ITextRenderer` 保证 UI 层不依赖具体后端。

**风险**：TrueType 度量与位图字体不同，最终界面仍需位图字体——已列入 M2。

## ADR-005：自研 `LittleEndianReader/Writer`

**决策**：不使用 `BinaryReader`/`BinaryWriter`。

**理由**：`BinaryReader` 的小端行为与长度前缀字符串、`ReadString` 语义与这些格式不匹配，
且无法精确控制「读取 15 字节 NUL 填充名称」这类操作；自研类型可一处修复字节序问题并统一抛错。

## ADR-006：存档格式不与 C++ 互通

**决策**：定义自己的版本化 schema，读取 `CURRENT_FORMAT_VERSION = 10034` 只作为参考。

**理由**：兼容 C++ 存档会把全部内部数据结构和演进历史绑死，收益（跨实现读档）远小于成本。

## ADR-007：不使用 MGCB 内容管线

**决策**：不引入 `.mgcb`；游戏资源在运行时从 AGG 解码，字体直接 `File.ReadAllBytes`。

**理由**：原版资源是自定义二进制格式，MGCB 无法处理；且不希望构建时依赖额外工具链。

## ADR-008：`.slnx` 解决方案格式

**决策**：使用 .NET 10 SDK 默认生成的 XML 解决方案（`FHeroes2Sharp.slnx`）。

**理由**：SDK 10 的 `dotnet new sln` 默认输出；`dotnet build/test` 与 VS 2022 17.13+ 均支持。

## ADR-009：数据目录解析（不自动下载）

**决策**：`--data` → `FHEROES2_DATA` → exe 目录/其 `Data` 子目录 → 开发机已知安装路径。

**理由**：GPLv2 项目不能分发原版资源；同时开发体验要求「零参数启动」。
路径解析失败时客户端降级为灰度调色板并返回非零退出码，而不是崩溃。

## ADR-010：依赖真实游戏数据的测试

**决策**：集成测试直接读本机真实 `heroes2.agg`，数据缺失时**提前返回**（视为跳过）。

**理由**：1434 条名称哈希是现成的最强基准，能一举验证哈希算法、记录布局与偏移计算。
代价是「静默通过」——因此 `PROGRESS.md` 会记录实际运行结果，且 `AssetProbe` 提供人工核对入口。

## ADR-011：考虑 NativeAOT，但暂不作为支持目标

**决策**：产品项目（`Core`/`Assets`/`Rendering`/`Game`/`UI`/`Client.Desktop`/`AssetProbe`）全部开启
AOT 与裁剪分析器：

```xml
<IsAotCompatible>true</IsAotCompatible>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
```

配合 `TreatWarningsAsErrors`，任何 `IL2xxx`/`IL3xxx` 发现都会**直接让构建失败**。
但**不提供** `PublishAot` 发布配置，也不承诺当前可 AOT 编译。

**理由（为什么考虑）**
* 长期收益：无 JIT 冷启动（地图/存档加载）、单文件部署、更小的工作集、便于将来打包到 Steam Deck 等受限环境。
* 本项目的天然优势：所有解析器都是纯字节操作（`LittleEndianReader`）、无 MGCB、无运行时资源编译、
  无插件式架构——本来就离「反射自由」很近，约束成本低。

**理由（为什么暂不支持）**
* MonoGame DesktopGL 的 NativeAOT 依赖本地 C/C++ 工具链与 SDL 原生库，上游支持仍在演进，
  现在投入会在每个 MonoGame 版本上反复返工。
* 图形后端、FontStashSharp、SDL 音频在 AOT 下的实际行为需要用真实资源长时间验证，
  属于 M9 之后才能有结论的事情；早期阶段把精力放在格式正确性上。
* 只开分析器就能让新代码保持「AOT 友好」，未来切换成本接近零。

**代价 / 约束（从 M0 起生效，写入 `MISC.md`）**
* 禁止 `Activator.CreateInstance`、`Type.GetType`、`dynamic`、`Assembly.Load`、
  `Expression.Compile`、运行时 `MakeGenericType`。
* 需要「按名字找类型」时用显式 `switch` 工厂。
* 序列化（M8 存档）必须用 `System.Text.Json` 源生成器（`JsonSerializerContext`）或手写二进制读写。
* 万不得已使用反射时，必须加 `[UnconditionalSuppressMessage]` 并在注释中说明为何安全。

**验证**：M0 阶段用一个临时探针验证过分析器确实生效——加入
`Activator.CreateInstance(Type.GetType(name)!)` 后构建以 `error IL2057` 失败；
移除探针后全解决方案 `--no-incremental` 重建：**0 warning / 0 error，0 条 IL 发现**。

**未来开启路径**：M9 评估 `PublishAot=true` + `InvariantGlobalization` 可行性，
前提是 MonoGame 上游正式支持且真实资源全流程验证通过。

## ADR-012：变换层语义与转换表「生成 + 摘要固定」

**决策**：

1. 变换层按 C++ 语义实现——`0` 复制图像层、`1` 跳过目标像素、其余值是
   `PaletteTransformTable` 的索引，用来转换**目标**（已存在于画布上）的颜色；它**不是**遮罩。
2. `transformTable`（16 × 256 字节）不手抄，而由
   `tools/scripts/GeneratePaletteTransformTable.ps1` 从 fheroes2 的 `src/engine/image.cpp`
   提取，生成以 Base64 存储的 `PaletteTransformTable.cs`。
3. 测试用 SHA-256 固定这 4096 字节，并抽查表 2/5（阴影强弱）、表 15（循环色静态化）。

**理由**：

* 手写 4096 个字节既无法审查也无法验证；脚本保证「来源唯一」，重跑即可确认生成物与参考源码一致。
* Base64 让 diff 保持可读规模（逐字节十六进制数组会是数千行）。
* 摘要常量与「1434 条名称哈希」的思路一致：任何一位偏差都会让测试立刻失败，而不是等到肉眼比对 PNG。

**代价 / 约束**：

* 参考版本升级后必须重跑脚本并更新期望摘要（`PaletteTransformTableTests`）。
* 导出 PNG 时 `1` 写成 alpha 0，其余变换值一律不透明——变换效果发生在目标像素上，
  单独看精灵无法表达，需按 `BlitSprite` 合成后再比对。

## ADR-013：PNG 编解码自己实现，逐像素比对代替人工看图

**决策**：

1. PNG 的写出与读回都由 `FHeroes2.Core.Imaging` 自己实现（`PngWriter` / `PngReader` / `PngImage`），
   不引入 ImageSharp / SkiaSharp 之类的第三方图像库。
2. 读回侧支持 PNG 规范里与回归相关的子集：色彩类型 0/2/3/4/6、位深 1/2/4/8/16、五种行滤波、
   `tRNS`；Adam7 交织与未知关键块显式抛异常而不是静默接受。
3. 回归验证以「逐像素比对」为准：`AssetProbe compare` 比较文件或目录，退出码 `0`/`3` 可直接门禁。

**理由**：

* `FHeroes2.Core` 的约束是零外部依赖（见 ADR-001/ARCHITECTURE），图像库会把原生依赖和
  AOT 限制带进来，而我们只需要 RGBA 这一种写出格式加一个「能读回别人编码的 PNG」的能力。
* 只有自己实现读回，才能把 `PngWriter`、行滤波、调色板展开都放进单元测试；
  用第三方库读回自己的输出，会把两侧的错误互相抵消掉。
* 逐像素比对可以脚本化、可复现；肉眼比对 PNG 无法进 CI，也无法给出差异数量和坐标。

**代价 / 约束**：

* 不支持 Adam7 交织；读取交织文件会抛 `NotSupportedException`（当前所有参考导出都是非交织的）。
* 解码结果统一展开为 RGBA，不再保留「索引 + 调色板」的信息；需要索引时用
  `PngImage.ToIndices(palette, buffer)` 反查，未知颜色会被计入未映射计数。
