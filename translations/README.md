# 语言包源文件

这里保存**可 diff 的** gettext `.po` 源文件；`.mo` 是构建产物，编译后安装到游戏数据目录
`<data>/files/lang/<culture>.mo`（与 fheroes2 的目录约定一致），不入库。

| 文件 | 说明 |
| --- | --- |
| `zh_CN.po` | 简体中文（UTF-8），当前覆盖主菜单、设置、语言选择、快捷键与分辨率说明 |

## 编译与安装

```powershell
pwsh tools/scripts/BuildTranslations.ps1                # 全部 .po：msgfmt --check + 安装
pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN
pwsh tools/scripts/BuildTranslations.ps1 -NoInstall     # 只校验
```

需要 GNU gettext 的 `msgfmt`；脚本会自动探测 MSYS2 的安装位置，也可以用 `-MsgfmtPath` 指定。

## 新增条目的规则

1. `msgid` 必须与参考实现 `fheroes2 1.1.18`（`src/fheroes2/**` 里 `_()` / `_n()` 的英文字面量）
   **逐字节一致**，否则查表失败、运行时回退英文。C++ 中跨行拼接的字符串要取完整原文，
   直接按源码行截取会得到半截 msgid（曾经踩过）。
   可对照参考目录（例如 `fheroes2/files/lang/de.po`）核对 `msgid` 是否存在。
2. 占位符 `%{name}`、`%{count}`、`%{resolution}`、`%{scale}` 必须原样保留，`\n` 也要保持一致。
3. 带上下文的条目（`army|Few`、`hotkeys|All Categories`）在 `msgid` 里保留前缀，**`msgstr` 不写前缀**，
   显示时由 `MoCatalog.StripContext` 去掉。
4. 未翻译的条目不要写成空 `msgstr`——直接不写条目即可，运行时自动回退英文，也能让
   `msgfmt --statistics` 如实反映覆盖率。
5. 复数形式用 `msgid_plural` / `msgstr[0]`；简体中文只需一种形式，头部写
   `Plural-Forms: nplurals=1; plural=0;`。

## 为什么 `.mo` 不放进仓库

原版资源与本项目的约定一致：仓库不含游戏资源（见 `docs/OTHER.md`），而 `.mo` 是派生构建产物，
放在数据目录里才能被 `AssetLocator` 找到；同时 `.po` 才是翻译者需要评审的内容。
