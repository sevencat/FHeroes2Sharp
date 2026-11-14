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
