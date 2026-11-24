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
| `adventure-map.png` | 冒险地图（M4，外壳 + 地形 + 对象层 + 怪物层 + 英雄层 + 小地图）：`Alteris.mp2` 以焦点英雄居中（`AdventureSession.Start` 之后 `SetFocus` 总是居中于英雄，见 ADR-207），含城镇（门口的紫色亡灵英雄）、成片的游荡怪物（`MINIMON`：骷髅、僵尸、骨龙等）、树林、山脊、海岸与沙滩；`ADVBORD` 框架外壳（16px 边框、`16,16` 处 448×448 视口）、右上角 144×144 雷达面板带视口虚线框、雷达下方的砖纹面板与图标条（左列英雄槽：焦点英雄头像；右列城堡槽：本王国唯一的一座城，其余是空槽；`LOCATORS`，ADR-055、ADR-207）与八个命令按钮、按钮下方的 `STONBACK` 石头状态面板（`STATUS_ARMY`：焦点英雄的部队，精灵 19 + 另一个 4，`(480,392,144,72)`；日期面板是旧的场景自绘，ADR-207）、底边框带上的状态栏，以及战争迷雾（`CLOF32` 整瓦 + `CLOP32` 边界件；初始侦察 = 蓝方英雄半径 4 + 聚焦城堡半径 5，ADR-061） | 640×480 | `pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update` |
| `adventure-map-boats.png` | 冒险地图的第二个画面（M4，船）：`Undeada.mp2` 以焦点英雄居中（ADR-207），城堡西南角的海面上停着一条空船 —— 船体取自 `BOAT32` 第 18 帧（`RIGHT`，62×56，比瓦片高 33 像素、向上伸两格），影子取自 `BOATSHAD` 第 18 帧（74×28），瓦片原来的 `OBJNWAT2` 帧被丢掉。`Alteris.mp2` 的视野里一条船都没有，所以船的像素只能靠这张图守住 | 640×480 | `pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -MapFile Undeada.mp2 -Baseline tests/baselines/adventure-map-boats.png -Update` |
| `adventure-options.png` | 冒险选项对话框（M4，`--adventure-options`）：同一张 `Alteris.mp2` 上盖一层 `StandardWindow` 石头窗口，窗口由按钮组反推尺寸（`APANEL.ICN` 四个 59×27 按钮排成两列，active area `(221,167,198,146)`），四按钮是 `APANEL.ICN` 的 8 帧（世界/谜题/信息/挖掘，各一对 released/pressed），底部居中是 `SYSTEM.ICN` 第 3/4 帧的 CANCEL（`{0,11}` 上抬 11 像素）。对话框不透明，把地图中间整块盖住，四周的边框、雷达、按钮面板与状态面板都不受影响 | 640×480 | `pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -AdventureOptions -Update` |
| `quick-info.png` | 冒险地图的右键提示框（M4，`--quick-info --right-button --cursor 250,190`）：光标停在一座矿上，`QWIKINFO.ICN` 的 158×94 木框挂在该 tile 的右下角，框内是 `smallWhite` 居中的对象名「Ore Mine」（`MineText.GetMineName`，ADR-207）。框只在地形上出现，`-QuickInfo` 把默认光标从 `620,100`（图标条上）挪到 `250,190`；框只活在右键按住期间，所以脚本同时带 `--right-button`，否则场景下一帧就把它收掉（ADR-208）。文本来自地图数据，框位置来自光标，所以帧是稳定的 | 640×480 | `pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -QuickInfo -Update` |
| `editor.png` | 地图编辑器（E1/E2）：`Alteris.mp2` 居中到地图中心（编辑器没有焦点英雄，`EditorScene` 用 `CenterOn(width/2, height/2)`），`ADVBORD` 框架外壳（448×448 视口、右上角雷达、雷达下方的 `EditorPanel`）、光标双线高亮、tile 网格，以及一次脚本化笔刷（`46,52,6,4,Snow`）刷出的雪地——雪块四周是真正的雪/草/水过渡，不是硬边。面板按参考的 `EditorPanel::setPos` 排布：三行九宫格工具按钮（`EDITBTNS` 前 35 帧 + 运行时拼装的冒险/王国/怪物三个图标）、140px 高的拾取区（`EDITPANL` 的九个 27×27 地面格子，草压在 `TERRAINS` 第 9 帧的黄框里）、四个 24×18 笔刷尺寸按钮（1×1 那个是按下的）与底部两行系统按钮（放大镜/撤销/重做、`SPECS`/存盘/系统），拾取区底纹是 `STONEBAK` 加六层内凹斜面 | 640×480 | `pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update` |

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
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -AdventureOptions          # 冒险选项对话框
pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -AdventureOptions -Update  # 重建对话框基线
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
`diff_settings.png`、`diff_scenario.png`、`diff_adventure-map.png`、`diff_adventure-map-boats.png`、
`diff_adventure-options.png`、`diff_quick-info.png`、`diff_editor.png`，退出码 `1` 表示超阈值）。找不到 `Heroes2.agg` 时
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
* 视口居中在焦点英雄：开局 `ResetFocus(FIRSTHERO)` → `SetFocus` 总是 `SetCenter(hero)`（参考
  `interface_base.cpp:76-79`，ADR-207），所以滚动偏移由地图文件唯一决定，不带时钟。
* `--seed 1`：地图载入时英雄的初始部队数量是从线程随机源抽的（`Rand.Get`，OS 播种），不固定的话
  状态栏部队格每次差 27～40 个像素。脚本把它钉死，见 ADR-207。
* 地形图来自 `GROUND32.TIL`，四个 shape 变体（原图 / 上下 / 左右 / 两者）在启动时预生成再拷贝，
  不经过任何时钟，所以同一台机器上逐帧相同。
* 对象层不带时钟：`--freeze-animation` 下帧号直接用 MP2 文件里存的静态帧（参考的动画表还没移植，
  ADR-036），所以城镇、树林、山脊的位置与帧号也逐字节稳定。
* 英雄同样不带时钟：身体、旗帜与阴影一律取第 18 帧（朝右站立，ADR-037），颜色与种族从 tile 里的
  `MINIHERO` 帧号反解，所以地图上的英雄位置与外观由地图文件唯一决定。
* 小地图（`480,16`，144×144）只由 tile 字段与英雄列表算出（ADR-038）：地面色来自 terrain image
  index，山树提亮三档，道路单色，英雄取王国色，视口方框由滚动偏移决定——没有时钟。
* 外壳（ADR-054）：`ADVBORD.ICN` 第 0 帧整块 blit 到 `0,0`，地图先画进 448×448 的独立 `PixelBuffer`
  再贴到视口 `16,16`；雷达下方的砖纹面板按 `(480,176,72,32)` 平铺同一帧的像素。三者都是静态美术，
  只有位置来自 `MapFrame` 的常量，所以逐帧相同。
* 状态面板（`480,392,144,72`，`StatusPanel.AreaFor`）：按钮底到框底正好 72 像素，即一帧
  `STONBACK.ICN`；`SetFocus` 同时 `SetState(STATUS_ARMY)`（参考同一处，ADR-207），所以面板里是焦点
  英雄的部队（本例精灵 19 + 另一个 4），部队是地图载入抽的、靠 `--seed` 钉住。日期态
  （`Month: 1 Week: 1` / `Day: 1` + `SUNMOON` 月相）要左键点面板才切过去
  （`StatusPanel::QueueEventProcessing`），基线里不会出现，所以面板也不带时钟。
* 图标条（ADR-055、ADR-207）：`LOCATORS.ICN` 的英雄帧与城堡帧按 `(480,176)`、`(552,176)` 两列各四格
  摆好，图标内缩 `5,5`，空槽用帧 1…8 循环。左列是本王国的英雄（焦点英雄），右列是本王国拥有的城
  （`Alteris` 开局只有一座，其余空槽）；没有悬停、没有高亮标记，所以也不带时钟。
* 状态栏（`16,466,608,11`）搬到了底边框带上，里面印着视口偏移与版本号，后者每次发版都会变，所以
  排除掉。地图内容本身是数据文件的字节，不受语言影响；`-Culture` 只决定文件名，为与其它脚本对称而保留。
* 战争迷雾（ADR-061）也不带时钟：初始侦察只有「人类颜色的英雄半径 4」与「聚焦城堡半径 5」两个圆盘，
  方向掩码由 tile 的探索位一次算出，整瓦取 `CLOF32` 的 `(column+row)%4`、边界件取 `CLOP32`，都是静态美术。
  所以两张冒险地图基线的可见区随数据固定；`Undeada.mp2` 的船在雾下只有当它与上一行都被雾遮住时才消失
  （`AdventureMapView.IsUnderFogWithUpperTile`）。
* 这张基线钉住的是「MP2 tile/addon 字段 → ICN/TIL 索引 → 像素」这条链：`Mp2MapTests` 钉住结构解析，
  `AdventureMapViewTests` 钉住滚动数学，`ObjectLayerTests` 钉住部件顺序与落位，`HeroLayerTests`
  钉住英雄的三带切分与阴影次序，`MinimapTests` 钉住地面分段与方框几何，`IconBarTests` 钉住图标条的
  几何与 `LOCATORS` 帧表，一起才能把这一屏钉死。

## 冒险选项对话框基线也是逐字节稳定的

`adventure-options.png` 是同一张 `Alteris.mp2` 上盖一层对话框，所以上面那些「不带时钟」的理由全部沿用；
对话框自己也不带时钟：

* 窗口尺寸由按钮组反推（`StandardWindow.ActiveAreaForSymmetricButtons`，复刻 `getSymmetricDialogActiveArea`），
  没有随屏幕或文字变化的部分；四个按钮取自 `APANEL.ICN` 的 8 帧，CANCEL 取自 `SYSTEM.ICN` 的 3/4 帧，
  都是静态美术。
* 参考用 `ICN::SYSTEM` 的 0/1 帧，那是它运行时用文字合成的表；归档里的 `SYSTEM.ICN` 前两帧是另一组按钮，
  3/4 才是 CANCEL（与设置对话框同一对），所以端口取 3/4。这是**帧号**上的差异，布局仍按参考。
* 没有悬停、没有按下态、没有光标：`--cursor 620,100` 落在右侧图标条上，`--freeze-animation` 关掉了地图动画，
  所以按钮始终是 released 帧。

## 编辑器基线也是逐字节稳定的

* 与冒险地图同一张图（`Alteris`，编辑器居中到地图中心，不像冒险地图那样跟英雄走），多出来的只有编辑器
  自己画的东西：tile 网格、光标
  双线高亮、`ADVBORD` 外壳与其中的雷达 / `EditorPanel`。它们都不带时钟。
* `--editor-paint 46,52,6,4,Snow`：脚本先刷一块雪地再截帧。没有这一笔，基线分不出「编辑器能改 tile」
  与「只是把地图画出来了」。
* 状态栏（`0,469,640,11`）被忽略：那里印着视口偏移、撤销步数与版本号。**面板不在忽略之列**——面板
  全是 `EDITBTNS` / `EDITPANL` / `STONEBAK` 的美术与运行时拼装的图标（ADR-056），改按钮美术或改排布
  就要重建这张基线；面板按钮的交互另有 `--editor-panel-click` 的日志验证（ADR-049）。
* 对象工具（`O` / `X` / 面板上的对象按钮）不在这张基线里：它需要按键或点击。对象的验证走
  `--editor-object` + `--editor-place`、`--editor-panel-click` 的日志与 `MapEditSessionTests`，
  见 `PROGRESS.md` 的 E2 记录。

## CI 怎么接

`.github/workflows/ci.yml` 的 `menu-baseline` job 依次调用十条命令（job 名是历史原因，现在跑的是
十份基线：菜单、设置对话框 `en` 与 `zh_CN`、场景选择窗口 `en` 与 `zh_CN`、冒险地图、船、
冒险选项对话框、右键提示框、地图编辑器各一次）：

* 托管 runner（`windows-latest`）没有游戏数据，脚本会停在自检之后：**基线文件本身**被校验
  （存在、是 PNG、640×480），像素比对退化为 `skip`。这一步拦的是「基线被删/被换错尺寸」。
* 要真正跑像素比对，需要一台装了数据的自建 runner：把仓库变量 `FHEROES2_RUNNER` 设成它的标签
  （例如 `fh2-data`）、`FHEROES2_DATA` 设成数据目录（例如 `D:\game\fh2`）。两个变量都在时，
  job 才会传 `-DataDirectory` 与 `-RequireData`（缺数据即失败，而不是静默 skip）。
* 失败时 job 会把 `tools-output/baseline/*.png`（刚渲染的图，文件名带语言后缀）与
  `diff/` 目录（`diff_menu.png` / `diff_settings.png` / `diff_settings.zh_CN.png` /
  `diff_scenario.png` / `diff_scenario.zh_CN.png` / `diff_adventure-map.png` /
  `diff_adventure-map-boats.png` / `diff_adventure-options.png` / `diff_quick-info.png` /
  `diff_editor.png`）作为
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

冒险地图的状态栏在 `ADVBORD` 的底边框带上（M4 外壳，ADR-054），不是窗口最底部，所以它用
`--ignore 16,466,608,11`，参与比对的是 **300,512** 个像素（307200 − 6688）。

## 2026-11-24 的重建

`menu.png`、`settings.png`、`settings.zh_CN.png`、`scenario.png`、`scenario.zh_CN.png` 五张重建了一次：主菜单
右下角的 **Editor 招牌**（E1/E2 之后接进 `MainMenuScene`）一直没进基线，五张图的右下角都少一块，差异恒为
9 867 像素——不是改坏了像素，是基线从 2026-11-14 起就落后于代码。

同一天 `adventure-map.png`、`adventure-map-boats.png`、`adventure-options.png`、`quick-info.png` 四张也重建了：
它们来自自建渲染时代（视口居中在「最近的城堡」、雷达没有雾、状态栏是场景自绘的日期），逐项核对后确认
现在这三处都是参考行为，所以按参考更新基线，并给脚本加 `--seed 1`（ADR-207）。`quick-info.png` 另外还暴露
一个真问题：提示框只活在右键按住期间，而脚本并没有按住，于是框在第一帧就被场景收掉、基线退化成「没有框的
地图」——`--right-button` 现在会为冒险地图按住右键，脚本也带上了它（ADR-208）。`editor.png` 没有动。

## 基线的数据版本

当前十张基线都用**资料片（PoL）数据**渲染：`F:\game\fh2\Data` 里同时存在 `heroes2.agg` 与
`heroes2x.agg`，`AssetBundle` 优先取后者，因此 `HEROES.ICN` 是 *The Price of Loyalty* 横幅版本。
只有基础版数据（原版街景背景）时基线必然对不上——那种情况应当另存一份（例如
`menu-classic.png`）而不是覆盖它。`settings.zh_CN.png` 还额外依赖 `files/lang/zh_CN.mo`
（`pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN` 生成并安装），译文改动会让它对不上：
这是有意的，改译文就该重建这张基线。

| 文件 | 字节数 | SHA-256 |
| --- | --- | --- |
| `menu.png` | 239,894 | `8A4723F802129D7CDC4605E5DF2820A3E06898CBF329208ACDBF573F03F09ACC` |
| `settings.png` | 231,544 | `076F3FFFCC3587AEBAD7A121A0E010C3FB395ECF461A5482BCBCDFD55F0A0185` |
| `settings.zh_CN.png` | 231,331 | `3B04E01CB3DA0D86BF781C338555B5041F8CAACD4D40B4A207627877A465071E` |
| `scenario.png` | 212,241 | `03EAB07B6AC553C95470678BE0EF9B5A764EEB54F2BA4EAB7BA4476C4178947F` |
| `scenario.zh_CN.png` | 212,244 | `567D2DC0CEBFF60702D678AA6066F6ACF114930198C4583954050B058DCC1E63` |
| `adventure-map.png` | 122,397 | `849702BFAFAC122952FD633AC32DD9283718A542DC6A2FA21FA88E749CDD4C1F` |
| `adventure-map-boats.png` | 132,362 | `63A9722237F906BACFD7DD8317B952DC2B870ABD9BE4577FDEC3B6D1DE2D8136` |
| `adventure-options.png` | 130,860 | `961E657BA1B4C2CFE5A5FB42A3810C39AE7D5A34D35296A198DFD2894A48507D` |
| `quick-info.png` | 121,856 | `012B298143C38E786ED8834E85ABAA163BFA16BDED372BB35485C199A59F672F` |
| `editor.png` | 132,506 | `27484497727C145CF3BEFE82CE341B4ACBDB2B6439524752B6E0C881C04AC523` |

## 更新基线的规矩

1. 只有在**有意**改变视觉时才 `-Update`（美术位置、绘制顺序、调色板、字体……）。
2. 同一个提交里说明变了什么、为什么变；顺便更新本文的字节数 / SHA-256 与数据版本。
3. 更新后立刻跑一次不带 `-Update` 的校验，确认可复现。
4. 纯重构（像素不该变）**不要**更新基线——它存在的意义就是拦下「不小心改了像素」。
5. 设置基线的文字随语言变化：换语言就等于换基线，不要在同一份文件上来回覆盖。
