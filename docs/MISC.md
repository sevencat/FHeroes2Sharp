# 杂项（MISC）

## 构建环境

| 项 | 值 |
| --- | --- |
| SDK | .NET 10（`global.json` 固定 10.0.101，`rollForward: latestFeature`；本机实际使用 10.0.402） |
| 解决方案 | `FHeroes2Sharp.slnx`（SDK 10 默认 XML 格式） |
| 渲染 | SDL3-CS 3.4.20 + SDL3-CS.Native 3.4.2（SDL 3.4） |
| 字体 | SDL3-CS.Native.TTF 3.3.0（SDL3_ttf / FreeType，仅诊断文本）+ Sevencat.GlyphRaster 0.1.5（纯托管 TrueType 光栅化，内嵌文泉驿微米黑 4.35 MB） |
| 测试 | xunit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Microsoft.NET.Test.Sdk 18.12.0 |
| 包管理 | 集中版本（`Directory.Packages.props`，`ManagePackageVersionsCentrally`） |
| AOT / 裁剪 | 已开启 `IsAotCompatible` + `IL2xxx`/`IL3xxx`（警告即错误）；**不提供** `PublishAot` 发布（ADR-011） |

所有 csproj 均手写，不使用内容管线；SDL3 / SDL3_ttf 的原生 DLL 由 NuGet 按 RID 拷贝到输出目录。

## 代码约定

* `Nullable=enable` + `ImplicitUsings=enable` + `TreatWarningsAsErrors=true` + `AnalysisLevel=latest`。
* 命名：PascalCase 公共成员；私有字段 `_camelCase`；常量 PascalCase。
* 几何类型是 `readonly record struct`；服务类 `sealed`；接口以 `I` 前缀。
* 一个文件一个公开类型（小 DTO 例外），文件作用域命名空间。
* 外部格式的数字常量集中在对应解析器内，不散落于业务代码。

## AOT / 裁剪约束（现在就要遵守）

项目**不发布** NativeAOT 版本（ADR-011），但分析器是开着的，且 `TreatWarningsAsErrors` 会让
`IL2xxx`/`IL3xxx` 直接构建失败。因此下列写法在提交前就会被拦住：

| 不允许 | 替代方案 |
| --- | --- |
| `Type.GetType(name)`、`Activator.CreateInstance` | 显式 `switch` 工厂方法 |
| `dynamic`、`Expression.Compile`、运行时 `MakeGenericType` | 泛型 + 显式类型参数 |
| `Assembly.Load` / `Assembly.GetTypes()` 扫描 | 显式注册表（静态数组或 `switch`） |
| 依赖反射的序列化（默认 `JsonSerializer` 泛型重载、`XmlSerializer`） | `System.Text.Json` 源生成器（`JsonSerializerContext`）或 `LittleEndianReader/Writer` 手写 |
| 依赖运行时代码生成的模板/映射库 | 源生成器或手写 |

约定：

* 「按名字找资源」是数据需求而不是类型需求——用 `switch`/字典显式映射，别把名字当类型名用。
* 万不得已需要反射时，加 `[UnconditionalSuppressMessage]` 并写清为什么安全（例如目标类型在同程序集且被显式引用）。
* `InvariantGlobalization` 目前是 `false`（需要 CJK 字符串处理），若将来要 AOT 单文件化需要重新评估。

真实性验证方式（M0 已做一次）：临时加入 `Activator.CreateInstance(Type.GetType(name)!)`，
构建以 `error IL2057` 失败；删除后 `dotnet build --no-incremental` 恢复 0 warning / 0 error。

## 常见任务

```powershell
dotnet build FHeroes2Sharp.slnx            # 编译（警告即错误）
dotnet test  FHeroes2Sharp.slnx            # 全部测试
dotnet run --project tools/FHeroes2.AssetProbe -- info
dotnet run --project tools/FHeroes2.AssetProbe -- list --limit 20
dotnet run --project tools/FHeroes2.AssetProbe -- extract KB.PAL kb.pal
dotnet run --project tools/FHeroes2.AssetProbe -- sprites HEROES.ICN tools-output/heroes
dotnet run --project tools/FHeroes2.AssetProbe -- tiles GROUND32.TIL tools-output/tiles
dotnet run --project tools/FHeroes2.AssetProbe -- cursor ADVMBW01.BMP tools-output/cursor
dotnet run --project tools/FHeroes2.AssetProbe -- tiles GROUND32.TIL tools-output/tiles-bmp --format bmp
dotnet run --project tools/FHeroes2.AssetProbe -- compare tools-output/heroes tools-output/heroes-ref --diff tools-output/diff
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --screenshot out.png
pwsh tools/scripts/BuildReferenceTool.ps1     # 用 MSYS2 UCRT64 的 g++ 编译参考引擎导出器
pwsh tools/scripts/CompareWithReference.ps1   # 14 个代表性条目逐字节比对（C++ ↔ C#）
pwsh tools/scripts/CompareWithReference.ps1 -All   # 归档内全部 ICN/TIL/BMP 条目
pwsh tools/scripts/VerifyMenuBaseline.ps1     # 主菜单像素基线（冻结动画 + 合成光标，退出码 0 = 一致）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update   # 有意改了视觉后重建 tests/baselines/menu.png
pwsh tools/scripts/BuildTranslations.ps1      # 编译并安装 translations/*.po → <data>/files/lang/*.mo
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN -NoInstall   # 只校验简体中文语言包
pwsh tools/scripts/GeneratePaletteTable.ps1   # 从 fheroes2 pal.cpp 重新生成 PaletteTable.cs
```

`compare` 可比较两个 PNG 文件或两个目录（按文件名配对），逐像素统计差异并打印首个差异坐标；
`--diff` 把差异像素标成品红写成 `diff_<name>.png`。退出码：`0` 全同、`3` 有差异、`1` 参数/IO 错误，
方便在脚本里做门禁。`--ignore x,y,w,h`（可重复）把一块矩形排除在比对之外，输出行尾会写
`N pixels ignored`——这是截图能当基线的关键：状态栏的 FPS 与版本号是运行时值，必须排除。
第三方交叉验证的办法：用 Pillow 把导出图重新编码
（`Image.open(p).convert("RGBA").save(q)`，它会用自适应行滤波），再 `compare` 两个目录，
结果应为全同——这能同时验证 `PngWriter` 的规范性与 `PngReader` 的行滤波/调色板展开实现。

导出格式由 `--format` 选择：

| 值 | 说明 |
| --- | --- |
| `png`（默认） | 透明直接写进 alpha，单色光标的形状一目了然，也能被 `compare` 比对 |
| `bmp` | 8 bit 索引位图（同 `SDL_SaveBMP`）；没有 alpha，精灵/光标用 `--background <0..255>` 填充底色（原版推荐 23） |

BMP 的第三方交叉验证同理：`PIL.Image.open("x.bmp")` 只有在文件头、调色板与自下而上的行序都对时
才会给出 `mode == "P"` 与正确的尺寸；`REFERENCE.md` 记录了实测数据（432 个图块 BMP 与 PNG 逐像素一致）。

### 主菜单像素基线

`tests/baselines/menu.png` 是入库的 640×480 基线（背景 + 灯体 + 光晕第 1 帧 + 五块招牌），
`tools/scripts/VerifyMenuBaseline.ps1` 负责「渲染 → 比对 → 退出码」：

```powershell
pwsh tools/scripts/VerifyMenuBaseline.ps1              # 0 = 逐像素一致；1 = 有差异（附差异图路径）
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update      # 有意改了视觉后重建基线
pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData # 自建 runner：缺数据直接失败而不是 skip
```

脚本真正执行的客户端命令就是下面这条，手搓等价：

```powershell
dotnet run --project src/FHeroes2.Client.Desktop -- --data F:\game\fh2 --scene menu --lang en `
    --cursor 620,100 --freeze-animation --screenshot-frame 30 --smoke-frames 90 `
    --screenshot tools-output/baseline/menu.png
FHeroes2.AssetProbe compare tools-output/baseline/menu.png tests/baselines/menu.png --ignore 0,469,640,11
```

三个「必须同时给」的开关：`--freeze-animation`（光晕不跟挂钟）、`--cursor`（人造光标，物理鼠标
不会点亮招牌）、`--ignore 0,469,640,11`（状态栏的 FPS / 版本号）。少任何一个，基线都会随机失败。
基线文件本身的来历与更新规矩写在 `tests/baselines/README.md`。

### 参考引擎对拍（C++ ↔ C#）

`tools/reference/fh2_reference.cpp` 只是一层参数解析：它把自己链接到 fheroes2 的
`agg_file.cpp` / `serialize.cpp` / `zzlib.cpp` / `image.cpp` / `image_palette.cpp` /
`image_tool.cpp`，于是「解码」这一步跑的是**原版实现**，写文件走的是原版 `fheroes2::Save()`
（内部调用 `SDL_SaveBMP`）。因此对拍的是「我们的移植 vs 参考实现」，而不是两份 C# 互相验证。

```powershell
# 1. 编译参考导出器（默认找 F:\code\fheroes2 与 D:\msys64\ucrt64，可用参数覆盖）
pwsh tools/scripts/BuildReferenceTool.ps1 -Fheroes2Source F:\code\fheroes2 -MsysRoot D:\msys64\ucrt64
#    产物：tools-output/reference/fh2_reference.exe（SDL2.dll / SDL3.dll 会一起复制过去）

# 2. 逐字节比对（脚本自己跑两侧工具、用 SHA-256 比较每个 BMP）
pwsh tools/scripts/CompareWithReference.ps1
pwsh tools/scripts/CompareWithReference.ps1 -All -DataDirectory 'D:\HoMM2'
```

* 参考导出器的命令行与 `AssetProbe --format bmp` 一一对应：

  | 参考导出器 | AssetProbe | 产物命名 |
  | --- | --- | --- |
  | `fh2_reference icn <AGG> <NAME.ICN> <OUT> [bg]` | `sprites <NAME.ICN> <OUT> --format bmp --background <bg>` | `<STEM>_000.bmp`、… |
  | `fh2_reference til <AGG> <NAME.TIL> <OUT>` | `tiles <NAME.TIL> <OUT> --format bmp` | `<STEM>_000.bmp`、… |
  | `fh2_reference cursor <AGG> <NAME.BMP> <OUT> [bg]` | `cursor <NAME.BMP> <OUT> --format bmp --background <bg>` | `<STEM>.bmp` |

* 底色必须一致才能比：参考引擎导出精灵/光标时用调色板索引 23，所以脚本传 `--background 23`
  （`AssetProbe` 的默认值是 0，直接对比会全图不同）。
* 目前结果：默认 14 个代表性条目（3 个 TIL + 11 个 ICN/BMP）共 1204 个 BMP **逐字节相同**；
  `-All` 扫描归档中的每个 `.ICN` / `.TIL` / `.BMP` 条目：**958 个条目 / 15,025 个 BMP 全部相同**，
  没有跳过项（两侧都拒绝解码的条目会记为 `skip`）。
* 这项工作替代了原先「只能靠 Pillow 与摘要间接验证」的做法：文件头、调色板（含第 4 字节）、
  行序、变换层合成、RLE 解码全部被钉死。

### 本地化（`.mo`）

语言包沿用 fheroes2 的目录约定：`<data>/files/lang/<culture>.mo`（例如 `zh_CN.mo`）。
仓库里只保存可 diff 的源文件 `translations/<culture>.po`，`.mo` 是构建产物，不入库。

```powershell
pwsh tools/scripts/BuildTranslations.ps1                  # 全部 .po：msgfmt --check 校验后安装
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN   # 只处理简体中文
pwsh tools/scripts/BuildTranslations.ps1 -NoInstall        # 只校验，不往数据目录写文件
```

* `msgfmt` 来自 GNU gettext。不在 `PATH` 时会依次探测 `D:\msys64\usr\bin`、`C:\msys64\usr\bin`，
  也可用 `-MsgfmtPath` 指定。安装目录默认是 `-DataDirectory` → `FHEROES2_DATA` → `F:\game\fh2`。
* `Assets.Localization.MoCatalog` 是 GNU MO 的读取实现，对照 `Translation::MOFile`
  （`src/engine/translations.cpp`）：小端/大端魔数、主版本必须为 0、`msgstr` 用 `\0` 分隔复数形式、
  空的 `msgid` / `msgstr` 跳过、`context|text` 的上下文前缀在显示前剥离。
* **msgid 必须与参考实现的英文原文逐字节一致**（例如 `Okay`、`New Game`、`combatMode|Manual`），
  否则查表失败并回退英文。新增条目时从 `src/fheroes2/**` 里的 `_()` 提取原文，并用
  `files/lang/de.po` 这类完整目录交叉核对（历史上有一个坑：C++ 里字符串跨行拼接时，直接从源码行
  截取会得到半截 msgid）。
* 未翻译的条目**不写进 `.po`**，运行时回退英文——这正是 `Translation::gettext()` 的行为，
  所以 95 条也可以是一个可用的语言包，覆盖范围逐步扩大即可。
* 编码统一 `charset=UTF-8`。参考引擎按 8 bit 代码页（CP1251 等）+ 自带字形渲染，而本项目用
  Unicode + TrueType，因此 `MoCatalog` 只接受 UTF-8/ASCII，其它字符集抛 `NotSupportedException`，
  避免静默乱码。
* 参考引擎本身**无法显示中文**：`fheroes2::SupportedLanguage`（`game_language.h`）没有中文，
  `CodePage` 只有单字节码表，字体是 256 字形的位图字体。所以 `zh_CN.mo` 目前只服务本项目；
  要让 C++ 版也用上，需要改 `SupportedLanguage`/`getCodePage()`、补 CJK 字体与断行，工作量在引擎侧。

环境变量：

| 变量 | 作用 |
| --- | --- |
| `FHEROES2_DATA` | 游戏数据目录（含 `Data/heroes2.agg` 或直接含 `heroes2.agg`） |
| `FHEROES2_TEST_DATA` | 覆盖测试使用的数据目录（默认走 `AssetLocator`） |
| `FHEROES2_FONT` | 覆盖字体文件路径 |
| `FHEROES2_TEXT_CRISP` | `1` = 把字形覆盖率二值化（硬边像素风，配合点阵字体使用） |
| `FHEROES2_LOG_LEVEL` | `Debug` / `Info` / `Warning` / `Error` / `None` |

## 坑位记录

1. **`SDL.Rect` / `SDL.Point` / `SDL.Color` 与引擎同名类型冲突**：客户端的呈现与文本代码里
   写完整限定名（`SDL.Rect`、`FHeroes2.Core.Geometry.Rect`），不要靠 `using` 前缀去猜。
2. **`SDLEvent` 的 `Type` 字段是 `uint`**：绑定的结构体里是 `UInt32 Type`，需要
   `(SDL.EventType)evt.Type` 再 `switch`，直接比较枚举编译不过。
3. **PCG32 的 stream 会被强制为奇数**（`stream | 1`），所以 54 与 55 是同一流。
4. **`KB.PAL` 是 6 bit**：直接当 8 bit 用会得到一张极暗的调色板（最大分量 63）。
5. **AGG 头部第一个 `uint16` 是条目数**，用脚本读取时注意字节序累加；错读会得到 154 这类假数据。
6. **`.ttc` 字体集合**：SDL_ttf 打开的是第 0 个字面；`simsun.ttc` 的第 0 个正是 SimSun，
   12 px 有内嵌点阵，小字号最锐利。
7. **依赖真实数据的测试**：`FHeroes2.Assets.Tests` 找不到数据时会**提前返回**（xunit 2 无 `Assert.Skip`），
   因此务必关注测试总数与 `PROGRESS.md` 中的实测记录。
8. **`--smoke-frames` 的前几帧 FPS 偏低**：`RenderPresent` 受垂直同步影响，启动阶段还在等首个 vsync，
   平滑后的数值要 0.25 s 之后才稳定。
9. **PowerShell 直接调用 MSYS2 的 `g++.exe` 会静默失败**：退出码 1、没有任何诊断输出。原因是从
   `libexec/gcc/...` 启动的 `cc1plus` / `ld` 找不到运行时 DLL，必须先把 `<msys2>/ucrt64/bin`
   放进 `PATH`（`BuildReferenceTool.ps1` 已处理）。若在 MSYS2 的 bash 里编译则天然没问题。
10. **SDL 写的 BMP 调色板第 4 字节不是 0**：`SDL_SaveBMP` 把 `SDL_Color.a` 写进去，而
    `fheroes2::SaveImage()` 把每个条目的 alpha 都设为 255。`BmpWriter` 也写 255，否则与参考导出
    差 256 个字节（每条目一个），阅读方（Pillow、Windows 图片查看器）并不关心这个字节。
11. **参考导出器不会自动建目录**：`fheroes2::Save()` 直接交给 `SDL_SaveBMP`，目录不存在就失败。
    `CompareWithReference.ps1` 在调用前会 `New-Item -ItemType Directory`。
12. **`msgfmt` 的统计信息会跟着系统语言变**：中文 Windows 上输出「95 条已翻译信息」，脚本里按
    `translated message` 匹配会什么都抓不到。`BuildTranslations.ps1` 在调用期间把 `LC_ALL` 设成 `C`。
    另外 `2>&1` 重定向出来的 stderr 是 `ErrorRecord`，取属性前要先 `[string]` 转换。
13. **`msgfmt` / `msgunfmt` 能当独立校验器**：`msgunfmt <mo>` 能把 `.mo` 反编译回 `.po`，
    条目数与译文都对得上就说明文件本身合法（与本项目 `MoCatalog` 的读取结果互相独立）。
14. **`HEROES.ICN` 在资料片里有第二份**：`heroes2x.agg` 的同名条目是 PoL 版主菜单（横幅写着
    *The Price of Loyalty*，且带变换层）。`AssetBundle.TryRead` 优先取资料片，与 `AGG::GetICN`
    一致，所以主菜单截图/像素基线必须注明数据版本；只有基础版数据时才是原版街景。
15. **按钮表的状态帧不是「一张图缩放」**：`BTNSHNGL.ICN` 每块招牌 4 帧，released 从 **1** 开始
    （0 是预备的禁用帧），悬停 `+1`、按下 `+2`；同一组帧的尺寸还可能差 1 px（HIGH SCORES 组是
    80×75 / 80×76），所以位置只能看 ICN 头里的 `OffsetX/OffsetY`，不能假设同名精灵等大。
16. **`Add-Type` 会锁住程序集**：在 PowerShell 里 `Add-Type -Path src\FHeroes2.Core\bin\...dll`
    之后，下一次 `dotnet build` 会在拷贝 `FHeroes2.Core.dll` 时失败（MSB3027/MSB3021，提示文件被
    `pwsh` 占用）。要么换一个进程做命令行验证，要么先关掉那个 shell 再构建。
17. **ICN 里没有「动画」这件事**：ICN 头第 9 字节那个 `frames` 字段不参与动画，`fheroes2` 用的是
    `ICN::getAnimatedIcnIndex()` 里几百行硬编码帧表（`SHNGANIM` = `1 + frame % 39`），每帧时长则来自
    `Game::DelayType`（主菜单 250 ms）。所以「哪些精灵是一段动画、多久一帧」只能抄参考实现的表，
    不能从文件里推导。
18. **`SHNGANIM` 的第 0 帧不是「动画第一帧」**：它是熄灯灯体（139×131 @ 46,176），而第 1…39 帧是
    统一 136×180 @ 48,183 的光晕层——尺寸、锚点都不同，所以必须画两次。光晕帧间差异只有
    8–942 像素（整屏的 0.003%–0.3%），「没接上」和「接上了」在单张截图上几乎看不出来，
    要比对就得用 `--screenshot-frame` 抓两个不同帧再 diff（第 1 帧 vs 第 11 帧差 615 像素）。
19. **`--screenshot` 默认在第 30 帧抓图**：抓到动画第几帧取决于机器帧率（30 帧 ≈ 0.3 s，光晕在
    第 1/2 帧之间摆动）。要做像素基线就加 `--freeze-animation`；`--cursor` 同理，给定后人造坐标
    会屏蔽真实鼠标事件。
20. **截图里的状态栏是运行时值**：右侧那行 `FPS | 语言 (F2) | vX.Y.Z` 会随帧率与 `VersionPrefix`
    变化，逐像素比对必然对不上。`AssetProbe compare --ignore 0,469,640,11` 把它排除掉之后，
    连续两次渲染的 `menu.png` 才是逐字节相同的（`VerifyMenuBaseline.ps1` 就是这么做的）。
21. **`(Get-Content x).Count` 与 `Measure-Object -Line` 数出来的行号不一样**：后者**不计空行**，
    脚本按它算插入位置会把代码插进函数体中间（表现为「脚本安静地什么都不做就 exit 0」）。
    需要行号时用 `(Get-Content -LiteralPath x).Count`，或者干脆按唯一文本做锚点替换。

## 目录与产物

| 路径 | 说明 |
| --- | --- |
| `assets/fonts/` | 可再分发字体（SIL OFL）才会入库；当前为空，运行时回退系统字体 |
| `tools/reference/` | 参考引擎导出器的 C++ 源码（链接 fheroes2 源码，不复制其代码） |
| `tools/scripts/` | 生成/校验脚本：变换层转换表、重着色调色板表、参考导出器编译、参考对拍、语言包编译、主菜单像素基线 |
| `tests/baselines/` | **入库**的回归基线 PNG（`menu.png` 等），来历与更新规矩见其中的 `README.md` |
| `translations/` | 语言包源文件（`.po`，UTF-8）；`.mo` 编译产物装到数据目录，不入库 |
| `tools-output/` | 截图、导出产物、编译出的参考导出器，已在 `.gitignore` 中忽略 |
| `data/` | 本地游戏数据（若复制进仓库目录），已忽略 |

