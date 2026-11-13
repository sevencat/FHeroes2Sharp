# 参考对照（REFERENCE）

C++ 参考实现校验值：`version.txt` = `1.1.18`，commit `6c51acfdc`。

## 已移植

| C++ | C# | 备注 |
| --- | --- | --- |
| `src/engine/agg_file.h/.cpp`（`AGGFile`） | `FHeroes2.Assets.Agg.AssetArchive` | 打开即逐条校验名称哈希；含越界/重复检测 |
| `calculateAggFilenameHash()` | `FHeroes2.Assets.Agg.AggFileNameHash` | 反向遍历、`hash<<5 + hash>>25`、大小写不敏感 |
| `src/fheroes2/agg/agg.cpp`（`AGG::AGG`、`getDataFromAggFile`） | `FHeroes2.Assets.AssetBundle` | 资料片优先、基础归档兜底 |
| `src/engine/rand.h/.cpp`（`Rand::PCG32`） | `FHeroes2.Core.Random.Pcg32` | 同 multiplier / seed=42 / stream=54；`|1` 强制奇数 |
| `Rand::uniformIntDistribution` | `Pcg32.NextInclusive` | Lemire 拒绝采样，`[from, to]` 闭区间 |
| `src/engine/math_base.h`（`Point`/`Size`/`Rect`） | `FHeroes2.Core.Geometry.Point/Size/Rect` | 右/下边界半开 |
| `src/engine/image_palette.h/.cpp` | `FHeroes2.Core.Imaging.Palette` | 6 bit → `<<2`，循环色复制见下 |
| `src/engine/image.h`（`Image`、`Blit`） | `PixelBuffer` + `ScreenBuffer` + `Sprite` + `ImageOps` | 变换层感知的 `BlitSprite` |
| `src/engine/image.cpp`（`ApplyTransform`、`addGradientShadow`、`addGradientShadowForArea`、`CreateDitheringTransition`） | `ImageOps.ApplyTransform` / `AddGradientShadow` / `AddGradientShadowForArea` / `CreateDitheringTransition` | 目标一律是单层表面；阴影线的斜率取整改用 `MidpointRounding.AwayFromZero`（`std::round` 的语义）；8 个用例与 C++ 逐字节相同（`tools/scripts/CompareEffectsWithReference.ps1`） |
| `src/engine/image.cpp`（`transformTable`、`ApplyPalette`） | `FHeroes2.Core.Imaging.PaletteTransformTable` | 16×256 表，逐字节移植（SHA-256 固定） |
| `src/engine/pal.cpp`（`PAL::GetPalette`、`CombinePalettes`） | `FHeroes2.Core.Imaging.PaletteTable` + `PaletteId` | 13×256 重着色表 + identity；脚本生成 + SHA-256 固定（ADR-020） |
| `src/engine/image_tool.cpp`（`decodeICNSprite`） | `FHeroes2.Assets.Icn.IcnSpriteDecoder` | 单色流 + 彩色流、`0xC0` 变换段 |
| `readIcnFromAgg()` / `ICNHeader` | `FHeroes2.Assets.Icn.IcnSpriteFile` + `IcnHeader` | 6 字节文件头 + 13 字节精灵头 |
| `src/engine/til.cpp`（TIL 读取） | `FHeroes2.Assets.Til.TilFile` | 未压缩图块网格 |
| PNG 导出 / 读回（用于回归比对） | `FHeroes2.Core.Imaging.PngWriter` + `PngReader` + `PngImage` | 导出 8 bit RGBA（`transform==1` → alpha 0）；读回支持 0/2/3/4/6 色彩类型、1/2/4/8/16 bit、五种行滤波、`tRNS` |
| `src/engine/image_tool.cpp`（`Save`） | `FHeroes2.Core.Imaging.BmpWriter` | 8 bit 索引 BMP，布局同 `SDL_SaveBMP`；`EncodeSprite` 对应 `Save(image, path, background)` |
| `src/engine/image_tool.cpp`（`decodeBMPFile`） | `FHeroes2.Assets.Cursor.MonoCursorFile` | 单色光标 `*.BMP` 条目（非 Windows 位图），1 = 白、2 = 黑、其他 = 透明 |
| `src/fheroes2/gui/cursor.*` | `FHeroes2.Core.Imaging.CursorTheme` + `CursorLayout` | 主题枚举、图集选择（`SetThemes`）、热点偏移（`SetOffset`）、距离主题换算 |
| `src/engine/screen.h` | `PresentationViewport` + `SdlPresenter` | 整数倍 + 黑边 |
| `DEBUG_LOG` / `ERROR_LOG` | `FHeroes2.Core.Diagnostics.GameLog` | 级别 + 环境变量 |
| `src/fheroes2/gui/ui_font.h` | `FHeroes2.Rendering.Text.ITextRenderer` | TrueType 后端先行；界面文本已改走 `Assets.Fonts.IGlyphSource` |
| `src/fheroes2/gui/interface_font.cpp`（`getTextWidth` / `renderText` 的「位图为主、TrueType 补位」） | `FHeroes2.Assets.Fonts.CompositeGlyphSource` | 度量只取位图字体，TrueType 只提供字形（ADR-021）；客户端装配在 `GameFonts` |
| `src/fheroes2/agg/game_assets.cpp`（`getFont()` / `getIcon()` 的字体缓存） | `FHeroes2.App.Fonts.GameFonts` | 启动时装载 `FONT.ICN` / `SMALFONT.ICN` 与 `YELLOW_FONT` 副本 |
| `src/engine/translations.h/.cpp`（`Translation::MOFile`、`stripContext`、`getCharsetFromHeader`） | `FHeroes2.Assets.Localization.MoCatalog` | GNU MO 读取；按 `msgid` 查表，未命中回退英文；仅 UTF-8 目录（ADR-016） |
| `files/lang/*.po` → `<data>/files/lang/*.mo` | `translations/*.po` + `tools/scripts/BuildTranslations.ps1` | `zh_CN.po` 当前 95 条（主菜单 / 设置 / 语言 / 快捷键 / 分辨率） |
| `src/fheroes2/game/game_mainmenu.cpp`（`Game::MainMenu`） | `FHeroes2.App.Scenes.MainMenuScene` | 背景 `HEROES.ICN`（资料片优先）、招牌 `BTNSHNGL.ICN` 1/5/9/13/17（悬停 `+1`、按下 `+2`）、热键 N/L/H/C/Q、灯笼光晕 `SHNGANIM.ICN`（第 0 帧灯体 + 第 1…39 帧闪烁） |
| `Game::MainMenu()` 的 `highlightDoor` / `settingsArea`（同上文件） | `FHeroes2.UI.Widgets.DoorHighlight` | `SHNGANIM` 第 18 帧套 `PAL::NO_CYCLE`（枚举值 8）后只取 `y ≥ 55`（`doorOffsetY`）的下半部分；命中区 `63,202 108×160`；每帧重画（参考实现挂在 250 ms 的动画节拍上），右键门的提示框、点门后的设置对话框都已移植（见下） |
| `Dialog::ResizableFrameBox` / `Dialog::FrameBox`（`src/fheroes2/dialog/dialog_frameborder.cpp`、`dialog_box.cpp`） | `FHeroes2.UI.Dialogs.FrameBox` + `Dialogs.FrameBoxChrome` | `BUYBUILD.ICN` 右列 0/1/2 + 左列 4/5/6；顶 99 px、中段从第 10 行切 35 px 重复、底 81 px；文字区 244 宽、最小 70 高；`showButtons` 预留 40 px |
| `getDialogHeight()` / `showMessage( header, body, Dialog::ZERO )`（`src/fheroes2/gui/ui_dialog.cpp`） | `FHeroes2.UI.Dialogs.MessageBox` | 标题 `normalYellow` + 正文 `normalWhite`，都按 244 px 折行居中；标题上方与正文上方各 10 px（`textOffsetY`）；无按钮、无 UI 元素（ADR-024） |
| `Game::MainMenu()` 的右键提示（`showStandardTextMessage( ..., Dialog::ZERO )`） | `MainMenuScene.SetRightButton` + `MessageBox` | 按住右键创建、松开丢弃；招牌标题/说明与门的「Game Settings」文案取自同一份 `.mo` |
| `src/fheroes2/gui/ui_window.cpp`（`fheroes2::StandardWindow`） | `FHeroes2.UI.Dialogs.StandardWindow` + `StandardWindowChrome` | 边框来自 `SURDRBKG.ICN`（横）与 `WINLOSE.ICN`（竖 + 四角），内部铺 `STONEBAK.ICN`；角块 22、可重复边框起点 43、接缝 10、底纹让位 22；阴影只挂在左/下 16 px（`TotalArea` 比窗口宽 16）；4 个用例与 C++ 逐字节相同（`tools/scripts/CompareWindowWithReference.ps1`） |
| `DrawBorder()` / `ADVBORD.ICN` 对话框边框 | 未移植（M3 起的对话框用） | `ADVBORD.ICN` 已能解码（单张 640×480），尚无九宫格拼装；消息框走的是 `BUYBUILD.ICN`（见上两行） |
| `ICN::getAnimatedIcnIndex()`（`src/fheroes2/agg/icn.cpp`）+ `Game::DelayType::MAIN_MENU_DELAY`（`game_delays.cpp`） | `FHeroes2.Assets.Icn.IcnAnimation` | 帧表按场景声明（起始帧 + 帧数 + 每帧时长），计时同 `fheroes2::TimeDelay`：整帧才前进、长停顿跳帧不补播 |
| `src/fheroes2/gui/ui_button.cpp`（`Button` / `ButtonSprite` / `ButtonBase` / `ButtonGroup`） | `FHeroes2.UI.Widgets.Button` + `ButtonState` / `ButtonBase` + `ButtonSprite` + `ButtonGroup` + `Dialogs.DialogButton` | 主菜单招牌 = `Button`（三帧精灵 + hover 状态机，`Area` 取 released 的包围盒 = ICN 的 `OffsetX/OffsetY`，缺帧回退）；对话框按钮 = `ButtonSprite`（released/pressed/可选 disabled，禁用时过 `PAL::DARKENING`）+ `ButtonGroup`（按下与松开都在区内才算点击、按住只点亮按下所在的按钮、OK/Enter 与 CANCEL/Escape 热键、单个 OK/CANCEL 两个热键都能关）；自动摆位 = `ButtonGroup.CreateStandard`（`ButtonGroup( area, types )`），文字按钮只移植了精灵侧（`ButtonArtwork`），标签仍待 `BUTTON_*_FONT_*` 字体表 |
| `DrawBorder()` / `ADVBORD.ICN` 对话框边框 | 未移植（M3 起的对话框用） | `ADVBORD.ICN` 已能解码（单张 640×480），尚无九宫格拼装 |
| `Translation::gettext()` + `Settings::getGameLanguage()` | `FHeroes2.App.Localization.GameText` | 一个语言一个实例；英文 = msgid，`zh_CN` = `.mo`；`--lang` / `F2` 切换 |
| `LocalEvent` 的鼠标坐标换算（窗口 → 逻辑像素） | `PresentationViewport.TryToLogical` | 整数倍 + 黑边；黑边上的点返回 `false` |
| `Display::render()` 的文字叠加 | `Scenes.IScene.DrawOverlay` | 只有诊断屏用（SDL3_ttf）；真实画面全部走索引字形 |

| `src/fheroes2/gui/ui_button.cpp` 的 `resizeButton()` / `addButtonShine()` / `makeTransparentBackground()` | `FHeroes2.UI.Widgets.ButtonArtwork`（`Resize` / `AddShine` / `MakeTransparentBackground`） | 空按钮按「宽三等分、高五等分」重复拉伸，四角原样保留；只有「两个方向都变大」那一支有中空区，用原图中心像素填充（`centralPosition = w*h/2 - 1 - min(w,h)/2`）；闪光像素的绝对坐标表、`width > 50` / `> 96` / `height > 25` 三层条件、Good/Evil 两套配色（10 / 37,15 / 39,16）逐像素移植 | 
| `getButtonSpecificValues()` + `getTextAdaptedSprite()`（同上文件） | `FHeroes2.UI.Widgets.DialogButtonTemplate` + `ButtonTemplateMetrics` | 6 个模板的 `textAreaBorders` / `minimumTextArea` / `maximumTextArea` / `backgroundBorders` / released、pressed 文字偏移全部照抄（Good/Evil 最小文字区 86×15、边框 10×8、偏移 6,4 / 5,5）；文字色只保留调色板 id（`WhiteFont` / `GrayFont`） |
| `ButtonGroup( const Rect & area, const int buttonTypes )` | `FHeroes2.UI.Widgets.ButtonGroup.CreateStandard` + `IDialogButtonArtworkSource` / `DialogButtonArtwork` | YES\|NO、OK\|CANCEL、单个 OK、单个 CANCEL 四支；一对按钮的左右各留 `(area.width - w1 - w2)/4`，单个按钮居中并贴底；精灵自带的 `OffsetX/OffsetY` 由 `ButtonSprite` 叠在摆位坐标上（与参考实现相同） |
| `src/engine/image.cpp` 的 `Copy` / `Fill` / `SetPixel` / `DrawLine`（双层写） | `FHeroes2.Core.Imaging.ImageOps.Copy` / `FillSprite` / `SetPixel` / `DrawLine` | 写颜色时把 transform 字节清 0；`Copy` 的三条规则（目标单层只拷颜色、源单层写 0、双层互通）逐条移植；`DrawLine` 是同一个整数 Bresenham（`ns = major/2`，负了才进次轴） |
| `dialog::openGameSettings()`（`src/fheroes2/dialog/dialog_game_settings.cpp`，子集） | `FHeroes2.UI.Dialogs.SettingsDialog` + `Dialogs.SettingsOption` | 一个 `StandardWindow` + 标题 + 每行一个选项 + 底部标准按钮；行内 `< 值 >`，点左半格退一格、右半格进一格（循环）；OK / Enter 应用、Escape 取消；当前只有「Language」一行（ADR-027） |
| `ICN::UNIFORM_GOOD_OKAY_BUTTON`（`game_assets.cpp` 运行时生成） | `FHeroes2.App.Scenes.SettingsArtwork` | 参考实现要么用 `SYSTEM.ICN` 第 1/2 帧，要么用 `EMPTY_GOOD_BUTTON` + 文字现做；端口取前者（`SKIP` 分支 `useOriginalResources()` 的做法），字按钮生成器只留在 `ButtonArtwork` 里备用 |

## 计划中

| C++ | 计划 C# | 里程碑 |
| --- | --- | --- |
| `src/engine/image_tool.cpp`（图像缩放） | `ImageOps.Resize` | 未排期 |
| `src/engine/h2d_file.h` | `Assets/H2D/H2DArchive` | M3 |
| `src/engine/audio/*`、`82M` 解码 | `Assets/Audio/*` | M2/M7 |
| `src/fheroes2/gui/ui_text.*`（排版、CJK 断行） | `Rendering.Text` 布局层 | M2 |
| `src/fheroes2/world/*`、`army`、`battle` | `FHeroes2.Game.*` | M3–M7 |
| `src/engine/serialize.h` | `Game/Persistence/*`（自研 schema） | M8 |

## 文件格式速查

### AGG（`heroes2.agg` / `heroes2x.agg`）

```
uint16                         条目数 count
count × { uint32 hash, uint32 offset, uint32 size }     ← 从文件偏移 2 开始
...                                            payload
EOF - count*15                  count × 15 字节名称表（NUL 填充，大写，顺序与条目一致）
```

* 名称哈希（`src/engine/agg_file.cpp`）：

```c
hash = 0; sum = 0;
for (char c : reverse(name)) {          // 反向遍历
    c = toupper(c);
    hash = (hash << 5) + (hash >> 25);
    sum += c;
    hash += sum + c;
}
```

* 实测：`heroes2.agg` = 1434 条 / 43,324,306 字节负载；`heroes2x.agg` = 52 条。
* 扩展名分布（基础归档）：`.ICN 846`、`.82M 309`、`.BIN 147`、`.BMP 109`、`.XMI 17`、`.TIL 3`、`.FNT 2`、`.PAL 1`。

### KB.PAL

* 768 字节，**6 bit VGA**（每个分量 0..63）。
* 归一化：`component << 2`。
* 循环色复制（`fheroes2::setGamePalette`）：
  * 水：231→246，232→247，233→248，235→249
  * 岩浆：214→250，215→251，216→252，217→253

### ICN（精灵图集，`decodeICNSprite`）

```
uint16                         精灵数 count
uint32                         数据块大小 dataSize（6 + dataSize == 条目长度）
count × 13 字节头 { int16 offsetX, int16 offsetY, uint16 width, uint16 height,
                    uint8 animationFrames, uint32 offsetData }
...                            RLE 压缩负载（每条 offsetData 相对 6，非相对文件头）
```

* `animationFrames & 0x20` → 单色精灵：只编码变换层，图像层全 0。
* 彩色流的控制字节：

| 控制字节 | 含义 |
| --- | --- |
| `0x00` | 行结束（该行剩余像素保持透明） |
| `0x01`–`0x7F` | 后续字面像素个数 |
| `0x80` | 图像结束 |
| `0x81`–`0xBF` | 透明像素个数 + `0x80`（`0xC0` 起被变换段占用） |
| `0xC0` | 变换段：紧跟描述字节，低 2 位是像素数（0 表示再读一字节） |
| `0xC1` | 单色游程，下一字节给出像素数 |
| `0xC2`–`0xFF` | 单色游程，像素数 = 值 − `0xC0` |

* 变换段描述字节：bit `0x40` 置位时，`((value & 0x3C) >> 2) + 2` 为 `PaletteTransformTable` 索引；
  bit `0x80`（轮廓高亮）在 C++ 里是 TODO，本项目同。
* 变换层语义：`0` 复制图像层，`1` 跳过目标像素，其余值用表转换**目标**颜色（阴影/光照）。
  整幅精灵都不需要变换层时（`noTransformLayer`）丢弃该层，等价 `_disableTransformLayer()`。
* 实测：`HEROES.ICN` = 1 个 640×480 精灵，全不透明（无变换层）。

### TIL（地形图块）

```
uint16 count, uint16 width, uint16 height
count × width × height          原始 8bpp 索引，无压缩
```

* 实测：`GROUND32.TIL` = 432 个 32×32 图块（6 + 432×1024 = 442,374 字节）。

### 单色光标（`.BMP` 条目）

```
uint8                          "黑色"像素使用的调色板索引（原版数据为 33）
uint8                          未使用（恒为 0）
uint16 width, uint16 height
width × height × uint8         像素码：1 = 白（调色板索引 11）、2 = 黑（头字节索引）、其他 = 透明
```

* 这些条目**不是** Windows 位图（C++ 注释明确写了），由 `fheroes2::decodeBMPFile()` 专用解码，
  C# 侧为 `MonoCursorFile.Parse`。
* 透明像素的图像层保持 0、变换层写 1，因此解码结果**总是**带变换层；`HasTransformLayer` 为真。
* 实测：`ADVMBW01.BMP` / `SPELBW00.BMP` 均为 32×32 / 1030 字节；
  `ADVMBW01` = 37 白 + 53 黑 + 934 透明，`SPELBW00` = 108 白 + 124 黑 + 792 透明。
* 主题 → 图集（`CursorLayout.ImageSet`）：`0x1000` → `ADVMCO.ICN`（单色 `ADVMBWnn.BMP`）、
  `0x2000` → `CMSECO.ICN`（`CMSEBWnn.BMP`）、`0x3000` → `SPELCO.ICN`（`SPELBWnn.BMP`）、
  `0x4000` → 运行时合成的距离光标（`COLOR_CURSOR_ADVENTURE_MAP` / `MONO_CURSOR_ADVENTURE_MAP`）。
  低字节是图集内序号（`theme & 0xFF`）；`POINTER_VIDEO` 即使开了单色也走彩色图集。
* 热点：先算 `((width - offsetX) / 2, (height - offsetY) / 2)`，再按 `Cursor::SetOffset()` 修正——
  指针类为 `(0, 0)`，剑/滚动光标有硬编码偏移（例如 `SWORD_RIGHT` = `(-25, -7)`）。

### BMP 导出（`fheroes2::Save`）

```
14 字节 BITMAPFILEHEADER        'BM'、文件大小、像素数据偏移 1078
40 字节 BITMAPINFOHEADER        1 平面、8 bpp、BI_RGB、imageSize、clrUsed = 256
256 × { B, G, R, 255 }          调色板（BGRA 顺序；第 4 字节是 SDL 写入的 alpha，恒为 255）
height 行                       自下而上，每行按 4 字节对齐补零
```

* 与 `SDL_SaveBMP` 对 8 bpp surface 的输出一致：C++ 端就是把索引图交给 SDL 保存。
  注意 BMP 规范称调色板第 4 字节为「保留」，但 `SDL_SaveBMP` 写的是 `SDL_Color.a`，
  `fheroes2::SaveImage()` 把每个条目都设为 255——`BmpWriter` 照抄 255 才能与参考导出逐字节相同。
* 索引位图没有 alpha，所以导出精灵时先用 `--background`（默认 0，原版推荐 23）填充画布，
  再用 `BlitSprite` 按变换层合成，对应 `fheroes2::Save(image, path, background)`。
* 实测：`GROUND32.TIL` 的 432 个图块导出 BMP 后用 Pillow 读回，调色板索引与 PNG 导出逐像素一致。
* 参考对拍：`tools/reference/fh2_reference.cpp` 链接原版 `image_tool.cpp` / `agg_file.cpp` 导出，
  默认 14 个代表性条目的 1204 个 BMP 与本项目 `--format bmp` 的输出**逐字节相同**；`-All` 覆盖
  归档中 958 个 `.ICN` / `.TIL` / `.BMP` 条目，共 15,025 个 BMP 全部相同
  （文件头、调色板、行序、变换层合成全部命中）；流程见 `MISC.md`。

### 语言包（GNU MO，`Translation::MOFile`）

```
uint32   magic            0x950412de（小端；大端文件按小端读得 0xde120495）
uint16   major            必须为 0
uint16   minor
uint32   count            字符串对数量
uint32   origTable        原始字符串表偏移，count × { uint32 length, uint32 offset }
uint32   transTable       翻译字符串表偏移，布局同上
uint32   hashSize, hashOffset                 （本项目忽略）
...                       NUL 结尾的 msgid / msgstr 负载
```

* 头部条目（空 `msgid`）的 `msgstr` 存放元信息：`Language:`、`Content-Type: text/plain; charset=UTF-8`、
  `Plural-Forms:`。字符集由 `getCharsetFromHeader()` 解析。
* 复数形式写在同一个 `msgstr` 里，用 `\0` 分隔；引擎按语言硬编码的规则取第几个形式，本项目把序号交给
  调用方（`Translate(id, pluralIndex)`），简体中文只声明 1 个形式。
* 上下文：`msgid` 中的 `context|text` 前缀用于消歧，显示前由 `stripContext()` 去掉（未命中时也去掉，
  因此缺失翻译总是显示英文原文而不带前缀）。
* 实测：`translations/zh_CN.po` → `zh_CN.mo` 95 条 / 7,503 字节；`msgfmt --check` 通过，
  `msgunfmt` 反编译得到 96 个 `msgid` 块（95 条 + 头部），与本项目 `MoCatalog` 的读取结果一致。

### 调色板转换表（`transformTable`）

* `src/engine/image.cpp` 中的 `transformTable` 共 16×256 字节 `const uint8_t`：
  `0` / `1` 为占位（全 0），`2`–`5` 阴影（2 最强）、`6`–`9` 光照、`15` 把循环色映射为静态色。
* 由 `tools/scripts/GeneratePaletteTransformTable.ps1` 从参考源码提取并生成
  `PaletteTransformTable.cs`（Base64 存储）；测试用 SHA-256 固定这 4096 字节。

### PCG32（`src/engine/rand.h`）

```
multiplier = 6364136223846793005
defaultSeed = 42, defaultStream = 54
state = 0; increment = stream; advance(); state += seed; advance();
next():  prev = state; advance();
         xorshifted = ((prev >> 18) ^ prev) >> 27;
         rot = prev >> 59;
         return rotr32(xorshifted, rot);
advance(): state = state * multiplier + (increment | 1);
```
