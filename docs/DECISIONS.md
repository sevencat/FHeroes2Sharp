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
* **其余一律清零**：`setInitialObjectInfo` 的默认分支 `resetMetadata()`，注释写着 "All other objects
  shall not have metadata"。

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

**后续**：上段除 `placeObjectOnTile` 外都已落——`setInitialObjectInfo`/`updateRandomXxx` 在
`Tile.InitialObjectInfo.cs`，`updateObjectInfoTile` 在 `TileWeekLife.cs`，军队三支在 `TileTroops.cs`，
`ClearFog`/`updateFogDirectionsInArea` 在 `World`/`TileFog.cs`；瓦片读取器统一按 `getMainObjectType(false)`
看穿英雄。`placeObjectOnTile` 之外还差的 1.1.08 旧存档矿井修复也已补上（`World.RepairHackedMines()`，
因端口 `setInitialObjectInfo` 要 `World`，从 `Tile.ReadFrom` 挪到瓦片循环之后）。`map_random_generator*`
仍按 P7 挂着。

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

---

## ADR-146：`monster` 目录的规则面——弱点表、随机分桶、弹道与住所图标、法术抗性与召唤映射 ✅

**背景**：`monster/` 的 3 份 `.cpp` 里已有 id 枚举、通用/战斗数值表、能力表与载荷、升级关系、住所反查与
`GetRNDSize`，但 `monster.cpp` 还差 `GetMissileICN`、`ICNMonh`、`GetRandomUnitLevel`、`Rand(LevelType)`、
`Monster( const Spell & )`、`isWeaknessPresent`、`isRandomMonster`；`monster_info.cpp` 差整张 `weaknesses`
表与 `getSpellResistance`；`monster_anim.cpp` 整份未转。

**决定**：

1. **弱点表与能力表同形**：`MonsterWeaknesses` 是 `MonsterAbilities` 的孪生——`MonsterWeaknessType` 枚举
   （`NONE` + 七个）、`MonsterWeaknessInstance` 记录（`Type`/`Percentage`/`Value`，`Value` 是法术 id）、
   按怪物给的实例表 + 折出来的 `IsPresent`/`TryGetInstance`。**只有四个元素人有弱点**，内容照抄
   `monster_info.cpp:531-561`：空气元素被三个闪电法术（连锁闪电、元素风暴、闪电箭）打 100% 额外伤害、被土系
   生物打双倍；土系反之；火元素怕冰系法术与水系生物，水系怕火系法术与火系生物。顺序照抄，因为
   `getSpellResistance` 与描述都按列表走。
2. **`IsBasic` 画的是"描述里藏不藏"的线**：四个"双倍伤害来自某系生物"是基本项，怪物信息面板不显示，只在
   伤害计算里生效；其余（冰/火法术、指定法术）显示。参考用同一个 `ignoreBasicWeaknesses` 开关，端口把它做成
   静态谓词，供将来的描述函数共用。
3. **随机分桶照抄，包括它的边界**：`MonsterLevelType` 的 `Any = 0`、`Level1..4 = 1..4`（参考用它减一索引
   数组），四桶是 10/15/27/14、共 66 只。`Any` 不查桶，直接 `Rand.Get( Peasant, WaterElement )`——正是
   参考的 `Rand::Get( PEASANT, WATER_ELEMENT )`，因此永远不会掷到 marker。桶按 creature id 升序一次建成
   （`Lazy`），与参考的 `static std::vector` 同序，同种子下掷出的序列一致。`RANDOM_MONSTER` 每问一次掷一个
   桶，这是参考 getter 里的行为，不是端口的发明。越界（`(MonsterLevelType)5`）抛
   `ArgumentOutOfRangeException`，参考那边是数组越界的未定义行为——端口不复制未定义行为。
4. **两个 ICN 查询**：`GetMissileIcn` 逐条照抄，半人马没有自己的弹道文件，参考用注释说明回落到
   `ELF__MSL`，端口照做；`GetDwellingIcn` 是 `ICN::MONH0000 + id - PEASANT`，端口的 `IcnType` 把
   `Monh0000…Monh0065` 列成连续值，所以偏移就是加法。**范围内检查是端口加的**：参考的 `ICNMonh()` 不检查，
   拿 marker 去问会算到 MONH 之外，端口对 `[Peasant, WaterElement]` 之外一律答 `Unknown`。
5. **召唤映射**：`MonsterSummons.FromSpell` 对应 `Monster( const Spell & )`——八个元素召唤/守卫法术各自
   开自己的元素，`HAUNT` 叫一个幽灵，其余法术给 `Unknown`（参考构造函数的初始值）。
6. **法术抗性照抄判定顺序**：心灵类法术先看亡灵/元素/心灵免疫，再"只对活物""只对亡灵"，再冰、火、元素
   免疫，然后魔抗（`100` 直接短路成全部免疫，否则只在**伤害法术**与**对敌法术**上生效），最后遍历
   `IMMUNE_TO_CERTAIN_SPELL` 取最大值。顺序不能换：龙的 `MAGIC_RESISTANCE 100` 一旦排在前面，矮子的 25
   就会盖掉龙的免疫；指定法术之所以取最大值，是因为表里可能有两项同名法术。
7. **`IsRandomMonster` 与 `IsValid` 互补**：marker 是 `MonsterCount` 前的五个，`IsValid` 正好排除它们。

**后果**：新增 `Monsters/MonsterWeaknesses.cs`、`Monsters/MonsterLevels.cs`、`Monsters/MonsterIcnIds.cs`、
`Monsters/MonsterSummons.cs`、`Monsters/MonsterSpellResistance.cs`；`MonsterStatistics` 加 `IsRandomMonster`。
测试 61 条新增：`MonsterWeaknessTests` 7、`MonsterLevelTests` 8（含四桶 10/15/27/14 与 66 只的计数、
`Any` 掷出的永远是真怪物、越界抛异常）、`MonsterSpellResistanceTests` 13、`MonsterSummonsTests` 14
（九个 theory 行 + 四个"什么都不叫"）、`MonsterIcnIdTests` 20。Game 2145 → **2206**，全解决方案
2895 → **2956** 全绿，`-warnaserror` 0 警告。

**覆盖率不动**：`monster` 行仍是 0/2/1，因为剩下的两处不是规则：

- **文案**：`GetName`/`GetMultiName`/`GetPluralName`/`getRandomRaceMonstersName`、
  `getMonsterAbilityDescription`/`getMonsterWeaknessDescription`/`getMonsterDescription`/
  `getMonsterPropertiesDescription` 都要 `Spell.GetName()` 与 `_()`，属于文本批次（`Spell` 类的注释里早已
  把 `GetName`/`GetDescription` 记作未转）。
- **两张战斗表与动画**：`monsterData` 里的 `icnId`/`sounds` 只被 `monster_anim.cpp`、`battle_troop.cpp`、
  `dialog_armyinfo.cpp` 读，`monster_anim.cpp` 的 `RandomMonsterAnimation` 依赖 `battle_animation.h` 的
  `AnimationReference`——两者都随战斗批次一起落，现在生成就是死数据。

---

## ADR-147：`battle` 起步——两个方向环、战场几何与胜负位 ✅

**背景**：`battle/` 的 18 份 `.cpp`（约 1.5 万行）此前一份未转。整份目录压在 `Battle::Unit` 上：
`battle_cell` 的 `Cell`/`Position` 持有 `Unit*`，`battle_arena`/`battle_troop`/`battle_action` 都绕着单位转，
`battle_interface`（6100 行）是绘制，`battle_anim`/`battle_dialogs` 是表现层。真正**不依赖单位**的地基只有两块：
`battle.h` 的枚举与结果类型、`battle_board.cpp` 的索引几何。这一批先把这两块搬过来，让后续批次有落点。

**决定**：

1. **方向是位环，不是连续枚举**：`CellDirection` 取 1,2,4,8,16,32，`AttackDirection` 再加 TOP/BOTTOM 取
   1..0x80，`CENTER` 分别是 0x40 与 0x100——参考的 `operator++` 就是左移一位，`operator--` 是右移，环在
   `CENTER` 处绕回。端口给 `BattleCellDirections.All`（顺时针六项）与 `Next`/`Previous`，并用测试证明
   `All` 与 `Next` 链完全一致，防止有人后来改数组顺序。
2. **`RightSide`/`LeftSide` 是掩码**：它们是三个方向的并，只能被 `IsLeftSide`/`IsRightSide` 消费。
   `AsAttackDirection` 收到掩码、`CENTER` 或 `UNKNOWN` 一律回 `Unknown`——参考的 `default` 分支。
3. **几何只搬 index 版本**：`GetDirection` 保持参考的写法——沿环试六个邻居、比 index，而不是查增量表；
   `GetDistance` 换到轴向坐标上，`du`/`dv` 同号取 `max`、异号相加。两个函数对越界 index 都答
   `Unknown`/`0`，与参考一致。除 `IsValidDirection` 之外一律不检查边界——参考自己也不检查。
4. **城堡表与墙外表在四个格子上重叠**：8、29、73、96 两表都答 true，因为参考的区间首尾相接
   （`index <= 8` 与 `8 <= index`）。这不是转录错误：测试钉住这四个格子，并断言其余 95 格恰在一侧，
   免得以后被"修正"成互斥。
5. **护城河九格**：7、18、28、39、61、72、84、95 八格无条件；49 格看 `unit.isFlying() || bridge == nullptr ||
   !bridge->isPassable( unit )`。桥与竞技场还没转，端口把答案化成两个参数
   （`unitIsFlying`/`bridgeIsPassable`），与 `IsMoatIndex` 的签名一起说明。
6. **宽单位的环只有一支**：参考按 `headIdx > tailIdx` 写了两个分支，实际是同一个序列——从小索引的那一端
   顺时针绕一圈，最后回到它。端口合成一支并注明；`GetMoveWideIndexes` 的四个候选保持参考顺序
   （左、右、朝向上的两个对角），因为调用方取第一个可用的。
7. **半径环包含内层**：`GetDistanceIndexes(center, 2)` 同时含距离 1 与距离 2 的格子——参考的循环边界是
   `|dq|,|dr|,|dq+dr| <= R`，语义是 "within radius"。测试按此断言（中心 45 的半径 2 得 17 格），
   并在注释里点明，防止后人当成越界 bug 修掉。宽单位版本经 `SortedSet` 合并两环、剔除自身两格，
   因此顺序是 index 升序（参考用 `std::set`，同序）。
8. **`Result` 的位值原样保留**：`LOSS=0x01`、`RETREAT=0x02`、`SURRENDER=0x04`、`WINS=0x80`，中间 0x08..0x40
   留空；零散组合的化简只在 `GetOutcome` 一处，优先级 surrender > retreat > loss > wins
   （`battle_main.cpp:163-179`）。

**后果**：新增 `Battle/BattleCellDirection.cs`、`Battle/BattleBoard.cs`、`Battle/BattleResult.cs`；测试
`BattleCellDirectionTests` 7、`BattleBoardTests` 19、`BattleResultTests` 7，共 33 条：方向环的正反向、
每个邻格的互反方向与距离 1（98×6 全覆盖）、偏移行的对角缺失、四张索引表、宽单位的环对称与四格候选、
半径环与邻格的等价性、结果的优先级。Game 2206 → **2239**，全解决方案 2956 → **2989** 全绿，
`-warnaserror` 0 警告。覆盖率：`battle` 0/0/18 → **0/1/17**，合计 41/63/145 → **41/64/144**
（`battle_board.cpp` 的几何部分齐了，还剩绘制与 `Unit` 相关的重载）。

**遗留**：其余 17 份都压在 `Unit` 上——`battle_cell` 的 `Cell`/`Position`、`battle_troop` 的 `Unit`（还要
`battle_animation` 与 `battle_interface`）、`battle_army`/`battle_grave`/`battle_bridge`/`battle_pathfinding`
都是在单位上做查询，`battle_arena` 是它们的宿主。下一批从 `Unit` 与 `Cell`/`Position` 入手；`monster_anim.cpp`
的 `RandomMonsterAnimation` 与 `monsterData` 的 `icnId`/`sounds`（ADR-146 遗留）也在那时一起落地。

## ADR-148：`battle` 第二批——计时器、动画序列、命令与格子几何 ✅

**背景**：ADR-147 之后剩下的 17 份 `.cpp` 全部压在 `Battle::Unit` 上，而 `Unit` 本身又压着三样不依赖单位的东西：
`fheroes2::TimeDelay`（`engine/timing.h`）、`battle_animation` 的动画序列、`battle_command` 的命令载体，
以及 `battle_cell` 里 `Cell` 的纯几何。先把这四层压实，`Unit` 落地时才不会一次跨太多层。

**决定**：

1. **`TimeDelay` 的边界是 `>=`，`Pass()` 不是 `Reset()`**：`IsPassed()` 用 `now >= _trigger`（相等即过期），
   `Pass()` 把起点拨到 `now - 2*delay`，于是下一次必然已过期但仍保留余量；`Reset()` 才是 `now + delay`。
   时钟做成 `Func<long>` 注入，测试用一条手推时钟。时钟倒退时端口直接答「未到」，不做无符号回绕——
   参考把时间差存进 `uint64_t`，倒退会算出天文数字，端口选可预测的一侧。
2. **`RandomizedDelay` 的骰子放在第一次询问时**：`delay = Rand.Get(0, half) + half*3/2`，即 75%～125%，
   过期后重置并重掷；构造时不掷，免得无谓地消耗全局随机流。
3. **`AnimationReference` 拆出两个构造函数**：参考只从 `MonsterAnimInfo` 缓存里取记录，端口额外提供直接吃
   `MonsterAnimInfo` 的版本，测试不必先造 `AssetBundle`。`ResolveMonsterInfo` 因此是 `protected static`
   （`AnimationState` 要用）。
4. **33 支动画的拼法逐条照抄**：站立/挨打（拆上下两段）/死亡/待机/`MoveStart..MoveQuick` 四段移动/
   飞起降落/三向近战与远程。两种兜底**不同**且都保留：非怪物直接返回空序列、所有序列为空；没有 `STATIC`
   的兵种把 `_static` 塞成 `[1]`。远程优先 `Shoot*`，否则**必须存在 `DoubleHex2`** 才把吐息当远程
   （原版只有六只），三段各取自己的起止帧。
5. **状态机拒绝的前提是序列为空**：`SwitchAnimation` 只在 `GetAnimationVector(state)` 为空时返回 `false`，
   而 `IDLE` 取不到待机会回落静态图，所以「没有待机动画」并不构成拒绝——测试按参考改口。
6. **帧偏移只对四段移动有意义**：`GetAnimationOffset` 对 `MoveStart`/`Moving`/`MoveEnd`/`MoveQuick`
   按子段拼行，其余返回等长零表；`GetCurrentFrameXOffset` 则在当前状态里按子段累进，`Static` 永远答 0。
   注意偏移行在 `bin_info.cpp:315-317` 已被 `WithLength` 裁到对应动画的帧数，所以「只设起步帧」时拿不到
   三段 48 个偏移——这是参考行为，不是索引写错（`Row()` 的 `(int)MonsterAnimId` 索引本身是对的）。
7. **命令的参数倒着存**：参考继承 `std::vector<int>`，构造时用折叠表达式**从右到左**压栈，于是
   `ATTACK(a, d, move, target, dir)` 存成 `[dir, target, move, d, a]`；`GetNextValue()` 从尾部弹，因此按调用方
   写参数的顺序返回，空命令答 0 而不报错。C# 没有 `static_assert` 的等价物，每种命令给一个静态工厂方法，
   参数个数与类型在签名上定死；`SpellCast` 只在 `Teleport` 时接受两个格子。
8. **`UpdatePcg32Stream` 攻击只混三个数**：`move`、防守方 UID、攻击方 UID（`at(2)/at(3)/at(4)`）。注释里
   参考写得明白：攻击格与方向会随人类/AI 而变，混进去会让同一条指令在两种控制方式下掷出不同序列。
   `ToggleAutoCombat`/`QuickCombat` 什么都不混。命令的流是 `uint64_t`，故给 `Rand` 补一个 `ulong` 重载。
9. **`BattleCell` 本期只做几何**：`Unit *`、`isPassable`/`isPassableFromAdjacent`/`isPassableForUnit` 随
   `battle_troop` 补。几何部分照抄：44×52 的格、行距 42（六边形竖直重叠）、奇数行左移 22、
   七个缓存点乘 12 化整、八切片按顺时针依次判、`CENTER` 只认正中心。

**后果**：新增 `Core/Timing/TimeDelay.cs`、`Game/Monsters/MonsterAnimationType.cs`、`Game/Battle/BattleAnimation.cs`、
`Game/Battle/BattleCommand.cs`、`Game/Battle/BattleCell.cs`，`Rand` 加 64 位混种重载；测试 47 条
（`BattleAnimationTests` 23、`BattleCommandTests` 9、`BattleCellTests` 8、`TimeDelayTests` 7）。Game 2239 → **2279**，
全解决方案 2989 → **3036** 全绿，`-warnaserror` 0 警告。覆盖率：`battle` 0/1/17 → **2/2/14**
（`battle_animation.cpp`、`battle_command.cpp` 转 ✔，`battle_cell.cpp` 转 ◐），合计 41/64/144 → **43/65/142**。

**遗留**：`battle_troop` 的 `Unit` 是下一扇门；`battle_cell` 的 `Position`、`battle_army`、`battle_grave`、
`battle_bridge`、`battle_pathfinding`、`battle_arena`、`battle_action`、`battle_interface*`、`battle_main`
共 14 份在门后。

## ADR-149：`battle` 第三批——格子数组、位置与单位的第一层 ✅

**背景**：`Unit` 是整目录的门，但它脚下还有三层没铺：99 格数组（参考挂在 `Arena` 单例上、由 `Board::GetCell()`
转发）、`Position` 的头尾两格、以及一整套模式位常量。这三层不依赖竞技场，先铺平，再把 `Unit` 里能独立算的
那一半落下来。

**决定**：

1. **格子数组做成对象而不是单例**：`BattleBoardCells` 持有 99 个 `BattleCell`，`GetCell(index)` 与
   `GetCell(index, direction)` 越界答 `null`，与参考「非法下标返回 `nullptr`」一致。`BattleArena` 落地时它就是
   竞技场的成员，`Position`/`Unit` 暂时持有同一个实例。
2. **`Position` 存格子引用而非下标**：这样「谁站在哪」只有一处真相——`Cell::Unit`。由此带来一个必然后果：
   `SetReflection` 必须显式释放旧尾巴格。参考的 `Position::Set()` 换掉两个 `Cell *` 后，旧格子上的 `Unit *`
   是由竞技场的重绘流程收拾的；端口不能用这种「等别人擦」的做法。
3. **`GetPosition` 允许踩自己的格子**：`unit.Position.Contains(index)` 时格子算通得过，否则宽单位朝自己尾巴的
   方向挪半格会被自己挡死。先试当前朝向的尾侧，被挡再试另一侧，两侧都不行才算不能走。
4. **`CompareTo` 按下标排序**：参考特意把 `Position::operator<` 从指针比较改成下标比较，端口照抄，否则同一个
   局面在两次运行里顺序会漂。
5. **模式位保留裸 `uint` 常量**：`BattleMonsterState` 用 `public const uint` 而不是 `[Flags] enum`。`IS_BAD_MAGIC`
   这样的大组合当枚举成员读起来反而绕，而且 `BitModes` 本身就是 `uint`，直接对接最省事。中间那个 `UNUSED`
   空位也照留，好让十六进制与参考对得上。
6. **`BattleUnit` 用组合而不是继承 `Troop`**：参考是 `Unit : ArmyTroop : Troop : Monster` 四层，端口的 `Troop`
   已经在但 `ArmyTroop` 没有；单位改的是自己的兵力，不该写回军队里那份，所以持有 `Troop` 并转发访问器。
7. **本期只做不依赖竞技场与英雄的那一半**：位置/朝向/格子占位、兵力/血量/箭矢、模式位、`GetSpeed`
   （减速与加速互斥、定身与「已行动」返回 `Speed.Standing`、塔不能动）、`isFlying`/`isDoubleAttack`/
   `isRetaliationAllowed`/`isRegenerating`/`isImmovable`。`ApplyDamage`/`CalculateDamageUnit`/`NewTurn`、
   `GetDefense`（城堡与护城河）、`GetMorale`/`GetControl`（所属军队）、`ApplySpell` 族与 `isHandFighting`
   （要竞技场的颜色）留到下一批——它们会各自再拉进 `HeroBase`、`Spell` 应用层与 `BattleArena`，硬塞会让这一层不可测。

**后果**：新增 `BattleBoardCells`、`BattlePosition`、`BattleMonsterState`、`BattleModesAffected`/`BattleModeDuration`、
`BattleUnit`，`BattleCell` 加了格上单位与 `IsPassable`，`BattleBoard` 加了吃两个 `Position` 的 `GetDistance`。
测试 29 条（`BattleUnitTests` 12、`BattlePositionTests` 12、`BattleModesAffectedTests` 5）。Game 2279 → **2308**，
全解决方案 3036 → **3065** 全绿，`-warnaserror` 0 警告。覆盖率：`battle` 2/2/14 → **2/3/13**
（`battle_troop.cpp` 转 ◐），合计 43/65/142 → **43/66/141**。

**遗留**：`battle_troop` 的伤害与回合是下一批；`battle_cell` 的 `GetReachable`/`isPassableFromAdjacent` 要等
`battle_bridge`；`battle_army`、`battle_grave`、`battle_pathfinding`、`battle_arena`、`battle_action`、
`battle_interface*`、`battle_main` 等 13 份仍在门后。

## ADR-150：`battle` 第四批——单位第二层，战斗算术 ✅

**背景**：ADR-149 把 `Unit` 立到「站着、走着」，战斗本身还缺：攻防、伤害、死亡、复活、回合重置。这一层不需要
竞技场对象，只需要四个**事实**：脚下是不是护城河、这一箭有没有被城墙削、射手的英雄箭术多高、被催眠后对面是谁。
参考靠 `Arena` 单例随手取，端口把它们收进一个接口。

**决定**：

1. **`IBattleField` 只放四个查询，且可空**：`IsOnMoat(unit)`、`GetShootingPenaltyPercent(attacker, defender)`、
   `GetArcheryBonusPercent(unit)`、`GetOppositeColor(color)`。空引用按「没有加成、不在护城河、没有对面」处理，
   于是单测里造一个 `BattleUnit` 不必先造城堡与英雄；将来的 `BattleArena` 直接实现它。
2. **攻防的加成顺序照抄**：血 lust 加攻击；石肤与钢肤是 `else if`（同时存在只算石肤）；每道瓦解射线扣
   `Spell(DISRUPTING_RAY).ExtraValue()`，护城河扣 `GameStatic::BattleMoatReduceDefense`，两者都把防御夹在 1 以上。
   `_disruptingRaysNum` 本来由 `ApplySpell` 直接自增，端口显式给一个 `AddDisruptingRay()`。
3. **`CalculateDamageUnit` 逐条照抄，包括「翻倍只翻一次」**：射手不在近战里才吃箭术与城墙惩罚；在近战里就减半，
   除非有 `NO_MELEE_PENALTY`；护盾只挡射击（塔的射击不吃）；致盲反击按致盲的减伤比例；石化目标减半；
   对亡灵与四系元素弱点是**或**关系，同时成立也只 ×2；最后攻防差正差每点 +10%（封顶 +300%）、负差每点 −5%
   （封底 −80%），结果至少 1。
4. **两种吸血计量对象不同**：噬魂（`SOUL_EATER`）按**攻击方**单兵血量算点数并允许超出历史上限；吸血（`HP_DRAIN`）
   按**被攻击方**单兵血量算且不许超上限。两者都只在 `killed != 0` 时才走。
5. **`Resurrect` 的三段**：先按点数换算成生物数，再在「不许超上限」时截断并把血量拉到满员（否则半血队伍复活到
   上限会带着不满的血），最后「非临时」才减死亡数。临时复活（镜像）不动死亡数。
6. **`_applyDamage` 会顺手破控制**：定身与致盲挨打即解，且因此算作「本回合已行动」；无无限反击的补「已反击」。
   镜像被打中血量直接归零；本体死时镜像跟着死。这一层保留 `ApplyDamage`/`Resurrect` 的公开形状，
   竞技场只需按参考调用。
7. **`NewTurn` 与 `PostKilledAction` 先做到不依赖坟场**：`NewTurn` = 再生补满 + 六个「本回合用过」标记清零 +
   逐个法术减时到期摘除（镜像到期打死镜像本体）；`PostKilledAction` = 解镜像引用 + 摘掉全部法术 + 交还两格。
   坟场登记（`Graveyard::addUnit`）要等 `battle_grave`，元素与镜像本来也不进坟场，所以那一支留到那时补。
8. **纠正 ADR-149 的 `GetSpeed`**：上一批把「塔不能动」写成了 `CAP_TOWER → STANDING`，并把「已行动」放进了
   `else`；参考实际是把 `TR_MOVED` **并进定身掩码**、且**不看** `CAP_TOWER`，加速先判、减速后判。已按
   `battle_troop.cpp:424-447` 逐行对齐，三条旧测试随之改口——这类偏差必须当批修掉，不能因为它「看起来合理」就留着。

**后果**：新增 `Game/Battle/IBattleField.cs`，`BattleUnit` 扩到约 900 行（攻防、伤害、死亡、复活、回合、清理）。
测试 24 条（`BattleUnitCombatTests`）。Game 2308 → **2332**，全解决方案 3065 → **3089** 全绿，`-warnaserror` 0 警告。
覆盖率不变：`battle` 2/3/13，合计 43/66/141（`battle_troop.cpp` 仍是 ◐）。

**遗留**：`ApplySpell` 族、`GetMorale`/`GetControl`/`GetLuck`、`GetMagicResist` 与坟场登记。下一步
`battle_army`/`battle_grave`/`battle_bridge`——它们正好给 `Unit` 补上「所属军队」与「坟场」这两块。

## ADR-151：`battle` 第五批——坟场、桥与单位集合 ✅

**背景**：ADR-150 把 `Unit` 的算术补全了，但它周围还有三个小对象没落地：尸体躺在哪（`Graveyard`）、护城河上的桥
开没开（`Bridge`）、以及一堆单位怎么按 UID/模式找、怎么按速度排序（`Units`）。三者都不需要竞技场，而且正好是
`battle_cell` 的桥通行性与 `battle_army` 的 `Force` 的前置。

**决定**：

1. **坟场是 `SortedDictionary<int, List<BattleUnit>>`**：参考是 `std::map`，键有序遍历才有意义（`getOccupiedCells`
   按格子升序）。空链留着不删（与参考一致），查询时跳过；`Count` 数的是「有尸体的格子」而不是尸体数。
2. **宽单位压两格，尸体排序用 rotate 语义**：`MoveToLastIfPresent` 把该单位挪到链尾、其余保持相对顺序——对尸体
   施法时它的动画要叠在最上面。用 `RemoveAt` + `Add` 复刻；不能用「与末尾交换」那种会打乱其余顺序的写法。
3. **坟场登记接进 `PostKilledAction`，且必须在交还格子之前**：参考在 `removeAffection( IS_MAGIC )` 之后、
   `head->SetUnit( nullptr )` 之前调 `graveyard->addUnit( this )`，因为坟场按格子下标归档。两个例外——镜像
   （`CAP_MIRRORIMAGE`）与元素（`isElemental`）——不进坟场。端口把 `Graveyard` 做成可空属性，单测造单位不必先造战场。
4. **桥的三个依赖做成构造参数**：格盘、坟场、守城方颜色。参考从 `Arena` 单例取，端口显式注入；颜色可写，因为
   竞技场通常是先建桥再建城堡。`NeedDown` 的六个落点分支逐条照抄。
5. **桥认 `GetColor()` 而不是 `GetCurrentColor()`**：被催眠的守城兵按参考不算守城方，过不了升起的桥。这类
   「看起来应该用 current」的地方要照抄参考，否则催眠+桥的组合会与参考分叉。
6. **`ActionUp`/`ActionDown` 返回布尔**：参考用 `assert` 卡前置条件（失败即崩），端口返回「到底动没动」。
   参考在这里还会触发 `Interface::RedrawBridgeAnimation`，动画归界面批次，不进这个类。
7. **`SortFastest` 用稳定排序，且比的是战斗速度**：参考把 `Unit*` 传给 `Army::FastestTroop( const Troop * )`，
   而 `Troop::GetSpeed` 是虚函数、被 `Unit::GetSpeed()` 覆盖，所以减速/加速会改变出手顺序（不是兵种表上的数字）。
   `List.Sort` 不稳定，用 LINQ 的稳定排序再写回；注释与测试都钉住「同速保持原序」。

**后果**：新增 `BattleGraveyard`、`BattleBridge`、`BattleUnits`；`BattleUnit` 加了 `Graveyard` 并在
`PostKilledAction` 里登记尸体。测试 27 条（`BattleGraveyardTests` 9、`BattleBridgeTests` 10、`BattleUnitsTests` 8）。
Game 2332 → **2359**，全解决方案 3089 → **3116** 全绿，`-warnaserror` 0 警告。覆盖率：`battle` 2/3/13 → **4/4/10**，
合计 43/66/141 → **45/67/140**。

**遗留**：`battle_army.cpp` 只转了 `Units`；`Force` 还差 `TroopsUidGenerator`（参考在 `battle_arena.h`，只是个计数器）、
按阵形摆位、`GetTotalNumberOfDeadUnitsForNecromancy`/`CalculateExperienceBasedOnLosses`、`syncOriginalArmy` 与
`GetSurrenderCost`（要英雄的宝物与外交技能）。`battle_bridge` 的动画与 `battle_grave` 的对尸体施法接口随界面与
法术批次补。

## ADR-152：`battle` 第六批——把一支军队摆上战场 ✅

**背景**：ADR-151 落地后，`BattleUnits` 只是「一堆单位」。可战斗从来不操作裸单位：它操作的是两片军队，要按阵形
摆位、要知道某个单位属于哪个槽位（伤亡回写、投降价、经验都靠这条对号），还要在每回合开头把英雄的「本回合已
施法」清掉。这就是 `Battle::Force`。

**决定**：

1. **`BattleForce` 继承 `BattleUnits`**，而不是包一层。参考里 `Force : public Units`，端口把 `BattleUnits` 去封即可——
   「一支军队」本来就是「一堆单位」，多了摆位与对号。`Force` 继承的 `BitModes`（撤退/投降标记）暂不跟，它们只影响
   战斗结果与经验加成，等 `battle_result` 落地再加。
2. **摆位公式逐字照抄**：`22 + 11*i`（铺开阵型 `22*i`），镜像侧 `+10`，宽单位上侧 `+9`、下侧 `+1`。这些不是随手挑的
   间距，而是棋盘上两片出发区的真实坐标；改一个数字就会压格或越界，所以宁可留硬编码也不「抽象成参数」。
3. **`_uids` 与 `Army` 槽位一一对应，空槽存 0**：所有「场上 ↔ 原始军队」的换算都走这张表。这样召唤物、镜像顶替、
   中途被清场的单位不会污染伤亡统计，`GetSurrenderCost` 也不会把召唤出来的兵算进价钱。
4. **伤亡统计一律夹在 `min( initialCount, dead )`**：死亡数可能因为吸血超过初始人数（`ApplyDamage` 允许），
   不夹的话亡灵巫术与经验都会凭空变大。
5. **投降价照抄三段式**：先按活着的单位累加 `cost * 存活数`；有指挥官时基数从 `0.5` 起步、宝物按百分比连乘、
   外交术先乘 2 再乘一档折扣；最后四舍五入且**不低于 1**。那个 `0.5` 是参考给的「英雄在场就打对折」，不是
   「没有折扣」，很容易抄错成 1。
6. **`IArmyCommander` 从只读扩出两个成员**：`GetLevelSkill`（默认 `None`，队长本来就没技能）与
   `ResetModes`（默认空，只有英雄有模式字）。这是唯一一处战斗要写指挥官的地方——清 `SPELLCASTED`；不开一个
   通用的「拿 HeroBase 指针」口子。
7. **`GetControl` 走 `Players`**：参考里英雄问王国、队长问城堡，两条路最后都落到 `Players::GetPlayerControl( color )`。
   端口没有「英雄→王国」的反向指针（同色只有一份玩家记录），于是两边都直接问 `Players`，答案相同、依赖更少。
8. **类型别名 `GameArmy`**：`FHeroes2.Game.Army` 既是命名空间又有同名类。`BattleForce`/`BattleUnit` 里统一
   `using GameArmy = FHeroes2.Game.Army.Army;`，同时保留 `Army` 作为属性名（与参考 `GetArmy()` 对应）。

**后果**：新增 `BattleForce`、`BattleTroopsUidGenerator`；`BattleUnits` 去封；`BattleUnit` 加 `Army` 回指、
`IsMonster`、`GetCost`、`GetSurrenderCost`、`GetControl`、`GetCurrentControl`；`Army` 加 `IsSpreadFormation` 与
`GetControl`；`IArmyCommander`、`Hero`、`Captain`、`HeroModes` 各加一到两个成员。测试 16 条（`BattleForceTests`）。
Game 2359 → **2375**，全解决方案 3116 → **3132** 全绿，`-warnaserror` 0 警告。覆盖率不变：`battle` 4/4/10，
合计 45/67/140（本批补的是 ◐ 文件的内部，没有新增 ✅ 文件）。

**遗留**：`GetKilledTroops`（要参考的 `Troops` 容器做同兵种合并）、`Force` 的撤退/投降标记；`Unit` 侧还差
`ApplySpell` 族与 `GetMorale`/`GetLuck`——所属军队已接通，可以顺着做。

## ADR-153：`battle` 第七/八批——士气运气与法术应用层 ✅

**背景**：`BattleForce`（ADR-152）让单位知道自己属于哪支军队之后，`battle_troop.cpp` 只剩两块：
士气/运气的来源与掷骰（`.cpp:276-329`），以及法术应用层（`.cpp:65-98`、`793-890`、`1174-1534`）。
后者是整个战斗最密的算术之一：能落不能落、落了打多少、打完治多少，全都写在这几百行里。

**决定**：

1. **`Army.GetMorale()` 先问指挥官，英雄侧自己补军队修正**。参考的 `Army::GetMorale()` 是
   `commander ? commander->GetMorale() : GetMoraleModificator()`（`army.cpp:1220-1224`），
   `Heroes::GetMorale()` 再自己叠 `GetArmy().GetMoraleModificator()`。两条必须同时照抄：
   只做前一条，有指挥官的军队会把「多种族 -1」与亡灵惩罚静默吃掉；两边互相回调则成环。
2. **骨龙减士气走 `IBattleField.EnemyHasMonster( color, monster )`**。参考在 `Unit::GetMorale()` 里
   直接问竞技场「对面有没有骨龙」，端口不因此把敌方军队塞进 `BattleUnit`——`IBattleField` 本来就是
   「单位需要问竞技场的那几件事」的集合，加一条比多一条反向引用便宜。
3. **随机序列逐字照抄**：好士气 `if`、坏士气 `else if`、AI 豁免只在坏分支。换成两个独立 `if` 会多掷一次骰，
   整场战斗的随机序列随之错位——这类错误不会在单条测试里暴露，只会在长战斗里「AI 运气怪怪的」。
4. **法术应用层留在 `BattleUnit`**，分成三条私有路：`SpellApplyDamage`、`SpellRestoreAction`、
   `SpellModesAction`。公共入口只有 `AllowApplySpell`/`ApplySpell`/`GetMagicResist`/`CalculateSpellDamage`
   与 `GetCurrentSpellEffects`，签名与参考一一对应。
5. **`TargetInfo` 用类（`BattleTargetInfo`）**。参考按引用传 `TargetInfo &`；C# 里结构体会让每个签名都加 `ref`，
   而类天然是引用语义。伤害与击杀数写回同一个对象，测试可以直接断言 `target.Damage`/`target.Killed`。
6. **`GetMagicResist` 先判「法术有没有意义」**：反魔 100%、治疗满血且干净 100%、复活满员 100%、驱散无可摘 100%、
   催眠目标超过法术上限 100%，然后才是宝物免疫与怪物抗魔。顺序不能换——「反魔单位对治疗也免疫」与
   「对满员的队伍不必复活」是两种不同的 100%，测试要能分辨。
7. **`CalculateSpellDamage` 的四分支是 `if/else if` 链**：指定法术减伤 → 元素法术减伤（附 `IsElementalSpell()`
   条件）→ 指定法术弱点 → 火焰/寒冷弱点，只取最具体的一条；元素那一条的条件不成立时要**继续**落到弱点分支。
   宝物加成则逐项连乘（不是把百分比相加），连锁闪电的 `/2`、`/4`、`/8` 写在**有施法者**的分支里——参考就是
   这样写的，没有英雄时不减半，照着留。
8. **宝物免疫只认参考的八种**：诅咒、催眠、死亡、狂暴、致盲、麻痹、神圣、驱散。没有「寒冷免疫」这类
   `ArtifactBonusType`，不要凭直觉补。

**后果**：新增 `BattleTargetInfo`；`BattleUnit` 加 `GetCommander`、`IsHaveDamage`、`AddAffection`、
`AllowApplySpell`、`ApplySpell`、`GetMagicResist`、`CalculateSpellDamage`、`GetCurrentSpellEffects` 与三条私有路；
`IBattleField` 加 `EnemyHasMonster`；`IArmyCommander` 加 `GetColor`/`GetPower`/`GetMorale`/`GetLuck`；
`Army`/`Hero`/`Captain` 接线。测试 13 + 24 条（`BattleUnitMoraleTests`、`BattleUnitSpellTests`）。
Game 2375 → **2412**，全解决方案 3132 → **3169** 全绿，`-warnaserror` 0 警告。覆盖率不变：
`battle` 4/4/10，合计 45/67/140（补的是 ◐ 文件的内部）。

**遗留**：`battle_troop.cpp` 已无未转函数；下一步是 `battle_arena`/`battle_action`（选目标、法术路径、行动执行），
那才是真正需要棋盘与单位集合的地方。

## ADR-154：`battle` 第九批——寻路（`BattlePathfinder`）✅

**背景**：`battle_pathfinding.h/.cpp`（124 + 416 行）是竞技场与 AI 共同的前置：谁走得到哪儿、走过去要多少移动力、
路径长什么样。它只依赖棋盘几何与一组单位查询，正好是单位层收尾后最干净的一块。

**决定**：

1. **竞技场依赖压成三个构造参数**：棋盘（`BattleBoardCells`）、有没有护城河、桥对这个单位通不通（`Func<BattleUnit,bool>`）。
   参考直接调 `Arena::GetBoard()`/`GetCastle()`/`Bridge::isPassable( unit )`；`isPassable` 要问单位，所以只能是委托。
   桥不存在与桥吊起给同一个答案（都不可通过），这与参考里 `bridge == nullptr` 的处理一致。
2. **「未到达」必须显式构造**。参考用父节点 `{ -1, -1 }` 当哨兵，C# 的 `default(BattlePathNode)` 是全零，父节点成了
   `( 0, 0 )`——一个合法格子。若照抄 `TryGetValue(...) ? existing : default`，所有节点都会被当成「已到达、代价 0」，
   于是没有任何松弛能成立，寻路结果恒为空，而且不会报错。引入 `BattlePathNode.None` 并在测试里把这条钉死。
3. **可变结构体出字典要「取出→改→写回」**。`Dictionary[key].Update(...)` 是对副本调用方法，编译器不吭声、运行期白干。
   飞行分支最初就是这么写的，改成局部变量后赋回。
4. **缓存键含整张棋盘的通行性快照**（起点、速度、宽窄、飞行、颜色、99 格布尔）。参考注释说明了理由：单位自己的回合里
   棋盘也会变（英雄施法、尸体挪位），不重算就会沿着一条已经不存在的路走。端口额外记一个 `_hasCache` 布尔——参考靠
   哨兵排除首轮，若单位恰好不在棋盘上（头格 -1）且其余参数都等于初值，会退化成「空缓存被当成有效缓存」。
5. **护城河罚款记在「迈出护城河」那一步**（`MOAT_PENALTY = UINT16_MAX = 65535`）。所以走进护城河只要 1，从护城河
   迈出去要 65535，而「本就站在护城河里」的那一格在起点上被豁免一次。直觉相反，但这是参考的可观测行为，测试按
   这个语义写，别按直觉「修正」。
6. **宽单位原地转身代价为 0**（`newNodeIdx == flippedCurrentNodeIdx`），横移半步代价 1，斜向走时尾巴必须落在非移动
   方向那一侧（`Cell::isPassableFromAdjacent`）。`buildPath` 末尾补一步转身，`getClosestReachablePosition` 里同样
   先 `Set` 再按需 `Swap`。
7. **飞行单位一次落地**：代价恒为 1，步数取 `max(GetDistance, 1)`——两个位置可以重叠，实际距离为零但确实动了一步。
8. **非飞行用「带重入的广度优先」**（发现更便宜才重新排队），照抄参考。因为每步代价非负且护城河惩罚是常数，结果与
   Dijkstra 相同，但探索顺序与参考一致，便于逐例对拍。

**后果**：新增 `BattlePathfinder`（六个公开查询：`IsPositionReachable`/`GetCost`/`GetDistance`/`BuildPath`/
`GetAllAvailableMoves`/`GetClosestReachablePosition`）与 `BattleNodeIndex`/`BattlePathNode`；`BattleCell` 补
`IsPassableFromAdjacent`/`IsPassableForUnit`（用 `unit.Position.Board` 找邻居，格子本身不持有棋盘）。测试 13 条
（`BattlePathfinderTests`）。Game 2412 → **2425**，全解决方案 3169 → **3182** 全绿，`-warnaserror` 0 警告。

**遗留**：`Position::GetReachable`（要问竞技场这个单位有多少移动力），以及 `battle_arena` 自己的选目标与法术路径。

## ADR-155：`battle` 第十批——投石车与箭塔（`BattleCatapult`/`BattleTower`）✅

**背景**：`battle_catapult.h/.cpp`（73 + 178 行）与 `battle_tower.h/.cpp`（78 + 192 行）是围城的两件器械。
两者都不在棋盘上活动（投石车在城外，箭塔的箭手是凭空的），依赖只有城堡、单位和随机数，是竞技场之前最后
一批能独立测的东西。

**决定**：

1. **塔用组合而不是继承**。参考的 `Tower : public Unit` 只为覆盖 `isValid()`——被砸掉的塔人还在但不打了。端口
   的 `BattleUnit` 是 sealed，于是 `BattleTower` 持有自己的 `BattleUnit`（构造过程与参考逐句对应）并用一个自己的
   `_isValid` 回答 `IsValid`。塔的「位置」是一张一次性的空棋盘，单位因此永远不占格，与参考的 `Position{}` 同效。
2. **`SetCount` 的怪癖照抄**：参考先按 `min(CountBuildings,20)` 造单位、再 `SetCount` 折半，而 `SetCount` 只改数量
   不动血量，于是炮塔是「半队人、满血」。端口先构造再改 `Troop.Count`，得到同一结果；测试同时断言数量与血量。
3. **攻击加成取法师塔等级，不是弹道术**。`GetAttackBonus() = castle.GetLevelMageGuild()`；英雄的弹道术只作用于
   投石车（射数、双倍概率、能否失手）。
4. **补上 `ArmyTroop` 那一项**。参考 `Unit::GetAttack()/GetDefense()` 是 `Troop::… + 指挥官…`，端口此前只算了怪物
   基础值。这不只是塔的问题：没这一项，英雄带的兵与地图上的野兵攻击完全相同。攻击与防御一起补，并把塔的
   「箭手继承指挥官攻击」当成它的第一个使用者。
5. **投石车的优先级照抄**：墙（四段）→ 塔（两座）→ 桥 → 主堡，每一级内部才随机，每一发都重新挑级。`GetTarget`
   的目标表由调用方给出「还剩多少」，缺键是调用方的错（参考 `assert`）。`GetAllowedTargets` 只有八项：桥头两座
   小塔不是目标。
6. **能不失手就不掷骰**：`IsNextShotHit` 在 `CanMiss == false` 时直接返回真，不消耗随机数——回放依赖这一点。
   命中率是掷 1..20 取 ≥ 6。
7. **顺手补三个留白**：`Board::GetMoatInfo()`（城堡描述要它）、`Castle::GetDescriptionBuilding` 里主堡之后接的
   塔与护城河两段（`castle.cpp:2544-2566`）、以及 `Castle::GetGarrisonStrength` 的塔强度项（`castle.cpp:2100-2111`，
   主堡一座、炮塔各半座，加在乘系数之前）。这三处此前都是「等 `Battle::Tower`/`Board` 就位再说」，现在一并接上。

**后果**：新增 `BattleCatapult`、`BattleTower`、`CastleDefenseStructure`、`BattleTowerType`；`BattleUnit` 补
`GetStrengthWithBonus` 并修正 `GetAttack`/`GetDefense` 的指挥官项；`BattleBoard` 补 `GetMoatInfo`；城堡的描述与
驻军强度各补一段。测试 26 条（`BattleTowerTests` 14、`BattleCatapultTests` 12）。Game 2425 → **2451**，全解决
方案 3182 → **3208** 全绿，`-warnaserror` 0 警告。覆盖率 `battle` 5/4/9 → **7/4/7**，合计 46/67/139 → **48/67/137**。

**遗留**：`battle_arena` 与 `battle_action`——塔与投石车在这两处各只剩一句调用，选目标与结算都在竞技场里。

## ADR-156：`battle` 第十一批——竞技场的装配与查询（`BattleArena`）◐

**背景**：`battle_arena.h/.cpp` 约 1000 + 1500 行，是战斗层的枢纽。它同时承担四件事：装配战场（棋盘、军队、城堡、
塔、桥、投石车、寻路器）、回答战斗中的事实（颜色、指挥官、护城河、射击惩罚）、推进回合，以及执行行动命令。
第三、四件事的全部实现都落在 `battle_action.cpp` 与 `Battle::Interface` 上。

**决定**：

1. **这一批只做前两件事**。`Turns()`、`UnitTurn()`、`ApplyAction()`、`TowerAction()`、`CatapultAction()` 要等
   `battle_action` 的命令结构落地；现在写只能是空壳，还会把「当前单位怎么被推进」写死在错误的位置上。状态与判据
   先写全，行动层落地时只需调用它们。
2. **竞技场不做单例**。参考在构造函数里把自己登记进文件静态的 `arena`，之后 `Arena::Get()` 全局可取，连
   `Unit::isMoatBonus()` 都靠这条路。端口让战场成为普通对象，把逆向依赖压缩成 `IBattleField` 的五个成员（`IsOnMoat`、
   `GetShootingPenaltyPercent`、`GetArcheryBonusPercent`、`GetOppositeColor`、`EnemyHasMonster`），并在构造末尾把
   `Field`/`Graveyard` 注入每个单位（含塔内部的单位）。
3. **`IsTown` 与 `Castle` 分开**。没有 `BUILD_CASTLE` 的村庄仍算攻城（多 500 经验），但棋盘上没有墙；参考在
   `battle_arena.cpp:360-362` 把城堡指针置空、`_isTown` 保留为真。端口照此实现，`Castle == null` 时才铺野战障碍。
4. **塔的军队取「实际守城的那支」**。参考向城堡要 `GetActualArmy()`（客将优先，否则驻军）。端口的城堡没有指向客将的
   回指，而竞技场收到的防守军本来就是那支军队，于是构造后把 `tower.Unit.Army` 指向 `DefendingForce.OriginalArmy`；
   测试断言「塔的箭手拿到哪个指挥官的攻击」。
5. **四类结构、三种摧毁语义**。墙可打穿到 0（格变可通行）；桥头塔与箭塔最低停在 1（掉一级、永久挡路）；桥一下到底
   （格变 0）。有炮塔建筑的箭塔把伤害记在 `BattleTower` 上、格子写瓦砾 1；没有炮塔的箭塔直接扣格子数。中央塔不在
   地震目标里，`getCastleDefenseStructureCondition` 的「地震打中央塔」分支照参考读 0 号格而不抛错。
6. **墙的耐久起点看 `Spec` 而不是主堡**。`IsFortificationBuilt` 只对骑士且建了 `Spec` 成立（`castle.cpp`），此前按
   「有主堡就是 3」的直觉写测试会失败——以参考为准，`3`/`2` 两档都测。
7. **射击惩罚：先豁免、后几何**。顺序是无城堡 → 塔 → 黄金弓/箭术 → 射手在墙内 → 双方同侧，最后才沿两支单位的背点
   连线以 `Cell::widthPx/3` 采样，检查是否从已打穿的墙格看过去。先算几何会把前四条豁免埋掉。新增
   `LinePoints.GetLinePoints`（`getLinePoints()` 移植）与 `BattleUnit.GetBackPoint`/`IsOutOfCastleWalls`/
   `GetCurrentOrArmyColor` 作为配套。
8. **障碍用独立随机源**。`PCG32( world.GetMapSeed() + tileIndex )`，与战斗随机数无关：同一格野战的地形永远一样。
   放置上限按已有大障碍（`0x40`）的格数分四档（6/4/3/2），并保留两条硬约束（双格障碍不从第 8 列起、高障碍不在最上
   两行）。`SetCobjObjects` 为免让棋盘认识世界，收「主对象类型 + 地面」而不是 `Tile`。
9. **护城河同时问城堡与桥**。城堡回答有没有建护城河，棋盘回答「飞行/桥面可通行」；桥的 `IsPassable` 还看闸门与
   护城河格是否被占——所以自己站在护城河格上时桥被自己占住，照样吃罚款，与参考一致。

**后果**：新增 `BattleArena`（三个 partial：主文件、`.Castle.cs`、`.Combat.cs`）与 `SiegeWeaponType`；
`BattleBoardCells` 补 `SetCovrObjects`/`SetCobjObjects`。测试 31 条新增（`BattleArenaTests` 24、
`BattleBoardObstacleTests` 7）。Game 2451 → **2482**，全解决方案 3208 → **3239** 全绿，`-warnaserror` 0 警告。
覆盖率 `battle` 7/4/7 → **7/5/6**，合计 48/67/137 → **48/68/136**。

**遗留**：`Turns()`/`UnitTurn()`/`ApplyAction()`/`TowerAction()`/`CatapultAction()`（等 `battle_action`）；界面、
音乐等待、AI 规划器、自动战斗；`FromTile` 的棋盘层测试已有，但缺一个真实 `World` 的端到端用例。

## ADR-157：`battle` 第十二批——行动层的移动与回合动作 ◐

**背景**：`battle_action.cpp` 有 1274 行，一半是界面动画（逐帧等待、三段攻击动画），另一半改格子与模式位。
`battle_arena` 的请求队列与 AI 都从 `ApplyAction` 进来，所以这一层是战斗可玩性的第一道门。

**决定**：

1. **先取与画面无关的一段**：分发器加上 `MOVE`/`SKIP`/`MORALE`。攻击要 `BattleUnit` 的反击判定、
   `TargetsApplyDamage`、`calculateAttackTarget` 三样都齐；塔与投石车要等这一批的命令结构被真正喂过一遍；
   撤退与投降要 `World`→`Kingdom` 的扣金链路（`World` 目前没有 `GetKingdom` 的公开入口）。先做能自洽验证的。
2. **未移植的命令显式抛 `NotSupportedException`**，消息里点名还差什么。分发器静默忽略一条 `ATTACK` 会让战斗停在
   原地而无任何迹象——那类 bug 比异常难查。非法参数同理，一律抛 `ArgumentException`（参考是打日志 + `assert`）。
3. **`Position::GetReachable` 由竞技场作参数**。它要问「走不走得到、花多少移动力」，参考向单例要；端口把
   `BattleArena` 传进去。这是 `BattlePosition` 第一次依赖 `battle_arena`，也让 `BattlePosition` 的文档承诺
   （「随 battle_arena 一起补」）兑现。
4. **`checkMoveParams` 要求头格命中**。宽单位在 `GetReachable` 里允许「尾巴落在 dst、头在身前」的倒行解，但
   `checkMoveParams` 又要求 `pos.GetHead()->GetIndex() == dst`，于是那条解在真实命令里被否掉。两条规则看似矛盾，
   其实是「寻路能想到」与「命令允许」的区别，测试里各覆盖一条。
5. **`moveUnit` 的逐格挪提成主路径**。参考只在有桥时真的逐格挪（`battle_action.cpp:333-350`），其余情况交给界面动画；
   端口没有界面，于是步行一律逐格挪，桥的升降（`NeedDown`/`AllowUp`）作为附加动作挂在每一步前后。
6. **桥的通不通必须由调用方烘进闸门格**。`Bridge::SetPassability()` 把答案写进 `objectType`，寻路器只读格子、不回头
   问桥。参考在 `Turns()` 的每个单位行动前刷一次；端口没有回合循环，把刷新挂在 `ApplyActionMove` 与 `MoveUnit`
   两个入口上，并注明「按正在行动的那一支重算」这个与参考一致的副作用。
7. **`UpdateDirection()` 需要竞技场回答攻方颜色**，于是 `IBattleField` 加了第五个成员 `AttackingArmyColor`。这是
   参考 `getAttackingArmyColor()` 的直接映射，也是单位对竞技场的最后一项反向依赖。
8. **士气语义照抄**：好士气清 `Moved|MoraleGood`（还回一次行动），坏士气清 `MoraleBad` 再打 `Moved`（吞掉这次）。
   两条都校验「单位确实在等这个答案」，否则抛异常。

**后果**：新增 `BattleArena.Action.cs`；`BattlePosition` 加 `GetReachable`；`BattleUnit` 加两个 `UpdateDirection`；
`IBattleField` 加一个成员（三个测试桩同步补上）。测试 11 条新增。Game 2482 → **2493**，全解决方案 3239 → **3250**，
`-warnaserror` 0 警告。覆盖率 `battle` 7/5/6 → **7/6/5**，合计 48/68/136 → **48/69/135**。

**遗留**：攻击（`calculateAttackTarget`、反击、`TargetsApplyDamage`）、法术、塔与投石车的执行、撤退/投降（需
`World` 的王国入口）、`Turns`/`UnitTurn` 的回合循环、界面与 AI。

## ADR-158：`world` 第三批——胜负条件、英雄查询与按英雄寻路 ◐

**背景**：`world.cpp` 里「谁赢了」这一段（`941-1157`）是 `world` 目录最后一块纯逻辑：地图文件头给出条件位掩码与
参数，`World` 拿当前局面去比对。它上接 `battle` 的结束判定与 `kingdom` 的存亡，下接 AI 的「我还有没有赢面」。
`getDistance`/`getPath` 则是参考给英雄留的唯一两个寻路入口（`1159-1169`），界面、AI、`Route` 都从这里走。

**决定**：

1. **条件位掩码与其参数翻译单独成两层**。`GameOver`（`Game/GameOver.cs`）只有常量，一字节对一位；`MapConditions`
   （`Game/Maps/MapConditions.cs`）把 `Mp2FileInfo` 的两个条件块翻成「位掩码 + 坐标/神器/千枚金币/天数」。
   翻译不放在 `Assets` 里：`FHeroes2.Assets` 不依赖 `FHeroes2.Game`，而 `GameOver` 的位定义属于游戏语义。
2. **文件头存在世界里，不每次去 `Settings` 拿**。参考每回判定都问 `Settings::Get().getCurrentMapInfo()`；端口的文件头
   就是 `LoadMap(map)` 的入参，`World.MapInfo` 收下它，`Reset()` 清掉。好处是判定在测试里不需要初始化单例，坏处是
   「运行期改 `Settings` 里的文件头」这类参考行为端口看不见——目前没有这种调用者。
3. **「实际在场的颜色」直接问王国**。`WINS_ALL`/`WINS_SIDE` 在参考里问 `Game::GetActualKingdomColors()`，它取的是
   `Settings` 里的玩家槽；端口还没有开局流程去填槽，而槽里的颜色正是同一份文件头建出来的，于是改用
   `Kingdoms.GetColors()`（颜色不为 `None` 的槽）。
4. **`WINS_SIDE` 在没有玩家槽时把自己当盟友**。参考的 `Players::GetPlayerFriends` 直接解引用玩家槽，没有槽就崩；
   端口在没有槽（`None`）时退化成 `friends = kingdom.Color`，等价于「单人、没有热座共享」的取值。
5. **`WINS_HERO` 的方向照抄参考，不做「直觉修正」**：`hero.isAvailableForHire() || GetKingdom(hero.Color).isControlHuman()`。
   读起来像是「英雄被电脑带走就赢」，但参考要的正是这一条，测试把它钉死，免得下一个人凭直觉改成 `!isControlHuman`。
6. **缺英雄不让装载失败**。参考在 `setHeroIdsForMapConditions()` 里找不到英雄就返回 `false`，整张图装载失败。端口的
   `LoadMap` 契约是「坏输入抛异常」，而这时世界已经装了一半，抛出去只会留下半个世界；于是记一条 `GameLog.Error` 并
   继续，效果是该条件永远不满足。等有开局错误对话框时把这里改成让装载失败。
7. **战役的两条特殊条件跳过**。`Campaign` 模块还没有：「占龙城即胜」与「女巫村全失即负」两条分支不写，`Switch` 里
   也没有它们的位置——写了只会是永远为假的死代码。
8. **`GetDistance`/`GetPath` 由 `World` 拆五个值喂寻路器**。参考有 `reEvaluateIfNeeded( const Heroes & )` 的重载；
   端口的寻路器只收拆开的五个值（它出现时 `Hero` 还没有移动力），所以这两个入口负责用户侧的便利。英雄不在图上时抛
   `ArgumentException`，与「非法输入不静默」的既定做法一致。
9. **`getCastleEntrance` 补一个以 `Point` 为参数的版本**，先查图内再换算下标。条件里存的正是 `Point`，而且可能是
   (-1, -1)（条件没有坐标时），参考那版对图外的点会断言。
10. **`HeroModes.NotDismiss` 补上**（`heroes.h:212`，`0x2000`）。它是条件英雄的「不许解雇」标记，与 `Custom` 一起
    打在守的那位英雄身上。

**后果**：新增 `Game/GameOver.cs`、`Game/Maps/MapConditions.cs`、`Game/World/World.Conditions.cs`；`World` 加
`MapInfo`/`GetHeroesCondWins`/`GetHeroesCondLoss`/`KingdomIsWins`/`KingdomIsLoss`/`CheckKingdomWins`/
`CheckKingdomLoss`/`GetKingdom`/`GetDistance`/`GetPath`/`GetCastleEntrance(Point)`，`LoadMap` 末尾接上
`SetHeroIdsForMapConditions()`；`Kingdoms` 加 `GetColors`/`FindWins`；`HeroModes` 加 `NotDismiss`。
测试 38 条新增（`MapConditionsTests` 18、`WorldConditionsTests` 20）。Game 2493 → **2531**，全解决方案 3250 →
**3288**，`-warnaserror` 0 警告。覆盖率不变：`world` 仍是 1/4/0（`world.cpp` 本来就记 ◐，这一批填的是它的内部函数，
没有让任何文件变 ✔），合计仍 **48/69/135**。

**遗留**：`world.cpp` 的 `NewMonth`/`_monthOfMonstersAction`、`getCurrentRumor`、
`fixFrenchCharactersInStrings`、`generateBattleOnlyMap`；AI 侧的王国动作与 `world_loadmap` 里的可占领对象表与终极
神器的埋放；战役的两条特殊条件。

## ADR-159：`world` 第四批——挖终极神器 ◐

**背景**：`world.cpp:850-873` 的 `DiggingForUltimateArtifact()` 是玩法里最直白的一步：英雄花一天挖一格，地上留一个
坑；坑下面有神器就归他。它需要的只有两样东西——坑的贴图（`MP2::getDiggingHoleSprite()`，`mp2.cpp:937`）和神器埋在
哪儿（`_ultimateArtifact`）。同一段里还有一个跨王国的「这个对象被碰过吗」（`isAnyKingdomVisited()`，`world.cpp:1272`）。

**决定**：

1. **坑贴图表放 Game 层，`Game/Maps/DiggingHole.cs`**。参考的表在 `mp2.cpp`，参数是 `Maps::Ground`；端口的
   `MapGround` 在 `FHeroes2.Game.AdventureMap`，`FHeroes2.Assets` 不能反向依赖它，所以表跟着枚举走。
2. **水面挖不了：返回 `false`，不 assert、不留对象部分**。参考在 `getDiggingHoleSprite` 的 `WATER` 分支和调用处各写
   一个 `assert(0)`；端口把「挖不了」当成正常输入处理，因为地图上水面就在那里，英雄站在船上挖是玩家会做的事。
3. **坑是地形层的一个部分，带新 UID**。它不是纯装饰：地形层决定通行与渲染，UID 让它和同一格上别的对象区分开。
4. **神器只标「已找到」，不从地图上移除**。参考的 `_ultimateArtifact` 记的是位置，不是地图对象；`markAsFound()` 之后
   同一格再挖返回 `false`，坑照留。
5. **神器归世界所有**（`World.UltimateArtifact`，`world.h:358`），因为它同时回答三个问题：`WINS_ARTIFACT` 条件、
   法师之眼的拼图提示、挖掘。归英雄会因为换手而丢。
6. **`isDiggingHoleSprite` 先保留、暂不接线**。它唯一的调用者是拼图模型的 `MP2::isHiddenForPuzzle()`，那个模型没移植；
   两张表是一对，拆开放反而要将来再找回来。文件注释写明了这一点，等拼图落地时接上。
7. **神器埋放不进这一批**。装载器要读对象的扩展 metadata（半径 + 神器 id）并挑格子（`setUltimateArtifact()`，
   `world_loadmap.cpp:1596-1662`），与 metadata 一起做更完整；在此之前 `UltimateArtifact.Position == -1`，
   `DiggingForUltimateArtifact()` 永远挖不到东西——这正是「地图里没有埋宝」的正常状态。
8. **`IsAnyKingdomVisited` 只是委托**。参考自己循环王国，端口那份循环已经在 `Kingdoms.IsAnyVisited`（`kingdom.cpp`），
   这里换一次参数顺序（参考是 `(type, index)`，端口是 `(index, type)`）交给它，不复制第二个循环。

**后果**：新增 `Game/Maps/DiggingHole.cs`；`World` 加 `_ultimateArtifact`/`UltimateArtifact`/
`DiggingForUltimateArtifact`/`IsAnyKingdomVisited`，`Reset()` 清神器。测试 7 条（`WorldUltimateArtifactTests`）。
Game 2531 → **2538**，全解决方案 3288 → **3295**，`-warnaserror` 0 警告。覆盖率不变：`world` 仍 1/4/0，合计仍
**48/69/135**。

**遗留**：`setUltimateArtifact()` 与它需要的对象 metadata、拼图模型（接 `DiggingHole.IsSprite`）、`NewMonth`/
`_monthOfMonstersAction`、`getCurrentRumor`、`generateBattleOnlyMap`。

## ADR-160：`world` 第五批——周与月、埋宝、可占领对象、战棋世界与法文文本 ◐

**背景**：ADR-159 收尾时点名的四条尾巴（`setUltimateArtifact`、`NewMonth`/`_monthOfMonstersAction`、
`generateBattleOnlyMap`、`fixFrenchCharactersInStrings`）加上 `world_loadmap` 的可占领对象表，是 `world`
目录最后一批还能靠现有系统立起来的函数。`getCurrentRumor` 是唯一一个依赖外部字符串系统的（见第 8 条）。

**决定**：

1. **周与月的钟表照抄参考的取模**：`BeginWeek()` 是 `day % 7 == 1`、`BeginMonth()` 是 `day % 28 == 1`
   (`world.h`)，时钟从 0 起，所以**第 8 天才开第二周、第 29 天才开第二月**；`NewDay` 先 `++_day`，再按两位
   推进 `_week`/`_month`，然后按「月 → 周 → 城/王国」的顺序调。第一周不刷新周命对象（参考的 `_week > 1`
   守卫），但招募池的清空在第一周也做——那两句在 `NewWeek` 的守卫之前。
2. **`World.Week.cs` 装日历与怪物月、`Maps/TileWeekLife.cs` 装瓦片刷新**。`updateObjectInfoTile()`
   (`maps_tiles_helper.cpp:1641`) 是瓦片自己的事（魔法花园 5 宝石或 500 金、水车首日 500 之后 1000、风车
   循环抽到非木再给 2 份、二十种住宅加人口），跟着 `Tile` 的其它 partial 走比塞进 `World` 更贴近参考。
3. **埋宝叫 `PlaceUltimateArtifact` 而不是 `setUltimateArtifact`**：端口已经有 `World.UltimateArtifact` 属性，
   C# 里属性和方法不能同名。行为逐条对齐 `world_loadmap.cpp:1596-1662`：找 `ActionRandomUltimateArtifact`
   占位物 → 读 metadata（`[0]` 半径、`[1]` 神器 id）→ 占位物与其**左格影子**一起用 `RemoveObjectPartsByUID`
   移除 → 半径 0 表示全图、半径 >0 时取 `GetAroundIndexes` 的邻格加自身 → `Width / 9` 的边缘一律不用 →
   挑不到就落回占位格。装载时在瓦片定稿之后、`PostLoad` 建缓存之前调用（`world_loadmap.cpp:1451`）。
   `UltimateArtifact` 归 `World`（ADR-159），这里只负责放。
4. **`SetMonster` 改存 `ActionMonster`**。这是一处真 bug：端口一直写非动作值 `Monster`，而
   `NewWeek` 的怪物成长、怪物月的邻格排除、`GetMonstersProtectingTile` 的落格判定都按 `ActionMonster` 找，
   于是「中立怪每周成长」与「怪物月避开已有怪」永远不生效（更早的寻路测试也只好用 `Monster` 当挡路样本）。
   参考的 `setMonsterOnTile()` 写的是 `MP2::OBJ_MONSTER`（带动作位，`maps_tiles_helper.cpp:1727`），与
   ADR-075「瓦片存文件给的值」一致，所以现在写 `ActionMonster`；寻路里那条 `Monster` 分支同步改成
   `ActionMonster`，两条既有测试（`TileTroopsTests`、`WorldPathfinderTests`）随之更正，
   `ReusesTheSearchWhenNothingChanged` 的期望也从「绕过去 >300」变成「怪物守护的格不再展开 → 0」，正是参考的
   `processCurrentNode` 行为。
5. **可占领对象表照表逐项落**：锯木厂 `0x00`、炼金实验室 `0x01`、五座矿 `0x02…0x06`、灯塔 `0x64`、龙城
   `0x65`、废矿 `0x67`，一律 `PlayerColor.None`；解析器已经把字节换成对象类型，所以端口只判「未知类型」
   （`Mp2ObjectType.None`）与「越界」两件事，各留一条警告并跳过。**不在这里写 ownership flag**——参考也不写，
   flag 归信息块（`world_loadmap.cpp:498` 那段）。
6. **只打一场的世界不经过 `LoadMap`**。`GenerateBattleOnlyMap` 直接调 `GenerateUninitializedMap(2)`、自己给
   四个瓦片编号、再 `SetUniformTerrain`（`world.cpp:338-357`）。绕开 `LoadMap` 有两个理由：参考的
   `generateUninitializedMap` 不做埋宝，而端口的 `LoadMap` 会；2×2 也不是合法的 MP2 尺寸。
   `SetUniformTerrain` 取 `MapGrounds.StartImageIndex`（参考是 `getTerrainStartImageIndex`），并把 shape 位清 0。
7. **法文文本修复：表是纯函数，门控交给调用方**。表落在 `MapTextCodec.FixFrenchCharacters`（`ui_font.cpp:7190`
   的 16 项，返回新串而不是原地改字节）；`World.FixFrenchCharactersInStrings` 覆盖英雄名、城堡名、传闻、每日
   事件、指示牌/漂流瓶、地面事件与斯芬克斯的谜面与答案。参考只在**界面语言是法语**时从装载器调用它，端口没有
   语言层，所以不在 `LoadMap` 里自动跑——对英文/中文地图跑一遍会把标点变成重音。为此把
   `MapSign.Message`/`MapEvent.Message`/`MapSphinx.Answers`/`MapSphinx.Riddle`/`EventDate.Message` 的属性从
   `private init` 放宽到 `private set`（构造仍走各自的 `TryRead`，外部看不出区别）。
8. **`getCurrentRumor` 不转**。它四分之一的分支要 `UltimateArtifact.GetName()`（神器名表）和区域名
   （`getUltimateArtifactRegion()` 一类），这两套字符串属于资源/本地化模块，端口都还没有；半截实现只会返回
   `UltimateBook` 这样的枚举名。等神器名表落地再补，届时它是一段纯粹的拼接。

**后果**：新增 `Game/World/World.Week.cs`、`Game/World/World.UltimateArtifact.cs`、
`Game/World/World.FrenchText.cs`、`Game/Maps/TileWeekLife.cs`;`World.LoadMap` 加埋宝与可占领对象表，`NewDay`
接上周/月，`World` 加 `GenerateBattleOnlyMap`/`SetUniformTerrain`/`FixFrenchCharactersInStrings`；
`MapTextCodec` 加 `FixFrenchCharacters`，`Funds` 加 `Rand`，`Kingdoms` 加 `ResetRecruits`，`AllHeroes` 加
`ResetModes`。新增测试 31 条（`WorldWeekTests` 9、`WorldCaptureObjectsTests` 10、`WorldTests` 3、
`WorldSphinxAndEventsTests` 5、`MapTextCodecTests` 4）。Game 2538 → **2565**，Assets 412 → **416**，全解决方案
3295 → **3326** 全绿，`-warnaserror` 0 警告。覆盖率不变：`world` 仍 1/4/0，合计仍 **48/69/135**（`world.cpp`
与 `world_loadmap.cpp` 都还在「部分」里，剩下的是 AI 侧与区域）。

**遗留**：`getCurrentRumor`（要神器名表与区域名）、`world_loadmap` 的 AI 区域、`Kingdom::ActionAI*` 那一半王国
动作、战役的两条特殊条件、拼图模型（接 `DiggingHole.IsSprite`）。

## ADR-161：`ai` 的契约层——先把签名立起来 ◐

**背景**：`ai` 目录的九个文件（`ai_battle`/`ai_common`/`ai_hero_action`/`ai_personality`/`ai_planner` 及其
`ai_planner_{castle,hero,kingdom,internals}`）此前一个都没转（`ai_personality.h` 的枚举除外，它落在
`System/AiPersonality.cs`）。这些文件的好处是**接口几乎全在头文件里**：`ai_planner.h` 的 281 行里公开契约与
数据结构占了 160 行，`ai_battle.h`/`ai_common.h`/`ai_hero_action.h` 同理。因此先把签名照抄成 C#，让后面写
`ai_planner_hero.cpp` 那几千行动作代码时有地方落、有地方调，比边写边改签名便宜。

**决定**：

1. **只落公开契约，不落私有方法**。`Planner` 有三十来个小方法（`getObjectValue` 的值表旁支、`evaluateRegionSafety`、
   `getMineralValue`、`followHero` 之类），它们全是 `ai_planner_{hero,castle,kingdom}.cpp` 的内部细节：没有跨文件
   调用者，参数形状也依赖尚未落地的东西（对象价值表、AI 寻路器）。提前声明等于猜，落地时必然改签名，所以干脆
   不写——公开契约是「谁调用 AI」，私有方法是「AI 怎么写」，后者只能在写实现的那一批一起出现。
2. **`fheroes2::GameMode` 折成 `AiTurnOutcome`**。`Planner::KingdomTurn()` 返回 `GameMode`，但它只会产出两个值：
   `END_TURN` 与 `QUIT_GAME`（AI 认输、自动战斗超时）。端口还没有 `GameMode`（场景是 `App/SceneKind` + 各 Scene
   自己决定下一幕），把整个 `game_mode.h` 拉进来只为了两个值不值得，因此定义 `AiTurnOutcome { EndTurn, QuitGame }`，
   并在文档里写明它对应哪两个参考值。等游戏的回合循环（`Game::StartGame`）落地，如果那时确实需要完整的
   `GameMode`，换过来只是一处 `switch` 的改动。
3. **`HeroBase&` 折成 `IArmyCommander`**。`HeroesPreBattle(HeroBase&, bool)` 的实参既可能是冒险地图英雄，也可能是城堡
   队长；参考用基类统一，端口没有 `HeroBase`（`Hero` 与 `Captain` 各写各的），但两者都实现了
   `IArmyCommander`（战斗侧已有的接口）。签名取 `IArmyCommander`，注释里说明「实现若真需要英雄专属成员再窄化
   成 `Hero`」。
4. **`Battle::Actions&` 折成 `List<BattleCommand>`**。参考的 `BattlePlanner::BattleTurn(arena, unit, Actions&)` 往一个
   动作队列里塞若干命令，由战斗循环稍后执行；端口没有队列，`BattleArena.ApplyAction(BattleCommand)` 是一次一条。
   所以 `BattleTurn` 收一个 `List<BattleCommand>`，语义一致（一次决策一整回合，按序执行），只是容器换了。
5. **真行为照写，其余 `throw new NotImplementedException`**。契约层不是一行不写：`RegionStats` 的默认值
   （`highestThreat = -1`、`spellLevel = 2`）、`BudgetEntry.Reset()`、`PriorityTask` 的构造函数、
   `SpellcastOutcome.UpdateOutcome()`（含「群体法术累加、单体取最大、不碰 `destinationCell`」这三条）、四个
   `ARMY_ADVANTAGE_*` 常量都是照参考抄的真代码，可以直接写。只有「要读地图/要算价值/要改游戏状态」的成员才抛，
   且抛的消息里带参考文件名，方便 grep 回源头。
6. **单例照旧**：`AiPlanner.Instance`（`Planner::Get()`）与 `BattlePlanner.Instance` 都是懒汉单例，与参考一样保存
   一整个回合的状态（优先级目标、敌人军队、预算、寻路器缓存）。`ResetPathfinder()` 现在是空实现——AI 侧寻路器
   （`AIWorldPathfinder`）还没转，planner 手里没有可清的东西；玩家侧的 `World.ResetPathfinder()` 已接（ADR-091）。
7. **`AIWorldPathfinderStateRestorer` 不落**（`ai_planner_internals.h:32-56`）。它的全部工作就是保存/恢复
   `AIWorldPathfinder` 的两个开关（`isOnBeach` 一览、`debug`），没有那个类它无事可做；`ai_planner_internals.h`
   这一批只落四个常量。
8. **文件名对齐参考**：`AiPlanner.cs`（`ai_planner.h`）、`AiPlannerTypes.cs`（同头里的五个数据结构）、
   `AiCommon.cs`、`AiBattlePlanner.cs`（`ai_battle.h` + 那个自由函数 `getBerserkerTarget`）、`AiHeroAction.cs`、
   `AiPlannerInternals.cs`。放在 `Game/Ai/` 而不是新开项目：契约依赖 `Kingdom`/`Castle`/`Hero`/`Army`/`Funds`/
   `UltimateArtifact`/`Tile`，全部在 `FHeroes2.Game` 里。
9. **`dialog` 这一轮不写代码骨架**，理由与替代路径见 ADR-162。

**后果**：新增 `Game/Ai/AiPlanner.cs`、`AiPlannerTypes.cs`、`AiCommon.cs`、`AiBattlePlanner.cs`、`AiHeroAction.cs`、
`AiPlannerInternals.cs`（6 个文件，约 640 行）。新增测试 13 条（`AiContractTests`）：三个枚举值、`RegionStats` 的
未求值状态、`BudgetEntry.Reset`、`EnemyArmy` 的两个构造、`PriorityTask` 的两种构造、`AiCastle` 的空值检查、
`UpdateOutcome` 的三条语义、两个单例、四个常量、一条「未移植的成员会显式抛」。Game 2565 → **2578** 全绿，
全解决方案 `-warnaserror` 0 警告。覆盖率不变（`ai` 仍 0 个整篇落地：`ai_personality.cpp` 在 `System/` 里只落了
枚举与随机/取名两个函数）。

**遗留**：`ai_battle.cpp`/`ai_common.cpp`/`ai_hero_action.cpp`/`ai_planner*.cpp` 的实现（契约已就位，填实现不需要
再动签名）；`AIWorldPathfinder`（契约里的 `ResetPathfinder` 等它）；`AiCommon` 的 `Funds?` 返回值等
`Economy`/市场规则落地后一起校。

## ADR-162：`dialog` 暂不写代码骨架——它是阻塞式门面，端口是场景式 ◐

**背景**：用户希望 `ai` 与 `dialog` 都先把接口写好。`ai` 的头文件天然是契约，照抄即可（ADR-161）；`dialog` 不是：
`dialog.h` 的 40 余个函数全是**阻塞式**的——`int SelectCount(...)` 内部跑一遍事件循环、返回按钮位；
`Dialog::GameInfo()` 自己读 `world.GetKingdoms()` 并画完整个窗口。端口从第一个 `SettingsDialog` 起就没有
事件循环：对话框是**不可变模型 + Scene 分派事件**（`Area`/`Buttons`/`Render`，按下哪个按钮由 Scene 读
`ButtonGroup` 的返回值）。照抄 `dialog.h` 的签名会得到一堆「阻塞函数」，与端口范式正面冲突，等于写一批注定要
重写的死代码。

**决定**：

1. **不照抄 `dialog.h` 的函数签名**。端口里「一个对话框」= 一个模型类（`Create(...)` + `Area` + `Render`）+
   一个结果表达（`DialogButton` 或该对话框自己的枚举），事件由 Scene 处理。`SettingsDialog`、`MessageBox`、
   `ScenarioSelectionDialog`、`LanguageSelectionDialog` 都已经是这样，`dialog.h` 的 24 个文件落地时照此办理。
2. **公共结构层已经齐了，不用再造**：`dialog.h` 里的 `ResizableFrameBox`/`FrameBox`/`FrameBorder` 在端口对应
   `Dialogs/FrameBox.cs` 与 `Dialogs/FrameBoxChrome.cs`（参考的继承层次被折成了组合，行为已在
   `FrameBox` 的构造与 `Chrome` 的九宫格里）；`StandardWindow`/`StandardWindowChrome` 对应 9-patch 窗口。
   所以 dialog 的「接口」不缺口在这一层。
3. **真正的缺口是控件**。24 个对话框全都站在 `fheroes2::DialogElement`/`TextBase`/`Slider`/`Scrollbar`/
   `OptionButtonGroup`/`TextBox` 上，这些一个都没转（端口现有 `Button`/`ButtonGroup`/`ScrollSlider`/`ButtonSprite`）。
   没有它们，`ArmyInfo`、`Marketplace`、`SelectCount` 的签名连参数都写不出来（`SelectCount` 收
   `const DialogElement* topUiElement`，`inputString` 收两个 `TextBase`），硬写只能靠猜控件形状，必然是返工。
4. **顺带确认的既有原则**：`Dialogs/DialogButton.cs` 的注释写着「只落四个按钮位，布局位（`Dismiss`/`Upgrade`/
   `Prev`/`Next`）跟用到它们的对话框一起加，不提前造值」。这条正好适用于现在的局面：`dialog.h` 的
   `DISMISS/UPGRADE/MAX/PREV/NEXT/WORLD/PUZZLE/INFO/DIG` 九个位值虽然照抄无风险，但没有任何调用者，先加就是死值。
   等第一个用到它们的对话框落地时一起补，那时还能顺手为 `ButtonGroup` 的布局参数写测试。

**后果**：本轮 `dialog` 目录零代码改动。下一次动 `dialog` 的正确顺序是：先落**控件契约**
（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/`OptionButtonGroup` 的端口形态，含 `Rect` + 渲染 + 事件三件套），
再按 `dialog.h` 的清单从最小的对话框（`ArtifactSetAssembled`、`BuyBoat`）开始验证范式，最后才是 `ArmyInfo`/
`Marketplace`/`KingdomOverview` 这些互相牵连的大块。

**遗留**：`dialog` 的 29 个文件全部未转；它们的共同前置是上面那批控件。

**后续**：第 3、4 条的判断没有变，但用户选择先要入口契约再做控件层，因此 `dialog.h` 的 24 个入口按
「模型 + `Create` + 结果类型」直接落下，见 ADR-163。`DialogButton` 的布局位也随之补齐——理由从「等调用者」
变成「已经有调用者」。上面「下一次动 `dialog` 的顺序」一条作废。

## ADR-163：`dialog` 的 24 个入口契约层 ◐

**背景**：ADR-162 判断 `dialog` 的代码骨架要等控件层，用户选择反向推进：先把 `dialog.h` 的 24 个入口按
「模型 + `Create` + 结果类型」落成可编译的契约，控件参数先占位，接受后续返工。理由也站得住——入口形状决定
`App` 层的场景怎么调它，这件事不依赖 `DialogElement` 长什么样。

**决定**：

1. **24 个入口落成 20 个模型文件**（另扩 `DialogButton`），放在 `src/FHeroes2.UI/Dialogs/`，命名与参考的
   `.cpp` 对齐：
   `AdventureOptionsDialog`（`dialog_adventure.cpp`）、`FileOptionsDialog`（`dialog_file.cpp`）、
   `SelectFileDialog`（`dialog_selectfile.cpp`，两个入口一个类）、`QuickInfoDialog`（`dialog_quickinfo.cpp`，七个
   入口一个类）、`LevelUpDialog`（`dialog_levelup.cpp`）、`GoldOrExperienceDialog`（`dialog_chest.cpp`）、
   `ArenaDialog`（`dialog_arena.cpp`）、`SelectCountDialog`（`dialog_selectcount.cpp`）、`InputStringDialog`
   （同文件）、`ArmySplitDialog`（同文件）、`RecruitDialog`（`dialog_recruit.cpp`）、`DwellingInfoDialog`（同）、
   `ArmyInfoDialog`（`dialog_armyinfo.cpp`）、`ArmyJoinDialog`（同，两个入口一个类）、`MarketplaceDialog`
   （`dialog_marketplace.cpp`）、`GiftResourcesDialog`（`dialog_giftresources.cpp`）、`BuyBoatDialog`
   （`dialog_buyboat.cpp`）、`ThievesGuildDialog`（`dialog_thievesguild.cpp`）、`GameInfoDialog`
   （`dialog_gameinfo.cpp`）、`ArtifactSetDialog`（`dialog_artifact.cpp`）。`ResizableFrameBox`/`FrameBox`/
   `FrameBorder` 三个类不重复实现（`FrameBox`/`FrameBoxChrome` 已有，ADR-162 第 2 条）。
2. **模型只承载「输入 + 答案」，不写 `Area`/`Render`**。参考的对话框自己画、自己跑事件循环；端口的场景发事件、
   模型描述内容。所以这一批只定 `Create(...)` 与结果，布局与渲染等控件层（`DialogElement`/`TextBase`/`Slider`/
   `Scrollbar`/`OptionButtonGroup`）落地时补——**补的时候 `Create` 的签名不变**，因为它的参数全是业务值。
3. **`DialogButton` 补齐到参考的 14 个值**（`dialog.h:65-85`）：四个按钮位 + `Dismiss`/`Upgrade`/`Max`/`Prev`/
   `Next`/`World`/`Puzzle`/`Info`/`Dig`。这不再是「提前造值」：`AdventureOptions` 的答案就是 `WORLD|PUZZLE|INFO|DIG`，
   `ArmyInfo` 的按钮就是 `UPGRADE|DISMISS`，有真实调用者。数值照抄参考，测试逐值钉住。
4. **`GameMode` 折成 `FileOptionsResult`**。`Dialog::FileOptions()` 返回 `fheroes2::GameMode`，但它只产出
   `NEW_GAME`/`LOAD_GAME`/`QUIT_GAME`/`CANCEL` 加上两个存档动作（`EventSaveGame`、`Game::QuickSave`），
   `MAIN_MENU`/`LOAD_STANDARD`/战役值都不会出现。端口按场景组织，「存档」「快速存档」是两件不同的事，所以结果
   是六个值：`Cancel`/`NewGame`/`LoadGame`/`SaveGame`/`QuickSave`/`Quit`。
5. **结果类型一律表达参考的返回语义**：返回 `bool` 的（`SelectCount`/`inputString`/`SelectGoldOrExp`）改成可空的
   值（`int?`/`string?`/`GoldOrExperience`），返回 `-1`/`UNKNOWN` 表示取消的改成 `null`（`MakeGiftResource`、
   `LevelUpSelectSkill`、`SelectSkillFromArena`），返回空 `Troop` 的改成 `Troop?`（`RecruitMonster`）。理由一致：
   参考用哨兵值表达「没有」，C# 有 `null`，用 `null` 可以少一类误读。
6. **`FromButton(int)` 是纯映射，属于真行为**。参考把「按了第几个按钮 → 返回什么」写成事件循环里的
   `if (le.MouseClickLeft(button[i])) return ...;`，端口把它抽成静态方法，逐条照抄按钮顺序，测试直接钉住
   （`AdventureOptions` 4 项、`FileOptions` 6 项、`BuyBoat`、`ArmyInfo`、`ArmyJoin`、`ArmySplit`、`ArmyInfo` 等）。
7. **参数校验按参考的断言补**：`SelectCount` 的 `min <= max`、`step > 0`（参考的 `step` 若为 0 会死循环），
   `ArmySplitTroop` 的两个非负值，`ArmyJoinWithCost` 的 `join > 0`。这些是 `Create` 里唯一的真逻辑，都写成
   `ArgumentOutOfRangeException`。
8. **三处占位，都在注释里点名**：（a）`inputString` 的 `fheroes2::TextBase` 用 `string` 代替（端口没有 `TextBase`）；
   （b）`SelectCount` 的 `topUiElement`/`bottomUiElement` 与 `ArmyInfo`/`RecruitMonster`/`QuickInfo` 的屏幕偏移
   `offsetY` 不建模（纯渲染参数）；（c）`SelectFileLoad`/`Save` 的文件列表不建模（存档目录还没有端口），
   `Choose(string?)` 先由场景把选中项写回。
9. **纯显示类不给结果**：`DwellingInfo`、`ThievesGuild`、`GameInfo`、`ArtifactSetAssembled` 只有 `Create`，
   因为参考返回 `void`，它们的效果全在画面上。

**后果**：新增 20 个模型文件（`src/FHeroes2.UI/Dialogs/`，1 225 行），`DialogButton` 从 4 个值扩到 14 个。
新增测试 25 条（`DialogModelsTests`：14 个位值、`AdventureOptions` 的两种按钮集与 6 条映射、`FileOptions` 的 6 条
映射、`BuyBoat`/`ArmySplit`/`ArmyJoin`/`ArmyInfo` 的映射与 `HasButtons`、`SelectCount` 的 clamp 与两条拒绝、
`inputString` 的上限、`Recruit` 的空栈、`LevelUp` 的未提供技能、`GiftResources` 的越界、`QuickInfo` 的三种目标）。
UI 145 → **170**，全解决方案 3 339 → **3 364** 全绿，`-warnaserror` 0 警告。覆盖率不变（`dialog` 仍 0/0/29）：这一批是契约，不是
实现。

**遗留**：全部 `Render`/`Area`/事件处理与控件层（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/
`OptionButtonGroup`/`TextBox`）；`PaymentConditions`（`BuyBoat` 的价格与能否购买）；存档目录（`SelectFile`）；
`QuickInfo` 的雷达与定位绘制；`GameInfo`/`ThievesGuild` 要读的世界数据（端口 `World`/`Kingdoms` 已有，实现时传）。

## ADR-164：`AIWorldPathfinder` 与玩家寻路器共用骨架 ◐

**背景**：`world_pathfinding.cpp:543-1434` 的 AI 侧一直以「要军队与对象价值表」为由押后。军队、逐格成本与
守怪强度（`WorldMapScan.IsTileProtectionStrongerThan`）就位后，它只剩自己的通行判定与成本记账，不必再等
对象价值表——那是 `ObjectValueStorage`，AI 计划器的事。

**决定**：

1. **继承 `WorldPathfinder`，不复制搜索**。参考的 `AIWorldPathfinder : PathfinderBase` 与玩家侧共用缓存、标签
   修正、邻居展开和「最后一格」；端口把同一骨架留在基类，只放开四个钩子（`ProcessWorldMap`、
   `ProcessCurrentNode`、`GetMovementPenalty`、`GetMaxMovePoints`）并让 `IsMovementAllowed` 可覆盖。基类
   `GetMaxMovePoints` 的「最后一格」判据在 AI 报 0 时自动失效，AI 侧不重写。
2. **AI 的三份重计算缓存放进 `WorldNode`**。参考把 `std::optional<bool> _isAccessibleForAI` 与
   `_isAvailableForWalkThroughForAI.{fromWater,fromLand}` 放在节点里，端口原样落成三个 `bool?` 字段。语义也照
   抄：`reset()` **不清**它们，整表失效发生在 `ProcessWorldMap` 的 `ClearCache()`——这一点容易看错，测试
   `TheWorldOwnsOneAiPathfinderAndHandsItToThePlanner` 因此改用可调参数观察 `Reset()`，而不是看距离。
3. **全局单例换成「世界持有 + 交给计划器」**。参考各处调用 `AIWorldPathfinder::Get()`，它背后是唯一的全局
   `world`。端口由 `World.AiPathfinder` 惰性创建，并在创建时 `AiPlanner.Instance.AttachPathfinder(...)`，让计划器
   仍有一个入口可取；`World.ResetPathfinder()` 重置这一份，避免多世界测试里重置到别人的缓存。
4. **没数据的成员不猜**。`ReEvaluateIfNeeded(Hero, double)`、`GetFogDiscoveryTile`、`GetNearestTileToMove`、
   `IsHeroPossiblyBlockingWay`、`BuildDimensionDoorPath`、`GetObjectsOnTheWay` 需要英雄↔城堡关系、集合点、
   装备或冒险地图对象扫描；一律抛 `NotImplementedException`，XML 注释写明缺哪份数据。
5. **`BuildPath` 的裁剪只在 `accountNearestObject` 时发生**。参考在回溯时用基类通行判定记下最后一个「不可穿越」
   的格，再把从它出发的步骤删光；`accountNearestObject == false` 时原样返回整条链。初版把裁剪做成了无条件的，
   会让「目标本身是拾取物」的路径少一步；本轮按参考改回，并补测试钉住两种形态。裁剪判定的水陆身份取**整条路
   起点**，不是逐格 `from`。

**后果**：新增 `src/FHeroes2.Game/World/AIWorldPathfinder.cs`（约 900 行）与 `WorldMapScan` 的守怪强度；`WorldNode`
多三个 `bool?`；`World` 多一个 `AiPathfinder`。新增测试 13 条（`AIWorldPathfinderTests`）。Game 2578 → **2591**，
全解决方案 3 364 → **3 377** 全绿，`-warnaserror` 0 警告。

**遗留**：`ai` 的九个 `.cpp` 实现（含 `ObjectValueStorage` 与 AI 计划器私有方法）；上面第 4 条那一串入口。

## ADR-165：城堡视图先落数据层，界面等渲染底子 ◐

**背景**：`castle` 目录的数值与规则早已落地，剩下的全是 `buildinginfo.cpp` 与五个界面 `.cpp`。界面要的控件层
（`DialogElement`/`TextBase`/`Slider`/`Scrollbar`/`TextBox`）与城堡屏都还没有，整块搬过来会一行也跑不通、
一条也测不了。但 `buildinginfo.cpp` 里有一半与贴图无关：`BuildingInfo` 的四个改写与两段文案、`DwellingsBar`
的条目与布局、以及住所描述要用的怪物复数名。

**决定**：

1. **先把与贴图无关的三块落下**：`Castle/BuildingInfo.cs`、`Castle/DwellingsBar.cs`、`Monsters/MonsterNames.cs`。
   绘制（`BuildingInfo::Redraw`/`Reset`）、弹窗（`QueueEventProcessing` 打开的招募与购买窗）留给城堡屏。
2. **`BuildingInfo` 是独立类，不是 `Castle` 上的方法**。参考即如此，且一个城堡要持有一屏建筑（每个带自己的
   位置与状态），挂在 `Castle` 上会把位置也塞进存档模型。
3. **状态只在两处换条件文案**。参考的构造函数仅对 `BuildDisabled`/`ShipyardNotAllowed` 调
   `GetConditionDescription()`；剩下五个分支（缺钱、已建、前置、可建、需要城堡）在这条路径上走不到。端口照抄
   这些分支并注释，不去「修正」——它们是界面别处的文案来源，擅自接上会改变状态栏的显示。
4. **`DwellingsBar` 不抽象成 `ItemsBar<T>`**。参考的 `ItemsBar` 是模板，目前只有一个用户；`KingdomOverview`
   将来也要用时再抽，现在抽只会为一个用户写一套泛型布局。
5. **`RenderingOffset` 重算整栏矩形**（唯一的行为偏离）。参考的 `setRenderingOffset()` 只重算图标位置，若先设
   表格再移动整栏，`getArea()` 就停在旧坐标。端口让两者一起更新：`Area` 回答的是「此刻」的栏，与 setter
   的调用顺序无关。代价是与参考在「中途移动」这一种情况下的 `getArea()` 不同。
6. **怪物名单独成表**。`MonsterStatistics` 明确只存数值（注释写着 minus the two names），名字放
   `MonsterNames`，与参考同一张表的顺序，`MonsterType.MonsterCount` 是长度并在静态构造里断言。复数取参考的
   原文（`Magi`/`Archmagi`/`Cavalries`），不做机械加 s。
7. **队长的矩形保留 `135x70`**。参考按 `ICN::getCaptainIcnId( race )` 的贴图尺寸算，端口在 `BuildingInfo` 附近
   拿不到贴图，注释写明等城堡屏接上渲染器再算。

**后果**：新增 `BuildingInfo`（约 230 行）、`DwellingsBar`（约 300 行）、`MonsterNames`（约 150 行数据）。
测试 21 条（`BuildingInfoTests` 10、`DwellingsBarTests` 7、`MonsterNamesTests` 4）。Game 2591 → **2612**，
全解决方案 3 377 → **3 398** 全绿，`-warnaserror` 0 警告。

**遗留**：`BuildingInfo` 的绘制与交互；`castle_building.cpp` 的 blit 与五个界面 `.cpp`；控件层与城堡屏。

## ADR-166：建筑绘制队列与淡入落在数据层，帧钟当参数 ◐

**背景**：`castle_building.cpp`（500 行）整份是绘制，但其中三块与 blit 无关：建筑淡入的 alpha 状态机、
「按绘制优先级 + 屏幕偏移」排好的矩形队列、以及绘制循环开头「这栋画不画 / 要不要补一段墙或桥」的判定。
不把它们落下，城堡屏接上渲染器时还要重新推一遍这些规则；而且这些规则里有一处（升级替代基础建筑）的例外
很容易做错。

**决定**：

1. **`FadeBuilding` 独立成类，帧钟当参数**。参考的 `updateFadeAlpha()` 先问
   `Game::validateAnimationDelay( CASTLE_ITEM_UPDATE_DELAY )`；端口没有帧循环，也没有延迟抽象（搜遍 `src` 无
   `DelayType`），所以 `Update( bool delayElapsed )` 把「这一帧是否到期」收成入参，返回值表示 alpha 是否推进。
   其余（0 起步、`< 246` 加 9 否则 255、`IsOnlyBoat`）照抄。
2. **队列在构造时就把 `top` 加进矩形**。参考 `BuildingsRenderQueue( castle, top )` 对每个 area 做 `area + top`，
   绘制侧不再补偏移。端口保持这个分工，`Areas` 一律是屏幕坐标——测试因此能直接断言偏移结果。
3. **判定抽成 `CastleBuildingRender` 的静态方法，而不是留给绘制循环**。参考把「未建不画」「升级版已建则不画
   基础」「升级版正在淡入则是例外」写在 `redrawCastleBuildings()` 的两个分支里，两处条件只差一个
   `upgrade != fadingInBuildingId`。端口合并成一个 `ShouldDrawBuilding( castle, building, fadingIn )`：
   `fadingIn == Nothing` 时第三条件恒真，等价于无淡入那支。连接件判定 `IsBuildingConnectionNeeded` 同样抽出，
   它只读 `Race`、`GetLevelMageGuild()` 与建筑位，不需要贴图。
4. **不搬 `CastleGetMaxArea` 与 `getTownIcnId`**。这两者要背景贴图的尺寸与 ICN 编号，端口在数据层拿不到；
   等城堡屏能从渲染器问尺寸时再加。
5. **blit 部分（`redrawCastleBuilding`/`redrawBuildingConnection`/`redrawAllBuildings`）不做**。它们的每个分支都
   以 `drawCastleDialogBuilding( icn, index, ... )` 结尾，没有 ICN 就无法验证任何一行。

**后果**：新增 `FadeBuilding`（约 110 行）与 `BuildingsRenderQueue`（约 180 行，含 `BuildingRenderInfo` 与
`CastleBuildingRender`）。测试 9 条（`CastleBuildingRenderTests`）。Game 2612 → **2621**，全解决方案
3 398 → **3 407** 全绿，`-warnaserror` 0 警告。

**遗留**：`redrawAllBuildings` 的三个绘制函数；`castle_dialog.cpp` 及其余四个界面 `.cpp`；控件层与城堡屏。

## ADR-167：魔法塔行与一键招满落在数据层与 UI 契约，钱与王国由调用方给 ◐

**背景**：`castle_mageguild.cpp` 与 `castle_well.cpp` 的主体是绘制循环，但有两个规则块与 blit 无关：魔法塔窗口
每层摆哪几个法术、水井 MAX 按钮怎么把国库分配掉。前者决定窗口内容，后者的分配顺序与「为什么买不了」的三句话
决定玩家看到的反馈，都是可测的规则。

**决定**：

1. **魔法塔的行数据放 `Castle`，窗口布局放 `UI/Dialogs/MageGuildDialog`**。行的槽位数来自
   `GetMaxSpellsCount( level, HasLibraryCapability )`（只看种族），法术本身来自
   `GetSpells( guildLevel, IsLibraryBuilt, level )`（种族且建了图书馆）——参考就是这样分开问的，端口照抄，
   不合并成一个「能不能多一格」的布尔。行列表保持等级顺序（`Rows[0]` 是一级），屏幕顺序由
   `GetRowTopOffset` 的 `365 - 90 * i` 解释，因为参考把一级画在底部。
2. **窗口不读王国**：参考用 `GetKingdom().GetCastles().size() < 2` 决定前后城按钮是否可点，端口 `Castle`
   没有王国，`Create` 收 `castleCount`。状态栏的两句话按「英雄有没有魔法书」二选一，与参考一致。
3. **一键招满做成纯规划对象**：`CastleMaxRecruitment.Create( castle, castleArmy, guestArmy, treasury )` 返回
   `Entries`/`Cost`/`Summary`/`Refusal`，不弹窗、不扣钱、不 `RecruitMonster`。参考用
   `GetKingdom().AddFundsResource()` 付账，端口让调用方在确认后自己扣，理由同 ADR-164 的 `BuyBuilding`。
4. **两个军是草稿副本**：参考把买到的兵 `JoinTroop` 进 `tempCastleArmy`/`tempGuestArmy`，好让后面的住所
   看见前一步占掉的槽位；端口保持这个副作用，并在文档里写明「传副本」。这也是为何它不能是 `Castle` 的纯函数。
5. **「为什么买不了」只看第一个有库存的住所**：参考在 `break` 上定了性，端口 `GetRefusal` 照抄；并且先判
   钱、再判队伍满，所以「既没钱又满编」报缺钱。`Summary` 的单复数按 `GetPluralName( count )` 的规则取。

**后果**：新增 `Castle/Castle.MageGuildView.cs`（约 60 行）、`UI/Dialogs/MageGuildDialog.cs`（约 110 行）、
`Castle/CastleMaxRecruitment.cs`（约 250 行）。测试 15 条（`CastleMageGuildViewTests` 4、
`MageGuildDialogTests` 4、`CastleMaxRecruitmentTests` 7）。Game 2621 → **2632**，UI 170 → **174**，
全解决方案 3 407 → **3 422** 全绿，`-warnaserror` 0 警告。

**遗留**：`castle_mageguild.cpp`/`castle_well.cpp` 的绘制循环；`castle_dialog.cpp`/`castle_town.cpp`/
`castle_tavern.cpp`；城堡屏与控件层。

## ADR-168：城堡屏只搬文字与返回值，招募英雄的句子抽成静态函数 ◐

**背景**：`castle_dialog.cpp`（859 行）与 `castle_town.cpp`（772 行）几乎全是绘制与事件循环，但里面夹着三处
会改变玩家所见所知的规则：悬停状态行怎么称呼一个住所、屏幕顶端的日期怎么写、招募英雄的窗口说什么话。

**决定**：

1. **状态行放 `BuildingInfo.GetStatusLine`，不放 UI**。它只依赖种族与建筑号，是「这栋房子是什么」的另一种说法，
   与 `GetBuildingDescription` 同类；`UI/Dialogs/CastleDialog` 只是转交。兵种取**复数小写**，因为参考用的是
   `StringReplaceWithLowercase`；随机城镇没有具体兵种，退化成 `"Recruit level 3 creatures"`。
2. **日期行与 `World.DateString()` 并存**。屏幕上是 `"Month: 1, Week: 3, Day: 5"`，日志里是小写形式；两个都是
   参考的原文，合并成一个会让其中一处失真。端口取三个整数作参数，不拉 `World`，便于测试。
3. **返回值八种，`DoNothing` 也算一种**。参考的 `CastleDialogReturnValue` 里「换一座城」「换一个窗口」与「关闭」
   并列，调用方必须能区分「关闭」与「关闭后去下一座城」，所以不做成 `bool` + 附加参数。
4. **招募英雄的文案抽成 `Describe( name, level, race, count )` 静态函数**。端口的 `Hero.Name` 只由 `LoadFromMP2`
   填，新造的英雄名字为空；写成静态函数后，测试能校验整句而不用伪造地图数据，实例属性只是把英雄的四个字段
   喂进去。
5. **魔法书从宝物计数里扣掉**：参考先 `GetCountArtifacts()` 再 `hasArtifact( MAGIC_BOOK )` 减一，因为书放在同一
   个背包里。不减会让「只有一本书」的英雄被说成「with 1 artifact」。三档文案是三条独立句子，不是带数字的模板。
6. **能不能招由调用方给**：`Castle::AllowBuyHero()` 要王国与访客英雄，窗口模型两个都没有，收 `canRecruit`。

**后果**：新增 `UI/Dialogs/CastleDialog.cs`（约 100 行）、`UI/Dialogs/RecruitHeroDialog.cs`（约 150 行），
`BuildingInfo` 追加 `GetStatusLine`（约 25 行）。测试 17 条（`BuildingInfoTests` 增 4、`CastleDialogTests` 4、
`RecruitHeroDialogTests` 9）。Game 2632 → **2636**，UI 174 → **187**，全解决方案 3 422 → **3 439** 全绿，
`-warnaserror` 0 警告。

**遗留**：`castle_dialog.cpp`/`castle_town.cpp` 的绘制与事件循环；`castle_tavern.cpp` 的传闻窗口（要等
`world.cpp` 的传闻池）；`buildinginfo.cpp` 的 `Redraw`/`QueueEventProcessing`；城堡屏与控件层。

## ADR-169：传闻池先立形状，酒馆窗口收一句话 ◐

**背景**：`castle_tavern.cpp`（54 行）只做两件事——取本周传闻、把它和开场句放进一个窗口。传闻来自
`World::getCurrentRumor()`（`world.cpp:632-703`），而端口的 `World` 已经有 `Rumors`（地图带来的原文）与
`WeekSeed`，独缺那个选择函数。按「先城堡、后世界」的顺序，这批要么把世界一起补完，要么让窗口不依赖世界。

**决定**：

1. **传闻池先立形状，函数体不写**。`World/World.Rumors.cs` 落 `StandardRumorCount`（= 9，`switch` 的分支数，
   不是地图上限）、九条内置文案、九个方向词、`TotalRumorCount`；`GetCurrentRumor`/`ChooseRumorId`/
   `GetRegionName` 抛 `NotImplementedException`，注释写参考行号与该做什么。这么做是因为池子的**内容**（文案、
   方向词、条数）是渲染与窗口真正要读的，而**选择**（周种子怎么喂给随机数、地图三等分怎么判边界）可以后补，
   两者的风险不同：前者错了整句都错，后者错了只是换了句传闻。
2. **未实现的函数显式抛异常，不返回空串**。返回空传闻会让酒馆安静地显示一句「…」，玩家与测试都看不出问题；
   抛异常至少会立刻暴露调用点。（更正：AI 层按 ADR-161 本来就有一组这样的桩，所以「唯一一处」的说法不成立；
  这里只是传闻池的首例，注释与文档都指向 ADR-169。ADR-170 已把它补齐。）
3. **语言标签单独成类型**。`LocalizedString`（`Text` + 可选 `SupportedLanguage`）对应参考的 `LocalizedString`
   （`game_string.h:30-50`）：内置九条由界面翻译、语言为空，地图带来的原文带自己的语言，渲染时该用对应字体。
   窗口现在就带着这个标签，字体层学会切换时不用改窗口。
4. **酒馆窗口收 `LocalizedString`，不读 `World`**。这样窗口与未实现的池子解耦：池子晚一点补，窗口照常可用。
   `Create` 拒绝空文本——载入器从不存空传闻，空的说明是 bug 而不是状态，拒绝比画一个空段落好。
5. **标题取建筑名而不是新常量**。参考是 `GetStringBuilding( BUILD_TAVERN )`，而六个种族都叫「Tavern」；端口用
   常量，但测试对着 `CastleBuildingInfo.GetName` 逐个种族核验，避免以后与表脱节。

**后果**：新增 `LocalizedString.cs`（约 30 行）、`World/World.Rumors.cs`（约 250 行，三个函数未实现）、
`UI/Dialogs/TavernDialog.cs`（约 90 行）。测试 13 条（`RumorPoolTests` 7、`TavernDialogTests` 6）。Game 2636 →
**2643**，UI 187 → **193**，全解决方案 3 439 → **3 452** 全绿，`-warnaserror` 0 警告。`castle` 统计 3/7/1 →
3/8/0：目录里已无完全未转的文件。

**遗留**：`World::getCurrentRumor()` 的三个函数体（`GetCurrentRumor`/`ChooseRumorId`/`GetRegionName`）；
`castle_tavern.cpp` 的 `TAVWIN` 动画与布局；`castle_dialog.cpp`/`castle_town.cpp` 的绘制与事件循环；城堡屏与控件层。

## ADR-170：传闻池补齐，句子抽成 `Describe` ◐

**背景**：ADR-169 只立了传闻池的形状，三个函数体抛 `NotImplementedException`。要让酒馆显示真传闻，就得实现
`World::getCurrentRumor()`；而它有两个外部缺口：终极宝物的名字，与自定义传闻的语言标签。

**决定**：

1. **句子抽成 `World.Describe( rumorId, artifactName, region, customRumors )` 静态函数**。参考是一个 `switch` 里
   边选边拼，端口把「拼」拿出来：九个分支与自定义回退都能在无地图、无周数、无宝物的条件下断言，实例方法只剩
   「这两个参数从哪来」。与 `RecruitHeroDialog.Describe`（ADR-168）同一手法。
2. **区域只在 id 为 1 时计算**。tile 下标要除地图宽度才换成坐标，而没载入地图的世界宽度为零；参考不判断是因为
   它总在地图里被调用，端口把这个前提收紧成「只有需要时才碰它」，于是那七句固定文案在空世界上也能回答。
3. **三等分用严格比较，且先判行再判列**。`height / 3 > y` 而非 `>=`，30 格地图的第 10 行已属中间带；行决定南北
   中，列再决定东西，所以中间带回答 `west`/`center`/`east` 而不是自己再切九份。测试钉住四条边界与小地图。
4. **宝物名字只覆盖八个终极 + 占位**。参考读 `getArtifactData( id ).untranslatedName`，那是 `artifact_info.cpp`
   的 104 行表；端口 `ArtifactTable` 只有数值列（脚本生成），`ArtifactBag.String()` 也因「还不画名字」没转。
   而地图只会埋八个终极之一（`UltimateArtifact::Set` 在没指定时从那一池里抽，编辑器也只提供「随机终极宝」），
   所以 `UltimateArtifact.Name` 只列这八个，表外（含 `Unknown`）返回参考自己的 `"Invalid Artifact"` 占位——
   与其编造一个会显示错的名字，不如露出参考的占位词。完整名字表属于文本层，将来连着 `ArtifactBag.String()`
   一起补。
5. **自定义传闻的语言标签返回空**。参考带 `getCurrentMapInfo().getSupportedLanguage()`；端口没有「地图语言」这个
   概念（`MapTextCodec` 解完码就丢，只留法文字符修正），所以给 `null` 而不是拿 `ResourceLanguage.Get()` 冒充——
   那是资材的语言、不是地图的语言，用错会让俄文地图配英文字体。`LocalizedString` 已经在位，等地图记住语言。
6. **越界抛异常而不是断言**。参考 `assert` 后直接索引，release 下是 UB；端口对 `rumorId` 越界与
   `totalRumorCount <= 0` 抛 `ArgumentOutOfRangeException`，消息里写出池子的构成。
7. **酒馆加深 `Create( World )` 重载**。窗口仍不读世界，重载只是替调用方做 `world.GetCurrentRumor()`；城堡屏接
   进来时不用改窗口，也不用在调用点重复这句。

**后果**：`World/World.Rumors.cs` 由骨架改为实现（约 330 行），`UltimateArtifact` 增 `Name`（约 30 行），
`TavernDialog` 增一个转发重载。测试 42 条（`RumorPoolTests` 增 31、`UltimateArtifactTests` 增 10、
`TavernDialogTests` 增 1）。Game 2643 → **2684**，UI 193 → **194**，全解决方案 3 452 → **3 494** 全绿，
`-warnaserror` 0 警告。传闻池里不再有桩（AI 层按 ADR-161 仍有成组的桩，与本批无关）。

**遗留**：地图语言（**已查明不需要补**：`maps_fileinfo.h:148` 的 `getSupportedLanguage()` 只对 Resurrection 地图
返回语言，经典 MP2 地图一律返回空——而端口今天只读 MP2，所以 `null` 就是正确答案，等 `.fh2m` 支持再说）；完整宝物名字表与
`ArtifactBag.String()`；`castle_tavern.cpp` 的 `TAVWIN` 动画与布局；`castle_dialog.cpp`/`castle_town.cpp` 的绘制
与事件循环；城堡屏与控件层。

## ADR-171：神器收入接进王国账本，补两个文案缺口 ◐

**背景**：`Kingdom.GetIncome` 的文档写着「神器与技能两项等英雄的神器袋与技能」，`IncomeType.Artifacts` 也写着
「英雄还没有神器袋，这一位今天加不出东西」——两句都过期了：`Hero.BagArtifacts` 早在 `hero` 批次就落地，
`ProfitConditions.FromArtifact` 也早就把 `artifact_info.cpp` 的收入与惩罚转完。另一边，周名
（`Week::GetName()`）与 `Monster::GetPluralName()` 的注释各自以一个「还缺依赖」的理由停在 ◐，而依赖也都已经在了
（`MonsterNames`、`WeekName`）。

**决定**：

1. **神器收入按整只袋遍历，空格不跳过**。参考是 `for ( const Artifact & artifact : bag )`，14 格全走；空格是
   `UNKNOWN`，表里那行没有 bonus，自然为 0。照做之后「魔法书算不算」这个问题消失了——魔法书是袋里的一格，
   不是另一样东西，它的行同样没有收入。
2. **求和允许为负**。税票 -250 与金袋 +1000 相加是 750；若英雄只带税票，这一位就是 -250。钳制是账本入账时的
   事（`AddFundsResource` 不让金库掉到零以下），`GetIncome` 只负责求和，否则「这一项到底多少」就说不清了。
3. **技能那一项仍留空，但理由改成实话**。英雄今天有袋子没有技能，`HeroSkills` 还是 0；文档从「袋子也没有」
   改成「有袋子、没技能」，并指向 ADR-112 的修正。
4. **周名返回参考的英文原文**，与 `Experience`（ADR-137）一致：端口没有翻译层，返回 key 会让调用处的句子
   （`"Astrologers proclaim the Week of the %{name}."`）读不通。两处反直觉照抄：怪物周返回生物单数名，瘟疫是
   全大写 `PLAGUE`（原版游戏就这么印）。表外回答 `Unnamed`——它只用于打印，与参考的 assert-then-default 同效。
5. **`MonsterNames.GetPluralName( count )` 取 `count == 1` 而不是 `count > 1`**：0 也用复数，参考就是这么写的，
   且 `"0 Peasants"` 正是对话框要的字。
6. **顺手更正两处错注释**，因为它们会让后来的人做错判断：`KingdomIncomeTests` 里「金鹅是唯一带收入的神器」
   （无尽钱袋 500/750/1000 也带），以及 `IncomeType`/`GetIncome` 的「英雄还没有神器袋」。

**后果**：`Kingdom.GetIncome` 增约 12 行，`Week` 增 `GetName()`（约 60 行含注释），`MonsterNames` 增
`GetPluralName`（约 13 行）。测试 13 条（`KingdomIncomeTests` 4、`WeekTests` 8、`MonsterNamesTests` 1）。
Game 2684 → **2697**，全解决方案 3 494 → **3 507** 全绿，`-warnaserror` 0 警告。`kingdom` 与 `monster` 两个
目录的统计不变（改动都落在已经是 ◐ 的文件里）。

**遗留**：`IncomeType.HeroSkills`（等英雄技能）；`IncomeType.CampaignBonus` 与电脑难度加成（等 `Players`/战役存档）；
`kingdom_overview`/`view_world`（渲染 + 对话框）；`monster_info.cpp` 的三支界面文案
（`getMonsterAbilityDescription`、`getMonsterWeaknessDescription`、`getMonsterPropertiesDescription`——ADR-172
补了 `Spell.GetName()`，ADR-173 把三支都转进 `MonsterDescriptions`；同文件的 `getMonsterDescription` 是英文
标签的调试转储，不面向玩家，端口以测试代替）。

## ADR-172：法术的名字与描述也返回参考原文 ◐

**背景**：`spell.cpp` 的 `spells` 表每行七列，`SpellTable` 只搬了五列，注释的理由是「名字与描述是 gettext
字符串，属于翻译层」。这个理由在 ADR-137（`Experience`）与 ADR-171（周名、怪物名）之后已经不成立：端口的做法是
**返回参考的英文原文、不返回 key**，翻译层等它自己的批次。

**决定**：

1. **新表 `SpellText`，一行两列**，与 `SpellTable` 的数值行同序同下标。不把两列塞进 `SpellStats`：数值表的用途是
   「对数字」的测试与战斗公式，文本进去了会让每个数值断言都拖着字符串。也不拆成名字表与描述表：参考里它们
   是同一行的两个字段。
2. **上下文前缀按显示规则去掉**。参考写 `gettext_noop( "spell|Slow" )`，`spell|` 只是给翻译者的上文，gettext
   显示时去掉；端口若照抄会在界面上印出 `spell|Slow`。全表只有 Slow 与 Blind 两处。
3. **参考的拼写优先于枚举名**：`FireBlast`→`"Fireblast"`、`StoneSkin`→`"Stoneskin"`。改词会与原版界面不一致，
   改 enum 会与存档 id 不一致，所以各留原样并在注释里点明。
4. **`%{count}` 不替换**。`Cure`、`Haste`、`Blind` 的句子带占位符，数字由法术自己的代码给；参考的
   `Spell::GetDescription()` 也只返回模板。
5. **六个随机占位与 Petrify 也进表**。它们不可施放，但表要能回答任意 id（调试窗口、坏存档），参考同样有句子。
6. **顺手改三处过时注释**：`SpellTable`/`Spell` 的「文案属于翻译层」，`SpellInfo` 对 `getSpellDescription` 的
   描述（它不只是拼句子，还要 `HeroBase`、最近的城堡与地图，行号也从 214-321 更正为 214-427）。

**后果**：新增 `SpellText.cs`（73 行表 + 两个方法，约 140 行），`Spell` 增 `GetName`/`GetDescription`，
测试新建 `SpellTextTests` 17 条。Game 2697 → **2714**，全解决方案 3 507 → **3 524** 全绿，`-warnaserror` 0 警告。
`spell` 目录统计不变（`spell.cpp` 仍因流序列化留在 ◐）。

**遗留**：`Spell::GetDescription()` 之外的 `getSpellDescription`（`spell_info.cpp:214-427`，要法术书屏）；
`spell.cpp` 的流序列化；`spell_book.cpp` 的绘制与输入循环。

## ADR-173：怪物信息屏的三支文案进 `MonsterDescriptions` ◐

**背景**：`monster_info.cpp` 的缺口一直被记成「四个描述函数（要 `Spell.GetName()` 与 `_()`）」。ADR-172 补上
`Spell.GetName()` 之后去核对，发现记错了：本版参考里面向玩家的只有三支，第四支 `getMonsterDescription` 是
带 `Name: / Base growth: / Cost:` 英文标签的调试转储，唯一调用者是开发者的工具而不是游戏。全游戏的调用点只有
一处——`dialog_armyinfo.cpp:431` 调 `getMonsterPropertiesDescription( troop.GetID() )`。

**决定**：

1. **三支函数一个文件**：`MonsterDescriptions` 装 `GetAbilityDescription`、`GetWeaknessDescription`、
   `GetPropertiesDescription`，加上它们共用的 `ReplaceMassSpells`/`ListSpells`。分到 `MonsterAbilities` 与
   `MonsterWeaknesses` 里会让「信息屏的一句话」这个概念被两处代码拼出来，而它其实是一个流程。
2. **`%{spell}` 就地替换，`%{count}` 不替换**。法术描述是模板（数字由战斗侧给），能力句里的法术名当场从
   `SpellText` 取，`"50% immunity to Fireball spell"` 是成品句子。数字同理：`"25% magic resistance"`。
3. **句尾不带句号**。`GetPropertiesDescription` 自己补 `.`，两支单句函数不补——照抄参考，否则分组行会有两个点。
4. **`MonsterAbilities.IsBasic` 与 `MonsterWeaknesses.IsBasic` 对称**：两格、飞行、龙、四种元素生物这八项
   信息屏一律不显示（玩家在战场上看得见）。`None` 也算隐藏项，因为忽略时它返回空串而不是 `"None"`。
5. **`Elemental` 是改写项不是隐藏项**：隐藏时返回 `"No Morale"`。这是参考的原文，也是玩家真正需要知道的那条。
6. **分组用有序容器**：参考是 `std::map<uint32_t, vector<int>>` + `std::set<int>`，端口用 `SortedDictionary`
   与 `SortedSet`，于是土元素的三条免疫合成 `"Immune to Lightning, Elemental Storm."`——连锁闪电因为
   闪电箭在场被 `replaceMassSpells` 去掉，剩下按 id 排序。闪电箭印作 `"Lightning"`，全表唯一的改名。
7. **额外伤害印总数**：表里存 100（+100%），句子是 `"200% damage from ..."`。
8. **调试转储明确不转**：`getMonsterDescription` 的标签是英文常量而非 gettext 串，转它只会骗过「文案已齐」
   的统计；端口用测试代替。

**后果**：新增 `MonsterDescriptions.cs`（约 300 行含注释），`MonsterAbilities` 增 `IsBasic`（约 20 行），
新建 `MonsterDescriptionsTests` 52 条。Game 2714 → **2766**，全解决方案 3 524 → **3 576** 全绿，
`-warnaserror` 0 警告。`monster` 目录统计不变（`monster_info.cpp` 里还剩只被战斗侧读的 `icnId`/`sounds`）。

**遗留**：`monster_info.cpp` 的 `icnId`/`sounds` 两张表（战斗批次；`icnId` 已在 ADR-174 转出）；`dialog_armyinfo.cpp`
的排版与绘制（已在 ADR-174 转出）；`spell_info.cpp` 的 `getSpellDescription`。

## ADR-174：军队信息窗口按「布局 / 美术 / 模型」三件拆开 ◐

**背景**：`dialog_armyinfo.cpp` 里有四种东西混在一个函数里——窗口与阴影的矩形算术、`ICN` 帧的选取、属性与描述
的文案、以及一个阻塞的输入循环。上一批（ADR-173）把文案备好之后，剩下的是排版。直接照抄成一个大
`ArmyInfoDialog.Draw()` 会把「数字」和「像素」焊死：没有显示器的测试只能整块跑一遍，偏移错一个像素也说不清
是哪一步。

**决定**：

1. **`ArmyInfoLayout` 只算矩形**。窗口、阴影、`pos_rt` 三个矩形，加上属性块、名字带、描述块、数量框与三个
   按钮的矩形，全部来自参考匿名 namespace 的常量。`Create( screen, ... )` 是纯函数，测试拿 640×480 一串
   数字就能核对 60/112/460/149 这些值，不需要 `PixelBuffer`。
2. **阴影锚点当参数传**，不放常量。参考的阴影帧存在 `(2,2)` 一类的锚点上，`(0,0)` 是猜；`ArmyInfoArtwork`
   用 `GetShadowShift()` 从两张精灵里读出来，布局收下。
3. **`ArmyInfoArtwork` 记 `EvilInterface`**。邪恶界面不只是换 `ICN::VIEWARME`：窗口还下移 3 像素、`pos_rt`
   右移 9，退出按钮少了 18 像素的余量。把这个布尔留在美术对象上，布局才不用再问一次调用方。
4. **`Draw` 不画按钮**。参考的按钮是可悬停的 `Button`，被主循环反复重画；端口沿用 `ButtonSprite`，由
   `CreateButtons` 返回三个（顺序 upgrade/dismiss/exit，与 `FromButton` 的索引一致），调用方负责绘制与命中。
5. **描述块返回 `(FirstRow, Lines)` 而不是补空行的列表**。参考从 `3 - 行数` 起画，行数超过 3 时是负数，文字
   向上长进名字带；用一个可为负的起始行表达这件事，比补前置空行更难写错，也把「截断」这个选项排除在外。
6. **属性块画「标签 / 冒号 / 值」三段**。参考把标签右对齐到 `dst.x - 3`、冒号画在那里、值从 `dst.x + 6` 起。
   标签超宽时参考会缩放到一行（`fitToOneRow`），端口不做缩放，注释说明这一处差异。
7. **伤害与生命值取单只**。`Monster( troop.GetMonster() )` 而不是 `troop.GetDamageMin()`——后者乘了数量。
   这条容易写错，测试里专门断言弓箭手 10 只时伤害仍是 `2-3`、生命值仍是 `10`。
8. **`icnId` 表写成 switch**。前十一只的升级与本体不一一对应（游侠读 `ARCHER2`、十字军读 `PALADIN2`、
   冠军骑士读 `CAVALRYB`——那是颜色不是升级），算术只会偶尔对。
9. **`Speed`/`Luck`/`Morale` 的 `String` 只转英文原文**，与 ADR-172 对 `Spell.GetName()` 的处理一致；三支
   `Description` 与 `getMonsterDescription` 仍不转。

**后果**：新增 `ArmyInfoLayout.cs`（约 260 行含注释）、`ArmyInfoArtwork.cs`（约 140 行）、`ArmyInfoDialog.cs`
从 41 行扩到约 500 行；`Speed`/`Luck`/`Morale` 各加 `GetName`，`Troop` 加 7 个成员，`MonsterIcnIds` 加
66 行 switch。新建 `ArmyInfoDialogTests` 14 条，`TroopTests` +3，`MonsterIcnIdTests` +3。Game 2766 → **2788**，
UI 195 → **209**，全解决方案 3 576 → **3 613** 全绿，`-warnaserror` 0 警告。

**遗留**：`dialog_armyinfo.cpp` 的输入循环与事件（要 `LocalEvent` 与 `ButtonGroup` 的主循环）；单位精灵的动画帧
（`monster_anim`，随战斗批次）；战斗型的 `Shots Left`/`Hit Points Left` 两行（`Troop` 还没有战斗状态，要
`BattleTroop`）；`monster_info.cpp` 只剩 `sounds` 表。

## ADR-175：法师塔窗口拆四件，补两个绘制件 ◐

**背景**：魔法塔的规则（五行数据、两句状态栏）在 ADR-167 已转，留在 `castle_mageguild.cpp` 的是窗口本身：
`STONEBAK` 底图、`MAGEGLD*` 公会建筑、五行卷轴（`ui_mage_guild.cpp` 的 `SpellsInOneRow`）、`SMALLBAR` 底栏的
两段拷贝与三个按钮。和军队信息窗口一样，把矩形算术、`ICN` 帧号、像素绘制、按钮顺序混在一个 `Draw()` 里，测试
就只能整块跑；而这里的像素逻辑又比军队窗口更绕——建筑的横向居中要读「已绘像素」的包围盒，纵向却读整张精灵的
高度。

**决定**：

1. **四件拆开**：`MageGuildLayout` 只算矩形（窗口、底栏、三段分栏、状态栏 ROI）；`MageGuildArtwork` 只管帧号与
   两个派生（`GetGuildBuildingIcn`、`GetSpellIcon`）；`SpellScrollRow` 是一行的控件（槽位、命中、当前法术、
   自绘）；`MageGuildDialog` 收模型、拼绘制、发按钮、吐状态行。行控件独立成件是因为它的几何（110 步长、折叠
   下移 7、图标内缩 3、名字左下角 18/57）在参考里也是独立的类，而且五行的差别只有数据。
2. **补 `ImageOps.GetActiveRoi`，语义按参考的 transform 判定**：`value == 0 || value >= minTransformValue`（默认
   6）算已绘。不读图像层颜色——「transparent」是 transform 层的事，读颜色会把合法的调色板 0 当成空白。单层
   精灵返回空矩形，与参考一致（调用方 `FitToRoi` 也不受影响）。一处小差异：参考在「整张都跳过」时
   `area.height` 会停在 `-1`，端口以 `bottom = top` 兜底成高度 1 的退化框，调用方拿到的仍是 `IsEmpty`。
3. **补 `ImageOps.BlitSpriteClipped`，因为原有重载的语义是另一件事**：`BlitSprite( dest, offset, sprite, sourceArea )`
   把**精灵原点**放在 `offset`，源区随之平移；底栏与建筑要的是「源区左上角落到目标点」。用前者会整体偏一列
   （底栏，源从第 1 列起）或偏半个源区宽（建筑）。新重载收「目标点 + 源区 + 裁剪区」，内部把裁剪折算回源区，
   再交给旧重载完成逐像素拷贝。先写错再补，注释里点了名，免得后来的人以为是冗余 API。
4. **建筑的纵向基线用整张精灵**：`y = dialog.y + 290 - sprite.height`，横向才是
   `dialog.x + 100 - area.x - area.width / 2`。这一条最容易顺手写错（两处都用 `area`），测试专门用「只有顶部
   10 行有内容、精灵 40 行高」的假精灵把它钉住。
5. **建筑裁剪到左侧 200 列**：高层公会的图比底板高，不裁会盖住法术行。裁剪走 `BlitSpriteClipped` 的 `clip`，
   而不是先算可见矩形再选源区——后者要维护三套坐标，正是第 3 条里出错的地方。
6. **随机种族在画的时候重上色**：`MapRace.Random` 取 `MAGEGLDZ`（与 Wizard 同图），再用 `PaletteId.Purple` 过一遍；
   先 `Clone()` 再改，调用方传进来的帧不被改脏。
7. **单城时箭头真的禁用**：不只把 released 图当禁用图，还调 `ButtonSprite.Disable()`，于是 `Press()` 拒绝、
   `GetStatusText` 回默认句，与参考的 `button.disable()` 一致。
8. **底栏两段各带自己的源列**：左段源列 `1`（第 0 列是黑的），右段源列 `宽度 - 右段宽`（保住圆角）；奇数宽度
   由 `left = width / 2` 决定，余数落给右段。
9. **不转的部分写明白**：消息循环与 `LocalEvent`、`SpellsInOneRow::queueEventProcessing` 的法术说明对话框、
   `redrawCurrentSpell` 的增量重绘（端口整行重画，省掉「记住上一格」的状态）。

**后果**：新增 `UI/Widgets/SpellScrollRow.cs`（约 200 行含注释）、`UI/Dialogs/MageGuildArtwork.cs`（约 130 行）、
`UI/Dialogs/MageGuildLayout.cs`（约 120 行），`MageGuildDialog` 从 110 行扩到约 460 行；`ImageOps` 增
`GetActiveRoi`（约 100 行）与 `BlitSpriteClipped`（约 45 行）。新建 `ImageOpsActiveRoiTests` 5 条、
`ImageOpsBlitClippedTests` 4 条，`MageGuildDialogTests` 从 4 条扩到 29 条。Core 200 → **209**，UI 209 → **234**，
全解决方案 3 613 → **3 647** 全绿，`-warnaserror` 0 警告。`castle` 目录统计不变（改动落在已是 ◐ 的文件里）。

**遗留**：`castle_mageguild.cpp` 的事件循环（要 `LocalEvent` 与 `ButtonGroup`）；法术说明对话框（要
`spell_info.cpp` 的 `getSpellDescription`）；`castle_well.cpp` 的绘制循环与 MAX 按钮的重画。

## ADR-176：水井窗口分数据/布局/美术/绘制，保留参考的两处不一致 ◐

**背景**：`castle_well.cpp` 是 `castle` 目录里最后一份「规则已转、窗口未转」的文件：`CastleMaxRecruitment`
（MAX 按钮的分配算法）在 ADR-167 落地，窗口本身没有。它有六格住宅、每格九行文字、底栏两段拼接，还有一处
只有原版才有的手工填黑列。上一批补的 `ImageOps.BlitSpriteClipped` 正好是它需要的（源区左上角落点 + 裁剪）。

**决定**：

1. **四件拆开**：`CastleWellDwelling` + `Castle.GetWellDwellings()` 只回答数据（建没建、库存多少、周产多少、
   生物是谁）；`WellLayout` 只算矩形与锚点；`WellArtwork` 只管帧号与归档；`WellDialog` 拼文字、发按钮、判命中。
   与魔法塔同一套切法，好处是六格几何（七组常量）能被单测逐条钉住。
2. **「格坐标 = 格原点 + 常量」显式化**。参考把 314/150 的步进和七组 `+ (21,35)`、`+ 86`、`+ 122`、`+ 269`
   散在 60 行里，端口收成 `GetCellOrigin/GetPictureOrigin/GetDwellingNameAnchor/…`，越界级别抛
   `ArgumentOutOfRangeException`。
3. **保留「同一行问两次住宅」的不一致**。生物、名字、图画取 `GetActualDwelling()`（建了就升级），
   Growth 两行却用**基础**住宅位判定是否显示。只买了升级位的存档因此会显示升级后的生物、却没有成长行。
   参考如此，端口照抄并在 `CastleWellDwelling` 的备注里写明两处取值的来源。
4. **成长量另立 `Castle.GetWellGrowth()`**，与 `DwellingsBar.GetGrowth()` 同一套算式（井 +2 全员、魔法泉 +8 只给
   一级），注释里互相指认。没有合并二者，因为 `DwellingsBar` 按 item 解析、这里按基础住宅位解析，合并需要多一层
   映射，收益不抵风险。
5. **「跳过一行」用空字符串**。`DescribeStats` 返回 8 条（未建）或 10 条（已建），空串占一行高度，调用方只按
   `SmallWhite.LineHeight` 往下挪。这样「缝在哪」不进绘制代码，测试也能直接比较字符串列表。
6. **三个字体面收成 `WellFonts` 记录**。参考用 `FontType` 组合（smallWhite / normalWhite / normalYellow）；
   端口一个 `IGlyphSource` 带一种调色板，于是用一个三元记录传参，而不是给 `Draw` 加三个参数。
7. **美术表要求完整的 12 帧**（19–30），缺一帧 `TryCreate` 返回 `null`；未加载的按钮帧是 `null` 也算缺。
   这里踩过一次：`Sprite` 是引用类型而 `DialogButtonArtwork` 是值类型，`default(DialogButtonArtwork)` 的
   `Released` 是 `null`，判空必须写成 `sprite is null || sprite.Image.IsEmpty`。
8. **底栏两段 + 手工填黑列**：左段源列 1（第 0 列全黑）、右段源列 `宽 - 320 + 退出宽 - 1`（保圆角），
   最右一列（x = 639）填 `0`；底栏到第 476 行，477–479 三行参考也不画，端口照旧留空。
9. **不转的部分写明白**：`_wellRedrawMonsterAnimation`（生物动画，要战斗动画档案）、`_openWell` 的消息循环、
   招募动作本身（交给 `CastleMaxRecruitment` 与住宅招募对话框）；标题与 `Available:` 的裁剪/`fitToOneRow`
   因 `GlyphText` 无裁剪而不做，英文串装得下。

**后果**：新增 `Game/Castle/Castle.WellView.cs`（约 150 行）、`UI/Dialogs/WellLayout.cs`（约 340 行）、
`UI/Dialogs/WellArtwork.cs`（约 190 行）、`UI/Dialogs/WellDialog.cs`（约 380 行）。新建 `CastleWellViewTests` 6 条、
`WellDialogTests` 38 条。Game 2788 → **2794**，UI 234 → **272**，全解决方案 3 647 → **3 691** 全绿，
`-warnaserror` 0 警告。

**遗留**：生物动画（`_wellRedrawMonsterAnimation`）；窗口消息循环与 `LocalEvent`；`castle_well.cpp` 的招募调用链；
`castle` 目录至此六个文件全部 ◐ 或 ✔。

## ADR-177：`LocalEvent` 只转状态机，按钮组用 `ProcessEvents` 接上 ◐

**背景**：ADR-174/175/176 三个窗口（军队信息、魔法塔、水井）都把绘制做完了，却都留了同一句「事件循环未转」。
`engine/localevent.cpp` 是它们的共同前置：它把 SDL 事件变成「这一帧点了哪个按钮、按了哪个键、滚了多少」。
但这份文件 77 KB，一半篇幅是 `HandleEvents`（SDK 轮询、`EventEngine`、光标绘制、音频混音）、手柄映射与双指
触摸手势 —— 那些都要 SDL 设备与渲染循环，与端口「场景推事件、`PixelBuffer` 出图」的分层不合。

**决定**：

1. **只转状态机**。`LocalEvent` 的八个动作状态位、三键的按下/松开位置、滚轮增量、长按、拖拽标志、三个钩子与
   全部查询都照抄；`HandleEvents` 本体不转，改为 `BeginFrame()` + 四个 `On…` 推送方法（`OnMouseMotion`、
   `OnMouseButton`、`OnKeyboard`、`OnMouseWheel`）。参考里这些 `on…Event` 是私有成员，端口只能把它们提成
   public，注释逐个指回 `localevent.cpp` 的行号。
2. **`BeginFrame` 对应 `HandleEvents` 开头，而不是 `reset()`**。前者清的是四个一次性状态 + 触摸标志，保留「按住
   的按钮」与「按住的键」；后者清空一切。两个方法都留着：`Reset()` 是参考 `localevent.h:201` 的语义，
   `ButtonGroup.ProcessEvents` 在热键命中后调它，行为与参考的 `le.reset()` 一致。
3. **`Key` 枚举逐值照抄**。C# 侧只去掉 `KEY_` 前缀（`KEY_KP_0` → `KeyPad0`，`KEY_0` → `Digit0`），顺序与值不变，
   `LastKey` 收尾兼作成员计数（测试钉住）。热键表按值索引，重排会静默错位，所以这条写进注释。
4. **`KeyModifier` 做成 `[Flags]`**。参考是裸 `int32_t` 常量位；端口给枚举是为了让 `getCharacterFromPressedKey`
   的签名说得清楚，值仍是 1/2/4/8/16。
5. **`KeyText` 提成公开静态类**。参考的 `getCharacterFromPressedKey`/`InsertKeySym` 在 `localevent.cpp` 的匿名
   命名空间里，唯一调用方是 `InsertKeySym`；端口公开它们是因为二者是「键 + 修饰位 → 字符」的纯函数，不碰窗口
   就能测。字母的 26 个 case 折成 `>= Key.A and <= Key.Z` 一段（依赖枚举连续性，注释写明）。
6. **`InsertKeySym` 钳制光标位置**。参考把 `position` 当 `size_t` 并信任调用方，`pos > size` 时 `insert` 会抛。
   端口钳到 `[0, Length]`：退化位置只影响这一次按键，不会从文本框里抛异常出来。参考「退格时 `pos >= size`
   就缩短字符串」的分支原样保留。
7. **`ButtonGroup.ProcessEvents( LocalEvent, ButtonHotKey? )` 是第三套入口**（前两套是 `SetCursor`/`SetLeftButton`
   与 `ProcessHotKey`）。它照抄 `ui_button.cpp:598-641` 的三段：先按 `isMouseLeftButtonPressedAndHeldInArea`
   更新按下外观，再按 `MouseClickLeft( area )` 返回按钮值，最后把热键交给 `ProcessHotKey`。热键命中后调
   `events.Reset()`，与参考 `le.reset()` 同理（同一个键不该顺手关掉下一个窗口）。
8. **`hotKey` 用可空表示「这一帧没有热键」**。`ButtonHotKey` 的零值是 `Okay`，不能拿它当「没有」；参考在这里调
   `Game::HotKeyPressEvent( DEFAULT_OKAY )`，在没有按键时返回 false。少了这层，只有一个 OK 按钮的对话框会在
   第一帧自己关掉。`ProcessHotKey` 也因此只能在确实有键时调用。
9. **不转的部分写明**：`HandleEvents`（SDL 轮询、`EventEngine`、光标增量绘制、音频）、手柄、双指触摸、
   `KeySymGetName`（`SDL_GetKeyName`）、`getKeyName`（按键英文名表）。`IsMouseEventFromTouchpad` 因此恒为 false，
   状态位与查询都留着，等手势移植后自然接通。

**后果**：新增 `UI/Input/Key.cs`（约 180 行）、`UI/Input/LocalEvent.cs`（约 670 行）、`UI/Input/KeyText.cs`
（约 270 行），`Widgets/ButtonGroup.cs` 增加约 75 行。新建 `LocalEventTests` 23 条、`KeyTextTests` 16 条，
`ButtonGroupTests` 加 5 条。UI 272 → **316**，全解决方案 3 691 → **3 735** 全绿，`-warnaserror` 0 警告。

**遗留**：`HandleEvents` 与 SDL 侧的翻译层（落在 `App`）；手柄与双指手势；`KeySymGetName`/`getKeyName`；
把三个窗口（军队信息、魔法塔、水井）的消息循环接到 `LocalEvent` + `ButtonGroup.ProcessEvents`。

## ADR-178：窗口消息循环折成 `ProcessEvents` 的纯函数 ◐

**背景**：ADR-177 把 `LocalEvent` 的状态机立起来后，魔法塔、水井与军队信息这三个窗口各缺最后一块：
`while ( le.HandleEvents() )` 里那三十行「谁被点了、接下来做什么」。照抄这个循环是做不到的 —— 它夹着 `Display`、
`ImageRestorer`、`StatusBar`、`Dialog::RecruitMonster` 与 `fheroes2::showStandardTextMessage`，全是场景与模态框的
东西；而参考的每一轮又必须按固定顺序问一遍那些查询，顺序本身带语义（点击会被第一个问出 `true` 的分支吃掉）。

**决定**：

1. **一帧一次，返回「要做什么」**。`WellDialog.ProcessEvents( LocalEvent, WellLayout, 两个按钮, WellHotKeys )`
   返回 `WellDialogFrame`，`MageGuildDialog.ProcessEvents( LocalEvent, MageGuildLayout, 三个按钮, 五行, MageGuildHotKeys )`
   返回 `MageGuildFrame`，`ArmyInfoDialog.ProcessEvents( LocalEvent, 退出按钮, 升级按钮?, 解散按钮?, ArmyInfoHotKeys )`
   返回 `ArmyInfoFrame`。函数不绘制、不弹窗、不动城堡；参考里的 `break`/`return` 变成同一个返回。
2. **顺序与短路照抄**。水井：EXIT → MAX → 六格（每格先点击后右击，命中即 `break`）→ 两个 tooltip；魔法塔：
   EXIT → 上一城 → 下一城 → 五行的 `queueEventProcessing` → tooltip 的 else-if 链；军队信息：升级 → 解散 → 退出 →
   三个 tooltip 的 else-if 链。参考的 `||` 链在第一个命中的行就停，所以后面的行保留旧的 `CurrentIndex`，端口用
   `break` 复现；点击被消费这一点也有用例钉住（升级吃掉的点击不会再被退出读到）。
3. **外包的四件事**。购买计划（`CastleMaxRecruitment`）、招募对话框、法术说明框（`SpellDialogElement`）、军队信息
   的三个消息框留在调用方；帧只带数据：`WellDialogFrame.DwellingLevel`、`MageGuildFrame.Spell` 与
   `SpellByRightClick`、`ArmyInfoAction` 与 `ArmyInfoButton`。右击法术在参考里是 `Dialog::ZERO`（没有按钮），左击是
   `Dialog::OK`，这一位就是那个区别；军队信息的三句文案（升级提问、钱不够、解散提问）作为常量留在
   `ArmyInfoDialog` 上，免得到处抄。
4. **热键用参数传入**。端口没有 `Game::HotKeyPressEvent` 的热键表，所以
   `WellHotKeys( CloseWindow, BuyAll, Dwelling )`、`MageGuildHotKeys( CloseWindow, PreviousTown, NextTown )` 与
   `ArmyInfoHotKeys( Upgrade, Dismiss, CloseWindow )` 由场景解码；`Dwelling` 是 1..6 的等级而不是建筑位。
5. **`MageGuildDialogAction` 补两个值并让 `None` 取零**。新增 `None` 与 `ShowSpell`，并把 `None` 放在最前 ——
   枚举值不落盘，所以顺序不是契约；而 `default(MageGuildFrame)` 必须是「什么也没发生」，不能是「退出」。
   `ArmyInfoAction` 同样以 `None` 打头。两个 `FromButton` 仍然只回答按钮值，为自行驱动按钮的调用方保留。
6. **可为空的按钮用可空参数**。军队信息的升级与解散按钮在参考里是 `std::unique_ptr<Button>`，由 flag 与贴图共同
   决定有没有；端口把 `ArmyInfoDialog.ProcessEvents` 的这两个参数做成 `ButtonSprite?`，`null` 时既不亮也不响应，
   正好复现 `if ( buttonUpgrade )` 的三处守卫。退出按钮永远存在，所以是必填参数。
7. **`ButtonBase.UpdatePressState( LocalEvent )`**。四处 `drawOnState( le.isMouseLeftButtonPressedAndHeldInArea(
   area() ) )` 的实参逻辑提成按钮自己的方法（按住且在区内→按下，否则松开，禁用时不动），按钮组、水井、魔法塔、
   军队信息共用；它只改状态不绘制，因为水井与军队信息的按钮要等各自的动画计时器到了才重画
   （`castle_well.cpp:289-296`、`dialog_armyinfo.cpp:696-712`）。
8. **不转的部分写明**：`StatusBar` 的滚动文字、光标形状（`CursorRestorer`）、`ImageRestorer`、
   `DrawBattleStats()` 的两块战斗属性与它们的法术 tooltip、无按钮时「等右键松开」的读法。
9. **按住箭头连发另行补上（`Widgets/TimedEventValidator.cs`）**。参考的 `fheroes2::TimedEventValidator`
   （`gui/ui_tool.h:227-259`）用 `ActionObject::subscribe` 让按钮的每次状态变化回调 `senderUpdate()` 重置两个
   `TimeDelay`，`isDelayPassed()` 要求「首按延迟已过 + 间隔延迟已过 + 按钮按住」。端口没有观察者（`ButtonBase`
   的文档早写明这一点），于是把「这一帧是否按住」作为参数传给 `IsDelayPassed( bool isHeld )`，由计时器自己比较
   上一帧的状态来复现通知：`press()` 与 `release()` 都会通知，所以「按下」和「松开」两帧都重置两个计时器。首按
   500 ms、之后每 100 ms，触发时只重置间隔那一只 —— 与参考逐字一致。魔法塔把它接在参考的第三项上
   （`ProcessEvents( ..., previousTownRepeat, nextTownRepeat )`），为 `null` 时只答点击与热键。

**后果**：`WellDialog.cs` 增加约 110 行，`MageGuildDialog.cs` 增加约 180 行，`ArmyInfoDialog.cs` 增加约 190 行，
`TimedEventValidator.cs` 新增约 110 行，`ButtonBase.cs` 增加约 30 行、`ButtonGroup.cs` 减少 12 行。
`WellDialogTests` 加 12 条、`MageGuildDialogTests` 加 12 条、`ArmyInfoDialogTests` 加 12 条、
`TimedEventValidatorTests` 新增 6 条。UI 316 → **358**，全解决方案 3 735 → **3 777** 全绿，`-warnaserror` 0 警告。

**遗留**：`_wellRedrawMonsterAnimation`；法术说明框；军队信息的 `DrawBattleStats()`；把三个窗口接进 `App` 的场景
循环。`TimedEventValidator` 另外五个用点（`castle_town.cpp`、`castle_dialog.cpp`、`heroes_dialog.cpp`、
`dialog_marketplace.cpp`、`dialog_recruit.cpp`）要等对应窗口移植时再接。

## ADR-179：冒险模式的回合骨架折成 `AdventureSession` ◐

**背景**：到 ADR-178 为止冒险地图已经能画、能滚、能开图（ADR-131、ADR-169、ADR-170），但它仍然只是个查看器：
没有玩家、没有日期、英雄不会走。缺的正是 `Interface::AdventureMap::StartGame()` 的那个 `while`
（`game_startgame.cpp:732-790`）、`humanTurn()` 开头的两句（`:1039-1043`）以及 `game.cpp` 里每帧挪一下英雄的那几行。
参考把这三样与界面挤在同一个类上 —— 它的事件循环就是界面循环 —— 端口照抄不了：`FHeroes2.Game` 不认识 SDL，
也不该认识画布。

**决定**：

1. **循环独立成类**。`Game/AdventureSession.cs` 只管「谁在行动、什么时候换人、什么时候换天」：
   `Start` 开一局并把世界推进到第 1 天，`EndTurn` 换人或换天，`NextHero`/`SetFocus` 管焦点，
   `SetPath` 是 `MouseLeftClick()` 里 `SetPath( world.getPath( ... ) )` 加 `SetMove( true )` 那一对。
   参考里这些是 `Interface::AdventureMap` 的成员，端口把它们从界面里拆出来，场景只剩「问会话 + 画」。
2. **一天的结构照抄**。新的一天发生在所有玩家都行动完之后（`StartGame()` 循环顶部的 `world.NewDay()`），
   `EndTurn` 只换人；`_acted[]` 记住这一圈谁已经动过，转一圈才清空并推进日期。
3. **电脑玩家被跳过**。`AI::Planner` 在端口只有契约（ADR-161），电脑回合的全部内容都在
   `AI::Planner::KingdomTurn()` 里，没有它就没有可执行的动作。跳过至少让日期与收入继续走、人类玩家的城照常可访问；
   等 AI 落地时把 `EndTurn` 里的那个 `continue` 换成一次 `KingdomTurn()` 就回到了参考的形状。
4. **回合开始的两件事按参考顺序**：先 `Kingdom.ActionNewDayResourceUpdate`（收入 + 当天事件），再
   `Kingdom.ActionBeforeTurn`（人类英雄 `calculatePath()`，AI 英雄清路线）。`Hero.CalculatePath` 是那次
   重算的实现：取旧目的地 → 两次 `IsValidForMovement` 校验（旧路线在中转格重算后就可能失效）→ `SetPath`。
   焦点在这里跟着回合走，落在当前王国的第一个英雄上（参考的 `_statusPanel.SetHero`）。
5. **玩家槽位在「开一局」时建，不在「读一张图」时建**。`Settings.Players` 是进程级的静态表
   （`Hero.GetControl()` 问它，以后的 AI 也会问），而地图文件头说的是「谁可以让人类玩、谁可以让电脑玩」，
   两张表经常重叠，直接拿 `ComputerPlayers` 当 AI 会把整张图判成电脑。参考的答案在
   `Players::Init( fi )` + `SetStartGame()`（`players.cpp:299-352`、`:487-508`）：热座时只留 human-only 色，
   否则把第一个「可以让人类玩」的色提升成唯一的人类。于是有两条规则：
   `World.Kingdoms.cs` 的 `ComputerColorsOf` 在装载地图时按同一规则算出王国的 AI 色集合（**不写全局表**：
   两个世界同时装载就会互相覆盖，测试正是这样）；`AdventureSession.Start` 在真正开局时
   `Players.Init( info )` + `SetStartGame()` 填好全局表。`Start` 因此多收一个 `Mp2FileInfo`。
   `Players.Init` 里原本问 `SharedColors` 的地方改成问 `HumanPlayers`（`players.cpp:330` 问的是
   `colorsAvailableForHumans`）：只允许人类的色同样该参与那唯一一次提升，否则单机图没有人能行动。
6. **英雄得有部队**。`Hero.LoadFromMP2` 现在补 `Army.Reset( true )`（`heroes.cpp:453`），`Army.Color`/`Army.Race`
   跟着英雄走（`SetColor`、换种族）。理由很实际：`GetMaxMovePoints()` 由部队里最慢的兵决定，空部队的移动力是 0，
   从地图上读出来的英雄一步也走不了。主技能、初始法术、二级技能、自定义部队仍然没读，文档里写明理由。
7. **画面这半**：`AdventureMapView` 增加世界英雄的绘制（身体与影子），帧取 `Hero.SpriteIndex`
   （`direction * 9 + frame`），位置叠加 `Hero.Offset`（走到两格中间的那半步），雾下不画；
   `IHeroSpriteSource.GetBody( race, frame )` 用默认接口方法实现，测试替身不必跟着改。
   一旦世界里已经有英雄就只画世界这一批，否则静态英雄表会在同一个格子上画出第二个人。
8. **场景这半**：`AdventureMapScene` 持有会话，开局时把世界传给视图并以焦点英雄居中；点地形 = 焦点英雄铺路，
   按钮面板改由 `CurrentButtonStatus()`（`_setButtonStatus` 的四问）决定，状态栏写日期与当前玩家，
   `EndTurn`/`NextHero` 收口在场景上；`GameHost` 绑 `TAB`/`H`（下一个英雄）与 `E`（结束回合）。
   没有人类玩家的地图仍然退回查看器，行为与 ADR-169 时一致。
9. **不转的部分写明**：`Interface::AdventureMap` 的其余分支（城堡与英雄对话框、系统菜单、`ShowControlPanel`）、
   `AI::Planner`、`Game::LoadGame`/`AutoSave`、过场动画、`StatusPanel` 的旗帜与地图名贴图、`game_hotkeys.cpp`
   的热键表（端口直接绑键）。

**后果**：`AdventureSession.cs` 新增约 345 行；`Hero.Movement.cs` 约 25 行（`CalculatePath`）、`Kingdom.cs` 约 30 行
（`ActionBeforeTurn`）、`Hero.cs` 约 20 行（默认部队与部队的色/种族）、`World.Kingdoms.cs` 约 45 行
（`ComputerColorsOf`）、`Players.cs` 一处判定；`AdventureMapView.cs` 约 100 行（世界英雄与影子）、
`HeroSpriteSet.cs` 约 10 行；`AdventureMapScene.cs` 约 190 行、`GameHost.cs` 约 10 行。
新建 `AdventureSessionTests` 6 条、`WorldHeroLayerTests` 2 条。全解决方案 3 777 → **3 785** 全绿，`-warnaserror` 0 警告。

**遗留**：`AI::Planner` 的电脑回合、存读档（`game_io`/`serialize`）、当天事件的对话框、状态栏的国旗与地图名贴图、
`game_hotkeys.cpp` 的热键表、`Game::AutoSave` 与过场动画。`Settings` 与 `Players` 都是进程级静态表，
因此按 `Settings` 的用例被收进同一条 xunit collection 串行执行。

## ADR-180：AI 层先把形状定死，实现留到最后 ◐

**背景**：ADR-161 给 `FHeroes2.Game.Ai` 立的是「别人要用的那一半」：`KingdomTurn`、`BattleTurn`、`HeroesAction`
这些入口都在，正文一律 `throw`，私有半边特意没声明，理由是三十来个私有方法的参数形状依赖还没落地的对象价值表。
先补其它子系统、AI 压到最后，这个划分就露了短板：真到写 `ai_planner_hero.cpp` 那天，要先回头补十几个私有声明，
而那时的形状只能靠当下的记忆去猜。参考的头文件早把形状写清楚了（`ai_planner.h:200-281`、`ai_battle.h:97-165`），
不如现在照抄，正文留空。

**决定**：

1. **私有半边照抄声明**。`AiPlanner` 的 `CastleTurn`、`HeroesTurn`、`GetPriorityTarget`、`EvaluateRegionSafety`、
   `FindCastlesInDanger`…… 26 个成员照 `ai_planner.h:200-281` 的顺序与参数声明；`BattlePlanner` 的
   `PlanUnitTurn`、`SelectBestSpell`、`SpellDamageValue`、`GetSpellSlowRatio`…… 23 个照 `ai_battle.h:97-165`。
   正文仍是 `NotImplementedException`，消息里带将来要移植的 `.cpp` 与 ADR 编号。
2. **状态字段一起补**：`_mapActionObjects`、`_priorityTargets`、`_enemyArmies`、`_tileArmyStrengthValues`、
   `_regions`、`_budget`（顺序照 `ai_planner.h:265-281`）。参考里这六个是整场游戏有效的缓存，正是「回合开始时清掉」
   那一段操作的对象。
3. **只有四处带代码**，理由统一：声明出来的字段必须有读点，否则 `-warnaserror` 下 CS0169/CS0414 会直接打断构建，
   而这四处的正文本来就是最终形态 —— `IsPriorityTask`/`IsCriticalTask`（header 里就是内联的两三行）、
   `BattlePlanner.BattleBegins()`（四个计数器归零，`ai_battle.cpp`）、`AiPlanner.ResetTurnCaches()`
   （`ai_planner_kingdom.cpp:652-659` 的清理块，收成一个方法）、`UpdateKingdomBudget()` 开头的预算清零
   （`ai_planner_castle.cpp:270-275`）。
4. **一次战斗内有效的分析数据不声明**：`_commander`、`_myArmyStrength`、`_enemyArmyStrength`、`_defensiveTactics`
   这些唯一读者是还在 `throw` 的规划方法，声明出来就是死字段，留到 `AnalyzeBattleState` 落地时一起加。
5. **`AIWorldPathfinderStateRestorer` 直接实现完**。ADR-161 时它还落不了地（要 `AIWorldPathfinder`），ADR-091 之后
   世界寻路器已经完整（`Reset()`、两个设置器的读写都在），这个守卫的全部内容就是「存两个数、放回去」，没有可留
   的决策；`Dispose()` 幂等，参考用析构函数天然只跑一次。
6. **`BattlePlanner.TurnLimitCounters`（internal 只读元组）只为测试存在**：`IsLimitOfTurnsExceeded()` 落地前那四个
   计数器没有任何读者，而 `battleBegins()` 已经把 50 这个数写进来了，值得钉住。测试项目本来就有
   `InternalsVisibleTo`。

**后果**：`AiPlanner.cs` 260 → **454** 行、`AiBattlePlanner.cs` 105 → **369** 行、新增
`AIWorldPathfinderStateRestorer.cs` 54 行；测试 +2（`ABattleStartsWithAFreshTurnLimit`、
`TheStateRestorerPutsBothTunablesBack`）。Game 2 802 → **2 804**，合计 3 785 → **3 787** 全绿，
`-warnaserror` 0 警告。

**遗留**：`ai_planner_hero.cpp`/`ai_planner_castle.cpp`/`ai_planner_kingdom.cpp`/`ai_battle.cpp`/`ai_battle_spell.cpp`/
`ai_common.cpp`/`ai_hero_action.cpp` 七个文件的正文；战斗内的分析字段；若实现里要就地改 `EnemyArmy` 的字段，再放开
它的设置器。

## ADR-181：存读档的地基是 `Core/Io` 里的流与压缩块 ◐

**背景**：`game_io.cpp` 的存读档全文压在 M8，但它下面有两层不是游戏规则、也没有任何游戏依赖的东西：参考
`engine/serialize.h`+`.cpp`（687 行）的四个流类，与 `engine/zzlib.cpp`（zlib 包装 + 存档块格式）。这两层不落，
后面无论是写 `Players` 的序列化、`Heroes`/`Castle` 的序列化还是 AI 的存读，每一处都要先自己搓一个流出来。
`Core/Io` 目录此前只有 `Crc32`、`LittleEndianReader/Writer` 与 `BinaryFormatException`，正是放它们的地方。

**决定**：

1. **一个抽象基类，不是两个**。参考用虚继承把 `IStreamBase`（读）与 `OStreamBase`（写）拼成 `StreamBase`，只读流
   因此不提供写侧。C# 没有虚继承，照搬只能得到「两个接口 + 一堆转发」。端口改为：`StreamBase` 负责字节序、
   字符串、失败旗和全部读写辅助，只向具体流要两个原语 —— `Get8()` 与 `PutRaw()`，外加 `Size`/`Tell`/`Seek`/
   `Skip`/`GetRaw`。只读的 `RoStreamBuf` 把 `Put8`/`PutRaw` 覆盖成 `NotSupportedException`：写只读流是程序错误，
   参考那里是编译不过，端口里是运行时立刻炸，比静默丢数据好。
2. **命名换成动词**。`stream << x` / `stream >> x` 在 C# 里没有对应写法（`<<` 不能重载成接受 `byte`），所以写侧
   一律 `Write( ... )`（每种类型一个重载，顺序与参考一致），读侧一律 `ReadUInt16()`/`ReadString()` 这种带类型名的。
   `fail()` 是属性 `Fail`，`setFail()` 是方法 `SetFail()`。
3. **短读语义各自照抄**：`StreamBuf`（参考 `RWStreamBuf`）读到不够就补零、不置失败；`StreamFile` 读不够返回空数组
   并置 `Fail`。参考里这两个类本来就分裂，统一成一种反而会让「存档被截断」这件事读不出来。
4. **字符串是「4 字节长度 + 原始字节」，没有零终止**。参考 `operator<<( StreamBase &, const std::string & )` 先写
   `size()` 再写字节；`ReadString()` 读到第一个 `\0` 为止（旧版本写进来的名字可能带填充）。编码用
   `Encoding.Latin1` —— 它把 0~255 一对一映射成字符，正好是「按字节搬运」的原意，不像 UTF-8 那样会因为非法字节
   改变长度。
5. **压缩走 `ZLibStream`，不 P/Invoke zlib**。与 ADR-062（PNG 编码器）同一个选择：框架自带的 zlib 就是 zlib，
   存档里那串字节与 `compress2()` 写出来的是同一种流。压缩级别只映射两档（`Z_BEST_COMPRESSION` →
   `CompressionLevel.SmallestSize`，`Z_DEFAULT_COMPRESSION` → `Optimal`），参考的 `Z_NO_COMPRESSION` 等档位没人用。
6. **块格式照 `zipStreamBuf()` 写死**：`rawSize`(4) + `zipSize`(4) + `formatVersion`(2，只有 0) + 两个未用字节 +
   zlib 流。两个尺寸让读的一侧能一次分配，`UnzipStream()` 因此可以要求「解压出来的长度必须正好等于 rawSize」，
   而不是像参考那样解完再比。
7. **不抛异常**：损坏的存档一律返回空数组 / `false`，并 `GameLog.Error`。调用方是对话框，异常会把要显示给玩家的
   那句话一起带走。
8. **解压有上限**：`unzipData()` 不知道结果多大时，端口按流式读并卡在 512 MB。参考交给 zlib 的 `Z_MAX` 去管，
   而存档是可以被玩家改的，这里宁可多写一行。带 `realSize` 的重载不设上限 —— 尺寸是块头里写的，分配前已经知道。

**后果**：新增 `src/FHeroes2.Core/Io/` 五个文件，共 **932** 行 —— `StreamBase.cs` 281、`StreamBuf.cs` 152、
`RoStreamBuf.cs` 108、`StreamFile.cs` 165、`Compression.cs` 226。测试 +24（`StreamTests` 16 条、`CompressionTests`
8 条），Core 209 → **233**，合计 3 787 → **3 811** 全绿，`-warnaserror` 0 警告。

**遗留**：`game_io.cpp`/`game_loadgame.cpp` 的正文（M8 的主体）；`game_over`/`highscores` 的存档格式；`Players`、
`Heroes`、`Castle` 等类型各自的 `operator<<`/`operator>>` 移植 —— 这次只给它们准备好了「往哪写」。


## ADR-182：army 目录全量移植：小怪物线、两处存档运算符与军队栏 ◐

**背景**：`army/` 的四个 `.cpp` 在此之前是 0 已转 / 2 部分 / 2 未转。`army.cpp` 缺的是两张怪物线与存档运算符，
`army_troop.cpp` 缺 `operator<<`/`operator>>`，`army_bar.cpp` 与 `army_ui_helper.cpp` 完全没有对应实现。最后这一对
是「军队」这件事唯一对玩家可见的一面：城堡里、英雄界面上、编辑器里的五个格子。它们不依赖战斗，也不该等
战斗层，`army` 目录于是可以先收口。

**决定**：

1. **`army_ui_helper.cpp` 只有一个函数，照抄进 `Game/Army/MiniMonsters.cs`**：`drawMiniMonsters()` 的窗口切块、
   居中、精灵高度偏移一像素、以及三处怪癖全部保留 —— 每个 chunk 按「本行剩余栈数」均分；`first == 0 &&
   count == 0`（不指定窗口）时**从右往左**画，于是最后一栈落在最右；盗贼公会只显示第一栈时其余栈画 `???`。
   精灵来源走接口 `IMiniMonsterSprites`，默认实现 `Mons32Sprites` 读 `MONS32.ICN`、帧号 = 怪物 id - 1。
2. **`army.cpp` 的两张怪物线进 `Game/Army/Army.MonsterLines.cs`**：`DrawSingleDetailedMonsterLine()` 与
   `DrawMultipleMonsterLines()`。<3 栈一行居中（下移 `offsetY / 2 + 1`、宽 2/3），4 栈以上前两栈一行、其余第二行
   （第二行两栈时宽 2/3）。这些数字都是参考里的原数，没有重算。
3. **存档运算符进两个 `Persistence` 分片**：`Troop.Persistence.cs` 写 `(int)Monster` + `Count`，读失败清成
   `Unknown`/0；`Army.Persistence.cs` 写槽位数 → 每栈 → 阵型布尔 → 颜色。**颜色宽度按存档版本分叉**：
   `version < Release1_1_09` 读 4 字节整数，之后读 1 字节 —— 这是参考 `army.cpp` 里唯一一处版本判断，也是这一层
   存在的理由。槽位数与常量不符即 `SetFail()` + 清空，不抛异常。读完后 `Commander` 置空：归属者由外面的装配层
   重新挂上，存档里没有指挥官的身份。
4. **`army_bar.cpp` 拆成四个 partial 文件 + 一个几何类**：
   * `ArmyBar.cs` —— 状态、`SetArmy`、`SetBackground`（迷你 43×43 / 详细 43×53）、两个 `Redraw*`、光标框；
   * `ArmyBar.Input.cs` —— 参考里全部 `ActionBar*()` 处理（光标文案、左键单击/双击/拖动/右键）；
   * `ArmyBar.Events.cs` —— `QueueEventProcessing()` 与 `ActionCursorItemIter()` 的分派、`GetItemIndexAt`；
   * `ArmyBar.Redistribute.cs` —— 参考匿名命名空间里那六条重分配规则；
   * `ItemsBarLayout.cs` —— `gui/interface_itemsbar.h` 的表格几何（单格尺寸、表尺寸、格间距、渲染偏移、每行格数、
     `GetItemRect`/`IndexAt`/`RenderArea`）。抽出来的理由是它是纯几何、与「格子里装什么」无关，后面的
     宝物栏、法术栏、城堡建筑栏都要用它。
5. **对话框不做阻塞调用**。参考在重分配里 `Dialog::ArmySplitTroop` 拿一个数、在双击里 `Dialog::ArmyInfo` 拿一个
   动作，端口把两者都变成「问题描述」：`RedistributeArmy()` 返回 `ArmyBarSplit`（来源、目标、同一怪物与否、
   总数、可分的格数、上限、建议值、是否均分），调用方拿去问玩家，再用 `ArmyBar.ApplySplit()` 执行；
   双击返回 `ArmyBarEvent.Info`（`ArmyInfoDialog` 模型），答案交给 `ArmyBar.ApplyInfoAnswer()` 消费
   （`Dismiss` 清空、`Upgrade` 付钱再升级）。
6. **右键的两个分支按参考分**：`MouseClickRight` 走 `ActionBarRightMouseSingleClick`（只做重分配，不做别的）；
   按住的那一帧走 `ActionBarRightMouseHold`（解除整栈 / 打开信息窗）。因此「按下右键立刻提示、松开不算」
   的时序与参考一致，而不是合成一次点击。
7. **世界的依赖外置成委托**。参考里 `world.GetKingdom( color ).AllowPayment()` 与 `OddFundsResource()` 直接读
   世界；端口不把世界塞进控件，改由拥有王国的一侧提供 `CanAffordUpgrade`（决定升级按钮是否灰）与
   `payForUpgrade`（执行扣款）。两者都可以不提供，缺省行为与参考「付得起」的一支相同。
8. **缺图不停机**。`MiniMonsters` 里参考写的是 `assert( monster != nullptr )`；端口改成 `GameLog.Warn` 后跳过该栈。
   缺一张 `MONS32` 画面不该把玩家从界面上打下来，也不该让存档一致性检查在测试里炸掉。
9. **`ImageOps.Borders.cs` 补上 `DrawBorder()`**：参考 `fheroes2::DrawBorder( image, color, thickness )` 是各条
   栏共用的小工具，端口此前没有；这次顺带落了，`ArmyBar` 的格子框与后面的栏都能用。

**后果**：新增 **11** 个文件、共 **2 091** 行：`Core/Imaging/ImageOps.Borders.cs` 83；`Game/Army/MiniMonsters.cs` 221、
`Army.MonsterLines.cs` 125、`Army.Persistence.cs` 89、`Troop.Persistence.cs` 62；`UI/Widgets/ArmyBar.cs` 388、
`ArmyBar.Input.cs` 423、`ArmyBar.Events.cs` 224、`ArmyBar.Redistribute.cs` 223、`ArmyBarSplit.cs` 79、
`ItemsBarLayout.cs` 174。测试 +33（`ArmyPersistenceTests` 8 条、`MiniMonstersTests` 6 条、`ArmyBarTests` 19 条），
Game 2 804 → **2 818**、UI 358 → **377**，合计 3 811 → **3 844** 全绿，`-warnaserror` 0 警告。
`army` 目录改记 1 已转 / 3 部分 / 0 未转（`army_ui_helper.cpp` 已转；`army.cpp`、`army_troop.cpp`、`army_bar.cpp`
各留一段，见遗留）。

**遗留**：`army.cpp` 的 `GetLuckModificator()`/`GetMoraleModificator()`（要 `Kingdom`/`Castle` 的加成链）与
`Army::String()`（本地化拼接）；`army_troop.cpp` 的 `GetAttackString()`/`GetSpeedString()`/`GetShotString()` 等
文字行；`army_bar.cpp` 的调用方 —— 英雄界面、城堡界面与编辑器的那几处 `QueueEventProcessing()` 还没接上；
存档的整体装配（`game_io.cpp` 的会话层）仍属 M8。

## ADR-183：列表控件只做状态机，贴图与行绘制由调用方给 ◐

**背景**：`kingdom` 目录剩下的两篇都是界面，其中 `kingdom_overview.cpp` 的英雄/城堡两个列表建立在参考的
`Interface::ListBox<Item>`（`gui/interface_list.h`）之上，而滚动条的索引↔像素映射在 `gui/ui_scrollbar.*` 里。
这两样是所有「多行可滚」窗口的公共前置（场景选择、法术书、宝物列表、招募、市场都会用到），但仓库里此前
只有 `ScrollSlider`（贴图拉伸），没有列表。

**决定**：

1. **`ListBox<T>` 按参考拆两层**：`ListBoxBase` 对应 `ListBasic`（非泛型，王国总览要靠它用一个指针在英雄表与
   城堡表之间切换），`ListBox<T>` 对应 `ListBox<Item>`。`T` 限 `class`，与参考「行以指针交出」一致。
2. **行内容一律由子类画**：`RedrawItem`/`RedrawBackground`/`ActionCurrentUp`/`ActionCurrentDn`/
   `ActionListSingleClick`/`DoubleClick`/`PressRight`/`LongPress`/`Cursor` 全是抽象或虚方法，签名照抄参考，
   但多一个 `PixelBuffer output` —— 端口没有全局 `Display`，帧缓冲从外面传。
3. **两个步进按钮不取图**：参考的 `SetScrollButtonUp( icn, index1, index2, pos )` 在端口里变成
   `SetScrollButtonUp( ButtonBase )`。控件层不碰 `ICN::`，调用方（对话框）自己按美术表造 `ButtonSprite`；
   按钮的按下外观由 `UpdatePressState(events)` 更新，不再在事件里顺手重绘。
4. **滚动条独立成类**：`ScrollBar` 只管 `Area`/滑块尺寸/`Position`/`min..max` 与五个移动方法，不继承贴图。
   `ListBox.UpdateScrollBarImage()` 仍用参考的 `ScrollSlider.Create(..., 0-8, 7-15)` 切拉伸，保持与
   `ui_scrollbar.cpp` 同一份几何。
5. **整数除法一处不改**：`MoveToIndex` 的截断、`MoveToPosition` 的 `+ length / 2` 与「单档返回 false」全部照抄，
   因为按下块靠返回值判断是否重画，行命中框也直接由同一套除法得出。
6. **两处必要的偏离**：`VisibleItemCount` 由 `protected` 放宽为可读（对话框算边框要用）；行区域小于行数时
   拖动的整除会除零，端口跳过那一段而不是像参考一样崩。

**后果**：新增 `UI/Widgets/ScrollBar.cs`（约 250 行）、`ListBoxBase.cs`（约 45 行）、`ListBox.cs`（约 700 行）；
测试 +27（`ScrollBarTests` 8、`ListBoxTests` 19），UI 377 → **404**，合计 3 844 → **3 871** 全绿，
`-warnaserror` 0 警告 0 错误。`gui` 目录的 `ui_scrollbar.cpp` 由「已转」补上另一半，`interface_list.h`
从「无」改记已转。

**遗留**：`kingdom_overview.cpp` 与 `view_world.cpp` 本身；`ListBox` 在场景选择/法术书/市场等窗口上的接线。

## ADR-184：世界视图先落档位模型，地图尺寸由调用方给 ◐

**背景**：`view_world.cpp`（795 行）是「世界视图」窗口：把整张地图按 1:4、1:6、1:12 各缓存一份（另加 1:1 调试档），
再按当前档位与中心从缓存里裁一块贴到屏幕上。它同时依赖 `ICN::MISC4/6/12`、`LETTER*`、`VWFLAG*` 六套图和
`Interface::GameArea` 的分块重画，整篇一次落不了；但其中「档位表 + 可见矩形 + 夹中心」是纯算术，且是所有
后续窗口（小地图点击、缩放按钮、光标定位）都要用的坐标系。

**决定**：

1. **拆成 `ViewWorldZoomLevels`（静态表与纯函数）+ `ViewWorldZoom`（有状态：档位 + 中心 + 每档矩形）**，
   对应参考的匿名常量、`computeROI`、`getNextZoomLevel`/`getPreviousZoomLevel` 与 `ZoomROIs`。
2. **地图尺寸走参数**：`ViewWorldZoom` 的构造要 `mapWidthInTiles`/`mapHeightInTiles`（参考读 `world.w()/h()`），
   与 ADR-111 一致；`tileWidthPx = 32` 仍是常量（`ui_constants.h` 的值）。
3. **三级视图的起始档位要校验**：参考只在开放调试档时才传 `ZoomLevel3`，三级数组里它越界；端口在构造时抛
   `ArgumentOutOfRangeException`，把参考的「调用方约定」变成显式契约。
4. **整数截断与 `( size - 1 ) / 2` 一处不改**：`computeROI` 的两步除法、`ChangeCenter` 的减一、`RoiInTiles`
   的 `+ 16` 全部照抄。这三处任意一处「修正」都会让四档的像素边界互相错开一格。
5. **状态变化才重算**：`ChangeCenter` 夹出来的中心与当前相同就返回 `false` 且不刷四档矩形；`ChangeZoom`
   先换档位再夹中心，复现「缩小时贴边中心被推回」。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorld.cs`（约 340 行）与 `ViewWorldZoomTests` 32 条；
Game 2 818 → **2 850**，合计 3 871 → **3 903** 全绿，`-warnaserror` 0 警告 0 错误。`kingdom/view_world.cpp`
在 PLAN 里由 ❌ 改记 ◐（模型已转，像素层未转）。

**遗留**：`CacheForMapWithResources`（18×18 分块重画 + `Resize` 四份缓存）、`DrawWorld` 的黑边填充、
`DrawObjectsIcons`（`renderCastleFlags`/`renderIcon`/`renderResourceIcon`）、`renderAI`/`renderCursor`、
`ViewWorldWindow` 的事件循环与小地图联动。

## ADR-185：世界视图的像素层靠注入绘图回调与精灵源，不引全局 ◐

**背景**：ADR-184 只转了 `view_world.cpp` 的坐标系。剩下的绘制部分有三处硬依赖：`CacheForMapWithResources` 里调
`Interface::GameArea::Redraw`（需要整个冒险地图的绘制管线）、`Assets::GetICN`（端口是 `IcnSpriteCache`）、以及
`DrawWorld` 里直接读 `Display` 与 `GetGameArea().GetROI()`。端口没有全局 `Display`/`GameArea`，硬拷贝会把单例
带进 `FHeroes2.Game`。

**决定**：

1. **分块重画做成回调**：`ViewWorldMapCache` 收一个 `ViewWorldBlockDrawer(PixelBuffer block, Point centre, ViewWorldDrawLayers layers)`
   委托。谁调谁来画一块 576×576 的地图（冒险地图渲染器或测试里的假画手），缓存的职责只剩「逐块重画 → 缩到四档」。
2. **精灵来源做成接口**：新增 `IViewWorldSpriteSource`（`GetSprite(IcnType, int)`），实现 `IcnViewWorldSpriteSource`
   包一个 `IcnSpriteCache`；测试用返回 2×2 纯色图标的假实现，断言的是「要了哪张图、第几帧」。
3. **屏幕是参数**：`DrawWorld(zoom, cache, display, visibleRoi, borderWidthPx, radarWidthPx)`，黑边宽度与雷达宽度
   默认取 `ui_constants.h` 的 16 / 144，但可覆盖；雷达区与窗口外一律不写。
4. **黑边不 assert，先求交**：参考假定整块贴图在屏内（`assert`），端口的 `FillBlack` 与 `display.Bounds` 求交，
   空区域直接返回，窄屏下右黑条算成负数也不会越界。
5. **对象类型先抹动作位再比**：参考 `switch` 用的是 `MP2::OBJ_*`（带动作位，`OBJ_CASTLE` = 206）；端口瓦片里
   两种值都能出现，统一 `Mp2ObjectTypes.NonActionType(...)` 后再比，避免「城门类型 35 的城镇不画旗」。
6. **城镇旗子仍只认城门格**：`GetCastleEntrance(tile.GetCenter(world))` 原样保留。端口的 `IsValidCastleEntrance`
   只认非动作值 `Castle`（与参考的 `OBJ_CASTLE` 判定相反），这是既有行为，不在渲染器里绕开；要改应改判定本身。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldMapCache.cs`（179 行）、`IViewWorldSpriteSource.cs`（46 行）、
`ViewWorldRenderer.cs`（375 行），测试 `ViewWorldMapCacheTests` 11 条 + `ViewWorldRendererTests` 23 条。Game
2 850 → **2 884**，合计 3 903 → **3 937** 全绿，`-warnaserror` 0 警告 0 错误。

**遗留**：`renderCursor` 的四种光标、`ViewWorldWindow` 的事件循环（换档/换模式/滚动），以及
`kingdom_overview.cpp` 整体。

## ADR-186：世界视图的雷达单独立类，缩放与边框复用冒险地图雷达的静态件 ◐

**背景**：`ViewWorldWindow` 的雷达在参考里是 `Radar` 的第二个实例（`RadarType::ViewWorld`），ROI 固定为整图，
但构造函数、`Build()` 与绘制都吃 `Mp2Map`。端口的 `Minimap` 是 `Game.AdventureMap` 里从 `Mp2Map` 建的一次性
面板（`ColorAt` 只读），既拿不到 `World` 的占领色/迷雾，也没有「按模式换一套颜色」的入口。

**决定**：

1. **单独立 `ViewWorldRadar`**（`Game/Kingdom`），构造只收 `World`。它持有「每格一个调色板索引」的数组与
   144×144 面板；`RedrawObjects(friends, mode)` 每次从头着色并重缩面板，语义等同参考的
   `RedrawObjects( Players::FriendColors(), mode )`。
2. **不复制 `Minimap` 的构建逻辑，只复制规则**：地面/王国/道路色与 `COLOR_GRAY` 仍是 `Minimap` 的公开常量，
   山的加深色阶也照它的 `HillShadeOffset`。
3. **缩放、边框、可见框抽成 `Minimap` 的静态方法**（`ScaleColors`、`DrawCursorFrame`、`CursorArea(roi, w, h)`），
   实例方法转调同一份实现。理由：两处雷达要用同一种点状边框与同一种「裁到世界再缩放」的取整；参考里它们就是
   同一个函数，端口只是被两个类共享。
4. **点击换算放在雷达里**：`CenterOn(zoom, panelPoint)` 把面板点换算成格、再换算成像素中心调
   `ViewWorldZoom.ChangeCenter`，返回值即「视图是否真的动了」。参考在此前用不同量纲比较过一次上左角，端口不照抄。
5. **城镇用 `world.Castles.Get(x, y)` 而不是城门判定**：参考的 `world.getCastle( point )` 命中的是城镇精灵
   覆盖的任意一格，端口 `AllCastles` 的占地表与之对应；`GetCastleEntrance` 只认城门格，用在雷达上会把 5×5
   的占地涂成草地色。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldRadar.cs`（约 270 行）与 `ViewWorldRadarTests` 16 条；
`Minimap` 多了三个静态方法（`GrayColor` 常量、`ScaleColors`、`DrawCursorFrame`、`CursorArea` 重载），
实例行为不变，原有 404 + 2 900 条测试全绿。Game 2 884 → **2 900**，合计 3 937 → **3 953**。

**遗留**：`ViewWorldWindow` 外壳（淡入淡出、缩放/退出按钮、侧栏整图、拖动与滚轮、退出记档位）与其在冒险地图
场景里的入口。雷达已可直接嵌入。

## ADR-187：世界视图窗口拆成「模型 + 调用方循环」，缓存由调用方建 ◐

**背景**：参考的 `ViewWorld::ViewWorldWindow()` 是一个 200 行的模态循环，里面混着淡入淡出、`Display` 的整屏
操作、`LocalEvent` 轮询、`Assets` 取图与 `Interface::GameArea` 的读写。端口的 App 层（`AdventureMapScene`）
已经是「场景收事件、`IScene` 分发」的结构，UI 层的对话框按 ADR-163 只是模型。

**决定**：

1. **`ViewWorldWindow`（`Game/Kingdom`）只做状态与几何**：淡出矩形、雷达/缩放按钮/退出按钮/侧栏的位置、初始
   视图（含「整图放得下就居中」）、拖动/滚轮/按钮/雷达点击的换算，以及侧栏与两个按钮的 `PixelBuffer` 绘制。
   它不轮询事件、不淡入淡出、不持有 `Display`。
2. **缓存不在窗口里建**：参考的 `CacheForMapWithResources` 需要一层四档地形渲染器（MISC4/6/12 + 迷雾 +
   城镇/英雄图标），端口还没有；窗口因此**接收**已建好的 `ViewWorldMapCache`，`DrawWorld` 只负责贴图。
   这条边界让窗口现在就能测，也让配色渲染器可以单独落地。
3. **按钮区来自精灵尺寸**：参考 `Button::area()` 就是精灵的宽高，端口的窗口在构造时用
   `IViewWorldSpriteSource` 读出两张按钮图的尺寸；取不到就是空矩形，点击自然落空——比参考的断言宽松，比崩溃好。
4. **雷达只在开窗时重新着色**：`DrawRadar(display, renderMapObjects)` 的参数对应参考
   `RedrawForViewWorld( roi, mode, renderMapObjects )`：`true` 整图重着色（开窗一次），`false` 只重画光标框。
5. **档位夹取**：`ViewWorldZoom` 的构造对「档位 ≥ 档位数」抛异常（照抄参考的 `assert`），而设置文件里可能存着
   编辑器用过的第 4 档；窗口在构造前把它夹到最后一档，调用方不必先判断。
6. **`Close()` 就是持久化点**：它返回 `Zoom.ZoomLevel`，对应参考的 `conf.SetViewWorldZoomLevel(...)`；端口没有
   `Settings` 的等价项，写回留给 App（`IScene` 或 `GameHost` 的配置存储）。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldWindow.cs`（约 430 行）与 `ViewWorldWindowTests` 21 条；
Game 2 900 → **2 921**，合计 3 953 → **3 974** 全绿，`-warnaserror` 0 警告。

**遗留**：`CacheForMapWithResources` 的分块绘制器（四档地形 + 迷雾 + 图标）与冒险地图场景里的入口按钮，
两者都还没有；窗口本身已经可以被这些代码直接驱动。

## ADR-188：世界视图的缓存由「一个 576×576 的冒险地图视图」逐块渲染，城镇默认不透过迷雾 ◐

**背景**：`ViewWorldMapCache` 早已转好（18 格一块、缩到三档、贴到缓存图），但它要一个
`ViewWorldBlockDrawer`——参考里那是「把 `Interface::GameArea` 的 ROI 临时改成 576×576、逐块设中心、`Redraw`」。
端口直到现在只有测试里的假绘制器，所以世界视图还不能真正出图。

**决定**：

1. **`ViewWorldMapBuilder`（`Game/Kingdom`）**：持 `Mp2Map` + `TerrainTileSet` + `World` + 四个精灵源，
   `CreateView(mode)` 返回一个 576×576 的 `AdventureMapView`，`Build(mode, levels)` 把闭包
   `(block, center, layers) => { view.ScrollTo(center - 288); view.Draw(block, layers.Towns); }` 交给缓存。
   **一个缓存配一个新视图**，构建完即弃：端口没有全局地图区域，也就不存在「世界视图改掉冒险地图 ROI」的风险。
2. **英雄不进缓存**：参考把 `LEVEL_HEROES` 异或掉、改由 `DrawObjectsIcons` 贴图标，所以视图固定 `heroes: null`。
3. **迷雾随模式**：`layers.Fog` 决定传不传 `World.Fog`（全图模式传 `null`）。
4. **`AdventureMapView.Draw(target, drawTownsOverFog)`**：把参考的 `LEVEL_TOWNS` 显式化，**默认关闭**。
   端口原先的迷雾通道无条件把 `OBJNTWBA`/`OBJNTOWN` 补在迷雾上，等于冒险地图也把未侦察的城镇显形；
   参考里那个位只有「看城镇」「看全图」两个法术会设，冒险地图的主重绘从来不带。这是一次保真度修复，
   顺带让 `ViewTowns` 模式有了实现途径。
5. **不预校验地图边长**：`ViewWorldMapCache` 已经对「不是 18 的倍数」抛 `ArgumentException`，构建器不重复。

**后果**：新增 `src/FHeroes2.Game/Kingdom/ViewWorldMapBuilder.cs`（约 150 行）与 `ViewWorldMapBuilderTests` 8 条；
`AdventureMapView.Draw` 多一个参数（默认值保持「不画迷雾里的城镇」），`FogLayerTests` 拆成默认/显式两条。
Game 2 921 → **2 930**，合计 3 974 → **3 983** 全绿，`-warnaserror` 0 警告。

**遗留**：App 侧没有任何模态对话框设施，也没有淡入淡出，所以世界视图还没接到界面上；接入需要先有
「覆盖冒险地图的模态场景」这一层，再谈打开入口（参考从冒险选项对话框的世界地图按钮进）。

## ADR-189：世界视图的模态由「覆盖层 + 主机分流」拼成；产出型对象的收入在装图时写进元数据 ◐

**背景**：世界视图的模型、缓存、绘制、雷达与窗口都转好了，但它们只被测试驱动，界面上没有入口。参考的
`ViewWorldWindow()` 是**阻塞**循环（`view_world.cpp:589-795`），端口的 App 是「主机收 SDL 事件、场景收调用」，
没有嵌套循环；同时真实地图第一次冒烟就崩在两个 `GetValidItemsCount() == 1` 断言上。

**决定**：

1. **不引入嵌套循环**：`ViewWorldOverlay`（`App/Scenes`）只做循环里的两件事——把事件翻译成窗口调用、把窗口画满
   屏幕。开关（建缓存、写回档位）留给 `AdventureMapScene`，因为开窗要重建一张缓存，那属于场景的状态。
2. **主机按事件类型分流，不按「是否有对话框」分流**：`GameHost` 在世界视图打开时把鼠标移动/离开、滚轮、左键喂给
   覆盖层，键盘在 `HandleAdventureKey` 最前面拦截（`Escape` 关闭，其余忽略）。参考的循环也只认左键、滚轮、
   `Exit`/`Escape`；把拦截写进已经存在的几个 `if` 里，比给 App 造一套通用模态栈便宜。
3. **点击按「按下与松开同在一个按钮」判定**：照抄 `MouseClickLeft` 的语义，覆盖层记住按下位置再在松开时比较；
   地图拖拽则从按下就开始，中心由 `ViewWorldWindow.DragTo` 推。这样「按下 EXIT 再拖出去松开」不会误关。
4. **档位写回场景字段**：`CloseViewWorld()` 返回 `ZoomLevel?`，场景把它存进 `_viewWorldZoom`（参考的
   `conf.SetViewWorldZoomLevel`）。端口没有 `Settings`/配置文件对象，所以只留在内存里，下次开窗沿用。
5. **入口临时化**：参考从冒险选项对话框的「世界地图」按钮进，那个对话框没转；先用 `V` 键与 `--view-world`
   命令行开关顶上，两处代码注释写明是临时入口，等对话框落地再撤。
6. **收入元数据在装图时补**：`Tile.Init` 末尾调 `InitIncomeObjectMetadata`，按对象类型与 `EXTRAOVR` 车架号照参考
   (`map_object_info.cpp:4256-4277/4403/4423`) 填 `metadata[0]`（资源位标志）与 `metadata[1]`（每日产量）。MP2 文件
   没有元数据块，`metadata[0]` 只是 `quantity >> 3`，把对象信息里的事实放在装图路径上是唯一不散落的地方。
7. **多格对象「没有资源就不画」**：参考给整个对象的所有格子写同一份元数据；端口按格读 MP2，矿井邻格与资源堆邻格
   读到空 `Funds`。渲染器把 `assert( GetValidItemsCount() == 1 )` 换成「不是恰好一项就跳过」，宁可少画一个邻格图标，
   也不崩、也不画五份。

**后果**：新增 `src/FHeroes2.App/Scenes/ViewWorldOverlay.cs`（约 155 行）；`AdventureMapScene` 加
`OpenViewWorld`/`CloseViewWorld`（后者返回档位）；`GameHost` 加四处分流与 `V` 键；`CommandLineOptions` 加
`--view-world`；`Tile` 加 `InitIncomeObjectMetadata`/`SetIncomeMetadata`；`ViewWorldRenderer` 两处断言改守卫。
`TileMetadataTests` +7（装图路径的收入表），`ViewWorldRendererTests` +1（`metadata` 全零不请求精灵）。
Game 2 930 → **2 938**，合计 3 983 → **3 991** 全绿，`-warnaserror` 0 警告。App 层没有自动化测试，用
`--view-world` 冒烟截图（`artifacts/viewworld.png`）验证。

**遗留**：淡入淡出没做；正式入口（冒险选项对话框）未转；档位没有落地到配置；`OBJ_RESOURCE` 的多格邻格在参考里也会
各画一次图标（因为元数据被复制），端口现在只画带精灵的那一格——若要完全对齐，得在装图时把整组对象的元数据分发到
全部格子，这需要先有「对象 → 格子」的映射，暂不做。

## ADR-190：招募英雄确认框复用建造框的骨架；英雄默认名做成 `Hero.Name` 的回落；酒馆补位交给带 `World` 的重载 ◐

**背景**：建造列表的两个英雄位在第十三批只上报「点了哪个位」。参考在那里的下一步是
`Castle::DialogBuyHero( hero )`（`castle_town.cpp:66-160`）：标题、`SURRENDR.ICN` 帧 4 的 111×105 相框、大肖像、一句
说明、`PaymentConditions::RecruitHero()` 的 2500 金币价格框、OKAY/CANCEL。模型 `RecruitHeroDialog` 早在 ADR-163 就有
（`Title`/`Description`/`Cost`/`ConfirmText`/`CanRecruit`），缺的是视图、窗口与入口。

冒烟时另外撞到两个洞：(1) 两个英雄位永远是空的——`Kingdom.GetRecruits( World, ... )` 的补位重载写好了却**没有人调**，
`ActionNewWeek` 结尾叫的是不带世界的那个；(2) 对话框里写着「 is a level 1 Wizard without artifacts.」——`Hero.Name`
只有地图的自定义名字，73 个默认名字那张表（`heroes.cpp:84-121`）没搬。

**决定**：

1. **视图照搬建造说明框的分工**：`RecruitHeroLayout`（几何）、`RecruitHeroArtwork`（`SURRENDR` 帧 4 + `RESOURCE` 0~6 +
   `BUYBUILD` 六块边框）、`RecruitHeroView`（绘制与按钮）、`App/Scenes/RecruitHeroWindow`（窗口：光标、按键、说明框）。
   大肖像不进素材类——它是每个英雄的 `PORT00xx` 帧 0，由窗口按英雄取，所以同一个对话框可以给任何英雄用。
2. **高度不带领先的 10 像素**：`DialogBuyHero` 的标题写在 `dialogRoi.y + 2`，相框从标题高度之后才开始
   （`castle_town.cpp:104-114`），与建造框「先让 10 像素再放标题」不同，`GetContentHeight` 照参考只加四段与三个间隔。
3. **相框右键只上报**：英雄速览（`Dialog::QuickInfo`）没搬，窗口用 `IsPortraitUnder` 回答「是否点在相框上」，
   由场景记一条日志；两个按钮的右键说明（`Recruit X the Y.` / `Exit this menu without doing anything.`）照参考写。
4. **招人之后整屏回城镇**：参考在 `recruitHeroDialog()` 里 `castle->RecruitHero( hero )`，`_openConstructionDialog`
   随即返回，建造列表关掉、城镇屏留着并重画英雄像（`castle_dialog.cpp` 的 `RecruitHero` 分支把 `alphaHero` 归零）。
   端口因此是「关确认框 + 关列表 + `DrawTown()`」，不重开列表。
5. **默认名字做成 `Hero.Name` 的回落**：新增 `HeroNames`（73 条英文原文，顺序即编号），`Name` 变成
   `_customName` 为空时取表——这正是参考 `Heroes::GetName()` 的形状，改一处就修好所有要显示英雄名的地方。
   `FixFrenchCharactersInName` 同时收窄成只改自定义名字，否则会把默认名字写进 `_customName`，之后再也回不到表。
6. **补位放进带 `World` 的重载**：`Kingdom.ActionNewWeek( World, uint, bool )` 先跑原来的逻辑，再 `GetRecruits( World )`；
   `Kingdoms.NewWeek( World, uint, bool )` 把世界透传下去（七个王国自己不持有世界，ADR-111 不变）；
   `ConstructionWindow` 取英雄时改用带世界的重载——参考的 `castle_town.cpp:193` 叫的就是会补位的那个。
7. **脚本入口加 `--town-hero <0|1>`**：对话框正常要两次点击（列表的英雄位、再 OKAY），`--left-click` 只有一个，
   所以给一条命令行直接开框；同一次跑里再点 OKAY 就能验证整条「开框 → 扣 2500 → 回城镇」。

**后果**：新增 `src/FHeroes2.UI/Dialogs/RecruitHeroLayout.cs`、`RecruitHeroArtwork.cs`、`RecruitHeroView.cs`，
`src/FHeroes2.App/Scenes/RecruitHeroWindow.cs`，`src/FHeroes2.Game/Heroes/HeroNames.cs`；`CastleScene` 加
`_recruitHero`/`OpenRecruitHero( Hero )`/`OpenRecruitHero( int )`/`FollowRecruitHero`/`HireRecruitedHero`/
`ShowRecruitHeroHelp` 与四处分流；`ConstructionWindow` 加 `FirstHero`/`SecondHero`、取英雄改带世界；
`Kingdom` 加带世界的 `ActionNewWeek`，`Kingdoms.NewWeek` 改签名（唯一调用点在 `World.NewWeek`）；
`CommandLineOptions` 加 `--town-hero`。测试：`RecruitHeroViewTests` +6（布局算术、相框居中、按钮灰化、点击回答、
绘制落在框内），`HeroNamesTests` +12（表长与编号对位、越界、默认名回落）；Game 2 938 → **2 950**，UI 818 → **824**，
合计 4 408 → **4 426** 全绿，`-warnaserror` 0 警告。截图 `artifacts/recruit-hero.png`（对话框）与
`artifacts/recruit-hero-hired.png`（招完回城镇，金币 4500 → 2000，卫戍栏换成新英雄的像）。

**遗留**：相框右键的英雄速览（`Dialog::QuickInfo`）未搬；酒馆（`TavernWindow`）还没接这份名单；`AllHeroes` 的
`RECRUIT` 模式位与投降英雄的处理仍是 ADR-118 的状态；英雄名只有英文原文，没有翻译层。

## ADR-191：英雄速览框照参考的「固定内容框」排布；右键的「按住才显示」用松开清空实现；亡灵士气图标先灰再暗 ◐

**背景**：城堡屏三处右键都该弹英雄速览——招募确认框的相框（`castle_town.cpp:148`）、建造列表的两个英雄位、
城镇屏卫戍栏的英雄像（`castle_dialog.cpp:551-553`），而船长那一处（`:549-550`）端口还没有船长像。参考的
`Dialog::QuickInfo( const Hero & )` 是个**阻塞**模态框：`while ( le.HandleEvents() && le.isMouseRightButtonPressed() )`
——按住右键期间一直显示、松开就退出（`dialog_quickinfo.cpp:886-911`）。框里的东西全照 `showQuickInfo( HeroBase )`
（`:693-911`）：`QWIKHERO` 帧 0 当底板、`MINIPORT` 小肖像配 `LOCATORS` 帧 22 的相框、`MINILKMR` 六帧士气/幸运、
`FLAG32` 两面阵营旗、四维 + 魔法点 + 移动力六行、最后一行 `DrawSingleDetailedMonsterLine` 的军队。

**决定**：

1. **内容框是常数，底板尺寸只当输入**：参考把 `cur_rt` 重写成底板 `+28,+10` 起的 146x144，之后每个部件都是相对
   这个矩形的固定偏移。端口因此不写死 236x160，`HeroQuickInfoLayout.Create( box, 相框尺寸, 旗子尺寸 )` 只吃位置与
   两个会变化的尺寸，测试可以用任意合成底板验算术。
2. **「按住才显示」放在右键的两个端点**：`SetRightButton( down )` 开头 `if (! down) _heroInfo = null;`，按下时按命中
   建框。参考的模态循环换成了浮层，`DrawFrame` 拆成 `DrawWindows()` + 最后画速览框 + `InvalidateAll()`，
   这样三处入口共用一条绘制路径，也不会被各个 `return` 分支挡住。
3. **亡灵士气先灰再暗**：参考对 `MINILKMR` 的士气帧连续两次 `ApplyPalette`（GRAY、DARKENING）表示「亡灵没有士气」，
   端口把图标复制进临时 `PixelBuffer` 再调两次 `ImageOps.ApplyPalette`，不动原件。
4. **小肖像不进素材类**：`MINIPORT` 的「编号 - 1」帧属于每个英雄，跟招募框的大肖像一样由调用方给，缺了就不画。
5. **凑不出颜色的英雄不画旗**：`getCastleLeftFlagIcnIndex()` 只认六色与中立（`:196-220`，其余 `assert`），端口遇到
   组合色返回 `null`，宁可少画两面旗也不猜颜色。
6. **位置照 `makeRectQuickInfo`**：`StandardPopupWindow.PositionOf( 光标, 界面矩形, 尺寸 )`，界面矩形给整屏 640x480
   ——参考在城堡界面里也这么问（它拿的是冒险地图那块区域），端口不另造规则。

**后果**：新增 `src/FHeroes2.UI/Dialogs/HeroQuickInfoLayout.cs`、`HeroQuickInfoArtwork.cs`、`HeroQuickInfoView.cs`；
`CastleScene` 加 `_heroInfo`/`_heroInfoArtwork`/`_miniMonsters` 与 `ShowHeroQuickInfo( Hero )`，`DrawFrame` 拆成
`DrawWindows` + 覆盖层，`SetRightButton` 三处接线（招募框相框、建造列表英雄位、城镇屏英雄像）。测试：`HeroQuickInfoTests`
+15（内容框算术、相框居中、图标列上下移、旗子锚点、六行四维、军队线、图标数量、旗号映射、界面太小时拒绝、真实素材下
贴边与底板像素、名字落在内容框里）。UI 824 → **839**，合计 4 426 → **4 441** 全绿，`-warnaserror` 0 警告。
截图 `artifacts/hero-quick-info.png`（招募框上右键相框）与 `artifacts/hero-quick-info-list.png`（建造列表右键英雄位）。

**遗留**：城镇屏船长像的速览（参考 `:549-550`，端口还没画船长像）；英雄位的士气/幸运小图标；
`isFullInfo == false` 的简化版只有一条代码路径、没有入口去触发（敌方英雄的速览要等冒险地图的英雄交互）；
英雄速览里的阵营旗用的是 `FLAG32` 的左右两帧，与城堡图上的旗子同源，但玩家配色的淡入仍未搬。

## ADR-192：速览框改吃「英雄/船长快照」；船长大头像在 UI 层合成、画的时候才建；右键先徽记后英雄像 ✅

**背景**：参考的速览框一直吃 `const HeroBase &`（`dialog_quickinfo.cpp:693`），船长的入口是城镇屏左侧徽记上的右键
（`castle_dialog.cpp:547-548` 的 `Dialog::QuickInfo( GetCaptain() )`）。船长是 `HeroBase` 而不是英雄：`isActiveHero`
为假，所以名字后面没有 `" (Level N)"`、六行四维只画五行（没有 `Move Points:`），名字取城镇名、军队就是卫戍部队本身。
它的头像是 `Captain::GetPortrait()`（`castle/captain.cpp:200-250`）：大头像是 `PORT0090`~`PORT0095` 再贴该色的 `HEROFL`
帧 0（偏移见 `:64-84`），小头像是 `MINICAPT` 帧 0~5。城镇屏左侧立的那张也是大头像（`:138-145` 在船长住处建好后用船长像
替换徽记），端口此前只有徽记。

**决定**：

1. **端口没有 `HeroBase`，改成一份快照**：新增 `UI.Dialogs.HeroQuickInfoSubject`，由 `FromHero( Hero, 小肖像 )` 与
   `FromCaptain( Captain, 小肖像 )` 两个工厂取数，`HeroQuickInfoView.TryCreate` 只认它。判据是 `Level` 与 `MovePoints`
   为 `null`——正好对应参考的 `isActiveHero`：为空就不加等级后缀、不画移动力那一行，其余（幸运、士气、双旗、四项基本
   技能、魔法点、军队）一律照旧。用小肖像当参数外的第二份输入，是因为 `MINIPORT` 与 `MINICAPT` 是两套素材，素材读取留在
   调用方。
2. **肖像不进 `Game` 层**：`Captain.GetPortrait()` 拆成 `UI.Dialogs.CaptainPortrait`——它要读归档，而 `Captain` 只该
   管数值。`TryCreateBig` 克隆 `PORT00xx` 帧 0 再把 `ICN::getFlagIcnId( 颜色 )` 帧 0 贴到种族偏移上（参考也是把旗子贴到
   底图的拷贝上，原件不动），`TryCreateSmall` 取 `MINICAPT` 的种族帧。没有船长的种族（`MapRace.None`）一律返回 null。
3. **大头像懒加载、按「城 + 颜色」缓存**：船长住处是城镇屏里建的，所以不能在切城时就读；`TownCaptainPortrait( castle )`
   在画的时候才合成，同一座城、同一个颜色直接复用，城易主（颜色变了）才重贴旗子。
4. **右键顺序照参考**：先 `rectSign1`（徽记 → 船长）后 `rectSign2`（英雄像 → 英雄），命中区就用 `CrestArea`——参考的
   `rectSign1` 本来也是 `CREST` 的尺寸。船长没建出来时按键照旧落到英雄像上。
5. **`CrestArea` 只当命中区**：大头像 101×93 比 `CREST` 的 100×92 各多一像素，参考也是从 `+5,+262` 原样贴、不裁，端口
   照做；命中判定仍按 `CREST` 尺寸，与参考一致。

**后果**：新增 `src/FHeroes2.UI/Dialogs/CaptainPortrait.cs`、`HeroQuickInfoSubject.cs`；`HeroQuickInfoView` 改吃快照并
去掉 `portrait` 参数（`Hero` 属性换成 `Subject`）；`CastleScene` 加 `TownCaptainPortrait` 缓存与 `ShowCaptainQuickInfo`，
`ShowHeroQuickInfo`/`ShowCaptainQuickInfo` 共用 `ShowQuickInfo( subject )`；`CastleScreenFrame.CaptainPortrait` 终于有值；
`Captain` 的过时注释（「`MINICAPT` 没有枚举」）改掉。测试：新增 8 条（英雄快照与船长快照的差异、种族→头像/帧/旗偏移、
无船长种族、真实素材下大头像尺寸与小头像、旗子确实贴上且原件不动、船长只画五行、英雄仍画六行）。UI 839 → **847**，
合计 4 441 → **4 449** 全绿，`-warnaserror` 0 警告。截图 `artifacts/captain-portrait.png`（骑士船长与红旗）与
`artifacts/captain-quick-info.png`（右键徽记弹出的船长速览：城名、小肖像、两面紫旗、五行、军队行）。

**遗留**：船长像的淡入（参考 `RedrawIcons` 用 `AlphaBlit`，端口直接换）；`--town-build Captain` 之后 `Captain.LoadDefaults`
是构造时做的，参考在住处建好时会再灌一次默认（当前数值相同，等有增益建筑再补）；船长的战斗界面（`battle_only` 模式）
未接；酒馆名单、`RECRUIT` 模式位、敌方英雄的 `isFullInfo == false` 路径照旧。

## ADR-193：酒馆窗口复用 MessageBox 的两段 `MultiFontText` + `TAVWIN` 动画；模态框压过城镇屏、右键不关 ✅

**背景**：`castle_dialog.cpp:697-701` 的 `BUILD_TAVERN` 分支调 `Castle::_openTavern()`
（`castle_tavern.cpp:39-54`）：本周传闻加一句固定引子，配 `TAVWIN.ICN` 的吧台手动画，再用
`showStandardTextMessage( GetStringBuilding( BUILD_TAVERN ), {}, Dialog::OK, { &imageUI, &textBodyUI } )` 弹框。
参考只组两个元素——`AnimationDialogElement( ICN::TAVWIN, { 0, 1 }, 0, 75ms )` 与一个装了两段 `MultiFontText` 的
`TextDialogElement`——没有独立的窗口类。亡灵的酒馆位是 `BUILD_SHRINE`，走的是说明框，不是这个函数。

**决定**：

1. **模型与渲染分开，渲染复用 `MessageBox`**：`TavernDialog` 只给标题、引子、本周传闻与 `DialogButton.Ok`；
   `TavernView.TryCreate` 把 `TAVWIN` 精灵与 `BUYBUILD` 框、两套字体、`SYSTEM` 按钮拼成 `MessageBox`。
   `frameCount = min( 20, 精灵数 - 2 )`，连一帧吧台手都没有（少于 3 张）就返回 `null`，不画半截动画。
2. **两段文字仍是两个 run**：引子与传闻各是一个 `Text`，中间 `"\n\n"`；因为没有移植本地化子系统（ADR-139），
   两段都用 `FontType.NormalWhite`，传闻的语言标签只留在 `LocalizedString` 里。空传闻由 `TavernDialog.Create`
   直接拒绝——地图 loader 从不存空传闻，空值只可能是 bug。
3. **动画参数照参考当常数**：背景帧 `{ 0, 1 }`、首帧 2、20 帧、每帧 75ms（`DelayType::CASTLE_TAVERN_DELAY`）。
   参考的 `ICN::getAnimatedIcnIndex()`（`2 + n % 20`）由 `AnimationDialogElement` 的「首帧 + 帧数」表达，
   不回头看 ICN 文件；素材读取留给调用方，和其它窗口一致。
4. **模态层级最高**：`CastleScene` 的 `_tavern` 在说明框、英雄速览和所有建筑窗口之前判；`SetCursor`、`SetLeftButton`、
   键盘、`Update`、`DrawWindows` 五处都先看它。`Update` 只推吧台手的动画，城镇屏自己的帧不动；
   `DrawWindows` 每帧先把城镇屏照常重画一遍再叠框，因为参考的 `showStandardTextMessage` 是「在当前画面上摆框」。
5. **只有 OK 能关**：左键在按钮上完成按下/松开、ESC/Enter/小键盘 Enter 三键都当 OK；右键直接忽略（`Dialog::OK`
   不认右键），不会像收入框那样按住右键显示。开框时顺手清掉 `_message`/`_messageHeldByRightButton`/`_heroInfo`。
6. **入口走 `OpenBuilding` 的 `Tavern` 分支**：`OpenTavern()` 要求城镇已建酒馆、`BUYBUILD` 框、`TAVWIN` 精灵与
   `SYSTEM` 按钮四样齐备，缺一样只记日志不开框。亡灵建的是 `Shrine`（`castle_dialog.cpp` 的说明框分支），
   所以这个 `Tavern` 分支不会顶掉亡灵的神龛。
7. **冒烟入口**：`--town-window tavern`、`--town-building Tavern`；未建酒馆时先加 `--town-build Tavern`。
   城镇屏点击坐标依赖种族，而 Beltway 的玩家城是随机城、载入时重掷种族（`World.Castles.cs`），固定坐标只在
   对应种族的那次启动命中——实测骑士酒馆区中心 `(386,145)` 开窗成功，其余几次启动是别的种族、点在同一处不会开。

**后果**：新增 `src/FHeroes2.UI/Dialogs/TavernView.cs`；`CastleScene` 加 `_tavern`、`OpenTavern()`、`CloseTavern()`
与五处优先级判断，`GameHost` 的 `--town-window` 加 `tavern`。测试：`TavernViewTests` +7（常数与参考一致、
少于 3 帧拒绝、两个元素与吧台手尺寸、单个居中的 OK、按下松开返回 OK、75ms 后换帧、六个参数都不许空）。
UI 847 → **854**，合计 4 449 → **4 456** 全绿，`-warnaserror` 0 警告。截图 `artifacts/tavern.png`（酒馆框）、
`artifacts/tavern-from-click.png`（点城镇屏酒馆建筑开框）、`artifacts/tavern-closed.png`（点 OK 后回到城镇屏）。

**遗留**：玩家配色与淡入（城堡屏主线只剩这一项）；船长战斗界面（`battle_only`）；随机城的固定坐标冒烟不可复现，
要么等地图随机种子可设，要么给 CLI 加按建筑名取中心的点击入口；酒馆传闻的多语言字体仍随本地化一起缺。

## ADR-194：城堡旗帜按玩家配色——152~174 整段线性映射到六色色阶，只有六张图、按帧裁矩形 ✅

**背景**：`drawCastleDialogBuilding`（`ui_castle.cpp:110-148`）在 blit 之前先问
`isImagePlayerColorDependent()`（`ui_castle.cpp:150-165`），命中就用 `getModifiedByColorImage()` 现做一张换过
调色板的图：`getColorEffectiveAreas()`（`:85-134`）给出这一帧里旗帜所在的矩形，`getModifiedPaletteByPlayerColor()`
（`:61-83`）给出从调色板 152~174（十六色加七级阴影）线性映射到该玩家色十六色色阶的 256 项表。命中名单只有六张：
骑士的城堡、左塔、右塔、五级住宅 `TWNKDW_4` 与升级版 `TWNKUP_4`、法师的城堡；死灵、野蛮人、女巫、术士的城堡
本来就没有玩家色的旗。无主的城（`PlayerColor::NONE`）在参考里会断言失败，正式版本退化为恒等映射。

**决定**：

1. **表就是恒等表加一段**：`CastleFlagPalette.ForColor()` 建 256 项表，只重写 152~174 这 23 项，其余保持下标。
   映射公式照参考 `outputStart + i * outputLength / inputLength`；六个玩家色的段是蓝 `63/22`、绿 `85/23`、
   黄 `108/23`、紫 `132/21`、红 `175/23`、橙 `199/16`。无主直接给恒等表，不抛断言。
2. **只改旗帜所在的矩形**：`EffectiveAreas()` 逐图逐帧照参考写死——法师城堡帧 0 是 `(76,7,3,3)`、`(96,7,3,3)`、
   `(178,11,3,3)`；骑士城堡帧 0 是 `(127,36,3,3)`、`(287,6,3,3)`；骑士左/右塔帧 0 是 `(5,6,3,3)` / `(55,6,3,3)`；
   五级住宅两种帧 0 是一像素 `(61,3,1,1)`、其余帧 `(59,0,6,5)`；其它帧一律整帧。整帧也不怕，因为表在 152~174
   之外是恒等。
3. **按「图 + 帧 + 颜色」缓存**：参考每画一次就新建一张，端口把结果留在 `CastleFlagPalette` 的字典里，动画二十帧
   与六个颜色最多一百多张，缓存的图与原图一样只读。`Get()` 对无主与非旗帜图直接返回原精灵，连克隆都不做。
4. **颜色一路从城堡传下来**：`CastleScreenPainter.DrawBuilding()` 末尾加 `PlayerColor color = PlayerColor.None`，
   全部二十一处在调用时补 `castle.Color`（原来带 `alpha` 的位置参数调用改成显式尾部实参，测试里的 `alpha:` 具名
   调用不受影响）。构造窗那侧的 `CastleImagePainter` 用 `OBJNTOWN`，参考不换色，保持不动。
5. **顺手让冒烟日志带上种族**：`--enter-town` 的日志改成
   `Opened the town screen of 'Quick Silver' (Wizard) from the command line.`，因为随机城每次载入重掷种族，
   点固定坐标的冒烟必须知道这一局是哪个族（上一条遗留的坑）。

**后果**：新增 `src/FHeroes2.UI/Dialogs/CastleFlagPalette.cs`；`CastleScreenPainter` 的 `DrawBuilding` 与二十一
处调用改签名，类注释从「未搬玩家配色」改成「配色见 `CastleFlagPalette`，未搬的只剩 `AlphaBlit`」。测试新增
`CastleFlagPaletteTests` 21 条（常量与六色段的首尾值、段外恒等、无主恒等、六张图名单、逐图逐帧的矩形、
只改旗像素、动画帧整帧、无旗帜图原样返回、缓存按颜色共享）。UI 854 → **875**，合计 4 456 → **4 477** 全绿，
`-warnaserror` 0 警告。截图 `artifacts/town-flag-color.png`：红玩家的法师城，三面旗帜是红的。

**遗留**：`AlphaBlit` 的半透明混合（新建筑淡入仍是一步到位，城堡屏主线只剩这一项）；船长战斗界面
（`battle_only`）；酒馆传闻的多语言字体随本地化一起缺。

## ADR-195：王国侧 AI 按「扫描 → 评估 → 决策」三段落地，KingdomTurn 的回合循环等 B5 ✅


**背景**：`ai_planner_kingdom.cpp` 的 `Planner::KingdomTurn()`（`:660-900`）是一整段：清缓存、数英雄、
逐瓦片扫描、`evaluateRegionSafety`、`setHeroRoles`、优先级目标的重算、城堡循环、英雄循环。端口里英雄循环的
第一个调用就是 `HeroesActionComplete`（`ai_planner_hero.cpp`，B5 范围），所以这段在 B4 无法整段落。

**决定**：

1. **扫描段抽成 `internal int ScanMap(GameKingdom)`**：清缓存 → 按 `world.RegionCount` 建 `_regions`（瓦片
   的 `Region` 直接做下标）→ 数非巡逻英雄 → 逐瓦片填 `_mapActionObjects`/`_enemyArmies`/区域统计 →
   收尾调 `EvaluateRegionSafety` 与 `UpdateKingdomBudget` → 返回 `availableHeroCount`。参考把这些写在循环里，
   端口分开是因为「`evaluateRegionSafety` 的产物只被扫描产生」——写在两处容易漏；B5 的 `KingdomTurn` 只剩
   「`ScanMap` → `SetHeroRoles` → 城堡循环 → 英雄循环」。
2. **对象类型一律先折叠（ADR-133）**：瓦片缓存的是带动作位的值（英雄 `ActionHero` = 183），
   `GetEnemyArmyOnTile` 原先拿它比 `Mp2ObjectType.Hero` 永不成立，本批改成 `Mp2ObjectTypes.NonActionType`；
   `_mapActionObjects` 仍存瓦片原值，与 `UpdateMapActionObjectCache` 保持同一口径（两者在同一字典里混存会不自洽）。
3. **`View All`、`SLEEPER`、次元门次数不移植**：端口没有冒险地图施法，扫描只认「不在雾里」的瓦片，
   参考里的 `bestHeroToViewAll` 与每英雄的 `ResetModes(SLEEPER)`/`setDimensionDoorUsage(0)` 都没有对应状态，
   注释里逐条写明（有状态时再补，不预留空方法）。
4. **测试窗口只开必要的四个**：`Regions` 只读属性 + `SetHeroRoles`/`GetSortedCastleList`/
   `PurchaseNewHeroes` 由 private 改 `internal`。生成的空地图没有城堡表（`GetCastleEntrance` 只在装图时
   登记），所以「友方城堡 +100 / 敌堡 -100」两支在合成世界上跑不到，测试覆盖「敌方英雄 → -50 → 邻区取份额」、
   撤走英雄后归零、受威胁城堡排前与招募上限。

**后果**：新增 `src/FHeroes2.Game/Ai/AiPlanner.Kingdom.cs`（十个成员 + `ScanMap`）与
`tests/FHeroes2.Game.Tests/AiKingdomPlannerTests.cs` 8 条；`AiPlanner` 改 `partial` 并删掉十个占位；
`ai_planner.cpp` 的最后一件 `revealFog` 一并落地，该文件全转。`ai` 行 4/1/4，合计 68/84/97，
剩余 `NotImplementedException` 60 处。`KingdomTurn` 的回合循环与 `HeroesActionComplete` 留待 B5。

## ADR-196：`AIWorldPathfinder` 的六个英雄入口由调用方显式给英雄，迷雾有两张表 ✅


**背景**：`world_pathfinding.cpp:566-1434` 的 AI 寻路器在端口里早已落地，只有六个英雄相关入口还抛异常：
`ReEvaluateIfNeeded`、`getFogDiscoveryTile`、`getNearestTileToMove`、`isHeroPossiblyBlockingWay`、
`buildDimensionDoorPath`、`getObjectsOnTheWay`（参考里这些方法大多直接读缓存的 `_pathStart`/`_pathColor`）。
B5 的 `ai_planner_hero.cpp` 依赖其中五个，所以必须先收。

**决定**：

1. **六个入口都接收 `Hero` 参数**：参考在 `reEvaluateIfNeeded` 里把英雄写进搜索上下文，之后的方法只读缓存。
   端口把英雄一路显式传下去，因为「缓存里哪一任英雄」在测试和未来的多英雄规划里都不可靠；单参
   `ReEvaluateIfNeeded(Hero)` 保留参考语义（军力自家算），双参版留给已知军力的调用方。
2. **`GetFogDiscoveryTile` 的 bool 改名为 `IsTerritoryExpansion`**：参考的注释写明第二项表示「雾就在这个瓦片
   旁边」，用于决定扩张还是探路，与坐船无关；原命名 `IsBoat` 会误导 B5 的调用方，行为未改。
3. **迷雾仍按两张表走**：`World.ClearFog` 只写 `MapFog`，寻路器只读 `Tile._fogColors`（ADR-061/ADR-079 的
   历史结果）。本批不改这条接线：`GetFogDiscoveryTile` 照参考「瓦片雾判相邻、`MapFog.CountFoggedAround`
   数可揭示格」，测试自己把两张表都写一遍，并在注释里说明原因。合并两张表要单独一条 ADR（会动 `World`、
   渲染与存档），不混在本批。
4. **次元门的空路由直接返回空表**：参考在 `_dimensionDoorMovementCost == 0 || _dimensionDoorSpCost == 0`
   处是 `assert`；端口返回空列表，避免调用方（B7 的 `HeroesCastDimensionDoor`）拿到死循环的落点序列。
5. **测试用竖长合成图**：`World.ComputeStaticAnalysis` 用列号乘宽当行首（与参考同款转置），宽度大于高度的
   合成图会越界。本批测试统一用 1×N 走廊，不修这个遗留问题（与 X1 无关）。

**后果**：`AIWorldPathfinder` 的 13 处 `NotImplementedException` 归零，`AIWorldPathfinderTests` 13 → 26 条；
随批补 `Hero.CanCastSpell`、`Hero.DimensionDoorUses`、`Difficulty.GetDimensionDoorLimitForAi`。
Game 测试 2991 → 3003，全量 4 530 条全绿；全解决方案 0 警告。`world_pathfinding` 的 AI 侧不再有占位，
B5 只剩对象价值表（X2）。


## ADR-197：被依赖文件先落接口骨架（`maps_tiles_render` 先行，G3 才动 `GameArea`） ✅


**背景**：G3 是 `gui/interface_gamearea.cpp`（1 053 行），但它的四个绘制入口——`Redraw`、`redrawOnlyFog`、
`renderTileAreaSelect`、`generateUltimateArtifactAreaSurface`——全部踩在 `maps/maps_tiles_render.cpp`
（1 327 行 / 1 118 非空行）上（`getTileSurface`、`drawFog`、`redrawBottomLayerObjects`、
`getMonsterSpritesPerTile` 等）。`docs/PLAN.md` 的「还差的主干」一直把这个文件记成 `maps` 目录最后一块
「地形绘制端口还没有等价物」。现有 `Game/AdventureMap/AdventureMapView.cs` 走的是 `Mp2Map`/`Mp2TileInfo`
的过渡渲染器（ADR-035/036），不是 `Maps::Tile` 部件模型的移植，不能当等价物。

**决定**（用户 2026-11-22 明确）：

1. **依赖顺序不破**：先把被依赖文件的**接口骨架**落下（签名 + 文档注释，方法体抛 `NotImplementedException`），
   再写调用方；调用方按参考逐行写调用，不写兜底，也不复制一份「先能跑」的近似实现。
2. **这条对目录内的单个文件同样成立**，不只对目录。今后凡是「已转目录里的文件踩到还没转的文件」，一律照此
   办理：被依赖文件先落骨架、调用方照参考写、实现随后补。已写进 `docs/PLAN.md` 总体原则第 6 条的「依赖先落接口」。
3. **落地**：`UI/Interface/TileRendering.cs` = `maps_tiles_render.cpp` 的 16 个入口（空瓦片 / 飞行幽灵 / 顶层部件 /
   雾 / 可通行性 / 四层地面部件 / 按 ICN 类型绘制 / 怪物·空船·矿守卫·英雄的四组精灵清单 / `getTileSurface`），
   名字、参数顺序、含义与参考一致，`Maps::` 限定符按端口命名空间去掉；`Game/Maps/ObjectRenderingInfo.cs` =
   `gui/ui_object_rendering.h` 的 DTO（参考本来就是 header 内实现，所以直接写全，不是桩，H 行
   「`+ui_object_rendering.h`」这一半提前完成）。

**记在骨架里的两处结构偏差**：

- **`TileRendering` 落在 UI 层**：参考文件在 `maps/`，但每个入口都收 `const Interface::GameArea &`，而端口的
  `GameArea` 属 `FHeroes2.UI`、`FHeroes2.Game` 不引用 `FHeroes2.UI`。`editor/` 阶段若要复用
  （`redrawPassable` 等），届时再定第二处落点，或改成显式几何参数——不在本批预先抽象。
- **`ObjectRenderingInfo.icnId` 用 `IcnType` 而非 `int`**：端口有 `ICN::` 枚举（`agg/icn.h` 的 1:1 表），参考的
  默认值 `-1` 对应 `IcnType.Unknown`（`IcnCatalog` 唯一没有文件名的成员）。

**后果**：G3 可以整段按参考写调用并能编译（运行时抛「未移植」，不会把过渡渲染器当结果）；`TileRendering` 的十个
匿名命名空间助手属实现细节，骨架里不出现死代码。构建 0 错误 0 警告，测试基线不变（4 945 绿）。

## ADR-198：联盟表按参考落地（MP2 头算、存档读、名册用） ✅

**背景**：参考把「谁是朋友」放在玩家名册上：`FileInfo::unions` 是一张按颜色索引的集合表，地图载入后
`player->SetFriends( fi.unions[index] | color )`（`players.cpp:325`）；MP2 头在「击败另一方」的地图上按胜利条件的
第 0 个参数把王国颜色切成两侧（`maps_fileinfo.cpp:341-371`），存档文件则整表存（`maps_fileinfo.cpp:664-675`）。
端口的 `Mp2FileInfo.PlayerUnions` 有字段但没有计算，`Players.Init` 只写「自己」，`Hero.IsFriends` 退回同色比较，
`CampaignData.UpdateScenarioGameplayConditions` 抛 `NotImplementedException`——同盟玩家互相视为敌人。

**决定**：

1. `Mp2FileInfo.PlayerUnions` 就是参考的 `unions`：`PlayerColor` 是位标志，每项是一个颜色集合；默认
   `PlayerColors.Ordered`（每人自己）。`Read` 的 `DefeatOtherSide` 分支按参数 0 切边，参数非 0 且小于王国色数才
   合法，否则整张图读不出来（参考 `return false`，端口抛 `BinaryFormatException`）。
2. `Players.Init` 写 `unions[i] | color`（自己也并进去，参考如此），`Hero.IsFriends` 改为问名册；名册没建好时
   保持参考的 false。
3. `CampaignData.UpdateScenarioGameplayConditions` 把「含 AI 颜色、又不是人类那一方」的项改成全部 AI 颜色，
   人类项不动。战役开局流程（`game_campaign.cpp`）还没接，等 P4 接线，不做临时调用。

**后果**：同盟地图上通行、战斗与 AI 敌友都走同一张表；「击败另一方」的地图以前被当成人人互相为敌，现在按地图
参数分边。`AiHeroMovementHandlerTests` / `AIWorldPathfinderTests` 改挂 `SettingsSingletonCollection`：AI 动作后要按
进程级的 `World.Current` 刷缓存，此前与换世界的测试并行跑会偶发「世界 0 格」。构建 0 错误 0 警告；测试 5 490 绿
（新增 3 条 MP2 头联盟测试、2 条战役并方测试）。

## ADR-199：漂流瓶走自己的动作分支，不混进取物 ✅

**背景**：参考里 `ActionToPickupResource()`（`heroes_action.cpp:752-793`）一处处理四种对象：散落资源、营火、木桶、
漂流瓶。前三者算钱进国库，漂流瓶只做 `showSignMessage( dst_index )`；四者在函数末尾一起播拾取音效、跑淡出动画、
`resetObjectMetadata()`。取物那条 ADR 曾把漂流瓶挡在 `IsPickupResource()` 之外，理由是「留言文本系统还没落地」，
于是英雄走到瓶子上返回 `HeroActionKind.None`——什么也不发生。

**决定**：

1. **数据层早就在**：`World.MapObjects` 里的 `MapSign` 同时承载 `OBJ_SIGN` 与 `OBJ_BOTTLE` 的信息块
   （`world_loadmap.cpp:624-629`），`MapTextCodec` 的中文/法文修复也早把漂流瓶串算进去了。缺的只是动作分支。
2. **单独一支 `HeroActionKind.Bottle`**，不借用 `Sign`：牌子可以反复读、瓶子读一次就没了，调用方（将来的界面）
   要据此决定「要不要播淡出、要不要重绘瓦片」，混成一个 kind 就分不出来了。
3. **`ActionToBottle` 自己清元数据**：`Tile.ResetObjectMetadata()`。参考把它写在函数末尾（所有分支共用），端口
   在每个分支自己的函数里收口，行为一致；淡出与移除主物件仍归界面（`ObjectFadingOutInfo`），与资源、骷髅一致。
4. **不 `SetVisited`、不动国库**：参考没有访问名册这一步，瓶子靠「元数据清空 + 物件移除」消失。
5. **AI 侧不改**：`AiToPickupResource()` 早已把 `Bottle` 当资源抹掉（不读留言，参考也不读）。

**后果**：英雄读到漂流瓶会拿到留言文本（`Payload` 是 `string?`）并清掉瓦片元数据；`HeroSignTests` 加两条
（直接调用 `ActionToBottle` 与走分发器），全量测试 5 492。界面消费 `HeroActionKind.Bottle`（弹留言框 + 淡出）仍待
App 的消息层，与 `Sign` 同一个缺口。

## ADR-200：配置文件读写照参考落进 `Settings`，键集合按端口已移植的设置项 ✅

**背景**：参考用 `fheroes2.cfg` 记住这台机器的偏好——音量、速度、全部开关、隐藏界面时四个窗口的位置、语言、
「第一次运行」标志（`Settings::Read()`/`Save()`，`settings.cpp:145-583`），格式由 `engine/tinyconfig.cpp` 的
`TinyConfig( '=', '#' )` 解析。端口 `Settings` 只有内存态，类注释里明写 `Read`/`Save`/`getOptionsString` 未移植，
连带 `IsFirstGameRun` 恒为 true（没人清、也没有文件可清），首运提示与语言选择窗都没法接。

**决定**：

1. **`TinyConfig` 单独成类**（`Game/System/TinyConfig.cs`），读文件与解析分开：`LoadFile` 碰磁盘，`Parse` 只吃文本。
   解析规则照参考：键小写 + 去首尾空白 + 连续空白压一个、重复键留第一个（`emplace` 语义）、无分隔符的行跳过；
   `IntParams` 整串必须是数字否则 0（`from_chars` 不接受尾随垃圾），`PointParams` 只认 `[ x, y ]`，
   `ResolutionParams` 先认 `宽x高:宽x高`，再认旧格式 `宽x高x倍数`，倍数为 0 或乘出溢出时回退。
2. **配置读写放 `Settings.Configuration.cs`**（partial，与 `Settings.Persistence.cs` 并列）。`Persistence` 那份是
   跟着一局游戏走的五个字段，配置文件是这台机器的偏好，读档不覆盖，两者不混。
3. **路径归宿主**：参考的 `Save()` 自己拼 `GetConfigDirectory( "fheroes2" )`，端口没有 `dir.cpp`，所以 `Save(string)`
   收完整路径；热键文件已是这个分工（`GameHotKeys` 收路径），保持一致。
4. **三个键按「有状态才写、有状态才读」处理**：`videomode` 与 `game window position` 属于 UI 层的分辨率与窗口，
   通过 `Save`/`GetOptionsString` 的可选参数写出去，`Read` 读到也跳过；`debug` 属于未移植的日志层，不写不读。
   其余键全部照参考的键名、默认值与注释写，两份文件可以并排 diff。
5. **`first time game run` 只认 `off`**：缺键、`on`、其它值都不动标志——参考只在读到 `off` 时 `resetFirstGameRun()`。

**后果**：`Settings` 能把自己完整写出去再读回来（`SettingsTests` 的往返用例逐字段断言）；`TinyConfig` 的边界
（缺文件、注释、重复键、非数字、溢出的旧分辨率）各有测试，全量 5 506 绿。**启动时读、退出时写尚未接线**：
分辨率/全屏/隐藏界面位置的真正生效要等 UI 层消费这些设置，与语言选择窗、`BaseInterface` 的淡入一起做（P6）。

## ADR-201：配置文件接进启动流程，加 `--config`/`--no-config`，语言以 `--lang` 为先 ✅

**背景**：ADR-200 落了 `Settings.Read/Save`，但没人调；参考在 `Game::Init()` 的 `initConfig()`
（`game_init.cpp:217-230`）里读它，读不到就立刻写一份默认的，之后由设置窗、热键、雷达拖动各处
`conf.Save()` 逐步落盘。端口要接这条链，同时不能让它干扰像素基线。

**决定**：

1. **路径是纯函数**（`Game/System/ConfigurationFile.cs`）：`DefaultPath( appData )` 拼
   `<appData>\fheroes2\fheroes2.cfg`（对应 `GetConfigDirectory( "fheroes2" )` + `Settings::configFileName`），
   `Resolve( appData, explicit )` 让显式路径优先，`EnsureDirectory` 对应 `MakeDirectory( configDir )`。
   「`%APPDATA%` 在哪」留给宿主（`Environment.GetFolderPath`），Game 层不读环境变量，测试直接喂临时目录。
2. **两个新开关**：`--config <file>` 与 `--no-config`。参考没有它们（只在固定目录里找），端口加是因为同一个 exe
   既跑游戏又跑像素基线：开发机上的 `hide interface = on`、`system info = off` 会把基线搅乱。五个
   `Verify*Baseline.ps1` 一律带 `--no-config`，基线从此与「这台机器有没有配置文件」无关。
3. **读不到就写默认**：`GameHost.LoadSettings()` 在 `Read` 失败或缺文件时 `EnsureDirectory` + `Save`，与
   `initConfig` 的 else 分支一致；写不出去（只读目录）只降级成「偏好不保存」，客户端照常跑。
4. **保存在退出与语言变更两处**：参考在各修改点各自保存，端口没有那么多修改点（设置窗只有语言行），统一在
   `Loop()` 结束与 `MainMenuScene.LanguageChanged` 各写一次，是参考所有保存点的超集；`videomode` 写
   `<640x480>:<当前窗口>`。
5. **语言优先级 `--lang` > 配置文件 > 英文**，且 `StartLanguage` 要把 `--lang` 单独再取一次——`--no-config` 时
   `LoadSettings` 提前返回，配置文件里的语言根本没被读进来，而基线正是「`--no-config` + `--lang zh_CN`」。

**后果**：语言一旦在设置窗里改过就会被记住，`first time game run` 留在文件里等首运 UI 消费（§7 的 P6 剩余项）。
顺带发现五张像素基线从 2026-11-14 起就落后（主菜单右下角的 Editor 招牌没进基线，恒差 9 867 像素），已重建并
复验 `identical`，记在 `tests/baselines/README.md` 的「2026-11-24 的重建」。
**没做**：首运的语言选择窗与欢迎消息、`videomode`/`fullscreen`/隐藏界面位置的生效、`IsFirstGameRun` 的清除。

## ADR-202：首运的窗口做成场景级模态，触发条件是「真的读到了配置文件」 ✅

**背景**：参考 `game_mainmenu.cpp:265-300` 在进事件循环之前阻塞地跑 `selectLanguage`、问候消息、分辨率选择，
最后清旗标并保存。端口的主菜单是帧驱动的场景，`GameHost.RunModal` 那套阻塞循环只适合 `showStandardTextMessage`
这类一条命的消息；把整个首运塞进去，`--screenshot` 和 `--smoke-frames` 就没法再拍照和退出（窗口在等输入）。

**决定**：

1. **场景级模态**：`MainMenuScene.BeginFirstRun()` 把 `LanguageSelectionDialog` 放在菜单上，输入路由、绘制、
   `HasModalDialog` 都沿用设置窗那一套；问候用 `MessageBox` 加一个 OK 按钮，同样由场景收键。帧循环照跑，
   所以自动化开关仍然可用。参考里「窗口下方是菜单」的视觉顺序也因此保留。
2. **触发条件是 `_configFile is not null && Settings.IsFirstGameRun`**，而不是只看旗标：`--no-config` 时
   `LoadSettings` 连文件都没碰，`IsFirstGameRun` 停在默认 `true` 上；只看旗标的话，五个像素基线和所有脚本
   都会停在语言选择窗上。旗标是配置文件的产物，就跟着配置文件走（与 ADR-201 的 `--no-config` 同一条线）。
   `--open-settings`/`--open-scenario` 先占了窗口时 `BeginFirstRun()` 让路，旗标留到下一次启动。
3. **问候只写欢迎段**：参考的三段里，「请选择游戏分辨率」后面紧跟 `Dialog::SelectResolution()`，「触摸输入」
   跟着触摸检测；端口两样都没有（`videomode` 只写不读，也没有触摸输入），照抄就是空承诺。少写两段而不是
   写一段假的，差异记在 PROGRESS §6.6。
4. **CANCEL 帧进 `SettingsArtwork`**：`SYSTEM.ICN` 的第 3、4 帧是 CANCEL 按钮，语言窗是第一个真正需要它的窗口；
   档案里缺帧时 `HasCancel` 为假，语言窗退回只画 OK，不崩。
5. **语言落地**：菜单在既无 `--lang`、配置里也没有 `lang` 时会自己落到系统语言上，宿主在 `CreateScenes` 里把这个
   结果记进 `Settings`，否则文件里的 `lang` 空着、界面却是中文，下一次启动又得重新猜。

**后果**：首运的语言窗与问候能出能关，`--config` 脚本要先写 `first time game run = off` 才不会被窗口拦住（文档已写）。
**验证边界**：合成点击一轮只有一次（`--left-click` 定在第十帧、`--cursor` 只有一个），语言窗 → 问候的截图与日志
都抓到了，但「点问候的 OK 之后清旗标」没有端到端截图；那一步由 `Settings.ResetFirstGameRun()` + `Save` 的持久化测试
（`SystemSettingsTests.ASavedResetFirstGameRunStaysClearedInTheFile`）加四行接线守住，要端到端就得让脚本能连点两个位置。
**没做**：分辨率选择窗与 `videomode` 的生效、触摸输入、`MainMenu` 退出时的窗口位置保存。
（2026-11-24：`videomode` 与窗口位置见 ADR-204，分辨率选择窗见 ADR-205，只剩触摸输入没人用。）

## ADR-203：会话难度用 `--difficulty` 先落地，起始资源与城堡默认建筑都跟随 `Game::getDifficulty()` ✅

**背景**：参考在剧本信息屏上用难度光标选难度（`game_scenarioinfo.cpp` 的 `ActionCursor` 分支），选中的那一档
进 `Game::getDifficulty()`，之后 `Kingdom::Init()` 拿它发起始资源（`kingdom.cpp:103`）、`Castle::_setDefaultBuildings()`
拿它抽二级兵营（`castle.cpp:542`）。端口还没有剧本信息屏（没有 `NGHSBKG`、没有难度图标），但 `Kingdom.GetStartingResources`
与 `Castle.SetDefaultBuildings` 都已经按难度分表，缺的只是「谁把这一档告诉它们」。在此之前 `World.LoadMap` 拿地图头里
的 `Mp2FileInfo.Difficulty` 顶上，这跟参考口径不一致：地图头那一档只参与 `Game::GetRating()`。

**决定**：

1. **`--difficulty <easy|normal|hard|expert|impossible|0..4>`** 作为玩家的选择的过渡入口，解析在
   `CommandLineOptions.ParseDifficulty`，非法值在参数解析阶段就报错。它是会话级开关，**不写进配置文件**
   （与 ADR-201 的 `--lang` 同一条线：命令行优先且不落盘），`GameHost.ApplySessionOptions()` 把它写进
   `Settings.SetGameDifficulty`，日志留一行 `Difficulty set to ... on the command line.`。
2. **`World.LoadMap( map, difficulty )` 记下这一档**：新增 `World._difficulty`（参考的 `Game::getDifficulty()`
   在端口没有对应的全局对象，装载时记在世界里），`CreateMapKingdoms` 用它发起始资源，`ReadCastleBlock` 用它抽
   城堡默认建筑（`Castle.LoadFromMP2` 的 `difficulty` 参数）。
3. **不传就退回地图头**：`LoadMap( map )` 保持老口径（`map.Info.Difficulty`），测试与工具不必改；`Reset()` 把
   它复位成 `Normal`，空世界不再带着上一张图的答案。
4. **`AdventureMapScene` 开局时传 `Settings.Get().GameDifficulty`**，所以 `--difficulty` 一路贯通到资源与城堡。

**后果**：`--difficulty impossible` 下起始金币为 0、任何城的二级兵营都不会出现；`--difficulty easy` 下 75% 的城
会带二级兵营。地图头难度退为评分口径，与参考一致。
**验证**：`TheDifficultyPassedInDecidesTheStartingResources`（资源）与
`TheDifficultyOfTheGameDecidesTheDefaultBuildingsOfATown`（城堡，八次装载 + 地图头 Easy / 会话 Impossible 对拍）；
全量 5515 测试绿；`--difficulty impossible --enter-map --screenshot` 冒烟通过（地图头 Hard、会话 Impossible 同时存在）。
**没做**：剧本信息屏的难度光标、`NGHSBKG`、难度图标、对手/职业/评分绘制——`--difficulty` 只是这些界面落地之前的过渡口子。

## ADR-204：窗口状态（`videomode` / `fullscreen` / 窗口位置）启动恢复、退出写回 ✅

**背景**：参考的 `DisplayInitializer`（`game_init.cpp:70-99`）在窗口创建后按配置恢复三样东西——全屏状态
（`:89`）、窗口位置（`:96`，`display.setWindowPos( conf.getSavedWindowPos() )`）、分辨率（`:98`，
`display.setResolution( conf.currentResolutionInfo() )`）；`MainMenu` 关闭前把窗口位置记回去（`game_mainmenu.cpp:239-244`），
`conf.Save()` 写文件时也是现场问 `Display::instance()`（`settings.cpp:427-433`）。端口此前只写不读：配置文件里
的 `videomode`、`game window position` 被 `Read` 跳过，`fullscreen` 读了却没人用，启动永远是 2 倍窗口、永远窗口化。

**决定**：

1. **读进设置**：`Settings.CurrentResolutionInfo`（默认 `640x480:640x480`，同 `settings.cpp:95`）与
   `Settings.SavedWindowPosition`（默认 `(-1, -1)`，意思是「没记过」），`Read` 见键才改，与其余键一致。
2. **恢复交给宿主**：`GameHost.CreatePresenter()` 按参考的次序来——全屏（`--fullscreen || Settings.IsFullScreen`）
   随 `SdlPresenter` 构造一起设好，窗口位置作为构造的可选参数在 `SDL.ShowWindow` **之前**应用（否则先闪一下
   默认位置），最后 `SetResolution` 应用 `videomode` 的窗口尺寸。
3. **只应用窗口那一半**：参考的帧缓冲可以换尺寸（`Display::setResolution` 重建渲染目标），端口的场景全部按
   640x480 画；`videomode` 的游戏尺寸不是 640x480 时打一行警告、只应用窗口尺寸，而不是抛异常。等场景与界面
   支持别的逻辑尺寸再放开。
4. **命令行压过文件**：`--scale` 从「默认 2」改成可空——给了就不看文件里的窗口尺寸（基线脚本靠默认 2 倍与
   `--no-config` 固定窗口）；`--fullscreen` 只会打开全屏。文件缺键时仍是 2 倍。
5. **写回的是现状**：`SaveSettings()` 现场问窗口（`videomode = 640x480:当前窗口`、`game window position = 当前坐标`、
   `fullscreen = 当前状态`），不把读进来的值原样写回；`F11` 切换后同步 `Settings.SetFullScreen`，文件里才不会
   永远停在开窗时那一档。`SdlPresenter` 相应加了 `WindowPosition` 读取与构造参数。

**后果**：改过窗口大小的玩家下次启动就在自己习惯的尺寸里；全屏状态、窗口位置都能记住；配置文件里三项与窗口实际
状态一致（此前的写入是「猜」的）。
**验证**：全量 5 517 测试绿（新增两条设置测试）；三组 `--config` 冒烟（1600x1200 生效并写回、800x600 游戏尺寸
只取窗口那一半并警告、`--scale 3` 压过文件的 1600x1200 写成 1920x1440）；菜单与设置像素基线仍 `identical`。
**没做**：分辨率选择窗（首运那次挑分辨率，`Dialog::SelectResolution`）、问候里的分辨率段、隐藏界面四个窗口位置的
生效；`videomode` 的游戏尺寸仍锁 640x480。另：冒险地图基线在本机不过，已用 HEAD 的 worktree 复现，与本条无关
（PROGRESS §7.5）。（2026-11-24 后续：首运的分辨率窗与问候里的那一段见 ADR-205。）

## ADR-205：首运的分辨率选择窗，以及「帧缓冲 + 窗口」的分辨率配对 ✅

**背景**：参考的首运（`game_mainmenu.cpp:265-300`）在问候之后调 `Dialog::SelectResolution()`，窗口列出
`engine().getAvailableResolutions()`（`dialog_resolution.cpp`）。端口的 `ResolutionDialog` 早就转好了（图形/系统选项页
在用），但没人调得动它：`IDisplayControl.GetAvailableResolutions()` 返回的是「游戏尺寸 = 窗口尺寸」的行，而
`SdlPresenter.SetResolution` 只认 640x480 的帧缓冲，挑一行就抛 `ArgumentException`；首运也缺这一步。

**决定**：

1. **统一成「帧缓冲 + 窗口」的配对**：`SdlDisplayControl.GetAvailableResolutions()` 用当前帧缓冲的尺寸当
   `GameSize`、显示模式当 `ScreenSize`。参考的现代版本让游戏尺寸跟着窗口走；端口所有场景按 640x480 画
   （ADR-204），所以只有窗口那一半能变。这样同一个列表既喂首运、也喂图形选项页，`SetResolution` 不再拒收。
2. **首运那一步照参考的顺序与文案**：语言选择 → 问候（欢迎段 + 「请选分辨率」段）→ 分辨率窗口 → 收尾（清旗标、
   保存）。问候里的触摸段仍不写，端口没有触摸输入。
3. **场景级模态，不阻塞**：参考的窗口是阻塞的，端口沿用 ADR-202 给语言窗定的做法——`ResolutionDialog` 挂在
   `MainMenuScene` 上，`Update` 每帧把 `LocalEvent.Current` 喂给 `ProcessEvents`。这样 `ListBox` 的滚动条拖动、
   双击、键盘全不用重写，`--smoke-frames`/`--screenshot` 也照常跑得下去。
4. **预选与回退按参考**：当前窗口所在行预选；当前窗口不是某个显示模式（端口开窗默认是 640x480 放大 2 倍，
   1280x960 往往不是一个模式）时停在第一行，参考同样如此。
5. **开不出来就跳过**：缺 `SCROLL.ICN`/`ADVBORD.ICN`、没有显示层、没有可用行，都只打警告并直接收尾，首运不会卡住。

**后果**：首运真的会让玩家挑窗口尺寸，挑中的写进 `videomode`；图形选项页里挑分辨率不再抛异常。
**验证**：全量 5 519 测试绿（`ResolutionDialogTests` 新增预选/回退两条）；首运冒烟（临时补丁把首运推进到该窗口）
日志 `resolution chooser open, 13 window(s), current 1280x960`、注入 Enter 后 `window resolution 2560x1600` 且
`videomode` 写成 `640x480:2560x1600`、`first time game run` 清掉；菜单/设置/剧本三张像素基线 `identical`。
**没做**：右键解释弹窗在首运里被丢弃（ADR-206 已补）；首运这一步的脚本点击（窗口读 `LocalEvent`，
合成的 `--left-click` 只进场景接口）。

## ADR-206：首运分辨率窗的右键解释，用第二层场景级消息框 ✅

**背景**：`ResolutionDialog` 早就把右键变成 `DialogPopup`（行文本 + 缩放说明），参考在
`Dialog::SelectResolution()` 的阻塞循环里用 `showStandardTextMessage( ..., Dialog::ZERO )` 弹出来
（`dialog_resolution.cpp:124-145`、`:291-296`）。ADR-205 把窗口挂进首运后，`TakePopup()` 被丢掉，右键没有反应。

**决定**：

1. **不改成阻塞的 `MessageBox.Show`**：首运的三个窗口都按 ADR-202/ADR-205 的分帧做法（`Update` 每帧喂输入）。
   中途起一层阻塞循环会让 `--smoke-frames`/`--screenshot` 的帧计数与事件批错位，也让「场景级模态」出现两种实现。
2. **消息框当第二层场景级模态**：`MainMenuScene._resolutionTip` 由 `MessageBox.Create` 拼出（OK 按钮，按钮素材复用
   `ResolutionArtwork.Buttons`）。`SetCursor`/`SetLeftButton`/`Select` 先让给它，`HasModalDialog` 计入它，
   `DrawFrame` 在分辨率窗之后画它。
3. **一帧只关一层**：`_skipResolutionInput` 让 `DriveResolutionPicker` 在「刚打开分辨率窗」或「刚关掉解释框」的那一帧
   不把 `LocalEvent` 喂给列表。否则关闭问候的 Enter 会直接确认分辨率窗，关闭解释框的 Esc 会直接取消它。
4. **文案照参考**：按钮的右键解释同时对齐原文（`Okay` → `Click to apply the selected resolution.`、
   `Cancel` → `Exit this menu without doing anything.`）。

**后果**：首运里右键任意一行（或 OK/Cancel 按钮）弹出与参考同文案的解释框，Enter/Esc/点的 OK 都能关；
列表在关闭的那一帧不会收到同一个按键。
**验证**：见 PROGRESS §6.10（5 521 测试绿、首运冒烟截图、菜单/设置/剧本三张基线 `identical`）。
**没做**：首运那一步的脚本化点击（窗口读 `LocalEvent`，合成的 `--left-click` 只进场景接口）。

## ADR-207：冒险地图基线按「参考行为」更新，并给随机源加 `--seed` ✅

**背景**：`adventure-map.png` 长期与 HEAD 的渲染差 165 198 个像素（PROGRESS §7.5 记为「已知未解，查清前不要 `-Update`」）。
worktree 二分：基线在 `11e6a51` 上 `identical`，`748904e`（冒险场景改为整屏交给 `Interface::AdventureMap::RedrawInterface`、
删掉场景自绘的状态行）之后才对不上。逐项核对后三条差异都是**正确行为**：

| 差异 | 参考 | 端口 |
| --- | --- | --- |
| 视口不居中在「最近的城堡」 | `game_startgame.cpp` 开局 `ResetFocus(FIRSTHERO)` → `interface_base.cpp:76-79` 的 `SetFocus` 总是 `SetCenter(hero)` | `AdventureSession.Start` 成功就 `SetFocus(actingHero, false)`（第二参数是「保留滚动条位置」，不是「不居中」） |
| 雷达只有零星亮点 | 雷达只画已探索区域 | 迷雾生效（`748904e` 修了 `Radar._hide` 默认值与迷雾色） |
| 状态栏是「19 精灵 + 4 兵」而不是日期 | `SetFocus` 里 `statusPanel.SetState(STATUS_ARMY)` | 同左；日期是旧的场景自绘 `StatusPanelPainter` 的产物，那个类已删除 |

**决定**：
1. **更新基线**，不把正确的参考行为当成回归去迁就旧图。
2. **补 `--seed <n>`**：更新后同一脚本连跑两次仍差 27～40 个像素，位置都在状态栏部队那一格——地图载入时英雄
   初始部队数量由 `Rand` 抽（OS 播种）。新增 `Rand.SeedThread(seed)` 与命令行 `--seed`（在
   `CreateScenes()` 之前应用），基线脚本带 `--seed 1`。参考没有这个开关（它的测试不做逐像素断言），这是端口
   像素回归设施的接缝，不改默认行为。
3. **不把随机写进 `Settings`**：种子是「本次运行」的东西，跟 `fheroes2.cfg` 无关。

**后果**：冒险地图基线可复现（连跑三次 `identical`），四张像素基线全部 `identical`；`--map` 的截图/冒烟从此确定。
**验证**：见 PROGRESS §7.5 结案段（5 523 测试绿，`RandTests` 两条）。
**没做**：多线程（AI）下的随机复现——`SeedThread` 只覆盖调用线程，AI 开工后各线程仍各自 OS 播种。

## ADR-208：`--right-button` 也为冒险地图按住右键（提示框基线） ✅

**背景**：重建 `quick-info.png` 时发现它其实是「没有提示框的地图」——与 `adventure-map.png` 逐字节相同。
`--quick-info` 只在 `GameHost.CreateScenes()` 里调一次 `ShowQuickInfoAt`，而
`AdventureMapScene.UpdateQuickInfo` 每帧检查 `LocalEvent.IsMouseRightButtonPressed`，没有按住就 `_quickInfo = null`，
于是框活不过第一帧。参考的 `Dialog::QuickInfo()` 是按住右键期间的阻塞循环，所以「框跟着松手消失」本身是对的，
错的是钩子：脚本从没真的按住过按钮。

**决定**：
1. **不改场景语义**，改钩子：`--right-button` 在 `GameHost.Loop` 里把「按住右键」推进 `LocalEvent`
   （`_le.OnMouseButton(true, Right, cursor)`，每帧在合成光标之后）。菜单与城镇屏各有自己的 `SetRightButton`，
   冒险地图读全局事件状态，所以要补在事件这一层；只在冒险地图场景存在时生效，不影响菜单基线。
2. **基线脚本同时带 `--quick-info` 与 `--right-button`**：前者负责「开一次 + 日志」，后者负责「按住」。
   CI 也补了这一条与 `-AdventureOptions`。
3. 不改 `UpdateQuickInfo`：它是参考语义（松手收框），为截图放宽会掩盖真问题。

**后果**：`quick-info.png` 重新带框（「Ore Mine」，`MineText.GetMineName` 按矿的资源取名，与参考
`showMineInfo` 一致）；四张冒险地图基线连跑两次 `identical`。`--right-button` 从此在冒险地图上也能用于
其他「按住才出现」的提示（状态面板、雷达的右键说明）。
**验证**：`VerifyAdventureMapBaseline.ps1 -QuickInfo` 连跑两次 `identical`；其余九张基线不受影响（全部 `identical`）。
**没做**：右键按住期间的拖拽卷屏（参考 `MouseRightClick` 的拖动分支）还没有端口的截图用例。

## ADR-209：`--left-click-at` 多点脚本点击与首运端到端冒烟 ✅

**背景**：首运是纯点击流程（语言窗 → 问候 → 分辨率窗），但 `--left-click` 固定在第十帧、只认一个 `--cursor`，
一次运行只能点一下，所以 §6.6/§6.9/§6.10 一直记着「首运那一步的脚本点击没做」，之前那次冒烟靠临时补丁直接跳过
前两个窗口。缺口不在场景，在钩子：脚本没法在一轮里连点四个位置。

**决定**：
1. **新增 `--left-click-at <frame:x,y[;frame:x,y…]>`**：一次运行里按帧重放任意多个左键点击，记录成
   `ScriptedClick(int Frame, Point Point)`。帧从第一帧起数（与 `--smoke-frames` 同口径），必须 ≥1 且**严格递增**，
   否则在参数解析阶段抛 `ArgumentException`——乱序的点击没有对应的玩家操作，静默重排只会掩盖脚本写错。
2. **`--left-click` 成为它的单点特例**：构造时若没有 `--left-click-at`，`--left-click` 合成
   `ScriptedClick(LeftClickFrame, --cursor)`；已有的单点用法与基线脚本不受影响。
3. **点击仍在主循环里重放**（事件批之后、场景 `Update` 之前），不在 `CreateScenes` 里——后者会跳过新场景的头几帧，
   而首运的每个窗口都是打开后先画一帧再收输入。
4. **分辨率窗单独走 `LocalEvent`**：首运的分辨率窗（`ResolutionDialog`）的列表滚动、双击、右键解释都读全局事件，
   而菜单/城镇屏读各自的 `SetLeftButton`。所以菜单那一路照旧（`SetCursor` + `SetLeftButton` + `HandleMenuAction`），
   另在分辨率窗开着时把点击推进 `_le.OnMouseButton`。
5. **按下与释放分两帧**：一次物理点击本来就跨两帧，列表与按钮行都在**释放**时触发；按下帧记
   `_pendingClickRelease`，下一帧先发释放再处理本帧点击。同帧按下+释放对列表完全无效（本轮踩过这个坑）。
6. **冒烟脚本 `tools/scripts/SmokeFirstRun.ps1`**：写一个仍带 `first time game run = on` 的临时配置，四击
   `10:236,380`（语言 OKAY）、`20:320,322`（问候 OKAY）、`30:300,114`（分辨率列表第一行）、`40:236,356`
   （分辨率 OKAY），然后按日志五个标记逐步核对；不带 `-KeepRunning` 时把配置恢复成首运状态。坐标取自窗口布局
   （两个窗的内容都是 345x306 居中），布局改动要同步改脚本里的数字。
7. **接进 CI**：`ci.yml` 末尾加一步 `Walk the first run with scripted clicks`，条件同其他数据依赖步骤
   （`vars.FHEROES2_DATA != ''`），跑 `SmokeFirstRun.ps1 -SkipBuild`。

**后果**：首运从语言窗到旗标清除有了不靠补丁的端到端冒烟；任何一步的输入接线断了，脚本会在对应标记上报缺失。
`--left-click-at` 也可以用于其他多步点击流程（设置页、剧本屏），但城镇屏内部仍只有「按下+释放同帧」的出口，
多点连点在那里的语义未验证。

**验证**：构建 0 错误 0 警告；全量测试 5 523 绿；十张像素基线一次性复验全部 `identical`；
`SmokeFirstRun.ps1 -SkipBuild` 输出 `the first run walked from the language picker to the cleared flag, match`；
`--left-click` 单点回归日志仍是 `language 'en' -> 'en'.` + `greeting shown.`；日志里没有 `TEMP-` 残留。

**没做**：问候里的触摸段（端口没有触摸输入）、隐藏界面的四个窗口位置。

## ADR-210：触摸输入、问候里的触摸段与隐藏界面面板的落位 ✅

**背景**：P6 就剩两项。其一，问候少一「两指右键」段（ADR-202 记的差异），根子是端口没有触摸输入：
参考在 `onTouchFingerEvent`（`localevent.cpp:1358-1430`）里自己把手指翻译成左右键，并关掉 SDL 的触摸合成，
所以那一段讲的手势真的能用。其二，隐藏界面模式下四个面板（雷达/按钮/图标/状态栏）从配置里读出的位置
（§6.8 读取、`AdventureMap.Reset` 应用）没人写过：拖面板时只画轮廓，松手丢掉，`pos_*` 永远是默认 `-1`，
读取那一半等于死代码。

**决定**：
1. **触摸状态进 `LocalEvent`**：新增 `TouchFinger`（Down/Up/Motion）、`TouchEmulation` 与两根手指的 ID 状态，
   `OnTouchFinger` 逐行照参考：第一指驱动光标并仿真左键；第一指还在时第二指按下仿真右键，抬起第一指**不**释放
   右键（这就是「按住物件，再看别处」）；第三指、以及不在屏上的手指事件一律忽略；每次仿真都置 `MOUSE_TOUCH`，
   `IsMouseEventFromTouchpad` 从此能真为 `true`。
2. **返回仿真结果**：参考的按钮直接读 `LocalEvent`，方法不必返回；端口的场景由宿主送输入，所以 `OnTouchFinger`
   返回 `None/Motion/Left|Right Pressed/Released`，宿主据此调 `MoveSceneCursor` 或 `DispatchButton`，
   场景看到的东西与鼠标事件相同。
3. **关掉 SDL 的合成**：启动 `SDL_Init` 之后设 `MouseTouchEvents = 0`、`TouchMouseEvents = 0`，否则一次点按会以
   「合成鼠标 + 手指事件」两条路各来一遍。手指坐标按窗口归一化，先乘 `OutputSize` 再走 `ToCursor`。
4. **检测**：`SDL_GetTouchDevices`（绑定返回托管数组，无需释放），结果交给 `MainMenuScene.TouchInputAvailable`。
   SDL 只在设备被用过之后才报告，所以首运仍可能看不到触摸屏——参考同样如此，段落只是少写一段。
5. **问候段**：`WelcomeTouchId` 照抄参考文案，按 `欢迎 → 触摸 → 分辨率` 的顺序拼；分辨率段照写（桌面版永远不是
   手持设备）。
6. **`--no-touch`**：本机有设备上报时，问候会因为多一段而变高、OKAY 下移，`SmokeFirstRun.ps1` 的固定坐标就会点空
   （本轮实际踩到，冒烟停在语言窗之后）。这个开关只关「检测 + 段落」，不关手指翻译，给脚本一个稳定的布局；
   首运冒烟已带上它。
7. **面板落位**：`BorderDrag` 带上被拖的 `Owner`，并新增 `Drop(cursor)`（参考松手时的
   `SetPos( mp.x - ox, mp.y - oy )`）；`AdventureMapScene.PumpDrag` 在释放帧调它。面板的 `SetPos` 本就会夹进屏幕
   并 `SavePosition()` 进 `Settings`，退出时随配置写出，于是「读位置 → 拖 → 落盘 → 下次启动复位」闭环。

**后果**：触摸屏上单指即鼠标、两指即右键，问候里的手势说明是真的；隐藏界面面板拖完会停在新位置并记住。
`Drop` 对没有 `Owner` 的轮廓（只画轮廓的测试）什么都不做。

**验证**：构建 0 错误 0 警告；全量测试 5 529 通过（`LocalEventTests` 新增 4 条触摸语义，`BorderWindowTests`
新增 2 条落位/无主轮廓）；十张像素基线一次性复验全部 `identical`；`SmokeFirstRun.ps1 -SkipBuild` 通过；
本机（有触摸设备上报）不带 `--no-touch` 截的问候图是三段（欢迎、触摸、分辨率），带 `--no-touch` 时回到两段。

**没做**：真机两指手势未实际点过（本机只报告了设备，没有手指输入）；捏合/滑动等指针手势（参考也只在未移植的
battle 里读 `IsMouseEventFromTouchpad`）；手持设备的问候/分辨率差异。
