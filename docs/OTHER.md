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
| SDL3-CS / SDL3-CS.Native | MIT（绑定）；SDL3 本体 Zlib | 窗口、图形、输入、音频 |
| SDL3-CS.Native.TTF | Zlib | TrueType 光栅化（SDL3_ttf / FreeType） |
| Sevencat.GlyphRaster | 仓库 GPL-3.0；内嵌字体 Apache-2.0 / GPL-3.0 双许可 | 纯托管 TrueType 光栅化 + 内嵌文泉驿微米黑（ADR-019）。**该包由本项目作者维护，许可可按需调整，不构成分发阻塞** |
| xunit / xunit.runner.visualstudio | Apache-2.0 | 测试 |

## 已知限制（M2）

* 主菜单已上屏（`HEROES.ICN` + `BTNSHNGL.ICN` 招牌、鼠标悬停、`F2` 中英切换）；新游戏/读档/高分/致谢
  四块招牌只更新状态栏提示，真正的画面从 M3 起实现；没有冒险地图与战斗。
* 没有音频、视频；`BGM` 播放、`SHNGANIM.ICN` 灯笼动画、fheroes2 式的语言选择对话框尚未接入。
* 无编辑器、无存档。
* 未提供 NativeAOT / 裁剪发布配置（ADR-011）：代码已是反射自由且分析器全开，但 SDL3 原生库
  在 AOT 下的完整验证留到 M9。
* 仅 Windows 桌面验证过；SDL3 绑定本身跨平台，但数据目录解析与字体回退目前针对 Windows 调优。

## 待讨论 / 想法池

1. **着色器调色板查找**：把 8bpp 上传为 `R8` 纹理 + 256×1 调色板纹理，可在保留索引语义的同时省掉 CPU 查表。
2. **黄金图像测试**：借助 `--screenshot` + `AssetProbe` 生成参考图，对 ICN/TIL 解码做逐像素回归。
3. **`.mo` 复用**：已落地（ADR-016）：`MoCatalog` 读取 fheroes2 的目录格式，`translations/zh_CN.po`
   经 `BuildTranslations.ps1` 生成 `zh_CN.mo`；后续需要与 `ITextRenderer` 的布局配合（CJK 断行）。
4. **存档兼容性**：目前决定不兼容 C++ 存档；若未来需要，可先做只读导入。
5. **性能**：`Upload` 在整屏脏时每帧转换 30 万像素，M1 引入「场景级脏矩形 + 图集缓存」后再评估。
6. **输入抽象**：键盘/鼠标事件在事件循环里翻译（`SDL_PollEvent` → `MainMenuScene.SetCursor/SetLeftButton`），
   主菜单已按需接好；真正需要「输入路由」时（多控件、对话框栈、长按/双击）再抽 `UI` 层路由。
7. **NativeAOT**：M9 试用 `PublishAot=true` + `InvariantGlobalization=true`，重点验证 SDL3 / SDL3_ttf
   原生库的加载与 SDL 音频；同时评估 `dotnet publish -p:PublishSingleFile=true` 的裁剪体积收益。

## 参考链接

* fheroes2 上游：<https://github.com/ihhub/fheroes2>
* 本项目参考版本：`version.txt` = `1.1.18`，commit `6c51acfdc`
* SDL3：<https://github.com/libsdl-org/SDL>
* SDL3-CS 绑定：<https://github.com/edwardgushchin/SDL3-CS>
