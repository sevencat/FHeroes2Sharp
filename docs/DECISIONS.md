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
   运行时放大而不是独立资源。**后续**：`BitmapFont` 仍然拒绝 `Large`，但大号与黄/灰/白/渐变这几种面
   已改由 `Game.Gui.FontGlyphTable` 从 `FONT.ICN` 派生（放大两倍 + `PAL::GetPalette` 重着色），
   见 ADR-020 与 `REFERENCE.md` 的 `ui_text` 行。
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
  它们只影响字形外观，不影响布局；等主菜单拼好后按需补齐并用截图对拍。**后续**：字形修补已由
  `Game.Gui.FontRepairs` 补齐，译版资材的插帧/对调/截断由 `FontAssetFixes` 与 `FontFrenchRepairs`
  接手（顺序：修补 → 资材修正 → 派生）；仍缺的是各代码页的按钮字形补充（`generateCP1250GoodButtonFont`
  这一批），见 `REFERENCE.md` 的「计划中」。
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
   **后续**：已移植，落在 `Game.Gui.FontGlyphTable` 的渐变面（每帧放大 6 px 留描边位、刷纵向渐变后依次叠
   内描边/黑边/外描边）；参考只生成两套渐变面，端口同样只做这两套。

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
   `DrawBorder()`/`ADVBORD` 属于另一支（更大的窗口），继续留在「未移植」。**后续**：ADVBORD 的九宫格
   已在 ADR-048（编辑器）与 ADR-054（冒险地图 `MapFrame`）落地，但那两支都是「地图外壳」；
   对话框侧的 `DrawBorder()` 仍未移植。
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
  （`SURDRBKG` 拉伸 + 抖动过渡 + 背景压暗），那是下一块工作。**后续**：`ButtonGroup` 与
  `StandardWindow` 都已落地（见 `REFERENCE.md` 的 `ui_button` / `ui_window` 行），设置对话框（ADR-027）
  与语言选择窗口已经用上；分辨率窗口与其余设置页仍未做。
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
却不起作用的控件。**后续**：音频后端已由 ADR-030 接上（`App.Audio.AudioPlayer`，SDL3_mixer，
音乐与音效各一条音量），所以「音量」这一组现在有实际落点；分辨率/全屏仍缺运行时重建路径。

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
   主对象类型推断（`Maps::getMainObjectType`）、英雄/怪物/船的专属渲染器。**后续**：英雄/怪物/船的
   专属渲染器已由 ADR-037 / ADR-059 / ADR-060 落地；主对象类型推断仍未做，现在是拾取与信息框的
   唯一卡点（PLAN「还差的主干」第 1 条）。

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
   **后续**：外壳已由 ADR-054 落地（`MapFrame`），雷达现在取 `MapFrame.Radar`，落点与参考的 (480,16)
   一致，`Minimap` 确实一行没改；面板舱也已由 ADR-055（图标条）与 ADR-058（按钮面板）填上。

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

## ADR-055：面板图标条只画 `LOCATORS` 的帧——英雄列先空着，等英雄身份

**背景**：`ADVBORD` 外壳落地后，右侧面板只有砖纹（ADR-054）。参考在这一列的上半部分放
`Interface::IconsPanel`：左英雄、右城堡，各四格（640×480 下），点一格就跳到那座城 / 那位英雄。
它的渲染散在 `gui/interface_icons.cpp`：

* 几何：`iconsCursorWidth = 56`、`iconsCursorHeight = 32`，`setPos( rect.x + 72, rect.y )`（城堡列在
  英雄列右边 72 px），`redrawItem( ox + 5, oy + 5 )`——图标贴在格子内缩 5 px 的地方。
* 空槽：`IconsBar::redrawBackground` 在 `validItemCount` 之后按 `1 + i % 8` 取 `LOCATORS.ICN` 的帧，
  每格 46×22，一个玩家色一帧。
* 城堡：`fheroes2::drawCastleIcon` → `getCastleIcnIndex( race, isCastle )`：`9…14` 是六族的要塞城堡，
  `15…20` 是六族的城镇，随机种族返回 `25 / 26`。
* 英雄：`Heroes::PortraitRedraw( PORT_SMALL )`——`MINIPORT.ICN` 按**英雄身份**取帧，再叠 `PORTXTRA`
  的背景与旗子。

**决策**：

1. **`Game.AdventureMap.IconBar` 只做布局与落位**：`Heroes` = `(480,176,56,128)`、`Castles` =
   `(552,176,56,128)`，`SlotAt` 给出每格 56×32，`CastleFrame` / `EmptySlotFrame` 是两张帧表，
   `Draw(frame, sprites, castles)` 把图标贴到 `slot + (5,5)`。列表、滚动、悬停高亮（`_sfMarker`）
   都不做：四格放得下，参考里滚动条只在图标超过四格时出现。
2. **起点取 `Panel.Y + BorderWidth`（176），与砖纹一致**：面板 bay 从雷达底 `160` 起，但图标条和
   `Interface::IconsBar::redrawBackground` 一样从 `176` 起，中间那 16 px 留给砖纹。
3. **帧表照参考，但随机种族返回 `null`**：`LOCATORS.ICN` 只有 25 帧（0…24），`25 / 26` 是编辑器
   「随机城堡」的选项图，而地图文件里存的是已经落定的种族（`MapRace.Multiple` 也一样没有图）。
   返回 `null` 时该格退回空槽，不画错帧。
4. **英雄列暂时全是空槽**：MP2 的 hero tile 只存 `MINIHERO` 的帧号（32×50 的地图精灵，编辑器用的），
   没有「这是哪位英雄」；身份在还没解析的 `MP2::HeroInfo` 信息块里。用 `MINIHERO` 硬塞进 46×22 的
   格子会把它压扁，所以宁可空着——`IconBar.Draw` 里有一行注释指向这件事。
5. **不画 `CSLMARKER` 的建设标记**：它回答「今天能否建造」，需要王国与它的建筑列表。

**后果**：

* `adventure-map.png` 重建（`160,793` → `168,802` 字节）：右列是 `Alteris` 城堡表的前四个城堡
  （已要塞的种族图标），左列四个空槽帧 1…4。`editor.png` 不受影响——编辑器不用 `IconBar`。
* `IconBarTests` 9 条：两列矩形、格距 32、六族 × 城堡/城镇的 12 个帧号、随机种族无图、空槽
  1…8 循环、图标落在 `+5,+5`（格子角落仍是砖纹）、源为空或没有帧时面板不动。
* 图标条的英雄列是这块面板里唯一「有位置没内容」的地方；等 `MP2::HeroInfo` 解析出来（或者开局
  分配英雄），把 `IconBar.Draw` 的左列换成 `MINIPORT` + `PORTXTRA` 即可，几何不用动。
* 面板剩下的按钮区与日期/资源栏仍空着，砖纹之外就是黑的。

## ADR-056：编辑器面板按参考重画——45 帧按钮集、由舱位高度推出的拾取区

**背景**：ADR-049 只画了九个工具位里能做的三个（地形 / 对象 / 橡皮），其余留空，底部还是五行文字。
参考的 `Editor::EditorPanel` 是三行九宫格按钮（九个全有美术）、一片拾取区、一行笔刷尺寸、两行系统按钮；
对着截图一看就是两副样子（用户 2026-11-14 指出）。

**决策**：

1. **`EditorButtonSet` 生成 45 帧**：`EDITBTNS.ICN` 的 35 帧照原样过账（测试逐字节比对），后 10 帧照
   `game_assets.cpp` 的 `case ICN::EDITBTNS` 在运行时拼：冒险对象 35/36（`X_LOC1` 第 0 帧）、王国 37/38
   （`OBJNARTI` 第 13 帧）、怪物 39/40（`MONS32` 第 11 帧）、撤销 41/42、重做 43/44（撤销图左右镜像）。
   干净石面把 detail 按钮的帧 4/5 克隆后填 `(16,6,18,24)` / `(16,7,17,23)` 的 41 / 46 号色；图标按
   参考的 `drawImageOnButton` 贴到 `(2,2)`、上限 39×29，四层 `updateShadow`（`(1,-1)/2`、`(2,-2)/5`、
   `(-1,1)/6`、`(-2,2)/9`），按下那层把 6 号换成 10 号，最后照参考补六个像素的抗锯齿阴影。
   撤销 / 重做箭头是运行时画的 19×13 图（参考 `createUndoButtonImage`），不是归档美术。
2. **拾取区高度由舱位推出来**：`PickerHeight = 舱高 − 3·36 − 2·36`，480 高下是 320 − 108 − 72 = **140**，
   正是参考的 `instrumentPanelHeight = 480 − 160 − 5·36`。ADR-049 里写死的 124 少了 16 px，底部会漏出
   一条砖纹、系统按钮也压不住底边框；现在两行系统按钮正好落到底边 `444…480`。
   笔刷行 `min(PickerHeight − 27, 135)`（= 113）、`x = 14 + 30·i`；只有地形与橡皮画它，
   480 高下参考本来也不画工具名条（`instrumentPanelHeight > 160` 才有）。
3. **拾取区是 `STONEBAK` 加六层内凹斜面**：照 `applyRectTransform` 的顺序在 0、1、2、2、3、4 号偏移上刷
   2/4/5/9/8/9 号 palette transform——参考的 `EditorPanel::setPos` 就是这么给背景压出凹槽的。
4. **九个工具按钮全画**：`EditorPanel.ToolAt` 对没实现的六个返回 `null`，`ClickPanel` 遇到 `null` 不换工具，
   所以按下去没有副作用。系统按钮里撤销 / 重做接到 `MapEditSession`，放大镜、`SPECS`、存盘、系统菜单
   先占位（`EditorSystemButton` 已有枚举，接功能时只改 `ClickSystemButton`）。
5. **面板不再写字**：ADR-049 那五行状态（光标格、undo 计数、快捷键）随 `DrawPanelNotes` 一起删掉，
   这些事实在状态栏与日志里；只有资产缺失（`EditorPanel.TryCreate` 返回 `null`）时才退回 `DrawPanelText`。

**后果**：

* `editor.png` 重建（`137,995` → `132,506` 字节）：按钮少了整片文字，但多了三行石面与斜面，PNG 反而更小。
  地图内容、雪块、雷达都没变。
* 新增 `EditorButtonSetTests` 6 条：帧表、每帧尺寸（48×36 / 笔刷 24×18 / 三个 16 px 备用帧）、归档帧逐字节
  未改、五个拼装按钮互不相同且按下帧不同色、撤销与重做箭头落在镜像的 `(23,16)` / `(27,16)`。
  `EditorPanel` 本身在 `FHeroes2.App` 里，没有测试工程引用它，几何由基线截图钉住。
* 拼装需要的 `X_LOC1` 在资料片归档里；缺它就只留石面，`Missing` 记下缺的条目，不影响其余按钮。
* 参考面板上的对象/怪物图标是美术占位：本项目的对象拾取器仍是名字表（ADR-049 第 3 条），
  怪物工具还没实现，按钮先画出来。
* 九宫格按钮的悬停与按下反馈只有按下态（`_pressed` 用 `BrushSize` 与工具表判断），鼠标悬停没有做。

## ADR-057：移植粒度定为函数级 1:1，全局状态用静态门面承接

**背景**：ADR-001 定的是「资源解析逐字节对齐、工程结构重写」，实际执行时每个界面都要重新推断
参考的布局意图（面板那次就是 `instrumentPanelHeight` 被写成 124 而不是 140），逐块对齐的成本高到
「感觉不是办法」。于是量了一下两边：参考 478 个文件 / 179,135 行，本项目 222 个文件 / 34,395 行
（生产 23,600 + 测试 10,778）；参考里 `goto` 0 处、`#define` 81 处、`template<` 64 处、类型转换 1542 处、
`SDL_` 符号 234 个 / 406 处调用。语言与 SDL 的差异不是主要障碍，**全局状态才是**：3 个全局变量、
10 个单例类型、832 处 `::Get()`，其中约 800 处在业务层。

**决策**：

1. **粒度定在函数级**：新代码与参考同名函数、同顺序、同常量（`updateTerrainTransitionOnTile`、
   `makeInstrumentPanelBackground` 这类名字直接沿用），允许结构差异（分层、不可变类型、没有指针）。
   不追求行级 1:1：行级翻译会继承宏、全局单例与 `SDL_Surface` 的原地改写语义，产物更难维护，
   而且对拍需求一点没减。
2. **全局状态第一版照抄，用静态门面**：`static X Current { get; set; }`，参考的
   `Settings::Get().foo()` 写成 `Settings.Current.Foo`。第二版「改成 DI」的落点因此只剩门面的赋值处，
   832 个调用点不动。
3. **禁止可变 `static` 字段**（除门面的 `Current`）：状态挂在实例上，静态成员只能是常量表或纯函数。
4. **只给参考里是单例的东西建门面**。已经是实例 + 构造函数传参的（`AssetBundle`、`AudioPlayer`、
   `Pcg32`、`AdventureMapScene`、`EditorScene`）不再套门面——没有 832 处调用点要照顾，多一层只多一个全局。
5. **不引入 DI 容器**：`Current` 由 `GameHost` 在组合根赋值，换实现就是换指针对；注入框架留在将来真有
   需要时再说（游戏里每帧 new 的对象不适合容器解析）。

**后果**：

* 清单与四条规矩进 `ARCHITECTURE.md` 的「全局状态与门面」一节；冲突判定顺序与判例后来单独立成
  同文件的「移植粒度与冲突处理」一节。数据类（`World`、`EditorInterface`、
  `BattlePlanner`）不造 `IWorldProvider` 之类接口，`Current` 指向有 owner 的实例。
* 粒度本身是**总纲级**约束，`PLAN.md` 的「总体原则」同步加了第 3 条（起初漏了，只在 ADR 与
  `ARCHITECTURE.md` / `PROGRESS.md` / `REFERENCE.md` 里落过）。今后改移植粒度时，`PLAN.md` 与
  `ARCHITECTURE.md` 必须一起改，ADR 的「后果」里要显式列出这两份文档。
* 静态门面是进程级共享状态：动它的测试要挂进同一 collection，场景切换要显式重置 `Current`。
* `GameLog.MinimumLevel` 是现存唯一可变静态，收进门面之前不再新增。
* 备份点 `backup/pre-facade-2026-11-14`，改造在新分支 `refactor/global-facade` 上进行，`main` 保持可运行。
* 全量行级 1:1 明确否决：参考 54% 的行数是业务逻辑（`battle`/`game`/`heroes`/`dialog`/`ai`），
  逐行搬过来只会得到 9 万行无法测试的静态类。

## ADR-058：按钮面板照 `interface_buttons.cpp` 摆八格——禁用就套 `DARKENING`，缺帧就留空

**背景**：ADR-055 之后右侧面板只有图标条，按钮区（参考 `Interface::ButtonsPanel`）还是砖纹。
参考里这块是八个 36×36 的 `Button`，两行四列，`ADVBTNS.ICN` 的 `2i / 2i+1` 帧对，外加一个只做
拖动把手的 `BorderWindow`：

* 构造：`setICNInfo( icnbtn, 0..14, 1..15 )`，`icnbtn` 是 `ADVBTNS` 或 `ADVEBTNS`（邪恶界面）。
* 布局：`x = GetArea().x`、`y = GetArea().y`，第一行四个按钮按 `area().width` 链着排，第二行
  `y += area().height` 再排四个；`BorderWindow` 的面积就是 `{0,0,144,72}`。
* 位置：`game_interface.cpp` 里 `_buttonsPanel.SetPos( px, _iconsPanel.GetArea().y + _iconsPanel.GetArea().height + borderWidthPx )`。
* 状态：`_setButtonStatus()` 每次重画都算一遍——没有聚焦英雄时，移动 / 施法 / 下一个英雄三个按钮
  `disable()`，其余五个从不禁用。移动按钮还会在 `2/3`（走路径）、`16/17`（访问脚下对象）、
  `18/19`（路径空闲）三对帧之间切换。
* 禁用外观：`ButtonBase::_getDisabled()` = 复制 released 帧 + `ApplyPalette( DARKENING )`。

**决策**：

1. **`Game.AdventureMap.ButtonsPanel` 只做布局与落位**：`Area` = `(480,320,144,72)`，`AreaOf( button )`
   给出每格 36×36；`BorderWindow` 的拖动把手、悬停、点击都留给以后的鼠标层——标准界面下参考也
   不画边框，按钮直接落在砖纹上。
2. **起点取图标列底 + 一条边框（320）**：图标列是 `176…304`，参考是
   `iconsArea.y + iconsArea.height + borderWidthPx`，`ADVBORD` 的这条边框是 16 px，所以 304 + 16。
   按钮区底 392，下面是空的砖纹，与参考一致（参考 `321…393`，bay 底 472）。
3. **状态是数据**：`AdventureButtonStatus` 只记移动 / 施法 / 下一个英雄的启用与移动按钮的三个含义，
   五个从不禁用的按钮不占标志位；`WithoutHero` 就是 `currentHero == nullptr` 时所有分支 fallthrough
   的结果。参考在 `_redraw` 里现算，这里由调用方给——本场景还没有王国。
4. **缺帧就留空，不退回 2/3**：`Assets::getImage` 越界返回空的 `errorImage`，参考于是画不出东西；
   本项目 `IcnSpriteCache` 返回 `null`，`ButtonsPanel.Draw` 直接跳过。本资材的 `ADVBTNS.ICN` 只有
   16 帧，所以没有英雄时移动按钮那一格是空的——这是「照着参考跑同一份资材」的真实结果，不是漏画。
5. **禁用帧有缓存**：`ButtonSpriteSet` 按帧号存一份 `DARKENING` 副本，released 帧本身不动
   （参考也是复制后再套调色板）。

**后果**：

* `adventure-map.png` 重建（`168,802` → `170,810` 字节，sha256 `AD9B2E39…`）：面板在图标条下多了
  两行按钮，第一行第二个是空格，第三格城堡、第四格施法，第二行沙漏 / 帐篷 / 磁盘 / 电脑。
* `ButtonsPanelTests` 11 条：面板与每格矩形、八个帧对、移动按钮三帧、`WithoutHero` 的五个 true /
  三个 false、启用与禁用的落位颜色、按 `HeroMovementIcon` 取帧、缺帧留空、精灵源为空不画；
  另 2 条真资材：36×36 与 16 帧（含 `18` 号帧确实没有）、`DARKENING` 只改副本。
* 面板剩下的日期/资源状态栏（`Interface::StatusPanel`）仍空着，它要王国与 `world` 的日期。

## ADR-059：怪物图层整帧 blit——锚点 `(16,30)`、帧号 `(id-1)*9`，影子跟着身体走

**背景**：ADR-054 之后冒险地图的地形、对象层、英雄层与右侧面板都齐了，缺的是地图上那些游荡的怪物。
参考里它们不属于对象层，`maps_tiles_render.cpp` 的 `getMonsterSpritesPerTile` 单独处理：

* 瓦片性质是 `MP2::OBJ_MONSTER`，生物 id 来自 `bottomIcnImageIndex + 1`（`objectName1 >> 2 == Mons32`）。
* 帧号是 `Monster::GetSpriteIndex() * 9`，即 `( monsterId - 1 ) * 9`——`MINIMON.ICN` 每只怪物九帧，
  站立一帧、八个方向的攻击各一帧；`Monster::UNKNOWN` 是 0，没有自己的帧。
* 落位锚点 `monsterImageOffset = ( 16, 30 )`：先按瓦片左上角定位，再加锚点，最后加帧自带的 `offset`。
* 精灵跨格（农民 31×28 站在 `(16,30)`，会伸到右边一格和下一行），所以和英雄一样走
  `DivideImageBySquares` 那套：按 32 px 行带切成「瓦片下方 / 瓦片内 / 瓦片上方」三份，分别画在
  Shadow 层之后、`OBJECT_LAYER` 之后、`TopLayer` 之后。
* `drawMonsterAnimation` 还会把帧劈成 body（`transform` 截到 1，去掉影子像素）与 shadow
  （`transform > 1`）两张，因为影子要压在**它覆盖的瓦片上的对象下面**。
* 被攻击时要播攻击动画，这需要知道周围英雄的位置；没有游戏状态时永远画站立帧。

**决策**：

1. **只做 `MonsterSpriteSet` 一层抽象**：`IMonsterSpriteSource.GetMonster( id )` 与英雄层的
   `IHeroSpriteSource` 同构，共用 `IcnSpriteCache`（一个文件名解码一次）。`id ≤ 0`、越界、缺资材都给
   `null`，`AdventureMapView` 遇到 `null` 直接跳过——与参考 `Assets::getImage` 越界返回空 `errorImage`
   等价，与 ADR-058 的缺帧处理同一条规矩。
2. **帧号按 `( id - 1 ) * 9` 现算，不做映射表**：`MonsterSpriteSet.FrameOf` 是纯函数
   （`1→0 / 2→9 / 7→54 / 66→585`），`Mp2MonsterName` 的名字表只用于日志与探针，不参与取帧。
3. **锚点做成 `AdventureMapView.MonsterAnchor` 常量，并把 `BlitTileSprite` 的锚点参数化**：
   英雄传 `Point.Zero`，怪物传 `(16,30)`。原来英雄专用那条路径改名 `TileBand` / `BlitTileSprite`，
   两条绘制路径共用同一份行带裁剪算术，不复制第二份。
4. **整帧 blit，不拆 body / shadow**：`MINIMON` 帧里烘焙好的影子跟着身体一起画。像素等价的前提是
   「同一批瓦片上没有别的对象压在影子上」；本移植还没有「怪物站在树/建筑前」这类逐像素遮挡需求，
   拆帧要引入 `transform` 层复制、两次 blit 与两套行带，收益是影子的层次更准，代价是路径翻倍。
   先在 `DrawMonsters` 的 XML 注释里写清前提与偏差，等真需要时再补。
5. **只画站立帧**：攻击动画需要英雄位置，本项目还没有 `world`；参考在没有 `Game::GetFocus` 时也是站立帧。

**后果**：

* `adventure-map.png` 重建（`170,810` → `183,591` 字节，sha256 `FC71E6B6…`）。与旧基线的差异是
  17,819 / 307,200 个像素，差异图（`tools-output/baseline/diff_adventure-map-en.png`）上的色块全在怪物
  身上，地形 / 对象 / 英雄 / 界面一处未动，说明这次只多画了怪物。
* `MonsterLayerTests` 7 条 + `MonsterSpriteSetTests` 2 条（真资材：农民 31×28、66 有帧、67 与
  `UNKNOWN` 无帧），全量测试 852 → 861。
* 数据核对：`Alteris.mp2` 有 2335 个 `ActionMonster`（48 骷髅 964、49 僵尸 228、50 变异僵尸 222、
  57 骨龙 192），`F:\game\fh2\maps` 前十张图里最大 id 是 65（火元素）；本资材 `MINIMON` 到 66 还有帧，
  现有地图全在覆盖范围内。
* 剩下的怪物相关行为：被攻击时播动画（要英雄位置）、怪物在雷达上是另一套颜色（`Radar` 已按对象类型
  取色）、点击怪物弹出对话框，都留到有 `world` 的里程碑。

## ADR-060：空船用 `BOAT32`/`BOATSHAD` 第 18 帧，锚点 `(0,21)`，并新增一张守船的基线

**背景**：冒险地图上还剩水面上的空船没画。它在 MP2 文件里长得和别的地面对象一样——地面部件存的是
编辑器用的水域图形（`Alteris.mp2` 里是 `OBJNWAT2` 第 23 帧）——所以通用对象路径一直在画那张编辑器帧。
参考的 `maps_tiles_render.cpp` 给它单独一条路径：

* `getBoatSpritesPerTile`：帧号来自主对象部件的 `icnIndex`，`255` 视作 `18`，大于 `128` 表示左向的镜像帧，
  实际帧号是 `icnIndex % 128`；落位 `boatSpriteOffset = ( sprite.x(), tileHeightPx - 11 + sprite.y() )`，
  镜像时 x 换成 `tileWidthPx + 1 - sprite.x() - sprite.width()`。
* `getBoatShadowSpritesPerTile`：影子来自 `BOATSHAD` 的同一帧号，落位 `( shadow.x(), tileHeightPx - 11 + shadow.y() )`。
* 两者都走 `divideImageBySquares`：船体按行带分三段画（`bottomBackgroundImages` → `bottomImages` →
  `topImages`），影子进 `shadowImages`。
* 装载时 `Maps::setInitialObjectInfo` 把船的主对象部件改写成 `BOAT32`，所以文件里那个编辑器帧不会被画。

**决策**：

1. **空船固定用第 18 帧**。`setInitialObjectInfo` 对每一条从 MP2 读进来的船都写 `18`（`RIGHT`），文件里
   并没有别的方向可用；其余七个方向和镜像帧只由 `Maps::Tile::setBoat` 在英雄上下船时写入，而本项目
   还没有 `world`。`BoatSpriteSet.DefaultFrame = 18` 把这件事写在名字里，而不是去猜 `icnIndex % 128`。
2. **`IBoatSpriteSource` 与影子成对**。`GetBoat()` / `GetShadow()` 各一个，与 `IHeroSpriteSource.GetShadow()`
   同构；两个 ICN 共用 `IcnSpriteCache`。没有数据时 `TryCreate` 给 `null`，视图就不画船。
3. **跳过编辑器帧只发生在有船精灵源的视图**。`DrawGroundLayer` 的条件是
   `!(_boats is not null && Mp2ObjectData.IsBoat(tile))`：游戏视图用 `BOAT32` 顶替，本项目的编辑器场景
   不传船精灵源，于是仍然显示编辑器帧——和原作编辑器看到的一致，也避免让编辑器基线无故变脸（ADR-051）。
4. **锚点 `(0, 21)`、可见格放 2 格**。船体 offset `(-8,-54)` 加上锚点后从瓦片上方 33 像素开始，
   比一格还高，所以 `VisibleSpriteTiles(2)`；影子落在 `(-38,-7)`，最多伸到左边两格，同样被 2 格覆盖。
5. **影子整张 blit、和 Shadow 层一起画**。参考逐格拆分是为了在船上盖着别的对象时让影子先落地；
   本移植的场景里船停在开阔水面上，整张画的结果逐像素相同（与 ADR-059 的取舍同理）。
6. **新增第二张基线**。`Alteris.mp2` 居中后的视野里 21 条船一条都看不见，`adventure-map.png` 因此对船
   完全没有约束力（改完仍是 `identical`）。用 `Undeada.mp2`（72×72，城堡 `(38;35)`，船在 `(37;38)`）
   生成 `adventure-map-boats.png`，`VerifyAdventureMapBaseline.ps1` 的 `-MapFile` 接受集合内的裸文件名，
   CI 里多一条带 `-MapFile` / `-Baseline` 的运行。

**后果**：

* 新文件 `adventure-map-boats.png`（`175,095` 字节，sha256 `D120E5BA…`），CI 比对 300,512 个像素、
  忽略 6,688 个外壳像素，与 `adventure-map.png` 用的是同一套框架区域。
* `adventure-map.png` 与 `editor.png` 都不变：前者视野里没有船，后者不接船精灵源。
* `BoatLayerTests` 7 条 + `BoatSpriteSetTests` 2 条（真资材：`BOAT32[18]` 62×56/`(-8,-54)`、
  `BOATSHAD[18]` 74×28/`(-38,-28)`），全量测试 861 → 870。
* 遗留：英雄上下船时船要换方向、船体要有动画、`BOATSHAD` 的逐格遮挡（参考自己都标着 TODO），
  这些都要等 `world` 与英雄移动。

## ADR-061：战争迷雾按 `Maps::drawFog` 移植——`CLOF32` 整瓦 + `CLOP32` 边界件，雾后重画城镇

**背景**：M4 的地形、对象、怪物、英雄与船都齐了，缺雾。参考里雾是**瓦片状态**而不是一层图像：
`Maps::Tile::_fogColors` 存每格对每种颜色「已探索」的位，`Players::FriendColors()` 给观察者集合，
`Tile::isFog( colors )` 是 `( fog & colors ) == colors`（对**所有**人类颜色都没探索才算雾）；
`Maps::drawFog()`（`maps.cpp`）在 `GameArea::Redraw` 的最后一步盖 `CLOF32` 的 4 张整瓦与 `CLOP32`
的 34 帧边界件，然后对每个雾格重画 `OBJNTWBA`，城堡再画 `OBJNTOWN`——所以城镇的轮廓在探明前就可见。
本项目的 `Mp2TileInfo` 不带雾位，也没有 `Maps::Tile` 那种可变世界状态，雾必须自己建。

**决策**：

1. **`MapFog` 存「每格对每种颜色是否已探索」的字节，方向掩码另存一份**。`IsFogged( c, r, colors )`
   就是 `Tile::isFog( colors )`；`GetDirection( c, r )` 是 `Tile::getFogDirection()`，`Unknown` 表示
   该格对当前颜色可见、不画雾。参考的 `updateFogDirectionsInArea` 只更新脏矩形，那是缓存优化，
   这里用 `UpdateDirections( colors )` 一次算全图（当前最大地图 144×144，可忽略）。
2. **八个 `if` 的顺序与「地图外算已雾」都照 `maps_tiles_helper.cpp` 的 `updateFogDirection`**：
   参考先把缓存填 1、越界项不写，所以边界瓦片的雾会「伸到地图外」；`IsFoggedOrOutside` 把这条写成
   显式判断。`FogDirection` 的位与参考的 `DIRECTION_*` 一一对应（中心 + 八方向），`FogDirections`
   给 `All`、`CenterRow`、四个角等常用复合掩码。（ADR-062 把这两个类型改名为 `Maps.Direction` /
   `Maps.Directions`，位与语义不变。）
3. **`FogFrames.Resolve` 保持参考 if/else 的原始判定顺序**：整瓦取 `( column + row ) % 4`，其余落
   34 帧边界表；`Flipped` 对应 `BlitOnTile( ..., revert )`，落位 `x = tileWidth - sprite.x() - sprite.width()`。
   非法掩码回退整瓦（参考的 `switch` 默认分支）。
4. **`IFogSpriteSource` / `FogSpriteSet.TryLoad` 与 `IHeroSpriteSource` 同构**：`CLOF32.TIL` 走
   `TilFile`、`CLOP32.ICN` 走共用的 `IcnSpriteCache`；镜像用 `ImageOps.Flip` 并保留 offset；缺数据或
   解码失败给 `null`，视图不画雾（编辑器就是这样，见 ADR-058 的缺帧规矩）。
5. **雾是最后一层，被它遮住的东西先跳过而不是靠覆盖**。`Draw` 的三段是地形 → 精灵（对象/怪物/英雄/船）
   → 雾；英雄与怪物在 `IsUnderFog` 时 `continue`，船用 `IsUnderFogWithUpperTile`（船伸到上一格，
   两格都被遮才跳过），与参考 `isTileUnderFog && isUpperTileUnderFog` 一致。
6. **初始侦察 = 人类颜色英雄半径 4 的圆盘**（`AllHeroes::Scout`，`FogDiscoveryType::HEROES`）；
   `AdventureMapScene.CreateFog` 再用半径 5（`CASTLE`）揭示一次「聚焦城堡」。参考的 `AllCastles::Scout`
   对每个城堡都做，但 MP2 的城堡表只有 x / y / 类型，`world_loadmap.cpp` 建出来的城堡全是
   `PlayerColor::NONE`，没有归属可判，所以只能把「玩家位置」当城堡用一次。
7. **圆盘几何照 `getSquaredScoutingRadiusLimit`**：`( d + 1 ) ^ 2 - d`，例外 `7 → 66`、`8 → 90`
   （神器能堆到的侦察值）。

**后果**：

* 两张冒险地图基线必须重建（雾改变了可见区）：`adventure-map.png` 183,591 → **136,059** 字节，
  `adventure-map-boats.png` 175,095 → **128,054** 字节；`editor.png` 逐字节不变（编辑器不传 `MapFog`），
  菜单/设置/场景三组也不变。重建后立即跑了不带 `-Update` 的校验，三张都是 `identical`。
* 新增 `MapFogTests` / `FogFramesTests` / `FogSpriteSetTests` / `FogLayerTests` 共 **57** 条
  （真资材：`CLOF32` 四张 32×32、`CLOP32` 34 帧与 offset；`FogLayerTests` 用假雾瓦钉住整瓦变体、
  「已探索格不被覆盖」「雾下英雄不留痕」与「城镇窗口在雾上重画」）。全量测试 870 → **927**，0 警告。
* 实测 `Alteris.mp2`（`HumanPlayers = Blue`，Blue 英雄在 `(83;51)` / `(82;52)`）：初始可见 **82** 格，
  包围盒 `(78;47)-(87;56)`，即只有半径 4 的圆盘 + 聚焦城堡的半径 5。
* 遗留：没有脏矩形缓存；英雄移动、侦察兵与视野类法术都没有增量更新（每次改 `MapFog` 要显式
  `UpdateDirections`）；MP2 没有雾位，「关雾」只能靠不传 `MapFog`，所以还做不到参考的「探索过的雾变暗」。

## ADR-062：`FogDirection` 并入 `Maps.Direction`；`maps.cpp` 的几何函数按「尺寸即实例」移植

**背景**：`Maps::Tile` 与英雄移动要用的第一块地基是 `src/fheroes2/maps/maps.cpp` 的几何函数——
`GetPoint`、`GetIndexFromAbsPoint`、`GetDirection`、`GetDirectionIndex`、`getDirectionPoint`、
`isValidDirection`、`isValidAbsIndex`、`isValidAbsPoint`、`getAroundIndexes`。它们全部只读
`world.w()` / `world.h()`，不碰任何瓦片。方向类型本身是 `heroes/direction.h` 的位掩码枚举（顶左 1、
顶 2、…、中心 0x100），而本项目为迷雾精灵表建过一个同值同义的 `AdventureMap.FogDirection`
（ADR-061 第 2 条）。

**决策**：

1. **不再有第二个方向类型**：`FogDirection` / `FogDirections` 从 `AdventureMap` 移到
   `Game.Maps.Direction` / `Directions`，位值与参考逐条对齐（`TopLeft=1` … `Left=0x80`、`Center=0x100`），
   并把参考里的宏一并搬来：`TopRow`、`BottomRow`、`CenterRow`、`LeftColumn`、`CenterColumn`、
   `RightColumn`、`All`、`Around`、四个角件、`AllCorners`；函数 `String`、`IsDiagonal`、`Reflect`、
   `ShortDistanceClockWise`、`Contains`（参考的 `contains()`）逐行照抄。迷雾那五个文件只改类型名，
   行为由既有迷雾用例守住。参考把枚举放在 `namespace Direction` 里再在外层写同名函数，
   C# 不能同时有同名类型与类，所以枚举叫 `Direction`、函数集中在 `Directions`。
2. **`MapGeometry` 是「尺寸即实例」的值对象**：参考每个函数都读全局 `world`，本项目还没有 `world`。
   把宽高塞进静态字段会造出可变的全局状态，测试之间互相污染，于是改成
   `readonly record struct MapGeometry( int width, int height )`——它承载的正是这些函数从
   `world` 拿的那两个数，函数签名之后与参考一致。`world` 落地时持有它一个实例并转发即可。
3. **`getAroundIndexes` 的第二个重载保持静态**：参考本来就有一个显式收 `( index, width, height, distance )`
   的重载，供地图还没载入时调用（`world_loadmap.cpp` 用它看邻居），照抄为静态方法；实例方法
   转发并填入自己的宽高。半径的方形裁剪、`index > width * height`（而不是 `>=`）这个参考的边界写法
   都保留，并在代码注释里写明。
4. **不可能的分支抛异常**：`getDirectionPoint` 对非方向值，参考是 `assert( 0 )` 后返回 `(-1, 0)`；
   端口抛 `ArgumentOutOfRangeException`——这个值只能由写错的调用方产生，静默返回会变成一个离奇坐标。
   与 ADR-037「不做静默兜底」同一条规矩。

**后果**：

* 新增 `Maps/Direction.cs`（枚举 + `Directions`）与 `Maps/MapGeometry.cs`（9 个几何函数）；
  删除 `AdventureMap/FogDirection.cs`，`MapFog`、`FogFrame`、`AdventureMapView` 与两个迷雾测试文件改成新名字。
* 新增 `DirectionTests` 10 条、`MapGeometryTests` 12 条（位值、复合掩码、`Reflect`、`ShortDistanceClockWise`、
  `String`；4×3 地图上的 index ↔ point、`GetDirection` 与 `GetDirectionIndex` 互逆、边界上的
  `IsValidDirection`、「允许的步一定落在合法索引上」的全量对拍、`getAroundIndexes` 的裁剪与半径）。
* 迷雾精灵表、英雄/怪物图层、基线截图全部不变（纯改名 + 新增几何）。
* 下一步：`Maps::Tile` 与 `world` 可以开始搬了，几何部分不再缺。

## ADR-063：`Maps::Tile` 分片移植，先搬不依赖 `world` 的那半边

**背景**：`maps_tiles.cpp` 1663 行、`maps_tiles.h` 437 行，一大半函数直接读写全局 `world`、英雄表、
迷雾、region 图和地面过渡表。一次搬完等于把 `world` 一起搬，违背 ADR-057「一次一支、能验证」的节奏。

**决策**：

1. **按「能不能只靠瓦片自己回答」切片**。本轮搬：`ObjectPart` 与 `Tile` 的对象层
   （三件容器、`sortObjectParts`、`removeObjects`、`replaceObject`、`updateObjectImageIndex`、
   `updateTileObjectIcnIndex`、三个查询函数）、对象类型缓存、metadata、地形图号与 flags、
   道路位（`isSpriteRoad`/`updateRoadFlag`/`isRoad`）、通行掩码（`getPassable`/`isPassableFrom`/
   `isPassableTo`）、`Init( MP2TileInfo )`、`getGround`/`isWater`。
   明确不搬（随 `world`）：`getHero`/`setHero`、`getMainObjectType( ignoreObjectUnderHero )`、
   船、迷雾位（`MapFog` 已覆盖，ADR-061）、`setInitialPassability`/`updatePassability`/
   `getTileIndependentPassability`、`updateObjectType`、`removeObjectPartsByUID` 的尾部（碰邻居）、
   `fixMP2MapTileObjectType`、`getIndexOfMainTile`、`isAnyTallObjectOnTile`、`isDetachedObject`、
   `isShadow`（要 `isValidShadowSprite` 那张表）、所有权旗标、`String()`、序列化运算符。
2. **`Tile` 用类不用结构**：参考是值类型，但只在存档运算符里拷贝；端口的三件容器是堆上的，
   拷贝语义要等序列化那一步再定，现在做成 `sealed class` 并在注释里写明。
3. **`sortObjectParts` 的两个坑都在注释里写清**：
   (a) 参考用 `std::list::sort`（稳定），`List<T>.Sort` 不稳定、会把同层部件重排，所以端口走
   `OrderByDescending` 这条稳定路径；
   (b) 参考把列表按层**降序**排完后再从尾往前找第一个非 `FLAG32`，所以被提为 main 的是**层值最小**
   的那个（有 object 层就选 object 层），不是「最上层」——变量名 `highestPriorityPartIter` 说的是
   「当主对象」的优先级。用例把这条钉住了，避免后人按直觉改反。
4. **保留两个参考怪癖**：`removeObjects` 之后不动对象类型（参考自己留了 TODO）；
   `isPassabilityTransparent` 在空瓦片上是 `false`（默认 main 部件在 object 层），且该函数在参考里
   没有任何调用点——端口照抄而不「修好」。
5. **`setMainObjectType` 里少了 `world.resetPathfinder()`**：路径查找器还不存在，缓存的类型照参考写入，
   重置留给 `world` 那一步。`Init` 也不设地形（参考由 `world_loadmap.cpp` 另行 `setTerrain`）。

**后果**：新增 `Maps/ObjectPart.cs`、`Maps/Tile.cs`；`TileTests` 21 条（Init 的 metadata/图层/裂纹特例/
道路位、排序与 flag 跳过、移主部件、移除/替换/换图、三个查询、`doesObjectExist` 只看主层与地面层、
空瓦片不透明的怪癖、`isSpriteRoad` 三张表、默认全向可通行）。`FHeroes2.Game.Tests` 442 → **475**。
`maps_tiles.cpp` 仍是「部分」，覆盖率合计不变（26/42/181）。

## ADR-064：`Maps::Tile` 第二片——把「不碰邻居」的通行计算提前搬完

**背景**：ADR-063 把 `setInitialPassability`/`updatePassability`/`getTileIndependentPassability` 都推给了
`world`。但 `getTileIndependentPassability` 只读本瓦片的部件，唯一的依赖是 `maps_tiles.cpp` 匿名命名空间里
那几张「对象分类」小表（阴影图号、短对象、可脱离对象、组合对象、暗礁图号）与 `mp2.cpp` 的
`getActionObjectDirection`。如果等 `world` 一起搬，`world` 那一步会同时背着「全局状态」和「对象分类表」
两坨不相干的东西，出问题时分不清是哪边的错。

**决策**：

1. **对象分类集中到 `Game/Maps/TileObjectTraits.cs`**，不散落在 `Tile` 里。参考里这些是文件级 `static`，
   语义上属于「瓦片上的部件是什么」这一层，将来 `world` 的通行重算、绘制、`isAnyTallObjectOnTile`
   与寻路都要用；`Tile` 只是第一个调用方。名字取 `TileObjectTraits` 而不是重复 `MapsTiles`。
2. **阴影表用 `Mp2ObjectIcnType` 而不是参考的 `ICN` id**：`getIcnIdFromObjectIcnType` 在
   `MTNSHAD`/`OBJNCRCK`/… 这几张表上都是 1:1，多绕一层没有收益。17 张表的数值逐字照抄，
   包括重复项，不「去重整理」——以后对表出问题，能一眼看出是抄错还是参考本来如此。
3. **`GetActionObjectDirection` 的 `case` 必须写 `Action*`**：`MP2::MapObjectType` 里 action 值 =
   同义的非 action 值 + `OBJ_ACTION_OBJECT_TYPE`(128)，而对象目录存的是 action 值。写成非 action 名
   编译得过、运行时永远走 `default`——这是一个只有对拍才抓得到的坑，注释与用例都钉住了。
   `default` 保留 `Debug.Assert` 后返回 `Direction.Unknown`，**不抛**：参考也只在断言后返回，
   且这个函数在遍历所有部件时被调用，抛异常等于把「未知对象」升级成崩溃，改变行为。
4. **遍历顺序照抄**：主部件 → 地面层部件**逆序**（层值从大到小）→ 逐部件取 `&` 交集；遇到 action
   对象立刻返回它自己的方向；`top` 层不参与。地面层逆序这条最容易按直觉写正，`TilePassabilityTests`
   两条用例把它夹住。
5. **`IsStreamPart` 的两段图号照抄**：`OBJNMUL2` 的 `< 14` 与 `218..231`，不做「看起来更整齐」的合并。
6. **`SetInitialPassability` 纯赋值**，不碰邻居、不发通知；`updatePassability` 与英雄占用仍留给 `world`。

**后果**：新增 `Game/Maps/TileObjectTraits.cs`；`Tile` 补 `IsStream`/`IsShadow`/`GetTileIndependentPassability`/
`SetInitialPassability` 与两个私有辅助。`TilePassabilityTests` 12 个方法、15 条用例（道路/溪流/`FLAG32`
四种图号用 Theory）：空瓦片全通、object 层挡中下两行、主部件同样被检查、地形层永不挡、阴影永不挡、
action 对象用自身方向（木材全通 / 矿只可下方进入）、主部件优先且 action 提前终止、暗礁全禁、
`SetInitialPassability` 写入的掩码能被 `IsPassableFrom` 读到、`IsStream` 两段图号、`IsShadow` 要求全部部件
都是阴影。`FHeroes2.Game.Tests` 475 → **490**，全解决方案 1081 条全绿，0 警告 0 错误。
`maps_tiles.cpp` 仍是「部分」，覆盖率合计不变（26/42/181）——剩下的通行相关只剩 `updatePassability` 与
英雄占用，两者都要 `world`。

## ADR-065：`World` 先立骨架——尺寸、瓦片表、取瓦片，其余随各自系统

**背景**：`world.h` 592 行、`world.cpp` 1691 行，`World` 一个类里塞了二十多个容器（英雄、城堡、王国、
俘获对象、地图对象、事件、谣言、区域图）和一百多个成员函数。而 `Maps::Tile` 剩下的成员
（`updateObjectType`、`updatePassability`、`getHero`/`setHero`、`isAnyTallObjectOnTile`）全都只是
「读 `world`」。先搬整个 `World` 等于把 `heroes`/`castle`/`kingdom`/`army` 一起搬，那是 PLAN 里第 4 条
的顺序，不能倒过来。

**决策**：

1. **只搬 `width`/`height`/`vec_tiles` 与围绕它们的三件事**：两个 `getTile`、`generateUninitializedMap`、
   `Reset`，外加 `ResetPathfinder()` 的空占位。其余成员一律等所属系统——日期（`_day`/`_week`/`_month`
   与 `NewDay` 一族吃王国与事件）、`Defaults()`（建王国/英雄/城堡）、俘获对象、地图对象、区域图、
   静态分析、`updatePassabilities`（要 `Tile.updateObjectType` 与 `updatePassability`）。
2. **门面照 ADR-056，构造函数改 `internal`**：`World.Current` 就是参考的 `World::Get()` / 全局 `world`。
   参考的构造函数是 private（配 `World::Get()` 里的函数内静态变量，是标准单例），但这样测试与「第二张
   地图」（编辑器、预览）都造不出独立实例；`internal` 让 `FHeroes2.Game.Tests` 能 `new World()`，生产代码仍只走
   `Current`。`sealed class` 代替参考里删掉的拷贝/移动构造。
3. **命名空间用根 `FHeroes2.Game`，不建 `...World` 子命名空间**：类名 `World` 与 `FHeroes2.Game.World`
   同名会让命名空间内的每次引用都多一层解析歧义，而参考里这也是一个顶层类型（`world.h` 的
   `class World`，全局 `world`）。
4. **`Geometry` 是 `MapGeometry?`**：ADR-062 说 `world` 落地时持有 `MapGeometry` 并转发，但
   `MapGeometry` 拒绝 0 维，而参考的 `World` 初始就是 0 × 0。空世界返回 `null`，有地图时返回一个按
   当前尺寸构造的值——`MapGeometry` 是 `readonly record struct`，每次取都等价，不必存字段。
5. **`getTile( x, y )` 只校验算出来的索引，不校验坐标**：参考写的是 `vec_tiles.at( y * width + x )`，
   所以 `x == width` 会静默落到下一行第一格、负 `x` 会借上一行。`WorldTests` 把这个怪癖钉住，
   不「顺手加校验」——所有调用方的坐标都来自 `MapGeometry`，那里不会越界。
6. **`GenerateUninitializedMap` 不设瓦片索引**：参考只 `resize`，索引由 loader 逐个 `setIndex`
   （`world_loadmap.cpp:279`），`generateBattleOnlyMap` 则自己循环设。端口照抄，测试断言新建瓦片的
   `Index` 仍是 0——在这里补上会让「loader 忘了设索引」变成静默通过。参考同函数里还有三件事没搬：
   往 `Settings` 写 `Maps::FileInfo`、`Defaults()`、`vec_tiles.empty()` 的断言。
7. **`Reset()` 从 private 改 public**：参考只被析构与两个 loader 调用，而两个 loader 都还没搬；
   编辑器场景与测试都需要「回到空世界」。这是显式记录的偏差，不是疏忽。
8. **`ResetPathfinder()` 是空方法**：`world_pathfinding.cpp` 与 `AI::Planner` 都不存在，但
   `Tile::setMainObjectType` 以及将来所有改地图的路径都要调它；先把调用点留好，省得以后逐个改两次。

**后果**：新增 `src/FHeroes2.Game/World/World.cs`；`WorldTests` 8 个方法 / 17 条用例（空世界、
`Current` 是同一个实例、生成 N×N 的默认瓦片、索引留在 0、`Geometry` 跟随尺寸、坐标与索引一致、
坐标不校验的环绕、越界抛 `ArgumentOutOfRangeException`、非正尺寸报错、重新生成前先清空、`Reset`）。
`FHeroes2.Game.Tests` 490 → **507**，全解决方案 1098 条全绿，0 警告 0 错误。
`world.cpp` 从「未转」转「部分」，覆盖合计 26/42/181 → **26/43/180**（总数仍是 249）。

## ADR-066：`Maps::Tile` 第三片——邻居相关的通行与对象类型，把 `world` 传给瓦片

**背景**：ADR-065 立完 `World` 骨架后，`Tile` 剩下的成员全都能写了。它们不是一堆独立函数，而是一条
调用链：`updatePassability` → `isDetachedObject` → `getIndexOfMainTile`，以及 `updatePassability` →
`isAnyTallObjectOnTile`，再加上 `updateObjectType`（`World::updatePassabilities` 第一趟就要它）。
拆开搬等于为了「一次只改一点」而把同一条链分三轮，每轮都得留一个临时的假实现。

**决策**：

1. **一次搬完整条链**：`UpdateObjectType`、`UpdatePassability`、`IsAnyTallObjectOnTile`、
   `IsDetachedObject`、`GetIndexOfMainTile`（`static`）+ `World.UpdatePassabilities`。这五个是同一个语义
   单元——「一个瓦片能不能走，取决于它自己和它上下左右的邻居」。
2. **`World` 作为参数显式传入，不走 `World.Current`**：`Tile` 是 `World` 的成员，让瓦片反向持有
   `World` 会形成环，还会让「没挂到世界上的瓦片」变成运行期炸弹；`World.Current` 则让每个测试都得去
   改全局静态量。签名因此是 `UpdatePassability(World world)`、`IsDetachedObject(World world)` 等，
   `World.UpdatePassabilities()` 传 `this`，测试传自己的局部世界——并行安全，且与 `MapGeometry`
   的「尺寸由调用方拿着走」一致（ADR-062）。参考里这些是全局 `world`，所以**每个调用点**都要记得传；
   目前只有 `World.UpdatePassabilities` 一处。
3. **修正 `Mp2ObjectTypes.BaseActionType` 为忠实版**：旧实现是「不是 action 值就加 128（`Event` 除外）」，
   而参考的 `getBaseActionObjectType` 先问 `isObjectCanBeAction`——树、山、湖这类装饰没有 action 对应值。
   旧实现会让 `isOffGameActionObject(correctedObjectType)` 对树也返回真，`updatePassability` 就会把每一棵
   树下面那格判成「上方是不可进入的高物体」。同时补上 `CanBeActionObject`（`mp2.cpp:38` 的 23 项表）。
   注意参考的 `MP2::OBJ_EVENT` 是 **action 值**（`mp2.h:291`，`OBJ_NON_ACTION_EVENT + 128`），端口里
   对应 `Mp2ObjectType.ActionEvent`，`Event` 是非 action 孪生值——旧实现对 `Event` 的特判正好反了。
   这一改没有既有调用方、没有既有测试，纯粹修正。
4. **`updateObjectType` 的事件分支不搬**：参考在这里查 `world.GetMapEvent()`，找不到就把类型清成
   `OBJ_NONE`；事件容器还没有，所以类型一律保留——这等于参考「事件数据完好」时的行为，而不是「猜一个」。
   瓦片类型判定的其余部分（主部件 → 地面层逆序 → 顶部件 → 海崖）逐字照抄。
5. **断言分两类**：`assert( _tilePassabilityDirections == getTileIndependentPassability() )`（重复调用检查）
   保留为 `Debug.Assert`——release 里参考也只是继续算，抛异常会改变行为；`assert( part._uid != 0 )`
   （数据完整性）**不搬**，同理：release 里是空操作，把它变成异常等于让一张怪地图直接崩掉，
   而参考的容错正是「算出来难看但能进游戏」。
6. **`SetMainObjectType` 里参考的 `world.resetPathfinder()` 仍然欠着**：`Tile` 现在没有世界引用，
   等路径查找器落地时在与它同一步处理（ADR-065 也留了这条）。
7. **测试必须自己设 `Tile.Index`**：`GenerateUninitializedMap` 故意不设索引（ADR-065），而这里的每一个
   邻居查找都是 `index ± 1 / ± width` 算出来的。测试里那条「手工设索引」的循环就是 loader 的活儿，
   顺便证明了「忘了设索引」会立刻表现为错乱，而不是静默通过。

**后果**：`World` 新增 `UpdatePassabilities()`；`Tile` 新增五个成员；`TileNeighbourPassabilityTests` 13 个方法 /
15 条用例（空陆地世界全通、贴底边不可通行、陆上物体压在水上不可通行、下方同 UID 延续不可通行、
action 对象不被邻居收窄、顶出物体的上格报告、左上对角被高物体吃掉、邻居顶向可通时不吃对角、
第一行没有高物体、海崖判定、水面不变海崖、事件类型保留、`IsDetachedObject` 三个分支、
`GetIndexOfMainTile` 的两条出口）。`FHeroes2.Game.Tests` 507 → **522**，全解决方案 1113 条全绿，
0 警告 0 错误。覆盖率合计不变（26/43/180，总数 249）：`maps_tiles.cpp` 与 `world.cpp` 本来就在「部分」里。

## ADR-067：`World::LoadMapMP2` 的可移植子集 + 对象 UID 计数器归世界——顺带修正对象表的 action 类型

**背景**：ADR-066 让「瓦片能不能走」这条链跑通了，但它只在合成地图（`Mp2Map.CreateBlank`）上验证过。
`world_loadmap.cpp:241` 的 `LoadMapMP2` 是让这条链第一次吃上真地图数据的入口，而它本身有 900 多行：
瓦片循环、`PostLoad`、城堡表、俘获对象表、信息块（英雄/城堡/招牌/瓶子/事件/狮身人面像/谣言/方尖碑）都在里面。
一次搬完等于把 `castle`、`kingdom`、英雄一起搬，违背 ADR-057 的节奏。

**决策**：

1. **只搬「决定有哪些瓦片、它们能不能走」的那部分**：`World.LoadMap(Mp2Map)` = 尺寸校验 → `Reset()` →
   逐瓦片 `setIndex`/`setTerrain`/`Tile.Init` → add-on 链（`EnumerateAddonChain`）→ `SortObjectParts` →
   全图 `FixMP2MapTileObjectType` → `PostLoad`。逐语句对齐 `world_loadmap.cpp:241-…`，
   注释标出参考行号。**没有搬**：城堡表、俘获对象表、信息块、Price of Loyalty 元数据、`PostLoad` 里的
   传送门/漩涡/魔眼缓存、`ComputeStaticAnalysis` 的 AI 区域。这些都不改变「瓦片存在性 + 通行掩码」，
   而它们是同一个方法里的顺序副作用，等各自系统落地时按参考顺序插回去。
2. **`PostLoad` 拆成可运行子集**：`UpdatePassabilities()` → `ResetPathfinder()`（占位，ADR-065）→
   扫全部部件取最大 UID 抬高计数器。参数化的两个开关保留参考语义：
   `setTilePassabilities`（编辑器重载传 `false` 时跳过通行重算）与 `updateUidCounterToMaximum`
   （编辑器自己维护 UID 时不抬高）。私有方法，不做成公开 API——参考里也只有这两个调用者。
3. **对象 UID 计数器从全局静态改成世界实例属性**：`world_object_uid.cpp` 是一个 `static uint32_t` +
   三个自由函数，而端口里「一个世界」是可以并存的（测试并行、编辑器预览）。因此 `World` 承载
   `LastObjectUid` / `SetLastObjectUid` / `GetNewObjectUid` / `ResetObjectUids`，
   `world_loadmap.cpp` 里对 `Maps::getNewObjectUID()` 的调用点直接落到 `world.GetNewObjectUid()`。
   语义细节照抄：`GetNewObjectUid` 是**前置自增**（先加后返回）；`ResetObjectUids` 只有编辑器用，
   不放进 `Reset()`（参考的地图装载也不重置它，装载后由文件里的值覆盖）。
4. **`LoadMap` 抛异常、`TryLoadMap` 返回 `false`**：参考的 `LoadMapMP2` 失败时返回 `false`（文件坏了是常态）。
   端口把「解析失败」关在 `TryLoadMap` 里（`Mp2Map.TryRead` 失败即 `false`），把「调用方给了自相矛盾的
   参数」关在 `LoadMap` 里（宽高与瓦片数不符抛 `ArgumentException`）——两种情况不该用同一个返回值表达。
5. **顺带修正对象表里 3 个手写文件的对象类型（这是本轮的真实 bug）**：`map_object_info.cpp` 里的主类型
   用的是 `MP2::OBJ_*`，而 `MP2::OBJ_*` 是**带 action 位**的值（`mp2.h:273-315`，
   `OBJ_ARTIFACT = OBJ_NON_ACTION_ARTIFACT + 128`）。端口的 C# 枚举把两者分名（`Artifact` = 41、
   `ActionArtifact` = 169），而生成器按**数值**配对，所以 `MapObjectCatalog.Generated.cs` 是对的；
   三个手写文件（`Artifacts`、`Monsters`、`Kingdom`）却照 C++ 的**名字**直译，落成了非 action 值。
   后果不是「有点不一样」而是直接崩：`Kingdom.AddTown` 里 `mainObjectType - OBJ_ACTION_OBJECT_TYPE`
   对 35 得到 65443，对 48 得到 65456（`ushort` 环绕），这个值被写进城堡部件的 `ObjectType`，
   于是 `GetObjectTypeByIcn` 对着真实地图 `Beltway.mp2` 返回 65456——一个任何地图都不会出现的类型。
   三个文件改为 `ActionArtifact` / `ActionMonster` / `ActionCastle` 家族，并在文件头写明「参考的 `OBJ_X`
   是 action 值，端口的 `X` 是非 action 孪生」。`LandscapeTownBasements` 与 `LandscapeFlags` **不**改：
   参考在这两处特意用 `MP2::OBJ_NON_ACTION_CASTLE`（`map_object_info.cpp:3779`），因为城基和旗帜是城堡的
   零件，不是可交互对象。
6. **加一条覆盖整张对象表的回归断言**：`ObjectsWithAnActionCounterpartAreStoredWithTheActionType` 遍历全部
   组，凡 `CanBeActionObject` 为真的主类型都必须带 action 位，只有上一条那两个城基/旗帜组例外
   （另有专门用例钉住它们是 `Castle`）。这样「照 C++ 名字直译」这个坑再犯一次会立刻红。

**后果**：`World` 新增 `LoadMap`/`TryLoadMap`/`PostLoad`/`LastObjectUid`/`SetLastObjectUid`/`GetNewObjectUid`/
`ResetObjectUids`；`Tile` 新增 `FixMP2MapTileObjectType`（含 `UpdatePriceOfLoyaltyObjectType`）；
`MapObjectCatalog` 三个手写表修正类型。新增 `WorldLoadMapTests` 14 个方法 / 14 条用例（瓦片与地形复制、
ground+top 部件与排序、add-on 链、礁石修复、随机怪物占位符、绿洲顶部件、沙漠骨架、水格海岸清除、
两格同 UID 使上格不可通行、计数器来自文件、计数器抬到最大 UID、重装载清空、尺寸不符抛异常、
非地图文件返回 `false`、真实 `Beltway.mp2` 72×72 全图校验），以及 `MapObjectCatalogTests` 2 条新用例。
`FHeroes2.Game.Tests` 522 → **539**，全解决方案 1130 条全绿，0 警告 0 错误。
覆盖率：`world` 组 `world_loadmap.cpp` 与 `world_object_uid.cpp` 都是「部分」，行数 0/1/4 → **0/3/2**，
合计 26/43/180 → **26/45/178**（总数 249 不变）。

**遗留**：`world_loadmap.cpp` 仍是「部分」——城堡/俘获对象/信息块/传送门缓存/AI 区域未转；`LoadMap` 里
`Tile.setHero`/`getHero`（英雄占用）也还没有，所以英雄格子的通行按「无英雄」算。

## ADR-068：英雄占用——`Tile.setHero`/`getHero` 与最小 `Hero` 契约

**背景**：ADR-066 的 `updatePassability()` 里有一支「看穿英雄」的分支（`maps_tiles.cpp:686`：
"get object type but ignore heroes as they are temporary objects"），ADR-067 把它记成了遗留。
英雄本体有几千行（军队、技能、法术书、宝物、路径、经验、移动力、肖像），不可能整搬；而瓦片这一侧
只需要两样东西：**id** 和**英雄脚下的对象类型**。

**决策**：

1. **只契约，不搬英雄**：`Heroes` 的 id 表（`heroes.h:87-186`：六个种族各 9 个 = 54、战役 6、
   The Price of Loyalty 11、`DEBUG_HERO`，加上 `UNKNOWN` 共 73）、`getObjectTypeUnderHero()` /
   `setObjectTypeUnderHero()`（`heroes.h:405` / `:410`）、`isValidId()`（`:723`）。其余成员随各自的
   系统落地。参考的 id 由 `AllHeroes::Init()` 一次性铸好，端口改为构造参数（`Hero(int id)`），
   因为「全部英雄」这件事还不存在。
2. **英雄表挂在世界上**：`World.Heroes` / `AddHero` / `GetHero`。参考的 `AllHeroes` 是「73 个槽、按 id
   直接索引」，端口用列表加线性查找——半成品世界里只会有少数英雄，`AllHeroes::Init()` 还没搬。
   参考靠「一个 id 一个槽」天然保证唯一，端口把这条不变量显式化：`AddHero` 遇到重复 id 抛
   `ArgumentException`（重复会让 `GetHero` 歧义，占用它的格子永远清不掉）。
3. **瓦片只存 id**：`_occupantHeroId`（`uint8`，0 = 无英雄，对应 `maps_tiles.h:417`）。`GetHero()` 要
   同时满足「缓存的类型是 `OBJ_HERO`」和「id 是合法英雄 id」，再去世界上查——参考也是这两个条件
   （`maps_tiles.cpp:429`），查不到就是 `nullptr`。
4. **`getMainObjectType` 的两种形态都要有**：`MainObjectType` 是缓存值（`getMainObjectType( true )`，
   英雄站在上面时就是 `OBJ_HERO`），`GetMainObjectType(world, ignoreObjectUnderHero: false)` 看穿英雄。
   这不是「多写一个 API」：`updatePassability()`（`:686`）与 `fixMP2MapTileObjectType()`（`:1323`）读的
   都是 `false` 形态，用缓存值会让英雄脚下的格子按「英雄」而不是「英雄踩着的物体」判通行。
5. **`MP2::OBJ_HERO` 是 action 值（183）**：`mp2.h:327` 里 `OBJ_HERO = OBJ_NON_ACTION_HERO + 128`，
   而 `OBJ_NON_ACTION_HERO`（55）的注释是 "Never set in maps"。所以 `setHero` 写入的是
   `Mp2ObjectType.ActionHero`，`setObjectTypeUnderHero()` 里「不许把英雄存成脚下类型」的判断也比较
   `ActionHero`。写错成 `Mp2ObjectType.Hero` 的后果是：瓦片缓存一个游戏从不使用的类型，通行、寻路、
   渲染的 action 判定全部落空——这跟 ADR-067 修的是同一类错误，本轮在写代码时再次抓到
   （`Mp2MapHero` 早就两种都认，所以地图读取那一侧没有暴露它）。
6. **pathfinder 复位放在 `SetHero` 里**：参考的 `setMainObjectType()` 结尾会
   `world.resetPathfinder()`（`maps_tiles.cpp:477`），因为寻路缓存了「是不是 action 对象」。端口的
   `SetMainObjectType` 没有 world，`SetHero` 是唯一同时拿到 world 的调用点，所以复位写在它里面，
   并在 setter 的注释里写明「等 pathfinder 落地时给 setter 加带 world 的重载，调用点再迁」。
7. **水格海岸修复的英雄分支补上**：`fixMP2MapTileObjectType()` 的 `Coast && IsWater` 分支
   （`maps_tiles.cpp:1353-1360`）现在区分有没有英雄——有英雄就把「空」存到英雄身上，否则改瓦片缓存；
   两种情况下瓦片本身都还是 `OBJ_HERO`。

**后果**：新增 `src/FHeroes2.Game/Heroes/Hero.cs`；`World` 加 `Heroes`/`AddHero`/`GetHero`，`Reset()`
连英雄表一起清；`Tile` 加 `OccupantHeroId`/`GetHero`/`SetHero`/`GetMainObjectTypeUnderHero`/
`GetMainObjectType`，`UpdatePassability` 与 `FixMP2MapTileObjectType` 改读
`GetMainObjectType(world, false)`。`HeroOccupancyTests` 13 个方法 / 13 条用例（无英雄、停放与恢复、
世界不认识的英雄退回 `updateObjectType`、id 表与 `ActionHero` 的数值锚点、水格海岸的英雄分支、
通行看穿英雄、重复 id、id 越界、重置清表）。`FHeroes2.Game.Tests` 539 → **552**，全解决方案
**1143** 条全绿，0 警告 0 错误。

**覆盖率不变**：`heroes` 行仍是 1/0/11。这一片只有 id 表加一个查找，`heroes.cpp` 的构造、移动、
招募、法术书全都未动，记成「部分」会把口径弄虚。

**遗留**：地图装载还不创建英雄——`world_loadmap.cpp` 的 `LoadMapMP2` 目前在端口里只处理瓦片，
`PostLoad` 之后没有「按 `Mp2MapHero` 建英雄并 `setHero` 到格子上」这一步；那需要英雄的位置、
种族与 `world.GetTile(...).SetHero(...)` 一起搬，等英雄主体与 `castle`/`kingdom` 一起做。

## ADR-069：地图对象查询与 `PostLoad` 的三个缓存

**背景**：ADR-068 之后「这块格子上站着谁」能答了，但「哪种对象在哪几格」还不能——寻路、AI、事件、
传送门全都伸手要它。参考里这一层是 `maps.cpp` 的三个查询（`doesObjectExistOnMap` `:601`、
`GetObjectPositions` `:613` 走 `MapsIndexesObject` `:65`、`getObjectParts` `:618`）加上
`maps_tiles_helper.cpp:487` 的 `getObjectPartByActionType()`；`World::PostLoad()`（`world.cpp:1200`）
里的三张缓存是它们的第一批消费者。

**决策**：

1. **查询挂在 `World` 上**：参考靠全局 `world.getTile(...)` 干活，端口没有全局单例，世界实例就是那个
   上下文，所以 `GetObjectPositions` / `GetObjectParts` / `DoesObjectExistOnMap` 是实例方法。
   `maps.cpp` 的覆盖率不变（几何之后仍是「部分」）。
2. **`ignoreHeroes` 的双重反转照抄**：`MapsIndexesObject` 比较的是 `getMainObjectType( !ignoreHeroes )`
   （`maps.cpp:70`）。默认 `true`（`GetObjectPositions` 的默认）→ `getMainObjectType( false )` →
   **看穿英雄**，英雄踩着的资源仍然算存在；传 `false` → 缓存的类型，也就是英雄本身。参数名和直觉相反，
   注释与两个用例把两种语义都钉住了。
3. **`getObjectPartByActionType` 只认 action 值**：没有 action 位就返回 `null`
   （`MP2::isOffGameActionObject`），所以调用方写 `ActionStoneLiths`(164) 而不是 `StoneLiths`(36)。
   这是 ADR-067 / ADR-068 那个坑的第三次现身——本片所有调用点一律 action 值，用例
   `GetObjectPartByActionTypeNeedsTheActionBit` 专门测这条。
4. **`getObjectParts` 走部件而不是瓦片类型**：漩涡可以被英雄丢下的小舟盖住，那种瓦片被标成 boat，
   只按类型找会漏（`world.cpp:1215` 的注释就是这么说的）。返回 `(index, part)`；`ObjectPart` 是 struct，
   所以 `part` 是副本，调用方只读 `IcnIndex` 与 `Uid`——这一点写在方法注释里，避免有人拿它当句柄改。
5. **三张缓存挂 `World`**：`AllTeleports` / `AllWhirlpools` 是 `Dictionary<byte, List<int>>`，
   键是 sprite 索引（两张石阵只有 sprite 相同才互相配对），`AllEyeOfMagi` 是 `List<int>`。
   填充位置照 `PostLoad` 的顺序：通行之后、寻路复位之前。
6. **`Reset()` 也清这三张表**：参考只在 `PostLoad` 里清，端口多清一处。理由很实在——缓存装的是瓦片
   下标，世界清空后还留着就是悬空数据，而 `Reset()` 是所有装载路径的入口。
7. **读取端没搬**：`GetTeleportEndPoints` / `GetWhirlpoolEndPoints` 内部用 `Rand::Get` 在候选里抽一个
   （`world.cpp:735`），而 `rand.cpp` 还没转。缓存先立，等寻路与 `rand.cpp` 一起接，免得凭空造一个
   假的随机源。

**后果**：`Tile.GetObjectPartByActionType()`；`World` 加
`AllTeleports`/`AllWhirlpools`/`AllEyeOfMagi`/`GetObjectPositions`/`GetObjectParts`/`DoesObjectExistOnMap`，
`Reset()` 清三表，`PostLoad()` 填三表。`World` 类头与 `LoadMap` 的「not here」清单缩到：城堡表、
俘获对象、信息块、PoL 元数据、AI 区域。`MapObjectQueryTests` 9 条（空白图无对象、石阵按 sprite 分组、
漩涡与法师之眼、只有类型没有部件的残图、action 位、部件在别的主部件之下、看穿英雄的正反问法、
`Reset` 清缓存）。`FHeroes2.Game.Tests` 552 → **561**，全解决方案 **1152** 条全绿，0 警告 0 错误。

**覆盖率不变**：`maps.cpp` 仍「部分」（几何 + 查询已转，渲染/`getObjectFromTile` 等未转），
`world.cpp` 仍「部分」。这一片是补厚既有两个文件，不新增「转」。

**遗留**：`GetTeleportEndPoints` / `GetWhirlpoolEndPoints`（等 `rand.cpp`）；AI 区域
`ComputeStaticAnalysis`；信息块与城堡表。

## ADR-070：`Rand` 门面与传送门/漩涡端点

**背景**：`Pcg32` 早就转了（`PLAN` 的 `engine` 行把 `rand.cpp` 记成已转），但 `Rand::` 那一层——
线程级发生器、`Get` / `GetWithSeed` / `GetWithGen`、`Get(vector)`、`Shuffle`、`Queue`——一直缺着。
于是 ADR-069 把 `GetTeleportEndPoints` / `GetWhirlpoolEndPoints` 停在「等 `rand.cpp`」上，
`Mp2ObjectType` 之外任何要掷骰子的地方也没有统一入口。

**决策**：

1. **线程发生器就是 `ThreadLocal<Pcg32>`**：参考是 `thread_local std::random_device rd; thread_local PCG32 gen( rd )`
   （`rand.cpp:87`），端口用 `ThreadLocal<Pcg32>` 加工厂——首次使用建一次，之后同一线程看到一条连续序列。
   种子取 OS 熵，两个 32 位合成 64 位（对应 `rand.h:106` 的 `generateUInt64`），所以**线程序列不可复现**；
   要与参考对齐、或在测试里重现，必须走 `GetWithSeed` 或自带一个 `Pcg32`。参考也是这两条路。
2. **`Get(from, to = 0)` 先交换**（`rand.cpp:95`）：`Get(6)` 因此就是 `[0, 6]`，正合默认参数的意思。
   区间映射沿用 `Pcg32.NextInclusive`（Lemire，`rand.cpp:33`），上下界语义与参考一致。
3. **多一个 `int` 重载，但拒绝负数**。参考参数是 `uint32_t`，传负数会静默变成巨大区间（实际没人这么调）。
   端口与其照抄这个坑，不如抛 `ArgumentOutOfRangeException`——写错的调用当场炸，而不是掷出一个离谱的值。
4. **`GetWithSeed` 每次新建再丢弃**（`rand.cpp:108`）：同参数永远同值。这不是「有状态」，是参考的行为，
   用例 `GetWithSeed_IsReproducibleAndMatchesADedicatedGenerator` 钉住它等同 `new Pcg32(seed).NextInclusive(...)`。
5. **`Get(vector)` 空表抛 `ArgumentException`**（参考 `assert`，release 下是未定义行为）。返回的是元素**值**
   不是引用（`rand.h:196` 返回 `const T &`），调用方持有的都是 `int`，没有差别。
6. **`Queue` 的三条契约照抄**：`Push` 丢掉 0 权重（`rand.h:234`）、空池 `Get()` 得 0（`rand.cpp:123`）、
   按权重切片线性扫描（`rand.cpp:133`）。参考构造函数的 `reserve` 容量提示在 C# 没有意义，端口不收这个参数。
7. **`combineSeedWithValueHash` 没搬**：它靠 `std::hash`，同一个值在不同标准库会给出不同哈希，本身就不是
   跨实现可复现的东西。等地图随机生成真要用它时，改用端口自己的确定性哈希，并在那时单独立 ADR。
8. **端点查询照抄不对称**：`GetTeleportEndPoints` / `GetWhirlpoolEndPoints`（`world.cpp:704/748`）的**入口**
   用 `getMainObjectType( false )`（看穿英雄：英雄踩在石阵上仍算入口），**候选**用缓存类型（英雄站在目的地
   就把目的地排除）。这不是笔误，`world.cpp:710` 与 `:725` 就是这样写的，ADR-069 的缓存已经按这个口径建好。
   漩涡还多一道：候选按 `icnIndex` 取，再按 **UID** 排除同一漩涡的其它格子（`world.cpp:778`）。
9. **缓存缺失时返回空**，而不是像参考那样 `_allTeleports.at()` 抛 `std::out_of_range`：只有在没走 `LoadMap`
   的世界里才会缺，那时「没有端点」比「进程崩溃」更接近可用行为。
10. **`NextTeleport` / `NextWhirlpool` 没有端点时返回入口本身**（`world.cpp:742/795`，参考打 WARN 日志），
    这样调用方不需要自己判空。

**后果**：新增 `src/FHeroes2.Core/Random/Rand.cs`（`CurrentThreadRandomDevice`、`Get`（`uint` 与 `int`）、
`GetWithSeed`、`GetWithGen`、`Get<T>(IReadOnlyList<T>)`、`Shuffle` / `ShuffleWithGen`、`Rand.Queue`）；
`World` 加 `GetTeleportEndPoints` / `NextTeleport` / `GetWhirlpoolEndPoints` / `NextWhirlpool`。
新增 `RandTests` 10 条（边界与反转、负数拒绝、种子可复现、`GetWithGen` 等价、列表选择与空表、洗牌、
`Queue` 的 0 权重/空池/权重分布、线程发生器稳定）与 `TeleportTests` 6 条（同 sprite 配对、水陆不配对、
多候选随机、英雄踩入口可用而踩目的地不行、非传送门瓦片、漩涡按 UID 排除自身其它格）。
`FHeroes2.Core.Tests` 182 → **192**，`FHeroes2.Game.Tests` 561 → **567**，全解决方案 **1168** 条全绿，
0 警告 0 错误。

**覆盖率**：`rand.cpp` 的 ✅ 现在名副其实——`Pcg32` + `Rand` 门面都在，只差 `combineSeedWithValueHash`；
`world.cpp` 仍「部分」（区域、事件、城堡表等都还没）。

**遗留**：`combineSeedWithValueHash`；`rand.h:167/175` 的枚举重载（等到真有枚举参数的调用点再补，避免凭空
造 API）。ADR-069 的「读取端等 `rand.cpp`」到此结束。

## ADR-071：英雄表 `AllHeroes`：种族、肖像与招募池

**背景**：ADR-068 给 `World` 挂了一个 `AddHero`，那是端口的替身——一个 `List<Hero>` 加线性查找，
只为让 `Tile.SetHero` / `GetHero` 有个可查的对象。参考里没有这个东西：英雄从一开始就全都在
`World::vec_heroes`（`world.h:555`，类型 `AllHeroes`）里，`AllHeroes::Init()`（`heroes.cpp:2265`）
在地图装载时把 73 个槽一次建好，槽的**下标就是 id**（`Get( heroId )` 是 `_heroes[heroId]`，
`heroes.cpp:2411`）。

装英雄到格子上（下一个切片）需要两件现在没有的东西：按 id 索引的容器，和一个「这个种族还有哪个英雄
没被雇」的池子（`AllHeroes::GetHeroForHire()`，`heroes.cpp:2322`）。后者的随机抽取上轮才解锁（ADR-070）。

**决策**：

1. **`AllHeroes` 取代 `_heroes` 列表**，`World.GetHero` 变成槽查找，`AddHero` 删除。这是本轮唯一的
   破坏性改动，四个用例文件里的 `new Hero(x)` + `AddHero(...)` 全部改成 `world.GetHero(x)!`。
   语义也更准：参考里「世界里有这个英雄」和「这个 id 的槽被填了」是同一件事。
2. **空槽用 `null`，不用 `Heroes( UNKNOWN, KNGT )` 占位**。参考放占位对象是因为 C++ 容器不能有空槽，
   而每个消费者第一句都是 `isValid()`——那正是 `null` 本身。这不影响 `Get( 61 )` 在无 PoL 的地图上拿到
   「不可用」，只影响「拿到的是空还是坏对象」这一层不再有意义。
3. **`AllHeroes.Init( MapVersion )` 与参考同构**：六族各九人（1-54）→ SW 战役六人（55-60）→
   PoL 十一人（61-71，仅 `.mx2`/`.hxc`/`.fh2m` 地图）→ 调试英雄（72）。`IS_DEVEL()` 的调试英雄在
   release 构建里不存在（`heroes.cpp:2312`），端口同样不建。战役与 PoL 英雄的 5000 点初始经验没搬：
   经验属于升级系统。
4. **种族表单独成 `HeroRaces`**：1-54 用 `getHeroIdRangeForRace()`（`heroes.cpp:38`）的九人一段，
   55-72 用 `Init()` 里逐个给出的种族。`GetRace( id )` 与 `GetHeroIdRange( race )` 在常规英雄上互为反函数，
   用例 `EveryHeroCarriesTheRaceOfTheTable` 遍历全表对拍这两个方向。`Race::NONE` 返回整个常规区间
   `{1, 54}`（参考的 `{ LORDKILBURN, CELIA }` 就是它的上下界），`Race::RAND` 落到同一个 `default` 分支——
   参考也这样，且没有任何调用方传它。
5. **`Hero( id, race )` 把种族变成构造参数**，而不是让对象自己去查表：`AllHeroes::Init()` 里种族本来就是
   一个个写死的实参，表放在 `HeroRaces` 一处、对象只持有结果，比两边各存一份表可靠。
   `Id = 1..72` / `IdCount = 73` / 战役与 PoL 的具名 id 都按参考枚举落成 `const`，
   `HeroRaces` 与 `AllHeroes` 因此没有魔数。
6. **`HeroModes` 只搬三个位**：`Recruit`(0x40)、`Jail`(0x80)、`Custom`(0x1000)。值必须与参考逐位一致
   （存档里就是这个字），但其余十几个位等设置它们的系统——先声明等于凭空造 API。
   `IsAvailableForHire` 就是「无主 + 不在牢里」（`heroes.cpp:1954`），参考多一条 `isValid()`，
   在端口里由「空槽是 `null`」代替。
7. **`GetHeroForHire` 三段填充照抄**：先按请求种族且避开自定义肖像，再退到任意种族（同样避开肖像），
   最后不限。`RACE_RAND` 与 `RACE_NONE` 走同一支（参考的 `switch` 里都落到第二段）。
   只有 id ≤ 54 的英雄能被雇（`heroes.cpp:2349`）——战役与 PoL 英雄永不上市。候选里优先挑没有
   `RECRUIT` 位的（别的王国没挂出来），全都挂出来时参考宁可共用也不返回空，端口一样。
   最后一步是 `Rand.Get( candidates )`。
8. **肖像与 id 分开存**：`PortraitId` 初值是 id，自定义肖像时改它；雇人时按**肖像**去重
   （`customHeroesPortraits`），因为两个英雄可以共用一张脸。肖像帧本身要等英雄渲染，这里只留 id。

**后果**：新增 `src/FHeroes2.Game/Heroes/` 下的 `AllHeroes.cs`、`HeroRaces.cs`、`HeroModes.cs`；
`Hero` 加 id 常量、`Race`、`Color`、`PortraitId`、`Modes`、`IsAvailableForHire`、`SetColor`、
`SetModes`、`ResetModes`、`SetPortraitId`，构造函数变成 `Hero( int id, MapRace race )`；
`World` 的 `_heroes` 换成 `AllHeroes`，`Heroes` 属性改返回它，`AddHero` 删除，新增
`GetHeroForHire( race, heroIdToIgnore )`（`World::GetHeroForHire` 的薄包装），`LoadMap` 里
`Reset()` 之后调 `_heroes.Init( map.Info.Version )`，`Reset()` 清空表。
新增 `AllHeroesTests` 13 条（SW 与 PoL 的槽数、id 表与种族表双向、遍历顺序、`Clear`、单种族与任意种族
的抽取范围、同种族雇完后的回退、牢里与已雇的排除、`heroIdToIgnore`、`RECRUIT` 优先级、
`IsAvailableForHire` 的三种状态），`HeroOccupancyTests` 里那条「同一 id 不能有两个英雄」换成
「一个槽一个对象」（`Assert.Same`）。`FHeroes2.Game.Tests` 567 → **580**，全解决方案 **1181** 条全绿，
0 警告 0 错误。

**覆盖率**：`heroes` 行 1/0/11 → **1/1/10**，合计 26/45/178 → **26/46/177**。`heroes.cpp` 转入「部分」：
`AllHeroes::Init` / `AllHeroes::Clear` / `GetHeroForHire` / `isAvailableForHire` / `getHeroIdRangeForRace`
都在，主体（移动、军队、技能、法术书、经验、AI 动作）没动。

**遗留**：地图装载时按信息块建英雄（`world_loadmap.cpp` 的 `MP2::OBJ_HERO` 分支）——它要先有
`Kingdom::AllowRecruitHero`、`Hero::LoadFromMP2` 的可用子集（军队、神器、技能）和 `MapRace.Random`
按王国种族落地；`Hero` 的军队、技能、法术、经验、移动点；英雄渲染与肖像帧。

## ADR-072：地图装载建英雄——`Mp2HeroInfo` 拆块、`Hero.LoadFromMP2` 子集与 `MINIHERO` 的退场

ADR-071 把英雄表建起来了，但地图上的英雄还是空的：`LoadMap` 只认识瓦片，不认识瓦片背后的 76 字节
信息块。参考的两半很清楚——**瓦片**给位置与 `MINIHERO` 精灵（蓝/骑士），**信息块**给其余一切，
两者用瓦片 `quantity` 字的 13 位值（= `Mp2InfoBlock.ObjectId`）挂钩（`world_loadmap.cpp:466`、
`:583-623`）。这一片把这条链打通。

1. **`Mp2HeroInfo`（Assets）只做拆字节**：76 字节块一个字段一个属性，偏移全部写成常量并注明参考行号。
   它保留**文件里的原始值**（怪物/神器/技能/肖像都从 0 数，引擎从 1 数），和 `Mp2TileInfo` 同一口径：
   `+ 1` 只在用它的地方做（`heroes.cpp:467/491/530`），这样与 `Mp2Map` 的对拍是逐字节的。
2. **块的归属按 `Metadata` 认**：遍历全部瓦片，找 `ObjectType` 是 `Hero`/`ActionHero` 且
   `Metadata == block.ObjectId` 的那一个。找不到就跳过——那块属于城堡、告示、瓶子、事件、牢房，
   这些对象端口还没读，**跳过不丢东西**（那些块的载荷仍原样留在 `Mp2Map.InfoBlocks` 里）。
   参考是先把 `objectTileId` 从瓦片算出来再 `switch`，端口反过来按块找瓦片，语义等价而对 `Mp2Map`
   的只读约束更友好。
3. **英雄的位置存 `MapIndex`，不存点坐标**：端口从 ADR-068 起就用瓦片下标说话（`Tile.Index`），
   参考的 `AllHeroes::Get( const fheroes2::Point & )`（`heroes.cpp:2404`）在端口里落成
   `AllHeroes.GetAtMapIndex( int )`，线性比较 `hero.MapIndex`——72 个槽，比换算坐标更不容易错。
4. **选人照抄参考的两条路**（`world_loadmap.cpp:600-610`）：信息块带自定义肖像且
   `PortraitId + 1 <= Jarkonas` 时按肖像取槽，但**该英雄必须还空着**（`IsAvailableForHire`，即无主且不在牢里）；
   否则从 `GetHeroForHire( sprite.Race )` 的池子里抽。第二条路不只是「没肖像时」用——参考的
   `if ( !hero || !hero->isAvailableForHire() )` 把「肖像点名的英雄已经站在地图上了」也接进池子。
   PoL 肖像（61-71）在非扩展地图上属于同一种情况：参考那个槽里是 `Heroes( UNKNOWN, KNGT )` 占位，
   `isValid()` 假 ⇒ 不可雇 ⇒ 抽池子；端口的空槽是 `null`，于是也落到池子里（**不是**留空，
   这一点和先前的 `Todo` 猜测相反）。
5. **`Hero.LoadFromMP2` 只搬「只依赖英雄自己」的部分**（`heroes.cpp:421-579`）：`modes = 0`、jail 位
   （现在恒 `false`）、`MapIndex`/`Color`、种族与 `raceType` 不符则 `Custom` + 改种族、自定义肖像
   （`+1`，越界回退 `Id` 并告警）、非空自定义名（`Custom`）、巡逻（`Patrol` 位、`PatrolCenterIndex = mapIndex`、
   `PatrolDistance`）。**未搬**：`HeroBase::LoadDefaults` 的主技能与初始法术、默认军队重置、自定义军队、
   三件神器、经验与按经验升级、自定义二级技能、法术点与移动点——这些要 `Army`/`Artifact`/`Skill`/`Spell`
   与升级表，属于各自系统。因此 `LoadFromMP2` 目前**不重置**军队与技能，调用方也不能指望它给出
   一个「干净」的英雄；这一点写进了方法注释。
6. **`HeroModes` 补 `Patrol = 0x0000_8000`**（`heroes.h:214`）。理由同 ADR-071：位值要与存档逐位一致，
   只搬现在真会设置的位。
7. **放置时删掉编辑器的 `MINIHERO` 精灵**：`world_loadmap.cpp:1583-1586` 在把英雄放到瓦片前
   `RemoveObjects( MINIHERO )`，因为地图文件画的是一个站着的「小英雄」图标，实际画的是英雄自己。
   端口在 `PlaceMapHero` 里先比 `MainObjectPart.IcnType == MiniHero` 再删，然后 `Tile.SetHero( world, hero )`
   （后者负责把当前对象类型寄存到英雄身上并写 `ActionHero`）。没有英雄的瓦片只告警、保留文件原样
   （参考是 `assert`），因为坏地图是正常能碰到的东西。
8. **`World.LoadMap` 分两步**：先 `CreateMapHeroes( map )` 建表与建人，再进瓦片循环 `FixMP2MapTileObjectType`
   + `PlaceMapHero`。参考也是先 `LoadMapMP2` 的块循环、后 `updateTileMetadata`，顺序不能反——
   `SetHero` 需要 `ObjectTypeUnderHero` 已经被 `FixMP2MapTileObjectType` 修正过。
9. **`isInJail` 恒传 `false`**：`MP2::OBJ_JAIL` 分支（`world_loadmap.cpp:528`）要把英雄挂到
   `PlayerColor::NONE` 并从块的第 60 字节读种族（那是牢房专用含义，不是巡逻距离），而牢房对象本身还没转，
   所以这条路径整体留空，参数先摆着。

**代价与缺口**：

* **没有 `Kingdom::AllowRecruitHero`**：参考在一国英雄达到上限时会 `DEBUG_LOG( "load heroes maximum" )`
  并**丢弃**这个英雄；端口没有王国，超出的英雄照样落地。地图装载因此只会多、不会少。
* **`RACE_RAND` 不落到王国种族**：参考在 `colorRace.second == RACE_RAND && color != NONE` 时取
  `kingdom.GetRace()`（`world_loadmap.cpp:593`），端口照旧按 `MapRace.Random` 处理，`LoadFromMP2` 会把
  英雄改判成 `Random` 种族并置 `Custom`。等王国落地再补。

**后果**：新增 `src/FHeroes2.Assets/Maps/Mp2HeroInfo.cs`；`Hero` 加 `NoPosition`、`MapIndex`、`Name`、
`PatrolCenterIndex`、`PatrolDistance`、`SetMapIndex`、`ClearModes`、`LoadFromMP2`（`Race` 改成私有 set）；
`HeroModes` 加 `Patrol`；`AllHeroes` 加 `GetAtMapIndex`；`World` 加 `CreateMapHeroes`/`PlaceMapHero`
并在 `LoadMap` 里接上。用例 `Mp2HeroInfoTests` 4 条（长度、全零、逐字段、名字终止符）+
`WorldHeroLoadTests` 10 条（落位与颜色/种族、`MINIHERO` 移除、肖像点名与改判种族、无肖像抽池子、
无块留空、坏块跳过、PoL 肖像抽池子、自定义名 + 巡逻、同一英雄只落一次、60 条英雄表）。
`FHeroes2.Assets.Tests` 264 → **268**、`FHeroes2.Game.Tests` 580 → **590**，全解决方案 **1195** 条全绿，
0 警告 0 错误。

**覆盖率**：**不变**（`heroes` 行 1/1/10、合计 26/46/177）。`heroes.cpp` 与 `world_loadmap.cpp` 早在
ADR-071/067 就记在「部分」里，这一片是把两边的缺口补薄，不新增「转」。

## ADR-073：牢房里的英雄——第 60 字节的种族、`PlayerColor::NONE` 与「瓦片不归他」

ADR-072 只建了地图上站着的英雄，牢房（`MP2::OBJ_JAIL`）这一支还是空的：否则一张带监狱的地图会把
里面的英雄整个丢掉。参考的这一支（`world_loadmap.cpp:528-582`）和普通英雄共用 `LoadFromMP2`，
差别全在参数上。

1. **信息块的归属改成分派**：`CreateMapHeroes` 拆成三段——`FindInfoBlockTile` 找块属于哪块瓦片、
   `CreateMapHero`（`MINIHERO` 精灵那支）、`CreateJailHero`（牢房那支），两者共用
   `SelectMapHero` 选人。ADR-072 里那段「先算 id 再 `_heroes.Get` 复查」随之消失：`SelectMapHero`
   直接返回 `Hero` 对象，池子和槽位给出的东西本来就在表里，第二次判空是多余的。
2. **种族来自第 60 字节**：这是同一个字节在两种块里的两种含义——巡逻英雄是巡逻半径，牢房英雄是种族
   （`world_loadmap.cpp:535`）。`Mp2HeroInfo` 早就把它命名成 `PatrolDistanceOrJailRace`，牢房这侧走
   `MapRaces.FromIndex`（0…7 → 六族 + `Multiple`/`Random`）。参考只 switch 0…5，其余走 `default` 并告警，
   所以 `None`/`Multiple`/`Random` 三种结果都按「不是可玩种族」处理：告警 + 当骑士。
3. **颜色是 `PlayerColor.None`、`isInJail = true`**：`LoadFromMP2` 因此置 `HeroModes.Jail`，并且
   `IsAvailableForHire`（无主 + 不在牢里）为假——这正是「肖像点名的英雄若已在地图上就换人」那条规则
   想要的语义。
4. **瓦片不归英雄**：参考只 `hero->LoadFromMP2( objectTileId, ... )`，没有 `setHero`，牢房对象留在瓦片上，
   英雄只是把 `index` 暂存在牢房的坐标等解救时用。端口照做：`Hero.MapIndex` 指向牢房瓦片，
   `Tile.OccupantHeroId` 保持 `UnknownId`，`PlaceMapHero` 因为只看 `Hero`/`ActionHero` 天然跳过。
5. **瓦片类型不做转换**：文件里存的是非 action 的 `OBJ_JAIL`（123），`MainObjectType` 保留它；
   访问时由 `Mp2ObjectTypes.BaseActionType` 给出 `ActionJail`（251）。端口不在这里「顺手修正」成
   action 值——`Tile.UpdatePassability` 依赖「缓存里是文件值、换算在使用处」这个约定。
6. **层的检查略过**：参考在 `(tile.getMainObjectPart().layerType & 0x3) == OBJECT_LAYER` 时才认这块瓦片，
   端口的 `Tile` 还没有把 layer 暴露给 `World`，而地面层对象带信息块的组合在真实地图里不存在，
   等 `Tile` 需要 layer 的消费者出现再补。

**后果**：`World.CreateMapHeroes` 拆成 `FindInfoBlockTile` / `CreateMapHero` / `CreateJailHero` /
`SelectMapHero` 四个方法；`WorldHeroLoadTests` 加 4 条（牢房英雄与 `PlayerColor.None`、第 60 字节改判
种族、非法种族回落到骑士、无肖像从池子抽、且牢房瓦片仍属于监狱而不是英雄）。
`FHeroes2.Game.Tests` 590 → **594**，全解决方案 **1199** 条全绿，0 警告 0 错误。

**覆盖率**：**不变**（`heroes` 1/1/10、合计 26/46/177）：`heroes.cpp` 与 `world_loadmap.cpp` 都是「部分」，
这一片补的是 `world_loadmap.cpp` 的缺口。剩下的是告示/瓶子/事件/狮身人面/每日事件/传闻六支，
它们要 `MapSign`/`MapEvent` 这些对象。

## ADR-074：告示与地面事件——`MapObject`/`MapObjects` 容器与事件类型的重算

ADR-073 之后，`world_loadmap.cpp` 的块循环还剩五支：城堡、狮身人面、告示/瓶子、地面事件、每日事件与
传闻。其中**告示/瓶子**与**地面事件**只读字符串和几个数值，不碰 `Castle`/`Army`/`Skill` 这些系统，
是能独立落地的一小片；它们也第一次给端口一个「地图对象容器」。

1. **`MapObject` 是参考 `MapBaseObject` 的替身**（`maps_objects.h:42`）：一个瓦片下标、一个 UID。
   参考 `setUIDAndIndex()` 把瓦片下标同时写进两者，端口也照做，于是 `Uid == (uint)TileIndex` 恒成立，
   将来接存档时两个字段都在。
2. **`MapObjects` 按瓦片下标做键**（`world.h:73`）：参考存 `unique_ptr` 并在 `add()` 里
   `try_emplace` + 赋值（同 UID 覆盖），端口存值、用 `Dictionary` 的索引器覆盖；`get()` 对应
   `get( uid )`，`Get<T>()` 是端口为「这个瓦片上的告示/事件」加的类型化查询。参考的
   `get( const fheroes2::Point & )` 返回列表，端口按 ADR-068 的口径直接收瓦片下标。
3. **`MapSign` 只有两个字段**（`maps_objects.h:145`）：消息，和从基类继承的位置。块结构是
   marker `0x01` + 8 个未用字节 + 从第 9 字节到块尾的 NUL 终止串；**块的长度就是字符串的长度**，
   所以没有越界问题（`maps_objects.cpp:286`）。文档要求最小 10 字节（`MP2_SIGN_STRUCTURE_MIN_SIZE`）。
4. **空消息留空，不填随机文案**：参考的 `setDefaultMessage()` 从本地化字符串里随机抽一句
   （`maps_objects.cpp:315`），端口既没有语言表也没有装载期的随机源，硬填一句反而会造出与参考不一致的
   行为，所以写成缺口而不是假实现。
5. **`MapEvent` 的字段与偏移**（`maps_objects.h:70`、`maps_objects.cpp:44`）：marker `0x01`、七个有符号
   32 位资源、一个 16 位神器、两个标志、10 个未用字节、六个颜色标志、第 49 字节起的消息。两处容易错：
   * 资源的**文件顺序**是 wood/mercury/ore/sulfur/crystal/gems/gold，而 `Funds` 的构造参数是 ore 打头，
     所以用具名参数逐个写清楚，不靠位置。
   * 神器在文件里从 0 数、在引擎里从 1 数，读出后 `+ 1`（`maps_objects.cpp:123`）。
6. **`ReadInfoBlocks` 取代 `CreateMapHeroes`**：方法名不再只讲英雄，因为它现在按 tile 的对象类型分派
   到 `CreateMapHero`/`CreateJailHero`/`CreateMapSign`/`CreateMapEvent`。块归属的匹配收进
   `HasReadableInfoBlock()`，认 `Hero`/`Jail`/`Sign`/`Bottle`/`Event` 的两种孪生值——文件里
   `MP2::OBJ_JAIL` 是非 action 值、`MP2::OBJ_EVENT` 是 action 值，两种都得接受。
7. **事件类型的分支补上**（`maps_tiles.cpp:1599-1611`）：`Tile.UpdateObjectType` 遇到
   `ActionEvent` 时，`world.GetMapEvent( Index )` 找不到数据就把类型清成 `None` 再返回，与参考
   「清空后 return」逐行一致；ADR-066 里留的那句「事件分支未移植」删掉。
8. **装载期不会触发它**：`LoadMap` 先 `FixMP2MapTileObjectType`、后 `PostLoad` 的
   `UpdatePassabilities`，而后者只重算**没有类型**的瓦片，所以事件块缺失的地图装载后仍留着事件类型——
   参考的 `World::UpdatePassabilities()` 也是同一个条件，这不是端口偷懒，而是参考行为。真正的清空发生在
   对象离开瓦片时的重算（`removeObjects`），所以测试显式调用 `UpdateObjectType` 来钉这条语义。

**后果**：新增 `Game/Maps/MapObject`、`MapSign`、`MapEvent`、`MapObjects`；`World` 加 `_mapObjects`、
`MapObjects` 属性、`GetMapSign`/`GetMapEvent`，`Reset()` 清空它，`CreateMapHeroes` 改名
`ReadInfoBlocks` 并加入四类分派，`CreateMapHero`/`CreateJailHero` 各自读块（不再由调用方解析）；
`Tile.UpdateObjectType` 补事件分支，`TileNeighbourPassabilityTests` 里那条「保留事件类型」的旧预期
改成「无数据时清空」。用例 `WorldMapObjectTests` 6 条（告示消息、空消息、非告示块跳过、事件资源/神器/
标志/颜色、有块时重算保留类型、无块时重算清空）。`FHeroes2.Game.Tests` 594 → **600**，全解决方案
**1205** 条全绿，0 警告 0 错误。

**覆盖率**：**不变**（合计 26/46/177）。`maps_objects.cpp` 早在 `map_object_info.cpp` 那一片就记在
「部分」里（当时只有对象表子集 `Mp2ObjectPreset`/`Mp2ObjectName`），这一片把 `MapSign`/`MapEvent` 的装载
也挂到它名下，仍是「部分」——`MapSphinx` 与拾取/信息框那几支没转。

**下一步**：狮身人面（`MapSphinx`，与事件同构但带 7 个答案）、每日事件与传闻（`World` 级列表）、
城堡表（要 `Castle` 系统）。

## ADR-075：狮身人面、每日事件与传闻——把「无瓦片的块」分流出去

ADR-074 只读了「瓦片指向的块」。块循环里还有一种块：**没有对象指向它**——每日事件与传闻
（`world_loadmap.cpp:656-684`）。它们和告示/事件挤在同一个 `InfoBlocks` 列表里，靠块自己的字节区分。
这一片一次做完剩下能独立落地的三支：狮身人面（有瓦片）、每日事件与传闻（无瓦片）。

1. **`MapSphinx`**（`maps_objects.h:99`、`maps_objects.cpp:166`）：marker `0x00`、七个 32 位资源、
   16 位神器、一个「正确答案个数」、八个 13 字节答案槽、第 136 字节起的问题。最小 137 字节。
   答案按参考存**小写**（`maps_objects.cpp:257`）。
2. **前四个字符决定对错**（`maps_objects.cpp:273`）：`isTruncatedAnswer` 默认 `true`，玩家输入与答案都取
   前 4 个字符比较——所以 `abram` 会答对 `abracadabra`。参考的注释说这是原版行为（编辑器把答案截到
   13 字节、1994 年的键盘也打不长），只有新版地图格式才关掉这个开关。
3. **`validate()` 用 `IsValid` 表达**（`maps_objects.h:117`）：没有奖励（无神器且资源全零）或没有问题/
   答案就是无效。参考在 `LoadMapMP2` 里调 `validate()` 再决定丢不丢；端口让 `MapSphinx` 自己算这个
   标志、由 `World.CreateMapSphinx` 决定丢弃——判据一样，位置不同，因为「丢弃」属于装载流程。
4. **「无神器」在 MP2 里没有干净表示**：参考是 `artifact = getLE16() + 1`，所以 0 会变成**第一个神器**，
   想表示「没有」只能写 `0xFFFF`，加一后是个越界值。参考不校验它，端口照做（`ArtifactType` 是枚举，
   越界值照样能存能比）；`validate()` 只看 `artifact == Artifact::UNKNOWN`（0），因此「无神器且无资源」
   才能判成无效。这是参考的坑，不是端口的。
5. **每日事件 `EventDate`**（`world.h:171`、`world.cpp:1517`）：marker `0x00`、七个**可负**的 32 位资源、
   16 位 AI 标志、16 位首日、16 位周期、六个字节的占位（最后一个恒为 `1`）、六个颜色字节、第 49 字节起的
   消息。`IsAllowed`/`IsDeprecated` 一起搬（`world.h:177`、`world.cpp:1647`），虽然日期系统还没有——
   两个方法只依赖参数，先钉住语义。
6. **传闻只是一根字符串**：marker `0x00`、七个未用字节、第 8 字节起的文本，最小 9 字节
   （`MP2::MP2_RUMOR_STRUCTURE_MIN_SIZE`）。参考把它们放进 `_customRumors`，端口放 `Rumors`。
7. **靠 `[42] == 1` 分辨事件与传闻**（`world_loadmap.cpp:659`）：两者都以 `0x00` 开头，事件块至少 50 字节
   且第 42 字节恒为 `1`，传闻没有这个常量。判别顺序必须是「先事件、后传闻」。
8. **`FindInfoBlockTile` 的匹配集合扩到「有块的对象」**：新增 `Sphinx`/`ActionSphinx` 与城堡四种
   （`Castle`/`ActionCastle`/`RandomCastle`/`ActionRandomCastle`/`RandomTown`/`ActionRandomTown`），
   方法从 `HasReadableInfoBlock` 改名 `HasInfoBlock`。**这不是洁癖**：城堡块至少 70 字节、也以 `0x00`
   开头，若找不到瓦片就会被当成传闻读出一串垃圾；参考靠「城堡块一定能匹配到瓦片」避开，端口得靠同一个
   匹配。匹配到的城堡块在分派里落到 `default` 被忽略，等 `Castle` 系统。
9. **`ReadWorldBlock` 只处理「没找到瓦片」的块**：顺序与参考一致——先找瓦片，找到就走对象分派，找不到才看
   是不是事件/传闻，两者都不是就告警（参考在 `else` 里报 "unrecognized block id"）。

**后果**：新增 `Game/Maps/MapSphinx`、`Game/World/EventDate`；`World` 加 `_dailyEvents`/`_rumors`、
`DailyEvents`/`Rumors`/`GetMapSphinx`/`AddEventDate`，`Reset()` 清空两个列表，`ReadInfoBlocks` 加
「空块跳过」「无瓦片分流」与 sphinx 分派，`FindInfoBlockTile` 的匹配集合扩到城堡。用例
`WorldSphinxAndEventsTests` 7 条（狮身人面奖励/答案/前四字符/无问题丢弃、事件日期与颜色、单次事件、
传闻文本、城堡块不被当成事件）。`FHeroes2.Game.Tests` 600 → **607**，全解决方案 **1212** 条全绿，
0 警告 0 错误。

**覆盖率**：**不变**（合计 26/46/177）。`maps_objects.cpp` 与 `world_loadmap.cpp` 都是「部分」：
狮身人面的**装载**转了，但拾取/问答题的信息框（`dialog` 那一侧）没转；`world_loadmap.cpp` 现在只差
城堡表、俘获对象与 AI 区域。

**下一步**：城堡表（要 `Castle` 系统，最大的一块）、俘获对象（`World::CaptureObject`）、AI 区域。

## ADR-076：日期系统——三个计数器、派生的一周/一月、`NewDay` 先只走时钟与事件剪枝

**背景**：ADR-065 立 `World` 骨架时把日期整族（`_day`/`_week`/`_month` 与 `NewDay`/`NewWeek`/`NewMonth`）
留了下来，理由是「吃王国与事件」。事件容器在 ADR-075 落地后，`NewDay` 里与日期有关的两件事
（计数器的嵌套递增、每日事件的过期剪枝，`world.cpp:425-460`）已经可以搬；剩下的
`NewWeek`（周生命对象刷新、中立怪物重掷、招募重置）与 `NewMonth`（怪物之月）仍然要
`Week` 类型、`Kingdoms`、`Castles`、`Heroes` 与怪物生成器。

**决策**：

1. **三个计数器留在 `World` 上，`uint`，由 `Reset()` 归零**：`_day`/`_week`/`_month` 是「时钟」而不是
   「会话状态」，参考也在 `Reset()` 里把它们归零（`world.cpp:328-330`）。新世界是 `0/0/0`，只有
   `NewDay()` 会推动它们；装载器不碰（MP2 文件里没有日期）。
2. **无参访问器用属性，布尔值加 `Is` 前缀**：`Day`/`Week`/`Month`/`DayCount`/`WeekCount`、
   `IsBeginWeek`/`IsBeginMonth`/`IsLastDay`/`IsFirstWeek`/`IsLastWeek`。这是与参考唯一的**形式**偏差
   （`GetDay()`/`BeginWeek()` 改属性名），语义逐字照抄，包括那个反直觉的怪癖：`LastDay()` 是裸取模，
   **0 也是「最后一天」**，于是新世界 `Day == 7`、`Week == 4`，而 `IsBeginWeek` 为假。`WorldDateTests`
   第一条就把它钉住——照直觉把 0 当第一天会让整个周循环错一天，而且错得很安静。
3. **`DateString()` 逐字照抄**：`"month: X, week: Y, day: Z"`（`world.cpp:888`），不本地化。原版的
   状态栏就用这个写法，翻译属于界面层，不属于日期。
4. **`NewDay()` 只搬计数与剪枝**：三层嵌套照参考写死（`++_day`；`IsBeginWeek` 时 `++_week`，其中
   `IsBeginMonth` 时再 `++_month`）；末尾用 `_day - 1` 剪掉过期事件（`world.cpp:459`）。
   `NewMonth`/`NewWeek` 与 `vec_kingdoms/vec_castles/vec_heroes` 的 `NewDay`/`NewWeek` 只在注释里
   按参考顺序标出插入点，不写空方法：空方法会让调用方以为「已经做过了」，而这一步的顺序
   （月 → 周 → 日 → 剪枝）是唯一容易写错、以后最难改的部分，先固定下来。
5. **剪枝用「刚过去的那一天」**：所以单次事件在触发后的第二天仍在表里（它还能被界面读到），第三天
   才消失。这是参考的行为，不是差一错误；`AnEventIsDroppedOneDayAfterItsOnlyOccurrence` 守住它。
6. **不搬 `GetWeekType()`**：它要 `Week::RandomWeek`（周类型表）、`GetWeekSeed()`（种子 + `Settings`）
   与静态缓存三件套；跟随 `Week` 与 `Settings` 一起做，`NewMonth` 同理。

**后果**：`World` 加三个字段与十一个成员；`Reset()` 归零；新增 `WorldDateTests` 9 条
（新世界 `0/0/0` 与 `Day == 7` 的怪癖、第一天开周开月、第 7 天收周、第 4 周收月、第 29 天开第二月、
`DateString`、`Reset` 回零、单次事件晚一天剪掉、周期事件永不剪）。`FHeroes2.Game.Tests` 607 → **618**，
全解决方案 1212 → **1270** 条全绿，0 警告 0 错误。

**覆盖率**：**不变**（合计 26/46/177）。`world.cpp` 早已是「部分」，这一片补的是它的日期缺口；
剩下的是 `NewWeek`/`NewMonth`、俘获对象、`CountObeliskOnMaps` 之外的静态分析与区域图。

## ADR-077：`mp2.cpp` 的周生命/战斗生命分类与方尖碑计数

**背景**：`mp2.cpp` 在 `PLAN.md` 里记的是 ✅（`Mp2Map`/`Mp2TileInfo`/`Mp2AddonInfo` 的解析全在），但
它的两张对象分类表 `isWeekLife` / `isBattleLife`（`mp2.cpp:511-576`）一直没搬。`isWeekLife` 是
`World::NewWeek()` 的判据（刷新周产建筑、怪物巢穴），也是 AI 判断「英雄移动力什么时候回来」的判据
（`ai_planner_hero.cpp:823`）；`isBattleLife` 是「幸运/士气加成只持续到下一场战斗」的判据。
同一轮把 `World::CountObeliskOnMaps()`（`world.cpp:900`）一起做掉：方尖碑谜题要它，而它只数瓦片，
不欠任何系统。

**决策**：

1. **两张表逐字照抄到 `Mp2ObjectTypes`**（`IsWeekLife` / `IsBattleLife`），包括两个容易「顺手删掉」的条目：
   `Hero` 在周生命表里（参考注释写着 *for AI*），`BlackCat` 在战斗生命表里（它同时改幸运与士气）。
2. **比较的是非 action 值**：参考调用处传的是 `tile.getMainObjectType( false )`，所以 `Action*` 值落到
   `default` 返回 `false`。端口不改这个语义，用例 `ActionVariantsAreNotWeekLife` 钉住——这里若「顺手」
   用 `BaseActionType` 归一化，`NewWeek` 就会把巢穴之外的 action 对象也当成周产建筑。
3. **两表不相交**：用全枚举遍历断言守住。将来加对象时若两边都塞（或复制粘贴串行），测试立刻红，
   而不是等到 `NewWeek` 把某个对象的产量翻倍才发现。
4. **`CountObeliskOnMaps()` 用 `GetMainObjectType(this, false)`，答案下限 6**：参考
   `res > 0 ? res : 6` 的语义是「谜题永远能拼完」，数量少于 6 的地图按 6 算；英雄站在方尖碑上
   仍然计数（那是参考的选择，也是唯一说得通的选择）。

**后果**：`Mp2ObjectTypes` 加两个谓词，`World` 加 `CountObeliskOnMaps()`。新增
`Mp2ObjectTypesTests`（Assets，47 条：26 条周生命、英雄特例、13 条战斗生命、5 条「都不是」、
action 变体为假、两表不相交）与 `WorldTests` 2 条（无方尖碑报 6、按瓦片计数）。
`FHeroes2.Assets.Tests` 268 → **315**，`FHeroes2.Game.Tests` 607 → **618**（含 ADR-076 的 9 条），
全解决方案 **1270** 条全绿，0 警告 0 错误。

**覆盖率**：**不变**（合计 26/46/177）。`mp2.cpp` 本来就是 ✅，这一片让那个 ✅ 名副其实一点；
`world.cpp` 仍是「部分」。

## ADR-078：俘获对象——按瓦片索引的 `（对象类型, 颜色）` 表，不含守卫军队与旗帜精灵

**背景**：`World::CaptureObject` / `map_captureobj` 是「谁拥有这座矿、这座民居、这个方尖碑」的唯一
来源，也是王国日收入的输入；`world_loadmap.cpp` 剩下的三条尾巴之一就是它。参考的结构是
`std::map<int, CapturedObject>`，条目里除了 `(objCol)` 还有 `Troop guardians`（易主后要重新守军）。

**决策**：

1. **只搬 `objCol`**：`CapturedObject` 目前只有对象类型与颜色。`guardians` 是 `Troop`，属军队系统，
   而 `CapturedObjects::Set()` 里与它有关的唯一动作是「易主时 `guardians.Reset()`」——一段只能清空
   本类还不持有的状态的代码，等 `Troop` 落地时补，不会造成数据分叉。
2. **`Get` 插入、`GetColor` 不插入**：参考两者都用 `operator[]`，但只有 `Get` 会留下条目。C# 侧
   照着分成 `Get`（插入）与 `GetColor`（只读），并用 `World.HasCapturedObject` 暴露「键是否存在」
   ——「条目存在但无主」与「条目不存在」在参考里是两种状态（前者是刚被清空的矿，后者是从未易主）。
3. **颜色用 `PlayerColor` 位集**，但 `CaptureObject` 断言只允许一位：参考的 `PlayerColor` 本来就是
   单值，端口用位集表示是为了雾/联盟，俘获对象只能是单色。
4. **`ResetColor` 只改数据**：参考还会调 `Tile::setOwnershipFlag` 重画旗帜精灵（FLAG32 的对象部件），
   那是渲染侧，等旗帜精灵移植时再挂上。对象类型保留，因此条目仍可被计数——这正是参考行为。
5. **`CountCapturedMines` 的两条特殊路径**照抄：木材走 `OBJ_SAWMILL`、水银走 `OBJ_ALCHEMIST_LAB`
   （它们不是 `OBJ_MINE`，但逻辑上是矿），其余资源才读矿的 `metadata[0]`。这个不对称是参考的形状，
   不是端口随手加的。
6. **`CaptureObject` 不处理城堡**：参考里城堡分支要 `getCastleEntrance` + `Castle::ChangeColor`，
   城堡系统未转；注释里写明这个缺口，而不是悄悄只做矿井。

**后果**：新增 `CapturedObject` / `CapturedObjects`；`World` 加 `_capturedObjects`、`CaptureObject`、
`ColorCapturedObject`、`GetCapturedObject`、`HasCapturedObject`、`CapturedObjectCount`、
`CountCapturedObject`、`CountCapturedMines`、`ResetCapturedObjects`，`Reset()` 清空。新增
`CapturedObjectTests` 9 条。`FHeroes2.Game.Tests` 618 → **647**（含 ADR-079/080）。

## ADR-079：迷雾位——每瓦片一组颜色位 + 一个绘制方向缓存

**背景**：迷雾不是世界级状态而是瓦片级状态：同一格对蓝方已探明、对红方仍是黑的，联盟共享。
参考把它放在 `Tile` 上（`_fogColors`：`PlayerColorsSet`，默认全部玩家；`_fogDirection`：`DIRECTION_ALL`）。

**决策**：

1. **`_fogColors` 默认是所有玩家颜色**：新瓦片对所有人都是黑的，探明是减法（`ClearFog` 清位），
   不是加法。这样「忘记初始化」的表现是「看不见」，而不是「全图可见」——失败方向是安全的。
2. **`IsFog` 与 `IsFogForAll` 分开**：参考有两个同名重载（单色：任一在雾中；
   集合：全部在雾中），C# 的两个参数类型是同一个位集，无法重载。单色调用两者等价；集合调用
   （联盟）必须用 `IsFogForAll`，语义是「联盟里还有一个人没看见」。
   `IsFog` 对 `PlayerColor.None` 返回 `false`，`IsFogForAll(None)` 返回 `true`——与参考的
   `(fog & 0) != 0` / `== 0` 一致，边界值不好看但没改。
3. **`ClearFog` 不重置路径查找器**：参考在这里调 `world.resetPathfinder()`（因为视野可能在没有移动
   的情况下变大：宝箱给侦察术、马格拉斯地图）。端口的 `World.ResetPathfinder` 还是占位（ADR-065），
   所以注释里指出调用点属于世界侧，留给「揭示迷雾」的调用方，避免 Tile 反向依赖 World。
4. **`FogDirection` 是渲染缓存，`SetFogDirection` 标注「不要直接调」**：参考的注释就是这么写的，
   值由 `updateFogDirectionsInArea` 重算；端口把它放进 `Tile` 但保留「只有区域揭示能写」的警告。
   默认 `Directions.All`，`Direction.Center` 表示「完全没有雾」。
5. **不搬 `updateFogDirectionsInArea`**：它要索引缓存与方向表，是迷雾渲染的一半，跟
   `Maps::ClearFog`（半径揭示、城堡视野、英雄侦察）一起做才有意义。

**后果**：`Tile` 加两个字段与 `FogColors`/`IsFog`/`IsFogForAll`/`ClearFog`/`FogDirection`/
`SetFogDirection`；新增 `TileFogTests` 5 条。

## ADR-080：瓦片元数据的读与写——`Funds`、神器 id、障碍颜色、贵重判定

**背景**：`maps_tiles_helper.cpp` 的 `getFundsFromTile` / `getArtifactFromTile` /
`getBarrierColorFromTile` / `doesTileContainValuableItems` / `setResourceOnTile` /
`setBarrierColorOnTile` / `resetObjectMetadata` 是拾取、访问与 AI 判断「值不值得绕路」的全部输入。
它们只读 `metadata` 三个 lane 与对象类型，不依赖任何未移植的系统——除了 `Spell` 与 `Artifact` 类。

**决策**：

1. **放进 `Tile`（`partial` 文件 `TileMetadata.cs`），不做 `Maps` 静态类**：参考把自由函数放在
   `Maps` 命名空间是因为 `Tile` 不该知道 `Funds`/`Artifact`；端口的 `Tile` 已经在 `FHeroes2.Game.Maps`
   里，再开一个静态类只会得到「第一个参数永远是瓦片」的函数群。与 ADR-070 的
   `GetDailyIncomeObjectResources` 保持同一形状。
2. **神器以 `ArtifactType` 返回，卷轴的「所附法术」暂时丢掉**：`Artifact` 类未转，卷轴的法术在
   `metadata[1]`。判断 `metadata[2] == ContainsSpell` 后返回 `ArtifactType.SpellScroll`——
   比返回 `metadata[0]`（卷轴 id）更接近参考，也比「读出 `Spell` 的一半」诚实。注释写明缺口。
3. **`ArtifactCaptureCondition` 整枚举搬来（`ContainsSpell` 是 15，没有 14）**：它是地图数据的一部分，
   编号不能重排；名字用 `Fight1Genie` 等前缀统一成 `Fight<N><Monster>`。
4. **`Wagon` 的 lane 1 是资源类型、lane 2 是数量**：这一点最容易误读成「两个金币车道」；
   `AWagonKeepsTheResourceTypeAndItsAmountInTheLastTwoLanes` 钉住它。测试第一版就写错了，
   是测试先红才发现注释也在骗人。
5. **`DoesTileContainValuableItems` 的 `Pyramid` 分支用 `metadata[0] != 0`** 代替参考的
   `getSpellFromTile().isValid()`：等价（`Spell::NONE == 0`），且不引入 `Spell` 依赖。
6. **保留参考的 `assert`**（`Debug.Assert`）：这些读取器只该被「已经知道对象类型」的调用方调用；
   对空瓦片读金币是程序错误，不是运行时分支。

**后果**：新增 `Game/Maps/TileMetadata.cs` 与 `Game/Maps/ArtifactCaptureCondition.cs`；`Tile` 变
`partial`；新增 `TileMetadataTests` 15 条。`FHeroes2.Game.Tests` 618 → **647**，
全解决方案 1270 → **1299** 条全绿，0 警告 0 错误。

**覆盖率**：**不变**（合计 26/46/177）：`maps_tiles_helper.cpp` 早已是「部分」，`world.cpp` 仍是「部分」。

## ADR-081：`mp2.cpp` 的对象分类谓词——十一个判据一次搬完

**背景**：`mp2.cpp` 在 `PLAN.md` 里记的是 ✅，ADR-077 补了 `isWeekLife`/`isBattleLife` 两张刷新表，但同一个文件
里还有十一个分类谓词（`mp2.cpp:505-813`）：`isDayLife`、`isInGameActionObject`（两个重载）、
`isWaterActionObject`、`isRevisitAllowedForObject`、`isValuableResourceObject`、`isCaptureObject`、
`isPickupObject`、`isArtifactObject`、`isBattleMandatoryifObjectIsProtected`、`isSafeForFogDiscoveryObject`、
`isNeedStayFront`。它们是三类调用方的唯一判据——AI（绕路值不值、探索迷雾时踩不踩）、访问（能不能交互、
能不能再访问一次）、拾取（拿走什么）——且都是纯 switch，不欠任何未移植的系统。

**决策**：

1. **新 `partial` 文件 `Mp2ObjectTypes.Classification.cs`**：`Mp2ObjectType.cs` 的静态类改 `partial`，ADR-077
   的两张刷新表留在原处（它们是为 `NewWeek` 引入的，那里的注释仍然成立）。
2. **一律按 action 值比较**：地图里存的是 action 值，`Tile.MainObjectType` 也是 action 值（`BaseActionType`
   的换算在使用处）。这里若「顺手」用基类型比较，`NewWeek`、访问与 AI 会同时按两套 id 判断。
3. **Resurrection 扩展对象是唯一例外**：`OBJ_SWAMPY_LAKE`…`OBJ_BARREL`（257-260）本身已经带了 128 位，所以
   参考的 `OBJ_BARREL` 在端口叫 `Mp2ObjectType.Barrel`，而不是那个并不存在的 `ActionBarrel`（基名
   `Barrel` 在这四个对象上等于 action 值）。
4. **`isInGameActionObject` 是两道门**：带 action 位 **且** 基类型有 action 孪生（`CanBeActionObject`）。
   第二道门是编辑器写了地形装饰的 action 值（树、山、湖）却不算可访问的原因；`OBJ_EVENT` 是唯一带位
   但不可访问的例外，照抄。
5. **`isWaterActionObject` 的 `default` 回落到 `isInGameActionObject`**，不是 `false`：海岸与水上的对象在
   表里，`OBJ_BOAT`/`OBJ_CASTLE` 显式 `false`，其余按陆地判据判——因为原版编辑器允许把任意对象放到水上，
   游戏得能应付。测试第一版断言「水中的矿不可访问」就是错在这条回落上。
6. **`isRevisitAllowedForObject` 与 `accessedFromWater` 无关地排除漩涡**：两条路都是 `false`。参考的调用处
   用它区分「已访问」提示，所以一个对象完全可以是 action 对象却拒绝第二次访问。
7. **三条兜底分支照抄，不当抽象**：`isValuableResourceObject` 兜 `isPickupObject`、
   `isBattleMandatoryifObjectIsProtected` 兜 `isCaptureObject`、`isNeedStayFront` 兜 `isPickupObject`、
   `isSafeForFogDiscoveryObject` 兜 `!isInGameActionObject`。它们在参考里就是复用，抽成「统一分类器」
   会让「为什么矿要打而箱子不必」这类规则失去出处。
8. **保留参考的拼写 `isBattleMandatoryifObjectIsProtected`**（`if` 少一个大写）：参考的注释里多处引用这个
   名字，改了就没法 grep 回原文。

**后果**：新增 `src/FHeroes2.Assets/Maps/Mp2ObjectTypes.Classification.cs`；新增
`Mp2ObjectTypesClassificationTests` 34 条（`[Theory]` 展开后），钉住全部易错边界：`OBJ_EVENT` 带 action 位
却不可访问、地形装饰的 action 孪生不可访问、海岸在水列表却不在 in-game 列表、水中的矿从水上可访问、
漩涡两边都不可重访、`isDayLife` 只有魔法井。

**覆盖率**：**不变**（合计 26/46/177）。`mp2.cpp` 本来就是 ✅，这一片让它名副其实：加上 ADR-077 的两张表，
`mp2.cpp` 的对象分类部分至此全数落地。

## ADR-082：`Ground` 的移动代价与地形图像边界

**背景**：`ground.h:72-76` 的五个代价常量、`ground.cpp:96-141` 的两个图像判据、`ground.cpp:169-242` 的
`GetPenalty`。代价是英雄移动点数记账的乘数（每格按百分之一点数计，直走乘 100、斜走乘 141 再除一百），
图像边界是编辑器拒绝过渡图、装载器决定重推瓦片、`Tile.IsSuitableForSummoningBoat` 判断水面的输入。

**决策**：

1. **放进 `MapGrounds` 的 `partial` 文件（`MapGrounds.Penalty.cs`）**：`MapGround` 是枚举，`MapGrounds` 是
   它的静态面，`StartImageIndex` / `FromTerrainImageIndex` 早已在那里；另立 `Maps::Ground` 的类只会得到
   第二个放同样九个常量的地方。
2. **`GetPenalty` 收 `(MapGround, SkillLevel)` 而不是 `Tile`**：参考只读 `tile.GetGround()`，而端口的
   `Ground` 与 `Tile` 分居两个命名空间，收 `Tile` 会把 `AdventureMap` 反向绑到 `Maps` 上。
3. **照抄「默认值 + 加成」的形状，不写查表**：参考注释里那十行表就是游戏数据，switch 与它并排才读得出
   「沙漠无寻路 2.00」对应 `100 + 100`；写成二维数组反而把边界（`Wasteland`/`Beach` 只区分「有没有寻路
   技能」）藏起来。
4. **新增 `SkillLevel` 枚举**（`None`/`Basic`/`Advanced`/`Expert`）：参考的参数是 `uint32_t pathfindingLevel`，
   裸 `3` 是魔法数；值域与 `Skill::Level` 一致，`Skill` 落地时直接换类型。
5. **`IsTerrainTransitionImage` 的两档与 0 档照抄**：水与土 16 张、草/雪/沼/熔岩/沙漠/荒原 38 张、沙滩 0 张。
   `ContainsEmbeddedObjects` 用的是 `>` 而不是 `>=`，所以恰好 +45 的那张仍是纯地面——45/46、23/24、7/8
   三组边界都进了用例，这类差一最容易在「顺手统一成区间」时丢掉。
6. **`RoadPenalty = 75`、`DefaultGroundPenalty = 100`、`FastestMovePenalty = RoadPenalty`、
   `SlowestMovePenalty = 200`**：`FastestMovePenalty` 在参考里就定义为 `roadPenalty`，不是独立数字；它与
   `SlowestMovePenalty` 一起决定英雄剩余移动点存进存档时用几个字节。
7. **道路不进 `GetPenalty`**：它是瓦片属性（`Tile.IsRoad`），路径查找器在离开道路格时用 `RoadPenalty`
   覆盖地面代价；坐在船上的英雄对水面付 `DefaultGroundPenalty`，所以水那一行就是普通行。

**后果**：新增 `src/FHeroes2.Game/AdventureMap/MapGrounds.Penalty.cs` 与 `Game/Heroes/SkillLevel.cs`；
`MapGround.cs` 的静态类改 `partial`；新增 `MapGroundPenaltyTests` 8 条（九行代价表逐格、未知地形回落默认、
四个常量与「最慢格恰等于 200」、水/土/草三档过渡边界、沙滩无过渡、三组装饰边界）。

## ADR-083：对象感知通行——`isPassableFrom` 的四步顺序与 `Hero.IsFriends`

**背景**：`maps_tiles.cpp:1007-1147` 的三个 `isSuitableFor*` 与 `isPassableFrom( direction, fromWater,
ignoreFog, heroColor )`。ADR-063 的 `PassableDirections` 只回答「这一步在掩码上过不过得去」，装载器与
编辑器够用；路径查找器问的是另一件事：**某个英雄**从某个方向、是否坐船、是否忽略迷雾，能不能进这一格。
两者不能互相代替。

**决策**：

1. **新 `partial` 文件 `TilePassability.cs`，四个成员**：`IsSuitableForDisembarkation`、
   `IsSuitableForSummoningBoat`、`IsSuitableForUltimateArtifact(World)`、`IsPassableFrom(World, ...)`。
   `IsPassabilityTransparent` 已在 `Tile.cs`（ADR-063），第一版重复定义过，删掉。
2. **顺序照抄：雾 → 上岸/上船 → hero 友军 → 通行掩码。** 掩码放最后是因为前三条是掩码表达不了的**对象**
   语义：停着船的水面、只能从海上进的海岸、站着友军英雄的格子。
3. **海岸是最特殊的例子**：`ActionCoast` 直接 `true`，不看对象（参考信任精灵而不是对象）。于是同一个海岸格
   「从水上进」进得去，「从陆地进」看掩码——这正是「通行掩码」与「对象感知通行」不能合一的证明。
4. **影子不挡路**，三个判定都走 `TileObjectTraits.IsObjectPartShadow`。`ObjnArti`/`ObjnRsrc` 的
   `icnIndex % 2 == 0` 才是影子、`1` 是本体；测试第一版拿 0 当「石头」，结果自然是「影子当然不挡」。
5. **`IsSuitableForSummoningBoat` 不看对象类型**：只看地形与部件，所以已经停着船的格子仍然「适合召船」，
   两条船靠 `IsPassableFrom` 的 `ActionBoat` 分支区分。测试第一版断言 `false`，是测试错、参考对。
6. **`IsSuitableForUltimateArtifact` 收一个 `World`**：参考的同名函数不带参数，但它内部调 `isPassableFrom`，
   而端口的 `isPassableFrom` 要找瓦片上的英雄，于是 `World` 从签名上浮（与 ADR-069 的 `World` 实例方法
   同一形状）。
7. **两处缺口各自只留一个落点**：同盟城堡入口（要 `Castle` 与 `world.getCastleEntrance`）在
   `IsPassableFrom` 里正好放在参考检测它的位置；`Hero.IsFriends` 的联盟语义落在 `Hero` 一处（目前等价于
   「同色」），而不是散进通行判断——所有调用方都是通行或战斗决策，联盟语义写错是真 bug，缺口必须只有一个。
8. **`Debug.Assert(hero != null)` 保留**：`ActionHero` 的瓦片上没有英雄是数据损坏，不是运行时分支。

**后果**：新增 `TilePassability.cs` 与 `Hero.IsFriends`（`heroes.h:437`）；`Tile` 现有两个 `IsPassableFrom`
重载并存——一参数版本问掩码，五参数版本问「这个英雄能不能进」。新增 `TileObjectPassabilityTests` 12 条：
陆地/水面、船占位、海岸必可上岸、英雄格与事件格、石头挡上岸、影子与道路不挡、雾按玩家、
终极神器只在干燥空地。

**过程记录**：这一片的第一版把既有的 `tests/FHeroes2.Game.Tests/TilePassabilityTests.cs`（ADR-064 的独立通行
14 条）整个覆盖成了新用例。跑全量时 `FHeroes2.Game.Tests` 只从 647 涨到 652 才暴露出来：数字对不上。已用
`git checkout --` 复原那个文件，新用例另立 `TileObjectPassabilityTests.cs`。教训：新增用例先确认同名文件
不存在，别让「文件名合适」变成覆盖。

## ADR-084：`maps.cpp` 的扫描与守卫——「周围有什么」的两个答案

**背景**：`maps.cpp:56-182`（`MapsIndexesFilteredObject`/`forEachMonsterProtectingTile`）、`:184`
（`ComparisonDistance`）、`:570-689`（`ScanAroundObject` ×2、`hasNearbyObject`、`isValidForDimensionDoor`、
`ScanAroundObjectWithDistance`、`GetObjectPositions( center, … )`、`isTileUnderProtection`、
`getMonstersProtectingTile`）。法术（次元门、看英雄）、AI 与地图动作都问同一个问题——「这格周围是什么」——
但答案有两类：**扫描**按对象类型找格子，**守卫**回答「动了这格会有哪些游荡怪物扑上来」。

**决策**：

1. **两块都放 `World` 的 `partial` 文件 `WorldMapScan.cs`**：两者读的都是 `Tile.GetMainObjectType` 与
   `Tile.IsPassableFrom`，都按瓦片索引对整张图说话，与 ADR-069 的 `GetObjectPositions` 同一形状。
   `World` 因此改 `sealed partial class`。
2. **`ActionMonster`，不是 `Monster`**。参考比较的是 `OBJ_MONSTER`——带 128 位的那一个；基类型 `Monster`
   永远到不了瓦片上。第一版写成 `Mp2ObjectType.Monster`，守卫一条也找不到，测试第一轮就红了。这条陷阱对
   所有 `Action*` 对都成立，已写进文件头的注解。
3. **`ScanAroundObject` 只合并两个重载成一个默认参数**：参考的两个重载只差 `ignoreHeroes` 的默认值，
   在 C# 里就是默认参数。半径固定为 1，因为参考给更大的范围另有一个函数。
4. **`ignoreHeroes` 的语义照抄参考的表达式 `getMainObjectType( !ignoreHeroes )`**，不重新起名。这件事在
   ADR-069 已经踩过一次：`ignoreHeroes == true` 是「看穿英雄、读缓存里的对象」，`false` 才是「英雄自己」。
5. **`isValidForDimensionDoor` 三个条件一个不少**：可进入（自己的 `Center` 位）、水陆同侧、不是
   **in-game** action 对象。第三条问的是 `isInGameActionObject` 而不是「空不空」，所以道路、影子、纯装饰
   都不妨碍传送——这正是 ADR-081 的分类谓词存在的意义。对象类型按参考用 `getMainObjectType( true )`
   读，也就是看穿站在目标格上的英雄。
6. **排序用「先排后筛」**：参考先按平方距离排序再过滤，端口照做。`std::sort` 与 `List.Sort` 都不稳定，
   距离相同的格子顺序未定义——不写进契约，测试也不依赖。
7. **守卫顺序按参考的八邻域手工展开**，不用 `GetAroundIndexes`：后者给的瓦片集合相同但顺序不同，而
   「先报告哪只怪物」对上层（战斗、AI 估算）是有意义的。
8. **两个入口的 `IsNeedStayFront` 早退不一样**：`getMonstersProtectingTile` 先问「这格的对象是否要站在
   前面用」（要，则不可能是战斗，除非瓦片上本身就是怪物）；`isTileUnderProtection` 先问「瓦片上是不是
   怪物」（是，则一定被保护）。两者不能互相化简，各自保留。
9. **缺口只留两处并写进文档**：`isTileProtectionStrongerThan`（要 `Army` 比较强度）与
   `getVisibleMonstersAroundHero`（要 `Heroes::GetVisionsDistance`）。都不在代码里塞假实现。
10. **越界索引返回空**：参考把校验留给两个调用方，端口在公开 API 上直接挡掉（返回空集合 / 不回调），
    因为 C# 的 `GetTile` 会抛异常，而这两个函数在 AI 估算里会被批量调用。

**后果**：新增 `src/FHeroes2.Game/World/WorldMapScan.cs`（八个公开成员 + 一个私有排序）+ 15 条
`WorldMapScanTests`。覆盖：单环扫描、`HasNearbyObject` 不算中心格、两种按距离排序、次元门的三种拒绝理由、
相邻/斜向/自身守卫、两格外不守卫、水陆不同侧不守卫、`IsNeedStayFront` 早退与 `checkObjectOnTile: false`
的反向开关、守卫报告顺序、越界索引。

**没测到的一支**：`ForEachMonsterProtectingTile` 里「直接走不通、但斜着相遇」的四条兜底分支只有逐字转写，
没有用例——端口要造出「目标格的斜向位被清掉、上下位仍在」的掩码，得在邻居上摆一个高物件再重算整张图
（ADR-064 的 `UpdatePassability` 只清 `TopLeft`/`TopRight`），成本高于这四行代码的风险。记在这里，别以为
被测过。

## ADR-085：`Maps::GetApproximateDistance` 与 `GetStraightLineDistance`

**背景**：`maps.cpp:691-716`。两个「大致多远」的度量，AI 的粗筛与「最近的对象」排序都在用。参考的注释
说得很清楚：它们都**忽略地形**，也**不是移动点数**。

**决策**：

1. **放进 `MapGeometry`**：两个函数都只吃两个瓦片索引和地图宽度，与 `GetDirection`/`GetAroundIndexes`
   同一族；`GetSquaredDistance` 也放这里。
2. **`GetApproximateDistance` 是 `max + min / 2`**（整数除法截断）：参考自己的例子是「下 7 右 5 = 9 格行程」，
   正好等于 5 个斜步 + 2 个直步的 9.5 截断。直接写成 `max + min / 2` 而不是 `(3*max + 3*min) / 4` 之类，
   免得截断行为漂移一格。
3. **`GetStraightLineDistance` 是 `std::hypot` 截断**：`(uint)Math.Sqrt( dx² + dy² )`。两条都用 `uint` 返回，
   与参考的 `uint32_t` 一致——它们不可能为负，签名里带 `int` 只会招来无意义的负数检查。
4. **`GetSquaredDistance` 是端口新增的一个名字**：参考把它内联在 `ComparisonDistance` 的比较器里（构造时
   存一个点，之后每次比较取两个 `GetPoint()`）。端口把它提成方法，一是因为两处排序都要用，二是因为
   比较器里内联的算术**没法单独测**。
5. **`ComparisonDistance` 不单独移植**：它的全部内容就是 `GetSquaredDistance` 的比较，端口在
   `World.SortByDistance` 里内联（ADR-084）。

**后果**：`MapGeometry` 新增三个方法；`WorldMapScanTests` 里三条用例钉住参考注释里的两个例子
（9 与 8）与平方距离的三个点。`ComparisonDistance` 在 `PLAN.md` 里按「已转（以方法形式）」记账。

## ADR-086：`getSquaredScoutingRadiusLimit` 少减了一次——一个真实的移植 bug

**背景**：`maps.cpp:531-560` 的 `getSquaredScoutingRadiusLimit`，被 `Maps::ClearFog`（半径版）与
`getFogTileCountToBeRevealed` 共用。端口在 E1 时把它实现成 `MapFog.SquaredRevealLimit`（渲染侧的雾模型），
公式写成 `( distance + 1 ) ^ 2 - distance`，只对 7 与 8 硬编码。

**问题**：参考的减法是**有条件的**：

```cpp
if ( scoutingDistance < 6 ) { squaredDistanceLimit -= scoutingDistance; }
return squaredDistanceLimit;
```

端口漏了 `< 6` 这个条件，于是半径 6 的上限是 43 而不是 49，半径 9 是 91 而不是 100——**寻路术宝物把视野
推到 6 以上之后，揭示圆盘比原版小一圈**。更糟的是 `MapFogTests` 里那行 `[InlineData(6, 43)]` 把这个错误
固化成了「参考表」，测试全绿反而挡住了发现。

**决策**：

1. **改公式**：`distance switch { 7 => 66, 8 => 90, < 6 => ((d + 1) ^ 2) - d, _ => (d + 1) ^ 2 }`，并把
   `< 6` 的理由写进 XML 注释（小半径时原版的圆盘比 `(d+1)` 的圆明显小一圈，半径 6 起才是完整的正方形）。
2. **改测试，不迁就实现**：`6 → 49`，并补一行 `9 → 100`（覆盖 `_` 分支的另一头）。
3. **不为此新增一层抽象**：`SquaredRevealLimit` 仍留在 `MapFog`。目前唯一的消费者是渲染侧的 `Reveal`，
   等 `Maps::ClearFog` 的半径版真正落到 `World`（要 `Kingdom`/`Players` 的联盟与 AI 分支）时再搬家，
   现在搬只会多一个「两边都在用」的中间层。

**后果**：雾的揭示圆盘在小半径之外与原版一致；`MapFogTests` 从 18 条变 19 条（多一行 `9 → 100`）。
教训：**把参考里的条件分支抄成无条件表达式，是移植里最容易「测试全绿却是错的」的一类错误**；凡是
「硬编码 + 公式」并存的函数，先看公式的适用条件。

## ADR-087：次要技能 14 项、数值表——参考的字段是**字母序**，移植时全部重排成 id 序

**背景**：`skill.h` 的 `Secondary::` 枚举（`skill.h:47-65`）定义十四个次要技能，id 就是它们在
`secondarySkillValuesPerLevel`（`game_static.cpp:91-104`）里的行下标。`Skill::SecondarySkillValues`
（`skill_static.h:45-61`）却把十四行的列写成**按技能名的字母序排列的十四个命名成员**
（`archery`、`ballistics`、`diplomacy`…），`game_static.cpp` 里的行字面量也跟着字母序写。

**问题**：这套「字段字母序 + 结构体按 id return」的写法在 C++ 里是自洽的，因为 `GetValue` 也是
一个十四分支的 `switch` 按名字取字段。C# 端如果照抄字段顺序建数组，但取值时按 id 下标访问，
**四个技能会拿到别人的数值而没有任何编译错误**——`Ballistics`（全零）与 `Pathfinding`（25/50/100）
会互换，`Estates`（100/250/500）会落到别处。

**决策**：

1. **枚举照 id 序**：`SecondarySkill`（`Unknown = 0`，`Pathfinding = 1` … `Estates = 14`），id 即下标，
   不做减法以外的任何映射。
2. **数值表只重排一次**：`SecondarySkills.Rows` 是一行一个技能的 `(Basic, Advanced, Expert)` 元组数组，
   按 id 序写，每行后面用注释点明技能名。重排发生在写这一张表的时候，取值路径里没有任何查表映射。
3. **用逐字段测试钉住整行**：`SecondarySkillsTests` 对十四行**逐个**断言三个等级的值，而不是只抽查
   两三个。只抽查的话，正是那种「互换一对」的错误最容易漏过去。
4. **`Unknown` 抛异常而不返回 0**：参考的 `GetValue` 对未知技能拿到 `nullptr` 行，返回 0；
   端口认为「手里有 `Unknown`」是调用方的 bug，抛 `ArgumentOutOfRangeException`。
   `SkillLevel.None` 仍返回 0（那是「没学这个技能」，合法）。
5. **单位不进表**：`GetValue` 返回裸数字，注释里写明每行的量纲（pathfinding 是移动点百分比、
   estates 是每天金币、wisdom 是等级上限）。参考的注释也是这么说的——只有调用方知道单位。
6. **`GetDiplomacySurrenderCostDiscount`（`skill.cpp:898`）单独一支**：40/30/20，无技能为 0。
   它不是 `secondarySkillValuesPerLevel` 里的一行（外交那行 25/50/100 是降低投降价的比例），
   所以不塞进表里，免得两个语义被误认为同一个。

**后果**：`SecondarySkills`（`Count`/`All`/`GetValue`/`GetDiplomacySurrenderCostDiscount`）。

## ADR-088：`factionProperties` 的六行数据——用一行字面量代替十四个命名属性

**背景**：`factionProperties`（`game_static.cpp:37-89`）是六行结构体，每行七个数块：队长主技能、
英雄初始主技能、初始法术 id、英雄初始次要技能（十四个值）、10 级分界、低/高两套主技能权重
（各四个值）、次要技能权重（十四个值）。`GameStatic::GetFactionProperties`（`game_static.cpp:205`）
是唯一的取用入口。

**决策**：

1. **`PrimarySkillValues` 用一个 `record struct`**：参考拿同一个结构体当四种东西用（初始值、
   两套权重），端口也用一个，但属性名把身份写清楚（`HeroInitialPrimarySkills` /
   `WeightsOfPrimarySkillsForLowLevels` / `WeightsOfPrimarySkillsForHighLevels`）。
2. **十四个值用 `SecondarySkillTable`，不用十四个命名属性**：参考靠命名字段 + 一个巨型构造调用
   获得按 id 取值的能力；端口靠「数组 + 下标器」。理由是**可读性与防错**：一次写成一行能看出
   它是「一行数据」，十四个命名属性要在六个种族里各写两遍，写反了没人发现。
   `Create` 校验长度必须 14、不许负数；下标器遇到非 14 项抛异常。
3. **重排同样只做一次**：六行里的十四个值全部按 id 序写（pathfinding 开头、estates 结尾），
   行注释点明参考行号（`game_static.cpp:37-44` 等）。
4. **初始法术 id 存裸 `int`**：端口还没有 `Spell` 枚举，`0 = 无`（骑士/野蛮人无魔法书）、
   15/19/17/10 分别是各族的初始法术。注释写明「等法术系统落地再收成枚举」，不假装有类型安全。
5. **非法种族返回 `null`**：`None`/`Random`/`Multiple` 与任何非单一值都返回 `null`，
   与参考的 `nullptr` 一致；调用方把 `null` 当「没有这个种族」而不是错误。
   比较用相等而不是位标志——随机种族是独立值，不是叠在某个种族上的位。
6. **只转这一支**：`game_static.cpp` 的其余函数（护城河减防、城墙远程惩罚、绿洲移动点、
   中立怪上限、漩涡失踪率、雾发现距离）都有系统依赖，本批一律不转。

**后果**：`FactionProperties`（record）、`PrimarySkillValues`、`SecondarySkillTable`、`Factions.Get`；
`FactionsTests` 对六族逐族断言初始值、两套权重各自的**和都是 100**、分界都是 10、
「队长不弱于英雄」这一条原版性质，以及对非法种族返回 `null`。

## ADR-089：`Skill::SecSkills`——空槽是数据，不是缺失

**背景**：`Skill::SecSkills`（`skill.h:128-153`，`skill.cpp:621-780`）是一个最多 8 项的向量
（`Heroes::maxNumOfSecSkills`，`heroes.h:85`），存的元素是 `{ skill, level }` **可以无效**——
`FillMax`（`skill.cpp:775`）会把它补齐到 8 个空槽，为的是存档格式定宽。

**问题**：如果 C# 端用「只存有效技能」的集合，`Count()`/`GetTotalLevel()` 看起来更简单，
但 `FillMax` 的补位语义、以及「第 i 个槽为空」这件事就无处可表达；而参考里所有计算都靠
**跳过无效项**来处理空槽，不是靠「不存」。

**决策**：

1. **保留槽位语义**：`_skills` 可以包含 `{ Unknown, None }`，`Skills` 原样暴露。
   `Count`/`TotalLevel` 与 `GetLevel` 的遍历都跳过无效项，和参考一致。
2. **填充顺序单独提出来**：`SecSkills(race)` 照参考构造器的十四个 `if` 的**书写顺序**填，
   这个顺序既不是 id 序也不是字母序。具体地：骑士是 `leadership` 第 9、`ballistics` 第 10，
   所以英雄界面列的是「领导术、炮术」而不是「炮术、领导术」。
   顺序提成 `SecondarySkills.InitialFillOrder`，并在两端（填充点与测试）都写明来源
   `skill.cpp:626-681`。
3. **`Add` 三步照参考的顺序**：先「已有就升级」，再「填第一个空槽」，最后「没满就追加」。
   第 9 个不同技能**静默丢弃**——参考就是静默的，防止它出现是升级流程的责任。
   无效技能直接返回（不写进槽里，否则空槽判断会失效）。
4. **`FindSkillsForLevelUp`（`skill.cpp:782`）不转**：它要两个随机种子与按种族权重抽取的那一层，
   属于升级界面本身。权重数据已经就位（ADR-088），缺的只有随机源。
5. **`SecondarySkillLevel` 用 `record struct` + `IsValid`**：对应参考的 `isValid()`，
   判据是「技能不是 `Unknown` 且等级不是 `None`」，两条都要——只看等级会把
   「有等级但没技能」的脏数据当成有效槽。

**后果**：`SecSkills`/`SecondarySkillLevel`；`SecSkillsTests` 钉住骑士的填充顺序、魔法族的自有技能在前、
空槽不计数、升级不增数量、第 9 个被丢弃、无效技能不入、`FillMax` 不动已学技能。

## ADR-090：经验阈值——硬编码 0–39 级，尾部按 1.2% 几何增长，阈值属于**下一级**

**背景**：`Heroes::GetExperienceFromLevel`（`heroes.cpp:1512-1602`）是 40 行硬编码数字，
`GetLevelFromExperience` 在它上面做反查，`getExperienceMaxValue()` 返回 39 级的阈值 2990600。

**决策**：

1. **表逐字抄**：40 个数（0 级到 39 级）一个不改，每行注释等级。这是数值平衡，不是公式的近似。
2. **40 级起用参考的浮点公式**：`previous + round((previous - FromLevel(level-2)) * 1.2 / 100) * 100`。
   保留浮点与 `MidpointRounding.AwayFromZero`，因为改成整数运算后中间值的舍入点会漂，
   几十级后差出几百点——参考的写法就是浮点，照抄浮点。
3. **阈值语义是「到下一级的经验」**：`FromLevel(1) = 1000` 表示「1000 经验就是 2 级」，
   所以 `LevelFrom(1000) == 2`、`LevelFrom(999) == 1`。这条在第一版测试里写反了
   （断言 `LevelFrom(threshold) == level`），红之后先怀疑实现，最后确认是**测试错、参考对**；
   已把两个函数的 XML 注释一起改清楚，避免下次再被相同的措辞绊住。
4. **上限是 39 级、上限经验 2990600**：`getExperienceMaxValue()` 就是 39 级阈值。40 级以上的
   尾段只为「存档里被改过的等级」这种边角输入存在，游戏本身走不到。
5. **超过 254 级返回 0**：参考的循环 `for ( level = 1; level < 255; ++level )` 找不到就返回 0，
   端口照抄，返回 0 表示「没有等级」而不是抛异常。
6. **负数等级抛异常**：`FromLevel(-1)` 在参考里是未定义行为，端口抛
   `ArgumentOutOfRangeException`。

**后果**：`HeroExperience`（`MaxValue`/`MaxTableLevel`/`FromLevel`/`LevelFrom`）；
`HeroExperienceTests` 对 40 行表逐行断言、对表内每级做「阈值落在下一级」的一致性检查、
并用 40/41/42 三级钉住尾部公式的舍入。

## ADR-091：`WorldPathfinder`——标签修正式搜索，队列的**追加顺序**是结果的一部分

**背景**：`world_pathfinding.cpp` 不是 A*，是一个「改进就入队」的标签修正式（label-correcting）搜索：
`processWorldMap()` 把起点写进缓存，然后用一个 `std::vector<int>` 当队列，`for ( size_t i = 0; i < v.size(); ++i )`
逐个取结点处理，`checkAdjacentNodes()` 在遍历邻居时**就地往这个 `vector` 追加**。同一个目标格有多条等代价
路线时，先被处理的结点先写下 `from`，后来的等代价路线不覆盖，所以最终那条路线的形状由这个顺序决定。

**决策**：

1. **照抄 `List<int>` + 索引循环 + 边遍历边追加**：`for ( int lastProcessedNode = 0; ...; ++lastProcessedNode )`
   配合 `nodesToExplore.Add(newIndex)`。换成 `Queue<int>`（语义等价）或 Dijkstra 的优先队列（语义不等价）
   都会改变等代价路线的选择，进而让「英雄路径预览」与参考对不上。这类差异不报错，只是走法不一样。
2. **松弛条件是「未到达 或 更便宜」**：`_cache[newIndex].From == -1 || _cache[newIndex].Cost > movementCost`。
   等代价**不**覆盖，这正是上一条依赖的写法。
3. **起点是唯一一个 `from` 保持 `-1` 的已到达结点**：`buildPath()` 的反向回溯就停在这个哨兵上。
   `Unvisited` 是共享的 `static readonly WorldNode`，`Array.Fill(_cache, Unvisited)` 一次清整张表；
   因为写缓存一律走 `WorldNode.Update()`（在 `WorldNode` 上原地改），并不会改到这个共享实例。
4. **`_mapOffset` 方向增量**：参考在 `setSearchContext()` 里以 0 号瓦片算一次八向偏移，之后用
   `node + offset[i]` 代替 `Maps::GetIndexFromDirection()`。端口照做，每次使用前都有
   `Geometry.IsValidDirection()` 守卫，所以只要「同一行内连续」成立就精确。
5. **矩形地图不断言**：参考里有一句
   `assert( "The reference pathfinder assumes a square map" )`（偏移量按行宽算，官方地图都是方形）。
   端口删掉这个断言——偏移是行增量，对任意形状都精确，而测试地图没必要是方形；真正的边界由
   `IsValidDirection()` 守住。这是**有意不移植**参考的断言，不是漏了。
6. **模板方法分四刀**：`IsMovementAllowed`（能否走一步）、`GetMovementPenalty`（一步多贵）、
   `ProcessCurrentNode`（到达后做什么）、`GetMaxMovePoints`（一天多少点）。基类包住缓存、队列、
   邻居遍历与「最后一格」规则；AI 侧将来只覆盖这四个。
7. **「最后一格」规则在基类**：剩余点 ≥ 出发格代价、但 < 目标格代价时，返回剩余点（花光）。
   条件是 `GetMaxMovePoints(...) > 0`——AI 寻路器报 0 个移动点，因此**不**享受这个折扣。
   地形代价取**出发格**（`getMovementPenalty` 用 `fromTile.ground`），只有道路要两端都是道路。
8. **船的对角切角**：查询的是**拐角背后**的格子，不是目标格；目标格自己的通行是第二个问题，
   不在这里重复。抄错成「查目标格」不会崩，只会让船在岸边多绕或少绕一格。

**后果**：`WorldPathfinder`（`GetDistance`/`Reset`/`SetSearchContext`/`ProcessWorldMap`/
`CheckAdjacentNodes`/`GetMovementPenalty`/`IsTileAvailableForWalkThrough`/`SubtractMovePoints` 与
四个抽象钩子）、`WorldNode`；`WorldPathfinderTests` 26 条钉住直线/对角代价、地形取出发格、
寻路术四档、道路、雾、水陆、切角、「最后一格」三态、起点无路径、路径连续性、怪物、缓存复用与越界。

## ADR-092：`PlayerWorldPathfinder`——五元组缓存，怪物「可到达、不可穿过」

**背景**：`reEvaluateIfNeeded( const Heroes & )` 只在「问的问题变了」时重跑整张地图的搜索；
`buildPath()` 反向回溯；`processCurrentNode()` 决定一个到达的格子要不要继续展开。

**决策**：

1. **缓存键是五元组**：起点索引、颜色、剩余移动点、寻路术等级、一回合移动点上限。五个全都等于
   上一次搜索时直接返回——鼠标悬停时每帧都会问同一个问题，这是它不卡的原因。
2. **端口没有 `Hero`，五个值改成参数**：参考读的是 `hero.GetIndex()`/`GetColor()`/`GetMovePoints()`/
   `GetLevelSkill( PATHFINDING )`/`GetMaxMovePoints()`。等 `Hero` 落到有移动点的那天，
   加一个 `ReEvaluateIfNeeded(Hero)` 重载转调本方法即可，其余不动。这是**已知的、临时的端口替换**，
   写在 XML 注释里逐条对照。
3. **不可穿过的格「到达但不展开」**：`ProcessCurrentNode` 先判 `IsTileAvailableForWalkThrough`。
   这让搜索停在怪物、拾取物、动作对象前——「能走到它旁边」和「能走穿它」是两件事。
4. **有守卫怪的格子只试探一步**：从被守卫的格出发只考虑「走向怪物」这一格，而且**不入队**——
   打完不打着由玩家决定，怪物背后的地是打完之后的问题。若怪物格不是邻格（守卫附近对象的怪），
   直接跳过（`Direction.Unknown`/`Center` 的判据）。
5. **`buildPath()` 空表即无路**：`GetNodeCost(target) == 0` 直接返回空表，起点自己也返回空表
   （起点代价是 0）。回溯链上 `from == -1` 与 `currentNode == -1` 各有一句 `Debug.Assert`，
   属于「不可能发生，发生了就是缓存被踩」的内部不变量，不是输入校验。
6. **越界抛 `ArgumentOutOfRangeException`**：参考的 `getDistance()`/`buildPath()` 不做检查，
   端口在公开入口检查——这是 C# 侧的既定约定（ADR-057 之后所有公开 API 都这样）。

**后果**：`PlayerWorldPathfinder`（`ReEvaluateIfNeeded`/`BuildPath`/`ProcessCurrentNode`/
`GetMaxMovePoints`）挂在 `World.Pathfinder` 上（`??=` 惰性创建，空地图构造抛异常），
`World.ResetPathfinder()` 与 `LoadMap` 都会丢缓存，保证两次搜索不跨地图残留。
AI 侧的 `AIWorldPathfinder` 与 `ObjectValueStorage` 未转——它要军队与对象价值表。

## ADR-093：`Route::Step` → `RouteStep`——三个索引都存下来，`Penalty` 是**单步**代价

**背景**：`route.h` 的 `Route::Step` 是 `{ from, index, direction, penalty }` 四元组，
`route.cpp` 里到处用 `step.from` / `step.index` 判断「这一步从哪到哪」。

**决策**：

1. **`readonly record struct RouteStep(int Index, int From, Direction Direction, uint Penalty)`**：
   四个字段全保留。`From` 看起来冗余（相邻格的 `from` 可以反推），但路径的**起点**推不出来，
   而 `Route::Path` 恰好靠 `front.from` 判断「英雄是否还站在路线的起点上」。
2. **`Penalty` 是单步代价，不是累计代价**：`Route::calculatePathPenalty()` 才把它们加起来。
   `GetAllowedSteps()` 因此是「逐步扣减」，不是「和总代价比较一次」。
3. **`Direction` 为 `Unknown` 表示传送**：`Route::Path` 用这一位区分「走一步」与「跳到远处的格子」，
   见下一条。传送的 `Penalty` 照参考填 0。

**后果**：`RouteStep`；`HeroPathTests` 与 `WorldPathfinderTests` 的联调用例钉住「
`BuildPath()` 产出的相邻步骤连成一条链」。

## ADR-094：`Route::Path` → `HeroPath`——队列语义，与 `_hero` 指针的两处参数化

**背景**：`Route::Path`（`route.h:120-215`、`route.cpp:38-414`）是英雄身上那条「预览路线」：
动画每走完一步 `PopFront()`，鼠标拖动时 `Truncate()`，读档时整条恢复再验一遍。

**决策**：

1. **它是队列不是栈**：`FrontIndex`/`FrontFrom`/`FrontDirection`/`FrontPenalty` 全看第 0 步，
   `NextStepDirection` 看第 1 步（少于两步就是 `Unknown`——转弯动画要用）。删尾部用 `Truncate()`，
   删头部用 `PopFront()`，两者都不做「删除后重排」，因为路线本来就是连续的。
2. **不存「英雄站在哪」**：那就是 `FrontFrom`。`IsValidForMovement(mapSize)` 只问「这一步本身合法吗」
   （方向已知 + 落点在地图内）；「英雄还在原地吗 + 付得起吗」合成 `IsWalkableFrom(heroIndex, movePoints)`。
   两个问题分开，是因为读档后两步校验的先后不同。
3. **`Direction.Unknown` 就是传送**：`IsValidForTeleportation()` 要求方向未知且落点合法。
   方向未知 + 落点合法 = 跳跃；方向已知 + 落点合法 = 走一步。这个编码参考里没有注释，
   是从 `IsValidForMovement`/`IsValidForTeleportation` 两个互为补集的判断反推出来的。
4. **`GetAllowedSteps()` 不做「最后一格」折扣**：它算的是界面标绿的那几步 = 今天真能走完的步数。
   寻路器里的折扣（ADR-091 第 7 条）回答的是「能不能到」，这里回答的是「到不到得了」，
   两个问题不同，抄成同一个会让绿色路线比实际多一格。
5. **`_hero` 指针改参数**：`hasAllowedSteps()`/`GetAllowedSteps()` 读 `_hero->GetMovePoints()`，
   端口改成参数；`String()` 要 `_hero` 的坐标，未转。等 `Hero` 有移动点后加重载转调。
6. **未转 `GetIndexSprite`**：260 行 `ICN::ROUTE` 精灵号表（260 = 40 个方向组合 × 代价档），
   需要渲染侧与 `ICN` 资源，先不搬。空路线约定：`FrontIndex`/`FrontFrom`/`DestinationIndex` 为 `-1`，
   `FrontDirection`/`NextStepDirection` 为 `Unknown`，`FrontPenalty` 为 0，与参考一致。
7. **默认隐藏**：`_hide = true`，路线是玩家要求出来的东西，不是英雄存在就有的。

**后果**：`HeroPath`（`SetPath`/`Reset`/`Truncate`/`PopFront`/`Show`/`Hide`/`IsValidForMovement`/
`IsValidForTeleportation`/`IsWalkableFrom`/`HasAllowedSteps`/`GetAllowedSteps`/`CalculatePenalty` 与
静态 `CalculatePenalty`）；`HeroPathTests` 21 条。

## ADR-095：`army.cpp`/`army_troop.cpp` → `Army`/`Troop`——`commander` 指针拆成两个属性，最慢兵种是移动点的唯一来源

**背景**：参考的 `Army` 持有一个 `HeroBase * commander`，种族、颜色、幸运、士气、`GetControl()` 全从它
问出来；`Troop` 继承 `Monster`，攻击/防御还要叠加英雄加成。这套继承在端口里没有对应物。

**决策**：

1. **`commander` 不搬，拆成属性**：`Army.Race`（`Reset()` 用它选初始兵种）、`Army.Color`（
   `GetCapturedObject` 一类交互用）都是可写的普通属性，由 `Hero` 在装载时同步。参考的
   `GetCommander()`/`inCastle()`/`GetControl()` 一律不转——它们都只服务于界面与 AI。
2. **`Troop` 是纯数据**：`{ MonsterType Monster, uint Count }` + `MonsterStatistics` 的查表。
   `GetAttack()`/`GetDefense()`（英雄加成版）**不转**，只留 `GetBattleStats()` 的原始值；
   `GetMonsterString()`（翻译文案）不转。
3. **五个槽位永远是对象**：参考是 `std::vector<Troop *>`，五个槽都 `new Troop()`，所以
   `GetTroop(i)` 从不返回空指针；端口同样用 `Troop[]`，空槽是 `IsValid == false` 的对象。
   这条看着琐碎，但 `JoinTroop`/`GetSlowestTroop` 全靠「解引用不用判空」简化。
4. **`GetSlowestTroop()` 是移动点的唯一来源**：`Heroes::GetMaxMovePoints()` 只问它一个。
   参考的 `getBestMatchToCondition()` 用严格小于比较（并列时保留**先出现的**那一槽），端口照抄，
   否则「两队同速」的最慢兵种会随槽位浮动，进而让每日移动点在读档前后不一致。
5. **`JoinTroop(monster, count, emptySlotFirst)` 的三分支照抄**：合并进**第一个**同兵种槽
   （不是最后一槽）、否则第一空槽、否则失败。`emptySlotFirst = false` 只改变「先合并还是先占空槽」，
   不改变合并目标。
6. **`Reset(defaultArmy, generator)` 的随机走参数**：参考读全局 `Rand`，端口收可选 `Pcg32`，
   空则用全局。初始兵数的档位（农民 30–50、1 级 6–10、2 级弓手 3–5 …）单列
   `GetNumberOfMonstersInStartingArmy()`，`Reset` 只负责掷骰。
7. **未转**：幸运/士气及其修正、`GetStrength()`、`String()`/`TroopSizeString()`、
   `JoinStrongestFromArmy`/`MoveTroops`/`SwapTroops`（要城堡与界面）、`GetJoinSolution`（要 AI）、
   `ArrangeFor*`（要战斗）、`setFromTile`（要对象数据字）。它们都记在类注释里。

**后果**：`Troop`、`Army`（`Slots`/`this[]`/`GetTroop`/`GetFirstValidTroop`/`GetSlowestTroop`/
`GetFastestTroop`/`GetCount`/`GetTotalHitPoints`/`HasMonster`/`CanJoinTroop`/`JoinTroop`×2/`Clean`/
`Reset`/`GetNumberOfMonstersInStartingArmy`）、`DwellingMonsters`（`Monster::FromDwelling` 的等价物）；
`TroopTests` 14 条、`ArmyTests` 42 条。

## ADR-096：英雄移动点——六档基数表 + `_updateMovementPoints` 的 33/66 精确取整

**背景**：`Heroes::GetMaxMovePoints()`（`heroes.cpp:972`）是「今天能走多远」的唯一入口。它分陆/水两支，
陆地按**最慢兵种**的速度查表，再由 `Logistics`/`Navigation` 放大，水上还要加灯塔。

**决策**：

1. **基数是查表不是公式**：极慢 1000 / 慢 1100 / 普通 1200 / 快 1300 / 极快 1400 / 超快 1500，
   与 `MonsterSpeed` 一一对应。空军队返回 **0**（参考里最慢兵种为空就直接 `return 0`），
   不是「按极慢算」。
2. **水上固定 1500**，与船上装了什么无关；`Logistics` 在水上**不生效**，只有 `Navigation` 生效。
3. **`_updateMovementPoints` 的 33/66 是乘分数，不是百分比**：`skillValue == 33` 时 `points * 4 / 3`、
   `== 66` 时 `points * 5 / 3`，其余才是 `points + value * points / 100`。
   1500 上基础航海术是 **2000**（不是 1995），高级是 **2500**（不是 2490）——这 5/10 点差异
   在整张地图上会累积成「多走一格或少走一格」，所以不能当实现细节忽略。
4. **灯塔加成放在水上那一支的末尾**：`+500 × CountCapturedObject(Lighthouse, color)`。
   注意它加在技能放大**之后**，所以航海术不会放大灯塔的 500。
5. **未转**：`LAND_MOBILITY`/`SEA_MOBILITY` 神器加成与马厩（Stables）的 400 点——
   两者都要神器与对象动作体系；参考里对未列出的速度档 `assert(0)` 返回 0，端口保留返回 0 不保留断言。
6. **`ApplyMovementPenalty()` 归零而不是变负**：代价大于剩余点时直接置 0。这是「最后一格」折扣的落点
   ——寻路器允许透支一格，动画负责把余额清干净。

**后果**：`Hero.Army.cs`（`Army`/`SecondarySkills`/`MovePoints`/`SetMovePoints`/`IncreaseMovePoints`/
`GetMaxMovePoints(bool onWater, World?)`/`ApplyMovementPenalty`）；`HeroArmyTests` 26 条。

## ADR-097：`heroes_move.cpp` → `Hero.Movement.cs`——九帧状态机，`_spriteIndex` 是钟摆

**背景**：`heroes_move.cpp` 不是「走一步」的函数，是一个每帧推进一步的状态机：一格九帧，
第 0/1/8 帧各有一个分支；转向是另外一段 45~53 的精灵块，与走动共用同一个 `_spriteIndex`。

**决策**：

1. **一格九帧，帧号 = `_spriteIndex % 9`**：
   - **帧 0**：若下一格是「必须停在前面」的对象，就地完成这一格的**记账**（扣点、弹路径、触发动作）
     但不移动；否则只把精灵推进一格。
   - **帧 1**：`Scout(下一格)` 清雾。
   - **帧 8**：`_spriteIndex -= 8` + `Move2Dest` + `PopFront` + 扣点 + 到达动作。
     若移动仍然开启、朝向不变、且下一格不需要停，则 `++_spriteIndex` **跳过站立帧**——
     否则每格之间会闪一下站姿。
2. **`_direction` 与 `_spriteIndex` 必须同时改**：参考里 `_direction{ RIGHT }`、`_spriteIndex{ 18 }`
   （`heroes.h:792`），18 正是**朝右**那个块的站姿。端口第一版写成 `Bottom` 配 18，
   于是 `SetValidDirectionSprite()` 一律把精灵修回 36，`Move()` 的帧 0 判定全部错位——
   这条是写测试时才发现的，记在这里是因为「两个字段在同一个不变量里」很容易漏。
3. **转向块只覆盖五个方向**：`0/9/18/27/36` 是上、右上、右、右下、下的**站姿**帧，左右共用一块
   （镜像翻转）。`45..53` 是转向动画块，入口与出口各一个 `switch`：入口按
   「顺时针进 45/47/48/49/50、逆时针进 46/48/49/50」定位，出口按「动画走到哪个方向」映射回站姿帧
   并把 `_direction` 前进一步。所以**一次 90° 转向要连续调用 7 次**（逆时针）、**6 次**（顺时针）。
4. **`framesToSkip = clamp(4 / speedMultiplier, 1, 3)`**：跳过帧数由动画倍速决定，`>= framesToSkip`
   才真的动一帧。基线速度是**每三帧动一帧**，不是每帧。
5. **像素偏移 = `frame - 1` 步 × 4**：帧 0/1 站格上，帧 2..8 依次偏移 4/8/…/28 像素；转向块（`>= 45`）
   恒为 0。`Offset` 是额外的基础偏移（眩晕、上船等），相加而不是覆盖。
6. **`Move()` 的守卫是 `midStep`**：移动点耗尽时若英雄已经走在两格之间（帧 2..8 或正在转向），
   这一帧仍然推完，否则精灵会卡在格子交界处。`midStep` 之外，`!(mode & ENABLEMOVE)` 直接返回。
7. **`isNeedStayFrontObject()` 只留对象表一支**：参考的「非友方有驻军的城堡」「船长下船」两支
   在端口里没有对象可查，落到 `Mp2ObjectTypes.IsNeedStayFront(tile.MainObjectType)`。
8. **未转**：`playHeroWalkingSound()`（无音频）、`FadeOut/FadeIn`（无渲染）、`_isInVisibleMapArea()`
   （无视口）、以及 `MoveStep` 里的声音节拍。

**后果**：`Hero.Movement.cs`（`FrameCountPerTile = 9`、`FirstTurningFrame = 45`、`MoveStep`/
`AngleStep`/`GetCurrentPixelOffset`/`Move`/`MovementDirection`/`SetValidDirectionSprite`/
`IsNeedStayFrontObject`/`MoveToDest`/`IsInDeepOcean`）；`HeroMovementTests` 37 条。

## ADR-098：`IHeroMovementHandler`——四个缺失子系统的接缝；`MoveEnabled`/`Action` 两位补上

**背景**：`heroes_move.cpp` 每走完一格都要调 `Scout`（清雾）、`Action`（打开对象）、
`ActionNewPosition`（重新渲染 hero 图层）、`ScoutRadar`（刷新小地图）。这四个子系统都还没转，
但状态机本身（帧、朝向、扣点、路径）与它们无关。

**决策**：

1. **注入一个接口而不是删掉调用**：`IHeroMovementHandler` 四个方法，默认实现
   `NullHeroMovementHandler.Instance` 全是空操作。理由是这些调用**有先后顺序与次数语义**
   （帧 1 一次 Scout、帧 8 一次 ScoutRadar + ActionNewPosition），删掉就丢了「谁在第几帧发生」，
   以后接回来还得重新推一遍时序。
2. **测试就是第二个实现**：`RecordingHandler` 记录调用序列，于是「帧 1 清雾、帧 8 才移动」
   这类断言不依赖雾与小地图。这比「转完子系统再测」早了好几个里程碑。
3. **`isControlHuman()` → `Hero.IsControlHuman` 属性**，`Game::HumanHeroAnimSpeedMultiplier()` →
   `Hero.AnimationSpeedMultiplier`。两者都是可写属性，AI 侧接进来时只改写入方，不碰状态机。
4. **`HeroModes` 补两位**：`MoveEnabled = 0x08`（参考 `ENABLEMOVE`）与 `Action = 0x100`
   （参考 `ACTION`）。参考的模式字与端口此前转的那批位是同一套编号，缺这两位 `SetMove()`/
   `Move()` 的守卫写不出来。
5. **`Move2Dest` 的三步顺序照抄**：旧格 `SetHero(null)` → `SetMapIndex` → 新格 `SetHero(this)`。
   先设索引再清旧格会让旧格残留一个指向自己的英雄，`GetHero()` 走 `OccupantHeroId` 恰好能自愈，
   但「身上有英雄的格子」这个判定会错一帧。同格调用是 no-op。

**后果**：`IHeroMovementHandler`、`NullHeroMovementHandler`、`HeroModes.MoveEnabled`、
`HeroModes.Action`；`HeroMovementTests` 里的 `RecordingHandler`。

## ADR-099：幸运、士气与怪物能力——两把尺子都是 `int`，能力是 33 位的位标记

**背景**：`Luck` 与 `Morale` 是 `army.cpp` 的士气修正、`heroes.cpp` 的 `GetLuck`/`GetMorale`、
战斗的开局判定与界面共用的两把七档尺子。两条计算都要先问「这个兵种是不是亡灵」，
而 `Monster::isUndead()` 读的是 `monster_info.cpp` 的能力表 —— 一个此前完全没转、却被四五个子系统
引用的数据块。

**决策**：

1. **`Luck`/`Morale` 是 `static class` + `const int`，不是 `enum`**：参考本身就是无名 enum
   （`namespace Luck { enum { ... }; }`），值要被相加（技能 +1、泉水 +1、金字塔 −1）再夹紧，
   不是枚举成员之间的选择。用 `enum` 会逼出一次 enum↔int 的来回转换，而没有任何类型安全收益。
   两个类型分开是为了「幸运值不能当士气值传」，这是参考用两个命名空间表达的意思。
2. **`Normalize` 的夹紧区间是 `CURSED..IRISH`，`UNKNOWN` 不在区间里**：`UNKNOWN = -4` 只是
   「英雄还没上场」的初值，`Normalize` 永远不会返回它，所以夹紧到 −3..3。
3. **能力表只存「有/没有」，用位标记**：参考的每个能力实例还带两个数——一个百分比和一个法术 id
   （`MAGIC_RESISTANCE, 100, 0`、`SPELL_CASTER, 20, Spell::BLIND`）。前者是战斗算术，后者要等法术 id
   体系；冒险地图问的全都是「有没有」，所以 `MonsterAbility` 是 `[Flags]` 位掩码。
   **必须 33 位**：`NONE` 之外有 32 项，最后一项 `SOUL_EATER` 的位在 1<<32 —— `uint` 上写
   `1u << 32` 会按 C# 的移位语义绕回 `1u << 0`，静默变成 `DOUBLE_HEX_SIZE`。所以底层是 `ulong`。
4. **表用脚本从参考生成，不手抄**：`monster_info.cpp:388-590` 是 108 条
   `monsterData[Monster::X].battleStats.abilities.emplace_back(...)` 语句，手抄 108 行必然出错且无法
   复核。用一次性脚本按正则抽取 `(monster, ability)` 对、按参考出现顺序去重输出 C# 行，生成后人工补
   注释与文件头。测试钉住分布：**50** 个兵种至少一项、亡灵 **11**、元素 **4**、飞行 **13**、龙 **4**、
   双格 **16**、语句 **108** —— 这几组数都能从参考里核对，脚本抽错就会露馅。
5. **`Army::GetMoraleModificator()` 1:1 照抄，包括那个反直觉的分支**：单一种族时不是「+1」而是
   「**且** `!AllTroopsAreTheSame()`」才 +1 —— 也就是说五个槽都是同一种兵时反而没有种族加成。
   两个种族 0、三个及以上 −(n−2)、有亡灵再 −1。返回值**不夹紧**，参考把 `Morale::Normalize` 留给
   调用方（`Heroes::getMoraleWithModifiers`）。`MapRace.None`（幽灵、元素）也是集合里的一个种族。
6. **`AllTroopsAreTheSame()` 与 `AllTroopsAreUndead()` 的空军队默认值不同，这是有意的**：
   前者 `true`（没有可比的槽），后者 `false`（有一个「见过有效槽」的标志）。两者都进了测试，
   因为它们是 `GetMoraleModificator` 那两个分支的实际入口。
7. **`Army` 没有 commander，所以 `GetLuck()`/`GetMorale()` 退化为修正项**：参考在
   `GetCommander()` 为空时正是这么做的。英雄那边的 `Hero.GetLuck()`/`GetMorale()` 只含**技能项**
   （`Luck`/`Leadership` 每级 1 点）并夹紧；访问对象效果（`GameStatic::getObjectLuckEffect`）、
   神器加成与「最大幸运/士气」神器三项未转 —— 它们都是加法项，接进来时不需要重算已有部分。
8. **城堡项留空但位置留好**：`Army::GetLuckModificator()` 参考只有 `inCastle()` 一项，端口返回
   `Luck.Normal` 并写明是缺 `Castle`。`GetMoraleModificator()` 的城堡项同理（它是唯一一处加法缺口），
   以及「殉难者之臂」把亡灵惩罚加到活人军队上那一支。

**后果**：`Kingdom.Luck`、`Kingdom.Morale`、`Monsters.MonsterAbilities`（`MonsterAbility` 32 位 +
`Get`/`IsPresent`/`IsUndead`/`IsElemental`/`IsFlying`/`IsDragons`/`IsWide`/`IsAffectedByMorale`）、
`Troop.GetRace`/`IsUndead`/`IsAffectedByMorale`、`Army.AllTroopsAreTheSame`/`AllTroopsAreUndead`/
`GetLuck`/`GetLuckModificator`/`GetMorale`/`GetMoraleModificator`、
`Heroes.Hero.GetLuck`/`GetMorale`；`MonsterAbilitiesTests` 9 条、`LuckMoraleTests` 35 条。

## ADR-100：兵种强度——法术 id 先落，能力载荷从「位」变成「实例」，强度的缓存必须 `Lazy`

**背景**：ADR-099 记「能力的数值与法术载荷要等战斗侧」，但 `getMonsterBaseStrength()`
（`monster_info.cpp:45`）在马上升级时就只读一个载荷 —— `SPELL_CASTER` 的能力值（法术 id）决定这个能力
加多少分。所以强度的第一块砖不是强度，而是 `spell.h` 的 id 枚举与能力表的载荷。这一批把
`getMonsterBaseStrength` → `Monster::GetMonsterStrength` → `Troop::GetStrength*` → `Army::GetStrength`
这条链整条落地。

**决策**：

1. **只转 `spell.h` 的 id 枚举，不转 `Spell` 对象**：`Spell` 类带等级、学派、图标与施法逻辑，那属于战斗
   引擎；而 id 是被数据文件、卷轴、能力表引用的常量，必须精确。`SpellType` 因此是纯 `enum`（73 项 +
   `Count` 哨兵），配一个 `SpellTypes.IsValid`（`id != NONE`）。`RANDOM`/`RANDOM1..5` 与 `PETRIFY` 也
   在这个编号里 —— 它们不进法术书，但确实是 id，因此 `Count` 是 73 而不是 67。
2. **能力表从「掩码」改成「实例 + 派生掩码」**：ADR-099 的 `Dictionary<MonsterType, MonsterAbility>` 只有
   位，载荷（`MAGIC_RESISTANCE, 25, 0`、`SPELL_CASTER, 20, Spell::BLIND`）被丢掉了。现在表存
   `MonsterAbilityInstance(Type, Percentage, Value)`，掩码在静态构造时从实例折出来，于是**只有一份数据**，
   `Get()` 的语义不变、旧测试一条都没改。两个数字的含义随能力而变（概率/法术/士气量），参考没有统一
   说法，所以照原样保留并逐条注明；`Spell` 属性只是把 `Value` 读成 `SpellType`。
3. **`getMonsterBaseStrength` 的每个系数都照抄，包括看起来奇怪的**：
   `DOUBLE_DAMAGE_TO_UNDEAD` 值 1.15（因为全部兵种里约七分之一是亡灵）、
   `DOUBLE_MELEE_ATTACK` 在有 `NO_ENEMY_RETALIATION` 时是 ×2 否则 ×1.75、
   速度**非对称**（比 AVERAGE 慢一档 −0.1，快一档 +0.05），最后 `sqrt(damage * effectiveHP)`。
   这些数字是原作者写下的启发式，没有"更合理"的版本，改动就是行为变更。
4. **强度的缓存必须用 `Lazy<double[]>`**：`BattleStats`/`GeneralStats` 两张表声明在类末尾，若把
   `BaseStrength = BuildBaseStrength()` 也写成普通静态字段并放在前面，类型初始化会先跑强度再初始化表，
   `BattleStats` 还是 `null` —— 实测就是 `TypeInitializationException` 套 `NullReferenceException`，
   所有相关测试一起红。`Lazy` 把计算推到第一次查询，顺序问题消失且仍然只算一次。
5. **`Monster::GetMonsterStrength` 的两个默认参数代替 C++ 的子类重载**：参考让战斗侧的 `BattleTroop`
   覆盖 `GetAttack()`/`GetDefense()` 把英雄或法术加成塞进去。端口没有子类，所以加
   `attack`/`defense` 两个参数，负数表示「用怪物自己的数值」。
6. **`Army::GetStrength` 拆成两半，`Army` 只留军队自己的部分**：参考的算式里，
   `commander->GetSecondarySkillValue(ARCHERY)`（平方根放大）、`commander->GetAttack()`/`GetDefense()`
   （进 `GetStrengthWithBonus`）与 `commander->GetMagicStrategicValue()`（加在最后）都要英雄；而士气与
   幸运系数在无英雄时依然生效（`GetMorale()`/`GetLuck()` 就是军队自己的修正）。
   `Army.GetStrength()` 因此保留 `1 + morale/(12 或 24)` 与 `1 + luck/24`，把箭术/攻防/魔法战略值留给
   `Hero` 侧（等神器袋与法术书）。`Troop.GetStrengthWithBonus(attack, defense)` 的签名先按参考保留，
   英雄那三项接进来时不需要改这里的算术。
7. **强度测试的期望值来自参考源码的独立转写，而不是 C# 实现的回读**：手算是可以的，但 12 个系数 +
   50 行能力表手算必然出错。于是用一次性 Python 脚本从 `monster_info.cpp` 的数值表与能力表按参考公式
   重算一遍（不 import 任何 C#），把结果写进 `[InlineData]`。任何漏掉的因子、串位的行、错的
   速度方向都会立刻变成数字不符，而不是"两边一起错"。这也是本批唯一能发现 `Lazy` 那个初始化 bug 的
   手段 —— 它没有被 C# 复读掩盖。

**后果**：`Spells.SpellType`（74 项）与 `Spells.SpellTypes.IsValid`；
`Monsters.MonsterAbilityInstance`、`MonsterAbilities.GetInstances`/`TryGetInstance`（掩码改为派生）；
`MonsterStatistics.GetMonsterBaseStrength`/`GetMonsterStrength`（`Lazy` 缓存）；
`Troop.GetStrength`/`GetStrengthWithBonus`；`Army.GetStrength`；
`SpellTypeTests` 24 条、`MonsterStrengthTests` 29 条、`MonsterAbilitiesTests` 13 条（+4）。



## ADR-101：神器袋与英雄四维——三把开关不构成划分，`ArtifactType` 不复制第二份，英雄数值是「加到」兵种上

**背景**：ADR-100 把 `Army.GetStrength` 拆成两半，英雄侧那三项（箭术、攻防、魔法战略值）留了
`IArmyCommander` 接口。要接上它，先得有英雄的四维和神器袋——而神器袋又要 `artifact_info.cpp` 那张
105 行数值表与 `artifact.cpp` 的 `BagArtifacts`。这批一开头还踩到两个本来不该存在的坑：一个重复的
105 项 id 枚举，一个被 `.gitignore` 吞掉的源码目录。

**决策**：

1. **id 枚举只留 `Resources.ArtifactType` 一份**：本批最初按参考的 `class Artifact` 又建了一个
   `Artifacts.Artifact` 枚举（105 项、一字不差），但 `Resources.ArtifactType` 早在 M4 就落地，且被
   `MapObjectCatalog`/`TileMetadata`/`MapEvent` 使用。两个 105 项枚举等于两个必须永远同步的源文件，
   所以删掉新的那份、整批改用 `ArtifactType`；`isValid` 谓词按 `SpellType`/`SpellTypes` 的既有写法
   并入 `ArtifactType.cs` 成为 `ArtifactTypes.IsValid`。顺带修掉 `.gitignore` 第 4 行的 `[Aa]rtifacts/`
   （VS 的构建输出规则）——它把 `src/FHeroes2.Game/Artifacts/` 整个目录忽略了，`git status` 看不见、
   `rg` 也搜不到，也就是说这批代码本来根本进不了版本库。
2. **数值表由一次性脚本从参考解析**：105 个神器 × 最多六条 bonus/curses，手抄必错。生成器解析
   `artifact.h` 的枚举与 `artifact_info.cpp` 的 `emplace_back`，并把三个非字面值也从参考算出来：
   `Spell::NONE`/`Spell::TOWN_PORTAL` 查 `spell.h` 的枚举下标，`GameStatic::getCastleWallRangedPenalty()`
   = 50（金弓取消城墙远程惩罚）。`UNKNOWN` 与哨兵共用 `ArtifactData.Empty`，不共享一行可变的表行。
3. **三把开关不是划分，第四堆必须显式建模**：`isBonusCumulative`/`isBonusMultiplied`/`isBonusUnique`
   全是 `switch` + `default: break`，三个都不命中的类型在 `getArtifactDescription`
   （`artifact_info.cpp:1028`）里叫 `[ cumulative per artifact type ]`——15 个 bonus（士气、幸运、
   陆海移动、每日法力点、`ADD_SPELL` 等）与 `MORALE` 这一条 curse 都在这一堆。`getTotalArtifactEffectValue`
   对它的求和是「每个神器 id 只算一次」，对 cumulative 才是每份都算。所以测试不能写「每个类型恰好落一堆」，
   那是错的前提；写成「至多一堆 + 第四堆成员清单」，同时把「两份同类只算一次、两份不同类会相加」两半都钉住。
4. **`IArmyCommander` 取代 `HeroBase*`**：参考里 `Army` 存 `HeroBase*` 并调四个方法
   （`GetAttack`/`GetDefense`/`GetSecondarySkillValue`/`GetMagicStrategicValue`）。端口不引入
   `HeroBase` 抽象类，只抽这四个方法；`Army.Commander` 可空，城堡守军与野怪就是空的那个，
   于是 `Army.GetStrength` 无英雄也照算。英雄构造时把 `_army.Commander = this` 接回来。
5. **四维的 clamp 差异要保留**：攻防 0..255，法力与知识 **1..255**（英雄不可能没法力）；
   `IncreasePrimarySkill`/`SetPrimarySkill` 自己再按 0..255 截一次。攻防下限 0 与法力/知识下限 1 的
   不对称是参考原样，不要「统一」。
6. **英雄加成是「加到」兵种自身数值上，不是替换**：`Troop.GetStrengthWithBonus(atk, def)` 把英雄攻防
   与兵种攻防相加再折 `1 + atk*0.1 + def*0.05`；箭术只放大射手，放大的对象是整只兵的强度
   （`sqrt(1 + archery/100)`）而不是伤害。测试初版按替换写，期望 6.9 实际 10.8，差值正好是一份兵种
   自身的攻防——这个错误只有把期望值当独立算式算出来才会暴露。
7. **`GetMagicStrategicValue` 先返回 0 而不假装**：参考的实现要法术书里的法术数与法力点，两者都不存在，
   所以接口先返回 0 并在 XML 注释里写明缺口，而不是编一个公式。

**后果**：`Resources.ArtifactTypes`；`Artifacts.ArtifactBonus`/`ArtifactCurse`/`ArtifactData`/
`ArtifactTable`/`ArtifactBonuses`/`ArtifactBag`；`Heroes.PrimarySkill`/`Hero.PrimarySkills`；
`Army.IArmyCommander`/`Army.Commander`；`Army.GetStrength`（箭术 + 英雄攻防 + 魔法战略值）；
`ArtifactTests` 28 条、`ArtifactBagTests` 11 条、`HeroPrimarySkillsTests` 12 条。
`FHeroes2.Game.Tests` 1091 → **1142**，全解决方案 1777 → **1828** 条全绿。

## ADR-102：法术书与法力点——参考把法术 id 当怪物 id 用，照抄；卷轴在端口里不带法术，写明

**背景**：ADR-101 之后 `Army.GetStrength` 只差魔法战略值那一项，而它要 `heroes_base.cpp:358-377` 的
「书里最好的战斗法术」。法术书是神器，法力点是计数器，法术的价值要 `spell.cpp` 的 73 行数值表与
`getStrategicValue` 的四个分支。三样都不存在，所以这一批一次补齐。

**决策**：

1. **数值表继续由脚本从参考解析**：73 行 × 五个数字，其中还有「看着像手误」的值（次元门 225 移动点 /
   最少 69、`VIEW_ALL` 只要 3 点法）。生成器解析 `spell.cpp` 的 `spells` 数组，只取五个数字，
   名字与描述是 gettext 文案，不进端口。
2. **`Spell` 用值类型，`SpellType` 仍是编号**：参考把 id 枚举放在 `Spell` 类里、行为也在类里；端口把
   枚举留着当存档/地图编号（`SpellType`，ADR-100 已落地），另加 `readonly record struct Spell` 当
   「法术书里存的那个值」。构造函数照抄越界归零，坏文件索引不到表外。
3. **`getStrategicValue` 的召唤分支照抄一个 bug**：`spell.cpp:263` 是
   `Monster( id ).GetMonsterStrength() * ExtraValue() * spellPower`，`id` 是**法术** id 却被当**怪物**
   id 用 —— 四个召唤术因此量的是 44..47 号怪（法师/大法师/巨人/泰坦），不是元素。AI 排名依赖这个数，
   所以端口写 `(MonsterType)(int)Type` 并在 XML 注释、测试与这里三处标注。**不要「修」它**，
   要改就得连 AI 的期望值一起改。
4. **战略值是凹函数且上限十次**：`casts == 1` 时修正为 1，否则 `casts - 0.05 × casts²`；十次施法值
   五次的钱，这是参考对「法术越多边际越低」的建模。召唤分支不乘这个修正（参考原样）。
5. **`isCombat` 是拒绝清单，兜底在 `casts`**：十八条冒险法术之外全是战斗法术，连五个 `RANDOM*`
   占位与 `PETRIFY` 也被判成战斗法术；`getStrategicValue` 用「耗法为 0 → `casts = 0` → 返回 0」兜住，
   而不是给 `isCombat` 打补丁。
6. **书是神器，没书就没有法术**：`getAllSpells()` 在没书时返回空容器而不是卷轴 —— 知道不等于能施。
   `GetMaxSpellPoints()` 读**有效**知识（含神器），所以永远是 10 的倍数且不小于 10。
7. **卷轴法术的缺口写成断言，不写成注释**：`Artifact::getSpellId()` 在 `ADD_SPELL` 为 `NONE` 时回落到
   神器的 `ext` 字节（卷轴的每实例载荷），而 `ArtifactBag` 是平铺 id、没有载荷。所以端口里一张卷轴
   什么都不加，只有 `BATTLE_GARB` 那种**表里写死**的 `ADD_SPELL` 才进得来；
   `SpellStorageTests.ASpellScrollCarriesNoSpellInThePort` 把这条钉成断言。将来给神器加每实例载荷时，
   这个测试会先红，提醒改这里。
8. **`spellPoints( hero )` 只转一半**：减耗要 `BagArtifacts::getTotalArtifactMultipliedPercent`（未转），
   所以端口只保留 `hero == nullptr` 的分支 —— 列表价，注释里点明，不编造公式。

**后果**：`Spells.SpellStats`/`SpellTable`/`Spell`/`SpellStorage`；`Heroes.Hero.Spells`（`MagicBook`、
`GetAllSpells`、`HasSpellBook`、`HasSpell`、`SpellPoints`/`SetSpellPoints`/`GetMaxSpellPoints`、
`GetMagicStrategicValue`）；`Army.GetStrength` 的魔法项生效。`SpellTests` 37 条、`SpellStorageTests` 9 条、
`HeroSpellsTests` 10 条。`FHeroes2.Game.Tests` 1142 → **1198**，全解决方案 1828 → **1884** 条全绿。
`spell` 行 0/1/3 → **1/1/2**，合计 26/57/166 → **27/57/165**。

## ADR-103：两种强度并存；补给的「价值」不是补给的强度

**背景**：ADR-095 起 `Army` 只会塞兵、取兵、清空与算强度。参考的 `Troops`（`army.cpp:324-700`）还有
一整层容器操作：去重、优化、排序、找最弱、算补给价值、比强弱、判断肉搏军、取最强兵种、修坏槽。
AI 的合兵与英雄交换界面全依赖这层，这一批搬过来。

**决策**：

1. **端口没有 `Troops`，只有五格，容器操作分两种写法**：`MergeSameMonsterTroops`/`MergeSameMonsterOnce`/
   `SortStrongest`/`ResetInvalidMonsters` 原地改槽（参考也是原地）；`GetOptimized` 与
   `GetReinforcementValue` 真的需要动态容器，临时建 `List<Troop>` 并放**副本**，调用方拿到的东西改不
   回去。不为了对齐参考而凭空造一个 `Troops` 类 —— 那样每个 `Army` 都得在两个容器之间同步。
2. **`Troops::GetStrength()` 与 `Army::GetStrength()` 是两个方法，不是一个 flag**：前者只是兵的强度之和，
   后者还要乘士气/幸运因子并加英雄攻防、箭术与魔法值。参考在 `getReinforcementValue` 的注释里明说
   合并后的容器里是 `Troop` 而不是 `ArmyTroop`，「计算将只基于兵种强度（不含英雄加成）」。端口用
   `GetTroopsStrength()` 表达这件事，并用测试钉住两者不相等（单种族军队 25.42 vs 24.4，差的是 25/24
   的士气因子）。
3. **`getReinforcementValue` 必须实现成「合并后相减」而不是「补给的强度」**：五个强槽 + 一个弱补给的价值
   是 0；五个弱槽 + 一条龙的价值是「龙 − 被挤掉的最弱兵」，小于龙本身。两条测试分别钉住这两种反直觉
   结果。若实现退化成加法，第三条测试（同兵种补给等于自身强度）仍会通过 —— 所以必须三条都在。
4. **排序不稳定是照抄，空槽显式垫底**：参考用 `std::sort`，端口用 `Array.Sort`，均不稳定，测试只断言
   降序与多重集合。空槽强度为 0，与「一个零强度槽」会打平，因此比较器显式把无效槽排到最后，而不是
   依赖 0 的比较结果。
5. **`GetStrongestMonster` 按单兵强度，不按整堆强度**：参考写的是
   `troop->GetMonster().GetMonsterStrength()`，一千个农民（整堆更强）也输给一只绿龙。调用方问的是
   「这支军队派出了什么兵」，用于战斗与 AI 的法术选择，所以照抄。
6. **`IsStrongerThan` 对空目标返回 `true`**：参考在比较数值之前就 `return true`，于是空军队对空军队也是
   「更强」。测试显式钉住「空目标恒胜」，避免后人当成笔误改成 `false`。
7. **`IsMeleeDominantArmy` 补齐 `Troop.IsFlying`**：判定是「既不是射手也不会飞的兵」对「射手或飞行兵」，
   按强度加权而不是按数量 —— 一条龙顶三十个农民。

**后果**：`Army` 改为 `partial`，新增 `src/FHeroes2.Game/Army/Army.Troops.cs`（13 个成员）；
`Troop.IsFlying`；`ArmyTroopsTests` 19 条。`FHeroes2.Game.Tests` 1198 → **1217**，
全解决方案 1884 → **1903** 条全绿。覆盖率不变：`army.cpp` 仍是「部分」，合计 27/57/165。

**仍未转**：`JoinStrongest`/`MoveTroops`/`SwapTroops`（两军之间搬兵，要 `IArmyCommander.isHeroes()`）、
`UpgradeMonsters`（要 `Monster::GetUpgrade()`）、`GetJoinSolution`、`ArrangeForCastleDefense`/
`ArrangeForWhirlpool`/`ActionToSirens`（地图对象）、城堡与神器两项强度修正。

## ADR-104：`IsHeroes` 进接口；「五个最强」与「英雄至少留一堆」都是可测不变量

**背景**：ADR-103 把 `Troops` 的容器半边搬完了，但两军之间的调兵（`JoinStrongest`/`MoveTroops`/
`SwapTroops`）还卡在一个没有的问题上 —— 参考靠 `commander->isHeroes()` 决定「这支部队能不能被清空」，
而端口的 `IArmyCommander` 只回答强度相关的四件事。这一批补上这一问，并把调兵落地。

**决策**：

1. **`IsHeroes` 进 `IArmyCommander`，而不是用 `Commander is Hero` 判断**：前者是参考的语义
   （`GetType() == HEROES`），`Captain` 落地时只要实现接口；后者在 `Captain` 出现的那天会悄悄把
   captain 当成 hero，而错误的方向恰好是「让英雄的军队被清空」—— 输掉游戏的那种错。接口里因此有五个
   成员：四个回答强度，一个回答身份。
2. **`JoinStrongest` 的四趟顺序照抄，不合并**：同槽 → 填空槽 → 五强者交换 → 退回。前两趟是「无损搬运」，
   第三趟才是决策。若把前两趟省掉直接做全局排序，本来能原样搬过去的堆也会被拆开重排，强度相同但
   兵种不同时次序就会跟参考不一致。
3. **第三趟的九步也照抄，包括先并自己的重复**：参考在自己五个槽里先 `MergeSameMonsterTroops()` 腾位置，
   然后对方的强者填空，再「我方最弱 ↔ 对方最强」对调并重排，直到对方最强也不比我方最弱强，剩下的退回。
   这一步的 `assert` 依赖「合并一定能腾出槽」这个前提，端口写成 `Debug.Assert` 并照旧继续。
4. **英雄留兵的三个夹取顺序是有意照抄，包括它产生的怪癖**：先算 `1/20 ÷ 强度 × 数量`，再压到不超过
   一半，最后抬到至少 1。于是**只有一个兵**时会走「压到 0 → 抬到 1」，整堆还给英雄、接收方变空。
   看起来像 bug，但它是参考的行为，而且「至少留一个」这句话只在第一层夹取之前成立。测试
   `ASingleCreatureOfAHeroIsNotSplitInTwo` 用注释把链条写清楚 —— 后人若想「修」它，会先看到一个
   显式钉住的断言，而不是一个悄悄变化的数值。
5. **`monsterIdToKeep` 是换位不是禁止**：先把接收方的保留兵种与给出方的一堆别的兵交换，把要留的兵种
   全集中到给出方，然后按「先别的兵种、后保留兵种」两趟搬。第二趟有位置就照样把保留兵种搬走 ——
   参考的注释写的是「try to move as much as we can」，即保留是愿望不是承诺。两条测试分别钉住
   「接收方满时真的留下」与「有空位时照样搬走」，否则读者会以为它保证不搬。
6. **`Assign` 只拷五格**：参考的 `Assign` 是 `Troops` 的方法，`SwapTroops(Army)` 用它，因此种族、颜色、
   指挥官都不参与交换。端口不许 `SwapTroops` 顺手交换种族 —— 那会让「换兵」变成「换英雄」，测试
   断言换完之后 `Race` 不变。
7. **参考的 `assert` 一律用 `Debug.Assert`，不抛异常**：和端口别处一致。调兵发生在界面主路径上，
   参考在 Release 里继续跑，端口也不在半路抛异常；只有 `MoveTroops`/`SwapTroops` 的「自己跟自己」
   改成 `ArgumentException` —— 那是调用方的编程错误，且参考同样 `assert` 拒绝。

**后果**：`IArmyCommander.IsHeroes`、`Hero.IsHeroes`、`Army.Transfer.cs`（`JoinStrongest`、
`JoinStrongestFromArmy`、`MoveTroops`、两个 `SwapTroops`、`SaveLastTroop`、私有 `AssignSlots`/
`MoveTroopsInto`/`SortWeakestFirst`）；`Army.Troops.cs` 的「不在本文件」段落改写。
`ArmyTransferTests` 19 条。`FHeroes2.Game.Tests` 1217 → **1236**，全解决方案 1903 → **1922** 条全绿。
覆盖率不变：`army.cpp` 仍是「部分」，合计 27/57/165。

**仍未转**：`UpgradeMonsters`（要 `Monster::GetUpgrade()`）、`SplitTroopIntoFreeSlots`/
`AssignToFirstFreeSlot`/`JoinAllTroopsOfType`/`addNewTroopsToFreeSlots`（要拖单堆的界面）、
`GetJoinSolution`（要中立军与地图）、`ArrangeFor*`、城堡与神器两项强度修正。

## ADR-105：升级关系是一张 21 行的表；「能不能升级」不另立表

**背景**：`Army` 会合兵、搬兵、换兵了，但「把这堆兵换成升级版」还缺参照表。参考用
`Monster::GetUpgrade()`（`monster.cpp:285-336`）的 21 条 `case` 回答这个问题，`GetDowngrade()` 是它的
镜像，`GetUpgradeCost()` 是差价的函数。这一批把这三样连同 `Troop::Upgrade`、`Troops::UpgradeMonsters`
一起落地。

**决策**：

1. **表只有 21 行，按 `case` 写，不摊成 66 行的数组**：`MonsterStatistics` 那两张数组是「每兵一行、按 id
   索引」，因为它们真的每个兵都有值；升级表绝大多数是「自己」，摊开会让 45 行 `X => X` 把 21 行有效信息
   淹掉。用 `switch` 表达式，`_ => monster` 兜底，行数与参考的 `default: break;` 对应。
2. **`GetDowngrade` 是第二张表，不是反查**：龙是唯一的两级链（绿 → 红 → 黑），反查会把红龙判成两种身份的
   冲突（它既是绿龙的升级，也是黑龙的降级），而且反查是 O(n) 的嵌套。参考写两张表，端口也写两张，并用
   一条整表测试钉住 `GetDowngrade(GetUpgrade(m)) == m`。
3. **`IsAllowUpgrade` 照参考写成 `GetUpgrade(m) != m`，不另立「可升级名单」**：两张表迟早会不一致，而
   不一致的表现是「升级按钮点了没反应」，属于最难查的一类。写成一个派生判断，不一致在定义上就不存在。
4. **升级只改 id**：`Monster::Upgrade()` 是 `id = GetUpgrade().id`，端口照做 —— `Troop` 只有兵种与数量，
   所以数量天然保留。连升两次沿链条走一步，链顶再升是空操作（黑 → 黑），不是异常。
5. **`GetUpgradeCost` 用差价的两倍，资源也参与**：`(upgr.GetCost() - GetCost()) * 2`。两条具体价格的测试
   （弓箭手 100 金、红龙 → 黑龙 1000 金 2 硫）把「金币与资源一起算」这件事钉住 —— 只测金币会让只算金币的
   实现在龙身上悄悄少收硫磺。无升级时返回空 `Funds`，于是累加费用的调用方不必先判空。
6. **`GetUpgradeCost` 公开，但保留参考的警告**：参考里它在 `Monster` 上是 `protected`，公开的门是
   `GetTotalUpgradeCost`。端口两个都公开（没有继承边界可放），注释里保留「先问 `IsAllowUpgrade`」的叮嘱，
   因为对一个没有升级的兵种来说「免费」是误导性答案。
7. **`UpgradeTroops( Castle )` 不转，并写明原因**：它比 `UpgradeMonsters` 多问两件 —— 城堡种族是否与兵
   相同、城堡是否建了升级后的住所 —— 都要 `Castle`。这条写进 `Army.Troops.cs` 的文件注释，避免被当成
   遗漏。
8. **测试用整表不变量，而不是 21 条点断言**：遍历所有真实兵种，检查「升级后种族不变、等级不变、更强、
   更贵、降级回原」，再按种族数一遍 5/3/3/3/3/4。点断言只证明「弓箭手是游侠」，扫一遍才证明「第 17 行
   没写错」。

**后果**：新增 `src/FHeroes2.Game/Monsters/MonsterUpgrades.cs`；`Troop` 加 5 个成员；
`Army.UpgradeMonsters`；`Army.Troops.cs` 与 `Troop` 的文件注释更新；`MonsterUpgradeTests` 10 条。
`FHeroes2.Game.Tests` 1236 → **1246**，全解决方案 1922 → **1932** 条全绿。覆盖率不变，合计 27/57/165。

**仍未转**：`UpgradeTroops`（要 `Castle`）、`SplitTroopIntoFreeSlots` 一族（要拖单堆的界面）、
`GetJoinSolution`、`ArrangeFor*`、`Monster::GetRandomMonster`/`GetDwellingFromMonster`（随机与住所反查，
属地图/城堡一侧）、城堡与神器两项强度修正。

## ADR-106：建筑是位掩码；住所位回到城堡；两张表互相验证

**背景**：`UpgradeTroops`、招募、建造、地图上的城堡对象全都等 `Castle`，而 `Castle` 的第一步不是那个类，
是建筑本身的元数据：它是什么（位掩码）、多少钱、要什么前置、升级成什么、是不是住所。这一批只做表，
不做状态，因此不需要 `Castle` 也能落地，也才能整表校验。

**决策**：

1. **`BuildingType : uint`，不是 `int`**：`DWELLING_UPGRADE7` 是 `0x80000000`，超了 `int` 的正数范围。
   参考的 C++ 枚举靠实现定义才没出事，端口的 `uint` 把这个坑堵死。
2. **`Well2` / `Spec` 保持参考的缩写**：这两个名字看着难看，但它们唯一的作用是能 grep 到 `BUILD_WEL2` /
   `BUILD_SPEC`。注释里写明它们实际是什么（地标建筑 / 特殊建筑，逐族不同），比改名有用。
3. **住所位从 `Monsters.Dwelling` 搬进 `BuildingType`**：ADR-095 时城堡模块不存在，住所位临时寄在怪物
   一侧，注释里预言了这次搬迁。现在 `Dwelling` 类删除、`DwellingMonsters.FromDwelling` 收 `BuildingType`、
   调用点改名字。留着两份常量是迟早对不上的那种债。
4. **造价表逐行照抄，不做「聪明」合并**：六行同价的 `Well2` 本来能压成一行全种族，但 86 行转录唯一的
   质量保证就是能和参考并排对照；一旦合并，对照就断了，而下一批人不会再重新对一遍。顺序也照抄。
5. **前置表写成 `(building, race)` 元组 switch**：参考是「建筑 switch 里嵌种族 switch」，形状一样但端口
   的平面写法让每个种族共用一处 `or` 模式（骑士和野蛮人的第五兵营前置相同）。答案仍是掩码 —— 一条前置
   可能同时要求三座兵营。
6. **`MapRace.Random` 的两处矛盾行为都保留**：前置表对它答「无」（还没分到种族，参考的 `default` 落到
   这里），升级表却给它一整套升级线（含第二段龙）。看起来不一致，但改掉任何一边都会让「随机城在分到
   种族前可建造集合」跟参考不同。测试把两条都钉住。
7. **用整表不变量代替点断言**：`EveryDwellingATownCanReachIsPricedAndNothingElseIs` 先沿升级链求可达
   集合，再断言「可达 ⟺ 有价且非零」，多一行少一行都会炸；`EveryPrerequisiteIsItselfBuildableInThatTown`
   断言前置不能要求本城造不出来的东西。86 + 60 行的表，靠肉眼是数不完的。
8. **让城堡的升级表和怪物升级表互相对齐**：`TheCastleUpgradesAndTheMonsterUpgradesAgree` 检查
   「住所升级后的兵 == 升级前住所里那个兵的升级」，并数出 21 —— 与 ADR-105 的 21 行严丝合缝。这不是
   重复断言，是两个独立转录的交叉验证：任何一边错一位，这条就会红。

**后果**：新增 `src/FHeroes2.Game/Castle/BuildingType.cs`、`Castle/CastleBuildings.cs`、
`tests/CastleBuildingsTests.cs`；`Monsters/DwellingMonsters.cs` 删掉 `Dwelling` 类并换参数类型；
`Army.cs` 与 `ArmyTests.cs` 的调用点改名。`FHeroes2.Game.Tests` 1246 → **1260**，全解决方案
1932 → **1946** 条全绿，0 警告 0 错误。`castle` 行 0/0/11 → **0/3/8**，合计 27/57/165 → **27/60/162**。

**仍未转**：`Castle` 本体（种族、`building` 位、`isBuild`/`isFort`/`CheckBuyBuilding`、招募、税收与周产）、
`BuildingInfo` 界面构件、建筑名与描述（译文）、图标矩形表（AGG）、`castle_heroes`/`castle_recruit`/
`castle_masons_guild`/`castle_switch`/`castle_ui`。

## ADR-107：城堡先做「不需要世界」的一半；判定顺序按参考的行序排

**背景**：`castle.cpp` 2500 行，其中招募要 `Kingdom` 的钱、周产要世界的一周类型、税收要天数、界面要
AGG 与渲染，`Scout()` 要迷雾、`ActionAfterBattle` 要战斗。如果等「世界齐了再做城堡」，那么 `UpgradeTroops`、
地图装载的城堡表、招募会一起卡住，而它们之间并没有真正的循环依赖 —— 卡的只是「全部做完」这个想法。

**决策**：

1. **按「需不需要外部状态」切，而不是按文件切**：这批只做 `Castle` 自己闭合的那一半 —— `building` 位掩码
   与它的后果（`IsBuild`/`IsCastle`/`CountBuildings`/`GetBuildingValue`/公会等级）、六个住所货架、每天的
   建造许可、买与不能买的理由。凡是要世界/王国/英雄/渲染的，一个不碰，文件头逐条写明缺哪一块，避免
   后人以为漏了。
2. **三个全局改成参数或属性，而不是门面**：`Kingdom::AllowPayment()`/`OddFundsResource()` → `in Funds
   treasury` 参数；`Settings` 的地图版本 → 构造参数（城一建成，版本就不会变）；`HasSeaAccess()` → 可写属性
   （它扫周围瓦片，要已装载的地图）。前两个王国落地后调用点不用改；第三个由装载方回答。对照 ADR-057 的
   「全局用 `static X Current` 门面照抄」：这条只适用于**本批就存在**的全局，不适用于「为避免参数化而先造
   一个空门面」。
3. **判定顺序逐条照排，不重排成「更合理」的样子**：`CheckBuyBuilding` 九道判断的优先级完全由参考的书写
   顺序决定：已建 → 禁用位 → 船坞/神龛/酒馆三个特例 → 公会逐级 → **今天能否建** → 帐篷/城堡规则 → 种族
   特有升级 → 前置位 → 资源。测试逐条钉「谁先回答」，因为一旦把公会逐级挪到「今天」之后，同一座城在
   两个不同状态下会给出不同的理由 —— 界面文案跟着变，这是最难靠肉眼发现的一类偏差。
4. **`AlreadyBuilt` 判「任一位」，`IsBuild` 判「全部位」**：参考里前者是 `_constructedBuildings & build`、
   后者是 `& build == build`。组合掩码（`MageGuild`/`DwellingMonsters`/`DwellingUpgrades`）让这个差别
   变成真实行为：问「公会建了吗」只要有一级就算，问「三级公会建了吗」必须三级都在。两个函数谁也不替代谁，
   注释里写清。
5. **`IsExactBuildingBuilt` 单独存在**：升级的前置是「确切那一级」（要二级住所才能升三级住所），而
   `IsBuild` 对组合掩码是「全部」。参考另写一支正是为了这个，端口照抄，没有折进 `IsBuild`。
6. **`BuyBuilding` 用 `ref Funds`，扣款后 `Trim()`**：参考的 `OddFundsResource()` 是 `operator-=` 之后
   `Trim()`（不会变负）。`Funds` 已是结构体，`ref` 传参就是它的自然形态；用返回值会迫使调用方两处赋值，
   与参考的「就地扣」不一致。
7. **住所是货架，升级不重填**：`_getDwelling()` 把原住所与升级映射到同一格，第六格回答三个名字。因此
   `GetMonstersInDwelling` 对链上任意一级都给同一个数，而 `BuyBuilding` 的填货分支只匹配**原住所**。
   照抄这个不对称，否则会多出「升完级又满货一次」的行为。
8. **周产不另立表**：`MonsterStatistics.GetGeneralStats(...).BaseGrowth`。ADR-095 的住所映射已经把「哪个
   建筑出哪种兵」定好了，这里只借它取数。
9. **掷点走可注入的 `Pcg32`**：`SetDefaultBuildings` 的第二住所按难度掷（75/50/25/10/0），默认走 `Rand`
   门面，测试传固定生成器。与 `Army.Reset` 是同一道缝 —— 需要可复现的地方就该有这道缝，而不是在测试里
   容忍随机。
10. **命名空间与类型撞名用别名，不改名**：`FHeroes2.Game.Castle` 里放 `Castle`，`FHeroes2.Game.Army` 里
    放 `Army`。源文件用 `using ArmyTroops = FHeroes2.Game.Army.Army;`，测试用
    `using CastleTown = FHeroes2.Game.Castle.Castle;`。改名（如 `Town`）会让所有注释里指向
    `Castle::CheckBuyBuilding` 的引用失准，而这两处别名各只出现一次。

**后果**：新增 `src/FHeroes2.Game/Castle/Castle.cs`、`Castle/BuildingStatus.cs`、
`tests/CastleTests.cs`。`FHeroes2.Game.Tests` 1260 → **1277**，全解决方案 1946 → **1963** 条全绿，
0 警告 0 错误。`castle` 行 0/3/8 → **0/4/7**，合计 27/60/162 → **27/61/161**。

**仍未转**：招募（`RecruitMonster`/`getRecruitLimit`/`getAvailableArmy`/`AllowBuyHero`）、`ActionNewWeek`/
`ActionNewMonth`（周产与井/水井/周之建筑/月之建筑的加成）、税收与法师公会的法术授予、
`Castle::LoadFromMP2`（70 字节块）与 `_postLoad()` 的填货尾巴、`GetPowerModificator`/`GetMoraleModificator`/
`GetLuckModificator`/`GetGarrisonStrength`、`HasBoatNearby`/`AllowBuyBoat`/`BuyBoat`、`Scout()`、
`ActionPreBattle`/`ActionAfterBattle`、`DrawImageCastle`/`GetICNBuilding`、`setName`/`fixFrenchCharactersInName`、
`getVisitValue`/`getArmyRecruitmentValue`；以及 `castle_heroes`/`castle_recruit`/`castle_masons_guild`/
`castle_ui`/`castle_switch`/`castle_building_info` 的其余部分与 `BuildingInfo` 界面构件。

## ADR-108：招募照抄参考的钳制不对称；住所反查另立一张表

**背景**：上一批给了货架与建造，这批把货架上的兵卖出去（`Castle::RecruitMonster`/`getRecruitLimit`），并把
「用钱换升级版」接上（`Troops::UpgradeTroops`）—— 后者从 ADR-107 起就只差一座能问「建没建那个升级住所」的
城。两处都还不需要世界，所以继续不碰招募对话框与英雄。

**决策**：

1. **`RecruitMonster` 的钳制不对称照抄**：参考把 `count` 钳到货架存量，却用**原始**数量算价钱、
   `JoinTroop`**原始**数量。要 20 只剩 10 → 付 20 的钱、进 20 个兵、货架归零。这是参考的真实现状（对话框永远
   先问 `getRecruitLimit()`，所以平时看不见），端口不"顺手修好"：修了以后移植的对话框会与原版行为不同，而
   这类偏差在界面上极难被发现。测试里写一条 `AskingForMoreThanTheShelfHolds...` 把它钉住，注释说明责任在
   调用方。
2. **`getLowestQuotient` 的符号回绕照抄**：钱为负（事件扣成负）时，`uint32_t willRecruit = int32_t` 绕成
   巨大值，于是「货架有多少就能买多少」。端口写 `unchecked((uint)...)` 显式表达"我知道这里会绕"，而不是加
   一道参考没有的钳位。
3. **失败的判定全部在扣钱之前**：无效兵种 → 不是住所兵种 → 钱不够 → 两边都放不下。参考的 `return false`
   都在 `kingdom.OddFundsResource()` 与 `_dwelling -= count` 之前，端口保持这个顺序，于是"没地方放"既不花钱
   也不减货 —— 一条测试同时验证三件事。
4. **守军优先、英雄其次，英雄以参数传入**：参考写 `GetArmy()` 再 `world.GetHero( *this )`；世界里没有英雄
   系统，所以第三个参数是 `ArmyTroops? heroArmy`。默认 `null` 表示"城里没英雄"，调用方（装载后的地图）传
   `hero.Army`。这与 ADR-107 把 `Kingdom` 的账本变成参数的思路一致：缺的东西以参数出现，不造假门面。
5. **住所反查另立一张表**（`DwellingMonsters.GetDwelling`，对应 `Monster::GetDwelling()`）：不要在第一张表
   （`FromDwelling(race, building)`）里搜。第一张表按种族回答，第二张表按兵种回答；兵种的住所只取决于它自己
   （弓手永远在第二格），而"第三格"在骑士城是精灵、在术士城是石像鬼。两张表彼此的约束用测试覆盖：六族 ×
   12 个住所位，反查回来必须落在同一货架。
6. **`Troop.GetDwelling()` 只做转接**：参考里 `Troop` 继承 `Monster`，`troop.GetDwelling()` 就是
   `Monster::GetDwelling()`。端口没有继承，加一个一行转接，让 `Castle`/`Army` 的代码读起来与参考同形。
7. **`UpgradeTroops` 问的是升级后的住所**：`castle.isBuild(troop.GetUpgrade().GetDwelling())` —— 升级住所
   本身就是"允许升级"这个能力的载体，问当前兵种的住所会让"有二级住所就能把三级升四级"漏进来。
8. **升级逐堆付费**：每堆单独 `AllowPayment` + 扣款，所以钱只够一堆时，靠前的升、靠后的不动。若改成先算总价
   再逐堆判断，行为会变成"钱不够就一个都不升"，与参考不同。

**后果**：新增 `tests/FHeroes2.Game.Tests/CastleRecruitTests.cs`（14 条），改动
`src/FHeroes2.Game/Castle/Castle.cs`（`GetRecruitLimit`/`RecruitMonster`）、
`src/FHeroes2.Game/Monsters/DwellingMonsters.cs`（`GetDwelling`）、
`src/FHeroes2.Game/Army/Troop.cs`（`GetDwelling`）、`src/FHeroes2.Game/Army/Army.Troops.cs`
（`UpgradeTroops`）。`FHeroes2.Game.Tests` 1277 → **1291**，全解决方案 1963 → **1977** 条全绿，0 警告
0 错误。`castle`/`army`/`monster` 都已是「部分」，行数与合计不变（27/61/161）。

**仍未转**：`AllowBuyHero`、`Castle::getAvailableArmy`/`getRecruitLimit` 的对话框侧用法
（`castle_recruit.cpp`）、`Castle::ActionNewWeek`/`ActionNewMonth`（周产与井/水井/周之建筑/月之建筑的加成）、
税收与法师公会法术授予、`Castle::LoadFromMP2` 与 `_postLoad()` 的填货尾巴、`Castle::GetPowerModificator`
等修正、`Troops::SplitTroopIntoFreeSlots`/`ArrangeForBattle`/`ArrangeForTownDefense`、
`Troops::GetJoinSolution`、`joinTroops`/`JoinTroop` 的"同族三连"变体，
`Troops::GetAttackModificator`/`GetDefenseModificator`（要城堡与神器）。

## ADR-109：70 字节块照顺序读；「读」与「修」分成两个函数

**背景**：MP2 的城堡块固定 70 字节，字段没有长度前缀。参考把它拆成 `LoadFromMP2()`（读）与 `_postLoad()`
（读完再修六步）。上一批的 `Castle` 已经有全部建筑位与住所货架，这批只是给它们一个来自文件的入口。

**决策**：

1. **固定宽度、顺序读、不猜长度**：每个跳过都写死字节数（非自定义建筑跳 5、非自定义守军跳 15、城名 13、
   尾部 29 字节不读）。格式如此，写死比"读到下一个字段"更安全，也用 `data.Length != 70` 直接拒绝畸形块。
2. **`LoadFromMP2` 与 `PostLoad` 分开**：读字段是机械的，修六步全是"文件说不清"的判断（随机城的种族是后定的、
   升级位可能不属于该种族、无主城要自己长兵、无水就没有船坞）。合成一个函数会让每步修复看起来像解析的一部分；
   分开之后，`PostLoad` 的六步顺序本身就是文档。
3. **`Race` 允许装载时确定**（`private set`）：参考的构造函数拿位置记录里的种族，随机城的真实种族要等块读到
   最后才定，端口因此不能让 `Race` 只读。这是本批唯一放松的既有契约，写在这里备案。
4. **世界缺席处一律加参数**：`GameDifficulty`（默认建筑的掷点）、`MapRace? ownerRace`（随机城有主时用王国
   种族）、`Pcg32?`（掷点与补兵可复现）、`uint dayCount`（中立城补兵按天数升级）。没有为任何一个造门面 ——
   与 ADR-107 的结论一致。
5. **兵种 id 是「枚举值 - 1」**：格式把 0 留给"没有"，读出来 `+ 1`。这条很容易写错（本批测试第一版就写错了），
   所以常量与 `+ 1` 都写在字段旁边，测试也用「Swordsman 而非 VeteranPikeman」这种能立刻暴露差一的断言。
6. **`Army.Assign` 是新的一支，不复用 `JoinTroop`**：地图给的是五个独立槽位，同种兵可能是两格；`JoinTroop`
   会合并、会挑空位，语义相反。Assign 先清空再按位放，跳过无效堆但保留位置。
7. **`HasCustomArmy` 作为模式位的替身**：参考用 `Modes(CUSTOM_ARMY)`，读它的只有 `_postLoad()` 里"要不要给
   中立城补兵"一处，所以端口落成一个只读属性，由装载置位。
8. **`HasSeaAccess` 仍由调用方先说**：块里可能写着船坞，而参考在 `_postLoad()` 里按地图扫水拆掉它。端口的
   属性默认 `false`，因此文档明确要求"先设水，再装载"，否则船坞必被拆。这条是 ADR-107 那条「世界给答案」
   的直接后果，写清楚免得后被当成 bug 修。
9. **两步没转就在代码里点名**：船长的默认值/法术点与 `_trainGuestHeroAndCaptainInMageGuild`。不悄悄跳过：
   "有船长建筑却没有船长"是最难排查的半截状态。

**后果**：改动 `src/FHeroes2.Game/Castle/Castle.cs`（`Mp2BlockSize`、`HasCustomArmy`、`LoadFromMP2`、
`PostLoad`、`FillShelf`、`JoinRandomArmy`、`Roll`、`ReadUInt16`，`Race` 改 `private set`）、
`src/FHeroes2.Game/Army/Army.Troops.cs`（`Assign`），新增
`tests/FHeroes2.Game.Tests/CastleLoadTests.cs`（19 条，全部手搓 70 字节块，不依赖地图文件）。
`FHeroes2.Game.Tests` 1291 → **1310**，全解决方案 1977 → **1996** 条全绿，0 警告 0 错误。行数与合计不变
（27/61/161）。

**仍未转**：`world_loadmap.cpp` 的城堡落格与其 3 字节位置记录、`Castle::saveToMP2`/`Game::Save` 的城堡段、
默认城名（`setName` + 72 个名字的 `updateCastleNames`）、船长与法师公会的法术、`ActionNewWeek`/
`ActionNewMonth`、税收、`AllowBuyHero`/`AllowBuyBoat`/`BuyBoat`、`Scout()`、`ActionAfterBattle`、
`DrawImageCastle`/`GetICNBuilding`。（城堡落格与默认城名已在 ADR-110 补上。）

## ADR-110：城表独立成类；装载顺序「建城 → 读块 → 起名」；随机城的两步重绘照抄参考

**背景**：ADR-109 让 70 字节块有了入口，但块找不到自己的城——参考里城表（`vec_castles`）是 `World` 的成员，
城堡块按「它站在哪个瓦片上」反查。这批把 `world_loadmap.cpp:296-355`（3 字节位置表建城）、`:596-641`
（两种城堡块）、`maps.cpp:717-819`（随机城的两次重绘）与 `:92-111`（`updateCastleNames`）接进装载流程。

**决策**：

1. **城表独立成类 `AllCastles`**：参考的 `AllCastles` 就是一个按瓦片索引反查的容器（`vec_castles` 的
   `Add`/`Get`/`Remove`），并不是 `World` 的方法集合。端口照此独立成类，构造时接收地图宽度，`Add` 一个城
   就把它的 17 格足迹（正中 + 城堡的 16 格，不含城门下方两行）全部登记。装地图与 `Reset` 时**替换**整个表
   而不是清空它——表记着地图宽度，清空后的旧宽度会让坐标换算错位。
2. **装载顺序写死在 `LoadMap` 里**：`Width/Height` → 瓦片 → **`CreateMapCastles` → `ReadInfoBlocks` →
   `UpdateCastleNames`**。三条都不能挪：块按瓦片找城，所以城必须先存在；默认名要避开地图自己起的名字，
   所以起名必须等所有块读完。参考也是这个顺序，端口把它写成注释而不是留给读者自己推。
3. **`HasSeaAccess` 挂到 `World` 上**：参考里它是 `Castle::HasSeaAccess()`，靠全局 `world` 探三个水格。
   端口的城堡不持有世界（ADR-107 的结论），所以扫描落成 `World.DoesCastleHaveSeaAccess(tileIndex)`，
   建城时把答案写进 `Castle.HasSeaAccess`，正好满足 ADR-109 要求的「先设水，再装载」。
4. **重绘分两遍，顺序不能反**：先 `UpdateCastleSprite`（换精灵：`OBJNTWRD` → `OBJNTOWN`/`OBJNTWSH`，顶格
   再手工重指 `fullTown - 16`），再 `ReplaceRandomCastleObjectId`（改类型）。反过来改类型会让重绘前的
   「这格是不是随机城」检查落空。第二遍的扫描框 `x ∈ [-2, 3)`、`y ∈ [-3, 2)` 照抄参考，**城门最后无条件
   设成 `Castle`**——框内的左向阴影不在框里，参考也不管它们。
5. **`ReplaceObject` 要求精确匹配，所以重绘不会「凭空生效」**：它按 UID + ICN 类型 + 图像索引三者匹配。
   这只在整座城的 32 个精灵都在地图里时才工作，测试地图因此必须真的铺满 32 格（`WorldCastleLoadTests` 的
   `PaintRandomTown` 就是这么做的，城门格用布局里的第 13 号索引而不是 0）。这条坑很隐蔽，写在这里备查。
6. **两个名字表的分工**：`Castle.DefaultNames` 是静态的 72 个名字（`castle.cpp:84-101`），`Castle.SetName`
   接一个「已占用名集合」，洗牌后取第一个没被占的。已用集合由 `World.UpdateCastleNames` 维护，因为参考用的
   是全局城表；全部占满时参考 `assert(0)`，端口抛 `InvalidOperationException`。
7. **畸形地图一律 warn + 跳过，不 assert**：城的位置越界（参考会照建一个越界的城）、块长不是 70、
   块找不到城、随机格不是随机城——四种都是地图缺陷，参考前两种不管、后两种 `assert`。端口统一
   `GameLog.Warn` 后跳过，理由是这三种情况都不该让装载整体失败，而未初始化的城更难排查。

**后果**：新增 `src/FHeroes2.Game/Castle/AllCastles.cs`、`src/FHeroes2.Game/World/World.Castles.cs`
（城表、两个查询、`DoesCastleHaveSeaAccess`、`CreateMapCastles`、`UpdateCastleNames`、`ReadCastleBlock`、
`ReplaceRandomCastleObjectId`、`UpdateCastleSprite`），改动 `Castle.cs`（`DefaultNames`、`SetName`）、
`World.cs`（城表生命周期、装载顺序、`ReadInfoBlocks` 的四个城堡分支），新增
`tests/FHeroes2.Game.Tests/WorldCastleLoadTests.cs`（12 条）。`FHeroes2.Game.Tests` 1310 → **1322**，
全解决方案 1996 → **2008** 条全绿，0 警告 0 错误。行数与合计不变（27/61/161）。

**仍未转**：`Game::Save` 的城堡段与 `Castle::saveToMP2`、`Kingdom`（资源簿与名册，随机城有主时的种族要问它）、
`ActionNewWeek`/`ActionNewMonth`、税收、船长与法师公会的法术、`AllowBuyHero`/`AllowBuyBoat`/`BuyBoat`、
`Scout()`/`ActionAfterBattle`、`DrawImageCastle`/`GetICNBuilding`。


## ADR-111：王国只做「账本 + 名册」；七槽容器照抄第七槽；起始资源按文件头算

**背景**：ADR-107 的城堡接口吃 `ref Funds treasury`，ADR-110 又提到随机城有主时要问王国的种族，但 `Kingdom`
一直没有——账本没有主人，名册没有名字。这批把 `kingdom.cpp` 里不需要别的系统的那一半搬过来：`Init`/`clear`
（`:103/124`）、两张名册（`:271/284/311/322`）、五个计数（`:344-374`）、`AllowPayment`（`:384`）、收付
（`:443/449`）与起始资源表（`:865`），以及七槽容器 `Kingdoms`（`:688-802`）。

**决策**：

1. **账本用 `ref Funds Treasury` 而不是 `Funds GetFunds()`**：参考的 `GetFunds()` 返回常量引用，城堡的
   `BuyBuilding`/`RecruitMonster` 也吃引用。端口既然已经把「钱」做成 `ref Funds` 参数（ADR-107/108），账本
   就必须交出货真价实的引用，否则下单只会改到副本。属性返回 `ref` 字段，测试里 `ref Funds treasury =
   ref kingdom.Treasury` 直接改字段也成立。
2. **`AllowPayment` 照抄「够，或者本来就要 0」**，不换成 `Funds >= funds`：零需求永远付得起，而 `Funds` 的
   比较运算符对空账本 + 零价格会给出 `false`。这条差异只有零需求时才显现，正是最难靠眼睛发现的那类。
3. **名册只做名册**：`RemoveCastle`/`RemoveHero` 不判负、不解散英雄、不给城改色，`AddCastle` 也不改色——
   参考那三件事分别属于 `LossPostActions`（要 `Players`）、英雄解散（要 `Players`）与占领流程的
   `ChangeColor`（ADR-108 已落）。端口把「出局没有」做成 `IsLoss`/`IsLosingGame` 两个只读属性，由调用方问。
   加城会把 `LostTownDays` 重置回 `GameOverLostDays + 1`，这是参考里唯一的重置点，照抄。
4. **`Kingdoms` 保留第七个「垃圾桶」槽**：参考把非法颜色与 `NONE` 都 `switch` 到第七槽，让调用方永远拿到一个
   合法对象。端口照抄（返回 `Kingdom` 而不是可空引用），因为天量调用点都拿着「可能为 `NONE` 的颜色」来查。
5. **起始资源按地图文件头算，而不是 `Settings`**：参考 `Kingdoms::Init` 从 `Settings` 取在局颜色与电脑颜色；
   装载时 `Settings` 还不存在，而 `Mp2FileInfo` 的 `KingdomColors`/`ComputerPlayers`/`Difficulty` 对这三件事
   说得同样清楚。于是 `World.CreateMapKingdoms` 直接把文件头交给 `Kingdoms.Init`，难度与「是不是电脑」仍是
   `Kingdom.Init` 的参数（沿用 ADR-107 的「世界缺席处一律加参数」）。`Players::HandicapStatus` 的折扣
   （`getHandicapDependentIncome`）百分比表在 `players.cpp`，未搬。
6. **装载顺序：王国先于英雄表，名册最后填**。参考在 `World::Defaults()` 里先 `vec_kingdoms.Init()` 再
   `vec_heroes.Init()`（英雄初始化要问起点资源），名册则在 `world_loadmap.cpp:1443-1445` 才按颜色分类。端口
   把两条都写进 `LoadMap` 并注上参考行号；`Reset` 里 `_kingdoms.Clear()` 而不是替换整张表（王国不记地图宽度，
   与 ADR-110 的城表相反）。
7. **命名空间撞名的处理照 ADR-110 的先例**：类 `Kingdom` 放在 `FHeroes2.Game.Kingdom` 命名空间里，于是
   `FHeroes2.Game` 下的代码写 `Kingdom` 会解析到**命名空间**。测试文件用 `using PlayerKingdom =
   FHeroes2.Game.Kingdom.Kingdom;`，与 `CastleTown` 别名同一手法；`Kingdoms` 没有同名命名空间，不需要别名。

**后果**：新增 `src/FHeroes2.Game/Kingdom/Kingdom.cs`、`src/FHeroes2.Game/Kingdom/Kingdoms.cs`、
`src/FHeroes2.Game/World/World.Kingdoms.cs`，改动 `World.cs`（`Reset` 清王国、`LoadMap` 起王国 + 填名册），
新增 `tests/FHeroes2.Game.Tests/KingdomTests.cs`（22 条）。`FHeroes2.Game.Tests` 1322 → **1344**，
全解决方案 2008 → **2030** 条全绿，0 警告 0 错误。`kingdom` 行 0/4/10 → **0/5/9**，合计 27/61/161 →
**27/62/160**。

**仍未转**：`ActionNewDay`/`ActionNewWeek`/`ActionNewMonth`（周产、税收、井水加成）、`Recruits`（要英雄池与随机
源）、`visit_object` 访问记录（要瓦片与对象类型）、`puzzle_maps`、`GetCountArtifacts`（`Hero` 还没有神器袋）、
`LossPostActions`/`GetLostTownDays` 的倒计时推进（要 `Players`）、瘟疫与投降英雄（`Players`/`Campaign`）、
`getHandicapDependentIncome`（`Players::HandicapStatus` 表）。
## ADR-112：日收入照抄三张表；世界询问改成回调；缺的两项明写成缺

**背景**：ADR-111 的账本只会「收」不会「进」，城堡的购买对话框与 AI 估价都需要「每天进多少」。
参考把这件事拆成两半：`profit.cpp` 的三张表（一座矿/一座建筑/一件神器值多少）与 `kingdom.cpp` 的
`Kingdom::GetIncome()`（把它们加总，再叠加电脑难度加成与人类折扣）。

**决策**：

1. **`profit.cpp` 整篇搬，独立成 `ProfitConditions`**：参考里这三个函数是 `ProfitConditions` 的静态方法，
   与 `Kingdom` 无关；端口照办，因为城堡的「买了这块地能多赚多少」也要用同一张表，而那时手上并没有王国。
   文件从「未转」直接到「已转」——`profit.cpp` 一共就 122 行三个函数。
2. **`Cost` 的字段顺序必须照抄，不能「顺手改正」**：`Cost{ gold, wood, mercury, ore, sulfur, crystal, gems }`，
   水银在矿石之前。把 `FromMine(ORE)` 读成「2 水银」是个看起来很合理的错误，测试逐项钉住七种产出。
3. **未知类型给零**：参考在 `FromMine` 的 `default` 里 `assert`，端口返回空 `Funds`。地图数据坏掉的代价应是
   「今天少赚点」，不是整局崩掉；这条与 ADR-095 的「不带异常的地图数据」同调。
4. **`FromArtifact` 走 `ArtifactTable`，不走「常见的那几件」**：参考遍历奖励与诅咒，只保留八种资源与
   `GoldPenalty`。于是七只无尽袋子、金鹅、三级金袋和税单都自动正确，新增神器也不用改代码。
5. **`GetIncome` 的矿井项改成回调参数**：参考直接问全局 `world.CountCapturedMines( resource, GetColor() )`。
   端口里 `Kingdom` 不认识 `World`（世界反过来认识王国），所以矿井数由调用方以 `Func<int, uint>` 递进来，
   颜色由调用方的闭包带进去。王国因此仍是一块纯数据，测试里可以直接塞一个「金矿三座」的 lambda。
6. **缺的能力明写，不装作支持**：参考的五个来源里，神器收入要英雄的神器袋、技能收入要英雄的技能，两者都没
   挂到 `Hero` 上（`ArtifactBag` 是独立类，ADR-101），电脑难度加成与人类折扣在 `players.cpp`。`IncomeType`
   的位值照抄参考（含 `All = 0xFF`），但 `Artifacts`/`HeroSkills`/`CampaignBonus` 三条在 XML 文档与
   `PROGRESS.md` 里都被标成「今天恒为 0」。当前端口与参考**等价**，因为英雄本来就拿不到神器与技能。
   等它们落地时只补分支，`All` 的语义自动对齐。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ProfitConditions.cs`、`src/FHeroes2.Game/Kingdom/IncomeType.cs`，
`Kingdom.cs` 增 `GetIncome`；新增 `tests/FHeroes2.Game.Tests/KingdomIncomeTests.cs`（14 条）。
`FHeroes2.Game.Tests` 1344 → **1358**，全解决方案 2030 → **2044** 条全绿，0 警告 0 错误。
`profit` 行由「未转」转「已转」：`kingdom` 行 0/5/9 → **1/5/8**，合计 27/62/160 → **28/62/159**。

**仍未转**：`ActionNewDay`（每日入账、事件、井水加成、失城倒计时）、`ActionNewWeek`/`ActionNewMonth`
（周产与周事件，要 `Week` 与 `Settings`）、`getHandicapDependentIncome` 与 `Difficulty::getResourceIncomeBonusForAI`
（`players.cpp`/`difficulty.cpp`）、`Recruits`、访问记录、`GetCountArtifacts`、`LossPostActions`。
## ADR-113：`Week` 照抄两条抽签表；`combineSeedWithValueHash` 落进 Core；`GetName` 等翻译层

**背景**：ADR-112 让王国算得出「一天多少钱」，但「今天是哪一周」还没有类型——`week.cpp` 是 `ActionNewWeek`、
怪物之周的招募增长和建筑/怪物产出的前置。另一方面 ADR-070 的 `rand.cpp` 一直记着「只差
`combineSeedWithValueHash`」，那正是 `week.cpp` 抽签依赖的函数。

**决策**：

1. **`WeekName` 的顺序就是抽签的坐标，不能重排**：参考从 `SQUIRREL` 到 `CONDOR` 之间抽周名、从 `ANT` 到
   `BEETLE` 之间抽月名（`week.cpp:47`、`week.cpp:62`），区间端点就是枚举值。端口因此逐项照抄 34 项顺序，
   并在注释里点名「重排会让抽签结果整体错位」。新增周名只能追加在 `PLAGUE` 之后。
2. **`Week` 是 `readonly struct`**：与参考的值类型语义一致，`new Week()` 落到 `Unnamed`/`Unknown`（两者底层
   都是 0），正好是参考的默认构造。世界将来持有它、在消息里拷贝它，值语义比类更贴合。
3. **一个种子、六个盐，而不是连续调用 `Get()`**：参考对同一个周种子做六次
   `combineSeedWithValueHash( seed, salt )`（367245 / 1946256 定周，9536582 / 5544783 定月，886473 / 1130906
   定怪物）。端口保留这一点，因为它同时保证了「同种子 → 同周」与「周与月的抽取互不污染」——后者在连续抽时
   做不到（抽不到月名时不会再抽怪物）。
4. **`combineSeedWithValueHash` 放进 `Rand` 门面**：参考是 `rand.h` 里的模板，C# 写成两个重载
   （`uint` 与 `int`，后者只是让调用点写 `367245` 而不是 `367245u`）。整型的 `std::hash` 是恒等，
   表达式在 32 位内环绕，两端实现逐位一致。ADR-070 曾把「靠 `std::hash`，跨库不可复现」记为未转的理由；
   那说的是 `std::hash` 对**非整型**（字符串）不保证一致，而本函数的全部调用点只传整型与枚举，所以结论更正。
5. **`Week::GetName()` 不转**：它要翻译层（`_()`）与 `Monster::GetName()`，协议里两者都还没有。`week.cpp`
   因此记「部分」：抽签、类型、怪物全转，只有那几个字没转。
6. **暂不接进 `World`**：`World::NewWeek()` 与 `GetWeekType()` 属于 `world.cpp` 的推进链，那一支还要
   `Settings`、会话种子与招募重置。只落类型与抽签，等 `NewWeek` 整体移植时一起接。另记一个坑：`World` 已有
   一个 `Week` 属性（uint，面板显示的第几周），接线时要么写全名 `FHeroes2.Game.Kingdom.Week`，要么给属性
   改名。

**后果**：新增 `src/FHeroes2.Game/Kingdom/WeekName.cs`、`src/FHeroes2.Game/Kingdom/Week.cs`，
`Rand` 增 `CombineSeedWithValueHash`；新增 `tests/FHeroes2.Game.Tests/WeekTests.cs`（17 条），
`tests/FHeroes2.Core.Tests/RandTests.cs` 加 1 条。`FHeroes2.Core.Tests` 192 → **193**，
`FHeroes2.Game.Tests` 1358 → **1375**，全解决方案 2044 → **2062** 条全绿，0 警告 0 错误。
`week` 由「未转」转「部分」：`kingdom` 行 1/5/8 → **1/6/7**，合计 28/62/159 → **28/63/158**。
PLAN 里 `rand.cpp` 那句「只差 `combineSeedWithValueHash`」同时更正。

**仍未转**：`Week::GetName()`（翻译层 + 怪物名）、`World::NewWeek()`/`GetWeekType()`、`Week::RandomWeek` 的
调用点（`ActionNewWeek` 与 `world.cpp` 的周推进）。
## ADR-114：周类型按需算不缓存；`MapSeed` 留给会话层；每日入账收参数不收世界

**背景**：ADR-113 立了 `Week` 类型，但 `World` 不知道自己处在哪一周，`Kingdom` 也没人告诉它「今天进多少」。
参考把这两件事分别放在 `world.cpp`（`GetWeekSeed`/`GetWeekType`/`GetEventsDate`）与 `kingdom.cpp`
（`ActionNewDayResourceUpdate`），都直接读全局 `world`。

**决策**：

1. **`WeekType` 每次重算，不学参考的函数内静态缓存**：参考把 `( _week, GetWeekSeed() )` 映射到 `Week` 存进
   static，因为重绘时每个瓦片都会问一次。端口算一次是两次哈希加最多两次 `GetWithSeed`，代价可忽略；而静态
   缓存要跨 `Reset` 存活，换地图（`_seed`、`_week` 都归零）时会返回上一局的周——这类 bug 在端口里已经有过
   一次（`_pathfinder`），不值得为省两次哈希再冒一次。
2. **`MapSeed` 的 setter 是 `internal`，且 `Reset` 归零**：参考在 `Defaults()` 里 `_seed = Rand::Get( max )`
   （`world.cpp:291`），`Defaults()` 属于「会话层」（`Settings`、玩家、初始英雄），端口还没有。所以种子只能
   从外部写：装载器、将来的存档、测试。默认 0 的世界仍然完全确定（每台机器同一串周），不会崩，也不会随机。
3. **`WeekType` 的类型名写全**：`World` 已有 `Week` 属性（uint，面板显示第几周），类内会遮蔽
   `FHeroes2.Game.Kingdom.Week`。ADR-113 预判过这个坑，这里按预判写全名，并在 XML 文档里说明原因——比给属性
   改名更小的动作，也保住了 `World.Week` 与参考 `world.h:381` 的对照关系。
4. **`GetEventsDate` 只过滤，不复制事件**：`EventDate` 是不可变的引用类型，参考返回 `std::vector<EventDate>`
   的拷贝；端口返回 `List<EventDate>` 引用同一批对象。顺序保持地图顺序，因为事件对话框与 AI 的记账顺序都和它
   一致。
5. **`ActionNewDayResourceUpdate` 收参数，不收 `World`**：参考在这个函数里问世界三件事（矿数、第几天、当日
   事件）。端口把三件事都变成参数：矿数沿用 ADR-112 的 `Func<int, uint>` 回调、天数用 `uint`、事件用
   `IReadOnlyList<EventDate>`（`EventDate` 在 `FHeroes2.Game` 命名空间，王国本来就能看见），`isAiControlled`
   替代参考的 `GetControl() == CONTROL_AI`（`Players` 未转）。于是王国仍是纯数据，日循环在调用方只有一行。
6. **首日不结算与 AI 门照抄，因为它们是规则不是偶然**：`if ( world.CountDay() > 1 )`（`kingdom.cpp:227`）——
   第 1 天的钱是 `GetStartingResources` 给的；`if ( isAIPlayer && !event.isApplicableForAIPlayers ) continue`
   （`kingdom.cpp:236`）——地图作者可以写只给人类看的事件。两条都有专门的测试。
7. **对话框拿实付金额**：先 `Resource.CalculateEventResourceUpdate` 钳制、`AddFundsResource` 入账，再把
   `fundsUpdate` 交给回调。参考如此，测试也钉住：一件 -1000 的事件在只有 50 金的王国里，对话框报的是 -50。

**后果**：`World.cs` 增 `_seed`/`MapSeed`/`WeekSeed`/`WeekType`/`GetEventsDate`，`Reset()` 清种子；
`Kingdom.cs` 增 `ActionNewDayResourceUpdate`。新增 `tests/FHeroes2.Game.Tests/KingdomDailyUpdateTests.cs`（8 条），
`WorldDateTests.cs` 加 4 条，`WorldSphinxAndEventsTests.cs` 加 3 条（并把事件块助手参数化）。
`FHeroes2.Game.Tests` 1375 → **1390**，全解决方案 2062 → **2077** 条全绿，0 警告 0 错误。覆盖率不动：
`week` 仍「部分」、`kingdom` 仍「部分」，合计仍 **28/63/158**。

**仍未转**：`World::NewWeek()`/`NewMonth()`（要 `Settings`、怪物刷新、招募池）、`Kingdom::ActionNewDay` 的访问
记录与胜负判定（要 `Visit` 与 `Players`）、`ActionNewWeek`（要 `Settings` 与 `Recruits`）、`Week::GetName()`
（翻译层）、`Players`/`Campaign`。

## ADR-115：周产的全部输入走参数；四个生长常量内联成 const；升级住所只吃自己生物的生长

**背景**：ADR-114 让世界数到第二周，但城还不会生兵。参考把周产放在 `castle.cpp` 的 `Castle::ActionNewWeek()`
（`castle.cpp:838-940`），由 `AllCastles::NewWeek()` 与 `World::NewDay()` 逐层分发；生长常量在
`game_static.cpp:145-163`，是四个直接返回字面量的函数。

**决策**：

1. **周产的全部输入进签名**：`ActionNewWeek( uint weekCount, in Week week, bool isBeginMonth, uint dayCount,
   Pcg32? generator )`。参考在这一个方法里问了世界五件事（`CountWeek`/`GetWeekType`/`BeginMonth`/`CountDay`
   与全局随机源），端口把它们变成五个实参——和 ADR-107（城不认世界）、ADR-109（难度/天数/种族/随机源进参）、
   ADR-112（矿井数走回调）、ADR-114（事件列表进参）是同一条线。好处是城仍是纯数据，测试不用造世界，
   `AllCastles.NewWeek` 只需往下透传。
2. **四个生长常量落成 `public const`**：`game_static.cpp:145-163` 里 `GetCastleGrownWell()` 等四个函数不读不写
   状态，返回的就是 `2`/`8`/`5`/`100`。端口没有设置层（`Settings` 属会话层），所以先内联成常量，并在 XML
   文档里写明「将来变成设置项时的落点」；测试直接用它们算期望值，等于把参考的数字钉住。
3. **生长取「实际住所」，周怪加成取「精确住所」**：两者在参考里是两次不同的判定——
   `GetGrowth( GetActualDwelling( dwelling ) )` 走升级后的生物（游侠塔按游侠的 8 算），而
   `_isExactBuildingBuilt( dwelling ) && Monster(...) == week.monster` 只认那一个位。所以箭矢之周不喂游侠塔，
   但游侠塔的常规生长比箭塔高——两条都单独测了。
4. **「首周跳过」留在城这一层**：`if ( weekCount < 2 ) return;`（`castle.cpp:840-843`）。看上去更适合放进
   调用方（世界已有 `IsFirstWeek`），但参考的语义是「房子建好时已经存了一周的兵」，那是城的知识；世界只是
   恰好知道现在是第几周。
5. **井的加成按「是否建成」而非「是否被前置拖进来」**：`IsBuild( Well )` 就够了。骑士的第三住所需要井，
   `DwellingUpgrade2` 又需要第三住所，于是「只建了箭塔升级」的城里其实有井，生长带 +2。这是参考的行为，
   测试里显式断言井在场，把这条链钉住而不是当噪音屏蔽。
6. **中立城的随机守军收 `generator`**：`JoinRandomArmy` 在 ADR-109 里已经接过一次随机源，这里继续透传；
   城堡必收两次，普通城 `Roll( 1, 100 ) <= 40` 才收第二次（`castle.cpp:911-916`）。`null` 表示线程自己的
   设备，与参考一致——测试用固定种子把「第二次到底有没有发生」钉死。
7. **`World.NewDay` 的顺序写成代码的次序**：月先、周次之、日最后（`world.cpp:427-459`）。现在只有周与日两段
   有实现，缺的三段（月的怪物、周命对象、招募池）在注释里按同一顺序列出，将来补的时候位置是确定的。

**后果**：`Castle.cs` 增 `GrownWell`/`GrownWel2`/`GrownWeekOf`/`GrownMonthOf`、两张私有住所表与
`ActionNewWeek`/`GetDwellingShelf`/`TryFindWeekMonsterDwelling`；`AllCastles.cs` 增 `NewWeek`/`NewDay` 并修掉
`Add` 在宽度为 0 时的除零；`World.NewDay` 增一段把周交给城表。新增
`tests/FHeroes2.Game.Tests/CastleWeekGrowthTests.cs`（13 条）。`FHeroes2.Game.Tests` 1390 → **1403**，
全解决方案 2077 → **2090** 条全绿，0 警告 0 错误。覆盖率不动（`castle.cpp` 与 `world.cpp` 本就在「部分」里），
合计仍 **28/63/158**。

**仍未转**：`ActionNewMonth`（月度事件、招募池重置）、`ActionNewWeek` 的招募点（要 `Recruits` 与英雄池）、
`AllowBuyHero`/`getAvailableArmy`、税收、法师公会法术与船长、`GetPowerModificator`、`World::NewWeek()`/
`NewMonth()`、`Kingdom::ActionNewDay` 的访问记录与胜负判定，以及 `Week::GetName()`（翻译层）。

## ADR-116：区域算法按参考的九步与种子顺序落地；并列按行号兜底；`FindMissingRegions` 写回种子类型

**背景**：`World::PostLoad()` 的末尾要算一次静态分析（`world.cpp:1233`），把地图切成连通的水陆区块并量出水
面占比与陆地粗糙度。参考在 `world_regions.cpp`，共九步：数障碍 → 取最空的行列作种子线 → 城堡作种子 →
补种子 → 带一圈 padding 的工作副本 → 每个种子一区 → 每轮各长一格 → 剩余区域自成区 → 写回格子并连邻居。
区域的结果是 AI「敌人在哪边」的全部依据，也是漩涡与石阵跨区域连接的载体。

**决策**：

1. **区域 id 与三个常量共用一套编号**：`MapRegionNodes` 的 `Blocked = 0`/`Open = 1`/`Border = 2`/
   `Found = 3`，真实区域从 `Found` 起编号，列表前三位就是这三个常量（各带一个永不生长的节点）。
   参考是匿名 enum 与 `std::vector<MapRegion>` 的下标混用（`world_regions.h:28-34`），端口照抄，
   因此 `MapRegionNode.Type` 是 `uint` 而不是枚举——它既能是常量也能是 id。`Border` 参考也不赋值，
   保留它只为让编号对齐。
2. **种子顺序必须可复现**：城堡按颜色倒序（`NONE` 为 0，中立城最后）、同色按 tile 索引；空行/列按障碍数
   升序。参考在计数相同时把次序留给 `std::sort`，端口在计数之后比行号。并列在真实地图上不罕见
   （整行都是水），不钉死就会有「两台机器切出不同区域」的隐患。
3. **种子间距用参考的常数**：城堡 17、补充种子 18、每 7 条线取一条（`world_regions.cpp:181-183`）。
   它们不是可调参数，是算法的一部分；落成方法内 `const`，避免和将来的设置层混淆。
4. **每轮每区只长一格，且先拍快照**：`RegionExpansion` 用 `nodesEnd = Nodes.Count` 冻结本轮要处理的节点，
   处理完再整体推进 `LastProcessedNode`。等价于参考的 `while ( _lastProcessedNode < nodesEnd )`
   （`world_regions.cpp:116-125`）。这样多源扩张是「公平竞争」，不是「大区碾压」。
5. **工作副本加一圈 padding**：宽高各 +2，`ConvertExtendedIndex` 做真实索引到扩展索引的映射，
   邻居读取因此不用判边界；padding 的 `Type` 天然是 0（`Blocked`）。代价是所有索引换算都要过一次函数，
   换来的是 `CheckAdjacentTiles` 里八个方向的全裸读取。
6. **刻意偏离参考：`FindMissingRegions` 写回种子类型**。参考建好区域后从不写 `currentTile->type`
   （`world_regions.cpp:136-151`），于是种子格留在 `Open`，区域绕回来时会把它再收一次——湖心那一格在
   `_nodes` 里出现两遍。端口写回，保证 `Nodes` 就是区域的格子集合；否则将来 AI 拿节点数估算面积、
   测试拿节点数做断言，都会莫名多一格。这是本批唯一的语义偏离，注释写在方法上。
7. **邻居集的遍历先快照**：参考在 `std::set` 里边遍历边插入（`world_regions.cpp:390-397`）。C# 的
   `SortedSet` 会在遍历中修改时抛异常，所以 `foreach (uint adjacent in region.Neighbours.ToArray())`。
   循环只加反向引用，快照不会漏掉本轮新加的那些——它们本来就要靠对方回填。
8. **`GetRegion` 越界返回空区域**：参考返回静态 `MapRegion`，因为「没有区域的格子」照样会被问到
   （山上的城、被封住的漩涡出口）。端口照抄：越界不抛异常，返回一个 `Id == Found`、无节点无邻居的实例。
9. **`Reset()` 清掉区域与两项统计**：参考的 `Reset` 不碰 `_regions`，端口清。理由与 ADR-114 的周缓存
   一致——区域是瓦片索引，比瓦片活得久就会在换图后指到上一张图的格子。
10. **参考 assert 的地方端口返回空答案**：`world_regions.cpp:239-244` 断言地图有可走格、有陆地；
    端口的世界可以是空的，所以空世界直接返回（`WaterPercentage` 0、`LandRoughness` 1.0），
    全水地图只跳过粗糙度。区域本身照算：全水地图也有区域，只是没有陆地。

**后果**：新增 `src/FHeroes2.Game/World/MapRegion.cs`（`MapRegionNodes`/`MapRegionNode`/`MapRegion`）与
`src/FHeroes2.Game/World/World.Regions.cs`（`ComputeStaticAnalysis` 及七个私有助手）；`Tile` 增
`Region`/`UpdateRegion`；`World.PostLoad` 末尾调用，`World.Reset` 清区域。新增
`tests/FHeroes2.Game.Tests/WorldRegionsTests.cs`（8 条，含一条读真实 `Alteris.mp2` 的对拍）。
`FHeroes2.Game.Tests` 1403 → **1411**，全解决方案 2090 → **2098** 条全绿，0 警告 0 错误。
覆盖率 `world` 目录 `0/4/1` → **`1/4/0`**，合计 **29/63/157**。

**仍未转**：`PostLoad` 只剩漩涡带船的修复（`MapObject` 与船位）；AI（区域的消费者）要 `Players`；
`World::NewWeek()`/`NewMonth()`（要 `Settings`、怪物刷新、招募池）；`Kingdom::ActionNewDay` 的访问记录与
胜负判定；`Game::NewDay` 那一层会话循环。

## ADR-117：价目表照字面量搬；市场汇率七张表不合并成公式；非法输入返回 0 而不是断言

**背景**：王国会算日收入（ADR-112）、会数周（ADR-113/115）、会切地块（ADR-116），但商店那一栏的标价还空着。
参考把「买船、买法术书、招英雄、炼金术士、麦哲伦地图」五个固定价放在 `payment.cpp:29-57`，把市场的换汇比例
放在 `resource_trading.cpp:25-160`。两篇都是纯查表，没有状态、没有界面。

**决策**：

1. **固定价照 `Cost` 字面量逐字搬**：`Cost` 的字段顺序是 `{ gold, wood, mercury, ore, sulfur, crystal, gems }`
   （`resource.h:37-46`），港口的 `Cost` 同序，所以 `new Cost { Gold = 1000, Wood = 10 }` 就是参考的
   `Cost{ 1000, 10, 0, 0, 0, 0, 0 }`。写成对象初始化器而不是位置参数，是为了让「千金银十木」在代码里也一眼可读；
   位置参数的 `new Cost(1000, 10, 0, 0, 0, 0, 0)` 与「十金千木」只差一个顺序，那是这类 bug 最容易藏的地方。
2. **`BuyBuilding` 保留参考的参数顺序**：`BuyBuilding( race, building )`，而 `CastleBuildings.GetCost` 是
   `( building, race )`。看上去别扭，但这一层存在的意义就是「价目表的门面」——把顺序抹平会让调用方按参考写代码时
   静默出错（种族与建筑都是 32 位范围内的值，编译器不会报警）。注释里写明两个顺序相反。
3. **七个类别组合，七张表，不合并成公式**：common（木、矿）、rare（水银、硫磺、水晶、宝石）、gold 三类之间有
   八个有序组合，其中 common→common 与 rare→rare 共用一张表，于是七张。每张 9 列（1–9 个市场）。
   参考的数值不是指数曲线：1 木 = 25 金（1 个市场）到 124 金（9 个市场），中间是 37/50/62/74/87/100/112，
   为了几个整数把曲线拟合出来得不偿失，也失去与参考对拍的能力。测试直接钉第 1 列与第 9 列，外加单调性。
4. **市场数为 0 返回 0，不抛异常**：参考 `assert( 0 )` 后返回 0（`resource_trading.cpp:61-64`）。市场对话框
   在「还没选」时就会来问价，那是正常状态而不是程序错误，所以港口不 assert、不抛，只返回 0。
5. **超过九座市场钳到第 9 列**：参考也钳（`resource_trading.cpp:72-74`）。一张图可以放十来座城，
   `AllCastles` 数出的市场数会超过表宽；钳位是参考语义的一部分，不是防御性代码，所以测试把 10 与 9 钉成相等。
6. **非法资源位返回 0**：四个 `default` 分支在参考里是 `assert( 0 )`，港口返回 0。资源是位掩码
   （`Resource.Wood == 0x01` 等），`0x4000` 这种值只会从坏存档或调试代码里来；让它「换不到任何东西」
   比中止游戏更合适。四张表的 `default` 各测一次，防止有人把它们合并成一个 `default` 时漏掉一支。
7. **价目表留在 `Kingdom` 命名空间**：`PaymentConditions` 虽然转发城堡的造价表，但它是「王国要付多少钱」的
   问题，参考也放在 `kingdom/payment.cpp`；`ResourceTrading` 同理。这样 `castle` 不反向依赖 `kingdom`。

**顺带更正**：`docs/PLAN.md` 的主干第 3 条曾写「`PostLoad` 到这一步只剩漩涡带船的修复」。核对 1.1.18 的源码，
`World::PostLoad()`（`world.cpp:1194-1263`）里没有这一步：带船的漩涡格在装载时被记成船对象，所以参考按**部件**
而不是按类型找漩涡（`world.cpp:1219-1225`），港口一直就是按部件查的。`PostLoad` 到此整篇落地，
那句话已改掉。

**后果**：新增 `src/FHeroes2.Game/Kingdom/PaymentConditions.cs` 与 `src/FHeroes2.Game/Kingdom/ResourceTrading.cs`，
两篇都没改任何既有文件。新增 `tests/FHeroes2.Game.Tests/PaymentConditionsTests.cs`（5 条）与
`ResourceTradingTests.cs`（6 条，其中一条是六个种族 × 全部建筑的转发等价性）。
`FHeroes2.Game.Tests` 1411 → **1422**，全解决方案 2098 → **2109** 条全绿，0 警告 0 错误。
覆盖率 `kingdom` 目录 `1/6/7` → **`3/6/5`**，合计 **29/63/157** → **31/63/155**。

**仍未转**：`kingdom` 的 `puzzle.cpp`（`Puzzle` 位集与揭示动画）、`speed.cpp`（`_()` 文案）、
`kingdom_overview.cpp` 与 `view_world.cpp`（界面）、`experience.cpp`（两句文案）；`kingdom.cpp` 里剩下的
`ActionNewWeek`/`ActionNewMonth`（要 `Recruits` 与 `Settings`）与 `ActionNewDay` 的访问记录、胜负判定；
`castle` 七篇、`army` 两篇、`battle` 十八篇全部未动。

## ADR-118：`Recruit` 用 `record struct` 且默认值即空槽；替换规则照抄并列时换第一格；`Kingdom.GetRecruits()` 暂不补位

**背景**：酒馆要回答「今天卖哪两位英雄」。参考把这份状态做成 `Recruit`（id + 投降日）与 `Recruits`
（两个槽位，派生自 `std::pair`），放在 `heroes_recruits.h/.cpp`，由 `Kingdom` 持有，`Kingdom::clear()` 里重置。
它同时承担三件事：新一周的待聘英雄、战斗后投降/撤退的英雄（fheroes2 的扩展，原版只留最后一位）、
以及「这位英雄已被某处预留」的 `RECRUIT` 模式位。

**决策**：

1. **`Recruit` 是 `readonly record struct`，`default` 就是空槽**：参考默认构造是 `_id = UNKNOWN`（0）、
   `_surrenderDay = 0`，C# 结构体默认值与之逐位相同，于是 `Recruit.None => default`，`Recruits.Reset()` 就是
   两次赋 `default`。这比「空槽用 `null`」更贴近参考（参考的值语义让整块状态可以按值拷贝、按值存档），
   代价见第 2 条。
2. **投降日 0 被拒**：因为 0 也是「空槽」的标志，`AppendSurrenderedHero( hero, 0 )` 会让槽位看起来有英雄而
   实际投降日为空，随后替换规则永远认为它最旧。参考在这里 `assert`，港口抛
   `ArgumentOutOfRangeException`——这是调用方的编码错误，不是玩家能触发的状态。
3. **`GetHero1/2` 接收 `AllHeroes` 参数**：参考用全局 `world.GetHeroes()`。端口把英雄表当参数传入，
   一是避免 `Recruits` 依赖 `World`（`Kingdom` 也不该反向依赖世界），二是让测试可以用一张真的英雄表
   （`AllHeroes.Init( MapVersion.SuccessionWars )`）而不是桩。`null` 表示槽位空或 id 不是英雄。
4. **`SetHero1/2` 打 `RECRUIT` 位，清空槽位不撤位**：与参考逐行一致。撤位属于「英雄离开待聘名单」的一方
   （被雇走、进监狱、被 AI 抢走），不是「这个王国不推荐他了」；两个王国可以同时推荐同一位英雄，
   谁先雇走谁撤位。把撤位塞进 `SetHero*` 会让另一个王国的名单静默失效。
5. **替换规则照抄，包括并列**：`appendSurrenderedHero` 换掉投降日更旧的格子，两天相等时替换**第一格**
   （参考的 `first > second ? second : first` 在等号时为假）。顺序会决定两位投降英雄在名单里的位置，
   而酒馆界面按槽位画，所以不能用「随便换一个」代替。
6. **`First`/`Second` 只读、写入口径统一**：参考的 `first`/`second` 是 public 的 `std::pair` 成员，
   谁都能赋值；端口给 `private set`，外部只能走 `SetHero1/2`、`AppendSurrenderedHero`、`Reset`。
   参考里也正好只有这三处写入（外加存档加载，未转），所以语义没有损失，只是把「顺手改一下」这条路堵上。
7. **`Kingdom.GetRecruits()` 暂时只返回**：参考同名函数会按需补位——槽位空着、或者占位英雄已不可招募，
   就现找一位补上；第一周还要优先考虑战役奖励给的英雄。补位需要 `Settings::isCampaignGameType`、
   战役存档、`Players::GetPlayerControl` 与 `World::CountWeek`，前四样都没落。端口先只返回已结算的那一对，
   XML 注释里写明缺口，等这几层落地再补上，而不是在 `Kingdom` 里塞一个「永远返回空名单」的假实现。
8. **`Kingdom::clear()` 一起清**：参考 `clear` 里 `recruits.Reset()`（`kingdom.cpp:132`）。不清就会在换图后
   继续推荐上一张图的英雄，而那份 id 在新图里可能是别人的英雄——这类状态越多越容易漏，所以每加一个
   王国级字段就先问一句「`clear` 要不要清」。

**后果**：新增 `src/FHeroes2.Game/Heroes/Recruits.cs`（`Recruit` + `Recruits`）；`Kingdom` 新增私有
`_recruits`、`GetRecruits()` 与 `Clear()` 里的重置。新增 `tests/FHeroes2.Game.Tests/RecruitsTests.cs`（7 条）。
`FHeroes2.Game.Tests` 1422 → **1429**，全解决方案 2109 → **2116** 条全绿，0 警告 0 错误。
覆盖率 `heroes` 目录 `1/4/7` → **`2/4/6`**，合计 **31/63/155** → **32/63/154**。

**仍未转**：`Recruits` 与 `Recruit` 的存档读写（`serialize.cpp` / `game_io.cpp`）；`Kingdom::GetRecruits()` 的补位；
`Kingdom::ActionNewWeek()`（要 `Visit` 的一周生命周期清理）与 `ActionNewMonth()`（要 `Recruits` 之外的怪物刷新、
`Settings`）；`castle` 的酒馆/招募对话框（`castle_recruit.cpp` 等七篇）。

## ADR-119：拼图用 `bool[48]` + 四条区带表；洗牌接受 `Pcg32`；`Reset` 只清格子不重洗

**背景**：石阵（Obelisk）每访问一座就揭开寻宝图的一角。参考把这张图做成 `std::bitset<48>` 派生类，外加四条
区带索引表（`puzzle.h:33-55`），揭示逻辑在 `Puzzle::Update`（`puzzle.cpp:323`）。`Kingdom` 各持一份，
`clear()` 里 `puzzle_maps.reset()`。对话框（`ShowMapsDialog`）与存档读写也在同一篇里，但依赖渲染与存档格式。

**决策**：

1. **`bool[48]` 而不是 `BitArray`/`ulong` 位运算**：48 个格子，`Count` 就是一次遍历，一年也只在访问石阵时
   调几次。位运算会把「同一批格子」这件事藏进下标算术里，而这份代码最需要的是能对着参考的
   `zone1_order` 一眼看出「24 + 16 + 4 + 4 = 48」。
2. **四条区带表逐字照抄，顺序即语义**：`zone1` 是外框 24 格、`zone2` 内环 16 格、`zone3`/`zone4` 两个 2×2 块。
   `Update` 逐区填满才进下一区，所以「半数石阵只揭开轮廓」是算法结果而不是偶然。测试里用「24 格时外框满、
   两个 2×2 一格未动」把这条顺序钉住——谁若把区带合并或排序，这条会立刻红。
3. **洗牌接受 `Pcg32` 参数**：参考在构造函数里用全局 `Rand::Shuffle`。端口把生成器做成可选构造参数，
   一是让存档能复现同一张布局（参考靠存档保存位集而不是布局，但端口将来的存档会更愿意保存种子），
   二是让测试能固定布局。不传参数时用 `Rand.CurrentThreadRandomDevice`，与参考一致。
4. **`Update` 只增不减，且差额从「已揭开」算起**：`open * 48 / total` 减去当前 `Count`。丢掉一座石阵不会
   把图收回去；重复访问同一座也不会多揭一格。两件事都写成了用例——它们是最容易被「顺手修正」成
   「按当前石阵数重算整张图」的地方，而那样会让玩家在丢城后看见图倒退。
5. **总数为 0 直接返回**：参考的除法会除零（UB）。没有石阵的地图是合法的，它没有拼图可揭。
6. **`Reset` 只清位集，不重洗区带**：参考 `Kingdom::clear()` 里就是 `puzzle_maps.reset()`。换图后同样的石阵数
   会揭开同样的格子。这看着像漏了重洗，其实是参考的行为，用例用「清前清后同一批格子」钉住。
7. **对话框与存档不进来**：`ShowMapsDialog` 要 `PUZZLE.ICN`、屏幕与对话框栈；`operator<<`/`operator>>` 要
   存档版本。两者都属于别的层，本批只搬「哪一格是开的」这件事。

**后果**：新增 `src/FHeroes2.Game/Kingdom/Puzzle.cs`；`Kingdom` 新增 `_puzzleMaps`、`GetPuzzleMaps()` 与
`Clear()` 里的 `Reset()`。新增 `tests/FHeroes2.Game.Tests/PuzzleTests.cs`（8 条）。
`FHeroes2.Game.Tests` 1429 → **1437**，全解决方案 2116 → **2124** 条全绿，0 警告 0 错误。
覆盖率 `kingdom` 目录 `3/6/5` → **`4/6/4`**，合计 **32/63/154** → **33/63/153**。

**仍未转**：`Puzzle::ShowMapsDialog()`（渲染层）与 `Puzzle` 的存档读写（`serialize`/`game_io`）；
「数已访问的石阵并调 `Update`」在 `heroes_action.cpp` 的石阵分支；`kingdom` 目录剩 `experience`、`speed`
（`_()` 文案）与 `kingdom_overview`、`view_world`（界面）。

## ADR-120：访问名册按「格子 + 对象类型」记账；清理在 `isPlay` 之前；`IsPlay` 用颜色加出局位代替 `Players`

**背景**：地图上有一半的对象只能受益有限次——魔法井每天一次、风车每周一次、石阵与一次性加值建筑永久一次。
参考用 `Kingdom::visit_object`（`std::list<IndexObject>`）记「谁看过什么」，用 `visit.cpp` 的三条谓词按对象类型
分类，再用 `Kingdom::ActionNewDay`/`ActionNewWeek` 按天/按周清掉对应的一批，最后由 `World::NewDay` 按
「月 → 周 → 日」的顺序调用。

**决策**：

1. **`IndexObject` 是 `readonly record struct`，等值即「同一格子同一对象」**：参考继承
   `std::pair<int32_t, MP2::MapObjectType>`。C# 的 record struct 给出同样的值语义与 `==`，于是去重逻辑就是
   `RemoveAll(entry => entry == visit)`，而没有手写的比较器。
2. **去重键包含对象类型**：一个格子上的对象会被事件、编辑器、战后的地形换掉；只按格子去重会让「新对象」
   借旧记录免票。参考的 `std::find( IndexObject( index, objectType ) )` 也是二元组。
3. **`SetVisited` 一律「先删后插表头」**：列表因此永远是最新在前、无重复。`IsVisited(type)` 与
   `CountVisitedObjects` 都靠这个不变量，不需要额外索引。
4. **清理放在 `isPlay()` 检查之前**：参考明写了理由（出局的王国也要清，否则存档越涨越大）。这是那种
   「看起来可以合并成一句 if」的地方，所以照抄并在 XML 注释里写明。
5. **`IsPlay` 用「颜色不是 `None` 且未出局」代替 `Players::Get(color)->isPlay()`**：`Players` 没落，但
   日/周推进必须有「这个王国还在不在局中」的判断。`Kingdom.Init` 给颜色、`Clear` 抹颜色，所以颜色确实
   能表示「有没有玩家」，只有「玩家还在不在」这一半需要补：`ActionNewDay` 一旦判定出局，就把
   `_isOutOfGame` 置真，此后 `IsPlay` 为假。
6. **那一位不是装饰**：参考的 `LossPostActions()` 第一件事是 `Players::SetPlay(false)`；端口没有那一步，
   第一版只调回调不置位，结果「一城一英雄都没有」的王国（`IsLoss` 恒真）每天都会再调一次回调，测试当场
   抓到 9 次。补上 `_isOutOfGame` 后语义与参考一致：出局那天交出去一次，此后不再重复。
7. **`LossPostActions` 提升为可选 `Action` 回调**：它要改英雄和城的颜色、从 `Players` 名册里删、弹胜负框。
   端口保留「出局 → 别再往下走」这一半，另一半作为 `Action?` 交给调用方——与 `EventDate` 的显示器回调
   （ADR-113/114）同一个路子。`Kingdoms.NewDay` 转发时把当前王国绑进闭包，回调拿得到是谁输了。
8. **日期与周数提到参数上**：参考读 `world.CountDay()`/`world.CountWeek()`；端口让 `Kingdom` 不认识世界，
   `ActionNewDay( dayCount )`/`ActionNewWeek( weekCount )` 由 `World.NewDay` 传下来。与
   `ActionNewDayResourceUpdate` 一致（ADR-114）。
9. **调试赠礼是参数而不是全局开关**：`IS_DEVEL()` 在发布构建里根本不存在，所以 `ActionNewWeek( weekCount,
   isDebugMode )` 表达的是「调用方是不是在走调试入口」，而不是运行时可拨的作弊开关。
10. **旅行者帐篷与水晶球不入名册**：帐篷是「按颜色位的永久解锁」（周也不清），水晶球是「今天看得见」
    （每天清）。三者生命周期不同，硬塞进同一个列表会让清理逻辑立刻变成三个特例。
11. **`KingdomModes` 照抄稀疏位值**：参考的三个值里两个被注释掉了（`0x0001`、`0x0004`），端口保留跳跃的
    数值，免得将来与参考的存档位对不上。
12. **`World.NewDay` 的顺序按参考排**：王国周 → 城周 → 王国日 → 城日。写在 `World.NewDay` 里一行注释，
    因为这一行是这个文件最贵的东西。

**后果**：新增 `src/FHeroes2.Game/Maps/Visit.cs`、`src/FHeroes2.Game/Kingdom/KingdomModes.cs`；`Kingdom` 新增
访问名册、帐篷位图、水晶球集合、模式位、`IsPlay`/`IsControlHuman`、`ActionNewDay`/`ActionNewWeek`、`Clear` 里
的清零；`Kingdoms` 新增 `NewDay`/`NewWeek`；`World.NewDay` 接上两者。新增
`tests/FHeroes2.Game.Tests/KingdomVisitTests.cs`（17 条，含石阵三条）。
`FHeroes2.Game.Tests` 1437 → **1454**，全解决方案 2124 → **2141** 条全绿，0 警告 0 错误。
覆盖率 `maps` 目录 `3/7/7` → **`4/7/6`**，合计 **33/63/153** → **34/63/152**。

**仍未转**：`Kingdom::ActionNewMonth`（要 `Settings` 与月怪刷新）、`LossPostActions`（要 `Players`）、
`GetRecruits()` 的补位（要 `Settings`/战役/`Players`）、英雄自己的 `visit_object`（要 `hero.GetKingdom()`）、
`Heroes::NewWeek`/`NewDay`（要英雄池）。

## ADR-121：石阵分支落在 `Kingdom.ActionObelisk`，用返回值把「揭开了没有」交给界面层

**背景**：`heroes_action.cpp` 的 `OBJ_OBELISK` 分支（`heroes_action.cpp:2924-2946`）只有三步：检查「这座石阵
本王国看过没有」、登记为全局访问、按「已访问石阵数 / 地图石阵总数」重算寻宝图；之后是音效与对话框。
它属于 `Heroes::Action` 那个 3200 行的总开关，而那个总开关的每一支都要 `hero.GetKingdom()`——端口还没有
英雄到王国的那条链接。

**决策**：

1. **这一支放在 `Kingdom` 上，签名 `ActionObelisk( tileIndex, totalObelisks )`**：三步里两步是王国状态。
   放在英雄上会让英雄必须认识王国，而那正是还没搭的链接；放在世界上会让世界认识王国的名册。
   王国自己既持有名册又持有拼图，是这几步唯一都不缺依赖的地方。
2. **返回 `bool`（这次揭开了一角没有）**：参考在「已看过」那一支只弹一句提示，在「揭开」那一支放音效、
   打开寻宝图。端口不弹任何东西，于是把这条分支信息交给调用方——一个 `bool` 比让界面层自己去比较拼图
   前后差了几个格子便宜得多。
3. **不在此处放对话框**：`Puzzle::ShowMapsDialog` 要 `PUZZLE.ICN`、屏幕与对话框栈（ADR-119 已把这条留在
   渲染层）。王国能算、能记、能返回答案，但不该知道有没有屏幕。
4. **总数由调用方给**：`world.CountObeliskOnMaps()` 已经有（`World` 的对象位置表），王国不认识世界，
   于是与日期、周数一样提到参数上（ADR-120 的第 8 条）。
5. **`IsVisited(tileIndex, Obelisk)` 而不是 `IsVisited(Obelisk)`**：同一张图上有多个石阵，问的是「这一座」。
   按类型问会在第一座之后把其余石阵全判成「看过」。
6. **`GLOBAL` 而不是 `LOCAL`**：换一位英雄去同一座石阵不该再揭一次。参考用 `Visit::GLOBAL` 登记，
   端口因此直接进王国名册，不需要英雄侧的名册。

**后果**：`Kingdom` 新增 `ActionObelisk`；`Puzzle`（ADR-119）第一次有了调用方，`Kingdom.GetPuzzleMaps()` 不再是
孤岛。`KingdomVisitTests` 里加三条（首次揭开一格、重复无效、48 座全揭）。
覆盖率不变（`heroes_action.cpp` 仍记「未转」——这一支是它的一小段，整篇还差 3000 行）。

**仍未转**：`heroes_action.cpp` 的其余分支（都要 `hero.GetKingdom()`）、`ActionToObelisk` 的音效与
`ShowMapsDialog`、拼图的存档读写。

## ADR-122：英雄自己也有访问名册；运气/士气靠「重算」而不靠「计数器」；动作层收 `Kingdoms`

**背景**：上一批把 `visit_object` 放在王国上（ADR-120），但参考是两层：`Heroes::isVisited`/`SetVisited`
（`heroes.cpp:1261-1320`）按 `Visit::LOCAL`/`GLOBAL` 决定问自己还是问王国。魔法井是「每天一次、每人一次」
（`LOCAL`），泉眼是「每张图一次、跨英雄只算一次」（`GLOBAL`）。同一个文件名册，两种寿命。

**决策**：

1. **英雄持有 `List<IndexObject> _visitedObjects`，最新在前**：与王国那份同构，但重复项**不挪位**——
   参考 `Heroes::SetVisited` 是 `std::find` 命中即返回，王国那份才是「先删后插」。测试分别钉住两种行为。
2. **`SetVisited(index, type)` 里 `OBJ_NONE` 一进门就挡掉**：清雾、空地、路边都会调到这里，不挡会让名册
   每天长几千条。
3. **运气/士气改成「重算式」**：不存计数器。`GetLuck`/`GetMorale` 每次取 `GetAllVisitedObjectTypes()`
   （`Distinct()` 的集合），查 `GameStatic.GetObjectLuckEffect`/`GetObjectMoraleEffect` 求和，再夹进
   `Luck.Normalize`/`Morale.Normalize`。好处是「战时的加成怎么消失」不需要任何减法：`ActionAfterBattle`
   删掉 `IsBattleLife` 的记录，数值自己回落。
4. **三条寿命对应三个清理时机**：`ActionNewDay`（魔法井）、`ActionNewWeek`（风车/水车/巢穴）、
   `ActionAfterBattle`（喷泉/仙女环/雕像/美人鱼/绿洲/水坑/神殿/浮标）。日/周两个只在「移动点满」时才回蓝，
   而清理无条件执行——参考的 `if( move_point < GetMaxMovePoints() ) return;` 在清理之前。
5. **每日回蓝 = 常数 1 + 神秘术等级**：`getDailyRestoredSpellPoints`（`heroes.cpp:1190`）照抄，
   `RestoreSpellPointsPerDay` 来自 `GameStatic`。第一天不回（`dayCount > 1`），测试钉住。
6. **动作层不认英雄的王国**：`HeroActions` 收 `(Hero, Kingdoms)`，内部 `_kingdoms.GetKingdom(_hero.Color)`。
   这与 ADR-121 的「放王国上」不同：那一步只需要王国状态，这里每一步都要改英雄（法力、移动点、运气），
   所以反向取值。代价是英雄必须有颜色，于是 `Kingdom.AddHero` 补 `hero.SetColor( _color )`
   （参考由 `Players` 设置；端口没有 `Players`，就在填名册时补上，测试当场抓到落到第七槽）。
7. **泉眼的全局登记写在 `if` 外面**：参考就是「没补到法力也算来过」。端口照抄，`Kingdoms.IsAnyVisited`
   做跨王国查询，登记一律 `Kingdom.SetVisited`。
8. **`IsDayLife` 比较不带动作位的 `MagicWell`**：端口瓦片存 MP2 原始值，动作位（+128）运行时才算；
   参考的 `OBJ_MAGIC_WELL` 自带动作位。第一版直译常量，Assets 的分类测试立刻红了，改成基础值并注明原因。
9. **移动点加成只做两张表**：绿洲 800、水坑 400。马厩（+500）要「买兵」分支，等军队落地再加。
10. **`ActionToGoodMoraleObject` 顺带加移动点**：参考在这一支里先登记、再 `IncreaseMovePoints`，因为表里
    只有一个值（神殿/绿洲/水坑），与「加运气」的建筑分开两支。
11. **风车/水车/小屋/魔法花园看瓦片，不看名册**：`ActionToObjectResource`（`heroes_action.cpp:795`）的「拿过没有」
    是读 `Tile.GetFundsFromTile`——取完 `ResetObjectMetadata`，第二次读到空袋。结尾那句 `setVisitedForAllies`
    照样登记王国名册，但那只为 AI/界面；端口没有 `Players` 的盟友表，先只登记自己所属王国。
12. **马厩的移动点是 400**：参考里 `OBJ_STABLES` 与 `OBJ_WATERING_HOLE` 共用 `case 400`，把它补进
    `GameStatic.GetMovementPointBonus`。马厩的另一半（在场骑兵免费升级）落在 `ActionToUpgradeArmyObject`，
    要费用与确认框，仍未搬，注释在方法上标出。
13. **黑猫、帐篷、石阵各走各的路**：黑猫记英雄本地名册（永久，三条清理都不含它）；帐篷连名册都不进，只在王国上
    点一色位（`MarkTravellerTentVisited`）；石阵只把「地图石阵总数」喂给 ADR-121 的 `Kingdom.ActionObelisk`，
    返回值 `false` 兼作「已经看过」的提示。
14. **竞技场把「选哪个主技能」提到参数上**：参考在动作里弹 `Dialog::SelectSkillFromArena()`；端口签名是
    `ActionToArena(tileIndex, skill)`，界面先问再调，方法只剩「查名册 → 登记 → 加一点」三步。
15. **屏障不改状态，只回一个谓词**：参考 `ActionToBarrier` 查「同色帐篷进过没有」然后说两句话，真正放行在移动
    判定里。端口 `ActionToBarrier` 返回 `bool`，避免在动作层伪造一次「拆屏障」。
16. **取宝只闭合无守卫那条**：`ActionToArtifact` 按参考顺序过三道门（已有法术书 → 满包 → 守卫条件），前两道
    不动瓦片；第三道只放行 `NoConditions`/`ContainsSpell`，三种付费回 `NeedsPayment`，技能/战斗/阵营回
    `NotSupported`。条件本身从 `Tile.GetArtifactCaptureCondition`（`metadata[2]`）读。这样地图上的付费神器
    不会被白送——宁可少做一步，也不做错一步。

**后果**：新增 `Hero.Visit.cs`、`HeroActions.cs`、`GameStatic.cs`；`Hero.Morale.cs` 改为重算；`Kingdom.AddHero`
补色；`Kingdom.ActionNewDay`/`ActionNewWeek` 里遍历 `_heroes` 做英雄 tick；`TileMetadata` 新增取条件。测试 +37 条
（`HeroVisitTests` 10、`HeroActionsTests` 27），`FHeroes2.Game.Tests` → 1436。

**仍未转**：`Heroes::Action` 的主 `switch`（其余约 32 支）、女巫小屋/学者/神殿（技能与法术）、住所/巢穴/流浪营地
（招募）、付费取宝的「掏钱」闭环（要 `getResourceTypeFromIconIndex`）、监狱、沉船/金字塔/龙穴（战斗）、
`GetMorale`/`GetLuck` 的宝物项（`MAXIMUM_MORALE`/`MAXIMUM_LUCK` 那一支）。

## ADR-123：升级是「经验反查 + 逐级抽签」，副技能的「谁选」用委托注入 ✅

**决定**：把 `Heroes::IncreaseExperience` / `_levelUp` 拆成三层，经验、抽签、选择互不纠缠。

1. **等级不落字段**：`Hero.Level` 由 `HeroExperience.LevelFrom(_experience)` 反查，`SetExperience` 只写经验。
   参考的存档里也只有经验，多存一个等级必然出现两份不同步的状态。
2. **连升三级抽三次**：循环里先把经验压到 `FromLevel(level)` 再 `LevelUp`，最后写回真实经验。这是参考的原写法，
   目的是让每一级的种子基于「当时的等级与技能」。
3. **抽签做成纯函数**：`SkillLevelUp.Primary` 返回该加的那一点，`SkillLevelUp.SecondaryCandidates` 返回两个
   「已经升好一级」的候选，都由 `Hero.LevelUp` 落地。参考这两个函数一个直接改主技能、一个返回对象让上层写回，
   端口统一成「先算后写」，避免一次升级写两遍。
4. **种子 = 地图种子 × 英雄状态**：`CreateLevelUpSeeds` 照抄 `_getSeedsForLevelUp`，混入地图种子、英雄 id、种族、
   四项主技能、十四个副技能等级，再 +0…+3。少了任何一项就复现不了「同图同英雄同成长」。
5. **「谁选副技能」用委托**：`IncreaseExperience(amount, mapSeed, chooser)`；`chooser` 为 `null` 即参考的
   `autoselect`（种子掷一次）。人类弹窗与 AI 决策都是同一个注入点，升级算法不必知道调用者是谁。
6. **知识树两份名册分开写**：先**无条件**记王国（参考的 `SetVisited(dst_index, Visit::GLOBAL)`），付得起才扣钱，
   真加经验时才记英雄本地。于是「钱不够」下次还能来，「学过」的那次只看本地名册。
7. **知识树给「本级经验」**：`FromLevel(level) - FromLevel(level - 1)`，不是固定值，也不是「到下一级的一半」。
8. **凉亭记瓦片级名册**：同一张图上的多个凉亭各给一次 1000 点，不能按类型去重。

**后果**：新增 `Hero.Experience.cs`、`SkillLevelUp.cs`、`Tile.GetTreeOfKnowledgeRequirement`；`HeroActions.cs` 补
凉亭与知识树（`TreeKnowledgeOutcome`）。测试 +10 条（`HeroExperienceTests`），`FHeroes2.Game.Tests` → 1446，
全解决方案 → 2133。

**仍未转**：`LoadFromMP2` 按经验补等级（现在读档只存经验，等级会停在 1）、侦察术学成后开图、AI 的
`pickSecondarySkill`、学者（`objscholar.cpp`）、住所与巢穴（招募）、监狱、沉船/金字塔/龙穴（战斗）、
付费取宝的图标索引映射。

## ADR-124：神殿与女巫小屋——判定靠「书里有没有」，名册写在分支之外 ✅

**决定**：这两类知识对象不靠名册挡重复访问，名册只在末尾无条件登记一次。

1. **车道读取放进 `TileMetadata`**：`GetSpellFromTile`（三个圆环 + 金字塔共用 `metadata[0]`）与
   `GetSecondarySkillFromWitchsHut`（`metadata[0]` 是技能号，等级固定基础）。动作层不该知道车道怎么排。
2. **四道门按参考的顺序**：无书 → 三级法术无智慧 → 书里已有 → 学会。顺序有语义：没有书时连「懂不懂」都不用问，
   免得给玩家「你看不懂」这种误导。
3. **`skipBag = true`**：查重只看法术书。背包里的同法术卷轴不是一个「已经学会」的证据。
4. **名册写在所有分支之外**：参考的 `SetVisited( GLOBAL )` 在 if/else 链之后无条件执行。端口照抄，于是「没书」
   与「听不懂」的访问也会进王国名册。**这与知识树相反**（ADR-123 里知识树靠名册挡重复），因为知识树的奖励是
   一次性的经验，而神殿/小屋的稳定性来自「书里/技能里有没有」。名册在这里只服务 AI 与界面提示。
5. **不写英雄本地名册**：`Hero.IsVisited` 对这两支保持干净，语义分区明确——凉亭/知识树是「瓦片级一次」，神殿/
   小屋是「每次可进、学会了不再给」。
6. **满槽检查在调用方**：`LearnSkill` 照参考不挡第九个技能（`SecSkills.Add` 静默丢弃），满槽由
   `HasMaxSecondarySkill` 先问。谁教技能，谁负责看槽。
7. **侦察术开图留缺口**：参考学会 Scouting 时 `Scout`+`ScoutRadar`；端口没有迷雾，只记技能，不假装开图。

**后果**：新增 `TileMetadata.GetSpellFromTile`、`GetSecondarySkillFromWitchsHut`、`Hero.SecondarySkills.cs`、
`HeroActions.ActionToShrine`/`ActionToWitchsHut` 与 `ShrineOutcome`/`WitchsHutOutcome`。测试 +10 条
（`HeroShrineTests`），`FHeroes2.Game.Tests` → 1456，全解决方案 → 2143。

**仍未转**：金字塔/沉船/龙穴的守卫判定与战斗、住所与巢穴的招募流程、`ActionToBattle*`、「钱或神器」那一簇
（营火/骷髅/货车/浮木/海上宝箱/沉船幸存者/桶）、`LoadFromMP2` 补等级、侦察术开图、付费取宝图标映射。

## ADR-125：宝箱二选一交给调用方，折金覆盖原金币 ✅

**背景**：上一轮把「学者」列为下一步，实际上 fheroes2 与端口都没有这个对象（那是《英雄无敌 3》的 Scholar）。
本轮改做宝箱（`heroes_action.cpp:1795`）与仙那度（`:2577`），两者都不依赖战斗或军队。

**决定**：

1. **选择用委托注入**：`ActionToTreasureChest( tileIndex, world, keepGold )`，`keepGold( gold, exp )` 回是否拿钱，
   `null` 拿钱。人类对话框与 AI 的概率模型都在这一个入口外，动作层只负责「按选择结算」。
2. **判定顺序照抄**：水上 → 有钱/满包 → 只有神器 → 空。顺序是语义，不是实现细节。
3. **折金覆盖而非叠加**：参考的 `gold = GoldInsteadArtifact( objectType )` 覆盖箱子自带的钱。看似 bug，是原版
   行为；ADR 记录以免后人「顺手修好」。
4. **不进访问名册**：宝箱只清元数据，空箱也清（`resetObjectMetadata` 在函数末尾）。「开过没有」靠瓦片空不空。
5. **经验式子是收益压制而非价值换算**：`gold > 500 ? gold - 500 : 500`，不做「优化」。
6. **仙那度资格单独成函数**：`GameStatic.IsHeroWorthyToVisitXanadu`，两条路（等级 > 9 / 外交术分级门槛）。
   资格不足**不登记名册**，记的是英雄本地名册（「同一个学生只教一次」）。
7. **折金表单独成类**：`ArtifactGold.InsteadOf`，纯函数、不碰英雄与瓦片，供五个动作共用。

**后果**：新增 `ArtifactGold`、`GameStatic.IsHeroWorthyToVisitXanadu`、`ActionToTreasureChest`、
`ActionToXanadu`、`TreasureChestReward`、`XanaduOutcome`。测试 +13 条（`HeroTreasureTests`），
`FHeroes2.Game.Tests` → 1469，全解决方案 → 2156。

**仍未转**：金字塔/沉船/龙穴的守卫与战斗、住所与巢穴招募、`ActionToBattle*`、`ActionToSirens` 与
`ActionToPoorMoraleObject`（军队）、传送门与漩涡（移动）、`ActionToSign`/`ActionToSphinx`（文本与答题）、
`LoadFromMP2` 补等级、侦察术开图、付费取宝图标映射。

## ADR-126：「钱或神器」一簇——五支的收尾规则互不相同，照抄而不是统一 ✅

**背景**：营火、桶、散落资源、骷髅、货车、浮木、沉船幸存者七种对象的产物都只是「一袋资源或一件神器」，
车道读取（`GetFundsFromTile`/`GetArtifactFromTile`/`DoesTileContainValuableItems`）在 ADR-123/125 已具备。
看起来可以抽成一个函数，参考里却是五个函数。

**决定**：

1. **不合并**。五支的「满包怎么办 / 清不清元数据 / 记不记名册」三项各不相同，抽象成一个函数只会让每个调用点
   传三个布尔开关，比分开写更难读、更容易漏。参考的位置差异就是语义差异。
2. **货车不折金**：骷髅、宝箱、沉船幸存者满包时把神器换成 1000 金币，货车只弹「车厢是空的」然后照样清空。
   这是原版的不一致，**照抄并写明**，不「修好」。
3. **货车先看神器再看货物**：神器车道非空时资源不另算。
4. **清元数据的位置决定空箱行为**：骷髅/货车把 `resetObjectMetadata` 放在「有价值」分支里，浮木/沉船幸存者放在
   外面无条件执行。前者是「空着也记一笔名册」，后者是「一个人只能救一次」。
5. **营火/桶的换算留在车道层**：`N` 个资源 + `N×100` 金币由 `GetFundsFromTile` 一次算完，动作层不重算。
6. **漂流瓶不进 `IsPickupResource`**：它只弹留言不给钱，等留言文本系统落地再接；现在让判定函数如实回答「不归我」。

**后果**：新增 `HeroActions.ActionToPickupResource`/`IsPickupResource`/`ActionToSkeleton`/`ActionToWagon`/
`ActionToFlotsam`/`ActionToShipwreckSurvivor` 与结果类型 `ObjectPickupReward`。测试 +14 条
（`HeroPickupTests`），`FHeroes2.Game.Tests` → 1483，全解决方案 → 2170。

**仍未转**：带守卫的金字塔/沉船/龙穴与矿井（战斗）、住所/巢穴/流浪营地（军队与城堡招募）、`ActionToSirens`
与 `ActionToPoorMoraleObject`（军队）、传送门/漩涡（移动）、`ActionToSign`/`ActionToSphinx`（文本）、
`ActionToObservationTower`/`ActionToMagellanMaps`（等迷雾）、`LoadFromMP2` 补等级、侦察术开图、付费取宝图标映射。

## ADR-127：监牢——先查名额后找囚犯，`Hero.Recruit` 收王国而不是颜色 ✅

**背景**：`ActionToJail`（`heroes_action.cpp:3402`）是动作层第一个「把一位英雄交给王国」的对象，它牵出三件
端口没有的东西：谁在这个瓦片的监狱里（`World.FromJailHeroes`）、王国还有没有名额（`AllowRecruitHero`）、
英雄怎么入伙（`Heroes::Recruit`）。

**决定**：

1. **判定顺序照抄：先名额、后囚犯**。参考在函数开头就问 `AllowRecruitHero( false )`，所以满编的王国连「这块
   地有没有囚犯」都不查，直接显示「你已经有 8 位英雄了」。这个顺序决定文案，不能为了「先看看牢里有没有人」
   而调换；有用例钉住。
2. **`Hero.Recruit` 的参数是王国，不是颜色**。参考是 `Recruit( PlayerColor, Point )` 内部再
   `world.GetKingdom( col )`；端口没有 `World.GetKingdom`（王国由 `Kingdoms` 持有，ADR-122），所以把王国直接
   递进去。颜色由 `kingdom.Color` 回答，避免「传进来的颜色与名册不一致」这种状态。
3. **`SAVEMP` 缺失等价于永远给满行动力**。参考只在 `!Modes( SAVEMP )` 时 `_movePoints = GetMaxMovePoints()`。
   端口没有这个模式位，于是无条件给满——刚出狱/刚雇来的英雄本来就该有整天的行动力。空军队时
   `GetMaxMovePoints` 返回 0，与参考同序（参考也是先给行动力、后补军队）。
4. **不清元数据、不记名册**。「监狱里还有没有人」由 `HeroModes.Jail` 回答，不由瓦片元数据回答；放出来的人
   模式位已清，再来一次自然 `NoPrisoner`。监狱物件本身留在瓦片上（英雄站上去时被 `Tile.SetHero` 寄存）。
5. **`AllowRecruitHero` 与既有的 `CanRecruitHeroes` 并存**。前者问「名额/钱够不够」，后者问「有没有城能出兵」，
   两个词几乎同义但判据无关，保留参考原来的两个名字并在文档里写明别互替。
6. **空牢也返回一个值**：参考对空牢什么也不显示，端口需要区分「放成功了」与「什么也没发生」，于是
   `JailOutcome` 多一个 `NoPrisoner`。

**后果**：新增 `HeroActions.ActionToJail`、`JailOutcome`、`Hero.Recruit`（`Hero.Recruit.cs`）、
`AllHeroes.FromJail`、`World.FromJailHeroes`、`Kingdom.AllowRecruitHero`。测试 +10 条（`HeroJailTests`），
`FHeroes2.Game.Tests` → 1493，全解决方案 → 2180。

**仍未转**：`ActionToOracle`（只剩弹文字 + 盗贼公会，都在 UI 层，逻辑层无可结算）、带守卫的金字塔/沉船/龙穴
与矿井（战斗）、住所/巢穴/流浪营地（军队与城堡招募）、`ActionToPoorMoraleObject`（战斗）、传送门/漩涡（移动）、
`ActionToSphinx`（答题 + 战斗）、`ActionToObservationTower`/`ActionToMagellanMaps`（等迷雾）、
`LoadFromMP2` 补等级、侦察术开图、付费取宝图标映射。

## ADR-128：塞伦——经验按血量折，名册记在英雄级；告示牌只取文本不改状态 ✅

**背景**：塞伦（`heroes_action.cpp:3366`，军队减员换经验）与告示牌（`heroes_action.cpp:1243`）此前都被我列进
「要前置系统」的欠账里。核对参考后发现判断错了：塞伦只需要 `Army`/`Troop` 的计数读写（端口早已完整），告示牌
只需要 `World.GetMapSign` 的留言（`world_loadmap.cpp` 那批信息块也已搬完）。

**决定**：

1. **塞伦的军队规则照抄三条**：空栈跳过、**只有 1 个的栈完全不动**、其余去 30%（向下取整但**至少去 1 个**）。
   第 3 条的补丁不能省：3 个兵的栈 30% 取整是 0，参考仍然淹死一个。
2. **经验按血量折，不按金币/战斗力**：`去掉的数量 × 单体生命值`。这样结果完全确定（无随机数），测试可以逐位
   断言；用 `MonsterStatistics.GetBattleStats(...).HitPoints` 取单体血量，不去读 `Troop.GetHitPoints()` 做除法。
3. **分工照抄参考**：`Army.ActionToSirens()` 只负责「兵少了 + 值多少经验」，名册登记与 `IncreaseExperience` 在
   `HeroActions.ActionToSirens` 里。因为加经验需要地图种子，动作层多收一个 `World` 参数，与
   `ActionToExperienceObject` 的既有做法一致。
4. **名册是英雄级，且「没掉兵」也记**：参考用 `hero.SetVisited( dst_index )`，所以同一位英雄听过一次就够，
   换一位还能再听。结果类型因此<b>不能只返回经验</b>：`SirensReward( Experience, AlreadyVisited )` 两个字段分别
   对应「淹死了多少」与「来过了」，`IsEmpty` 只表示「什么都没掉」。
5. **告示牌折成「取文本」**：参考的全部逻辑就是取 `MapSign` 的留言并弹窗，不改状态。端口返回 `string?`，标题
   与弹窗留给 UI；空留言如实返回空串，不替原版随机抽句子（端口没有那张语言表）。

**后果**：新增 `Army.ActionToSirens`（`Army.Troops.cs`）、`HeroActions.ActionToSirens` + `SirensReward`、
`HeroActions.ActionToSign`。测试 +11 条（`HeroSirensTests` 7、`HeroSignTests` 4），`FHeroes2.Game.Tests` →
1504，全解决方案 → 2191。

**仍未转**：`ActionToOracle`（弹文字 + 盗贼公会都在 UI 层）、带守卫的金字塔/沉船/龙穴与矿井（战斗）、
住所/巢穴/流浪营地（军队与城堡招募）、`ActionToPoorMoraleObject`（战斗）、传送门/漩涡（移动）、
`ActionToSphinx`（答题 + 战斗）、`LoadFromMP2` 补等级、侦察术开图、付费取宝图标映射。

## ADR-129：迷雾是 `World` 的状态——观察塔与麦哲伦地图由此落地 ✅

**背景**：`ActionToObservationTower`（`heroes_action.cpp:2521`）与 `ActionToMagellanMaps`（
`heroes_action.cpp:2759`）都不需要战斗、军队或移动，此前唯一的前置是「迷雾」。而端口里迷雾只有
`AdventureMap` 的场景对象（`MapFog.CreateForMap`，由 App 的场景按 `Mp2Map` 造一份），逻辑层够不着它。

**参考事实**：参考把迷雾存在<b>每块瓦片的元数据</b>里（`Maps::Tile::_fogColors`，一个颜色位掩码），
`Maps::ClearFog( tileIndex, distance, color )` 直接改瓦片，`World::ActionForMagellanMaps( color )` 逐块看
`tile.isWater()` 再揭。也就是说：迷雾本来就是<b>世界状态</b>，不是渲染状态。

**决定**：

1. **`World.Fog` 拥有它**，类型仍是既有的 `MapFog`（每格一个颜色掩码 + 一份方向缓存，早就有测试）。按需创建：
   没有地图返回 `null`，有 `Width`×`Height` 时建全黑的表，`Reset` 里丢掉。这样两个加载器都不必记得「同时建
   迷雾」，也不会出现「地图换了、迷雾还是旧的」。
2. **`World.ClearFog(tileIndex, distance, color)` 对应 `Maps::ClearFog`**：半径 ≤ 0、颜色为空、瓦片号越界都
   直接返回 0（参考是 `return`），否则揭圆并重算方向。方向的重算参考只做脏矩形，端口整张重算——代价可忽略，
   换掉脏矩形追踪。
3. **`MapFog.Reveal` 改成返回「真正掉了迷雾的瓦片数」**：这是唯一能既让对象报告战果、又让测试钉住「第二次爬塔
   揭不出东西」的写法。语句式调用不受影响（返回值可丢弃）。
4. **半径 → 方形的换算不改成平方**：`SquaredRevealLimit` 在半径 ≥ 6 时用 `(distance+1)²`，于是 19 格对应 400
   而不是 361，18 格与 19 格揭出来一样大。参考本来如此（`Maps::ClearFog` 的 `sqr` 就是这么算的），不改。
5. **只揭自己的颜色**：参考揭 `Players::GetPlayerFriends( color )`，端口没有联盟，`Hero.IsFriends` 是那个缺口
   唯一的一处，注释写明等联盟落地再改这里，而不是现在编一个 `Alliances` 类型。
6. **麦哲伦地图两段式**：参考在同一个函数里弹「是/否」再结算。端口把它拆成
   `NeedsConfirmation` → 调用方问玩家 → 带 `confirmed: true` 再来一次才扣钱开图，与
   `ArtifactPickupOutcome.NeedsPayment` 同一套做法。**「买过了」排在「钱不够」前面**，顺序决定文案，用例钉住。
7. **「买过了」记在王国名册上**（`Kingdom.IsVisited( Mp2ObjectType.MagellansMaps )`，按类型问），对应参考的
   `Visit::GLOBAL` + `setVisitedForAllies`：同阵营任何一位英雄都不必再买，地图上有第二个船长也照样挡住。

**后果**：新增 `World.Fog`/`World.ClearFog`/`World.ActionForMagellanMaps`、
`MapFog.ObservationTowerRevealDistance`/`MagiEyesRevealDistance`、`HeroActions.ActionToObservationTower`、
`HeroActions.ActionToMagellanMaps`、`MagellansMapsOutcome`；`MapFog.Reveal` 的返回类型由 `void` 改为 `int`。
测试 +13 条（`HeroFogTests`），`FHeroes2.Game.Tests` → 1517，全解决方案 → 2204。

**遗留（已写进代码注释与 PROGRESS）**：

* **开局侦察没搬**：`AllHeroes::Scout`（英雄 `HeroRevealDistance`=4、城堡 `CastleRevealDistance`=5 各开一圈）
  还没接，所以地图一加载是全黑的，只有观察塔与麦哲伦地图能揭开。这正是「侦察术开图」那笔欠账——**下一轮已
  补上，见 ADR-130**。
* **场景预览图仍用 `MapFog.CreateForMap`**：App 的冒险地图场景目前不持有 `World`，等游戏循环把 `World` 交给
  场景后再让它改用 `World.Fog`，避免现在造一个「两套迷雾互相打脸」的中间态——**已做，见 ADR-131（助手已删除）**。

**仍未转**：`ActionToOracle`（弹文字 + 盗贼公会都在 UI 层）、带守卫的金字塔/沉船/龙穴与矿井（战斗）、
住所/巢穴/流浪营地（军队与城堡招募）、`ActionToPoorMoraleObject`（战斗）、传送门/漩涡（移动）、
`ActionToSphinx`（答题 + 战斗）、`LoadFromMP2` 补等级、付费取宝图标映射。

## ADR-130：侦察是「英雄/城堡各自开一圈」，距离三项相加 ✅

**背景**：ADR-129 把迷雾收进 `World`，并记下「开局侦察（`AllHeroes::Scout`）没搬」。没有它，地图一加载全黑，
只有观察塔与麦哲伦地图能揭开——迷雾这块等于只用了一半。参考里这件事分在四处：
`Heroes::Scout`（`heroes.cpp:1749`）、`Heroes::GetScoutingDistance`（`heroes.cpp:1768`）、
`AllHeroes::Scout`（`heroes.cpp:2435`）、`Castle::Scout`（`castle.cpp:2213`）/`AllCastles::Scout`
（`castle.cpp:2380`），汇总在 `World::ClearFog( color )`（`world.cpp:838`）。

**决定**：

1. **距离 = 三项之和，第三项是「数值」不是「等级」**：神器 `AREA_REVEAL_DISTANCE` +
   `MapFog.HeroRevealDistance`(4) + `GetSecondarySkillValue( Scouting )`(0/1/2/3)。基础 4 格、专家 7 格、
   再加望远镜 8 格。参考不做上限裁剪，端口也不做。没有技能时那一项是 <b>0</b>，不会退化成「基础 +1」。
2. **`Hero.Scout( world, tileIndex )` 收瓦片号**，不自己读 `MapIndex`：走路时参考传的是「即将踏入」的那一格
   （`_moveStep()`），开局侦察传脚下那一格。签名跟着参考走，调用方才决定是哪一格。
3. **中立英雄返回 0**：参考 `assert( GetColor() != NONE )`，端口不崩——没有颜色的英雄本来就不该替任何人开图。
4. **按颜色筛，不按状态筛**：`AllHeroes.Scout` / `AllCastles.Scout` 只看颜色（监狱里的英雄只要还挂着颜色也
   开图；无主城镇永远筛不进来——这与参考一致）。`PlayerColor` 本来就是 `[Flags]`，将来有盟友不用改签名。
5. **`World.ClearFog( PlayerColor )` 只做两件**：城堡 + 英雄。参考还有 `map_captureobj.ClearFog`，端口的
   `CapturedObject` 只记颜色与类型、没有迷雾位；与其现在编一张假的占领表，不如留到「占领对象带颜色表」那一轮。
6. **`FogHeroMovementHandler` 是第一个真实的 `IHeroMovementHandler`**：四个回调里只有 `Scout` 现在有活干
   （迷雾是世界的状态，所以处理器持有 `World`），另三个（雷达、对象动作、AI）仍空着。`NullHeroMovementHandler`
   留着不动，测试与不需要迷雾的场合继续用它。
7. **侦察不记名册**：`IsObjectTypeVisited` 不参与，反复走、反复开都允许——开图只是清迷雾位，不改别的状态。

**后果**：新增 `Hero.GetScoutingDistance`/`Hero.Scout`（新分部文件 `Hero.Fog.cs`）、`AllHeroes.Scout`、
`Castle.Scout`、`AllCastles.Scout`、`World.ClearFog( PlayerColor )`、`FogHeroMovementHandler`（与接口同文件）。
测试 +12 条（`HeroScoutTests`），`FHeroes2.Game.Tests` → 1529，全解决方案 → 2216。

**遗留**：**没有调用者**。端口没有游戏循环，所以「开局调一次 `World.ClearFog( color )`、为每位英雄装上
`FogHeroMovementHandler`」目前只有 API。等场景/循环把 `World` 接过去时一起接，同时让 `AdventureMapScene` 改用
`World.Fog` 而不是自己 `MapFog.CreateForMap`（这条从 ADR-129 延续下来）——**场景那一半已做，见 ADR-131；
`FogHeroMovementHandler` 仍在等移动系统真正调它**。占领对象的迷雾位仍未做。

## ADR-131：冒险地图画的是 `World.Fog`，`MapFog.CreateForMap` 删除 ✅

**背景**：ADR-129 把迷雾收进 `World`，但同一张表在端口里有两个生产者：`World.Fog`（规则改它）与
`MapFog.CreateForMap`（App 的场景按 `Mp2Map` 另造一份）。ADR-130 补齐侦察后仍没有装配——
`World.ClearFog( colors )` 只有测试在调，场景画的还是那份「孤儿迷雾」。两套迷雾并存意味着任何一次开图
（观察塔、麦哲伦地图、英雄走路）都不会出现在画面上，是明确的错误状态。

**决定**：

1. **场景把地图装进 `World.Current`，画 `World.Fog`**：`World` 的构造函数是 internal（「游戏里只有一个世界」），
   所以不是 new 一个私有世界，而是 `World.Current.LoadMap( map )`——参考 `game_startgame.cpp` 的职责顺序
   也是「读图 → 建世界 → 开局侦察 → 交给界面」。
2. **开局侦察写成一个私有步骤 `ScoutStart`**：`World.ClearFog( observers )`（人类颜色的英雄圆盘），再加
   一次 `ClearFog( tileIndex, MapFog.CastleRevealDistance, observers )` 作为「聚焦城堡」的占位（MP2 城堡无
   归属，`AllCastles::Scout` 筛不出来）。注释写明「地图自带归属后删掉这段」。
3. **`MapFog.CreateForMap` 删除**：它存在的唯一理由是「场景手里没有 `World`」。留着就是第二条生产路径，
   迟早有人再踩一遍。它带来的 2 条测试与两个私有助手（`SyntheticMap`/`HeroTile`）随之删除，装载路径改由
   `WorldFogOnLoadTests` 从真实地图文件覆盖。
4. **`World.ClearFog( PlayerColor )` 末尾补 `Fog?.UpdateDirections( color )`**：揭开本身会更新掩码，但**零揭开**
   （有人类颜色、却既无英雄也无城镇的地图）会让新表保留全零掩码，而渲染器读的是掩码不是迷雾字节——结果是
   一张「有雾的表、一格雾都不画」的地图。多跑一遍 O(n) 换掉这个边界，值得。
5. **无人类玩家的地图仍传 `null` 迷雾**：语义不变（编辑器用途的地图不画雾），而不是传一张全黑的表。
6. **不接管 `FogHeroMovementHandler` 的装配**：App 里还没有英雄移动，装了也没人调；等移动系统落地时在
   同一处（场景拿到世界之后）装。

**后果**：改 `AdventureMapScene`（新增 `World` 字段/属性、`ScoutStart` 取代 `CreateFog`、日志加已侦察格数）、
`World.ClearFog( PlayerColor )`（补方向掩码）、删 `MapFog.CreateForMap`、修 `MapFog.HeroRevealDistance` 注释。
测试 +5 条（`WorldFogOnLoadTests`）、−2 条（`MapFogTests`），`FHeroes2.Game.Tests` → 1532，
全解决方案 → 2219。`VerifyAdventureMapBaseline.ps1` 对 `Alteris.mp2` 的渲染与
`tests/baselines/adventure-map.png` **逐字节一致**（640×480，0 像素差）：换生产者没有改行为。

**遗留**：`AdventureMapScene` 里 `World` 目前只被构造与展示，没有别的东西用它——它是给移动/战斗接进来的
把手。占领对象的迷雾位（`map_captureobj.ClearFog`）仍未做；`FogHeroMovementHandler` 仍在等移动系统。

## ADR-132：传送对象（立石与漩涡）——第三种位移，以及沉兵怎么算 ✅

**背景**：英雄此前只有「沿路线走过去」一种位移，`Hero.MoveToDest`（`heroes.cpp:2049` 的 `Move2Dest`）是专为传送
留的挂点，却没有调用者；`World.NextTeleport`/`NextWhirlpool` 与出口分组（`world.cpp:704/725/762`）也早已就位，
同样只被测试用。`heroes_action.cpp` 的这两支因此是「零件齐全、装配缺失」的典型。

**决定**：

1. **两支动作都先 `Scout` 再 `MoveToDest` 再 `ResetPath`**，顺序照 `heroes_action.cpp:1962-1972`。先 Scout 是因为
   雾要按**新**位置算；清路线紧跟位移，否则下一次走动会把英雄沿旧路线拉回传送门。两者共用的三步抽成私有
   `MoveThroughTeleport`，避免两支各写一遍还写出细微差别。
2. **「无处可去」折成 `TeleportOutcome.NoDestination`，不抛异常**。`NextTeleport`/`NextWhirlpool` 在只剩一头时把
   入口本身还回来，参考也是打一句警告就收场；孤立立石是编辑器允许的地图内容。
3. **落点不作为返回值**。参考返回 void，端口只回「走了 / 没走」；落点就是 `hero.MapIndex`，多返一个瓦片号等于
   让两个真相并存。沉了几只同理——去问军队。
4. **沉兵按血量算，不按数量算**：50%（`GameStatic.LostOnWhirlpoolPercent`，`game_static.cpp:107`）作用在整叠兵的
   血量上，剩余血量再按单兵血量**向上取整**折回只数，这就是新增 `Troop.GetCountFromHitPoints`（`monster.cpp:840`）
   的全部理由。按数量砍一半会得到完全不同的结果（3 个剑士砍半变 1 个，按血量是 2 个）。
5. **随机数收可选 `Pcg32`**：参考 `Rand::Get( 1, 3 )`，端口有同名 API 但全局伪随机在测试里钉不住；缺省用
   `Rand.CurrentThreadRandomDevice`，测试传固定种子。与 `Army::Reset` 的 `generator` 参数是同一套做法。
6. **`Troop` 挂 `GetCountFromHitPoints` 而不是给 `MonsterType` 扩方法**：`MonsterType` 是纯枚举，而这里查的正是
   `Troop` 用的那张战斗属性表。
7. **不接分发者**：`Heroes::Action` 的 switch 仍空着，与前面 28 个动作同一个欠账（端口现有 30 个
   `ActionTo*`，一个分发者都没有），集中到下一轮处理。

**后果**：`HeroActions.cs` +2 公开动作、+2 私有助手、+1 枚举（`TeleportOutcome`）、+2 using；`Troop.cs` +1 静态
方法；`GameStatic.cs` +1 常量。测试新增 `HeroTeleportTests` 8 条，`FHeroes2.Game.Tests` → 1540，全解决方案 →
2227。覆盖率合计不变（**34/63/152**），因为 `heroes_action.cpp`、`monster.cpp`、`game_static.cpp` 早在「部分」里。

**遗留**：动作仍**没有分发者**——`Heroes::Action`（`heroes_action.cpp:3747-4018`）还没搬，谁踩到对象、调哪一支
仍是空的；这是 30 个 `ActionTo*` 共同的欠账，且卡在「结果给谁」（消息层/交互层）这个设计问题上。
`ActionToSirens`/`ActionToPoorMoraleObject` 需要战斗，`ActionToSphinx` 需要答题界面，都不在这一轮。

## ADR-133：动作分发者与「非动作值」这条界——结果模型、两步走、以及被顺手堵掉的三个洞 ✅

**背景**：端口的 `HeroActions` 已经有 30 支 `ActionTo*`，每一支都能干活，却没有分发者——`Heroes::Action()`
（`heroes_action.cpp:3747-4018`）那张 300 行的 `switch` 没搬。卡点不在这 300 行本身，而在一个设计问题：**结果给谁**。
参考的 `Heroes::Action` 返回 void，它靠对话框直接把话说给玩家；端口没有消息层。

**决定**：

1. **结果折成 `HeroActionResult`**：`ObjectType`（进了哪一支）、`Kind`（`HeroActionKind`，哪一支真的干了活，
   `None`/`Unsupported` 两个哨兵）、`Changed`（世界有没有被改动——要不要重绘、要不要刷面板）、`Payload`
   （那一支自己的返回值，`TryGetPayload<T>()` 取回）。`Changed` 与「要不要提示玩家」是两件事：第二次踩法力泉
   什么都没改，但调用方照样该说「晚点再来」。
2. **非动作值在进门处折一次**：瓦片存的是带动作位的值（`ActionWindmill`），而端口的分类谓词、名册与 30 支动作
   的入参讲的都是不带位的值（`Windmill`）。新增 `Mp2ObjectTypes.NonActionType` 与
   `Tile.GetNonActionObjectType( World )`（对应参考的 `MP2::getNonActionObjectType`），分发器在 `switch` 之前
   折一次，`case` 标签就能与参考的 `OBJ_*` 一一对上。
3. **顺手堵掉三个洞**：`IsInGameActionObject` 原本自己剥位、`TileMetadata.GetDailyIncomeObjectResources` 的
   switch 比的是带位值、几支谓词混用两套词汇。三处全部改比 `NonActionType`，否则分发器能进、查询查不到，风车这类
   对象会「看得见、摸不着」。风车测试也因此改成直接写 `tile.Metadata[0]/[1]`，不再走带位的元数据编码。
4. **两步走的对象不硬做**：竞技场要先问玩家选哪项主技能（参考在函数里弹对话框），分发器只报到
   `HeroActionKind.Arena`、什么都不改，问完由调用方调 `ActionToArena`；马格勒的航海图与有主的神器本来就在返回值
   里带「还没问」，照常分发。宝箱那条「钱还是经验」的岔路用动作默认值：拿钱。
5. **不会静默通过**：怪物、城堡、另一个英雄、船、金字塔、沉船、龙穴、矿井、招募所、事件、神谕、狮身人面像这些
   要战斗/城堡界面/文本的对象一律回 `HeroActionKind.Unsupported`——「参考会做事、端口还不会」明说，调用方可以提示
   或干脆不许他走过去；踩空地才是 `None`。

**后果**：新增 `Heroes/HeroActions.Action.cs`（分发器 + 结果模型 + 两个枚举）、`Mp2ObjectTypes.NonActionType`、
`Tile.GetNonActionObjectType`；改 `TileMetadata.cs`、`HeroActions` 各谓词、风车测试。`heroes_action.cpp` 早已在
「部分」里，覆盖率合计不变。测试新增 `HeroActionDispatchTests` 7 条，`FHeroes2.Game.Tests` → 1547，
全解决方案 → 2234，0 警告。

**遗留**：`ActionToSirens`/`ActionToPoorMoraleObject` 要战斗，`ActionToSphinx` 要答题界面；
`ActionToArtifact` 里「有主的神器」只能报到，不能替玩家决定要不要抢。

## ADR-134：castle 目录的整套逻辑——表由脚本翻、法师塔的那一格、以及五处写明的缺口 ✅

**背景**：`castle` 目录 11 个文件，此前只有 `BuildingType`/`CastleBuildings`/`Castle` 的骨架（ADR-106/107/108/
109/110/115）。剩下的是三份能独立搬完的逻辑文件（`captain.cpp`、`mageguild.cpp`、
`castle_building_info.cpp`）、`castle.cpp` 的 AI 与训练那一半，以及七个纯界面/绘制文件。

**决定**：

1. **六族三张大表由脚本从参考 `.cpp` 翻**，不手抄。`castle_building_info.cpp` 的 32-805 行是六族 × （面积、
   名字、描述）加绘制顺序，共 21 个方法、上千行字面量。PowerShell 脚本先按 `pendingName` 预读把 `BUILD_*`
   的注释映射成 `BuildingType` 成员（注释在成员行的**上一行**，第一版按同行读是错的），再按 `case Race::*`
   分组输出 `CastleBuildingInfo.Tables.cs`。抽查（Knight 的 `Spec`="Fortifications"、`Well2`="Farm"、
   Wizard 的 `Spec`="Library"）与参考逐字一致。
2. **法师塔的免费槽数含图书馆槽——顺手修掉一个真 bug**。参考 `freeSlots` 的初值是
   `getMaxSpellsCount( level, hasLibrary )`（`mageguild.cpp:91`），端口第一版按 `[3,3,2,2,1]` 初始化，图书馆镇
   永远少一格（1 级只出 3 个法术而不是 4 个）。修法：初值加 `+ 1`；同时把「已放几格」单独计数——参考往 vector
   里 `push_back`，端口是定长数组，原来的下标公式 `偏移 + 容量 - 剩余` 在有图书馆时会算成 −1。
3. **`ISpellBookOwner` 是参考 `HeroBase` 的一半**：法师塔只关心「有没有书、能不能学、学进去」，于是参数类型
   收窄到这个接口，英雄与队长共用一套训练代码；智慧术的等级门槛（1–2 级免费、3 级基础、4 级高级、5 级专家）
   留在各自的实现里，队长那边直接是「只学 1–2 级」。
4. **亡灵神殿要判版本**：`Shrine` 是 Price of Loyalty 才加的建筑，绘制顺序里它紧跟 `Spec` 之后，SuccessionWars
   的图不插。生成器为这一处专门输出 `WithShrine( [...], index, version )`，其余五族是纯列表。
5. **`GetVisitValue` 与参考刻意不同**：参考 `Troops futureArmy( heroArmy )` 复制的是**指针**，随后
   `Troop::Upgrade()` 会真的升级英雄自己的兵——评估一次访问顺手白升一队。端口按值复制（`Army.Assign`），数字
   一样，副作用没了。这条写进方法注释，属于「行为一致」的例外，故记录在此。
6. **世界缺席就变参数**：`world.GetHero( castle )` → `Hero? guestHero` 参数（`TrainGuestHeroAndCaptainInMageGuild`
   与 `BuyBuilding` 都有这个可选参数）；`GetKingdom().GetFunds()` → `in Funds kingdomFunds`；
   `GetForestMoatInfo`/`Battle::Tower` 这类要战斗的直接写明缺口。
7. **缺口不写成「空函数凑数」，每条都写为什么**：塔的守城强度（`Battle::Tower` 未搬，护城河与城墙的乘数照抄）、
   四条船的接口（`World` 没有船对象）、`ActionPreBattle`/`ActionAfterBattle`（没有 battle）、
   `GetICNBuilding`/`GetICNBoat`（端口按文件名索引 ICN，那张 400 行的 id 表要重写而不是翻译）、
   `DrawImageCastle`（没有城镇屏）。七个界面文件同理不搬：约 4700 行窗口/按钮/贴图/本地化，城堡屏还不存在。

**后果**：新增 `CastleBuildingInfo.cs`、`CastleBuildingInfo.Tables.cs`（脚本生成）、`MageGuild.cs`、
`Captain.cs`、`Heroes/ISpellBookOwner.cs`、`Castle.Modifiers.cs`、`Castle.Values.cs`、`Castle.Training.cs`、
`Castle.Description.cs`；改 `Castle.cs`（`partial`、`_mageGuild`/`_captain`、`PostLoad` 的两步、`BuyBuilding`
的训练分支）、`Hero.Spells.cs`（`CanLearnSpell`/`AppendSpellToBook`）、`Spell.cs`（`WeightForRace`、
`SuitableForSpellBook`）、`SpellStorage.Clear`、`GameStatic.BattleMoatReduceDefense`。`castle` 行
0/4/7 → **3/5/3**。测试新增 `CastleInfoTests` 19 条，`FHeroes2.Game.Tests` → 1566，全解决方案 → 2253，0 警告。

**遗留**：七个界面/绘制文件（`buildinginfo`、`castle_building`、`castle_dialog`、`castle_mageguild`、
`castle_tavern`、`castle_town`、`castle_well`）等城堡屏；`ActionNewMonth`（月度事件与招募池重置）与税收；
`GetGarrisonStrength` 缺塔的强度、`GetVisitValue` 缺 `ArrangeForCastleDefense` 的合军。

## ADR-135：agg 目录收尾——821 字节的 BIN、原版数据的十余处修补，以及三张生成表 ✅

**背景**：`agg` 目录 6 个 `.cpp` 此前只转了 `agg.cpp` 的两个入口，`icn.cpp`/`m82.cpp` 只到「名字表」。
剩下的是 `bin_info.cpp`（怪物动画的二进制解析 + 原版数据修补）、`mus.cpp`（三支音乐查询）、
`xmi.cpp`（曲目名与 `FromMUS`），以及 `icn.cpp` 的英雄头像与 `m82.cpp` 的法术音效、瓦片环境音。

**决定**：

1. **二进制解析按写死的字节偏移直读**，用 `BinaryPrimitives` + `ReadOnlySpan<byte>` 切片，不用
   `BinaryReader` 也不用 `StructLayout`：记录定长 821（`CORRECT_FRM_LENGTH`），字段散在
   1/3/5/117/118/138/158/162/166/170/174/176/186/187/235/239/243/277 等偏移上，其中有 16 位有符号、
   float、uint32 混排，`Record` 还要处理对齐与变长尾部，直读反而最贴近参考。
2. **原版数据的十余处修补照抄，不去「修正」数据**：狼的下方攻击帧 16→2、矮人的死亡动画换成 49…55、
   石像鬼与食人魔的移动序列重排、剑士补帧、巨人/巨魔/美杜莎/游侠/元素/游牧民的逐帧偏移。这些不是美化，
   是在补原版 BIN 的错误；少一条战场画面就与原版不一致，因此每条修补都在注释里标了参考行号（`bin_info.cpp:250-490`）。
3. **越界与坏数据都给确定的答案**：长度不是 821 就整体作废（只留 `moveSpeed = 450` 的默认值）；
   `IsValid` 要求七支必备动画（移动/站立/死亡/受击/三个方向的攻击）与 `idlePriority.Length == idleAnimationCount`；
   参考里会 UB 的地方（`frameXOffset[MOVE_ONE][7]`、`animationFrames[MOVE_MAIN].back()`、`binFileName[id]`）
   端口一律先判长度/范围——这些偏差写进注释，属于「不越狱」的替换，不改变正常数据的结果。
4. **`std::vector` 的修补换成「换新数组」**：`resize`/`insert`/`erase` 在端口无法原地做，于是
   `WithLength`/`Appended`/`InsertedAt` 各返回一个新数组赋回去；对外仍是 `int[][]`，调用方只读。
5. **缓存注入 `AssetBundle`，不用全局归档**：`MonsterAnimInfoCache( AssetBundle )` 对应参考的
   `Bin_Info::GetMonsterInfo( monsterID )`；失败的记录**不进缓存**（每次重试、每次都报警告），与参考一致。
6. **三张表全由脚本翻，不手抄**：`GenerateAggEnums.ps1` 扩到 `xmimap`（17 支 + 6 条原版别名，别名说明随注释带出），
   新增 `GenerateMonsterTables.ps1` 翻 `binFileName`。`MonsterType.MonsterCount` 是**哨兵不是怪物行**——
   第一版把它当第 74 行，表就多一行、`RANDOM_MONSTER_LEVEL_4` 还被当成「参考漏了」补了一个 `UNKNOWN`；
   实际参考 72 个初始化器对 72 个怪物是完整的，脚本改成读哨兵的值并严格校验两端数量。
7. **分层照旧**：文件/枚举查表进 `FHeroes2.Assets`（`M82Catalog`、`XmiCatalog`、`IcnCatalog` 的
   Captain/Building/Castle/Flag 四个 `getXxxIcnId`）；按游戏类型分支的查询进 `FHeroes2.Game`
   （`SpellSound`、`TileAmbientSound`、`MusicThemes`、`HeroPortraitIcn`、`MonsterAnimInfoCache`）。

**后果**：新增 `Assets/Audio/M82Catalog.cs`、`Assets/Audio/XmiCatalog.cs`、`Assets/Audio/M82FileNames.cs`、
`Assets/Audio/XmiFileNames.cs`、`Game/Monsters/MonsterAnimId.cs`、`Game/Monsters/MonsterAnimInfo.cs`、
`Game/Monsters/MonsterAnimInfoCache.cs`、`Game/Monsters/MonsterBinFileNames.cs`（生成）、
`Game/Spells/SpellSound.cs`、`Game/Maps/TileAmbientSound.cs`、`Game/AdventureMap/MusicThemes.cs`、
`Game/Heroes/HeroPortraitIcn.cs`；改 `AssetBundle.TryRead`/`IsPriceOfLoyaltyPresent`、
`tools/scripts/GenerateAggEnums.ps1`，新增 `tools/scripts/GenerateMonsterTables.ps1`。`agg` 行 1/2/3 → **6/0/0**。
全解决方案测试 2354 条全绿（Core 193、UI 145、Assets 379、Game 1637），0 警告 0 错误。

**遗留**：`agg` 到「名字与映射」为止，**播放**不在这个目录——`engine/audio.cpp` 与 `audio_xmi2mid.cpp`
仍是未转项，所以 `m82`/`xmi` 现在只回答「该放哪个文件」。`bin_info` 的消费者还没接上：怪物信息窗
（`dialog_monsterinfo` 未搬）里 `MINIMON` 的帧号依旧用 `(id - 1) * 9` 硬编码，`MonsterAnimInfoCache`
已经能给出正确帧序，等那个窗口搬过来时替换。

## ADR-136：`maps` 的收尾三批——动作位与分类谓词的边界、中性城堡为什么不能删旗、以及 `MapPosition` 为什么不改继承 ✅

**背景**：`maps` 目录只剩 `position.cpp` 与 `maps_tiles.cpp` / `maps_tiles_helper.cpp` 的尾巴。尾巴分两组：
一组是**摘除**（`removeObjectPartsByUID`、`removeMainObjectFromTile`、`removeObjectFromTileByType`、
`removeObjectFromMapByUID`、`restoreAbandonedMine`），另一组是**所有权旗与船**（`setOwnershipFlag`/`updateFlag`/
`setBoat`/`getBoatDirection`）。两组都碰同一件事：对象不是一个部件、一个瓦片，而是一个 UID 铺在若干瓦片上。

**决定**：

1. **`removeObjectPartsByUID` 的尾巴照抄，不简化**：删部件只是第一句，后面必须重算对象类型、
   本瓦片与**八个邻居**的通行（邻居可能被高物体的影子挡过），并把「英雄脚下藏着的类型」重新挂回英雄。
   少任何一句话地图都是「看着对、走不对」。参考里那句「顺手清元数据」的 TODO 参考自己也没做，端口保留同一个缺口。
2. **按类型摘除先查精灵、再按 UID 扩散**：`removeObjectFromTileByType` 比的是 `getObjectTypeByIcn()` 而不是瓦片的
   对象类型（主部件 → 顶层倒序 → 地面倒序，倒序是因为画得最晚的那片要让路），拿到 UID 后交给
   `removeObjectFromMapByUID` 做泛洪——只有真的摘掉部件的瓦片才向八邻扩散，代价跟着对象的大小而不是地图的大小。
3. **`isCaptureObject` 只认带位值：错的用例，不是错的代码**：参考的 `OBJ_MINE` 本身就是带动作位的值（端口
   `ActionMine`），`getMainObjectType(false)` 给回来的也正是它。原先的用例拿 `Mp2ObjectType.Mine` 造瓦片，
   谓词当然说「不是占领对象」——修的是用例（真实地图上矿井存的就是 `ActionMine`），代码保持「分类谓词比带位值、
   其余 switch 先剥位」这条分工。
4. **中性城堡的旗不能删**：参考把 `PlayerColor::NONE` 换成自己的 `UNUSED` 再画，于是两扇旗门留下中性精灵
   （`12`/`13`）而不是消失。端口没有 `UNUSED`，也不该为一句断言往 `PlayerColor` 里加一个 MP2 里不存在的值，
   因此把「画旗」与「按颜色收旗」拆成两个方法：`UpdateFlag`（参考的 `updateFlag`，`None` 即收旗）与私有的
   `UpdateFlagSprite`（只画，中性城堡那一支用它）。
5. **`CapturedObjects::ResetColor` 改成回答「清了哪些瓦片」**：参考在同一个循环里顺手重画旗子（靠全局 `world`），
   端口的数据类没有 world。于是它返回被清掉的瓦片号、由 `World.ResetCapturedObjects` 重画——工作与顺序不变，
   只是把「谁有权拿 world」这件事摆明。
6. **`MapPosition` 单独落地，不动 `MapObject` 的继承**：参考的 `MapBaseObject`/`ActionObject`/`ObjectInfo` 都继承它，
   但端口的地图对象是在**读图时按瓦片号**放置的，那一刻世界几何还不存在。`MapPosition` 因此提供 `center` 与
   `GetIndex`/`SetIndex(MapGeometry)`，由编辑器与存档格式使用；`MapObject` 继续持有 `TileIndex`（各文件里已写明）。
7. **调试 dump 用端口的枚举名**：`getObjectLayerName`/`getObjectPartInfo`/`String()` 是参考的调试输出，
   它打印自己的字符串表（`MP2::StringObject`、`Ground::String`、`Direction::String`）与英雄/城堡的 `String()`。
   端口打印数值 + 枚举名，并在注释里写明这是替换；英雄与城堡那两段留给它们的 `String()` 搬过来时补。

**后果**：新增 `Game/Maps/MapPosition.cs`、`TileOwnership.cs`、`TileObjectParts.cs`、`TileRemoval.cs`；
改 `Tile.cs`（文件概览注释）、`TileMetadata.cs`（补 `GetResourceOnTile`/`GetResourceCountOnTile`）、
`World.cs`（`CaptureObject` 接上旗子与城堡入口改色、`ResetCapturedObjects`）、`CapturedObject.cs`
（`ResetColor` 返回清单）、`TileMetadata.Mutators.cs`（遗留清单更新）。测试新增 `TileOwnershipTests` 28 条、
`TileRemovalTests` 8 条，全解决方案 2446 条全绿，0 警告。
覆盖率：`maps` 行 `4/7/6` → **`5/7/5`**，合计 **42/62/145** → **43/62/144**。

**遗留**：`maps_tiles_helper.cpp` 还差半径 `ClearFog`、`updateFogDirectionsInArea`、法术/军队几支与编辑器的
`setInitialObjectInfo`/`updateObjectInfoTile`/`updateRandomXxx`/`placeObjectOnTile`（要 `Artifact::Rand` 与
编辑器 `ObjectInfo`）；`Tile` 的迷雾位与存档运算符仍未搬；`maps` 目录里 `map_random_generator*` 三支未开。

## ADR-137：三个目录同步推进的第一刀——`speed`/`experience` 的文案、`GetRNDSize` 的特例表、以及军队三支为什么拆成 `Tile` 的方法 ✅

**背景**：`maps`/`heroes`/`kingdom` 三个目录一起收尾。`maps` 的尾巴只剩军队三支与编辑器的四项；
`kingdom` 的十四个文件里只有 `speed`/`experience` 是纯逻辑（其余是渲染与对话框）；`heroes` 的剩余文件
都是动画、战斗与对话框。

**决定**：

1. **`experience.cpp` 照搬两条英文原文**：参考把它们交给 `_()`（翻译表）。端口没有翻译层，返回原文
   就是「没有加载翻译时的参考行为」，比「因为要翻译所以不搬」更接近完成——名字与描述本来就是 UI 的输入。
   `speed.cpp` 的 `Speed::String()` 是另一回事：它是十档尺子的**名字**，随 UI 一起搬。
2. **`GetSlowerSpeed`/`GetFasterSpeed` 提升为公开方法**：参考里是 `speed.cpp` 的匿名辅助函数，但
   `getSlowSpeedFromSpell` 的 `extraValue == 0` 分支直接调用它们，测试也只能通过它们验证十档表；
   私有会让「法术没写 extraValue 时退一格」这条规则无法被单测钉住。
3. **slow/haste 的 `extraValue` 本来就是 0**：参考的 `ExtraValue()` 读的是**伤害法术**那一列，
   Slow/Haste 在表里没有值，所以两条分支实际都走「退/进一格」。端口照这个事实写，测试也照这个事实断言。
4. **`Monster::GetRNDSize` 放 `MonsterStatistics.GetRandomCount`**：它读的是等级与十六种特例
   （农民 80、盗贼 40、幽灵/美杜莎 20、骑兵 18、圣武士/凤凰 12……），属于数值表而不是实体；
   特例逐条 `switch`，其余按等级查 `{0,50,30,25,25,12,8}`。
5. **军队三支成为 `Tile` 的实例方法**（`GetTroop`/`UpdateMonsters`/`SetMonster`）：参考收
   `const Tile &`，端口所有瓦片辅助都是实例方法；`getTroopFromTile` 的「被占对象回答守军」正好对上
   `CapturedObject.Guardians`。
6. **`setMonsterOnTile` 的三种加入条件照抄优先级**：幽灵与元素生物永远 `SKIP`；地图写了数量的、
   以及本周正是自己那一族的堆叠一律 `MONEY`；其余 `Rand(1,10) < 3` 才是 `FREE`。
7. **新精灵的 UID 由世界发**：主部件已经是 `MONS32` 时保留 UID，否则把旧精灵压到地面层再取
   `GetNewObjectUid()`。新世界计数器从零开始，第一枚 UID 是 1——测试钉这个事实，不写「大于零」。

**后果**：新增 `Game/Kingdom/Speed.cs`、`Game/Kingdom/Experience.cs`、`Game/Maps/TileTroops.cs`；
`MonsterStatistics` 增 `GetRandomCount` 与等级表。测试新增 `KingdomSpeedTests` 19 条、
`TileTroopsTests` 24 条，全解决方案 2489 条全绿，0 警告。覆盖率：`kingdom` 行 `4/6/4` → **`6/6/2`**；
`maps` 行仍 `5/7/5`（helper 仍差编辑器的四项）。

**遗留**：`maps` 差编辑器四项与 `map_random_generator*` 三支；`heroes` 差 `heroes_dialog`/
`heroes_indicator`/`heroes_meeting`/`heroes_spell` 与 `heroes_move` 的 `GetIndexSprite` 精灵表、
行走音效、淡入淡出；`kingdom` 差 `kingdom_overview`/`view_world` 与 `kingdom.cpp` 的 `ActionNewMonth`、
`week.cpp` 的 `GetName()`。

## ADR-138：路线预览的箭头为什么是表而不是 260 行嵌套 `switch` ✅

**背景**：`route.cpp:53-319` 的 `Route::Path::GetIndexSprite()` 用八层嵌套 `switch`（8 个 `from` × 7 个
`to`）选出 `ICN::ROUTE` 里的箭头。它在 `heroes_move.cpp` 之后一直记作「等渲染侧」，其实没有任何渲染
依赖——输入两个方向与一个代价，输出一个整数。

**决定**：

1. **八乘八的偏移表 + 行/列索引**：每格是 `sbyte`，`-1` 表示「没有这支箭头」（参考里 `index = 0`）。
   八层嵌套 `switch` 与一张表是同一份数据，表能一眼看出「每行都有七个值、缺的那个正是回头的方向」，
   也便于逐格对照参考——这正是移植时最容易抄错的地方。
2. **块起点照抄五个代价**：100/125/150/175/200 对应 1/25/49/73/97/121，其余一律 1。参考的 `switch`
   没有 `default` 分支只是因为它先把 `index` 初始化为 1，端口写成 `_ => 1` 把同一件事写明。
3. **表不对称**：`Top→BottomLeft` 是 5，`BottomLeft→Top` 是 16——箭头的尾和头形状不同，参考两侧都存，
   端口两侧都抄，测试也各钉一条。
4. **`Direction` 是位标志，表用位置索引**：`Direction.TopLeft = 1`、`Top = 2`……不能直接当下标，
   于是有一个 `DirectionIndex()` 把八个方向折成 0..7（顺时针从左上开始），未知方向回 `-1`。

**后果**：`HeroPath` 增 `GetIndexSprite`、`ArrowOffsets`、`DirectionIndex`；`HeroPathTests` 增 21 条
（五块起点 + 两个越界代价、九对方向、五对无箭头），Game 测试 1772 → 1793，全解决方案 2510 条全绿。

**遗留**：`Route::Path::String()`（调试串）、`heroes_move` 的行走音效与淡入淡出仍未搬。

## ADR-139：`system` 目录——位标志、六个玩家槽、一个单例，以及不搬的那半目录 ✅

**背景**：`src/fheroes2/system/` 只有八个文件，但它们是全局状态的源头：`bitmodes`（三个位操作的基类）、
`players`（六个玩家槽、控制者、`Focus` 相机）、`settings`（进程唯一的一份设置，约 900 行）、两个版本头
（`version.h`/`save_format_version.h`）与 `ai/ai_personality`。同一目录里还有 `settings.cpp` 的配置读写
（走 `tinyconfig`）与 `dir.cpp` 式的文件搜索，两者都不属于「游戏状态」。

**决定**：

1. **`BitModes` 原样搬**：`SetModes`/`ResetModes`/`HasModes`/`HasAllModes` 与一个 `uint Modes`。参考的
   `Player` 同时继承 `BitModes` 与 `PlayerControl`（一个只有四个值的枚举），C# 不能多继承，所以端口只继承
   `BitModes`，把 `PlayerControl` 降成属性 + `IsAi()`/`IsHuman()` 扩展方法。`[Flags]` 保留了 `Human|Ai`
   这种合法组合（开局前「可人可机」的槽）。
2. **`Players` 同时保留两种形态**：参考的实例列表（`std::vector<Player*>`，`Settings::Players` 持有）与
   按颜色的静态查表（`Color::GetIndex(color)` 的六格数组）都搬。实例列表决定回合顺序（按色枚举升序，而不是
   文件头里的书写顺序）；静态表让 `Players::Get(color)` 保持参考的 O(1)。`Settings::Get()` 里的
   `Players` 就是那张实例列表——参考的持有关系原样照抄。
3. **`Get(None)` 回 `null`，不复刻 `Color::GetIndex(NONE) == 0`**：参考里查 `Color::NONE` 会撞上索引 0
   （蓝色槽），全靠调用方自己先判空。端口把「没有颜色就没有玩家」写成显式契约，`GetInPlayOpponents(None)`
   也因此回空表。
4. **`Focus` 从 `std::pair<int32_t, void*>` 换成两个类型化引用**：参考用 `void*` + kind 标记，读的时候
   必须自己按 kind 强转。端口存 `Hero?`/`Castle?` 两个字段，`Kind` 与字段一起改，`GetHeroes()`/`GetCastle()`
   只在 kind 匹配时给引用——陈旧引用读不回来。`Reset()` 两边都清。
5. **AI 自动接管是两段式**：`SetAiAutoControlMode(true)` 立即生效（`EffectiveControl` 变 `Ai`），关掉只写
   「计划」位，等 AI 回合结束由 `CommitAiAutoControlMode()` 落地。这是参考 `Player::setAIAutoControlMode` /
   `commitAIAutoControlMode` 的原样：不能在 AI 还在搬的时候把控制权抢回去。`Control` 本身永远不动，所以
   「一手人类一手电脑」的槽信息不会丢。
6. **`SetStartGame` 的四遍照抄顺序**：`SetPlay(true)` → `Focus.Reset()` → 拿掉已被占的种族 → 解析
   `Random` 种族 → 把 `Human|Ai` 收敛成 `Ai`。顺序有意义：先清相机再发种族，最后才定控制者，于是
   `GetColors(Human)` 在开局后只会看到真正的单人类。
7. **`Settings` 保持单例，但补一个 `internal static Reset()`**：参考就是 `Settings::Get()` 的懒汉单例，
   没有「重新加载」入口（配置来自文件，只读一次）。测试需要一份干净的首跑默认值，于是加了一个只对
   `InternalsVisibleTo` 的测试工程可见的 `Reset()`，注释写明它不是参考的一部分。
8. **不搬配置读写、文件搜索与翻译**：`tinyconfig`、`dir.cpp` 的 `SearchFile`/语言探测、以及所有 `_()`
   文案都不在本次范围（ADR-017/137 已有同类决议）。于是 `Settings` 只提供内存里的 setter，`GameLanguage`
   是一个字符串，名字类文本（`PersonalityString`）返回英文原文。
9. **数值 setter 一律钳制**：音量 0..10、AI 速度 0..10、英雄与战斗速度 1..10、滚动速度
   `None..VeryFast`。参考在 UI 层钳，端口在 setter 里钳——调用方有两个（UI 与测试），钳在唯一入口更稳。
10. **`EnablePriceOfLoyaltySupport(false)` 顺手退回原版音乐**：参考在同一处把 `MidiExpansion` 改成
    `MidiOriginal`；其余音乐来源（`External`/`MidiOriginal`）不动。这是一条容易漏的联动，单测专门钉住。
11. **`IsEvilInterfaceEnabled` 的三种模式与 AI 回合**：`Good`/`Evil` 直接回答；`Dynamic` 看当前玩家的种族，
    当前是电脑时按**倒序**找最后一个人类玩家——屏幕不能因为电脑接管了一回合就翻成邪恶界面。
    本次移植时该属性曾漏写，`SwitchToNextInterfaceType()` 的循环方向也写反（写成
    `Evil→Good→Dynamic`），一并按 `settings.cpp:1014-1045`/`1057-1070` 修正：现在循环是
    `Dynamic → Good → Evil → Dynamic`。
12. **`version.h`/`save_format_version.h` 顺带落地**：`EngineVersion`（1.1.18）与 `SaveFileFormat`
    （当前 10034、最低可读 10010）都是常量，没有依赖，留给存档章节直接用。

**后果**：新增 `Game/System/` 下 `EngineVersion`、`SaveFileFormat`、`BitModes`、`PlayerControl`、
`AiPersonality`、`PlayerFocus`、`Player`、`Players`、`Settings` 九个文件。测试新增
`SystemSettingsTests`（17 条）、`SystemPlayersTests`（14 条），加上 `PlayerTests`（6 条）、
`PlayerFocusTests`（3 条）、`SystemMiscTests`（5 条），共 45 条；`MapRaces` 的五个辅助（`Basic`/`Random`/
`IsEvil`/`IsMagical`/`Name`，`Settings` 的依赖）另在 Assets 侧补 `MapRaceTests` 4 条。Game 测试 1844 条、
全解决方案 2565 条全绿，`-warnaserror` 0 警告。覆盖率：`system` 行 `0/2/1` → **`3/0/0`**。

另需注意：新命名空间 `FHeroes2.Game.System` 会遮蔽 BCL 的 `System`，同程序集里凡是用
`System.Collections.IEnumerator` / `System.Text.StringBuilder` 的位置都得写 `global::`（本次修了
`AllCastles`、`MultiFontText` 与测试里的 `MonsterAnimInfoTests`）。

**遗留**：`settings.cpp` 的配置文件读写（`Read/Save`、`tinyconfig` 的 INI 解析）、`dir.cpp` 的文件搜索、
`system.cpp` 的 `GetString` 错误码文案、`audio` 的开闭与 `Settings` 的联动都还没搬——它们需要文件系统与
音频层，不属于「状态」。

## ADR-140：`audio` 目录——把线程换成 `Update()`、把混音器换成接口，以及循环音效的三步计划 ✅

**背景**：`src/fheroes2/audio/` 只有一个 `audio_manager.{h,cpp}`（119 + 961 行），但它同时装着四件事：
声音从哪来（AGG 里的裸采样包一个 WAV 头、音乐来自音乐文件夹或归档里的 XMI）、任务怎么排队
（`AsyncSoundManager` 的线程与三种优先级）、循环音效怎么复用通道（`playLoopSoundsImpl` 的配对算法），
以及直接对着 SDL_mixer 说话（`Mixer::`/`Music::` 两套全局函数）。这四件事在参考里被线程和全局状态缝在一起，
没法单独验证。

**决定**：

1. **一个文件拆成九个类型**：`AudioManager`（门面，唯一知道「声音从哪来」的地方）、`AudioTaskQueue` +
   `AudioTask`/`AudioTaskType`/`MusicRequest`/`LoopSoundRequest`（`AsyncSoundManager` 的簿记）、
   `AudioLoopEffects` + `AudioLoopPlan`（循环音效的规则）、`IAudioDevice`/`IAudioSource`（两处外部世界）、
   `MusicIdentity`（64 位曲目身份）、`MusicRestorer`（参考的作用域对象）。
2. **工作线程换成显式的 `AudioManager.Update()`**：参考起一个线程、用条件变量唤醒来避免「起 MIDI 卡住地图」。
   端口把同样的决策做成每帧排空一次的队列，宿主在主循环里调一次。参考的线程最终也只是独占队列，
   语义等价；换来的是整套逻辑可在单测里被驱动，且不用给每种请求加锁。
3. **任务优先级与「取出即出队」照抄**：`TryTakeNext` 先音乐、再音效（FIFO）、最后循环音效，取出时立即从
   队列里摘掉并把 `Running` 置成该类型。`RemoveMusicTask`/`RemoveSoundTasks`/`RemoveAllSoundTasks`/
   `RemoveAllTasks` 无条件清 `Running`——参考的 `_taskToExecute` 就是被这么清的，否则一条已经取出但还没执行的
   任务会把刚下的取消令撤回去（本次移植时这三处早退是错的，已按 `audio_manager.cpp:150-250` 删掉）。
4. **设备与来源注入**：`IAudioDevice` 覆盖 `Mixer::Play/Stop/setPosition/isPlaying` 与 `Music::Play/Stop/
   isPlaying`，通道只是一个不透明的 `int`（参考用 SDL 的通道号）；`IAudioSource` 给出音乐文件夹、音效裸数据与
   已经转好的 MIDI（XMI→MIDI 的转换留在 Assets 侧，管理器不该知道 XMI 怎么变 MID）。参考里这些都是进程全局，
   端口把它们变成构造参数，于是 fake 设备就能测完整流程。
5. **两种缓存都缓存失败**：`GetWAV()`/`GetMID()` 读不到时把 `null` 也记下来。归档在磁盘上，一次读不到不会因为
   再读而变成能读，缓存失败状态反而避免每帧重试。MIDI 另加 `ignoreExpansion = track >= MidiOriginalKnight`：
   原版曲目不在扩展归档里，扩展档必须在读它们时让开（`LoadMID` 的同一条规则）。
6. **`MusicIdentity.Get` 对哨兵抛异常**：参考在高 32 位放 `MusicSource`、低 32 位放 `MusicTrack`，用
   `assert` 挡住哨兵。端口改成 `ArgumentOutOfRangeException`——一个能和真实曲目撞号的身份比没有身份更糟。
7. **循环音效拆成「算计划」+「动设备」**：参考在算法中间直接调混音器，端口让 `AudioLoopEffects.Plan` 返回
   `ChannelsToMove`/`ChannelsToStop`/`EffectsToStart`，调用方按「先移动、再停、后起」执行，新起的通道用
   `Register` 回报。顺序不能换：被复用的通道若先被停掉，后面那次移动就落在死通道上。这一拆也让「复用规则」
   可以脱离声卡断言。
8. **3D 开关翻转时全部重来**：`Plan` 发现 `is3DAudioEnabled` 与记录不同，先把所有还在响的通道列进
   `ChannelsToStop` 并清表，再照常配对。位置从「无」变「有」时旧通道的位置信息是脏的。
9. **配对规则按参考的「只跳过严格更差」**：角度优先、距离破平；参考比较时用 `<`，所以角度与距离都相等的
   候选**后找到者胜**（不是先找到者）。测试最初按「先到先得」写，落地后改成
   `ATieGoesToThePairThatWasFoundLast`，`<remarks>` 一并写明。
10. **音乐的三级回退顺序**：设备里已加载的曲目（参考的音乐数据库）→ 音乐文件夹（只在 `External` 模式）→
    XMI 归档（`MidiExpansion` 先试扩展档，找不到再落回原版）。文件夹里没有某支曲子不等于没有这支曲子，
    所以 `External` 找不到时继续往下走而不是直接返回。
11. **「已经在响就别重起」与 `PlayCurrentMusic`**：同一曲目且设备确实在放时直接返回（否则每次进界面都会把
    曲子从头切一次）。`PlayCurrentMusic` 先清掉当前曲目再以 `ResumeAndPlayInfinite` 重放，曲目是哨兵时只清不放。
    `PlayMusic`/`PlayMusicAsync` 都会记住 `LastRequestedMusicTrack`，即便这次请求的是哨兵——「先停音乐再要回来」
    的那一屏不该拿回上一支曲子。
12. **`MusicRestorer` 用 `IDisposable` 替代参考的栈上对象**：构造时记下 `LastRequestedMusicTrack`，`Dispose` 时发
    `PlayMusicAsync(track, ResumeAndPlayInfinite)`。C# 的 `using` 与参考的花括号作用域一一对应。

**后果**：新增 `Game/Audio/` 下十个文件（`AudioManager` 423 行、`AudioLoopEffects` 314 行，其余更小）。测试
`AudioLoopEffectsTests`（10 条）、`AudioTaskQueueTests`（8 条）、`MusicIdentityTests`（3 条）、
`AudioManagerTests`（16 条，fake 设备 + fake 来源）共 37 条；Game 测试 1844 → **1881**，全解决方案 **2602**
条全绿（Core 193、UI 145、Assets 383、Game 1881），`-warnaserror` 0 警告。覆盖率：`audio` 行 `0/0/1` →
**`1/0/0`**，合计 **37/61/151** → **38/61/150**。

**遗留**：真正对着 SDL3_mixer 的实现没有搬进这一层——`src/FHeroes2.App/Audio/AudioPlayer.cs` 仍是独立的一套
（直接放外部音乐文件、单曲循环、音效名走 `GameSound.GetArchiveName`）。要把它换成 `AudioManager` 需要一个
`IAudioDevice` + `IAudioSource` 的 SDL3_mixer 实现，属于 App 组合根的活，等界面的音乐切换点（`MusicRestorer`
的调用方）一起做。`audio_manager.cpp` 里 `Music::Xmi2Mid` 的转换本身也在 Assets 侧，本层只消费结果。

## ADR-141：`h2d` 目录——把格式读取放进 Assets、把样本表留在 Game，写回与参考逐字节一致 ✅

**背景**：`src/fheroes2/h2d/h2d.cpp` 只有 151 行，真正的格式在 `src/engine/h2d_file.cpp`（236 行）里：一个
「魔数带版本 + 记录表 + zlib 体」的容器，加一个 17 字节头的 sprite 编码。`h2d.cpp` 只做两件事——开档时把
`resurrection.h2d` 的内容与一张 68 条的样本名单比对，以及在有人要图时按名字取。这张名单是「游戏认为资源该
长什么样」的策略，容器则是纯格式，两者不该待在一起；而且不搬 `h2d_file.cpp` 就根本转不了这个目录。

**决定**：

1. **两个文件一起搬，按层落位**：`H2DArchive`/`H2DWriter`/`H2DImageCodec` 进 `FHeroes2.Assets.H2D`（与
   `IcnSpriteFile`、`Mp2MapFile` 同级，`Sprite` 在 Core，所以 Assets 造得出图）；`h2d.cpp` 的样本表、开档
   校验与 `Current` 门面进 `FHeroes2.Game.H2D.H2DResources`。于是「这版游戏需要哪些 h2d 内容」改不改只影响
   Game，「h2d 文件怎么解析」只影响 Assets。
2. **zlib 只需要一个入口**：`zzlib.cpp` 的 `unzipData`/`zipData` 在 H2D 上表现为标准 zlib 流（RFC 1950），
   `ZLibStream` 直接对上，和 PNG 的取法一致；压缩级照 `zipData( …, true )` 取 `CompressionLevel.SmallestSize`。
   `zzlib.cpp` 因此仍记 ◐——AGG 侧不需要解压，它缺的只是「按需分配 + 失败返回空」这套 C 接口。
3. **坏记录跳过、坏头部报错**：空名、`size == 0`、`offset + size` 越过文件末尾的记录整条跳过，重名取第一条
   （`std::map::try_emplace` 的行为）；但头部本身读不下去（长度前缀超出剩余字节、`count` 明显放不下）改成抛
   `BinaryFormatException`——参考会读出一串 0 然后静默 `continue`，那种静默只会让「文件截断」伪装成「档里没这张图」。
   `minFileSize = 22` 照抄，注释说明它是参考随手定的下限而不是真实最小档。
4. **图像体长度必须精确相等**：`data.size() == 17 + w*h*(单层 ? 1 : 2)`。非正尺寸直接拒绝——参考按 `size_t`
   乘出天文数字后同样不匹配，结果一致但没有溢出路径。单层图不分配透明层（参考是先 `disableTransformLayer()`
   再 `resize`）。
5. **写回按名字序数序**：记录表在前、体在后，`offset` 从「记录段 + 8」起算，字符串是 `uint32` 长度 + 字节，
   `put( bool )` 是 1 字节 0/1。`Serialize()` 是参考 `write( path )` 的内存版（测试与 `AssetProbe` 用），
   `Write( path )` 保留它的 `bool` 语义（内容为空、或写不进磁盘时返回 false，不抛）。
6. **样本表是 `FrozenSet`，比较用序数**：`std::string::operator<` 按无符号字节比较，68 个名字都是 ASCII，
   序数比较给出同样的顺序和同样的判断。三条失败路径的消息照参考：找不到档、打不开、名单不符（`FileNotFoundException`
   / `BinaryFormatException` / `InvalidOperationException`），最后一条还把文件名带回给用户。
7. **没有 `Settings::findFile` 就显式给路径**：`GetDefaultPath( dataDir )` 拼 `files/data/resurrection.h2d`，
   `Current` 只由组合根赋值；`TryReadCurrentImage` 在没装档时返回 false，等价于参考里初始器还没跑时的全局 reader。
8. **对拍放进工具而不是测试**：`AssetProbe h2d <file>` 打记录表、每条体的压缩/解压长度与每张图的尺寸，测试则用
   自造的档覆盖畸形布局。用 `F:\game\fh2\files\data\resurrection.h2d` 实测：68 条记录、68 张图全部解出、
   名字集合与参考源码里的样本表完全一致（`Compare-Object` 无差异）。

**后果**：新增 `Assets/H2D/` 三个文件（`H2DArchive` 233 行、`H2DWriter` 150 行、`H2DImageCodec` 139 行）
与 `Game/H2D/H2DResources`（205 行，含 68 条样本表）；`AssetProbe` 多一条 `h2d` 命令。测试
`H2DArchiveTests` 19 条、`H2DImageCodecTests` 10 条、`H2DResourcesTests` 8 条，共 37 条：Assets 383 → **412**、
Game 1881 → **1889**，全解决方案 2602 → **2639** 全绿，`-warnaserror` 0 警告。覆盖率：`h2d` 行 `0/0/1` →
**`1/0/0`**，`engine` 行 `7/7/12` → **`8/7/11`**（`h2d_file.cpp` 从 ❌ 移入 ✅），合计 **38/61/150** →
**40/61/148**。

**遗留**：读原版存档时才用到 h2d 的那条链路（`Game::LoadSAV` 里的 h2d 兜底图）还没接，等 `serialize.cpp` 的
批次；`AssetProbe h2d` 只解容器与 sprite，不解 `.GAM`/`.SAV`。

## ADR-142：`resource` 收尾——档位/定价/随机/交换四件套，档位为什么与地图版本绑定 ✅

**背景**：`resource` 四个文件里，`resource.cpp` 早已转完，`artifact_ultimate.cpp` 一直没动，`artifact.cpp`
（1438 行）只搬了 `BagArtifacts` 的 14 格容器与两种求和，`artifact_info.cpp` 只差两支文案。上一轮（ADR-101）
明确留了这几支：`Artifact::Level`/`LoyaltyLevel`/`isUltimate`、`getArtifactValue`、
`getTotalArtifactMultipliedPercent`、`Rand`/`ResetArtifactStats`/`ExcludeArtifactFromRandom`、
`getArtifactFromMapSpriteIndex`、`exchangeArtifacts`、`assembleArtifactSetIfPossible`、
`sortFromWorstToBest`，以及 `UltimateArtifact`。它们互相咬合——定价要档位、交换要定价、地图装载要随机与精灵
索引——所以一起做。

**决定**：

1. **`Artifact` 类不单独造**：端口已经有 `ArtifactType`（105 项 id）与 `ArtifactTable`（数值表），再加一个只
   包一个 id 的引用类型，只会让每个调用点多一层拆包。参考里 `Artifact` 自己的成员按语义拆成四个静态类：
   档位进 `ArtifactLevels`，价格进 `ArtifactValues`，随机进 `ArtifactRandom`，地图精灵索引进
   `ArtifactMapSprites`。
2. **档位只由「地图版本 + id」决定**：参考读 `Settings::Get().getCurrentMapInfo().version`，端口把版本当参数
   传，与 `Castle` 的建筑表同一个理由——没有设置单例也能调用。于是 `Level( artifact, version )`、
   `Next( level, version )`、`FromMapIndex( index, version, hasExpansion )` 三个入口都显式带版本。
3. **`.mp2` 上资料片宝物只有 `Loyalty` 位**：参考对 `SuccessionWars` 直接返回 `ART_LOYALTY`，不叠
   `LoyaltyLevel()`。这不是表没写完——随机只认 `Treasure`/`Minor`/`Major`/`Ultimate` 四种位，不带档位的宝物
   自然抽不到，等于「旧格式地图不该出现资料片宝物」。端口照抄，注释里点明这是机制。
4. **随机与禁用共用一张位图，且是进程级静态**：参考一个 `uint8_t` 数组、`ART_RNDDISABLED` 与 `ART_RNDUSED`
   两位；`ResetArtifactStats()` 整表清零（连禁用一起），`ExcludeArtifactFromRandom()` 只置禁用位。端口保留
   进程级静态（还没有「地图」对象能拥有它），`Reset()` 全清——因此测试必须串行：`ArtifactRandomCollection`
   （`DisableParallelization = true`）加每个用例 `IDisposable` 前后各 `Reset()` 一次。
5. **`getTotalMultipliedPercent` 返回列表而不是和**：乘法加成不累加（两个「+50% 寒冷伤害」不是「+100%」，参考
   逐件乘进法术），所以返回 `IReadOnlyList<int>`。非乘算类型上调是编程错误，参考 `assert` 后给空表；端口只给
   空表、不 assert——xUnit 把 `Debug.Assert` 当失败抛出，留着它就等于「这条契约不可测」，而空表本身正是要钉
   的行为。
6. **卷轴不带具体法术，交换里按「空卷轴」处理**：参考的 `Artifact` 有一条 `ext` 存法术 id，端口没有，因此
   `getSpellId()` 恒为 `SpellType.None`。两个后果都照参考对空卷轴的行为走：卷轴永远不会被判为「法术已在书
   里」，两张卷轴彼此不可区分。等卷轴界面落地时再补 `ext`。
7. **交换拆成独立的 `ArtifactExchange`**：这方法要读英雄的副技能与法术书，`ArtifactBag` 不该认识 `Hero`。
   流程与顺序照参考：池化（魔法书不动）→ 纯诅咒宝物先还给 giver → 死灵法师保留死灵系宝物 → 已知法术的卷轴
   让与 → 唯一宝物（`isUltimate` 与套装件）的重复件让与 → 按价值升序发牌，taker 从尾部取到
   `MaxCapacity - 1`（给法术书留一格）→ 剩下的按 giver 的空位回吐 → 双方各跑一次 `AssembleArtifactSets()`。
8. **排序给出确定语义**：参考的 `sortFromWorstToBest` 比较器在任一侧是魔法书时一律返回「不小于」，不是严格
   弱序（书与一切等价，空位按价值排到最前），`std::sort` 的结果未定义。端口定义成：书占第一格、其余按
   `ArtifactValues.Value` 升序、空位在尾部——这正是参考想要的可见效果，也保证 `Push` 仍从第一个空位填。
9. **套装装配是 `while` 不是 `if`**：参考每装成一套就重新检查整张表，所以重复的三件套能装两次、套装件也能再
   进另一套。先删零件再 `Push` 成品，袋满也放得进去；`Push` 仍失败只 `Debug.Assert`（参考不看返回值）。
10. **`UltimateArtifact` 用组合而不是继承**：参考 `UltimateArtifact : Artifact`，端口没有 `Artifact` 类型，
    于是它是独立类（`Artifact` 值 + 瓦片索引 + `IsFound` + `Offset`）。`Set` 收到无效 id 时改抽随机终极，偏移
    取 `Rand(0,4)-2`/`Rand(0,2)-1`（不让宝物永远落在拼图正中），`Reset()` 回到 `-1` 与零偏移。

**后果**：新增 `Game/Artifacts/` 七个文件（`ArtifactLevel`、`ArtifactValue`、`ArtifactRandom`、
`ArtifactMapSprites`、`ArtifactSets`、`ArtifactExchange`、`UltimateArtifact`），`ArtifactBag` 追加十个成员。
测试 106 条：`ArtifactLevelTests` 35、`ArtifactValueTests` 24、`ArtifactRandomTests` 10（串行集合）、
`ArtifactMapSpritesTests` 19、`UltimateArtifactTests` 6、`ArtifactExchangeTests` 7、`ArtifactBagTests` 追加 5。
Game 1889 → **1995**，全解决方案 2639 → **2745** 全绿，`-warnaserror` 0 警告。覆盖率：`resource` 行 1/2/1 →
**2/2/0**，合计 **40/61/148** → **41/61/147**。

**遗留**：`artifact.cpp` 仍记 ◐——`Artifact::GetName`/`getDiscoveryDescription` 的文案、`String()` 与
`getTotalArtifactEffectValue( bonus, description )` 的 `description` 重载（都是文字），以及 `ArtifactsBar`
（1104-1381 行，装备栏的点击/拖放界面）。`artifact_info.cpp` 的两支描述文案同理；`SetSpell` 的 `ext` 字段随
卷轴界面一起补。序列化（`operator<<`/`>>`）与 `GetPuzzleMapSurface` 按惯例留在 serialize/UI 批次。

## ADR-143：`spell` 收尾——谓词/定价进 `Spell`，数值公式进 `SpellInfo`，页状态进 `SpellBook` ✅

**背景**：`spell` 四个文件里 `spell_storage.cpp` 早已转完（ADR-102），另外三个都只搬了一半：`spell.cpp` 有数值表、
等级与七条特性谓词，但目标/元素/守卫那批谓词、`spellPoints(hero)` 的四类神器减耗、`SuitableForSpellBook` 与
随机查询都没落；`spell_info.cpp`（426 行）整份没动；`spell_book.cpp` 也没动。三个文件是同一个调用链——法师公会
要 `SuitableForSpellBook`，法术书要过滤与翻页，施法要减耗后的费用——所以一起做。

**决定**：

1. **不造 `Spell` 值类型，也不搬运 `Hero`**：端口沿用既有形状——`SpellType`（id）+ `SpellTable`（73 行）+ 只读
   包装 `Spell`。参考里 `spell.cpp` 的成员方法全部落在 `Spell` 上，唯独 `spellPoints( const HeroBase * )` 换成
   `SpellPoints( ArtifactBag? )`：它真正需要的只是英雄身上的乘法减免，传空袋就等于参考里的 `hero == nullptr`。
   这样 `Spells/` 目录不认识 `Hero`/`HeroBase`，层依赖不倒退。
2. **减耗逐件乘、最低 1 点**：`GetTotalMultipliedPercent` 返回的是列表（ADR-142），照参考逐件 `cost = cost*(100-p)/100`
   而不是求和，四次整数截断保留原顺序；最后钳到 1（参考的 `std::max<u32>( 1, ... )`）。四种减免类型（祝福/诅咒、
   四种召唤、四种精神法术）各映射到哪个 `ArtifactBonusType` 与参考的 switch 一致。
3. **`SuitableForSpellBook` 修掉旧的错误排除**：上一版手写排除了 `ViewMines`（怕它进公会）。参考只按
   `Level() >= 1` 过滤：`ViewMines`/`ViewResources` 都是一级法术、本来就该出现；占位符、`Petrify` 与 `None` 的等级
   是 0，自动出局。`exclude` 参数对应参考的 `spellToExclude`（巫术/法师塔的抽签用）。非法等级（0、>5、<-1）返回空表，
   照参考的 `assert` + 空表。
4. **随机抽法术用端口自己的 `Rand`**：参考在 `Rand::Get( 0, count-1 )` 上按等级均匀抽，端口 `RandomSpell` 同样在
   `SuitableForSpellBook( level )` 的结果上均匀抽；空表抛 `ArgumentOutOfRangeException`（参考的 `assert` 换成语义
   明确的异常，因为它会被法师公会每层调用一次，静默返回 `None` 更容易掩盖表错误）。
5. **`spell_info.cpp` 的数值公式只用「法术点 + 神器袋」两参数**：六条公式（伤害、召唤数量、治疗量、复活量、守卫
   数量、催眠 HP）读的是法术的 `extraValue` 与三/四类乘法百分比，`ArtifactBag?` 足够；地图上的四条查询（最近城堡、
   召唤船落点、可召唤船、英雄是否近水）收 `World`/`Hero`，因为参考用的就是它们。`getSpellDescription` 是文案拼装
   （连英雄名字、最近城的名字都要），按惯例不转，留在 UI 批次。
6. **`MapGeometry?` 走模式匹配**：`world.Geometry` 在空世界（`new World()` 还没装图）是 `null`，参考在那种状态下
   不可能被调用，端口选择返回 `-1`/`false` 而不是抛异常——这四支都是「问一句现在能不能放船」，没有世界就等于不能。
7. **召唤船查 `ActionBoat` 而不是 `Boat`**：参考的 `MP2::OBJ_BOAT` 带动作位（`mp2.h:315`），而 `GetObjectPositions`
   在 `ignoreHeroes = false` 时比的是**缓存**的类型，`Tile.SetBoat` 缓存的正是 `ActionBoat`。测试第一次跑出 `-1` 就是
   这一处：查非动作值永远空表。这条抄错的枚举值已改并留注释。
8. **保留参考的两个怪癖**：`getSummonableBoat` 返回的是「距离 > 1 的第一条船」而不是最近的船（`GetObjectPositions`
   已按距离排过，所以通常体现不出来，但它是参考的写法）；原版地图上 AI（`isControlAI` 且非 `isAiAutoControlMode`）
   不能召唤中立船。
9. **`SpellBook` 只搬规则与页状态**：参考里英雄的法术书**就是** `SpellBook`（继承 `SpellStorage`）。端口把英雄的
   存储留作 `SpellStorage`，`SpellBook` 只管「当前标签、首法术下标、四个翻页动作、`Open` 的起始状态块」，并且
   `SetFilter` 是**静态**方法——返回一份排好序的列表，不动英雄的书。理由：玩家上次看到第几页是 UI 状态，没有规则读它；
   而 `SetFilter` 每次切标签都要重建列表，做成实例方法就必须先给 `SpellBook` 塞一份书。
10. **卷轴暂时加不进法术书**：`SetFilter` 照参考「先复制书、再追加神器上的卷轴」，但卷轴还不带具体法术（ADR-142），
    所以这一步现在贡献为零；补 `ext` 的那天这一支不用改。

**后果**：新增 `Game/Spells/SpellInfo.cs`、`Game/Spells/SpellBook.cs`；`Spell.cs` 追加十五条谓词、`SpellPoints`、
`DimensionDoorDistance`、`TotalSpellsPerLevel`、`RandomSpell`，并修正 `SuitableForSpellBook`。测试 27 条新增：
`SpellTests` 追加 10（治疗量、目标谓词、元素/守卫、四类减耗与最低 1 点、公会法术表 12/11/16/16/10 共 65 条、
`View` 法术可学、排除集、随机等级、传送门 14 格）、`SpellInfoTests` 8、`SpellBookTests` 9。Game 2005 → **2022**，
全解决方案 2745 → **2772** 全绿，`-warnaserror` 0 警告。覆盖率：`spell` 行 1/1/2 → **1/3/0**，合计
**41/61/147** → **41/63/145**。

**遗留**：`spell.cpp` 仍记 ◐——73 个法术名与描述文案（参考走 `_()`，端口没有翻译层）、`operator<<`/`>>`；
`spell_info.cpp` 仍记 ◐——`getSpellDescription` 的文案拼装；`spell_book.cpp` 仍记 ◐——`SpellBook::Open` 的绘制与
输入循环（同一批的 `ArtifactsBar` 也在等 UI 批次）。

## ADR-144：`army` 收尾——八种固定守卫、四个 `Arrange*`，以及被放开的两条「满军」断言 ✅

**背景**：`army/` 四个 `.cpp` 里 `army.cpp` 与 `army_troop.cpp` 早已记「部分」，缺的正是「军队形状」这一层：
地图对象的固定守卫（`setFromTile`）、城堡换防与漩涡的摆法（`ArrangeForCastleDefense`/`ArrangeForWhirlpool`）、
战场阵型（两支 `ArrangeForBattle`）、分兵/合并那批容器助手（`SplitTroopIntoFreeSlots` 一族）与中立怪的
加入判定（`GetJoinSolution`）。这一批全部落在 `FHeroes2.Game/Army/` 的六个分部文件里。

**决定**：

1. **`SetFromTile` 逐格照抄，不抽表**：金字塔、墓地、沉船、废弃船、神器守卫、亡者之城、巨魔桥、龙之城、
   恶魔洞穴九支的槽位是设计而不是数据（中间那格是 `ArrangeForBattle` 唯一会升级的格），所以写成显式
   `Set` 调用并逐格断言。两处照抄参考的怪癖：恶魔洞穴只填四格、末格留空；龙之城是「三绿一红一黑」而不是
   三绿二红。战役翻倍（`isCampaignGameType`）没有对应物，未转。
2. **两个 fallback 的 `allowUpgrade` 不对称**：捕获对象记录的守军走 `world.GetCapturedObject(...).Guardians`
   且 `allowUpgrade: false`（记录里没有可升级的信息），地图上的怪物走 `tile.GetTroop( world )` 且
   `allowUpgrade: true`。`Artifact` 分支用元组 `switch` 显式列出「50 强盗 / 1 神怪 / 1 圣骑士 / 1 独眼 /
   1 凤凰 / 1 绿龙 / 1 泰坦 / 1 骨龙」，无守卫条件时返回 `( Unknown, 0 )` 并不布置。
3. **两支 `ArrangeForBattle` 分工**：`( monster, count, stacksCount )` 管固定阵型——`stacks` 一路递减到
   `count/stacks > 0`，居中 `( 5 - stacksCount ) / 2`，余数给最左几格；`( monster, count, tileIndex,
   allowUpgrade, world )` 管地图上的怪物——弓兵固定五格，其余由 `Pcg32( MapSeed + tileIndex )` 抽 3..5 格，
   只有最中间那格可能升级，升级走第二路随机 `MapSeed + tileIndex + monster` 的 50%。种子挑法沿用 ADR-057 的
   「同一个 tile 每次进战场都一样」。
4. **`ArrangeForCastleDefense` 收 `bool isControlAi`**：参考问 `isControlAI()`，端口没有玩家对象，问谁由调用方
   说。AI 路线按强度降序 `JoinTroop`、满了先 `MergeSameMonsterOnce` 再试一次；人类路线只把守军搬到**同一个
   槽号**的空位上，绝不打乱玩家的排布。
5. **放开参考的两条「满军」断言，换成更弱的一条**：参考在入口断言 `size() == 5 && garrison.size() == 5`、在
   AI 分支出口断言 `size() == 5`。但真实调用点 `Castle::ActionPreBattle` 会把一个可能只有两三格的守军交进来
   （城堡没建满住所就会断言失败），所以端口不要求入口满军；出口改成「守军里还剩兵 ⟹ 我方五格全满」——
   这正是参考那条断言想表达的意思（只有满军才可能塞不进去）。
6. **`GetJoinSolution` 返回 record，王国可空**：`NeutralMonsterJoiningCondition( Reason, MonsterCount )`；
   参考的 `hero.GetKingdom()` 换成可空参数 `Kingdom.Kingdom?`（命名空间与类型同名，只能写全名）。顺序照抄：
   先查 `troop.isValid()`（空栈是调用方的 bug，断言后返回 `None`），再算强度比，`canJoin` 四条（比 > 2、能收下、
   没有禁join 神器诅咒、tile 不是 skip），免费 → `Free`；否则外交 → `ForMoney`（按血量折算、且王国付得起
   **整队**的钱）；最后 `比 > 5 且不是人类` → `RunAway`。
7. **`Troops::Insert`/`PushBack`/`PopBack` 不移植**：它们改的是参考的 `std::vector<Troop*>`；端口五槽定长，
   开放式容器一律是 `List<Troop>`，`Insert` 就是 `List.AddRange`。`areAllTroopsUnique()` 仍补上（AI 规划器
   判断满军无可合并时用）。

**后果**：新增 `Army.Size.cs`、`Army.Split.cs`、`Army.JoinSolution.cs`、`Army.Arrangement.cs` 四个分部文件；
`Army.cs` 追加 `AreAllTroopsUnique`。测试 86 条新增：`ArmySizeTests` 38（九档边界、`SizeRangeString`、
`SizeString`）、`ArmySplitTests` 12（分兵余数靠左、请求被夹到空槽数、不动另一支军队、满员合并最弱重复、
最弱兵种每空槽搬一个、单兵不拆、`AssignToFirstFreeSlot`、`JoinAllTroopsOfType`）、`ArmyArrangementTests` 24
（九种固定守卫的逐格断言、两支 `ArrangeForBattle` 的居中/余数/弓兵五格/同 tile 同结果/只有中格升级、
漩涡三态、换防的 AI/人类/满军三态）、`ArmyJoinSolutionTests` 9（免费、幽灵不加入、满军无处放、AI 逃跑、
人类不逃、外交付款与两种付不起），`ArmyTests` 追加 3（唯一性三态）。Game 2022 → **2108**，全解决方案
2772 → **2858** 全绿，`-warnaserror` 0 警告。覆盖率：**`army` 行不变（0/2/2），合计不变（41/63/145）**。

**遗留**：`army.cpp` 仍记 ◐——`TroopSizeString`/`String` 的文案（要复数兵种名与翻译层）、三个绘制函数、
`GetLuckModificator`/`GetMoraleModificator` 的城堡与神器两项、`GetCurrentUnit`（战斗侧）、`GetControl`
（没有玩家对象）、`operator<<`/`>>`（无存档格式）；`army_troop.cpp` 仍记 ◐——`GetName`/四个数值字符串、
`ArmyTroop` 的攻防加值（战斗侧）与序列化；`army_bar.cpp`、`army_ui_helper.cpp` 仍记未转——整份是界面绘制。

---

## ADR-145：`castle` 收尾——队长成为指挥官、四条船坞方法、两个战斗钩子，以及六族的建筑 ICN 表 ✅

**背景**：`castle/` 的 11 份 `.cpp` 里 3 份完整、5 份部分、3 份未转，而 `castle.cpp`（约 2600 行）剩下的不是
「界面」而是仍然独立的规则：队长（`captain.cpp` 的那一半）没有接进守军、船坞四条方法还是空桩、
`ActionPreBattle`/`ActionAfterBattle` 没有实现、`GetICNBuilding`/`GetICNBoat` 与 `isPosition` 都没转，
`GetGarrisonStrength` 在有客将时只算客将不算守军。这一批把它们补齐，并顺手补上 ADR-144 留下的两项遗留
（`Army` 的士气/幸运里缺城堡与神器两项）。

**决定**：

1. **队长是 `IArmyCommander`，并且知道自己的城**：`Captain` 实现 `IArmyCommander`（`IsHeroes => false`、
   `InCastle => _home`、`BagArtifacts` 自带一个永远为空的口袋、`GetSecondarySkillValue` 恒 0），
   `Castle` 的构造函数里 `_army.Commander = _captain`（参考在 `_postLoad` 里做同一件事，
   `castle.cpp:2457`）。攻/防/知识是「本值 + 城的 modificator」，力量钳到 `[1, 255]`，士气/幸运走
   `Morale.Normalize( Morale.Normal + 城项 )`。训练、法术点、只学 1–2 级沿用 ADR-134。
2. **城堡项与神器诅咒接进 `Army`**：`Army.GetMoraleModificator`/`GetLuckModificator` 现在问
   `Commander?.InCastle` 与 `Commander?.BagArtifacts`，没有指挥官时退回中立值。殉难者之臂这类**非累计**诅咒
   不能走 `GetTotalArtifactEffectValue`（它对非 cumulative 有 `Debug.Assert`），所以
   `ArtifactBag` 新增 `IsArtifactCursePresent( curse )`，只问「在不在」。英雄的 `InCastle` 仍是 `null`
   （英雄还不追踪自己站在哪座城），队长是唯一实现者。
3. **四条船坞方法照抄**：`AllowBuyBoat` = 有船坞 + （`checkPayment` 为真时）付得起 + `GetTileIndexToPlaceBoat`
   不返回 -1；`BuyBoat` 先验，然后扣钱、给 `SetBoat( world, Direction.Right, kingdom.Color )`；
   `HasBoatNearby` 扫港口行三格找 `ActionBoat`/`ActionHero`。**`HasBoatNearby` 查的是带动作位的值**
   （`MP2::OBJ_BOAT` = 43 + 128），写成 `Mp2ObjectType.Boat`（43，mp2 文件里的原值）会永远拿不到——
   这是本批唯一一处「先写错再改对」的地方，记在这里免得后人再踩。港口行的落点规则照抄：
   `isWater() && ( icnType == Unknown || isPassabilityTransparent() )`，中心格优先。
4. **两个战斗钩子**：`ActionPreBattle( guestHero, isControlAi )` —— AI 分支是 `AI::Planner::CastlePreBattle`，
   规划器未转，留空并注明；人类分支 `guestHero.Army.ArrangeForCastleDefense( _army, isControlAi: false )`，
   即同槽位规则（ADR-144 的第 4 条）。`ActionAfterBattle( attackerWins )` —— 只有攻方胜才清空守军并复位
   `HasCustomArmy`，攻方负时什么都不做（战斗已经扣过伤亡）。参考的钩子就这么短，塔与护城河是竞技场自己的事。
5. **`GetGarrisonStrength` 的合军与 `guestIsControlAi`**：参考把守军并进客将的军（`combinedArmy`）再问强度，
   而并法取决于 `isControlAI()`，即**客将的控制**。端口没有玩家对象，所以加可选参数
   `guestIsControlAi`，默认 `true`：这个函数只服务于「AI 评估目标城」，多算进去的那点兵力是安全的一侧。
   合军在副本上做（合并会清空来源），客将与守军都不受影响。
6. **`GetICNBuilding`/`GetICNBoat` 逐族照抄**：六个私有 per-race `switch`，缺省的 `_ => Unknown`。参考的怪癖
   全部保留：骑士 2 级住所与它的升级**互换**图（`Twnkup1`/`Twnkdw1`）；术士没有 5/6 级升级；亡灵没有酒馆
   （神殿用酒馆的 `Twnntvrn`）、队长是独立建筑 `NecromancerCastleStandaloneCaptainQuarters`；术士的帐篷是
   `Twnstent`、`Well2` 用骑士的右农场 `KnightCastleRightFarm`；warlock 的 7 级升级是 `Twnwup5b`；wizard 没有
   2/4 级升级。法师塔在参考里是五个 `case` 共一个图，端口的表按掩码建，所以在入口把
   `MageGuild1..5` 归一成 `MageGuild`。六族 147 条用脚本与参考 `GetICNBuilding` 逐条对比过（键名去下划线
   后比对），全等。
7. **`IsPosition`/`IsControlAi`/`FirstCastle`**：`IsPosition( MapGeometry, Point )` 是「上面一行三格 + 本行
   五格」的十字（八格）；`IsControlAi( Kingdom )` = `!kingdom.IsControlHuman`（端口没有 `Control` 枚举，
   控制是谁在 `Kingdom.Init` 时定下）；`AllCastles.FirstCastle` —— 端口城表没有空位，等价于「有城就是 0，
   没城就是 -1」。

**后果**：新增 `Castle.Battle.cs`、`Castle.Boats.cs`、`Castle.IcnTables.cs` 三个分部文件；`Captain.cs` 重写为
指挥官；`Castle.cs` 加 `IsPosition`/`IsControlAi`/构造函数里的守军绑定；`Castle.Values.cs` 的
`GetGarrisonStrength` 接上合军；`AllCastles.cs` 加 `FirstCastle`；`Army/IArmyCommander.cs` 加
`InCastle`/`BagArtifacts`；`Army/Army.cs` 的两项 modificator 接上城堡与神器；`Artifacts/ArtifactBag.cs` 加
`IsArtifactCursePresent`。测试 37 条新增：`CastleCaptainTests` 7（指挥官三态、城的士气/幸运/力量、无点数为 0、
书里最好的战斗法术、只学 1–2 级）、`CastleBoatsTests` 8（船坞/钱/水三条件、落点、付款与旗色、英雄算船、
地图边缘无港口、六族船图）、`CastleRulesTests` 9（八格十字、控制、`FirstCastle`、战前合军、空城、AI 分支不动、
战后清空、合军强度与副本、人机并法有别）、`CastleIcnTableTests` 11（六族公共建筑、城图互不相同、法师塔五级归一、
骑士互换、骑士农场、亡灵神殿、亡灵队长、术士无 5/6 升级、warlock 7 级升级、wizard 无 2/4 升级、空槽与未知族），
`LuckMoraleTests` 追加 2（殉难者之臂、队长的城把士气传给守军）。Game 2108 → **2145**，全解决方案 2858 →
**2895** 全绿，`-warnaserror` 0 警告。覆盖率：**`castle` 行不变（3/5/3），合计不变（41/63/145）**。

**遗留**：`castle.cpp` 仍记 ◐——塔的守城强度（`Battle::Tower`，`towerStr` 与每座箭塔半额）、`ActionNewMonth`
的维护费与税收、`DrawImageCastle`（要城堡屏）、`ActionPreBattle` 的 AI 分支（`AI::Planner::CastlePreBattle`）、
`FixFrenchCharactersInName`（属 `MapTextCodec` 所在的文本批次）。`buildinginfo.cpp`、`castle_building.cpp`、
`castle_dialog.cpp`、`castle_mageguild.cpp`、`castle_tavern.cpp`、`castle_town.cpp`、`castle_well.cpp` 七份
（约 4700 行）仍是界面批次，与 `army_bar.cpp`/`spell_book.cpp` 同批。
