namespace FHeroes2.UI.Interface;

/// <summary>
/// What has to be repainted on the adventure screen, the <c>REDRAW_*</c> masks of
/// <c>src/fheroes2/gui/interface_base.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>interface_base.h:21-46</c> 的九个 <c>REDRAW_*</c> 常量，其中 <c>REDRAW_ICONS</c> 是
/// <c>REDRAW_HEROES | REDRAW_CASTLES</c> 的组合，<c>REDRAW_ALL</c> 是全部。
/// </para>
/// <para>
/// <b>为什么是掩码而不是一个「重画」方法。</b>参考的 <c>Interface::BaseInterface::Redraw( int32_t force )</c>
/// 按位取并集，谁改了地图谁只立自己那一位，最后由 <c>Interface::Basic::Redraw</c> 一次性判断。端口现在只有一个
/// 场景，逐帧重画整屏（<c>AdventureMapScene.DrawFrame</c> 开头就 <c>InvalidateAll</c>），所以这一批只搬掩码
/// 本身：后面接上局部重画时，规则层与界面层用同一套位就能对上，不必再回头改调用方。
/// </para>
/// <para>
/// 标志组合成 <see cref="uint"/> 而不是 <see langword="enum"/> 的底层类型，因为参考传的是 <c>int32_t</c>，
/// 且 <c>REDRAW_PASSABILITIES</c> 在 0x200 位上，离符号位还很远。
/// </para>
/// </remarks>
[Flags]
public enum InterfaceRedraw : uint
{
    /// <summary>Nothing to repaint.</summary>
    None = 0,

    /// <summary>The cursor on the radar, <c>REDRAW_RADAR_CURSOR</c>.</summary>
    RadarCursor = 0x01,

    /// <summary>The minimap itself, <c>REDRAW_RADAR</c>.</summary>
    Radar = 0x02,

    /// <summary>The frame art, <c>REDRAW_BORDER</c>.</summary>
    Border = 0x04,

    /// <summary>The terrain between the borders, <c>REDRAW_GAMEAREA</c>.</summary>
    GameArea = 0x08,

    /// <summary>The status panel under the buttons, <c>REDRAW_STATUS</c>.</summary>
    Status = 0x10,

    /// <summary>The hero list of the icons bar, <c>REDRAW_HEROES</c>.</summary>
    Heroes = 0x20,

    /// <summary>The castle list of the icons bar, <c>REDRAW_CASTLES</c>.</summary>
    Castles = 0x40,

    /// <summary>The icon bar, <c>REDRAW_ICONS</c>.</summary>
    Icons = Heroes | Castles,

    /// <summary>The button panel, <c>REDRAW_BUTTONS</c>.</summary>
    Buttons = 0x80,

    /// <summary>The whole right hand panel, <c>REDRAW_PANEL</c>.</summary>
    Panel = 0x100,

    /// <summary>Everything on screen, <c>REDRAW_ALL</c>.</summary>
    All = RadarCursor | Radar | Border | GameArea | Status | Icons | Buttons | Panel,

    /// <summary>
    /// Which tiles a hero can walk on, <c>REDRAW_PASSABILITIES</c>: it repaints the terrain only, but the
    /// reference keeps it a separate bit so the overlay can be removed without a full repaint.
    /// </summary>
    Passabilities = 0x200,
}
