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
| `src/engine/image.cpp`（`transformTable`、`ApplyPalette`） | `FHeroes2.Core.Imaging.PaletteTransformTable` | 16×256 表，逐字节移植（SHA-256 固定） |
| `src/engine/image_tool.cpp`（`decodeICNSprite`） | `FHeroes2.Assets.Icn.IcnSpriteDecoder` | 单色流 + 彩色流、`0xC0` 变换段 |
| `readIcnFromAgg()` / `ICNHeader` | `FHeroes2.Assets.Icn.IcnSpriteFile` + `IcnHeader` | 6 字节文件头 + 13 字节精灵头 |
| `src/engine/til.cpp`（TIL 读取） | `FHeroes2.Assets.Til.TilFile` | 未压缩图块网格 |
| PNG 导出 / 读回（用于回归比对） | `FHeroes2.Core.Imaging.PngWriter` + `PngReader` + `PngImage` | 导出 8 bit RGBA（`transform==1` → alpha 0）；读回支持 0/2/3/4/6 色彩类型、1/2/4/8/16 bit、五种行滤波、`tRNS` |
| `src/engine/image_tool.cpp`（`Save`） | `FHeroes2.Core.Imaging.BmpWriter` | 8 bit 索引 BMP，布局同 `SDL_SaveBMP`；`EncodeSprite` 对应 `Save(image, path, background)` |
| `src/engine/image_tool.cpp`（`decodeBMPFile`） | `FHeroes2.Assets.Cursor.MonoCursorFile` | 单色光标 `*.BMP` 条目（非 Windows 位图），1 = 白、2 = 黑、其他 = 透明 |
| `src/fheroes2/gui/cursor.*` | `FHeroes2.Core.Imaging.CursorTheme` + `CursorLayout` | 主题枚举、图集选择（`SetThemes`）、热点偏移（`SetOffset`）、距离主题换算 |
| `src/engine/screen.h` | `PresentationViewport` + `MonoGamePresenter` | 整数倍 + 黑边 |
| `DEBUG_LOG` / `ERROR_LOG` | `FHeroes2.Core.Diagnostics.GameLog` | 级别 + 环境变量 |
| `src/fheroes2/gui/ui_font.h` | `FHeroes2.Rendering.Text.ITextRenderer` | TrueType 后端先行 |

## 计划中

| C++ | 计划 C# | 里程碑 |
| --- | --- | --- |
| `src/engine/image_tool.cpp`（图像缩放） | `ImageOps.Resize` | 未排期 |
| `src/engine/h2d_file.h` | `Assets/H2D/H2DArchive` | M3 |
| `src/engine/audio/*`、`82M` 解码 | `Assets/Audio/*` | M2/M7 |
| `src/fheroes2/gui/ui_text.*`、`translations/*.mo` | `Assets/Localization` + `Rendering.Text` | M2 |
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
256 × { B, G, R, 0 }            调色板（BGRA 顺序，A 恒为 0）
height 行                       自下而上，每行按 4 字节对齐补零
```

* 与 `SDL_SaveBMP` 对 8 bpp surface 的输出一致：C++ 端就是把索引图交给 SDL 保存。
* 索引位图没有 alpha，所以导出精灵时先用 `--background`（默认 0，原版推荐 23）填充画布，
  再用 `BlitSprite` 按变换层合成，对应 `fheroes2::Save(image, path, background)`。
* 实测：`GROUND32.TIL` 的 432 个图块导出 BMP 后用 Pillow 读回，调色板索引与 PNG 导出逐像素一致。

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
