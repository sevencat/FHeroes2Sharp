# 参考对照（REFERENCE）

> **2026-11-24 起**：`PLAN.md` 与 `PROGRESS.md` 已退役并从仓库删除，任务与进度以 **bd (beads)** 为准；
> 全文归档为 closed issue `FHeroes2Sharp-3so`（PLAN）与 `FHeroes2Sharp-jwk`（PROGRESS）。

C++ 参考实现校验值：`version.txt` = `1.1.18`，commit `6c51acfdc`。

## 已移植

> 本节只收「已能跑、有对拍或测试」的映射。逐文件的覆盖率总表（`src/engine` + `src/fheroes2` 共 249 个
> `.cpp` 的三态统计、未转清单与依赖顺序）在 `docs/PLAN.md` 的「参考文件覆盖率（逐文件盘点）」；
> 「部分可用」在那边单列一档，本节不重复。

| C++ | C# | 备注 |
| --- | --- | --- |
| `src/engine/agg_file.h/.cpp`（`AGGFile`） | `FHeroes2.Assets.Agg.AssetArchive` | 打开即逐条校验名称哈希；含越界/重复检测 |
| `calculateAggFilenameHash()` | `FHeroes2.Assets.Agg.AggFileNameHash` | 反向遍历、`hash<<5 + hash>>25`、大小写不敏感 |
| `src/fheroes2/agg/agg.cpp`（`AGG::AGG`、`getDataFromAggFile`） | `FHeroes2.Assets.AssetBundle` | 资料片优先、基础归档兜底 |
| `src/engine/rand.h/.cpp`（`Rand::PCG32`） | `FHeroes2.Core.Random.Pcg32` | 同 multiplier / seed=42 / stream=54；`|1` 强制奇数 |
| `Rand::uniformIntDistribution` | `Pcg32.NextInclusive` | Lemire 拒绝采样，`[from, to]` 闭区间 |
| `src/engine/math_base.h`（`Point`/`Size`/`Rect`） | `FHeroes2.Core.Geometry.Point/Size/Rect` | 右/下边界半开 |
| `src/engine/image_palette.h/.cpp` | `FHeroes2.Core.Imaging.Palette` | 6 bit → `<<2`，循环色复制见下 |
| `src/engine/image.h`（`Image`、`Blit`、`CreateContour`、`Resize`） | `PixelBuffer` + `ScreenBuffer` + `Sprite` + `ImageOps` | 变换层感知的 `BlitSprite`（单层/双层目标两种），`CreateContour`、`Resize` 已搬 |
| `src/engine/image.cpp`（`ApplyTransform`、`ReplaceColorId`、`addGradientShadow`、`addGradientShadowForArea`、`CreateDitheringTransition`） | `ImageOps.ApplyTransform` / `AddGradientShadow` / `AddGradientShadowForArea` / `CreateDitheringTransition` | 目标一律是单层表面；阴影线的斜率取整改用 `MidpointRounding.AwayFromZero`（`std::round` 的语义）；8 个用例与 C++ 逐字节相同（`tools/scripts/CompareEffectsWithReference.ps1`） |
| `src/engine/image.cpp`（`transformTable`、`ApplyPalette`） | `FHeroes2.Core.Imaging.PaletteTransformTable` | 16×256 表，逐字节移植（SHA-256 固定） |
| `src/engine/pal.cpp`（`PAL::GetPalette`、`CombinePalettes`） | `FHeroes2.Core.Imaging.PaletteTable` + `PaletteId` | 13×256 重着色表 + identity；脚本生成 + SHA-256 固定（ADR-020） |
| `src/engine/image_tool.cpp`（`decodeICNSprite`） | `FHeroes2.Assets.Icn.IcnSpriteDecoder` | 单色流 + 彩色流、`0xC0` 变换段 |
| `readIcnFromAgg()` / `ICNHeader` | `FHeroes2.Assets.Icn.IcnSpriteFile` + `IcnHeader` | 6 字节文件头 + 13 字节精灵头 |
| `src/engine/image_tool.cpp`（`decodeTILImages`；1.1.18 没有 `til.cpp`） | `FHeroes2.Assets.Til.TilFile` | 未压缩图块网格 |
| PNG 导出 / 读回（用于回归比对） | `FHeroes2.Core.Imaging.PngWriter` + `PngReader` + `PngImage` | 导出 8 bit RGBA（`transform==1` → alpha 0）；读回支持 0/2/3/4/6 色彩类型、1/2/4/8/16 bit、五种行滤波、`tRNS` |
| `src/engine/image_tool.cpp`（`Save`） | `FHeroes2.Core.Imaging.BmpWriter` | 8 bit 索引 BMP，布局同 `SDL_SaveBMP`；`EncodeSprite` 对应 `Save(image, path, background)` |
| `src/engine/image_tool.cpp`（`decodeBMPFile`） | `FHeroes2.Assets.Cursor.MonoCursorFile` | 单色光标 `*.BMP` 条目（非 Windows 位图），1 = 白、2 = 黑、其他 = 透明 |
| `src/engine/h2d_file.h/.cpp`（`H2DReader`、`H2DWriter`、`readImageFromH2D`、`writeImageToH2D`） | `FHeroes2.Assets.H2D.H2DArchive` + `H2DWriter` + `H2DImageCodec` | 魔数里带版本（3）、`uint32` 长度前缀字符串、每条体是 zlib（`zzlib.cpp` 的 `unzipData`/`zipData`，用 `ZLibStream`）；空名 / 零长 / 越界的记录跳过而不是整档失败，重名取第一条（`try_emplace`）。图像体 17 字节头（w、h、x、y、单层标志）+ 像素 + 可选透明层，长度必须精确匹配；单层 sprite 不分配透明层。写回按名字序重排，压缩级同 `zipData( …, true )`（`SmallestSize`），所以重写的档与参考逐字节一致 |
| `src/fheroes2/h2d/h2d.cpp`（`H2DInitializer`、`h2d::readImage`） | `FHeroes2.Game.H2D.H2DResources` | 68 个文件名的样本表在 `SampleFileNames`（`FrozenSet`，序数比较等于 `std::set<std::string>`）；开档三处失败：缺档 `FileNotFoundException`、读不动 `BinaryFormatException`、名单不符 `InvalidOperationException`（消息照参考）。`Settings::findFile` 的文件搜索没搬，`GetDefaultPath(dataDir)` 给 `files/data/resurrection.h2d`；`Current` 只由组合根赋值，`TryReadCurrentImage` 在没装档时返回 false（同参考全局 reader 未初始化） |
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
| `getDialogHeight()` / `showMessage( header, body, Dialog::ZERO )`（`src/fheroes2/gui/ui_dialog.cpp`） | `FHeroes2.UI.Dialogs.MessageBox` | 标题 `normalYellow` + 正文 `normalWhite`，都按 244 px 折行居中；标题上方与正文上方各 10 px（`textOffsetY`）。`DialogElement` 已接：先按 `( max(行宽, 元素宽) + 10 ) × (个数 + 1) ≤ 244` 打包成行，行间 10 px，元素在行内底对齐并水平居中，正文与第一行之间再加 10 px，行高计入 `getDialogHeight()`；`ProcessEvents` 把 `DialogElementResult` 的弹窗请求交回调用方（参考在元素内部直接弹）。五个派生类齐了：`TextDialogElement`、`CustomImageDialogElement`、`ArtifactDialogElement`（`RESOURCE.ICN` 7 帧 + 器物帧内缩 6 px，右键弹名称/说明，两串文本由调用方给——器物的文本表未建）、`ResourceDialogElement`、`SpellDialogElement`（图标 + `名称 [耗蓝]`，无耗蓝不显示括号，右键弹 `GetName()`/`GetDescription()`，字体以 `IGlyphSource` 传入，耗蓝由调用方用 `Spell.SpellPoints( bag )` 算好；`%{count}` 保持模板，因为 `updateSpellDescription` 的伤害表未搬）。仍未搬：按钮（`Dialog::OK`/`YES_NO` 的 `ButtonGroup`，ADR-024） |
| `Game::MainMenu()` 的右键提示（`showStandardTextMessage( ..., Dialog::ZERO )`） | `MainMenuScene.SetRightButton` + `MessageBox` | 按住右键创建、松开丢弃；招牌标题/说明与门的「Game Settings」文案取自同一份 `.mo` |
| `fheroes2::ValueSelectionDialogElement`（`src/fheroes2/gui/ui_dialog.cpp:937-1024`） | `FHeroes2.UI.Widgets.ValueSelectionElement` | `TOWNWIND.ICN` 第 4 帧（68×19 输入框）+ 第 5/6、第 7/8 帧（20×10 上下箭头，锚点 3,2）；6 px 横向间隔、5 px 纵向间隔，整块 94×25，输入框垂直居中；白色数字按 `LineHeight` 居中；按下与松开都在同一箭头上才生效，到边界停住。差异：参考的按住自动重复（150 ms）在端口交给调用方的循环，且数字固定为白字（参考允许调用方换字体） |
| `Dialog::SelectCount()`（`src/fheroes2/dialog/dialog_selectcount.cpp:79-131`） | `FHeroes2.UI.Dialogs.SelectCountView` + `Dialogs.SelectCountDialog` | `FrameBox` 包 `ValueSelectionElement`：标题按 244 px 折行、行高累加成 `headerHeight`，框高 = `headerHeight + 10 + 30`，数字块在 `(框.x + 38, 框.y + headerHeight + 10)`，OK/CANCEL 由 `ButtonGroup.CreateStandard` 贴底；OK 写回数字、CANCEL 留空。未搬：调用方的上下可选控件与 MAX/MIN 快捷按钮（MIN 的图是参考启动时合成的，归档里没有） |
| `Interface::displayStandardPopupWindow()`（`src/fheroes2/gui/ui_map_interface.cpp:56-81`）+ `Interface::getPopupWindowPosition()` | `FHeroes2.UI.Dialogs.StandardPopupWindow` | 单张 `QWIKINFO.ICN` 158×94：文字区 = 框宽减两侧 16 px（126），左边距 22、垂直 −6 后在框内按行高中点居中，`GlyphText.DrawMultiline(center: true)`；挂位按光标所在 tile 的右下角为中心（`((local - 16) / 32) * 32 + 32 - 框宽/2`），再夹取到界面区；框大于界面区时 `TryCreate` 返回 `null`。按住右键期间由调用方持有（`AdventureMapScene.SetRightButton`）。未搬：`CursorRestorer` / `ImageRestorer`——那是参考为省重绘做的还原，端口每帧整屏重画 |
| `getQuickInfoText()`（`src/fheroes2/dialog/dialog_quickinfo.cpp:455-595`） | `FHeroes2.UI.Dialogs.QuickInfoText` | 只接了不依赖世界状态的三支：`OBJ_NONE`/`OBJ_EVENT`/`OBJ_COAST` → `showGroundInfo()` 的「海岸 / Road / `Ground` 名」；`OBJ_RESOURCE` → 主部件 `OBJNRSRC.ICN` 帧号反查资源名并首字母大写（`Mp2ObjectData.ResourceFromSpriteIndex`）；其余 → `MP2::StringObject()` 的对象名（`Mp2ObjectName.Of`，等于参考的 `default` 支）。未搬：雾（"Uncharted Territory" 常量已留，由调用方传文）、`isCaptureObjectProtected()` 与 "guarded by"、怪物数量、挖掘行与寻路惩罚、神殿/住所/矿/关卡/帐篷/知识树/访问物的附加行 |
| `AdventureMapInterface` 的右键分发（`Interface::MouseRightClick`，同文件） | `FHeroes2.App.GameHost` + `Scenes.AdventureMapScene.SetRightButton` | 按下建框、松开清空；调试入口 `--quick-info`（配合 `--cursor x,y`）走同一条链路，截图基线 `tests/baselines/quick-info.png` |
| `Dialog::Marketplace()` 的状态机（`src/fheroes2/dialog/dialog_marketplace.cpp:385-685`，含 `getTradeCosts()` `:600-607` 与 `getExchangeRateText()` `:80-100`） | `FHeroes2.UI.Dialogs.MarketplaceDialog` | 参考是一个模态循环，端口把它拆成「模型 / 视图」两半，这一行只是模型：交易市场数（交易站固定 `tradingPostValue = 3`）、`max_sell = 财库(resourceFrom)`、`max_buy = GOLD == to ? max_sell*rate : max_sell/rate`（无符号除法，余数丢弃）、滑块位置 → `count_sell`/`count_buy` 的唯一换算、交易区显示条件（`fundsFrom ≤ 0`、两边同资源、非金目标且 `max_buy == 0`）、`Trade()` 走 `OddFundsResource`/`AddFundsResource` 并清空选择、`firstExchange` 两条句子、`-N (M)` / `+N (M)` 两行、税率窗口四支句子。差异：滑块状态用 index 保存、两个计数由它导出（参考同时存三个值）；`ClearSelection` 连 `max_sell`/`max_buy` 一起清（参考留着，但在交易区隐藏时不可读）；`ExchangeRateTextFor` 提成静态纯函数，好测九座市场那支。赠予窗口的视图见下两行（`IsGiftEnabled` 按「≥ 2 个在玩的玩家」建模） |
| `showKingdomIncome()`（`src/fheroes2/gui/ui_kingdom.cpp:32-44`）+ `getResourceDialogElements()`（`ui_dialog.cpp:456-484`） | `FHeroes2.UI.Dialogs.KingdomIncome` | 标题 `Kingdom Income` + 正文 `Kingdom Income per day.`，元素按金、木、水银、矿、硫磺、水晶、宝石的顺序，收入为零的资源不出现；图标取 `RESOURCE.ICN` 的 `ResourceIconIndex.Of` 帧，数量是小号白字。走 `MessageBox` + `ResourceDialogElement`，所以右键图标会要一个「资源名 + 空说明」的弹窗（`Resource::getDescription()` 未搬）。差异：市集右键的 `Dialog::ZERO` 变体已可用（调用方管生命周期），城镇里的 `Dialog::OK` 变体等按钮层 |
| `Dialog::MakeGiftResource()` 的绘制与交互（`dialog_giftresources.cpp:231-346`，含 `SelectRecipientsColors` `:84-139` 与 `ResourceBar` `:147-222`） | `FHeroes2.UI.Dialogs.GiftWindowView` + `Dialogs.GiftWindowLayout` + `Dialogs.GiftWindowArtwork` | 320×234 的活动区（`StandardWindow(320, 234)` 居中）、三段标题 `Select Recipients` / `Your Funds` / `Planned Gift`（活动区内居中）、收件人方框块按**对话宽 320** 居中（不是活动区宽）、两行资源格按 `(320 - (格宽 + 240)) / 2` 起排每格 40 px、计数小号白字居中在图标下方 `高-10` 处、OKAY/CANCEL 贴活动区左下/右下各 10 px。交互：点方框切换收件人（并把计划赠礼清空、财库重新读一遍，因为多人分摊）；点计划行请求 `SelectCount`（`mul` 个收件人时 `max = (财库+计划)/mul`，金步长 100、其余 1；`mul == 0` 给 "First select recipients!"，`max == 0` 给 "You cannot select X!"），结果由调用方喂 `ApplySelection`（计划 +差额，财库 -差额×人数）；OKAY 无收件人或无计划时禁用（过 `PAL::DARKENING`），CANCEL 恒可用。美术：`TRADPOST.ICN` 7-13、`CELLWIN.ICN` 43-48 + 勾号帧 2、`BUTTON_SMALL_OKAY/CANCEL_GOOD\|EVIL` 各 0/1 帧。未搬：写世界（`world.AddEventDate()`、`kingdom.OddFundsResource()`）留在调用方，光标恢复器与逐事件 `render()` 不需要（场景每帧重画） |
| `Dialog::RecruitMonster()`（`dialog_recruit.cpp:242-665`） | `FHeroes2.UI.Dialogs.RecruitDialog` | 事件循环里的全部状态与字符串：`CalculateMax()` 逐只累加问 `kingdom.AllowPayment( cost * ( max + 1 ) )` 并封顶 `available`；开窗即 `result = max`、`paymentCosts = paymentMonster * result`；MAX/MIN 两键的 `SwitchMaxMinButtons` 忠实复刻（`result == max` 只剩 MIN+「Max」，`result == 1` 只剩 MAX+「Min」，中间段只清标签不动按钮，`max == 0` 两键皆灰、OK 也灰）；数字键入走 `processIntegerValueTyping( 0, max, buf )`（累积、超上限的字符被丢且缓冲区不变、退格清空为 0）；左右箭头沿 `monster0` 往下建的升级链循环换兵并重算上限、跳回 `max`；`Title`/`AvailableText` 与 `Cost per troop:`/`Number to buy:` 四句文案。绘制与点击见下两行的 `RecruitWindowView` |
| `Dialog::RecruitMonster()` 的绘制与点击（`dialog_recruit.cpp:96-221`、`:242-665`） | `FHeroes2.UI.Dialogs.RecruitWindowView` | `Draw` 每帧整窗重画（参考用 `background` 备份 + `ImageRestorer` 局部还原，端口不需要）：`StandardWindow( 299, 272 )` 的框、标题、`Available: N`、怪物精灵（宽体左移 22、冠军再右移 1、高过 96 的部分上探）、计数框、`Cost per troop:` 合成框里的单价行、`Number to buy:`、UP/DOWN、MAX/MIN（共位）、左右换兵箭头、OKAY/CANCEL，阴影 `(-5, 5)`。`ProcessEvents` 照参考 `Action()` 的 `else-if` 顺序转发：换兵箭头（重算上限、跳回 `max`）→ 怪物右键请求 `ArmyInfo`（不带键）、怪物左键请求 `ArmyInfo`（带键）→ 数字键/退格直接进 `RecruitDialog` → 计数框点击请求 `SelectCount` → 滚轮与 UP/DOWN（含按住连按）→ MAX → MIN → OKAY → CANCEL → 四处 Tooltip（OKAY/CANCEL/MAX/MIN 的原句以常量留在类上）。计数框的点击矩形是 `pos + (118,143,68,19)`，与显示切图 `(134,159)` 不是同一个点；右键判定只看「按住」（参考在 `isMouseRightButtonPressed` 为真时就建框）。`Refresh()` 按 `SwitchMaxMinButtons` 的结果同步两键与标签（禁用过 `PAL::DARKENING`）。未搬：热键（`DEFAULT_UP/DOWN/LEFT/RIGHT/OKAY/CANCEL`）、`ArmyInfo()` 的窗口内容、扣款（归调用方） |
| 同上两处的坐标与切图 | `FHeroes2.UI.Dialogs.RecruitWindowLayout` + `Dialogs.RecruitWindowArtwork` | 所有偏移量收在 `Create( activeArea )` 一处（活动区 299×272）：计数框 `+118,+143`、数字中心 `+151,+147`、价格框 `+132,+38`、MAX/MIN 中心 `+253,+140`、OKAY 与 CANCEL 贴底 `+233` 各 18 px、UP/DOWN `+189,+138`/`+189,+153`、滚轮区 `+114,+139 100×30`、换兵箭头 `+64` 行上的 `+6`/`+105`、怪物区 `24,19 75×95`、标题/库存句/`Number to buy:`/金额行的居中与右对齐点、第二资源与总量图标的错位。美术：`RECRBKG` 第一帧切 `68×19` 计数框（源 `134,159`）、价格框由 `BLDGXTRA` 标题帧顶 + `RECRBKG` 左右边拼成 `132×67`（这两件是邪恶界面唯一需要重新着色的）；`RECRUIT` 0/1、2/3 是 UP/DOWN 的松开与按下，`MONSTER_SWITCH_*_ARROW` 各 2 帧，OKAY/CANCEL/MAX/MIN 的 `DialogButtonArtwork` 由调用方按善良/邪恶给，`RESOURCE.ICN` 七帧按 `ResourceIconIndex` 取 |
| `TradeWindowGUI` 的绘制与点击（`dialog_marketplace.cpp:118-293`、`:400-533`） | `FHeroes2.UI.Dialogs.MarketplaceView` + `Dialogs.MarketplaceLayout` + `Dialogs.MarketplaceArtwork` | `Create( dialog )` 保留参考的 297+40 框；`Draw` 每帧整窗重画（参考用 `ImageRestorer` 擦旧交易区，端口不需要）：标题、七格（左行财库、右行 `1/rate` 或财库）、两条标题、税率图标与 `x N` / `x 9+`、两个光标、交易区（帧 1 的条、滑块拇指、两边资源图标 ±70、帧 0 的箭头、Max/Min/`Qty to trade`、`-N (M)` / `+N (M)`）、店铺句子、交易按钮与左右箭头、赠予/退出。`ProcessEvents` 转发退出、赠予（`IsGiftEnabled` 才响应）、税率图标、成交、滑块拖拽与滚轮、Max/Min（Min 是 1 不是 0）、左右箭头（点按 + 按住重复）、左行格子的左右键与右行格子。`Refresh()` 同步 widget 状态（禁用时过 `PAL::DARKENING`），滑块的槽位只依赖美术所以在构造时就定死。差异：Max/Min 参考的 `smallYellow` 缺，借小号白字；退出按钮的两个精灵由调用方给；`TRADPOSE` 的 15-18 帧常量已留、邪恶界面未接 |
| `Resource::getIconIcnIndex()`（`src/fheroes2/resource/resource.cpp`） | `FHeroes2.Game.Resources.ResourceIconIndex` | `RESOURCE.ICN` 的七帧序号（木 0 … 金 6）与反查 `Between`，`Count` 供美术校验 |
| `Resource::String()`（`src/fheroes2/resource/resource.cpp:335-357`） | `FHeroes2.Game.Resources.ResourceText` | 七个名字 + `Unknown`；未搬 `Resource::getDescription()`（资源对话框才用） |
| `src/fheroes2/gui/ui_window.cpp`（`fheroes2::StandardWindow`） | `FHeroes2.UI.Dialogs.StandardWindow` + `StandardWindowChrome` | 边框来自 `SURDRBKG.ICN`（横）与 `WINLOSE.ICN`（竖 + 四角），内部铺 `STONEBAK.ICN`；角块 22、可重复边框起点 43、接缝 10、底纹让位 22；阴影只挂在左/下 16 px（`TotalArea` 比窗口宽 16）；4 个用例与 C++ 逐字节相同（`tools/scripts/CompareWindowWithReference.ps1`） |
| `DrawBorder()` / `ADVBORD.ICN` 边框 | `MapFrame`（地图外壳，ADR-048）；对话框侧未移植 | `ADVBORD.ICN` 单张 640×480；九宫格拼装已用于冒险地图外壳（448×448 视口），对话框边框仍走 `BUYBUILD.ICN`（见上两行） |
| `ICN::getAnimatedIcnIndex()`（`src/fheroes2/agg/icn.cpp`）+ `Game::DelayType::MAIN_MENU_DELAY`（`game_delays.cpp`） | `FHeroes2.Assets.Icn.IcnAnimation` | 帧表按场景声明（起始帧 + 帧数 + 每帧时长），计时同 `fheroes2::TimeDelay`：整帧才前进、长停顿跳帧不补播 |
| `src/fheroes2/gui/ui_button.cpp`（`Button` / `ButtonSprite` / `ButtonBase` / `ButtonGroup`） | `FHeroes2.UI.Widgets.Button` + `ButtonState` / `ButtonBase` + `ButtonSprite` + `ButtonGroup` + `Dialogs.DialogButton` | 主菜单招牌 = `Button`（三帧精灵 + hover 状态机，`Area` 取 released 的包围盒 = ICN 的 `OffsetX/OffsetY`，缺帧回退）；对话框按钮 = `ButtonSprite`（released/pressed/可选 disabled，禁用时过 `PAL::DARKENING`）+ `ButtonGroup`（按下与松开都在区内才算点击、按住只点亮按下所在的按钮、OK/Enter 与 CANCEL/Escape 热键、单个 OK/CANCEL 两个热键都能关）；自动摆位 = `ButtonGroup.CreateStandard`（`ButtonGroup( area, types )`），文字按钮只移植了精灵侧（`ButtonArtwork`），标签仍待 `BUTTON_*_FONT_*` 字体表 |
| `Translation::gettext()` + `Settings::getGameLanguage()` | `FHeroes2.App.Localization.GameText` | 一个语言一个实例；英文 = msgid，`zh_CN` = `.mo`；`--lang` / `F2` 切换 |
| `LocalEvent` 的鼠标坐标换算（窗口 → 逻辑像素） | `PresentationViewport.TryToLogical` | 整数倍 + 黑边；黑边上的点返回 `false` |
| `Display::render()` 的文字叠加 | `Scenes.IScene.DrawOverlay` | 只有诊断屏用（SDL3_ttf）；真实画面全部走索引字形 |
| `src/fheroes2/gui/ui_button.cpp` 的 `resizeButton()` / `addButtonShine()` / `makeTransparentBackground()` | `FHeroes2.UI.Widgets.ButtonArtwork`（`Resize` / `AddShine` / `MakeTransparentBackground`） | 空按钮按「宽三等分、高五等分」重复拉伸，四角原样保留；只有「两个方向都变大」那一支有中空区，用原图中心像素填充（`centralPosition = w*h/2 - 1 - min(w,h)/2`）；闪光像素的绝对坐标表、`width > 50` / `> 96` / `height > 25` 三层条件、Good/Evil 两套配色（10 / 37,15 / 39,16）逐像素移植 | 
| `getButtonSpecificValues()` + `getTextAdaptedSprite()`（同上文件） | `FHeroes2.UI.Widgets.DialogButtonTemplate` + `ButtonTemplateMetrics` | 6 个模板的 `textAreaBorders` / `minimumTextArea` / `maximumTextArea` / `backgroundBorders` / released、pressed 文字偏移全部照抄（Good/Evil 最小文字区 86×15、边框 10×8、偏移 6,4 / 5,5）；文字色只保留调色板 id（`WhiteFont` / `GrayFont`） |
| `ButtonGroup( const Rect & area, const int buttonTypes )` | `FHeroes2.UI.Widgets.ButtonGroup.CreateStandard` + `IDialogButtonArtworkSource` / `DialogButtonArtwork` | YES\|NO、OK\|CANCEL、单个 OK、单个 CANCEL 四支；一对按钮的左右各留 `(area.width - w1 - w2)/4`，单个按钮居中并贴底；精灵自带的 `OffsetX/OffsetY` 由 `ButtonSprite` 叠在摆位坐标上（与参考实现相同） |
| `src/engine/image.cpp` 的 `Copy` / `Fill` / `FillTransform` / `ReplaceColorIdByTransformId` / `SetPixel` / `DrawLine` / `CreateContour` / `Blit`（双层写） | `FHeroes2.Core.Imaging.ImageOps.Copy` / `FillSprite` / `FillTransform` / `ReplaceColorIdByTransformId` / `SetPixel` / `DrawLine` / `CreateContour` / `BlitSprite` | 写颜色时把 transform 字节清 0；`Copy` 的三条规则（目标单层只拷颜色、源单层写 0、双层互通）逐条移植；`DrawLine` 是同一个整数 Bresenham（`ns = major/2`，负了才进次轴） |
| `dialog::openGameSettings()`（`src/fheroes2/dialog/dialog_game_settings.cpp`，子集） | `FHeroes2.UI.Dialogs.SettingsDialog` + `Dialogs.SettingsOption` | 一个 `StandardWindow` + 标题 + 每行一个选项 + 底部标准按钮；行 = 左标签 + 右对齐的值 + 两侧自绘箭头（`ChevronWidth` 4 × 高 7，色号 10；参考实现的 `OptionButtonGroup` 箭头美术未移植），点左半格退一格、右半格进一格（循环）；OK / Enter 应用、Escape 取消；当前只有「Language」一行（ADR-027） |
| `ICN::UNIFORM_GOOD_OKAY_BUTTON`（`game_assets.cpp` 运行时生成） | `FHeroes2.App.Scenes.SettingsArtwork` | 参考实现要么用 `SYSTEM.ICN` 第 1/2 帧，要么用 `EMPTY_GOOD_BUTTON` + 文字现做；端口取前者（`SKIP` 分支 `useOriginalResources()` 的做法），字按钮生成器只留在 `ButtonArtwork` 里备用 |
| `src/fheroes2/gui/ui_tool.h:48` / `.cpp:123`（`MovableSprite`） | `FHeroes2.Core.Imaging.MovableSprite` | 位置 + 显隐；`Sprite` 在 C# 是 `sealed`，所以用组合。参考的 `show()` 会立即 `Blit` 到 `Display`，这里只置标志位、由场景统一呈现（ADR-057） |
| `src/fheroes2/game/game_assets.h:33` / `.cpp:5817/5834`（`Assets::getImage`、`getImageCount`、`loadICN`） | `FHeroes2.Game.Assets`（`Current` / `GetIcn` / `GetImage` / `GetImageCount`） | 全局取图门面，解码结果按（文件, 帧）缓存；参考越界给 `errorImage`，这里给 `null`；键用归档文件名而不是 `ICN::` 枚举。`getImageCount` 是 `getCharacterLimit` 的依赖，缺条目返回 0。另外补了 `TryReadRaw`（`getDataFromAggFile( name, false )`：解压后的原始条目，给语言探测算 CRC 用）与 `GetBaseArchiveImage`（`getDataFromAggFile( name, true )` 加一次解码，只走基础归档，供波兰资材换星号） |
| `src/fheroes2/editor/editor_ui_helper.h:42` / `.cpp:36`（`Checkbox`、`createColorCheckboxes`、`drawCheckboxWithText`、`renderResources`） | `FHeroes2.Game.Editor.EditorUiHelper` + `Editor.Checkbox` | 帧号 `NONE=1`、其余 `Color::GetIndex + 43`；勾选标记落在 `+2,+2`；步进 32；文字左移 23、下移 4，框带 `(-4, 4)` 渐变阴影。资源面板：边缘 7、格 39×32、两行、金子在第二行跨两格。`CELLWIN_EVIL` 走 `NotSupportedException`（启动期生成器未搬） |
| `src/fheroes2/resource/resource.h:37/48/64` / `.cpp:58`（`Cost`、`Funds`、`Resource` 常量与 `CalculateEventResourceUpdate`） | `FHeroes2.Game.Resources.Funds` + `Resources.Resource` / `Resources.Cost` | 七字段顺序是 ore、wood、mercury、sulfur、crystal、gems、gold（构造与运算符都按这个顺序）；加减乘除、`Max`、`Get`/`GetPtr`、`Trim`/`Reset`、`GetValidItems*`、`GetFirstValidResource`、`GetLowestQuotient` 逐条移植。差异：`operator=` 从 `Cost` 改名 `SetFrom`（C# 不能重载赋值），`GetPtr` 改成静态 + `ref` 参数（CS8170），重载 `==` 时被迫重写 `Equals`/`GetHashCode`（CS0660/CS0661）。未搬：`Resource::Rand`、`Resource::getIconIcnIndex`/`getResourceTypeFromIconIndex` 的 ICN 序号映射、`BoxSprite` 的图标放大/降级分支（价格框部分见城建说明框那张表）、流序列化；`Resource::String` 见上一张表 |
| `src/fheroes2/gui/ui_text.h` / `.cpp`（`FontSize`、`FontColor`、`FontType`、`TextLineInfo`、`TextBase`、`Text`、`TextInput`、`MultiFontText`、`FontCharHandler`） | `FHeroes2.Game.Gui.*`（`FontType.cs`、`FontCharHandler.cs`、`TextBase.cs`、`Text.cs`、`TextInput.cs`、`MultiFontText.cs`、`TextAlgorithms.cs`） | 字形就是 `FONT.ICN` / `SMALFONT.ICN` 的逐帧图，所以只依赖 `Assets.GetImage` / `GetImageCount`；宽度、换行（`_getTextLineInfos`）、`fitToOneRow`、`fitToArea`、`drawInRoi`、混排（`_getMultiFontTextLineInfos`）、输入框（`TextInput` 的可见窗口与光标几何）全部按参考算法搬。**帧号要减 `0x20`**：参考在加载时给字体数组前置 32 个空帧让下标等于 ASCII（`game_assets.cpp:2555`），归档里的原始帧 0 才是 `' '`，C# 侧没有那步预处理，所以 `FontCharHandler` 统一 `character - GlyphFrameOffset` 取帧、`getCharacterLimit` 统一 `+32`。放在 `FHeroes2.Game` 而不是 `Rendering`：参考的 `ui_text.cpp` 直接调 `Assets` 门面，而门面在 `FHeroes2.Game`，放在 `Rendering` 会形成反向依赖。`MultiFontText` / `TextInput` 靠参考的 `friend` 与 `protected` 读写 `Text` 私有成员，C# 改成同程序集 `internal`，`MultiFontText.Add` 还要 `Clone`（参考按值入 vector）。彩色面与大号面由 `FontGlyphTable` 惰性生成——参考的 `ICN::GRAY_FONT` / `YELLOW_FONT` / `WHITE_LARGE_FONT` 都不是归档条目：`game_assets.cpp:736` 的 `CopyICNWithPalette` 把 `FONT.ICN` 逐帧过一遍 `PAL::GetPalette`，`game_assets.cpp:3054` 则把每帧 `Resize` 成两倍（位置也乘 2）。C# 侧复用 `PaletteTable.Get` + `ImageOps.ApplyPalette` / `ImageOps.Resize(Sprite, Sprite)`，整表按（归档包, 字号, 颜色）缓存，换包即失效；大号按参考只有白色（`ui_text.cpp:61` 的矩阵里没有黄色/灰色大号）。两个渐变面走 `game_assets.cpp:2433` 的 `generateGradientFont`：每帧放大 6 像素留出描边位、刷一层纵向渐变（`applyFontVerticalGradient`，`ui_font.cpp:7151`），再依次叠 `ImageOps.CreateContour` 画的内描边 / 黑边 / 外描边。白色面并不是归档原图：加载后要先过一遍 `modifyBaseNormalFont` / `modifyBaseSmallFont`（`ui_font.cpp:6852` / `7046`，见 `FontRepairs`）——`$`/`&` 挪锚点、`%` 擦掉多余白线、`\`（两种字号）与普通号 `|` 是现画的、小写 `k` 重拼、小号 `*` 手绘；修补逐帧克隆，所以帧之间不再共享对象，派生面不必按引用去重。译版资材还更早一层：`game_assets.cpp:2461` 的 `processICN` 拿 `FONT.ICN` 的原始字节算 CRC32 认版本（`ResourceLanguage`，表见 `ui_language.cpp:41`，认不出算英文），波兰 GoG 小号字体整体下移一像素、波兰资材的 `*`（被画成 `.`）从基础归档重取 `42 - 32` 帧、CP1251/CP1252 资材插空帧 + 对调 + 截断重排（`FontAssetFixes`，顺序在修补之后、派生之前）；法语资材再按 `ui_font.cpp:6897` / `7091` 重画 `$`/`&`/`*` 并把其余十个可打印字符指到 `.`（`FontFrenchRepairs`）。重排会改变帧数，所以 `getCharacterLimit` 下沉到 `FontGlyphTable` 去问已排好的表。四个按钮面（`ICN::BUTTON_GOOD_FONT_RELEASED` 等，归档里没有）由 `ButtonFont` 从零画出 ASCII 形（`generateGoodButtonFontBaseShape`，`ui_font.cpp:4948`），再经 `updateButtonFont`（`ui_font.cpp:170`）派生：白面加右下阴影 + 描边、按下换深色并把阴影翻到左上、灰面只把绿换红；只支持白色与灰色（`ui_text.cpp:61` 的矩阵）。未搬：`ui_language` 切换、各代码页的按钮字形补充 |
| `src/fheroes2/castle/castle_dialog.cpp:325-384`（`Castle::OpenDialog` 的坐标表） | `FHeroes2.UI.Dialogs.CastleDialogLayout` | 整屏 640×480：底部 19 像素三段（`SMALLBAR` 1/2、0、3/4），状态文字两端各让 16 像素；旗帜 `(5,262)`、英雄像 `(5,361,100,92)`、两排军队条原点 `(112,262)` / `(112,361)`（格子与 6 像素间距归 `ArmyBar`）、EXIT `(553,428)`（尺寸取自帧，不猜）、城名牌居中于 `320 - 帧宽/2, 248`；资源面板的命中区到 EXIT 上沿再上收 3。底部三段（左箭头、状态条、右箭头）各取自己那一帧的宽高：状态条是帧 0、两个箭头是帧 1/3，拿状态条的宽度去摆箭头会把状态条和状态文字推出 640 像素之外。只管坐标：素材、状态文案、军队内容都在视图层。建筑区命中表已由 `CastleBuildingHitTest` 补上。未搬：状态栏滚动文字 |
| `src/fheroes2/gui/ui_castle.cpp:242-301`（`fheroes2::drawResourcePanel`） | `FHeroes2.UI.Widgets.ResourcePanel` | 面板 `offset + (552, 262, 82, 192)` 先填黑；格宽 39、格高 32、两列间距 2，行偏移是 `{ 0, maxH+fontH+2, (maxH+fontH)*2-1, (maxH+fontH)*3+1 }`（刻意不均匀）；左列木/晶/矿、右列硫/水银/宝，图标底对齐、数字在小号白字行高之下 1 像素处居中；金子既不底对齐也不落在列内，整块居中画在第四行顶、数字在 `行顶 + 图标高 + 1`；最后把 EXIT 按钮的常态帧盖在 `+1,+166`（原始资源下即 `TREASURY` 帧 1，`game_assets.cpp:918-926`） |
| `src/fheroes2/castle/castle_building.cpp:44-470` + `ui_castle.cpp:110-148`（`drawCastleDialogBuilding`） | `FHeroes2.UI.Dialogs.CastleScreenPainter` | `getTownIcnId` 的六张 `TOWNBKG`；野蛮人河 `TWNBEXT1` 与法师峡谷河 `TWNZEXT0` 都用 `1 + index % 5` 帧；海湾（`TWN*EXT0`/`WizardCastleBay`）在「船厂正在淡入」或「有海岸且（野蛮人 或 没造船厂）」时画帧 0 加动画帧；建筑按 `BuildingsRenderQueue` 顺序画主帧（法师塔按等级取帧、死灵每级 +6）、动画帧（`getAnimatedIcnIndex`）、骑士「右塔替城堡挂旗」的替换、扩展件（船厂船/码头动画、雕像与水晶园合体 `TWNSEXT1`、骑士左农场挂在右农场左下、野蛮人与女巫的队长宿舍左半、法师河）、连接件（野蛮人 `TWNBEXT2/3`、死灵船长桥）；所有 blit 裁到背景矩形。与参考的一处结构差异：参考靠显示缓冲的脏矩形，淡入分支「没有变化」时直接返回，端口每帧从背景重建，所以淡入时也要把整座城画一遍。旗帜的玩家配色由 `CastleFlagPalette` 负责（`ui_castle.cpp:42-165` 的 `getModifiedByColorImage`：调色板 152~174 整段线性映射到该玩家色的 16 色色阶，只有骑士城堡/左塔/右塔/五级住宅两种/法师城堡六张图，动画帧整帧换、静态帧只换那几面 3×3 小旗；`DrawBuilding` 的 `color` 参数接 `Castle.Color`、无主时走恒等）；未搬：`AlphaBlit`（新建筑瞬间出现而非淡入） |
| `src/fheroes2/castle/castle_dialog.cpp:385-398` + `402-451`（`Castle::OpenDialog` 的合成与状态行） | `FHeroes2.UI.Dialogs.CastleScreenView`（+ `CastleScreenArtwork`、`CastleScreenFrame`、`Widgets.ArmyBarArtwork`） | 静态帧一次性取好：`STRIP` 0/1/2/3/11、`CREST` 按玩家色、`TOWNNAME` 0、`SMALLBAR` 0…4、`RESOURCE` 0…6，任一缺失 `TryCreate` 返回 null；EXIT 用 `TREASURY` 1/2（参考 `BUTTON_EXIT_TOWN` 在 `LAST_VALID_FILE_ICN` 之后、由 `game_assets.cpp:918-932` 生成，原始资源分支就是这两帧）。`Show` 建两条 `ArmyBar`（5 列、6 像素间距，`SetStripBackground(STRIP 帧 2, 帧 1)` 给格子底板、选中框与 43×43 的尺寸）；`Draw` 顺序：建筑层（裁到背景）→ `STRIP` 底板 `+256` → 徽章或船长像 → 英雄像（或空像 + 空军队底板）→ 两条军队条 → 状态条（先按宽度截断再居中）→ 城名牌 → 资源面板（内含 EXIT，按下换帧）。状态行优先级：EXIT（`isCastle()` 决定 Castle/Town）→ 资源面板 `Show Income` → 上一城 → 下一城 → 英雄像 `View Hero` → 建筑 `CastleDialog.GetStatusLine`；全不中返回空串，由调用方换成日期行。未搬：`AlphaBlit`（英雄淡出只按 `HeroAlpha` 整条跳过）、状态栏滚动 |
| `src/fheroes2/castle/castle_dialog.cpp:493-776`（循环里的 `MouseClickLeft` 分派） | `FHeroes2.UI.Dialogs.CastleScreenInput` | 命中的顺序照参考：EXIT → 两个箭头（`CanSwitchCastle` 对应 `isEnabled()`）→ 资源面板 → 英雄像 → 建筑（倒序）。按下与松手都要落在同一目标上才算一次点击；按下帧只看「按住且指针现在在区域内」。`TakeAction` 把一次完成的点击交出去（消费掉），循环本身仍留给场景层 |
| `src/fheroes2/castle/castle_dialog.cpp:416-435`（`BuildingsRenderQueue` 的命中） | `FHeroes2.UI.Dialogs.CastleBuildingHitTest` | 倒序遍历渲染队列（最上层先问），`!Castle.IsBuild` 的建筑跳过，`CastleBuildingInfo.GetArea` 的任一矩形命中即返回该建筑；都不中返回 `BuildingType.Nothing` |
| `src/fheroes2/castle/castle_dialog.cpp:470-859`（循环本体） | `FHeroes2.App.Scenes.CastleScene` | 整屏占满 640×480：合成交给 `CastleScreenView`、命中交给 `CastleScreenInput`，这里跑循环——EXIT 关闭，`<`/`>` 在 `Kingdom.Castles` 里循环（回合结尾的 `PreviousCastle`/`NextCastle`，不足两座时两个箭头都不响应）；指针不在可交互区域时状态行换成 `world.DateString()`；镇内动画每 `CASTLE_AROUND_DELAY`（200 ms）走一帧；点魔法塔开 `MageGuildWindow`、点水井开 `WellWindow`、点住宅（`GetActualDwelling` 认得出升级后的）开 `RecruitWindow`（`castle_dialog.cpp:683-687`，怪物取 `DwellingMonsters.FromDwelling`、库存取 `Castle.GetMonstersInDwelling`、窗口下移 `-65`），窗口打开时输入与绘制都转交它（模态，`IsWindowOpen`）；开窗前把整屏留一份底（`RememberTownFrame`），关窗贴回去——底栏 `<ICN::STRIP>` 的兵栏口子是透明的，重画城镇屏不会盖住它们（`castle_dialog.cpp:136` 的 `Blit` 同样跳过透明像素）。兵栏用 `ArmyBar` 的非图标形态：格子底板与被选中框是 `STRIP` 帧 2/1（`army_bar.cpp:198-201`、`:300`），格子里的立绘是 `MONH####` 第 0 帧（`ui_monster.cpp:29` 的 `monster.ICNMonh()`），不是战斗动画档案——后者的第 0 帧只有一个 1×1 的锚点。`--town-building <name>` 走同一个 `OpenBuilding`，脚本里不用鼠标也能开窗。点市场开 `MarketplaceWindow`（`castle_dialog.cpp:630-640` 的 `Dialog::Marketplace( kingdom, false )`），右键也转给窗口（`SetRightButton`：市集按住右键弹「王国每日收入」的 `Dialog::ZERO` 框，松开就收起，`--right-button` 就是按住不放）；说明框 `_message` 是模态的（左键按下或 ESC/Enter 收起，`Update` 期间底下的窗口冻结，`IsWindowOpen` 把它算进去）。未搬：`BUILD_STRUCTURE`（城镇屏点空地进建筑列表）、英雄与船长的肖像帧、`AlphaBlit` 淡入、玩家配色（建造确认框见下一行） |
| `src/fheroes2/dialog/dialog_thievesguild.cpp:242-486`（`Dialog::ThievesGuild`） | `FHeroes2.App.Scenes.ThievesGuildWindow`（+ `UI.Dialogs.ThievesGuildView`、`ThievesGuildLayout`、`ThievesGuildReport`、`ThievesGuildRanking`、`ThievesGuildRow`、`ThievesGuildArtwork`） | 整屏 640×480 的排行：`STONEBAK(_EVIL)` 帧 0 铺底，`TOWNWIND` 帧 22…27 当旗帜（`22 + Color::GetIndex`），`LOCATORS(_EVIL)` 帧 22 是英雄小窗，`MINIPORT` 帧 `portraitId - 1` 是小肖像，`MONS32` 帧 `monster - Peasant` 是最强兵，`SMALLBAR` 帧 0 垫底栏（左半 + 右半拼接，右半的源偏移 `width - 320 + exitWidth - 1`），EXIT 复用 `WELLXTRA` 0/1（原版 AGG 没有 `BUTTON_GUILDWELL_EXIT`，`game_assets.cpp:904-911`）。十行按 `ThievesGuilds` 逐级解锁：Towns/Castles/Heroes 无条件，Gold/Wood&Ore >1，稀有资源/方尖碑 >2，宝物/军队实力 >3，收入 >4；神谕处传 `0xFF` 当公会数，十行全开且标题换 `Oracle: Player Rankings`。排行按值降序、等值合并成一个 `PlayerColor` 位掩码组，组占一列、组内旗帜挨着排在列心（`264 + i*68 - 面数*旗宽/2 + 3`，纵向 `row - 4`）。`Best Hero` 门槛 0、四维 1、个性 2、最强兵 3。左键 EXIT 或 ESC/Enter 退出，右键 EXIT 弹 `"Exit this menu."`（复用市场的模态 `_message`，按住右键才留） |
| `src/fheroes2/dialog/dialog_quickinfo.cpp:436-447`（`makeRectQuickInfo`）+ `:693-911`（`showQuickInfo( const HeroBase & )`）+ `ui_castle.cpp:196-220`（`getCastleLeftFlagIcnIndex`）+ `castle_dialog.cpp:551-553` | `FHeroes2.UI.Dialogs.HeroQuickInfoView`（+ `HeroQuickInfoLayout`、`HeroQuickInfoArtwork`） | 鼠标旁边的英雄速览框：`QWIKHERO` 帧 0 的 236×160 底板，内部是一个固定的内容框（底板 `+28,+10` 起 146×144）——名字居中写在 `+2`，完整信息时后缀 `" (Level N)"` 且名字先截到 `236-39-等级宽度`；小肖像的相框是 `LOCATORS` 帧 22（邪恶界面 `LOCATORE`）横居中在 `+12`，像是 `MINIPORT` 的「编号 - 1」帧贴在 `+16`（30×30）；左士气（`MINILKMR` 3~5）在 `+10`、右幸运（0~2）在 `+120`，数量为 1 时下移到 `+20`，多枚时步进「图标高 - 1」；两面阵营旗用 `FLAG32` 的偶/奇帧（六色 0~11、中立 12~13），左旗从 `(146-40)/2` 往左伸、右旗从 `(146+40)/2` 往右伸；`Attack:`/`Defense:`/`Spell Power:`/`Knowledge:`（右移 89）/`Spell Points: 当前/上限`/`Move Points: 当前/上限` 六行，从相框下沿 `+13` 起每行 11 像素；军队一行 `DrawSingleDetailedMonsterLine` 画在 `-7,+118`、宽 160。亡灵军团的士气图标先 `GRAY` 再 `DARKENING`。`isFullInfo` 为假时只画名字、相框与 `DrawMultipleMonsterLines` 版的军队。框的位置照 `getPopupWindowPosition`（`StandardPopupWindow.PositionOf`），参考的模态循环（按住右键期间显示）由 `CastleScene.SetRightButton` 的按下/松开两个端点实现 |
| `castle/captain.cpp:44-84`（`GetPortraitIcnId`/`GetFlagOffset`）+ `:200-250`（`GetPortrait`）+ `castle_dialog.cpp:138-145`（徽记换成船长像）/`:547-548`（右键船长） | `FHeroes2.UI.Dialogs.CaptainPortrait`（+ `HeroQuickInfoSubject`） | 船长大头像是 `PORT0090`~`PORT0095` 帧 0（骑士、野蛮人、女巫、术士、巫师、亡灵），克隆一份后把 `ICN::getFlagIcnId( 颜色 )` 帧 0 贴到种族偏移上（骑士/女巫 `(43,9)`、野蛮人 `(42,8)`、术士 `(41,9)`、巫师 `(42,10)`、亡灵 `(42,9)`，其余 `(0,0)`），原图不动；小头像是 `MINICAPT` 帧 0~5（同序）。城镇屏左侧在船长住处建好后画它（101×93，从 `CREST` 的 `+5,+262` 原样贴），否则画 `CREST`。速览框改成吃 `HeroQuickInfoSubject`：`FromCaptain` 的 `Level`/`MovePoints` 为 null，于是没有等级后缀、没有 `Move Points:` 行，名字取城镇名、军队是卫戍本身；右键先判徽记（`CrestArea`）再判英雄像 |
| `src/fheroes2/castle/castle_tavern.cpp:39-54`（`Castle::_openTavern()`）+ `castle_dialog.cpp:697-701`（`BUILD_TAVERN` 分支） | `FHeroes2.UI.Dialogs.TavernView`（+ `TavernDialog`）+ `FHeroes2.App.Scenes.CastleScene` | 吧台手与本周传闻的模态框：`TAVWIN.ICN` 的动画元素（背景帧 `{0,1}`、首帧 2、20 帧、`CASTLE_TAVERN_DELAY` 75ms），一个 `TextDialogElement` 里装两段 `MultiFontText`（引子 `A generous tip for the barkeep yields the following rumor:` 与 `world.GetCurrentRumor()`，中间 `\n\n`，都走 `fontWhite`），标题 `GetStringBuilding( BUILD_TAVERN )` = `Tavern`，按钮只有 `Dialog::OK`。框体与标题/说明/按钮布局复用 `MessageBox`（`BUYBUILD` 框 + `SYSTEM` 的 OK，`FrameBox` + `ButtonGroup`）；两段文字在端口仍是两个 run，但本地化未移植（ADR-139），都用 `FontType::normalWhite()`。城镇屏是模态的：只有 OK、ESC、Enter/小键盘 Enter 能关，右键不关；每帧先把城镇屏重画再叠框。亡灵的酒馆位是 `BUILD_SHRINE`，走的是说明框，不是这个函数 |
| `src/fheroes2/castle/castle_town.cpp:66-160`（`Castle::DialogBuyHero()`）+ `heroes.cpp:84-121`/`2100-2116`（`defaultHeroNames`/`GetName`）+ `kingdom.cpp:248-269`（补位） | `FHeroes2.App.Scenes.RecruitHeroWindow`（+ `UI.Dialogs.RecruitHeroView`、`RecruitHeroLayout`、`RecruitHeroArtwork`、`Game.Heroes.HeroNames`、`Kingdom.ActionNewWeek( World, ... )`） | 320 宽的招募英雄确认框：标题 `Recruit Hero`（`FontType::normalYellow()`，写在框体左边缘 `+2`，**不**占领先的 10 像素）、`SURRENDR` 帧 4 的 111×105 相框（横居中）、大肖像（`PORT00xx` 帧 0，贴在框内 `+5,+6`）、白字说明（244 像素换行，`<名字> is a level N <种族> without artifacts/artifacts.`）、`PaymentConditions::RecruitHero()` 的 2500 金币价格框（`Resource::BoxSprite`），最下一排 OKAY/CANCEL（`SYSTEM.ICN` 1/2、3/4）。框体总高 = 标题 + 10 + 相框 + 10 + 说明 + 10 + 价格，超出就 `TryCreate` 返回 null。`AllowBuyHero( guestHero, kingdom )` 为假时 OKAY 置灰。右键：相框上出英雄速览（`Dialog::QuickInfo`，未搬）、OKAY 上 `Okay` + `Recruit <名字> the <种族>.`、CANCEL 上 `Cancel` + `Exit this menu without doing anything.`（都是 `Dialog::ZERO`，按住右键才留）。招人走 `Castle::RecruitHero( hero, kingdom, world, guest )`，成功就关列表回城镇屏并重画英雄像（`castle_dialog.cpp` 的 `RecruitHero` 分支把 `alphaHero` 归零）。英雄名字是 `_customName` 为空时回落 `defaultHeroNames[id]`（73 条，顺序即编号）；酒馆名单的补位在 `Kingdom::ActionNewWeek()` 结尾的 `GetRecruits()`（`kingdom.cpp:268`），端口放在带 `World` 的重载里，由 `Kingdoms.NewWeek( World, ... )` 逐国调用 |
| `src/fheroes2/castle/buildinginfo.cpp:435-537`（`Dialog::BuyBuilding()`） | `FHeroes2.App.Scenes.BuildWindow`（+ `UI.Dialogs.BuildDialogView`、`BuildDialogLayout`、`BuildDialogArtwork`、`ResourceCostBox`、`App.Scenes.SystemButtonArtwork`） | 320 宽（244 的框体）说明框：`BLDGXTRA` 相框帧 1、种族建筑 ICN 的 `CastleBuildingInfo.GetSpriteIndex` 帧、名字、说明、需求（`Requires:` 标题 + `GetRequirementString`）、价格框、OKAY/CANCEL。名字与说明按 `WrapLines` 折行（框架宽 244、行首 2 像素），说明框总高 = 10 + 相框高 + 10 + 说明高 + 10 +（需求标题 + 正文 + 10，仅当有需求）+ 价格高；内容超出就 `TryCreate` 返回 null。状态不是 `AllowBuild` 时把 `GetConditionDescription()` 追加在说明后面（相同则不重复），`BuildDisabled` 不显示需求行，OKAY 置灰（参考也是按钮照画、只是不进 `_result`）。价格框按 `Resource::BoxSprite`（`resource.cpp:603-664`）：图标 33×27、底对齐，数字在小字基线处居中；一行 45 像素、三列（`width/3`），剩余 1~2 项落在窄行（三列里占中格、两列里各占一格），行高 45/90/135。按钮取 `SYSTEM.ICN` 1/2（OKAY）与 3/4（CANCEL）；右键 OKAY 弹 `Okay` + 条件、右键 CANCEL 弹 `Cancel` + `Exit this menu without doing anything.`（都是 `Dialog::ZERO`，按住右键才留）。端口差异：进入这个框的入口仍是 `--town-building`，城镇屏点空地进建筑列表（`BUILD_STRUCTURE`）未接；按钮的禁用态没有专门的帧，与参考一致 |
| `src/fheroes2/castle/castle_town.cpp:171-800`（`Castle::_openConstructionDialog()`）+ `buildinginfo.cpp:318-412`（`BuildingInfo::Redraw`） | `FHeroes2.App.Scenes.ConstructionWindow`（+ `UI.Dialogs.ConstructionView`、`ConstructionLayout`、`ConstructionArtwork`、`ConstructionCell`、`CastleImagePainter`、`ConstructionInput`） | 整屏 640×480 的建造列表：`CASLWIND` 帧 0 铺底，没有船长时把整屏 `STONEBAK` 按 `(530, 163, 110, 84)` 裁一块贴上（`castle_town.cpp:204-208`），顶部 `Castle::DrawImageCastle` 的 5×5 瓦片（`OBJNTOWN` 种族基址 + 未建城堡 +16、`OBJNTWBA` 地面按地面 ×10），名字以小号白字居中在 `x=538`、`y=2`。19 格：六种住宅两行（`(5,2)`/`(149,2)`/`(293,2)` 与 `+75`）、法师塔 `(5,157)`（按等级取 `MageGuild1…5`）、酒馆 `(149,157)`（亡灵族旧地图上是空槽，参考同样留一个空壳）、盗贼公会 `(293,157)`、第二/三行住宅（`y=232`、`307`）、最后一排 `(5,387)` 含左塔/右塔/护城河，船长 `(444,165)`。每格是 `BLDGXTRA` 相框帧 0 → 种族建筑 ICN 的 `CastleBuildingInfo.GetSpriteIndex` 帧 → 名字底 `CASLXTRA`（可建帧 1、不可建帧 2，名字截到 125 像素写在 `y=61`）→ 右下角状态标记（已建勾 `y+58`、缺钱金币、不满足条件叉，船长格锚点 `(83,79)`）。船长建好后在 `(535,170)` 起写四维（`Skill::Primary::String` 的名字 + 值，行高 14）与 `(550,220)`/`(585,220)` 的两个阵型图标，选中的那个在 `-1,-1` 处叠 `HSICONS` 光标帧；两个英雄位是 `(443,260,102,93)`/`(443,363,102,94)`，有英雄时画 `PORT00xx` 帧 0 的 102×93 大肖像，没有时画 `STRIP` 帧 3 的空像；`!allow_buy_hero1/2` 时在右下角盖叉（`x+102-4+1-w`、`y+h-2-h`，灰化）。状态条、两个箭头与 EXIT 复用城堡屏的布局，资源面板画在 `(552,262)`、EXIT 在 `(553,428)`。点击只弹价格框，不直接扣钱（`Dialog::BuyBuilding( true )`）；阵型图标由窗口自己改 `Army.IsSpreadFormation`；左右换城由场景 `SwitchTo` 后重开窗口。命中优先级照参考：EXIT → 箭头 → 资源面板 → 格子 → 英雄位 → 阵型，其中「散开」图标压在船长格上——状态栏按参考先问格子（`castle_town.cpp:558`）再问图标（`:583`），所以那一小块显示船长格的状态；点击则要穿过已建好的船长格才落到图标上（`HitTest( forClick: true )`，对应 `QueueEventProcessing` 对已建格子返回 false）。未搬：英雄位的士气/幸运小图标（`PortraitRedraw` 的 `MINIMORAL`/`MINILUCK`）、招募窗口（点英雄位只上报）、右键英雄位的英雄信息框、换城的淡入 |
| `src/fheroes2/castle/castle_mageguild.cpp:143-212`（窗口的消息循环与返回） | `FHeroes2.App.Scenes.MageGuildWindow` | 把 `MageGuildDialog` 包成场景能用的窗口：`TryCreate` 取 `STONEBAK(_EVIL)`、`SMALLBAR`、`TOWNWIND`、`SPELLS`、退出按钮（`BUTTON_GUILDWELL_EXIT` 无独立档案，原始资源下取 `WELLXTRA` 0/1，`game_assets.cpp:904-911`）与种族 `MAGEGLD*` 的建筑帧，任一缺失返回 null 并打印各档案帧数；`SetCursor`/`SetLeftButton` 灌进 `LocalEvent`，`Update` 一帧消费一次并交回 `MageGuildFrame`（退出、换城、点法术），换城后场景重新 `TryCreate` 新城的窗口；`Draw` 画满 640×480（底板、建筑、五行卷轴、底栏、按钮、状态行）——状态行居中于 `StatusBar` 的 ROI（参考传高度 0）、纵向居状态条底板并裁在底板内 |
| `src/fheroes2/castle/castle_well.cpp:203-306`（`Castle::_openWell` 的循环） | `FHeroes2.App.Scenes.WellWindow` | 把 `WellDialog` 包成场景窗口：`TryCreate` 取 `WELLBKG`、`SMALLBAR`、`WELLXTRA` 的 EXIT 帧与种族 `CSTL*` 的第 19~30 帧（六个住宅图），任一缺失返回 null 并打印各档案帧数；`MAX` 按钮在原版资源里没有档案，照 `game_assets.cpp:4946-4976`+`1910-1917` 从 EXIT 帧擦掉文字再写 `MAX`（底色取那块区域里出现最多的调色板索引，参考用的是固定 RGB）；`SetCursor`/`SetLeftButton` 灌 `LocalEvent`，`Update` 一帧交回 `WellDialogFrame`（退出、MAX、点住宅、右键说明），`Draw` 画满 640×480（石面、六个格子、住宅、属性、底栏、两个按钮）；点住宅交回 `Recruit`（开 `RecruitWindow`）、`Max` 就地走 `CastleMaxRecruitment` 逐条征满后 `WellDialog.Create` 重建模型刷新数字 |
| `src/fheroes2/dialog/dialog_marketplace.cpp:380-685`（`Dialog::Marketplace()` 的循环本体） | `FHeroes2.App.Scenes.MarketplaceWindow` | 把 `MarketplaceView` 包成场景窗口（只盖屏幕中间 340 宽的石面，四周还看得见城镇屏）：`TryCreate` 取 `BUYBUILD`（外框六块）、`TRADPOST`、`MARKETPLACE_ICON`（原版 AGG 里没有，h2d 才有，缺了只是少一颗图标）、`RESOURCE` 与字形，任一外框/资源缺失返回 null 并打印各档案帧数；四颗按钮都按原始资源分支现拼——TRADE 与 EXIT 直接取 `TRADPOST` 第 15/16 与 17/18 帧（`game_assets.cpp:2196-2210`），GIFT 是把 EXIT 帧的标签区域擦成按钮底色再写白字（`game_assets.cpp:2240-2257`）；`SetCursor`/`SetLeftButton`/`SetRightButton` 灌 `LocalEvent`，`Update` 一帧交回 `MarketplaceAction`（`Exit`/`Trade`/`Gift`/`ExchangeRate`/`ResourceIncome`），`PressClose` 供 ESC/Enter/命令行用，`IsClosed` 由场景读；`Draw` 只画窗口本身（外框 + `MarketplaceView`），说明框由场景叠上去 | `--town-window market` 直接开这个窗口 |
| `src/fheroes2/castle/castle_well.cpp:263-271`、`castle_dialog.cpp:683-687` + `dialog/dialog_recruit.cpp:253-668`（住宅的招募窗口，水井与城镇屏共用一个外壳） | `FHeroes2.App.Scenes.RecruitWindow` | 把 `RecruitWindowView` 包成场景窗口（只盖屏幕中间，底下的水井屏/城镇屏由调用方先画）：`TryCreate` 取 `RECRBKG`、`BLDGXTRA`（帧 1）、`RECRUIT`、`RESOURCE`、怪物静态帧（`MonsterIcnIds.GetSpriteIcn` + `MonsterAnimInfoCache` 的 `Static` 首帧）与外框（`SURDRBKG`/`WINLOSE`/`STONEBAK`），任一缺失返回 null 并打印各档案帧数；四颗小按钮在原版资源里都没有独立档案，照 `game_assets.cpp` 现拼——OKAY=`SPANBTN` 0/1（1042-1056）、CANCEL=`CPANEL` 8/9（1020-1034）、MAX=`RECRUIT` 4/5 裁 `(5,0,60,25)`（2214-2239）、MIN 是把 MAX 的标签区域填平再写 `MIN`（2240-2257，参考用的是固定 RGB，端口取那块区域出现最多的调色板索引）。裁帧要**保留变换层**：阴影、高光和圆角都在那一层，丢掉就成了调色板噪声。换降级兵的两个箭头（`MONSTER_SWITCH_*_ARROW`）原版也没有档案，由 `RECRUIT` 的上下箭头转置再上下翻转现拼（`game_assets.cpp:3022-3042`），端口搬成 `Sprite.Transpose()`；两个调用方都传 `allowDowngradedMonster = true`，住宅没升级时 `RecruitDialog.CanSwitchMonster` 自己就把箭头藏起来。窗口下移量随调用方：水井 `2`、城镇屏 `-65`（`WellWindowOffsetY`/`CastleWindowOffsetY`）。`Update` 一帧交回 `RecruitWindowFrame`，OKAY 时把选中队伍交给场景、ESC/Return 当取消（`Selected` 为 null） |
| `src/fheroes2/gui/interface_events.cpp:1652-1659`（地图上点自己的城） | `FHeroes2.App.Scenes.AdventureMapScene.TakeCastleRequest` + `GameHost.TryOpenCastleFromAdventure` | 参考是两段式：第一次点聚焦、再点一次才 `Game::OpenCastleDialog`；端口先简化成一次按下就交出城。只认本王国（`Color == CurrentColor`）、位置在视口内，命中问 `World.GetCastle(x, y)`（整块 footprint 都算）。场景不自己开屏，由宿主换到 `CastleScene` 再换回来 |

## 计划中

> 这里只排长期未动的项；未转清单的完整名单（2026-11-21 实测：85 个整文件 / 约 4.3 万行，另有「部分文件里点明的缺口」）与「还差的主干」的先后顺序见 `PLAN.md` 同一节。

| C++ | 计划 C# | 里程碑 | 备注 |
| --- | --- | --- | --- |
| `src/engine/image_tool.cpp`（图像缩放余量） | `ImageOps.Resize`（最近邻，颜色层与透明度层各一份，`engine/image.cpp:3087`） | 未排期 | 单层与双层两条路径已搬；「输出带透明度、输入不带」那条按 `transformTable` 反查的分支未搬（无全量 `transformTable` 数据） |
| `src/engine/audio/*`（`audio_xmi2mid`、`mus`、`xmi`）、`82M` 解码 | `Assets/Audio/*` | M7 | 音乐/音效播放已有 `AudioPlayer`，现在只能播 AGG 里现成的音轨；转换与 82M 是战斗与过场才要 |
| `src/fheroes2/gui/ui_font.cpp`（各代码页的 `generateCP1250GoodButtonFont` … 补充字形）与 `SubpixelResize` | `Game.Gui.ButtonFont`、`Core.Imaging.ImageOps` | 未排期 | 只有非英文资材的按钮字形需要；`SubpixelResize` 目前没有调用者 |
| `src/fheroes2/gui/ui_language.*` 的语言切换部分 | `Assets.Localization.*` | 未排期 | `getResourceLanguage` 已搬；运行时换资材语言还没入口（`--lang` / `F2` 只换 `.mo`） |
| `src/fheroes2/world/*`、`army`、`castle`、`kingdom`、`battle` | `FHeroes2.Game.*` | M5–M7 | 顺序见 `PLAN.md`「还差的主干」：`world` → `kingdom` → `castle` → `army` → `battle` |
| `src/engine/serialize.h` | `Game/Persistence/*`（自研 schema） | M8 | 不做与原版互通 |

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
