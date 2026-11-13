# 进度记录（PROGRESS）

## M0 — 解决方案与最小可运行引擎 ✅

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `global.json` / `Directory.Build.props` / `Directory.Packages.props` | ✅ | SDK 10.0.101（rollForward latestFeature，本机解析到 10.0.402）；集中管理包版本 |
| 解决方案（`FHeroes2Sharp.slnx`） | ✅ | 9 个项目，`.slnx` 为 .NET 10 SDK 默认格式 |
| `FHeroes2.Core` | ✅ | 几何（Point/Size/Rect/PresentationViewport）、`Pcg32`、`LittleEndianReader/Writer`、`Palette`、`PixelBuffer`、`ScreenBuffer`、`ImageOps`、`GameLog`、`FrameTimer`、`BuildInfo` |
| `FHeroes2.Assets` | ✅ | `AssetArchive`（AGG）、`AggFileNameHash`、`AssetLocator`、`AssetBundle` |
| `FHeroes2.Rendering` | ✅ | `IScreenPresenter`、`ITextRenderer`/`TextStyle`/`TextMetrics` |
| `FHeroes2.Game` / `FHeroes2.UI` | ✅（占位） | 仅层边界标记类型，M2/M3 起实现 |
| `FHeroes2.Client.Desktop` | ✅ | SDL3 宿主、`SdlPresenter`、`SdlTextRenderer`（SDL3_ttf）、诊断场景、命令行、截图 |
| `tools/FHeroes2.AssetProbe` | ✅ | `list` / `info` / `extract` / `sprites` / `tiles` 五个子命令 |
| 测试 | ✅ | 174 个（Core 117 / Assets 57，全部通过；支持真实数据时含 1434 条哈希与 ICN/TIL 摘要集成校验） |
| AOT / 裁剪 | ✅ | 产品项目开启 `IsAotCompatible` 等分析器（警告即错误），0 条 IL 发现；**不发布** AOT（ADR-011） |
| 文档 | ✅ | PLAN / PROGRESS / ARCHITECTURE / DECISIONS / REFERENCE / MISC / OTHER / README |

### M0 验证记录

```
dotnet build FHeroes2Sharp.slnx      → 0 error / 0 warning（-warnaserror + TreatWarningsAsErrors）
dotnet test  FHeroes2Sharp.slnx      → Core: 40 passed / 0 failed；Assets: 14 passed / 0 failed
AssetProbe info                      → Heroes2.agg: 1434 entries / 43,324,306 bytes 负载
                                       .ICN 846, .82M 309, .BIN 147, .BMP 109, .XMI 17, .TIL 3, .FNT 2, .PAL 1
AssetProbe extract KB.PAL            → 768 字节；最大分量 63 → 确认 6 bit VGA 调色板
客户端 --smoke-frames 300            → 退出码 0，末帧 77.8 FPS，无异常
客户端 --screenshot tools-output/m0.png → 640×480 PNG，调色板色带 + 中文文本 + 2x 呈现正确
AOT 分析器探针                        → 临时加入 Type.GetType 后构建以 error IL2057 失败；
                                       移除后 --no-incremental 重建 0 warning / 0 error
```

`docs/` 之外的实际画面证据：`tools-output/m0.png`（该目录已在 `.gitignore` 中忽略）。

### 呈现层切换为 SDL3 的验证记录

```
dotnet build FHeroes2Sharp.slnx            → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx            → Core: 117 passed；Assets: 57 passed（合计 174）
客户端 --smoke-frames 40 --scale 2         → 退出码 0，末帧 111~118 FPS（vsync 未限速的合成速度）
客户端 --screenshot artifacts/smoke/*.png  → 640×480 PNG：调色板色带 + 中文叠加文本，2x 整数倍呈现
字体选择                                    → simsun.ttc（12 px 内嵌点阵，CJK 与拉丁混排均锐利）
```

### 关键数据校正（相对早前侦察笔记）

* `heroes2.agg` 条目数是 **1434**（不是 154——早期脚本读取头部时字节序累加写错）。
  该数字现由 `AssetArchive` 打开时对 **1434 个真实名称哈希**逐一校验得出，因此同时验证了
  `AggFileNameHash` 的移植正确性。
* `heroes2x.agg` 条目数是 **52**。
* `KB.PAL` 是 **6 bit VGA** 调色板（分量 0..63），需 `<< 2` 归一化，并按 `setGamePalette`
  复制水/岩浆循环色（231–233、235 → 246–249，214–217 → 250–253）。
* 原版归档里有 `BIGFONT.FNT` / `SMALFONT.FNT`（各约 17 KB），但参考实现从不读取它们；
  界面字体实际来自 `FONT.ICN` / `SMALFONT.ICN` 精灵表（ADR-018）。

### M0 已知偏差 / 待办

1. 诊断场景每帧整屏重绘（`InvalidateAll`）；脏矩形机制已实现但留到 M1 真正使用。
2. `ITextRenderer` 目前只有「度量 + 后端名」；文本绘制方法在 M2 引入位图字体后端时统一。
3. 诊断界面直接写死中英文字符串（回退规则：字体支持 CJK 则中文，否则英文）；`.mo` 读取与
   `zh_CN` 语言包已在 M2 落地，界面接入要等主菜单实现。
4. 未生成与 C++ 的 PCG32 黄金向量对比（需要构建参考二进制）；当前用性质测试 + 可复现性测试。
5. 帧率在启动瞬间偏低（着色器/纹理首次创建），稳定后受垂直同步约束；`--smoke-frames` 的 FPS 不是基准。

## M1 — 图像栈 ✅

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `FHeroes2.Core.Imaging.Sprite` | ✅ | 图像层 + 可选变换层 + 锚点偏移；`Reset` / `Clone` / `WithoutTransformLayer` 对齐 `fheroes2::Image` |
| `ImageOps.BlitSprite` | ✅ | 变换层感知：`0` 复制、`1` 跳过、其余值经 `PaletteTransformTable` 转换目标色 |
| `PaletteTransformTable` | ✅ | 16×256 表由 `tools/scripts/GeneratePaletteTransformTable.ps1` 从 C++ 源码提取，SHA-256 固定 |
| `PngWriter` | ✅ | 手写 RGBA PNG（0 依赖）；`transform == 1` 写为 alpha 0 |
| `PngReader` / `PngImage` | ✅ | 手写 PNG 解码：色彩类型 0/2/3/4/6、1/2/4/8/16 bit、五种行滤波、`tRNS`；Adam7 交织显式拒绝 |
| `Assets.Icn.*` | ✅ | `IcnHeader`（13 字节）、`IcnSpriteDecoder`（`decodeICNSprite` 逐字节移植）、`IcnSpriteFile` |
| `Assets.Til.TilFile` | ✅ | 未压缩图块网格 |
| `AssetProbe sprites` / `tiles` | ✅ | 解码后按序号导出 PNG，便于与 C++ 输出比对 |
| `AssetProbe compare` | ✅ | PNG 逐像素比对（文件或目录），`--diff` 输出品红差异图；退出码 0 = 全同、3 = 有差异 |
| `Assets.Cursor.MonoCursorFile` | ✅ | 单色光标 `*.BMP` 条目解码（`decodeBMPFile` 移植）；透明经变换层表达 |
| `Core.Imaging.CursorTheme` / `CursorLayout` | ✅ | 主题枚举（`0x1000`–`0x4037`）、图集选择、热点偏移、距离主题换算 |
| `BmpWriter` | ✅ | 8 bit 索引 BMP（布局同 `SDL_SaveBMP`）；`EncodeSprite` 完成「背景 + 变换层」合成 |
| `AssetProbe cursor` / `--format` | ✅ | 单色光标导出；`sprites`/`tiles`/`cursor` 可选 PNG 或 BMP，`--background` 指定 BMP 底色 |
| `tools/reference` + `tools/scripts` | ✅ | 参考引擎导出器（C++ 直接链接原版源码）+ 逐字节对拍脚本 |

### M1 验证记录

```
dotnet build FHeroes2Sharp.slnx        → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx        → Core: 111 passed / 0 failed；Assets: 44 passed / 0 failed

AssetProbe sprites HEROES.ICN          → 1 个 640×480 精灵，color/single，PNG 243,258 字节
AssetProbe tiles   GROUND32.TIL        → 432 个 32×32 图块，共 432 个 PNG（399,161 字节）
HEROES.ICN 解码摘要                     → SHA-256 C27A3BB3…2734A（图像层 + 变换层状态）
GROUND32.TIL 解码摘要                   → SHA-256 8AAB8C87…64BA5F

AssetProbe compare ground-a ground-b   → 432 identical / 0 different（退出码 0）
AssetProbe compare ground-a ground-pil → 432 identical / 0 different（Pillow 重新编码的 PNG）
AssetProbe compare ground-a ground-diff → 431 identical / 1 different（1 像素，退出码 3）

AssetProbe cursor ADVMBW01.BMP         → 32×32 单色光标，90/1024 不透明，1 个 PNG
AssetProbe cursor ADVMBW01.BMP --format bmp --background 23 → 1 个索引 BMP
AssetProbe sprites ADVMCO.ICN --format bmp --background 23  → 41 个索引 BMP（尺寸与 PNG 一致）
AssetProbe tiles   GROUND32.TIL --format bmp               → 432 个 32×32 索引 BMP
ADVMBW01.BMP 解码摘要                   → SHA-256 EA6B3726…2C402
SPELBW00.BMP 解码摘要                   → SHA-256 52DB46A2…C9B50B
Pillow 交叉校验                         → 光标 BMP/PNG 的透明与颜色一致；432 个图块 BMP 与 PNG 逐像素一致
pwsh tools/scripts/BuildReferenceTool.ps1      → g++ 16.2（MSYS2 UCRT64）+ SDL2 + zlib 编译通过
pwsh tools/scripts/CompareWithReference.ps1    → 14 个条目 / 1204 个 BMP 逐字节相同（退出码 0）
pwsh tools/scripts/CompareWithReference.ps1 -All → 958 个条目 / 15,025 个 BMP 逐字节相同（退出码 0）
```

* `HEROES.ICN` 一条目 288,082 字节（RLE）→ 全不透明 640×480，`noTransformLayer` 生效即丢弃变换层。
* 变换表抽查：表 2 比表 5 更暗（64 → 69 / 65），表 15 把循环色（214–217、231–233、235）映射到静态副本。
* 摘要值写在 `SpriteAssetIntegrationTests` / `PaletteTransformTableTests` 中，任何解码偏差都会让测试失败。
* `ground-pil` 是把 `AssetProbe` 导出的 PNG 用 Pillow 13 重新编码（自适应行滤波、不同压缩）后的目录：
  `compare` 判定两者逐像素相同，说明 `PngWriter` 输出符合规范、`PngReader` 能正确还原第三方编码器的结果。
* 环境内没有 C++ 编译器时，「C++ 导出 ↔ C# 解码」只能用 Pillow/摘要间接验证；现在补上了真实对拍：
  `tools/reference/fh2_reference.cpp` 链接 fheroes2 的 `agg_file.cpp` / `serialize.cpp` / `zzlib.cpp` /
  `image.cpp` / `image_palette.cpp` / `image_tool.cpp`，用 MSYS2 UCRT64 的 g++ 16.2 + SDL2 2.32 +
  zlib 编译成 `tools-output/reference/fh2_reference.exe`，导出的是原版解码器 + `fheroes2::Save()`
  （内部 `SDL_SaveBMP`）的结果。`tools/scripts/CompareWithReference.ps1` 跑两侧工具并逐字节比较：
  默认 14 个代表性条目（GROUND32 / CLOF32 / STON、HEROES / ADVMCO / SPELCO / MINIMON / ADVBORD /
  HOURGLAS / ESCROLL / EDITOR、ADVMBW01 / ADVMBW16 / SPELBW00）共 **1204 个 BMP 全部相同**；
  `-All` 覆盖归档里全部 958 个 `.ICN` / `.TIL` / `.BMP` 条目，**15,025 个 BMP 逐个相同**。
  对拍只暴露过一处差异：SDL 把调色板第 4 字节写成 255（`SDL_Color.a`），而 `BmpWriter` 写 0，
  已改成 255（见 `MISC.md` 坑位 10）。
* 单色光标的摘要钉住 `ADVMBW01.BMP` / `SPELBW00.BMP`（图像层 + 变换层），任何像素码或调色板索引偏差都会失败；
  Pillow 读回导出的 BMP 时按 8 bit 索引解释，因此同时校验了文件头、调色板顺序与自下而上的行序。

## M2 — 主菜单（进行中）

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `Assets.Localization.MoCatalog` | ✅ | GNU MO 读取（对齐 `Translation::MOFile`）：大小端、主版本校验、`\0` 分隔复数、空条目跳过、`context\|` 剥离；仅 UTF-8（ADR-016） |
| `translations/zh_CN.po` | ✅ | 简体中文 95 条：主菜单、设置、语言选择、分辨率说明、快捷键；未翻译项回退英文 |
| `tools/scripts/BuildTranslations.ps1` | ✅ | `msgfmt --check` 校验 + 安装到 `<data>/files/lang`；`-Cultures` / `-NoInstall` / `-MsgfmtPath` |
| `Assets.Fonts.BitmapFont`（`FONT.ICN` / `SMALFONT.ICN`） | ✅ | 原版位图字体：每字符一个 `Sprite`，索引 = 字符码；ICN 从 0x20 起，载入时前置 32 个空格字形副本（对齐 `processICN()`）。度量与绘制移植自 `fheroes2::FontCharHandler`（advance = `OffsetX + Width`、空格 4/6/8、行高 11/17/33、`?` 回退、`\n`、多行居中/换行、裁剪区） |
| `Assets.Fonts.IGlyphSource` | ✅（接口） | 文本布局与光栅化之间的接缝：「度量 + 取字形」；实现有 `BitmapFont`（原版 ICN）与 `TrueTypeGlyphSource`（CJK／非单字节） |
| `Assets.Fonts.TrueTypeGlyphSource` | ✅ | 实现 `IGlyphSource`：`Sevencat.GlyphRaster`（纯托管 + 内嵌 WenQuanYi Micro Hei）按 em 尺寸光栅化，覆盖率经 `GlyphCoverageRamp` 量化到 `KB.PAL` 灰阶；`OffsetY += Ascent` 对齐位图字体的原点；`ForLineHeight(font, 17)` 直接对齐原版行高；字形按字符缓存 |
| `Assets.Fonts.GlyphText` | ✅ | 唯一的一份布局实现（`Measure` / `DrawLine` / `DrawMultiline` / `WrapLines`），`BitmapFont` 与 `TrueTypeGlyphSource` 共用；CJK 无空格段落按字符断行 |
| `Core.Imaging.PaletteTable` / `PaletteId`（文字颜色） | ✅ | `PAL::GetPalette` 的 13 张 256 字节重着色表 + identity，`tools/scripts/GeneratePaletteTable.ps1` 从 C++ 源生成、`PaletteTableTests` 固定 SHA-256；`ImageOps.ApplyPalette` 负责应用；`BitmapFont.WithPalette` 与 `GlyphCoverageRamp.WithPalette` 让位图字体与 TrueType 用同一颜色（ADR-020） |
| `Assets.Fonts.CompositeGlyphSource` | ✅ | `IGlyphSource` 的复合实现：primary（原版位图字体）定 `LineHeight`/`SpaceWidth`，fallback（TrueType）只补 primary 不能表达的字符；`ForBitmapFont()` 自动把 primary 的调色板交给 CJK 梯度（ADR-021） |
| `Client.Desktop.Fonts.GameFonts` | ✅ | 启动时装载 `FONT.ICN` / `SMALFONT.ICN` 并合成 `Small`/`Normal`/`Yellow` 三个字形源，暴露 `SupportsCjk` + `Description`；缺数据时返回 `null` 不抛异常 |
| 主菜单界面（`HEROES.ICN` 背景 + `BTNSHNGL.ICN` 招牌） | ✅ | `Scenes.MainMenuScene`：背景取 `HEROES.ICN`（资料片优先），五块招牌取 `BTNSHNGL.ICN` 的 1/5/9/13/17 帧（悬停 `+1`、按下 `+2`），位置直接来自 ICN 的 `OffsetX/OffsetY`；鼠标命中 + 释放判定 + `N/L/H/C/Q` 热键，`ESC` 退出（ADR-022） |
| `UI.Widgets.Button` / `ButtonState` | ✅ | 三帧 `Sprite` + 状态机，`Area` = released 精灵包围盒，`Update(cursor, leftButtonDown)` 返回状态是否变化；缺帧回退到 released |
| `Core.Geometry.PresentationViewport.TryToLogical` | ✅ | 窗口坐标 → 640×480 逻辑坐标（整数倍 + 黑边），黑边上返回 `false` |
| `Core.Imaging.Palette.FindDarkestIndex/FindBrightestIndex` | ✅ | 状态栏底色与诊断场景的极值色查找，权重 `299/587/114`、并列取最小索引 |
| `Assets.AssetBundle.TryReadIcn` | ✅ | ICN 装载的单一入口（`HEROES.ICN` / `BTNSHNGL.ICN` / `FONT.ICN` …），缺条目返回 `null` |
| `Client.Desktop.Localization.GameText` | ✅ | 一个语言一个不可变实例：英文 = msgid，`zh_CN` = `<data>/files/lang/zh_CN.mo`；无 CJK 字形时自动退回英文 |
| 状态栏（索引文字 + 语言标记 + FPS） | ✅ | 底部 11 px（一行 `SMALFONT.ICN`），左侧画悬停招牌的 `.mo` 译文、右侧画 `FPS \| 语言 (F2) \| vX.Y.Z`；`F2` 循环切换语言 |
| `--scene` / `--lang` / `--cursor` 开关 | ✅ | 默认进主菜单，`--scene diagnostics` 保留 M0 诊断屏；`--cursor x,y` 让冒烟截图定格 hover 态 |
| `SHNGANIM.ICN` 灯笼摆动动画 | ⏳ | 未开始（`MainMenuScene.Update` 为空实现） |
| 语言选择对话框、BGM 播放 | ⏳ | 未开始（语言目前由 `--lang` / `F2` 切换，没有 fheroes2 的选择界面） |

### M2 验证记录（本地化部分）

```
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN
  → zh_CN  95 translated messages.  7,503 bytes  F:\game\fh2\files\lang\zh_CN.mo
msgunfmt <mo>                          → 96 个 msgid 块（95 条 + 头部），与 .po 内容一致（独立校验）
dotnet test FHeroes2Sharp.slnx         → Core 111 passed / Assets 57 passed / 0 failed
```

* `MoCatalogTests` 新增 13 个用例：大小端、复数拆分、上下文回退、空译文跳过、魔数 / 主版本 / 截断 /
  无译文 / 非 UTF-8 字符集的拒绝、`FindCatalog` 的 `Data` 子目录父级探测，以及一条读取真实
  `zh_CN.mo` 的集成用例（数据缺失时提前返回，遵循 ADR-010）；实测该用例确实读到了
  `F:\game\fh2\files\lang\zh_CN.mo`（95 条，`Language=zh_CN`）。
* 条目数会随翻译推进增长，测试只断言下限（`Count > 50`）与若干固定译文，补翻译不需要改测试。
* `msgid` 与参考实现的英文原文逐字节一致（`Okay`、`New Game`、`combatMode|Manual` 等已由
  `fheroes2/files/lang/de.po` 交叉核对），因此同一份 `.po` 将来可以被 C++ 侧复用。

### M2 验证记录（位图字体）

```
dotnet test tests/FHeroes2.Assets.Tests --filter FullyQualifiedName~BitmapFontTests
  → 17 passed / 0 failed
dotnet test FHeroes2Sharp.slnx
  → Core 117 passed / Assets 74 passed / 0 failed（合计 191）
  FONT.ICN     : 96 个 sprite → 128 个字形（前置 32），空格 6，行高 17
                 'A' 15x13 @ (0,0) → advance 15；'i' 5x12 @ (0,1)；"Heroes" = 56
  SMALFONT.ICN : 96 个 sprite → 128 个字形，空格 4，行高 11；'A' advance 9；"Heroes" = 40
  "New Game"   : 宽度 91，绘制结束笔位 95（起点 4），目标缓冲 SHA256 =
                 B22BC79E11181778A41310FC75B7E23AE39B3711AC02562FEE716D347BD84446
```

* 索引移位是关键：`FONT.ICN` 的第 0 个 sprite 是空格，`game_assets.cpp` 会
  `insert(begin(), 32, imageArray[0])` 让「索引 = 字符码」，所以 `FromIcn` 也前置 32 个副本；
  少了这一步整张表会错位一个单词的首字母（`'A'` 会被当成 `'!'`）。
* 原始表只有 96 项（0x20–0x7F，含小写），因此当前字体能画 `'a'`–`'z'`；
  0x80 以上（含 CJK）在 `BitmapFont` 里走 `?` 回退，CJK 文本改用 `TrueTypeGlyphSource`（ADR-019）。
* 只做布局/绘制，不改字形：`modifyBaseNormalFont()` 的若干像素级修补（`$`、`%`、`&`、`-`、`\`、`|`、
  小写 `k`）与 `resizeCodePage()`/`generateAlphabet()` 的码页扩展尚未移植，属于主菜单拼接阶段的像素级对齐项。
* 摘要（digest）只随测试输出打印、暂不断言：真实字形的逐像素结果还没有和 C++ 侧对拍过，
  先用它做变化哨兵，等主菜单拼好（M2 已拼好，见下）后再固化为回归基线。

### M2 验证记录（TrueType 字体）

```
dotnet test tests/FHeroes2.Assets.Tests --filter FullyQualifiedName~TrueTypeGlyphSourceTests
  → 12 passed / 0 failed
dotnet test FHeroes2Sharp.slnx
  → Core 117 passed / Assets 86 passed / 0 failed（合计 203）
```

* 内置字体实测（`TextFont.BuiltIn` = WenQuanYi Micro Hei，unitsPerEm 2048）：em 16 → ascent 14.98、
  行高 18.76、空格 4 px；`'A'` 10x12 advance 10、`'中'` 14x15 advance 16。
  `TrueTypeGlyphSource.ForLineHeight(font, 17)` 反解出 em 14.50，行高正好 17，与 `FONT.ICN` 的
  17 px 网格一致，所以中英混排能落在同一基线上。
* 量化依据：`KB.PAL` 的 10–36 号索引是一条 27 级灰阶（10 = (63,63,63) 最亮 → 36 = 黑），
  `FONT.ICN` 的亮像素集中在 10/16/21/12/14 上——原版字形自带「预烘焙抗锯齿」。因此 TrueType 也量化到
  同一梯度（默认 `[25, 21, 16, 12, 10]`，最淡→实心），同屏混排不会出现「两种白」。
* `'中'` @16 的覆盖率有 16 个档位（0–255），量化后只落 3–4 个调色板索引；覆盖率 < 32 的像素走变换层
  跳过，这是小字号不糊边的关键。
* 坐标只换算一次：`Sprite.OffsetY = mask.OffsetY + Ascent`（em 16 下 `'中'` 为 -13 + 15 = 2），
  于是 `GlyphText.DrawLine` 用同一套「左上角 + OffsetX/OffsetY」就能画两种字形。
* 布局抽到 `GlyphText` 之后，`BitmapFontTests` 的 17 个用例原样通过：换行、居中、裁剪、尾随空格规则没有变化。

### M2 验证记录（文字颜色）

```
pwsh tools/scripts/GeneratePaletteTable.ps1
  → 3584 bytes (14 tables)，SHA-256 4DB8D4E1…81FA3C
dotnet test FHeroes2Sharp.slnx
  → Core 128 passed / Assets 93 passed / 0 failed（合计 221）
```

* 表语义抽查：`Standard` 为恒等表；`YellowFont` 把灰阶 10–36 映射到黄阶 114–130；`WhiteFont` 保持灰阶
  不变、其余索引清零；`GrayFont` 10 → 15（更暗）；`Darkening` 10 → 16；`Purple` 落在 131–151。
* `GlyphCoverageRamp.Heroes2Text.WithPalette(PaletteId.YellowFont)` = `[124, 121, 118, 115, 114]`，
  于是 `'中'` 的每个落地像素都落在 114–130，与 `YELLOW_FONT` 的色阶完全一致（`FontPaletteTests` 断言）。
* 共享字形只转换一次：`FromIcn` 让 32 个控制码共用首个 sprite，`WithPalette` 用引用相等的字典去重，
  否则二次映射会把像素清零（`BitmapFont_ConvertsAReusedGlyphOnlyOnce`）。
* 变换层不参与重着色：`WithPalette` 保留 `Transform`（1 = 跳过），只改图像层，因此 TrueType 字形的
  「跳过透明像素」语义与位图字体一致。

### M2 验证记录（复合字形与 UI 接线）

```
dotnet test FHeroes2Sharp.slnx
  → Core 128 passed / Assets 105 passed / 0 failed（合计 233）
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --screenshot tools-output/fonts.png
  → Game fonts ready: FONT.ICN + SMALFONT.ICN + WenQuanYi Micro Hei (CJK).
    Presentation ready: 1280x960 output, x2 scale, logical 640x480.  91.0 FPS
```

* 截图里同一行 `英雄无敌 II  Hero Edition 12345` 画了两遍（`FONT.ICN` 过 `YELLOW_FONT` 与
  未重着色的表），逐像素统计这两行的颜色：

  ```
  黄字行: 228,200,20 ×337 | 200,160,24 ×242 | 224,196,0 ×174 | 168,124,20 ×151
  灰字行: 252,252,252 ×337 | 192,192,192 ×242 | 232,232,232 ×167 | 144,144,144 ×144
  ```

  两行的**像素档位计数完全相同**（337/242/…），只有索引不同：这正是「一份字形 + 一张
  调色板表」的预期——英文来自 ICN、中文来自 TrueType，量化后的覆盖率档位数量必然一致。
  黄色行落在 114–130 黄阶、灰色行落在 10–24 灰阶，与 ADR-020 的表一致。
* `CompositeGlyphSourceTests` 12 个用例（其中 1 个读真实归档、需游戏数据）：度量只取位图字体
  （小 4/11、正常 6/17，
  `Fallback.LineHeight` 与之相同）、拉丁字符返回**同一个** ICN `Sprite` 实例（`Assert.Same`）、
  CJK 走 fallback、空格/换行沿用位图字体的 advance、`U+E000` 这类两边都没有的码位落在位图
  `?` 上、`ForBitmapFont` 让 CJK 继承 `YellowFont` 的 114–130 而默认灰阶仍是
  `[25,21,16,12,10]`、显式 `ramp` 生效、空 provider 抛 `ArgumentNullException`；另有 1 个
  真实归档用例（读出 `FONT.ICN` 的灰/黄两份、`'新'` 只在复合源里可用、`新游戏 New Game`
  的笔位等于 `Measure` + 起点）。
* `"英雄 A"` 的结束笔位严格等于 `起点 + Measure`，且 `Measure("英雄") > Measure("AA")`；
  整行 17 px 高度内没有像素溢出到 y=17，说明 fallback 的字形原点（`OffsetY + Ascent`）与
  位图字体落在同一基线。
* 诊断覆盖层新增「游戏字体」一行，直接显示 `Description`，因此字体装载失败在画面上可见
  （`GameFonts.TryLoad` 捕获 `BinaryFormatException` / `FileNotFoundException` 等并降级）。

### M2 验证记录（主菜单）

```
dotnet build FHeroes2Sharp.slnx                       → 0 warning / 0 error
dotnet test  FHeroes2Sharp.slnx                       → Core 136 / Assets 108 / UI 8 = 252 passed, 0 failed
dotnet run --project src/FHeroes2.Client.Desktop -- --data F:\game\fh2\Data \
    --smoke-frames 90 --screenshot tools-output/menu.png
  → Text catalog 'F:\game\fh2\files\lang\zh_CN.mo' loaded: 95 entries, language 'zh_CN'.
    Main menu ready: HEROES.ICN 640x480, 5 signs from BTNSHNGL.ICN, language 'zh_CN' (95 entries).
    Starting on the main menu (language 'zh_CN', 2 available).
    Presentation ready: 1280x960 output, x2 scale, logical 640x480.
    Smoke mode finished after 90 frames (61.6 FPS)
dotnet run … --smoke-frames 60 --cursor 230,210 --screenshot tools-output/menu-hover.png
  → 71.1 FPS
AssetProbe compare menu.png menu-hover.png
  → 1563 of 307200 pixels differ, max channel delta 252, first at (221,190)
    差异包围盒 (8,190)-(525,478)：招牌高亮 + 状态栏文字，其余像素逐字节相同
```

* 招牌位置完全来自 ICN 头（`BTNSHNGL.ICN` 的 sprite 1/5/9/13/17，四个帧一组）：
  `482,176 81x113` / `195,180 80x77` / `408,107 80x76` / `303,137 76x43` / `0,425 86x44`，
  `AssetBundleTests` 用真实归档把这张表钉住；菜单代码里没有第二套坐标常量。
* 背景随数据版本变化：`heroes2x.agg` 里的 `HEROES.ICN` 是 PoL 版（横幅 *The Price of Loyalty*），
  `AssetBundle` 优先取它（行为同 `AGG::GetICN`），所以截图是资料片样式；只有基础版数据时是原版街景。
* 悬停把招牌换成第 `+1` 帧（更亮），状态栏左侧出现 `zh_CN.mo` 的译文（截图为「载入之前保存的游戏。」），
  右侧是 `78 FPS | 简体中文 (F2) | v0.1.0`：一行 `SMALFONT.ICN` + 一个调色板索引，中英文同色。
* 语言切换 `F2` 在 `en` / `zh_CN` 之间循环；字体没有 CJK 字形时 `MainMenuScene` 只保留英文，
  避免把中文画成 `?`（`GameFonts.SupportsCjk` 决定）。
* `ButtonTests` 8 个用例：`Area` 来自精灵锚点、半开边界命中、hover → press → 离开的状态迁移与
  「没有变化就不算变化」的返回值、缺 hover/pressed 帧回退到 released、按下与悬停画不同帧、
  变换层 `1` 保留目标像素（ICN 用这种方式表达按钮上的洞）。
* `AssetBundleTests` 3 个用例：缺条目返回 `null`、真实 `HEROES.ICN`（1 张 640×480）与
  `BTNSHNGL.ICN`（20 张，含上述五个锚点）、`TryReadIcn` 与 `IcnSpriteFile.Parse` 对同一入口逐字节一致。
* Core 新增 4 个用例：`TryToLogical` 在整数倍下与 `ToOutput` 互逆、黑边与视口外返回 `false`、
  空视口返回 `false`、`FindDarkestIndex` / `FindBrightestIndex` 的亮度权重与并列取最小索引。
* 诊断屏仍可用：`--scene diagnostics` 走到原来的 M0 画面（文本路径未变，只是 `IScene.DrawFrame`
  多了一个运行时快照参数）。

## 后续里程碑

见 [PLAN.md](PLAN.md)。M1 的条目已全部完成，并且已经用**原版 C++ 实现**对拍过
（`tools/scripts/CompareWithReference.ps1`，默认 14 个条目 / 1204 个 BMP 逐字节相同）；
`AssetProbe compare` 仍然保留，用于 PNG 视觉回归与差异图输出。
