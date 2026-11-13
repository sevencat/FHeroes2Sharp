# 架构（ARCHITECTURE）

## 分层

| 程序集 | 职责 | 允许依赖 |
| --- | --- | --- |
| `FHeroes2.Core` | 几何、随机数、二进制 IO、索引图像与调色板、精灵与变换层、PNG 导出/读回、日志、计时、版本信息 | 无 |
| `FHeroes2.Assets` | AGG 归档、资源定位、资源聚合（base + 资料片）、ICN 精灵与 TIL 图块解码 | Core |
| `FHeroes2.Rendering` | 呈现与文本的后端无关抽象 | Core |
| `FHeroes2.Game` | 世界状态、回合、AI、存档 | Core, Assets, Rendering |
| `FHeroes2.UI` | 对话框栈、控件、界面流转 | Core, Rendering, Game |
| `FHeroes2.Client.Desktop` | MonoGame 宿主、SDL 呈现、字体、场景、命令行 | 全部 |

依赖方向单向；`Core` 不得引用任何 MonoGame 类型，这样解码与算法可以在无 GPU 的测试进程里跑。

## 帧数据流（M0 已贯通）

```
数据目录解析  AssetLocator            --data / FHEROES2_DATA / exe 目录 / 已知安装位置
      ↓
归档读取      AssetArchive            heroes2.agg(1434) + heroes2x.agg(52)，逐条名称哈希校验
      ↓
调色板        Palette.FromGamePalette KB.PAL：6 bit → <<2，并复制水/岩浆循环色
      ↓
场景绘制      ScreenBuffer(640×480, byte) + ImageOps    纯 CPU、索引色、无 GC 分配
      ↓
脏区上传      MonoGamePresenter.Upload  Color[256] 查表 → Texture2D.SetData(脏矩形)
      ↓
逻辑合成      RenderTarget2D 640×480  帧 + 文本叠加（FontStashSharp，1x 逻辑像素）
      ↓
呈现          PointClamp + 整数倍放大 + 居中黑边 → 背缓冲
```

放大的数学在 `PresentationViewport.Compute` 中：窗口 ≥ 640×480 时取整数倍并居中；
窗口更小时退化为等比缩放（浮点），保证不裁切。

## 组合根与生命周期

`GameHost`（MonoGame `Game`）是唯一的组合根：

```
Initialize()   → 打开 AssetBundle（失败则记错误、ExitCode=1、使用灰度调色板继续）
LoadContent()  → SpriteBatch / MonoGamePresenter / FontStashTextRenderer / DiagnosticsScene
Update()       → FrameTimer、场景动画、输入（ESC/F11/F12）、smoke 帧数控制
Draw()         → 场景绘制 → 上传 → 逻辑 RT 合成 → 文本 → 截图 → 呈现
UnloadContent()→ 逆序释放全部 IDisposable（文本、呈现器、SpriteBatch、归档）
```

不使用 DI 容器；所有依赖在构造函数中显式传入。

## 文本

`ITextRenderer` 只暴露 `Name` 与 `Measure(string, TextStyle)`，M0 实现为
`FontStashTextRenderer`（FontStashSharp，TrueType，2048² 动态图集）。
绘制入口 `Draw(SpriteBatch, text, Point, TextStyle)` 属于客户端层，因为它需要 GPU 上下文。
M2 将加入 `AggBitmapTextRenderer`（`BIGFONT.FNT`/`SMALFONT.FNT`），并在此基础上把绘制方法
上移为后端无关接口。

字体解析顺序：`FHEROES2_FONT` → `assets/fonts/*.ttf|otf` → Windows 系统字体
（`simhei.ttf` → `msyh.ttc` → `simsun.ttc` → `arialuni.ttf` → `segoeui.ttf` → `arial.ttf`）。
若最终字体不含 CJK，诊断文本自动切换为英文。

## 错误处理

* 外部数据格式错误 → `BinaryFormatException`（`Core.Io`），消息包含文件名、条目名与偏移。
* 归档缺失/损坏 → `AssetBundle.Open` 抛 `DirectoryNotFoundException` / `FileNotFoundException`，
  客户端降级运行并设置 `ExitCode = 1`；资料片损坏只记录警告（与原版行为一致：资料片可选）。
* 日志 → `GameLog`（`FHEROES2_LOG_LEVEL` 可调），`Warning` 及以上立即 flush。

## 线程模型

M0 全单线程。计划中的并行点：音频流播放（独立线程 + 环形缓冲）、
资源解码（`Parallel.For` 解码 ICN 图集）、AI 评估（不可访问渲染状态）。
所有 `AssetArchive` 的读取都经过内部锁，允许后续从其他线程读取原始字节。
