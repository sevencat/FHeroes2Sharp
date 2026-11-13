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
| `FHeroes2.Client.Desktop` | ✅ | MonoGame 宿主、`MonoGamePresenter`、`FontStashTextRenderer`、诊断场景、命令行、截图 |
| `tools/FHeroes2.AssetProbe` | ✅ | `list` / `info` / `extract` / `sprites` / `tiles` 五个子命令 |
| 测试 | ✅ | 97 个（Core 61 / Assets 36，全部通过；支持真实数据时含 1434 条哈希与 ICN/TIL 摘要集成校验） |
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

### 关键数据校正（相对早前侦察笔记）

* `heroes2.agg` 条目数是 **1434**（不是 154——早期脚本读取头部时字节序累加写错）。
  该数字现由 `AssetArchive` 打开时对 **1434 个真实名称哈希**逐一校验得出，因此同时验证了
  `AggFileNameHash` 的移植正确性。
* `heroes2x.agg` 条目数是 **52**。
* `KB.PAL` 是 **6 bit VGA** 调色板（分量 0..63），需 `<< 2` 归一化，并按 `setGamePalette`
  复制水/岩浆循环色（231–233、235 → 246–249，214–217 → 250–253）。
* 原版位图字体文件名是 `BIGFONT.FNT` / `SMALFONT.FNT`（不是 `.ICN`）。

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
| 主菜单界面（`ADVBORD` 边框、按钮、FNT 位图字体） | ⏳ | 未开始 |
| 语言切换界面、BGM 播放 | ⏳ | 未开始 |

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

## 后续里程碑

见 [PLAN.md](PLAN.md)。M1 的条目已全部完成，并且已经用**原版 C++ 实现**对拍过
（`tools/scripts/CompareWithReference.ps1`，默认 14 个条目 / 1204 个 BMP 逐字节相同）；
`AssetProbe compare` 仍然保留，用于 PNG 视觉回归与差异图输出。
