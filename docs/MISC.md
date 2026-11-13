# 杂项（MISC）

## 构建环境

| 项 | 值 |
| --- | --- |
| SDK | .NET 10（`global.json` 固定 10.0.101，`rollForward: latestFeature`；本机实际使用 10.0.402） |
| 解决方案 | `FHeroes2Sharp.slnx`（SDK 10 默认 XML 格式） |
| 渲染 | MonoGame.Framework.DesktopGL 3.8.6.1 |
| 字体 | FontStashSharp.MonoGame 1.6.2 |
| 测试 | xunit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Microsoft.NET.Test.Sdk 18.12.0 |
| 包管理 | 集中版本（`Directory.Packages.props`，`ManagePackageVersionsCentrally`） |
| AOT / 裁剪 | 已开启 `IsAotCompatible` + `IL2xxx`/`IL3xxx`（警告即错误）；**不提供** `PublishAot` 发布（ADR-011） |

MonoGame 模板未安装，所有 csproj 均手写，也不使用 MGCB 内容管线。

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
```

`compare` 可比较两个 PNG 文件或两个目录（按文件名配对），逐像素统计差异并打印首个差异坐标；
`--diff` 把差异像素标成品红写成 `diff_<name>.png`。退出码：`0` 全同、`3` 有差异、`1` 参数/IO 错误，
方便在脚本里做门禁。第三方交叉验证的办法：用 Pillow 把导出图重新编码
（`Image.open(p).convert("RGBA").save(q)`，它会用自适应行滤波），再 `compare` 两个目录，
结果应为全同——这能同时验证 `PngWriter` 的规范性与 `PngReader` 的行滤波/调色板展开实现。

导出格式由 `--format` 选择：

| 值 | 说明 |
| --- | --- |
| `png`（默认） | 透明直接写进 alpha，单色光标的形状一目了然，也能被 `compare` 比对 |
| `bmp` | 8 bit 索引位图（同 `SDL_SaveBMP`）；没有 alpha，精灵/光标用 `--background <0..255>` 填充底色（原版推荐 23） |

BMP 的第三方交叉验证同理：`PIL.Image.open("x.bmp")` 只有在文件头、调色板与自下而上的行序都对时
才会给出 `mode == "P"` 与正确的尺寸；`REFERENCE.md` 记录了实测数据（432 个图块 BMP 与 PNG 逐像素一致）。

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

环境变量：

| 变量 | 作用 |
| --- | --- |
| `FHEROES2_DATA` | 游戏数据目录（含 `Data/heroes2.agg` 或直接含 `heroes2.agg`） |
| `FHEROES2_TEST_DATA` | 覆盖测试使用的数据目录（默认走 `AssetLocator`） |
| `FHEROES2_FONT` | 覆盖字体文件路径 |
| `FHEROES2_LOG_LEVEL` | `Debug` / `Info` / `Warning` / `Error` / `None` |

## 坑位记录

1. **`FHeroes2.Game` 命名空间遮蔽 `Microsoft.Xna.Framework.Game`**
   在 `FHeroes2.*` 命名空间内写 `: Game` 会解析成命名空间。解决：客户端里用别名
   `using XnaGame = Microsoft.Xna.Framework.Game;`。
2. **`TextStyle` / `Point` 名称冲突**：`FontStashSharp.TextStyle`、`Microsoft.Xna.Framework.Point`
   与引擎同名类型冲突，客户端用 `using` 别名显式指向引擎类型。
3. **PCG32 的 stream 会被强制为奇数**（`stream | 1`），所以 54 与 55 是同一流。
4. **`KB.PAL` 是 6 bit**：直接当 8 bit 用会得到一张极暗的调色板（最大分量 63）。
5. **AGG 头部第一个 `uint16` 是条目数**，用脚本读取时注意字节序累加；错读会得到 154 这类假数据。
6. **`.ttc` 字体集合**：FontStashSharp 对 TTC 支持有限，加载失败会自动尝试下一个候选字体。
7. **依赖真实数据的测试**：`FHeroes2.Assets.Tests` 找不到数据时会**提前返回**（xunit 2 无 `Assert.Skip`），
   因此务必关注测试总数与 `PROGRESS.md` 中的实测记录。
8. **MonoGame 窗口在后台不活跃时**会按 `InactiveSleepTime` 限速，`--smoke-frames` 前几帧 FPS 偏低属正常。
9. **PowerShell 直接调用 MSYS2 的 `g++.exe` 会静默失败**：退出码 1、没有任何诊断输出。原因是从
   `libexec/gcc/...` 启动的 `cc1plus` / `ld` 找不到运行时 DLL，必须先把 `<msys2>/ucrt64/bin`
   放进 `PATH`（`BuildReferenceTool.ps1` 已处理）。若在 MSYS2 的 bash 里编译则天然没问题。
10. **SDL 写的 BMP 调色板第 4 字节不是 0**：`SDL_SaveBMP` 把 `SDL_Color.a` 写进去，而
    `fheroes2::SaveImage()` 把每个条目的 alpha 都设为 255。`BmpWriter` 也写 255，否则与参考导出
    差 256 个字节（每条目一个），阅读方（Pillow、Windows 图片查看器）并不关心这个字节。
11. **参考导出器不会自动建目录**：`fheroes2::Save()` 直接交给 `SDL_SaveBMP`，目录不存在就失败。
    `CompareWithReference.ps1` 在调用前会 `New-Item -ItemType Directory`。

## 目录与产物

| 路径 | 说明 |
| --- | --- |
| `assets/fonts/` | 可再分发字体（SIL OFL）才会入库；当前为空，运行时回退系统字体 |
| `tools/reference/` | 参考引擎导出器的 C++ 源码（链接 fheroes2 源码，不复制其代码） |
| `tools/scripts/` | 生成/校验脚本：调色板转换表、参考导出器编译、参考对拍 |
| `tools-output/` | 截图、导出产物、编译出的参考导出器，已在 `.gitignore` 中忽略 |
| `data/` | 本地游戏数据（若复制进仓库目录），已忽略 |

