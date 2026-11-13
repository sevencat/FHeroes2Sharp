# 参考对照（REFERENCE）

C++ 参考实现校验值：`version.txt` = `1.1.18`，commit `6c51acfdc`。

## 已移植（M0）

| C++ | C# | 备注 |
| --- | --- | --- |
| `src/engine/agg_file.h/.cpp`（`AGGFile`） | `FHeroes2.Assets.Agg.AssetArchive` | 打开即逐条校验名称哈希；含越界/重复检测 |
| `calculateAggFilenameHash()` | `FHeroes2.Assets.Agg.AggFileNameHash` | 反向遍历、`hash<<5 + hash>>25`、大小写不敏感 |
| `src/fheroes2/agg/agg.cpp`（`AGG::AGG`、`getDataFromAggFile`） | `FHeroes2.Assets.AssetBundle` | 资料片优先、基础归档兜底 |
| `src/engine/rand.h/.cpp`（`Rand::PCG32`） | `FHeroes2.Core.Random.Pcg32` | 同 multiplier / seed=42 / stream=54；`|1` 强制奇数 |
| `Rand::uniformIntDistribution` | `Pcg32.NextInclusive` | Lemire 拒绝采样，`[from, to]` 闭区间 |
| `src/engine/math_base.h`（`Point`/`Size`/`Rect`） | `FHeroes2.Core.Geometry.Point/Size/Rect` | 右/下边界半开 |
| `src/engine/image_palette.h/.cpp` | `FHeroes2.Core.Imaging.Palette` | 6 bit → `<<2`，循环色复制见下 |
| `src/engine/image.h`（`Image` 基础） | `PixelBuffer` + `ScreenBuffer` + `ImageOps` | 变换层待 M1 |
| `src/engine/screen.h` | `PresentationViewport` + `MonoGamePresenter` | 整数倍 + 黑边 |
| `DEBUG_LOG` / `ERROR_LOG` | `FHeroes2.Core.Diagnostics.GameLog` | 级别 + 环境变量 |
| `src/fheroes2/gui/ui_font.h` | `FHeroes2.Rendering.Text.ITextRenderer` | TrueType 后端先行 |

## 计划中

| C++ | 计划 C# | 里程碑 |
| --- | --- | --- |
| `src/engine/image_tool.cpp`（ICN 解码、变换层） | `Assets/Icn/*` | M1 |
| `src/engine/til.cpp` | `Assets/Terrain/TerrainSet` | M1 |
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
