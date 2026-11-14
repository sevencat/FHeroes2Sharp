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
| 像素基线 `adventure-map.png` | ✅ | `--map Alteris.mp2` 直接开图，居中到地图中部城堡，忽略状态栏；`VerifyAdventureMapBaseline.ps1` 与另外四条基线共用 `PixelBaseline.ps1` |
| `Assets.Maps.Mp2MapHero` / `Mp2MapHeroes` | ✅ | 英雄从 tile 反解而不是读信息块：`MINIHERO` 帧号 `/7` → 颜色（`heroColorsOrder`）、`%7` → 种族（`Race::IndexToRace`），越界抛 `BinaryFormatException`；`Mp2Map.Heroes` 按 tile 顺序列出 |
| `Game.AdventureMap.IHeroSpriteSource` + `HeroSpriteSet` | ✅ | 按 (颜色, 种族) 取三张图集：身体 `KNGT32`/`BARB32`/`SORC32`/`WRLK32`/`WZRD32`/`NECR32`，旗帜 `B-FLAG32`…`P-FLAG32`，阴影 `SHADOW32`，一律取第 18 帧（朝右站立）；`Random` 种族没有身体，只出旗帜（ADR-037） |
| `Game.AdventureMap.IcnSpriteCache` | ✅ | 对象层与英雄层共用的「按文件名解码一次」缓存，`ObjectSpriteSet` 改为复用它 |
| `AdventureMapView` 英雄三带绘制 | ✅ | `AboveTile` / `OnTile` / `BelowTile` 按 32 px 行带切精灵，分别在地面层 `SHADOW_LAYER` 之后、四层全部画完之后、顶部层之后绘制；阴影单独一批、位于对象之下；可见性判定左右各放 2 格 |
| `tests/FHeroes2.Game.Tests/HeroLayerTests` + `HeroSpriteSetTests` + `Mp2MapHeroesTests` | ✅ | 8 条绘制用例（盖住本格对象、上带/下带行位、阴影先于对象、缺旗帜仍有身体）+ 18 条图集映射用例（6 种族 / 6 色 / 缺省 / 真实数据负锚点）+ 16 条帧号解析用例（颜色、7 帧种族、越界、tile 过滤、整图收集） |
| `tools/FHeroes2.AssetProbe map` 英雄列表 | ✅ | 打印 `x,y 颜色 种族 MINIHERO 帧号`，`Alteris.mp2` 9 名英雄（1 黄野蛮人 / 2 蓝 / 6 紫亡灵） |
| `Game.AdventureMap.MapGround` + `MapGrounds` | ✅ | `Maps::Ground` 的移植：九个 `*_START_IMAGE_INDEX`（30/92/146/208/262/321/361/415/432）把 terrain image index 反解成地面类型，越界给 `Unknown` |
| `Game.AdventureMap.Minimap` | ✅ | `Interface::Radar` 的数据与缩放：每格一个调色板索引（地面色 / 山树 +3 / 道路 `0x7A` / 英雄王国色）、`Zoom = 144 / 宽`、「每 6 像素省一个」的虚线视口方框、视口覆盖全图时不画方框；输出 144×144 `PixelBuffer`（ADR-038） |
| `AdventureMapScene` 雷达叠加 | ✅ | 雷达画在参考坐标 `(480,16)`，1 px 黑框圈住；面板美术 `ADVBORD.ICN` 与 448×448 视口属于界面外壳，仍未做 |
| `tests/FHeroes2.Game.Tests/MinimapTests` | ✅ | 35 条用例：19 条地形索引分段 + 8 条调色板常量 + 道路/山树/未知地面/英雄染色 + 缩放块对齐 + 方框几何（含贴边裁剪、全图时隐藏）+ 虚线步长 + 面板落位；另 2 条 `TileAt` 的整数除法（72 / 108 格两种缩放、越界不 clamp） |
| `Game.AdventureMap.MapScrollController` | ✅ | 冒险地图的鼠标：`GameArea::QueueEventProcessing` 的拖动（10 px 严格阈值、位移 `last - current`、从按下点补位移）、`HumanTurn` 的 16 px 边框滚动（20 ms 一步、`2 << ScrollSpeed` = 8 px、拖动期间禁止、单帧最多 4 步）、滚轮（参考没有，自定一格 tile） |
| `Minimap.TileAt` | ✅ | `Radar::QueueEventProcessing` 的面板像素 → tile 反算 `( pt - rect.pos ) * world / rect.size`，整数除法，越界交由视图 clamp |
| `AdventureMapScene` 鼠标接线 | ✅ | `SetCursor` / `SetLeftButton` / `ScrollByWheel` / `Update`；按下落在雷达上就交给小地图（点击即居中、按住继续跟随，参考把两者放在同一分支），其余交给拖动；`GameHost` 的三处 `_adventure is null` 守卫改成转发，左键与滚轮都进地图 |
| `tests/FHeroes2.Game.Tests/MapScrollControllerTests` | ✅ | 12 条：10 px 不算拖 / 11 px 算拖、方向、clamp、半 tick 不滚、角落双轴、拖动期间禁止边缘滚动、贴边返回 false、滚轮一格与 clamp |

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
* **雾、路径、界面面板都没有**：雷达已经上屏（ADR-038），但右侧/底部控制面板（`ADVBORD.ICN`）、
  英雄状态、日期与资源栏仍然缺；地图区还是整屏 640×480，参考是 448×448 @ (16,16)。状态栏是占位。
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

## E2 — 地图编辑器（改地形 + 放对象 + 撤销/重做）✅ 地形与对象

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
| 对象属性（信息框 + 数据字） | ✅ | `I` 打开指针下那一格的属性框（参考右键弹出的 `Editor::showPopupWindow`）：对象名（编辑器放过的就是预设名）、`地面 icn:帧 layer`、`顶 icn:帧`、`data N`、`[x; y]`；`[` / `]` 逐 1 改这段 13 位数据字，一次一撤销。数据字就是 MP2 的 `quantity1/quantity2` 高位，写入时保留 `quantity1 & 0x07`（含地面层位）；空 tile 没有属性可改（ADR-051） |
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
* **数据字有编辑入口、没有语义**：属性框里的 `data N` 就是 MP2 的 13 位字，`[` / `]` 能改，但资源堆
  显示的还是数字而不是「500 黄金」，矿也不会写上归属。语义要等参考的**对象目录**
  （`map_object_info.cpp`）与 `MP2::StringObject` 搬过来（ADR-051）；城堡表与可占领表同理，也不随
  对象更新。
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


