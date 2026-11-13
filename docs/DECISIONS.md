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

**决策**：640×480 目标纹理承载帧与文本，最后用最近邻（nearest）整数倍放大并加黑边。

**理由**：像素风必须在整数倍下用最近邻放大；文本在逻辑像素空间绘制才能与位图字体度量一致。
放大数学集中在 `PresentationViewport`，可单元测试。

## ADR-004：TrueType 作为首选文本后端（已由 ADR-017 落实为 SDL3_ttf）

**决策**：M0 用 TrueType 渲染（含 CJK）；原版位图字体作为第二实现延后接入。

**理由**：早期需要可读的中英文调试界面与 UI 文案，TrueType 不需要资源打包流程；
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
* SDL3-CS 是 `DllImport` 绑定，本身 AOT 友好，但原生库（SDL3/SDL3_ttf）随 NuGet 分发，
  各 RID 的加载与打包行为需要用发布产物实测，现阶段不投入。
* 图形后端、SDL3_ttf 与音频在 AOT 下的实际行为需要用真实资源长时间验证，
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
前提是 SDL3 原生库在各 RID 下的发布产物与真实资源全流程验证通过。

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

## ADR-014：BMP 导出保持 8 bit 索引，光标按专用格式解码

**决策**：

1. `BmpWriter` 只写 8 bit 索引 BMP：`BITMAPFILEHEADER` + `BITMAPINFOHEADER` + 256 条 `{ B, G, R, 255 }`
   调色板 + 自下而上、按 4 字节对齐的行。这与参考引擎「把 8 bpp surface 交给 `SDL_SaveBMP`」的输出
   **逐字节相同**（第 4 字节是 SDL 写出的调色板 alpha，规范里叫「保留」）。
2. 索引位图没有 alpha，所以 `BmpWriter.EncodeSprite(sprite, palette, background)` 先按
   `background` 填满画布，再用 `ImageOps.BlitSprite` 按变换层合成，等价于
   `fheroes2::Save( image, path, background )`。
3. 归档里的 `*.BMP` 条目（`ADVMBWnn` / `CMSEBWnn` / `SPELBWnn`）不是 Windows 位图，
   由 `MonoCursorFile` 按引擎自己的「黑索引 + 宽高 + 像素码」布局解码，而不是复用 BMP 读取器。

**理由**：

* 保留调色板索引意味着导出结果可以直接拿来断言「解码得到的索引是否正确」，不必先经过
  RGB 归一化——与 `PngWriter` 写 RGBA 的方向互补：PNG 用于看图与 `compare`，BMP 用于索引级校验。
* 32 bit BMP 会让两种导出器写同一份像素，失去这种互补性，而且与原版 `Save()` 的行为不一致。
* 把光标条目的「伪 BMP」和真正的位图格式混在一个读取器里，会让格式判断变得不可靠；
  两者的头部（6 字节 vs 14+40 字节）与像素语义（3 值码 vs 调色板索引）都不同。

**代价 / 约束**：

* `compare` 只读 PNG；BMP 的读回没有实现，索引级校验依靠「参考引擎导出 ⇄ 本项目导出」的逐字节比对
  （见 ADR-015）与 Pillow 抽查（见 `MISC.md`）。
* 单色光标只有黑/白两色，颜色由 `KB.PAL` 的 11（白）与条目头字节（原版为 33）决定，
  改了调色板就无法得到黑白效果——这与参考引擎一致。

## ADR-015：用原版 C++ 实现做逐字节对拍

**决策**：

1. 新增 `tools/reference/fh2_reference.cpp`：一个只做参数解析的 C++ 驱动，编译时**链接** fheroes2 的
   `agg_file.cpp` / `serialize.cpp` / `zzlib.cpp` / `image.cpp` / `image_palette.cpp` /
   `image_tool.cpp`（外加 `system.cpp` / `logging.cpp` / `tools.cpp` / `image_color_conversion.cpp`
   以满足链接），不复制、不改写任何引擎代码。
2. 产物写到 `tools-output/reference/`（已被 `.gitignore` 忽略），并把 SDL2.dll / SDL3.dll 一并复制
   过去，运行时不依赖 `PATH`。
3. `tools/scripts/CompareWithReference.ps1` 同时运行参考导出器与 `AssetProbe --format bmp`，
   用 SHA-256 逐文件比较；文件名集合或数量不一致同样算失败，`-All` 可扫描归档里的全部条目。

**理由**：

* 「移植是否正确」的黄金标准只能是原实现：摘要测试只能证明「我们没改坏」，不能证明「与参考一致」。
* 只链接解码相关的翻译单元（10 个 `.cpp`），不构建整个游戏、也不引入 CMake 工程，编译约 20 秒。
* BMP 是索引格式，文件逐字节相同即可同时钉住解码结果、变换层合成、文件头与调色板，
  不需要额外的像素比较器，也不需要实现 BMP 读回。
* 实测：默认 14 个条目 1204 个 BMP、`-All` 958 个条目 15,025 个 BMP **全部**与参考实现逐字节相同。

**代价 / 约束**：

* 需要本机的 MSYS2 UCRT64 工具链（g++、SDL2、zlib）与一份 fheroes2 源码；CI 目前没有这两样，
  所以对拍是**本地门禁**而不是 `dotnet test` 的一部分，CI 仍由摘要测试守护。
* 参考导出器只覆盖 ICN / TIL / 单色光标三条路径；距离光标（`0x4000`）是参考引擎运行时合成的，
  没有可导出的文件（M1 遗留项，推迟到 M4/M5）。
* `fheroes2::Save()` 不会创建目录，脚本负责建目录；底色必须两侧一致（参考引擎用 23）。

## ADR-016：语言包沿用 GNU MO，内部按 UTF-8 解码

**决策**：

1. 语言包格式继续用 gettext `.mo`（fheroes2 的 `files/lang/<language>.mo`），不发明新格式、
   也不把文案写进 C# 代码。源文件是仓库内的 `translations/<culture>.po`，`.mo` 由
   `tools/scripts/BuildTranslations.ps1`（`msgfmt`）生成并安装到 `<data>/files/lang/`。
2. 读取实现 `Assets.Localization.MoCatalog` 完全对齐 `Translation::MOFile` 的语义：小端/大端魔数、
   主版本必须为 0、复数形式在单个 `msgstr` 内用 `\0` 分隔、空 `msgid`/`msgstr` 跳过、
   `context|text` 前缀在显示前剥离、未命中时回退英文原文。
3. 但按 `msgid` 字符串建索引，而不是像引擎那样按 `crc32b(msgid)` 的哈希值查找。
4. `.po` 一律 `charset=UTF-8`；`MoCatalog` 只解 UTF-8/ASCII，其它声明直接抛 `NotSupportedException`。

**理由**：

* 复用 `.mo` 可以直接拿 fheroes2 现成的 25 种语言目录做参考与交叉验证（`msgid` 一致时未来可共享）。
* CRC32 是引擎读实现的细节，不是文件格式的一部分；用字典可以避免哈希冲突被静默合并，也省掉
  「碰撞检测」这类防御代码。
* 引擎把 `msgstr` 当作 8 bit 代码页字节直接交给位图字体渲染（CP1251 等），而本项目用 Unicode +
  TrueType（SDL3_ttf 支持 CJK）。UTF-8 是唯一不需要引入代码页表的子集；要做到「静默不乱码」，
  遇到 CP1251 目录时必须显式报错而不是猜测。

**代价 / 约束**：

* 暂时读不了 `de.mo` / `ru.mo` 这类非 UTF-8 目录；要支持需引入代码页解码（并评估 `System.Text.Encoding.CodePages`
  对 AOT 的影响），或把这些 `.po` 转成 UTF-8 重新编译。
* 参考引擎**无法显示中文**（`SupportedLanguage` 无中文、`CodePage` 只有单字节、字体是 256 字形位图），
  所以 `zh_CN.mo` 目前只对 C# 端口有意义；C++ 侧要支持需要改引擎的枚举、码页与 CJK 排版。
* 翻译按批次推进，未翻译条目回退英文，`BuildTranslations.ps1` 的 `msgfmt --statistics` 是对外可见的覆盖率指标。

## ADR-017：呈现层从 MonoGame 换成 SDL3（+ SDL3_ttf）

**决策**：

1. `FHeroes2.Client.Desktop` 直接使用 `SDL3-CS`（3.4.20）+ `SDL3-CS.Native`（3.4.2）+
   `SDL3-CS.Native.TTF`（3.3.0），从依赖图里移除 MonoGame 与 FontStashSharp。
2. `SdlPresenter` 独占窗口与呈现：流式帧纹理（`SDL_PIXELFORMAT_ABGR8888`）+ 640×480 目标纹理，
   脏矩形 `SDL_UpdateTexture`，最后最近邻整数倍放大并加黑边；`PresentationViewport` 的数学不变。
3. 事件循环自己写（`SDL_PollEvent`）：ESC 退出、F11 全屏、F12 截图、窗口关闭，
   `--smoke-frames` / `--screenshot` / `--scale` / `--fullscreen` 语义与之前完全一致。
4. 索引色 → 打包像素的转换放在 `Core`（`IndexedFrameConverter`），不引用任何 SDL 类型，
   因此可以在无 GPU 的测试进程里跑；单元测试钉住 `0xAABBGGRR` 字节序、裁剪与短缓冲报错。

**理由**：

* MonoGame 的 DesktopGL 后端本身就是 SDL2 的封装，直接调 SDL3 少一层依赖，
  也让 AOT 分析器面对的是纯 `DllImport` 绑定。
* 目标纹理 + 最近邻整数倍放大与原版 `fheroes2` 的呈现方式一致，逐像素结果可用截图对拍。
* SDL3-CS 提供 `ReadOnlySpan<byte>` 重载，脏区上传不需要中间托管数组。

**文本与「字体清晰度」**：

* SDL3_ttf 走 FreeType，12 px 下纯轮廓字体（SimHei、微软雅黑）灰边明显，放大 2 倍后发糊；
  把系统字体优先级改成 **SimSun**（12 px 有内嵌点阵）后小字号恢复锐利，同时保留 AA。
* 需要硬边像素风时可设 `FHEROES2_TEXT_CRISP=1`，把字形覆盖率二值化为 0/255；
  实测它只适合点阵字体，对纯轮廓字体会打断细笔画，所以默认关闭。
* 备选方案（已实测评估，未采用）：`Sevencat.GlyphRaster` 是纯托管的 TrueType 光栅化器，内置文泉驿微米黑，
  不依赖系统字体与原生库。同一条文本在 12 px 下的对比结果：它的覆盖率光栅化比 SimSun 的内嵌点阵更糊
  （内置字体与 `simsun.ttc` 都只有灰阶轮廓，171~210 级覆盖率），所以小字号锐度并不占优；
  它的价值在于「跨平台自带 CJK」，将来需要时可在 `ITextRenderer` 后面再加一个实现，其余代码无需改动。

**代价 / 约束**：

* 截图改走 `SDL_RenderReadPixels` + `SDL_ConvertSurface` + `PngWriter`，必须在目标纹理仍绑定时读取。
* 文本实现依赖渲染器句柄（`SdlTextRenderer(renderer, fontPath)`），必须晚于呈现器创建、早于其释放。
* 找不到可用字体时诊断文本降级为关闭（与之前一致）。

## ADR-018：主菜单用原版 `FONT.ICN`/`SMALFONT.ICN` 位图字体，TrueType 只做 CJK 补充

**背景**：`docs/PLAN.md` 早期把原版字体写成「FNT 位图字体」。归档里确实有 `BIGFONT.FNT`（17,210 字节）
与 `SMALFONT.FNT`（17,227 字节），但参考实现从不读取它们（全仓库无引用）；`fheroes2` 的界面字体是
**ICN 精灵表**：`FONT.ICN` = 正常字号，`SMALFONT.ICN` = 小字号，大字号由 `FONT.ICN` 运行时放大。
M0 的 `SdlTextRenderer`（SDL3_ttf）只服务诊断文本，风格与主菜单不符。

**决策**：

1. `FHeroes2.Assets.Fonts.BitmapFont` 从 `AssetBundle` 读 `FONT.ICN`/`SMALFONT.ICN`，一个字符一个
   `Sprite`。`BitmapFontSize.Large` 暂不支持（`Load` 抛 `NotSupportedException`），因为参考实现是
   运行时放大而不是独立资源。
2. **索引移位**：ICN 表从字符 `0x20`（空格）开始，`game_assets.cpp::processICN()` 会在表头
   `insert(32, imageArray[0])` 让「索引 = 字符码」。`FromIcn()` 前置同样的 32 个副本，因此
   128 个字形 = 96 个 ICN sprite + 32。不做这一步整表会错位（`'A'` 变成 `'!'`）。
3. **度量与绘制**按 `fheroes2::FontCharHandler` / `renderSingleLine()` 逐条移植：advance =
   `sprite.OffsetX + sprite.Width`、空格宽度按字号 4/6/8、行高 11/17/33、无效字符回退 `?`、
   `_charLimit` = 字形表长度、`\n` 换行、尾随空格不计宽（多行拼接除外）、支持裁剪区。
4. `IGlyphSource` 把「度量 + 取字形」抽象出来，`BitmapFont` 是第一个实现。TrueType 实现
   （SDL3_ttf，候选字体 `Huiwenmincho-improved.ttf` / `WenQuanYiMicroHei.ttf`）留给需要 CJK 或
   `U+00FF` 以上字符的场景，接入时文本布局代码不用改。**后续**：该实现已由 ADR-019 落地，但换成了
   纯托管的 `Sevencat.GlyphRaster`（不引入 SDL 原生依赖）；因为走的是同一个接口，布局代码确实一行没改。

**理由**：

* 主菜单的观感由原版像素字形决定，用 TrueType 重排只能「像」，不能「一致」；M2 的验收目标是
  可以从数据目录启动到主菜单，因此先保住逐像素一致的那一半。
* 原版字体本身就是单字节表（0x20–0x7F 含大小写，扩展码页由 `resizeCodePage()`/`generateAlphabet()`
  生成），与 ADR-016 的 UTF-8 内部编码并不冲突：布局层按 `char` 取字形，取不到就 `?`。
* 把 TrueType 放在接口后面而不是写进 `BitmapFont`，避免出现「一个类两种坐标/两种 advance」。

**代价 / 约束**：

* 0x80 以上字符在 `BitmapFont` 里一律画 `?`；CJK 文本请用 `TrueTypeGlyphSource`（ADR-019 已落地）。
* `modifyBaseNormalFont()` 的若干像素修补（`$`、`%`、`&`、`-`、`\`、`|`、小写 `k`）与码页扩展尚未移植，
  它们只影响字形外观，不影响布局；等主菜单拼好后按需补齐并用截图对拍。
* 字体测试只依赖 `FONT.ICN`/`SMALFONT.ICN`，数据缺失时提前返回（ADR-010）；渲染摘要先打印不硬断言。

## ADR-019：TrueType 字形用 `Sevencat.GlyphRaster`（纯托管），覆盖率量化回原版灰阶

**背景**：ADR-018 把 CJK 与非单字节文本留给 `IGlyphSource` 的 TrueType 实现，并预留了三种候选：

1. **SDL3_ttf**——与 C++ 参考实现同路，但引入原生依赖，和 ADR-011 的「可 AOT、禁反射」目标冲突。
2. **系统字体 + `System.Drawing`/FreeType 绑定**——不可移植，依赖用户机器装了哪些字体。
3. **纯托管、自带 CJK 字形的光栅化库**——`F:\code\zlmsharp\src\GlyphRaster`
   （NuGet `Sevencat.GlyphRaster` 0.1.5）：TrueType 解析 + 扫描线抗锯齿全托管，程序集内嵌
   WenQuanYi Micro Hei，不需要 SDL，也不需要查找系统字体。

**决策**：

1. 依赖放在 **`FHeroes2.Assets`**（与 `BitmapFont` 同层、紧邻 `IGlyphSource`），不进入 Core；
   版本统一在 `Directory.Packages.props` 声明。
2. `TrueTypeGlyphSource` 实现 `IGlyphSource`，构造函数收 `TextFont` + **em 尺寸（像素）**。
   `ForLineHeight(font, 17)` 反解 em 尺寸，使行高正好等于原版 `FONT.ICN` 的 17 px（小字号 11 px）；
   每个字符只光栅化一次并缓存 `Sprite`。
3. **覆盖率 → 调色板索引**由 `GlyphCoverageRamp` 完成。实测 `KB.PAL` 的 **10–36 号索引是一条 27 级
   灰阶**（10 = (63,63,63) 最亮 → 36 = 黑），而 `FONT.ICN` 的亮像素集中在 10/16/21/12/14 上，
   即原版字形自带「预烘焙抗锯齿」。默认梯度 `[25, 21, 16, 12, 10]`（最淡→实心），
   覆盖率 < 32 的像素直接跳过；单级梯度即硬边像素风格。
4. **坐标系只换算一次**：GlyphRaster 的 `OffsetY` 以基线为准，`Sprite.OffsetY =
   mask.OffsetY + Ascent`，两种 provider 因此共享「文本左上角」原点（`BitmapFont` 的约定）。
5. 布局与绘制抽到 `GlyphText`（`Measure` / `DrawLine` / `DrawMultiline` / `WrapLines`），
   `BitmapFont` 与 `TrueTypeGlyphSource` 走同一条路径，规则仍是 `FontCharHandler` /
   `renderSingleLine()` 的移植版。

**理由**：

* 无原生依赖、可 AOT，符合 ADR-011；不需要用户安装字体，符合 ADR-010「只依赖数据目录」的假设。
* 只替换「像素从哪来」，不替换布局规则：换行、居中、裁剪、尾随空格在两个 provider 上完全一致，
  因此中英混排不需要两套 UI 代码。
* 量化到原版灰阶后，TrueType 文本与 ICN 文本同屏时色调一致，不会显得「一块是新渲染的」。

**代价 / 约束**：

* 新增一个第三方 NuGet 依赖，许可已澄清：`Sevencat.GlyphRaster` 是**本项目作者自己维护**的包
  （`F:\code\zlmsharp`），改协议不受外部约束，因此**不是分发阻塞项**。当前该包标注 GPL-3.0
  （内嵌字体文泉驿微米黑为 Apache-2.0 / GPL-3.0 双许可、含字体嵌入例外），若要与本仓库的 GPLv2
  严格对齐，把该包改为 Apache-2.0 即可，本仓库代码无需改动——`IGlyphSource` 本来就把依赖隔在一层
  之后，这正是 ADR-018 留接口的目的。
* 内嵌只有一套 CJK 黑体，`assets/fonts/` 的字体目前没有被自动选用；自定义字体需要先
  `TextFont.LoadFromFile(...)` 再传给构造函数（字体的自动探测尚未接入 UI）。
* 光栅化不做 hinting，≤12 px 的中文不如 SimSun 的内嵌点阵锐利；必要时用单级梯度换取硬边观感。

## ADR-020：文字颜色用「调色板表」重映射索引，不做 RGB → 最近色搜索

**背景**：主菜单需要白、黄、灰三种文字，而原版只有一份 `FONT.ICN`。查参考实现可见颜色不是资源差异：
`game_assets.cpp` 用 `CopyICNWithPalette( ICN::YELLOW_FONT, ICN::FONT, PAL::PaletteType::YELLOW_FONT )`
在运行时生成黄色字体，灰色同理（`GRAY_FONT` / `GRAY_SMALL_FONT`）；`PAL::GetPalette()`（`src/engine/pal.cpp`）
返回的是 256 字节的**索引重映射表**，`yellowTextTable` 把灰阶 10–36 映射到黄阶 114–130。
`GOLDEN_GRADIENT_FONT` / `SILVER_GRADIENT_FONT` 例外：它们是 `generateGradientFont()` 按行做的渐变，
不是调色板。

**决策**：

1. 移植 `pal.cpp` 的 13 张表 + `PAL::GetPalette(STANDARD)` 的 identity → `Core.Imaging.PaletteTable`
   （`PaletteId` 枚举数值与 `PAL::PaletteType` 一致，`CUSTOM` 除外）。数据由
   `tools/scripts/GeneratePaletteTable.ps1` 从 C++ 源抽取成 Base64，`PaletteTableTests` 固定 SHA-256，
   与 ADR-012 的「生成 + 摘要固定」是同一套手法。
2. 应用入口是 `ImageOps.ApplyPalette(PixelBuffer, ReadOnlySpan<byte>)`（`fheroes2::ApplyPalette`）：
   只改图像层，不动变换层——变换层存的是操作（0 复制 / 1 跳过 / n 查 `PaletteTransformTable`），不是颜色。
3. `BitmapFont.WithPalette(PaletteId)` 返回重着色副本，`Load(bundle, size, PaletteId)` 是便捷入口；
   `PaletteId.Standard` 直接返回自身。**同一个 `Sprite` 实例只转换一次**：`FromIcn` 让 32 个控制码共用
   首个字形副本，转换两次会把像素再映射一遍（落到 0），所以实现里用引用相等的字典去重。
4. `GlyphCoverageRamp.WithPalette(PaletteId)` 把同一张表套到覆盖率梯度上：默认灰阶
   `[25,21,16,12,10]` → 黄色 `[124,121,118,115,114]`。于是 TrueType 的 CJK 字形与位图字体同色，
   不需要第二套光栅化或逐像素调色。
5. 渐变字体（黄金 / 白银）暂不实现，等主菜单标题需要时再移植 `generateGradientFont()`。

**理由**：

* 与原版逐字节一致：颜色是数据表，不是算法；不会出现「按 RGB 找最近色」找不到同色相梯度的情况。
* 混排颜色天然一致：中英文都走「索引 → 表」，只要传同一张表就同色。
* 一张表顺带解决 M3+ 的需求：`Darkening`（禁用按钮）、`Gray`（石化/未解锁）、`Red`（血祭）、
  `Purple`（随机物体）、`GoodToEvil*`（邪恶界面）、`NoCycle`（静态水/岩浆）。

**代价 / 约束**：

* `PaletteTable` 是 3584 字节常量，修改必须回到生成脚本，不能手改。
* 颜色只能取原版表里的结果（不是任意调色板）；新增颜色类别需要同时改脚本与 `PaletteId`。
* 渐变文字与「按 alpha 混合」（`ApplyAlpha`）仍缺；后者等半透明提示框时再移植。

## ADR-021：中英混排用「复合字形源」——位图字体定度量，TrueType 只补位

**背景**：ADR-018/019 分别定下了「原版 `FONT.ICN` 为主」与「TrueType 做 CJK 补充」，但两者
各自实现 `IGlyphSource`，谁负责一段混排文本的布局、行高、空格宽度和「找不到字符时画什么」
没有定论。参考实现的答案很直接：`interface_font.cpp` 的 `getTextWidth()` / `renderText()`
里从来只有一套度量——位图字体决定 advance，遇到 `isValidValue()` 为假的字符才去调
`fontTrueType` 取字形，而行高始终是 `getFontHeight(fontType)`（小 11 px / 正常 17 px）。
换句话说 TrueType 在原版里是**字形提供者**，不是第二种字体。

**决策**：

1. 新增 `Assets.Fonts.CompositeGlyphSource`（实现 `IGlyphSource`）：构造时接收 primary 与
   fallback 两个 provider。`LineHeight` / `SpaceWidth` **只**取自 primary，
   `GetAdvance()` / `TryGetGlyph()` 按「primary 能表达就用 primary，否则问 fallback」分派，
   两者都不能表达时返回 primary 的替代字形（位图 `?`），并让 `IsAvailable` 返回 `false`，
   于是「测量」与「绘制」永远不会给出不一致的结论。
2. `CompositeGlyphSource.ForBitmapFont(BitmapFont, TextFont, ramp = null)` 是标准组合方式：
   内部调用 `TrueTypeGlyphSource.ForLineHeight(face, primary.LineHeight, ramp ??
   GlyphCoverageRamp.Heroes2Text.WithPalette(primary.Palette))`。因为
   `BitmapFont` 现在记录自己的 `Palette`（`WithPalette` 的产物，未重着色为
   `PaletteId.Standard`），混排里的中文自动继承英文的颜色，调用方不必把调色板传两遍（ADR-020）。
3. 客户端把加载收进 `Client.Desktop.Fonts.GameFonts`：`TryLoad(AssetBundle, TextFont?)`
   读 `FONT.ICN` / `SMALFONT.ICN`，生成 `Small` / `Normal` / `Yellow` 三个 `IGlyphSource`
   （`Yellow` = `FONT.ICN` 过 `PaletteId.YellowFont`），并暴露 `SupportsCjk`（探测 `'中'`）
   与 `Description` 供诊断覆盖层显示。缺数据或缺字体时不抛异常，`TryLoad` 返回
   `null`，客户端仍能显示 M0 覆盖层。
4. 文本走索引路径：`DiagnosticsScene` 用 `GlyphText.DrawLine` 把示例行画进
   `ScreenBuffer`，再由 `SdlPresenter` 按调色板上屏。SDL_ttf 只剩诊断覆盖层一项用途。

**理由**：

* 布局规则只有一份：`GlyphText` 从 `IGlyphSource` 读度量，复合源把「哪套度量算数」这件事
  收在一个类里，不需要在每个绘制点判断字符范围。
* 英文像素与 M1 完全一致：primary 命中时直接返回 ICN 的 `Sprite` 实例，不经过光栅化。
* 颜色只有一条路：位图字体的调色板被 fallback 继承，黄色标题里的中文不会变成灰色。
* 上屏路径统一为「索引 → 调色板 → 整数倍放大」，没有 RGB 纹理与索引帧缓冲两套像素语义。

**代价 / 约束**：

* 混合字体的行高永远是位图字体的行高（11/17 px）；要给 CJK 更大的字号必须换 primary，
  或者显式传 `ramp` / 自建 `CompositeGlyphSource`。
* `BitmapFont.Palette` 只记录「这份字形被哪张表转换过」，不是逐像素真值；手工改过像素的
  字体会让记录失真（`Create(..., palette)` 允许显式声明）。
* 两份 provider 的字形缓存各自独立，`ForBitmapFont` 每次调用都会新建一个
  `TrueTypeGlyphSource`；组合件应当在启动时创建一次并复用。
* 逐字符分派带来一次 `IsAvailable` 查询（`BitmapFont` 是数组边界检查，TrueType 侧是
  `GlyphRaster` 的字符映射查询），对 640×480 的文本量可以忽略；如需极致性能可以在
  `GlyphText` 层按「连续同源片段」批处理，目前未做。


## ADR-022：主菜单由原版 ICN 拼装，界面文字全部走索引渲染

**决策**：

1. 主菜单不是「画一个对话框」，而是把原版资源拼起来：背景是 `HEROES.ICN`（单张 640×480，
   `AssetBundle` 优先取资料片里的同名条目，行为与 `AGG::GetICN` 一致）；五块招牌取
   `BTNSHNGL.ICN` 的第 1 / 5 / 9 / 13 / 17 帧，悬停 `+1`、按下 `+2`，与
   `Game::MainMenu()` 传给 `fheroes2::Button` 的偏移完全相同。招牌的屏幕位置直接来自 ICN 头里的
   `OffsetX/OffsetY`，代码里没有第二套坐标常量，也就不需要一张单独的「主菜单布局表」。
2. 新增 `FHeroes2.UI.Widgets.Button`：三张 `Sprite`（released / hovered / pressed）+ 一个
   `ButtonState` 状态机，`Area` 取 released 精灵的包围盒，`Update(cursor, leftButtonDown)`
   返回「状态是否变化」，缺 hover/pressed 帧时回退到 released（只有一帧的表也能用）。
3. 新增 `FHeroes2.Client.Desktop.Scenes.IScene`：`Update` / `DrawFrame(in DiagnosticsState)` /
   `DrawOverlay(in DiagnosticsState)`。真实画面（`MainMenuScene`）只用前两个：文字全部画进
   索引帧缓冲，`DrawOverlay` 留空；`DiagnosticsScene` 继续用 SDL_ttf 覆盖层，两条路径共存但互不影响。
   `DiagnosticsState` 因此被提升为「宿主交给场景的运行时快照」（FPS、归档条目数、字体描述等）。
4. 文字统一由 `Client.Desktop.Localization.GameText` 提供：一个语言一个不可变实例，英文直接用
   msgid（`.mo` 缺失时也是英文），`zh_CN` 由 `<data>/files/lang/zh_CN.mo` 装入。主菜单用
   `GameFonts.Small` 把 `.mo` 里的提示译文与语言标记画进状态栏，`F2` 循环切换语言。
5. 鼠标坐标由 `PresentationViewport.TryToLogical` 反算（整数倍放大 + 黑边），落在黑边上返回
   `false`，因此点击窗口边缘不会命中任何控件。SDL 事件在 `GameHost` 里翻译成
   `SetCursor` / `SetLeftButton`，场景本身不依赖 SDL。
6. 为了能回归验证「有内容可看」，客户端补了几个开关：`--scene menu|diagnostics`、
   `--lang <culture>`、`--cursor x,y`（后者把人造光标放进逻辑坐标，冒烟截图即可定格 hover 态；
   该坐标是「人造」的，宿主随即忽略真实鼠标移动与移出事件，否则物理指针离开窗口会把 hover 清掉）、
   `--screenshot` / `--screenshot-frame n`（默认第 30 帧）与 `--freeze-animation`。
7. `SHNGANIM.ICN` 的灯笼光晕由新增的 `FHeroes2.Assets.Icn.IcnAnimation` 播放：第 0 帧是熄灯状态的
   灯体，先画一次；第 1…39 帧每 250 ms 换一帧（`Game::DelayType::MAIN_MENU_DELAY`），循环 9.75 s，
   与 `ICN::getAnimatedIcnIndex()` 里灯笼那条 `1 + frame % 39` 完全等价。绘制顺序是
   「背景 → 灯体 → 光晕 → 招牌 → 状态栏」，与 `Game::MainMenu()` 相同，所以光晕不可能盖住招牌。
   `IcnAnimation` 不复制像素，每一帧仍按自己的 ICN 锚点绘制；计时按 `fheroes2::TimeDelay` 的语义
   累加（整帧才前进、长停顿只跳帧不补播），场景把 `Update(elapsed)` 换算成 `TimeSpan` 交给它。

**理由**：

* 布局、配色、像素全部来自原版资源，重写布局等于重新发明一遍美术，且很难与参考实现对齐。
* 只有一套文字路径：`GlyphText` + `IGlyphSource`，中英混排同色（ADR-020/021），
  不再出现「位图字体 + GPU 矢量文本」两种像素语义并存。
* `GameText` 不可变，语言切换就是换实例，没有全局状态，也不需要在绘制时判断当前语言。
* 交互逻辑（命中测试、按下/释放、热键）都放在场景里，`Button` 可单测，`GameHost` 只做事件翻译。
* 帧表只移植场景真正用到的那一条：`icn.cpp` 的 `getAnimatedIcnIndex()` 是几百行硬编码 `switch`，
  一次搬空既没法验证也用不上；`IcnAnimation` 接收「起始帧 + 帧数 + 每帧时长」，将来接新动画
  （如 `MONSTER.ICN` 的站立帧）只需补一行调用。

**代价 / 约束**：

* 状态栏是移植新增物：原版用右键弹窗显示提示，这里固定在底部 11 px（一行 `SMALFONT.ICN`），
  会遮住背景最下面 11 行像素；`QUIT` 招牌（y=425..469）刚好不被覆盖。
* 截图取决于数据版本：资料片存在时 `HEROES.ICN` 是 PoL 版（横幅写着 *The Price of Loyalty*），
  做像素基线时必须注明源数据（落地方式见 ADR-023 与 `tests/baselines/README.md`）。
* 语言列表目前硬编码为 `en` + `zh_CN`（`.mo` 存在才加入），没有 fheroes2 的多语言选择对话框。
* 动画由墙钟驱动：`--screenshot` 默认在第 30 帧抓图，抓到第几帧取决于机器帧率，做像素基线
  必须同时加 `--freeze-animation`（把光晕冻结在第 1 帧）。`SHNGANIM` 第 0 帧是熄灯灯体
  （139×131 @ 46,176），第 1…39 帧是光晕（统一 136×180 @ 48,183），所以必须是两层；
  光晕这一层的帧间差异在 8–942 像素之间（整屏 640×480 = 307200 像素的 0.003%–0.3%），
  也就是说动画很轻微，肉眼要靠连续播放才看得出来。
* fheroes2 用鼠标扫过左下角的「门」让门发光来提示设置入口，这条交互与设置对话框一起留给后续里程碑。
* `Button` 只处理「按下与释放落在同一控件」这一种点击语义，没有拖拽取消、没有禁用态
  （`BTNSHNGL` 的第 4 帧就是禁用用的，等 M3 用得着时再加）。

## ADR-023：像素基线入库，门禁排除运行时区域

**背景**：ADR-013 定下「逐像素比对代替人工看图」，但目前只有 `AssetProbe compare` 这个工具，
没有一段被固定下来的期望图像。主菜单已经能出图（ADR-022），改绘制顺序、换调色板、调字体时
「看起来没变」和「真的没变」无法区分；而截图产物目录 `tools-output/` 是被 `.gitignore` 排除的，
上次的截图过一天就没了。

**决策**：

1. 期望图像作为 **基线文件入库**：`tests/baselines/menu.png`（640×480，239,932 字节）。游戏数据
   依旧不入库（ADR-010），所以基线是「我们自己渲染出来的结果」，不是原始美术。
2. 门禁是一个脚本而不是新测试项目：`tools/scripts/VerifyMenuBaseline.ps1` 负责
   `dotnet build` → 渲染一帧 → 比对 → 退出码（0 一致 / 1 不一致）。客户端是 WinExe + SDL3，
   放进 xunit 会让测试进程加载原生库并与窗口/时钟耦合；脚本保持「跑真程序」这一点不变。
3. 渲染必须完全可复现，因此固定三个开关：`--freeze-animation`（光晕冻在第 1 帧）、
   `--cursor 620,100`（人造光标屏蔽真实指针）、`--screenshot-frame 30 --smoke-frames 90`。
4. 状态栏排除在比对之外：`AssetProbe compare` 新增 `--ignore x,y,w,h`（可重复），
   基线用 `--ignore 0,469,640,11`（底部 11 px 的 `FPS | 语言 | 版本`）。
5. 缺数据的机器上脚本打印 `skip` 并以 **0** 退出（与 ADR-010 一致），`-RequireData` 才把缺数据
   变成失败；`-Tolerance <n>` 允许把判据从「逐字节相等」放宽到「差异像素 ≤ n」。
6. 基线只在**有意**改变视觉时用 `-Update` 重建，规矩写在 `tests/baselines/README.md`
   （连同数据版本与 SHA-256）。
7. CI 用 `.github/workflows/ci.yml`：`build` job 只做还原/编译/`dotnet test`（不需要数据）；
   `menu-baseline` job 调 `VerifyMenuBaseline.ps1`，数据目录与 runner 由仓库变量
   `FHEROES2_DATA` / `FHEROES2_RUNNER` 注入（设了才加 `-RequireData`）。脚本在**渲染之前**先校验
   基线文件本身（PNG 魔数 + IHDR 必须 640×480），所以没有数据的托管 runner 上「基线被删/被换尺寸」
   依然会让 job 失败，只有像素比对退化为 `skip`。失败时上传 `tools-output/baseline/` 作 artifact。

**理由**：

* 基线入库才叫回归：`tools-output/menu.png` 是一次性的，`git status` 里没人会看它变了没有。
* 判据越硬越好：忽略状态栏后连续两次渲染逐字节相同，于是 `-Tolerance` 默认 0 —— 任何像素变化
  都会让门禁失败，而不是「差得不多就放过」。
* `--ignore` 放在 `compare` 里而不是在脚本里做后处理：矩形排除的语义与「逐像素差异」统计是同一件事，
  放在一起才能让差异图、`first at`、退出码保持自洽。
* 门禁必须是「一条命令 + 退出码」（`pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData`），
  CI 只负责在什么机器上以什么变量调它；把渲染逻辑塞进 yml 会让本地与 CI 走两条不同的路。
* 基线文件自检放在渲染之前而不是之后：没有游戏数据的托管 runner 也能抓住「基线被删掉」这类
  错误，这类错误恰恰是提交基线时最容易犯的。

**代价 / 约束**：

* 二进制 PNG 进了仓库：239 KB / 张，且以后每加一个场景就再添一张。只给「一屏一图」的场景做基线，
  不为每一帧动画建基线（动画的验证靠帧间 diff，见 PROGRESS）。
* 基线绑定数据版本：当前是资料片（PoL）的 `HEROES.ICN`。只有基础版数据的机器必然 `DIFF`，
  必须另存 `menu-classic.png`，不能覆盖。
* 状态栏像素不在基线覆盖范围内（版本号与 FPS 是运行时值），它的渲染靠人工看
  `tools-output/menu-hover.png` 这类截图。
* **托管 runner 只能做基线自检，做不了像素比对**：游戏数据不能入库、也不该塞进仓库 secret（体积），
  所以真正的门禁需要一台装了数据的自建 runner（`FHEROES2_RUNNER=fh2-data` + `FHEROES2_DATA=…`）。
  在此之前，PR 上的 `menu-baseline` job 绿只代表「基线文件本身没坏」。
* 脚本依赖 Windows + PowerShell 与本地 Debug 输出路径（`bin/Debug/net10.0/FHeroes2Sharp.exe`）。
  Linux 上的 runner 需要 `pwsh` 且路径/程序集名一致，否则先 `dotnet build` 再手搓那两条命令。

