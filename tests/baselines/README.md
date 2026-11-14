# 像素基线（tests/baselines）

这里存放**必须入库**的回归基线 PNG。`tools-output/` 是临时产物目录（`.gitignore` 排除），
游戏数据（`Heroes2.agg`）同样不入库（ADR-010），所以真正用来兜底的基线只有这里的几张图。

| 文件 | 内容 | 尺寸 | 生成方式 |
| --- | --- | --- | --- |
| `menu.png` | 主菜单首屏：背景 + 灯体 + 光晕第 1 帧 + 五块招牌（未悬停、门未点亮） | 640×480 | `pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update` |
| `settings.png` | 主菜单 + 设置对话框（`en`，语言行未悬停，OK 未按下） | 640×480 | `pwsh tools/scripts/VerifySettingsBaseline.ps1 -Update` |
| `settings.zh_CN.png` | 同上，界面语言 `zh_CN`（`设置` / `语言` / `简体中文`） | 640×480 | `pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN -Update` |
| `scenario.png` | 主菜单 + 场景选择窗口（`en`：`Alteris` 行选中、`Hard` 难度、ALL 尺寸筛选） | 640×480 | `pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Update` |
| `scenario.zh_CN.png` | 同上，界面语言 `zh_CN`（`地图难度：困难`） | 640×480 | `pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Culture zh_CN -Update` |
| `adventure-map.png` | 冒险地图（M4，地形 + 对象层 + 英雄层 + 小地图）：`Alteris.mp2` 以「最靠地图中心的城堡」居中，含城镇（门口的紫色亡灵英雄）、树林、山脊、海岸与沙滩；右上角 144×144 雷达面板带视口虚线框 | 640×480 | `pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update` |
| `editor.png` | 地图编辑器（E1/E2）：`Alteris.mp2` 同上居中，顶部 `MAP EDITOR` 工具条、光标双线高亮、tile 网格、小地图，以及一次脚本化笔刷（`46,52,6,4,Snow`）刷出的雪地——雪块四周是真正的雪/草/水过渡，不是硬边 | 640×480 | `pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update` |

## 怎么用

```powershell
pwsh tools/scripts/VerifyMenuBaseline.ps1                   # 主菜单：0 = 一致
pwsh tools/scripts/VerifySettingsBaseline.ps1               # 设置对话框（en）：0 = 一致
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN  # 设置对话框（中文）
pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN -Update   # 有意改了视觉后重建基线
pwsh tools/scripts/VerifyScenarioBaseline.ps1               # 场景选择（en）：0 = 一致
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Culture zh_CN
pwsh tools/scripts/VerifyScenarioBaseline.ps1 -Update       # 有意改了视觉后重建场景基线
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1           # 冒险地图：0 = 一致
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update   # 有意改了视觉后重建冒险地图基线
pwsh tools/scripts/VerifyEditorBaseline.ps1                 # 地图编辑器：0 = 一致
pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update         # 有意改了视觉后重建编辑器基线
pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData      # 自建 runner 上把「没数据」当失败
pwsh tools/scripts/VerifyMenuBaseline.ps1 -Tolerance 16     # 容忍少量像素漂移
```

五个脚本共用 `tools/scripts/PixelBaseline.ps1`：数据目录查找、`dotnet build`、基线自检、
`AssetProbe compare` 与容差判定都在那里，脚本本身只剩「哪些开关让这一帧可复现」与
「哪块区域要忽略」。所以五个脚本的命令行、退出码与失败输出格式完全一致。

脚本做的事：先做**基线自检**（PNG 魔数 + `IHDR` 必须是 640×480，失败即退出 `1`），
再 `dotnet build` → 用 `--freeze-animation --cursor 620,100 --screenshot-frame 30`
渲染一帧（设置基线多一个 `--open-settings`，场景基线多一个 `--open-scenario`）→ 交给
`AssetProbe compare` 与基线逐像素比对（差异图写到 `tools-output/baseline/diff/diff_menu.png`、
`diff_settings.png`、`diff_scenario.png`、`diff_adventure-map.png`、`diff_editor.png`，退出码 `1` 表示超阈值）。找不到 `Heroes2.agg` 时
打印 `skip` 并以 `0` 退出，与「依赖真实数据的 xunit 用例」策略一致（ADR-010）；只有显式给
`-RequireData` 才把缺数据当失败。

## 设置基线为什么也是逐字节稳定的

* `--open-settings`：模态框只能点门打开，脚本没有鼠标，这个开关把它直接放到屏幕上
  （设置对话框本身没有动画，`--freeze-animation` 只是把框后的灯晕钉在第 1 帧）。
* `--cursor 620,100`：在窗口占的 `138,181 348×133` 之外，所以行文字、箭头与 OK 按钮都停在
  静止态。要抓「按下的 OK」就另存一张，别覆盖这张。
* `--lang en`：行里的文字是界面文字，**语言属于基线**。脚本按 `-Culture` 选文件：`en` 是
  `settings.png`，其它语言是 `settings.<culture>.png`（当前入库 `settings.zh_CN.png`）。换语言
  等于换基线，不要在同一份文件上来回覆盖。
* 行文字超宽时被裁在 `RowPadding` 之内（值还额外被推回左内边距），所以译文变长只会「截断」，
  不会把文字画到边框或窗口外——`SettingsDialogTests` 用 40 个字符宽的假译文钉住了这一点。
* 状态栏同样用 `--ignore 0,469,640,11` 排除。

## 场景基线为什么也是逐字节稳定的

* `--open-scenario`：窗口只能点 NEW GAME 招牌打开，脚本没有鼠标，这个开关直接把它放到屏幕上
  （`--freeze-animation` 把框后的灯晕钉在第 1 帧）。
* 面板落在 `115,17 377×458`，`--cursor 620,100` 在它之外，所以尺寸按钮、滚动箭头、OKAY 与
  列表行都停在静止态。
* 列表内容来自数据目录：首个地图文件（按 `MapCatalog` 的顺序是 `Alteris`）被选中，`ALL`
  筛选处于按下态；地图名、难度与描述都取自该文件，所以基线也钉住了「列表 → 详情」这条路径。
* 产品徽章（地图类型列）是运行时从 `ARTFX.ICN` 帧 6/90/101 缩放到 15×15 合成的，和参考的
  `ICN::MAP_TYPE_ICON` 一致；缺扩展包时该列留空，基线会跟着变，重建前先确认数据完整。
* 状态栏同样用 `--ignore 0,469,640,11` 排除。

## 冒险地图基线为什么也是逐字节稳定的

* `--map <file>`：直接打开一张地图，不经过 NEW GAME 与场景列表，所以基线只依赖**一个**地图文件
  （默认 `Alteris.mp2`），与安装了多少地图、地图目录叫什么无关。
* 视口居中到「最靠地图中心的城堡」：M4 还没有英雄可以居中，这是 `AdventureMapScene.TryCreate`
  的占位规则，因此滚动偏移由地图文件唯一决定。
* 地形图来自 `GROUND32.TIL`，四个 shape 变体（原图 / 上下 / 左右 / 两者）在启动时预生成再拷贝，
  不经过任何时钟，所以同一台机器上逐帧相同。
* 对象层不带时钟：`--freeze-animation` 下帧号直接用 MP2 文件里存的静态帧（参考的动画表还没移植，
  ADR-036），所以城镇、树林、山脊的位置与帧号也逐字节稳定。
* 英雄同样不带时钟：身体、旗帜与阴影一律取第 18 帧（朝右站立，ADR-037），颜色与种族从 tile 里的
  `MINIHERO` 帧号反解，所以地图上的英雄位置与外观由地图文件唯一决定。
* 小地图（`480,16`，144×144）只由 tile 字段与英雄列表算出（ADR-038）：地面色来自 terrain image
  index，山树提亮三档，道路单色，英雄取王国色，视口方框由滚动偏移决定——没有时钟，也没有 `ADVBORD`
  面板美术（那块属于界面外壳，还没移植）。
* 状态栏（`0,469,640,11`）里印着视口偏移与版本号，后者每次发版都会变，所以排除掉。地图内容本身
  是数据文件的字节，不受语言影响；`-Culture` 只决定文件名，为与其它脚本对称而保留。
* 这张基线钉住的是「MP2 tile/addon 字段 → ICN/TIL 索引 → 像素」这条链：`Mp2MapTests` 钉住结构解析，
  `AdventureMapViewTests` 钉住滚动数学，`ObjectLayerTests` 钉住部件顺序与落位，`HeroLayerTests`
  钉住英雄的三带切分与阴影次序，`MinimapTests` 钉住地面分段与方框几何，一起才能把这一屏钉死。

## 编辑器基线也是逐字节稳定的

* 与冒险地图同一张图（`Alteris`，居中规则相同），多出来的只有编辑器自己画的东西：tile 网格、光标
  双线高亮、顶部工具条与底部状态栏、小地图。它们都不带时钟。
* `--editor-paint 46,52,6,4,Snow`：脚本先刷一块雪地再截帧。没有这一笔，基线分不出「编辑器能改 tile」
  与「只是把地图画出来了」。
* 状态栏（`0,469,640,11`）被忽略：那里印着 `scroll`、撤销步数与版本号。**顶部工具条不在忽略之列**，
  因为它只有英文常量——改工具条文字就要重建这张基线（ADR-045 把工具名从工具条挪到状态栏时就是这么做的）。
* 对象工具（`O` / `X`）不在这张基线里：它需要按键，而脚本没有键盘。对象的验证走
  `--editor-object` + `--editor-place` 的截图与 `MapEditSessionTests`，见 `PROGRESS.md` 的 E2 记录。

## CI 怎么接

`.github/workflows/ci.yml` 的 `menu-baseline` job 依次调用七条命令（job 名是历史原因，现在跑的是
七份基线：菜单、设置对话框 `en` 与 `zh_CN`、场景选择窗口 `en` 与 `zh_CN`、冒险地图、地图编辑器各一次）：

* 托管 runner（`windows-latest`）没有游戏数据，脚本会停在自检之后：**基线文件本身**被校验
  （存在、是 PNG、640×480），像素比对退化为 `skip`。这一步拦的是「基线被删/被换错尺寸」。
* 要真正跑像素比对，需要一台装了数据的自建 runner：把仓库变量 `FHEROES2_RUNNER` 设成它的标签
  （例如 `fh2-data`）、`FHEROES2_DATA` 设成数据目录（例如 `D:\game\fh2`）。两个变量都在时，
  job 才会传 `-DataDirectory` 与 `-RequireData`（缺数据即失败，而不是静默 skip）。
* 失败时 job 会把 `tools-output/baseline/*.png`（刚渲染的图，文件名带语言后缀）与
  `diff/` 目录（`diff_menu.png` / `diff_settings.png` / `diff_settings.zh_CN.png` /
  `diff_scenario.png` / `diff_scenario.zh_CN.png` / `diff_adventure-map.png` / `diff_editor.png`）作为
  artifact `pixel-baseline-diff` 上传。

## 为什么逐字节一致是可达的

* `--freeze-animation`：灯笼光晕固定在第 1 帧，不再跟挂钟走（见 `PROGRESS.md` 的灯笼光晕记录）。
* `--cursor 620,100`：给出人造光标后宿主忽略真实指针事件，物理鼠标不会把招牌点亮。这个点也在
  设置门的命中区（`63,202 108×160`）与设置窗口（`138,181 348×133`）之外，所以基线里既没有
  门光也没有对话框的 hover 态——两者都要用单独截图验证。
* 门禁**不带** `--right-button`（那是抓右键提示框用的测试钩子）：基线的语义是「一帧普通主菜单」，
  提示框与门光一样属于按需渲染，验证方式见 `PROGRESS.md` 的右键提示框记录。
* 渲染本身是确定性的：同一份数据 + 同一份代码，连续两次运行得到的 PNG 逐字节相同
  （`menu.png`、`settings.png` 与 `settings.zh_CN.png` 都实测两次 `identical`）。

## 唯一被忽略的区域：底部状态栏

`--ignore 0,469,640,11`。状态栏右侧印着 `FPS | 语言 (F2) | v0.1.0`：FPS 是运行时值，
版本号随 `Directory.Build.props` 的 `VersionPrefix` 变化，两者都不能钉死。忽略后参与比对的
是 **300,160** 个像素（307200 − 7040），即背景、灯体、光晕、招牌与对话框；状态栏本身靠人工看
`tools-output/menu-hover.png` 之类的截图来核对。

## 基线的数据版本

当前七张基线都用**资料片（PoL）数据**渲染：`F:\game\fh2\Data` 里同时存在 `heroes2.agg` 与
`heroes2x.agg`，`AssetBundle` 优先取后者，因此 `HEROES.ICN` 是 *The Price of Loyalty* 横幅版本。
只有基础版数据（原版街景背景）时基线必然对不上——那种情况应当另存一份（例如
`menu-classic.png`）而不是覆盖它。`settings.zh_CN.png` 还额外依赖 `files/lang/zh_CN.mo`
（`pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN` 生成并安装），译文改动会让它对不上：
这是有意的，改译文就该重建这张基线。

| 文件 | 字节数 | SHA-256 |
| --- | --- | --- |
| `menu.png` | 239,932 | `73C1A211A86C0BCD2C10B47C32F4B7537B007498F9D325ED9004FC26EFC1E563` |
| `settings.png` | 231,545 | `AFBCFAEA28F4100423DA102E018FBEB0DBEB3BB44003691BA639E7AC6900DE32` |
| `settings.zh_CN.png` | 231,317 | `E8E317A96E8849121C796B8FF237B49823A31F64792603C0E8B4BF234DDD94C4` |
| `scenario.png` | 212,376 | `278483E27CD589F995131ECC8E45559083D2890947440F1F5A1AB4ECE82A2E25` |
| `scenario.zh_CN.png` | 212,398 | `FCA0DC303A70C94C29A6610A620FB2A628EC66F5730EEBE3B3352D0A7183BC67` |
| `adventure-map.png` | 168,285 | `DCFAD127EA40D3062EE6BE40C54C5558303603D6544B64293204CAD93EDF89E4` |
| `editor.png` | 108,862 | `46B35BD4B26E18745368FEF686A5D98B2305A9BA325EB3AAAB778ECF88051CE3` |

## 更新基线的规矩

1. 只有在**有意**改变视觉时才 `-Update`（美术位置、绘制顺序、调色板、字体……）。
2. 同一个提交里说明变了什么、为什么变；顺便更新本文的字节数 / SHA-256 与数据版本。
3. 更新后立刻跑一次不带 `-Update` 的校验，确认可复现。
4. 纯重构（像素不该变）**不要**更新基线——它存在的意义就是拦下「不小心改了像素」。
5. 设置基线的文字随语言变化：换语言就等于换基线，不要在同一份文件上来回覆盖。
