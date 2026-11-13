# 其他事项（OTHER）

## 许可与合规

* 本项目以 **GPLv2** 发布（与 fheroes2 一致）。任何衍生作品都需保持 GPLv2。
* 仓库**不包含**《英雄无敌 II》的任何原始资源（`.agg`、`.h2d`、`.smk`、音乐、位图字体、翻译 `.mo`）。
  `Heroes2.agg` 等文件仅在运行时从玩家自己的安装目录读取。翻译**源文件** `translations/*.po`
  （我们自己的文案）入库，编译出的 `.mo` 安装到数据目录，同样不入库。
* `assets/fonts/` 只允许放置可再分发字体（SIL OFL / Apache-2.0 等），并在同目录注明来源与许可证。
  项目默认不打包字体，运行时回退到操作系统字体（Windows: `simhei.ttf` 等），这条路不产生再分发义务。
* 商标与版权归原权利人所有；本项目与 Ubisoft / New World Computing 无关联。

## 第三方依赖

| 依赖 | 许可 | 用途 |
| --- | --- | --- |
| MonoGame.Framework.DesktopGL | MS-PL | 窗口、图形、输入、音频 |
| FontStashSharp.MonoGame | MIT | TrueType 光栅化与动态图集 |
| xunit / xunit.runner.visualstudio | Apache-2.0 | 测试 |

## 已知限制（M0）

* 只有诊断场景；没有主菜单、地图或战斗。
* 没有音频、视频；本地化已有 `.mo` 读取与 `zh_CN` 语言包，但界面尚未接入（M2）。
* 无编辑器、无存档。
* 未提供 NativeAOT / 裁剪发布配置（ADR-011）：代码已是反射自由且分析器全开，但 MonoGame DesktopGL
  在 AOT 下的完整验证留到 M9。
* 仅 Windows 桌面验证过；MonoGame DesktopGL 理论上可跨平台，但数据目录解析与字体回退目前针对 Windows 调优。

## 待讨论 / 想法池

1. **着色器调色板查找**：把 8bpp 上传为 `R8` 纹理 + 256×1 调色板纹理，可在保留索引语义的同时省掉 CPU 查表。
2. **黄金图像测试**：借助 `--screenshot` + `AssetProbe` 生成参考图，对 ICN/TIL 解码做逐像素回归。
3. **`.mo` 复用**：已落地（ADR-016）：`MoCatalog` 读取 fheroes2 的目录格式，`translations/zh_CN.po`
   经 `BuildTranslations.ps1` 生成 `zh_CN.mo`；后续需要与 `ITextRenderer` 的布局配合（CJK 断行）。
4. **存档兼容性**：目前决定不兼容 C++ 存档；若未来需要，可先做只读导入。
5. **性能**：`Upload` 在整屏脏时每帧转换 30 万像素，M1 引入「场景级脏矩形 + 图集缓存」后再评估。
6. **输入抽象**：键盘/鼠标事件目前直接读 `Keyboard.GetState()`，M2 需要接入 `UI` 层的输入路由（含长按、双击）。
7. **NativeAOT**：M9 试用 `PublishAot=true` + `InvariantGlobalization=true`，重点验证 MonoGame DesktopGL、
   FontStashSharp 图集与 SDL 音频；同时评估 `dotnet publish -p:PublishSingleFile=true` 的裁剪体积收益。

## 参考链接

* fheroes2 上游：<https://github.com/ihhub/fheroes2>
* 本项目参考版本：`version.txt` = `1.1.18`，commit `6c51acfdc`
* MonoGame：<https://monogame.net/>
* FontStashSharp：<https://github.com/FontStashSharp/FontStashSharp>
