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

**决策**：产品项目（`Core`/`Assets`/`Rendering`/`Game`/`UI`/`App`/`AssetProbe`）全部开启
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

1. `FHeroes2.App` 直接使用 `SDL3-CS`（3.4.20）+ `SDL3-CS.Native`（3.4.2）+
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
3. 客户端把加载收进 `App.Fonts.GameFonts`：`TryLoad(AssetBundle, TextFont?)`
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
3. 新增 `FHeroes2.App.Scenes.IScene`：`Update` / `DrawFrame(in DiagnosticsState)` /
   `DrawOverlay(in DiagnosticsState)`。真实画面（`MainMenuScene`）只用前两个：文字全部画进
   索引帧缓冲，`DrawOverlay` 留空；`DiagnosticsScene` 继续用 SDL_ttf 覆盖层，两条路径共存但互不影响。
   `DiagnosticsState` 因此被提升为「宿主交给场景的运行时快照」（FPS、归档条目数、字体描述等）。
4. 文字统一由 `App.Localization.GameText` 提供：一个语言一个不可变实例，英文直接用
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
   「背景 → 灯体 → 光晕 → 门光 → 招牌 → 状态栏」，与 `Game::MainMenu()` 相同，所以光晕与门光
   都不可能盖住招牌。
   `IcnAnimation` 不复制像素，每一帧仍按自己的 ICN 锚点绘制；计时按 `fheroes2::TimeDelay` 的语义
   累加（整帧才前进、长停顿只跳帧不补播），场景把 `Update(elapsed)` 换算成 `TimeSpan` 交给它。
8. 设置门的悬停发光是 `FHeroes2.UI.Widgets.DoorHighlight`：光源不是独立美术，而是 `SHNGANIM.ICN`
   第 18 帧套 `PaletteId.NoCycle`（`fheroes2::ApplyPalette( highlightDoor, 8 )`）后、只取
   `y ≥ 55`（`doorOffsetY = 55 * scale + offsetY`）的那一半，画在光晕之上、招牌之下。命中区是
   `settingsArea` 的 `63,202 108×160`，与发光像素不重合；参考实现只在 250 ms 的动画节拍里重画这一笔，
   这里每帧都画（场景本来就从背景重铺），于是「移开就灭」不依赖动画时钟、
   `--freeze-animation` 下的悬停截图也是确定的。命中判定、裁剪与调色板都封在 widget 里，
   `DoorHighlightTests` 钉住 18 / 55 / 63,202 / 108×160 这四个数与 transform 层语义。

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
* 设置门只会发光，点它还没有反应：设置对话框（语言 / 分辨率 / 音效，参考实现是
  `Dialog::GameSettings()`）还没接，命中区目前只驱动 `DoorHighlight`。门光与光晕共用 `SHNGANIM`
  第 18 帧，所以它是「同一张精灵的下半部分 + 静态调色板」而不是新美术；这套数据里 `NO_CYCLE`
  对那一帧恰好是恒等映射（去掉与加上逐像素无差），保留只为与参考实现逐字一致。
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
   这个坐标同时避开五块招牌和设置门的命中区（`63,202 108×160`，ADR-022 第 8 条），所以基线里
   既没有 hover 态也没有门光——门光的行为靠单独渲染悬停帧来验证（见 `PROGRESS.md` 的记录）。
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

## ADR-024：右键提示用原版消息框（`BUYBUILD.ICN` 六块拼装）

**背景**：M2 的主菜单已经能点亮设置门（ADR-022），但鼠标右键什么也不做——而参考实现里
右键招牌/门会弹出「标题 + 说明」的消息框（`Game::MainMenu()` 的 `showStandardTextMessage(
_( "New Game" ), _( "Start a single or multi-player game." ), Dialog::ZERO )`）。这是端口里
第一个真正的对话框，也是设置对话框将来的地基：`Dialog::GameSettings()` 的每个子窗口都建立在
同一套边框与文本版式上。

**决策**：

1. 边框用 `BUYBUILD.ICN` 的六块精灵拼，不用 `ADVBORD.ICN` 九宫格。参考实现的
   `Dialog::FrameBox` 就是前者（`ResizableFrameBox( boxAreaWidthPx, height, -1, buttons )`），
   `DrawBorder()`/`ADVBORD` 属于另一支（更大的窗口），继续留在「未移植」。
2. 版式照 `dialog_box.cpp` 抄算术，包括不对称：顶 99 px（左列第 4 帧）+ 中段 + 底 81 px
   （第 6 帧）；中段把第 5/1 帧**从第 10 行起切 35 行**重复；文字区固定 244 宽、最小 70 高
   （两块切片）；`showButtons` 先把 40 px 的按钮行加进高度再数切片。
3. 列宽取每列三块的最大值（左 161、右 145），于是窗口 288 宽、画出来的画面却是 305/306 宽，
   接缝落在 `x = 320`（逻辑分辨率正中）。这种「不居中」是原版的样子，不修正。
4. `Dialogs.MessageBox` 只实现 `Dialog::ZERO` 分支：没有按钮组、没有 dialog elements。
   框高 = 标题行数×行高 + 10 +（正文行数×行高 + 10），标题用 `normalYellow`、正文用
   `normalWhite`，都按 244 px 折行**居中**（`Text::draw( x, y, maxWidth, output )` 的语义）。
5. 对话框不碰输入。参考实现在 `showMessage()` 里跑阻塞事件循环；端口里一帧只画一次，
   于是改成「场景决定谁存在」：`MainMenuScene.SetRightButton( down )` 在按下时创建、松开时丢弃，
   对话框只是一个每帧重画的普通对象（与 ADR-022 第 8 条的思路一致）。
6. 右键语义以「按住」为界（`le.isMouseRightButtonPressed()`）：提示框只存在于按住期间；
   左键、热键都不影响它，这与参考实现的 `else if ( !le.isMouseRightButtonPressed() ) break;` 等价。
7. 不往像素基线里新增东西：`menu.png` 用 `--cursor 620,100` 且不按键，所以提示框不出现，
   基线仍是逐字节相等。要抓提示框的图，用新开关 `--cursor x,y --right-button`——它是和
   `--cursor` 同一类的**测试钩子**（无 `--cursor` 时无效），产物留在 `tools-output/` 不入库。
8. `FHeroes2.UI` 新增对 `FHeroes2.Assets` 的项目引用，因为 `MessageBox` 要 `IGlyphSource` /
   `GlyphText`。这与 PLAN 的 `Core ← Assets ← Rendering ← UI` 方向一致，只是补上了原本没接的箭头。

**理由**：

* 拼装而不是缩放：`BUYBUILD` 的列是 35 px 重复图案，照切片拼出来与原版逐像素一致；
  若改用「拉伸整张图」，边角的宝石与卷轴纹样会糊掉，也无法与参考实现对照。
* 版式写成常量 + 纯函数（`FrameBox.Create`），几何可以用单元测试钉死：640×480 下
  `Area = (198,205,244,70)`、`FrameArea = (159,141,305,180)`、`height=140 → 3 块切片`。
  这样以后接设置对话框，改动边框行为会立刻被测试抓住。
* 把「谁创建对话框」交给场景，端口就不需要把事件循环嵌进 UI 组件里：`MessageBox` 没有
  时间、没有输入、没有全局状态，只有「画一次」。
* 复用 `GlyphText` 而不是再写一套折行：提示框的中文与英文、位图字形与 TrueType 补位都走
  同一条布局路径，颜色由 `PaletteTable` 决定（ADR-020/021）。

**代价 / 约束**：

* 只有无按钮的消息框。带 OK/Cancel 的窗口需要 `ButtonGroup` 与按钮精灵定位（`renderButton` 的
  padding 规则），设置/语言选择/分辨率对话框仍未移植——它们大多还依赖 `StandardWindow`
  （`SURDRBKG` 拉伸 + 抖动过渡 + 背景压暗），那是下一块工作。
* `FrameBox` 目前只有「好」界面的一份：`BUYBUILE.ICN`（邪恶界面）会用到 `Settings` 的
  界面类型开关，等 M8 的设置持久化一起做。
* 提示框的文案取自 `.mo`，中文长句（如门的说明）在 244 px 内折成两行——与参考实现的折行
  规则相同，但中文按字符断行的位置可能与 C++ 用语料实测出来的位置不同（没有逐句对拍）。
* `--right-button` 是测试钩子，不是玩法：真实运行时提示框仍然只由物理右键按住驱动。


## ADR-025：先只支持 Windows x64；程序图标由一份 `assets/logo.ico` 驱动

**背景**：客户端一直是「能编就能跑」，`Directory.Packages.props` 里的 SDL3 原生包给
win-x64/arm64、linux-x64/arm64、osx-x64/arm64 都带了二进制，于是看起来哪儿都能跑。实际上
窗口创建、`SDL_SetWindowIcon`、像素基线脚本 `VerifyMenuBaseline.ps1` 全都只在 Windows 上验证过，
后面要接的对话框、输入法、存档路径也会继续贴着 Win32 语义走。同时项目有了美术资源
（`assets/logo.png`），它需要同时出现在两个地方：Explorer 里 exe 的图标，以及运行时的
标题栏 / 任务栏图标。

**决策**：

1. 只有客户端项目固定 `RuntimeIdentifier=win-x64`；配合
   `AppendRuntimeIdentifierToOutputPath=false`，输出目录仍是 `bin/Debug/net10.0`，
   `VerifyMenuBaseline.ps1` 里的 `FHeroes2Sharp.exe` 路径不变。Core / Assets / Rendering /
   Game / UI / AssetProbe / 测试项目不设 RID，保持可移植（它们不碰窗口）。
2. `Program.Main` 先做平台检查：非 Windows 或 32 位进程打印一行说明并以 1 退出；
   `Program.cs` 里加 `[assembly: SupportedOSPlatform("windows")]`，让 CA1416 按 Windows 目标
   分析平台 API（与 `net10.0-windows` 目标效果相同，但不用换 TFM）。
3. 图标只有一个源：`assets/logo.png`（640×640）经 `tools/scripts/GenerateAppIcon.ps1` 生成
   `assets/logo.ico`（16/24/32/48/64 为 32 位 DIB，128/256 为 PNG）。同一个文件既是 csproj 的
   `<ApplicationIcon>`（编进 exe 的 Win32 资源），又是 `LogicalName=logo.ico` 的
   `EmbeddedResource`。
4. 运行时由 `Graphics.WindowIcon` 读嵌入资源：`Core.Imaging.IcoFile` 解成 `PngImage`，64 px
   那张作为 100% 图标，128/256 用 `SDL_AddSurfaceAlternateImage` 注册为高 DPI 备选，最后
   `SDL_SetWindowIcon`。像素缓冲与 surface 活到 `SdlPresenter.Dispose`（在 `SDL_DestroyWindow`
   之后释放），因为 `SDL_CreateSurfaceFrom` 不复制像素。
5. `IcoFile` 只认 PNG 与未压缩 24/32 位 DIB；调色板位图、RLE 压缩、光标文件（type 2）一律抛
   `BinaryFormatException`，不猜也不静默降级。

**理由**：

* 收口比假装跨平台便宜：RID 一写，`dotnet run` 必定拿到配套的原生 DLL，`dotnet publish` 只会
  产出 win-x64；将来要放开时删两行属性、再补 Linux/macOS 的图标路径即可。
* 图标「一份文件两个消费者」：Explorer 与 SDL 看到同一份像素，不会出现「exe 是新图标、任务栏还是
  旧的」这种只在截图里才发现的偏差；换美术也只剩「跑一次脚本」。
* 小尺寸用 DIB 而不是 PNG 是为了兼容性（部分外壳程序只认 DIB 或只认 256 的 PNG），而
  `IcoFileTests` 用内存里现搭的 ICO 把 DIB 的两页高度、自下而上的行序、4 字节行对齐、AND 掩码
  与「32 位 alpha 全零则改用掩码」逐条钉死，改坏了立刻红。
* 图标编进程序集：发布目录里没有「图标丢了」这种状态，也不需要往输出目录复制素材。

**代价 / 约束**：

* Windows 之外现在不是「没测试」而是「明确拒绝」：`--help` 都不给，直接退出码 1。
* `IcoFile` 不是 fheroes2 的移植（原版把图标编进资源，运行时不需要解析 ICO），所以
  `REFERENCE.md` 里没有对应行；它只与 `PngReader` 和生成脚本互相约束。
* 源图是 24 位（没有 alpha），四角不透明。换成带透明边的美术时脚本与解码器都已支持，但要重跑
  `GenerateAppIcon.ps1` 并提交新的 `.ico`。
* 图标没有像素门禁：`VerifyMenuBaseline.ps1` 只比对 640×480 的逻辑画面。窗口图标属于 Win32 层，
  能拿到的证据只有「`ExtractAssociatedIcon` 取得出图标」与日志里的 `Window icon set`。

## ADR-026：SDL 宿主项目更名为 `FHeroes2.App`

**背景**：分层表里其余项目都是「一个词」（`Core` / `Assets` / `Rendering` / `Game` / `UI`），
只有组合根叫 `FHeroes2.Client.Desktop`。这两段都是误导：`Desktop` 在 .NET 里通常指
`net10.0-windows` 加 WPF/WinForms，而这里只有 SDL3；`Client` 又预设了一个并不存在的服务端，
ADR-025 把平台收口到 win-x64 之后更没有保留的必要。

**决策**：目录 `src/FHeroes2.Client.Desktop` → `src/FHeroes2.App`，工程文件
`FHeroes2.Client.Desktop.csproj` → `FHeroes2.App.csproj`，根命名空间
`FHeroes2.Client.Desktop` → `FHeroes2.App`（子命名空间同理：`App.Fonts` / `App.Graphics` /
`App.Localization` / `App.Scenes`）。**程序集名与产物名保持不变**：`AssemblyName` 仍是
`FHeroes2Sharp`，可执行文件还是 `bin/Debug/net10.0/FHeroes2Sharp.exe`。

**理由**：

* 名字直接说明职责：`App` 是组合根，引用 `Core` / `Assets` / `Rendering` / `Game` / `UI` 并
  驱动它们；`dotnet run --project src/FHeroes2.App` 与 `using FHeroes2.App.Scenes;` 也更短。
* 保留 `AssemblyName` 是刻意的：`VerifyMenuBaseline.ps1`、CI 与所有截图命令都按 exe 名找产物，
  改 exe 名会把「改项目名」变成一次构建产物的变更——那才是真正需要重新回归的部分。

**代价 / 约束**：

* 纯机械重命名：`.slnx`、`VerifyMenuBaseline.ps1` 的工程路径（脚本内变量随之改为
  `$appProject` / `$appExe` / `$appArguments`）、文档与 `REFERENCE.md` 映射表里的项目名和命名空间
  引用全部同步，`git mv` 保留历史。
* 语义上它仍然是「客户端」：文档里说「客户端渲染」时指的就是这个项目，命名空间写 `App`，两者
  不冲突。
* 没有行为变更，所以没有新增验证项：与图标那次一样，证据是构建、单元测试与
  `VerifyMenuBaseline.ps1` 三件套。

## ADR-027：设置对话框先只做「语言」，选项在 OK 时才生效

**背景**：主菜单左下角的门在原版里是 `dialog::openGameSettings()` 的入口。那个窗口有分辨率、
音量、全屏、快捷方式、语言等十余组控件，其中分辨率/全屏要动 SDL 的窗口与缩放（`SdlPresenter`
目前只在构造时接收这两个值），音量要接音频后端（M2 尚未接），所以一次补齐只会得到一堆画得像
却不起作用的控件。

**决策**：

1. 先移植**一个**真正会生效的选项：界面语言（`GameText` 已经有 `zh_CN` / `en` 两个实例，
   主菜单的 `F2` 也在切换它）。窗口尺寸由字体与按钮高度推导（`SettingsDialog.GetRequiredHeight`），
   加行不用改版面代码。
2. 选项行不做带箭头的 `OptionButtonGroup`，而是「标签 + 右对齐的值 + 两侧箭头」：按下与松开落在
   同一行的左半格退一格、右半格进一格，出界循环。箭头是**自绘像素线**（宽 4、高 7、色号 10 =
   `KB.PAL` 里字体灰阶最亮的一级）：参考实现的箭头是 `ICN::BUTTON_SMALL_*` 之类还没移植的美术，
   而 `FONT.ICN` 自带的 `<` / `>` 只有 3×4 像素、缩在行里几乎读不出来（第一版就是拿这两个字形
   画的，从截图里才发现画成了两个小点）。自绘线不依赖额外资源，也让「值」可以直接是中文。
3. 选项在 **OK / Enter** 时一次性应用，Escape 丢弃。参考实现是「改一下就立刻生效」，但语言一改
   窗口里的文字就得换语言，而语言的切换在端口里是「换一个 `GameText` 实例」，中途换会让对话框
   自己的一半文字是旧语言——延迟到关闭时应用是更小的偏离。
4. 对话框的 OK 按钮用 `SYSTEM.ICN` 的第 1、2 帧。参考实现的 `ICN::UNIFORM_GOOD_OKAY_BUTTON`
   是**运行时生成**的（`EMPTY_GOOD_BUTTON` + `BUTTON_GOOD_FONT_RELEASED` 文字），而那套按钮专用
   字体表（`BUTTON_*_FONT_*`，行高 15）还没移植；`SYSTEM.ICN` 那两帧正是参考实现
   `useOriginalResources()` 分支用的同一张图，所以外观仍然是原版按钮。
5. 模态框归 `MainMenuScene` 持有（`Settings` / `OpenSettings()`），不新建 `IScene`：参考实现
   也是 `Game::MainMenu()` 里的一个模态循环，且这样门的光晕、招牌状态、右键提示都不必跨场景保存。
6. 新增 `--open-settings`：模态对话框的截图需要真实鼠标才能点开，命令行开关把它变成可复现的
   smoke 运行（与 `--cursor` / `--right-button` 同一思路）。

**后果**：

* 界面语言从「F2 盲切」变成「看得见当前值、能选的语言」；设置窗口、按钮、文字都是原版资源。
* 分辨率/全屏/音量仍然没有入口。它们需要 `SdlPresenter` 支持运行时切换与音频后端，属于 M8
  （「存档/设置」）的范围，`SettingsDialog` 只要再加几行就能接上。
* 文字按钮的标签仍缺 `BUTTON_*_FONT_*` 字体；`ButtonArtwork` 已经把拉伸与闪光做完，补字体表
  即可生成与 `UNIFORM_GOOD_OKAY_BUTTON` 等价的按钮。
* 证据：UI 111 条测试（新增 41 条：`ButtonArtworkTests` 12、`DialogButtonLayoutTests` 7、
  `DialogButtonTemplateTests` 9、`SettingsDialogTests` 13）、Core 167 条（新增 8 条
  `SpriteOpsTests`），`menu.png` 仍逐像素相同；设置对话框那一帧已单独入库成
  `tests/baselines/settings.png`（ADR-028），与纯菜单相比 42,680 个像素不同，其中 42,679 个在
  窗口矩形（348×133）内、1 个在矩形外的阴影边缘。

## ADR-028：像素基线脚本共用一个管线，设置对话框单独入库一份基线

**背景**：`VerifyMenuBaseline.ps1` 里八成代码（数据目录查找、`dotnet build`、基线自检、
`AssetProbe compare` + 容差判定）与「菜单」无关。设置对话框落地后，仓库里多了一张值得钉死的
画面（`--open-settings` 那一帧：窗口边框、标题、行文字、箭头、OK 按钮一次全在），照抄一份脚本
等于把同一套退出码 / 容差 / 缺数据跳过策略维护两遍。

**决策**：

1. 抽出 `tools/scripts/PixelBaseline.ps1`（只定义函数，调用方 dot-source）：`Resolve-DataDirectory`、
   `Invoke-Build`、`Read-BigEndianInt32`、`Assert-PngBaseline`、`Invoke-PixelRender`、
   `Invoke-PixelCompare`、`Complete-PixelCompare`。两个 `Verify*Baseline.ps1` 只留参数块、
   「渲染什么」与「忽略哪块区域」。
2. 拆分**不改行为**：菜单脚本的参数、默认值、输出行（`render menu` / `match` / `DIFF` / `skip`）
   与退出码保持原样，`menu.png` 依旧逐像素相同——纯重构不该动像素，这本身就由基线兜住。
3. 设置基线用同一条命令行加 `--open-settings`，同样 `--freeze-animation --cursor 620,100
   --ignore 0,469,640,11`；文件是 `tests/baselines/settings.png`，语言默认 `en` 并写进文档，
   因为行里是界面文字，**语言属于基线**。
4. CI 的 `menu-baseline` job 里加一个 step 跑第二份基线；job 名保持不变（分支保护与外部脚本
   按名字引用它），文档里说明名字是历史原因。

**后果**：

* 再加一份基线（战场、城堡画面）只要 ~90 行脚本 + 一张 PNG，失败输出格式与 artifact 位置
  也自动一致（`DIFF   N of M pixels differ, tolerance is T` + `diff/diff_<name>.png`）。
* 设置基线拦得住窗口位置/尺寸、标题颜色、行距、箭头形状与按钮贴图的变化；它**不**拦语言内容
  （换语言就是换基线），也不拦 hover / pressed 态（光标固定在窗口外）。

## ADR-029：设置行文字裁在行内边距里；设置基线按语言分文件

**背景**：设置行是「标签 + 右对齐的值」。第一版把值画在 `row.Right - RowPadding - 值宽`，标签也不
裁剪：译文一旦比内容区宽，值就会从右往左越过左内边距、压在标签上，甚至画到窗口边框外面。另一件事
是 `settings.png` 只有 `en` 一份——中文界面上屏后（项目自带 `zh_CN.mo`）没有任何像素兜底，而脚本
只会写同一个文件名，换语言就是覆盖掉另一种语言的基线。

**决策**：

1. 行文字只画在 `RowPadding` 之内：`GlyphText.DrawLine` 的裁剪区改成 `TextArea(row)`（行去掉左右
   内边距），值的起点再夹到 `max(右对齐位置, row.X + RowPadding)`。译文过长时表现为「截断」，不会
   压住标签或溢出窗口；行太窄时仍然先丢箭头（`ChevronsFit`）。
2. `VerifySettingsBaseline.ps1` 的默认基线由 `-Culture` 推导：`en` → `settings.png`，其它 →
   `settings.<culture>.png`；渲染出的临时图同样带语言后缀。`-Baseline` 仍可显式覆盖。
3. CI 跑三条：菜单、设置 `en`、设置 `zh_CN`。`zh_CN.po` 顺手补上 `msgid "Language"`，让中文基线里
   的行标签真的是中文（统计从 95 条变 96 条）。
4. `tools-output/baseline/*.png` 整体作为失败 artifact 上传，不再逐个点名文件。

**后果**：

* `settings.png` 逐像素未变（`en` 行文字本来就装得下），证明裁剪是纯防御性的：只影响超宽的情况。
* 加一种语言的基线 = 一句 `-Culture <名字> -Update` 加 CI 列表里的一行，不需要新脚本。
* 取舍：超宽译文是「看得见的截断」，不是省略号也不是自动缩字号。参考实现把文字塞进按需生成的
  按钮精灵里（`BUTTON_*_FONT_*` 那套按钮专用字体）；本项目连那套字体都没移植（ADR-027 第 4 条），
  截断是当前信息量最大的降级方式，且不会破坏窗口框架。

## ADR-030：音频走 SDL3_mixer；音轨只在 `MUSIC/` 里找，音效从 AGG 取原始 PCM

**背景**：M2 只剩 BGM / 音效没做。参考实现的 `audio_manager.cpp` 有两条完全不同的链路：
音乐是「外部文件优先（`.ogg/.mp3/.flac`，三套命名），否则解析 AGG 里的 XMI 并用 SDL_mixer 的
MIDI 合成播放」，音效是「从 AGG 取 `<名字>.82M` 的裸 PCM，补 44 字节 WAV 头后当内存音频播」。
XMI 解析 + MIDI 合成器是一个独立的大工程（`xmi.cpp` 外加 mt32/fluidsynth 之类的合成后端），
而本机安装（GOG 版）的 `MUSIC/` 里 42 条 `homm2_NN.ogg` 是齐全的。

**决策**：

1. 后端用 SDL3_mixer 3.x：NuGet 上是 `SDL3-CS.Native.Mixer`（只有原生库），绑定已经在
   `SDL3-CS` 包里（`SDL3.Mixer` 静态类），所以不引入新的绑定项目。`App.Audio.AudioPlayer`
   独占 mixer 生命周期：`SDL_InitSubSystem(Audio)` → `MIX_Init` → `MIX_CreateMixerDevice`，
   释放顺序反过来。
2. 音乐只走外部文件。`Assets.Audio.MusicTrackCatalog` 把曲目 id 映射成文件名（DOS
   `homm2_NN`、Windows `TrackNN`、Mapped `NN <描述>`，扩展名 `.ogg/.mp3/.flac`，顺序照抄
   `MUS::getFileName` 与 `getExternalMusicFile`），搜索目录是数据目录本身、它的 `MUSIC`
   子目录、以及数据目录**父级**的 `MUSIC`（GOG 版布局是 `Data/` 与 `MUSIC/` 平级）。没有
   外部音轨时不放音乐，只记一条日志——不做 XMI/MIDI 兜底（留给后续里程碑）。
3. 音效不抄 `m82.h` 的 300 项表：`Assets.Audio.GameSound` 把调用方给的名字（`PICKUP01`）
   转成 AGG 条目（`PICKUP01.82M`），`WaveSound.FromGameSound` 补 RIFF 头（8 位、单声道、
   22050 Hz，逐字段照 `LoadWAV`）。`AudioPlayer.PlaySound` 给每个音效单独建 track，播完在
   `Update()` 里回收，避免互相打断。M2 还没有可点的玩法，所以 `--play-sound <NAME>` 是当前
   唯一的验证入口（不发明「菜单点击音」，参考实现也没有这个音）。
4. 一切都软失败：没有声卡、`MIX_Init` 失败、音轨缺失都只 `GameLog.Warn`，客户端继续跑。
   `--no-audio` 连音频子系统都不初始化；两个像素基线脚本都带上它，回归渲染与有没有音轨、有没有
   声卡彻底无关。

**后果**：

* 主菜单上屏即循环播 `homm2_41.ogg`（`MUS::MAINMENU` = 42，DOS 命名减一），换场景切曲要等
  有第二个场景（M3）。
* 音量还没有入口：`AudioPlayer.MusicVolume` / `SoundVolume` 已在（0..1，音乐用 track gain、
  音效用各自 track 的 gain），设置对话框接上它们是 M8 的事。
* 只支持外部音轨意味着「只有原版 CD/AGG 没有 `MUSIC/` 目录」的安装会是静音的；`PROGRESS.md`
  把 XMI/MIDI 记为已知遗留，而不是假装做完了。


## ADR-032：M3 从「地图文件头」开始，先读信息块再谈地图加载器

**背景**：M3 的验收是「能创建新对局并进入冒险地图（占位渲染）」，起点是原版的场景选择对话框
（`dialog_selectscenario.cpp`）。它需要的信息全部来自 MP2 文件的前 428 字节（`MP2_MAP_INFO_SIZE`）：
名字、描述、尺寸、难度、六种颜色的王国／人类／电脑归属、初始种族、胜负条件与其参数。真正的
tile／addon 解析（`world_loadmap.cpp` + `maps_tiles.cpp`，还包括对象表 400 KB 的
`map_object_info.cpp`）是后面冒险地图渲染的活，与「列出一张地图的摘要」没有依赖关系。

**决策**：

1. 先做纯数据层：`Assets.Maps.Mp2FileInfo.Read` 只读文件头，逐字段照 `readMP2Map`（字节偏移、
   LE/BE、`seek(29)` 之后那段胜利／失败条件、`seek(58)` 名字 16 字节、`seek(118)` 描述 200 字节），
   `FileInfo` 的派生量（`HumanOnlyColors` / `ComputerOnlyColors` / `AllowCompHumanColors`）用同一个
   位集合语义实现。文件不是合法地图时抛 `Core.Io.BinaryFormatException`，扫描用 `TryRead` 吞掉。
2. 枚举值照抄参考实现：`PlayerColor`（`color.h` 的 1/2/4/8/16/32）、`MapRace`（0…7 →
   KNGT/BARB/SORC/WRLK/WZRD/NECR/MULT/RAND）、`GameDifficulty`（0…4，MP2 只存 0…3，
   `IMPOSSIBLE` 只出现在玩家选择里）、`MapVictoryCondition` / `MapLossCondition`、
   `MapVersion`（`.mp2` = 原作，`.mx2`/`.hxc` = 资料片）。
3. 目录搜索照 `Maps::getAllMapFileInfos`：数据目录本身、它的 `maps` 子目录，另外**只有**当这个目录
   真的是数据目录（含 `Heroes2.agg`）时才看它的父级——GOG 版布局是 `Data/` 与 `Maps/` 平级，
   而写死父级会在用户随手指一个临时目录时去扫无关目录。有效性过滤（`getValidMaps`）与「人类颜色
   正好等于玩家数时收窄可选颜色」的规则都照搬，被丢掉的文件的理由记在 `MapScanResult.Rejected`。
4. 诊断入口是 `AssetProbe maps`，不往客户端加命令：M3 的 UI 还没写，先保证数据层能对着真实安装
   （本机 112 张可玩图）看输出。

**已知偏差**（写进 `PROGRESS.md`，不假装做完）：

* ~~地图字符串按 Latin-1 解码。~~原版编辑器用的是卖家的 DOS 代码页，本机中文图的 GBK 名字会显示成
  乱码；接 UI 时按 ADR-034 换成了 `MapTextCodec`（UTF-8 → GBK → Latin-1 探测）。
* `readMP2Map` 里「单人类玩家 + 失去某英雄即败 + 该英雄站在电脑颜色格子上」会把胜利条件改写成
  `VICTORY_DEFEAT_OTHER_SIDE` 并建立盟友。它需要读 tile 数据与英雄精灵颜色表（`HEROES.ICN`
  前 42 帧的分组），等地图加载器落地后再补；本机 4 张图受影响，`AssetProbe maps` 会把这个数字
  打出来，避免静默分叉。

**后果**：

* 场景选择对话框可以直接从 `MapCatalog.ReadAll(...)` 拿数据，不必等 `world_loadmap`。
* `.fh2m`（fheroes2 自己的地图格式）不支持，`MapVersion.Resurrection` 只是占位；
  战役场景的 `campaign_scenariodata.cpp` 同样要等 MP2 tile 解析。
* 文件头之外的数据（tile、addon、城堡／英雄／事件结构）仍必须由 M4 的地图加载器读，
  `Mp2FileInfo` 不是「地图」本身，只是它的名片。


## ADR-033：场景选择窗口照参考布局自绘，`NEW_GAME` 直接开列表

**背景**：`Mp2FileInfo` / `MapCatalog` 已经能列出本机 112 张可玩地图（ADR-032），但它们还没有
消费者。参考实现里这条路是 `Game::MainMenu()` → `NEW_GAME` → `Dialog::SelectScenario`
（`dialog_selectscenario.cpp`）：377×458 的 `REQSBKG.ICN` 面板居中，顶部五个尺寸按钮，中间
30 行的 listbox（宽 364，行内放玩家数／尺寸／类型／名字／胜负图标），右侧滚动条，底部选中
地图的难度与描述、`SYSTEM.ICN` 的 OKAY。它还有「标准局 / 战役」分叉与开局对话框（难度、玩家）。

**决策**：

1. 面板、17×17 图标、滑块、OKAY 帧全部用归档资源，坐标逐条照 `dialog_selectscenario.cpp`
   （列表 55,55、行内 48/67/86/104、胜负 266/285、详情 42,318 292×90、OKAY 140,410）。
   列表视口用 9 行 × 19 px，而不是参考的 30 行 × 12 px：`FONT.ICN` 的 small 字体加上 CJK 回退
   在 12 px 行高里会互相顶到，19 px 是本项目 `SettingsOption` 已经验证过的行高。
2. 列表只有一份，尺寸筛选（ALL/S/M/L/XL）重建视图并保留选中项，不照参考分成五个 bucket：
   `MapCatalog` 已经排好序，筛选只需一次 `Where`，112 张图的内存代价可以忽略。
3. `MAP_TYPE_ICON` 不是归档里的表：参考在启动时把 `ARTFX.ICN` 帧 6/90/101 缩放到 15×15 再放进
   17×17 透明格。`App.Scenes.ScenarioArtwork` 做同一件事，UI 层只收三张 17×17 精灵。
   只有扩展包 `Heroes2x.agg` 才有 90/101 帧，纯原作数据下 PoL/Resurrection 徽章留空。
4. 难度只读显示，不弹参考里的开局对话框：改难度要等 M4 的地图加载器（`world_loadmap`）与
   `Game::Setup`。`NEW_GAME` 也跳过「标准局 / 战役」分叉，直接开场景列表；分叉等战役数据。
5. 键盘由宿主转发：`ScenarioSelectionDialog` 只认 `MoveSelection(±n)` / `MovePage(±1)` /
   `Select(index)` 这类语义动作，`GameHost.HandleKeyDown` 把 SDL 的 Up/Down/PageUp/PageDown/
   Home/End 映射过去。UI 层因此不依赖 SDL 键码，和鼠标、测试三边都解耦。
6. Escape 的语义统一成「先关最上层模态窗口」：设置对话框或场景列表打开时 Esc 只关它们，
   只有空菜单上的 Esc 才退出客户端（顺带修掉 M2 遗留的「设置打开时 Esc 直接退出」）。
   同理，`F2` 在模态窗口打开时不生效：它会重建菜单文字，而窗口里的文字是打开时取的一份快照。
7. `--open-scenario` 是截图入口，不新增玩法命令：窗口只能点 NEW GAME 招牌打开，脚本没有鼠标。
   `scenario.png` / `scenario.zh_CN.png` 因此成为第三、第四份像素基线（门禁见
   `tools/scripts/VerifyScenarioBaseline.ps1` 与 `tests/baselines/README.md`）。

**后果**：

* 从主菜单到场景列表已经全链路可用，`ConfirmScenario()` 把 `Mp2FileInfo` 交回宿主；M4 接上
  地图加载器后只需替换那里「只改状态栏」的实现。
* 参考里有、这里没做的：双击确认、Debug 版的 Delete 删文件、滚动条上下箭头的点击（只画
  `REQUESTS.ICN` 5/7 帧）、难度选择与「标准局 / 战役」分叉。都记在 `PROGRESS.md`，不假装做完。
* 中文界面能同框显示 `.mo` 译文与地图文件里的英文描述：地图文字是数据，不交给翻译层。
* **修正（实现后对照参考复查）**：第 1 条里「列表视口用 9 行 × 19 px」是错的。参考的
  `ListBox::Redraw()` 是 `rtAreaItems.y + i * height / maxItems`，即 `i*175/9` 截断（19、38、
  58、77 …），不是固定 17/9 = 19 的整数倍；图标也画在行顶（没有 4 px 偏移），只有名字下沉 3 px。
  第一版把 `MAP_LIST_ROW_SPACING_Y` 当成图标偏移，导致内容整体低 4 px、最后一行压到框沿、命中
  区域跟着漂。已按参考改正并补了 UI 测试，细节见 `PROGRESS.md` 的「M3 修正记录」。


## ADR-034：MP2 文本按「UTF-8 → GBK → Latin-1」的顺序自动判编码

**背景**：MP2 的 428 字节头里，名字（45 字节）、描述（13 行 × 41）与胜负条件都是**裸字节**，
没有任何编码标记。原版用数据集的 DOS 代码页把它画出来；本项目早期直接用 `Encoding.Latin1`
解码，于是中文图包（GBK）的名字与描述变成一串 `?`——`AssetProbe maps` 里的 `Slugfes.mp2
¼¤Õ½` 就是 `激战` 被当成 Latin-1 的结果，界面上更是整块不可读（见 M3 修正记录）。

**决策**：

1. 用一个显式的 `MapTextCodec` 负责这三个字段，判定顺序固定为：纯 ASCII → 严格 UTF-8（且必须
   真的解出多字节字符）→ 严格 GBK（能解、回环编码字节一致、且至少 2 个汉字）→ Latin-1。
   每一步都要么完全成功、要么整体放弃，不做逐字节猜测。
2. GBK 的门槛是「至少 2 个汉字」而不是「解得出就行」：CP1252 的 `Château` 里 `E2 74` 恰好也是
   一个合法 GBK 字，一个汉字的门槛会把它变成「Châ+汉字+eau」。这条边界有单测钉住。
3. 不做自动探测的「学习」或配置项：地图文本是数据，界面语言是设置，两者不联动；中文图包在
   英文界面下也照原样显示中文（`ADR-033` 的后果里已经说明地图文字不交给翻译层）。
4. `System.Text.Encoding.CodePages` 的 `GBK`（936）在运行时才注册，取不到时 `SupportsGbk` 为
   false，退化为「UTF-8 → Latin-1」，不会崩；对应的单测在这种情况下直接跳过。

**后果**：

* 中文图包的名字、描述、胜负条件都能正常显示，`MapCatalog` 按新名字排序，`scenario*.png`
  基线因此重建（哈希已更新）。
* 仍然是启发式：一串**同时**是合法 GBK 且解出两个以上汉字的 CP1252 文本会被误判成中文——这种
  组合在真实地图里没有出现，真出现时也只能靠人工改名。
* `--scenario-select <n>` 是这次顺手加的诊断开关（开窗并选中第 n 张图），用来给列表末尾的中文
  图拍基线；它不改变玩法。

## ADR-035：M4 先做「结构层 + 地形层」，对象层单独一刀

**背景**：M3 之后要真正「进冒险地图」。参考实现里这一步跨三层：`World::loadMapMP2`（文件 →
`Maps::Tile` 部件模型）、`Maps::redraw*Layer`（部件 → 像素）、`Interface::GameArea`（滚动与面板）。
`Maps::Tile` 的部件模型（`pushGroundObjectPart` / `pushTopObjectPart` / `sortObjectParts` / 主对象
类型推断）是其中最重的一块，且只有它有输入交互（信息框、拾取、路径）依赖。若一次全做，既没法
分层验证，也没法给地形层拍稳定的像素基线。

**决策**：

1. `FHeroes2.Assets.Maps` 只负责**文件结构**：`Mp2TileInfo` / `Mp2AddonInfo` / `Mp2ObjectType` /
   `Mp2ObjectIcnType` / `Mp2Map`。`Mp2Map` 不构建任何游戏对象（没有 Tile、没有 Castle、没有 Kingdom），
   也不解释信息块内容，只把字节交给上层。
2. 地形层单独一步：`AdventureMapView` 直接把 `Mp2TileInfo.TerrainImageIndex` 映射到
   `GROUND32.TIL`，`TerrainShape`（`TerrainFlags & 3`）决定四个预翻转变体之一，与参考
   `getTileImage(TIL::GROUND32, index, shape)` 一一对应。这层不依赖任何部件模型，因此可以先上。
3. 对象层（下一刀）才引入 `Maps::Tile` 的部件模型：addon 链展开成部件、按 layer 排序、主对象类型
   由 `mapObjectType` 与 addon 共同决定。地形层保持不动，只在它上面加绘制。
4. 没有英雄之前，视口居中规则是占位：「最靠地图中心的城堡」。有玩家/英雄后立刻换成参考的规则
   （玩家英雄，其次玩家城堡），`AdventureMapScene.TryCreate` 是唯一改动点。
5. `--map <file>` 与 `--enter-map` 是为可验证性付的成本：前者让地形基线只依赖一个地图文件，后者
   端到端跑通「NEW GAME → 列表 → OKAY → 冒险地图」而不需要鼠标。

**后果**：

* 结构层可以被独立钉死：`Mp2MapTests` 用合成地图覆盖每条偏移与拒绝路径，真实数据抽查 `Beltway`；
  `AdventureMapViewTests` 用 2×2 像素的假 TIL 钉住滚动数学与 tile 循环。
* 当时的冒险地图**没有对象**（地形图里内嵌的装饰照原样出现，与参考一致）；对象层随后按同一刀法
  补上，见 ADR-036。
* `Mp2Map` 的 API 是**增量**的：对象层只需要新增消费方，不需要改读取器；但 `Mp2InfoBlock` 目前是
  原始字节，解析城堡/英雄/事件时要加各自的解码器，且必须保持「块索引 = `Metadata`」这条链。
* 如果之后发现 `Metadata` 与 tile 的匹配必须依赖主对象类型（`getMainObjectType` 与
  `mapObjectType` 可能不同），那属于对象层的工作，届时在部件模型里解决，而不是往 `Mp2Map` 里塞近似值。

## ADR-036：对象层不移植 `map_object_info` 的大表，只按文件里的部件逐层绘制

**背景**：对象层的关键问题是「一个对象由哪些图块、画在哪里、按什么顺序」。参考的答案分两半：
一半在**地图文件**里（每个 tile 记录自己的地面对象 ICN 类型 + 帧号 + 层，addon 链记录同一对象
其余部件），另一半在 `map_object_info.cpp` 的**手写数据表**里（`objectInfo` 与每个对象类型的
`animationFrames`、部件偏移、主对象类型推断）。那张表 41 万字节，是编辑器与动画用的字典，不是
「画对静态画面」的必要条件。

**决策**：

1. 只用文件里的数据画：`Mp2TileInfo` 与 `Mp2AddonInfo` 的 (ICN 类型, 帧号) 就是每个部件的图，
   ICN 帧头自带的锚点就是落位偏移（`BlitOnTile(..., sprite.x(), sprite.y())`）。
2. 绘制按**层**遍历整个视口，不按 tile：地形 → layer 0..3 → 顶部层，与
   `Interface::GameArea::Redraw()` 的调用顺序一致。逐 tile 画会把「对象伸到下一格」的像素被下一格
   的地形盖掉，这是肉眼可见的差异，所以顺序不是风格问题。
3. `Mp2ObjectIcnTypes` 负责枚举 → AGG 条目名（64 项，一对一，`Unused*`/`Unknown` 没有条目，
   `X_LOC*` 需要 `heroes2x.agg`），并复刻 `isObjectPartDirectRenderingRestricted` 的跳过集合。
4. 图块按需解码并缓存（`ObjectSpriteSet`），缺失文件只记一次，不让每 tile 重试；绘制逻辑只依赖
   `IObjectSpriteSource`，所以对象层可以在没有游戏数据的机器上单测。
5. 不移植的部分明确列为遗留：动画帧表、`tileUnfit`（超过 1×1 tile 的对象整体上移再画）、
   主对象类型推断（`Maps::getMainObjectType`）、英雄/怪物/船的专属渲染器。

**后果**：

* 静态画面与参考一致到「每个部件用哪个帧、画在哪、谁压谁」，但**动画不动**：火把、旗帜、水波纹、
  烟在参考里会随 `animationFrame` 切换帧，这里停在 MP2 里存的静态帧。`--freeze-animation` 的基线
  因此不受影响。
* **超大对象可能被下一格地形切掉一条边**：参考把这类对象记进 `tileUnfit` 并在 `redraw` 末尾整体
  重画一遍。图上暂未观察到（`Alteris` 一屏的城镇、山脊都完整），一旦出现，补的是 `tileUnfit`
  收集 + 末尾重画，不需要改现在的分层结构。
* 英雄/怪物/船不画：它们的精灵由引擎按 `MINIHERO`/`MINIMON`/`BOAT32` 拆块，走通用部件路径会画出
  错位的碎片。等玩家与英雄进入视野时，按参考的做法加专属渲染器，而不是把这三类 ICN 塞进部件循环。
* 主对象类型推断缺失意味着**拾取与信息框还开不了工**：那两者要看「这个 tile 的 main object 是什么」，
  到时候需要 `map_object_info` 的对象表——但只需要其中「ICN 类型 + 帧号 → 对象类型」的映射，
  不需要动画帧数，可以按需生成一张小表。

## ADR-037：英雄按「三带」绘制，身体取种族图集、旗帜取 `<色>-FLAG32`、阴影取 `SHADOW32`

**背景**：英雄（连同船、地图怪物）在 MP2 里不是普通对象：每个 tile 只留一个 `MINIHERO` 帧号，
真正的像素来自三张不同的图集——身体按种族取 `KNGT32`/`BARB32`/`SORC32`/`WRLK32`/`WZRD32`/`NECR32`，
旗帜按王国颜色取 `B-FLAG32`/`G-FLAG32`/`R-FLAG32`/`Y-FLAG32`/`O-FLAG32`/`P-FLAG32`，阴影取
`SHADOW32`；`MINIHERO` 本身属于编辑器，只用来表达「谁站在哪」。参考实现
（`Interface::GameArea::Redraw()` 的 `redrawHeroes`）因此不把英雄当成一层，而是把一张精灵
**按 32 px 行带切成三段**，分别塞进三个批次里画。

**决策**：

1. **颜色与种族从 `MINIHERO` 帧号反解**，不再单独存：`frame / 7` 给颜色（蓝绿红黄橙紫，与
   `MP2::heroColorsOrder` 同序）、`frame % 7` 给种族（`Race::IndexToRace` 的 0…6），读取集中在
   `Mp2MapHeroes.FromSpriteIndex`；越界帧号直接抛 `BinaryFormatException`，不做静默兜底。
   每张图集是「六个方向 × 9 帧 + 转身 9 帧」，静止英雄朝右，所以身体、旗帜与阴影一律取第 18 帧
   （`HeroSpriteSet.StandingFrame`）——与参考 `Heroes::_direction` 的默认值一致。
2. **三段按行切，不按图切**（`AdventureMapView.HeroBand`）：`AboveTile`（英雄自己那格之上）、
   `OnTile`（本格）、`BelowTile`（本格之下）。绘制时机照参考的三处插入点：
   地面层 3/1/2/0 里画到 `SHADOW_LAYER`(2) 之后 → 英雄阴影 + `BelowTile`；四层全部画完 → `OnTile`；
   顶部层画完 → `AboveTile`。
   于是「塔楼压住旗、英雄盖住自己格上的路」这类遮挡关系自动成立，不需要给每个对象标
   「谁和英雄抢像素」。
3. **阴影单独一层且按 40 px 左移**：`SHADOW32` 帧自带 `OffsetX ≈ -40` 的锚点，直接按 tile 落位即可，
   它排在 `SHADOW_LAYER`(2) 之后、`OBJECT_LAYER`(0) 之前——参考把影子放进 `tileUnfit.shadowImages`
   就是为了让路、草、水波先铺好再压影子，而树、山、城堡又能盖住影子。
4. **图集与对象层共用一个解码缓存**（`IcnSpriteCache`）：`ObjectSpriteSet` 与 `HeroSpriteSet` 都从
   `AssetBundle.TryReadIcn` 后缓存在同一个字典里，`GROUND32.TIL` 那套「载入一次、按帧取」的约定不变。
5. **视口裁剪扩到 2 tile**：英雄精灵高约 1.5 格、阴影再往左伸一格，所以 `VisibleHeroes()` 在
   `VisibleTileArea` 基础上左右各放 2 格再测试，超出部分交给 `ImageOps.BlitSprite` 的裁剪。

**后果**：

* `MINIHERO` 不再进入对象部件循环（`Mp2ObjectIcnTypes.IsDrawnAsObjectPart` 已经把它排掉），所以
  以前「英雄会画成错位碎片」的问题从根上消失；`MONS32`（编辑器静态怪物）与 `BOAT32`（船）虽然也被
  同一套规则排掉，但它们还没接专属渲染器，暂时**不可见**。
* 英雄**不参与动画**：参考的旗帜飘动、英雄待机帧依赖 `animationFrame` 与 `map_object_info`，
  与 ADR-036 同一批遗留，`--freeze-animation` 的基线不受影响。
* 种族为 `Random` 的英雄（帧号 `%7 == 6`，即每个王国的第 7 帧）没有身体图集，只有旗帜会被画出来。
  参考在开局后会把随机种族落成具体种族，本端口还没有「玩家 → 英雄」这一步，先保持「不猜」。
* 一名英雄占一格，`Mp2MapHeroes` 按 tile 顺序输出；两英雄同格的地图不存在（参考同一位置只允许
  一个英雄），所以不做叠放排序。
* 像素基线随这次改动重建：`adventure-map.png` 的 4,495 个像素差全部落在英雄与「层序」两处，
  见 `PROGRESS.md` 的 M4 英雄层验证记录。

## ADR-038：小地图的颜色从 tile 字段直接算，不依赖对象表；面板先叠加在占位布局上

**背景**：参考的雷达（`Interface::Radar`）把整张地图压成 144×144 的方块：每格一个调色板索引，
地面色按 `Maps::Ground::getGroundByImageIndex` 从 terrain image index 反解，山与树提亮三档，道路单独
一色，英雄与城堡用王国色，再叠一个标出当前视口的虚线方框。颜色本可以查 `map_object_info` 的
`objectType`，但那张大表正是 ADR-036 里刻意没移植的东西。

**决策**：

1. **只用 tile 里已有的字段**：地面类型由 `TerrainImageIndex` 落在
   `[0,30) [30,92) [92,146) [146,208) [208,262) [262,321) [321,361) [361,415) [415,432)` 的哪一段决定
   （`MapGrounds.FromTerrainImageIndex`，与参考的九个 `*_START_IMAGE_INDEX` 一一对应）；道路看
   `GroundIcnType == Road`，山与树看 `Mp2ObjectIcnTypes.IsMountain/IsTree`，英雄看 `Mp2MapHeroes`
   （`Mp2MapHero` 已经有颜色）。**因此小地图先于对象表落地**，等 ADR-037 那条遗留（主对象类型推断）
   补上时，这里只需要把「山/树」换成 `OBJ_MOUNTAINS`/`OBJ_TREES` 判断。
2. **城堡与矿暂不着色**：MP2 文件里没有城堡归属、也没有矿主（两者都由玩家与对象表在后来的
   `Kingdom` 阶段确定），所以雷达上它们落在地面色里。参考对无主对象画白点，这里选择**不画**，避免
   把「未知」画成「白色王国」。
3. **缩放就是最近邻**：`Zoom = 144 / map.Width`（参考 `SetZoom` 用 `rect.width / world.width` 的浮点
   除法），雷达像素 `p` 取 tile `floor(p / Zoom)`。参考用 `memset` 把 tile `x` 铺进
   `[floor(x*Zoom), floor((x+1)*Zoom))`，两者逐像素等价。
4. **视口方框按参考的虚线画**：`fheroes2::DrawBorder(..., 6)` 是「每 6 个像素省一个」的 1 px 边框，
   颜色 `RADARCOLOR = 0xB5`；视口覆盖整张地图时**不画**（否则整圈边框反而误导）。
5. **布局先叠加，不抢占地图**：参考的地图区是 448×448 @ (16,16)，右侧与底部被 `ADVBORD.ICN` 面板
   占掉。本端口还没有面板美术（ADR 待补），所以雷达放在参考坐标 (480,16) 上**盖住地图右上角**，并
   用 1 px 黑框圈住面板。等界面外壳落地时，只改 `AdventureMapScene` 的布局常量，`Minimap` 本身不动。

**后果**：

* `Minimap` 是纯数据 + 一个 144×144 的 `PixelBuffer`，与地图、场景都解耦：`ColorAt(x, y)` 给测试用，
  `CursorArea(visibleTiles)` 给点击居中与调试用（点击交互要等鼠标事件管线，现在还没有）。
* 迷雾不存在，所以「全部可见」就是参考在迷雾揭开前的行为，`revealOnlyVisible` 的分支等迷雾落地再补。
* 每帧多一次 144×144 的 blit（约 2 万像素），与地形绘制不在一个量级，不做缓存失效判断。
* 像素基线再次重建：`adventure-map.png` 的 20,551 个差异像素全部落在雷达矩形 `(480,16,144,144)` 里
  （`first at 480,16`），其余 640×480 区域逐字节不变，见 `PROGRESS.md` 的 M4 小地图验证记录。

## ADR-039：地图编辑器一期不做

> **更正（2026-11-14）**：本 ADR 的**事实前提是错的**。fheroes2 1.1.18 有完整的地图编辑器，
> 位于 `src/fheroes2/editor/`（约 40 个源文件：`editor_interface.cpp` 164 KB、`editor_interface_panel.cpp`
> 72 KB、`editor_map_specs_window.cpp` 114 KB 等），入口是主菜单的 EDITOR 按钮
> （`Game::MainMenu()` 里 `ICN::BTNSHNGL` 第 20 帧，`EDITOR_DEFAULT = 20`，仅 `isPriceOfLoyaltySupported()`
> 时出现）→ `Editor::menuMain()` 编辑器主菜单 → `Editor::Interface` 编辑器界面；保存走
> `Maps::Map_Format::saveMap`（`maps/map_format_info.cpp:824`）。
> 因此「MP2 写路径没有参考可对拍」也不成立。**本文的决策（一期不做编辑器）仍然有效**——那是范围选择，
> 由 ADR-040 收窄为「先做只读可视化」——但**理由必须换成工作量与优先级，而不是「没有参考」**。
> 写路径将来按 `Editor::Interface` 与 `Maps::Map_Format::saveMap` 移植即可对拍。

**背景**：`PLAN.md` 的 M9 原本把「地图编辑器」与战役、视频并列，验收标准写着「编辑器可保存地图」。
但参考实现 fheroes2 1.1.18 **本身不含地图编辑器**（它是游戏引擎，地图由原版 `h2maped` 与第三方编辑器
生成），所以这条路径上没有任何 C++ 代码可以逐字节对拍。

**决策**：

1. **一期不做地图编辑器**，M9 只保留战役、音频完善、SMK 视频与 NativeAOT 评估。`PLAN.md` 的 M9 验收
   标准删掉「编辑器可保存地图」，并新增「一期范围之外」一节说明这件事。
2. **理由是对拍纪律而不是工作量**：本项目的第一条原则是「外部资源格式逐字节一致」，而每一处一致都是
   靠 `tools/reference` 把参考源码编成导出器、再 `AssetProbe compare` 逐字节比对钉出来的（ADR-015）。
   MP2 的**写**路径没有参考可对拍，只能靠「自己写 → 自己读 → 自己比」，这种自证循环没有说服力；
   与其含糊地做，不如明确不做。
3. **读路径的成果照常保留**：`Mp2Map` / `Mp2TileInfo` / addon 链 / 城堡与捕获物表都已解析（M3/M4），
   `AssetProbe map` 能打印整张图的结构，`Mp2MapFileBuilder` 能合成完整 MP2 给测试用。将来若要写地图，
   这两样正是「写出来能不能被自己读回」的第一道闸门，属于现成基础，不额外投入。

**后果**：

* 一期的范围收敛为「**能完整打一局**」：M5 回合与英雄 → M6 城镇 → M7 战斗 → M8 存档/设置 → M9 战役与
  收尾。编辑器若要做，单独立项，并且先补「写 MP2 的最小闭环 + 与原版编辑器的产物对拍」再谈界面。
* 文档里不再出现「编辑器可保存地图」这类验收措辞，避免读者以为一期会交付它。
* 玩家侧的诉求（改地图、造地图）一期由用户自带的原版编辑器满足：我们只读 `.mp2`，不写。

## ADR-040：编辑器先做只读可视化（E1），收窄 ADR-039 的「一期不做」

**背景**：ADR-039 把地图编辑器排除在一期之外，理由是 MP2 的写路径没有参考实现可对拍。实际运行后，
「看不到编辑器」成了最直接的体验缺口：能打开地图、能滚动、能指出每个 tile 的地形与对象，这些都不需要
写盘，却被一票否决了。用户于 2026-11-14 明确要求「先做编辑器，先能看到」。

**决策**：

1. **把编辑器拆成 E 系列，与 M 系列并行**，第一步 E1 只做**只读可视化**：`--scene editor [--map x.mp2]`
   直接进编辑器，画出地形与对象层，叠加 tile 网格、光标所在 tile 的高亮方框、工具条与状态栏（含
   `x,y` 与地形/对象名）、小地图，键鼠滚动全部复用冒险地图那套。
2. **渲染完全复用 `AdventureMapView`**，不复制一份「编辑器专用渲染」。这一点与参考一致：`Editor::Interface`
   也只是对同一个 `GameArea` 调 `SetAreaPosition( borderWidthPx, borderWidthPx, ... )`，没有第二套地图绘制。
   复用还意味着 M4 每修好一层（对象、英雄、动画），编辑器跟着变好，`editor.png` 基线也连带守住这些层。
3. **E1 不写路径**：不改 tile、不保存、不导出。E2（改地图、撤销）与 E3（保存 MP2）的参考对象是
   `Editor::Interface` 的编辑动作与 `Maps::Map_Format::saveMap`，**有可对拍的 C++ 代码**（见 ADR-039 的
   更正段），所以它们不是「先证明能写」的研究题，而是照参考移植的工程题；`Mp2MapFileBuilder` 与
   `AssetProbe map` 仍是有用的第一道闸门。
4. **入口是主菜单右下角的按钮**（原先写的是「只走命令行」，2026-11-14 按用户要求改）：位置与语义照参考
   的 `buttonEditor`（`BTNSHNGL` 20/21/22）。但**普通 Heroes II 数据只有 20 帧招牌**（编辑器是 PoL 的产物，
   参考也只在 `isPriceOfLoyaltySupported()` 时显示该按钮），所以 `BTNSHNGL` 没有这三帧时菜单自绘一块
   `EditorFallbackArea` 招牌（深底 + 亮框 + 字体文本，三态与招牌一致），有帧时用原版美术。两条路径共用
   同一个命中区与提示框。

**后果**：

* `PLAN.md` 增加 E1～E3 里程碑，M9 不再声称「编辑器不在本期」，而是指向 E 系列。
* 新增 `MapEditorView`（Game 层，可测）与 `EditorScene`（App 层，只画像素），新增 `--scene editor`、
  像素基线 `tests/baselines/editor.png` 与 `tools/scripts/VerifyEditorBaseline.ps1`。
* 主菜单右下角新增编辑器按钮（`MainMenuAction.Editor`、`MainMenuScene.EditorArea`），美术与自绘两条路径；
  `menu.png`、`settings.png`、`scenario.png` 及其 zh_CN 版本随之更新。
* 编辑器的**工具条与状态栏文字**仍是英文常量（`MAP EDITOR`、`G grid ON`、`drag / edge / wheel scroll`），
  未接入 `.mo`；按钮标题 `Editor` 与提示 `Create new or modify existing maps.` 走 `Text`，zh_CN 已有译文
  （显示为「地图编辑器」）。`editor.png` 因此只有一份基线，不按语言分叉。
* 已知缺口按「能不能看到」排序：网格颜色单一（深地形上对比低）、无地形/对象选择面板、无缩放、
  右键无作用、无「新建地图」。这些都写在 `PROGRESS.md` 的 E1 已知遗留里。

## ADR-041：编辑器的地形改走「原地写 + 笔划撤销」，左键让给笔刷

**背景**：E1 交付后编辑器只能看。用户要求继续做，第一个真正的编辑动作是刷地形。参考的
`Editor::Interface` 里左键直接进当前 instrument（`editor_interface.cpp` 的 `mouseCursorAreaClickLeft`），
右键拖地图；它没有撤销栈——改了就改了。我们要一个能回退的实现，而且不能把 `Mp2Map` 的 record 语义改烂。

**决策**：

1. **tile 原地写**：`Mp2Map` 加 `SetTileAt(index, tile)` / `SetTile(x, y, tile)`，写回 `Tiles` 背后的数组并
   返回旧值；`Tiles` 对外仍是只读视图。理由是量级：每刷一格就 `with` 复制整张地图在 108×108 上是
   233 KB，拖拽笔刷会把 GC 拖成卡顿。作为代价，两张共用同一数组的地图会互相看见改动——当前没有这种用法，
   记录在方法的 remarks 里。
2. **撤销的单位是笔划不是 tile**：一次按下到抬起是一个 `MapStroke`，同一格被来回刷只保留「首次 before /
   末次 after」。拖过二十格再 `Ctrl+Z` 应该一次退完，这是所有编辑器的常识；逐格撤销会让长拖拽无法收拾。
   上限 200 笔，超出丢最旧的。
3. **左键 = 刷，右键 = 拖**：E1 的左键拖动被让给笔刷。`MapScrollController` 增加 `SetPanButton`，与
   `SetLeftButton` 走同一个状态机，所以冒险地图那边一行没动。右键单击（没拖动）做吸管，取光标下的地面当笔刷。
4. **只换地形图，不做相邻过渡**：`MapGrounds.StartImageIndex` 给出每块地面的区间首图，刷完是硬边。参考会
   重算与四邻的接缝；在没做那个算法之前，先把「能改」做出来，并把硬边写进已知遗留，而不是假装有过渡。
5. **`--editor-paint` 是诊断开关**：截图驱动按不动鼠标。这个参数让一次矩形笔刷在打开编辑器时执行，于是
   `editor.png` 能同时守住「地形层被改写」和「对象层仍压在上面」两条渲染事实。

**后果**：

* 新增 `Game.Editor.MapEditSession`（笔刷、笔划、双栈撤销、吸管）与 `MapTileChange`/`MapStroke`；
  `EditorScene` 增加 `Paint/Undo/Redo/SetBrush/SetBrushByNumber/PaintArea`，工具条与状态栏跟着改写。
* `Mp2Map` 有了第一个写方法，但它只写 tile 数组，`Info`、`Castles`、`Addons` 都还是只读的。
* 编辑后的地图仍**不能保存**：`IsDirty` 只驱动状态栏的 `edited` 标记，E3 才谈 `saveMap` 与对拍。
* 六条像素基线全部重建：`editor.png`（含脚本笔刷）、`menu.png`（编辑器按钮）、`settings.png`、
  `scenario.png` 及两份 zh_CN。
* 已知缺口见 `PROGRESS.md` 的 E2 已知遗留：无地形过渡、笔刷只有 1×1、对象不能放不能删、撤销不落盘。

## ADR-042：场景每帧重画必须标脏，否则画面停在上一屏

**背景**：E2 完成后，从主菜单点 RIGHT 下角的编辑器招牌，日志明明打印了
`Main menu: '地图编辑器' opens the map editor.` 和 `Editor opened: 'Alteris' ...`，
`_scene` 也确实换成了 `EditorScene`（调试日志 `Drawing the 'EditorScene'.`），
但窗口里仍然是主菜单。用 `--scene editor` 启动却一切正常。

**根因**：`SdlPresenter.Upload` 只上传脏区：

```csharp
Rect dirty = frame.TakeDirtyRegion();
if (dirty.IsEmpty)
{
    return;
}
```

`MainMenuScene` 与 `DiagnosticsScene` 靠 `_frame.InvalidateAll()` 满足这个前提，
而 `EditorScene` 与 `AdventureMapScene` 把地形/图块直接写进 `ScreenBuffer.Pixels`
（`_editor.Map.Draw(_frame.Pixels)`、`ImageOps.FillRect`），从不标脏。像素是裸 span，
改它不会产生任何通知，`TakeDirtyRegion()` 于是永远为空、上传被跳过。
`--scene editor` 之所以看着正常，是因为 `ScreenBuffer` 初始脏区就是全屏，第一帧蒙对了一次；
之后画面即冻结——冒险地图滚动其实一直有同样的隐患，只是没人注意到。

**决策**：

1. 两个直接写像素的场景在 `DrawFrame` 第一行调用 `_frame.InvalidateAll()`：它们本来就是整屏重画，
   没有更细粒度可标。
2. `IScene.DrawFrame` 的文档注释把这条写成契约，避免下一个场景再踩。
3. 保留 `GameHost` 里 `Drawing the '<场景>'` 的一行日志：场景切换后到底是谁在画，这类问题
   一眼就能看明白，比再猜一轮渲染链路划算。

**后果**：点招牌能进编辑器，冒险地图滚动不再冻结。六条像素基线全部 `identical`
（修复只补上传，不改画面），构建 0 警告，测试 663 条全绿。

## ADR-043：保存 MP2 是「只改 tile 区」的原地补丁，不是全量序列化

**背景**：E3 要让编辑器把改动存回 `.mp2`。参考实现 1.1.18 帮不上忙——它的编辑器已经没有 MP2
写出，只写自己的压缩 `Map_Format`（`maps/map_format_info.cpp` 的 `saveToStream` 写的是带版本号的
新格式，不是原版结构），所以**没有可以逐字节对拍的原版产物**。

**备选**：

* **全量序列化**：照 `Mp2Map.Read` 逆着写一遍，header 428 字节 + tiles + add-on 表 + 城堡表 +
  可占领表 + 方尖碑数 + 信息块 + 对象编号。
* **原地补丁**：读取时拿到原始文件镜像，只把 tile 区重写，其余字节原样保留。

**决策**：原地补丁。

1. 全量写出的收益是零：这些段编辑器一个都不改，重写只是把「未映射/保留字段」逐个暴露成丢弃字段的
   风险。原版编辑器本身也是原地改这些结构再写回。
2. 补丁版有一个**可判定**的正确性标准，而全量版没有：打开→保存后与原文件**字节完全一致**；
   改一块地形后只有被刷 tile 的 20 字节窗变化。任何段被意外改动、丢字段、少写尾巴，都会立刻在
   字节差里暴露出来。
3. 失败不留半截地图：先写同目录临时文件，再 `File.Move(..., overwrite: true)`。

**后果**：

* 保存要求「有一份原始文件」。新建地图（合成地图、将来的 `New Map`）没有可修补的镜像，`Save` 会
  返回明确错误；全量写出随「新建地图」一起做。
* 另存为以当前文件为源（先把最新内容落到 `SavePath`，再以它为源写到新路径），所以另存为之后的
  保存目标是新文件。
* `IsDirty` 从「`_undo.Count > 0`」改成「与保存点的深度比较，且进行中的笔划也算脏」：否则按住左键
  拖完还没抬手时状态栏不显示 `edited`，拖到一半保存也会把地图当干净的。撤销越过保存点也算脏
  （那是撤掉了已存盘的内容）。
* 实测：`Alteris.mp2`（268562 字节）round-trip 0 差异；`--editor-paint 50,50,6,4,Snow` 后 30 字节变化
  全部落在 24 个 tile 的窗内，尾部 0 变化；保存后的文件再打开再保存仍 0 差异。

## ADR-044：新建地图与全量写出——header 以原始字节为基底，只覆盖建模过的字段

**背景**：ADR-043 把「没有源文件就不能保存」记成代价，并把全量写出留给「新建地图」。现在补上：
`--editor-new 36|72|108|144` 打开一张空白草地地图，`--editor-save` 给它一个文件，之后就是普通的
补丁保存。全量写出需要能构造 header 的 428 个字节，而 `Mp2FileInfo` 只建模了其中一部分。

**决策**：

1. `Mp2Map.CreateBlank(size, terrainImageIndex, name)` 造空白地图：全图同一张草地纯图块、没有对象、
   城堡与可占领表为空、`Addons` 只含 index 0 的占位项（格式里 0 是「没有 add-on」）、没有信息块、
   UID 从 0 起；默认全六色可玩、每色骑士、Normal 难度、无胜利条件。
2. `Mp2FileInfo.HeaderBytes` 在读取时保留原始 428 字节；`ToHeaderBytes()` 以它为基底（没有就全零），
   再把建模过的字段写上去。未建模的字节（AI 设置、条件周围的 unknown、保留位）因此原样留在文件里。
3. `Mp2MapWriter.WriteAll` / `Serialize` 按读取的逆序写出全部段：header、tiles、add-on 数量与数组、
   72 项城堡表、144 项可占领表、方尖碑数、信息块数量与载荷、对象 UID 计数器。目标文件与补丁路径
   共用「同目录临时文件 + `File.Move`」。
4. `Mp2AddonInfo.Write` 把 `ObjectNameN1` 除以 2 写回——读取时乘过 2，这个字段不是文件字节的原样镜像。

**「读回会被改写」的三处，都是拿真实地图实测出来的**（`Alteris.mp2` 写全量再读回逐字段比对）：

* **种族槽 255**：地图不用的玩家槽存 255，`MapRaces.FromIndex(255)` 是 `None`，若写 0 就变成骑士。
  改成「`ToIndex` 为 null 时保留原字节」——零基底的 header 仍然得到 0。
* **名字字段里塞了第二个串**：Alteris 的 16 字节名字字段是 `"Alteris\0" + "10\0"`，读取只到第一个 NUL，
  整字段重写会把 `"10"` 抹掉。改成「文本与读出来的相同就不重写该字段」。描述同理（200 字节）。
* **颜色字节不一定是 0/1**：部分地图存 2 之类的值，读取按「非零即开」理解。写回时只把 0 抬成 1，
  不把非零压成 1，否则「打开→保存」会改字节。

**信息块数量是版本阶梯**：Alteris 在该位置有 13 个非零对（`22,33,39,…,49`）再跟一个终止对，读取器
只认最后一个（`count = 49 - 1 = 48`）。全量写出只写一对 `count+1` 加终止：读回等价，文件更短。
原始阶梯只在补丁路径下才原样保留——那正是「打开的地图保存」所走的路。

**后果**：

* 空白地图可以直接存盘：36×36 = 27024 字节、144×144 = 415824 字节，与布局公式一致；读回再保存
  （此时走补丁）0 字节差异；在空白图上刷一块雪地再读回，画面正确。
* `MapEditSession.SaveAs` 自动选路径：有源文件就补丁，没有就全量。`Save`（无参、`Ctrl+S`）仍要求
  已有文件名，新建地图第一次存盘必须给路径。
* `Alteris.mp2` 全量写出后：428 字节 header 与原文逐字节相同，tiles / add-ons / 城堡 / 可占领 /
  信息块 / UID 全部与读入相等（这条用一次性探针验证，探针已删，因为它依赖真实数据目录）。
* 限制：空白地图只有草地，没有场景设置（难度、颜色、胜利条件、名字都还是固定默认）；城堡与可占领
  表在写出时是压紧的（空槽排在后面），只有「表里本来有空槽」的地图才看得出来，而那种地图走的是补丁。

---

## ADR-045：对象放置与删除——预设抄真实地图、add-on 只增不回收、保存切回整份

**背景**：E2 剩下的对象工具。MP2 里没有「对象表」：一个对象就是 tile 上的 `ObjectName1/2`（icn 类型）、
`BottomIcnImageIndex/TopIcnImageIndex`（帧号）、`Quantity1` 的低两位（地面层的 layer）、`ObjectType`
（游戏报的类型），多格对象再挂一条 add-on 链。编辑器要放一个「树」，就得自己知道这一串字节。

**决策**：

1. **对象目录 = 真实地图里的 tile**。`Mp2ObjectPresets` 的 9 个条目（树、丛林树、树顶、山、带山顶的山、
   墓地、宝箱、矿、资源）每一个都是从 `Alteris.mp2` 里取出来的真实 tile，icn 类型、帧号、layer 原样照抄。
   不凭 ICN 长度或常识猜帧号——猜错的帧号不报错，只会画出一个别的东西。
2. **预设是模板，不是 tile**。落点的 `TerrainImageIndex` 与 `TerrainFlags` 属于地形，保留；`NextAddonIndex`
   与两个 UID 由放置时重建（`TakeObjectUid` 推进文件尾部的计数器）。所以「在雪地上放树」得到的是一棵
   站在雪地上的树，不是一块被覆盖成草地的地。
3. **add-on 只增不回收**。格式没有空闲表，删除一条要重编号它后面所有链，而孤儿只占文件里 15 字节、不占
   帧。于是「替换对象」「撤销放置」都只是不再引用那条链，`Addons` 只会变长。`AppendAddon` 在表为空
   （文件里 add-on 数为 0）时先补 0 号占位，因为 0 号永远是「没有 add-on」。
4. **同格重复放置不算改动**。拖动会反复经过同一格，若每帧都取 UID 并追加 add-on，一次拖拽就能把文件撑大
   几十条。字段完全相同就直接返回 false。
5. **擦除写回真实空 tile 的字节**：`ObjectName1/2 = 0`、两处帧号 `255`、`Quantity1/2 = 0`、UID `0`、
   `NextAddonIndex = 0`。这是从 `Alteris.mp2` 的空 tile 统计出来的（只有 `TerrainFlags` 非零），不是
   `CreateBlank` 里那个 0。
6. **add-on 表变长后保存必须整份写**。tile 补丁（ADR-043）只覆盖 `w*h*20` 那一块，表达不了新增的 add-on 行。
   `MapEditSession` 记 `_addonsChanged`，为真时 `SaveAs` 走 `WriteAll`；真实地图实测 268538 → 268568 字节，
   正好 2×15，其余段与补丁同一份逻辑。
7. **对象名放状态栏**。工具栏左侧原本印 `brush <地面>`，加进 `object <名字>` 后与右侧提示在 640 宽下重叠
   （`Mountain (peak)` 正好越界）。工具栏只留 `MAP EDITOR`，工具与对象名进状态栏左侧，右侧补 `G grid` 的状态。

**后果**：

* 放下去的对象只是外观：矿不产资源、宝箱没守卫、城堡表与可占领表不跟着变。这些属于「对象属性」，见
  PROGRESS 的遗留。
* 多格建筑（城镇、锯木厂）没进目录：它们要按格拼装并处理 add-on 的相对偏移，等 `EditorPanel` 的对象按钮再一起做。
* 一次会话里反复替换同一格的多格对象会让 add-on 表增长；文件仍合法（孤儿合法存在），只是比必要的大。


---

## ADR-046：地形接缝——图号即地面、改一格要改九格、变体用坐标哈希

**背景**：E2 早期的地形笔刷只把 `TerrainImageIndex` 设成地面区间的首图，刷出来是硬边（ADR-041 把它写成
公开遗留）。参考的编辑器刷完会调 `updateTerrainTransitionOnTile`（`map_format_helper.cpp`，`setTerrainBoundaries`
是它的主体）重算与八邻的过渡。

**决策**：

1. **移植整套 `setTerrainBoundaries`**：缺一角的四种情形（`+12`，靠海滩位决定是否 `+16`）、开一边的四种
   （缺左/右走 `+8`、缺上/下走 `+0`，海滩在那一侧时走 `+8+16` 或 `+16`）、开四分之一的四种（`+4`，靠海滩
   角落选 `36/37/33/32`），以及"只剩斜角"回退到纯图。dirt 只与水、海滩接壤；beach 永不接缝；越界邻格钳制
   到地图边界，所以地图最外圈不算过渡。
2. **纯图不是区间首图**。地面区间的头几张是过渡图：草 30..67 过渡、68..75 纯图；水 0..15 过渡、16..23 纯图；
   海滩 415..431 全是纯图。笔刷写入的是"纯图 + 变体"（`MapGrounds.PlainImageOffset`），再由接缝覆盖。
   原来写区间首图，等于每次都画一张"与某地面接壤"的边。
3. **改一格要改九格**。邻居的图号各自描述"我与谁接壤"，所以刷完要把该格与八邻一起重算，否则新地面的一侧
   还是旧边缘。九格写进同一笔 `MapStroke`，`Ctrl+Z` 一次退干净。
4. **变体用坐标哈希**。参考用 `Rand::Get` 在三张或八张同组图里挑一张；编辑器必须可复现（同一笔出同样的字节，
   截图基线不能随机），所以 `MapGrounds.VariantOffset(x, y, count)`。单纯 `x*A ^ y*B` 在取模后有条纹，所以
   再乘一个 `x*y` 项并把高位右移下来。
5. **孤岛保留纯图，不学参考改地面**。参考找不到图时会把这块地面换回邻居的地面；编辑器不该悄悄改用户刚画的
   东西，所以 `TryGetBlend` 返回 false，调用方保持原样。
6. **新建地图也用纯图**。`Mp2Map.CreateBlank` 增加 `Func<int,int,ushort>` 重载，`EditorScene` 用它铺八张纯草
   变体——否则整张新图都是"过渡图"，刷同地面时反而处处有改动。

**后果**：

* 只重算被刷格的九宫格，地图其余部分字节不变（`diff_editor.png` 的差异全部落在雪块与小地图对应点）。
* "两个以上角落都要过渡"的组合没有对应图像，落到纯图；参考同样如此，画面上只是斜角差一点。
* 参考的水波重绘与道路重绘没有移植：MP2 没有独立道路层，水面波纹是动画帧，与地形图无关。
* 地形接缝是纯函数（地图 + 坐标 → 图号 + shape），可以脱离窗口测试，`TerrainTransitionsTests` 覆盖了
  孤岛、水岸两侧、海滩、边界、dirt 特例、变体稳定与"刷水会改编四邻"。


---

## ADR-047：编辑器的小地图导航——按下即居中、按住一直跟、点它不进撤销栈

**背景**：E2 完成时编辑器的小地图只是画出来的（`Minimap.Draw`），鼠标在上面点或拖动都没有反应，用户报告"小地图
点击和移动，大地图没有跟随"。

**决策**：

1. **照抄参考的分支**。`Interface::Radar::QueueEventProcessing`（`gui/interface_radar.cpp`）里没有"点击"与"拖动"
   之分：`le.MouseClickLeft( rect ) || le.isMouseLeftButtonPressedInArea( rect )` 一成立就
   `gamearea.SetCenter( ( pt - rect.pos ) * world.size / rect.size )`。所以点一下居中的那一格，按住不放一直跟随，
   松手才结束。`Minimap.TileAt` 的整数除法早就按它写好了，只是没人调用。
2. **雷达优先，且不开笔划**。参考靠鼠标捕获（`captureMouse()` + `_radar.isMouseCaptured()`）让编辑器在雷达上
   时跳过 game area。这里的对应物是 `_radarGrabbed`：左键在面板上按下时置位，不 `BeginStroke`、不 `Paint`，
   松手只清标志。这样点小地图既不会画到背后的格子，也不会往撤销栈塞一步空笔划。
3. **`Paint` 排除面板**。工具栏与状态栏早已排除；小地图同理——面板盖住地图，指针下的格子不是用户点的地方。
   一笔从地图上开始、拖过小地图时，`Paint` 返回 false，笔画在地图之外不会落笔。
4. **右键在小地图上什么都不做**。参考在那里弹 "World Map / A miniature view of the known world" 提示框；编辑器
   里这个提示是噪声，而且右键在编辑器里是拖动地图与吸管，弹框会打断拖拽。只吞掉一次落在地图外的吸管。
5. **`--editor-radar-click <x,y[,x2,y2]>`** 是它的脚本版：面板坐标而不是屏幕坐标，第二点是拖动终点。截图无法
   点面板，而"点一下视图就动"正是这条链路唯一的证据。

**后果**：

* 编辑器与冒险地图场景现在共用同一套雷达行为（`AdventureMapScene` 早就有同样的 `_radarGrabbed` 分支），
  两处的差别只剩编辑器多一个"不画笔刷"的检查。
* 视图移动不改编地图，所以小地图不需要重烤；居中的钳制由 `AdventureMapView.CenterOn` 负责，点面板边角也不会
  把地图拖出边界。
* 参考的雷达可以拖动面板本身（`BorderWindow`）与"世界地图视图"里的滚轮缩放，编辑器不需要这两样，没有移植。


---

## ADR-048：编辑器套上 `ADVBORD` 外壳——448×448 视口、整图贴框架、面板槽先写文字

**背景**：编辑器此前把地图铺满整个 640×480 逻辑帧，再用自绘的顶部工具条与底部状态栏压在上面。参考的
`Interface::EditorInterface::reset()` 不是这样：地图只占 16 px 边框与雷达列之间的 448×448，雷达在右上角
(480,16)，雷达下方是 `EditorPanel` 的 144 宽面板槽，外壳是 `ADVBORD.ICN` 的界面图。

**决策**：

1. **按参考的布局切视口**。`_viewport = (16,16, frame.Width − 144 − 3×16, frame.Height − 2×16)` = 448×448，
   雷达 `(frame.Width − 16 − 144, 16)` = (480,16)，面板 `(480, 160, 144, 304)`。与
   `reset()` 里 `_radar.SetPos( xOffset, borderWidthPx )`、`gameArea.SetAreaPosition( 16, 16, 448, 448 )` 一致。
2. **框架整张贴，不学参考的重复拼**。`ADVBORD.ICN` 只有一个 640×480 的 sprite（探针确认：1 张，640×480，
   transform 层），参考 `BorderWindow::_redraw` 从里面裁 16 px 花纹再 `repeatPattern`，是为了把界面拉到
   640×480 以上的分辨率。我们的逻辑帧就是 640×480，整贴一次即可，且边框、雷达槽、面板槽的坐标天然对齐。
3. **两级坐标**。地图视图（`AdventureMapView`）、`MapEditorView`、`MapScrollController` 只认视口像素；
   框架、雷达、面板认屏幕像素。换算只发生在 `EditorScene.ViewportCursor`：指针不在视口内就是 `null`。
   这一个 `null` 同时给出参考的三种行为——不更新高亮格、不落笔、不做边缘滚动。
4. **面板槽先写文字**。参考的面板是按钮与选择器阵列；在它们移植之前，槽里写工具、光标格/地面/对象、
   `clean`/`edited`/`saved`、`undo`、`scroll` 与快捷键。槽里有按钮凹槽的边框花纹，文字会像噪声，所以先把
   槽清成黑卡（保留 16 px 的装饰头与外围凹槽线）。
5. **缺资产时退化成纯色边框**，布局不变：`LoadFrameChrome` 拿不到 `ADVBORD.ICN` 就打一条警告，`DrawFrameChrome`
   填暗色块。这样 `--data` 指向缺文件的目录时，截图仍是同一套骨架。

**后果**：

* 边缘滚动、`Home`、`CenterOn` 的尺度都跟着视口变：`CenterOn` 让格子落正中，视口从 640×480 收窄到 448×448
  后，同一个 tile 的 scroll 反而变大（Alteris 上雷达点 (20,20) 从 (160;240) 变成 (256;256)）。
  `--editor-radar-click` 的期望值已同步。
* 视口变小，可视 tile 从 20×15 变成 14×14；`MapScrollController` 的 16 px 边缘带仍是 16 px，相对占比更大，
  但语义与参考一致（参考的 game area 也是 448×448）。
* 基线 `editor.png` 整体重建（108,862 → 116,108 字节）。面板文字是英文常量，和此前工具条/状态栏一样，
  中文化要等面板按钮一起做。
* 没移植：参考的面板按钮、雷达拖动面板本身、`BorderWindow` 的 9 段拼接（只在非 640×480 下需要）。

---

## ADR-049：`EditorPanel` 按钮阵列——照参考的格子坐标，对象选择器只能是名字表

**背景**：ADR-048 把面板槽留成一张写字的黑卡。参考的 `Editor::InterfacePanel` 是按钮阵列：
`EDITBTNS.ICN` 的九宫格工具按钮（地形 / 景观对象 / 细节 / 冒险对象 / 王国 / 怪物 / 溪流 / 道路 / 橡皮，
位置 `x = 48·col`、`y = 36·row`，帧号成对 released/pressed），`EDITPANL.ICN` 里裁出的九个 27×27 地面图
（`(30+29·col, 11+29·row)`）、一条名字条，以及选中格上压的那块 `TERRAINS.ICN` 第 9 帧黄框。

**决策**：

1. **只移植参考里现在能画的工具**：地形（帧 0/1，格 0,0）、对象（帧 2/3，格 1,0）、橡皮（帧 10/11，格 2,2）。
   其余六个 instrument 在本项目里没有对应实现（景观/细节/冒险对象/王国/怪物/溪流/道路），留空位而不画，
   免得点了一个什么都不做的按钮。
2. **格子坐标与步进照抄**：按钮 `48×36`、步进 `36×33`；地面格 `27×27`、步进 `36`，源坐标
   `(30+29·col, 11+29·row)`；黄框 30×30 画在格子 `(-2,-2)`。名字条裁自 `(7,104,130,14)`，放在
   面板 `y + 113`（参考 `drawInstrumentName` 的目标位置，比最后一行格子低 9 px），文字居中。
   这一行后来让给了笔刷尺寸按钮（ADR-050）——参考在 480 高时本来也不画名字。
3. **对象选择器是名字表**：`EDITBTNS` 只有 35 帧（参考要 45+），对象图标不在其中，`.mp2` 里也没有对象表。
   于是 9 个预设排成 `1 Tree` … `9 Resources`（行高 14，从选择器顶下 6 px 起），选中行用面板亮色描框；
   黄框是 30×30，套不进 136×14 的行。
4. **面板底纹用 `STONEBAK`**：参考 `makeInstrumentPanelBackground()` 用它调
   `StandardWindow::renderBackgroundImage()` 铺面板。这里按 `borderOffset = 0` 从面板左上整贴一次
   （144×320 一次就够，不需要参考的重复循环），面板因此一直画到显示底边、压住 `ADVBORD` 的底边框——
   参考在 640×480 也是这样，`bottomBorderOffset` 只在显示高于 480 时才加。缺 `STONEBAK` 时退回 ADR-048 的
   黑卡。
5. **缺少面板资产就退回文字版**：`EditorPanel.TryCreate` 任一资产缺失即返回 `null`，`EditorScene` 走
   `DrawPanelText`。这样 `--data` 指向不全的数据目录时截图仍是同一套骨架，只是面板退化。
6. **点击不进撤销栈**：面板按下直接 `ClickPanel` 并 `return false`，与雷达同理——换工具、换笔刷不是地图改动。

**后果**：

* 面板从「6 行状态」变成五行：光标格 + 地面、`undo n`/`clean`/`saved`、`1-9`/`G`、`O`/`X`、`^Z`/`^Y`/`Esc`。
  参考那一排是系统按钮，本项目还没有它们的功能，所以这里仍是文字。
* `editor.png` 重建（116,108 → 137,834 字节）：按钮、九张地形图与石头纹理都是新进画面的高频色，
  PNG 压不动。地图内容与雪块不变。
* `--editor-panel-click` 是面板的脚本入口，坐标是面板内的 0～143 × 0～319；日志报命中的类型与点完的状态。
* 对象选择器的行数是硬编码的 9（`Mp2ObjectPresets.All` 的长度），面板高度也只够 9 行 + 五行文字；
  预设表变长时要么分页要么缩行距。
* 没移植：参考的系统按钮行（undo / redo / 网格 / 视图 / 系统菜单）、八方向笔刷图标——
  `EDITBTNS` 的帧不够，且这些功能本身还没做。笔刷尺寸按钮后来补上了，见 ADR-050。

---

## ADR-050：笔刷尺寸——四个面积照抄 `getBrushArea()`，名字条让位给尺寸按钮

**背景**：ADR-049 把面板补成了按钮阵列，但笔刷仍然只有「一格一刷」。参考的 `EditorPanel` 有四个尺寸
（`BrushSize::SMALL` / `MEDIUM` / `LARGE` / `AREA`），配 `EDITBTNS` 第 24～31 帧的四个按钮（24×18，
`x = 14 + 30·i`、`y = 113`，released/pressed 成对），绘制在 ground、landscape objects、detail 与 eraser
这几个 instrument 上。尺寸本身不是画笔粗细，而是 `getBrushArea()` 返回的 tile 偏移矩形。

**决策**：

1. **面积照抄 `getBrushArea()`**：`Small = (0,0,1,1)`、`Medium = (0,0,2,2)`、`Large = (-1,-1,4,4)`、
   `Area` 返回空矩形。落笔时把偏移加到指针格上，再与地图边界求交，`Large` 贴着边缘也不会写出界。
2. **一次落笔只重算一次接缝**：写整个面积，然后对「面积外扩一圈」重算 `TerrainTransitions`，取代原来的
   「写一格 + 重算九格」逐格循环。对 1×1 结果完全相同（3×3 就是外扩一圈），对 16 格则从 144 次重算
   降到 36 次。
3. **`Area` 用笔划锚点画矩形**：按住时以笔划起手那格为锚，与当前格取包围盒，每移一格重刷一遍。参考是
   拖动时画橡皮框、松手才填；这里边拖边填，少一个框，语义相同。往回拖不会「退填」，已落下的格子留在
   地图上，回退手段仍是撤销。
4. **橡皮按面积清对象**：参考的 eraser instrument 也用 `getBrushArea()`，于是 `EraseObject` 变成
   「对面积内每一格调一次擦除」。`PlaceObject` 不受影响——对象的大小由它自己的美术决定，不由笔刷决定，
   所以对象工具不画尺寸按钮。
5. **名字条撤掉，那一行给尺寸按钮**：参考的 `drawInstrumentName` 只在面板高于 160 px 时才画，640×480 上
   那一行 `y = 113` 本来就是按钮的位置。ADR-049 画名字条是为了补信息，现在有了真正的按钮，地面名留在
   面板下方的状态文字里就够。
6. **默认 `Small`，不是参考的 `Medium`**：这个移植从「一格一刷」起步，键位 `1..9` 选的是地面而不是尺寸，
   改成 2×2 会让既有的笔划测试与直觉都变。尺寸只由面板按钮（以及 `MapEditSession.SetBrushSize`）改。
7. **越界枚举值一律拒绝**：`SetBrushSize` 只接受 `BrushSize` 的四个值，其余抛
   `ArgumentOutOfRangeException`；面板点击时下标来自固定表，不会造出非法值。

**后果**：

* `editor.png` 重建（137,834 → 138,002 字节）：只是面板那一行换了内容，地图与雪块一个像素没动。
* `EditorPanelHitKind` 多一个 `BrushSize`，面板点击日志多一项 `size`；`--editor-panel-click "56,230"`
  就是「选 2×2」。
* `MapEditSession` 多两个字段：`_brushSize` 与笔划锚点 `_strokeAnchor`（后者在 `BeginStroke` / `EndStroke`
  清空，只服务于 `Area`）。
* 尺寸不影响撤销粒度：一个面积、一个矩形都仍然是一笔。
* 没移植：八方向笔刷图标（`EDITBTNS` 里没有那组帧）、`Area` 的橡皮框预览、按数量而非面积定义的
  `Medium`/`Large` 变体。

---

## ADR-051：对象属性——先暴露 tile 里的数据字，语义等对象目录

**背景**：参考的对象属性有两个面：右键弹出的信息框（`Editor::showPopupWindow` →
`editor_object_popup_window.cpp`，正文来自 `Editor::getObjectInfoText`）和按对象类型分支的属性对话框
（城堡细节、事件、斯芬克斯……）。这两者都建在参考自己的**对象目录**上：`map_object_info.cpp` 把
`icn + 帧号 → 对象类型、名字、metadata 语义` 全表列好，`getObjectInfoText` 再按类型拼「500 黄金」
「日产 2 宝石」这样的句子。本移植只有 9 个预设、没有目录，也没有 `MP2::StringObject`（744 行名字表）。

同时 MP2 里对象属性只有一个落点：tile 的 `quantity1 / quantity2`，`metadata = (q2 << 8 + q1) >> 3`，
13 位。参考的 `Maps::Tile::Init(mp2)` 就是把这 13 位原样搬进 `_metadata[0]`，语义由对象类型在读取时解释。

**决策**：

1. **属性 = 数据字**：`MapEditSession.ObjectData(Point)` 读 `Metadata`，`SetObjectData(Point?, int)`
   写它，范围 0…`MaxObjectData`（0x1FFF），越界抛 `ArgumentOutOfRangeException`。
2. **只动属于字的位**：写入算 `quantity1 = (q1 & 0x07) | ((data & 0x1F) << 3)`、
   `quantity2 = data >> 5`。低三位在字外——其中两位是地面层位（`GroundLayer`）——必须原样保留，否则
   改一个数字会把精灵的叠放顺序改掉。
3. **空 tile 没有属性**：擦除写回的是 `Quantity1/Quantity2 = 0` 的空 tile，此时数据是空 tile 自己的
   字节而不是「某个对象的属性」，`SetObjectData` 返回 `false` 而不是先把 tile 变成"有对象"。
4. **信息框挂在 `I` 上，不挂右键**：参考用右键弹框，本移植的右键已经给了平移（按住）与吸管（单击）。
   框里四到五行：名字（预设名 / `Mp2ObjectType` 名 / 地面名）、`ground icn:帧 layer`、`top icn:帧`、
   `data N`、`[x; y]`——`[x; y]` 的写法照参考的 `getObjectInfoText` 收尾。
5. **框是模态的**：开框时任何点击或按键都先关框（`[` / `]` 除外，它们改数据），不会顺手在背后刷一格、
   撤销一步或换工具。参考的 `Dialog::Message` 同样挡住背后的编辑器。
6. **`[` / `]` 逐 1 步进**：没有文本框控件，也不发明「+100」这类语义化的步长；一次按键一次撤销，
   与其它编辑动作共用 `MapStroke`。
7. **名字来自预设匹配**：`Mp2ObjectPresets.Match` 按 `ObjectType + ObjectName1 + icn + 帧号` 认预设，
   **不比较数据字**——对象改名换不了身份，改数据也换不了名字。
8. **脚本入口 `--editor-info` / `--editor-data`**：截图不能按键，所以开框与写数据各有一条命令行；
   后者先写后开框，正好截出「框里显示刚写进去的数字」。

**后果**：

* `editor.png` 重建（138,002 → 137,995 字节）：面板第五行提示换成 `I info  ^Z/^Y Esc`，地图没动。
* `MapEditSession` 多两个公开成员与一个常量；`EditorScene` 多一层 `MessageBox` 缓存（按 tile + 数据字
  失效），`FHeroes2.App` 因此引用 `FHeroes2.UI` 的对话框（组合根本来就引）。
* 缺口写在 PROGRESS 的已知遗留里：**数据字有编辑入口、没有语义**。下一步是搬 `map_object_info.cpp`
  与 `MP2::StringObject`，把 `data N` 变成「500 黄金」「日产 2 宝石」，再据此做资源类型/数量的选择器、
  归属、事件与守卫。
* 没移植：`getObjectInfoText` 的按类型文本、属性对话框（城堡、事件、斯芬克斯、日常事件、传闻）、
  右键弹框的手势（我们的右键另有用途）。

## ADR-052：属性框说数据字**真正**代表的东西，而不是参考目录里的句子

**背景**：ADR-051 把 tile 的 13 位数据字暴露给编辑器，框里印 `data N`，并留下「语义等对象目录」的缺口。
下一步本应是搬 `map_object_info.cpp` + `MP2::StringObject`，把 `data N` 换成「500 黄金」「日产 2 宝石」。
动手前先把参考读地图的那段代码翻出来核对（`Maps::setInitialObjectInfo`，`maps/maps_tiles_helper.cpp`），
结论与 ADR-051 的假设不一样：

* **只有怪物保留这个字**：`OBJ_MONSTER` 走 `setMonsterOnTile(tile, mons, tile.metadata()[0])`，字就是
  兵堆数量。`OBJ_ARTIFACT` 只在卷轴形态下用字表示法术 id，随后又被神器 id 覆盖。
* **资源堆与矿都不看字**：资源类型由精灵决定（`Resource::FromIndexSprite`：`OBJNRSRC` 帧 1/3/5/7/9/11/13
  = 木/水银/矿/硫/水晶/宝石/金），矿的类型由 `EXTRAOVR` 覆盖层帧号决定（0 矿 / 1 硫 / 2 水晶 / 3 宝石 / 4 金）。
* **数量与内容是开局掷出来的**：资源堆 `Rand::Get`（金 500～1000、木/矿 5～10、其它 3～6），宝箱
  1000/1500/2000 金币或经验，墓地给随机神器，且都写进 **`metadata[1]`**。
* **其余一律清零**：`setInitialObjectInfo` 的默认分支 `resetMetadata()`，注释写着 “All other objects
  shall not have metadata”。

也就是说「500 黄金」在参考里存在，但它活在参考**自己的地图格式**的扩展元数据块里（tile 的 13 位只是那个
块的 UID），MP2 文件没有这一层。Alteris 的实测统计印证这一点：整张图里只有 `OBJ_MONSTER` 的格子上有
非零字（250、30、40、4000…），资源堆、矿、宝箱的格子全是 0。

**决策**：

1. **说真话，不编数字**：新增 `FHeroes2.Assets.Maps.Mp2ObjectData`，把字翻成一句能站得住的话——怪物是
   `the word is the troop count: 250 monsters`；资源堆按精灵报种类并说明「数量开局才定」；矿按覆盖层报
   种类；宝箱与墓地说明内容是开局掷的；本类型不认字时直说「游戏会把它清掉」；`ContainsMetadata` 认得的
   类型（神器、关卡、帐篷、扩张、终极神器的半径）报「这个版本还没读」。
2. **资源名与掩码照参考**：`Mp2ResourceType` 用 `Resource::WOOD`=1 … `Resource::GOLD`=64、`All`=0x7F，
   方便以后直接用参考里的掩码判断。
3. **一句话，不是新控件**：只在 `data N` 下加一行，`[` / `]` 照旧逐 1 步进；怪物改完字数就跟着变，因为
   两边读的是同一个字。
4. **不搬对象目录**：`map_object_info.cpp`（4950 行）与 `MP2::StringObject`（744 行）的任务是「icn + 帧号
   → 对象身份」，那是面板图标菜单、属性对话框与「500 黄金」句子的共同前提，但和 MP2 的数据字无关；
   先做它会把这一轮变成一次没有终点的大搬用。等有对象身份以后，语义表可以直接扩进 `Mp2ObjectData`。
5. **顺手改掉 `Mp2TileInfo.Metadata` 的错误注释**：原文写「资源堆的数量、宝箱的内容、矿的种类」，
   那是参考自己格式的语义，不是 MP2 的。

**后果**：

* 属性框在原有的 `ground` / `top` / `data` / `[x; y]` 之间多一行说明；`editor.png` 不变（框不在基线里）。
* `Mp2ObjectDataTests` 11 条新测试在 Assets 工程（纯解码，不需要 App）：`OBJNRSRC` 全帧表、矿覆盖层
  0～4 与越界、主精灵优先、只有怪物保留字、各类句子、`All` 掩码。
* 遗留改成「属性框写不出『500 黄金』」：要么改用参考的存档格式（E4）才有扩展元数据，要么在 MP2 之外
  另存旁路数据（与别的编辑器不兼容）——这两条都是新决策，不在这里顺手定。
* 参考编辑器本身也不给资源堆写数量到 tile：点资源对象弹 `Dialog::SelectCount`，写的是它自己格式的
  `resourcesMetadata`，与 MP2 无关。

## ADR-053：属性框用参考的对象名与怪物名，不印枚举名

**背景**：ADR-051 的属性框拿 `Mp2ObjectType.ToString()` 当标题，于是框里出现 `ActionMonster`、
`ActionRandomResource` 这种移植内部的枚举名；ADR-052 的怪物句子也只能写 `250 monsters`，因为
`Media::` 里没有一张「怪物 id → 名字」的表。参考这两处都有现成的表：

* `MP2::StringObject`（`maps/mp2.cpp`）——一个按 `MP2::MapObjectType` 展开的大 `switch`，进来先把
  `OBJ_ACTION_OBJECT_TYPE` 位掩掉，所以 `ActionArtifact` 与 `Artifact` 共用一条分支，名字是
  `Artifact`、`Archer's House`、`Random Monster - very strong` 这样的游戏原文。
* `Monster::monster_t` 的名字（`monster_info.cpp` 的 `untranslatedName` / `untranslatedPluralName`，
  经 `Monster::GetName` / `GetPluralName` 取用）——72 条，`UNKNOWN` 在前、66 种兵在后、4 个随机怪物收尾。

**决策**：

1. **照抄两张表，不做本地化**：新增 `FHeroes2.Assets.Maps.Mp2ObjectName` 与 `Mp2MonsterName`，字符串与
   参考一字不差。界面语言切换管的是 `.mo` 里的界面词条，对象名与怪物名在参考里也是原文常量，不属于
   `GameText`；等界面接 CJK 译文时再统一给这两张表接 `.mo`（届时 key 就是这些英文原文）。
2. **动作位只掩那一位**：`(type & ~0x80)`，不是 `type & 0x7F`——`OBJ_RESURRECTION_OBJECT_TYPE` 之上
   的宝物（`Swampy Lake`、`Barrel`…）用第 8 位，掩成 `& 0x7F` 会把它们名字吃掉。
3. **未知字节退回枚举名**：参考对没见过的类型直接 `assert`，地图允许有错，编辑器不该因此崩。
4. **怪物 id = `MONS32` 帧 + 1**：与 `Maps::setInitialObjectInfo` 的 `OBJ_MONSTER` 分支一致
   （`Monster( icnIndex + 1 ) // ICN::MONS32 start from PEASANT`）；主精灵优先，其次地面精灵。
5. **复数只按 1 变**：`Monster::GetPluralName(count)` 的规则就是 `count == 1 ? name : pluralName`，
   所以 `1 Veteran Pikeman` / `25 Veteran Pikemen`。参考对灯塔、风车、魔法花园用 `_n` 数个数，这里
   一个对象取单数。

**后果**：

* 属性框标题与面板下的悬停提示都换成参考名（`Monster`、`Trees`），`data` 那句里的怪物也有种类
  （`the word is the troop count: 250 Medusas`）。
* `Mp2ObjectNameTests` 10 条 + `Mp2ObjectDataTests` 4 条（Assets 254 → 264）：枚举成员全部有名字、
  动作位同名、第 8 位不被吃掉、未知类型留数字、怪物名单复数与越界。
* 两张表都只解码、不碰界面状态，`editor.png` 与其它四条基线逐像素不变（框与悬停提示都不在基线里）。
* 仍未做：参考按类型拼出来的整段正文（`getObjectInfoText`、`map_object_info.cpp`）与属性对话框；
  那些要有「icn + 帧号 → 对象身份」的目录才写得出。

## ADR-054：冒险地图套上 `ADVBORD` 外壳——`MapFrame` 一处几何、视口独立缓冲、面板砖纹双起点

**背景**：M4 第一版把地图直接画满 640×480，边框、雷达槽与右侧面板列都是空的。参考的
`ADVBORD` 图像（`ICN::ADVBORD` 第 0 帧）本身就是一张 640×480 的完整外壳，它的布局散在三处：

* `fheroes2::borderWidthPx = 16`——地图与窗口边缘之间的边宽，滚动判定与 `reset()` 都用它。
* `Interface::EditorInterface::reset`（`editor/editor_interface.cpp`）——编辑器把地图放在
  `16,16 448×448`，雷达在右上 `480,16 144×144`，面板从雷达底填到窗口底边。
* `Interface::IconsBar::redrawBackground`（`gui/interface_icons.cpp`）——面板列不是「把雷达下方的
  图块切下来贴」，而是取 `(icnAdv.width - radarWidthPx - borderWidthPx, radarWidthPx + 2*borderWidthPx,
  radarWidthPx/2, 32)` = `(480,176,72,32)` 这一块半宽砖，向下、向右重复平铺。

编辑器那边 `EditorScene` 已经用 `RadarBay` / `PanelBay` 两个私有方法算过同样的矩形，冒险地图若再抄
一份，两边就会各改各的。

**决策**：

1. **抽 `Game.AdventureMap.MapFrame`**：一个类同时持有美术（`ADVBORD.ICN` 第 0 帧）与四个矩形
   （`Viewport` / `Radar` / `Panel` / `BottomBorder`），`Load(AssetBundle?)` 读帧、`Draw(frame, bg)`
   贴外壳（缺图时铺平色，布局不随数据在不在而变），`DrawPanel(frame)` 铺砖纹。编辑器与冒险地图
   共用同一个实例类型，`EditorScene` 删掉 `LoadFrameChrome` / `RadarBay` / `PanelBay`。
2. **整图一次 blit，不切边框**：参考的 `Interface::BorderWindow::Redraw` 把图切成上/下/左/右四条，
   因为它的显示分辨率可以大于一块美术；这里的逻辑帧恰好等于图像尺寸（640×480），一次
   `BlitSprite` 就够。将来窗口放大时再补拉伸，不提前搬切图逻辑。
3. **地图画进 448×448 的 `PixelBuffer` 再贴到 `(16,16)`**：参考先画满屏再盖边框，两者最终像素
   相同；独立缓冲让滚动换算继续用「地图像素」，也让视口裁剪由缓冲区尺寸天然保证，不必给
   `AdventureMapView` 传偏移。
4. **面板矩形与砖纹起点是两个数**：`Panel` 从雷达底 `160` 起（编辑器面板要占满这一列），砖纹却从
   `176`（`radarWidthPx + 2*borderWidthPx`）起——参考的图标条本身就在第一条边框下面缩进一格。
   两个常量各自带注释，免得下一个人把 160 当成平铺起点。
5. **状态栏搬到底边框带**：原来压在窗口最底 `0,469,640,11`，那是地图/菜单的症状；有了外壳以后
   中间那块 `16,464,608,16` 才是空出来的地方，状态栏改画在 `16,466,608,11`（深色条 + `SMALFONT`），
   基线忽略区跟着改成这块矩形。菜单与设置/场景的忽略区不动。
6. **砖纹用 `BlitSprite` 的子矩形，目标偏移要减去砖块原点**：`ImageOps.BlitSprite(dst, offset, src,
   rect)` 的 `offset` 是相对于 `rect` 左上角的，直接传 `(x, y)` 会把整块砖推到面板外；传
   `(x - brick.X, y - brick.Y)` 才是「把这一块放到 (x,y)」。

**后果**：

* 冒险地图与编辑器共用一套几何：`editor.png` 在抽取后逐像素不变（证明 `RadarBay` / `PanelBay` 与
  `MapFrame` 等价），`adventure-map.png` 重建（`168,285` → `160,793` 字节，视口里同一批地形像素
  换成 16px 边框与砖纹面板，忽略区从 `7040` 变 `6688` 像素）。
* `MapFrameTests` 7 条钉住矩形（`(16,16,448,448)`、`(480,16,144,144)`、`(480,160,144,320)`、
  `(16,464,608,16)`）、小画框的收缩、缺图时 `Draw` 铺满而 `DrawPanel` 不动像素。
* 面板里的**内容**（城堡/英雄图标条、按钮面板、日期与资源栏）仍是空的：`Interface::IconsBar` 的
  图标与交互属于后面那一轮，砖纹只是让它不像个洞。
* 底边框带被状态栏占去 11 px，参考在那里放的是系统按钮；真按钮落地时状态栏要另找位置（或干脆
  只在调试模式下显示）。

