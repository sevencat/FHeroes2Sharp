# 像素基线（tests/baselines）

这里存放**必须入库**的回归基线 PNG。`tools-output/` 是临时产物目录（`.gitignore` 排除），
游戏数据（`Heroes2.agg`）同样不入库（ADR-010），所以真正用来兜底的基线只有这里的几张图。

| 文件 | 内容 | 尺寸 | 生成方式 |
| --- | --- | --- | --- |
| `menu.png` | 主菜单首屏：背景 + 灯体 + 光晕第 1 帧 + 五块招牌（未悬停） | 640×480 | `pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update` |

## 怎么用

```powershell
pwsh tools/scripts/VerifyMenuBaseline.ps1              # 渲染 + 比对，退出码 0 = 一致
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update      # 有意改了视觉后重建基线
pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData # 自建 runner 上把「没数据」当失败
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Tolerance 16 # 容忍少量像素漂移
```

脚本做的事：先做**基线自检**（PNG 魔数 + `IHDR` 必须是 640×480，失败即退出 `1`），
再 `dotnet build` → 用 `--freeze-animation --cursor 620,100 --screenshot-frame 30`
渲染一帧 → 交给 `AssetProbe compare` 与基线逐像素比对（差异图写到
`tools-output/baseline/diff/diff_menu.png`，退出码 `1` 表示超阈值）。
找不到 `Heroes2.agg` 时打印 `skip` 并以 `0` 退出，与「依赖真实数据的 xunit 用例」策略一致
（ADR-010）；只有显式给 `-RequireData` 才把缺数据当失败。

## CI 怎么接

`.github/workflows/ci.yml` 的 `menu-baseline` job 直接调用这个脚本：

* 托管 runner（`windows-latest`）没有游戏数据，脚本会停在自检之后：**基线文件本身**被校验
  （存在、是 PNG、640×480），像素比对退化为 `skip`。这一步拦的是「基线被删/被换错尺寸」。
* 要真正跑像素比对，需要一台装了数据的自建 runner：把仓库变量 `FHEROES2_RUNNER` 设成它的标签
  （例如 `fh2-data`）、`FHEROES2_DATA` 设成数据目录（例如 `D:\game\fh2`）。两个变量都在时，
  job 才会传 `-DataDirectory` 与 `-RequireData`（缺数据即失败，而不是静默 skip）。
* 失败时 job 会把 `tools-output/baseline/menu.png`（刚渲染的图）与 `diff/diff_menu.png`
  作为 artifact `menu-baseline-diff` 上传。

## 为什么逐字节一致是可达的

* `--freeze-animation`：灯笼光晕固定在第 1 帧，不再跟挂钟走（见 `PROGRESS.md` 的灯笼光晕记录）。
* `--cursor 620,100`：给出人造光标后宿主忽略真实指针事件，物理鼠标不会把招牌点亮。
* 渲染本身是确定性的：同一份数据 + 同一份代码，连续两次运行得到的 `menu.png` 逐字节相同
  （实测两次都是 `identical`）。

## 唯一被忽略的区域：底部状态栏

`--ignore 0,469,640,11`。状态栏右侧印着 `FPS | 语言 (F2) | v0.1.0`：FPS 是运行时值，
版本号随 `Directory.Build.props` 的 `VersionPrefix` 变化，两者都不能钉死。忽略后参与比对的
是 **300,160** 个像素（307200 − 7040），即背景、灯体、光晕与五块招牌；状态栏本身靠人工看
`tools-output/menu-hover.png` 之类的截图来核对。

## 基线的数据版本

当前 `menu.png` 用**资料片（PoL）数据**渲染：`F:\game\fh2\Data` 里同时存在 `heroes2.agg` 与
`heroes2x.agg`，`AssetBundle` 优先取后者，因此 `HEROES.ICN` 是 *The Price of Loyalty* 横幅版本。
只有基础版数据（原版街景背景）时基线必然对不上——那种情况应当另存一份（例如
`menu-classic.png`）而不是覆盖它。

* 字节数：239,932
* SHA-256：`73C1A211A86C0BCD2C10B47C32F4B7537B007498F9D325ED9004FC26EFC1E563`

## 更新基线的规矩

1. 只有在**有意**改变视觉时才 `-Update`（美术位置、绘制顺序、调色板、字体……）。
2. 同一个提交里说明变了什么、为什么变；顺便更新本文的字节数 / SHA-256 与数据版本。
3. 更新后立刻跑一次不带 `-Update` 的校验，确认可复现。
4. 纯重构（像素不该变）**不要**更新基线——它存在的意义就是拦下「不小心改了像素」。
