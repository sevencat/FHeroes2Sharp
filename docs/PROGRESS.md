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
| `tools/FHeroes2.AssetProbe` | ✅ | `list` / `info` / `extract` 三个子命令 |
| 测试 | ✅ | 54 个（Core 40 / Assets 14，全部通过；支持真实数据时含 1434 条哈希集成校验） |
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
3. 未接入 `.mo` 本地化：诊断界面直接写死中英文字符串，回退规则为「字体支持 CJK 则中文，否则英文」。
4. 未生成与 C++ 的 PCG32 黄金向量对比（需要构建参考二进制）；当前用性质测试 + 可复现性测试。
5. 帧率在启动瞬间偏低（着色器/纹理首次创建），稳定后受垂直同步约束；`--smoke-frames` 的 FPS 不是基准。

## 后续里程碑

见 [PLAN.md](PLAN.md)。M1 起点：ICN/TIL 解码与变换层，配套「C++ 导出 ↔ C# 解码」像素比对工具。
