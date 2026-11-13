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
dotnet run --project src/FHeroes2.Client.Desktop -- --smoke-frames 40 --screenshot out.png
```

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

## 目录与产物

| 路径 | 说明 |
| --- | --- |
| `assets/fonts/` | 可再分发字体（SIL OFL）才会入库；当前为空，运行时回退系统字体 |
| `tools-output/` | 截图、导出产物，已在 `.gitignore` 中忽略 |
| `data/` | 本地游戏数据（若复制进仓库目录），已忽略 |
