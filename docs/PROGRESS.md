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
| `FHeroes2.App` | ✅ | SDL3 宿主、`SdlPresenter`、`SdlTextRenderer`（SDL3_ttf）、诊断场景、命令行、截图 |
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
| `AssetProbe compare` | ✅ | PNG 逐像素比对（文件或目录），`--diff` 输出品红差异图、`--ignore x,y,w,h` 排除运行时区域；退出码 0 = 全同、3 = 有差异 |
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
| `App.Fonts.GameFonts` | ✅ | 启动时装载 `FONT.ICN` / `SMALFONT.ICN` 并合成 `Small`/`Normal`/`Yellow` 三个字形源，暴露 `SupportsCjk` + `Description`；缺数据时返回 `null` 不抛异常 |
| 主菜单界面（`HEROES.ICN` 背景 + `BTNSHNGL.ICN` 招牌） | ✅ | `Scenes.MainMenuScene`：背景取 `HEROES.ICN`（资料片优先），五块招牌取 `BTNSHNGL.ICN` 的 1/5/9/13/17 帧（悬停 `+1`、按下 `+2`），位置直接来自 ICN 的 `OffsetX/OffsetY`；鼠标命中 + 释放判定 + `N/L/H/C/Q` 热键，`ESC` 退出（ADR-022） |
| `UI.Widgets.Button` / `ButtonState` | ✅ | 三帧 `Sprite` + 状态机，`Area` = released 精灵包围盒，`Update(cursor, leftButtonDown)` 返回状态是否变化；缺帧回退到 released |
| `Core.Geometry.PresentationViewport.TryToLogical` | ✅ | 窗口坐标 → 640×480 逻辑坐标（整数倍 + 黑边），黑边上返回 `false` |
| `Core.Imaging.Palette.FindDarkestIndex/FindBrightestIndex` | ✅ | 状态栏底色与诊断场景的极值色查找，权重 `299/587/114`、并列取最小索引 |
| `Assets.AssetBundle.TryReadIcn` | ✅ | ICN 装载的单一入口（`HEROES.ICN` / `BTNSHNGL.ICN` / `FONT.ICN` …），缺条目返回 `null` |
| `App.Localization.GameText` | ✅ | 一个语言一个不可变实例：英文 = msgid，`zh_CN` = `<data>/files/lang/zh_CN.mo`；无 CJK 字形时自动退回英文 |
| 状态栏（索引文字 + 语言标记 + FPS） | ✅ | 底部 11 px（一行 `SMALFONT.ICN`），左侧画悬停招牌的 `.mo` 译文、右侧画 `FPS \| 语言 (F2) \| vX.Y.Z`；`F2` 循环切换语言 |
| `--scene` / `--lang` / `--cursor` / `--right-button` / `--screenshot-frame` / `--freeze-animation` 开关 | ✅ | 默认进主菜单，`--scene diagnostics` 保留 M0 诊断屏；`--cursor x,y` 把人造光标放进逻辑坐标（真实鼠标移动 / 移出被忽略，hover 态不会被物理指针清掉）；`--right-button` 在人造光标处按住右键，用来看/抓提示框；`--screenshot-frame n` 选抓图帧，`--freeze-animation` 把灯笼冻在第 1 帧 |
| `Assets.Icn.IcnAnimation` | ✅ | ICN 帧区间循环播放器（对齐 `ICN::getAnimatedIcnIndex` 的帧表 + `fheroes2::TimeDelay` 的计时）：起始帧 + 帧数 + 每帧时长，整帧才前进、长停顿跳帧不补播、越界/非正时长抛异常 |
| `SHNGANIM.ICN` 灯笼光晕动画 | ✅ | 第 0 帧灯体先画一次，第 1…39 帧每 250 ms 换帧（9.75 s 一圈），顺序「背景 → 灯体 → 光晕 → 门光 → 招牌 → 状态栏」同 `Game::MainMenu()`（ADR-022） |
| 设置门悬停发光 | ✅ | `UI.Widgets.DoorHighlight`：光标进入 `settingsArea`（63,202 108×160）就把 `SHNGANIM.ICN` 第 18 帧套 `PaletteId.NoCycle` 后只画 `y ≥ 55` 的下半部分（`doorOffsetY`），画在光晕之上、招牌之下；命中区不等于发光像素，缺帧/太矮只警告不建门（ADR-022） |
| `tests/baselines/{menu,settings}.png` + `tools/scripts/Verify{Menu,Settings}Baseline.ps1` | ✅ | 入库的 640×480 像素基线 + 门禁脚本（两份基线共用 `PixelBaseline.ps1` 的渲染/比对管线）：先做基线自检（PNG 魔数 + 尺寸），再冻结动画 + 人造光标渲染一帧，用 `compare --ignore 0,469,640,11` 逐像素比对；设置那一帧多一个 `--open-settings`，语言钉成 `en`；缺数据时 `skip` 退出 0（ADR-023、ADR-028） |
| `.github/workflows/ci.yml` | ✅ | `build` job（`dotnet restore` → `build -warnaserror` → `test`）+ `menu-baseline` job（调基线脚本，`FHEROES2_DATA`/`FHEROES2_RUNNER` 两个仓库变量接线），失败时把差异图当 artifact 上传 |
| 右键提示框（`BUYBUILD.ICN` 消息框） | ✅ | `UI.Dialogs.FrameBoxChrome` + `FrameBox` + `MessageBox`：六块列拼装（顶 99 + 中段 35×n + 底 81），文字区 244 宽、最小 70 高；标题 yellow / 正文 normal 折行居中；`MainMenuScene.SetRightButton` 按住创建、松开丢弃，画在状态栏之后（ADR-024） |
| 对话框影像原语（渐变阴影 / 抖动过渡） | ✅ | `ImageOps` 新增 partial 文件：`ApplyTransform`（按 16 张重着色表转换一块矩形）、`AddGradientShadow`（沿精灵轮廓投 2–5 号表的渐变阴影，斜率取整同 `std::round`）、`AddGradientShadowForArea`（矩形左边与下边的 16 px 阴影带）、`CreateDitheringTransition`（周期逐次翻倍的棋盘过渡，奇数尺寸的一半右移一格照抄）；8 个用例与 C++ 参考实现逐字节相同（`CompareEffectsWithReference.ps1`） |
| 标准对话框窗口（`StandardWindow`） | ✅ | `UI.Dialogs.StandardWindowChrome`（`SURDRBKG.ICN` / `WINLOSE.ICN` / `STONEBAK.ICN` 第 0 帧）+ `StandardWindow`：四角 22 px、可重复边框起点 43 px（横边框从 `43+16` 取像素）、接缝 10 px、底纹从 22 px 起铺；`Create` / `CreateCentered` 给出 `ActiveArea` / `WindowArea` / `TotalArea`（阴影只挂左/下 16 px）；4 个用例与 C++ 参考实现逐字节相同（`CompareWindowWithReference.ps1`），尚未接线到对话框（ADR-024） |
| `Core.Imaging.IcoFile` | ✅ | Windows 图标（`.ico`）读取：`ICONDIR` / `ICONDIRENTRY`（0 = 256）、PNG 条目、未压缩 24/32 位 DIB（两页高度、自下而上行序、4 字节行对齐、AND 掩码、32 位 alpha 全零时回退到掩码），`Largest` / `Closest` 选择；14 个用例（`IcoFileTests`） |
| `assets/logo.ico` + `tools/scripts/GenerateAppIcon.ps1` | ✅ | 从 `assets/logo.png` 生成 7 张图（16/24/32/48/64 位图 + 128/256 PNG，240,361 字节）；同一份文件既是 exe 的 `<ApplicationIcon>` 又是嵌入资源（ADR-025） |
| `Graphics.WindowIcon` + `SdlPresenter` 接线 | ✅ | 读嵌入的 `logo.ico` → `SDL_CreateSurfaceFrom`（64 px 为主图、128/256 为高 DPI 备选，`SDL_AddSurfaceAlternateImage`）→ `SDL_SetWindowIcon`；surface 与像素缓冲在 `SDL_DestroyWindow` 之后才释放 |
| 仅 Windows x64 | ✅ | 客户端 `RuntimeIdentifier=win-x64`（不进输出路径，基线脚本路径不变）、`[assembly: SupportedOSPlatform("windows")]`、`Main` 首行平台检查；工具与测试项目不设 RID（ADR-025） |
| 语言选择对话框（`dialog_language_selection.cpp`） | 🟡 | `UI.Dialogs.LanguageSelectionDialog` 的**对话框本体**已写好（参考布局、列表、滚动跟随、点击选择、OKAY/CANCEL），但**未接线、无测试、无 ADR**；剩余项见下方「语言选择对话框：剩余工作」 |
| BGM / 音效播放 | 🟡 | `App.Audio.AudioPlayer`（SDL3_mixer 3.2.0）：主菜单循环播 `MUS::MAINMENU`（`homm2_41.ogg`，外部 `MUSIC/`），音效走 AGG 里的 `*.82M`（8 位 22050 Hz 单声道，`Assets.Audio.WaveSound` 补 44 字节 RIFF 头）；`Assets.Audio.MusicTrackCatalog` 支持 DOS/Windows/Mapped 三套命名，`--no-audio` / `--play-sound <NAME>` 供验证（ADR-030）。仍未做：XMI/MIDI 合成（没有外部音轨时的兜底）、音量设置、按场景切曲 |

### M2 验证记录（音频）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx              → Core 167 / Assets 143 / UI 113，全部通过
                                                （Assets 新增 17 条：MusicTrackCatalogTests 11、
                                                  WaveSoundTests 6，其中 3 条要真实 AGG）
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --smoke-frames 30
  → Audio ready: SDL3_mixer 3003000, 6 music folder(s) probed.
  → Music playing: 'Main Menu' from 'homm2_41.ogg'.
dotnet run … --play-sound PICKUP01           → 无警告（AGG 取 PICKUP01.82M → 补 RIFF 头 → 播）
dotnet run … --play-sound NOSUCH             → WARNING Sound 'NOSUCH.82M' is not in the archive.
dotnet run … --no-audio                      → Audio disabled by --no-audio.
```

* 音乐文件名是**算出来的**而不是查表猜的：`MUS::MAINMENU` = 42 + DOS 命名（减一）= `homm2_41`
  ，与 GOG 版 `MUSIC/` 里实际存在的文件对得上；`MusicTrackCatalogTests` 同时钉住 Windows
  （`Track42`）与 Mapped（`42 Main Menu.ogg`）两套命名。
* 音效的 WAV 头是逐字段照 `LoadWAV` 写的（8 位 / 单声道 / 22050 Hz），`WaveSoundTests` 把 44
  字节头的每个字段都断言了一遍，比「能听到声音」更早发现问题。

### M2 验证记录（对话框影像原语）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx              → Core 145 / Assets 120 / UI 36，全部通过
                                               （Core 新增 9 条：ImageShadingTests）
pwsh tools/scripts/BuildReferenceTool.ps1    → 参考导出器编译通过（新增 effects 子命令）
pwsh tools/scripts/CompareEffectsWithReference.ps1
  → Files: 8 of 8 are byte for byte identical
```

* `effects` 的输入全是公式（96×64 画布、37×21 精灵，每第五条对角线透明），两侧共享的只有算式：
  8 个用例分别覆盖变换表、精灵阴影、矩形阴影、纵/横过渡的偶数与奇数尺寸，以及越界裁剪。
  这条比对是「移植是否忠实」唯一的硬证据，单元测试只钉 C# 侧的约定。
* 测试补的是对拍覆盖不到的部分：`ApplyTransform` 只动请求的矩形、负坐标裁剪、非法表号抛异常、
  单层源不出阴影、阴影越靠近轮廓越强（4 号表比 5 号表深）、过渡里奇数尺寸的一半位移、透明源像素不覆盖目标。
* 一个实现细节：C++ 用指针走 `CreateDitheringTransition`，移植改成「左右/上下两个索引各自前进」，
  但奇偶分支的 `--`/`++` 与 `patternPoint` 的计算一字未改——这正是逐字节相同的原因。

### M2 验证记录（标准对话框窗口）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  tests/FHeroes2.UI.Tests         → 45 passed / 0 failed（新增 9 条：StandardWindowTests）
pwsh tools/scripts/BuildReferenceTool.ps1    → 参考导出器编译通过（新增 window 子命令）
pwsh tools/scripts/CompareWindowWithReference.ps1
  → Files: 4 of 4 are byte for byte identical
```

* `window` 的 4 个用例：332×232 普通窗口、283×189 奇数尺寸窗口、332×232 纯边框（无底纹）、
  552×432 宽窗口（横向边框要重复一次）。画布是同一条公式填充，两侧共享的数据只有
  `SURDRBKG.ICN` / `WINLOSE.ICN` / `STONEBAK.ICN` 的第 0 帧；任何坐标错位都会立刻失败。
* 参考侧 `renderStandardWindow()` 是把 `ui_window.cpp` 的 `render()` 逐行抄进导出器的
  （只把 `_windowArea` / `_hasBackground` 换成参数），所以它验证的是「移植是否忠实」，
  而不是「两份 C# 是否互相一致」。
* `StandardWindowTests` 补的是对拍覆盖不到的部分：`Create` 的几何（`TotalArea` 只比窗口多左/下 16 px
  的非对称）、`CreateCentered` 的奇数余量留右下、缺任一归档返回 `null`、四角 / 两条竖边 / 两条横边 /
  底纹各自的来源、角部接缝确实同时含边框与底纹两种像素、无底纹时内部一个像素都不动、
  窗口起点在画布外时按裁剪绘制。
* 仍未接线：没有任何对话框创建它（设置对话框缺的是按钮组，见 ADR-024）。



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
dotnet run --project src/FHeroes2.App -- --smoke-frames 40 --screenshot tools-output/fonts.png
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
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2\Data \
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

### M2 验证记录（灯笼光晕动画）

```
dotnet build FHeroes2Sharp.slnx                       → 0 warning / 0 error
dotnet test  FHeroes2Sharp.slnx                       → Core 136 / Assets 120 / UI 8 = 264 passed, 0 failed
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2\Data \
    --smoke-frames 90 --freeze-animation --screenshot tools-output/menu.png
  → Main menu ready: HEROES.ICN 640x480, 5 signs from BTNSHNGL.ICN,
    lantern glow 39 frames every 250 ms, language 'zh_CN' (95 entries).
    Smoke mode finished after 90 frames (93.6 FPS)
dotnet run … --smoke-frames 400 --screenshot-frame 300 --cursor 620,300 --screenshot tools-output/menu-anim.png
  → 第 300 帧 ≈ 光晕第 11 帧（帧率越高帧号越小）
AssetProbe compare menu.png menu-anim.png
  → 649 of 307200 pixels differ, max channel delta 252, first at (154,190)
dotnet run … --smoke-frames 90 --cursor 230,210 --freeze-animation --screenshot tools-output/menu-hover.png
AssetProbe compare menu.png menu-hover.png
  → 1565 of 307200 pixels differ, max channel delta 252, first at (221,190)
```

* `SHNGANIM.ICN` 的真实结构被 `IcnAnimationTests` 钉住：40 张精灵，第 0 张 139×131 @ (46,176)（熄灯灯体），
  第 1…39 张统一 136×180 @ (48,183)，正是 `ICN::getAnimatedIcnIndex()` 里 `1 + frame % 39` 那一条。
  两者尺寸/锚点不同，所以灯体与光晕必须是两层（先画第 0 帧，再画第 1…39 帧）。
* 把 40 帧导出成 PNG 后逐对 `AssetProbe compare`：帧间可见差异 **8–942 像素**。第 3→4 … 21→22 帧
  基本稳定在 450–590（「强光扫过」的那一段，其中 12→13、19→20 两步几乎不动），
  之后 22→23 … 25→26 落在 35–160，再由 26→27 的 707、29→30 的 942 冲到最强。
* 最强的一条证据：`SHNGANIM_001` 与 `SHNGANIM_011` 在 136×180 的精灵内部差 615 像素，而整屏
  `menu.png` 与 `menu-anim.png` 差 649 像素 = 615（美术差）+ 34（状态栏 FPS 数字），
  说明光晕的锚点、帧序、循环位置都与参考实现一致，没有被背景或招牌覆盖。
* 截图必须配 `--freeze-animation`：`--screenshot` 默认在第 30 帧抓图，抓到循环第 1 还是第 2 帧
  取决于机器帧率（两帧本身只差 94 像素），冻结后基线才是确定的。
* `IcnAnimationTests` 12 个用例：首帧与 `SpriteIndex` 映射、250 ms 未满不换帧、部分累积（200+60 ms）、
  长步跳帧不补播、整圈回绕、`TimeSpan.Zero` / 负时长被忽略、`Reset` 连累积一起清掉、
  帧区间越界 / 负起始帧 / 零帧 / 非正时长抛异常、区间外的精灵永不被取到，以及 1 个读真实归档的
  `SHNGANIM` 用例（数据缺失时提前返回，遵循 ADR-010）。
* `--cursor` 的语义顺带修了：真实鼠标移动 / 移出窗口会把 hover 清掉，导致 hover 截图时有时无；
  现在 `--cursor` 一旦给出，宿主就忽略真实指针事件（ADR-022 第 6 条）。

### M2 验证记录（设置门悬停发光）

```
dotnet build -warnaserror                              → 0 warning / 0 error
dotnet test  --no-build                                → Core 136 / Assets 120 / UI 16 = 272 passed, 0 failed
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --scene menu --lang en \
    --cursor 100,280 --freeze-animation --screenshot-frame 30 --smoke-frames 90 \
    --screenshot tools-output/door/hover.png
  → Main menu ready: …, settings door lights up on hover, language 'en' (0 entries)
AssetProbe compare hover.png tests/baselines/menu.png --ignore 0,469,640,11
  → 499 of 307200 pixels differ, max channel delta 124, first at (115,241)
```

* 门光与参考实现同源：`Game::MainMenu()` 里 `highlightDoor = Assets::getImage( ICN::SHNGANIM, 18 )`
  + `ApplyPalette( highlightDoor, 8 )`，只在 `le.isMouseCursorPosInArea( settingsArea )` 为真时从
  `y = 55 * scale + offsetY` 起 blit 到精灵底部；`settingsArea` 是 `63,202,108,160`。
  `DoorHighlightTests` 把这四个数（18 / 55 / 63,202 / 108×160）全部钉住。
* 悬停帧与入库基线的差异只有 **499 像素**：用 Pillow 逐像素核对后 bbox = `(87,241)-(122,362)`，
  完全落在门光的裁剪区 `48,238 136×125` 之内，且 499 像素**全部变亮**（平均 +33.6 亮度）。
  招牌、灯笼本体、状态栏一个像素都没动，说明光只落在门上。
* 命中区边界逐点验过（同一套渲染参数，与基线比）：`63,202` 与 `100,280` → 499 像素差（亮）；
  `62,202`、`63,201`、`171,300`、`100,363`、`620,100` → `identical`。右/下边界是开区间
  （`Rect.Contains` 语义），与 C++ 的 `isMouseCursorPosInArea` 一致。
* 光晕本身也在画同一块区域，门光只是叠在它上面，所以帧号错位不会「多亮一块」：冻结在第 1 帧再比，
  差值就是「第 18 帧 vs 第 1 帧」落在门上的那部分——参考实现同样是先画当前动画帧、再画门光。
* `PaletteId.NoCycle` 在本数据集上是**无操作**：临时跳过调色板再渲染一帧，与套上调色板的悬停帧
  比对为 `0` 像素差（`SHNGANIM` 第 18 帧没用到循环色）。保留它是为了与
  `ApplyPalette( highlightDoor, 8 )` 逐字对齐，别的数据版本里那一帧可能带循环色。
* 参考实现只在 250 ms 的动画节拍里重画门光，端口在每帧的 `DrawFrame` 里画：场景本来就从背景重铺，
  所以不会出现「移开后残留门光」；顺带让 `--freeze-animation` 的悬停截图也是确定的（冻结动画不影响门光）。
* 未接的部分：点门没有反应。设置对话框（语言 / 分辨率 / 音效，参考实现是 `Dialog::GameSettings()`）
  属于后续里程碑，门现在只负责「提示入口存在」。

### M2 验证记录（像素基线门禁）

```
dotnet build FHeroes2Sharp.slnx                       → 0 warning / 0 error
dotnet test  FHeroes2Sharp.slnx                       → Core 136 / Assets 120 / UI 8 = 264 passed, 0 failed
AssetProbe compare menu.png menu.png --ignore 0,0,10,10
  → identical 640x480  100 pixels ignored                      （退出码 0）
AssetProbe compare menu.png menu-anim.png --ignore 0,469,640,11
  → 615 of 307200 pixels differ  7040 pixels ignored           （退出码 3）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update
  → render menu F:\game\fh2 → update tests/baselines/menu.png
    sha256 73C1A211A86C0BCD2C10B47C32F4B7537B007498F9D325ED9004FC26EFC1E563（239,932 字节）
pwsh tools/scripts/VerifyMenuBaseline.ps1            （连续两次）
  → menu.png identical 640x480  7040 pixels ignored → match（退出码 0 / 0）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Baseline tools-output/menu-hover.png
  → 1095 of 307200 pixels differ → DIFF + diff_menu.png（退出码 1）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Baseline tools-output/menu-hover.png -Tolerance 2000
  → 1095 of 307200 pixels differ, tolerance is 2000 → match（退出码 0）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData（数据目录不可用时）
  → error Heroes2.agg was not found（退出码 1）；不给 -RequireData 时 skip（退出码 0）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Baseline tools-output/nope.png
  → error baseline '…\nope.png' does not exist（退出码 1，渲染前就失败）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Baseline README.md
  → error baseline '…\README.md' is not a PNG file（退出码 1）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Baseline tools-output/preflight/BTNSHNGL_000.png
  → error baseline '…' is 81x113; the logical surface is 640x480（退出码 1）
```

* CI：`.github/workflows/ci.yml`（Python `yaml.safe_load` 校验通过，job/step 与 `on` 键都能解析）——
  `build` job 跑 `dotnet restore` → `dotnet build -warnaserror` → `dotnet test --no-build`；
  `menu-baseline` job 用 `@splat` 拼参数调脚本（设了 `vars.FHEROES2_DATA` 才加 `-DataDirectory` 与
  `-RequireData`），失败时上传 `tools-output/baseline/`。本地按 yml 里的片段原样跑过两次：
  设 `FHEROES2_DATA=F:\game\fh2` → `match`（退出码 0），splat 传参与 `-RequireData` 都生效。
* 基线自检（`Assert-PngBaseline`）在查数据**之前**执行：读 8 字节 PNG 魔数 + `IHDR`，再用
  `Read-BigEndianInt32` 解出宽高要求 640×480。这样托管 runner 上没有数据也能抓住「基线被删/被换」，
  只有真正的像素比对退化为 `skip`。
* 没有游戏数据的机器上脚本 `skip` 并以 0 退出（与 ADR-010 的「缺数据提前返回」同一策略），
  自建 runner 用 `-RequireData` 把 skip 变成失败。托管 runner 永远没有数据（不入库、也不该塞进
  secret），所以 PR 上的绿色 `menu-baseline` 只代表「基线文件没坏」；真正的像素门禁要靠
  `FHEROES2_RUNNER=fh2-data` + `FHEROES2_DATA=…` 的自建 runner（ADR-023 第 7 条）。

* 门禁的核心是「三个开关必须一起给」：`--freeze-animation`（光晕不跟挂钟）、`--cursor 620,100`
  （人造光标，物理鼠标不会点亮招牌）、`--ignore 0,469,640,11`（状态栏的 FPS 与版本号）。
  前两个都在客户端侧，第三个是新加的 `AssetProbe compare --ignore x,y,w,h`（可重复，
  输出行尾写 `N pixels ignored`）。
* 忽略状态栏后连续两次运行都是 `identical`，所以基线用**逐字节相等**做判据（`-Tolerance` 默认 0，
  需要时可以放宽到像素数阈值）；差异图写到 `tools-output/baseline/diff/diff_menu.png`。
* 忽略的 7040 像素正好是 640×11 的状态栏，剩下参与比对的是 300,160 像素。顺带复核了上一节的
  算术：`menu.png` 与 `menu-anim.png` 的 649 像素差异里，扣掉 34 像素的 FPS 数字正好是 615 像素
  的精灵差（`SHNGANIM_001` vs `_011`）。
* 基线用**资料片（PoL）数据**渲染（`heroes2x.agg` 的 `HEROES.ICN` 是 *The Price of Loyalty* 横幅版），
  这一点与更新规矩写在 `tests/baselines/README.md`，换基础版数据要另存一份而不是覆盖。
* 没有游戏数据的机器上脚本 `skip` 并以 0 退出（与 ADR-010 的「缺数据提前返回」同一策略），
  自建 runner 用 `-RequireData` 把 skip 变成失败。仓库当前没有托管 CI，这一步是「可被 CI 直接
  调用的门禁命令」，不是某个具体 pipeline 的 yml。

### M2 验证记录（右键提示框）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx              → Core 136 / Assets 120 / UI 36，全部通过
                                               （UI 新增 20 条：几何 14 + 版式 6）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -DataDirectory F:\game\fh2
  → match  the rendered menu is identical to the baseline（7040 pixels ignored）
客户端 --cursor 520,230 --right-button --freeze-animation --screenshot tools-output/tip/new-game.png
  → 消息框 159,141 305×180：标题「新游戏」、正文「开始单人游戏或多人游戏。」
客户端 --cursor 100,300 --right-button --freeze-animation --screenshot tools-output/tip/door.png
  → 「游戏设置」/「更改游戏的语言、分辨率与设置。」
```

* 与同坐标但不带 `--right-button` 的截图相减：`y < 469` 的差异矩形**恰好**是 `159,141 305×180`
  ——正是 `FrameBox.FrameArea`——其余像素（含 `620,100` 附近、招牌、灯笼）一个都没变；
  另外两帧完全相同、只有状态栏 FPS 数字不同，说明提示框的渲染是确定性的。
* `FrameBoxTests` 钉住参考实现的算术：`height=70 → 0 切片`、`71 → 1×1`、`140 → 3×70`、
  `showButtons` 先加 40、`Area` 最小 70 高、`Origin.Y = ( 480 − 中段 ) / 2 − 99`；
  `Draw` 的用例逐像素验证「中段从源图第 10 行开始切」以及接缝落在 `x = 320`。
* `MessageBoxTests` 用「实心字形 + 固定度量」的假字形源，可以直接断言标题在文字区第 10 行、
  正文在标题下 25 行、两者都居中于 244 px 宽——不需要游戏数据也不需要真字体。

### M2 验证记录（程序图标与 win-x64 收口）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx              → Core 159 / Assets 120 / UI 45，全部通过
                                               （Core 新增 14 条：IcoFileTests）
pwsh tools/scripts/GenerateAppIcon.ps1        → 7 张图（16/24/32/48/64 位图 + 128/256 PNG）
                                               240,361 字节；assets/logo.ico
pwsh tools/scripts/VerifyMenuBaseline.ps1 -DataDirectory F:\game\fh2
  → match  the rendered menu is identical to the baseline（7040 pixels ignored）
客户端 --smoke-frames 10 --cursor 620,100 --freeze-animation --screenshot tools-output/icon-smoke.png
  → [INFO] Window icon set (3 embedded image(s))；退出码 0，截图 640×480
[System.Drawing.Icon]::ExtractAssociatedIcon(bin/Debug/net10.0/FHeroes2Sharp.exe)
  → 32×32，中心像素 #CEA750，确认图标资源已经编进 exe
```

* 生成的 `.ico` 用两条互不相干的路径读过一遍：Windows 自己的 `System.Drawing.Icon` 能打开
  （32×32 与 128×128），我们的 `IcoFile` 也能解出 16/24/32/48/64/128/256 七张，且每张中心像素
  alpha = 255（不透明，任务栏不会显示空白）。
* `Window icon set (3 …)` 的「3」是 64 px 主图 + 128/256 两个高 DPI 备选；SDL 的
  `SDL_SetWindowIcon` 文档要求先 `SDL_AddSurfaceAlternateImage` 再设置，顺序就是代码里的顺序。
* 输出路径没有因为 RID 改变（`bin/Debug/net10.0/FHeroes2Sharp.exe` 仍在原地），所以
  `VerifyMenuBaseline.ps1` 与 CI 的 `menu-baseline` job 都不需要改；`menu.png` 依然逐像素相同，
  说明这条接线只影响 Win32 窗口层，逻辑画面一个像素都没动。
* 生成脚本第一版把 `$stream.ToArray()` 直接 `return`，PowerShell 把它展开成 `object[]`，
  `BinaryWriter.Write` 于是只写进 1 个字节（产出 125 字节的坏文件，脚本却打印了正确长度）——
  MISC 的坑位记录里留了这一条。

### M2 验证记录（对话框按钮框架）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx
    → Core 159 / Assets 120 / UI 70，全部通过
      （UI 新增 25 条：ButtonSpriteTests 12 + ButtonGroupTests 13）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -DataDirectory F:\game\fh2
  → match  the rendered menu is identical to the baseline
```

* `ButtonBase` / `ButtonSprite` / `ButtonGroup` 是 `ui_button.cpp` 的对话框按钮：两块命中区
  （released / pressed）跟着 ICN 里的 `OffsetX/OffsetY` 走，禁用外观是 released 过
  `PaletteId.Darkening` 并按精灵缓存（`PAL::PaletteType::DARKENING`）。
* 点击规则与 `LocalEvent::MouseClickLeft( area )` 一致：按下与松开都必须落在区内。测试里三条
  用例分别钉住「都在区内 → 返回值」「松手在外 → `None`」「按下在外 → `None`」，
  另有一条确认拖到邻居不会点亮它（`isMouseLeftButtonPressedAndHeldInArea` 用的是按下位置）。
* 热键走 `ButtonHotKey.Okay` / `ButtonHotKey.Cancel`：YES/OK 答 Okay、NO/CANCEL 答 Cancel；
  只有一个 OK 或 CANCEL 的组两个键都答（`Game::HotKeyCloseWindow()`），而只有一个 YES 的组
  Cancel 返回 `None` —— 与参考实现的判断顺序相同，参考实现在这条分支里也不检查按钮是否禁用。
* `DrawShadow` 直接把 released 精灵交给 `ImageOps.AddGradientShadow`：测试用一块 6×6 全不透明
  的精灵（带 transform 层）断言按钮左上角外的像素过表 5、左下角旁过表 2，方向 -5/+5 与
  `drawShadow()` 相同；没有 transform 层的精灵不投影子（与 `addGradientShadow` 的早退一致）。
* 这套控件还没有调用方：设置对话框（点主菜单的门）要等 `ui_button.cpp` 的文字按钮工厂与
  `ButtonGroup( area, Dialog::OK | Dialog::CANCEL )` 的自动摆位一起移植，和 `StandardWindow`
  一样先作为可测试的构件放着。


### M2 验证记录（按钮美术、自动摆位与设置对话框）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx
    → Core 167 / Assets 120 / UI 108，全部通过
      （Core 新增 8：SpriteOpsTests；UI 新增 38：
        ButtonArtworkTests 12 + DialogButtonLayoutTests 7 +
        DialogButtonTemplateTests 9 + SettingsDialogTests 10）
pwsh tools/scripts/VerifyMenuBaseline.ps1  → match（menu.png 逐像素相同）
FHeroes2Sharp.exe --data F:\game\fh2 --lang en --cursor 620,100
    --open-settings --freeze-animation --smoke-frames 20
    --screenshot tools-output\settings-en.png --screenshot-frame 10
    → Settings dialog opened: language 'en', 2 available, content 300x85
    → 与 menu.png 比较：对话框区域（138,181 348x133）42,679 像素不同，
      区域外 1 像素（状态栏以外）
```

* `ButtonArtwork` 是 `ui_button.cpp` 里生成按钮的那一半：`resizeButton()` 的五支（只变宽、只变高、
  两个方向都变小、变高变窄、两个方向都变大）、`addButtonShine()` 的坐标表与
  `makeTransparentBackground()`。测试用「每个像素写着自己坐标」的 9×10 精灵，把四条拉伸分支的
  源像素映射逐点比对；变大的那支还要确认中空区填的是原图中心像素（`w*h/2-1-min(w,h)/2`）。
* 闪光测试值得记一笔：右侧那条长斜线 `DrawLine( w-8,5 → w-10,7 )` 会**盖掉**先前写下的
  `secondColor`，所以 `(w-8,5)` 最终是 `firstColor`；`(w-10,2)`/`(w-8,3)` 才是纯 `secondColor`。
  第一版测试按「先写的赢」断言，三条用例一起红，正好证明了移植是按顺序执行的。
* `ImageOps` 新增四个「写两层」的原语：`Copy`（三条 transform 规则）、`FillSprite`、
  `SetPixel`、`DrawLine`（整数 Bresenham，`ns = major/2`）。`Core` 的 8 条用例分别钉住
  「双层互拷」「源单层写 0」「目标单层只拷颜色」「裁剪」「越界忽略」「浅斜线每两步进一行」。
* `ButtonGroup.CreateStandard` 摆出 `Dialog::OK | CANCEL` 那种一行：一对按钮的左右各留
  `(area.width - w1 - w2)/4`，单个居中贴底；7 条用例覆盖四支 + 精灵锚点 + 「不支持的类型给空组」+
  「点 OK 返回 Ok」。
* 设置对话框（ADR-027）：`SettingsDialog` = `StandardWindow` + 标题 + 每行「标签 + 右对齐的值 +
  两侧自绘箭头」+ 底部 OK。`SettingsOption` 负责取值与循环，行命中要求按下与松开在同一行
  （与按钮同一规则）。13 条用例，其中一条真的把 640×480 画出来，检查标题色、行文字色都落在窗口
  内容区里；两条钉箭头（几何 + 实际画出的像素），一条钉「行太窄就不画箭头」。
* 接线：点主菜单左下角的门 → `MainMenuScene.OpenSettings()`；Escape 取消、Enter/OK 应用并把
  语言写回 `GameText`；`--open-settings` 让模态框可截图。`SYSTEM.ICN` 第 1/2 帧是 OK 按钮，
  参考实现在 `useOriginalResources()` 分支里用的就是这张图。


### M2 验证记录（设置对话框像素基线）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 error / 0 warning
dotnet test  tests/FHeroes2.UI.Tests         → 111 passed / 0 failed
                                               （新增 3 条：箭头几何 / 箭头像素 / 窄行不画箭头）
pwsh tools/scripts/VerifyMenuBaseline.ps1    → match（menu.png 逐像素相同，拆脚本没动像素）
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Update
  → update tests/baselines/settings.png（231,545 字节，sha256 AFBCFAE…00DE32）
pwsh tools/scripts/VerifySettingsBaseline.ps1 （连续两次）
  → settings.png identical 640x480 7040 pixels ignored → match（退出码 0 / 0）
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Baseline tests/baselines/menu.png
  → 42680 of 307200 pixels differ → DIFF + diff/diff_menu.png（退出码 1）
pwsh … -Baseline tests/baselines/menu.png -Tolerance 200000 → match（退出码 0）
pwsh … -Baseline tools-output/nope.png        → error + 退出码 1（基线不存在）
pwsh … -Baseline tools-output/settings-crop3.png（1050×435）→ error + 退出码 1（尺寸不对）
```

* 拆脚本是**不改行为**的重构：`VerifyMenuBaseline.ps1` 的参数、输出行与退出码一字未变，
  数据目录查找 / build / 基线自检 / compare + 容差进了 `tools/scripts/PixelBaseline.ps1`
  （ADR-028）。证据就是 `menu.png` 与重构前逐像素相同。
* 第一版把值画成 `< English >`——用的是 `FONT.ICN` 自带的 `<` / `>`。截图放大三倍才看出来：
  这两个字形只有 **3×4** 像素，行里只是两个小点。改成自绘像素线（宽 4、高 7、色号 10 =
  `KB.PAL` 字体灰阶最亮的一级）后箭头清楚可读，而且与被括起来的值同色。
* 同一次放大检查确认其余版面是对的：内容区 300×85、标题居中、OK 居中贴底、行距 `RowGap` 6、
  左边距 `RowPadding` 8（+2 px 边框）——所以只改了箭头这一处。
* 语言属于基线：`settings.png` 用 `en`（`--lang en`）；换 `zh_CN` 得到的是另一张图，另存而不是覆盖。
* 与纯菜单的差异 42,680 个像素（状态栏已忽略）：其中 42,679 个落在窗口矩形
  （138,181 348×133）里，只剩 1 个在矩形外（阴影取整的边缘）。把矩形外的四块区域全 `--ignore`
  再比一次，仍然是这 42,679 个——所以「差异就在这张窗口上」是被测出来的，不是估的。


### M2 验证记录（设置行文字裁剪与中文设置基线）

```
dotnet build FHeroes2Sharp.slnx          → 0 error / 0 warning
dotnet test  FHeroes2Sharp.slnx          → Core 167 / Assets 120 / UI 113 = 400，全部通过
                                            （UI 新增 2：超长值丢箭头 / 超长文字留在内边距里）
pwsh tools/scripts/VerifySettingsBaseline.ps1        → settings.png identical（像素未变）
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN
    → 96 translated messages（新增 msgid "Language"）
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN -Update
    → update tests/baselines/settings.zh_CN.png（231,317 字节，sha256 E8E317A…DDD94C4）
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN
    → settings.zh_CN.png identical 640x480 7040 pixels ignored → match（退出码 0）
```

* 先修「行文字越界」：`ValueX` 把值的起点夹到 `row.X + RowPadding`，`Draw` 用 `TextArea()`（行去掉
  左右内边距）当裁剪区。`en` 基线逐像素不变，说明这条是纯防御——译文装得下时一个像素都不动。
* 中文界面上屏后才发现两件事：`zh_CN.po` 里没有 `msgid "Language"`（行标签还是英文），以及
  `settings.png` 是 `en` 专用、脚本却只会写同一个文件名。前者补一条译文（95 → 96），后者按
  `-Culture` 推导基线文件名（ADR-029），CI 因此跑三条命令。
* `settings.zh_CN.png` 里能直接读到「设置 / 语言 / ◀ 简体中文 ▶ / OKAY」：CJK 走
  `CompositeGlyphSource` 的内嵌字体，与 `FONT.ICN` 的拉丁字形同一行基线；OK 仍是 `SYSTEM.ICN`
  第 1 帧（ADR-027 第 4 条）。
* 状态栏（`88 FPS | 简体中文 (F2) | v0.1.0`）继续由 `--ignore 0,469,640,11` 排除在比对之外。
* 新增的两条 UI 用例用 40 个字符宽的假译文（240 px > 200 px 的行）钉住行为：箭头先消失，文字只
  落在这行的左右内边距之间。

### 语言选择对话框：剩余工作（暂停，先做别的）

`src/FHeroes2.UI/Dialogs/LanguageSelectionDialog.cs` 已经能编译，但还没人用它。要收尾还差：

1. **场景接线**（`MainMenuScene`）：用 `StandardWindow.CreateCentered(chrome,
   new Size(LanguageSelectionDialog.ContentWidth, LanguageSelectionDialog.GetRequiredHeight(lineHeight, count)),
   true, frame.Size)` 开窗，`ButtonGroup.CreateStandard(..., DialogButton.Ok | DialogButton.Cancel, artwork)`；
   `DrawFrame` 里画在设置对话框之后；`SetCursor` / `SetLeftButton` / `Select(char)` 加输入路由；
   OK 后 `SetLanguage(dialog.SelectedIndex)` 并回写设置对话框那一行（`SettingsOption.Select`）。
2. **入口**：`F2` 由「直接循环切换语言」改成开这个窗口（开不出来再退回 `NextLanguage()`）；
   新增 `--open-language`（`CommandLineOptions` + `GameHost.CreateScenes` + 帮助文本）供截图。
3. **键盘**：`GameHost.HandleKeyDown` 加 `Up` / `Down` → `MoveSelection(±1)`；Enter / Escape 走
   `ProcessHotKey` / `Cancel`。
4. **按钮贴图**：`SettingsArtwork` 目前只装 `SYSTEM.ICN` 的 1/2 帧（OKAY），要补 3/4 帧（CANCEL）
   并让 `Get(DialogButton)` 按按钮返回对应的一对。
5. **测试**（`tests/FHeroes2.UI.Tests/LanguageSelectionDialogTests.cs`）：行布局、点击选中、拖出行不改、
   OK / Esc、移动选择循环、滚动跟随选择、至少 11 行、选中行与选中名用黄色字体。
6. **文档**：`docs/DECISIONS.md` ADR-031（照参考布局；无滚动条贴图 `SCROLL.ICN`、无双击确认的取舍）、
   `README.md` 的开关列表；若加 `tests/baselines/language.png` 基线，还要补 `VerifyLanguageBaseline.ps1`
   与 CI 一步（照着 settings 那套抄）。
7. **译文**：`translations/zh_CN.po` 加 `Select Game Language:`（现在只有主菜单/设置/状态栏的词条）。

### M2 已知遗留（记下，暂不处理）

* **超宽译文的降级**：目前是「看得见的截断」（ADR-029）。省略号、自动缩字号、按钮专用字体
  （`BUTTON_*_FONT_*`）都没做；真有长译文需求时再回来。
* **设置对话框只有语言一行**：分辨率、音量、动画速度、快捷键都还没有对应的设置模型（M8 的活）。
* **像素基线只钉了默认光标态的 640×480 一帧**：hover / pressed / 右键提示框 / 语言选择窗口都没有
  基线，靠 `tools-output/*.png` 人工核对。
* **evil 界面**：`SURDRBKE` / `WINLOSEE` / `STONEBAK_EVIL` 与邪恶光标未接线（ADR-024）。
* **语言选择窗口**：未接线，见上一节；另外它没有滚动条贴图（`SCROLL.ICN` 未移植）与双击即确认。
* **BGM / 音效**：外部音轨能播（主菜单曲）、音效链路能播（`--play-sound`），但**没有 XMI/MIDI
  合成**（没有 `MUSIC/` 的安装会静音）、**音量无 UI 入口**、**只有一条曲子**（M2 没有别的场景）。
* **平台**：只保证 Windows x64（ADR-025）。



见 [PLAN.md](PLAN.md)。M1 的条目已全部完成，并且已经用**原版 C++ 实现**对拍过
（`tools/scripts/CompareWithReference.ps1`，默认 14 个条目 / 1204 个 BMP 逐字节相同）；
`AssetProbe compare` 仍然保留，用于 PNG 视觉回归与差异图输出。

## M3 — 新游戏/场景设置（进行中）

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `Assets.Maps.Mp2FileInfo` | ✅ | MP2 地图文件**信息块**（前 428 字节）读取，逐字段对齐 `Maps::FileInfo::readMP2Map`：BE 魔数 `0x5C000000`、难度 LE16、宽/高、王国/人类/电脑三组六色、`seek(29)` 起的胜负条件块（类型 + 电脑也算赢 + 普通胜利 + 两个参数）、`seek(58)` 的 16 字节名字、`seek(118)` 的 200 字节描述；派生量 `HumanOnlyColors` / `ComputerOnlyColors` / `SharedColors` / `HumanPlayerCount` 按位集合语义；非法文件抛 `Core.Io.BinaryFormatException`，`TryRead` 吞掉（ADR-032） |
| `Assets.Maps.PlayerColor` / `MapRace` / `GameDifficulty` / `MapVictoryCondition` / `MapLossCondition` / `MapVersion` | ✅ | 头里出现的枚举与换算：颜色位（1/2/4/8/16/32，`color.h`）、种族索引 0…7 → KNGT/BARB/SORC/WRLK/WZRD/NECR/MULT/RAND（`Race::IndexToRace`）、难度 0…4（MP2 只存 0…3）、`MapVersion`（`.mp2` = 原作，`.mx2`/`.hxc` = 资料片） |
| `Assets.Maps.MapCatalog` | ✅ | `Maps::getAllMapFileInfos` + `getValidMaps`：数据目录本身、`maps` 子目录（大小写不敏感），**且只有**该目录含 `Heroes2.agg` 时才看父级（GOG 版 `Data/` 与 `Maps/` 平级）；`*.mp2`/`*.mx2` 去重、按路径排序；按人类玩家数过滤（人类独占色多于需求、或共享色不够都被丢掉），人类独占色正好等于需求时收窄可选颜色；被丢的文件进 `MapScanResult.Rejected` |
| `AssetProbe maps` | ✅ | 打印 `文件名 / 场景名 / 宽x高 / 难度 / 玩家数与颜色 / 种族 / 胜负条件`，末尾打「延迟的英雄落败改写」张数 |
| `tests/FHeroes2.Assets.Tests/Mp2FileInfoTests` + `MapCatalogTests` | ✅ | 19 个用例：合成 MP2 头覆盖每个字段、魔数/难度/无人类颜色/无名字/截断文件/缺文件的拒绝路径、`forEditor`、颜色子集与收窄、种族索引换算、目录搜索与排序、`.mx2` 版本、真实数据（本机 112 张）抽样 `Beltway` |
| `UI.Dialogs.ScenarioSelectionDialog` | ✅ | 照 `dialog_selectscenario.cpp` 的布局：`REQSBKG.ICN` 0/1 帧面板 + 阴影贴在 `(W-377)/2, (H-458)/2`；5 个尺寸按钮（`REQUESTS.ICN` 9…13，`+36/23`，步长 62，`ALL` 默认按下）；列表 9 行按 `i*175/9` 截断分布（19/20 交替，**不是**固定 19，见「修正记录」），行内玩家数（19+人数 → 25 上限）、尺寸（26…29）、类型徽章、居中名字与胜负条件图标（30+2t / 36+t）；详情区选中图标 45/64/83、难度标签右对齐 210、难度值 220、描述 42,318 292×90 换行；`ESCROLL.ICN` 3 帧作滑块，点击行、拖滑块、点击尺寸按钮都会重建/重定位；OKAY 走 `SYSTEM.ICN` 1/2 帧 |
| 新游戏接线（`NEW_GAME` → 场景选择） | ✅ | `MainMenuScene.OpenScenario()` 惰性调用 `MapCatalog.ReadAll`（本机 112 张，缓存整个会话）；`SetCursor` / `SetLeftButton` / `Select` 加模态路由；`GameHost` 转发 `Up/Down/PageUp/PageDown/Home/End` 给 `MoveScenarioSelection` / `MoveScenarioPage` / `SelectFirst…`；`--open-scenario` 供截图冒烟；有模态窗口时 Esc 先关窗口（顺带修掉「设置对话框打开时 Esc 直接退出客户端」） |

### M3 验证记录（地图文件头）

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 162 / UI 113 全绿（新增 19）
dotnet run --project tools/FHeroes2.AssetProbe -- maps --data F:\game\fh2
# F:\game\fh2\Data: 112 playable maps, 1 skipped
Alteris.mp2     Alteris         108x108 Hard    players 3: human Blue / computer Yellow, Purple …
Beltway.mp2     Beltway          72x72  Normal  players 3: human Red, Yellow, Purple / computer Red, Yellow, Purple …
Betrayal.mp2    Betrayal        144x144 Normal  players 6: human Blue, Green / computer Blue … Purple win DefeatOtherSide
DdDragon.mp2    Dead Dragons    144x144 Hard    players 6: human Red / computer Blue … Purple     win CaptureTown loss LoseTown
WIZLAND1.MP2    Wizard's Land 1  72x72  Expert  players 2: human Blue / computer Orange          win KillHero    loss LoseHero
Slugfes.mp2     ¼¤Õ½            36x36  Normal  players 6: human Blue … Purple                   win DefeatEveryone
…
# maps with a deferred hero-loss victory rewrite: 4
```

* 唯一被跳过的是 `GateHell.mp2`（人类独占色 2 种，而扫描默认按 1 名人类玩家算）；
  `FHEROES2_LOG_LEVEL=Debug` 时 `[DEBUG] Map '…\maps\GateHell.mp2' requires 2 human players and is skipped.`
* 画面外的核对：`Beltway` 72×72 / `Normal` / 3 个王国 / `DefeatEveryone`、`DdDragon` 144×144 且
  失败条件是 `LoseTown`，与原版场景选择框里看到的摘要一致。

### M3 验证记录（场景选择窗口）

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 162 / UI 137 全绿（新增 24）
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --smoke-frames 10 --freeze-animation \
    --open-scenario --screenshot tools-output\scenario-zh.png --screenshot-frame 6
# [INFO] Scenario selection opened: 112 maps, 112 listed, 'Alteris' selected.
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Update
# update tests/baselines/scenario.png  sha256 dcebe408…66654c
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Culture zh_CN -Update
# update tests/baselines/scenario.zh_CN.png  sha256 ef5b5fa5…2e28ac
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -SkipBuild
# verify tests/baselines/scenario.png → identical
```

* 画面外核对：112 张地图全部列出，默认选中排序第一的 `Alteris`（108×108、`Hard`、3 个王国）；底部
  详情区的名字、难度、描述与列表行的玩家数/尺寸/胜负图标都与 `AssetProbe maps` 的输出一致。
* 尺寸按钮是 `ALL` 按下态，列表里 `S/M/L/XL` 四种尺寸都出现；点 `XL` 后只剩 144 图，再点 `ALL` 恢复。
* `MAP_TYPE_ICON` 并不存在于归档里，参考是在启动时从 `ARTFX.ICN` 6/90/101 缩放到 15×15 合成的；
  `ScenarioArtwork` 做同一件事（扩展包 `Heroes2x.agg` 才有 90/101 帧，纯原作数据只会显示 SW 徽章）。
* `zh_CN` 基线证明 `地图难度：困难` 与描述区的英文地图原文能同框显示：界面词条走 `.mo`，地图描述
  是文件里的原文，不做翻译。
* 顺手修掉一个 M2 遗留的输入缺陷：设置对话框打开时按 Esc 会直接退出客户端；现在先关窗口，再关菜单。

### M3 修正记录（列表行坐标与地图文本解码）

第一版把 `MAP_LIST_ROW_SPACING_Y = 4` 当成了「图标离行顶 4 px」并把行高写成固定 19 px，结果整块
内容比参考低 4 px、最后一行被列表框下沿吃掉 2 px，点击命中也跟着偏。两处都按参考改回：

* **行坐标**：`RowArea(i)` 用 `i*175/9`（0/19/38/58/77/97/116/136/155），高度取相邻两条边界之差
  （19 或 20），`RowAt` 自然跟着对齐；图标不再下移（`RowIconOffsetY = 0`），名字回到行顶 +3
  （`MAP_LIST_ROW_SPACING_Y - 1`）。UI 测试新增「第 3/8 行起点」与「逐行点中心选中该行」两条。
* **地图文本编码**：`MapTextCodec` 取代裸 `Encoding.Latin1`，顺序是 ASCII → UTF-8（严格解码且真的
  含多字节）→ GBK（严格解码 + 回环一致 + 至少 2 个汉字）→ Latin-1。中文图包的名字与描述现在能
  正常显示（`Slugfes.mp2` 显示「激战」、`Mineral.mp2` 显示「矿山之战」），`Château` 这类 CP1252
  单词不会被误判成汉字（只有 1 个汉字时不信 GBK）。列表按新名字重排，`scenario*.png` 基线随之重建。
* **`--scenario-select <n>`**：截图入口的一个小扩展，开窗并选中第 n 张图，用来给任意一行（尤其是
  排在末尾的中文图）拍基线；`tools-output/scenario-cn.png` 就是这么核对「领主」那一屏的。

```
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 169 / UI 139 全绿（新增 7 条编码用例）
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --scenario-select 110 --smoke-frames 30 \
    --freeze-animation --screenshot tools-output\scenario-cn.png
# 列表滚到底、选中「领主」，名字/难度/描述与 AssetProbe maps 一致
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Update            # sha256 278483e2…a2e25
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Culture zh_CN -Update  # sha256 fca0dc30…83bc67
```

### M3 修正记录（列表列的 x 基准）

上一版把列表行的**所有**列都从列表区左边缘（`rt.x + 55`）算起，于是玩家数/尺寸/类型图标、名字列、
胜利与失败图标整体右移 55 px：名字挤到列表右半边，失败图标落在滚动条上（`rt.x+285+55 = 340` 与
槽 `rt.x+328` 重叠 12 px）。原版截图里名字明显靠左、两列条件图标与滚动条之间留空，就是这处差异。

根因在参考的 `ScenarioListBox`：

```cpp
explicit ScenarioListBox( const fheroes2::Point & pt )
    : Interface::ListBox<Maps::FileInfo>( pt )
    , _offsetX( pt.x )   // 对话框的 x，不是列表区的 x
{}
```

`RedrawItem` 只把**行 y**（`rtAreaItems.y + i*175/9`）传下去，`_renderScenarioListItem` 里的
`_offsetX + 48/67/86/104/266/285` 全部以对话框左边为基准；`SetAreaItems( { rt.x + 55, … } )` 的 55
只参与点击命中与滚动。两者基准不同是参考本来的写法，不是笔误。

* `DrawRows` 的图标原点改成 `_area.X`，名字矩形改成 `_area.X + RowNameOffsetX`，行 y 仍取 `RowArea(i).Y`；
  `RowArea` 保留为**点击区域**（`rt.x+55, 270, 175`），注释写明它为什么不能直接拿来画。
* `Drawing_PaintsTheIconsOfEveryRow` 原来用 `row.X` 取像素，等于把这个 bug 写进了断言，改成
  `dialog.Area.X`；`RowArea` 的 XML 注释补上「绘制用面板 x、命中用列表 x」。
* 基线重建：`scenario.png` → `3312FB28…`，`scenario.zh_CN.png` → `5CB8299F…`，两条 verify 均 identical。

### M3 修正记录（列表滚轮、滑块拖拽与箭头语义）

上一版的列表只能靠方向键、PageUp/PageDown 和上下箭头滚动：槽里的滑块**画**出来了却没人接鼠标，
滚轮事件在宿主层根本没有分支（`GameHost.PumpEvents` 只处理 Motion / Button / Key），所以右键菜单
外的滚轮整台机器都不响应。三处都补上：

* **拖拽**（`ScenarioSelectionDialog`）：按下时若指针在 `ScrollBarArea` 内就进入拖拽态并立刻按
  `Scrollbar::moveToPos` 的公式定位（`offset = clamp(y - thumb/2 - groove.y, 0, travel)`，
  `index = (steps * offset + travel / 2) / travel`），点槽空白处等同于跳转到该段；拖拽中的位置
  来自 `SetCursor`（SDL 拖动时只有 Motion），释放时跳过行命中，避免松手误选一行。
  滑块长度与绘制共用 `CreateThumb()`，保证手感与看到的一致。
* **滚轮**：`ScrollBy(rows)` 只改可见顶部（对应参考的 `--_topId` / `++_topId`），不动选中，并按
  `isMouseWheelInArea` 的规则只在指针位于列表或滚动条内时生效；`GameHost` 新增
  `SDL.EventType.MouseWheel` 分支（`IntegerY` 优先、`Y` 的符号兜底），方向取反后转给
  `MainMenuScene.ScrollScenario`。
* **箭头按钮**：上一版点一下就走 `MoveSelection(±1)`，改的是选中行；参考的 PgUp/PgDn 分支只动
  `_topId`，于是同一个"滚动"动作出现两套语义（箭头带走选中、滚轮/拖拽只滚视图）。改成
  `ScrollRows(±1)` 后三者一致：拖的是视图，选中只由点击行和键盘改变。`ScrollBy` 负责光标区域
  判断，`ScrollRows` 是唯一的夹取点。
* UI 测试新增 6 条：滚轮只滚视图不动选中、滚轮上下都夹在范围内、指针在列表外时滚轮无效、
  点槽底部跳到最后、按住滑块拖到底并确认松手不选行、点上下箭头只滚视图不动选中；
  UI 测试 139 → 145。

```
dotnet build -c Release → 0 警告 / 0 错误
dotnet test  -c Release → Core 167 / Assets 221 / Game 86 / UI 145 全绿
VerifyScenarioBaseline.ps1 (-SkipBuild, en + zh_CN) → identical
dotnet run -- --data F:\game\fh2 --open-scenario --smoke-frames 20 → 112 maps listed, 截图正常
```



### M3 已知遗留

* ~~**地图名按 Latin-1 解码**~~：已在「M3 修正记录」里用 `MapTextCodec` 解决（GBK 探测，见 ADR-034）。
  仍不可靠的只剩「既是合法 GBK 又解出 ≥2 个汉字的 CP1252 文本」这一类，实际地图里没碰到。
* **英雄落败改写没做**：`readMP2Map` 对「单人类玩家 + 失去某英雄即败 + 该英雄站在电脑色格子上」会把
  胜利条件改写成 `VICTORY_DEFEAT_OTHER_SIDE` 并结盟，这需要读 tile 数据与 `HEROES.ICN` 前 42 帧的
  颜色分组；本机 4 张图受影响（`WIZLAND1/2/3`、`Whoam`），`AssetProbe maps` 末尾会打张数，避免静默分叉。
* **只读信息块**：tile / addon / 城堡 / 英雄 / 事件结构都没有解析，真正进冒险地图（M4）要另写地图加载器。
* **`.fh2m` 不支持**：fheroes2 自己的地图格式没有移植，`MapVersion.Resurrection` 只是占位。
* **难度选择、`NEW GAME` 分叉**（标准局 / 战役）：`NEW_GAME` 现在直接开场景选择，跳过了参考里的
  「标准游戏 / 战役」分叉与 `Setup` 对话框；难度只在列表里只读显示，不能改（要等 M4 的地图加载器）。
* **场景列表的几处简化**：没有双击确认、没有 Debug 版的 Delete 删文件；滚动条上下箭头只画
  `REQUESTS.ICN` 5/7 帧作装饰，点击无效（滑块可拖、滚轮可用）；`MAP_TYPE_ICON` 徽章缩放用的
  最近邻与参考的 `Resize` 取样点可能差 1 px（视觉无差别）。
* **显示分辨率固定 640×480**：场景窗口按 `frame.Size` 居中，放大分辨率要等 M8 的设置项。

## M4 — 冒险地图（进行中）

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `Assets.Maps.Mp2TileInfo` | ✅ | 20 字节 tile 结构，逐字段对齐 `MP2::loadTile`：地形图索引、地面对象（层 + ICN 类型 + 帧）、`quantity1/quantity2`（派生 13 位 `Metadata`）、顶部对象、地形标志（低 2 位 shape）、`mapObjectType`、addon 链头、两级对象 UID |
| `Assets.Maps.Mp2AddonInfo` | ✅ | 15 字节 addon 结构；`objectNameN1` 读取时 ×2，与参考一致（`mp2_helper.cpp` 注释照抄，避免后人来「修」） |
| `Assets.Maps.Mp2ObjectType` / `Mp2ObjectTypes` | ✅ | `MP2::MapObjectType` 全量移植（0…127 非动作、+128 动作、256+ 资料片），连同 `IsActionObject` / `BaseActionType` / `NeedsExtendedMetadata` / `ContainsMetadata` 四个判定 |
| `Assets.Maps.Mp2ObjectIcnType` | ✅ | `MP2::ObjectIcnType`（`UNKNOWN`…`X_LOC3`，共 64 项）与 `Unknown` 判定；数值是地图格式的一部分，不许重排 |
| `Assets.Maps.Mp2Map` | ✅ | 结构层全量读取：tile 区 → addon 计数与数组 → 城堡表（72×3）→ 捕获物表（144×3）→ 方尖碑计数 → 信息块计数列表（`256*high+low-1`，`00 00` 终止）与块（LE16 长度，0 长度跳过）→ 尾部对象 UID；尺寸必须正方形且为 36/72/108/144；`65/193 → 农夫小屋`；`rejectPriceOfLoyaltyObjects` 复刻参考对原作图的 PoL 拒绝；`EnumerateAddonChain` 对越界索引与环都设了上限 |
| `AssetProbe map <file>` | ✅ | 打印摘要、对象类型直方图、地形图/`shape` 分布、城堡与捕获物表、最长 addon 链、信息块尺寸分布；定位「读错偏移」的第一现场 |
| `tests/FHeroes2.Assets.Tests/Mp2MapTests` + `Mp2MapFileBuilder` | ✅ | 17 个用例：合成完整地图（默认空图）覆盖每个字段与每条拒绝路径（尺寸、截断、PoL、越界点）、`65/193` 归一化、addon 链与环、城堡种族（含 `0x80` 城墙位与未知种族）、捕获物映射、空信息块；末尾一条在真实数据上读 `Beltway.mp2`（缺数据即跳过） |
| `Game.AdventureMap.TerrainTileSet` | ✅ | 载入 `GROUND32.TIL` 并预生成 4 个 shape（原图 / 上下 / 左右 / 两者），与参考 `getTileImage(TIL::GROUND32, index, shape)` 的 `Flip(horizontal = shape&2, vertical = shape&1)` 一致；越界索引返回 `null`（留黑，不崩） |
| `Assets.Maps.Mp2ObjectIcnTypes.GetArchiveName` | ✅ | `MP2::ObjectIcnType` → AGG 条目名（`OBJNGRAS.ICN` 等），即 `ICN::getIcnIdFromObjectIcnType` + `ICN::GetString`；`X_LOC*` 需要资料片；`Unused*` / `Unknown` 返回 `null`；附 `IsDrawnAsObjectPart`（`isObjectPartDirectRenderingRestricted`）排除 `UNKNOWN`/`BOAT32`/`MINIHERO`/`MONS32` |
| `Game.AdventureMap.IObjectSpriteSource` + `ObjectSpriteSet` | ✅ | 按 (ICN 类型, 帧号) 取图；`ObjectSpriteSet` 懒加载并缓存 ICN，缺失/损坏记一次就够（不让每 tile 重试）；接口让绘制逻辑可以脱离游戏数据测试 |
| `Game.AdventureMap.AdventureMapView` | ✅ | 像素级滚动 + clamp（`max(0, map - viewport)`）、`TileAt`、`VisibleTileArea`（含被探到一半的边界 tile）、`CenterOn`；绘制按**层**遍历而不是按 tile：地形 → 地面部件 3/1/2/0 → 顶部部件，与 `Interface::GameArea::Redraw()` 一致（升序会把道路画到树和城镇上面）；部件按 ICN 帧自带锚点落位（`BlitOnTile(..., sprite.x(), sprite.y())`） |
| `App.Scenes.AdventureMapScene` | ✅ | 地形 + 对象层 + 底部 `SMALFONT` 状态栏（地图名/尺寸、视口偏移、版本）；无英雄时把视口居中到「最靠地图中心的城堡」（占位规则，见 ADR-035）；地图或 TIL 读不出来时回到主菜单并在状态栏留信息，不会黑屏 |
| `GameHost` 接线 | ✅ | `NEW GAME → 场景窗口 → OKAY` 现在真的进入冒险地图；方向键按 tile 滚动、`Home` 回左上、`Esc` 回主菜单；`--enter-map`（配合 `--scenario-select`）与 `--map <file>` 两个截图/冒烟入口；鼠标事件在冒险地图上不再转给菜单 |
| `tests/FHeroes2.Game.Tests/ObjectLayerTests` + `Mp2ObjectIcnTypesTests` | ✅ | 9 个绘制用例（地面/顶部/层序/锚点/空帧/缺帧/无源/跨 tile 覆盖）+ 19 个 ICN 命名用例（枚举全覆盖不重名、PoL 开关、可绘制集合） |
| 像素基线 `adventure-map.png` | ✅ | `--map Alteris.mp2` 直接开图，居中到地图中部城堡，忽略状态栏 `(16,466,608,11)`；`VerifyAdventureMapBaseline.ps1` 与另外四条基线共用 `PixelBaseline.ps1` |
| `Assets.Maps.Mp2MapHero` / `Mp2MapHeroes` | ✅ | 英雄从 tile 反解而不是读信息块：`MINIHERO` 帧号 `/7` → 颜色（`heroColorsOrder`）、`%7` → 种族（`Race::IndexToRace`），越界抛 `BinaryFormatException`；`Mp2Map.Heroes` 按 tile 顺序列出 |
| `Game.AdventureMap.IHeroSpriteSource` + `HeroSpriteSet` | ✅ | 按 (颜色, 种族) 取三张图集：身体 `KNGT32`/`BARB32`/`SORC32`/`WRLK32`/`WZRD32`/`NECR32`，旗帜 `B-FLAG32`…`P-FLAG32`，阴影 `SHADOW32`，一律取第 18 帧（朝右站立）；`Random` 种族没有身体，只出旗帜（ADR-037） |
| `Game.AdventureMap.IcnSpriteCache` | ✅ | 对象层与英雄层共用的「按文件名解码一次」缓存，`ObjectSpriteSet` 改为复用它 |
| `AdventureMapView` 英雄三带绘制 | ✅ | `AboveTile` / `OnTile` / `BelowTile` 按 32 px 行带切精灵，分别在地面层 `SHADOW_LAYER` 之后、四层全部画完之后、顶部层之后绘制；阴影单独一批、位于对象之下；可见性判定左右各放 2 格 |
| `tests/FHeroes2.Game.Tests/HeroLayerTests` + `HeroSpriteSetTests` + `Mp2MapHeroesTests` | ✅ | 8 条绘制用例（盖住本格对象、上带/下带行位、阴影先于对象、缺旗帜仍有身体）+ 18 条图集映射用例（6 种族 / 6 色 / 缺省 / 真实数据负锚点）+ 16 条帧号解析用例（颜色、7 帧种族、越界、tile 过滤、整图收集） |
| `tools/FHeroes2.AssetProbe map` 英雄列表 | ✅ | 打印 `x,y 颜色 种族 MINIHERO 帧号`，`Alteris.mp2` 9 名英雄（1 黄野蛮人 / 2 蓝 / 6 紫亡灵） |
| `Game.AdventureMap.MapGround` + `MapGrounds` | ✅ | `Maps::Ground` 的移植：九个 `*_START_IMAGE_INDEX`（30/92/146/208/262/321/361/415/432）把 terrain image index 反解成地面类型，越界给 `Unknown` |
| `Game.AdventureMap.Minimap` | ✅ | `Interface::Radar` 的数据与缩放：每格一个调色板索引（地面色 / 山树 +3 / 道路 `0x7A` / 英雄王国色）、`Zoom = 144 / 宽`、「每 6 像素省一个」的虚线视口方框、视口覆盖全图时不画方框；输出 144×144 `PixelBuffer`（ADR-038） |
| `AdventureMapScene` 雷达叠加 | ✅ | 雷达画在参考坐标 `(480,16)`，1 px 黑框圈住 |
| `Game.AdventureMap.MapFrame` | ✅ | `ADVBORD.ICN` 一张 640×480 图 + 参考布局：视口 `(16,16,448,448)`、雷达 `(480,16,144,144)`、面板 `(480,160,144,320)`、底边框带 `(16,464,608,16)`；`Load` 读第 0 帧，缺图时 `Draw` 画平色兜底；`DrawPanel` 照 `Interface::IconsBar::redrawBackground` 把 `(480,176,72,32)` 的砖块往下、往右平铺满面板列（ADR-054） |
| `AdventureMapScene` 外壳 | ✅ | 地图先画进 448×448 的 `_viewportPixels` 再贴到 `(16,16)`，`ADVBORD` 边框、雷达槽、面板砖纹都来自同一张图；状态栏移到底边框带里（`(16,466,608,11)`，深色条 + `SMALFONT`），基线忽略区随之改成这块矩形；`EditorScene` 改用同一个 `MapFrame`，`editor.png` 逐像素不变 |
| `tests/FHeroes2.Game.Tests/MinimapTests` | ✅ | 35 条用例：19 条地形索引分段 + 8 条调色板常量 + 道路/山树/未知地面/英雄染色 + 缩放块对齐 + 方框几何（含贴边裁剪、全图时隐藏）+ 虚线步长 + 面板落位；另 2 条 `TileAt` 的整数除法（72 / 108 格两种缩放、越界不 clamp） |
| `Game.AdventureMap.MapScrollController` | ✅ | 冒险地图的鼠标：`GameArea::QueueEventProcessing` 的拖动（10 px 严格阈值、位移 `last - current`、从按下点补位移）、`HumanTurn` 的 16 px 边框滚动（20 ms 一步、`2 << ScrollSpeed` = 8 px、拖动期间禁止、单帧最多 4 步）、滚轮（参考没有，自定一格 tile） |
| `Minimap.TileAt` | ✅ | `Radar::QueueEventProcessing` 的面板像素 → tile 反算 `( pt - rect.pos ) * world / rect.size`，整数除法，越界交由视图 clamp |
| `AdventureMapScene` 鼠标接线 | ✅ | `SetCursor` / `SetLeftButton` / `ScrollByWheel` / `Update`；按下落在雷达上就交给小地图（点击即居中、按住继续跟随，参考把两者放在同一分支），其余交给拖动；`GameHost` 的三处 `_adventure is null` 守卫改成转发，左键与滚轮都进地图 |
| `tests/FHeroes2.Game.Tests/MapScrollControllerTests` | ✅ | 12 条：10 px 不算拖 / 11 px 算拖、方向、clamp、半 tick 不滚、角落双轴、拖动期间禁止边缘滚动、贴边返回 false、滚轮一格与 clamp |
| `tests/FHeroes2.Game.Tests/MapFrameTests` | ✅ | 7 条：640×480 下视口/雷达/面板/底边框带的四个矩形、面板底到画框边缘、小画框同比缩进、缺图时 `Draw` 铺满整屏与每个 bay、缺图时 `DrawPanel` 不动像素 |
| `Game.AdventureMap.IconBar` + `IconSpriteSet` | ✅ | `Interface::IconsPanel` 的布局与落位：英雄列 `(480,176,56,128)`、城堡列 `(552,176,56,128)`、每格 56×32、图标内缩 `5,5`；帧表照 `fheroes2::getCastleIcnIndex`（`LOCATORS` 9…14 城堡 / 15…20 城镇）与空槽 `1 + i % 8`；随机种族返回 `null` 退回空槽（ADR-055） |
| `AdventureMapScene` 图标条 | ✅ | 砖纹之后画城堡表的前四座城堡（右列）、左列四个空槽——MP2 的 hero tile 没有英雄身份，`MINIPORT` 取不了帧；图标缺失时该格留砖纹 |
| `Game.AdventureMap.ButtonsPanel` + `ButtonSpriteSet` | ✅ | `Interface::ButtonsPanel` 的布局与落位：面板 `(480,320,144,72)`——图标列（176…304）下再空一条 16 px 砖边；4 列 × 2 行、每格 36×36、帧对 `2i / 2i+1`（`ADVBTNS.ICN` 的 16 帧）；禁用态是 released 帧套 `PaletteId.Darkening`（`ButtonBase::_getDisabled`）；移动按钮三套帧 2/3、16/17、18/19，本资材只有前 16 帧，缺帧留空，与参考 `Assets::getImage` 越界返回 `errorImage` 一致 |
| `Game.AdventureMap.AdventureButtonStatus` | ✅ | `_setButtonStatus` 的数据化：五个无条件启用的按钮不占标志位，只记移动 / 施法 / 下一个英雄的启用与移动按钮的三个含义；`WithoutHero` 就是「没有聚焦英雄」的全 false 分支 |
| `AdventureMapScene` 按钮面板 | ✅ | 图标条之后画八个按钮，状态取 `WithoutHero`——MP2 的 hero tile 没有英雄身份 |
| `tests/FHeroes2.Game.Tests/ButtonsPanelTests` | ✅ | 11 条：面板与每格矩形、八个帧对、移动按钮三帧、`WithoutHero`、启用/禁用像素、按 `HeroMovementIcon` 取帧、缺帧留空、无精灵源不画；另 2 条真资材：36×36 与 16 帧、`DARKENING` 只改副本 |
| `Game.AdventureMap.IMonsterSpriteSource` + `MonsterSpriteSet` | ✅ | `ICN::MINI_MONSTER_IMAGE` 的移植：帧号 `GetSpriteIndex() * 9` 即 `(id - 1) * 9`（站立帧，`UNKNOWN` 为 0 没有帧），`MINIMON.ICN` 走与对象层共用的 `IcnSpriteCache`；`id ≤ 0` 或越界给 `null` |
| `AdventureMapView` 怪物三带绘制 | ✅ | 锚点 `MonsterAnchor = (16,30)`（`monsterImageOffset`）；与英雄一样按 32 px 行带分三批：`SHADOW_LAYER` 之后画瓦片下方部分、`OBJECT_LAYER` 之后画瓦片内部分、`TopLayer` 之后画伸到上一行的部分；可见格取视口左右各放 1 格（`ScanAroundObject`） |
| `tests/FHeroes2.Game.Tests/MonsterLayerTests` | ✅ | 9 条：瓦片内绘制、下方行先于 ObjectLayer、上方行后于 TopLayer、被同格顶层对象遮挡、按瓦片怪物取帧、无精灵源不画、锚点 `(16,30)`；另 2 条真资材：帧号 `(id-1)*9`、农民 31×28 与 66 有帧 / 67 无帧 |
| `Game.AdventureMap.IBoatSpriteSource` + `BoatSpriteSet` | ✅ | `getBoatSpritesPerTile` / `getBoatShadowSpritesPerTile` 的移植：`BOAT32` 第 18 帧（`RIGHT`，62×56，offset `(-8,-54)`）与 `BOATSHAD` 第 18 帧（74×28，offset `(-38,-28)`）；地图里的空船永远是这个帧，参考在装载时把编辑器的 `OBJNWAT2` 帧换掉 |
| `AdventureMapView` 船层与船影子 | ✅ | 锚点 `BoatAnchor = (0,21)`（`tileHeightPx - 11`）；船体按 32 px 行带三批画，影子整张随 Shadow 层画；可见格放 2 格（船体向上伸 33 像素）；有船精灵源时不再画瓦片上的编辑器材 |
| `tests/FHeroes2.Game.Tests/BoatLayerTests` | ✅ | 9 条：船的批次落位、编辑器帧被替换、下方行先于 ObjectLayer、上方行后于 TopLayer、影子先于船体、无精灵源保留编辑器帧、锚点 `(0,21)`；另 2 条真资材：两个帧的尺寸与偏移、无数据时没有集合 |
| `Game.AdventureMap.MapFog` | ✅ | `Maps::Tile` 的雾状态与 `Maps::ClearFog` 的圆盘：每格一个「已探索颜色」字节，`IsFogged` 即 `( explored & colors ) == 0`（对应 `Tile::isFog` = `( fog & colors ) == colors`）；`Reveal` 用 `getSquaredScoutingRadiusLimit`（`(d+1)^2-d`，例外 `7→66`、`8→90`）；`UpdateDirections` 是 `updateFogDirectionsInArea` 的整图版本，`Unknown` 表示对该颜色可见（ADR-061） |
| `Game.AdventureMap.FogDirection` / `FogFrames` | ✅ | `DIRECTION_*` 位与复合掩码（`All`、`CenterRow`、四角等）；`Resolve` 按参考 `drawFog` 的 if/else 原始顺序选帧：整瓦 `( column + row ) % 4`，其余落 `CLOP32` 的 34 帧边界表，`Flipped` 对应 `BlitOnTile( ..., revert )`；非法掩码回退整瓦 |
| `Game.AdventureMap.IFogSpriteSource` + `FogSpriteSet` | ✅ | `CLOF32.TIL`（4 张 32×32）走 `TilFile`、`CLOP32.ICN`（34 帧）走共用的 `IcnSpriteCache`；镜像用 `ImageOps.Flip` 并保留 offset；缺数据或解码失败给 `null`，视图不画雾 |
| `AdventureMapView` 雾层 | ✅ | `Draw` = 地形 → 精灵 → 雾；整瓦 `ImageOps.Blit`、边界件落位 `x = tileWidth - sprite.OffsetX - sprite.Width`；雾后对每格重画 `OBJNTWBA`、城堡再画 `OBJNTOWN`（`DrawObjectParts` 查 add-on 链与瓦片自身，只画 `GroundIcnType` 匹配的部件）；英雄/怪物在 `IsUnderFog` 时跳过，船用 `IsUnderFogWithUpperTile`（两格都被遮才跳过） |
| `AdventureMapScene` 初始侦察 | ✅ | 观察者取 `map.Info.HumanPlayers`；地图装进 `World.Current` 后由 `World.ClearFog( observers )` 揭示英雄圆盘，聚焦城堡再按半径 5 揭示一次（MP2 城堡无颜色，`AllCastles::Scout` 只能占位）；无人类玩家的地图不传 `MapFog`，不画雾（ADR-131） |
| `tests/FHeroes2.Game.Tests/MapFogTests` + `FogFramesTests` + `FogSpriteSetTests` + `FogLayerTests` | ✅ | 57 条：圆盘几何与多颜色语义、地图边缘的雾掩码、`Unknown`、34 帧判定顺序与 `Flipped`、真资材的 4 张整瓦与 34 帧 offset、整瓦变体、已探索格不被覆盖、雾下英雄不留痕、城镇窗口在雾上重画 |

### M4 界面外壳 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 166（+7）/ UI 145 全绿
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update → adventure-map.png sha256 881A597C…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,Editor}Baseline.ps1 → 四条 still identical
  → adventure-map-en.png identical 640x480 6688 pixels ignored（= 608 × 11，新的状态栏矩形）
```

* 三处观察：
  1. **参考是「画满屏再盖边框」，这里是「画进视口再贴过去」**：`Interface::BorderWindow::Redraw`
     把 `ADVBORD` 切成四条边贴到显示器的边上，地图本身由 `GameArea` 画满整屏。我们的逻辑帧正好等于
     一张图（640×480），所以先往 448×448 的缓冲里画地图、再贴到 `(16,16)`，屏幕上的像素与参考一致，
     滚动算术还留在视口的坐标系里，不必做四边裁剪。
  2. **面板列在参考里有两套起点**：编辑器面板从雷达底（160）开始 `Interface::EditorInterface::reset`；
     冒险地图的图标条从 176 开始（`Interface::IconsBar::redrawBackground` 的 `radarWidthPx +
     2 * borderWidthPx`），中间 16 px 是空档。`MapFrame.Panel` 取 160 给编辑器用，`DrawPanel` 取 176
     开始平铺，两者都不越界。
  3. **状态栏搬了位置**：原来是整屏底边 `(0,469,640,11)`，现在是底边框带里的 `(16,466,608,11)`，
     基线忽略区跟着改；`editor.png` 因为 `EditorScene` 换用同一个 `MapFrame` 而必须逐像素不变——复核
     确实 identical，说明抽出的几何与原来的 `RadarBay`/`PanelBay` 完全等价。

* 一处遗留照旧：面板里还没有图标条、按钮与资源栏，砖纹只是把空槽铺成参考的底色；也没有雾与路径。
  （图标条在紧接着的下一轮补上了，见下方「M4 面板图标条」；雾在「战争迷雾」一节补上。）

### M4 面板图标条 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 175（+9）/ UI 145 全绿
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --map map\Alteris.mp2 --screenshot …
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update → adventure-map.png sha256 928C4C3A…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,Editor}Baseline.ps1 → 四条 still identical
  → adventure-map-en.png identical 640x480 6688 pixels ignored
```

* 三处观察：
  1. **`LOCATORS.ICN` 的帧分段是「格底板 / 空槽 / 城堡 / 城镇」**：帧 0 是 56×32 的整格底板（砖纹已经
     铺过，不用），1…8 是 46×22 的空槽框（一色一帧），9…14 与 15…20 是六族的城堡与城镇。参考的
     `getCastleIcnIndex` 对随机种族给 `25 / 26`，但那两帧文件里没有（是编辑器的「随机城堡」选项图），
     所以这里返回 `null`、该格退回空槽。
  2. **右列是地图数据直接给的**：`Alteris` 的城堡表前四座按 `RawType` 分出「城堡 / 城镇」与种族，
     画出来正好是四种不同的要塞图标；左列四个空槽是 `1 + i % 8`，前四帧分别是四个玩家色的框。
  3. **英雄列空着不是漏画**：MP2 的 hero tile 只存 `MINIHERO` 的帧号（32×50 的地图精灵），没有英雄
     身份；参考的小肖像走 `MINIPORT`，按身份索引。把 32×50 塞进 46×22 会压扁，等 `MP2::HeroInfo`
     解析出来再换。
* 基线：`adventure-map.png` 从 `160,793` 变 `168,802` 字节（右列四个城堡图标 + 左列四个空槽），
  `editor.png` 与其余四条逐像素不变——`IconBar` 只在 `AdventureMapScene` 里调用。

### M4 验证记录（结构层、地形层与对象层）

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 205 / UI 139 / Game 24 全绿（新增 51）
dotnet run --project tools/FHeroes2.AssetProbe -- map F:\game\fh2\maps\Alteris.mp2 --limit 8
# Map Alteris: 108x108 tiles, 11664 tiles, 1684 add-ons (1239 tiles with add-ons),
# ground objects 4480, top objects 866, castles 12, capturable 59, obelisks 7,
# information blocks 48, last object UID 4076.
# Object types: ActionMonster 2335 / Mountains 449 / Trees 238 / …
# Terrain images (206 distinct): 18 → 1408 tiles, 16 → 1349, 19 → 1333, …
# Terrain shapes: 0 → 10435 tiles, 1 → 510, 2 → 507, 3 → 212
dotnet run --project src/FHeroes2.App -- --map F:\game\fh2\maps\Beltway.mp2 \
    --screenshot tools-output\map-direct.png --smoke-frames 30 --freeze-animation --lang en
# [INFO] Terrain ready: 432 images of 32x32 from 'GROUND32.TIL' (4 shapes).
# [INFO] Adventure map opened: 'Beltway' (72x72, 2304x2304 pixels, 13 castles, 1684 add-ons).
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update
# update tests/baselines/adventure-map.png  sha256 21023d95…f5b0c
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild
# verify tests/baselines/adventure-map.png → identical
```

* 画面核对：`Alteris` 居中那一屏现在能同框看到城镇（城墙、门楼、城内的草地）、盗贼营地、山脊、树林、
  沉船、海岸线与被沙地包围的小屋；`Beltway` 第一屏有草地、泥土、水域与岸边礁石。地形图里内嵌的树、花、
  礁石、小船照原样出现——参考也一样（`TIL` 的图自带这些装饰，与对象层无关）。
* `GROUND32.TIL` 共 432 张 32×32，与参考 `GetMaximumTILIndex` 的输出一致；`shape` 分布说明翻转真的用上了。
* 结构层与参考逐项对齐：tile 20 字节、addon 15 字节、城堡表 72 条、捕获物表 144 条都在同一偏移读出，
  `Beltway` 的 13 座城镇与信息块里 15 个 70 字节城堡结构（13 城 + 2 个随机城）数量自洽。
* 对象层与参考对齐的部分：ICN 条目名（64 项全覆盖、无重名）、部件跳过规则、四层地面部件 + 顶部部件的
  绘制顺序、ICN 帧自带锚点、`255` 表示无图。`Alteris` 全图共用到 26 个对象 ICN 条目（`AssetProbe map`
  的 `Object ICN types` 一行），一屏只会解码其中一部分；从进程启动到首帧约 3.2 s。

### M4 验证记录（英雄层）

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 221 / UI 139 / Game 51 全绿
dotnet run --project tools/FHeroes2.AssetProbe -- map F:\game\fh2\maps\Alteris.mp2
# Heroes (9):
#    2,4    Yellow Barbarian  MINIHERO 22
#   83,51  Blue Sorceress    MINIHERO 2
#   82,52  Blue Warlock      MINIHERO 3
#   48,72  Purple Necromancer MINIHERO 40
#   96,75 / 77,90 / 68,98 / 49,99 / 105,102  Purple Necromancer  MINIHERO 40
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild
# DIFF 4495 of 307200 pixels differ（7040 ignored），diff 图存 tools-output/baseline/diff_adventure-map-en.png
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild -Update
# update tests/baselines/adventure-map.png  176,818 字节  sha256 FC51E000…C34EAC3
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild   → match（逐字节相同）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -SkipBuild           → match
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -SkipBuild       → match
pwsh tools/scripts/VerifySettingsBaseline.ps1 -SkipBuild       → match
```

* 基线那一屏的视口是 `1216,2062`（状态栏里印着），覆盖 tile `x 38…57 / y 64…79`，9 名英雄里**只有
  `(48,72)` 那名紫色亡灵在视口内**——它正站在城镇门口，放大四倍能看到骑马的紫色英雄（截图
  `tools-output/cmp-forest.png` 是旧/新渲染的上下对照）。所以 4,495 个差异像素分成两类：
  *(a)* 城镇门口那一块是英雄本体 + 旗帜 + 左移的阴影；
  *(b)* 树林、山脊、海岸处的零散小块是**层序修正**：地面层从升序 0/1/2/3 改成参考的 3/1/2/0，
  旧顺序把道路与水纹画在树、山、礁石之上。那些位置没有英雄（见上一条），所以差异只可能来自层序。
* 图层本身没有错位：`(48,72)` 的英雄正落在城镇门前的道路上（放大图里脚踩路面、旗帜在 tile 上方、
  阴影从英雄脚下向左铺开），说明 `BlitHeroSprite` 与 `BlitOnTile` 用的是同一套「tile 左上角 + 精灵锚点」
  语义。`SHADOW32` 第 18 帧实测 `63×26`、锚点 `−40,−20`（`AssetProbe sprites SHADOW32.ICN`）。
* `HeroLayerTests` 的 8 条用例是这一层的硬证据：英雄盖住本格 `OBJECT_LAYER` 的对象、
  英雄跨到上一格的行**在顶部层之后**才画（所以塔楼不吃掉英雄的头）、跨到下一格的行**在对象层之前**
  画（所以路面/树能盖住脚）、阴影在 `SHADOW_LAYER` 之后出现（用 40 号色值单独识别）。
* `HeroSpriteSetTests` 把 6 个种族 6 个颜色的条目名逐一钉住（`KNGT32.ICN` … `P-FLAG32.ICN`），
  真实数据用例确认身体宽 32 px 且 `OffsetY < 0`（头伸到上一格）、`FLAG32` 第 18 帧同样 `OffsetY < 0`
  （旗杆在 tile 上方）、`SHADOW32` 有第 18 帧且图集没有缺失条目。
* `Mp2MapHeroesTests` 钉住帧号语义：`/7` 颜色（第 6 个王国起一律紫）、`%7` 种族且第 7 帧是
  `Random`、负帧号抛异常、`Hero` 与 `ActionHero` 两种对象类型都收、非英雄 tile 一律不收。
* 其余五张基线逐字节不变：这次只动了冒险地图的绘制路径，菜单/设置/场景三张基线没受影响。

### M4 验证记录（小地图雷达）

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 221 / UI 139 / Game 86 全绿（新增 35）
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild
# DIFF 20551 of 307200 pixels differ（7040 ignored），first at 480,16
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild -Update
# update tests/baselines/adventure-map.png  168,285 字节  sha256 DCFAD12…EDF89E4
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild   → match
pwsh tools/scripts/VerifyMenu/Scenario/SettingsBaseline.ps1 -SkipBuild → 全部 match
```

* 差异被雷达矩形**完全包住**：`first at 480,16`，且 640×480 里除 `(480,16,144,144)` 以外的像素逐字节
  不变——雷达是纯叠加，没有动地形、对象与英雄的绘制路径。放大图 `tools-output/radar-zoom.png` 里能
  看到：左半屏是大片水（`0x4D`）、右下角草地（`0x62`）与提亮三档的山树、海岸线是沙滩色（`0x29`）、
  左下角一个黄色方块正是 `(2,4)` 的黄野蛮人、右侧几个紫色小点是紫色亡灵，中间那圈虚线方框就是视口。
* 方框几何对得上：视口 `1216,2062`、640×480 → tile `(38,64)` 起 21×16 格；`Alteris` 是 108×108，
  `Zoom = 144/108 = 1.333`，于是方框在面板上落在 `(50,85)`、`28×21` 像素——`MinimapTests` 里
  「缩放块对齐」「方框贴边裁剪」两条用例把这个换算钉死。
* `MinimapTests` 的 35 条里有 19 条是地形索引分段的边界（`29/30`、`91/92` … `431/432`），因为
  `MapGrounds.FromTerrainImageIndex` 是分段函数，写错一个边界只会在某张图上偶尔错一格，看基线看不出来。
* 其余五张基线不变：只改了 `AdventureMapScene` 的绘制顺序（地形 → 雷达 → 状态栏）。

### M4 验证记录（鼠标滚动）

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test   FHeroes2Sharp.slnx              → Core 167 / Assets 221 / UI 145 / Game 100 全绿（新增 14）
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -SkipBuild         → identical
pwsh tools/scripts/VerifyMenu/Scenario/SettingsBaseline.ps1 -SkipBuild → 三条全部 identical
dotnet run --project src/FHeroes2.App -- --data F:\game\fh2 --map …\Alteris.mp2 \
    --smoke-frames 20 --freeze-animation --screenshot tools-output\smoke-map-mouse.png  → 正常出图
```

* **拖动**：左键按下记起点，位移**严格大于 10 px**（`minimalRequiredDraggingMovement`，比较用 `>`）才转为拖动；每次位移 `last - current`，所以地形跟着指针走、方向与"拖动一张纸"一致；起点是按下点，因此越过阈值的那一段不会被吞掉。松键后下一次按下重新按"点击"处理，拖动期间不碰行/对象命中。
* **边缘滚动**：视口四边各 16 px（`borderWidthPx`），每 20 ms（`SCROLL_DELAY`）一步、`2 << ScrollSpeed` = 8 px（默认档 `NORMAL` = 2），四角自动合成斜向；拖动期间禁止，对应参考 `GameArea::SetScroll` 里的 `assert( !isDragScroll() )`；单帧最多补 4 步，避免调试器停顿后一次跳半张图（参考用 `DelayType` 节拍天然没这个问题）。
* **小地图**：`Minimap.TileAt` 复刻 `( pt - rect.pos ) * world / rect.size` 的整数除法，按下即在雷达上居中、按住拖动继续跟随——参考把"点击"和"按住"写在同一个 `if` 里，所以按住横扫面板就是连续定位。出界点不在 `TileAt` 里 clamp，交给 `ScrollTo` 的 clamp，与 `GameArea::SetCenter` 一致。
* **滚轮（与参考的差异）**：1.1.18 的冒险地图**不响应滚轮**（全仓库 `isMouseWheel*` 只在列表/对话框、`Radar::QueueEventProcessingForWorldView` 的缩放与编辑器里出现）。这里按方向键的粒度自定为"一格 notch = 一格 tile"，并在类注释里写明来由；滚轮想换成"平滑像素滚动"时只需改 `ScrollByWheel`。
* **没做的部分**：拖动惯性（`UIScrollInertia` + `MapSmoothScrolling` 设置）、快速滚动迟滞（`fastScrollStartThreshold` 的 2 帧/`mouseIndicatesFastScroll` 的 3 px dead zone）、滚动时切换的 8 种 `Cursor::SCROLL_*`、右键信息框。这些都需要设置项或光标资源，留给「界面外壳」一起做。
* `MapScrollControllerTests` 的 12 条把边界钉死：10 px 不算拖、11 px 算拖、方向为 `起点 - 当前`、clamp 到 160、半 tick 不滚、角落双轴、拖动期间不滚边、贴边返回 `false`、滚轮一格与 clamp。`MinimapTests` 的两条覆盖 72 格（2 px/tile）与 108 格（0.75 缩放）的整数除法，末像素落在最后一格而不是越界。
* 四条像素基线逐字节不变：smoke 截图没有鼠标事件，`_cursor` 为 `null` 时 `Update` 直接返回且不累积计时。

### M4 已知遗留

* **对象层没有动画与超大对象**：帧号直接用 MP2 里存的静态帧，参考的 `map_object_info` 动画表（火焰、旗帜、
  水面波纹、烟）与 `tileUnfit`（超过 1×1 tile 的对象整体上移绘制）都没移植，见 ADR-036。
* **英雄不动、怪物与船不画**：英雄已按三带绘制（ADR-037），但旗帜飘动与行走方向没做（一律朝右第 18 帧）；
  `MONS32`（地图怪物）与 `BOAT32`（船）的精灵同样被引擎拆到多个 tile，专属渲染器还没写，仍是主动跳过。
* **主对象类型推断没做**：`Maps::getMainObjectType` 依赖 `map_object_info` 的对象表，`Mp2TileInfo.ObjectType`
  目前直接用文件里的 `mapObjectType`；拾取与信息框需要它时再补。
* **雾、路径、面板内容都没有**：外壳已按参考落地（`ADVBORD.ICN`、448×448 @ (16,16) 视口、雷达槽、
  面板砖纹，ADR-054）。城堡图标条已有（`IconBar`，ADR-055），但它的**英雄列是空的**——MP2 的 tile 没有
  英雄身份，要等 `MP2::HeroInfo` 解析出来才能取 `MINIPORT`；按钮面板、日期与资源栏也还没有。路径预览
  与拾取同样没做（雾后来补上了，见「战争迷雾」，ADR-061）。状态栏是占位（底边框带里的诊断文字）。
* **视口居中规则是占位**：参考按玩家英雄定位；现在按「最靠地图中心的城堡」，等有玩家与英雄后替换。
* **城堡/英雄/事件信息块只存原始字节**：`Mp2InfoBlock` 保留长度与内容，字段解析（`MP2::CastleInfo`、
  `MP2::HeroInfo`、事件日期与资源）留给后续里程碑；因此「英雄落败改写」仍挂起。
* **没有存档/退出确认**：`Esc` 直接回主菜单，参考会先弹系统对话框。
* **滚动仍缺细节**：拖动 / 边缘滚动 / 小地图点击 / 滚轮已接上（见「M4 验证记录（鼠标滚动）」），
  但拖动惯性、快速滚动迟滞与滚动时的专用光标未做；键盘滚动步长固定 1 tile。
* **地图编辑器拆成了 E 系列**：~~一期不做~~（ADR-039 已被 ADR-040 收窄）。E1 只读可视化已完成（见
  `## E1` 章节），改 tile 与写 MP2 仍受 ADR-039 的约束——没有可对拍的参考代码，必须先成立「写出来能被
  自己读回 + 与原版编辑器产物对拍」。

## E1 — 地图编辑器（只读可视化）✅

> **参考对象**：fheroes2 1.1.18 有完整编辑器，`src/fheroes2/editor/`（`editor_interface.cpp` 164 KB、
> `editor_interface_panel.cpp` 72 KB、`editor_map_specs_window.cpp` 114 KB …），入口是主菜单的 EDITOR 按钮
> （`Game::MainMenu()` 里 `ICN::BTNSHNGL` 第 20 帧，仅 PoL 数据出现）→ `Editor::menuMain()` →
> `Editor::Interface`。**地图绘制复用同一个 `GameArea`**（`SetAreaPosition( borderWidthPx, borderWidthPx, ... )`），
> 保存走 `Maps::Map_Format::saveMap`（`maps/map_format_info.cpp:824`）。ADR-039 里「参考没有编辑器」的说法
> 已在该 ADR 顶部标注更正。边界与理由见 **ADR-040**，里程碑见 `PLAN.md` 的 E 系列。

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| `--scene editor` | ✅ | `SceneKind.Editor`；`--map <file.mp2>` 指定地图，缺省取 `MapCatalog` 的第一张（实测为 `Alteris`）；地图目录缺失时留在诊断屏并留一条 `WARN` |
| 主菜单入口 | ✅ | `MainMenuAction.Editor` + `MainMenuScene.EditorArea`：位置照参考的 `buttonEditor`（右下角）。`BTNSHNGL` 有 23 帧（PoL 数据）时用原版 20/21/22 美术；普通数据只有 20 帧，菜单自绘一块深底亮框招牌（三态、右击提示、`E` 热键），两条路径共用命中区 |
| `Game.Editor.MapEditorView` | ✅ | 编辑器状态：光标所在 tile（**每次现算，不缓存**）、地面类型（复用 `MapGrounds.FromTerrainImageIndex`）、可见 tile 边界、高亮方框矩形、网格开关 |
| `App.Scenes.EditorScene` | ✅ | 绘制顺序：地形/对象/英雄（复用 `AdventureMapView`）→ 网格 → 光标高亮 → 小地图 → 顶栏 → 状态栏。视口仍是整屏 640×480，两条栏是**覆盖式**，不改 `AdventureMapView.Viewport` |
| 工具条 | ✅ | 13 px 深色条 + `MAP EDITOR  brush <地面>` + 右侧 `G grid ON/OFF  1-9 brush  ^Z/^Y undo  RMB drag  Esc` |
| 状态栏 | ✅ | 左：地图名与尺寸；中：`x,y 地面 [对象类型]`（改过地图时追加 `edited`），没有 tile 时 `no tile`；右：撤销深度、视口偏移与版本（基线忽略） |
| 光标高亮 | ✅ | 外框取调色板最亮、内框取最深，1 px 双线，浅色沙地与深蓝海面上都看得见 |
| `GameHost` 接线 | ✅ | 鼠标 Motion / Button / Wheel 与键盘分发都**优先**看 `_editor`；方向键按 tile 滚、`Home` 回左上、`G` 切网格、`Esc` 关闭（没有菜单可回时退出客户端）；`--cursor` 的合成光标也送进编辑器，否则基线里没有高亮 |
| `tests/FHeroes2.Game.Tests/MapEditorViewTests` | ✅ | 11 条：hover 跟随光标、滚动后 hover 跟着地图变（缓存陷阱）、视口外与地图外都为 `null`、方框像素（含滚动偏移）、网格三条（可视边界 / 跟随滚动 / 地图边缘收口）、开关清空、地形反解（水 / 草） |
| 像素基线 `editor.png` | ✅ | `--scene editor --cursor 320,240`，居中到地图中心，128 px 视口里每 32 px 一条网格线；忽略状态栏；`VerifyEditorBaseline.ps1` 与其余五条基线共用 `PixelBaseline.ps1` |

### E1 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 167 / Assets 221 / UI 145 / Game 111 全绿（新增 11）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png sha256 33BE4BF5…
pwsh tools/scripts/VerifyEditorBaseline.ps1 -SkipBuild → identical（7040 像素忽略：状态栏）
pwsh tools/scripts/VerifyMenu/Scenario/Settings/AdventureMapBaseline.ps1 -SkipBuild → 四条全部 identical
dotnet run -- --scene editor --map …\Alteris.mp2 --cursor 320,240 --smoke-frames 20 → 出图，网格/高亮/状态栏齐全
dotnet run -- --scene editor（不带 --map）→ 日志 Editor opened: 'Alteris' (108x108, 12 castles, 1684 add-ons)
```

* 三条决定值得记下来：
  1. **复用而不是复制渲染**：参考的 `Editor::Interface` 也只是对同一个 `GameArea` 调 `SetAreaPosition`，
     所以这里直接上 `AdventureMapView`；M4 修好一层编辑器就跟着好，`editor.png` 也连带守住那一层。
  2. **hover 不缓存**：`HoverTile` 每次从当前 `Scroll` 现算。缓存的话「光标不动、地图滚动」会留下一个指错
     地方的高亮框——这一条专门写了测试（`HoverTile_FollowsTheMapAfterAScroll`）。
  3. **网格给地图边缘收口**：只画视口内的边界，但**包含最后一列的右边缘**，否则视口比地图大时（小地图合成、
     将来放大视图）网格会是没有右边和底边的开口方框。
* 顶栏盖住地图顶部 13 px、状态栏盖住底部 11 px：编辑器需要这两条信息，而整屏视口是 M4 的既定形状；参考的
  编辑器用 `ADVBORD` 外壳把地图区缩到 448×448 再在底部放 `EditorPanel`，E2 移植外壳时这两条栏会挪进去。

### E1 已知遗留

* **网格颜色单一**：用调色板最深色画线，在深蓝海面、深色岩石上对比偏低。需要「按底色取反」或双色棋盘网格。
* ~~**没有编辑动作**~~：E2 补齐了地形笔刷与撤销/重做（见 `## E2`）；对象放置与保存仍未做。
* **没有编辑器主菜单**：参考点 EDITOR 后先进新建/载入/从零/随机四选一的 `Editor::menuMain()`；这里直接打开
  目录里第一张地图，等「新建」能力做出来再补这一层。
* **没有缩放**：参考的游戏内地图也不缩放，但编辑器通常需要整图概览；目前由小地图承担定位。
* **右键只用来拖动**：E2 起右键点击是吸管、按住拖动是平移；参考里右键还负责对象菜单（弹出属性与删除）。
* **工具条与状态栏文本是英文常量**：未接入 `.mo`，因此 `editor.png` 只有一份基线，不按语言分叉（ADR-040）。
  按钮标题与提示走 `Text`，zh_CN 显示「地图编辑器」。
* **主菜单按钮在无 PoL 数据时是自绘的**：`BTNSHNGL` 普通数据只有 20 帧，自绘招牌的配色与招牌美术不完全一致；
  换到有 20/21/22 帧的数据会自动改用原版美术。
* ~~新建地图未做~~：E3b 已做，`--editor-new <size>` 打开空白地图（见下）。

## E2 — 地图编辑器（改地形 + 放对象 + 撤销/重做）✅ 已完成

> **参考对象**：`Editor::Interface` 的输入分发（`editor_interface.cpp` 把左键送进当前 instrument、右键拖地图）
> 与地形工具的重绘逻辑；撤销不是参考里的概念（它的编辑直接落地），所以撤销栈是我们自己的设计，理由见
> **ADR-041**。里程碑见 `PLAN.md` 的 E2。

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| 地形笔刷 | ✅ | `MapEditSession.SetBrush/SetBrushByNumber`：写入该地面的**纯图**（`MapGrounds.PlainImageOffset` 之后的八张之一，见 ADR-046），shape 与 beach 位随后交给接缝重算；对象字段原样保留 |
| 地形接缝 | ✅ | `TerrainTransitions.TryGetBlend`：参考 `setTerrainBoundaries` 的整套移植（缺一角 / 开一边 / 开四分之一 / 只剩斜角），按八邻算出图像偏移与翻转；越界邻格钳制在地图边界，所以地图边缘不算过渡；beach 永不接缝 |
| 纯图与变体 | ✅ | `MapGrounds.PlainImageOffset`（草/雪/沼泽/熔岩/沙漠/荒地为 38、水与泥土 16、海滩 0）与 `VariantOffset`（坐标哈希代替 `Rand::Get`，同一格永远同一张图，截图才可复现） |
| `Mp2Map.CreateBlank(size, Func<int,int,ushort>)` | ✅ | 按坐标取图的重载：新建地图把八张纯图变体铺满，而不是整图都是同一张"过渡图" |
| `tests/.../TerrainTransitionsTests` | ✅ | 8 条：孤岛无图、水边草的偏移与翻转、水对草、beach 不接缝、边界钳制、dirt 只认水与海滩、八变体与稳定性、刷水会连带改四邻且一笔撤销 |
| 笔划（stroke） | ✅ | 按下开笔、抬起结笔；同一格在这一笔里被刷多次只留「首次 before / 末次 after」，所以一次拖拽 = 一次 `Ctrl+Z` |
| 撤销/重做 | ✅ | `List<MapStroke>` 双栈，200 笔上限（最旧丢弃）；新笔清空 redo；撤销会先把未结的笔关上再回退 |
| `Mp2Map.SetTile / SetTileAt` | ✅ | 记录的唯一原地写：返回旧值，越界抛 `ArgumentOutOfRangeException`，`Tiles` 仍是对外只读视图 |
| 输入重排 | ✅ | 左键 = 刷、右键按住 = 平移（`MapScrollController.SetPanButton`）、右键单击不拖 = 吸管、`1..9` 选地面、`Ctrl+Z` / `Ctrl+Y` 撤销重做；方向键 / `Home` / `G` / `Esc` 不变 |
| 小地图重建 | ✅ | 编辑器里唯一的缓存：结笔、撤销、重做后 `Minimap.Build` 重烤；地图视图本来就是逐帧读 tile，改完立刻可见 |
| `--editor-paint <x,y,w,h,ground>` | ✅ | 诊断开关：截图按不动鼠标，用它做一次脚本化矩形笔刷，`editor.png` 靠它守住「改 tile → 画面变」这条链路 |
| `tests/.../MapEditSessionTests` | ✅ | 37 条：笔刷选择与非法地面、刷地形改 image/ground、shape 由接缝决定且 beach 位清掉、同地面刷两次只算一次、越界与 `null` 忽略、一笔多格一次撤销、同格来回刷合并、重做、空历史、新笔清 redo、无显式笔划自成一笔、撤销先结笔、吸管、对象字段不动、四种笔刷面积、数据字读写与撤销、层位保留、空 tile 拒绝、`Mp2ObjectPresets.Match` 认预设 |
| `tests/.../Mp2MapTests` | ✅ | 2 条：`SetTile` 原地写并返回旧值（含相邻格不变）、越界抛异常 |
| `MapScrollControllerTests` | ✅ | 1 条：`SetPanButton` 与左键共用状态机 |
| `ADVBORD` 外壳与 448×448 视口 | ✅ | 地图落在 16 px 边框与雷达列之间，雷达在右上角 (480,16)，雷达下方是面板槽 (480,160,144,304)；外壳就是 `ADVBORD.ICN` 那张 640×480 整图，缺资产时退化成纯色边框而布局不变（ADR-048） |
| `EditorPanel` 按钮阵列 | ✅ | `EDITBTNS` 的三个工具按钮（地形 / 对象 / 橡皮，按下是亮帧）、`EDITPANL` 裁出的九个 27×27 地面格（选中压 `TERRAINS` 第 9 帧的 30×30 黄框）、对象名字表，以及选择器下方五行状态/快捷键；面板底纹是 `STONEBAK`，点按钮/格子直接换工具与笔刷，缺资产时退回文字版（ADR-049） |
| 笔刷尺寸 | ✅ | 四个尺寸照参考的 `EditorPanel::BrushSize`：`Small` 1×1、`Medium` 2×2（指针在左上角）、`Large` 4×4（以指针为中心）、`Area` 从按下那格拖成矩形；面板上是 `EDITBTNS` 第 24～31 帧的四个按钮（24×18，`x = 14 + 30·i`，`y = 113`），当前那个是按下帧，点一下就换；橡皮同样按面积清对象（ADR-050） |
| `--editor-panel-click <x,y[;x,y…]>` | ✅ | 面板的脚本版，值是新面板坐标（0～143 × 0～319）：逐点 `EditorScene.ClickPanel`，日志报命中工具/地面/对象/笔刷尺寸与点完的状态；多条串起来就能截「先换工具再选对象」的面板 |
| 对象属性（信息框 + 数据字） | ✅ | `I` 打开指针下那一格的属性框（参考右键弹出的 `Editor::showPopupWindow`）：对象名（编辑器放过的就是预设名）、`地面 icn:帧 layer`、`顶 icn:帧`、`data N` 与**这句话的意思**、`[x; y]`；`[` / `]` 逐 1 改这段 13 位数据字，一次一撤销。数据字就是 MP2 的 `quantity1/quantity2` 高位，写入时保留 `quantity1 & 0x07`（含地面层位）；空 tile 没有属性可改（ADR-051、ADR-052） |
| 数据字的语义 | ✅ | `Mp2ObjectData` 把 13 位字翻成一句话：怪物是「the word is the troop count: 250 Medusas」（种类与单复数来自 `MONS32` 帧与 `Mp2MonsterName`），资源堆/矿的类型取自精灵（`OBJNRSRC` 帧 1/3/…/13、矿的 `EXTRAOVR` 帧 0～4），神庙/金字塔是法术 id、女巫小屋是技能 id（0 表示开局掷），宝箱与墓地开局随机，其余游戏直接清掉（ADR-052） |
| 对象名表 `Mp2ObjectName` | ✅ | 照 `MP2::StringObject` 把类型翻成名（`Artifact`、`Archer's House`、`Random Monster - very strong`…），动作位先掩掉，所以 `ActionArtifact` 与 `Artifact` 同名；`OBJ_RESURRECTION_OBJECT_TYPE` 之上的宝物（`Swampy Lake`、`Barrel`…）保留第 8 位；未知字节退回枚举名而不是像参考那样断言（ADR-053） |
| 怪物名表 `Mp2MonsterName` | ✅ | 按 `Monster::monster_t` 顺序的 72 条名字（`UNKNOWN` + 66 种 + 4 个随机怪物），`Of` 给单数、`Plural(id, count)` 按 `Monster::GetPluralName` 的规则：只有 1 个用单数（`1 Veteran Pikeman` / `25 Veteran Pikemen`） |
| `--editor-info <x,y>` / `--editor-data <x,y,value>` | ✅ | 属性框与数据字的脚本版：前者开框（截图即所见），后者在开场景前把某个对象的数据字写成 0～8191 |
| 小地图导航 | ✅ | `EditorScene` 的 `_radarGrabbed`：左键在 144×144 小地图上按下就 `CenterOn` 到指针下的格子，按住拖动一直跟随，松手结束；**不开笔划**，所以点小地图不会往撤销栈塞一步（ADR-047，照抄 `Radar::QueueEventProcessing` 的 `MouseClickLeft \|\| isMouseLeftButtonPressedInArea`） |
| `--editor-radar-click <x,y[,x2,y2]>` | ✅ | 雷达手势的脚本版，值是面板坐标：先按下，给了第二点就拖过去，再松手；`EditorScene.ClickRadar` 返回视图是否真的移动 |
| 像素基线 `editor.png` | ✅ | 一次脚本笔刷（`46,52,6,4,Snow`，Alteris 中心左侧一块）把雪落在海岸线上：地形层被改写、雪块四周是真正的雪/草/水过渡、对象层（船）仍压在上面 |
| 对象工具 | ✅ | `EditorTool`：`1..9` 选地面并回到地形工具、`O` 循环对象并选中对象工具、`X` 擦除、`T` 回地形；`MapEditSession.Apply` 是左键唯一入口（地形 / 放置 / 擦除）。工具与对象名印在状态栏 |
| 对象目录 `Mp2ObjectPresets` | ✅ | 9 个预设：树、丛林树、树顶、山、带山顶的山、墓地、宝箱、矿、资源。**每个字节都取自 `Alteris.mp2` 的真实 tile**（icn 类型、帧号、layer/quantity1），不发明帧号——格式里没有对象表，`icn+帧号` 就是「一个对象」的全部定义 |
| `Mp2Map` 的 add-on 编辑 | ✅ | `AppendAddon`（文件里 add-on 数为 0 时自动补 0 号占位）、`SetAddonAt`、`TakeObjectUid`（UID 计数器随之推进，写进文件尾部）；`Addons` 对外仍是只读视图 |
| 放置 | ✅ | 保留落点 tile 的地形图与地形标志，只换对象字段；新取 UID（ground/top 各一）；预设的 add-on 按倒序接成链；**同一格重复放同一对象不算改动**（拖拽不会每帧消耗 UID） |
| 擦除 | ✅ | 写回真实空 tile 的字节：`ObjectName1/2 = 0`、两处帧号 `255`、`Quantity1/2 = 0`、UID `0`、`NextAddonIndex = 0`；地形图与地形标志不动 |
| 对象也进撤销栈 | ✅ | 放置/擦除与刷地形共用 `MapStroke`，所以 `Ctrl+Z` 一样能退；撤销只回滚 tile，新追加的 add-on 变成无人引用的孤儿（格式没有空闲表，见 ADR-045） |
| 保存切回整份写出 | ✅ | `_addonsChanged` 为真时 `SaveAs` 走 `WriteAll`：add-on 段变长，tile 补丁表达不了；其余情况仍是 ADR-043 的补丁 |
| `--editor-object` / `--editor-place` | ✅ | 脚本化对象工具（名字或 1 起的序号 + `x,y;x,y`），截图与存盘回环靠它 |
| `tests/.../Mp2ObjectPresetsTests` | ✅ | 7 条：预设都有可画的部分、名字/序号查找与越界、空名字拒绝、0 号占位补齐、非 List 表拒绝追加、`SetAddonAt` 边界、链写出后读回、UID 递增 |
| `tests/.../Mp2ObjectDataTests` | ✅ | 11 条：`OBJNRSRC` 只有奇数帧是资源（0～15 全表）、矿按 `EXTRAOVR` 帧 0～4 认、主精灵优先于地面精灵、只有怪物保留字、怪物句子里有数量、资源堆/矿/宝箱/墓地的句子、无字可言的句子、空地的句子、资源名与 `All` 掩码 |
| `tests/.../Mp2ObjectNameTests` | ✅ | 10 条：枚举里每个成员都有名字（不会退成数字）、动作位掩掉后同名、`Beach`/`Trees`/`Archer's House` 等直白条目、第 8 位不被当动作位吃掉、未知类型保留数字、怪物名首尾与随机怪物、单复数只按 1 变、越界 id 退回 `Unknown Monster`；另加 4 条 `Mp2ObjectDataTests`（怪物名进句子、`MonsterOf` 取 `MONS32` 帧、神庙/金字塔/女巫小屋的句子） |
| `tests/.../MapEditSessionTests` | ✅ | 新增 8 条：放置写 tile 与 add-on、重复放置不改动、擦除保留地面、空格擦除不成笔、撤销/重做带回对象、`Apply` 按工具分派、`O`/`1..9` 切工具与环绕、带 add-on 的保存走整份 |

### E2 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 223 / UI 145 / Game 127 全绿（新增 18）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png sha256 C2B13233…
pwsh tools/scripts/Verify{Editor,Menu,Settings,Scenario,AdventureMap}Baseline.ps1 -SkipBuild → 六条全部 identical
dotnet run -- --scene editor --editor-paint "50,50,8,5,Snow" → 日志 Painted 8x5 tiles of 'Snow' at (50; 50)
```

* 三条决定：
  1. **tile 原地写 + 撤销栈**：不改 `Mp2Map` 的 record 语义就得每刷一格复制整张地图（108×108×20 B = 233 KB），
     拖拽笔刷会变成 GC 抖动；撤销栈保存 `before/after`，改动照样可回退。只有 `SetTile` 能写，`Tiles` 仍只读。
  2. **左键让给笔刷**：E1 里左键是拖动，但编辑器的主动作是画；参考同样是左键进 instrument、右键拖地图，
     所以加 `SetPanButton` 让同一个状态机由右键驱动，冒险地图那边一行没动。
  3. ~~只换地形图，不做相邻过渡~~：当时只把 `TerrainImageIndex` 设成区间首图，刷出来是硬边。E2 后来
     补了接缝（`TerrainTransitions`，见下与 **ADR-046**）。

### E2 对象放置与删除 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 242 / UI 145 / Game 140 全绿
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png sha256 A86AF0FE…
pwsh tools/scripts/Verify{Editor,Menu,AdventureMap}Baseline.ps1 → identical
dotnet run -- --scene editor --editor-new 36 --editor-object "Tree" --editor-place "15,15;17,15;19,16;16,18"
  → Placed 4 of 4 objects，截图里四棵树与小地图四个点都在
dotnet run -- --scene editor --map Alteris.mp2 --editor-object 5 --editor-place "60,60;62,60" --editor-save …
  → tile(60,60) icn 128/帧 56/type 100/next 1684/uid 4077；add-on 1684 → 1686（+30 字节）
```

* 三处观察：
  1. **对象没有目录可抄**：MP2 只存 `icn 类型 + 帧号`，连「这是树还是矿」都只是 `ObjectType` 的一个枚举值。
     所以 9 个预设的每个字节都去 `Alteris.mp2` 里取真实 tile，再用「写→读」回环钉住
     （`APlacedChainSurvivesAWriteAndRead`），而不是凭 ICN 长度猜帧号。
  2. **add-on 只增不回收**：删掉一个多格对象后它的 add-on 留在表里，代价是每次替换多 15 字节；换来的是
     不必重编号后面所有链，撤销也因此天然安全（撤销只回滚 tile 的 `NextAddonIndex`，孤儿无人引用）。
  3. **保存方式必须跟着数据长大**：add-on 段一变长，tile 补丁就表达不了——`SaveAs` 用 `_addonsChanged`
     切到整份写出。真实地图实测 268538 → 268568 字节，正好是两条 add-on（2×15）。
* 这一次 `editor.png` 的差异只在顶部工具栏与底部状态栏（工具名从工具栏挪到状态栏，避免和右侧提示重叠），
  地图内容逐字节不变，差异图 `diff_editor.png` 已确认。

### E2 地形接缝 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / UI 145 / Game 150 全绿（新增 10）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png sha256 46B35BD4…，108,862 字节
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap}Baseline.ps1 → identical
dotnet run -- --scene editor --map Alteris.mp2 --editor-paint "46,52,6,4,Snow"
  → diff_editor.png 的差异全部落在雪块与小地图对应点，海岸线一处未动
dotnet run -- --scene editor --map Alteris.mp2 --editor-paint "50,50,4,3,Water" --editor-save …
  → 重开截图与保存前逐像素相同（差异 517 px 全在状态栏 y=469..479）
dotnet run -- --scene editor --editor-new 36 --editor-paint "2,2,3,3,Grass"
  → 零改动：新建地图铺的已经是纯草，笔刷没有可改的
```

* 四处观察：
  1. **地面不是字段而是图号**：MP2 的 tile 不存 `Ground`，`TerrainImageIndex` 落在哪个区间就是哪个地面。
     所以「接缝」在这里不是加一层遮罩，而是把图号换成同一区间的另一张：草 30..83 里 30..67 是过渡、
     68..75 是纯图；水 0..29 里 0..15 过渡、16..23 纯图；海滩 415..431 全是纯图。
  2. **改一格要改九格**：邻居的图号各自描述了"我与谁接壤"，刷完必须把它们一起重算，否则新地面的一侧
     仍是旧边缘。九格同属一笔，所以 `Ctrl+Z` 一次退干净。
  3. **地图边界钳制**：参考把越界邻格钳到边界上，于是地图最外圈不算"与别的地面接壤"。不这么做，整张图
     的四条边会凭空多出一圈过渡。
  4. **变体必须确定**：参考用 `Rand::Get(3)` 选同组里的三张之一。编辑器要可复现——同一笔刷出同样的字节，
     截图基线不能随机——所以换成坐标哈希（`MapGrounds.VariantOffset`）。差异图证明同一次编辑每次都一样。

### E2 ADVBORD 外壳 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / UI 145 / Game 150 全绿
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png 重建为 116,108 字节 / 12CEAB70…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 match
dotnet run -- --scene editor --editor-new 72 → 72x72 新图落在 448×448 视口内，面板报 scroll 928,928
dotnet run -- --scene editor --map Alteris.mp2 --editor-paint 46,52,6,4,Snow --editor-save rt.mp2
  → 重开对比：165 px 差异，全在面板（saved → clean、undo 1 → 0），地图逐像素相同
```

* 四处观察：
  1. **`ADVBORD.ICN` 只有一张 640×480**：参考把它裁成 16 px 花纹再重复，是为了适配比 640×480 更大的显示；
     逻辑帧正好是它本身，一次整贴就对——边框、雷达槽 (480,16)、面板槽 (480,160) 的坐标天然吻合。
  2. **面板槽要先清成黑卡**：槽里是参考给按钮留的凹槽边框，文字直接压上去像噪声；清成黑底后 6 行状态 +
     6 行快捷键可读，槽边框仍露在外面。
  3. **坐标分两层**：地图视图与 `MapEditorView` 只认视口像素，框架/雷达/面板认屏幕像素，`ViewportCursor`
     是唯一换算点。指针不在视口内就传 `null`，于是"视口外不画、不吸管、不滚边"三件事自动成立。
  4. **基线必换**：整个画面换了骨架，`editor.png` 从 108,862 字节变成 116,108 字节，雪块与接缝不变。

### E2 对象属性语义 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 254 / Game 159 / UI 145 全绿（新增 11 条 Assets 测试）
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical（面板与地图没动）
dotnet run -- --scene editor --map Alteris.mp2 --editor-object Resources --editor-place "40,40" \
             --editor-data "40,40,300" --editor-info "40,40"
  → Editor object: Resources (ActionRandomResource) ground ObjnRsrc:17 top - addons 0.
  → Editor info: Resources at (40; 40), data 300.
  → 框里第五行：a random pile: resource and amount are rolled when the map starts
dotnet run -- --scene editor --map Alteris.mp2 --editor-object Mine --editor-place "60,60" --editor-info "60,60"
  → 框里：the word means nothing to this object, the game clears it
dotnet run -- --scene editor --map Alteris.mp2 --editor-info "76,6"
  → Editor info: ActionMonster at (76; 6), data 250.
  → 框里：the word is the troop count: 250 monsters
```

* 三处观察：
  1. **MP2 里只有怪物保留数据字**：`Maps::setInitialObjectInfo`（`maps_tiles_helper.cpp`）对每个动作对象
     重写 `metadata[0]`，唯一取原值的是 `OBJ_MONSTER`（`setMonsterOnTile(tile, mons, tile.metadata()[0])`）；
     资源堆的类型来自 `Resource::FromIndexSprite`（`OBJNRSRC` 帧 1/3/5/7/9/11/13 = 木/水银/矿/硫/水晶/宝石/金），
     矿的类型来自 `EXTRAOVR` 帧 0～4，宝箱与墓地的内容、资源堆的数量都是开局 `Rand::Get` 出来的，默认分支
     用 `resetMetadata()` 把字清掉。Alteris 的实际统计也印证：只有 `OBJ_MONSTER`（152）的格子里有非零字
     （250、30、40、4000…）。
  2. **「500 黄金」不在 MP2 里**：参考自己的地图格式把数量写在 `metadata[1]`（扩展元数据块，tile 里的 13 位
     只是那个块的 UID），MP2 没有这一层。所以属性框只能写「资源的种类来自精灵、数量开局才定」——把
     `data N` 硬翻成「500 黄金」会是编出来的数字。参考编辑器同样不给资源堆设数量（点资源对象弹
     `Dialog::SelectCount`，那是它自己格式的扩展元数据）。
  3. **句子是「这句话的意思」，不是新的控件**：只是属性框多一行（`Mp2ObjectData.Describe`），`[` / `]`
     照旧逐 1 改字；怪物的字改了数量就跟着变，因为那一行读的是同一个字。

### E2 对象名 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 159 / UI 145 全绿（新增 10 条）
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
FHeroes2Sharp.exe --scene editor --map Alteris.mp2 --editor-info "76,6"
  → Editor info: Monster at (76; 6), data 250.
  → 框头从 ActionMonster 变成 Monster；第五行 the word is the troop count: 250 Medusas
FHeroes2Sharp.exe --scene editor --map Alteris.mp2 --editor-info "78,3"
  → Editor info: Trees at (78; 3), data 0.（框头 Trees，面板下悬停提示同步）
```

* 三处观察：
  1. **名字照参考的一张表**：`MP2::StringObject`（`mp2.cpp`）把类型写成一个大 `switch`，进来先把动作位
     掩掉，所以 `ActionArtifact` 与 `Artifact` 同名；我们照这个规则做 `Mp2ObjectName.Of`，132 条条目
     逐条对过参考的字符串。
  2. **`MONS32` 帧到怪物 id 是 +1**：参考 `Maps::setInitialObjectInfo` 里 `OBJ_MONSTER` 的分支写着
     `Monster( tile.getMainObjectPart().icnIndex + 1 ) // ICN::MONS32 start from PEASANT`。Alteris (76;6)
     的 `MONS32:61` 因此是 62 号 Medusa，句子里的种类与 `[` / `]` 改出来的数量读的是同一个字。
  3. **复数是参考的规则**：`Monster::GetPluralName` 只在 `count == 1` 时用单数，所以 1 个是
     `1 Medusa`、250 个是 `250 Medusas`；参考对灯塔、风车这类会数个数（`_n`），这里按「一个对象」取单数。
* 名字表之外没有新控件：框头与面板下的悬停提示都只换了字符串，`editor.png` 里不含这两处，基线不变。

### 截图自动退出 修正记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 159 / UI 145 全绿
FHeroes2Sharp.exe --scene editor --map Alteris.mp2 --editor-info "76,6" --screenshot tools-output\quit-check.png
  → Screenshot saved to 'tools-output\quit-check.png'.
    Screenshot was requested on the command line, closing the client.
  → 45 秒内自行退出，退出码 0（此前要人工关窗口）
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
```

* `--screenshot` 以前只负责抓图，退出靠 `--smoke-frames`；手写的截图命令一旦漏了这个开关，抓完就停在
  主循环里，只能人工关窗口（本轮前面几次截图正是这样中止的）。现在图写盘（或写盘失败）之后立刻把
  `_running` 置 false，命令行截图一律自己退出。
* 判据是**有没有配 `--screenshot`**，而不是这一次由谁触发：F12 就算抢在抓图帧之前，脚本化运行也不会卡住；
  玩家运行时没有这个开关，F12 截完继续玩。
* 五条基线脚本本来就带 `--smoke-frames`，行为不变、仍逐像素 identical；`--help` 与 README 的说明同步。

### E2 对象属性 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / Game 159 / UI 145 全绿（新增 4 条会话测试）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png 重建为 137,995 字节 / 0A3FA38A…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
dotnet run -- --scene editor --map Alteris.mp2 --editor-place "60,60" --editor-object Mine \
             --editor-data "60,60,300" --editor-info "60,60"
  → Wrote data 300 into the object at (60; 60). / Editor info: Mine at (60; 60), data 300.
  → 框里四行：ground MtnMult:81 layer 0 / data 300 / [ ] change the data / [60; 60]
dotnet run -- --scene editor --map Alteris.mp2 --editor-info "20,20"
  → Editor info: Water at (20; 20), data 0.（框里是 Water / no object / [20; 20]）
```

* 四处观察：
  1. **属性就是 tile 里那段 13 位字**：MP2 没有「属性块」，`quantity1` 的高五位与整个 `quantity2`
     拼成 `metadata`。参考把这张表当对象目录用（资源类型、数量、守卫种类都从这里取），本移植先在
     编辑器里把这段字暴露出来：`MapEditSession.ObjectData` 读、`SetObjectData` 写，写的时候把
     `quantity1 & 0x07` 原样留下——那三位不在字里，其中两位还是地面层位（ADR-051）。
  2. **空 tile 没有属性**：擦除写回的是 `Quantity1/Quantity2 = 0` 的空 tile，这时「数据」属于空 tile
     而不是对象，`SetObjectData` 直接返回 `false`，不会把一块空白变成「有数据」的一格。
  3. **名字取自预设表**：`Mp2ObjectPresets.Match` 按 `ObjectType + icn + 帧号` 认预设，不比较数据字，
     所以改完数据名字还在；认不出来的对象退化成 `Mp2ObjectType` 的名字，空 tile 印地面名。参考用的是
     整张对象目录（`getObjectInfoText` 里几百条分支），我们只有 9 个预设，这一层差异写进了已知遗留。
  4. **框是模态的**：参考在右键上弹 `displayStandardPopupWindow`，我们右键已经给了平移与吸管，于是
     挂在 `I` 上；框开着时点击或按其它键都只关框，不会顺手在背后刷一格或撤销一步——`[` / `]` 改数据
     是框里唯一的编辑动作，一次一撤销。
* 面板提示行重排为 `1-9 ground  G grid` / `O object  X erase` / `I info  ^Z/^Y Esc`，`I` 因此进了
  `editor.png` 的像素：字节数 138,002 → 137,995，地图与雪块一个像素都没动。
* 没做：`data` 的语义。资源堆该显示「500 黄金」、矿该显示日产多少、宝箱该显示金币，都要先把参考的
  对象目录（`map_object_info.cpp`）与 `MP2::StringObject` 搬过来，那是独立的一步。

### E2 笔刷尺寸 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / Game 155 / UI 145 全绿（新增 5 条会话测试）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png 重建为 138,002 字节 / 893EAA73…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
dotnet run -- --scene editor --map Alteris.mp2 --editor-panel-click "56,230"
  → Panel click at (56; 230) hit BrushSize. Tool Terrain, ground Grass, size Medium, object 'Tree'.
dotnet run -- --scene editor --map Alteris.mp2 --editor-panel-click "120,90;116,230"
  → 先命中橡皮按钮，再命中第 4 个尺寸：Tool Erase, …, size Area
```

* 四处观察：
  1. **四个尺寸的矩形照抄 `getBrushArea()`**：`Small` 是 `(0,0,1,1)`、`Medium` 是 `(0,0,2,2)`（指针在
     左上角）、`Large` 是 `(-1,-1,4,4)`（以指针为中心）、`Area` 返回空矩形——它的大小取决于拖动。
     落笔时把这些偏移加到指针格上，再与地图边界求交，所以贴着边缘用 `Large` 不会越界。
  2. **一次落笔只重算一次接缝**：以前是「写一格 + 重算它和八邻」，现在是「写整个面积 + 重算面积外扩
     一圈」，`Large` 从 16 次写 + 144 次重算降到 16 次写 + 36 次重算，结果与逐格刷完相同。
  3. **`Area` 是拖出来的矩形**：按住时以笔划起手那格为锚，当前格为另一角，取两点的包围盒；参考是拖动时
     画橡皮框、松手才填，这里边拖边填（少一个「框」，语义相同）。往回拖不会「退填」，已经落下的格子留在
     地图上——和刷子一样，撤销才是回退手段。
  4. **名字条让位给尺寸按钮**：参考在 640×480 上根本不画名字（`drawInstrumentName` 只在面板高于 160 时
     才画），那一行 `y = 113` 本来就是四个尺寸按钮的位置。ADR-049 把名字条画在那儿是为了补信息，现在
     有了真正的按钮就撤掉，地面名仍在面板下方的状态文字里。
* 边缘：`EDITBTNS` 缺帧（少于 32 帧）时尺寸按钮不画、也点不中，工具与选择器照常工作；`Area` 之外三个
  尺寸的下标就是 `BrushSize` 的枚举值，和帧号的对应是 `24 + 2·i`（按下 +1），与参考的
  `_brushSizeButtons[i]` 一致。
* 基线只差面板那一行：字节数 137,834 → 138,002，地图与雪块一个像素都没动。

### E2 EditorPanel 按钮阵列 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / Game 150 / UI 145 全绿（面板是 App 层绘制，不加测试）
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png 重建为 137,834 字节 / B9C29A3A…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
dotnet run -- --scene editor --map Alteris.mp2 --editor-panel-click "72,18;50,145"
  → Panel click at (72; 18) hit Tool. Tool Object, ground Grass, object 'Tree'.
    Panel click at (50; 145) hit Object. Tool Object, ground Grass, object 'Tree (top)'.
```

* 四处观察：
  1. **坐标与帧号照抄参考**：三个工具按钮落在参考那张 3×3 网格的同一格（地形 0,0 / 对象 1,0 / 橡皮 2,2），
     帧号 0 / 2 / 10（按下 +1），格子步进 `36×33`、按钮本身 `48×36`；地面选择器在按钮下方，
     九张 27×27 的图从 `EDITPANL` 的 `(30+29·col, 11+29·row)` 裁出，选中格压 `TERRAINS` 第 9 帧的
     30×30 黄框（相对格子偏 -2）。名字条裁自同表的 `(7,104,130,14)`，放在选择器下 9 px
     （参考 `drawInstrumentName` 的 `panel.y + 113`），文字居中——**这一行后来让给了笔刷尺寸按钮**，
     见上面 ADR-050 那条记录。
  2. **`BlitSprite` 的源偏移会双计**：`BlitSprite(dest, Point, Sprite, sourceRect)` 会把 `Point` 和
     `sourceRect` 的原点相加，而参考的 `Blit` 是「目标位置」与「源图区域」两回事；所以 `BlitPart`
     传的是 `position - source`，否则整片地形图向右下偏 `(30,11)`。
  3. **底纹用 `STONEBAK`**：参考 `makeInstrumentPanelBackground()` 就是用这张纹理调
     `StandardWindow::renderBackgroundImage()` 铺满面板，再另画阴影；这里只做纹理那一步（从面板左上
     整贴一次，和参考 `borderOffset = 0` 一致）。面板一直画到显示底边、压住 `ADVBORD` 的底边框，
     也是参考在 640×480 的行为（`bottomBorderOffset` 只在显示比 480 高时才加）。
  4. **对象选择器只能是名字表**：`EDITBTNS` 只有 35 帧，参考那套对象图标（对象菜单）不在里面，
     `.mp2` 里也没有对象表；于是 9 个预设排成 `1 Tree` … `9 Resources`，选中行用面板亮色描一圈
     （黄框是 30×30，套不进 136×14 的行）。名字条当时只在工具/橡皮分支画——九行恰好占到它的位置；
     ADR-050 之后那一行换成尺寸按钮，对象工具依旧不画（对象不讲笔刷面积）。
    地面格的命中区是 27×27 的图本身，格与格之间的 9 px 空隙点不中，和参考的 `_terrainButtonsRect` 一样。
* 边缘：`EDITBTNS` / `EDITPANL` / `TERRAINS` 任何一个缺失，`EditorPanel.TryCreate` 返回 `null`，
  面板退回 ADR-048 的文字版，`editor.png` 也随之变成文字版（这是有意的降级，不是崩溃）。
* 基线比文字版大了 21,726 字节：按钮、九张地形图和一件纹理都是新进画面的高频色，PNG 压不动。

### E2 面板对齐参考（重画）验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 181 / UI 145 全绿
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update → editor.png 重建为 132,506 字节 / 274844977C…
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 identical
dotnet run -- --scene editor --editor-panel-click "24,18;24,72;30,125;20,225;70,255"
  → Tool Terrain｜Tool Terrain（景观对象没实现，不换工具）｜Ground Water｜BrushSize｜System
```

* 四处观察：
  1. **拾取区高度是 140，不是 124**：舱位 320 = 三行按钮 108 + 拾取区 + 两行系统按钮 72，正好对上参考的
     `instrumentPanelHeight = 480 − 160 − 5·36`。写死 124 时底部会漏出一条砖纹、系统按钮也压不住底边框；
     现在两行落在 `408…444`、`444…480`，笔刷行在 `381…399`。
  2. **`EDITBTNS` 只有 35 帧，后 10 帧是拼出来的**：冒险对象 / 王国 / 怪物 / 撤销 / 重做，干净石面照参考填
     detail 按钮的 `(16,6,18,24)`，图标按 `drawImageOnButton` 贴 `(2,2)` 并压四层阴影；撤销 / 重做箭头是
     运行时画的 19×13 图，镜像后 `(23,16)` 与 `(27,16)` 都是 56 号色（`EditorButtonSetTests` 钉住）。
  3. **冒险对象按钮的「脏底」是美术本身**：`X_LOC1` 第 0 帧是一块带暗色地面的对象图，不是拼装的瑕疵；
     王国（`OBJNARTI` 13）与怪物（`MONS32` 11）都是透明底，只有阴影边缘落在石面上。
  4. **面板不再写字**：`DrawPanelNotes` 与那五行状态一起删了，只有 `EditorPanel.TryCreate` 返回 `null`
     （缺 `EDITBTNS` / `EDITPANL` / `TERRAINS`）时才退回 `DrawPanelText`。

### E2 小地图导航 验证记录

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 243 / UI 145 / Game 150 全绿（这条逻辑在 App 层，不加测试）
pwsh tools/scripts/Verify{Menu,Settings,Scenario,AdventureMap,Editor}Baseline.ps1 → 五条全部 match
dotnet run -- --scene editor --map Alteris.mp2 --editor-radar-click "20,20"
  → "Minimap click at (20; 20) put the view at (256; 256)"：tile (15;15) 居中，小地图白框移到左上
dotnet run -- --scene editor --map Alteris.mp2 --editor-radar-click "20,20,100,100"
  → "Minimap drag at (20; 20) put the view at (2176; 2176)"：tile (75;75) 居中，面板 `undo 0`（拖动没开笔划）
dotnet run -- --scene editor --map Alteris.mp2 --editor-radar-click "140,140"
  → (3008; 3008)：点右下角被 `CenterOn` 钳在地图边界（3456−448）
```

（数值是 448×448 视口下的；视口收窄成 `ADVBORD` 框架之前每条各少 96，见 ADR-048。）

* 四处观察：
  1. **参考里点击与拖动是同一个分支**：`MouseClickLeft(rect) || isMouseLeftButtonPressedInArea(rect)` 都直接
     `SetCenter`，没有"点击跳一次、拖动慢慢跟"的区别。照抄之后，`--editor-radar-click` 的两点形式就是它的拖动。
  2. **面板必须吞掉笔划**：不吞的话，在小地图上按一下就 `BeginStroke` + `Paint`，既会在背后的格子上画一笔，
     又可能留下一个空笔划。参考靠雷达的鼠标捕获挡住 game area，这里靠 `_radarGrabbed` 在 `SetLeftButton` 里提前返回。
  3. **`Paint` 要排除面板**：把面板当作"盖住地图的工具栏"处理，一笔从地图拖到小地图上就不会在面板背后落笔。
  4. **小地图不需要重烤**：视图移动不改编地图，`_minimap` 里烤好的是颜色不是位置，白框（视口框）每次 `Draw` 现算。

### E2 修复记录（从主菜单进不去编辑器）

* **现象**：在招牌上点左键，日志已有 `Main menu: '地图编辑器' opens the map editor.` 与
  `Editor opened: 'Alteris' ...`，场景也换成了 `EditorScene`，窗口却仍是主菜单；`--scene editor` 启动正常。
* **根因**：`SdlPresenter.Upload` 只上传 `TakeDirtyRegion()`，而 `EditorScene` / `AdventureMapScene`
  把地形直接写进 `ScreenBuffer.Pixels`，从不标脏；初始脏区是全屏，所以第一帧蒙对一次之后画面冻结。
* **修复**：两个场景的 `DrawFrame` 第一行 `_frame.InvalidateAll()`；`IScene.DrawFrame` 文档写明这条契约
  （见 **ADR-042**）。顺带修掉冒险地图滚动冻结的同类隐患。
* **验证**：`--left-click`（现在在 `Loop` 第 10 帧按真实帧序模拟一次左键）点招牌后截图是编辑器；
  六条像素基线 `identical`；`-warnaserror` 0 警告；663 条测试全绿。

### E2 已知遗留

* ~~没有地形过渡/海岸自动衔接~~：E2 已补（`TerrainTransitions`，见上表与 ADR-046）。
* **孤岛不会被同化**：参考刷出一块四面不接的孤岛时，会把这块地面改回邻居的地面（它找不到图就换地面）；
  我们保留这块地面并保持纯图。编辑器不该悄悄改用户刚画的东西，所以这是有意的差异。
* ~~笔刷只有 1×1~~：E2 已补四个尺寸（ADR-050）。
* ~~对象不能放、不能删~~：E2 的对象工具已做（`O` / `X`，见上表）。
* ~~数据字有编辑入口、没有语义~~：E2 已补上 MP2 真正支持的那部分语义（怪物数量、资源堆/矿的种类、
  宝箱与墓地开局随机，ADR-052）。
* **属性框写不出「500 黄金」**：MP2 的 tile 只存 13 位字，资源堆的数量与宝箱的内容在参考自己的格式里
  是 `metadata[1]`（扩展元数据块），MP2 根本没有这一层。要写出数量只有两条路：改用参考的存档格式
  （E4），或者在 MP2 之外另存一份旁路数据（会与别的编辑器不兼容）。参考那句按类型拼出来的
  「日产 2 宝石」还要搬 `map_object_info.cpp` 的**对象目录**（`MP2::StringObject` 的名字表已在 ADR-053
  搬过来，目录是它缺的前一半：`icn + 帧号 → 对象身份`）；城堡表与可占领表同理，也不随对象更新。
* **对象目录只有 9 个、且几乎都是单格对象**：城镇、锯木厂这类多格建筑要按格拼装并处理 add-on 的
  相对偏移；面板的对象工具现在只是把这 9 个预设列成名字表，参考那套按对象类型分页的图标菜单要先有图标来源。
* **面板少了参考的系统按钮行**：`EDITBTNS` 只有 35 帧（参考要 45+：undo / redo / 网格 / 视图 / 系统菜单），
  所以面板下部写的是状态与快捷键文字，不是按钮（ADR-049）。
* **笔刷默认 1×1、`Area` 边拖边填**：参考开局就是 `Medium`、`Area` 松手才填；本移植沿用「一格一刷」的旧
  行为起步，`Area` 每移一格把矩形重刷一遍（ADR-050）。
* **撤销深度不落盘**：200 笔内存栈，退出即丢。
* **没有编辑器主菜单**：进编辑器只有主菜单按钮与 `--scene editor`；新建地图已由 `--editor-new` 提供（E3b）。

## E3 — 地图编辑器（存回 MP2）✅ 保存完成

`Ctrl+S` 把当前地图写回打开的那个 `.mp2`，状态栏闪 2 秒 `saved`；`--editor-save <file>` 是脚本入口。
写回是**原地补丁**：只重写 tile 区（`428 + x*y*20` 起的 `w*h*20` 字节），文件头、add-on 表、城堡表、
可占领对象表、方尖碑数、信息块、对象编号全部按原字节保留（ADR-043）。

改动：

* `Mp2TileInfo.Write`：与 `Read` 对称的 20 字节写出。
* `Assets/Maps/Mp2MapWriter`：`Save(path, map, sourcePath)` 与可单测的 `WriteTiles(byte[], map)`；
  写文件走同目录临时文件 + `File.Move(overwrite)`，失败不留半截地图。
* `MapEditSession`：`Save` / `SaveAs` / `SavePath`；`IsDirty` 改成与**保存点**比较，
  并且**进行中的笔划也算脏**（原来只数 `_undo.Count`，按住左键拖完还没抬手时状态栏不亮 `edited`，
  拖到一半保存也会被当成"干净"）。保存时先 `EndStroke()` 收尾当前笔划。
* `EditorScene`：`Save` / `SaveAs` + 状态栏 `saved` 提示；`GameHost` 接 `Ctrl+S`；
  `--editor-save` 在编辑器打开时立即写一次。

### E3 验证记录（真实地图）

```text
Alteris.mp2 268562 字节
打开→另存            → 0 字节差异（round-trip 完全一致）
--editor-paint 50,50,6,4,Snow → 保存 → 30 字节变化，全部落在被刷 tile 的 20 字节窗内，尾部 0 变化
保存后的文件再打开再另存 → 0 字节差异
截图 editor-saved.png：刷出来的雪地读回后正确显示
```

测试：`Mp2MapWriterTests` 6 条（round-trip、只改被编辑 tile、尾部段保真、tile 数量不符、源文件截断、
不留临时文件）；`MapEditSessionTests` 4 条（保存清脏 / 再画又脏 / 撤销回保存点 / 保存失败不改变脏标记与目标）。
`-warnaserror` 0 警告；673 条测试全绿；五条像素基线全部 `match`。

### E3 已知遗留

* **另存为没有 UI**：只有 `--editor-save` 与 `MapEditSession.SaveAs`，没有对话框。
* **保存不动对象/英雄/城堡**：这些段原样保留，等对象编辑做完再考虑。

## E3b — 新建地图与全量写出 ✅

`--editor-new 36|72|108|144` 不读文件，直接造一张空白地图进编辑器；`--editor-save` 给它一个文件，
之后 `Ctrl+S` 就是普通的补丁保存（ADR-044）。

改动：

* `Mp2Map.CreateBlank(size, terrainImageIndex, name)`：全图草地纯图块、无对象、城堡与可占领表为空、
  `Addons` 只有 index 0 的占位项、无信息块、UID 0、全六色可玩、每色骑士、Normal 难度。
* `Mp2FileInfo.HeaderBytes` + `ToHeaderBytes()`：header 以原始 428 字节为基底，只覆盖建模过的字段。
* `Mp2AddonInfo.Write`：`ObjectNameN1` 除以 2 写回（读取时乘过 2）。
* `Mp2MapWriter.WriteAll` / `Serialize`：写全部段；`MapEditSession.SaveAs` 无源文件时自动走它。
* `EditorScene.CreateNew` + `Assemble`（与 `TryCreate` 共用的装配）；`GameHost.OpenEditor` /
  `OpenEditorMap`；`CommandLineOptions --editor-new`。

### E3b 验证记录

```text
--editor-new 36 --editor-save    → 27024 字节（= 428 + 1296*20 + 4 + 15 + 216 + 432 + 1 + 4 + 4）
--editor-new 144 --editor-save   → 415824 字节，公式一致
new36.mp2 读回再保存（补丁）     → 0 字节差异
--editor-new 36 --editor-paint 10,10,6,4,Snow → 保存 → 读回截图：雪地区块正确显示
--editor-new 50                  → "'--editor-new' expects a map size of 36, 72, 108 or 144 tiles, got '50'."
Alteris.mp2 全量写出（一次性探针）→ header 428 字节与原文逐字节相同，tiles/add-ons/城堡/可占领/
                                   信息块/UID 与读入相等
```

**实测改掉的三个「读回会变字节」的坑**（详见 ADR-044）：种族槽 255 被写成 0、名字字段里跟在 NUL 后的
第二个串被抹掉、颜色字节的 2 被压成 1。

测试：`Mp2MapWriterTests` 增 5 条（全量写出的读回、对象与信息块保真、读写稳定、非法尺寸、`CreateBlank`
拒绝非标尺寸）；`MapEditSessionTests` 增 1 条（新建地图全量写盘并成为保存目标）。`-warnaserror` 0 警告，
679 条测试全绿，五条像素基线 `match`。

### E3b 已知遗留

* **空白地图只有草地，没有场景设置**：难度、颜色、胜利条件、地图名都还是固定默认，没有对话框。
* **全量写出会压紧城堡/可占领表**：空槽排到后面；「表里本来有空槽」的地图才看得出来，而那种地图走补丁。
* **信息块数量只写一对**：真实地图在那里存了一串历史值（Alteris 13 对），全量写出只写 `count+1` 加终止，
  读回等价但字节更短；补丁路径原样保留。

## 函数级 1:1 起步（ADR-057）✅

**决策**：移植粒度定在函数级（同名、同顺序、同常量），全局状态用静态门面 `static X Current` 照抄；
不引入 DI 容器，第二版换实现只动 `Current` 的赋值点。数据见 ADR-057 与 `ARCHITECTURE.md` 的
「全局状态与门面」一节（参考只有 3 个全局变量，10 个单例、832 处 `::Get()`，其中约 800 处在业务层）。
备份点 `backup/pre-facade-2026-11-14`，工作在 `refactor/global-facade` 分支。

**第一枪**：`src/fheroes2/editor/editor_ui_helper.cpp`（130 行）的前两块。

```text
dotnet build FHeroes2Sharp.slnx -warnaserror → 0 警告 / 0 错误
dotnet test  FHeroes2Sharp.slnx              → Core 168 / Assets 264 / Game 187 / UI 145 全绿
pwsh tools/scripts/VerifyEditorBaseline.ps1  → editor.png identical（新代码还没接进渲染路径）
```

新增：

* `Core/Imaging/MovableSprite.cs`：参考的 `fheroes2::MovableSprite`（位置 + 显隐）。`Sprite` 是
  `sealed`，所以用组合而不是继承；默认隐藏、空图，和参考一致。
* `Game/Assets.cs`：参考的 `Assets::getImage( ICN, frame )`。`Current` 指向归档包，解码结果按
  (文件, 帧) 缓存，换 `Current` 时清空——参考的全局 ICN 缓存就是干这个的。
* `Game/Editor/EditorUiHelper.cs`：`Checkbox`（构造里内联帧号 `NONE ? 1 : Color::GetIndex + 43`、
  贴玩家色框、勾选标记落在 `+2,+2`）、`CreateColorCheckboxes`（步进 32）。公开面与参考一致：
  只有构造、`getRect`、`getColor`、`toggle`；`_checkmark` 唯一一处暴露成 `Checkmark`，理由写在类注释里。
* `EditorUiHelperTests` 6 条：帧号（None=1、六色 43…48）、色框整块像素与 `CELLWIN.ICN` 帧逐点相同、
  框外不画、`Toggle` 两个方向、步进 32 与初始勾选、无归档时 `GetImage` 返回空、换 `Current` 清缓存。

**两处没搬**（依赖还没移植）：`drawCheckboxWithText`（要 `Text::fitToOneRow`，本项目只有
`GlyphText.WrapLines`）、`renderResources`（要 `Funds`）。下一枪是这两件：先补 `Text::fitToOneRow`。

**基础设施缺口**：参考 UI 层的第一批基础件在 C# 侧并不齐（`MovableSprite` 之前没有，`fitToOneRow`
也没有）。函数级 1:1 的成本结构因此是「先把基础件补齐，之后每个文件都是纯翻译」。静态门面在
xUnit 里是进程级状态，`EditorUiHelperTests` 因此挂进 `GlobalArchiveCollection`（禁并行）。

**1:1 收敛（同日）**：第一版自作主张加了几个参考没有的 API，已删——

* `EditorUiHelper.ColorBoxFrame`：参考是在 `Checkbox` 构造里内联的一行（`cpp:40`），已内联回去；
* `EditorUiHelper.FileName` 常量：参考直接写 `ICN::CELLWIN`，现按参考写两处 `"CELLWIN.ICN"`；
* `Checkbox.IsChecked`：参考没有这个访问器，删（需要时看 `Checkmark.IsHidden`）；
* `Assets.Reset()`：参考没有，删；清缓存就是 `Assets.Current = null`；
* `Assets.GetIcn` 改私有：参考的 `loadICN`（`game_assets.cpp:655`）也是内部函数。

反向补齐的只有 `MovableSprite( width, height, x, y )`（参考 `ui_tool.cpp:129`，`_isHidden` 取
`width == 0 && height == 0`）。唯一保留的"参考没有的访问器"是 `Checkbox.Checkmark`：参考的勾选标记
由 `MovableSprite::show` 自己画到 `Display`，本项目没有 Display 层，只能把精灵交给调用方；类注释里
写明它是临时差异，Display 移植后删除。

**出处注释**：每个从参考搬来的类型与函数都写 `参考：路径:行 函数名`，行号对应 `REFERENCE.md` 记的
1.1.18 / `6c51acfdc`；与参考有出入的，同一段里以 `差异：` 起头。已补齐：
`MovableSprite`（`gui/ui_tool.h:48`、`.cpp:123/137/152/165/177`）、`Assets`（`game/game_assets.h:33`、
`game_assets.cpp:5817` 与 `:655` 的 `loadICN`）、`EditorUiHelper`/`Checkbox`
（`editor/editor_ui_helper.h:42`、`.cpp:36/40/57/66`）。规范落在 `ARCHITECTURE.md` 的「移植注释」一节，
`REFERENCE.md` 的「已移植」表同步加了三行。

### 字体排版层（`ui_text.*`）✅

`drawCheckboxWithText` 与 `renderResources` 都要 `fheroes2::Text`，所以先把排版层搬下来。查过之后发现
它的依赖比想象中轻：**字形就是 `FONT.ICN` / `SMALFONT.ICN` 的逐帧图**（`getChar` 等价于
`Assets::getImage( icn, character )`），不需要另造字体解码层，唯一的缺口是 `Assets::getImageCount`。

* `Game/Assets.cs`：新增 `GetImageCount( fileName )`（`game_assets.cpp:5834`，`ui_text.cpp:143` 的
  `getCharacterLimit` 用它量「这个字体有多少个字符」）；
* `Game/Gui/FontType.cs`：`FontSize`、`FontColor`、`FontType`（默认 `NORMAL`+`WHITE`，工厂
  `NormalWhite()` / `SmallWhite()` …）、`TextLineInfo`；
* `Game/Gui/FontCharHandler.cs`：`FontCharHandler`（`isAvailable` / `getSprite` / `getWidth` /
  空格宽 = `(M 宽 - 1) / 3`）+ `getFontHeight`（13/15/15/16/16）、`getCharacterLimit`、`getChar`；
* `Game/Gui/TextAlgorithms.cs`：`ui_text.cpp` 匿名命名空间的 `getLineWidth`、`getTextLineArea`、
  `getMaxCharacterCount`、`renderSingleLine`、`getMaxWordWidth`、`getTruncationSymbolWidth`；
* `Game/Gui/TextBase.cs` + `Text.cs`：`width` / `height` / `rows` / `area` / `drawInRoi` / `draw` /
  `fitToOneRow` / `set` / `keepLineTrailingSpaces`，核心是逐行搬来的 `_getTextLineInfos`（空格挂行尾、
  连字符断行、整词下移、`_isUniformedVerticalAlignment` 的二分收敛）；
* `Game/Editor/EditorUiHelper.cs`：`DrawCheckboxWithText`（`cpp:77`），21 行代码，直接吃上面的排版层。

**位置**：放在 `FHeroes2.Game.Gui` 而不是 `FHeroes2.Rendering`。参考的 `ui_text.cpp` 直接调 `Assets`
门面，而门面在 `FHeroes2.Game`；放 `Rendering` 会形成 `Rendering → Game` 的反向引用。等门面下沉到
`FHeroes2.Assets` 项目后再迁。

**未搬（都在注释里标了）**：`TextInput`、`MultiFontText`、`fitToArea`、`ui_language` 的语言切换、
启动期生成的字体（`GRAY`/`YELLOW`/渐变/大号/按钮字体 → `NotSupportedException`）、`CELLWIN_EVIL`。
`Text.text()` 改名 `Content`（C# 不允许成员名等于类型名）。

**测试**：`TextTests` 9 条——字号表、空文本、宽度 = 逐字前进宽度之和、`fitToOneRow` 以 `...` 收尾且不超宽、
长句换行、绘制落像素、`DrawCheckboxWithText` 的框/文字/勾选标记位置、`isEvil` 与未移植字体抛异常。
写测试时发现一个参考行为：`FONT.ICN` 只有 96 帧（0x20–0x5F），所以小写字母宽度退化成 1px，测试文本
一律用大写。

### 资源层与编辑器资源面板（`resource.*` + `renderResources`）✅

`renderResources` 是 `editor_ui_helper.cpp` 的最后一个函数，唯一前置是 `Funds`。C# 侧此前完全没有资源类型，
所以这一轮把 `resource.h/.cpp` 的资源部分一起搬了：

* `Game/Resources/Funds.cs`：`Resource` 常量（7 个位值 + `ALL`）、`Cost`、`Funds`。七字段顺序照抄参考
  ——**ore 在第一位**（`Funds( _ore, _wood, _mercury, _sulfur, _crystal, _gems, _gold )`），这是全文件最容易
  写错的地方，测试里专门对拍了这个顺序；运算符、`Max`、`Get`/`GetPtr`、`Trim`/`Reset`、
  `GetValidItems`/`GetValidItemsCount`、`GetFirstValidResource`、`GetLowestQuotient`、
  `Resource::CalculateEventResourceUpdate` 全部逐条翻译。
* `Game/Editor/EditorUiHelper.cs`：`RenderResources`（`cpp:99`）——边缘 7、格 39×32、两行、
  第二行起点 `+32+字体高+2`，金子在第二行横跨两格。

**命名空间**：一开始叫 `FHeroes2.Game.Resource`，和 `Resource` 类同名，`FHeroes2.Game.Tests` 里的
`Resource.Wood` 会被解析成命名空间而不是类型（CS0234）。改名 `FHeroes2.Game.Resources` 解决。

**语言差异（全部写在注释里）**：

* `operator=( const Cost & )` → `SetFrom`（C# 不能重载赋值）；
* `GetPtr` → 静态 + `ref Funds` 参数：结构实例方法返回自身字段的引用是 CS8170 禁止的，`this` 只能显式传；
* 参考用 `nullptr` 表示非法资源，C# 的 `ref` 不能为 null，改为 `ArgumentOutOfRangeException`；
* 重载 `==` 时 C# 强制重写 `Equals`/`GetHashCode`（CS0660/CS0661）；
* `String()` → `ToString()`（`object.ToString` 已存在，并存两个名字没有意义）。

**未搬**：`Resource::String`（要 `Translation`）、`Resource::Rand`（要 `Rand::Get`）、
`GetIndexSprite`/`FromIndexSprite`/`getIconIcnIndex`（要 ICN 序号映射）、`BoxSprite`（要 `Display`）、
流序列化。

**测试**：`FundsTests` 17 条（构造顺序、零值与未知类型、六种运算、比较、`Get`/`GetPtr`、`Max`、
`Trim`/`Reset`、位标志、有效资源顺序、最低商、`Cost` 拷贝、事件资源裁剪、`ToString`），
`EditorUiHelperTests` 加 2 条（两行七个图标的布局 + 图标逐像素对拍、`resourceRoi` 长度不足）。
`editor_ui_helper.cpp` 至此全部搬完。

### 混排文本（`MultiFontText`）✅

`ui_text.*` 里剩下两块：`TextInput` 和 `MultiFontText`。先搬 `MultiFontText`——它只依赖已经搬好的
`Text`，而 `TextInput` 还要 `Cursor` 和键盘事件。

`Game/Gui/MultiFontText.cs`：`add` / `width` / `height` / `width( maxWidth )` / `height( maxWidth )` /
`rows` / `area` / 两个 `drawInRoi` / `fitToOneRow` / `text` / `_getMultiFontTextLineInfos`，逐条对照
`ui_text.cpp:1088–1331`。

**两处语言差异**：

* 参考靠 `friend class MultiFontText` 直接读 `Text::_text`、`_fontType` 并调私有 `_getTextLineInfos`。
  C# 没有友元，改成同程序集的 `internal`（`Text.Bytes`、`Text.Font`、`Text.GetTextLineInfos`、
  `Text.TruncateTo`），并把这些写进了注释。`Text.TruncateTo` 顺便让 `Text.FitToOneRow` 复用同一段截断代码。
* 参考的 `add( Text text )` 按值接收再 move 进 `std::vector<Text>`，容器里存的是**副本**；C# 类是引用语义，
  所以 `Add` 里显式 `Text.Clone()`，否则外部改原 `Text` 会串进去（`fitToOneRow` 会就地截断 `_texts`）。

**一个参考里的怪行为**（测试里也注了）：`_getTextLineInfos` 在 `firstLineOffsetX > 0`（也就是「这一段接着
上一段往下排」）且首字符放不下时，会先 `emplace_back` 一条**字符数为 0 的行记录**，再另起一行。所以
`MultiFontText::rows()` 会比实际有字形的行数多 1；`height( maxWidth )` 仍是对的（它只看最后一行的
`offsetY`）。

**测试踩到的坑**：`FONT.ICN` 的 `.` 帧是 15×13，截断符号 `...` 宽 45 像素。一开始用 `maxWidth` 比 45 还小
去测 `fitToOneRow`，那时参考本身也会产出超宽的 `...`（`getMaxCharacterCount` 的 `maxWidth > 0` 断言在
debug 下会直接挂），测试改成留出 45 像素的余量。

**测试**：`MultiFontTextTests` 6 条——空段被忽略且入容器的是副本、宽度求和而高度取最大、单行绘制与逐段
单独绘制逐像素一致、小字号按 `( 最高 - 本段 ) / 2` 竖直居中、超过 `maxWidth` 时换行（含那条空行记录）、
`fitToOneRow` 截断首段并丢掉后续段。

### 输入框与 `fitToArea`（`TextInput`、`Text.fitToArea`）✅

`Game/Gui/TextInput.cs` 逐条对照 `ui_text.cpp:702–1084`：`width`、`drawInRoi`、`fitToOneRow`（只挪可见窗口，
不真的截断文本，这样光标周围还能继续编辑）、`getCursorPosition`（单行 / 多行两个分支）、
`cursorArea`、`getCursorPositionInAdjacentLine`、`_updateCursorAreaInText`。
`Text.fitToArea` 补在 `Text.cs`（`ui_text.cpp:524`）。

**差异**：

* 构造函数省掉参考的 `std::optional<SupportedLanguage>`（`ui_language.*` 尚未移植）。
* 参考靠 `using Text::width` / `using Text::drawInRoi` 重新暴露被隐藏的重载；C# 按签名区分，不需要。
* `Rect` 在 C# 是只读记录结构体，参考里的 `_cursorArea.x += ...` 改成局部变量算完再一次性赋值。
* `Text` 去掉 `sealed`：参考的 `TextInput` 继承它。
* `Text` 新增 `internal void SetBytes(...)`（参考的 `TextInput::set` 直接给 `_text` 赋值、不动字体）。
* 两处参考会越界的地方加了界：`TextInput::fitToOneRow` 在 `maxWidth` 小于两倍截断符号宽时
  `twiceTruncatedMaxWidth` 为负，会把 `_visibleTextBeginPos` 推过文本末尾；`Text::fitToArea` 的修剪循环在文本
  只剩截断符号时会 `pop_back` 空串。两个都改成「退化成只剩省略号」而不是崩。

### 修正：字体帧号少了 `0x20` 偏移 🐞

写 `TextInput` 时量出 `FONT.ICN` 的 `'.'` 帧是 **15×13**、`'-'` 是 16×13，明显不是句点和连字符的尺寸。
打印位图后确认：**归档里的帧 0 是 `' '`（0x20），帧 46 是 `'N'`、帧 33 是 `'A'`**，即
`帧号 = ASCII - 0x20`。

参考之所以看起来「帧号就等于字符码」，是因为 `game_assets.cpp:2555` 在加载 `FONT.ICN` / `SMALFONT.ICN` 时
往数组前端插了 32 个空图，把下标对齐到 ASCII。C# 侧直接读 AGG 原始结构，没有这一步预处理，于是
`FontCharHandler.GetChar( 'A' )` 查的是帧 65 —— 那其实是 `'a'`（`0x61 - 0x20`）的位置，**整体错位 32 个字符**，
之前的测试全是自洽比较所以没暴露。

修复：

* `FontCharHandler.GlyphFrameOffset = 0x20`；`GetChar` 用 `character - GlyphFrameOffset` 取帧。
* `GetCharacterLimit` 返回 `32 + Assets.GetImageCount( icn )`，与参考「预处理后的数组长度」一致，
  这样 `IsValid` 的 `character <= _charLimit` 才等价。
* 顺带补上 `GetWidth( byte )` 漏掉的 `isLineSeparator → 0` 分支，并去掉 `Math.Max( 1, ... )`
  （参考就是原样返回 `image.x() + image.width()`），以及新增 `GetWidth( ReadOnlyStringView )` 重载
  （`getCursorPositionInAdjacentLine` 要用）。

`BitmapFont`（`FHeroes2.Assets.Fonts`，另一套给 App 用的渲染）本来就用 `IcnFirstCharacter = ' '` 做偏移，
互相印证这个结论。

1 条 `MultiFontText` 测试和 1 条新加的 `TextInput` 测试因为「截断符号宽 45 像素」的前提失效而调整
（真实的 `...` 只有 3 个 3×4 的句点）。

**测试**：`TextInputTests` 9 条（空输入框、短文本与纯 `Text` 逐像素一致、光标按前缀宽度移动、超宽截断、
窄于截断符号不崩、多行宽度与 `width(maxWidth)` 一致、多行光标取行首、上下移动留在目标行内、
点击换算最近边界）、`TextTests` 新增 3 条（`fitToArea` 高度预算、字形按 ASCII 取帧、字符上限含 32 个空帧）。

### 黄色与灰色字体面（`FontGlyphTable`）✅

`FontColor.Gray` / `FontColor.Yellow`（两种字号）现在可用。参考里这两个颜色**不是归档条目**：
`game_assets.cpp:736` 的 `CopyICNWithPalette` 把 `FONT.ICN` / `SMALFONT.ICN` 逐帧复制一份、再把像素过一遍
`PAL::GetPalette( YELLOW_FONT / GRAY_FONT )`，存进 `_icnVsSprite[ICN::YELLOW_FONT]` 等。

C# 侧不重复搬 `pal.cpp` 的表，直接用已有的 `PaletteTable.Get( PaletteId.YellowFont / GrayFont )` +
`ImageOps.ApplyPalette`，也就是 `FHeroes2.Assets.Fonts.BitmapFont.WithPalette` 已经在用的那套。

`Game/Gui/FontGlyphTable.cs`：

* 整表惰性生成并缓存，键是（`Assets.Current`, 字号, 颜色）——参考是启动时一次性生成，语义等价；
  换归档包时键变了，旧表不再命中。
* 转换时按**引用**去重（`ReferenceEqualityComparer`）：`FONT.ICN` 的若干帧在归档里指向同一张图，
  同一张图过两次表就错色了（参考靠拷贝构造出独立对象，天然没这问题）。
* 白色走 `PaletteId.Standard`，仍然直接返回归档缓存的那个 sprite（不复制），零开销。

未搬的仍抛 `NotSupportedException`：渐变面要 `applyFontVerticalGradient` / `CreateContour`，
`BUTTON_*_FONT` 要 `generateButtonAlphabet` 手工画字形 —— 全在 `ui_font.cpp`（七千多行，是独立的一大块）。

**测试**：`TextTests` 新增 2 条（黄色/灰色面逐像素等于白色帧过调色板表、先取彩色面不污染归档缓存的白色帧），
并把原来断言 `NormalYellow` 抛异常的用例改成断言渐变面仍然抛。

### 大号字体面与 `ImageOps.Resize` ✅

参考的 `WHITE_LARGE_FONT`（`game_assets.cpp:3054`）是把 `FONT.ICN` 每帧放大两倍、位置乘二，
放大用的是 `fheroes2::Resize` / `SubpixelResize`（按画面缩放设置二选一）。

`Fheroes2.Core/Imaging/ImageOps.cs` 新增两个 `Resize` 重载，对应 `engine/image.cpp:3087` 的最近邻路径：

* `Resize( PixelBuffer, PixelBuffer )`：逐目标像素取 `x * widthIn / widthOut`，尺寸相同时退化成拷贝；
* `Resize( Sprite, Sprite )`：在它之上再按同样的映射搬一遍透明度层。参考在「源有透明度层、目标没有」时
  会按 `transformTable` 反查调色板变换，C# 侧没有全量 `transformTable`，这种情况直接抛而不是静默丢透明信息。

`FontGlyphTable` 相应支持 `FontSize.Large`：文件仍是 `FONT.ICN`，`Enlarge` 造一张两倍大的 sprite 调 `Resize`；
按调色板矩阵，大号只有白色（`ui_text.cpp:61` 里没有黄色/灰色大号），其余组合抛。
`SubpixelResize` 未搬：它要把 RGB 重新映射回调色板索引（`GetPALColorId` 的 64³ 查表 + 游戏调色板），
而 C# 侧没有「当前游戏调色板」的全局入口，画面本身也一律最近邻采样，所以这里固定用 `Resize`。

**测试**：`SpriteOpsTests` +3 条（放大成块、透明度层同步放大、目标装不下透明度时报错）；
`TextTests` +1 条（大号字形逐像素等于普通字形的 2 倍、位置乘二、黄色大号仍抛）。

### 金色与银色渐变字体面（`generateGradientFont`）✅

`FontColor.GoldenGradient` / `SilverGradient` 现在可用，普通号与大号都有。参考
`game_assets.cpp:2433` 的 `generateGradientFont` 是四步：

1. 每帧 `resize( w + 6, h + 6 )`，字形贴到 `( 3, 3 )`，位置 `setPosition( x - 2, y - 2 )`——那圈 3 像素的边是给描边留的；
2. `applyFontVerticalGradient`（`ui_font.cpp:7151`）刷纵向渐变：只认可见像素（`transform == 0`），
   索引小于 21 的是字身、按行取渐变色的当前行，大等于 21 的是白色基础字形自带的阴影、直接改成透明；
   每行的颜色只由`|centerY - row|`决定，`centerY = max( 1, h / 2 - h % 2 )`；
3. 依次 `Blit( CreateContour( out, 内描边色 ), out )`、`CreateContour( out, 0 )`、`CreateContour( out, 外描边色 )`——
   顺序不能换，后画的一圈盖掉先画的一圈，最外面那圈才留得下来；
4. 四组颜色参数按参考抄：金普通 `( 108, 133, 55, 62 )`、银普通 `( 10, 31, 29, 0 )`、
   金大号 `( 108, 127, 55, 62 )`、银大号 `( 10, 26, 29, 0 )`。大号的渐变区间窄一点，
   因为参考是从 `WHITE_LARGE_FONT`（已放大两倍的表）长的。

为此 `Core/Imaging` 补了三个原语：

* `ImageOps.BlitSprite( Sprite destination, Point, Sprite source )`——`fheroes2::Blit` 的**双层目标**分支
  （`engine/image.cpp:1328`）：transparent 跳过；源像素带变换而目标像素可见时只把目标像素过一遍变换表；
  其余情况连透明层一起拷。原来只有 `PixelBuffer` 目标那一条，描边叠不上去。
* `ImageOps.CreateContour( Sprite, byte )`——`engine/image.cpp:1490`：给「不可见或阴影、且四邻里有可见像素」
  的像素画一圈边，其余全透明。
* `FontGlyphTable` 里的 `ApplyVerticalGradient` 与 `CreateGradientGlyph`（`applyFontVerticalGradient` /
  `generateGradientFont` 的正文）。

`SubpixelResize` 仍未搬：它要把 RGB 反查回调色板索引，而 C# 侧没有「当前游戏调色板」的全局入口。

**测试**：`SpriteOpsTests` +3 条（描边只画在四邻、单层图报错、双层目标 blit 连透明层一起拷）；
`TextTests` +1 条（四种渐变面的尺寸/锚点、可见像素只能是渐变区间或描边色、渐变确实有多个色阶；
小号渐变仍抛），原来断言渐变面抛异常的用例改名 `UnportedFontSizesAreRejected`，只留按钮字体与「参考没有的面」。

### 基础字形的启动期修补（`modifyBaseNormalFont` / `modifyBaseSmallFont`）✅

归档里的英文字形有一批历史问题，参考在加载 `FONT.ICN` / `SMALFONT.ICN` 之后就地改一遍，改好的表再拿去派生
黄/灰/大号/渐变面——所以修补必须发生在派生之前，这也是这轮的重点：`FontGlyphTable` 多了一层
`GetBaseTable( size )`，白色面直接就是它，其余面从它派生，大号是它对每帧放大两倍。

普通号（`ui_font.cpp:6852`）：

* `$` 锚点 `( 0, 2 )`、`&` 锚点 `( 0, 1 )`、`-` 往下挪一像素（三者都重算阴影）；
* `%` 左上两道多余的白线用 `FillTransform` 擦掉（`(5,0,5,1)`、`(6,2,2,1)`）；
* `\` 归档里没有，新造 8×14 把 `/` 水平镜像到 `( 1, 0 )`，锚点抄 `/`；
* 小写 `k` 先擦 `(4,1,5,8)`，再从 `K` 借五条横段拼回来；
* `|` 也没有，新造 3×( `[` 高 + 3 )：上段抄 `[` 的竖笔，下段抄它带钩的尾巴。**注意它不重算阴影**。

小号（`ui_font.cpp:7046`）：`$` 锚点 `( 0, -1 )`；`%` 擦两道白线——参考这里调的是**普通号**那套阴影参数
（`(-1,2)` / `false`），照抄了这个不一致；`*` 归档里没有、用三条 `DrawLine` 手绘；`\` 把 `/` 的竖笔
按行斜着抄四段；`k` 重拼（竖干抄 `l`、右下两笔抄 `X`、竖干右上那点抄 `a`）。

顺带把同一加载分支里的 `ReplaceColorIdByTransformId( FONT.ICN, 50, 2 )` 也搬了：字形里有 3 个像素误用了
调色板索引 50（正好是 UTF-8 里 `'2'` 的码），本该是阴影。

为此 `Core/Imaging` 补了两个原语：

* `ImageOps.FillTransform( Sprite, Rect, byte )`——`engine/image.cpp:2273`。名字只提变换层，其实颜色也一起清零，
  这块变成「透明 + 指定变换表」；单层图整段不动。
* `ImageOps.ReplaceColorIdByTransformId( Sprite, byte, byte )`——`engine/image.cpp:2724`。只改可见像素，
  颜色留着不管，变换一旦不是 0 就不会以原色画出。

修几个坑：`Sprite` 的构造函数**不会**把变换层置 1（只有 `Reset()` 会），新造的字形必须先 `Reset()`，
否则整张图都是可见的黑色；`Sprite::resize` 不碰 `_x/_y`，所以参考里那些 `setPosition( x, x )` 是空操作，
锚点沿用原帧。

未搬（都在同一个加载分支，但需要本项目还没有的东西）：`replacePOLAssetWithSW`（波兰语资材的星号）、
小号字体的 CRC 平移修补（要原始 ICN 数据算 CRC32）、CP1251 帧重排、两处
`if ( getResourceLanguage() == french )`（法语资材把 `$`/`&`/`*` 换成了 CP1252 符号，要按 `DrawLine` 重画）。

**测试**：`SpriteOpsTests` +2（`FillTransform` 连颜色一起清、`ReplaceColorIdByTransformId` 只碰可见像素）；
`TextTests` +1（`\` 与 `/` 逐像素镜像、`|` 的尺寸与锚点、`$`/`&` 锚点、小号 `*`/`\`/`k` 的尺寸、
派生面确实从修补后的表长出来、没点名的字形一个像素都没动）。

### 按钮字体（`generateButtonAlphabet` / `updateButtonFont`）✅

按钮字体不在归档里——`BUTTON_*_FONT` 不是 ICN 条目，参考是从零画的。这块搬完后，`FontCharHandler` 里
再也没有 `NotSupportedException`，`ui_text.cpp:61` 那张「字号 × 颜色」矩阵算是全部落地。

**基形**（`ui_font.cpp:4948` `generateGoodButtonFontBaseShape`）：64 个可打印 ASCII 字母，逐个用
`DrawLine` / `SetPixel` / `Fill` 手绘，每个字母四周留 2 像素（`offset = 2`）给阴影和描边，整体左移 1 像素。
这不是算法而是数据，共 295 条 `DrawLine`、64 条 `SetPixel`、1 条 `Fill`，所以是脚本从参考源码直接转写成
`ButtonFontBaseShape.cs`（`offset`→`Offset`、`buttonGoodReleasedColor`→`GoodReleasedColour`，一一对应，
可以并排比对），没有手抄。参考的 `resize( w + offset * 2, h + offset * 2 )` 后面固定跟一个 `reset()`，
合并成 `Create( w, h, previous )`；每个字母是一个 `{ ... }` 块加一句 `released[N] = letter;`。

**派生四面**（`ui_font.cpp:170` `updateButtonFont`）：白未按下加两层右下阴影 `(1,-1)` / `(2,-2)` 再套一圈
`addContour` 描边和一层 `(-1,1)` 阴影；白按下把笔画 56→62、阴影翻到左上 `(1,-1)` / `(-1,1)` / `(-2,2)`；
两个灰面在效果之后复制、只做 `ReplaceColorId`（56→30、62→36），所以灰面带着白面那圈描边。

`addContour`（匿名命名空间的，`ui_font.cpp:96`）和 `CreateContour` 是两码事：它**不放大图**，
只擦「离笔画一格」的透明像素，并跳过两个方向都没笔画的斜角，所以描边不会把阴影框住。它那套指针算术
等价于「input 的下标比 output 少 `contourOffset.X + width * contourOffset.Y`」，循环上界保证不越界。

**Core 补的原语**：`ImageOps.ReplaceColorId( Sprite, byte, byte )`——`engine/image.cpp:2707`，只看颜色层、
不看变换层（半透明像素的底色也一起换）。

**字号的索引约定**：位图字体加载时前置 32 个空帧，所以下标 = ASCII − 32；按钮字体本来就是整张 ASCII 表
（`resize( asciiCharSetSize )`），下标 = 字符本身。`FontCharHandler.GetChar` 按字号选偏移，
`GetCharacterLimit` 对按钮返回 128。顺带确认了参考只画大写：小写字母与 `$`、`@` 都是 1×1 透明占位。

**测试**：`SpriteOpsTests` +1（`ReplaceColorId` 连半透明像素一起换）；
`TextTests` 新增 `TheButtonFacesAreDrawnFromScratch`（`!` 的 6×14、四个面的笔画色 56/62/30/36、
小写与 `$`/`@` 的 1×1 占位、上限 128、只有白面有描边），原来的 `UnportedFontSizesAreRejected`
改名 `UnportedFontColoursAreRejected`，只剩「按钮没有黄/渐变」「参考本来没有灰色大号」两条。

### 资材语言探测与译版字体修补（`getResourceLanguage` / `processICN`）✅

字体加载分支里剩下的几条都挂在「这套资材是哪国语言版本」上，而参考没有语言标记文件，是把
`FONT.ICN` 的**原始字节**算一遍 CRC32 去比一张已知表（`ui_language.cpp:41`）——认不出就是英文。

**Core**：原来的 `PngCrc32` 私用工具提升成公开的 `Crc32`（`Io/Crc32.cs`，多项式 `0xEDB88320`、
初值/末值取反同 zlib，与 `fheroes2::calculateCRC32` 一致），`PngReader` / `PngWriter` 跟着改调用点。
`Compute` 支持分两段给数据，`Append` 暴露未取反的运行值。

**资产层**：`Assets.TryReadRaw( name )` 对应 `AGG::getDataFromAggFile( name, false )`（解压后的原始条目，
不缓存——调用方只有字体和语言探测几处）；`Assets.GetBaseArchiveImage( name, frame )` 对应
`getDataFromAggFile( name, true )` 加一次解码，只走基础归档、绕开扩展包，供波兰资材换星号用。

**探测**：`SupportedLanguage`（23 个值，顺序同参考）与 `ResourceLanguage`。后者缓存跟着
`Assets.Current` 的引用走（参考是进程级静态量），没有归档或没有字体时返回英文。

**`processICN` 的三类修补**（`game_assets.cpp:2461`，全部走 `Gui/FontAssetFixes`）：
波兰 GoG 小号字体整体下移一像素（CRC `0xE9EC7A63`）；波兰资材的 `*` 被画成 `.`，从**基础归档**重取
`42 - 32` 那一帧（只在小号不做，参考里那一步在 `assert( id == ICN::FONT )` 分支）；德语/意大利语资材用
CP1252、俄语用 CP1251，帧序和 ASCII 对不上，要插空帧 + 对调 + 截断。

**帧索引约定**：参考最后才在数组前插 32 个空帧让「字符码 = 帧号」，本项目用 `FontCharHandler.GlyphFrameOffset`
偏移代替那一步，所以重排仍在「帧号 = 字符 − 0x20」空间里做，顺序是：CRC 分支（波兰下移 / 星号替换）→
`modifyBase*Font`（修补）→ 重排 → 派生。`GetCharacterLimit` 因此下沉到 `FontGlyphTable`：它必须问**已排好**的
表，否则俄语 224 帧 / 德语 221 帧 / 意大利语 218 帧的长度会算错。

参考那几段是裸的 `insert` / `swap` / `erase`，数组短一点就越界（未定义行为），C# 侧按「表长得够不着那些下标
就当没命中」处理，真实资材不会走到。

**法语资材**（`ui_font.cpp:6897` / `7091`）：`$`、`&`、`*` 被换成 CP1252 符号，参考按 `DrawLine` 逐条重画；
另外十个可打印字符（`#`、`<`、`>`、`@`、`^`、反引号、`{`、`}`、`~`、DEL）一律指到 `46 - 32` 那张 `.` 上。
这也是数据不是算法，
由一次性脚本从参考源码转写成 `Gui/FontFrenchRepairs.cs`（`icnVsSprite[N-32]` → 局部 `letter` 块），
`FontRepairs.Apply` 多了 `SupportedLanguage` 参数，只在法语时调用。

**测试**：`Core` 新增 `Crc32Tests`（zlib 校验值 `0xCBF43926`、空输入、分段拼装、`Append` 串接、单字节定值）；
`Game` 新增 `FontAssetTests`（无归档时判英文、指纹就是原文 CRC、只有波兰 CRC 命中、星号帧号 10、
`MoveUp` 抬锚点且共享图像、CP1251 的双对调与 162→224、字号参与 CP1251 判定、德语的 226→221 与七次对调、
意大利语的 226→218 与借用字形、未命中返回原数组、法语普通号三符号重画 + 圆点、小号两符号重画 + 圆点）。

### 面板按钮区（`Interface::ButtonsPanel`）✅

图标条下面那一排按钮：`Game.AdventureMap.ButtonsPanel` 管布局与落位，`ButtonSpriteSet` 管帧的来源
（`ButtonBase::_getReleased` / `_getDisabled`），`AdventureButtonStatus` 是 `_setButtonStatus` 的数据化。

**几何**：参考的 `BorderWindow` 面积 `{0,0,144,72}`，四个 36×36 的按钮按 `area().width` 链着排第一行，
第二行 `y += area().height`；起点是 `iconsArea.y + height + borderWidthPx`。`ADVBORD` 的这条边框是
16 px，图标列 `176…304`，所以面板落在 `(480,320,144,72)`，底边 392，下面留空砖纹——与参考的
`321…393`（bay 底 472）同构。

**帧**：`ADVBTNS.ICN` 的 `2i / 2i+1`（`setICNInfo( icnbtn, 0..14, 1..15 )`），移动按钮另有三对：
`2/3` 走路径、`16/17` 访问脚下对象、`18/19` 路径空闲。本资材只有 16 帧，后两对取不到——参考的
`Assets::getImage` 越界也是返回空的 `errorImage`，所以那一格确实空着，不退回 2/3。

**禁用**：`ButtonBase::_getDisabled` = 复制 released 帧 + `ApplyPalette( DARKENING )`；`ButtonSpriteSet`
按帧号缓存副本，released 帧本身不动。五个按钮（王国 / 结束回合 / 冒险选项 / 文件 / 系统）参考从不禁用，
只有移动 / 施法 / 下一个英雄看英雄状态，`WithoutHero` 就是全部 fallthrough 的那一支。

**测试**：`ButtonsPanelTests` 9 条（面板与每格矩形、八个帧对、移动按钮三帧、`WithoutHero`、启用与禁用的
落位颜色、按 `HeroMovementIcon` 取帧、缺帧留空、精灵源为空不画）+ `ButtonSpriteSetTests` 2 条真资材
（36×36 与 16 帧且 `18` 号确实没有、`DARKENING` 只改副本）。基线 `adventure-map.png` 重建。

### 怪物图层（`getMonsterSpritesPerTile` / `MINIMON`）✅

冒险地图上那些游荡的怪物：`Game.AdventureMap.MonsterSpriteSet` 管帧的来源，`AdventureMapView` 管落位。
参考里它们既不走对象层也不走英雄层，`maps_tiles_render.cpp` 专门有一段 `getMonsterSpritesPerTile`。

**帧**：`MINI_MONSTER_IMAGE` 是 `MINIMON.ICN`，每只怪物九帧（站立 + 八个方向的攻击）。
`Monster::GetSpriteIndex()` 是 `monsterId - 1`（`UNKNOWN` 是 0，没有自己的帧），所以站立帧号 =
`( id - 1 ) * 9`，与英雄层的 `MINIHERO` 帧号 `/7`、`%7` 是同一路反解。瓦片这边拿不到怪物对象，只有数据字：
`Mp2ObjectData.MonsterOf` 已按 `objectName1 >> 2 == Mons32` 判断并从 `BottomIcnImageIndex + 1` 取 id。

**锚点**：`monsterImageOffset = (16, 30)`——先按 `( column * 32, row * 32 )` 定位瓦片左上角，再加锚点，
最后加帧自带的 `offset`。这也是 `AdventureMapView.BlitTileSprite` 抽出 `Point anchor` 参数的原因：
英雄传 `Point.Zero`，怪物传 `MonsterAnchor`。

**三带**：怪物与英雄一样跨格（农民 31×28，站在 `(16,30)` 处会伸到下一行、右边一格），所以按 32 px 行带切
三次画：`SHADOW_LAYER` 之后画「瓦片下方」的行、`OBJECT_LAYER` 之后画「瓦片内」的行、`TopLayer` 之后画
「伸到上一行」的行。行带切分沿用的是英雄那套（`HeroBand` 顺势改名 `TileBand`），两条绘制路径共用
`BlitTileSprite`。

**可见范围**：`VisibleMonsterTiles` 取视口左右各放 1 格、上下各放 1 行（对应参考的 `ScanAroundObject`），
再 clamp 到地图边界；怪物 id 为 0 的格子跳过。

**与参考的偏差**：参考在 `drawMonsterAnimation` 里把 `MINIMON` 的帧劈成 body（`transform` 截到 1、去掉影子
像素）和 shadow（`transform > 1`）两张，因为影子要压在所覆盖瓦片的对象**下面**。本移植直接整帧 blit，
帧里烘焙好的影子跟着身体走——只要同一批瓦片上没有别的对象盖住影子，像素就一样（ADR-059）。

**数据核对**：拿 `F:\game\fh2\maps` 前十张地图跑了一遍怪物 id 分布（临时探针，已删）：`Alteris.mp2`
2335 个 `ActionMonster`，前四为 48 骷髅 964 / 49 僵尸 228 / 50 变异僵尸 222 / 57 骨龙 192；十张图里最大的
id 是 65（火元素）。`MINIMON` 在本资材里到 66 还有帧、67 起就没有，所以现有地图全在覆盖范围内。

**测试**：`MonsterLayerTests` 7 条（瓦片内绘制、下方行先于 ObjectLayer、上方行后于 TopLayer、
被同格顶层对象遮挡、按瓦片怪物取帧、无精灵源不画、锚点常量）+ `MonsterSpriteSetTests` 2 条
（帧号 `1→0 / 2→9 / 7→54 / 66→585`；真资材：农民 31×28、66 有帧、67 与 `UNKNOWN` 返回 `null`）。

**基线**：`adventure-map.png` 重建（`170,810` → `183,591` 字节，sha256 `FC71E6B6…`）；与旧基线比
17,819 / 307,200 个像素不同，差异图上的色块**全部**落在怪物身上，地形、对象、英雄、界面一处未动
（`quantity1` 多为 0 的那批高 id 怪物也在 `MINIMON` 覆盖范围内）。

### 空船（`getBoatSpritesPerTile` / `BOAT32` + `BOATSHAD`）✅

水面上停着的空船。和怪物一样不属于对象层的通用路径：参考给它们单独一段
`getBoatSpritesPerTile`，外加 `getBoatShadowSpritesPerTile` 负责影子。

**编辑器帧与游戏帧**：MP2 文件里船的地面部件存的是**编辑器**用的水域图形——`Alteris.mp2` 里是
`OBJNWAT2` 第 23 帧。参考在装载时把这个部件改写成 `BOAT32` 第 18 帧（`RIGHT` 方向，
`Maps::setInitialObjectInfo`），所以游戏里那条 `OBJNWAT2` 帧根本不会被画。本移植照做：
`Mp2ObjectData.IsBoat` 判断瓦片是船、`DrawGroundLayer` 跳过它的地面部件、`DrawBoats` 改画 `BOAT32`。
**地图编辑器**（本项目自己的编辑器场景）不接船精灵源，于是保留编辑器帧——和原作编辑器的显示一致。

**帧与锚点**：空船的帧是 `BOAT32` 第 18 帧，62×56，自带 offset `(-8,-54)`；锚点
`BoatAnchor = (0, tileHeightPx - 11) = (0,21)`，所以船体相对瓦片左上角从 `y = -33` 开始——
**比瓦片高 33 像素，向上伸两格**，这也是可见格要放 2 格、而不是像怪物那样只放 1 格的原因。

**三带**：船体按 32 px 行带切成「瓦片下方 / 瓦片内 / 瓦片上方」三份，和怪物、英雄共用
`BlitTileSprite`；`AboveTile` 那一支从精灵顶一直画到瓦片顶，所以跨两格也没问题。

**影子**：`BOATSHAD` 第 18 帧，74×28，offset `(-38,-28)`——同样是 32 像素高的格子坐标系，
锚点后落在 `(-38,-7)`，也就是船体左下、伸到左边一格。参考把它和别的 tile-unfit 影子一起放在
`shadowImages`（Shadow 层之后、`OBJECT_LAYER` 之前），本移植在同一点整张 blit。

**与参考的偏差**：参考按 `divideImageBySquares` 把船体和影子拆成若干格，逐格决定画或不画；本移植
直接整张 blit（与英雄、怪物同一套取舍）。另外只画 `RIGHT` 一个方向：其余七个方向和左向的镜像帧由
`Maps::Tile::setBoat` 在英雄上下船时设置，本项目还没有 `world`。

**基线**：`Alteris.mp2` 的视野里一条船都没有（21 条船全在画面外），旧基线因此一模一样——`identical`。
为了让船的像素有东西守住，新增第二张基线 `adventure-map-boats.png`：`Undeada.mp2` 以「最靠地图中心的
城堡」居中，城堡西南角海面上正好停着一条空船（瓦片 `(37;38)`）。脚本的 `-MapFile` 现在也接受集合里的
裸文件名，CI 里多一条同样参数的运行（结果写 `diff_adventure-map-boats.png`）。

**测试**：`BoatLayerTests` 7 条（瓦片内落位、编辑器帧被替换、下方行先于 ObjectLayer、上方行后于
TopLayer、影子先于船体、无精灵源保留编辑器帧、锚点常量）+ `BoatSpriteSetTests` 2 条（真资材：
两个帧的尺寸与 offset、无数据时没有集合），全量 861 → 870。

### 战争迷雾（`Maps::drawFog` / `CLOF32` + `CLOP32`）✅

M4 的最后一块：雾。参考里雾不是一张覆盖图，而是**瓦片状态**——`Maps::Tile::_fogColors` 记每格对
每种颜色是否已探索，`Tile::isFog( colors )` 是 `( fog & colors ) == colors`（对所有人类颜色都没
探索才算雾），`drawFog()` 再按邻居的雾算出方向掩码来选边界件。三个新类型分工如下：

* `MapFog`：每格一个字节（已探索的颜色位）+ 一份方向掩码。`Reveal` 是 `Maps::ClearFog` 的圆盘
  （`getSquaredScoutingRadiusLimit` = `(d+1)^2 - d`，例外 `7→66`、`8→90`），`UpdateDirections` 是
  `updateFogDirectionsInArea` 的整图版本（参考只刷脏矩形，那是缓存优化）。
* `FogFrames`：`Resolve( direction, column, row, width )` 按参考 `drawFog` 的 if/else **原始顺序**
  选帧——整瓦 `( column + row ) % 4`，其余落 `CLOP32` 的 34 帧边界表，`Flipped` 表示
  `BlitOnTile( ..., revert )`。
* `FogSpriteSet`：`CLOF32.TIL` 走 `TilFile`、`CLOP32.ICN` 走共用的 `IcnSpriteCache`；镜像用
  `ImageOps.Flip` 并保留 offset，落位 `x = tileWidth - sprite.x() - sprite.width()`。

**地图边界的雾会伸到地图外**：参考的 `updateFogDirection` 先把邻居缓存填 1、越界项不写，所以边缘
瓦片始终认为自己外面还是雾。`IsFoggedOrOutside` 把这条写成了显式判断，`MapFogTests` 用「整张地图
只有一格被揭示」的用例钉住四角与四边的掩码。

**绘制顺序**：`AdventureMapView.Draw` = 地形 → 精灵（对象 / 怪物 / 英雄 / 船）→ **雾**。雾既然是最后
一层，被它遮住的东西就不能等它来盖，必须先跳过：

* 英雄、怪物：`IsUnderFog` → 不画（参考在 `Redraw` 里对两者都判 `isFog`）；
* 船：`IsUnderFogWithUpperTile`，船体伸到上一格，只有两格都被遮才不画——否则半条船会浮在雾上。

**雾上的城镇**：参考在雾之后对每个雾格调用 `drawByObjectIcnType( ..., OBJNTWBA )`，城堡再画
`OBJNTOWN`，所以城镇轮廓在探明前就在雾上（精灵本体被雾「抹平」，只剩轮廓）。本移植的
`DrawObjectParts` 遍历 add-on 链与瓦片自身，只画 `GroundIcnType` 匹配的部件。

**初始侦察**：只揭示**人类颜色**的英雄半径 4 的圆盘（`AllHeroes::Scout`，`FogDiscoveryType::HEROES`），
再把「聚焦城堡」按半径 5（`CASTLE`）揭示一次。参考的 `AllCastles::Scout` 会遍历所有城堡，但 MP2 的城堡表
只有 x / y / 类型，`world_loadmap.cpp` 建出来的城堡全是 `PlayerColor::NONE`，没有归属可判，因此「聚焦城堡」
是占位（与视口居中的占位规则同源）。当时这套侦察写在场景的 `MapFog.CreateForMap` 里；**ADR-131 之后改由
`World.Fog` + `World.ClearFog( colors )` 承担**，`CreateForMap` 已删除，见本文末的「场景接上世界迷雾」一节。

**实测**：`Alteris.mp2`（`HumanPlayers = Blue`，Blue 英雄在 `(83;51)` 与 `(82;52)`）初始可见 **82** 格，
包围盒 `(78;47)-(87;56)`，正是两个半径 4 的圆盘并集；截图里可见区是八边形、边界件接缝无空洞
（另用合成地图 5×5 逐格验证过 `( column + row ) % 4` 与 34 帧边界件的几何）。

**基线**：五张基线里只有两张冒险地图变了——雾把它们的大部分画面盖住：`adventure-map.png`
183,591 → 136,059 字节、`adventure-map-boats.png` 175,095 → 128,054 字节；`editor.png`（不传 `MapFog`）
与菜单 / 设置 / 场景三组**逐字节不变**。重建后立刻跑了不带 `-Update` 的校验，三张都是 `identical`。

**测试**：`MapFogTests`（圆盘几何、`isFog` 的多颜色语义、边界掩码、`UpdateDirections` 的 `Unknown`）
+ `FogFramesTests`（整瓦与 34 帧边界件的判定顺序、`Flipped`）+ `FogSpriteSetTests`（真资材：`CLOF32`
四张 32×32、`CLOP32` 34 帧与 offset）+ `FogLayerTests`（假雾瓦钉整瓦变体与「已探索格不被覆盖」；
真资材钉「雾下英雄不留痕」与「城镇窗口在雾上重画」），共 57 条，全量 870 → **927**。

**遗留**：没有脏矩形缓存（每次改雾要显式 `UpdateDirections`）；英雄移动、侦察兵与视野类法术的增量
更新都还没有（等 `M7` 的英雄移动与 `world`）；MP2 没有雾位，「关雾」只能靠不传 `MapFog`，
所以参考的「探索过但当前不可见」那一档暗雾也还没做。

## 参考文件覆盖率盘点（2026-11-15 快照）

M4 收尾时把参考源码逐文件核了一遍：`src/engine`（26 个）+ `src/fheroes2`（223 个）= **249** 个 `.cpp`，
其中**已转 25、部分 40、未转 184**。`src/tools` 的 8 个命令行工具不逐个对照（本项目用自研 `AssetProbe`
替代）。逐文件的落点、缺口说明与依赖顺序在 `docs/PLAN.md` 的「参考文件覆盖率（逐文件盘点）」；
本处保留快照，**下次重算时新开一节，不改这张表**。

| 目录 | 文件数 | 已转 | 部分 | 未转 |
| --- | --- | --- | --- | --- |
| `engine` | 26 | 7 | 7 | 12 |
| `agg` | 6 | 1 | 2 | 3 |
| `ai` | 9 | 0 | 0 | 9 |
| `army` | 4 | 0 | 0 | 4 |
| `audio` | 1 | 0 | 0 | 1 |
| `battle` | 18 | 0 | 0 | 18 |
| `campaign` | 3 | 0 | 0 | 3 |
| `castle` | 11 | 0 | 0 | 11 |
| `dialog` | 29 | 2 | 3 | 24 |
| `editor` | 18 | 1 | 4 | 13 |
| `game` | 28 | 2 | 3 | 23 |
| `gui` | 33 | 8 | 10 | 15 |
| `h2d` | 1 | 0 | 0 | 1 |
| `heroes` | 12 | 0 | 0 | 12 |
| `kingdom` | 14 | 0 | 2 | 12 |
| `maps` | 17 | 3 | 6 | 8 |
| `monster` | 3 | 0 | 1 | 2 |
| `resource` | 4 | 1 | 0 | 3 |
| `spell` | 4 | 0 | 0 | 4 |
| `system` | 3 | 0 | 2 | 1 |
| `world` | 5 | 0 | 0 | 5 |
| **合计** | **249** | **25** | **40** | **184** |

**已转 25 个**：`engine` 的 `agg_file` / `image_palette` / `image_tool` / `logging` / `pal` / `rand` /
`timing`；`agg/agg`；`dialog/dialog_box` / `dialog_frameborder`；`editor/editor_ui_helper`；
`game/game_mainmenu` / `game_delays`；`gui` 的 `cursor` / `ui_button` / `ui_dialog` / `ui_text` /
`ui_tool` / `ui_window` / `ui_scrollbar` / `interface_font`；`maps/ground` / `mp2` / `mp2_helper`；
`resource/resource`。

**部分 40 个**：`engine` 的 `image` / `screen` / `localevent` / `audio` / `translations` / `tools` /
`zzlib`；`agg/icn` / `m82`；`dialog` 的 `game_settings` / `selectscenario` / `language_selection`；
`editor` 的 `interface` / `interface_panel` / `history_manager` / `object_popup_window`；`game` 的
`assets` / `scenarioinfo` / `newgame`；`gui` 的 `ui_font` / `interface_gamearea` / `interface_radar` /
`interface_icons` / `interface_border` / `interface_buttons` / `interface_cpanel` / `interface_events` /
`ui_monster` / `statusbar`；`kingdom/color` / `race`；`maps` 的 `maps` / `maps_fileinfo` /
`maps_objects` / `maps_tiles` / `maps_tiles_helper` / `maps_tiles_render`；`monster/monster`；`system`
的 `players` / `settings`。

**未转 184 个（按目录）**：

* `engine`（12）：`audio_xmi2mid` `core` `dir` `h2d_file` `image_color_conversion` `math_tools`
  `render_processor` `serialize` `smk_decoder` `system` `thread` `tinyconfig`
* `agg`（3）：`bin_info` `mus` `xmi`
* `ai`（9）：`ai_battle` `ai_battle_spell` `ai_common` `ai_hero_action` `ai_personality` `ai_planner`
  `ai_planner_castle` `ai_planner_hero` `ai_planner_kingdom`
* `army`（4）：`army` `army_bar` `army_troop` `army_ui_helper`
* `audio`（1）：`audio_manager`
* `battle`（18）：`battle_action` `battle_animation` `battle_arena` `battle_army` `battle_board`
  `battle_bridge` `battle_catapult` `battle_cell` `battle_command` `battle_dialogs` `battle_grave`
  `battle_interface` `battle_interface_settings` `battle_main` `battle_only` `battle_pathfinding`
  `battle_tower` `battle_troop`
* `campaign`（3）：`campaign_data` `campaign_savedata` `campaign_scenariodata`
* `castle`（11）：`buildinginfo` `captain` `castle` `castle_building` `castle_building_info`
  `castle_dialog` `castle_mageguild` `castle_tavern` `castle_town` `castle_well` `mageguild`
* `dialog`（24）：`adventure` `arena` `armyinfo` `artifact` `audio` `buyboat` `chest` `file` `gameinfo`
  `giftresources` `graphics_settings` `hotkeys` `interface_settings` `levelup` `marketplace` `quickinfo`
  `random_map_generator` `recruit` `resolution` `selectcount` `selectfile` `selectitems`
  `system_options` `thievesguild`
* `editor`（13）：`castle_details` `daily_event_spec` `daily_events` `event_details` `language_window`
  `mainmenu` `map_specs` `options` `rumor` `save_map` `secondary_skill_selection` `spell_selection`
  `sphinx`
* `game`（23）：`difficulty` `fheroes2` `game_auto_playtest` `game_campaign` `game_credits` `game_exit`
  `game_highscores` `game_hotkeys` `game_init` `game_interface` `game_intro` `game_invalid_assets`
  `game_io` `game_loadgame` `game_mainmenu_ui` `game_over` `game_startgame` `game_static`
  `game_string` `game_tools` `game_video` `game` `highscores`
* `gui`（15）：`interface_base` `interface_focus` `interface_status` `player_info` `skill_bar`
  `ui_base` `ui_campaign` `ui_castle` `ui_keyboard` `ui_kingdom` `ui_mage_guild` `ui_map_interface`
  `ui_map_object` `ui_option_item` `ui_slider`
* `h2d`（1）：`h2d`
* `heroes`（12）：`direction` `heroes` `heroes_action` `heroes_base` `heroes_dialog` `heroes_indicator`
  `heroes_meeting` `heroes_move` `heroes_recruits` `heroes_spell` `route` `skill`
* `kingdom`（12）：`experience` `kingdom_overview` `kingdom` `luck` `morale` `payment` `profit`
  `puzzle` `resource_trading` `speed` `view_world` `week`
* `maps`（8）：`map_format_helper` `map_format_info` `map_object_info` `map_random_generator`
  `map_random_generator_helper` `map_random_generator_info` `position` `visit`
* `monster`（2）：`monster_anim` `monster_info`
* `resource`（3）：`artifact` `artifact_info` `artifact_ultimate`
* `spell`（4）：`spell` `spell_book` `spell_info` `spell_storage`
* `system`（1）：`bitmodes`
* `world`（5）：`world` `world_loadmap` `world_object_uid` `world_pathfinding` `world_regions`

**结论**：M4 之后缺的是 `world` + `kingdom` + `castle` + `army` + `battle` 这条主干，以及它前面的
拾取与移动（`map_object_info` → `heroes_move` + `world_pathfinding` + `visit`）。`battle` 最大（18 个
文件、约 620 KB），必须等 `army` 与 `castle` 落定才能动。

**口径备注**：`agg/xmi.cpp` 归未转（XMI→MIDI 没有对应实现，`MusicTrackCatalog` 只读目录），所以
`agg` 是 1 / 2 / 3；文件总数 249（`src/engine` 26 + `src/fheroes2` 223），三态相加 25 + 40 + 184 = 249。

## 对象目录（2026-11-15，M5 前置）

`map_object_info.cpp` 的第一批落地，代码在 `src/FHeroes2.Game/Maps/`：

* `ObjectLayerType`、`ObjectPartInfo` / `LayeredObjectPartInfo`、`ObjectInfo`、`ObjectGroup` 按参考命名，
  组号与图层值原样保留——编辑器存地图时按组号与组内索引引用对象，顺序不能改。
* `MapObjectCatalog` 承担 `objectData`、`populateObjectData` 与五个查询。参考的
  `static bool isPopulated` 换成静态构造（CLR 保证只执行一次），`try_emplace` 的「先到先得」
  语义用 `Dictionary.TryAdd`。
* 表已填三组：roads 513（512 种方向布局 + 城堡入口路）、streams 13、trees 69。roads 由
  `tools/scripts/GenerateObjectTables.ps1` 从参考逐行生成（373 个 part、263 处复制 + 末尾的循环），
  手抄不可行。

### 对象目录第二批：其余 20 组全部落地 ✅

参考的 `populateObjectData` 调 20 个 `populate*()`，第二批把余下 17 组一次补齐，**21 个组现在都不空**：

| 组 | 数量 | 来源 |
| --- | --- | --- |
| LandscapeMountains / Rocks / Water | 63 / 40 / 12 | 生成 |
| LandscapeMiscellaneous | 147 | 生成 |
| LandscapeTownBasements / Flags | 8 / 14 | 生成 |
| AdventureDwellings / Mines / PowerUps | 25 / 56 / 23 | 生成 |
| AdventureTreasures / Water / Miscellaneous | 13 / 13 / 71 | 生成 |
| AdventureArtifacts | 96 | 手写，`MapObjectCatalog.Artifacts.cs` |
| KingdomHeroes | 42 | 生成 |
| KingdomTowns | 14 | 手写，`MapObjectCatalog.Kingdom.cs` |
| Monsters | 71 | 手写，`MapObjectCatalog.Monsters.cs` |
| MapExtras | 7 | 生成 |

* 生成器扩到 `tools/scripts/GenerateMapObjectTables.ps1`：新增 `Resource::` 按值配对（C++
  `resource.h` 的 `enum` 与 C# `Mp2ResourceType`，含十六进制与 `|` 组合值）、初始化列表区间
  `for ( const auto type : { .. } )`（元素类型由列表项反推）、`std::make_pair` 对列表
  `for ( const auto & [type, offset] : { .. } )`、`size_t` 计数循环。
* 整数语义按需对齐：参考的 `uint8_t` 局部量与计数器一律生成为 `uint`（它们最终是 ICN 索引，本来就是字）；
  由有符号局部量算出的 part 索引补上 C++ 的隐式转换（`(uint)( ... )`）；`metadata[i]` 存资源/有符号值时同样补
  `(uint)`。`uint + int` 三元式按 C# 的整型提升规则回到声明类型。
* 复制语义：`objects.emplace_back( object )`（无 `std::move`）生成 `objects.Add(object.Copy())`。参考只在
  mines 里这么做——推入后弹掉最后一个 part 再换资源推入，两个对象必须是独立实例，无法共用引用。
* 手写三个文件的原因：KingdomTowns 的参考用 `addTown` lambda（生成器不做 lambda，改成私有 helper
  `AddTown`）；Monsters 走 `Monster` 枚举、Artifacts 的循环边界是 `Artifact` 枚举成员。两个枚举当时尚未移植，
  先按数值写成常量，随后在下一小节补上了枚举并删掉常量。
* 校验：逐个函数比对「参考 `objects.emplace_back` 句数 = 生成 `objects.Add(` 句数」，17 组全部相等（1:1 逐行）；
  表大小钉进 `MapObjectCatalogTests`。
* `MapObjectCatalogTests` 现有用例：21 组大小、每组非空、每个对象都有 ground part、mines 五个资源副本相互独立
  且 `metadata[0]` 为 Ore/Sulfur/Crystal/Gems/Gold、flags 七个颜色各两份（ICN 索引 `color*2` 与 `color*2+1`）、
  宝物的 id 顺序与 Magic Book 的临时帧、怪物的 `id - 1` 帧号。

### 两个 id 枚举：`MonsterType` / `ArtifactType` ✅

上一节留的依赖缺口补齐，两个枚举都由 `tools/scripts/GenerateIdEnums.ps1` 从参考逐行生成：

| C# | 参考 | 成员数 | 锚点 |
| --- | --- | --- | --- |
| `Game.Monsters.MonsterType` | `monster/monster.h` 的 `enum MonsterType : int32_t` | 73（`Unknown`…`MonsterCount`） | `Peasant=1`、`WaterElement=66`、`RandomMonster=67`、`MonsterCount=72` |
| `Game.Resources.ArtifactType` | `resource/artifact.h` 里 `class Artifact` 的匿名 `enum : int32_t` | 105（`Unknown`…`ArtifactCount`） | `ArcaneNecklace=9`、`BlackPearl=81`、`MagicBook=82`、`SpellScroll=87`、`SpadeNecromancy=103`、`ArtifactCount=104` |

* 命名：`MonsterType` 用参考自己的枚举名；参考给宝物枚举没起名字，端口叫 `ArtifactType`，因为 `Artifact`
  要留给带行为的类（对应参考的 `class Artifact`），与 `MonsterType` 对 `class Monster` 的做法对称。
* 生成规则：`SCREAMING_CASE` → PascalCase（`RANDOM_MONSTER_LEVEL_1` → `RandomMonsterLevel1`、`UNUSED_84` →
  `Unused84`），保留参考的空行与注释分组，遇到带显式值的成员或无法识别的行直接报错（防止编号被悄悄改掉）。
* **生成枚举当场抓到一个错误**：手写时期我把 `ARTIFACT_COUNT` 算成 103（PoL 段 16 个），实际是 104 —— PoL 段是
  `SPELL_SCROLL=87`…`SPADE_NECROMANCY=103` 共 17 个，宝物表因此是 96 条而不是 95 条（末条 ICN 205）。
  这正是「id 由参考生成、不靠手算」的价值。
* `MapObjectCatalog.Monsters.cs` / `.Artifacts.cs` 里的 11 个 `*Id` 常量删除，改用枚举成员。
* 用例：`IdEnumTests` 6 条（两个枚举的锚点、成员无空洞无重复、`*_COUNT` 等于最大 id 加一）、
  `MapObjectCatalogTests` 加 2 条（宝物表 id 顺序与 Magic Book 帧 207、怪物表 `id - 1` 帧与五个随机项）。
  `FHeroes2.Game.Tests` 360 个用例全通过，`dotnet build` 0 警告 0 错误。

**待办**

1. KingdomTowns 若要回到生成器，需先让生成器支持 lambda 与枚举减法的 `static_cast`。
2. `Assets.Maps.Mp2MonsterName` 仍是按 id 硬编码的 72 条名字表（它在 Assets 层，引用不到 `Game` 的
   `MonsterType`）；等需要名字表与枚举互相校验时再决定是搬枚举还是加一层映射。
3. `class Monster` / `class Artifact` 的行为（`monster.cpp`、`artifact.cpp`）仍未移植，随 `army` / 英雄装备进场。

没做：`Tile::getMainObjectType`。它读 `Tile` 的缓存字段（`_mainObjectType` 与对象层信息），而 `Tile`
类要等 `world` 那一步；现在硬搬到 `Mp2TileInfo` 上会造出一个没有宿主的函数，与 ADR-057 的落点不符。

### 地图几何与方向：`Direction` / `Directions` / `MapGeometry` ✅

`world` 与 `Maps::Tile` 的地基，参考 `src/fheroes2/maps/maps.cpp` 的几何部分 + `heroes/direction.h/.cpp`：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.Maps.Direction`（enum，位掩码） | `heroes/direction.h` 的 `namespace Direction` | `TopLeft=0x1` … `Left=0x80`、`Center=0x100`、`Unknown=0` |
| `Game.Maps.Directions`（静态类） | `direction.h` 的宏与 `direction.cpp` 的函数 | `TopRow`/`BottomRow`/`CenterRow`/三条竖列/`All`/`Around`/四角件/`AllCorners`、`Contains`、`String`、`IsDiagonal`、`Reflect`、`ShortDistanceClockWise`、`AllNeighboringDirections` |
| `Game.Maps.MapGeometry`（`readonly record struct`） | `maps.cpp` | `GetPoint`、`GetIndexFromAbsPoint`（两个重载）、`GetDirection`、`GetDirectionIndex`、`GetDirectionPoint`、`IsValidDirection`、`IsValidAbsIndex`、`IsValidAbsPoint`、`GetAroundIndexes`（实例 + 显式宽高的静态重载）、`TileCount` |

* **`FogDirection` / `FogDirections` 并入 `Direction` / `Directions`**（ADR-062）：迷雾表是它们此前的唯一
  用户，值一直与 `DIRECTION_*` 逐位相同，留着就是同一个枚举两份定义。`MapFog`、`FogFrame`、
  `AdventureMapView` 与 `MapFogTests`、`FogFramesTests` 只改类型名，雾的 57 条用例原样通过。
* **宽高改成实例而不是静态字段**：参考每个函数都读全局 `world`，端口按「尺寸即实例」把
  `( width, height )` 做成 `MapGeometry` 的值，函数内部逻辑与参考逐行一致；`world` 落地后持有它并转发。
  `getAroundIndexes( index, width, height, distance )` 的重载照参考保留为静态——`world_loadmap.cpp`
  在地图还没建好时就用它看邻居。
* 参考的边界怪癖保留并注明：`getAroundIndexes` 判的是 `index > width * height`（不是 `>=`），
  所以恰好等于瓦片总数的索引仍会算出「地图下方一行被裁剪后的环」；`getDirectionPoint` 对非方向值
  参考是 `assert( 0 )` 后返回 `(-1, 0)`，端口抛 `ArgumentOutOfRangeException`（ADR-037 的规矩）。
* 用例：`DirectionTests` 10 条、`MapGeometryTests` 12 条。后者在 4×3 地图上钉住 index ↔ point 互逆、
  九宫格的八个方向、`GetDirection` 与 `GetDirectionIndex` 互逆、四条边界上的 `IsValidDirection`，
  并对「`IsValidDirection` 允许的每一步都必须落在合法索引上且方向可还原」做全图对拍。
  `FHeroes2.Game.Tests` 360 → **442**，全量 951 → **1033**，0 警告 0 错误。

**下一步**（`world` / `Tile` 的前置已经齐了）：`Maps::Tile`（`maps_tiles.h` 330 行 + `.cpp` 1663 行，
先搬对象层三件 `MapObjectPart` 与 `getMainObjectType` 的缓存）、随后 `world.h/world.cpp` 的骨架，
再回到 `heroes_move.cpp` + `world_pathfinding.cpp` + `maps/visit.cpp`。

### `Maps::Tile` 第一片：对象层、地形位、道路位、通行掩码 ✅

`src/fheroes2/maps/maps_tiles.cpp` 的「只靠瓦片自己就能回答」的那一半（ADR-063）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.Maps.ObjectPart`（struct） | `maps_tiles.h:58` `ObjectPart` | `Uid`/`LayerType`/`IcnType`/`IcnIndex`（默认 255）、`IsPassabilityTransparent`、值相等 |
| `Game.Maps.Tile`（sealed class） | `maps_tiles.h:99` `Tile` | `Init(MP2TileInfo)`、三件容器（main/ground/top）与它们的增删改查、`SortObjectParts`、`RemoveObjects`、`ReplaceObject`、`UpdateObjectImageIndex`、`UpdateTileObjectIcnIndex`、`GetObjectIdByObjectIcnType`、`GetValidObjectIcnTypes`、`ContainsAnyObjectIcnType`、`ContainsSprite`、`DoesObjectExist`、`MainObjectType`/`SetMainObjectType`、`Metadata`、`SetTerrain`/`Ground`/`IsWater`/`TerrainFlags`、`IsSpriteRoad`/`UpdateRoadFlag`/`IsRoad`、`PassableDirections`/`IsPassableFrom`/`IsPassableTo` |

三个必须记住的参考细节（都写进了注释与用例）：

* **排序后提为 main 的是层值最小的部件**：参考按层降序排完再从尾往前找第一个非 `FLAG32`，
  所以有 object 层部件时主对象就是它；变量名 `highestPriorityPartIter` 指的是「当主对象」的优先级。
* **`std::list::sort` 是稳定的**，`List<T>.Sort` 不是，端口改用 `OrderByDescending` 才没有把同层部件重排。
* **空瓦片的 `IsPassabilityTransparent` 是 `false`**（默认 main 部件在 object 层），且该函数在参考里
  没有调用点；`RemoveObjects` 之后也不更新对象类型（参考自己留了 TODO）。两处照抄，不「顺手修」。

用例：`TileTests` 21 条，覆盖 `Init` 的 metadata 公式 / 图层低两位 / 裂纹 226 下移 / 道路位、
排序与 flag 跳过、主部件下移、移除与换图、三个查询函数、`DoesObjectExist` 只看主层与地面层、
`IsSpriteRoad` 三张索引表、默认全向可通行。`FHeroes2.Game.Tests` 442 → **475**。

未搬（随 `world`，ADR-063 第 1 条列全）：`getHero`/`setHero`、`getMainObjectType( ignoreObjectUnderHero )`、
船、迷雾位、`setInitialPassability`/`updatePassability`、`updateObjectType`、`removeObjectPartsByUID`
的尾部、`fixMP2MapTileObjectType`、`getIndexOfMainTile`、`isAnyTallObjectOnTile`、`isDetachedObject`、
`isShadow`、所有权旗标、`String()`、序列化。

**下一步**：`Maps::Tile` 剩下的部分是 `world` 的镜像，先立 `world.h/world.cpp` 的骨架
（`w`/`h`/`vec_tiles`/`vec_heroes`/`resetPathfinder` 的占位），再把通行计算与英雄占用接上去。

### `Maps::Tile` 第二片：独立通行计算 ✅

把 ADR-063 里「明确不搬」的 `getTileIndependentPassability` / `setInitialPassability` 提前搬了（ADR-064），
连同它们在 `maps_tiles.cpp` 匿名命名空间里全部依赖：阴影图号表、短对象、可脱离对象、组合对象、
暗礁图号，以及 `mp2.cpp` 的 `getActionObjectDirection`。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.Maps.TileObjectTraits`（静态类） | `maps_tiles.cpp` 匿名命名空间 + `mp2.cpp:815` | 17 张 ICN 阴影图号表、`IsValidShadowSprite`、`IsObjectPartShadow`、`IsShortObject`、`IsDetachedObjectType`、`IsCombinedObject`、`IsValidReefsSprite`、`GetActionObjectDirection` |
| `Tile.GetTileIndependentPassability` | 同名 | 主部件 → 地面层逆序 → 逐部件取交集；action 对象立即返回；`top` 层不参与 |
| `Tile.SetInitialPassability` | 同名 | 纯赋值，不碰邻居 |
| `Tile.IsStream` / `Tile.IsShadow` | 同名 | `IsStreamPart` 两段图号（`OBJNMUL2` `< 14`、`218..231`）；`IsShadow` 要求主部件与地面层**全部**是阴影 |

两个只有对拍才抓得到的坑：

* **`GetActionObjectDirection` 的 `case` 必须是 `Action*` 值**：`MP2::MapObjectType` 里 action 值 =
  同义非 action 值 + 128，对象目录存的正是 action 值。写成非 action 名能编译、也永远走 `default`。
  `default` 照参考 `assert` 后返回 `Direction.Unknown`，**不抛**——这个函数在遍历里被调用，
  抛异常等于把「未知对象」升级成崩溃。
* **地面层部件是逆序（层值从大到小）遍历的**，不是从头到尾；写正了在单部件用例上照样通过，
  只有多部件重叠时才露。

用例：`TilePassabilityTests` 12 个方法 / 15 条（道路与溪流、`FLAG32` 用 Theory）。`FHeroes2.Game.Tests`
475 → **490**，全解决方案 1081 条全绿，0 警告 0 错误。

**下一步**：`world.h/world.cpp` 骨架（`w`/`h`/`vec_tiles`/`vec_heroes`/`resetPathfinder`），
然后 `Tile.updatePassability` 与英雄占用。

### `World` 骨架：尺寸、瓦片表、取瓦片 ✅

`src/fheroes2/world/world.cpp` 的第一片（ADR-065）。`World` 在参考里同时装着二十多个容器，这里只立
`Tile` 剩下的成员真正需要的那部分：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.World`（sealed class） | `world.h:197` | `Current`（= `World::Get()`）、`Width`/`Height`、`Tiles`、`Size`、`Geometry`、`GetTile(x,y)`、`GetTile(index)`、`GenerateUninitializedMap`、`Reset`、`ResetPathfinder` |

四条必须记住的细节（都进了注释与用例）：

* **`getTile( x, y )` 不校验坐标，只校验算出来的索引**：`x == width` 会静默落到下一行第一格，负 `x`
  会借上一行。参考写的就是 `vec_tiles.at( y * width + x )`。

* **`generateUninitializedMap` 不设瓦片索引**：参考只 `resize`，索引由 loader 逐个 `setIndex`
  （`world_loadmap.cpp:279`），`generateBattleOnlyMap` 自己循环设。端口若在这里补上，loader 忘了设
  索引就永远查不出来。

* **`Geometry` 是 `MapGeometry?`**：`MapGeometry` 拒绝 0 维，而世界的初始尺寸就是 0 × 0，所以空世界
  返回 `null`；有地图时按当前尺寸现造一个值（`readonly record struct`，不必存字段）。

* **构造函数是 `internal`**：参考是 private + `World::Get()`。端口改成 `internal` 让测试与将来的
  第二张地图（编辑器/预览）能造独立实例；生产代码只走 `World.Current`（ADR-056 的门面）。

未搬（随各自系统）：英雄/城堡/王国/俘获对象/地图对象容器、日期与事件、谣言、区域图、`Defaults()`、
静态分析、`updatePassabilities`、`world.h` 里那一百多个成员函数。

用例：`WorldTests` 8 个方法 / 17 条。`FHeroes2.Game.Tests` 490 → **507**，全解决方案 1098 条全绿，
0 警告 0 错误。`world.cpp` 从「未转」转「部分」，覆盖合计 26/42/181 → **26/43/180**（总数 249 不变）。

**下一步**：把 `Tile` 剩下的世界相关成员接上——`updateObjectType`、`updatePassability`（碰邻居）、
`getHero`/`setHero`，以及 `World.updatePassabilities`；之后才是 `world_loadmap.cpp` 的对象表。

### `Maps::Tile` 第三片 + `World.UpdatePassabilities`：邻居相关的通行 ✅

`Tile` 里凡是「要看邻居」的部分，一次搬完（ADR-066）——它们是一条调用链，拆开搬只会造出临时的假实现：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Tile.UpdatePassability(World)` | `maps_tiles.cpp:657` | 高物体吃掉左上/右上对角；自身对象与下格冲突则整格禁行；action 对象只看自己 |
| `Tile.UpdateObjectType(World)` | `:1599` | 主部件 → 地面层逆序 → 顶部件 → 海崖（邻居有水） |
| `Tile.IsAnyTallObjectOnTile(World)` | `:1767` | 本格物体的 UID 是否出现在上一格的 top 层 |
| `Tile.IsDetachedObject(World)` | `:1854` | 城堡/矿/祭坛这类「不挂在下面」的对象；要看主格 |
| `Tile.GetIndexOfMainTile(Tile, World)` | `:1798` | 半径 3 格内按「action 类型 + 共享 UID」找主格 |
| `World.UpdatePassabilities()` | `world.cpp:1177` | 两趟：先补空类型 + 存独立掩码，再逐格收窄 |

三条必须记住的：

* **`World` 是参数，不是 `World.Current`**：瓦片反向持有世界会成环，用静态门面则每个测试都要动全局。
  参考里是全局 `world`，所以每个调用点都得记着传——目前只有 `World.UpdatePassabilities` 一处。
* **`Mp2ObjectTypes.BaseActionType` 被修正为忠实版**（补 `CanBeActionObject`，`mp2.cpp:38`）：旧实现
  「非 action 就加 128」会让树/山/湖也算成 action 对象，于是每棵树下面那格都被判成禁区。顺带修正了
  `Event` 分支——参考的 `MP2::OBJ_EVENT` 是 **action 值**，对应端口的 `ActionEvent`。
* **`updateObjectType` 的事件分支没搬**：参考查 `world.GetMapEvent()` 决定是否清类型，事件容器还没有，
  所以类型一律保留（= 参考「事件数据完好」时的行为）。

断言策略：重复调用检查用 `Debug.Assert`（release 行为与参考一致），数据完整性断言（`_uid != 0`）不搬
——把它变成异常等于让一张怪地图直接崩掉，而参考的容错正是「算出来难看但能进游戏」。

用例：`TileNeighbourPassabilityTests` 13 个方法 / 15 条。测试里手工设 `Tile.Index` 是必须的
（`GenerateUninitializedMap` 故意不设，ADR-065），那段循环就是 loader 的活儿。
`FHeroes2.Game.Tests` 507 → **522**，全解决方案 1113 条全绿，0 警告 0 错误。

**下一步**：`world_loadmap.cpp` 的对象表与 `Tile.setHero`/`getHero`（英雄占用）谁先都可以，但
`world_loadmap` 是让整条链吃上真数据的入口，建议先它。

### `World.LoadMap` 的可移植子集 + 对象 UID 计数器 + 对象表 action 类型修正 ✅

`world_loadmap.cpp` 有 900 多行，一次搬完等于把 `castle`/`kingdom`/英雄一起搬。所以只搬
「决定有哪些瓦片、它们能不能走」的那部分（ADR-067）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.LoadMap(Mp2Map)` | `world_loadmap.cpp:241` | 尺寸校验 → `Reset()` → 逐格 `Index`/`SetTerrain`/`Init` → add-on 链 → 排序 → 全图类型修复 → `PostLoad` |
| `World.TryLoadMap(path, rejectPriceOfLoyalty)` | 同上的失败出口 | `Mp2Map.TryRead` 失败返回 `false`，不抛 |
| `World.PostLoad(setTilePassabilities, updateUidCounterToMaximum)` | `world.cpp:1194` 的子集 | `UpdatePassabilities` → `ResetPathfinder` → 扫部件取最大 UID |
| `World.GetNewObjectUid` 等四个 | `world_object_uid.cpp` | 计数器从全局静态改成世界实例属性（前置自增） |
| `Tile.FixMP2MapTileObjectType(World)` | `maps_tiles.cpp:1321` | 沙漠骨架、绿洲顶部件、礁石、水格海岸、随机怪物占位符、PoL 类型 |
| `Tile.UpdatePriceOfLoyaltyObjectType(...)` | `maps_tiles.cpp:319` | 只对 `XLoc1/2/3`；类型为 `None` 时走 `UpdateObjectType` 重建 |

本轮真正的 bug 是**对象表的 action 位**，不是装载逻辑本身：

* 参考的主类型写的是 `MP2::OBJ_ARTIFACT` / `OBJ_MONSTER` / `OBJ_CASTLE`，而这些是**带 action 位**的值
  （`mp2.h` 里 `OBJ_ARTIFACT = OBJ_NON_ACTION_ARTIFACT + 128`）。端口的 C# 枚举把两者分名
  （`Artifact` = 41、`ActionArtifact` = 169）。
* `MapObjectCatalog.Generated.cs` 按**数值**配对，所以没问题；三个**手写**表（`Artifacts`、`Monsters`、
  `Kingdom`）照 C++ 的**名字**直译，落成了非 action 值。
* 后果不是「有点不一样」：`AddTown` 里 `mainObjectType - 128` 对 35/48 得到 65443/65456（`ushort` 环绕），
  这个值进了城堡部件的 `ObjectType`，于是真实地图上 `MapObjectCatalog.GetObjectTypeByIcn` 返回 `0xFFB0`。
* 三处改成 `ActionArtifact`/`ActionMonster`/`ActionCastle` 家族，文件头写明「参考的 `OBJ_X` 是 action 值」。
  `LandscapeTownBasements` 与 `LandscapeFlags` 保持 `Castle`（参考在这两处特意用 `OBJ_NON_ACTION_CASTLE`）。

另两条要记住的：

* **`PostLoad` 的私有开关不是装饰**：编辑器重载传 `setTilePassabilities: false`（随后自己重算），
  且不抬高 UID 计数器（编辑器自己维护）。写成 `PostLoad()` 无参就会把这两条语义丢掉。
* **`ResetObjectUids` 不进 `Reset()`**：参考的地图装载也不重置它，装载后由文件里的 `LastObjectUid` 覆盖；
  只有编辑器「新地图」用它。

用例：`WorldLoadMapTests` 14 个方法 / 14 条（含真实 `Beltway.mp2` 72×72：索引连续、通行只能收窄、
既有全禁也有全通），`MapObjectCatalogTests` 新增 2 条（全表 action 位断言 + 城基/旗帜的 `Castle` 断言）。
`FHeroes2.Game.Tests` 522 → **539**，全解决方案 1130 条全绿，0 警告 0 错误。
`world_loadmap.cpp` 与 `world_object_uid.cpp` 从「未转」转「部分」，覆盖率 26/43/180 → **26/45/178**。

**下一步**：`Tile.setHero`/`getHero`（英雄占用决定格子通行，`updatePassability` 里那一支现在按
「无英雄」算）；之后再回 `world_loadmap` 补城堡表/俘获对象/信息块。

### 英雄占用：`Tile.setHero`/`getHero` + 最小 `Hero` 契约 ✅

英雄本体几千行，瓦片这一侧只需要两样东西：**id** 和**英雄脚下的对象类型**。所以本轮只立契约（ADR-068）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Heroes.Hero` | `heroes.h:63` | 73 个 id 的取值域、`ObjectTypeUnderHero`、`SetObjectTypeUnderHero`、`IsValidId`；**没有**军队/技能/法术书/宝物/路径 |
| `World.Heroes` / `AddHero` / `GetHero` | `AllHeroes` + `World::GetHeroes` | 列表 + 线性查找；重复 id 抛异常（参考靠「一 id 一槽」天然保证） |
| `Tile.OccupantHeroId` / `GetHero` / `SetHero` | `maps_tiles.cpp:429` / `:434` | 瓦片只存 `uint8` id；放置时把当前类型停放到英雄身上，取下时还原 |
| `Tile.GetMainObjectType(world, bool)` / `GetMainObjectTypeUnderHero` | `maps_tiles.cpp:463` / `:467` | `true` = 缓存（英雄站着时是 `OBJ_HERO`），`false` = 看穿英雄 |
| `fixMP2MapTileObjectType` 的英雄分支 | `maps_tiles.cpp:1353` | 水格被标成海岸时，有英雄就把「空」存到英雄身上 |

两个必须记住的点：

* **`MP2::OBJ_HERO` 是 action 值（183）**，`Mp2ObjectType.Hero`（55）是 `OBJ_NON_ACTION_HERO`，注释写着
  "Never set in maps"。写错成 55 的话，瓦片缓存一个游戏从不使用的类型，通行/寻路/渲染的 action 判定
  全部落空。这是 ADR-067 同类错误的第二次现身，本轮在写代码时抓到。
* **`updatePassability` 必须读 `false` 形态**（`maps_tiles.cpp:686`：英雄是临时对象），否则英雄站上去
  就把格子的通行改了。`PassabilityLooksThroughTheHero` 钉住这条：站上去前后掩码必须一样。

另外 `setMainObjectType()` 在参考里会 `world.resetPathfinder()`；端口的 setter 没有 world，复位写在
`SetHero` 里（唯一有 world 的调用点），并注明将来给 setter 加带 world 的重载。

用例：`HeroOccupancyTests` 13 条。`FHeroes2.Game.Tests` 539 → **552**，全解决方案 1143 条全绿，
0 警告 0 错误。覆盖率不变（`heroes` 行仍 1/0/11：这一片只有 id 表加一个查找，记「部分」会让口径变虚）。

### 地图对象查询 + `PostLoad` 三张缓存 ✅

「这块格子上站着谁」有了（ADR-068），接着补「哪种对象在哪几格」——寻路、AI、事件、传送门都要它。
参考里这一层是 `maps.cpp` 的三个查询 + `maps_tiles_helper.cpp` 的部件查找（ADR-069）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Tile.GetObjectPartByActionType` | `maps_tiles_helper.cpp:487` | 先主部件后地面部件，按 `getObjectTypeByIcn` 比类型；**只认 action 值** |
| `World.GetObjectPositions` | `maps.cpp:613` / `MapsIndexesObject:65` | 按缓存类型找格子；`ignoreHeroes` 的默认语义是**看穿英雄** |
| `World.GetObjectParts` | `maps.cpp:618` | 按**部件**找，连被小舟盖住的漩涡也能找到 |
| `World.DoesObjectExistOnMap` | `maps.cpp:601` | 用 `getMainObjectType(false)`，英雄站着也算对象存在 |
| `World.AllTeleports` / `AllWhirlpools` / `AllEyeOfMagi` | `world.cpp:1200-1230` | `PostLoad` 填的三张缓存，`Reset` 一并清 |

三个坑记一下：

* **`ignoreHeroes` 名字和语义相反**：参考比较 `getMainObjectType( !ignoreHeroes )`，所以默认 `true` 走的是
  「看穿英雄」。写反了的效果是：英雄踩在资源上，资源就「从地图上消失」。
* **又是 action 值**：`getObjectPartByActionType` 对没有 action 位的类型直接返回 null，调用方必须写
  `ActionStoneLiths`(164) 而不是 `StoneLiths`(36)。ADR-067/068 的同一坑，第三次。
* **漩涡要按部件找**：英雄下船后留下的小舟会把瓦片标成 boat，按类型找就漏了。

用例：`MapObjectQueryTests` 9 条。`FHeroes2.Game.Tests` 552 → **561**，全解决方案 **1152** 条全绿，
0 警告 0 错误。覆盖率不变（`maps.cpp` / `world.cpp` 仍是「部分」，这一片是补厚既有文件）。

### `Rand` 门面 + 传送门/漩涡端点 ✅

`Pcg32` 早就在，缺的是它上面那一层（线程级发生器、区间取数、洗牌、权重池），以及等它的两个端点查询
（ADR-070）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Rand.CurrentThreadRandomDevice` | `rand.cpp:85` | `ThreadLocal<Pcg32>`，OS 熵种子；同线程一条连续序列 |
| `Rand.Get` / `GetWithSeed` / `GetWithGen` | `rand.cpp:93/102/112` | 先交换上下界，所以 `Get(6)` = `[0,6]`；`GetWithSeed` 每次新建再丢弃 |
| `Rand.Get<T>(IReadOnlyList<T>)` | `rand.h:196/205` | 空表抛 `ArgumentException`（参考是 `assert`） |
| `Rand.Shuffle` / `ShuffleWithGen` | `rand.h:184/190` | 委托给 `Pcg32.Shuffle`（同一套 Fisher-Yates） |
| `Rand.Queue` | `rand.h:224` / `rand.cpp:121` | 0 权重丢弃、空池返回 0、按权重线性切片 |
| `World.GetTeleportEndPoints` / `NextTeleport` | `world.cpp:704/737` | 同 sprite 才配对；入口看穿英雄，候选用缓存类型 |
| `World.GetWhirlpoolEndPoints` / `NextWhirlpool` | `world.cpp:748/790` | 候选再按 UID 排除同一漩涡的其它格 |

**那个不对称是参考的，不是笔误**：入口用 `getMainObjectType(false)`（英雄踩在石阵上仍能传送），
候选用缓存类型（英雄站在目的地就把它排除）。`world.cpp:710` 与 `:725` 一行之差，语义完全不同。

`combineSeedWithValueHash` 没搬：它靠 `std::hash`，跨标准库本就不可复现，等地图随机生成时再定。

用例：`RandTests` 10 条 + `TeleportTests` 6 条。`FHeroes2.Core.Tests` 182 → **192**，
`FHeroes2.Game.Tests` 561 → **567**，全解决方案 **1168** 条全绿，0 警告 0 错误。
`rand.cpp` 的 ✅ 现在名副其实（只差 `combineSeedWithValueHash`）。

### 英雄表 `AllHeroes`：种族、肖像与招募池 ✅

ADR-068 给 `World` 挂的 `AddHero` 是端口替身（`List<Hero>` + 线性查找）。参考没有这东西：英雄从一开始
全在 `World::vec_heroes` 里，**槽的下标就是 id**（`heroes.cpp:2411`）。这一片把它换成正主，并补上按种族
挑人的池子（ADR-071）：

| C# | 参考 | 内容 |
| --- | --- | --- |
| `AllHeroes` | `heroes.h:823` / `heroes.cpp:2265` | 73 槽按 id 索引；`Get`/`Clear`/`Count`/按 id 遍历 |
| `AllHeroes.Init(MapVersion)` | `AllHeroes::Init` | 六族各九人 → SW 战役六人 → PoL 十一人（仅扩展地图）→ 调试英雄（release 不建） |
| `AllHeroes.GetHeroForHire` | `heroes.cpp:2322` | 三段填充 + 避开自定义肖像 + 优先没被别的王国挂出的，最后 `Rand.Get` |
| `HeroRaces` | `getHeroIdRangeForRace`（`heroes.cpp:38`）+ `Init` 的实参 | 1-54 九人一段，55-72 逐个给；`Race::NONE` 是 `{1,54}` |
| `Hero.Race` / `Color` / `PortraitId` / `Modes` / `IsAvailableForHire` | `heroes.h` + `heroes.cpp:1954` | 种族是构造参数，肖像初值 = id，`Modes` 只搬 `Recruit`/`Jail`/`Custom` 三位 |

几个要点：

* **空槽是 `null`**：参考放 `Heroes(UNKNOWN, KNGT)` 占位只是因为 C++ 容器不能空，而每个消费者第一句
  都是 `isValid()`——那正是 `null`。
* **只有 id ≤ 54 能被雇**：战役与 PoL 英雄永不上市（`heroes.cpp:2349`）。同种族的都雇完时会回退到
  任意种族，全都雇完才返回 null。
* **雇人按肖像去重，不按 id**：两个英雄可以共用一张脸。
* `HeroModes` 的值必须与参考逐位一致（存档里就是这个字），所以只搬三个用得到的位，其余等设置它们的系统。

用例：`AllHeroesTests` 13 条 + `HeroOccupancyTests` 里那条重复 id 改成「一个槽一个对象」。
`FHeroes2.Game.Tests` 567 → **580**，全解决方案 **1181** 条全绿，0 警告 0 错误。
覆盖率：`heroes` 行 1/0/11 → **1/1/10**（合计 26/46/177），`heroes.cpp` 进「部分」。

**下一步**：地图装载时按信息块建英雄（`world_loadmap.cpp` 的 `MP2::OBJ_HERO` 分支，信息块 76 字节里
的肖像字节决定 id）——它要先有 `Kingdom::AllowRecruitHero`（每国英雄上限）和 `Hero::LoadFromMP2` 的
可用子集（军队、神器、技能），以及 `Race::RAND` 按王国种族落地。

### 地图装载建英雄：`Mp2HeroInfo` 与 `Hero.LoadFromMP2` ✅

英雄的两半第一次接上：瓦片给位置和 `MINIHERO` 精灵（颜色 + 种族），76 字节的信息块给其余，
两者用瓦片 `quantity` 字的 13 位值（= `Mp2InfoBlock.ObjectId`）挂钩（ADR-072）。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Mp2HeroInfo`（Assets） | `heroes.cpp:421-579` 的字段注释 | 76 字节块逐字段读出，保留文件原始值（引擎 id = +1） |
| `World.CreateMapHeroes` | `world_loadmap.cpp:583-623` | 按 `Metadata` 找块的瓦片；肖像点名或抽招募池；建人 |
| `World.PlaceMapHero` | `world_loadmap.cpp:1583-1586` | 删 `MINIHERO`、`Tile.SetHero`，把英雄站到瓦片上 |
| `Hero.LoadFromMP2` | `heroes.cpp:421` | 清 mode、jail 位、位置/颜色、种族改判、自定义肖像/名字、巡逻 |
| `Hero.MapIndex` / `AllHeroes.GetAtMapIndex` | `SetIndex` / `AllHeroes::Get( Point )` | 端口用瓦片下标代替点坐标 |
| `HeroModes.Patrol` | `heroes.h:214` | `0x8000`，与存档逐位一致 |

几个要点：

* **选人两条路，第二条兜的是三件事**：肖像点名的英雄**已经站在地图上**、肖像点名的是 PoL 英雄而
  当前地图没有那个槽、以及块里根本没有肖像——参考的 `if ( !hero || !hero->isAvailableForHire() )`
  把它们一起接进 `GetHeroForHire( race )`。所以「PoL 肖像在非扩展地图上留空」是错的，实际是抽一个
  同族英雄。
* **自定义肖像自己就会置 `Custom`**（`heroes.cpp:486-488`），哪怕种族没被改判。
* **`MiniHero` 必须在 `SetHero` 之前删**：否则瓦片的主对象仍是编辑器图标，英雄无处可站。
* **顺序不能反**：先 `CreateMapHeroes`，再 `FixMP2MapTileObjectType` + `PlaceMapHero`——`SetHero` 要把
  已经修正过的对象类型寄存到英雄身上。
* **`LoadFromMP2` 不重置军队与技能**：默认主技能、初始法术、默认军队、神器、经验、二级技能、法术点与
  移动点都要 `Army`/`Artifact`/`Skill`/`Spell` 与升级表，留给各自系统。现在的英雄只有「身份」。
* **缺 `Kingdom::AllowRecruitHero`**：参考在一国英雄满额时丢弃多余的，端口没有王国，所以只会多不会少。

用例：`Mp2HeroInfoTests` 4 条（长度、全零、逐字段、名字终止符）+ `WorldHeroLoadTests` 10 条。
`FHeroes2.Assets.Tests` 264 → **268**，`FHeroes2.Game.Tests` 580 → **590**，全解决方案 **1195** 条全绿，
0 警告 0 错误。覆盖率不变（`heroes` 1/1/10，合计 26/46/177）。

**下一步**：`Hero` 的军队与技能（`LoadFromMP2` 里剩下的那半拉）、`MapRace.Random` 按王国种族落地，
以及 `MP2::OBJ_JAIL` 分支（牢房里的英雄走 `isInJail = true` 并把第 60 字节当种族）。

### 牢房里的英雄：第 60 字节的种族 ✅

`MP2::OBJ_JAIL` 那一支（ADR-073）。它和普通英雄共用 `Hero.LoadFromMP2`，差别只在参数：
瓦片上没有 `MINIHERO` 精灵，所以种族来自信息块第 60 字节，颜色是 `PlayerColor.None`，
`isInJail = true`。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.FindInfoBlockTile` | `world_loadmap.cpp:473-483` | 找块属于哪块瓦片，认 `Hero`/`ActionHero`/`Jail`/`ActionJail` |
| `World.CreateJailHero` | `world_loadmap.cpp:528-582` | 第 60 字节 → 种族（0…5，其余告警当骑士）；无主 + 牢里 |
| `World.SelectMapHero` | `:567-573`、`:604-610` | 两支共用的选人：肖像点名 → 池子 |
| `World.CreateMapHero` | `:583-623` | 原先那段从瓦片精灵取颜色/种族 + 池子 |

要点：

* **同一个字节两种含义**：巡逻英雄是巡逻半径，牢房英雄是种族——`Mp2HeroInfo.PatrolDistanceOrJailRace`
  就是为这件事命名的。
* **瓦片不归英雄**：参考没有 `setHero`，牢房对象留在瓦片上，英雄只是把坐标暂存到 `MapIndex` 等解救；
  端口的 `Tile.OccupantHeroId` 因此仍是 `UnknownId`，`PlaceMapHero` 只看 `Hero`/`ActionHero`，天然跳过。
* **不做 action 转换**：文件里存的是非 action 的 `OBJ_JAIL`(123)，`Tile.MainObjectType` 保留它，
  访问时由 `Mp2ObjectTypes.BaseActionType` 给 `ActionJail`(251)——与「缓存存文件值、换算在使用处」一致。
* **`SelectMapHero` 顺手去掉了一次复查**：返回 `Hero` 对象后，ADR-072 里「拿到 id 再 `_heroes.Get` 判空」
  就是多余的了。

用例：`WorldHeroLoadTests` 再加 4 条（牢房与 `PlayerColor.None`、改判种族、非法种族回落骑士、
无肖像抽池子且瓦片仍属于监狱）。`FHeroes2.Game.Tests` 590 → **594**，全解决方案 **1199** 条全绿，
0 警告 0 错误。覆盖率不变。

**下一步**：信息块剩下的六支（告示、瓶子、事件、狮身人面、每日事件、传闻）——**都已落**（前两支与
事件见下、后三支见再下）；`Hero` 的军队与技能仍是 `LoadFromMP2` 的另一半。

### 告示与地面事件：地图对象容器 ✅

`MP2::OBJ_SIGN`/`OBJ_BOTTLE` 与 `MP2::OBJ_EVENT` 两支（ADR-074）。这是端口第一次有「地图对象」这一层：
对象自己带数据、按瓦片下标索引，和参考的 `map_objects` 对齐。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MapObject` | `maps_objects.h:42` | 瓦片下标 + UID（两者恒等，`setUIDAndIndex`） |
| `MapSign` | `maps_objects.cpp:286` | marker + 消息；块长就是串长 |
| `MapEvent` | `maps_objects.cpp:44` | 七种资源、神器（+1）、AI/一次性标志、六色位、消息 |
| `MapObjects` | `world.h:73` | 按瓦片下标存取，同键覆盖 |
| `World.ReadInfoBlocks` | `world_loadmap.cpp:466-688` | 原 `CreateMapHeroes`，按对象类型分派四支 |
| `Tile.UpdateObjectType` | `maps_tiles.cpp:1599-1611` | 事件数据不在就清成 `None` |

要点：

* **资源顺序是坑**：文件里是 wood/mercury/ore/…，`Funds` 的构造参数却是 ore 打头，所以用具名参数写。
* **神器 +1**：文件从 0 数、引擎从 1 数。
* **空告示不填随机文案**：参考从本地化表随机抽一句，端口没有语言表与装载期随机源，留空并记为缺口。
* **装载期不会清空事件类型**：`UpdatePassabilities` 只重算无类型的瓦片（参考也是这个条件），所以坏地图上
  事件仍在；真正的清空发生在对象离开瓦片后的重算，测试显式调用 `UpdateObjectType` 钉住这条语义。

用例：`WorldMapObjectTests` 6 条；`FHeroes2.Game.Tests` 594 → **600**，全解决方案 **1205** 条全绿，
0 警告 0 错误。覆盖率不变（合计 26/46/177，`maps_objects.cpp` 原本就在「部分」里）。

**下一步**：狮身人面（与事件同构、多 7 个答案）、每日事件与传闻（`World` 级列表）、城堡表（要
`Castle` 系统）。

### 狮身人面、每日事件与传闻：无瓦片的块 ✅

信息块剩下能独立落地的三支一次做完（ADR-075）。前一支有瓦片，后两支**没有对象指向它**——它们和
告示/事件挤在同一个 `InfoBlocks` 里，靠块自己的字节区分。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MapSphinx` | `maps_objects.cpp:166` | marker 0、七资源、神器、答案个数、8×13 字节答案、136 起的问题 |
| `MapSphinx.IsCorrectAnswer` | `maps_objects.cpp:273` | 只比前 4 个字符（原版行为），答案小写 |
| `MapSphinx.IsValid` | `maps_objects.h:117` | `validate()`：无奖励或无问题/答案即无效 |
| `EventDate` | `world.cpp:1517` | marker 0、可负资源、AI 标志、首日/周期、六色、消息 |
| `EventDate.IsAllowed` / `IsDeprecated` | `world.cpp:1647`、`world.h:177` | 日期判定（日期系统未移植，先钉语义） |
| `World.Rumors` / `DailyEvents` | `world.h:558`、`:559` | 无瓦片块的两个去处 |
| `World.ReadWorldBlock` | `world_loadmap.cpp:656-684` | `[0]==0 && [42]==1` → 事件，否则 ≥9 字节 → 传闻 |

要点：

* **前四字符**：玩家输入与答案都截到 4 个字符，所以 `abram` 答对 `abracadabra`——原版行为，不是 bug。
* **「无神器」没有干净表示**：文件里 0 会被 `+1` 变成第一个神器，所以「没有」只能写 `0xFFFF`，加一后
  越界。参考不校验，端口照做；`validate()` 只认 `Artifact::UNKNOWN`(0)。
* **城堡块必须能匹配到瓦片**：它也以 `0x00` 开头、至少 70 字节，找不到瓦片就会被当传闻读出一串垃圾。
  所以 `FindInfoBlockTile` 的匹配集合扩到了城堡（`HasReadableInfoBlock` → `HasInfoBlock`），匹配到的
  城堡块在分派里 `default` 忽略。
* **判别顺序**：先事件（`[42]==1`）后传闻，反了会把事件读成垃圾文本。

用例：`WorldSphinxAndEventsTests` 7 条；`FHeroes2.Game.Tests` 600 → **607**，全解决方案 **1212** 条全绿，
0 警告 0 错误。覆盖率不变（`maps_objects.cpp` 的**装载**转了，问答题/信息框那一侧没转）。

**下一步**：城堡表（要 `Castle` 系统，最大的一块）、俘获对象、AI 区域。

### 日期系统与每周/战斗对象分类 ✅

`World` 的时钟（ADR-076）与 `mp2.cpp` 的两张对象分类表、`CountObeliskOnMaps()`（ADR-077）。时钟是
`NewWeek`/`NewMonth` 与兽人月等玩法的前提，两张表是 `NewWeek` 刷新周产建筑与 AI 计算移动力的判据。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.Day/Week/Month/DayCount/WeekCount` | `world.h:371-394` | 显示值（1 起、按周/月取模）与总数 |
| `World.IsBeginWeek/IsBeginMonth/IsLastDay/IsFirstWeek/IsLastWeek` | `world.h:396-419` | 五个裸取模判定 |
| `World.DateString()` | `world.cpp:888` | `"month: X, week: Y, day: Z"`，不本地化 |
| `World.NewDay()` | `world.cpp:425-460` | 三层嵌套递增 + 剪掉过期每日事件 |
| `Mp2ObjectTypes.IsWeekLife/IsBattleLife` | `mp2.cpp:511-576` | 周产/巢穴（含 `Hero`）与幸运士气加成（含黑猫） |
| `World.CountObeliskOnMaps()` | `world.cpp:900` | 数方尖碑，下限 6 |

要点：

* **0 是「最后一天」**：`LastDay()`/`LastWeek()` 是裸取模，新世界 `_day == 0` 于是 `Day == 7`、`Week == 4`，
  而 `IsBeginWeek` 为假。照直觉把 0 当第一天会让整个周循环错一天，测试第一条钉住它。
* **`NewDay` 只搬时钟与剪枝**：`NewMonth`（怪物之月）/`NewWeek`（周产刷新、怪物重掷、招募重置）与
  三张表的 `NewDay`/`NewWeek` 依赖 `Week`/`Kingdoms`/`Castles`/`Monster`，按参考顺序在注释里标了插入点。
* **剪枝用的是「刚过去的那一天」**：单次事件在触发后的第二天仍在表里，第三天才消失——参考行为，不是差一。
* **两张表比较非 action 值**：传 `Action*` 一律 `false`（参考调用处传 `getMainObjectType( false )`），
  用 `BaseActionType` 归一化会多刷一批对象。两表不相交也用全枚举遍历守住。

用例：`WorldDateTests` 9 条 + `WorldTests` 2 条（方尖碑）+ Assets 的 `Mp2ObjectTypesTests` 47 条；
`FHeroes2.Game.Tests` 607 → **618**，`FHeroes2.Assets.Tests` 268 → **315**，全解决方案 **1270** 条全绿，
0 警告 0 错误。覆盖率不变（合计 26/46/177）。

**下一步**：城堡表（要 `Castle` 系统，最大的一块）、俘获对象（`World::CaptureObject`，卡在
`Tile.setOwnershipFlag`）、AI 区域；日期侧剩 `GetWeekType`/`NewWeek`/`NewMonth`。

### 俘获对象、迷雾位、瓦片元数据读写 ✅

三片一起：`map_captureobj`（ADR-078）、瓦片级迷雾（ADR-079）、`maps_tiles_helper.cpp` 的元数据读
写（ADR-080）。它们互不依赖，但都落在 `Tile` 与 `World` 上，且都是「拾取/占矿/AI 绕路判断」的前置。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `CapturedObject` / `CapturedObjects` | `world.h:117-166`、`world.cpp:171-275` | 瓦片索引 → （对象类型, 颜色），有序 |
| `World.CaptureObject` | `world.cpp:815` | 记录类型与主人（城堡分支、旗帜精灵未搬） |
| `World.ColorCapturedObject` / `GetCapturedObject` / `HasCapturedObject` | `world.h:438-456` | 查询、插入语义、键存在性 |
| `World.CountCapturedObject` / `CountCapturedMines` | `world.cpp:801` | 按类型计数；木=锯木厂、水银=炼金实验室 |
| `World.ResetCapturedObjects` | `world.h:451` | 只改数据，不重画旗帜 |
| `Tile.FogColors` / `IsFog` / `IsFogForAll` | `maps_tiles.h:301-306` | 单色与联盟两种判定 |
| `Tile.ClearFog` / `FogDirection` / `SetFogDirection` | `maps_tiles.cpp:1576`、`maps_tiles.h:356` | 清位与绘制方向缓存 |
| `Tile.GetFundsFromTile` / `GetArtifactFromTile` | `maps_tiles_helper.cpp:570-761` | 拾取所得；卷轴退化成 `SpellScroll` |
| `Tile.DoesTileContainValuableItems` | `maps_tiles_helper.cpp:808` | AI 绕路判据（已访问的矿/箱子为假） |
| `Tile.SetResourceOnTile` / `SetBarrierColorOnTile` / `ResetObjectMetadata` | `maps_tiles_helper.cpp:719-854` | 三个 lane 的写入口 |
| `ArtifactCaptureCondition` | `maps_tiles_helper.h:53` | 守卫条件枚举，编号即地图数据 |

要点：

* **`Get` 插入、`GetColor` 不插入**：「条目存在但无主」与「从未易主」是两种状态，`HasCapturedObject`
  专门暴露前者。
* **木材与水银不走矿**：`OBJ_SAWMILL` / `OBJ_ALCHEMIST_LAB` 不是 `OBJ_MINE`，计数必须按对象类型，
  其余资源才读 `metadata[0]`。
* **迷雾是减法**：新瓦片对所有人黑（`PlayerColors.All`），`ClearFog` 清位；忘了初始化时表现为
  「看不见」而不是「全图可见」。
* **`IsFog` 与 `IsFogForAll`**：参考两个同名重载（任一 / 全部），C# 位集只有一个类型，联盟语义单独命名。
* **`Wagon` 的 lane 1 是资源类型、lane 2 是数量**，不是「两条金币车道」——测试第一版就写错，注释也跟着错。
* **卷轴只返回 `ArtifactType.SpellScroll`**：`Artifact` 未转，法术 id 留在 `metadata[1]`，注释写明缺口。

用例：`CapturedObjectTests` 9 条 + `TileFogTests` 5 条 + `TileMetadataTests` 15 条；
`FHeroes2.Game.Tests` 618 → **647**，全解决方案 1270 → **1299** 条全绿，0 警告 0 错误。
覆盖率不变（合计 26/46/177）。

**下一步**：城堡表（要 `Castle` 系统，最大的一块）、AI 区域、`World::NewWeek`/`NewMonth`
（要 `Week`/`Kingdoms`/`Monster`）、`Maps::ClearFog` 与 `updateFogDirectionsInArea`（要半径揭示）。

### 对象分类，地形代价，条件通行 ✅

三片一起：`mp2.cpp` 剩下的十一个分类谓词（ADR-081）、`ground.cpp` 的移动代价与地形图像边界（ADR-082）、
`maps_tiles.cpp` 的对象感知通行（ADR-083）。前两片是「游戏数据 + 纯函数」，第三片是路径查找器的最后一
块地基——`heroes_move.cpp` / `world_pathfinding.cpp` 现在欠的只剩移动点记账本身。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Mp2ObjectTypes.IsDayLife` | `mp2.cpp:505` | 只有魔法井每日恢复 |
| `Mp2ObjectTypes.IsInGameActionObject`（两个重载） | `mp2.cpp:590`、`:635` | action 位 + 基类型有 action 孪生；`ActionEvent` 是唯一例外 |
| `Mp2ObjectTypes.IsWaterActionObject` | `mp2.cpp:599` | 水上的十六个对象；`default` 回落 in-game 判据 |
| `Mp2ObjectTypes.IsRevisitAllowedForObject` | `mp2.cpp:578` | 排除漩涡 |
| `Mp2ObjectTypes.IsValuableResourceObject` | `mp2.cpp:669` | 十六项 + `IsPickupObject` |
| `Mp2ObjectTypes.IsCaptureObject` | `mp2.cpp:700` | 五个可易主对象 |
| `Mp2ObjectTypes.IsPickupObject` | `mp2.cpp:717` | 十个拾取后消失的对象 |
| `Mp2ObjectTypes.IsArtifactObject` | `mp2.cpp:739` | 与 `Tile.GetArtifactFromTile` 的 switch 同一张表 |
| `Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected` | `mp2.cpp:760` | 宝物与怪物，其余看 `IsCaptureObject` |
| `Mp2ObjectTypes.IsSafeForFogDiscoveryObject` | `mp2.cpp:775` | 石阵/漩涡/告示，其余看「不是 action 对象」 |
| `Mp2ObjectTypes.IsNeedStayFront` | `mp2.cpp:793` | 十个需要站在前面的对象 + 拾取物 |
| `MapGrounds.GetPenalty` | `ground.cpp:169` | 九行代价表 × 四档寻路 |
| `MapGrounds.RoadPenalty` 等五常量 | `ground.h:72-76` | 75 / 100 / 75 / 200 |
| `MapGrounds.IsTerrainTransitionImage` | `ground.cpp:96` | 水/土 16、其余 38、沙滩 0 |
| `MapGrounds.ContainsEmbeddedObjects` | `ground.cpp:119` | 45 / 23 / 7 三档，边界用 `>` |
| `Tile.IsSuitableForUltimateArtifact` | `maps_tiles.cpp:1007` | 干燥、无主部件、无地面部件 |
| `Tile.IsSuitableForDisembarkation` | `maps_tiles.cpp:1028` | 海岸直接通过；石头挡上岸 |
| `Tile.IsSuitableForSummoningBoat` | `maps_tiles.cpp:1064` | 水面 + 灯塔旗可越过 |
| `Tile.IsPassableFrom(World, …)` | `maps_tiles.cpp:1102` | 雾 → 船 → 友军 → 掩码 |
| `Hero.IsFriends` | `heroes.h:437` | 暂等价于同色（联盟缺口只有这一处） |
| `Game.Heroes.SkillLevel` | `Skill::Level` | `None`/`Basic`/`Advanced`/`Expert` |

要点：

* **回归的教训**：新增用例前先确认同名文件不存在。这一片第一版把 ADR-064 的 `TilePassabilityTests.cs`
  （独立通行 14 条）整个覆盖成新用例，全量数字从 647 只涨到 652 才露馅；已 `git checkout --` 复原，
  新用例另立 `TileObjectPassabilityTests.cs`。
* **两次「测试错、参考对」**：`IsSuitableForSummoningBoat` 根本不看对象类型（已停着船的格子仍返回
  `true`，两条船靠 `IsPassableFrom` 的 `ActionBoat` 分支区分）；`IsRevisitAllowedForObject( Whirlpool )`
  两条路都是 `false`。都是先红后改测试，并在断言旁写下原因。
* **第三条也是「测试错、参考对」**：`IsWaterActionObject` 的 `default` 是
  `IsInGameActionObject(type)`，不是 `false`——水中的矿从船上可访问。
* **影子索引**：`ObjnArti`/`ObjnRsrc` 的 `icnIndex % 2 == 0` 是影子、`1` 是本体，拿 0 当石头测不出东西。
* **海岸不可替代**：`PassableDirections` 说海岸可过，但只有从水上来的英雄能上岸——这是两个判断必须并存的
  原因，也是 `isPassableFrom` 里掩码必须放在最后的原因。
* **地形图像边界用 `>`**：恰好 +45 的那张仍是纯地面；「统一成区间」会静默丢掉一格。

用例：`Mp2ObjectTypesClassificationTests` 34 条（Assets）、`MapGroundPenaltyTests` 8 条、
`TileObjectPassabilityTests` 12 条。`FHeroes2.Assets.Tests` 315 → **349**，`FHeroes2.Game.Tests` 647 →
**667**，全解决方案 1299 → **1353** 条全绿，0 警告 0 错误。覆盖率不变（合计 26/46/177）：`mp2.cpp` 早已是
✅、`maps_tiles.cpp` 与 `ground.cpp` 早已在「部分」里，这一片补的是它们名下的缺口。

**下一步**：`heroes/heroes_move.cpp` + `world/world_pathfinding.cpp`（通行三件已齐：掩码、独立通行、
条件通行；欠移动点记账 `GetPenalty` 已可用、`Skill::Level` 已就位），以及 `maps/visit.cpp` 的访问分派。

### 地图扫描与守卫，距离度量，雾半径修正 ✅

`maps.cpp` 收尾的第二片：把「周围有什么」的两个答案（**扫描**与**守卫**）搬到 `World`，把两个「大致多远」
的度量搬到 `MapGeometry`，顺手修掉雾半径上一个把**错误当成参考表**的 bug。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.ScanAroundObject` | `maps.cpp:570`、`:588` | 单环扫描；两个重载合成一个默认参数 |
| `World.HasNearbyObject` | `maps.cpp:576` | 见到第一个就返回，始终看穿英雄 |
| `World.IsValidForDimensionDoor` | `maps.cpp:582` | 可进入 + 水陆同侧 + 不是 in-game action 对象 |
| `World.ScanAroundObjectWithDistance` | `maps.cpp:594` | 方形邻域，按距离排序 |
| `World.GetObjectPositions( center, … )` | `maps.cpp:632` | 全图命中按距离排序 |
| `World.GetMonstersProtectingTile` | `maps.cpp:664` | 守卫清单；`checkObjectOnTile` 对应参考的早退 |
| `World.IsTileUnderProtection` | `maps.cpp:639` | 中心格优先 |
| `World.ForEachMonsterProtectingTile` | `maps.cpp:100` | 八邻域的固定顺序 + 斜向兜底 |
| `MapGeometry.GetApproximateDistance` | `maps.cpp:691` | `max + min / 2` |
| `MapGeometry.GetStraightLineDistance` | `maps.cpp:704` | `hypot` 截断 |
| `MapGeometry.GetSquaredDistance` | `maps.cpp:184` | `ComparisonDistance` 的比较算术，提成可测的方法 |
| `MapFog.SquaredRevealLimit`（修正） | `maps.cpp:531` | 减法只在半径 < 6 时生效 |

要点：

* **`ActionMonster` 不是 `Monster`**：地图上存的是带 128 位的 `OBJ_MONSTER`。第一版写 `Mp2ObjectType.Monster`，
  守卫一条都找不到，测试第一轮就红。
* **一次真实 bug**：`getSquaredScoutingRadiusLimit` 的减法带 `if ( scoutingDistance < 6 )` 条件，端口漏了条件，
  半径 6 的上限算成 43（应为 49）、9 算成 91（应为 100）——寻路术宝物把视野推到 7 以上时一直是错的。
  更糟的是测试里那行 `[InlineData(6, 43)]` 把错误值当成了「参考表」。已改公式（`< 6` 与 `_` 两支分开）、
  改期望（`6 → 49`，补 `9 → 100`）。教训写进 ADR-086。
* **守卫不是「周围的怪物」**：要站在前面使用的对象（`IsNeedStayFront`）不会引发战斗，除非瓦片上本身就是
  怪物——那条早退是两条入口的分水岭。
* **两个入口的早退不能互换**：`GetMonstersProtectingTile` 先问对象是否「站在前面用」，`IsTileUnderProtection`
  先问瓦片上是不是怪物。
* **八邻域顺序是契约**：参考手工展开八个方向，「先报告哪只怪物」对上层有意义；改用 `GetAroundIndexes`
  会换顺序。
* **排序不稳定是参考的性质**：距离相同的命中顺序未定义，`std::sort` 与 `List.Sort` 都不稳定，测试不依赖。
* **两支没测到**：`ForEachMonsterProtectingTile` 的四条斜向兜底分支（要造出「斜向位被清、竖直位仍在」的掩码，
  得在邻居上摆高物件并重算全图）与 `getVisibleMonstersAroundHero`（要 `Heroes` 的视野距离）——都记在
  ADR-084 里，没装作测过。

用例：`WorldMapScanTests` 15 条 + `MapFogTests` 多一行（18 → 19 条）。`FHeroes2.Game.Tests` 667 →
**683**，全解决方案 1353 → **1369** 条全绿，0 警告 0 错误。覆盖率不变（合计 26/46/177）：`maps.cpp` 仍记
「部分」，只是部分清单里几何、距离、扫描、守卫都已经划掉。

**下一步**：`heroes/heroes_move.cpp` + `world/world_pathfinding.cpp` —— 扫描与守卫已可用（AI 的
「这格能不能过、过去会不会打起来」两块都齐了），通行与地形代价也已就位，剩下的只有移动点记账与路径查找表。

### 英雄的技能与经验 ✅

英雄身上两块「不依赖任何系统」的数据：**次要技能**（枚举、数值表、种族初始技能、`SecSkills` 容器）
与**经验/等级**（40 行阈值表 + 尾部增长公式）。它们是把英雄从「一个位置 + 一个种族」变成
「一个有属性的单位」的最后一块拼图。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `SecondarySkill` | `skill.h:47-65` | 14 项，id 即表下标 |
| `SecondarySkills.All` | `Skill::allSecondarySkills` | 14 项按 id 序 |
| `SecondarySkills.InitialFillOrder` | `skill.cpp:626-681` | 构造器十四个 `if` 的书写顺序 |
| `SecondarySkills.WitchsHutSkills` | `game_static.cpp:265` | 12 项，无领导/招魂 |
| `SecondarySkills.GetValue` | `skill.cpp:119` | `secondarySkillValuesPerLevel` 的一行 |
| `SecondarySkills.GetDiplomacySurrenderCostDiscount` | `skill.cpp:898` | 40 / 30 / 20 / 0 |
| `PrimarySkillValues` | `skill_static.h:37-43` | 攻/防/力/知四元组 |
| `SecondarySkillTable` | `skill_static.h:45-61` | 14 值一行，下标即技能 |
| `FactionProperties` / `Factions.Get` | `game_static.cpp:37-89`、`:205` | 六族一行；非法种族返回 `null` |
| `SecondarySkillLevel` | `skill.h:67-126` | `{ 技能, 等级 }` + `IsValid` |
| `SecSkills` | `skill.cpp:621-780` | 最多 8 槽，可含空槽；`Add` / `FillMax` |
| `HeroExperience.FromLevel` | `heroes.cpp:1512-1602` | 0–39 级硬编码，40+ 按 1.2% |
| `HeroExperience.LevelFrom` | `heroes.cpp:1604` | 反查；1000 经验 = 2 级 |
| `HeroExperience.MaxValue` | `getExperienceMaxValue()` | 2990600（39 级阈值） |

要点：

* **参考的字段是字母序**：`Skill::SecondarySkillValues`（`skill_static.h:45-61`）的十四个成员按技能名
  排（`archery`、`ballistics`…），`game_static.cpp` 的行字面量也跟着字母序写。端口改成 id 序时
  **只重排一次**（写在表里），取值路径不做映射；测试对十四行**逐行**断言，而不是抽查——
  「一对技能互换」是这类错误唯一的表现形式，抽查正好漏过去。
* **`Ballistics` 全零**：炮术的 25/50/100 在表里属于 Pathfinding（移动点），炮术那行是 `(0,0,0)`
  ——它的效果写在攻城里，不在数值表里。照抄字母序会把它和寻路术换掉。
* **填充顺序第三套**：种族初始技能既不是 id 序也不是字母序，是构造器十四个 `if` 的书写顺序。
  骑士是 `leadership`（第 9 个 `if`）在 `ballistics`（第 10 个）之前，英雄界面照这个顺序列。
* **空槽是数据**：`SecSkills` 存得下 `{ Unknown, None }`，`FillMax` 补到 8 个是为了存档定宽。
  `Count`/`TotalLevel`/`GetLevel`/`Add` 全都靠**跳过无效项**处理空槽，与参考一致。
* **第 9 个技能静默丢弃**：`Add` 的三步（升级 → 填空槽 → 追加）之后没位置就什么也不做。
  参考就是静默的；保证不出现第 9 个是升级流程的责任。
* **阈值属于下一级**：`FromLevel(1) = 1000` 意味着「1000 经验就是 2 级」，`LevelFrom(1000) == 2`。
  第一版测试断言 `LevelFrom(threshold) == level`，红了之后确认是**测试错、参考对**，
  测试与两个函数的注释一起改清楚。
* **尾部用浮点**：40 级起 `previous + round(step * 1.2 / 100) * 100`，保留参考的浮点与
  `AwayFromZero` 舍入；改成整数运算后中间值舍入点会漂，几十级后差出几百点。
* **没转的两支**：`FindSkillsForLevelUp`（要两个随机种子与按种族权重抽取）、
  `game_static.cpp` 里带系统依赖的其余函数（护城河减防、城墙远程惩罚、绿洲移动点、中立怪上限、
  漩涡失踪率、雾发现距离）。前者等升级界面，后者等调用它们的系统。

用例：`SecondarySkillsTests` 24 条（14 行数值 + None/Unknown + 排列检查 + 女巫小屋 + 外交折扣）、
`FactionsTests` 27 条（六族逐族 + 两套权重各和为 100 + 分界 + 非法种族）、`SecSkillsTests` 11 条、
`HeroExperienceTests` 55 条（40 行表 + 每级阈值一致性 + 尾部公式）。
`FHeroes2.Game.Tests` 683 → **824**，全解决方案 1369 → **1510** 条全绿，0 警告 0 错误。
覆盖率：`heroes` 行 1/1/10 → **1/2/9**（`skill.cpp` 转部分），`game` 行 2/3/23 → **2/4/22**
（`game_static.cpp` 转部分），合计 26/46/177 → **26/48/175**。

**下一步**：`heroes/heroes_move.cpp` + `world/world_pathfinding.cpp` —— 技能的数值与等级已经可用，
通行与地形代价也就位，剩下的只有移动点记账（寻路术那行 25/50/100 终于有地方用了）与路径查找表。

## 寻路与路线 ✅（2026-11-16）

`world/world_pathfinding.cpp` 的玩家侧与 `heroes/route.cpp` 是同一件事的两半：前者回答「走哪条路、
多贵」，后者回答「这条路记在哪、怎么一步步走掉」。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `WorldPathfinder` | `world_pathfinding.cpp:40-140` | 缓存、标签修正式搜索、八向松弛、「最后一格」 |
| `WorldNode` | `world_pathfinding.h` 的 `WorldNode` | `{ From, Cost, RemainingMovePoints }` |
| `PlayerWorldPathfinder` | `world_pathfinding.cpp:220-330` | 五元组缓存、`buildPath`、`processCurrentNode` |
| `World.Pathfinder` | 参考的全局 `world.pathfinder` | `??=` 惰性创建；`ResetPathfinder()`/`LoadMap` 丢缓存 |
| `RouteStep` | `route.h` 的 `Route::Step` | `{ Index, From, Direction, Penalty }`，代价是**单步** |
| `HeroPath` | `route.h:120-215`、`route.cpp:38-414` | 队列语义 + 传送标记 + 绿格计数 |

要点：

* **队列的追加顺序是结果的一部分**：参考用 `std::vector` 当队列、边遍历邻居边追加，等代价路线不覆盖。
  换成 `Queue<int>` 语义等价但实现不同、换成优先队列则**不等价**——等代价时选哪条路会变。
  见 `WorldPathfinder.ProcessWorldMap` 的索引循环。
* **地形代价取出发格**：`getMovementPenalty()` 用 `fromTile.ground`，只有道路要两端都是道路
  （`RoadPenalty = 75`）。取目标格会让整张地图的代价系统性偏低，且很难在单格测试里看出来。
* **「最后一格」规则**：剩余点 ≥ 出发格代价但 < 目标格代价时可以走，并花光剩余点。
  AI 寻路器报 `maxMovePoints == 0`，因此**不**享受折扣——这条判据写在基类里，AI 侧不用重写。
* **船的对角切角查的是拐角背后**：不是目标格。目标格自己的通行是第二个问题。
* **怪物「可到达、不可穿过」**：不可穿过的格子照样写入缓存（这样「能走到它旁边」成立），但不展开；
  有守卫时只从该格试探「走向怪物」的一步，且**不入队**——打不打是玩家的事。
* **`world` 全局变量的替换**：参考读 `world.pathfinder`，端口把 `World` 注入构造函数并由
  `World.Pathfinder` 持有，两个世界不共享缓存。
* **`Route::Path` 存的 `from` 是必需的**：相邻格的 `from` 可以反推，但**起点**不行，
  而「英雄是否还站在路线起点」靠 `FrontFrom` 判断。
* **`Direction.Unknown` 就是传送**：方向未知 + 落点在图内 = 跳跃；方向已知 + 落点在图内 = 一步。
  这个编码参考里没有注释，是从两个互为补集的判断反推的。
* **`GetAllowedSteps` 不做「最后一格」折扣**：它算的是界面标绿、今天真能走完的步数，
  与寻路器的「能不能到」是两个问题。
* **端口替换**：参考的 `const Heroes *` 指针改成参数——`reEvaluateIfNeeded()` 收五个值
  （起点/颜色/剩余点/寻路术等级/一回合上限），`hasAllowedSteps()`/`GetAllowedSteps()` 收 `movePoints`。
  等 `Hero` 有移动点后加重载转调，调用点不用改。
* **没转的两支**：`AIWorldPathfinder`/`ObjectValueStorage`（要军队与对象价值表）、
  `HeroPath.GetIndexSprite`（260 行 `ICN::ROUTE` 精灵表，等渲染侧）、`HeroPath.String`（调试字符串）。
* **有意不移植的断言**：参考假设方形地图（偏移量按行宽算），端口删掉该断言——偏移是行增量，
  对任意形状都精确，边界由 `IsValidDirection()` 守住；测试地图不必是方形。

用例：`WorldPathfinderTests` 26 条（直线/对角代价、地形取出发格、寻路术四档沙漠、道路两端、雾、
水陆、切角、「最后一格」三态、起点无路径、链条连续、最后一行不回绕、怪物、缓存复用、越界、
世界唯一寻路器、换地图换寻路器）、`HeroPathTests` 21 条（空路线约定、`SetPath`/`Show`/`Hide`、
首尾索引、`NextStepDirection`、`PopFront`/`Truncate`、移动与传送合法性、`HasAllowedSteps`/
`GetAllowedSteps` 各档、`CalculatePenalty`、与 `BuildPath` 联调）。
`FHeroes2.Game.Tests` 824 → **871**，全解决方案 1510 → **1557** 条全绿，0 警告 0 错误。
覆盖率：`world` 行 0/3/2 → **0/4/1**（`world_pathfinding` 转部分），`heroes` 行 1/2/9 → **1/3/8**
（`route.cpp` 转部分），合计 26/48/175 → **26/50/173**。

**下一步**：`heroes/heroes_move.cpp` —— 要先让 `Hero` 拥有军队与移动点，否则整支都是动画记账；
或者转 `world_pathfinding.cpp` 的 AI 侧，但它同样卡在军队与对象价值表上。

## 军队、移动点与英雄行走 ✅（2026-11-16）

先给 `Hero` 装上军队与移动点，再转 `heroes/heroes_move.cpp`：前者是后者的记账基础
（每走一格扣的是「最慢兵种决定的每日额度」），后者是前者的消费者。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MonsterStatistics` | `monster/monster_info.cpp` | 通用数值表（等级/生长/造价）与战斗数值表（攻防伤血速射） |
| `DwellingMonsters` | `Monster::FromDwelling` | 住所索引 × 种族 → `MonsterType` |
| `Troop` | `army/army_troop.cpp` | `{ Monster, Count }` + 按「每兵 × 数量」算的血、伤、造价 |
| `Army` | `army/army.cpp` | 五槽、最慢/最快/首个、合并、清空、`Reset` 初始军队 |
| `Hero.Army.cs` | `heroes.cpp:972`、`:1782`、`heroes_move.cpp:468` | `Army`/`SecondarySkills`/`MovePoints`、`GetMaxMovePoints`、`_updateMovementPoints`、`_applyMovementPenalty` |
| `Hero.Movement.cs` | `heroes/heroes_move.cpp` | 九帧状态机、转向块、像素偏移、帧循环与四个小查询 |
| `IHeroMovementHandler` | 清雾/小地图/对象动作/AI 四个缺失子系统 | 接缝接口 + 空实现 |

要点：

* **最慢兵种是每日移动点的唯一来源**：空军队是 **0** 而不是「按极慢算」；水上固定 1500，与载了什么无关。
* **`_updateMovementPoints` 的 33/66 是乘分数**：1500 上基础航海术 2000（不是 1995）、高级 2500（不是
  2490）。按百分比算会系统性少 5–10 点，整张图累积成「少走一格」。
* **灯塔 +500 加在技能放大之后**：航海术不放大灯塔那 500，且只在亲水那一支生效。
* **一格九帧，帧号 = `_spriteIndex % 9`**：帧 0 是「记账点在不在前面绕不过去的对象」、帧 1 清雾、
  帧 8 才真的移动；直行时帧 8 之后 `++_spriteIndex` **跳过站立帧**，否则每格之间闪一下站姿。
* **`_direction` 与 `_spriteIndex` 是同一个不变量**：参考是 `_direction{ RIGHT }` + `_spriteIndex{ 18 }`
  （18 正是朝右那块的站姿）。第一版写成 `Bottom`，于是 `SetValidDirectionSprite()` 一律把精灵修回 36，
  `Move()` 的帧 0 判定全部错位——写测试时才发现。
* **转向块 45~53 与走动共用同一个精灵索引**：五个方向块（0/9/18/27/36，左右共用一块镜像），
  一次 90° 转向要连续 7 次（逆时针）或 6 次（顺时针）`AngleStep`；`framesToSkip = clamp(4/倍速,1,3)`
  让基线速度是「每三帧动一帧」。
* **`Move()` 允许走完已经跨出去的那一格**：移动点耗尽时若帧在 2..8 或正在转向，仍推完这一帧，
  否则精灵卡在两格之间。
* **`isNeedStayFrontObject()` 只留对象表一支**：城堡驻军与船长下船两支没有对象可查。
* **端口替换**：全局 `world` → 参数 `World`；`isControlHuman()` → `Hero.IsControlHuman`；
  动画倍速 → `Hero.AnimationSpeedMultiplier`；`Scout`/`Action`/`ActionNewPosition`/`ScoutRadar`
  → `IHeroMovementHandler`（默认空实现），调用次数与时序保留。
* **没转**：神器 `LAND_MOBILITY`/`SEA_MOBILITY`、马厩 400 点、幸运士气与军队强度、军队间调兵与
  `GetJoinSolution`、行走音效与淡入淡出。均在类注释里逐条记着。

用例：`TroopTests` 14 条（空槽回落到 Unknown 行、每兵×数量、造价、弓手标记、等级、拷贝/重置）、
`ArmyTests` 42 条（21 个方法 + 初始兵数 9 档 + 住所映射 12 行：五槽、负索引、合并三支、满员、
并列取先、计数与总血、清空、`Reset` 三态）、`HeroArmyTests` 26 条（14 个方法 + 六档基数 + Logistics 三档
+ Navigation 三档：空军队 0、33/66 精确值、水上不吃后勤、灯塔同色/异色、陆地不加灯塔、加点、扣点两态）、
`HeroMovementTests` 37 条（帧 0/1/2..7/8、停在对象前、跳帧三态、`jumpToNextTile` 两支、转向两个方向、
跳帧数、像素偏移五组、`Move` 守卫与 midStep、方向查询、精灵修复、占位迁移、深水半格）。
`FHeroes2.Game.Tests` 871 → **990**，全解决方案 1557 → **1676** 条全绿，0 警告 0 错误。
覆盖率：`army` 行 0/1/2 → **0/2/1**（`army.cpp`/`army_troop.cpp` 转部分），`heroes` 行 1/3/8 →
**1/4/7**（`heroes_move.cpp` 转部分），合计 26/50/173 → **26/52/171**。

**下一步**：`heroes/heroes.cpp` 的剩余主体（`LoadFromMP2`、`Action`、`ActionNewPosition`）现在有了
军队与移动点可以挂，或者补 `army.cpp` 的幸运/士气/强度（战斗侧要用）。

## 幸运、士气与怪物能力 ✅（2026-11-16）

`Army::GetMoraleModificator()` 要问「这一队里有亡灵吗」，而亡灵与否写在 `monster_info.cpp` 的能力表里。
那张表此前一行没转，却是四五个子系统的公共前提，于是这一批先把能力表整体落地，再在其上写幸运/士气。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Kingdom.Luck` | `kingdom/luck.cpp` | 七档尺子 `CURSED..IRISH` 与 `Normalize`（欠 `String`/`Description` 文案） |
| `Kingdom.Morale` | `kingdom/morale.cpp` | 七档尺子 `TREASON..BLOOD` 与 `Normalize`（同上） |
| `Monsters.MonsterAbilities` | `monster_info.cpp:388-590` | 32 项能力的位标记 + 108 条语句的 50 行表 + 七个小查询 |
| `Troop.GetRace`/`IsUndead`/`IsAffectedByMorale` | `monster.cpp:136`、`monster.h:237/287` | 种族（给士气按种族分组）与两条能力查询 |
| `Army.GetMoraleModificator`/`GetLuckModificator` | `army.cpp:1206/1226` | 单一种族 +1、每多一种 −1、带亡灵再 −1 |
| `Army.AllTroopsAreTheSame`/`AllTroopsAreUndead` | `army.cpp:487/413` | 两个 `Troops` 谓词，空军队的默认值**不同** |
| `Hero.GetLuck`/`GetMorale` | `heroes.cpp:1033/1069` | 技能项（`Luck`/`Leadership` 每级 1 点）+ 夹紧 |

要点：

* **两把尺子都是 `int`，不是 `enum`**：参考本身是匿名 enum，值要相加再夹紧。两个类型分开，是为了
  「幸运值不能当士气值传」——参考用两个命名空间表达的就是这个意思。
* **`Normalize` 夹到 `CURSED..IRISH` / `TREASON..BLOOD`，`UNKNOWN = -4` 不在区间内**：它只是
  「英雄还没上场」的初值，`Normalize` 永远不会返回它。
* **能力表只存「有/没有」，而且是 33 位**：参考每个能力实例还带一个百分比和一个法术 id（战斗算术 +
  法术体系），冒险地图只问有没有。`NONE` 之外 32 项，最后一项的位在 `1 << 32`，`uint` 上写
  `1u << 32` 会绕回 `1u << 0` 静默变成「双格」——所以底层是 `ulong`，并有测试钉住每位互不相同。
* **108 条语句用脚本生成，不手抄**：正则抽 `(monster, ability)` 对、按参考顺序去重输出，生成后补注释
  与文件头。测试钉住分布：50 个兵种至少一项、亡灵 11、元素 4、飞行 13、龙 4、双格 16。
* **士气修正那个反直觉分支照抄**：单一种族时不是无条件 +1，而是「**且** 五个槽不是同一种兵」才 +1；
  两个种族 0；三个及以上每多一种 −1；有亡灵再 −1；结果**不夹紧**（参考留给调用方 normalize）。
  `MapRace.None`（幽灵、元素）也是集合里的一个种族。
* **两个谓词的空军队默认值不同**：`AllTroopsAreTheSame()` → `true`（没有可比的槽），
  `AllTroopsAreUndead()` → `false`（有一个「见过有效槽」的标志）。这两条正是士气两个分支的入口。
* **`Army` 没有 commander，所以 `GetLuck()`/`GetMorale()` 退化为修正项**（参考在 `GetCommander()`
  为空时正是这样）。英雄侧只含技能项；访问对象效果、神器与「最大幸运」神器三项未转，都是加法项，
  接进来不用重算已有部分。
* **没转**：`Army::GetStrength`（要 `Troop::GetStrength` 与英雄攻防/箭术）、城堡与神器两项修正、
  `Luck`/`Morale` 的文案、能力载荷（百分比 + 法术 id）。

用例：`MonsterAbilitiesTests` 9 条（亡灵/元素/龙/飞行/双格五张名单、士气是否生效、有能力的兵种数、
三行完整掩码、位标记互不相同）、`LuckMoraleTests` 35 条（两把尺子的九个夹紧点各一组、空军队、
单一种族 +1、同兵种无加成、两/三/四种族、亡灵叠加、中立种族算一档、两个 `Troops` 谓词的默认值、
英雄无技能与三级技能各一组）。`FHeroes2.Game.Tests` 990 → **1034**，全解决方案 1676 → **1720** 条
全绿，0 警告 0 错误。覆盖率：`kingdom` 行 0/2/12 → **0/4/10**（`luck.cpp`/`morale.cpp` 进「部分」），
`monster` 行的 `monster_info.cpp` 从「未转」补成「部分」（行数不变），合计 26/52/171 → **26/55/168**。

**下一步**：`Army::GetStrength`（`Troop::GetStrengthWithBonus` + `Monster::GetMonsterStrength`，
能力表已就位，缺的是箭术与法力那两项）或者 `heroes.cpp` 的 `Action`/`ActionNewPosition`
（`IHeroMovementHandler` 正等着这两个实现）。

## 兵种强度与法术 id ✅（2026-11-16）

`getMonsterBaseStrength()` 是「一支军队值多少」的第一块砖，而它读的第一个载荷就是
`SPELL_CASTER` 的法术 id。于是这一批从 `spell.h` 的 id 枚举开始，一路把能力载荷、两条强度、
`Troop.GetStrength*` 与 `Army.GetStrength` 全部落地。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Spells.SpellType` | `spell/spell.h:38-120` | 73 个 id + `Count` 哨兵，`SpellTypes.IsValid` |
| `Monsters.MonsterAbilityInstance` | `monster_info.h:174` | 能力实例（Type / Percentage / Value），载荷不再丢弃 |
| `MonsterAbilities.GetInstances`/`TryGetInstance` | 同上 | 按参考顺序取实例；掩码改为从实例派生 |
| `MonsterStatistics.GetMonsterBaseStrength` | `monster_info.cpp:45-132` | 12 个系数 + 速度非对称 + 法术施放者按法术分组 |
| `MonsterStatistics.GetMonsterStrength` | `monster.cpp:105` | `1 + attack*0.1 + defense*0.05`，攻防可外部指定 |
| `Troop.GetStrength`/`GetStrengthWithBonus` | `army_troop.cpp:52/57` | 一只兵 × 数量；加成先加在攻防上再折 |
| `Army.GetStrength` | `army.cpp:1291` | 五槽求和 + 己方士气系数 + 幸运系数 |

要点：

* **只转 id，不转 `Spell` 对象**：等级/学派/图标/施法属于战斗引擎，而 id 是被数据文件与能力表引用的
  常量。`RANDOM`/`RANDOM1..5`/`PETRIFY` 也在编号里，所以 `Count` 是 73 而不是 67。
* **能力表从「掩码」变「实例 + 派生掩码」**：上一批只存了「有/没有」，把参考构造函数里的两个数字丢了；
  现在表存实例、掩码静态折出来，**只有一份数据**，`Get()` 语义不变、旧测试一条没改。
* **两个数字的含义随能力而变**：`MAGIC_RESISTANCE,25,0` 是概率、`SPELL_CASTER,20,Blind` 是概率+法术、
  `MORAL_DECREMENT,100,1` 是概率+士气量、`ENEMY_HALVING,10,0` 是触发概率。参考没有统一说法，照抄并逐条注明。
* **强度的每个系数都照抄**：亡灵双倍伤害只记 1.15（全兵种约七分之一是亡灵）、有「不受反击」的双击是 ×2
  否则 ×1.75、速度**非对称**（慢一档 −0.1、快一档 +0.05），末了 `sqrt(damage × effectiveHP)`。
* **缓存必须 `Lazy`**：两张数值表声明在类末尾，若把 `BaseStrength` 写成普通静态字段放前面，类型初始化会
  先算强度后建表，`BattleStats` 还是 `null` —— 实测 `TypeInitializationException` + `NullReferenceException`，
  相关测试全红。改成 `Lazy<double[]>` 后顺序问题消失且仍只算一次。
* **`Army::GetStrength` 拆成两半**：箭术（平方根放大）、英雄攻防（进 `GetStrengthWithBonus`）与魔法战略值
  都要英雄，留在 `Hero` 侧；士气与幸运系数在无英雄时也生效，留在 `Army`。`GetStrengthWithBonus` 的签名
  先按参考保留，英雄那三项接进来时不用改这里的算术。
* **期望值不来自 C# 回读**：一次性 Python 脚本按参考公式从参考的数值表/能力表重算（不引用 C#），
  结果写进 `[InlineData]`。漏因子、串行、速度方向反了都会变成数字不符，而不是两边一起错。

用例：`SpellTypeTests` 24 条（22 个 id 锚点 + 成员数 + `IsValid` 边界）、`MonsterStrengthTests` 29 条
（9 个基础强度、9 个强度、攻防因子、速度非对称、法术施放者三档、全表最强/最弱、未知与随机标记为 0、
兵力×数量、加成先加攻防、空军队、军队士气系数、亡灵不受士气、无英雄时幸运中性）、
`MonsterAbilitiesTests` 9 → **13** 条（载荷数值、每种免疫法术一条实例、掩码等于实例并集、无表兵种为空）。
`FHeroes2.Game.Tests` 1034 → **1091**，全解决方案 1720 → **1777** 条全绿，0 警告 0 错误。
覆盖率：`spell` 行 0/0/4 → **0/1/3**（`spell.h` 进「部分」），合计 26/55/168 → **26/56/167**。

**下一步**：`Hero.GetAttack`/`GetDefense`（神器袋，`GetStrengthWithBonus` 正等着它）与
`Army::GetMagicStrategicValue`（法术书 + 法力点），这样 `Army.GetStrength` 的英雄侧三项就能补齐；
或者回到 `heroes.cpp` 的 `Action`/`ActionNewPosition`（`IHeroMovementHandler` 的另外两个空位）。
## 神器袋与英雄四维 ✅（2026-11-16）

`Army::GetStrength` 的三个英雄项（箭术、攻防、魔法战略值）一直空着，因为「英雄」只是个接口。这一批把
`heroes_base.cpp` 的四维与神器袋、`artifact.cpp` 的 `BagArtifacts`、`artifact_info.cpp` 的整张数值表
一起落地，让 `Army.GetStrength` 真的能吃英雄加成。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Resources.ArtifactType` + `ArtifactTypes.IsValid` | `artifact.h:65-189` | 复用已有的 105 项 id 枚举；`isValid` 谓词按 `SpellTypes` 的写法并进来 |
| `Artifacts.ArtifactBonus`/`ArtifactCurse`/`ArtifactData` | `artifact_info.h:116-195` | 一个类型 + 一个数值，`Spell` 属性把数值读成法术 id |
| `ArtifactTable` | `artifact_info.cpp:586-982` | 105 行数值表 + `getArtifactData`；`UNKNOWN` 与哨兵共用 `ArtifactData.Empty` |
| `ArtifactBonuses` | `artifact_info.cpp:839-947` | 六个 `isBonus*`/`isCurse*` 谓词，逐条照抄 |
| `ArtifactBag` | `artifact.cpp:517-889` | 平铺 14 格、魔法书置顶右移、`Count`/`IsPresent`/`ContainSpell`、两种求和 |
| `Heroes.PrimarySkill`/`Hero.PrimarySkills` | `heroes_base.h:222-260`、`heroes_base.cpp:180-360` | 四维 base + 神器修正 + clamp，`Increase`/`Set` |
| `Army.IArmyCommander`/`Army.Commander` | `HeroBase*` 回指针 | 攻防、二级技能、魔法战略值四个方法的接缝 |
| `Army.GetStrength` | `army.cpp:1291` | 补上 `GetStrengthWithBonus` 的英雄攻防、箭术 `√(1+v/100)` 与末尾的魔法战略值 |

要点：

* **id 枚举不再复制一份**：`Resources.ArtifactType` 早就在（`MapObjectCatalog`/`TileMetadata` 都在用），
  本批最初又建了一个同名 105 项枚举，等于让两个源文件永远同步。现在整批代码只用 `ArtifactType`，
  `isValid` 按 `SpellType`/`SpellTypes` 的既有写法并入 `ArtifactType.cs`。顺带发现
  `.gitignore` 第 4 行的 `[Aa]rtifacts/`（VS 的构建输出规则）把 `src/FHeroes2.Game/Artifacts/`
  整个目录吞了 —— `git status` 看不见、`rg` 也搜不到，加了一行 `!src/FHeroes2.Game/Artifacts/`。
* **数值表由脚本从参考解析，不手抄**：105 个神器、每行最多六条 bonus/curses 手抄必错。生成器解析
  `artifact.h` 的枚举与 `artifact_info.cpp` 的 `emplace_back`，三个非字面值（`Spell::NONE` 与
  `Spell::TOWN_PORTAL` 查 `spell.h` 的枚举下标、`GameStatic::getCastleWallRangedPenalty()` = 50）
  也从参考算出来。
* **三类叠加其实是四堆**：参考的 `isBonusCumulative`/`isBonusMultiplied`/`isBonusUnique` 不构成划分，
  三个都不命中的是 `getArtifactDescription` 里那第四堆 `[ cumulative per artifact type ]` —— 士气、
  幸运、移动点、每日法力点、`ADD_SPELL` 等 15 个 bonus 与 `MORALE` 这一条 curse。`getTotalArtifactEffectValue`
  对它按「每个神器 id 只算一次」求和（两个流浪者之靴还是 +600），对 cumulative 才是每份都算。
  测试因此改成「至多一堆 + 第四堆成员清单」，而不是「恰好一堆」——后者是错的假设。
* **魔法书不是第一个空格**：`PushArtifact` 里书固定占第 0 格并把其余右移，第二本直接拒绝；`Remove`
  只清第一个副本且不挪动别人（UI 依赖位置）。14 格没有武器/盾牌/头盔的装备位，参考也没有。
* **四维的 clamp 不一样**：攻防 0..255，法力与知识 **1..255**（英雄不可能没法力）；`IncreasePrimarySkill`
  与 `SetPrimarySkill` 另按 0..255 截一次。`heroes_base.cpp` 的加成是 base + 神器修正再截，
  两个「终极之剑」累计成 +24 是因为 `ATTACK_SKILL` 是 cumulative。
* **英雄加成的方向是「加到兵种自身数值上」**：`GetStrengthWithBonus(atk, def)` 里英雄攻防与兵种攻防
  相加再折 `1 + atk*0.1 + def*0.05`，不是替换。测试初版按替换写，6.9 vs 10.8 一眼看穿。
* **`IArmyCommander` 而不是 `HeroBase`**：参考里 `Army` 存 `HeroBase*`，端口只抽 `Army` 真正调的
  四个方法，`Commander` 可空 —— 城堡守军与野怪就是空的那个，`Army.GetStrength` 因此无英雄也能算。

用例：`ArtifactTests` 28 条（10 个 id 锚点、成员数、三坑 + 编辑器占位不可用、魔法书/金弓/战斗套装/空白
卷轴的数值、三条诅咒、占位行、99 个有效神器都有行、三堆抽查、四堆划分）、`ArtifactBagTests` 11 条
（14 格、满包、书置顶、第二本、`Remove` 只清一个、`Count`/`IsPresent`、累计 vs 按 id、诅咒双堆、
空包、`ContainSpell`）、`HeroPrimarySkillsTests` 12 条（初值、下限、神器加成、两份累计、诅咒减法力、
`Increase`/`Set`、255 截断、`GetMagicStrategicValue` 为 0、`Army.Commander` 回指、军队强度吃英雄攻防、
箭术只放大射手）。`FHeroes2.Game.Tests` 1091 → **1142**，全解决方案 1777 → **1828** 条全绿，
0 警告 0 错误。覆盖率：`resource` 行 1/1/2 → **1/2/1**（`artifact_info.cpp` 进「部分」），
合计 26/56/167 → **26/57/166**。

**下一步**：城堡的 `GetAttackModificator`/`GetDefenseModificator`（`Army.GetStrength` 在城里还差这
一项）与 `Army::GetMagicStrategicValue`（法术书 + 法力点，现在恒 0）；或者回到 `heroes.cpp` 的
`Action`/`ActionNewPosition`（`IHeroMovementHandler` 的另外两个空位）。
## 法术书、法力点与魔法战略值 ✅（2026-11-16）

ADR-101 把 `Army.GetStrength` 的英雄攻防与箭术接上了，魔法战略值那一项还恒 0 —— 因为英雄没有法术书，
也没有法力点。这一批把 `spell.cpp` 的数值表与特性、`spell_storage.cpp` 的容器、`heroes_base` 的法术书/
法力点一起落地，`GetMagicStrategicValue` 于是真的能算。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Spells.SpellStats`/`SpellTable` | `spell.cpp:42-150` | 73 行数值表（耗法、移动点、最少移动点、图标、`extraValue`），由脚本从参考解析 |
| `Spells.Spell` | `spell.h:31-333`、`spell.cpp:152-750` | 值类型包装 id；等级、`IsCombat`/`IsDamage`/`IsResurrect`/`IsSummon`/`IsMassActions`/`IsMindInfluence`、`Damage`/`Resurrect`/`ExtraValue`/`SpellPoints` 与 `GetStrategicValue` |
| `Spells.SpellStorage` | `spell_storage.cpp:30-108` | 去重追加（书 + 神器上的 `ADD_SPELL`）、`Remove`、`IsPresent`、`GetSpells(level)` |
| `Heroes.Hero`（`Hero.Spells.cs`） | `heroes_base.h:118-150`、`heroes_base.cpp:172-190`、`heroes_base.cpp:358-377`、`heroes.cpp:967-970` | `MagicBook`（书本身）、`GetAllSpells`（书 + 卷轴）、`HasSpellBook`、`HasSpell`、`SpellPoints`/`SetSpellPoints`、`GetMaxSpellPoints`（10 × 知识）、`GetMagicStrategicValue` |

要点：

* **数值表照旧由脚本解析，不手抄**：73 行的五个数字里有几个是「看起来像手误」的值 —— 次元门的
  `movePoints` 225 与 `minMovePoints` 69、城镇门的 20 点、`VIEW_ALL` 只要 3 点 —— 手抄没有意义。
* **`Spell` 是值类型**：参考把 id 枚举放在类里、行为也在类里，移植保留这个分法 —— `SpellType` 是存档与
  地图用的编号，`Spell` 是法术书里存的那个值（一个 int），与 `ArtifactType`/`ArtifactBag` 的分工一致。
  构造函数照抄参考的越界归零（`id < 0 || id >= SPELL_COUNT` → `NONE`），坏文件不会索引到表外。
* **`isCombat` 是拒绝清单**：十八条冒险地图法术之外全是战斗法术，所以 `NONE` 与 `PETRIFY` 之外的
  「零耗」占位法术也会被判成战斗法术 —— 战略值那边靠 `casts == 0` 兜住，而不是靠 `isCombat`。
* **战略值是凹的，且有上限**：`casts = min(10, 现有法力 / 单次耗法)`，`casts == 1` 时修正为 1，
  否则 `casts - 0.05 × casts²` —— 所以两次施法是 1.8 而不是 2，十次是 5 而不是 10：满法力的法师不
  比半法力强一倍。召唤分支连这个修正都不乘（参考就是直接返回）。
* **参考的召唤分支是个 bug，照抄**：`spell.cpp:263` 写的是 `Monster( id ).GetMonsterStrength()`，把
  **法术** id 当**怪物** id 传了进去，于是四个召唤术量的是 44..47 号怪 —— 法师、大法师、巨人、泰坦，
  而不是四种元素。AI 的军队强度排名依赖这个数字，所以端口照抄表达式（`(MonsterType)(int)Type`）
  并在 XML 注释、`SpellTests.ASummonIsMeasuredWithTheWrongMonster` 与 ADR-102 里写清楚，
  免得后人当成手误「修」掉。
* **`spellPoints( hero )` 只转了一半**：参考用英雄参数走 `BagArtifacts::getTotalArtifactMultipliedPercent`
  给法术减耗（法力之泉那类神器），那个函数还没转，所以端口只保留 `hero == nullptr` 的分支 —— 列表价。
  注释里点明了，没有假装。
* **书是神器，没有书就没有法术**：`getAllSpells()` 在没书时返回空容器，而不是卷轴 —— 知道法术不等于
  能施法；`GetMaxSpellPoints` 读的是**有效**知识（神器加成算在内），因此永远是 10 的倍数且不小于 10。
* **卷轴在端口里不带法术**：参考的 `Artifact::getSpellId()` 在 `ADD_SPELL` 为 `NONE` 时回落到神器自己的
  `ext` 字节（卷轴的每实例载荷），而 `ArtifactBag` 是平铺的 id，没有载荷。所以一张卷轴什么也不加，
  `BATTLE_GARB` 的传送门这类**表里写死**的才进得来。测试里 `ASpellScrollCarriesNoSpellInThePort` 把这个
  缺口钉成断言，而不是让它悄悄错。

用例：`SpellTests` 37 条（表行数与 8 行锚点、占位法术全零、越界归零、9 个等级的 45 个断言、
9 个 `isCombat`、伤害/复活的 `extraValue`、四召唤七群体、四条心智法术、耗法、伤害/群体/召唤/冒险
四支战略值与 10 次上限）、
`SpellStorageTests` 9 条（空、去重、拒绝 `NONE`、合并保留顺序、`Remove` 返回值、按等级筛、`GetSpells(-1)`
是副本、战斗套装的传送门、无 `ADD_SPELL` 的神器不贡献、卷轴不贡献）、`HeroSpellsTests` 10 条
（新英雄无书无法力、书是神器、没书时书里的法术不可达、卷轴并入、`skipBag`、最大法力点随知识、
setter 不截断、没法力就没有魔法值、取最好而非求和、冒险法术不计入、军队强度增量恰好等于魔法值）。
`FHeroes2.Game.Tests` 1142 → **1198**，全解决方案 1828 → **1884** 条全绿，0 警告 0 错误。
覆盖率：`spell` 行 0/1/3 → **1/1/2**，合计 26/57/166 → **27/57/165**。

**下一步**：城堡的 `GetAttackModificator`/`GetDefenseModificator`（`Army.GetStrength` 在城里那两项修正，
以及 `castle/*` 整块的入口）；或者 `spell_book.cpp` 的筛选（书已经有数据，只差分类与排序），
再或者回到 `heroes.cpp` 的 `Action`/`ActionNewPosition`。
## 队伍的整理、统计与比较 ✅（2026-11-16）

`Army` 到 ADR-102 为止只会「往里塞、往外拿、清空、算强度」。参考里 `Troops` 还有一整层容器操作 ——
去重、排序、找最弱、算补给价值、比强弱、判断是不是肉搏军 —— AI 与英雄交换界面全靠它。这一批把这层搬过来。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Army.Troops.cs`（新 partial 文件） | `army.cpp:324-700` | `GetOptimized`、`MergeSameMonsterTroops`/`MergeSameMonsterOnce`、`SortStrongest`、`GetWeakestTroop`/`GetStrongestTroop`、`GetCountMonsters`、`GetTroopsStrength`、`GetReinforcementValue` |
| 同上 | `army.cpp:1573-1603` | `IsStrongerThan`（带 `safetyRatio`）、`IsMeleeDominantArmy` |
| 同上 | `army.cpp:1758-1782` | `GetStrongestMonster`、`ResetInvalidMonsters` |
| `Troop.IsFlying` | `Monster::isFlying()` | 肉搏判定需要的第五条能力查询 |

要点：

* **端口没有 `Troops`，只有五格**：参考的 `Troops` 是 `std::vector<Troop*>`、`Army` 在它上面加五格与英雄。
  端口只有 `Army`（五格）。所以 `MergeSameMonster*`/`SortStrongest` 原地改槽，`GetOptimized` 与
  `GetReinforcementValue` 需要动态容器时才临时建一个 `List<Troop>`，并放**副本**进去（参考放的是新
  `Troop` 对象）。
* **两个 `GetStrength`，不能合并**：`Troops::GetStrength()` 只是兵的强度之和，`Army::GetStrength()` 还要乘
  士气、幸运、英雄攻防、箭术与魔法值。参考在比较「容器」时只用前者 —— `getReinforcementValue` 的注释
  写得很明白（合并后的容器里是 `Troop` 而不是 `ArmyTroop`，所以没有英雄加成）。端口因此有
  `GetTroopsStrength()` 与 `GetStrength()` 两个方法，而不是一个带 flag 的。测试实测：同一支单种族军队
  的 `GetStrength()`（25.42）比 `GetTroopsStrength()`（24.4）大，差的正是 25/24 的士气因子。
* **补给价值不是「补给的强度」**：参考把两军合起来、去重、按强度降序、截到五格再相减。于是
  「五个强槽 + 一个弱补给」的价值是 **0**（弱兵被挤掉），而「五个弱槽 + 一条龙」的价值是
  `龙 − 被挤掉的那个最弱兵`，**小于**龙本身的强度。两个反直觉的情形各有一条测试钉住；只测「等于补给
  自身强度」的那种情形会让实现退化成加法而没人发现。
* **`SortStrongest` 用不稳定排序是照抄**：参考是 `std::sort`，端口用 `Array.Sort`，两者都不稳定，所以
  测试只断言「降序」与「多重集合不变」，不断言同强度之间的次序。另一点是空槽必须显式排到最后 ——
  空槽的强度是 0，与「一个 0 强度兵种」的槽会打平，靠比较器保证无效槽永远在后。
* **`GetStrongestMonster` 比的是「每个兵」**：参考写的是 `troop->GetMonster().GetMonsterStrength()`，
  于是一千个农民（整堆更强）也输给一只绿龙。调用方是战斗与 AI 的「这支军队派出了什么兵」，
  问的是兵种不是总量，所以照抄并在注释里写明。
* **`IsStrongerThan` 对空目标是 `true`**：参考在比较任何数值之前就 `return true`，于是空军队对空军队也
  是「更强」。这不是笔误，是「没有敌人要打」的调用路径依赖的语义，测试里显式钉住。
* **`ResetInvalidMonsters` 修的是「有名无兵」的槽**：id 合法但数量为 0 的槽 `IsValid` 为假却记着一个怪物，
  参考把它抹成 `UNKNOWN`，这样地图画槽时不必先问 `isValid()`。
* **没转的三件事写清楚了**：`JoinStrongest`/`MoveTroops`/`SwapTroops` 是三处**两军之间**搬兵，
  要 `IArmyCommander.isHeroes()`（接口还没有这个问题）；`UpgradeMonsters` 要 `Monster::GetUpgrade()`
  那张 66 行表；`ArrangeForCastleDefense`/`ArrangeForWhirlpool`/`ActionToSirens` 属于地图对象。

用例：`ArmyTroopsTests` 19 条（去重、单次去重逐次释放槽、无重复时返回假、`GetOptimized` 的首现顺序与
副本语义、空军队、降序排序与空槽在后、最弱/最强槽是活引用、按兵种计数、`GetTroopsStrength` 与
`GetStrength` 的差、同兵种补给等于自身强度、被挤掉的补给价值为 0、强补给要扣掉被挤掉的自己人、
`safetyRatio`、空目标恒胜、肉搏判定含飞行、最强兵种按单兵算、修复有名无兵的槽）。
`FHeroes2.Game.Tests` 1198 → **1217**，全解决方案 1884 → **1903** 条全绿，0 警告 0 错误。
覆盖率不变（`army.cpp` 仍是「部分」），合计 27/57/165。

**下一步**：`IArmyCommander.isHeroes()` + `JoinStrongest`/`MoveTroops`/`SwapTroops`（两军调兵，
英雄交换界面与 AI 合兵都要它），或者 `Monster::GetUpgrade()` + `UpgradeMonsters`/`UpgradeTroops`。
## 两军之间的调兵 ✅（2026-11-15）

ADR-103 之后 `Army` 会整理自己，但还不会跟另一支军队交换任何东西 —— `JoinStrongest`/`MoveTroops`/
`SwapTroops` 一直卡在「`isHeroes()` 还没人问」上。这一批把 `IArmyCommander.IsHeroes` 补上，
`Army.Transfer.cs` 落地两军调兵的五个方法。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Army.JoinStrongest(giver, keepAtLeastOneSlotForGiver)` | `army.cpp:702-832` | 四趟：同槽搬/并 → 填空槽 → 五强者交换 → 退回；末尾给英雄留兵 |
| `Army.JoinStrongestFromArmy(giver)` | `army.cpp:1426-1430` | 只多一句 `giver.Commander?.IsHeroes` |
| `Army.MoveTroops(from, monsterIdToKeep)` | `army.cpp:1432-1526` | 并自己的重复 → 换位 → 两趟搬运 → 英雄最后一堆留一个 |
| `Army.SwapTroops(Army)` / `SwapTroops(Troop, Troop)` | `army.cpp:1528-1540`、`1753-1756` | 只换五格；换两堆的 `Monster`/`Count` |
| `Army.SaveLastTroop()` | `army.cpp:1758-1761` | `Commander?.IsHeroes == true && OccupiedSlotCount == 1` |
| `IArmyCommander.IsHeroes` + `Hero.IsHeroes` | `heroes_base.cpp:91` | 参考是 `GetType() == HEROES`，端口里英雄恒真 |

要点：

* **四趟的顺序不是随便排的**：先按槽位对齐（同槽空则整堆搬、同兵种则合并），再填空槽（这一步顺带把
  跨槽的同兵种并掉），然后才进「装不下」的分支，最后把没留下的退回。前两趟能解决绝大多数合兵，
  第三趟才是真正的排序决策 —— 少了前两趟，第三趟会把本来能原样搬过去的堆也拆开重排。
* **第三趟内部也是四步**：先把**自己**的重复堆并掉（腾出空槽，参考的 `assert` 默认这一步能成）、
  让对方的强者填空、再拿自己最弱的槽跟对方最强的槽对调、对调一次重排一次，直到对方最强也不比自己
  最弱强；最后剩下的退回。测试不写死「谁留在谁那儿」，而是断言**兵员守恒**加
  「留下的最弱 ≥ 退回的最强」—— 后者才是「五个最强」这句话的可验证形式。
* **英雄留兵是二十分之一规则**：合并后接收方不为空、而给出方是英雄且被清空时，若自己最弱一堆的
  强度低于全军的 1/20，整堆还给英雄；否则按 `1/20 ÷ 该堆强度 × 数量` 切一份还回去，上限是这堆的
  一半，下限是一个兵。这三个夹取顺序（先算、再压一半、再抬到 1）是有意照抄的 ——
  它会产生一个怪癖：**只有一个兵**的军队会把那一个兵整堆还给英雄，接收方反而空了。
  `ASingleCreatureOfAHeroIsNotSplitInTwo` 把这个怪癖钉成断言（`count / 2 == 0` → 再抬到 1 的链条），
  免得后人以为「至少留一个」没实现。
* **`MoveTroops` 先并自己的重复**：五个槽里有两堆同样的兵时，那一格是白占的。参考在搬之前先
  `MergeSameMonsterTroops()`，`MovingTroopsMergesTheReceiversDuplicatesFirstToMakeRoom` 钉住这条 ——
  不并的话龙会「无处可去」，尽管军队只有四种兵。
* **`monsterIdToKeep` 是先换位再搬，不是「不许搬」**：参考先把接收方的那堆保留兵种跟给出方的一堆别的
  兵**交换**，把要留的兵种全集中到给出方，然后才搬。两趟搬运的顺序（先搬别的兵种、再搬要留的兵种）
  保证了「该留的」不会被「可能搬得动的」挤掉。`TheMonsterToKeepIsExchangedWithoutACreatureChangingSides`
  与 `TheMonsterToKeepIsMovedOnceThereIsRoom` 分别钉住「接收方满时它真的留下」和「有空位时它照样搬走」——
  后者说明「保留」是愿望不是承诺。
* **换军只换五格**：参考的 `Assign` 是 `Troops` 的方法，只拷五格，`Army` 的种族/颜色/指挥官不动。
  端口照此只拷槽位（`AssignSlots` 先 `Clean()`，再逐格拷有效堆），测试断言换完之后种族不变。
* **参考的 `assert` → `Debug.Assert`**：和端口别处一致，只在 Debug 构建里检查调用方的前置条件；
  几个 `JoinTroop` 的「这里不可能失败」也写成 `Debug.Assert(false, …)` 而不抛异常 ——
  参考在 Release 里继续跑，端口也不在半路炸掉界面。
* **实例方法与静态方法重载**：`SwapTroops(Army)` 与 `SwapTroops(Troop, Troop)` 同名，编译期按参数类型
  选；测试里 `ArmyStack.SwapTroops(left, right)` 就是静态那个。

用例：`ArmyTransferTests` 19 条（同槽合并、跨槽搬移不掉兵、七堆变五强者、英雄按份留兵、弱堆整堆退回、
驻军可被清空、单兵怪癖、自己合自己无操作、强堆换掉最弱、`MoveTroops` 全能装下/英雄最后一堆留一个/
先并重复/装满就停/保留兵种换位/有空位时照样搬/自己搬自己抛异常、换军只换五格、换两堆、`SaveLastTroop`
的四种组合）。`FHeroes2.Game.Tests` 1217 → **1236**，全解决方案 1903 → **1922** 条全绿，0 警告 0 错误。
覆盖率不变（`army.cpp` 仍是「部分」），合计 27/57/165。

**下一步**：`Monster::GetUpgrade()` + `UpgradeMonsters`/`UpgradeTroops`（升级兵种，AI 与城堡都要），
或者 `SplitTroopIntoFreeSlots` 一族（拖单堆的界面），再或者城堡的
`GetAttackModificator`/`GetDefenseModificator`。
## 升级线 ✅（2026-11-15）

ADR-104 之后 `Army` 会合兵、搬兵、换兵，但「把兵种升级」还缺一张表 —— 参考的 `Monster::GetUpgrade()`
（`monster.cpp:285-336`）。这一批把升级关系、降价、以及建立在它上面的 `Troop::Upgrade` 与
`Troops::UpgradeMonsters` 落地。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MonsterUpgrades.GetUpgrade` | `monster.cpp:285-336` | 21 条映射，其余自映射 |
| `MonsterUpgrades.GetDowngrade` | `monster.cpp:232-283` | 反向 21 条，顶部自映射 |
| `MonsterUpgrades.IsAllowUpgrade` | `monster.h:257` | 写成 `GetUpgrade(m) != m` |
| `MonsterUpgrades.GetUpgradeCost` | `monster.cpp:830-838` | `(升级后价 - 原价) * 2`，无升级返回空 `Funds` |
| `Troop.IsAllowUpgrade`/`GetUpgrade`/`Upgrade` | `army_troop.h`（继承 `Monster`） | 只改 id，数量不变 |
| `Troop.GetUpgradeCost`/`GetTotalUpgradeCost` | `army_troop.cpp:98` | 单个与整堆 |
| `Army.UpgradeMonsters(monster)` | `army.cpp:324-332` | 按兵种原地升级，别的堆不碰 |

要点：

* **表只有 21 行，不是 66 行**：升级的是每个种族除一级兵与两个特殊兵之外的那几个 —— 骑士 5（弓箭手、
  枪兵、剑士、骑兵、圣骑士），野蛮人 3（半兽人、食人魔、巨魔），女巫 3（矮人、精灵、德鲁伊），
  术士 3（牛头人、绿龙、红龙），巫师 3（铁人、法师、巨人），亡灵 4（僵尸、木乃伊、吸血鬼、尸巫）。
  亡灵的骨龙没有升级，所以是 4 而不是 3。测试按种族计数钉住这 5/3/3/3/3/4。
* **龙是唯一的两级链**：绿 → 红 → 黑。因此 `GetUpgrade` 在链顶不是自逆的，`GetDowngrade` 必须是一张
  独立的表而不是「在升级表里反查」——反查会得到两个候选（绿龙既可以是红龙的降级，也不出现在别人的
  升级里）。
* **`IsAllowUpgrade` 不另立表**：参考写的是 `id != GetUpgrade().id`，端口照写。若另写一张「可升级名单」，
  两张表迟早会不一致，而不一致的表现是「对话框上的升级按钮点了没反应」—— 最难查的那类。
* **升级只改 id**：`Monster::Upgrade()` 是 `id = GetUpgrade().id`，所以数量天然保留，堆里的伤兵语义也保留
  （`Troop` 没有逐兵血量，只有数量）。连升两次就沿链条走一步：绿龙 → 红龙 → 黑龙，第三次是空操作。
* **费用是差价的两倍**：`(upgr.GetCost() - GetCost()) * 2`。测试用两个有代表性的价格钉住单位换算 ——
  弓箭手 150 → 游侠 200 是 100 金；红龙 3500 金 1 硫 → 黑龙 4000 金 2 硫是 1000 金 2 硫（注意资源也参与
  差价，不只是金币）。没有升级时参考返回空 `Funds`，端口照做，于是调用方可以直接累加而不必先问。
* **`GetUpgradeCost` 在参考里是 `protected`**：公开的门是 `GetTotalUpgradeCost`。端口把两个都做成公开，
  并在注释里保留了参考的叮嘱（先问 `IsAllowUpgrade`，否则拿到的是误导性的「免费」）。
* **`UpgradeTroops(Castle)` 没转**：它比 `UpgradeMonsters` 多问两件事 —— 城堡种族是否等于兵的种族、
  城堡是否建了升级后的住所 —— 两件都要 `Castle`，而端口还没有城堡。这条写进了 `Army.Troops.cs`
  的文件注释，免得后人以为漏了。
* **测试里两条是整表扫描**：21 行全部检查「种族不变、等级不变、更强、更贵」，再加一条「降级回到原兵种」。
  手写「弓箭手变游侠」的测试抓不到第 17 行写错一个字，扫一遍能。

用例：`MonsterUpgradeTests` 10 条（龙链两级与链顶自映射、21 条与按种族计数、整表保持种族与等级且更强、
降级回原、无升级者自映射且免费（黑龙的降级不是自己）、可升级者一定有费用、两条具体价格、
升级保留数量且可连升、整堆费用随数量、按兵种升级只动那一种）。
`FHeroes2.Game.Tests` 1236 → **1246**，全解决方案 1922 → **1932** 条全绿，0 警告 0 错误。
覆盖率不变（`monster`/`army` 仍是「部分」），合计 27/57/165。

**下一步**：`Castle` 的最小骨架（种族、建筑位、`isBuild`），它是 `UpgradeTroops`、招募与建造的共同前置；
或者 `SplitTroopIntoFreeSlots` 一族（拖单堆的界面）；再或者回到 `Kingdom`（资源与英雄名册）。
## 城堡建筑表 ✅（2026-11-15）

`castle`（11 个 cpp）一直是「全部未转」，而它挡着 `UpgradeTroops`、招募、建造与地图上的城堡对象。这批
从最底层起：建筑本身的元数据 —— 位掩码、造价、前置、升级线、是不是住所。没有任何对象状态，因此没有
`Castle` 类也能落地，并且能整表校验。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.Castle.BuildingType` | `castle.h:68-110` | 33 项位掩码 + 3 个组合掩码 |
| `CastleBuildings.GetCost(building, race)` | `buildinginfo.cpp:203-212` | 86 行造价表 |
| `CastleBuildings.GetRequirement(race, building)` | `castle_building_info.cpp:1097-1350` | 前置掩码（兵营 × 种族） |
| `CastleBuildings.GetUpgradeForBuilding(race, building)` | `castle_building_info.cpp:980-1095` | 五条全种族 + 六族住所链 |
| `CastleBuildings.IsDwelling(building)` | `buildinginfo.cpp:253-273` | 12 个单一位 |
| `DwellingMonsters.FromDwelling(race, BuildingType)` | `Monster::FromDwelling` | 参数从 `uint` 换成建筑位 |

要点：

* **位掩码是 `uint`，不是 `int`**：`DWELLING_UPGRADE7` 是 `0x80000000`，参考的枚举里它已经出了
  `int` 的正数范围（C++ 靠实现定义才没炸）。端口的 `BuildingType : uint` 因此不是风格选择，是必须。
* **两个名字故意不长**：`Well2`（参考 `BUILD_WEL2`，其实是各族的「地标」建筑：农场、垃圾堆、水晶花园、
  瀑布、果园、骷髅堆）与 `Spec`（`BUILD_SPEC`：防御工事、竞技场、彩虹、地牢、图书馆、暴风雪）。改漂亮
  名字的唯一后果是搜不到参考。
* **住所位从 `Monsters.Dwelling` 搬进来**：ADR-095 那会儿城堡模块还不存在，住所位以 `uint` 常量的形式
  寄在怪物那一侧，注释里写着「城堡落地时搬过去」。这批兑现了：`Dwelling` 类删除，`FromDwelling` 收
  `BuildingType`，`Army.Reset` 与测试直接写 `BuildingType.DwellingMonster1`。
* **造价表逐行照抄，包括六行同价的 `Well2`**：本来可以合并成一行「全种族 1000 金」，但那样这张表就跟参考
  没法并排对照了，而 86 行的转录唯一的质量保证就是并排对照。`Spec` 六行本来就不同（1500+15 矿到
  1500+各 5 资源），正好说明这个位置确实按种族分化。
* **前置表是二维的，因为「需要什么」按种族裁剪**：骑士的弓箭手只要农民房，女巫的要农民房 + 酒馆；术士的
  牛头人只要石像鬼塔，亡灵的吸血鬼要墓穴 + 盗贼公会。参考用嵌套 switch 表达，端口写成 `(building, race)`
  的元组 switch —— 同一个形状，顺序仍是「先建筑、后种族」。
* **只有两处前置指向非兵营建筑**：`Spec` 只有法师城要 `MageGuild1`；`DwellingUpgrade7`（黑龙穴）要
  `DwellingUpgrade6`（红龙穴）—— 全游戏唯一「升级的前置是另一个升级」的地方。
* **`MapRace.Random` 在两张表里都有一份**：前置表对它答「无」（还没分到种族），升级表却给它一套完整的
  升级线（含第二段龙）。这是参考的原样，测试把这两条都钉住了 —— 否则「随机城在建城前什么都不能建」
  会被当成 bug 修掉。
* **两条整表不变量撑起 86 行 + 60 行转录**：`EveryDwellingATownCanReachIsPricedAndNothingElseIs`
  （沿升级链求可达集合，可达 ⟺ 有价，且价不为零 —— 多一行/少一行都会炸）；
  `EveryPrerequisiteIsItselfBuildableInThatTown`（前置不能要求本城造不出来的东西）。
* **城堡的升级链和怪物升级表是同一件事**：`TheCastleUpgradesAndTheMonsterUpgradesAgree` 遍历六族的
  可达住所，凡「升级后不是自己」的，要求 `MonsterUpgrades.GetUpgrade(住所里的兵) == 升级后住所里的兵`，
  并数出 21 条 —— 与 ADR-105 的 21 行升级表严丝合缝。两张表分开写、互相验证，这是它们最有价值的用法。

用例：`CastleBuildingsTests` 14 条（城堡 5000+20 木+20 矿、六族 `Spec` 造价、亡灵神龛、16 个通用建筑
同价、住所可达即有钱、升级链终止、法师公会逐级与帐篷变城堡、黑龙穴、弓箭手的前置（女巫多一个酒馆）、
法师城图书馆、通用建筑无前置、前置自身可建、两张升级表对齐、恰好 12 个住所位）。
`FHeroes2.Game.Tests` 1246 → **1260**，全解决方案 1932 → **1946** 条全绿，0 警告 0 错误。
`castle` 行 0/0/11 → **0/3/8**，合计 27/57/165 → **27/60/162**。

**下一步**：`Castle` 本体（种族、`building` 位、`isBuild`/`isFort`、`CheckBuyBuilding`、招募与周产），
它一来 `UpgradeTroops` 就能落地；或者 `Kingdom` 的资源与英雄名册；再或者城堡的两条强度修正
（`GetAttackModificator`/`GetDefenseModificator`）。

## 城堡骨架 ✅（2026-11-15）

`castle.cpp` 有 2500 行，其中一半要世界、王国、英雄与渲染。这批只切出**不需要这些**的那一半 —— 恰好是
「建造」这条线从头到尾能自洽的部分：位掩码、每天一次的许可、买与不能买的理由、住所的货架。有了它，
`UpgradeTroops`、地图装载的城堡表、招募（下一批）都有了落点，且整条链路都能在没有地图的情况下测。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Game.Castle.BuildingStatus` | `castle.h:111` | 10 项：`AllowBuild`…`UnknownCondition` |
| `Castle(int tileIndex, MapRace race, MapVersion version)` | `castle.cpp:428` `_postLoad()` 的无图部分 | 空住所、空驻军、当天可建 |
| `IsBuild` / `IsCastle` / `IsBuildingDisabled` | `castle.h:321` / `:178` / `:377` | 位判断；`IsCastle` 看 `Castle` 位 |
| `IsLibraryBuilt` / `IsFortificationBuilt` / `IsNecromancyShrineBuild` | `castle.h:228/238/191` | 种族 + `Spec`/`Shrine` 位 |
| `GetLevelMageGuild` | `castle.cpp:955` | 公会 1..5 级 |
| `CountBuildings` | `castle.cpp:564-581` | 22 位上限；亡灵按版本在酒馆/神龛之间选 |
| `GetBuildingValue` | `castle.cpp:613-662` | 城堡 +5、第六住所 +6、黑龙巢 +2、五个升级 +1、公会 +1..+4 |
| `GetActualDwelling` / `GetUpgradeBuilding` | `castle.cpp:1931` / `:1960` | 升到链顶的住所 / 一步升级 |
| `GetMonstersInDwelling` | `castle.cpp:1108` | 三名字共用第六格 |
| `IsExactBuildingBuilt` | `castle.cpp:731` | 升级的前置看「确切那一级」 |
| `CheckBuyBuilding` / `AllowBuyBuilding` | `castle.cpp:1137-1234` / `castle.h:207` | 九道判定，顺序即语义 |
| `GetAllBuildingStatus` | `castle.cpp:1236-1258` | 三次扫描：可建 → 缺钱 → 缺前置 |
| `BuyBuilding` | `castle.cpp:1260-1324` | 扣款（`-=` 后 `Trim`）、建城堡清帐篷、住所满货、花掉许可 |
| `ActionNewDay` | `castle.cpp:833-836` | 还回许可 |
| `AddBuildings` | `castle.cpp:407-410` | 装载/编辑器直接置位 |
| `SetDefaultBuildings` | `castle.cpp:537-562` | 第一住所必有、第二住所按难度掷点 |
| `ChangeColor` | `castle.cpp:949` | 连驻军一起换色 |

要点：

* **`CheckBuyBuilding` 的判定顺序就是它的语义**：已建 → 禁用位 → 三个特例（船坞要水、神龛只给亡灵且
  只在 `Price of Loyalty`、亡灵没有酒馆）→ 法师公会逐级 → **今天还能不能建** → 帐篷/城堡规则 → 种族特有
  升级 → 前置位 → 资源。参考靠书写顺序表达优先级，端口逐条照排；测试也是逐条钉住「谁先回答」。
  最容易读错的两处：公会逐级在「今天不能建」**之前**（所以没有一级公会时二级答 `BuildDisabled` 而不是
  `NotToday`），而种族特有升级在它**之后**。
* **`AlreadyBuilt` 判的是「任一位命中」**：参考写的是 `_constructedBuildings & build`，所以问一个组合掩码
  （如 `DwellingMonsters`）只要建了其中一个就算已建。`IsBuild` 却要求**全部**位命中 —— 两个函数一个 `&`、
  一个 `& == build`，不能互相替换。`IsExactBuildingBuilt` 因此单独存在：升级的前置必须是「确切那一级」。
* **三个全局变成参数或属性**：`Kingdom::AllowPayment()`/`OddFundsResource()` → `in Funds treasury` 参数
  （王国落地后传 `kingdom.GetFunds()`，调用点不用改）；`Settings` 的地图版本 → 构造参数 `Version`；
  `HasSeaAccess()`（扫周围瓦片找水）→ 可写属性，由装载或测试回答。
* **许可花在 `BuyBuilding` 的末尾**：不论建的是什么，许可都花掉；建城堡顺手把帐篷清掉 —— 帐篷只是「还没
  城墙」时的占位，参考用 `&= ~BUILD_TENT`。
* **住所是「货架」，不是「建筑」**：`_getDwelling()` 把原住所与它的升级映射到同一格（第六格回答三个名字：
  `DwellingMonster6`/`Upgrade6`/`Upgrade7`）。所以升完级货不动，`GetMonstersInDwelling` 对链上任意一级
  都答同一个数。`BuyBuilding` 的填货只发生在**原住所**上：升级不重填。
* **周产来自 `MonsterStatistics.GetGeneralStats(...).BaseGrowth`**：不另抄一张成长表，与 ADR-095 的住所
  映射接上即可。
* **掷点走可注入的 `Pcg32`**：`SetDefaultBuildings` 的第二个住所按难度掷（简单 75 / 普通 50 / 困难 25 /
  专家 10 / 不可能 0），默认走 `Rand` 门面，测试传固定种子 —— 与 `Army.Reset` 同一道缝。测试掷 200 次
  验证「难度越低、有第二住所的比例越高」，而不是钉死某一次的布尔值。
* **命名空间撞名用别名**：`FHeroes2.Game.Castle` 命名空间里有 `Castle` 类型，`FHeroes2.Game.Army` 里也有
  `Army`。源文件里 `using ArmyTroops = FHeroes2.Game.Army.Army;`，测试里 `using CastleTown = ...Castle;`。
  改名会让所有调用点与参考脱节，别名只在文件头出现一次。

用例：`CastleTests` 17 条（新城什么都没有且当天可建；第二住所按难度掷点；建城堡前只有城堡可卖；城墙一到
帐篷就没了；一天只建一次；公会逐级；船坞要水；亡灵用神龛换酒馆；只有术士有黑龙穴；住所落成即满货；
升级与原住所同一格；黑龙穴在龙链末端；`CountBuildings` 不数升级与帐篷；`GetBuildingValue` 的三项加权；
`IsExactBuildingBuilt` 要求下面每一级；六族 × 两版本整表扫「能建的都能建、不能建的都有理由」；
`GetAllBuildingStatus` 的优先级）。
`FHeroes2.Game.Tests` 1260 → **1277**，全解决方案 1946 → **1963** 条全绿，0 警告 0 错误。
`castle` 行 0/3/8 → **0/4/7**，合计 27/60/162 → **27/61/161**。

**下一步**：招募（`RecruitMonster`/`getRecruitLimit`/`getAvailableArmy`）—— 有了货架与 `Troop`，只差钱；
或者 `UpgradeTroops`（现在两个条件都齐了）；或者地图装载的城堡表（`Castle::LoadFromMP2` 的 70 字节块
+ `PostLoad` 落格）。

## 城堡招募与升级 ✅（2026-11-16）

上一批把「货架上有多少兵」做出来了，这批把兵卖掉：`RecruitMonster` 从货架取、收钱、塞进守军或来访英雄的
队伍；`UpgradeTroops` 反过来，用钱把已经在队伍里的兵换成本族的升级版 —— 而升不升级由**这座城建没建对应的
升级住所**决定。两条线都只靠上一批的 `Castle` 与新加的住所反查表，仍然不需要世界。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `DwellingMonsters.GetDwelling(MonsterType)` | `Monster::GetDwelling()`（`monster.cpp:691-780`） | 兵种 → 住所位的反查表（75 条 + 中立） |
| `Troop.GetDwelling()` | 同上 | 堆知道自己从哪一格来 |
| `Castle.GetRecruitLimit(MonsterType, in Funds)` | `castle.cpp:1093-1106` | 货架与钱取小；异族为 0 |
| `Castle.RecruitMonster(Troop, ref Funds, Army? heroArmy)` | `castle.cpp:1025-1091` | 钳货架 → 算全价 → 查钱 → 入守军/英雄 → 扣钱 → 减货 |
| `Army.UpgradeTroops(Castle, ref Funds)` | `army.cpp:528-560` | 逐堆：能否升 → 同族 → 升级后的住所已建 → 逐堆付费后升级 |

要点：

* **只钳货架，不钳价、不钳入队**：参考里 `count` 被钳到货架存量，但 `paymentCosts` 和 `JoinTroop(troop)`
  用的都是**调用方给的整数**。所以「要 20 个但只剩 10 个」会付 20 个的钱、进 20 个兵、货架归零 —— 差出来
  的 10 个白送。参考的招募对话框永不超过 `getRecruitLimit()`，所以这不对称平时看不见。端口照抄并在测试
  `AskingForMoreThanTheShelfHoldsSellsWhatTheShelfHasAndGivesTheRestAway` 里钉死，免得后人「顺手修好」把
  以后移植的对话框行为一起改掉。
* **`getLowestQuotient` 的负数进 `uint`**：参考写 `uint32_t willRecruit = budget.getLowestQuotient(...)`，
  而它返回 `int32_t`。钱是负数（事件可以扣成负）时会绕成巨大值，于是答案变成「货架上有多少就能买多少」。
  端口用 `unchecked((uint)...)` 显式保留这个转换，而不是加一道参考没有的钳位。
* **守军优先，英雄其次，都不行就整笔不成交**：参考先 `GetArmy().JoinTroop()`，失败再试来访英雄的军队，
  两处都失败直接 `return false`（弹「没有空位」）。失败发生在扣钱之前，所以「没地方放」不花钱、不减货。
* **`JoinTroop` 决定放在哪一格**：`Army.JoinTroop(Troop)` 会先找同名堆合并、再找空格，所以招募不会因为
  守军里已有同种兵而被判「满」。
* **反查表是第二张表，不是第一张表搜一遍**：`FromDwelling` 按种族回答，`GetDwelling` 按兵种回答 —— 兵种的
  住所只取决于它自己（弓手永远在第二格），而同一个「第三格」在术士城里是石像鬼、在骑士城里是精灵。两条路
  不能互相代替，参考也确实是两张表。测试用「六族 × 12 个住所位，反查回来必须落在同一货架」把两张表对上。
* **升级问的是「升级后的住所」，不是现在的**：`castle.isBuild(troop.GetUpgrade().GetDwelling())`。这正是
  升级建筑的意义 —— 没有升级住所，有钱也不给升；有住所但钱不够，就跳过这一堆继续看下一堆（参考逐堆检查
  `AllowPayment`），所以钱只够一堆时，前面的升、后面的不动。
* **种族不匹配直接跳过**：亡灵城里的骑士兵不能升级，哪怕它建了 `DwellingUpgrade4`（亡灵本来也没有）。

用例：`CastleRecruitTests` 14 条（货架与钱轮流当上限；异族不出售；升级住所卖的是升级后的兵；招募扣钱减货；
守军优先；守军满时进英雄队伍；两边都满则不成交且不扣钱不减货；没钱不成交；超量招募的不对称；升级必须有
升级住所；异族城不升级；逐堆付费到没钱为止；六族 × 12 住所位的反查不变量；`Troop.GetDwelling` 的黑龙/红龙/
骨龙与中立兵）。
`FHeroes2.Game.Tests` 1277 → **1291**，全解决方案 1963 → **1977** 条全绿，0 警告 0 错误。
`castle`/`army`/`monster` 三行都已是「部分」，行数与合计不变（27/61/161）。

**下一步**：地图装载的城堡表（`Castle::LoadFromMP2` 的 70 字节块 + `PostLoad` 落格 + `_postLoad()` 的填货
尾巴）—— 它会顺手把 `Game::Save` 的城堡段也解掉；或者 `Kingdom` 的资源簿与英雄名册（`RecruitMonster` 现在
的 `ref Funds` 那时就能接上）；或者城堡的周产（`ActionNewWeek`）与税收，把 M6 的「城镇全流程」补完。

## 城堡的 70 字节块 ✅（2026-11-16）

上一批能卖兵，这一批能**从文件里造出一座城**。MP2 的城堡块固定 70 字节，字段没有长度前缀，全靠顺序和固定
宽度；参考把「读字段」与「读完之后修一遍」分成 `LoadFromMP2()` 与 `_postLoad()` 两段，端口照分 —— 因为那
六步修复回答的是「文件说不清、引擎必须猜」的问题，混进解析里会很难读。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Castle.Mp2BlockSize` | `MP2::MP2_CASTLE_STRUCTURE_SIZE`（`mp2.h:36`） | 70 |
| `Castle.LoadFromMP2(ReadOnlySpan<byte>, GameDifficulty, MapRace?, Pcg32?, uint)` | `castle.cpp:102-395` | 所有者 → 建筑位/默认建筑 → 守军 → 船长/名字 → 种族 → 城堡或帐篷 → 禁用位 |
| `Castle.PostLoad(uint, Pcg32?)` | `castle.cpp:428-535` `_postLoad()` | 六步修复 |
| `Castle.JoinRandomArmy(uint, Pcg32?)` | `castle.cpp:2218-2247` `_joinRNDArmy()` | 中立城的守军强化 |
| `Army.Assign(IReadOnlyList<Troop>)` | `army.cpp:493-501` `Troops::Assign()` | 按位放堆，不合并、不找空位 |
| `Castle.HasCustomArmy` | `Modes( CUSTOM_ARMY )` | 地图给了守军就别再替他长兵 |
| `Castle.Race`（`private set`） | `_race` | 随机城的种族要等块读到最后才知道 |

块布局（偏移都是 0 起）：`0` 所有者、`1` 是否自定义建筑、`2-3` 公共建筑、`4-5` 住所、`6` 公会等级、`7` 是否
自定义守军、`8-12` 五个兵种 id、`13-22` 五个 LE16 数量、`23` 船长、`24` 是否自定义城名、`25-37` 城名 13 字节、
`38` 种族、`39` 城堡还是帐篷、`40` 是否禁止建城堡、`41-69` 参考从不读的 29 字节。

要点：

* **住所的升级位自带基础位**：文件里只有「第 N 个住所」和「第 N 个住所升过级」两种位，所以升级位要同时置上
  基础住所位。这是格式的规矩，不是修复 —— 升级住所本就意味着基础住所存在。
* **公会的 1 字节是等级，不是位**：`3` 表示一、二、三级都建了。端口按 `> 0 / > 1 / …` 展开成五个位，与
  `GetLevelMageGuild()` 互为逆运算（测试里两头都对）。
* **兵种 id 存的是「枚举值 - 1」**：格式把 0 留给「没有」，所以读出来要 `+ 1`，与参考一致。测试第一版写错了
  这个偏移，被 `Swordsman` 收成 `VeteranPikeman` 抓到。
* **自定义守军是五个独立槽**：不能走 `JoinTroop`，那会合并同种兵并挑空位。`Army.Assign` 照参考按位放，重复的
  兵种就是两格；`PostLoad` 看见 `HasCustomArmy` 就不再给中立城补兵。
* **`LoadFromMP2` 里最绕的一个分支是种族**：块里写了 0…5 就用它；否则若「城有主」则用**所属王国的种族**，
  再否则随机从六族里挑。参考那头要 `Players::GetPlayerRace()`，端口把它变成 `MapRace? ownerRace` 参数。
  「有主」这个条件是关键 —— 无主的随机城就算传了 `ownerRace` 也不用。
* **`PostLoad` 的六步顺序不能乱**：先按种族剔掉没有的升级（随机城可能拿到别的种族的升级位），再按「真正的
  住所」填货（有升级就填升级后的兵），然后给守军上色，中立城补四次兵，无水的船坞拆掉、亡灵的酒馆换成神龛，
  最后还回建造许可。填货在剔升级之后，否则刚剔掉的升级位还会被用来决定货架上是哪种兵。
* **船坞规则要调用方先说水**：`HasSeaAccess` 是属性，默认 `false`。所以 `LoadFromMP2` 的文档里写明「先设它，
  再来装载」，否则块里写着船坞也会被立刻拆掉。这与 ADR-107 定的「世界给答案」一致。
* **两步没转，代码里点名**：船长的默认值与法术点（要 `HeroBase` 的船长）、`_trainGuestHeroAndCaptainInMageGuild`
  （要法师公会的法术库）。写在这里而不是悄悄跳过，是因为「有船长建筑却没有船长」这种半截状态最难查。

用例：`CastleLoadTests` 19 条（所有者/种族/城堡或帐篷；公共建筑的八个位；升级位带基础位；公会等级；难度掷点
默认建筑；每格填一周产量；升级住所填升级后的兵；种族剔掉没有的升级；自定义守军按位放且不合并；中立城自动补
兵且只用本族兵；自定义守军不补兵；船坞要水；亡灵酒馆换神龛（两个版本）；自定义城名；船长位；随机城用所有者
的种族；无主随机城落在六族之一；块长不对抛异常）。
`FHeroes2.Game.Tests` 1291 → **1310**，全解决方案 1977 → **1996** 条全绿，0 警告 0 错误。
行数与合计不变（27/61/161）。

**下一步**：把城接到地图上（`world_loadmap.cpp` 的 `MP2::OBJ_CASTLE` 分支：位置记录 3 字节 + 70 字节块 →
`World.Castles`，并回答 `HasSeaAccess`），顺带 `Game::Save` 的城堡段；或者 `Kingdom`（资源簿 + 城/英雄名册，
让 `ref Funds` 接到真正的账本）；或者周产（`ActionNewWeek`，需要先有周类型 `Week`）。

## 把城接到地图装载上 ✅（2026-11-16）

上一批的 70 字节块没有「自己的城」可填，这一批补上：地图文件的 72 条 3 字节位置记录变成城表，两种城堡块按
瓦片找到各自的城，随机城被重绘成它掷出来的样子，最后地图没起名的城从 72 个默认名里抽一个。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Castle.AllCastles`（新） | `all_castles.h` / `castle.h` 的 `AllCastles` | 按地图宽度建表、17 格足迹登记、`Add`/`Get(x, y)`/`Remove`/索引器 |
| `World.Castles`（新） | `world.h:311` `vec_castles` | 城表，装地图与 `Reset` 时替换 |
| `World.GetCastle` / `GetCastleEntrance` / `IsValidCastleEntrance` | `world.cpp:390/399/405-408` | 宽查询答足迹、窄查询只认城门 |
| `World.DoesCastleHaveSeaAccess(int)` | `castle.cpp:1836-1875` `Castle::HasSeaAccess()` | 探城门下、左下、右下的三格水 |
| `World.CreateMapCastles(Mp2Map)` | `world_loadmap.cpp:296-355` | 位置表 → 城对象 + 捕获表 |
| `World.ReadCastleBlock(...)` | `world_loadmap.cpp:596-641` | 两种块（城堡 / 随机城）共用 70 字节载荷 |
| `World.ReplaceRandomCastleObjectId(int)` | `maps.cpp:717-732` | 随机城的类型改成城堡 |
| `World.UpdateCastleSprite(int, MapRace, bool, bool)` | `maps.cpp:734-819` | 42 个精灵重绘（16 城 + 16 阴影 + 顶格重指） |
| `World.UpdateCastleNames()` | `world_loadmap.cpp:92-111` | 未起名的城分配默认名 |
| `Castle.SetName(IReadOnlySet<string>)` | `castle.cpp:2169-2183` | 洗牌取第一个没被占的名字 |
| `Castle.DefaultNames` | `castle.cpp:84-101` | 72 个英文城名 |

要点：

* **装载顺序是三条硬约束**：`CreateMapCastles` → `ReadInfoBlocks` → `UpdateCastleNames`。块按「它站在哪个
  瓦片上」找城，所以城必须先于块存在；默认名要避开地图自己起的名字，所以要等所有块读完再分配。参考也是
  这个顺序，端口把它写成 `LoadMap` 里的注释。
* **城表按地图宽度构造、装载时替换**：表记着宽度才能把坐标换算成瓦片索引，`Reset` 之后也换一个新表而不是
  清空旧的，否则下一次装载前的查询会用错的宽度。
* **`HasSeaAccess` 由世界先说**：参考是城堡方法、靠全局 `world` 探水；端口的城堡不持有世界（ADR-107），所以
  扫描落成 `World.DoesCastleHaveSeaAccess`，`CreateMapCastles` 建城时就把答案写进城堡。这正好接上 ADR-109
  要求的「先设水，再装载」——块里写着船坞也不会被 `PostLoad` 拆掉。
* **随机城要重绘两遍，顺序不能反**：先 `UpdateCastleSprite`（`OBJNTWRD` → `OBJNTOWN`/`OBJNTWSH`，顶格再手工
  重指 `fullTown - 16`），再 `ReplaceRandomCastleObjectId`（改类型）。反过来的话，重绘里那句「这格是不是
  随机城」的检查会先看到已被改成城堡的类型而直接返回。
* **`Tile.ReplaceObject` 要求 UID + ICN + 图像索引三者精确匹配**：它不会「按 UID 找部件」，所以重绘只在整座城
  的 32 个精灵都真的画在地图上时才生效。测试地图因此必须铺满 32 格，城门格用布局里的第 13 号索引（不是 0）
  —— 第一版测试只画了城门一格、索引又是 0，重绘静默无效，断言才暴露出「城门还是 `OBJNTWRD`」。
* **只有城门的 5×6 框内才该自称随机城**：`ReplaceRandomCastleObjectId` 的扫描框是 `x ∈ [-2, 3)`、
  `y ∈ [-3, 2)`，左边的阴影列在框外，参考也不管；测试把框外格写成 `OBJ_NONE`，框内写成随机城，正好验证
  这段扫描。
* **名字先收集再分配**：`UpdateCastleNames` 先收齐地图给的名字（已占用），再逐个给无名城抽名并把抽到的加进
  集合。参考用全局城表做同样的事；72 个名字全被占满时参考 `assert(0)`，端口抛 `InvalidOperationException`。
* **畸形地图 warn 后跳过**：城的位置越界、块长不是 70、块找不到城、随机格不是随机城。参考前两种不管、后两种
  `assert`；端口统一告警跳过，理由是这几种都不该让整张图装载失败。

用例：`WorldCastleLoadTests` 12 条（城表条目建城并就位捕获表；足迹答 17 格且非城门拒绝窄查询；70 字节块填
名字/颜色/种族/城堡位/住所/守军着色；随机城的三组断言 —— 城门精灵变城镇第 13 号、足迹格变第 3 号、左侧阴影
变 `OBJNTWSH`，且全图不再有随机城类型；滨水城保留船坞；无水丢船坞；只有侧面有水也算；无名城拿到两个不同的
默认名；地图名保留且不再分配；越界城跳过；坏块保留建城初值；`Remove` 后其余城仍可查）。
`FHeroes2.Game.Tests` 1310 → **1322**，全解决方案 1996 → **2008** 条全绿，0 警告 0 错误。
行数与合计不变（27/61/161）。

**下一步**：`Game::Save` 的城堡段（`Castle::saveToMP2` 与存档的城列表）；或者 `Kingdom`（资源簿 + 城/英雄
名册，随机城有主时的种族要问它，`ref Funds` 也能接到真正的账本）；或者周产（`ActionNewWeek`，需要先有周类型
`Week`）。

## 王国：账本、名册与七槽容器 ✅（2026-11-16）

城堡的 `ref Funds` 下单接口一直改的是调用方递进来的临时账本；`IsLoss` 也没人算。这一批补上 `Kingdom`
的「账本 + 名册」这半边与 `Kingdoms` 七槽容器：地图装载时按文件头的颜色与难度给每个在局颜色开一个王国，
瓦片全部读完后再把英雄与城塞进各自的名册。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Kingdom.Kingdom`（新） | `kingdom.h:71` / `kingdom.cpp:103-471` | `Color`、`Treasury`（`ref Funds`）、两张名册 |
| `Kingdom.Init(PlayerColor, GameDifficulty, bool)` | `kingdom.cpp:103` | 先 `Clear`，非法颜色 warn 后留空 |
| `Kingdom.Clear()` | `kingdom.cpp:124` | 账本、颜色、倒计时、两张名册一起清 |
| `Kingdom.GetStartingResources(...)` | `kingdom.cpp:865` | 玩家/电脑两张表 × 五档难度 |
| `Kingdom.AllowPayment(in Funds)` | `kingdom.cpp:384` | 「够，或者本来就要 0」 |
| `Kingdom.AddFundsResource` / `OddFundsResource` | `kingdom.cpp:443/449` | 收付后 `Trim`，不会写成负数 |
| `Kingdom.AddCastle` / `RemoveCastle` | `kingdom.cpp:311/322` | 名册去重；加城重置失城倒计时 |
| `Kingdom.AddHero` / `RemoveHero` | `kingdom.cpp:271/284` | 名册去重 |
| `Kingdom.IsLoss` / `IsLosingGame` / `HasHeroes` | `kingdom.h:98/147/115` | 出局与「正在输」分开 |
| `Kingdom.CanRecruitHeroes` | `kingdom.h:108` | 有城堡，或者还能盖城堡 |
| `Kingdom.CountCastles`/`CountTowns`/`CountMarketplaces`/`CountThievesGuilds`/`CountNecromancyShrines`/`CountBuildings` | `kingdom.cpp:344-374` | 位掩码计数 |
| `Kingdom.Kingdoms`（新） | `kingdom.h:294` / `kingdom.cpp:688-802` | 七槽容器、`GetKingdom`、`Init`、`Clear`、`AddCastles`、`AddHeroes`、`GetNotLossColors` |
| `World.Kingdoms`（新） | `world.h:312` `vec_kingdoms` | 世界持有的七槽 |
| `World.CreateMapKingdoms(Mp2FileInfo)` | `world.cpp:287` → `kingdom.cpp:688` | 按文件头起王国 |
| `World.AttachMapRosters()` | `world_loadmap.cpp:1443-1445` | 英雄与城按颜色进名册 |

要点：

* **账本是引用，不是快照**：`Treasury` 返回 `ref Funds`。城堡的 `BuyBuilding`/`RecruitMonster` 吃
  `ref Funds treasury`（ADR-107/108），交出去的必须是字段本身，否则下单只改副本。测试里用
  `ref Funds treasury = ref kingdom.Treasury` 直接改字段来钉住这件事。
* **`AllowPayment` 不能用 `Funds >=` 代替**：参考的规则是「某一项够，或者那一项本来就要 0」，所以零价格在空账本
  上也付得起；换成语义更漂亮的比较运算符会在「只要 0 木」这类需求上翻脸。
* **名册只做名册**：`RemoveCastle`/`RemoveHero` 不判负、解散英雄、给城改色。参考那三件事分别要 `Players` 与
  占领流程，端口把「有没有出局」做成 `IsLoss`/`IsLosingGame` 两个只读属性，由调用方问。
* **第七个槽是垃圾桶**：非法颜色、`NONE`、多色位掩码全落第七槽，`GetKingdom` 因而不返回可空引用——调用点太多
  都拿着「可能为 `NONE` 的颜色」来查，返回 `null` 会让每处都写空判断。
* **起始资源按文件头算**：参考从 `Settings` 取在局颜色与电脑颜色，装载时 `Settings` 还不存在；`Mp2FileInfo` 的
  `KingdomColors`/`ComputerPlayers`/`Difficulty` 对同一件事说得一样清楚，于是直接交给 `Kingdoms.Init`。
  `Players::HandicapStatus` 的折扣表在 `players.cpp`，未搬。
* **装载顺序**：王国先于英雄表（参考在 `Defaults()` 里就是这个次序），名册最后填——颜色是装载过程中才写进英雄
  与城的。`Reset` 用 `Clear()` 而不是换表：王国不记地图宽度，与城表（ADR-110）相反。
* **命名空间撞名**：类 `Kingdom` 在 `FHeroes2.Game.Kingdom` 里，所以 `FHeroes2.Game` 侧写 `Kingdom` 会解析到
  命名空间。测试用 `using PlayerKingdom = ...` 规避，与 ADR-110 的 `CastleTown` 同一手法。
* **测试踩到的两个坑**：英雄 id 从 1 起（`Hero(0, ...)` 直接抛）；「禁用城堡」位在 70 字节块的第 40 字节而不是
  最后一个字节，写错时城照样能招英雄。

用例：`KingdomTests` 22 条（新王国一无所有；玩家/电脑两张起始资源表；非法颜色留空；`Init` 会清掉旧名册；零价格
永远付得起；付多了留空；负收入不减到负；`Treasury` 是活引用；城与英雄各自去重；有英雄无城是「正在输」不是
「已出局」；有城堡或还能盖城堡才招英雄，禁用时不能；六个计数与组合掩码；七个槽的颜色映射与垃圾桶；`Init` 只动
在局颜色；名册跳过中立城与中立英雄；`GetNotLossColors` 排掉出局的；`Clear` 清干净；真实装载把蓝城放进蓝王国且
起始金 7500；文件头无颜色时王国保持清空态但名册仍按颜色分类）。
`FHeroes2.Game.Tests` 1322 → **1344**，全解决方案 2008 → **2030** 条全绿，0 警告 0 错误。
`kingdom` 行 0/4/10 → **0/5/9**，合计 27/61/161 → **27/62/160**。

**下一步**：周产与税收（`ActionNewDay`/`ActionNewWeek`，需要先有 `Week` 与 `Settings` 的地图版本），把招募
（`Recruits` + 英雄池 + 随机源）接上名册，或者 `Game::Save` 的城堡/英雄段。

## 日收入：三张表与王国求和 ✅（2026-11-16）

上一批的账本只会「收」不会「进」。这一批补上 `profit.cpp` 全部（三张日收入表）与 `Kingdom::GetIncome()`
里能算的两项（矿井、城镇），王国因此能回答「我每天进多少」——城堡购买对话框与 AI 估价都等这句话。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `ProfitConditions.FromMine(int)` | `profit.cpp:98-122` | 七种资源的矿井日产（木/矿 2，宝石类各 1，金 1000） |
| `ProfitConditions.FromBuilding(BuildingType, MapRace)` | `profit.cpp:33-50` | 城堡 1000、帐篷/雕像 250、术士的 `Spec` 500 |
| `ProfitConditions.FromArtifact(ArtifactType)` | `profit.cpp:52-96` | 神器奖励里的八种资源收入 + 税单的 `GoldPenalty` |
| `Kingdom.GetIncome(countCapturedMines, IncomeType)`（新） | `kingdom.cpp:564-646` | 矿井 × 表 + 城镇（城堡/雕像/术士地牢）求和 |
| `IncomeType`（新） | `kingdom.h:72-79` | 五个 `INCOME_*` 位 + `All = 0xFF` |

要点：

* **`Cost` 的字段顺序是坑**：参考的 `Cost{ gold, wood, mercury, ore, sulfur, crystal, gems }` —— 水银排在矿石
  之前，所以 `FromMine(ORE)` 的 `Cost{0,0,0,2,0,0,0}` 是「两块矿石」而不是「两点水银」。三张表都按这个顺序读，
  测试逐项钉住。
* **王国的日收入是「矿井 + 城镇」，不是「矿井 + 城镇 + 神器 + 技能」**：英雄还没有神器袋与技能（ADR-101 的
  `ArtifactBag` 是独立类，没挂到 `Hero` 上），所以这两项在端口里恒为 0。`IncomeType` 的位值照抄参考，
  等它们落地时只补分支，`All` 的含义自动对齐。当前状态与参考**等价**，因为英雄本来也拿不到神器。
* **世界不认识王国，王国也不认识世界**：参考的 `GetIncome` 直接问 `world.CountCapturedMines( resource,
  GetColor() )`；端口把这次询问变成一个 `Func<int, uint>` 参数（ADR-078 的 `CountCapturedMines` 已在
  `World` 上），调用方按需闭包上颜色。王国因此仍然是纯数据。
* **未知资源给零，不 assert**：参考在 `FromMine` 的 `default` 分支里断言，端口返回空 `Funds`——地图数据坏掉时
  少赚一天的矿，好过为了一个字节崩掉整局。
* **`FromArtifact` 走整张神器表**：`ArtifactTable.Get` 的 bonuses/curses 里只有八种资源奖励与 `GoldPenalty`
  与钱有关，其余（攻击、士气、法术威力……）一律不看。税单因此返回**负**的 `Funds`，王国的和也得能变小。

用例：`KingdomIncomeTests` 14 条（七种矿井各一日产；未知矿井给零；四种付钱的建筑与术士限定；金鹅 10000；
税单 -250；七只无尽袋子各一种资源；无资源奖励的神器给零；空王国给零；矿数乘表；回调被问到的七种资源；
帐篷 250 / 城堡 + 雕像 1250 / 两座城堡 + 术士地牢 2500；`IncomeType` 位掩码分别只要城镇或只要矿井）。
`FHeroes2.Game.Tests` 1344 → **1358**，全解决方案 2030 → **2044** 条全绿，0 警告 0 错误。
`profit` 由「未转」转「已转」（`profit.cpp` 全篇就三个函数），`kingdom` 行 0/5/9 → **1/5/8**，
合计 27/62/160 → **28/62/159**。

**下一步**：`ActionNewWeek`（周产与周事件，需要先有 `Week` 类型与 `Settings`），`ActionNewDay` 的每日入账
（要 `EventDate` 与访问记录），或者先给英雄挂上神器袋与技能，把 `GetIncome` 剩下的两项补齐。

## 周与月：`week.cpp` 的两条抽签表 ✅（2026-11-16）

上周的收入表只能算「一天多少钱」，但没人告诉它今天是「松鼠之周」还是「怪物之月」。这一批把 `week.h` /
`week.cpp` 搬过来：周名枚举、`Week` 值类型、周与月的两条抽签表、十二种月怪，以及抽签所依赖的种子混合函数
（`rand.h:260` 的 `combineSeedWithValueHash`——上一轮 ADR-070 记的「只差它」到此补齐）。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `WeekName`（新） | `week.h:29-72` | 34 项：15 个动物周、10 个昆虫月、怪物之周、瘟疫之月 |
| `Week`（新） | `week.h:75-97`、`week.cpp:126` | `Type`/`Monster`、`RandomWeek(isNewMonth, seed)` |
| `Rand.CombineSeedWithValueHash`（新） | `rand.h:260-265` | boost 式 `hash_combine`，六个盐值的来源 |

要点：

* **两条表，四种结果**：普通周从 `Squirrel..Condor` 里抽（75%），否则是怪物之周（25%）；每月第一周则从
  `Ant..Beetle` 抽（50%）、怪物之月（40%）或瘟疫之月（10%）。抽中的是枚举值区间，所以枚举顺序必须与参考
  一模一样——`WeekName` 的注释里写明了这一点。
* **一个种子六次加盐，不是连续抽两次**：参考用 `combineSeedWithValueHash( seed, salt )` 把地图的周种子拌上
  六个常数（367245 / 1946256 决定周类型与周名，9536582 / 5544783 决定月类型与月名，886473 / 1130906 决定
  怪物），所以「同一种子 → 同一周」与「周名与月名无关」同时成立。`CombineSeedWithValueHash` 是
  `seed ^= hash( value ) + 0x9e3779b9 + ( seed << 6 ) + ( seed >> 2 )`，整型的 `std::hash` 是恒等，所以
  32 位内算完即可；MSVC 与 libstdc++ 在这里没有分歧——ADR-070 当时判断「跨库不可复现」，是因为 `std::hash`
  对**非整型**（字符串）不保证一致，本函数的实际调用点只传整型与枚举。
* **怪物之周抽七十二种，怪物之月只抽十二种**：周版是 `GetWithSeed( PEASANT, BONE_DRAGON, seed )` 的直取，
  月版是 `GetWithSeed( 1, 12, seed )` 再走一个十二分支的 switch（农民 / 狼 / 食人魔 / 巨魔 / 矮人 / 德鲁伊 /
  独角兽 / 半人马 / 石像鬼 / 大鹏 / 吸血鬼 / 巫妖）。所以「怪物之月」永远不会是龙。
* **`Week::GetName()` 不转**：它要翻译层（`_()`）与 `Monster::GetName()`，两者都还没有。这也是 `week` 记
  「部分」而不是「已转」的原因——其余全转。
* **与 `World.Week` 属性撞名**：`World` 上有一个 `Week`（uint，面板显示的第几周，`world.h:381`）。
  将来把 `Week` 接进 `World` 时要么写全名 `FHeroes2.Game.Kingdom.Week`，要么给属性改名——记在 ADR-113 里。

用例：`WeekTests` 17 条（默认 `Unnamed`/`Unknown`、构造保留两个字段、同种子可重复、11 个种子的快照把六个
盐钉死、周扫描：动物周不带怪物且怪物之周的怪物落在 `Peasant..BoneDragon`、月扫描：昆虫月不带怪物 / 怪物之月
的怪物只在十二种里 / 瘟疫月不带怪物、20000 个种子的概率 75/25 与 50/40/10）＋ `RandTests` 1 条
（`combineSeedWithValueHash` 的恒等、环绕、盐区分与有符号重载）。
`FHeroes2.Core.Tests` 192 → **193**，`FHeroes2.Game.Tests` 1358 → **1375**，全解决方案 2044 → **2062**
条全绿，0 警告 0 错误。`week` 由「未转」转「部分」：`kingdom` 行 1/5/8 → **1/6/7**，合计
28/62/159 → **28/63/158**。

**下一步**：把 `Week` 接进 `World`（`World::NewWeek()` 的周类型那一支 + `GetWeekType()`），然后是
`ActionNewDayResourceUpdate`（每日入账，要 `World.GetEventsDate`）与 `ActionNewWeek` 的招募增长。

## 周接进世界、每日入账 ✅（2026-11-16）

上一批立了 `Week` 类型，但世界不知道自己是哪一周，王国也还没人告诉它「今天进多少钱」。这一批把两头接上：
`World` 从地图种子与周计数算出本周（`WeekSeed`/`WeekType`），并按颜色筛出当日事件（`GetEventsDate`）；
`Kingdom.ActionNewDayResourceUpdate` 则把「收入 + 当日事件」一次记账，首日不结算、AI 跳过人类专属事件、
金额按金库钳制。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.MapSeed`（新） | `world.h:504`、`world.cpp:291/335` | 地图种子；`Reset` 归零，随机取值在 `Defaults()` |
| `World.WeekSeed`（新） | `world.cpp:1263` | `CombineSeedWithValueHash( MapSeed, _week )` |
| `World.WeekType`（新） | `world.cpp:410` | `Week.RandomWeek( IsFirstWeek, WeekSeed )`，按需算 |
| `World.GetEventsDate(PlayerColor)`（新） | `world.cpp:875` | 按颜色与日期筛 `EventDate.IsAllowed` |
| `Kingdom.ActionNewDayResourceUpdate(...)`（新） | `kingdom.cpp:225-250` | 首日不结算 → 收入 → 当日事件（AI 门 + 钳制 + 对话框） |

要点：

* **周类型算而不缓存**：参考把答案存进 `GetWeekType()` 里的函数内静态表，键是 `( _week, GetWeekSeed() )`，
  因为重绘时每个瓦片都会问一次。端口每次重算——两次哈希加最多两次 `GetWithSeed`——换来的是「缓存不会跨
  `Reset` 存活」这条不变量；`Reset` 之后 `_seed` 与 `_week` 都归零，缓存版迟早会在换地图时读到上一局的周。
* **`Reset` 把种子一起清掉**：参考在 `Defaults()` 里抽新种子、在 `Reset()` 里写 0（`world.cpp:291/335`）。
  端口没有 `Defaults()`（那是会话层的活），所以 `MapSeed` 的 setter 是 `internal`，留给装载器、将来的存档与
  测试；默认 0 的世界仍然自洽，只是每台机器都玩同一串周。
* **`WeekType` 里必须写全名**：`World` 自己有一个 `Week` 属性（面板显示第几周，uint），在类内会遮蔽
  `FHeroes2.Game.Kingdom.Week` 这个类型名——ADR-113 记过的坑，这次照约定写全名并注明原因。
* **`GetEventsDate` 只做过滤**：事件本身在装载时已经进 `_dailyEvents`，这里只是按 `IsAllowed( color, _day )`
  取当日、当色、当周期的那些，保持地图顺序。返回值是 `List<EventDate>`（参考返回 vector 的拷贝）。
* **首日不给钱是参考的规则，不是漏写**：`if ( world.CountDay() > 1 )`（`kingdom.cpp:227`）——第 1 天玩家拿的是
  起始资源，不该再领一次收入；测试专门钉住这一条。
* **事件的金额是钳制后的**：`Resource.CalculateEventResourceUpdate` 用 `std::clamp` 把增量压到「金库有多少」
  与「上限」之间，所以负事件最多把资源扣到 0，正事件也不会越过资源上限。对话框拿到的是**实付**金额而不是
  事件里写的数额，这一点也测了。
* **AI 门**：地图可以把事件标成「只给人类玩家」，电脑玩家的王国会跳过它（`kingdom.cpp:236`）。端口没有
  `Players`，所以 `isAiControlled` 是参数。
* **王国仍然不认识世界**：`GetIncome` 要的矿井数走回调、`GetEventsDate` 的结果当数据传进来，于是这条日循环在
  调用方只有一行：`kingdom.ActionNewDayResourceUpdate( world.DayCount, resource => world.CountCapturedMines(
  resource, kingdom.Color ), world.GetEventsDate( kingdom.Color ), isAiControlled )`。`Game::NewDay` 那一层还
  没转，所以这行调用暂时只存在于测试与文档里。

用例：新增 `KingdomDailyUpdateTests` 8 条（首日不结算、次日起矿井付钱、事件给资源并回报对话框、负事件只能扣到
零、对话框看到实付金额、AI 跳过人类专属事件、无对话框时静默应用、多个事件依次入账）；`WorldDateTests` 加 4 条
（周种子 = 地图种子加周计数、周类型随周计数在两张表间切换、九周不是同一个抽签、`Reset` 忘掉种子）；
`WorldSphinxAndEventsTests` 加 3 条（只返回当日当色的事件、保持地图顺序、重复事件在每次发生日返回）。
`FHeroes2.Game.Tests` 1375 → **1390**，全解决方案 2062 → **2077** 条全绿，0 警告 0 错误。覆盖率不动：
`week` 仍「部分」（`GetName` 等翻译层）、`kingdom` 仍「部分」，合计仍 **28/63/158**。

**下一步**：`ActionNewWeek` 的招募增长（要先有 `Recruits`/英雄池）、`ActionNewDay` 剩下的访问记录与胜负判定
（要 `Visit` 与 `Players`），或者继续 `PostLoad` 的 AI 区域。

## 城堡的周产 ✅（2026-11-16）

世界已经在数周了，城却还不会生兵。这一批把 `Castle::ActionNewWeek` 整篇搬过来，并把「月 → 周 → 日」的
推进顺序接到 `World.NewDay` 上：城表的 `NewWeek` 先于 `NewDay`，与参考一致。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Castle.ActionNewWeek(...)`（新） | `castle.cpp:838-940` | 首周跳过 → 住所生长（井 +2、第一住所的 `Well2` +8、中立减半）→ 怪物周 +5 → 中立收守军 → 月初：瘟疫减半 / 怪物月加 100% |
| `Castle.GrownWell`/`GrownWel2`/`GrownWeekOf`/`GrownMonthOf`（新，`const`） | `game_static.cpp:145-163` | 2 / 8 / 5 / 100 |
| `Castle.GetDwellingShelf`/`TryFindWeekMonsterDwelling`（新，私有） | `castle.cpp:813-836`、`886-908` | 住所位查表；按**精确**建成的住所找周怪 |
| `AllCastles.NewWeek`/`NewDay`（新） | `castle.cpp:2361-2373` | 城表把周与日分发给每座城 |
| `World.NewDay()`（改） | `world.cpp:427-459` | 周的类型交给城表，日再交；事件的剪枝留到最后 |
| `AllCastles.Add`（改） | `castle.cpp:2389` 一侧 | 宽度为 0 时先返回，别让 `PointOf` 除零 |

要点：

* **周产要的参数全部进签名**：周计数、`Week`、是否月初、天数、随机源。城不认识世界，这条约定从 ADR-107/109
  一直用到 ADR-114，这里照旧——`world.cpp` 里那五个 `world.*` 调用在端口里是五个实参。
* **四个常量不是配置**：参考的 `GameStatic::GetCastleGrownWell()` 等四个函数在 `game_static.cpp` 里直接返回
  字面量（`2`/`8`/`5`/`100`），端口没有设置层，就落成 `public const`，XML 文档里写明它们将来会变成设置项。
* **生长的数是「实际住所」的**：`GetGrowth( GetActualDwelling( dwelling ) )`——箭塔升级成游侠塔之后，那一
  层的生长按游侠算（8），但**周怪加成**仍然要求精确建成的那个住所，所以怪物周的箭矢不会喂给游侠塔。
* **怪物周的加成跳过月初**：`castle.cpp:884` 的 `if ( isMonsterWeek && !isBeginMonth )`——月初那一周给的
  是月度量（库存的 100%），再叠 +5 就是重复给。
* **中立城的两条规则**：生长 `/2`（向下取整），并 `JoinRandomArmy` 收守军——城堡必收两次，普通城 40% 才
  收第二次。随机源从参数进，测试用固定种子把「第二次到底有没有发生」钉死。
* **`AllCastles.Add` 的顺序 bug**：`PointOf( TileIndex )` 要除以 `_width`，而 `Add` 原来的宽度检查在它之后
  调用，`new AllCastles(0)` 一加城就 `DivideByZeroException`。检查提前，注释里写明原因。
* **井是被前置拖进来的**：骑士的第三住所需要井，而 `DwellingUpgrade2` 的前置含第三住所，所以「只建了箭塔
  升级」的城里其实有井——测试一开始就栽在这里（期望 16 实得 18），现在测试显式断言井在场并把 +2 算进去。

用例：新增 `CastleWeekGrowthTests` 13 条（首周不生长、每个已建住所长一周、井给每个住所 +2、`Well2` 只给第一
住所、中立半速、怪物周 +5、怪物月翻倍、瘟疫周冻结、瘟疫月初减半且向下取整、升级住所只吃自己生物的生长、
`AllCastles` 推进全城、`NewDay` 归还建造许可、`World.NewDay` 第八天让城生长）。
`FHeroes2.Game.Tests` 1390 → **1403**，全解决方案 2077 → **2090** 条全绿，0 警告 0 错误。覆盖率不动：
`castle.cpp` 与 `world.cpp` 本来就在「部分」里，合计仍 **28/63/158**。

**下一步**：`ActionNewMonth`（月度事件与招募池重置，要 `Recruits`）、`ActionNewDay` 剩下的访问记录与胜负判定
（要 `Visit` 与 `Players`），或者继续 `PostLoad` 的 AI 区域。

## 区域划分 ✅（2026-11-16）

世界已经在数周了，`PostLoad` 的尾巴上却还缺那块「AI 眼里的地图」：参考在装载结束时把地图切成连通的地块，
顺手量出水面占比与地形粗糙度（`world_regions.cpp`，由 `world.cpp:1233` 调用）。这一批把它整篇搬过来。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.ComputeStaticAnalysis()`（新） | `world_regions.cpp:178-398` | 九步：数障碍 → 空行/列 → 城堡种子 → 补种子 → 带 padding 的工作副本 → 每种子一区 → 每轮各长一格 → 剩余自成区 → 写回并连邻居 |
| `MapRegionNodes`/`MapRegionNode`/`MapRegion`（新） | `world_regions.h:28-65` | 四个常量（`Blocked`/`Open`/`Border`/`Found`）、可变结构体节点、区域（节点表 + 有序邻居集） |
| `World.WaterPercentage`/`LandRoughness`（新） | `world.h:488/493`、`world_regions.cpp:239-246` | 水面百分比（向下取整）、陆地粗糙度（地形惩罚均值 / 草地惩罚） |
| `World.RegionCount`/`GetRegion(id)`（新） | `world_regions.cpp:163-176` | 越界返回静态空区域，不抛异常 |
| `World.ClearRegions()`（新，私有） | `World::Reset()`（参考不清） | `Reset` 把区域与两项统计一起清掉 |
| `Tile.Region`/`UpdateRegion(...)`（新） | `maps_tiles.cpp:70-78` | 每格的区域 id，默认 `MapRegionNodes.Blocked` |
| `World.PostLoad()`（改） | `world.cpp:1233` | `ResetPathfinder()` 之后算一次静态分析 |

要点：

* **三个常量区不是凑数**：区域列表的前三项是 `Blocked`/`Open`/`Border`，它们各带一个节点、永不生长。
  这样 `MapRegionNode.Type` 只有一套编号——四个常量或真实区域 id——节点能区分「没有区域」和「3 号区域」。
  代价是任何按区域遍历的代码都得从 `MapRegionNodes.Found` 起（测试一开始就在这上面栽过一次）。
* **种子顺序是算法的一部分**：城堡先（颜色倒序，`NONE` 是 0 所以中立城最后；同色按 tile 索引），
  然后每 7 行/列取最空的那几条作种子线，线交叉处再补种子（至少 18 格远；城堡是 17 格）。
  两个种子相距两格与相距二十格得到的是完全不同的划分，所以顺序不能当成实现细节。
* **同分按行号兜底**：参考只按障碍数排序（`world_regions.cpp:250`），同分的次序交给 `std::sort`。端口在
  计数相同的时候比行号，无并列的地图结果一致，有并列的地图也可复现。
* **城堡种子取「城门下一格」**：`index + width`，那是英雄落脚的地方；参考的范围判断原样保留——
  种子落到地图最后一行之后时回落到城堡自身（`world_regions.cpp:282`）。
* **每轮每区只长一格**：`RegionExpansion` 先把 `Nodes.Count` 拍成快照，本轮只处理快照内的节点，
  所以节点多的大区不会比小区抢得快。这正是不用 Dijkstra/多源 BFS 的原因：参考要的是「同时扩张」。
* **一圈 padding 免掉边界判断**：工作副本宽高各 +2，`ConvertExtendedIndex` 把真实索引映射进去；
  padding 的 `Type` 就是 `new` 出来的 0（`Blocked`），因此邻居检查可以直接读八个方向。
* **区域不跨岸**：邻居必须「朝回走」可通行（`GetDirectionBitmask(direction, reflect: true)`），
  且水陆同类。两个条件各写一半，缺一个就会把海岸线吃穿。
* **剩余区域没有邻居**：`FindMissingRegions` 建的区域只长自己的格子，不加邻居——参考如此，
  测试里显式断言这类区域 `Neighbours` 为空，而不是按「相邻的陆地与水域应该互为邻居」去猜。
* **刻意偏离：写回种子类型**。参考在这里把种子留在 `REGION_NODE_OPEN`（`world_regions.cpp:136-151` 从不写
  `type`），区域绕回来时会把种子再收一次，于是湖心那一格在 `_nodes` 里出现两遍。端口写回 `Type`，
  `Nodes` 精确——将来 AI 拿 `Nodes.Count` 算面积时不会凭空多一格。
* **传送点连区域**：石阵与漩涡的出口格所属区域加进本区 `Neighbours`，再回填对称引用。参考在一个
  `std::set` 里边遍历边插入，C# 的集合不允许，所以先 `ToArray()` 快照再回填（循环本身只加反向引用）。
* **参考 assert，端口返回空答案**：`world_regions.cpp:239-244` 断言地图有可走格、有陆地。端口的世界可以是
  空的（`Reset` 之后就是），所以空世界直接返回；全水地图的 `LandRoughness` 保持 1.0 而不是除零——
  区域照样算，只是没有陆地可量。
* **`Reset` 清区域**：参考的 `Reset` 不动 `_regions`，端口清掉。理由与 ADR-114 的周缓存相同：
  区域是瓦片索引，不能比瓦片活得久。

用例：新增 `WorldRegionsTests` 8 条（空世界没有区域、纯草地是一块陆地、相接的区域互为邻居、
海岸线隔开水陆、全水地图是一块水域、沙漠让陆地更粗糙、`Reset` 忘掉区域、真实地图上每块区域连通且同质——
每格的 `Region` 与区域的 `Nodes` 对得上、同区 BFS 连通、邻居互相登记、水比例与粗糙度复算一致）；
真实地图用 `GameData.TryResolveDataDirectory()` 找 `Alteris.mp2`，顺带把「安装目录的上一级才是 `maps`」这条也测了。
`FHeroes2.Game.Tests` 1403 → **1411**，全解决方案 2090 → **2098** 条全绿，0 警告 0 错误。
覆盖率：`world` 目录 `0/4/1` → **`1/4/0`**（`world_regions` 由「未转」变 ✔），合计 **28/63/158** → **29/63/157**。

**下一步**：`PostLoad` 只剩漩涡带船的修复；再往下是给每日入账找调用方（`Game::NewDay` 那一层要 `Settings`
与 `Players`），或者按依赖顺序进 `kingdom/*`/`army/*`。AI 才是区域的消费者，但它读不写，可以等 `Players`。

## 王国的价目与市场汇率 ✅（2026-11-16）

王国已经会算钱、会数周、会切地块，但商店那一栏的标价还空着：船多少钱、法术书多少钱、市场里一个木换多少金。
这一批把 `payment.cpp` 与 `resource_trading.cpp` 两篇整搬过来——它们都是纯查表，正好接在 `profit.cpp` 旁边。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `PaymentConditions`（新） | `payment.cpp:29-57` | 船 1000 金 + 10 木、法术书 500 金、酒馆英雄 2500 金、炼金术士 750 金、麦哲伦地图 1000 金；`BuyBuilding` 转发建筑造价 |
| `ResourceTrading.GetTradeCost(...)`（新） | `resource_trading.cpp:59-160` | common/rare/gold 三类的七个组合、每类九列（1–9 个市场）汇率表 + 一个 switch |

要点：

* **`Cost` 的字段顺序是个坑**：参考是 `{ gold, wood, mercury, ore, sulfur, crystal, gems }`（`resource.h:37-46`），
  港口的 `Cost` 同序，所以 `new Cost { Gold = 1000, Wood = 10 }` 与参考的 `Cost{ 1000, 10, 0, 0, 0, 0, 0 }`
  一一对应。船是「千金十木」，写成 `Wood = 1000` 就会变成一千根木头——测试专门盯着这一条。
* **`BuyBuilding` 不是别名**：参考的签名是 `BuyBuilding( int race, uint32_t build )`，race 在前；港口的
  `CastleBuildings.GetCost` 是 `( building, race )`。端口照参考的顺序收参，注释里写明两者相反，
  免得下一个人把它「简化」成一行转发再踩一遍。
* **未知或不属于该种族的建筑返回空价，而不是断言**：骑士没有黑龙门（`DwellingUpgrade7` 只有术士有），
  在 UI 里那是一颗灰按钮，不是 bug。测试同时钉住「术士有、骑士没有」。
* **七张表，不是一条公式**：1 个市场 1 木 = 25 金，9 个市场 = 124 金；common→common 与 rare→rare 共用一张表
  （参考的注释说得很清楚：类别内互换算同一个比例），于是八个有序组合收敛成七张表。
* **市场数为 0 返回 0**：参考 `assert( 0 )` 后返回 0。港口直接返回 0——对话框里什么都还没选是正常状态。
* **超过 9 个市场钳到第 9 列**：参考也钳。这不是防御性代码：一张图可以放十来座城，`AllCastles` 数出来
  的市场数会超过表宽。
* **非法资源位返回 0**：`0x4000` 这种不在七个资源里的位，参考落到 `default: assert( 0 )`，港口返回 0。
  四张类别表各有自己的 `default`，四支都测了。

用例：新增 `PaymentConditionsTests` 5 条（五个固定价、船的「金 vs 木」、按种族定价、不能建的城返回空价、
「转发不改表」——六个种族 × 全部建筑逐一对 `CastleBuildings.GetCost`）、`ResourceTradingTests` 6 条
（第 1 列、第 9 列、类别内双向同价、市场越多不会更差、超过九个钳位、无意义交易与非法资源位返回 0）。
`FHeroes2.Game.Tests` 1411 → **1422**，全解决方案 2098 → **2109** 条全绿，0 警告 0 错误。
覆盖率：`kingdom` 目录 `1/6/7` → **`3/6/5`**（`payment`、`resource_trading` 由「未转」变 ✔），
合计 **29/63/157** → **31/63/155**。

顺带更正一条旧结论：1.1.18 的 `PostLoad` 里并没有「漩涡带船的修复」。带船的漩涡格在装载时被记成船对象，
所以参考按**部件**而不是按类型找漩涡（`world.cpp:1219-1225`），港口一直就是按部件查的。`PostLoad` 到
`ComputeStaticAnalysis` 加 UID 修补为止，已经整篇落地。

**下一步**：`kingdom` 只剩 `puzzle`（`Puzzle` 的位集与揭示逻辑）、`speed`（`_()` 文案）、`kingdom_overview`
与 `view_world`（界面）、`experience`（两句文案）；再往后是 `castle` 的七篇未转与 `army` 剩下的两篇。

## 酒馆的那一对英雄 ✅（2026-11-16）

王国会数钱、会数周、会切地块，却还答不出「酒馆今天卖哪两位」。参考把这件事放进 `heroes_recruits.cpp`：
一个 `Recruit`（英雄 id + 投降日）与一个 `Recruits`（两个槽位），随王国走。这一批整篇搬过来，顺手把它挂进 `Kingdom`。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Recruit`（新） | `heroes_recruits.h:39-70`、`.cpp:33-41` | 记录的 `readonly record struct`；`default` 就是「UNKNOWN + 0」，即参考的默认构造 |
| `Recruits`（新） | `heroes_recruits.h:72-92`、`.cpp:44-104` | `First`/`Second`、`Id1`/`Id2`、`GetHero1/2`、`GetSurrenderDayOfHero1/2`、`SetHero1/2`、`AppendSurrenderedHero`、`Reset` |
| `Kingdom.GetRecruits()`（新） | `kingdom.h:169`、`kingdom.cpp:317` | 返回本王国的那一对 |
| `Kingdom.Clear()`（改） | `kingdom.cpp:132` | 连同城、英雄一并清掉酒馆的一对 |

要点：

* **`Recruit` 只是一个 id 加一个日子**：英雄本身要从 `AllHeroes` 里查（参考走 `world.GetHeroes()`）。
  这样酒馆、新周结算、投降结算三处能共用同一份状态，而不用把它绑在 `World` 上。
* **`default` 恰好就是「空槽」**：参考默认构造是 `_id = UNKNOWN`、`_surrenderDay = 0`，C# 的
  `record struct` 默认值一模一样，所以 `Recruit.None` 直接返回 `default`，`Reset` 就是两个 `default`。
  没有比「默认值即空值」更省事的表示，但它也带来一条纪律：投降日不能是 0（见下）。
* **`SetHero` 顺手打 `RECRUIT` 位，清空槽位却不撤位**：参考的 `SetHero1( nullptr )` 只把槽位置空。
  撤位属于「英雄离开待聘名单」的那一方（被雇走、进监狱），不是「这个王国不再推荐他」。
  把撤位塞进 `SetHero1` 会让两个王国共用一位英雄时互相踩。
* **替换规则照抄，含并列**：`appendSurrenderedHero` 把新来的塞进投降日更旧的格子；两天相等时参考的
  `first.getSurrenderDay() > second.getSurrenderDay()` 为假，于是替换**第一格**。这决定了连续三位投降英雄
  在槽位里的顺序，测试逐日断言（5→第一格、7→第二格、9→第一格，5 的那位出局但保留 `RECRUIT` 位）。
* **投降日为 0 抛异常**：参考 `assert( heroSurrenderDay > 0 )`。港口抛 `ArgumentOutOfRangeException`——
  0 同时也是空槽的标志，允许它会让这位英雄永远挂在名单上。
* **原版只给一位，fheroes2 给两位**：注释里写着这是扩展机制，所以「第二个槽位」不是 bug 也不是遗留代码。
* **`Kingdom.GetRecruits()` 只返回，不刷新**：参考的同名函数顺手补位（槽位空着、占位英雄已不可招募），
  第一周还要优先考虑战役奖励的英雄。补位本身要 `Settings::isCampaignGameType`、战役存档、`Players` 的
  「是否人类控制」和 `World::CountWeek`，前四样都没有；`AllHeroes.GetHeroForHire` 已经有了，等那一层落下来接上。

用例：新增 `RecruitsTests` 7 条（新的一对两个槽位都空、`SetHero` 打位并填槽、清空槽位保留英雄的 `RECRUIT` 位、
投降英雄替换更旧的一格、投降日为 0 被拒、`Reset` 清两格、王国开场没有待聘英雄且 `Clear` 之后忘干净）。
`FHeroes2.Game.Tests` 1422 → **1429**，全解决方案 2109 → **2116** 条全绿，0 警告 0 错误。
覆盖率：`heroes` 目录 `1/4/7` → **`2/4/6`**（`heroes_recruits` 由「未转」变 ✔），合计 **31/63/155** → **32/63/154**。

**下一步**：`heroes` 剩下 `heroes_action`（要 `Players` 与 `World` 的动作层）、`heroes_spell`、`heroes_ui` 之类；
随时可以回到 `kingdom.cpp` 的 `ActionNewWeek`——它现在只差「一周生命周期的访问记录」（`Visit` 层）与一处
调试模式赠礼，`GetRecruits()` 这个调用点已经能接上了。

## 石阵的拼图 ✅（2026-11-16）

地图上的石阵（Obelisk）每访问一座，就揭开寻宝图的一角。参考把它做成一个 48 格的位集加四条洗好的区带表
（`puzzle.h`），王国各持一份（`Kingdom::puzzle_maps`）。这一批搬的是位集与 `Update`——对话框和存档留给后面的层。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Puzzle`（新） | `puzzle.h:29-55`、`puzzle.cpp:303-339` | 48 格 `bool[]`、四条区带（24/16/4/4）、`Update(open, total)`、`Reset()`、`Count`、索引器 |
| `Puzzle(generator)`（新） | `puzzle.cpp:303` | 洗牌走传进来的 `Pcg32`；不传就用线程级随机源 |
| `Kingdom.GetPuzzleMaps()`（新） | `kingdom.h:180` | 返回本王国的那张拼图 |
| `Kingdom.Clear()`（改） | `kingdom.cpp:134` | `puzzle_maps.reset()`：只清格子，**不**重洗区带 |

要点：

* **四条区带是「先框后心」的顺序，不是编号巧合**：`zone1` 是外框的 24 格、`zone2` 是内环 16 格、
  `zone3`/`zone4` 是两个 2×2 块。`Update` 逐区填满才轮到下一区，所以半数的石阵必然只揭开轮廓。
  这也解释了为什么原版的寻宝图总是先有边框——那是算法，不是美术。
* **洗牌在王国创建时做一次**：参考在 `Puzzle::Puzzle()` 里 `Rand::Shuffle` 四条表。港口把它做成构造参数
  `Pcg32`，因为存档要能复现同一张布局；不给参数时退回线程级随机源，行为与参考一致。
* **只增不减**：`Update` 拿「应揭开的数量」减去「已揭开的数量」，差为 0 就什么都不做。丢掉一座石阵不会把
  图收回去，重复访问同一座也不会多揭一格——两件事各有一条用例钉着。
* **截断而不是四舍五入**：`open * 48 / total`。48 座石阵时一次一座一格；5 座时一次 9 格（9.6 截断），
  所以「揭开比例」永远不超过玩家挣到的比例。
* **总数为 0 直接返回**：参考会除零（UB）。一张没有石阵的图是合法的，它只是没有拼图可揭。
* **`Reset` 只清格子**：参考 `Kingdom::clear()` 里是 `puzzle_maps.reset()`，位集清零、区带顺序保留。
  换图之后同一份石阵数会揭开同一批格子——这是参考的行为，测试用「清前清后同一批格子」钉住。
* **`Count` 是数出来的**：参考用 `std::bitset::count()`，端口逐格数。48 个格子，没有优化的必要，
  也没有缓存可写错。

用例：新增 `PuzzleTests` 8 条（新拼图全关、没有石阵什么都不开、全开、截断与重复访问不叠加、24 格时
「外框满而两个 2×2 未动」、丢石阵不退图、`Reset` 后同一批格子重开、王国开场全关且 `Clear` 之后忘干净）。
`FHeroes2.Game.Tests` 1429 → **1437**，全解决方案 2116 → **2124** 条全绿，0 警告 0 错误。
覆盖率：`kingdom` 目录 `3/6/5` → **`4/6/4`**（`puzzle` 由「未转」变 ✔，对话框与存档仍算在别处），
合计 **32/63/154** → **33/63/153**。

**下一步**：拼图的两头都缺——「谁数已访问的石阵、何时调 `Update`」在 `heroes_action.cpp` 的石阵分支，
`ShowMapsDialog` 在渲染层，`Puzzle` 的存档读写要等存档格式。`kingdom` 目录只剩 `experience`/`speed`
（`_()` 文案）与 `kingdom_overview`/`view_world`（界面）四篇。

## 王国的访问名册与日/周推进 ✅（2026-11-16）

「这座风车我这周拿过了」「这座石阵我看过了」「这周已经过去了」——三句话都要一张按对象类型分类的访问名册，
和两个每天/每周跑一次的清理动作。参考把它们放在 `visit.cpp`（三条寿命谓词）、`kingdom.cpp`（名册本体与
`ActionNewDay`/`ActionNewWeek`）、`world.cpp:443`（谁在什么时候调）。这一批三处一起搬，顺手把拼图的调用方接上。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Maps/Visit.cs`（新） | `visit.h:24-33`、`visit.cpp:22-40`、`pairs.h:34-44` | `IndexObject`（格子 + 对象类型，`record struct`）、`VisitScope`（`Local`/`Global`）、`Visit.IsDayLife/IsWeekLife/IsBattleLife` |
| `Kingdom` 访问名册（新） | `kingdom.h` 的 `visit_object`、`kingdom.cpp:666/681` | `SetVisited`（`OBJ_NONE` 不记、同「格子 + 类型」先删后插表头）、`IsVisited(index, type)`、`IsVisited(type)`、`IsVisited(tile)`、`CountVisitedObjects`、`VisitedObjects` |
| `Kingdom` 其余状态（新） | `kingdom.h:60-69`、`kingdom.h:224-251` | `KingdomModes`（`IdentifyHero`、`KingdomOverviewCastleSelection`）、`SetModes`/`ResetModes`、旅行者帐篷位图 `MarkTravellerTentVisited`/`IsTravellerTentVisited`、水晶球 `AddMonsterUnderVision`/`IsMonsterUnderVisionSpell`、`IsPlay`/`IsControlHuman` |
| `Kingdom.ActionNewDay`（新） | `kingdom.cpp:197-231` | 清一天寿命的访问记录（**出局也清**）→ 失城倒计时（第一天不算、有城不递减）→ 出局或归零交给 `LossPostActions` → 否则清「已鉴定」位与水晶球 |
| `Kingdom.ActionNewWeek`（新） | `kingdom.cpp:266-290` | 清一周寿命的访问记录（出局也清）→ 跳过第一周 → 调试赠礼（仅人类）→ 重排酒馆那一对 |
| `Kingdoms.NewDay`/`NewWeek`（新） | `kingdom.cpp:770-786` | 七个槽全走一遍 |
| `World.NewDay`（改） | `world.cpp:443-455` | 王国的周在城之前、王国的日在城之前 |
| `Kingdom.ActionObelisk`（新） | `heroes_action.cpp:2924-2946` | 全局登记 + 按「已访问 / 总数」重算拼图，返回「是否揭开了一角」 |

要点：

* **名册是「格子 + 类型」，不是「格子」**：编辑器（和事件、和战后的地形）会把一个格子上的对象换掉，
  只按格子去重会把「新对象还没看过」当成旧对象的记录。参考的 `std::find` 找的也是这个二元组。
* **最新在前**：`SetVisited` 先删同项、再插表头。按类型问「访问过没有」时可以边走边停，`CountVisitedObjects`
  也不用排序。
* **清理在 `isPlay()` 之前**：参考自己写了理由（`kingdom.cpp:198`）——已经出局的王国也要清，否则存档里的
  访问表会一直涨。顺序照抄，日志里那条注释也照抄。
* **三条寿命正好是三个清理时机**：一天（魔法井）、一周（风车/水车/巢穴）、永久（石阵、一次性加值建筑）。
  所以 `Visit` 的三条谓词直接转发 `Mp2ObjectTypes` 的分类，不另立一套表。
* **`IsPlay` 暂时等于「颜色不是无」**：参考问 `Players::Get(color)->isPlay()`。端口还没有 `Players`，
  用颜色代替，并在 `ActionNewDay` 里加了一个 `_isOutOfGame`：一旦交出去就不再参战——这正是参考
  `LossPostActions` 第一件事 `SetPlay(false)` 的效果，不这么做的话「已经输了」的王国每天都会再走一遍
  `LossPostActions`（第一版就是这样，测试抓到了）。
* **失城倒计时只在「归零那一天」交出去一次**：`IsLoss` 对「一城一英雄都没有」的王国本来就是真，参考靠
  `SetPlay(false)` 挡住第二次；端口补上了那个状态位，因此回调恰好一次。
* **`LossPostActions` 是可选回调**：它要 `Players`（改色、删名册、弹胜负框）。端口保留「出局了就别往下走」
  这一半语义，另一半等 `Players` 落地由调用方传进来——与 `EventDate` 的显示器回调一个路子（ADR-113/114）。
* **调试赠礼是编译期开关不是运行时开关**：参考用 `IS_DEVEL()`，端口提成 `isDebugMode` 参数；金额照抄
  （5000 金、20 木、20 水银、其余各 10）。
* **帐篷与水晶球不进名册**：帐篷是按颜色位的永久解锁（周/日都不清），水晶球是「今天看得见」（每天清）。
  两者都放在王国上，但生命周期不同，所以不与 `visit_object` 混在一起。
* **石阵走 `GLOBAL`、风车走 `LOCAL`**：这是 `heroes_action.cpp` 每一支自己选的。石阵进王国名册，因为
  换个英雄去同一座石阵不该再揭一次；风车进英雄自己的记录（等 `hero.GetKingdom()` 那条链接落地后再说）。

用例：新增 `KingdomVisitTests` 17 条（最新在前且不重复、按格子/按类型/按瓦片问、`OBJ_NONE` 不记、
数石阵、日/周各清各的、出局也清、失城倒计时八天归零、胜负流程只交一次、每日清「已鉴定」与水晶球、
调试赠礼只给人类且跳过第一周、帐篷是永久解锁、`Clear` 忘干净、七槽全 tick、石阵首次/重复/全开）。
`FHeroes2.Game.Tests` 1437 → **1454**，全解决方案 2124 → **2141** 条全绿，0 警告 0 错误。
覆盖率：`maps` 目录 `3/7/7` → **`4/7/6`**（`visit` 由「未转」变 ✔），合计 **33/63/153** → **34/63/152**。

**下一步**：`hero.GetKingdom()` 那条链接（英雄 → 王国）是 `heroes_action.cpp` 其余每一支的前置；`ActionNewMonth`
要 `Settings`；`castle` 的法师塔要 `spell.cpp` 的种族权重表；`battle` 核心要 `HeroBase`/`Battle::Board`。

## 英雄的访问名册、动作层与运气/士气重算 ✅（2026-11-16）

上一批把名册放在王国上，这一批把它放在英雄上：`Heroes::SetVisited`/`isVisited` 有两层（`Visit::LOCAL` 记在
英雄自己身上，`Visit::GLOBAL` 转给王国），`GetLuck`/`GetMorale` 每次按「访问过的对象类型集合」重算，`heroes_action.cpp`
的十二支动作对象也一起搬（魔法井、泉眼、运气/士气建筑四支，加上黑猫、风车/水车/小屋/魔法花园、马厩、
旅行者帐篷、石阵、竞技场、屏障、取宝），顺手把「英雄在每日/每周该做什么」接到王国的 tick 上；后面一章再补
凉亭与知识树两支（见下一节）。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Hero.Visit.cs`（新） | `heroes.cpp:1165`（`ActionNewDay`）、`:1178`（`ActionNewWeek`）、`:1183`（`ActionAfterBattle`）、`:1190`（`getDailyRestoredSpellPoints`）、`:1261-1320`（`isVisited`/`SetVisited`/`isObjectTypeVisited`/`getAllVisitedObjectTypes`） | 英雄侧名册 `_visitedObjects`（最新在前）、`IsVisited(int, type)`/`IsVisited(Tile)`/`IsObjectTypeVisited`/`GetAllVisitedObjectTypes`（按类型去重）、`SetVisited`、`ClearVisitedObjects`；日/周/战后三个清理动作 |
| `Hero.Morale.cs`（改） | `heroes.cpp:145-205`（两个对象修正汇总）、`:1033-1102`（`GetMorale`/`getMoraleWithModifiers`/`GetLuck`/`getLuckWithModifiers`） | `GetMorale`/`GetLuck` 改为「技能项 + 访问对象项」重算，`GetVisitedObjectsMorale/Luck` 用 `Sum(GameStatic.Get…)` |
| `GameStatic.cs`（新） | `game_static.cpp:135`（`GetHeroesRestoreSpellPointsPerDay`）、`:165`（`getObjectLuckEffect`）、`:183`（`getObjectMoraleEffect`）、`:283`（`getMovementPointBonus`） | 每日回蓝常数、运气/士气修正表、移动点加成表 |
| `HeroActions.cs`（新） | `heroes_action.cpp:795`（资源对象）、`:1109`（运气建筑）、`:1253`（魔法井）、`:1463`（士气建筑）、`:1611`（取宝）、`:2541`（泉眼）、`:2924`（石阵）、`:3313`（马厩）、`:3353`（竞技场）、`:3650`（屏障）、`:3681`（帐篷）、`:3702`（黑猫） | `ActionToMagicWell`、`ActionToArtesianSpring`、`ActionToGoodMoraleObject`、`ActionToGoodLuckObject`、`ActionToBlackCat`、`ActionToObjectResource`（风车/水车/小屋/魔法花园）、`ActionToStables`、`ActionToTravellersTent`、`ActionToObelisk`、`ActionToArena`、`ActionToBarrier`、`ActionToArtifact`（+ `ArtifactPickupOutcome`），以及 `IsGoodLuckObject`/`IsGoodMoraleObject`/`IsResourceObject` 三张清单 |
| `Tile.GetArtifactCaptureCondition`（新） | `maps_tiles_helper.cpp:623` | 从 `metadata[2]` 读神器守卫条件；非神器对象回 `NoConditions` |
| `Kingdoms.IsAnyVisited`（新） | `kingdom.cpp` 的 `isVisited(type)` 跨王国版 | 泉眼「全局只算一次」的查询 |
| `Kingdom.AddHero`（改） | `players.cpp` 里给英雄上色的那一句 | `hero.SetColor(_color)`；没有它英雄的 `Color` 停在 `None`，查王国会落到第七槽 |
| `Mp2ObjectTypes.IsDayLife`（改） | `mp2.cpp` 的 `isDayLifeObject` | 比较值从 `ActionMagicWell` 改成 `MagicWell` |
| `Visit.cs`（改） | `visit.cpp:22-40` | 三条谓词改为转发 `Mp2ObjectTypes` 的分类表，不再自己维护一份 |

要点：

* **`LOCAL` 与 `GLOBAL` 是两层，不是一个开关**：参考的 `isVisited(tile, type)` 按 `type` 决定问自己还是问王国；
  端口照抄，`SetVisited` 则是「`LOCAL` 记自己、`GLOBAL` 记王国」。这样魔法井（每天一次，记自己）和泉眼
  （每张图一次，记王国）可以共用同一份代码。
* **英雄的名册遇到重复不动位置**：`SetVisited` 只在「没记过」时插表头，已在表里就直接返回。王国那份是「先删后插」。
  这是参考的两段不同代码，测试分别钉住了（英雄 `[0]=Fountain`、`[1]=Windmill`，王国表头会翻新）。
* **`OBJ_NONE` 不记**：`SetVisited` 一进门就挡掉，否则清雾、路边空地的访问会把名册撑爆。
* **运气/士气是「重算」不是「累计」**：不存计数器，`GetLuck`/`GetMorale` 每次遍历访问名册的类型集合、查
  `GameStatic` 的两张表求和。所以「战时加成消失」只要删记录——`ActionAfterBattle` 删掉战斗寿命的对象，数值
  自己就回落了，不需要记「该减多少」。
* **按类型去重求和**：去过两座不同的喷泉仍然只 +1（参考的 `std::set<MapObjectType>`）。端口用 `Distinct()`。
* **每日回蓝「第一天不算」**：参考只在 `dayCount > 1` 时回蓝，且仅在移动点满时（`ActionNewDay` 开头就判
  `move_point < GetMaxMovePoints()` 直接返回）。端口照抄，测试钉住「第 1 天不回」。
* **神秘术按等级加回蓝**：`getDailyRestoredSpellPoints` 是「常数 1 + 神秘术等级」，端口在
  `GetDailyRestoredSpellPoints()` 里查 `SecSkills`。
* **泉眼「无论成败都登记」**：参考把 `GetKingdom().SetVisited(dst_index, GLOBAL)` 写在 `if` 外面——没补到法力
  也算「这周来过了」。端口照抄，测试用「满法力去一次、第二天再去仍然没补」钉住。
* **动作层不认英雄的王国**：端口没有 `hero.GetKingdom()`，`HeroActions` 收一个 `Kingdoms`，用 `hero.Color`
  查。这也是 `Kingdom.AddHero` 补 `SetColor` 的原因——色是空的，查表就会落到第七槽（测试当场抓到）。
* **`IsDayLife` 比的是不带动作位的值**：端口的瓦片存 MP2 原始值，动作位（+128）是运行时算的；参考的
  `OBJ_MAGIC_WELL` 本身就带动作位。第一版照抄常量，Assets 侧的分类测试立刻红了。
* **风车/水车「拿过没有」看瓦片，不看名册**：钱与资源存在瓦片的 `metadata` 里，`ActionToObjectResource` 取完就
  `ResetObjectMetadata`，第二次读到空的。`setVisitedForAllies` 那句照样登记进王国名册，但那只为界面/AI 提示用。
* **马厩是 400 不是 500**：参考里 `OBJ_STABLES` 与 `OBJ_WATERING_HOLE` 共用一个 `case`。马厩的另一半（在场骑兵免费
  升级）属于 `ActionToUpgradeArmyObject`，要费用与确认框，还没搬。
* **黑猫、帐篷不进同一条路**：黑猫记英雄本地名册（永久，三条清理都不碰），帐篷连名册都不进，只在王国上点一色位。
* **石阵只做转发**：英雄侧那一支只负责把「地图上石阵总数」喂给 `Kingdom.ActionObelisk`（ADR-121 已有登记与拼图
  重算），返回值 `false` 同时表示「已经看过」。
* **竞技场四选一提到参数上**：参考在动作里弹 `Dialog::SelectSkillFromArena()`；端口改成
  `ActionToArena(tileIndex, skill)`，界面先问再调。守卫那句「The Arena guards turn you away」就是返回 `false`。
* **屏障什么都不改**：参考 `ActionToBarrier` 只查「同色帐篷进过没有」然后说两句不同的话。端口折成
  `ActionToBarrier` 返回 `bool`，供移动/界面判定；真正放行仍在移动规则里。
* **取宝有三道门，顺序不能换**：已有法术书 → 满包 → 守卫条件；前两道都不动瓦片，宝物留在地上。端口在第三道
  只看「无条件/卷轴」两种，付费三种返回 `NeedsPayment`（等玩家掏钱再来），技能/战斗/阵营返回 `NotSupported`。

用例：新增 `HeroVisitTests` 10 条（名册最新在前且不挪位、去重集合、`None` 不记、每日回蓝、第 1 天不回、
不超上限、神秘术 +3、日/周/战后各自清理、王国替英雄 tick）与 `HeroActionsTests` 27 条（魔法井一次/满法力/次日再喝、
泉眼双倍与全局记录、绿洲 800/水坑 400/神殿 +2 无移动点、三座神殿仍 +2、喷泉 +1 运气、战后丢失、跨族对象无效、
两族清单；黑猫只算一次、风车付钱并清空瓦片、空水车仍登记王国、四类资源对象清单、马厩 400 与每周重置、
帐篷只解自己那一色且不进名册、石阵只揭一次、竞技场只练一次、屏障只认同色帐篷、免费取宝清空瓦片、
满包拒收且留宝、第二本法术书被拒、付费守卫等钱、技能守卫不冒充已实现）。`FHeroes2.Game.Tests` **1436**，
全解决方案 **2123** 条全绿，0 警告 0 错误。（本段初版记的 1491/2178 是计数笔误，两处以本节实测为准。）
覆盖率不变：`heroes.cpp`、`heroes_action.cpp`、`game_static.cpp`、`maps_tiles_helper.cpp` 早已在「部分」里，
合计仍 **34/63/152**。

## 英雄的经验、升级抽签与第一批发经验的对象 ✅（2026-11-16）

上一批铺的是「不花经验」的那一半动作，这一批补上经验这条主链：`Heroes::IncreaseExperience` 的连升、`_levelUp`
的两次抽签（主技能一点 + 副技能二选一），以及两个真正发经验的对象——凉亭（1000 点）与知识树（正好一级的经验，
可能收费）。升级链一通，后面所有「打赢给经验」「访问给经验」的分支都有了落点。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Hero.Experience.cs`（新） | `heroes.cpp:1482`（`IncreaseExperience`）、`:1848`（`_levelUp`）、`:1862`（副技能那一半）、`:2201`（`_getSeedsForLevelUp`） | `Experience`/`Level`/`SetExperience`/`IncreaseExperience`/`LevelUp`；等级由经验反查，连升时逐级压门槛 |
| `SkillLevelUp.cs`（新） | `skill.cpp:99`（`SecondaryPriorityFromRace`）、`:217`（`Primary::LevelUp`）、`:782`（`FindSkillsForLevelUp`） | 主技能加权抽签、副技能两候选（Expert 与满书黑名单、魔法职业每三级强制智慧、第二候选带第一候选的黑名单）、`IsMagicalRace` |
| `HeroActions.cs`（改） | `heroes_action.cpp:1521`（凉亭）、`:2946-3026`（知识树） | `ActionToExperienceObject`、`ActionToTreeKnowledge`（+ `TreeKnowledgeOutcome`）、`ExperienceOfCurrentLevel` |
| `Tile.GetTreeOfKnowledgeRequirement`（新） | `maps_tiles_helper.cpp:697` | `metadata[0]` 资源 + `metadata[1]` 数量 → `Funds`；零数量即免费 |

要点：

* **等级不存，反查**：参考的存档里只有经验，`GetLevel()` 每次查阈值表。端口照抄，`Level` 是只读属性，`SetExperience`
  改经验、等级自动跟着走。
* **连升三级要抽三次，不是抽一次乘三**：参考先把经验压到每一级的门槛上逐级 `_levelUp`，最后写回真实经验。
  这样每一级的种子都基于「当时的等级与技能」，否则连升的几级会拿到同一套牌。
* **种子是「地图种子 × 英雄状态」的哈希**：参考 `_getSeedsForLevelUp` 把地图种子、英雄 id、种族、四项主技能和
  **十四个**副技能等级（含没学的）混进去，再 +0/+1/+2/+3 得到四个种子。少了任何一项，「重开同图」就复现不了，
  或者每级都抽到同一个技能。端口逐项照抄。
* **「谁选副技能」抽成委托**：参考有人类弹窗、AI 决策、`autoselect` 三条路；端口 `IncreaseExperience(amount, mapSeed,
  chooser)`，`chooser` 为 `null` 时就是 `autoselect`（种子掷一次）。界面接上后传自己的函数即可，升级算法不用再改。
* **主技能从不问人**：参考也只按权重抽一点加上去，没有「选攻击还是防御」这回事。
* **知识树给的是「本级经验」，不是固定值**：`FromLevel(level) - FromLevel(level - 1)`，所以永远刚好推上一级、
  且只推一级。参考的 `GetExperienceFromLevel` 表首项是 0，`level - 1` 在 1 级时取到 0 也成立。
* **知识树写两份名册，顺序不能换**：先**无条件**记王国（参考的 `SetVisited(dst_index, Visit::GLOBAL)`，付费树
  也是先登记再说），真的加经验时才记英雄本地。于是「钱不够」的英雄下次还能来，「学过了」的那次只看本地。
* **凉亭记瓦片级名册**：地图上多个凉亭各给一次 1000 点，不是「同类型只算一次」。
* **侦察术学成后开图那段没搬**：它要雾与雷达，属于迷雾系统；升级本身不依赖它。

用例：新增 `HeroExperienceTests` 10 条（等级按阈值反查、知识树正好给一级经验且第二次不给、付费树扣钱、钱不够
不扣不加且只记王国、凉亭每片瓦片一次、升级加一点主技能与一项副技能、连升两级抽两次、同种子同状态可复现、
选择器决定拿哪一张、四个魔法职业）。`FHeroes2.Game.Tests` → **1446**，全解决方案 → **2133** 条全绿，0 警告 0 错误。
覆盖率不变：`heroes.cpp`、`skill.cpp`、`heroes_action.cpp`、`maps_tiles_helper.cpp` 早已在「部分」里，合计仍
**34/63/152**。

## 神殿与女巫小屋：给法术、给技能 ✅（2026-11-16）

经验链通了以后，「给法术」与「给技能」这两簇就能落地了。它们的动作本身很短，难的是三处语义：没有法术书、
没有智慧术却碰上三级法术、八个副技能槽已满；以及一条容易写错的规矩——**名册登记在所有分支之外**。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `TileMetadata.GetSpellFromTile`（新） | `maps_tiles_helper.cpp:388` | 三个圆环的神殿与金字塔共用一个车道：`metadata[0]` 是法术编号，与 `Spell` 同序 |
| `TileMetadata.GetSecondarySkillFromWitchsHut`（新） | `maps_tiles_helper.cpp:708` | `metadata[0]` 是技能编号，等级固定为基础 |
| `Hero.SecondarySkills.cs`（新） | `heroes.cpp:1722`（`HasSecondarySkill`）、`:1732`（`HasMaxSecondarySkill`）、`:1737`（`GetLevelSkill`）、`:1742`（`LearnSkill`） | 三个查询与一个学习；都是对 `SecSkills` 的一行转发，独立出来是为了「英雄会不会这门技能」这层语义 |
| `HeroActions.ActionToShrine`（新） | `heroes_action.cpp:995-1056` | 四道门：无书 / 三级法术无智慧 / 书里已有 / 抄进书；`ShrineOutcome` |
| `HeroActions.ActionToWitchsHut`（新） | `heroes_action.cpp:1058-1107` | 三道门：满八槽 / 已会 / 按基础等级学会；`WitchsHutOutcome` |

要点：

* **智慧术只挡三级法术**：参考的条件是 `3 == spellLevel && GetLevelSkill( WISDOM ) == NONE`。一二级不看，四五级
  的金字塔根本不走这条（它走带守卫的 `ActionToPyramid`，`heroes_action.cpp:3902`）。
* **查书时 `skipBag = true`**：`HaveSpell( id, true )` 只查法术书。若查背包，一张同法术的卷轴会让神殿误判「你已经
  会了」——而卷轴本身并不在书里。
* **名册写在所有分支之外**：参考的 `SetVisited( dst_index, Visit::GLOBAL )` 在 if/else 链结束之后无条件执行，端口
  照抄。于是「没书」「听不懂」「已会」这三次访问都会被王国记住，人类英雄再来一次照样看同一段对话——判定靠
  「书里有没有」，名册只服务 AI 与界面。这一点与知识树恰好相反（知识树靠名册挡重复访问）。
* **两支都不登记英雄本地**：只写 GLOBAL，`Hero.IsVisited` 保持干净。凉亭/知识树是「瓦片级一次」，这两支是
  「每次都能进、但学会了就不再给」，三种语义各有各的落点。
* **满槽是 8 而不是 14**：`HasMaxSecondarySkill` 比的是 `SecSkills.Count` 与 `MaxCount`（8 个槽），不是会了几种技能。
  `LearnSkill` 自己不挡第九个——参考也不挡，满槽检查是调用方的事。
* **侦察术开图没搬**：参考学会 Scouting 时顺手 `Scout`+`ScoutRadar`，端口没有迷雾，技能照常记下。

用例：新增 `HeroShrineTests` 10 条（无书不教但王国仍记账、写入书里、三级法术无智慧被拒、有智慧就学会、同一法术
不教第二次、只记王国不记英雄、小屋教基础技能、已会不重复、满八槽拒收、小屋也只记王国）。`FHeroes2.Game.Tests` →
**1456**，全解决方案 → **2143** 条全绿，0 警告 0 错误。覆盖率：`maps_tiles_helper.cpp`、`heroes_action.cpp`、
`heroes.cpp` 早已在「部分」里，合计仍 **34/63/152**。

## 宝箱与仙那度：二选一，以及「够不够格」 ✅（2026-11-16）

先纠正上一轮的一处错误：fheroes2 里**没有「学者」这个对象**（那是《英雄无敌 3》的 Scholar，两边的
`MP2::MapObjectType` 与 `Mp2ObjectType` 都查不到）。所以这一轮改做宝箱与仙那度——两者都不需要战斗或军队。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `ArtifactGold.InsteadOf`（新） | `artifact.cpp:1066-1083` | 六种对象在背包满时把神器折成的金币，硬编码：骷髅/宝箱/沉船幸存者 1000、海上宝箱 1500、墓地 2000、沉船 5000 |
| `GameStatic.IsHeroWorthyToVisitXanadu`（新） | `game_static.cpp:298-308` | 十级以上放行；否则外交术基础/高级/专家分别要 8/6/4 级以上 |
| `HeroActions.ActionToTreasureChest`（新） | `heroes_action.cpp:1795-1910` | 三条分支 + `TreasureChestReward` |
| `HeroActions.ActionToXanadu`（新） | `heroes_action.cpp:2577-2617` | 三道门 + `XanaduOutcome` |

要点：

* **宝箱的判断顺序本身就是语义**：先看是否在水上（==只能拿钱，或钱+神器，没有二选一），再看「有钱」或「有神器
  但背包满」，最后才轮到「只有神器」与空箱。把顺序调换会让一个空箱子白送一次经验选择。
* **经验式子照抄**：`gold > 500 ? gold - 500 : 500`。拿得越多换经验越亏，且最低保底 500——这不是在算「经验
  价值」，而是原版压高额宝箱收益的手段，不「优化」。
* **背包满时折金是覆盖而不是叠加**：`gold = GoldInsteadArtifact( objectType )`，箱子自带的 800 与神器的 1000
  之间只留 1000。看着像 bug，是原版行为，ADR 里留了记录。
* **宝箱不进访问名册**：参考的 `ActionToTreasureChest` 只 `resetObjectMetadata`，没有 `SetVisited`。箱子开完就
  没了，「开过没有」靠瓦片空不空来答。空箱也一样清——`resetObjectMetadata` 写在函数最末尾。
* **选择交给调用方**：`keepGold` 委托吃（金币, 经验）、回是否拿金币，`null` 表示拿金币。AI 侧那套「按角色 +
  国库算概率」的逻辑（`ai_hero_action.cpp:751` 起）等 AI 落地再接；端口现在不假装知道。
* **仙那度的门槛是两条路**：等级 > 9 直接进；否则看外交术等级决定门槛（严格大于 7/5/3）。没有外交术就永远
  进不去，等级再高也不行——这是唯一一个「技能决定能不能进门」的对象。
* **不够格不算一次访问**：`SetVisited` 只在受训分支里。记的是**英雄本地**名册（参考的 `hero.SetVisited( dst_index )`
  是 LOCAL），因为管家「不认同一个学生两次」。这与神殿/女巫小屋「怎么都记王国」、知识树「先记王国后记本地」
  正好凑成三种名册语义。
* **四主技能固定各加一点**，不抽签、不看职业。

用例：新增 `HeroTreasureTests` 13 条（默认拿钱、改为拿经验、小额宝箱保底 500、开完清元数据、满包折金覆盖原金币、
空箱什么都不给、陆地纯神器、海上钱+神器、海上满包只给折金、仙那度受训四维各 +1、不够格不记名册、不教第二次、
外交术降门槛的边界 7/8 级）。`FHeroes2.Game.Tests` → **1469**，全解决方案 → **2156** 条全绿，0 警告 0 错误。
覆盖率：`artifact.cpp`、`game_static.cpp`、`heroes_action.cpp` 早已在「部分」里，合计仍 **34/63/152**。

## 「钱或神器」一簇：营火、骷髅、货车、浮木、沉船幸存者 ✅（2026-11-16）

上一轮说的「同形对象一次做完」，这一轮做了。五个动作对象，共同点是产物只有「一袋资源或一件神器」，车道读取
（`Tile.GetFundsFromTile`、`Tile.GetArtifactFromTile`、`Tile.DoesTileContainValuableItems`）此前就已具备，
所以它们全是短函数，难点在四件事都不一样的**收尾规则**。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `HeroActions.ActionToPickupResource`（新） | `heroes_action.cpp:752-793` | 散落资源/营火/桶：拿钱与资源、清元数据、<b>不记名册</b> |
| `HeroActions.IsPickupResource`（新） | 同上 | 上面三个对象的判定；漂流瓶不在内 |
| `HeroActions.ActionToSkeleton`（新） | `heroes_action.cpp:866-909` | 有神器给神器、满包折 1000 金币；清元数据；<b>记名册</b> |
| `HeroActions.ActionToWagon`（新） | `heroes_action.cpp:911-962` | 有神器给神器、否则给货物资源；满包<b>什么都不给</b>；清元数据；记名册 |
| `HeroActions.ActionToFlotsam`（新） | `heroes_action.cpp:964-993` | 木材 + 金币；清元数据；不记名册 |
| `HeroActions.ActionToShipwreckSurvivor`（新） | `heroes_action.cpp:1570-1609` | 有神器给神器、满包折 1000 金币；无条件清元数据；不记名册 |
| `ObjectPickupReward`（新） | — | `Funds` + `ArtifactType`，`IsEmpty` 判「什么都没给」 |

要点：

* **四支的收尾规则互不相同**，这是本轮最容易写错的地方，逐一记：

  | 对象 | 满包怎么办 | 清元数据 | 记名册 |
  | --- | --- | --- | --- |
  | 营火/桶/散落资源 | 无关（没有神器） | 总是 | 否 |
  | 骷髅 | 折 1000 金币 | 只在「有价值」分支里 | 总是 |
  | 货车 | **什么都不给**，车厢直接清空 | 只在「有价值」分支里 | 总是 |
  | 浮木 | 无关 | 总是 | 否 |
  | 沉船幸存者 | 折 1000 金币 | 总是 | 否 |

* **货车是唯一不给折金的**：骷髅、宝箱、沉船幸存者都会把神器换成 1000 金币，货车只弹一句「别人先找到了」然后
  照样清空。所以满包开货车等于白扔一件神器。原版如此，照抄并在文档里写明，免得被人当 bug「修好」。
* **货车先看神器再看货物**：`metadata[0]` 是神器、`metadata[1]` 是资源类型、`metadata[2]` 是数量。神器车道非空
  时资源<b>不另算</b>——不是「两样都给」。
* **营火/桶的换算写在车道里**：`N` 个某种资源 + `N × 100` 金币，`GetFundsFromTile` 一次算完，动作层不重算。
  资源类型可以是金币本身，此时 `Funds(type, 0)` 不产生任何项，空桶就是空袋。
* **骷髅与货车「清元数据」在价值分支内**：空骷髅/空车厢不重写车道（本来就是 0），但仍然记一次名册访问；
  而浮木与沉船幸存者是<b>无条件清</b>。这四行差异都是参考的 `resetObjectMetadata` 写在哪儿决定的。
* **漂流瓶没进来**：参考在 `ActionToPickupResource` 里给它留了一个分支，只弹留言、不给钱。端口没有留言文本
  系统，`IsPickupResource` 明确不收它，等留言系统落地再接。
* 结果类型用 `Funds` 而不是金币数：货车与骷髅都可能给木材、水晶这类非金币资源。

用例：新增 `HeroPickupTests` 14 条（散落资源进国库、营火按 N×100 折金、桶同营火、拾取不记名册、骷髅给神器、
骷髅满包折 1000、空骷髅只记名册、货车优先神器、货车无神器给货物、货车满包什么都不给、浮木给木材+金币、
空浮木什么都没有、沉船幸存者给神器、满包折 1000）。`FHeroes2.Game.Tests` → **1483**，全解决方案 → **2170**
条全绿，0 警告 0 错误。覆盖率：`heroes_action.cpp` 早已在「部分」里，合计仍 **34/63/152**。

## 监牢与英雄入伙：`ActionToJail` + `Hero.Recruit` ✅（2026-11-16）

监牢是动作层里第一个**要把一位英雄交给王国**的对象，所以它顺手补上了「英雄入伙」这条链：找囚犯、查名额、
上色、站到瓦片、挂进名册。四个新成员，三层。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `HeroActions.ActionToJail`（新） | `heroes_action.cpp:3402-3434` | 满编 → `NoSeat`；牢里没人 → `NoPrisoner`；否则放人 |
| `JailOutcome`（新） | — | `None`/`Released`/`NoSeat`/`NoPrisoner` |
| `Hero.Recruit`（新，`Hero.Recruit.cs`） | `heroes.cpp:1105-1150` | 清 `Recruit`/`Jail` 位、上色、定位、给行动力、补空军队、占瓦片、进名册 |
| `AllHeroes.FromJail`（新） | `heroes.cpp:2475-2486` | 同时要求 `Jail` 模式位与「位置正好是这个瓦片」 |
| `World.FromJailHeroes`（新） | `world.h:340` | 一行转给 `AllHeroes.FromJail` |
| `Kingdom.AllowRecruitHero`（新） | `kingdom.cpp:499-502` | `英雄数 < 8`，按需再查酒馆价 `PaymentConditions.RecruitHero()` |

要点：

* **判定顺序是先名额、后囚犯**：参考在函数开头就问 `AllowRecruitHero`，所以满编的国王连「这块地有没有囚犯」
  都不查，直接看「你已经有 8 位英雄了」。端口照抄，并专门写了一条用例钉住这个顺序。
* **`AllowRecruitHero` 与 `CanRecruitHeroes` 不是一回事**：前者问「名额/钱够不够」，后者问「有没有城能出兵」。
  名字像，判据完全不同，文档里明确写了别互替。
* **监牢不查钱**：`AllowRecruitHero( false )`，囚犯免费；酒馆那条路才查 `PaymentConditions.RecruitHero()`。
* **不清元数据、不记名册**：参考里没有这两步，端口也没有。监狱靠 `HeroModes.Jail` 回答「还有人吗」——
  放出来的人模式位已清，再来自然 `NoPrisoner`。监狱**物件本身留在瓦片上**：英雄站上去时它被 `Tile.SetHero`
  寄存到英雄身上，英雄一走，空牢重新露出来。
* **`Hero.Recruit` 收王国而不是颜色**：参考是 `Recruit( PlayerColor, Point )` 内部再 `world.GetKingdom( col )`，
  端口没有 `World.GetKingdom`（王国在 `Kingdoms` 里，ADR-122），所以直接把王国递进去，颜色由
  `kingdom.Color` 回答，不存在「颜色与名册不一致」的中间态。
* **行动力给满**：参考只在 `!Modes( SAVEMP )` 时给，端口没有 `SAVEMP` 位，等价于永远给满——刚出狱的英雄
  本来就该有整天的行动力。空军队时 `GetMaxMovePoints` 返回 0，这与参考一致（参考也是先给行动力再补军队）。
* **军队只在无效时才补**：`!_army.isValid()` 才 `Reset`，监狱里的英雄携带的兵不会被抹掉。
* **没搬的三步**：`setAlphaValue( 255 )`（端口没有英雄透明值）、`kingdom.GetRecruits()` 的刷新（ADR-118）、
  `Scout`/`ScoutRadar`（迷雾与雷达未搬）。

用例：新增 `HeroJailTests` 10 条（有空位就放人、放到监狱瓦片上、放完再也找不到囚犯、放出来是 active、
朝右站、满编不放人、空牢无人可放、满编优先于空牢、`AllowRecruitHero` 数名额、只在要求时查酒馆价）。
`FHeroes2.Game.Tests` → **1493**，全解决方案 → **2180** 条全绿，0 警告 0 错误。覆盖率：`heroes_action.cpp`
已在「部分」里，合计仍 **34/63/152**。

## 塞伦与告示牌：`ActionToSirens` + `ActionToSign` ✅（2026-11-16）

上一节的「下一步」列了两支**不需要新前置**的对象：塞伦（军队已经在）与告示牌（留言早就在
`World.GetMapSign` 里）。这一轮把这两支都做了。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Army.ActionToSirens`（新） | `army.cpp:1542-1571` | 每栈去掉 30%、至少 1 个、独苗不动；返回「淹死的兵值多少经验」 |
| `HeroActions.ActionToSirens`（新） | `heroes_action.cpp:3366-3400` | 查英雄级名册、调军队、加经验、记名册 |
| `SirensReward`（新） | — | `Experience` + `AlreadyVisited` |
| `HeroActions.ActionToSign`（新） | `heroes_action.cpp:1243-1251` | 取 `World.GetMapSign(...).Message`，不改任何状态 |

要点：

* **塞伦的三条军队规则**：空栈跳过、**只有 1 个的栈完全不动**、其余去 30%（向下取整但**至少 1 个**）。所以
  3 个兵的栈会掉 1 个而不是 0 个；这一条单独写了一条用例。
* **经验按血量算，不按金币或战斗力**：`去掉的数量 × 单体生命值`，10 个弓箭手掉 3 个（每个 10 血）= 30 点。
  全部是营养公式，没有随机数，所以结果可以逐位断言。
* **分工照抄参考**：军队层只管「兵少了、值多少经验」，名册与加经验都在动作层。要塞进 `IncreaseExperience` 就需要
  地图种子，所以动作层这次多收一个 `World` 参数（与 `ActionToExperienceObject` 一致）。
* **名册是英雄级的，而且「没掉兵」也记**：参考用 `hero.SetVisited( dst_index )`（不是王国级），所以同一个英雄
  听过一次就够，换英雄仍可再听；`SetVisited` 在 `else` 末尾，兵太少一个都没掉时**同样登记**。于是
  `SirensReward` 必须分开两个字段：`IsEmpty`（什么都没掉）与 `AlreadyVisited`（来过了），调用方据此选文案。
* **告示牌不改状态**：参考里它只是「取文本 + 弹窗」，不记名册、不动元数据、不加经验，所以可以反复读。端口把它
  折成返回字符串——留言本来就在 `MapSign` 里，动作层只负责把人领过去；标题与弹窗留给 UI。
* **空牌子不编内容**：原版会给空留言的牌子随机抽一句话，端口没有那张语言表，如实返回空字符串。
* **塞伦不再是「等军队」的欠账**：军队系统早已具备（`Army`/`Troop` 完整），此前把它列在「要前置」里是我判断
  错了——真正缺的只是「谁少兵、值多少经验」这条换算，参考里就在 `army.cpp`。

用例：新增 `HeroSirensTests` 7 条（30% 减员与经验、3 个兵仍掉 1 个、独苗不动、混编按血量合计 31 点、空军队
无人可掉、没掉兵也记名册、第二次来兵不动）与 `HeroSignTests` 4 条（读出留言、无牌返 null、可反复读、空牌
仍为空）。`FHeroes2.Game.Tests` → **1504**，全解决方案 → **2191** 条全绿，0 警告 0 错误。覆盖率：
`heroes_action.cpp` 已在「部分」里，合计仍 **34/63/152**。

## 迷雾入世界：`World.Fog` + 观察塔 + 麦哲伦地图 ✅（2026-11-16）

上一节的「下一步」说得很清楚：剩下两支不需要战斗的对象（`ActionToObservationTower`、
`ActionToMagellanMaps`）卡在同一件事上——**迷雾此前不是世界状态**。这一轮先把迷雾搬进 `World`，两支对象
随之落地。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `World.Fog`（新） | `Maps::Tile::_fogColors` | 按需创建、尺寸随地图、`Reset` 丢弃 |
| `World.ClearFog`（新） | `maps.cpp:464-500` | 半径 ≤ 0 / 越界什么都不做；揭开圆并重算方向 |
| `World.ActionForMagellanMaps`（新） | `world.cpp:906-922` | 逐块看，是水就揭 |
| `MapFog.Reveal`（改） | `maps.cpp:480` | 返回<b>真正被揭开的瓦片数</b>（原来返回 void） |
| `MapFog.ObservationTowerRevealDistance`（新） | `game_static.cpp:119` | 19 格 |
| `MapFog.MagiEyesRevealDistance`（新） | `game_static.cpp:122` | 9 格（留给法师之眼） |
| `HeroActions.ActionToObservationTower`（新） | `heroes_action.cpp:2521-2539` | 以塔为心揭 19 格，返回揭开数 |
| `HeroActions.ActionToMagellanMaps`（新） | `heroes_action.cpp:2759-2809` | 四道门：买过了 / 钱不够 / 要确认 / 成交 |
| `MagellansMapsOutcome`（新） | — | `None`/`AlreadyBought`/`NotEnoughGold`/`NeedsConfirmation`/`Bought` |

要点：

* **迷雾进 `World` 是按需创建的**：参考把它塞在每块瓦片的元数据里（`_fogColors`），端口本来就有独立的
  `MapFog`（每格一个颜色掩码 + 一份方向缓存），于是 `World.Fog` 只是给它一个归宿——没有地图时返回
  `null`，有尺寸时按 `Width`×`Height` 建全黑的表，`Reset` 里丢掉。这样两个加载器（`GenerateUninitializedMap`
  与 `LoadMap`）都不必记得「同时建迷雾」，也不会出现「地图换了迷雾还是旧的」。
* **`MapFog.Reveal` 改返回数量**：原来返回 `void`，现在返回「这次真正掉了迷雾的瓦片数」（已经可见的不算）。
  这是唯一能既让观察塔/麦哲伦地图报告战果、又让测试钉住「第二次爬塔揭不出东西」的写法；语句式调用不受影响。
* **半径 → 方形距离不是平方**：`SquaredRevealLimit` 里半径 ≥ 6 时用 `(distance+1)²`，所以 19 格对应的是
  400 而不是 361。18 格与 19 格揭出来的圆<b>一样大</b>，参考本来就这样，测试按同一公式算期望值。
* **`ClearFog` 的三道早退照抄**：半径 ≤ 0、颜色为空、瓦片号越界——参考在这些情况下直接 `return`，端口返回 0。
* **只揭自己的颜色**：参考揭 `Players::GetPlayerFriends( color )`（盟友一起看见），端口没有联盟，
  `Hero.IsFriends` 是那个缺口唯一的一处，注释里写明了等联盟落地再改。
* **麦哲伦地图是两段式**：参考在同一次调用里弹「是/否」再结算，端口把确认留给调用方——带
  `confirmed: false` 只会拿到 `NeedsConfirmation`，一分钱不动；玩家点「是」再带 `true` 回来才扣钱开图。
  与 `ArtifactPickupOutcome.NeedsPayment` 同一套做法，所以「没答应就扣钱」不可能发生。
* **「买过了」排在「钱不够」前面**：既买过又只剩几块钱的王国应当被告知「你已经把海图拿走了」，而不是
  「你钱不够」。参考就是这个顺序，专写一条用例钉住。
* **「买过了」是王国级的**：参考用 `Visit::GLOBAL` + `setVisitedForAllies`，端口用 `Kingdom.IsVisited` 这张
  王国名册，而且按<b>类型</b>问——地图上有第二个船长也照样挡住。
* **当时还没搬的两块**（**都已在后续两轮补上**）：开局侦察（`AllHeroes::Scout`，以英雄为心开一圈）没搬，
  所以地图一加载就是全黑，只有观察塔与麦哲伦地图能揭开——这正是「侦察术开图」那笔欠账，ADR-130 已补；
  `AdventureMap` 的场景预览图（`MapFog.CreateForMap`）没接 `World.Fog`，ADR-131 已接并且把该助手删掉。

用例：新增 `HeroFogTests` 13 条（迷雾随地图生灭、19 格圆的期望数量、只揭给定颜色、二次揭为 0、
半径/越界早退、观察塔揭自己颜色、可爬两次、麦哲伦只揭水、船长先问价、扣 1000 金、买过不重复扣、
穷国买不起、买过优先于没钱）。`FHeroes2.Game.Tests` → **1517**，全解决方案 → **2204** 条全绿，0 警告
0 错误。覆盖率：`heroes_action.cpp` 已在「部分」里，合计仍 **34/63/152**。

## 开局侦察：`GetScoutingDistance` + 三家 `Scout` ✅（2026-11-16）

上一节的「下一步」说迷雾这条线最自然的一步是「`AllHeroes::Scout` 搬出来」。这一轮把它和它的三个同伴一起搬了：
英雄看多远、英雄开一圈、城堡开一圈、一个玩家的全部家当开一圈，外加把「走路开图」接上。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Hero.GetScoutingDistance`（新） | `heroes.cpp:1768-1772` | 神器加成 + 基础 4 + 侦察术数值（1/2/3） |
| `Hero.Scout`（新） | `heroes.cpp:1749-1766` | 以给定瓦片为心揭 `GetScoutingDistance()` 格，用自己的颜色 |
| `AllHeroes.Scout`（新） | `heroes.cpp:2435-2446` | 按颜色筛英雄，各自以脚下为心 |
| `Castle.Scout`（新） | `castle.cpp:2213-2216` | 以城镇为心揭 5 格，用主人的颜色 |
| `AllCastles.Scout`（新） | `castle.cpp:2380-2391` | 按颜色筛城镇 |
| `World.ClearFog(PlayerColor)`（新） | `world.cpp:838-848` | 城堡 + 英雄（占领对象还没有迷雾位） |
| `FogHeroMovementHandler`（新） | `Heroes::Scout` 的移动侧 | 第一个真实的 `IHeroMovementHandler`，走路即开图 |

要点：

* **距离是三项之和，第三项是数值不是等级**：`GetScoutingDistance()` = 神器 `AREA_REVEAL_DISTANCE` +
  `MapFog.HeroRevealDistance`(4) + `GetSecondarySkillValue( Scouting )`(1/2/3)。所以基础 4 格，专家侦察术 7 格，
  再加望远镜 8 格。参考在这里<b>不做上限裁剪</b>，端口也不做。
* **侦察术给的是「数值」**：基础 1／高级 2／专家 3，来自 `secondarySkillValuesPerLevel` 那一行，不是
  「等级 = 1/2/3」的巧合——两者恰好相等，但代码里问的是数值，将来表变了也不会错。
* **不是「有多少级就加多少」**：没有技能时 `GetSecondarySkillValue` 返回 0，不会退化成「基础 +1」。
* **英雄级名册不参与**：侦察<b>不看</b>`IsObjectTypeVisited`，反复走、反复开都是允许的（开图本身不改任何
  状态，只是把迷雾位清掉）。
* **`Hero.Scout` 的瓦片由调用方给**：走路时是「即将踏入」的那一格（参考 `_moveStep()`），开局侦察是脚下那一格。
  所以它收 `tileIndex` 而不是自己读 `MapIndex`。
* **中立英雄（`PlayerColor.None`）什么都不揭**：参考 `assert`，端口返回 0。
* **按颜色筛，不按别的状态**：监狱里的英雄只要还挂着颜色也会开图——参考如此，端口照抄，不去替它做决定。
* **`Castle` 的半径比英雄多一圈**（5 对 4），没有主人的城镇永远筛不进来，所以开局只有英雄与自己的城镇亮着。
* **`World.ClearFog( PlayerColor )` 是三件事里的两件**：参考还有 `map_captureobj.ClearFog`（被占领的矿井/
  资源点对盟友可见），端口的 `CapturedObject` 只记颜色与类型、没有迷雾位，留给「占领对象带颜色表」那一轮。
* **`FogHeroMovementHandler` 是第一个真实的移动处理器**：迷雾落地之前四个回调全是空的
  （`NullHeroMovementHandler`），现在 `Scout` 有东西可做了——它只做这一件，另三个（雷达、对象动作、AI）
  仍空着，理由写在接口注释里。它持有世界，所以游戏循环造出世界后为每位英雄装上即可。
* **与 `ClearFog` 重载的关系**：`ClearFog( tileIndex, distance, color )` 揭一个圆（`maps.cpp`），
  `ClearFog( color )` 揭一方的全部家当（`world.cpp`），参考里在两个文件，端口放在一起因为都只改同一张迷雾表。

用例：新增 `HeroScoutTests` 12 条（基础 4 格、侦察术基础 5/专家 7、望远镜 5、揭圆且只揭自己颜色、专家 + 望远镜
到 8 格、无颜色不揭、英雄按颜色筛且不替对手开图、城镇按主人筛且 5 格、无主城镇不揭、全世界侦察等于城堡+英雄
之和、`ClearFog(None)` 为 0、处理器把「即将踏入」那格揭掉）。`FHeroes2.Game.Tests` → **1529**，全解决方案 →
**2216** 条全绿，0 警告 0 错误。覆盖率：`heroes.cpp` 已在「部分」里，合计仍 **34/63/152**。

## 场景接上世界迷雾：一套迷雾、一个生产者 ✅（2026-11-16）

上一节的「下一步」说差的只是**装配**。这一轮把装配做完了：`AdventureMapScene` 不再自己按 `Mp2Map` 造一份
迷雾，而是把地图装进 `World.Current`，画的就是 `World.Fog`；`MapFog.CreateForMap` 因此失去唯一调用者，删掉。
于是端口的迷雾只剩一条路径：**世界持有，规则改它，渲染器读它**。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `AdventureMapScene.TryCreate`（改） | `game_startgame.cpp:769` | `World.Current.LoadMap( map )` 之后调 `ClearFog( observers )`，画 `World.Fog` |
| `AdventureMapScene.ScoutStart`（新，替 `CreateFog`） | `world.cpp:838` | 人类颜色的英雄圆盘 + 聚焦城堡的占位圆盘，返回揭开格数 |
| `World.ClearFog(PlayerColor)`（改） | `maps.cpp:updateDirectionsInArea` | 末尾补一次 `Fog?.UpdateDirections( color )`，零揭开时方向掩码也在 |
| `MapFog.CreateForMap`（**删**） | 无（端口自造） | 存在的唯一理由是场景手里没有 `World`，现在有了 |
| `MapFog.HeroRevealDistance`（注释改） | `GameStatic::getFogDiscoveryDistance` | 说明它只是**基数**，加成才在 `Hero.GetScoutingDistance` 里 |

要点：

* **顺序照参考**：`LoadMap` → `ClearFog( colors )` → 交给渲染。参考在 `game_startgame.cpp` 里就是这么排的，
  所以「世界建好但全黑」这个中间态只在 `LoadMap` 与 `ClearFog` 之间**一瞬**存在，不再是场景的常态。
* **顺手修掉一个真 bug**：`ClearFog( PlayerColor )` 只经 `_castles.Scout` / `_heroes.Scout` 揭开，
  **零揭开时不更新方向掩码**（`MapFog` 的渲染读的是掩码，不是迷雾字节）。一张有人类颜色却没有英雄也没有
  城镇的地图，此前会得到「全黑的表 + 全零的掩码」= 一格雾都不画，整张地图明晃晃。现在末尾统一补一次
  `UpdateDirections`，这条边界有测试钉住（`AMapWithoutAHeroOfThatColorStaysEntirelyUnderTheFog`）。
* **颜色来自精灵**：`world_loadmap.cpp` 按 `MINIHERO` 帧号（每色 7 帧）给英雄定色，所以「地图文件里的英雄
  有没有颜色」是装配能不能成立的关键，测试直接断言 `world.GetTile(...).GetHero(...).Color` 是 Blue / Red。
* **聚焦城堡仍是占位**：MP2 城堡无归属，`AllCastles::Scout` 筛不出来，所以场景按半径 5 手动揭一次
  （`ClearFog( tileIndex, MapFog.CastleRevealDistance, observers )`）。注释里写明了「等地图自带归属时删掉这段」。
* **无人类玩家仍是 `null` 迷雾**：给 `AdventureMapView` 传 `null` 而不是一张全黑的表，语义与原来一致。
* **世界挂在 `World.Current` 上**：场景不 new 一个私有世界（`World` 的构造函数是 internal，本来就为「游戏里
  只有一个世界」而设），于是后面搬移动、战斗时拿到的就是这份状态。
* **像素基线没变**：`VerifyAdventureMapBaseline.ps1` 对 `Alteris.mp2` 渲染出的 640×480 帧与
  `tests/baselines/adventure-map.png` **逐字节一致**——换掉迷雾的生产者没有动到任何一个像素，这是「装配只
  换主人、不改行为」最直接的证据。

用例：新增 `WorldFogOnLoadTests` 5 条（装载后全黑、开局侦察按人类颜色揭英雄且只揭该色、别色英雄不揭、
无该色英雄时整图仍在雾下且掩码就绪、装载第二张图丢掉第一张的雾）；`MapFogTests` 删掉针对已移除助手的 2 条。
`FHeroes2.Game.Tests` → **1532**，全解决方案 → **2219** 条全绿，0 警告 0 错误。覆盖率合计仍 **34/63/152**。

**下一步**：「一套迷雾」这件事到这儿闭合了（造、改、读都经由 `World`）。剩下的都回到**前置系统**：带守卫的
金字塔/沉船/龙穴与矿井（战斗）、住所/巢穴/流浪营地（军队与城堡招募）、`ActionToPoorMoraleObject`（战斗）、
传送门与漩涡（移动，需要 `FogHeroMovementHandler` 真正被走路调用）、`ActionToSphinx`（答题 + 战斗）、
`ActionToOracle`（盗贼公会界面）。两处欠账不变：`LoadFromMP2` 按经验补等级、付费取宝的图标索引映射
（`getResourceTypeFromIconIndex`）。

## 传送门与漩涡：第三种位移，以及漩涡吞掉的半叠兵 ✅（2026-11-16）

英雄此前只有「走过去」一种位移，`MoveToDest` 是为传送留的挂点却没人用。这一轮把 `heroes_action.cpp` 的两个
传送对象接上：**立石**（`ActionToTeleports`）与**漩涡**（`ActionToWhirlpools`），后者顺手把沉兵一起搬了。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `HeroActions.ActionToTeleports`（新） | `heroes_action.cpp:1940-1979` | 找出口 → `Scout` 出口 → `MoveToDest` → 清路线；返回 `TeleportOutcome` |
| `HeroActions.ActionToWhirlpools`（新） | `heroes_action.cpp:1981-2020` | 同上，外加沉兵；收可选 `Pcg32` 让测试钉住骰子 |
| `HeroActions.MoveThroughTeleport`（新，私有） | 同上两段 | 两个对象共用的三步位移，顺序照参考 |
| `HeroActions.LoseTroopsToWhirlpool`（新，私有） | `WhirlpoolTroopLoseEffect`，`heroes_action.cpp:300-340` | 最弱一叠：三分之一概率丢一半**血量**；四个早退 |
| `Troop.GetCountFromHitPoints`（新，静态） | `Monster::GetCountFromHitPoints`，`monster.cpp:840` | 血量 → 只数，**向上取整** |
| `GameStatic.LostOnWhirlpoolPercent`（新） | `GameStatic::GetLostOnWhirlpoolPercent`，`game_static.cpp:107` | 50 |
| `TeleportOutcome`（新枚举） | 无（参考返回 void） | `NoDestination` / `Moved` |

要点：

* **三步的顺序不能动**：`Scout( 出口 )` → `MoveToDest` → `ResetPath()`。先 Scout 是因为雾要按**新**位置算，
  先挪人的话那一帧算出来的是旧位置的雾；`ResetPath` 紧跟 `Move2Dest`，留着旧路线会让下一次走动把英雄沿旧路线
  拉回传送门（`heroes_action.cpp:1972`）。
* **「无处可去」是正常态**：`World.NextTeleport` 在一对只剩一头时把入口本身还回来，参考打一句警告就收场。端口
  折成 `TeleportOutcome.NoDestination` 而不是异常——地图编辑器允许孤立立石，孤立的漩涡同理。
* **沉兵按血量不按数量**：50% 作用在整叠兵的**血量**上，再把剩余血量按单兵血量向上取整折回只数。所以「10 个
  农民」（每只 1 点血）是 10 → 5，而「3 个剑士」受伤后可能只剩 2 只——这一位小数就是
  `Troop.GetCountFromHitPoints` 存在的理由。
* **两条豁免、四个早退**，顺序照抄参考：没兵 → 只有一叠且只有一只（漩涡不吞玩家最后一个兵）→ 骰子没中
  （只传送不沉兵）→ 那叠只剩一只（整叠消失，不是「剩零只」）。
* **随机数怎么进**：参考用 `Rand::Get( 1, 3 )`。端口有同名 API，但全局伪随机在测试里钉不住，所以公开动作多收一个
  可选 `Pcg32`、缺省用线程里那份；测试传固定种子就能确定地走「命中」或「没中」（`Army::Reset` 的 `generator`
  是同一套做法）。
* **落点不返回**：参考返回 void，端口也不多事——落点就是 `hero.MapIndex`，沉了几只去问军队。多返一个瓦片号只会
  让两个真相并存。
* **测试里踩到的坑记一笔**：`Tile.SetHero` 之后，瓦片是经 `World.GetHero( id )` 找「站在上面的是谁」的。测试若
  自己 `new Hero( 1, … )`，id 会和世界预建的英雄撞车，瓦片就去看一个没落位的陌生人：底下的立石查不到、出口
  列表为空，**表现与「孤立立石」一模一样**（第一版测试 8 条里 6 条因此失败）。测试改用 `world.GetHero( 3 )`，
  这条也写进了测试的注释，免得下次再花一轮排查。

用例：新增 `HeroTeleportTests` 8 条（立石送人到另一头并清掉路线、落点开雾而入口不开、孤立立石不动人、漩涡沉掉
最弱一叠的一半、血量折只数向上取整、独苗被豁免、骰子没中照样传送、孤立漩涡不吞兵）。`FHeroes2.Game.Tests` →
**1540**，全解决方案 → **2227** 条全绿，0 警告 0 错误。覆盖率合计仍 **34/63/152**（`heroes_action.cpp`、
`monster.cpp`、`game_static.cpp` 早已在「部分」里）。

**下一步**：`Heroes::Action` 那张 switch（`heroes_action.cpp:3747-4018`）是唯一的大缺口——30 个 `ActionTo*`
已经能干活，却**没有分发者**：谁踩到对象、按对象类型调哪一支还是空的。难点不在 switch 本身，而在「结果怎么
办」：大多数动作改完状态还要弹消息/开界面，而端口连消息层都没有，所以分发者得先定「返回值给谁」。
再往后是带守卫的金字塔/沉船/龙穴与矿井（战斗）、住所/巢穴/流浪营地（军队与城堡招募）、
`ActionToPoorMoraleObject`（战斗）、`ActionToSphinx`（答题 + 战斗）、`ActionToOracle`（盗贼公会界面）。
两处欠账不变：`LoadFromMP2` 按经验补等级、付费取宝的图标索引映射（`getResourceTypeFromIconIndex`）。

## 动作分发者：「结果给谁」的答案，以及被顺手堵掉的三个洞 ✅（2026-11-16）

30 支 `ActionTo*` 早就能干活，缺的是它们共同的入口 `Heroes::Action()`（`heroes_action.cpp:3747-4018`）。
这一轮把那张 300 行的 switch 搬完，也顺手回答了上一轮留下的设计问题——**结果给谁**。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `HeroActions.Action`（新） | `Heroes::Action`，`heroes_action.cpp:3747` | 取瓦片主对象 → 折成非动作值 → 30 支 `case` → 结果 |
| `HeroActionKind`（新枚举） | 无 | 哪一支跑了；`None` / `Unsupported` 两个哨兵 |
| `HeroActionResult`（新 struct） | 无（参考返回 void） | `ObjectType` + `Kind` + `Changed` + `Payload`，`TryGetPayload<T>()` 取回那一支自己的结果 |
| `Mp2ObjectTypes.NonActionType`（新） | `MP2::getNonActionObjectType` | 去掉动作位；分发器进门就折一次 |
| `Tile.GetNonActionObjectType`（新） | 同上 | 瓦片级的同一次折叠，供谓词与日收入共用 |
| `HeroActions` 各谓词、`TileMetadata.GetDailyIncomeObjectResources`（改） | `MP2::isActionObject` 一族 | 全部改比非动作值 |

要点：

* **「结果给谁」折成 `HeroActionResult`**。参考没有返回值，它靠对话框直接说给玩家；端口没有消息层，于是结果
  交回调用方：`Kind` 说是哪一支干的活（调用方据此选消息），`Changed` 说世界有没有被改动（要不要重绘、要不要刷
  面板），`Payload` 装那一支自己的返回值。`Changed` 与「要不要提示玩家」是两件事——第二次踩法力泉什么都没改，
  但调用方照样该说「晚点再来」。
* **非动作值是进门处唯一的交点**。瓦片存的是带动作位的值（`ActionWindmill`），端口的名册、分类谓词与 30 支动作
  的入参讲的都是不带位的值（`Windmill`）。分发器在 `switch` 之前折一次，`case` 标签就能与参考的 `OBJ_*` 一一对上。
* **一格三洞**：`IsInGameActionObject` 原本自己剥位、`TileMetadata` 的日收入 switch 比的是带位值、几支谓词混用两套
  词汇——三处都在这一轮统一到 `NonActionType`，否则分发器能进、助手查不到，风车这类对象会「看得见、摸不着」。
* **两步走的对象不硬做**：竞技场要先问玩家选哪项主技能（参考在函数里弹对话框），分发器只报到
  `HeroActionKind.Arena`、什么都不改，问完由调用方调 `ActionToArena`；马格勒的航海图与有主的神器本来就在返回值里
  带「还没问」，照样分发。宝箱那条「钱还是经验」的岔路用动作默认值：拿钱。
* **不会静默通过**：怪物、城堡、另一个英雄、船、金字塔、沉船、龙穴、矿井、招募所、事件、神谕、狮身人面像这些
  要战斗/城堡界面/文本的对象一律回 `HeroActionKind.Unsupported`——「参考会做事，端口还不会」明说，调用方可以提示
  或者干脆不放他走过去。踩空地才是 `None`。

用例：新增 `HeroActionDispatchTests` 7 条（非动作值折叠、有对象的格子才置 ActionResult.Action 模式、`Unsupported`
与 `None` 的区别、两步走的两支不改状态、日收入按非动作值查表、风车的元数据直接写 `tile.Metadata[0]/[1]`）。
`FHeroes2.Game.Tests` → **1547**，全解决方案 → **2234** 条全绿，0 警告 0 错误。

## castle 目录：建筑表、法师塔、队长，以及城堡的 AI 数值 ✅（2026-11-16）

`castle` 目录里**不画界面**的部分这一轮全部落地：`captain.cpp`、`mageguild.cpp`、`castle_building_info.cpp`
三份搬完，`castle.cpp` 补上 AI 与训练相关的方法。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `CastleBuildingInfo`（新） | `castle_building_info.cpp:806-1438` | 面积、名字、描述、前置条件文字、图标序号、绘制优先级 |
| `CastleBuildingInfo.Tables`（新，脚本生成） | 同文件 32-805 | 六族三张大表 + 绘制顺序；**手不算，脚本翻** |
| `MageGuild`（新） | `mageguild.cpp` | 布局 `[3,3,2,2,1]`、图书馆各层 +1、必有伤害/取消法术、冒险法术每层一个 |
| `Captain`（新） | `captain.cpp` | 主技能按族、无法术书但有法术点、只能学 1–2 级 |
| `ISpellBookOwner`（新） | `HeroBase` 的那一半 | 英雄与队长共用，智慧术规则在实现里 |
| `Hero.CanLearnSpell` / `AppendSpellToBook`（新） | `HeroBase::CanLearnSpell` 等 | 智慧术门槛 1–2 级免费 / 3 级基础 / 4 级高级 / 5 级专家 |
| `Castle.Modifiers.cs`（新） | `castle.cpp:2003-2063` | 力量（亡灵神殿 +2）、士气（酒馆 +1、斗兽场 +2）、幸运（彩虹 +2） |
| `Castle.Values.cs`（新） | `castle.cpp:643-729/976-1002/2065-2123` | `GetAvailableArmy`、`GetArmyRecruitmentValue`、`GetVisitValue`、`GetGarrisonStrength`、`AllowBuyHero` |
| `Castle.Training.cs`（新） | `castle.cpp:597-611` | `TrainHeroInMageGuild`、`TrainGuestHeroAndCaptainInMageGuild`、`InitializeMageGuild` |
| `Castle.Description.cs`（新） | `castle.cpp:976-1023/2527-2570` | 描述占位符替换、`RecruitHero`，以及缺口清单（塔、船、战斗、城镇界面、ICN） |
| `Castle.cs`（改） | — | 挂上 `_mageGuild`/`_captain`、`partial`、购买时的训练、`PostLoad` 的两步 |

要点：

* **三张大表由脚本从参考 `.cpp` 翻**，六族 × （面积、名字、描述）+ 绘制顺序共 21 个方法，手抄必错；脚本先把
  `BUILD_*` 注释映射成 `BuildingType` 成员、再按 `case` 分组输出。名字/描述抽查（Knight 的 `Spec`="Fortifications"、
  `Well2`="Farm"、Wizard 的 `Spec`="Library"）与参考一致。
* **法师塔的免费槽数含图书馆槽**——这是这一轮**抓到并修掉的 bug**：参考的 `freeSlots` 初值是
  `getMaxSpellsCount( level, hasLibrary )`（`mageguild.cpp:91`），端口第一版按 `[3,3,2,2,1]` 初始化，图书馆镇永远
  少一格（1 级只出 3 个法术而不是 4 个）。修法是把 `+1` 计入初值，并把「已放几格」单独计数（参考往 vector 里
  `push_back`，端口是定长数组，原来的下标公式在有图书馆时会算成 -1）。
* **`ISpellBookOwner` 是参考 `HeroBase` 的一半**：法师塔只关心「有没有书、能不能学、学进去」，所以参数类型收窄到
  这个接口，英雄与队长共用一套训练代码，智慧术规则留在各自的实现里。
* **队长只能学 1–2 级**，且**没有法术书**但有法术点：`HasSpellBook` 恒真在这条路线上是刻意的——参考的队长不是
  「买了书」，而是「本来就该学」。
* **亡灵神殿要判版本**：`Shrine` 是 Price of Loyalty 才加的，绘制顺序里它紧跟在 `Spec` 之后；SuccessionWars 的图不插。
* **`GetVisitValue` 与参考的一处刻意不同**：参考 `Troops futureArmy( heroArmy )` 复制的是**指针**，随后
  `Troop::Upgrade()` 会真的把英雄自己的兵升级掉——评估一次访问顺手白升一队兵。端口按值复制（`Assign`），数字一样，
  副作用没了。这条写进了方法的注释。

用例：新增 `CastleInfoTests` 19 条（共有名 vs 族名、占位符、面积按族且 `Nothing` 为空、多精灵建筑、图标序号、前置
条件文字、亡灵神殿的版本门槛、士气/幸运/力量三种加成与 `appendModifierToString` 的符号、法师塔各级槽数与图书馆、
队长的四项主技能与「只学 1–2 级」、无塔不教人、可招募军队不动守军、空城访问价值为 0、城墙与护城河把守军放大）。
`FHeroes2.Game.Tests` → **1566**，全解决方案 → **2253** 条全绿，0 警告 0 错误。

**遗留**：`castle` 目录里**画界面与画城镇**的七个文件没搬——`buildinginfo.cpp`、`castle_building.cpp`、
`castle_dialog.cpp`、`castle_mageguild.cpp`、`castle_tavern.cpp`、`castle_town.cpp`、`castle_well.cpp`
（合计约 4700 行，全是窗口、按钮、ICN 贴图与本地化文本），它们要的城堡界面在端口的 UI 层还不存在；`castle.cpp`
内部也留了五处明确标注的缺口：塔的守城强度、四条船的接口、`ActionPreBattle`/`ActionAfterBattle`、
`GetICNBuilding`/`GetICNBoat`、`DrawImageCastle`。这些都不是空函数凑数，每个都写了「为什么现在做不了」。

## agg 目录收尾：怪物动画的 821 字节、法术音效与三支音乐查询 ✅（2026-11-17）

`agg` 目录 6 个 `.cpp` 全部落地。这一轮把剩下的 `bin_info.cpp`（怪物动画）、`mus.cpp`（音乐查询）搬完，
并补上 `icn.cpp` 的英雄头像、`m82.cpp` 的法术音效与瓦片环境音、`xmi.cpp` 的 `FromMUS`。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MonsterAnimId`（新） | `bin_info.h:34` | 33 支动画的序号，就是 BIN 的列号 |
| `MonsterAnimInfo`（新） | `bin_info.cpp:88-523` | 821 字节记录：视线点、七列 × 16 帧的 x 偏移、三支速度、待机权重、投射物起点/角度、兵数偏移、33 支帧序 |
| `MonsterBinFileNames`（新，脚本生成） | `monster_info.cpp:147` | `binFileName` 表，72 条 |
| `MonsterAnimInfoCache`（新） | `bin_info.cpp:51-73` | 取归档 → 解析 → `IsValid` 才进缓存；坏记录每次重试并报警告 |
| `SpellSound`（新） | `m82.cpp:105-203` | 法术 → 音效；48 条 `case` 一条对一个法术，其中五对共用同一个音效 |
| `TileAmbientSound`（新） | `m82.cpp:205-286` | 瓦片环境音：溪流、火山口、篝火、矿、石阵、怪物、瀑布 |
| `MusicThemes`（新） | `mus.cpp:141-211` | 地形主题、城堡主题、战斗随机（`Rand.Get(1,3)`） |
| `HeroPortraitIcn`（新） | `icn.cpp:726-882` | 英雄 id 1…71 → `PORT0000…PORT0070`；`DEBUG_HERO` 借 Brax 的 |
| `M82Catalog` / `XmiCatalog`（新） | `m82.cpp:100`、`xmi.cpp:61-110` | 音效名表、曲目名表（含 6 条原版别名）、`FromMUS` 的十七支 |
| `M82FileNames` / `XmiFileNames`（新，脚本生成） | `m82.cpp:26`、`xmi.cpp:29` | 两张名表，`GenerateAggEnums.ps1` 扩了 `xmimap` |

要点：

* **一条记录 821 字节，字段全按写死的偏移读**（`bin_info.cpp:88-248`）。16 位有符号、float、uint32 混排，
  用 `BinaryPrimitives` + `ReadOnlySpan<byte>` 直读最贴近参考；`Record`/`StructLayout` 反而要处理对齐。
* **原版数据的十余处修补照抄**：狼的下方攻击帧 16→2、矮人死亡动画换成 49…55、石像鬼与食人魔的移动序列重排、
  剑士补帧、巨人/巨魔/美杜莎/游侠/元素/游牧民的逐帧偏移。参考里会 UB 的三处（`frameXOffset[MOVE_ONE][7]`、
  `animationFrames[MOVE_MAIN].back()`、`binFileName[id]`）端口先判长度/范围，注释里写明这是替换不是改写。
* **升级兵种的速度要按速度差折算**：与「前一个兵种」比（元素与火元素比），移动乘 `1 - 0.12 × diff`，
  射击乘 `1 - 0.08 × diff`；游侠单独用 0.78（它升级后多一次攻击）。
* **`MonsterCount` 是哨兵不是怪物行**——第一版生成器把它当第 74 行，表多一行，还把 `RANDOM_MONSTER_LEVEL_4`
  误判成「参考漏了」补了一个 `UNKNOWN`。实际参考 72 个初始化器对 72 个怪物是完整的，脚本改成读哨兵值并严格校验数量。
* **缓存注入 `AssetBundle`**：`MonsterAnimInfoCache(assets)` 对应参考的全局 `GetMonsterInfo`，失败不进缓存。

用例：新增 `MonsterAnimInfoTests` 20 条（错长度整体作废、450 默认移速、各字段偏移、待机条数上限 5、投射物条数上限 12、
角度分档、帧数上限 16、x 偏移是有符号字节、`IsValid` 两端、狼/矮人修补、升级速度折算与游侠系数、石像鬼的序列重排、
真实归档里七个兵种可用、随机怪物返回空记录）、`HeroPortraitIcnTests` 8 条、`MusicThemesTests` 21 条、
`SpellSoundTests` 22 条、`M82CatalogTests` 7 条、`XmiCatalogTests` 24 条。
全解决方案 **2354** 条全绿（Core 193、UI 145、Assets 379、Game 1637），0 警告 0 错误。
覆盖率：`agg` 行 `1/2/3` → **`6/0/0`**，连同上一轮 `castle` 的 `0/4/7` → `3/5/3`，合计 **34/63/152** → **42/62/145**。

**遗留**：`agg` 到这里只到「名字与映射」，**播放**不在这个目录（`engine/audio.cpp`、`audio_xmi2mid.cpp`
仍未转）；`bin_info` 的消费者还没接上——怪物信息窗（`dialog_monsterinfo`）未搬，`MINIMON` 的帧号依旧是
`(id - 1) * 9` 的硬编码，`MonsterAnimInfoCache` 已能给出正确帧序，等窗口搬过来时替换。

## maps 收尾：摘除一只对象要动八个邻居、中性城堡的旗，以及 `MapPosition` ✅（2026-11-17）

`maps` 目录这一轮把 `position.cpp` 转完，并补上 `maps_tiles.cpp` / `maps_tiles_helper.cpp` 的尾巴：摘除、
所有权旗、船与废矿恢复。前几轮的 `TileMetadata.Mutators` 56 条用例在这里也全部通过（其中「被占对象回答守军」
一条的用例本身写错了，见要点）。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `MapPosition`（新） | `position.h:33-74`、`position.cpp:29-37` | `center`、`GetIndex`、`SetIndex`、`isPosition`，几何由参数带入 |
| `TileOwnership`（新） | `maps_tiles.cpp:484-581`、`:1185-1303` | `setBoat`/`getBoatDirection`/`resetBoatOwnerColor`、`setOwnershipFlag`/`updateFlag` |
| `TileObjectParts`（新） | `maps_tiles.cpp:300-317`、`:354-390`、`:462-465`、`:885-1010`、`:1450-1509` | `GetCenter`、`removeObjectPartsByUID`、`getObjectLayerName`/`getObjectPartInfo`/`String` |
| `TileRemoval`（新） | `maps_tiles_helper.cpp:1858-2061` | `restoreAbandonedMine`、`removeMainObjectFromTile`、`removeObjectFromTileByType`、`removeObjectFromMapByUID` |
| `World.CaptureObject` | `world.cpp:815` | 接上旗子重画与城堡入口改色（原先是两条 TODO） |
| `CapturedObjects.ResetColor` | `world.cpp:261` | 返回被清掉的瓦片号，由 `World.ResetCapturedObjects` 重画旗子 |

要点：

* **摘除不是「删部件」而是「删完再修」**：`removeObjectPartsByUID` 之后要重算对象类型、本瓦片与八个邻居的
  通行（邻居可能被高物体的影子挡过），再把英雄脚下藏着的类型挂回英雄。少一步就是「看着对、走不对」。
* **按类型摘除先查精灵**：`removeObjectFromTileByType` 比的是 `getObjectTypeByIcn()`（主部件 → 顶层倒序 →
  地面倒序，画得最晚的那片先让路），拿到 UID 再交给 `removeObjectFromMapByUID` 泛洪——只有真的摘掉部件的
  瓦片才向八邻扩散。
* **`isCaptureObject` 只认带位值**：参考的 `OBJ_MINE` 就是端口的 `ActionMine`。原先「被占对象回答守军」
  用例拿 `Mine` 造瓦片，谓词当然说不是占领对象——修的是用例，代码保持「分类谓词比带位值、其余 switch 先剥位」。
* **中性城堡不能删旗**：参考用 `UNUSED` 颜色顶替 `NONE` 再画，两扇旗门留 `12`/`13`。端口没有 `UNUSED`，
  也不该为一个断言往 `PlayerColor` 里塞 MP2 没有的值，于是把「画旗」拆成私有的 `UpdateFlagSprite`。
* **`MapPosition` 不改 `MapObject` 的继承**：端口的地图对象在读图时按瓦片号放置，那一刻几何还不存在，
  `MapObject` 继续持 `TileIndex`；`MapPosition` 给编辑器与存档格式用。
* **`SetBoat` 把旧主部件压到地面层最前**：渲染顺序，参考明确要求 `emplace_front`；四个左向船身是翻转精灵，
  索引带 `128` 位，`GetBoatDirection` 原样读回。

用例：新增 `TileOwnershipTests` 28 条（矿井/灯塔/炼金实验室/锯木厂/城堡五种旗位、改色不重叠、收旗、
船的方向往返与船主、按 UID 摘除跨瓦片与不越界、`MapPosition` 往返与越界、调试串）、`TileRemovalTests` 8 条
（主对象摘除、按类型查顶层、跨瓦片 UID、废矿的左右半与矿工覆盖图换图、金币/宝石写元数据）。
全解决方案 **2446** 条全绿（Core 193、UI 145、Assets 379、Game 1729），0 警告 0 错误。覆盖率：`maps` 行 `4/7/6` → **`5/7/5`**，合计
**42/62/145** → **43/62/144**。

**遗留**：`maps_tiles_helper.cpp` 还差半径 `ClearFog`、`updateFogDirectionsInArea`、法术/军队几支与编辑器的
`setInitialObjectInfo`/`updateObjectInfoTile`/`updateRandomXxx`/`placeObjectOnTile`；`Tile` 的迷雾位与存档
运算符未搬；`map_random_generator*` 三支未开。

## maps / heroes / kingdom 三目录推进之一：速度尺子、经验文案与瓦片上的军队 ✅（2026-11-17）

三个目录一起收尾的第一批。`kingdom` 这一批把它剩下的两个纯逻辑文件清零，`maps` 补上瓦片军队三支，
`heroes` 的剩余文件都是动画/战斗/对话框，留给下一批。

| C# | 参考 | 内容 |
| --- | --- | --- |
| `Kingdom/Speed.cs`（新） | `kingdom/speed.cpp:29-124` | 十档尺子、`getSlowerSpeed`/`getFasterSpeed`、慢/急两法的 `extraValue` 分支 |
| `Kingdom/Experience.cs`（新） | `kingdom/experience.cpp:26-34` | `getExperienceName`/`getExperienceDescription` 两条文案 |
| `Monsters/MonsterStatistics.GetRandomCount` | `monster.cpp:165-220` | `GetRNDSize`：七级表 + 十六种特例，`Rand.Get(result/2, result)` |
| `Maps/TileTroops.cs`（新） | `maps_tiles_helper.cpp:763`、`:1697`、`:1727` | `GetTroop`、`UpdateMonsters`、`SetMonster`、`MonsterSpriteIndex` |

要点：

* **`GetSlowerSpeed`/`GetFasterSpeed` 是公开方法**：参考里是匿名辅助，但测试只能通过它们验证十档表，
  而且 `extraValue == 0` 的分支就是它们——私有会让这条规则无法钉住。
* **slow/haste 的 `extraValue` 本来就是 0**：`Spell::ExtraValue()` 读的是伤害法术那一列，两法在表里没有值，
  于是实际都退/进一格。测试照这个事实断言，不假装法术带值。
* **`experience` 的两条英文原文照搬**：参考交给 `_()`，端口无翻译层，返回原文即「未加载翻译时的参考行为」。
* **`GetRNDSize` 的特例是十六个数不出来的数字**（农民 80、盗贼 40、幽灵/美杜莎 20、骑兵 18……），
  逐条 `switch`；其余按等级查 `{0,50,30,25,25,12,8}`，再取半到全的随机。
* **军队三支的加入条件照抄优先级**：幽灵与元素生物永远 `SKIP`；写了数量的、以及本周正是自己那一族的
  一律 `MONEY`；其余 `Rand(1,10) < 3` 才 `FREE`。
* **新精灵取新 UID，老 UID 只在主部件已是 `MONS32` 时保留**；旧精灵压到地面层而不是扔掉。新世界第一枚
  UID 是 1，测试钉这个事实。

用例：`KingdomSpeedTests` 19 条（十档表两个方向、两法与 `extraValue` 的关系、两条文案）、`TileTroopsTests`
24 条（游荡堆叠与被占对象的守军、7 只必涨 1、6 只涨 0 或 1、上限与零堆叠、非怪物不涨、精灵与 UID 的两种
情形、旧精灵下沉、零数量=摇一次、四种加入条件、六种怪物数量的区间、精灵索引 = id − 1）。
全解决方案 **2489** 条全绿（Core 193、UI 145、Assets 379、Game 1772），0 警告 0 错误。
覆盖率：`kingdom` 行 `4/6/4` → **`6/6/2`**，合计 **43/62/144** → **45/62/142**。

**遗留**：`maps` 差编辑器四项与 `map_random_generator*` 三支；`heroes` 差 `heroes_dialog`/
`heroes_indicator`/`heroes_meeting`/`heroes_spell`、`heroes_move` 的 `GetIndexSprite`/音效/淡入淡出、
`heroes.cpp` 的剩余动画；`kingdom` 差 `kingdom_overview`/`view_world`（渲染）、`ActionNewMonth`（`Settings`）、
`week.cpp` 的 `GetName()`（翻译层）。

### 追加：路线预览的箭头表 ✅

`route.cpp:53-319` 的 `GetIndexSprite` 落地为 `HeroPath.GetIndexSprite`：五个代价块（1/25/49/73/97/121）
加一张八乘八的偏移表，`-1` 表示「回头的方向不画」。参考的八层嵌套 `switch` 与它是同一份数据，表的形状
让「每行恰好缺一格」这件事看得见；`Direction` 是位标志，因此另有一个 `DirectionIndex()` 把八个方向折成
0..7。`HeroPathTests` 增 21 条（五块起点 + 两个越界代价、九对方向、五对无箭头），Game 测试
1772 → **1793**，全解决方案 **2510** 条全绿。`Route::Path::String()`、行走音效与淡入淡出仍未搬。

### 追加：技能名与两套图标索引 ✅

`skill.cpp:407-455` 的三支落地到 `SecondarySkills`：`GetName`（十四项英文原文，无翻译层时即参考行为）、
`GetIndexSprite1`（id 本身，`ICN::SECSKILL`）与 `GetIndexSprite2`（id − 1，`EDITOR` 用，非法值 `0xFF`）。
`SecondarySkillsTests` 增 6 条，Game 测试 1793 → **1799**，全解决方案 **2516** 条全绿。
`skill.cpp` 只剩描述文案与 `FindSkillsForLevelUp` 之外的杂项。

### 追加：`system` 目录 ✅

`src/fheroes2/system/` 八个文件全部落地到 `src/FHeroes2.Game/System/`（ADR-139）：

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `System/EngineVersion.cs` | `system/version.h` | `1.1.18`、`Major/Minor/Intermediate/Build`、`Version` |
| `System/SaveFileFormat.cs` | `system/save_format_version.h` | 当前 10034、最低可读 10010，逐版本常量 |
| `System/BitModes.cs` | `system/bitmodes.{h,cpp}` | `SetModes`/`ResetModes`/`HasModes`/`HasAllModes` |
| `System/PlayerControl.cs` | `system/players.h` 的控制枚举 | `[Flags]`：`None`/`Human`/`Ai`，`Human|Ai` 合法 |
| `System/AiPersonality.cs` | `ai/ai_personality.{h,cpp}` | 三档性格、`Random()`、`GetString()` |
| `System/PlayerFocus.cs` | `system/players.h` 的 `Focus` | 英雄/城堡两类型化引用 + `FocusKind` |
| `System/Player.cs` | `system/players.{h,cpp}` | 名字、个性、控制者、自动接管、`Handicap`、`SetPlay` |
| `System/Players.cs` | `system/players.{h,cpp}` | 两种 `Init`、`SetStartGame` 四遍、颜色过滤、按色静态表 |
| `System/Settings.cs` | `system/settings.{h,cpp}` | 单例、`GameOptions`/`EditorOptions` 全部位值、各 setter、两个循环 |

要点：

* **`BitModes` 继承、`PlayerControl` 降成属性**：参考的 `Player` 双继承在 C# 里做不到，`Human|Ai` 这种
  「开局前可人可机」的组合仍保留在位标志里。
* **`Players` 两种形态都留**：实例列表（`Settings.Players`，按色枚举升序决定回合顺序）+ 六格静态表
  （`Players.Get(color)`）。`Get(None)` 回 `null`，不复刻参考的「撞上蓝槽」。
* **`Focus` 不再是 `void*`**：`Hero?`/`Castle?` 两个字段 + `FocusKind` 二次校验，陈旧引用读不回来。
* **AI 自动接管两段式**：开立即生效，关只写计划位，`CommitAiAutoControlMode()` 在 AI 回合结束时落地；
  `Control` 本身不动。
* **`Settings` 单例 + `internal Reset()`**：测试要干净的默认值；参考没有重新加载入口，注释写明这是端口补充。
* **三个易错点已修正**：`IsEvilInterfaceEnabled` 本次补写（`Good`/`Evil` 直答，`Dynamic` 看当前玩家种族，
  AI 回合按倒序找最后一个人类）；`SwitchToNextInterfaceType()` 原本写成 `Evil→Good→Dynamic`，与参考
  `settings.cpp:1057` 相反，现为 `Dynamic→Good→Evil`；`EnablePriceOfLoyaltySupport(false)` 顺带把
  `MidiExpansion` 退回 `MidiOriginal`。
* **不搬**：配置文件读写（`tinyconfig`）、`dir.cpp` 的文件搜索、`system.cpp` 的错误码文案、翻译层。

另：新命名空间 `FHeroes2.Game.System` 会遮蔽 BCL 的 `System`，`AllCastles`、`MultiFontText` 与测试里的
`MonsterAnimInfoTests` 都改成了 `global::System.*`。

用例：`SystemSettingsTests` 17 条（首跑默认值、三组钳制、价格扩展与音乐联动、各类开关、游戏类型位、
两个循环、动态界面的四支）、`SystemPlayersTests` 14 条（两种 `Init`、人类独占与共享槽、空文件头不清表、
强弱过滤、实际颜色、随机种族不重复、开局清相机、人类颜色缓存的失效时机、单向友谊、清表、`ToString`）、
`PlayerTests` 6 条、`PlayerFocusTests` 3 条、`SystemMiscTests` 5 条；`MapRace` 的五个辅助另在 Assets 侧
补 `MapRaceTests` 4 条（六种族表与随机落点、三支邪恶、四支魔法、九个名字）。Game 测试 1799 → **1844**，
Assets 379 → **383**，全解决方案 **2565** 条全绿（Core 193、UI 145、Assets 383、Game 1844），0 警告 0 错误。
覆盖率：`system` 行 `0/2/1` → **`3/0/0`**，合计 **34/63/152** → **37/61/151**。

**遗留**：`settings.cpp` 的配置读写与音频开闭联动、`dir.cpp` 的文件搜索、`system.cpp` 的错误码文案。

### 追加：`audio` 目录 ✅（2026-11-17）

`src/fheroes2/audio/audio_manager.{h,cpp}`（119 + 961 行）整个落地到 `src/FHeroes2.Game/Audio/`（ADR-140）：

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `Audio/AudioManager.cs` | `audio_manager.cpp` 的门面 | 缓存、`PlaySound(Async)`、`PlayMusic(Async)`、`PlayCurrentMusic`、`PlayLoopSoundsAsync`、`StopSounds`、`ResetAudio`、`Update` |
| `Audio/AudioTaskQueue.cs` | `AsyncSoundManager` | 三种优先级、`Push*`/`Remove*`、`TryTakeNext`、`Running`（`_taskToExecute`） |
| `Audio/AudioTask.cs` | 同上 | `AudioTask`/`AudioTaskType`/`MusicRequest`/`LoopSoundRequest` |
| `Audio/AudioLoopEffects.cs` | `playLoopSoundsImpl`、`findPairOfClosestSoundEffects` | `Plan` 出三步计划、`Register`、`Clear`、`GetChannels` |
| `Audio/AudioLoopEffectInfo.cs` | `ChannelAudioLoopEffectInfo` | 角度 + 距离的等值记录 |
| `Audio/MusicIdentity.cs` | `getMusicUID` | 高 32 位来源、低 32 位曲目；哨兵抛异常 |
| `Audio/MusicPlaybackMode.cs` | `Music::PlaybackMode` | `Resume`/`ResumeAndPlayInfinite`/`PlayFromBeginning` |
| `Audio/IAudioDevice.cs` | `Mixer::` + `Music::` | 通道不透明，`Play`/`SetPosition`/`Stop*`/`IsSoundPlaying`/`PlayMusic*` |
| `Audio/IAudioSource.cs` | `getDataFromAggFile` + `LoadMID` | 音乐目录、音效裸数据、转好的 MIDI、扩展档是否存在 |
| `Audio/MusicRestorer.cs` | 播放器作用域对象 | `IDisposable`，`Dispose` 时重放 `LastRequestedMusicTrack` |

要点：

* **线程换成 `Update()`**：参考的工作线程换成宿主每帧调一次的排空循环，语义等价且可测；`AudioManager.Update`
  里音乐先于音效、音效 FIFO、循环音效最后。
* **取出即出队，`remove*` 无条件清 `Running`**：否则「取出后、执行前」到达的取消会被撤销。移植时三处早退是错的，
  已按参考删掉。
* **循环音效拆成计划**：`Plan` 返回「移动 / 停止 / 新起」三张表，调用方按此顺序动设备，新通道用 `Register` 回报；
  被复用的通道不能被提前停掉。3D 开关翻转时清空全部通道重发。
* **并列时后找到者胜**：参考只跳过严格更差的候选；测试名同步为 `ATieGoesToThePairThatWasFoundLast`。
* **音乐三级回退**：设备已加载 → 音乐文件夹（仅 `External`）→ XMI 归档（`MidiExpansion` 先试扩展档）；
  已在响不重起；`PlayCurrentMusic` 清当前曲目再以无限循环重放；哨兵只记 `LastRequestedMusicTrack` 不播。
* **两种缓存都缓存失败**（磁盘上的归档不会自愈），MIDI 另按 `track >= MidiOriginalKnight` 让扩展档让开。

用例：`AudioLoopEffectsTests` 10 条（未移动保留原通道、移动复用最近通道、并列取后者、3D 翻转全停、超出通道、
停止与清空、3D 关闭清表）、`AudioTaskQueueTests` 8 条（三种优先级顺序、音乐与循环音效的替换、FIFO 音效、
四组取消）、`MusicIdentityTests` 3 条（两个来源不撞号、高低位、哨兵抛异常）、`AudioManagerTests` 16 条
（WAV 头包装与缓存、未知音效忽略、无声卡吞掉一切、音乐目录优先、归档回退、读不到不播、已在播放不重启、
设备库优先、`StopSounds`/`ResetAudio`、哨兵只记不播、`MusicRestorer`、队列顺序、读不到的音效跳过）。
Game 测试 1844 → **1881**，全解决方案 **2602** 条全绿（Core 193、UI 145、Assets 383、Game 1881），
0 警告 0 错误。覆盖率：`audio` 行 `0/0/1` → **`1/0/0`**，合计 **37/61/151** → **38/61/150**。

**遗留**：`App/Audio/AudioPlayer.cs` 还是自己的一套 SDL3_mixer 实现（只放外部音乐文件、单曲循环），
换成 `AudioManager` 需要 `IAudioDevice`/`IAudioSource` 的 SDL3_mixer 版本，属于 App 组合根，等界面音乐切换点
（`MusicRestorer` 的调用方）一起接。

### 追加：`h2d` 目录 ✅（2026-11-17）

`src/fheroes2/h2d/h2d.cpp`（151 行）与它依赖的 `src/engine/h2d_file.cpp`（236 行）一起落地（ADR-141）：

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `Assets/H2D/H2DArchive.cs` | `H2DReader` | 魔数带版本（3）、`uint32` 长度前缀字符串、每条体 zlib 解压、坏记录跳过、重名取首条 |
| `Assets/H2D/H2DWriter.cs` | `H2DWriter` | `Add`/`Add(H2DArchive)`/`Remove`、按名字序重排、`SmallestSize` 压缩、`Serialize`/`Write` |
| `Assets/H2D/H2DImageCodec.cs` | `readImageFromH2D`、`writeImageToH2D` | 17 字节头（w、h、x、y、单层）+ 像素 + 可选透明层，长度精确校验 |
| `Game/H2D/H2DResources.cs` | `H2DInitializer`、`h2d::readImage` | 68 条样本名单、三条开档失败路径、`GetDefaultPath`、`Current` 与 `TryReadCurrentImage` |

要点：

* **分两层**：容器是纯格式，进 `Assets`（与 `IcnSpriteFile` 同级，`Sprite` 在 `Core`）；「这版游戏需要哪些
  h2d 内容」是策略，留在 `Game`。
* **zlib 只用一个入口**：`zzlib.cpp` 在 H2D 上表现为标准 zlib 流，直接用 `ZLibStream`；压缩级照
  `zipData( …, true )` 取 `CompressionLevel.SmallestSize`，重写的档与参考逐字节一致。
* **坏记录跳过、坏头部报错**：空名 / 零长 / 越界记录跳过（同参考），重名取首条；但头部读不下去时抛
  `BinaryFormatException`，不学参考「读出一串 0 继续」。
* **图像体长度精确相等**，非正尺寸拒绝；单层图不分配透明层。
* **样本表用 `FrozenSet` + 序数比较**（等价 `std::set<std::string>`），三条失败消息照抄参考。
* **`Settings::findFile` 没搬**：`GetDefaultPath(dataDir)` 给 `files/data/resurrection.h2d`，`Current` 由组合根赋值，
  没装档时 `TryReadCurrentImage` 返回 false。
* **工具对拍**：`AssetProbe h2d <file.h2d>` 打记录表与每张图尺寸；用
  `F:\game\fh2\files\data\resurrection.h2d` 实测 68 条记录 / 68 张图全部解出，名字集合与参考源码的样本表
  完全一致。

用例：`H2DArchiveTests` 19 条（记录序、解压、缺名、非 zlib 体、坏魔数/坏版本/过短/空档/放不下的 count、
越界记录跳过、重名取首、写回往返、空名空体重拒、同名替换、删除、空档不写盘、体的名字序、复制整档、压缩生效）、
`H2DImageCodecTests` 10 条（像素与透明层、单层、截断/超长、非正尺寸、空体、写回往返、单层标志、空图拒绝、缺条目）、
`H2DResourcesTests` 8 条（样本 68 条、名单匹配、多一条/少一条、缺档、未装档时不取图、装了档走 `Current`、
`GetDefaultPath`）。Assets 383 → **412**、Game 1881 → **1889**，全解决方案 2602 → **2639** 全绿，
0 警告 0 错误。覆盖率：`h2d` 行 `0/0/1` → **`1/0/0`**，`engine` 行 `7/7/12` → **`8/7/11`**，
合计 **38/61/150** → **40/61/148**。

**遗留**：读原版存档时用 h2d 兜底图的那条链路还没接（等 `serialize.cpp` 的批次）；`AssetProbe h2d`
只解容器与 sprite，不解 `.GAM`/`.SAV`。

### 追加：`resource` 的宝物四件套（2026-11-17）

`src/fheroes2/resource/` 剩下的三块一起落地（ADR-142）：`artifact.cpp` 的档位/定价/随机/交换、`artifact_info.cpp`
缺的两支以外都已齐、以及整份 `artifact_ultimate.cpp`。

| 端口文件（新） | 参考 | 内容 |
| --- | --- | --- |
| `Game/Artifacts/ArtifactLevel.cs` | `Artifact::Level`、`LoyaltyLevel`、`isUltimate`、`isPriceOfLoyaltyArtifact` | `[Flags] ArtifactLevel`（`Treasure`/`Minor`/`Major`/`Ultimate`/`NoRandom`/`Loyalty` 与 `AllNormal`）、105 行的三档表、8 个终极（含 Golden Goose）、17 个资料片宝物的 `LoyaltyLevel` |
| `Game/Artifacts/ArtifactValue.cs` | `Artifact::getArtifactValue` | 每种加成的权重（金 /800、海移动 /500、陆移动 /200、百分比 /50、投降折扣 /10，旗帜类各 1 分，诅咒为负） |
| `Game/Artifacts/ArtifactRandom.cs` | `Artifact::Rand`、`ResetArtifactStats`、`ExcludeArtifactFromRandom` | 一张 `byte[]` 上两位（禁用/已发），先抽未发、空了再抽已发，禁用的两遍都不碰 |
| `Game/Artifacts/ArtifactMapSprites.cs` | `Artifact::getArtifactFromMapSpriteIndex` | 旧格式精灵索引反查（每宝物两个索引）、五个随机对象、207 只在 Resurrection 里是法术书 |
| `Game/Artifacts/ArtifactSets.cs` | `artifactSets` | Anduran 三件 → BattleGarb |
| `Game/Artifacts/ArtifactExchange.cs` | `BagArtifacts::exchangeArtifacts` | 池化、三条让与规则、按价值发牌到 `MaxCapacity - 1`、回吐、双向装配 |
| `Game/Artifacts/UltimateArtifact.cs` | `artifact_ultimate.cpp` | `Set`（无效 id 改抽终极）、±2/±1 偏移、`IsAt`/`MarkAsFound`/`Reset` |

`ArtifactBag` 追加：`GetTotalMultipliedPercent` 两个重载（返回列表）、`IsBonusPresent`/`IsCursePresent`、
`GetFirstArtifactWithBonus`/`Curse`、`ContainsUltimateArtifact`、`GetArtifactValue`、
`AssembleArtifactSets`、`SortFromWorstToBest`。

要点：

* **档位与地图版本绑定**：`.mp2` 上资料片宝物只有 `Loyalty` 位，随机抽不到——这是旧格式地图不该出现资料片
  宝物的机制，不是表没写完。版本当参数传，不读设置单例。
* **随机状态是进程级静态**：参考就是一张全局数组，`ResetArtifactStats()` 连禁用位一起清。测试因此放进
  `ArtifactRandomCollection`（`DisableParallelization = true`），每个用例前后各 `Reset()`。
* **乘法百分比不求和**：`getTotalMultipliedPercent` 返回 `IReadOnlyList<int>`，调用方逐件乘进法术；非乘算类型
  只给空表（参考是 `assert` + 空表，端口去掉 assert 以便测试钉住契约）。
* **交换拆到 `ArtifactExchange`**：它要读副技能与法术书，`ArtifactBag` 不该依赖 `Hero`。顺序照参考：纯诅咒
  先还、死灵法师留死灵宝物、已知法术的卷轴让与、唯一宝物去重、taker 取价值最高的一批并留一格给法术书、剩余
  回吐、双方装配。卷轴在端口里不带具体法术，按空卷轴处理。
* **排序给确定语义**：参考比较器不是严格弱序（魔法书与一切等价），端口定义成「书在首格、其余按价值升序、空位
  在尾」。
* **装配是 `while`**：重复的三件套能装两次；先删零件再 `Push`，袋满也放得进。

用例 106 条：`ArtifactLevelTests` 35（八终极、三档、`.mp2` 的资料片位、每宝物恰好一个档位位、资料片 17 个
且为尾部连续块）、`ArtifactValueTests` 24（各权重、诅咒为负、BattleGarb = 22、袋总和、乘法列表、存在性与
首个命中、终极判定）、`ArtifactRandomTests` 10（各档位、终极八选一、同档先发完再重复、排除项不可达、整档
禁用给 `Unknown`、`Reset` 清两位、`.mp2` 不抽资料片、`Loyalty` 档只抽资料片）、`ArtifactMapSpritesTests` 19
（两索引、资料片块需支持、法术书 207、五个随机对象、未用索引 `Unknown`、172 走算术落在 `Unused86`）、
`UltimateArtifactTests` 6、`ArtifactExchangeTests` 7（纯诅咒归还、死灵法师保留、法术书不动、只取最优 13 件、
Anduran 装配、空 giver、双满袋不丢件）、`ArtifactBagTests` 追加 5（装配、装两次、不完整不动、排序、空袋）。
Game 1889 → **1995**，全解决方案 2639 → **2745** 全绿，0 警告 0 错误。覆盖率：`resource` 行 1/2/1 →
**2/2/0**，合计 **40/61/148** → **41/61/147**。

**遗留**：`artifact.cpp` 仍记 ◐——`Artifact::GetName`/描述文案、`String()` 与 `description` 重载（文字），
`ArtifactsBar`（装备栏界面）；`artifact_info.cpp` 的两支描述文案同理；`SetSpell` 的 `ext` 随卷轴界面补。
序列化与 `GetPuzzleMapSurface` 留在 serialize/UI 批次。

### 追加：`spell` 收尾（2026-11-17）

`src/fheroes2/spell/` 剩下的三块一起落地（ADR-143）：`spell.cpp` 的谓词/定价/随机查询、整份
`spell_info.cpp` 的公式与地图查询、`spell_book.cpp` 的过滤与页状态。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/Spells/Spell.cs`（追加） | `spell.cpp:562-776`、`spellPoints`、`SuitableForSpellBook` | 十五条谓词（治疗、亡灵/存活、单体、无焦点、驱散、友/敌/任意、火/冷/元素、守卫、仅内置）、`Restore()`、`SpellPoints(bag)` 的四类神器减耗（逐件乘、最低 1 点）、`DimensionDoorDistance()`、`TotalSpellsPerLevel`、`RandomSpell`，并修正 `SuitableForSpellBook` 只按等级过滤 |
| `Game/Spells/SpellInfo.cs`（新） | `spell_info.cpp:69-426` | 六条公式（伤害、召唤数量、治疗量、复活量、守卫数量、催眠 HP，均带神器乘法百分比）与两条距离助手；地图查询四条：`GetNearestCastle`（平方距离、首个胜平局）、`GetPossibleBoatPosition`（英雄格的方向通行性 + 邻居 `IsSuitableForSummoningBoat`）、`GetSummonableBoat`（己方/他国/中立三档 + 原版 AI 限制）、`IsHeroNearWater` |
| `Game/Spells/SpellBook.cs`（新） | `spell_book.h:28-90`、`spell_book.cpp:200-262`、`496-552` | `[Flags] SpellBookFilter`、`SpellsPerPage = 6`/`SpellsPerScreen = 12`、`BeginSession`（四个起始状态分支 + 过期下标回首页）、`SelectFilter`、两个翻页动作、`ResetState`、`ScreenSpells` 切片、静态 `SetFilter`（复制书 + 卷轴 + 按冒险/战斗过滤 + 按 id 排序） |

要点：

* **不把 `Hero` 拖进 `Spells/`**：`spellPoints( hero )` 换成 `SpellPoints( ArtifactBag? )`，空袋等价参考的
  `hero == nullptr`；`spell_info` 的公式同样只收法术点与袋子，只有真正读地图的四条查询收 `World`/`Hero`。
* **减耗是乘法不是加法**：`GetTotalMultipliedPercent` 给列表，逐件 `cost*(100-p)/100` 再逐次截断，末尾钳到 1。
* **`SuitableForSpellBook` 修了旧错**：以前手写排除 `ViewMines`，参考只按 `Level() >= 1`；`View` 系法术本就该进
  法师公会，占位符与 `Petrify` 因等级为 0 自然出局。等级表现在是 12/11/16/16/10，合计 65 条可学法术。
* **召唤船查的是 `ActionBoat`**：`MP2::OBJ_BOAT` 带动作位，而 `GetObjectPositions( ..., ignoreHeroes: false )` 比的是
  缓存类型，`SetBoat` 缓存的正是 `ActionBoat`；写成 `Boat` 会永远拿到空表（新测试第一次就是这么挂的）。
* **保留两个怪癖**：返回「距离 > 1 的第一条船」而非最近的；原版地图上 AI 不召中立船（玩家托管时例外）。
* **法术书只搬规则**：英雄的存储仍是 `SpellStorage`，`SpellBook` 只管标签/下标/翻页/会话起始，`SetFilter` 是静态
  方法返回列表、不动英雄的书；`Open` 的绘制与输入留给 UI 批次。
* **卷轴暂时贡献为零**：`SetFilter` 照参考追加神器上的卷轴，但卷轴还不带法术 id（ADR-142）。

用例 27 条新增：`SpellTests` 追加 10（`TheHealingSpellsCarryTheirRestoreValue`、目标谓词、元素/守卫、四类减耗、
最低 1 点、公会法术表、`View` 可学、排除集、随机等级、传送门 14 格）、`SpellInfoTests` 8（三类元素加成互不串台、
召唤/治疗/复活/守卫/催眠、最近城堡平局、放船落点、近水、可召唤船的四种组合）、`SpellBookTests` 9（两个标签的
切分与排序、不动原书、会话起始三分支、过期下标、标签开关、翻页止于两端、切片、卷轴）。Game 2005 → **2022**，
全解决方案 2745 → **2772** 全绿，0 警告 0 错误。覆盖率：`spell` 行 1/1/2 → **1/3/0**，合计 **41/61/147** →
**41/63/145**。

**遗留**：`spell.cpp` 仍记 ◐——73 个法术名与描述文案（参考走 `_()`，端口无翻译层）、序列化运算符；`spell_info.cpp`
仍记 ◐——`getSpellDescription` 的文案拼装（要英雄名、最近城名）；`spell_book.cpp` 仍记 ◐——`SpellBook::Open` 的
绘制与输入循环，与 `ArtifactsBar` 同批。

### 追加：`army` 收尾（2026-11-17）

`src/fheroes2/army/` 剩下的规则一起落地（ADR-144）：地图对象的固定守卫、换防/漩涡/战场三种摆法、
分兵与合并那批容器助手，以及中立怪的加入判定。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/Army/Army.Size.cs`（新） | `army.cpp:120-251` | `ArmySize` 九档（Few 1-4 … Legion 1000+）、`GetArmySize`（十个 `if` 的升序阈值）、`SizeRangeString`（`1-4`…`1000+`）、`SizeString`（`Few`…`Legion`） |
| `Game/Army/Army.Split.cs`（新） | `army.cpp:1010-1040`、`900-1010` | `AddNewTroopsToFreeSlots`（余数靠左）、`SplitTroopIntoFreeSlots`（只按空槽数夹、不动另一支军队）、`MergeWeakestTroopsIfNeeded`（满员时合并最弱的重复兵种）、`SplitStackOfWeakestUnitsIntoFreeSlots`（每空槽搬一个）、`AssignToFirstFreeSlot`、`JoinAllTroopsOfType` |
| `Game/Army/Army.JoinSolution.cs`（新） | `army.cpp:1641-1731` | `GetJoinSolution` 返回 `NeutralMonsterJoiningCondition(Reason, MonsterCount)`：空栈断言、强度比、`canJoin` 四条、免费/外交（按血量折算 + 王国付得起整队）/逃跑 |
| `Game/Army/Army.Arrangement.cs`（新） | `army.cpp:1040-1192`、`1783-1975` | `SetFromTile` 九种固定守卫 + 两个 fallback；`ArrangeForCastleDefense`（AI 降序塞入 + 满员合并、人类同槽位搬运）；`ArrangeForWhirlpool`（优化后最弱扣一个）；两支 `ArrangeForBattle`（居中定阵 / 地图怪 3-5 格 + 中格升级） |
| `Game/Army/Army.cs`（追加） | `army.cpp:384` | `AreAllTroopsUnique()`：满军无可合并时 AI 规划器的前提 |

要点：

* **`SetFromTile` 逐格写死**：九支固定守卫的槽位顺序是设计（中格是唯一会升级的格），所以不抽表；照抄两处
  怪癖——恶魔洞穴只有四格、龙之城三绿一红一黑；战役翻倍未转（没有战役状态）。
* **两个 fallback 的 `allowUpgrade` 不对称**：捕获对象记录的守军不升级，地图上的怪物可升级。
* **阵型由种子决定**：地图怪按 `Pcg32( MapSeed + tileIndex )` 抽 3..5 格，中格升级走第二路
  `MapSeed + tileIndex + monster` 的 50%；同一个 tile 每次进战场都一样。
* **放开参考的两条「满军」断言**：入口的 `size() == 5 && garrison.size() == 5` 与 AI 分支出口的
  `size() == 5` 在真实调用点 `Castle::ActionPreBattle` 会误伤（城堡没建满住所），端口只保留
  「守军还剩兵 ⟹ 我方满格」这条更弱的不变量。
* **`isControlAi` 由调用方传入**：端口没有玩家对象；`GetControl` 因此未转。
* **`Troops::Insert`/`PushBack`/`PopBack` 不移植**：它们改参考的 vector，端口五槽定长、开放式容器是 `List`。

用例 86 条新增：`ArmySizeTests` 38、`ArmySplitTests` 12、`ArmyArrangementTests` 24、`ArmyJoinSolutionTests` 9、
`ArmyTests` 追加 3。Game 2022 → **2108**，全解决方案 2772 → **2858** 全绿，0 警告 0 错误。覆盖率：
`army` 行不变（0/2/2），合计不变（41/63/145）。

**遗留**：`army.cpp` 仍记 ◐——`TroopSizeString`/`String` 的文案（要复数兵种名与翻译层）、三个绘制函数、
`GetLuckModificator`/`GetMoraleModificator` 的城堡与神器两项、`GetCurrentUnit`（战斗侧）、`GetControl`、
`operator<<`/`>>`（无存档格式）；`army_troop.cpp` 仍记 ◐——`GetName` 与四个数值字符串、`ArmyTroop` 的
攻防加值（战斗侧）与序列化；`army_bar.cpp`、`army_ui_helper.cpp` 整份是界面绘制，与 `castle_dialog` 等同批。

### 追加：`castle` 收尾（2026-11-17）

`src/fheroes2/castle/` 剩下的规则一起落地（ADR-145）：队长变成指挥官、船坞四条、两个战斗钩子、
`isPosition`/`GetControl` 与六族的建筑 ICN 表；顺带补上 ADR-144 留下的两项 `Army` 遗留。

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/Castle/Captain.cs`（重写） | `captain.cpp:170-330` | `ISpellBookOwner` + `IArmyCommander`：攻/防/知识 = 本值 + 城 modificator，力量钳到 `[1, 255]`，士气/幸运 `Normalize`，`InCastle`/`BagArtifacts`，`GetMagicStrategicValue` |
| `Game/Castle/Castle.Boats.cs`（新） | `castle.cpp:1799-1934` | `allowBuyBoat`/`buyBoat`/`hasBoatNearby`/`getTileIndexToPlaceBoat` + `GetICNBoat` |
| `Game/Castle/Castle.Battle.cs`（新） | `castle.cpp:1999-2032` | `ActionPreBattle`（人类同槽位合军、AI 分支留空）、`ActionAfterBattle`（攻方胜才清空） |
| `Game/Castle/Castle.IcnTables.cs`（新） | `castle.cpp:1451-1797` | `GetICNBuilding` + 六个 per-race `switch`，147 条 |
| `Game/Castle/Castle.cs`（追加） | `castle.cpp:2457`、`2527` | 构造里 `_army.Commander = _captain`；`IsPosition`（八格十字）、`IsControlAi` |
| `Game/Castle/Castle.Values.cs`（改） | `castle.cpp:2167-2240` | `GetGarrisonStrength` 接客将合军，新增 `guestIsControlAi` |
| `Game/Castle/AllCastles.cs`（追加） | `castle.h` `VecCastles` | `FirstCastle`（有城 0、无城 -1） |
| `Game/Army/IArmyCommander.cs`（追加） | — | `InCastle`（默认 `null`）、`BagArtifacts` |
| `Game/Army/Army.cs`（改） | `army.cpp:270-390` | 士气/幸运的城堡项与神器项，殉难者之臂的诅咒 |
| `Game/Artifacts/ArtifactBag.cs`（追加） | `artifact.cpp:551-564` | `IsArtifactCursePresent`（非累计诅咒不能走累计查询） |

要点：

* **`HasBoatNearby` 查带动作位的值**：`MP2::OBJ_BOAT`/`OBJ_HERO` 是 43+128 / 55+128，端口对应
  `Mp2ObjectType.ActionBoat`/`ActionHero`；写成 `Boat`/`Hero`（mp2 文件里的原值）会永远拿不到，第一次就写错了。
* **队长的修正只来自城**：参考 `GetAttack()` 加 `GetAttackModificator( nullptr )`，即城项 + 神器项；端口的队长
  口袋永远为空、也不在冒险地图上，所以只有城项。
* **合军的并法由客将的控制决定**：`isControlAI()` 问的是合并后那支军队（客将）的控制，不是城的。端口没有玩家
  对象，参数化并默认 `true`（AI 评估目标城的口径）。
* **法师塔要归一**：参考把 `BUILD_MAGEGUILD1..5` 列成五个 `case` 共一个图，端口的表按掩码建，入口把五个等级
  折成掩码。
* **怪癖照抄**：骑士 2 级住所与升级互换、术士无 5/6 级升级与 `Well2` 借用骑士农场、亡灵无酒馆（神殿顶位）
  与独立队长建筑、术士帐篷 `Twnstent`、warlock 7 级升级 `Twnwup5b`、wizard 无 2/4 级升级。
* **表用脚本对过**：从参考 `GetICNBuilding` 抽出 147 条（键名去下划线后与端口枚举名同构），逐条比对全等。

用例 37 条新增：`CastleCaptainTests` 7、`CastleBoatsTests` 8、`CastleRulesTests` 9、`CastleIcnTableTests` 11、
`LuckMoraleTests` 追加 2。Game 2108 → **2145**，全解决方案 2858 → **2895** 全绿，0 警告 0 错误。覆盖率：
`castle` 行不变（3/5/3），合计不变（41/63/145）。

**遗留**：`castle.cpp` 仍记 ◐——塔的守城强度（`Battle::Tower`）、`ActionNewMonth` 的维护费与税收、
`DrawImageCastle`（城堡屏）、`ActionPreBattle` 的 AI 分支（`AI::Planner::CastlePreBattle`）、
`FixFrenchCharactersInName`（文本批次）；`buildinginfo.cpp`、`castle_building.cpp`、`castle_dialog.cpp`、
`castle_mageguild.cpp`、`castle_tavern.cpp`、`castle_town.cpp`、`castle_well.cpp` 七份（约 4700 行）仍是界面批次。

### 追加：`monster` 目录的规则面（2026-11-17）

`src/fheroes2/monster/` 补齐规则部分（ADR-146）：

| 端口文件 | 参考 | 内容 |
| --- | --- | --- |
| `Game/Monsters/MonsterWeaknesses.cs`（新） | `monster_info.cpp:531-561`、`:947` | `MonsterWeaknessType`（8 项）、`MonsterWeaknessInstance`、四元素人的弱点表、`IsPresent`/`TryGetInstance`/`IsBasic` |
| `Game/Monsters/MonsterLevels.cs`（新） | `monster.cpp:576-689` | `MonsterLevelType`、`GetRandomUnitLevel`、`GetRandom`（10/15/27/14 四桶，共 66 只） |
| `Game/Monsters/MonsterIcnIds.cs`（新） | `monster.cpp:40-75`、`:825-828` | `GetMissileIcn`、`GetDwellingIcn`（`Monh0000 + id - Peasant`） |
| `Game/Monsters/MonsterSummons.cs`（新） | `monster.cpp:77-108` | `Monster( const Spell & )`：八个元素法术 + `Haunt` |
| `Game/Monsters/MonsterSpellResistance.cs`（新） | `monster_info.cpp:880-940` | `getSpellResistance`，判定顺序照抄 |
| `Game/Monsters/MonsterStatistics.cs`（追加） | `monster.h:227` | `IsRandomMonster` |

要点：

* **弱点只有四个元素人**：空气/土互怕闪电三件套与对方生物，火怕冰法术与水生物，水怕火法术与火生物；
  `IsBasic` 画"描述里显示不显示"的线（四个"双倍伤害来自某系生物"不显示）。
* **`Any` 不查桶**：直接 `Rand.Get( Peasant, WaterElement )`，永远掷不到 marker；桶按 id 升序一次建成，
  同种子下与参考同序。`RANDOM_MONSTER` 每问一次掷一个桶，这是参考 getter 本身的行为。越界抛异常而不是
  数组越界（不复制未定义行为）。
* **半人马借精灵的箭**：`GetMissileIcn` 照参考注释回落到 `ElfMsl`。
* **`GetDwellingIcn` 的边界是端口加的**：参考的 `ICNMonh()` 不检查，marker 会算出 MONH 之外；端口对
  `[Peasant, WaterElement]` 之外答 `Unknown`。
* **抗性顺序不能换**：心灵类 → 只对活物 → 只对亡灵 → 冰/火/元素免疫 → 魔抗（100 短路，否则只作用于伤害
  法术与对敌法术）→ 指定法术免疫取最大。龙的 `MAGIC_RESISTANCE 100` 必须晚于前三项才算"全免疫"。

用例 61 条新增：`MonsterWeaknessTests` 7、`MonsterLevelTests` 8、`MonsterSpellResistanceTests` 13、
`MonsterSummonsTests` 14、`MonsterIcnIdTests` 20。Game 2145 → **2206**，全解决方案 2895 → **2956** 全绿，
0 警告 0 错误。覆盖率不动（`monster` 仍 0/2/1）。

**遗留**：`monster.cpp`/`monster_info.cpp` 只剩文案（`GetName`/`GetMultiName`/`GetPluralName`/
`getRandomRaceMonstersName` 与四个描述函数，都要 `Spell.GetName()` 与 `_()`）与两张只被战斗侧读的表
（`icnId`/`sounds`）；`monster_anim.cpp` 的 `RandomMonsterAnimation` 依赖 `battle_animation.h`，随战斗批次。
