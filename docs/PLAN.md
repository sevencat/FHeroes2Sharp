# 移植计划（PLAN）

> 参考实现：**fheroes2 1.1.18（commit `6c51acfdc`）**
> 目标：C# / .NET 10 / MonoGame / FontStashSharp，逻辑分辨率 640×480。

## 总体原则

1. **外部资源格式逐字节一致**：AGG / ICN / TIL / MP2 / H2D / SMK / `.mo` / `KB.PAL` 的解析必须与 C++ 完全一致，
   包括名称哈希、条目顺序、裁剪/透明规则、字节序。
2. **内部工程现代重构**：`readonly record struct` 几何类型、`sealed` 服务类、`Nullable=enable`、
   分层命名空间、显式依赖注入（无 DI 容器）、可单元测试。
3. **不提交任何原版资源**：GPLv2 项目，仓库只含代码与可再分发字体。
4. **每个里程碑都能运行、能验证**：可执行 + 测试 + 截图/日志证据。

## 里程碑

| 里程碑 | 内容 | 验收标准 |
| --- | --- | --- |
| **M0** | 解决方案骨架；Core/Assets/Rendering 最小子集；MonoGame 窗口 640×480 + 2x；FontStashSharp 文本；AssetProbe；文档 | 构建 0 警告（warnings-as-errors）；测试全绿；`--smoke-frames` 可运行并出图 |
| **M1** | 图像栈：ICN/TIL 解码、变换层（palette transform 6–13）、`Cursor`、PNG/BMP 导出、黄金图像测试 | 解码 `HEROES.ICN`/`GROUND32.TIL` 与 C++ 输出逐像素一致（抽样 20 个资源） |
| **M2** | 主菜单：`ADVBORD.ICN` 边框、按钮、FNT 位图字体、`.mo` 本地化、BGM 播放 | 可从数据目录启动到主菜单，中英文切换 |
| **M3** | 新游戏/场景设置：地图尺寸、玩家数、种族、难度；H2D 场景列表 | 能创建新对局并进入冒险地图（占位渲染） |
| **M4** | 冒险地图渲染：地形、物体、英雄/城镇精灵、滚动、小地图、路径 | 与参考实现同视角截图对齐（tile 级一致） |
| **M5** | 回合与英雄：移动力、拾取资源、战斗触发、AI 基础 | 完整回合可循环，AI 可行动 |
| **M6** | 城镇：建筑树、招募、市场、城镇界面控件 | 城镇全流程可用 |
| **M7** | 战斗：战场渲染、ATB 回合、法术、怪物动画（82M） | 一场战斗可完整进行到结算 |
| **M8** | 存档/设置：版本化存档 schema、自动存档、键位与音量 | 存/读档往返一致，设置持久化 |
| **M9** | 战役、音频完善、视频（SMK）、地图编辑器；评估 NativeAOT 发布可行性（ADR-011） | 战役可通，编辑器可保存地图，AOT 结论有据 |

## 依赖方向（单向，不允许反向引用）

```
FHeroes2.Core  ←  FHeroes2.Assets  ←  FHeroes2.Rendering  ←  FHeroes2.UI
                        ↑                    ↑                    ↑
                        └──── FHeroes2.Game ─┴────────────────────┘
FHeroes2.Client.Desktop  →  引用以上全部（组合根）
```

## 主要风险与对策

| 风险 | 对策 |
| --- | --- |
| ICN 变换层/裁剪语义复杂 | M1 已落地「参考导出对比」工具链：C++ 侧链接原版源码导出 BMP，C# 侧逐字节比较（ADR-015） |
| 字体：原版位图字体 vs TrueType 度量差异 | `ITextRenderer` 抽象；布局用逻辑像素，位图字体延后接入 |
| 大端/字节对齐陷阱 | 统一 `LittleEndianReader`，禁止 `BinaryReader` |
| 存档与 C++ 不互通 | 自研版本化 schema（`CURRENT_FORMAT_VERSION` 仅作参考），不做互操作 |
| 音频/视频后端差异 | SMK 与 82M 解码作为独立可测试组件，先做数据层再接入播放 |
| 未来可能需要 NativeAOT（单文件/无 JIT 冷启动） | M0 起开启 AOT/裁剪分析器并禁止反射（ADR-011），暂不提供发布配置；M9 评估 |
